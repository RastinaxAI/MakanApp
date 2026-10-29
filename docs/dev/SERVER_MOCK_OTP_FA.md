# انتشار سرور آزمایشی با Mock OTP

این مسیر فقط برای سرور و دادهٔ آزمایشیِ تأییدشده است؛ در این حالت هر شخص دارای دسترسی شبکه می‌تواند با شمارهٔ یک حساب موجود و هر کد وارد آن شود. سرور را با VPN، محدودیت IP یا شبکهٔ خصوصی از اینترنت عمومی جدا کنید. شماره‌های نمونه لزوماً بدون مالک نیستند؛ پیامک واقعی برای آن‌ها ارسال نکنید. این سند اثبات اجرای تست روی سرور نیست.

## تنظیم دقیق

محیط برنامه `Test` است، نه `Development`، `Testing` یا `Production`:

```text
ASPNETCORE_ENVIRONMENT=Test
DOTNET_ENVIRONMENT=Test
```

مقادیر هم‌نام قدیمی در IIS و محیط سیستم نباید با فایل جدید تعارض داشته باشند. به‌ویژه `DOTNET_ENVIRONMENT=Production` را روی این سایت باقی نگذارید.

الگوی نسخه‌بندی‌شدهٔ `appsettings.Test.json.example` بدون secret و از نظر مسیر کلیدها، ساختار تو در تو و نوع JSON هم‌شکل الگوی `appsettings.Production.json.example` است. فایل واقعی `appsettings.Test.json` خصوصی و Git-ignored است؛ نسخهٔ موجود آن را بازنویسی نکنید. فقط در checkout تازه و فاقد فایل خصوصی، الگو را به نام `appsettings.Test.json` کپی و تنظیمات مجاز همان محیط را امن تأمین کنید. بخش OTP و Swagger الگو:

```json
{
  "Otp": {
    "Enabled": false,
    "AllowMockAuthentication": true
  },
  "Swagger": {
    "Enabled": true
  }
}
```

نمونهٔ بالا فقط بخشی از فایل است، نه جایگزین کامل آن. فایل Test اکنون بخش‌های ConnectionStrings، Identity، Security/DataProtection، Cors، Hosting، Database، ReverseProxy، Sms، Payments، Messaging، Notifications، Storage، Logging و AllowedHosts را هم دارد. جای connection string، Identity key، MeliPayamak token و certificate password خالی است؛ این مقادیر را فقط از تنظیمات خصوصی همان سرور تست تأمین کنید. آدرس‌های `example.test` نمونه‌اند و باید با host/origin واقعیِ مجاز محیط تست جایگزین شوند. مسیر پیش‌فرض Storage در Test برابر `C:\MakanAppData\Test\Storage` است؛ مسیر دادهٔ موجود را بی‌دلیل تغییر ندهید.

هر دو مقدار OTP برای Mock لازم‌اند. همان تنظیم در Production یا Staging همچنان Startup را متوقف می‌کند. محیط `Test` برخلاف `Testing` نرخ درخواست واقعی و محافظ‌های جاری مجوزها را نگه می‌دارد؛ providerهای توسعهٔ SMS/Notification/Content Safety مجاز نمی‌شوند. لاگ خام framework و EF نیز مانند Production با رخداد امن جایگزین می‌شود.

تصمیم جدید آمادگی انتشار `2026-10-22` جایگزین مقدار قبلی Production شد: base، Development و Test
همچنان `false/true` هستند، ولی فایل Production محلی/نمونه اکنون `true/false` دارد.
کد محافظ Startup تغییر نکرده است. برای Production پروفایل جداگانهٔ `ProductionIis`، بدون فایل Test، استفاده شود.

## انتشار محلی

از checkout تمیزِ شامل آخرین commit اجرا کنید:

```powershell
dotnet publish MakanApp.Api/MakanApp.Api.csproj --configuration Release --no-restore -p:PublishProfile=TestIis --output .artifacts/publish/makanapp-test-iis
```

پروفایل `MakanApp.Api/Properties/PublishProfiles/TestIis.pubxml` در Visual Studio هم قابل انتخاب است. در خروجی `web.config` باید محیط `Test` داشته باشد. `web.config` اصلی پروژه همچنان Production باقی می‌ماند. فایل‌های `appsettings.Production.json` و `appsettings.Development.json` عمداً در این خروجی نیستند؛ بسته نباید secretهای لپ‌تاپ را به سرور حمل کند.

## حفظ کانفیگ موجود سرور

۱. قبل از جایگزینی IIS، از DB، Storage و config خصوصی همان محیط backup سازگار بگیرید. code/schema جدید۵۲ migration دارد؛ این سند اجازه اجرای دوباره Seed یا migration سرور نیست.

۲. در محیط Test، `appsettings.Production.json` خودکار خوانده نمی‌شود. روی خودِ سرور، مقادیر خصوصیِ فعلی و مربوط به همان دیتابیس آزمایشی را با فایل خصوصی `appsettings.Test.json` ادغام کنید یا از متغیرهای محیطی IIS بدهید؛ الگوی نسخه‌بندی‌شده عمداً secret ندارد و بدون تنظیمات ضروری آمادهٔ اتصال نیست. خالی‌بودن جایگاه‌ها مجوز استفاده از دیتابیس Production نیست. فایل خصوصی و خروجی Publish حاوی آن را عمومی یا Commit نکنید.

۳. این تنظیمات قبلی باید حفظ شوند:

```text
ConnectionStrings__MakanDatabase
Identity__SecurityKey
Storage__RootPath
AllowedHosts
Cors__AllowAnyOrigin
Cors__AllowedOrigins__0
ReverseProxy__Enabled
ReverseProxy__KnownProxies__0
Hosting__RequireHttps
```

BackofficeSecurity و Security:DataProtection صرفاً متعلق به MFA حذف‌شده‌اند و قرارداد فعال تنظیم نیستند؛ Identity key/OTP/Storage پابرجایند. فایل backup/key قدیمی با این سند حذف نمی‌شود.

Identity key و Storage فعلی حفظ شوند؛ App Pool مجوز لازم Storage و read config دارد. key ring، DPAPI یا PFX صرفاً MFA شرط startup/ready فعلی نیستند؛ محیط Development راه دورزدن guardهای Production نیست.

۴. تنظیمات MeliPayamak را حذف یا عوض نکنید. با Mock فعال، provider ارسال OTP ثبت و فراخوانی نمی‌شود؛ با برگشت به OTP واقعی همان adapter موجود استفاده خواهد شد. `DevelopmentBypassEnabled=true` و `Identity:Otp:Mode=DevelopmentBypass` همچنان غیرمجازند.

۵. اگر سایت آزمایشی HTTP-only است، Hosting:RequireHttps=false فقط در config مجاز Test حفظ شود؛ HTTP رمزگذاری token ندارد. CORS از AllowAnyOrigin و مستقل از محیط می‌آید؛ KnownProxies واقعی همچنان صریح است.

۶. بعد از استقرار، App Pool را recycle کنید. تغییر flag بدون restart تضمین تغییر حالت نیست. ابتدا `GET /health/live` و `GET /health/ready`، سپس `/swagger/index.html` و مسیرهای ورود را بررسی کنید؛ این بررسی عملیاتی باید روی سرور توسط مالک انجام شود.

## تست دستی بدون پیامک

- `POST /api/v1/auth/otp/challenges` با شمارهٔ معتبر → `202`؛ شناسهٔ پاسخ صرفاً برای حفظ DTO است، نه OTP ذخیره‌شده.
- `POST /api/v1/auth/otp/verify` با `challengeId` تصادفی یا خالی، همان شماره و یک کد دلخواه با فرمت معتبر → `200` برای حساب فعال؛ نیازی به Request قبلی نیست. طول پیش‌فرض پس از trim برابر ۴ تا ۶۴ کاراکتر و بدون control character است؛ کدهای شش‌رقمی دلخواه از جمله `000000` مجازند.
- شمارهٔ نامعتبر → `400`؛ حساب معلق → `403`؛ محدودیت نرخ → `429`.
- `GET /api/v1/me` با access token صادرشده → `200`؛ بدون token → `401`.
- نقش سازمانی و دسترسی منبع همچنان مستقل‌اند؛ Mock مجوز ادمین نمی‌سازد. عامل دوم با تصمیم جداگانهٔ مالک حذف شده است.

کد، token یا پاسخ ورود را در log، screenshot یا گزارش عمومی قرار ندهید.

## بازگشت به حالت واقعی

در config مؤثر `Otp:Enabled=true` و `Otp:AllowMockAuthentication=false` بگذارید؛ در انتشار واقعی محیط Production و کانفیگ خصوصی کامل آن را بازگردانید. پس از restart، برای حساب‌های تست sessionهای صادرشده در Mock را با مسیرهای موجود revoke/logout کنید؛ تغییر flag به‌تنهایی آن sessionها را لغو نمی‌کند.

## تشخیص علت تنظیم اشتباه

ترتیب پیش‌فرضِ مؤثر از پایین به بالا: `appsettings.json`، فایل `appsettings.{Environment}.json`، User Secrets فقط در Development، متغیرهای محیطی و سپس آرگومان‌های command line است. برای `WebApplication.CreateBuilder`، مقدار `DOTNET_ENVIRONMENT` بر `ASPNETCORE_ENVIRONMENT` اولویت دارد؛ بنابراین هر دو را روی Test تنظیم کنید یا تعارض را حذف کنید. [مرجع رسمی پیکربندی ASP.NET Core 9](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0)

ویرایش سورس، DLL مستقر یا config موجود IIS را عوض نمی‌کند. `launchSettings.json` برای اجرای ابزار توسعه است، نه انتخاب محیط IIS. قبل از publish، `git rev-parse HEAD` و مسیر solution/worktree را بررسی کنید؛ checkout قدیمیِ فاقد commit `37fdd89` اساساً فلگ جدید را نمی‌خواند.

در startup این نسخه، رخداد `MockOtpAuthenticationEnabled` با سطح Warning یا `RealOtpAuthenticationEnabled` با سطح Information فقط نام محیط و دو boolean مؤثر را ثبت می‌کند. هیچ phone، code، token، کلید یا connection string در آن نیست. برای عیب‌یابی همین رخداد و status/error code را بررسی کنید، نه dump کل Configuration یا `dotnet user-secrets list`. در حالت Production نامعتبر startup قبل از این رخداد متوقف می‌شود.

در بازبینی محلی، هیچ override مربوط به OTP در User/Machine/Process یا User Secrets پیدا نشد؛ این مشاهده دربارهٔ رایانهٔ توسعه است و وضعیت IIS سرور را اثبات نمی‌کند. فایل Production خصوصی موجود را بی‌اجازه تغییر ندهید؛ پروفایل Test آن را بارگذاری نمی‌کند.
