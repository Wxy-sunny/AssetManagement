<template>
  <el-container class="layout-container">
    <!-- 左侧菜单（可折叠） -->
    <el-aside :width="collapsed ? '64px' : '200px'" class="aside">
      <div class="logo">
        <span v-if="!collapsed">资产管理系统</span>
        <span v-else>资</span>
      </div>
      <el-menu
        :default-active="$route.path"
        :collapse="collapsed"
        :collapse-transition="false"
        router
        class="menu"
        background-color="#001529"
        text-color="#fff"
        active-text-color="#409EFF"
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
          <el-menu-item v-if="menuPerm.notifications" index="/system/notifications">
            <el-icon><EditPen /></el-icon>
            <template #title>通知管理</template>
          </el-menu-item>
        </el-sub-menu>

        <el-sub-menu
          v-if="menuPerm.users || menuPerm.roles || menuPerm.depts"
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
        </el-sub-menu>
      </el-menu>
    </el-aside>

    <!-- 右侧主体 -->
    <el-container>
      <!-- 顶部导航 -->
      <el-header class="header">
        <div class="header-left">
          <!-- 折叠 / 展开侧边栏 -->
          <el-tooltip :content="collapsed ? '展开菜单' : '收起菜单'" placement="bottom">
            <el-icon class="collapse-btn" @click="collapsed = !collapsed">
              <Expand v-if="collapsed" />
              <Fold v-else />
            </el-icon>
          </el-tooltip>
        </div>
        <div class="header-right">
          <!-- 用户下拉菜单 -->
          <el-dropdown trigger="click" @command="handleCommand">
            <div class="user-info">
              <!-- 头像：如果有头像图片则显示图片，否则显示图标 -->
              <el-avatar :size="36" :src="userStore.userInfo?.avatar || ''">
                <el-icon><User /></el-icon>
              </el-avatar>
              <span class="username">{{ userStore.displayName}}</span>
              <el-icon class="arrow"><ArrowDown /></el-icon>
            </div>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="notifications">
                  <el-icon><Bell /></el-icon> 系统通知
                </el-dropdown-item>
                <el-dropdown-item command="profile">
                  <el-icon><User /></el-icon> 个人资料
                </el-dropdown-item>
                <el-dropdown-item command="resetPassword">
                  <el-icon><Lock /></el-icon> 修改密码
                </el-dropdown-item>
                <el-dropdown-item divided command="logout">
                  <el-icon><SwitchButton /></el-icon> 退出登录
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <!-- 页签导航（访问过的页面，可切换 / 关闭） -->
      <div class="tags-bar">
        <el-tag
          v-for="tag in visitedTags"
          :key="tag.path"
          class="tag-item"
          :class="{ 'tag-active': tag.path === route.path }"
          :closable="tag.path !== AFFIX_PATH"
          :effect="tag.path === route.path ? 'light' : 'plain'"
          @click="handleTagClick(tag)"
          @close="handleCloseTag(tag)"
        >
          {{ tag.title }}
        </el-tag>
      </div>

      <!-- 内容区域 -->
      <el-main class="main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter, type RouteLocationNormalizedLoaded } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  HomeFilled,
  Box,
  Grid,
  Goods,
  Folder,
  Setting,
  User,
  Avatar,
  OfficeBuilding,
  Bell,
  ChatDotRound,
  EditPen,
  Lock,
  SwitchButton,
  ArrowDown,
  Fold,
  Expand,
} from '@element-plus/icons-vue'
import { useUserStore } from '@/stores/user'
import { PERMISSIONS } from '@/types'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

// ============ 侧边栏折叠 ============
const collapsed = ref(false)

// ============ 页签导航 ============
interface TagView {
  path: string
  title: string
}

// 首页页签常驻不可关闭
const AFFIX_PATH = '/dashboard'

const visitedTags = ref<TagView[]>([])

const addTag = (r: RouteLocationNormalizedLoaded) => {
  const title = (r.meta?.title as string) || '未命名'
  if (!visitedTags.value.some((t) => t.path === r.path)) {
    visitedTags.value.push({ path: r.path, title })
  }
}

// 路由变化时自动追加页签
watch(
  () => route.path,
  () => addTag(route),
  { immediate: true },
)

const handleTagClick = (tag: TagView) => {
  if (tag.path !== route.path) router.push(tag.path)
}

const handleCloseTag = (tag: TagView) => {
  if (tag.path === AFFIX_PATH) return
  const index = visitedTags.value.findIndex((t) => t.path === tag.path)
  if (index === -1) return

  visitedTags.value.splice(index, 1)

  // 关闭的是当前页时，自动跳到相邻页签
  if (route.path === tag.path) {
    const next = visitedTags.value[index] ?? visitedTags.value[index - 1]
    if (next) router.push(next.path)
  }
}

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
}))

// 处理下拉菜单命令
const handleCommand = (command: string) => {
  switch (command) {
    case 'notifications':
      router.push('/notifications')
      break
    case 'profile':
      router.push('/profile')
      break
    case 'resetPassword':
      // 直接进入个人资料页的"修改密码"页签（自助修改，需验证原密码）
      router.push({ path: '/profile', query: { tab: 'password' } })
      break
    case 'logout':
      // 二次确认退出
      ElMessageBox.confirm('确定要退出登录吗？', '提示', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning',
      }).then(() => {
        userStore.logout()
        ElMessage.success('已退出登录')
        router.push('/login')
      }).catch(() => {})
      break
    default:
      break
  }
}
</script>

<style scoped>
.layout-container {
  height: 100vh;
}
.aside {
  background-color: #001529;
  color: #fff;
  transition: width 0.25s;
  overflow-x: hidden;
}
.logo {
  height: 60px;
  line-height: 60px;
  text-align: center;
  font-size: 18px;
  font-weight: bold;
  color: #fff;
  border-bottom: 1px solid #0a1f3a;
  white-space: nowrap;
  overflow: hidden;
}
.menu {
  border-right: none;
}
.header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #fff;
  border-bottom: 1px solid #e6e6e6;
  padding: 0 20px;
}
.header-left {
  display: flex;
  align-items: center;
}
.collapse-btn {
  font-size: 20px;
  color: #303133;
  cursor: pointer;
}
.collapse-btn:hover {
  color: #409eff;
}
.header-right {
  display: flex;
  align-items: center;
}
.user-info {
  display: flex;
  align-items: center;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  transition: background 0.2s;
}
.user-info:hover {
  background: #f0f2f5;
}
.username {
  margin: 0 8px;
  font-size: 14px;
  color: #333;
}
.arrow {
  font-size: 12px;
  color: #999;
}
/* 页签导航栏 */
.tags-bar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  background: #fff;
  border-bottom: 1px solid #e6e6e6;
  padding: 8px 16px;
}
.tag-item {
  cursor: pointer;
}
.tag-active {
  border-color: #409eff;
  color: #409eff;
}
.main {
  background: #f0f2f5;
  padding: 20px;
}
</style>
