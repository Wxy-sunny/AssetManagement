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
const request = axios.create({
  baseURL: '/api', 
  timeout: 15000, // 15秒超时,不要设置太短了，之前设置成15，导致永远超时
  headers: {
    'Content-Type': 'application/json' 
  }
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

    // 防止 GET 请求缓存
    if (config.method === 'get') {
      config.params = { ...config.params, _t: Date.now() };
    }

    return config;
  },
  (error) => Promise.reject(error)
);


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
    else if (code === 401) {
      // --- Token 失效处理 (防死循环) ---
      ElMessage.error(message || '登录已过期，请重新登录');
      //localStorage.removeItem('token');
      localStorage.clear();
      // 跳转到登录页（如果不在登录页才跳，避免死循环）
      if (!window.location.pathname.includes('/login')) {
        window.location.href = '/login';
      }
      return Promise.reject(new Error('Unauthorized'));
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

    // HTTP 401：Token 失效（过期 / 无效），清除登录态并跳转登录页
    if (error.response?.status === 401) {
      ElMessage.error('登录已过期，请重新登录');
      localStorage.clear();
      if (!window.location.pathname.includes('/login')) {
        window.location.href = '/login';
      }
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