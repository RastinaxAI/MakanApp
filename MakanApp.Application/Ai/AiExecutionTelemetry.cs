using System.Diagnostics;
using System.Diagnostics.Metrics;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Ai;

// سنجهٔ اجرای Use Case از سنجهٔ transport موجود جداست؛ هیچ شناسه، متن یا Exception در tag قرار نمی‌گیرد.
public static class AiExecutionTelemetry
{
    private static readonly Meter Meter = new("MakanApp.AiService", "1.0.0");
    private static readonly Counter<long> Results = Meter.CreateCounter<long>("makan.ai.execution.results");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("makan.ai.execution.duration", "ms");

    public static Measurement Begin(string operation, WorkspaceType context, AiPayerSelection? payer) => new(
        operation is "chat" or "knowledge_retrieve" or "knowledge_rag" ? operation : "role_specific",
        context == WorkspaceType.Personal ? "Personal" : "Organization",
        (payer ?? (context == WorkspaceType.Personal ? AiPayerSelection.Individual : AiPayerSelection.Organization)) switch
        {
            AiPayerSelection.Individual => "Individual",
            AiPayerSelection.FreeTrial => "FreeTrial",
            AiPayerSelection.Organization => "Organization",
            _ => "Invalid"
        });

    public sealed class Measurement : IDisposable
    {
        private readonly string _operation;
        private readonly string _context;
        private readonly string _payer;
        private readonly long _started = Stopwatch.GetTimestamp();
        private string _result = "failure";
        private bool _disposed;
        internal Measurement(string operation, string context, string payer) => (_operation, _context, _payer) = (operation, context, payer);
        public void Succeeded() => _result = "success";
        public void ContractBlocked() => _result = "blocked_upstream_contract";
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var tags = new TagList { { "operation", _operation }, { "contextKind", _context }, { "payerKind", _payer }, { "result", _result } };
            Results.Add(1, tags); Duration.Record(Stopwatch.GetElapsedTime(_started).TotalMilliseconds, tags);
        }
    }
}
