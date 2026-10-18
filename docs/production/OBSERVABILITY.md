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
با تأیید صریح مالک، `ProductionLoggingSafety` فقط خانوادهٔ category با نام
`Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware` را در Production/Staging محدود می‌کند.
قاعدهٔ category در framework از نوع prefix است؛ فهرست گستردهٔ EF، Data Protection، Health یا AI HTTP خاموش نشده است.
override اختصاصی provider نیز این category را دوباره فعال نمی‌کند. Development تغییر نکرده است.
رخداد امن `UnexpectedError` با کد ثابت `UNEXPECTED_ERROR`، status 500، correlation و trace، بدون exception object/message/inner/stack،
همراه counter خطا باقی می‌ماند. نگاشت status/code خطاهای شناخته‌شده تغییر نکرده است.
اگر writer به دلیل `Accept` نامنطبق پاسخ ندهد، fallback همچنان ProblemDetails امن با `traceId` قدیمی و `correlationId` یکسان می‌نویسد.

اگر exception از handler بیرون بماند، مثلاً پس از شروع response، مرز بیرونی خطا را نمی‌بلعد و اتصال را خودسرانه abort نمی‌کند.
رخداد ثابت `RequestPipelineFailure` و metric جدا ثبت می‌شوند و host یک `SanitizedRequestFailureException` جدید با پیام ثابت
و بدون inner exception می‌گیرد؛ داده و stack اصلی به logger IIS/Kestrel منتقل نمی‌شوند.
status قبلاً ارسال‌شده، مثلاً 200، حفظ می‌شود؛ failure آن را نباید به 500 ساختگی تبدیل کرد.
با cancellation درخواست، حتی اگر provider نوع دیگری از exception بدهد، فقط `OperationCanceledException` تازه با همان token و متن ثابت منتقل می‌شود.

تست‌های واقعی pipeline و مرز مستقیم، exceptionهای مصنوعی دارای دادهٔ محرمانه، override provider، SQL readiness و response شروع‌شده را بررسی می‌کنند؛
این پوشش تضمین پاک‌سازی عمومی تمام loggerهای framework یا هر تنظیم collector آینده نیست.
برای عیب‌یابی هرگز logging خام HTTP/EF را با secret واقعی افزایش ندهید؛ `EnableSensitiveDataLogging` فعال نشود.
collector آینده نباید raw exception از DiagnosticSource، request/response body، header یا exception خودکار را بدون سیاست جدا جمع‌آوری کند.
منبع رفتار نسخهٔ ۹: [کد رسمی ExceptionHandlerMiddlewareImpl](https://github.com/dotnet/aspnetcore/blob/v9.0.5/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs).

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
| `makan.worker.duration` | مدت batch به `ms` |
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

## Health

- `/health/live`: فقط زنده‌بودن process.
- `/health/ready`: اعتبار config لازم Production، ring پایدار همان provider، اتصال SQL Server و قابلیت write/delete فایل مصنوعی روی storage. timeout کل check پانزده ثانیه است؛ cancellation منتقل می‌شود.
- `/health`: alias کم‌جزئیات liveness برای سازگاری.

خروجی عمومی فقط Healthy/Unhealthy است و connection string، path، payload provider یا stack trace منتشر نمی‌کند. جزئیات عملیات موجود از endpointهای permissionدار Backoffice خوانده می‌شود.

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
تا ایجاد کنترل محدود در PH-6 صریحاً `NotImplemented` است.

`GET /api/admin/v1/operations/jobs` همان permission را حفظ می‌کند. فیلد افزودهٔ `Queues`
تعداد pending (شامل انتظار backoff)، lease فعال، exhausted/failed و زمان/سن قدیمی‌ترین pending را
برای نوع‌های ثابت صف می‌دهد؛ payload، destination، path و credential منتشر نمی‌شوند.
آیتم outbox در سقف attempt فقط پس از نبود/پایان lease، Failed است؛ آیتم در حال پردازش
به‌اشتباه poison اعلام نمی‌شود. intent زمان‌بندی‌شدهٔ آینده در statusهای کل دیده می‌شود،
ولی در backlog due و oldest overdue نمی‌آید. این endpoint ابزار خواندن است، نه retry/purge عمومی.

## worker و hintهای Messenger

علاوه بر Outbox، backlog/age/failure و recovery پیام‌های Scheduled و NotificationDelivery پایش شوند؛ registration/token/endpoint کامل Push و محتوای پیام log نشوند. Typing/Presence hint گذرا هستند و مسیر audit پیام دائمی نیستند. موفقیت provider با رسیدن به دستگاه تفاوت دارد؛ gate runtime و sample کاربران آزمایشی جدا ثبت شود.
