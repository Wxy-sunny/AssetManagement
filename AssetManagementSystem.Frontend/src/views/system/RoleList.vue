<template>
  <div class="role-page">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="query" inline @submit.prevent>
        <el-form-item label="关键字">
          <el-input
            v-model="query.keyword"
            placeholder="编码 / 名称"
            clearable
            style="width: 200px"
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
          <el-button :icon="Refresh" @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card shadow="never">
      <div class="toolbar">
        <el-button v-if="canManage" type="primary" :icon="Plus" @click="openDialog()">
          新增角色
        </el-button>
      </div>

      <el-table v-loading="loading" :data="tableData" border stripe>
        <el-table-column prop="roleId" label="角色编码" width="140" show-overflow-tooltip />
        <el-table-column prop="roleName" label="角色名称" width="160" show-overflow-tooltip />
        <el-table-column prop="description" label="描述" min-width="220" show-overflow-tooltip />
        <el-table-column label="用户数" width="90" align="center">
          <template #default="{ row }">
            <el-tag type="info" size="small">{{ row.userCount ?? 0 }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">
              {{ row.isEnabled ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="创建时间" width="180">
          <template #default="{ row }">{{ row.createTime || '-' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="220" fixed="right">
          <template #default="{ row }">
            <el-button
              v-if="canManage"
              link
              type="primary"
              size="small"
              @click="openDialog(row)"
            >
              编辑
            </el-button>
            <el-button
              v-if="canManage"
              link
              type="success"
              size="small"
              @click="openPermission(row)"
            >
              分配权限
            </el-button>
            <el-button
              v-if="canManage"
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
          <el-empty description="暂无角色数据" />
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
      width="520px"
      destroy-on-close
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="角色编码" prop="roleId">
          <el-input v-model="form.roleId" placeholder="如 admin / user" />
        </el-form-item>
        <el-form-item label="角色名称" prop="roleName">
          <el-input v-model="form.roleName" placeholder="如 系统管理员" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="选填" />
        </el-form-item>
        <el-form-item label="状态">
          <el-switch v-model="form.isEnabled" active-text="启用" inactive-text="禁用" />
        </el-form-item>
        <el-form-item label="超级管理员">
          <el-switch v-model="form.isAdmin" active-text="是" inactive-text="否" />
          <div class="form-tip">开启后该角色默认拥有全部功能权限，无需再单独分配</div>
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitForm">确定</el-button>
      </template>
    </el-dialog>

    <!-- 分配权限 -->
    <el-dialog
      v-model="permVisible"
      :title="`分配权限 - ${currentRole?.roleName ?? ''}`"
      width="520px"
      destroy-on-close
      :close-on-click-modal="false"
    >
      <el-alert
        v-if="currentRole?.isAdmin"
        type="warning"
        :closable="false"
        title="该角色为超级管理员，默认拥有全部权限，无需单独分配"
        class="perm-tip"
      />
      <el-tree
        ref="permTreeRef"
        :data="treeData"
        :props="{ label: 'name', children: 'children' }"
        node-key="code"
        show-checkbox
        default-expand-all
      />
      <template #footer>
        <el-button @click="permVisible = false">取消</el-button>
        <el-button type="primary" :loading="permSaving" @click="savePermissions">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Search, Refresh, Plus } from '@element-plus/icons-vue'
import { getRolePage, addRole, updateRole, deleteRole } from '@/api/role'
import { getPermissionTree, getRolePermissions, assignPermissions } from '@/api/permission'
import { useUserStore } from '@/stores/user'
import { PERMISSIONS, type PermissionGroup, type RoleItem } from '@/types'

const loading = ref(false)
const submitting = ref(false)
const tableData = ref<RoleItem[]>([])
const total = ref(0)

// 角色写操作权限（后端同样会校验）
const userStore = useUserStore()
const canManage = computed(() => userStore.hasPermission(PERMISSIONS.RoleManage))

const query = reactive({
  keyword: '',
  pageIndex: 1,
  pageSize: 10,
})

const dialogVisible = ref(false)
const dialogTitle = ref('新增角色')
const formRef = ref<FormInstance>()
const editingId = ref<string | null>(null)

const createDefaultForm = (): RoleItem => ({
  roleId: '',
  roleName: '',
  description: '',
  isEnabled: true,
  isAdmin: false,
})

const form = ref<RoleItem>(createDefaultForm())

const rules: FormRules = {
  roleId: [{ required: true, message: '请输入角色编码', trigger: 'blur' }],
  roleName: [{ required: true, message: '请输入角色名称', trigger: 'blur' }],
}

const loadData = async () => {
  loading.value = true
  try {
    const res = await getRolePage({ ...query, keyword: query.keyword || undefined })
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
  query.pageIndex = 1
  loadData()
}

const openDialog = (row?: RoleItem) => {
  editingId.value = row?.id ?? null
  dialogTitle.value = row ? '编辑角色' : '新增角色'
  form.value = row
    ? { ...createDefaultForm(), ...row, isAdmin: row.isAdmin ?? false }
    : createDefaultForm()
  dialogVisible.value = true
}

const submitForm = async () => {
  if (!formRef.value) return
  await formRef.value.validate()

  submitting.value = true
  try {
    if (editingId.value) {
      await updateRole({ ...form.value, id: editingId.value })
      ElMessage.success('修改成功')
    } else {
      await addRole(form.value)
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

const handleDelete = async (row: RoleItem) => {
  try {
    await ElMessageBox.confirm(`确定删除角色「${row.roleName}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await deleteRole(row.id as string)
    ElMessage.success('删除成功')
    loadData()
  } catch {
    // 忽略
  }
}

// ============ 权限分配 ============
const permVisible = ref(false)
const permSaving = ref(false)
const permTreeRef = ref<any>()
const permGroups = ref<PermissionGroup[]>([])
const currentRole = ref<RoleItem | null>(null)

// 把后端返回的分组数据转换成 el-tree 需要的结构
const treeData = computed(() =>
  permGroups.value.map((g) => ({
    code: g.code,
    name: g.name,
    children: g.permissions.map((p) => ({ code: p.code, name: p.name })),
  })),
)

// 打开权限分配弹窗并回显已有权限
const openPermission = async (row: RoleItem) => {
  currentRole.value = row
  permVisible.value = true

  try {
    permGroups.value = await getPermissionTree()
  } catch {
    permGroups.value = []
  }

  try {
    const codes = await getRolePermissions(row.roleId as string)
    await nextTick() // 等树渲染完成后再设置选中
    permTreeRef.value?.setCheckedKeys(codes ?? [], false)
  } catch {
    // 失败时拦截器已提示
  }
}

const savePermissions = async () => {
  const roleId = currentRole.value?.roleId
  if (!roleId) return

  // leafOnly = true：只取叶子节点（权限项），排除模块分组节点
  const codes = (permTreeRef.value?.getCheckedKeys(true) ?? []) as string[]

  permSaving.value = true
  try {
    await assignPermissions(roleId, codes)
    ElMessage.success(`权限分配成功，共 ${codes.length} 项`)
    permVisible.value = false
  } catch {
    // 失败时拦截器已提示
  } finally {
    permSaving.value = false
  }
}

onMounted(loadData)
</script>

<style scoped>
.role-page {
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
.form-tip {
  font-size: 12px;
  color: #a8abb2;
  line-height: 1.4;
  margin-top: 4px;
}
.perm-tip {
  margin-bottom: 12px;
}
</style>
