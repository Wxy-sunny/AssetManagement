using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	/// <summary>
	/// 资产流转单据类型常量
	/// 说明：领用 / 归还 / 报修 / 维修完成 / 报废 构成资产全生命周期闭环，
	/// 每一次状态跃迁都必须落一条流转单据（既是业务单据，也是审计流水）。
	/// </summary>
	public static class AssetFlowTypes
	{
		/// <summary>领用：闲置 → 在用</summary>
		public const string Borrow = "borrow";

		/// <summary>归还：在用 → 闲置</summary>
		public const string Return = "return";

		/// <summary>报修：在用/闲置 → 维修中</summary>
		public const string Repair = "repair";

		/// <summary>维修完成：维修中 → 在用/闲置</summary>
		public const string RepairDone = "repair_done";

		/// <summary>报废：在用/闲置/维修中 → 已报废（终态）</summary>
		public const string Scrap = "scrap";
	}

	/// <summary>
	/// 流转单据处理状态常量
	/// </summary>
	public static class AssetFlowStatusCodes
	{
		/// <summary>待处理（已提交，等待维修完成等后续动作）</summary>
		public const string Pending = "pending";

		/// <summary>已完成（资产状态已同步跃迁）</summary>
		public const string Completed = "completed";

		/// <summary>已驳回（资产状态保持不变）</summary>
		public const string Rejected = "rejected";

		/// <summary>已取消（资产状态保持不变）</summary>
		public const string Cancelled = "cancelled";
	}

	/// <summary>
	/// 资产流转单据（领用 / 归还 / 维修 / 报废）
	/// 说明：单据一旦创建并完成，资产主表状态同步跃迁，二者在同一事务内提交，
	/// 保证"资产状态"与"业务单据"始终一致，杜绝只改状态不留痕的情况。
	/// </summary>
	[SugarTable("T_ASSET_FLOW")]
	[DbBelong(DbNames.Asset)]
	public class AssetFlow : BaseModel
	{
		/// <summary>单据编号（LF + yyyyMMdd + 6 位序列）</summary>
		[SugarColumn(ColumnName = "flow_no", IsNullable = false, Length = 50)]
		public string FlowNo { get; set; } = "";

		/// <summary>单据类型（borrow/return/repair/repair_done/scrap）</summary>
		[SugarColumn(ColumnName = "flow_type", IsNullable = false, Length = 20)]
		public string FlowType { get; set; } = "";

		/// <summary>资产主键</summary>
		[SugarColumn(ColumnName = "asset_id", IsNullable = false, Length = 50)]
		public string AssetId { get; set; } = "";

		/// <summary>资产编码（冗余，便于列表检索）</summary>
		[SugarColumn(ColumnName = "asset_code", IsNullable = true, Length = 50)]
		public string? AssetCode { get; set; }

		/// <summary>资产名称（冗余）</summary>
		[SugarColumn(ColumnName = "asset_name", IsNullable = true, Length = 255)]
		public string? AssetName { get; set; }

		/// <summary>流转前状态</summary>
		[SugarColumn(ColumnName = "from_status", IsNullable = true, Length = 20)]
		public string? FromStatus { get; set; }

		/// <summary>流转后状态</summary>
		[SugarColumn(ColumnName = "to_status", IsNullable = true, Length = 20)]
		public string? ToStatus { get; set; }

		/// <summary>单据状态（pending/completed/rejected/cancelled）</summary>
		[SugarColumn(ColumnName = "flow_status", IsNullable = false, Length = 20)]
		public string FlowStatus { get; set; } = AssetFlowStatusCodes.Pending;

		/// <summary>使用部门 ID</summary>
		[SugarColumn(ColumnName = "dept_id", IsNullable = true, Length = 50)]
		public string? DeptId { get; set; }

		/// <summary>使用部门名称</summary>
		[SugarColumn(ColumnName = "dept_name", IsNullable = true, Length = 255)]
		public string? DeptName { get; set; }

		/// <summary>使用人 ID</summary>
		[SugarColumn(ColumnName = "use_user_id", IsNullable = true, Length = 50)]
		public string? UseUserId { get; set; }

		/// <summary>使用人姓名</summary>
		[SugarColumn(ColumnName = "use_user_name", IsNullable = true, Length = 255)]
		public string? UseUserName { get; set; }

		/// <summary>申请人 ID</summary>
		[SugarColumn(ColumnName = "apply_user_id", IsNullable = true, Length = 50)]
		public string? ApplyUserId { get; set; }

		/// <summary>申请人姓名</summary>
		[SugarColumn(ColumnName = "apply_user_name", IsNullable = true, Length = 255)]
		public string? ApplyUserName { get; set; }

		/// <summary>处理人 ID</summary>
		[SugarColumn(ColumnName = "handle_user_id", IsNullable = true, Length = 50)]
		public string? HandleUserId { get; set; }

		/// <summary>处理人姓名</summary>
		[SugarColumn(ColumnName = "handle_user_name", IsNullable = true, Length = 255)]
		public string? HandleUserName { get; set; }

		/// <summary>处理时间</summary>
		[SugarColumn(ColumnName = "handle_time", IsNullable = true)]
		public DateTime? HandleTime { get; set; }

		/// <summary>费用（维修费 / 处置残值）</summary>
		[SugarColumn(ColumnName = "fee", IsNullable = true, DecimalDigits = 2)]
		public decimal? Fee { get; set; }

		/// <summary>预计归还日期（领用单）</summary>
		[SugarColumn(ColumnName = "expect_return_date", IsNullable = true)]
		public DateTime? ExpectReturnDate { get; set; }

		/// <summary>处理结果说明</summary>
		[SugarColumn(ColumnName = "handle_result", IsNullable = true, Length = 255)]
		public string? HandleResult { get; set; }
	}
}
