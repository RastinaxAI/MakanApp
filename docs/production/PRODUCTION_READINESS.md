# آمادگی جاری Production و حدود انتشار

بازبینی هماهنگی `2026-10-29` / `main@701cba0`: آخرین schema مخزن `20261028202239_CompleteSchoolManagement`، ۵۲ migration است؛ Profile DOB/Photo و مدیریت مدرسه/Guardian consent/Lesson به binary جدید نیاز دارند. بسته‌های قبلی خودبه‌خود این تغییرات را ندارند. MFA/key ring حذف، CORS تابع AllowAnyOrigin و OTP Test=false/true، Production=true/false است. [استقرار نسخهٔ جاری](../deployment-windows-linux-docker.md) و [مدرسه](../product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md). پذیرش سرور یا اجرای migration با این بازبینی انجام نشده است.

بازبینی `2026-10-23`، مبنا `main@5885d11`؛ نتیجهٔ انتشار عمومی همچنان `NOT_READY_FOR_PUBLIC_RELEASE` است. closure کد GAP-01/04/05 با smoke محیط هدف، استقرار یا تأیید حقوقی/عملیاتی یکی نیست. مالک انتشار فعلی Production/HTTP را با دو استثنای صریح پذیرفته، نه یک محیط Staging تازه. [runbook IIS](DEPLOYMENT_IIS.md) و [ممیزی gapها](../release/NON_AI_BACKEND_FINAL_GAP_AUDIT.md) مرجع جزئیات‌اند.

## تغییرات جدید مبنای آمادگی

PH-1..6 در کد شامل readiness، log/metrics امن، worker recovery، shutdown و cleanup محدود opt-in است؛ این‌ها نیاز عملیات target را حذف نمی‌کنند. [مراجع PH](../operations/PRODUCTION_CONFIGURATION_RUNBOOK.md). مدل جاری ۱۰۵ جدول و ۴۹ Migration دارد؛ حذف MFA فقط در مدل و تست ایزوله اعمال شده است.

TestIis و ProductionIis در انتشار محلی جدا و با OTP false/true و true/false آزموده شده‌اند؛ templateهای Test/Production هم‌شکل‌اند. گزارش قبلی [Release Readiness](BACKEND_RELEASE_READINESS_FA.md) با ۲۲۳۹ تست، شاهد همان مرحله است؛ نتیجهٔ بازبینی فعلی در [وضعیت جاری](../CURRENT_PROJECT_STATUS.md) درج می‌شود. [راهنمای دو بسته](PUBLISH_GUIDE_FA.md) کانفیگ خصوصی بسته را عمومی نمی‌کند.

Service Auth واقعی AI قبلاً PASS شده؛ Parent Child Summary هنوز Provider Timeout دارد و Full-12 واقعی Deferred است. [Consolidation](../release/BACKEND_FINAL_CONSOLIDATION_FA.md). از موفقیت Publish یا Fake-AI، دریافت Push، عبور LiveKit rejoin، TLS، restore/load یا AI کامل استنتاج نمی‌شود.

## تغییرات Messenger در این بازبینی

P0/P1/P2 تا Checklist در کد و تست‌های محلی موجودند؛ Enum/OpenAPI در snapshot جاری mismatch قدیمی ندارد. Web Push/FCM transport واقعی دارند ولی دستگاه واقعی تأیید نشده است. Typing/Presence حافظه‌ای تک‌instance‌اند و PENDING_SCALE_INFRA برقرار است. P3 فقط policy مصوب دارد و در S1 با P3_S_INFRA_REQUIRED متوقف است؛ LIVEKIT_REJOIN_SECURITY_GATE=NOT_TESTED و P3_PRODUCTION_MEDIA_APPROVAL=NOT_GRANTED. [وضعیت و منابع دقیق](../messaging/CURRENT_MESSENGER_STATUS.md).

## Blockerهای الزامی

| کد | وضعیت | شرط رفع |
|---|---|---|
| `PAYMENT_PROVIDER_REQUIRED` | BLOCKED | قرارداد تأییدشده‌ی create/verify/reconcile و adapter واقعی |
| `CHILD_SAFETY_OPERATIONAL_VALIDATION` | IMPLEMENTED_WITH_LIMITATIONS | سیاست v1 و Internal Makan مصوب و پیاده‌سازی شده‌اند؛ رسانه unsupported منتشر نمی‌شود، ReviewRequired است؛ ظرفیت بازبینی و محدودیت موتور قواعد باید عملیاتی بررسی شوند |
| `TARGET_TLS_AND_CORE_SMOKE` | OPEN | هدف فعلی HTTP است؛ تأیید استثنا، TLS یا smoke امن GAP-02 را ثابت نمی‌کند |

Backend AI شامل adapter و ۱۲ API است؛ پیش‌فرض غیرفعال و real E2E از شواهد commit‌شده تأیید نشده است. RAG source lifecycle deferred است. core آموزشی به موفقیت سرویس AI وابسته نیست و فایل‌های dirty شواهد AI وارد این تحویل نمی‌شوند.

## ماتریس آمادگی

| حوزه | وضعیت | شاهد یا محدودیت |
|---|---|---|
| Authentication | READY | opaque bearer، access یک‌ساعته، refresh چرخشی ۳۰روزه و revocation سمت سرور |
| Platform Authentication | REMOVED_SECONDARY_FACTOR | OTP و Session معتبر به‌همراه نقش و permission؛ عامل دوم و وابستگی key ring حذف شده‌اند |
| Authorization | READY | context از session/membership/resource سمت سرور resolve می‌شود |
| Secrets | CONTRACT_VALIDATED / OPS_PENDING | فایل واقعی Production ignored است؛ وجود/محتوای secret روی سرور از clean worktree نتیجه نمی‌شود؛ نمونه بدون secret و اعتبارسنجی تنظیمات فعال موجود است؛ provisioning/ACL/custody وظیفهٔ اپراتور است |
| SQL Server | READY_WITH_LIMITATION | TLS و least privilege مستند/اسکریپت شده؛ ایجاد principal روی سرور باقی است |
| Storage | READY_WITH_LIMITATION | root مطلق، probe، authorization و امضای حداقلی؛ AV/CDR provider وجود ندارد |
| CORS | CONFIGURABLE | طبق تصمیم مالک: `AllowAnyOrigin=true` و `AllowedHosts=*` در Test/Production؛ با false، allowlist اجباری است؛ cookie credentials فعال نیست؛ [قرارداد](CORS_CONFIGURATION_FA.md) |
| TLS | OWNER_EXCEPTION / NOT_SECURE | هدف فعلی HTTP؛ default امن باقی است ولی فعال بودن استثنا شبکه را رمزگذاری نمی‌کند |
| Security headers | READY | nosniff، referrer، frame/CSP و permissions policy |
| Rate limiting | READY_WITH_LIMITATION | policyهای local-process؛ چند instance به limiter مشترک نیاز دارد |
| Payment | BLOCKED | provider واقعی وجود ندارد و موفقیت دستی ممنوع است |
| Child Safety | COMPLETE_IN_CODE / OPS_PENDING | Policy v1 و Unknown=protected، رابطه/رضایت، current-state Send و provider داخلی واقعی؛ بدون ادعای ML یا تحلیل معنایی رسانه |
| Notifications | CODE_COMPLETE / DEVICE_RUNTIME_PENDING | Web Push/VAPID و FCM HTTP v1 واقعی؛ credential/device واقعی و دریافت روی دستگاه تأیید نشده؛ P1_PUSH_RUNTIME_PENDING |
| Logging | CODE_VALIDATED / COLLECTOR_POLICY_PENDING | correlation واحد، metadata محدود، خطای امن و تست نشت؛ پاک‌سازی عمومی همهٔ loggerها/DiagnosticSource ادعا نمی‌شود |
| Metrics | CODE_VALIDATED / EXPORTER_PENDING | meterهای vendor-neutral درخواست/worker/صف و snapshot stale-aware؛ collector یا endpoint metrics نصب‌شده نداریم |
| Tracing | CODE_VALIDATED / EXPORTER_PENDING | ActivitySourceهای API/worker؛ collector نباید body/header/exception حساس را خودکار جمع کند |
| Health | CODE_VALIDATED / SERVER_PENDING | live سبک؛ ready با config/ring/SQL/storage؛ optional provider readiness core را نمی‌بندد |
| Workers | CODE_VALIDATED / IIS_LIFECYCLE_PENDING | claim/fencing/repair محدود و SQL recovery محلی؛ recycle/reboot و old/new legacy هم‌پوشان آزمایش سرور نیستند؛ تحویل خارجی exactly-once نیست |
| IIS runtime | CODE_AND_RUNBOOK / SERVER_PENDING | shutdown Host محدود، ANCM 120s و دستور بررسی WAS/idle/preload؛ تنظیم سرور اجرا نشده است |
| Retention | INVENTORIED / POLICY_PENDING | ۱۰۷ جدول طبقه‌بندی شده؛ فقط مکانیزم خاموشِ bytes terminal بدون reference؛ هیچ SQL history purge ندارد |
| Alerting | CONTRACT_READY / DELIVERY_PENDING | signal/severity/runbook مشخص؛ exporter و دریافت واقعی alert همچنان زیرساخت لازم دارد |
| Backup | READY_WITH_LIMITATION | runbook آماده؛ job واقعی SQL/VPS باید ایجاد شود |
| Restore | READY_WITH_LIMITATION | drill disposable روی LocalDB موفق؛ rehearsal روی topology واقعی staging باقی است |
| Migration | READY | startup migration ممنوع؛ principal و gate جدا مستند است |
| Deployment | LOCAL_VALIDATION_ONLY | Publish محلی و SQL handoff تست شده؛ سرور از طرف این تحویل تغییر نکرده؛ اپراتور باید هدف واقعی را validate کند |
| Rollback | READY_WITH_LIMITATION | app rollback و DB forward-fix/restore مستند؛ باید rehearsal شود |
| Performance | READY_WITH_LIMITATION | queryهای residual bounded و load plan آماده؛ baseline staging اجرا نشده است |
| AI integration | BACKEND_IMPLEMENTED / REAL_E2E_PENDING | قراردادهای 10A–10E موجود؛ lifecycle RAG و شواهد واقعی جدا، برای core الزامی نیست |

## قرارداد providerها

### ورود Platform

طبق تصمیم مالک در ۲۰۲۶-۱۰-۲۴، MFA، TOTP، Recovery و Step-Up حذف شده‌اند؛ key ring فقط مرتبط با آن‌ها نیز دیگر gate Startup/ready نیست.
OTP و Session معتبر، Platform role/permission، Reason، Audit و محافظ‌های آخرین Owner/Manager حفظ شده‌اند. School Manager به‌تنهایی Platform Admin نیست.
Migration `RemovePlatformMfa` مخرب است و اجرای عملیاتی آن مرحله‌ای مستقل با Backup و تأیید مالک است؛ فایل‌های قدیمی key ring سرور فعلاً حفظ شوند.

### Payment

adapter باید `CreatePayment`، callback/webhook authenticated، `Verify`، `Query/Reconciliation`، idempotency، تطبیق amount/currency، timeout/unknown state، یکتایی provider reference و قرارداد refund آینده را مشخص کند. هیچ endpoint مدیریتی مجاز به موفق اعلام‌کردن دستی پرداخت نیست.

### Child Safety

Internal Makan تصمیم جاری مصوب و موتور قاعده‌محور محدود متن است؛ vendor بیرونی لازم یا پیاده‌شده فرض نمی‌شود. مسیر Minor/Unknown هنگام unavailable بودن fail-closed است؛ attachment/image unsupported نتیجهٔ ReviewRequired دارد، نه موفقیت ساختگی. هر ارتقای classifier/provider نیازمند scope و validation مستقل است؛ [Policy GAP-05](../architecture/GAP_05_MESSENGER_CHILD_SAFETY.md).

## تصمیم Web client

قرارداد فعلی Bearer شکسته نشده است. برای Web آینده، BFF یا cookie نشست با `HttpOnly`، `Secure` و `SameSite` مناسب باید در یک تصمیم مستقل همراه با CSRF protection طراحی شود؛ نگهداری token بلندمدت در JavaScript storage توصیه نمی‌شود.

## ماتریس پیکربندی محیط

| قابلیت | Development | Production امنِ پیش‌فرض | انتشار HTTP تأییدشدهٔ فعلی |
|---|---|---|---|
| Logging | `Information`، local | `Information` + collector | `Information`، framework/EF در `Warning` |
| Swagger/OpenAPI | Available | Disabled پیش‌فرض | مالک برای artifact خصوصی قبلی Enabled را تأیید کرده؛ وضعیت واقعی deploy دوباره بررسی نشده؛ config خصوصی تغییر نکرده |
| CORS | فقط فلگ Configuration، بدون fallback محیط | با false: allowlist صریح HTTPS | طبق تصمیم مالک فلگ true؛ با false: allowlist و استثنای مستقل HTTP |
| HTTPS/HSTS | redirect قابل استفاده، بدون HSTS | RequireHttps=true، HSTS | RequireHttps=false، AllowInsecureHttp=true |
| Storage | مسیر `%LOCALAPPDATA%` | persistent absolute path | persistent absolute path و fail-fast |
| SQL | مقصد توسعهٔ مجاز | Encrypt=True و بررسی معتبر گواهی | Encrypt=True؛ استثنای مستقل پذیرش گواهی تأییدنشده |
| Payment | Unavailable/Test adapter فقط در test host | Unavailable | `Unavailable` / FailClosed تا provider واقعی |
| Content Safety | DevelopmentAllow فقط توسعه | Internal / FailClosed برای failure | Internal / ReviewRequired برای رسانه unsupported |
| AI | adapter تست یا سرویس مصوب | پیش‌فرض غیرفعال؛ مستقل از core | پیش‌فرض غیرفعال؛ real E2E تأیید نشده |
| Platform Authentication | OTP/Session و permission جاری | همان قواعد، بدون عامل دوم | همان قواعد؛ HTTP مجوز bypass ایجاد نمی‌کند |
| Push notification | Development | Unavailable مگر adapter تأییدشده | Unavailable مگر adapter تأییدشده |
| SMS notification | Development | Unavailable مگر adapter تأییدشده | Unavailable مگر adapter تأییدشده |
| Login SMS OTP | Development یا MeliPayamak با secret | configured provider | configured provider با secret store |

## محدودیت‌های صریح

- Swagger در Production پیش‌فرض خاموش است و فقط با تصمیم عملیات و کنترل دسترسی باید فعال شود.
- limiter در حافظه‌ی process است؛ برای IIS تک-instance قابل قبول و برای scale-out ناکافی است.
- signature validation فقط formatهای allowlist شده را با magic bytes/UTF-8 حداقلی می‌سنجد و جایگزین malware scanning نیست.
- readiness، config لازم Production، SQL Server و storage اصلی را gate می‌کند؛ providerهای اختیاری/degraded در ماتریس عملیات دیده می‌شوند.
- load baseline و restore rehearsal روی topology واقعی staging باید اجرا شوند، نه Production؛ drill پایه‌ی LocalDB انجام شده است.

## نتیجهٔ PH و مرز پذیرش

[ممیزی PH](../operations/PRODUCTION_HARDENING_AUDIT.md) شواهد کد/تست/commit را ثبت می‌کند؛
[چک‌لیست پذیرش](RELEASE_CHECKLIST.md#پذیرش-ph--تفکیک-کد-از-سرور) gateهای اجرا‌نشدهٔ محیط را دارد.
هیچ تغییر روی IIS مقصد، reboot، SMS واقعی، credential/device Push، AI واقعی، collector یا alert delivery
با تحویل محلی PH ادعا نمی‌شود. `BACKEND_HTTPS=PENDING_INFRASTRUCTURE`،
`OBSERVABILITY_EXPORTER=PENDING_INFRASTRUCTURE` و `ALERT_DELIVERY=PENDING_INFRASTRUCTURE` باقی‌اند.
backup زمان‌بندی‌شده، off-server copy، failure alert، drill واقعی و RPO/RTO نیز شاهد عملیاتی مستقل می‌خواهند.
تا ثبت این شواهد، `PRODUCTION_SERVER_ACCEPTANCE_REQUIRED` نتیجهٔ کد است و
`NOT_READY_FOR_PUBLIC_RELEASE` با استثنای HTTP یا پاس تست‌ها بسته نمی‌شود.
