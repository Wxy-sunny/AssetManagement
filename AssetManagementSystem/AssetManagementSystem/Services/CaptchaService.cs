using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 图形验证码服务
	/// 说明：生成 4 位字符的验证码并绘制为 SVG 图片（无需第三方绘图库），
	/// 正确答案以 captchaId 为键存入缓存（默认 2 分钟），校验通过后立即失效（一次性）。
	/// </summary>
	public interface ICaptchaService
	{
		/// <summary>生成验证码，返回验证码ID、SVG 图片(base64 data URI)与有效期（秒）</summary>
		Task<CaptchaResult> GenerateAsync();

		/// <summary>校验用户输入，校验通过即失效该验证码（防止重复使用）</summary>
		Task<bool> ValidateAsync(string? captchaId, string? input);
	}

	/// <summary>验证码生成结果</summary>
	public class CaptchaResult
	{
		public string captchaId { get; set; } = "";
		public string imageBase64 { get; set; } = "";
		public int expireSeconds { get; set; }
	}

	public class CaptchaService : ICaptchaService
	{
		private readonly ICacheService _cache;
		// 去除易混淆字符：0/O/1/l/I
		private static readonly char[] Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
		private const int Length = 4;
		private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

		public CaptchaService(ICacheService cache)
		{
			_cache = cache;
		}

		public async Task<CaptchaResult> GenerateAsync()
		{
			var code = RandomCode();
			var captchaId = Guid.NewGuid().ToString("N");
			await _cache.SetAsync(CaptchaKey(captchaId), code, Ttl);
			return new CaptchaResult
			{
				captchaId = captchaId,
				imageBase64 = "data:image/svg+xml;base64," + SvgBase64(code),
				expireSeconds = (int)Ttl.TotalSeconds
			};
		}

		public async Task<bool> ValidateAsync(string? captchaId, string? input)
		{
			if (string.IsNullOrWhiteSpace(captchaId) || string.IsNullOrWhiteSpace(input))
				return false;

			var expected = await _cache.GetAsync<string>(CaptchaKey(captchaId));
			if (string.IsNullOrWhiteSpace(expected))
				return false;

			// 一次性：校验后立即失效，避免被重复使用
			await _cache.RemoveAsync(CaptchaKey(captchaId));
			return string.Equals(expected, input.Trim(), StringComparison.OrdinalIgnoreCase);
		}

		private static string CaptchaKey(string id) => "ams:captcha:" + id;

		private static string RandomCode()
		{
			var sb = new StringBuilder();
			using var rng = RandomNumberGenerator.Create();
			var bytes = new byte[Length];
			rng.GetBytes(bytes);
			for (int i = 0; i < Length; i++)
				sb.Append(Chars[bytes[i] % Chars.Length]);
			return sb.ToString();
		}

		/// <summary>将验证码绘制为轻量 SVG（含干扰线、随机颜色/旋转），返回 base64</summary>
		private static string SvgBase64(string code)
		{
			var rand = new Random();
			var colors = new[] { "#e74c3c", "#2980b9", "#27ae60", "#8e44ad", "#d35400", "#16a085" };
			var sb = new StringBuilder();
			sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"40\" viewBox=\"0 0 120 40\">");
			sb.Append("<rect width=\"120\" height=\"40\" fill=\"#f7f8fa\"/>");
			// 干扰线
			for (int i = 0; i < 4; i++)
			{
				var x1 = rand.Next(0, 120); var y1 = rand.Next(0, 40);
				var x2 = rand.Next(0, 120); var y2 = rand.Next(0, 40);
				var c = colors[rand.Next(colors.Length)];
				sb.Append($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{c}\" stroke-width=\"1\" opacity=\"0.35\"/>");
			}
			// 字符
			for (int i = 0; i < code.Length; i++)
			{
				var ch = code[i];
				var x = 14 + i * 26 + rand.Next(-3, 4);
				var y = 27 + rand.Next(-4, 5);
				var size = rand.Next(22, 30);
				var color = colors[rand.Next(colors.Length)];
				var angle = rand.Next(-18, 18);
				sb.Append($"<text x=\"{x}\" y=\"{y}\" font-family=\"Arial, sans-serif\" font-size=\"{size}\" font-weight=\"bold\" fill=\"{color}\" transform=\"rotate({angle} {x} {y})\">{ch}</text>");
			}
			sb.Append("</svg>");

			return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
		}
	}
}
