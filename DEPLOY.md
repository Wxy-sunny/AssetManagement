# 生产部署指南（公网部署）

本系统后端为 .NET 8（Kestrel），前端为 Vite 静态资源。**不要**把 Kestrel 的 5099 端口直接暴露在公网；
务必放在反向代理（nginx / Caddy）之后做 TLS 终止与安全加固。

---

## 1. 架构

```
[ 用户浏览器 ]
      │  HTTPS (443)
      ▼
[ nginx / Caddy ]  ← TLS、HSTS、安全头、限流、静态资源
      │  HTTP (内部) + X-Forwarded-For / X-Forwarded-Proto
      ▼
[ Kestrel :5099 ]  ← ASP.NET Core API
      ▼
[ SQL Server / MySQL ]  + [ Redis（可选，验证码缓存） ]
```

---

## 2. 环境变量（生产必须注入，切勿写入仓库）

| 变量 | 说明 |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | 设为 `Production`，启用安全头、HTTPS 重定向、关闭 Swagger |
| `Jwt__SecretKey` | **≥32 字节**的稳定密钥（base64 或任意长随机串）。缺失则生产环境启动直接失败 |
| `ConnectionStrings__AssetConnection` | 数据库连接串（含密码）。例如 `Server=...;Database=...;User Id=...;Password=...;` |
| `AdminSeed__Password`（可选） | 首次初始化的管理员密码；留空则自动生成随机强密码并打印到控制台 |
| `ReverseProxy__TrustedProxies`（可选） | 反向代理 IP，逗号分隔（如 `127.0.0.1,10.0.0.5`）。未配置会取错客户端 IP |
| `Redis__Connection`（可选） | Redis 连接串；缺失时验证码自动降级为内存缓存 |

> 生产配置模板见 `AssetManagementSystem/appsettings.Production.json`。该文件**不含任何真实凭据**，
> 所有机密走环境变量（`__` 双下划线表示配置层级）。

---

## 3. nginx 参考配置

```nginx
server {
    listen 443 ssl http2;
    server_name your-domain.com www.your-domain.com;

    ssl_certificate     /etc/nginx/ssl/your-domain.com/fullchain.pem;
    ssl_certificate_key /etc/nginx/ssl/your-domain.com/privkey.pem;
    # 推荐 Mozilla  intermediate 套件，并开启 OCSP stapling

    # 安全头（也可由应用层下发，这里再补一层更稳）
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "DENY" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # 前端静态资源（npm run build 产物）
    location / {
        root /var/www/asset-frontend/dist;
        try_files $uri $uri/ /index.html;
    }

    # 上传文件（按需在代理层限制大小）
    client_max_body_size 100m;

    # API 反向代理：务必透传 X-Forwarded-*，否则 IP 限流/登录守卫会取错客户端 IP
    location /api/ {
        proxy_pass http://127.0.0.1:5099/;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP        $remote_addr;
        proxy_set_header X-Forwarded-For  $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host  $host;
    }
}
```

> 前端 `vite.config.ts` 中 `proxy` 的 `/api` 前缀仅用于本地开发。生产由 nginx 负责 `/api` 转发，
> 前端打包时 `VITE_API_BASE` 应设为空或 `/api`，使请求走相对路径。

---

## 4. 上线前核对表

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `Jwt__SecretKey` 已设置（≥32 字节，且与备份一致、多实例相同）
- [ ] `ConnectionStrings__AssetConnection` 已设置，**非**空
- [ ] `appsettings.Production.json` 的 `AllowedHosts` / `Cors:AllowedOrigins` 已改为真实域名
- [ ] `DemoData:Enabled=false`（默认即 false）
- [ ] `DatabaseInitialization:Enabled=false`（首次部署初始化表后可关闭）
- [ ] `Sms:ReturnCodeInResponse=false`（否则验证码回传前端）
- [ ] 反向代理已透传 `X-Forwarded-For` / `X-Forwarded-Proto`，并把代理 IP 加入 `ReverseProxy:TrustedProxies`
- [ ] TLS 证书有效，HSTS 已开启
- [ ] 数据库定期备份（含异机/异地副本）
- [ ] `npm audit` 与 `dotnet list package --vulnerable` 已扫描并修复高危项
- [ ] 已修改初始管理员密码（若依赖随机生成密码，请先登录再改）

---

## 5. 已内置的安全能力

- 密码 BCrypt 哈希；刷新令牌仅存 SHA256 哈希
- JWT 完整校验（Issuer/Audience/Lifetime/SigningKey），生产缺失密钥即启动失败
- 登录防爆破（账号锁定 + 可选 IP 限流）
- 速率限制：`/auth/sms/code`（10 分钟 5 次）、`/auth/register`、`/auth/password/forgot`、`/request/submit`（10 分钟 10 次/IP）
- 安全响应头：HSTS（HTTPS 下）、CSP、X-Frame-Options、X-Content-Type-Options、Referrer-Policy；移除 `Server` 头
- 全局异常中间件（不向客户端泄露堆栈）
- 文件上传：扩展名白名单 + 大小限制 + SVG 强制附件下载 + `nosniff`
- Swagger 仅在开发环境开放

---

## 6. 本仓库内置部署模板（`deploy/`）

仓库已提供开箱即用的部署参考文件，按真实域名替换 `example.com` 即可：

| 文件 | 用途 |
|---|---|
| `deploy/app.env.example` | 生产环境变量模板，复制为 `/etc/asset-api/app.env` 填入真实值（**切勿提交含真实凭据的文件**） |
| `deploy/nginx-asset.conf` | nginx 反向代理 + TLS 终止配置（含 HSTS / 安全头 / `/api` 转发 / 附件转发） |
| `deploy/asset-api.service` | systemd 单元，引用上面的 env 文件并做最小权限加固 |

使用流程简述：

```bash
# 1) 后端发布（在装有 .NET 10 SDK 的机器上）
dotnet publish AssetManagementSystem/AssetManagementSystem/AssetManagementSystem.csproj \
  -c Release -o /opt/asset-api

# 2) 准备环境变量
sudo cp deploy/app.env.example /etc/asset-api/app.env
sudo nano /etc/asset-api/app.env   # 填入 Jwt__SecretKey / 连接串 / 管理员密码

# 3) 安装并启用服务
sudo cp deploy/asset-api.service /etc/systemd/system/
sudo useradd -r -s /usr/sbin/nologin assetapi
sudo chown -R assetapi:assetapi /opt/asset-api
sudo systemctl daemon-reload && sudo systemctl enable --now asset-api

# 4) nginx
sudo cp deploy/nginx-asset.conf /etc/nginx/sites-available/asset
#   编辑把 example.com 改为真实域名并配好证书，再软链 + `nginx -t && systemctl reload nginx`
```

> 注意：前端 `npm run build` 需要 **Node.js ≥ 22.18**（见 `README.md` 环境要求）。
> 当前构建机若版本过低会构建失败，请在 CI 或专用构建机使用符合要求的 Node 版本，
> 然后把 `dist/` 部署到 nginx 的 `/var/www/asset-frontend/dist`。
>
> 上线前请按第 4 节核对表逐项确认，并完成一次 `scripts/backup/` 下的"备份 → 还原"演练。

---

## 7. Docker 部署（可选，推荐）

仓库已提供容器化打包，免手动安装 .NET / Node 运行时：

| 文件 | 用途 |
|---|---|
| `AssetManagementSystem/AssetManagementSystem/Dockerfile` | 后端多阶段镜像（.NET 10 SDK 构建 → aspnet:10.0 运行），监听 5099 |
| `AssetManagementSystem.Frontend/Dockerfile` | 前端镜像（node:22 构建 → nginx:alpine 托管并反向代理 `/api`） |
| `AssetManagementSystem.Frontend/nginx.conf` | 前端容器内 nginx 配置（SPA 回退 + 代理到 `backend:5099`） |
| `docker-compose.yml` | 编排 backend / frontend / redis，可选自带 mssql（需 `--profile db`） |
| `.env.example` | compose 用密钥模板，复制为 `.env` 填入 |

快速开始：

```bash
cp .env.example .env && nano .env        # 填入 Jwt__SecretKey / AssetConnection / 等
docker compose up -d                     # 前端暴露在宿主机 8080
# 如需自带 SQL Server：docker compose --profile db up -d
```

> 安全提醒：
> - 本编排仅内部 HTTP。**不要直接把 8080 暴露到公网**；请在宿主机再用 nginx / Caddy / Traefik 做 TLS 终止（证书 + 443 转发到 8080）。
> - 环境变量来自 `.env`（已被 git 忽略），不会烤进镜像——这也是"环境变量在云端服务器编辑"的位置。
> - `ReverseProxy__TrustedProxies=172.16.0.0/12` 已信任 Docker 网桥，确保登录 IP 限流取到真实客户端 IP。
> - 自带 mssql 服务的数据在 `mssql-data` 卷；生产建议用云数据库并另行备份。


