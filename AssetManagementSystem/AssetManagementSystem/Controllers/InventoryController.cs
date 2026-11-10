using AssetManagementSystem.Attributes;
using AssetManagementSystem.Dtos;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	/// <summary>
	/// 资产盘点（盘点单 + 明细录入 + 账实差异汇总 + 差异转单据）
	/// </summary>
	[ApiController]
	[Route("inventory")]
	[Authorize]
	public class InventoryController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;

		public InventoryController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 分页查询盘点单
		/// </summary>
		[HttpGet("page")]
		[Permission(Permissions.InventoryList)]
		public async Task<ApiResult<PageResult<InventoryDto>>> GetPage([FromQuery] InventoryQueryDto query)
		{
			query ??= new InventoryQueryDto();
			(query.pageIndex, query.pageSize) = PageGuard.Normalize(query.pageIndex, query.pageSize);

			var total = 0;
			var totalRef = new RefAsync<int>(0);
			var list = await _db.Queryable<AssetInventory>()
				.WhereIF(!string.IsNullOrWhiteSpace(query.keyword), i =>
					i.InventoryNo.Contains(query.keyword) ||
					i.InventoryName.Contains(query.keyword))
				.WhereIF(!string.IsNullOrWhiteSpace(query.inventoryStatus), i => i.InventoryStatus == query.inventoryStatus)
				.WhereIF(!string.IsNullOrWhiteSpace(query.deptId), i => i.DeptId == query.deptId)
				.WhereIF(query.startDate.HasValue, i => i.CreateTime >= query.startDate)
				.WhereIF(query.endDate.HasValue, i => i.CreateTime <= query.endDate)
				.OrderBy(i => i.CreateTime, OrderByType.Desc)
				.ToPageListAsync(query.pageIndex, query.pageSize, totalRef);
			total = totalRef.Value;

			var dtoList = list.Select(MapToDto).ToList();
			var result = new PageResult<InventoryDto>(dtoList, total, query.pageIndex, query.pageSize);
			return ApiResult<PageResult<InventoryDto>>.Success(result, "获取成功");
		}

		/// <summary>
		/// 盘点单详情（含账实差异汇总）
		/// </summary>
		[HttpGet("{id}")]
		[Permission(Permissions.InventoryList)]
		public async Task<ApiResult<object>> GetById(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "盘点单ID不能为空");

			var inventory = await _db.Queryable<AssetInventory>().FirstAsync(i => i.Id == id);
			if (inventory == null)
				return ApiResult<object>.Fail("404", "盘点单不存在");

			var summary = await BuildSummaryAsync(id);
			return ApiResult<object>.Success(new
			{
				inventory = MapToDto(inventory),
				summary
			}, "获取成功");
		}

		/// <summary>
		/// 盘点明细分页
		/// </summary>
		[HttpGet("{id}/items")]
		[Permission(Permissions.InventoryList)]
		public async Task<ApiResult<PageResult<InventoryItemDto>>> GetItems(
			string id,
			[FromQuery] bool onlyDiff = false,
			[FromQuery] int pageIndex = 1,
			[FromQuery] int pageSize = 20)
		{
			(pageIndex, pageSize) = PageGuard.Normalize(pageIndex, pageSize);

			var total = 0;
			var totalRef = new RefAsync<int>(0);
			var list = await _db.Queryable<AssetInventoryItem>()
				.Where(i => i.InventoryId == id)
				.WhereIF(onlyDiff, i => i.IsDiff == true)
				.OrderBy(i => i.AssetCode)
				.ToPageListAsync(pageIndex, pageSize, totalRef);
			total = totalRef.Value;

			var dtoList = list.Select(MapItemToDto).ToList();
			return ApiResult<PageResult<InventoryItemDto>>.Success(
				new PageResult<InventoryItemDto>(dtoList, total, pageIndex, pageSize),
				"获取成功");
		}

		/// <summary>
		/// 创建盘点单（对范围内资产生成账面快照明细）
		/// </summary>
		[HttpPost]
		[Permission(Permissions.InventoryManage)]
		public async Task<ApiResult<object>> Create([FromBody] InventoryCreateDto dto)
		{
			dto ??= new InventoryCreateDto();

			var deptId = dto.deptId;
			string? deptName = dto.deptName;
			if (!string.IsNullOrWhiteSpace(deptId))
			{
				var dept = await _db.Queryable<Department>().FirstAsync(d => d.Id == deptId);
				if (dept == null)
					return ApiResult<object>.Fail("400", "盘点部门不存在");
				deptName = dept.DepartmentName;
			}

			// 盘点范围：指定部门则只盘该部门，否则全量盘点
			var assets = await _db.Queryable<Asset>()
				.WhereIF(!string.IsNullOrWhiteSpace(deptId), a => a.DeptId == deptId)
				.Where(a => a.IsEnabled != false)
				.ToListAsync();

			if (assets.Count == 0)
				return ApiResult<object>.Fail("400", "盘点范围内没有资产");

			var now = DateTime.Now;
			var inventory = new AssetInventory
			{
				Id = Guid.NewGuid().ToString(),
				InventoryNo = $"PD{now:yyyyMMdd}{Random.Shared.Next(1000, 9999)}",
				InventoryName = string.IsNullOrWhiteSpace(dto.inventoryName)
					? $"{deptName ?? "全部部门"}资产盘点"
					: dto.inventoryName.Trim(),
				DeptId = deptId,
				DeptName = deptName,
				InventoryStatus = AssetInventoryStatusCodes.Draft,
				PlanDate = dto.planDate,
				StartTime = now,
				OwnerId = CurrentUserId(),
				OwnerName = CurrentUserName(),
				TotalCount = assets.Count,
				CheckedCount = 0,
				DiffCount = 0,
				TotalAmount = assets.Sum(a => a.OriginalValue ?? 0),
				Remark = dto.remark,
				IsEnabled = true,
				StatusCode = 1,
				StatusDesc = "启用",
				CreateTime = now,
				UpdateTime = now
			};

			var items = assets.Select(a => new AssetInventoryItem
			{
				Id = Guid.NewGuid().ToString(),
				InventoryId = inventory.Id,
				AssetId = a.Id,
				AssetCode = a.AssetCode,
				AssetName = a.AssetName,
				CategoryName = a.CategoryName,
				DeptName = a.DeptName,
				BookLocation = a.Location,
				BookUserName = a.UseUserName,
				BookStatus = a.AssetStatus ?? AssetStatusCodes.InUse,
				BookValue = a.OriginalValue,
				IsDiff = false,
				Handled = false,
				IsEnabled = true,
				StatusCode = 1,
				StatusDesc = "启用",
				CreateTime = now,
				UpdateTime = now
			}).ToList();

			try
			{
				_db.Ado.BeginTran();

				await _db.Insertable(inventory).ExecuteCommandAsync();
				await _db.Insertable(items).ExecuteCommandAsync();

				_db.Ado.CommitTran();
			}
			catch (Exception ex)
			{
				_db.Ado.RollbackTran();
				return ApiResult<object>.Fail("500", $"创建盘点单失败：{ex.Message}");
			}

			return ApiResult<object>.Success(
				new { id = inventory.Id, inventoryNo = inventory.InventoryNo, totalCount = items.Count },
				$"盘点单已创建，共生成 {items.Count} 条盘点明细");
		}

		/// <summary>
		/// 批量录入实盘结果
		/// </summary>
		[HttpPost("{id}/check")]
		[Permission(Permissions.InventoryManage)]
		public async Task<ApiResult<object>> Check(string id, [FromBody] List<InventoryItemCheckDto> list)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "盘点单ID不能为空");
			if (list == null || list.Count == 0)
				return ApiResult<object>.Fail("400", "请至少录入一条实盘结果");

			var inventory = await _db.Queryable<AssetInventory>().FirstAsync(i => i.Id == id);
			if (inventory == null)
				return ApiResult<object>.Fail("404", "盘点单不存在");
			if (inventory.InventoryStatus != AssetInventoryStatusCodes.Draft)
				return ApiResult<object>.Fail("400", "盘点单已完成，不能继续录入");

			var items = await _db.Queryable<AssetInventoryItem>()
				.Where(i => i.InventoryId == id && list.Select(x => x.id).Contains(i.Id))
				.ToListAsync();

			if (items.Count == 0)
				return ApiResult<object>.Fail("400", "未找到对应的盘点明细");

			var operatorId = CurrentUserId();
			var operatorName = CurrentUserName();
			var now = DateTime.Now;

			foreach (var item in items)
			{
				var input = list.FirstOrDefault(x => x.id == item.Id);
				if (input == null) continue;

				// 未选择实盘状态的行视为"未盘点"，不写入盘点时间，避免误判为已盘
				if (string.IsNullOrWhiteSpace(input.actualStatus)) continue;

				if (!IsValidActualStatus(input.actualStatus))
					return ApiResult<object>.Fail("400", "实盘状态取值非法");

				item.ActualStatus = input.actualStatus;
				item.ActualLocation = input.actualLocation ?? item.ActualLocation;
				item.ActualUserName = input.actualUserName ?? item.ActualUserName;
				item.CheckedUserId = operatorId;
				item.CheckedUserName = operatorName;
				item.CheckedTime = now;
				item.UpdateTime = now;

				var (isDiff, diffDesc) = EvaluateDiff(item);
				item.IsDiff = isDiff;
				item.DiffDesc = diffDesc;
			}

			try
			{
				_db.Ado.BeginTran();

				await _db.Updateable(items).ExecuteCommandAsync();

				// 同步盘点单的已盘 / 差异计数
				var stats = await _db.Queryable<AssetInventoryItem>()
					.Where(i => i.InventoryId == id)
					.ToListAsync();

				inventory.CheckedCount = stats.Count(i => i.CheckedTime != null);
				inventory.DiffCount = stats.Count(i => i.IsDiff == true);
				inventory.UpdateTime = now;
				await _db.Updateable(inventory)
					.UpdateColumns(i => new { i.CheckedCount, i.DiffCount, i.UpdateTime })
					.ExecuteCommandAsync();

				_db.Ado.CommitTran();
			}
			catch (Exception ex)
			{
				_db.Ado.RollbackTran();
				return ApiResult<object>.Fail("500", $"录入实盘结果失败：{ex.Message}");
			}

			return ApiResult<object>.Success(
				new { checkedCount = inventory.CheckedCount, diffCount = inventory.DiffCount },
				$"已录入 {items.Count} 条实盘结果");
		}

		/// <summary>
		/// 完成盘点（汇总账实差异）
		/// </summary>
		[HttpPost("{id}/complete")]
		[Permission(Permissions.InventoryManage)]
		public async Task<ApiResult<object>> Complete(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "盘点单ID不能为空");

			var inventory = await _db.Queryable<AssetInventory>().FirstAsync(i => i.Id == id);
			if (inventory == null)
				return ApiResult<object>.Fail("404", "盘点单不存在");
			if (inventory.InventoryStatus != AssetInventoryStatusCodes.Draft)
				return ApiResult<object>.Fail("400", "盘点单已完成");

			var items = await _db.Queryable<AssetInventoryItem>()
				.Where(i => i.InventoryId == id)
				.ToListAsync();

			if (items.Count == 0)
				return ApiResult<object>.Fail("400", "盘点单没有明细");

			var uncheckedCount = items.Count(i => i.CheckedTime == null);
			if (uncheckedCount > 0)
				return ApiResult<object>.Fail("400", $"还有 {uncheckedCount} 条资产未盘点，请先完成录入");

			var now = DateTime.Now;
			var diffItems = items.Where(i => i.IsDiff == true).ToList();

			inventory.InventoryStatus = AssetInventoryStatusCodes.Completed;
			inventory.EndTime = now;
			inventory.TotalCount = items.Count;
			inventory.CheckedCount = items.Count;
			inventory.DiffCount = diffItems.Count;
			inventory.TotalAmount = items.Sum(i => i.BookValue ?? 0);
			inventory.UpdateTime = now;

			try
			{
				_db.Ado.BeginTran();

				await _db.Updateable(inventory).ExecuteCommandAsync();

				_db.Ado.CommitTran();
			}
			catch (Exception ex)
			{
				_db.Ado.RollbackTran();
				return ApiResult<object>.Fail("500", $"完成盘点失败：{ex.Message}");
			}

			var summary = await BuildSummaryAsync(id);
			return ApiResult<object>.Success(summary, $"盘点已完成，共发现 {diffItems.Count} 条账实差异");
		}

		/// <summary>
		/// 删除盘点单（仅允许删除盘点中的单据）
		/// </summary>
		[HttpDelete("{id}")]
		[Permission(Permissions.InventoryManage)]
		public async Task<ApiResult<object>> Delete(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "盘点单ID不能为空");

			var inventory = await _db.Queryable<AssetInventory>().FirstAsync(i => i.Id == id);
			if (inventory == null)
				return ApiResult<object>.Fail("404", "盘点单不存在");
			if (inventory.InventoryStatus != AssetInventoryStatusCodes.Draft)
				return ApiResult<object>.Fail("400", "已完成的盘点单不允许删除");

			try
			{
				_db.Ado.BeginTran();

				await _db.Deleteable<AssetInventoryItem>().Where(i => i.InventoryId == id).ExecuteCommandAsync();
				await _db.Deleteable<AssetInventory>().Where(i => i.Id == id).ExecuteCommandAsync();

				_db.Ado.CommitTran();
			}
			catch (Exception ex)
			{
				_db.Ado.RollbackTran();
				return ApiResult<object>.Fail("500", $"删除失败：{ex.Message}");
			}

			return ApiResult<object>.Success(null, "删除成功");
		}

		/// <summary>
		/// 差异转处理单据（报废 / 报修），与流转模块形成闭环
		/// </summary>
		[HttpPost("items/{itemId}/handle")]
		[Permission(Permissions.InventoryManage)]
		public async Task<ApiResult<object>> HandleItem(string itemId, [FromBody] InventoryItemHandleDto dto)
		{
			if (string.IsNullOrWhiteSpace(itemId))
				return ApiResult<object>.Fail("400", "盘点明细ID不能为空");

			var item = await _db.Queryable<AssetInventoryItem>().FirstAsync(i => i.Id == itemId);
			if (item == null)
				return ApiResult<object>.Fail("404", "盘点明细不存在");
			if (item.IsDiff != true)
				return ApiResult<object>.Fail("400", "该明细无账实差异，无需处理");
			if (item.Handled == true)
				return ApiResult<object>.Fail("400", "该差异已生成过处理单据");

			var flowType = dto?.flowType;
			if (!AssetStatusMachine.IsValidFlowType(flowType))
				return ApiResult<object>.Fail("400", "请选择处理方式（报废 / 报修）");

			var createDto = new AssetFlowCreateDto
			{
				assetId = item.AssetId,
				flowType = flowType!,
				remark = $"盘点差异处理：{item.DiffDesc ?? item.AssetName}"
			};

			var result = await AssetFlowService.CreateAsync(_db, createDto, CurrentUserId(), CurrentUserName());
			if (!result.Success || result.Flow == null)
				return ApiResult<object>.Fail("400", result.Message);

			item.Handled = true;
			item.HandledFlowId = result.Flow.Id;
			item.UpdateTime = DateTime.Now;
			await _db.Updateable(item).ExecuteCommandAsync();

			return ApiResult<object>.Success(
				new { flowId = result.Flow.Id, flowNo = result.Flow.FlowNo },
				"已生成处理单据");
		}

		/// <summary>
		/// 汇总盘点差异
		/// </summary>
		private async Task<InventorySummaryDto> BuildSummaryAsync(string inventoryId)
		{
			var items = await _db.Queryable<AssetInventoryItem>()
				.Where(i => i.InventoryId == inventoryId)
				.ToListAsync();

			var diffItems = items.Where(i => i.IsDiff == true).ToList();
			var checkedCount = items.Count(i => i.CheckedTime != null);
			var totalCount = items.Count;

			var summary = new InventorySummaryDto
			{
				totalCount = totalCount,
				checkedCount = checkedCount,
				diffCount = diffItems.Count,
				missingCount = items.Count(i => i.ActualStatus == InventoryActualStatusCodes.Missing),
				totalAmount = items.Sum(i => i.BookValue ?? 0),
				diffAmount = diffItems.Sum(i => i.BookValue ?? 0),
				accuracyRate = totalCount > 0
					? Math.Round((totalCount - diffItems.Count) * 100m / totalCount, 2)
					: 0m
			};

			summary.statusStats = items
				.Where(i => !string.IsNullOrWhiteSpace(i.ActualStatus))
				.GroupBy(i => i.ActualStatus!)
				.Select(g => new StatItemDto
				{
					code = g.Key,
					name = GetActualStatusName(g.Key),
					value = g.Count(),
					amount = g.Sum(x => x.BookValue ?? 0)
				})
				.OrderByDescending(x => x.value)
				.ToList();

			return summary;
		}

		/// <summary>
		/// 依据账面与实盘信息判定是否存在账实差异
		/// </summary>
		private static (bool isDiff, string? diffDesc) EvaluateDiff(AssetInventoryItem item)
		{
			var reasons = new List<string>();

			// 1. 状态差异（normal 表示实物在册且状态与账面对应）
			if (!string.IsNullOrWhiteSpace(item.ActualStatus))
			{
				if (item.ActualStatus == InventoryActualStatusCodes.Missing)
				{
					reasons.Add($"实物缺失（账面：{AssetStatusMachine.GetStatusName(item.BookStatus)}）");
				}
				else if (item.ActualStatus != InventoryActualStatusCodes.Normal &&
					!string.Equals(item.ActualStatus, item.BookStatus, StringComparison.Ordinal))
				{
					reasons.Add($"状态不符（账面：{AssetStatusMachine.GetStatusName(item.BookStatus)} / 实盘：{GetActualStatusName(item.ActualStatus)}）");
				}
			}

			// 2. 位置差异
			if (!string.IsNullOrWhiteSpace(item.ActualLocation) &&
				!string.IsNullOrWhiteSpace(item.BookLocation) &&
				!string.Equals(item.ActualLocation, item.BookLocation, StringComparison.Ordinal))
			{
				reasons.Add($"存放位置不符（账面：{item.BookLocation} / 实盘：{item.ActualLocation}）");
			}

			// 3. 使用人差异
			if (!string.IsNullOrWhiteSpace(item.ActualUserName) &&
				!string.IsNullOrWhiteSpace(item.BookUserName) &&
				!string.Equals(item.ActualUserName, item.BookUserName, StringComparison.Ordinal))
			{
				reasons.Add($"使用人不符（账面：{item.BookUserName} / 实盘：{item.ActualUserName}）");
			}

			return (reasons.Count > 0, reasons.Count > 0 ? string.Join("；", reasons) : null);
		}

		/// <summary>
		/// 实盘状态编码是否合法
		/// </summary>
		private static bool IsValidActualStatus(string status)
		{
			return status == InventoryActualStatusCodes.Normal
				|| status == InventoryActualStatusCodes.Idle
				|| status == InventoryActualStatusCodes.Repair
				|| status == InventoryActualStatusCodes.Scrapped
				|| status == InventoryActualStatusCodes.Missing;
		}

		/// <summary>
		/// 实盘状态编码转中文名
		/// </summary>
		private static string GetActualStatusName(string? code) => code switch
		{
			InventoryActualStatusCodes.Normal => "正常",
			InventoryActualStatusCodes.Idle => "闲置",
			InventoryActualStatusCodes.Repair => "维修中",
			InventoryActualStatusCodes.Scrapped => "已报废",
			InventoryActualStatusCodes.Missing => "盘亏",
			_ => "未盘"
		};

		private static InventoryDto MapToDto(AssetInventory entity)
		{
			return new InventoryDto
			{
				id = entity.Id,
				inventoryNo = entity.InventoryNo ?? "",
				inventoryName = entity.InventoryName ?? "",
				deptId = entity.DeptId,
				deptName = entity.DeptName,
				inventoryStatus = entity.InventoryStatus ?? "",
				inventoryStatusName = entity.InventoryStatus == AssetInventoryStatusCodes.Completed ? "已完成" : "盘点中",
				planDate = entity.PlanDate,
				startTime = entity.StartTime,
				endTime = entity.EndTime,
				ownerId = entity.OwnerId,
				ownerName = entity.OwnerName,
				totalCount = entity.TotalCount,
				diffCount = entity.DiffCount,
				checkedCount = entity.CheckedCount,
				totalAmount = entity.TotalAmount,
				remark = entity.Remark,
				createTime = entity.CreateTime,
				updateTime = entity.UpdateTime
			};
		}

		private static InventoryItemDto MapItemToDto(AssetInventoryItem entity)
		{
			return new InventoryItemDto
			{
				id = entity.Id,
				inventoryId = entity.InventoryId ?? "",
				assetId = entity.AssetId ?? "",
				assetCode = entity.AssetCode,
				assetName = entity.AssetName,
				categoryName = entity.CategoryName,
				deptName = entity.DeptName,
				bookLocation = entity.BookLocation,
				bookUserName = entity.BookUserName,
				bookStatus = entity.BookStatus,
				bookStatusName = AssetStatusMachine.GetStatusName(entity.BookStatus),
				actualStatus = entity.ActualStatus,
				actualStatusName = GetActualStatusName(entity.ActualStatus),
				actualLocation = entity.ActualLocation,
				actualUserName = entity.ActualUserName,
				bookValue = entity.BookValue,
				isDiff = entity.IsDiff,
				diffDesc = entity.DiffDesc,
				checkedUserName = entity.CheckedUserName,
				checkedTime = entity.CheckedTime,
				handled = entity.Handled,
				handledFlowId = entity.HandledFlowId
			};
		}

		/// <summary>
		/// 当前登录用户主键（JWT sub）
		/// </summary>
		private string? CurrentUserId()
		{
			return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
				?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
		}

		/// <summary>
		/// 当前登录用户姓名（JWT name）
		/// </summary>
		private string? CurrentUserName()
		{
			return User.FindFirst(ClaimTypes.Name)?.Value;
		}
	}
}
