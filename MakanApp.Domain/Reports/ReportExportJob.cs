using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;

namespace MakanApp.Domain.Reports;

public sealed partial class ReportExportJob
{
    private ReportExportJob()
    {
    }

    private ReportExportJob(
        Guid id,
        Guid organizationId,
        Guid requestedByUserId,
        Guid requestedBySessionId,
        Guid membershipId,
        OrganizationRole audience,
        Guid? subjectOrganizationPersonId,
        ReportExportType reportType,
        ReportExportFormat format,
        DateTime dataAsOfUtc,
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? classId,
        Guid? courseId,
        Guid? academicPeriodId,
        EnrollmentStatus? enrollmentStatus,
        ReportExportAssessmentKind? assessmentKind,
        string sort,
        int expectedRowCount,
        DateTime requestedAtUtc)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty ||
            requestedByUserId == Guid.Empty || requestedBySessionId == Guid.Empty ||
            membershipId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های job و محدوده دسترسی الزامی هستند.");
        }

        if (string.IsNullOrWhiteSpace(sort) || expectedRowCount < 0)
        {
            throw new ArgumentException("فیلتر معتبر و تعداد سطر غیرمنفی الزامی است.");
        }

        EnsureUtc(dataAsOfUtc, nameof(dataAsOfUtc));
        EnsureUtc(requestedAtUtc, nameof(requestedAtUtc));
        if (fromUtc.HasValue)
        {
            EnsureUtc(fromUtc.Value, nameof(fromUtc));
        }

        if (toUtc.HasValue)
        {
            EnsureUtc(toUtc.Value, nameof(toUtc));
        }

        Id = id;
        OrganizationId = organizationId;
        RequestedByUserId = requestedByUserId;
        RequestedBySessionId = requestedBySessionId;
        MembershipId = membershipId;
        Audience = audience;
        SubjectOrganizationPersonId = subjectOrganizationPersonId;
        ReportType = reportType;
        Format = format;
        DataAsOfUtc = dataAsOfUtc;
        FromUtc = fromUtc;
        ToUtc = toUtc;
        ClassId = classId;
        CourseId = courseId;
        AcademicPeriodId = academicPeriodId;
        EnrollmentStatus = enrollmentStatus;
        AssessmentKind = assessmentKind;
        Sort = sort;
        ExpectedRowCount = expectedRowCount;
        RequestedAtUtc = requestedAtUtc;
        Status = ReportExportJobStatus.Queued;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid RequestedBySessionId { get; private set; }
    public Guid MembershipId { get; private set; }
    public OrganizationRole Audience { get; private set; }
    public Guid? SubjectOrganizationPersonId { get; private set; }
    public ReportExportType ReportType { get; private set; }
    public ReportExportFormat Format { get; private set; }
    public DateTime DataAsOfUtc { get; private set; }
    public DateTime? FromUtc { get; private set; }
    public DateTime? ToUtc { get; private set; }
    public Guid? ClassId { get; private set; }
    public Guid? CourseId { get; private set; }
    public Guid? AcademicPeriodId { get; private set; }
    public EnrollmentStatus? EnrollmentStatus { get; private set; }
    public ReportExportAssessmentKind? AssessmentKind { get; private set; }
    public string Sort { get; private set; } = string.Empty;
    public int ExpectedRowCount { get; private set; }
    public ReportExportJobStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public Guid? OutputFileAssetId { get; private set; }
    public string? FailureCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ReportExportJob Create(
        Guid organizationId,
        Guid requestedByUserId,
        Guid requestedBySessionId,
        Guid membershipId,
        OrganizationRole audience,
        Guid? subjectOrganizationPersonId,
        ReportExportType reportType,
        ReportExportFormat format,
        DateTime dataAsOfUtc,
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? classId,
        Guid? courseId,
        Guid? academicPeriodId,
        EnrollmentStatus? enrollmentStatus,
        ReportExportAssessmentKind? assessmentKind,
        string sort,
        int expectedRowCount,
        DateTime requestedAtUtc) =>
        new(
            Guid.NewGuid(),
            organizationId,
            requestedByUserId,
            requestedBySessionId,
            membershipId,
            audience,
            subjectOrganizationPersonId,
            reportType,
            format,
            dataAsOfUtc,
            fromUtc,
            toUtc,
            classId,
            courseId,
            academicPeriodId,
            enrollmentStatus,
            assessmentKind,
            sort,
            expectedRowCount,
            requestedAtUtc);

    public void Claim(DateTime startedAtUtc, DateTime leaseExpiresAtUtc)
    {
        EnsureUtc(startedAtUtc, nameof(startedAtUtc));
        EnsureUtc(leaseExpiresAtUtc, nameof(leaseExpiresAtUtc));
        if (Status == ReportExportJobStatus.Processing &&
            (!LeaseExpiresAtUtc.HasValue || LeaseExpiresAtUtc > startedAtUtc))
        {
            throw new InvalidOperationException("job در حال پردازش lease معتبر دارد.");
        }

        if (Status is ReportExportJobStatus.Completed or ReportExportJobStatus.Failed ||
            leaseExpiresAtUtc <= startedAtUtc)
        {
            throw new InvalidOperationException("انتقال job به Processing مجاز نیست.");
        }

        Status = ReportExportJobStatus.Processing;
        StartedAtUtc ??= startedAtUtc;
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
        NextAttemptAtUtc = null;
        AttemptCount++;
        FailureCode = null;
    }

    public void Complete(Guid outputFileAssetId, DateTime completedAtUtc)
    {
        EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        if (Status != ReportExportJobStatus.Processing || outputFileAssetId == Guid.Empty)
        {
            throw new InvalidOperationException("فقط job در حال پردازش با فایل معتبر کامل می‌شود.");
        }

        Status = ReportExportJobStatus.Completed;
        OutputFileAssetId = outputFileAssetId;
        CompletedAtUtc = completedAtUtc;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = null;
        FailureCode = null;
    }

    public void ScheduleRetry(DateTime nextAttemptAtUtc)
    {
        EnsureUtc(nextAttemptAtUtc, nameof(nextAttemptAtUtc));
        if (Status != ReportExportJobStatus.Processing)
        {
            throw new InvalidOperationException("فقط job در حال پردازش قابل retry است.");
        }

        Status = ReportExportJobStatus.Queued;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }

    public void Fail(string failureCode, DateTime failedAtUtc)
    {
        EnsureUtc(failedAtUtc, nameof(failedAtUtc));
        if (Status is ReportExportJobStatus.Completed or ReportExportJobStatus.Failed ||
            string.IsNullOrWhiteSpace(failureCode))
        {
            throw new InvalidOperationException("انتقال job به Failed مجاز نیست.");
        }

        Status = ReportExportJobStatus.Failed;
        FailureCode = failureCode;
        FailedAtUtc = failedAtUtc;
        LeaseExpiresAtUtc = null;
        NextAttemptAtUtc = null;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان job باید UTC باشد.", parameterName);
        }
    }
}
