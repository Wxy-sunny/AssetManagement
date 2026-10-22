using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	/// <summary>
	/// 刷新令牌表
	/// 作用：JWT 访问令牌过期后，用户无感知续期（滑动登录态）。
	/// 安全设计：
	///   1) 数据库中只存令牌的 SHA256 哈希（TokenHash），库泄露也无法直接冒用；
	///   2) 每次刷新即"轮换"——旧令牌作废、签发新令牌，防止刷新令牌被长期复用；
	///   3) 支持主动注销（logout 置为已作废）。
	/// </summary>
	[SugarTable("T_REFRESH_TOKEN")]
	[DbBelong(DbNames.Asset)]
	public class RefreshToken : BaseModel
	{
		/// <summary>所属用户（SysUser.Id，与 JWT Sub 一致）</summary>
		[SugarColumn(ColumnName = "user_id", IsNullable = false, Length = 50)]
		public string UserId { get; set; }

		/// <summary>刷新令牌的 SHA256 哈希（十六进制小写，64 字符）</summary>
		[SugarColumn(ColumnName = "token_hash", IsNullable = false, Length = 64)]
		public string TokenHash { get; set; }

		/// <summary>过期时间</summary>
		[SugarColumn(ColumnName = "expires_time", IsNullable = false)]
		public DateTime ExpiresTime { get; set; }

		/// <summary>是否已作废（轮换后旧令牌 / 主动注销）</summary>
		[SugarColumn(ColumnName = "is_revoked", IsNullable = true)]
		public bool? IsRevoked { get; set; } = false;

		/// <summary>签发时的客户端 IP</summary>
		[SugarColumn(ColumnName = "created_ip", IsNullable = true, Length = 50)]
		public string? CreatedIp { get; set; }
	}
}
