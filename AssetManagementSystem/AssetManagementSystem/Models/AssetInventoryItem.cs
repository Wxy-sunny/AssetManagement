using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	/// <summary>
	/// 实盘状态常量（与资产状态编码不同：normal 表示"在册且状态一致"）
	/// </summary>
	public static class InventoryActualStatusCodes
	{
		/// <summary>正常（实物在册，状态与账面对应）</summary>
		public const string Normal = "normal";

		/// <summary>闲置（实物在册但处于闲置状态）</summary>
		public const string Idle = "idle";

		/// <summary>维修中</summary>
		public const string Repair = "repair";

		/// <summary>已报废（实物在册但已按报废处理）</summary>
		public const string Scrapped = "scrapped";

		/// <summary>盘亏（实物找不到）</summary>
		public const string Missing = "missing";
	}

	/// <summary>
	/// 盘点明细（账面快照 + 实盘结果）
	/// </summary>
	[SugarTable("T_ASSET_INVENTORY_ITEM")]
	[DbBelong(DbNames.Asset)]
	public class AssetInventoryItem : BaseModel
	{
		/// <summary>盘点单 ID</summary>
		[SugarColumn(ColumnName = "inventory_id", IsNullable = false, Length = 50)]
		public string InventoryId { get; set; } = "";

		/// <summary>资产 ID</summary>
		[SugarColumn(ColumnName = "asset_id", IsNullable = false, Length = 50)]
		public string AssetId { get; set; } = "";

		/// <summary>资产编码</summary>
		[SugarColumn(ColumnName = "asset_code", IsNullable = true, Length = 50)]
		public string? AssetCode { get; set; }

		/// <summary>资产名称</summary>
		[SugarColumn(ColumnName = "asset_name", IsNullable = true, Length = 255)]
		public string? AssetName { get; set; }

		/// <summary>资产分类</summary>
		[SugarColumn(ColumnName = "category_name", IsNullable = true, Length = 255)]
		public string? CategoryName { get; set; }

		/// <summary>使用部门</summary>
		[SugarColumn(ColumnName = "dept_name", IsNullable = true, Length = 255)]
		public string? DeptName { get; set; }

		/// <summary>账面存放位置</summary>
		[SugarColumn(ColumnName = "book_location", IsNullable = true, Length = 255)]
		public string? BookLocation { get; set; }

		/// <summary>账面使用人</summary>
		[SugarColumn(ColumnName = "book_user_name", IsNullable = true, Length = 255)]
		public string? BookUserName { get; set; }

		/// <summary>账面状态</summary>
		[SugarColumn(ColumnName = "book_status", IsNullable = true, Length = 20)]
		public string? BookStatus { get; set; }

		/// <summary>实盘状态（normal/idle/repair/scrapped/missing）</summary>
		[SugarColumn(ColumnName = "actual_status", IsNullable = true, Length = 20)]
		public string? ActualStatus { get; set; }

		/// <summary>实盘存放位置</summary>
		[SugarColumn(ColumnName = "actual_location", IsNullable = true, Length = 255)]
		public string? ActualLocation { get; set; }

		/// <summary>实盘使用人</summary>
		[SugarColumn(ColumnName = "actual_user_name", IsNullable = true, Length = 255)]
		public string? ActualUserName { get; set; }

		/// <summary>账面原值</summary>
		[SugarColumn(ColumnName = "book_value", IsNullable = true, DecimalDigits = 2)]
		public decimal? BookValue { get; set; }

		/// <summary>是否账实不符</summary>
		[SugarColumn(ColumnName = "is_diff", IsNullable = true)]
		public bool? IsDiff { get; set; }

		/// <summary>差异说明</summary>
		[SugarColumn(ColumnName = "diff_desc", IsNullable = true, Length = 255)]
		public string? DiffDesc { get; set; }

		/// <summary>盘点人 ID</summary>
		[SugarColumn(ColumnName = "checked_user_id", IsNullable = true, Length = 50)]
		public string? CheckedUserId { get; set; }

		/// <summary>盘点人姓名</summary>
		[SugarColumn(ColumnName = "checked_user_name", IsNullable = true, Length = 255)]
		public string? CheckedUserName { get; set; }

		/// <summary>盘点时间</summary>
		[SugarColumn(ColumnName = "checked_time", IsNullable = true)]
		public DateTime? CheckedTime { get; set; }

		/// <summary>差异是否已生成处理单据</summary>
		[SugarColumn(ColumnName = "handled", IsNullable = true)]
		public bool? Handled { get; set; }

		/// <summary>处理单据 ID</summary>
		[SugarColumn(ColumnName = "handled_flow_id", IsNullable = true, Length = 50)]
		public string? HandledFlowId { get; set; }
	}
}
