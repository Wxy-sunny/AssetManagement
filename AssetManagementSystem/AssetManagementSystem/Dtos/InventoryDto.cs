using System;
using System.Collections.Generic;

namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 盘点单查询条件
	/// </summary>
	public class InventoryQueryDto
	{
		public string? keyword { get; set; }
		public string? inventoryStatus { get; set; }
		public string? deptId { get; set; }
		public DateTime? startDate { get; set; }
		public DateTime? endDate { get; set; }
		public int pageIndex { get; set; } = 1;
		public int pageSize { get; set; } = 10;
	}

	/// <summary>
	/// 创建盘点单
	/// </summary>
	public class InventoryCreateDto
	{
		/// <summary>盘点名称</summary>
		public string? inventoryName { get; set; }

		/// <summary>盘点范围部门 ID（为空表示全部部门）</summary>
		public string? deptId { get; set; }

		/// <summary>盘点范围部门名称</summary>
		public string? deptName { get; set; }

		/// <summary>计划盘点日期</summary>
		public DateTime? planDate { get; set; }

		/// <summary>备注</summary>
		public string? remark { get; set; }
	}

	/// <summary>
	/// 录入实盘结果
	/// </summary>
	public class InventoryItemCheckDto
	{
		/// <summary>盘点明细 ID</summary>
		public string id { get; set; } = "";

		/// <summary>实盘状态（normal/idle/repair/scrapped/missing）</summary>
		public string? actualStatus { get; set; }

		/// <summary>实盘存放位置</summary>
		public string? actualLocation { get; set; }

		/// <summary>实盘使用人</summary>
		public string? actualUserName { get; set; }

		/// <summary>差异说明</summary>
		public string? diffDesc { get; set; }
	}

	/// <summary>
	/// 差异转处理单据
	/// </summary>
	public class InventoryItemHandleDto
	{
		/// <summary>处理方式：scrap（报废）/ repair（报修）</summary>
		public string? flowType { get; set; }
	}

	/// <summary>
	/// 盘点单输出
	/// </summary>
	public class InventoryDto
	{
		public string id { get; set; } = "";
		public string inventoryNo { get; set; } = "";
		public string inventoryName { get; set; } = "";
		public string? deptId { get; set; }
		public string? deptName { get; set; }
		public string inventoryStatus { get; set; } = "";
		public string inventoryStatusName { get; set; } = "";
		public DateTime? planDate { get; set; }
		public DateTime? startTime { get; set; }
		public DateTime? endTime { get; set; }
		public string? ownerId { get; set; }
		public string? ownerName { get; set; }
		public int? totalCount { get; set; }
		public int? diffCount { get; set; }
		public int? checkedCount { get; set; }
		public decimal? totalAmount { get; set; }
		public string? remark { get; set; }
		public DateTime? createTime { get; set; }
		public DateTime? updateTime { get; set; }
	}

	/// <summary>
	/// 盘点明细输出
	/// </summary>
	public class InventoryItemDto
	{
		public string id { get; set; } = "";
		public string inventoryId { get; set; } = "";
		public string assetId { get; set; } = "";
		public string? assetCode { get; set; }
		public string? assetName { get; set; }
		public string? categoryName { get; set; }
		public string? deptName { get; set; }
		public string? bookLocation { get; set; }
		public string? bookUserName { get; set; }
		public string? bookStatus { get; set; }
		public string? bookStatusName { get; set; }
		public string? actualStatus { get; set; }
		public string? actualStatusName { get; set; }
		public string? actualLocation { get; set; }
		public string? actualUserName { get; set; }
		public decimal? bookValue { get; set; }
		public bool? isDiff { get; set; }
		public string? diffDesc { get; set; }
		public string? checkedUserName { get; set; }
		public DateTime? checkedTime { get; set; }
		public bool? handled { get; set; }
		public string? handledFlowId { get; set; }
	}

	/// <summary>
	/// 盘点汇总（差异统计）
	/// </summary>
	public class InventorySummaryDto
	{
		/// <summary>盘点总数</summary>
		public int totalCount { get; set; }

		/// <summary>已盘点数</summary>
		public int checkedCount { get; set; }

		/// <summary>差异条数</summary>
		public int diffCount { get; set; }

		/// <summary>盘亏条数</summary>
		public int missingCount { get; set; }

		/// <summary>涉及资产原值合计</summary>
		public decimal totalAmount { get; set; }

		/// <summary>差异资产原值合计</summary>
		public decimal diffAmount { get; set; }

		/// <summary>盘点准确率（%）</summary>
		public decimal accuracyRate { get; set; }

		/// <summary>按实盘状态分布</summary>
		public List<StatItemDto> statusStats { get; set; } = new List<StatItemDto>();
	}
}
