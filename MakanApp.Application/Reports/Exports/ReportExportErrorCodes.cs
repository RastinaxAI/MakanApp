namespace MakanApp.Application.Reports.Exports;

public static class ReportExportErrorCodes
{
    public const string InvalidRequest = "REPORT_EXPORT_INVALID";
    public const string UnsupportedFormat = "REPORT_EXPORT_FORMAT_UNSUPPORTED";
    public const string RowLimitExceeded = "REPORT_EXPORT_ROW_LIMIT_EXCEEDED";
    public const string NotFound = "REPORT_EXPORT_NOT_FOUND";
    public const string NotReady = "REPORT_EXPORT_NOT_READY";
    public const string DataNoLongerAuthorized = "REPORT_DATA_NO_LONGER_AUTHORIZED";
    public const string GenerationFailed = "REPORT_GENERATION_FAILED";
}

public sealed class ReportExportException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
