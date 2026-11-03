namespace MakanApp.Application.Assessment;

public enum AcademicAuditAction
{
    AssignmentPublished, AssignmentResultReleased, AssignmentResultCorrected,
    ExamPublished, ExamResultReleased, ExamResultCorrected
}

public interface IAcademicAuditWriter
{
    // فقط metadata لازم برای ممیزی در Unit of Work فعلی ثبت می‌شود؛ این متد Commit جداگانه ندارد.
    void Record(AcademicAccessContext context, AcademicAuditAction action, Guid resourceId,
        Guid versionId, int revisionNumber, DateTime occurredAtUtc);
}
