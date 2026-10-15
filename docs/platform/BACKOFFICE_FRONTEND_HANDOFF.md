# قرارداد تحویل Backoffice به Frontend

این سند از baseline ورودی STEP 15E یعنی `main@eebf02c` در تاریخ `2026-10-13` استخراج و با پیاده‌سازی نهایی همان مرحله تکمیل شده است. این سند فقط قرارداد مصرف API را توضیح می‌دهد و مجوز ساخت یا تغییر frontend نیست.

## مرز محصول

- Backoffice از اپ عملیاتی مدرسه جداست و همه‌ی مسیرهای آن با `/api/admin/v1` شروع می‌شوند.
- `OrganizationRole.Manager` به معنی Platform Admin نیست.
- مجوزها از assignmentهای فعال Platform IAM در SQL Server خوانده می‌شوند؛ frontend نباید role یا permission را از روی UI حدس بزند.
- `GET /api/admin/v1/me` منبع navigation و capabilityهای جاری است.
- مخفی‌کردن یک منو امنیت نیست؛ هر endpoint مجوز را در server دوباره بررسی می‌کند.
- Platform roleها مجوز عمومی خواندن Contact، Direct خصوصی یا تاریخچه پیام ایجاد نمی‌کنند.

## ورود و bootstrap UI

Backoffice از همان Bearer session احرازشده‌ی Identity استفاده می‌کند. پس از ورود:

1. `GET /api/admin/v1/me` را فراخوانی کنید.
2. در `401` کاربر را به ورود برگردانید.
3. در `403` کد خطا را بررسی کنید: `MFA_REQUIRED` به جریان MFA می‌رود؛ نبود permission همان «دسترسی Backoffice ندارید» است. نقش سازمانی fallback نیست.
4. navigation را از `permissions` پاسخ بسازید.

bootstrap اولین Owner یک فرمان عمومی HTTP نیست. اجرای آن out-of-band و از command-line host انجام می‌شود و پس از ایجاد اولین Owner بسته می‌ماند.

## ماژول‌های navigation

| ماژول | Permission حداقل | APIهای اصلی |
|---|---|---|
| حساب من | `platform.access` | `GET /api/admin/v1/me` |
| IAM | `platform.roles.read` | role catalog و role assignmentها |
| Audit | `platform.audit.read` | `GET /api/admin/v1/audit-logs` |
| سازمان‌ها | `platform.organizations.read` | فهرست/detail/membership/impact |
| کاربران | `platform.users.read` | فهرست و detail امن |
| Provisioning | `platform.provisioning.read` | فهرست درخواست‌ها و وضعیت stageها |
| آموزش | `platform.education.read` | کلاس، معلم، ثبت‌نام، تکلیف و آزمون |
| فراداده پیام‌رسان | `platform.messaging.read_metadata` | فقط فهرست metadata گفتگوها |
| Platform Safety | `platform.moderation.review` | queue، detail و evidence همان Case |
| Commerce | `platform.commerce.read` | Plan/PlanVersion و Subscription |
| Finance | `platform.finance.read` | Order/PaymentAttempt/Payment امن |
| Notification Ops | `platform.notifications.read` | شمارنده‌ها و failure metadata امن |
| System Ops | `platform.operations.read` | job summary و health |

برای AI منو یا route نسازید. backend فقط در health مقدار `NotIntegrated/DependencyPending` برمی‌گرداند و endpoint AI ندارد.

## الگوی list/detail

فهرست‌های Backoffice از `page` یک‌مبنا و `pageSize` استفاده می‌کنند. مقدار پیش‌فرض معمولاً ۳۰ و حداکثر ۱۰۰ است؛ audit پیش‌فرض ۵۰ دارد. پاسخ متداول شامل `items`, `page`, `pageSize`, `totalCount` است.

- با تغییر filter، page را به ۱ برگردانید.
- sort دلخواه برای همه‌ی فهرست‌ها وجود ندارد. فقط وقتی contract پارامتر `sortBy` دارد از مقدارهای allowlist شده استفاده کنید.
- `OrganizationId`, `ClassId`, `UserId` و سایر شناسه‌های UI selector هستند، نه مدرک مجوز.
- detailهای غیرقابل‌مشاهده ممکن است برای جلوگیری از disclosure با `404` پاسخ داده شوند.

## APIهای نوشتن و confirmation

عملیات زیر به permission مدیریت، `Reason` معتبر و در موارد حساس به step-up نیاز دارند:

| عملیات | Permission | Reason | Step-up |
|---|---|---:|---:|
| grant/revoke نقش پلتفرم | `platform.roles.manage` | بله | بله |
| suspend/reactivate سازمان | `platform.organizations.manage` | بله | بله |
| ساخت سازمان | `platform.organizations.manage` | بله | فقط هنگام اتصال PlanVersion |
| provisioning با Subscription | `platform.provisioning.manage` | بله | بله |
| تغییر/پایان عضویت و دعوت | `platform.organizations.manage` | بله | بر حسب عملیات؛ قواعد server مرجع‌اند |
| suspend/reactivate کاربر | `platform.users.manage` | بله | بله |
| revoke نشست | `platform.sessions.manage` | بله | خیر |
| ساخت Plan/PlanVersion | `platform.commerce.manage` | بله | خیر |
| publish PlanVersion | `platform.commerce.manage` | بله | بله |
| override اشتراک | `platform.commerce.manage` | بله | بله |
| review/resolve/dismiss پرونده | `platform.moderation.review` | بله | خیر |
| حذف محتوای case-scoped | review + `platform.moderation.enforce` | بله | خیر |

UI باید پیش از mutation حساس خلاصه‌ی اثر، target صریح و Reason را نشان دهد. از GAP-04، در `PLATFORM_STEP_UP_REQUIRED` verify واقعی TOTP/recovery انجام و سپس همان عملیات با context جاری retry می‌شود. در `MFA_REQUIRED` ابتدا status/enrollment را بررسی کنید؛ نبود key ring با `MFA_UNAVAILABLE` باید به اپراتور ارجاع شود، نه bypass. [قرارداد کامل](../release/GAP_04_BACKOFFICE_TOTP_MFA.md).

## stateهای HTTP

همه‌ی خطاهای business به‌شکل ProblemDetails و با extension پایدار `code` برمی‌گردند.

| Status | رفتار UI |
|---:|---|
| `401` | session نامعتبر/منقضی؛ ورود مجدد |
| `403` | permission یا step-up کافی نیست؛ پیام مجوز و عدم retry خودکار |
| `404` | منبع وجود ندارد یا باید از actor پنهان بماند؛ detail را ببندید |
| `409` | state/idempotency/last-owner/last-manager conflict؛ داده را refresh کنید |
| `412` | precondition/rowversion conflict در قراردادهای مشترک؛ نسخه‌ی جدید را بگیرید. endpointهای Backoffice فعلی عمدتاً conflict را با `409` می‌دهند |
| `422` | ورودی از نظر شکل معتبر ولی از نظر policy/provider رد شده؛ پیام code را نمایش دهید. در Backoffice فعلی نادر است |
| `429` | rate limit؛ طبق `Retry-After` یا backoff تلاش کنید و loop نسازید |
| `503` | dependency پیکربندی نشده یا unavailable؛ موفقیت جعلی نشان ندهید |

کدهای مهم Backoffice:

- `PLATFORM_ACCESS_DENIED`
- `PLATFORM_STEP_UP_REQUIRED`
- `PLATFORM_REASON_REQUIRED`
- `PLATFORM_QUERY_INVALID`
- `PLATFORM_LAST_OWNER_REQUIRED`
- `PLATFORM_LAST_MANAGER_REQUIRED`
- `PLATFORM_ASSIGNMENT_CONFLICT`
- `PLATFORM_ORGANIZATION_CONFLICT`
- `PLATFORM_PROVISIONING_CONFLICT`
- `PLATFORM_MODERATION_STATE_CONFLICT`
- `PLATFORM_PLAN_CONFLICT`
- `PLATFORM_SUBSCRIPTION_CONFLICT`

## UX ماژول‌ها

### IAM و Audit

- role catalog را read-only نمایش دهید.
- grant تکراری همان assignment را با replay برمی‌گرداند؛ duplicate row نسازید.
- revoke آخرین Owner با `409` رد می‌شود.
- Audit read-only و append-only است؛ دکمه edit/delete ندارد.

### Organization و User

- فهرست user شماره تلفن mask‌شده دارد؛ raw phone درخواست نکنید.
- suspend سازمان impact preview دارد؛ قبل از confirmation آن را بخوانید.
- پایان آخرین Manager یا suspend کاربری که آخرین Manager است با `409` رد می‌شود.
- suspend کاربر نشست‌های فعال را revoke می‌کند؛ reactivate نشست قدیمی را زنده نمی‌کند.

### Provisioning

درخواست با `ClientOperationId` پایدار ارسال شود. retry همان payload نتیجه‌ی قبلی را برمی‌گرداند؛ retry با payload متفاوت conflict است. UI stageهای `Organization`, `Subscription` و `ManagerInvitation` را از پاسخ server نمایش دهد و موفقیت بخشی را به‌عنوان تکمیل کل جریان نشان ندهد.

### Education و Messaging

Education read modelها global و با context سازمان هستند. Messaging فقط metadata امن گفتگو را می‌دهد؛ هیچ صفحه‌ای برای مرور متن Direct خصوصی طراحی نکنید.

### Platform Safety

evidence فقط از route همان `CaseId + FileAssetId` دریافت می‌شود. `ExpectedVersion` را از detail جاری ارسال کنید. enforcement فقط روی پیام گزارش‌شده‌ی همان case ممکن است.

### Commerce و Finance

- PlanVersion منتشرشده edit نمی‌شود؛ برای تغییر قیمت/quota/entitlement نسخه‌ی جدید بسازید.
- override اشتراک تاریخچه را نگه می‌دارد.
- Finance فقط read model امن دارد؛ `ProviderReference` mask شده و Payment URL حذف شده است.
- هیچ دکمه‌ی «پرداخت را موفق کن»، refund یا reconcile دستی نسازید.

### Operations

Health یک مشاهده‌ی امن از وضعیت adapter و queue است، نه تضمین SLA. notification failure متن اعلان، شماره، token یا مقصد را برنمی‌گرداند. retry دستی job/notification وجود ندارد.

## وضعیت‌های loading/empty/error

- Loading: skeleton هر جدول و detail مستقل باشد.
- Empty: تفاوت «هیچ داده‌ای نیست» با «filter نتیجه ندارد» مشخص شود.
- Partial dependency: در `Unavailable/DependencyPending` قابلیت مربوطه disabled و دلیل قابل مشاهده باشد.
- Conflict: فرم بسته نشود؛ refresh و مقایسه‌ی state پیشنهاد شود.
- Forbidden after revoke: cache permission پاک و navigation دوباره از `/me` ساخته شود.

## مواردی که frontend نباید بسازد

- صفحه‌ی Landing CMS، Leads، Blog یا FAQ در این Backoffice
- مرور عمومی Contacts یا Direct خصوصی
- payment success دستی
- AI dashboard یا prompt/response viewer
- retry عمومی برای queue/job
- impersonation یا shared admin account

## منابع تکمیلی

- [تصمیم‌های محصول Backoffice](../architecture/BACKOFFICE_PRODUCT_DECISIONS.md)
- [IAM و Owner](../architecture/STEP_15A_PLATFORM_IAM_OWNER_FOUNDATION.md)
- [Organization/User/Provisioning](../architecture/STEP_15B_ORGANIZATION_USER_PROVISIONING.md)
- [Education/Messaging/Safety](../architecture/STEP_15C_EDUCATION_MESSAGING_PLATFORM_SAFETY.md)
- [Commerce/Finance/Operations](../architecture/STEP_15D_COMMERCE_FINANCE_OPERATIONS.md)
- [پذیرش نهایی](../architecture/STEP_15E_BACKOFFICE_FINAL_ACCEPTANCE.md)
