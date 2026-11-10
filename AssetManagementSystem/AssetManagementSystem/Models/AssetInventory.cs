using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	/// <summary>
	/// 盘点单状态常量
	/// </summary>
	public static class AssetInventoryStatusCodes
	{
		/// <summary>盘点中</summary>
		public const string Draft = "draft";

		/// <summary>已完成（账实差异已汇总）</summary>
		public const string Completed = "completed";
	}

	/// <summary>
	/// 资产盘点单
	/// 说明：盘点单创建时对范围内的资产做"账面快照"，之后逐条录入实盘结果，
	/// 完成时汇总账实差异并输出差异清单（可一键转成报废/报修单据形成闭环）。
	/// </summary>
	[SugarTable("T_ASSET_INVENTORY")]
	[DbBelong(DbNames.Asset)]
	public class AssetInventory : BaseModel
	{
		/// <summary>盘点单号（PD + yyyyMMdd + 4 位序列）</summary>
		[SugarColumn(ColumnName = "inventory_no", IsNullable = false, Length = 50)]
		public string InventoryNo { get; set; } = "";

		/// <summary>盘点名称</summary>
		[SugarColumn(ColumnName = "inventory_name", IsNullable = false, Length = 255)]
		public string InventoryName { get; set; } = "";

		/// <summary>盘点范围部门 ID（为空表示全部部门）</summary>
		[SugarColumn(ColumnName = "dept_id", IsNullable = true, Length = 50)]
		public string? DeptId { get; set; }

		/// <summary>盘点范围部门名称</summary>
		[SugarColumn(ColumnName = "dept_name", IsNullable = true, Length = 255)]
		public string? DeptName { get; set; }

		/// <summary>盘点单状态（draft / completed）</summary>
		[SugarColumn(ColumnName = "inventory_status", IsNullable = false, Length = 20)]
		public string InventoryStatus { get; set; } = AssetInventoryStatusCodes.Draft;

		/// <summary>计划盘点日期</summary>
		[SugarColumn(ColumnName = "plan_date", IsNullable = true)]
		public DateTime? PlanDate { get; set; }

		/// <summary>开始时间</summary>
		[SugarColumn(ColumnName = "start_time", IsNullable = true)]
		public DateTime? StartTime { get; set; }

		/// <summary>完成时间</summary>
		[SugarColumn(ColumnName = "end_time", IsNullable = true)]
		public DateTime? EndTime { get; set; }

		/// <summary>盘点负责人 ID</summary>
		[SugarColumn(ColumnName = "owner_id", IsNullable = true, Length = 50)]
		public string? OwnerId { get; set; }

		/// <summary>盘点负责人姓名</summary>
		[SugarColumn(ColumnName = "owner_name", IsNullable = true, Length = 255)]
		public string? OwnerName { get; set; }

		/// <summary>盘点资产总数</summary>
		[SugarColumn(ColumnName = "total_count", IsNullable = true)]
		public int? TotalCount { get; set; }

		/// <summary>账实差异条数</summary>
		[SugarColumn(ColumnName = "diff_count", IsNullable = true)]
		public int? DiffCount { get; set; }

		/// <summary>已录入实盘条数</summary>
		[SugarColumn(ColumnName = "checked_count", IsNullable = true)]
		public int? CheckedCount { get; set; }

		/// <summary>涉及资产原值合计</summary>
		[SugarColumn(ColumnName = "total_amount", IsNullable = true, DecimalDigits = 2)]
		public decimal? TotalAmount { get; set; }
	}
}
