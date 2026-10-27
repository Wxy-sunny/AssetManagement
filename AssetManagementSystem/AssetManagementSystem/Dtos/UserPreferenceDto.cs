namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 用户个性化配置保存入参
	/// 注意：属性都带默认值——ApiController 会把"非可空引用类型"视为隐式 [Required]，
	/// 无默认值的属性在反序列化缺字段时会被直接拒掉（400 The xx field is required）。
	/// </summary>
	public class UserPreferenceDto
	{
		public string prefKey { get; set; } = string.Empty;
		public string prefValue { get; set; } = string.Empty;
	}
}
