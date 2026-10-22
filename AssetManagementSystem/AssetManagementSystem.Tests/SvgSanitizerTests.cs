using System;
using System.Text;
using AssetManagementSystem.Utils;
using Xunit;

namespace AssetManagementSystem.Tests;

public class SvgSanitizerTests
{
	private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);
	private static string Text(byte[] b) => Encoding.UTF8.GetString(b);

	private const string SafeSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""100"" height=""100""><rect width=""100"" height=""100"" fill=""#00ff00""/><circle cx=""50"" cy=""50"" r=""40""/></svg>";

	[Fact]
	public void Sanitize_KeepsSafeSvgContent()
	{
		var result = Text(SvgSanitizer.Sanitize(Bytes(SafeSvg)));

		Assert.Contains("<svg", result);
		Assert.Contains("rect", result);
		Assert.Contains("circle", result);
	}

	[Fact]
	public void Sanitize_RemovesScriptElement()
	{
		var svg = @"<svg xmlns=""http://www.w3.org/2000/svg""><script>alert(1)</script><rect/></svg>";

		var result = Text(SvgSanitizer.Sanitize(Bytes(svg)));

		Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("alert", result);
		Assert.Contains("rect", result);
	}

	[Fact]
	public void Sanitize_RemovesEventAttributes()
	{
		var svg = @"<svg xmlns=""http://www.w3.org/2000/svg""><rect onload=""alert(1)"" onclick=""steal()"" width=""10""/></svg>";

		var result = Text(SvgSanitizer.Sanitize(Bytes(svg)));

		Assert.DoesNotContain("onload", result, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("alert", result);
	}

	[Fact]
	public void Sanitize_RemovesJavascriptUrl()
	{
		// 合法的 xlink 用法需声明命名空间
		var svg = @"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink""><a xlink:href=""javascript:alert(1)""><rect/></a></svg>";

		var result = Text(SvgSanitizer.Sanitize(Bytes(svg)));

		Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Sanitize_UndeclaredPrefix_TreatedAsInvalidAndRejected()
	{
		// 使用了未声明前缀的 XML 属于非法文档：直接拒绝（失败即拒，不猜测）
		var svg = @"<svg xmlns=""http://www.w3.org/2000/svg""><a xlink:href=""javascript:alert(1)""><rect/></a></svg>";

		Assert.Throws<InvalidOperationException>(() => SvgSanitizer.Sanitize(Bytes(svg)));
	}

	[Fact]
	public void Sanitize_BlocksNestedSvgDataUri_ButKeepsSafeImageData()
	{
		var dangerous = @"<svg xmlns=""http://www.w3.org/2000/svg""><image href=""data:image/svg+xml;base64,PHN2Zz48L3N2Zz4="" /></svg>";
		var safe = @"<svg xmlns=""http://www.w3.org/2000/svg""><image href=""data:image/png;base64,iVBORw0KGgo="" /></svg>";

		Assert.DoesNotContain("image/svg+xml", Text(SvgSanitizer.Sanitize(Bytes(dangerous))), StringComparison.OrdinalIgnoreCase);
		Assert.Contains("data:image/png", Text(SvgSanitizer.Sanitize(Bytes(safe))));
	}

	[Fact]
	public void Sanitize_RemovesDangerousCss()
	{
		var svg = @"<svg xmlns=""http://www.w3.org/2000/svg""><rect style=""behavior:url(#x);background:url(javascript:alert(1))""/></svg>";

		var result = Text(SvgSanitizer.Sanitize(Bytes(svg)));

		Assert.DoesNotContain("behavior", result, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Sanitize_InvalidXml_Throws()
	{
		Assert.Throws<InvalidOperationException>(() => SvgSanitizer.Sanitize(Bytes("<svg><unclosed>")));
	}

	[Fact]
	public void Sanitize_NonSvgRoot_Throws()
	{
		Assert.Throws<InvalidOperationException>(() => SvgSanitizer.Sanitize(Bytes("<html><body>x</body></html>")));
	}

	[Fact]
	public void Sanitize_StripsUtf8Bom()
	{
		var bom = new byte[] { 0xEF, 0xBB, 0xBF };
		var content = Bytes(SafeSvg);

		var result = Text(SvgSanitizer.Sanitize(bom.Concat(content).ToArray()));

		Assert.Contains("<svg", result);
	}
}
