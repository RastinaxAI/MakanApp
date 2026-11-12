using MakanApp.Application.Commerce;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Notification;

namespace MakanApp.Application.Assessment;

// وجود engine و grant به معنی فعال‌بودن محصول پیشنهادی نیست؛ تنظیمات فقط در composition root مقدار می‌گیرند.
public sealed class PersonalClassAcademicFeatures
{
    public const string SectionName = "Features:PersonalClass";
    public bool AssignmentsEnabled { get; set; }
    public bool ExamsEnabled { get; set; }
    public bool AnyEnabled => AssignmentsEnabled || ExamsEnabled;

    public bool AllowsNotification(Guid? organizationId, NotificationResourceType resourceType) =>
        organizationId.HasValue || resourceType switch
        {
            NotificationResourceType.Assignment or NotificationResourceType.SubmissionAttempt => AssignmentsEnabled,
            NotificationResourceType.Exam or NotificationResourceType.ExamAttempt => ExamsEnabled,
            _ => true
        };

    public void EnsureAssignments() { if (!AssignmentsEnabled) throw Unavailable(); }
    public void EnsureExams() { if (!ExamsEnabled) throw Unavailable(); }

    public void EnsureResource(AcademicResourceKind kind, AcademicOperation operation)
    {
        if (kind is AcademicResourceKind.Assignment or AcademicResourceKind.SubmissionAttempt || operation == AcademicOperation.AuthorAssignment)
            EnsureAssignments();
        else if (kind is AcademicResourceKind.Exam or AcademicResourceKind.ExamAttempt || operation == AcademicOperation.AuthorExam)
            EnsureExams();
        else if (!AnyEnabled) throw Unavailable();
    }

    public PersonalClassAcademicCapabilities Effective(PersonalClassAcademicCapabilities capabilities)
    {
        if (!AssignmentsEnabled) capabilities &= ~PersonalClassAcademicCapabilities.AuthorAssignments;
        if (!ExamsEnabled) capabilities &= ~PersonalClassAcademicCapabilities.AuthorExams;
        if (!AnyEnabled) capabilities &= ~PersonalClassAcademicCapabilities.Grade;
        return capabilities;
    }

    public void EnsureCapabilities(PersonalClassAcademicCapabilities capabilities)
    {
        if (Effective(capabilities) != capabilities) throw Unavailable();
    }

    private static CommerceException Unavailable() => new(CommerceErrorCodes.FeatureNotEntitled,
        "قابلیت آموزشی پیشنهادی کلاس مستقل در این نسخه فعال نیست.");
}
