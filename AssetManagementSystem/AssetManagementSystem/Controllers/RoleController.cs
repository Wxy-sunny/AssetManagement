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
	[Route("role")]
	[Authorize]
	public class RoleController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;

		public RoleController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 角色列表（下拉选择器用，附带用户数）
		/// </summary>
		[HttpGet("list")]
		[Permission(Permissions.RoleList)]
		public async Task<ApiResult<List<RoleDto>>> GetList([FromQuery] string? keyword = null)
		{
			var list = await _db.Queryable<SysRole>()
				.WhereIF(!string.IsNullOrWhiteSpace(keyword), r =>
					r.RoleId.Contains(keyword) || r.RoleName.Contains(keyword))
				.OrderBy(r => r.RoleId)
				.ToListAsync();

			var dtoList = new List<RoleDto>();
			foreach (var r in list)
			{
				var dto = MapToDto(r);
				dto.userCount = await _db.Queryable<SysUser>().CountAsync(u => u.RoleId == r.RoleId);
				dtoList.Add(dto);
			}

			return ApiResult<List<RoleDto>>.Success(dtoList, "获取成功");
		}

		/// <summary>
		/// 角色分页
		/// </summary>
		[HttpGet("page")]
		[Permission(Permissions.RoleList)]
		public async Task<ApiResult<PageResult<RoleDto>>> GetPage(
			[FromQuery] string? keyword = null,
			[FromQuery] int pageIndex = 1,
			[FromQuery] int pageSize = 10)
		{
			if (pageIndex < 1) pageIndex = 1;
			if (pageSize < 1) pageSize = 10;

			var total = 0;
			var list = await _db.Queryable<SysRole>()
				.WhereIF(!string.IsNullOrWhiteSpace(keyword), r =>
					r.RoleId.Contains(keyword) || r.RoleName.Contains(keyword))
				.OrderBy(r => r.RoleId)
				.ToPageListAsync(pageIndex, pageSize, total);

			var dtoList = new List<RoleDto>();
			foreach (var r in list)
			{
				var dto = MapToDto(r);
				dto.userCount = await _db.Queryable<SysUser>().CountAsync(u => u.RoleId == r.RoleId);
				dtoList.Add(dto);
			}

			var result = new PageResult<RoleDto>(dtoList, total, pageIndex, pageSize);
			return ApiResult<PageResult<RoleDto>>.Success(result, "获取成功");
		}

		/// <summary>
		/// 新增角色
		/// </summary>
		[HttpPost]
		[Permission(Permissions.RoleManage)]
		public async Task<ApiResult<object>> Add([FromBody] RoleDto dto)
		{
			if (dto == null)
				return ApiResult<object>.Fail("400", "参数不能为空");
			if (string.IsNullOrWhiteSpace(dto.roleId))
				return ApiResult<object>.Fail("400", "角色编码不能为空");
			if (string.IsNullOrWhiteSpace(dto.roleName))
				return ApiResult<object>.Fail("400", "角色名称不能为空");

			var exist = await _db.Queryable<SysRole>().AnyAsync(r => r.RoleId == dto.roleId);
			if (exist)
				return ApiResult<object>.Fail("400", "该角色编码已存在");

			var entity = new SysRole
			{
				Id = Guid.NewGuid().ToString(),
				RoleId = dto.roleId.Trim(),
				RoleName = dto.roleName.Trim(),
				Description = dto.description ?? "",
				IsEnabled = dto.isEnabled ?? true,
				IsAdmin = dto.isAdmin ?? false,
				StatusCode = 1,
				StatusDesc = "启用",
				CreateTime = DateTime.Now
			};

			var count = await _db.Insertable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(new { id = entity.Id }, "新增成功")
				: ApiResult<object>.Fail("500", "新增失败");
		}

		/// <summary>
		/// 修改角色
		/// </summary>
		[HttpPut]
		[Permission(Permissions.RoleManage)]
		public async Task<ApiResult<object>> Update([FromBody] RoleDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.id))
				return ApiResult<object>.Fail("400", "角色ID不能为空");
			if (string.IsNullOrWhiteSpace(dto.roleName))
				return ApiResult<object>.Fail("400", "角色名称不能为空");

			var entity = await _db.Queryable<SysRole>().FirstAsync(r => r.Id == dto.id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "角色不存在");

			// 编码不允许重复（排除自身）
			var exist = await _db.Queryable<SysRole>()
				.AnyAsync(r => r.RoleId == dto.roleId && r.Id != dto.id);
			if (exist)
				return ApiResult<object>.Fail("400", "该角色编码已存在");

			entity.RoleId = dto.roleId?.Trim() ?? entity.RoleId;
			entity.RoleName = dto.roleName.Trim();
			entity.Description = dto.description;
			entity.IsEnabled = dto.isEnabled ?? true;
			entity.IsAdmin = dto.isAdmin ?? false;
			entity.UpdateTime = DateTime.Now;

			var count = await _db.Updateable(entity).ExecuteCommandAsync();

			// 同步刷新用户表冗余的角色名称
			if (count > 0)
			{
				await _db.Updateable<SysUser>()
					.SetColumns(u => u.RoleName == entity.RoleName)
					.Where(u => u.RoleId == entity.RoleId)
					.ExecuteCommandAsync();
			}

			return count > 0
				? ApiResult<object>.Success(null, "修改成功")
				: ApiResult<object>.Fail("500", "修改失败");
		}

		/// <summary>
		/// 删除角色（存在用户引用时拒绝删除）
		/// </summary>
		[HttpDelete("{id}")]
		[Permission(Permissions.RoleManage)]
		public async Task<ApiResult<object>> Delete(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "角色ID不能为空");

			var entity = await _db.Queryable<SysRole>().FirstAsync(r => r.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "角色不存在");

			var used = await _db.Queryable<SysUser>().AnyAsync(u => u.RoleId == entity.RoleId);
			if (used)
				return ApiResult<object>.Fail("400", "该角色下仍有用户，不能删除");

			var count = await _db.Deleteable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "删除成功")
				: ApiResult<object>.Fail("500", "删除失败");
		}

		private RoleDto MapToDto(SysRole entity)
		{
			return new RoleDto
			{
				id = entity.Id,
				roleId = entity.RoleId,
				roleName = entity.RoleName,
				description = entity.Description,
				isEnabled = entity.IsEnabled ?? true,
				isAdmin = entity.IsAdmin ?? false,
				createTime = entity.CreateTime
			};
		}
	}
}
