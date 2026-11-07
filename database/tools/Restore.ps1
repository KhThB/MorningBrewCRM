<#
 Phục hồi database MorningBrew từ file .bak (yêu cầu III.1).
 Mặc định phục hồi sang database MỚI (không đụng database đang chạy). Ghi đè database đang có phải thêm -Replace.
 Ví dụ:
   ./database/tools/Restore.ps1 -BackupFile database\.local\backups\MorningBrewCRM_Crm_FULL_20261107_230000.bak -Database MorningBrewCRM_Restore
   ./database/tools/Restore.ps1 -BackupFile <file.bak> -Database MorningBrewCRM_Crm -Replace     # dừng API trước khi chạy
#>
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [Parameter(Mandatory)][string]$BackupFile,
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,60}$')][string]$Database = 'MorningBrewCRM_Restore',
    [switch]$Replace
)
$ErrorActionPreference = 'Stop'
$bak = (Resolve-Path $BackupFile).Path.Replace("'", "''")
$dataDir = Join-Path (Split-Path $PSScriptRoot -Parent) '.local'
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null

& sqlcmd -S $Server -E -C -b -Q "RESTORE VERIFYONLY FROM DISK=N'$bak' WITH CHECKSUM;"
if ($LASTEXITCODE -ne 0) { throw 'File sao lưu hỏng hoặc không đọc được.' }

$exists = (& sqlcmd -S $Server -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$Database') IS NULL THEN 0 ELSE 1 END") | Select-Object -First 1
if ($exists.Trim() -eq '1' -and -not $Replace) { throw "Database $Database đã tồn tại. Thêm -Replace để ghi đè (dừng API trước)." }

# Tên logic của file dữ liệu/log trong bản sao lưu.
$list = & sqlcmd -S $Server -E -C -b -h -1 -W -s '|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK=N'$bak';"
$moves = @()
foreach ($line in $list) {
    $cols = $line.Split('|'); if ($cols.Count -lt 3) { continue }
    $logical = $cols[0].Trim(); $type = $cols[2].Trim()
    $target = if ($type -eq 'L') { Join-Path $dataDir "${Database}_log.ldf" } else { Join-Path $dataDir "$Database.mdf" }
    $moves += "MOVE N'$($logical.Replace("'", "''"))' TO N'$($target.Replace("'", "''"))'"
}
if ($moves.Count -lt 2) { throw 'Không đọc được danh sách file trong bản sao lưu.' }
$single = if ($exists.Trim() -eq '1') { "ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;" } else { '' }
$sql = "$single RESTORE DATABASE [$Database] FROM DISK=N'$bak' WITH $($moves -join ', '), REPLACE, CHECKSUM, RECOVERY; ALTER DATABASE [$Database] SET MULTI_USER;"
& sqlcmd -S $Server -E -C -b -Q $sql
if ($LASTEXITCODE -ne 0) { throw 'Phục hồi thất bại.' }
& sqlcmd -S $Server -d $Database -E -C -b -Q "SET NOCOUNT ON; SELECT COUNT(*) AS Users FROM dbo.Users; SELECT COUNT(*) AS Customers FROM dbo.Customers;"
Write-Host "Đã phục hồi $Database từ $BackupFile"
