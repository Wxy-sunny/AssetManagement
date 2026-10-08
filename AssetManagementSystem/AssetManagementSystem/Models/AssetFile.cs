using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	public static class FileBizTypes
	{
		public const string Asset = "asset";
		public const string Common = "common";
		public const string Contract = "contract";
		public const string Manual = "manual";
	}

	[SugarTable("T_ASSET_FILE")]
	[DbBelong(DbNames.Asset)]
	public class AssetFile : BaseModel
	{
		[SugarColumn(ColumnName = "file_name", IsNullable = false, Length = 255)]
		public string FileName { get; set; }

		[SugarColumn(ColumnName = "saved_name", IsNullable = false, Length = 255)]
		public string SavedName { get; set; }

		[SugarColumn(ColumnName = "file_path", IsNullable = false, Length = 500)]
		public string FilePath { get; set; }

		[SugarColumn(ColumnName = "physical_path", IsNullable = true, Length = 500)]
		public string? PhysicalPath { get; set; }

		[SugarColumn(ColumnName = "file_ext", IsNullable = true, Length = 20)]
		public string? FileExt { get; set; }

		[SugarColumn(ColumnName = "file_size", IsNullable = true)]
		public long? FileSize { get; set; }

		[SugarColumn(ColumnName = "content_type", IsNullable = true, Length = 100)]
		public string? ContentType { get; set; }

		[SugarColumn(ColumnName = "biz_type", IsNullable = true, Length = 30)]
		public string? BizType { get; set; } = FileBizTypes.Common;

		[SugarColumn(ColumnName = "asset_id", IsNullable = true, Length = 50)]
		public string? AssetId { get; set; }

		[SugarColumn(ColumnName = "asset_name", IsNullable = true, Length = 255)]
		public string? AssetName { get; set; }

		[SugarColumn(ColumnName = "upload_user_name", IsNullable = true, Length = 255)]
		public string? UploadUserName { get; set; }
	}
}
