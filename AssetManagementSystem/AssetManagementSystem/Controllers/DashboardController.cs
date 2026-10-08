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
			var all = await _db.Queryable<Asset>().ToListAsync();

			// 逐条重算折旧，保证净值与累计折旧为最新
			foreach (var item in all)
			{
				DepreciationService.Calculate(item);
			}

			var now = DateTime.Now;
			var monthStart = new DateTime(now.Year, now.Month, 1);

			var dto = new DashboardDto
			{
				assetTotal = all.Count,
				assetQuantity = all.Sum(a => a.Quantity ?? 0),
				originalValue = all.Sum(a => a.OriginalValue ?? 0),
				accumulatedDepreciation = all.Sum(a => a.AccumulatedDepreciation ?? 0),
				netValue = all.Sum(a => a.NetValue ?? 0),
				monthNewCount = all.Count(a => a.CreateTime.HasValue && a.CreateTime.Value >= monthStart)
			};

			// 按状态分布
			dto.statusStats = all
				.GroupBy(a => string.IsNullOrWhiteSpace(a.AssetStatus) ? "unknown" : a.AssetStatus)
				.Select(g => new StatItemDto
				{
					code = g.Key,
					name = StatusName(g.Key),
					value = g.Count(),
					amount = g.Sum(x => x.OriginalValue ?? 0)
				})
				.OrderByDescending(x => x.value)
				.ToList();

			// 按分类分布（取前 8 名）
			dto.categoryStats = all
				.GroupBy(a => string.IsNullOrWhiteSpace(a.CategoryName) ? "未分类" : a.CategoryName)
				.Select(g => new StatItemDto
				{
					name = g.Key,
					value = g.Count(),
					amount = g.Sum(x => x.OriginalValue ?? 0)
				})
				.OrderByDescending(x => x.value)
				.Take(8)
				.ToList();

			// 近 30 天新增趋势（补齐没有数据的日期，保证图表 X 轴连续）
			var today = now.Date;
			var startDate = today.AddDays(-29);
			for (var d = startDate; d <= today; d = d.AddDays(1))
			{
				dto.trend.Add(new TrendItemDto { date = d.ToString("yyyy-MM-dd"), count = 0, amount = 0 });
			}

			var grouped = all
				.Where(a => a.CreateTime.HasValue && a.CreateTime.Value.Date >= startDate && a.CreateTime.Value.Date <= today)
				.GroupBy(a => a.CreateTime.Value.Date)
				.ToDictionary(g => g.Key, g => new
				{
					Count = g.Count(),
					Amount = g.Sum(x => x.OriginalValue ?? 0)
				});

			foreach (var item in dto.trend)
			{
				if (!DateTime.TryParse(item.date, out var d)) continue;
				if (!grouped.TryGetValue(d, out var g)) continue;
				item.count = g.Count;
				item.amount = g.Amount;
			}

			return ApiResult<DashboardDto>.Success(dto, "获取成功");
		}

		/// <summary>
		/// 状态编码转中文名
		/// </summary>
		private static string StatusName(string? code) => code switch
		{
			AssetStatusCodes.InUse => "在用",
			AssetStatusCodes.Idle => "闲置",
			AssetStatusCodes.Repair => "维修中",
			AssetStatusCodes.Scrapped => "已报废",
			_ => "未知"
		};
	}
}
