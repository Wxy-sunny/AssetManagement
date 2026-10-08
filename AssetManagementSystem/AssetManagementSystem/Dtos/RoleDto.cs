using System;

namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 角色 DTO
	/// </summary>
	public class RoleDto
	{
		public string? id { get; set; }

		/// <summary>角色编码</summary>
		public string? roleId { get; set; }

		/// <summary>角色名称</summary>
		public string? roleName { get; set; }

		public string? description { get; set; }
		public bool? isEnabled { get; set; } = true;

		/// <summary>是否超级管理员：拥有全部权限，无需单独分配</summary>
		public bool? isAdmin { get; set; } = false;

		public DateTime? createTime { get; set; }

		/// <summary>该角色下的用户数（列表展示用）</summary>
		public int? userCount { get; set; }
	}
}
