# آمادگی جاری Production و حدود انتشار

بازبینی `2026-10-15`، مبنا `main@80d6a14`؛ نتیجهٔ انتشار عمومی همچنان `NOT_READY_FOR_PUBLIC_RELEASE` است. closure کد GAP-01/04/05 با smoke محیط هدف، استقرار یا تأیید حقوقی/عملیاتی یکی نیست. مالک انتشار فعلی Production/HTTP را با دو استثنای صریح پذیرفته، نه یک محیط Staging تازه. [runbook IIS](DEPLOYMENT_IIS.md) و [ممیزی gapها](../release/NON_AI_BACKEND_FINAL_GAP_AUDIT.md) مرجع جزئیات‌اند.

## Blockerهای الزامی

| کد | وضعیت | شرط رفع |
|---|---|---|
| `MFA_KEY_CUSTODY` | OPERATIONAL_VALIDATION_PENDING | TOTP/recovery/step-up واقعی موجود است؛ مسیر پایدار، ACL، هویت IIS، زمان صحیح و custody/recovery کلیدهای محیط باید تأیید شوند |
| `PAYMENT_PROVIDER_REQUIRED` | BLOCKED | قرارداد تأییدشده‌ی create/verify/reconcile و adapter واقعی |
| `CHILD_SAFETY_OPERATIONAL_VALIDATION` | IMPLEMENTED_WITH_LIMITATIONS | سیاست v1 و Internal Makan مصوب و پیاده‌سازی شده‌اند؛ رسانه unsupported منتشر نمی‌شود، ReviewRequired است؛ ظرفیت بازبینی و محدودیت موتور قواعد باید عملیاتی بررسی شوند |
| `TARGET_TLS_AND_CORE_SMOKE` | OPEN | هدف فعلی HTTP است؛ تأیید استثنا، TLS یا smoke امن GAP-02 را ثابت نمی‌کند |

Backend AI شامل adapter و ۱۲ API است؛ پیش‌فرض غیرفعال و real E2E از شواهد commit‌شده تأیید نشده است. RAG source lifecycle deferred است. core آموزشی به موفقیت سرویس AI وابسته نیست و فایل‌های dirty شواهد AI وارد این تحویل نمی‌شوند.

## ماتریس آمادگی

| حوزه | وضعیت | شاهد یا محدودیت |
|---|---|---|
| Authentication | READY | opaque bearer، access یک‌ساعته، refresh چرخشی ۳۰روزه و revocation سمت سرور |
| MFA | COMPLETE_IN_CODE / OPS_PENDING | TOTP، recovery، replay guard و step-up session-scoped؛ نگه‌داری key ring محیط جداست |
| Authorization | READY | context از session/membership/resource سمت سرور resolve می‌شود |
| Secrets | CONFIGURED_LOCALLY / OPS_PENDING | فایل واقعی Production محلی و ignored است؛ نمونهٔ عمومی بدون secret؛ ACL/انتقال امن و حفظ کلیدهای قبلی وظیفهٔ اپراتور است |
| SQL Server | READY_WITH_LIMITATION | TLS و least privilege مستند/اسکریپت شده؛ ایجاد principal روی سرور باقی است |
| Storage | READY_WITH_LIMITATION | root مطلق، probe، authorization و امضای حداقلی؛ AV/CDR provider وجود ندارد |
| CORS | IMPLEMENTED | allowlist صریح؛ HTTP فقط با Hosting:AllowInsecureHttp تأییدشده، بدون wildcard یا credentials |
| TLS | OWNER_EXCEPTION / NOT_SECURE | هدف فعلی HTTP؛ default امن باقی است ولی فعال بودن استثنا شبکه را رمزگذاری نمی‌کند |
| Security headers | READY | nosniff، referrer، frame/CSP و permissions policy |
| Rate limiting | READY_WITH_LIMITATION | policyهای local-process؛ چند instance به limiter مشترک نیاز دارد |
| Payment | BLOCKED | provider واقعی وجود ندارد و موفقیت دستی ممنوع است |
| Child Safety | COMPLETE_IN_CODE / OPS_PENDING | Policy v1 و Unknown=protected، رابطه/رضایت، current-state Send و provider داخلی واقعی؛ بدون ادعای ML یا تحلیل معنایی رسانه |
| Notifications | READY_WITH_LIMITATION | durable delivery موجود؛ providerهای Production باید پیکربندی شوند |
| Logging | READY | structured logging، correlation ID و حداقل سطح Information |
| Metrics | READY_WITH_LIMITATION | `System.Diagnostics.Metrics` vendor-neutral؛ exporter در محیط نصب می‌شود |
| Tracing | READY_WITH_LIMITATION | ASP.NET/Activity و trace ID موجود؛ collector/exporter عملیات محیط است |
| Health | READY | `/health/live` و `/health/ready` جدا و بدون جزئیات secret |
| Workers | READY | batch/lease/cancellation/backoff و سقف retry برای poison item |
| Backup | READY_WITH_LIMITATION | runbook آماده؛ job واقعی SQL/VPS باید ایجاد شود |
| Restore | READY_WITH_LIMITATION | drill disposable روی LocalDB موفق؛ rehearsal روی topology واقعی staging باقی است |
| Migration | READY | startup migration ممنوع؛ principal و gate جدا مستند است |
| Deployment | LOCAL_VALIDATION_ONLY | Publish محلی و SQL handoff تست شده؛ سرور از طرف این تحویل تغییر نکرده؛ اپراتور باید هدف واقعی را validate کند |
| Rollback | READY_WITH_LIMITATION | app rollback و DB forward-fix/restore مستند؛ باید rehearsal شود |
| Performance | READY_WITH_LIMITATION | queryهای residual bounded و load plan آماده؛ baseline staging اجرا نشده است |
| AI integration | BACKEND_IMPLEMENTED / REAL_E2E_PENDING | قراردادهای 10A–10E موجود؛ lifecycle RAG و شواهد واقعی جدا، برای core الزامی نیست |

## قرارداد providerها

### MFA / Step-up

پیاده‌سازی جاری GAP-04 نتیجهٔ TOTP/recovery را سمت سرور بررسی و assurance/freshness همان Session را ثبت می‌کند؛ counter، replay، محدودیت تلاش و audit دارد. OTP ورود عامل دوم نامیده نمی‌شود. برای عامل آینده سیستم موازی نسازید؛ port و قواعد موجود reuse شوند. [قرارداد جاری](../release/GAP_04_BACKOFFICE_TOTP_MFA.md).

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
| Swagger/OpenAPI | Available | Disabled پیش‌فرض | Disabled |
| CORS | در نبود config باز برای ابزار توسعه | allowlist صریح HTTPS | allowlist صریح؛ HTTP با opt-in |
| HTTPS/HSTS | redirect قابل استفاده، بدون HSTS | RequireHttps=true، HSTS | RequireHttps=false، AllowInsecureHttp=true |
| Storage | مسیر `%LOCALAPPDATA%` | persistent absolute path | persistent absolute path و fail-fast |
| SQL | مقصد توسعهٔ مجاز | Encrypt=True و بررسی معتبر گواهی | Encrypt=True؛ استثنای مستقل پذیرش گواهی تأییدنشده |
| Payment | Unavailable/Test adapter فقط در test host | Unavailable | `Unavailable` / FailClosed تا provider واقعی |
| Content Safety | DevelopmentAllow فقط توسعه | Internal / FailClosed برای failure | Internal / ReviewRequired برای رسانه unsupported |
| AI | adapter تست یا سرویس مصوب | پیش‌فرض غیرفعال؛ مستقل از core | پیش‌فرض غیرفعال؛ real E2E تأیید نشده |
| Platform step-up | test double فقط test host | TOTP/recovery واقعی و key ring پایدار | همان پیاده‌سازی، نه bypass با HTTP |
| Push notification | Development | Unavailable مگر adapter تأییدشده | Unavailable مگر adapter تأییدشده |
| SMS notification | Development | Unavailable مگر adapter تأییدشده | Unavailable مگر adapter تأییدشده |
| Login SMS OTP | Development یا MeliPayamak با secret | configured provider | configured provider با secret store |

## محدودیت‌های صریح

- Swagger در Production پیش‌فرض خاموش است و فقط با تصمیم عملیات و کنترل دسترسی باید فعال شود.
- limiter در حافظه‌ی process است؛ برای IIS تک-instance قابل قبول و برای scale-out ناکافی است.
- signature validation فقط formatهای allowlist شده را با magic bytes/UTF-8 حداقلی می‌سنجد و جایگزین malware scanning نیست.
- readiness فقط SQL Server و storage اصلی را gate می‌کند؛ providerهای اختیاری/degraded در ماتریس عملیات دیده می‌شوند.
- load baseline و restore rehearsal روی topology واقعی staging باید اجرا شوند، نه Production؛ drill پایه‌ی LocalDB انجام شده است.
