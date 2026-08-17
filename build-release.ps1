$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$releaseRoot = Join-Path $root 'Release'
$stage = Join-Path $releaseRoot 'ONHARU-2.1.0'
$installerOutput = Join-Path $releaseRoot 'Installer'
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'

$layerHostExe = Join-Path $root 'ExplorerLayer\OnharuV3.LayerHost.exe'
if (Test-Path -LiteralPath $layerHostExe) { & $layerHostExe --stop | Out-Null }
Get-Process -Name 'OnharuV3.App','ONHARU','ONHARU-2.1-local-test','OnharuV3.LayerHost' -ErrorAction SilentlyContinue | Stop-Process -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

& (Join-Path $root 'App\build.ps1') -OutputName 'ONHARU.exe'
& (Join-Path $root 'ExplorerLayer\build.ps1')

if (Test-Path -LiteralPath $stage) {
    $resolved = [IO.Path]::GetFullPath($stage)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath($releaseRoot), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe release cleanup target.' }
    [IO.Directory]::Delete($resolved, $true)
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'App\ONHARU.exe') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'ExplorerLayer\OnharuV3.LayerHost.exe') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'ExplorerLayer\OnharuV3.DesktopHook.dll') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $stage

$hashes = Get-ChildItem -LiteralPath $stage -File | Sort-Object Name | Get-FileHash -Algorithm SHA256
$hashes | ForEach-Object { $_.Hash + '  ' + (Split-Path -Leaf $_.Path) } | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding ascii
if (-not (Test-Path -LiteralPath $iscc)) { throw 'Inno Setup 6 compiler was not found.' }
& $iscc (Join-Path $root 'Installer\ONHARU.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
Get-FileHash -LiteralPath (Join-Path $installerOutput 'ONHARU-2.1.0-Setup.exe') -Algorithm SHA256 |
    ForEach-Object { $_.Hash + '  ONHARU-2.1.0-Setup.exe' } | Set-Content -LiteralPath (Join-Path $installerOutput 'SHA256SUMS.txt') -Encoding ascii

# Keep source directories clean. Everything below is reproducible from source.
foreach ($generated in @(
    (Join-Path $root 'App\ONHARU.exe'),
    (Join-Path $root 'ExplorerLayer\OnharuV3.DesktopHook.dll'),
    (Join-Path $root 'ExplorerLayer\OnharuV3.LayerHost.exe'),
    (Join-Path $root 'ExplorerLayer\DesktopHook.obj'),
    (Join-Path $root 'ExplorerLayer\DesktopHook.lib'),
    (Join-Path $root 'ExplorerLayer\DesktopHook.exp'),
    (Join-Path $root 'ExplorerLayer\LayerHost.obj')
)) { if (Test-Path -LiteralPath $generated) { [IO.File]::Delete($generated) } }
Write-Host "Release: $stage"
Write-Host "Installer: $(Join-Path $installerOutput 'ONHARU-2.1.0-Setup.exe')"
