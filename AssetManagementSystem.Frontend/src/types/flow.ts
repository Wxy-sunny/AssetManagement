// 资产流转单据类型（与后端 AssetFlowTypes 保持一致）
export const FLOW_TYPE_MAP: Record<string, string> = {
  borrow: '领用',
  return: '归还',
  repair: '报修',
  repair_done: '维修完成',
  scrap: '报废',
}

export const FLOW_TYPE_TAG: Record<string, 'primary' | 'success' | 'info' | 'warning' | 'danger'> = {
  borrow: 'primary',
  return: 'success',
  repair: 'warning',
  repair_done: 'success',
  scrap: 'danger',
}

// 单据处理状态（与后端 AssetFlowStatusCodes 保持一致）
export const FLOW_STATUS_MAP: Record<string, string> = {
  pending: '待处理',
  completed: '已完成',
  rejected: '已驳回',
  cancelled: '已取消',
}

export const FLOW_STATUS_TAG: Record<string, 'info' | 'success' | 'warning' | 'danger'> = {
  pending: 'warning',
  completed: 'success',
  rejected: 'danger',
  cancelled: 'info',
}

// 资产当前状态下可执行的流转动作（与后端状态机保持一致）
export const ALLOWED_FLOW_ACTIONS: Record<string, Array<{ flowType: string; label: string; tip: string }>> = {
  in_use: [
    { flowType: 'return', label: '归还', tip: '资产回到闲置池，清空使用人' },
    { flowType: 'repair', label: '报修', tip: '提交报修单，资产转入维修中' },
    { flowType: 'scrap', label: '报废', tip: '资产进入终态，不可再流转' },
  ],
  idle: [
    { flowType: 'borrow', label: '领用', tip: '登记使用人，资产转为在用' },
    { flowType: 'repair', label: '报修', tip: '提交报修单，资产转入维修中' },
    { flowType: 'scrap', label: '报废', tip: '资产进入终态，不可再流转' },
  ],
  repair: [{ flowType: 'scrap', label: '报废', tip: '维修无望，直接走报废' }],
  scrapped: [],
}

// 流转单据
export interface AssetFlowItem {
  id: string
  flowNo: string
  flowType: string
  flowTypeName?: string
  flowStatus: string
  flowStatusName?: string
  assetId: string
  assetCode?: string | null
  assetName?: string | null
  fromStatus?: string | null
  fromStatusName?: string | null
  toStatus?: string | null
  toStatusName?: string | null
  deptId?: string | null
  deptName?: string | null
  useUserId?: string | null
  useUserName?: string | null
  applyUserName?: string | null
  handleUserName?: string | null
  handleTime?: string | null
  fee?: number | null
  expectReturnDate?: string | null
  handleResult?: string | null
  remark?: string | null
  createTime?: string | null
}

// 流转单据查询条件
export interface AssetFlowQuery {
  keyword?: string
  flowType?: string
  flowStatus?: string
  assetId?: string
  deptId?: string
  startDate?: string
  endDate?: string
  pageIndex?: number
  pageSize?: number
}

// 创建流转单据入参
export interface AssetFlowCreate {
  assetId: string
  flowType: string
  useUserId?: string | null
  useUserName?: string | null
  deptId?: string | null
  deptName?: string | null
  toStatus?: string | null
  fee?: number | null
  expectReturnDate?: string | null
  remark?: string | null
}

// 完成 / 取消单据入参
export interface AssetFlowHandle {
  toStatus?: string | null
  fee?: number | null
  handleResult?: string | null
}
