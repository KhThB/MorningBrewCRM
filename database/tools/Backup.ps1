<#
 Sao lưu database MorningBrew (yêu cầu III.1).
 - Bản FULL có CHECKSUM, kiểm tra lại bằng RESTORE VERIFYONLY.
 - Giữ lại $Keep bản mới nhất trong thư mục, xóa bản cũ hơn.
 Ví dụ:
   ./database/tools/Backup.ps1 -Database MorningBrewCRM_Crm
   ./database/tools/Backup.ps1 -Database MorningBrewCRM_Crm -Directory D:\Backups\MorningBrew -Keep 30
 Lên lịch hằng ngày (Task Scheduler, 23:00):
   schtasks /Create /SC DAILY /ST 23:00 /TN "MorningBrew Backup" /TR "powershell -File <repo>\database\tools\Backup.ps1 -Database MorningBrewCRM_Crm"
#>
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,60}$')][string]$Database = 'MorningBrewCRM',
    [string]$Directory = (Join-Path (Split-Path $PSScriptRoot -Parent) '.local\backups'),
    [ValidateRange(1,365)][int]$Keep = 14
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $Directory -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$file = Join-Path (Resolve-Path $Directory) "${Database}_FULL_$stamp.bak"
$path = $file.Replace("'", "''")
$sql = "BACKUP DATABASE [$Database] TO DISK=N'$path' WITH CHECKSUM, INIT, NAME=N'$Database full $stamp'; RESTORE VERIFYONLY FROM DISK=N'$path' WITH CHECKSUM;"
& sqlcmd -S $Server -E -C -b -Q $sql
if ($LASTEXITCODE -ne 0) { throw "Sao lưu thất bại: $Database" }
Write-Host "Đã sao lưu và kiểm tra: $file"
Get-ChildItem $Directory -Filter "${Database}_*.bak" | Sort-Object LastWriteTime -Descending | Select-Object -Skip $Keep |
    ForEach-Object { Write-Host "Xóa bản cũ: $($_.Name)"; Remove-Item $_.FullName }
