using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;

namespace MakanApp.Application.Reports.Exports;

public sealed record CreateReportExportCommand(
    ReportExportType ReportType,
    ReportExportFormat Format,
    ReportExportFilters? Filters,
    Guid? ClientOperationId = null);

public sealed record ReportExportFilters(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Guid? ClassId = null,
    string? Sort = null,
    Guid? CourseId = null,
    Guid? AcademicPeriodId = null,
    EnrollmentStatus? EnrollmentStatus = null,
    ReportExportAssessmentKind? AssessmentKind = null);

public sealed record ReportExportJobResult(
    Guid JobId,
    ReportExportType ReportType,
    ReportExportFormat Format,
    ReportExportJobStatus Status,
    int ExpectedRowCount,
    DateTime DataAsOfUtc,
    DateTime RequestedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? FailedAtUtc,
    bool DownloadAvailable,
    string? FailureCode);

public sealed record ReportExportPreparation(
    Guid OrganizationId,
    Guid RequestedByUserId,
    Guid RequestedBySessionId,
    Guid MembershipId,
    OrganizationRole Audience,
    Guid? SubjectOrganizationPersonId,
    DateTime DataAsOfUtc,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? ClassId,
    Guid? CourseId,
    Guid? AcademicPeriodId,
    EnrollmentStatus? EnrollmentStatus,
    ReportExportAssessmentKind? AssessmentKind,
    string Sort,
    int ExpectedRowCount);

public sealed record ReportExportCell(string? Value, bool IsUserControlledText = false);

public sealed record ReportExportDataPage(
    int TotalCount,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<ReportExportCell>> Rows);

public sealed class GeneratedReportFile(
    Stream content,
    string contentType,
    string downloadFileName) : IAsyncDisposable
{
    public Stream Content { get; } = content;
    public string ContentType { get; } = contentType;
    public string DownloadFileName { get; } = downloadFileName;

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
