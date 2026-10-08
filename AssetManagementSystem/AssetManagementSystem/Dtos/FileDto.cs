using System;

namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 资料/附件 DTO
	/// </summary>
	public class FileDto
	{
		public string? id { get; set; }
		public string? fileName { get; set; }
		public string? savedName { get; set; }

		/// <summary>相对访问路径 /uploads/yyyy/MM/xxx.ext</summary>
		public string? filePath { get; set; }
		public string? fileExt { get; set; }
		public long? fileSize { get; set; }

		/// <summary>格式化后的大小，如 1.2 MB</summary>
		public string? fileSizeText { get; set; }
		public string? contentType { get; set; }
		public string? bizType { get; set; }
		public string? assetId { get; set; }
		public string? assetName { get; set; }
		public string? uploadUserName { get; set; }
		public DateTime? createTime { get; set; }
	}
}
