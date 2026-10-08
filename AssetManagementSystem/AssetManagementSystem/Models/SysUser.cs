using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	[SugarTable("T_SYS_USER")]
	[DbBelong(DbNames.Asset)]  
	public class SysUser : BaseModel
	{
		/// <summary>
		/// 登录账号（唯一）
		/// </summary>
		[SugarColumn(ColumnName = "user_id", IsNullable = false, Length = 50)]
		public string UserId { get; set; }

		/// <summary>
		/// 用户姓名
		/// </summary>
		[SugarColumn(ColumnName = "name", IsNullable = true, Length = 255)]
		public string? UserName { get; set; }

		/// <summary>
		/// 用户昵称
		/// </summary>
		[SugarColumn(ColumnName = "nick_name", IsNullable = true, Length = 255)]
		public string? NickName { get; set; }

		/// <summary>
		/// 密码（存储哈希值）
		/// </summary>
		[SugarColumn(ColumnName = "password", IsNullable = false, Length = 255)]
		public string PassWord { get; set; }

		/// <summary>
		/// 邮箱
		/// </summary>
		[SugarColumn(ColumnName = "email", IsNullable = true, Length = 100)]
		public string? Email { get; set; }

		/// <summary>
		/// 手机号
		/// </summary>
		[SugarColumn(ColumnName = "mobile", IsNullable = true, Length = 20)]
		public string? Mobile { get; set; }

		/// <summary>
		/// 角色ID（关联角色表）
		/// </summary>
		[SugarColumn(ColumnName = "role_id", IsNullable = true,Length =50)]
		public string? RoleId { get; set; }
		/// <summary>
		/// 角色名称（关联角色表）
		/// </summary>
		[SugarColumn(ColumnName = "role_name", IsNullable = true,Length =50)]
		public string? RoleName { get; set; }

		/// <summary>
		/// 部门ID
		/// </summary>
		[SugarColumn(ColumnName = "dept_id", IsNullable = true, Length = 50)]
		public string? DeptId { get; set; }


		/// <summary>
		/// 最后登录时间
		/// </summary>
		[SugarColumn(ColumnName = "last_login_time", IsNullable = true)]
		public DateTime? LastLoginTime { get; set; }
	}
}
