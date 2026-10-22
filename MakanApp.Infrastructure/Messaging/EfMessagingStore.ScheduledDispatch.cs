using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<ScheduledMessageClaim?> ClaimNextScheduledMessageAsync(CancellationToken ct)
    {
        dbContext.ChangeTracker.Clear(); var now = timeProvider.GetUtcNow().UtcDateTime;
        var candidate = await dbContext.ScheduledMessages.AsNoTracking().Where(s => s.Status == ScheduledMessageStatus.Scheduled &&
            s.ScheduledAtUtc <= now && (s.NextAttemptAtUtc == null || s.NextAttemptAtUtc <= now) && (s.ClaimedUntilUtc == null || s.ClaimedUntilUtc <= now))
            .OrderBy(s => s.ScheduledAtUtc).ThenBy(s => s.Id).Select(s => new { s.Id, s.ConversationId }).FirstOrDefaultAsync(ct);
        if (candidate is null) return null;
        // انتخاب اولیه فقط hint است؛ claim هم مانند Cancel/Send ترتیب Conversation→Scheduled و بازبینی زیر قفل دارد.
        // قفل گرفتن مستقیم از index صف و سپس نوشتن row با مسیر لغو، conversion deadlock می‌ساخت.
        await using var transaction = await BeginManagementTransactionAsync(ct);
        await LockConversationAsync(candidate.ConversationId, ct);
        var row = await dbContext.ScheduledMessages.FromSqlInterpolated($"SELECT * FROM [messaging].[ScheduledMessages] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={candidate.Id}").SingleOrDefaultAsync(ct);
        now = timeProvider.GetUtcNow().UtcDateTime;
        if (row is null || row.Status != ScheduledMessageStatus.Scheduled || row.ScheduledAtUtc > now || row.NextAttemptAtUtc > now || row.ClaimedUntilUtc > now)
        { await transaction.CommitAsync(ct); return null; }
        if (row.AttemptCount >= ScheduledMessage.MaximumAttempts)
        {
            row.FailExhausted(now); await SaveScheduledAsync(ct); await transaction.CommitAsync(ct); dbContext.ChangeTracker.Clear();
            telemetry.RecordOutcome(WorkerTelemetry.ScheduledMessage, "Failed");
            telemetry.RecordFailure(WorkerTelemetry.ScheduledMessage, "Process");
            logger.LogError("پیام زمان‌بندی‌شدهٔ رهاشده به سقف تلاش رسید. Code={FailureCode}", "SCHEDULED_MESSAGE_ATTEMPTS_EXHAUSTED");
            return new(row.Id, Guid.Empty, true);
        }
        var recovered = row.ClaimToken.HasValue;
        var token = Guid.NewGuid(); row.Claim(token, now, now.AddSeconds(30));
        await SaveScheduledAsync(ct); await transaction.CommitAsync(ct); dbContext.ChangeTracker.Clear();
        telemetry.RecordOutcome(WorkerTelemetry.ScheduledMessage, "Claimed");
        if (recovered) telemetry.RecordOutcome(WorkerTelemetry.ScheduledMessage, "LeaseRecovered");
        return new(row.Id, token);
    }

    public async Task<ScheduledMessageWork?> GetScheduledMessageWorkAsync(ScheduledMessageClaim claim, CancellationToken ct)
    {
        var row = await ScheduledRows(false).SingleOrDefaultAsync(s => s.Id == claim.Id, ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (row is null || !row.HasClaim(claim.Token, now)) return null;
        return new(row.Id, row.ConversationId, row.UserId,
            new(row.WorkspaceType, row.OrganizationId, row.MembershipId, row.Role, row.SubjectOrganizationPersonId),
            new(row.ClientMessageId, row.Kind, row.Text, row.Attachments.Select(a => a.FileAssetId).ToArray(), row.ReplyToMessageId,
                row.Mentions.Select(m => m.UserId).ToArray(), ContactUserId: row.ContactUserId, TopicId: row.TopicId, IsSilent: row.IsSilent));
    }

    public async Task FailScheduledMessageAsync(ScheduledMessageClaim claim, string code, bool retryable, CancellationToken ct)
    {
        // transaction ارسال ممکن است rollback شده باشد؛ entityهای آن نباید با ثبت خطا دوباره ذخیره شوند.
        dbContext.ChangeTracker.Clear();
        var conversationId = await dbContext.ScheduledMessages.AsNoTracking().Where(s => s.Id == claim.Id).Select(s => (Guid?)s.ConversationId).SingleOrDefaultAsync(ct);
        if (!conversationId.HasValue) return;
        await using var transaction = await BeginManagementTransactionAsync(ct);
        await LockConversationAsync(conversationId.Value, ct);
        var row = await dbContext.ScheduledMessages.FromSqlInterpolated($"SELECT * FROM [messaging].[ScheduledMessages] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={claim.Id}")
            .SingleOrDefaultAsync(ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (row is null || !row.HasClaim(claim.Token, now))
        {
            await transaction.CommitAsync(ct);
            telemetry.RecordOutcome(WorkerTelemetry.ScheduledMessage, "LeaseLost");
            return;
        }
        row.Fail(claim.Token, code, retryable, now); await SaveScheduledAsync(ct); await transaction.CommitAsync(ct); dbContext.ChangeTracker.Clear();
        telemetry.RecordOutcome(WorkerTelemetry.ScheduledMessage, row.Status == ScheduledMessageStatus.Failed ? "Failed" : "RetryScheduled");
        telemetry.RecordFailure(WorkerTelemetry.ScheduledMessage, "Process");
        logger.LogError("ارسال پیام زمان‌بندی‌شده انجام نشد؛ نتیجهٔ پایدار ثبت شد. Code={FailureCode}", "SCHEDULED_MESSAGE_DISPATCH_FAILED");
    }

    private async Task<ScheduledMessage?> ValidateScheduledSendAsync(Guid userId, Guid conversationId, SendMessageStoreCommand command, bool locked, CancellationToken ct, AccessContext? context = null)
    {
        var proof = command.ScheduledClaim;
        if (proof is null)
        {
            if (await dbContext.ScheduledMessages.AsNoTracking().AnyAsync(s => s.ConversationId == conversationId && s.UserId == userId && s.ClientMessageId == command.ClientMessageId, ct)) throw ScheduledReserved();
            return null;
        }
        IQueryable<ScheduledMessage> query = locked
            ? dbContext.ScheduledMessages.FromSqlInterpolated($"SELECT * FROM [messaging].[ScheduledMessages] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={proof.ScheduledMessageId}")
            : dbContext.ScheduledMessages.AsNoTracking().Where(s => s.Id == proof.ScheduledMessageId);
        var row = await query.Include(s => s.Attachments).Include(s => s.Mentions).AsSplitQuery().SingleOrDefaultAsync(ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (row is null || row.UserId != userId || row.ConversationId != conversationId || row.ClientMessageId != command.ClientMessageId ||
            !row.HasClaim(proof.Token, now) || !CryptographicOperations.FixedTimeEquals(row.PayloadHash, proof.PayloadHash) ||
            context is null || context.UserId != row.UserId || context.WorkspaceType != row.WorkspaceType || context.OrganizationId != row.OrganizationId ||
            context.MembershipId != row.MembershipId || context.ActiveRole != row.Role || context.SubjectOrganizationPersonId != row.SubjectOrganizationPersonId ||
            row.Kind != command.Kind || row.Text != command.Text || row.TopicId != command.TopicId || row.IsSilent != command.IsSilent ||
            row.ReplyToMessageId != command.ReplyToMessageId || row.ContactUserId != command.ContactUserId || command.ForwardedFromMessageId is not null ||
            command.AllowSourceAttachments || command.SafetyPurpose != MessageSafetyPurpose.Message ||
            !row.Attachments.Select(a => a.FileAssetId).Order().SequenceEqual(command.AttachmentIds.Distinct().Order()) ||
            !row.Mentions.Select(m => m.UserId).Order().SequenceEqual(command.MentionedUserIds.Distinct().Order())) throw ScheduledClaimLost();
        return row;
    }
}
