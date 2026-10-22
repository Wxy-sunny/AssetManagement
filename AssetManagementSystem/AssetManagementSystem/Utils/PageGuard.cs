namespace AssetManagementSystem.Utils
{
	/// <summary>
	/// 分页参数保护：防止恶意/异常请求（如 pageSize=1000000）导致
	/// 数据库一次性拉取超大结果集，造成内存飙升与慢查询。
	/// </summary>
	public static class PageGuard
	{
		/// <summary>单页最大条数（超出按此值截断）</summary>
		public const int MaxPageSize = 200;

		/// <summary>规范化分页参数（pageIndex ≥ 1；1 ≤ pageSize ≤ MaxPageSize）</summary>
		/// <returns>规范化后的 (pageIndex, pageSize)，配合解构赋值使用</returns>
		public static (int pageIndex, int pageSize) Normalize(int pageIndex, int pageSize)
		{
			if (pageIndex < 1) pageIndex = 1;
			if (pageSize < 1) pageSize = 10;
			if (pageSize > MaxPageSize) pageSize = MaxPageSize;
			return (pageIndex, pageSize);
		}
	}
}
