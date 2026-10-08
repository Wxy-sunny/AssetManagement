// 资产状态字典（与后端 AssetStatusCodes 保持一致）
export const ASSET_STATUS_MAP: Record<string, string> = {
  in_use: '在用',
  idle: '闲置',
  repair: '维修中',
  scrapped: '已报废',
}

// 资产状态对应的 Element Plus Tag 类型
export const ASSET_STATUS_TAG: Record<string, 'success' | 'info' | 'warning' | 'danger'> = {
  in_use: 'success',
  idle: 'info',
  repair: 'warning',
  scrapped: 'danger',
}

// 折旧方式字典（与后端 DepreciationMethods 保持一致）
export const DEPRECIATION_METHOD_MAP: Record<string, string> = {
  none: '不计提折旧',
  'straight-line': '平均年限法',
  'units-of-production': '工作量法',
  'double-declining': '双倍余额递减法',
  'sum-of-years': '年数总和法',
}

// 资产
export interface AssetItem {
  id?: string
  assetCode: string
  assetName: string
  categoryId?: string | null
  categoryName?: string | null
  spec?: string | null
  unit?: string | null
  quantity?: number | null
  supplier?: string | null
  unitPrice?: number | null
  originalValue?: number | null
  purchaseDate?: string | null
  deptId?: string | null
  deptName?: string | null
  useUserId?: string | null
  useUserName?: string | null
  location?: string | null
  assetStatus?: string | null
  // 折旧相关
  depreciationMethod?: string | null
  usefulLifeMonths?: number | null
  salvageRate?: number | null
  salvageValue?: number | null
  monthlyDepreciation?: number | null
  depreciationStartDate?: string | null
  usedMonths?: number | null
  accumulatedDepreciation?: number | null
  netValue?: number | null
  isEnabled?: boolean | null
  remark?: string | null
  createTime?: string | null
  updateTime?: string | null
}

// 资产查询条件
export interface AssetQuery {
  keyword?: string
  categoryId?: string
  assetStatus?: string
  deptId?: string
  purchaseDateStart?: string
  purchaseDateEnd?: string
  pageIndex?: number
  pageSize?: number
}
