// 用户
export interface UserItem {
  id?: string
  userId?: string | null
  userName?: string | null
  nickName?: string | null
  // 新增时必填；编辑时留空表示不修改密码
  password?: string
  email?: string | null
  mobile?: string | null
  roleId?: string | null
  roleName?: string | null
  deptId?: string | null
  deptName?: string | null
  isEnabled?: boolean | null
  createTime?: string | null
  lastLoginTime?: string | null
}

// 用户查询条件
export interface UserQuery {
  keyword?: string
  roleId?: string
  deptId?: string
  pageIndex?: number
  pageSize?: number
}
