using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	/// <summary>
	/// 角色权限关联表
	/// 说明：权限项本身由代码中的 Permissions 常量定义，此表只记录"角色拥有哪些权限"，
	/// 因此不需要单独的权限表，避免权限项与代码不同步。
	/// </summary>
	[SugarTable("T_SYS_ROLE_PERMISSION")]
	[DbBelong(DbNames.Asset)]
	public class SysRolePermission
	{
		[SugarColumn(IsPrimaryKey = true, ColumnName = "id", Length = 50)]
		public string Id { get; set; } = Guid.NewGuid().ToString();

		/// <summary>角色编码（对应 SysRole.RoleId）</summary>
		[SugarColumn(ColumnName = "role_id", IsNullable = false, Length = 50)]
		public string RoleId { get; set; }

		/// <summary>权限编码（对应 Permissions 常量）</summary>
		[SugarColumn(ColumnName = "permission_code", IsNullable = false, Length = 100)]
		public string PermissionCode { get; set; }

		[SugarColumn(ColumnName = "create_time", IsNullable = true)]
		public DateTime? CreateTime { get; set; }
	}
}
