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
	/// 仪表盘统计
	/// 说明：全部统计走 SQL 聚合（COUNT / SUM / GROUP BY），不再整表加载后在内存里分组，
	/// 资产量级上来后首页依旧能秒开。
	/// </summary>
	[ApiController]
	[Route("dashboard")]
	[Authorize]
	public class DashboardController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;

		public DashboardController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 首页统计：总量指标 + 状态分布 + 分类分布 + 近 30 天趋势
		/// </summary>
		[HttpGet("stats")]
		[Permission(Permissions.DashboardView)]
		public async Task<ApiResult<DashboardDto>> GetStats()
		{
			var now = DateTime.Now;
			var monthStart = new DateTime(now.Year, now.Month, 1);
			var today = now.Date;
			var startDate = today.AddDays(-29);

			var dto = new DashboardDto
			{
				statusStats = await QueryStatusStatsAsync(),
				categoryStats = await QueryGroupStatsAsync(
					"SELECT COALESCE(category_name, '未分类') AS name, COUNT(*) AS value, SUM(COALESCE(original_value, 0)) AS amount FROM T_ASSET GROUP BY category_name",
					8)
			};

			var sum = await QuerySumAsync();
			dto.assetTotal = sum.cnt ?? 0;
			dto.assetQuantity = sum.total_quantity ?? 0;
			dto.originalValue = sum.total_original ?? 0;
			dto.accumulatedDepreciation = sum.total_accumulated ?? 0;
			dto.netValue = sum.total_net ?? 0;

			dto.monthNewCount = await _db.Queryable<Asset>()
				.Where(a => a.CreateTime >= monthStart)
				.CountAsync();

			// 近 30 天新增趋势（补齐没有数据的日期，保证图表 X 轴连续）
			var trendRows = await _db.Ado.SqlQueryAsync<AggTrendDto>(
				"SELECT create_time, original_value FROM T_ASSET WHERE create_time >= @startDate",
				new SugarParameter("@startDate", startDate));

			var grouped = trendRows
				.Where(r => r.create_time.HasValue)
				.GroupBy(r => r.create_time!.Value.Date)
				.ToDictionary(
					g => g.Key,
					g => new { Count = g.Count(), Amount = g.Sum(x => x.original_value ?? 0) });

			for (var d = startDate; d <= today; d = d.AddDays(1))
			{
				var item = new TrendItemDto { date = d.ToString("yyyy-MM-dd"), count = 0, amount = 0 };
				if (grouped.TryGetValue(d, out var g))
				{
					item.count = g.Count;
					item.amount = g.Amount;
				}
				dto.trend.Add(item);
			}

			return ApiResult<DashboardDto>.Success(dto, "获取成功");
		}

		/// <summary>
		/// 数据仪表盘：KPI + 多维度图表数据
		/// </summary>
		[HttpGet("overview")]
		[Permission(Permissions.DashboardOverview)]
		public async Task<ApiResult<DashboardOverviewDto>> GetOverview()
		{
			var now = DateTime.Now;
			var monthStart = new DateTime(now.Year, now.Month, 1);

			var dto = new DashboardOverviewDto();

			var sum = await QuerySumAsync();
			dto.assetTotal = sum.cnt ?? 0;
			dto.assetQuantity = sum.total_quantity ?? 0;
			dto.originalValue = sum.total_original ?? 0;
			dto.accumulatedDepreciation = sum.total_accumulated ?? 0;
			dto.netValue = sum.total_net ?? 0;

			dto.monthNewCount = await _db.Queryable<Asset>()
				.Where(a => a.CreateTime >= monthStart)
				.CountAsync();

			// 状态分布（SQL 聚合）
			dto.statusStats = await QueryStatusStatsAsync();

			foreach (var s in dto.statusStats)
			{
				switch (s.code)
				{
					case AssetStatusCodes.InUse: dto.inUseCount = s.value; break;
					case AssetStatusCodes.Idle: dto.idleCount = s.value; break;
					case AssetStatusCodes.Repair: dto.repairCount = s.value; break;
					case AssetStatusCodes.Scrapped: dto.scrappedCount = s.value; break;
				}
			}

			var total = dto.assetTotal > 0 ? dto.assetTotal : 0;
			if (total > 0)
			{
				dto.utilizationRate = Math.Round(dto.inUseCount * 100m / total, 2);
				dto.idleRate = Math.Round(dto.idleCount * 100m / total, 2);
				dto.repairRate = Math.Round(dto.repairCount * 100m / total, 2);
				dto.scrapRate = Math.Round(dto.scrappedCount * 100m / total, 2);
			}

			dto.depreciationRate = dto.originalValue > 0
				? Math.Round(dto.accumulatedDepreciation * 100m / dto.originalValue, 2)
				: 0m;

			// 分类 / 部门 / 折旧方式分布
			dto.categoryStats = await QueryGroupStatsAsync(
				"SELECT COALESCE(category_name, '未分类') AS name, COUNT(*) AS value, SUM(COALESCE(original_value, 0)) AS amount FROM T_ASSET GROUP BY category_name",
				10);

			dto.deptStats = await QueryGroupStatsAsync(
				"SELECT COALESCE(dept_name, '未分配部门') AS name, COUNT(*) AS value, SUM(COALESCE(original_value, 0)) AS amount FROM T_ASSET GROUP BY dept_name",
				10);

			dto.depreciationMethodStats = await QueryGroupStatsAsync(
				"SELECT COALESCE(depreciation_method, 'straight-line') AS code, COUNT(*) AS value, SUM(COALESCE(net_value, 0)) AS amount FROM T_ASSET GROUP BY depreciation_method",
				10);

			// 近 12 个月新增趋势
			dto.monthlyTrend = await QueryMonthlyTrendAsync(now);

			// 流转单据统计
			dto.flowTypeStats = await QueryGroupStatsAsync(
				"SELECT flow_type AS code, COUNT(*) AS value, SUM(COALESCE(fee, 0)) AS amount FROM T_ASSET_FLOW GROUP BY flow_type",
				10);

			dto.flowPendingCount = await _db.Queryable<AssetFlow>()
				.Where(f => f.FlowStatus == AssetFlowStatusCodes.Pending)
				.CountAsync();

			// 盘点统计
			dto.inventoryStats = await QueryGroupStatsAsync(
				"SELECT inventory_status AS code, COUNT(*) AS value, SUM(COALESCE(diff_count, 0)) AS amount FROM T_ASSET_INVENTORY GROUP BY inventory_status",
				10);

			var latestInventory = await _db.Queryable<AssetInventory>()
				.Where(i => i.InventoryStatus == AssetInventoryStatusCodes.Completed)
				.OrderBy(i => i.EndTime, OrderByType.Desc)
				.FirstAsync();

			if (latestInventory != null && latestInventory.TotalCount > 0)
			{
				var totalCount = latestInventory.TotalCount.Value;
				var diffCount = latestInventory.DiffCount ?? 0;
				dto.inventoryAccuracyRate = Math.Round((totalCount - diffCount) * 100m / totalCount, 2);
			}

			return ApiResult<DashboardOverviewDto>.Success(dto, "获取成功");
		}

		/// <summary>
		/// 资产总量聚合（COUNT / SUM，单行返回）
		/// </summary>
		private async Task<AggSumDto> QuerySumAsync()
		{
			const string sql = @"SELECT COUNT(*) AS cnt,
	SUM(COALESCE(quantity, 0)) AS total_quantity,
	SUM(COALESCE(original_value, 0)) AS total_original,
	SUM(COALESCE(accumulated_depreciation, 0)) AS total_accumulated,
	SUM(COALESCE(net_value, 0)) AS total_net
FROM T_ASSET";

			var rows = await _db.Ado.SqlQueryAsync<AggSumDto>(sql);
			return rows.FirstOrDefault() ?? new AggSumDto();
		}

		/// <summary>
		/// 资产状态分布（SQL GROUP BY）
		/// </summary>
		private async Task<List<StatItemDto>> QueryStatusStatsAsync()
		{
			const string sql = @"SELECT COALESCE(asset_status, 'unknown') AS code,
	COUNT(*) AS value,
	SUM(COALESCE(original_value, 0)) AS amount
FROM T_ASSET
GROUP BY asset_status";

			var rows = await _db.Ado.SqlQueryAsync<AggGroupDto>(sql);

			return rows
				.Select(r => new StatItemDto
				{
					code = r.code,
					name = AssetStatusMachine.GetStatusName(r.code),
					value = r.value ?? 0,
					amount = r.amount ?? 0
				})
				.OrderByDescending(x => x.value)
				.ToList();
		}

		/// <summary>
		/// 通用分组聚合（SQL GROUP BY，可限制返回条数）
		/// </summary>
		private async Task<List<StatItemDto>> QueryGroupStatsAsync(string sql, int take)
		{
			var rows = await _db.Ado.SqlQueryAsync<AggGroupDto>(sql);

			return rows
				.Select(r => new StatItemDto
				{
					code = r.code,
					name = r.name ?? r.code,
					value = r.value ?? 0,
					amount = r.amount ?? 0
				})
				.OrderByDescending(x => x.amount ?? 0)
				.Take(take)
				.ToList();
		}

		/// <summary>
		/// 近 12 个月新增趋势（按月分组，缺失月份补 0）
		/// </summary>
		private async Task<List<TrendItemDto>> QueryMonthlyTrendAsync(DateTime now)
		{
			var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

			var rows = await _db.Ado.SqlQueryAsync<AggTrendDto>(
				"SELECT create_time, original_value FROM T_ASSET WHERE create_time >= @startDate",
				new SugarParameter("@startDate", monthStart));

			var grouped = rows
				.Where(r => r.create_time.HasValue)
				.GroupBy(r => new DateTime(r.create_time!.Value.Year, r.create_time.Value.Month, 1))
				.ToDictionary(
					g => g.Key,
					g => new { Count = g.Count(), Amount = g.Sum(x => x.original_value ?? 0) });

			var trend = new List<TrendItemDto>();
			for (var m = monthStart; m <= new DateTime(now.Year, now.Month, 1); m = m.AddMonths(1))
			{
				var item = new TrendItemDto { date = m.ToString("yyyy-MM"), count = 0, amount = 0 };
				if (grouped.TryGetValue(m, out var g))
				{
					item.count = g.Count;
					item.amount = g.Amount;
				}
				trend.Add(item);
			}

			return trend;
		}
	}
}
