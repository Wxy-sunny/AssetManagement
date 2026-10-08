// 通用统计项
export interface StatItem {
  name?: string | null
  code?: string | null
  value: number
  amount?: number | null
}

// 趋势项
export interface TrendItem {
  date?: string | null
  count: number
  amount?: number | null
}

// 首页统计聚合
export interface DashboardData {
  assetTotal: number
  assetQuantity?: number | null
  originalValue?: number | null
  accumulatedDepreciation?: number | null
  netValue?: number | null
  monthNewCount: number
  statusStats: StatItem[]
  categoryStats: StatItem[]
  trend: TrendItem[]
}
