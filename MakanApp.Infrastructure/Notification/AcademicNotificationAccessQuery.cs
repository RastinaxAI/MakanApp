using MakanApp.Domain.Assessment;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.Infrastructure.Notification;

internal static class AcademicNotificationAccessQuery
{
    internal static async Task<bool> AllowsDeliveryAsync(MakanDbContext db, DomainNotification notification, DateTime now, CancellationToken ct)
    {
        var user = notification.RecipientUserId;
        switch (notification.ResourceType)
        {
            case NotificationResourceType.Assignment:
                var assignment = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(a => a.Id == notification.ResourceId, ct);
                if (assignment is null || !assignment.IsRecipientVisible || assignment.OrganizationId != notification.OrganizationId ||
                    !await ClassIsActive(db, assignment.Scope, assignment.ClassId, ct)) return false;
                return await AcademicContextQuery.Participants(db, assignment.Scope).AnyAsync(p => p.ClassId == assignment.ClassId && p.IsActive && p.UserId == user &&
                    db.AssignmentRecipients.Any(r => r.AssignmentId == assignment.Id && r.ContextKind == assignment.ContextKind &&
                        r.ContextId == assignment.ContextId && r.ParticipantId == p.ParticipantId &&
                        db.AssignmentVersions.Any(v => v.Id == r.AssignmentVersionId && v.VersionNumber == assignment.CurrentVersionNumber)), ct) &&
                    await HasRole(db, assignment.Scope, user, OrganizationRole.Student, ct);
            case NotificationResourceType.Exam:
                var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == notification.ResourceId, ct);
                if (exam is null || !exam.LatestPublishedVersionNumber.HasValue || exam.OrganizationId != notification.OrganizationId ||
                    !await ClassIsActive(db, exam.Scope, exam.ClassId, ct)) return false;
                if (!await db.ExamVersions.AnyAsync(v => v.ExamId == exam.Id && v.VersionNumber == exam.LatestPublishedVersionNumber &&
                    v.Status == ExamVersionStatus.Published && v.PublishedAtUtc != null, ct)) return false;
                return await AcademicContextQuery.Participants(db, exam.Scope).AnyAsync(p => p.ClassId == exam.ClassId && p.IsActive && p.UserId == user, ct) &&
                    await HasRole(db, exam.Scope, user, OrganizationRole.Student, ct);
            case NotificationResourceType.SubmissionAttempt:
                var submission = await (from s in db.SubmissionAttempts
                    join a in db.Assignments on s.AssignmentId equals a.Id
                    where s.Id == notification.ResourceId && db.GradeReleases.Any(r => r.SubmissionAttemptId == s.Id)
                    select new { s.ContextKind, s.ContextId, s.OrganizationId, a.ClassId, s.ParticipantId }).SingleOrDefaultAsync(ct);
                return submission is not null && submission.OrganizationId == notification.OrganizationId &&
                    await AllowsResult(db, new(submission.ContextKind, submission.ContextId), submission.ClassId, submission.ParticipantId, user, now, ct);
            case NotificationResourceType.ExamAttempt:
                var attempt = await db.ExamAttempts.Where(a => a.Id == notification.ResourceId && db.ExamGradeReleases.Any(r => r.ExamAttemptId == a.Id))
                    .Select(a => new { a.ContextKind, a.ContextId, a.OrganizationId, a.ClassId, a.ParticipantId }).SingleOrDefaultAsync(ct);
                return attempt is not null && attempt.OrganizationId == notification.OrganizationId &&
                    await AllowsResult(db, new(attempt.ContextKind, attempt.ContextId), attempt.ClassId, attempt.ParticipantId, user, now, ct);
            default: return true;
        }
    }

    private static async Task<bool> AllowsResult(MakanDbContext db, AcademicScope scope, Guid classId, Guid participantId,
        Guid user, DateTime now, CancellationToken ct)
    {
        if (!await ClassIsActive(db, scope, classId, ct)) return false;
        var participant = await AcademicContextQuery.Participants(db, scope).Where(p => p.ClassId == classId && p.ParticipantId == participantId && p.IsActive)
            .Select(p => new { p.UserId, p.LearnerOrganizationPersonId }).SingleOrDefaultAsync(ct);
        if (participant is null) return false;
        if (participant.UserId == user) return await HasRole(db, scope, user, OrganizationRole.Student, ct);
        if (scope.Kind != AcademicClassKind.OrganizationClass || participant.LearnerOrganizationPersonId is not Guid learner) return false;
        return await HasRole(db, scope, user, OrganizationRole.Parent, ct) && await db.GuardianRelations.AnyAsync(g =>
            g.OrganizationId == scope.OrganizationId && g.LearnerOrganizationPersonId == learner && g.GuardianUserId == user &&
            g.Status == GuardianRelationStatus.Active && g.EndedAtUtc == null && g.ValidFromUtc <= now, ct);
    }

    private static Task<bool> ClassIsActive(MakanDbContext db, AcademicScope scope, Guid classId, CancellationToken ct) =>
        AcademicContextQuery.Classes(db, scope).AnyAsync(c => c.ClassId == classId && c.IsActive, ct);

    private static Task<bool> HasRole(MakanDbContext db, AcademicScope scope, Guid user, OrganizationRole role, CancellationToken ct) =>
        scope.Kind == AcademicClassKind.PersonalClass ? Task.FromResult(true) : db.Memberships.AnyAsync(m =>
            m.OrganizationId == scope.OrganizationId && m.UserId == user && m.Status == MembershipStatus.Active && m.EndedAtUtc == null &&
            db.Organizations.Any(o => o.Id == m.OrganizationId && o.Status == OrganizationStatus.Active) &&
            db.RoleAssignments.Any(r => r.MembershipId == m.Id && r.Role == role && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null), ct);
}
