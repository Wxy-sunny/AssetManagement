# 演示当天一键启动：拉起 Docker 栈（含 SQL Server）+ 开 cloudflared 隧道，并打印访问 URL
# 用法（PowerShell，建议管理员运行）：  .\start-demo.ps1
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $repo

Write-Host '==> 启动 Docker 栈（含 SQL Server 容器）...' -ForegroundColor Cyan
docker compose up -d --profile db

Write-Host '==> 等待 SQL Server 就绪（约 30s）...' -ForegroundColor Cyan
Start-Sleep -Seconds 30
docker compose ps

# 启动 cloudflared 隧道：前台运行并打印 URL，关闭此窗口即停止隧道
if (Get-Command cloudflared -ErrorAction SilentlyContinue) {
    Write-Host '==> 打开 cloudflared 隧道（http://localhost:8080）...' -ForegroundColor Cyan
    Write-Host '    下方 trycloudflare.com 的 URL 即对外访问地址，发给要看的人即可。' -ForegroundColor Yellow
    cloudflared tunnel --url http://localhost:8080
} else {
    Write-Host '!! 未检测到 cloudflared。请先安装：winget install Cloudflare.cloudflared' -ForegroundColor Red
    Write-Host '   然后手动运行：cloudflared tunnel --url http://localhost:8080' -ForegroundColor Yellow
}
