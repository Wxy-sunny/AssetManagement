import { createRouter, createWebHistory } from 'vue-router'
import Login from '@/views/Login.vue'
import Layout from '@/views/Layout.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'Login',
      component: Login,
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
          meta: { title: '首页' },
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
          meta: { title: '资产列表' },
        },
        {
          path: '/categories',
          name: 'Categories',
          component: () => import('@/views/asset/CategoryList.vue'),
          meta: { title: '资产分类' },
        },
        // 资料管理
        {
          path: '/files',
          name: 'Files',
          component: () => import('@/views/file/FileManager.vue'),
          meta: { title: '资料管理' },
        },
        // 通知公告
        {
          path: '/notifications',
          name: 'NoticeBoard',
          component: () => import('@/views/notification/NoticeBoard.vue'),
          meta: { title: '系统通知' },
        },
        {
          path: '/system/notifications',
          name: 'NotificationManage',
          component: () => import('@/views/system/NotificationList.vue'),
          meta: { title: '通知管理' },
        },
        // 权限管理
        {
          path: '/system/users',
          name: 'Users',
          component: () => import('@/views/system/UserList.vue'),
          meta: { title: '用户管理' },
        },
        {
          path: '/system/roles',
          name: 'Roles',
          component: () => import('@/views/system/RoleList.vue'),
          meta: { title: '角色管理' },
        },
        {
          path: '/system/departments',
          name: 'Departments',
          component: () => import('@/views/system/DepartmentList.vue'),
          meta: { title: '部门管理' },
        },
      ],
    },
  ],
})

// 导航守卫：未登录跳转 login，已登录访问 login 跳转 dashboard
router.beforeEach((to, from, next) => {
  const token = localStorage.getItem('token')
  if (to.path === '/login') {
    if (token) next('/dashboard')
    else next()
  } else {
    if (token) next()
    else next('/login')
  }
})

export default router
