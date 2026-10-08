import request from '@/utils/request'

export interface LoginResult {
  token: string
  userInfo: any
}

export interface RegisterPayload {
  userId: string
  userName?: string
  password: string
  confirmPassword: string
  mobile: string
  email?: string
}

// 账号密码登录
export function login(username: string, password: string): Promise<LoginResult> {
  return request.post('/auth/login', { username, password }).then((res: any) => res?.data)
}

// 注册账号（注册时绑定手机号）
export function register(data: RegisterPayload): Promise<any> {
  return request.post('/auth/register', data).then((res: any) => res?.data)
}

// 发送手机验证码
// 说明：本地联调模式下后端会把 code 一并返回，正式环境接入短信网关后为 null
export function sendSmsCode(mobile: string): Promise<{ code?: string } | null> {
  return request.post('/auth/sms/code', { mobile }).then((res: any) => res?.data ?? null)
}

// 手机号 + 验证码登录
// 手机号未绑定账户时后端返回 404
export function smsLogin(mobile: string, code: string): Promise<LoginResult> {
  return request.post('/auth/sms/login', { mobile, code }).then((res: any) => res?.data)
}

// 绑定手机号到当前登录账户
export function bindMobile(mobile: string, code: string): Promise<any> {
  return request.post('/auth/sms/bind', { mobile, code }).then((res: any) => res?.data)
}

// 个人资料（当前登录用户）
export interface ProfileData {
  id?: string
  userId?: string | null
  userName?: string | null
  nickName?: string | null
  email?: string | null
  mobile?: string | null
  roleId?: string | null
  roleName?: string | null
  deptId?: string | null
  deptName?: string | null
  lastLoginTime?: string | null
  isAdmin?: boolean
}

// 获取当前登录用户的完整资料
export function getProfile(): Promise<ProfileData> {
  return request.get('/auth/profile').then((res: any) => res?.data)
}

// 更新个人资料，返回最新用户信息
export function updateProfile(data: {
  userName: string
  nickName?: string
  email?: string
  mobile?: string
}): Promise<any> {
  return request.put('/auth/profile', data).then((res: any) => res?.data)
}

// 修改当前用户密码（需原密码）
export function changePassword(payload: {
  oldPassword: string
  newPassword: string
  confirmPassword: string
}): Promise<any> {
  return request.put('/auth/password', payload).then((res: any) => res?.data)
}

// 忘记密码：手机号 + 验证码重置密码，成功后返回登录账号
export function forgotPassword(payload: {
  mobile: string
  code: string
  newPassword: string
  confirmPassword: string
}): Promise<{ userId?: string } | null> {
  return request.post('/auth/password/forgot', payload).then((res: any) => res?.data ?? null)
}
