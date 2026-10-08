namespace AssetManagementSystem.Utils
{
	/// <summary>
	/// 密码加解密工具（BCrypt）
	/// </summary>
	public static class PasswordHelper
	{
		/// <summary>
		/// 生成 BCrypt 哈希
		/// </summary>
		public static string Hash(string password)
		{
			return BCrypt.Net.BCrypt.HashPassword(password);
		}

		/// <summary>
		/// 校验密码，并兼容历史明文密码（平滑迁移）
		/// </summary>
		/// <param name="password">用户输入的明文密码</param>
		/// <param name="stored">数据库中存储的密码（BCrypt 哈希或历史明文）</param>
		/// <param name="needUpgrade">命中历史明文时为 true，调用方应将其升级为哈希</param>
		public static bool Verify(string password, string? stored, out bool needUpgrade)
		{
			needUpgrade = false;
			if (string.IsNullOrEmpty(stored)) return false;

			// BCrypt 哈希固定以 $2a / $2b / $2y 开头
			if (stored.StartsWith("$2"))
			{
				try
				{
					return BCrypt.Net.BCrypt.Verify(password, stored);
				}
				catch
				{
					// 哈希格式异常时按失败处理
					return false;
				}
			}

			// 历史明文密码：匹配成功则提示调用方升级为哈希
			if (string.Equals(stored, password, StringComparison.Ordinal))
			{
				needUpgrade = true;
				return true;
			}

			return false;
		}
	}
}
