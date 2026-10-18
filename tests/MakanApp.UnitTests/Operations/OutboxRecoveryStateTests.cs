using MakanApp.Infrastructure.Hosting;
using MakanApp.Infrastructure.Outbox;
using Xunit;

namespace MakanApp.UnitTests.Operations;

public sealed class OutboxRecoveryStateTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TerminalFailureClearsClaimAndRetryWithoutPretendingProcessed()
    {
        var row = OutboxMessage.Create(Guid.NewGuid(), "test.v1", "{}", Now);
        var token = Guid.NewGuid();
        row.Claim(token, Now, Now.AddSeconds(30));
        row.MarkFailed(token, OutboxProcessor.AttemptsExhaustedErrorCode);
        Assert.Null(row.ProcessedAtUtc);
        Assert.Null(row.ClaimToken);
        Assert.Null(row.ClaimedUntilUtc);
        Assert.Null(row.NextAttemptAtUtc);
        Assert.Equal(OutboxProcessor.AttemptsExhaustedErrorCode, row.LastErrorCode);
        Assert.Equal(1, row.AttemptCount);
    }

    [Fact]
    public void StaleClaimCannotTerminalizeReplacementClaim()
    {
        var row = OutboxMessage.Create(Guid.NewGuid(), "test.v1", "{}", Now);
        var oldToken = Guid.NewGuid();
        var currentToken = Guid.NewGuid();
        row.Claim(oldToken, Now, Now.AddSeconds(1));
        row.Claim(currentToken, Now.AddSeconds(2), Now.AddSeconds(32));
        Assert.Throws<InvalidOperationException>(() => row.MarkFailed(oldToken, "failed"));
        Assert.Equal(currentToken, row.ClaimToken);
        Assert.Null(row.LastErrorCode);
    }

    [Fact]
    public void InvalidTerminalErrorDoesNotClearCurrentClaim()
    {
        var row = OutboxMessage.Create(Guid.NewGuid(), "test.v1", "{}", Now);
        var token = Guid.NewGuid();
        row.Claim(token, Now, Now.AddSeconds(30));
        Assert.Throws<ArgumentException>(() => row.MarkFailed(token, " "));
        Assert.Equal(token, row.ClaimToken);
        Assert.Equal(Now.AddSeconds(30), row.ClaimedUntilUtc);
    }

    [Fact]
    public void CompletionBudgetIsFiniteAndIndependentOfCallerCancellation()
    {
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        using var completion = WorkerCompletionBudget.Create();
        Assert.True(completion.Token.CanBeCanceled);
        Assert.False(completion.IsCancellationRequested);
        Assert.InRange(WorkerCompletionBudget.Timeout, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(5));
    }
}
