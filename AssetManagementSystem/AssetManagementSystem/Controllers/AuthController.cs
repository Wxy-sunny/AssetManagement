using AssetManagementSystem.Dtos;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SqlSugar;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetManagementSystem.Controllers
{
	[ApiController]
	[Route("auth")]
	public class AuthController : ControllerBase
	{
		private readonly ISqlSugarClient _db;
		private readonly IConfiguration _configuration;
		private readonly SmsCodeService _smsCodeService;

		public AuthController(
			ISqlSugarClient db,
			IConfiguration configuration,
			SmsCodeService smsCodeService)
		{
			_db = db;
			_configuration = configuration;
			_smsCodeService = smsCodeService;
		}

		/// <summary>
		/// 账号密码登录（支持登录账号 UserId 或姓名 UserName）
		/// </summary>
		[HttpPost("login")]
		public async Task<ApiResult<object>> Login([FromBody] LoginDto loginDto)
		{
			try
			{
				// 1. 基本校验
				if (string.IsNullOrWhiteSpace(loginDto.username) || string.IsNullOrWhiteSpace(loginDto.password))
					return ApiResult<object>.Fail("400", "用户名或密码不能为空");

				// 2. 查询用户
				var user = await _db.Queryable<SysUser>()
									.Where(u => u.UserId == loginDto.username || u.UserName == loginDto.username)
									.FirstAsync();

				// 3. 校验密码（BCrypt 哈希；命中历史明文时自动升级为哈希）
				// 说明：登录失败属于业务参数错误，用 400 而非 401，
				// 401 保留给"Token 失效/未认证"，前端会据此清除登录态并跳转登录页
				if (user == null || !PasswordHelper.Verify(loginDto.password, user.PassWord, out var needUpgrade))
					return ApiResult<object>.Fail("400", "用户名或密码错误");

				// 历史明文密码：登录成功后自动升级为 BCrypt 哈希
				if (needUpgrade)
				{
					user.PassWord = PasswordHelper.Hash(loginDto.password);
					await _db.Updateable(user).UpdateColumns(u => u.PassWord).ExecuteCommandAsync();
				}

				// 4. 检查账号是否启用
				if (user.IsEnabled == false)
					return ApiResult<object>.Fail("403", "账号已被禁用");

				// 5. 生成 Token 并更新最后登录时间
				var result = await BuildToken(user);
				UpdateLastLoginTime(user);

				return ApiResult<object>.Success(
					new { Token = result.Token, UserInfo = result.UserInfo }, "登录成功");
			}
			catch
			{
				// 生产环境建议使用 ILogger 记录异常
				return ApiResult<object>.Fail("500", "系统繁忙，请稍后再试");
			}
		}

		/// <summary>
		/// 注册账号（开放接口，无需登录）
		/// 手机号必填：注册时即完成绑定，之后可用手机号 + 验证码登录
		/// </summary>
		[HttpPost("register")]
		public async Task<ApiResult<object>> Register([FromBody] RegisterDto dto)
		{
			try
			{
				// 1. 基本校验
				if (string.IsNullOrWhiteSpace(dto?.userId))
					return ApiResult<object>.Fail("400", "登录账号不能为空");
				if (string.IsNullOrWhiteSpace(dto.password))
					return ApiResult<object>.Fail("400", "密码不能为空");
				if (dto.password != dto.confirmPassword)
					return ApiResult<object>.Fail("400", "两次输入的密码不一致");

				var mobile = dto.mobile?.Trim();
				if (string.IsNullOrWhiteSpace(mobile))
					return ApiResult<object>.Fail("400", "手机号不能为空，注册后可用于手机号登录");
				if (!IsMobile(mobile))
					return ApiResult<object>.Fail("400", "请输入正确的手机号");

				// 2. 账号唯一性校验（登录账号与姓名都占用，避免登录匹配歧义）
				var account = dto.userId.Trim();
				var exist = await _db.Queryable<SysUser>()
					.AnyAsync(u => u.UserId == account || u.UserName == account);
				if (exist)
					return ApiResult<object>.Fail("400", "该登录账号已被使用");

				// 3. 手机号唯一性校验（一个手机号只能绑定一个账户）
				var mobileUsed = await _db.Queryable<SysUser>().AnyAsync(u => u.Mobile == mobile);
				if (mobileUsed)
					return ApiResult<object>.Fail("400", "该手机号已绑定其他账户");

				// 4. 创建用户（默认启用；角色/部门由管理员后续在权限管理中分配）
				var displayName = string.IsNullOrWhiteSpace(dto.userName) ? account : dto.userName.Trim();
				var entity = new SysUser
				{
					Id = Guid.NewGuid().ToString(),
					UserId = account,
					UserName = displayName,
					NickName = displayName,
					PassWord = PasswordHelper.Hash(dto.password), // BCrypt 哈希存储
					Email = dto.email,
					Mobile = mobile,
					IsEnabled = true,
					StatusCode = 1,
					StatusDesc = "启用",
					CreateTime = DateTime.Now
				};

				var count = await _db.Insertable(entity).ExecuteCommandAsync();
				return count > 0
					? ApiResult<object>.Success(null, "注册成功，请登录")
					: ApiResult<object>.Fail("500", "注册失败");
			}
			catch
			{
				return ApiResult<object>.Fail("500", "系统繁忙，请稍后再试");
			}
		}

		/// <summary>
		/// 发送手机验证码（登录/绑定共用）
		/// </summary>
		[HttpPost("sms/code")]
		public ApiResult<object> SendSmsCode([FromBody] SmsCodeDto dto)
		{
			var mobile = dto?.mobile?.Trim();
			if (string.IsNullOrWhiteSpace(mobile) || !IsMobile(mobile))
				return ApiResult<object>.Fail("400", "请输入正确的手机号");

			if (!_smsCodeService.CanSend(mobile, out var waitSeconds))
				return ApiResult<object>.Fail("400", $"发送过于频繁，请 {waitSeconds} 秒后再试");

			var code = _smsCodeService.Generate(mobile);

			// 本地联调：未接入短信网关时把验证码回传给前端，便于测试
			var data = _smsCodeService.ReturnCodeInResponse ? new { code } : null;
			return ApiResult<object>.Success(data, "验证码已发送");
		}

		/// <summary>
		/// 手机号 + 验证码登录
		/// 手机号未绑定任何账户时返回 404，前端应引导用户先注册
		/// </summary>
		[HttpPost("sms/login")]
		public async Task<ApiResult<object>> SmsLogin([FromBody] SmsLoginDto dto)
		{
			try
			{
				var mobile = dto?.mobile?.Trim();
				if (string.IsNullOrWhiteSpace(mobile) || !IsMobile(mobile))
					return ApiResult<object>.Fail("400", "请输入正确的手机号");
				if (string.IsNullOrWhiteSpace(dto.code))
					return ApiResult<object>.Fail("400", "请输入验证码");

				// 1. 校验验证码（通过后立即失效）
				if (!_smsCodeService.Validate(mobile, dto.code))
					return ApiResult<object>.Fail("400", "验证码错误或已失效");

				// 2. 查找绑定该手机号的账户
				var user = await _db.Queryable<SysUser>().FirstAsync(u => u.Mobile == mobile);
				if (user == null)
					return ApiResult<object>.Fail("404", "该手机号未绑定账户，请先注册账号");

				// 3. 检查账号是否启用
				if (user.IsEnabled == false)
					return ApiResult<object>.Fail("403", "账号已被禁用");

				var result = await BuildToken(user);
				UpdateLastLoginTime(user);

				return ApiResult<object>.Success(
					new { Token = result.Token, UserInfo = result.UserInfo }, "登录成功");
			}
			catch
			{
				return ApiResult<object>.Fail("500", "系统繁忙，请稍后再试");
			}
		}

		/// <summary>
		/// 绑定手机号到当前登录账户（已注册但未填手机号的用户可在此补齐）
		/// </summary>
		[Authorize]
		[HttpPost("sms/bind")]
		public async Task<ApiResult<object>> BindMobile([FromBody] BindMobileDto dto)
		{
			try
			{
				var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
				if (string.IsNullOrWhiteSpace(userId))
					return ApiResult<object>.Fail("401", "登录信息无效，请重新登录");

				var mobile = dto?.mobile?.Trim();
				if (string.IsNullOrWhiteSpace(mobile) || !IsMobile(mobile))
					return ApiResult<object>.Fail("400", "请输入正确的手机号");

				if (!_smsCodeService.Validate(mobile, dto?.code))
					return ApiResult<object>.Fail("400", "验证码错误或已失效");

				// 该手机号已被其他账户占用
				var used = await _db.Queryable<SysUser>()
					.AnyAsync(u => u.Mobile == mobile && u.Id != userId);
				if (used)
					return ApiResult<object>.Fail("400", "该手机号已绑定其他账户");

				var user = await _db.Queryable<SysUser>().FirstAsync(u => u.Id == userId);
				if (user == null)
					return ApiResult<object>.Fail("404", "账户不存在");

				user.Mobile = mobile;
				user.UpdateTime = DateTime.Now;

				await _db.Updateable(user)
					.UpdateColumns(u => new { u.Mobile, u.UpdateTime })
					.ExecuteCommandAsync();

				return ApiResult<object>.Success(null, "手机号绑定成功");
			}
			catch
			{
				return ApiResult<object>.Fail("500", "系统繁忙，请稍后再试");
			}
		}

		/// <summary>
		/// 忘记密码：通过手机号 + 验证码直接设置新密码
		/// 说明：校验通过后写入 BCrypt 哈希，并返回登录账号方便用户直接登录
		/// </summary>
		[HttpPost("password/forgot")]
		public async Task<ApiResult<object>> ForgotPassword([FromBody] ForgotPasswordDto dto)
		{
			try
			{
				var mobile = dto?.mobile?.Trim();
				if (string.IsNullOrWhiteSpace(mobile) || !IsMobile(mobile))
					return ApiResult<object>.Fail("400", "请输入正确的手机号");
				if (string.IsNullOrWhiteSpace(dto.code))
					return ApiResult<object>.Fail("400", "请输入验证码");
				if (string.IsNullOrWhiteSpace(dto.newPassword) || dto.newPassword.Length < 6)
					return ApiResult<object>.Fail("400", "新密码至少 6 位");
				if (dto.newPassword != dto.confirmPassword)
					return ApiResult<object>.Fail("400", "两次输入的新密码不一致");

				// 1. 校验验证码（通过后立即失效）
				if (!_smsCodeService.Validate(mobile, dto.code))
					return ApiResult<object>.Fail("400", "验证码错误或已失效");

				// 2. 查找绑定该手机号的账户
				var user = await _db.Queryable<SysUser>().FirstAsync(u => u.Mobile == mobile);
				if (user == null)
					return ApiResult<object>.Fail("404", "该手机号未绑定账户，请先注册账号");

				// 3. 禁用账号不允许通过此方式重置
				if (user.IsEnabled == false)
					return ApiResult<object>.Fail("403", "账号已被禁用，请联系管理员");

				// 4. 写入新密码哈希
				user.PassWord = PasswordHelper.Hash(dto.newPassword);
				user.UpdateTime = DateTime.Now;

				await _db.Updateable(user)
					.UpdateColumns(u => new { u.PassWord, u.UpdateTime })
					.ExecuteCommandAsync();

				return ApiResult<object>.Success(new { userId = user.UserId }, "密码重置成功，请登录");
			}
			catch
			{
				return ApiResult<object>.Fail("500", "系统繁忙，请稍后再试");
			}
		}

		/// <summary>
		/// 获取当前登录用户的完整资料（含角色名、部门名、最后登录时间）
		/// </summary>
		[Authorize]
		[HttpGet("profile")]
		public async Task<ApiResult<object>> GetProfile()
		{
			try
			{
				var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
				if (string.IsNullOrWhiteSpace(userId))
					return ApiResult<object>.Fail("401", "登录信息无效，请重新登录");

				var user = await _db.Queryable<SysUser>().FirstAsync(u => u.Id == userId);
				if (user == null)
					return ApiResult<object>.Fail("404", "账户不存在");

				var role = string.IsNullOrWhiteSpace(user.RoleId)
					? null
					: await _db.Queryable<SysRole>().FirstAsync(r => r.RoleId == user.RoleId);

				var dept = string.IsNullOrWhiteSpace(user.DeptId)
					? null
					: await _db.Queryable<Department>().FirstAsync(d => d.Id == user.DeptId);

				var profile = new
				{
					id = user.Id,
					userId = user.UserId,
					userName = user.UserName,
					nickName = user.NickName,
					email = user.Email,
					mobile = user.Mobile,
					roleId = user.RoleId,
					roleName = role?.RoleName,
					deptId = user.DeptId,
					deptName = dept?.DepartmentName,
					lastLoginTime = user.LastLoginTime,
					isAdmin = role != null && role.IsAdmin == true
				};

				return ApiResult<object>.Success(profile, "获取成功");
			}
			catch
			{
				return ApiResult<object>.Fail("500", "系统繁忙，请稍后再试");
			}
		}

		/// <summary>
		/// 更新当前登录用户的个人资料（姓名 / 昵称 / 邮箱 / 手机号）
		/// </summary>
		[Authorize]
		[HttpPut("profile")]
		public async Task<ApiResult<object>> UpdateProfile([FromBody] UpdateProfileDto dto)
		{
			try
			{
				var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
				if (string.IsNullOrWhiteSpace(userId))
					return ApiResult<object>.Fail("401", "登录信息无效，请重新登录");

				var user = await _db.Queryable<SysUser>().FirstAsync(u => u.Id == userId);
				if (user == null)
					return ApiResult<object>.Fail("404", "账户不存在");

				if (string.IsNullOrWhiteSpace(dto?.userName))
					return ApiResult<object>.Fail("400", "姓名不能为空");

				// 手机号若被其他账户占用则拒绝（手机号是登录方式之一，必须唯一）
				var mobile = dto.mobile?.Trim();
				if (!string.IsNullOrWhiteSpace(mobile))
				{
					var used = await _db.Queryable<SysUser>()
						.AnyAsync(u => u.Mobile == mobile && u.Id != userId);
					if (used)
						return ApiResult<object>.Fail("400", "该手机号已绑定其他账户");
				}

				user.UserName = dto.userName.Trim();
				user.NickName = string.IsNullOrWhiteSpace(dto.nickName)
					? dto.userName.Trim()
					: dto.nickName.Trim();
				user.Email = dto.email?.Trim();
				user.Mobile = mobile;
				user.UpdateTime = DateTime.Now;

				await _db.Updateable(user)
					.UpdateColumns(u => new { u.UserName, u.NickName, u.Email, u.Mobile, u.UpdateTime })
					.ExecuteCommandAsync();

				// 返回最新用户信息，便于前端同步刷新本地缓存
				return ApiResult<object>.Success(await BuildUserInfo(user), "资料更新成功");
			}
			catch
			{
				return ApiResult<object>.Fail("500", "系统繁忙，请稍后再试");
			}
		}

		/// <summary>
		/// 修改当前登录用户的密码（必须校验原密码）
		/// 说明：与管理员的 reset-password 不同，此接口需要用户自己输入原密码
		/// </summary>
		[Authorize]
		[HttpPut("password")]
		public async Task<ApiResult<object>> ChangePassword([FromBody] ChangePasswordDto dto)
		{
			try
			{
				var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
				if (string.IsNullOrWhiteSpace(userId))
					return ApiResult<object>.Fail("401", "登录信息无效，请重新登录");

				if (string.IsNullOrWhiteSpace(dto?.oldPassword))
					return ApiResult<object>.Fail("400", "请输入原密码");
				if (string.IsNullOrWhiteSpace(dto.newPassword) || dto.newPassword.Length < 6)
					return ApiResult<object>.Fail("400", "新密码至少 6 位");
				if (dto.newPassword != dto.confirmPassword)
					return ApiResult<object>.Fail("400", "两次输入的新密码不一致");
				if (dto.oldPassword == dto.newPassword)
					return ApiResult<object>.Fail("400", "新密码不能与原密码相同");

				var user = await _db.Queryable<SysUser>().FirstAsync(u => u.Id == userId);
				if (user == null)
					return ApiResult<object>.Fail("404", "账户不存在");

				// 校验原密码（BCrypt；兼容历史明文）
				if (!PasswordHelper.Verify(dto.oldPassword, user.PassWord, out _))
					return ApiResult<object>.Fail("400", "原密码不正确");

				user.PassWord = PasswordHelper.Hash(dto.newPassword);
				user.UpdateTime = DateTime.Now;

				await _db.Updateable(user)
					.UpdateColumns(u => new { u.PassWord, u.UpdateTime })
					.ExecuteCommandAsync();

				return ApiResult<object>.Success(null, "密码修改成功");
			}
			catch
			{
				return ApiResult<object>.Fail("500", "系统繁忙，请稍后再试");
			}
		}

		/// <summary>
		/// 构造脱敏后的用户信息（含权限编码，不含密码）
		/// </summary>
		private async Task<object> BuildUserInfo(SysUser user)
		{
			var role = string.IsNullOrWhiteSpace(user.RoleId)
				? null
				: await _db.Queryable<SysRole>().FirstAsync(r => r.RoleId == user.RoleId);

			List<string> permissions;
			if (role != null && role.IsAdmin == true)
			{
				// 超级管理员拥有全部权限
				permissions = Permissions.All.ToList();
			}
			else if (!string.IsNullOrWhiteSpace(user.RoleId))
			{
				permissions = await _db.Queryable<SysRolePermission>()
					.Where(p => p.RoleId == user.RoleId)
					.Select(p => p.PermissionCode)
					.ToListAsync();
			}
			else
			{
				permissions = new List<string>();
			}

			return new
			{
				user.Id,
				user.UserId,
				user.UserName,
				user.NickName,
				user.Email,
				user.Mobile,
				user.RoleId,
				user.DeptId,
				isAdmin = role != null && role.IsAdmin == true,
				permissions
			};
		}

		/// <summary>
		/// 生成 JWT Token 与脱敏后的用户信息（含该用户拥有的权限编码）
		/// </summary>
		private async Task<(string Token, object UserInfo)> BuildToken(SysUser user)
		{
			var claims = new[]
			{
				new Claim(JwtRegisteredClaimNames.Sub, user.Id),
				new Claim(JwtRegisteredClaimNames.UniqueName, user.UserId),
				new Claim(ClaimTypes.Name, user.UserName ?? ""),
				new Claim(ClaimTypes.Role, user.RoleId ?? ""),
				new Claim("deptId", user.DeptId ?? "")
			};

			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? ""));
			var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
			var expireMinutes = Convert.ToDouble(_configuration["Jwt:ExpireMinutes"]);

			var tokenDescriptor = new SecurityTokenDescriptor
			{
				Subject = new ClaimsIdentity(claims),
				Expires = DateTime.UtcNow.AddMinutes(expireMinutes),
				Issuer = _configuration["Jwt:Issuer"],
				Audience = _configuration["Jwt:Audience"],
				SigningCredentials = creds
			};

			var tokenHandler = new JwtSecurityTokenHandler();
			var tokenString = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

			var userInfo = await BuildUserInfo(user);
			return (tokenString, userInfo);
		}

		/// <summary>
		/// 异步更新最后登录时间（不阻塞主流程）
		/// </summary>
		private void UpdateLastLoginTime(SysUser user)
		{
			_ = Task.Run(() =>
			{
				user.LastLoginTime = DateTime.Now;
				_db.Updateable(user).UpdateColumns(u => u.LastLoginTime).ExecuteCommand();
			});
		}

		/// <summary>
		/// 中国大陆手机号格式校验
		/// </summary>
		private static bool IsMobile(string? mobile) =>
			!string.IsNullOrWhiteSpace(mobile) && Regex.IsMatch(mobile, "^1[3-9]\\d{9}$");
	}
}
