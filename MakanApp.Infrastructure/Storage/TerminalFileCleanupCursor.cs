namespace MakanApp.Infrastructure.Storage;

// cursor فنی در طول عمر host باقی می‌ماند، نه در scope پردازنده؛ حذف bytes نیز idempotent است.
public sealed class TerminalFileCleanupCursor : IDisposable
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal int NextStatus { get; set; } = 3;
    internal Dictionary<int, TerminalFileCleanupPosition> Positions { get; } = [];
    public void Dispose() => Gate.Dispose();
}

internal sealed record TerminalFileCleanupPosition(int Status, DateTime CreatedAtUtc, Guid Id);
