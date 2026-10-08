using System;

namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 用户 DTO（新增/编辑入参与列表出参共用）
	/// </summary>
	public class UserDto
	{
		public string? id { get; set; }

		/// <summary>登录账号</summary>
		public string? userId { get; set; }

		/// <summary>用户姓名</summary>
		public string? userName { get; set; }

		/// <summary>昵称</summary>
		public string? nickName { get; set; }

		/// <summary>密码（新增时必填；编辑时留空表示不修改）</summary>
		public string? password { get; set; }

		public string? email { get; set; }
		public string? mobile { get; set; }
		public string? roleId { get; set; }
		public string? roleName { get; set; }
		public string? deptId { get; set; }
		public string? deptName { get; set; }
		public bool? isEnabled { get; set; } = true;
		public DateTime? createTime { get; set; }
		public DateTime? lastLoginTime { get; set; }
	}

	/// <summary>
	/// 启用 / 禁用入参
	/// </summary>
	public class EnabledUpdateDto
	{
		public bool isEnabled { get; set; }
	}

	/// <summary>
	/// 重置密码入参
	/// </summary>
	public class PasswordResetDto
	{
		public string? password { get; set; }
	}
}
