using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AssetManagementSystem.Utils
{
	/// <summary>
	/// SVG 安全清洗器
	/// 背景：SVG 是可执行脚本的 XML 文档，若原样以内联方式在站点域名下渲染，
	/// 其中的 &lt;script&gt; / 事件属性（onload 等）/ javascript: 链接会以当前域身份执行，
	/// 形成存储型 XSS（可窃取登录态）。
	/// 本类在上传时对 SVG 做白名单式清洗，剔除危险节点与属性；
	/// 预览接口再对 .svg 强制以附件方式响应，双保险。
	/// </summary>
	public static class SvgSanitizer
	{
		/// <summary>需要整体移除的危险元素（本地名，忽略命名空间）</summary>
		private static readonly HashSet<string> DangerousElements = new(StringComparer.OrdinalIgnoreCase)
		{
			"script", "foreignobject", "handler", "listener"
		};

		/// <summary>URL 类属性（本地名，忽略命名空间）</summary>
		private static readonly HashSet<string> UrlAttributes = new(StringComparer.OrdinalIgnoreCase)
		{
			"href", "src"
		};

		/// <summary>data: URI 中允许的图片 MIME（其余一律剔除）</summary>
		private static readonly HashSet<string> SafeDataMimes = new(StringComparer.OrdinalIgnoreCase)
		{
			"data:image/png", "data:image/jpeg", "data:image/jpg", "data:image/gif", "data:image/webp"
		};

		/// <summary>
		/// 清洗 SVG 字节内容，返回清洗后的字节数组。
		/// 若内容不是合法的 SVG 文档（XML 解析失败或根元素不是 svg），抛出 InvalidOperationException。
		/// </summary>
		public static byte[] Sanitize(byte[] content)
		{
			if (content == null || content.Length == 0)
				throw new InvalidOperationException("SVG 内容为空");

			// 去掉 UTF-8 BOM，避免 XmlReader 解析异常
			var text = Encoding.UTF8.GetString(content.TrimBom());

			var settings = new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit, // 禁止 DTD，防 XXE
				XmlResolver = null,
				IgnoreComments = true,
				IgnoreWhitespace = true
			};

			XDocument doc;
			try
			{
				using var reader = XmlReader.Create(new StringReader(text), settings);
				doc = XDocument.Load(reader);
			}
			catch (XmlException ex)
			{
				throw new InvalidOperationException($"SVG 不是合法的 XML 文档：{ex.Message}", ex);
			}

			if (doc.Root == null || !string.Equals(doc.Root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("SVG 根元素不是 <svg>");

			SanitizeElement(doc.Root);

			using var ms = new MemoryStream();
			using (var writer = XmlWriter.Create(ms, new XmlWriterSettings
			{
				Indent = false,
				Encoding = new UTF8Encoding(false), // 不带 BOM
				OmitXmlDeclaration = false
			}))
			{
				doc.WriteTo(writer);
			}
			return ms.ToArray();
		}

		/// <summary>递归清洗元素：移除危险子元素、事件属性与危险 URL</summary>
		private static void SanitizeElement(XElement element)
		{
			// 1. 移除危险元素（先摘除再递归其余子节点）
			foreach (var child in element.Elements().ToList())
			{
				if (DangerousElements.Contains(child.Name.LocalName))
				{
					child.Remove();
					continue;
				}
				SanitizeElement(child);
			}

			// 2. 清洗属性
			foreach (var attr in element.Attributes().ToList())
			{
				var name = attr.Name.LocalName;

				// 2.1 事件属性（onload / onclick / onerror ...）一律移除
				if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
				{
					attr.Remove();
					continue;
				}

				// 2.2 样式属性：剔除 expression(...) 与 javascript: 等
				if (string.Equals(name, "style", StringComparison.OrdinalIgnoreCase))
				{
					if (ContainsDangerousCss(attr.Value))
						attr.Remove();
					continue;
				}

				// 2.3 URL 属性：仅允许 http/https/相对路径/安全 data:image
				if (UrlAttributes.Contains(name) && !IsSafeUrl(attr.Value))
				{
					attr.Remove();
					continue;
				}
			}
		}

		/// <summary>判断 URL 是否安全</summary>
		private static bool IsSafeUrl(string? url)
		{
			if (string.IsNullOrWhiteSpace(url)) return true;

			var value = url.Trim().ToLowerInvariant();

			// 危险的脚本协议
			if (value.StartsWith("javascript:") || value.StartsWith("vbscript:") || value.StartsWith("livescript:"))
				return false;

			// data: 仅允许安全图片类型（阻止 data:image/svg+xml 嵌套脚本）
			if (value.StartsWith("data:"))
				return SafeDataMimes.Any(m => value.StartsWith(m + ";") || value.StartsWith(m + ","));

			return true; // http / https / 协议相对 / 相对路径 / #锚点
		}

		/// <summary>判断 CSS 是否含危险内容</summary>
		private static bool ContainsDangerousCss(string? css)
		{
			if (string.IsNullOrWhiteSpace(css)) return false;
			var value = css.ToLowerInvariant();
			return value.Contains("javascript:") || value.Contains("expression(") || value.Contains("vbscript:")
				|| value.Contains("-moz-binding") || value.Contains("behavior:");
		}
	}

	/// <summary>字节数组 BOM 处理扩展</summary>
	internal static class ByteArrayExtensions
	{
		/// <summary>去除开头的 UTF-8 BOM（若存在）</summary>
		public static byte[] TrimBom(this byte[] bytes)
		{
			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
				return bytes.Skip(3).ToArray();
			return bytes;
		}
	}
}
