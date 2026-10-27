# 部署指南（SQLite + Docker，腾讯云 2GB 小机）

本指南针对 **腾讯云 CVM（2 核 2GB / Ubuntu）** 的最小化部署：内置 **SQLite** 数据库 + Docker 一键编排，直接用公网 IP 访问（HTTP 80，无需 TLS 即可演示）。

---

## 分支策略

| 分支 | 用途 | 说明 |
|---|---|---|
| `main` | **已锁定**（需 PR 才能合并） | SQL Server 原始基线，作为稳定参考 |
| `sqlite` | **本部署用** | SQLite 内置数据库 + Docker 编排，前端端口 80 |
| `sqlserver-docker` | 需要 SQL Server 时用 | compose 自带 `db` 容器，适合本地 / 有内存的机器 |

> 开发请在子分支进行（`git checkout -b feat-xxx sqlite`），改完开 PR 合回 `main`。
> `main` 已配置分支保护规则（Ruleset `protect-main`）：直接 `git push origin main` 会被拒绝。

---

## 架构

```
[ 浏览器 ] ──HTTP 80──▶ [ 前端容器 nginx:80 ] ──/api/──▶ [ 后端 Kestrel:5099 ]
                                                     │
                                           [ Redis:6379 ]  （验证码缓存）
                                           [ SQLite 文件 /app/data/asset.db ]
```

---

## 一、服务器准备

### 1. 登录
```bash
ssh root@<你的公网IP>
```

### 2. 加 2GB swap（2GB 机器必做，防 OOM）
```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```
✅ 验证：`swapon --show` 看到 `/swapfile`；`free -h` 的 Swap 约 2G。

### 3. 安装 Docker + 腾讯云内网加速（3Mbps 公网拉镜像很慢，走内网加速器）
```bash
sudo apt-get update
sudo apt-get install -y docker.io docker-compose-v2
sudo systemctl enable --now docker
sudo mkdir -p /etc/docker
sudo tee /etc/docker/daemon.json >/dev/null <<'EOF'
{ "registry-mirrors": ["https://mirror.ccs.tencentyun.com"] }
EOF
sudo systemctl restart docker
```
✅ 验证：`docker --version`、`docker compose version`（显示 v2.x）；`docker info | grep -A1 Mirrors` 显示 `tencentyun`。
> 若 `docker-compose-v2` 装不上，改用 `sudo apt-get install -y docker-compose-plugin` 或 `curl -fsSL https://get.docker.com | sh`。

### 4. 停掉系统 nginx（它默认占 80，会和容器抢端口）
```bash
sudo systemctl stop nginx 2>/dev/null; sudo systemctl disable nginx 2>/dev/null
sudo ss -lntp | grep ':80' || echo "80 已空闲"
```
✅ 验证：显示“80 已空闲”。

---

## 二、部署（sqlite 分支）

### 5. 拉代码
```bash
sudo mkdir -p /opt/asset
sudo git clone -b sqlite https://github.com/Wxy-sunny/AssetManagement.git /opt/asset
cd /opt/asset
```
✅ 验证：`ls` 看到 `docker-compose.yml`；`git branch` 显示 `* sqlite`。

### 6. 生成密钥并写 `.env`
`.env` 已被 git 忽略，clone 下来没有，必须手动创建：
```bash
JWT=$(openssl rand -base64 48); ADMIN=$(openssl rand -base64 18)
sudo tee .env >/dev/null <<EOF
Jwt__SecretKey=$JWT
DbType=sqlite
AssetConnection=DataSource=/app/data/asset.db
AdminSeed__Password=$ADMIN
Redis__Connection=redis:6379
EOF
cat .env
```
✅ 验证：输出 5 行，且 `AdminSeed__Password` 非空。

### 7. 腾讯云控制台放通 80（控制台操作，非命令）
实例 → 防火墙 / 安全组 → 添加入站规则：**TCP 80**，来源 `0.0.0.0/0`；`22` 保持开放。

### 8. 构建并启动
```bash
sudo docker compose up -d --build
```
✅ 验证：`sudo docker compose ps` 三个服务状态均为 `Up`。

### 9. 看日志确认建表 + 播种
```bash
sudo docker compose logs backend | tail -30
```
✅ 验证：出现 `成功创建 12 个表`、`已初始化管理员账号：admin`。

### 10. 接口验证
```bash
curl -s http://127.0.0.1/api/auth/captcha | head -c 200        # 容器层
curl -s http://<公网IP>/api/auth/captcha | head -c 200          # nginx + 安全组
```
✅ 验证：两次都返回 JSON（含 `imageBase64`、`captchaId`），不是 400。

### 11. 浏览器登录
打开 `http://<公网IP>`，用 `admin` + 第 6 步的 `ADMIN` 密码登录（`grep AdminSeed .env` 可查）。

---

## 三、环境自检（确认前面步骤是否完成）

在服务器执行，对照判断进度：
```bash
echo "=== swap ==="; swapon --show
echo "=== docker ==="; docker --version; docker compose version; sudo systemctl is-active docker
echo "=== 镜像加速 ==="; cat /etc/docker/daemon.json
echo "=== 80 占用 ==="; sudo ss -lntp | grep ':80' || echo "80 空闲"
echo "=== 代码 ==="; ls /opt/asset/docker-compose.yml 2>/dev/null && echo 已有 || echo 没有
```

---

## 四、故障排查

1. **某服务非 Up** → `sudo docker compose logs <服务名>` 看报错。
2. **后端起不来** → 多半 `.env` 缺项；`cat .env` 核对 5 行齐全。
3. **`:80` 被占用** → `sudo ss -lntp | grep ':80'` 找出进程（常见是系统 nginx），停掉：`sudo systemctl stop nginx`。
4. **浏览器打不开** → 先在服务器 `curl http://<公网IP>/api/auth/captcha`：通说明容器/反代正常、问题在安全组；不通再查本机 80 监听与容器日志。
5. **首次建表报错** → 清空数据重来：`sudo docker compose down -v`（会删除 `asset-data` 卷，数据丢失，仅首次调试用）。

---

## 五、其他分支（SQL Server）

`sqlserver-docker` 分支自带 `db` 容器，`.env` 改为：
```
Jwt__SecretKey=<随机串>
DbType=sqlserver
AssetConnection=Server=db,1433;Database=AssetDB;User Id=sa;Password=<强密码>;TrustServerCertificate=True;
DbSaPassword=<与上面一致的强密码>
AdminSeed__Password=<强密码>
Redis__Connection=redis:6379
```
启动命令相同（`sudo docker compose up -d --build`，compose 已含 `db` 服务）。
⚠️ SQL Server 至少需要 2GB 内存，不适合本 2GB 小机；该分支适合本地或内存充足的机器。

---

## 六、安全与上线核对

- [ ] `Jwt__SecretKey` ≥ 32 字节且稳定（重启不变）
- [ ] 登录后立即在后台修改管理员初始密码
- [ ] 定期备份数据：直接拷贝 `asset-data` 卷里的 `asset.db` 文件
- [ ] 公网仅暴露 80；如需 HTTPS，在宿主机外层做 TLS 终止，再把 `Program.cs` 中注释掉的 `UseHttpsRedirection` 启用
- [ ] 表结构稳定后，将 compose 环境变量 `DatabaseInitialization__Enabled` 改为 `"false"`
- [ ] 应用层已内置：BCrypt 密码哈希、JWT 完整校验、登录防爆破、速率限制、安全响应头（HSTS 仅在 HTTPS 下下发）、Swagger 仅开发环境开放
