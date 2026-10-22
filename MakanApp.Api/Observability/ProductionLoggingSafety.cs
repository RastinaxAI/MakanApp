using MakanApp.Infrastructure.Observability;

namespace MakanApp.Api.Observability;

public static class ProductionLoggingSafety
{
    private const string ExceptionLogger = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";

    public static void Configure(IServiceCollection services, IHostEnvironment environment)
    {
        if (!environment.IsProduction() && !environment.IsStaging() && !environment.IsEnvironment("Test")) return;
        services.PostConfigure<LoggerFilterOptions>(options =>
        {
            // فقط مسیرهای خام مشخص با رخداد امن جایگزین می‌شوند؛ خطا و سطح آن حذف نمی‌شوند.
            // EF از EventData مستقل از این filter استفاده می‌کند؛ سایر categoryها دست‌نخورده‌اند.
            var categories = EfCoreLoggingSafety.Categories.Prepend(ExceptionLogger).ToArray();
            var providers = options.Rules.Select(rule => rule.ProviderName).Append(null).Distinct().ToArray();
            // قانون دقیق‌ترِ provider یا زیرشاخه نباید حفاظت همین categoryها را دور بزند.
            for (var index = options.Rules.Count - 1; index >= 0; index--)
            {
                var category = options.Rules[index].CategoryName;
                if (category is not null && categories.Any(prefix =>
                    category.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                    category.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)))
                    options.Rules.RemoveAt(index);
            }

            foreach (var provider in providers)
                foreach (var category in categories)
                    options.Rules.Add(new LoggerFilterRule(provider, category, LogLevel.None, null));
        });
    }
}
