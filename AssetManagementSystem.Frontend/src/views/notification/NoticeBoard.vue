<template>
  <div class="notice-board" v-loading="loading">
    <el-card shadow="never">
      <template #header>
        <div class="card-header">
          <span>系统通知</span>
          <el-input
            v-model="query.keyword"
            placeholder="搜索通知标题"
            clearable
            style="width: 220px"
            @keyup.enter="handleSearch"
            @clear="handleSearch"
          >
            <template #prefix>
              <el-icon><Search /></el-icon>
            </template>
          </el-input>
        </div>
      </template>

      <template v-if="tableData.length > 0">
        <div
          v-for="item in tableData"
          :key="item.id"
          class="notice-item"
          @click="openDetail(item)"
        >
          <div class="notice-main">
            <el-tag :type="NOTICE_TYPE_TAG[item.noticeType ?? ''] || 'info'" size="small">
              {{ NOTICE_TYPE_MAP[item.noticeType ?? ''] || '通知' }}
            </el-tag>
            <span class="notice-title">{{ item.title }}</span>
          </div>
          <div class="notice-meta">
            <span>{{ item.publisher || '-' }}</span>
            <el-divider direction="vertical" />
            <span>{{ item.publishTime || '-' }}</span>
          </div>
        </div>
      </template>
      <el-empty v-else description="暂无通知" />

      <div class="pagination">
        <el-pagination
          v-model:current-page="query.pageIndex"
          v-model:page-size="query.pageSize"
          :page-sizes="[10, 20, 50]"
          :total="total"
          layout="total, sizes, prev, pager, next"
          background
          @size-change="loadData"
          @current-change="loadData"
        />
      </div>
    </el-card>

    <!-- 通知详情 -->
    <el-dialog v-model="detailVisible" :title="current?.title" width="620px">
      <div class="detail-meta">
        <el-tag :type="NOTICE_TYPE_TAG[current?.noticeType ?? ''] || 'info'" size="small">
          {{ NOTICE_TYPE_MAP[current?.noticeType ?? ''] || '通知' }}
        </el-tag>
        <span class="meta-text">
          {{ current?.publisher || '-' }} 发布于 {{ current?.publishTime || '-' }}
        </span>
      </div>
      <div class="detail-content">{{ current?.content }}</div>
      <template #footer>
        <el-button @click="detailVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { Search } from '@element-plus/icons-vue'
import { getPublishedNotifications } from '@/api/notification'
import {
  NOTICE_TYPE_MAP,
  NOTICE_TYPE_TAG,
  type NotificationItem,
} from '@/types'

const loading = ref(false)
const tableData = ref<NotificationItem[]>([])
const total = ref(0)

const query = reactive({
  keyword: '',
  pageIndex: 1,
  pageSize: 10,
})

const detailVisible = ref(false)
const current = ref<NotificationItem | null>(null)

const loadData = async () => {
  loading.value = true
  try {
    const res = await getPublishedNotifications({
      ...query,
      keyword: query.keyword || undefined,
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

const openDetail = (item: NotificationItem) => {
  current.value = item
  detailVisible.value = true
}

onMounted(loadData)
</script>

<style scoped>
.notice-board {
  padding: 0;
}
.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.notice-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 8px;
  border-bottom: 1px solid #f0f0f0;
  cursor: pointer;
  border-radius: 4px;
  transition: background 0.2s;
}
.notice-item:hover {
  background: #f5f7fa;
}
.notice-main {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}
.notice-title {
  font-size: 14px;
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.notice-meta {
  font-size: 12px;
  color: #a8abb2;
  flex-shrink: 0;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.detail-meta {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 12px;
}
.meta-text {
  font-size: 12px;
  color: #a8abb2;
}
.detail-content {
  font-size: 14px;
  line-height: 1.8;
  color: #303133;
  white-space: pre-wrap;
  word-break: break-word;
  min-height: 80px;
}
</style>
