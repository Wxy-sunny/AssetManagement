<template>
  <div ref="chartRef" class="echart" :style="{ height: height }"></div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue'
import * as echarts from 'echarts'

const props = withDefaults(
  defineProps<{
    // ECharts 配置项
    option: Record<string, any>
    height?: string
  }>(),
  {
    height: '300px',
  },
)

const chartRef = ref<HTMLDivElement | null>(null)
const chart = shallowRef<echarts.ECharts | null>(null)

const render = () => {
  if (!chartRef.value) return
  if (!chart.value) {
    chart.value = echarts.init(chartRef.value)
  }
  // notMerge=true 保证数据切换时不会残留旧系列
  chart.value.setOption(props.option, true)
}

const handleResize = () => chart.value?.resize()

onMounted(() => {
  render()
  window.addEventListener('resize', handleResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
  chart.value?.dispose()
  chart.value = null
})

watch(
  () => props.option,
  () => render(),
  { deep: true },
)
</script>

<style scoped>
.echart {
  width: 100%;
}
</style>
