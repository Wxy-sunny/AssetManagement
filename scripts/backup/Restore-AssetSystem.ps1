<#
.SYNOPSIS
    资产管理系统还原脚本：从备份目录还原 SQL Server 数据库与上传附件。
.DESCRIPTION
    警告：数据库还原会覆盖现有数据，执行前请确认备份目录正确。
    用法示例：
      .\Restore-AssetSystem.ps1 -BackupFolder D:\Backups\AssetManagement\20261022-020000 -RestoreUploads
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BackupFolder,
    [string]$Server = "localhost",
    [string]$Database = "AssetDB",
    [string]$User = "demo",
    [string]$Password = "123456789",
    [string]$UploadsPath = "",
    [switch]$RestoreUploads
)

if (-not (Test-Path $BackupFolder)) { throw "备份目录不存在：$BackupFolder" }
$ErrorActionPreference = "Stop"

$bakFile = Get-ChildItem $BackupFolder -Filter "*.bak" | Select-Object -First 1
if (-not $bakFile) { throw "备份目录中未找到 .bak 文件：$BackupFolder" }

Write-Host "将还原数据库 [$Database] <= $($bakFile.FullName)" -ForegroundColor Yellow
Write-Host "警告：还原会覆盖现有数据库，并断开所有活动连接！" -ForegroundColor Red
$confirm = Read-Host "确认继续？(yes/no)"
if ($confirm -ne "yes") { Write-Host "已取消"; exit 0 }

# 1. 断开活动连接并还原数据库
Write-Host "正在还原数据库 ..."
& sqlcmd -S $Server -U $User -P $Password -b -Q "ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$Database] FROM DISK = N'$($bakFile.FullName)' WITH REPLACE; ALTER DATABASE [$Database] SET MULTI_USER"
if ($LASTEXITCODE -ne 0) { throw "数据库还原失败（sqlcmd 退出码 $LASTEXITCODE）" }
Write-Host "数据库还原完成" -ForegroundColor Green

# 2. 还原附件目录（可选）
if ($RestoreUploads) {
    if ([string]::IsNullOrWhiteSpace($UploadsPath)) {
        $UploadsPath = Join-Path $PSScriptRoot "..\..\AssetManagementSystem\AssetManagementSystem\wwwroot\uploads"
    }
    $zip = Join-Path $BackupFolder "uploads.zip"
    $dir = Join-Path $BackupFolder "uploads"
    if (Test-Path $zip) {
        Expand-Archive -Path $zip -DestinationPath $UploadsPath -Force
        Write-Host "附件已还原至 $UploadsPath" -ForegroundColor Green
    } elseif (Test-Path $dir) {
        Copy-Item -Path $dir -Destination $UploadsPath -Recurse -Force
        Write-Host "附件已还原至 $UploadsPath" -ForegroundColor Green
    } else {
        Write-Warning "备份中无附件数据（uploads.zip / uploads 均不存在）"
    }
} else {
    Write-Host "（未指定 -RestoreUploads，跳过附件还原）"
}
