# STEP 15D — عملیات تجاری، مالی و سلامت سامانه

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## هدف

این مرحله Backoffice را به read modelها و عملیات کنترل‌شده‌ی Commerce، Finance، Notification و System Operations متصل می‌کند. طراحی عمداً از ساخت مسیرهای نمایشی یا خطرناک خودداری می‌کند: موفقیت پرداخت فقط از جریان verification درگاه موجود به دست می‌آید، retry دستی برای job یا اعلان وجود ندارد و تا زمانی که AI provider واقعی در backend متصل نشده باشد endpoint یا metric ساختگی AI ارائه نمی‌شود.

## تصمیم‌های ثابت

- تمام endpointهای این مرحله زیر `/api/admin/v1` و تحت IAM پلتفرم هستند.
- قیمت هر `PlanVersion` نسخه‌دار و فقط با واحد `IRR` است.
- `PlanVersion` منتشرشده immutable است؛ تغییر قیمت، entitlement یا quota فقط با ساخت نسخه‌ی جدید انجام می‌شود.
- اشتراک‌های موجود به نسخه‌ی قبلی متصل می‌مانند و انتشار نسخه‌ی جدید آن‌ها را بازنویسی نمی‌کند.
- override دستی اشتراک، رکورد فعال قبلی را لغو و رکورد جدیدی ایجاد می‌کند؛ تاریخچه حذف نمی‌شود.
- انتشار نسخه‌ی پلن و override اشتراک علاوه بر permission به step-up authorization و `Reason` معتبر نیاز دارند.
- Backoffice هیچ مسیر `mark successful` برای پرداخت ندارد. وضعیت موفق فقط از verification واقعی provider و قواعد موجود Commerce ایجاد می‌شود.
- اطلاعات مالی، notification و health نباید token، شماره مقصد، متن اعلان، payment URL یا provider reference کامل را افشا کنند.
- retry دستی اعلان/job در این مرحله ارائه نشده است، چون مدل‌های فعلی عملیات امن و idempotent برای بازکردن failure نهایی ندارند.
- Refund و عملیات reconcile دستی تا اتصال provider واقعی و تصویب قرارداد محصول پیاده‌سازی نشده‌اند.

## Permissionها و نقش‌ها

Permissionهای جدید:

- `platform.commerce.read`
- `platform.commerce.manage`
- `platform.finance.read`
- `platform.finance.manage`
- `platform.operations.read`
- `platform.operations.manage`
- `platform.notifications.read`
- `platform.ai.read`

ماتریس این مرحله:

| نقش | Commerce Read | Commerce Manage | Finance Read | Finance Manage | Operations Read | Operations Manage | Notifications Read | AI Read |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `PlatformOwner` | بله | بله | بله | بله | بله | بله | بله | بله |
| `Operations` | بله | خیر | بله | خیر | بله | بله | بله | بله |
| `Support` | خیر | خیر | خیر | خیر | بله | خیر | بله | خیر |
| `Finance` | بله | بله | بله | بله | خیر | خیر | خیر | خیر |
| `SecurityAudit` | خیر | خیر | خیر | خیر | بله | خیر | بله | خیر |

وجود permissionهای `platform.finance.manage`، `platform.operations.manage` و `platform.ai.read` به معنای وجود mutation یا endpoint مصنوعی نیست. این کلیدها قرارداد IAM آینده را تثبیت می‌کنند؛ endpoint فقط وقتی اضافه می‌شود که use case امن و وابستگی واقعی وجود داشته باشد.

## Endpointهای Commerce

### خواندن

- `GET /api/admin/v1/commerce/plans`
- `GET /api/admin/v1/commerce/plans/{planId}`
- `GET /api/admin/v1/commerce/subscriptions`

فهرست‌ها صفحه‌بندی می‌شوند. subscription read model شامل context سازمان، نسخه‌ی دقیق پلن، وضعیت و مصرف quotaهای همان اشتراک است. مصرف از `ICommerceStore.GetCurrentUsageAsync` محاسبه می‌شود و سقف نسخه‌ی متصل به اشتراک را نشان می‌دهد.

### نوشتن

- `POST /api/admin/v1/commerce/plans`
- `POST /api/admin/v1/commerce/plans/{planId}/versions`
- `POST /api/admin/v1/commerce/plan-versions/{planVersionId}/publish`
- `POST /api/admin/v1/commerce/organizations/{organizationId}/subscription-override`

ساخت نسخه داخل transaction با isolation سطح `Serializable` و قفل `UPDLOCK, HOLDLOCK` انجام می‌شود تا شماره نسخه‌ی تکراری در رقابت ایجاد نشود. انتشار با `rowversion` و `ExpectedVersion` محافظت می‌شود. override اشتراک نیز سازمان و اشتراک فعال را در همان transaction قفل می‌کند؛ اشتراک قبلی لغو و اشتراک جدید فعال می‌شود.

عملیات زیر audit append-only ایجاد می‌کنند:

- `platform.commerce.plan.create`
- `platform.commerce.plan-version.create`
- `platform.commerce.plan-version.publish`
- `platform.commerce.subscription.override`

## Endpointهای Finance

- `GET /api/admin/v1/finance/orders`
- `GET /api/admin/v1/finance/orders/{orderId}`

این read model سفارش، تلاش‌های پرداخت و پرداخت نهایی را با context سازمان نمایش می‌دهد. `ProviderReference` فقط به‌صورت mask شده برگردانده می‌شود و `PaymentUrl` در قرارداد Backoffice وجود ندارد.

در محیط production فعلی `UnavailablePaymentGateway` ثبت شده است. بنابراین reconciliation واقعی تا تنظیم provider قابل انجام نیست و پاسخ فهرست مقدار `Unavailable/ProviderDependencyPending` را گزارش می‌کند. در محیطی که provider واقعی/آزمایشی ثبت شده باشد مقدار `ProviderVerificationOnly` برگردانده می‌شود؛ این مقدار اجازه‌ی جعل نتیجه پرداخت نیست.

هیچ endpoint برای موارد زیر وجود ندارد:

- علامت‌زدن دستی سفارش یا پرداخت به‌عنوان موفق
- ساخت `Payment` بدون verification provider
- refund
- تغییر provider reference

## Notification و Job Operations

- `GET /api/admin/v1/operations/notifications`
- `GET /api/admin/v1/operations/jobs`

خلاصه‌ی notification فقط تعداد وضعیت‌های Pending، Processing، Deferred و Failed، سن قدیمی‌ترین مورد قابل ارسال، availability نوع adapter و failureهای اخیر را برمی‌گرداند. failure item شامل شناسه، کانال، وضعیت، تعداد تلاش، failure reason و timestampها است و این داده‌ها را ندارد:

- `DestinationReference`
- شماره تلفن یا push token
- عنوان و متن اعلان
- provider message id

خلاصه‌ی job تعداد وضعیت‌های `ReportExport` و تعداد pendingهای `IntegrationOutbox` و `MessagingRealtimeOutbox` را گزارش می‌کند. مسیر عمومی برای rerun دلخواه job یا retry دستی failure نهایی وجود ندارد.

## سلامت سامانه و AI

- `GET /api/admin/v1/operations/health`

این endpoint فقط وضعیت‌هایی را گزارش می‌کند که backend در لحظه می‌تواند با شواهد محلی تعیین کند:

- API: تولید موفق پاسخ توسط host
- SQL Server: نتیجه‌ی `Database.CanConnectAsync`
- Storage: نوع adapter پیکربندی‌شده، بدون نوشتن فایل آزمایشی destructive
- Payment provider: نام/availability adapter، بدون انجام تراکنش
- SignalR: ثبت‌شدن hub در host؛ نه ادعای اتصال client خارجی
- Notification و job queue: همان شمارنده‌های امن عملیاتی

AI backend integration در repository وجود ندارد. مقدار `AiIntegrationStatus` برابر `NotIntegrated/DependencyPending` است و هیچ endpoint AI ساخته نشده است. این رفتار عمداً صادقانه است و prompt، محتوای کاربر یا metric جعلی تولید نمی‌کند.

## لایه‌بندی Onion

- `Domain`: کلیدهای permission به catalog پلتفرم افزوده شده‌اند؛ قواعد نسخه‌پذیری Plan و وضعیت Subscription/Payment همان مدل‌های موجود Domain هستند.
- `Application`: قراردادهای request/response، validation، use case service و port با نام `IPlatformCommerceOperationsStore` قرار دارند.
- `Infrastructure`: queryها، transactionها، locking، audit و health adapter inspection در `EfPlatformCommerceOperationsStore` پیاده‌سازی شده‌اند.
- `Api`: سه controller نازک، routing، policy authorization و mapping HTTP را انجام می‌دهند.

جریان dependency:

```text
HTTP Request
-> Platform Controller
-> PlatformCommerceOperationsService
-> Domain invariants / Application validation
-> IPlatformCommerceOperationsStore
-> EfPlatformCommerceOperationsStore
-> SQL Server / configured adapters
-> Application result
-> HTTP Response
```

## دیتابیس و Migration

Migration این مرحله:

```text
20261013064642_AddPlatformCommerceFinanceOperations
```

این migration جدول یا ستون تجاری جدید ایجاد نمی‌کند. فقط هشت permission ثابت را در `platform.PlatformPermissions` و mapping نقش‌ها را در `platform.PlatformRolePermissions` seed می‌کند. Plan، PlanVersion، Price، Entitlement، Quota، Subscription، Order، PaymentAttempt، Payment، Notification، Outbox و ReportExport از schemaهای موجود استفاده می‌شوند.

## مرزهای فعلی

- retire کردن PlanVersion در Domain فعلی مدل نشده و endpoint ساختگی برای آن ایجاد نشده است.
- edit درجا برای PlanVersion منتشرشده وجود ندارد.
- provider پرداخت production هنوز unavailable است؛ reconcile/refund واقعی وابستگی خارجی و تصمیم محصول می‌خواهد.
- retry دستی notification، outbox یا report export ارائه نشده است.
- AI provider و endpoint AI وجود ندارد.
- health check یک مشاهده‌ی کم‌خطر از dependencyهای محلی است و جایگزین synthetic monitoring بیرونی یا SLA monitoring نیست.
