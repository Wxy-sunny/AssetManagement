import { defineStore } from 'pinia';
import router from '@/router';

// 从 localStorage 恢复 userInfo
const getUserInfoFromStorage = () => {
  const info = localStorage.getItem('userInfo');
  if (info) {
    try {
      return JSON.parse(info);
    } catch {
      return null;
    }
  }
  return null;
};

export const useUserStore = defineStore('user', {
  state: () => ({
    token: localStorage.getItem('token') || '',
    userInfo: getUserInfoFromStorage() || null,   // 存储用户信息对象
  }),
  getters: {
    displayName: (state) => state.userInfo?.nickName || state.userInfo?.userName || '用户',
    // 当前用户拥有的权限编码集合
    permissions: (state): string[] => state.userInfo?.permissions ?? [],
    // 是否超级管理员（拥有全部权限）
    isAdmin: (state): boolean => state.userInfo?.isAdmin === true,
  },
  actions: {
    setToken(token: string) {
      this.token = token;
      localStorage.setItem('token', token);
    },
    setUserInfo(info: any) {
      this.userInfo = info;
      // 持久化到 localStorage，保证刷新页面后权限信息不丢失
      if (info) {
        localStorage.setItem('userInfo', JSON.stringify(info));
      } else {
        localStorage.removeItem('userInfo');
      }
    },
    /**
     * 判断是否拥有某项功能权限
     * 超级管理员恒为 true
     */
    hasPermission(code: string): boolean {
      if (this.isAdmin) return true;
      return this.permissions.includes(code);
    },
    logout() {
      this.token = '';
      this.userInfo = null;
      localStorage.removeItem('token');
      localStorage.removeItem('userInfo');
      // 如果当前不在登录页，则跳转
      if (router.currentRoute.value.path !== '/login') {
        router.push('/login');
      }
    },
  },
});
