using MakanApp.Application.Assessment;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;

namespace MakanApp.Infrastructure.Realtime;

// همان read use case عمومی مرجع مجوز است؛ قانون نمره، Draft یا Guardian اینجا دوباره تعریف نمی‌شود.
public sealed class AssessmentRealtimeAccess(IAssignmentService assignments, IExamService exams,
    ISubmissionService submissions, IEvaluationService evaluations, IExamGradingService examGrades, IExamAttemptService attempts)
{
    internal async Task<bool> AllowsAsync(AccessContext c, InvalidationEnvelope item, CancellationToken ct)
    {
        if (item.ResourceId is not Guid id || item.StaffOnly && c.ActiveRole is not (OrganizationRole.Manager or OrganizationRole.Teacher)) return false;
        try
        {
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
}
