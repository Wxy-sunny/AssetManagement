using AssetManagementSystem.Dtos;

namespace AssetManagementSystem.Utils
{
	/// <summary>
	/// 系统权限定义
	/// 说明：权限项在代码中集中定义，数据库只存"角色拥有哪些权限"（T_SYS_ROLE_PERMISSION），
	/// 这样权限项不会与代码脱节，新增接口时在此补充常量即可。
	/// </summary>
	public static class Permissions
	{
		// ========== 首页 ==========
		public const string DashboardView = "dashboard:view";

		// ========== 信息管理 ==========
		public const string AssetList = "asset:list";
		public const string AssetAdd = "asset:add";
		public const string AssetEdit = "asset:edit";
		public const string AssetDelete = "asset:delete";
		public const string CategoryList = "category:list";
		public const string CategoryManage = "category:manage";

		// ========== 资料管理 ==========
		public const string FileList = "file:list";
		public const string FileUpload = "file:upload";
		public const string FileDelete = "file:delete";

		// ========== 权限管理 ==========
		public const string UserList = "user:list";
		public const string UserManage = "user:manage";
		public const string RoleList = "role:list";
		public const string RoleManage = "role:manage";
		public const string DeptList = "dept:list";
		public const string DeptManage = "dept:manage";

		// ========== 通知公告 ==========
		public const string NotificationList = "notification:list";
		public const string NotificationManage = "notification:manage";

		/// <summary>
		/// 全部权限编码（超级管理员默认拥有）
		/// </summary>
		public static readonly string[] All =
		{
			DashboardView,
			AssetList, AssetAdd, AssetEdit, AssetDelete,
			CategoryList, CategoryManage,
			FileList, FileUpload, FileDelete,
			UserList, UserManage,
			RoleList, RoleManage,
			DeptList, DeptManage,
			NotificationList, NotificationManage
		};

		/// <summary>
		/// 权限分组清单（前端据此渲染权限分配树）
		/// </summary>
		public static List<PermissionGroupDto> GetGroups()
		{
			return new List<PermissionGroupDto>
			{
				new PermissionGroupDto
				{
					code = "dashboard",
					name = "首页",
					permissions = new List<PermissionItemDto>
					{
						New(DashboardView, "查看首页", "查看首页统计图表")
					}
				},
				new PermissionGroupDto
				{
					code = "info",
					name = "信息管理",
					permissions = new List<PermissionItemDto>
					{
						New(AssetList, "查看资产", "浏览资产列表与详情"),
						New(AssetAdd, "新增资产", "登记新资产"),
						New(AssetEdit, "编辑资产", "修改资产信息与折旧参数"),
						New(AssetDelete, "删除资产", "删除资产及其附件"),
						New(CategoryList, "查看分类", "浏览资产分类"),
						New(CategoryManage, "管理分类", "分类的增删改")
					}
				},
				new PermissionGroupDto
				{
					code = "file",
					name = "资料管理",
					permissions = new List<PermissionItemDto>
					{
						New(FileList, "查看资料", "浏览与下载资料"),
						New(FileUpload, "上传资料", "上传附件"),
						New(FileDelete, "删除资料", "删除附件")
					}
				},
				new PermissionGroupDto
				{
					code = "system",
					name = "权限管理",
					permissions = new List<PermissionItemDto>
					{
						New(UserList, "查看用户", "浏览用户列表"),
						New(UserManage, "管理用户", "用户的增删改、启停、重置密码"),
						New(RoleList, "查看角色", "浏览角色列表"),
						New(RoleManage, "管理角色", "角色的增删改与权限分配"),
						New(DeptList, "查看部门", "浏览部门架构"),
						New(DeptManage, "管理部门", "部门的增删改")
					}
				},
				new PermissionGroupDto
				{
					code = "notice",
					name = "通知公告",
					permissions = new List<PermissionItemDto>
					{
						New(NotificationList, "查看通知管理", "查看全部通知（含未发布草稿）"),
						New(NotificationManage, "管理通知", "新增、编辑、发布、撤回、删除通知")
					}
				}
			};
		}

		private static PermissionItemDto New(string code, string name, string? description = null) =>
			new PermissionItemDto { code = code, name = name, description = description };
	}
}
