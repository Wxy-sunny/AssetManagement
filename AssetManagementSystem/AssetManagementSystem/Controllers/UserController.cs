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
	[Route("user")]
	[Authorize]
	public class UserController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;

		public UserController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
		}

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
			if (pageIndex < 1) pageIndex = 1;
			if (pageSize < 1) pageSize = 10;

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

			var result = new PageResult<UserDto>(dtoList, total, pageIndex, pageSize);
			return ApiResult<PageResult<UserDto>>.Success(result, "获取成功");
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
			return count > 0
				? ApiResult<object>.Success(new { id = entity.Id }, "新增成功")
				: ApiResult<object>.Fail("500", "新增失败");
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
			return count > 0
				? ApiResult<object>.Success(null, "修改成功")
				: ApiResult<object>.Fail("500", "修改失败");
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
			return count > 0
				? ApiResult<object>.Success(null, "删除成功")
				: ApiResult<object>.Fail("500", "删除失败");
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

			return count > 0
				? ApiResult<object>.Success(null, dto.isEnabled ? "已启用" : "已禁用")
				: ApiResult<object>.Fail("500", "操作失败");
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

			return count > 0
				? ApiResult<object>.Success(null, "密码重置成功")
				: ApiResult<object>.Fail("500", "密码重置失败");
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
	}
}
