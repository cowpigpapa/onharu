param([string]$OutputName = '..\Tests\LocalTest\ONHARU-2.1-local-test.exe')
$ErrorActionPreference = 'Stop'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$output = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputName))
$outputFolder = Split-Path -Parent $output
if (-not (Test-Path -LiteralPath $outputFolder)) { New-Item -ItemType Directory -Path $outputFolder | Out-Null }
$icon = Join-Path $PSScriptRoot 'Assets\onharu.ico'
$resizeNwSe = Join-Path $PSScriptRoot 'Assets\resize-nwse.cur'
$resizeNeSw = Join-Path $PSScriptRoot 'Assets\resize-nesw.cur'
$resizeHorizontal = Join-Path $PSScriptRoot 'Assets\resize-horizontal.cur'
$resizeVertical = Join-Path $PSScriptRoot 'Assets\resize-vertical.cur'
if (-not (Test-Path -LiteralPath $icon)) { throw 'ONHARU icon is missing. Run Assets\create-icon.ps1 first.' }
if (-not (Test-Path -LiteralPath $resizeNwSe) -or -not (Test-Path -LiteralPath $resizeNeSw) -or
    -not (Test-Path -LiteralPath $resizeHorizontal) -or -not (Test-Path -LiteralPath $resizeVertical)) {
  & (Join-Path $PSScriptRoot 'Assets\create-resize-cursors.ps1')
}

$arguments = @(
  '/nologo', '/target:winexe', '/optimize+', "/out:$output", "/win32icon:$icon",
  "/resource:$resizeNwSe,FamilyPlanner.Assets.resize-nwse.cur", "/resource:$resizeNeSw,FamilyPlanner.Assets.resize-nesw.cur",
  "/resource:$resizeHorizontal,FamilyPlanner.Assets.resize-horizontal.cur", "/resource:$resizeVertical,FamilyPlanner.Assets.resize-vertical.cur",
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
