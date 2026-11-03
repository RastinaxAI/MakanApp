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
        var active = p.PersonalClassId.HasValue
            ? await db.PersonalClasses.AnyAsync(c => c.Id == p.PersonalClassId && c.Id == p.ClassId && c.State == PersonalClassState.Active, ct)
            : await db.Classes.AnyAsync(c => c.Id == p.ClassId && c.OrganizationId == p.OrganizationId && c.Status == ClassStatus.Active &&
            db.Organizations.Any(o => o.Id == c.OrganizationId && o.Status == MakanApp.Domain.Organization.OrganizationStatus.Active), ct);
        if (p.LessonId is Guid lessonId)
        {
            var current = active && await db.ClassLessons.AnyAsync(x => x.Id == lessonId && x.OrganizationId == p.OrganizationId && x.ClassId == p.ClassId &&
                x.Status == LessonStatus.Published && x.RevisionNumber == p.SourceRevision, ct);
            return (lessonId, lessonId, current);
        }
        if (p.AssignmentVersionId is Guid assignmentVersionId)
        {
            var version = await db.AssignmentVersions.AsNoTracking().SingleAsync(v => v.Id == assignmentVersionId && v.ContextKind == p.ContextKind && v.ContextId == p.ContextId, ct);
            var current = active && version.IsPublished && version.VersionNumber == p.SourceRevision && version.ClassId == p.ClassId && !await db.AssignmentVersions.AnyAsync(v =>
                v.ContextKind == p.ContextKind && v.ContextId == p.ContextId && v.AssignmentId == version.AssignmentId && v.PublishedAtUtc != null && v.VersionNumber > p.SourceRevision, ct);
            return (version.AssignmentId, version.Id, current);
        }
        if (p.SessionId is Guid sessionId)
        {
            var current = active && await db.AcademicSessions.AnyAsync(s => s.Id == sessionId && s.OrganizationId == p.OrganizationId &&
                s.ClassId == p.ClassId && s.ActivityRevision == p.SourceRevision, ct);
            return (sessionId, sessionId, current);
        }
        var examVersion = await db.ExamVersions.AsNoTracking().SingleAsync(v => v.Id == p.ExamVersionId && v.ContextKind == p.ContextKind && v.ContextId == p.ContextId, ct);
        var examCurrent = active && examVersion.IsPublished && examVersion.VersionNumber == p.SourceRevision && await db.Exams.AnyAsync(e => e.Id == examVersion.ExamId && e.ClassId == p.ClassId && e.ContextKind == p.ContextKind && e.ContextId == p.ContextId, ct)
            && !await db.ExamVersions.AnyAsync(v => v.ExamId == examVersion.ExamId && v.ContextKind == p.ContextKind && v.ContextId == p.ContextId && v.PublishedAtUtc != null && v.VersionNumber > p.SourceRevision, ct);
        return (examVersion.ExamId, examVersion.Id, examCurrent);
    }

    public static async Task<AcademicActivityCardResult?> CardAsync(MakanDbContext db, Message message, CancellationToken ct, DateTime nowUtc)
    {
        if (message.AcademicActivityPublicationId is not Guid id || message.IsDeleted) return null;
        var p = await db.AcademicActivityPublications.AsNoTracking().SingleAsync(p => p.Id == id, ct);
        var c = await db.Conversations.AsNoTracking().SingleAsync(c => c.Id == message.ConversationId, ct);
        if (c.OrganizationId != p.OrganizationId || c.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic ||
            (p.PersonalClassId.HasValue ? c.PersonalClassId != p.PersonalClassId : c.AcademicClassId != p.ClassId || c.PersonalClassId.HasValue)) return null;
        var current = await ResolveAsync(db, p, ct);
        var expired = p.DueAtUtc <= nowUtc || p.AvailableUntilUtc <= nowUtc;
        var closed = p.SessionAction is SessionActivityAction.Cancelled or SessionActivityAction.Completed;
        var actionable = current.Current && !expired && !closed;
        var state = !current.Current ? "Superseded" : closed ? p.SessionAction!.Value.ToString() : expired ? "Expired" : "Published";
        return new(p.Id, p.Kind, current.SourceId, current.VersionId, p.SourceRevision, p.OrganizationId, p.ClassId, c.Id, c.Type,
            "System", p.InitiatedByUserId, DateTime.SpecifyKind(p.OccurredAtUtc, DateTimeKind.Utc), p.Title,
            Utc(p.DueAtUtc), Utc(p.AvailableFromUtc), Utc(p.AvailableUntilUtc), state, actionable) { SessionAction = p.SessionAction, ClassRef = p.ClassRef };
    }
    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
