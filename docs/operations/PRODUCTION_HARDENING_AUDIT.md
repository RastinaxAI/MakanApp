# ممیزی سخت‌سازی Production — PH-0

تاریخ: 2026-10-18. مبنا: کد واقعی `origin/main` در commit
`0f5d861f382ac41c81b15406c92db9c181acf88c`؛ این سند شاهد اجرای سرور نیست.
Worktree مستقل: `MakanApp-Backend-Production-Hardening`، branch:
`operations/production-hardening` با upstream برابر `origin/main`.
workspace قدیمی و dirty مربوط به AI و worktree پیام‌رسان تغییر نکرده‌اند.

## روش و معنای وضعیت‌ها

کد، migration و تست‌های موجود مبنای پیاده‌سازی‌اند؛ ادعای اسناد تاریخی به‌تنهایی کافی نیست.
`IMPLEMENTED`: رفتار در کد/تست موجود است؛ `PARTIAL`: بخش مهمی هنوز ناقص است؛
`MISSING`: در کد وجود ندارد؛ `OPERATIONAL_ONLY`: اثبات نیازمند اقدام اپراتور است؛
`BLOCKED_BY_INFRA`: زیرساخت فعلی برای اثبات فراهم نیست؛
`DEFERRED_BY_POLICY`: تصمیم محصول/حقوقی لازم است. وضعیت‌ها مربوط به **مبنای PH-0** هستند؛
نتیجهٔ اصلاحات بعدی در بخش پیگیری ثبت می‌شود و مبنای تاریخی بازنویسی نمی‌شود.

## Validation مبنا

- `dotnet restore MakanApp.sln`: موفق؛ هر ۷ پروژه.
- `dotnet build MakanApp.sln --configuration Debug --nologo`: موفق؛ ۰ warning، ۰ error.
- `dotnet test MakanApp.sln --configuration Debug --no-build --nologo`: موفق؛
  Unit: **606**، Integration: **1034**، Architecture: **14**، مجموع: **1654**؛
  Failed: **0**، Skipped: **0**. فایل‌های TRX در مسیر ignored
  `.artifacts/production-hardening/ph0-baseline` نگهداری شده‌اند.
- تست Integration از SQL Server آزمایشی ایزوله استفاده می‌کند؛ دیتابیس عادی توسعه/Production هدف نیست.

## ماتریس ۴۵ حوزه

| # | حوزه | وضعیت مبنا | شاهد و فاصلهٔ واقعی |
|---|---|---|---|
| 1 | Data Protection | PARTIAL | `Api/Configuration/BackofficeMfaConfiguration.cs` یک registration سراسری دارد؛ نام ثابت `MakanApp.BackofficeMfa.v1` و purpose فعلی MFA باید حفظ شوند. |
| 2 | ماندگاری کلید | PARTIAL | مسیر خارجی، PFX یا DPAPI کاربر جاری پشتیبانی می‌شود؛ probe زمان startup و ApplicationName قابل‌پیکربندی وجود ندارد. بدون ring در Production، MFA fail-closed است. |
| 3 | secretهای Production | IMPLEMENTED | secret از configuration دریافت می‌شود؛ Production واقعی ignored و نمونهٔ اشتراکی خالی است. این بررسی، اثبات پاک‌بودن تمام تاریخچهٔ قدیمی یا تنظیم صحیح سرور نیست. |
| 4 | appsettings Production | IMPLEMENTED | `ProductionConfigurationValidator` و نمونهٔ Production؛ کنترل SQL/TLS، Storage، Identity، Hosts، CORS و opt-in استثناهای مصوب وجود دارد. |
| 5 | Connection String | IMPLEMENTED | SQL Server و کنترل TLS؛ exception اعتماد به certificate فقط با opt-in Production. مقدار واقعی در این ممیزی خوانده/منتشر نشده است. |
| 6 | MeliPayamak/OTP | IMPLEMENTED | provider و config فعلی حفظ می‌شوند؛ secret نمونه خالی است. هیچ تست ارسال واقعی یا تغییر provider در PH انجام نمی‌شود. |
| 7 | AI secrets/config | IMPLEMENTED | configuration موجود reuse می‌شود؛ AI اختیاری است. audit این فاز مجوز تغییر قرارداد، RAG یا اجرای واقعی AI نیست. |
| 8 | Push secrets/config | IMPLEMENTED | Web Push/FCM اختیاری و credentialها بیرون از source؛ دریافت واقعی دستگاه و credential عملیاتی اثبات نشده است. |
| 9 | logging configuration | PARTIAL | categoryهای پایه موجودند؛ request completion ساختاریافته و سیاست یکپارچهٔ خطا/worker ناقص است. |
| 10 | correlation | PARTIAL | `CorrelationIdMiddleware` ورودی امن ۶۴ کاراکتری و response header دارد؛ logging scope و Activity tag ندارد. |
| 11 | trace identifier | PARTIAL | Activity trace و correlation موجود دو مفهوم متفاوت‌اند؛ ProblemDetails باید هر دو را صریح و سازگار بیان کند. |
| 12 | GlobalExceptionHandler | PARTIAL | mapping و error code پایدار دارد؛ unexpected exception خام به logger داده می‌شود. |
| 13 | ProblemDetails | IMPLEMENTED | پاسخ عمومی generic برای unexpected و mappingهای فعلی وجود دارد؛ قراردادها حفظ می‌شوند. |
| 14 | نشت خطای Production | PARTIAL | بدنهٔ پاسخ sanitized است، اما logger handler و framework ممکن است message/inner exception محرمانه را ثبت کنند. |
| 15 | Health endpoints | IMPLEMENTED | `/health` و `/health/live` فقط process؛ `/health/ready` برای dependencyهای core. |
| 16 | Readiness | PARTIAL | SQL و storage بررسی می‌شوند؛ دسترسی جاری به ring و اعتبار config لازم باید افزوده شوند. |
| 17 | SQL health | IMPLEMENTED | `CoreReadinessHealthCheck` از `CanConnectAsync` استفاده می‌کند؛ public response حداقلی است. تست outage صریح لازم است. |
| 18 | Storage health | PARTIAL | `LocalFileStorage` probe نوشتن/حذف دارد؛ شکست حذف نیز باید پاسخ امن ایجاد کند. |
| 19 | dependency اختیاری | PARTIAL | AI/Push/payment readiness را gate نمی‌کنند؛ diagnostics محافظت‌شده باید «پیکربندی‌شده» را از «شبکهٔ سالم اثبات‌شده» جدا کند. |
| 20 | Outbox | PARTIAL | `OutboxDispatcher`: SQL claim و token، batch50/lease30s؛ preclaim کل batch و crash در آخرین attempt نیازمند اصلاح است. |
| 21 | Notification/Push worker | PARTIAL | claim تک‌به‌تک، token، timeout، retry و recovery وجود دارند؛ completion مستقل باید زمان محدود داشته باشد. external delivery فقط at-least-once است. |
| 22 | Scheduled Message worker | IMPLEMENTED | claim تک‌به‌تک، token/expiry، بازبینی permission/safety، message+receipt+outbox اتمیک و تست race/recovery موجود است. |
| 23 | cleanup worker | MISSING | worker cleanup ثبت نشده؛ expiry فایل صرفاً metadata است. generic business purge مجاز نیست. |
| 24 | retry/backoff | PARTIAL | همهٔ workerهای durable retry محدود دارند؛ بهبود repair نهایی و visibility لازم است. |
| 25 | lease | PARTIAL | notification/scheduled fencing مناسب دارند؛ realtime بدون token و report بدون renewal/timeout کافی است. |
| 26 | poison/failure | PARTIAL | terminal delivery/scheduled/report وجود دارد؛ outbox exhausted ممکن است به‌اشتباه Pending شمرده شود و claim باقی بماند. |
| 27 | crash recovery | PARTIAL | expiry/reclaim موجود؛ report output پیش از ذخیرهٔ terminal DB می‌تواند توسط worker قدیمی دست‌کاری شود. |
| 28 | graceful shutdown | PARTIAL | cancellation در ۶ loop وجود دارد؛ برخی saveها `CancellationToken.None` و timeout صریح host/ANCM ندارند. |
| 29 | IIS recycle | OPERATIONAL_ONLY | دستورالعمل پایه موجود؛ اثبات recycle واقعی انجام نشده است. |
| 30 | overlapped recycle | PARTIAL | SQL durable leases مبنا هستند؛ stale worker report/realtime و preclaim باید تست/اصلاح شوند. |
| 31 | SignalR single-node | IMPLEMENTED | ephemeral state و rate limiter در حافظه؛ بدون backplane. AppPool باید یک worker داشته باشد؛ restart حضور گذرا را از دست می‌دهد نه SQL truth را. |
| 32 | rate limiting | IMPLEMENTED | limiterهای فعلی در host؛ in-process و single-node، نه تضمین distributed quota. |
| 33 | CORS | IMPLEMENTED | allowlist مبتنی بر config؛ wildcard/credential ناامن در Production پذیرفته نمی‌شود؛ localhost فقط محیط مجاز. |
| 34 | Security headers | IMPLEMENTED | nosniff/referrer/frame/permissions و استثنای Swagger/OpenAPI برای CSP موجود است. |
| 35 | HTTPS/HSTS readiness | IMPLEMENTED | HSTS/redirect/forwarded headers کنترل‌شده موجود؛ exception مصوب HTTP نباید بی‌اجازه حذف شود. |
| 36 | HTTP عمومی | BLOCKED_BY_INFRA | محیط معرفی‌شده HTTP است؛ TLS/domain معتبر اثبات نشده. `BACKEND_HTTPS = PENDING_INFRASTRUCTURE`. |
| 37 | diagnostics داخلی | PARTIAL | `/api/admin/v1/operations/health` و jobs/notifications با permission موجود؛ scheduled/cleanup و شکست نهایی outbox کافی نیستند. |
| 38 | metrics | PARTIAL | Meter مربوط به HTTP موجود؛ ترتیب middleware خطای mapped را ممکن است 2xx بشمارد؛ method نامحدود و worker/backlog signals ناقص است. |
| 39 | disk/log retention | OPERATIONAL_ONLY | stdout خاموش؛ rotation/فضای دیسک وابسته به IIS/collector/host است، نه حذف سفارشی فایل‌های نامرتبط توسط برنامه. |
| 40 | DB retention/cleanup | DEFERRED_BY_POLICY | مدت مجاز عمومی تصویب نشده؛ مالی، audit، moderation، academic و history از generic cleanup خارج‌اند. |
| 41 | startup migrations | IMPLEMENTED | بررسی source واقعی Api/Infrastructure: هیچ `Migrate`/`MigrateAsync` در startup وجود ندارد. rollout schema فقط اپراتوری/versioned است. |
| 42 | EnsureCreated | IMPLEMENTED | در کد Production وجود ندارد؛ جایگزین migration نشده است. |
| 43 | backup/restore | OPERATIONAL_ONLY | framework/runbook بازیابی موجود؛ schedule، off-server copy، failure alert، restore drill و RPO/RTO عملیاتی اثبات نشده‌اند. |
| 44 | deployment/runbook | PARTIAL | اسناد canonical در `docs/production` موجودند؛ دستورهای دقیق AppPool/ACL، ring recovery، observability و acceptance باید تکمیل شوند. |
| 45 | restart/reboot سرور | OPERATIONAL_ONLY | این رایانه سرور IIS مجاز نیست؛ local test جای اثبات reboot/start/stop واقعی را نمی‌گیرد. |

پیشوند مسیرهای کوتاه جدول، پروژه‌های واقعی `MakanApp.Api`، `MakanApp.Infrastructure` و غیره است.

## Workerهای واقعی و شواهد کلیدی

دقیقاً ۶ `BackgroundService` فعلی: `ReportExportDispatcher`،
`ScheduledMessageDispatcher`، `MessagingEphemeralDispatcher`،
`MessagingRealtimeOutboxDispatcher`، `OutboxDispatcher`، `NotificationDeliveryDispatcher`.
`DependencyInjection.cs` ثبت آن‌ها را انجام می‌دهد. worker ساختگی یا محصول آینده در inventory نیست.
Notification، scheduled و report دارای terminal state هستند؛ outboxها با AttemptCount نهایی
قابل تشخیص‌اند و نباید pending قابل‌اجرا گزارش شوند. ephemeral عمداً nondurable و bounded است.

تست‌های مبنا شامل `PlatformMfaProtectionConfigurationTests`، `HealthEndpointTests`،
`ProductionHostingTests`، `NotificationEndpointsTests`،
`MessagingP1NotificationDeliveryTests`، `MessagingP2ScheduledRecoveryTests`،
`MessagingP2ScheduledEndpointsTests` و `ReportExportEndpointsTests` هستند.
بیشتر factoryها hosted worker را غیرفعال و processor را صریح اجرا می‌کنند؛ پس
این تست‌ها شاهد recycle واقعی IIS نیستند.

## ترتیب اصلاحات و مرزها

1. PH-1: تکمیل همان Data Protection سراسری؛ حفظ discriminator/purpose/ring قبلی،
   اعتبارسنجی startup و runbook حفاظت و بازیابی secret/کلید.
2. PH-2: readiness core و diagnostics امن؛ هیچ provider اختیاری شرط آماده‌بودن کل API نیست.
3. PH-3: correlation یکپارچه، خطای امن در تمام pipeline، request/worker metrics کم‌کاردینالیتی.
4. PH-4: claim/fencing/retry/repair و shutdown bounded؛ SQL مرجع durable باقی می‌ماند.
5. PH-5: timeout سازگار، runbook دقیق IIS/ACL و پذیرش TLS مستقل از exception فعلی.
6. PH-6: فقط transient reconciliation مفید، bounded و پیش‌فرض خاموش؛ بدون مدت retention اختراعی.
7. PH-7: alert contract، regression Release و checklist اپراتوری؛ collector/alert/reboot بدون اجرا PASS نمی‌شوند.

اسناد canonical موجود (`docs/production/DEPLOYMENT_IIS.md`، `OBSERVABILITY.md`،
`RELEASE_CHECKLIST.md`، `BACKUP_RESTORE.md`) به‌جای ایجاد نسخه‌های موازی به‌روز می‌شوند.
هیچ تغییری در MeliPayamak، AI/RAG، P3 media، frontend یا secret واقعی مجاز این فاز نیست.
هیچ migration خودکاری، schema mutation دستی و عملیات سرور اجرا نمی‌شود.

## نکات آموزشی PH-0

- نوشتن runbook به معنی اجرای آن نیست؛ code-level و server-level evidence دو gate مستقل‌اند.
- rowversion جلوی filesystem side effect پیش از commit را نمی‌گیرد؛ مالکیت باید در مرز اثر نیز کنترل شود.
- پاسخ HTTP امن کافی نیست اگر framework همان exception را در log منتشر کند.
- DPAPI کاربر جاری به identity/machine وابسته است؛ backup XML به‌تنهایی recovery بین سرورها نیست.
- expiry metadata مجوز حذف همهٔ فایل‌های ظاهراً unattached نیست؛ هر binding و evidence باید بررسی شود.

این مرحله فقط سند اضافه می‌کند؛ Domain/Application/Infrastructure/Api، endpoint و schema تغییر ندارند.
مسیر مطالعه: `Program.cs` → `BackofficeMfaConfiguration` → `CoreReadinessHealthCheck`
→ middlewareهای Observability → dispatcherها → تست‌های نام‌برده → runbookهای canonical.

## پیگیری مراحل بعد

وضعیت نهایی هر PH و نتایج دقیق validation/commit در ادامهٔ اجرای همین برنامه ثبت خواهد شد؛
این جدول مبنا به‌تنهایی ادعای بسته‌شدن فاصله‌ها نیست.

### PH-1 — تکمیل‌شده در کد و تست محلی

همان provider سراسری با نام/purpose و مسیر legacy حفظ شد؛ aliasهای canonical، رد تعارض،
startup lifecycle validation و probe جاری بدون reseed اضافه شدند. host عملیاتی بدون ring پایدار
دیگر شروع نمی‌شود. DPAPI current-user و PFX موجود حفظ شده‌اند؛ custody/ACL/انتقال واقعی gate اپراتوری‌اند.
مسیر مطالعه: `BackofficeMfaConfiguration` → `DataProtectionKeyRingSettings` →
`DataProtectionKeyRingStartupValidator` → `DataProtectionKeyRingProbe` → `ProductionDataProtectionTests`.

Validation واقعی: restore موفق؛ build و final build با ۰ warning/error؛ **49** تست متمرکز موفق؛
final suite: **606 Unit + 1060 Integration + 14 Architecture = 1680**؛ failed/skipped=0.
**26** مورد تست Integration به مبنا اضافه شد. migration و endpoint جدید ندارد.
XML/PFX تستی از secret عملیاتی مستقل‌اند. اجرای IIS/recycle/reboot یا secret provisioning سرور انجام نشده است.
