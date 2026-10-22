using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 登录防爆破守卫
	/// 机制：
	///   1) 账号级失败锁定：同一账号在统计窗口内连续失败达到阈值即锁定一段时间（默认 5 次/10 分钟 → 锁 15 分钟）；
	///   2) IP 级限流（可选，默认关闭）：同一 IP 在窗口内失败过多即临时封禁。
	///     注意：经反向代理（nginx 等）部署时 RemoteIpAddress 会是代理地址，
	///     开启 IP 限流前请先配置 ForwardedHeaders 中间件，否则可能误伤全部用户。
	/// 存储：复用 ICacheService（Redis 优先、内存降级），锁值存"解锁时间(UTC ticks)"，
	///       剩余锁定时间由值本身计算，不依赖缓存 TTL 查询。
	/// </summary>
	public class LoginGuardService
	{
		private readonly ICacheService _cache;
		private readonly IConfiguration _configuration;

		public LoginGuardService(ICacheService cache, IConfiguration configuration)
		{
			_cache = cache;
			_configuration = configuration;
		}

		// ---- 可配置阈值（带默认值） ----
		private int MaxFailures => _configuration.GetValue<int>("LoginGuard:MaxFailures", 5);
		private int FailWindowMinutes => _configuration.GetValue<int>("LoginGuard:FailWindowMinutes", 10);
		private int LockMinutes => _configuration.GetValue<int>("LoginGuard:LockMinutes", 15);
		private bool IpLimitEnabled => _configuration.GetValue<bool>("LoginGuard:IpLimitEnabled", false);
		private int IpMaxFailures => _configuration.GetValue<int>("LoginGuard:IpMaxFailures", 30);

		// ---- 缓存键 ----
		private static string FailKey(string username) => $"ams:login:fail:{username.ToLowerInvariant()}";
		private static string LockKey(string username) => $"ams:login:lock:{username.ToLowerInvariant()}";
		private static string IpFailKey(string ip) => $"ams:login:ipfail:{ip}";
		private static string IpLockKey(string ip) => $"ams:login:iplock:{ip}";

		/// <summary>
		/// 判断账号/IP 是否处于锁定状态
		/// </summary>
		/// <returns>(是否锁定, 剩余等待分钟数；未锁定时为 0)</returns>
		public async Task<(bool locked, int waitMinutes)> IsLockedAsync(string username, string? ip)
		{
			var (accountLocked, accountWait) = await CheckLockAsync(LockKey(username));
			if (accountLocked)
				return (true, accountWait);

			if (IpLimitEnabled && !string.IsNullOrWhiteSpace(ip))
			{
				var (ipLocked, ipWait) = await CheckLockAsync(IpLockKey(ip));
				if (ipLocked)
					return (true, ipWait);
			}

			return (false, 0);
		}

		/// <summary>
		/// 登记一次登录失败；达到阈值时自动锁定
		/// </summary>
		public async Task RegisterFailureAsync(string username, string? ip)
		{
			// 1. 账号失败计数
			var failCount = (await _cache.GetAsync<int?>(FailKey(username))) ?? 0;
			failCount++;
			await _cache.SetAsync(FailKey(username), failCount, TimeSpan.FromMinutes(FailWindowMinutes));

			if (failCount >= MaxFailures)
			{
				await LockAsync(LockKey(username));
				await _cache.RemoveAsync(FailKey(username)); // 锁定期间不再累计
			}

			// 2. IP 失败计数（可选）
			if (IpLimitEnabled && !string.IsNullOrWhiteSpace(ip))
			{
				var ipCount = (await _cache.GetAsync<int?>(IpFailKey(ip))) ?? 0;
				ipCount++;
				await _cache.SetAsync(IpFailKey(ip), ipCount, TimeSpan.FromMinutes(FailWindowMinutes));

				if (ipCount >= IpMaxFailures)
				{
					await LockAsync(IpLockKey(ip));
					await _cache.RemoveAsync(IpFailKey(ip));
				}
			}
		}

		/// <summary>登录成功：清除该账号的失败计数与锁定</summary>
		public async Task ClearAsync(string username)
		{
			await _cache.RemoveAsync(FailKey(username));
			await _cache.RemoveAsync(LockKey(username));
		}

		// ---- 内部实现 ----

		private async Task LockAsync(string key)
		{
			var unlockAt = DateTime.UtcNow.AddMinutes(LockMinutes);
			await _cache.SetAsync(key, unlockAt.Ticks.ToString(), TimeSpan.FromMinutes(LockMinutes));
		}

		/// <summary>检查锁键：返回 (是否锁定, 剩余分钟)</summary>
		private async Task<(bool locked, int waitMinutes)> CheckLockAsync(string key)
		{
			var raw = await _cache.GetAsync<string>(key);
			if (string.IsNullOrWhiteSpace(raw)) return (false, 0);

			if (!long.TryParse(raw, out var unlockTicks))
			{
				// 值异常：按未锁定处理并清理
				await _cache.RemoveAsync(key);
				return (false, 0);
			}

			var unlockAt = new DateTime(unlockTicks, DateTimeKind.Utc);
			var remaining = unlockAt - DateTime.UtcNow;
			if (remaining <= TimeSpan.Zero)
			{
				await _cache.RemoveAsync(key);
				return (false, 0);
			}

			return (true, Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)));
		}

		/// <summary>
		/// 获取客户端 IP：优先取反向代理转发的 X-Forwarded-For 首地址，否则取直连地址。
		/// 注意：未配置 ForwardedHeaders 信任代理时，X-Forwarded-For 可被伪造，
		/// 因此 IP 限流默认关闭，仅作为可选的第二层防护。
		/// </summary>
		public static string? GetClientIp(HttpContext context)
		{
			var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
			if (!string.IsNullOrWhiteSpace(forwarded))
			{
				var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
				if (!string.IsNullOrWhiteSpace(first)) return first;
			}
			return context.Connection.RemoteIpAddress?.ToString();
		}
	}
}
