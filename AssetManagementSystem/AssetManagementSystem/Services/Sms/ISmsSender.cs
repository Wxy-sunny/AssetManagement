namespace AssetManagementSystem.Services.Sms
{
	/// <summary>
	/// 短信发送结果
	/// </summary>
	public class SmsSendResult
	{
		/// <summary>是否发送成功</summary>
		public bool Success { get; init; }

		/// <summary>结果描述（失败时可直接展示给用户）</summary>
		public string Message { get; init; } = string.Empty;

		/// <summary>使用的发送通道（none / log / aliyun / webhook）</summary>
		public string Provider { get; init; } = string.Empty;

		/// <summary>网关返回的错误码（便于排查）</summary>
		public string? ErrorCode { get; init; }

		/// <summary>验证码（仅验证码场景有值，便于联调回显）</summary>
		public string? Code { get; init; }

		public static SmsSendResult Ok(string provider, string? code = null, string message = "发送成功")
			=> new() { Success = true, Provider = provider, Message = message, Code = code };

		public static SmsSendResult Fail(string provider, string message, string? errorCode = null)
			=> new() { Success = false, Provider = provider, Message = message, ErrorCode = errorCode };
	}

	/// <summary>
	/// 短信发送通道抽象。
	/// 实现类：DisabledSmsSender（未启用）、LogSmsSender（仅日志，联调用）、
	/// AliyunSmsSender（阿里云短信）、WebhookSmsSender（自定义 HTTP 网关）。
	/// </summary>
	public interface ISmsSender
	{
		/// <summary>通道名称</summary>
		string Name { get; }

		/// <summary>发送验证码短信</summary>
		Task<SmsSendResult> SendVerifyCodeAsync(string mobile, string code);

		/// <summary>发送普通文本短信（如业务通知）</summary>
		Task<SmsSendResult> SendTextAsync(string mobile, string text);
	}
}
