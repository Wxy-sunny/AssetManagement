# 生产部署指南（SQLite + Docker，腾讯云 2GB 小机）

本指南记录**实际走通的一次完整部署流程**：腾讯云 CVM（2 核 2GB / 3Mbps / Ubuntu）、内置 SQLite 数据库、Docker 一键编排、用公网 IP 直接访问（HTTP 80，不做 TLS）。

每一步都给出：**为什么这么做 → 命令逐条解释 → 怎么验证成功**。

---

## 0. 部署总览

```
[ 浏览器 ] ──HTTP 80──▶ [ 前端容器 nginx:80 ] ──/api/──▶ [ 后端容器 Kestrel:5099 ]
                                                      │
                                            [ redis:6379 ]   验证码缓存
                                            [ SQLite 文件 /app/data/asset.db ]
```

| 容器 | 服务 | 作用 | 端口 |
|---|---|---|---|
| `frontend` | nginx | 托管前端静态资源，反向代理 `/api`、`/uploads`、`/avatars` | 宿主机 `80` |
| `backend` | .NET 10 (Kestrel) | 业务 API | 仅容器内 `5099` |
| `redis` | redis:7-alpine | 图形/短信验证码缓存 | 仅容器内 `6379` |

**为什么用 SQLite 而不是 SQL Server**：SQL Server 容器启动时会硬性检查物理内存 ≥ 2GB，2GB 的机器装不下（会启动失败或反复 OOM）。SQLite 是单文件数据库，零额外进程，最省内存，业务代码无需改动。

---

## 1. 分支选择（先搞清楚自己部署的是哪一版）

| 分支 | 数据库 | Docker 编排 | 适用场景 |
|---|---|---|---|
| `main` | SQL Server（原始版） | 无 | **已锁定**，只能通过 PR 修改 |
| `sqlite` | **SQLite**（本文档） | 有，无 `db` 服务 | 2GB 小机 ✅ |
| `sqlserver-docker` | SQL Server | 有，含 `db` 容器 | 本地 / 内存 ≥4GB 的机器 |

> ### 本分支（`sqlserver-docker`）说明
>
> 本分支默认使用 **SQL Server**：`docker-compose.yml` 自带 `db` 容器（SQL Server 2022），
> `backend` 会等它健康（1433 端口就绪）后再启动。
>
> `.env` 必须包含以下内容（`DbSaPassword` 是 SQL Server 的 sa 密码，需与连接串里的密码一致）：
> ```
> DbType=sqlserver
> AssetConnection=Server=db,1433;Database=AssetDB;User Id=sa;Password=<与 DbSaPassword 一致>;TrustServerCertificate=True;
> DbSaPassword=<强密码>
> ```
> 其余章节（Docker 部署流程、短信配置、日常运维、故障排查）与 `sqlite` 分支完全一致；
> 若想改用 SQLite，把 `DbType` 改为 `sqlite`、`AssetConnection` 改为 `DataSource=/app/data/asset.db` 并移除 `db` 服务即可。
>
> ⚠️ SQL Server 容器启动时要求物理内存 ≥ 2GB，2GB 的小机器请使用 `sqlite` 分支。

```bash
# 确认当前分支（应为 sqlite）
git branch
# 切换分支
git checkout sqlite
```

> `main` 已配置分支保护（Ruleset `protect-main`），直接 `git push origin main` 会被 GitHub 拒绝。日常改动都在子分支做。

---

## 2. 准备阶段

### 2.1 登录服务器

```bash
ssh root@<你的公网IP>
```

- `ssh`：远程登录命令。
- `root@<IP>`：以 root 身份登录（后面大量命令需要写系统目录）。
- 出现密码提示时输入服务器密码；看到 `root@xxx:~$` 提示符即登录成功。

### 2.2 加 2GB swap（2GB 机器必做，防 OOM）

物理内存只有 2GB，构建 .NET 镜像时很容易被 OOM Killer 杀掉。加 swap 用磁盘换内存。

```bash
sudo fallocate -l 2G /swapfile   # 预分配 2GB 磁盘文件作为 swap 区
sudo chmod 600 /swapfile         # 权限设为仅 root 可读写（swap 内容可能含敏感数据）
sudo mkswap /swapfile            # 标记为 swap 空间（在磁盘文件上写入 swap 头信息）
sudo swapon /swapfile            # 立即启用
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab   # 开机自动挂载
```

逐条解释：
- `fallocate -l 2G`：立刻分配 2GB 文件（比 `dd` 快很多）。
- `chmod 600`：防止其他用户读取 swap 内容。
- `mkswap`：把普通文件格式化为 swap 区。
- `swapon`：挂载启用。
- `/etc/fstab` 那行：`/swapfile` 是设备，`none` 表示不做文件系统检查，`swap` 是类型，后两个 `0` 是 dump/检查优先级。

✅ **验证**：
```bash
swapon --show    # 出现 /swapfile 即成功
free -h          # Swap 行显示已用/总量
```

### 2.3 安装 Docker + 腾讯云内网镜像加速

公网带宽只有 3Mbps，直接从 Docker Hub 拉镜像很慢。腾讯云 CVM 可以用**内网镜像加速器**（不走公网流量，很快）。

```bash
sudo apt-get update                                  # 更新软件源索引
sudo apt-get install -y docker.io docker-compose-v2   # 安装 Docker 引擎 + Compose v2 插件
sudo systemctl enable --now docker                   # 设为开机自启并立即启动
sudo mkdir -p /etc/docker                            # 确保配置目录存在
sudo tee /etc/docker/daemon.json >/dev/null <<'EOF'  # 写入镜像加速配置
{ "registry-mirrors": ["https://mirror.ccs.tencentyun.com"] }
EOF
sudo systemctl restart docker                        # 重启使配置生效
```

逐条解释：
- `apt-get update`：刷新包索引，否则可能找不到 `docker-compose-v2`。
- `-y`：自动确认安装，无需交互。
- `systemctl enable --now`：`enable`=开机自启，`--now`=现在就启动。
- `daemon.json` 是 Docker 的全局配置，`registry-mirrors` 只对 **Docker Hub** 生效。
- `tee ... >/dev/null`：把内容写入文件同时不打印到屏幕（`<<'EOF'` 表示直到 `EOF` 结束的单引号块，原样写入不做变量展开）。

✅ **验证**：
```bash
docker --version            # 例如 Docker version 29.1.3
docker compose version      # 显示 v2.x（低于 v2 不支持本项目的 compose 文件）
sudo systemctl is-active docker   # 应输出 active
cat /etc/docker/daemon.json        # 确认加速器地址
```

> ⚠️ `mcr.microsoft.com`（微软的 .NET 基础镜像）**不走**该加速器，需要走公网国际链路，会比较慢，见第 5 节。

### 2.4 停掉系统 nginx（80 端口冲突）

Ubuntu 镜像常预装 nginx，它默认监听 80，而我们的前端容器也要用 80 → 端口冲突启动失败。

```bash
sudo systemctl stop nginx      # 立即停止
sudo systemctl disable nginx   # 取消开机自启（避免重启后又抢端口）
sudo ss -lntp | grep ':80' || echo "80 已空闲"
```

逐条解释：
- `ss -lntp`：列出正在监听的 TCP 端口及对应进程（`-l` 监听、`-n` 不解析域名、`-t` TCP、`-p` 显示进程）。
- `grep ':80'`：筛出 80 端口；`|| echo` 表示 grep 没匹配到（有输出）时执行 echo，即端口空闲。

✅ **验证**：最后一行输出 `80 已空闲`。

---

## 3. 获取代码

> 国内服务器访问 GitHub 经常很慢甚至卡死。准备三种方式，**优先用方式 B**。

### 方式 A：git clone（最标准，但可能卡）

```bash
sudo mkdir -p /opt/asset          # 创建部署目录
sudo git clone -b sqlite https://github.com/Wxy-sunny/AssetManagement.git /opt/asset
cd /opt/asset
```
- `-b sqlite`：直接检出 `sqlite` 分支（否则默认是 main，服务器跑的是 SQL Server 版）。
- 最后一个参数是目标目录。

⚠️ 若长时间停在 `Cloning into '/opt/asset'...` 且目录大小不变（另开终端 `du -sh /opt/asset` 查看），说明卡住了 → 按 `Ctrl+C` 中止，改用方式 B。

### 方式 B：下载分支压缩包（推荐，单文件下载更稳）

```bash
sudo rm -rf /opt/asset /tmp/asset.tar.gz
curl -L --connect-timeout 15 --max-time 180 -o /tmp/asset.tar.gz \
  https://ghfast.top/https://github.com/Wxy-sunny/AssetManagement/archive/refs/heads/sqlite.tar.gz
ls -lh /tmp/asset.tar.gz
sudo tar -xzf /tmp/asset.tar.gz -C /opt
sudo mv /opt/AssetManagement-sqlite /opt/asset
ls /opt/asset/docker-compose.yml
```
- `curl -L`：跟随重定向（代理站点会 302 到真实地址）。
- `--connect-timeout 15`：连接超时 15 秒；`--max-time 180`：整体最多 180 秒，避免无限挂起。
- `-o`：指定输出文件。
- `tar -xzf`：解压 gzip 格式；`-C /opt` 指定解压目录。
- GitHub 压缩包解压后目录名是 `AssetManagement-sqlite`，所以要 `mv` 成 `/opt/asset`。
- 代理地址若不通，换 `ghproxy.net` 或 `gh-proxy.com` 替换 `ghfast.top`。

✅ **验证**：`ls /opt/asset/docker-compose.yml` 能列出文件。

### 方式 C：本地打包上传（兜底）

在**本地电脑** PowerShell 执行：
```powershell
cd D:\Projects\AssetManagement
git archive -o D:\asset.zip sqlite
```
`git archive` 只打包该分支**已提交**的文件，自动排除 `.git`、`node_modules`、`bin`、`obj`、`.env`，体积很小。
然后用 OrcaTerm 文件管理器把 `asset.zip` 上传到 `/opt`，在服务器：
```bash
cd /opt && sudo unzip -o asset.zip && sudo rm -f asset.zip && sudo mv AssetManagement-sqlite asset && ls /opt/asset/docker-compose.yml
```

---

## 4. 配置 `.env`（生产密钥，仓库里没有，必须自建）

`.env` 已被 `.gitignore` 忽略，不会随代码下发，必须在服务器上创建。

```bash
cd /opt/asset
JWT=$(openssl rand -base64 48)     # 生成 48 字节随机密钥（JWT 签名用）
ADMIN=$(openssl rand -base64 18)   # 生成 18 字节随机管理员初始密码
sudo tee .env >/dev/null <<EOF    # 写入 .env 文件
Jwt__SecretKey=$JWT
DbType=sqlite
AssetConnection=DataSource=/app/data/asset.db
AdminSeed__Password=$ADMIN
Redis__Connection=redis:6379
EOF
cat .env
```

逐条解释：
- `openssl rand -base64 48`：生成 48 字节密码学安全随机数并 base64 编码；JWT 密钥要求 ≥32 字节。
- `$JWT` / `$ADMIN`：shell 变量，取上面命令的输出；`$( )` 是命令替换语法。
- `<<EOF`（**不加引号**）：heredoc，里面的 `$JWT`、`$ADMIN` 会被 shell 展开成实际值。若写成 `<<'EOF'` 则不会展开，会写入字面量 `$JWT`（坑）。
- `sudo tee .env`：以 root 权限写文件（`sudo` 后 tee 才有权限），`-a` 是追加，这里不需要；`>/dev/null` 屏蔽输出。
- `DbType=sqlite` → compose 通过 `SqlSugarConfig__DbType: ${DbType}` 传给后端，决定用哪种数据库。
- `AssetConnection=DataSource=/app/data/asset.db` → SQLite 单文件路径，落在 `asset-data` 数据卷里，容器重建不丢。
- `Redis__Connection=redis:6379` → compose 内网服务名 `redis`。
- `AdminSeed__Password` → 首次启动自动播种 `admin` 账号的密码，**登录后请立刻改掉**。

✅ **验证**：`cat .env` 输出 5 行，`AdminSeed__Password` 非空。密码忘了可随时 `grep AdminSeed /opt/asset/.env` 再查。

---

## 5. 腾讯云控制台放通 80（控制台操作，不能用命令）

1. 打开腾讯云控制台 → 云服务器 CVM → 找到实例 → **安全组**（或"防火墙"）。
2. 添加入站规则：**协议 TCP**、**端口 80**、**来源 0.0.0.0/0**。
3. 确认 `22`（SSH）规则仍在，否则会断连。

> 安全组是云厂商侧的包过滤规则，服务器内部 `curl` 到自己的公网 IP 也可能通，但**外部机器**能否访问完全取决于它。

---

## 6. 构建并启动

```bash
cd /opt/asset
sudo docker compose up -d --build
```

逐条解释：
- `docker compose`：读取当前目录的 `docker-compose.yml`。
- `up`：创建并启动服务。
- `-d`：后台运行（detached），命令立即返回。
- `--build`：强制重新构建镜像（首次必须；后端/前端代码有改动时也必须，否则还用旧镜像）。

**首次会很慢（10~25 分钟）**，原因是：
- `redis` 走腾讯云内网加速器 → 几秒完成；
- `mcr.microsoft.com/dotnet/sdk:10.0`（.NET SDK 基础镜像，约 200~300MB）**不走**该加速器，只能走国际链路，实测约 140KB/s。

✅ **验证**：
```bash
sudo docker compose ps
```
三个服务状态都应为 `Up`，前端应显示 `0.0.0.0:80->80/tcp`。

---

## 7. 验证清单（逐条执行，全部通过才算部署完成）

```bash
# 1) 容器都起来了吗
sudo docker compose ps

# 2) 后端日志：自动建表 + 播种管理员
sudo docker compose logs backend | tail -30
```
✅ 日志应出现 `数据库 AssetConnection 成功创建 12 个表`、`已初始化管理员账号：admin`。
（出现 `DataProtection keys ... not persisted` 属正常，容器重启后密钥会变，见第 10 节加固建议。）

```bash
# 3) 容器层接口是否通（走本机 80 → nginx → backend）
curl -s http://127.0.0.1/api/auth/captcha | head -c 200
```
✅ 返回 `{"code":200,"message":"获取成功","data":{"captchaId":...,"imageBase64":"data:image/svg+xml;base64,...`

```bash
# 4) 对外是否通（同时验证安全组 + nginx 反代）
curl -s http://<你的公网IP>/api/auth/captcha | head -c 200
```
✅ 同样返回 JSON。

```bash
# 5) 浏览器打开 http://<你的公网IP>
```
✅ 出现登录页，用 `admin` + 第 4 步生成的 `AdminSeed__Password` 登录。

```bash
# 6) 登录后建议立刻改密码；也可在后台重置
```

---

## 8. 故障排查（症状 → 原因 → 处理）

| 症状 | 原因 | 处理 |
|---|---|---|
| `bind: address already in use` | 80 端口被系统 nginx 占用 | `sudo systemctl stop nginx`（2.4 节） |
| `Cloning into ...` 一直不动 | GitHub 直连卡住 | 换第 3 节方式 B |
| 拉镜像特别慢 | `mcr.microsoft.com` 不走内网加速 | 属正常，等 10~25 分钟；`sudo docker system df` 看进度 |
| 后端起不来，日志报连接串错误 | `.env` 缺项或写错 | `cat /opt/asset/.env` 核对 5 行 |
| `400 Invalid Hostname` | 主机白名单没放行你的 IP | 在 `appsettings.Production.json` 的 `AllowedHosts` 加公网 IP 后重新 `up -d --build` |
| 浏览器跳到 https 打不开 | 旧版强制 HTTPS 重定向 / HSTS | 本项目已关闭 `UseHttpsRedirection`、nginx 用 `$scheme`；若浏览器缓存了 HSTS，用无痕窗口 |
| 登录后立刻跳回登录页 | **用户没分配角色** | 后台「权限管理 → 用户管理」给该用户选角色，然后**重新登录**（JWT 里的角色是登录时写入的） |
| 登录后提示"没有该功能的操作权限" | 角色权限没配 | 后台「权限管理 → 角色管理 → 分配权限」勾选对应功能 |
| 短信验证码收不到 | 短信服务未启用（默认关闭），或服务商配置/签名/模板有误 | 见第 11 节；后端日志 `grep SMS` 可看具体错误码 |
| 磁盘满 | 构建缓存 + 镜像占空间 | `docker system prune -a`（会删未使用的镜像） |

---

## 9. 日常运维

```bash
cd /opt/asset

# 查看服务状态
sudo docker compose ps

# 实时看后端日志（排错最常用）
sudo docker compose logs -f backend

# 重启后端（改了后端代码并重新 build 后）
sudo docker compose up -d --build backend

# 全部停止（保留数据卷）
sudo docker compose down

# 停止并删除数据卷（会清空数据库！）
sudo docker compose down -v

# 备份 SQLite 数据（拷走这一个文件即可）
sudo docker compose exec backend cat /app/data/asset.db > ./asset-$(date +%F).db

# 查看磁盘占用
sudo docker system df
```

**更新代码**（本地改完推送后）：
```bash
cd /opt/asset
sudo git fetch origin && sudo git checkout sqlite && sudo git pull origin sqlite
sudo docker compose up -d --build
```
> 若当初是方式 B（压缩包）部署的，目录里没有 `.git`，需要重新下载压缩包覆盖，或改用方式 A/C。

---

## 10. 生产加固建议（按需）

1. **HTTPS**：在宿主机外层用 Caddy/nginx 做 TLS 终止（443 → 80），再把 `Program.cs` 中注释掉的 `UseHttpsRedirection` 启用。注意：外层代理必须透传 `X-Forwarded-Proto`。
2. **DataProtection 密钥持久化**：在 compose 的 backend 服务加 `dpkeys:/root/.aspnet/DataProtection-Keys` 卷并在 `volumes:` 声明，重启后登录态不再失效。
3. **关闭自动建表**：表结构稳定后，把 compose 里 `DatabaseInitialization__Enabled` 改为 `"false"`。
4. **数据库初始化开关**：`DatabaseInitialization__CreateTablesOnStartup` 是幂等补表，可保留。
5. **定期备份**：SQLite 是单文件，定时 `docker compose exec backend cat /app/data/asset.db > backup.db` 即可。
6. **换强密码**：管理员初始密码在 `.env` 里且出现过在终端记录中，登录后务必修改。

---

## 11. 短信验证码配置（默认未启用）

短信功能**已完整接入并可配置**，但**默认关闭**：未配置时接口会明确返回“短信服务未启用”，**不会再出现“提示已发送、手机却收不到”的假成功**。

### 11.1 四种通道

| Provider | 说明 | 需要填的参数 |
|---|---|---|
| `none` | 不发送（默认） | — |
| `log` | 只写后端日志，**不真实下发**，用于内网演示/联调 | 无（可开 `ReturnCodeInResponse` 让验证码直接显示在页面上） |
| `aliyun` | 阿里云短信（dysmsapi），**真实下发** | `AccessKeyId`、`AccessKeySecret`、`SignName`、`TemplateCode` |
| `webhook` | 自建 / 第三方 HTTP 网关，**真实下发** | `WebhookUrl`（可选 `WebhookToken`） |

### 11.2 接入阿里云（真实发送）

1. 阿里云控制台开通「短信服务」，创建**签名**（`SignName`，需审核通过）和**验证码模板**（`TemplateCode`，模板变量名固定为 `code`）。
2. 拿到 `AccessKeyId` / `AccessKeySecret`。
3. 在服务器 `/opt/asset/.env` 追加：
```
SmsEnabled=true
SmsProvider=aliyun
SmsAccessKeyId=你的AccessKeyId
SmsAccessKeySecret=你的AccessKeySecret
SmsSignName=你的签名
SmsTemplateCode=SMS_123456789
# 可选：业务通知类短信模板（模板变量名 content）
SmsTextTemplateCode=SMS_987654321
```
4. 重启后端使配置生效：
```bash
cd /opt/asset && sudo docker compose up -d backend
```

> 实现说明：后端直接调用阿里云 OpenAPI 并自行完成 HMAC-SHA1 签名（`Services/Sms/AliyunSigner.cs`），**没有引入阿里云 SDK**，镜像体积和构建时间不受影响。

### 11.3 接入自建 / 第三方网关（webhook）

在 `/opt/asset/.env` 追加：
```
SmsEnabled=true
SmsProvider=webhook
SmsWebhookUrl=https://your-gateway/sms
SmsWebhookToken=可选的校验令牌
```
后端会以 `POST`（JSON）转发到该地址：
```json
{ "type": "code", "mobile": "13800138000", "code": "123456", "text": null, "timestamp": 1760000000 }
```
`type` 为 `code`（验证码）或 `text`（业务通知）；配了 Token 会带请求头 `X-Sms-Token`。

### 11.4 联调模式（不真实发短信）

```
SmsEnabled=true
SmsProvider=log
SmsReturnCodeInResponse=true
```
验证码会写进后端日志，并**直接显示在登录页上**，方便自测：
```bash
sudo docker compose logs backend | grep "SMS-LOG"
```

### 11.5 注意事项

- 发送失败时**不会写入验证码缓存**（先发后存），用户输入任何验证码都无效，不会出现“发不出去但验证码却有效”的错乱。
- 限流仍在：同 IP 10 分钟 5 次、同一手机号 60 秒 1 次。
- 签名/模板审核不通过时，后端日志会打印阿里云返回的错误码（如 `isv.SMS_SIGNATURE_ILLEGAL`），可据此排查。
- 全部参数也可直接用 `Sms__Xxx` 环境变量注入（compose 已透传，见 `docker-compose.yml`）。

---

## 12. 附：本次部署实际踩到的坑（供参考）

1. **GitHub clone 卡死** → 改用分支压缩包 + 代理下载（方式 B）。
2. **80 端口被系统 nginx 占用** → 停用系统 nginx。
3. **.NET 基础镜像拉取极慢** → 属国际链路，非配置错误，等待即可。
4. **新用户登录后立刻"过期"** → 根因是该用户未分配角色，后端把"无角色"当成 401（未登录），前端据此清空登录态；现已改为返回 403，并在前端加了路由级权限拦截与 403 页面。
