using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class SubmissionService
{
    public async Task<IReadOnlyCollection<SubmissionAttemptResult>> GetMyAttemptsAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var context = await GetStudentContextAsync(userId, sessionId, AcademicResourceKind.Assignment, assignmentId, cancellationToken);
        var eligibility = await _store.GetEligibilityForStudentAsync(
            context.Scope,
            assignmentId,
            context.UserId,
            cancellationToken);
        if (eligibility is null)
        {
            throw RecipientNotFound();
        }

        var attempts = await _store.GetAttemptsForStudentAsync(
            context.Scope,
            assignmentId,
            context.UserId,
            cancellationToken);
        return attempts.Select(attempt => ToResult(attempt, true)).ToArray();
    }

    public async Task<SubmissionAttemptResult> GetAttemptAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.SubmissionAttempt,
            attemptId, AcademicOperation.Read, false, cancellationToken);
        var record = await _store.GetAttemptAsync(
            context.Scope,
            attemptId,
            cancellationToken) ?? throw SubmissionNotFound();

        var includeContent = context.IsPersonal
            ? context.IsLocalLearner && record.StudentUserId == userId ||
                context.Has(PersonalClassAcademicCapabilities.Grade) && record.Attempt.Status == SubmissionAttemptStatus.Submitted
                ? true : throw SubmissionNotFound()
            : context.ActiveRole switch
        {
            OrganizationRole.Student when record.StudentUserId == context.UserId => true,
            OrganizationRole.Parent when
                record.Attempt.Status == SubmissionAttemptStatus.Submitted &&
                context.SubjectOrganizationPersonId == record.Participation.LearnerOrganizationPersonId => false,
            OrganizationRole.Manager when record.Attempt.Status == SubmissionAttemptStatus.Submitted => true,
            OrganizationRole.Teacher when
                record.Attempt.Status == SubmissionAttemptStatus.Submitted &&
                await _store.HasActiveTeacherAssignmentAsync(
                    record.Attempt.Scope,
                    record.Assignment.ClassId,
                    context.MembershipId!.Value,
                    cancellationToken) => true,
            _ => throw SubmissionNotFound()
        };

        return ToResult(record, includeContent);
    }

    public async Task<IReadOnlyCollection<SubmissionAttemptResult>> GetSubmittedAttemptsAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.Assignment,
            assignmentId, AcademicOperation.Grade, false, cancellationToken);
        var assignment = await _store.GetAssignmentAsync(
            context.Scope,
            assignmentId,
            cancellationToken) ?? throw SubmissionNotFound();

        var allowed = context.IsPersonal ? context.Has(PersonalClassAcademicCapabilities.Grade) : context.ActiveRole switch
        {
            OrganizationRole.Manager => true,
            OrganizationRole.Teacher => await _store.HasActiveTeacherAssignmentAsync(
                assignment.Scope,
                assignment.ClassId,
                context.MembershipId!.Value,
                cancellationToken),
            _ => false
        };
        if (!allowed)
        {
            throw SubmissionNotFound();
        }

        var attempts = await _store.GetSubmittedAttemptsAsync(
            context.Scope,
            assignmentId,
            cancellationToken);
        return attempts.Select(attempt => ToResult(attempt, true)).ToArray();
    }
}
