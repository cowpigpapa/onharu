$hostExe = Join-Path $PSScriptRoot 'Onharu.LayerHost.exe'
if (Test-Path -LiteralPath $hostExe) { & $hostExe --stop }
Get-Process -Name 'Onharu.LayerHost' -ErrorAction SilentlyContinue | Stop-Process
