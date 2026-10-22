using AssetManagementSystem.Utils;
using Xunit;

namespace AssetManagementSystem.Tests;

public class PageGuardTests
{
	[Fact]
	public void NegativeOrZero_ResetToDefaults()
	{
		Assert.Equal((1, 10), PageGuard.Normalize(0, 0));
		Assert.Equal((1, 10), PageGuard.Normalize(-5, -1));
	}

	[Fact]
	public void PageSize_IsCapped()
	{
		// 极端请求：pageSize=1000000 必须被截断，防止一次性拉取超大结果集
		Assert.Equal((1, PageGuard.MaxPageSize), PageGuard.Normalize(1, 1_000_000));
	}

	[Fact]
	public void NormalValues_PassThrough()
	{
		Assert.Equal((3, 20), PageGuard.Normalize(3, 20));
		Assert.Equal((2, PageGuard.MaxPageSize), PageGuard.Normalize(2, PageGuard.MaxPageSize));
	}
}
