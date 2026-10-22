import request from '@/utils/request'
import type { PageResult } from '@/types'

// 请求类型字典（与后端 RequestTypes 保持一致）
export const REQUEST_TYPE_MAP: Record<string, string> = {
  reset_password: '重置密码',
  other: '其他',
}

// 请求状态字典（与后端 RequestStatus 保持一致）
export const REQUEST_STATUS_MAP: Record<string, string> = {
  pending: '待处理',
  handled: '已处理',
}

// 联系请求项
export interface ContactRequestItem {
  id?: string
  account?: string | null
  mobile?: string | null
  requestType?: string | null
  content?: string | null
  status?: string | null
  senderId?: string | null
  handlerName?: string | null
  handleTime?: string | null
  handleRemark?: string | null
  createTime?: string | null
  updateTime?: string | null
}

// 联系请求查询条件
export interface ContactRequestQuery {
  keyword?: string
  status?: string
  pageIndex?: number
  pageSize?: number
}

// 公开提交联系请求（登录页"联系管理员"）
export function submitContactRequest(data: {
  account?: string
  mobile?: string
  requestType?: string
  content?: string
  captchaId: string
  captcha: string
  senderId?: string
}): Promise<any> {
  return request.post('/request/submit', data).then((res: any) => res?.data)
}

// 管理员：分页查询联系请求
export function getRequestPage(params: ContactRequestQuery): Promise<PageResult<ContactRequestItem>> {
  return request
    .get('/request/page', { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 10, totalPages: 0 })
}

// 管理员：待处理数量（菜单角标）
export function getRequestPendingCount(): Promise<number> {
  return request.get('/request/pending-count').then((res: any) => res?.data ?? 0)
}

// 管理员：标记已处理（remark 处理备注；notifySms 是否同时发送短信，默认 true）
export function handleRequest(
  id: string,
  remark?: string,
  notifySms: boolean = true,
): Promise<any> {
  return request
    .put(`/request/${id}/handle`, { remark, notifySms })
    .then((res: any) => res?.data)
}
