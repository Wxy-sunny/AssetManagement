<template>
  <div class="inventory-page">
    <el-card shadow="never" class="search-card">
      <el-form :model="query" inline @submit.prevent>
        <el-form-item label="关键字">
          <el-input
            v-model="query.keyword"
            placeholder="盘点单号 / 名称"
            clearable
            style="width: 200px"
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="query.inventoryStatus" placeholder="全部状态" clearable style="width: 140px">
            <el-option v-for="(label, code) in INVENTORY_STATUS_MAP" :key="code" :label="label" :value="code" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
          <el-button :icon="Refresh" @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card shadow="never">
      <div class="toolbar">
        <div class="toolbar-left">
          <el-button v-if="canManage" type="primary" :icon="Plus" @click="openCreateDialog">
            新建盘点单
          </el-button>
          <el-button :icon="Document" @click="goReport">盘点差异报表</el-button>
        </div>
        <el-button :icon="RefreshRight" @click="loadData">刷新</el-button>
      </div>

      <el-table v-loading="loading" :data="tableData" border stripe>
        <el-table-column prop="inventoryNo" label="盘点单号" width="165" show-overflow-tooltip />
        <el-table-column prop="inventoryName" label="盘点名称" min-width="160" show-overflow-tooltip />
        <el-table-column prop="deptName" label="盘点范围" width="130" show-overflow-tooltip>
          <template #default="{ row }">{{ row.deptName || '全部部门' }}</template>
        </el-table-column>
        <el-table-column label="状态" width="95" align="center">
          <template #default="{ row }">
            <el-tag :type="INVENTORY_STATUS_TAG[row.inventoryStatus] || 'info'" size="small">
              {{ INVENTORY_STATUS_MAP[row.inventoryStatus] || '未知' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="盘点进度" width="150" align="center">
          <template #default="{ row }">
            <span>{{ row.checkedCount ?? 0 }} / {{ row.totalCount ?? 0 }}</span>
            <el-progress
              :percentage="calcProgress(row)"
              :stroke-width="5"
              :show-text="false"
              class="row-progress"
            />
          </template>
        </el-table-column>
        <el-table-column label="差异" width="85" align="center">
          <template #default="{ row }">
            <el-tag v-if="(row.diffCount ?? 0) > 0" type="danger" size="small" effect="plain">
              {{ row.diffCount }} 条
            </el-tag>
            <span v-else class="muted">无</span>
          </template>
        </el-table-column>
        <el-table-column label="涉及原值" width="125" align="right">
          <template #default="{ row }">{{ formatMoney(row.totalAmount) }}</template>
        </el-table-column>
        <el-table-column prop="ownerName" label="负责人" width="110" show-overflow-tooltip />
        <el-table-column label="创建时间" width="155">
          <template #default="{ row }">{{ formatTime(row.createTime) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="210" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openDrawer(row)">
              {{ row.inventoryStatus === 'draft' ? '录入实盘' : '查看结果' }}
            </el-button>
            <el-button
              v-if="canManage && row.inventoryStatus === 'draft'"
              link
              type="success"
              size="small"
              @click="handleComplete(row)"
            >
              完成盘点
            </el-button>
            <el-button
              v-if="canManage && row.inventoryStatus === 'draft'"
              link
              type="danger"
              size="small"
              @click="handleDelete(row)"
            >
              删除
            </el-button>
          </template>
        </el-table-column>
        <template #empty><el-empty description="暂无盘点单" /></template>
      </el-table>

      <div class="pagination">
        <el-pagination
          v-model:current-page="query.pageIndex"
          v-model:page-size="query.pageSize"
          :page-sizes="[10, 20, 50]"
          :total="total"
          layout="total, sizes, prev, pager, next, jumper"
          background
          @size-change="loadData"
          @current-change="loadData"
        />
      </div>
    </el-card>

    <!-- 新建盘点单 -->
    <el-dialog v-model="createVisible" title="新建盘点单" width="520px" destroy-on-close>
      <el-alert type="info" :closable="false" class="create-alert">
        创建时会按盘点范围对在册资产生成「账面快照明细」，后续逐条录入实盘结果，系统自动判定账实差异。
      </el-alert>
      <el-form :model="createForm" label-width="105px">
        <el-form-item label="盘点名称" required>
          <el-input v-model="createForm.inventoryName" placeholder="如 2026 年度固定资产盘点" />
        </el-form-item>
        <el-form-item label="盘点范围">
          <el-select v-model="createForm.deptId" clearable placeholder="全部部门" style="width: 100%">
            <el-option v-for="d in depts" :key="d.id" :label="d.departmentName" :value="d.id as string" />
          </el-select>
        </el-form-item>
        <el-form-item label="计划日期">
          <el-date-picker
            v-model="createForm.planDate"
            type="date"
            value-format="YYYY-MM-DD"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="createForm.remark" type="textarea" :rows="2" placeholder="选填" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="createVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitCreate">创建并生成明细</el-button>
      </template>
    </el-dialog>

    <!-- 盘点明细 / 录入 -->
    <el-drawer v-model="drawerVisible" :title="`盘点明细 · ${current?.inventoryName || ''}`" size="76%">
      <template v-if="current">
        <el-row :gutter="12" class="summary-row">
          <el-col v-for="s in summaryCards" :key="s.label" :xs="12" :sm="8" :md="4">
            <el-card shadow="never" class="summary-card">
              <div class="summary-label">{{ s.label }}</div>
              <div class="summary-value" :style="{ color: s.color }">{{ s.value }}</div>
            </el-card>
          </el-col>
        </el-row>

        <div class="detail-toolbar">
          <el-radio-group v-model="onlyDiff" @change="loadItems">
            <el-radio-button :value="false">全部明细</el-radio-button>
            <el-radio-button :value="true">仅看差异</el-radio-button>
          </el-radio-group>
          <div class="detail-actions">
            <el-button
              v-if="canManage && current.inventoryStatus === 'draft'"
              :icon="CircleCheck"
              @click="markAllNormal"
            >
              未盘标记正常
            </el-button>
            <el-button
              v-if="canManage && current.inventoryStatus === 'draft'"
              type="primary"
              :icon="Select"
              :loading="submitting"
              @click="saveChecks"
            >
              保存录入
            </el-button>
          </div>
        </div>

        <el-table v-loading="detailLoading" :data="items" border stripe size="small" max-height="520">
          <el-table-column prop="assetCode" label="资产编码" width="125" show-overflow-tooltip />
          <el-table-column prop="assetName" label="资产名称" min-width="150" show-overflow-tooltip />
          <el-table-column label="账面状态" width="90" align="center">
            <template #default="{ row }">{{ row.bookStatusName }}</template>
          </el-table-column>
          <el-table-column label="账面位置" width="130" show-overflow-tooltip>
            <template #default="{ row }">{{ row.bookLocation || '—' }}</template>
          </el-table-column>
          <el-table-column label="实盘状态" width="170">
            <template #default="{ row }">
              <el-select
                v-model="row.actualStatus"
                :disabled="!canManage || current.inventoryStatus !== 'draft'"
                placeholder="未盘"
                clearable
                size="small"
                style="width: 100%"
              >
                <el-option v-for="o in ACTUAL_STATUS_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
              </el-select>
            </template>
          </el-table-column>
          <el-table-column label="实盘位置" width="160">
            <template #default="{ row }">
              <el-input
                v-model="row.actualLocation"
                :disabled="!canManage || current.inventoryStatus !== 'draft'"
                size="small"
                placeholder="与账面一致可留空"
              />
            </template>
          </el-table-column>
          <el-table-column label="实盘使用人" width="140">
            <template #default="{ row }">
              <el-input
                v-model="row.actualUserName"
                :disabled="!canManage || current.inventoryStatus !== 'draft'"
                size="small"
                placeholder="与账面一致可留空"
              />
            </template>
          </el-table-column>
          <el-table-column label="差异" width="80" align="center">
            <template #default="{ row }">
              <el-tag v-if="row.isDiff" type="danger" size="small" effect="plain">差异</el-tag>
              <el-tag v-else-if="row.checkedTime" type="success" size="small" effect="plain">一致</el-tag>
              <span v-else class="muted">未盘</span>
            </template>
          </el-table-column>
          <el-table-column label="差异说明" min-width="200" show-overflow-tooltip>
            <template #default="{ row }">
              <span :title="row.diffDesc || ''">{{ row.diffDesc || '—' }}</span>
            </template>
          </el-table-column>
          <el-table-column label="差异处理" width="170" fixed="right">
            <template #default="{ row }">
              <el-button
                v-if="row.handled"
                link
                type="success"
                size="small"
                disabled
              >
                已生成单据
              </el-button>
              <el-dropdown
                v-else-if="row.isDiff && canManage"
                @command="(cmd: string) => handleDiff(row, cmd)"
              >
                <el-button link type="primary" size="small">
                  生成处理单<el-icon class="el-icon--right"><ArrowDown /></el-icon>
                </el-button>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item command="scrap">生成报废单</el-dropdown-item>
                    <el-dropdown-item command="repair">生成报修单</el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
              <span v-else class="muted">—</span>
            </template>
          </el-table-column>
        </el-table>

        <div class="detail-pagination">
          <el-pagination
            v-model:current-page="itemPageIndex"
            v-model:page-size="itemPageSize"
            :page-sizes="[20, 50, 100, 200]"
            :total="itemTotal"
            layout="total, sizes, prev, pager, next"
            background
            @size-change="loadItems"
            @current-change="loadItems"
          />
        </div>
      </template>
    </el-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  Search,
  Refresh,
  Plus,
  RefreshRight,
  Document,
  CircleCheck,
  Select,
  ArrowDown,
} from '@element-plus/icons-vue'
import { useUserStore } from '@/stores/user'
import {
  getInventoryPage,
  getInventoryById,
  getInventoryItems,
  createInventory,
  checkInventoryItems,
  completeInventory,
  deleteInventory,
  handleInventoryDiff,
} from '@/api/inventory'
import { getDeptList } from '@/api/department'
import {
  INVENTORY_STATUS_MAP,
  INVENTORY_STATUS_TAG,
  ACTUAL_STATUS_OPTIONS,
  PERMISSIONS,
  type InventoryItem,
  type InventoryDetailItem,
  type InventorySummary,
  type InventoryQuery,
  type InventoryCheck,
  type DepartmentItem,
} from '@/types'

const router = useRouter()
const userStore = useUserStore()
const canManage = computed(() => userStore.hasPermission(PERMISSIONS.InventoryManage))
const goReport = () => router.push({ path: '/reports', query: { type: 'inventory_diff' } })

const loading = ref(false)
const submitting = ref(false)
const tableData = ref<InventoryItem[]>([])
const total = ref(0)
const depts = ref<DepartmentItem[]>([])

const query = reactive<InventoryQuery>({
  keyword: '',
  inventoryStatus: undefined,
  pageIndex: 1,
  pageSize: 10,
})

const formatMoney = (val?: number | null) =>
  '¥' + Number(val ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const formatTime = (val?: string | null) => (val ? val.replace('T', ' ').slice(0, 16) : '—')
const calcProgress = (row: InventoryItem) => {
  const totalCount = Number(row.totalCount ?? 0)
  if (totalCount <= 0) return 0
  return Math.min(100, Math.round((Number(row.checkedCount ?? 0) * 100) / totalCount))
}

const loadData = async () => {
  loading.value = true
  try {
    const res = await getInventoryPage({
      ...query,
      keyword: query.keyword || undefined,
      inventoryStatus: query.inventoryStatus || undefined,
    })
    tableData.value = res?.list ?? []
    total.value = res?.total ?? 0
  } catch {
    tableData.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

const loadOptions = async () => {
  try {
    depts.value = await getDeptList()
  } catch {
    depts.value = []
  }
}

const handleSearch = () => {
  query.pageIndex = 1
  loadData()
}

const handleReset = () => {
  query.keyword = ''
  query.inventoryStatus = undefined
  query.pageIndex = 1
  loadData()
}

// ---------- 创建盘点单 ----------
const createVisible = ref(false)
const createForm = reactive({
  inventoryName: '',
  deptId: '' as string | null,
  planDate: null as string | null,
  remark: '',
})

const openCreateDialog = () => {
  Object.assign(createForm, { inventoryName: '', deptId: '', planDate: null, remark: '' })
  createVisible.value = true
  loadOptions()
}

const submitCreate = async () => {
  if (!createForm.inventoryName.trim()) {
    ElMessage.warning('请填写盘点名称')
    return
  }
  const dept = depts.value.find((d) => d.id === createForm.deptId)
  submitting.value = true
  try {
    const res = await createInventory({
      inventoryName: createForm.inventoryName.trim(),
      deptId: createForm.deptId || undefined,
      deptName: dept?.departmentName,
      planDate: createForm.planDate,
      remark: createForm.remark,
    })
    ElMessage.success(`盘点单已创建，生成 ${res?.totalCount ?? 0} 条明细`)
    createVisible.value = false
    loadData()
  } catch {
    // 拦截器已提示错误
  } finally {
    submitting.value = false
  }
}

// ---------- 明细抽屉 ----------
const drawerVisible = ref(false)
const detailLoading = ref(false)
const current = ref<InventoryItem | null>(null)
const summary = ref<InventorySummary | null>(null)
const items = ref<InventoryDetailItem[]>([])
const itemTotal = ref(0)
const itemPageIndex = ref(1)
const itemPageSize = ref(20)
const onlyDiff = ref(false)

const summaryCards = computed(() => [
  { label: '盘点总数', value: String(summary.value?.totalCount ?? 0), color: 'var(--ams-primary, #409eff)' },
  { label: '已盘数量', value: String(summary.value?.checkedCount ?? 0), color: 'var(--ams-text-main, #303133)' },
  { label: '差异条数', value: String(summary.value?.diffCount ?? 0), color: '#f56c6c' },
  { label: '盘亏条数', value: String(summary.value?.missingCount ?? 0), color: '#e6a23c' },
  { label: '准确率', value: `${summary.value?.accuracyRate ?? 0}%`, color: '#67c23a' },
  { label: '差异资产原值', value: formatMoney(summary.value?.diffAmount), color: '#f56c6c' },
])

const loadDetail = async () => {
  if (!current.value) return
  try {
    const res = await getInventoryById(current.value.id)
    current.value = res?.inventory ?? current.value
    summary.value = res?.summary ?? null
  } catch {
    summary.value = null
  }
}

const loadItems = async () => {
  if (!current.value) return
  detailLoading.value = true
  try {
    const res = await getInventoryItems(current.value.id, {
      onlyDiff: onlyDiff.value,
      pageIndex: itemPageIndex.value,
      pageSize: itemPageSize.value,
    })
    items.value = res?.list ?? []
    itemTotal.value = res?.total ?? 0
  } catch {
    items.value = []
    itemTotal.value = 0
  } finally {
    detailLoading.value = false
  }
}

const openDrawer = async (row: InventoryItem) => {
  current.value = { ...row }
  summary.value = null
  onlyDiff.value = false
  itemPageIndex.value = 1
  drawerVisible.value = true
  await Promise.all([loadDetail(), loadItems()])
}

const markAllNormal = () => {
  let count = 0
  items.value.forEach((row) => {
    if (!row.checkedTime) {
      row.actualStatus = 'normal'
      count++
    }
  })
  ElMessage.info(`已将本页 ${count} 条标记为正常，请点击「保存录入」提交`)
}

const saveChecks = async () => {
  if (!current.value) return
  const checks: InventoryCheck[] = items.value
    .filter((row) => row.actualStatus)
    .map((row) => ({
      id: row.id,
      actualStatus: row.actualStatus,
      actualLocation: row.actualLocation || undefined,
      actualUserName: row.actualUserName || undefined,
    }))
  if (checks.length === 0) {
    ElMessage.warning('本页没有可提交的实盘结果')
    return
  }
  submitting.value = true
  try {
    const res = await checkInventoryItems(current.value.id, checks)
    ElMessage.success(`已保存 ${checks.length} 条，当前差异 ${res?.diffCount ?? 0} 条`)
    await Promise.all([loadDetail(), loadItems(), loadData()])
  } catch {
    // 拦截器已提示错误
  } finally {
    submitting.value = false
  }
}

// ---------- 完成 / 删除 ----------
const handleComplete = async (row: InventoryItem) => {
  try {
    await ElMessageBox.confirm(
      '完成盘点后会汇总账实差异并锁定录入，未盘点的资产将无法完成。是否继续？',
      '完成盘点',
      { type: 'warning', confirmButtonText: '确定', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    const res = await completeInventory(row.id)
    ElMessage.success(`盘点完成，共 ${res?.diffCount ?? 0} 条差异，准确率 ${res?.accuracyRate ?? 0}%`)
    loadData()
  } catch {
    // 拦截器已提示错误
  }
}

const handleDelete = async (row: InventoryItem) => {
  try {
    await ElMessageBox.confirm(`确定删除盘点单「${row.inventoryName}」及其全部明细吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await deleteInventory(row.id)
    ElMessage.success('删除成功')
    loadData()
  } catch {
    // 拦截器已提示错误
  }
}

// ---------- 差异转处理单据 ----------
const handleDiff = async (row: InventoryDetailItem, command: string) => {
  const label = command === 'scrap' ? '报废' : '报修'
  try {
    await ElMessageBox.confirm(
      `为资产「${row.assetName}」生成${label}单据？资产状态将按流转规则同步变更。`,
      `生成${label}单`,
      { type: 'warning', confirmButtonText: '确定', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    await handleInventoryDiff(row.id, command)
    ElMessage.success(`${label}单据已生成，可在「业务流程 / 流转单据」中查看`)
    await Promise.all([loadItems(), loadDetail()])
  } catch {
    // 拦截器已提示错误
  }
}

onMounted(() => {
  loadData()
  loadOptions()
})
</script>

<style scoped>
.inventory-page {
  padding: 0;
}
.search-card {
  margin-bottom: 16px;
}
.toolbar,
.detail-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  gap: 12px;
  flex-wrap: wrap;
}
.toolbar-left,
.detail-actions {
  display: flex;
  gap: 10px;
}
.pagination,
.detail-pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.row-progress {
  margin-top: 4px;
}
.muted {
  color: var(--ams-text-regular, #a8abb2);
}
.create-alert {
  margin-bottom: 18px;
  line-height: 1.6;
}
.summary-row {
  margin-bottom: 16px;
}
.summary-card {
  margin-bottom: 12px;
}
.summary-label {
  font-size: 12px;
  color: var(--ams-text-regular, #909399);
}
.summary-value {
  font-size: 19px;
  font-weight: 700;
  margin-top: 6px;
  line-height: 1.3;
  word-break: break-all;
}
</style>
