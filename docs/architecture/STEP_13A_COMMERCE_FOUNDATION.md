# STEP 13A — Commerce Foundation

این سند وضعیت واقعی زیرساخت Plan، Subscription، Entitlement، Quota و UsageLedger را ثبت می‌کند. Payment، Order، Backoffice و Provisioning در این مرحله پیاده‌سازی نشده‌اند.

## هدف و مرز ماژول

Commerce به این پرسش پاسخ می‌دهد که یک سازمان در زمان جاری به چه قابلیت‌ها و چه ظرفیتی دسترسی دارد. این ماژول مالک داده‌های آموزشی، فایل، کاربر یا سازمان نیست و فقط policy تجاری را نگهداری می‌کند.

```text
Organization
-> Subscription
-> frozen PlanVersion
-> PlanEntitlement / PlanQuotaDefinition
-> authoritative usage or UsageLedger
-> effective decision
```

نام نمایشی Plan در authorization استفاده نمی‌شود. تصمیم‌ها فقط با `EntitlementKey` و `QuotaKey`های typed و server-owned گرفته می‌شوند.

## Plan و PlanVersion

`Plan` هویت پایدار محصول تجاری با `Code` یکتا و `DisplayName` است. محدودیت‌ها روی خود Plan قرار ندارند.

`PlanVersion` lifecycle ساده `Draft -> Published` دارد. Entitlement و Quota فقط در Draft با متدهای aggregate قابل تنظیم‌اند. بعد از `Publish` هر تغییر Domain رد می‌شود. تغییر شرایط تجاری با نسخه جدید انجام می‌شود و Subscription قبلی خودکار به نسخه جدید منتقل نمی‌شود.

کلیدهای Entitlement فعلی:

```text
Academic
Assignments
Exams
Messaging
Reports
Storage
```

این کلیدها وجود قابلیت را بیان می‌کنند و با ظرفیت عددی یکی نیستند.

## Subscription

Subscription در STEP 13A فقط متعلق به Organization است و مستقیماً به یک `PlanVersion` منتشرشده متصل می‌شود. lifecycle آن:

```text
Pending -> Active -> Expired
                  -> Cancelled
Pending ---------> Cancelled
```

زمان شروع inclusive و زمان پایان exclusive است. وضعیت `Active` به‌تنهایی کافی نیست؛ بازه UTC نیز هنگام resolve شدن بررسی می‌شود. SQL Server با filtered unique index اجازه بیش از یک ردیف دارای Status فعال برای هر سازمان را نمی‌دهد. تاریخچه حذف نمی‌شود.

نبود Subscription یک وضعیت معتبر provisioning است و API مدیر آن را با `NoSubscription` و HTTP 200 نشان می‌دهد. Subscription منقضی، لغوشده، هنوز شروع‌نشده یا خارج از بازه، `Inactive` است و Entitlement/Quota مؤثر ندارد.

## Quota و منبع مصرف

Quotaهای واقعی این مرحله:

| کلید | منبع حقیقت مصرف | نوع |
|---|---|---|
| `ActiveLearners` | Enrollmentهای فعال و learnerهای distinct در Academic | authoritative module |
| `ActiveClasses` | Classهای فعال در Academic | authoritative module |
| `StorageBytes` | FileAssetهای Ready سازمان در Storage | authoritative module |
| `ReportExports` | `commerce.UsageLedger` | metered ledger |

`Limit = null` به معنی unlimited صریح است. نبودن quota definition با unlimited یکسان نیست و تصمیم `QUOTA_REQUIRED` می‌دهد. سقف صفر معتبر است و ایجاد مصرف جدید را می‌بندد.

`CheckCapacityAsync` تصمیم read-only می‌دهد و برای عملیات concurrent تضمین رزرو نیست. مسیر metered باید از `RecordUsageAsync` استفاده کند. برای quotaهای authoritative مانند فراگیر فعال، ماژول مالک در integration آینده باید lock سهمیه و write کسب‌وکاری را در همان transaction کوتاه ترکیب کند؛ check جداگانه قبل از insert تضمین strict maximum نیست.

## UsageLedger و هم‌زمانی

`UsageLedgerEntry` immutable است و شامل Subscription، QuotaKey، Amount مثبت، `SourceOperationId` و زمان UTC است. Unique Index زیر retry همان عملیات را idempotent می‌کند:

```text
SubscriptionId + QuotaKey + SourceOperationId
```

مصرف `ReportExports` در Transaction سریال‌پذیر انجام می‌شود:

```text
Begin transaction
-> lock active Subscription with UPDLOCK/HOLDLOCK
-> verify effective published PlanVersion
-> read quota definition
-> resolve duplicate SourceOperationId
-> aggregate current ledger usage in SQL Server
-> reject over capacity or append one entry
-> commit
```

قفل مشترک Subscription درخواست‌های مصرف متفاوت را برای آخرین واحد ظرفیت serialize می‌کند. retry هم‌زمان با SourceOperationId یکسان یک entry می‌سازد. استفاده مجدد همان SourceOperationId با Amount متفاوت `COMMERCE_USAGE_CONFLICT` است.

Reservation/settlement/reversal هنوز ساخته نشده‌اند. مدل append-only فعلی مسیر افزودن آن‌ها را بدون تبدیل مصرف به یک counter مبهم باز می‌گذارد.

## Downgrade و workflow فعال

Downgrade هیچ Enrollment، Class، File یا رکورد تاریخی را حذف یا suspend نمی‌کند. اگر مصرف authoritative از سقف نسخه جدید بالاتر باشد، Summary مقدار `IsOverQuota=true` و Remaining صفر می‌دهد. عملیات ظرفیت‌ساز جدید باید در integration مربوط به ماژول مالک رد شود تا مصرف کاهش یابد یا Plan ارتقا پیدا کند.

QuotaGuard سراسری وجود ندارد. workflowی که قبلاً مجاز و شروع شده است، مانند آزمون در حال اجرا، فقط به‌دلیل تمام‌شدن quota قطع نمی‌شود. هر integration آینده باید مرز «شروع عملیات جدید» را از «تکمیل workflow جاری» جدا کند.

## سرویس Application

`IEntitlementService` قرارداد reusable ماژول‌هاست:

```text
GetEffectiveSubscriptionAsync
IsEnabledAsync
GetQuotaAsync
CheckCapacityAsync
RecordUsageAsync
```

ماژول‌های دیگر نباید جدول‌های Commerce را مستقیم query کنند. `CommerceService` از `TimeProvider` استفاده می‌کند و زمان authoritative را از client نمی‌گیرد.

## API مدیر و authorization

```http
GET /api/v1/commerce/summary
Authorization: Bearer <opaque-session-token>
```

ورودی OrganizationId ندارد. `IAccessContextResolver` باید workspace سازمانی، Membership فعال و `OrganizationRole.Manager` جاری را resolve کند. مدیر A فقط Summary سازمان انتخاب‌شده A را می‌بیند. Teacher، Student و Parent مجاز نیستند.

Response شامل Plan/Version، lifecycle Subscription، Entitlementهای مؤثر، Limit، مصرف جاری، Remaining، unlimited و over-quota است. credential، provider token، payment data یا platform note در DTO وجود ندارد.

هیچ API عمومی برای ساخت/ویرایش Plan، انتشار نسخه، تغییر quota یا فعال‌سازی Subscription وجود ندارد. Manager سازمانی Platform Admin نیست.

## schema SQL Server

Migration `20261012171406_AddCommerceFoundation` schema `commerce` و جدول‌های زیر را ایجاد می‌کند:

- `Plans`
- `PlanVersions`
- `PlanEntitlements`
- `PlanQuotaDefinitions`
- `Subscriptions`
- `UsageLedger`

قواعد اصلی دیتابیس:

- `Plans.Code` یکتا
- `(PlanId, VersionNumber)` یکتا و VersionNumber مثبت
- سازگاری Status/PublishedAtUtc نسخه
- `(PlanVersionId, Key)` یکتا برای Entitlement و Quota
- Limit برابر null یا غیرمنفی
- تاریخ پایان Subscription بعد از شروع
- سازگاری Status لغوشده و CancelledAtUtc
- filtered unique index برای یک Subscription دارای Status فعال در هر Organization
- FK ترکیبی UsageLedger به `(OrganizationId, SubscriptionId)` برای tenant isolation
- Amount مصرف مثبت
- unique retry identity برای UsageLedger
- `rowversion` روی Plan، PlanVersion، PlanEntitlement، PlanQuotaDefinition و Subscription

Migration در startup خودکار اجرا نمی‌شود و روی دیتابیس عادی یا Production این مرحله apply نشده است. Integration Testها آن را روی LocalDB ایزوله اجرا می‌کنند.

## خطاهای پایدار

```text
COMMERCE_SUMMARY_NOT_ALLOWED
SUBSCRIPTION_REQUIRED
SUBSCRIPTION_INACTIVE
ENTITLEMENT_REQUIRED
QUOTA_REQUIRED
QUOTA_EXCEEDED
COMMERCE_USAGE_INVALID
COMMERCE_USAGE_CONFLICT
```

خطاها با ProblemDetails نگاشت می‌شوند و جزئیات SQL Server را افشا نمی‌کنند.

## مرزهای آینده

- STEP 13B مالک Order، Payment، PaymentAttempt، callback/webhook، verification و handoff فعال‌سازی Subscription است. Payment با Subscription یکی نیست.
- Backoffice آینده باید PlatformRole/Permission مستقل، least privilege، audit، ایجاد Organization، انتخاب PlanVersion، provisioning و دعوت مدیر اولیه را بسازد.
- Manager سازمانی هیچ دسترسی ضمنی به Backoffice ندارد. حفاظت آخرین Manager متعلق به Organization/Backoffice است، نه Commerce.
- Contacts و People Discovery متعلق به Messaging هستند و با Commerce مخلوط نشده‌اند.
- AI فعلی به Commerce وصل نشده است. سرویس AI آینده نباید SQL Server Commerce را مستقیم تغییر دهد؛ Backend باید authority باقی بماند.
- احراز هویت Opaque Bearer و UserSession در این مرحله تغییر نکرده است. hardening آینده شامل access token کوتاه‌عمر، refresh rotation، secure device storage، revocation، گزینه HttpOnly Web و MFA/re-auth برای Backoffice حساس است.
- full Platform Audit، quota override، reservation، reversal و اتصال quota به تمام write pathها هنوز خارج از این مرحله‌اند.
