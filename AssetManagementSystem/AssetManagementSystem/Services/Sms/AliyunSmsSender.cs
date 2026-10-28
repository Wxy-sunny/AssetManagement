using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AssetManagementSystem.Services.Sms
{
	/// <summary>
	/// 阿里云短信通道（dysmsapi / SendSms）。直接调用 OpenAPI 并自行完成 HMAC-SHA1 签名，
	/// 无需引入阿里云 SDK 依赖。
	/// 配置：AccessKeyId / AccessKeySecret / SignName / TemplateCode /
	///       RegionId(默认 cn-hangzhou) / Endpoint(默认 https://dysmsapi.aliyuncs.com/)；
	///       文本短信另需 TextTemplateCode。
	/// </summary>
	public class AliyunSmsSender : ISmsSender
	{
		private readonly IConfiguration _configuration;
		private readonly ILogger<AliyunSmsSender> _logger;
		private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

		public AliyunSmsSender(IConfiguration configuration, ILogger<AliyunSmsSender> logger)
		{
			_configuration = configuration;
			_logger = logger;
		}

		public string Name => "aliyun";

		public Task<SmsSendResult> SendVerifyCodeAsync(string mobile, string code)
		{
			var templateCode = _configuration["Sms:TemplateCode"];
			if (string.IsNullOrWhiteSpace(templateCode))
				return Task.FromResult(SmsSendResult.Fail(Name, "未配置短信模板号（Sms:TemplateCode）", "CONFIG_MISSING"));
			// 阿里云验证码模板的变量名固定为 code
			return SendAsync(mobile, templateCode, "{\"code\":\"" + code + "\"}");
		}

		public Task<SmsSendResult> SendTextAsync(string mobile, string text)
		{
			var templateCode = _configuration["Sms:TextTemplateCode"];
			if (string.IsNullOrWhiteSpace(templateCode))
				return Task.FromResult(SmsSendResult.Fail(Name, "未配置文本短信模板号（Sms:TextTemplateCode）", "CONFIG_MISSING"));
			return SendAsync(mobile, templateCode, "{\"content\":\"" + EscapeJson(text) + "\"}");
		}

		private async Task<SmsSendResult> SendAsync(string mobile, string templateCode, string templateParam)
		{
			var accessKeyId = _configuration["Sms:AccessKeyId"];
			var accessKeySecret = _configuration["Sms:AccessKeySecret"];
			var signName = _configuration["Sms:SignName"];
			if (string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(accessKeySecret))
				return SmsSendResult.Fail(Name, "未配置短信服务密钥（Sms:AccessKeyId / Sms:AccessKeySecret）", "CONFIG_MISSING");
			if (string.IsNullOrWhiteSpace(signName))
				return SmsSendResult.Fail(Name, "未配置短信签名（Sms:SignName）", "CONFIG_MISSING");

			var endpoint = _configuration["Sms:Endpoint"];
			if (string.IsNullOrWhiteSpace(endpoint)) endpoint = "https://dysmsapi.aliyuncs.com/";
			var regionId = _configuration["Sms:RegionId"];
			if (string.IsNullOrWhiteSpace(regionId)) regionId = "cn-hangzhou";

			var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
			{
				{ "AccessKeyId", accessKeyId },
				{ "Action", "SendSms" },
				{ "Format", "JSON" },
				{ "PhoneNumbers", mobile },
				{ "RegionId", regionId },
				{ "SignName", signName },
				{ "SignatureMethod", "HMAC-SHA1" },
				{ "SignatureNonce", Guid.NewGuid().ToString("N") },
				{ "SignatureVersion", "1.0" },
				{ "TemplateCode", templateCode },
				{ "TemplateParam", templateParam },
				{ "Timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) },
				{ "Version", "2017-05-25" }
			};

			// 1) 规范化参数（不含 Signature）→ 2) 拼待签名字符串 → 3) HMAC-SHA1 签名
			var canonicalized = AliyunSigner.BuildCanonicalizedQueryString(parameters);
			var stringToSign = AliyunSigner.BuildStringToSign(canonicalized);
			parameters["Signature"] = AliyunSigner.Sign(accessKeySecret, stringToSign);

			// 4) 组装请求体（含 Signature）
			var body = AliyunSigner.BuildCanonicalizedQueryString(parameters);

			try
			{
				using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
				using var response = await Http.PostAsync(endpoint, content);
				var responseText = await response.Content.ReadAsStringAsync();
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError("阿里云短信请求失败 HTTP {Status}：{Body}", (int)response.StatusCode, responseText);
					return SmsSendResult.Fail(Name, $"短信网关返回异常（HTTP {(int)response.StatusCode}）", response.StatusCode.ToString());
				}

				using var doc = JsonDocument.Parse(responseText);
				var root = doc.RootElement;
				var code = GetString(root, "Code");
				var message = GetString(root, "Message");
				var requestId = GetString(root, "RequestId");
				if (!string.Equals(code, "OK", StringComparison.OrdinalIgnoreCase))
				{
					_logger.LogError("阿里云短信发送失败 Code={Code} Message={Message} RequestId={RequestId}", code, message, requestId);
					return SmsSendResult.Fail(Name, $"短信发送失败：{message ?? code}", code);
				}
				_logger.LogInformation("阿里云短信发送成功，手机号：{Mobile}，RequestId={RequestId}", mobile, requestId);
				return SmsSendResult.Ok(Name);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "调用阿里云短信接口异常");
				return SmsSendResult.Fail(Name, "短信服务暂时不可用，请稍后再试", ex.GetType().Name);
			}
		}

		private static string? GetString(JsonElement root, string name)
			=> root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

		private static string EscapeJson(string? text)
			=> (text ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
	}
}
