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
}
