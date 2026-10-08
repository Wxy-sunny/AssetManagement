<template>
  <div class="user-page">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :model="query" inline @submit.prevent>
        <el-form-item label="关键字">
          <el-input
            v-model="query.keyword"
            placeholder="账号 / 姓名 / 手机号"
            clearable
            style="width: 200px"
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item label="角色">
          <el-select v-model="query.roleId" placeholder="全部角色" clearable style="width: 150px">
            <el-option v-for="r in roles" :key="r.id" :label="r.roleName" :value="r.roleId" />
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

    <el-card shadow="never">
      <div class="toolbar">
        <el-button v-if="canManage" type="primary" :icon="Plus" @click="openDialog()">
          新增用户
        </el-button>
      </div>

      <el-table v-loading="loading" :data="tableData" border stripe>
        <el-table-column prop="userId" label="登录账号" width="130" show-overflow-tooltip />
        <el-table-column prop="userName" label="姓名" width="110" show-overflow-tooltip />
        <el-table-column prop="nickName" label="昵称" width="110" show-overflow-tooltip />
        <el-table-column prop="roleName" label="角色" width="120" show-overflow-tooltip />
        <el-table-column prop="deptName" label="部门" width="130" show-overflow-tooltip />
        <el-table-column prop="mobile" label="手机号" width="130" />
        <el-table-column prop="email" label="邮箱" min-width="180" show-overflow-tooltip />
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">
              {{ row.isEnabled ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="最后登录" width="170">
          <template #default="{ row }">{{ row.lastLoginTime || '-' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="240" fixed="right">
          <template #default="{ row }">
            <template v-if="canManage">
              <el-button link type="primary" size="small" @click="openDialog(row)">编辑</el-button>
              <el-button link type="warning" size="small" @click="handleResetPwd(row)">
                重置密码
              </el-button>
              <el-button link type="info" size="small" @click="handleToggle(row)">
                {{ row.isEnabled ? '禁用' : '启用' }}
              </el-button>
              <el-button link type="danger" size="small" @click="handleDelete(row)">删除</el-button>
            </template>
            <span v-else class="no-perm">无操作权限</span>
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="暂无用户数据" />
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
      width="620px"
      destroy-on-close
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="登录账号" prop="userId">
              <el-input v-model="form.userId" placeholder="登录账号" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="姓名" prop="userName">
              <el-input v-model="form.userName" placeholder="真实姓名" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="昵称">
              <el-input v-model="form.nickName" placeholder="选填" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="密码" prop="password">
              <el-input
                v-model="form.password"
                type="password"
                show-password
                :placeholder="editingId ? '留空表示不修改' : '请输入密码'"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="手机号">
              <el-input v-model="form.mobile" placeholder="选填" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="邮箱">
              <el-input v-model="form.email" placeholder="选填" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="角色">
              <el-select v-model="form.roleId" placeholder="请选择角色" clearable style="width: 100%">
                <el-option v-for="r in roles" :key="r.id" :label="r.roleName" :value="r.roleId" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="部门">
              <el-select v-model="form.deptId" placeholder="请选择部门" clearable style="width: 100%">
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
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Search, Refresh, Plus } from '@element-plus/icons-vue'
import { useUserStore } from '@/stores/user'
import {
  getUserPage,
  addUser,
  updateUser,
  deleteUser,
  updateUserEnabled,
  resetUserPassword,
} from '@/api/user'
import { getRoleList } from '@/api/role'
import { getDeptList } from '@/api/department'
import { PERMISSIONS, type UserItem, type UserQuery, type RoleItem, type DepartmentItem } from '@/types'

const loading = ref(false)
const submitting = ref(false)
const tableData = ref<UserItem[]>([])
const total = ref(0)
const roles = ref<RoleItem[]>([])
const depts = ref<DepartmentItem[]>([])

// 用户写操作权限（后端同样会校验）
const userStore = useUserStore()
const canManage = computed(() => userStore.hasPermission(PERMISSIONS.UserManage))

const query = reactive<UserQuery>({
  keyword: '',
  roleId: undefined,
  deptId: undefined,
  pageIndex: 1,
  pageSize: 10,
})

const dialogVisible = ref(false)
const dialogTitle = ref('新增用户')
const formRef = ref<FormInstance>()
const editingId = ref<string | null>(null)

const createDefaultForm = (): UserItem => ({
  userId: '',
  userName: '',
  nickName: '',
  password: '',
  email: '',
  mobile: '',
  roleId: null,
  deptId: null,
  isEnabled: true,
})

const form = ref<UserItem>(createDefaultForm())

// 编辑时密码可留空（表示不修改）
const rules = computed<FormRules>(() => ({
  userId: [{ required: true, message: '请输入登录账号', trigger: 'blur' }],
  userName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  password: editingId.value
    ? []
    : [{ required: true, message: '请输入密码', trigger: 'blur' }],
}))

const loadData = async () => {
  loading.value = true
  try {
    const res = await getUserPage({
      ...query,
      keyword: query.keyword || undefined,
      roleId: query.roleId || undefined,
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

const loadOptions = async () => {
  try {
    roles.value = await getRoleList()
  } catch {
    roles.value = []
  }
  try {
    depts.value = await getDeptList()
  } catch {
    depts.value = []
  }
}

const handleSearch = () => {
  query.pageIndex = 1
  loadData()
}

const handleReset = () => {
  query.keyword = ''
  query.roleId = undefined
  query.deptId = undefined
  query.pageIndex = 1
  loadData()
}

const openDialog = (row?: UserItem) => {
  editingId.value = row?.id ?? null
  dialogTitle.value = row ? '编辑用户' : '新增用户'
  form.value = row ? { ...createDefaultForm(), ...row, password: '' } : createDefaultForm()
  dialogVisible.value = true
}

const submitForm = async () => {
  if (!formRef.value) return
  await formRef.value.validate()

  submitting.value = true
  try {
    const payload: UserItem = { ...form.value }
    // 编辑且密码为空时，移除该字段，后端保持原密码不变
    if (editingId.value && !payload.password) {
      delete payload.password
    }
    if (editingId.value) {
      await updateUser({ ...payload, id: editingId.value })
      ElMessage.success('修改成功')
    } else {
      await addUser(payload)
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

const handleToggle = async (row: UserItem) => {
  const next = !row.isEnabled
  try {
    await updateUserEnabled(row.id as string, next)
    ElMessage.success(next ? '已启用' : '已禁用')
    loadData()
  } catch {
    // 忽略
  }
}

const handleResetPwd = async (row: UserItem) => {
  try {
    const { value } = await ElMessageBox.prompt(
      `请输入「${row.userName || row.userId}」的新密码`,
      '重置密码',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        inputPlaceholder: '请输入新密码',
        inputValidator: (v: string) => (v && v.trim().length > 0 ? true : '密码不能为空'),
      },
    )
    await resetUserPassword(row.id as string, value.trim())
    ElMessage.success('密码重置成功')
  } catch {
    // 取消或失败均忽略
  }
}

const handleDelete = async (row: UserItem) => {
  try {
    await ElMessageBox.confirm(`确定删除用户「${row.userName || row.userId}」吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await deleteUser(row.id as string)
    ElMessage.success('删除成功')
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
.user-page {
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
.no-perm {
  font-size: 12px;
  color: #c0c4cc;
}
</style>
