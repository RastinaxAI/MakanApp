namespace MakanApp.Application.Reports.Detailed;

public static class DetailedReportErrorCodes
{
    public const string PaginationInvalid = "REPORT_PAGINATION_INVALID";
    public const string DateRangeInvalid = "REPORT_DATE_RANGE_INVALID";
    public const string SortInvalid = "REPORT_SORT_INVALID";
    public const string ResourceNotFound = "REPORT_RESOURCE_NOT_FOUND";
}

public sealed class DetailedReportException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
