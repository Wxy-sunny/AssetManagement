using System;

namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 资产 DTO（字段命名与前端保持一致 camelCase）
	/// </summary>
	public class AssetDto
	{
		public string? id { get; set; }
		public string assetCode { get; set; } = string.Empty;
		public string assetName { get; set; } = string.Empty;
		public string? categoryId { get; set; }
		public string? categoryName { get; set; }
		public string? spec { get; set; }
		public string? unit { get; set; }
		public decimal? quantity { get; set; } = 1;
		public string? supplier { get; set; }
		public decimal? unitPrice { get; set; }
		public decimal? originalValue { get; set; }
		public DateTime? purchaseDate { get; set; }
		public string? deptId { get; set; }
		public string? deptName { get; set; }
		public string? useUserId { get; set; }
		public string? useUserName { get; set; }
		public string? location { get; set; }
		public string? assetStatus { get; set; }

		// 折旧相关
		public string? depreciationMethod { get; set; }
		public int? usefulLifeMonths { get; set; }
		public decimal? salvageRate { get; set; }
		public decimal? salvageValue { get; set; }
		public decimal? monthlyDepreciation { get; set; }
		public DateTime? depreciationStartDate { get; set; }
		public int? usedMonths { get; set; }
		public decimal? accumulatedDepreciation { get; set; }
		public decimal? netValue { get; set; }

		public bool? isEnabled { get; set; } = true;
		public string? remark { get; set; }
		public DateTime? createTime { get; set; }
		public DateTime? updateTime { get; set; }
	}
}
