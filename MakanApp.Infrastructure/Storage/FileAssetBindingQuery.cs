using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Storage;

// فهرست واحد اتصال‌های واقعی؛ خواندن، حذف، اتصال منبع و cleanup نباید فهرست‌های متفاوت داشته باشند.
internal static class FileAssetBindingQuery
{
    internal static async Task<bool> IsBoundAsync(MakanDbContext db, Guid id, CancellationToken ct, Guid? exceptSourceId = null,
        bool includeMessagingBindings = true) =>
        await db.SupportTicketAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.SchoolApplicationDocuments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.EducationalSourceVersions.AnyAsync(a => a.FileAssetId == id && (!exceptSourceId.HasValue || a.SourceId != exceptSourceId), ct) ||
        await db.AssignmentVersionAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.SubmissionAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.AnswerRevisionAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        includeMessagingBindings && await db.MessageAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        includeMessagingBindings && await db.ModerationEvidenceAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        includeMessagingBindings && await db.ModerationCases.AnyAsync(c => c.MediaFileAssetId == id, ct) ||
        includeMessagingBindings && await db.Set<ScheduledMessageAttachment>().AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.ClassLessonAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.Users.AnyAsync(a => a.ProfilePhotoFileAssetId == id, ct) ||
        await db.Conversations.AnyAsync(a => a.PhotoFileAssetId == id, ct) ||
        await db.ReportExportJobs.AnyAsync(a => a.Id == id || a.OutputFileAssetId == id, ct);

    internal static Task<bool> HasBlockingModerationEvidenceAsync(MakanDbContext db, Guid id, CancellationToken ct) =>
        db.ModerationEvidenceAttachments.AnyAsync(e => e.FileAssetId == id &&
            db.ModerationCases.Any(c => c.Id == e.ModerationCaseId && c.Source != ModerationCaseSource.MediaReview &&
                !(c.Source == ModerationCaseSource.AutomatedSafety && c.SafetyIncidentId.HasValue &&
                    db.SafetyIncidents.Any(i => i.Id == c.SafetyIncidentId &&
                        i.Decision == ContentSafetyDecision.ReviewRequired && i.PolicyCodes != null &&
                        i.PolicyCodes.Contains(MakanApp.Infrastructure.Messaging.InternalMakanContentSafetyProvider.PolicyVersion + ":Other.UnsupportedMedia")))), ct);
}
