# STEP 10D — Generic Copilot Chat

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## Endpoint

```text
POST /api/v1/ai/chat
Authorization: Bearer <opaque-user-session>
X-Correlation-ID: optional
```

ورودی عمومی فقط `message` و `conversationId` اختیاری است. role، organization، school، learner و context از client پذیرفته نمی‌شوند.

## جریان

```text
AiController
-> AiChatService
-> GuardianAccessContextResolver
-> EfAiScopeStore (فقط برای شناسه learner نقش Student)
-> IAiServiceClient
-> AiServiceClient
-> Makan AI /api/v1/ai/chat
```

`AiChatService` فقط workspace سازمانی با نقش جاری Student/Parent/Teacher/Manager را می‌پذیرد. `schoolId` در schema فعلی AI برابر `OrganizationId` است. برای Student، `studentId` از `OrganizationPerson.Id` فعال resolve می‌شود؛ برای Parent فقط child از قبل انتخاب‌شده و دوباره اعتبارسنجی‌شده استفاده می‌شود.

Generic Chat در این مرحله هیچ grade، assignment، exam، attendance، messenger history یا private AI history ارسال نمی‌کند. `AiAuthorizedContext.Empty` انتخاب آگاهانه حداقل‌سازی است؛ contextهای آموزشی structured در 10E ساخته می‌شوند.

## Availability و Rate Limit

- `AiService:Enabled=false` پیش‌فرض است و فقط AI را با 503 خاموش می‌کند.
- policy اختصاصی `ai-chat` بر اساس UserId احراز‌شده، 20 درخواست در دقیقه در محیط غیر Testing دارد.
- POST retry خودکار ندارد.

## خطاهای عمومی

- ورودی نامعتبر: 400
- workspace/role/learner نامعتبر: 403
- rate limit: 429
- response contract نامعتبر: 502
- disabled/unavailable/service-auth failure: 503
- timeout: 504

جزئیات FastAPI/provider و body خطای upstream به client منتقل نمی‌شود.

## امنیت

- session کاربر opaque باقی مانده و هرگز به AI forward نمی‌شود.
- service JWT مستقل توسط adapter اضافه می‌شود.
- client نمی‌تواند role یا tenant را override کند.
- تعویض workspace در حساب چندسازمانی، scope درخواست بعدی را از state معتبر سرور تغییر می‌دهد.
