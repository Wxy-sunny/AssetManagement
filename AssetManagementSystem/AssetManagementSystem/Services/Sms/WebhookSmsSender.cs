using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AssetManagementSystem.Services.Sms
{
	/// <summary>
	/// 通用 HTTP 短信网关通道：把短信内容以 JSON POST 到配置的地址。
	/// 适用于短信宝、阿里云市场、以及各类自建短信代理服务。
	/// 请求体：{ "type": "code"|"text", "mobile": "...", "code": "...", "text": "...", "timestamp": 1760000000 }
	/// 若配置了 Sms:WebhookToken，会带上请求头 X-Sms-Token 供对方校验。
	/// </summary>
	public class WebhookSmsSender : ISmsSender
	{
		private readonly IConfiguration _configuration;
		private readonly ILogger<WebhookSmsSender> _logger;
		private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

		public WebhookSmsSender(IConfiguration configuration, ILogger<WebhookSmsSender> logger)
		{
			_configuration = configuration;
			_logger = logger;
		}

		public string Name => "webhook";

		public Task<SmsSendResult> SendVerifyCodeAsync(string mobile, string code)
			=> PostAsync("code", mobile, code, null);

		public Task<SmsSendResult> SendTextAsync(string mobile, string text)
			=> PostAsync("text", mobile, null, text);

		private async Task<SmsSendResult> PostAsync(string type, string mobile, string? code, string? text)
		{
			var url = _configuration["Sms:WebhookUrl"];
			if (string.IsNullOrWhiteSpace(url))
				return SmsSendResult.Fail(Name, "未配置短信网关地址（Sms:WebhookUrl）", "CONFIG_MISSING");

			var payload = new
			{
				type,
				mobile,
				code,
				text,
				timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			};

			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Post, url)
				{
					Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
				};
				var token = _configuration["Sms:WebhookToken"];
				if (!string.IsNullOrWhiteSpace(token))
					request.Headers.TryAddWithoutValidation("X-Sms-Token", token);

				using var response = await Http.SendAsync(request);
				var body = await response.Content.ReadAsStringAsync();
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError("短信网关返回 HTTP {Status}：{Body}", (int)response.StatusCode, body);
					return SmsSendResult.Fail(Name, $"短信网关返回异常（HTTP {(int)response.StatusCode}）", response.StatusCode.ToString());
				}

				_logger.LogInformation("短信网关发送成功，手机号：{Mobile}，响应：{Body}", mobile, body);
				return SmsSendResult.Ok(Name);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "调用短信网关异常");
				return SmsSendResult.Fail(Name, "短信服务暂时不可用，请稍后再试", ex.GetType().Name);
			}
		}
	}
}
