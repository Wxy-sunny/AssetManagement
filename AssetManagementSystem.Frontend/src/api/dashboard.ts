import request from '@/utils/request'
import type { DashboardData, DashboardOverview } from '@/types'

// 首页统计数据
export function getDashboardStats(): Promise<DashboardData> {
  return request.get('/dashboard/stats').then((res: any) => res?.data)
}

// 数据仪表盘（多维 KPI + 图表聚合）
export function getDashboardOverview(): Promise<DashboardOverview> {
  return request.get('/dashboard/overview').then((res: any) => res?.data)
}
