namespace MakanApp.Api.Observability;

public static class ProductionLoggingSafety
{
    private const string ExceptionLogger = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";

    public static void Configure(IServiceCollection services, IHostEnvironment environment)
    {
        if (!environment.IsProduction() && !environment.IsStaging()) return;
        services.PostConfigure<LoggerFilterOptions>(options =>
        {
            // با تأیید مالک: فقط همین logger در .NET 9 پیش از handler exception خام می‌نویسد.
            // رخداد امن UnexpectedError و metricها باقی‌اند؛ SQL/DP/Health و سایر loggerها حذف نمی‌شوند.
            var providers = options.Rules.Select(rule => rule.ProviderName).Append(null).Distinct().ToArray();
            foreach (var provider in providers)
                options.Rules.Add(new LoggerFilterRule(provider, ExceptionLogger, LogLevel.None, null));
        });
    }
}
