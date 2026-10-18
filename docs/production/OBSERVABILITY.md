# Observability و عملیات

<!-- CURRENT-REVIEW:BEGIN -->
بازبینی `2026-10-18` از `main@ef67624`؛ [وضعیت جاری](../CURRENT_PROJECT_STATUS.md)، [ماتریس آمادگی](PRODUCTION_READINESS.md) و [runbook IIS](DEPLOYMENT_IIS.md) مبنای gateها هستند. این بازبینی deployment، credential واقعی یا load/restore محیط هدف را اثبات نمی‌کند.
<!-- CURRENT-REVIEW:END -->

## Logging و correlation

logها structured هستند و سطح Production پیش‌فرض `Information` با overrideهای `Microsoft.AspNetCore=Warning` و `Microsoft.EntityFrameworkCore.Database.Command=Warning` است. `X-Correlation-ID` امن با سقف ۶۴ نویسه پذیرفته یا از trace جاری تولید می‌شود و در پاسخ و `ProblemDetails.traceId` برمی‌گردد.

با فعال شدن Hosting:AllowInsecureHttp یا Database:AllowUntrustedServerCertificate در Production، startup هشدار ثابت و بدون مقدار secret ثبت می‌کند. در انتشار فعلی این دو استثنا تأییدشده‌اند؛ دیدن هشدار به معنی TLS امن یا سلامت SQL نیست. Event Viewer/IIS برای خطای startup بررسی شود؛ چاپ appsettings.Production.json یا URL ملی‌پیامک برای عیب‌یابی مجاز نیست.

هیچ log جدیدی نباید OTP، access/refresh token، شماره کامل، متن private chat، moderation evidence، payment secret/reference خام، push token، AI payload یا connection string را ثبت کند. شناسه‌های فنی کنترل‌شده برای عیب‌یابی مجازند.

## Metrics و tracing

Meter مستقل از vendor با نام `MakanApp.Api` این instrumentها را منتشر می‌کند:

- `makan.http.server.requests`
- `makan.http.server.errors`
- `makan.http.server.duration`

labelها فقط method، route template و status class هستند؛ `UserId`، phone، message/order ID label نمی‌شوند. ASP.NET Core Activity/trace context مبنای tracing است. exporter/collector (OTLP یا agent سازمان) باید در staging/Production در لایه‌ی deployment متصل شود و در source به vendor خاص قفل نشده است.

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
- `/health/ready`: اتصال SQL Server و قابلیت write/delete probe روی storage.
- `/health`: alias کم‌جزئیات liveness برای سازگاری.

خروجی عمومی فقط Healthy/Unhealthy است و connection string، path، payload provider یا stack trace منتشر نمی‌کند. جزئیات عملیات موجود از endpointهای permissionدار Backoffice خوانده می‌شود.

## worker و hintهای Messenger

علاوه بر Outbox، backlog/age/failure و recovery پیام‌های Scheduled و NotificationDelivery پایش شوند؛ registration/token/endpoint کامل Push و محتوای پیام log نشوند. Typing/Presence hint گذرا هستند و مسیر audit پیام دائمی نیستند. موفقیت provider با رسیدن به دستگاه تفاوت دارد؛ gate runtime و sample کاربران آزمایشی جدا ثبت شود.
