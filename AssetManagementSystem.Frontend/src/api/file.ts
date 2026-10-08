import request from '@/utils/request'
import type { FileItem, FileQuery, PageResult, CustomAxiosRequestConfig } from '@/types'

// 资料分页查询
export function getFilePage(params: FileQuery): Promise<PageResult<FileItem>> {
  return request
    .get('/file/page', { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 10, totalPages: 0 })
}

/**
 * 上传单个文件（multipart/form-data）
 * 后端字段名不固定，用 form.Files 读取，此处以 file 字段名提交即可
 */
// 后端返回的是本次上传成功的文件列表（数组），不是单个对象
export function uploadFile(
  file: File,
  bizType?: string,
  assetId?: string,
): Promise<Array<{ id: string; fileName: string; filePath: string }>> {
  const formData = new FormData()
  formData.append('file', file)
  if (bizType) formData.append('bizType', bizType)
  if (assetId) formData.append('assetId', assetId)

  // 说明两点：
  // 1. 不手动设置 Content-Type：交给浏览器自动填充 multipart/form-data 并附带 boundary，
  //    否则服务端无法解析分界，ReadFormAsync 读不到文件。
  // 2. repeatable=true 关闭请求去重：FormData 的 JSON.stringify 结果恒为 {}，
  //    若参与去重则并发上传会共用同一个 key，后一个请求会 abort 掉前一个。
  return request
    .post('/file/upload', formData, { repeatable: true } as CustomAxiosRequestConfig)
    .then((res: any) => res?.data)
}

/**
 * 下载文件：以 blob 方式请求，保留 Authorization 请求头
 * （拦截器对 blob 响应会直接返回 AxiosResponse）
 */
export async function downloadFile(id: string, fileName?: string): Promise<void> {
  const res: any = await request.get(`/file/download/${id}`, { responseType: 'blob' })
  const raw = res?.data
  const blob = raw instanceof Blob ? raw : new Blob([raw])
  const url = window.URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName || 'download'
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  window.URL.revokeObjectURL(url)
}

// 在线预览地址（图片等静态资源，走 /uploads 代理）
export function getPreviewUrl(file: FileItem): string {
  return file?.filePath || ''
}

// 删除文件
export function deleteFile(id: string): Promise<any> {
  return request.delete(`/file/${id}`).then((res: any) => res?.data)
}

// 批量删除文件
export function batchDeleteFile(ids: string[]): Promise<any> {
  return request.post('/file/batch-delete', ids).then((res: any) => res?.data)
}
