# 资产管理系统 (Asset Management System)

一套前后端分离的企业资产全生命周期管理平台：从资产登记、领用 / 归还 / 维修 / 报废闭环，到盘点、报表导出与数据仪表盘，每次状态变更全程留痕。

- **后端**：ASP.NET Core Web API（**.NET 10 LTS**），SqlSugar ORM（**SQLite / SQL Server** 双支持，CodeFirst 自动建表），JWT 鉴权（含无感刷新），Swagger 文档，Redis 可选（验证码存储，不可用时自动降级为内存缓存）。
- **前端**：Vue 3 + TypeScript + Vite 8 + Element Plus，Pinia 状态管理，Vue Router，**ECharts 6** 图表，Axios。
- **部署**：Docker Compose 一键编排（backend / frontend / redis，SQL Server 为可选容器），详见 [`DEPLOY.md`](DEPLOY.md)。

## 功能模块

### 资产全生命周期闭环

- **状态机白名单**：在用 / 闲置 / 维修中 / 已报废，已报废为终态，禁止越级跃迁（如报废资产不可再领用）
- **5 类流转单据**：领用、归还、报修、维修完成、报废；资产状态与单据在**同一事务**内同步落库，杜绝只改状态不留痕
- **闭环处理**：报修单可完成（恢复在用 / 转闲置）、取消、驳回（资产状态自动回退），并补齐 `repair → in_use` 流水
- **入口**：资产列表操作列「业务办理」直接发起；`/flows` 流转单据页集中查询、完成维修、查看单据详情

### 盘点与报表导出

- **盘点单**：按部门或全量生成账面快照明细，逐条录入实盘状态 / 位置 / 使用人，自动判定账实差异
- **差异汇总**：盘点进度、差异与盘亏条数、准确率、差异资产原值、实盘状态分布
- **差异闭环**：差异明细一键生成报废 / 报修单据，与流转模块打通
- **报表中心**：资产台账、分类汇总、状态汇总、部门资产分布、折旧汇总、流转记录、盘点差异共 7 类
- **导出**：Excel（`.xlsx`，最小 OOXML 手工打包，无第三方依赖）与 CSV（UTF-8 BOM，中文不乱码）；服务端**内存生成 + 流式下载**，不落盘，容器内无需可写共享目录

### 数据仪表盘

- 首页概览 + 独立「数据仪表盘」页（`/dashboard/analytics`）
- 利用率仪表盘、状态环图、分类 Top10 双轴条形、近 12 个月趋势、部门 Top10、折旧方式玫瑰图、流转单据统计、运营健康度
- 全部统计走 SQL `COUNT / SUM / GROUP BY` 聚合，不整表加载；配色跟随 4 套主题，容器尺寸变化通过 `ResizeObserver` 自适配

### 基础能力

- 用户登录 / JWT 鉴权（账号密码登录强制图形验证码；手机号验证码登录/注册）
- Token 无感刷新（refresh token 轮换 + 退出登录作废，访问令牌过期不掉线）
- 登录防爆破（账号连续失败锁定；可选 IP 限流）
- 资产管理（增删改查、资料附件上传、折旧重算）
- 资产分类树（支持关键字检索）
- 部门管理
- 角色与权限管理（细粒度接口级权限）
- 通知公告（管理员发布）+ 个人消息（联系请求处理回执，未读角标）
- 联系管理员（登录页公开提交重置密码等申请，管理员处理并回执）
- 手机端自适应（≤768px：抽屉菜单、表单纵向堆叠、表格横向滚动、对话框全宽）
- 主题风格切换（默认蓝 / 暗黑模式 / 清新绿 / 优雅紫，选择自动持久化，刷新不丢失）
- 系统图标（自定义 Logo 应用于登录页/侧边栏/移动抽屉，favicon 用于浏览器标签页）
- 登录页布局偏好（左 / 中 / 右可选；匿名存本机，登录后同步到用户配置表 `T_USER_PREFERENCE`，跨设备跟随）
- 系统介绍页（`/intro`，未登录可访问，登录页 Logo 与「系统介绍」链接可跳转）

## 主要页面

| 路径 | 页面 | 所需权限 |
| --- | --- | --- |
| `/dashboard` | 首页概览 | `dashboard:view` |
| `/dashboard/analytics` | 数据仪表盘 | `dashboard:overview` |
| `/assets` | 资产列表（含业务办理） | `asset:list` |
| `/flows` | 流转单据 | `flow:list` |
| `/inventories` | 资产盘点 | `inventory:list` |
| `/reports` | 报表中心 | `report:view` |
| `/intro` | 系统介绍（公开） | 无 |

> **升级提示**：新增了 7 项权限（`dashboard:overview`、`flow:list`、`flow:manage`、`inventory:list`、`inventory:manage`、`report:view`、`report:export`）。超级管理员默认拥有全部权限；**非超管角色需在「角色管理 → 权限分配」中重新勾选**，否则看不到新菜单或无法办理业务。

## 目录结构

```
AssetManagement/
├── AssetManagementSystem/              # 后端解决方案
│   ├── AssetManagementSystem.sln
│   ├── AssetManagementSystem/          # ASP.NET Core Web API
│   │   ├── Controllers/                # Asset / AssetFlow / Inventory / Report / Dashboard 等
│   │   ├── Models/                     # 实体（AssetFlow、AssetInventory、AssetInventoryItem…）
│   │   ├── Dtos/                       # 入参 / 出参 DTO
│   │   ├── Services/
│   │   │   ├── AssetStatusMachine.cs   # 状态跃迁白名单
│   │   │   ├── AssetFlowService.cs     # 流转事务（状态 + 单据同步落库）
│   │   │   ├── ReportExportService.cs  # CSV / XLSX 内存生成
│   │   │   └── Sms/                   # 短信通道（none/log/aliyun/webhook）
│   │   ├── Utils/                      # 权限常量、DbSchemaPatcher（结构补齐）
│   │   ├── Middlewares/                # 全局异常与请求日志
│   │   └── appsettings.json
│   └── AssetManagementSystem.Tests/    # xUnit 单元 / 集成测试
├── AssetManagementSystem.Frontend/     # Vue 3 前端
│   └── src/
│       ├── views/
│       │   ├── asset/                  # AssetList.vue、AssetFlowList.vue
│       │   ├── inventory/              # InventoryList.vue
│       │   ├── report/                 # ReportCenter.vue
│       │   ├── dashboard/              # DashboardAnalytics.vue
│       │   └── Intro.vue               # 系统介绍页（公开）
│       ├── api/                        # 按模块拆分的请求封装
│       ├── types/                      # TS 类型与字典常量
│       ├── components/EChart.vue       # ECharts 封装（含 ResizeObserver）
│       └── router/index.ts
├── docker-compose.yml                  # 一键编排
├── DEPLOY.md                           # 部署与运维手册
└── README.md
```

## 环境要求

| 依赖 | 版本 / 说明 |
| --- | --- |
| .NET SDK | **10.0**（LTS，2028-11 前受支持） |
| Node.js | `^22.18.0` 或 `>=24.12.0`（`package.json` engines；Node 20.19.5 实测亦可构建，npm 会给出版本警告） |
| 数据库 | **SQLite**（默认，单文件，2GB 内存小机友好）/ SQL Server 2019+（可选，需 ≥4GB 内存） |
| Redis | 可选（未配置时验证码自动降级为内存缓存） |
| Docker | 可选，用于 Compose 一键部署 |

## 快速开始

### 方式 A：Docker Compose（推荐）

```bash
cp .env.example .env      # 按注释填写 Jwt__SecretKey、AdminSeed__Password 等
docker compose up -d --build
```

前端映射宿主机 `80` 端口，访问 `http://<服务器IP>/`；SQLite 数据落在 `asset-data` 卷，容器重建不丢。完整说明（SQL Server 分支、更新流程、排障）见 [`DEPLOY.md`](DEPLOY.md)。

### 方式 B：本地开发

#### 1. 后端

```bash
cd AssetManagementSystem/AssetManagementSystem
dotnet restore
dotnet run
```

- 默认 HTTP 端口 `5099`（HTTPS `7205`），Swagger 文档：`http://localhost:5099/swagger`。
- 首次启动若 `AdminSeed:Enabled=true` 且未配置密码，会自动创建系统管理员账号并将随机生成的密码打印到控制台日志（详见下方"默认管理员"）。

**免装数据库：用 SQLite 本地跑**（Windows PowerShell 示例）：

```powershell
$env:SqlSugarConfig__DbType = "sqlite"
$env:ConnectionStrings__AssetConnection = "DataSource=D:\data\asset.db"
$env:DatabaseInitialization__Enabled = "true"
$env:DatabaseInitialization__CreateTablesOnStartup = "true"
$env:DemoData__Enabled = "true"          # 可选：生成 46 条演示资产 / 分类 / 部门 / 用户
$env:AdminSeed__Password = "YourStrongPwd"
dotnet run
```

> **表结构补齐**：`DbSchemaPatcher` 会在每次启动时自动补齐新增列，并用 CodeFirst 创建流转 / 盘点等**新表**，该过程不受 `DatabaseInitialization:Enabled` 开关影响；开关只控制「全量建表 + 播种管理员」。首次部署可临时设为 `true`，完成后改回 `false`。

> 本地开发连接串在 `appsettings.Development.json`。全新克隆后若本地无此文件，可手动创建：
>
> ```json
> {
>   "ConnectionStrings": {
>     "AssetConnection": "Server=localhost;Database=AssetDB;User Id=demo;Password=123456789;TrustServerCertificate=true;"
>   }
> }
> ```
>
> 使用 SQLite 时改填 `"AssetConnection": "DataSource=D:\\data\\asset.db"`，并把 `SqlSugarConfig:DbType` 设为 `sqlite`。

#### 2. 前端

```bash
cd AssetManagementSystem.Frontend
npm install
npm run dev      # 开发服务器 http://localhost:5173
```

开发模式下，`vite.config.ts` 已将 `/api` 与 `/uploads` 代理到后端 `http://localhost:5099`，无需额外配置跨域即可联调。

生产构建（含类型检查）：

```bash
npm run build    # 类型检查 + 打包到 dist/
npm run preview  # 本地预览构建产物
```

## 默认管理员

`appsettings.json` 中 `AdminSeed` 用于初始化系统管理员：

- 若 `AdminSeed:Password` 留空，首次初始化会**自动生成随机强密码**并打印到启动日志（`SeedAdmin` 仅在账号不存在时创建，不会覆盖已有密码）。
- 登录账号 `admin`，密码见启动日志。登录后请立即在「个人资料 → 修改密码」中更换。
- 生产环境建议通过环境变量 `AdminSeed__Password` 指定固定强密码。

## 配置说明

生产环境**不要**在 `appsettings.json` 中提交真实密码，请通过环境变量（ASP.NET Core 的双下划线 `__` 约定）覆盖：

| 配置项 | 环境变量 | 说明 |
| --- | --- | --- |
| `SqlSugarConfig:DbType` | `SqlSugarConfig__DbType` | 数据库类型：`sqlite`（默认，省内存）/ `sqlserver` |
| `ConnectionStrings:AssetConnection` | `ConnectionStrings__AssetConnection` | 连接串；SQLite 用 `DataSource=/app/data/asset.db` |
| `Jwt:SecretKey` | `Jwt__SecretKey` | JWT 签名密钥，生产环境必须设置（≥32 字节，建议随机） |
| `AdminSeed:Password` | `AdminSeed__Password` | 系统管理员初始密码 |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins` | 允许跨域的前端来源（逗号分隔） |
| `DatabaseInitialization:Enabled` | `DatabaseInitialization__Enabled` | 全量建表 / 播种开关；表结构稳定后可设 `false`（新增列与新表由 `DbSchemaPatcher` 自动补齐） |
| `DatabaseInitialization:CreateTablesOnStartup` | `DatabaseInitialization__CreateTablesOnStartup` | 与上一项配合，首次部署设 `true` |
| `DemoData:Enabled` | `DemoData__Enabled` | 演示数据开关，生产环境设 `false` |
| `Sms:Enabled` / `Sms:Provider` | `Sms__Enabled` / `Sms__Provider` | 短信开关与通道：`none` / `log` / `aliyun` / `webhook`（默认不启用，见 `DEPLOY.md` 第 11 节） |
| `Sms:ReturnCodeInResponse` | `Sms__ReturnCodeInResponse` | 验证码回传前端（仅本地联调），生产环境设 `false` |
| `Jwt:RefreshExpireDays` | `Jwt__RefreshExpireDays` | 刷新令牌有效期（天，默认 7） |
| `LoginGuard:MaxFailures` | `LoginGuard__MaxFailures` | 账号登录失败锁定阈值（默认 5 次） |
| `LoginGuard:LockMinutes` | `LoginGuard__LockMinutes` | 锁定时长（分钟，默认 15） |
| `LoginGuard:IpLimitEnabled` | `LoginGuard__IpLimitEnabled` | IP 限流开关（默认 `false`；反向代理部署需先配置 ForwardedHeaders 再开启） |

> 说明：若 `Jwt:SecretKey` 为空，启动时会生成**一次性随机密钥**（重启后旧 token 失效），仅适合本地开发，并会打印警告日志。

## 生产部署注意事项

1. 通过环境变量注入 `ConnectionStrings__AssetConnection`、`Jwt__SecretKey`、`AdminSeed__Password`，切勿提交明文凭据。
2. 将 `DemoData`、`Sms:ReturnCodeInResponse` 设为 `false`；`DatabaseInitialization` 在表结构稳定后可设 `false`——升级带来的新表 / 新列由 `DbSchemaPatcher` 自动补齐，无需手动执行建表脚本。
3. 通过 `Cors:AllowedOrigins` 限定实际前端域名。
4. 后端不强制 HTTPS 重定向（`UseHttpsRedirection` 默认关闭）：Compose 部署时前端 nginx 映射宿主机 `80` 纯 HTTP，公网 HTTPS 请在宿主机外层做 TLS 终止（见 `DEPLOY.md`）。
5. 日志默认输出到控制台（`Information` 级别），生产可按需接入文件/集中式日志。

## 日志

- 使用 Serilog 双写：控制台 + 文件。
- 文件位置：`logs/asset-api-YYYYMMDD.log`（相对于后端运行目录），按天滚动、单文件 50MB 上限、最多保留 31 天。
- 访问日志（每次请求的方法/路径/状态码/耗时）由 `ApiPipelineMiddleware` 记录；未处理异常记录完整堆栈。
- 建议将 `logs/` 接入集中式日志（ELK / Loki 等）收集，并按磁盘容量调整保留策略。

## 备份与恢复

备份范围：**数据库**（资产/用户/权限/流转单据/盘点/消息等全部数据）+ **附件目录**（`wwwroot/uploads`，资料管理的上传文件）。日志目录无需备份。

- **SQLite**（默认）：备份单文件即可。宿主机直跑备份 `asset.db`；Compose 部署备份 `asset-data` 卷（容器内路径 `/app/data/asset.db`），例如 `docker run --rm -v asset-data:/data -v $PWD:/backup alpine tar czf /backup/asset-data.tgz /data`。
- **SQL Server**：使用下方 `.bak` 备份脚本。

```bash
# 全量备份（数据库 .bak + 附件 zip），保留最近 14 天
scripts/backup/Backup-AssetSystem.ps1 -BackupRoot D:\Backups\AssetManagement -Compress

# 从指定备份目录还原（数据库 + 附件）
scripts/backup/Restore-AssetSystem.ps1 -BackupFolder D:\Backups\AssetManagement\20261022-020000 -RestoreUploads
```

建议通过 Windows 计划任务每天凌晨执行（备份期间数据库仅有秒级 IO 压力，不影响使用）：

```bat
schtasks /Create /TN "AssetSystem-Backup" /SC DAILY /ST 02:00 ^
  /TR "powershell -ExecutionPolicy Bypass -File D:\Backups\Backup-AssetSystem.ps1 -BackupRoot D:\Backups\AssetManagement -Compress"
```

注意：

- 执行机需安装 `sqlcmd`（随 SQL Server / SSMS 提供），且备份账号需有数据库 `BACKUP` 权限。
- 备份文件应存放在与数据库服务器**不同的磁盘/机器**上（防单点故障），重要环境建议再加异地副本。
- 首次部署完成、正式启用前，建议立即做一次"备份 → 还原"演练，确认流程可用。

## 构建状态

- 后端：`dotnet build` 通过（0 错误）。
- 前端：`npm run build`（`vue-tsc` 类型检查 + `vite build`）通过。
- 测试：`dotnet test` 64 个用例全部通过。

## 自动化测试

```bash
cd AssetManagementSystem
dotnet test
```

测试分两层：

- **单元测试**（不依赖数据库）：密码哈希（BCrypt）、图形验证码（一次性/大小写）、SVG 安全清洗、登录防爆破锁定逻辑、分页参数守卫、**资产状态机跃迁白名单**（防止报废资产被重新领用等闭环回归）、权限定义一致性（防止新增权限漏配 `All` / 分组）。
- **集成测试**（`WebApplicationFactory`，免数据库）：验证码接口、无验证码登录被拒、未登录访问受保护接口返回 401、刷新令牌入参校验等——这些用例都刻意停在访问数据库之前的校验环节，CI 无需数据库即可运行。
