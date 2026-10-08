namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 更新个人资料入参（当前登录用户自助修改）
	/// </summary>
	public class UpdateProfileDto
	{
		public string? userName { get; set; }
		public string? nickName { get; set; }
		public string? email { get; set; }
		public string? mobile { get; set; }
	}

	/// <summary>
	/// 修改密码入参（需校验原密码）
	/// </summary>
	public class ChangePasswordDto
	{
		/// <summary>原密码</summary>
		public string? oldPassword { get; set; }

		/// <summary>新密码</summary>
		public string? newPassword { get; set; }

		/// <summary>确认新密码</summary>
		public string? confirmPassword { get; set; }
	}
}
