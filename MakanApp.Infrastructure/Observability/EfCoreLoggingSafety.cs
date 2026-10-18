using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Observability;

public sealed class EfCoreLoggingSafety(ILogger<EfCoreLoggingSafety> logger)
{
    public static IReadOnlyList<string> Categories { get; } = Array.AsReadOnly(new[]
    {
        DbLoggerCategory.Database.Command.Name,
        DbLoggerCategory.Database.Connection.Name,
        DbLoggerCategory.Database.Transaction.Name,
        DbLoggerCategory.Query.Name,
        DbLoggerCategory.Update.Name
    });

    public void Configure(DbContextOptionsBuilder options, string environmentName)
    {
        if (!environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase) &&
            !environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase)) return;

        // مسیر EventData از فیلتر ILogger مستقل است؛ ConfigureWarnings.Ignore جایگزین این مسیر نیست.
        options.LogTo((eventId, level) => level >= LogLevel.Warning && CategoryFor(eventId) is not null, WriteSafeEvent);
    }

    private void WriteSafeEvent(EventData eventData)
    {
        var category = CategoryFor(eventData.EventId);
        if (category is null) return;
        var hasException = eventData is IErrorEventData;
        var level = hasException && eventData.LogLevel < LogLevel.Error ? LogLevel.Error : eventData.LogLevel;
        var originalId = eventData.EventId.Id is >= 0 and <= 99999 ? eventData.EventId.Id : 0;
        var activity = Activity.Current;
        var correlation = activity?.GetTagItem("makan.correlation_id") as string;
        if (correlation is not { Length: > 0 and <= 64 } ||
            !correlation.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
            correlation = activity?.TraceId.ToString();

        // نه EventData، نه Exception و نه formatter اصلی ارزیابی یا به provider منتقل نمی‌شوند.
        logger.Log(level, new EventId(1003, "SafeEfCoreDiagnostic"),
            "رخداد امن EF Core. Code={ErrorCode}; Category={Category}; DatabaseEventId={DatabaseEventId}; HasException={HasException}; CorrelationId={CorrelationId}; TraceId={TraceId}",
            level >= LogLevel.Error ? "EF_CORE_OPERATION_FAILED" : "EF_CORE_DIAGNOSTIC",
            category, originalId, hasException, correlation, activity?.TraceId.ToString());
    }

    private static string? CategoryFor(EventId eventId)
    {
        var name = eventId.Name;
        return name is null ? null : Categories.FirstOrDefault(category => name.StartsWith(category + ".", StringComparison.Ordinal));
    }
}
