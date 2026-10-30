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
    private async Task<bool> CanReadAcademicAnswerAsync(FileAsset file, AccessContext access, CancellationToken ct)
    {
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
                    record.Enrollment.Status != EnrollmentStatus.Active || record.Enrollment.EndedAtUtc is not null) continue;
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
}
