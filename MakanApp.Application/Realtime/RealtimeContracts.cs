using MakanApp.Application.Messaging;

namespace MakanApp.Application.Realtime;

public static class RealtimeErrorCodes
{
    public const string ResyncRequired = "REALTIME_RESYNC_REQUIRED";
    public const string InvalidQuery = "REALTIME_INVALID_QUERY";
    public const string Unavailable = "REALTIME_TEMPORARILY_UNAVAILABLE";
}

public sealed class RealtimeException(string code) : Exception(code switch
{
    RealtimeErrorCodes.ResyncRequired => "بافت یا نشانگر تغییر کرده است؛ Snapshot مجاز و نشانگر تازه دریافت کنید.",
    RealtimeErrorCodes.Unavailable => "بازیابی موقتاً در دسترس نیست؛ نشانگر قبلی را حفظ کنید.",
    _ => "درخواست بازیابی معتبر نیست."
})
{
    public string Code { get; } = code;
}

// اعلان فقط دستور refetch است؛ دادهٔ خصوصی یا تغییر افزایشی شمارنده حمل نمی‌کند.
public sealed record RealtimeInvalidation(Guid EventId, string Key, Guid? ResourceId,
    string ContextRevision, string StreamVersion, DateTime OccurredAtUtc, int PayloadVersion = 1, string? TraceId = null);

public sealed record AccountChangeResult(Guid EventId, string StreamVersion,
    MessagingRealtimeNotification? Conversation, RealtimeInvalidation? Invalidation);

public sealed record AccountChangePageResult(IReadOnlyList<AccountChangeResult> Changes,
    string NextCursor, bool HasMore, string Watermark, string Epoch, string ContextRevision,
    int? RetryAfterMilliseconds = null);

public interface IRealtimeSyncService
{
    Task<AccountChangePageResult> ReadAsync(Guid userId, Guid sessionId, string? cursor, int limit, CancellationToken ct);
}

public interface IRealtimeCursorProtector
{
    string Protect(string value);
    string Unprotect(string value);
}

public interface IRealtimeSessionScope
{
    Task<string?> AuthorizeConversationAsync(Guid sessionId, MessagingRealtimeNotification notification, CancellationToken ct);
}

public interface IRealtimeInvalidationNotifier
{
    Task NotifyAsync(Guid eventId, string payload, CancellationToken ct);
}

public interface IRealtimeInvalidationAudience
{
    IAsyncEnumerable<(Guid SessionId, RealtimeInvalidation Notification)> ResolveAsync(Guid eventId, string payload, CancellationToken ct);
}
