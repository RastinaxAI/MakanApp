using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MakanApp.Application.Ai;
using Microsoft.Extensions.Logging;

namespace MakanApp.AiIntegrationProbe;

// این observer فقط در حالت تشخیصی نصب می‌شود و هیچ header محرمانه‌ای دریافت نمی‌کند.
internal sealed class FailureEvidenceCapture(Func<string, bool> isSafe)
{
    private readonly List<FailureWireEvidence> _cases = [];
    private readonly Dictionary<string, string> _identifiers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _traceIds = new(StringComparer.Ordinal);
    private FailureWireEvidence? _current;
    public int Count => _cases.Count;

    public void Begin(string operation, string publicRoute)
    {
        _current = new FailureWireEvidence { Operation = operation, BackendRoute = SanitizeRoute(publicRoute) };
        _cases.Add(_current);
    }

    public async Task RequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var current = Current();
        var json = await request.Content!.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        current.RequestId = document.RootElement.GetProperty("requestId").GetString();
        current.CorrelationId = document.RootElement.GetProperty("correlationId").GetString();
        // این traceها را خود Backend و runner می‌سازند و برای lookup تیم AI نگه می‌داریم.
        foreach (var value in new[] { current.RequestId, current.CorrelationId })
        {
            if (value is null || !Regex.IsMatch(value, "^[A-Za-z0-9_.:-]{1,128}$") || !isSafe(value))
                throw new InvalidOperationException("UNSAFE_DIAGNOSTIC_TRACE");
            _traceIds.Add(value);
        }
        current.Method = request.Method.Method;
        current.AiRoute = request.RequestUri!.AbsolutePath;
        current.RequestJson = Sanitize(json);
        current.StartedUtc = DateTimeOffset.UtcNow;
        current.StartedTimestamp = Stopwatch.GetTimestamp();
    }

    public async Task ResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var current = Current();
        current.AiStatus = (int)response.StatusCode;
        current.ContentType = response.Content.Headers.ContentType?.ToString();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            current.ResponseJson = Sanitize(json);
            current.ResponseJsonValid = true;
            var root = current.ResponseJson.Value;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                current.ErrorResponse = error.Clone();
                current.AiErrorCode = StringField(error, "code");
                current.ErrorRequestIdMatches = StringField(error, "requestId") == current.RequestId;
            }
        }
        catch (JsonException)
        {
            // بدنه غیر JSON ممکن است secret یا stack trace داشته باشد؛ هرگز چاپ نمی‌شود.
            current.ResponseJsonValid = false;
        }
        current.AiDurationMs = Math.Round(Stopwatch.GetElapsedTime(current.StartedTimestamp).TotalMilliseconds, 2);
    }

    public void Backend(int status, string json, long startedTimestamp)
    {
        var current = Current();
        current.BackendStatus = status;
        current.BackendJson = Sanitize(json);
        current.BackendCode = StringField(current.BackendJson.Value, "code");
        current.BackendDurationMs = Math.Round(Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds, 2);
        current.ValidationCategory = current.AiStatus is null
            ? "TransportOrTimeout_NoAiHttpResponse" : current.AiStatus >= 400
            ? "UpstreamErrorResponse_StatusMapping_SuccessDtoValidationNotReached"
            : current.BackendCode == AiServiceErrorCodes.ResponseInvalid
                ? "BackendStrictResponseValidation" : "NoBackendValidationFailureObserved";
    }

    public void Exception(Exception? exception)
    {
        if (_current is null || exception is not AiServiceException ai) return;
        _current.BackendExceptionType = ai.GetType().FullName;
        _current.BackendInnerExceptionType = ai.InnerException?.GetType().FullName;
        if (ai.InnerException is JsonException json)
        {
            _current.BackendJsonPath = json.Path is not null &&
                Regex.IsMatch(json.Path, "^[A-Za-z0-9_$.[\\]'-]{1,256}$") ? json.Path : null;
            _current.BackendJsonLine = json.LineNumber;
            _current.BackendJsonBytePosition = json.BytePositionInLine;
        }
    }

    public string Export()
    {
        if (_cases.Count != 3 || _cases.Any(item => item.RequestJson is null || item.BackendJson is null))
            throw new InvalidOperationException("DIAGNOSTIC_WIRE_EVIDENCE_INCOMPLETE");
        var output = JsonSerializer.Serialize(new { stage = "sanitized_failure_evidence", cases = _cases },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (!isSafe(output)) throw new InvalidOperationException("DIAGNOSTIC_SANITIZATION_FAILED");
        return output;
    }

    private FailureWireEvidence Current() => _current ?? throw new InvalidOperationException("DIAGNOSTIC_CASE_NOT_STARTED");
    private static string? StringField(JsonElement element, string name) =>
        element.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;

    private string SanitizeRoute(string route) => string.Join('/', route.Split('/').Select(value =>
        Guid.TryParse(value, out _) ? Identifier(value) : value));

    private string Identifier(string value)
    {
        if (!_identifiers.TryGetValue(value, out var replacement))
        {
            replacement = "00000000-0000-0000-0000-" + (_identifiers.Count + 101).ToString("D12", CultureInfo.InvariantCulture);
            _identifiers.Add(value, replacement);
        }
        return replacement;
    }

    private JsonElement Sanitize(string json)
    {
        using var input = JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) WriteSanitized(writer, input.RootElement, "$", string.Empty);
        using var result = JsonDocument.Parse(buffer.ToArray());
        if (!SameShape(input.RootElement, result.RootElement))
            throw new InvalidOperationException("DIAGNOSTIC_JSON_SHAPE_CHANGED");
        return result.RootElement.Clone();
    }

    private void WriteSanitized(Utf8JsonWriter writer, JsonElement element, string path, string name)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteSanitized(writer, property.Value, path + "." + property.Name, property.Name);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteSanitized(writer, item, path + "[]", name);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(SanitizeString(element.GetString()!, path, name));
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private string SanitizeString(string value, string path, string name)
    {
        if (isSafe(value))
        {
            if (name is "requestId" or "correlationId" && _traceIds.Contains(value)) return value;
            if (name == "code" && Regex.IsMatch(value, "^AI_[A-Z_]{1,64}$")) return value;
            if (name is "role" or "severity" or "priority" or "trend" or "type" or "assessmentType" &&
                value is "Student" or "Parent" or "Teacher" or "Manager" or "Info" or "Low" or "Medium" or
                    "High" or "Critical" or "Improving" or "Stable" or "Declining" or "Unknown" or
                    "Assignment" or "Homework" or "Exam" or "Class" or "Subject" or "Grade") return value;
            if (name == "language" && value is "fa" or "en") return value;
            if (name == "modelAlias" && value == "education-fast") return value;
            if (name == "scoreScale" && value == "0-100 normalized percentage") return value;
            if (name == "message" && path.StartsWith("$.error.", StringComparison.Ordinal) &&
                value is "AI structured response is invalid." or "AI provider request failed." or
                    "AI provider is unavailable." or "AI provider request timed out." or
                    "AI provider rate limit exceeded." or "Invalid AI request." or
                    "Invalid service credential." or "Insufficient service permission." or
                    "Invalid Student request context." or "Invalid Parent request context." or
                    "Invalid Teacher request context." or "Invalid Manager request context.") return value;
            if (name == "errorCode" && Regex.IsMatch(value, "^AI_[A-Z_]{1,64}$")) return value;
            if (Regex.IsMatch(value, "^\\d{4}-\\d{2}-\\d{2}T[0-9:.+-]+Z?$") &&
                DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return value;
        }
        if (Guid.TryParse(value, out _)) return Identifier(value);
        return name switch
        {
            "classTitle" => "Sample Class",
            "courseTitle" => "Sample Course",
            "title" when path.Contains("homeworks", StringComparison.Ordinal) => "Sample Assignment",
            "title" when path.Contains("exams", StringComparison.Ordinal) => "Sample Exam",
            "title" => "Sample Title",
            "message" => "Sanitized public message.",
            _ => "Synthetic educational text."
        };
    }

    private static bool SameShape(JsonElement before, JsonElement after)
    {
        if (before.ValueKind != after.ValueKind) return false;
        if (before.ValueKind == JsonValueKind.Object)
        {
            var original = before.EnumerateObject().ToArray();
            var sanitized = after.EnumerateObject().ToArray();
            return original.Length == sanitized.Length && original.Zip(sanitized).All(pair =>
                pair.First.Name == pair.Second.Name && SameShape(pair.First.Value, pair.Second.Value));
        }
        if (before.ValueKind == JsonValueKind.Array)
            return before.GetArrayLength() == after.GetArrayLength() &&
                before.EnumerateArray().Zip(after.EnumerateArray()).All(pair => SameShape(pair.First, pair.Second));
        if (before.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)
            return before.GetRawText() == after.GetRawText();
        return true;
    }

    public static void VerifySanitization()
    {
        const string canary = "diagnostic-sensitive-canary";
        var capture = new FailureEvidenceCapture(value => !value.Contains(canary, StringComparison.Ordinal));
        capture._traceIds.Add("synthetic-request-trace");
        const string input = """
            {"requestId":"synthetic-request-trace","actor":{"userId":"11111111-1111-1111-1111-111111111111","role":"Parent"},
             "scope":{"organizationId":"22222222-2222-2222-2222-222222222222","schoolId":"22222222-2222-2222-2222-222222222222","classId":null},
             "data":{"score":17.25,"maxScore":30,"active":true,"finished":false,"values":[null,{},[],"diagnostic-sensitive-canary"]},
             "error":{"code":"AI_RESPONSE_INVALID","message":"AI structured response is invalid.","requestId":"synthetic-request-trace"}}
            """;
        var sanitized = capture.Sanitize(input);
        var json = sanitized.GetRawText();
        if (json.Contains(canary, StringComparison.Ordinal) || json.Contains("11111111", StringComparison.Ordinal) ||
            sanitized.GetProperty("actor").GetProperty("role").GetString() != "Parent" ||
            sanitized.GetProperty("scope").GetProperty("organizationId").GetString() !=
                sanitized.GetProperty("scope").GetProperty("schoolId").GetString() ||
            sanitized.GetProperty("error").GetProperty("message").GetString() != "AI structured response is invalid." ||
            sanitized.GetProperty("requestId").GetString() != "synthetic-request-trace")
            throw new InvalidOperationException("DIAGNOSTIC_SANITIZER_SELF_CHECK_FAILED");
    }
}

internal sealed class FailureWireEvidence
{
    public string Operation { get; init; } = string.Empty;
    public string BackendRoute { get; init; } = string.Empty;
    public string? Method { get; set; }
    public string? AiRoute { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public long StartedTimestamp { get; set; }
    public string? RequestId { get; set; }
    public string? CorrelationId { get; set; }
    public JsonElement? RequestJson { get; set; }
    public int? AiStatus { get; set; }
    public string? ContentType { get; set; }
    public double AiDurationMs { get; set; }
    public bool ResponseJsonValid { get; set; }
    public JsonElement? ResponseJson { get; set; }
    public JsonElement? ErrorResponse { get; set; }
    public string? AiErrorCode { get; set; }
    public bool? ErrorRequestIdMatches { get; set; }
    public int? BackendStatus { get; set; }
    public string? BackendCode { get; set; }
    public double BackendDurationMs { get; set; }
    public JsonElement? BackendJson { get; set; }
    public string? ValidationCategory { get; set; }
    public string? BackendExceptionType { get; set; }
    public string? BackendInnerExceptionType { get; set; }
    public string? BackendJsonPath { get; set; }
    public long? BackendJsonLine { get; set; }
    public long? BackendJsonBytePosition { get; set; }
}

internal sealed class FailureEvidenceLoggerProvider(FailureEvidenceCapture capture) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SafeLogger(capture);
    public void Dispose() { }
    private sealed class SafeLogger(FailureEvidenceCapture capture) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => capture.Exception(exception);
    }
}
