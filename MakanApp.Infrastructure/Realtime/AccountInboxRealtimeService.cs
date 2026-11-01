using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Realtime;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.Infrastructure.Realtime;

// فقط نمای دیگری از همان دو Outbox موجود؛ هیچ Stream یا Outbox موازی ذخیره نمی‌شود.
public sealed class AccountInboxRealtimeService(MakanDbContext db, IMessagingStore messages,
    IMessagingOperationContextStore sessions, IRealtimeCursorProtector protection, TimeProvider clock,
    IServiceScopeFactory scopes, MakanApp.Infrastructure.Observability.RealtimeTelemetry telemetry) : IAccountInboxRealtimeService
{
    public const string View = "AccountInbox";
    public const string InvalidationKey = "messaging.account-inbox";
    private sealed record Cursor(string View, Guid UserId, Guid SessionId, string Lower, DateTime IssuedAtUtc, string Epoch);

    public async Task<AccountChangePageResult> ReadAsync(Guid userId, Guid sessionId, string? cursor, int limit, CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var outcome = "failed";
        try { var result = await ReadCoreAsync(userId, sessionId, cursor, limit, ct); outcome = "success"; return result; }
        catch (RealtimeException) { outcome = "invalid"; throw; }
        catch (IdentityException) { outcome = "denied"; throw; }
        catch (SqlException exception) when (exception.Number is 1205 or -2)
        { throw new RealtimeException(RealtimeErrorCodes.Unavailable); }
        finally { telemetry.Replay(outcome, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }

    private async Task<AccountChangePageResult> ReadCoreAsync(Guid userId, Guid sessionId, string? cursor, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 200) throw new RealtimeException(RealtimeErrorCodes.InvalidQuery);
        await AuthorizeSessionAsync(userId, sessionId, ct);
        var revision = Revision(userId, sessionId);
        var upper = await db.Database.SqlQuery<byte[]>($"SELECT CONVERT(binary(8), MIN_ACTIVE_ROWVERSION()) AS [Value]").SingleAsync(ct);
        if (cursor is null)
        {
            var start = Encode(userId, sessionId, upper);
            return new([], start, false, start, RealtimeSyncService.Epoch, revision);
        }
        var lower = Decode(userId, sessionId, cursor);
        if (Number(lower) > Number(upper)) throw new RealtimeException(RealtimeErrorCodes.ResyncRequired);
        await using var scan = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var rows = await db.Database.SqlQuery<FeedRow>($"""
            SELECT TOP ({limit + 1}) [EventId], [Revision], [MessagingChangeId], [OccurredAtUtc]
            FROM (
                SELECT q.[ChangeEventId] AS [EventId], q.[RowVersion] AS [Revision], q.[ChangeEventId] AS [MessagingChangeId], q.[OccurredAtUtc]
                FROM [messaging].[RealtimeOutbox] q
                JOIN [messaging].[ChangeEvents] e ON e.[Id]=q.[ChangeEventId]
                WHERE EXISTS (SELECT 1 FROM [messaging].[ConversationParticipants] p
                    WHERE p.[ConversationId]=e.[ConversationId] AND p.[UserId]={userId}
                      AND p.[Status]={(int)ConversationParticipantStatus.Active} AND p.[EndedAtUtc] IS NULL)
                  AND (e.[AudienceUserId] IS NULL OR e.[AudienceUserId]={userId})
                UNION ALL
                SELECT q.[Id], q.[RowVersion], CAST(NULL AS uniqueidentifier), q.[OccurredAtUtc]
                FROM [platform].[OutboxMessages] q
                WHERE q.[MessageType]={RealtimeInvalidationWriter.MessageType}
                  AND JSON_VALUE(CASE WHEN ISJSON(q.[Payload])=1 THEN q.[Payload] ELSE 'null' END,'$.Key')='access'
                  AND JSON_VALUE(CASE WHEN ISJSON(q.[Payload])=1 THEN q.[Payload] ELSE 'null' END,'$.TargetUserId')={userId.ToString()}
                  AND JSON_VALUE(CASE WHEN ISJSON(q.[Payload])=1 THEN q.[Payload] ELSE 'null' END,'$.ResourceId') IS NULL
                  AND JSON_VALUE(CASE WHEN ISJSON(q.[Payload])=1 THEN q.[Payload] ELSE 'null' END,'$.OrganizationId') IS NULL
            ) feed WHERE [Revision]>={lower} ORDER BY [Revision]
            """).ToArrayAsync(ct);
        await scan.CommitAsync(ct);
        var stable = rows.TakeWhile(row => Number(row.Revision) < Number(upper)).ToArray();
        var pendingCut = stable.Length < rows.Length;
        var scanned = stable.Take(limit).ToArray();
        var changes = new List<AccountChangeResult>(scanned.Length);
        foreach (var row in scanned)
        {
            if (row.MessagingChangeId.HasValue && !await VisibleChangeAsync(userId, sessionId, row.MessagingChangeId.Value, ct)) continue;
            var version = Convert.ToHexString(row.Revision);
            changes.Add(new(row.EventId, version, null, Invalidation(row.EventId, revision, version, row.OccurredAtUtc)));
        }
        await AuthorizeSessionAsync(userId, sessionId, ct);
        var next = stable.Length > limit ? Increment(scanned[^1].Revision) : upper;
        return new(changes, Encode(userId, sessionId, next), stable.Length > limit || pendingCut,
            Encode(userId, sessionId, upper), RealtimeSyncService.Epoch, revision, pendingCut && scanned.Length < limit ? 100 : null);
    }

    public async IAsyncEnumerable<(Guid SessionId, RealtimeInvalidation Notification)> ResolveAsync(
        MessagingRealtimeNotification notification, Guid? audienceUserId, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!MessagingChangeCursor.TryDecode(notification.Cursor, notification.ConversationId, out var sequence))
            throw new InvalidOperationException("نشانگر رویداد با گفتگو سازگار نیست.");
        var change = await db.MessagingChangeEvents.AsNoTracking().SingleAsync(e => e.ConversationId == notification.ConversationId &&
            e.ChangeSequence == sequence && e.ResourceId == notification.ResourceId && e.ChangeType == notification.Type, ct);
        var row = await db.MessagingRealtimeOutboxMessages.AsNoTracking().SingleAsync(q => q.ChangeEventId == change.Id, ct);
        Guid? after = null;
        while (true)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var candidates = db.UserSessions.AsNoTracking().Where(s => s.RevokedAtUtc == null && s.ExpiresAtUtc > now && s.AbsoluteExpiresAtUtc > now &&
                (!audienceUserId.HasValue || s.UserId == audienceUserId) &&
                db.Users.Any(u => u.Id == s.UserId && u.AccountStatus == UserAccountStatus.Active) &&
                db.ConversationParticipants.Any(p => p.UserId == s.UserId && p.ConversationId == notification.ConversationId &&
                    p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null));
            if (after.HasValue) candidates = candidates.Where(s => s.Id.CompareTo(after.Value) > 0);
            var page = await candidates.OrderBy(s => s.Id).Select(s => new { s.Id, s.UserId }).Take(128).ToArrayAsync(ct);
            if (page.Length == 0) yield break;
            after = page[^1].Id;
            foreach (var session in page)
            {
                await using var scope = scopes.CreateAsyncScope();
                var fresh = scope.ServiceProvider.GetRequiredService<AccountInboxRealtimeService>();
                if (!await fresh.IsSessionActiveAsync(session.UserId, session.Id, ct) ||
                    !await fresh.VisibleChangeAsync(session.UserId, session.Id, change.Id, ct)) continue;
                yield return (session.Id, Invalidation(change.Id, Revision(session.UserId, session.Id), Convert.ToHexString(row.RowVersion), row.OccurredAtUtc));
            }
        }
    }

    private async Task<bool> VisibleChangeAsync(Guid userId, Guid sessionId, Guid eventId, CancellationToken ct)
    {
        var change = await db.MessagingChangeEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == eventId &&
            (e.AudienceUserId == null || e.AudienceUserId == userId), ct);
        if (change is null) return false;
        var context = await AccountInboxAccessQuery.ResolveReadContextAsync(db, userId, sessionId, change.ConversationId, ct);
        if (context is null) return false;
        try
        {
            var page = await messages.GetConversationChangesAsync(userId, change.ConversationId, context, change.ChangeSequence - 1, 1, ct);
            return page.Changes.Any(e => e.Id == change.Id);
        }
        catch (MessagingException) { return false; }
    }

    private async Task AuthorizeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        if (!await IsSessionActiveAsync(userId, sessionId, ct))
            throw new IdentityException(IdentityErrorCodes.AuthRequired, "نشست معتبر لازم است.");
    }

    private Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        sessions.HasActiveAccountSessionAsync(userId, sessionId, clock.GetUtcNow().UtcDateTime, ct);

    private static string Revision(Guid userId, Guid sessionId) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{View}:{userId:N}:{sessionId:N}:{MessagingStatusPolicy.PolicyVersion}")));
    private static RealtimeInvalidation Invalidation(Guid id, string revision, string version, DateTime occurredAtUtc) =>
        new(id, InvalidationKey, null, revision, version, DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc));
    private string Encode(Guid userId, Guid sessionId, byte[] lower) => protection.Protect(JsonSerializer.Serialize(
        new Cursor(View, userId, sessionId, Convert.ToHexString(lower), clock.GetUtcNow().UtcDateTime, RealtimeSyncService.Epoch)));
    private byte[] Decode(Guid userId, Guid sessionId, string token)
    {
        try
        {
            if (token.Length > 4096) throw new RealtimeException(RealtimeErrorCodes.ResyncRequired);
            var value = JsonSerializer.Deserialize<Cursor>(protection.Unprotect(token));
            var age = value is null ? TimeSpan.MaxValue : clock.GetUtcNow().UtcDateTime - value.IssuedAtUtc;
            if (value is null || value.View != View || value.UserId != userId || value.SessionId != sessionId ||
                value.Epoch != RealtimeSyncService.Epoch || value.IssuedAtUtc.Kind != DateTimeKind.Utc ||
                age < TimeSpan.Zero || age > RealtimeSyncService.CursorLifetime || value.Lower is not { Length: 16 })
                throw new RealtimeException(RealtimeErrorCodes.ResyncRequired);
            return Convert.FromHexString(value.Lower);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        { throw new RealtimeException(RealtimeErrorCodes.ResyncRequired); }
    }
    private static ulong Number(byte[] value) => BinaryPrimitives.ReadUInt64BigEndian(value);
    private static byte[] Increment(byte[] value)
    {
        var result = new byte[8]; BinaryPrimitives.WriteUInt64BigEndian(result, checked(Number(value) + 1)); return result;
    }
    private sealed class FeedRow
    {
        public Guid EventId { get; set; }
        public byte[] Revision { get; set; } = [];
        public Guid? MessagingChangeId { get; set; }
        public DateTime OccurredAtUtc { get; set; }
    }
}
