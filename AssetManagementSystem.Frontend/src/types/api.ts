//注意，必须 声明为 export  否则无法引用

// 通用的 API 响应结构
// 后端 ApiResult<T> 属性是 PascalCase，但 ASP.NET Core 默认按 camelCase 序列化，
// 因此实际收到的 JSON 为 { code, message, data }
export interface ApiResult<T = any> {
  code: number | null;
  message: string | null;
  data: T | null;
}

// 分页响应（字段与后端 PageResult<T> 保持一致）
export interface PageResult<T> {
  list: T[];
  total: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
}