using SqlSugar;

namespace AssetManagementSystem.Models
{
	public class BaseModel
	{
		/// <summary>
		/// Id全局唯一
		/// </summary>
		[SugarColumn(IsPrimaryKey = true, ColumnName = "id",Length =50)]
		public string Id { get; set; } = Guid.NewGuid().ToString();
		[SugarColumn(ColumnName = "create_user_id", IsNullable = true,Length =50)]
		public string? CreateUserId { get; set; }

		[SugarColumn(ColumnName ="create_time", IsNullable = true)]
		public DateTime? CreateTime {  get; set; }
		[SugarColumn(ColumnName = "update_user_id", IsNullable = true,Length =50)]
		public string? UpdateUserId { get; set; }

		[SugarColumn(ColumnName = "update_time", IsNullable = true)]
		public DateTime? UpdateTime { get; set; }

		/// <summary>
		/// 是否启用（1启用，0禁用）
		/// </summary>
		[SugarColumn(ColumnName = "is_enabled", IsNullable = true)]
		public bool? IsEnabled { get; set; } = true;

		[SugarColumn(ColumnName = "status_code",IsNullable =true,Length =50)]
		public short? StatusCode {  get; set; }

		[SugarColumn(ColumnName = "status_desc", IsNullable = true,Length =255)]
		public string? StatusDesc { get; set; }

		[SugarColumn(ColumnName = "remark", IsNullable = true, Length = 255)]
		public string? Remark { get; set; }
	}
}
