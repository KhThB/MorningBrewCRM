# Existing SINGLE-STORE databases only. A full backup is required before applying updates.
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]{0,60}$')][string]$Database
)
$ErrorActionPreference = 'Stop'
$dbRoot = Split-Path $PSScriptRoot -Parent
$files = @('03_crm_updates.sql', '04_menu_updates.sql')
Get-Command sqlcmd -ErrorAction Stop | Out-Null
foreach ($name in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $dbRoot "sql/$name") -PathType Leaf)) {
        throw "Missing SQL file: $name. Upgrade not started."
    }
}
$preflight = @'
SET NOCOUNT ON;
IF OBJECT_ID('dbo.CreateOrder','P') IS NULL OR OBJECT_ID('dbo.GetMenu','P') IS NULL
 THROW 51000,'Core schema/procedures missing. Upgrade not started.',1;
IF (SELECT COUNT(*) FROM dbo.Branches WHERE IsActive=1)>1
 THROW 51000,'This upgrade supports the single-store CRM only.',1;
IF EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE r.Code='AreaManager')
 THROW 51000,'Resolve AreaManager accounts before the single-store upgrade.',1;
'@
& sqlcmd -S $Server -d $Database -E -C -I -b -Q $preflight
if ($LASTEXITCODE -ne 0) { throw 'Preflight failed. Upgrade not started.' }
$backupDir = Join-Path $dbRoot '.local'
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
$backupName = '{0}-before-upgrade-{1}-{2}.bak' -f $Database, (Get-Date -Format 'yyyyMMdd-HHmmss'), ([guid]::NewGuid().ToString('N'))
$backupFile = Join-Path $backupDir $backupName
$sqlPath = $backupFile.Replace("'", "''")
& sqlcmd -S $Server -d master -E -C -b -Q "BACKUP DATABASE [$Database] TO DISK=N'$sqlPath' WITH COPY_ONLY, CHECKSUM; RESTORE VERIFYONLY FROM DISK=N'$sqlPath' WITH CHECKSUM;"
if ($LASTEXITCODE -ne 0) { throw 'Backup or backup verification failed. Upgrade not started.' }
Write-Host "Backup: $backupFile"
foreach ($name in $files) {
    & sqlcmd -S $Server -d $Database -E -C -I -b -f 65001 -i (Join-Path $dbRoot "sql/$name")
    if ($LASTEXITCODE -ne 0) { throw "Failed: $name. Stop and inspect the database. No automatic restore. Backup: $backupFile" }
}
Write-Host "Upgrade complete: $Database. Backup: $backupFile"
