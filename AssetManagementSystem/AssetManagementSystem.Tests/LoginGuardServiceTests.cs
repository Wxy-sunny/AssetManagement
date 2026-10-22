using System.Collections.Generic;
using AssetManagementSystem.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AssetManagementSystem.Tests;

public class LoginGuardServiceTests
{
	private static LoginGuardService CreateService(Dictionary<string, string?>? settings = null)
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(settings ?? new Dictionary<string, string?>
			{
				["LoginGuard:MaxFailures"] = "5",
				["LoginGuard:FailWindowMinutes"] = "10",
				["LoginGuard:LockMinutes"] = "15",
				["LoginGuard:IpLimitEnabled"] = "false",
			})
			.Build();
		return new LoginGuardService(new FakeCacheService(), config);
	}

	[Fact]
	public async Task NotLocked_BelowThreshold()
	{
		var guard = CreateService();

		for (var i = 0; i < 4; i++)
			await guard.RegisterFailureAsync("zhangsan", "1.2.3.4");

		var (locked, _) = await guard.IsLockedAsync("zhangsan", "1.2.3.4");
		Assert.False(locked);
	}

	[Fact]
	public async Task Locked_WhenReachingThreshold()
	{
		var guard = CreateService();

		for (var i = 0; i < 5; i++)
			await guard.RegisterFailureAsync("zhangsan", "1.2.3.4");

		var (locked, waitMinutes) = await guard.IsLockedAsync("zhangsan", "1.2.3.4");
		Assert.True(locked);
		Assert.InRange(waitMinutes, 1, 15);
	}

	[Fact]
	public async Task Clear_Unlocks()
	{
		var guard = CreateService();
		for (var i = 0; i < 5; i++)
			await guard.RegisterFailureAsync("zhangsan", null);

		await guard.ClearAsync("zhangsan");

		var (locked, _) = await guard.IsLockedAsync("zhangsan", null);
		Assert.False(locked);
	}

	[Fact]
	public async Task ExpiredLock_IsNotLocked()
	{
		// 直接写入一个"已过期"的锁值（解锁时间在过去）
		var cache = new FakeCacheService();
		await cache.SetAsync("ams:login:lock:zhangsan",
			System.DateTime.UtcNow.AddMinutes(-1).Ticks.ToString(), System.TimeSpan.FromMinutes(15));

		var guard = new LoginGuardService(cache, new ConfigurationBuilder().Build());

		var (locked, _) = await guard.IsLockedAsync("zhangsan", null);
		Assert.False(locked);
	}

	[Fact]
	public async Task OtherAccount_NotAffected()
	{
		var guard = CreateService();
		for (var i = 0; i < 5; i++)
			await guard.RegisterFailureAsync("zhangsan", null);

		var (locked, _) = await guard.IsLockedAsync("lisi", null);
		Assert.False(locked);
	}
}
