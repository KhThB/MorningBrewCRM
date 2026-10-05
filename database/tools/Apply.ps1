param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,60}$')][string]$Database = 'MorningBrewCRM',
    [switch]$NoSeed
)
$ErrorActionPreference = 'Stop'
$dbRoot = Split-Path $PSScriptRoot -Parent
$dataDir = Join-Path $dbRoot '.local'
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
$mdf = (Join-Path $dataDir "$Database.mdf").Replace("'", "''")
$ldf = (Join-Path $dataDir "${Database}_log.ldf").Replace("'", "''")
$create = "IF DB_ID(N'$Database') IS NOT NULL THROW 51000, N'Database already exists. Choose a new name; this installer never overwrites data.', 1; CREATE DATABASE [$Database] ON PRIMARY (NAME=N'$Database', FILENAME=N'$mdf') LOG ON (NAME=N'${Database}_log', FILENAME=N'$ldf'); ALTER DATABASE [$Database] SET COMPATIBILITY_LEVEL=160;"
& sqlcmd -S $Server -E -C -b -Q $create
if ($LASTEXITCODE -ne 0) { throw 'Database creation refused or failed.' }
$files = @('01_schema.sql','02_rules.sql','03_orders.sql','04_crm.sql','05_access.sql')
if (-not $NoSeed) { $files += @('06_seed.sql','07_demo.sql') }
$files += '08_admin.sql'
foreach ($name in $files) {
    Write-Host "Applying $name"
    & sqlcmd -S $Server -d $Database -E -C -I -b -f 65001 -i (Join-Path $dbRoot "sql/$name")
    if ($LASTEXITCODE -ne 0) { throw "Failed: $name. Existing data was not deleted. Inspect this new database before retrying." }
}
& sqlcmd -S $Server -d $Database -E -C -b -Q 'SET NOCOUNT ON; SELECT COUNT(*) AS TableCount FROM sys.tables;'
if ($LASTEXITCODE -ne 0) { throw 'Final connection check failed.' }
Write-Host "Database ready: $Database on $Server"
