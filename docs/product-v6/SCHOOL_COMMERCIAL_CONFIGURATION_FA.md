# تنظیم تجاری تأسیس آموزشگاه

تاریخ: `2026-11-13`. مرجع، API و مدل واقعی Backend است. این راهنما قیمت، دوره یا Quota تجاری پیشنهاد نمی‌دهد و هیچ Seed در دیتابیس واقعی اجرا نشده است. نبود دادهٔ مصوب Production برابر `BLOCKED_CONFIGURATION` است؛ آزمون ساختگی جای آن را نمی‌گیرد.

## مرز خدمت و دسترسی

دو نگاشت مستقل برای `LegalOrganization` و `IndividualTeacher` ثبت می‌شوند. هر دو به خدمت ایجاد **Organization** مربوط‌اند؛ بنابراین Plan باید `audience=Organization` داشته باشد، حتی وقتی متقاضی مدرس حقیقی است. خریدار، حساب متقاضی پرونده و ذی‌نفع نهایی، سازمان ساخته‌شده است. این خرید اشتراک شخصیِ متقاضی نیست.

مدیریت Catalog نیازمند نشست معتبر و `platform.commerce.manage` است؛ خواندن Catalog به `platform.commerce.read` نیاز دارد. تنظیم نگاشت آموزشگاه علاوه بر CommerceManage به `platform.school_applications.read` و `platform.school_applications.review` نیاز دارد. نقش Manager سازمانی مجوز Platform ایجاد نمی‌کند. MFA/Step-Up موجود در مسیرهای حساس حفظ می‌شود.

## ایجاد دو Plan و نسخهٔ مصوب

این مراحل را جداگانه برای هر دو نوع خدمت، با دادهٔ مصوب همان محیط انجام دهید:

1. `POST /api/admin/v1/commerce/plans` با `CreatePlatformPlanCommand`: `code` یکتا، `displayName`، `reason` و `audience=Organization`. شناسه و `version` واقعی پاسخ را نگه دارید.
2. `POST /api/admin/v1/commerce/plans/{planId}/versions` با `CreatePlatformPlanVersionCommand`. فیلدهای واقعی: `priceAmount` بر حسب `IRR`، `currency=Irr`، `entitlements` با `{key,enabled}`، `quotas` با `{key,limit}`، `reason`، `calendar`، `saleTermsReference` و تنظیم‌های اختیاری هشدار/`explicitlyFree`. قیمت و محدودیت‌های واقعی را مالک محصول مشخص می‌کند. این راهنما عدد جایگزین نمی‌سازد. اشتراک ماهانه باید نسخهٔ کامل و معتبر داشته باشد.
3. اگر Draft نیاز به اصلاح دارد، `PUT /api/admin/v1/commerce/plan-versions/{versionId}/draft` را با `CommercialDraftCommand`، `expectedVersion` واقعی و `reason` فراخوانی کنید. **در این DTO قیمت `priceToman` است، ولی در CreatePlatformPlanVersionCommand قیمت `priceAmount` ریال است.** تبدیل را دوبار اعمال نکنید. Entitlement و Quotaها نیز صریح باشند؛ omission را با سهمیهٔ نامحدود فرضی جبران نکنید.
4. `GET /api/admin/v1/commerce/plan-versions/{versionId}/validation` باید `canPublish=true` و `missingFields=[]` بدهد. این بررسی، قیمت یا شرایط تجاری را تأیید محصولی نمی‌کند.
5. `POST /api/admin/v1/commerce/plan-versions/{planVersionId}/publish` با `{expectedVersion,reason}`. `expectedVersion` رشتهٔ rowversion پاسخ فعلی است، نه عدد نسخهٔ پروندهٔ آموزشگاه. Published Version درجا اصلاح نمی‌شود؛ تغییر شرایط، نسخهٔ جدید می‌خواهد.
6. با `PUT /api/admin/v1/commerce/plans/{planId}/availability`، `newPurchaseAvailable` و وضعیت‌های مصوب Catalog/Renewal را صریح ثبت کنید؛ body شامل `catalogVisible/newPurchaseAvailable/renewalAvailable/expectedVersion/reason` است. خواندن مجدد `GET /api/admin/v1/commerce/plans/{planId}` وضعیت نهایی را نشان می‌دهد.

قیمت صفر یا نسخهٔ منتشرنشده/ناقص برای مسیر پرداخت تأسیس معتبر فرض نمی‌شود. نسخه‌ای که قبلاً در خرید/اشتراک عادی استفاده شده، نباید به خدمت اختصاصی تأسیس تبدیل شود. Entitlement/Quotaهای سازمان‌های Legacy دست‌کاری یا بازسازی حدسی نمی‌شوند.

## ثبت نگاشت

`GET /api/admin/v1/school-applications/offerings` نگاشت‌ها و Version عددی جاری را می‌دهد. برای نوعی که وجود ندارد `expectedVersion=0` است. سپس:

```text
PUT /api/admin/v1/school-applications/offerings/LegalOrganization
PUT /api/admin/v1/school-applications/offerings/IndividualTeacher
```

Body هرکدام `SchoolOfferingCommand` است: `clientOperationId` تازه، `expectedVersion` جاری، `planVersionId` منتشرشدهٔ همان خدمت و `reason`. از شناسه‌های محیط دیگر استفاده نکنید. این نگاشت قیمت مستقل ندارد؛ قیمت و شرایط از Published PlanVersion می‌آیند. نسخهٔ جاری یا قبلاً تخصیص‌یافته از خرید عادی جداست تا Approval پرونده دور زده نشود.

Retry همان operation و payload رسید قبلی را بازمی‌گرداند. تغییر payload با همان شناسه یا نسخهٔ stale، ۴۰۹ است. تغییر نگاشت، قیمت/پلن Orderهای قبلی را بازنویسی نمی‌کند. برای Quote/Order، Client فقط `{clientOperationId,expectedVersion}` پرونده را می‌فرستد؛ Price/Currency/PlanVersion را انتخاب نمی‌کند.

## کنترل صحت تنظیمات

با حساب تست مجاز، پروندهٔ کامل Submit شود؛ Reviewer در Approval مدیر فعالِ دارای پروفایل کامل را **صریحاً** انتخاب کند. قبل از انتخاب مدیر، Quote/Order مجاز نیست. سپس:

```text
GET  /api/admin/v1/school-applications/offerings
GET  /api/admin/v1/commerce/plans/{planId}
GET  /api/admin/v1/school-applications/{applicationId}
POST /api/v1/school-applications/{applicationId}/quote
POST /api/v1/school-applications/{applicationId}/orders
```

در Quote، `schoolApplicationId/planVersionId/priceToman` و در Order مبلغ `IRR` با دادهٔ مصوب همان نسخه تطبیق داده شود. نبود نگاشت، `409 SCHOOL_APPLICATION_COMMERCIAL_TERMS_UNAVAILABLE` می‌دهد و Approval را باطل نمی‌کند. `progress` مقدار واقعی پلن/نسخه/مبلغ/واحد و وضعیت Order/Payment/Provisioning را جدا نشان می‌دهد.

پرداخت همان API موجود Commerce/Zibal را دارد؛ callback یا صفحهٔ موفق Client سند Paid نیست. پس از Server Verify معتبر، Payment و Outbox ثبت می‌شوند. پردازش بعدی سازمان، اشتراک ماهانهٔ دارای پایان معتبر و دعوت مدیر را از همان مرجع می‌سازد. `Ready` به معنی ساخته‌شدن onboarding است؛ پذیرش دعوت توسط مدیر جداست. شکست Provisioning موجب پرداخت دوباره نمی‌شود. راه بازیابی مجاز در [Handoff Admin](SCHOOL_APPLICATION_ADMIN_HANDOFF_FA.md) است.

## زمان دعوت مدیر

گزینهٔ زیر از Configuration/Options Pattern خوانده می‌شود و در نبود override، همین مقدار فنی پیش‌فرض را دارد:

```json
{
  "Provisioning": {
    "InitialManagerInvitationLifetimeDays": 7
  }
}
```

محدودهٔ معتبر۱..۳۰ روز است و مقدار نامعتبر Startup را رد می‌کند. زمان واقعی در اولین ایجاد دعوت، Backend محاسبه و ذخیره می‌شود. Retry همان دعوت را تمدید نمی‌کند. این گزینه مستقل از Messaging/Guardian است؛ هیچ secret یا تنظیم خصوصی موجود برای افزودن آن عوض نشده است.

## Test در برابر Production

آزمون‌های Integration فقط در SQL Server/LocalDB disposable، با PlanVersion و قیمت کاملاً ساختگی و adapter واقعی زیبال با HTTP provider کنترل‌شده اجرا می‌شوند. آن اعداد قیمت فروش نیستند و از fixture به Test/Production Seed نمی‌شوند. `MakanApp`، `MakanApp_Test` و سرور در این Task تغییر نمی‌کنند.

در Production باید Admin دادهٔ مصوب، availability، نگاشت دو نوع، تنظیم خصوصی درگاه، callback و پشتیبان‌گیری را جدا آماده کند؛ این سند هیچ credential ندارد. تا آن زمان `PRODUCTION_COMMERCIAL_CONFIGURATION=BLOCKED_CONFIGURATION` گزارش می‌شود. موفقیت کد و تست با آمادگی تنظیم تجاری محیط واقعی تفاوت دارد. AI/Knowledge Service Auth و Gate آموزشی PersonalClass در این مرحله فعال نمی‌شوند.
