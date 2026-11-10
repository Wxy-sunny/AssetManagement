using AssetManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 资产状态机
	/// 说明：
	/// 1) 定义资产状态之间"允许的跃迁关系"，任何状态变更都必须经过本类校验，
	///    杜绝"在用直接跳报废"之类的越级操作；
	/// 2) 定义每种流转单据（领用/归还/报修/维修完成/报废）的来源状态与目标状态；
	/// 3) 已报废为终态，不允许再流转。
	/// </summary>
	public static class AssetStatusMachine
	{
		/// <summary>
		/// 允许的状态跃迁表（源状态 → 允许的目标状态集合）
		/// </summary>
		private static readonly Dictionary<string, string[]> Transitions = new()
		{
			[AssetStatusCodes.InUse] = new[]
			{
				AssetStatusCodes.Idle,
				AssetStatusCodes.Repair,
				AssetStatusCodes.Scrapped
			},
			[AssetStatusCodes.Idle] = new[]
			{
				AssetStatusCodes.InUse,
				AssetStatusCodes.Repair,
				AssetStatusCodes.Scrapped
			},
			[AssetStatusCodes.Repair] = new[]
			{
				AssetStatusCodes.InUse,
				AssetStatusCodes.Idle,
				AssetStatusCodes.Scrapped
			},
			// 报废是终态：资产进入报废状态后不再允许任何流转
			[AssetStatusCodes.Scrapped] = Array.Empty<string>()
		};

		/// <summary>
		/// 流转单据规则：单据类型 → (允许的来源状态, 固定目标状态)
		/// repair_done 的目标状态不固定（可回到在用或转闲置），由业务参数指定。
		/// </summary>
		private static readonly Dictionary<string, FlowRule> FlowRules = new()
		{
			[AssetFlowTypes.Borrow] = new FlowRule(
				new[] { AssetStatusCodes.Idle },
				AssetStatusCodes.InUse,
				"领用"),
			[AssetFlowTypes.Return] = new FlowRule(
				new[] { AssetStatusCodes.InUse },
				AssetStatusCodes.Idle,
				"归还"),
			[AssetFlowTypes.Repair] = new FlowRule(
				new[] { AssetStatusCodes.InUse, AssetStatusCodes.Idle },
				AssetStatusCodes.Repair,
				"报修"),
			[AssetFlowTypes.RepairDone] = new FlowRule(
				new[] { AssetStatusCodes.Repair },
				null,
				"维修完成"),
			[AssetFlowTypes.Scrap] = new FlowRule(
				new[] { AssetStatusCodes.InUse, AssetStatusCodes.Idle, AssetStatusCodes.Repair },
				AssetStatusCodes.Scrapped,
				"报废")
		};

		/// <summary>全部合法资产状态</summary>
		public static IReadOnlyList<string> AllStatuses { get; } = new[]
		{
			AssetStatusCodes.InUse,
			AssetStatusCodes.Idle,
			AssetStatusCodes.Repair,
			AssetStatusCodes.Scrapped
		};

		/// <summary>全部流转单据类型</summary>
		public static IReadOnlyList<string> AllFlowTypes { get; } = new[]
		{
			AssetFlowTypes.Borrow,
			AssetFlowTypes.Return,
			AssetFlowTypes.Repair,
			AssetFlowTypes.RepairDone,
			AssetFlowTypes.Scrap
		};

		/// <summary>状态编码是否合法</summary>
		public static bool IsValidStatus(string? status)
		{
			return !string.IsNullOrWhiteSpace(status) && Transitions.ContainsKey(status);
		}

		/// <summary>单据类型编码是否合法</summary>
		public static bool IsValidFlowType(string? flowType)
		{
			return !string.IsNullOrWhiteSpace(flowType) && FlowRules.ContainsKey(flowType);
		}

		/// <summary>
		/// 判断状态跃迁是否被允许
		/// </summary>
		public static bool CanTransit(string? from, string? to)
		{
			if (!IsValidStatus(from) || !IsValidStatus(to)) return false;
			return Transitions[from!].Contains(to);
		}

		/// <summary>
		/// 获取指定状态允许流转到的目标状态集合
		/// </summary>
		public static IReadOnlyList<string> GetAllowedTargets(string? from)
		{
			if (!IsValidStatus(from)) return Array.Empty<string>();
			return Transitions[from!];
		}

		/// <summary>
		/// 获取指定状态当前可发起的流转单据类型
		/// （报废为终态时返回空集合）
		/// </summary>
		public static IReadOnlyList<string> GetAllowedFlowTypes(string? from)
		{
			if (!IsValidStatus(from)) return Array.Empty<string>();

			return FlowRules
				.Where(kv => kv.Value.FromStatuses.Contains(from))
				.Select(kv => kv.Key)
				.ToList();
		}

		/// <summary>
		/// 校验某个单据类型在当前资产状态下是否允许发起
		/// </summary>
		/// <param name="flowType">单据类型</param>
		/// <param name="from">资产当前状态</param>
		/// <param name="to">期望的目标状态（可为空，如维修完成）</param>
		/// <param name="error">校验失败原因</param>
		public static bool CanStartFlow(string? flowType, string? from, string? to, out string error)
		{
			if (!IsValidFlowType(flowType))
			{
				error = "未知的流转单据类型";
				return false;
			}

			if (!IsValidStatus(from))
			{
				error = "资产当前状态异常，无法办理流转";
				return false;
			}

			var rule = FlowRules[flowType!];

			if (!rule.FromStatuses.Contains(from))
			{
				error = $"当前状态「{GetStatusName(from)}」不允许发起{rule.Name}单据";
				return false;
			}

			if (rule.TargetStatus != null)
			{
				if (!CanTransit(from, rule.TargetStatus))
				{
					error = $"不允许从「{GetStatusName(from)}」变更为「{GetStatusName(rule.TargetStatus)}」";
					return false;
				}
				error = "";
				return true;
			}

			// 目标状态不固定（如维修完成）：必须显式指定且在允许集合内
			if (!IsValidStatus(to))
			{
				error = "请指定流转后的目标状态";
				return false;
			}

			if (!CanTransit(from, to))
			{
				error = $"不允许从「{GetStatusName(from)}」变更为「{GetStatusName(to)}」";
				return false;
			}

			error = "";
			return true;
		}

		/// <summary>资产状态编码转中文名</summary>
		public static string GetStatusName(string? code) => code switch
		{
			AssetStatusCodes.InUse => "在用",
			AssetStatusCodes.Idle => "闲置",
			AssetStatusCodes.Repair => "维修中",
			AssetStatusCodes.Scrapped => "已报废",
			_ => "未知"
		};

		/// <summary>流转单据类型转中文名</summary>
		public static string GetFlowTypeName(string? code) => code switch
		{
			AssetFlowTypes.Borrow => "领用",
			AssetFlowTypes.Return => "归还",
			AssetFlowTypes.Repair => "报修",
			AssetFlowTypes.RepairDone => "维修完成",
			AssetFlowTypes.Scrap => "报废",
			_ => "未知"
		};

		/// <summary>单据处理状态转中文名</summary>
		public static string GetFlowStatusName(string? code) => code switch
		{
			AssetFlowStatusCodes.Pending => "待处理",
			AssetFlowStatusCodes.Completed => "已完成",
			AssetFlowStatusCodes.Rejected => "已驳回",
			AssetFlowStatusCodes.Cancelled => "已取消",
			_ => "未知"
		};

		/// <summary>
		/// 流转规则（内部结构）
		/// </summary>
		private sealed class FlowRule
		{
			public string[] FromStatuses { get; }
			public string? TargetStatus { get; }
			public string Name { get; }

			public FlowRule(string[] fromStatuses, string? targetStatus, string name)
			{
				FromStatuses = fromStatuses;
				TargetStatus = targetStatus;
				Name = name;
			}
		}
	}
}
