using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	/// <summary>
	/// 通知类型常量
	/// </summary>
	public static class NoticeTypes
	{
		/// <summary>系统公告</summary>
		public const string Notice = "notice";
		/// <summary>更新通知</summary>
		public const string Update = "update";
		/// <summary>紧急通知</summary>
		public const string Urgent = "urgent";
	}

	/// <summary>
	/// 系统通知表
	/// 说明：通知由管理员在"通知管理"中维护，发布后全员可在"系统通知"中查看
	/// </summary>
	[SugarTable("T_NOTIFICATION")]
	[DbBelong(DbNames.Asset)]
	public class Notification : BaseModel
	{
		[SugarColumn(ColumnName = "title", IsNullable = false, Length = 255)]
		public string Title { get; set; }

		[SugarColumn(ColumnName = "content", IsNullable = false, ColumnDataType = "nvarchar(max)")]
		public string Content { get; set; }

		/// <summary>
		/// 通知类型：notice 系统公告 / update 更新通知 / urgent 紧急通知
		/// </summary>
		[SugarColumn(ColumnName = "notice_type", IsNullable = true, Length = 20)]
		public string? NoticeType { get; set; } = NoticeTypes.Notice;

		/// <summary>
		/// 是否已发布（未发布仅管理端可见）
		/// </summary>
		[SugarColumn(ColumnName = "is_published", IsNullable = true)]
		public bool? IsPublished { get; set; } = false;

		/// <summary>
		/// 发布时间
		/// </summary>
		[SugarColumn(ColumnName = "publish_time", IsNullable = true)]
		public DateTime? PublishTime { get; set; }

		/// <summary>
		/// 发布人
		/// </summary>
		[SugarColumn(ColumnName = "publisher", IsNullable = true, Length = 50)]
		public string? Publisher { get; set; }
	}
}
