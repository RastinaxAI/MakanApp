using System.Threading.Channels;
using MakanApp.Application.Reports.Exports;

namespace MakanApp.Infrastructure.Reports.Exports;

public sealed class ReportExportQueueSignal : IReportExportQueueSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });

    public void Signal() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan maximumDelay, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(maximumDelay);
        try
        {
            await _channel.Reader.ReadAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }
}
