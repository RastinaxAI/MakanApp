using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MakanApp.IntegrationTests;

// فقط سرور تست Loopback است؛ هیچ adapter یا credential واقعی Provider ندارد.
public sealed class ControlledAiV1Server : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly TimeProvider _clock;
    private readonly ConcurrentQueue<JsonObject> _requests = new();
    private readonly ConcurrentQueue<AiContractTestEvent> _events = new();
    public string Key { get; }
    public const string KeyId = "controlled-local-v1";
    public string BaseUrl { get; private set; } = "";
    public int BusinessCalls => _requests.Count;
    public int? NextStatus { get; set; }
    public string? InvalidResponse { get; set; }
    public string? RedirectLocation { get; set; }
    public TimeSpan ResponseDelay { get; set; }
    public IReadOnlyList<JsonObject> Requests => _requests.ToArray();
    public IReadOnlyList<AiContractTestEvent> Events => _events.ToArray();

    private ControlledAiV1Server(WebApplication app, string key, TimeProvider clock)
    { _app = app; Key = key; _clock = clock; }

    public static async Task<ControlledAiV1Server> StartAsync(TimeProvider? clock = null, string? key = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test", Args = [] });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var server = new ControlledAiV1Server(app, key ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), clock ?? TimeProvider.System);
        app.MapPost("/api/v1/ai/{**operation}", server.HandleAsync);
        await app.StartAsync();
        server.BaseUrl = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        if (!new Uri(server.BaseUrl).IsLoopback) throw new InvalidOperationException("فقط Loopback مجاز است.");
        return server;
    }

    private async Task HandleAsync(HttpContext http)
    {
        var operation = http.Request.RouteValues["operation"]?.ToString() ?? "";
        var auth = Authenticate(http.Request);
        if (auth != 200) { await Failure(auth, auth == 401 ? "AI_SERVICE_UNAUTHORIZED" : "AI_SERVICE_FORBIDDEN"); return; }
        if (http.Request.Headers["X-AI-Contract-Version"].Count != 1 || http.Request.Headers["X-AI-Contract-Version"] != "v1" ||
            http.Request.ContentType?.Split(';')[0] != "application/json")
        { await Failure(422, "AI_INVALID_REQUEST"); return; }
        JsonObject body;
        try { body = (await JsonNode.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted))?.AsObject() ?? throw new JsonException(); }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        { await Failure(422, "AI_INVALID_REQUEST"); return; }
        var requestId = body["requestId"]?.GetValue<string>();
        var correlation = body["correlationId"]?.GetValue<string>();
        var header = http.Request.Headers["X-Correlation-ID"];
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128 || header.Count > 1 ||
            header.Count == 1 && (string.IsNullOrWhiteSpace(header[0]) || header[0] != correlation))
        { await Failure(422, "AI_INVALID_REQUEST"); return; }
        var fields = AllowedFields(operation);
        if (fields is null || body.Any(p => !fields.Contains(p.Key)) || body["actor"] is not JsonObject actor ||
            string.IsNullOrWhiteSpace(actor["userId"]?.GetValue<string>()) || body["scope"] is not JsonObject scope)
        { await Failure(422, "AI_INVALID_REQUEST"); return; }
        var role = actor["role"]?.GetValue<string>();
        var expected = operation.StartsWith("teachers/") ? "Teacher" : operation.StartsWith("managers/") ? "Manager"
            : operation.StartsWith("parents/") ? "Parent" : operation == "chat" ? role : "Student";
        if (role != expected || role is not ("Student" or "Parent" or "Teacher" or "Manager") ||
            scope["organizationId"]?.GetValue<string>() != scope["schoolId"]?.GetValue<string>() ||
            string.IsNullOrWhiteSpace(scope["organizationId"]?.GetValue<string>()))
        { await Failure(422, "AI_INVALID_CONTEXT"); return; }
        _requests.Enqueue(body.DeepClone().AsObject());
        if (RedirectLocation is not null) { http.Response.Redirect(RedirectLocation); return; }
        if (ResponseDelay > TimeSpan.Zero) await Task.Delay(ResponseDelay, http.RequestAborted);
        if (NextStatus is int status) { NextStatus = null; await Failure(status, status switch { 429 => "AI_RATE_LIMITED", 503 => "AI_MODEL_UNAVAILABLE", 504 => "AI_PROVIDER_TIMEOUT", _ => "AI_INTERNAL_ERROR" }); return; }
        var response = Response(operation, body);
        if (InvalidResponse == "correlation") response["correlationId"] = "wrong-correlation";
        if (InvalidResponse == "alias") response["metadata"]!["modelAlias"] = "unexpected-model";
        if (InvalidResponse == "grounding") response["sources"] = new JsonArray(new JsonObject { ["type"] = "Homework", ["id"] = Guid.NewGuid().ToString("D") });
        if (InvalidResponse == "schema") response.Remove("metadata");
        _events.Enqueue(new(operation, 200, null, requestId, correlation, response["metadata"]?["modelAlias"]?.GetValue<string>()));
        await http.Response.WriteAsJsonAsync(response, http.RequestAborted);

        async Task Failure(int status, string code)
        {
            http.Response.StatusCode = status;
            _events.Enqueue(new(operation, status, code, null, null));
            await http.Response.WriteAsJsonAsync(new { error = new { code, message = "Controlled failure" } }, http.RequestAborted);
        }
    }

    private int Authenticate(HttpRequest request)
    {
        var values = request.Headers.Authorization;
        if (values.Count != 1 || values[0] is not string value || !value.StartsWith("Bearer ", StringComparison.Ordinal)) return 401;
        var token = value[7..]; if (token.Length > 4096) return 401;
        try
        {
            var parts = token.Split('.'); if (parts.Length != 3) return 401;
            using var head = JsonDocument.Parse(Decode(parts[0])); using var payload = JsonDocument.Parse(Decode(parts[1]));
            var h = head.RootElement; var p = payload.RootElement;
            if (h.GetProperty("alg").GetString() != "HS256" || h.GetProperty("typ").GetString() != "JWT" || h.GetProperty("kid").GetString() != KeyId) return 401;
            var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key), Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]));
            if (!CryptographicOperations.FixedTimeEquals(signature, Decode(parts[2]))) return 401;
            var allowed = new HashSet<string>(["iss", "aud", "sub", "scope", "iat", "nbf", "exp", "jti"]);
            var names = p.EnumerateObject().Select(x => x.Name).ToArray();
            if (names.Distinct().Count() != names.Length || names.Any(n => !allowed.Contains(n))) return 401;
            var now = _clock.GetUtcNow().ToUnixTimeSeconds(); var iat = p.GetProperty("iat").GetInt64(); var exp = p.GetProperty("exp").GetInt64();
            if (p.GetProperty("iss").GetString() != "makan-backend" || p.GetProperty("aud").GetString() != "makan-ai" ||
                p.GetProperty("sub").GetString() != "makan-backend" || string.IsNullOrWhiteSpace(p.GetProperty("jti").GetString()) ||
                exp <= now || p.GetProperty("nbf").GetInt64() > now + 5 || iat > now + 5 || exp <= iat || exp - iat > 300) return 401;
            return p.TryGetProperty("scope", out var scope) && scope.GetString()?.Split(' ').Contains("makan.ai.invoke") == true ? 200 : 403;
        }
        catch (Exception e) when (e is JsonException or FormatException or KeyNotFoundException or InvalidOperationException or ArgumentException) { return 401; }
    }

    private static HashSet<string>? AllowedFields(string operation)
    {
        var extra = operation switch
        {
            "chat" => new[] { "conversationId", "message", "context" },
            "students/analyze" => ["studentData", "language"],
            "study-plan" => ["studentData", "goals", "preferences", "language"],
            "homeworks/explain" => ["homework", "question", "context", "language"],
            "exams/prepare" => ["exam", "studentData", "context", "language"],
            "parents/child-summary" => ["childData", "language"],
            "teachers/class-summary" => ["classData", "language"],
            "teachers/student-insight" => ["studentData", "language"],
            "teachers/activity-suggestions" => ["classData", "objective", "constraints", "language"],
            "managers/school-summary" => ["schoolData", "language"],
            "managers/class-insight" => ["classData", "language"],
            "managers/student-insight" => ["studentData", "language"],
            _ => null
        };
        return extra is null ? null : new HashSet<string>(extra.Concat(["requestId", "correlationId", "actor", "scope"]));
    }

    private static JsonObject Response(string operation, JsonObject request)
    {
        object content = operation switch
        {
            "chat" => new { answer = new { text = "پاسخ کنترل‌شده", language = "fa" }, insights = Array.Empty<object>(), risks = Array.Empty<object>() },
            "students/analyze" => new { summary = "خلاصه", strengths = Array.Empty<object>(), weaknesses = Array.Empty<object>(), risks = Array.Empty<object>() },
            "study-plan" => new { summary = "برنامه", priorities = Array.Empty<object>(), planItems = Array.Empty<object>() },
            "homeworks/explain" => new { explanation = "توضیح", steps = Array.Empty<object>(), hints = Array.Empty<object>() },
            "exams/prepare" => new { summary = "آمادگی", priorityTopics = Array.Empty<object>(), preparationSteps = Array.Empty<object>(), risks = Array.Empty<object>() },
            "parents/child-summary" => new { academicSummary = "خلاصه", progress = new { trend = "Unknown", description = "کنترل‌شده" }, attentionAreas = Array.Empty<object>(), upcomingItems = Array.Empty<object>(), risks = Array.Empty<object>() },
            "teachers/activity-suggestions" => new { summary = "فعالیت", activities = Array.Empty<object>() },
            _ => new { summary = "بینش", strengths = Array.Empty<object>(), attentionAreas = Array.Empty<object>(), risks = Array.Empty<object>() }
        };
        var response = JsonSerializer.SerializeToNode(content)!.AsObject();
        response["requestId"] = request["requestId"]!.DeepClone(); response["correlationId"] = request["correlationId"]?.DeepClone();
        if (operation == "chat") response["conversationId"] = request["conversationId"]?.DeepClone();
        response["recommendations"] = new JsonArray(); response["sources"] = new JsonArray();
        response["metadata"] = new JsonObject { ["modelAlias"] = "education-fast", ["processingTimeMs"] = 1 };
        return response;
    }
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
    public async ValueTask DisposeAsync() { await _app.StopAsync(); await _app.DisposeAsync(); }
}
public sealed record AiContractTestEvent(string Operation, int Status, string? ErrorCode, string? RequestId, string? CorrelationId, string? ModelAlias = null);
