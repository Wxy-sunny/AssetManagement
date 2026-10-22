<template>
  <div class="message-page">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="query" inline @submit.prevent>
        <el-form-item label="关键字">
          <el-input
            v-model="query.keyword"
            placeholder="标题 / 内容"
            clearable
            style="width: 220px"
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="query.readStatus" placeholder="全部状态" clearable style="width: 130px">
            <el-option label="未读" value="unread" />
            <el-option label="已读" value="read" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
          <el-button :icon="Refresh" @click="handleReset">重置</el-button>
        </el-form-item>
        <el-form-item style="float: right">
          <el-button :icon="Check" :disabled="unreadCount <= 0" @click="handleReadAll">
            全部已读
          </el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card shadow="never">
      <el-table v-loading="loading" :data="tableData" border stripe>
        <el-table-column width="70" align="center">
          <template #default="{ row }">
            <el-badge is-dot :hidden="row.isRead === true">
              <el-icon v-if="row.isRead !== true" class="unread-dot"><Bell /></el-icon>
            </el-badge>
          </template>
        </el-table-column>
        <el-table-column label="标题" min-width="240">
          <template #default="{ row }">
            <span :class="{ 'unread-title': row.isRead !== true }">{{ row.title }}</span>
            <el-tag v-if="row.isRead !== true" type="danger" size="small" class="unread-tag">未读</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="类型" width="110">
          <template #default="{ row }">
            <el-tag :type="row.msgType === 'request_reply' ? 'warning' : 'info'" size="small">
              {{ MSG_TYPE_MAP[row.msgType ?? ''] || '系统消息' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="content" label="内容" min-width="260" show-overflow-tooltip />
        <el-table-column label="时间" width="170">
          <template #default="{ row }">{{ row.createTime || '-' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="110" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openDetail(row)">查看</el-button>
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="暂无消息" />
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

    <!-- 消息详情对话框 -->
    <el-dialog v-model="dialogVisible" title="消息详情" width="560px" destroy-on-close>
      <div class="detail-title">
        <span :class="{ 'unread-title': current.isRead !== true }">{{ current.title }}</span>
        <el-tag v-if="current.isRead !== true" type="danger" size="small">未读</el-tag>
      </div>
      <div class="detail-meta">
        <span>类型：{{ MSG_TYPE_MAP[current.msgType ?? ''] || '系统消息' }}</span>
        <span style="margin-left: 16px">时间：{{ current.createTime || '-' }}</span>
      </div>
      <el-divider />
      <div class="detail-content">{{ current.content }}</div>
      <template #footer>
        <el-button @click="dialogVisible = false">关闭</el-button>
        <el-button
          v-if="current.isRead !== true"
          type="primary"
          :loading="submitting"
          @click="markRead"
          >标为已读</el-button
        >
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Search, Refresh, Bell, Check } from '@element-plus/icons-vue'
import {
  getMyMessages,
  getUnreadCount,
  markMessageRead,
  markAllRead,
} from '@/api/message'
import { MSG_TYPE_MAP, type UserMessageItem, type UserMessageQuery } from '@/types'

const loading = ref(false)
const submitting = ref(false)
const tableData = ref<UserMessageItem[]>([])
const total = ref(0)
const unreadCount = ref(0)

const query = reactive<UserMessageQuery>({
  keyword: '',
  readStatus: undefined,
  pageIndex: 1,
  pageSize: 10,
})

const dialogVisible = ref(false)
const current = ref<UserMessageItem>({})

const loadUnread = async () => {
  try {
    unreadCount.value = await getUnreadCount()
  } catch {
    unreadCount.value = 0
  }
}

const loadData = async () => {
  loading.value = true
  try {
    const res = await getMyMessages({
      ...query,
      keyword: query.keyword || undefined,
      readStatus: query.readStatus || undefined,
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
  query.readStatus = undefined
  query.pageIndex = 1
  loadData()
}

const openDetail = (row: UserMessageItem) => {
  current.value = { ...row }
  dialogVisible.value = true
}

const markRead = async () => {
  if (!current.value.id) return
  submitting.value = true
  try {
    await markMessageRead(current.value.id)
    current.value.isRead = true
    unreadCount.value = Math.max(0, unreadCount.value - 1)
    ElMessage.success('已标记为已读')
    // 刷新列表（未读筛选时该条会消失）
    await loadData()
  } catch {
    // 拦截器已提示
  } finally {
    submitting.value = false
  }
}

const handleReadAll = async () => {
  try {
    await markAllRead()
    ElMessage.success('已全部标记为已读')
    unreadCount.value = 0
    await loadData()
  } catch {
    // 拦截器已提示
  }
}

onMounted(() => {
  loadUnread()
  loadData()
})
</script>

<style scoped>
.message-page {
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
.unread-dot {
  color: #f56c6c;
  font-size: 18px;
}
.unread-title {
  font-weight: 600;
  color: #303133;
}
.unread-tag {
  margin-left: 8px;
}
.detail-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
  font-weight: 600;
}
.detail-meta {
  margin-top: 8px;
  font-size: 13px;
  color: #909399;
}
.detail-content {
  white-space: pre-wrap;
  line-height: 1.8;
  color: #303133;
}
</style>
