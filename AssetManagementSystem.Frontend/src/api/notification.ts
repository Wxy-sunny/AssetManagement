import request from '@/utils/request'
import type { NotificationItem, NotificationQuery, PageResult } from '@/types'

// 通知分页（管理端：包含未发布草稿）
export function getNotificationPage(params: NotificationQuery): Promise<PageResult<NotificationItem>> {
  return request
    .get('/notification/page', { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 10, totalPages: 0 })
}

// 已发布通知分页（用户端：登录即可）
export function getPublishedNotifications(params: {
  keyword?: string
  pageIndex?: number
  pageSize?: number
}): Promise<PageResult<NotificationItem>> {
  return request
    .get('/notification/published', { params })
    .then((res: any) => res?.data ?? { list: [], total: 0, pageIndex: 1, pageSize: 10, totalPages: 0 })
}

// 新增通知（默认草稿）
export function addNotification(data: Partial<NotificationItem>): Promise<any> {
  return request.post('/notification', data).then((res: any) => res?.data)
}

// 修改通知
export function updateNotification(data: Partial<NotificationItem>): Promise<any> {
  return request.put('/notification', data).then((res: any) => res?.data)
}

// 删除通知
export function deleteNotification(id: string): Promise<any> {
  return request.delete(`/notification/${id}`).then((res: any) => res?.data)
}

// 发布 / 撤回通知
export function publishNotification(id: string, isPublished: boolean): Promise<any> {
  return request.put(`/notification/${id}/publish`, { isPublished }).then((res: any) => res?.data)
}
