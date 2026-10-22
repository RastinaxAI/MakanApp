# گزارش آموزشی کاربران تست و OTP توسعه

تاریخ: ۲۰۲۶-۱۰-۲۱. این سند فقط دربارهٔ همین قابلیت است؛ نتیجهٔ استقرار یا SMS واقعی نیست.
راهنمای اجرایی، کاربران و queryها در [راهنمای SSMS و Login](DEVELOPMENT_TEST_USERS_FA.md) قرار دارند.

## ۱. خلاصهٔ مرحله

پیش‌تر DevelopmentOtpProvider فقط کد تصادفی می‌ساخت و fixture مشترکِ قابل اجرای SSMS برای تمام نقش‌ها نداشتیم.
اکنون SQL برای ۴ نقش سازمانی و ۷ نقش پلتفرم، رابطه‌های آموزشی و bypass محدود و opt-in داریم.
روی LocalDB، دیتابیس مستقل `MakanApp_DevelopmentTestUsers` ساخته و seed دو بار اجرا شد؛ دقیقاً ۱۱ حساب باقی ماند.
Mode دائمی عمداً Real است؛ کد ورود واقعی یا Production secret ساخته/ثبت نشد.

## ۲. معماری Onion

- Domain تغییر نکرد: `OtpChallenge` همچنان expiry، single-use و failed attempts را enforce می‌کند.
- Application: `OtpOptions` قرارداد Mode و binding را نگه می‌دارد؛ `IdentityService` وجود حساب و
  binding را پیش از delivery و دوباره داخل تراکنش Verify بررسی می‌کند. token/session مسیر قبلی‌اند.
- Infrastructure: `DevelopmentOtpBypassOptions` config/environment را fail-closed می‌خواند و
  `DevelopmentBypassOtpProvider` پیاده‌سازی بی‌نیاز از SMS/HTTP است.
- Api: فقط configuration تغییر کرد؛ Controller و قرارداد عمومی عوض نشدند.
- SQL در docs/dev ابزار عملیاتی دستی است؛ migration یا business module جدید نیست.

## ۳. فایل‌های مهم جدید

| مسیر | مسئولیت / دلیل / نکتهٔ یادگیری |
|---|---|
| MakanApp.Infrastructure/Identity/DevelopmentOtpBypassOptions.cs | validation Mode، محیط، Code و binding؛ config نامعتبر باید startup را متوقف کند، بدون چاپ مقدار |
| MakanApp.Infrastructure/Identity/DevelopmentBypassOtpProvider.cs | adapter بدون SMS؛ provider ثابت جای authorization را نمی‌گیرد |
| docs/dev/create-development-test-users.sql | fixture تراکنشی، idempotent و دارای برخوردسنجی؛ schema فعلی و catalog واقعی مرجع‌اند |
| docs/dev/cleanup-development-test-users.sql | cleanup محدود با rollback در FK وابسته؛ history دلخواه را cascade نمی‌کند |
| tests/MakanApp.UnitTests/Identity/DevelopmentOtpBypassTests.cs | قواعد config و انتخاب provider، مستقل از DB |
| tests/MakanApp.IntegrationTests/DevelopmentTestUsersTests.cs | SQL واقعی، Controller/session و MFA واقعی؛ آزمون تمام ۱۱ نقش |
| tests/MakanApp.IntegrationTests/MakanAppWebApplicationFactory.Development.cs | DB تست با نام و شناسهٔ تصادفی محدود؛ نه DB توسعهٔ عادی |
| docs/dev/DEVELOPMENT_TEST_USERS_FA.md | مراحل SSMS، config، Login، query، خاموش‌کردن و cleanup |

## ۴. فایل‌های تغییرکرده

`IdentityOptions.cs`، Mode و TestUsers را اضافه می‌کند؛ `IdentityService.cs` binding و domain-separated hash را اعمال می‌کند.
`DependencyInjection.cs` فقط در Mode مصوب provider را انتخاب می‌کند؛ مسیر Real/MeliPayamak بازنویسی نشد.
`appsettings.json` و `appsettings.Development.json` و نمونهٔ Production، پیش‌فرض Real دارند؛
فایل Development نگاشت شماره/UserId و Code خالی دارد. Integration csproj اسکریپت‌های واقعی را برای اجرای تست کپی می‌کند.
فایل Production واقعیِ خصوصی و تمام dirtyهای AI در workspace اصلی تغییر نکردند.

## ۵. جریان واقعی اجرا

```text
HTTP POST otp/challenges
→ AuthController.RequestOtp
→ IdentityService.RequestOtpAsync
→ binding شماره و UserId موجود / حساب Active
→ DevelopmentBypassOtpProvider
→ hash همراه salt / OtpChallenge
→ EfIdentityStore / SQL Server
→ RequestOtpResult / HTTP 202

HTTP POST otp/verify
→ AuthController.VerifyOtp
→ IdentityService.VerifyOtpAsync
→ Serializable transaction / بازبینی binding
→ OtpChallenge.Verify
→ UserSession + RefreshToken عادی
→ EfIdentityStore / SQL Server
→ VerifyOtpResult / HTTP 200
```

hash در bypass پیشوند control-character داخلی دارد؛ NormalizeOtpCode اجازهٔ ارسال آن از API را نمی‌دهد.
بنابراین Code تست، حتی با DB/SecurityKey مشترک، در Mode=Real قابل مصرف نیست. این قاعده Schema تازه نمی‌خواهد.

## ۶. دیتابیس

Migration جدید نداریم؛ ۴۸ migration موجود فقط روی دیتابیس جدید LocalDB اعمال شد.
جدول‌های seed: Persons، Users، UserCredentials در identity؛ Organizations، Memberships، RoleAssignments،
OrganizationPersons در organization؛ AcademicPeriods، Courses، Classes، Enrollments، TeacherAssignments در academic؛
GuardianRelations در guardian و PlatformRoleAssignments در platform.
PKها deterministic و مختص fixture هستند؛ RoleId پلتفرم با Key از catalog فعلی resolve می‌شود.
FKهای مرکب Organization/Class/OrganizationPerson، unique/filter indexهای عضویت، enrollment و نقش حفظ‌اند.
rowversion را SQL می‌سازد؛ seed دستی مقدار آن را نمی‌نویسد.
seed یک transaction، XACT_ABORT و transaction-owned application lock دارد؛ برخورد شماره/ID باعث THROW و rollback می‌شود.
اسکریپت هیچ حسابی را بر اساس شباهت شماره/نام merge نمی‌کند و suspension را برنمی‌گرداند.

## ۷. Endpointها

هیچ endpoint تازه‌ای ساخته نشد:

| Method / Route | Request / Response | validation / authentication / authorization / خطا |
|---|---|---|
| POST /api/v1/auth/otp/challenges | RequestOtpCommand → RequestOtpResult / 202 | عمومی و rate-limited؛ bypass فقط حساب allowlist موجود؛ 400/403/429؛ Code در پاسخ نیست |
| POST /api/v1/auth/otp/verify | VerifyOtpCommand → VerifyOtpResult / 200 | challenge/phone/code و حساب معتبر؛ 400/403/409/410/429؛ session واقعی، نه Role جعلی |
| POST /api/v1/auth/refresh | RefreshSessionCommand → RefreshSessionResult | refresh rotation قبلی، token نامعتبر 401 |
| POST /api/v1/workspaces/select | SelectWorkspaceCommand → AccessContext | session، Membership و RoleAssignment فعلی؛ selector مجوز نیست |
| GET /api/admin/v1/me | PlatformMeResult | permission پلتفرم + MFA موجود؛ حساب سازمانی یا session صرفاً Primary → 403 |

## ۸. مفاهیم استفاده‌شده

Options/DI، allowlist با binding هویت، domain separation در HMAC input، Serializable transaction موجود،
SQL application lock، unique/FK/rowversion موجود، xUnit و WebApplicationFactory/LocalDB استفاده شدند.
JWT کاربر، سیستم MFA جدید، schema جدید یا معماری تازه اختراع نشد.

## ۹. تصمیم‌های طراحی

به‌جای پذیرفتن هر Code، فقط یک Code محلی و binding صریح پذیرفته شد. به‌جای تغییر MeliPayamak، adapter جدا
برای Mode تست انتخاب شد؛ هزینهٔ آن دو Options مرتبط برای نیاز متفاوت Application/Infrastructure است.
به‌جای auto-seed در startup یا migration داده، SQL دستیِ guardدار انتخاب شد؛ مزیتش کنترل مقصد و تکرارپذیری است.
Cleanup با وابستگی جدید fail می‌شود؛ این محدودیت از حذف گستردهٔ دادهٔ تست‌نشده بهتر قابل بازبینی است.

## ۱۰. تست‌ها

- Unit جدید: محیط غیرمجاز، Mode نامعتبر، config خالی، binding/شمارهٔ تکراری، code نامعتبر،
  Mode پیش‌فرض Real، adapter بدون MeliPayamak و cancellation.
- Integration جدید: Login/refresh/workspace هر ۱۱ نقش، session با assurance=Primary و MFA واقعیِ اجباری برای ۷ نقش پلتفرم؛
  نقش Manager سازمانی نیز نمی‌تواند وارد Backoffice شود.
- تست‌های SQL: تکرار seed، FK/رابطه‌های لازم، approval guard، cleanup بدون حذف catalog و برخورد شماره بدون merge.
- تست‌های lifecycle: کد اشتباه، binding تغییرکرده، حساب ناموجود، Credential حذف‌شده بعد challenge، suspension،
  single-use، expiry، hash و عدم مصرف challenge توسعه در Real.
- Architecture جدید اضافه نشد؛ suite موجود برای جهت وابستگی‌ها اجرا می‌شود.

در اجرای اولیهٔ Integration، Options میزبان تست دیر override می‌شد و challenge با OTP_INVALID رد می‌شد؛
با فراخوانی همان Load واقعی در composition تست اصلاح شد، نه با ضعیف‌کردن اعتبارسنجی برنامه.
انتظار تست expiry نیز به contract فعلی 410 اصلاح شد؛ status برنامه عوض نشد.

## ۱۱. Validation واقعی

Restore/Build اولیه موفق و بدون warning/error بود. Focused Unit شامل تست‌های قبلی MeliPayamak و provider توسعه: ۴۲ passed.
Focused Integration پس از اصلاح میزبان تست: ۴۲ passed، صفر failed/skipped؛ تست‌های بیشتر پس از آن به regression اضافه شدند.
`migrations has-pending-model-changes` موفق: هیچ تغییر مدل نداریم.
Regression کامل موفق شد: ۷۶۰ Unit، ۱۲۷۹ Integration و ۱۴ Architecture؛ مجموع ۲۰۵۳ passed، صفر failed/skipped.
Final restore و build نیز موفق و بدون warning/error بودند. Final test هم با ۷۶۰ Unit، ۱۲۷۹ Integration
و ۱۴ Architecture موفق شد: مجموع ۲۰۵۳ passed، صفر failed و صفر skipped. نتیجهٔ هر پروژه در
`TestResults/development-test-users-final.trx` ثبت شده است؛ این artifact تولیدشده وارد Git نمی‌شود.
Smoke مستقل روی Kestrel واقعی و دیتابیس باقی‌ماندهٔ LocalDB نیز موفق بود: Student از challenge/verify
به workspace صحیح رسید و logout با 204 انجام شد. در همان process، Sms:Provider=Unavailable و AI غیرفعال بود؛
Code فقط در حافظه/محیط child process بود، توکن/پاسخ خصوصی/log خام در فایل یا خروجی ابزار ذخیره نشد و فرایند smoke متوقف شد.

## ۱۲. Git

Branch: `identity/development-test-users-20261021`؛ upstream: `origin/main`؛ base: `211cd24`.
فقط فایل‌های همین قابلیت stage می‌شوند؛ hash نهایی و Push/remote verification در گزارش نهایی گفتگو ثبت می‌شوند.
این سند همراه commit با `git log -1 -- docs/dev/DEVELOPMENT_TEST_USERS_REPORT_FA.md` قابل پیگیری است.
۲۲ فایل dirty قبلی در workspace اصلی خارج از commit می‌مانند.

## ۱۳. محدودیت‌های فعلی

این قابلیت Production/staging bypass، SMS واقعی، دورزدن MFA، تغییر Child Safety،
auto-activation حساب معلق، fixture تکلیف/آزمون/پیام یا login با هر عدد ایجاد نمی‌کند.
Code محلی در این تحویل تنظیم/گزارش نشده است؛ activation باید طبق راهنما و فقط روی محیط مستقل انجام شود.

## ۱۴. نکات آموزشی

1. ساده‌کردن یک factor ورود نباید مجوز، وضعیت حساب یا tenant را ساده کند.
2. اجازهٔ شماره بدون binding به UserId برای fixture امن کافی نیست.
3. شرایط حساس را در Verify و داخل transaction دوباره بررسی کنید؛ Request-time check به‌تنهایی کافی نیست.
4. محیط مشترک/اشتباه را با domain separation در hash نیز fail-closed کنید.
5. opaque session با Service JWT یک قرارداد نیست؛ Role را به JWT کاربر فرضی منتقل نکنید.
6. seed idempotent نباید حساب معلق را خودکار فعال یا permission لغوشده را احیا کند.
7. SQL تست فقط وقتی شاهد معتبر است که واقعاً روی SQL Server با migrationهای واقعی اجرا شده باشد.

## ۱۵. مسیر مطالعه

AuthController → IdentityOptions/OtpMode → IdentityService → OtpChallenge → IdentityAbstractions
→ DevelopmentOtpBypassOptions → DevelopmentBypassOtpProvider → DependencyInjection → EfIdentityStore
→ EF configurations/migrationهای موجود → SQL seed/cleanup → Unit/Integration جدید.

## ۱۶. مرحلهٔ بعد

کد محلی را در User Secrets تنظیم و فقط نسخهٔ همین قابلیت را به دیتابیس مستقل LocalDB وصل کنید؛
سپس workspace/child/MFA عادی را تمرین کنید. Production deploy، تغییر AI Auth و غیرفعال‌سازی OTP عمومی جزو این مرحله نیستند.
