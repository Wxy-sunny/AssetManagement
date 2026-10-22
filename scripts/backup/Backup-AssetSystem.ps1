<#
.SYNOPSIS
    资产管理系统备份脚本：备份 SQL Server 数据库 + 上传附件目录。
.DESCRIPTION
    备份内容：
      1) 数据库完整备份（.bak，通过 sqlcmd 执行 BACKUP DATABASE）
      2) wwwroot/uploads 附件目录（默认复制，-Compress 时打包为 zip）
    保留策略：默认保留最近 14 天的备份目录，更早的自动删除。
    用法示例：
      .\Backup-AssetSystem.ps1 -BackupRoot D:\Backups\AssetManagement
      .\Backup-AssetSystem.ps1 -Server 10.0.0.5 -Database AssetDB -User sa -Password 'xxx' -Compress
    计划任务（每天 02:00 全量备份）：
      schtasks /Create /TN "AssetSystem-Backup" /SC DAILY /ST 02:00 /TR "powershell -File D:\Backups\Backup-AssetSystem.ps1 -BackupRoot D:\Backups\AssetManagement"
.NOTES
    前提：执行机需安装 sqlcmd（随 SQL Server / SSMS 提供），
    且所用登录账号对数据库具有 BACKUP 权限（db_backupoperator 或 sysadmin）。
#>
[CmdletBinding()]
param(
    [string]$Server = "localhost",
    [string]$Database = "AssetDB",
    [string]$User = "demo",
    [string]$Password = "123456789",
    [string]$BackupRoot = "D:\Backups\AssetManagement",
    [string]$UploadsPath = "",
    [int]$RetainDays = 14,
    [switch]$Compress
)

$ErrorActionPreference = "Stop"

# 未显式指定时，按仓库相对位置推断后端 wwwroot/uploads
if ([string]::IsNullOrWhiteSpace($UploadsPath)) {
    $UploadsPath = Join-Path $PSScriptRoot "..\..\AssetManagementSystem\AssetManagementSystem\wwwroot\uploads"
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$targetDir = Join-Path $BackupRoot $stamp

function Write-Step($msg) { Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $msg" -ForegroundColor Cyan }

try {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Step "备份目录：$targetDir"

    # ---------- 1. 数据库完整备份 ----------
    $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if (-not $sqlcmd) {
        throw "未找到 sqlcmd，请先安装 SQL Server 客户端工具（或改用 SSMS 维护计划）"
    }
    $bakFile = Join-Path $targetDir "$Database.bak"
    Write-Step "正在备份数据库 [$Database] ..."
    & sqlcmd -S $Server -U $User -P $Password -b -Q "BACKUP DATABASE [$Database] TO DISK = N'$bakFile' WITH FORMAT, INIT, NAME = N'$Database-Full-$stamp', COMPRESSION"
    if ($LASTEXITCODE -ne 0) { throw "数据库备份失败（sqlcmd 退出码 $LASTEXITCODE）" }
    Write-Step "数据库备份完成：$bakFile ($([math]::Round((Get-Item $bakFile).Length/1MB,2)) MB)"

    # ---------- 2. 附件目录备份 ----------
    if (Test-Path $UploadsPath) {
        if ($Compress) {
            $zipFile = Join-Path $targetDir "uploads.zip"
            Write-Step "正在压缩附件目录 ..."
            # 使用 .NET 原生 API（Compress-Archive 在 Windows PowerShell 5.1 下对空目录不兼容）
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::CreateFromDirectory($UploadsPath, $zipFile)
            Write-Step "附件压缩完成：$zipFile ($([math]::Round((Get-Item $zipFile).Length/1MB,2)) MB)"
        } else {
            $destUploads = Join-Path $targetDir "uploads"
            Write-Step "正在复制附件目录 ..."
            Copy-Item -Path $UploadsPath -Destination $destUploads -Recurse -Force
            $size = (Get-ChildItem $destUploads -Recurse | Measure-Object Length -Sum).Sum
            Write-Step "附件复制完成：$destUploads ($([math]::Round($size/1MB,2)) MB)"
        }
    } else {
        Write-Warning "附件目录不存在（$UploadsPath），跳过附件备份"
    }

    # ---------- 3. 保留策略：清理过期备份 ----------
    # 仅清理由本脚本生成的"时间戳目录"（yyyyMMdd-HHmmss），避免误删其他内容
    $cutoff = (Get-Date).AddDays(-$RetainDays)
    $removed = Get-ChildItem $BackupRoot -Directory | Where-Object {
        $_.Name -match '^\d{8}-\d{6}$' -and $_.CreationTime -lt $cutoff
    }
    foreach ($d in $removed) {
        Remove-Item $d.FullName -Recurse -Force
        Write-Step "已清理过期备份：$($d.Name)"
    }

    # ---------- 4. 摘要 ----------
    $total = (Get-ChildItem $targetDir -Recurse | Measure-Object Length -Sum).Sum
    Write-Step "备份完成，总大小 $([math]::Round($total/1MB,2)) MB，保留最近 $RetainDays 天"
    Write-Host "备份位置：$targetDir" -ForegroundColor Green
}
catch {
    Write-Error "备份失败：$($_.Exception.Message)"
    exit 1
}
