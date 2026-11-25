using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using MakanApp.Application.Ai;
using MakanApp.Application.Assessment;
using MakanApp.Application.Messaging;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

// این fixture فقط pipeline واقعی HTTP را اجرا می‌کند؛ هیچ اتصال SQL یا provider واقعی ندارد.
public sealed class ApiErrorAuditEndpointsTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _host = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:MakanDatabase"] = "Server=(localdb)\\MSSQLLocalDB;Database=MakanApp_Audit_NoConnection;Integrated Security=True",
                ["Identity:SecurityKey"] = new string('T', 32),
                ["Sms:Provider"] = "Unavailable",
                ["Sms:MeliPayamakOtp:ServiceToken"] = "",
                ["AiService:Enabled"] = "false",
                ["Otp:Enabled"] = "true",
                ["Otp:AllowMockAuthentication"] = "false",
                ["Hosting:RequireHttps"] = "false",
                ["Swagger:Enabled"] = "false",
                ["Logging:LogLevel:Default"] = "None"
            };
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddControllers().AddApplicationPart(typeof(ApiErrorAuditFixtureController).Assembly);
            });
        });

    [Theory]
    [InlineData("missing", "/api/v1/audit-resource-does-not-exist", "GET", 404, "RESOURCE_NOT_FOUND", "application/json")]
    [InlineData("method", "/api/v1/audit-fixture/input", "GET", 405, "METHOD_NOT_ALLOWED", "application/json")]
    [InlineData("missing-html", "/api/v1/audit-resource-does-not-exist", "GET", 404, "RESOURCE_NOT_FOUND", "text/html")]
    [InlineData("method-html", "/api/v1/audit-fixture/input", "GET", 405, "METHOD_NOT_ALLOWED", "text/html")]
    [InlineData("unsupported", "/api/v1/audit-fixture/input", "POST", 415, "UNSUPPORTED_MEDIA_TYPE", "application/json")]
    public async Task FrameworkErrorsHaveStableProblemDetails(
        string variant, string path, string method, int status, string code, string accept)
    {
        using var response = await Send(variant, path, new(method), "plain text", "text/plain", accept,
            status, code, status == 405 ? "C08" : status == 415 ? "C09" : "C03");
        await AssertProblem(response, status, code);
        if (status == 405) Assert.Contains("POST", response.Content.Headers.Allow);
    }

    [Theory]
    [InlineData("malformed", "{", "C09")]
    [InlineData("missing", "{}", "C01")]
    [InlineData("null", "{\"value\":null}", "C02")]
    [InlineData("empty", "{\"value\":\"\"}", "C04")]
    [InlineData("whitespace", "{\"value\":\"   \"}", "C04")]
    [InlineData("wrong-type", "{\"value\":[]}", "C02")]
    public async Task ModelBindingKeepsFieldErrorsAndCorrelation(string variant, string payload, string caseId)
    {
        using var response = await Send(variant, "/api/v1/audit-fixture/input", HttpMethod.Post, payload,
            "application/json", "application/json", 400, "REQUEST_INVALID", caseId);
        var problem = await AssertProblem(response, 400, "REQUEST_INVALID");
        Assert.NotEmpty(problem.GetProperty("errors").EnumerateObject());
    }

    [Theory]
    [InlineData("deadline", 409, AssessmentErrorCodes.SubmissionDeadlinePassed, "C16")]
    [InlineData("file-not-ready", 409, StorageErrorCodes.FileNotReady, "U04")]
    [InlineData("file-type", 415, StorageErrorCodes.FileTypeNotAllowed, "C10")]
    [InlineData("file-size", 413, StorageErrorCodes.FileTooLarge, "C11")]
    [InlineData("pending-review", 422, MessagingErrorCodes.MessageSafetyReviewRequired, "U20")]
    [InlineData("rejected", 422, MessagingErrorCodes.MessageSafetyBlocked, "U20")]
    [InlineData("storage", 503, StorageErrorCodes.FileStorageFailed, "S06")]
    [InlineData("upstream-timeout", 504, AiServiceErrorCodes.ServiceTimeout, "S05")]
    [InlineData("upstream-auth", 503, AiServiceErrorCodes.ServiceUnauthorized, "S04")]
    [InlineData("upstream-invalid", 502, AiServiceErrorCodes.ResponseInvalid, "S03")]
    [InlineData("upstream-rate", 429, AiServiceErrorCodes.RateLimited, "C14")]
    [InlineData("forbidden", 403, MessagingErrorCodes.MessageNotAllowed, "C07")]
    [InlineData("concealed", 404, MessagingErrorCodes.MessageNotFound, "C13")]
    [InlineData("stale", 412, MessagingErrorCodes.MessageEditConflict, "C12")]
    public async Task BusinessFailuresPreserveSpecificCodeStatusAndIds(string variant, int status, string code, string caseId)
    {
        using var response = await Send(variant, "/api/v1/audit-fixture/known/" + variant,
            HttpMethod.Get, null, null, "application/json", status, code, caseId);
        var body = await AssertProblem(response, status, code);
        if (variant is "pending-review" or "rejected")
        {
            Assert.False(body.GetProperty("recipientDeliveryGranted").GetBoolean());
            Assert.False(body.GetProperty("recipientDownloadGranted").GetBoolean());
            Assert.False(body.GetProperty("retryable").GetBoolean());
        }
    }

    [Theory]
    [InlineData("rate", 429, AiServiceErrorCodes.RateLimited)]
    [InlineData("unavailable", 503, AiServiceErrorCodes.ServiceUnavailable)]
    [InlineData("knowledge-rate", 429, KnowledgeErrorCodes.RateLimited)]
    [InlineData("knowledge-unavailable", 503, KnowledgeErrorCodes.Unavailable)]
    public async Task UpstreamRetryAfterSurvivesHttpMapping(string variant, int status, string code)
    {
        using var response = await Send(variant, "/api/v1/audit-fixture/retry/" + variant,
            HttpMethod.Get, null, null, "application/json", status, code, "S04", expectedRetrySeconds: 17);
        await AssertProblem(response, status, code);
        Assert.Equal(TimeSpan.FromSeconds(17), response.Headers.RetryAfter?.Delta);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/html")]
    [InlineData("image/png")]
    public async Task RateLimitFailureKeepsProblemDetailsForNonJsonAccept(string accept)
    {
        using var limited = _host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<RateLimiterOptions>(options => options.GlobalLimiter =
                PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(_ =>
                    RateLimitPartition.GetFixedWindowLimiter("audit", _ => new FixedWindowRateLimiterOptions
                    { PermitLimit = 1, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })))));
        // ابتدا سلامت میزبان و مصرف تنها مجوز مشاهده می‌شود؛ شکست ساخت limiter با 429 اشتباه نمی‌شود.
        using var warmClient = limited.CreateClient();
        using var warm = await warmClient.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, warm.StatusCode);
        using var response = await Send("rate-accept", "/api/v1/audit-fixture/known/storage",
            HttpMethod.Get, null, null, accept, 429, "RATE_LIMITED", "C14", host: limited);
        await AssertProblem(response, 429, "RATE_LIMITED");
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/html")]
    [InlineData("image/png")]
    public async Task UnauthenticatedAccessIs401WithSafeContractForAnyAccept(string accept)
    {
        using var response = await Send("unauthenticated", "/api/v1/audit-fixture/secured",
            HttpMethod.Get, null, null, accept, 401, "AUTH_REQUIRED", "C06");
        await AssertProblem(response, 401, "AUTH_REQUIRED");
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, 0)]
    [InlineData(1250, 2)]
    public async Task RetryAfterIsNumericAndNeverShortensProviderDelay(int milliseconds, int? seconds)
    {
        using var response = await Send("retry-value-" + milliseconds, "/api/v1/audit-fixture/retry-value/" + milliseconds,
            HttpMethod.Get, null, null, "application/json", 503, AiServiceErrorCodes.ServiceUnavailable, "S04");
        await AssertProblem(response, 503, AiServiceErrorCodes.ServiceUnavailable);
        Assert.Equal(seconds.HasValue ? TimeSpan.FromSeconds(seconds.Value) : (TimeSpan?)null, response.Headers.RetryAfter?.Delta);
    }

    [Theory]
    [InlineData("HEAD", "/api/v1/audit-public-missing")]
    [InlineData("GET", "/audit-non-api-missing")]
    public async Task EmptyErrorWriterPreservesHeadAndNonApiBehavior(string method, string path)
    {
        using var client = _host.CreateClient();
        using var request = new HttpRequestMessage(new(method), path);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task AlreadyWrittenErrorBodyIsNotOverwrittenOrAppended()
    {
        using var client = _host.CreateClient();
        using var response = await client.GetAsync("/api/v1/audit-fixture/written");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EXISTING_FIXTURE_CONTRACT", body.GetProperty("code").GetString());
        Assert.Single(body.EnumerateObject());
    }

    [Fact]
    public async Task UnknownExceptionIs500WithSafeDetailsAndNeverSuccess()
    {
        using var response = await Send("unexpected", "/api/v1/audit-fixture/unexpected",
            HttpMethod.Get, null, null, "application/json", 500, "UNEXPECTED_ERROR", "S01");
        await AssertProblem(response, 500, "UNEXPECTED_ERROR");
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(body.Contains(ApiErrorAuditFixtureController.PrivateMarker, StringComparison.Ordinal));
        Assert.False(body.Contains("System.InvalidOperationException", StringComparison.Ordinal));
    }

    private async Task<HttpResponseMessage> Send(string variant, string path, HttpMethod method,
        string? payload, string? contentType, string accept, int expectedStatus, string expectedCode, string caseId,
        int? expectedRetrySeconds = null, WebApplicationFactory<Program>? host = null)
    {
        using var client = (host ?? _host).CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.ParseAdd(accept);
        request.Headers.Add("X-Correlation-ID", "audit-" + Guid.NewGuid().ToString("N"));
        if (payload is not null) request.Content = new StringContent(payload, Encoding.UTF8, contentType!);
        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        JsonElement? body = null;
        try { body = JsonSerializer.Deserialize<JsonElement>(content); }
        catch (JsonException) { }
        var actualCode = body is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("code", out var value)
            ? value.GetString() : null;
        var directory = Environment.GetEnvironmentVariable("MAKAN_ERROR_AUDIT_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            string? Text(string key) => body is { ValueKind: JsonValueKind.Object } element &&
                element.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
            var evidence = new
            {
                runId = "20261125-api-errors-ac9a239-01", caseId, variantId = variant, repetition = 1,
                attempt = 1, requestSequence = 1, interactionId = "fixture-" + variant,
                evidenceKind = "LOCAL_INTEGRATION_FAULT", startedAtUtc = started, elapsedMs = clock.Elapsed.TotalMilliseconds,
                method = method.Method, endpoint = path, requestPayload = new { fixtureVariant = variant },
                responsePayload = new { status = (int)response.StatusCode, code = actualCode },
                httpStatus = (int)response.StatusCode, businessCode = actualCode,
                traceId = Text("traceId"), correlationId = Text("correlationId"),
                retryAfterSeconds = response.Headers.RetryAfter?.Delta?.TotalSeconds,
                stateBefore = "CONFIGURED_NO_DATABASE", stateAfter = "RESPONSE_RECEIVED_NO_MUTATION",
                retryCount = 0, retrySource = "NONE", backoffMs = 0,
                expected = new { httpStatus = expectedStatus, code = expectedCode, retryAfterSeconds = expectedRetrySeconds },
                transportResult = "HTTP_RESPONSE", businessResult = actualCode ?? "MISSING_BUSINESS_CODE",
                userExperienceResult = "NOT_RUN_NO_BROWSER",
                verdict = (int)response.StatusCode == expectedStatus && actualCode == expectedCode &&
                    (!expectedRetrySeconds.HasValue || response.Headers.RetryAfter?.Delta?.TotalSeconds == expectedRetrySeconds) ? "PASS" : "FAIL",
                causeConfidence = "CONFIRMED_LOCAL_ONLY", proposedOwner = "backend"
            };
            await File.WriteAllTextAsync(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json"), JsonSerializer.Serialize(evidence));
        }
        return response;
    }

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("code", out var codeProperty), "PROBLEM_BUSINESS_CODE_MISSING");
        Assert.Equal(code, codeProperty.GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), body.GetProperty("correlationId").GetString());
        return body;
    }

    public void Dispose() => _host.Dispose();
}

// این Controller فقط در میزبان تست ثبت می‌شود؛ هیچ endpoint تشخیصی وارد محصول نمی‌شود.
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/v1/audit-fixture")]
public sealed class ApiErrorAuditFixtureController : ControllerBase
{
    public const string PrivateMarker = "synthetic-audit-private-failure-canary";

    [HttpPost("input")]
    public IActionResult Input([FromBody] ApiErrorAuditInput input) => Ok(new { accepted = true });

    [Authorize]
    [HttpGet("secured")]
    public IActionResult Secured() => Ok();

    [HttpGet("unexpected")]
    public IActionResult Unexpected() => throw new InvalidOperationException(PrivateMarker);

    [HttpGet("retry/{kind}")]
    public IActionResult Retry(string kind)
    {
        if (kind.StartsWith("knowledge-", StringComparison.Ordinal))
            throw new AiKnowledgeException(kind == "knowledge-rate" ? KnowledgeErrorCodes.RateLimited : KnowledgeErrorCodes.Unavailable,
                kind == "knowledge-rate" ? 429 : 503, retryAfter: TimeSpan.FromSeconds(17));
        throw new AiServiceException(kind == "rate" ? AiServiceErrorCodes.RateLimited : AiServiceErrorCodes.ServiceUnavailable,
            "سرویس موقتاً پاسخ‌گو نیست.", retryAfter: TimeSpan.FromSeconds(17));
    }

    [HttpGet("retry-value/{milliseconds:int}")]
    public IActionResult RetryValue(int milliseconds) => throw new AiServiceException(
        AiServiceErrorCodes.ServiceUnavailable, "سرویس موقتاً پاسخ‌گو نیست.", retryAfter: TimeSpan.FromMilliseconds(milliseconds));

    [HttpGet("written")]
    public IActionResult Written() => BadRequest(new { code = "EXISTING_FIXTURE_CONTRACT" });

    [HttpGet("known/{kind}")]
    public IActionResult Known(string kind) => throw (kind switch
    {
        "deadline" => new AssessmentException(AssessmentErrorCodes.SubmissionDeadlinePassed, "مهلت تحویل این تکلیف به پایان رسیده است."),
        "file-not-ready" => new StorageException(StorageErrorCodes.FileNotReady, "فایل هنوز آماده نیست."),
        "file-type" => new StorageException(StorageErrorCodes.FileTypeNotAllowed, "نوع فایل مجاز نیست."),
        "file-size" => new StorageException(StorageErrorCodes.FileTooLarge, "اندازهٔ فایل بیش از حد مجاز است."),
        "pending-review" => new MessagingException(MessagingErrorCodes.MessageSafetyReviewRequired, "فایل منتظر بررسی است."),
        "rejected" => new MessagingException(MessagingErrorCodes.MessageSafetyBlocked, "فایل رد شده است."),
        "storage" => new StorageException(StorageErrorCodes.FileStorageFailed, "ذخیرهٔ فایل در دسترس نیست."),
        "upstream-timeout" => new AiServiceException(AiServiceErrorCodes.ServiceTimeout, "مهلت پاسخ پایان یافت."),
        "upstream-auth" => new AiServiceException(AiServiceErrorCodes.ServiceUnauthorized, "سرویس در دسترس نیست."),
        "upstream-invalid" => new AiServiceException(AiServiceErrorCodes.ResponseInvalid, "پاسخ سرویس معتبر نیست."),
        "upstream-rate" => new AiServiceException(AiServiceErrorCodes.RateLimited, "کمی بعد تلاش کنید."),
        "forbidden" => new MessagingException(MessagingErrorCodes.MessageNotAllowed, "اجازهٔ این عملیات را ندارید."),
        "concealed" => new MessagingException(MessagingErrorCodes.MessageNotFound, "مورد درخواستی در دسترس نیست."),
        "stale" => new MessagingException(MessagingErrorCodes.MessageEditConflict, "نسخهٔ تازه را دریافت کنید."),
        _ => new InvalidOperationException("UNKNOWN_TEST_VARIANT")
    });
}

public sealed record ApiErrorAuditInput([Required] string Value);
