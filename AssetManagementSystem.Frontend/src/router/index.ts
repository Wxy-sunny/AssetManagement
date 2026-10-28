import { createRouter, createWebHistory } from 'vue-router'
import Login from '@/views/Login.vue'
import Layout from '@/views/Layout.vue'
import { PERMISSIONS } from '@/types'

/**
 * 从 localStorage 读取当前用户信息。
 * 路由守卫执行时机早于组件渲染，这里直接读存储，避免 Pinia 初始化时序问题
 * （与 stores/user.ts 中的 getUserInfoFromStorage 逻辑保持一致）。
 */
const getStoredUserInfo = (): any => {
  try {
    const raw = localStorage.getItem('userInfo')
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
}

/**
 * 判断当前登录用户是否拥有指定权限。
 * 超级管理员恒为 true；未声明权限的页面一律放行。
 */
export const hasPermission = (code?: string | null): boolean => {
  if (!code) return true
  const info = getStoredUserInfo()
  if (!info) return false
  if (info.isAdmin === true) return true
  const permissions: string[] = Array.isArray(info.permissions) ? info.permissions : []
  return permissions.includes(code)
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'Login',
      component: Login,
    },
    {
      path: '/403',
      name: 'Forbidden',
      component: () => import('@/views/error/Forbidden.vue'),
      meta: { title: '无访问权限' },
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'NotFound',
      component: () => import('@/views/error/NotFound.vue'),
      meta: { title: '页面不存在' },
    },
    {
      path: '/',
      component: Layout,
      redirect: '/dashboard',
      children: [
        // 首页
        {
          path: '/dashboard',
          name: 'Dashboard',
          component: () => import('@/views/Dashboard.vue'),
          meta: { title: '首页', permission: PERMISSIONS.DashboardView },
        },
        // 个人中心（个人资料 / 修改密码）
        {
          path: '/profile',
          name: 'Profile',
          component: () => import('@/views/system/Profile.vue'),
          meta: { title: '个人资料' },
        },
        // 信息管理
        {
          path: '/assets',
          name: 'Assets',
          component: () => import('@/views/asset/AssetList.vue'),
          meta: { title: '资产列表', permission: PERMISSIONS.AssetList },
        },
        {
          path: '/categories',
          name: 'Categories',
          component: () => import('@/views/asset/CategoryList.vue'),
          meta: { title: '资产分类', permission: PERMISSIONS.CategoryList },
        },
        // 资料管理
        {
          path: '/files',
          name: 'Files',
          component: () => import('@/views/file/FileManager.vue'),
          meta: { title: '资料管理', permission: PERMISSIONS.FileList },
        },
        // 通知公告
        {
          path: '/notifications',
          name: 'NoticeBoard',
          component: () => import('@/views/notification/NoticeBoard.vue'),
          meta: { title: '系统通知' },
        },
        // 我的消息（个人收件箱，登录即可）
        {
          path: '/messages',
          name: 'MyMessages',
          component: () => import('@/views/message/MyMessages.vue'),
          meta: { title: '我的消息' },
        },
        {
          path: '/system/notifications',
          name: 'NotificationManage',
          component: () => import('@/views/system/NotificationList.vue'),
          meta: { title: '通知管理', permission: PERMISSIONS.NotificationList },
        },
        // 权限管理
        {
          path: '/system/users',
          name: 'Users',
          component: () => import('@/views/system/UserList.vue'),
          meta: { title: '用户管理', permission: PERMISSIONS.UserList },
        },
        {
          path: '/system/roles',
          name: 'Roles',
          component: () => import('@/views/system/RoleList.vue'),
          meta: { title: '角色管理', permission: PERMISSIONS.RoleList },
        },
        {
          path: '/system/departments',
          name: 'Departments',
          component: () => import('@/views/system/DepartmentList.vue'),
          meta: { title: '部门管理', permission: PERMISSIONS.DeptList },
        },
        {
          path: '/system/requests',
          name: 'ContactRequests',
          component: () => import('@/views/system/ContactRequestList.vue'),
          meta: { title: '联系请求', permission: PERMISSIONS.RequestList },
        },
      ],
    },
  ],
})

// 导航守卫：未登录跳转 login，已登录访问 login 跳转 dashboard；
// 目标页面声明了 permission 且当前用户没有该权限时，跳转到 403 页
router.beforeEach((to, from, next) => {
  const token = localStorage.getItem('token')
  if (to.path === '/login') {
    if (token) next('/dashboard')
    else next()
    return
  }
  if (!token) {
    next('/login')
    return
  }
  const need = to.meta?.permission as string | undefined
  if (need && !hasPermission(need)) {
    next({ path: '/403', query: { from: to.fullPath } })
    return
  }
  next()
})

export default router
