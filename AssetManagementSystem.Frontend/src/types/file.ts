// 资料/附件
export interface FileItem {
  id?: string
  fileName?: string | null
  savedName?: string | null
  filePath?: string | null
  fileExt?: string | null
  fileSize?: number | null
  fileSizeText?: string | null
  contentType?: string | null
  bizType?: string | null
  assetId?: string | null
  assetName?: string | null
  uploadUserName?: string | null
  createTime?: string | null
}

// 资料业务类型字典（与后端 FileBizTypes 保持一致）
export const FILE_BIZ_TYPE_MAP: Record<string, string> = {
  asset: '资产资料',
  common: '公共资料',
  contract: '合同文档',
  manual: '操作手册',
}

// 资料查询条件
export interface FileQuery {
  keyword?: string
  bizType?: string
  assetId?: string
  pageIndex?: number
  pageSize?: number
}
