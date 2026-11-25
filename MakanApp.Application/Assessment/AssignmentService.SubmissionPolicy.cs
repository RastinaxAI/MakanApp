using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed partial class AssignmentService
{
    public async Task<AssignmentResult> UpdateSubmissionPolicyAsync(Guid userId, Guid sessionId, Guid assignmentId,
        UpdateAssignmentSubmissionPolicyCommand command, CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, AcademicResourceKind.Assignment,
            assignmentId, AcademicOperation.AuthorAssignment, false, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        context = await _accessContextResolver.RevalidateAsync(context, cancellationToken);
        var record = await GetAssignmentForUpdateAsync(context.Scope, assignmentId, cancellationToken);
        await EnsureCanManageClassAsync(context, record.Assignment.ClassId, cancellationToken);
        EnsureClassActive(record);
        if (record.Assignment.Status != AssignmentStatus.Published || !record.Version.IsPublished)
            throw new AssessmentException(AssessmentErrorCodes.AssignmentNotSubmittable,
                "این عملیات فقط برای تکلیف و نسخهٔ منتشرشده مجاز است.");

        var expectedAssignmentRowVersion = DecodeRowVersion(command.ExpectedAssignmentRowVersion);
        var expectedVersionRowVersion = DecodeRowVersion(command.ExpectedVersionRowVersion);
        if (!record.Assignment.RowVersion.SequenceEqual(expectedAssignmentRowVersion) || !record.Version.RowVersion.SequenceEqual(expectedVersionRowVersion))
            throw new AssessmentException(AssessmentErrorCodes.ConcurrencyConflict,
                "Policy تکلیف هم‌زمان تغییر کرده است؛ اطلاعات تازه را دریافت کنید.");

        var nowUtc = UtcNow();
        var oldDue = DateTime.SpecifyKind(record.Version.DueAtUtc, DateTimeKind.Utc);
        var oldAllowLate = record.Version.AllowLateSubmission;
        try { record.Version.UpdatePublishedSubmissionPolicy(command.DueAtUtc, command.AllowLateSubmission, nowUtc); }
        catch (ArgumentException exception) { throw MapValidationException(exception); }

        if (oldDue == command.DueAtUtc && oldAllowLate == command.AllowLateSubmission)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToResult(record.Assignment, record.Version, record.ClassTitle);
        }

        record.Assignment.MarkSubmissionPolicyUpdated(nowUtc);
        _store.SetOriginalRowVersion(record.Assignment, expectedAssignmentRowVersion);
        _store.SetOriginalRowVersion(record.Version, expectedVersionRowVersion);
        _store.MarkSubmissionPolicyChanged(record.Assignment);
        _audit.RecordSubmissionPolicyChange(context, new(record.Assignment.Id, record.Version.Id,
            record.Version.VersionNumber, oldDue, command.DueAtUtc, oldAllowLate, command.AllowLateSubmission, nowUtc));
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(record.Assignment, record.Version, record.ClassTitle);
    }
}
