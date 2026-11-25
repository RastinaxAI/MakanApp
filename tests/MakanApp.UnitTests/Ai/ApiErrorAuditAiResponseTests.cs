using System.Net;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using MakanApp.Application.Ai;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed partial class AiServiceClientTests
{
    [Theory]
    [InlineData("error-envelope-only")]
    [InlineData("success-false-only")]
    [InlineData("answer-with-business-failure")]
    public async Task ErrorAuditHttp200WithBusinessFailureNeverBecomesSuccessfulAnswer(string variant)
    {
        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        const string marker = "synthetic-private-provider-failure";
        var errorEnvelope = new JsonObject { ["code"] = "AI_INTERNAL_ERROR", ["message"] = marker };
        var body = variant switch
        {
            "error-envelope-only" => new JsonObject { ["error"] = errorEnvelope },
            "success-false-only" => new JsonObject { ["success"] = false, ["error"] = errorEnvelope },
            _ => JsonNode.Parse(ValidResponse())!.AsObject()
        };
        if (variant == "answer-with-business-failure")
        {
            body["success"] = false;
            body["error"] = errorEnvelope;
        }
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, body.ToJsonString())));
        var error = await Assert.ThrowsAsync<AiServiceException>(() => CreateClient(handler).ChatAsync(Request(), default));
        Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code);
        Assert.Equal(1, handler.CallCount);
        Assert.False(error.ToString().Contains(marker, StringComparison.Ordinal));
        var directory = Environment.GetEnvironmentVariable("MAKAN_ERROR_AUDIT_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        // فقط نوع fixture و نتیجهٔ اعتبارسنجی ثبت می‌شود؛ body و credential داخل شواهد نمی‌روند.
        await File.WriteAllTextAsync(Path.Combine(directory, "business-200-" + variant + ".json"), JsonSerializer.Serialize(new
        {
            runId = "20261125-api-errors-ac9a239-01", caseId = "N07", variantId = variant,
            evidenceKind = "LOCAL_INTEGRATION_FAULT", testKind = "UNIT_ADAPTER", stage = "UPSTREAM_HTTP_STUB",
            startedAtUtc = started, elapsedMs = clock.ElapsedMilliseconds, attempt = 1, requestSequence = 1,
            httpStatus = 200, businessCode = error.Code, backendHttpStatus = (int?)null,
            transportResult = "SIMULATED_UPSTREAM_HTTP_200", businessResult = "REJECTED_NOT_SUCCESS",
            userExperienceResult = "NOT_RUN_NO_BROWSER", verdict = "PASS", retryCount = 0,
            stateBefore = "CONTROLLED_RESPONSE", stateAfter = "SAFE_APPLICATION_EXCEPTION",
            responsePayload = new { fixtureVariant = variant }, providerCallCount = handler.CallCount
        }));
    }
}
