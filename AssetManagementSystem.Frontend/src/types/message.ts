// 个人消息类型字典（与后端 UserMessageTypes 保持一致）
export const MSG_TYPE_MAP: Record<string, string> = {
  request_reply: '请求回执',
  system: '系统消息',
}

// 个人消息项（用户收件箱）
export interface UserMessageItem {
  id?: string
  title?: string
  content?: string
  msgType?: string | null
  isRead?: boolean | null
  readTime?: string | null
  sourceId?: string | null
  createTime?: string | null
}

// 个人消息查询条件
export interface UserMessageQuery {
  keyword?: string
  readStatus?: string // unread 仅看未读
  pageIndex?: number
  pageSize?: number
}
