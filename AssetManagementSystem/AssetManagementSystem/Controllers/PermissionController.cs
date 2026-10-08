using AssetManagementSystem.Attributes;
using AssetManagementSystem.Dtos;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	[ApiController]
	[Route("permission")]
	[Authorize]
	public class PermissionController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;

		public PermissionController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 权限树：所有可分配的权限，按模块分组（供角色分配时渲染）
		/// </summary>
		[HttpGet("tree")]
		[Permission(Permissions.RoleList)]
		public ApiResult<List<PermissionGroupDto>> GetTree()
		{
			return ApiResult<List<PermissionGroupDto>>.Success(Permissions.GetGroups(), "获取成功");
		}

		/// <summary>
		/// 查询某角色已分配的权限编码
		/// </summary>
		/// <param name="roleId">角色编码（SysRole.RoleId）</param>
		[HttpGet("role/{roleId}")]
		[Permission(Permissions.RoleList)]
		public async Task<ApiResult<List<string>>> GetRolePermissions(string roleId)
		{
			if (string.IsNullOrWhiteSpace(roleId))
				return ApiResult<List<string>>.Fail("400", "角色不能为空");

			var list = await _db.Queryable<SysRolePermission>()
				.Where(p => p.RoleId == roleId)
				.Select(p => p.PermissionCode)
				.ToListAsync();

			return ApiResult<List<string>>.Success(list, "获取成功");
		}

		/// <summary>
		/// 给角色分配权限（全量覆盖：以本次提交的权限集合为准）
		/// </summary>
		[HttpPost("assign")]
		[Permission(Permissions.RoleManage)]
		public async Task<ApiResult<object>> Assign([FromBody] AssignPermissionDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.roleId))
				return ApiResult<object>.Fail("400", "角色不能为空");

			var role = await _db.Queryable<SysRole>().FirstAsync(r => r.RoleId == dto.roleId);
			if (role == null)
				return ApiResult<object>.Fail("404", "角色不存在");

			// 只保留代码中已定义的权限，过滤掉无效编码
			var codes = (dto.permissionCodes ?? new List<string>())
				.Where(c => Permissions.All.Contains(c))
				.Distinct()
				.ToList();

			// 超级管理员角色无需配置权限（默认全权限），这里仍允许保存以便展示
			await _db.Deleteable<SysRolePermission>()
				.Where(p => p.RoleId == dto.roleId)
				.ExecuteCommandAsync();

			if (codes.Count > 0)
			{
				var entities = codes.Select(code => new SysRolePermission
				{
					Id = Guid.NewGuid().ToString(),
					RoleId = dto.roleId,
					PermissionCode = code,
					CreateTime = DateTime.Now
				}).ToList();

				await _db.Insertable(entities).ExecuteCommandAsync();
			}

			return ApiResult<object>.Success(null, $"权限分配成功，共 {codes.Count} 项");
		}
	}
}
