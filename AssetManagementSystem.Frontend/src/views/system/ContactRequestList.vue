<template>
  <div class="request-page">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="query" inline @submit.prevent>
        <el-form-item label="关键字">
          <el-input
            v-model="query.keyword"
            placeholder="账号 / 手机号 / 说明"
            clearable
            style="width: 220px"
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="query.status" placeholder="全部状态" clearable style="width: 130px">
            <el-option label="待处理" value="pending" />
            <el-option label="已处理" value="handled" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
          <el-button :icon="Refresh" @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card shadow="never">
      <el-table v-loading="loading" :data="tableData" border stripe>
        <el-table-column prop="account" label="账号/姓名" min-width="140" show-overflow-tooltip />
        <el-table-column prop="mobile" label="手机号" width="130" />
        <el-table-column label="类型" width="110">
          <template #default="{ row }">
            <el-tag :type="row.requestType === 'reset_password' ? 'warning' : 'info'" size="small">
              {{ REQUEST_TYPE_MAP[row.requestType ?? ''] || '其他' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="content" label="说明" min-width="220" show-overflow-tooltip />
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.status === 'pending' ? 'warning' : 'success'" size="small">
              {{ REQUEST_STATUS_MAP[row.status] || '待处理' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="handlerName" label="处理人" width="110" />
        <el-table-column prop="handleRemark" label="处理备注" min-width="160" show-overflow-tooltip />
        <el-table-column label="提交时间" width="170">
          <template #default="{ row }">{{ row.createTime || '-' }}</template>
        </el-table-column>
        <el-table-column label="处理时间" width="170">
          <template #default="{ row }">{{ row.handleTime || '-' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="100" fixed="right">
          <template #default="{ row }">
            <el-button
              v-if="canManage && row.status === 'pending'"
              link
              type="primary"
              size="small"
              @click="openHandle(row)"
              >处理</el-button
            >
            <el-button
              v-else-if="row.status !== 'pending'"
              link
              type="info"
              size="small"
              @click="openHandle(row)"
              >查看</el-button
            >
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="暂无联系请求" />
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

    <!-- 处理 / 查看对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogTitle"
      width="560px"
      destroy-on-close
      :close-on-click-modal="false"
    >
      <el-descriptions :column="1" border>
        <el-descriptions-item label="账号/姓名">{{ current.account || '-' }}</el-descriptions-item>
        <el-descriptions-item label="手机号">{{ current.mobile || '-' }}</el-descriptions-item>
        <el-descriptions-item label="类型">
          {{ REQUEST_TYPE_MAP[current.requestType ?? ''] || '其他' }}
        </el-descriptions-item>
        <el-descriptions-item label="说明">{{ current.content || '-' }}</el-descriptions-item>
        <el-descriptions-item label="提交时间">{{ current.createTime || '-' }}</el-descriptions-item>
        <el-descriptions-item v-if="current.status === 'handled'" label="处理人">
          {{ current.handlerName || '-' }}
        </el-descriptions-item>
      </el-descriptions>

      <el-alert
        v-if="current.status === 'pending'"
        type="info"
        :closable="false"
        show-icon
        class="notify-tip"
      >
        处理后将自动向提交人发送一条站内消息通知（需能识别其账号）；如填写了手机号，可选择同时发送短信。
      </el-alert>

      <el-form label-width="80px" class="handle-form">
        <el-form-item :label="current.status === 'handled' ? '处理备注' : '处理备注'">
          <el-input
            v-model="remark"
            type="textarea"
            :rows="3"
            :disabled="current.status === 'handled'"
            placeholder="请填写处理备注（如已重置密码、已电话联系等）"
            maxlength="500"
            show-word-limit
          />
        </el-form-item>
        <el-form-item v-if="current.status === 'pending'" label="短信通知">
          <el-checkbox v-model="notifySms" :disabled="!current.mobile">
            同时发送短信{{ current.mobile ? `（${current.mobile}）` : '（未填写手机号）' }}
          </el-checkbox>
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="dialogVisible = false">关闭</el-button>
        <el-button
          v-if="canManage && current.status === 'pending'"
          type="primary"
          :loading="submitting"
          @click="submitHandle"
          >标记为已处理</el-button
        >
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Search, Refresh } from '@element-plus/icons-vue'
import { useUserStore } from '@/stores/user'
import { PERMISSIONS } from '@/types'
import {
  getRequestPage,
  handleRequest,
  REQUEST_TYPE_MAP,
  REQUEST_STATUS_MAP,
  type ContactRequestItem,
  type ContactRequestQuery,
} from '@/api/request'

const userStore = useUserStore()
// 无「联系请求处理」权限时，隐藏处理/标记已处理入口（仅保留查看）
const canManage = computed(() => userStore.hasPermission(PERMISSIONS.RequestManage))

const loading = ref(false)
const submitting = ref(false)
const tableData = ref<ContactRequestItem[]>([])
const total = ref(0)

const query = reactive<ContactRequestQuery>({
  keyword: '',
  status: undefined,
  pageIndex: 1,
  pageSize: 10,
})

const dialogVisible = ref(false)
const dialogTitle = ref('处理联系请求')
const current = ref<ContactRequestItem>({})
const remark = ref('')
const notifySms = ref(true)

const loadData = async () => {
  loading.value = true
  try {
    const res = await getRequestPage({
      ...query,
      keyword: query.keyword || undefined,
      status: query.status || undefined,
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
  query.status = undefined
  query.pageIndex = 1
  loadData()
}

const openHandle = (row: ContactRequestItem) => {
  current.value = { ...row }
  remark.value = row.handleRemark || ''
  notifySms.value = true
  dialogTitle.value = row.status === 'handled' ? '查看联系请求' : '处理联系请求'
  dialogVisible.value = true
}

const submitHandle = async () => {
  if (!current.value.id) return
  submitting.value = true
  try {
    await handleRequest(current.value.id, remark.value.trim(), notifySms.value)
    ElMessage.success('已标记为已处理，处理结果将通知提交人')
    dialogVisible.value = false
    loadData()
  } catch {
    // 失败时拦截器已统一提示
  } finally {
    submitting.value = false
  }
}

onMounted(loadData)
</script>

<style scoped>
.request-page {
  padding: 0;
}
.search-card {
  margin-bottom: 16px;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.handle-form {
  margin-top: 16px;
}
.notify-tip {
  margin-bottom: 12px;
}
</style>
