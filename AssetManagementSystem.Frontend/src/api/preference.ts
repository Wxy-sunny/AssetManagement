import request from '@/utils/request'

// 读取当前用户的个性化配置（未设置时返回 null）
export function getPreference(key: string): Promise<string | null> {
  return request
    .get('/user/preference', { params: { key } })
    .then((res: any) => (res?.data ?? null) as string | null)
}

// 保存当前用户的个性化配置（upsert）
export function savePreference(key: string, value: string): Promise<any> {
  return request.put('/user/preference', { prefKey: key, prefValue: value }).then((res: any) => res)
}
