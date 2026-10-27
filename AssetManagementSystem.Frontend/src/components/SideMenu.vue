<template>
  <!--
    侧边菜单（桌面侧栏与移动端抽屉共用）。
    颜色全部走主题 CSS 变量（themes.css），切换主题时自动跟随。
  -->
  <el-menu
    :default-active="$route.path"
    :collapse="collapsed"
    :collapse-transition="false"
    router
    class="side-menu"
    background-color="var(--ams-sidebar-bg)"
    text-color="var(--ams-sidebar-text)"
    active-text-color="var(--ams-primary)"
    @select="$emit('navigate')"
  >
    <el-menu-item v-if="menuPerm.dashboard" index="/dashboard">
      <el-icon><HomeFilled /></el-icon>
      <template #title>首页</template>
    </el-menu-item>

    <el-sub-menu v-if="menuPerm.assets || menuPerm.categories" index="info">
      <template #title>
        <el-icon><Box /></el-icon>
        <span>信息管理</span>
      </template>
      <el-menu-item v-if="menuPerm.assets" index="/assets">
        <el-icon><Goods /></el-icon>
        <template #title>资产列表</template>
      </el-menu-item>
      <el-menu-item v-if="menuPerm.categories" index="/categories">
        <el-icon><Grid /></el-icon>
        <template #title>资产分类</template>
      </el-menu-item>
    </el-sub-menu>

    <el-menu-item v-if="menuPerm.files" index="/files">
      <el-icon><Folder /></el-icon>
      <template #title>资料管理</template>
    </el-menu-item>

    <el-sub-menu index="notice">
      <template #title>
        <el-icon><Bell /></el-icon>
        <span>通知公告</span>
      </template>
      <el-menu-item index="/notifications">
        <el-icon><ChatDotRound /></el-icon>
        <template #title>系统通知</template>
      </el-menu-item>
      <el-menu-item index="/messages">
        <el-icon><Message /></el-icon>
        <template #title>
          我的消息
          <el-badge v-if="msgUnreadCount > 0" :value="msgUnreadCount" :max="99" class="menu-badge" />
        </template>
      </el-menu-item>
      <el-menu-item v-if="menuPerm.notifications" index="/system/notifications">
        <el-icon><EditPen /></el-icon>
        <template #title>通知管理</template>
      </el-menu-item>
    </el-sub-menu>

    <el-sub-menu
      v-if="menuPerm.users || menuPerm.roles || menuPerm.depts || menuPerm.requests"
      index="system"
    >
      <template #title>
        <el-icon><Setting /></el-icon>
        <span>权限管理</span>
      </template>
      <el-menu-item v-if="menuPerm.users" index="/system/users">
        <el-icon><User /></el-icon>
        <template #title>用户管理</template>
      </el-menu-item>
      <el-menu-item v-if="menuPerm.roles" index="/system/roles">
        <el-icon><Avatar /></el-icon>
        <template #title>角色管理</template>
      </el-menu-item>
      <el-menu-item v-if="menuPerm.depts" index="/system/departments">
        <el-icon><OfficeBuilding /></el-icon>
        <template #title>部门管理</template>
      </el-menu-item>
      <el-menu-item v-if="menuPerm.requests" index="/system/requests">
        <el-icon><ChatLineSquare /></el-icon>
        <template #title>
          联系请求
          <el-badge v-if="pendingCount > 0" :value="pendingCount" :max="99" class="menu-badge" />
        </template>
      </el-menu-item>
    </el-sub-menu>
  </el-menu>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import {
  HomeFilled,
  Box,
  Goods,
  Grid,
  Folder,
  Setting,
  User,
  Avatar,
  OfficeBuilding,
  Bell,
  ChatDotRound,
  EditPen,
  Message,
  ChatLineSquare,
} from '@element-plus/icons-vue'
import { useUserStore } from '@/stores/user'
import { PERMISSIONS } from '@/types'
import { getRequestPendingCount } from '@/api/request'
import { getUnreadCount } from '@/api/message'

defineProps<{ collapsed?: boolean }>()
defineEmits<{ navigate: [] }>()

const route = useRoute()
const userStore = useUserStore()

// 菜单可见性：根据当前用户权限动态控制
const menuPerm = computed(() => ({
  dashboard: userStore.hasPermission(PERMISSIONS.DashboardView),
  assets: userStore.hasPermission(PERMISSIONS.AssetList),
  categories: userStore.hasPermission(PERMISSIONS.CategoryList),
  files: userStore.hasPermission(PERMISSIONS.FileList),
  users: userStore.hasPermission(PERMISSIONS.UserList),
  roles: userStore.hasPermission(PERMISSIONS.RoleList),
  depts: userStore.hasPermission(PERMISSIONS.DeptList),
  notifications: userStore.hasPermission(PERMISSIONS.NotificationList),
  requests: userStore.hasPermission(PERMISSIONS.RequestList),
}))

// 联系请求待处理数量（菜单角标）
const pendingCount = ref(0)
const loadPendingCount = async () => {
  if (!menuPerm.value.requests) return
  try {
    pendingCount.value = await getRequestPendingCount()
  } catch {
    pendingCount.value = 0
  }
}

// 个人消息未读数量（菜单角标，登录即可，无权限门槛）
const msgUnreadCount = ref(0)
const loadMsgUnreadCount = async () => {
  try {
    msgUnreadCount.value = await getUnreadCount()
  } catch {
    msgUnreadCount.value = 0
  }
}

// 进入对应页面时刷新角标
watch(
  () => route.path,
  (p) => {
    if (p === '/system/requests') loadPendingCount()
    if (p === '/messages') loadMsgUnreadCount()
  },
)

onMounted(() => {
  loadPendingCount()
  loadMsgUnreadCount()
})
</script>

<style scoped>
.side-menu {
  border-right: none;
  height: 100%;
}
.menu-badge {
  margin-left: 6px;
}
</style>
