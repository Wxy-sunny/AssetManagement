<template>
  <div class="report-page">
    <el-row :gutter="16">
      <!-- 左侧报表清单 -->
      <el-col :xs="24" :sm="24" :md="6">
        <el-card shadow="never" class="type-card">
          <template #header><span>报表目录</span></template>
          <div
            v-for="item in reportTypes"
            :key="item.code"
            class="type-item"
            :class="{ active: currentType === item.code }"
            @click="selectType(item.code)"
          >
            <div class="type-name">
              <el-icon><Document /></el-icon>
              <span>{{ item.name }}</span>
            </div>
            <div class="type-desc">{{ item.description }}</div>
          </div>
        </el-card>
      </el-col>

      <!-- 右侧预览与导出 -->
      <el-col :xs="24" :sm="24" :md="18">
        <el-card shadow="never" class="search-card">
          <el-form :model="query" inline @submit.prevent>
            <el-form-item label="关键字">
              <el-input
                v-model="query.keyword"
                placeholder="编码 / 名称"
                clearable
                style="width: 150px"
                @keyup.enter="handleSearch"
              />
            </el-form-item>
            <el-form-item label="分类">
              <el-select v-model="query.categoryId" clearable placeholder="全部分类" style="width: 140px">
                <el-option v-for="c in categories" :key="c.id" :label="c.categoryName" :value="c.id as string" />
              </el-select>
            </el-form-item>
            <el-form-item label="部门">
              <el-select v-model="query.deptId" clearable placeholder="全部部门" style="width: 140px">
                <el-option v-for="d in depts" :key="d.id" :label="d.departmentName" :value="d.id as string" />
              </el-select>
            </el-form-item>
            <el-form-item label="状态">
              <el-select v-model="query.assetStatus" clearable placeholder="全部状态" style="width: 120px">
                <el-option
                  v-for="(label, code) in ASSET_STATUS_MAP"
                  :key="code"
                  :label="label"
                  :value="code"
                />
              </el-select>
            </el-form-item>
            <el-form-item label="采购日期">
              <el-date-picker
                v-model="dateRange"
                type="daterange"
                value-format="YYYY-MM-DD"
                start-placeholder="开始"
                end-placeholder="结束"
                style="width: 230px"
              />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
              <el-button :icon="Refresh" @click="handleReset">重置</el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card shadow="never">
          <div class="toolbar">
            <div class="toolbar-title">
              <span class="title">{{ currentTypeName }} · 数据预览</span>
              <el-tag type="info" size="small">共 {{ preview.total }} 条</el-tag>
            </div>
            <div class="toolbar-actions">
              <el-button
                v-if="canExport"
                type="primary"
                :icon="Download"
                :loading="exporting"
                @click="handleExport('xlsx')"
              >
                导出 Excel
              </el-button>
              <el-button
                v-if="canExport"
                :icon="Download"
                :loading="exporting"
                @click="handleExport('csv')"
              >
                导出 CSV
              </el-button>
            </div>
          </div>

          <el-alert type="info" :closable="false" class="export-tip">
            导出在服务端内存中生成（XLSX / 带 BOM 的 UTF-8 CSV），直接流式下载，不占用服务器磁盘；
            导出不受预览分页限制，始终包含筛选后的全部数据。
          </el-alert>

          <el-table v-loading="loading" :data="preview.rows" border stripe size="small" max-height="520">
            <el-table-column
              v-for="(col, i) in preview.columns"
              :key="col + i"
              :label="col"
              min-width="135"
              show-overflow-tooltip
            >
              <template #default="{ row }">{{ formatCell(row[i]) }}</template>
            </el-table-column>
            <template #empty><el-empty description="暂无数据" /></template>
          </el-table>

          <div class="pagination">
            <el-pagination
              v-model:current-page="query.pageIndex"
              v-model:page-size="query.pageSize"
              :page-sizes="[20, 50, 100, 200]"
              :total="preview.total"
              layout="total, sizes, prev, pager, next, jumper"
              background
              @size-change="loadData"
              @current-change="loadData"
            />
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Search, Refresh, Download, Document } from '@element-plus/icons-vue'
import { useUserStore } from '@/stores/user'
import { getReportTypes, getReportData, exportReport } from '@/api/report'
import { getCategoryList } from '@/api/category'
import { getDeptList } from '@/api/department'
import {
  ASSET_STATUS_MAP,
  PERMISSIONS,
  type ReportTypeItem,
  type ReportQuery,
  type ReportPreview,
  type CategoryItem,
  type DepartmentItem,
} from '@/types'

const route = useRoute()
const userStore = useUserStore()
const canExport = computed(() => userStore.hasPermission(PERMISSIONS.ReportExport))

const reportTypes = ref<ReportTypeItem[]>([])
const currentType = ref('asset_ledger')
const currentTypeName = computed(
  () => reportTypes.value.find((t) => t.code === currentType.value)?.name ?? '报表',
)
const loading = ref(false)
const exporting = ref(false)
const dateRange = ref<[string, string] | null>(null)
const categories = ref<CategoryItem[]>([])
const depts = ref<DepartmentItem[]>([])

const emptyPreview = (): ReportPreview => ({
  reportType: '',
  title: '',
  columns: [],
  rows: [],
  total: 0,
})
const preview = ref<ReportPreview>(emptyPreview())

const query = reactive<ReportQuery>({
  reportType: 'asset_ledger',
  keyword: '',
  categoryId: undefined,
  deptId: undefined,
  assetStatus: undefined,
  pageIndex: 1,
  pageSize: 20,
})

const formatCell = (val: string | number | boolean | null | undefined) => {
  if (val === null || val === undefined || val === '') return '—'
  if (typeof val === 'number') return val.toLocaleString('zh-CN')
  return String(val)
}

const buildParams = (): ReportQuery => ({
  reportType: currentType.value,
  keyword: query.keyword || undefined,
  categoryId: query.categoryId || undefined,
  deptId: query.deptId || undefined,
  assetStatus: query.assetStatus || undefined,
  startDate: dateRange.value?.[0],
  endDate: dateRange.value?.[1],
  pageIndex: query.pageIndex,
  pageSize: query.pageSize,
})

const loadData = async () => {
  loading.value = true
  try {
    preview.value = (await getReportData(buildParams())) ?? emptyPreview()
  } catch {
    preview.value = emptyPreview()
  } finally {
    loading.value = false
  }
}

const loadOptions = async () => {
  try {
    categories.value = await getCategoryList()
  } catch {
    categories.value = []
  }
  try {
    depts.value = await getDeptList()
  } catch {
    depts.value = []
  }
}

const loadTypes = async () => {
  try {
    reportTypes.value = await getReportTypes()
    const fromRoute = route.query.type as string | undefined
    if (fromRoute && reportTypes.value.some((t) => t.code === fromRoute)) {
      currentType.value = fromRoute
    }
  } catch {
    reportTypes.value = []
  }
}

const selectType = (code: string) => {
  currentType.value = code
  query.pageIndex = 1
  loadData()
}

const handleSearch = () => {
  query.pageIndex = 1
  loadData()
}

const handleReset = () => {
  query.keyword = ''
  query.categoryId = undefined
  query.deptId = undefined
  query.assetStatus = undefined
  dateRange.value = null
  query.pageIndex = 1
  loadData()
}

const handleExport = async (format: 'xlsx' | 'csv') => {
  exporting.value = true
  try {
    await exportReport({ ...buildParams(), format })
    ElMessage.success(`已导出 ${currentTypeName.value}（${format.toUpperCase()}）`)
  } catch {
    // 拦截器已提示错误
  } finally {
    exporting.value = false
  }
}

watch(
  () => route.query.type,
  (val) => {
    if (typeof val === 'string' && reportTypes.value.some((t) => t.code === val)) {
      currentType.value = val
      loadData()
    }
  },
)

onMounted(async () => {
  await Promise.all([loadTypes(), loadOptions()])
  loadData()
})
</script>

<style scoped>
.report-page {
  padding: 0;
}
.type-card {
  margin-bottom: 16px;
}
.type-item {
  padding: 12px 10px;
  border-radius: 6px;
  cursor: pointer;
  margin-bottom: 6px;
  transition: background-color 0.2s ease;
}
.type-item:hover {
  background: var(--ams-hover-bg, #f5f7fa);
}
.type-item.active {
  background: var(--el-color-primary-light-9, #ecf5ff);
  border-left: 3px solid var(--ams-primary, #409eff);
}
.type-name {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 600;
  color: var(--ams-text-main, #303133);
}
.type-desc {
  margin-top: 5px;
  font-size: 12px;
  line-height: 1.5;
  color: var(--ams-text-regular, #909399);
}
.search-card {
  margin-bottom: 16px;
}
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
  margin-bottom: 14px;
}
.toolbar-title {
  display: flex;
  align-items: center;
  gap: 10px;
}
.toolbar-title .title {
  font-size: 15px;
  font-weight: 600;
  color: var(--ams-text-main, #303133);
}
.toolbar-actions {
  display: flex;
  gap: 10px;
}
.export-tip {
  margin-bottom: 14px;
  font-size: 12px;
  line-height: 1.6;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
