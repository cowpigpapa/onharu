param([string]$Source = '.')
$ErrorActionPreference = 'Stop'
$layer = Get-Content -Raw -Encoding UTF8 (Join-Path $Source 'DesktopLayer.cs')
$main = Get-Content -Raw -Encoding UTF8 (Join-Path $Source 'MainWindow.cs')

foreach ($required in @('SHELLDLL_DefView', 'BackgroundWorker()', 'SetParent(handle, desktop)', 'HWND_BOTTOM')) {
    if (-not $layer.Contains($required)) { throw "Desktop layer requirement missing: $required" }
}
if (($main.Split('PlaceCalendarDialog(window)').Count - 1) -lt 2) { throw 'New and edit dialogs must both use the non-owning placement.' }
Write-Host 'ONHARU desktop icon and dialog layer checks passed.'
