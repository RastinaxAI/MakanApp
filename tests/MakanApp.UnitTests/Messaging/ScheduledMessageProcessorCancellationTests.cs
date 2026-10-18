using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class ScheduledMessageProcessorCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationDoesNotPersistFailureEvenWhenStoreThrowsAnotherException(bool nonCancellationException)
    {
        using var cancellation = new CancellationTokenSource();
        var schedules = new CancellingStore(cancellation, nonCancellationException);
        // پیش از خواندن work نباید ارسال یا Safety اجرا شود؛ این پورت‌ها عمداً در سناریو مصرف نمی‌شوند.
        var processor = new ScheduledMessageProcessor(schedules, null!, null!, new MessagingOptions(), TimeProvider.System);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.DispatchBatchAsync(cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, schedules.FailureWrites);
        Assert.DoesNotContain("synthetic-private-provider-marker", exception.ToString(), StringComparison.Ordinal);
    }

    private sealed class CancellingStore(CancellationTokenSource cancellation, bool nonCancellationException) : IScheduledMessageStore
    {
        public int FailureWrites { get; private set; }

        public Task<ScheduledMessageClaim?> ClaimNextScheduledMessageAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<ScheduledMessageClaim?>(new(Guid.NewGuid(), Guid.NewGuid()));
        }

        public Task<ScheduledMessageWork?> GetScheduledMessageWorkAsync(ScheduledMessageClaim claim, CancellationToken ct)
        {
            cancellation.Cancel();
            Exception failure = nonCancellationException
                ? new IOException("synthetic-private-provider-marker")
                : new OperationCanceledException("synthetic-private-provider-marker", ct);
            return Task.FromException<ScheduledMessageWork?>(failure);
        }

        public Task FailScheduledMessageAsync(ScheduledMessageClaim claim, string code, bool retryable, CancellationToken ct)
        {
            FailureWrites++;
            return Task.CompletedTask;
        }

        public Task<ScheduledMessageResult> ScheduleAsync(Guid userId, Guid sessionId, Guid conversationId,
            SendMessageStoreCommand command, byte[] payloadHash, DateTime scheduledAtUtc, AccessContext context,
            string correlationId, CancellationToken ct) => throw new NotSupportedException();

        public Task<ScheduledMessagePageResult> GetScheduledMessagesAsync(Guid userId, Guid conversationId,
            AccessContext context, int skip, int take, CancellationToken ct) => throw new NotSupportedException();

        public Task<ScheduledMessageResult> CancelScheduledMessageAsync(Guid userId, Guid sessionId, Guid conversationId,
            Guid id, byte[] expectedVersion, AccessContext context, string correlationId, CancellationToken ct) => throw new NotSupportedException();
    }
}
