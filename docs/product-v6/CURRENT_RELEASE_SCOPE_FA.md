# محدودهٔ قطعی Release جاری Backend

به‌روزرسانی سیاست مالک: `2026-11-18`؛ مبنای این تغییر
`d397d73c5f532f7492614e95925ceaa1850a8150` روی `feature/product-v6-backend-alignment`.
سیاست جاری [AI نامحدود](AI_UNMETERED_CURRENT_RELEASE_FA.md) بر توضیحات تجاری تاریخی اولویت دارد.
این سند تصمیم صریح مالک برای ممیزی نهایی BE01–BE25 را ثبت می‌کند. سند هدف محصول،
شاهد کد، تست یا استقرار نیست. نتیجهٔ ممیزی و آمادگی نصب در
[گزارش نهایی](FINAL_BE01_BE25_AUDIT_FA.md) و [موانع](CURRENT_RELEASE_BLOCKERS_FA.md) است.

## IN SCOPE — قابلیت‌های این نسخه

| حوزه | دامنهٔ پذیرش |
|---|---|
| BE01 / BE02 | قرارداد واقعی Controller/DTO/OpenAPI/EF؛ هویت ثابت، Profile/Bio/DOB/Avatar، تغییر امن شماره، OTP، نشست و Refresh rotation |
| BE03 | پروندهٔ تأسیس آموزشگاه، مدارک خصوصی، اصلاح/بررسی/تصمیم و مدیر اولیهٔ صریح؛ کد Quote/Order/Payment/Provisioning محفوظ، چرخهٔ نیازمند پرداخت در Runtime این نسخه Deferred |
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
| BE18 | حفظ و Regression کد Zibal، Verify/Inquiry/Recovery، هم‌زمانی، Payment/Outbox/Fulfillment؛ فعال‌سازی و نمایش پرداخت برای نسخهٔ آینده است |
| BE19 | AI شخصی/سازمانی و Knowledge/RAG با `AiUsage:Mode=Unmetered`؛ بدون پرداخت/اشتراک/Entitlement تجاری/Quota/Trial/سقف محلی ai-chat؛ مجوز جاری، Source ACL و عملیات پایدار Retry/Unknown الزامی |
| BE20 / BE21 | مجوزهای Platform، کاربران، آموزشگاه/Commerce/Finance/Support/Moderation/Audit؛ چرخهٔ فعلی تیکت بدون Reopen تازه |
| BE22 / BE23 | درخواست حذف و workflow حریم خصوصی؛ آمادگی محلی Migration/Publish/Health/Worker/Recovery و راهنمای عملیات |

`School Manager` معادل `Platform Admin` نیست. Guardian مجوز چت خصوصی کودک نمی‌سازد.
UserId/ResourceId انتخاب‌گر است؛ مجوز از حساب، نشست و رابطهٔ معتبر فعلی می‌آید.
در Messaging، سیاست مصوب `2026-11-01` و محافظ‌های مستقل Privacy/Block/Safety مرجع‌اند؛
محدودیت سنی تاریخیِ متعارض دوباره فعال نمی‌شود.

## DEFERRED — موارد خارج از پذیرش این Release

| قابلیت | تصمیم قطعی |
|---|---|
| تمام پرداخت‌های Runtime، شامل Zibal | `DEFERRED_CURRENT_RELEASE`؛ Provider هر دو محیط `Unavailable` می‌ماند |
| School Quote → Payment → Paid Provisioning | `DEFERRED_CURRENT_RELEASE`؛ کد محفوظ، Provisioning رایگان یا قیمت حدسی ساخته نمی‌شود |
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
CURRENT_RELEASE_PAYMENTS=DEFERRED_CURRENT_RELEASE
ZIBAL_RUNTIME=DEFERRED_CURRENT_RELEASE
TEST_PAYMENTS_PROVIDER=Unavailable
TEST_ZIBAL_CONFIGURATION=NOT_REQUIRED_CURRENT_RELEASE
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

- در هر دو فایل خصوصی، `Payments:Provider=Unavailable` حفظ می‌شود؛ Merchant، Callback،
  `ClientReturnUrl` و allowlist برای **انتشار آیندهٔ پرداخت** لازم‌اند؛ هیچ‌کدام ورودی یا مانع
  نسخهٔ جاری نیستند. مقدار فرضی ساخته نمی‌شود و فعال‌سازی ناقص انجام نمی‌شود.
- در base و private Test/Production، `AiUsage:Mode=Unmetered` صریح است؛ پیش‌فرض کد
  `Commercial` و اجرای تجاری قابل بازگشت است. فقط تنظیم جدید تغییر کرده، Secretها محفوظ‌اند.
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

هیچ مقدار خصوصی شناخته‌شده خالی یا وارد Git نمی‌شود. این مرحله فقط `AiUsage` را در هر دو
فایل خصوصی اضافه می‌کند؛ سایر مقادیر خصوصی یکسان می‌مانند. Schema تنظیمات **مؤثر** پس از بارگذاری فایل پایه
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
پذیرش محلی به معنی پذیرش سرور نیست. دو مورد نخست، شمارش عملیاتی Test و چهار مورد آخر،
شمارش مستقل Production هستند:

1. استقرار نسخهٔ نهایی روی Test و بررسی Health/Ready، احراز هویت و Smoke قابلیت‌های داخل محدوده.
2. اتصال واقعی همان Backend مقصد به AI/Knowledge با تنظیم مصوب، بدون تغییر قرارداد/Service Auth.
3. پایداربودن Data Protection Ring، ACL و بازیابی/Restart با purposeهای واقعی.
4. دسترسی Storage هویت IIS و جدایی کامل فایل‌های Test/Production.
5. Restore واقعی Production روی محیط ایزوله با SQL، Storage و Key Ring؛ drill محلی جای آن نیست.
6. اتصال exporter/داشبورد/alert و اثبات دریافت رخداد شکست، بدون نشت محتوای خصوصی.

چهار مورد آخر، gate عملیاتی Production هستند؛ برای نصب اولیهٔ Test به‌عنوان خطای implementation
گزارش نمی‌شوند. بسته‌های تاریخی با Source قدیمی، بستهٔ تازهٔ این ممیزی محسوب نمی‌شوند.
تولید/استقرار Release تازه باید پس از این Audit در Task صریح خود انجام شود.

رفت‌وبرگشت درگاه و Callback به انتشار آیندهٔ پرداخت منتقل شدند. شمارش شاهد نهایی این مرحله
در [گزارش AI](AI_UNMETERED_CURRENT_RELEASE_FA.md) ثبت می‌شود؛ نبود تنظیم درگاه، مانع Test Deploy نیست.
Frontend/Admin باید Purchase، Renew، Upgrade، Downgrade نیازمند پرداخت، Pay و redirect
زیبال را پنهان کنند. AI فقط به ورود و Context مجاز نیاز دارد؛ انتخاب Payer، شمارندهٔ Remaining
و UI تجاری QuotaExceeded در این نسخه استفاده نمی‌شوند. محدودیت واقعی Upstream همچنان خطاست.
