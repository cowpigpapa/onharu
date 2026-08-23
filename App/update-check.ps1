$ErrorActionPreference = 'Stop'
$service = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'UpdateService.cs'))
$flow = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'MainWindow.Updates.cs'))
foreach ($required in @('releases/latest', 'SHA256SUMS.txt', 'DownloadVerifiedInstallerAsync', 'SHA256.Create')) {
    if (-not $service.Contains($required)) { throw "Update boundary is missing: $required" }
}
if (-not $flow.Contains('TimeSpan.FromHours(24)')) { throw 'Daily update check throttle is missing.' }
if (-not $flow.Contains('UpdateAvailableWindow')) { throw 'User-approved update prompt is missing.' }
Write-Host 'ONHARU update boundary checks passed.'
