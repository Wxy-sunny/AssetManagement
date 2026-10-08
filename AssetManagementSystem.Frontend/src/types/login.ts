export interface LoginRequest {
  username: string;      // 用户名 / 手机号 / 邮箱
  password: string;      // 密码（通常前端会先加密，但类型定义为string）
  captcha?: string;      // 图形验证码（可选，但通常登录失败3次后必填）
  captchaId?: string;    // 验证码唯一ID（用于后台校验）
  grantType?: string;    // 授权类型，如 'password' 或 'sms'
  tenantId?: string;     // 多租户系统下的租户ID
  loginType?: string;    // 登录方式，如 'account' | 'mobile' | 'email'
}