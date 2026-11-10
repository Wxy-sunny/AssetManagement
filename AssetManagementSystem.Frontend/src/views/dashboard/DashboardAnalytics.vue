<template>
  <div class="analytics" v-loading="loading">
    <!-- 顶部 KPI -->
    <el-row :gutter="14">
      <el-col v-for="card in kpiCards" :key="card.label" :xs="12" :sm="8" :md="4">
        <el-card shadow="hover" class="kpi-card">
          <div class="kpi-label">{{ card.label }}</div>
          <div class="kpi-value" :style="{ color: card.color }">{{ card.value }}</div>
          <div class="kpi-sub">{{ card.sub }}</div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 第一行：利用率仪表盘 + 状态分布 + 分类 Top10 -->
    <el-row :gutter="14">
      <el-col :xs="24" :sm="24" :md="7">
        <el-card shadow="never" class="chart-card">
          <template #header><span>资产利用率</span></template>
          <EChart :option="utilizationOption" height="290px" />
          <div class="chart-foot">
            在用 {{ data.inUseCount }} ｜ 闲置 {{ data.idleCount }} ｜ 维修 {{ data.repairCount }} ｜ 报废
            {{ data.scrappedCount }}
          </div>
        </el-card>
      </el-col>
      <el-col :xs="24" :sm="24" :md="7">
        <el-card shadow="never" class="chart-card">
          <template #header><span>资产状态分布</span></template>
          <EChart :option="statusOption" height="290px" />
          <div class="chart-foot">
            闲置率 {{ data.idleRate }}% ｜ 维修率 {{ data.repairRate }}% ｜ 报废率 {{ data.scrapRate }}%
          </div>
        </el-card>
      </el-col>
      <el-col :xs="24" :sm="24" :md="10">
        <el-card shadow="never" class="chart-card">
          <template #header><span>资产分类 Top 10（数量 / 原值）</span></template>
          <EChart :option="categoryOption" height="320px" />
        </el-card>
      </el-col>
    </el-row>

    <!-- 第二行：近 12 月趋势 + 部门分布 -->
    <el-row :gutter="14">
      <el-col :xs="24" :md="14">
        <el-card shadow="never" class="chart-card">
          <template #header>
            <div class="card-header">
              <span>近 12 个月资产新增趋势</span>
              <el-tag type="info" size="small">柱：数量 / 线：金额(万元)</el-tag>
            </div>
          </template>
          <EChart :option="trendOption" height="300px" />
        </el-card>
      </el-col>
      <el-col :xs="24" :md="10">
        <el-card shadow="never" class="chart-card">
          <template #header><span>部门资产分布 Top 10</span></template>
          <EChart :option="deptOption" height="300px" />
        </el-card>
      </el-col>
    </el-row>

    <!-- 第三行：折旧方式 + 流转单据 + 运营健康度 -->
    <el-row :gutter="14">
      <el-col :xs="24" :md="8">
        <el-card shadow="never" class="chart-card">
          <template #header><span>折旧方式分布</span></template>
          <EChart :option="depreciationOption" height="260px" />
        </el-card>
      </el-col>
      <el-col :xs="24" :md="8">
        <el-card shadow="never" class="chart-card">
          <template #header>
            <div class="card-header">
              <span>业务流转单据</span>
              <el-tag v-if="data.flowPendingCount > 0" type="warning" size="small">
                {{ data.flowPendingCount }} 单待处理
              </el-tag>
            </div>
          </template>
          <EChart :option="flowOption" height="260px" />
        </el-card>
      </el-col>
      <el-col :xs="24" :md="8">
        <el-card shadow="never" class="chart-card">
          <template #header><span>运营健康度</span></template>
          <div class="health-list">
            <div v-for="h in healthItems" :key="h.label" class="health-item">
              <div class="health-head">
                <span>{{ h.label }}</span>
                <b :style="{ color: h.color }">{{ h.value }}%</b>
              </div>
              <el-progress
                :percentage="Math.min(100, Number(h.value))"
                :color="h.color"
                :stroke-width="9"
                :show-text="false"
              />
              <div class="health-tip">{{ h.tip }}</div>
            </div>
          </div>
          <div class="health-foot">
            <el-button type="primary" link :icon="Tickets" @click="goFlowList">去处理流转单据</el-button>
            <el-button type="primary" link :icon="Document" @click="goReports">查看报表中心</el-button>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Tickets, Document } from '@element-plus/icons-vue'
import EChart from '@/components/EChart.vue'
import { getDashboardOverview } from '@/api/dashboard'
import { useThemeColors } from '@/composables/useThemeColors'
import { FLOW_TYPE_MAP, ASSET_STATUS_MAP, DEPRECIATION_METHOD_MAP, type DashboardOverview } from '@/types'

const router = useRouter()
const colors = useThemeColors()
const goFlowList = () => router.push('/flows')
const goReports = () => router.push('/reports')

const loading = ref(false)
const data = ref<DashboardOverview>({
  assetTotal: 0,
  assetQuantity: 0,
  originalValue: 0,
  accumulatedDepreciation: 0,
  netValue: 0,
  monthNewCount: 0,
  inUseCount: 0,
  idleCount: 0,
  repairCount: 0,
  scrappedCount: 0,
  utilizationRate: 0,
  idleRate: 0,
  repairRate: 0,
  scrapRate: 0,
  depreciationRate: 0,
  statusStats: [],
  categoryStats: [],
  deptStats: [],
  depreciationMethodStats: [],
  monthlyTrend: [],
  flowTypeStats: [],
  flowPendingCount: 0,
  inventoryStats: [],
  inventoryAccuracyRate: 0,
})

const STATUS_COLOR: Record<string, string> = {
  in_use: '#67C23A',
  idle: '#909399',
  repair: '#E6A23C',
  scrapped: '#F56C6C',
}

const PALETTE = ['#409EFF', '#67C23A', '#E6A23C', '#F56C6C', '#909399', '#9254DE', '#36CFC9', '#FF85C0']

const toWan = (val?: number | null) => Number(((val ?? 0) / 10000).toFixed(2))
const formatWan = (val?: number | null) => `¥${toWan(val).toLocaleString('zh-CN')} 万`

const loadData = async () => {
  loading.value = true
  try {
    const res = await getDashboardOverview()
    if (res) data.value = res
  } catch {
    // 拦截器已统一提示
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

const kpiCards = computed(() => [
  {
    label: '资产总数',
    value: `${data.value.assetTotal} 条`,
    sub: `数量合计 ${data.value.assetQuantity}`,
    color: 'var(--ams-primary, #409eff)',
  },
  {
    label: '资产原值',
    value: formatWan(data.value.originalValue),
    sub: '按采购原值统计',
    color: '#67C23A',
  },
  {
    label: '账面净值',
    value: formatWan(data.value.netValue),
    sub: `累计折旧 ${formatWan(data.value.accumulatedDepreciation)}`,
    color: '#E6A23C',
  },
  {
    label: '本月新增',
    value: `${data.value.monthNewCount} 条`,
    sub: '按创建时间统计',
    color: '#F56C6C',
  },
  {
    label: '资产利用率',
    value: `${data.value.utilizationRate}%`,
    sub: `在用 ${data.value.inUseCount} / 总 ${data.value.assetTotal}`,
    color: '#9254DE',
  },
  {
    label: '待处理报修',
    value: `${data.value.flowPendingCount} 单`,
    sub: '维修完成后可闭环',
    color: data.value.flowPendingCount > 0 ? '#E6A23C' : '#67C23A',
  },
])

const healthItems = computed(() => [
  {
    label: '资产利用率',
    value: data.value.utilizationRate,
    color: '#409EFF',
    tip: '在用资产占全部资产的比例，越高说明闲置越少',
  },
  {
    label: '资产折旧进度',
    value: data.value.depreciationRate,
    color: '#E6A23C',
    tip: '累计折旧占原值比例，反映资产新旧程度',
  },
  {
    label: '最近盘点准确率',
    value: data.value.inventoryAccuracyRate,
    color: '#67C23A',
    tip: '最近一次已完成盘点的账实相符比例',
  },
])

const axisBase = computed(() => ({
  axisLabel: { color: colors.value.textRegular },
  axisLine: { lineStyle: { color: colors.value.border } },
  nameTextStyle: { color: colors.value.textRegular },
}))

// 仪表盘：资产利用率
const utilizationOption = computed(() => ({
  tooltip: { formatter: '{a}: {c}%' },
  series: [
    {
      name: '资产利用率',
      type: 'gauge',
      radius: '92%',
      center: ['50%', '55%'],
      startAngle: 210,
      endAngle: -30,
      min: 0,
      max: 100,
      progress: { show: true, width: 14, roundCap: true },
      axisLine: { lineStyle: { width: 14, color: [[1, colors.value.border]] } },
      pointer: { show: false },
      axisTick: { show: false },
      splitLine: { show: false },
      axisLabel: { show: false },
      anchor: { show: false },
      title: { offsetCenter: [0, '30%'], color: colors.value.textRegular, fontSize: 12 },
      detail: {
        valueAnimation: true,
        offsetCenter: [0, '-5%'],
        fontSize: 30,
        fontWeight: 'bold',
        formatter: '{value}%',
        color: colors.value.primary,
      },
      data: [{ value: data.value.utilizationRate, name: '在用资产占比' }],
    },
  ],
}))

// 状态分布：环形图
const statusOption = computed(() => ({
  tooltip: { trigger: 'item', formatter: '{b}: {c} 条 ({d}%)' },
  legend: { bottom: 0, textStyle: { color: colors.value.textRegular } },
  color: PALETTE,
  series: [
    {
      type: 'pie',
      radius: ['40%', '68%'],
      center: ['50%', '44%'],
      avoidLabelOverlap: true,
      itemStyle: { borderRadius: 6, borderColor: colors.value.cardBg, borderWidth: 2 },
      label: { formatter: '{b}\n{c} 条', color: colors.value.textRegular, fontSize: 11 },
      data: data.value.statusStats.map((s) => ({
        name: s.name || ASSET_STATUS_MAP[s.code || ''] || '未知',
        value: s.value ?? 0,
        itemStyle: { color: STATUS_COLOR[s.code || ''] ?? '#909399' },
      })),
    },
  ],
}))

// 分类 Top10：横向双系列
const categoryOption = computed(() => {
  const list = [...data.value.categoryStats].slice(0, 10).reverse()
  return {
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    legend: {
      data: ['资产数量', '原值(万)'],
      bottom: 0,
      textStyle: { color: colors.value.textRegular },
    },
    grid: { left: '3%', right: '8%', bottom: '12%', top: '4%', containLabel: true },
    xAxis: [
      { type: 'value', name: '数量', ...axisBase.value, splitLine: { lineStyle: { color: colors.value.border } } },
      { type: 'value', name: '原值(万)', ...axisBase.value, splitLine: { show: false } },
    ],
    yAxis: {
      type: 'category',
      data: list.map((c) => c.name || c.code || '未分类'),
      ...axisBase.value,
      axisTick: { show: false },
    },
    series: [
      {
        name: '资产数量',
        type: 'bar',
        data: list.map((c) => c.value ?? 0),
        itemStyle: { color: colors.value.primary, borderRadius: [0, 4, 4, 0] },
        barMaxWidth: 14,
      },
      {
        name: '原值(万)',
        type: 'bar',
        xAxisIndex: 1,
        data: list.map((c) => toWan(c.amount ?? 0)),
        itemStyle: { color: '#67C23A', borderRadius: [0, 4, 4, 0] },
        barMaxWidth: 14,
      },
    ],
  }
})

// 近 12 个月趋势
const trendOption = computed(() => {
  const list = data.value.monthlyTrend ?? []
  return {
    tooltip: { trigger: 'axis' },
    legend: {
      data: ['新增数量', '新增金额(万)'],
      bottom: 0,
      textStyle: { color: colors.value.textRegular },
    },
    grid: { left: '3%', right: '4%', bottom: '13%', top: '8%', containLabel: true },
    xAxis: {
      type: 'category',
      data: list.map((t) => t.date ?? ''),
      ...axisBase.value,
    },
    yAxis: [
      {
        type: 'value',
        name: '数量',
        ...axisBase.value,
        splitLine: { lineStyle: { color: colors.value.border } },
      },
      {
        type: 'value',
        name: '金额(万)',
        ...axisBase.value,
        splitLine: { show: false },
      },
    ],
    series: [
      {
        name: '新增数量',
        type: 'bar',
        data: list.map((t) => t.count ?? 0),
        itemStyle: { color: colors.value.primary, borderRadius: [3, 3, 0, 0] },
        barMaxWidth: 22,
      },
      {
        name: '新增金额(万)',
        type: 'line',
        yAxisIndex: 1,
        smooth: true,
        data: list.map((t) => toWan(t.amount ?? 0)),
        itemStyle: { color: '#E6A23C' },
        areaStyle: { opacity: 0.12 },
      },
    ],
  }
})

// 部门分布
const deptOption = computed(() => {
  const list = [...data.value.deptStats].slice(0, 10).reverse()
  return {
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    grid: { left: '3%', right: '10%', bottom: '4%', top: '4%', containLabel: true },
    xAxis: { type: 'value', ...axisBase.value, splitLine: { lineStyle: { color: colors.value.border } } },
    yAxis: {
      type: 'category',
      data: list.map((d) => d.name || d.code || '未分配部门'),
      ...axisBase.value,
      axisTick: { show: false },
    },
    series: [
      {
        type: 'bar',
        data: list.map((d) => d.value ?? 0),
        itemStyle: { color: colors.value.primary, borderRadius: [0, 4, 4, 0] },
        barMaxWidth: 18,
        label: { show: true, position: 'right', fontSize: 11, color: colors.value.textRegular },
      },
    ],
  }
})

// 折旧方式分布
const depreciationOption = computed(() => ({
  tooltip: { trigger: 'item', formatter: '{b}: {c} 条 ({d}%)' },
  legend: { bottom: 0, textStyle: { color: colors.value.textRegular }, type: 'scroll' },
  color: PALETTE,
  series: [
    {
      type: 'pie',
      radius: '58%',
      center: ['50%', '43%'],
      roseType: 'radius',
      itemStyle: { borderRadius: 5, borderColor: colors.value.cardBg, borderWidth: 2 },
      label: { color: colors.value.textRegular, fontSize: 11 },
      data: data.value.depreciationMethodStats.map((m) => ({
        name: DEPRECIATION_METHOD_MAP[m.code || ''] || m.name || '未知',
        value: m.value ?? 0,
      })),
    },
  ],
}))

// 流转单据统计
const flowOption = computed(() => {
  const list = data.value.flowTypeStats ?? []
  return {
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    grid: { left: '3%', right: '8%', bottom: '10%', top: '8%', containLabel: true },
    xAxis: {
      type: 'category',
      data: list.map((f) => FLOW_TYPE_MAP[f.code || ''] || f.name || '未知'),
      ...axisBase.value,
    },
    yAxis: {
      type: 'value',
      ...axisBase.value,
      splitLine: { lineStyle: { color: colors.value.border } },
    },
    series: [
      {
        type: 'bar',
        data: list.map((f) => f.value ?? 0),
        itemStyle: { color: colors.value.primary, borderRadius: [4, 4, 0, 0] },
        barMaxWidth: 32,
        label: { show: true, position: 'top', fontSize: 11, color: colors.value.textRegular },
      },
    ],
  }
})
</script>

<style scoped>
.analytics {
  padding: 0;
}
.kpi-card {
  margin-bottom: 14px;
}
.kpi-label {
  font-size: 12px;
  color: var(--ams-text-regular, #909399);
}
.kpi-value {
  font-size: 20px;
  font-weight: 700;
  line-height: 1.3;
  margin: 6px 0 4px;
  word-break: break-all;
}
.kpi-sub {
  font-size: 11px;
  color: var(--ams-text-regular, #a8abb2);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.chart-card {
  margin-bottom: 14px;
}
.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}
.chart-foot {
  text-align: center;
  font-size: 11px;
  color: var(--ams-text-regular, #909399);
  padding-top: 6px;
}
.health-list {
  padding: 4px 2px 0;
}
.health-item {
  margin-bottom: 16px;
}
.health-head {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
  margin-bottom: 8px;
  color: var(--ams-text-main, #303133);
}
.health-tip {
  margin-top: 6px;
  font-size: 11px;
  color: var(--ams-text-regular, #a8abb2);
  line-height: 1.5;
}
.health-foot {
  display: flex;
  justify-content: space-between;
  border-top: 1px solid var(--ams-border, #ebeef5);
  padding-top: 10px;
}
</style>
