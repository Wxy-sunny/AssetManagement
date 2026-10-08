<template>
  <div class="file-page">
    <!-- 上传区 -->
    <el-card v-if="canUpload" shadow="never" class="upload-card">
      <template #header><span>上传资料</span></template>
      <el-form :model="uploadMeta" inline class="upload-meta">
        <el-form-item label="业务类型">
          <el-select v-model="uploadMeta.bizType" style="width: 150px">
            <el-option
              v-for="(label, code) in FILE_BIZ_TYPE_MAP"
              :key="code"
              :label="label"
              :value="code"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="关联资产">
          <el-select
            v-model="uploadMeta.assetId"
            placeholder="可不选"
            clearable
            filterable
            style="width: 240px"
          >
            <el-option
              v-for="a in assets"
              :key="a.id"
              :label="`${a.assetCode} ${a.assetName}`"
              :value="a.id"
            />
          </el-select>
        </el-form-item>
      </el-form>

      <el-upload
        class="upload-dragger"
        drag
        multiple
        :show-file-list="false"
        :http-request="customUpload"
        :before-upload="beforeUpload"
      >
        <el-icon class="upload-icon"><UploadFilled /></el-icon>
        <div class="el-upload__text">将文件拖到此处，或<em>点击上传</em></div>
        <template #tip>
          <div class="el-upload__tip">
            支持图片、PDF、Office 文档、压缩包等，单个文件不超过 20MB
          </div>
        </template>
      </el-upload>
    </el-card>

    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="query" inline @submit.prevent>
        <el-form-item label="关键字">
          <el-input
            v-model="query.keyword"
            placeholder="文件名"
            clearable
            style="width: 200px"
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item label="业务类型">
          <el-select v-model="query.bizType" placeholder="全部类型" clearable style="width: 150px">
            <el-option
              v-for="(label, code) in FILE_BIZ_TYPE_MAP"
              :key="code"
              :label="label"
              :value="code"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="关联资产">
          <el-select
            v-model="query.assetId"
            placeholder="全部资产"
            clearable
            filterable
            style="width: 240px"
          >
            <el-option
              v-for="a in assets"
              :key="a.id"
              :label="`${a.assetCode} ${a.assetName}`"
              :value="a.id"
            />
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
        <el-button
          v-if="canDelete"
          type="danger"
          :icon="Delete"
          :disabled="selectedIds.length === 0"
          @click="handleBatchDelete"
        >
          批量删除
        </el-button>
        <el-button :icon="RefreshRight" @click="loadData">刷新</el-button>
      </div>

      <el-table
        v-loading="loading"
        :data="tableData"
        border
        stripe
        @selection-change="onSelectionChange"
      >
        <el-table-column type="selection" width="46" />
        <el-table-column label="预览" width="80" align="center">
          <template #default="{ row }">
            <el-image
              v-if="isImage(row.fileExt)"
              :src="row.filePath"
              :preview-src-list="[row.filePath]"
              fit="cover"
              class="thumb"
              preview-teleported
            />
            <el-icon v-else class="file-icon"><Document /></el-icon>
          </template>
        </el-table-column>
        <el-table-column prop="fileName" label="文件名" min-width="220" show-overflow-tooltip />
        <el-table-column prop="fileSizeText" label="大小" width="100" align="right" />
        <el-table-column label="业务类型" width="110">
          <template #default="{ row }">
            {{ FILE_BIZ_TYPE_MAP[row.bizType] || row.bizType || '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="assetName" label="关联资产" width="160" show-overflow-tooltip>
          <template #default="{ row }">{{ row.assetName || '-' }}</template>
        </el-table-column>
        <el-table-column prop="uploadUserName" label="上传者" width="110" />
        <el-table-column label="上传时间" width="170">
          <template #default="{ row }">{{ row.createTime || '-' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="150" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="handleDownload(row)">
              下载
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
          <el-empty description="暂无资料" />
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
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Refresh, RefreshRight, Delete, UploadFilled, Document } from '@element-plus/icons-vue'
import { getFilePage, uploadFile, downloadFile, deleteFile, batchDeleteFile } from '@/api/file'
import { getAssetList } from '@/api/asset'
import { useUserStore } from '@/stores/user'
import {
  FILE_BIZ_TYPE_MAP,
  PERMISSIONS,
  type FileItem,
  type FileQuery,
  type AssetItem,
} from '@/types'

// 资料操作权限（后端同样会校验）
const userStore = useUserStore()
const canUpload = computed(() => userStore.hasPermission(PERMISSIONS.FileUpload))
const canDelete = computed(() => userStore.hasPermission(PERMISSIONS.FileDelete))

const IMAGE_EXTS = ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.svg']

const loading = ref(false)
const tableData = ref<FileItem[]>([])
const total = ref(0)
const selectedIds = ref<string[]>([])
const assets = ref<AssetItem[]>([])

const query = reactive<FileQuery>({
  keyword: '',
  bizType: undefined,
  assetId: undefined,
  pageIndex: 1,
  pageSize: 10,
})

// 上传时的附加信息
const uploadMeta = reactive({
  bizType: 'common',
  assetId: undefined as string | undefined,
})

const isImage = (ext?: string | null) => !!ext && IMAGE_EXTS.includes(ext.toLowerCase())

const loadData = async () => {
  loading.value = true
  try {
    const res = await getFilePage({
      ...query,
      keyword: query.keyword || undefined,
      bizType: query.bizType || undefined,
      assetId: query.assetId || undefined,
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

const loadAssets = async () => {
  try {
    assets.value = await getAssetList()
  } catch {
    assets.value = []
  }
}

const handleSearch = () => {
  query.pageIndex = 1
  loadData()
}

const handleReset = () => {
  query.keyword = ''
  query.bizType = undefined
  query.assetId = undefined
  query.pageIndex = 1
  loadData()
}

const onSelectionChange = (rows: FileItem[]) => {
  selectedIds.value = rows.map((r) => r.id).filter(Boolean) as string[]
}

// 上传前校验（大小与类型由后端兜底，这里做前端提示）
const beforeUpload = (file: File) => {
  const maxSize = 20 * 1024 * 1024
  if (file.size > maxSize) {
    ElMessage.error(`文件 ${file.name} 超过 20MB 上限`)
    return false
  }
  return true
}

// 自定义上传：走封装的 request，自动携带 Authorization
const customUpload = async (options: any) => {
  const { file, onSuccess, onError } = options
  try {
    await uploadFile(file, uploadMeta.bizType, uploadMeta.assetId)
    onSuccess?.({})
    ElMessage.success(`「${file.name}」上传成功`)
    loadData()
  } catch (err) {
    onError?.(err)
  }
}

const handleDownload = async (row: FileItem) => {
  try {
    await downloadFile(row.id as string, row.fileName ?? undefined)
  } catch {
    // 拦截器已提示
  }
}

const handleDelete = async (row: FileItem) => {
  try {
    await ElMessageBox.confirm(`确定删除文件「${row.fileName}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await deleteFile(row.id as string)
    ElMessage.success('删除成功')
    loadData()
  } catch {
    // 忽略
  }
}

const handleBatchDelete = async () => {
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedIds.value.length} 个文件吗？`, '批量删除', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await batchDeleteFile(selectedIds.value)
    ElMessage.success('删除成功')
    loadData()
  } catch {
    // 忽略
  }
}

onMounted(() => {
  loadAssets()
  loadData()
})
</script>

<style scoped>
.file-page {
  padding: 0;
}
.upload-card {
  margin-bottom: 16px;
}
.upload-meta {
  margin-bottom: 4px;
}
.upload-dragger {
  width: 100%;
}
.upload-icon {
  font-size: 48px;
  color: #c0c4cc;
}
.thumb {
  width: 44px;
  height: 44px;
  border-radius: 4px;
  vertical-align: middle;
}
.file-icon {
  font-size: 26px;
  color: #909399;
}
.search-card {
  margin-bottom: 16px;
}
.toolbar {
  display: flex;
  gap: 10px;
  margin-bottom: 16px;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
