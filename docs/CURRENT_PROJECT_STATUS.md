# وضعیت جاری پروژه و مسیر مطالعه اسناد

بازبینی `2026-11-26` از `source@f8178a47de94eae0ec2431adebcb9256a9789786` روی `feature/product-v6-backend-alignment`.

**۴۹۸ عملیات HTTP،۴۴۲ مسیر،۹۱ Controller،۷۹۶ schema،۱۵۳ جدول و۷۸ Migration** از OpenAPI/ApiExplorer و مدل طراحی EF استخراج شد. آخرین Migration `20261122085930_AddMessagingMediaReviews` است؛ استخراج بدون SQL connection/worker/SMS/AI انجام شد. این شمارش، applied schema یا نسخهٔ سرور نیست.

## تغییرات مهم نسبت به کتاب‌های قبلی

| حوزه | قرارداد جاری و مسیر مطالعه |
|---|---|
| دسترسی | Commerce:EnforcementEnabled=false و AI Unmetered؛ سن gate عمومی نیست، ACL/Privacy/Block/Safety محفوظ؛ [Open Access](product/OPEN_ACCESS_FILE_DELIVERY_FA.md) |
| AI | Chat/Profile اختیاری، History مستقل Owner/Context و SSE واقعی؛ RAG غیرجریانی، SourceVersion/citations و Retry پایدار؛ [Adaptive](product-v6/ADAPTIVE_AI_FRONTEND_HANDOFF_FA.md) |
| فایل پیام | Ready≠Approval؛ ManualMediaReview، تصمیم کارشناس اثرانگشت‌دار، Send مجدد صریح و Download با ACL؛ [رسانه](messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md) |
| تکلیف | PATCH Published submission-policy با چهار فیلد اجباری و دو RowVersion؛ deadline metadata، حفظ Draft و isLate تاریخی؛ [قرارداد](product/ASSIGNMENT_SUBMISSION_POLICY_FRONTEND_HANDOFF_FA.md) |
| HTTP | code پایدار خطاهای400/404/405/413/415، حفظ کد دامنه و Retry-After معتبر AI/Knowledge روی429/503؛ [ممیزی](../reports/api-error-audit/20261125-api-errors-ac9a239-01/FRONTEND_ERROR_HANDOFF_FA.md) |
| حساب و مدرسه | Bio/DOB/Avatar، تغییر شماره، مدیریت نقش و آخرین مدیر، School application و مدیر اولیهٔ صریح؛ [v6](product-v6/BACKEND_V6_FRONTEND_HANDOFF_FA.md) |
| PersonalClass | درخواست/تأیید، نقش محلی، عضویت، انتقال مسئولیت و Official Spaces؛ آموزش مستقل با Feature flag خاموش |
| Messenger | Home حساب و History SQL مستقل از سوییچ Workspace، Replay صندوق و scoped جدا، Safety/Privacy جاری؛ [History](product-v6/MESSENGER_HISTORY_FRONTEND_HANDOFF_FA.md) |
| Source / Library | چرخهٔ منبع/نسخه/Knowledge و ACL جاری؛ Library عمومی/AI بسته؛ [قرارداد](product-v6/FILES_LIBRARY_FRONTEND_HANDOFF_FA.md) |
| Notification / Privacy | target با مجوز فعلی؛ حذف حساب پایدار با checkpoint/recovery و execution پیش‌فرض خاموش؛ [اعلان](product-v6/NOTIFICATION_FRONTEND_HANDOFF_FA.md)، [حذف](product-v6/ACCOUNT_DELETION_FRONTEND_HANDOFF_FA.md) |

## مرز انتشار و عملیات

پرداخت Runtime شامل Zibal، Paid Provisioning، Store Billing و Push بیرونی Deferred هستند؛ کد و تست‌هایشان حفظ‌اند. PersonalClass AssignmentsEnabled/ExamsEnabled=false و Library برای دانلود/AI عمومی بسته‌اند. Open Access این gateها را روشن نمی‌کند. [Scope](product-v6/CURRENT_RELEASE_SCOPE_FA.md) و [آمادگی](production/PRODUCTION_READINESS.md).

Enabled و URLهای واقعی AI/Knowledge از config هر محیط می‌آید؛ Service Auth دو سرویس طبق سیاست جاری false است. پذیرش‌های Test محلی تاریخ‌دار در [Runbook Test](operations/TEST_RUNTIME_READINESS_FA.md) هستند. این به‌روزرسانی Deploy/Publish، فعال‌سازی Provider، SQL عادی/سرور یا تغییر Frontend انجام نداده است.

DB و Blob و SourceVersion و Evidence رسانه و AI History باید سازگار backup شوند. حذف حساب و terminal cleanup دو workflow مستقل و نیازمند config مصوب‌اند؛ terminal cleanup مجوز purge SQL نیست. Data Protection استاندارد برای Replay cursor از Identity key جداست؛ استمرار ring، backplane، Load/SLO، TLS/Restore/alert مقصد مستقل پذیرفته شوند.

## مسیر مطالعه و خروجی‌ها

[راهنمای بک‌اند](backend-api-guide.md) جریان و معماری را توضیح می‌دهد؛ [مرجع API](backend-api-reference.md) Method/Route/DTO را کامل فهرست می‌کند. سپس [قرارداد فرانت](frontend-contract/README.md)، Handoff تخصصی جدول بالا، [فرهنگ دیتابیس](database-current-structure.md) و [استقرار](deployment-windows-linux-docker.md) را بخوانید. HTML/PDF دو کتاب و قرارداد فرانت از متن‌های همین بازبینی بازسازی می‌شوند؛ HTML/PDF دیتابیس طبق ignore قبلی محلی می‌مانند.

## Validation این بازبینی

Restore و Build اولیه/نهایی موفق با صفر Warning/Error؛ هر دو دور کامل 3789 تست موفق (1499 Unit، 2274 Integration و 16 Architecture)، صفر Failed/Skipped.۱۴ آزمون ابزار، تطابق EF/Migration، لینک‌ها و PDF/HTML نیز موفق‌اند. [گزارش کامل و فایل‌های تغییرکرده](DOCUMENTATION_UPDATE_REPORT.md). اعداد آزمون و وضعیت‌های تاریخ‌دار اسناد مرحله‌ای، نتیجهٔ اجرای امروز نیستند. [فهرست کامل اسناد](documentation-inventory.csv) مسیر و طبقه‌بندی فایل‌ها را نگه می‌دارد.
