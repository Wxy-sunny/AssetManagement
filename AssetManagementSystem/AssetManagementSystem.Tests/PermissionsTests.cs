using AssetManagementSystem.Dtos;
using AssetManagementSystem.Utils;
using Xunit;

namespace AssetManagementSystem.Tests;

/// <summary>
/// 权限定义一致性测试：
/// 防止"新增了权限常量但忘记加入 Permissions.All 或权限分组"这类回归
/// （历史上就出现过新权限未纳入 All 导致非超管角色拿不到权限的问题）。
/// </summary>
public class PermissionsTests
{
	[Fact]
	public void All_ContainsEveryGroupedPermission()
	{
		var groups = Permissions.GetGroups();
		var grouped = groups.SelectMany(g => g.permissions).Select(p => p.code).ToList();

		foreach (var code in grouped)
			Assert.Contains(code, Permissions.All);
	}

	[Fact]
	public void All_HasNoDuplicates()
	{
		Assert.Equal(Permissions.All.Length, Permissions.All.Distinct().Count());
	}

	[Fact]
	public void Groups_HaveNoDuplicatedCode()
	{
		var all = Permissions.GetGroups().SelectMany(g => g.permissions).Select(p => p.code).ToList();

		Assert.Equal(all.Count, all.Distinct().Count());
	}

	[Fact]
	public void EveryPermissionInAll_IsGrouped()
	{
		var grouped = Permissions.GetGroups().SelectMany(g => g.permissions).Select(p => p.code).ToHashSet();

		foreach (var code in Permissions.All)
			Assert.Contains(code, grouped);
	}

	[Fact]
	public void GroupCodes_AreUnique()
	{
		var codes = Permissions.GetGroups().Select(g => g.code).ToList();

		Assert.Equal(codes.Count, codes.Distinct().Count());
	}

	[Theory]
	[InlineData(Permissions.RequestList)]
	[InlineData(Permissions.RequestManage)]
	[InlineData(Permissions.NotificationList)]
	[InlineData(Permissions.AssetList)]
	[InlineData(Permissions.UserManage)]
	public void KeyPermissions_Exist(string code)
	{
		Assert.Contains(code, Permissions.All);
	}
}
