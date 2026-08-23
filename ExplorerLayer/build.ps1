$ErrorActionPreference = 'Stop'
$vcvars = 'C:\BuildTools\VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path -LiteralPath $vcvars)) { throw 'Visual C++ Build Tools are not installed.' }
$here = $PSScriptRoot
$sdk = 'C:\Program Files (x86)\Windows Kits\10'
$version = '10.0.26100.0'
$include = '/I"' + $sdk + '\Include\' + $version + '\shared" /I"' + $sdk + '\Include\' + $version + '\um" /I"' + $sdk + '\Include\' + $version + '\ucrt"'
$libraries = '/LIBPATH:"' + $sdk + '\Lib\' + $version + '\um\x64" /LIBPATH:"' + $sdk + '\Lib\' + $version + '\ucrt\x64"'
$common = ' /nologo /utf-8 /std:c++17 /EHsc /W4 /MT /DUNICODE /D_UNICODE ' + $include + ' '
$command = 'call "' + $vcvars + '" && cd /d "' + $here + '" && cl' + $common + '/LD DesktopHook.cpp user32.lib gdi32.lib msimg32.lib comctl32.lib /link ' + $libraries + ' /OUT:Onharu.DesktopHook.dll && cl' + $common + 'LayerHost.cpp user32.lib /link ' + $libraries + ' /SUBSYSTEM:WINDOWS /ENTRY:wmainCRTStartup /OUT:Onharu.LayerHost.exe'
cmd.exe /d /c $command
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
Write-Host 'Built layer prototype and WndProc probe binaries'
