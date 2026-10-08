<template>
  <div class="login-container">
    <el-card class="login-card">
      <h2 class="login-title">资产管理系统</h2>

      <el-tabs v-model="activeTab" class="login-tabs">
        <!-- 账号密码登录 -->
        <el-tab-pane label="账号登录" name="password">
          <el-form
            ref="loginFormRef"
            :model="loginForm"
            :rules="loginRules"
            label-width="0"
            @submit.prevent="handleLogin"
          >
            <el-form-item prop="username">
              <el-input
                v-model="loginForm.username"
                placeholder="请输入用户名 / 登录账号"
                prefix-icon="User"
                clearable
              />
            </el-form-item>
            <el-form-item prop="password">
              <el-input
                v-model="loginForm.password"
                type="password"
                placeholder="请输入密码"
                prefix-icon="Lock"
                show-password
                clearable
                @keyup.enter="handleLogin"
              />
            </el-form-item>
            <el-form-item>
              <el-button
                type="primary"
                native-type="submit"
                class="login-btn"
                :loading="loading"
                >登 录</el-button
              >
            </el-form-item>
            <div class="extra-row">
              <el-link type="primary" :underline="false" @click="openForgot">
                忘记密码？
              </el-link>
            </div>
          </el-form>
        </el-tab-pane>

        <!-- 手机号 + 验证码登录 -->
        <el-tab-pane label="手机号登录" name="sms">
          <el-form
            ref="smsFormRef"
            :model="smsForm"
            :rules="smsRules"
            label-width="0"
            @submit.prevent="handleSmsLogin"
          >
            <el-form-item prop="mobile">
              <el-input
                v-model="smsForm.mobile"
                placeholder="请输入手机号"
                prefix-icon="Iphone"
                clearable
                maxlength="11"
              />
            </el-form-item>
            <el-form-item prop="code">
              <div class="code-row">
                <el-input
                  v-model="smsForm.code"
                  placeholder="请输入验证码"
                  prefix-icon="Message"
                  clearable
                  maxlength="6"
                  @keyup.enter="handleSmsLogin"
                />
                <el-button
                  class="code-btn"
                  :disabled="countdown > 0"
                  :loading="codeSending"
                  @click="handleSendCode(smsForm.mobile)"
                >
                  {{ countdown > 0 ? `${countdown}s 后重发` : '发送验证码' }}
                </el-button>
              </div>
            </el-form-item>
            <el-form-item>
              <el-button
                type="primary"
                native-type="submit"
                class="login-btn"
                :loading="loading"
                >登 录</el-button
              >
            </el-form-item>
          </el-form>
        </el-tab-pane>

        <!-- 注册账号（注册时即绑定手机号） -->
        <el-tab-pane label="注册账号" name="register">
          <el-form
            ref="registerFormRef"
            :model="registerForm"
            :rules="registerRules"
            label-width="0"
            @submit.prevent="handleRegister"
          >
            <el-form-item prop="userId">
              <el-input
                v-model="registerForm.userId"
                placeholder="请输入登录账号"
                prefix-icon="User"
                clearable
              />
            </el-form-item>
            <el-form-item prop="userName">
              <el-input
                v-model="registerForm.userName"
                placeholder="请输入姓名（选填）"
                prefix-icon="Postcard"
                clearable
              />
            </el-form-item>
            <el-form-item prop="mobile">
              <el-input
                v-model="registerForm.mobile"
                placeholder="请输入手机号（用于手机号登录）"
                prefix-icon="Iphone"
                clearable
                maxlength="11"
              />
            </el-form-item>
            <el-form-item prop="password">
              <el-input
                v-model="registerForm.password"
                type="password"
                placeholder="请输入密码（至少 6 位）"
                prefix-icon="Lock"
                show-password
                clearable
              />
            </el-form-item>
            <el-form-item prop="confirmPassword">
              <el-input
                v-model="registerForm.confirmPassword"
                type="password"
                placeholder="请再次输入密码"
                prefix-icon="Lock"
                show-password
                clearable
                @keyup.enter="handleRegister"
              />
            </el-form-item>
            <el-form-item>
              <el-button
                type="primary"
                native-type="submit"
                class="login-btn"
                :loading="loading"
                >注 册</el-button
              >
            </el-form-item>
          </el-form>
        </el-tab-pane>

      </el-tabs>

      <!-- 忘记密码：手机号 + 验证码重置（弹窗，与 Tab 方案是同一功能） -->
      <el-dialog
        v-model="forgotVisible"
        title="找回密码"
        width="380px"
        destroy-on-close
        :close-on-click-modal="false"
      >
        <el-form
          ref="forgotFormRef"
          :model="forgotForm"
          :rules="forgotRules"
          label-width="0"
          @submit.prevent="handleForgot"
        >
          <el-form-item prop="mobile">
            <el-input
              v-model="forgotForm.mobile"
              placeholder="请输入绑定的手机号"
              prefix-icon="Iphone"
              clearable
              maxlength="11"
            />
          </el-form-item>
          <el-form-item prop="code">
            <div class="code-row">
              <el-input
                v-model="forgotForm.code"
                placeholder="请输入验证码"
                prefix-icon="Message"
                clearable
                maxlength="6"
              />
              <el-button
                class="code-btn"
                :disabled="countdown > 0"
                :loading="codeSending"
                @click="handleSendCode(forgotForm.mobile)"
              >
                {{ countdown > 0 ? `${countdown}s 后重发` : '发送验证码' }}
              </el-button>
            </div>
          </el-form-item>
          <el-form-item prop="newPassword">
            <el-input
              v-model="forgotForm.newPassword"
              type="password"
              placeholder="请输入新密码（至少 6 位）"
              prefix-icon="Lock"
              show-password
              clearable
            />
          </el-form-item>
          <el-form-item prop="confirmPassword">
            <el-input
              v-model="forgotForm.confirmPassword"
              type="password"
              placeholder="请再次输入新密码"
              prefix-icon="Lock"
              show-password
              clearable
              @keyup.enter="handleForgot"
            />
          </el-form-item>
          <el-form-item>
            <el-button
              type="primary"
              native-type="submit"
              class="login-btn"
              :loading="loading"
              >重置密码</el-button
            >
          </el-form-item>
        </el-form>
      </el-dialog>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { login, register, sendSmsCode, smsLogin, forgotPassword } from '@/api/auth'
import { useUserStore } from '@/stores/user'

const router = useRouter()
const userStore = useUserStore()

type TabName = 'password' | 'sms' | 'register'
const activeTab = ref<TabName>('password')
const loading = ref(false)

// 找回密码弹窗
const forgotVisible = ref(false)

const openForgot = () => {
  // 每次打开都清空上次的输入
  forgotForm.mobile = ''
  forgotForm.code = ''
  forgotForm.newPassword = ''
  forgotForm.confirmPassword = ''
  forgotVisible.value = true
}

// 中国大陆手机号
const MOBILE_RE = /^1[3-9]\d{9}$/

const mobileValidator = (_rule: any, value: string, callback: any) => {
  if (!value) callback(new Error('请输入手机号'))
  else if (!MOBILE_RE.test(value)) callback(new Error('请输入正确的手机号'))
  else callback()
}

// ============ 账号密码登录 ============
const loginFormRef = ref<FormInstance>()
const loginForm = reactive({
  username: '',
  password: '',
})
const loginRules: FormRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

// ============ 手机号验证码登录 ============
const smsFormRef = ref<FormInstance>()
const smsForm = reactive({
  mobile: '',
  code: '',
})
const smsRules: FormRules = {
  mobile: [{ validator: mobileValidator, trigger: 'blur' }],
  code: [{ required: true, message: '请输入验证码', trigger: 'blur' }],
}

// ============ 找回密码 ============
const forgotFormRef = ref<FormInstance>()
const forgotForm = reactive({
  mobile: '',
  code: '',
  newPassword: '',
  confirmPassword: '',
})

const confirmForgotValidator = (_rule: any, value: string, callback: any) => {
  if (value !== forgotForm.newPassword) callback(new Error('两次输入的新密码不一致'))
  else callback()
}

const forgotRules: FormRules = {
  mobile: [{ validator: mobileValidator, trigger: 'blur' }],
  code: [{ required: true, message: '请输入验证码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码至少 6 位', trigger: 'blur' },
  ],
  confirmPassword: [
    { required: true, message: '请再次输入新密码', trigger: 'blur' },
    { validator: confirmForgotValidator, trigger: 'blur' },
  ],
}

const codeSending = ref(false)
const countdown = ref(0)
let timer: ReturnType<typeof setInterval> | undefined

const startCountdown = () => {
  countdown.value = 60
  if (timer) clearInterval(timer)
  timer = setInterval(() => {
    countdown.value -= 1
    if (countdown.value <= 0) {
      clearInterval(timer)
      timer = undefined
    }
  }, 1000)
}

onBeforeUnmount(() => {
  if (timer) clearInterval(timer)
})

/**
 * 发送验证码（手机号登录与找回密码共用）
 * @param mobile 目标手机号
 */
const handleSendCode = async (mobile: string) => {
  if (!MOBILE_RE.test(mobile)) {
    ElMessage.warning('请输入正确的手机号')
    return
  }
  codeSending.value = true
  try {
    const res = await sendSmsCode(mobile)
    ElMessage.success('验证码已发送')
    // 联调模式：未接入短信网关时后端会回传验证码，自动填入方便测试
    if (res?.code) {
      if (forgotVisible.value) forgotForm.code = res.code
      else smsForm.code = res.code
      ElMessage.info(`联调模式，验证码：${res.code}`)
    }
    startCountdown()
  } catch {
    // 失败时拦截器已统一提示
  } finally {
    codeSending.value = false
  }
}

// ============ 注册 ============
const registerFormRef = ref<FormInstance>()
const registerForm = reactive({
  userId: '',
  userName: '',
  mobile: '',
  password: '',
  confirmPassword: '',
})

const confirmValidator = (_rule: any, value: string, callback: any) => {
  if (value !== registerForm.password) callback(new Error('两次输入的密码不一致'))
  else callback()
}

const registerRules: FormRules = {
  userId: [{ required: true, message: '请输入登录账号', trigger: 'blur' }],
  mobile: [{ validator: mobileValidator, trigger: 'blur' }],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码至少 6 位', trigger: 'blur' },
  ],
  confirmPassword: [
    { required: true, message: '请再次输入密码', trigger: 'blur' },
    { validator: confirmValidator, trigger: 'blur' },
  ],
}

// ============ 登录成功后的统一处理 ============
const afterLogin = (res: any) => {
  if (!res?.token) {
    ElMessage.error('登录失败，未获取到令牌')
    return
  }
  userStore.setToken(res.token)
  userStore.setUserInfo(res.userInfo)
  ElMessage.success('登录成功')
  router.push('/dashboard')
}

const handleLogin = async () => {
  if (!loginFormRef.value) return
  await loginFormRef.value.validate()

  loading.value = true
  try {
    const res = await login(loginForm.username, loginForm.password)
    afterLogin(res)
  } catch {
    // 失败时拦截器已统一提示
  } finally {
    loading.value = false
  }
}

const handleSmsLogin = async () => {
  if (!smsFormRef.value) return
  await smsFormRef.value.validate()

  loading.value = true
  try {
    const res = await smsLogin(smsForm.mobile, smsForm.code)
    afterLogin(res)
  } catch (e: any) {
    // 404：该手机号未绑定任何账户，引导用户先注册并把手机号带过去
    if (e?.code === 404) {
      registerForm.mobile = smsForm.mobile
      activeTab.value = 'register'
    }
  } finally {
    loading.value = false
  }
}

const handleRegister = async () => {
  if (!registerFormRef.value) return
  await registerFormRef.value.validate()

  loading.value = true
  try {
    await register({ ...registerForm })
    ElMessage.success('注册成功，请登录')
    // 回填账号并切回账号登录
    loginForm.username = registerForm.userId
    loginForm.password = ''
    activeTab.value = 'password'
  } catch {
    // 失败时拦截器已统一提示
  } finally {
    loading.value = false
  }
}

const handleForgot = async () => {
  if (!forgotFormRef.value) return
  await forgotFormRef.value.validate()

  loading.value = true
  try {
    const res = await forgotPassword({ ...forgotForm })
    ElMessage.success('密码重置成功，请用新密码登录')
    // 后端返回登录账号，回填后切回账号登录
    if (res?.userId) loginForm.username = res.userId
    loginForm.password = ''
    forgotVisible.value = false
    activeTab.value = 'password'
  } catch (e: any) {
    // 404：该手机号未绑定任何账户，关闭弹窗并引导先注册
    if (e?.code === 404) {
      forgotVisible.value = false
      registerForm.mobile = forgotForm.mobile
      activeTab.value = 'register'
    }
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-container {
  height: 100vh;
  display: flex;
  justify-content: center;
  align-items: center;
  background: #f0f2f5;
}
.login-card {
  width: 420px;
  padding: 20px 30px 30px;
}
.login-title {
  text-align: center;
  margin-bottom: 20px;
  color: #303133;
}
.login-btn {
  width: 100%;
}
.code-row {
  display: flex;
  gap: 8px;
  width: 100%;
}
.code-row :deep(.el-input) {
  flex: 1;
}
.code-btn {
  flex-shrink: 0;
  width: 116px;
}
.extra-row {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 4px;
}
</style>
