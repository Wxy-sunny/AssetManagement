namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 忘记密码（手机号 + 验证码重置）入参
	/// </summary>
	public class ForgotPasswordDto
	{
		public string? mobile { get; set; }
		public string? code { get; set; }
		public string? newPassword { get; set; }
		public string? confirmPassword { get; set; }
	}
}
