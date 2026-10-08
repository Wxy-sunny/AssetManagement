<template>
  <div class="asset-page">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="query" inline @submit.prevent>
        <el-form-item label="关键字">
          <el-input
            v-model="query.keyword"
            placeholder="编码 / 名称 / 规格"
            clearable
            style="width: 180px"
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item label="分类">
          <el-select v-model="query.categoryId" placeholder="全部分类" clearable style="width: 160px">
            <el-option v-for="c in categories" :key="c.id" :label="c.categoryName" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="query.assetStatus" placeholder="全部状态" clearable style="width: 140px">
            <el-option
              v-for="(label, code) in ASSET_STATUS_MAP"
              :key="code"
              :label="label"
              :value="code"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="部门">
          <el-select v-model="query.deptId" placeholder="全部部门" clearable style="width: 160px">
            <el-option
              v-for="d in depts"
              :key="d.id"
              :label="d.departmentName"
              :value="d.id"
            />
          </el-select>
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
          <el-button v-if="canAdd" type="primary" :icon="Plus" @click="openDialog()">
            新增资产
          </el-button>
          <el-button
            v-if="canDelete"
            type="danger"
            :icon="Delete"
            :disabled="selectedIds.length === 0"
            @click="handleBatchDelete"
          >
            批量删除
          </el-button>
        </div>
        <el-button v-if="canEdit" :icon="RefreshRight" @click="handleRecalculate">
          重算折旧
        </el-button>
      </div>

      <el-table
        v-loading="loading"
        :data="tableData"
        border
        stripe
        @selection-change="onSelectionChange"
      >
        <el-table-column type="selection" width="46" />
        <el-table-column prop="assetCode" label="资产编码" width="120" show-overflow-tooltip />
        <el-table-column prop="assetName" label="资产名称" min-width="150" show-overflow-tooltip />
        <el-table-column prop="categoryName" label="分类" width="110" show-overflow-tooltip />
        <el-table-column prop="spec" label="规格型号" width="120" show-overflow-tooltip />
        <el-table-column prop="quantity" label="数量" width="70" align="right" />
        <el-table-column label="原值" width="110" align="right">
          <template #default="{ row }">{{ formatMoney(row.originalValue) }}</template>
        </el-table-column>
        <el-table-column label="账面净值" width="110" align="right">
          <template #default="{ row }">{{ formatMoney(row.netValue) }}</template>
        </el-table-column>
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="ASSET_STATUS_TAG[row.assetStatus] || 'info'" size="small">
              {{ ASSET_STATUS_MAP[row.assetStatus] || '未知' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="deptName" label="使用部门" width="120" show-overflow-tooltip />
        <el-table-column prop="useUserName" label="使用人" width="100" show-overflow-tooltip />
        <el-table-column label="操作" width="150" fixed="right">
          <template #default="{ row }">
            <el-button
              v-if="canEdit"
              link
              type="primary"
              size="small"
              @click="openDialog(row)"
            >
              编辑
            </el-button>
            <el-button
              v-if="canDelete"
              link
              type="danger"
              size="small"
              @click="handleDelete(row)"
            >
              删除
            </el-button>
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="暂无资产数据" />
        </template>
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

    <!-- 新增 / 编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogTitle"
      width="760px"
      destroy-on-close
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-divider content-position="left">基础信息</el-divider>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="资产编码" prop="assetCode">
              <el-input v-model="form.assetCode" placeholder="如 ZC-001" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="资产名称" prop="assetName">
              <el-input v-model="form.assetName" placeholder="请输入资产名称" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="资产分类">
              <el-select
                v-model="form.categoryId"
                placeholder="请选择分类"
                clearable
                style="width: 100%"
                @change="onCategoryChange"
              >
                <el-option
                  v-for="c in categories"
                  :key="c.id"
                  :label="c.categoryName"
                  :value="c.id"
                />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="规格型号">
              <el-input v-model="form.spec" placeholder="如 Dell-3020" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="计量单位">
              <el-input v-model="form.unit" placeholder="台 / 套 / 件" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="数量">
              <el-input-number v-model="form.quantity" :min="1" :precision="0" style="width: 100%" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="单价">
              <el-input-number
                v-model="form.unitPrice"
                :min="0"
                :precision="2"
                style="width: 100%"
                @change="calcOriginal"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="资产原值">
              <el-input-number
                v-model="form.originalValue"
                :min="0"
                :precision="2"
                style="width: 100%"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="供应商">
              <el-input v-model="form.supplier" placeholder="选填" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="采购日期">
              <el-date-picker
                v-model="form.purchaseDate"
                type="date"
                value-format="YYYY-MM-DD"
                placeholder="选择日期"
                style="width: 100%"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="使用部门">
              <el-select
                v-model="form.deptId"
                placeholder="请选择部门"
                clearable
                style="width: 100%"
                @change="onDeptChange"
              >
                <el-option
                  v-for="d in depts"
                  :key="d.id"
                  :label="d.departmentName"
                  :value="d.id"
                />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="使用人">
              <el-select
                v-model="form.useUserId"
                placeholder="请选择使用人"
                clearable
                filterable
                style="width: 100%"
                @change="onUserChange"
              >
                <el-option
                  v-for="u in users"
                  :key="u.id"
                  :label="u.userName || u.userId"
                  :value="u.id"
                />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="存放位置">
              <el-input v-model="form.location" placeholder="如 A 座 301" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="资产状态">
              <el-select v-model="form.assetStatus" style="width: 100%">
                <el-option
                  v-for="(label, code) in ASSET_STATUS_MAP"
                  :key="code"
                  :label="label"
                  :value="code"
                />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>

        <el-divider content-position="left">折旧信息</el-divider>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="折旧方式">
              <el-select v-model="form.depreciationMethod" style="width: 100%">
                <el-option
                  v-for="(label, code) in DEPRECIATION_METHOD_MAP"
                  :key="code"
                  :label="label"
                  :value="code"
                />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="使用年限(月)">
              <el-input-number v-model="form.usefulLifeMonths" :min="1" style="width: 100%" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="净残值率(%)">
              <el-input-number
                v-model="form.salvageRate"
                :min="0"
                :max="100"
                :precision="2"
                style="width: 100%"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="折旧起始日">
              <el-date-picker
                v-model="form.depreciationStartDate"
                type="date"
                value-format="YYYY-MM-DD"
                placeholder="默认取采购日期"
                style="width: 100%"
              />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="备注">
              <el-input v-model="form.remark" type="textarea" :rows="2" placeholder="选填" />
            </el-form-item>
          </el-col>
        </el-row>

        <!-- 只读的折旧计算结果预览 -->
        <el-alert type="info" :closable="false" class="depreciation-preview">
          <template #default>
            <span>
              预计净残值 <b>{{ formatMoney(form.salvageValue) }}</b> ｜ 月折旧额
              <b>{{ formatMoney(form.monthlyDepreciation) }}</b> ｜ 累计折旧
              <b>{{ formatMoney(form.accumulatedDepreciation) }}</b> ｜ 账面净值
              <b>{{ formatMoney(form.netValue) }}</b>
              <span class="preview-tip">（保存后由服务端按当前日期自动计算）</span>
            </span>
          </template>
        </el-alert>
      </el-form>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitForm">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Search, Refresh, Plus, Delete, RefreshRight } from '@element-plus/icons-vue'
import { useUserStore } from '@/stores/user'
import {
  getAssetPage,
  addAsset,
  updateAsset,
  deleteAsset,
  batchDeleteAsset,
  recalculateDepreciation,
} from '@/api/asset'
import { getCategoryList } from '@/api/category'
import { getDeptList } from '@/api/department'
import { getUserPage } from '@/api/user'
import {
  ASSET_STATUS_MAP,
  ASSET_STATUS_TAG,
  DEPRECIATION_METHOD_MAP,
  PERMISSIONS,
  type AssetItem,
  type AssetQuery,
  type CategoryItem,
  type DepartmentItem,
  type UserItem,
} from '@/types'

const loading = ref(false)
const submitting = ref(false)
const tableData = ref<AssetItem[]>([])
const total = ref(0)
const selectedIds = ref<string[]>([])

// 按钮级权限（后端接口同样会校验，这里只是提前隐藏，避免用户点了被拒）
const userStore = useUserStore()
const canAdd = computed(() => userStore.hasPermission(PERMISSIONS.AssetAdd))
const canEdit = computed(() => userStore.hasPermission(PERMISSIONS.AssetEdit))
const canDelete = computed(() => userStore.hasPermission(PERMISSIONS.AssetDelete))

// 下拉数据源
const categories = ref<CategoryItem[]>([])
const depts = ref<DepartmentItem[]>([])
const users = ref<UserItem[]>([])

const query = reactive<AssetQuery>({
  keyword: '',
  categoryId: undefined,
  assetStatus: undefined,
  deptId: undefined,
  pageIndex: 1,
  pageSize: 10,
})

// 表单
const dialogVisible = ref(false)
const dialogTitle = ref('新增资产')
const formRef = ref<FormInstance>()
const editingId = ref<string | null>(null)

const createDefaultForm = (): AssetItem => ({
  assetCode: '',
  assetName: '',
  categoryId: null,
  categoryName: '',
  spec: '',
  unit: '',
  quantity: 1,
  supplier: '',
  unitPrice: 0,
  originalValue: 0,
  purchaseDate: null,
  deptId: null,
  deptName: '',
  useUserId: null,
  useUserName: '',
  location: '',
  assetStatus: 'in_use',
  depreciationMethod: 'straight-line',
  usefulLifeMonths: 60,
  salvageRate: 5,
  salvageValue: 0,
  monthlyDepreciation: 0,
  depreciationStartDate: null,
  usedMonths: 0,
  accumulatedDepreciation: 0,
  netValue: 0,
  isEnabled: true,
  remark: '',
})

const form = ref<AssetItem>(createDefaultForm())

const rules: FormRules = {
  assetCode: [{ required: true, message: '请输入资产编码', trigger: 'blur' }],
  assetName: [{ required: true, message: '请输入资产名称', trigger: 'blur' }],
}

const formatMoney = (val?: number | null) => {
  const n = Number(val ?? 0)
  return '¥' + n.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// 加载列表
const loadData = async () => {
  loading.value = true
  try {
    const res = await getAssetPage({
      ...query,
      keyword: query.keyword || undefined,
      categoryId: query.categoryId || undefined,
      assetStatus: query.assetStatus || undefined,
      deptId: query.deptId || undefined,
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

// 加载下拉数据
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
  try {
    const res = await getUserPage({ pageIndex: 1, pageSize: 100 })
    users.value = res?.list ?? []
  } catch {
    users.value = []
  }
}

const handleSearch = () => {
  query.pageIndex = 1
  loadData()
}

const handleReset = () => {
  query.keyword = ''
  query.categoryId = undefined
  query.assetStatus = undefined
  query.deptId = undefined
  query.pageIndex = 1
  loadData()
}

const onSelectionChange = (rows: AssetItem[]) => {
  selectedIds.value = rows.map((r) => r.id).filter(Boolean) as string[]
}

// 选择分类 / 部门 / 使用人时同步冗余名称
const onCategoryChange = (id: string) => {
  form.value.categoryName = categories.value.find((c) => c.id === id)?.categoryName ?? ''
}
const onDeptChange = (id: string) => {
  form.value.deptName = depts.value.find((d) => d.id === id)?.departmentName ?? ''
}
const onUserChange = (id: string) => {
  const u = users.value.find((x) => x.id === id)
  form.value.useUserName = u?.userName ?? u?.userId ?? ''
}

// 单价或数量变化时自动回填原值
const calcOriginal = () => {
  const price = Number(form.value.unitPrice ?? 0)
  const qty = Number(form.value.quantity ?? 0)
  form.value.originalValue = Number((price * qty).toFixed(2))
}

const openDialog = (row?: AssetItem) => {
  editingId.value = row?.id ?? null
  dialogTitle.value = row ? '编辑资产' : '新增资产'
  form.value = row ? { ...createDefaultForm(), ...row } : createDefaultForm()
  dialogVisible.value = true
}

const submitForm = async () => {
  if (!formRef.value) return
  await formRef.value.validate()

  submitting.value = true
  try {
    if (editingId.value) {
      await updateAsset({ ...form.value, id: editingId.value })
      ElMessage.success('修改成功')
    } else {
      await addAsset(form.value)
      ElMessage.success('新增成功')
    }
    dialogVisible.value = false
    loadData()
  } catch {
    // 拦截器已提示错误
  } finally {
    submitting.value = false
  }
}

const handleDelete = async (row: AssetItem) => {
  try {
    await ElMessageBox.confirm(`确定删除资产「${row.assetName}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await deleteAsset(row.id as string)
    ElMessage.success('删除成功')
    loadData()
  } catch {
    // 忽略
  }
}

const handleBatchDelete = async () => {
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedIds.value.length} 条资产吗？`, '批量删除', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await batchDeleteAsset(selectedIds.value)
    ElMessage.success('删除成功')
    loadData()
  } catch {
    // 忽略
  }
}

const handleRecalculate = async () => {
  try {
    await ElMessageBox.confirm('将按当前日期重算全部资产的折旧与净值，是否继续？', '重算折旧', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await recalculateDepreciation()
    ElMessage.success('折旧重算完成')
    loadData()
  } catch {
    // 忽略
  }
}

onMounted(() => {
  loadOptions()
  loadData()
})
</script>

<style scoped>
.asset-page {
  padding: 0;
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
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.depreciation-preview {
  margin-top: 4px;
}
.preview-tip {
  color: #a8abb2;
  margin-left: 4px;
}
</style>
