using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MakanApp.IntegrationTests;

// شاهد آزمون فقط route ماسک‌شده، status و correlation را نگه می‌دارد؛ پاسخ خصوصی یا credential ضبط نمی‌شود.
internal sealed class AcademicAttachmentEvidence(string scenario)
{
    private readonly ConcurrentQueue<object> _requests = new();
    public string Stage { get; set; } = "SETUP";
    public bool HashMatches { get; set; }
    public bool Passed { get; set; }
    public DelegatingHandler Handler() => new Observer(this);

    public void Save()
    {
        var directory = Environment.GetEnvironmentVariable("MAKANAPP_ACADEMIC_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "MakanApp.sln"))) root = root.Parent;
        var target = Path.GetFullPath(directory);
        if (root is null || !target.StartsWith(Path.Combine(root.FullName, ".artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("شاهد آموزشی فقط در artifact خصوصی مخزن ذخیره می‌شود.");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, scenario + "-" + Guid.NewGuid().ToString("N") + ".json"),
            JsonSerializer.Serialize(new { scenario, stage = Stage, passed = Passed, hashMatches = HashMatches,
                httpRequestCount = _requests.Count, requests = _requests.ToArray(), browserUiTested = false,
                privatePayloadCaptured = false }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class Observer(AcademicAttachmentEvidence owner) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            owner._requests.Enqueue(new
            {
                method = request.Method.Method,
                route = Regex.Replace(request.RequestUri!.AbsolutePath, @"[a-fA-F0-9]{8}(?:-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12}", "{id}"),
                status = (int)response.StatusCode,
                correlationId = response.Headers.TryGetValues("X-Correlation-ID", out var values) ? values.SingleOrDefault() : null
            });
            return response;
        }
    }
}
