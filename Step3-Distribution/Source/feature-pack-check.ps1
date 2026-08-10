param([string]$ExePath)
$ErrorActionPreference = 'Stop'

$exe = if ([string]::IsNullOrWhiteSpace($ExePath)) { Join-Path $PSScriptRoot 'FamilyPlanner.exe' } else { $ExePath }
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build FamilyPlanner.exe first.' }

$assembly = [Reflection.Assembly]::LoadFrom($exe)
$main = $assembly.GetType('FamilyPlanner.MainWindow', $true)
$solar = $main.GetMethod('SolarTerm', [Reflection.BindingFlags]'Public,Static')

$cases = @(
    @{ Date = [datetime]'2021-02-03'; Expected = ([string][char]0xC785 + [char]0xCD98) },
    @{ Date = [datetime]'2021-06-21'; Expected = ([string][char]0xD558 + [char]0xC9C0) },
    @{ Date = [datetime]'2021-12-22'; Expected = ([string][char]0xB3D9 + [char]0xC9C0) },
    @{ Date = [datetime]'2021-06-20'; Expected = $null }
)
foreach ($case in $cases) {
    $actual = $solar.Invoke($null, @($case.Date))
    if ($actual -ne $case.Expected) { throw "Solar term mismatch: $($case.Date.ToString('yyyy-MM-dd')) expected '$($case.Expected)', got '$actual'." }
}

$settingsType = $assembly.GetType('FamilyPlanner.PlannerSettings', $true)
$settings = [Activator]::CreateInstance($settingsType)
if ($settings.Version -ne 6) { throw "Unexpected settings version: $($settings.Version)" }
if (-not $settings.CompletedLast) { throw 'CompletedLast must default to true.' }
if ($null -eq $settings.DateBackgroundColors) { throw 'DateBackgroundColors must be initialized.' }

Write-Host 'Feature pack checks passed.'
