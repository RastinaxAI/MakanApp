using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed partial class EfCommercialStore
{
    private async Task<long> FileCapacityAsync(SubscriptionOwner owner, bool assignmentOnly, CancellationToken ct)
    {
        var files = db.FileAssets.AsNoTracking().Where(f => f.Status == FileAssetStatus.Ready);
        if (owner.Type == SubscriptionOwnerType.Organization)
            files = files.Where(f => f.OrganizationId == owner.Id);
        else
        {
            // مالک فایل کلاس، میزبان مالی است؛ بارگذار یا مدیر فعلی جای مالک قرارداد را نمی‌گیرد.
            // فایل منبع شخصی فقط پس از اتصال معتبر، از سهمیهٔ همان مالک منبع شمرده می‌شود.
            files = files.Where(f => f.OrganizationId == null &&
                (f.PersonalClassId.HasValue && db.PersonalClasses.Any(c => c.Id == f.PersonalClassId && c.HostUserId == owner.Id)
                || f.PersonalClassId == null && db.EducationalSourceVersions.Any(v => v.FileAssetId == f.Id &&
                    db.EducationalSources.Any(s => s.Id == v.SourceId && s.Scope == EducationalSourceScope.Personal && s.OwnerUserId == owner.Id))));
        }

        if (assignmentOnly)
            files = files.Where(f => db.AssignmentVersionAttachments.Any(a => a.FileAssetId == f.Id &&
                    (f.OrganizationId.HasValue ? a.OrganizationId == f.OrganizationId : f.PersonalClassId.HasValue && a.PersonalClassId == f.PersonalClassId))
                || db.SubmissionAttachments.Any(a => a.FileAssetId == f.Id &&
                    (f.OrganizationId.HasValue ? a.OrganizationId == f.OrganizationId : f.PersonalClassId.HasValue && a.PersonalClassId == f.PersonalClassId)));

        // EXISTS به‌جای جمع اتصال‌ها: یک فایل در چند نسخه/پاسخ فقط یک‌بار ظرفیت اشغال می‌کند.
        // Ready شامل Retained هم هست؛ Archive یا پایان عضویت، حذف فیزیکی فایل تاریخی نیست.
        return await files.SumAsync(f => (long?)f.SizeBytes, ct) ?? 0;
    }
}
