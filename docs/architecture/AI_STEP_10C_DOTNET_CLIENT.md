# STEP 10C — .NET AI Service Client

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

`IAiServiceClient` در Application مرز use case با AI است. `AiServiceClient` در Infrastructure با `HttpClientFactory` درخواست typed را به `/api/v1/ai/chat` می‌فرستد، Service JWT را اضافه می‌کند و correlation/contract version را منتقل می‌کند.

POST به‌صورت خودکار retry نمی‌شود. timeout و cancellation از هم تفکیک شده‌اند و یک circuit breaker process-local پس از failureهای transient متوالی، callهای جدید را برای زمان محدود fail-fast می‌کند.

پاسخ فقط با 2xx پذیرفته نمی‌شود؛ JSON و invariantهای response شامل request id، answer، collectionها، metadata، confidence و source نیز بررسی می‌شوند. body خطای upstream به caller یا log منتقل نمی‌شود.

قابلیت با `AiService:Enabled=false` به‌صورت پیش‌فرض خاموش است. URL، timeout، contract version و circuit policy تنظیم‌شدنی‌اند. secret احراز هویت در `appsettings.json` خالی است و باید از secret store/environment بیاید.
