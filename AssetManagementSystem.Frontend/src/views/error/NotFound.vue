<template>
  <div class="error-page">
    <el-result icon="info" title="404" sub-title="页面不存在或已被移除。">
      <template #extra>
        <el-button type="primary" @click="goHome">返回首页</el-button>
      </template>
    </el-result>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { hasPermission } from '@/router'

const router = useRouter()

const goHome = () => {
  if (hasPermission('dashboard:view')) {
    router.push('/dashboard')
  } else {
    router.push('/login')
  }
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
