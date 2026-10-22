namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 刷新令牌入参（刷新访问令牌 / 退出登录共用）
	/// </summary>
	public class RefreshTokenDto
	{
		public string? refreshToken { get; set; }
	}

	public class LoginDto
	{
		public string? userid { get; set; }   // 对应数据库的 user_id
		public string? username { get; set; }// 对应数据库的 name
		public string? password { get; set; }
		public string? captcha { get; set; }
		public string? captchaId { get; set; }
		public string? grantType { get; set; }
		public string? tenantId { get; set; }
		public string? loginType { get; set; }
	}
}
