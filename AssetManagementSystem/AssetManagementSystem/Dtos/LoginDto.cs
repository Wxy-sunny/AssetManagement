namespace AssetManagementSystem.Dtos
{
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
