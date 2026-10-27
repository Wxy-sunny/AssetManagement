import { defineStore } from 'pinia'

export type ThemeName = 'light' | 'dark' | 'green' | 'purple'

export interface ThemeOption {
  name: ThemeName
  label: string
  tip: string
}

/** 可选主题列表（顺序即下拉展示顺序） */
export const THEME_OPTIONS: ThemeOption[] = [
  { name: 'light', label: '默认蓝', tip: '经典浅色主题' },
  { name: 'dark', label: '暗黑模式', tip: '适合夜间或弱光环境' },
  { name: 'green', label: '清新绿', tip: '绿色品牌色' },
  { name: 'purple', label: '优雅紫', tip: '紫色品牌色' },
]

const STORAGE_KEY = 'ams-theme'

const isThemeName = (v: string | null): v is ThemeName =>
  v === 'light' || v === 'dark' || v === 'green' || v === 'purple'

export const useThemeStore = defineStore('theme', {
  state: () => ({
    // 刷新后从 localStorage 恢复；无记录或记录非法时用默认浅色
    theme: (isThemeName(localStorage.getItem(STORAGE_KEY))
      ? (localStorage.getItem(STORAGE_KEY) as ThemeName)
      : 'light') as ThemeName,
  }),

  getters: {
    currentOption: (state): ThemeOption =>
      THEME_OPTIONS.find((t) => t.name === state.theme) ?? THEME_OPTIONS[0]!,
  },

  actions: {
    /**
     * 应用主题：在 <html> 上设置 data-theme，并同步 Element Plus 暗黑模式所需的 dark 类。
     * 同时持久化到 localStorage，刷新后保持。
     */
    applyTheme(name?: ThemeName) {
      const target = name ?? this.theme
      this.theme = target
      localStorage.setItem(STORAGE_KEY, target)

      const root = document.documentElement
      root.setAttribute('data-theme', target)
      // Element Plus 暗黑组件样式依赖 html.dark 类（见 dark/css-vars.css）
      root.classList.toggle('dark', target === 'dark')
    },
  },
})
