using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 通用缓存服务（Redis 优先，不可用时自动降级为内存缓存）。
	/// 内置缓存三件套防护：
	///   1. 缓存空值（防穿透）：DB 查不到也写入短 TTL 的占位，避免恶意/不存在的 key 反复打到 DB。
	///   2. TTL 随机抖动（防雪崩）：基础过期时间叠加随机偏移，避免大量 key 同时失效。
	///   3. 单 key 互斥锁（防击穿）：同一 key 缓存失效瞬间，仅放行一个请求回源，其余等待复用结果。
	/// 注意：互斥锁为进程内，单实例场景足够；多实例部署应改为 Redis 分布式锁。
	/// </summary>
	public interface ICacheService
	{
		/// <summary>
		/// 缓存 aside：先读缓存，未命中则执行 factory 回源并写回。
		/// </summary>
		/// <param name="key">缓存键</param>
		/// <param name="factory">回源委托（查 DB 等）</param>
		/// <param name="expire">正常结果过期时间</param>
		/// <param name="nullExpire">空值占位过期时间（防穿透，默认 2 分钟）</param>
		Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan expire, TimeSpan? nullExpire = null);

		/// <summary>
		/// 删除缓存（写操作后调用，保证缓存与 DB 一致）
		/// </summary>
		Task RemoveAsync(string key);
	}

	public class CacheService : ICacheService
	{
		private const string NullSentinel = "__NULL__"; // 空值占位，与正常 JSON 区分

		private readonly IConnectionMultiplexer _redis;
		private readonly IMemoryCache _memory;
		// 单 key 互斥锁（进程内，防击穿）。key 数量随业务接口有界，无需清理。
		private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

		public CacheService(IConnectionMultiplexer redis, IMemoryCache memory)
		{
			_redis = redis;
			_memory = memory;
		}

		/// <summary>
		/// Redis 可用则返回数据库，否则返回 null（调用方走内存降级）。与 SmsCodeService 同策略。
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

		public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan expire, TimeSpan? nullExpire = null)
		{
			// 1. 先读缓存（命中即返回，含空值占位 -> 防穿透）
			var cached = await GetRawAsync(key);
			if (cached != null)
			{
				return cached == NullSentinel ? default : JsonSerializer.Deserialize<T>(cached);
			}

			// 2. 缓存未命中：单 key 互斥，防并发击穿回源
			var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
			await gate.WaitAsync();
			try
			{
				// double check：等待期间可能已被其他请求回填
				cached = await GetRawAsync(key);
				if (cached != null)
				{
					return cached == NullSentinel ? default : JsonSerializer.Deserialize<T>(cached);
				}

				var value = await factory();
				if (value is null)
				{
					// 防穿透：缓存空值，短 TTL
					await SetRawAsync(key, NullSentinel, nullExpire ?? TimeSpan.FromMinutes(2));
					return default;
				}

				// 防雪崩：基础 TTL + 随机抖动（最多 +20%）
				var jitter = TimeSpan.FromSeconds(
					Random.Shared.Next(0, Math.Max(1, (int)(expire.TotalSeconds * 0.2))));
				await SetRawAsync(key, JsonSerializer.Serialize(value), expire + jitter);
				return value;
			}
			finally
			{
				gate.Release();
			}
		}

		public Task RemoveAsync(string key)
		{
			try
			{
				var db = Db;
				if (db != null)
				{
					return db.KeyDeleteAsync(key);
				}
			}
			catch { }
			_memory.Remove(key);
			return Task.CompletedTask;
		}

		private async Task<string?> GetRawAsync(string key)
		{
			try
			{
				var db = Db;
				if (db != null)
				{
					return await db.StringGetAsync(key);
				}
			}
			catch { }
			_memory.TryGetValue(key, out string? v);
			return v;
		}

		private async Task SetRawAsync(string key, string value, TimeSpan expire)
		{
			try
			{
				var db = Db;
				if (db != null)
				{
					await db.StringSetAsync(key, value, expire);
					return;
				}
			}
			catch { }
			_memory.Set(key, value, expire);
		}
	}
}
