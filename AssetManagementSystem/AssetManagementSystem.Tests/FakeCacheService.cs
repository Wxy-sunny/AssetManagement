using System.Collections.Concurrent;
using System.Text.Json;
using AssetManagementSystem.Services;

namespace AssetManagementSystem.Tests;

/// <summary>
/// 内存假缓存：用于单元测试（不依赖 Redis）。
/// 取值时按 JSON 反序列化，与真实 CacheService 的存储行为保持一致。
/// </summary>
public class FakeCacheService : ICacheService
{
	private readonly ConcurrentDictionary<string, string> _store = new();

	public Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan expire, TimeSpan? nullExpire = null)
		=> factory();

	public Task RemoveAsync(string key)
	{
		_store.TryRemove(key, out _);
		return Task.CompletedTask;
	}

	public Task RemoveByPrefixAsync(string prefix)
	{
		foreach (var k in _store.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
			_store.TryRemove(k, out _);
		return Task.CompletedTask;
	}

	public Task SetAsync<T>(string key, T value, TimeSpan ttl)
	{
		_store[key] = JsonSerializer.Serialize(value);
		return Task.CompletedTask;
	}

	public Task<T?> GetAsync<T>(string key)
	{
		if (_store.TryGetValue(key, out var raw))
			return Task.FromResult(JsonSerializer.Deserialize<T>(raw));
		return Task.FromResult<T?>(default);
	}
}
