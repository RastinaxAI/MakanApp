using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore : IConversationPhotoStore
{
    public async Task<ConversationPhotoMutationResult> SetPhotoAsync(Guid actorId, Guid sessionId, Guid conversationId, Guid? fileAssetId,
        byte[] expectedVersion, AccessContext context, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var (conversation, actor) = await LoadManagedForMutationAsync(actorId, conversationId, context, ct, allowOfficialContent: true);
        var now = await EnsureAdvancedSessionAsync(actorId, sessionId, context, ct);
        await RequireContentManagementAsync(conversation, actor, actorId, ConversationAdminPermission.ChangeInfo, ct);
        await EnsureActorCanMessageAsync(actorId, now, ct);
        await EnsureNotBannedAsync(conversation.Id, actorId, now, ct);
        if (conversation.PhotoFileAssetId == fileAssetId)
        {
            var unchanged = await ReadConversationPhotoAsync(conversation, ct);
            await transaction.CommitAsync(ct);
            return new(conversation.Id, unchanged, Convert.ToBase64String(conversation.RowVersion));
        }
        if (!conversation.RowVersion.SequenceEqual(expectedVersion)) throw StateConflict();
        FileAsset? file = null;
        if (fileAssetId.HasValue)
        {
            file = await dbContext.FileAssets.FromSqlInterpolated(
                $"SELECT * FROM [storage].[FileAssets] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={fileAssetId.Value}").SingleOrDefaultAsync(ct);
            if (file is null || file.PersonalClassId.HasValue || file.UploadedByUserId != actorId ||
                file.OrganizationId.HasValue && file.OrganizationId != conversation.OrganizationId) throw PhotoUnavailable();
            if (file.Status != FileAssetStatus.Ready)
                throw new StorageException(StorageErrorCodes.FileNotReady, "عکس گفتگو باید فایل آماده باشد.");
            if (file.ContentType is not ("image/jpeg" or "image/png"))
                throw new StorageException(StorageErrorCodes.FileTypeNotAllowed, "عکس گفتگو فقط JPEG یا PNG معتبر است.");
            if (file.RetainedAtUtc.HasValue || await IsOtherPhotoBindingAsync(file.Id, ct)) throw PhotoUnavailable();
            // rowversion فایل، حذف هم‌زمان با نسخهٔ قبل از Binding را نامعتبر می‌کند.
            dbContext.Entry(file).Property(f => f.Status).IsModified = true;
        }
        dbContext.Entry(conversation).Property(c => c.RowVersion).OriginalValue = expectedVersion;
        conversation.SetPhoto(fileAssetId);
        await AuditAdvancedAsync(actorId, sessionId, "messaging.conversation.photo", "Conversation", conversation.Id.ToString("D"),
            fileAssetId.HasValue ? "تغییر عکس گفتگو" : "حذف عکس گفتگو", System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"), now, ct);
        await SaveManagementAsync(ct);
        await AppendChangeAsync(conversation, MessagingChangeType.ConversationChanged, conversation.Id, VersionOf(conversation.RowVersion), now, actorId, null, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal();
        return new(conversation.Id, file is null ? null : new(file.Id, file.ContentType), Convert.ToBase64String(conversation.RowVersion));
    }

    private async Task<bool> IsOtherPhotoBindingAsync(Guid id, CancellationToken ct) =>
        await dbContext.Conversations.AnyAsync(c => c.PhotoFileAssetId == id, ct) ||
        await dbContext.Users.AnyAsync(u => u.ProfilePhotoFileAssetId == id, ct) ||
        await dbContext.EducationalSourceVersions.AnyAsync(v => v.FileAssetId == id, ct) ||
        await dbContext.ClassLessonAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await dbContext.SubmissionAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await dbContext.AssignmentVersionAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await dbContext.AnswerRevisionAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await dbContext.MessageAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await dbContext.Set<ScheduledMessageAttachment>().AnyAsync(a => a.FileAssetId == id, ct) ||
        await dbContext.ModerationEvidenceAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await dbContext.ReportExportJobs.AnyAsync(j => j.Id == id || j.OutputFileAssetId == id, ct);

    private Task<ConversationPhotoResult?> ReadConversationPhotoAsync(Conversation conversation, CancellationToken ct) =>
        dbContext.FileAssets.AsNoTracking().Where(f => f.Id == conversation.PhotoFileAssetId && f.Status == FileAssetStatus.Ready &&
            (f.ContentType == "image/jpeg" || f.ContentType == "image/png"))
            .Select(f => new ConversationPhotoResult(f.Id, f.ContentType)).SingleOrDefaultAsync(ct);

    private static StorageException PhotoUnavailable() => new(StorageErrorCodes.FileNotFound, "فایل قابل اتصال به عکس گفتگو نیست.");
}
