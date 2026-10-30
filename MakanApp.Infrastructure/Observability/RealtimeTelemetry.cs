using System.Diagnostics.Metrics;

namespace MakanApp.Infrastructure.Observability;

public sealed class RealtimeTelemetry
{
    public const string MeterName = "MakanApp.Realtime";
    private readonly Counter<long> _replay;
    private readonly Counter<long> _resync;
    private readonly Counter<long> _denied;
    private readonly Counter<long> _delivery;
    private readonly Counter<long> _connections;
    private readonly UpDownCounter<long> _active;
    private readonly Histogram<double> _duration;

    public RealtimeTelemetry(IMeterFactory factory)
    {
        var meter = factory.Create(MeterName);
        _replay = meter.CreateCounter<long>("makan.realtime.replay.requests");
        _resync = meter.CreateCounter<long>("makan.realtime.resync.required");
        _denied = meter.CreateCounter<long>("makan.realtime.authorization.rejections");
        _delivery = meter.CreateCounter<long>("makan.realtime.invalidation.deliveries");
        _connections = meter.CreateCounter<long>("makan.realtime.connections.started");
        _active = meter.CreateUpDownCounter<long>("makan.realtime.connections.active");
        _duration = meter.CreateHistogram<double>("makan.realtime.replay.duration", "ms");
    }

    public void Replay(string outcome, double milliseconds)
    {
        var bounded = outcome is "success" or "resync" or "invalid" or "denied" or "failed" ? outcome : "other";
        var tag = new KeyValuePair<string, object?>("outcome", bounded);
        _replay.Add(1, tag); _duration.Record(Math.Max(0, milliseconds), tag);
        if (bounded == "resync") _resync.Add(1);
    }
    public void Denied() => _denied.Add(1);
    public void Delivered() => _delivery.Add(1);
    public void Connected() { _connections.Add(1); _active.Add(1); }
    public void Disconnected() => _active.Add(-1);
}
