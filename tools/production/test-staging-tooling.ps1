[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$smokePath = Join-Path $PSScriptRoot 'staging-core-smoke.ps1'
$migrationPath = Join-Path $PSScriptRoot 'validate-local-migrations.ps1'
$parseErrors = $null
$tokens = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($smokePath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw 'syntax ابزار smoke معتبر نیست.' }
[void][Management.Automation.Language.Parser]::ParseFile($migrationPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw 'syntax ابزار migration معتبر نیست.' }

$oldOptIn = $env:MAKAN_STAGING_LIVE_OTP
try {
    $env:MAKAN_STAGING_LIVE_OTP = $null
    $rejected = $false
    try { & $smokePath -BaseUrl 'https://staging.example.test' -ApplicationCommit ('a' * 40) -LiveOtp -ConfirmIsolatedStaging }
    catch { $rejected = $true }
    if (-not $rejected) { throw 'نبود تأیید environment مانع اجرا نشد.' }
    $env:MAKAN_STAGING_LIVE_OTP = 'YES'
    foreach ($unsafeUrl in @('http://staging.example.test', 'https://staging.example.test/path', 'https://staging.example.test/?x=marker', 'https://marker@staging.example.test')) {
        $rejected = $false
        try { & $smokePath -BaseUrl $unsafeUrl -ApplicationCommit ('a' * 40) -LiveOtp -ConfirmIsolatedStaging }
        catch { $rejected = $true }
        if (-not $rejected) { throw 'URL ناامن پذیرفته شد.' }
    }
    $rejected = $false
    try { & $migrationPath } catch { $rejected = $true }
    if (-not $rejected) { throw 'نبود تأیید دیتابیس مانع اجرا نشد.' }
}
finally { $env:MAKAN_STAGING_LIVE_OTP = $oldOptIn }

# تنها helper HTTP از سورس مورداعتماد بارگذاری می‌شود؛ بدنه اصلی یا WebSocket اجرا نمی‌شود.
$helper = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-Check' }, $true)
Invoke-Expression $helper.Extent.Text
Add-Type -AssemblyName System.Net.Http
Add-Type -ReferencedAssemblies @('System.Net.Http', 'System.Runtime') -TypeDefinition @'
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
public sealed class StagingOfflineHandler : HttpMessageHandler
{
    public int Requests;
    public bool SawAuthorization;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Requests++;
        SawAuthorization |= request.Headers.Authorization != null;
        var fail = request.RequestUri.AbsolutePath.EndsWith("failure");
        var response = new HttpResponseMessage(fail ? HttpStatusCode.BadRequest : HttpStatusCode.Accepted);
        response.Content = new StringContent(fail ? "{\"code\":\"OFFLINE_FAILURE\",\"private\":\"not-real-secret-marker\"}" : "{\"challengeId\":\"offline-marker\"}");
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        return Task.FromResult(response);
    }
}
'@
$fake = New-Object StagingOfflineHandler
$client = New-Object System.Net.Http.HttpClient($fake)
$client.BaseAddress = [Uri]'https://staging.example.test'
$steps = New-Object 'System.Collections.Generic.List[object]'
$token = 'not-real-session-marker'
try {
    $response = Invoke-Check -Name 'Offline 202 contract' -Path '/offline' -Method POST -Expected 202 -Body @{ phoneNumber = 'not-real-recipient' } -Authenticated
    if ($response.Json.challengeId -ne 'offline-marker' -or -not $fake.SawAuthorization) { throw 'قرارداد HTTP helper معتبر نیست.' }
    $failure = Invoke-Check -Name 'Offline expected rejection' -Path '/offline/failure' -Expected 400
    if ($failure.Json.code -ne 'OFFLINE_FAILURE') { throw 'خطای کنترل‌شده قابل بررسی نیست.' }
    $rejected = $false
    try { $null = Invoke-Check -Name 'Offline unexpected rejection' -Path '/offline/failure' }
    catch {
        $rejected = $true
        if ($_.ToString().Contains('not-real-secret-marker')) { throw 'body خطا افشا شد.' }
    }
    if (-not $rejected -or $fake.Requests -ne 3) { throw 'خطای غیرمنتظره به درستی مدیریت نشد.' }
}
finally { $client.Dispose(); $fake.Dispose(); $token = $null }
Write-Output 'PASS: syntax, explicit opt-in, unsafe URL rejection, migration consent, fake HTTP 202/auth/rejection/redaction. No network or SMS sent.'
