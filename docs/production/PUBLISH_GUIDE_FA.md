# راهنمای انتشار MakanApp Backend — Test و Production

## مبنای جاری Realtime، اشتراک و پیوست پاسخ — ۲۰۲۶-۱۰-۳۰

مبنای کد `main@e11d4ec`، 365 عملیات HTTP، 608 schema، 113 جدول و55 migration است؛ آخرین `20261030060025_AddExamAnswerAttachments`. سه migration پس از CompleteSchoolManagement به‌ترتیب AddCommercialOwnershipPeriodsAndPersonalClasses، BindReportExportsToCommercialReservations و AddExamAnswerAttachments هستند. اولی مالکیت/دوره/رزرو و PersonalClass، دومی اتصال Export به رزرو و سومی policy سؤال و AnswerRevisionAttachments را می‌افزاید. از SQL export تازه و history واقعی مقصد استفاده کنید؛ بستهٔ قدیمی خودکار schema/binary جدید ندارد. در این بازبینی Publish/Deploy یا migration سرور اجرا نشده است.

Data Protection استاندارد host اکنون برای cursor حساب در RealtimeCursorProtector مصرف می‌شود. تداوم cursor پس از restart/redeploy به persistence و دسترسی درست key ring وابسته است؛ Identity:SecurityKey جای آن را نمی‌گیرد. در IIS پروفایل/هویت App Pool و مسیر امن کلیدها، و در Linux/Docker مکان پایدار خارج از filesystem موقت container بررسی شوند. کد AddDataProtection پیش‌فرض دارد و setting خصوصی تازه‌ای برای محل ring یا ApplicationName اضافه نشده است؛ متغیر ساختگی Security:DataProtection نسازید. از دست‌رفتن کلید یا context/epoch ناسازگار،409 REALTIME_RESYNC_REQUIRED و Snapshot تازه می‌خواهد؛ readiness این تداوم را probe نمی‌کند. این مصرف جدید، بازگشت MFA/Step-Up نیست. چند replica/backplane و اشتراک کلیدها هنوز gate مستقل‌اند. [Runbook Realtime](../operations/REALTIME_RUNBOOK_FA.md).

Outbox عمومی اکنون invalidation را پس از Commit و بیرون transaction ارسال می‌کند؛ crash می‌تواند همان EventId را تکرار کند. replay به Outboxهای موجود تکیه دارد؛ processed بودن مجوز purge نیست. bootstrap پیش از Snapshot، قطع/وصل و replay همان cursor، ContextRevision، صفحهٔ خالی با HasMore=true و RetryAfterMilliseconds و خطای503 با حفظ cursor در smoke هدف بررسی شوند. cursor/credential/payload در log نباشند.

کنترل تجاری Legacy خودکار فعال نمی‌شود؛ ثبت قرارداد معتبر Admin نقطهٔ enforcement است. دورهٔ ماهانه، Grace هفت‌روزهٔ سازمان، رزرو ReportExport و تفاوت meter unavailable/Unlimited در smoke بررسی شوند؛ بدون درخواست مستقل قرارداد واقعی نسازید. قیمت/سقف پولی یا gateway واقعی و AI runtime با این اسناد فعال نمی‌شوند.

DB و Storage پیوست آزمون، تکلیف، Lesson و Photo با هم backup/restore شوند. AnswerRevision تاریخی و فایل Retained با Detach حذف فیزیکی نمی‌شوند؛ Parent/Guardian و Teacher لغوشده grant raw attachment ندارند. BE-01 رسانهٔ Messenger همچنان Product-blocked است. [پیوست آموزشی](../product/ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md) و [اشتراک](../product/SUBSCRIPTION_FRONTEND_HANDOFF_FA.md).

## انتشار Foundation اشتراک — ۲۰۲۶/۱۰/۲۹

Release جاری، تمام قابلیت‌های قبلی به‌اضافهٔ Subscription/Entitlement/Quota و بنیاد PersonalClass را دارد. زنجیرهٔ فعلی ۵۴ Migration و آخرین مورد `20261029122246_BindReportExportsToCommercialReservations` است. مبنای قبل از این Foundation، `20261028202239_CompleteSchoolManagement` بود. نتیجهٔ نهایی تست، Commit و SHA بسته‌ها از `validation-summary.json` همان پوشهٔ Release خوانده شوند؛ اعداد بازبینی تاریخی پایین این بخش، وضعیت همان نسخهٔ قدیمی‌اند.

بسته‌های این انتشار **بدون credential واقعی** هستند. فایل خصوصی اصلی مخزن تغییر نمی‌کند؛ پیش از استقرار، ConnectionStrings:MakanDatabase، Identity:SecurityKey و Sms:MeliPayamakOtp:ServiceToken معتبر مقصد از تنظیمات خصوصی موجود یا environment variables فراهم شوند. تنظیمات خصوصی سرور را با مقادیر خالی بسته جایگزین نکنید. کلید Identity موجود حفظ شود؛ کلید تازهٔ تصادفی برای سرور نسازید. User Secrets یا credential AI داخل ZIP نیست. قابلیت واقعی ملی‌پیامک حفظ شده، اما ارسال زنده در smoke محلی بررسی نمی‌شود.

Test با Mock OTP و Production با OTP واقعی اجرا می‌شود؛ AI در هر دو خاموش، Swagger روشن، AllowedHosts=* و Cors.AllowAnyOrigin=true است. مقصدهای آینده C:\Sites\MakanApp-Test و C:\Sites\MakanApp هستند؛ این مأموریت Deploy، Push یا Migration روی این مقاصد انجام نمی‌دهد. بازکردن ZIP داخل مسیر فعلی بدون Backup/حفظ کانفیگ خصوصی مجازِ این راهنما نیست.

نام فایل‌های SQL تحویل `migrations-idempotent-safe.sql` و `migrations-subscription-incremental.sql` است. افزایشی فقط برای دیتابیسی با پیش‌نیاز تا CompleteSchoolManagement است؛ SQL کامل برای زنجیرهٔ کامل استفاده می‌شود. هر دو باید مطابق history واقعی و پس از Backup اجرا شوند؛ اجرای این مرحله فقط روی LocalDB موقت است.

Foundation تجاری به معنی فعال‌شدن فروش عمومی نیست. قیمت‌ها و سقف‌های پولی حدس زده نمی‌شوند و درگاه واقعی unavailable است. سازمان Legacy تا قرارداد معتبر Admin کنترل تازه نمی‌گیرد؛ وضعیت مهاجرت آن از قرارداد فعال واقعی جداست. چرخهٔ مستقل Assignment/Exam برای PersonalClass خارج از این Release است. قراردادهای [Frontend](../product/SUBSCRIPTION_FRONTEND_HANDOFF_FA.md) و [Admin](../product/SUBSCRIPTION_ADMIN_HANDOFF_FA.md) و [شرح پیاده‌سازی](../product/SUBSCRIPTION_ENTITLEMENT_QUOTA_IMPLEMENTATION_FA.md) مرجع قابلیت تازه‌اند.

بسته‌های ZIP قبلی برای این انتشار SUPERSEDED / DO_NOT_DEPLOY هستند؛ فایل‌ها و Backupهای قبلی حذف نمی‌شوند. smoke محلیِ Publish، پذیرش IIS/SQL/TLS سرور نیست. جزئیات تاریخی انتشار مدرسه در ادامه حفظ شده‌اند.

بازبینی هماهنگی `2026-10-30` / `main@e11d4ec`: آخرین schema مخزن `20261030060025_AddExamAnswerAttachments`،۵۵ migration است؛ Profile/School، Commerce، Realtime و پیوست پاسخ آزمون به binary/schema سازگار نیاز دارند. بسته‌های قبلی خودبه‌خود این تغییرات را ندارند. MFA و ring اختصاصی آن حذف، CORS تابع AllowAnyOrigin و OTP Test=false/true، Production=true/false است. [استقرار نسخهٔ جاری](../deployment-windows-linux-docker.md) و [مدرسه](../product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md). پذیرش سرور یا اجرای migration با این بازبینی انجام نشده است.

این راهنما مربوط به Release تأییدشدهٔ ۲۰۲۶-۱۰-۲۹ شامل School Backend Completion در مخزن اصلی است. Commit دقیق، نام ZIPها، SHA-256 و نتیجهٔ واقعی فرمان‌ها در `validation-summary.json` و `SHA256SUMS.txt` تحویل ثبت می‌شوند. ساخت بسته و smoke محلی، استقرار یا پذیرش سرور نیست.

## ۱. تغییرات این Release

- حذف Platform MFA، TOTP، Recovery Codes و Step-Up از Commit پایهٔ `b87fbd89cef33f26315fddd4a1e57116b44cc781` حفظ شده است؛ این سازوکارها برنگشته‌اند.
- تاریخ تولد خوداظهاری `DateOfBirth` به پروفایل شخصی اضافه شده است.
- عکس پروفایل شخصی User با FileAsset موجود، مالکیت فایل و حفظ Privacy در این Release وجود دارد؛ [شرح Avatar](../product/PROFILE_PHOTO_IMPLEMENTATION_FA.md).
- School شامل Directory اعضا، Roster مستقل فراگیر/مدرس، چندمدرسی، دعوت رضایت‌محور والد، Lesson/Handout و کاتالوگ‌های Course/AcademicPeriod است؛ [قرارداد کامل Frontend](../product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md).
- ساخت Class فقط Manager است؛ Teacher فقط با Assignment فعال همان Class می‌تواند Session و محتوای مجاز بسازد. دسترسی Parent به جزوه همچنان `OPEN_PRODUCT_DECISION` و فعلاً deny است.
- `MessagingStatusPolicy.Evaluate` برای پروفایل ناقص، خواندن گفتگوهای مجاز را در راهنمای قابلیت‌ها اعلام می‌کند؛ مجوز هر منبع همچنان جدا بررسی می‌شود.
- CORS از `Cors:AllowAnyOrigin` تبعیت می‌کند. هر دو بسته فعلاً `AllowAnyOrigin=true`، `AllowedOrigins=[]` و `AllowedHosts=*` دارند.
- سند تأییدشدهٔ [مدل محصول و کسب‌وکار V2](../product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md) مرجع هدف محصول است؛ وجود این سند اثبات پیاده‌سازی همهٔ قابلیت‌های آن نیست.
- AI در هر دو بسته صریحاً خاموش است؛ Swagger با مجوز مالک در هر دو روشن است.

## ۲. انتخاب بسته و تفاوت محیط‌ها

| مورد | Test | Production |
|---|---|---|
| Environment | `Test` | `Production` |
| دیتابیس مقصدِ تنظیمات خصوصی | `MakanApp_Test` | `MakanApp` |
| `Otp:Enabled` | false | true |
| `Otp:AllowMockAuthentication` | true | false |
| `AiService:Enabled` | false | false |
| `Swagger:Enabled` | true | true |
| `Cors:AllowAnyOrigin` | true | true |
| `Cors:AllowedOrigins` | [] | [] |
| `AllowedHosts` | * | * |

Test و Production دو پوشه و دو ZIP مستقل‌اند؛ روی هم Extract نشوند. هر ZIP فقط فایل پایه و appsettings خصوصی همان محیط را دارد؛ Development و محیط مقابل در آن نیستند.

Mock مالکیت شماره را اثبات نمی‌کند؛ فقط داده و حساب آزمایشی در محیط مجاز استفاده شود. مسیر OTP واقعی Production و provider فعلی ملی‌پیامک حفظ شده‌اند. خاموش‌کردن OTP، Suspension، Authorization یا قواعد Messaging را دور نمی‌زند؛ محافظ Startup در Production نیز حفظ شده است.

## ۳. محرمانگی تحویل

بسته‌ها **Private Artifact** هستند و تنظیمات خصوصی مجاز مقصد دارند. آن‌ها را در Git، لینک عمومی یا چت منتشر نکنید.

- Connection String، `Identity:SecurityKey` و `Sms:MeliPayamakOtp:ServiceToken` چاپ یا مستند نمی‌شوند.
- User Secrets محلی تغییر نمی‌کنند و به Publish یا ZIP کپی نمی‌شوند.
- در JSONهای خروجی، `AiService:Authentication:CurrentKey` و `CurrentKeyId` وجود ندارند؛ Service JWT، HMAC secret و Authorization Header بسته‌بندی نمی‌شوند.
- ثابت‌ماندن کلید Identity و تنظیمات معتبر ملی‌پیامک مقصد را پیش از جابه‌جایی بررسی کنید؛ تنظیمات واقعی سرور با مقادیر ماشین توسعه بی‌خبر جایگزین نشوند.
- schema و نوع کلیدهای Test و Production باید یکسان باشد؛ مقدارهایی مانند OTP و نام دیتابیس مخصوص محیط هستند.

## ۴. پیش‌نیاز Windows/IIS

- .NET 9 Hosting Bundle و ASP.NET Core Module V2؛ خروجی framework-dependent است.
- App Pool اختصاصی، No Managed Code، معماری مناسب سرور و یک worker process.
- WebSocket Protocol برای SignalR و Binding درست سایت.
- دسترسی Read/Execute به Publish و مجوز لازم برای SQL و Storage پایدار خارج از Publish.
- runtime principal کم‌اختیار و مجوز جدا برای اجرای Migration.

هیچ SDK، App Pool، IIS یا مسیر واقعی سرور توسط این Release تنظیم نمی‌شود.

## ۵. web.config هر بسته

در هر دو بسته:

```text
processPath=dotnet
arguments=.\MakanApp.Api.dll
hostingModel=inprocess
stdoutLogEnabled=false
shutdownTimeLimit=120
```

Test:

```xml
<environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Test" />
<environmentVariable name="DOTNET_ENVIRONMENT" value="Test" />
```

Production:

```xml
<environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
<environmentVariable name="DOTNET_ENVIRONMENT" value="Production" />
```

فایل کامل تولیدشدهٔ همان بسته استفاده شود؛ fragment بالا جایگزین کل web.config نیست. کلید تکراری یا Environment متعارض در IIS/سیستم نماند. متغیرهای محیطی روی JSON اولویت دارند.

## ۶. تنظیمات و CORS

`Cors:AllowAnyOrigin=true` مسیر `AllowAnyOrigin / AllowAnyHeader / AllowAnyMethod` را فعال می‌کند؛ Allow-Credentials همراه آن فعال نیست. Authentication و Authorization APIها محفوظ‌اند.

Restricted mode همچنان با `AllowAnyOrigin=false` و originهای صریح معتبر قابل استفاده است؛ فهرست خالی یا wildcard در این حالت معتبر نیست. جزئیات و تست‌ها در [راهنمای CORS](CORS_CONFIGURATION_FA.md) هستند.

`AllowedHosts=*` طبق تصمیم مالک این Release است. این تنظیم مجوز منابع ایجاد نمی‌کند.

استثناهای قبلاً تأییدشدهٔ HTTP و SQL certificate مقصد حفظ می‌شوند. روی HTTP، OTP و Token در شبکه رمزگذاری نمی‌شوند؛ برای استفادهٔ عمومی، TLS معتبر مقصد آماده شود. تنظیمات امن معمول `Hosting:RequireHttps=true`، `Hosting:AllowInsecureHttp=false`، `TrustServerCertificate=False` و `Database:AllowUntrustedServerCertificate=false` هستند. استثنای certificate مجوز `Encrypt=False` در Production نیست.

Swagger/OpenAPI در هر دو JSON خروجی فعال‌اند. تغییر Swagger فقط در خروجی Publish است؛ فایل خصوصی اصلی بازنویسی نمی‌شود.

## ۷. DOB و قرارداد پروفایل

Migration: `20261028102653_AddSelfDeclaredProfileDateOfBirth`.

- `identity.Persons.DateOfBirth`: نوع SQL برابر `date`، nullable، بدون default یا backfill.
- قرارداد JSON: `YYYY-MM-DD` بدون timezone.
- در پاسخ `GET /api/v1/me` و ورودی `PATCH /api/v1/me/profile` حضور دارد.
- تاریخ آینده رد می‌شود؛ null برای کاربران قبلی معتبر است.
- در PATCH، نبود مقدار یا null، تاریخ قبلی را پاک نمی‌کند؛ حذف صریح DOB در این تغییر اضافه نشده است.
- این فیلد مستقل از `VerifiedBirthDate` است، تأیید سن ایجاد نمی‌کند و وارد هویت عمومی Messaging نمی‌شود.

## ۸. Personal Messaging

**School Role برای Personal Messaging لازم نیست. Organization Membership برای Personal Messaging لازم نیست.**

با این حال، حداقل هویت Profile، وضعیت Communication Age/Safety، Privacy، Block، Suspension و مجوز Official همچنان اعمال می‌شوند. نام، نام خانوادگی و username معتبر، مسیر تکمیل پروفایل را تشکیل می‌دهند؛ DOB خوداظهاری جای تأیید سن را نمی‌گیرد.

پذیرش محلی قبلی با Kestrel واقعی، SQL موقت و ۶۲ درخواست HTTP موفق بود: دو حساب فعال و Verified Adult با صفر Membership، School Role و Platform Role، Personal Context، Discovery، Direct، ارسال/دریافت، پاسخ و بقای تاریخچه پس از ورود مجدد را گذراندند. نتیجه: `ORDINARY_USER_PERSONAL_MESSAGING_PASS=true`.

کنترل‌های منفی Official، Privacy و Block نیز اجرا شدند. پروفایل ناقص list را با 200 می‌خواند، فقط `ReadExistingConversations` می‌گیرد و ساخت Direct با `404 / DIRECT_RECIPIENT_NOT_AVAILABLE` رد می‌شود. این پذیرش، آزادبودن تماس با Minor/UnknownAge یا پذیرش زندهٔ SignalR/Frontend را ثابت نمی‌کند.

شرح پیاده‌سازی و مسیر مطالعه: [گزارش Profile و Personal Messaging](../product/PROFILE_AND_PERSONAL_MESSAGING_IMPLEMENTATION_FA.md).

## ۹. Platform MFA و سازگاری نسخه

Migration `20261024184613_RemovePlatformMfa` دو جدول عامل/Recovery و ستون‌های Session مربوط به Assurance/Step-Up را حذف می‌کند. حذف دادهٔ عامل در آن برگشت‌پذیر نیست؛ Down تنها ساختار را بازمی‌سازد.

API فعال MFA، TOTP و Step-Up در نسخهٔ جدید وجود ندارد. `GET /api/admin/v1/mfa` باید 404 شود. key ring قدیمی برای راه‌اندازی معماری فعلی لازم نیست، اما فایل‌ها و backup قدیمی سرور را خودکار حذف نکنید.

پس از اجرای این Migration، Binary قدیمی ممکن است ستون‌های حذف‌شده را بخواهد؛ بازگرداندن صرف Binary، Rollback امن نیست. [سند تغییر Authentication](../platform/PLATFORM_AUTHENTICATION_CHANGE_FA.md) نیز مطالعه شود.

## ۱۰. Migration SQL امن

موجودی جاری **۵۵** migration دارد؛ آخرین Migration (بسته‌های قدیمی باید دوباره از نسخهٔ جاری ساخته شوند):

`20261030060025_AddExamAnswerAttachments`.

Migration قبلی عکس `20261028164838_AddUserProfilePhoto` و Migration DOB نیز داخل همین chain هستند. Migration School جدول‌های `academic.ClassLessons` و `academic.ClassLessonAttachments`، مراجع Guardian روی Invitation و قیود/ایندکس‌های مربوط را اضافه می‌کند. User، Person، OrganizationPerson، Membership، Enrollment و GuardianRelation همچنان مفاهیم مستقل‌اند. روی دیتابیس‌های اصلی `MakanApp` و `MakanApp_Test` یا سرور هیچ Migration خودکاری اجرا نمی‌شود.

خروجی تحویل: `migrations-idempotent-safe.sql`. این فایل از ابزار بررسی‌شدهٔ پروژه تولید می‌شود، تاریخچه را بررسی و عملیات را با کامپایل مؤخر اجرا می‌کند تا خطای قدیمی ستون‌های `Visibility/GroupHistoryPolicy/MemberCapabilities` تکرار نشود.

```powershell
& ./tools/production/export-migration-sql.ps1 -Configuration Release -NoBuild -OutputPath .artifacts/deployment/new-release-safe.sql
```

خروجی قدیمی خام EF، جایگزین فایل امن نیست. کل فایل امن در نشست تازه و بدون تراکنش باز، روی دیتابیس صحیح اجرا شود. برنامه هنگام Startup مایگریشن خودکار ندارد.

فایل تحویل باید روی SQL موقت تازه، دو بار اجرا و history کامل و idempotency تأیید شود؛ نتیجهٔ واقعی در `validation-summary.json` ثبت می‌شود. دیتابیس‌های محلی معمول و سرور هدف این اعتبارسنجی نیستند.

## ۱۱. ترتیب انتشار دستی و Rollback

۱. محیط، نام سایت/App Pool، تنظیمات خصوصی و دیتابیس صحیح را مشخص کنید؛ این راهنما به سرور متصل نمی‌شود.
۲. از SQL، فایل‌های کاربر، تنظیمات خصوصی و Binary قبلی Backup معتبر تهیه و روش Restore را بررسی کنید.
۳. ZIP همان محیط را در پوشهٔ نسخهٔ تازه Extract و SHA-256 را بررسی کنید.
۴. در پنجرهٔ maintenance، سایت و App Pool مربوط را متوقف و خروج worker قدیمی را تأیید کنید.
۵. با مجوز Migration و در دیتابیس درست، فایل امن را کامل اجرا و history نهایی را بررسی کنید.
۶. Binary متناظر همین Release، web.config و تنظیمات مقصد را مستقر کنید؛ Storage پایدار حفظ شود.
۷. سایت/App Pool را شروع کنید و smoke واقعی مقصد را انجام دهید.
۸. فقط پس از سلامت مقصد نسخه را بپذیرید؛ backup و نسخهٔ قبلی فوراً حذف نشوند.

ترتیب مهم: **Backup → Stop Site/AppPool → Migration → Deploy matching binary → Smoke**.

برای Rollback پس از حذف ستون‌ها/جداول MFA، سازگاری schema و برنامه باید بررسی شود؛ ممکن است Restore کنترل‌شدهٔ backup لازم باشد. Migration Down دادهٔ MFA را برنمی‌گرداند. هیچ Rollback یا Restore در این مأموریت اجرا نمی‌شود.

## ۱۲. AI: نتیجهٔ محلی و وضعیت انتشار

طبق evidence محلی اعلام‌شدهٔ مالک، نتیجهٔ تازهٔ AI عبارت است از:

```text
Service Auth: PASS
Focused Three: PASS
Full live probe: PASS
realAiCalls=15
modelAlias=education-fast
```

این شواهد مربوط به پذیرش **Local AI** هستند؛ در این مأموریت AI دوباره فراخوانی نمی‌شود. گزارش‌های تاریخی 401 مربوط به اجراهای قدیمی‌اند و وضعیت آخرین پذیرش محلی محسوب نمی‌شوند.

**AI در این Release فعال نیست.** مقصد برنامه‌ریزی‌شدهٔ Production برابر `http://10.77.0.2:8001` است؛ provisioning و credential آن در بسته قرار نمی‌گیرند.

```text
Local AI acceptance only
AI enabled in this Release: false
Production WireGuard acceptance: NOT TESTED
Production IIS AI activation: NOT TESTED
```

فعال‌سازی آینده نیازمند تنظیم امن همان محیط و پذیرش واقعی از Windows Backend از مسیر WireGuard است؛ نه کپی User Secrets رایانهٔ توسعه.

## ۱۳. Smoke محلی بسته‌ها و بررسی مقصد

هر Publish جداگانه از پوشهٔ نهایی، روی Loopback با SQL و Storage موقت و overrideهای امن اجرا شود:

| درخواست | انتظار |
|---|---:|
| `GET /health/live` | 200 |
| `GET /health/ready` | 200 |
| `GET /api/v1/me` بدون نشست | 401 |
| `GET /swagger/index.html` | 200 |
| `GET /openapi/v1.json` | 200 |
| `GET /api/admin/v1/mfa` | 404 |

وجود `dateOfBirth` در schemaهای request/response، Direct route و Personal scope و محافظت Official در قرارداد نهایی بررسی شود. اسمبلی‌ها باید با Build آزموده‌شدهٔ Release برابر باشند تا اصلاح MessagingStatusPolicy واقعاً در بسته باشد.

routeهای Directory، Learners، Teachers، Guardian invitation/confirmation، Lesson list/create/update/publish، Course/Period catalog و پارامترهای اجباری UTC Schedule نیز از OpenAPI همان بسته بررسی شوند. در Test Publish، مسیر Manager → Enrollment و دو Teacher → Lesson/Session → Student download و Parent consent/report روی دیتابیس موقت آزموده شود. Teacher CreateClass، Teacher نامرتبط، Parent نامرتبط، cross-org، خواندن private chat توسط Manager و لینک فایل پس از revoke باید رد شوند. Production smoke شامل Startup/health/OpenAPI/Auth/CORS است؛ موفقیت ارسال واقعی ملی‌پیامک از آن استنباط نمی‌شود و در smoke محلی SMS واقعی ارسال نمی‌شود.

Smoke سلامت، ارسال واقعی SMS/Push، AI، پذیرش اینترنتی یا مرورگر را اثبات نمی‌کند. در Production، تست SMS واقعی فقط با مجوز و شمارهٔ مجاز انجام شود.

## ۱۴. بازتولید خروجی

پس از restore و دو نوبت Build/Test کامل موفق:

```powershell
dotnet publish MakanApp.Api/MakanApp.Api.csproj --configuration Release --no-restore -p:PublishProfile=TestIis --output .artifacts/releases/<release-id>/test
dotnet publish MakanApp.Api/MakanApp.Api.csproj --configuration Release --no-restore -p:PublishProfile=ProductionIis --output .artifacts/releases/<release-id>/production
```

Publish پس از Commit دوباره Build می‌شود تا SourceRevisionId داخل metadata اسمبلی به Commit جدید اشاره کند؛ سورس اجرایی باید با نسخهٔ آزموده‌شده برابر باشد. hash اسمبلی‌های خروجی با Build انتشار متناظر و metadata هر چهار لایه بررسی شود؛ تغییر شناسهٔ Commit تغییر رفتار برنامه نیست.

در خروجی همان محیط، Swagger روشن، AI خاموش و OTP طبق جدول تنظیم شوند؛ کلیدهای Authentication سرویس AI از JSON خروجی حذف شوند. Secretهای مجاز Identity/SMS/SQL همان محیط حفظ شوند. پوشهٔ جدید انتخاب شود تا فایل قدیمی یا محیط مقابل باقی نماند.

ZIP باید محتوای همان پوشه را در ریشه داشته باشد؛ `SHA256SUMS.txt` حداقل هر دو ZIP و SQL امن را پوشش دهد. Commit جدید بخشی از نام ZIP است. پیش و پس از انتقال با `Get-FileHash -Algorithm SHA256` تطبیق داده شود.

نام‌های این تحویل `MakanApp-Test-20261029-<sha7>.zip` و `MakanApp-Production-20261029-<sha7>.zip` هستند. ZIPهای قبلی برای این انتشار `SUPERSEDED / DO_NOT_DEPLOY` محسوب می‌شوند؛ حذف نمی‌شوند و جایگزین نسخهٔ جدید نیستند. Commit باید قبل از Build/Test نهایی و Publish ساخته شود؛ هر چهار اسمبلی باید metadata و hash متناظر همان Commit را داشته باشند. در این مأموریت Push، Deploy و Server Migration انجام نمی‌شود.

## ۱۵. عیب‌یابی

| نشانه | بررسی |
|---|---|
| Startup رد می‌شود | Environment، محافظ OTP، تنظیمات SQL/Storage/Proxy و استثناهای صریح مقصد |
| IIS 500.30/500.31 | Hosting Bundle، runtime و web.config؛ exception خام منتشر نشود |
| Swagger برابر 404 | `Swagger:Enabled` مؤثر، Environment و overrideهای IIS |
| ready برابر 503 | SQL/Storage؛ live برابر 200 به معنی آماده‌بودن وابستگی‌ها نیست |
| Direct برای پروفایل ناقص رد می‌شود | `GET /api/v1/me/messaging-status` و حداقل هویت Profile |
| Personal موفق ولی Official رد می‌شود | مرز مجوز مورد انتظار؛ نقش و منبع سازمانی جدا هستند |

## ۱۶. نکات آموزشی و مسیر مطالعه

این مرحله قابلیت جدیدی طراحی نمی‌کند؛ تغییرات تأییدشده را به Commit و Artifact قابل ردیابی تبدیل می‌کند. Onion و Modular Monolith و .NET 9/EF Core 9/SQL Server حفظ شده‌اند.

- Api میزبان، CORS و قرارداد HTTP را نگه می‌دارد؛ Application جریان Profile و سیاست قابلیت‌ها را؛ Domain تاریخ تقویمی را؛ Infrastructure نگاشت `date` و Migration را.
- `DateOnly?` برای تاریخ تولد مناسب است؛ زمان UTC یا timezone به تاریخ تقویمی تحمیل نمی‌شود.
- `MessagingStatusPolicy` راهنمای self است و جای مجوز جاری مقصد را نمی‌گیرد.
- schema parity به معنای برابری Secret یا مقادیر محیط‌ها نیست.
- idempotent SQL باید واقعاً دو بار روی SQL Server اجرا شود؛ موفقیت تولید فایل به‌تنهایی کافی نیست.
- Commit، Build و ZIP با hash به هم متصل می‌شوند؛ Push و Deploy اقدامات جدا هستند.

مسیر مطالعه: `ProfileController` → `IdentityService.CompleteProfileAsync` → `Person` → `PersonConfiguration` → Migration DOB → تست‌های Profile/DOB؛ سپس `MessagingStatusPolicy` → تست‌های Personal Messaging؛ برای میزبانی `Program.cs` → `ProductionConfigurationValidator` → تست‌های CORS.

نتیجهٔ restore/build/tests، تعدادهای دقیق، فهرست Commit و محدودیت‌های این اجرا در گزارش تحویل ثبت می‌شوند؛ هیچ موفقیت سرور از نتایج محلی استنباط نمی‌شود. مرحلهٔ بعد، انتشار دستی توسط مالک و پذیرش همان مقصد است؛ فعال‌سازی AI یا تغییر Frontend در این Release ترکیب نمی‌شود.
