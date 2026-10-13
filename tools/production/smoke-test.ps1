param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^https://')]
    [string]$BaseUrl,

    [string]$AccessToken
)

$ErrorActionPreference = 'Stop'
$baseAddress = $BaseUrl.TrimEnd('/')

function Invoke-SmokeRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int[]]$ExpectedStatus,
        [string]$BearerToken,
        [string]$Method = 'GET'
    )

    $headers = @{ 'X-Correlation-ID' = "smoke-$([Guid]::NewGuid().ToString('N'))" }
    if (-not [string]::IsNullOrWhiteSpace($BearerToken)) {
        $headers.Authorization = "Bearer $BearerToken"
    }

    try {
        $response = Invoke-WebRequest -Uri "$baseAddress$Path" -Method $Method -Headers $headers -UseBasicParsing
        $status = [int]$response.StatusCode
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
        $status = [int]$_.Exception.Response.StatusCode
    }

    if ($ExpectedStatus -notcontains $status) {
        throw "$Name failed. Expected $($ExpectedStatus -join ',') but received $status."
    }

    Write-Host "PASS $Name ($status)"
}

Invoke-SmokeRequest -Name 'Liveness' -Path '/health/live' -ExpectedStatus 200
Invoke-SmokeRequest -Name 'Readiness' -Path '/health/ready' -ExpectedStatus 200
Invoke-SmokeRequest -Name 'Admin boundary' -Path '/api/admin/v1/me' -ExpectedStatus 401,403
Invoke-SmokeRequest -Name 'SignalR auth boundary' -Path '/hubs/messaging/negotiate?negotiateVersion=1' -Method 'POST' -ExpectedStatus 401

if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
    Invoke-SmokeRequest -Name 'Current profile' -Path '/api/v1/me' -BearerToken $AccessToken -ExpectedStatus 200
    Invoke-SmokeRequest -Name 'Workspaces' -Path '/api/v1/workspaces' -BearerToken $AccessToken -ExpectedStatus 200
    Invoke-SmokeRequest -Name 'Conversation list' -Path '/api/v1/conversations' -BearerToken $AccessToken -ExpectedStatus 200
}
else {
    Write-Warning 'Authenticated smoke checks skipped because AccessToken was not supplied.'
}

Write-Host 'Smoke checks completed. OTP/SMS and payment were intentionally not invoked.'
