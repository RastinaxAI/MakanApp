# تصمیم‌های باز Product v6

تاریخ بررسی: `2026-11-13`. مبنا: Product v6، شرح وظایف v6 و پرامپت مصوب School Establishment. این فهرست مجوز فعال‌سازی قابلیت پیشنهادی نیست.

| شناسه | موضوع | وضعیت | رفتار تا تعیین تکلیف |
|---|---|---|---|
| V6-D03 | مدت نگه‌داری Tombstone و رسیدهای Sync دفترچه و اثر حذف از دفترچهٔ اصلی گوشی بر cloud | `PRODUCT_DECISION_REQUIRED` | عدد TTL، purge یا حذف ابری خودکار از نبود مخاطب در snapshot گوشی اختراع نشود؛ Delete صریح cloud شناسه را Tombstone می‌کند و دستگاه قدیمی حق احیایش را ندارد. |
| V6-D04 | Retention/استثنا و روش اجرای حذف Identity، Contacts، Messaging، دادهٔ آموزشی، مدارک مدرسه، مالی، Audit و AI/vector | `PRODUCT_LEGAL_DECISION_REQUIRED` | ثبت و پیگیری درخواست ممکن است؛ زمان اجرا، anonymization یا حذف خودکار فعال نشود. |
| V6-D05 | اشتراک Student/Parent: چند فرزند، pooling/splitting، تعداد والد و پایان رابطه | `PRODUCT_DECISION_REQUIRED` | هویت/نشست/پیام/دفترچه/رسیدها جدا بمانند؛ سهمیه یا انتقال مالکیت حدسی ایجاد نشود. |
| V6-D06 | Billing مربوط به iOS | `PRODUCT_DECISION_REQUIRED` | روش پرداخت یا bypass سیاست Store فرض نشود. |
| V6-D08 | دامنهٔ گزارش آموزشی مستقل PersonalClass در محصول نهایی | `PRODUCT_DECISION_REQUIRED` | گزارش سازمانی فعال بماند؛ گزارش پیشنهادی مستقل به‌صورت خودکار فعال نشود. |
| V6-D09 | Restore/Reopen کلاس آرشیوشده و ظرفیت/تجارت مرتبط | `PRODUCT_DECISION_REQUIRED` | مسیر فعلی Archive حفظ شود؛ احیای خودکار عضویت، مجوز یا مصرف ساخته نشود. |
| V6-D10 | Reopen تیکت Resolved و اختیار کاربر برای بستن نهایی | `PRODUCT_DECISION_REQUIRED` | Create/Read/Reply و Assign/Resolve مجاز موجود از بازگشایی جدا بمانند؛ Reply روی پروندهٔ نهایی، بازگشایی ضمنی ایجاد نکند. |
| V6-D13 | سیاست نهایی target v6 دربارهٔ درخواست/stranger برای Unknown/Minor | `PRODUCT_DECISION_REQUIRED` برای رفتار پیشنهادی تازه | سیاست Source مصوب `2026-11-01`، Privacy/Block/Safety حفظ شود؛ Request Messaging تازه یا منع عمومی بر اساس سن احیا نشود. |
| V6-D14 | شکل عضویت/اطلاعیهٔ والد در فضای رسمی کلاس در target v6 | `PRODUCT_DECISION_REQUIRED` برای گسترش پیشنهادی | دسترسی فعلیِ منبع و Guardian جاری حفظ شود؛ عضویت گسترده یا مجوز Direct از آن استنتاج نشود. |
| V6-D15 | الزام سخت‌گیرانه‌تر مثبت‌بودن هر دو ظرفیت آموزشگاه در آینده | `PRODUCT_DECISION_REQUIRED` برای تغییر سیاست جاری | سیاست مصوب فعلی required/integer/non-negative است و صفر پذیرفته می‌شود؛ این تصمیم باز مانع Submit فعلی نیست. |
| V6-D16 | فیلدهای حقوقی و دسته‌بندی دقیق‌تر مدارک آموزشگاه | `PRODUCT_DECISION_REQUIRED` برای گسترش بعدی | حداقل فعلی حقوقی با License/OrganizationDocument و مدرس با Degree، سابقه و نمونه‌کار اجرا می‌شود؛ کد ملی/ثبت/مالیات یا زیرنوع مدرک الزامی حدس زده نشود. |
| V6-D17 | مرجع و روش انتخاب مدیر اولیهٔ آموزشگاه پس از پرداخت | `BLOCKED_PRODUCT_DECISION` | متقاضی مدیر فرض نشود؛ Paid محفوظ و Outbox به همان PlatformProvisioningRequest با مرجع پرونده/Order متصل شود؛ ساخت Organization/Subscription/دعوت تا تعیین سیاست بسته بماند. |
| V6-D18 | Correction/Reopen پرونده بعد از Approved/Rejected/Cancelled | `PRODUCT_DECISION_REQUIRED` | فرم و شرایط تاریخی درجا mutate نشوند؛ چرخهٔ تازهٔ تأیید یا restore اختراع نشود. |

## مواردی که باز نیستند

- V6-D01 با پرامپت School Establishment مورخ `2026-11-13` بسته شد: فیلد/مدرک لازم هر دو نوع و دو ظرفیت نامنفی مشخص‌اند؛ Submit واقعی، NeedsChanges/resubmit، لغو و History فعال‌اند. الزامات جزئی در [Handoff آموزشگاه](SCHOOL_APPLICATION_FRONTEND_HANDOFF_FA.md) است.
- V6-D02 تصویب شد: نشست فعال + OTP واقعی شمارهٔ جدید؛ تغییر اتمیک، حفظ UserId/نشست جاری و ابطال سایر نشست‌ها و Refreshهایشان. Mock ورود در تغییر شماره پذیرفته نمی‌شود.
- V6-D12 تصویب شد: LinkAccount فقط reference خصوصی مالک است؛ پروفایل و Block جاری بررسی می‌شوند و مجوز Direct/ContactsAudience یا تغییر هویتی/سازمانی نمی‌سازد.
- V6-D07 برابر `DEFERRED_APPROVED` است: BE-25 پیشنهادی، هر دو Gate پیش‌فرض false و engine/سوابق محفوظ؛ فعال‌سازی نهایی Task بعدی است.
- سیاست Messaging مصوب `2026-11-01` و دعوت اولیهٔ هفت‌روزه مرجع Source هستند. وضعیت سن به‌تنهایی منع عمومی Messaging ایجاد نمی‌کند؛ PrivacyAudience، Block، عضویت و Safety همچنان اعمال می‌شوند. تصمیم قدیمیِ متعارض به‌عنوان Policy تازه احیا نمی‌شود.
- دسترسی Parent به فضای رسمی کلاس از رابطهٔ جاری Guardian و ACL همان فضای رسمی می‌آید؛ این رابطه دسترسی به Direct خصوصی کودک ایجاد نمی‌کند.
- Refund در این Task `OUT_OF_SCOPE` است؛ اجرای آن ساخته نمی‌شود.
- Service Authentication هر دو AI Gateway و Knowledge طبق override صریح `DISABLED` باقی می‌ماند؛ کلید یا JWT تازه لازم نمی‌شود.
- Minor/Unknown و مسیر `ReviewRequired` بدون تصمیم جدید bypass نمی‌شوند. بستن Moderation پرونده، تأیید Attach فایل نیست.

## موانع اتصال Storeها

اسناد عمومی رسمی بررسی شدند: [درگاه مستندات بازار](https://developers.cafebazaar.ir/fa/guidelines/in-app-billing/)، [SDK رسمی بازار](https://github.com/cafebazaar/Poolakey)، [نمونهٔ رسمی مایکت](https://github.com/myketstore/billing-sample-android) و [پنل توسعه‌دهندگان مایکت](https://developer.myket.ir/). صفحات اصلی مستندات/پنل در ابزار خواندن فقط shell سمت Client نمایش دادند؛ قرارداد server receipt validation از منبع رسمی قابل استخراج نبود. نمونهٔ رسمی مایکت تأیید Server-side و اتصال Developer Payload به intent/account را توصیه می‌کند؛ این جای schema دقیق API، package/product mapping و تنظیم امن Provider را نمی‌گیرد.

تا دریافت قرارداد رسمی قابل استناد و mapping مصوب package/product/environment و تنظیمات خصوصی، پذیرش واقعی CafeBazaar/Myket گزارش نمی‌شود. از SDKهای غیررسمی برای حدس endpoint، وضعیت خرید یا اعطای Subscription استفاده نمی‌شود. Zibal موجود و زنجیرهٔ Order→Payment→Outbox حفظ می‌شوند.

V6-D11 مانع سازگاری Upstream است، نه مجوز ساخت Role/Organization جعلی: Contract فعلی AI فقط Student/Parent/Teacher/Manager دارد. انتخاب Personal با limitation صریح ذخیره می‌شود ولی Generic Chat شخصی فعال نمی‌شود. `AI_PERSONAL_CONTEXT_UPSTREAM_CONTRACT_REQUIRED` در Handoff ثبت شده است. قرارداد سازگار سرویس AI لازم است؛ Service Auth هر دو سرویس تغییر نمی‌کند.

شمار تصمیم‌های باز Product/Legal در جدول بالا: **۱۳**؛ D01/D02/D12 بسته یا تأیید شده‌اند، D07 به‌صورت مصوب Deferred است و D11 مانع Contract/Configuration جداست. D15/D16 محدودیت‌های توسعهٔ آینده‌اند و Submit جاری را نمی‌بندند. آماده‌نبودن credential/device برای Push نیز تصمیم محصولی تازه نیست.

## تنظیم تجاری آموزشگاه

`SCHOOL_APPLICATION_COMMERCIAL_TERMS=BLOCKED_CONFIGURATION`: هیچ قیمت، دوره یا Quota واقعی Seed نشده است. API نگاشت server-owned دو نوع پرونده به PlanVersion منتشرشدهٔ اختصاصی و معتبر موجود است؛ Platform باید دادهٔ مصوب را با permission تجاری صریح ثبت کند. این نیاز Configuration است و به شمار تصمیم‌های Product/Legal اضافه نشده است. تست‌ها فقط با دادهٔ ساختگی و درگاه کنترل‌شده انجام می‌شوند. سیاست مدیر اولیه مستقل از این نگاشت و در D17 باز است.

## پیش‌نیاز عملیاتی رمزنگاری دفترچه

دفترچه از [Data Protection رسمی ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-9.0) موجود استفاده می‌کند. Key ring باید پایدار، محدود به هویت سرویس، جدا برای Test/Production و همراه Backup/Restore باشد؛ حذف یا گم‌شدن کلید، دادهٔ رمز‌شده را ناخوانا می‌کند. `Identity:SecurityKey` موجود مبنای شاخص HMAC با purpose جداست؛ rotation آن بدون برنامهٔ بازسازی شاخص انجام نشود. هیچ مقدار کلیدی در این سند یا Git ثبت نمی‌شود. شماره‌ها توسط [کتابخانهٔ معتبر libphonenumber-csharp](https://github.com/twcclegg/libphonenumber-csharp) اعتبارسنجی می‌شوند؛ تشابه شماره هیچ Account Link ایجاد نمی‌کند.
