using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfExamStore(
    MakanDbContext dbContext,
    ILogger<EfExamStore> logger, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : IExamStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfExamTransaction(transaction);
    }

    public Task<AcademicClassRecord?> GetClassForUpdateAsync(
        AcademicScope scope,
        Guid classId,
        CancellationToken cancellationToken) =>
        AcademicContextQuery.ClassAsync(dbContext, scope, classId, true, cancellationToken);

    public Task<bool> HasActiveTeacherAssignmentAsync(
        AcademicScope scope,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        dbContext.TeacherAssignments.AnyAsync(
            assignment => assignment.OrganizationId == scope.OrganizationId &&
                          assignment.ClassId == classId &&
                          assignment.TeacherMembershipId == membershipId &&
                          assignment.Status == TeacherAssignmentStatus.Active &&
                          assignment.EndedAtUtc == null,
            cancellationToken);

    public Task<ExamAggregateRecord?> GetCurrentAsync(
        AcademicScope scope,
        Guid examId,
        CancellationToken cancellationToken) =>
        GetAggregateAsync(scope, examId, published: false, forUpdate: false, cancellationToken);

    public Task<ExamAggregateRecord?> GetCurrentForUpdateAsync(
        AcademicScope scope,
        Guid examId,
        CancellationToken cancellationToken) =>
        GetAggregateAsync(scope, examId, published: false, forUpdate: true, cancellationToken);

    public Task<ExamAggregateRecord?> GetLatestPublishedAsync(
        AcademicScope scope,
        Guid examId,
        CancellationToken cancellationToken) =>
        GetAggregateAsync(scope, examId, published: true, forUpdate: false, cancellationToken);

    public Task<bool> IsStudentActivelyEnrolledAsync(
        AcademicScope scope,
        Guid classId,
        Guid userId,
        CancellationToken cancellationToken) =>
        AcademicContextQuery.Participants(dbContext, scope).AnyAsync(p => p.ContextKind == scope.Kind && p.ContextId == scope.Id &&
            p.ClassId == classId && p.IsActive && p.UserId == userId, cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetActiveLearnerUserIdsForUpdateAsync(
        AcademicScope scope,
        Guid classId,
        CancellationToken cancellationToken)
    {
        if (scope.Kind == AcademicClassKind.PersonalClass)
            return await AcademicContextQuery.Participants(dbContext, scope).Where(p => p.ContextKind == scope.Kind && p.ContextId == scope.Id &&
                p.ClassId == classId && p.IsActive && p.UserId.HasValue).Select(p => p.UserId!.Value).Distinct().ToArrayAsync(cancellationToken);
        return await (
            from enrollment in dbContext.Enrollments.FromSqlInterpolated(
                $"SELECT * FROM [academic].[Enrollments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {scope.Id} AND [ClassId] = {classId} AND [Status] = 1 AND [EndedAtUtc] IS NULL")
            join organizationPerson in dbContext.OrganizationPersons
                on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            select user.Id)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ExamSummaryRecord>> GetForManagerAsync(
        AcademicScope scope,
        CancellationToken cancellationToken) =>
        await CreateCurrentSummaryQuery(scope)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExamSummaryRecord>> GetForTeacherAsync(
        AcademicScope scope,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        await (
            from exam in dbContext.Exams
            join version in dbContext.ExamVersions
                on new
                {
                    exam.OrganizationId,
                    ExamId = exam.Id,
                    VersionNumber = exam.CurrentVersionNumber
                }
                equals new
                {
                    version.OrganizationId,
                    version.ExamId,
                    version.VersionNumber
                }
            join academicClass in dbContext.Classes
                on new { exam.OrganizationId, Id = exam.ClassId }
                equals new { OrganizationId = (Guid?)academicClass.OrganizationId, academicClass.Id }
            join assignment in dbContext.TeacherAssignments
                on new { exam.OrganizationId, exam.ClassId }
                equals new { OrganizationId = (Guid?)assignment.OrganizationId, assignment.ClassId }
            where scope.Kind == AcademicClassKind.OrganizationClass && exam.OrganizationId == scope.OrganizationId &&
                  assignment.TeacherMembershipId == membershipId &&
                  assignment.Status == TeacherAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null
            orderby version.AvailableFromUtc, exam.Id
            select new ExamSummaryRecord(exam, version, academicClass.Title))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExamSummaryRecord>> GetForStudentAsync(
        AcademicScope scope,
        Guid userId,
        CancellationToken cancellationToken) =>
        await (
            from exam in dbContext.Exams
            join version in dbContext.ExamVersions
                on new
                {
                    exam.ContextKind, exam.ContextId,
                    ExamId = exam.Id,
                    VersionNumber = exam.LatestPublishedVersionNumber
                }
                equals new
                {
                    version.ContextKind, version.ContextId,
                    version.ExamId,
                    VersionNumber = (int?)version.VersionNumber
                }
            join academicClass in AcademicContextQuery.Classes(dbContext, scope)
                on new { exam.ContextKind, exam.ContextId, exam.ClassId }
                equals new { academicClass.ContextKind, academicClass.ContextId, academicClass.ClassId }
            join participant in AcademicContextQuery.Participants(dbContext, scope)
                on new { exam.ContextKind, exam.ContextId, exam.ClassId }
                equals new { participant.ContextKind, participant.ContextId, participant.ClassId }
            where exam.ContextKind == scope.Kind && exam.ContextId == scope.Id &&
                  exam.Status == ExamStatus.Published &&
                  version.Status == ExamVersionStatus.Published &&
                  participant.IsActive && participant.UserId == userId
            orderby version.AvailableFromUtc, exam.Id
            select new ExamSummaryRecord(exam, version, academicClass.Title))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExamSummaryRecord>> GetForLearnerAsync(
        AcademicScope scope,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken) =>
        await (
            from exam in dbContext.Exams
            join version in dbContext.ExamVersions
                on new
                {
                    exam.OrganizationId,
                    ExamId = exam.Id,
                    VersionNumber = exam.LatestPublishedVersionNumber
                }
                equals new
                {
                    version.OrganizationId,
                    version.ExamId,
                    VersionNumber = (int?)version.VersionNumber
                }
            join academicClass in dbContext.Classes
                on new { exam.OrganizationId, Id = exam.ClassId }
                equals new { OrganizationId = (Guid?)academicClass.OrganizationId, academicClass.Id }
            join enrollment in dbContext.Enrollments
                on new { exam.OrganizationId, exam.ClassId }
                equals new { OrganizationId = (Guid?)enrollment.OrganizationId, enrollment.ClassId }
            where scope.Kind == AcademicClassKind.OrganizationClass && exam.OrganizationId == scope.OrganizationId &&
                  exam.Status == ExamStatus.Published &&
                  version.Status == ExamVersionStatus.Published &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null
            orderby version.AvailableFromUtc, exam.Id
            select new ExamSummaryRecord(exam, version, academicClass.Title))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public void SetOriginalRowVersion(Exam exam, byte[] rowVersion) =>
        dbContext.Entry(exam).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void SetOriginalRowVersion(ExamVersion version, byte[] rowVersion) =>
        dbContext.Entry(version).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void SetOriginalRowVersion(QuestionVersion question, byte[] rowVersion) =>
        dbContext.Entry(question).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void Add(Exam exam) => dbContext.Exams.Add(exam);
    public void Add(ExamVersion version) => dbContext.ExamVersions.Add(version);
    public void Add(QuestionVersion question) => dbContext.QuestionVersions.Add(question);
    public void AddRange(IEnumerable<QuestionVersion> questions) => dbContext.QuestionVersions.AddRange(questions);
    public void ReplaceOptions(
        QuestionVersion question,
        IEnumerable<QuestionOption> previousOptions)
    {
        dbContext.QuestionOptions.RemoveRange(previousOptions);
        dbContext.QuestionOptions.AddRange(question.Options);
    }

    public void Remove(QuestionVersion question) => dbContext.QuestionVersions.Remove(question);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await realtime.CaptureAssessmentAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(
                "Exam optimistic concurrency conflict for entries: {EntryTypes}",
                string.Join(",", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "اطلاعات آزمون هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  sqlException.Message.Contains(
                      "UX_QuestionVersions_Organization_ExamVersion_Order",
                      StringComparison.Ordinal))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamQuestionOrderInvalid,
                "ترتیب سؤال در نسخه آزمون باید یکتا باشد.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  sqlException.Message.Contains(
                      "UX_QuestionOptions_Organization_Question_Order",
                      StringComparison.Ordinal))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamAnswerKeyInvalid,
                "ترتیب گزینه‌های سؤال باید یکتا باشد.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  (sqlException.Message.Contains(
                       "UX_ExamVersions_Organization_Exam_VersionNumber",
                       StringComparison.Ordinal) ||
                   sqlException.Message.Contains(
                       "UX_ExamVersions_Organization_Exam_ActiveDraft",
                       StringComparison.Ordinal)))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "نسخه دیگری برای آزمون هم‌زمان ایجاد شده است.");
        }
    }

    private async Task<ExamAggregateRecord?> GetAggregateAsync(
        AcademicScope scope,
        Guid examId,
        bool published,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        Exam? exam;
        if (forUpdate)
        {
            exam = await dbContext.Exams
                .FromSqlInterpolated(
                    $"SELECT * FROM [assessment].[Exams] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [Id] = {examId}")
                .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            exam = await dbContext.Exams
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.ContextKind == scope.Kind && item.ContextId == scope.Id && item.Id == examId,
                    cancellationToken);
        }

        var versionNumber = published ? exam?.LatestPublishedVersionNumber : exam?.CurrentVersionNumber;
        if (exam is null || !versionNumber.HasValue)
        {
            return null;
        }

        ExamVersion? version;
        if (forUpdate)
        {
            version = await dbContext.ExamVersions
                .FromSqlInterpolated(
                    $"SELECT * FROM [assessment].[ExamVersions] WITH (UPDLOCK, HOLDLOCK) WHERE [ContextKind] = {(int)scope.Kind} AND [ContextId] = {scope.Id} AND [ExamId] = {examId} AND [VersionNumber] = {versionNumber.Value}")
                .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            version = await dbContext.ExamVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.ContextKind == scope.Kind && item.ContextId == scope.Id &&
                            item.ExamId == examId &&
                            item.VersionNumber == versionNumber.Value,
                    cancellationToken);
        }

        if (version is null)
        {
            return null;
        }

        var questionQuery = dbContext.QuestionVersions
            .Include(question => question.Options)
            .Where(question => question.ContextKind == scope.Kind && question.ContextId == scope.Id &&
                               question.ExamVersionId == version.Id)
            .OrderBy(question => question.Order);
        var questions = forUpdate
            ? await questionQuery.ToArrayAsync(cancellationToken)
            : await questionQuery.AsNoTracking().ToArrayAsync(cancellationToken);
        var classData = await AcademicContextQuery.ClassAsync(dbContext, scope, exam.ClassId, false, cancellationToken)
            ?? throw new AssessmentException(AssessmentErrorCodes.ExamNotFound, "کلاس آزمون پیدا نشد.");
        return new ExamAggregateRecord(exam, version, classData.Title, classData.IsActive, questions);
    }

    private IQueryable<ExamSummaryRecord> CreateCurrentSummaryQuery(AcademicScope scope) =>
        from exam in dbContext.Exams
        join version in dbContext.ExamVersions
            on new
            {
                exam.ContextKind, exam.ContextId,
                ExamId = exam.Id,
                VersionNumber = exam.CurrentVersionNumber
            }
            equals new
            {
                version.ContextKind, version.ContextId,
                version.ExamId,
                version.VersionNumber
            }
        join academicClass in AcademicContextQuery.Classes(dbContext, scope)
            on new { exam.ContextKind, exam.ContextId, exam.ClassId }
            equals new { academicClass.ContextKind, academicClass.ContextId, academicClass.ClassId }
        where exam.ContextKind == scope.Kind && exam.ContextId == scope.Id
        orderby version.AvailableFromUtc, exam.Id
        select new ExamSummaryRecord(exam, version, academicClass.Title);

    private sealed class EfExamTransaction(IDbContextTransaction transaction) : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
