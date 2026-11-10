using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using Xunit;

namespace AssetManagementSystem.Tests;

/// <summary>
/// 资产状态机测试：防止"报废资产被重新领用""维修单在错误状态下被发起"等闭环回归。
/// </summary>
public class AssetStatusMachineTests
{
	[Theory]
	[InlineData(AssetStatusCodes.InUse, AssetStatusCodes.Idle, true)]
	[InlineData(AssetStatusCodes.InUse, AssetStatusCodes.Repair, true)]
	[InlineData(AssetStatusCodes.InUse, AssetStatusCodes.Scrapped, true)]
	[InlineData(AssetStatusCodes.Idle, AssetStatusCodes.InUse, true)]
	[InlineData(AssetStatusCodes.Repair, AssetStatusCodes.InUse, true)]
	[InlineData(AssetStatusCodes.Repair, AssetStatusCodes.Idle, true)]
	[InlineData(AssetStatusCodes.Repair, AssetStatusCodes.Scrapped, true)]
	[InlineData(AssetStatusCodes.Scrapped, AssetStatusCodes.InUse, false)]
	[InlineData(AssetStatusCodes.Scrapped, AssetStatusCodes.Idle, false)]
	[InlineData(AssetStatusCodes.InUse, AssetStatusCodes.InUse, false)]
	public void CanTransit_FollowsWhitelist(string from, string to, bool expected)
	{
		Assert.Equal(expected, AssetStatusMachine.CanTransit(from, to));
	}

	[Fact]
	public void Scrapped_IsTerminal()
	{
		Assert.Empty(AssetStatusMachine.GetAllowedTargets(AssetStatusCodes.Scrapped));
		Assert.Empty(AssetStatusMachine.GetAllowedFlowTypes(AssetStatusCodes.Scrapped));
	}

	[Fact]
	public void Borrow_RequiresIdle()
	{
		Assert.True(AssetStatusMachine.CanStartFlow(
			AssetFlowTypes.Borrow, AssetStatusCodes.Idle, null, out _));
		Assert.False(AssetStatusMachine.CanStartFlow(
			AssetFlowTypes.Borrow, AssetStatusCodes.InUse, null, out _));
	}

	[Fact]
	public void RepairDone_RequiresRepairAndExplicitTarget()
	{
		Assert.False(AssetStatusMachine.CanStartFlow(
			AssetFlowTypes.RepairDone, AssetStatusCodes.Repair, null, out _));
		Assert.False(AssetStatusMachine.CanStartFlow(
			AssetFlowTypes.RepairDone, AssetStatusCodes.InUse, AssetStatusCodes.Idle, out _));
		Assert.True(AssetStatusMachine.CanStartFlow(
			AssetFlowTypes.RepairDone, AssetStatusCodes.Repair, AssetStatusCodes.InUse, out _));
	}

	[Fact]
	public void AllStatuses_AreValidAndNamed()
	{
		foreach (var status in AssetStatusMachine.AllStatuses)
		{
			Assert.True(AssetStatusMachine.IsValidStatus(status));
			Assert.NotEqual("未知", AssetStatusMachine.GetStatusName(status));
		}
	}

	[Fact]
	public void UnknownCodes_AreRejected()
	{
		Assert.False(AssetStatusMachine.IsValidStatus("not_exist"));
		Assert.False(AssetStatusMachine.IsValidFlowType("not_exist"));
		Assert.False(AssetStatusMachine.CanTransit(null, AssetStatusCodes.InUse));
	}
}
