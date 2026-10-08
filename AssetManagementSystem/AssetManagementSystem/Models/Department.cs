using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	/// <summary>
	/// 部门/组织架构表（支持树形层级）
	/// </summary>
	[SugarTable("T_SYS_DEPARTMENT")]
	[DbBelong(DbNames.Asset)]
	public class Department : BaseModel
	{
		/// <summary>
		/// 部门编码（业务唯一）
		/// </summary>
		[SugarColumn(ColumnName = "department_id", IsNullable = false, Length = 50)]
		public string DepartmentId { get; set; }

		/// <summary>
		/// 部门名称
		/// </summary>
		[SugarColumn(ColumnName = "department_name", IsNullable = false, Length = 255)]
		public string DepartmentName { get; set; }

		/// <summary>
		/// 父级部门Id（根节点为空）
		/// </summary>
		[SugarColumn(ColumnName = "parent_id", IsNullable = true, Length = 50)]
		public string? ParentId { get; set; }

		/// <summary>
		/// 部门负责人
		/// </summary>
		[SugarColumn(ColumnName = "leader_name", IsNullable = true, Length = 50)]
		public string? LeaderName { get; set; }

		/// <summary>
		/// 联系电话
		/// </summary>
		[SugarColumn(ColumnName = "phone", IsNullable = true, Length = 20)]
		public string? Phone { get; set; }

		/// <summary>
		/// 排序号（越小越靠前）
		/// </summary>
		[SugarColumn(ColumnName = "sort_order", IsNullable = true)]
		public int? SortOrder { get; set; } = 0;

		/// <summary>
		/// 描述
		/// </summary>
		[SugarColumn(ColumnName = "description", IsNullable = true, Length = 255)]
		public string? Description { get; set; }
	}
}
