// 权限项
export interface PermissionItem {
  code: string
  name: string
  description?: string | null
}

// 权限分组（后端按模块分组返回）
export interface PermissionGroup {
  code: string
  name: string
  permissions: PermissionItem[]
}

/**
 * 权限编码常量，需与后端 Permissions 定义保持一致
 * 命名规则：模块:操作
 */
export const PERMISSIONS = {
  DashboardView: 'dashboard:view',

  AssetList: 'asset:list',
  AssetAdd: 'asset:add',
  AssetEdit: 'asset:edit',
  AssetDelete: 'asset:delete',
  CategoryList: 'category:list',
  CategoryManage: 'category:manage',

  FileList: 'file:list',
  FileUpload: 'file:upload',
  FileDelete: 'file:delete',

  UserList: 'user:list',
  UserManage: 'user:manage',
  RoleList: 'role:list',
  RoleManage: 'role:manage',
  DeptList: 'dept:list',
  DeptManage: 'dept:manage',

  NotificationList: 'notification:list',
  NotificationManage: 'notification:manage',
} as const
