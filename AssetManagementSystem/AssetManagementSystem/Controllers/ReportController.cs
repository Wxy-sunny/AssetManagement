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
using System.Linq;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	/// <summary>
	/// 报表中心：资产台账 / 分类汇总 / 状态汇总 / 部门分布 / 折旧汇总 / 流转记录 / 盘点差异
	/// 说明：导出在服务端内存中生成字节流后直接返回，不落盘（容器内无可写共享目录）。
	/// </summary>
	[ApiController]
	[Route("report")]
	[Authorize]
	public class ReportController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;

		public ReportController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>报表类型定义</summary>
		private static readonly List<ReportTypeDto> ReportTypes = new List<ReportTypeDto>
		{
			new ReportTypeDto { code = "asset_ledger", name = "资产台账", description = "逐条资产明细（编码、分类、金额、状态、使用人等）" },
			new ReportTypeDto { code = "asset_category", name = "分类汇总", description = "按资产分类统计数量与金额" },
			new ReportTypeDto { code = "asset_status", name = "状态汇总", description = "按资产状态统计数量与金额" },
			new ReportTypeDto { code = "asset_dept", name = "部门资产分布", description = "按使用部门统计资产数量与金额" },
			new ReportTypeDto { code = "depreciation", name = "折旧汇总", description = "按分类 + 折旧方式统计折旧与净值" },
			new ReportTypeDto { code = "flow_log", name = "流转记录", description = "领用 / 归还 / 维修 / 报废全流程流水" },
			new ReportTypeDto { code = "inventory_diff", name = "盘点差异", description = "历次盘点的账实差异清单" }
		};

		/// <summary>
		/// 可用报表类型
		/// </summary>
		[HttpGet("types")]
		[Permission(Permissions.ReportView)]
		public ApiResult<List<ReportTypeDto>> GetTypes()
		{
			return ApiResult<List<ReportTypeDto>>.Success(ReportTypes, "获取成功");
		}

		/// <summary>
		/// 报表数据预览（分页）
		/// </summary>
		[HttpGet("data")]
		[Permission(Permissions.ReportView)]
		public async Task<ApiResult<ReportPreviewDto>> GetData([FromQuery] ReportQueryDto query)
		{
			query ??= new ReportQueryDto();
			var type = string.IsNullOrWhiteSpace(query.reportType) ? "asset_ledger" : query.reportType!;

			if (!ReportTypes.Any(t => t.code == type))
				return ApiResult<ReportPreviewDto>.Fail("400", "未知的报表类型");

			var table = await BuildTableAsync(type, query);

			// 预览只返回当前页数据，导出会取全量
			var pageIndex = Math.Max(1, query.pageIndex);
			var pageSize = Math.Clamp(query.pageSize, 1, 500);
			var total = table.Rows.Count;
			var rows = table.Rows.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();

			return ApiResult<ReportPreviewDto>.Success(new ReportPreviewDto
			{
				reportType = type,
				title = table.Title,
				columns = table.Columns,
				rows = rows,
				total = total
			}, "获取成功");
		}

		/// <summary>
		/// 导出报表（xlsx / csv）
		/// </summary>
		[HttpGet("export")]
		[Permission(Permissions.ReportExport)]
		public async Task<IActionResult> Export([FromQuery] ReportQueryDto query)
		{
			query ??= new ReportQueryDto();
			var type = string.IsNullOrWhiteSpace(query.reportType) ? "asset_ledger" : query.reportType!;

			if (!ReportTypes.Any(t => t.code == type))
				return BadRequest(new { code = 400, message = "未知的报表类型" });

			var format = (query.format ?? "xlsx").Trim().ToLowerInvariant();
			if (format != "xlsx" && format != "csv")
				return BadRequest(new { code = 400, message = "导出格式仅支持 xlsx / csv" });

			var table = await BuildTableAsync(type, query);
			byte[] content;
			string mimeType;
			string extension;

			if (format == "csv")
			{
				content = ReportExportService.BuildCsv(table);
				mimeType = "text/csv; charset=utf-8";
				extension = "csv";
			}
			else
			{
				content = ReportExportService.BuildXlsx(table);
				mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
				extension = "xlsx";
			}

			var fileName = $"{table.Title}_{DateTime.Now:yyyyMMddHHmmss}.{extension}";
			// FileDownloadName 由框架按 RFC 5987 自动编码，中文文件名不会乱码
			return new FileContentResult(content, mimeType)
			{
				FileDownloadName = fileName
			};
		}

		/// <summary>
		/// 按报表类型构建数据表
		/// </summary>
		private async Task<ReportTable> BuildTableAsync(string type, ReportQueryDto query)
		{
			return type switch
			{
				"asset_category" => await BuildCategoryTableAsync(query),
				"asset_status" => await BuildStatusTableAsync(query),
				"asset_dept" => await BuildDeptTableAsync(query),
				"depreciation" => await BuildDepreciationTableAsync(query),
				"flow_log" => await BuildFlowTableAsync(query),
				"inventory_diff" => await BuildInventoryDiffTableAsync(query),
				_ => await BuildLedgerTableAsync(query)
			};
		}

		/// <summary>
		/// 资产台账明细
		/// </summary>
		private async Task<ReportTable> BuildLedgerTableAsync(ReportQueryDto query)
		{
			var assets = await QueryAssetsAsync(query);
			foreach (var a in assets) DepreciationService.Calculate(a);

			var table = new ReportTable
			{
				Title = "资产台账",
				Columns = new List<string>
				{
					"资产编码", "资产名称", "分类", "规格型号", "数量", "单位",
					"原值", "累计折旧", "账面净值", "状态", "使用部门", "使用人",
					"存放位置", "采购日期", "折旧方式", "使用年限(月)", "净残值率(%)"
				}
			};

			foreach (var a in assets)
			{
				table.Rows.Add(new List<object?>
				{
					a.AssetCode,
					a.AssetName,
					a.CategoryName ?? "未分类",
					a.Spec ?? "",
					a.Quantity ?? 0,
					a.Unit ?? "",
					a.OriginalValue ?? 0,
					a.AccumulatedDepreciation ?? 0,
					a.NetValue ?? 0,
					AssetStatusMachine.GetStatusName(a.AssetStatus),
					a.DeptName ?? "",
					a.UseUserName ?? "",
					a.Location ?? "",
					a.PurchaseDate?.ToString("yyyy-MM-dd") ?? "",
					GetDepreciationMethodName(a.DepreciationMethod),
					a.UsefulLifeMonths ?? 0,
					a.SalvageRate ?? 0
				});
			}

			return table;
		}

		/// <summary>
		/// 分类汇总
		/// </summary>
		private async Task<ReportTable> BuildCategoryTableAsync(ReportQueryDto query)
		{
			var assets = await QueryAssetsAsync(query);
			foreach (var a in assets) DepreciationService.Calculate(a);

			var table = new ReportTable
			{
				Title = "分类汇总",
				Columns = new List<string>
				{
					"资产分类", "资产条数", "数量合计", "原值合计", "累计折旧合计", "账面净值合计", "原值占比(%)"
				}
			};

			var totalAmount = assets.Sum(a => a.OriginalValue ?? 0);

			foreach (var g in assets
				.GroupBy(a => string.IsNullOrWhiteSpace(a.CategoryName) ? "未分类" : a.CategoryName!)
				.OrderByDescending(g => g.Sum(x => x.OriginalValue ?? 0)))
			{
				var amount = g.Sum(x => x.OriginalValue ?? 0);
				table.Rows.Add(new List<object?>
				{
					g.Key,
					g.Count(),
					g.Sum(x => x.Quantity ?? 0),
					amount,
					g.Sum(x => x.AccumulatedDepreciation ?? 0),
					g.Sum(x => x.NetValue ?? 0),
					totalAmount > 0 ? Math.Round(amount * 100m / totalAmount, 2) : 0m
				});
			}

			return table;
		}

		/// <summary>
		/// 状态汇总
		/// </summary>
		private async Task<ReportTable> BuildStatusTableAsync(ReportQueryDto query)
		{
			var assets = await QueryAssetsAsync(query);
			foreach (var a in assets) DepreciationService.Calculate(a);

			var table = new ReportTable
			{
				Title = "状态汇总",
				Columns = new List<string>
				{
					"资产状态", "资产条数", "数量合计", "原值合计", "账面净值合计", "条数占比(%)"
				}
			};

			foreach (var g in assets
				.GroupBy(a => string.IsNullOrWhiteSpace(a.AssetStatus) ? AssetStatusCodes.InUse : a.AssetStatus!)
				.OrderByDescending(g => g.Count()))
			{
				table.Rows.Add(new List<object?>
				{
					AssetStatusMachine.GetStatusName(g.Key),
					g.Count(),
					g.Sum(x => x.Quantity ?? 0),
					g.Sum(x => x.OriginalValue ?? 0),
					g.Sum(x => x.NetValue ?? 0),
					assets.Count > 0 ? Math.Round(g.Count() * 100m / assets.Count, 2) : 0m
				});
			}

			return table;
		}

		/// <summary>
		/// 部门资产分布
		/// </summary>
		private async Task<ReportTable> BuildDeptTableAsync(ReportQueryDto query)
		{
			var assets = await QueryAssetsAsync(query);
			foreach (var a in assets) DepreciationService.Calculate(a);

			var table = new ReportTable
			{
				Title = "部门资产分布",
				Columns = new List<string>
				{
					"使用部门", "资产条数", "数量合计", "原值合计", "账面净值合计", "原值占比(%)"
				}
			};

			var totalAmount = assets.Sum(a => a.OriginalValue ?? 0);

			foreach (var g in assets
				.GroupBy(a => string.IsNullOrWhiteSpace(a.DeptName) ? "未分配部门" : a.DeptName!)
				.OrderByDescending(g => g.Sum(x => x.OriginalValue ?? 0)))
			{
				var amount = g.Sum(x => x.OriginalValue ?? 0);
				table.Rows.Add(new List<object?>
				{
					g.Key,
					g.Count(),
					g.Sum(x => x.Quantity ?? 0),
					amount,
					g.Sum(x => x.NetValue ?? 0),
					totalAmount > 0 ? Math.Round(amount * 100m / totalAmount, 2) : 0m
				});
			}

			return table;
		}

		/// <summary>
		/// 折旧汇总（分类 + 折旧方式）
		/// </summary>
		private async Task<ReportTable> BuildDepreciationTableAsync(ReportQueryDto query)
		{
			var assets = await QueryAssetsAsync(query);
			foreach (var a in assets) DepreciationService.Calculate(a);

			var table = new ReportTable
			{
				Title = "折旧汇总",
				Columns = new List<string>
				{
					"资产分类", "折旧方式", "资产条数", "原值合计", "月折旧合计", "累计折旧合计", "账面净值合计", "折旧率(%)"
				}
			};

			foreach (var g in assets
				.GroupBy(a => new
				{
					Category = string.IsNullOrWhiteSpace(a.CategoryName) ? "未分类" : a.CategoryName!,
					Method = a.DepreciationMethod ?? DepreciationMethods.StraightLine
				})
				.OrderByDescending(g => g.Sum(x => x.OriginalValue ?? 0)))
			{
				var original = g.Sum(x => x.OriginalValue ?? 0);
				var accumulated = g.Sum(x => x.AccumulatedDepreciation ?? 0);
				table.Rows.Add(new List<object?>
				{
					g.Key.Category,
					GetDepreciationMethodName(g.Key.Method),
					g.Count(),
					original,
					g.Sum(x => x.MonthlyDepreciation ?? 0),
					accumulated,
					g.Sum(x => x.NetValue ?? 0),
					original > 0 ? Math.Round(accumulated * 100m / original, 2) : 0m
				});
			}

			return table;
		}

		/// <summary>
		/// 流转记录
		/// </summary>
		private async Task<ReportTable> BuildFlowTableAsync(ReportQueryDto query)
		{
			var flows = await _db.Queryable<AssetFlow>()
				.WhereIF(!string.IsNullOrWhiteSpace(query.keyword), f =>
					f.FlowNo.Contains(query.keyword) ||
					(f.AssetCode != null && f.AssetCode.Contains(query.keyword)) ||
					(f.AssetName != null && f.AssetName.Contains(query.keyword)))
				.WhereIF(!string.IsNullOrWhiteSpace(query.deptId), f => f.DeptId == query.deptId)
				.WhereIF(query.startDate.HasValue, f => f.CreateTime >= query.startDate)
				.WhereIF(query.endDate.HasValue, f => f.CreateTime <= query.endDate)
				.OrderBy(f => f.CreateTime, OrderByType.Desc)
				.ToListAsync();

			var table = new ReportTable
			{
				Title = "流转记录",
				Columns = new List<string>
				{
					"单据编号", "单据类型", "处理状态", "资产编码", "资产名称",
					"原状态", "新状态", "使用人", "使用部门", "申请人", "处理人",
					"处理时间", "费用", "事由"
				}
			};

			foreach (var f in flows)
			{
				table.Rows.Add(new List<object?>
				{
					f.FlowNo,
					AssetStatusMachine.GetFlowTypeName(f.FlowType),
					AssetStatusMachine.GetFlowStatusName(f.FlowStatus),
					f.AssetCode ?? "",
					f.AssetName ?? "",
					AssetStatusMachine.GetStatusName(f.FromStatus),
					AssetStatusMachine.GetStatusName(f.ToStatus),
					f.UseUserName ?? "",
					f.DeptName ?? "",
					f.ApplyUserName ?? "",
					f.HandleUserName ?? "",
					f.HandleTime?.ToString("yyyy-MM-dd HH:mm") ?? "",
					f.Fee ?? 0,
					f.Remark ?? ""
				});
			}

			return table;
		}

		/// <summary>
		/// 盘点差异
		/// </summary>
		private async Task<ReportTable> BuildInventoryDiffTableAsync(ReportQueryDto query)
		{
			var items = await _db.Queryable<AssetInventoryItem>()
				.Where(i => i.IsDiff == true)
				.WhereIF(!string.IsNullOrWhiteSpace(query.keyword), i =>
					(i.AssetCode != null && i.AssetCode.Contains(query.keyword)) ||
					(i.AssetName != null && i.AssetName.Contains(query.keyword)))
				.OrderBy(i => i.AssetCode)
				.ToListAsync();

			// 关联盘点单号，便于按单追溯
			var inventoryIds = items.Select(i => i.InventoryId).Distinct().ToList();
			var inventories = inventoryIds.Count == 0
				? new List<AssetInventory>()
				: await _db.Queryable<AssetInventory>()
					.Where(i => inventoryIds.Contains(i.Id))
					.ToListAsync();
			var noMap = inventories.ToDictionary(i => i.Id, i => i.InventoryNo);

			var table = new ReportTable
			{
				Title = "盘点差异",
				Columns = new List<string>
				{
					"盘点单号", "盘点名称", "资产编码", "资产名称", "分类", "使用部门",
					"账面状态", "实盘状态", "账面位置", "实盘位置", "账面使用人", "实盘使用人",
					"账面原值", "差异说明", "盘点人", "盘点时间", "是否已处理"
				}
			};

			foreach (var i in items)
			{
				var inventory = inventories.FirstOrDefault(x => x.Id == i.InventoryId);
				table.Rows.Add(new List<object?>
				{
					noMap.TryGetValue(i.InventoryId, out var no) ? no : "",
					inventory?.InventoryName ?? "",
					i.AssetCode ?? "",
					i.AssetName ?? "",
					i.CategoryName ?? "",
					i.DeptName ?? "",
					AssetStatusMachine.GetStatusName(i.BookStatus),
					GetActualStatusName(i.ActualStatus),
					i.BookLocation ?? "",
					i.ActualLocation ?? "",
					i.BookUserName ?? "",
					i.ActualUserName ?? "",
					i.BookValue ?? 0,
					i.DiffDesc ?? "",
					i.CheckedUserName ?? "",
					i.CheckedTime?.ToString("yyyy-MM-dd HH:mm") ?? "",
					i.Handled == true ? "已处理" : "未处理"
				});
			}

			return table;
		}

		/// <summary>
		/// 按查询条件加载资产（用于台账与各类汇总）
		/// </summary>
		private async Task<List<Asset>> QueryAssetsAsync(ReportQueryDto query)
		{
			return await _db.Queryable<Asset>()
				.WhereIF(!string.IsNullOrWhiteSpace(query.keyword), a =>
					a.AssetCode.Contains(query.keyword) ||
					a.AssetName.Contains(query.keyword) ||
					a.Spec.Contains(query.keyword))
				.WhereIF(!string.IsNullOrWhiteSpace(query.categoryId), a => a.CategoryId == query.categoryId)
				.WhereIF(!string.IsNullOrWhiteSpace(query.deptId), a => a.DeptId == query.deptId)
				.WhereIF(!string.IsNullOrWhiteSpace(query.assetStatus), a => a.AssetStatus == query.assetStatus)
				.WhereIF(query.startDate.HasValue, a => a.PurchaseDate >= query.startDate)
				.WhereIF(query.endDate.HasValue, a => a.PurchaseDate <= query.endDate)
				.OrderBy(a => a.AssetCode)
				.ToListAsync();
		}

		/// <summary>
		/// 实盘状态转中文名
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

		/// <summary>
		/// 折旧方式转中文名
		/// </summary>
		private static string GetDepreciationMethodName(string? code) => code switch
		{
			DepreciationMethods.None => "不计提折旧",
			DepreciationMethods.StraightLine => "平均年限法",
			DepreciationMethods.UnitsOfProduction => "工作量法",
			DepreciationMethods.DoubleDeclining => "双倍余额递减法",
			DepreciationMethods.SumOfYears => "年数总和法",
			_ => "未知"
		};
	}
}
