# تصمیم Service Authentication برای Backend ↔ AI

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## تصمیم

برای نسخه اول integration از JWT داخلی کوتاه‌عمر با امضای `HS256` استفاده می‌شود. این انتخاب موقت و عملی با dependencyهای فعلی است؛ کلید asymmetric یا workload identity در محیط orchestration بالغ‌تر گزینه ترجیحی آینده است.

## Token contract

Header:

- `alg=HS256`
- `typ=JWT`
- `kid` اجباری برای rotation

Claims اجباری:

- `iss`: issuer اختصاصی Makan Backend
- `aud`: audience اختصاصی Makan AI
- `sub`: شناسه service caller، مثلاً `makan-backend`
- `scope`: شامل `makan.ai.invoke`
- `iat`, `nbf`, `exp`: عمر حداکثر 5 دقیقه با clock skew محدود
- `jti`: شناسه یکتای token

Token نباید user id، organization، role، learner، session یا محتوای business داشته باشد.

## Rotation

- Backend با `CurrentKeyId/CurrentKey` امضا می‌کند.
- AI کلید جاری و حداکثر یک `PreviousKeyId/PreviousKey` را برای پنجره rotation می‌پذیرد.
- ابتدا کلید جدید به verifier اضافه، سپس signer عوض و بعد از بیشینه عمر token کلید قبلی حذف می‌شود.
- keyها secret هستند و فقط از environment/secret store خوانده می‌شوند؛ در appsettings، log، docs یا repository مقدار واقعی قرار نمی‌گیرد.

## رفتار fail-closed

- نبودن/خراب بودن/منقضی بودن token، issuer/audience/scope نادرست و `kid` ناشناخته با `401` یا `403` مردود می‌شود.
- routeهای business بدون service token اجرا نمی‌شوند.
- health/readiness برای probe عمومی باقی می‌مانند و داده حساس برنمی‌گردانند.
- Backend خطای service auth را به client به شکل `ai_service_unavailable` نمایش می‌دهد و جزئیات داخلی را log امن می‌کند.

## مرز با User Authentication

Bearer کاربر هیچ‌گاه به AI forward نمی‌شود. Backend ابتدا آن را validate و مجوز use case را resolve می‌کند، سپس token داخلی مستقل می‌سازد. AI فقط caller service را authenticate می‌کند و به role داخل payload برای authorization اعتماد نمی‌کند.

## چرایی عدم انتخاب گزینه‌های دیگر

- mTLS اکنون PKI و deployment lifecycle تعریف‌شده ندارد.
- OAuth2 client credentials نیازمند authorization server/service identity زیرساختی است که در مخازن فعلی وجود ندارد.
- asymmetric JWT امن‌تر برای جداسازی signer/verifier است، اما dependency و key distribution فعلی آماده نیست. ارتقا بدون تغییر contract claimها ممکن است.

## تنظیمات نام‌گذاری‌شده

Backend:

```text
AiService:Authentication:Issuer
AiService:Authentication:Audience
AiService:Authentication:ServiceName
AiService:Authentication:Scope
AiService:Authentication:CurrentKeyId
AiService:Authentication:CurrentKey
AiService:Authentication:TokenLifetimeSeconds
```

AI:

```text
MAKAN_AI_AUTH_ISSUER
MAKAN_AI_AUTH_AUDIENCE
MAKAN_AI_AUTH_REQUIRED_SCOPE
MAKAN_AI_AUTH_CURRENT_KEY_ID
MAKAN_AI_AUTH_CURRENT_KEY
MAKAN_AI_AUTH_PREVIOUS_KEY_ID
MAKAN_AI_AUTH_PREVIOUS_KEY
MAKAN_AI_AUTH_CLOCK_SKEW_SECONDS
```
