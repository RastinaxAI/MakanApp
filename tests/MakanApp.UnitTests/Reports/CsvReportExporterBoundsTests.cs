using MakanApp.Application.Reports.Exports;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;
using MakanApp.Infrastructure.Reports.Exports;
using Xunit;

namespace MakanApp.UnitTests.Reports;

public sealed class CsvReportExporterBoundsTests
{
    [Fact]
    public async Task GenerationReadsOnlyBoundedPagesAndStopsAtTheDeclaredRowCount()
    {
        var source = new ControlledDataSource((page, _, _) => Task.FromResult(page switch
        {
            1 => new ReportExportDataPage(3, ["value"], Rows(2)),
            2 => new ReportExportDataPage(3, ["value"], Rows(1)),
            _ => throw new InvalidOperationException("خواندن صفحهٔ اضافی مجاز نیست.")
        }));
        var exporter = new CsvReportExporter(source, new ReportExportOptions { PageSize = 2, MaximumRows = 3 });

        await using var generated = await exporter.GenerateAsync(Job(), CancellationToken.None);
        using var reader = new StreamReader(generated.Content);
        var csv = await reader.ReadToEndAsync();

        Assert.Equal([(1, 2), (2, 2)], source.Reads);
        Assert.Equal(4, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(1, 3)]
    public async Task ReportedOrActualRowsBeyondMaximumAreRejectedBeforeAnotherPage(int totalCount, int rowCount)
    {
        var source = new ControlledDataSource((_, _, _) =>
            Task.FromResult(new ReportExportDataPage(totalCount, ["value"], Rows(rowCount))));
        var exporter = new CsvReportExporter(source, new ReportExportOptions { PageSize = 2, MaximumRows = 2 });

        var exception = await Assert.ThrowsAsync<ReportExportException>(() => exporter.GenerateAsync(Job(), CancellationToken.None));

        Assert.Equal(ReportExportErrorCodes.RowLimitExceeded, exception.Code);
        Assert.Single(source.Reads);
    }

    [Fact]
    public async Task CancellationAfterPageReadStopsTheRowWriteWithoutRequestingMoreData()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new ControlledDataSource((_, _, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return Task.FromResult(new ReportExportDataPage(2, ["value"], Rows(1)));
        });
        var exporter = new CsvReportExporter(source, new ReportExportOptions { PageSize = 1, MaximumRows = 2 });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => exporter.GenerateAsync(Job(), cancellation.Token));

        Assert.Single(source.Reads);
    }

    private static IReadOnlyList<IReadOnlyList<ReportExportCell>> Rows(int count) =>
        Enumerable.Range(0, count).Select(index => (IReadOnlyList<ReportExportCell>)[new($"row-{index}")]).ToArray();

    private static ReportExportJob Job()
    {
        var now = DateTime.UtcNow;
        return ReportExportJob.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            OrganizationRole.Manager, null, ReportExportType.ManagerAttendance, ReportExportFormat.Csv,
            now, null, null, null, null, null, null, null, "classtitleasc", 0, now);
    }

    private sealed class ControlledDataSource(Func<int, int, CancellationToken, Task<ReportExportDataPage>> read) : IReportExportDataSource
    {
        public List<(int Page, int PageSize)> Reads { get; } = [];
        public Task<ReportExportDataPage> ReadPageAsync(ReportExportJob job, int pageNumber, int pageSize, CancellationToken cancellationToken)
        {
            Reads.Add((pageNumber, pageSize));
            return read(pageNumber, pageSize, cancellationToken);
        }
        public Task EnsureCurrentAccessAsync(Guid userId, Guid sessionId, ReportExportJob job, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ReportExportPreparation> PrepareAsync(Guid userId, Guid sessionId, ReportExportType reportType, ReportExportFilters filters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
