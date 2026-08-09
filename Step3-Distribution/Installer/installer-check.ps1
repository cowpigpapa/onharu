$ErrorActionPreference = 'Stop'
$script = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'ONHARU.iss')
$source = Join-Path $PSScriptRoot '..\Release\ONHARU.exe'

if (-not (Test-Path -LiteralPath $source)) { throw '배포용 ONHARU EXE가 없습니다.' }
foreach ($required in @('PrivilegesRequired=lowest', 'VersionInfoVersion=1.0.0.0', 'Release\ONHARU.exe', 'DestName: "{#AppExeName}"', '{userstartup}', 'Tasks: desktopicon', 'UninstallDisplayIcon')) {
    if (-not $script.Contains($required)) { throw "설치 설정 누락: $required" }
}
Write-Host 'ONHARU installer configuration checks passed.'
