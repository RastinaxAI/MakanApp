using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    private static bool CanReevaluateMediaIncident(SafetyIncident incident, SendMessageStoreCommand command) =>
        incident.Decision == ContentSafetyDecision.ReviewRequired && command.AttachmentIds.Count > 0 &&
        ((incident.PolicyCodes ?? string.Empty).Split(',').Contains(
            InternalMakanContentSafetyProvider.PolicyVersion + ":Other.UnsupportedMedia", StringComparer.Ordinal) ||
         (incident.PolicyCodes ?? string.Empty).Split(',').Contains(
             ManualMediaSafetyProvider.PolicyVersion + ":ReviewRequired", StringComparer.Ordinal));

    private async Task<SafetyIncidentStoreResult> RecordMediaReviewOutcomeAsync(Conversation conversation, Guid userId,
        SendMessageStoreCommand command, byte[] payloadHash, ContentSafetyResult result, SafetyIncident? existingIncident,
        FileAsset[] files, DateTime nowUtc, CancellationToken ct)
    {
        if (files.Length == 0 || command.Kind is not (MessageKind.File or MessageKind.Image))
            throw new ContentSafetyProviderUnavailableException("درخواست بررسی رسانه معتبر نیست.");
        var decision = ContentSafetyDecision.Allowed;
        foreach (var file in files.OrderBy(f => f.Id))
        {
            // ترتیب مشترک: گفتگو، فایل‌ها با شناسهٔ مرتب، پرونده؛ تصمیم و ایجاد Case در همان تراکنش‌اند.
            await dbContext.Entry(file).ReloadAsync(ct);
            EnsureFileCanBeAttached(file, conversation, userId, command.Kind, command.AllowSourceAttachments);
            var review = await dbContext.ModerationCases.SingleOrDefaultAsync(c => c.MediaFileAssetId == file.Id, ct);
            if (review is null)
            {
                if (await MakanApp.Infrastructure.Storage.FileAssetBindingQuery.IsBoundAsync(dbContext, file.Id, ct, includeMessagingBindings: false) ||
                    await MakanApp.Infrastructure.Storage.FileAssetBindingQuery.HasBlockingModerationEvidenceAsync(dbContext, file.Id, ct))
                    throw AttachmentNotAllowed();
                review = ModerationCase.CreateForMediaReview(conversation.Id, file.Id, file.Sha256Hash!, file.ContentType, file.SizeBytes, nowUtc);
                dbContext.ModerationCases.Add(review);
                dbContext.ModerationEvidenceAttachments.Add(ModerationEvidenceAttachment.Create(review.Id, file.Id, nowUtc));
                dbContext.ModerationActions.Add(ModerationAction.Create(review.Id, ModerationActionType.Created, userId, nowUtc));
                file.Retain(nowUtc);
            }
            var current = review.MatchesMedia(file.Id, file.Sha256Hash!, file.ContentType, file.SizeBytes)
                ? review.MediaDecision!.Value : ContentSafetyDecision.Blocked;
            if (current == ContentSafetyDecision.Blocked) decision = ContentSafetyDecision.Blocked;
            else if (current == ContentSafetyDecision.ReviewRequired && decision != ContentSafetyDecision.Blocked)
                decision = ContentSafetyDecision.ReviewRequired;
        }
        if (result.Decision == ContentSafetyDecision.Blocked || existingIncident?.Decision == ContentSafetyDecision.Blocked)
            decision = ContentSafetyDecision.Blocked;
        if (decision == ContentSafetyDecision.Allowed) return new(null, decision);
        var incident = existingIncident;
        if (incident is null)
        {
            incident = SafetyIncident.Create(conversation.Id, userId, command.ClientMessageId, command.Kind, decision,
                decision == ContentSafetyDecision.Blocked ? "High" : "Review", null,
                $"{ManualMediaSafetyProvider.PolicyVersion}:{decision}", command.Text,
                HashSafetyContent(command.Text, files.Select(f => f.Sha256Hash!)), payloadHash, nowUtc);
            dbContext.SafetyIncidents.Add(incident);
        }
        await SaveSafetyAsync(ct);
        LogSafetyDecision(incident, existingIncident is not null);
        return new(null, decision, incident.Id);
    }

    private async Task EnsureCurrentMediaReviewAllowsAsync(FileAsset[] files, CancellationToken ct)
    {
        if (files.Length == 0) return;
        var ids = files.Select(f => f.Id).ToArray();
        // این خواندن در تراکنش Send انجام می‌شود؛ دادهٔ قدیمیِ preflight مجوز ارسال نمی‌شود.
        var reviews = await dbContext.ModerationCases.AsNoTracking().Where(c => c.MediaFileAssetId.HasValue && ids.Contains(c.MediaFileAssetId.Value)).ToArrayAsync(ct);
        foreach (var review in reviews)
        {
            var file = files.Single(f => f.Id == review.MediaFileAssetId);
            var allowed = review.MediaDecision == ContentSafetyDecision.Allowed && review.MatchesMedia(file.Id, file.Sha256Hash!, file.ContentType, file.SizeBytes);
            if (!allowed)
                throw new MessagingException(review.MediaDecision == ContentSafetyDecision.ReviewRequired
                    ? MessagingErrorCodes.MessageSafetyReviewRequired : MessagingErrorCodes.MessageSafetyBlocked,
                    "نتیجهٔ جاری بررسی رسانه اجازهٔ ارسال نمی‌دهد.") { SafetyReference = review.Id, MediaReviewAvailable = true };
        }
    }
}
