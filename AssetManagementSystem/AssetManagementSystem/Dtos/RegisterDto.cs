namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 注册入参
	/// </summary>
	public class RegisterDto
	{
		/// <summary>登录账号（唯一）</summary>
		public string? userId { get; set; }

		/// <summary>姓名（选填，默认取登录账号）</summary>
		public string? userName { get; set; }

		public string? password { get; set; }

		/// <summary>确认密码（后端二次校验）</summary>
		public string? confirmPassword { get; set; }

		/// <summary>手机号（选填）</summary>
		public string? mobile { get; set; }

		/// <summary>邮箱（选填）</summary>
		public string? email { get; set; }
	}
}
