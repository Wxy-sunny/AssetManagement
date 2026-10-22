using System;

namespace AssetManagementSystem.Dtos
{
	/// <summary>联系请求 DTO（列表与详情共用）</summary>
	public class ContactRequestDto
	{
		public string? id { get; set; }
		public string? account { get; set; }
		public string? mobile { get; set; }
		public string? requestType { get; set; }
		public string? content { get; set; }
		public string? status { get; set; }
		public string? senderId { get; set; }
		public string? handlerName { get; set; }
		public DateTime? handleTime { get; set; }
		public string? handleRemark { get; set; }
		public DateTime? createTime { get; set; }
		public DateTime? updateTime { get; set; }
	}

	/// <summary>联系请求查询条件</summary>
	public class ContactRequestQueryDto
	{
		public string? keyword { get; set; }
		public string? status { get; set; }
		public int pageIndex { get; set; } = 1;
		public int pageSize { get; set; } = 10;
	}

	/// <summary>公开提交联系请求的入参</summary>
	public class ContactRequestSubmitDto
	{
		public string? account { get; set; }
		public string? mobile { get; set; }
		public string? requestType { get; set; }
		public string? content { get; set; }
		public string? captchaId { get; set; }
		public string? captcha { get; set; }
		/// <summary>已登录用户可选带回（前台提交场景），登录页提交为 null</summary>
		public string? senderId { get; set; }
	}

	/// <summary>管理员处理请求入参</summary>
	public class HandleRequestDto
	{
		public string? remark { get; set; }
	}
}
