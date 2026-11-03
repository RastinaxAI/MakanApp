using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamGradingService
{
    public async Task<IReadOnlyCollection<ExamGradingQueueItemDto>> GetQueueAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken)
    {
        var context = await QueueAccessAsync(userId, sessionId, examId, query, cancellationToken);
        IReadOnlyCollection<ExamGradingQueueRecord> records = context.IsPersonal
            ? await store.GetQueueForManagerAsync(context.Scope, examId, query, cancellationToken)
            : context.ActiveRole switch
        {
            OrganizationRole.Manager => await store.GetQueueForManagerAsync(
                context.Scope,
                examId,
                query,
                cancellationToken),
            OrganizationRole.Teacher => await store.GetQueueForTeacherAsync(
                context.Scope,
                context.MembershipId!.Value,
                examId,
                query,
                cancellationToken),
            _ => throw GradeNotAllowed()
        };

        return records
            .Select(ToQueueItem)
            .Where(item => !query.Status.HasValue || item.Status == query.Status.Value)
            .ToArray();
    }

    public async Task<AcademicPage<ExamGradingQueueItemDto>> GetQueuePageAsync(Guid userId, Guid sessionId, Guid examId,
        ExamGradingQueueQuery query, CancellationToken ct)
    {
        var context = await QueueAccessAsync(userId, sessionId, examId, query, ct);
        var page = await store.GetQueuePageAsync(context.Scope,
            !context.IsPersonal && context.ActiveRole == OrganizationRole.Teacher ? context.MembershipId : null, examId, query, ct);
        return new(page.Items.Select(ToQueueItem).ToArray(), page.Page, page.PageSize, page.Total);
    }

    private async Task<AcademicAccessContext> QueueAccessAsync(Guid userId, Guid sessionId, Guid examId, ExamGradingQueueQuery query, CancellationToken ct)
    {
        PersonalAcademicReadService.Validate(new(query.Page ?? 1, query.PageSize ?? 25));
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw new AssessmentException(AssessmentErrorCodes.AcademicQueryInvalid, "فیلتر وضعیت معتبر نیست.");
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.Exam,
            examId, AcademicOperation.Grade, false, ct);
        if (!context.IsPersonal && context.ActiveRole is not (OrganizationRole.Manager or OrganizationRole.Teacher)) throw GradeNotAllowed();
        return context;
    }

    public async Task<ExamGradeDto> GetForGradingAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.ExamAttempt,
            attemptId, AcademicOperation.Grade, false, cancellationToken);
        var aggregate = await GetAggregateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        var revision = aggregate.LatestRevision ?? throw GradeNotFound();
        return ToGradeDto(aggregate, revision, aggregate.LatestQuestionGrades);
    }

    public async Task<StudentExamResultDto> GetStudentResultAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.ExamAttempt,
            attemptId, AcademicOperation.OwnResult, false, cancellationToken);
        var aggregate = await GetAggregateAsync(context, attemptId, cancellationToken);
        if ((!context.IsLocalLearner && context.ActiveRole != OrganizationRole.Student) ||
            context.SubjectOrganizationPersonId.HasValue ||
            aggregate.Source.StudentUserId != context.UserId)
        {
            throw GradeNotFound();
        }

        EnsureFinalized(aggregate.Source.Attempt);
        var releaseStatus = GetReleaseStatus(aggregate);
        var released = aggregate.CurrentReleasedRevision;
        var release = aggregate.CurrentRelease;
        return new StudentExamResultDto(
            aggregate.Source.Attempt.Id,
            aggregate.Source.Exam.Id,
            aggregate.Source.Version.Id,
            aggregate.Source.Attempt.AttemptNumber,
            aggregate.Source.Attempt.FinalizedAtUtc!.Value,
            releaseStatus,
            released?.RevisionNumber,
            release?.ReleasedAtUtc,
            released?.TotalScore,
            released is null ? null : aggregate.Source.Version.MaxScore,
            released?.LearnerFeedback);
    }

    public async Task<GuardianExamResultDto> GetGuardianResultAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.ExamAttempt,
            attemptId, AcademicOperation.Read, false, cancellationToken);
        var aggregate = await GetAggregateAsync(context, attemptId, cancellationToken);
        if (context.ActiveRole != OrganizationRole.Parent ||
            !context.SubjectOrganizationPersonId.HasValue ||
            context.SubjectOrganizationPersonId.Value !=
            aggregate.Source.Participation.LearnerOrganizationPersonId)
        {
            throw GradeNotFound();
        }

        EnsureFinalized(aggregate.Source.Attempt);
        var releaseStatus = GetReleaseStatus(aggregate);
        var released = aggregate.CurrentReleasedRevision;
        var release = aggregate.CurrentRelease;
        return new GuardianExamResultDto(
            aggregate.Source.Attempt.Id,
            aggregate.Source.Exam.Id,
            aggregate.Source.Version.Id,
            aggregate.Source.Attempt.AttemptNumber,
            aggregate.Source.Attempt.FinalizedAtUtc!.Value,
            releaseStatus,
            released?.RevisionNumber,
            release?.ReleasedAtUtc,
            released?.TotalScore,
            released is null ? null : aggregate.Source.Version.MaxScore,
            released?.GuardianVisibleFeedback);
    }

    private static ExamGradingQueueItemDto ToQueueItem(ExamGradingQueueRecord record)
    {
        var status = record.LatestRevision switch
        {
            null => ExamGradingQueueStatus.AwaitingGrading,
            { Status: ExamGradeRevisionStatus.ReadyForRelease } => ExamGradingQueueStatus.ReadyForRelease,
            { Status: ExamGradeRevisionStatus.Released } => ExamGradingQueueStatus.Released,
            { IsMutable: true, SupersedesExamGradeRevisionId: not null } =>
                ExamGradingQueueStatus.CorrectionDraft,
            _ => ExamGradingQueueStatus.Draft
        };
        return new ExamGradingQueueItemDto(
            record.Source.Attempt.Id,
            record.Source.Exam.Id,
            record.Source.Version.Id,
            record.Source.Exam.ClassId,
            record.Source.Version.Title,
            record.Source.StudentDisplayName,
            record.Source.Attempt.AttemptNumber,
            record.Source.Attempt.FinalizedAtUtc!.Value,
            record.Source.Version.MaxScore,
            status,
            record.LatestRevision?.RevisionNumber,
            record.Source.Questions.Any(question =>
                question.Question.Type == ExamQuestionType.Descriptive));
    }

    private static ExamResultReleaseStatus GetReleaseStatus(ExamGradeAggregateRecord aggregate) =>
        aggregate.CurrentReleasedRevision is not null && aggregate.CurrentRelease is not null
            ? ExamResultReleaseStatus.Released
            : aggregate.LatestRevision is null
                ? ExamResultReleaseStatus.AwaitingGrading
                : ExamResultReleaseStatus.AwaitingRelease;
}
