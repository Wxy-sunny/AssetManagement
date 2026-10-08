namespace AssetManagementSystem.Dtos
{
	/// <summary>
	/// 单个权限项
	/// </summary>
	public class PermissionItemDto
	{
		/// <summary>权限编码，如 asset:add</summary>
		public string code { get; set; } = string.Empty;

		/// <summary>权限名称</summary>
		public string name { get; set; } = string.Empty;

		public string? description { get; set; }
	}

	/// <summary>
	/// 权限分组（前端据此渲染权限树）
	/// </summary>
	public class PermissionGroupDto
	{
		/// <summary>分组编码</summary>
		public string code { get; set; } = string.Empty;

		/// <summary>分组名称</summary>
		public string name { get; set; } = string.Empty;

		public List<PermissionItemDto> permissions { get; set; } = new List<PermissionItemDto>();
	}

	/// <summary>
	/// 给角色分配权限的入参
	/// </summary>
	public class AssignPermissionDto
	{
		/// <summary>角色编码（SysRole.RoleId）</summary>
		public string? roleId { get; set; }

		/// <summary>选中的权限编码集合，传空表示清空该角色所有权限</summary>
		public List<string>? permissionCodes { get; set; }
	}
}
