<script setup lang="ts">
import { useThemeStore } from '@/stores/theme'

// 启动时应用主题（从 localStorage 恢复），保证刷新后不丢失且避免首屏闪烁
useThemeStore().applyTheme()
</script>

<template>
  <!-- 这里是个"插槽" -->
   <!-- 内容区域根据 URL 动态变化 -->
    <!--路由在  router-index.ts 配置-->
   <router-view />
</template>

<style scoped></style>
