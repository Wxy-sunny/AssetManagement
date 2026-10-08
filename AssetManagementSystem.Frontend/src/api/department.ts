import request from '@/utils/request'
import type { DepartmentItem } from '@/types'

// 部门树
export function getDeptTree(keyword?: string): Promise<DepartmentItem[]> {
  return request.get('/dept/tree', { params: { keyword } }).then((res: any) => res?.data ?? [])
}

// 部门平铺列表（下拉选择器）
export function getDeptList(keyword?: string): Promise<DepartmentItem[]> {
  return request.get('/dept/list', { params: { keyword } }).then((res: any) => res?.data ?? [])
}

// 部门详情
export function getDeptById(id: string): Promise<DepartmentItem> {
  return request.get(`/dept/${id}`).then((res: any) => res?.data)
}

// 新增部门
export function addDept(data: Partial<DepartmentItem>): Promise<any> {
  return request.post('/dept', data).then((res: any) => res?.data)
}

// 修改部门
export function updateDept(data: Partial<DepartmentItem>): Promise<any> {
  return request.put('/dept', data).then((res: any) => res?.data)
}

// 删除部门
export function deleteDept(id: string): Promise<any> {
  return request.delete(`/dept/${id}`).then((res: any) => res?.data)
}
