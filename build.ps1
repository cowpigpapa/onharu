param([string]$OutputName = 'FamilyPlanner.exe')
$ErrorActionPreference = 'Stop'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$output = Join-Path $PSScriptRoot $OutputName

$arguments = @(
  '/nologo', '/target:winexe', '/optimize+', "/out:$output",
  ('/reference:' + (Join-Path $wpf 'PresentationCore.dll')),
  ('/reference:' + (Join-Path $wpf 'PresentationFramework.dll')),
  ('/reference:' + (Join-Path $wpf 'WindowsBase.dll')),
  ('/reference:' + (Join-Path $framework 'System.Xaml.dll')),
  ('/reference:' + (Join-Path $framework 'System.Runtime.Serialization.dll')),
  ('/reference:' + (Join-Path $framework 'System.Net.Http.dll')),
  ('/reference:' + (Join-Path $framework 'System.Security.dll')),
  ('/reference:' + (Join-Path $framework 'System.Windows.Forms.dll')),
  ('/reference:' + (Join-Path $framework 'System.Drawing.dll'))
) + @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
& $csc $arguments

if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "Built: $output"
