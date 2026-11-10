// 报表类型
export interface ReportTypeItem {
  code: string
  name: string
  description?: string | null
}

// 报表查询条件
export interface ReportQuery {
  reportType?: string
  format?: 'xlsx' | 'csv'
  keyword?: string
  categoryId?: string
  deptId?: string
  assetStatus?: string
  startDate?: string
  endDate?: string
  pageIndex?: number
  pageSize?: number
}

// 报表预览
export interface ReportPreview {
  reportType: string
  title: string
  columns: string[]
  rows: Array<Array<string | number | boolean | null>>
  total: number
}
