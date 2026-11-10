using AssetManagementSystem.Dtos;
using AssetManagementSystem.Models;
using SqlSugar;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 资产流转服务（领用 / 归还 / 报修 / 维修完成 / 报废）
	/// 说明：所有状态变更必须经过本服务，保证
	/// "资产主表状态"与"流转单据（审计流水）"在同一事务内同步落库。
	/// </summary>
	public static class AssetFlowService
	{
		/// <summary>
		/// 办理结果
		/// </summary>
		public class FlowResult
		{
			public bool Success { get; set; }
			public string Message { get; set; } = "";
			public AssetFlow? Flow { get; set; }
		}

		/// <summary>
		/// 创建并提交流转单据
		/// </summary>
		public static async Task<FlowResult> CreateAsync(
			ISqlSugarClient db,
			AssetFlowCreateDto dto,
			string? operatorId,
			string? operatorName)
		{
			var result = new FlowResult();

			if (dto == null || string.IsNullOrWhiteSpace(dto.assetId))
			{
				result.Message = "请选择要办理的资产";
				return result;
			}

			if (!AssetStatusMachine.IsValidFlowType(dto.flowType))
			{
				result.Message = "未知的流转单据类型";
				return result;
			}

			var asset = await db.Queryable<Asset>().FirstAsync(a => a.Id == dto.assetId);
			if (asset == null)
			{
				result.Message = "资产不存在";
				return result;
			}

			var fromStatus = asset.AssetStatus ?? AssetStatusCodes.InUse;
			var toStatus = ResolveTargetStatus(dto.flowType, dto.toStatus);

			if (!AssetStatusMachine.CanStartFlow(dto.flowType, fromStatus, toStatus, out var error))
			{
				result.Message = error;
				return result;
			}

			// 领用单必须指定使用人（否则资产"在用"却无人负责，账实不符）
			string? useUserId = dto.useUserId;
			string? useUserName = dto.useUserName;
			string? deptId = dto.deptId;
			string? deptName = dto.deptName;

			if (dto.flowType == AssetFlowTypes.Borrow)
			{
				if (string.IsNullOrWhiteSpace(useUserId))
				{
					result.Message = "领用单必须选择使用人";
					return result;
				}

				var user = await db.Queryable<SysUser>().FirstAsync(u => u.Id == useUserId);
				if (user != null)
				{
					useUserName = user.UserName ?? user.NickName ?? useUserName;
					if (string.IsNullOrWhiteSpace(deptId)) deptId = user.DeptId;
				}

				if (!string.IsNullOrWhiteSpace(deptId) && string.IsNullOrWhiteSpace(deptName))
				{
					var dept = await db.Queryable<Department>().FirstAsync(d => d.Id == deptId);
					deptName = dept?.DepartmentName;
				}
			}

			var now = DateTime.Now;
			var flow = new AssetFlow
			{
				Id = Guid.NewGuid().ToString(),
				FlowNo = GenerateFlowNo(),
				FlowType = dto.flowType,
				FlowStatus = dto.flowType == AssetFlowTypes.Repair
					? AssetFlowStatusCodes.Pending
					: AssetFlowStatusCodes.Completed,
				AssetId = asset.Id,
				AssetCode = asset.AssetCode,
				AssetName = asset.AssetName,
				FromStatus = fromStatus,
				ToStatus = toStatus,
				DeptId = deptId,
				DeptName = deptName,
				UseUserId = useUserId,
				UseUserName = useUserName,
				ApplyUserId = operatorId,
				ApplyUserName = operatorName,
				HandleUserId = dto.flowType == AssetFlowTypes.Repair ? null : operatorId,
				HandleUserName = dto.flowType == AssetFlowTypes.Repair ? null : operatorName,
				HandleTime = dto.flowType == AssetFlowTypes.Repair ? null : now,
				Fee = dto.fee,
				ExpectReturnDate = dto.expectReturnDate,
				Remark = dto.remark,
				IsEnabled = true,
				StatusCode = 1,
				StatusDesc = "启用",
				CreateTime = now,
				UpdateTime = now
			};

			try
			{
				db.Ado.BeginTran();

				await db.Insertable(flow).ExecuteCommandAsync();

				// 同步资产主表：状态 + 领用信息（归还时清空使用人）
				await ApplyAssetChangeAsync(db, asset, dto.flowType, toStatus, useUserId, useUserName, deptId, deptName);

				db.Ado.CommitTran();
			}
			catch (Exception ex)
			{
				db.Ado.RollbackTran();
				result.Message = $"办理失败：{ex.Message}";
				return result;
			}

			result.Success = true;
			result.Flow = flow;
			result.Message = $"{AssetStatusMachine.GetFlowTypeName(dto.flowType)}单据已提交";
			return result;
		}

		/// <summary>
		/// 完成待处理单据（维修完成 → 恢复在用 / 闲置）
		/// </summary>
		public static async Task<FlowResult> CompleteAsync(
			ISqlSugarClient db,
			string flowId,
			AssetFlowCompleteDto dto,
			string? operatorId,
			string? operatorName)
		{
			var result = new FlowResult();

			if (string.IsNullOrWhiteSpace(flowId))
			{
				result.Message = "单据ID不能为空";
				return result;
			}

			var flow = await db.Queryable<AssetFlow>().FirstAsync(f => f.Id == flowId);
			if (flow == null)
			{
				result.Message = "单据不存在";
				return result;
			}

			if (flow.FlowStatus != AssetFlowStatusCodes.Pending)
			{
				result.Message = $"单据状态为「{AssetStatusMachine.GetFlowStatusName(flow.FlowStatus)}」，不可再次处理";
				return result;
			}

			var asset = await db.Queryable<Asset>().FirstAsync(a => a.Id == flow.AssetId);
			if (asset == null)
			{
				result.Message = "关联资产不存在，无法完成单据";
				return result;
			}

			var fromStatus = asset.AssetStatus ?? AssetStatusCodes.InUse;
			var toStatus = dto?.toStatus;

			if (!AssetStatusMachine.CanStartFlow(AssetFlowTypes.RepairDone, fromStatus, toStatus, out var error))
			{
				result.Message = error;
				return result;
			}

			var now = DateTime.Now;

			try
			{
				db.Ado.BeginTran();

				// 1. 资产状态跃迁
				asset.AssetStatus = toStatus;
				asset.UpdateTime = now;
				await db.Updateable(asset)
					.UpdateColumns(a => new { a.AssetStatus, a.UpdateTime })
					.ExecuteCommandAsync();

				// 2. 原单据置为已完成
				flow.FlowStatus = AssetFlowStatusCodes.Completed;
				flow.HandleUserId = operatorId;
				flow.HandleUserName = operatorName;
				flow.HandleTime = now;
				flow.HandleResult = dto?.handleResult;
				if (dto?.fee.HasValue == true) flow.Fee = dto.fee;
				flow.UpdateTime = now;
				await db.Updateable(flow).ExecuteCommandAsync();

				// 3. 补一条"维修完成"流水，完整记录 repair → in_use/idle 的跃迁轨迹
				var doneFlow = new AssetFlow
				{
					Id = Guid.NewGuid().ToString(),
					FlowNo = GenerateFlowNo(),
					FlowType = AssetFlowTypes.RepairDone,
					FlowStatus = AssetFlowStatusCodes.Completed,
					AssetId = flow.AssetId,
					AssetCode = flow.AssetCode,
					AssetName = flow.AssetName,
					FromStatus = fromStatus,
					ToStatus = toStatus,
					DeptId = flow.DeptId,
					DeptName = flow.DeptName,
					UseUserId = flow.UseUserId,
					UseUserName = flow.UseUserName,
					ApplyUserId = operatorId,
					ApplyUserName = operatorName,
					HandleUserId = operatorId,
					HandleUserName = operatorName,
					HandleTime = now,
					Fee = dto?.fee ?? flow.Fee,
					HandleResult = dto?.handleResult,
					Remark = $"关联报修单 {flow.FlowNo}",
					IsEnabled = true,
					StatusCode = 1,
					StatusDesc = "启用",
					CreateTime = now,
					UpdateTime = now
				};
				await db.Insertable(doneFlow).ExecuteCommandAsync();

				db.Ado.CommitTran();

				result.Flow = doneFlow;
			}
			catch (Exception ex)
			{
				db.Ado.RollbackTran();
				result.Message = $"处理失败：{ex.Message}";
				return result;
			}

			result.Success = true;
			result.Message = "单据已完成处理";
			return result;
		}

		/// <summary>
		/// 取消 / 驳回待处理单据（资产状态回退到单据发起时的状态）
		/// </summary>
		public static async Task<FlowResult> CloseAsync(
			ISqlSugarClient db,
			string flowId,
			bool rejected,
			string? reason,
			string? operatorId,
			string? operatorName)
		{
			var result = new FlowResult();

			if (string.IsNullOrWhiteSpace(flowId))
			{
				result.Message = "单据ID不能为空";
				return result;
			}

			var flow = await db.Queryable<AssetFlow>().FirstAsync(f => f.Id == flowId);
			if (flow == null)
			{
				result.Message = "单据不存在";
				return result;
			}

			if (flow.FlowStatus != AssetFlowStatusCodes.Pending)
			{
				result.Message = $"单据状态为「{AssetStatusMachine.GetFlowStatusName(flow.FlowStatus)}」，不可再次处理";
				return result;
			}

			var now = DateTime.Now;

			try
			{
				db.Ado.BeginTran();

				flow.FlowStatus = rejected ? AssetFlowStatusCodes.Rejected : AssetFlowStatusCodes.Cancelled;
				flow.HandleUserId = operatorId;
				flow.HandleUserName = operatorName;
				flow.HandleTime = now;
				flow.HandleResult = reason;
				flow.UpdateTime = now;
				await db.Updateable(flow).ExecuteCommandAsync();

				// 报修单被取消/驳回时，资产状态回退到发起报修之前的状态
				if (flow.FlowType == AssetFlowTypes.Repair && !string.IsNullOrWhiteSpace(flow.FromStatus))
				{
					var asset = await db.Queryable<Asset>().FirstAsync(a => a.Id == flow.AssetId);
					if (asset != null)
					{
						asset.AssetStatus = flow.FromStatus;
						asset.UpdateTime = now;
						await db.Updateable(asset)
							.UpdateColumns(a => new { a.AssetStatus, a.UpdateTime })
							.ExecuteCommandAsync();
					}
				}

				db.Ado.CommitTran();

				result.Flow = flow;
			}
			catch (Exception ex)
			{
				db.Ado.RollbackTran();
				result.Message = $"操作失败：{ex.Message}";
				return result;
			}

			result.Success = true;
			result.Message = rejected ? "单据已驳回" : "单据已取消";
			return result;
		}

		/// <summary>
		/// 根据单据类型解析目标状态
		/// </summary>
		private static string? ResolveTargetStatus(string flowType, string? toStatus)
		{
			return flowType switch
			{
				AssetFlowTypes.Borrow => AssetStatusCodes.InUse,
				AssetFlowTypes.Return => AssetStatusCodes.Idle,
				AssetFlowTypes.Repair => AssetStatusCodes.Repair,
				AssetFlowTypes.Scrap => AssetStatusCodes.Scrapped,
				// 维修完成的目标状态由业务指定（在用 / 闲置）
				_ => toStatus
			};
		}

		/// <summary>
		/// 同步资产主表：状态 + 领用信息
		/// </summary>
		private static async Task ApplyAssetChangeAsync(
			ISqlSugarClient db,
			Asset asset,
			string flowType,
			string? toStatus,
			string? useUserId,
			string? useUserName,
			string? deptId,
			string? deptName)
		{
			asset.AssetStatus = toStatus;
			asset.UpdateTime = DateTime.Now;

			switch (flowType)
			{
				case AssetFlowTypes.Borrow:
					asset.UseUserId = useUserId;
					asset.UseUserName = useUserName;
					if (!string.IsNullOrWhiteSpace(deptId))
					{
						asset.DeptId = deptId;
						asset.DeptName = deptName;
					}
					break;

				case AssetFlowTypes.Return:
					// 归还后资产回到闲置池，使用人清空（历史使用人保留在流转单据里）
					asset.UseUserId = null;
					asset.UseUserName = null;
					break;

				case AssetFlowTypes.Scrap:
					asset.UseUserId = null;
					asset.UseUserName = null;
					break;
			}

			await db.Updateable(asset)
				.UpdateColumns(a => new
				{
					a.AssetStatus,
					a.UseUserId,
					a.UseUserName,
					a.DeptId,
					a.DeptName,
					a.UpdateTime
				})
				.ExecuteCommandAsync();
		}

		/// <summary>
		/// 生成单据编号：LF + yyyyMMddHHmmss + 4 位随机数
		/// </summary>
		private static string GenerateFlowNo()
		{
			var random = Random.Shared.Next(1000, 9999);
			return $"LF{DateTime.Now:yyyyMMddHHmmss}{random}";
		}
	}
}
