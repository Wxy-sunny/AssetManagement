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
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	[ApiController]
	[Route("user")]
	[Authorize]
	public class UserController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;
		private readonly AssetManagementSystem.Services.ICacheService _cache;

		public UserController(DbServer dbServer, AssetManagementSystem.Services.ICacheService cache)
		{
			_dbServer = dbServer;
			_cache = cache;
			_db = dbServer.Use(DbNames.Asset);
		}

		private static string PageKey(string? keyword, string? roleId, string? deptId, int pageIndex, int pageSize)
			=> $"ams:user:page:{keyword}|{roleId}|{deptId}|{pageIndex}|{pageSize}";

		/// <summary>
		/// 分页查询用户
		/// </summary>
		[HttpGet("page")]
		[Permission(Permissions.UserList)]
		public async Task<ApiResult<PageResult<UserDto>>> GetPage(
			[FromQuery] string? keyword = null,
			[FromQuery] string? roleId = null,
			[FromQuery] string? deptId = null,
			[FromQuery] int pageIndex = 1,
			[FromQuery] int pageSize = 10)
		{
			(pageIndex, pageSize) = PageGuard.Normalize(pageIndex, pageSize);

			var key = PageKey(keyword, roleId, deptId, pageIndex, pageSize);
			var result = await _cache.GetOrSetAsync<PageResult<UserDto>>(
				key,
				() => BuildUserPageAsync(keyword, roleId, deptId, pageIndex, pageSize),
				TimeSpan.FromMinutes(5));
			return ApiResult<PageResult<UserDto>>.Success(result, "获取成功");
		}

		private async Task<PageResult<UserDto>> BuildUserPageAsync(string? keyword, string? roleId, string? deptId, int pageIndex, int pageSize)
		{
			var total = 0;
			var list = await _db.Queryable<SysUser>()
				.WhereIF(!string.IsNullOrWhiteSpace(keyword), u =>
					u.UserId.Contains(keyword) ||
					u.UserName.Contains(keyword) ||
					u.NickName.Contains(keyword) ||
					u.Mobile.Contains(keyword))
				.WhereIF(!string.IsNullOrWhiteSpace(roleId), u => u.RoleId == roleId)
				.WhereIF(!string.IsNullOrWhiteSpace(deptId), u => u.DeptId == deptId)
				.OrderBy(u => u.CreateTime, OrderByType.Desc)
				.ToPageListAsync(pageIndex, pageSize, total);

			var dtoList = new List<UserDto>();
			foreach (var u in list)
			{
				var dto = MapToDto(u);
				dto.deptName = await ResolveDeptName(u.DeptId);
				dtoList.Add(dto);
			}

			return new PageResult<UserDto>(dtoList, total, pageIndex, pageSize);
		}

		/// <summary>
		/// 用户详情
		/// </summary>
		[HttpGet("{id}")]
		[Permission(Permissions.UserList)]
		public async Task<ApiResult<UserDto>> GetById(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<UserDto>.Fail("400", "用户ID不能为空");

			var user = await _db.Queryable<SysUser>().FirstAsync(u => u.Id == id);
			if (user == null)
				return ApiResult<UserDto>.Fail("404", "用户不存在");

			var dto = MapToDto(user);
			dto.deptName = await ResolveDeptName(user.DeptId);
			return ApiResult<UserDto>.Success(dto, "获取成功");
		}

		/// <summary>
		/// 新增用户
		/// </summary>
		[HttpPost]
		[Permission(Permissions.UserManage)]
		public async Task<ApiResult<object>> Add([FromBody] UserDto dto)
		{
			if (dto == null)
				return ApiResult<object>.Fail("400", "参数不能为空");
			if (string.IsNullOrWhiteSpace(dto.userId))
				return ApiResult<object>.Fail("400", "登录账号不能为空");
			if (string.IsNullOrWhiteSpace(dto.password))
				return ApiResult<object>.Fail("400", "密码不能为空");

			var exist = await _db.Queryable<SysUser>().AnyAsync(u => u.UserId == dto.userId);
			if (exist)
				return ApiResult<object>.Fail("400", "该登录账号已存在");

			// 手机号需唯一（手机号 + 验证码登录依赖该字段定位账户）
			if (!string.IsNullOrWhiteSpace(dto.mobile))
			{
				var mobileUsed = await _db.Queryable<SysUser>().AnyAsync(u => u.Mobile == dto.mobile);
				if (mobileUsed)
					return ApiResult<object>.Fail("400", "该手机号已绑定其他账户");
			}

			var entity = new SysUser
			{
				Id = Guid.NewGuid().ToString(),
				UserId = dto.userId.Trim(),
				UserName = dto.userName,
				NickName = dto.nickName,
				PassWord = PasswordHelper.Hash(dto.password), // BCrypt 哈希存储
				Email = dto.email,
				Mobile = dto.mobile,
				RoleId = dto.roleId,
				RoleName = await ResolveRoleName(dto.roleId, dto.roleName),
				DeptId = dto.deptId,
				IsEnabled = dto.isEnabled ?? true,
				StatusCode = 1,
				StatusDesc = "启用",
				CreateTime = DateTime.Now
			};

			var count = await _db.Insertable(entity).ExecuteCommandAsync();
			if (count > 0)
			{
				await _cache.RemoveByPrefixAsync(CacheKeys.UserPagePrefix);
				await _cache.RemoveAsync(CacheKeys.RoleList);
				return ApiResult<object>.Success(new { id = entity.Id }, "新增成功");
			}
			return ApiResult<object>.Fail("500", "新增失败");
		}

		/// <summary>
		/// 修改用户（password 留空表示不修改密码）
		/// </summary>
		[HttpPut]
		[Permission(Permissions.UserManage)]
		public async Task<ApiResult<object>> Update([FromBody] UserDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.id))
				return ApiResult<object>.Fail("400", "用户ID不能为空");
			if (string.IsNullOrWhiteSpace(dto.userId))
				return ApiResult<object>.Fail("400", "登录账号不能为空");

			var entity = await _db.Queryable<SysUser>().FirstAsync(u => u.Id == dto.id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "用户不存在");

			var exist = await _db.Queryable<SysUser>()
				.AnyAsync(u => u.UserId == dto.userId && u.Id != dto.id);
			if (exist)
				return ApiResult<object>.Fail("400", "该登录账号已存在");

			// 手机号需唯一（排除自身）
			if (!string.IsNullOrWhiteSpace(dto.mobile))
			{
				var mobileUsed = await _db.Queryable<SysUser>()
					.AnyAsync(u => u.Mobile == dto.mobile && u.Id != dto.id);
				if (mobileUsed)
					return ApiResult<object>.Fail("400", "该手机号已绑定其他账户");
			}

			entity.UserId = dto.userId.Trim();
			entity.UserName = dto.userName;
			entity.NickName = dto.nickName;
			entity.Email = dto.email;
			entity.Mobile = dto.mobile;
			entity.RoleId = dto.roleId;
			entity.RoleName = await ResolveRoleName(dto.roleId, dto.roleName);
			entity.DeptId = dto.deptId;
			entity.IsEnabled = dto.isEnabled ?? true;
			entity.UpdateTime = DateTime.Now;

			// 密码留空则不更新
			if (!string.IsNullOrWhiteSpace(dto.password))
				entity.PassWord = PasswordHelper.Hash(dto.password);

			var count = await _db.Updateable(entity).ExecuteCommandAsync();
			if (count > 0)
			{
				await _cache.RemoveByPrefixAsync(CacheKeys.UserPagePrefix);
				await _cache.RemoveAsync(CacheKeys.RoleList);
				return ApiResult<object>.Success(null, "修改成功");
			}
			return ApiResult<object>.Fail("500", "修改失败");
		}

		/// <summary>
		/// 删除用户
		/// </summary>
		[HttpDelete("{id}")]
		[Permission(Permissions.UserManage)]
		public async Task<ApiResult<object>> Delete(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "用户ID不能为空");

			var entity = await _db.Queryable<SysUser>().FirstAsync(u => u.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "用户不存在");

			var count = await _db.Deleteable(entity).ExecuteCommandAsync();
			if (count > 0)
			{
				await _cache.RemoveByPrefixAsync(CacheKeys.UserPagePrefix);
				await _cache.RemoveAsync(CacheKeys.RoleList);
				return ApiResult<object>.Success(null, "删除成功");
			}
			return ApiResult<object>.Fail("500", "删除失败");
		}

		/// <summary>
		/// 启用 / 禁用用户
		/// </summary>
		[HttpPut("{id}/enabled")]
		[Permission(Permissions.UserManage)]
		public async Task<ApiResult<object>> UpdateEnabled(string id, [FromBody] EnabledUpdateDto dto)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "用户ID不能为空");
			if (dto == null)
				return ApiResult<object>.Fail("400", "参数不能为空");

			var entity = await _db.Queryable<SysUser>().FirstAsync(u => u.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "用户不存在");

			entity.IsEnabled = dto.isEnabled;
			entity.StatusCode = dto.isEnabled ? (short)1 : (short)0;
			entity.StatusDesc = dto.isEnabled ? "启用" : "禁用";
			entity.UpdateTime = DateTime.Now;

			var count = await _db.Updateable(entity)
				.UpdateColumns(u => new { u.IsEnabled, u.StatusCode, u.StatusDesc, u.UpdateTime })
				.ExecuteCommandAsync();

			if (count > 0)
			{
				await _cache.RemoveByPrefixAsync(CacheKeys.UserPagePrefix);
				return ApiResult<object>.Success(null, dto.isEnabled ? "已启用" : "已禁用");
			}
			return ApiResult<object>.Fail("500", "操作失败");
		}

		/// <summary>
		/// 重置用户密码
		/// </summary>
		[HttpPut("{id}/reset-password")]
		[Permission(Permissions.UserManage)]
		public async Task<ApiResult<object>> ResetPassword(string id, [FromBody] PasswordResetDto dto)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "用户ID不能为空");
			if (dto == null || string.IsNullOrWhiteSpace(dto.password))
				return ApiResult<object>.Fail("400", "新密码不能为空");

			var entity = await _db.Queryable<SysUser>().FirstAsync(u => u.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "用户不存在");

			entity.PassWord = PasswordHelper.Hash(dto.password);
			entity.UpdateTime = DateTime.Now;

			var count = await _db.Updateable(entity)
				.UpdateColumns(u => new { u.PassWord, u.UpdateTime })
				.ExecuteCommandAsync();

			if (count > 0)
			{
				await _cache.RemoveByPrefixAsync(CacheKeys.UserPagePrefix);
				return ApiResult<object>.Success(null, "密码重置成功");
			}
			return ApiResult<object>.Fail("500", "密码重置失败");
		}

		private UserDto MapToDto(SysUser entity)
		{
			return new UserDto
			{
				id = entity.Id,
				userId = entity.UserId,
				userName = entity.UserName,
				nickName = entity.NickName,
				email = entity.Email,
				mobile = entity.Mobile,
				roleId = entity.RoleId,
				roleName = entity.RoleName,
				deptId = entity.DeptId,
				isEnabled = entity.IsEnabled ?? true,
				createTime = entity.CreateTime,
				lastLoginTime = entity.LastLoginTime
			};
		}

		private async Task<string?> ResolveRoleName(string? roleId, string? fallback)
		{
			if (!string.IsNullOrWhiteSpace(fallback)) return fallback;
			if (string.IsNullOrWhiteSpace(roleId)) return null;

			var role = await _db.Queryable<SysRole>().FirstAsync(r => r.RoleId == roleId);
			return role?.RoleName;
		}

		private async Task<string?> ResolveDeptName(string? deptId)
		{
			if (string.IsNullOrWhiteSpace(deptId)) return null;

			var dept = await _db.Queryable<Department>().FirstAsync(d => d.Id == deptId);
			return dept?.DepartmentName;
		}

		// ==================== 用户个性化配置（如登录页布局） ====================

		/// <summary>
		/// 读取当前用户的某项个性化配置
		/// 说明：不限制权限（仅 [Authorize]），用户只能读自己的配置；未设置时返回 data = null。
		/// </summary>
		[HttpGet("preference")]
		public async Task<ApiResult<string?>> GetPreference([FromQuery] string? key = null)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
				?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
			if (string.IsNullOrWhiteSpace(userId))
				return ApiResult<string?>.Fail("401", "登录信息无效，请重新登录");
			if (string.IsNullOrWhiteSpace(key) || key.Length > 50)
				return ApiResult<string?>.Fail("400", "配置键不能为空且不超过 50 字符");

			var pref = await _db.Queryable<UserPreference>()
				.FirstAsync(p => p.UserId == userId && p.PrefKey == key);
			return ApiResult<string?>.Success(pref?.PrefValue, "获取成功");
		}

		/// <summary>
		/// 保存当前用户的某项个性化配置（upsert：存在则更新，不存在则插入）
		/// </summary>
		[HttpPut("preference")]
		public async Task<ApiResult<object>> SavePreference([FromBody] UserPreferenceDto dto)
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
				?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
			if (string.IsNullOrWhiteSpace(userId))
				return ApiResult<object>.Fail("401", "登录信息无效，请重新登录");
			if (dto == null || string.IsNullOrWhiteSpace(dto.prefKey) || dto.prefKey.Length > 50)
				return ApiResult<object>.Fail("400", "配置键不能为空且不超过 50 字符");
			if (dto.prefValue == null || dto.prefValue.Length > 255)
				return ApiResult<object>.Fail("400", "配置值不能为空且不超过 255 字符");

			var now = DateTime.Now;
			var existing = await _db.Queryable<UserPreference>()
				.FirstAsync(p => p.UserId == userId && p.PrefKey == dto.prefKey);

			if (existing != null)
			{
				existing.PrefValue = dto.prefValue;
				existing.UpdateTime = now;
				existing.UpdateUserId = userId;
				await _db.Updateable(existing)
					.UpdateColumns(p => new { p.PrefValue, p.UpdateTime, p.UpdateUserId })
					.ExecuteCommandAsync();
			}
			else
			{
				var entity = new UserPreference
				{
					UserId = userId,
					PrefKey = dto.prefKey,
					PrefValue = dto.prefValue,
					CreateUserId = userId,
					CreateTime = now,
					UpdateTime = now
				};
				try
				{
					await _db.Insertable(entity).ExecuteCommandAsync();
				}
				catch
				{
					// 并发首次保存可能触发 (user_id, pref_key) 唯一索引冲突：另一请求已插入，
					// 这里改为更新，保证最终以最后一次保存为准
					existing = await _db.Queryable<UserPreference>()
						.FirstAsync(p => p.UserId == userId && p.PrefKey == dto.prefKey);
					if (existing != null)
					{
						existing.PrefValue = dto.prefValue;
						existing.UpdateTime = now;
						existing.UpdateUserId = userId;
						await _db.Updateable(existing)
							.UpdateColumns(p => new { p.PrefValue, p.UpdateTime, p.UpdateUserId })
							.ExecuteCommandAsync();
					}
				}
			}

			return ApiResult<object>.Success(null, "保存成功");
		}
	}
}
