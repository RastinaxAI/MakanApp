using MakanApp.Domain.Commerce;

namespace MakanApp.Infrastructure.Persistence.Configurations.Commerce;

internal static class CurrencyPersistence
{
    public static string ToDatabase(CurrencyCode currency) => currency switch
    {
        CurrencyCode.Irr => "IRR",
        _ => throw new InvalidOperationException("واحد پول برای persistence پشتیبانی نمی‌شود.")
    };

    public static CurrencyCode FromDatabase(string value) => value switch
    {
        "IRR" => CurrencyCode.Irr,
        _ => throw new InvalidOperationException("واحد پول ذخیره‌شده پشتیبانی نمی‌شود.")
    };
}
