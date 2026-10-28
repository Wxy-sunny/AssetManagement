<template>
  <div class="intro-page">
    <header class="intro-header">
      <div class="intro-logo">
        <img :src="logoUrl" alt="资产管理系统" />
        <span>资产管理系统</span>
      </div>
      <div class="intro-actions">
        <el-button text @click="goBack">返回</el-button>
        <el-button type="primary" @click="goLogin">进入系统</el-button>
      </div>
    </header>

    <section class="hero">
      <el-tag class="hero-tag" effect="dark" round>资产全生命周期 · 数字化管理平台</el-tag>
      <h1 class="hero-title">资产管理系统</h1>
      <p class="hero-desc">
        覆盖资产登记、分类归档、折旧统计、资料归档与权限管控的一体化平台。
        支持多主题界面、角色级权限、文件管理与站内消息，手机端也能用手机号登录。
      </p>
      <div class="hero-badges">
        <span v-for="t in highlights" :key="t.label" class="hero-badge">
          <el-icon><component :is="t.icon" /></el-icon>
          {{ t.label }}
        </span>
      </div>
      <div class="hero-actions">
        <el-button type="primary" size="large" @click="goLogin">立即体验</el-button>
        <el-button size="large" @click="scrollTo('gallery')">查看界面</el-button>
      </div>
    </section>

    <section class="section">
      <h2 class="section-title">核心能力</h2>
      <div class="feature-grid">
        <div v-for="f in features" :key="f.title" class="feature-card">
          <div class="feature-icon" :style="{ background: f.color }">
            <el-icon :size="20"><component :is="f.icon" /></el-icon>
          </div>
          <h3 class="feature-title">{{ f.title }}</h3>
          <p class="feature-desc">{{ f.desc }}</p>
        </div>
      </div>
    </section>

    <section id="gallery" class="section">
      <h2 class="section-title">界面预览</h2>
      <p class="section-sub">点击任意截图可放大查看</p>
      <div v-for="group in gallery" :key="group.title" class="gallery-group">
        <h3 class="group-title">{{ group.title }}</h3>
        <p class="group-desc">{{ group.desc }}</p>
        <div class="gallery-grid">
          <figure v-for="shot in group.shots" :key="shot.src" class="shot">
            <el-image
              :src="shot.src"
              :preview-src-list="[shot.src]"
              preview-teleported
              fit="contain"
              :alt="shot.title"
              class="shot-img"
            />
            <figcaption class="shot-caption">{{ shot.title }}</figcaption>
          </figure>
        </div>
      </div>
    </section>

    <section class="section">
      <h2 class="section-title">技术架构</h2>
      <div class="tech-grid">
        <div v-for="t in tech" :key="t.name" class="tech-card">
          <div class="tech-name">{{ t.name }}</div>
          <div class="tech-desc">{{ t.desc }}</div>
        </div>
      </div>
    </section>

    <footer class="intro-footer">
      <span>资产管理系统 · 前后端分离 · Docker 一键部署</span>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Box, Files, Lock, Bell, TrendCharts, Picture } from '@element-plus/icons-vue'
import logoUrl from '@/assets/logo.png'
import loginRight from '@/assets/screenshots/login-purple-right.png'
import loginCenter from '@/assets/screenshots/login-purple-center.png'
import loginMobile from '@/assets/screenshots/login-blue-mobile.png'
import homeBlue from '@/assets/screenshots/home-blue.png'
import homeDark from '@/assets/screenshots/home-dark.png'
import sidebarCollapsed from '@/assets/screenshots/sidebar-collapsed.png'
import profileAvatar from '@/assets/screenshots/profile-avatar.png'

const router = useRouter()

const highlights = [
  { label: '资产全生命周期', icon: Box },
  { label: '角色权限管控', icon: Lock },
  { label: '文件资料归档', icon: Files },
  { label: '站内消息通知', icon: Bell },
  { label: '折旧统计分析', icon: TrendCharts },
]

const features = [
  { title: '资产台账', desc: '登记、编辑、批量维护资产档案，按分类与部门归档，支持关键字检索与状态筛选。', icon: Box, color: '#409eff' },
  { title: '折旧统计', desc: '按年限与残值自动计算折旧，一键重算并以图表呈现资产净值分布。', icon: TrendCharts, color: '#67c23a' },
  { title: '分类体系', desc: '多级资产分类树，支持新增下级分类并绑定到资产，便于按类统计。', icon: Files, color: '#e6a23c' },
  { title: '权限管理', desc: '用户、角色、部门三级管理，21 项功能权限自由勾选，无权限的入口前端直接隐藏。', icon: Lock, color: '#f56c6c' },
  { title: '资料管理', desc: '上传归档合同、发票、说明书等附件，支持在线预览与批量清理。', icon: Picture, color: '#909399' },
  { title: '消息通知', desc: '站内通知公告与个人消息，支持发布、撤回与已读状态跟踪。', icon: Bell, color: '#14b8a6' },
]

const gallery = [
  {
    title: '登录界面',
    desc: '支持账号密码、手机号验证码两种登录方式；登录框位置可左 / 中 / 右切换，并提供浅紫、深蓝、墨绿、暗黑四套主题。',
    shots: [
      { src: loginRight, title: '登录页 · 右侧布局' },
      { src: loginCenter, title: '登录页 · 居中布局' },
      { src: loginMobile, title: '手机号验证码登录' },
    ],
  },
  {
    title: '主界面',
    desc: '资产总览仪表盘、资产列表与统计图表，支持浅色与暗黑主题一键切换，侧边菜单可收起。',
    shots: [
      { src: homeBlue, title: '主界面 · 浅色主题' },
      { src: homeDark, title: '主界面 · 暗黑主题' },
      { src: sidebarCollapsed, title: '菜单栏收起状态' },
    ],
  },
  {
    title: '个人中心',
    desc: '修改个人资料与登录密码，并可从系统内置头像库挑选或上传自定义头像。',
    shots: [
      { src: profileAvatar, title: '个人资料 · 头像设置' },
    ],
  },
]

const tech = [
  { name: '前端', desc: 'Vue 3 + TypeScript + Vite + Element Plus' },
  { name: '后端', desc: '.NET 10 + ASP.NET Core Web API + SqlSugar' },
  { name: '数据库', desc: 'SQLite（内置）/ SQL Server（可选），同一套代码跨库兼容' },
  { name: '缓存', desc: 'Redis 承载验证码与热点缓存，异常时自动降级为内存' },
  { name: '部署', desc: 'Docker Compose 一键编排，前端 nginx 反代并对外提供 80 端口' },
  { name: '安全', desc: 'BCrypt 哈希、JWT 校验、登录防爆破、速率限制与安全响应头' },
]

const goLogin = () => router.push('/login')
const goBack = () => (window.history.length > 1 ? router.back() : router.push('/login'))
const scrollTo = (id: string) => {
  const el = document.getElementById(id)
  if (el) el.scrollIntoView({ behavior: 'smooth' })
}
</script>

<style scoped>
/* 系统介绍页：配色全部走主题变量，浅色/暗黑主题都能自适应 */
.intro-page {
  min-height: 100vh;
  background: var(--ams-body-bg, #f0f2f5);
  color: var(--ams-text-regular, #606266);
}

.intro-header {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 32px;
  background: var(--ams-card-bg, #fff);
  box-shadow: 0 1px 6px rgba(0, 0, 0, 0.06);
}
.intro-logo {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 16px;
  font-weight: 600;
  color: var(--ams-text-primary, #303133);
}
.intro-logo img {
  width: 30px;
  height: 30px;
  border-radius: 8px;
}

.hero {
  padding: 64px 32px 48px;
  text-align: center;
  background: linear-gradient(180deg, rgba(64, 158, 255, 0.12), transparent);
}
.hero-tag {
  margin-bottom: 18px;
}
.hero-title {
  margin: 0 0 14px;
  font-size: 40px;
  font-weight: 700;
  letter-spacing: 2px;
  color: var(--ams-text-primary, #303133);
}
.hero-desc {
  max-width: 720px;
  margin: 0 auto 22px;
  font-size: 15px;
  line-height: 1.9;
}
.hero-badges {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 10px;
  margin-bottom: 26px;
}
.hero-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: 999px;
  font-size: 13px;
  background: var(--ams-card-bg, #fff);
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);
}
.hero-actions {
  display: flex;
  justify-content: center;
  gap: 12px;
}

.section {
  max-width: 1160px;
  margin: 0 auto;
  padding: 40px 32px;
}
.section-title {
  margin: 0 0 8px;
  font-size: 22px;
  font-weight: 600;
  color: var(--ams-text-primary, #303133);
}
.section-sub {
  margin: 0 0 22px;
  font-size: 13px;
  opacity: 0.7;
}

.feature-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 18px;
}
.feature-card {
  padding: 22px;
  border-radius: 14px;
  background: var(--ams-card-bg, #fff);
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.05);
  transition: transform 0.25s, box-shadow 0.25s;
}
.feature-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 22px rgba(0, 0, 0, 0.1);
}
.feature-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 12px;
  color: #fff;
  margin-bottom: 12px;
}
.feature-title {
  margin: 0 0 8px;
  font-size: 16px;
  color: var(--ams-text-primary, #303133);
}
.feature-desc {
  margin: 0;
  font-size: 13px;
  line-height: 1.8;
  opacity: 0.85;
}

.gallery-group {
  margin-bottom: 38px;
}
.group-title {
  margin: 0 0 6px;
  font-size: 17px;
  color: var(--ams-text-primary, #303133);
}
.group-desc {
  margin: 0 0 16px;
  font-size: 13px;
  line-height: 1.8;
  opacity: 0.75;
}
.gallery-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 20px;
}
.shot {
  margin: 0;
  border-radius: 14px;
  overflow: hidden;
  background: var(--ams-card-bg, #fff);
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.06);
}
.shot-img {
  display: block;
  width: 100%;
  aspect-ratio: 16 / 10;
  cursor: zoom-in;
  /* contain 完整展示界面截图，避免裁掉两侧内容 */
  padding: 10px;
  background: var(--ams-body-bg, #f0f2f5);
}
.shot-caption {
  padding: 12px 14px;
  font-size: 13px;
}

.tech-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 14px;
}
.tech-card {
  padding: 16px 18px;
  border-radius: 12px;
  background: var(--ams-card-bg, #fff);
  box-shadow: 0 1px 6px rgba(0, 0, 0, 0.05);
}
.tech-name {
  margin-bottom: 6px;
  font-size: 14px;
  font-weight: 600;
  color: var(--ams-text-primary, #303133);
}
.tech-desc {
  font-size: 13px;
  opacity: 0.8;
}

.intro-footer {
  padding: 26px 32px 40px;
  text-align: center;
  font-size: 13px;
  opacity: 0.6;
}
</style>
