# موانع و تصمیم آمادگی Release جاری

به‌روزرسانی `2026-11-18`؛ بازپذیرش AI/Knowledge از Source پایه
`f20cfcbbf381d1adde1ad1a9d3471ea9d064088e`؛ سابقهٔ پیگیری قبلی از `f704a5c` در گزارش محفوظ است.
این سند وضعیت **قابل‌اثبات در مخزن اصلی و محیط اجرای محلی** را گزارش می‌کند؛ از نبود تنظیم
محلی، دربارهٔ مقادیر واقعی سرور نتیجه‌گیری نمی‌کند. معیار محدوده در
[Scope Freeze](CURRENT_RELEASE_SCOPE_FA.md) است؛ شواهد کد/تست در [Audit](FINAL_BE01_BE25_AUDIT_FA.md).

## موانع Configuration قابلیت‌های داخل محدوده

| شناسه | وضعیت | شاهد امن | اثر و اقدام دقیق |
|---|---|---|---|
| CFG-01 | `BLOCKED_CONFIGURATION` | Test هنوز Provider=`Unavailable` دارد؛ چهار مقدار Merchant/Callback/ClientReturn/Allowlist در فایل خصوصی `MISSING` هستند | تنها ورودی نامعلوم `Payments:Zibal:ClientReturnUrl` است. Origin واقعی Backend Test در سند استقرار موجود است؛ Merchant تست و BaseUrl هم قرارداد صریح دارند. پس از دریافت URL واقعی کلاینت، مجموعهٔ تنظیمات درگاه باید یکجا کامل و `ZibalOptions.Validate("Test", true)` اجرا شود. مقدار ساختگی یا فعال‌سازی ناقص انجام نشد. |
| CFG-02 | `CLOSED_LOCAL_TEST_RUNTIME` | هر دو Runtime در private Test فعال‌اند؛ BaseUrlها `CONFIGURED`؛ Personal Chat، Ingest/Ready/Retrieve/RAG واقعی موفق | هر دو Service Auth خاموش، timeout برابر35s و حدود پردازش ثابت‌اند. Startup در Environment=`Test` و Health/Ready در میزبان ایزوله200 شدند. این شاهد از Backend محلی به AI مستقر است؛ اتصال از IIS مقصد هنوز OP-02 است. |

در بازپذیرش `2026-11-18`، Health/Ready مستقیم200، Personal Backend→AI200 با یک مصرف و
replay409 و Knowledge Ingest/Ready/Retrieve/RAG دوباره موفق شدند؛ حساب دیگر404 گرفت و فقط
QA تازه پاک‌سازی شد. هیچ فایل config تغییر نکرد. Focused برابر1547 PASS و هر دو Full/Final
برابر3461 PASS با صفر Failed/Skipped و Warning/Error شدند. Scope rejection، نقش‌های سازمانی،
Usage/Quota، ACL و Archive/Revoke با۱۵ متد پذیرش لازم در هر سه دور تأیید شدند؛
`TEST_AI_RUNTIME=PASS` و `TEST_KNOWLEDGE_RUNTIME=PASS`. مرز دقیق شاهد و Backup/hash در
[گزارش Runtime](../operations/TEST_RUNTIME_READINESS_FA.md) ثبت است.

مالک صریحاً تأیید کرده ClientReturnUrl واقعی Frontend Test هنوز مشخص/Deploy نشده است؛ CFG-01
عمداً باز می‌ماند. Callback شناخته‌شدهٔ Backend جای مقصد کلاینت نیست و Zibal در این Task تغییر نمی‌کند.

تعداد مانع Configuration لازم برای Test Deploy اکنون **1** است؛ فیلدهای ناقص یک درگاه، چهار
مانع جدا شمرده نمی‌شوند. نبود تنظیم Production در این شمارش وارد نمی‌شود. جزئیات اجرا،
محدودیت شاهد و گزارش توسعه‌دهنده در [Test Runtime Readiness](../operations/TEST_RUNTIME_READINESS_FA.md) است.

## موارد مشروط که کل Release را مسدود نمی‌کنند

| مورد | وضعیت | مرز |
|---|---|---|
| Zibal در Production | `BLOCKED_CONFIGURATION` | تنظیم واقعی Production آماده نیست؛ فایل خصوصی آن byte-for-byte حفظ شد. این مورد مانع مستقل Test Deploy نیست. |
| AI/Knowledge در Production | `OPERATIONAL_ACTIVATION_PENDING` | Runtime خصوصی Production فعال نشد؛ موفقیت اجرای محلی Test به معنی فعال‌سازی آن نیست. |
| قیمت/Plan واقعی فروش آموزشگاه | `BLOCKED_CONFIGURATION` | فقط School Sales؛ Workflow و Provisioning با دادهٔ تست و gateway کنترل‌شده قابل پذیرش‌اند. هیچ قیمت/تاریخ/Quota واقعی از روی نمونه ساخته نمی‌شود. |
| اجرای حذف حساب | `BLOCKED_CONFIGURATION` | برای Release اول می‌تواند خاموش بماند؛ درخواست/پیگیری Privacy موجود است. |
| Retention/Legal hold/Backup durations | `PRODUCT_LEGAL_CONFIGURATION_REQUIRED` | مانع فعال‌سازی اجرای حذف یا purge حقوقی، نه API درخواست حذف و دیگر قابلیت‌های این نسخه. |
| Push واقعی و SMS عمومی اعلان | `DEFERRED_CURRENT_RELEASE` | نیاز VAPID/FCM/device یا provider SMS در این نسخه blocker نیست. |
| Store Billing، iOS، Family، PersonalClass Academic | مطابق Scope مصوب | قابلیت آینده‌اند؛ عدم پیاده‌سازی جدیدشان failure این Audit نیست. |
| Library عمومی/AI | `PASS_CURRENT_POLICY` | خاموشی Product مصوب است؛ مجوز موجود Platform تغییر نمی‌کند. |
| تصمیم رسانهٔ ReviewRequired | `PRODUCT_DECISION_REQUIRED` | وضعیت امن و قابل‌ردیابی فعلی حفظ می‌شود؛ تأیید ارسال رسانه ساخته یا Safety bypass نمی‌شود. |

## پذیرش‌های عملیاتی باقی‌مانده

سه پذیرش Test باقی است: OP-01 نسخهٔ نصب‌شده/Health/Smoke، OP-02 اتصال از Backend مقصد به
AI/Knowledge، OP-03 رفت‌وبرگشت و Callback درگاه Test. اجرای محلی این Task، Deploy یا پذیرش
همان Windows Server نیست. چهار پذیرش Production یعنی Ring/Restart، Storage/ACL، Restore و
exporter/alert جدا باقی‌اند و در شمارش Test وارد نمی‌شوند.

```text
PRODUCTION_DATA_PROTECTION_RING=OPERATIONAL_VALIDATION_PENDING
PRODUCTION_STORAGE=OPERATIONAL_VALIDATION_PENDING
PRODUCTION_RESTORE_DRILL=NOT_RUN
PRODUCTION_OBSERVABILITY=OPERATIONAL_VALIDATION_PENDING
CURRENT_RELEASE_CODE_GAPS=0
CURRENT_RELEASE_CONFIGURATION_BLOCKERS=1
CURRENT_RELEASE_OPERATIONAL_VALIDATIONS=3
MERGE_TO_MAIN=NOT_RUN
DEPLOYMENT=NOT_RUN
```

## تصمیم

با تنظیمات خصوصیِ قابل‌مشاهدهٔ فعلی، نتیجهٔ آمادگیِ نسخهٔ **تنظیم‌شده برای پذیرش همهٔ قابلیت‌های
داخل محدوده** برابر `NOT_READY_FOR_TEST_DEPLOY` است: CFG-01 به URL واقعی بازگشت کلاینت
نیاز دارد؛ CFG-02 با شاهد واقعی محلی بسته شد. سلامت کد، Regression و استقرار مقصد ادعاهای
جدا هستند. پرداخت مالی واقعی و Full-12 در این Task اجرا نشده‌اند.

هیچ عدد تجاری، Secret یا URL حدسی ساخته نشد. تنها فلگ‌های مصوب AI/Knowledge در فایل خصوصی
Test فعال شدند؛ حدود پردازش و سایر مقادیر خصوصی محفوظ‌اند. اقدام بعدی دریافت URL واقعی،
validation کامل درگاه و سپس Task صریح بسته/استقرار Test است.

## USER_INPUT_REQUIRED

`Payments:Zibal:ClientReturnUrl`
