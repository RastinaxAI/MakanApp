using System.Diagnostics;
using System.Text.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfAcademicAuditWriter(MakanDbContext db) : IAcademicAuditWriter
{
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
