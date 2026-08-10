param([string]$Exe = '..\Release\ONHARU.exe')
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe).Path)
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

$forced = $method.Invoke($null, @($visible, $areas, $true))
if ($forced.Left -lt 0 -or $forced.Right -gt 1366) { throw '트레이 복귀가 주 모니터로 이동하지 못했습니다.' }

Write-Host 'ONHARU window position recovery checks passed.'
