import request from '@/utils/request'
import type { PageResult } from '@/types'
import type { UserMessageItem, UserMessageQuery } from '@/types'

// 我的消息分页（登录即可）
export function getMyMessages(params: UserMessageQuery): Promise<PageResult<UserMessageItem>> {
  return request
    .get('/message/page', { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 10, totalPages: 0 })
}

// 未读消息数量（菜单角标）
export function getUnreadCount(): Promise<number> {
  return request.get('/message/unread-count').then((res: any) => res?.data ?? 0)
}

// 标记单条已读
export function markMessageRead(id: string): Promise<any> {
  return request.put(`/message/${id}/read`).then((res: any) => res?.data)
}

// 一键全部已读
export function markAllRead(): Promise<any> {
  return request.put('/message/read-all').then((res: any) => res?.data)
}
