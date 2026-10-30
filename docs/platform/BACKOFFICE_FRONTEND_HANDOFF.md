# قرارداد تحویل Backoffice به Frontend

## قرارداد تجاری افزودهٔ Admin — ۲۰۲۶-۱۰-۳۰

PlatformCommercialContractsController مسیرهای زیر /api/admin/v1/commerce را با permissionهای CommerceRead/CommerceManage اضافه می‌کند: summary مالک User/Organization، availability مستقل Plan، ویرایش Draft و validation نسخه، ثبت commercial-contract و history. [Handoff دقیق Admin](../product/SUBSCRIPTION_ADMIN_HANDOFF_FA.md) و [مرجع request/response](../backend-api-reference.md).

School Manager مجوز Global Plan ندارد. Limit باید در JSON حاضر باشد؛ صفر، null صریح و حذف field متفاوت‌اند. Published terms از Draft تغییر نمی‌کند؛ قیمت صفر ExplicitlyFree می‌خواهد. Legacy بدون ثبت معتبر Admin خودکار قفل نمی‌شود؛ ثبت موفق MigrationStatus=Completed و زمان واقعی CommercialEnforcementFromUtc می‌دهد. قیمت/سقف یا سررسید گذشته حدس نزنید و پرداخت دستی موفق نسازید. reason، rowversion، قفل مالک و Audit حفظ‌اند؛ AI/provider واقعی با این API فعال نمی‌شود.

ClientInvalidated کلید commerce یا access فقط refetch مجاز می‌خواهد؛ تغییر زمان‌محور پایان دوره event مستقل جدید ندارد. [قرارداد Realtime](../product/REALTIME_EVENT_CONTRACT_FA.md).

بازبینی `2026-10-30` از `main@e11d4ec`: ورود Platform فقط OTP/Session و permission جاری دارد؛ MFA/step-up و endpointهای آن حذف شده‌اند و UI نباید آن‌ها را فراخوانی کند. Directory مدرسه، roster و Lesson APIهای School با Workspace Manager/Teacher هستند؛ Platform role جای نقش آموزشی نیست. DOB شخصی و عکس مجوز admin یا تماس با فرد ایجاد نمی‌کنند. [مدرسه](../product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md) و [حذف عامل دوم](PLATFORM_AUTHENTICATION_CHANGE_FA.md).

## افزودهٔ عملیات PH و ورود دو محیط

ورود همچنان همان Bearer session است؛ Test/Mock مجوز Platform نمی‌سازد. پس از login، me و permission جاری resolve می‌شوند؛ عامل دوم و Step-Up حذف شده‌اند. مسیر Production از OTP واقعی استفاده می‌کند؛ UI از شکل challenge نباید محیط یا موفقیت OTP واقعی را حدس بزند.

| GET /api/admin/v1/operations/... | permission | رفتار مصرف |
|---|---|---|
| `health` | OperationsRead | SQL/Storage به‌صورت runtime؛ providerها و AI عمدتاً وضعیت تنظیم؛ HTTP200 به‌تنهایی سبزبودن همه موارد نیست |
| `jobs` | OperationsRead | Counts و OldestPendingAtUtc قبلی + Queues تفکیک‌شده با PendingCount/ProcessingCount/FailedCount و سن oldest؛ Scheduled آینده backlog overdue نیست |
| `notifications` | NotificationsRead | PendingCount/ProcessingCount/DeferredCount/FailedCount، OldestQueuedAgeSeconds، availability providerها و RecentFailures؛ پذیرش provider معادل دریافت دستگاه نیست |

FileCleanup=Enabled/Disabled فقط تنظیم را نشان می‌دهد و نه حذف موفق یا مجوز روشن‌کردن آن. مقدار ناشناختهٔ آینده را امن نمایش دهید. reset attempt، purge history یا موفق‌کردن دستی payment/job از این APIها نتیجه نمی‌شود. [worker recovery](../operations/WORKER_RELIABILITY_AND_RECOVERY.md)، [metrics و stale snapshot](../production/OBSERVABILITY.md)، [retention](../operations/RETENTION_AND_CLEANUP_POLICY.md).

بازبینی `2026-10-24` از `main@e525c78` به‌همراه حذف عامل دوم؛ شامل Backoffice، Child Safety و قرارداد عملیات PH است. گزارش‌های STEP15E و GAP-04 تاریخچه‌اند؛ قرارداد جاری ورود فقط OTP/Session/Platform permission است. [مرجع کامل HTTP/DTO](../backend-api-reference.md).

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
3. در `403` دسترسی Platform وجود ندارد؛ نقش سازمانی fallback نیست و جریان Authenticator اجرا نمی‌شود.
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

برای AI منوی مدیریتی، prompt viewer یا دسترسی تازه نسازید. Backend در حوزهٔ مستقل AI دوازده API دارد، اما API مدیریتی Backoffice AI و مجوز عمومی محتوا از این نقش‌ها ایجاد نشده است. real E2E و RAG lifecycle بسته‌شده فرض نشوند؛ اظهارات legacy در STEP 15E متعلق به همان زمان‌اند.

## ورود Platform بدون عامل دوم — ۲۰۲۶-۱۰-۲۴

طبق تصمیم مالک، Authenticator/TOTP، enrollment، Recovery Code، reset و Step-Up از کد فعال حذف شده‌اند؛ bypass جدیدی ساخته نشده است.
جریان مصرف: OTP موجود → Session → `GET /api/admin/v1/me` → در 200 نقش‌ها/مجوزها و پنل؛ در 401 ورود مجدد؛ در 403 عدم دسترسی.
Frontend نباید صفحه یا فراخوانی `/api/admin/v1/mfa/*` و handling عامل دوم داشته باشد. School Manager بدون Platform permission همچنان 403 می‌گیرد.
گزارش GAP-04 سابقهٔ تاریخی است؛ [تصمیم جاری و اثر Migration](PLATFORM_AUTHENTICATION_CHANGE_FA.md) مرجع جدید است.

## الگوی list/detail

فهرست‌های Backoffice از `page` یک‌مبنا و `pageSize` استفاده می‌کنند. مقدار پیش‌فرض معمولاً ۳۰ و حداکثر ۱۰۰ است؛ audit پیش‌فرض ۵۰ دارد. پاسخ متداول شامل `items`, `page`, `pageSize`, `totalCount` است.

- با تغییر filter، page را به ۱ برگردانید.
- sort دلخواه برای همه‌ی فهرست‌ها وجود ندارد. فقط وقتی contract پارامتر `sortBy` دارد از مقدارهای allowlist شده استفاده کنید.
- `OrganizationId`, `ClassId`, `UserId` و سایر شناسه‌های UI selector هستند، نه مدرک مجوز.
- detailهای غیرقابل‌مشاهده ممکن است برای جلوگیری از disclosure با `404` پاسخ داده شوند.

## APIهای نوشتن و confirmation

همهٔ مجوزهای مدیریت، `Reason`، هم‌زمانی، Audit و محافظ آخرین Owner/Manager حفظ شده‌اند؛ Step-Up وجود ندارد.

| عملیات | Permission | Reason |
|---|---|---:|
| grant/revoke نقش پلتفرم | `platform.roles.manage` | بله |
| suspend/reactivate یا ساخت سازمان | `platform.organizations.manage` | بله |
| provisioning با Subscription | `platform.provisioning.manage` | بله |
| تغییر/پایان عضویت و دعوت | `platform.organizations.manage` | بله |
| suspend/reactivate کاربر | `platform.users.manage` | بله |
| revoke نشست | `platform.sessions.manage` | بله |
| ساخت/انتشار PlanVersion و override اشتراک | `platform.commerce.manage` | بله |
| review/resolve/dismiss پرونده | `platform.moderation.review` | بله |
| حذف محتوای case-scoped | review + `platform.moderation.enforce` | بله |

UI همچنان خلاصهٔ اثر، target صریح و Reason را نشان می‌دهد؛ شکست permission مجوز retry خودکار یا fallback به نقش مدرسه نیست.

## stateهای HTTP

همه‌ی خطاهای business به‌شکل ProblemDetails و با extension پایدار `code` برمی‌گردند.

| Status | رفتار UI |
|---:|---|
| `401` | session نامعتبر/منقضی؛ ورود مجدد |
| `403` | permission کافی نیست؛ پیام مجوز و عدم retry خودکار |
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

### افزوده قرارداد GAP-05

جزئیات در [قرارداد ایمنی کودک](../architecture/GAP_05_MESSENGER_CHILD_SAFETY.md) است. detail پرونده `contextMessages` محدود به ۲ قبل/۲ بعد دارد؛ برای مرور عمومی چت استفاده نشود. درخواست `expanded-context` فقط پس از escalation/claim، با permission قوی‌تر، reason، نسخه و نشست معتبر ممکن است. `actions` فقط Warned/MessagingRestricted/Escalated را می‌پذیرد؛ برای محدودیت، پایان UTC لازم است. 422 ایمنی یعنی پیام معمولی منتشر نشده؛ ReviewRequired را Sent نمایش ندهید. `AGE_VERIFICATION_CONFLICT` نیازمند حل Owner است؛ فرم مدرسه نباید retry کور برای overwrite انجام دهد. Frontend یا نقش Manager هیچ راه میان‌بری برای مشاهده private Direct ندارد.

- [تصمیم‌های محصول Backoffice](../architecture/BACKOFFICE_PRODUCT_DECISIONS.md)
- [IAM و Owner](../architecture/STEP_15A_PLATFORM_IAM_OWNER_FOUNDATION.md)
- [Organization/User/Provisioning](../architecture/STEP_15B_ORGANIZATION_USER_PROVISIONING.md)
- [Education/Messaging/Safety](../architecture/STEP_15C_EDUCATION_MESSAGING_PLATFORM_SAFETY.md)
- [Commerce/Finance/Operations](../architecture/STEP_15D_COMMERCE_FINANCE_OPERATIONS.md)
- [پذیرش نهایی](../architecture/STEP_15E_BACKOFFICE_FINAL_ACCEPTANCE.md)

## افزوده‌های Messenger پس از تحویل اولیه

تأیید رسمی اولیه سن و وضعیت self طبق Handoff P0 وجود دارند؛ policy جاری GAP-05، مجوز UsersManage و نشست معتبر ملاک‌اند. availability امن Push می‌تواند Configured/PartiallyConfigured/Unavailable باشد؛ این metadata تحویل واقعی دستگاه نیست. نقش Platform، Guardian یا Manager مجوز مرور Direct خصوصی، presence مخفی یا افشای کلید Quiz نمی‌سازد. [مرزهای جاری Messenger](../messaging/CURRENT_MESSENGER_STATUS.md) و [آمادگی محیط](../production/PRODUCTION_READINESS.md) را کنار کاتالوگ HTTP بخوانید.
