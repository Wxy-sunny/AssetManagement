using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	/// <summary>
	/// 联系请求表（用户→管理员）
	/// 场景：用户在登录页点击"联系管理员"（通常是重置密码申请），填写账号/手机号/说明后提交，
	/// 记录到此表，管理员在"联系请求"管理页统一查看并处理。
	/// </summary>
	[SugarTable("T_CONTACT_REQUEST")]
	[DbBelong(DbNames.Asset)]
	public class ContactRequest : BaseModel
	{
		/// <summary>关联账号或姓名（用户填写，可能为空）</summary>
		[SugarColumn(ColumnName = "account", IsNullable = true, Length = 50)]
		public string? Account { get; set; }

		/// <summary>手机号</summary>
		[SugarColumn(ColumnName = "mobile", IsNullable = true, Length = 20)]
		public string? Mobile { get; set; }

		/// <summary>请求类型：reset_password 重置密码 / other 其他</summary>
		[SugarColumn(ColumnName = "request_type", IsNullable = true, Length = 30)]
		public string? RequestType { get; set; } = RequestTypes.ResetPassword;

		/// <summary>说明 / 详情</summary>
		[SugarColumn(ColumnName = "content", IsNullable = true, Length = -1)]
		public string? Content { get; set; }

		/// <summary>处理状态：pending 待处理 / handled 已处理</summary>
		[SugarColumn(ColumnName = "status", IsNullable = true, Length = 20)]
		public string? Status { get; set; } = RequestStatus.Pending;

		/// <summary>提交用户Id（已登录用户可带回，否则为 null）</summary>
		[SugarColumn(ColumnName = "sender_id", IsNullable = true, Length = 50)]
		public string? SenderId { get; set; }

		/// <summary>处理人姓名</summary>
		[SugarColumn(ColumnName = "handler_name", IsNullable = true, Length = 50)]
		public string? HandlerName { get; set; }

		/// <summary>处理时间</summary>
		[SugarColumn(ColumnName = "handle_time", IsNullable = true)]
		public DateTime? HandleTime { get; set; }

		/// <summary>处理备注</summary>
		[SugarColumn(ColumnName = "handle_remark", IsNullable = true, Length = -1)]
		public string? HandleRemark { get; set; }
	}

	/// <summary>请求类型常量</summary>
	public static class RequestTypes
	{
		public const string ResetPassword = "reset_password";
		public const string Other = "other";
	}

	/// <summary>请求处理状态常量</summary>
	public static class RequestStatus
	{
		public const string Pending = "pending";
		public const string Handled = "handled";
	}
}
