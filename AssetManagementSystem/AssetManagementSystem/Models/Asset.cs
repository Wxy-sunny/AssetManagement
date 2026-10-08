using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Models
{
	public static class DepreciationMethods
	{
		public const string None = "none";
		public const string StraightLine = "straight-line";
		public const string UnitsOfProduction = "units-of-production";
		public const string DoubleDeclining = "double-declining";
		public const string SumOfYears = "sum-of-years";
	}

	/// <summary>
	/// 资产状态常量（避免与 Asset.AssetStatus 属性同名冲突，故加 Codes 后缀）
	/// </summary>
	public static class AssetStatusCodes
	{
		public const string InUse = "in_use";
		public const string Idle = "idle";
		public const string Repair = "repair";
		public const string Scrapped = "scrapped";
	}

	[SugarTable("T_ASSET")]
	[DbBelong(DbNames.Asset)]
	public class Asset : BaseModel
	{
		[SugarColumn(ColumnName = "asset_code", IsNullable = false, Length = 50)]
		public string AssetCode { get; set; }

		[SugarColumn(ColumnName = "asset_name", IsNullable = false, Length = 255)]
		public string AssetName { get; set; }

		[SugarColumn(ColumnName = "category_id", IsNullable = true, Length = 50)]
		public string? CategoryId { get; set; }

		[SugarColumn(ColumnName = "category_name", IsNullable = true, Length = 255)]
		public string? CategoryName { get; set; }

		[SugarColumn(ColumnName = "spec", IsNullable = true, Length = 255)]
		public string? Spec { get; set; }

		[SugarColumn(ColumnName = "unit", IsNullable = true, Length = 20)]
		public string? Unit { get; set; }

		[SugarColumn(ColumnName = "quantity", IsNullable = true, DecimalDigits = 2)]
		public decimal? Quantity { get; set; } = 1;

		[SugarColumn(ColumnName = "supplier", IsNullable = true, Length = 255)]
		public string? Supplier { get; set; }

		[SugarColumn(ColumnName = "unit_price", IsNullable = true, DecimalDigits = 2)]
		public decimal? UnitPrice { get; set; }

		[SugarColumn(ColumnName = "original_value", IsNullable = true, DecimalDigits = 2)]
		public decimal? OriginalValue { get; set; }

		[SugarColumn(ColumnName = "purchase_date", IsNullable = true)]
		public DateTime? PurchaseDate { get; set; }

		[SugarColumn(ColumnName = "dept_id", IsNullable = true, Length = 50)]
		public string? DeptId { get; set; }

		[SugarColumn(ColumnName = "dept_name", IsNullable = true, Length = 255)]
		public string? DeptName { get; set; }

		[SugarColumn(ColumnName = "use_user_id", IsNullable = true, Length = 50)]
		public string? UseUserId { get; set; }

		[SugarColumn(ColumnName = "use_user_name", IsNullable = true, Length = 255)]
		public string? UseUserName { get; set; }

		[SugarColumn(ColumnName = "location", IsNullable = true, Length = 255)]
		public string? Location { get; set; }

		[SugarColumn(ColumnName = "asset_status", IsNullable = true, Length = 20)]
		public string? AssetStatus { get; set; } = AssetStatusCodes.InUse;

		[SugarColumn(ColumnName = "depreciation_method", IsNullable = true, Length = 30)]
		public string? DepreciationMethod { get; set; } = DepreciationMethods.StraightLine;

		[SugarColumn(ColumnName = "useful_life_months", IsNullable = true)]
		public int? UsefulLifeMonths { get; set; } = 60;

		[SugarColumn(ColumnName = "salvage_rate", IsNullable = true, DecimalDigits = 2)]
		public decimal? SalvageRate { get; set; } = 5;

		[SugarColumn(ColumnName = "salvage_value", IsNullable = true, DecimalDigits = 2)]
		public decimal? SalvageValue { get; set; }

		[SugarColumn(ColumnName = "monthly_depreciation", IsNullable = true, DecimalDigits = 2)]
		public decimal? MonthlyDepreciation { get; set; }

		[SugarColumn(ColumnName = "depreciation_start_date", IsNullable = true)]
		public DateTime? DepreciationStartDate { get; set; }

		[SugarColumn(ColumnName = "used_months", IsNullable = true)]
		public int? UsedMonths { get; set; }

		[SugarColumn(ColumnName = "accumulated_depreciation", IsNullable = true, DecimalDigits = 2)]
		public decimal? AccumulatedDepreciation { get; set; }

		[SugarColumn(ColumnName = "net_value", IsNullable = true, DecimalDigits = 2)]
		public decimal? NetValue { get; set; }
	}
}
