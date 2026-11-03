using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Assessment;

internal static class AcademicFileBindingQuery
{
    internal static async Task<bool> IsAvailableAsync(MakanDbContext db, Guid fileId, Guid? assignmentId, Guid? submissionId, Guid? examAttemptId, CancellationToken ct) =>
        !await db.AssignmentVersionAttachments.AnyAsync(a => a.FileAssetId == fileId && (!assignmentId.HasValue || a.AssignmentId != assignmentId), ct) &&
        !await db.SubmissionAttachments.AnyAsync(a => a.FileAssetId == fileId && (!submissionId.HasValue || a.SubmissionAttemptId != submissionId), ct) &&
        !await db.AnswerRevisionAttachments.AnyAsync(a => a.FileAssetId == fileId && (!examAttemptId.HasValue || a.ExamAttemptId != examAttemptId), ct) &&
        !await db.EducationalSourceVersions.AnyAsync(a => a.FileAssetId == fileId, ct) &&
        !await db.ClassLessonAttachments.AnyAsync(a => a.FileAssetId == fileId, ct) &&
        !await db.MessageAttachments.AnyAsync(a => a.FileAssetId == fileId, ct) &&
        !await db.Set<ScheduledMessageAttachment>().AnyAsync(a => a.FileAssetId == fileId, ct) &&
        !await db.ModerationEvidenceAttachments.AnyAsync(a => a.FileAssetId == fileId, ct) &&
        !await db.Users.AnyAsync(a => a.ProfilePhotoFileAssetId == fileId, ct) &&
        !await db.Conversations.AnyAsync(a => a.PhotoFileAssetId == fileId, ct) &&
        !await db.ReportExportJobs.AnyAsync(a => a.Id == fileId || a.OutputFileAssetId == fileId, ct);
}
