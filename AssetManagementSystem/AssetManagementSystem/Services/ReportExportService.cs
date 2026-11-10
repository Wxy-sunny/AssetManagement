using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 报表数据表（列 + 行），既用于导出也用于前端预览
	/// </summary>
	public class ReportTable
	{
		/// <summary>工作表名称</summary>
		public string Title { get; set; } = "Sheet1";

		/// <summary>列标题</summary>
		public List<string> Columns { get; set; } = new List<string>();

		/// <summary>数据行</summary>
		public List<List<object?>> Rows { get; set; } = new List<List<object?>>();
	}

	/// <summary>
	/// 报表导出服务
	/// 说明：
	/// 1) 全部在内存中生成字节流，不落盘（容器内没有可写共享目录，落盘方案不可靠）；
	/// 2) 不引入第三方 Excel 依赖：CSV 带 UTF-8 BOM（Excel 双击可直接打开且中文不乱码），
	///    XLSX 采用最小 OOXML 结构手工打包（无第三方库也能得到真正的 .xlsx）。
	/// </summary>
	public static class ReportExportService
	{
		/// <summary>
		/// 生成 CSV 字节流（含 UTF-8 BOM，保证 Excel 打开中文不乱码）
		/// </summary>
		public static byte[] BuildCsv(ReportTable table)
		{
			var sb = new StringBuilder();
			sb.AppendLine(string.Join(",", table.Columns.Select(EscapeCsv)));

			foreach (var row in table.Rows)
			{
				sb.AppendLine(string.Join(",", row.Select(v => EscapeCsv(FormatCell(v)))));
			}

			var body = Encoding.UTF8.GetBytes(sb.ToString());
			var bom = new byte[] { 0xEF, 0xBB, 0xBF };
			var result = new byte[bom.Length + body.Length];
			Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
			Buffer.BlockCopy(body, 0, result, bom.Length, body.Length);
			return result;
		}

		/// <summary>
		/// 生成 XLSX 字节流（最小 OOXML 打包，无第三方依赖）
		/// </summary>
		public static byte[] BuildXlsx(ReportTable table)
		{
			using var buffer = new MemoryStream();
			using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
			{
				WriteEntry(archive, "[Content_Types].xml", BuildContentTypes());
				WriteEntry(archive, "_rels/.rels", BuildRootRels());
				WriteEntry(archive, "xl/workbook.xml", BuildWorkbook());
				WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRels());
				WriteEntry(archive, "xl/styles.xml", BuildStyles());
				WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSheet(table));
			}

			return buffer.ToArray();
		}

		/// <summary>
		/// 单元格值格式化（数值用不变文化，日期按标准格式）
		/// </summary>
		private static string FormatCell(object? value)
		{
			if (value == null) return "";
			if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
			if (value is bool b) return b ? "是" : "否";
			if (value is decimal dec) return dec.ToString("0.##", CultureInfo.InvariantCulture);
			if (value is double dbl) return dbl.ToString("0.##", CultureInfo.InvariantCulture);
			if (value is float flt) return flt.ToString("0.##", CultureInfo.InvariantCulture);
			return value.ToString() ?? "";
		}

		/// <summary>
		/// 判断单元格是否按数值写入
		/// </summary>
		private static bool IsNumeric(object? value)
		{
			return value is decimal or double or float or int or long or short or byte;
		}

		/// <summary>
		/// CSV 字段转义（含逗号 / 引号 / 换行时用双引号包裹）
		/// </summary>
		private static string EscapeCsv(string? value)
		{
			var text = value ?? "";
			if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
			{
				return "\"" + text.Replace("\"", "\"\"") + "\"";
			}
			return text;
		}

		/// <summary>
		/// XML 文本转义
		/// </summary>
		private static string EscapeXml(string? value)
		{
			if (string.IsNullOrEmpty(value)) return "";
			return value
				.Replace("&", "&amp;")
				.Replace("<", "&lt;")
				.Replace(">", "&gt;")
				.Replace("\"", "&quot;")
				.Replace("'", "&apos;");
		}

		/// <summary>
		/// 列序号（1 起）转 Excel 列名（A / B / ... / Z / AA ...）
		/// </summary>
		private static string ColumnName(int index)
		{
			var name = "";
			while (index > 0)
			{
				var remainder = (index - 1) % 26;
				name = (char)('A' + remainder) + name;
				index = (index - 1) / 26;
			}
			return name;
		}

		private static void WriteEntry(ZipArchive archive, string path, string content)
		{
			var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
			using var stream = entry.Open();
			var bytes = new UTF8Encoding(false).GetBytes(content);
			stream.Write(bytes, 0, bytes.Length);
		}

		private static string BuildContentTypes()
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
				"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
				"<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
				"<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
				"<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
				"<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
				"<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
				"</Types>";
		}

		private static string BuildRootRels()
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
				"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
				"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
				"</Relationships>";
		}

		private static string BuildWorkbook()
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
				"<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
				"xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
				"<sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
				"</workbook>";
		}

		private static string BuildWorkbookRels()
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
				"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
				"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
				"<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
				"</Relationships>";
		}

		private static string BuildStyles()
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
				"<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
				"<fonts count=\"2\">" +
				"<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
				"<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
				"</fonts>" +
				"<fills count=\"2\">" +
				"<fill><patternFill patternType=\"none\"/></fill>" +
				"<fill><patternFill patternType=\"gray125\"/></fill>" +
				"</fills>" +
				"<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
				"<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
				"<cellXfs count=\"2\">" +
				"<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
				"<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
				"</cellXfs>" +
				"<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
				"</styleSheet>";
		}

		/// <summary>
		/// 构建工作表 XML（表头加粗，数值按数字写入，文本走 inlineStr）
		/// </summary>
		private static string BuildSheet(ReportTable table)
		{
			var sb = new StringBuilder();
			sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
			sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

			// 列宽（按标题长度估算，最小 10，最大 40）
			if (table.Columns.Count > 0)
			{
				sb.Append("<cols>");
				for (var i = 0; i < table.Columns.Count; i++)
				{
					var title = table.Columns[i] ?? "";
					var width = Math.Clamp(title.Length * 2 + 6, 10, 40);
					sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{width}\" customWidth=\"1\"/>");
				}
				sb.Append("</cols>");
			}

			sb.Append("<sheetData>");

			// 表头行（样式索引 1 = 加粗）
			sb.Append("<row r=\"1\">");
			for (var i = 0; i < table.Columns.Count; i++)
			{
				var reference = ColumnName(i + 1) + "1";
				sb.Append($"<c r=\"{reference}\" s=\"1\" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
				sb.Append(EscapeXml(table.Columns[i]));
				sb.Append("</t></is></c>");
			}
			sb.Append("</row>");

			// 数据行
			for (var r = 0; r < table.Rows.Count; r++)
			{
				var rowNumber = r + 2;
				sb.Append($"<row r=\"{rowNumber}\">");
				var row = table.Rows[r];
				for (var c = 0; c < table.Columns.Count; c++)
				{
					var value = c < row.Count ? row[c] : null;
					var reference = ColumnName(c + 1) + rowNumber;

					if (IsNumeric(value))
					{
						var number = Convert.ToDouble(value, CultureInfo.InvariantCulture)
							.ToString("0.####", CultureInfo.InvariantCulture);
						sb.Append($"<c r=\"{reference}\"><v>{number}</v></c>");
					}
					else
					{
						var text = EscapeXml(FormatCell(value));
						sb.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
						sb.Append(text);
						sb.Append("</t></is></c>");
					}
				}
				sb.Append("</row>");
			}

			sb.Append("</sheetData></worksheet>");
			return sb.ToString();
		}
	}
}
