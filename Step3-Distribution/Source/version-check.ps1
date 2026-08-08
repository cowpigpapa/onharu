param([string]$Exe = '..\ONHARU-ver1.0.0-preview.exe')
$ErrorActionPreference = 'Stop'
$path = (Resolve-Path $Exe).Path
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)

if ($info.ProductName -ne 'ONHARU') { throw "제품명이 올바르지 않습니다: $($info.ProductName)" }
if ($info.CompanyName -ne 'JUAN.HJLEE') { throw "제작자명이 올바르지 않습니다: $($info.CompanyName)" }
if ($info.FileVersion -ne '1.0.0.0') { throw "파일 버전이 올바르지 않습니다: $($info.FileVersion)" }
if ($info.ProductVersion -ne '1.0.0') { throw "제품 버전이 올바르지 않습니다: $($info.ProductVersion)" }

Write-Host 'ONHARU version metadata checks passed.'
