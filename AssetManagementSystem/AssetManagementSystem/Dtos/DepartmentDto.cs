using System;
using System.Collections.Generic;

namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 部门 DTO（支持树形）
	/// </summary>
	public class DepartmentDto
	{
		public string? id { get; set; }

		/// <summary>部门编码</summary>
		public string? departmentId { get; set; }

		/// <summary>部门名称</summary>
		public string? departmentName { get; set; }

		public string? parentId { get; set; }
		public string? parentName { get; set; }
		public string? leaderName { get; set; }
		public string? phone { get; set; }
		public int? sortOrder { get; set; }
		public string? description { get; set; }
		public bool? isEnabled { get; set; } = true;
		public DateTime? createTime { get; set; }

		public List<DepartmentDto> children { get; set; } = new List<DepartmentDto>();
	}
}
