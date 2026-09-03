$ErrorActionPreference = 'Stop'
$vcvars = 'C:\BuildTools\VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcvars)) { throw 'Visual C++ Build Tools are not installed.' }
# vcvars64.bat은 내부에서 vswhere.exe를 PATH에서 찾는다. Visual Studio 없이 Build Tools만
# 설치한 환경에서는 설치 폴더가 PATH에 없어 네이티브 빌드가 통째로 막힌다. 그 폴더를 앞에 붙인다.
$vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (-not (Test-Path -LiteralPath (Join-Path $vsInstaller 'vswhere.exe'))) { throw 'vswhere.exe was not found next to the Visual Studio installer.' }
$here = $PSScriptRoot
$sdk = 'C:\Program Files (x86)\Windows Kits\10'
$version = '10.0.26100.0'
$include = '/I"' + $sdk + '\Include\' + $version + '\shared" /I"' + $sdk + '\Include\' + $version + '\um" /I"' + $sdk + '\Include\' + $version + '\ucrt"'
$libraries = '/LIBPATH:"' + $sdk + '\Lib\' + $version + '\um\x64" /LIBPATH:"' + $sdk + '\Lib\' + $version + '\ucrt\x64"'
$common = ' /nologo /utf-8 /std:c++17 /EHsc /W4 /MT /DUNICODE /D_UNICODE ' + $include + ' '
$command = 'set "PATH=' + $vsInstaller + ';%PATH%" && call "' + $vcvars + '" && cd /d "' + $here + '" && cl' + $common + '/LD DesktopHook.cpp user32.lib gdi32.lib msimg32.lib comctl32.lib /link ' + $libraries + ' /OUT:Onharu.DesktopHook.dll && cl' + $common + 'LayerHost.cpp user32.lib /link ' + $libraries + ' /SUBSYSTEM:WINDOWS /ENTRY:wmainCRTStartup /OUT:Onharu.LayerHost.exe'
cmd.exe /d /c $command
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
Write-Host 'Built layer prototype and WndProc probe binaries'
