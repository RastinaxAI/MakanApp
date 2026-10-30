using MakanApp.Application.Messaging;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

internal static class AcademicActivityProjection
{
    public static async Task<(Guid SourceId, Guid VersionId, bool Current)> ResolveAsync(MakanDbContext db, AcademicActivityPublication p, CancellationToken ct)
    {
        var active = await db.Classes.AnyAsync(c => c.Id == p.ClassId && c.OrganizationId == p.OrganizationId && c.Status == ClassStatus.Active &&
            db.Organizations.Any(o => o.Id == c.OrganizationId && o.Status == MakanApp.Domain.Organization.OrganizationStatus.Active), ct);
        if (p.LessonId is Guid lessonId)
        {
            var current = active && await db.ClassLessons.AnyAsync(x => x.Id == lessonId && x.OrganizationId == p.OrganizationId && x.ClassId == p.ClassId &&
                x.Status == LessonStatus.Published && x.RevisionNumber == p.SourceRevision, ct);
            return (lessonId, lessonId, current);
        }
        if (p.AssignmentVersionId is Guid assignmentVersionId)
        {
            var version = await db.AssignmentVersions.AsNoTracking().SingleAsync(v => v.Id == assignmentVersionId && v.OrganizationId == p.OrganizationId, ct);
            var current = active && version.IsPublished && version.VersionNumber == p.SourceRevision && version.ClassId == p.ClassId && !await db.AssignmentVersions.AnyAsync(v =>
                v.OrganizationId == p.OrganizationId && v.AssignmentId == version.AssignmentId && v.PublishedAtUtc != null && v.VersionNumber > p.SourceRevision, ct);
            return (version.AssignmentId, version.Id, current);
        }
        var examVersion = await db.ExamVersions.AsNoTracking().SingleAsync(v => v.Id == p.ExamVersionId && v.OrganizationId == p.OrganizationId, ct);
        var examCurrent = active && examVersion.IsPublished && examVersion.VersionNumber == p.SourceRevision && await db.Exams.AnyAsync(e => e.Id == examVersion.ExamId && e.ClassId == p.ClassId && e.OrganizationId == p.OrganizationId, ct)
            && !await db.ExamVersions.AnyAsync(v => v.ExamId == examVersion.ExamId && v.OrganizationId == p.OrganizationId && v.PublishedAtUtc != null && v.VersionNumber > p.SourceRevision, ct);
        return (examVersion.ExamId, examVersion.Id, examCurrent);
    }

    public static async Task<AcademicActivityCardResult?> CardAsync(MakanDbContext db, Message message, CancellationToken ct)
    {
        if (message.AcademicActivityPublicationId is not Guid id || message.IsDeleted) return null;
        var p = await db.AcademicActivityPublications.AsNoTracking().SingleAsync(p => p.Id == id, ct);
        var c = await db.Conversations.AsNoTracking().SingleAsync(c => c.Id == message.ConversationId, ct);
        if (c.OrganizationId != p.OrganizationId || c.AcademicClassId != p.ClassId || c.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic) return null;
        var current = await ResolveAsync(db, p, ct);
        return new(p.Id, p.Kind, current.SourceId, current.VersionId, p.SourceRevision, p.OrganizationId, p.ClassId, c.Id, c.Type,
            "System", p.InitiatedByUserId, DateTime.SpecifyKind(p.OccurredAtUtc, DateTimeKind.Utc), p.Title,
            Utc(p.DueAtUtc), Utc(p.AvailableFromUtc), Utc(p.AvailableUntilUtc), current.Current ? "Published" : "Superseded", current.Current);
    }
    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
