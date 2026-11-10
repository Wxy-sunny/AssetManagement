import request from '@/utils/request'
import type { PageResult } from '@/types'
import type {
  InventoryItem,
  InventoryDetailItem,
  InventorySummary,
  InventoryQuery,
  InventoryCreate,
  InventoryCheck,
} from '@/types'

// 盘点单分页
export function getInventoryPage(params: InventoryQuery): Promise<PageResult<InventoryItem>> {
  return request
    .get('/inventory/page', { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 10, totalPages: 0 })
}

// 盘点单详情（含差异汇总）
export function getInventoryById(
  id: string,
): Promise<{ inventory: InventoryItem; summary: InventorySummary }> {
  return request.get(`/inventory/${id}`).then((res: any) => res?.data)
}

// 盘点明细分页
export function getInventoryItems(
  id: string,
  params: { onlyDiff?: boolean; pageIndex?: number; pageSize?: number } = {},
): Promise<PageResult<InventoryDetailItem>> {
  return request
    .get(`/inventory/${id}/items`, { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 20, totalPages: 0 })
}

// 创建盘点单（生成账面快照明细）
export function createInventory(data: InventoryCreate): Promise<{ id: string; inventoryNo: string; totalCount: number }> {
  return request.post('/inventory', data).then((res: any) => res?.data)
}

// 批量录入实盘结果
export function checkInventoryItems(
  id: string,
  list: InventoryCheck[],
): Promise<{ checkedCount: number; diffCount: number }> {
  return request.post(`/inventory/${id}/check`, list).then((res: any) => res?.data)
}

// 完成盘点
export function completeInventory(id: string): Promise<InventorySummary> {
  return request.post(`/inventory/${id}/complete`, {}).then((res: any) => res?.data)
}

// 删除盘点单
export function deleteInventory(id: string): Promise<any> {
  return request.delete(`/inventory/${id}`).then((res: any) => res?.data)
}

// 差异转处理单据（与流转闭环打通）
export function handleInventoryDiff(itemId: string, flowType: string): Promise<{ flowId: string; flowNo: string }> {
  return request
    .post(`/inventory/items/${itemId}/handle`, { flowType })
    .then((res: any) => res?.data)
}
