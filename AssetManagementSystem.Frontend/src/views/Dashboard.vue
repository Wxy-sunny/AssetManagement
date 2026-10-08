<template>
  <div class="dashboard" v-loading="loading">
    <!-- 顶部统计卡片 -->
    <el-row :gutter="16">
      <el-col v-for="card in statCards" :key="card.label" :xs="24" :sm="12" :md="6">
        <el-card shadow="hover" class="stat-card">
          <div class="stat-body">
            <div class="stat-icon" :style="{ backgroundColor: card.color }">
              <el-icon><component :is="card.icon" /></el-icon>
            </div>
            <div class="stat-meta">
              <div class="stat-label">{{ card.label }}</div>
              <div class="stat-value">{{ card.value }}</div>
              <div class="stat-sub">{{ card.sub }}</div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 近 30 天趋势 -->
    <el-card shadow="never" class="chart-card">
      <template #header>
        <div class="card-header">
          <span>近 30 天资产新增趋势</span>
          <el-tag type="info" size="small">柱：数量 / 线：金额</el-tag>
        </div>
      </template>
      <EChart :option="trendOption" height="320px" />
    </el-card>

    <!-- 状态分布 + 分类分布 -->
    <el-row :gutter="16">
      <el-col :xs="24" :md="10">
        <el-card shadow="never" class="chart-card">
          <template #header><span>资产状态分布</span></template>
          <EChart :option="statusOption" height="320px" />
        </el-card>
      </el-col>
      <el-col :xs="24" :md="14">
        <el-card shadow="never" class="chart-card">
          <template #header><span>资产分类分布（Top 8）</span></template>
          <EChart :option="categoryOption" height="320px" />
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Box, Money, Wallet, Calendar } from '@element-plus/icons-vue'
import EChart from '@/components/EChart.vue'
import { getDashboardStats } from '@/api/dashboard'
import type { DashboardData } from '@/types'

const loading = ref(false)
const stats = ref<DashboardData | null>(null)

// 状态对应的图表配色
const STATUS_COLOR: Record<string, string> = {
  in_use: '#67C23A',
  idle: '#909399',
  repair: '#E6A23C',
  scrapped: '#F56C6C',
}

// 金额：超过 1 万以「万元」展示
const formatAmount = (val?: number | null) => {
  const num = Number(val ?? 0)
  if (num >= 10000) return (num / 10000).toFixed(2)
  return num.toFixed(2)
}

const loadData = async () => {
  loading.value = true
  try {
    stats.value = await getDashboardStats()
  } catch {
    // 失败时拦截器已统一提示
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

// 统计卡片数据
const statCards = computed(() => {
  const d = stats.value
  return [
    {
      label: '资产总数',
      value: d ? `${d.assetTotal} 条` : '-',
      sub: d ? `数量合计 ${Number(d.assetQuantity ?? 0)}` : '',
      icon: Box,
      color: '#409EFF',
    },
    {
      label: '资产原值',
      value: d ? `¥${formatAmount(d.originalValue)} 万` : '-',
      sub: '按采购原值统计',
      icon: Money,
      color: '#67C23A',
    },
    {
      label: '账面净值',
      value: d ? `¥${formatAmount(d.netValue)} 万` : '-',
      sub: d ? `累计折旧 ¥${formatAmount(d.accumulatedDepreciation)} 万` : '',
      icon: Wallet,
      color: '#E6A23C',
    },
    {
      label: '本月新增',
      value: d ? `${d.monthNewCount} 条` : '-',
      sub: '按创建时间统计',
      icon: Calendar,
      color: '#F56C6C',
    },
  ]
})

// 趋势图：柱=数量，折线=金额（万元）
const trendOption = computed(() => {
  const trend = stats.value?.trend ?? []
  const dates = trend.map((t) => (t.date ?? '').slice(5)) // 只保留 MM-DD
  const counts = trend.map((t) => t.count ?? 0)
  const amounts = trend.map((t) => Number(((t.amount ?? 0) / 10000).toFixed(2)))

  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['新增数量', '新增金额(万)'], bottom: 0 },
    grid: { left: '3%', right: '3%', bottom: '12%', top: '8%', containLabel: true },
    xAxis: {
      type: 'category',
      data: dates,
      axisLabel: { interval: 4, rotate: 0 },
      axisLine: { lineStyle: { color: '#dcdfe6' } },
    },
    yAxis: [
      { type: 'value', name: '数量', axisLine: { show: false } },
      { type: 'value', name: '金额(万)', axisLine: { show: false }, splitLine: { show: false } },
    ],
    series: [
      {
        name: '新增数量',
        type: 'bar',
        data: counts,
        itemStyle: { color: '#409EFF', borderRadius: [3, 3, 0, 0] },
        barMaxWidth: 18,
      },
      {
        name: '新增金额(万)',
        type: 'line',
        yAxisIndex: 1,
        smooth: true,
        data: amounts,
        itemStyle: { color: '#67C23A' },
        areaStyle: { opacity: 0.15 },
      },
    ],
  }
})

// 状态分布：环形图
const statusOption = computed(() => {
  const list = stats.value?.statusStats ?? []
  return {
    tooltip: { trigger: 'item', formatter: '{b}: {c} 条 ({d}%)' },
    legend: { bottom: 0 },
    series: [
      {
        type: 'pie',
        radius: ['42%', '68%'],
        center: ['50%', '45%'],
        avoidLabelOverlap: true,
        itemStyle: { borderRadius: 6, borderColor: '#fff', borderWidth: 2 },
        label: { formatter: '{b}\n{c} 条' },
        data: list.map((s) => ({
          name: s.name ?? '未知',
          value: s.value ?? 0,
          itemStyle: { color: STATUS_COLOR[s.code ?? ''] ?? '#909399' },
        })),
      },
    ],
  }
})

// 分类分布：横向柱状图
const categoryOption = computed(() => {
  const list = stats.value?.categoryStats ?? []
  // 横向柱状图数据需倒序，让最大值显示在顶部
  const reversed = [...list].reverse()
  return {
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    grid: { left: '3%', right: '8%', bottom: '3%', top: '3%', containLabel: true },
    xAxis: { type: 'value', name: '数量', axisLine: { show: false } },
    yAxis: {
      type: 'category',
      data: reversed.map((c) => c.name ?? '未分类'),
      axisLine: { show: false },
      axisTick: { show: false },
    },
    series: [
      {
        type: 'bar',
        data: reversed.map((c) => c.value ?? 0),
        itemStyle: {
          borderRadius: [0, 4, 4, 0],
          color: '#409EFF',
        },
        barMaxWidth: 18,
        label: { show: true, position: 'right', fontSize: 11 },
      },
    ],
  }
})
</script>

<style scoped>
.dashboard {
  padding: 0;
}
.stat-card {
  margin-bottom: 16px;
}
.stat-body {
  display: flex;
  align-items: center;
  gap: 14px;
}
.stat-icon {
  width: 52px;
  height: 52px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 26px;
  flex-shrink: 0;
}
.stat-meta {
  min-width: 0;
}
.stat-label {
  font-size: 13px;
  color: #909399;
}
.stat-value {
  font-size: 22px;
  font-weight: 700;
  color: #303133;
  margin: 4px 0 2px;
  line-height: 1.2;
}
.stat-sub {
  font-size: 12px;
  color: #a8abb2;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.chart-card {
  margin-bottom: 16px;
}
.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
