# هم‌ساختارسازی تنظیمات Test و Production

> به‌روزرسانی ادغام مخزن اصلی: فایل عمومی جاری `appsettings.Test.json.example` است؛
> فایل واقعی Test خصوصی و ignored باقی می‌ماند. تست schema فقط الگوهای عمومی را مقایسه
> می‌کند و تست Configuration از ContentRoot موقتِ بدون Secret استفاده می‌کند.
> جزئیات تاریخی زیر را با این نام‌گذاری جدید بخوانید.

## ۱. خلاصه و مسئلهٔ حل‌شده

فایل Test قبلی فقط OTP و Swagger داشت. اکنون تمام مسیرها، ساختارهای تو در تو، نوع‌های JSON و ساختار آرایه‌های الگوی کامل Production در Test موجود است. مقادیر محیطی الزاماً برابر نیستند؛ هدف یکسانی schema است، نه انتقال credential یا فعال‌کردن Mock در Production.

مرجع قابل بازتولید، `MakanApp.Api/appsettings.Production.json.example` است و خود این الگو تغییر نکرد. فایل خصوصی Production در checkout اصلی نیز بدون تغییر حفظ شد. در worktree تمیز `identity/configurable-otp-20261021`، فایل نادیده‌گرفته‌شدهٔ Production قبلاً فقط OTP داشت؛ همان نمونهٔ محلی بدون secret با الگوی کامل تکمیل شد. این فایل جزو Commit نیست و جایگزین تنظیمات واقعی سرور نمی‌شود.

مقایسهٔ محلی هر سه فایل Test، Production نمونهٔ محلی و Production.example تعداد ۵۳ مسیر، شامل root/object/array و اعضا، و هیچ اختلاف نوع یا مسیر نشان داد. مقدارهای قبلی OTP و Swagger در Test حفظ شدند. هیچ مقدار خصوصی از Production به Test کپی نشد.

## ۲. معماری و محدوده

تغییر اجرایی فقط در پیکربندی Host یعنی لایهٔ Api است. Domain، Application، Infrastructure، Controllerها، منطق احراز هویت و providerها تغییر نکردند. .NET 9، EF Core SQL Server، Onion و Modular Monolith حفظ شدند.

جریان موجود: `Program` → `WebApplication.CreateBuilder` → فایل پایه/فایل محیط/overrideها → `AddInfrastructure` و factoryهای Options → `OtpAuthenticationOptions.Load` → DI. این مرحله endpoint یا use case جدید نمی‌سازد. در مسیر درخواست، `AuthController` همچنان به `IdentityService` و portهای موجود Identity متکی است؛ این وابستگی‌ها بازنویسی نشده‌اند.

## ۳. فایل‌های مهم ایجادشده

- `tests/MakanApp.UnitTests/Identity/TestConfigurationSchemaTests.cs`: مقایسهٔ recursive بدون وابستگی به سرور یا دیتابیس؛ مسیر و نوع را مقایسه می‌کند، نه مقدار secret را. مرجع نسخه‌بندی‌شده همیشه بررسی می‌شود؛ اگر فایل Production خصوصی در همان checkout وجود داشته باشد، schema آن نیز بررسی می‌شود. CI بدون فایل خصوصی هم آزمون معنی‌دار دارد.
- `tests/MakanApp.IntegrationTests/TestEnvironmentConfigurationTests.cs`: آزمون providerهای واقعی Configuration و factoryهای DI، ترتیب بارگذاری، انتخاب محیط، overrideها و محافظ موجود. برای binding از مقدارهای مصنوعیِ درون حافظه استفاده می‌شود؛ این تست‌ها به مقصد SQL نمونه یا SMS درخواست نمی‌زنند.
- همین گزارش: توضیح مرز schema، مقادیر محیطی، شواهد Validation و راهنمای مطالعه.

## ۴. فایل‌های مهم تغییرکرده و کلیدهای اضافه‌شده

`MakanApp.Api/appsettings.Test.json` چهارده بخش زیر را دریافت کرد. این‌ها ۴۷ مسیر جدید، شامل objectها و اعضای آرایه، نسبت به فایل Test قبلی هستند. کلید ناموجود در مدل واقعی اختراع نشد.

| بخش | کلیدهای اضافه‌شده در Test |
| --- | --- |
| ConnectionStrings | MakanDatabase |
| Identity | SecurityKey، Otp.Mode |
| Security.DataProtection | ApplicationName، KeyRingPath، CertificatePath، CertificatePassword |
| Cors | AllowedOrigins و عضو رشته‌ای نمونه |
| Hosting | RequireHttps، AllowInsecureHttp، HstsDays |
| Database | AllowUntrustedServerCertificate |
| ReverseProxy | Enabled، KnownProxies و دو IP نمونهٔ loopback |
| Sms | Provider، MeliPayamakOtp.BaseUrl/ServiceToken/TimeoutSeconds |
| Payments | Provider |
| Messaging | ContentSafetyProvider |
| Notifications | PushProvider، SmsProvider |
| Storage | RootPath |
| Logging.LogLevel | Default، Microsoft.AspNetCore، Microsoft.EntityFrameworkCore.Database.Command |
| AllowedHosts | مقدار رشته‌ای host نمونه |

`docs/dev/SERVER_MOCK_OTP_FA.md` نیز برای فایل کامل Test، جایگاه secretها، host/originهای نمونه، مسیر Storage و alias سازگار key ring اصلاح شد. راهنمای قبلیِ JSON کوتاه اکنون صریحاً «بخشی از فایل» است، نه تنظیم کامل اتصال.

هیچ کلید قبلی Test حذف نشد. Swagger یک کلید اضافیِ نامعتبر نبود؛ در الگوی کامل Production هم وجود دارد، فقط مقدار محیط Test برابر true است. مقایسهٔ schema ترتیب propertyها را نادیده می‌گیرد ولی نام، nesting، نوع، تعداد و ساختار اعضای آرایه را بررسی می‌کند. true و false هر دو یک نوع boolean هستند.

## ۵. جریان اجرای تنظیمات و IIS

در محیط مؤثر Test، `appsettings.json` ابتدا و سپس `appsettings.Test.json` بارگذاری می‌شوند. فایل Production یا Development به‌طور خودکار در Test بارگذاری نمی‌شود. AiService، Service JWT، Reports، Identity.Session و Notification.Delivery که در فایل پایه هستند و در الگوی Production کلید محیطی ندارند، از پایه به ارث می‌رسند؛ تست برابری این بخش‌ها از تغییر ناخواسته جلوگیری می‌کند. SignalR در `Program` ثبت می‌شود و برای این هم‌ساختارسازی بخش تنظیمات ساختگی دریافت نکرد.

اولویت مؤثر از پایین به بالا: JSON پایه → JSON محیط → User Secrets فقط در Development → environment variables → command line. با `WebApplication.CreateBuilder`، نام محیط `DOTNET_ENVIRONMENT` بر `ASPNETCORE_ENVIRONMENT` اولویت دارد؛ سه حالت این تقدم مستقیماً تست شدند.

برای سایت آزمایشی هر دو نام محیط باید Test باشند یا مقدار متعارض حذف شود:

```text
ASPNETCORE_ENVIRONMENT=Test
DOTNET_ENVIRONMENT=Test
```

پروفایل موجود `Properties/PublishProfiles/TestIis.pubxml` محیط Test را در خروجی IIS انتخاب می‌کند و فایل Production خصوصی و Development را از آن خروجی حذف می‌کند. `web.config` سورس همچنان Production است و در این مرحله تغییر نکرد. `launchSettings.json` انتخاب محیط IIS نیست. ویرایش سورس به معنی تعویض فایل مستقر نیست؛ برای تغییر مؤثر حالت احراز هویت، انتشار درست و restart/recycle لازم است.

## ۶. دیتابیس و Migration

هیچ Entity، EF configuration، snapshot، migration، PK/FK/index/constraint یا rowversion تغییر نکرد. Migration جدید یا اجرای Migration روی دیتابیس عادی و سرور انجام نشد. تست‌های SQL راه‌حل از زیرساخت ایزولهٔ موجود استفاده می‌کنند؛ مقدار `sql.example.test` در تست binding صرفاً syntax اتصال است و به آن وصل نمی‌شویم. Seed و نقش‌های کاربران در این مرحله تغییر نکردند.

## ۷. API و رفتار OTP

هیچ Method، Route، DTO، Response، ProblemDetails یا permission تغییر نکرد. تنظیمات OTP همچنان این ساختار را دارند:

```json
"Otp": {
  "Enabled": false,
  "AllowMockAuthentication": true
}
```

در Test، `OtpAuthenticationOptions.Load` هر دو boolean را می‌خواند و Mock موجود فعال می‌ماند؛ `AddInfrastructure` در این حالت adapter واقعی SMS را ثبت نمی‌کند. اعتبارسنجی فرمت شماره و کد، وضعیت حساب، session، role/resource authorization و MFA موجود تغییر نکردند.

در Production/Staging همین جفت false/true همچنان Startup را متوقف می‌کند. برای اجرای واقعی باید در تنظیم مؤثر جفت true/false بدهید و secretهای لازم را تأمین کنید. تست این مرحله با مقدارهای مصنوعی تأیید می‌کند که حالت واقعی همچنان `MeliPayamakOtpProvider` را ثبت می‌کند؛ ارسال واقعی پیامک در این مرحله انجام نشد.

## ۸. Options binding و validation

تست از factoryهای واقعی `DependencyInjection.AddInfrastructure` استفاده می‌کند، نه از مدل Options موازی. `OtpAuthenticationOptions` از `IOptions<T>` و validator موجود خوانده می‌شود؛ `OtpOptions`، `SessionOptions`، `MessagingOptions`، `MessagingEphemeralOptions`، `PushDeviceOptions`، `BackofficeSecurityOptions`، `PaymentOptions`، `ReportExportOptions`، `NotificationDeliveryOptions`، `OperationalMetricsOptions`، `TerminalFileCleanupOptions`، `AiServiceOptions`، `AiServiceAuthenticationOptions` و `StorageOptions` نیز resolve می‌شوند.

`DataProtectionKeyRingSettings` و `HostOptions` از مسیر موجود Host/MFA بررسی می‌شوند. binding شدن `MeliPayamakOtpOptions` به معنی معتبر یا فعال‌بودن token خالی نیست؛ در Mock adapter ثبت نمی‌شود و برای حالت واقعی token معتبرِ محیط لازم است. نبود connection string یا Identity key همچنان خطا می‌دهد. هیچ validator برای سبزشدن تست ضعیف یا حذف نشد.

## ۹. تصمیم‌های طراحی و امنیت

- الگوی Production نسخه‌بندی‌شده مرجع دائمی است؛ وابسته‌کردن CI فقط به فایل خصوصیِ معمولاً غایب، قابل بازتولید نبود. فایل خصوصی موجود در همان checkout علاوه بر الگو بررسی می‌شود.
- clone کردن credential واقعی رد شد؛ جای connection string، Identity key، ServiceToken و certificate password رشتهٔ خالی ماند. محیط اجرا باید فقط مقدارهای خصوصی همان محیط تست را بدهد.
- `Swagger.Enabled=true` و دو boolean OTP قبلی Test حفظ شدند؛ `Storage.RootPath` نمونهٔ Test از مسیر نمونهٔ Production جداست. این انتخاب داده‌ای جابه‌جا نمی‌کند و مجوز تغییر مسیر storage موجود سرور نیست.
- host/origin نمونه باید جایگزین شوند. پیش‌فرض HTTPS، محدودیت CORS، reverse proxy صریح و محافظ SQL حفظ شده‌اند؛ استثنای ناامن جدیدی اضافه نشد. برای سایت HTTP-only آزمایشی، راهنمای موجود تنظیم خصوصی و صریح را توضیح می‌دهد.
- key ring در Test همچنان پایدار و محافظت‌شده لازم است. نام canonical `Security:DataProtection:KeyRingPath` و alias قدیمی `BackofficeSecurity:KeyRingPath` باید در صورت حضور همزمان سازگار باشند. مسیر/Identity key موجود را بی‌دلیل عوض نکنید.
- هیچ secret، مقدار OTP، session token، ConnectionString واقعی یا payload خصوصی در log/assertion/report درج نشد. لاگ‌گیری امنیتی موجود تغییر نکرد.

هزینهٔ این تصمیم، نیاز به نگه‌داری schema هر دو فایل و تنظیم خصوصی معتبر پیش از راه‌اندازی است؛ تست recursive جلوی drift خاموش را می‌گیرد. هم‌شکل‌بودن فایل‌ها به معنی برابری policy محیط‌ها یا آمادگی واقعی سرور نیست.

## ۱۰. تست‌های اضافه‌شده

۱۸ مورد Unit: برابری schema؛ JSON معتبر و نبود کلید تکراری در چهار فایل نسخه‌بندی‌شده؛ هفت نمونهٔ منفی برای missing/extra key، nested structure، array و type؛ استقلال schema از مقدار/ترتیب؛ چهار جایگاه secret خالی؛ حفظ مقدارهای قبلی OTP/Swagger. این‌ها آزمون داده/مقایسهٔ خالص و بدون SQL/HTTP هستند.

۱۲ مورد Integration: بارگذاری JSON واقعی؛ ارث‌بری بخش‌های پایه؛ سه حالت نام محیط؛ دو حالت override محیط و command line؛ resolve شدن Options از DI واقعی؛ دو مورد نبود تنظیم ضروری؛ دو محیط Production/Staging و حفظ adapter واقعی. علاوه بر این‌ها تست‌های موجود Mock/Real OTP، منع Mock در Production، میزبان Test، MFA و authentication دوباره اجرا می‌شوند. تست انتخاب محیط در collection سریالی موجود است و تمام متغیرهای موقت را در Dispose بازمی‌گرداند.

Architecture test جدید لازم نبود چون هیچ dependency اجرایی تغییر نکرد؛ مجموعهٔ موجود باید کامل اجرا شود. مقایسهٔ فایل Production خصوصی در CI غایب جایگزین آزمون الگوی نسخه‌بندی‌شده نیست؛ آزمون الگو همیشه اجرا می‌شود و skip ندارد.

## ۱۱. Validation

آزمون اولیه پیش از تکمیل JSON: ۱۸ Unit اجرا شد، ۱۳ موفق و ۵ ناموفق؛ شکست‌ها نبود schema/کلیدهای ضروری Test را نشان دادند. پس از اصلاح، focused suite شامل ۵۳ Unit و ۸۲ Integration موفق شد؛ صفر failed/skipped. فیلتر focused برای پروژهٔ Architecture موردی انتخاب نمی‌کند و این به معنی پاس‌شدن Architecture نیست؛ مجموعهٔ کامل جداگانه اجرا می‌شود.

Restore موفق بود. برخورد نام `SessionOptions` در اولین build تست جدید با alias صریح رفع شد؛ build مجدد و build نهایی هر دو با صفر warning/error موفق شدند. اجرای کامل اول: ۸۴۵ Unit، ۱۳۳۵ Integration و ۱۴ Architecture؛ مجموع ۲۱۹۴ موفق، صفر failed/skipped. Integration این اجرا حدود ۱۴ دقیقه طول کشید.

اجرای نهایی کامل و بدون filter نیز موفق شد:

| مجموعه | اجراشده | موفق | ناموفق | Skip |
| --- | ---: | ---: | ---: | ---: |
| Unit | ۸۴۵ | ۸۴۵ | ۰ | ۰ |
| Integration | ۱۳۳۵ | ۱۳۳۵ | ۰ | ۰ |
| Architecture | ۱۴ | ۱۴ | ۰ | ۰ |
| مجموع | ۲۱۹۴ | ۲۱۹۴ | ۰ | ۰ |

Integration نهایی ۹ دقیقه و ۳۰ ثانیه طول کشید. counterهای شش فایل TRX برای هر دو اجرای کامل کنترل شدند. خروجی‌ها در `tests/<TestProject>/TestResults/test-config-alignment-regression.trx` و `test-config-alignment-final.trx` هستند و وارد Git نمی‌شوند. هیچ تستی برای سبزشدن نتیجه غیرفعال نشد. Migration جدید لازم نبود و روی دیتابیس عادی یا سرور اجرا نشد.

## ۱۲. Git و حفظ تغییرات قبلی

این هم‌ساختارسازی یک عملیات منطقی واحد است؛ JSON، آزمون‌ها و مستند همان عملیات پس از Validation نهایی در یک Commit مرتبط ثبت و به upstream موجود push می‌شوند، نه commitهای بی‌معنیِ تک‌خطی. پیام انتخابی:

```text
chore(config): align test settings with production schema
```

Branch: `identity/configurable-otp-20261021`؛ upstream: `origin/main`؛ remote: `https://github.com/RastinaxAI/MakanApp.git`. شناسه و نتیجهٔ واقعی Commit/Push/remote verification در گزارش نهایی گفتگو ثبت می‌شود؛ برای یافتن commit حامل این سند از `git log -1 --format="%H %s" -- docs/dev/TEST_CONFIGURATION_ALIGNMENT_FA.md` استفاده کنید.

فقط appsettings.Test، دو فایل تست و دو سند مرتبط باید وارد Commit شوند. Production خصوصی و artifactهای تست وارد Git نمی‌شوند. checkout اصلی با ۲۲ فایل dirty قبلی AI جدا نگه داشته شده و باید با hashهای baseline بدون تغییر تأیید شود. هیچ reset/stash/clean یا force-push مجاز نیست.

## ۱۳. محدودیت‌ها و بدهی فعلی

این فایل بدون تنظیم secretها، host/originهای واقعی و key ring معتبر یک پیکربندی آمادهٔ اجرای سرور نیست. هیچ استقرار، دستکاری IIS، تست شبکهٔ واقعی سرور، تماس AI یا ارسال واقعی SMS انجام نشد. فایل Production خصوصی checkout قدیمی ممکن است نسبت به الگوی فعلی تفاوت داشته باشد؛ این مرحله برای حفظ داده‌های مالک آن را بازنویسی نمی‌کند. publish باید از نسخهٔ جدید انجام شود، نه checkout قدیمی فاقد implementation جدید.

Mock برای حساب‌های تست در شبکهٔ کنترل‌شده است؛ هر شخصی با دسترسی شبکه می‌تواند حساب تست دیگری را با کد دلخواهِ دارای فرمت معتبر انتخاب کند. تغییر config به‌تنهایی sessionهای قبلی را revoke نمی‌کند. این مرحله تنظیم Production برای Mock را مجاز نمی‌کند.

## ۱۴. نکات آموزشی

۱. schema، مقدار محرمانه و policy محیط سه مفهوم مستقل‌اند.
۲. environment variable یا command line می‌تواند JSON ظاهراً درست را override کند؛ providerهای واقعی را تست کنید.
۳. مقدار true/false متفاوت، mismatch نوع نیست؛ stringِ «false» با boolean false فرق دارد.
۴. `IOptions<T>` تنها مسیر موجود نیست؛ کارخانه‌های صریحِ map کردن seconds به TimeSpan هم باید تست شوند.
۵. تست schema نباید مقدار credential را در خروجی مقایسه چاپ کند.
۶. محیط Test با Testing یکی نیست؛ در این پروژه Test الزامات key ring و محدودیت‌های عملیاتی مهم را نگه می‌دارد.

## ۱۵. مسیر مطالعهٔ کد

۱. `MakanApp.Api/appsettings.Test.json` در کنار `appsettings.Production.json.example` و فایل پایه.
۲. `MakanApp.Api/Program.cs`: ایجاد builder و ثبت سرویس‌ها.
۳. `MakanApp.Infrastructure/DependencyInjection.cs`: factoryهای Options و ثبت adapterها.
۴. `MakanApp.Infrastructure/Identity/OtpAuthenticationOptions.cs`: محافظ محیط و دو فلگ.
۵. `MakanApp.Api/Configuration/DataProtectionKeyRingSettings.cs`: الزام و سازگاری aliasها.
۶. `MakanApp.Api/Properties/PublishProfiles/TestIis.pubxml`: محیط بستهٔ IIS.
۷. `TestConfigurationSchemaTests.cs` و `TestEnvironmentConfigurationTests.cs` و سپس تست‌های قبلی OTP.

Domain/EF/Migration در این تغییر فایل جدیدی ندارند و برای این مرحله مسیر مطالعهٔ ساختگی معرفی نشده است.

## ۱۶. مرحلهٔ بعد

مالک، مقادیر خصوصیِ همان محیط آزمایشی را مطابق راهنمای SERVER_MOCK_OTP_FA تنظیم کند و سپس از checkout جدید publish بگیرد. پس از استقرار و recycle، health، readiness و ورود Mock روی حساب‌های تست بررسی شوند. تغییر provider، Seed، Migration و تضعیف محافظ Production نباید با این هم‌ساختارسازی مخلوط شوند.
