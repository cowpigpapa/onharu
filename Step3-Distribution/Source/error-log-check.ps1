param([string]$Exe = '..\ONHARU-ver1.0.0-category-preview.exe')
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe))
$type = $assembly.GetType('FamilyPlanner.ErrorLog')
if ($null -eq $type) { throw 'ErrorLog 형식을 찾을 수 없습니다.' }
$clean = $type.GetMethod('Clean', [Reflection.BindingFlags]'NonPublic,Static')
$sample = 'GOCSPX-example-secret user@example.com access_token=sample-token'
$result = [string]$clean.Invoke($null, [object[]]@($sample))

foreach ($privateValue in @('GOCSPX-example-secret', 'user@example.com', 'sample-token')) {
    if ($result.Contains($privateValue)) { throw "민감정보가 가려지지 않았습니다: $privateValue" }
}
Write-Host 'ONHARU error log privacy checks passed.'
