using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Commerce;

public static class CommercePolicy
{
    // فقط کلیدهای قرارداد قدیمی برای سازگاری باقی می‌مانند؛ Legacy حق تازه ایجاد نمی‌کند.
    public static bool IsLegacyCompatible(EntitlementKey key) => key is EntitlementKey.Academic or EntitlementKey.Assignments
        or EntitlementKey.Exams or EntitlementKey.Messaging or EntitlementKey.Reports or EntitlementKey.Storage;
    public static bool IsLegacyCompatible(QuotaKey key) => key is QuotaKey.ActiveLearners or QuotaKey.ActiveClasses
        or QuotaKey.StorageBytes or QuotaKey.ReportExports;

    public static bool IsLedgerMetered(QuotaKey key) => QuotaKinds.For(key) == QuotaKind.PeriodicConsumption;

    public static QuotaUsageSource GetUsageSource(QuotaKey key) =>
        IsLedgerMetered(key)
            ? QuotaUsageSource.UsageLedger
            : QuotaUsageSource.AuthoritativeModule;
}
