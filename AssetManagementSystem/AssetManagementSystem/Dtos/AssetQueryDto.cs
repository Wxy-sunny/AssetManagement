namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 资产查询条件（支持分页与多条件筛选）
	/// </summary>
	public class AssetQueryDto
	{
		/// <summary>关键字：资产名称 / 资产编码 / 规格型号 模糊匹配</summary>
		public string? keyword { get; set; }

		/// <summary>分类Id</summary>
		public string? categoryId { get; set; }

		/// <summary>资产状态：in_use / idle / repair / scrapped</summary>
		public string? assetStatus { get; set; }

		/// <summary>使用部门Id</summary>
		public string? deptId { get; set; }

		/// <summary>采购日期起始</summary>
		public DateTime? purchaseDateStart { get; set; }

		/// <summary>采购日期截止</summary>
		public DateTime? purchaseDateEnd { get; set; }

		/// <summary>页码，从 1 开始</summary>
		public int pageIndex { get; set; } = 1;

		/// <summary>每页条数</summary>
		public int pageSize { get; set; } = 10;
	}
}
