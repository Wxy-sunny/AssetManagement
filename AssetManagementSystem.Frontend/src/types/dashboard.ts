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

// 数据仪表盘聚合（与后端 DashboardOverviewDto 保持一致）
export interface DashboardOverview {
  assetTotal: number
  assetQuantity: number
  originalValue: number
  accumulatedDepreciation: number
  netValue: number
  monthNewCount: number
  inUseCount: number
  idleCount: number
  repairCount: number
  scrappedCount: number
  utilizationRate: number
  idleRate: number
  repairRate: number
  scrapRate: number
  depreciationRate: number
  statusStats: StatItem[]
  categoryStats: StatItem[]
  deptStats: StatItem[]
  depreciationMethodStats: StatItem[]
  monthlyTrend: TrendItem[]
  flowTypeStats: StatItem[]
  flowPendingCount: number
  inventoryStats: StatItem[]
  inventoryAccuracyRate: number
}
