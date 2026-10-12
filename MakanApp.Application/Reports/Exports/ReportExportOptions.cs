namespace MakanApp.Application.Reports.Exports;

public sealed class ReportExportOptions
{
    public const string SectionName = "Reports:Exports";

    public int MaximumRows { get; init; } = 10_000;
    public int PageSize { get; init; } = 100;
    public long MaximumFileSizeBytes { get; init; } = 20 * 1024 * 1024;
    public TimeSpan ClaimLease { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan IdlePollingInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(10);
    public int MaximumAttempts { get; init; } = 3;
}
