# OTP قابل تنظیم و Mock Authentication امن

## ۱. خلاصهٔ مرحله

پیش‌تر provider توسعه/واقعی و حالت قدیمیِ کد ثابت داشتیم. اکنون فقط `Otp:Enabled` انتخاب رفتار
احراز هویت را مشخص می‌کند: true برای OTP واقعی و false برای Mock. هیچ OTP ثابت یا secret تازه‌ای لازم نیست.
Mock اجازهٔ impersonation حساب موجود در محیط تست می‌دهد؛ بنابراین به مجوز مستقل و محیط مجاز نیاز دارد.

## ۲. معماری

Domain تغییر نکرد. در Application، `OtpOptions.Enabled` و `IdentityService` رفتار را کنترل می‌کنند؛
Controllerها بدون شرط محیط/OTP باقی ماندند. Infrastructure، `OtpAuthenticationOptions` را از Configuration
می‌خواند، با Options Pattern ثبت می‌کند و پیش از startup محیط و تنظیمات ناسازگار را رد می‌کند.
Api فقط appsettings را مالک است. Application وابستگی Configuration/ASP.NET/EF تازه ندارد.

## ۳. فایل‌های مهم ایجادشده

- `MakanApp.Infrastructure/Identity/OtpAuthenticationOptions.cs`: قرارداد root section، guard مستقل و رد فلگ‌های متناقض.
- `tests/MakanApp.UnitTests/Identity/ConfigurableOtpOptionsTests.cs`: قواعد configuration و انتخاب adapter.
- `tests/MakanApp.IntegrationTests/ConfigurableOtpAuthenticationTests.cs`: مسیر HTTP/session واقعی با SQL Server موقت.
- همین سند: راهنمای اجرایی و گزارش آموزشی؛ کانفیگ خصوصی Production وارد Git نمی‌شود.

## ۴. فایل‌های مهم تغییرکرده و تنظیم دقیق

`IdentityOptions.cs` و `IdentityService.cs` انتخاب مرکزی رفتار را اضافه می‌کنند.
`DependencyInjection.cs` Options را ثبت و در Mock هیچ adapter ارسال SMS را فعال نمی‌کند.
فایل‌های appsettings و نمونهٔ Production تنظیم زیر را دارند.

| فایل | Enabled | AllowMockAuthentication |
|---|---|---|
| appsettings.json | false | true |
| appsettings.Development.json | false | true |
| appsettings.Test.json | false | true |
| appsettings.Production.json | false | false |

در Development و فایل اختصاصی Test، محافظ دوم صریحاً فعال است. در Testing باید آن را صریحاً تنظیم کنید.
انتشار سرور آزمایشی با پروفایل `TestIis` و محدودیت‌های آن در [راهنمای سرور تست](SERVER_MOCK_OTP_FA.md) آمده است.
در نبود section جدید، fallback کد Enabled=true و AllowMockAuthentication=false است.
فایل واقعی Production خصوصی/ignored باقی می‌ماند؛ نمونهٔ غیرمحرمانهٔ آن نسخه‌بندی می‌شود.

```json
{
  "Otp": {
    "Enabled": false,
    "AllowMockAuthentication": true
  }
}
```

fragment بالا فقط برای Development/Test/Testing است. برای Production:

```json
{
  "Otp": {
    "Enabled": true,
    "AllowMockAuthentication": false
  }
}
```

مقدار اولیهٔ فایل Production طبق درخواست false است و عمداً startup را متوقف می‌کند؛
قبل از publish/deploy باید Enabled را true کنید. این توقف failure واقعی configuration است، نه موفقیت یا warning بی‌اثر.
Environment Variableهای `Otp__Enabled` و `Otp__AllowMockAuthentication` می‌توانند JSON را override کنند.
Options snapshot شروع برنامه است؛ بعد از تغییر config برنامه را restart کنید، hot-reload احراز هویت نداریم.

اصلاح `2026-10-22`: ترکیب true/true در تمام محیط‌ها متناقض و غیرمجاز است؛ false/false نیز fail-closed است.
در Mock مقدار کد بررسی نمی‌شود، ولی فرمت موجود آن حفظ می‌شود: پس از trim و تبدیل ارقام فارسی/عربی،
طول پیش‌فرض ۴ تا ۶۴ کاراکتر و بدون control character؛ قرارداد فعلی الزام «دقیقاً شش رقم» ندارد.
کدهای نمونهٔ شش‌رقمی از جمله مقدارهای صفر معتبرند؛ OTP یک‌رقمی دیگر مجاز نیست.
تعداد رقم و مقدار OTP واقعی را provider تعیین می‌کند؛ این اصلاح adapter ملی‌پیامک را تغییر نمی‌دهد.

## ۵. جریان واقعی اجرا

Real: HTTP → AuthController → IdentityService → IOtpDeliveryProvider قبلی → OtpChallenge/hash/salt
→ EfIdentityStore/SQL Server → Verify معتبر → User/Credential → UserSession/RefreshToken → HTTP.

Mock Request: همان Controller → IdentityService → normalize شماره → پاسخ موقت بدون provider/DB.
Mock Verify: همان Controller → IdentityService → normalize شماره → Serializable transaction
→ resolve/create عادی User و MobileCredential → کنترل suspension → session/refresh عادی → HTTP.

توکن کاربر opaque session token است، نه JWT نقش‌دار. Mock نقش یا Membership/Enrollment/GuardianRelation
ایجاد نمی‌کند و session را MultiFactor نمی‌کند. permission، workspace، tenant و MFA موجود همچنان بررسی می‌شوند.

## ۶. دیتابیس

هیچ schema/model/migration جدیدی لازم نیست و startup migration اجرا نمی‌کند.
Mock Request هیچ OTP یا challenge ذخیره نمی‌کند. Mock Verify فقط همان User/Credential و session/refresh معمول
را در transaction موجود می‌سازد. PK/FK/unique indexهای موجود و transaction حفظ می‌شوند.
در این کار SQL seed و migration ناتمام قبلی تغییر/commit نمی‌شوند. مقصد دستی کاربر همان `Server=.;Database=MakanApp` است.
تست خودکار هرگز به این DB متصل نمی‌شود؛ DB موقت تست پس از اجرای عادی حذف می‌شود.

## ۷. Endpointها و Contract

هیچ API/DTO/route تازه‌ای ساخته نشد:

| Method / Route | Request | Response / رفتار |
|---|---|---|
| POST /api/v1/auth/otp/challenges | RequestOtpCommand(phoneNumber) | 202 و همان RequestOtpResult |
| POST /api/v1/auth/otp/verify | VerifyOtpCommand(challengeId,phoneNumber,code,deviceName) | 200 و همان VerifyOtpResult |
| POST /api/v1/auth/refresh | RefreshSessionCommand | refresh rotation عادی |

در Mock، ChallengeId پاسخ یک Guid صرفاً برای سازگاری شکل DTO است؛ در DB وجود ندارد.
ExpiresAtUtc/ResendAvailableAtUtc فقط شکل پاسخ قبلی را حفظ می‌کنند و مدرک ورود یا قاعدهٔ Verify نیستند.
Verify به Request قبلی نیاز ندارد؛ Guid.Empty یا شناسهٔ ناموجود/منقضی مانع آن نیست.
Code در Application با هیچ مقدار مقایسه یا normalize نمی‌شود؛ حتی رشتهٔ کوتاه/غیرعددی واردشده پذیرفته می‌شود.
الزام موجود MVC برای وجود فیلدهای non-nullable و JSON معتبر حفظ شده؛ این قابلیت حذف validation حمل‌ونقل نیست.
فرمت شماره همیشه معتبر و normalizeشدنی باید باشد؛ شمارهٔ غیرواقعی با قالب معتبر مجاز است.

خطاهای مشترک: شمارهٔ نامعتبر 400، حساب معلق 403، محدودیت HTTP با 429.
در Real همهٔ خطاهای قبلی OTP از جمله invalid/expired/consumed/attempts حفظ‌اند.
در Mock rate limiterهای HTTP باقی‌اند؛ قواعد resend/attempt/expiry مبتنی بر challenge عمداً اعمال نمی‌شوند.
پس از روشن‌شدن Real، شناسهٔ موقت Mock به‌عنوان مدرک OTP پذیرفته نمی‌شود.

## ۸. مفاهیم استفاده‌شده

Options Pattern/`IOptions<T>`، validation زودهنگام fail-closed، constructor DI، transaction موجود،
normalization شماره، WebApplicationFactory و SQL Server واقعی استفاده شده‌اند.
MeliPayamak و secretهای آن، AI و معماری پروژه تغییر نکرده‌اند.

## ۹. تصمیم‌ها و تنظیمات قدیمی

تنها switch رفتار `Otp:Enabled` است؛ `AllowMockAuthentication` محافظ deployment است و mode دیگری نیست.
`Identity:Otp:Mode=DevelopmentBypass` و هر DevelopmentBypassEnabled=true در root، Identity:Otp یا Otp
در composition رد می‌شوند. Mode قدیمی Real صرفاً مقدار غیرفعالِ سازگار است و switch جدید را override نمی‌کند.
DevelopmentCode و TestUsers دیگر در فایل Development عرضه نمی‌شوند؛ هیچ کد ثابت لازم نیست.
typeهای قدیمی برای سازگاری کد/testهای موجود حفظ شده‌اند، اما activation configuration آن‌ها مسدود است.
حالت قدیمی با کد ثابت را به‌عنوان تست Mock جدید گزارش نمی‌کنیم.

انتخاب نشدن provider در Mock، از ارسال اتفاقی SMS حتی با Sms:Provider=MeliPayamakOtp جلوگیری می‌کند.
adapter دفاعی Unavailable ثبت می‌شود؛ اگر مسیر Mock اشتباهاً delivery را صدا کند، عملیات fail می‌شود.
هر درخواست Verify در Mock همچنان session معمول جدید می‌سازد؛ این قابلیت idempotency یا OTP تک‌مصرفِ ساختگی ادعا نمی‌کند.

## ۱۰. تست‌ها

Unit: محیط Production/Staging/ناشناخته، نبود مجوز مستقل، تنظیم نامعتبر، default امن، Options و adapter بدون SMS،
و رد فعال‌سازی legacy. Integration: شمارهٔ غیرواقعی و normalize ارقام فارسی، کدهای مختلف بدون challenge،
resolve حساب بدون duplicate، refresh واقعی، suspension و MFA، نبود OTP row، privacy log،
نادیده‌گرفتن challenge منقضی در Mock و رد شناسهٔ Mock در Real.
تست قدیمی composition کد ثابت به تست رد آن تنظیم منسوخ تبدیل شد؛ حفاظت SMS با تست‌های جدید جایگزین شد.
suite قبلی Authentication با Otp:Enabled=true اجرا می‌شود تا Mock اشکال آن را پنهان نکند.
Architecture suite قبلی حفظ می‌شود.

## ۱۱. Validation

Restore و build اولیه موفق و بدون warning/error بودند. Focused: ۸۶ تست Identity Unit و ۵۳ Integration موفق شدند؛
تست‌های تکمیلی پس از آن اضافه شدند. Regression کامل اول: ۷۸۱ Unit، ۱۲۹۴ Integration و ۱۴ Architecture؛
مجموع ۲۰۸۹ passed، صفر failed/skipped. انتظار حافظهٔ SQL (RESOURCE_SEMAPHORE) این اجرا را کند کرد، اما شکست نداشت.
EF گزارش داد مدل نسبت به آخرین migration تغییری ندارد.

Smoke مستقل Kestrel با SMS=Unavailable و SQL عمداً غیرقابل‌دسترسی، Request Mock را با HTTP 202 بررسی کرد.
DB تازه‌ای ساخته نشد؛ log خام/secret چاپ نشد و فرایند smoke متوقف شد. خطای اولیهٔ cleanup اسکریپت PowerShell
با روش سازگار اصلاح و smoke دوباره با exit=0 و توقف تأییدشده اجرا شد.

اجرای نهایی با build مجدد و تنظیم اجرایی `-- xUnit.ParallelizeTestCollections=false` انجام می‌شود؛
این [تنظیم رسمی xUnit](https://xunit.net/docs/config-runsettings) فقط هم‌زمانی collectionها را کم می‌کند،
نه تعداد تست‌ها یا concurrency سناریوهای داخلی هر تست را. هیچ تستی حذف/skip نشده و تنظیمات SQL Server تغییر نکرده‌اند.
Final restore/build موفق و بدون warning/error بودند. Final test: ۷۸۲ Unit، ۱۲۹۴ Integration و ۱۴ Architecture؛
مجموع ۲۰۹۰ executed/passed، صفر failed و صفر skipped. نتایج هر پروژه در
`TestResults/configurable-otp-final.trx` ثبت شده‌اند و artifactها وارد Git نمی‌شوند.
۲۲ تست Unit و ۱۵ تست Integration جدید اضافه شدند؛ چهار مورد Integration، توقف میزبان واقعی Production/Staging را می‌سنجند.

## ۱۲. Git

Worktree تمیز: `.artifacts/configurable-otp-20261021`؛ branch: `identity/configurable-otp-20261021`؛ upstream: `origin/main`.
فقط فایل‌های OTP مرحلهٔ فعلی stage می‌شوند؛ hash و نتیجهٔ commit/push/remote verification در گزارش نهایی گفتگو ثبت می‌شوند.
پیام commit این مرحله: `feat(identity): add guarded configurable mock OTP authentication`.
کانفیگ خصوصی Production فقط تغییر محلی مرتبط دارد و secretهایش وارد commit نمی‌شوند.
dirtyهای AI اصلی و چهار فایل SQL/test ناتمام worktree قبلی خارج از این scope باقی می‌مانند.

## ۱۳. محدودیت‌ها

Mock روی Production/Staging حتی با AllowMockAuthentication=true اجرا نمی‌شود.
در محیط تست امکان ورود به حساب موجود با شمارهٔ آن وجود دارد؛ محیط Development را عمومی منتشر نکنید
و آن را به دیتابیس واقعی Production وصل نکنید. این روش هویت/مالکیت شماره را اثبات نمی‌کند.
روشن‌کردن OTP، sessionهای صادرشدهٔ قبلی را خودکار revoke نمی‌کند؛ عمر و لغو آن‌ها همان قواعد معمول session است.
پیش از انتقال داده/محیط آزمایشی به محیط واقعی، sessionهای آزمایشی را با مسیرهای موجود logout/revoke لغو کنید
و از اتصال Mock به داده یا کلیدهای Production جلوگیری کنید؛ تغییر این flag جای مرزبندی محیط‌ها نیست.
در این مرحله کاربر دستی seed نمی‌شود، SQL قبلی اجرا نمی‌شود، SMS پولی ارسال نمی‌شود و سروری deploy نمی‌شود.

## ۱۴. نکات آموزشی

1. شکل DTO را می‌توان بدون ایجاد دادهٔ امنیتی ساختگی حفظ کرد؛ شکل challenge، اثبات challenge نیست.
2. غیرفعال‌کردن عامل احراز هویت، مجوز tenant یا MFA را غیرفعال نمی‌کند.
3. flag منفی به محافظ مستقل deployment نیاز دارد؛ پیش‌فرض false در Production باید fail-closed باشد.
4. تست مسیر Real را صریحاً Real اجرا کنید؛ Mock نباید تست امنیتی را به‌طور اتفاقی سبز کند.
5. تنظیم متناقض قدیمی را رد کنید؛ ترتیب config نباید روش احراز هویت را مبهم کند.
6. Mock واقعی بدون کد، با provider توسعه‌ای که کد تصادفی می‌سازد تفاوت دارد.

## ۱۵. مسیر مطالعه

AuthController → IdentityOptions → IdentityService.RequestOtpAsync/VerifyOtpAsync → OtpChallenge
→ OtpAuthenticationOptions → DependencyInjection → EfIdentityStore → تست‌های جدید → تست‌های قبلی Identity.

## ۱۶. مرحلهٔ بعد

برای اجرای محلی محیط Development را انتخاب کنید؛ false/true در Otp بخش Development، Mock را فعال می‌کند.
برای آزمایش SMS واقعی Enabled را true و AllowMockAuthentication را false کنید و provider/secretهای قبلی را حفظ کنید؛ روشن‌کردن Enabled به‌تنهایی روی کانفیگ Mock، ترکیب متناقض true/true می‌سازد.
برای انتشار Production، Enabled=true و AllowMockAuthentication=false الزامی است؛ به محیط تست عمومی یا OTP bypass Production گسترش ندهید.
