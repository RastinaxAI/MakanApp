using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Storage;

// فهرست واحد اتصال‌های واقعی؛ خواندن، حذف، اتصال منبع و cleanup نباید فهرست‌های متفاوت داشته باشند.
internal static class FileAssetBindingQuery
{
    internal static async Task<bool> IsBoundAsync(MakanDbContext db, Guid id, CancellationToken ct, Guid? exceptSourceId = null) =>
        await db.SupportTicketAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.SchoolApplicationDocuments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.EducationalSourceVersions.AnyAsync(a => a.FileAssetId == id && (!exceptSourceId.HasValue || a.SourceId != exceptSourceId), ct) ||
        await db.AssignmentVersionAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.SubmissionAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.AnswerRevisionAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.MessageAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.ModerationEvidenceAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.Set<ScheduledMessageAttachment>().AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.ClassLessonAttachments.AnyAsync(a => a.FileAssetId == id, ct) ||
        await db.Users.AnyAsync(a => a.ProfilePhotoFileAssetId == id, ct) ||
        await db.Conversations.AnyAsync(a => a.PhotoFileAssetId == id, ct) ||
        await db.ReportExportJobs.AnyAsync(a => a.Id == id || a.OutputFileAssetId == id, ct);
}
