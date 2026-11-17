# محدودهٔ قطعی Release جاری Backend

تاریخ: `2026-11-17`؛ مبنای ممیزی `14b0271a9d3aea840e23413dacae5300fc68ebed` و مبنای
پیگیری Runtime=`f704a5c3a0a0c808335ce05f4c8f6863e04b2235` روی `feature/product-v6-backend-alignment`.
این سند تصمیم صریح مالک برای ممیزی نهایی BE01–BE25 را ثبت می‌کند. سند هدف محصول،
شاهد کد، تست یا استقرار نیست. نتیجهٔ ممیزی و آمادگی نصب در
[گزارش نهایی](FINAL_BE01_BE25_AUDIT_FA.md) و [موانع](CURRENT_RELEASE_BLOCKERS_FA.md) است.

## IN SCOPE — قابلیت‌های این نسخه

| حوزه | دامنهٔ پذیرش |
|---|---|
| BE01 / BE02 | قرارداد واقعی Controller/DTO/OpenAPI/EF؛ هویت ثابت، Profile/Bio/DOB/Avatar، تغییر امن شماره، OTP، نشست و Refresh rotation |
| BE03 | پروندهٔ تأسیس آموزشگاه، مدارک خصوصی، اصلاح/بررسی/تصمیم، مدیر اولیهٔ صریح، Quote/Order/Payment و Provisioning قابل بازیابی |
| BE04 / BE05 | چندسازمانی/چندنقشی، عضویت و Guardian جاری؛ دوره/درس/کلاس، Lesson، جلسه، برنامه و حضور |
| BE06 / BE07 | چرخهٔ کامل Assignment/Exam **سازمانی**، پیوست پاسخ، نسخه، رسید، نمره، انتشار و اصلاح؛ engine مشترک محفوظ است |
| BE08 | Dashboard/گزارش واقعی سازمانی، فیلتر، صفحه‌بندی، Export مجاز و بازیابی Worker |
| BE09 / BE10 | درخواست و Lifecycle کلاس مستقل، عضویت/دعوت/درخواست پیوستن، نقش محلی، ظرفیت Learner و انتقال مسئولیت با محافظ مدیر |
| BE11 | فضای رسمی سازمان/کلاس/PersonalClass، یکتایی، Provision/Repair، دسترسی از منبع جاری و لغو دسترسی |
| BE12 | Inbox حساب، تاریخچهٔ SQL، ورود تازه/دستگاه دوم، صفحه‌بندی و Replay؛ نتیجهٔ MKN-BE-001 با ACL جاری |
| BE13 / BE14 | Group/Channel شخصی و قابلیت‌های P2 موجود؛ دفترچهٔ خصوصی چندشماره‌ای، رضایت، Sync/Delta/Tombstone و Link خصوصی |
| BE15 | فایل و منبع آموزشی با مجوز منبع، محافظ حذف، Safety/ReviewRequired، Archive/Revoke و پنهان‌بودن Library طبق سیاست جاری |
| BE16 | اعلان داخلی، Outbox پایدار، Dedup، Preference، Deep Link مجاز و بازیابی؛ adapterهای WebPush/FCM حفظ می‌شوند |
| BE17 | سیاست جاری مالکیت مستقل Individual/Organization، Catalog/Price/PlanVersion، Entitlement/Quota، Order، تمدید/تغییر پلن و Usage Reservation |
| BE18 | فقط Zibal: Request، Callback غیرقابل‌اعتماد، Verify/Inquiry سمت سرور، Recovery، هم‌زمانی، Payment، Outbox و Fulfillment |
| BE19 | Gateway شخصی/سازمانی AI، Assistant Context، Payer/FreeTrial/Quota، Source ACL، Knowledge/RAG و رفتار Retry/Unknown موجود |
| BE20 / BE21 | مجوزهای Platform، کاربران، آموزشگاه/Commerce/Finance/Support/Moderation/Audit؛ چرخهٔ فعلی تیکت بدون Reopen تازه |
| BE22 / BE23 | درخواست حذف و workflow حریم خصوصی؛ آمادگی محلی Migration/Publish/Health/Worker/Recovery و راهنمای عملیات |

`School Manager` معادل `Platform Admin` نیست. Guardian مجوز چت خصوصی کودک نمی‌سازد.
UserId/ResourceId انتخاب‌گر است؛ مجوز از حساب، نشست و رابطهٔ معتبر فعلی می‌آید.
در Messaging، سیاست مصوب `2026-11-01` و محافظ‌های مستقل Privacy/Block/Safety مرجع‌اند؛
محدودیت سنی تاریخیِ متعارض دوباره فعال نمی‌شود.

## DEFERRED — موارد خارج از پذیرش این Release

| قابلیت | تصمیم قطعی |
|---|---|
| CafeBazaar Billing | `DEFERRED_CURRENT_RELEASE` |
| Myket Billing | `DEFERRED_CURRENT_RELEASE` |
| iOS Billing | `DEFERRED_CURRENT_RELEASE`؛ تصمیم محصولی آینده باز است |
| دریافت واقعی WebPush/FCM | `DEFERRED_CURRENT_RELEASE`؛ کد adapter و آزمون‌ها حذف یا تضعیف نمی‌شوند |
| پیامک عمومی Notification | `DEFERRED_CURRENT_RELEASE`؛ OTP ملی‌پیامک حوزهٔ مستقل است و تغییر نمی‌کند |
| PersonalClass Assignment | `DEFERRED_APPROVED`؛ `Features:PersonalClass:AssignmentsEnabled=false` |
| PersonalClass Exam | `DEFERRED_APPROVED`؛ `Features:PersonalClass:ExamsEnabled=false` |
| Family/Shared Subscription | `DEFERRED_PRODUCT_DECISION`؛ سیاست جاری، اشتراک مستقل هر User است |
| Family Quota Pooling | `DEFERRED_PRODUCT_DECISION`؛ Guardian مالکیت مالی منتقل نمی‌کند |
| دانلود عمومی Library | `PRODUCT_POLICY_DISABLED`؛ catalog مجاز فقط metadata مصوب را می‌دهد |
| Library AI | `PRODUCT_POLICY_DISABLED`؛ Library از انتخاب منبع AI حذف می‌ماند |

این وضعیت‌ها نقص کد نسخهٔ جاری نیستند. BE25 برابر `DEFERRED_APPROVED` است و engine/داده‌های
موجود آن باقی می‌مانند. گسترش گزارش PersonalClass، Reopen تیکت/کلاس/پرونده و workflow تأیید
رسانه نیز در این ممیزی اختراع نمی‌شوند. `ReviewRequired` به Approved یا Rejected تبدیل
نمی‌شود؛ بستن پروندهٔ Moderation به معنی قابل‌ارسال‌شدن رسانه نیست.

```text
ZIBAL=IN_SCOPE
BE18_STORE_BILLING=DEFERRED_CURRENT_RELEASE
PRODUCTION_PUSH_RUNTIME=DEFERRED_CURRENT_RELEASE
LIVE_WEB_PUSH=NOT_REQUIRED_CURRENT_RELEASE
LIVE_FCM_PUSH=NOT_REQUIRED_CURRENT_RELEASE
LIBRARY_PUBLIC_DOWNLOAD=PRODUCT_POLICY_DISABLED
LIBRARY_AI=PRODUCT_POLICY_DISABLED
```

## CONFIGURATION PENDING — تنظیم از پیاده‌سازی جداست

پیگیری Runtime، فایل خصوصی Test را با URL مصوب AI/Knowledge تکمیل و در میزبان محلی
Environment=`Test` با SQL/Storage ایزوله اجرا کرد. هیچ دیتابیس عادی یا تنظیم سرور تغییر نکرد.
شاهد و روش تکرار در [گزارش Runtime](../operations/TEST_RUNTIME_READINESS_FA.md) است.

- در هر دو فایل خصوصی، `Payments:Provider=Unavailable` است. تنها ورودی نامعلوم برای تکمیل
  Test، `Payments:Zibal:ClientReturnUrl` واقعی است؛ Callback از Origin مستند Backend و
  allowlist از URL کلاینت استخراج خواهد شد. فعال‌سازی ناقص و پرداخت مالی انجام نشد.
- در Test، هر دو `AiService:Enabled` و `AiKnowledge:Enabled` برابرtrue و BaseUrlها
  `CONFIGURED` هستند؛ Personal Chat، Ingest/Ready/Retrieve/RAG واقعی موفق شدند. Service
  Authentication هر دو **false** و timeout35s باقی‌اند؛ JWT/کلید فعال نشد. Production خصوصی
  همچنان غیرفعال و `OPERATIONAL_ACTIVATION_PENDING` است.
- `PRODUCTION_SCHOOL_COMMERCIAL_CONFIGURATION=BLOCKED_CONFIGURATION` تا ثبت دادهٔ
  واقعی و مصوب Offering/PlanVersion/Price/Quota است. این فقط فروش آموزشگاه را می‌بندد؛
  به‌تنهایی دلیل رد کل نسخه نیست. این ممیزی قیمت یا سهمیهٔ ساختگی Seed نمی‌کند.
- اجرای واقعی حذف حساب خاموش می‌تواند بماند؛ API درخواست و workflow موجود باید سالم باشند.
  `PRODUCTION_ACCOUNT_DELETION_EXECUTION=BLOCKED_CONFIGURATION` و
  `RETENTION_DURATIONS=PRODUCT_LEGAL_CONFIGURATION_REQUIRED` مستقل از سلامت کد هستند.

هیچ مقدار خصوصی شناخته‌شده خالی یا وارد Git نشد. Production خصوصی byte-for-byte حفظ شد؛
Test فقط در دو بخش AI/Knowledge تغییر کرد. Schema تنظیمات **مؤثر** پس از بارگذاری فایل پایه
و override هر محیط یکسان است؛ برابری raw دو فایل override لازم نیست. تفاوت مصوب:

| تنظیم | Test | Production |
|---|---|---|
| OTP | `Enabled=false`، `AllowMockAuthentication=true` | `Enabled=true`، `AllowMockAuthentication=false` |
| Database مقصد | `MakanApp_Test` | `MakanApp` |
| Storage | `C:\MakanAppData\Test\Storage` | `C:\MakanAppData\Production\Storage` |

Mock در Production/Staging با Startup guard رد می‌شود. تغییر شماره همیشه provider واقعی
مستقل می‌خواهد. SMS واقعی در Regression ارسال نمی‌شود. مقادیر Secret در گزارش ثبت نمی‌شوند.

## OPERATIONAL VALIDATION PENDING — بعد از آماده‌سازی و نصب مجاز

این مرحله Deploy، Merge به main، خرید واقعی یا Migration روی دیتابیس‌های عادی اجرا نمی‌کند.
پذیرش محلی به معنی پذیرش سرور نیست. سه مورد نخست، شمارش عملیاتی Test و چهار مورد آخر،
شمارش مستقل Production هستند:

1. استقرار نسخهٔ نهایی روی Test و بررسی Health/Ready، احراز هویت و Smoke قابلیت‌های داخل محدوده.
2. اتصال واقعی همان Backend مقصد به AI/Knowledge با تنظیم مصوب، بدون تغییر قرارداد/Service Auth.
3. رفت‌وبرگشت Zibal و Callback واقعی Test؛ تنظیم و پذیرش Production جداست.
4. پایداربودن Data Protection Ring، ACL و بازیابی/Restart با purposeهای واقعی.
5. دسترسی Storage هویت IIS و جدایی کامل فایل‌های Test/Production.
6. Restore واقعی Production روی محیط ایزوله با SQL، Storage و Key Ring؛ drill محلی جای آن نیست.
7. اتصال exporter/داشبورد/alert و اثبات دریافت رخداد شکست، بدون نشت محتوای خصوصی.

چهار مورد آخر، gate عملیاتی Production هستند؛ برای نصب اولیهٔ Test به‌عنوان خطای implementation
گزارش نمی‌شوند. بسته‌های تاریخی با Source قدیمی، بستهٔ تازهٔ این ممیزی محسوب نمی‌شوند.
تولید/استقرار Release تازه باید پس از این Audit در Task صریح خود انجام شود.

شمارش جاری: `CURRENT_RELEASE_CODE_GAPS=0`، `CURRENT_RELEASE_CONFIGURATION_BLOCKERS=1`،
`CURRENT_RELEASE_OPERATIONAL_VALIDATIONS=3`؛ تصمیم `NOT_READY_FOR_TEST_DEPLOY` فقط تا تکمیل
تنظیم الزامی درگاه است. Store Billing/Push/PersonalClass Academic و gateهای Production
به مانع اضافی نصب Test تبدیل نشده‌اند.
