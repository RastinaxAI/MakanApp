[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BaseUrl,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$ApplicationCommit,
    [ValidateSet('Staging', 'Production')][string]$EnvironmentName = 'Staging',
    [switch]$ConfirmIsolatedStaging,
    [switch]$LiveOtp,
    [Guid]$MembershipId,
    [Guid]$OrganizationId,
    [ValidateSet('Student', 'Teacher', 'Parent', 'Manager')][string]$Role = 'Manager',
    [Guid]$ForeignOrganizationPersonId,
    [switch]$CheckStorage,
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'
# هیچ درخواست شبکه‌ای پیش از دو تأیید مستقل ارسال نمی‌شود؛ این ابزار عضو dotnet test نیست.
if (-not $ConfirmIsolatedStaging -or -not $LiveOtp -or $env:MAKAN_STAGING_LIVE_OTP -cne 'YES') {
    throw 'اجرای زنده نیازمند ConfirmIsolatedStaging، LiveOtp و MAKAN_STAGING_LIVE_OTP=YES است.'
}
$baseUri = $null
if (-not [Uri]::TryCreate($BaseUrl, [UriKind]::Absolute, [ref]$baseUri) -or
    $baseUri.Scheme -ne 'https' -or $baseUri.AbsolutePath -ne '/' -or
    $baseUri.UserInfo -or $baseUri.Query -or $baseUri.Fragment) {
    throw 'BaseUrl باید فقط origin معتبر HTTPS باشد؛ credential، path و query مجاز نیستند.'
}
if (($MembershipId -eq [Guid]::Empty) -ne ($OrganizationId -eq [Guid]::Empty)) {
    throw 'MembershipId و OrganizationId باید با هم ارائه شوند.'
}
Add-Type -AssemblyName System.Net.Http
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$client = New-Object System.Net.Http.HttpClient($handler)
$client.BaseAddress = $baseUri
$client.Timeout = [TimeSpan]::FromSeconds(30)
$steps = New-Object 'System.Collections.Generic.List[object]'
$evidence = [ordered]@{
    StartedAtUtc = [DateTime]::UtcNow.ToString('o')
    ApplicationCommit = $ApplicationCommit
    EnvironmentDeclaredByOperator = $EnvironmentName
    CommitVerifiedByServer = $false
    Recipient = $null
    SmsReceived = 'NOT_EXECUTED'
    ProviderDuration = 'NOT_OBSERVABLE_FROM_CLIENT'
    Steps = $steps
    Result = 'BLOCKED'
}
$token = $null
$otp = $null
$phone = $null
$login = $null
$wrongCode = $null
$currentStep = 'preflight'

function Read-PrivateInput([string]$Prompt) {
    $secure = Read-Host $Prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $secure.Dispose() }
}

function Invoke-Check {
    param([string]$Name, [string]$Path, [string]$Method = 'GET', [object]$Body,
        [System.Net.Http.HttpContent]$Content, [int[]]$Expected = @(200), [switch]$Authenticated)
    $script:currentStep = $Name
    $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::new($Method), $Path)
    [void]$request.Headers.TryAddWithoutValidation('X-Correlation-ID', ('gap02-' + [Guid]::NewGuid().ToString('N')))
    if ($Authenticated) {
        $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $script:token)
    }
    if ($null -ne $Body) {
        $request.Content = [System.Net.Http.StringContent]::new(($Body | ConvertTo-Json -Compress -Depth 8), [Text.Encoding]::UTF8, 'application/json')
    }
    elseif ($null -ne $Content) { $request.Content = $Content }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $response = $null
    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $status = [int]$response.StatusCode
        $passed = $Expected -contains $status
        $steps.Add([pscustomobject]@{ Name = $Name; Status = $(if ($passed) { 'PASS' } else { 'FAIL' }); Http = $status; DurationMs = $watch.ElapsedMilliseconds })
        if (-not $passed) { throw 'وضعیت HTTP مورد انتظار نیست؛ response خام منتشر نمی‌شود.' }
        $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $json = $null
        if ($response.Content.Headers.ContentType.MediaType -match 'json') {
            $json = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
        }
        return [pscustomobject]@{ Json = $json; Bytes = $bytes }
    }
    finally { if ($null -ne $response) { $response.Dispose() }; $request.Dispose() }
}

function Test-HubConnection {
    $negotiation = Invoke-Check -Name 'SignalR negotiate' -Path '/hubs/messaging/negotiate?negotiateVersion=1' -Method POST -Authenticated
    if (-not $negotiation.Json.connectionToken) { throw 'SignalR connection token موجود نیست.' }
    $script:currentStep = 'Authenticated WebSocket handshake'
    $socket = New-Object System.Net.WebSockets.ClientWebSocket
    $timeout = New-Object Threading.CancellationTokenSource([TimeSpan]::FromSeconds(20))
    try {
        $socket.Options.SetRequestHeader('Authorization', "Bearer $script:token")
        $address = [UriBuilder]::new($baseUri)
        $address.Scheme = 'wss'
        $address.Path = '/hubs/messaging'
        $address.Query = 'id=' + [Uri]::EscapeDataString($negotiation.Json.connectionToken)
        $socket.ConnectAsync($address.Uri, $timeout.Token).GetAwaiter().GetResult()
        $hello = [Text.Encoding]::UTF8.GetBytes('{"protocol":"json","version":1}' + [char]30)
        $socket.SendAsync([ArraySegment[byte]]::new($hello), [Net.WebSockets.WebSocketMessageType]::Text, $true, $timeout.Token).GetAwaiter().GetResult()
        $buffer = New-Object byte[] 4096
        $responseBytes = New-Object IO.MemoryStream
        try {
            do {
                $received = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $timeout.Token).GetAwaiter().GetResult()
                if ($received.MessageType -ne [Net.WebSockets.WebSocketMessageType]::Text) { throw 'پاسخ handshake معتبر نیست.' }
                $responseBytes.Write($buffer, 0, $received.Count)
                if ($responseBytes.Length -gt 4096) { throw 'پاسخ handshake بیش از حد بزرگ است.' }
                $handshake = [Text.Encoding]::UTF8.GetString($responseBytes.ToArray())
            } while (-not $handshake.Contains([string][char]30))
            $handshake = $handshake.Split([char]30)[0]
        }
        finally { $responseBytes.Dispose() }
        if ($handshake -ne '{}') { throw 'SignalR handshake تأیید نشد.' }
        $socket.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'smoke complete', $timeout.Token).GetAwaiter().GetResult()
    }
    finally { $socket.Dispose(); $timeout.Dispose() }
}

try {
    $null = Invoke-Check -Name 'Health' -Path '/health'
    $null = Invoke-Check -Name 'Readiness' -Path '/health/ready'
    $null = Invoke-Check -Name 'Unauthenticated profile' -Path '/api/v1/me' -Expected 401
    $null = Invoke-Check -Name 'Unauthenticated SignalR' -Path '/hubs/messaging/negotiate?negotiateVersion=1' -Method POST -Expected 401
    $phone = $env:MAKAN_STAGING_TEST_PHONE
    if ([string]::IsNullOrWhiteSpace($phone)) { $phone = Read-PrivateInput 'شماره تست مجاز را وارد کنید' }
    $phone = $phone.Trim()
    if ($phone.Length -lt 4) { throw 'شماره تست معتبر لازم است.' }
    $evidence.Recipient = '*******' + $phone.Substring($phone.Length - 2)
    $challenge = Invoke-Check -Name 'Real OTP challenge' -Path '/api/v1/auth/otp/challenges' -Method POST -Expected 202 -Body @{ phoneNumber = $phone }
    if (-not $challenge.Json.challengeId) { throw 'شناسه challenge موجود نیست.' }
    $currentStep = 'SMS receipt confirmation'
    $answer = Read-Host 'آیا SMS همین challenge دریافت شد؟ برای تأیید YES بنویسید'
    if ($answer -cne 'YES') { $evidence.SmsReceived = 'NO'; throw 'دریافت SMS تأیید نشد؛ ارسال مجدد خودکار انجام نمی‌شود.' }
    $evidence.SmsReceived = 'YES'
    $otp = (Read-PrivateInput 'کد همان پیامک را وارد کنید؛ ثبت یا نمایش داده نمی‌شود').Trim()
    $normalized = New-Object Text.StringBuilder
    foreach ($character in $otp.ToCharArray()) {
        $digit = [Globalization.CharUnicodeInfo]::GetDecimalDigitValue($character)
        if ($digit -lt 0) { throw 'کد باید رقم باشد.' }
        [void]$normalized.Append($digit)
    }
    if ($normalized.Length -lt 1) { throw 'کد خالی است.' }
    $normalizedOtp = $normalized.ToString()
    $wrongCode = $(if ($normalizedOtp[0] -eq '0') { '1' } else { '0' }) + $normalizedOtp.Substring(1)
    $wrong = Invoke-Check -Name 'One wrong OTP rejected' -Path '/api/v1/auth/otp/verify' -Method POST -Expected 400 -Body @{
        challengeId = $challenge.Json.challengeId; phoneNumber = $phone; code = $wrongCode
    }
    if ($wrong.Json.code -ne 'OTP_INVALID' -or $wrong.Json.accessToken) { throw 'رفتار کد اشتباه مطابق قرارداد نیست.' }
    $login = Invoke-Check -Name 'Real OTP verification' -Path '/api/v1/auth/otp/verify' -Method POST -Body @{
        challengeId = $challenge.Json.challengeId; phoneNumber = $phone; code = $otp; deviceName = 'staging-core-smoke'
    }
    $token = $login.Json.accessToken
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'نشست احرازشده ایجاد نشد.' }
    $null = Invoke-Check -Name 'Authenticated profile' -Path '/api/v1/me' -Authenticated
    $workspaces = Invoke-Check -Name 'Authenticated workspaces' -Path '/api/v1/workspaces' -Authenticated
    if ($MembershipId -ne [Guid]::Empty) {
        $allowed = @($workspaces.Json | Where-Object { $_.membershipId -eq $MembershipId.ToString() -and $_.organizationId -eq $OrganizationId.ToString() -and $_.role -eq $Role })
        if ($allowed.Count -eq 0) { throw 'عضویت/نقش موردنظر در پاسخ مجاز workspace نیست.' }
        $selected = Invoke-Check -Name 'Select existing workspace' -Path '/api/v1/workspaces/select' -Method POST -Authenticated -Body @{
            workspaceType = 'Organization'; membershipId = $MembershipId; role = $Role
        }
        if ($selected.Json.organizationId -ne $OrganizationId.ToString()) { throw 'سازمان workspace منطبق نیست.' }
        $null = Invoke-Check -Name 'Organization read' -Path '/api/v1/academic/classes' -Authenticated
    }
    else { $steps.Add([pscustomobject]@{ Name = 'Organization read'; Status = 'NOT_EXECUTED' }) }
    if ($ForeignOrganizationPersonId -ne [Guid]::Empty -and $MembershipId -ne [Guid]::Empty -and $Role -eq 'Manager') {
        $null = Invoke-Check -Name 'Cross-org concealment' -Path "/api/v1/organization/people/$ForeignOrganizationPersonId" -Authenticated -Expected 404
    }
    else { $steps.Add([pscustomobject]@{ Name = 'Cross-org concealment'; Status = 'NOT_EXECUTED' }) }
    $currentStep = 'Authenticated WebSocket reconnect'
    Test-HubConnection
    Test-HubConnection
    $steps.Add([pscustomobject]@{ Name = 'Authenticated WebSocket reconnect'; Status = 'PASS' })
    if ($CheckStorage) {
        $currentStep = 'Storage roundtrip'
        $sample = 'makan staging smoke ' + [Guid]::NewGuid().ToString('N')
        $multipart = New-Object System.Net.Http.MultipartFormDataContent
        $content = [Net.Http.StringContent]::new($sample, [Text.Encoding]::UTF8, 'text/plain')
        $multipart.Add($content, 'File', 'staging-smoke.txt')
        $upload = Invoke-Check -Name 'Own storage upload' -Path '/api/v1/files' -Method POST -Authenticated -Content $multipart -Expected 201
        $file = $upload.Json
        $download = Invoke-Check -Name 'Own storage download' -Path "/api/v1/files/$($file.id)/content" -Authenticated
        if ([Text.Encoding]::UTF8.GetString($download.Bytes) -ne $sample) { throw 'محتوای فایل منطبق نیست.' }
        $null = Invoke-Check -Name 'Unauthenticated download denied' -Path "/api/v1/files/$($file.id)/content" -Expected 401
        $version = [Uri]::EscapeDataString($file.rowVersion)
        $null = Invoke-Check -Name 'Delete own smoke asset' -Path "/api/v1/files/$($file.id)?expectedRowVersion=$version" -Method DELETE -Authenticated
    }
    else { $steps.Add([pscustomobject]@{ Name = 'Storage roundtrip'; Status = 'NOT_EXECUTED' }) }
    $currentStep = 'Sensitive log review'
    if ($LogPath) {
        # فقط log تأییدشده بازه smoke خوانده می‌شود؛ متن یا match هرگز در خروجی نمی‌آید.
        $log = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $LogPath).Path)
        if ([string]::IsNullOrWhiteSpace($log)) { throw 'بازه log خالی است و شاهد قابل ارزیابی نیست.' }
        $sensitive = @($phone, $otp, $normalizedOtp, $wrongCode, $token, $login.Json.refreshToken,
            $env:Sms__MeliPayamakOtp__ServiceToken, $env:Identity__SecurityKey)
        foreach ($value in $sensitive) {
            if (-not [string]::IsNullOrWhiteSpace($value) -and $log.Contains($value)) { throw 'داده حساس در log مشاهده شد؛ متن منتشر نشده است.' }
        }
        if ($log -match '(?i)(Bearer\s+[A-Za-z0-9_\-+/]{16,}|api/send/otp/\S+|(?:Password|Pwd|ServiceToken|SecurityKey)["\s]*[:=]["\s]*\S+)') {
            throw 'الگوی حساس در log مشاهده شد؛ متن منتشر نشده است.'
        }
        $steps.Add([pscustomobject]@{ Name = 'Sensitive log review'; Status = 'PASS'; Scope = 'Supplied log window and known values/patterns only' })
        $log = $null
    }
    else { $steps.Add([pscustomobject]@{ Name = 'Sensitive log review'; Status = 'NOT_EXECUTED' }) }
    $evidence.Result = $(if (@($steps | Where-Object Status -ne 'PASS').Count -eq 0) { 'PASS' } else { 'BLOCKED' })
}
catch {
    $evidence.Result = 'FAIL'
    $evidence['FailureStep'] = $currentStep
    $evidence['Failure'] = 'مرحله موفق نبود؛ exception، URL، body، OTP و credential خام عمداً چاپ نشده‌اند.'
}
finally {
    $client.Dispose()
    $handler.Dispose()
    $phone = $otp = $normalizedOtp = $wrongCode = $token = $login = $sensitive = $null
}
$evidence['CompletedAtUtc'] = [DateTime]::UtcNow.ToString('o')
$evidence | ConvertTo-Json -Depth 8
if ($evidence.Result -ne 'PASS') { exit 1 }
