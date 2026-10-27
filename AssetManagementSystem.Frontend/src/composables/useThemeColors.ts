import { nextTick, ref, watch } from 'vue'
import { useThemeStore } from '@/stores/theme'

/**
 * 读取当前主题的 CSS 变量值。
 * 用途：ECharts 的 option 是 JS 对象，无法直接使用 CSS 变量，
 * 通过此 composable 在主题切换后重新读取，实现图表配色跟随主题。
 */
export function useThemeColors() {
  const themeStore = useThemeStore()

  const read = () => {
    const style = getComputedStyle(document.documentElement)
    const pick = (name: string, fallback: string) =>
      style.getPropertyValue(name).trim() || fallback
    return {
      border: pick('--ams-border', '#dcdfe6'),
      textRegular: pick('--ams-text-regular', '#606266'),
      textMain: pick('--ams-text-main', '#303133'),
      cardBg: pick('--ams-card-bg', '#ffffff'),
      primary: pick('--ams-primary', '#409eff'),
    }
  }

  const colors = ref(read())

  // 主题切换后重新读取（nextTick 确保 data-theme 已生效）
  watch(
    () => themeStore.theme,
    async () => {
      await nextTick()
      colors.value = read()
    },
  )

  return colors
}
