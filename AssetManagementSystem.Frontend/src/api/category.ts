import request from '@/utils/request'
import type { CategoryItem } from '@/types'

// 分类树（可按关键字过滤编码/名称）
export function getCategoryTree(keyword?: string): Promise<CategoryItem[]> {
  return request.get('/category/tree', { params: keyword ? { keyword } : undefined }).then((res: any) => res?.data ?? [])
}

// 分类平铺列表
export function getCategoryList(keyword?: string): Promise<CategoryItem[]> {
  return request.get('/category/list', { params: { keyword } }).then((res: any) => res?.data ?? [])
}

// 新增分类
export function addCategory(data: Partial<CategoryItem>): Promise<any> {
  return request.post('/category', data).then((res: any) => res?.data)
}

// 修改分类
export function updateCategory(data: Partial<CategoryItem>): Promise<any> {
  return request.put('/category', data).then((res: any) => res?.data)
}

// 删除分类
export function deleteCategory(id: string): Promise<any> {
  return request.delete(`/category/${id}`).then((res: any) => res?.data)
}
