using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Application.Notification;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Notification;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamGradingService(
    IAcademicContextResolver accessContextResolver,
    IExamGradingStore store,
    TimeProvider timeProvider,
    IOutbox outbox,
    IAcademicAuditWriter audit) : IExamGradingService
{
    public async Task<ExamGradeDto> CreateOrGetDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.ExamAttempt, attemptId, AcademicOperation.Grade, false, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        context = await accessContextResolver.RevalidateAsync(context, cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        if (aggregate.LatestRevision is { IsMutable: true })
        {
            await transaction.CommitAsync(cancellationToken);
            return ToGradeDto(aggregate, aggregate.LatestRevision, aggregate.LatestQuestionGrades);
        }

        if (aggregate.LatestRevision is not null)
        {
            throw AlreadyReleased();
        }

        var nowUtc = UtcNow();
        var gradeRevision = ExamGradeRevision.CreateDraft(
            aggregate.Source.Attempt.Scope,
            aggregate.Source.Attempt.Id,
            context.ActorMembershipId,
            nowUtc);
        var questionGrades = aggregate.Source.Questions
            .Select(question => ExamQuestionGrade.Create(
                gradeRevision,
                aggregate.Source.Attempt,
                question.AttemptQuestion,
                question.Question,
                question.FinalAnswer,
                question.FinalRevision,
                context.ActorMembershipId,
                nowUtc))
            .ToArray();
        gradeRevision.Recalculate(questionGrades, aggregate.Source.Version.MaxScore, nowUtc);
        store.Add(gradeRevision);
        store.AddRange(questionGrades);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToGradeDto(aggregate, gradeRevision, questionGrades);
    }

    public async Task<ExamGradeDto> GradeQuestionAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        Guid attemptQuestionId,
        GradeExamQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.ExamAttempt, attemptId, AcademicOperation.Grade, false, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        context = await accessContextResolver.RevalidateAsync(context, cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        var revision = RequireMutableRevision(aggregate);
        ApplyExpectedRowVersion(revision, command.ExpectedGradeRowVersion);
        var questionGrade = aggregate.LatestQuestionGrades.SingleOrDefault(
            item => item.ExamAttemptQuestionId == attemptQuestionId) ?? throw GradeNotFound();
        var nowUtc = UtcNow();
        try
        {
            questionGrade.ReviewManually(
                command.AwardedScore,
                command.LearnerFeedback,
                command.EvaluatorPrivateNote,
                context.ActorMembershipId,
                nowUtc);
            revision.Recalculate(
                aggregate.LatestQuestionGrades,
                aggregate.Source.Version.MaxScore,
                nowUtc);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw MapValidationException(exception);
        }

        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToGradeDto(aggregate, revision, aggregate.LatestQuestionGrades);
    }

    public async Task<ExamGradeDto> CompleteReviewAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CompleteExamGradeCommand command,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.ExamAttempt, attemptId, AcademicOperation.Grade, false, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        context = await accessContextResolver.RevalidateAsync(context, cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        var revision = RequireMutableRevision(aggregate);
        ApplyExpectedRowVersion(revision, command.ExpectedGradeRowVersion);
        try
        {
            revision.CompleteReview(
                aggregate.LatestQuestionGrades,
                aggregate.Source.Version.MaxScore,
                command.LearnerFeedback,
                command.GuardianVisibleFeedback,
                command.EvaluatorPrivateNote,
                UtcNow());
        }
        catch (ExamGradeIncompleteException)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamGradeQuestionNotReviewed,
                "همه سؤال‌های نیازمند بازبینی دستی هنوز بررسی نشده‌اند.");
        }
        catch (ArgumentException exception)
        {
            throw MapValidationException(exception);
        }

        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToGradeDto(aggregate, revision, aggregate.LatestQuestionGrades);
    }

    public async Task<ExamGradeReleaseDto> ReleaseAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        ReleaseExamGradeCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ClientOperationId == Guid.Empty)
        {
            throw ReleaseIdempotencyConflict();
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.ExamAttempt, attemptId, AcademicOperation.Grade, false, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        context = await accessContextResolver.RevalidateAsync(context, cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        var requestHash = ComputeReleaseRequestHash(attemptId, command.ExpectedGradeRowVersion);
        var operationRelease = aggregate.Releases.SingleOrDefault(
            release => release.ClientOperationId == command.ClientOperationId);
        if (operationRelease is not null)
        {
            if (!string.Equals(operationRelease.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw ReleaseIdempotencyConflict();
            }

            var operationRevision = aggregate.Revisions.Single(
                revision => revision.Id == operationRelease.ExamGradeRevisionId);
            await transaction.CommitAsync(cancellationToken);
            return ToReleaseDto(operationRelease, operationRevision, aggregate.Source.Version.MaxScore);
        }

        var revision = aggregate.LatestRevision ?? throw GradeNotFound();
        if (revision.IsReleased)
        {
            var existingRelease = aggregate.Releases.SingleOrDefault(
                release => release.ExamGradeRevisionId == revision.Id) ?? throw ReleaseConflict();
            await transaction.CommitAsync(cancellationToken);
            return ToReleaseDto(existingRelease, revision, aggregate.Source.Version.MaxScore);
        }

        ApplyExpectedRowVersion(revision, command.ExpectedGradeRowVersion);
        var nowUtc = UtcNow();
        if (aggregate.CurrentReleasedRevision is not null)
        {
            if (revision.SupersedesExamGradeRevisionId != aggregate.CurrentReleasedRevision.Id)
            {
                throw ReleaseConflict();
            }

            aggregate.CurrentReleasedRevision.MarkSuperseded(nowUtc);
            await store.SaveChangesAsync(cancellationToken);
        }
        else if (revision.SupersedesExamGradeRevisionId.HasValue)
        {
            throw ReleaseConflict();
        }

        try
        {
            revision.Release(
                aggregate.LatestQuestionGrades,
                aggregate.Source.Version.MaxScore,
                nowUtc);
        }
        catch (ExamGradeIncompleteException)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamGradeIncomplete,
                "نمره آزمون برای انتشار کامل نیست.");
        }

        var release = ExamGradeRelease.Create(
            revision,
            context.ActorMembershipId,
            command.ClientOperationId,
            requestHash,
            nowUtc);
        IReadOnlyCollection<Guid> guardianUserIds = aggregate.Source.Participation.LearnerOrganizationPersonId is Guid learnerId
            ? await store.GetActiveGuardianUserIdsAsync(aggregate.Source.Attempt.Scope, learnerId, nowUtc, cancellationToken)
            : [];

        store.Add(release);
        var recipients = new List<NotificationRecipientIntent>
        {
            new(
                aggregate.Source.StudentUserId,
                "نتیجه آزمون منتشر شد",
                "نتیجه جدید یک آزمون برای شما منتشر شده است.")
        };
        recipients.AddRange(guardianUserIds.Select(guardianUserId => new NotificationRecipientIntent(
            guardianUserId,
            "نتیجه آموزشی منتشر شد",
            "نتیجه جدید آموزشی برای فرزند شما منتشر شده است.")));
        outbox.EnqueueRange(NotificationOutboxContract.Create(new NotificationIntent(
            release.Id,
            aggregate.Source.Attempt.OrganizationId,
            NotificationCategory.Educational,
            NotificationType.ExamGradeReleased,
            NotificationResourceType.ExamAttempt,
            aggregate.Source.Attempt.Id,
            nowUtc,
            recipients)));
        audit.Record(context, revision.SupersedesExamGradeRevisionId.HasValue
                ? AcademicAuditAction.ExamResultCorrected : AcademicAuditAction.ExamResultReleased,
            attemptId, revision.Id, revision.RevisionNumber, nowUtc);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToReleaseDto(release, revision, aggregate.Source.Version.MaxScore);
    }

    public async Task<ExamGradeDto> CorrectReleasedAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CorrectReleasedExamGradeCommand command,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.ExamAttempt, attemptId, AcademicOperation.Grade, false, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        context = await accessContextResolver.RevalidateAsync(context, cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        var released = aggregate.CurrentReleasedRevision ?? throw GradeNotReleased();
        if (aggregate.LatestRevision?.Id != released.Id)
        {
            throw ReleaseConflict();
        }

        ApplyExpectedRowVersion(released, command.ExpectedReleasedGradeRowVersion);
        ExamGradeRevision correction;
        try
        {
            correction = ExamGradeRevision.CreateCorrection(
                released,
                context.ActorMembershipId,
                command.CorrectionReason,
                UtcNow());
        }
        catch (ArgumentException exception)
        {
            throw MapValidationException(exception);
        }

        var copiedGrades = aggregate.LatestQuestionGrades
            .Select(grade => grade.CopyTo(correction))
            .ToArray();
        correction.Recalculate(copiedGrades, aggregate.Source.Version.MaxScore, UtcNow());
        store.Add(correction);
        store.AddRange(copiedGrades);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToGradeDto(aggregate, correction, copiedGrades);
    }
}
