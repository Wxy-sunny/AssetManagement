using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AssetManagementSystem.Services.Sms
{
	/// <summary>
	/// 未启用短信服务的占位通道。
	/// 任何发送都会返回失败，避免出现"接口提示已发送、手机却收不到"的假成功。
	/// </summary>
	public class DisabledSmsSender : ISmsSender
	{
		private readonly ILogger<DisabledSmsSender> _logger;
		private readonly string _reason;

		public DisabledSmsSender(ILogger<DisabledSmsSender> logger, string reason)
		{
			_logger = logger;
			_reason = reason;
		}

		public string Name => "none";

		public Task<SmsSendResult> SendVerifyCodeAsync(string mobile, string code)
		{
			_logger.LogWarning("短信服务未启用，拒绝发送验证码短信。手机号：{Mobile}", Mask(mobile));
			return Task.FromResult(SmsSendResult.Fail(Name, _reason, "SMS_DISABLED"));
		}

		public Task<SmsSendResult> SendTextAsync(string mobile, string text)
		{
			_logger.LogWarning("短信服务未启用，跳过文本短信。手机号：{Mobile}", Mask(mobile));
			return Task.FromResult(SmsSendResult.Fail(Name, _reason, "SMS_DISABLED"));
		}

		private static string Mask(string mobile)
			=> string.IsNullOrEmpty(mobile) ? "" : mobile.Length <= 7 ? mobile : mobile[..3] + "****" + mobile[^4..];
	}

	/// <summary>
	/// 日志通道：把短信内容写进后端日志，不真正下发。
	/// 适用于内网演示 / 本地联调；配合 Sms:ReturnCodeInResponse=true 可在页面上直接看到验证码。
	/// </summary>
	public class LogSmsSender : ISmsSender
	{
		private readonly ILogger<LogSmsSender> _logger;

		public LogSmsSender(ILogger<LogSmsSender> logger)
		{
			_logger = logger;
		}

		public string Name => "log";

		public Task<SmsSendResult> SendVerifyCodeAsync(string mobile, string code)
		{
			_logger.LogInformation("[SMS-LOG] 验证码短信（未真实下发） 手机号：{Mobile}，验证码：{Code}", mobile, code);
			return Task.FromResult(SmsSendResult.Ok(Name, code, "验证码已生成（日志通道，未真实下发）"));
		}

		public Task<SmsSendResult> SendTextAsync(string mobile, string text)
		{
			_logger.LogInformation("[SMS-LOG] 文本短信（未真实下发） 手机号：{Mobile}，内容：{Text}", mobile, text);
			return Task.FromResult(SmsSendResult.Ok(Name, null, "已记录到日志"));
		}
	}

	/// <summary>
	/// 按配置创建短信发送通道。
	/// 配置项（Sms 节）：
	///   Enabled        总开关，false 时一律走 DisabledSmsSender
	///   Provider       none（默认）/ log / aliyun / webhook
	/// </summary>
	public static class SmsSenderFactory
	{
		public static ISmsSender Create(IConfiguration configuration, ILoggerFactory loggerFactory)
		{
			var enabled = configuration.GetValue<bool>("Sms:Enabled", false);
			var provider = (configuration["Sms:Provider"] ?? "none").Trim().ToLowerInvariant();

			if (!enabled)
			{
				return new DisabledSmsSender(
					loggerFactory.CreateLogger<DisabledSmsSender>(),
					"短信服务未启用，请管理员在 Sms 配置中启用并填写服务商参数");
			}

			switch (provider)
			{
				case "aliyun":
					return new AliyunSmsSender(configuration, loggerFactory.CreateLogger<AliyunSmsSender>());
				case "webhook":
					return new WebhookSmsSender(configuration, loggerFactory.CreateLogger<WebhookSmsSender>());
				case "log":
					return new LogSmsSender(loggerFactory.CreateLogger<LogSmsSender>());
				default:
					return new DisabledSmsSender(
						loggerFactory.CreateLogger<DisabledSmsSender>(),
						$"短信通道配置无效（Provider={provider}），请检查 Sms:Provider");
			}
		}
	}
}
