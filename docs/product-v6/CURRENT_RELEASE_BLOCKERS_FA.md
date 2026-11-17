# موانع و تصمیم آمادگی Release جاری

تاریخ `2026-11-17`؛ Source پایه `14b0271a9d3aea840e23413dacae5300fc68ebed`.
این سند وضعیت **قابل‌اثبات در مخزن اصلی و محیط اجرای محلی** را گزارش می‌کند؛ از نبود تنظیم
محلی، دربارهٔ مقادیر واقعی سرور نتیجه‌گیری نمی‌کند. معیار محدوده در
[Scope Freeze](CURRENT_RELEASE_SCOPE_FA.md) است؛ شواهد کد/تست در [Audit](FINAL_BE01_BE25_AUDIT_FA.md).

## موانع Configuration قابلیت‌های داخل محدوده

| شناسه | وضعیت | شاهد امن | اثر و اقدام دقیق |
|---|---|---|---|
| CFG-01 | `BLOCKED_CONFIGURATION` | در هر دو private config، Provider=`Unavailable`؛ Merchant/Callback/Return زیبال تعیین نشده؛ override محیط Payments هم دیده نشد | Zibal تنها درگاه داخل محدوده است. اپراتور باید `Payments:Provider=Zibal` و بخش معتبر `Payments:Zibal` را در تنظیم خصوصی مقصد فراهم کند؛ نتیجهٔ `ZibalOptions.Validate` و رفت‌وبرگشت واقعی درگاه بعداً بررسی شود. در Test فقط Merchant تستِ مجاز؛ Production Merchant واقعی و HTTPS لازم دارد. هیچ credential در Git/چت لازم نیست. |
| CFG-02 | `BLOCKED_CONFIGURATION` | `AiService:Enabled=false`، `AiKnowledge:Enabled` فاقد مقدار فعال، BaseUrl دانش تعیین نشده و override محیط مرتبط دیده نشد | برای پذیرش فعال BE19، Runtime و URL مصوب هر دو سرویس باید در تنظیم خصوصی مقصد آماده شوند. هر دو `UseServiceAuthentication=false`، timeout35s، Source ACL و Quota حفظ شوند؛ فعال‌سازی یا تست زنده در این ممیزی اجرا نمی‌شود. |

این دو مورد با Deferred Billing/Push جایگزین نشده‌اند. پذیرش کد و transport کنترل‌شده،
`CONFIGURED` بودن مقصد را اثبات نمی‌کند. برای اعلام readiness تنظیم‌شده، فایل خصوصی مقصد یا
شاهد امن override و validation آن لازم است؛ نیازی به افشای مقدار Secret نیست.

## موارد مشروط که کل Release را مسدود نمی‌کنند

| مورد | وضعیت | مرز |
|---|---|---|
| قیمت/Plan واقعی فروش آموزشگاه | `BLOCKED_CONFIGURATION` | فقط School Sales؛ Workflow و Provisioning با دادهٔ تست و gateway کنترل‌شده قابل پذیرش‌اند. هیچ قیمت/تاریخ/Quota واقعی از روی نمونه ساخته نمی‌شود. |
| اجرای حذف حساب | `BLOCKED_CONFIGURATION` | برای Release اول می‌تواند خاموش بماند؛ درخواست/پیگیری Privacy موجود است. |
| Retention/Legal hold/Backup durations | `PRODUCT_LEGAL_CONFIGURATION_REQUIRED` | مانع فعال‌سازی اجرای حذف یا purge حقوقی، نه API درخواست حذف و دیگر قابلیت‌های این نسخه. |
| Push واقعی و SMS عمومی اعلان | `DEFERRED_CURRENT_RELEASE` | نیاز VAPID/FCM/device یا provider SMS در این نسخه blocker نیست. |
| Store Billing، iOS، Family، PersonalClass Academic | مطابق Scope مصوب | قابلیت آینده‌اند؛ عدم پیاده‌سازی جدیدشان failure این Audit نیست. |
| Library عمومی/AI | `PASS_CURRENT_POLICY` | خاموشی Product مصوب است؛ مجوز موجود Platform تغییر نمی‌کند. |
| تصمیم رسانهٔ ReviewRequired | `PRODUCT_DECISION_REQUIRED` | وضعیت امن و قابل‌ردیابی فعلی حفظ می‌شود؛ تأیید ارسال رسانه ساخته یا Safety bypass نمی‌شود. |

## پذیرش‌های عملیاتی باقی‌مانده

هفت مورد مجزای OP-01 تا OP-07 همان ترتیب بخش عملیاتی Scope هستند: Smoke نسخهٔ نصب‌شدهٔ Test،
اتصال Backend مقصد به AI/Knowledge، Callback واقعی Zibal، Ring/Restart، Storage/ACL، Restore
Production، و exporter/alert. هیچ‌کدام در این Audit به‌عنوان اجرای واقعی سرور PASS نمی‌شوند.

```text
PRODUCTION_DATA_PROTECTION_RING=OPERATIONAL_VALIDATION_PENDING
PRODUCTION_STORAGE=OPERATIONAL_VALIDATION_PENDING
PRODUCTION_RESTORE_DRILL=NOT_RUN
PRODUCTION_OBSERVABILITY=OPERATIONAL_VALIDATION_PENDING
MERGE_TO_MAIN=NOT_RUN
DEPLOYMENT=NOT_RUN
```

## تصمیم

با تنظیمات خصوصیِ قابل‌مشاهدهٔ فعلی، نتیجهٔ آمادگیِ نسخهٔ **تنظیم‌شده برای پذیرش همهٔ قابلیت‌های
داخل محدوده** برابر `NOT_READY_FOR_TEST_DEPLOY` است: CFG-01 و CFG-02 هنوز آماده نیستند.
این نتیجه ادعای خرابی Backend یا عدم امکان نصب باینری نیست؛ دو وابستگی الزامی پذیرش هنوز
پیکربندی نشده‌اند. سلامت کد و Regression جدا در Audit گزارش می‌شود.

هیچ عدد تجاری، Secret، URL عملیاتی، timeout یا فلگ فعال‌سازی برای سبزکردن گزارش حدس زده یا
تغییر داده نمی‌شود. اقدام بعدی فقط آماده‌کردن تنظیم خصوصی همین دو مورد، سپس ساخت بستهٔ جدید
و پذیرش Test توسط اپراتور مجاز است. چهار gate عملیاتی Production و قابلیت‌های Deferred
نباید به‌صورت blocker اضافیِ نصب Test شمرده شوند.
