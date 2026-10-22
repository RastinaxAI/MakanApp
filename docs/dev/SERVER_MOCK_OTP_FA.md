# انتشار سرور آزمایشی با Mock OTP

این مسیر فقط برای سرور و دادهٔ آزمایشیِ تأییدشده است؛ در این حالت هر شخص دارای دسترسی شبکه می‌تواند با شمارهٔ یک حساب موجود و هر کد وارد آن شود. سرور را با VPN، محدودیت IP یا شبکهٔ خصوصی از اینترنت عمومی جدا کنید. شماره‌های نمونه لزوماً بدون مالک نیستند؛ پیامک واقعی برای آن‌ها ارسال نکنید. این سند اثبات اجرای تست روی سرور نیست.

## تنظیم دقیق

محیط برنامه `Test` است، نه `Development`، `Testing` یا `Production`:

```text
ASPNETCORE_ENVIRONMENT=Test
DOTNET_ENVIRONMENT=Test
```

مقادیر هم‌نام قدیمی در IIS و محیط سیستم نباید با فایل جدید تعارض داشته باشند. به‌ویژه `DOTNET_ENVIRONMENT=Production` را روی این سایت باقی نگذارید.

`appsettings.Test.json` نسخه‌بندی‌شده بدون secret و از نظر مسیر کلیدها، ساختار تو در تو و نوع JSON هم‌شکل الگوی `appsettings.Production.json.example` است. بخش OTP و Swagger آن:

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

هر دو مقدار OTP برای Mock لازم‌اند. همان تنظیم در Production یا Staging همچنان Startup را متوقف می‌کند. محیط `Test` برخلاف `Testing` نرخ درخواست واقعی و الزام key ring پایدار MFA را نگه می‌دارد؛ providerهای توسعهٔ SMS/Notification/Content Safety مجاز نمی‌شوند. لاگ خام framework و EF نیز مانند Production با رخداد امن جایگزین می‌شود.

طبق تأیید تکمیلی مالک، base، Development و فایل Production محلی/نمونه همگی مقدار اولیهٔ false/true دارند. این یکسان‌سازی فایل‌ها، Production را به محیط تست تبدیل نمی‌کند؛ برای اجرای Production باید جفت true/false صریحاً اعمال شود. کد محافظ Startup تغییر نکرده است.

## انتشار محلی

از checkout تمیزِ شامل آخرین commit اجرا کنید:

```powershell
dotnet publish MakanApp.Api/MakanApp.Api.csproj --configuration Release --no-restore -p:PublishProfile=TestIis --output .artifacts/publish/makanapp-test-iis
```

پروفایل `MakanApp.Api/Properties/PublishProfiles/TestIis.pubxml` در Visual Studio هم قابل انتخاب است. در خروجی `web.config` باید محیط `Test` داشته باشد. `web.config` اصلی پروژه همچنان Production باقی می‌ماند. فایل‌های `appsettings.Production.json` و `appsettings.Development.json` عمداً در این خروجی نیستند؛ بسته نباید secretهای لپ‌تاپ را به سرور حمل کند.

## حفظ کانفیگ موجود سرور

۱. قبل از جایگزینی فایل‌های IIS، از کانفیگ خصوصی فعلی، دیتابیس و key ring نسخهٔ پشتیبان بگیرید. این مرحله Migration جدید ندارد؛ Seed که مالک اجرا کرده دوباره لازم نیست.

۲. در محیط Test، `appsettings.Production.json` خودکار خوانده نمی‌شود. روی خودِ سرور، مقادیر خصوصیِ فعلی و مربوط به همان دیتابیس آزمایشی را با `appsettings.Test.json` ادغام کنید یا از متغیرهای محیطی IIS بدهید؛ فایل کامل نسخه‌بندی‌شده هم عمداً secret ندارد و بدون تنظیمات ضروری آمادهٔ اتصال نیست. خالی‌بودن جایگاه‌ها مجوز استفاده از دیتابیس Production نیست. هیچ مقدار واقعی وارد Git نشود.

۳. این تنظیمات قبلی باید حفظ شوند:

```text
ConnectionStrings__MakanDatabase
Identity__SecurityKey
Storage__RootPath
BackofficeSecurity__KeyRingPath
AllowedHosts
Cors__AllowedOrigins__0
ReverseProxy__Enabled
ReverseProxy__KnownProxies__0
Hosting__RequireHttps
```

نام canonical مربوط به key ring، `Security__DataProtection__KeyRingPath` است؛ alias قدیمی `BackofficeSecurity__KeyRingPath` همچنان توسط کد موجود پشتیبانی می‌شود. مقدار خالی در JSON مانع alias نمی‌شود. اگر هر دو را تنظیم می‌کنید باید یک مسیر باشند؛ مقدارهای متعارض Startup را متوقف می‌کنند. نام ثابت ApplicationName را نیز برای key ring موجود حفظ کنید.

کلید Identity، مسیر storage و key ring قبلی را بی‌دلیل عوض نکنید. App Pool باید روی storage و key ring مجوز لازم و برای DPAPI تنظیم هویت/پروفایل سازگار داشته باشد. در Linux علاوه بر key ring پایدار، گواهی محافظ آن لازم است؛ از پیکربندی موقت Development برای دورزدن این الزام استفاده نکنید.

۴. تنظیمات MeliPayamak را حذف یا عوض نکنید. با Mock فعال، provider ارسال OTP ثبت و فراخوانی نمی‌شود؛ با برگشت به OTP واقعی همان adapter موجود استفاده خواهد شد. `DevelopmentBypassEnabled=true` و `Identity:Otp:Mode=DevelopmentBypass` همچنان غیرمجازند.

۵. اگر همین سایت آزمایشی هنوز HTTP-only است، مقدار قبلی `Hosting:RequireHttps=false` را صریحاً در config خصوصی Test نگه دارید؛ HTTP هیچ رمزگذاری شبکه‌ای برای token ایجاد نمی‌کند. `Test` را به دلیل نبود HTTPS برای سرویس واقعی استفاده نکنید. CORS و KnownProxies باید محدود و صریح باشند.

۶. بعد از استقرار، App Pool را recycle کنید. تغییر flag بدون restart تضمین تغییر حالت نیست. ابتدا `GET /health/live` و `GET /health/ready`، سپس `/swagger/index.html` و مسیرهای ورود را بررسی کنید؛ این بررسی عملیاتی باید روی سرور توسط مالک انجام شود.

## تست دستی بدون پیامک

- `POST /api/v1/auth/otp/challenges` با شمارهٔ معتبر → `202`؛ شناسهٔ پاسخ صرفاً برای حفظ DTO است، نه OTP ذخیره‌شده.
- `POST /api/v1/auth/otp/verify` با `challengeId` تصادفی یا خالی، همان شماره و یک کد دلخواه با فرمت معتبر → `200` برای حساب فعال؛ نیازی به Request قبلی نیست. طول پیش‌فرض پس از trim برابر ۴ تا ۶۴ کاراکتر و بدون control character است؛ کدهای شش‌رقمی دلخواه از جمله `000000` مجازند.
- شمارهٔ نامعتبر → `400`؛ حساب معلق → `403`؛ محدودیت نرخ → `429`.
- `GET /api/v1/me` با access token صادرشده → `200`؛ بدون token → `401`.
- نقش سازمانی، دسترسی منبع و MFA همچنان مستقل‌اند؛ Mock مجوز ادمین نمی‌سازد و MFA را حذف نمی‌کند.

کد، token یا پاسخ ورود را در log، screenshot یا گزارش عمومی قرار ندهید.

## بازگشت به حالت واقعی

در config مؤثر `Otp:Enabled=true` و `Otp:AllowMockAuthentication=false` بگذارید؛ در انتشار واقعی محیط Production و کانفیگ خصوصی کامل آن را بازگردانید. پس از restart، برای حساب‌های تست sessionهای صادرشده در Mock را با مسیرهای موجود revoke/logout کنید؛ تغییر flag به‌تنهایی آن sessionها را لغو نمی‌کند.

## تشخیص علت تنظیم اشتباه

ترتیب پیش‌فرضِ مؤثر از پایین به بالا: `appsettings.json`، فایل `appsettings.{Environment}.json`، User Secrets فقط در Development، متغیرهای محیطی و سپس آرگومان‌های command line است. برای `WebApplication.CreateBuilder`، مقدار `DOTNET_ENVIRONMENT` بر `ASPNETCORE_ENVIRONMENT` اولویت دارد؛ بنابراین هر دو را روی Test تنظیم کنید یا تعارض را حذف کنید. [مرجع رسمی پیکربندی ASP.NET Core 9](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0)

ویرایش سورس، DLL مستقر یا config موجود IIS را عوض نمی‌کند. `launchSettings.json` برای اجرای ابزار توسعه است، نه انتخاب محیط IIS. قبل از publish، `git rev-parse HEAD` و مسیر solution/worktree را بررسی کنید؛ checkout قدیمیِ فاقد commit `37fdd89` اساساً فلگ جدید را نمی‌خواند.

در startup این نسخه، رخداد `MockOtpAuthenticationEnabled` با سطح Warning یا `RealOtpAuthenticationEnabled` با سطح Information فقط نام محیط و دو boolean مؤثر را ثبت می‌کند. هیچ phone، code، token، کلید یا connection string در آن نیست. برای عیب‌یابی همین رخداد و status/error code را بررسی کنید، نه dump کل Configuration یا `dotnet user-secrets list`. در حالت Production نامعتبر startup قبل از این رخداد متوقف می‌شود.

در بازبینی محلی، هیچ override مربوط به OTP در User/Machine/Process یا User Secrets پیدا نشد؛ این مشاهده دربارهٔ رایانهٔ توسعه است و وضعیت IIS سرور را اثبات نمی‌کند. فایل Production خصوصی موجود را بی‌اجازه تغییر ندهید؛ پروفایل Test آن را بارگذاری نمی‌کند.
