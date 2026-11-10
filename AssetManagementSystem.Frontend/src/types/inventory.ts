// 盘点单状态
export const INVENTORY_STATUS_MAP: Record<string, string> = {
  draft: '盘点中',
  completed: '已完成',
}

export const INVENTORY_STATUS_TAG: Record<string, 'warning' | 'success'> = {
  draft: 'warning',
  completed: 'success',
}

// 实盘状态（与后端 InventoryActualStatusCodes 保持一致）
export const ACTUAL_STATUS_MAP: Record<string, string> = {
  normal: '正常',
  idle: '闲置',
  repair: '维修中',
  scrapped: '已报废',
  missing: '盘亏',
}

export const ACTUAL_STATUS_OPTIONS = [
  { value: 'normal', label: '正常（与账面一致）' },
  { value: 'idle', label: '闲置' },
  { value: 'repair', label: '维修中' },
  { value: 'scrapped', label: '已报废' },
  { value: 'missing', label: '盘亏（实物缺失）' },
]

// 盘点单
export interface InventoryItem {
  id: string
  inventoryNo: string
  inventoryName: string
  deptId?: string | null
  deptName?: string | null
  inventoryStatus: string
  inventoryStatusName?: string
  planDate?: string | null
  startTime?: string | null
  endTime?: string | null
  ownerName?: string | null
  totalCount?: number | null
  diffCount?: number | null
  checkedCount?: number | null
  totalAmount?: number | null
  remark?: string | null
  createTime?: string | null
}

// 盘点明细
export interface InventoryDetailItem {
  id: string
  inventoryId: string
  assetId: string
  assetCode?: string | null
  assetName?: string | null
  categoryName?: string | null
  deptName?: string | null
  bookLocation?: string | null
  bookUserName?: string | null
  bookStatus?: string | null
  bookStatusName?: string | null
  actualStatus?: string | null
  actualStatusName?: string | null
  actualLocation?: string | null
  actualUserName?: string | null
  bookValue?: number | null
  isDiff?: boolean | null
  diffDesc?: string | null
  checkedUserName?: string | null
  checkedTime?: string | null
  handled?: boolean | null
  handledFlowId?: string | null
}

// 盘点汇总
export interface InventorySummary {
  totalCount: number
  checkedCount: number
  diffCount: number
  missingCount: number
  totalAmount: number
  diffAmount: number
  accuracyRate: number
  statusStats: Array<{ code?: string | null; name?: string | null; value: number; amount?: number | null }>
}

export interface InventoryQuery {
  keyword?: string
  inventoryStatus?: string
  deptId?: string
  startDate?: string
  endDate?: string
  pageIndex?: number
  pageSize?: number
}

export interface InventoryCreate {
  inventoryName?: string
  deptId?: string | null
  deptName?: string | null
  planDate?: string | null
  remark?: string | null
}

export interface InventoryCheck {
  id: string
  actualStatus?: string | null
  actualLocation?: string | null
  actualUserName?: string | null
  diffDesc?: string | null
}
