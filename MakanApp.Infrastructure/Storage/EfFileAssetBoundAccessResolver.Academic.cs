using MakanApp.Application.Assessment;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Storage;

public sealed partial class EfFileAssetBoundAccessResolver
{
    private async Task<bool> CanReadAssignmentAttachmentAsync(FileAsset file, AccessContext access, CancellationToken ct)
    {
        var bindings = await (from binding in dbContext.AssignmentVersionAttachments.AsNoTracking()
            join version in dbContext.AssignmentVersions on binding.AssignmentVersionId equals version.Id
            where binding.FileAssetId == file.Id select new { binding.AssignmentId, VersionId = version.Id, version.PublishedAtUtc }).ToArrayAsync(ct);
        foreach (var binding in bindings)
        {
            try
            {
                var context = await academicContexts.ResolveAsync(access.UserId, access.SessionId, AcademicResourceKind.Assignment,
                    binding.AssignmentId, AcademicOperation.Read, false, ct);
                if (context.IsPersonal)
                {
                    if (context.Has(PersonalClassAcademicCapabilities.AuthorAssignments) || context.Has(PersonalClassAcademicCapabilities.Grade)) return true;
                    if (binding.PublishedAtUtc.HasValue && context.IsLocalLearner && await dbContext.AssignmentRecipients.AnyAsync(r =>
                        r.AssignmentVersionId == binding.VersionId && r.PersonalClassMembershipId == context.PersonalMembershipId, ct)) return true;
                }
                else if (context.ActiveRole == OrganizationRole.Manager || context.ActiveRole == OrganizationRole.Teacher &&
                    await grading.HasActiveTeacherAssignmentAsync(context.Scope, context.ClassId!.Value, context.MembershipId!.Value, ct)) return true;
                else if (binding.PublishedAtUtc.HasValue && context.ActiveRole == OrganizationRole.Student && await (
                    from r in dbContext.AssignmentRecipients join e in dbContext.Enrollments on r.EnrollmentId equals e.Id
                    join p in dbContext.OrganizationPersons on e.LearnerOrganizationPersonId equals p.Id
                    join u in dbContext.Users on p.PersonId equals u.PersonId
                    where r.AssignmentVersionId == binding.VersionId && u.Id == access.UserId && e.Status == EnrollmentStatus.Active &&
                        e.EndedAtUtc == null && p.Status == OrganizationPersonStatus.Active && p.EndedAtUtc == null select r.Id).AnyAsync(ct)) return true;
            }
            catch (AssessmentException) { /* نبود مجوز، پاسخ یکسان عدم دسترسی دارد. */ }
        }
        return false;
    }
    private async Task<bool> CanReadAcademicAnswerAsync(FileAsset file, AccessContext access, CancellationToken ct)
    {
        if (file.PersonalClassId.HasValue) return await CanReadPersonalAcademicAnswerAsync(file, access, ct);
        if (access.WorkspaceType != WorkspaceType.Organization || !access.OrganizationId.HasValue ||
            !access.MembershipId.HasValue || access.ActiveRole is not (OrganizationRole.Student or OrganizationRole.Teacher or OrganizationRole.Manager)) return false;
        var submissionIds = await dbContext.SubmissionAttachments.AsNoTracking()
            .Where(item => item.OrganizationId == access.OrganizationId && item.FileAssetId == file.Id)
            .Select(item => item.SubmissionAttemptId).Distinct().ToArrayAsync(ct);
        foreach (var id in submissionIds)
        {
            if (access.ActiveRole == OrganizationRole.Student)
            {
                var record = await submissionStore.GetAttemptAsync(access.OrganizationId.Value, id, ct);
                if (record is null || record.StudentUserId != access.UserId || !record.OrganizationPersonIsActive ||
                    !record.Participation.IsActive) continue;
            }
            try
            {
                var result = await submissions.GetAttemptAsync(access.UserId, access.SessionId, id, ct);
                if (result.ContentVisible && result.Attachments.Any(item => item.FileAssetId == file.Id)) return true;
            }
            catch (AssessmentException e) when (e.Code == AssessmentErrorCodes.SubmissionNotFound) { }
        }

        // تنها نسخهٔ جاری یا نسخهٔ نهایی منبع مجوز است؛ نگه‌داری تاریخچه، مجوز خواندن تاریخچه ایجاد نمی‌کند.
        var candidates = await (from binding in dbContext.AnswerRevisionAttachments.AsNoTracking()
                                join question in dbContext.ExamAttemptQuestions on binding.ExamAttemptQuestionId equals question.Id
                                join attempt in dbContext.ExamAttempts on binding.ExamAttemptId equals attempt.Id
                                where binding.OrganizationId == access.OrganizationId && binding.FileAssetId == file.Id &&
                                    (attempt.Status == ExamAttemptStatus.Finalized
                                        ? dbContext.ExamFinalAnswers.Any(final => final.ExamAttemptQuestionId == question.Id && final.AnswerRevisionId == binding.AnswerRevisionId)
                                        : question.CurrentAnswerRevisionId == binding.AnswerRevisionId)
                                select new { attempt.Id, attempt.ClassId, attempt.Status }).Distinct().ToArrayAsync(ct);
        foreach (var attempt in candidates)
        {
            if (access.ActiveRole == OrganizationRole.Student)
            {
                try { _ = await examAttempts.GetAsync(access.UserId, access.SessionId, attempt.Id, ct); return true; }
                catch (AssessmentException e) when (e.Code == AssessmentErrorCodes.ExamAttemptNotFound) { }
            }
            else if (attempt.Status == ExamAttemptStatus.Finalized &&
                (access.ActiveRole == OrganizationRole.Manager ||
                 await grading.HasActiveTeacherAssignmentAsync(access.OrganizationId.Value, attempt.ClassId, access.MembershipId.Value, ct))) return true;
        }
        return false;
    }

    private async Task<bool> CanReadPersonalAcademicAnswerAsync(FileAsset file, AccessContext access, CancellationToken ct)
    {
        if (access.WorkspaceType != WorkspaceType.Personal) return false;
        var submissionsForFile = await dbContext.SubmissionAttachments.AsNoTracking()
            .Where(a => a.PersonalClassId == file.PersonalClassId && a.FileAssetId == file.Id)
            .Select(a => a.SubmissionAttemptId).Distinct().ToArrayAsync(ct);
        foreach (var id in submissionsForFile)
        {
            try
            {
                var result = await submissions.GetAttemptAsync(access.UserId, access.SessionId, id, ct);
                if (result.ContentVisible && result.Attachments.Any(a => a.FileAssetId == file.Id)) return true;
            }
            catch (AssessmentException) { /* عدم دسترسی یک اتصال، مجوز اتصال دیگر را ایجاد نمی‌کند. */ }
        }
        var attempts = await (from binding in dbContext.AnswerRevisionAttachments.AsNoTracking()
            join question in dbContext.ExamAttemptQuestions on binding.ExamAttemptQuestionId equals question.Id
            join attempt in dbContext.ExamAttempts on binding.ExamAttemptId equals attempt.Id
            where binding.PersonalClassId == file.PersonalClassId && binding.FileAssetId == file.Id &&
                (attempt.Status == ExamAttemptStatus.Finalized
                    ? dbContext.ExamFinalAnswers.Any(final => final.ExamAttemptQuestionId == question.Id && final.AnswerRevisionId == binding.AnswerRevisionId)
                    : question.CurrentAnswerRevisionId == binding.AnswerRevisionId)
            select new { attempt.Id, attempt.Status }).Distinct().ToArrayAsync(ct);
        foreach (var attempt in attempts)
        {
            try
            {
                var context = await academicContexts.ResolveAsync(access.UserId, access.SessionId,
                    AcademicResourceKind.ExamAttempt, attempt.Id, AcademicOperation.Read, false, ct);
                if (context.IsLocalLearner || attempt.Status == ExamAttemptStatus.Finalized && context.Has(PersonalClassAcademicCapabilities.Grade))
                    return true;
            }
            catch (AssessmentException) { /* پاسخ فایل همواره عدم دسترسی امن است. */ }
        }
        return false;
    }
}
