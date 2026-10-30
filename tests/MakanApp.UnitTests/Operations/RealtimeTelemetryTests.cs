using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using MakanApp.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.UnitTests.Operations;

public sealed class RealtimeTelemetryTests
{
    [Fact]
    public void RealtimeMetricsBalanceConnectionsAndBoundOutcomeWithoutSensitiveLabels()
    {
        var seen = new ConcurrentQueue<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, source) =>
        {
            if (instrument.Meter.Name == RealtimeTelemetry.MeterName) source.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => seen.Enqueue((instrument.Name, value, tags.ToArray())));
        listener.Start();
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var metrics = new RealtimeTelemetry(provider.GetRequiredService<IMeterFactory>());
        metrics.Connected(); metrics.Connected(); metrics.Disconnected();
        metrics.Replay("resync", 10); metrics.Replay("untrusted-outcome", 20);
        metrics.Denied(); metrics.Delivered();
        Assert.Equal(1, seen.Where(x => x.Name == "makan.realtime.connections.active").Sum(x => x.Value));
        Assert.Equal(2, seen.Where(x => x.Name == "makan.realtime.connections.started").Sum(x => x.Value));
        Assert.Equal(1, seen.Where(x => x.Name == "makan.realtime.resync.required").Sum(x => x.Value));
        var tags = seen.SelectMany(x => x.Tags).ToArray();
        Assert.All(tags, x => Assert.Equal("outcome", x.Key));
        Assert.DoesNotContain(tags, x => Equals(x.Value, "untrusted-outcome"));
        Assert.Contains(tags, x => Equals(x.Value, "other"));
    }
}
