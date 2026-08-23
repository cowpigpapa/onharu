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

foreach ($name in $checks) {
    Write-Host "[CHECK] $name"
    $path = Join-Path $root "App\$name"
    if ($name -eq 'feature-pack-check.ps1') {
        & pwsh -NoProfile -File $path -ExePath $exe
        if ($LASTEXITCODE -ne 0) { throw "$name failed with exit code $LASTEXITCODE." }
    }
    elseif ($name -in @('oauth-check.ps1', 'email-backup-check.ps1', 'update-check.ps1', 'release-config-check.ps1')) { & $path }
    elseif ($name -in @('feature-pack-check.ps1', 'sync-security-check.ps1')) { & $path -ExePath $exe }
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
