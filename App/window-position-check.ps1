param([string]$Exe = '..\Tests\LocalTest\ONHARU-2.1-local-test.exe')
$ErrorActionPreference = 'Stop'
$exePath = if ([IO.Path]::IsPathRooted($Exe)) { $Exe } else { Join-Path $PSScriptRoot $Exe }
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $exePath).Path)
$type = $assembly.GetType('FamilyPlanner.MainWindow', $true)
$method = $type.GetMethod('FitWindowToScreens', [Reflection.BindingFlags]'Public,Static')
$primary = New-Object Windows.Rect 0, 0, 1366, 728
$secondary = New-Object Windows.Rect 1366, 0, 1920, 1080
$areas = [Windows.Rect[]]@($primary, $secondary)

$offscreen = New-Object Windows.Rect 4000, 100, 1120, 700
$recovered = $method.Invoke($null, @($offscreen, $areas, $false))
if ($recovered.Left -lt 0 -or $recovered.Right -gt 1366 -or $recovered.Top -lt 0 -or $recovered.Bottom -gt 728) {
    throw "화면 밖 창을 주 모니터로 복귀하지 못했습니다: $recovered"
}

$visible = New-Object Windows.Rect 1450, 80, 1120, 700
$unchanged = $method.Invoke($null, @($visible, $areas, $false))
if ($unchanged -ne $visible) { throw '현재 연결된 보조 모니터의 창 위치를 불필요하게 변경했습니다.' }

$clipped = New-Object Windows.Rect 2200, 5, 1090, 1000
$fitted = $method.Invoke($null, @($clipped, $areas, $false))
if ($fitted.Right -gt $secondary.Right -or $fitted.Bottom -gt $secondary.Bottom) { throw "부분적으로 잘린 창을 모니터 안에 맞추지 못했습니다: $fitted" }

$forced = $method.Invoke($null, @($visible, $areas, $true))
if ($forced.Left -lt 0 -or $forced.Right -gt 1366) { throw '트레이 복귀가 주 모니터로 이동하지 못했습니다.' }

$mainSource = (Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'MainWindow*.cs' | Sort-Object Name |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 }) -join "`n"
$programSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Program.cs') -Raw -Encoding UTF8
$publisherSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ExplorerFramePublisher.cs') -Raw -Encoding UTF8
foreach ($physicalPlacementFeature in @('SavePhysicalPlacement()', 'RestorePhysicalPlacement()', 'MonitorDeviceName', 'PhysicalWidth', 'GetWindowRect(', 'SetWindowPos(')) {
    if (-not $mainSource.Contains($physicalPlacementFeature) -and -not (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PlannerSettings.cs') -Raw).Contains($physicalPlacementFeature)) {
        throw "혼합 DPI 물리 좌표 저장·복원이 누락됐습니다: $physicalPlacementFeature"
    }
}
foreach ($dpiPlacementFeature in @('AttachDpiPlacement()', 'ApplyPhysicalMinimums(', 'WM_DPICHANGED', 'DPI_RESTORE')) {
    if (-not $mainSource.Contains($dpiPlacementFeature)) { throw "PMv2 물리 위치 복원이 누락됐습니다: $dpiPlacementFeature" }
}
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'app.manifest') -Raw -Encoding UTF8
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'app.config') -Raw -Encoding UTF8
if (-not $manifest.Contains('PerMonitorV2, PerMonitor')) { throw 'PerMonitorV2 manifest가 누락됐습니다.' }
if (-not $config.Contains('Switch.System.Windows.DoNotScaleForDpiChanges=false')) { throw 'WPF DPI 변경 스위치가 누락됐습니다.' }
foreach ($dpiPositionFeature in @('DesktopClientPoint(screenPoint)', 'ScreenToClient(list, ref point)', 'Left = panelOrigin.X', 'Top = panelOrigin.Y')) {
    if (-not $publisherSource.Contains($dpiPositionFeature)) { throw "Explorer 고정 프레임의 DPI 안전 좌표 변환이 누락됐습니다: $dpiPositionFeature" }
}
if ($programSource.Contains('LayerHostController.Start(')) { throw 'LayerHost가 첫 프레임보다 먼저 시작됩니다.' }
$publishIndex = $mainSource.IndexOf('explorerFrame.Publish(this')
$hostStartIndex = $mainSource.IndexOf('LayerHostController.Start()')
if ($publishIndex -lt 0 -or $hostStartIndex -lt 0 -or $publishIndex -gt $hostStartIndex) { throw 'LayerHost 시작이 첫 프레임 게시보다 빠릅니다.' }

Write-Host 'ONHARU window position recovery checks passed.'
