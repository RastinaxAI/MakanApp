using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using MakanApp.Application.Ai;
using MakanApp.Infrastructure.Ai;

namespace MakanApp.AiIntegrationProbe;

internal static class ServiceAuthProbe
{
    public static async Task RunAsync(Uri target, IAiServiceCredentialProvider credentials,
        AiServiceAuthenticationOptions options)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = target, Timeout = TimeSpan.FromSeconds(15) };
        var valid = await credentials.GetCredentialAsync(CancellationToken.None);
        var parts = valid.Token.Split('.');
        var header = Decode(parts[0]);
        var payload = Decode(parts[1]);
        var passed = await CheckAsync("missing_auth", null, 401, "AI_SERVICE_UNAUTHORIZED");
        passed &= await CheckAsync("malformed_token", "not-a-jwt", 401, "AI_SERVICE_UNAUTHORIZED");
        // بدون کنترل مثبت، 401های بعدی اثبات بررسی claim یا signature نیستند.
        if (!await CheckAsync("current_key", valid.Token, 422, "AI_INVALID_REQUEST"))
        {
            LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new
            {
                stage = "service_auth_completed", passed = false, reason = "current_credential_rejected",
                claimNegativeTests = "NOT_RUN", contractHeaderTests = "NOT_RUN"
            }));
            throw new InvalidOperationException("SERVICE_AUTH_REAL_CONTROL_FAILED");
        }
        passed &= await CheckAsync("expired", Mutate(expired: true), 401, "AI_SERVICE_UNAUTHORIZED");
        passed &= await CheckAsync("bad_signature", Mutate(badSignature: true), 401, "AI_SERVICE_UNAUTHORIZED");
        passed &= await CheckAsync("unknown_kid", Mutate(headerField: "kid", value: "unknown-integration-key"), 401, "AI_SERVICE_UNAUTHORIZED");
        foreach (var field in new[] { "iss", "aud", "sub" })
            passed &= await CheckAsync("wrong_" + field, Mutate(payloadField: field, value: "wrong-integration-identity"), 401, "AI_SERVICE_UNAUTHORIZED");
        passed &= await CheckAsync("missing_scope", Mutate(payloadField: "scope", value: "unrelated.scope"), 403, "AI_SERVICE_FORBIDDEN");
        var contractBody = JsonSerializer.Serialize(new
        {
            requestId = "step10g-contract-" + Guid.NewGuid().ToString("N"), correlationId = "step10g-contract-body",
            actor = new { userId = Guid.NewGuid().ToString("D"), role = "Student" },
            scope = new { organizationId = "11111111-1111-1111-1111-111111111111", schoolId = "11111111-1111-1111-1111-111111111111", studentId = "22222222-2222-2222-2222-222222222222" },
            message = new { text = "Synthetic contract validation probe", language = "en" }
        });
        // بدنهٔ معتبر اجازه نمی‌دهد خطای body به‌جای رد header گزارش شود.
        // اگر سرویس guard نداشته باشد ممکن است inference رخ دهد؛ تعداد این probeها محدود است.
        passed &= await CheckAsync("missing_contract_header", valid.Token, 422, "AI_INVALID_REQUEST", version: null, body: contractBody);
        passed &= await CheckAsync("wrong_contract_header", valid.Token, 422, "AI_INVALID_REQUEST", version: "v2", body: contractBody);
        passed &= await CheckAsync("correlation_mismatch", valid.Token, 422, "AI_INVALID_REQUEST", correlation: "step10g-contract-header", body: contractBody);
        LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new { stage = "service_auth_completed", passed }));
        if (!passed) throw new InvalidOperationException("SERVICE_AUTH_REAL_GATE_FAILED");
        return;

        string Mutate(string? headerField = null, string? payloadField = null, string? value = null,
            bool expired = false, bool badSignature = false)
        {
            var changedHeader = new Dictionary<string, JsonElement>(header);
            var changedPayload = new Dictionary<string, JsonElement>(payload);
            if (headerField is not null) changedHeader[headerField] = JsonSerializer.SerializeToElement(value);
            if (payloadField is not null) changedPayload[payloadField] = JsonSerializer.SerializeToElement(value);
            if (expired)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                changedPayload["iat"] = JsonSerializer.SerializeToElement(now - 900);
                changedPayload["nbf"] = JsonSerializer.SerializeToElement(now - 900);
                changedPayload["exp"] = JsonSerializer.SerializeToElement(now - 780);
            }
            var unsigned = Encode(JsonSerializer.SerializeToUtf8Bytes(changedHeader)) + "." +
                Encode(JsonSerializer.SerializeToUtf8Bytes(changedPayload));
            var key = badSignature ? RandomNumberGenerator.GetBytes(32) : Encoding.UTF8.GetBytes(options.CurrentKey);
            return unsigned + "." + Encode(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(unsigned)));
        }

        async Task<bool> CheckAsync(string name, string? token, int expectedStatus, string expectedCode,
            string? version = "v1", string? correlation = null, string body = "{}")
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1100));
            var started = Stopwatch.GetTimestamp();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/chat")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (version is not null) request.Headers.Add("X-AI-Contract-Version", version);
            if (correlation is not null) request.Headers.Add("X-Correlation-ID", correlation);
            using var response = await client.SendAsync(request);
            var (code, messagePresent) = LiveAiErrorContract.Read(await response.Content.ReadAsStringAsync(),
                response.Content.Headers.ContentType?.MediaType);
            var success = (int)response.StatusCode == expectedStatus && code == expectedCode &&
                messagePresent;
            LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new
            {
                stage = "service_auth_" + name, httpStatus = (int)response.StatusCode,
                errorCode = code, bodyWasIntentionallyInvalid = body == "{}",
                elapsedMs = Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 2), passed = success
            }));
            return success;
        }
    }

    private static Dictionary<string, JsonElement> Decode(string segment)
    {
        var value = segment.Replace('-', '+').Replace('_', '/');
        value = value.PadRight(value.Length + (4 - value.Length % 4) % 4, '=');
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Convert.FromBase64String(value))!;
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
