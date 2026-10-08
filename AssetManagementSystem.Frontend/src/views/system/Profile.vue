<template>
  <div class="profile-page" v-loading="loading">
    <!-- 账户概览 -->
    <el-card shadow="never" class="overview-card">
      <div class="overview">
        <el-avatar :size="64" class="avatar">
          {{ avatarText }}
        </el-avatar>
        <div class="overview-meta">
          <div class="name-row">
            <span class="name">{{ profile.userName || profile.userId || '-' }}</span>
            <el-tag v-if="profile.isAdmin" type="danger" size="small">超级管理员</el-tag>
            <el-tag v-else-if="profile.roleName" type="info" size="small">
              {{ profile.roleName }}
            </el-tag>
          </div>
          <div class="sub-row">
            <span>登录账号：{{ profile.userId || '-' }}</span>
            <el-divider direction="vertical" />
            <span>所属部门：{{ profile.deptName || '未分配' }}</span>
            <el-divider direction="vertical" />
            <span>最后登录：{{ profile.lastLoginTime || '-' }}</span>
          </div>
        </div>
      </div>
    </el-card>

    <el-card shadow="never">
      <el-tabs v-model="activeTab">
        <!-- 基本资料 -->
        <el-tab-pane label="基本资料" name="profile">
          <el-form
            ref="profileFormRef"
            :model="profileForm"
            :rules="profileRules"
            label-width="100px"
            class="profile-form"
          >
            <el-form-item label="登录账号">
              <el-input :model-value="profile.userId || ''" disabled />
              <div class="form-tip">登录账号不可修改</div>
            </el-form-item>
            <el-form-item label="姓名" prop="userName">
              <el-input v-model="profileForm.userName" placeholder="请输入姓名" />
            </el-form-item>
            <el-form-item label="昵称" prop="nickName">
              <el-input v-model="profileForm.nickName" placeholder="选填" />
            </el-form-item>
            <el-form-item label="邮箱" prop="email">
              <el-input v-model="profileForm.email" placeholder="选填" />
            </el-form-item>
            <el-form-item label="手机号" prop="mobile">
              <el-input
                v-model="profileForm.mobile"
                placeholder="用于手机号登录 / 找回密码"
                maxlength="11"
                clearable
              />
              <div class="form-tip">修改后可用于手机号 + 验证码登录</div>
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="saving" @click="handleSaveProfile">
                保存资料
              </el-button>
            </el-form-item>
          </el-form>
        </el-tab-pane>

        <!-- 修改密码 -->
        <el-tab-pane label="修改密码" name="password">
          <el-form
            ref="pwdFormRef"
            :model="pwdForm"
            :rules="pwdRules"
            label-width="100px"
            class="profile-form"
          >
            <el-form-item label="原密码" prop="oldPassword">
              <el-input
                v-model="pwdForm.oldPassword"
                type="password"
                placeholder="请输入当前密码"
                show-password
                clearable
              />
            </el-form-item>
            <el-form-item label="新密码" prop="newPassword">
              <el-input
                v-model="pwdForm.newPassword"
                type="password"
                placeholder="至少 6 位"
                show-password
                clearable
              />
            </el-form-item>
            <el-form-item label="确认新密码" prop="confirmPassword">
              <el-input
                v-model="pwdForm.confirmPassword"
                type="password"
                placeholder="请再次输入新密码"
                show-password
                clearable
              />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="changing" @click="handleChangePassword">
                修改密码
              </el-button>
              <el-button @click="resetPwdForm">重置</el-button>
            </el-form-item>
          </el-form>
        </el-tab-pane>
      </el-tabs>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { getProfile, updateProfile, changePassword, type ProfileData } from '@/api/auth'
import { useUserStore } from '@/stores/user'

const route = useRoute()
const userStore = useUserStore()

const activeTab = ref('profile')
const loading = ref(false)
const saving = ref(false)
const changing = ref(false)

const profile = ref<ProfileData>({})

const MOBILE_RE = /^1[3-9]\d{9}$/

const mobileValidator = (_rule: any, value: string, callback: any) => {
  // 手机号可留空（表示不绑定）
  if (!value) callback()
  else if (!MOBILE_RE.test(value)) callback(new Error('请输入正确的手机号'))
  else callback()
}

// 头像文字：取姓名首字
const avatarText = computed(() => {
  const name = profile.value.userName || profile.value.userId || 'U'
  return name.charAt(0).toUpperCase()
})

// ============ 基本资料 ============
const profileFormRef = ref<FormInstance>()
const profileForm = reactive({
  userName: '',
  nickName: '',
  email: '',
  mobile: '',
})

const profileRules: FormRules = {
  userName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  email: [{ type: 'email', message: '邮箱格式不正确', trigger: 'blur' }],
  mobile: [{ validator: mobileValidator, trigger: 'blur' }],
}

const loadProfile = async () => {
  loading.value = true
  try {
    profile.value = await getProfile()
    profileForm.userName = profile.value.userName ?? ''
    profileForm.nickName = profile.value.nickName ?? ''
    profileForm.email = profile.value.email ?? ''
    profileForm.mobile = profile.value.mobile ?? ''
  } catch {
    // 失败时拦截器已提示
  } finally {
    loading.value = false
  }
}

const handleSaveProfile = async () => {
  if (!profileFormRef.value) return
  await profileFormRef.value.validate()

  saving.value = true
  try {
    const updated = await updateProfile({ ...profileForm })
    ElMessage.success('资料已更新')
    // 接口返回最新用户信息，合并进本地缓存，保证顶栏昵称、权限立即同步
    if (updated) {
      userStore.setUserInfo({ ...(userStore.userInfo ?? {}), ...updated })
    }
    await loadProfile()
  } catch {
    // 失败时拦截器已提示
  } finally {
    saving.value = false
  }
}

// ============ 修改密码 ============
const pwdFormRef = ref<FormInstance>()
const pwdForm = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: '',
})

const confirmPwdValidator = (_rule: any, value: string, callback: any) => {
  if (value !== pwdForm.newPassword) callback(new Error('两次输入的新密码不一致'))
  else callback()
}

const pwdRules: FormRules = {
  oldPassword: [{ required: true, message: '请输入原密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码至少 6 位', trigger: 'blur' },
  ],
  confirmPassword: [
    { required: true, message: '请再次输入新密码', trigger: 'blur' },
    { validator: confirmPwdValidator, trigger: 'blur' },
  ],
}

const resetPwdForm = () => {
  pwdForm.oldPassword = ''
  pwdForm.newPassword = ''
  pwdForm.confirmPassword = ''
}

const handleChangePassword = async () => {
  if (!pwdFormRef.value) return
  await pwdFormRef.value.validate()

  changing.value = true
  try {
    await changePassword({ ...pwdForm })
    ElMessage.success('密码修改成功，下次登录请使用新密码')
    resetPwdForm()
  } catch {
    // 失败时拦截器已提示（如"原密码不正确"）
  } finally {
    changing.value = false
  }
}

onMounted(() => {
  // 从顶栏"重置密码"进入时直接定位到密码页
  if (route.query.tab === 'password') activeTab.value = 'password'
  loadProfile()
})
</script>

<style scoped>
.profile-page {
  padding: 0;
}
.overview-card {
  margin-bottom: 16px;
}
.overview {
  display: flex;
  align-items: center;
  gap: 16px;
}
.avatar {
  background: #409eff;
  font-size: 26px;
  flex-shrink: 0;
}
.overview-meta {
  min-width: 0;
}
.name-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}
.name {
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}
.sub-row {
  font-size: 13px;
  color: #909399;
}
.profile-form {
  max-width: 520px;
  margin-top: 8px;
}
.form-tip {
  font-size: 12px;
  color: #a8abb2;
  line-height: 1.4;
  margin-top: 4px;
}
</style>
