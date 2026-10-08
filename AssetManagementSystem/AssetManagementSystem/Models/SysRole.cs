using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	[SugarTable("T_SYS_ROLE")]
	[DbBelong(DbNames.Asset)]
	public class SysRole:BaseModel
	{
		[SugarColumn(ColumnName = "role_id", IsNullable = false, Length = 50)]
		public string RoleId {  get; set; }

		[SugarColumn(ColumnName = "role_name", IsNullable = false, Length = 50)]
		public string RoleName { get; set; }

		[SugarColumn(ColumnName = "description", IsNullable = false, Length = 255)]
		public string Description { get; set; }

		/// <summary>
		/// 是否超级管理员角色：为 true 时拥有全部权限，且不参与权限校验
		/// </summary>
		[SugarColumn(ColumnName = "is_admin", IsNullable = true)]
		public bool? IsAdmin { get; set; } = false;
	}
}
