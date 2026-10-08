using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 手机验证码服务
	/// 优先使用 Redis 存储（多实例共享、服务重启不丢失）；
	/// Redis 不可用时自动降级为进程内存缓存，保证功能可用。
	/// 说明：当前未接入真实短信网关，验证码仅打印到控制台。
	/// </summary>
	public class SmsCodeService
	{
		private const int ExpireMinutes = 5;          // 验证码有效期
		private const int ResendIntervalSeconds = 60; // 重发间隔

		/// <summary>
		/// 校验并消费验证码（Lua 脚本保证 GET + DEL 原子执行，防止并发重复使用）
		/// </summary>
		private static readonly LuaScript ValidateScript = LuaScript.Prepare(
			"local v = redis.call('GET', @key) " +
			"if v then redis.call('DEL', @key) end " +
			"return v");

		private readonly IConnectionMultiplexer _redis;
		private readonly IMemoryCache _memory;
		private readonly IConfiguration _configuration;

		public SmsCodeService(
			IConnectionMultiplexer redis,
			IMemoryCache memory,
			IConfiguration configuration)
		{
			_redis = redis;
			_memory = memory;
			_configuration = configuration;
		}

		private static string CodeKey(string mobile) => $"ams:sms:code:{mobile}";
		private static string ThrottleKey(string mobile) => $"ams:sms:throttle:{mobile}";

		/// <summary>
		/// Redis 可用则返回数据库连接，否则返回 null（调用方走内存降级）
		/// </summary>
		private IDatabase? Db
		{
			get
			{
				try
				{
					return _redis.IsConnected ? _redis.GetDatabase() : null;
				}
				catch
				{
					return null;
				}
			}
		}

		/// <summary>
		/// 是否允许发送验证码（60 秒内只允许发一次）
		/// </summary>
		/// <param name="waitSeconds">不允许时还需等待的秒数</param>
		public bool CanSend(string mobile, out int waitSeconds)
		{
			waitSeconds = 0;

			var db = Db;
			if (db != null)
			{
				var ttl = db.KeyTimeToLive(ThrottleKey(mobile));
				if (ttl.HasValue && ttl.Value.TotalSeconds > 0)
				{
					waitSeconds = (int)Math.Ceiling(ttl.Value.TotalSeconds);
					return false;
				}
				return true;
			}

			// 降级：内存
			if (!_memory.TryGetValue(ThrottleKey(mobile), out DateTime lastSend)) return true;

			var elapsed = (DateTime.Now - lastSend).TotalSeconds;
			if (elapsed >= ResendIntervalSeconds) return true;

			waitSeconds = (int)Math.Ceiling(ResendIntervalSeconds - elapsed);
			return false;
		}

		/// <summary>
		/// 生成并缓存验证码
		/// </summary>
		public string Generate(string mobile)
		{
			// 6 位数字验证码
			var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

			var db = Db;
			if (db != null)
			{
				// SET key value EX seconds
				db.StringSet(CodeKey(mobile), code, TimeSpan.FromMinutes(ExpireMinutes));
				db.StringSet(ThrottleKey(mobile), "1", TimeSpan.FromSeconds(ResendIntervalSeconds));
			}
			else
			{
				// 降级：内存
				_memory.Set(CodeKey(mobile), code, TimeSpan.FromMinutes(ExpireMinutes));
				_memory.Set(ThrottleKey(mobile), DateTime.Now, TimeSpan.FromSeconds(ResendIntervalSeconds));
			}

			// TODO: 接入真实短信网关（阿里云 / 腾讯云等）
			Console.WriteLine($"[SMS] 手机号 {mobile} 的验证码为 {code}，{ExpireMinutes} 分钟内有效");

			return code;
		}

		/// <summary>
		/// 校验验证码，通过后立即失效（原子操作，防止重复使用）
		/// </summary>
		public bool Validate(string mobile, string? code)
		{
			if (string.IsNullOrWhiteSpace(code)) return false;

			var db = Db;
			if (db != null)
			{
				var cached = (string?)db.ScriptEvaluate(ValidateScript, new { key = (RedisKey)CodeKey(mobile) });
				return string.Equals(cached, code, StringComparison.Ordinal);
			}

			// 降级：内存
			if (!_memory.TryGetValue(CodeKey(mobile), out string? memCode)) return false;
			if (!string.Equals(memCode, code, StringComparison.Ordinal)) return false;

			_memory.Remove(CodeKey(mobile));
			return true;
		}

		/// <summary>
		/// 是否在接口响应中返回验证码（仅本地联调用，生产必须为 false）
		/// </summary>
		public bool ReturnCodeInResponse =>
			_configuration.GetValue<bool>("Sms:ReturnCodeInResponse", false);
	}
}
