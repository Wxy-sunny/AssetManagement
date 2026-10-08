<template>
  <div class="category-page">
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
        <div class="toolbar-left">
          <el-button v-if="canManage" type="primary" :icon="Plus" @click="openDialog()">
            新增分类
          </el-button>
        </div>
        <el-button :icon="RefreshRight" @click="loadData">刷新</el-button>
      </div>

      <el-table
        v-loading="loading"
        :data="tableData"
        row-key="id"
        border
        default-expand-all
        :tree-props="{ children: 'children' }"
      >
        <el-table-column prop="categoryCode" label="分类编码" min-width="160" />
        <el-table-column prop="categoryName" label="分类名称" min-width="200" />
        <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">
              {{ row.isEnabled ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
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
          <el-empty description="暂无分类数据" />
        </template>
      </el-table>
    </el-card>

    <!-- 新增 / 编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogTitle"
      width="560px"
      destroy-on-close
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-form-item label="上级分类">
          <el-tree-select
            v-model="form.parentId"
            :data="treeOptions"
            :props="treeProps"
            check-strictly
            clearable
            placeholder="顶级分类（不选则为根级）"
            style="width: 100%"
            node-key="id"
          />
        </el-form-item>
        <el-form-item label="分类编码" prop="categoryCode">
          <el-input v-model="form.categoryCode" placeholder="如 IT / IT-PC" />
        </el-form-item>
        <el-form-item label="分类名称" prop="categoryName">
          <el-input v-model="form.categoryName" placeholder="请输入分类名称" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="选填" />
        </el-form-item>
        <el-form-item label="状态">
          <el-switch
            v-model="form.isEnabled"
            active-text="启用"
            inactive-text="禁用"
          />
        </el-form-item>
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
import { Search, Refresh, Plus, RefreshRight } from '@element-plus/icons-vue'
import { getCategoryTree, addCategory, updateCategory, deleteCategory } from '@/api/category'
import { useUserStore } from '@/stores/user'
import { PERMISSIONS, type CategoryItem } from '@/types'

// 分类写操作权限（后端同样会校验）
const userStore = useUserStore()
const canManage = computed(() => userStore.hasPermission(PERMISSIONS.CategoryManage))

const loading = ref(false)
const submitting = ref(false)
const rawTree = ref<CategoryItem[]>([])
const query = ref({ keyword: '' })

const dialogVisible = ref(false)
const dialogTitle = ref('新增分类')
const formRef = ref<FormInstance>()
const editingId = ref<string | null>(null)

const createDefaultForm = (): Partial<CategoryItem> => ({
  parentId: '',
  categoryCode: '',
  categoryName: '',
  description: '',
  isEnabled: true,
})

const form = ref<Partial<CategoryItem>>(createDefaultForm())

const rules: FormRules = {
  categoryCode: [{ required: true, message: '请输入分类编码', trigger: 'blur' }],
  categoryName: [{ required: true, message: '请输入分类名称', trigger: 'blur' }],
}

const treeProps = { label: 'categoryName', children: 'children' }

// 清洗空 children，避免表格出现空的展开箭头
const normalizeTree = (list: CategoryItem[]): CategoryItem[] =>
  (list ?? []).map((item) => {
    const node: CategoryItem = { ...item }
    if (node.children && node.children.length > 0) {
      node.children = normalizeTree(node.children)
    } else {
      delete node.children
    }
    return node
  })

const tableData = computed(() => normalizeTree(rawTree.value))

// 上级分类下拉（排除自身，避免把自己设为上级）
const treeOptions = computed(() => {
  const list = tableData.value
  if (!editingId.value) return list
  const removeSelf = (nodes: CategoryItem[]): CategoryItem[] =>
    nodes
      .filter((n) => n.id !== editingId.value)
      .map((n) => (n.children ? { ...n, children: removeSelf(n.children) } : n))
  return removeSelf(list)
})

const loadData = async () => {
  loading.value = true
  try {
    rawTree.value = await getCategoryTree(query.value.keyword || undefined)
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
 * @param row 编辑的分类；为空表示新增根级
 * @param asChild 为 true 时以 row 作为上级新增下级
 */
const openDialog = (row?: CategoryItem, asChild = false) => {
  if (row && asChild) {
    editingId.value = null
    dialogTitle.value = '新增下级分类'
    form.value = { ...createDefaultForm(), parentId: row.id }
  } else if (row) {
    editingId.value = row.id ?? null
    dialogTitle.value = '编辑分类'
    form.value = {
      id: row.id,
      parentId: row.parentId || '',
      categoryCode: row.categoryCode,
      categoryName: row.categoryName,
      description: row.description ?? '',
      isEnabled: row.isEnabled ?? true,
    }
  } else {
    editingId.value = null
    dialogTitle.value = '新增分类'
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
      // 根级分类传空字符串，后端按空父级处理
      parentId: form.value.parentId || '',
    }
    if (editingId.value) {
      await updateCategory({ ...payload, id: editingId.value })
      ElMessage.success('修改成功')
    } else {
      await addCategory(payload)
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

const handleDelete = async (row: CategoryItem) => {
  try {
    await ElMessageBox.confirm(`确定删除分类「${row.categoryName}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await deleteCategory(row.id)
    ElMessage.success('删除成功')
    loadData()
  } catch {
    // 忽略
  }
}

onMounted(loadData)
</script>

<style scoped>
.category-page {
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
.no-perm {
  font-size: 12px;
  color: #c0c4cc;
}
</style>
