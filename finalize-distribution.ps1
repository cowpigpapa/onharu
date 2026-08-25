$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

& (Join-Path $root 'build-release.ps1')

$installerDir = Join-Path $root 'Release\Installer'
$installer = Join-Path $installerDir 'ONHARU-2.2.3-Setup.exe'
$hashFile = Join-Path $installerDir 'SHA256SUMS.txt'
$zip = Join-Path $installerDir 'ONHARU-2.2.3-Installer.zip'
$package = Join-Path $installerDir '_zip-package'

if (-not (Test-Path -LiteralPath $installer)) { throw 'Installer was not created.' }
if (Test-Path -LiteralPath $package) { [IO.Directory]::Delete($package, $true) }
[IO.Directory]::CreateDirectory($package) | Out-Null

Copy-Item -LiteralPath $installer -Destination $package
Copy-Item -LiteralPath $hashFile -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'Distribution\EMAIL_DISTRIBUTION_NOTE.md') -Destination (Join-Path $package '설치안내.md')
if (Test-Path -LiteralPath $zip) { [IO.File]::Delete($zip) }
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -CompressionLevel Optimal
[IO.Directory]::Delete($package, $true)

$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
$zipHash + '  ONHARU-2.2.3-Installer.zip' | Set-Content -LiteralPath (Join-Path $installerDir 'ONHARU-2.2.3-Installer.zip.sha256.txt') -Encoding ascii
Write-Host "Distribution ZIP: $zip"
Write-Host "SHA-256: $zipHash"
