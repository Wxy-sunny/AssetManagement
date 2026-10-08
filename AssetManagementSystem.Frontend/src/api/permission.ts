import request from '@/utils/request'
import type { PermissionGroup } from '@/types'

// 权限树（按模块分组，用于角色分配）
export function getPermissionTree(): Promise<PermissionGroup[]> {
  return request.get('/permission/tree').then((res: any) => res?.data ?? [])
}

// 查询某角色已分配的权限编码
export function getRolePermissions(roleId: string): Promise<string[]> {
  return request.get(`/permission/role/${roleId}`).then((res: any) => res?.data ?? [])
}

// 给角色分配权限（全量覆盖）
export function assignPermissions(roleId: string, permissionCodes: string[]): Promise<any> {
  return request.post('/permission/assign', { roleId, permissionCodes }).then((res: any) => res?.data)
}
