using System.Diagnostics.Metrics;
using System.Text.Json;

namespace MakanApp.AiIntegrationProbe;

internal sealed class LiveAiObservation : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<string> _forbidden = [];
    private readonly List<string> _forbiddenPayload = [];
    public Guid OrganizationId { get; set; }
    public Guid ForeignOrganizationId { get; set; }
    public string ExpectedRole { get; set; } = string.Empty;
    public Guid? ExpectedStudentId { get; set; }
    public Guid? ExpectedClassId { get; set; }
    public int OutgoingCalls { get; private set; }
    public string? RequestId { get; private set; }
    public string? CorrelationId { get; private set; }
    public int Measurements { get; private set; }
    public bool MetricsSafe { get; private set; } = true;
    public bool PayloadsSafe { get; private set; } = true;
    public int? AiHttpStatus { get; set; }
    public string? AiErrorCode { get; set; }
    public string? ModelAlias { get; set; }

    public LiveAiObservation()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "MakanApp.AiService")
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((_, _, tags, _) => CheckTags(tags));
        _listener.SetMeasurementEventCallback<double>((_, _, tags, _) => CheckTags(tags));
        _listener.Start();
    }

    public void Forbid(params string[] values) => _forbidden.AddRange(values.Where(value => !string.IsNullOrEmpty(value)));
    public void ForbidPayload(params string[] values) => _forbiddenPayload.AddRange(values);
    public bool IsSafe(string value) => !_forbidden.Any(item => value.Contains(item, StringComparison.Ordinal));

    private void CheckTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        Measurements++;
        foreach (var tag in tags)
        {
            MetricsSafe &= tag.Key is "operation" or "outcome" or "error.code";
            MetricsSafe &= tag.Value is string value && IsSafe(value) && value.Length <= 64;
        }
    }

    public async Task InspectAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AiHttpStatus = null;
        AiErrorCode = null;
        ModelAlias = null;
        // محتوا فقط در حافظه بررسی می‌شود؛ نه متن payload و نه token در خروجی قرار نمی‌گیرد.
        Forbid(request.Headers.Authorization?.Parameter ?? string.Empty);
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var scope = root.GetProperty("scope");
        var valid = IsSafe(body) && !_forbiddenPayload.Any(value => body.Contains(value, StringComparison.Ordinal)) &&
            !body.Contains(ForeignOrganizationId.ToString("D"), StringComparison.Ordinal) &&
            root.GetProperty("actor").GetProperty("role").GetString() == ExpectedRole &&
            scope.GetProperty("organizationId").GetGuid() == OrganizationId &&
            scope.GetProperty("schoolId").GetGuid() == OrganizationId;
        if (ExpectedStudentId.HasValue)
            valid &= scope.GetProperty("studentId").GetGuid() == ExpectedStudentId;
        if (ExpectedClassId.HasValue)
            valid &= scope.GetProperty("classId").GetGuid() == ExpectedClassId;
        RequestId = root.GetProperty("requestId").GetString();
        CorrelationId = root.GetProperty("correlationId").GetString();
        valid &= request.Headers.GetValues("X-Correlation-ID").Single() == CorrelationId &&
            request.Headers.GetValues("X-AI-Contract-Version").Single() == "v1" &&
            request.Content.Headers.ContentType?.MediaType == "application/json" &&
            request.Headers.Authorization?.Scheme == "Bearer";
        if (root.TryGetProperty("homework", out var homework))
            valid &= homework.GetProperty("maxScore").GetDecimal() == 10m;
        if (root.TryGetProperty("exam", out var exam))
            valid &= exam.GetProperty("maxScore").GetDecimal() == 30m;
        valid &= SafeFields(root);
        PayloadsSafe &= valid;
        if (!valid || OutgoingCalls >= 15)
            throw new InvalidOperationException("LIVE_AI_OUTGOING_GUARD_FAILED");
        OutgoingCalls++;
    }

    private static bool SafeFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.ToLowerInvariant() is "accesstoken" or "refreshtoken" or "sessiontoken" or
                    "otp" or "phonenumber" or "contacts" or "password" or "connectionstring" or
                    "messengerhistory" or "moderationevidence" or "childsafetyevidence" or
                    "paymentsecret" or "platformaudit" or "chathistory" or "rawconversation")
                    return false;
                if (!SafeFields(property.Value)) return false;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (!SafeFields(item)) return false;
        }
        return true;
    }

    public void Dispose() => _listener.Dispose();
}

internal sealed class LiveAiObservationHandler(LiveAiObservation observation) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await observation.InspectAsync(request, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        observation.AiHttpStatus = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            // فقط envelope امن قرارداد ثبت می‌شود؛ بدنه یا پیام provider چاپ نمی‌شود.
            var errorCode = LiveAiErrorContract.Read(await response.Content.ReadAsStringAsync(cancellationToken),
                response.Content.Headers.ContentType?.MediaType).Code;
            observation.AiErrorCode = errorCode;
            LiveAiProgram.EvidenceOutput.WriteLine(JsonSerializer.Serialize(new { stage = "real_ai_upstream_failure",
                httpStatus = (int)response.StatusCode, errorCode }));
        }
        else
        {
            // فقط alias محدود نگه داشته می‌شود؛ هیچ پاسخ یا محتوای آموزشی ذخیره نمی‌شود.
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var alias = document.RootElement.GetProperty("metadata").GetProperty("modelAlias").GetString();
            observation.ModelAlias = alias == "education-fast" ? alias : "other";
        }
        return response;
    }
}
