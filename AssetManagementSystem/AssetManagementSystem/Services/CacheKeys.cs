namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 缓存键集中管理，避免各控制器硬编码字符串、并支持跨控制器失效
	/// （如用户增删会令角色列表的 userCount 变化，需同步失效角色列表缓存）。
	/// </summary>
	public static class CacheKeys
	{
		public const string CategoryTree = "ams:category:tree";
		public const string CategoryList = "ams:category:list";
		public const string DeptTree = "ams:dept:tree";
		public const string DeptList = "ams:dept:list";
		public const string RoleList = "ams:role:list";
		public const string UserPagePrefix = "ams:user:page:";
	}
}
