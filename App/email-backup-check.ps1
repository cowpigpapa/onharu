$ErrorActionPreference = 'Stop'
$service = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EmailBackupService.cs'))
$flow = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'MainWindow.Settings.cs'))
$choice = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'DataManagementChoiceWindow.cs'))
if (-not $service.Contains('https://onharu.app/api/v1/backup-email')) { throw 'Mail endpoint must use HTTPS.' }
if (-not $service.Contains('1024 * 1024')) { throw 'Mail attachment size limit is missing.' }
if (-not $service.Contains('googleIdToken')) { throw 'Google identity proof is missing from mail request.' }
if (-not $flow.Contains('!Store.IsGoogleItem(x)')) { throw 'Email export must exclude Google source items.' }
if (-not $flow.Contains('File.Delete(tempPath)')) { throw 'Temporary email attachment cleanup is missing.' }
if (-not $flow.Contains('GoogleCalendar.IsConnected')) { throw 'Email export must require a connected Google account.' }
if (-not $flow.Contains('GoogleCalendar.ConnectedAccountId')) { throw 'Email recipient must come from the connected Google account.' }
if (-not $flow.Contains('GoogleCalendar.IdentityTokenAsync()')) { throw 'Email export must obtain a fresh Google identity token.' }
$google = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'GoogleCalendarService.cs'))
if (-not $google.Contains('openid email')) { throw 'Google identity scopes are missing.' }
$window = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EmailBackupWindow.cs'))
if ($window.Contains('new MailAddress')) { throw 'Email recipient must not be user-editable.' }
if (-not $window.Contains('Text = "G  " + connectedGoogleAddress')) { throw 'Connected Google account display is missing.' }
if (-not $choice.Contains('Choice("Excel CSV"')) { throw 'CSV email export choice is missing.' }
if (-not $flow.Contains('csv ? "text/csv"')) { throw 'CSV email content type is missing.' }
if (-not $flow.Contains('csv ? items : items.Where')) { throw 'CSV email export must include the full visible item set.' }
Write-Host 'ONHARU email backup boundary checks passed.'
