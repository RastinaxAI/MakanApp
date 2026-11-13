using MakanApp.Domain.Identity;

namespace MakanApp.Domain.Organization;

public static class SchoolInitialManagerPolicy
{
    // درخواست حذفِ فاقد اجرای مصوب، وضعیت حذف نهایی حساب محسوب نمی‌شود.
    public static bool IsEligible(User? user) => user is { AccountStatus: UserAccountStatus.Active, IsProfileComplete: true };
}
