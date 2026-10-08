using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;
namespace AssetManagementSystem.Models
{
	[SugarTable("T_ASSET_CATEGORY")]
	[DbBelong(DbNames.Asset)]
	public class AssetCategory: BaseModel
	{
		/// <summary>
		/// 父级Id
		/// </summary>
		[SugarColumn(ColumnName = "parent_id", IsNullable = true, Length = 50)]
		public string? ParentId { get; set; }
		/// <summary>
		/// 分类代码
		/// </summary>
		[SugarColumn(ColumnName = "categroy_code", IsNullable = true, Length = 50)]
		public string? CategoryCode { get; set; }

		/// <summary>
		/// 分类名称
		/// </summary>
		[SugarColumn(ColumnName = "categroy_name", IsNullable = true, Length = 255)]
		public string? CategoryName { get; set; }
		/// <summary>
		/// 描述
		/// </summary>
		[SugarColumn(ColumnName = "description", IsNullable = true, Length = 255)]
		public string? Description { get; set; }
	}
}
