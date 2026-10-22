using System.Text;
using AssetManagementSystem.Services;
using Xunit;

namespace AssetManagementSystem.Tests;

public class CaptchaServiceTests
{
	private readonly ICaptchaService _svc = new CaptchaService(new FakeCacheService());

	/// <summary>从 SVG 图片中还原验证码答案（与冒烟脚本同原理：字符在 &lt;text&gt; 节点里）</summary>
	private static string ExtractCode(string imageBase64)
	{
		var b64 = imageBase64.Replace("data:image/svg+xml;base64,", "");
		var svg = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
		var sb = new StringBuilder();
		foreach (System.Text.RegularExpressions.Match m in
			System.Text.RegularExpressions.Regex.Matches(svg, "<text[^>]*>([^<])</text>"))
			sb.Append(m.Groups[1].Value);
		return sb.ToString();
	}

	[Fact]
	public async Task Generate_ReturnsIdAndSvgImage()
	{
		var result = await _svc.GenerateAsync();

		Assert.False(string.IsNullOrWhiteSpace(result.captchaId));
		Assert.StartsWith("data:image/svg+xml;base64,", result.imageBase64);
		Assert.True(result.expireSeconds > 0);
		// SVG 里应包含 4 个字符
		Assert.Equal(4, ExtractCode(result.imageBase64).Length);
	}

	[Fact]
	public async Task Validate_CorrectCode_Passes_And_IsOneTime()
	{
		var result = await _svc.GenerateAsync();
		var code = ExtractCode(result.imageBase64);

		Assert.True(await _svc.ValidateAsync(result.captchaId, code));
		// 一次性：第二次校验同一验证码必须失败
		Assert.False(await _svc.ValidateAsync(result.captchaId, code));
	}

	[Fact]
	public async Task Validate_WrongCode_Fails()
	{
		var result = await _svc.GenerateAsync();
		var code = ExtractCode(result.imageBase64);
		var wrong = code == "AAAA" ? "BBBB" : "AAAA";

		Assert.False(await _svc.ValidateAsync(result.captchaId, wrong));
	}

	[Fact]
	public async Task Validate_IsCaseInsensitive()
	{
		var result = await _svc.GenerateAsync();
		var code = ExtractCode(result.imageBase64);

		Assert.True(await _svc.ValidateAsync(result.captchaId, code.ToLowerInvariant()));
	}

	[Fact]
	public async Task Validate_UnknownId_Or_EmptyInput_Fails()
	{
		Assert.False(await _svc.ValidateAsync("not-exist", "ABCD"));
		Assert.False(await _svc.ValidateAsync("", "ABCD"));

		var result = await _svc.GenerateAsync();
		Assert.False(await _svc.ValidateAsync(result.captchaId, null));
		Assert.False(await _svc.ValidateAsync(result.captchaId, "   "));
	}
}
