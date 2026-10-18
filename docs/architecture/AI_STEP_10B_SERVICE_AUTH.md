# STEP 10B — Service Authentication

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

Backend اکنون از طریق `IAiServiceCredentialProvider` یک credential داخلی مستقل از کاربر می‌گیرد. پیاده‌سازی `HmacAiServiceCredentialProvider` JWT کوتاه‌عمر HS256 با `kid`، issuer، audience، service subject، scope، timestamps و `jti` می‌سازد.

AI Service تمام endpointهای business زیر `/api/v1/ai/*` را با `require_service_auth` محافظت می‌کند. health/readiness عمومی‌اند. verifier امضا، الگوریتم، key id، issuer، audience، scope، زمان و بیشینه عمر token را fail-closed بررسی می‌کند.

## Rotation

AI هم‌زمان کلید جاری و یک کلید قبلی را می‌پذیرد. Backend فقط با کلید جاری امضا می‌کند. پس از انتشار کلید جدید و عبور بیشینه عمر token، کلید قبلی حذف می‌شود.

## Secrets

مقادیر `AiService:Authentication:CurrentKey` و `MAKAN_AI_AUTH_CURRENT_KEY` باید حداقل 32 بایت باشند و فقط از secret store یا environment تأمین شوند. هیچ secret واقعی در repository ذخیره نشده است.

## مرز امنیتی

Service JWT شامل user/role/organization/session نیست. bearer کاربر به AI ارسال نمی‌شود. مجوز business همچنان در Backend و پیش از ساخت payload اعمال می‌شود.
