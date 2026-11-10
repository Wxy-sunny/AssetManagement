using System;
using System.Collections.Generic;

namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 报表查询条件
	/// </summary>
	public class ReportQueryDto
	{
		/// <summary>报表类型（见 ReportController 中的报表定义）</summary>
		public string? reportType { get; set; }

		/// <summary>导出格式：xlsx / csv</summary>
		public string? format { get; set; }

		/// <summary>关键字</summary>
		public string? keyword { get; set; }

		/// <summary>资产分类 ID</summary>
		public string? categoryId { get; set; }

		/// <summary>使用部门 ID</summary>
		public string? deptId { get; set; }

		/// <summary>资产状态</summary>
		public string? assetStatus { get; set; }

		/// <summary>起始日期</summary>
		public DateTime? startDate { get; set; }

		/// <summary>截止日期</summary>
		public DateTime? endDate { get; set; }

		/// <summary>页码（预览用）</summary>
		public int pageIndex { get; set; } = 1;

		/// <summary>每页条数（预览用）</summary>
		public int pageSize { get; set; } = 20;
	}

	/// <summary>
	/// 报表类型说明
	/// </summary>
	public class ReportTypeDto
	{
		public string code { get; set; } = "";
		public string name { get; set; } = "";
		public string? description { get; set; }
	}

	/// <summary>
	/// 报表预览数据
	/// </summary>
	public class ReportPreviewDto
	{
		public string reportType { get; set; } = "";
		public string title { get; set; } = "";
		public List<string> columns { get; set; } = new List<string>();
		public List<List<object?>> rows { get; set; } = new List<List<object?>>();
		public int total { get; set; }
	}
}
