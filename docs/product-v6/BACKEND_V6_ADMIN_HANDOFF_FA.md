# تحویل عملیاتی Admin برای Backend v6

بازبینی `2026-11-12`؛ مبنای Source: `e5bd3eae658991585ca09f581f6236351857b3b8` + شاخهٔ `feature/product-v6-backend-alignment`. هیچ Deploy یا تغییر دیتابیس عادی در این Task انجام نشده است. این سند به‌جای تعریف نقش جدید، permissionهای واقعی را توضیح می‌دهد.

## مجوزها و مرز مسئولیت

| Permission جدید | مسئولیت | تخصیص اولیه |
|---|---|---|
| `platform.school_applications.read` | صف/جزئیات پروندهٔ قابل‌بررسی آموزشگاه | PlatformOwner |
| `platform.school_applications.review` | شروع بررسی/ثبت تصمیم | PlatformOwner؛ همراه Read لازم است |
| `platform.support.read` | صف/جزئیات پروندهٔ Support | PlatformOwner و نقش Support موجود |
| `platform.support.manage` | Reply، Assign و Resolve | PlatformOwner و Support موجود؛ همراه Read لازم است |
| `platform.privacy_requests.read` | صف metadata درخواست حذف حساب | PlatformOwner |

School Manager به‌واسطهٔ نقش سازمانی این permissionها را نمی‌گیرد. IAM موجود مسئول واگذاری و ابطال است؛ هیچ MFA یا IAM موازی ساخته نشده است. Support حتی با این مجوزها حق مرور Direct، Address Book، فایل عمومیِ متعلق به دیگری یا همهٔ مدارک کاربران را ندارد. فایل پرونده فقط با ACL همان پرونده باز می‌شود و با revoke مجوز، دسترسی آینده قطع می‌شود.

## درخواست آموزشگاه

| Method / Route | ورودی و رفتار |
|---|---|
| `GET /api/admin/v1/school-applications` | `status?`, `page`, `pageSize`؛ Draft خصوصی متقاضی از صف Reviewer پنهان است |
| `GET /api/admin/v1/school-applications/{id}` | `revision?`؛ snapshot همان نسخه و feedback ثبت‌شده؛ فایل فقط از مسیر FileAsset با ACL |
| `POST /api/admin/v1/school-applications/{id}/review` | `clientOperationId`, `expectedVersion`؛ فقط وضعیت مناسب وارد UnderReview می‌شود |
| `POST /api/admin/v1/school-applications/{id}/decisions` | همان شناسه/نسخه و `decision`, `applicantFeedback`؛ تصمیم NeedsChanges/Approved/Rejected با feedback حداکثر۲۰۰۰ |

نسخهٔ قدیمی ۴۰۹ است؛ refresh وضعیت و عملیات جدید لازم است. retry همان عملیات با payload یکسان اثر جدید نمی‌سازد. تاریخچهٔ نسخه و تصمیم append-only است. نام، سابقه، نمونه‌کار و اطلاعات مدارک در Audit/Push قرار نمی‌گیرند.

**به‌روزرسانی `2026-11-13`:** Submit دو نوع با الزامات مصوب فعال است. Review برای همهٔ اقدامات دلیل می‌خواهد؛ Cancel/History و نگاشت خدمت با مجوز اضافی Commerce وجود دارند. Approval هنوز پرداخت یا سازمان نمی‌سازد؛ Quote/Order/Zibal/Outbox موجود اکنون correlation پرونده دارند. نبود تنظیم قیمت و سیاست مدیر اولیه دو محدودیت جدا هستند؛ intent پرداخت‌شده تا تعیین مدیر، سازمان/اشتراک نمی‌سازد. راهنمای صف/مدرک/تنظیم تجاری/خطا در [Handoff Admin آموزشگاه](SCHOOL_APPLICATION_ADMIN_HANDOFF_FA.md) است. هیچ Draft با SQL دستکاری نشود و قیمت/Quota حدس زده نشود.

## پشتیبانی مستقل

`GET /api/admin/v1/support/tickets` صف metadata دارد؛ بازکردن `/{id}` متن همان پرونده را می‌دهد. برای پیام‌های بیشتر `afterMessageVersion` را از `nextMessageVersion` ادامه دهید. مسیرهای POST زیر، `clientOperationId` و `expectedVersion` دارند:

- `/{id}/messages`: `body`, `fileAssetIds?`؛ پاسخ پرونده و version تازه.
- `/{id}/assign`: `reason`, `assignedStaffUserId`؛ مقصد باید حساب فعال با Read+Manage جاری باشد.
- `/{id}/resolve`: `reason`؛ وضعیت Resolved نهایی است و Reply آن را خودکار reopen نمی‌کند.

کاربر فقط پروندهٔ خودش را می‌بیند. فایل باید آماده، متعلق به فرستنده و به همین پرونده متصل باشد؛ لینک مستقیم FileAsset حق دورزدن این مرز را نمی‌دهد. Resolve، عملیات Moderation، تأیید رسانهٔ `ReviewRequired` یا حذف پیام خصوصی نیست. مشاهده/تغییر متناسب پرونده Audit دارد؛ متن خصوصی در Audit ثبت نمی‌شود. اختلاف نسخه ۴۰۹ و Resource غیرمجاز ۴۰۴ است.

## صف حذف حساب و تغییر شماره

`GET /api/admin/v1/privacy/deletion-requests?page=1&pageSize=50` تنها شناسه/وضعیت/زمان درخواست را نشان می‌دهد. هیچ Execute/Anonymize endpoint یا زمان‌بندی وجود ندارد. `PRODUCT_LEGAL_DECISION_REQUIRED` به‌معنی درخواست ثبت‌شده و اجرای غیرفعال است؛ سیاست‌های مالی، Audit، پیام، آموزش، اسناد و AI باید پیش از حذف تصویب شوند. زمان نگه‌داری یا استثنا با SQL/تنظیم حدسی تعیین نشود.

تغییر شماره متعلق به صاحب حساب و OTP واقعی شمارهٔ جدید است؛ Admin راه میان‌بر Credential ندارد. موفقیت `identity.PhoneChanged` و تلاش ناموفق `identity.PhoneChangeVerificationFailed` با شناسهٔ درخواست، Actor/Session و correlation ثبت می‌شوند؛ شماره، OTP، Hash یا Token در Audit نیست. UserId، پروفایل، عضویت‌ها و تاریخچه حفظ و سایر نشست‌ها/Refreshها باطل می‌شوند. تحویل نامطمئن SMS با همان OperationId resend نمی‌شود؛ آماده‌نبودن provider واقعی پاسخ503 دارد، حتی اگر Mock Login فعال باشد.

## عملیات و مهاجرت

Migrationهای این مرحله افزودنی‌اند و فقط روی LocalDBهای GUIDدار disposable تست می‌شوند. هیچ‌کدام روی `MakanApp`، `MakanApp_Test` یا سرور اجرا نشده‌اند. پیش از Deploy بعدی، Backup معتبر، فهرست Migrationهای اعمال‌شده، SQL تولیدشده از همین Source و config محیط باید جدا بررسی شوند. Startup Migration نداریم.

Test و Production همچنان Storage جدا دارند: `C:\MakanAppData\Test\Storage` و `C:\MakanAppData\Production\Storage`. فایل‌های private و secretهای فعلی تغییر نکرده‌اند. Data Protection دفترچه باید ring پایدار با ACL محدود و Backup/Restore داشته باشد؛ Test/Production ring یا private config مشترک نگیرند. تغییر purpose رمزنگاری یا `Identity:SecurityKey` بدون برنامهٔ بازسازی شاخص مجاز نیست. برای keyring، زیرساخت Data Protection موجود reuse شده و هیچ مقدار کلید در Git وارد نشده است.

`Features:PersonalClass:AssignmentsEnabled=false` و `ExamsEnabled=false` پیش‌فرض محصول‌اند؛ permission قدیمی نباید آن را دور بزند. School Assignment/Exam برقرار است. فعال‌سازی این دو فقط Task/تصمیم محصول بعدی است. AI و Knowledge `UseServiceAuthentication=false` حفظ شده‌اند؛ credential یا JWT تازه لازم نیست و تست این Task تماس واقعی AI ندارد.

Notificationهای جدید روی Outbox موجود و transaction همان owner نوشته می‌شوند؛ target در تحویل/خواندن مجدد مجوز می‌گیرد. Push واقعی بدون تنظیم امن و دستگاهِ تأییدشده `CONFIGURATION_PENDING` است؛ اعلان OTP یا SMS عمومی ساخته نشده است. Zibal موجود در Regression حفظ می‌شود؛ خرید Store، iOS و Refund از موفقیت تست Zibal نتیجه نمی‌شوند.

مرجع محدودیت‌ها: [تصمیم‌های باز](BACKEND_V6_OPEN_DECISIONS_FA.md). مرجع نتیجهٔ تست/مهاجرت/فایل‌ها: [گزارش پیاده‌سازی](BACKEND_V6_IMPLEMENTATION_REPORT_FA.md).
