param([string]$Exe = (Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.1-local-test.exe'))
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $Exe).Path)
$type = $assembly.GetType('FamilyPlanner.V21Migration', $true)
$method = $type.GetMethod('BackupPreUpgrade', [Reflection.BindingFlags]'Public,Static')
$temp = Join-Path ([IO.Path]::GetTempPath()) ('onharu-v21-migration-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $temp 'source'; $target = Join-Path $source 'pre-2.1-backup'
try {
    New-Item -ItemType Directory -Path $source | Out-Null
    [IO.File]::WriteAllText((Join-Path $source 'settings.json'), 'settings-v2')
    [IO.File]::WriteAllText((Join-Path $source 'items-local.json'), 'items-v2')
    [IO.File]::WriteAllText((Join-Path $source 'google-v3.token'), 'secret')
    $method.Invoke($null, [object[]]@([string]$source, [string]$target)) | Out-Null
    if ([IO.File]::ReadAllText((Join-Path $target 'settings.json')) -ne 'settings-v2') { throw 'Settings snapshot mismatch.' }
    if ([IO.File]::ReadAllText((Join-Path $target 'items-local.json')) -ne 'items-v2') { throw 'Items snapshot mismatch.' }
    if (Test-Path -LiteralPath (Join-Path $target 'google-v3.token')) { throw 'Google token must not be copied.' }
    if (-not (Test-Path -LiteralPath (Join-Path $target 'completed.txt'))) { throw 'Migration marker is missing.' }
    [IO.File]::WriteAllText((Join-Path $source 'settings.json'), 'changed')
    $method.Invoke($null, [object[]]@([string]$source, [string]$target)) | Out-Null
    if ([IO.File]::ReadAllText((Join-Path $target 'settings.json')) -ne 'settings-v2') { throw 'One-time snapshot was overwritten.' }
}
finally {
    if (Test-Path -LiteralPath $temp) {
        $resolvedTemp = [IO.Path]::GetFullPath($temp)
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $resolvedTemp.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe temporary cleanup target: $resolvedTemp" }
        [IO.Directory]::Delete($resolvedTemp, $true)
    }
}
Write-Host 'ONHARU 2.1 pre-upgrade migration checks passed.'
