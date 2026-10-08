<template>
  <div class="notification-page">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="query" inline @submit.prevent>
        <el-form-item label="关键字">
          <el-input
            v-model="query.keyword"
            placeholder="通知标题"
            clearable
            style="width: 200px"
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item label="类型">
          <el-select v-model="query.noticeType" placeholder="全部类型" clearable style="width: 140px">
            <el-option
              v-for="(label, code) in NOTICE_TYPE_MAP"
              :key="code"
              :label="label"
              :value="code"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="query.isPublished" placeholder="全部状态" clearable style="width: 130px">
            <el-option label="已发布" :value="true" />
            <el-option label="草稿" :value="false" />
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
        <el-button type="primary" :icon="Plus" @click="openDialog()">新增通知</el-button>
      </div>

      <el-table v-loading="loading" :data="tableData" border stripe>
        <el-table-column prop="title" label="标题" min-width="220" show-overflow-tooltip />
        <el-table-column label="类型" width="110">
          <template #default="{ row }">
            <el-tag :type="NOTICE_TYPE_TAG[row.noticeType] || 'info'" size="small">
              {{ NOTICE_TYPE_MAP[row.noticeType] || '通知' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isPublished ? 'success' : 'info'" size="small">
              {{ row.isPublished ? '已发布' : '草稿' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="publisher" label="发布人" width="110" />
        <el-table-column label="发布时间" width="170">
          <template #default="{ row }">{{ row.publishTime || '-' }}</template>
        </el-table-column>
        <el-table-column label="创建时间" width="170">
          <template #default="{ row }">{{ row.createTime || '-' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="210" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openDialog(row)">编辑</el-button>
            <el-button
              link
              :type="row.isPublished ? 'warning' : 'success'"
              size="small"
              @click="handlePublish(row)"
            >
              {{ row.isPublished ? '撤回' : '发布' }}
            </el-button>
            <el-button link type="danger" size="small" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="暂无通知" />
        </template>
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

    <!-- 新增 / 编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogTitle"
      width="640px"
      destroy-on-close
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="通知标题" prop="title">
          <el-input v-model="form.title" placeholder="请输入通知标题" maxlength="100" />
        </el-form-item>
        <el-form-item label="通知类型">
          <el-select v-model="form.noticeType" style="width: 100%">
            <el-option
              v-for="(label, code) in NOTICE_TYPE_MAP"
              :key="code"
              :label="label"
              :value="code"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="通知内容" prop="content">
          <el-input
            v-model="form.content"
            type="textarea"
            :rows="8"
            placeholder="请输入通知内容"
            maxlength="5000"
            show-word-limit
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitForm">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Search, Refresh, Plus } from '@element-plus/icons-vue'
import {
  getNotificationPage,
  addNotification,
  updateNotification,
  deleteNotification,
  publishNotification,
} from '@/api/notification'
import {
  NOTICE_TYPE_MAP,
  NOTICE_TYPE_TAG,
  type NotificationItem,
  type NotificationQuery,
} from '@/types'

const loading = ref(false)
const submitting = ref(false)
const tableData = ref<NotificationItem[]>([])
const total = ref(0)

const query = reactive<NotificationQuery>({
  keyword: '',
  noticeType: undefined,
  isPublished: undefined,
  pageIndex: 1,
  pageSize: 10,
})

const dialogVisible = ref(false)
const dialogTitle = ref('新增通知')
const formRef = ref<FormInstance>()
const editingId = ref<string | null>(null)

const createDefaultForm = (): Partial<NotificationItem> => ({
  title: '',
  content: '',
  noticeType: 'notice',
})

const form = ref<Partial<NotificationItem>>(createDefaultForm())

const rules: FormRules = {
  title: [{ required: true, message: '请输入通知标题', trigger: 'blur' }],
  content: [{ required: true, message: '请输入通知内容', trigger: 'blur' }],
}

const loadData = async () => {
  loading.value = true
  try {
    const res = await getNotificationPage({
      ...query,
      keyword: query.keyword || undefined,
      noticeType: query.noticeType || undefined,
      isPublished: query.isPublished ?? undefined,
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

const handleSearch = () => {
  query.pageIndex = 1
  loadData()
}

const handleReset = () => {
  query.keyword = ''
  query.noticeType = undefined
  query.isPublished = undefined
  query.pageIndex = 1
  loadData()
}

const openDialog = (row?: NotificationItem) => {
  editingId.value = row?.id ?? null
  dialogTitle.value = row ? '编辑通知' : '新增通知'
  form.value = row ? { ...row } : createDefaultForm()
  dialogVisible.value = true
}

const submitForm = async () => {
  if (!formRef.value) return
  await formRef.value.validate()

  submitting.value = true
  try {
    if (editingId.value) {
      await updateNotification({ ...form.value, id: editingId.value })
      ElMessage.success('修改成功')
    } else {
      await addNotification(form.value)
      ElMessage.success('新增成功，可在列表中发布')
    }
    dialogVisible.value = false
    loadData()
  } catch {
    // 失败时拦截器已提示
  } finally {
    submitting.value = false
  }
}

const handlePublish = async (row: NotificationItem) => {
  const next = !row.isPublished
  const action = next ? '发布' : '撤回'
  try {
    await ElMessageBox.confirm(`确定${action}通知「${row.title}」吗？`, `${action}确认`, {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await publishNotification(row.id as string, next)
    ElMessage.success(next ? '发布成功，全员可见' : '已撤回，仅管理端可见')
    loadData()
  } catch {
    // 失败时拦截器已提示
  }
}

const handleDelete = async (row: NotificationItem) => {
  try {
    await ElMessageBox.confirm(`确定删除通知「${row.title}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await deleteNotification(row.id as string)
    ElMessage.success('删除成功')
    loadData()
  } catch {
    // 失败时拦截器已提示
  }
}

onMounted(loadData)
</script>

<style scoped>
.notification-page {
  padding: 0;
}
.search-card {
  margin-bottom: 16px;
}
.toolbar {
  margin-bottom: 16px;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
