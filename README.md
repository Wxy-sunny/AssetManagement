# 资产管理系统 (Asset Management System)

一套前后端分离的企业资产全生命周期管理平台，覆盖资产台账、分类树、部门、角色权限、通知与数据仪表盘。

- **后端**：ASP.NET Core Web API（.NET 8），基于 SqlSugar ORM 操作 SQL Server，JWT 鉴权，Swagger 文档，Redis 可选（验证码存储，不可用时自动降级为内存缓存）。
- **前端**：Vue 3 + TypeScript + Vite，Element Plus 组件库，Pinia 状态管理，Vue Router，ECharts 图表，Axios 请求。

## 功能模块

- 用户登录 / JWT 鉴权
- 资产管理（增删改查、资料附件上传）
- 资产分类树（支持关键字检索）
- 部门管理
- 角色与权限管理（细粒度接口级权限）
- 通知公告
- 数据仪表盘（ECharts 可视化）

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

> 说明：若 `Jwt:SecretKey` 为空，启动时会生成**一次性随机密钥**（重启后旧 token 失效），仅适合本地开发，并会打印警告日志。

## 生产部署注意事项

1. 通过环境变量注入 `ConnectionStrings__AssetConnection`、`Jwt__SecretKey`、`AdminSeed__Password`，切勿提交明文凭据。
2. 将 `DatabaseInitialization`、`DemoData`、`Sms:ReturnCodeInResponse` 全部设为 `false`。
3. 通过 `Cors:AllowedOrigins` 限定实际前端域名。
4. 非开发环境会自动启用 HTTPS 重定向，请配置好 TLS 证书。
5. 日志默认输出到控制台（`Information` 级别），生产可按需接入文件/集中式日志。

## 构建状态

- 后端：`dotnet build` 通过（0 错误）。
- 前端：`npm run build`（`vue-tsc` 类型检查 + `vite build`）通过。
