using MakanApp.Application.Storage;
using MakanApp.Domain.Reports;

namespace MakanApp.Application.Reports.Exports;

public sealed class ReportExportService(
    IReportExportJobStore jobStore,
    IReportExportDataSource dataSource,
    IReportExportQueueSignal queueSignal,
    IStorageService storageService,
    ReportExportOptions options,
    TimeProvider timeProvider) : IReportExportService
{
    public async Task<ReportExportJobResult> CreateAsync(
        Guid userId,
        Guid sessionId,
        CreateReportExportCommand command,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(command.ReportType))
        {
            throw Error(ReportExportErrorCodes.InvalidRequest, "نوع گزارش export معتبر نیست.");
        }

        if (command.Format != ReportExportFormat.Csv)
        {
            throw Error(ReportExportErrorCodes.UnsupportedFormat, "در این مرحله فقط خروجی CSV پشتیبانی می‌شود.");
        }

        var preparation = await dataSource.PrepareAsync(
            userId,
            sessionId,
            command.ReportType,
            command.Filters ?? new ReportExportFilters(),
            cancellationToken);
        if (preparation.ExpectedRowCount > options.MaximumRows)
        {
            throw Error(
                ReportExportErrorCodes.RowLimitExceeded,
                $"تعداد سطرهای گزارش از سقف {options.MaximumRows} بیشتر است؛ فیلترها را محدودتر کنید.");
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var job = ReportExportJob.Create(
            preparation.OrganizationId,
            preparation.RequestedByUserId,
            preparation.RequestedBySessionId,
            preparation.MembershipId,
            preparation.Audience,
            preparation.SubjectOrganizationPersonId,
            command.ReportType,
            command.Format,
            preparation.DataAsOfUtc,
            preparation.FromUtc,
            preparation.ToUtc,
            preparation.ClassId,
            preparation.CourseId,
            preparation.AcademicPeriodId,
            preparation.EnrollmentStatus,
            preparation.AssessmentKind,
            preparation.Sort,
            preparation.ExpectedRowCount,
            nowUtc);
        await jobStore.AddAsync(job, cancellationToken);
        queueSignal.Signal();
        return ToResult(job);
    }

    public async Task<ReportExportJobResult> GetStatusAsync(
        Guid userId,
        Guid sessionId,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await GetOwnedAsync(userId, jobId, cancellationToken);
        await dataSource.EnsureCurrentAccessAsync(userId, sessionId, job, cancellationToken);
        return ToResult(job);
    }

    public async Task<FileDownloadResult> DownloadAsync(
        Guid userId,
        Guid sessionId,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await GetOwnedAsync(userId, jobId, cancellationToken);
        await dataSource.EnsureCurrentAccessAsync(userId, sessionId, job, cancellationToken);
        if (job.Status != ReportExportJobStatus.Completed || !job.OutputFileAssetId.HasValue)
        {
            throw Error(ReportExportErrorCodes.NotReady, "خروجی گزارش هنوز برای دریافت آماده نیست.");
        }

        return await storageService.DownloadFileAsync(
            userId,
            sessionId,
            job.OutputFileAssetId.Value,
            cancellationToken);
    }

    private async Task<ReportExportJob> GetOwnedAsync(Guid userId, Guid jobId, CancellationToken cancellationToken)
    {
        var job = await jobStore.GetAsync(jobId, cancellationToken);
        if (job is null || job.RequestedByUserId != userId)
        {
            throw Error(ReportExportErrorCodes.NotFound, "درخواست export پیدا نشد.");
        }

        return job;
    }

    private static ReportExportJobResult ToResult(ReportExportJob job) =>
        new(
            job.Id,
            job.ReportType,
            job.Format,
            job.Status,
            job.ExpectedRowCount,
            job.DataAsOfUtc,
            job.RequestedAtUtc,
            job.StartedAtUtc,
            job.CompletedAtUtc,
            job.FailedAtUtc,
            job.Status == ReportExportJobStatus.Completed && job.OutputFileAssetId.HasValue,
            job.Status == ReportExportJobStatus.Failed ? job.FailureCode : null);

    private static ReportExportException Error(string code, string message) => new(code, message);
}
