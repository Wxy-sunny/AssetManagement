using AssetManagementSystem.Utils;
using Xunit;

namespace AssetManagementSystem.Tests;

public class PasswordHelperTests
{
	[Fact]
	public void Hash_ProducesBcryptHash_And_VerifyPasses()
	{
		var hash = PasswordHelper.Hash("Passw0rd!");

		Assert.StartsWith("$2", hash);
		Assert.True(PasswordHelper.Verify("Passw0rd!", hash, out _));
	}

	[Fact]
	public void Verify_WrongPassword_Fails()
	{
		var hash = PasswordHelper.Hash("Passw0rd!");

		Assert.False(PasswordHelper.Verify("wrong-password", hash, out _));
	}

	[Fact]
	public void Hash_SamePassword_DifferentHashes_DueToSalt()
	{
		var h1 = PasswordHelper.Hash("same-password");
		var h2 = PasswordHelper.Hash("same-password");

		Assert.NotEqual(h1, h2);
		Assert.True(PasswordHelper.Verify("same-password", h1, out _));
		Assert.True(PasswordHelper.Verify("same-password", h2, out _));
	}

	[Fact]
	public void Verify_LegacyPlaintext_FlagsUpgrade()
	{
		// 历史明文密码：校验通过并提示需要升级为哈希
		Assert.True(PasswordHelper.Verify("legacy123", "legacy123", out var needUpgrade));
		Assert.True(needUpgrade);

		// 明文不匹配则失败
		Assert.False(PasswordHelper.Verify("legacy123", "other", out _));
	}

	[Fact]
	public void Verify_EmptyStored_Fails()
	{
		Assert.False(PasswordHelper.Verify("whatever", null, out _));
		Assert.False(PasswordHelper.Verify("whatever", "", out _));
	}
}
