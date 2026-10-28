<template>
  <div class="error-page">
    <el-result icon="warning" title="无访问权限" sub-title="当前账号没有访问该页面的权限，如需开通请联系管理员。">
      <template #extra>
        <el-button type="primary" @click="goHome">返回首页</el-button>
        <el-button @click="goBack">返回上一页</el-button>
      </template>
    </el-result>
  </div>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { hasPermission } from '@/router'

const router = useRouter()
const route = useRoute()

const goHome = () => {
  // 没有首页权限时，退出登录比反复跳转更合理
  if (hasPermission('dashboard:view')) {
    router.push('/dashboard')
  } else {
    localStorage.removeItem('token')
    localStorage.removeItem('refreshToken')
    localStorage.removeItem('userInfo')
    router.push('/login')
  }
}

const goBack = () => {
  const from = route.query.from as string | undefined
  if (from) router.push(from)
  else router.back()
}
</script>

<style scoped>
.error-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: #f5f7fa;
}
</style>
