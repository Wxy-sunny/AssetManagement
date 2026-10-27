<template>
  <div :class="['login-container', 'layout-' + loginLayout]">
    <!-- 空侧动态装饰：登录框在哪一侧，另一侧（及居中时的两侧）展示品牌动画 -->
    <div
      v-for="side in brandSides"
      :key="side"
      :class="['brand-side', 'brand-' + side]"
      aria-hidden="true"
    >
      <div class="brand-inner">
        <div class="brand-logo-wrap">
          <span class="brand-ring r1"></span>
          <span class="brand-ring r2"></span>
          <img class="brand-logo" :src="logoUrl" alt="" />
        </div>
        <h1 class="brand-name">资产管理系统</h1>
        <p class="brand-slogan">资产全生命周期 · 数字化管理平台</p>
        <div class="brand-features">
          <span class="feature-chip"><el-icon><Box /></el-icon>资产台账</span>
          <span class="feature-chip"><el-icon><Coin /></el-icon>资产盘点</span>
          <span class="feature-chip"><el-icon><TrendCharts /></el-icon>折旧分析</span>
        </div>
        <el-icon class="float-icon f1"><Box /></el-icon>
        <el-icon class="float-icon f2"><Coin /></el-icon>
        <el-icon class="float-icon f3"><DataLine /></el-icon>
        <el-icon class="float-icon f4"><Files /></el-icon>
        <el-icon class="float-icon f5"><TrendCharts /></el-icon>
      </div>
    </div>

    <!-- 右上角控制组：主题切换 + 登录框位置切换 -->
    <div class="top-controls">
      <!-- 主题切换（与 Layout 同款：tooltip 包在 dropdown 外层，避免干扰其触发器） -->
      <el-tooltip :content="'当前主题：' + themeStore.currentOption.label" placement="bottom">
        <el-dropdown trigger="click" @command="onThemeCommand">
          <el-icon class="theme-btn">
            <Moon v-if="themeStore.theme === 'dark'" />
            <Sunny v-else />
          </el-icon>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item
                v-for="t in THEME_OPTIONS"
                :key="t.name"
                :command="t.name"
                :disabled="t.name === themeStore.theme"
              >
                <span class="theme-label">{{ t.label }}</span>
                <span class="theme-tip">{{ t.tip }}</span>
              </el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </el-tooltip>

      <!-- 登录框位置切换（左 / 中 / 右）：滑动分段按钮，滑到哪边登录框就在哪边 -->
      <div class="layout-switcher" role="radiogroup" aria-label="登录框位置">
        <span class="switch-thumb" :style="{ transform: `translateX(calc((100% + 4px) * ${layoutIndex}))` }"></span>
        <button
          v-for="(opt, i) in LAYOUT_OPTIONS"
          :key="opt.value"
          type="button"
          :class="['switch-item', { active: i === layoutIndex }]"
          @click="onLayoutPick(opt.value)"
        >
          {{ opt.label }}
        </button>
      </div>
    </div>

    <el-card class="login-card">
      <img class="login-logo" :src="logoUrl" alt="资产管理系统" />
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
            <el-form-item prop="captcha">
              <div class="code-row">
                <el-input
                  v-model="loginForm.captcha"
                  placeholder="请输入验证码"
                  prefix-icon="Picture"
                  clearable
                  maxlength="4"
                  @keyup.enter="handleLogin"
                />
                <img
                  class="captcha-img"
                  :src="captchaImage"
                  alt="验证码"
                  title="点击刷新"
                  @click="fetchCaptcha"
                />
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
            <div class="extra-row">
              <el-link type="primary" :underline="false" @click="openForgot">
                忘记密码？
              </el-link>
              <el-link type="primary" :underline="false" @click="openContact">
                联系管理员
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

      <!-- 联系管理员：用户提交重置密码等申请 -->
      <el-dialog
        v-model="contactVisible"
        title="联系管理员"
        width="400px"
        destroy-on-close
        :close-on-click-modal="false"
      >
        <el-form
          ref="contactFormRef"
          :model="contactForm"
          :rules="contactRules"
          label-width="0"
          @submit.prevent="handleContactSubmit"
        >
          <el-form-item prop="account">
            <el-input
              v-model="contactForm.account"
              placeholder="登录账号或姓名（选填）"
              prefix-icon="User"
              clearable
            />
          </el-form-item>
          <el-form-item prop="mobile">
            <el-input
              v-model="contactForm.mobile"
              placeholder="手机号（选填）"
              prefix-icon="Iphone"
              clearable
              maxlength="11"
            />
          </el-form-item>
          <el-form-item prop="requestType">
            <el-select v-model="contactForm.requestType" style="width: 100%" placeholder="请求类型">
              <el-option label="重置密码" value="reset_password" />
              <el-option label="其他" value="other" />
            </el-select>
          </el-form-item>
          <el-form-item prop="content">
            <el-input
              v-model="contactForm.content"
              type="textarea"
              :rows="3"
              placeholder="请描述您的问题（至少 4 个字）"
              maxlength="500"
              show-word-limit
            />
          </el-form-item>
          <el-form-item prop="captcha">
            <div class="code-row">
              <el-input
                v-model="contactForm.captcha"
                placeholder="请输入验证码"
                prefix-icon="Picture"
                clearable
                maxlength="4"
              />
              <img
                class="captcha-img"
                :src="contactCaptchaImage"
                alt="验证码"
                title="点击刷新"
                @click="fetchContactCaptcha"
              />
            </div>
          </el-form-item>
          <el-form-item>
            <el-button
              type="primary"
              native-type="submit"
              class="login-btn"
              :loading="contactLoading"
              >提交</el-button
            >
          </el-form-item>
        </el-form>
      </el-dialog>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Box, Coin, DataLine, Files, TrendCharts, Sunny, Moon } from '@element-plus/icons-vue'
import { login, register, sendSmsCode, smsLogin, forgotPassword, getCaptcha } from '@/api/auth'
import { submitContactRequest } from '@/api/request'
import { getPreference, savePreference } from '@/api/preference'
import { useUserStore } from '@/stores/user'
import { useThemeStore, THEME_OPTIONS, type ThemeName } from '@/stores/theme'
import logoUrl from '@/assets/logo.png'

const router = useRouter()
const userStore = useUserStore()
const themeStore = useThemeStore()

// ============ 主题切换（与 Layout 同一套 store，选择即时生效并持久化） ============
const onThemeCommand = (name: ThemeName) => {
  themeStore.applyTheme(name)
}

// ============ 登录框位置（左 / 中 / 右） ============
// 匿名阶段存 localStorage（本浏览器即时生效）；登录成功后与服务端"用户配置表"同步：
//   - 本次在登录页主动选过 → 以本次选择为准，写入配置表（跨设备跟随）
//   - 未主动选过 → 以配置表为准（换浏览器也能恢复自己的偏好）
type LoginLayout = 'left' | 'center' | 'right'
const LAYOUT_KEY = 'ams:login-layout'
const LAYOUT_OPTIONS: Array<{ value: LoginLayout; label: string }> = [
  { value: 'left', label: '左侧' },
  { value: 'center', label: '居中' },
  { value: 'right', label: '右侧' },
]
const isLayout = (v: unknown): v is LoginLayout =>
  v === 'left' || v === 'center' || v === 'right'

const saved = localStorage.getItem(LAYOUT_KEY)
const loginLayout = ref<LoginLayout>(isLayout(saved) ? saved : 'center')
const layoutTouched = ref(false)

const applyLayout = (v: LoginLayout) => {
  loginLayout.value = v
  localStorage.setItem(LAYOUT_KEY, v)
}

const onLayoutPick = (v: LoginLayout) => {
  if (v === loginLayout.value) return
  layoutTouched.value = true
  applyLayout(v)
}

// 滑块位置：左=0 / 中=1 / 右=2
const layoutIndex = computed(() => LAYOUT_OPTIONS.findIndex((o) => o.value === loginLayout.value))

// 空侧装饰展示逻辑：登录框在左侧 → 右侧展示；右侧 → 左侧展示；居中 → 两侧都展示
const brandSides = computed<Array<'left' | 'right'>>(() => {
  if (loginLayout.value === 'left') return ['right']
  if (loginLayout.value === 'right') return ['left']
  return ['left', 'right']
})

// 登录成功后同步偏好（失败不影响登录流程）
const syncLayoutPref = async () => {
  try {
    if (layoutTouched.value) {
      await savePreference('login_layout', loginLayout.value)
    } else {
      const remote = await getPreference('login_layout')
      if (remote && isLayout(remote)) applyLayout(remote)
    }
  } catch {
    // 偏好同步失败可忽略
  }
}

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
  captcha: '',
})
const loginRules: FormRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
  captcha: [{ required: true, message: '请输入验证码', trigger: 'blur' }],
}

// ============ 图形验证码 ============
const captchaId = ref('')
const captchaImage = ref('')

const fetchCaptcha = async () => {
  try {
    const res = await getCaptcha()
    captchaId.value = res?.captchaId ?? ''
    captchaImage.value = res?.imageBase64 ?? ''
  } catch {
    // 获取失败不阻塞登录，用户可点击图片重试
  }
}

onMounted(fetchCaptcha)

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
  // 刷新令牌：访问令牌过期时用于无感续期（后端轮换签发）
  if (res.refreshToken) userStore.setRefreshToken(res.refreshToken)
  userStore.setUserInfo(res.userInfo)
  ElMessage.success('登录成功')
  // 同步登录框位置偏好到用户配置表（不阻塞跳转）
  void syncLayoutPref()
  router.push('/dashboard')
}

const handleLogin = async () => {
  if (!loginFormRef.value) return
  await loginFormRef.value.validate()

  loading.value = true
  try {
    const res = await login(loginForm.username, loginForm.password, loginForm.captcha, captchaId.value)
    afterLogin(res)
  } catch {
    // 失败时拦截器已统一提示
  } finally {
    loading.value = false
    // 验证码一次性：无论成功失败都刷新
    loginForm.captcha = ''
    fetchCaptcha()
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

// ============ 联系管理员 ============
const contactVisible = ref(false)
const contactLoading = ref(false)
const contactFormRef = ref<FormInstance>()
const contactCaptchaId = ref('')
const contactCaptchaImage = ref('')

const contactForm = reactive({
  account: '',
  mobile: '',
  requestType: 'reset_password',
  content: '',
  captcha: '',
})

// 账号与手机号至少填一项；填了手机号需格式正确
const contactValidator = (_rule: any, _value: any, callback: any) => {
  const hasAccount = !!contactForm.account.trim()
  const hasMobile = !!contactForm.mobile.trim()
  if (!hasAccount && !hasMobile) {
    callback(new Error('请填写账号或手机号'))
    return
  }
  if (hasMobile && !MOBILE_RE.test(contactForm.mobile.trim())) {
    callback(new Error('手机号格式不正确'))
    return
  }
  callback()
}

const contactRules: FormRules = {
  account: [{ validator: contactValidator, trigger: 'blur' }],
  mobile: [{ validator: contactValidator, trigger: 'blur' }],
  content: [
    { required: true, message: '请描述您的问题', trigger: 'blur' },
    { min: 4, message: '至少 4 个字', trigger: 'blur' },
  ],
  captcha: [{ required: true, message: '请输入验证码', trigger: 'blur' }],
}

const fetchContactCaptcha = async () => {
  try {
    const res = await getCaptcha()
    contactCaptchaId.value = res?.captchaId ?? ''
    contactCaptchaImage.value = res?.imageBase64 ?? ''
  } catch {
    // 失败可点击图片重试
  }
}

const openContact = () => {
  contactForm.account = ''
  contactForm.mobile = ''
  contactForm.requestType = 'reset_password'
  contactForm.content = ''
  contactForm.captcha = ''
  contactVisible.value = true
  fetchContactCaptcha()
}

const handleContactSubmit = async () => {
  if (!contactFormRef.value) return
  await contactFormRef.value.validate()

  contactLoading.value = true
  try {
    await submitContactRequest({
      account: contactForm.account.trim() || undefined,
      mobile: contactForm.mobile.trim() || undefined,
      requestType: contactForm.requestType,
      content: contactForm.content.trim(),
      captchaId: contactCaptchaId.value,
      captcha: contactForm.captcha.trim(),
    })
    ElMessage.success('提交成功，管理员会尽快处理')
    contactVisible.value = false
  } catch {
    // 失败（含验证码错误）刷新验证码
    fetchContactCaptcha()
    contactForm.captcha = ''
  } finally {
    contactLoading.value = false
  }
}
</script>

<style scoped>
.login-container {
  height: 100vh;
  display: flex;
  justify-content: center;
  align-items: center;
  background: var(--ams-body-bg, #f0f2f5);
  position: relative;
}
/* 登录框位置选项：左侧 / 居中 / 右侧 */
.login-container.layout-left {
  justify-content: flex-start;
  padding-left: 8vw;
}
.login-container.layout-right {
  justify-content: flex-end;
  padding-right: 8vw;
}
/* 右上角控制组：主题切换 + 登录框位置切换 */
.top-controls {
  position: absolute;
  top: 16px;
  right: 16px;
  z-index: 10;
  display: flex;
  align-items: center;
  gap: 8px;
}
/* 主题切换按钮 */
.theme-btn {
  font-size: 20px;
  color: var(--ams-text-regular, #606266);
  cursor: pointer;
  padding: 8px;
  border-radius: 50%;
  background: var(--ams-card-bg, #fff);
  box-shadow: 0 1px 6px rgba(0, 0, 0, 0.1);
  transition: color 0.25s;
}
.theme-btn:hover {
  color: var(--el-color-primary, #409eff);
}
.theme-label {
  margin-right: 8px;
}
.theme-tip {
  font-size: 12px;
  color: var(--ams-text-regular, #606266);
}
/* 位置切换器：滑动分段按钮（滑到哪边，登录框就在哪边）
 * 对齐口径说明：按钮 flex 三等分（每段 = W/3），滑块宽 = W/3 - 4px、left 2px，
 * 每步位移 = 滑块宽 + 4px = W/3，与按钮段宽恒等，任意缩放下不会漂移 */
.layout-switcher {
  position: relative; /* 滑块的定位基准必须是切换器自身，而非外层控制组 */
  display: flex;
  padding: 3px 0;
  background: var(--ams-card-bg, #fff);
  border-radius: 18px;
  box-shadow: 0 1px 6px rgba(0, 0, 0, 0.1);
  overflow: hidden;
}
.switch-thumb {
  position: absolute;
  top: 3px;
  bottom: 3px;
  left: 2px;
  width: calc(100% / 3 - 4px);
  background: var(--el-color-primary, #409eff);
  border-radius: 14px;
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}
.switch-item {
  position: relative;
  z-index: 1;
  border: none;
  background: transparent;
  flex: 1 1 0;
  min-width: 64px;
  padding: 6px 0;
  font-size: 13px;
  color: var(--ams-text-regular, #606266);
  cursor: pointer;
  transition: color 0.3s;
  font-family: inherit;
}
.switch-item.active {
  color: #fff;
}
.switch-item:not(.active):hover {
  color: var(--el-color-primary, #409eff);
}

/* ============ 空侧品牌动画装饰 ============ */
.brand-side {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  z-index: 1;
  pointer-events: none;
  user-select: none;
  animation: brand-enter 0.8s ease both;
}
.brand-left {
  left: 9vw;
}
.brand-right {
  right: 9vw;
  text-align: right;
}
.brand-right .brand-features {
  justify-content: flex-end;
}
/* 右侧装饰：漂浮图标镜像到外侧，避免压住文字 */
.brand-right .float-icon.f1 { left: auto; right: -52px; }
.brand-right .float-icon.f2 { left: auto; right: -70px; }
.brand-right .float-icon.f3 { right: auto; left: -60px; }
.brand-right .float-icon.f4 { right: auto; left: -36px; }
.brand-inner {
  position: relative;
  width: 320px;
  padding: 24px 0;
}
/* 居中布局：两侧只保留漂浮的动态符号，不展示品牌文案区 */
.layout-center .brand-logo-wrap,
.layout-center .brand-name,
.layout-center .brand-slogan,
.layout-center .brand-features {
  display: none;
}
/* 文案隐藏后符号失去锚点，给容器一个较高的隐形框让符号纵向铺开 */
.layout-center .brand-inner {
  width: 26vw;
  max-width: 420px;
  height: 72vh;
  max-height: 640px;
  padding: 0;
}
/* Logo：呼吸浮动 + 双层扩散光环 */
.brand-logo-wrap {
  position: relative;
  display: inline-block;
  margin-bottom: 18px;
}
.brand-logo {
  display: block;
  width: 96px;
  height: 96px;
  border-radius: 22px;
  box-shadow: 0 12px 32px rgba(64, 158, 255, 0.28);
  animation: logo-float 4s ease-in-out infinite;
  position: relative;
  z-index: 1;
}
.brand-right .brand-logo-wrap {
  margin-left: auto;
}
.brand-ring {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  border: 2px solid rgba(64, 158, 255, 0.4);
  animation: ring-pulse 2.6s ease-out infinite;
}
.brand-ring.r2 {
  animation-delay: 1.3s;
}
.brand-name {
  margin: 0 0 8px;
  font-size: 34px;
  font-weight: 700;
  color: var(--ams-text-main, #303133);
  letter-spacing: 2px;
}
.brand-slogan {
  margin: 0 0 22px;
  font-size: 15px;
  color: var(--ams-text-regular, #909399);
  letter-spacing: 1px;
}
.brand-features {
  display: flex;
  gap: 10px;
}
.feature-chip {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 6px 14px;
  background: var(--ams-card-bg, #fff);
  border-radius: 16px;
  font-size: 13px;
  color: var(--ams-text-regular, #606266);
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.06);
}
.feature-chip .el-icon {
  color: var(--el-color-primary, #409eff);
}
/* 漂浮的资产符号：各自不同的轨迹 / 速度 / 透明度 */
.float-icon {
  position: absolute;
  font-size: 40px;
  color: rgba(64, 158, 255, 0.28);
  animation: icon-float 6s ease-in-out infinite;
}
.float-icon.f1 { top: -46px; left: -52px; font-size: 46px; animation-duration: 7s; }
.float-icon.f2 { bottom: 20px; left: -70px; font-size: 38px; animation-duration: 5.5s; animation-delay: 0.8s; color: rgba(103, 194, 58, 0.3); }
.float-icon.f3 { top: 30px; right: -60px; animation-duration: 6.5s; animation-delay: 0.4s; }
.float-icon.f4 { bottom: -40px; right: -36px; font-size: 44px; animation-duration: 8s; animation-delay: 1.2s; color: rgba(230, 162, 60, 0.32); }
.float-icon.f5 { top: 130px; left: 40px; font-size: 30px; animation-duration: 5s; animation-delay: 1.6s; }

@keyframes brand-enter {
  from { opacity: 0; transform: translateY(calc(-50% + 24px)); }
  to { opacity: 1; transform: translateY(-50%); }
}
@keyframes logo-float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-10px); }
}
@keyframes ring-pulse {
  0% { transform: scale(1); opacity: 0.7; }
  100% { transform: scale(1.9); opacity: 0; }
}
@keyframes icon-float {
  0%, 100% { transform: translateY(0) rotate(0deg); }
  33% { transform: translateY(-16px) rotate(6deg); }
  66% { transform: translateY(8px) rotate(-5deg); }
}
/* 中等宽度：只保留居中布局，装饰退场避免拥挤 */
@media (max-width: 1280px) {
  .brand-side {
    display: none;
  }
}
.login-logo {
  display: block;
  width: 64px;
  height: 64px;
  margin: 0 auto 12px;
  border-radius: 12px;
}
.login-card {
  width: 420px;
  padding: 20px 30px 30px;
}
.login-title {
  text-align: center;
  margin-bottom: 20px;
  color: var(--ams-text-main, #303133);
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
.captcha-img {
  flex-shrink: 0;
  width: 120px;
  height: 40px;
  border-radius: 4px;
  border: 1px solid var(--ams-border, #dcdfe6);
  cursor: pointer;
}
.extra-row {
  display: flex;
  justify-content: flex-end;
  gap: 16px;
  margin-bottom: 4px;
}
</style>
