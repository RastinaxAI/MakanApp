# قرارداد جاری Commerce برای Frontend — BE-17

بازبینی `2026-11-26` از `source@f8178a47de94eae0ec2431adebcb9256a9789786`.

> سیاست جاری `2026-11-26`: [Open Access](../product/OPEN_ACCESS_FILE_DELIVERY_FA.md) با Commerce:EnforcementEnabled=false شرط تجاری استفادهٔ عادی و ذخیرهٔ Source را حذف کرده است؛ AI Unmetered و OperationId/ACL برقرارند. پرداخت/Push بیرونی و PersonalClass Academic مستقل Deferred هستند. [Media Review](../messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md) اکنون تصمیم انسانی مصوب دارد؛ Pending/Rejected تحویل ندارد و Approval خودکار پیام نمی‌فرستد. شواهد و اعداد تاریخی این سند متعلق به زمان خود هستند.

> به‌روزرسانی `2026-11-22`: [سیاست Open Access](../product/OPEN_ACCESS_FILE_DELIVERY_FA.md) بر توضیحات Enforcement تجاری پایین اولویت دارد. کنترل Subscription/Entitlement/Quota برای قابلیت‌های عادی خاموش است؛ فیلد `enforcementEnabled=false` از وضعیت واقعی قرارداد جداست. دادهٔ مالی و APIهای مدیریت باقی‌اند؛ پرداخت زنده همچنان Deferred است.

**سیاست انتشار `2026-11-18` از پایهٔ `d397d73`:** پرداخت‌های Runtime همه Deferred هستند و
Provider هر دو محیط `Unavailable` می‌ماند. Purchase، Renew، Upgrade، Downgrade نیازمند
پرداخت، Pay و Zibal redirect در flow قابل استفاده نمایش داده نشوند. School Quote → Payment
→ Paid Provisioning نیز Deferred است؛ کد حذف و Provisioning رایگان حدسی ساخته نمی‌شود.
Catalog/مدل‌ها/مالکیت/Regression زیر حفظ‌اند؛ توضیح فروش زنده برای انتشار آینده است.

AI/Knowledge جاری از تجارت مستقل‌اند: Login و مجوز Context/Source لازم، Subscription،
Entitlement تجاری، پرداخت، Quota، Trial و انتخاب Payer لازم نیستند. Remaining=null است و
شمارنده/QuotaExceeded UI برای AI استفاده نمی‌شود. [قرارداد Unmetered](AI_UNMETERED_CURRENT_RELEASE_FA.md)
و [Scope جاری](CURRENT_RELEASE_SCOPE_FA.md) بر توضیحات تجاری تاریخی پایین اولویت دارند.

مبنا `f030ab7` روی `feature/product-v6-backend-alignment`؛ بازبینی `2026-11-16`.
Source اعتبارسنجی: `3561610336e88b225da5463c959a640d4bcf9ae5`؛۳۴۲۵ تست موفق و Migration جدید صفر.
این سند قرارداد Source است؛ وجود کد/تست، شاهد تنظیم Catalog یا پرداخت زندهٔ Production نیست.
Frontend در این مرحله تغییر نکرده است.

## مرز مالکیت و مجوز

Individual فقط متعلق به همان UserId و Organization فقط متعلق به OrganizationId معتبر است.
مسیر سازمانی، Workspace جاری با Membership و نقش فعال Manager می‌خواهد؛ query/body مجوز نمی‌سازد.
خرید، Membership، RoleAssignment، Enrollment، Guardian یا اختیار آموزشی ایجاد نمی‌کند.
از نام Plan یا قیمت آن permission استخراج نکنید؛ شرط عملیات عبارت است از:
Feature Gate روشن + Authorization معتبر + Entitlement مناسب + Quota کافی.
Gateهای Assignment/Exam کلاس مستقل پیش‌فرض خاموش‌اند و خرید آن‌ها را روشن نمی‌کند.

Guardian به‌تنهایی اشتراک، سهمیه، Order، Payment یا Usage کودک را در اختیار والد نمی‌گذارد.
والد و هر فرزند قرارداد مالی جدا دارند. D05 باز است؛ Family/Shared و pooling پیاده نشده‌اند.

## مسیرهای کاربر

همهٔ مسیرهای جدول نشست معتبر می‌خواهند؛ پایه `/api/v1/commerce` است.

| Method | مسیر زیر پایه | ورودی/پاسخ و مجوز |
|---|---|---|
| GET | catalog?audience=Individual یا Organization | CommercialPlanDto[]؛ نسخهٔ Published کامل، نمایان و قابل خرید همان Audience |
| GET | personal/subscription | CommercialSummaryDto فقط همان User؛ انتخاب Child/User دیگر ندارد |
| GET | organization/subscription | Summary سازمان Workspace جاری، فقط Manager |
| POST | personal/quote یا organization/quote | CreateOrderCommand → CommercialQuoteDto؛ preview بدون Order/Payment |
| POST | personal/orders | همان Command؛ ایجاد201، replay200 با همان Order |
| GET | personal/orders و personal/orders/{orderId} | فهرست یا OrderDto فقط مالک |
| POST | personal/orders/{orderId}/payments | InitiatePaymentCommand با ClientOperationId → InitiatePaymentResult |
| POST / GET | orders و orders/{orderId} | سفارش سازمانی موجود؛ Context از نشست Manager resolve می‌شود |
| POST | orders/{orderId}/payments | initiation همان سفارش سازمانی، بدون مبلغ Client |
| POST | personal/subscription/downgrade یا organization/subscription/downgrade | ScheduleDowngradeCommand با PlanVersionId/ExpectedVersion/Reason |
| POST | ai/simple-text/preflight | AiQuotaPreflightCommand؛ ContextType/ContextId/Payer، بدون تماس AI یا برداشت |

CreateOrderCommand شامل PlanVersionId، ClientOperationId، Purpose و SubscriptionId اختیاری است.
برای Renewal/Upgrade، SubscriptionId باید متعلق به همان مالک resolveشده باشد.
Quote نیز ClientOperationId معتبر می‌خواهد؛ این شناسه رزرو قیمت Quote نیست.
ساخت Order دوباره شرایط جاری را بررسی و سپس شرایط همان Order را freeze می‌کند.

بازیابی/رسید پرداخت از Controllerهای موجود مصرف می‌شود؛ endpoint مستقل تازه‌ای در BE-17 ساخته نشده است.
جزئیات در [Handoff زیبال](../commerce/ZIBAL_FRONTEND_HANDOFF_FA.md) است.
callback، return مرورگر و متن موفقیت Client authority پرداخت نیستند؛ Server Verify لازم است.

## قیمت، دوره و نسخه

| قرارداد | واحد |
|---|---|
| CreatePlatformPlanVersionCommand.PriceAmount | IRR؛ ورودی Admin هنگام ساخت نسخه |
| CommercialDraftCommand.PriceToman | TOMAN؛ ویرایش Draft، تبدیل یک‌بارهٔ سرور به IRR |
| OrderDto.Amount / PaymentAttemptDto.Amount | IRR، همراه Currency متناظر |
| Catalog/Quote/Summary.PriceToman | TOMAN؛ دوباره تقسیم بر۱۰ نشود |

مبلغ Quote/Order/Payment را Client تعیین نمی‌کند. قیمت صفر فقط با ExplicitlyFree معتبر است.
پشتیبانی engine از قیمت رایگان، وجود Plan رایگان مصوب در Production را اثبات نمی‌کند.
Published Version immutable است؛ تغییر Price/Quota/Entitlement/Calendar نسخهٔ جدید می‌خواهد.
Order قدیمی PlanVersion و مبلغ/واحد/شرایط منجمد خود را نگه می‌دارد.

دوره از BillingCalendar نسخه و لنگر UTC با مرز تهران محاسبه می‌شود؛ ساعت دستگاه مرجع نیست.
AutoRenew=false و RenewalMode=Manual است. Commerce:QuoteLifetimeMinutes فقط اعتبار فنی
Quote است؛ مدت اشتراک از آن استنتاج نشود. ExpiresAtUtc پاسخ مرجع UI است.

## Availability و جریان خرید

CatalogVisible=false عرضهٔ عمومی را پنهان می‌کند. NewPurchaseAvailable=false خرید تازه را می‌بندد.
RenewalAvailable=false تمدید تازه را حتی با تغییر Plan مسدود می‌کند؛ انتقال به Plan متفاوت
علاوه بر مجازبودن تمدید، NewPurchaseAvailable مقصد را هم لازم دارد.
Scheduled downgrade مقصد غیرقابل تمدید پذیرفته نمی‌شود و Summary آن را گزینهٔ معتبر معرفی نمی‌کند.
Draft کامل نیز هنوز عرضهٔ Published نیست؛ Downgrade آن409 با PLAN_NOT_PURCHASABLE می‌دهد.
این تغییرات Subscription موجود یا شرایط Order قبلاً ثبت‌شده را حذف/بازنویسی نمی‌کنند.

Quote.PreviewOnly=true است؛ PaymentAvailable/PaymentUnavailableCode و ExpiresAtUtc را بخوانید.
Quote != Order؛ Order != Paid؛ Paid != Entitlement فعال تا Fulfillment.
پس از بازگشت پرداخت، Order/Receipt/FulfillmentState را دوباره از Backend بخوانید؛
گم‌شدن callback دلیل ساخت پرداخت تازه نیست. Retry همان operation/intent رسید قبلی را می‌دهد.
در Plan صریحاً رایگان ممکن است Order بدون IPG Paid شود؛ Fulfillment همچنان مستقل است.

Renewal زودهنگام دورهٔ آینده می‌سازد؛ قبل از شروع، حق آینده وارد مصرف جاری نمی‌شود.
Upgrade قیمت و سهمیهٔ دوره‌ای را prorate و مصرف/رزرو قبلی را حفظ می‌کند.
Downgrade در دورهٔ بعد مؤثر است؛ کاهش سقف، مجوز حذف دادهٔ مازاد نیست.

## Quota و مصرف

صفر ظرفیت صفر است؛ null فقط برای تعریف Quota موجود، Unlimited است. نبود کلید، Unlimited نیست.
Key/Kind/Unit/Limit/Consumed/Reserved/Remaining/IsUsageAvailable/IsOverQuota را مصرف کنید.
CurrentCapacity با شروع ماه صفر نمی‌شود؛ PeriodicConsumption به Subscription/Period بسته است.

| کلید | مرجع شمارش فعلی |
|---|---|
| ActiveClasses | کلاس‌های بایگانی‌نشدهٔ Host یا کلاس‌های فعال سازمان، مطابق مدل موجود |
| ActiveLearners | Learnerهای distinct و فعال همان مالک؛ Teacher/Manager بدون Learner صندلی نیست |
| SourceCount | منابع ذخیره‌شدهٔ مالک؛ Archive با حذف فیزیکی یکی نیست |
| StorageBytes سازمان | FileAssetهای Ready همان Organization |
| StorageBytes فردی | Readyهای کلاس‌های Host و فایل‌های متصل به نسخه‌های منابع Personal همان صاحب منبع |
| AssignmentFileBytes | Ready متصل به نسخهٔ تکلیف یا Submission همان scope؛ یک FileAsset فقط یک‌بار |
| AiTextRequests / ReportExports | ledger مصرف + Reserved/Unknown در همان دوره |

Retained همچنان Ready و ظرفیت اشغال‌شده است. Pending/Rejected/Deleted در این meterهای bytes شمرده نمی‌شوند.
فایل Library یا فایل موقت فاقد اتصال تجاری، حدسی به مالک شخصی نسبت داده نمی‌شود.
meter خواندنی، gate سراسری جدید روی Upload عمومی نیست؛ مجوز و Safety قبلی برقرارند.
SourceCount و meterهای فایل در Quote/Summary قابل‌شمارش‌اند؛ placeholder بنیاد قدیمی ملاک UI نیست.

رزرو AI/Export: Preflight → Reserve → اجرا بیرون transaction → Consume/Release/Unknown.
Unknown سهمیه را نگه می‌دارد؛ failure قطعی یا Released نیست.
Consumer + OperationId + همان payload/payer/context/quota تکرار امن است؛ intent متفاوت conflict می‌دهد.
نهایی‌سازی مصرف endpoint عمومی ندارد و اجراکنندهٔ داخلی معتبر آن را انجام می‌دهد.

## Scope، Grace و تنظیم محیط

AI شخصی و AI دانشجوی PersonalClass از Individual/FreeTrial همان مصرف‌کننده‌اند؛ Quota میزبان قرض داده نمی‌شود.
AI سازمانی پیش‌فرض Organization است؛ انتخاب Individual صریح و مستقل از permission داده است.
Report Export فقط Organization payer دارد؛ Individual/Trial fallback ندارد.
FreeTrial همان سیاست فعلی روزانهٔ متن ساده با مرز تهران است؛ device/role/organization سهمیهٔ تازه نمی‌سازد.

Organization بدون قرارداد معتبر در AwaitingValidContract/LegacyPendingContract می‌ماند؛
تاریخ، قیمت، Quota یا Unlimited حدسی ساخته نمی‌شود. کنترل جدید قرارداد معتبر مصوب می‌خواهد.
Grace سازمان هفت روز است؛ خواندن مجاز، Messaging، پرداخت و کاهش دسترسی محفوظ‌اند؛
ظرفیت تازه بسته است و ExistingWrite طبق policy فعلی ادامه دارد. Individual مهلت سازمان ندارد.
انقضای اشتراک، Login/Security/حذف حساب را مسدود نمی‌کند.

School Offering برای LegalOrganization و IndividualTeacher از Published Organization PlanVersion می‌آید؛
اشتراک Personal متقاضی نیست. [پیکربندی آموزشگاه](SCHOOL_COMMERCIAL_CONFIGURATION_FA.md) دادهٔ مصوب می‌خواهد؛
PRODUCTION_SCHOOL_COMMERCIAL_CONFIGURATION=BLOCKED_CONFIGURATION با code failure یکی نیست.
هیچ قیمت/سهمیهٔ Production در این مرحله Seed نشده است.

## خطا و قرارداد جاری

ProblemDetails/errorCode و correlation موجود را مصرف کنید؛ پیام خصوصی/مالی دیگران را نمایش ندهید.
COMMERCE_USAGE_INVALID/ORDER_INVALID=400؛ Guid، ExpectedVersion و ورودی نامعتبر را اصلاح کنید.
AUTH_REQUIRED=401؛ COMMERCE_CONTEXT_NOT_ALLOWED/FEATURE_NOT_ENTITLED=403؛
ORDER_NOT_FOUND/COMMERCE_RESOURCE_NOT_FOUND=404؛ QUOTA_REQUIRED/QUOTA_EXCEEDED/PLAN_NOT_PURCHASABLE
و COMMERCE_USAGE_CONFLICT=409؛ PLAN_TERMS_INCOMPLETE=422؛ COMMERCE_METER_UNAVAILABLE=503.
نبود مجوز داده با خرید رفع نمی‌شود؛403 را خودکار CTA خرید نکنید.

Snapshot واقعی:۴۳۸ Path،۴۹۴ Operation،۷۸۳ Schema؛ SHA-256 برابر
`616e22cf15bc67b604ea12f3348c4612d0289ebaf413eb4d83635c65770cf4f0` است.
با ثابت‌بودن Controller/DTO، Hash قرارداد تغییر نکرده است.

## راهنمای Admin با API موجود

نشست فعال و platform.commerce.read/manage لازم است؛ School Manager، Platform Admin نیست.
پایه `/api/admin/v1/commerce` و ترتیب کار عبارت است از:

1. POST plans با CreatePlatformPlanCommand و Audience صریح؛ Code/DisplayName/Reason.
2. POST plans/{planId}/versions با CreatePlatformPlanVersionCommand؛ PriceAmount ریال، Currency، Entitlements، Quotas، Calendar و SaleTermsReference مصوب.
3. PUT plan-versions/{versionId}/draft در صورت نیاز؛ PriceToman تومان، ExpectedVersion جاری و Reason. این مسیر upsert مقادیر است؛ حذف کلید را جای حذف تعریف قبلی فرض نکنید.
4. GET plan-versions/{versionId}/validation؛ MissingFields باید خالی و CanPublish=true باشد.
5. POST plan-versions/{versionId}/publish با ExpectedVersion و Reason؛ تغییر بعدی نیازمند نسخهٔ جدید است.
6. PUT plans/{planId}/availability با سه flag، ExpectedVersion و Reason مصوب.
7. GET history/{targetId} برای Audit امن؛ actor/action/time/reason/before/after، بدون payment secret.

ثبت نخستین قرارداد معتبر از POST organizations/{ownerId}/commercial-contracts یا users/{ownerId}/commercial-contracts
با RegisterCommercialContractCommand انجام می‌شود؛ این مسیر جای ویرایش تاریخچهٔ Legacy نیست.
مرجع School Offering و permissionهای اختصاصی آن، سند پیکربندی آموزشگاه بالاست؛ قیمت یا مدت تجاری حدس زده نمی‌شود.

[Product Matrix](COMMERCE_PRODUCT_MATRIX_FA.md) و [گزارش توسعه‌دهنده](COMMERCE_BE17_DEVELOPER_REPORT_FA.md) مرجع شواهد‌اند.
