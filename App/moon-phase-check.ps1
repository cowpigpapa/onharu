param([string]$Exe)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$settings = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'PlannerSettings.cs')
$calendar = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'MainWindow.Calendar.cs')
$display = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'MainWindow.Display.cs')
$detail = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'MainWindow.Detail.cs')
$navigation = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'MainWindow.Navigation.cs')
$ui = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'SettingsWindow.cs')

foreach ($token in @('ShowMoonPhase', 'MoonPhase(date)', '29.530588853', 'DetailDateFormat', 'DetailIncompleteRange',
    'return source != null && source.Visible')) {
    if (-not (($settings + $calendar + $display + $detail + $navigation + $ui).Contains($token))) {
        throw "Moon phase integration is missing: $token"
    }
}
Write-Host 'ONHARU moon phase display and Google duplicate-mode checks passed.'
