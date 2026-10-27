using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	/// <summary>
	/// 用户个性化配置表
	/// 作用：按用户记录界面偏好（如登录页布局、主题等），跨设备跟随用户。
	/// 设计：通用的 Key/Value 结构（user_id + pref_key 唯一），新增偏好项无需改表。
	/// 注意：pref_value 只存界面偏好，禁止存放敏感信息（表无加密且随用户信息接口可读）。
	/// </summary>
	[SugarTable("T_USER_PREFERENCE")]
	[SugarIndex("ux_userpref_user_key", nameof(UserId), OrderByType.Asc, nameof(PrefKey), OrderByType.Asc, true)]
	[DbBelong(DbNames.Asset)]
	public class UserPreference : BaseModel
	{
		/// <summary>所属用户（SysUser.Id，与 JWT Sub 一致）</summary>
		[SugarColumn(ColumnName = "user_id", IsNullable = false, Length = 50)]
		public string UserId { get; set; }

		/// <summary>配置键（如 login_layout）</summary>
		[SugarColumn(ColumnName = "pref_key", IsNullable = false, Length = 50)]
		public string PrefKey { get; set; } = string.Empty;

		/// <summary>配置值（如 left / center / right）</summary>
		[SugarColumn(ColumnName = "pref_value", IsNullable = false, Length = 255)]
		public string PrefValue { get; set; } = string.Empty;
	}
}
