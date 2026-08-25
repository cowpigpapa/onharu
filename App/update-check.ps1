$ErrorActionPreference = 'Stop'
$service = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'UpdateService.cs'))
$flow = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'MainWindow.Updates.cs'))
$startup = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'MainWindow.Startup.cs'))
$google = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'MainWindow.Google.cs'))
foreach ($required in @('releases/latest', 'SHA256SUMS.txt', 'DownloadVerifiedInstallerAsync', 'SHA256.Create')) {
    if (-not $service.Contains($required)) { throw "Update boundary is missing: $required" }
}
if (-not $service.Contains('SecurityProtocolType.Tls12')) { throw 'GitHub update checks must explicitly enable TLS 1.2 for .NET Framework clients.' }
if ($startup.IndexOf('CheckForUpdatesAsync(false)', [StringComparison]::Ordinal) -gt $startup.IndexOf('SyncGoogle(false)', [StringComparison]::Ordinal)) {
    throw 'Startup update check must run before Google synchronization.'
}
foreach ($required in @('IsGoogleAuthenticationError', 'invalid_grant', 'GoogleConnectFailed')) {
    if (-not $google.Contains($required)) { throw "Google authentication recovery is missing: $required" }
}
if (-not $flow.Contains('TimeSpan.FromHours(24)')) { throw 'Daily update check throttle is missing.' }
if (-not $flow.Contains('UpdateAvailableWindow')) { throw 'User-approved update prompt is missing.' }
Write-Host 'ONHARU update boundary checks passed.'
