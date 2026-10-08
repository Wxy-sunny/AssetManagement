<template>
  <div class="dept-page">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="query" inline @submit.prevent>
        <el-form-item label="关键字">
          <el-input
            v-model="query.keyword"
            placeholder="编码 / 名称"
            clearable
            style="width: 200px"
            @keyup.enter="loadData"
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :icon="Search" @click="loadData">查询</el-button>
          <el-button :icon="Refresh" @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card shadow="never">
      <div class="toolbar">
        <el-button v-if="canManage" type="primary" :icon="Plus" @click="openDialog()">
          新增部门
        </el-button>
      </div>

      <el-table
        v-loading="loading"
        :data="tableData"
        row-key="id"
        border
        default-expand-all
        :tree-props="{ children: 'children' }"
      >
        <el-table-column prop="departmentId" label="部门编码" min-width="140" />
        <el-table-column prop="departmentName" label="部门名称" min-width="200" />
        <el-table-column prop="leaderName" label="负责人" width="110" />
        <el-table-column prop="phone" label="联系电话" width="140" />
        <el-table-column prop="sortOrder" label="排序" width="80" align="center" />
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">
              {{ row.isEnabled ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="description" label="描述" min-width="180" show-overflow-tooltip />
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <template v-if="canManage">
              <el-button link type="primary" size="small" @click="openDialog(row, true)">
                新增下级
              </el-button>
              <el-button link type="primary" size="small" @click="openDialog(row)">编辑</el-button>
              <el-button link type="danger" size="small" @click="handleDelete(row)">删除</el-button>
            </template>
            <span v-else class="no-perm">无操作权限</span>
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="暂无部门数据" />
        </template>
      </el-table>
    </el-card>

    <!-- 新增 / 编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogTitle"
      width="600px"
      destroy-on-close
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="上级部门">
              <el-tree-select
                v-model="form.parentId"
                :data="treeOptions"
                :props="treeProps"
                check-strictly
                clearable
                node-key="id"
                placeholder="顶级部门（不选则为根级）"
                style="width: 100%"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="部门编码" prop="departmentId">
              <el-input v-model="form.departmentId" placeholder="如 DEPT-001" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="部门名称" prop="departmentName">
              <el-input v-model="form.departmentName" placeholder="请输入部门名称" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="负责人">
              <el-input v-model="form.leaderName" placeholder="选填" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="联系电话">
              <el-input v-model="form.phone" placeholder="选填" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="排序号">
              <el-input-number v-model="form.sortOrder" :min="0" style="width: 100%" />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="描述">
              <el-input v-model="form.description" type="textarea" :rows="2" placeholder="选填" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="状态">
              <el-switch v-model="form.isEnabled" active-text="启用" inactive-text="禁用" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitForm">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Search, Refresh, Plus } from '@element-plus/icons-vue'
import { getDeptTree, addDept, updateDept, deleteDept } from '@/api/department'
import { useUserStore } from '@/stores/user'
import { PERMISSIONS, type DepartmentItem } from '@/types'

// 部门写操作权限（后端同样会校验）
const userStore = useUserStore()
const canManage = computed(() => userStore.hasPermission(PERMISSIONS.DeptManage))

const loading = ref(false)
const submitting = ref(false)
const rawTree = ref<DepartmentItem[]>([])
const query = ref({ keyword: '' })

const dialogVisible = ref(false)
const dialogTitle = ref('新增部门')
const formRef = ref<FormInstance>()
const editingId = ref<string | null>(null)

const createDefaultForm = (): Partial<DepartmentItem> => ({
  parentId: '',
  departmentId: '',
  departmentName: '',
  leaderName: '',
  phone: '',
  sortOrder: 0,
  description: '',
  isEnabled: true,
})

const form = ref<Partial<DepartmentItem>>(createDefaultForm())

const rules: FormRules = {
  departmentId: [{ required: true, message: '请输入部门编码', trigger: 'blur' }],
  departmentName: [{ required: true, message: '请输入部门名称', trigger: 'blur' }],
}

const treeProps = { label: 'departmentName', children: 'children' }

// 清洗空 children，避免表格出现空的展开箭头
const normalizeTree = (list: DepartmentItem[]): DepartmentItem[] =>
  (list ?? []).map((item) => {
    const node: DepartmentItem = { ...item }
    if (node.children && node.children.length > 0) {
      node.children = normalizeTree(node.children)
    } else {
      delete node.children
    }
    return node
  })

const tableData = computed(() => normalizeTree(rawTree.value))

// 上级部门下拉：排除自身及其所有子孙，避免把上级设成自己或子孙而成环
const treeOptions = computed(() => {
  const list = tableData.value
  const selfId = editingId.value
  if (!selfId) return list

  const findNode = (nodes: DepartmentItem[], id: string): DepartmentItem | null => {
    for (const n of nodes) {
      if (n.id === id) return n
      if (n.children) {
        const hit = findNode(n.children, id)
        if (hit) return hit
      }
    }
    return null
  }

  // 需要排除的 Id 集合
  const banned = new Set<string>([selfId])
  const collectIds = (node: DepartmentItem) => {
    for (const c of node.children ?? []) {
      if (!c.id) continue
      banned.add(c.id)
      collectIds(c)
    }
  }

  const me = findNode(list, selfId)
  if (me) collectIds(me)

  const filter = (nodes: DepartmentItem[]): DepartmentItem[] =>
    nodes
      .filter((n) => !n.id || !banned.has(n.id))
      .map((n) => (n.children ? { ...n, children: filter(n.children) } : n))

  return filter(list)
})

const loadData = async () => {
  loading.value = true
  try {
    rawTree.value = await getDeptTree(query.value.keyword || undefined)
  } catch {
    rawTree.value = []
  } finally {
    loading.value = false
  }
}

const handleReset = () => {
  query.value.keyword = ''
  loadData()
}

/**
 * 打开对话框
 * @param row 编辑的部门；为空表示新增根级
 * @param asChild 为 true 时以 row 作为上级新增下级
 */
const openDialog = (row?: DepartmentItem, asChild = false) => {
  if (row && asChild) {
    editingId.value = null
    dialogTitle.value = '新增下级部门'
    form.value = { ...createDefaultForm(), parentId: row.id }
  } else if (row) {
    editingId.value = row.id ?? null
    dialogTitle.value = '编辑部门'
    form.value = {
      id: row.id,
      parentId: row.parentId || '',
      departmentId: row.departmentId,
      departmentName: row.departmentName,
      leaderName: row.leaderName ?? '',
      phone: row.phone ?? '',
      sortOrder: row.sortOrder ?? 0,
      description: row.description ?? '',
      isEnabled: row.isEnabled ?? true,
    }
  } else {
    editingId.value = null
    dialogTitle.value = '新增部门'
    form.value = createDefaultForm()
  }
  dialogVisible.value = true
}

const submitForm = async () => {
  if (!formRef.value) return
  await formRef.value.validate()

  submitting.value = true
  try {
    const payload = {
      ...form.value,
      parentId: form.value.parentId || '',
    }
    if (editingId.value) {
      await updateDept({ ...payload, id: editingId.value })
      ElMessage.success('修改成功')
    } else {
      await addDept(payload)
      ElMessage.success('新增成功')
    }
    dialogVisible.value = false
    loadData()
  } catch {
    // 拦截器已提示
  } finally {
    submitting.value = false
  }
}

const handleDelete = async (row: DepartmentItem) => {
  try {
    await ElMessageBox.confirm(`确定删除部门「${row.departmentName}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await deleteDept(row.id as string)
    ElMessage.success('删除成功')
    loadData()
  } catch {
    // 忽略
  }
}

onMounted(loadData)
</script>

<style scoped>
.dept-page {
  padding: 0;
}
.search-card {
  margin-bottom: 16px;
}
.toolbar {
  margin-bottom: 16px;
}
.no-perm {
  font-size: 12px;
  color: #c0c4cc;
}
</style>
