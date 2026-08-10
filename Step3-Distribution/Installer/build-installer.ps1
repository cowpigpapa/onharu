$ErrorActionPreference = 'Stop'
$compiler = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $compiler) {
    throw 'Inno Setup 6가 필요합니다. winget install --id JRSoftware.InnoSetup -e 명령으로 먼저 설치하세요.'
}

& $compiler (Join-Path $PSScriptRoot 'ONHARU.iss')
if ($LASTEXITCODE -ne 0) { throw 'ONHARU 설치 파일 생성에 실패했습니다.' }
Write-Host "Built: $(Join-Path $PSScriptRoot 'Output\ONHARU-Setup-1.1.1.exe')"
