using System.Diagnostics;
using System.Text.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfAcademicAuditWriter(MakanDbContext db) : IAcademicAuditWriter
{
    public void RecordSubmissionPolicyChange(AcademicAccessContext context, AssignmentSubmissionPolicyChange change)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("ممیزی Policy باید در تراکنش همان تغییر ثبت شود.");
        var before = JsonSerializer.Serialize(new
        {
            assignmentId = change.AssignmentId, assignmentVersionId = change.AssignmentVersionId,
            versionNumber = change.VersionNumber, classId = context.ClassId,
            dueAtUtc = change.OldDueAtUtc, allowLateSubmission = change.OldAllowLateSubmission
        });
        var after = JsonSerializer.Serialize(new
        {
            assignmentId = change.AssignmentId, assignmentVersionId = change.AssignmentVersionId,
            versionNumber = change.VersionNumber, classId = context.ClassId,
            dueAtUtc = change.NewDueAtUtc, allowLateSubmission = change.NewAllowLateSubmission
        });
        var correlation = Activity.Current?.GetTagItem("makan.correlation_id") as string ??
            Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        db.AdminAuditLogs.Add(AdminAuditLog.Create(context.UserId, null, context.SessionId,
            context.IsPersonal ? "personal_class.academic.SubmissionPolicyChanged" : "organization.academic.SubmissionPolicyChanged",
            context.IsPersonal ? "PersonalClassAssignment" : "OrganizationAssignment", change.AssignmentId.ToString(),
            context.OrganizationId, "تغییر مجاز مهلت یا پذیرش پاسخ دیرهنگام تکلیف منتشرشده",
            change.ChangedAtUtc, correlation, AdminAuditResult.Succeeded, before, after));
    }

    public void Record(AcademicAccessContext context, AcademicAuditAction action, Guid resourceId,
        Guid versionId, int revisionNumber, DateTime occurredAtUtc)
    {
        if (!context.IsPersonal) return;
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("ممیزی آموزشی باید در تراکنش عملیات ثبت شود.");
        var target = action switch
        {
            AcademicAuditAction.AssignmentPublished => "PersonalClassAssignment",
            AcademicAuditAction.ExamPublished => "PersonalClassExam",
            AcademicAuditAction.AssignmentResultReleased or AcademicAuditAction.AssignmentResultCorrected => "PersonalClassSubmission",
            _ => "PersonalClassExamAttempt"
        };
        var correlation = Activity.Current?.GetTagItem("makan.correlation_id") as string ??
            Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        // پاسخ، نمره، بازخورد، عنوان و دلیل آزاد کاربر وارد خلاصهٔ Audit نمی‌شوند.
        var summary = JsonSerializer.Serialize(new { personalClassId = context.ClassId, versionId, revisionNumber });
        db.AdminAuditLogs.Add(AdminAuditLog.Create(context.UserId, null, context.SessionId,
            "personal_class.academic." + action, target, resourceId.ToString(), null,
            "ثبت تغییر معتبر چرخهٔ آموزشی کلاس مستقل", occurredAtUtc, correlation,
            AdminAuditResult.Succeeded, null, summary));
    }
}
