# STEP 16 — Production Hardening

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## تصمیم‌های معماری

احراز هویت همچنان opaque bearer و server-authoritative است. access token کوتاه و قابل revoke است؛ refresh token تصادفی فقط یک بار مصرف می‌شود، raw آن ذخیره نمی‌شود و hash/HMAC آن در SQL Server قرار می‌گیرد. replay یک token مصرف‌شده کل session family را revoke و security event امن ثبت می‌کند.

`UserSession` زمان انقضای access و absolute، device label، `AssuranceLevel` و `StepUpAtUtc` را نگه می‌دارد. ورود OTP فقط assurance سطح Primary است. چون provider مستقل MFA تأیید نشده، `UnavailablePlatformStepUpAuthorization` حفظ شده و mutationهای حساس Platform fail-closed می‌مانند.

Dependency flow:

```text
HTTP AuthController
-> IdentityService
-> Domain UserSession / RefreshToken
-> IIdentityStore / IIdentitySecurity
-> EfIdentityStore / IdentityCryptography
-> SQL Server
```

Health check قرارداد hosting است و در Api قرار دارد؛ برای SQL از `MakanDbContext` و برای storage از `IStorageReadinessProbe` استفاده می‌کند. metrics با `System.Diagnostics.Metrics` vendor-neutral هستند. configuration حیاتی Production پیش از ساخت host fail-fast می‌شود؛ providerهای اختیاری که نبودشان نباید core را خاموش کند، unavailable/fail-closed باقی می‌مانند.

## امنیت HTTP و فایل

- trusted proxy allowlist، `ForwardLimit=1`، HTTPS/HSTS فقط Production.
- CORS Production با origin صریح HTTPS؛ Swagger پیش‌فرض خاموش.
- security headers و correlation ID بدون پذیرش مقدار کنترل‌نشده.
- rate limit برای OTP request/verify، refresh، discovery، payment، upload، report export و mutationهای admin.
- filename server-safe، extension/content-type mapping و signature حداقلی PDF/JPEG/PNG/MP4/MP3/UTF-8 text.
- download با attachment content disposition، authorization موجود و range processing.

## پایداری و performance

Outbox اعلان و realtime پس از ۱۰ attempt دیگر claim نمی‌شوند؛ batch، lease، retry delay و cancellation موجود حفظ شده‌اند. report و notification نیز سقف مستقل تنظیم‌شده دارند. جزئیات Plan حداکثر ۱۰۰ version و جزئیات Class حداکثر ۲۰۰ teacher و ۲۰۰ learner برمی‌گرداند؛ flagهای `VersionsTruncated`، `ActiveTeachersTruncated` و `ActiveLearnersTruncated` truncation را آشکار می‌کنند.

## Persistence

Migration `AddRefreshTokenRotationAndSessionAssurance` جدول `identity.RefreshTokens`، hash یکتا، FK session، self-reference replacement، rowversion، lifecycle/expiry checks و index session-expiry را می‌سازد. ستون‌های session با backfill کنترل‌شده اضافه می‌شوند و SQL default دائمی ندارند.

## موارد عمداً حل‌نشده

MFA، Payment و Child Safety provider و policy گروه Minor تصمیم بیرونی می‌خواهند. AV/CDR، distributed rate limiting، telemetry exporter، backup job، restore drill و load baseline مسئولیت integration/operations بعدی هستند. AI integration و Landing در این مرحله شروع نشده‌اند.
