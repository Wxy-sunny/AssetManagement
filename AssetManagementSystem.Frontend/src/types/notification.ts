// 通知类型字典（与后端 NoticeTypes 保持一致）
export const NOTICE_TYPE_MAP: Record<string, string> = {
  notice: '系统公告',
  update: '更新通知',
  urgent: '紧急通知',
}

// 通知类型对应的 Tag 类型
export const NOTICE_TYPE_TAG: Record<
  string,
  'primary' | 'success' | 'warning' | 'danger' | 'info'
> = {
  notice: 'primary',
  update: 'success',
  urgent: 'danger',
}

// 通知
export interface NotificationItem {
  id?: string
  title: string
  content: string
  noticeType?: string | null
  isPublished?: boolean | null
  publishTime?: string | null
  publisher?: string | null
  createTime?: string | null
  updateTime?: string | null
}

// 通知查询条件
export interface NotificationQuery {
  keyword?: string
  noticeType?: string
  isPublished?: boolean | null
  pageIndex?: number
  pageSize?: number
}
