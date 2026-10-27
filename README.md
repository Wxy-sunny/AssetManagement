# 资产管理系统 (Asset Management System)

一套前后端分离的企业资产全生命周期管理平台，覆盖资产台账、分类树、部门、角色权限、通知与数据仪表盘。

- **后端**：ASP.NET Core Web API（.NET 8），基于 SqlSugar ORM 操作 SQL Server，JWT 鉴权，Swagger 文档，Redis 可选（验证码存储，不可用时自动降级为内存缓存）。
- **前端**：Vue 3 + TypeScript + Vite，Element Plus 组件库，Pinia 状态管理，Vue Router，ECharts 图表，Axios 请求。

## 功能模块

- 用户登录 / JWT 鉴权（账号密码登录强制图形验证码；手机号验证码登录/注册）
- Token 无感刷新（refresh token 轮换 + 退出登录作废，访问令牌过期不掉线）
- 登录防爆破（账号连续失败锁定；可选 IP 限流）
- 资产管理（增删改查、资料附件上传、折旧重算）
- 资产分类树（支持关键字检索）
- 部门管理
- 角色与权限管理（细粒度接口级权限）
- 通知公告（管理员发布）+ 个人消息（联系请求处理回执，未读角标）
- 联系管理员（登录页公开提交重置密码等申请，管理员处理并回执）
- 数据仪表盘（ECharts 可视化，配色跟随主题）
- 手机端自适应（≤768px：抽屉菜单、表单纵向堆叠、表格横向滚动、对话框全宽）
- 主题风格切换（默认蓝 / 暗黑模式 / 清新绿 / 优雅紫，选择自动持久化，刷新不丢失）
- 系统图标（自定义 Logo 应用于登录页/侧边栏/移动抽屉，favicon 用于浏览器标签页）
- 登录页布局偏好（左 / 中 / 右可选；匿名存本机，登录后同步到用户配置表 `T_USER_PREFERENCE`，跨设备跟随）

## 目录结构

```
AssetManagement/
├── AssetManagementSystem/        # 后端解决方案与项目
│   ├── AssetManagementSystem.sln
│   └── AssetManagementSystem/    # ASP.NET Core Web API 项目
│       ├── Controllers/          # 接口
│       ├── Models/               # 实体与 DTO
│       ├── Services/             # 业务服务（含 SmsCodeService、DataSeeder）
│       ├── Utils/                # 权限常量、Schema 补齐等
│       ├── Middlewares/          # 全局异常与请求日志
│       └── appsettings.json      # 配置
└── AssetManagementSystem.Frontend/  # Vue 3 前端
    ├── src/
    ├── vite.config.ts            # 含 /api 代理到后端 5099
    └── package.json
```

## 环境要求

| 依赖 | 版本 |
| --- | --- |
| .NET SDK | 8.0 |
| Node.js | `^22.18.0` 或 `>=24.12.0`（Vite 8 需要 Node 22+） |
| SQL Server | 本地开发可用，生产建议使用独立实例 |
| Redis | 可选（未配置时验证码自动降级为内存缓存） |

## 快速开始

### 1. 后端

```bash
cd AssetManagementSystem/AssetManagementSystem
dotnet restore
dotnet run
```

- 默认 HTTP 端口 `5099`（HTTPS `7205`）。
- 启动后访问 Swagger 文档：`http://localhost:5099/swagger`。
- 首次启动若 `AdminSeed:Enabled=true` 且未配置密码，会自动创建系统管理员账号并将随机生成的密码打印到控制台日志（详见下方"默认管理员"）。

> 数据库表结构由 `DatabaseInitialization` 控制。**首次部署**可临时将该配置 `Enabled` 设为 `true` 以初始化表结构，完成后请改回 `false`。

> 本地开发连接串在 `appsettings.Development.json`（该文件被 `.gitignore` 忽略，不会入库）。若全新克隆后本地无此文件，请手动创建并填入本地数据库连接串：
>
> ```json
> {
>   "ConnectionStrings": {
>     "AssetConnection": "Server=localhost;Database=AssetDB;User Id=demo;Password=123456789;TrustServerCertificate=true;"
>   }
> }
> ```

### 2. 前端

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
- 登录地址：`admin` / 日志中的随机密码。登录后请立即在"角色权限"中修改密码。
- 生产环境建议通过环境变量 `AdminSeed__Password` 指定固定强密码。

## 配置说明

生产环境**不要**在 `appsettings.json` 中提交真实密码，请通过环境变量（ASP.NET Core 的双下划线 `__` 约定）覆盖：

| 配置项 | 环境变量 | 说明 |
| --- | --- | --- |
| `ConnectionStrings:AssetConnection` | `ConnectionStrings__AssetConnection` | SQL Server 连接字符串 |
| `Jwt:SecretKey` | `Jwt__SecretKey` | JWT 签名密钥，生产环境必须设置（≥32 字节，建议随机） |
| `AdminSeed:Password` | `AdminSeed__Password` | 系统管理员初始密码 |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins` | 允许跨域的前端来源（逗号分隔） |
| `DatabaseInitialization:Enabled` | `DatabaseInitialization__Enabled` | 启动建表/结构补齐开关，生产环境设 `false` |
| `DemoData:Enabled` | `DemoData__Enabled` | 演示数据开关，生产环境设 `false` |
| `Sms:ReturnCodeInResponse` | `Sms__ReturnCodeInResponse` | 验证码回传前端（仅本地联调），生产环境设 `false` |
| `Jwt:RefreshExpireDays` | `Jwt__RefreshExpireDays` | 刷新令牌有效期（天，默认 7） |
| `LoginGuard:MaxFailures` | `LoginGuard__MaxFailures` | 账号登录失败锁定阈值（默认 5 次） |
| `LoginGuard:LockMinutes` | `LoginGuard__LockMinutes` | 锁定时长（分钟，默认 15） |
| `LoginGuard:IpLimitEnabled` | `LoginGuard__IpLimitEnabled` | IP 限流开关（默认 `false`；反向代理部署需先配置 ForwardedHeaders 再开启） |

> 说明：若 `Jwt:SecretKey` 为空，启动时会生成**一次性随机密钥**（重启后旧 token 失效），仅适合本地开发，并会打印警告日志。

## 生产部署注意事项

1. 通过环境变量注入 `ConnectionStrings__AssetConnection`、`Jwt__SecretKey`、`AdminSeed__Password`，切勿提交明文凭据。
2. 将 `DatabaseInitialization`、`DemoData`、`Sms:ReturnCodeInResponse` 全部设为 `false`。
3. 通过 `Cors:AllowedOrigins` 限定实际前端域名。
4. 非开发环境会自动启用 HTTPS 重定向，请配置好 TLS 证书。
5. 日志默认输出到控制台（`Information` 级别），生产可按需接入文件/集中式日志。

## 日志

- 使用 Serilog 双写：控制台 + 文件。
- 文件位置：`logs/asset-api-YYYYMMDD.log`（相对于后端运行目录），按天滚动、单文件 50MB 上限、最多保留 31 天。
- 访问日志（每次请求的方法/路径/状态码/耗时）由 `ApiPipelineMiddleware` 记录；未处理异常记录完整堆栈。
- 建议将 `logs/` 接入集中式日志（ELK / Loki 等）收集，并按磁盘容量调整保留策略。

## 备份与恢复

备份范围：**SQL Server 数据库**（资产/用户/权限/消息等全部数据）+ **附件目录**（`wwwroot/uploads`，资料管理的上传文件）。日志目录无需备份。

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

## 自动化测试

```bash
cd AssetManagementSystem
dotnet test
```

测试分两层：

- **单元测试**（不依赖数据库）：密码哈希（BCrypt）、图形验证码（一次性/大小写）、SVG 安全清洗、登录防爆破锁定逻辑、权限定义一致性（防止新增权限漏配）。
- **集成测试**（`WebApplicationFactory`，免数据库）：验证码接口、无验证码登录被拒、未登录访问受保护接口返回 401、刷新令牌入参校验等——这些用例都刻意停在访问数据库之前的校验环节，CI 无需数据库即可运行。
