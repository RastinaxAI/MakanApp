param()
$ErrorActionPreference='Stop'
if(-not $env:MAKAN_BASE_URL){ throw 'Set MAKAN_BASE_URL to the authorized test origin' }
$base=$env:MAKAN_BASE_URL.TrimEnd('/')
if($base -notmatch '^https://' -and $base -notmatch '^http://(localhost|127\.0\.0\.1)(:\d+)?$'){ throw 'Refusing insecure non-local origin' }
Write-Host 'GET /health/live'
curl.exe --fail-with-body --silent --show-error --max-time 15 "$base/health/live"
Write-Host 'GET /health/ready'
curl.exe --silent --show-error --max-time 15 -i "$base/health/ready"
Write-Host 'GET /api/v1/me (without Bearer; expected 401)'
curl.exe --silent --show-error --max-time 15 -i -H 'Accept: application/json' "$base/api/v1/me"
