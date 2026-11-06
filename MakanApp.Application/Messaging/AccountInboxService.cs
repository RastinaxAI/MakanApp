using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Realtime;

namespace MakanApp.Application.Messaging;

public sealed record AccountInboxQuery(ConversationListFilter Filter = ConversationListFilter.All,
    string? Search = null, int Limit = 30, string? Cursor = null);
public sealed record AccountInboxPosition(bool Pinned, DateTime ActivityAtUtc, Guid ConversationId);
public sealed record AccountInboxPageResult(IReadOnlyList<ConversationSummaryResult> Items, string? NextCursor, bool HasMore);

public interface IAccountInboxStore
{
    Task<ConversationSummaryStoreRecord?> ReadConversationSummaryAsync(Guid userId,
        Guid conversationId, CancellationToken ct);

    Task<IReadOnlyList<ConversationSummaryStoreRecord>> ReadAccountInboxAsync(Guid userId,
        ConversationListFilter filter, string? search, int limit, AccountInboxPosition? position, CancellationToken ct);
}

public sealed class AccountInboxService(IAccountInboxStore store, MessagingOperationContextResolver contexts,
    IRealtimeCursorProtector protection, TimeProvider clock)
{
    private const string View = "account-inbox-page-v1";
    private sealed record Cursor(string View, Guid UserId, Guid SessionId, string Filter,
        AccountInboxPosition Position, DateTime ExpiresAtUtc);

    public async Task<ConversationSummaryResult> ReadSummaryAsync(Guid userId, Guid sessionId,
        Guid conversationId, CancellationToken ct)
    {
        await contexts.PersonalAsync(userId, sessionId, ct);
        if (conversationId == Guid.Empty)
            throw SummaryNotFound();
        // مجوز همان صندوق حساب است؛ این خواندن، بافت نشست یا مجوز محتوای کامل را تغییر نمی‌دهد.
        var record = await store.ReadConversationSummaryAsync(userId, conversationId, ct);
        await contexts.PersonalAsync(userId, sessionId, ct);
        return record is null ? throw SummaryNotFound() : ConversationSummaryMapping.Map(record, clock.GetUtcNow().UtcDateTime);
    }

    private static MessagingException SummaryNotFound() =>
        new(MessagingErrorCodes.ConversationNotFound, "گفتگوی مجاز یافت نشد.");

    public async Task<AccountInboxPageResult> ReadAsync(Guid userId, Guid sessionId, AccountInboxQuery query, CancellationToken ct)
    {
        if (!Enum.IsDefined(query.Filter) || query.Limit is < 1 or > 100 || query.Search?.Trim().Length > 100)
            throw Invalid();
        await contexts.PersonalAsync(userId, sessionId, ct);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { query.Filter, search, query.Limit }))));
        var now = clock.GetUtcNow().UtcDateTime;
        AccountInboxPosition? position = null;
        if (query.Cursor is not null)
        {
            try
            {
                if (query.Cursor.Length > 4096) throw Invalid();
                var decoded = JsonSerializer.Deserialize<Cursor>(protection.Unprotect(query.Cursor));
                if (decoded is null || decoded.View != View || decoded.UserId != userId || decoded.SessionId != sessionId ||
                    decoded.Filter != fingerprint || decoded.ExpiresAtUtc <= now || decoded.Position is null ||
                    decoded.Position.ConversationId == Guid.Empty || decoded.Position.ActivityAtUtc.Kind != DateTimeKind.Utc)
                    throw Invalid();
                position = decoded.Position;
            }
            catch (Exception exception) when (exception is JsonException or CryptographicException or FormatException or ArgumentException or RealtimeException)
            { throw Invalid(); }
        }
        // هر صفحه مجوز SQL جاری را دوباره می‌سنجد؛ Cursor فقط مرز ترتیب است.
        var records = await store.ReadAccountInboxAsync(userId, query.Filter, search, query.Limit + 1, position, ct);
        await contexts.PersonalAsync(userId, sessionId, ct);
        var page = records.Take(query.Limit).ToArray();
        var hasMore = records.Count > query.Limit;
        var next = hasMore ? protection.Protect(JsonSerializer.Serialize(new Cursor(View, userId, sessionId, fingerprint,
            new(page[^1].State?.IsPinned == true, DateTime.SpecifyKind(page[^1].LastMessageAtUtc ?? page[^1].Conversation.CreatedAtUtc, DateTimeKind.Utc),
                page[^1].Conversation.Id), now.AddHours(24)))) : null;
        return new(page.Select(item => ConversationSummaryMapping.Map(item, now)).ToArray(), next, hasMore);
    }

    private static MessagingException Invalid() => new(MessagingErrorCodes.MessagePageInvalid, "فیلتر یا Cursor صندوق حساب معتبر نیست؛ فهرست را از ابتدا دریافت کنید.");
}
