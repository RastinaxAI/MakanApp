using MakanApp.Application.Platform;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed partial class EfPlatformEducationSafetyStore
{
    public async Task<PlatformModerationCaseDetail> ReadModerationContextAsync(PlatformActorContext actor, Guid caseId,
        bool expanded, string reason, CancellationToken ct, byte[]? expectedVersion = null)
    {
        var mediaResources = await ReadMediaReviewResourcesAsync(caseId, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        await LockMediaReviewResourcesAsync(mediaResources, ct);
        await RequireReviewerAsync(actor, expanded, ct);
        var moderationCase = await LockPlatformSafetyCaseAsync(caseId, ct) ?? throw ModerationNotFound();
        var subjectId = await CaseSubjectAsync(moderationCase, ct);
        if (subjectId == actor.UserId) throw ModerationNotFound();
        if (expanded && (moderationCase.AssignedReviewerUserId != actor.UserId || moderationCase.Status != ModerationCaseStatus.InReview ||
            !await dbContext.ModerationActions.AnyAsync(a => a.ModerationCaseId == caseId && a.Type == ModerationActionType.Escalated, ct)))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "گسترش شاهد نیازمند ارجاع و بازبین مجاز است.");
        if (expectedVersion is not null) EnsureVersion(moderationCase, expectedVersion);
        var detail = await LoadRequiredModerationCaseAsync(caseId, ct);
        var context = Array.Empty<PlatformModerationContextMessage>();
        if (detail.AbuseReport is not null)
        {
            var selected = await dbContext.Messages.AsNoTracking().Where(m => m.Id == detail.AbuseReport.MessageId && m.ConversationId == moderationCase.ConversationId)
                .Select(m => new { m.Sequence }).SingleOrDefaultAsync(ct);
            if (selected is not null)
            {
                var window = expanded ? 20 : 2;
                var messages = dbContext.Messages.AsNoTracking().Where(m => m.ConversationId == moderationCase.ConversationId);
                var before = await messages.Where(m => m.Sequence < selected.Sequence).OrderByDescending(m => m.Sequence).Take(window)
                    .Select(m => new PlatformModerationContextMessage(m.Id, m.SenderUserId, m.Sequence, m.Kind, m.IsDeleted ? null : m.Text, m.SentAtUtc)).ToArrayAsync(ct);
                var after = await messages.Where(m => m.Sequence > selected.Sequence).OrderBy(m => m.Sequence).Take(window)
                    .Select(m => new PlatformModerationContextMessage(m.Id, m.SenderUserId, m.Sequence, m.Kind, m.IsDeleted ? null : m.Text, m.SentAtUtc)).ToArrayAsync(ct);
                context = before.Concat(after).OrderBy(m => m.Sequence).ToArray();
            }
        }
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(actor,
            expanded ? "platform.moderation.evidence.expanded" : "platform.moderation.evidence.view", moderationCase,
            reason, "CaseScoped", $"window={(expanded ? 20 : 2)};contextCount={context.Length}", ct));
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return detail with { ContextMessages = context };
    }

    public async Task<PlatformModerationCaseDetail> ApplySafetyActionAsync(PlatformActorContext actor, Guid caseId,
        byte[] expectedVersion, string reason, ModerationActionType action, DateTime? restrictedUntilUtc, CancellationToken ct)
    {
        var mediaAction = action is ModerationActionType.MediaApproved or ModerationActionType.MediaRejected;
        var mediaResources = await ReadMediaReviewResourcesAsync(caseId, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        await LockMediaReviewResourcesAsync(mediaResources, ct);
        await RequireReviewerAsync(actor, mediaAction, ct);
        var moderationCase = await LockPlatformSafetyCaseAsync(caseId, ct) ?? throw ModerationNotFound();
        EnsureVersion(moderationCase, expectedVersion);
        var targetId = await CaseSubjectAsync(moderationCase, ct);
        if (targetId == actor.UserId || moderationCase.Status != ModerationCaseStatus.InReview || moderationCase.AssignedReviewerUserId != actor.UserId)
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "اقدام فقط در پرونده بازبین مسئول مجاز است.");
        try
        {
            moderationCase.RecordSafetyAction(actor.UserId, actor.OccurredAtUtc);
            dbContext.Entry(moderationCase).Property(c => c.UpdatedAtUtc).IsModified = true;
            if (mediaAction)
            {
                if (!moderationCase.MediaFileAssetId.HasValue || await dbContext.ModerationActions.AnyAsync(a =>
                        a.ModerationCaseId == caseId && a.Type == ModerationActionType.Created && a.ActorUserId == actor.UserId, ct))
                    throw new PlatformException(PlatformErrorCodes.AccessDenied, "درخواست‌کننده نمی‌تواند فایل خودش را تأیید کند.");
                var file = await dbContext.FileAssets.AsNoTracking().SingleAsync(f => f.Id == moderationCase.MediaFileAssetId, ct);
                if (file.Status != MakanApp.Domain.Storage.FileAssetStatus.Ready ||
                    !moderationCase.MatchesMedia(file.Id, file.Sha256Hash!, file.ContentType, file.SizeBytes)) throw ModerationConflict();
                moderationCase.DecideMedia(actor.UserId, action == ModerationActionType.MediaApproved
                    ? ContentSafetyDecision.Allowed : ContentSafetyDecision.Blocked, actor.OccurredAtUtc);
            }
            else if (action == ModerationActionType.MessagingRestricted)
            {
                var user = await dbContext.Users.SingleAsync(u => u.Id == targetId, ct);
                user.RestrictMessaging(restrictedUntilUtc!.Value, actor.OccurredAtUtc);
            }
            else if (action == ModerationActionType.Escalated) moderationCase.Escalate(actor.UserId, actor.OccurredAtUtc);
            else if (action != ModerationActionType.Warned) throw ModerationConflict();
        }
        catch (InvalidOperationException) { throw ModerationConflict(); }
        dbContext.ModerationActions.Add(ModerationAction.Create(caseId, action, actor.UserId, actor.OccurredAtUtc));
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(actor, "platform.moderation.action." + action.ToString().ToLowerInvariant(),
            moderationCase, reason, "InReview", action.ToString(), ct));
        await SaveModerationAsync(ct);
        await transaction.CommitAsync(ct);
        return await LoadRequiredModerationCaseAsync(caseId, ct);
    }

    private async Task<Guid> CaseSubjectAsync(ModerationCase moderationCase, CancellationToken ct) =>
        moderationCase.AbuseReportId.HasValue
            ? await dbContext.AbuseReports.Where(r => r.Id == moderationCase.AbuseReportId.Value).Select(r => r.ReportedUserId).SingleAsync(ct)
            : moderationCase.MediaFileAssetId.HasValue
                ? await dbContext.FileAssets.Where(f => f.Id == moderationCase.MediaFileAssetId.Value).Select(f => f.UploadedByUserId).SingleAsync(ct)
                : await dbContext.SafetyIncidents.Where(i => i.Id == moderationCase.SafetyIncidentId!.Value).Select(i => i.ActorUserId).SingleAsync(ct);

    private async Task RequireReviewerAsync(PlatformActorContext actor, bool stronger, CancellationToken ct)
    {
        var snapshot = await iamStore.GetAuthorizationSnapshotAsync(actor.UserId, ct);
        if (snapshot is null || !snapshot.Permissions.Contains(PlatformPermissionKeys.ModerationReview) ||
            stronger && !snapshot.Permissions.Contains(PlatformPermissionKeys.ModerationEnforce))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "دسترسی پرونده مجاز نیست.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (!await dbContext.UserSessions.AnyAsync(session => session.Id == actor.SessionId &&
                session.UserId == actor.UserId && session.RevokedAtUtc == null &&
                session.ExpiresAtUtc > now && session.AbsoluteExpiresAtUtc > now, ct))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "نشست جاری معتبر لازم است.");
    }
}
