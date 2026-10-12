using System.Text;
using MakanApp.Application.Reports.Exports;
using MakanApp.Domain.Reports;

namespace MakanApp.Infrastructure.Reports.Exports;

public sealed class CsvReportExporter(
    IReportExportDataSource dataSource,
    ReportExportOptions options) : IReportExporter
{
    private const string ContentType = "text/csv; charset=utf-8";

    public async Task<GeneratedReportFile> GenerateAsync(
        ReportExportJob job,
        CancellationToken cancellationToken)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "MakanApp-ReportExports");
        Directory.CreateDirectory(temporaryDirectory);
        var temporaryPath = Path.Combine(temporaryDirectory, $"{Guid.NewGuid():N}.csv");
        var stream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        try
        {
            await using (var writer = new StreamWriter(
                stream,
                new UTF8Encoding(true),
                81_920,
                leaveOpen: true))
            {
                writer.NewLine = "\r\n";
                var pageNumber = 1;
                var writtenRows = 0;
                int? totalCount = null;
                IReadOnlyList<string>? columns = null;
                while (!totalCount.HasValue || writtenRows < totalCount.Value)
                {
                    var page = await dataSource.ReadPageAsync(
                        job,
                        pageNumber,
                        options.PageSize,
                        cancellationToken);
                    totalCount ??= page.TotalCount;
                    if (page.TotalCount > options.MaximumRows ||
                        writtenRows + page.Rows.Count > options.MaximumRows)
                    {
                        throw new ReportExportException(
                            ReportExportErrorCodes.RowLimitExceeded,
                            $"تعداد سطرهای گزارش از سقف {options.MaximumRows} بیشتر است.");
                    }

                    if (columns is null)
                    {
                        columns = page.Columns;
                        await writer.WriteLineAsync(string.Join(',', columns.Select(CsvCellEncoder.EncodeHeader)));
                    }
                    else if (!columns.SequenceEqual(page.Columns, StringComparer.Ordinal))
                    {
                        throw new ReportExportException(
                            ReportExportErrorCodes.GenerationFailed,
                            "ساختار صفحه‌های گزارش یکسان نیست.");
                    }

                    foreach (var row in page.Rows)
                    {
                        if (row.Count != columns.Count)
                        {
                            throw new ReportExportException(
                                ReportExportErrorCodes.GenerationFailed,
                                "تعداد ستون‌های گزارش معتبر نیست.");
                        }

                        await writer.WriteLineAsync(string.Join(',', row.Select(CsvCellEncoder.Encode)));
                    }

                    writtenRows += page.Rows.Count;
                    if (page.Rows.Count == 0)
                    {
                        break;
                    }

                    pageNumber++;
                }

                await writer.FlushAsync(cancellationToken);
            }

            stream.Position = 0;
            var fileName = $"{job.ReportType.ToString().ToLowerInvariant()}-{job.RequestedAtUtc:yyyyMMdd-HHmmss}-{job.Id:N}.csv";
            return new GeneratedReportFile(stream, ContentType, fileName);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }
}

public static class CsvCellEncoder
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];

    public static string EncodeHeader(string value) => EncodeCore(value, false);

    public static string Encode(ReportExportCell cell) =>
        EncodeCore(cell.Value ?? string.Empty, cell.IsUserControlledText);

    private static string EncodeCore(string value, bool protectFormula)
    {
        if (protectFormula)
        {
            var firstVisible = value.AsSpan().TrimStart();
            if (!firstVisible.IsEmpty && FormulaPrefixes.Contains(firstVisible[0]))
            {
                value = "'" + value;
            }
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
