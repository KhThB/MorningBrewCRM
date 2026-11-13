param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,60}$')][string]$Database = 'MorningBrewCRM_Crm',
    [switch]$NoSeed
)
$ErrorActionPreference = 'Stop'
$dbRoot = Split-Path $PSScriptRoot -Parent
$files = @('01_core.sql')
if (-not $NoSeed) { $files += '02_demo_data.sql' }
$files += @('03_crm_updates.sql', '04_menu_updates.sql')
Get-Command sqlcmd -ErrorAction Stop | Out-Null
foreach ($name in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $dbRoot "sql/$name") -PathType Leaf)) {
        throw "Missing SQL file: $name. Database creation not started."
    }
}
$dataDir = Join-Path $dbRoot '.local'
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
$mdf = (Join-Path $dataDir "$Database.mdf").Replace("'", "''")
$ldf = (Join-Path $dataDir "${Database}_log.ldf").Replace("'", "''")
$create = "IF DB_ID(N'$Database') IS NOT NULL THROW 51000, N'Database already exists. Choose a new name; this installer never overwrites data.', 1; CREATE DATABASE [$Database] ON PRIMARY (NAME=N'$Database', FILENAME=N'$mdf') LOG ON (NAME=N'${Database}_log', FILENAME=N'$ldf'); ALTER DATABASE [$Database] SET COMPATIBILITY_LEVEL=160;"
& sqlcmd -S $Server -E -C -b -Q $create
if ($LASTEXITCODE -ne 0) { throw 'Database creation refused or failed.' }
foreach ($name in $files) {
    Write-Host "Applying $name"
    & sqlcmd -S $Server -d $Database -E -C -I -b -f 65001 -i (Join-Path $dbRoot "sql/$name")
    if ($LASTEXITCODE -ne 0) { throw "Failed: $name. Inspect this new database before retrying; no automatic deletion or rollback across files." }
}
& sqlcmd -S $Server -d $Database -E -C -b -Q 'SET NOCOUNT ON; SELECT COUNT(*) AS TableCount FROM sys.tables;'
if ($LASTEXITCODE -ne 0) { throw 'Final connection check failed.' }
Write-Host "Database ready: $Database on $Server"
