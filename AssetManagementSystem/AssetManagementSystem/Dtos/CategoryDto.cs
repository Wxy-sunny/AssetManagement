namespace AssetManagementSystem.Dtos
{
	public class CategoryDto
	{
		public string id { get; set; }
		public string categoryCode { get; set; } = string.Empty;
		public string categoryName { get; set; } = string.Empty;
		public string parentId { get; set; }
		public string  sortOrder { get; set; }
		public string? description { get; set; }
		public bool isEnabled { get; set; }
		public List<CategoryDto> children { get; set; } = new();
	}
}
