using MakanApp.Application.Assessment;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Academic;

namespace MakanApp.Infrastructure.Realtime;

// همان read use case عمومی مرجع مجوز است؛ قانون نمره، Draft یا Guardian اینجا دوباره تعریف نمی‌شود.
public sealed class AssessmentRealtimeAccess(IAssignmentService assignments, IExamService exams,
    ISubmissionService submissions, IEvaluationService evaluations, IExamGradingService examGrades, IExamAttemptService attempts,
    IAcademicContextResolver academicContexts)
{
    internal async Task<bool> AllowsAsync(AccessContext c, InvalidationEnvelope item, CancellationToken ct)
    {
        if (item.ResourceId is not Guid id) return false;
        try
        {
            if (item.PersonalClassId.HasValue) return await PersonalAsync(c, item, id, ct);
            if (item.StaffOnly && c.ActiveRole is not (OrganizationRole.Manager or OrganizationRole.Teacher)) return false;
            switch (item.Key)
            {
                case "assessment.assignment": await assignments.GetAsync(c.UserId, c.SessionId, id, ct); break;
                case "assessment.exam":
                    if (c.ActiveRole is OrganizationRole.Manager or OrganizationRole.Teacher) await exams.GetEditorAsync(c.UserId, c.SessionId, id, ct);
                    else await exams.GetStudentPreviewAsync(c.UserId, c.SessionId, id, ct);
                    break;
                case "assessment.submission":
                    if (c.ActiveRole is OrganizationRole.Manager or OrganizationRole.Teacher) await evaluations.GetSubmissionForEvaluationAsync(c.UserId, c.SessionId, id, ct);
                    else await submissions.GetAttemptAsync(c.UserId, c.SessionId, id, ct);
                    break;
                case "assessment.grade":
                    if (c.ActiveRole == OrganizationRole.Parent) await evaluations.GetGuardianReleasedAssignmentResultAsync(c.UserId, c.SessionId, id, ct);
                    else if (c.ActiveRole == OrganizationRole.Student) await evaluations.GetReleasedAssignmentResultAsync(c.UserId, c.SessionId, id, ct);
                    else await evaluations.GetEvaluationAsync(c.UserId, c.SessionId, id, ct);
                    break;
                case "assessment.exam-grade":
                    if (c.ActiveRole == OrganizationRole.Parent) await examGrades.GetGuardianResultAsync(c.UserId, c.SessionId, id, ct);
                    else if (c.ActiveRole == OrganizationRole.Student) await examGrades.GetStudentResultAsync(c.UserId, c.SessionId, id, ct);
                    else await examGrades.GetForGradingAsync(c.UserId, c.SessionId, id, ct);
                    break;
                case "assessment.exam-attempt": await attempts.GetAsync(c.UserId, c.SessionId, id, ct); break;
                default: return false;
            }
            return true;
        }
        catch (Exception e) when (e is AssessmentException or OrganizationException or GuardianException or IdentityException) { return false; }
    }

    private async Task<bool> PersonalAsync(AccessContext c, InvalidationEnvelope item, Guid id, CancellationToken ct)
    {
        var kind = item.Key switch
        {
            "assessment.assignment" => AcademicResourceKind.Assignment,
            "assessment.exam" => AcademicResourceKind.Exam,
            "assessment.submission" or "assessment.grade" => AcademicResourceKind.SubmissionAttempt,
            _ => AcademicResourceKind.ExamAttempt
        };
        var access = await academicContexts.ResolveAsync(c.UserId, c.SessionId, kind, id, AcademicOperation.Read, false, ct);
        if (!access.IsPersonal || access.ClassId != item.PersonalClassId || access.PersonalMembershipId != item.PersonalMembershipId) return false;
        switch (item.Key)
        {
            case "assessment.assignment":
                if (item.StaffOnly && !access.Has(PersonalClassAcademicCapabilities.AuthorAssignments)) return false;
                await assignments.GetAsync(c.UserId, c.SessionId, id, ct); break;
            case "assessment.exam":
                if (access.Has(PersonalClassAcademicCapabilities.AuthorExams)) await exams.GetEditorAsync(c.UserId, c.SessionId, id, ct);
                else if (!item.StaffOnly) await exams.GetStudentPreviewAsync(c.UserId, c.SessionId, id, ct);
                else return false;
                break;
            case "assessment.submission": await submissions.GetAttemptAsync(c.UserId, c.SessionId, id, ct); break;
            case "assessment.grade":
                if (item.StaffOnly || !access.IsLocalLearner) await evaluations.GetEvaluationAsync(c.UserId, c.SessionId, id, ct);
                else await evaluations.GetReleasedAssignmentResultAsync(c.UserId, c.SessionId, id, ct);
                break;
            case "assessment.exam-grade":
                if (item.StaffOnly || !access.IsLocalLearner)
                {
                    // صف باید قبل از ایجاد اولین Draft نمره نیز از پاسخ نهایی باخبر شود.
                    if (!access.Has(PersonalClassAcademicCapabilities.Grade)) return false;
                }
                else await examGrades.GetStudentResultAsync(c.UserId, c.SessionId, id, ct);
                break;
            case "assessment.exam-attempt": return access.IsLocalLearner;
            default: return false;
        }
        return true;
    }
}
