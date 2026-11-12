using MakanApp.Application.Assessment;
using MakanApp.Application.Commerce;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Notification;
using MakanApp.Application.Organization;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.Infrastructure.Notification;

public sealed class EfNotificationTargetResolver(MakanDbContext db, ProductNotificationAccess access, IAccessContextResolver contexts,
    IAssignmentService assignments, IExamService exams, IEvaluationService evaluations, IExamGradingService grades) : INotificationTargetResolver
{
    public async Task<NotificationTargetResult?> ResolveAsync(Guid user, Guid session, DomainNotification notification, CancellationToken ct)
    {
        if (!await access.AllowsAsync(user, notification.ResourceType, notification.ResourceId, ct)) return null;
        var id = notification.ResourceId;
        var requiredOrganization = notification.OrganizationId;
        string? route;
        try
        {
            switch (notification.ResourceType)
            {
                case NotificationResourceType.SupportTicket:
                    route = await db.SupportTickets.AnyAsync(t => t.Id == id && t.OwnerUserId == user, ct) ? $"/api/v1/support/tickets/{id:D}" : $"/api/admin/v1/support/tickets/{id:D}"; break;
                case NotificationResourceType.SchoolApplication:
                    route = await db.SchoolApplications.AnyAsync(a => a.Id == id && a.ApplicantUserId == user, ct) ? $"/api/v1/school-applications/{id:D}" : $"/api/admin/v1/school-applications/{id:D}"; break;
                case NotificationResourceType.Order:
                    var org = await db.Orders.Where(o => o.Id == id && o.PurchaserUserId == user).Select(o => o.OrganizationId).SingleAsync(ct);
                    requiredOrganization = org;
                    route = org.HasValue ? $"/api/v1/commerce/orders/{id:D}" : $"/api/v1/commerce/personal/orders/{id:D}"; break;
                case NotificationResourceType.Membership: route = "/api/v1/workspaces/snapshot"; break;
                case NotificationResourceType.OrganizationInvitation: route = "/api/v1/invitations"; break;
                case NotificationResourceType.Conversation:
                    if (!await AccountInboxAccessQuery.Conversations(db, user).AnyAsync(c => c.Id == id, ct)) return null;
                    route = $"/api/v1/conversations/{id:D}"; break;
                case NotificationResourceType.Assignment:
                    await assignments.GetAsync(user, session, id, ct); route = $"/api/v1/academic/assignments/{id:D}"; break;
                case NotificationResourceType.Exam:
                    var examContext = await contexts.ResolveAsync(user, session, ct);
                    if (examContext.ActiveRole is OrganizationRole.Manager or OrganizationRole.Teacher)
                    { await exams.GetEditorAsync(user, session, id, ct); route = $"/api/v1/academic/exams/{id:D}/editor"; }
                    else { await exams.GetStudentPreviewAsync(user, session, id, ct); route = $"/api/v1/academic/exams/{id:D}/student-preview"; }
                    break;
                case NotificationResourceType.SubmissionAttempt:
                    var context = await contexts.ResolveAsync(user, session, ct);
                    if (context.ActiveRole == OrganizationRole.Parent)
                    { await evaluations.GetGuardianReleasedAssignmentResultAsync(user, session, id, ct); route = $"/api/v1/academic/submission-attempts/{id:D}/guardian-result"; }
                    else { await evaluations.GetReleasedAssignmentResultAsync(user, session, id, ct); route = $"/api/v1/academic/submission-attempts/{id:D}/result"; }
                    break;
                case NotificationResourceType.ExamAttempt:
                    var gradeContext = await contexts.ResolveAsync(user, session, ct);
                    if (gradeContext.ActiveRole == OrganizationRole.Parent)
                    { await grades.GetGuardianResultAsync(user, session, id, ct); route = $"/api/v1/academic/exam-attempts/{id:D}/guardian-result"; }
                    else { await grades.GetStudentResultAsync(user, session, id, ct); route = $"/api/v1/academic/exam-attempts/{id:D}/result"; }
                    break;
                default: return null;
            }
        }
        catch (Exception e) when (e is AssessmentException or OrganizationException or GuardianException or IdentityException ||
            e is CommerceException commerce && commerce.Code == CommerceErrorCodes.FeatureNotEntitled) { return null; }
        return new(notification.ResourceType.ToString(), id, route, requiredOrganization);
    }
}
