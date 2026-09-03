param([switch]$Build, [string]$Exe)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$exe = if ([string]::IsNullOrWhiteSpace($Exe)) { Join-Path $root 'Tests\LocalTest\ONHARU-2.2-local-test.exe' } else { [IO.Path]::GetFullPath((Join-Path $root $Exe)) }

if ($Build -or -not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $root 'build-local-test.ps1')
    $exe = Join-Path $root 'Tests\LocalTest\ONHARU-2.2-local-test.exe'
}

$checks = @(
    'version-check.ps1',
    'feature-pack-check.ps1',
    'migration-check.ps1',
    'search-check.ps1',
    'ui-construction-check.ps1',
    'recurrence-check.ps1',
    'multi-day-check.ps1',
    'moon-phase-check.ps1',
    'export-check.ps1',
    'sync-security-check.ps1',
    'window-position-check.ps1',
    'error-log-check.ps1',
    'oauth-check.ps1',
    'email-backup-check.ps1',
    'update-check.ps1',
    'theme-check.ps1',
    'popup-policy-check.ps1',
    'release-config-check.ps1'
)

# 2026-09-02: feature-pack-check와 theme-check만 `pwsh`(PowerShell 7)로 호출하고 있었다.
# 이 프로젝트의 기본 셸은 Windows PowerShell 5.1이고 개발 PC에 pwsh가 없어 게이트가 두 번째 항목에서
# 멈췄다. 두 파일은 UTF-8 BOM이 있어 5.1도 한국어 문자열을 그대로 읽으므로 pwsh가 필요하지 않다.
# 나머지 검사와 같이 같은 셸에서 실행하고, 스크립트마다 다른 인자 이름만 아래 표로 구분한다.
$exePathParameter = @('feature-pack-check.ps1', 'sync-security-check.ps1')
$noParameter = @('oauth-check.ps1', 'email-backup-check.ps1', 'update-check.ps1', 'release-config-check.ps1')

foreach ($name in $checks) {
    Write-Host "[CHECK] $name"
    $path = Join-Path $root "App\$name"
    if ($name -in $noParameter) { & $path }
    elseif ($name -in $exePathParameter) { & $path -ExePath $exe }
    else { & $path -Exe $exe }
}

$required = @(
    (Join-Path $root 'Tests\LocalTest\ONHARU-2.2-local-test.exe'),
    (Join-Path $root 'Tests\LocalTest\ONHARU-2.2-local-test.exe.config'),
    (Join-Path $root 'Tests\LocalTest\Onharu.LayerHost.exe'),
    (Join-Path $root 'Tests\LocalTest\Onharu.DesktopHook.dll')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing local-test artifact: $path" }
}

Write-Host 'ONHARU 2.2 quality checks passed.'
