using System;

namespace AssetManagementSystem.Dtos
{
	/// <summary>个人消息 DTO（列表与详情共用）</summary>
	public class UserMessageDto
	{
		public string? id { get; set; }
		public string? title { get; set; }
		public string? content { get; set; }
		public string? msgType { get; set; }
		public bool? isRead { get; set; }
		public DateTime? readTime { get; set; }
		public string? sourceId { get; set; }
		public DateTime? createTime { get; set; }
	}

	/// <summary>个人消息查询条件</summary>
	public class UserMessageQueryDto
	{
		public string? keyword { get; set; }
		/// <summary>unread 仅看未读；为空看全部</summary>
		public string? readStatus { get; set; }
		public int pageIndex { get; set; } = 1;
		public int pageSize { get; set; } = 10;
	}
}
