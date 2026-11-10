using System;
using System.Collections.Generic;

namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 通用统计项（名称 + 数值）
	/// </summary>
	public class StatItemDto
	{
		public string? name { get; set; }
		public string? code { get; set; }
		public int value { get; set; }
		public decimal? amount { get; set; }
	}

	/// <summary>
	/// 趋势项（按天）
	/// </summary>
	public class TrendItemDto
	{
		/// <summary>日期 yyyy-MM-dd</summary>
		public string? date { get; set; }

		/// <summary>当日新增资产数</summary>
		public int count { get; set; }

		/// <summary>当日新增资产金额</summary>
		public decimal? amount { get; set; }
	}

	/// <summary>
	/// 首页统计聚合
	/// </summary>
	public class DashboardDto
	{
		/// <summary>资产总数（台账条数）</summary>
		public int assetTotal { get; set; }

		/// <summary>资产总数量（数量求和）</summary>
		public decimal? assetQuantity { get; set; }

		/// <summary>资产原值总额</summary>
		public decimal? originalValue { get; set; }

		/// <summary>累计折旧总额</summary>
		public decimal? accumulatedDepreciation { get; set; }

		/// <summary>账面净值总额</summary>
		public decimal? netValue { get; set; }

		/// <summary>本月新增资产数</summary>
		public int monthNewCount { get; set; }

		/// <summary>按状态分布</summary>
		public List<StatItemDto> statusStats { get; set; } = new List<StatItemDto>();

		/// <summary>按分类分布</summary>
		public List<StatItemDto> categoryStats { get; set; } = new List<StatItemDto>();

		/// <summary>近 30 天新增趋势</summary>
		public List<TrendItemDto> trend { get; set; } = new List<TrendItemDto>();
	}

	/// <summary>
	/// 汇总行（用于 SQL 聚合查询的接收对象）
	/// </summary>
	public class AggSumDto
	{
		public int? cnt { get; set; }
		public decimal? total_quantity { get; set; }
		public decimal? total_original { get; set; }
		public decimal? total_accumulated { get; set; }
		public decimal? total_net { get; set; }
	}

	/// <summary>
	/// 分组聚合行（用于 SQL GROUP BY 查询的接收对象）
	/// </summary>
	public class AggGroupDto
	{
		public string? code { get; set; }
		public string? name { get; set; }
		public int? value { get; set; }
		public decimal? amount { get; set; }
	}

	/// <summary>
	/// 趋势原始行（按创建时间分组时使用）
	/// </summary>
	public class AggTrendDto
	{
		public DateTime? create_time { get; set; }
		public decimal? original_value { get; set; }
	}

	/// <summary>
	/// 数据仪表盘聚合（多维度指标 + 多图表数据）
	/// </summary>
	public class DashboardOverviewDto
	{
		// ---------- KPI ----------
		/// <summary>资产总数（台账条数）</summary>
		public int assetTotal { get; set; }

		/// <summary>资产总数量</summary>
		public decimal assetQuantity { get; set; }

		/// <summary>资产原值总额</summary>
		public decimal originalValue { get; set; }

		/// <summary>累计折旧总额</summary>
		public decimal accumulatedDepreciation { get; set; }

		/// <summary>账面净值总额</summary>
		public decimal netValue { get; set; }

		/// <summary>本月新增</summary>
		public int monthNewCount { get; set; }

		/// <summary>在用数量</summary>
		public int inUseCount { get; set; }

		/// <summary>闲置数量</summary>
		public int idleCount { get; set; }

		/// <summary>维修中数量</summary>
		public int repairCount { get; set; }

		/// <summary>已报废数量</summary>
		public int scrappedCount { get; set; }

		/// <summary>在用率（%）</summary>
		public decimal utilizationRate { get; set; }

		/// <summary>闲置率（%）</summary>
		public decimal idleRate { get; set; }

		/// <summary>维修率（%）</summary>
		public decimal repairRate { get; set; }

		/// <summary>报废率（%）</summary>
		public decimal scrapRate { get; set; }

		/// <summary>折旧率（%）</summary>
		public decimal depreciationRate { get; set; }

		// ---------- 图表数据 ----------
		/// <summary>状态分布</summary>
		public List<StatItemDto> statusStats { get; set; } = new List<StatItemDto>();

		/// <summary>分类分布（Top 10）</summary>
		public List<StatItemDto> categoryStats { get; set; } = new List<StatItemDto>();

		/// <summary>部门资产分布（Top 10）</summary>
		public List<StatItemDto> deptStats { get; set; } = new List<StatItemDto>();

		/// <summary>折旧方式分布</summary>
		public List<StatItemDto> depreciationMethodStats { get; set; } = new List<StatItemDto>();

		/// <summary>近 12 个月新增趋势</summary>
		public List<TrendItemDto> monthlyTrend { get; set; } = new List<TrendItemDto>();

		/// <summary>流转单据统计（按类型）</summary>
		public List<StatItemDto> flowTypeStats { get; set; } = new List<StatItemDto>();

		/// <summary>待处理流转单数（报修未完成）</summary>
		public int flowPendingCount { get; set; }

		/// <summary>盘点单统计</summary>
		public List<StatItemDto> inventoryStats { get; set; } = new List<StatItemDto>();

		/// <summary>最近一次盘点准确率（%）</summary>
		public decimal inventoryAccuracyRate { get; set; }
	}
}
