import request from '@/utils/request'
import type { UserItem, UserQuery, PageResult } from '@/types'

// 用户分页查询
export function getUserPage(params: UserQuery): Promise<PageResult<UserItem>> {
  return request
    .get('/user/page', { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 10, totalPages: 0 })
}

// 用户详情
export function getUserById(id: string): Promise<UserItem> {
  return request.get(`/user/${id}`).then((res: any) => res?.data)
}

// 新增用户
export function addUser(data: UserItem): Promise<any> {
  return request.post('/user', data).then((res: any) => res?.data)
}

// 修改用户（password 留空表示不修改密码）
export function updateUser(data: UserItem): Promise<any> {
  return request.put('/user', data).then((res: any) => res?.data)
}

// 删除用户
export function deleteUser(id: string): Promise<any> {
  return request.delete(`/user/${id}`).then((res: any) => res?.data)
}

// 启用 / 禁用用户
export function updateUserEnabled(id: string, isEnabled: boolean): Promise<any> {
  return request.put(`/user/${id}/enabled`, { isEnabled }).then((res: any) => res?.data)
}

// 重置用户密码
export function resetUserPassword(id: string, password: string): Promise<any> {
  return request.put(`/user/${id}/reset-password`, { password }).then((res: any) => res?.data)
}
