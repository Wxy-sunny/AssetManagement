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
		private readonly AssetManagementSystem.Services.ICacheService _cache;

		private const string ListKey = CacheKeys.RoleList;

		public RoleController(DbServer dbServer, AssetManagementSystem.Services.ICacheService cache)
		{
			_dbServer = dbServer;
			_cache = cache;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 角色列表（下拉选择器用，附带用户数）
		/// </summary>
		[HttpGet("list")]
		[Permission(Permissions.RoleList)]
		public async Task<ApiResult<List<RoleDto>>> GetList([FromQuery] string? keyword = null)
		{
			if (!string.IsNullOrWhiteSpace(keyword))
			{
				return ApiResult<List<RoleDto>>.Success(await BuildRoleListAsync(keyword), "获取成功");
			}

			var list = await _cache.GetOrSetAsync<List<RoleDto>>(
				ListKey,
				() => BuildRoleListAsync(null),
				TimeSpan.FromMinutes(10));
			return ApiResult<List<RoleDto>>.Success(list, "获取成功");
		}

		private async Task<List<RoleDto>> BuildRoleListAsync(string? keyword)
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
			return dtoList;
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
			(pageIndex, pageSize) = PageGuard.Normalize(pageIndex, pageSize);

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
			if (count > 0)
			{
				await _cache.RemoveAsync(ListKey);
				return ApiResult<object>.Success(new { id = entity.Id }, "新增成功");
			}
			return ApiResult<object>.Fail("500", "新增失败");
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

			// 角色更新 + 用户表冗余角色名同步，包在同一事务，避免出现"角色已改名但用户表未同步"的不一致
			var count = 0;
			try
			{
				_db.Ado.BeginTran();

				count = await _db.Updateable(entity).ExecuteCommandAsync();
				if (count > 0)
				{
					// 同步刷新用户表冗余的角色名称
					await _db.Updateable<SysUser>()
						.SetColumns(u => u.RoleName == entity.RoleName)
						.Where(u => u.RoleId == entity.RoleId)
						.ExecuteCommandAsync();
				}

				_db.Ado.CommitTran();
				}
				catch (Exception ex)
				{
				_db.Ado.RollbackTran();
				return ApiResult<object>.Fail("500", $"修改失败：{ex.Message}");
				}

			if (count > 0)
			{
				// 角色名变化会同步到用户表冗余字段，一并失效角色列表与用户分页缓存
				await _cache.RemoveAsync(ListKey);
				await _cache.RemoveByPrefixAsync(CacheKeys.UserPagePrefix);
				return ApiResult<object>.Success(null, "修改成功");
			}
			return ApiResult<object>.Fail("500", "修改失败");
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
			if (count > 0)
			{
				await _cache.RemoveAsync(ListKey);
				return ApiResult<object>.Success(null, "删除成功");
			}
			return ApiResult<object>.Fail("500", "删除失败");
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
