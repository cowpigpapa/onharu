$ErrorActionPreference = 'Stop'
$script = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'ONHARU.iss')
$source = Join-Path $PSScriptRoot '..\Release\ONHARU.exe'

if (-not (Test-Path -LiteralPath $source)) { throw '배포용 ONHARU EXE가 없습니다.' }
foreach ($required in @('AppId={{C43E8BF2-2B16-4CC7-A85B-D18C2AA7D706}', 'PrivilegesRequired=lowest', 'VersionInfoVersion=1.2.1.0', 'Release\ONHARU.exe', 'DestName: "{#AppExeName}"', '{userstartup}', 'Tasks: desktopicon', 'UninstallDisplayIcon')) {
    if (-not $script.Contains($required)) { throw "설치 설정 누락: $required" }
}
Write-Host 'ONHARU installer configuration checks passed.'
