using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Commerce;

public static class CommercePolicy
{
    public static bool IsLedgerMetered(QuotaKey key) => key == QuotaKey.ReportExports;

    public static QuotaUsageSource GetUsageSource(QuotaKey key) =>
        IsLedgerMetered(key)
            ? QuotaUsageSource.UsageLedger
            : QuotaUsageSource.AuthoritativeModule;
}
