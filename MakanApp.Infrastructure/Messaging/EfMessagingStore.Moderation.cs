using System.Data;
using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<ModerationCaseListStoreResult> GetCasesAsync(
        Guid reviewerUserId,
        Guid conversationId,
        AccessContext accessContext,
        ModerationCaseStatus? status,
        ModerationPriority? priority,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureConversationReviewerAsync(
            reviewerUserId,
            conversationId,
            accessContext,
            cancellationToken);
        var query = dbContext.ModerationCases
            .AsNoTracking()
            .Where(item =>
                item.ConversationId == conversationId &&
                item.QueueScope == ModerationQueueScope.ConversationModerators &&
                item.AbuseReportId != null &&
                !dbContext.AbuseReports.Any(report =>
                    report.Id == item.AbuseReportId &&
                    report.ReportedUserId == reviewerUserId));
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(item => item.Priority == priority.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return new ModerationCaseListStoreResult(items, totalCount);
    }

    public async Task<ModerationCaseDetailStoreRecord> GetCaseAsync(
        Guid reviewerUserId,
        Guid conversationId,
        Guid caseId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        await EnsureConversationReviewerAsync(
            reviewerUserId,
            conversationId,
            accessContext,
            cancellationToken);
        await EnsureReviewerIsNotReportedUserAsync(caseId, reviewerUserId, cancellationToken);
        return await LoadModerationCaseDetailAsync(
            conversationId,
            caseId,
            cancellationToken);
    }

    public async Task<ModerationCaseDetailStoreRecord> StartReviewAsync(
        Guid reviewerUserId,
        Guid conversationId,
        Guid caseId,
        AccessContext accessContext,
        byte[] expectedVersion,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await EnsureConversationReviewerAsync(
            reviewerUserId,
            conversationId,
            accessContext,
            cancellationToken);
        var moderationCase = await LockConversationModerationCaseAsync(
            conversationId,
            caseId,
            cancellationToken);
        await EnsureReviewerIsNotReportedUserAsync(caseId, reviewerUserId, cancellationToken);
        EnsureModerationVersion(moderationCase, expectedVersion);
        try
        {
            moderationCase.StartReview(reviewerUserId, nowUtc);
        }
        catch (InvalidOperationException exception)
        {
            throw ModerationStateConflict(exception);
        }

        var report = await dbContext.AbuseReports
            .SingleAsync(item => item.Id == moderationCase.AbuseReportId, cancellationToken);
        report.MarkUnderReview();
        dbContext.ModerationActions.Add(ModerationAction.Create(
            moderationCase.Id,
            ModerationActionType.ReviewStarted,
            reviewerUserId,
            nowUtc));
        await SaveModerationAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadModerationCaseDetailAsync(conversationId, caseId, cancellationToken);
    }

    public async Task<ModerationCaseDetailStoreRecord> CompleteAsync(
        Guid reviewerUserId,
        Guid conversationId,
        Guid caseId,
        AccessContext accessContext,
        byte[] expectedVersion,
        string? internalNote,
        bool dismiss,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await EnsureConversationReviewerAsync(
            reviewerUserId,
            conversationId,
            accessContext,
            cancellationToken);
        var moderationCase = await LockConversationModerationCaseAsync(
            conversationId,
            caseId,
            cancellationToken);
        await EnsureReviewerIsNotReportedUserAsync(caseId, reviewerUserId, cancellationToken);
        EnsureModerationVersion(moderationCase, expectedVersion);
        var report = await dbContext.AbuseReports
            .SingleAsync(item => item.Id == moderationCase.AbuseReportId, cancellationToken);
        try
        {
            if (dismiss)
            {
                moderationCase.Dismiss(reviewerUserId, internalNote, nowUtc);
                report.Dismiss();
            }
            else
            {
                moderationCase.Resolve(reviewerUserId, internalNote, nowUtc);
                report.Resolve();
            }
        }
        catch (InvalidOperationException exception)
        {
            throw ModerationStateConflict(exception);
        }

        dbContext.ModerationActions.Add(ModerationAction.Create(
            moderationCase.Id,
            dismiss ? ModerationActionType.Dismissed : ModerationActionType.Resolved,
            reviewerUserId,
            nowUtc));
        await SaveModerationAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadModerationCaseDetailAsync(conversationId, caseId, cancellationToken);
    }

    public async Task<FileAsset> GetEvidenceFileAsync(
        Guid reviewerUserId,
        Guid conversationId,
        Guid caseId,
        Guid fileAssetId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        await EnsureConversationReviewerAsync(
            reviewerUserId,
            conversationId,
            accessContext,
            cancellationToken);
        await EnsureReviewerIsNotReportedUserAsync(caseId, reviewerUserId, cancellationToken);
        var file = await (
            from moderationCase in dbContext.ModerationCases.AsNoTracking()
            join evidence in dbContext.ModerationEvidenceAttachments.AsNoTracking()
                on moderationCase.Id equals evidence.ModerationCaseId
            join candidate in dbContext.FileAssets.AsNoTracking()
                on evidence.FileAssetId equals candidate.Id
            where moderationCase.Id == caseId &&
                  moderationCase.ConversationId == conversationId &&
                  moderationCase.QueueScope == ModerationQueueScope.ConversationModerators &&
                  candidate.Id == fileAssetId
            select candidate)
            .SingleOrDefaultAsync(cancellationToken);
        return file ?? throw ModerationCaseNotFound();
    }

    private async Task EnsureConversationReviewerAsync(
        Guid reviewerUserId,
        Guid conversationId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw ModerationCaseNotFound();
        var participant = await dbContext.ConversationParticipants
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.ConversationId == conversationId &&
                item.UserId == reviewerUserId &&
                item.Status == ConversationParticipantStatus.Active &&
                item.EndedAtUtc == null,
                cancellationToken) ?? throw ModerationCaseNotFound();
        if (!await CanAccessScopeAsync(
                conversation,
                reviewerUserId,
                accessContext,
                cancellationToken))
        {
            throw ModerationCaseNotFound();
        }

        if (conversation.ManagementPolicy != ConversationManagementPolicy.UserManaged ||
            participant.Role != ConversationParticipantRole.Owner &&
            (participant.Role != ConversationParticipantRole.Admin ||
             !participant.AdminPermissions.HasFlag(ConversationAdminPermission.ReviewReports)))
        {
            throw new MessagingException(
                MessagingErrorCodes.ModerationPermissionRequired,
                "مجوز بررسی گزارش‌های این گفتگو وجود ندارد.");
        }
    }

    private async Task<ModerationCase> LockConversationModerationCaseAsync(
        Guid conversationId,
        Guid caseId,
        CancellationToken cancellationToken) =>
        await dbContext.ModerationCases
            .FromSqlInterpolated($"SELECT * FROM [messaging].[ModerationCases] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {caseId} AND [ConversationId] = {conversationId} AND [QueueScope] = 1")
            .SingleOrDefaultAsync(cancellationToken) ?? throw ModerationCaseNotFound();

    private async Task EnsureReviewerIsNotReportedUserAsync(
        Guid caseId,
        Guid reviewerUserId,
        CancellationToken cancellationToken)
    {
        if (await (
                from moderationCase in dbContext.ModerationCases.AsNoTracking()
                join report in dbContext.AbuseReports.AsNoTracking()
                    on moderationCase.AbuseReportId equals report.Id
                where moderationCase.Id == caseId && report.ReportedUserId == reviewerUserId
                select report.Id)
            .AnyAsync(cancellationToken))
        {
            throw ModerationCaseNotFound();
        }
    }

    private async Task<ModerationCaseDetailStoreRecord> LoadModerationCaseDetailAsync(
        Guid conversationId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var moderationCase = await dbContext.ModerationCases
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.Id == caseId &&
                item.ConversationId == conversationId &&
                item.QueueScope == ModerationQueueScope.ConversationModerators &&
                item.Source == ModerationCaseSource.UserReport,
                cancellationToken) ?? throw ModerationCaseNotFound();
        var report = await dbContext.AbuseReports
            .AsNoTracking()
            .SingleAsync(item => item.Id == moderationCase.AbuseReportId, cancellationToken);
        var attachmentRows = await (
            from evidence in dbContext.ModerationEvidenceAttachments.AsNoTracking()
            join file in dbContext.FileAssets.AsNoTracking() on evidence.FileAssetId equals file.Id
            where evidence.ModerationCaseId == caseId
            orderby evidence.CreatedAtUtc, evidence.Id
            select new { evidence, file })
            .ToArrayAsync(cancellationToken);
        var actions = await dbContext.ModerationActions
            .AsNoTracking()
            .Where(action => action.ModerationCaseId == caseId)
            .OrderBy(action => action.CreatedAtUtc)
            .ThenBy(action => action.Id)
            .Select(action => new ModerationActionStoreRecord(action))
            .ToArrayAsync(cancellationToken);
        return new ModerationCaseDetailStoreRecord(
            moderationCase,
            report,
            await LoadSafeIdentityAsync(report.ReporterUserId, cancellationToken),
            await LoadSafeIdentityAsync(report.ReportedUserId, cancellationToken),
            attachmentRows.Select(item => new ModerationEvidenceAttachmentStoreRecord(
                item.evidence,
                item.file)).ToArray(),
            actions);
    }

    private void EnsureModerationVersion(ModerationCase moderationCase, byte[] expectedVersion)
    {
        if (!CryptographicOperations.FixedTimeEquals(moderationCase.RowVersion, expectedVersion))
        {
            throw ModerationStateConflict();
        }

        dbContext.Entry(moderationCase).Property(item => item.RowVersion).OriginalValue = expectedVersion;
    }

    private async Task SaveModerationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw ModerationStateConflict(exception);
        }
    }

    private static MessagingException ModerationCaseNotFound() => new(
        MessagingErrorCodes.ModerationCaseNotFound,
        "پرونده تعدیل یافت نشد.");

    private static MessagingException ModerationStateConflict(Exception? exception = null) => new(
        MessagingErrorCodes.ModerationStateConflict,
        "وضعیت پرونده هم‌زمان تغییر کرده است؛ نسخه تازه را دریافت کنید.",
        exception);
}
