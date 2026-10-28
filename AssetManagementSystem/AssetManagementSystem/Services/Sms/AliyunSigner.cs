using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AssetManagementSystem.Services.Sms
{
	/// <summary>阿里云 OpenAPI（RPC 风格）签名工具：HMAC-SHA1 + RFC3986 百分号编码。</summary>
	public static class AliyunSigner
	{
		/// <summary>规范化参数串：按键名字典序排序，key/value 分别百分号编码后用 & 连接</summary>
		public static string BuildCanonicalizedQueryString(IEnumerable<KeyValuePair<string, string>> parameters)
			=> string.Join("&", parameters.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{PercentEncode(p.Key)}={PercentEncode(p.Value)}"));

		/// <summary>待签名字符串：POST&%2F&百分号编码(规范化参数串)</summary>
		public static string BuildStringToSign(string canonicalizedQueryString) => $"POST&{PercentEncode("/")}&{PercentEncode(canonicalizedQueryString)}";

		/// <summary>HMAC-SHA1 签名（阿里云要求密钥末尾补一个 & 再参与计算）</summary>
		public static string Sign(string accessKeySecret, string stringToSign)
		{
			using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(accessKeySecret + "&"));
			return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
		}

		/// <summary>RFC3986 百分号编码（空格→%20、星号→%2A、波浪线→%7E）</summary>
		public static string PercentEncode(string? value)
		{
			if (string.IsNullOrEmpty(value)) return string.Empty;
			var bytes = Encoding.UTF8.GetBytes(value);
			var sb = new StringBuilder(bytes.Length * 3);
			foreach (var b in bytes)
			{
				var c = (char)b;
				if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.') sb.Append(c);
				else sb.Append('%').Append(b.ToString("X2"));
			}
			return sb.ToString();
		}
	}
}
