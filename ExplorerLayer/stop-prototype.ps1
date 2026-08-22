$hostExe = Join-Path $PSScriptRoot 'OnharuV3.LayerHost.exe'
if (Test-Path -LiteralPath $hostExe) { & $hostExe --stop }
Get-Process -Name 'OnharuV3.LayerHost' -ErrorAction SilentlyContinue | Stop-Process
