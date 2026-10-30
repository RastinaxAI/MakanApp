using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MakanApp.IntegrationTests;

// Gate تک‌مرحله‌ای صرفاً برای بازتولید قطعی race پس از محاسبهٔ مرز SQL در fixture ایزوله است.
public sealed class RealtimeBoundaryGate : DbCommandInterceptor
{
    private State? _armed;
    public (Task Entered, Action Release) Arm()
    {
        var state = new State();
        if (Interlocked.CompareExchange(ref _armed, state, null) is not null) throw new InvalidOperationException("Gate آزمون قبلاً فعال است.");
        return (state.Entered.Task, () => state.Released.TrySetResult());
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("MIN_ACTIVE_ROWVERSION", StringComparison.OrdinalIgnoreCase) &&
            Interlocked.Exchange(ref _armed, null) is State state)
        {
            state.Entered.TrySetResult();
            await state.Released.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        return result;
    }
    private sealed class State
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
