param(
    [uri]$BackendBaseUrl = 'http://127.0.0.1',
    [switch]$PreflightOnly
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

# این ابزار فقط روی Windows Backend اجرا شود؛ تنظیمات IIS، دیتابیس و AI را تغییر نمی‌دهد.
$aiBaseUrl = 'http://10.77.0.2:8001'
if (-not (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -eq '10.77.0.1' })) {
    Write-Output 'AI_BACKEND_SERVER_EXECUTION_REQUIRED'
    exit 2
}
if ($BackendBaseUrl.Scheme -ne 'https' -and
    -not ($BackendBaseUrl.Scheme -eq 'http' -and $BackendBaseUrl.IsLoopback)) {
    throw 'برای جلوگیری از نشت توکن، Backend باید HTTPS یا HTTP روی loopback همین سرور باشد.'
}

$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$handler.UseProxy = $false
$http = New-Object System.Net.Http.HttpClient($handler)
$http.Timeout = [TimeSpan]::FromSeconds(195)
$sessions = @{}
$failures = 0
try {
    foreach ($probePath in @('health', 'ready')) {
        $probeTimeout = New-Object System.Threading.CancellationTokenSource
        $probeTimeout.CancelAfter(5000)
        try {
            $probe = $http.GetAsync("$aiBaseUrl/api/v1/$probePath", $probeTimeout.Token).GetAwaiter().GetResult()
            try {
                Write-Output "$probePath HTTP $([int]$probe.StatusCode)"
                if ([int]$probe.StatusCode -ne 200) { $failures++ }
            } finally { $probe.Dispose() }
        } catch {
            Write-Output "$probePath NOT_REACHABLE"
            $failures++
        } finally { $probeTimeout.Dispose() }
    }
    if ($failures -gt 0) { exit 2 }
    if ($PreflightOnly) { exit 0 }

    # این درخواست عمداً DTO ندارد؛ فقط تقدم auth بر parse را می‌سنجد و inference اجرا نمی‌کند.
    $noJwt = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, "$aiBaseUrl/api/v1/ai/chat")
    $noJwt.Content = New-Object System.Net.Http.StringContent('{}', [Text.Encoding]::UTF8, 'application/json')
    [void]$noJwt.Headers.TryAddWithoutValidation('X-AI-Contract-Version', 'v1')
    try {
        $noJwtResponse = $http.SendAsync($noJwt).GetAwaiter().GetResult()
        try {
            Write-Output "no-JWT HTTP $([int]$noJwtResponse.StatusCode)"
            if ([int]$noJwtResponse.StatusCode -ne 401) { exit 3 }
        } finally { $noJwtResponse.Dispose() }
    } finally { $noJwt.Dispose() }

    function Read-Identifier([string]$label) {
        return ([guid](Read-Host $label)).ToString('D')
    }

    function Invoke-BackendPost([string]$operation, [string]$role, [string]$path, $body) {
        $correlation = 'step10g-' + [guid]::NewGuid().ToString('N')
        $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post,
            ([uri]::new($BackendBaseUrl, $path)))
        $tokenPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sessions[$role])
        try { $sessionToken = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPointer) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPointer) }
        $request.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $sessionToken)
        $sessionToken = $null
        [void]$request.Headers.TryAddWithoutValidation('X-Correlation-ID', $correlation)
        if ($null -ne $body) {
            $request.Content = New-Object System.Net.Http.StringContent(
                ($body | ConvertTo-Json -Depth 5 -Compress), [Text.Encoding]::UTF8, 'application/json')
        }
        try {
            $response = $http.SendAsync($request).GetAwaiter().GetResult()
            try {
                $status = [int]$response.StatusCode
                $payload = $null
                try { $payload = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json }
                catch { }
                # فقط شناسه‌های امن چاپ می‌شوند؛ نه پاسخ مدل، نه header و نه پیام خطا.
                $requestId = if ($payload.requestId -match '^[a-zA-Z0-9_.-]{1,128}$') { $payload.requestId } else { $null }
                $echo = if ($payload.correlationId -eq $correlation) { $correlation } else { $null }
                [pscustomobject]@{ Operation=$operation; Status=$status; RequestId=$requestId; CorrelationId=$echo }
                if ($status -notin @(200, 204)) { $script:failures++ }
                if ($operation -notlike 'Select*' -and (-not $requestId -or -not $echo)) { $script:failures++ }
            } finally { $response.Dispose() }
        } catch {
            [pscustomobject]@{ Operation=$operation; Status='TRANSPORT_ERROR'; RequestId=$null; CorrelationId=$correlation }
            $script:failures++
        } finally { $request.Dispose() }
    }

    # نشست‌های تستِ موجود و مجاز؛ هیچ User/Enrollment/Role ساخته نمی‌شود.
    foreach ($role in @('Student', 'Parent', 'Teacher', 'Manager')) {
        $sessions[$role] = Read-Host "توکن نشست تستِ موجود برای $role (فقط همین ترمینال)" -AsSecureString
        $membership = Read-Identifier "MembershipId مجاز $role"
        Invoke-BackendPost "Select$role" $role '/api/v1/workspaces/select' @{
            workspaceType='Organization'; membershipId=$membership; role=$role
        }
    }
    if ($failures -gt 0) { exit 4 }
    $child = Read-Identifier 'OrganizationPersonId فرزند مجاز Parent'
    Invoke-BackendPost 'SelectChild' 'Parent' "/api/v1/guardian/children/$child/select" $null
    if ($failures -gt 0) { exit 4 }
    $assignment = Read-Identifier 'AssignmentId منتشرشده و تخصیص‌یافته به Student'
    $exam = Read-Identifier 'ExamId منتشرشده و مجاز Student'
    $teacherClass = Read-Identifier 'ClassId دارای TeacherAssignment فعال'
    $teacherStudent = Read-Identifier 'OrganizationPersonId دانش‌آموز enrolled در همان کلاس معلم'
    $managerClass = Read-Identifier 'ClassId در سازمان جاری Manager'
    $managerStudent = Read-Identifier 'OrganizationPersonId دانش‌آموز enrolled در همان کلاس مدیر'

    # عمداً ترتیبی و بدون retry؛ هر ۱۲ مشاهده باید با log امن AI تطبیق داده شود.
    Invoke-BackendPost 'Chat' 'Student' '/api/v1/ai/chat' @{message='یک توصیه عمومی برای مطالعه بده'; conversationId=$null}
    Invoke-BackendPost 'StudentAnalysis' 'Student' '/api/v1/ai/students/analyze' $null
    Invoke-BackendPost 'StudyPlan' 'Student' '/api/v1/ai/study-plan' @{goals=@('مرور درس'); availableMinutes=30; notes=$null}
    Invoke-BackendPost 'Homework' 'Student' "/api/v1/ai/homeworks/$assignment/explain" @{question='روش حل را مرحله به مرحله توضیح بده'}
    Invoke-BackendPost 'Exam' 'Student' "/api/v1/ai/exams/$exam/prepare" $null
    Invoke-BackendPost 'ParentChildSummary' 'Parent' '/api/v1/ai/parents/child-summary' $null
    Invoke-BackendPost 'TeacherClass' 'Teacher' "/api/v1/ai/teachers/classes/$teacherClass/summary" $null
    Invoke-BackendPost 'TeacherStudent' 'Teacher' "/api/v1/ai/teachers/classes/$teacherClass/students/$teacherStudent/insight" $null
    Invoke-BackendPost 'TeacherActivitySuggestions' 'Teacher' "/api/v1/ai/teachers/classes/$teacherClass/activity-suggestions" @{
        objective='یک فعالیت مرور پیشنهاد بده'; availableMinutes=30; materials=@(); notes=$null
    }
    Invoke-BackendPost 'ManagerSchool' 'Manager' '/api/v1/ai/managers/school-summary' $null
    Invoke-BackendPost 'ManagerClassInsight' 'Manager' "/api/v1/ai/managers/classes/$managerClass/insight" $null
    Invoke-BackendPost 'ManagerStudent' 'Manager' "/api/v1/ai/managers/classes/$managerClass/students/$managerStudent/insight" $null
    Write-Output 'این‌ها فقط مشاهدات HTTP هستند؛ image، inference واقعی، ماتریس منفی Service Auth و log امن هم‌بسته را جداگانه تأیید کنید.'
    if ($failures -gt 0) { exit 5 }
} finally {
    foreach ($token in $sessions.Values) { $token.Dispose() }
    $sessions.Clear()
    $http.Dispose()
    $handler.Dispose()
}
