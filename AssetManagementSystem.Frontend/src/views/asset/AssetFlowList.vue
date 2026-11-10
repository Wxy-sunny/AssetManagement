<template>
  <div class="flow-page">
    <!-- 生命周期闭环示意 -->
    <el-card shadow="never" class="guide-card">
      <div class="guide-title">
        <span>资产全生命周期闭环</span>
        <el-tag v-if="pendingTotal > 0" type="warning" size="small" effect="dark">
          {{ pendingTotal }} 张单据待处理
        </el-tag>
      </div>
      <div class="guide-steps">
        <div v-for="(s, i) in guideSteps" :key="s.code" class="guide-step">
          <div class="step-dot" :style="{ backgroundColor: s.color }">
            <el-icon><component :is="s.icon" /></el-icon>
          </div>
          <div class="step-text">
            <b>{{ s.name }}</b>
            <span>{{ s.desc }}</span>
          </div>
          <el-icon v-if="i < guideSteps.length - 1" class="step-arrow"><Right /></el-icon>
        </div>
      </div>
      <div class="guide-tip">
        领用 / 归还 / 报修 / 维修完成 / 报废均生成流转单据；已报废为终态，不再允许流转。
        每次状态变更都留有审计流水，账实始终可追溯。
      </div>
    </el-card>

    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="query" inline @submit.prevent>
        <el-form-item label="关键字">
          <el-input
            v-model="query.keyword"
            placeholder="单号 / 编码 / 名称 / 使用人"
            clearable
            style="width: 210px"
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item label="单据类型">
          <el-select v-model="query.flowType" placeholder="全部类型" clearable style="width: 140px">
            <el-option v-for="(label, code) in FLOW_TYPE_MAP" :key="code" :label="label" :value="code" />
          </el-select>
        </el-form-item>
        <el-form-item label="处理状态">
          <el-select v-model="query.flowStatus" placeholder="全部状态" clearable style="width: 140px">
            <el-option v-for="(label, code) in FLOW_STATUS_MAP" :key="code" :label="label" :value="code" />
          </el-select>
        </el-form-item>
        <el-form-item label="办理日期">
          <el-date-picker
            v-model="dateRange"
            type="daterange"
            value-format="YYYY-MM-DD"
            start-placeholder="开始"
            end-placeholder="结束"
            style="width: 240px"
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
          <el-button :icon="Refresh" @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- 列表 -->
    <el-card shadow="never">
      <div class="toolbar">
        <div class="toolbar-left">
          <el-button v-if="canManage" type="primary" :icon="Plus" @click="openCreateDialog">
            新建流转单据
          </el-button>
        </div>
        <el-button :icon="RefreshRight" @click="loadData">刷新</el-button>
      </div>

      <el-table v-loading="loading" :data="tableData" border stripe>
        <el-table-column prop="flowNo" label="单据编号" width="175" show-overflow-tooltip />
        <el-table-column label="类型" width="100" align="center">
          <template #default="{ row }">
            <el-tag :type="FLOW_TYPE_TAG[row.flowType] || 'info'" size="small">
              {{ FLOW_TYPE_MAP[row.flowType] || '未知' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="资产" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="asset-code">{{ row.assetCode }}</span>
            <span class="asset-name">{{ row.assetName }}</span>
          </template>
        </el-table-column>
        <el-table-column label="状态流转" width="165" align="center">
          <template #default="{ row }">
            <span class="status-text">{{ row.fromStatusName || '—' }}</span>
            <el-icon class="status-arrow"><Right /></el-icon>
            <span class="status-text strong">{{ row.toStatusName || '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="useUserName" label="使用人" width="100" show-overflow-tooltip />
        <el-table-column prop="deptName" label="使用部门" width="120" show-overflow-tooltip />
        <el-table-column label="处理状态" width="95" align="center">
          <template #default="{ row }">
            <el-tag :type="FLOW_STATUS_TAG[row.flowStatus] || 'info'" size="small">
              {{ FLOW_STATUS_MAP[row.flowStatus] || '未知' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="办理时间" width="155">
          <template #default="{ row }">{{ formatTime(row.handleTime || row.createTime) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="185" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openDetail(row)">详情</el-button>
            <template v-if="canManage && row.flowStatus === 'pending'">
              <el-button link type="success" size="small" @click="openCompleteDialog(row)">
                完成维修
              </el-button>
              <el-dropdown @command="(cmd: string) => handleClose(row, cmd)">
                <el-button link type="info" size="small">
                  更多<el-icon class="el-icon--right"><ArrowDown /></el-icon>
                </el-button>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item command="cancel">取消单据</el-dropdown-item>
                    <el-dropdown-item command="reject">驳回单据</el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </template>
          </template>
        </el-table-column>
        <template #empty><el-empty description="暂无流转单据" /></template>
      </el-table>

      <div class="pagination">
        <el-pagination
          v-model:current-page="query.pageIndex"
          v-model:page-size="query.pageSize"
          :page-sizes="[10, 20, 50, 100]"
          :total="total"
          layout="total, sizes, prev, pager, next, jumper"
          background
          @size-change="loadData"
          @current-change="loadData"
        />
      </div>
    </el-card>

    <!-- 新建单据 -->
    <el-dialog v-model="createVisible" title="新建流转单据" width="620px" destroy-on-close>
      <el-form ref="createFormRef" :model="createForm" :rules="createRules" label-width="105px">
        <el-form-item label="选择资产" prop="assetId">
          <el-select
            v-model="createForm.assetId"
            filterable
            clearable
            placeholder="输入编码或名称搜索资产"
            style="width: 100%"
            @change="onAssetChange"
          >
            <el-option
              v-for="a in assetOptions"
              :key="a.id"
              :label="`${a.assetCode} · ${a.assetName}`"
              :value="a.id as string"
            >
              <span>{{ a.assetCode }} · {{ a.assetName }}</span>
              <el-tag size="small" class="option-tag">
                {{ ASSET_STATUS_MAP[a.assetStatus || ''] || '未知' }}
              </el-tag>
            </el-option>
          </el-select>
        </el-form-item>
        <el-form-item v-if="currentAsset" label="当前状态">
          <el-tag :type="ASSET_STATUS_TAG[currentAsset.assetStatus || ''] || 'info'" size="small">
            {{ ASSET_STATUS_MAP[currentAsset.assetStatus || ''] || '未知' }}
          </el-tag>
          <span class="asset-hint">
            {{ currentAsset.deptName || '未分配部门' }} / {{ currentAsset.useUserName || '无使用人' }}
          </span>
        </el-form-item>
        <el-form-item label="办理类型" prop="flowType">
          <el-select v-model="createForm.flowType" placeholder="请选择办理类型" style="width: 100%">
            <el-option
              v-for="a in availableActions"
              :key="a.flowType"
              :label="a.label"
              :value="a.flowType"
            >
              <span>{{ a.label }}</span>
              <span class="option-tip">{{ a.description || a.tip }}</span>
            </el-option>
          </el-select>
          <div v-if="currentAsset && availableActions.length === 0" class="form-tip">
            该资产已报废，处于终态，不能继续流转
          </div>
        </el-form-item>
        <template v-if="createForm.flowType === 'borrow'">
          <el-form-item label="使用人" prop="useUserId">
            <el-select v-model="createForm.useUserId" filterable clearable placeholder="请选择使用人" style="width: 100%">
              <el-option v-for="u in users" :key="u.id" :label="u.userName || u.userId" :value="u.id as string" />
            </el-select>
          </el-form-item>
          <el-form-item label="预计归还">
            <el-date-picker
              v-model="createForm.expectReturnDate"
              type="date"
              value-format="YYYY-MM-DD"
              placeholder="选填"
              style="width: 100%"
            />
          </el-form-item>
        </template>
        <el-form-item v-if="['repair', 'scrap'].includes(createForm.flowType)" label="费用 / 残值">
          <el-input-number v-model="createForm.fee" :min="0" :precision="2" style="width: 100%" />
        </el-form-item>
        <el-form-item label="事由">
          <el-input v-model="createForm.remark" type="textarea" :rows="3" placeholder="领用用途 / 故障描述 / 报废原因" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="createVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitCreate">提交单据</el-button>
      </template>
    </el-dialog>

    <!-- 完成维修 -->
    <el-dialog v-model="completeVisible" title="完成维修" width="520px" destroy-on-close>
      <el-form :model="completeForm" label-width="105px">
        <el-form-item label="维修后状态">
          <el-radio-group v-model="completeForm.toStatus">
            <el-radio-button value="in_use">恢复在用</el-radio-button>
            <el-radio-button value="idle">转入闲置</el-radio-button>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="维修费用">
          <el-input-number v-model="completeForm.fee" :min="0" :precision="2" style="width: 100%" />
        </el-form-item>
        <el-form-item label="处理结果">
          <el-input v-model="completeForm.handleResult" type="textarea" :rows="3" placeholder="如：更换主板，测试通过" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="completeVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitComplete">确认完成</el-button>
      </template>
    </el-dialog>

    <!-- 单据详情 -->
    <el-drawer v-model="detailVisible" title="流转单据详情" size="480px">
      <el-descriptions :column="1" border>
        <el-descriptions-item label="单据编号">{{ detail.flowNo }}</el-descriptions-item>
        <el-descriptions-item label="单据类型">{{ detail.flowTypeName }}</el-descriptions-item>
        <el-descriptions-item label="处理状态">{{ detail.flowStatusName }}</el-descriptions-item>
        <el-descriptions-item label="资产编码">{{ detail.assetCode }}</el-descriptions-item>
        <el-descriptions-item label="资产名称">{{ detail.assetName }}</el-descriptions-item>
        <el-descriptions-item label="状态流转">
          {{ detail.fromStatusName }} → {{ detail.toStatusName }}
        </el-descriptions-item>
        <el-descriptions-item label="使用人">{{ detail.useUserName || '—' }}</el-descriptions-item>
        <el-descriptions-item label="使用部门">{{ detail.deptName || '—' }}</el-descriptions-item>
        <el-descriptions-item label="申请人">{{ detail.applyUserName || '—' }}</el-descriptions-item>
        <el-descriptions-item label="处理人">{{ detail.handleUserName || '—' }}</el-descriptions-item>
        <el-descriptions-item label="办理时间">{{ formatTime(detail.handleTime) }}</el-descriptions-item>
        <el-descriptions-item label="费用 / 残值">{{ formatMoney(detail.fee) }}</el-descriptions-item>
        <el-descriptions-item label="预计归还">{{ detail.expectReturnDate || '—' }}</el-descriptions-item>
        <el-descriptions-item label="事由">{{ detail.remark || '—' }}</el-descriptions-item>
        <el-descriptions-item label="处理结果">{{ detail.handleResult || '—' }}</el-descriptions-item>
      </el-descriptions>
    </el-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import {
  Search,
  Refresh,
  Plus,
  RefreshRight,
  Right,
  ArrowDown,
  Box,
  User,
  Tools,
  Delete,
} from '@element-plus/icons-vue'
import { useUserStore } from '@/stores/user'
import {
  getAssetFlowPage,
  getAllowedFlowActions,
  createAssetFlow,
  completeAssetFlow,
  cancelAssetFlow,
  rejectAssetFlow,
} from '@/api/assetFlow'
import { getAssetPage } from '@/api/asset'
import { getUserPage } from '@/api/user'
import {
  ASSET_STATUS_MAP,
  ASSET_STATUS_TAG,
  FLOW_TYPE_MAP,
  FLOW_TYPE_TAG,
  FLOW_STATUS_MAP,
  FLOW_STATUS_TAG,
  PERMISSIONS,
  type AssetFlowItem,
  type AssetFlowQuery,
  type AssetItem,
  type UserItem,
} from '@/types'

const userStore = useUserStore()
const canManage = computed(() => userStore.hasPermission(PERMISSIONS.FlowManage))

const guideSteps = [
  { code: 'idle', name: '闲置', desc: '资产入池待分配', color: '#909399', icon: Box },
  { code: 'in_use', name: '在用', desc: '登记使用人领用', color: '#67C23A', icon: User },
  { code: 'repair', name: '维修中', desc: '报修 / 维修完成', color: '#E6A23C', icon: Tools },
  { code: 'scrapped', name: '已报废', desc: '终态，不再流转', color: '#F56C6C', icon: Delete },
]

const loading = ref(false)
const submitting = ref(false)
const tableData = ref<AssetFlowItem[]>([])
const total = ref(0)
const pendingTotal = ref(0)
const dateRange = ref<[string, string] | null>(null)

const query = reactive<AssetFlowQuery>({
  keyword: '',
  flowType: undefined,
  flowStatus: undefined,
  pageIndex: 1,
  pageSize: 10,
})

const formatTime = (val?: string | null) => (val ? val.replace('T', ' ').slice(0, 16) : '—')
const formatMoney = (val?: number | null) =>
  '¥' + Number(val ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

const loadData = async () => {
  loading.value = true
  try {
    const res = await getAssetFlowPage({
      ...query,
      keyword: query.keyword || undefined,
      flowType: query.flowType || undefined,
      flowStatus: query.flowStatus || undefined,
      startDate: dateRange.value?.[0],
      endDate: dateRange.value?.[1],
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

const loadPendingCount = async () => {
  try {
    const res = await getAssetFlowPage({ flowStatus: 'pending', pageIndex: 1, pageSize: 1 })
    pendingTotal.value = res?.total ?? 0
  } catch {
    pendingTotal.value = 0
  }
}

const handleSearch = () => {
  query.pageIndex = 1
  loadData()
}

const handleReset = () => {
  query.keyword = ''
  query.flowType = undefined
  query.flowStatus = undefined
  dateRange.value = null
  query.pageIndex = 1
  loadData()
}

// ---------- 新建单据 ----------
const createVisible = ref(false)
const createFormRef = ref<FormInstance>()
const assetOptions = ref<AssetItem[]>([])
const users = ref<UserItem[]>([])
const availableActions = ref<
  Array<{ flowType: string; label: string; description?: string | null; tip?: string }>
>([])
const currentAsset = ref<AssetItem | null>(null)

const createForm = reactive({
  assetId: '',
  flowType: '',
  useUserId: '',
  expectReturnDate: null as string | null,
  fee: null as number | null,
  remark: '',
})

const createRules: FormRules = {
  assetId: [{ required: true, message: '请选择资产', trigger: 'change' }],
  flowType: [{ required: true, message: '请选择办理类型', trigger: 'change' }],
}

const loadOptions = async () => {
  try {
    const res = await getAssetPage({ pageIndex: 1, pageSize: 200 })
    assetOptions.value = res?.list ?? []
  } catch {
    assetOptions.value = []
  }
  try {
    const res = await getUserPage({ pageIndex: 1, pageSize: 100 })
    users.value = res?.list ?? []
  } catch {
    users.value = []
  }
}

const openCreateDialog = async () => {
  Object.assign(createForm, {
    assetId: '',
    flowType: '',
    useUserId: '',
    expectReturnDate: null,
    fee: null,
    remark: '',
  })
  currentAsset.value = null
  availableActions.value = []
  createVisible.value = true
  await loadOptions()
}

const onAssetChange = async (assetId: string) => {
  createForm.flowType = ''
  currentAsset.value = assetOptions.value.find((a) => a.id === assetId) ?? null
  if (!assetId) {
    availableActions.value = []
    return
  }
  try {
    availableActions.value = await getAllowedFlowActions(assetId)
  } catch {
    availableActions.value = []
  }
}

const submitCreate = async () => {
  if (!createFormRef.value) return
  await createFormRef.value.validate()
  if (createForm.flowType === 'borrow' && !createForm.useUserId) {
    ElMessage.warning('领用单必须选择使用人')
    return
  }

  const selectedUser = users.value.find((u) => u.id === createForm.useUserId)
  submitting.value = true
  try {
    await createAssetFlow({
      assetId: createForm.assetId,
      flowType: createForm.flowType,
      useUserId: createForm.useUserId || undefined,
      useUserName: selectedUser?.userName || selectedUser?.userId,
      deptId: currentAsset.value?.deptId,
      deptName: currentAsset.value?.deptName,
      fee: createForm.fee,
      expectReturnDate: createForm.expectReturnDate,
      remark: createForm.remark,
    })
    ElMessage.success('流转单据已提交')
    createVisible.value = false
    await Promise.all([loadData(), loadPendingCount()])
  } catch {
    // 拦截器已提示错误
  } finally {
    submitting.value = false
  }
}

// ---------- 完成维修 ----------
const completeVisible = ref(false)
const currentFlowId = ref('')
const completeForm = reactive({ toStatus: 'in_use', fee: null as number | null, handleResult: '' })

const openCompleteDialog = (row: AssetFlowItem) => {
  currentFlowId.value = row.id
  completeForm.toStatus = 'in_use'
  completeForm.fee = null
  completeForm.handleResult = ''
  completeVisible.value = true
}

const submitComplete = async () => {
  submitting.value = true
  try {
    await completeAssetFlow(currentFlowId.value, {
      toStatus: completeForm.toStatus,
      fee: completeForm.fee,
      handleResult: completeForm.handleResult,
    })
    ElMessage.success('维修单已完成，资产状态已恢复')
    completeVisible.value = false
    await Promise.all([loadData(), loadPendingCount()])
  } catch {
    // 拦截器已提示错误
  } finally {
    submitting.value = false
  }
}

// ---------- 取消 / 驳回 ----------
const handleClose = async (row: AssetFlowItem, command: string) => {
  const isReject = command === 'reject'
  try {
    await ElMessageBox.confirm(
      `${isReject ? '驳回' : '取消'}单据「${row.flowNo}」后，资产状态将回退到办理前，是否继续？`,
      isReject ? '驳回确认' : '取消确认',
      { type: 'warning', confirmButtonText: '确定', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    if (isReject) await rejectAssetFlow(row.id, { handleResult: '管理员驳回' })
    else await cancelAssetFlow(row.id, { handleResult: '管理员取消' })
    ElMessage.success(isReject ? '单据已驳回' : '单据已取消')
    await Promise.all([loadData(), loadPendingCount()])
  } catch {
    // 拦截器已提示错误
  }
}

// ---------- 详情 ----------
const detailVisible = ref(false)
const detail = ref<Partial<AssetFlowItem>>({})
const openDetail = (row: AssetFlowItem) => {
  detail.value = row
  detailVisible.value = true
}

onMounted(() => {
  loadData()
  loadPendingCount()
})
</script>

<style scoped>
.flow-page {
  padding: 0;
}
.guide-card {
  margin-bottom: 16px;
}
.guide-title {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 15px;
  font-weight: 600;
  color: var(--ams-text-main, #303133);
  margin-bottom: 18px;
}
.guide-steps {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}
.guide-step {
  display: flex;
  align-items: center;
  gap: 10px;
}
.step-dot {
  width: 38px;
  height: 38px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 18px;
  flex-shrink: 0;
}
.step-text {
  display: flex;
  flex-direction: column;
  line-height: 1.4;
}
.step-text b {
  font-size: 13px;
  color: var(--ams-text-main, #303133);
}
.step-text span {
  font-size: 11px;
  color: var(--ams-text-regular, #909399);
}
.step-arrow {
  margin: 0 12px;
  color: var(--ams-text-regular, #c0c4cc);
}
.guide-tip {
  margin-top: 14px;
  padding: 10px 12px;
  border-radius: 6px;
  background: var(--ams-hover-bg, #f5f7fa);
  color: var(--ams-text-regular, #909399);
  font-size: 12px;
  line-height: 1.6;
}
.search-card {
  margin-bottom: 16px;
}
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}
.toolbar-left {
  display: flex;
  gap: 10px;
}
.asset-code {
  color: var(--ams-primary, #409eff);
  margin-right: 6px;
}
.asset-name {
  color: var(--ams-text-main, #303133);
}
.status-text {
  font-size: 12px;
  color: var(--ams-text-regular, #606266);
}
.status-text.strong {
  font-weight: 600;
  color: var(--ams-text-main, #303133);
}
.status-arrow {
  margin: 0 6px;
  vertical-align: middle;
  color: var(--ams-text-regular, #c0c4cc);
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.option-tag {
  float: right;
  margin-top: 6px;
}
.option-tip {
  float: right;
  margin-left: 16px;
  font-size: 12px;
  color: #a8abb2;
}
.asset-hint {
  margin-left: 10px;
  font-size: 12px;
  color: var(--ams-text-regular, #909399);
}
.form-tip {
  font-size: 12px;
  color: #e6a23c;
  line-height: 1.5;
}
</style>
