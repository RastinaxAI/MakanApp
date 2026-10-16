using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore : IScheduledMessageStore
{
    public async Task<ScheduledMessageResult> ScheduleAsync(Guid userId, Guid sessionId, Guid conversationId, SendMessageStoreCommand command,
        byte[] payloadHash, DateTime scheduledAtUtc, AccessContext context, string correlationId, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var conversation = await LockConversationAsync(conversationId, ct) ?? throw ConversationNotFound();
        var now = await EnsureAdvancedSessionAsync(userId, sessionId, context, ct);
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(conversation, actor, userId, context, ct);
        var existing = await ScheduledRows(false).SingleOrDefaultAsync(s => s.ConversationId == conversationId && s.UserId == userId && s.ClientMessageId == command.ClientMessageId, ct);
        if (existing is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(existing.PayloadHash, payloadHash) || existing.ScheduledAtUtc != scheduledAtUtc) throw ScheduledConflict();
            await transaction.CommitAsync(ct); return MapScheduled(existing, true);
        }
        if (await dbContext.Messages.AnyAsync(m => m.ConversationId == conversationId && m.SenderUserId == userId && m.ClientMessageId == command.ClientMessageId, ct)) throw ScheduledReserved();
        await EnsureCanSendAdvancedAsync(conversation, actor, userId, context, command.Kind, now, ct);
        await LoadAndValidateMessageReferencesAsync(conversation, actor, userId, command, ct);
        if (await dbContext.ScheduledMessages.CountAsync(s => s.ConversationId == conversationId && s.UserId == userId && s.Status == ScheduledMessageStatus.Scheduled, ct)
            >= ScheduledMessage.MaximumPendingPerConversationSender) throw new MessagingException(MessagingErrorCodes.ScheduledInvalid, "تعداد پیام‌های در انتظار به سقف رسیده است.");
        ScheduledMessage scheduled;
        try
        {
            scheduled = ScheduledMessage.Create(conversationId, userId, command.ClientMessageId,
                new(context.WorkspaceType, context.OrganizationId, context.MembershipId, context.ActiveRole, context.SubjectOrganizationPersonId),
                ToScheduledContent(command), payloadHash, scheduledAtUtc, now);
        }
        catch (ArgumentException ex) { throw new MessagingException(MessagingErrorCodes.ScheduledInvalid, "زمان‌بندی معتبر نیست.", ex); }
        dbContext.ScheduledMessages.Add(scheduled);
        await AuditAdvancedAsync(userId, sessionId, "messaging.scheduled.created", "ScheduledMessage", scheduled.Id.ToString("D"), "ثبت قصد ارسال زمان‌بندی‌شده", correlationId, now, ct);
        await SaveScheduledAsync(ct); await transaction.CommitAsync(ct); return MapScheduled(scheduled);
    }

    public async Task<ScheduledMessagePageResult> GetScheduledMessagesAsync(Guid userId, Guid conversationId, AccessContext context, int skip, int take, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, ct) ?? throw ConversationNotFound();
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(conversation, actor, userId, context, ct);
        // محتوا و فرزندان پس از ایجاد immutable هستند؛ split query نسخهٔ متناقض محتوا ایجاد نمی‌کند.
        var rows = await ScheduledRows(false).Where(s => s.ConversationId == conversationId && s.UserId == userId)
            .OrderBy(s => s.ScheduledAtUtc).ThenBy(s => s.Id).Skip(skip).Take(take + 1).ToArrayAsync(ct);
        return new(rows.Take(take).Select(s => MapScheduled(s)).ToArray(), rows.Length > take);
    }

    public async Task<ScheduledMessageResult> CancelScheduledMessageAsync(Guid userId, Guid sessionId, Guid conversationId, Guid id,
        byte[] expectedVersion, AccessContext context, string correlationId, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var conversation = await LockConversationAsync(conversationId, ct) ?? throw ConversationNotFound();
        var now = await EnsureAdvancedSessionAsync(userId, sessionId, context, ct);
        var actor = await LoadActiveParticipantAsync(conversationId, userId, ct) ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(conversation, actor, userId, context, ct);
        var scheduled = await dbContext.ScheduledMessages.FromSqlInterpolated($"SELECT * FROM [messaging].[ScheduledMessages] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id} AND [ConversationId]={conversationId} AND [UserId]={userId}")
            .Include(s => s.Attachments).Include(s => s.Mentions).AsSplitQuery().SingleOrDefaultAsync(ct)
            ?? throw ScheduledNotFound();
        if (scheduled.Status == ScheduledMessageStatus.Cancelled) { await transaction.CommitAsync(ct); return MapScheduled(scheduled, true); }
        if (scheduled.Status != ScheduledMessageStatus.Scheduled || !scheduled.RowVersion.SequenceEqual(expectedVersion)) throw ScheduledConflict();
        dbContext.Entry(scheduled).Property(s => s.RowVersion).OriginalValue = expectedVersion; scheduled.Cancel(now);
        await AuditAdvancedAsync(userId, sessionId, "messaging.scheduled.cancelled", "ScheduledMessage", scheduled.Id.ToString("D"), "لغو قصد ارسال زمان‌بندی‌شده", correlationId, now, ct);
        await SaveScheduledAsync(ct); await transaction.CommitAsync(ct); return MapScheduled(scheduled);
    }

    private IQueryable<ScheduledMessage> ScheduledRows(bool tracking) => (tracking ? dbContext.ScheduledMessages.AsTracking() : dbContext.ScheduledMessages.AsNoTracking())
        .Include(s => s.Attachments).Include(s => s.Mentions).AsSplitQuery();
    private static ScheduledMessageContent ToScheduledContent(SendMessageStoreCommand command) => new(command.Kind, command.Text, command.AttachmentIds,
        command.ReplyToMessageId, command.MentionedUserIds, command.ContactUserId, command.TopicId, command.IsSilent);
    private static ScheduledMessageResult MapScheduled(ScheduledMessage s, bool alreadyExisted = false) => new(s.Id, s.ConversationId, s.ClientMessageId,
        s.Status, AdvancedUtc(s.ScheduledAtUtc), AdvancedUtc(s.CreatedAtUtc), s.CompletedAtUtc.HasValue ? AdvancedUtc(s.CompletedAtUtc.Value) : null,
        s.DispatchedMessageId, s.LastFailureCode, new(s.Kind, s.Text, s.Attachments.Select(a => a.FileAssetId).Order().ToArray(), s.ReplyToMessageId,
            s.Mentions.Select(m => m.UserId).Order().ToArray(), s.ContactUserId, s.TopicId, s.IsSilent), Convert.ToBase64String(s.RowVersion), alreadyExisted);
    private async Task SaveScheduledAsync(CancellationToken ct)
    {
        try { await dbContext.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw ScheduledConflict(ex); }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex)) { throw ScheduledConflict(ex); }
    }
    private static MessagingException ScheduledConflict(Exception? ex = null) => new(MessagingErrorCodes.ScheduledConflict, "وضعیت زمان‌بندی هم‌زمان تغییر کرده یا نهایی شده است.", ex);
    private static MessagingException ScheduledNotFound() => new(MessagingErrorCodes.ScheduledNotFound, "زمان‌بندی در دسترس نیست.");
    private static MessagingException ScheduledReserved() => new(MessagingErrorCodes.ScheduledReserved, "شناسهٔ ارسال قبلاً رزرو شده است.");
    private static MessagingException ScheduledClaimLost() => new(MessagingErrorCodes.ScheduledClaimLost, "تصاحب کار دیگر معتبر نیست.");
}
