# Observability و عملیات

<!-- CURRENT-REVIEW:BEGIN -->
بازبینی `2026-10-18` از `main@ef67624`؛ [وضعیت جاری](../CURRENT_PROJECT_STATUS.md)، [ماتریس آمادگی](PRODUCTION_READINESS.md) و [runbook IIS](DEPLOYMENT_IIS.md) مبنای gateها هستند. این بازبینی deployment، credential واقعی یا load/restore محیط هدف را اثبات نمی‌کند.
<!-- CURRENT-REVIEW:END -->

## Logging و correlation

سطح پیش‌فرض `Information` با overrideهای `Microsoft.AspNetCore=Warning` و `Microsoft.EntityFrameworkCore.Database.Command=Warning` است؛ این تنظیم به‌تنهایی تضمین حذف exception خام از لاگ نیست.
`X-Correlation-ID` امن با سقف ۶۴ نویسهٔ ASCII شامل حرف/عدد و `-_.` پذیرفته یا از trace جاری تولید می‌شود.
همان مقدار در `HttpContext.TraceIdentifier`، header پاسخ، scope لاگ با نام `CorrelationId`،
tag فعالیت `makan.correlation_id` و `ProblemDetails.correlationId` قرار دارد.
`ProblemDetails.traceId` قرارداد قبلی Activity را حفظ می‌کند؛ correlation ورودی، TraceId استاندارد W3C را overwrite نمی‌کند.

رخداد `RequestCompleted` فقط method استاندارد یا `OTHER`، route template، status واقعی، مدت به میلی‌ثانیه، correlation و trace دارد؛
path/query/body/header خام و شناسهٔ user/organization در این رخداد ثبت نمی‌شوند.
middleware اندازه‌گیری بیرون exception handler است تا status نهایی نگاشت‌شده را ببیند.
برای exception رسیدگی‌شده، template از `IExceptionHandlerFeature.Endpoint` بازیابی می‌شود؛ پاک‌شدن endpoint جاری نباید خطای route را `unmatched` کند.

### سیاست محدود exception و مرز مسئولیت آن

در .NET 9، framework پیش از handler برنامه exception اصلی را log می‌کند؛ سطح `Warning` جلوی رخداد `Error` را نمی‌گیرد.
با تأیید صریح مالک، `ProductionLoggingSafety` خانوادهٔ
`Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware` و پنج category مشخص EF را در
Production/Staging از خروجی خام منع می‌کند: `Microsoft.EntityFrameworkCore.Database.Command`،
`Database.Connection`، `Database.Transaction`، `Query` و `Update` با همان پیشوند EF.
قاعدهٔ category در framework از نوع prefix است؛ override دقیق‌ترِ provider یا زیرشاخه نیز
حفاظت همین مسیرها را دور نمی‌زند. تمام namespace EF، Data Protection، Health یا AI HTTP
خاموش نشده‌اند؛ برای نمونه categoryهای Migrations و Model دست‌نخورده‌اند. Development تغییر نکرده است.

برای EF، صرف `Warning` یا خاموش‌کردن logger کافی نیست: `EfCoreLoggingSafety` از callback مستقل
`LogTo` با `EventData` استفاده می‌کند، نه formatter یا `ToString` دادهٔ خام. رخدادهای Warning و بالاتر
در پنج مسیر مشخص، با category محدود، شناسهٔ عددی رخداد EF، کد ثابت، correlation/trace در صورت وجود
و سطح مناسب بازثبت می‌شوند؛ وجود exception سطح را دست‌کم Error می‌کند. command text، state،
پارامتر، exception object/message/inner/stack، connection string و مسیر در رخداد جایگزین نیستند.
`WarningBehavior.Ignore` استفاده نشده است؛ exception اصلی همچنان به caller می‌رسد و semantics
query/transaction/retry تغییر نمی‌کند. جریان بدون Activity، شناسهٔ HTTP ساختگی تولید نمی‌کند.
رخداد امن `UnexpectedError` با کد ثابت `UNEXPECTED_ERROR`، status 500، correlation و trace، بدون exception object/message/inner/stack،
همراه counter خطا باقی می‌ماند. نگاشت status/code خطاهای شناخته‌شده تغییر نکرده است.
خطای شناخته‌شده نیز رخداد امن دارد: 5xx در سطح Error و رد درخواست 4xx در سطح Warning؛
همان status و code معتبر ثبت می‌شوند، نه متن exception. catalog امن یک‌بار از ثابت‌های عمومی
همان ۱۴ کلاس ErrorCodes متعلق به exceptionهای رسیدگی‌شده ساخته می‌شود؛ اسکن assembly یا اعتماد
به شکل رشته انجام نمی‌شود. کدهای معتبر با mapping پیش‌فرض 400، مثل `OTP_INVALID`، نیز حفظ می‌شوند.
اگر Code ناشناخته و بالقوه
حساس باشد، فقط لاگ از `UNRECOGNIZED_ERROR` استفاده می‌کند؛ پاسخ و قرارداد قدیمی آن تغییر نمی‌کند.
اگر writer به دلیل `Accept` نامنطبق پاسخ ندهد، fallback همچنان ProblemDetails امن با `traceId` قدیمی و `correlationId` یکسان می‌نویسد.

اگر exception از handler بیرون بماند، مثلاً پس از شروع response، مرز بیرونی خطا را نمی‌بلعد و اتصال را خودسرانه abort نمی‌کند.
رخداد ثابت `RequestPipelineFailure` و metric جدا ثبت می‌شوند و host یک `SanitizedRequestFailureException` جدید با پیام ثابت
و بدون inner exception می‌گیرد؛ `StackTrace` تهی و `ToString` متن ثابت دارند، پس stack جدید
این exception نیز مسیر source یا internals middleware را وارد لاگ host نمی‌کند.
رخداد failure خود status واقعی، correlation و trace دارد؛ خطا به success تبدیل نمی‌شود.
status قبلاً ارسال‌شده، مثلاً 200، حفظ می‌شود؛ failure آن را نباید به 500 ساختگی تبدیل کرد.
با cancellation درخواست، حتی اگر provider نوع دیگری از exception بدهد، فقط `OperationCanceledException` تازه با همان token و متن ثابت منتقل می‌شود.

تست‌های واقعی pipeline و مرز مستقیم، خطای شناخته‌شدهٔ 5xx/4xx، Code ناشناخته، exceptionهای مصنوعی
دارای دادهٔ محرمانه، override provider، Development diagnostics، خطای واقعی EF روی SQL تست ایزوله،
SQL readiness و response شروع‌شده را بررسی می‌کنند؛
این پوشش تضمین پاک‌سازی عمومی تمام loggerهای framework یا هر تنظیم collector آینده نیست.
برای عیب‌یابی هرگز logging خام HTTP/EF را با secret واقعی افزایش ندهید؛ `EnableSensitiveDataLogging` فعال نشود.
collector آینده نباید raw exception از DiagnosticSource، request/response body، header یا exception خودکار را بدون سیاست جدا جمع‌آوری کند.
منبع رفتار نسخهٔ ۹: [کد رسمی ExceptionHandlerMiddlewareImpl](https://github.com/dotnet/aspnetcore/blob/v9.0.5/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs).
قرارداد callback دادهٔ ساختاریافتهٔ EF: [LogTo با EventData در EF Core 9](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder.logto?view=efcore-9.0).

با فعال شدن Hosting:AllowInsecureHttp یا Database:AllowUntrustedServerCertificate در Production، startup هشدار ثابت و بدون مقدار secret ثبت می‌کند. در انتشار فعلی این دو استثنا تأییدشده‌اند؛ دیدن هشدار به معنی TLS امن یا سلامت SQL نیست. Event Viewer/IIS برای خطای startup بررسی شود؛ چاپ appsettings.Production.json یا URL ملی‌پیامک برای عیب‌یابی مجاز نیست.

هیچ log جدیدی نباید OTP، access/refresh token، شماره کامل، متن private chat، moderation evidence، payment secret/reference خام، push token، AI payload یا connection string را ثبت کند. شناسه‌های فنی کنترل‌شده برای عیب‌یابی مجازند.

## Metrics و tracing

Meter مستقل از vendor با نام `MakanApp.Api` این instrumentها را منتشر می‌کند:

- `makan.http.server.requests`
- `makan.http.server.errors`
- `makan.http.server.duration`
- `makan.http.server.failures`: failure عبوری از مرز pipeline با `outcome` محدود `aborted`، `unhandled` یا `cancelled`؛ مستقل از status واقعی wire.

labelها فقط method، route template و status class هستند؛ `UserId`، phone، message/order ID label نمی‌شوند. ASP.NET Core Activity/trace context مبنای tracing است. exporter/collector (OTLP یا agent سازمان) باید در staging/Production در لایه‌ی deployment متصل شود و در source به vendor خاص قفل نشده است.

### worker و snapshot عملیاتی

Meter و ActivitySource با نام `MakanApp.Workers` مستقل از vendor هستند:

| Instrument | معنا و label محدود |
|---|---|
| `makan.worker.batches` | تعداد batch، `worker` از فهرست ثابت |
| `makan.worker.items` | تعداد مورد claim/پردازش‌شدهٔ گزارش‌شده توسط worker؛ اثبات تحویل خارجی نیست |
| `makan.worker.failures` | شکست با `worker` و `stage` محدود |
| `makan.worker.outcomes` | transition تأییدشده با `worker` و `outcome` محدود؛ claim با completion یکی نیست |
| `makan.worker.duration` | مدت batch به `ms` |
| `makan.file_cleanup.results` | PH-6؛ Examined/Deleted/AlreadyMissing/Skipped/Failed/Other با label محدود outcome؛ حذف SQL row نیست |
| `makan.queue.items` | تعداد در snapshot با `queue` و `state` محدود |
| `makan.queue.oldest_age` | سن قدیمی‌ترین pending به ثانیه، نه شناسهٔ مورد |
| `makan.operational.snapshot.available` | آخرین دریافت snapshot موفق `1` یا ناموفق `0` |
| `makan.operational.snapshot.age` | سن snapshot؛ قدیمی‌شدن داده باید کنار مقدار صف دیده شود |

نام صف‌ها `IntegrationOutbox`، `MessagingRealtimeOutbox`، `ScheduledMessage`، `ReportExport` و `NotificationDelivery` است؛
نام ناشناخته به `Other` تبدیل می‌شود. callbackهای gauge فقط حافظه می‌خوانند، نه SQL یا provider.
publisher با `Observability:OperationalMetrics:SnapshotIntervalSeconds` (پیش‌فرض ۳۰، محدودهٔ ۵ تا ۳۶۰۰ ثانیه)
و بودجهٔ ده‌ثانیه‌ای snapshot می‌گیرد. failure مقدار موفق قبلی را با سن افزاینده و available صفر نگه می‌دارد؛
پیش از اولین موفقیت، صف خالی و سلامت ساختگی منتشر نمی‌شود.
این snapshot می‌تواند برای چند ثانیه از SQL عقب باشد؛ SQL و endpoint محافظت‌شده مرجع جاری‌اند.

در ممیزی مجدد PH-4، outcome فقط یکی از `Claimed`، `Completed`، `RetryScheduled`، `Failed`،
`LeaseRecovered`، `Deferred`، `Suppressed`، `LeaseLost` یا `Other` است؛ count غیرمثبت ثبت نمی‌شود.
transition پایدار فقط پس از acknowledgement موفق commit شمرده می‌شود. `LeaseLost` نتیجهٔ guard جاری است،
نه شکست تجاری تازه؛ recovery تعداد leaseهای منقضیِ واقعاً دوباره claim‌شده است، نه هر poll صف.
گم‌شدن acknowledgement می‌تواند باعث کم‌شماری شود؛ metric جای ledger SQL و deduplication نیست.
`Completed` در Notification به معنی پذیرش provider است، نه دریافت دستگاه. هیچ job/message/user ID
یا کد خطای آزاد به این labelها اضافه نشده است. failed terminal/repair و خطای schema با کد ثابت و
سطح Error ثبت می‌شوند؛ payload و exception خام در این signalها وجود ندارند.

وجود instrument، به معنی نصب collector یا دریافت alert نیست:

```text
OBSERVABILITY_EXPORTER = PENDING_INFRASTRUCTURE
ALERT_DELIVERY = PENDING_INFRASTRUCTURE
```

## Dashboard و alert پیشنهادی

- نرخ 5xx و p50/p95/p99 latency بر route template.
- SQL/readiness failure.
- سن و تعداد Outbox، notification delivery، report export و realtime pending.
- تعداد attemptهای رسیده به سقف و provider unavailable.
- payment unknown/failed، moderation queue و safety unavailable.
- SignalR connection count در صورت افزودن instrument کنترل‌شده در مرحله‌ی بعد.

thresholdها باید بعد از baseline staging تعیین شوند و SLA محصول در این سند اختراع نشده است.

### قرارداد Alert در PH-7

جدول زیر قرارداد signal و اقدام است، نه ادعای استقرار alert manager. category آستانه باید با
baseline، حداقل traffic، window، maintenance و owner escalation در سامانهٔ monitoring مقداردهی شود؛
این اعداد در Domain یا منطق worker hard-code نمی‌شوند. collector باید meterهای `MakanApp.Api`
و `MakanApp.Workers` را صریح subscribe کند؛ این نسخه endpoint عمومی `/metrics` یا exporter تازه ندارد.

| Signal واقعی/منبع | معنا | دستهٔ آستانهٔ آغازین | Severity | اقدام اپراتور / Runbook |
|---|---|---|---|---|
| `/health/live` از probe مستقل | process قابل دسترسی نیست | استمرار خارج maintenance | CRITICAL | Hosting Bundle، App Pool، binding و Event Viewer؛ [IIS Runtime](DEPLOYMENT_IIS.md#قرارداد-runtime-و-shutdown-در-ph-5) |
| `/health/ready` برابر 503 | core آماده نیست | استمرار failure core | CRITICAL | config/ring/SQL/storage؛ بخش Health همین سند؛ ring را برای سبزکردن probe بازسازی نکنید |
| خطای SQL در health محافظت‌شده | اتصال core ناموفق | failure همراه readiness و retry مشاهده‌شده | CRITICAL | شبکه/گواهی/principal/SQL؛ اتصال محرمانه چاپ نشود؛ [Configuration](../operations/PRODUCTION_CONFIGURATION_RUNBOOK.md) |
| نسبت `makan.http.server.errors/requests` | wire 5xx واقعی | افزایش نسبت با حداقل traffic/window | CRITICAL | route template و correlation امن؛ [معیار rollback](DEPLOYMENT_IIS.md#gate-شکست-و-rollback) |
| `makan.http.server.failures`؛ aborted/unhandled | failure عبوری، حتی پس از wire 200 | افزایش غیرمنتظرهٔ پایدار | CRITICAL | failure را از status موفق جدا ببینید؛ cancellation به‌تنهایی failure تجاری نیست؛ بخش Logging |
| histogram مدت request به ms | latency | p95/p99 نسبت baseline هر route | WARNING | SQL/load/بازهٔ استقرار؛ SLA عددی از این جدول نتیجه نشود |
| snapshot.available و snapshot.age به s | اعتبار دادهٔ صف | unavailable یا سن بیش از interval+budget+tolerance مصوب | WARNING | publisher/SQL را بررسی کنید؛ صف قدیمی، صفر یا سالم فرض نشود |
| pending و oldest_age صف Outbox/Realtime/Report | عقب‌افتادگی durable | رشد پایدار نسبت حد هر صف، فقط snapshot تازه | WARNING | jobs محافظت‌شده و backoff؛ [Recovery](../operations/WORKER_RELIABILITY_AND_RECOVERY.md#عیب‌یابی-و-اقدام-اپراتور) |
| Failed/exhausted در صف‌ها | کار نهایی ناموفق | افزایش جدید/بدون acknowledgement، نه count تاریخی ثابت | CRITICAL | triage کد ثابت؛ reset attempt/purge/replay خودکار ممنوع؛ بخش Recovery |
| Pending/Deferred/Failed اعلان | backlog یا شکست delivery | failure جدید و رشد ناموجه با context defer/provider | WARNING | Quiet Hours و زمان آینده را جدا کنید؛ پذیرش provider رسید دستگاه نیست؛ بخش worker همین سند |
| Scheduled pending و oldest_age | intent موعدگذشته، نه آینده | رشد due backlog نسبت حد مصوب | WARNING | ساعت UTC/مجوز جاری/Safety/lease؛ intent آینده در این شمارش due نیست |
| `makan.worker.failures` با stage ثابت | شکست اجرای worker | استمرار همراه backlog/core health | WARNING | Loop/Claim/Process/Snapshot را تفکیک کنید؛ اسم/ID پیام label نشود |
| وضعیت AI/Push در operations و signalهای adapter موجود | configured/degraded، نه probe قطعی شبکه | الگوی failure همان integration، مستقل از readiness core | WARNING | runbook همان provider؛ تست endpoint/device واقعی جدا؛ secret/body استخراج نشود |
| شکست cleanup، فقط اگر صریحاً Enabled | بازیابی bytes terminal ناموفق | failure تکراری نسبت batch بررسی‌شده | WARNING | [Retention](../operations/RETENTION_AND_CLEANUP_POLICY.md)؛ خاموش‌بودن cleanup failure نیست |
| restart/rapid-fail و disk pressure از OS/IIS | ظرفیت/چرخهٔ host | حد مصوب زیرساخت | CRITICAL | مانیتور OS/IIS لازم است؛ backend gauge متناظر ندارد؛ [IIS](DEPLOYMENT_IIS.md) |
| سن backup، شکست job/off-server copy از SQL/OS | نقض برنامهٔ recovery | RPO مصوب/شکست job | CRITICAL | [Backup/Restore](BACKUP_RESTORE.md)؛ VERIFYONLY جای drill نیست |
| lifecycle اطلاع‌رسانی deploy/rotation مصوب | تغییر کنترل‌شده | event قابل ثبت اپراتور | INFO | ثبت UTC، commit و reference شواهد؛ نه dump secret/config |

`/api/admin/v1/operations/health` و `/jobs` مجوز `OperationsRead` و `/notifications`
مجوز `NotificationsRead` دارند؛ collector عمومی نباید با حذف Authentication دادهٔ غنی بگیرد.
وضعیت Configured، اتصال AI، سلامت credential یا تحویل Push را ثابت نمی‌کند. پیشنهاد alert روی
payment/moderation/Safety نیز instrument اختصاصی تازه ایجادشده نامیده نمی‌شود.

برای availability واقعی AI/Push، PH probe شبکهٔ تازه‌ای نساخته است؛ status تنظیمی یا failure
محدود integration فقط ورودی triage است. alert قطعی unavailable به probe مجاز/شاهد runtime
در track همان integration یا زیرساخت نیاز دارد و بدون آن PASS اعلام نمی‌شود؛ readiness core مستقل است.

snapshot notification قدیمی‌ترین Pending **و Deferred** را در نظر می‌گیرد؛ Quiet Hours یا defer
آینده می‌تواند سن بالا داشته باشد و به‌تنهایی overdue نیست. backoff Outbox در pending حساب می‌شود.
در recycle دو process ممکن است همان gauge SQL را منتشر کنند؛ gauge صف را بین instanceها `sum`
نکنید. انتخاب instance یا `max`/dedup با سیاست collector انجام شود؛ counter تلاش‌ها معنای دیگری دارد
و با restart از نو آغاز می‌شود. واحد duration برابر ms و واحد age برابر s است.

برای acceptance، probe failure و یک signal مصنوعی بی‌خطر را در محیط مجاز تا **دریافت واقعی**
notification اپراتور دنبال کنید؛ ایجاد dashboard یا log، شاهد دریافت alert نیست.
بدون این اجرا، `OBSERVABILITY_EXPORTER=PENDING_INFRASTRUCTURE` و
`ALERT_DELIVERY=PENDING_INFRASTRUCTURE` باقی می‌مانند. هیچ endpoint health تستی برای fail کردن
Production، قطعی عمدی SQL یا ارسال secret روی HTTP اضافه نشده است.

## Health

- `/health/live`: فقط زنده‌بودن process.
- `/health/ready`: اعتبار config لازم Production، ring پایدار همان provider، اتصال SQL Server و قابلیت write/delete فایل مصنوعی روی storage. timeout کل check پانزده ثانیه است؛ cancellation منتقل می‌شود.
- `/health`: alias کم‌جزئیات liveness برای سازگاری.

خروجی عمومی فقط Healthy/Unhealthy است و connection string، path، payload provider یا stack trace منتشر نمی‌کند. جزئیات عملیات موجود از endpointهای permissionدار Backoffice خوانده می‌شود.

بخش SQL این probe، اتصال را می‌سنجد؛ بررسی schema کامل، مجوز تمام queryهای کسب‌وکاری و
smoke هر ماژول نیست. history/version migration، runtime principal و core smoke مجاز همچنان
gateهای rollout مستقل‌اند؛ health 200 آن‌ها یا دریافت provider را اثبات نمی‌کند.

### قرارداد health پس از PH-2

`/health/live` و alias قدیمی `/health` به SQL، دیسک، AI، Push یا payment وابسته نیستند.
سه route عمومی health نشست نمی‌خوانند؛ حتی Bearer اشتباهیِ فرستاده‌شده به probe،
احراز هویت وابسته به SQL را فعال نمی‌کند. این استثنا فقط metadata همین routeهاست، نه bypass APIهای محافظت‌شده.
`/health/ready` برای شکست وابستگی core، HTTP 503 و فقط `Unhealthy` می‌دهد؛ exception فنی
در `HealthCheckResult` قرار نمی‌گیرد. نبود runtime کلید/پوشه نباید با cache یا ایجاد ring جدید پنهان شود.
در Development/Testing فقط الزام ring عملیاتی استثنا دارد؛ این محیط‌ها روش استقرار Production نیستند.

`GET /api/admin/v1/operations/health` با session معتبر و permission فعلی `OperationsRead`
جزئیات امن core و تنظیم providerهای اختیاری را برمی‌گرداند؛ permission تغییر نکرده و endpoint جدید نداریم.
AI یا Push با وضعیت `Configured`/`Configured/RuntimeNotProbed`، شبکهٔ سالم یا دریافت واقعی دستگاه
اعلام نمی‌شوند. failure این providerها readiness کل Backend را gate نمی‌کند. `FileCleanup`
در PH-6 از NotImplemented به Disabled/Enabled **تنظیمی** تغییر می‌کند؛ پیش‌فرض Disabled است.
Enabled، query موفق، حذف واقعی یا تصویب دورهٔ حقوقی را اثبات نمی‌کند؛
[سیاست cleanup](../operations/RETENTION_AND_CLEANUP_POLICY.md) مرجع محدودیت‌هاست.

`GET /api/admin/v1/operations/jobs` همان permission را حفظ می‌کند. فیلد افزودهٔ `Queues`
تعداد pending (شامل انتظار backoff)، lease فعال، exhausted/failed و زمان/سن قدیمی‌ترین pending را
برای نوع‌های ثابت صف می‌دهد؛ payload، destination، path و credential منتشر نمی‌شوند.
آیتم outbox در سقف attempt فقط پس از نبود/پایان lease، Failed است؛ آیتم در حال پردازش
به‌اشتباه poison اعلام نمی‌شود. intent زمان‌بندی‌شدهٔ آینده در statusهای کل دیده می‌شود،
ولی در backlog due و oldest overdue نمی‌آید. این endpoint ابزار خواندن است، نه retry/purge عمومی.

## worker و hintهای Messenger

علاوه بر Outbox، backlog/age/failure و recovery پیام‌های Scheduled و NotificationDelivery پایش شوند؛ registration/token/endpoint کامل Push و محتوای پیام log نشوند. Typing/Presence hint گذرا هستند و مسیر audit پیام دائمی نیستند. موفقیت provider با رسیدن به دستگاه تفاوت دارد؛ gate runtime و sample کاربران آزمایشی جدا ثبت شود.
