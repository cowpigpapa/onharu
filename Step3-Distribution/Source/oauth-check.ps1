$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot 'GoogleCalendarService.cs') -Raw -Encoding UTF8

foreach ($forbidden in @('ClientSecret', 'ONHARU_GOOGLE_CLIENT_SECRET', 'client_secret=')) {
    if ($source.Contains($forbidden)) { throw "배포 소스에 금지된 OAuth 항목이 있습니다: $forbidden" }
}

foreach ($required in @('code_challenge_method=S256', 'code_verifier=', 'query["state"] != state')) {
    if (-not $source.Contains($required)) { throw "PKCE 검사가 누락되었습니다: $required" }
}

if (-not $source.Contains('397166784516-g8l18umimg4uvp3l4tjcnlguedoa4c1j.apps.googleusercontent.com')) {
    throw '배포용 Desktop OAuth Client ID가 적용되지 않았습니다.'
}

Write-Host 'Secret-free PKCE OAuth checks passed.'
