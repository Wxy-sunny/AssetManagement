import request from '@/utils/request'
import type { PageResult } from '@/types'
import type { AssetFlowItem, AssetFlowQuery, AssetFlowCreate, AssetFlowHandle } from '@/types'

// 流转单据分页
export function getAssetFlowPage(params: AssetFlowQuery): Promise<PageResult<AssetFlowItem>> {
  return request
    .get('/asset-flow/page', { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 10, totalPages: 0 })
}

// 单据详情
export function getAssetFlowById(id: string): Promise<AssetFlowItem> {
  return request.get(`/asset-flow/${id}`).then((res: any) => res?.data)
}

// 某资产当前可执行的流转动作
export function getAllowedFlowActions(
  assetId: string,
): Promise<Array<{ flowType: string; label: string; toStatus?: string | null; description?: string | null }>> {
  return request
    .get('/asset-flow/allowed-actions', { params: { assetId } })
    .then((res: any) => res?.data ?? [])
}

// 办理流转单据（领用 / 归还 / 报修 / 报废）
export function createAssetFlow(data: AssetFlowCreate): Promise<AssetFlowItem> {
  return request.post('/asset-flow', data).then((res: any) => res?.data)
}

// 完成单据（维修完成）
export function completeAssetFlow(id: string, data: AssetFlowHandle): Promise<AssetFlowItem> {
  return request.post(`/asset-flow/${id}/complete`, data).then((res: any) => res?.data)
}

// 取消单据（资产状态回退）
export function cancelAssetFlow(id: string, data: AssetFlowHandle): Promise<any> {
  return request.post(`/asset-flow/${id}/cancel`, data).then((res: any) => res?.data)
}

// 驳回单据（资产状态回退）
export function rejectAssetFlow(id: string, data: AssetFlowHandle): Promise<any> {
  return request.post(`/asset-flow/${id}/reject`, data).then((res: any) => res?.data)
}
