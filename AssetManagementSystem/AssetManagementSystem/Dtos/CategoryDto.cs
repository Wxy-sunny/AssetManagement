namespace AssetManagementSystem.Dtos
{
	public class CategoryDto
	{
		// 注意：id/sortOrder 在"新增"时前端不传，且 ApiController 会把"非可空引用类型"视为隐式 [Required]，
		// 若不标记为可空，新增分类会被模型绑定阶段直接拒掉（400 The id field is required）。
		public string? id { get; set; }
		public string categoryCode { get; set; } = string.Empty;
		public string categoryName { get; set; } = string.Empty;
		public string? parentId { get; set; }
		public string? sortOrder { get; set; }
		public string? description { get; set; }
		public bool isEnabled { get; set; }
		public List<CategoryDto> children { get; set; } = new();
	}
}
