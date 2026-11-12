using MakanApp.Application.Assessment;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Academic;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Assessment;

public sealed class AcademicContextResolver(MakanDbContext db, IAccountOperationContextResolver accounts,
    IAccessContextResolver organizations, PersonalClassAcademicFeatures features) : IAcademicContextResolver
{
    public async Task<AcademicAccessContext> RevalidateAsync(AcademicAccessContext context, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("بازبینی نوشتن به تراکنش نیاز دارد.");
        // پیش از هر خواندن Serializable قفل نوشتن گرفته می‌شود؛ ارتقای هم‌زمان S به U بن‌بست می‌سازد.
        if (context.IsPersonal)
        {
            _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={context.PersonalClassHostUserId}")
                .AsNoTracking().Select(u => u.Id).SingleAsync(ct);
            _ = await db.PersonalClasses.FromSqlInterpolated($"SELECT * FROM [academic].[PersonalClasses] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={context.ClassId}")
                .AsNoTracking().Select(c => c.Id).SingleAsync(ct);
        }
        else if (context.ClassId.HasValue)
            _ = await db.Classes.FromSqlInterpolated($"SELECT * FROM [academic].[Classes] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={context.OrganizationId} AND [Id]={context.ClassId}")
                .AsNoTracking().Select(c => c.Id).SingleAsync(ct);
        _ = await db.UserSessions.FromSqlInterpolated($"SELECT * FROM [identity].[UserSessions] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={context.SessionId} AND [UserId]={context.UserId}")
            .AsNoTracking().Select(s => s.Id).SingleOrDefaultAsync(ct);
        return await ResolveAsync(context.UserId, context.SessionId, context.ResourceKind, context.ResourceId, context.Operation, true, ct);
    }

    public async Task<AcademicAccessContext> ResolveAsync(Guid userId, Guid sessionId, AcademicResourceKind kind,
        Guid resourceId, AcademicOperation operation, bool forUpdate, CancellationToken ct)
    {
        var account = await accounts.PersonalAsync(userId, sessionId, ct);
        if (kind == AcademicResourceKind.Organization)
        {
            var organization = await organizations.ResolveAsync(userId, sessionId, ct);
            EnsureOrganization(organization);
            return new(organization, AcademicScope.Organization(organization.OrganizationId!.Value), null, null,
                PersonalClassLocalRoles.None, PersonalClassAcademicCapabilities.None, null, kind, resourceId, operation);
        }

        var resource = await ResourceAsync(kind, resourceId, ct) ?? throw Missing(kind, operation);
        if (resource.Kind == AcademicClassKind.OrganizationClass)
        {
            var organization = await organizations.ResolveAsync(userId, sessionId, ct);
            EnsureOrganization(organization);
            if (organization.OrganizationId != resource.ContextId) throw Missing(kind, operation);
            return new(organization, AcademicScope.Organization(resource.ContextId), resource.ClassId, null,
                PersonalClassLocalRoles.None, PersonalClassAcademicCapabilities.None, null, kind, resourceId, operation);
        }

        if (forUpdate)
        {
            if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("بازبینی نوشتن به تراکنش نیاز دارد.");
            // ترتیب قفل با چرخهٔ عضویت یکسان است: میزبان، کلاس، سپس نشست عامل.
            var host = await db.PersonalClasses.AsNoTracking().Where(c => c.Id == resource.ClassId)
                .Select(c => c.HostUserId).SingleAsync(ct);
            _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={host}")
                .AsNoTracking().Select(u => u.Id).SingleAsync(ct);
            _ = await db.PersonalClasses.FromSqlInterpolated(
                $"SELECT * FROM [academic].[PersonalClasses] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={resource.ClassId}")
                .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw Missing(kind);
            _ = await db.UserSessions.FromSqlInterpolated(
                $"SELECT * FROM [identity].[UserSessions] WITH (HOLDLOCK) WHERE [Id]={sessionId} AND [UserId]={userId}")
                .AsNoTracking().Select(s => s.Id).SingleOrDefaultAsync(ct);
            _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (HOLDLOCK) WHERE [Id]={userId}")
                .AsNoTracking().Select(u => u.Id).SingleOrDefaultAsync(ct);
            account = await accounts.PersonalAsync(userId, sessionId, ct);
        }
        var classroom = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == resource.ClassId, ct)
            ?? throw Missing(kind);
        var member = await db.PersonalClassParticipants.AsNoTracking()
            .SingleOrDefaultAsync(m => m.PersonalClassId == resource.ClassId && m.StudentUserId == userId && m.EndedAtUtc == null, ct)
            ?? throw Missing(kind);
        // ابتدا مالکیت/عضویت اثبات می‌شود تا پاسخ Gate وجود منبع خصوصی را برای فرد نامرتبط افشا نکند.
        features.EnsureResource(kind, operation);
        var capabilities = await PersonalClassAcademicAccessQuery.Effective(db, resource.ClassId)
            .Where(g => g.MembershipId == member.Id).Select(g => (PersonalClassAcademicCapabilities?)g.Capabilities)
            .SingleOrDefaultAsync(ct) ?? PersonalClassAcademicCapabilities.None;
        var context = new AcademicAccessContext(account, AcademicScope.Personal(resource.ClassId), resource.ClassId,
            member.Id, member.LocalRoles, capabilities, resource.ParticipantId, kind, resourceId, operation)
            { PersonalClassHostUserId = classroom.HostUserId };
        var allowed = operation switch
        {
            AcademicOperation.AuthorAssignment => context.Has(PersonalClassAcademicCapabilities.AuthorAssignments),
            AcademicOperation.AuthorExam => context.Has(PersonalClassAcademicCapabilities.AuthorExams),
            AcademicOperation.Grade => context.Has(PersonalClassAcademicCapabilities.Grade),
            AcademicOperation.Learn or AcademicOperation.OwnResult => context.IsLocalLearner,
            AcademicOperation.Read or AcademicOperation.Upload => context.IsLocalLearner || capabilities != PersonalClassAcademicCapabilities.None,
            _ => false
        };
        if (!allowed) throw Missing(kind);
        if ((forUpdate || operation == AcademicOperation.Upload) && classroom.State != PersonalClassState.Active)
            throw new AssessmentException(MakanApp.Application.Academic.AcademicErrorCodes.ClassNotActive, "کلاس برای عملیات آموزشی فعال نیست.");
        return context;
    }

    private Task<Resource?> ResourceAsync(AcademicResourceKind kind, Guid id, CancellationToken ct) => kind switch
    {
        AcademicResourceKind.OrganizationClass => db.Classes.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new Resource(AcademicClassKind.OrganizationClass, c.OrganizationId, c.Id, null)).SingleOrDefaultAsync(ct),
        AcademicResourceKind.PersonalClass => db.PersonalClasses.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new Resource(AcademicClassKind.PersonalClass, c.Id, c.Id, null)).SingleOrDefaultAsync(ct),
        AcademicResourceKind.Assignment => db.Assignments.AsNoTracking().Where(a => a.Id == id)
            .Select(a => new Resource(a.ContextKind, a.ContextId, a.ClassId, null)).SingleOrDefaultAsync(ct),
        AcademicResourceKind.SubmissionAttempt => (from attempt in db.SubmissionAttempts.AsNoTracking()
            join assignment in db.Assignments on new { attempt.ContextKind, attempt.ContextId, attempt.AssignmentId }
                equals new { assignment.ContextKind, assignment.ContextId, AssignmentId = assignment.Id }
            where attempt.Id == id
            select new Resource(attempt.ContextKind, attempt.ContextId, assignment.ClassId, attempt.PersonalClassMembershipId)).SingleOrDefaultAsync(ct),
        AcademicResourceKind.Exam => db.Exams.AsNoTracking().Where(a => a.Id == id)
            .Select(a => new Resource(a.ContextKind, a.ContextId, a.ClassId, null)).SingleOrDefaultAsync(ct),
        AcademicResourceKind.ExamAttempt => db.ExamAttempts.AsNoTracking().Where(a => a.Id == id)
            .Select(a => new Resource(a.ContextKind, a.ContextId, a.ClassId, a.PersonalClassMembershipId)).SingleOrDefaultAsync(ct),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void EnsureOrganization(AccessContext context)
    {
        if (context.WorkspaceType != WorkspaceType.Organization || context.OrganizationId is null ||
            context.MembershipId is null || context.ActiveRole is null)
            throw new AssessmentException(AssessmentErrorCodes.AssignmentNotAllowed, "زمینهٔ سازمانی معتبر لازم است.");
    }
    private static AssessmentException Missing(AcademicResourceKind kind, AcademicOperation operation = AcademicOperation.Read) => new(kind switch
    {
        AcademicResourceKind.SubmissionAttempt when operation == AcademicOperation.Grade => AssessmentErrorCodes.EvaluationNotFound,
        AcademicResourceKind.ExamAttempt when operation == AcademicOperation.Grade => AssessmentErrorCodes.ExamGradeNotFound,
        AcademicResourceKind.SubmissionAttempt => AssessmentErrorCodes.SubmissionNotFound,
        AcademicResourceKind.ExamAttempt => AssessmentErrorCodes.ExamAttemptNotFound,
        AcademicResourceKind.Exam => AssessmentErrorCodes.ExamNotFound,
        _ => AssessmentErrorCodes.AssignmentNotFound
    }, "منبع آموزشی در دسترس نیست.");

    private sealed record Resource(AcademicClassKind Kind, Guid ContextId, Guid ClassId, Guid? ParticipantId);
}
