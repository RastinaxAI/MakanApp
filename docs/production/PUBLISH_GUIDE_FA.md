# راهنمای انتشار MakanApp Backend روی IIS — Test و Production

> یادداشت ۲۰۲۶-۱۰-۲۴: این سند نتیجه و راهنمای بسته‌های مشخص ۲۰۲۶-۱۰-۲۳ است. در کد جاری، MFA و وابستگی key ring حذف شده‌اند؛ ZIPهای قدیمی این سند بازسازی نشده‌اند و شامل تغییر جدید نیستند. برای نسخهٔ جدید، [قرارداد حذف عامل دوم و Migration](../platform/PLATFORM_AUTHENTICATION_CHANGE_FA.md) مرجع است؛ فایل‌های قدیمی key ring سرور فعلاً حذف نشوند.

تاریخ بسته: `2026-10-23`؛ مخزن اصلی: `D:\Project\MakanApp\MakanApp-Backend`.
این راهنما برای دو بستهٔ مستقل Windows/IIS است؛ ساخت بسته، استقرار یا تأیید سلامت سرور نیست.
در این مرحله کد برنامه، Authentication، AI، ملی‌پیامک، مدل و Migration تغییر نمی‌کنند.

## ۱. انتخاب بسته

| بسته | محیط | OTP | کاربرد |
|---|---|---|---|
| `MakanApp-Test-*.zip` | `Test` | `Enabled=false`، `AllowMockAuthentication=true` | فقط حساب و دادهٔ آزمایشی در محیط محدود |
| `MakanApp-Production-*.zip` | `Production` | `Enabled=true`، `AllowMockAuthentication=false` | مسیر OTP واقعی موجود با ملی‌پیامک |

بسته‌ها را روی یک پوشه Extract نکنید. فایل Test نباید وارد سایت Production شود.
Mock مالکیت شماره را اثبات نمی‌کند؛ Test را عمومی یا به داده، credential، Storage و key ring واقعی Production متصل نکنید.
شمارهٔ دارای فرمت معتبر و کد دارای فرمت مجاز در Mock پذیرفته می‌شود؛ حد فعلی کد ۴ تا ۶۴ کاراکتر بدون control character است.
حساب معلق، Role، Membership، workspace، مجوز منابع و MFA با خاموش‌کردن OTP دور زده نمی‌شوند.
در Production تنظیم ناامن OTP باعث رد startup می‌شود؛ تغییر نام محیط راه‌حل مجاز خطای Production نیست.

## ۲. محتویات و محرمانگی

هر ZIP باید در ریشهٔ خود `MakanApp.Api.dll`، وابستگی‌ها، `web.config`، `appsettings.json`
و فقط `appsettings.<Environment>.json` مربوط به خودش داشته باشد.
فایل Development، فایل خصوصی محیط مقابل، سورس، ابزار AI و backup در بستهٔ اجرایی لازم نیستند.
نسخهٔ همین راهنما در بسته‌ها نیز قرار می‌گیرد؛ ZIP شامل محتوای پوشهٔ Publish است، نه سورس Solution.
پیش از انتقال و پس از کپی روی سرور، SHA-256 فایل ZIP را با `Get-FileHash -Algorithm SHA256` بگیرید و با `SHA256SUMS.txt` همراه تحویل مقایسه کنید؛ اختلاف hash یعنی بسته نباید منتشر شود.

**بسته‌های این تحویل تنظیمات خصوصی دارند.** ZIP، پوشهٔ خروجی و backup تنظیمات را در Git، لینک عمومی یا چت نگذارید.
Connection String، `Identity:SecurityKey` و `Sms:MeliPayamakOtp:ServiceToken` را چاپ یا وارد مستندات نکنید.
Secretهای فعلی سرور را با مقادیر ماشین توسعه بی‌خبر جایگزین نکنید؛ تنظیمات مقصد باید پیش از جابه‌جایی بازبینی شوند.
User Secrets لپ‌تاپ به سرور منتقل نمی‌شوند. برای AI از provisioning امن همان محیط استفاده کنید؛ JWT سرویس را داخل JSON ننویسید.

## ۳. پیش‌نیاز سرور

- IIS و ASP.NET Core Module V2 با .NET 9 Hosting Bundle؛ Runtime نصب‌شده باید `Microsoft.AspNetCore.App 9.x` را داشته باشد.
- App Pool اختصاصی، `No Managed Code`، معماری 64-bit متناسب با سرور و تنها یک worker process؛ Web Garden/scale-out با این بسته تأیید نشده است.
- WebSocket Protocol برای SignalR و Binding سایت با host/port صحیح.
- دسترسی App Pool به SQL Server و مسیرهای persistent؛ مجوز Read/Execute روی Publish و مجوز لازم روی Storage و key ring.
- SQL Server اصلی، نه LocalDB تست؛ runtime principal کم‌اختیار و migration principal جدا.

خروجی Framework-dependent است؛ نصب SDK روی سرور برای اجرای برنامه لازم نیست، ولی Hosting Bundle مناسب لازم است.
این مرحله چیزی روی سرور نصب یا تنظیم نمی‌کند.

## ۴. محیط صحیح در web.config و IIS

`web.config` تولیدی را از همان بسته استفاده کنید. مقدار موجود `ASPNETCORE_ENVIRONMENT` باید با بسته یکسان باشد.
اگر `DOTNET_ENVIRONMENT` در IIS/سیستم تنظیم شده، آن را نیز هم‌نام کنید؛ مقدار متعارض می‌تواند انتخاب فایل تنظیمات را تغییر دهد.
نمونهٔ زیر فقط fragment است؛ جایگزین کل `web.config` و تنظیمات ANCM نشود:

```xml
<!-- فقط سایت آزمایشی -->
<environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Test" />
<environmentVariable name="DOTNET_ENVIRONMENT" value="Test" />
```

```xml
<!-- فقط سایت واقعی -->
<environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
<environmentVariable name="DOTNET_ENVIRONMENT" value="Production" />
```

کلید تکراری environmentVariable نسازید؛ مقدار موجود را ویرایش کنید.
`processPath="dotnet"`، `arguments=".\MakanApp.Api.dll"`، `hostingModel="inprocess"` و budget خاموشی موجود حفظ شوند.
`stdoutLogEnabled` پیش‌فرض خاموش بماند؛ stdout دائمی جای logger امن برنامه نیست.
متغیرهای محیطی روی JSON اولویت دارند؛ قبل از نسبت‌دادن خطا به فایل جدید، overrideهای قدیمی IIS را بررسی کنید.

## ۵. تنظیمات مقصد؛ بدون تغییر Secretها

| تنظیم | کنترل پیش از اجرا |
|---|---|
| `ConnectionStrings:MakanDatabase` | دیتابیس درست همان محیط، credential معتبر و schema سازگار |
| `Identity:SecurityKey` | کلید پایدار قبلی؛ تعویض تصادفی آن هنگام انتشار مجاز نیست |
| `Otp:Enabled` / `Otp:AllowMockAuthentication` | Test=`false/true`؛ Production=`true/false` |
| `Sms:Provider` | Production با provider موجود `MeliPayamakOtp`؛ Mock نباید پیامک بفرستد |
| `Sms:MeliPayamakOtp:BaseUrl/ServiceToken/TimeoutSeconds` | تنظیمات معتبر همان محیط؛ Token را در log ننویسید |
| `Storage:RootPath` | مسیر مطلق و persistent خارج از Publish؛ فایل‌های قبلی حفظ شوند |
| `AllowedHosts` / `Cors:AllowedOrigins` | host و origin واقعی و صریح؛ بدون wildcard Production |
| `ReverseProxy:Enabled/KnownProxies` | IIS و proxyهای واقعاً مورد اعتماد؛ اعتماد سراسری اضافه نکنید |
| `Swagger:Enabled` | در Production نمایش Swagger/OpenAPI تابع این فلگ است؛ Test طبق کد موجود همیشه آن‌ها را منتشر می‌کند |

ساختار Test و Production باید دوطرفه برابر بماند، ولی مقادیر مخصوص محیط نباید با هم overwrite شوند.
فایل‌های نمونهٔ `.json.example` بدون Secret هستند و به‌تنهایی برای اجرای واقعی کافی نیستند.

### Swagger این انتشار

طبق تأیید مالک، فقط در JSON داخل هر دو بسته `Swagger:Enabled=true` شده است؛ فایل‌های تنظیمات اصلی مخزن تغییر نکرده‌اند.
Swagger/OpenAPI در Production با این فلگ فعال‌اند، ولی endpointهای کسب‌وکاری همچنان Authentication/Authorization دارند.
برای بازتولید همین بسته پس از فرمان Publish، فقط فلگ Swagger فایل خروجی همان محیط را true کنید
یا override مجاز `Swagger__Enabled=true` را روی همان سایت مقصد تنظیم کنید؛ Secretها و کل فایل config را بازنویسی نکنید.

### HTTP فعلی و SQL certificate

استثناهای HTTP و SQL certificate قبلاً به‌صورت خصوصی تأیید شده‌اند؛ این انتشار آن‌ها را بی‌خبر تغییر نمی‌دهد.
برای حالت امن، `Hosting:RequireHttps=true`، `Hosting:AllowInsecureHttp=false`،
`TrustServerCertificate=False` و `Database:AllowUntrustedServerCertificate=false` لازم‌اند.
در انتشار HTTP فعلی، `RequireHttps=false/AllowInsecureHttp=true` و origin صریح HTTP به‌صورت استثنا استفاده می‌شوند.
روی HTTP، OTP و token در شبکه رمزگذاری نمی‌شوند؛ برای login واقعی، TLS مقصد را قبل از استفادهٔ عمومی آماده کنید.
استثنای گواهی SQL مجوز `Encrypt=False` یا SQL عمومی روی اینترنت نیست.

## ۶. Data Protection و MFA

مسیر پیش‌فرض موجود در web.config، `C:\ProgramData\MakanApp\BackofficeMfaKeys` است.
اگر سرور قبلاً مسیر دیگری دارد، همان مسیر و کلیدها را حفظ کنید؛ Test و Production نباید حلقهٔ کلید مشترک داشته باشند.
مسیر key ring نباید داخل Publish یا Storage عمومی باشد. پوشه و ACL باید توسط اپراتور و با identity واقعی App Pool آماده شوند.
`BackofficeSecurity:KeyRingPath` و alias جدید `Security:DataProtection:KeyRingPath` در صورت استفادهٔ هم‌زمان باید سازگار باشند.
Windows بدون certificate از DPAPI وابسته به هویت کاربر استفاده می‌کند؛ identity/profile App Pool را بی‌برنامه عوض نکنید.
کپی XML به سرور دیگر، بازیابی MFA را تضمین نمی‌کند؛ سند `docs/production/DEPLOYMENT_IIS.md` در مخزن را برای custody/محافظ گواهی ببینید.

## ۷. ترتیب انتشار دستی روی سرور

۱. محیط، سایت، App Pool و physical path دقیق را مشخص کنید؛ این راهنما هیچ سایت یا نام واقعی را حدس نمی‌زند.
۲. از نسخهٔ اجرایی قبلی، تنظیمات خصوصی، SQL و key ring نسخهٔ پشتیبان امن تهیه و راه بازگشت را ثبت کنید.
۳. ZIP درست را به پوشهٔ نسخهٔ تازه Extract کنید؛ روی فایل‌های در حال اجرا یا دو محیط مختلف overwrite نکنید.
۴. تنظیمات خصوصی مقصد، envهای متعارض، Storage، key ring، ACL و schema را بررسی کنید.
۵. در پنجرهٔ maintenance سایت و App Pool مربوط را کنترل‌شده متوقف و خروج worker را تأیید کنید؛ توقف صرف سایت کافی نیست.
۶. physical path را به نسخهٔ تازه تغییر دهید یا فایل‌های اجرایی را در مقصد تأییدشده جایگزین کنید؛ داده‌های persistent خارج آن می‌مانند.
۷. App Pool و سایت را شروع کنید و health/ورود/مجوزها را بررسی کنید.
۸. فقط پس از موفقیت بررسی‌ها نسخه را پذیرفته اعلام کنید؛ نسخه و backup قبلی را فوراً حذف نکنید.

این مراحل دستور انجام خودکار روی سرور نیستند؛ هیچ Stop/Start، IIS deployment یا migration سرور از این رایانه اجرا نشده است.

## ۸. دیتابیس و Migration

برای ساخت مجدد این بسته‌ها Migration جدیدی لازم نیست؛ آخرین Migration موجود
`20261017033804_AddConversationChecklists` و تعداد فعلی ۴۸ است.
برنامه هنگام startup migration اجرا نمی‌کند. وضعیت دیتابیس سرور از build یا Publish نتیجه نمی‌شود.
اگر history سرور عقب است، پس از Backup و بررسی pendingها فقط اسکریپت امن موجود را در کار جداگانه تولید/بازبینی کنید:

```powershell
& ./tools/production/export-migration-sql.ps1 -OutputPath .artifacts/deployment/migrations-idempotent-safe.sql
```

خروجی قدیمی خام EF را که خطای ستون‌های `Visibility/GroupHistoryPolicy/MemberCapabilities` می‌داد دوباره اجرا نکنید.
این فرمان فقط فایل تولید می‌کند؛ اجرای SQL نیازمند انتخاب دیتابیس صحیح و مجوز مستقل است.

## ۹. بررسی پس از انتشار و عیب‌یابی

با base URL واقعی مقصد و بدون چاپ credential:

```text
GET /health/live       → 200
GET /health/ready      → 200؛ وابسته به SQL/Storage/key ring/config
GET /api/v1/me         → 401 بدون نشست
GET /swagger/index.html → Test: 200؛ Production: فقط با Swagger:Enabled=true
GET /openapi/v1.json    → Test: 200؛ Production: فقط با Swagger:Enabled=true
```

در Test یک حساب تست مجاز با کد دارای فرمت معتبر وارد شود و پیامک واقعی ارسال نشود.
در Production تست واقعی OTP فقط با اجازهٔ مالک و شمارهٔ مجاز، نه شماره‌های ساختگی، انجام شود.
کد، token و Authorization header را در شواهد ذخیره نکنید؛ status، error code و correlationId کافی‌اند.
Health موفق، تحویل واقعی SMS/Push، AI Full-12 یا مرورگر/دستگاه واقعی را ثابت نمی‌کند.

| نشانه | بررسی |
|---|---|
| startup رد می‌شود | Environment/فلگ OTP، config لازم، key ring/ACL، aliasهای متعارض |
| IIS 500.30/500.31 | Hosting Bundle/runtime، web.config و خطای startup؛ اطلاعات خام حساس منتشر نشود |
| Swagger 404 | در Production مقدار مؤثر `Swagger:Enabled`، محیط و override IIS؛ ربطی به حذف API ندارد |
| ready=503 | SQL، storage و key ring؛ live=200 به معنی آماده‌بودن همهٔ وابستگی‌ها نیست |
| OTP نامعتبر در Production | challenge/expiry/attempts، کد provider و ثبات `Identity:SecurityKey`؛ محافظ را خاموش نکنید |
| MFA پس از جابه‌جایی خراب شده | مسیر/identity/محافظ همان key ring قبلی؛ حلقهٔ تازه جایگزین آن نکنید |

## ۱۰. Rollback و تولید مجدد

در صورت failure، سایت را از دسترس خارج و App Pool را کنترل‌شده متوقف کنید، نسخه و config قبلی را با همان Storage/key ring برگردانید و health را دوباره بررسی کنید.
بازگشت binary به‌تنهایی downgrade دیتابیس نیست؛ برای این انتشار schema جدید نداریم، اما compatibility نسخهٔ برگشتی باید بررسی شود.
کلیدهای MFA، فایل‌های کاربران و دیتابیس را برای «رفع سریع» حذف نکنید.

برای تولید بسته از مخزن اصلی، ابتدا restore/build/test موفق و سپس:

```powershell
dotnet publish MakanApp.Api/MakanApp.Api.csproj --configuration Release --no-restore -p:PublishProfile=TestIis --output .artifacts/publish/<new-release>/test
dotnet publish MakanApp.Api/MakanApp.Api.csproj --configuration Release --no-restore -p:PublishProfile=ProductionIis --output .artifacts/publish/<new-release>/production
```

هر بار پوشهٔ تازه انتخاب کنید؛ بسته را با اجرای برنامه از پوشهٔ Publish و overrideهای امن محلی بررسی کنید، نه اتصال ناخواسته به Production.

## نتیجهٔ این تحویل

کد بسته‌ها از commit `593b6bfcdde44b8b8bbf5bb7ec2ef1805c51b802` ساخته شده است؛ commit این تحویل فقط همین سند را اضافه می‌کند.
Restore موفق بود؛ Buildهای Debug و Release و Build نهایی Release همگی با صفر Warning و صفر Error موفق شدند.

| اجرای کامل | Unit | Integration | Architecture | مجموع موفق | Failed / Skipped |
|---|---:|---:|---:|---:|---:|
| Debug | 873 | 1352 | 14 | 2239 | 0 / 0 |
| Release نهایی | 873 | 1352 | 14 | 2239 | 0 / 0 |

هشت تست موجود `ReleaseEnvironmentOtpTests` نیز در هر دو نوبت موفق‌اند. تست جدیدی اضافه نشده است.
اسمبلی‌های هر دو خروجی با SHA-256 اسمبلی‌های Build نهایی Release تطبیق داده شدند.
JSONها معتبرند؛ تنها تفاوت تنظیمات خصوصی خروجی با فایل اصلی همان محیط، `Swagger:Enabled=true` تأییدشده است.
کد و تنظیمات اصلی Authentication، AI و ملی‌پیامک تغییر نکرده‌اند؛ سه فایل untracked قبلی AI و دو config خصوصی با hash اولیه برابرند.

اجرای واقعی DLL از هر دو پوشهٔ Publish با overrideهای محلی ایزوله و Swagger فعال بررسی شد:

| مسیر | Test | Production |
|---|---:|---:|
| `/health/live` | 200 | 200 |
| `/health/ready` | 200 | 200 |
| `/api/v1/me` بدون ورود | 401 | 401 |
| `/swagger/index.html` | 200 | 200 |
| `/openapi/v1.json` | 200 | 200 |

صفر Warning مربوط به Build است؛ دو هشدار قدیمی EF دربارهٔ sentinel برای `OnlineAudience/LastSeenAudience` و تشخیص امن `10103` در smoke دیده شدند و پنهان نشده‌اند.
Migration جدید نداریم و EF تغییر مدل pending پیدا نکرد؛ ۴۸ Migration موجود حفظ شده‌اند.
دیتابیس موقت smoke پس از توقف برنامه‌ها حذف شد؛ دیتابیس اصلی، IIS و هیچ سروری تغییر نکردند. پیامک واقعی یا AI فراخوانی نشد.

محل تحویل خصوصی خارج از Git:

```text
.artifacts/publish/20261023-f0a708ac-593b6bf/
├── MakanApp-Test-20261023-593b6bf.zip
├── MakanApp-Production-20261023-593b6bf.zip
├── PUBLISH_GUIDE_FA.md
└── SHA256SUMS.txt
```

ZIPها تنظیمات خصوصی دارند؛ فقط امن و برای اپراتور مجاز منتقل شوند. پذیرش واقعی سرور، TLS و سرویس‌های خارجی همچنان باید در مقصد انجام شود.

## گزارش آموزشی این مرحله

۱. **خلاصه:** دو بستهٔ مستقل و راهنمای عملیاتی؛ این مرحله افزودن قابلیت کسب‌وکاری نیست.
۲. **معماری:** همان Onion/Modular Monolith؛ Host و profileهای Api استفاده شدند و هیچ لایهٔ داخلی تغییر نکرد.
۳. **فایل مهم جدید:** همین راهنما؛ مسیر اجرایی و تصمیم‌های ایمن انتشار را بدون Secret ثبت می‌کند.
۴. **فایل مهم تغییرکرده:** کد، مدل، تنظیمات اصلی و profileهای موجود تغییر نمی‌کنند؛ فقط مستند تازه نسخه‌بندی می‌شود.
۵. **جریان اجرا:** IIS/ANCM → `Program` → اعتبارسنجی configuration/DI → `DataProtectionKeyRingStartupValidator` → endpointها و workerهای موجود.
۶. **دیتابیس:** تغییر schema و Migration جدید نداریم؛ تست‌های SQL فقط روی دیتابیس ایزوله‌اند.
۷. **Endpoint:** API جدید نداریم؛ liveness/readiness، پاسخ 401 بدون نشست و Swagger مؤثر بررسی می‌شوند.
۸. **مفاهیم:** Framework-dependent Publish، انتخاب Environment، override configuration و نگهداری persistent data.
۹. **تصمیم:** بستهٔ Test و Production جدا؛ Secretها فقط در artifact خصوصی، نه Git؛ deploy از ساخت بسته جداست.
۱۰. **تست:** تست جدید برای رفتار تغییرنیافته اضافه نمی‌شود؛ suiteهای Unit/Integration/Architecture و smoke خروجی استفاده می‌شوند.
۱۱. **Validation:** فقط نتایج واقعی بخش نتیجهٔ تحویل معتبرند؛ خروجی قبلی جای اجرای این مرحله نیست.
۱۲. **Git:** commit مستند، branch/upstream و نتیجهٔ Push در گزارش نهایی اعلام می‌شوند؛ artifactها Commit نمی‌شوند.
۱۳. **محدودیت:** پذیرش واقعی IIS/TLS/Backup، ارسال SMS و AI Full-12 در این مرحله انجام نمی‌شوند.
۱۴. **نکات آموزشی:** اسم پوشه Environment نیست؛ env مؤثر را کنترل کنید. Key ring دادهٔ ماندگار است. Mock فقط ابزار محیط تست است. Health جای تست provider نیست. ZIP حاوی config خصوصی هم Secret محسوب می‌شود.
۱۵. **مسیر مطالعه:** این راهنما → `TestIis.pubxml`/`ProductionIis.pubxml` → `Program.cs` → `OtpAuthenticationOptions` → اسناد IIS و تست‌های `ReleaseEnvironmentOtpTests`.
۱۶. **مرحلهٔ بعد:** اپراتور بستهٔ مناسب را با تنظیمات و backup مقصد مستقر و smoke واقعی سرور را اجرا کند؛ هیچ اصلاح AI/Frontend به این انتشار مخلوط نشود.
