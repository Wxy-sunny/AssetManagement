import request from '@/utils/request'
import type { DashboardData } from '@/types'

// 首页统计数据
export function getDashboardStats(): Promise<DashboardData> {
  return request.get('/dashboard/stats').then((res: any) => res?.data)
}
