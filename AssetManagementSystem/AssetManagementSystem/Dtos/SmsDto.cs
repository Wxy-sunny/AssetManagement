namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 发送验证码入参
	/// </summary>
	public class SmsCodeDto
	{
		public string? mobile { get; set; }
	}

	/// <summary>
	/// 手机号 + 验证码登录入参
	/// </summary>
	public class SmsLoginDto
	{
		public string? mobile { get; set; }
		public string? code { get; set; }
	}

	/// <summary>
	/// 绑定手机号入参（登录状态下把手机号绑定到当前账户）
	/// </summary>
	public class BindMobileDto
	{
		public string? mobile { get; set; }
		public string? code { get; set; }
	}
}
