param([string]$Exe = (Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.2-local-test.exe'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$path = (Resolve-Path $Exe).Path
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)

if ($info.ProductName -ne 'ONHARU') { throw "제품명이 올바르지 않습니다: $($info.ProductName)" }
if ($info.CompanyName -ne 'JUAN.HJLEE') { throw "제작자명이 올바르지 않습니다: $($info.CompanyName)" }
if ($info.FileVersion -ne '2.2.4.0') { throw "파일 버전이 올바르지 않습니다: $($info.FileVersion)" }
if ($info.ProductVersion -ne '2.2.4') { throw "제품 버전이 올바르지 않습니다: $($info.ProductVersion)" }
$icon = [Drawing.Icon]::ExtractAssociatedIcon($path)
if ($null -eq $icon) { throw '실행 파일 아이콘을 읽을 수 없습니다.' }
$icon.Dispose()

Write-Host 'ONHARU version metadata checks passed.'
