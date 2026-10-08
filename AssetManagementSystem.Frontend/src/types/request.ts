import type { InternalAxiosRequestConfig } from 'axios'


//注意，必须 声明为 export  否则无法引用
// --- 扩展 Axios 类型，用于标记是否允许重复请求 ---
export  interface CustomAxiosRequestConfig extends InternalAxiosRequestConfig {
  repeatable?: boolean; // 是否允许重复请求，默认 false 不允许
}

//注意，必须 声明为 export  否则无法引用