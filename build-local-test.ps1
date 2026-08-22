$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$target = Join-Path $root 'Tests\LocalTest'

New-Item -ItemType Directory -Force -Path $target | Out-Null
& (Join-Path $root 'App\build.ps1')
& (Join-Path $root 'ExplorerLayer\build.ps1')

Copy-Item -Force -LiteralPath (Join-Path $root 'ExplorerLayer\OnharuV3.DesktopHook.dll') -Destination $target
Copy-Item -Force -LiteralPath (Join-Path $root 'ExplorerLayer\OnharuV3.LayerHost.exe') -Destination $target

@('OnharuV3.DesktopHook.dll','OnharuV3.LayerHost.exe','DesktopHook.obj','DesktopHook.lib','DesktopHook.exp','LayerHost.obj') |
    ForEach-Object { Remove-Item -Force -ErrorAction SilentlyContinue -LiteralPath (Join-Path $root ('ExplorerLayer\' + $_)) }

Write-Host "Local test build: $target"
