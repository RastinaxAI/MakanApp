# آمادگی Production در STEP 16

این سند وضعیت واقعی backend را پس از hardening ثبت می‌کند. نتیجه‌ی فعلی `READY_FOR_STAGING_WITH_PRODUCTION_BLOCKERS` است؛ نبود provider واقعی با readiness جعلی پنهان نشده است.

## Blockerهای الزامی

| کد | وضعیت | شرط رفع |
|---|---|---|
| `MFA_PROVIDER_REQUIRED` | BLOCKED | انتخاب و پیاده‌سازی TOTP، WebAuthn یا factor تأییدشده و اتصال آن به assurance نشست |
| `PAYMENT_PROVIDER_REQUIRED` | BLOCKED | قرارداد تأییدشده‌ی create/verify/reconcile و adapter واقعی |
| `CHILD_SAFETY_PROVIDER_REQUIRED` | BLOCKED | provider واقعی ارزیابی متن/attachment با قرارداد privacy و timeout |
| `MINOR_GROUP_POLICY_REQUIRED` | BLOCKED | تصمیم Product برای ساخت/join گروه و کانال توسط Minor |

AI برای قابلیت‌های AI هنوز `NotIntegrated/DependencyPending` است؛ core آموزشی به آن وابسته نیست و در STEP 16 پیاده‌سازی نشده است.

## ماتریس آمادگی

| حوزه | وضعیت | شاهد یا محدودیت |
|---|---|---|
| Authentication | READY | opaque bearer، access یک‌ساعته، refresh چرخشی ۳۰روزه و revocation سمت سرور |
| MFA | BLOCKED | `UnavailablePlatformStepUpAuthorization` عمداً fail-closed است |
| Authorization | READY | context از session/membership/resource سمت سرور resolve می‌شود |
| Secrets | READY_WITH_LIMITATION | secret در repo نیست؛ تزریق secret store روی سرور هنوز عملیات deployment است |
| SQL Server | READY_WITH_LIMITATION | TLS و least privilege مستند/اسکریپت شده؛ ایجاد principal روی سرور باقی است |
| Storage | READY_WITH_LIMITATION | root مطلق، probe، authorization و امضای حداقلی؛ AV/CDR provider وجود ندارد |
| CORS | READY | Production فقط allowlist صریح HTTPS را می‌پذیرد |
| TLS | READY_WITH_LIMITATION | enforce و HSTS در کد؛ certificate و binding وظیفه‌ی IIS است |
| Security headers | READY | nosniff، referrer، frame/CSP و permissions policy |
| Rate limiting | READY_WITH_LIMITATION | policyهای local-process؛ چند instance به limiter مشترک نیاز دارد |
| Payment | BLOCKED | provider واقعی وجود ندارد و موفقیت دستی ممنوع است |
| Child Safety | BLOCKED | abstraction و fail-closed موجود، provider و policy Minor حل نشده‌اند |
| Notifications | READY_WITH_LIMITATION | durable delivery موجود؛ providerهای Production باید پیکربندی شوند |
| Logging | READY | structured logging، correlation ID و حداقل سطح Information |
| Metrics | READY_WITH_LIMITATION | `System.Diagnostics.Metrics` vendor-neutral؛ exporter در محیط نصب می‌شود |
| Tracing | READY_WITH_LIMITATION | ASP.NET/Activity و trace ID موجود؛ collector/exporter عملیات محیط است |
| Health | READY | `/health/live` و `/health/ready` جدا و بدون جزئیات secret |
| Workers | READY | batch/lease/cancellation/backoff و سقف retry برای poison item |
| Backup | READY_WITH_LIMITATION | runbook آماده؛ job واقعی SQL/VPS باید ایجاد شود |
| Restore | READY_WITH_LIMITATION | drill disposable روی LocalDB موفق؛ rehearsal روی topology واقعی staging باقی است |
| Migration | READY | startup migration ممنوع؛ principal و gate جدا مستند است |
| Deployment | READY_WITH_LIMITATION | IIS checklist و smoke script آماده؛ staging deployment لازم است |
| Rollback | READY_WITH_LIMITATION | app rollback و DB forward-fix/restore مستند؛ باید rehearsal شود |
| Performance | READY_WITH_LIMITATION | queryهای residual bounded و load plan آماده؛ baseline staging اجرا نشده است |
| AI integration | NOT_APPLICABLE | عمداً خارج STEP 16 و برای core الزامی نیست |

## قرارداد providerها

### MFA / Step-up

provider آینده باید نتیجه‌ی factor را سمت سرور اعتبارسنجی کند، `SessionId` جاری را از authentication context بگیرد، `AuthenticationAssuranceLevel.MultiFactor` و `StepUpAtUtc` را ثبت کند، freshness policy داشته باشد و replay/audit امن را پوشش دهد. OTP ورود به‌تنهایی MFA نامیده نمی‌شود.

### Payment

adapter باید `CreatePayment`، callback/webhook authenticated، `Verify`، `Query/Reconciliation`، idempotency، تطبیق amount/currency، timeout/unknown state، یکتایی provider reference و قرارداد refund آینده را مشخص کند. هیچ endpoint مدیریتی مجاز به موفق اعلام‌کردن دستی پرداخت نیست.

### Child Safety

provider باید text و در صورت تأیید Product attachment/image، idempotency، timeout، region، retention، data-processing/privacy، response mapping، failure policy و monitoring را پوشش دهد. نبود provider در مسیر child-sensitive باید همچنان fail-closed باشد.

## تصمیم Web client

قرارداد فعلی Bearer شکسته نشده است. برای Web آینده، BFF یا cookie نشست با `HttpOnly`، `Secure` و `SameSite` مناسب باید در یک تصمیم مستقل همراه با CSRF protection طراحی شود؛ نگهداری token بلندمدت در JavaScript storage توصیه نمی‌شود.

## ماتریس پیکربندی محیط

| قابلیت | Development | Staging | Production |
|---|---|---|---|
| Logging | `Information`، local | `Information` + collector | `Information`، framework/EF در `Warning` |
| Swagger/OpenAPI | Available | Available طبق allowlist شبکه | Disabled پیش‌فرض |
| CORS | در نبود config باز برای ابزار توسعه | allowlist صریح | allowlist صریح HTTPS و fail-fast |
| HTTPS/HSTS | redirect قابل استفاده، بدون HSTS | TLS و redirect | TLS الزامی، HSTS فعال |
| Storage | مسیر `%LOCALAPPDATA%` | persistent absolute path | persistent absolute path و fail-fast |
| SQL | Local SQL با trusted certificate dev | encrypted isolated SQL | `Encrypt=True;TrustServerCertificate=False` |
| Payment | Unavailable/Test adapter فقط در test host | Unavailable | `Unavailable` / FailClosed تا provider واقعی |
| Content Safety | `DevelopmentAllow` مجاز | `Unavailable` / FailClosed | `Unavailable` / FailClosed |
| AI | NotIntegrated/Optional | NotIntegrated/Optional | NotIntegrated/Optional |
| Platform step-up | test double فقط در test host | Unavailable/FailClosed | Unavailable/FailClosed |
| Push notification | Development | Unavailable مگر adapter تأییدشده | Unavailable مگر adapter تأییدشده |
| SMS notification | Development | Unavailable مگر adapter تأییدشده | Unavailable مگر adapter تأییدشده |
| Login SMS OTP | Development یا MeliPayamak با secret | configured provider | configured provider با secret store |

## محدودیت‌های صریح

- Swagger در Production پیش‌فرض خاموش است و فقط با تصمیم عملیات و کنترل دسترسی باید فعال شود.
- limiter در حافظه‌ی process است؛ برای IIS تک-instance قابل قبول و برای scale-out ناکافی است.
- signature validation فقط formatهای allowlist شده را با magic bytes/UTF-8 حداقلی می‌سنجد و جایگزین malware scanning نیست.
- readiness فقط SQL Server و storage اصلی را gate می‌کند؛ providerهای اختیاری/degraded در ماتریس عملیات دیده می‌شوند.
- load baseline و restore rehearsal روی topology واقعی staging باید اجرا شوند، نه Production؛ drill پایه‌ی LocalDB انجام شده است.
