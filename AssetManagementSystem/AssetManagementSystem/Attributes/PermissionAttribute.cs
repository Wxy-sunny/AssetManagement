using AssetManagementSystem.Models;
using AssetManagementSystem.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SqlSugar;
using System.Security.Claims;

namespace AssetManagementSystem.Attributes
{
	/// <summary>
	/// 权限校验特性
	/// 用法：[Permission(Permissions.AssetAdd)]
	/// 说明：超级管理员角色（SysRole.IsAdmin = true）默认放行，拥有全部权限
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
	public class PermissionAttribute : TypeFilterAttribute
	{
		public PermissionAttribute(string code) : base(typeof(PermissionFilter))
		{
			Arguments = new object[] { code };
		}
	}

	/// <summary>
	/// 权限校验过滤器：从 JWT 中取角色，判断该角色是否拥有目标权限
	/// </summary>
	public class PermissionFilter : IAsyncActionFilter
	{
		private readonly string _code;
		private readonly ISqlSugarClient _db;

		public PermissionFilter(string code, ISqlSugarClient db)
		{
			_code = code;
			_db = db;
		}

		public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
		{
			var user = context.HttpContext.User;
			var roleId = user?.FindFirst(ClaimTypes.Role)?.Value;

			if (string.IsNullOrWhiteSpace(roleId))
			{
				// 未认证的请求交由 [Authorize] 处理，这里只做兜底
				context.Result = new ObjectResult(ApiResult<object>.Fail("401", "未登录或登录信息无效"))
				{
					StatusCode = 401
				};
				return;
			}

			// 超级管理员角色拥有全部权限，直接放行
			var role = await _db.Queryable<SysRole>().FirstAsync(r => r.RoleId == roleId);
			if (role != null && role.IsAdmin == true)
			{
				await next();
				return;
			}

			var hasPermission = await _db.Queryable<SysRolePermission>()
				.AnyAsync(p => p.RoleId == roleId && p.PermissionCode == _code);

			if (hasPermission)
			{
				await next();
				return;
			}

			context.Result = new ObjectResult(ApiResult<object>.Fail("403", "没有该功能的操作权限"))
			{
				StatusCode = 403
			};
		}
	}
}
