using MakanApp.Application.Storage;
using MakanApp.Domain.Reports;

namespace MakanApp.Application.Reports.Exports;

public interface IReportExportService
{
    Task<ReportExportJobResult> CreateAsync(Guid userId, Guid sessionId, CreateReportExportCommand command, CancellationToken cancellationToken);
    Task<ReportExportJobResult> GetStatusAsync(Guid userId, Guid sessionId, Guid jobId, CancellationToken cancellationToken);
    Task<FileDownloadResult> DownloadAsync(Guid userId, Guid sessionId, Guid jobId, CancellationToken cancellationToken);
}

public interface IReportExportDataSource
{
    Task<ReportExportPreparation> PrepareAsync(Guid userId, Guid sessionId, ReportExportType reportType, ReportExportFilters filters, CancellationToken cancellationToken);
    Task EnsureCurrentAccessAsync(Guid userId, Guid sessionId, ReportExportJob job, CancellationToken cancellationToken);
    Task<ReportExportDataPage> ReadPageAsync(ReportExportJob job, int pageNumber, int pageSize, CancellationToken cancellationToken);
}

public interface IReportExportJobStore
{
    Task<ReportExportJob> AddAsync(ReportExportJob job, CancellationToken cancellationToken);
    Task<ReportExportJob?> GetByOperationAsync(Guid userId, Guid operationId, CancellationToken cancellationToken);
    Task<ReportExportJob?> GetAsync(Guid jobId, CancellationToken cancellationToken);
    Task<ReportExportJob?> TryClaimNextAsync(DateTime nowUtc, TimeSpan lease, int maximumAttempts, CancellationToken cancellationToken);
    Task<bool> TrySaveClaimChangesAsync(ReportExportJob job, byte[] expectedClaimRowVersion, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IReportExportQueueSignal
{
    void Signal();
    Task WaitAsync(TimeSpan maximumDelay, CancellationToken cancellationToken);
}

public interface IReportExporter
{
    Task<GeneratedReportFile> GenerateAsync(ReportExportJob job, CancellationToken cancellationToken);
}

public interface IReportExportOutputStore
{
    Task<Guid?> FindReusableOutputAsync(ReportExportJob job, CancellationToken cancellationToken);
    Task<Guid?> SaveAsync(ReportExportJob job, byte[] expectedClaimRowVersion, GeneratedReportFile file, long maximumSizeBytes, CancellationToken cancellationToken);
}
