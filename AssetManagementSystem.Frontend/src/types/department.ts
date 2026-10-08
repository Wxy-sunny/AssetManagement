// 部门（树形节点）
export interface DepartmentItem {
  id?: string
  departmentId?: string | null
  departmentName?: string | null
  parentId?: string | null
  parentName?: string | null
  leaderName?: string | null
  phone?: string | null
  sortOrder?: number | null
  description?: string | null
  isEnabled?: boolean | null
  createTime?: string | null
  children?: DepartmentItem[]
}
