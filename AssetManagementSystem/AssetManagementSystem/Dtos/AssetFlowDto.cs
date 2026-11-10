using System;
using System.Collections.Generic;

namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 流转单据查询条件
	/// </summary>
	public class AssetFlowQueryDto
	{
		public string? keyword { get; set; }
		public string? flowType { get; set; }
		public string? flowStatus { get; set; }
		public string? assetId { get; set; }
		public string? deptId { get; set; }
		public DateTime? startDate { get; set; }
		public DateTime? endDate { get; set; }
		public int pageIndex { get; set; } = 1;
		public int pageSize { get; set; } = 10;
	}

	/// <summary>
	/// 创建流转单据（领用 / 归还 / 报修 / 维修完成 / 报废）
	/// </summary>
	public class AssetFlowCreateDto
	{
		/// <summary>资产 ID</summary>
		public string assetId { get; set; } = "";

		/// <summary>单据类型：borrow / return / repair / repair_done / scrap</summary>
		public string flowType { get; set; } = "";

		/// <summary>使用人 ID（领用单必填）</summary>
		public string? useUserId { get; set; }

		/// <summary>使用人姓名</summary>
		public string? useUserName { get; set; }

		/// <summary>使用部门 ID</summary>
		public string? deptId { get; set; }

		/// <summary>使用部门名称</summary>
		public string? deptName { get; set; }

		/// <summary>流转后的目标状态（维修完成时必填：in_use / idle）</summary>
		public string? toStatus { get; set; }

		/// <summary>费用（维修费 / 处置残值）</summary>
		public decimal? fee { get; set; }

		/// <summary>预计归还日期（领用单）</summary>
		public DateTime? expectReturnDate { get; set; }

		/// <summary>事由 / 原因</summary>
		public string? remark { get; set; }
	}

	/// <summary>
	/// 完成流转单据（维修完成等需要二次确认的场景）
	/// </summary>
	public class AssetFlowCompleteDto
	{
		/// <summary>流转后的目标状态（维修完成时必填）</summary>
		public string? toStatus { get; set; }

		/// <summary>费用</summary>
		public decimal? fee { get; set; }

		/// <summary>处理结果说明</summary>
		public string? handleResult { get; set; }
	}

	/// <summary>
	/// 流转单据输出
	/// </summary>
	public class AssetFlowDto
	{
		public string id { get; set; } = "";
		public string flowNo { get; set; } = "";
		public string flowType { get; set; } = "";
		public string flowTypeName { get; set; } = "";
		public string flowStatus { get; set; } = "";
		public string flowStatusName { get; set; } = "";
		public string assetId { get; set; } = "";
		public string? assetCode { get; set; }
		public string? assetName { get; set; }
		public string? fromStatus { get; set; }
		public string? fromStatusName { get; set; }
		public string? toStatus { get; set; }
		public string? toStatusName { get; set; }
		public string? deptId { get; set; }
		public string? deptName { get; set; }
		public string? useUserId { get; set; }
		public string? useUserName { get; set; }
		public string? applyUserId { get; set; }
		public string? applyUserName { get; set; }
		public string? handleUserId { get; set; }
		public string? handleUserName { get; set; }
		public DateTime? handleTime { get; set; }
		public decimal? fee { get; set; }
		public DateTime? expectReturnDate { get; set; }
		public string? handleResult { get; set; }
		public string? remark { get; set; }
		public DateTime? createTime { get; set; }
		public DateTime? updateTime { get; set; }
	}

	/// <summary>
	/// 资产当前可执行的流转动作（供前端操作列下拉渲染）
	/// </summary>
	public class AssetFlowActionDto
	{
		/// <summary>单据类型</summary>
		public string flowType { get; set; } = "";

		/// <summary>动作名称</summary>
		public string label { get; set; } = "";

		/// <summary>流转后的目标状态</summary>
		public string? toStatus { get; set; } = "";

		/// <summary>动作说明</summary>
		public string? description { get; set; }
	}
}
