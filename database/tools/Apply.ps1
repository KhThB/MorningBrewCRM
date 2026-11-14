# Apply.ps1 – cài mới (hoặc nâng cấp với -Upgrade) database MorningBrew CRM bằng CHÍNH file
# database/sql_ssms/morningbrew_fullscript.sql (01–14). [SQL-03] Trước đây chạy lần lượt 14 file lẻ trong database/sql;
# nay dùng chung một nguồn với SSMS để hai cách cài luôn ra cùng một kết quả.
#
#   ./database/tools/Apply.ps1                                  # tạo MorningBrewCRM_Crm mới (dữ liệu mẫu)
#   ./database/tools/Apply.ps1 -Database MorningBrewCRM_Test    # tên khác
#   ./database/tools/Apply.ps1 -NoSeed                          # không có dữ liệu mẫu (bỏ 06, 07)
#   ./database/tools/Apply.ps1 -Upgrade                         # database đang dùng: sao lưu .bak rồi chạy 08–14
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,60}$')][string]$Database = 'MorningBrewCRM_Crm',
    [switch]$NoSeed,
    [switch]$Upgrade
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8   # đọc đúng tiếng Việt do sqlcmd -f 65001 in ra
$dbRoot = Split-Path $PSScriptRoot -Parent
$full = Join-Path $dbRoot 'sql_ssms/morningbrew_fullscript.sql'
if (-not (Test-Path $full)) { throw "Không tìm thấy $full" }
$dataDir = Join-Path $dbRoot '.local'
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null

# 1. Database đã có?
$exists = & sqlcmd -S $Server -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$Database') IS NULL THEN 0 ELSE 1 END;" | Select-Object -First 1
if ($LASTEXITCODE -ne 0 -or $null -eq $exists) { throw "Không kết nối được SQL Server $Server." }
$exists = "$exists".Trim()
if ($exists -eq '1' -and -not $Upgrade) {
    throw "Database $Database đã tồn tại. Installer không ghi đè dữ liệu. Muốn nâng cấp (tự sao lưu rồi chạy 08–14) thì thêm -Upgrade."
}
if ($exists -eq '0' -and $Upgrade) { throw "Không có database $Database để nâng cấp. Bỏ -Upgrade để cài mới." }
if ($Upgrade -and $NoSeed) { throw '-NoSeed chỉ dùng khi cài mới.' }

# 2. Cài mới: tạo database với file MDF/LDF trong database/.local (ngoài Git), rồi fullscript thấy database trống → CÀI MỚI.
if ($exists -eq '0') {
    $mdf = (Join-Path $dataDir "$Database.mdf").Replace("'", "''")
    $ldf = (Join-Path $dataDir "${Database}_log.ldf").Replace("'", "''")
    & sqlcmd -S $Server -E -C -b -Q "CREATE DATABASE [$Database] ON PRIMARY (NAME=N'$Database', FILENAME=N'$mdf') LOG ON (NAME=N'${Database}_log', FILENAME=N'$ldf');"
    if ($LASTEXITCODE -ne 0) { throw 'Tạo database thất bại.' }
}

# 3. Chuẩn bị script: đổi tên database; -NoSeed thì bỏ đoạn 06_seed + 07_demo.
$sql = [IO.File]::ReadAllText($full, [Text.Encoding]::UTF8).Replace('MorningBrewCRM_Crm', $Database)
if ($NoSeed) {
    $a = $sql.IndexOf("PRINT N'--- 06_seed.sql ---';")
    $b = $sql.IndexOf('-- ===== Hết phần cài mới')
    if ($a -lt 0 -or $b -lt $a) { throw 'Không tìm thấy đoạn dữ liệu mẫu trong fullscript.' }
    $sql = $sql.Substring(0, $a) + $sql.Substring($b)
}
$tmp = Join-Path $dataDir "apply_$Database.sql"
[IO.File]::WriteAllText($tmp, $sql, (New-Object Text.UTF8Encoding($true)))

# 4. Chạy. Không dùng -b: khi nâng cấp, SQL Server "dịch thử" đoạn 01–07 bị bỏ qua và có thể in dòng đỏ không phải lỗi thật.
#    Kết quả thật do fullscript tự báo: "=== HOÀN TẤT" hoặc ">>> KẾT QUẢ: CHƯA HOÀN TẤT".
Write-Host "Đang chạy morningbrew_fullscript.sql trên [$Database] ($Server)..."
$ErrorActionPreference = 'Continue'   # sqlcmd có thể ghi lỗi ra stderr; không để PowerShell dừng giữa chừng
$out = & sqlcmd -S $Server -E -C -I -f 65001 -i $tmp 2>&1
$ErrorActionPreference = 'Stop'
$out | ForEach-Object { Write-Host $_ }
Remove-Item $tmp -ErrorAction SilentlyContinue
$text = ($out | Out-String)
# Kiểm tra lại trên database: đủ bảng của 01–14 (43 bảng, có bảng kho 14_inventory).
$check = & sqlcmd -S $Server -d $Database -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN (SELECT COUNT(*) FROM sys.tables) >= 43 AND OBJECT_ID(N'dbo.Ingredients') IS NOT NULL AND OBJECT_ID(N'dbo.CreateOrder') IS NOT NULL THEN 'OK' ELSE 'FAIL' END;" | Select-Object -First 1
# So khớp phần đầu chỉ có ký tự ASCII ('>>> K' = ">>> KẾT QUẢ: CHƯA HOÀN TẤT", '=== HO' = "=== HOÀN TẤT") để không phụ thuộc bảng mã console.
if ($text -match '>>> K' -or $text -notmatch '=== HO' -or "$check".Trim() -ne 'OK') {
    throw "Chưa hoàn tất trên $Database. Đọc dòng '>>> DỪNG' / lỗi đỏ đầu tiên ở trên. Dữ liệu có sẵn không bị xóa; nâng cấp lỗi thì phục hồi từ file .bak đã in ra."
}
Write-Host "Database ready: $Database on $Server"
