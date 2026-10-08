// 资产分类（树形节点）
export interface CategoryItem {
  id: string
  categoryCode: string
  categoryName: string
  parentId: string
  sortOrder?: string
  description?: string | null
  isEnabled?: boolean
  children?: CategoryItem[]
}
