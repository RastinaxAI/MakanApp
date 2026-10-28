using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Messaging;

public sealed class ModerationService(
    IAccessContextResolver accessContextResolver,
    IModerationStore store,
    IFileStorage fileStorage,
    TimeProvider timeProvider) : IModerationService
{
    public async Task<ModerationCasePageResult> GetCasesAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        GetModerationCasesQuery query,
        CancellationToken cancellationToken)
    {
        ValidateIds(conversationId);
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value) ||
            query.Priority.HasValue && !Enum.IsDefined(query.Priority.Value) ||
            query.Page is <= 0 or > 100_000 || query.PageSize <= 0 || query.PageSize > 100)
        {
            throw Error(MessagingErrorCodes.MessagePageInvalid, "پارامترهای صف تعدیل معتبر نیستند.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var result = await store.GetCasesAsync(
            userId,
            conversationId,
            context,
            query.Status,
            query.Priority,
            query.Page,
            query.PageSize,
            cancellationToken);
        return new ModerationCasePageResult(
            result.Items.Select(Map).ToArray(),
            query.Page,
            query.PageSize,
            result.TotalCount);
    }

    public async Task<ModerationCaseDetailResult> GetCaseAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        ValidateIds(conversationId, caseId);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.GetCaseAsync(
            userId,
            conversationId,
            caseId,
            context,
            cancellationToken));
    }

    public Task<ModerationCaseDetailResult> StartReviewAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        StartModerationReviewCommand command,
        CancellationToken cancellationToken) =>
        MutateAsync(
            userId,
            sessionId,
            conversationId,
            caseId,
            command.ExpectedVersion,
            null,
            null,
            cancellationToken);

    public Task<ModerationCaseDetailResult> ResolveAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        CompleteModerationCaseCommand command,
        CancellationToken cancellationToken) =>
        MutateAsync(
            userId,
            sessionId,
            conversationId,
            caseId,
            command.ExpectedVersion,
            command.InternalNote,
            false,
            cancellationToken);

    public Task<ModerationCaseDetailResult> DismissAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        CompleteModerationCaseCommand command,
        CancellationToken cancellationToken) =>
        MutateAsync(
            userId,
            sessionId,
            conversationId,
            caseId,
            command.ExpectedVersion,
            command.InternalNote,
            true,
            cancellationToken);

    public async Task<FileDownloadResult> DownloadEvidenceAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        Guid fileAssetId,
        CancellationToken cancellationToken)
    {
        ValidateIds(conversationId, caseId, fileAssetId);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var file = await store.GetEvidenceFileAsync(
            userId,
            conversationId,
            caseId,
            fileAssetId,
            context,
            cancellationToken);
        if (file.Status != FileAssetStatus.Ready)
        {
            throw Error(MessagingErrorCodes.ModerationCaseNotFound, "شاهد پرونده یافت نشد.");
        }

        var stream = await fileStorage.OpenReadAsync(file.StorageKey, cancellationToken);
        return new FileDownloadResult(stream, file.ContentType, file.OriginalFileName, file.SizeBytes);
    }

    private async Task<ModerationCaseDetailResult> MutateAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid caseId,
        string expectedVersion,
        string? internalNote,
        bool? dismiss,
        CancellationToken cancellationToken)
    {
        ValidateIds(conversationId, caseId);
        if (internalNote?.Trim().Length > ModerationCase.MaximumInternalNoteLength)
        {
            throw Error(MessagingErrorCodes.ModerationStateConflict, "یادداشت داخلی از طول مجاز بیشتر است.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var version = DecodeVersion(expectedVersion);
        var result = dismiss.HasValue
            ? await store.CompleteAsync(
                userId,
                conversationId,
                caseId,
                context,
                version,
                internalNote,
                dismiss.Value,
                UtcNow(),
                cancellationToken)
            : await store.StartReviewAsync(
                userId,
                conversationId,
                caseId,
                context,
                version,
                UtcNow(),
                cancellationToken);
        return Map(result);
    }

    private static ModerationCaseSummaryResult Map(ModerationCase item) => new(
        item.Id,
        item.ConversationId,
        item.Source,
        item.Priority,
        item.Status,
        item.AssignedReviewerUserId,
        item.CreatedAtUtc,
        item.UpdatedAtUtc,
        Convert.ToBase64String(item.RowVersion));

    private static ModerationCaseDetailResult Map(ModerationCaseDetailStoreRecord record) => new(
        record.Case.Id,
        record.Case.ConversationId,
        record.Case.Source,
        record.Case.Priority,
        record.Case.Status,
        record.Case.AssignedReviewerUserId,
        record.Case.CreatedAtUtc,
        record.Case.UpdatedAtUtc,
        record.Case.ReviewStartedAtUtc,
        record.Case.ResolvedAtUtc,
        record.Case.DismissedAtUtc,
        record.Report.Id,
        record.Report.MessageId,
        record.Report.Reason,
        record.Report.Description,
        Map(record.Reporter),
        Map(record.ReportedUser),
        record.Report.ReportedMessageKind,
        record.Report.ReportedContentSnapshot,
        record.Attachments.Select(item => new ModerationEvidenceAttachmentResult(
            item.FileAsset.Id,
            item.FileAsset.OriginalFileName,
            item.FileAsset.ContentType,
            item.FileAsset.SizeBytes)).ToArray(),
        record.Actions.Select(item => new ModerationActionResult(
            item.Action.Type,
            item.Action.ActorUserId,
            item.Action.CreatedAtUtc)).ToArray(),
        Convert.ToBase64String(record.Case.RowVersion));

    private static SafeMessagingIdentityResult Map(SafeMessagingIdentityRecord identity) =>
        new(identity.UserId, identity.Username, identity.DisplayName, identity.ProfilePhoto);

    private static byte[] DecodeVersion(string value)
    {
        try
        {
            var version = Convert.FromBase64String(value);
            return version.Length == 8 ? version : throw new FormatException();
        }
        catch (FormatException)
        {
            throw Error(MessagingErrorCodes.ModerationStateConflict, "نسخه پرونده معتبر نیست.");
        }
    }

    private static void ValidateIds(params Guid[] ids)
    {
        if (ids.Any(id => id == Guid.Empty))
        {
            throw Error(MessagingErrorCodes.ModerationCaseNotFound, "پرونده تعدیل یافت نشد.");
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static MessagingException Error(string code, string message) => new(code, message);
}
