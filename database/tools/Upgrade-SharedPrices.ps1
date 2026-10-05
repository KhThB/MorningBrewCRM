param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,60}$')][string]$Database = 'MorningBrewCRM'
)
$ErrorActionPreference = 'Stop'
$dbRoot = Split-Path $PSScriptRoot -Parent
$backupFolder = Join-Path $dbRoot ('.local/shared-prices-before-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backupFolder | Out-Null
$backupFile = Join-Path $backupFolder 'restore-procedures.sql'
$backupSql = @("USE [$Database];", 'GO', 'SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;', 'GO')
foreach ($procedureName in @('CreateOrder','GetMenu','SetBranchPrice')) {
    $definition = & sqlcmd -S $Server -d $Database -E -C -I -b -f 65001 -y 0 -w 65535 -Q "SET NOCOUNT ON; SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.$procedureName'));"
    if ($LASTEXITCODE -ne 0) { throw "Could not back up $procedureName. Migration not started." }
    $body = ($definition -join "`n").Trim()
    $start = [regex]::Match($body, '(?im)^\s*(?:CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+PROCEDURE')
    if (-not $start.Success) { throw "Procedure $procedureName missing or unreadable. Migration not started." }
    $body = $body.Substring($start.Index)
    $body = $body -replace '(?is)^\s*(?:CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+PROCEDURE', 'CREATE OR ALTER PROCEDURE'
    $backupSql += $body
    $backupSql += 'GO'
}
$backupSql | Set-Content -LiteralPath $backupFile -Encoding utf8

# Migration only changes procedure definitions. Verify financial rows are untouched.
$snapshotQuery = @'
SET NOCOUNT ON;
SELECT COUNT_BIG(*),COALESCE(SUM(UnitPrice),0),COALESCE(SUM(LineTotal),0) FROM dbo.OrderDetails;
SELECT COUNT_BIG(*),COALESCE(SUM(CONVERT(bigint,Points)),0) FROM dbo.LoyaltyTransactions;
'@
$before = & sqlcmd -S $Server -d $Database -E -C -I -b -h -1 -W -Q $snapshotQuery
if ($LASTEXITCODE -ne 0) { throw 'Could not read pre-migration financial snapshot.' }
& sqlcmd -S $Server -d $Database -E -C -I -b -f 65001 -i (Join-Path $dbRoot 'sql/09_shared_prices.sql')
if ($LASTEXITCODE -ne 0) { throw "Migration failed. Original procedure definitions: $backupFile" }
$after = & sqlcmd -S $Server -d $Database -E -C -I -b -h -1 -W -Q $snapshotQuery
if ($LASTEXITCODE -ne 0) { throw 'Migration ran, but final financial snapshot could not be read.' }
if (($before -join "`n") -ne ($after -join "`n")) {
    throw "Financial data changed during migration; inspect concurrent activity. Backup: $backupFile"
}
Write-Host "Shared pricing enabled on $Database. Historical financial rows unchanged."
Write-Host "Original procedures saved to: $backupFile"
