using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	/// <summary>
	/// 个人消息类型常量
	/// </summary>
	public static class UserMessageTypes
	{
		/// <summary>联系请求回执（管理员处理"联系管理员/重置密码"申请后回发给用户）</summary>
		public const string RequestReply = "request_reply";
		/// <summary>系统消息</summary>
		public const string System = "system";
	}

	/// <summary>
	/// 个人消息表（用户收件箱）
	/// 与 T_NOTIFICATION（管理员→全员公告）不同，本表用于"点对点"消息，
	/// 典型场景：管理员处理联系请求后，给提交人回发一条处理结果通知。
	/// </summary>
	[SugarTable("T_USER_MESSAGE")]
	[DbBelong(DbNames.Asset)]
	public class UserMessage : BaseModel
	{
		/// <summary>
		/// 收件人（SysUser.Id，与 JWT Sub 一致）
		/// </summary>
		[SugarColumn(ColumnName = "recipient_id", IsNullable = false, Length = 50)]
		public string RecipientId { get; set; }

		/// <summary>
		/// 消息标题
		/// </summary>
		[SugarColumn(ColumnName = "title", IsNullable = false, Length = 255)]
		public string Title { get; set; }

		/// <summary>
		/// 消息内容
		/// </summary>
		[SugarColumn(ColumnName = "content", IsNullable = false, ColumnDataType = "nvarchar(max)")]
		public string Content { get; set; }

		/// <summary>
		/// 消息类型：request_reply 请求回执 / system 系统消息
		/// </summary>
		[SugarColumn(ColumnName = "msg_type", IsNullable = true, Length = 20)]
		public string? MsgType { get; set; } = UserMessageTypes.RequestReply;

		/// <summary>
		/// 是否已读
		/// </summary>
		[SugarColumn(ColumnName = "is_read", IsNullable = true)]
		public bool? IsRead { get; set; } = false;

		/// <summary>
		/// 阅读时间
		/// </summary>
		[SugarColumn(ColumnName = "read_time", IsNullable = true)]
		public DateTime? ReadTime { get; set; }

		/// <summary>
		/// 来源业务ID（如联系请求ID，便于回溯）
		/// </summary>
		[SugarColumn(ColumnName = "source_id", IsNullable = true, Length = 50)]
		public string? SourceId { get; set; }
	}
}
