using MakanApp.Application.Messaging;
using Microsoft.Data.SqlClient;

namespace MakanApp.IntegrationTests;

public sealed class RealtimeFailureSwitch(RealtimeTestDiagnostics diagnostics) : IDisposable
{
    public Guid HostId { get; } = Guid.NewGuid();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTime> _publications = new();
    public void RecordPublication(Guid resourceId)
    {
        _publications.TryAdd(resourceId, DateTime.UtcNow);
        diagnostics.Record(HostId, resourceId, "Started");
    }
    public void RecordCompletion(Guid resourceId) => diagnostics.Record(HostId, resourceId, "Completed");
    public void RecordFault(Guid resourceId, Exception exception) => diagnostics.Record(HostId, resourceId, exception.GetType().Name);
    public void Dispose() => diagnostics.Record(HostId, Guid.Empty, "HostDisposed");
    public DateTime? PublicationStartedAtUtc(Guid resourceId) => _publications.TryGetValue(resourceId, out var instant) ? instant : null;
    private TaskCompletionSource _failureObserved = NewCompletionSource();

    public bool FailDispatch { get; set; }
    public Func<MessagingRealtimeNotification, CancellationToken, Task>? BeforePublication { get; set; }

    public Task FailureObserved => _failureObserved.Task;

    public void RecordFailure() => _failureObserved.TrySetResult();

    public void Reset()
    {
        FailDispatch = false;
        BeforePublication = null;
        _failureObserved = NewCompletionSource();
    }

    private static TaskCompletionSource NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class FaultInjectingMessagingRealtimeNotifier(
    IMessagingRealtimeNotifier inner,
    RealtimeFailureSwitch failureSwitch) : IMessagingRealtimeNotifier
{
    public async Task NotifyConversationChangedAsync(
        MessagingRealtimeNotification notification,
        Guid? audienceUserId,
        CancellationToken cancellationToken)
    {
        if (failureSwitch.FailDispatch)
        {
            failureSwitch.RecordFailure();
            throw new InvalidOperationException("Injected realtime dispatch failure.");
        }

        failureSwitch.RecordPublication(notification.ResourceId);
        try
        {
            if (failureSwitch.BeforePublication is { } before) await before(notification, cancellationToken);
            await inner.NotifyConversationChangedAsync(notification, audienceUserId, cancellationToken);
            failureSwitch.RecordCompletion(notification.ResourceId);
        }
        catch (Exception exception)
        {
            failureSwitch.RecordFault(notification.ResourceId, exception);
            throw;
        }
    }
}

public sealed partial class MakanAppWebApplicationFactory
{
    public static MakanAppWebApplicationFactory CreateRealtimeIsolationProbe()
    {
        var factory = new MakanAppWebApplicationFactory(knowledgeProbe: true);
        factory._instanceConnectionString = new SqlConnectionStringBuilder(factory._instanceConnectionString)
            { MaxPoolSize = 1 }.ConnectionString;
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, factory._instanceConnectionString);
        return factory;
    }
}
