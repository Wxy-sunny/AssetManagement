import request from '@/utils/request'
import type { RoleItem, PageResult } from '@/types'

// 角色列表（下拉选择器）
export function getRoleList(keyword?: string): Promise<RoleItem[]> {
  return request.get('/role/list', { params: { keyword } }).then((res: any) => res?.data ?? [])
}

// 角色分页
export function getRolePage(params: { keyword?: string; pageIndex?: number; pageSize?: number }): Promise<PageResult<RoleItem>> {
  return request
    .get('/role/page', { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 10, totalPages: 0 })
}

// 新增角色
export function addRole(data: RoleItem): Promise<any> {
  return request.post('/role', data).then((res: any) => res?.data)
}

// 修改角色
export function updateRole(data: RoleItem): Promise<any> {
  return request.put('/role', data).then((res: any) => res?.data)
}

// 删除角色
export function deleteRole(id: string): Promise<any> {
  return request.delete(`/role/${id}`).then((res: any) => res?.data)
}
