import axios from 'axios'
import type { AxiosError, InternalAxiosRequestConfig, AxiosResponse } from 'axios'
import { ElMessage } from 'element-plus'; 
import type { ApiResult,CustomAxiosRequestConfig } from '@/types';


// --- 维护一个“请求池”，用于取消重复请求 ---
const pendingMap = new Map<string, AbortController>();

//生成请求的唯一标识（根据 Method + URL + 参数）
const getPendingKey = (config: InternalAxiosRequestConfig) => {
  const { method, url, params, data } = config;
  return [method, url, JSON.stringify(params), JSON.stringify(data)].join('&');
};


// 1. 创建实例
// 注意：不要在这里设置全局 Content-Type: application/json。
// axios v1 在"全局 JSON 头 + FormData 数据"时会把 FormData 字符串化成 {}，
// 导致服务端 ReadFormAsync 报 Incorrect Content-Type（文件上传 500）。
// 改为在请求拦截器中按请求体类型动态设置。
const request = axios.create({
  baseURL: '/api', 
  timeout: 15000, // 15秒超时,不要设置太短了，之前设置成15，导致永远超时
});

// 2. 请求拦截器
request.interceptors.request.use(
  (config: CustomAxiosRequestConfig) => {
    // --- 处理重复请求（除非明确允许重复） ---
    if (!config.repeatable) {
      const key = getPendingKey(config);
      // 如果已存在请求，则取消上一次请求
      if (pendingMap.has(key)) {
        pendingMap.get(key)?.abort();
        pendingMap.delete(key);
      }
      // 创建新的 AbortController 并保存
      const controller = new AbortController();
      config.signal = controller.signal;
      pendingMap.set(key, controller);
    }

    // --- 注入 Token (跳过登录接口) ---
    const token = localStorage.getItem('token');
    if (token && !config.url?.includes('/login')) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    // --- 按请求体类型设置 Content-Type ---
    // FormData（文件上传）：不设置，由浏览器自动生成 multipart/form-data 及 boundary；
    // 其余带 body 的请求：按 JSON 提交。
    if (config.data instanceof FormData) {
      const h = config.headers as any
      if (typeof h?.delete === 'function') h.delete('Content-Type')
      else delete h?.['Content-Type']
    } else if (config.data !== undefined && config.data !== null) {
      config.headers['Content-Type'] = 'application/json'
    }

    // 防止 GET 请求缓存
    if (config.method === 'get') {
      config.params = { ...config.params, _t: Date.now() };
    }

    return config;
  },
  (error) => Promise.reject(error)
);


// ============ Token 无感刷新（访问令牌过期时自动续期） ============
let isRefreshing = false
// 刷新期间到达的 401 请求排队等待，刷新成功后统一重放
let refreshWaiters: Array<(token: string) => void> = []

// 调用刷新接口：成功则更新本地令牌并返回新的访问令牌；失败返回 null
const tryRefreshToken = async (): Promise<string | null> => {
  const refreshToken = localStorage.getItem('refreshToken')
  if (!refreshToken) return null
  try {
    // 用原生 axios 发起，避免被本文件的拦截器递归处理
    const resp = await axios.post('/api/auth/refresh', { refreshToken })
    const data = resp.data
    if (data?.code === 200 && data.data?.token) {
      localStorage.setItem('token', data.data.token)
      if (data.data.refreshToken) {
        localStorage.setItem('refreshToken', data.data.refreshToken)
      }
      if (data.data.userInfo) {
        localStorage.setItem('userInfo', JSON.stringify(data.data.userInfo))
      }
      return data.data.token
    }
  } catch {
    // 刷新失败（刷新令牌过期 / 已被顶掉 / 网络异常）
  }
  return null
}

// 跳转登录页并清理登录态
const goLogin = (msg = '登录状态已过期，请重新登录') => {
  ElMessage.error(msg)
  localStorage.clear()
  if (!window.location.pathname.includes('/login')) {
    window.location.href = '/login'
  }
}

// 统一 401 处理：先尝试刷新令牌并重放原请求；无法刷新则回登录页
const handleUnauthorized = async (originalConfig: InternalAxiosRequestConfig): Promise<any> => {
  const cfg = originalConfig as CustomAxiosRequestConfig & { _retry?: boolean }
  // 已重试过一次仍 401，说明刷新也没用，直接回登录页
  if (cfg._retry) {
    goLogin()
    return Promise.reject(new Error('Unauthorized'))
  }
  cfg._retry = true

  // 正在刷新中：排队等待，刷新成功后用新令牌重放
  if (isRefreshing) {
    return new Promise((resolve, reject) => {
      refreshWaiters.push((token) => {
        cfg.headers = cfg.headers || {}
        ;(cfg.headers as any).Authorization = `Bearer ${token}`
        request(cfg).then(resolve).catch(reject)
      })
    })
  }

  isRefreshing = true
  const newToken = await tryRefreshToken()
  isRefreshing = false

  if (newToken) {
    refreshWaiters.forEach((cb) => cb(newToken))
    refreshWaiters = []
    cfg.headers = cfg.headers || {}
    ;(cfg.headers as any).Authorization = `Bearer ${newToken}`
    return request(cfg)
  }

  refreshWaiters = []
  goLogin()
  return Promise.reject(new Error('Unauthorized'))
}

// 3. 响应拦截器
request.interceptors.response.use(
  (response: AxiosResponse) => {
    // --- 请求完成，从请求池中移除 ---
    const key = getPendingKey(response.config);
    if (pendingMap.has(key)) {
      pendingMap.delete(key);
    }

    // --- 文件下载等二进制响应：直接返回原始响应，不做 ApiResult 解析 ---
    const responseType = response.config?.responseType;
    if (responseType === 'blob' || responseType === 'arraybuffer') {
      return response;
    }

    //const { Code, Data, Message } = response.data;
    // --- 统一处理后端数据结构 
    const apiResult = response.data;
    const code = apiResult.Code ?? apiResult.code;
    // 后端 ApiResult 属性为 PascalCase，但默认序列化为 camelCase，两种命名都要兼容
    const message = apiResult.Message ?? apiResult.message;
    if (code === 0 || code === 200) {
      return apiResult; 
    } 
    else if (code === 401 || code === '401') {
      // --- 业务 401（如"登录信息无效"）：尝试刷新令牌并重放，失败再回登录页 ---
      return handleUnauthorized(response.config);
    } else {
      // 普通业务错误（如 400 参数错误）
      // 把业务码挂到 Error 上，便于调用方针对不同 code 做分支处理
      // （例如手机号登录返回 404 表示未绑定账户，需要引导用户先注册）
      const err = new Error(message || '请求失败') as Error & { code?: number | null };
      err.code = code;
      ElMessage.error(message || '请求失败');
      return Promise.reject(err);
    }
  },
  (error: AxiosError) => {
    // --- 网络/超时/手动取消的错误处理 ---
    const key = getPendingKey(error.config as InternalAxiosRequestConfig);
    if (pendingMap.has(key)) {
      pendingMap.delete(key);
    }

    // 如果是用户主动取消（AbortError），不报错
    if (axios.isCancel(error) || error.code === 'ERR_CANCELED') {
      return Promise.reject(error);
    }

    // HTTP 401：Token 失效（过期 / 无效）——先尝试无感刷新并重放，失败再回登录页
    if (error.response?.status === 401) {
      if (error.config) {
        return handleUnauthorized(error.config as InternalAxiosRequestConfig);
      }
      goLogin()
      return Promise.reject(error);
    }

    // HTTP 403：已登录但没有该功能的权限——只提示，不清除登录态（与 401 区分）
    if (error.response?.status === 403) {
      const data: any = error.response?.data;
      const msg = data?.message ?? data?.Message ?? '没有该功能的操作权限';
      ElMessage.error(msg);
      return Promise.reject(error);
    }

    if (error.code === 'ECONNABORTED' || error.message.includes('timeout')) {
      ElMessage.error('请求超时，请稍后重试');
    } else if (!window.navigator.onLine) {
      ElMessage.error('网络连接异常，请检查网络');
    } else {
      ElMessage.error(error.message || '服务端异常');
    }

    return Promise.reject(error);
  }
);

//一定要到导出啊！！
export default request;
//使用方式 import request from '@/utils/request';