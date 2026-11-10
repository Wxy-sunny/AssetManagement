import request from '@/utils/request'
import type { ReportTypeItem, ReportQuery, ReportPreview } from '@/types'

// 可用报表类型
export function getReportTypes(): Promise<ReportTypeItem[]> {
  return request.get('/report/types').then((res: any) => res?.data ?? [])
}

// 报表数据预览
export function getReportData(params: ReportQuery): Promise<ReportPreview> {
  return request
    .get('/report/data', { params })
    .then((res: any) => res?.data ?? { reportType: '', title: '', columns: [], rows: [], total: 0 })
}

/**
 * 导出报表（xlsx / csv）
 * 说明：后端以 FileContentResult 流式返回字节，不落盘；
 * 这里走 blob 分支（拦截器会直接返回 AxiosResponse），再用 a 标签触发下载。
 */
export async function exportReport(params: ReportQuery): Promise<void> {
  const res: any = await request.get('/report/export', {
    params,
    responseType: 'blob',
  })

  const raw = res?.data
  const blob = raw instanceof Blob ? raw : new Blob([raw])
  const url = window.URL.createObjectURL(blob)

  // 优先使用后端返回的文件名（URL 编码过的中文需 decode）
  const disposition: string = res?.headers?.['content-disposition'] ?? ''
  let fileName = `报表_${Date.now()}.${params.format || 'xlsx'}`
  const matched = /filename\*?=(?:UTF-8'')?([^;]+)/i.exec(disposition)
  const rawName = matched?.[1]?.trim().replace(/^"|"$/g, '')
  if (rawName) {
    try {
      fileName = decodeURIComponent(rawName)
    } catch {
      fileName = rawName
    }
  }

  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  window.URL.revokeObjectURL(url)
}
