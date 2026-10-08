import request from '@/utils/request'
import type { AssetItem, AssetQuery, PageResult } from '@/types'

/**
 * 说明：后端 ApiResult 经序列化后字段为小写（code/message/data），
 * 响应拦截器已返回整个 ApiResult，故此处统一取 res.data 作为业务数据。
 * 失败时拦截器会提示错误并 reject，调用方用 try/catch 处理即可。
 */

// 资产分页查询
export function getAssetPage(params: AssetQuery): Promise<PageResult<AssetItem>> {
  return request
    .get('/asset/page', { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 10, totalPages: 0 })
}

// 资产列表（下拉选择器）
export function getAssetList(keyword?: string): Promise<AssetItem[]> {
  return request.get('/asset/list', { params: { keyword } }).then((res: any) => res?.data ?? [])
}

// 资产详情
export function getAssetById(id: string): Promise<AssetItem> {
  return request.get(`/asset/${id}`).then((res: any) => res?.data)
}

// 新增资产
export function addAsset(data: AssetItem): Promise<any> {
  return request.post('/asset', data).then((res: any) => res?.data)
}

// 修改资产
export function updateAsset(data: AssetItem): Promise<any> {
  return request.put('/asset', data).then((res: any) => res?.data)
}

// 删除资产
export function deleteAsset(id: string): Promise<any> {
  return request.delete(`/asset/${id}`).then((res: any) => res?.data)
}

// 批量删除资产
export function batchDeleteAsset(ids: string[]): Promise<any> {
  return request.post('/asset/batch-delete', ids).then((res: any) => res?.data)
}

// 变更资产状态
export function updateAssetStatus(id: string, status: string): Promise<any> {
  return request.put(`/asset/${id}/status`, { status }).then((res: any) => res?.data)
}

// 重新计算全部资产折旧
export function recalculateDepreciation(): Promise<any> {
  return request.post('/asset/recalculate').then((res: any) => res?.data)
}
