# موانع و تصمیم آمادگی Release جاری

بازبینی `2026-11-26` از `source@f8178a47de94eae0ec2431adebcb9256a9789786`.

> سیاست جاری `2026-11-26`: [Open Access](../product/OPEN_ACCESS_FILE_DELIVERY_FA.md) با Commerce:EnforcementEnabled=false شرط تجاری استفادهٔ عادی و ذخیرهٔ Source را حذف کرده است؛ AI Unmetered و OperationId/ACL برقرارند. پرداخت/Push بیرونی و PersonalClass Academic مستقل Deferred هستند. [Media Review](../messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md) اکنون تصمیم انسانی مصوب دارد؛ Pending/Rejected تحویل ندارد و Approval خودکار پیام نمی‌فرستد. شواهد و اعداد تاریخی این سند متعلق به زمان خود هستند.

به‌روزرسانی سیاست مالک `2026-11-18` از Source پایه
`d397d73c5f532f7492614e95925ceaa1850a8150`؛ [گزارش AI نامحدود](AI_UNMETERED_CURRENT_RELEASE_FA.md)
مرجع تغییر این مرحله و شواهد نهایی آن است. پرداخت برای نسخهٔ جاری Deferred است.
این سند وضعیت **قابل‌اثبات در مخزن اصلی و محیط اجرای محلی** را گزارش می‌کند؛ از نبود تنظیم
محلی، دربارهٔ مقادیر واقعی سرور نتیجه‌گیری نمی‌کند. معیار محدوده در
[Scope Freeze](CURRENT_RELEASE_SCOPE_FA.md) است؛ شواهد کد/تست در [Audit](FINAL_BE01_BE25_AUDIT_FA.md).

## موانع Configuration قابلیت‌های داخل محدوده

| شناسه | وضعیت | شاهد امن | اثر و اقدام دقیق |
|---|---|---|---|
| CFG-01 | `NOT_REQUIRED_CURRENT_RELEASE` | هر دو Provider=`Unavailable`؛ کد و Regression زیبال محفوظ | تمام پرداخت‌ها Deferred هستند. Merchant/Callback/ClientReturn/Allowlist فقط برای انتشار آیندهٔ پرداخت باید یکجا کامل و validate شوند؛ نبودشان مانع Deploy این نسخه نیست. مقدار ساختگی ساخته نمی‌شود. |
| CFG-02 | `CLOSED_LOCAL_TEST_RUNTIME` | هر دو Runtime در private Test فعال‌اند؛ BaseUrlها `CONFIGURED`؛ Personal Chat، Ingest/Ready/Retrieve/RAG واقعی موفق | هر دو Service Auth خاموش، timeout برابر35s و حدود پردازش ثابت‌اند. Startup در Environment=`Test` و Health/Ready در میزبان ایزوله200 شدند. این شاهد از Backend محلی به AI مستقر است؛ اتصال از IIS مقصد هنوز OP-02 است. |

در بازپذیرش **پیش از تغییر سیاست** در `2026-11-18`، Health/Ready مستقیم200، Personal Backend→AI200 با یک مصرف و
replay409 و Knowledge Ingest/Ready/Retrieve/RAG دوباره موفق شدند؛ حساب دیگر404 گرفت و فقط
QA تازه پاک‌سازی شد. هیچ فایل config تغییر نکرد. Focused برابر1547 PASS و هر دو Full/Final
برابر3461 PASS با صفر Failed/Skipped و Warning/Error شدند. Scope rejection، نقش‌های سازمانی،
Usage/Quota، ACL و Archive/Revoke با۱۵ متد پذیرش لازم در هر سه دور تأیید شدند؛
`TEST_AI_RUNTIME=PASS` و `TEST_KNOWLEDGE_RUNTIME=PASS`. مرز دقیق شاهد و Backup/hash در
[گزارش Runtime](../operations/TEST_RUNTIME_READINESS_FA.md) ثبت است.

ClientReturnUrl واقعی Frontend Test هنوز مشخص/Deploy نشده است؛ تصمیم جدید مالک، CFG-01 را
از Current Release خارج می‌کند. Callback شناخته‌شده جای مقصد کلاینت نیست؛ Zibal فعال نمی‌شود.

تعداد مانع Configuration لازم برای Test Deploy اکنون **0** است؛ تنظیم پرداخت Deferred در
این شمارش وارد نمی‌شود. نبود تنظیم Production هم در این شمارش وارد نمی‌شود. جزئیات اجرا،
محدودیت شاهد و گزارش توسعه‌دهنده در [Test Runtime Readiness](../operations/TEST_RUNTIME_READINESS_FA.md) است.

## موارد مشروط که کل Release را مسدود نمی‌کنند

| مورد | وضعیت | مرز |
|---|---|---|
| پرداخت Test/Production | `DEFERRED_CURRENT_RELEASE` | تنظیم آینده لازم خواهد بود؛ در این مرحله فقط AiUsage خصوصی تغییر می‌کند و Provider/Secretها محفوظ‌اند. |
| AI/Knowledge در Production | `OPERATIONAL_ACTIVATION_PENDING` | Runtime خصوصی Production فعال نشد؛ موفقیت اجرای محلی Test به معنی فعال‌سازی آن نیست. |
| فروش و Paid Provisioning آموزشگاه | `DEFERRED_CURRENT_RELEASE` | کد و آزمون‌ها حفظ شده‌اند؛ قیمت/تاریخ/Quota و Provisioning رایگان حدسی ایجاد نمی‌شود. |
| اجرای حذف حساب | `BLOCKED_CONFIGURATION` | برای Release اول می‌تواند خاموش بماند؛ درخواست/پیگیری Privacy موجود است. |
| Retention/Legal hold/Backup durations | `PRODUCT_LEGAL_CONFIGURATION_REQUIRED` | مانع فعال‌سازی اجرای حذف یا purge حقوقی، نه API درخواست حذف و دیگر قابلیت‌های این نسخه. |
| Push واقعی و SMS عمومی اعلان | `DEFERRED_CURRENT_RELEASE` | نیاز VAPID/FCM/device یا provider SMS در این نسخه blocker نیست. |
| Store Billing، iOS، Family، PersonalClass Academic | مطابق Scope مصوب | قابلیت آینده‌اند؛ عدم پیاده‌سازی جدیدشان failure این Audit نیست. |
| Library عمومی/AI | `PASS_CURRENT_POLICY` | خاموشی Product مصوب است؛ مجوز موجود Platform تغییر نمی‌کند. |
| تصمیم رسانهٔ ReviewRequired | `CLOSED_IMPLEMENTATION` در 2026-11-22 | مسیر انسانی مصوب در Moderation موجود، تصمیم صریح و اثرانگشت فایل دارد؛ Pending/Rejected ارسال نمی‌شوند. [قرارداد و مرز پذیرش محلی](../messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md). نصب و پذیرش IIS مقصد مستقل باقی است. |

## پذیرش‌های عملیاتی باقی‌مانده

دو پذیرش Test باقی است: OP-01 نسخهٔ نصب‌شده/Health/Smoke و OP-02 اتصال از Backend مقصد به
AI/Knowledge. OP-03 درگاه به انتشار آیندهٔ پرداخت منتقل شد. اجرای محلی این Task، Deploy یا پذیرش
همان Windows Server نیست. چهار پذیرش Production یعنی Ring/Restart، Storage/ACL، Restore و
exporter/alert جدا باقی‌اند و در شمارش Test وارد نمی‌شوند.

```text
PRODUCTION_DATA_PROTECTION_RING=OPERATIONAL_VALIDATION_PENDING
PRODUCTION_STORAGE=OPERATIONAL_VALIDATION_PENDING
PRODUCTION_RESTORE_DRILL=NOT_RUN
PRODUCTION_OBSERVABILITY=OPERATIONAL_VALIDATION_PENDING
CURRENT_RELEASE_CODE_GAPS=0
CURRENT_RELEASE_CONFIGURATION_BLOCKERS=0
CURRENT_RELEASE_OPERATIONAL_VALIDATIONS=2
CURRENT_RELEASE_PAYMENTS=DEFERRED_CURRENT_RELEASE
TEST_PAYMENTS_PROVIDER=Unavailable
TEST_ZIBAL_CONFIGURATION=NOT_REQUIRED_CURRENT_RELEASE
MERGE_TO_MAIN=NOT_RUN
DEPLOYMENT=NOT_RUN
```

## تصمیم

نبود ClientReturnUrl دیگر مانع نسخهٔ جاری نیست. معیار `READY_FOR_TEST_DEPLOY`، موفقیت
Unmetered/ACL/Idempotency، Smoke واقعی محدود، Build/Regression و نبود pending model است؛
هر دو دور کامل۳۴۹۸ تست و Smokeهای واقعی محدود موفق شدند؛ نتیجهٔ جاری
`CURRENT_RELEASE_ACCEPTANCE=READY_FOR_TEST_DEPLOY` است. سلامت کد و آمادگی نصب به معنی Deploy نیست.
پرداخت مالی واقعی و Full-12 در این Task اجرا نمی‌شوند.

هیچ عدد تجاری، Secret یا URL حدسی ساخته نمی‌شود. فقط AiUsage در private Test/Production
به Unmetered تنظیم می‌شود؛ حدود پردازش، Service Auth خاموش و سایر مقادیر خصوصی محفوظ‌اند.
اقدام بعدی پس از Validation سبز، Task صریح بسته/استقرار Test است.

## USER_INPUT_REQUIRED

`NONE` برای Current Release. `Payments:Zibal:ClientReturnUrl` و تنظیمات کامل درگاه فقط
در **Future Payment Release** لازم خواهند بود.
