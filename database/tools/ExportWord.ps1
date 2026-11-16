param(
    [string]$InputPath = 'database/docs/MorningBrewCRM_Thiet_ke_database_cap_nhat.docx',
    [string]$OutputPath = 'database/qa/word/MorningBrewCRM.pdf'
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $InputPath).Path
$destination = [IO.Path]::GetFullPath((Join-Path $PWD $OutputPath))
New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
$word = $null
$document = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $document = $word.Documents.Open($source, $false, $true)
    $document.Repaginate()
    $document.ExportAsFixedFormat($destination, 17)
    Write-Output "Rendered through installed Office automation: $destination"
} finally {
    if ($null -ne $document) {
        $document.Close(0)
        [Runtime.InteropServices.Marshal]::ReleaseComObject($document) | Out-Null
    }
    if ($null -ne $word) {
        try { $word.Quit() } catch [Runtime.InteropServices.COMException] {
            if ($_.Exception.HResult -ne -2147023174) { throw }
        }
        try { [Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null } catch {}
    }
}
