import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'

import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import ElementPlus  from 'element-plus'
import 'element-plus/dist/index.css'
// Element Plus 暗黑模式变量（html.dark 时生效）
import 'element-plus/theme-chalk/dark/css-vars.css'
// 主题变量体系（data-theme 切换）
import './styles/themes.css'
// 全局响应式样式（手机端适配）
import './styles/responsive.css'

const app = createApp(App)

app.use(createPinia())
app.use(router)

// 注册所有图标（可选）
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}
app.use(ElementPlus)

app.mount('#app')
