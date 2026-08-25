$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$installer = Get-Content (Join-Path $root 'Installer\ONHARU.iss') -Raw -Encoding UTF8
$release = Get-Content (Join-Path $root 'build-release.ps1') -Raw -Encoding UTF8
$readme = Get-Content (Join-Path $root 'README.md') -Raw -Encoding UTF8

foreach ($token in @('#define AppVersion "2.2.3"', 'ONHARU-2.2.3-Setup', 'Release\ONHARU-2.2.3', 'C43E8BF2-2B16-4CC7-A85B-D18C2AA7D706')) {
    if (-not $installer.Contains($token)) { throw "Installer 2.2 token is missing: $token" }
}
foreach ($token in @("'ONHARU-2.2.3'", 'ONHARU-2.2.3-Setup.exe', 'SHA256SUMS.txt')) {
    if (-not $release.Contains($token)) { throw "Release build token is missing: $token" }
}
if (-not $readme.Contains('ver. 2.2.3')) { throw 'README product version is stale.' }
if ($installer.Contains('ONHARU-2.0.0') -or $release.Contains('ONHARU-2.0.0')) { throw 'Active release configuration still targets 2.0.' }
if (-not $installer.Contains('ONHARU.exe.config') -or -not $release.Contains('ONHARU.exe.config')) {
    throw 'PMv2 WPF 설정 파일이 설치 또는 릴리스 패키지에서 누락됐습니다.'
}
Write-Host 'ONHARU 2.2 release configuration checks passed.'
