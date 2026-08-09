$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$source = Join-Path $root 'Source'
$release = Join-Path $root 'Release'
$installer = Join-Path $root 'Installer'
$exe = Join-Path $release 'ONHARU.exe'
$setup = Join-Path $installer 'Output\ONHARU-Setup-1.0.0.exe'

New-Item -ItemType Directory -Force -Path $release | Out-Null
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $source 'build.ps1') '..\Release\ONHARU.exe'
if ($LASTEXITCODE -ne 0) { throw 'ONHARU EXE 빌드에 실패했습니다.' }

foreach ($check in @('version-check.ps1', 'oauth-check.ps1', 'recurrence-check.ps1', 'error-log-check.ps1', 'export-check.ps1', 'multi-day-check.ps1')) {
    if ($check -eq 'oauth-check.ps1') { & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $source $check) }
    else { & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $source $check) $exe }
    if ($LASTEXITCODE -ne 0) { throw "배포 검사 실패: $check" }
}

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $installer 'installer-check.ps1')
if ($LASTEXITCODE -ne 0) { throw '설치 설정 검사에 실패했습니다.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $installer 'build-installer.ps1')
if ($LASTEXITCODE -ne 0) { throw '설치 파일 빌드에 실패했습니다.' }

$hashes = @($exe, $setup) | ForEach-Object {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_
    '{0}  {1}' -f $hash.Hash, [IO.Path]::GetFileName($_)
}
Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Value $hashes -Encoding ASCII
Write-Host "Release EXE: $exe"
Write-Host "Installer: $setup"
