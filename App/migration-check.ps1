param([string]$Exe = (Join-Path $PSScriptRoot '..\Tests\LocalTest\ONHARU-2.2-local-test.exe'))
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $Exe).Path)
$type = $assembly.GetType('FamilyPlanner.V21Migration', $true)
$method = $type.GetMethod('BackupPreUpgrade', [Reflection.BindingFlags]'Public,Static')
$store = $assembly.GetType('FamilyPlanner.Store', $true)
$readLegacy = $store.GetMethod('ReadImportFile', [Reflection.BindingFlags]'Public,Static', $null, [Type[]]@([string]), $null)
$temp = Join-Path ([IO.Path]::GetTempPath()) ('onharu-v21-migration-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $temp 'source'; $target = Join-Path $source 'pre-2.1-backup'
try {
    New-Item -ItemType Directory -Path $source | Out-Null
    [IO.File]::WriteAllText((Join-Path $source 'settings.json'), 'settings-v2')
    [IO.File]::WriteAllText((Join-Path $source 'items-local.json'), 'items-v2')
    [IO.File]::WriteAllText((Join-Path $source 'google-token.dat'), 'secret')
    $method.Invoke($null, [object[]]@([string]$source, [string]$target)) | Out-Null
    if ([IO.File]::ReadAllText((Join-Path $target 'settings.json')) -ne 'settings-v2') { throw 'Settings snapshot mismatch.' }
    if ([IO.File]::ReadAllText((Join-Path $target 'items-local.json')) -ne 'items-v2') { throw 'Items snapshot mismatch.' }
    if (Test-Path -LiteralPath (Join-Path $target 'google-token.dat')) { throw 'Google token must not be copied.' }
    if (-not (Test-Path -LiteralPath (Join-Path $target 'completed.txt'))) { throw 'Migration marker is missing.' }
    [IO.File]::WriteAllText((Join-Path $source 'settings.json'), 'changed')
    $method.Invoke($null, [object[]]@([string]$source, [string]$target)) | Out-Null
    if ([IO.File]::ReadAllText((Join-Path $target 'settings.json')) -ne 'settings-v2') { throw 'One-time snapshot was overwritten.' }

    $legacyItems = Join-Path $source 'ONHARU-2.1-backup.json'
    [IO.File]::WriteAllText($legacyItems, '[{"AllDay":true,"Category":"\uc5c5\ubb34","End":"\/Date(1787583600000+0900)\/","Start":"\/Date(1787497200000+0900)\/","Title":"v21 import compatibility"}]', [Text.UTF8Encoding]::new($false))
    $loaded = $readLegacy.Invoke($null, [object[]]@([string]$legacyItems))
    if ($loaded.Count -ne 1 -or $loaded[0].Title -ne 'v21 import compatibility' -or $loaded[0].Category -ne ([char]0xC5C5 + [char]0xBB34 + [char]0xC77C + [char]0xC815)) {
        $actual = if ($loaded.Count) { "count=$($loaded.Count), title=$($loaded[0].Title), category=$($loaded[0].Category), start=$($loaded[0].Start)" } else { 'count=0' }
        throw "ONHARU 2.1 backup data was not normalized for 2.2: $actual"
    }
}
finally {
    if (Test-Path -LiteralPath $temp) {
        $resolvedTemp = [IO.Path]::GetFullPath($temp)
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $resolvedTemp.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe temporary cleanup target: $resolvedTemp" }
        [IO.Directory]::Delete($resolvedTemp, $true)
    }
}
Write-Host 'ONHARU 2.1 backup and pre-upgrade migration checks passed.'
