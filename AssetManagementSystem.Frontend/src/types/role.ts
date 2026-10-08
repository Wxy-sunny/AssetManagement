// 角色
export interface RoleItem {
  id?: string
  roleId?: string | null
  roleName?: string | null
  description?: string | null
  isEnabled?: boolean | null
  // 是否超级管理员：拥有全部权限，无需单独分配
  isAdmin?: boolean | null
  createTime?: string | null
  // 该角色下的用户数（列表展示用）
  userCount?: number | null
}
