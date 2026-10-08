using System;

namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 通知 DTO（列表与表单共用）
	/// </summary>
	public class NotificationDto
	{
		public string? id { get; set; }
		public string title { get; set; } = string.Empty;
		public string content { get; set; } = string.Empty;
		public string? noticeType { get; set; }
		public bool? isPublished { get; set; } = false;
		public DateTime? publishTime { get; set; }
		public string? publisher { get; set; }
		public DateTime? createTime { get; set; }
		public DateTime? updateTime { get; set; }
	}

	/// <summary>
	/// 通知查询条件
	/// </summary>
	public class NotificationQueryDto
	{
		/// <summary>关键字：标题</summary>
		public string? keyword { get; set; }

		/// <summary>通知类型：notice / update / urgent</summary>
		public string? noticeType { get; set; }

		/// <summary>发布状态</summary>
		public bool? isPublished { get; set; }

		public int pageIndex { get; set; } = 1;
		public int pageSize { get; set; } = 10;
	}

	/// <summary>
	/// 发布 / 撤回通知入参
	/// </summary>
	public class PublishNotificationDto
	{
		public bool isPublished { get; set; }
	}
}
