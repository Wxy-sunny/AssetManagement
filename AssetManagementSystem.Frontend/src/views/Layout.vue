<template>
  <el-container class="layout-container">
    <!-- 桌面端：左侧菜单（窄屏隐藏，改为抽屉菜单） -->
    <el-aside v-if="!isMobile" :width="collapsed ? '64px' : '200px'" class="aside">
      <div class="logo">
        <img class="logo-icon" :src="logoUrl" alt="logo" />
        <span v-if="!collapsed">资产管理系统</span>
      </div>
      <SideMenu :collapsed="collapsed" />
    </el-aside>

    <!-- 右侧主体 -->
    <el-container>
      <!-- 顶部导航 -->
      <el-header class="header">
        <div class="header-left">
          <!-- 移动端：汉堡菜单（打开抽屉） -->
          <el-tooltip v-if="isMobile" content="打开菜单" placement="bottom">
            <el-icon class="collapse-btn" @click="drawerOpen = true">
              <Menu />
            </el-icon>
          </el-tooltip>
          <!-- 桌面端：折叠 / 展开侧边栏 -->
          <el-tooltip v-else :content="collapsed ? '展开菜单' : '收起菜单'" placement="bottom">
            <el-icon class="collapse-btn" @click="collapsed = !collapsed">
              <Expand v-if="collapsed" />
              <Fold v-else />
            </el-icon>
          </el-tooltip>
        </div>
        <div class="header-right">
          <!-- 主题切换（tooltip 包在 dropdown 外层，避免干扰其触发器） -->
          <el-tooltip :content="'当前主题：' + themeStore.currentOption.label" placement="bottom">
            <el-dropdown trigger="click" @command="onThemeCommand">
              <el-icon class="header-btn">
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

          <!-- 用户下拉菜单 -->
          <el-dropdown trigger="click" @command="handleCommand">
            <div class="user-info">
              <!-- 头像：如果有头像图片则显示图片，否则显示图标 -->
              <el-avatar :size="36" :src="topAvatar" @error="onTopAvatarError">
                <el-icon><User /></el-icon>
              </el-avatar>
              <span class="username">{{ userStore.displayName }}</span>
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

      <!-- 页签导航（访问过的页面，可切换 / 关闭；窄屏横向滚动） -->
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

  <!-- 移动端：抽屉菜单（从左侧滑出） -->
  <el-drawer
    v-model="drawerOpen"
    direction="ltr"
    size="220px"
    :with-header="false"
    class="menu-drawer"
  >
    <div class="drawer-logo">
      <img class="drawer-logo-icon" :src="logoUrl" alt="logo" />
      <span>资产管理系统</span>
    </div>
    <SideMenu :collapsed="false" @navigate="drawerOpen = false" />
  </el-drawer>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter, type RouteLocationNormalizedLoaded } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  User,
  Bell,
  Lock,
  SwitchButton,
  ArrowDown,
  Fold,
  Expand,
  Menu,
  Sunny,
  Moon,
} from '@element-plus/icons-vue'
import SideMenu from '@/components/SideMenu.vue'
import { useUserStore } from '@/stores/user'
import { useThemeStore, THEME_OPTIONS, type ThemeName } from '@/stores/theme'
import request from '@/utils/request'
import logoUrl from '@/assets/logo.png'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const themeStore = useThemeStore()

// 顶栏头像：加载失败（如头像地址失效）时回退为图标
const topAvatar = ref(userStore.userInfo?.avatar || '')
watch(
  () => userStore.userInfo?.avatar,
  (v) => { topAvatar.value = v || '' },
)
const onTopAvatarError = () => { topAvatar.value = '' }

// ============ 主题切换 ============
const onThemeCommand = (name: ThemeName) => {
  themeStore.applyTheme(name)
}

// ============ 移动端适配 ============
// ≤768px 视为手机：侧边栏改为抽屉菜单
const mobileQuery = window.matchMedia('(max-width: 768px)')
const isMobile = ref(mobileQuery.matches)
const drawerOpen = ref(false)

const onMobileChange = (e: MediaQueryListEvent) => {
  isMobile.value = e.matches
  // 回到桌面宽度时自动关闭抽屉，避免状态残留
  if (!e.matches) drawerOpen.value = false
}
mobileQuery.addEventListener('change', onMobileChange)
onBeforeUnmount(() => mobileQuery.removeEventListener('change', onMobileChange))

// ============ 侧边栏折叠（桌面端） ============
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
      }).then(async () => {
        // 通知服务端作废刷新令牌（失败也不影响本地退出）
        const refreshToken = localStorage.getItem('refreshToken')
        if (refreshToken) {
          try {
            await request.post('/auth/logout', { refreshToken })
          } catch {
            // 网络异常等：本地照常退出
          }
        }
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
  background-color: var(--ams-sidebar-bg);
  color: var(--ams-sidebar-text);
  transition: width 0.25s;
  overflow-x: hidden;
}
.logo {
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  font-size: 18px;
  font-weight: bold;
  color: #fff;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  white-space: nowrap;
  overflow: hidden;
}
.logo-icon {
  width: 30px;
  height: 30px;
  border-radius: 6px;
  flex-shrink: 0;
}
.drawer-logo {
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  font-size: 18px;
  font-weight: bold;
  color: var(--ams-primary);
  border-bottom: 1px solid var(--ams-border);
}
.drawer-logo-icon {
  width: 28px;
  height: 28px;
  border-radius: 6px;
}
.header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: var(--ams-header-bg);
  color: var(--ams-header-text);
  border-bottom: 1px solid var(--ams-tagsbar-border);
  padding: 0 20px;
}
.header-left {
  display: flex;
  align-items: center;
}
.collapse-btn {
  font-size: 20px;
  color: var(--ams-header-text);
  cursor: pointer;
}
.collapse-btn:hover {
  color: var(--ams-primary);
}
.header-right {
  display: flex;
  align-items: center;
  gap: 4px;
}
.header-btn {
  font-size: 18px;
  color: var(--ams-header-text);
  cursor: pointer;
  padding: 6px;
  border-radius: 4px;
}
.header-btn:hover {
  color: var(--ams-primary);
  background: var(--ams-hover-bg);
}
.theme-label {
  margin-right: 8px;
}
.theme-tip {
  font-size: 12px;
  color: var(--ams-text-regular);
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
  background: var(--ams-hover-bg);
}
.username {
  margin: 0 8px;
  font-size: 14px;
  color: var(--ams-text-main);
}
.arrow {
  font-size: 12px;
  color: var(--ams-text-regular);
}
/* 页签导航栏 */
.tags-bar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  background: var(--ams-tagsbar-bg);
  border-bottom: 1px solid var(--ams-tagsbar-border);
  padding: 8px 16px;
}
.tag-item {
  cursor: pointer;
}
.tag-active {
  border-color: var(--ams-primary);
  color: var(--ams-primary);
}
.main {
  background: var(--ams-body-bg);
  padding: 20px;
}

/* 抽屉内的菜单撑满高度 */
.menu-drawer :deep(.el-drawer__body) {
  padding: 0;
  display: flex;
  flex-direction: column;
}

/* ---------- 手机端（≤768px）---------- */
@media (max-width: 768px) {
  /* 隐藏用户名文字，只留头像，节省头部空间 */
  .username {
    display: none;
  }
  .header {
    padding: 0 12px;
  }
  /* 页签栏改为横向滚动（不换行），并隐藏滚动条 */
  .tags-bar {
    flex-wrap: nowrap;
    overflow-x: auto;
    padding: 8px 12px;
    scrollbar-width: none;
  }
  .tags-bar::-webkit-scrollbar {
    display: none;
  }
  .tag-item {
    flex-shrink: 0;
  }
}
</style>
