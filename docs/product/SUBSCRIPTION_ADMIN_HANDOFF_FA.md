# قرارداد Admin بنیاد اشتراک

پایهٔ مسیرهای مرکزی `/api/admin/v1/commerce` است. نشست فعال و permission موجود CommerceRead یا CommerceManage لازم است؛ Manager آموزشگاه دسترسی Global Plan Management ندارد.

| Method | Route زیر پایه | ورودی/کارکرد |
|---|---|---|
| GET / POST | plans | فهرست یا ساخت Code/DisplayName/Reason/Audience |
| GET | plans/{planId} | جزئیات و نسخه‌های موجود |
| POST | plans/{planId}/versions | Draft؛ PriceAmount بر حسب IRR، ExplicitlyFree، Entitlements، Quotas و شرایط ماهانه |
| PUT | plan-versions/{versionId}/draft | PriceToman، ExplicitlyFree، Calendar، SaleTermsReference، هشدارها، Entitlements، Quotas، ExpectedVersion و Reason |
| GET | plan-versions/{versionId}/validation | MissingFields، CanPublish و Version |
| POST | plan-versions/{versionId}/publish | ExpectedVersion و Reason؛ نسخهٔ کامل منتشر می‌شود |
| PUT | plans/{planId}/availability | CatalogVisible، NewPurchaseAvailable، RenewalAvailable، ExpectedVersion و Reason |
| GET | organizations/{ownerId}/subscription | مشاهدهٔ قرارداد و وضعیت مهاجرت سازمان |
| GET | users/{ownerId}/subscription | مشاهدهٔ قرارداد Individual |
| POST | organizations/{ownerId}/commercial-contracts | نخستین قرارداد معتبر و فعال‌سازی کنترل سازمان |
| POST | users/{ownerId}/commercial-contracts | قرارداد معتبر Individual |
| GET | history/{targetId} | actor، زمان، reason، before/after برای هدف مشخص |

Enumهای JSON با نام انگلیسی ارسال می‌شوند. Audience فقط Individual یا Organization است. Published PlanVersion از مسیر Draft ویرایش نمی‌شود؛ قیمت، سقف، حق و دورهٔ جدید نیازمند نسخهٔ جدیدند. Hidden کردن Plan، قرارداد فعال را لغو نمی‌کند. نمایش، خرید تازه و تمدید مستقل‌اند.

Unset با صفر و Unlimited تفاوت دارد. در هر QuotaItem حضور Limit اجباری است؛ صفر سقف صفر و null نامحدود صریح است. حذف Limit خطای ۴۰۰ می‌دهد. قیمت صفر تنها با ExplicitlyFree پذیرفته می‌شود. Calendar و SaleTermsReference باید صریح باشند و سهمیه‌های وابسته به قابلیت فعال وجود داشته باشند. نسخهٔ ناقص قابل انتشار یا فروش نیست.

RegisterCommercialContractCommand شامل PlanVersionId، BillingAnchorUtc، BillingPeriodIndex، ExpectedCurrentVersion در صورت سابقهٔ فعال و Reason است. دوره باید هنگام ثبت معتبر باشد؛ PlanVersion منتشر، کامل و هم‌Audience مالک باشد. این endpoint ابزار بازنویسی آزاد قرارداد پرداخت‌شده یا Refund نیست.

برای Legacy، پایان، قیمت، نسخه یا سقف حدس نزنید. AwaitingValidContract به معنی قرارداد Unlimited نیست. سابقه با LegacySubscriptionId و ETag گزارش می‌شود؛ رفتار قدیمی تا ثبت معتبر حفظ می‌شود، اما قابلیت/سهمیهٔ جدید نمی‌گیرد. ثبت موفق، Completed و CommercialEnforcementFromUtc واقعی می‌دهد. دورهٔ صریح می‌تواند قدیمی‌تر باشد، ولی کنترل تجاری عقب برده نمی‌شود؛ تاریخ و مصرف Legacy به قرارداد تازه backfill نمی‌شوند.

قفل مشترک مالک و rowversion از ثبت هم‌زمان متعارض جلوگیری می‌کنند. رکورد و مصرف قبلی حذف نمی‌شوند؛ گذار در AdminAudit ثبت می‌شود. خرید و قرارداد، Membership/RoleAssignment/Enrollment ایجاد نمی‌کنند.

در این Release نام/قیمت/سقف پولی و سیاست Refund/Discount نهایی نشده‌اند. Paid Plan حدسی ساخته یا منتشر نشود. Payment provider واقعی unavailable و AI خاموش است. Contract آزمایشی فقط در دیتابیس ایزولهٔ smoke استفاده می‌شود.
