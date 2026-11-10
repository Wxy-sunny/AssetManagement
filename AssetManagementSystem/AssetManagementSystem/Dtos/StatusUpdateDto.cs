namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 状态变更入参
	/// </summary>
	public class StatusUpdateDto
	{
		/// <summary>目标状态：in_use / idle / repair / scrapped</summary>
		public string? status { get; set; }

		/// <summary>变更事由（会记入流转流水）</summary>
		public string? remark { get; set; }
	}
}
