# GAP-02 — شواهد staging و smoke واقعی core

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

تاریخ محلی: ۲۰۲۶-۱۰-۱۵ (`Asia/Tehran`). وضعیت: **GAP_02_BLOCKED_OPERATIONAL_VALIDATION**.

آماده‌سازی و validation محلی انجام شده است؛ دریافت واقعی SMS، ورود واقعی و تأیید deployment هدف انجام نشده‌اند. مالک پروژه صریحاً اعلام کرد مقصد فعلی HTTPS ندارد. نبود HTTPS یا آماده‌نبودن محیط، مجوز استفاده از bypass گواهی، ارسال OTP/token روی HTTP یا تغییر policy امنیتی نیست.

## ۱. مبنا و دامنه

- مبنای Git: `ca448406cb8d3538fc6d3a703c6cea2d6fef0927`؛ GAP-01 بسته بود.
- validation این سند روی همان baseline به‌همراه تغییرات GAP-02 در worktree اجرا شده؛ hash commit حامل آماده‌سازی در گزارش نهایی Git می‌آید. این سند hash آینده خودش را حدس نمی‌زند.
- ۱۱ فایل dirty قبلی AI در baseline SHA-256 ثبت شدند؛ دست‌نخورده و خارج از staging/commit این مرحله هستند.
- خروجی publish محلی از worktree شامل آن تغییرات قبلی AI ساخته شد؛ artifact انتشار clean و قابل انتساب صرفاً به commit محسوب نمی‌شود. برای deployment واقعی، از checkout تمیز commit تأییدشده artifact بسازید. هیچ AI call یا STEP10G اجرا نشده است.
- زبان/معماری/قرارداد کسب‌وکاری، SQL Server، نسخه .NET 9، MeliPayamak، Messenger و Backoffice تغییر نکرده‌اند.

## ۲. یافته و اصلاح مخزن

| موضوع | قبل | اصلاح محدود |
|---|---|---|
| fail-fast محیط | کنترل سخت‌گیرانه فقط Production بود | `ProductionConfigurationValidator` همان قواعد TLS SQL، HMAC، root، host، CORS، HTTPS و proxy را برای Staging هم اجرا می‌کند |
| Swagger | شرط `!IsProduction` در Staging همواره آن را باز می‌کرد | Staging مانند Production به گزینه صریح وابسته است؛ فایل Staging موجود اکنون پیش‌فرض false دارد |
| تنظیمات Staging | فقط Swagger/logging داشت | جای‌خالی امن تنظیمات الزامی اضافه شد؛ مقدار محرمانه‌ای درج نشده است |
| smoke زنده | ابزار قبلی عمداً OTP را اجرا نمی‌کرد | ابزار جدا، opt-in و تعاملی اضافه شد؛ ابزار قبلی و تست‌های عادی همچنان SMS زنده نمی‌فرستند |
| rehearsal migration | دستور EF موجود بود، target-guard اختصاصی نداشت | ابزار محلی فقط DB تازه با نام مستقل `MakanApp_Gap02_<guid>` روی LocalDB می‌سازد؛ مقصد موجود را رد می‌کند و هیچ DROP ندارد |

هیچ endpoint جدید، migration جدید، bypass امنیتی، provider جدید یا تغییر قواعد OTP ایجاد نشده است. توکن نشست opaque و HMAC سروری موجود است؛ JWT/signing-secret جدیدی اختراع نشده است.

## ۳. محیط واقعاً اجراشده

- OS محلی: Windows 11 Pro، `10.0.22000`؛ نه Windows Server هدف.
- SDK .NET 9 موجود: `9.0.300`؛ runtime ASP.NET Core/.NET 9 موجود: `9.0.5`؛ target تمام پروژه‌ها net9.0 باقی ماند.
- سرویس‌های محلی W3SVC/WAS در حال اجرا بودند؛ سایت، binding، certificate یا App Pool محلی/remote تغییر نکرد.
- launch واقعی artifact روی Kestrel loopback با environment برابر Staging انجام شد؛ تنظیمات از environment process تزریق شدند. HMAC تصادفی مخصوص همان آزمایش بود و در Git/لاگ ثبت نشد؛ کلید موقت، جای provisioning کلید پایدار سرور را نمی‌گیرد.
- `Sms:Provider=Unavailable` بود؛ آزمون راه‌اندازی محلی هیچ پیامکی ارسال نکرد.
- برای شبیه‌سازی proxy، درخواست loopback با `X-Forwarded-Proto: https` از proxy مجاز محلی فرستاده شد. این شبیه‌سازی، اثبات TLS اینترنت یا IIS واقعی نیست.
- process آزمایش محلی بعد از smoke متوقف شد؛ سرویس staging متوقف/راه‌اندازی مجدد نشد.

## ۴. ماتریس شواهد

| آزمون | دامنه | نتیجه | شاهد/محدودیت |
|---|---|---|---|
| restore/build/test | مخزن | PASS | 1021 تست موفق؛ بخش ۸ |
| migration از DB خالی | LocalDB مستقل | PASS | تمام ۳۵ migration و history منطبق با source |
| EF pending model | محلی | PASS | مدل تغییری خارج migration ندارد |
| SQL بعد migration | محلی | PASS | تاریخچه از همان DB خوانده شد؛ readiness host نیز 200 شد |
| publish/web.config | artifact محلی Release | PASS | AspNetCoreModuleV2، processPath=dotnet، arguments مربوط به MakanApp.Api.dll، inprocess، stdoutLogEnabled=false، environment=Staging |
| startup/environment | Kestrel محلی منتشرشده | PASS | بدون exception بالا آمد؛ Hosting environment: Staging مشاهده شد |
| `/health` و `/health/live` | Kestrel محلی | PASS | HTTP 200 |
| `/health/ready` | Kestrel محلی | PASS | HTTP 200؛ اتصال SQL و storage probe واقعی |
| `/api/v1/me` بدون token | Kestrel محلی | PASS | HTTP 401 |
| `/openapi/v1.json` پیش‌فرض | Kestrel محلی | PASS | HTTP 404 |
| known-secret log check | log همان process محلی | PASS | کلید HMAC موقت و connection string کامل در stdout/stderr نبودند؛ نه ممیزی log سرور/SMS |
| HTTPS هدف | مقصد معرفی‌شده | BLOCKED | مالک تأیید کرد HTTPS ندارد؛ probe امن timeout شد |
| `/health` هدف | HTTP بدون credential | PASS | 200 و بدنه Healthy؛ فقط liveness |
| `/health/ready` هدف | HTTP بدون credential | FAIL | 404؛ نسخه deployment یا routing باید بررسی شود |
| `/api/v1/me` هدف بدون token | HTTP بدون credential | PASS | 401 با ProblemDetails؛ نه login موفق |
| environment/commit هدف | سرور | NOT_EXECUTED | بدون دسترسی deployment و شاهد artifact، از health قابل تشخیص نیست |
| migration/TLS/principal هدف | SQL سرور staging | NOT_EXECUTED | مدرک تاریخچه، certificate و runtime principal ارائه نشده است |
| real MeliPayamak challenge | سرور | BLOCKED | درخواست ارسال نشده؛ HTTPS معتبر پیش‌شرط است |
| SMS received | سرور | NOT_EXECUTED | نه YES و نه ادعای عدم تحویل؛ ارسال اصلاً اجرا نشده است |
| OTP verify و session | سرور | NOT_EXECUTED | کد یا token دریافت/منتشر نشده است |
| wrong OTP زنده | سرور | NOT_EXECUTED | سناریوی عادی/ساختگی در تست‌ها موجود است؛ provider را تکراری فراخوانی نکردیم |
| profile/workspace/org read | با نشست واقعی staging | NOT_EXECUTED | login امن هنوز ممکن نیست |
| cross-org منفی | staging | NOT_EXECUTED | داده و نشست مجاز مرحله بعد لازم است |
| SignalR/WebSocket/reconnect | IIS هدف | NOT_EXECUTED | ابزار آماده است؛ موفقیت تست‌های .NET/ساختگی، موفقیت browser/IIS نیست |
| storage upload/download/ACL | IIS هدف | NOT_EXECUTED | probe محلی موفق، ولی identity/ACL سرور و roundtrip هدف اثبات نشده است |
| log-redaction هدف | IIS/provider/proxy | NOT_EXECUTED | log بازه تست زنده در اختیار نبود؛ stdout logging artifact خاموش است |

زمان probe بدون credential: `2026-10-14T22:28:53Z` تا شروع probe HTTPS در `22:28:55Z`؛ HTTPS پس از timeout ده‌ثانیه‌ای بدون پاسخ HTTP تمام شد. مقصد `http://94.184.32.84` بود؛ `/health` و `/api/v1/me` در این آزمایش redirect HTTPS نداشتند. تنها GETهای عمومی ارسال شدند؛ هیچ شماره واقعی، OTP، Authorization یا secret ارسال نشد.

وجود readiness محلی در کنار 404 هدف، نیاز به بررسی نسخه/routing را اثبات می‌کند؛ علت قطعی 404 یا environment سرور از این شواهد قابل استنتاج نیست.

## ۵. دیتابیس و artifact آزمایش

دیتابیس مستقل ایجادشده و حفظ‌شده:

`MakanApp_Gap02_36bf16a03a6943c3b1c20a2abe6339dc`

آخرین migration: `20261014214734_AddOperationalPersonGuardianOnboarding`؛ مجموع ۳۵. schema هدف عادی/Production تغییر نکرد. ابزار EF موجود و design-time factory استفاده شدند؛ EnsureCreated یا auto-migrate startup افزوده نشد.

فرمان تکرار محلی، بعد از build:

```powershell
& .\tools\production\validate-local-migrations.ps1 -ConfirmLocalDisposableDatabase
```

ابزار برای هر اجرا DB تازه می‌خواهد، connection قبلی process را در finally برمی‌گرداند و هیچ دیتابیسی را حذف نمی‌کند. تنظیم آسان‌تر TLS صرفاً برای rehearsal LocalDB داخل همین ابزار است؛ نباید به تنظیم Staging/Production منتقل شود. runtime آزمایش منتشرشده با `Encrypt=True;TrustServerCertificate=False` به LocalDB متصل شد؛ این نتیجه همچنان certificate/topology واقعی SQL Server هدف را اثبات نمی‌کند.

artifact و root ذخیره‌سازی آزمایش در دو پوشه مستقل TEMP با prefixهای `MakanApp_Gap02_publish_` و `MakanApp_Gap02_storage_` حفظ شدند. پاک‌سازی این منابع در این مرحله اجرا نشده است؛ DB یا فایل موجود کاربر حذف نشد.

## ۶. روش اجرای امن روی staging آماده

پیش‌شرط‌ها:

۱. origin HTTPS معتبر و قابل دسترسی، بدون bypass certificate؛ domain/certificate و binding با مسئول سرور است.

۲. artifact تمیز از commit تأییدشده، .NET 9 Hosting Bundle، ANCM V2، WebSocket IIS و storage پایدار خارج publish با ACL درست.

۳. تنظیمات فقط در environment ویندوز/IIS یا secret mechanism مصوب؛ هیچ مقدار واقعی در JSON مخزن، script، launchSettings، image یا گزارش نوشته نشود:

```text
ASPNETCORE_ENVIRONMENT=Staging
ConnectionStrings__MakanDatabase
Identity__SecurityKey
Storage__RootPath
AllowedHosts
Cors__AllowedOrigins__0
ReverseProxy__Enabled=true
ReverseProxy__KnownProxies__0
Hosting__RequireHttps=true
Swagger__Enabled=false
Sms__Provider=MeliPayamakOtp
Sms__MeliPayamakOtp__BaseUrl=https://console.melipayamak.com
Sms__MeliPayamakOtp__ServiceToken
Sms__MeliPayamakOtp__TimeoutSeconds=10
```

Secretها باید بیرون Git مقداردهی شوند؛ HMAC حداقل ۳۲ بایت تصادفی و بین restartها پایدار باشد. SQL هدف TLS معتبر و principal جدا برای runtime/migration لازم دارد. `Development` providerها در Staging مجاز نیستند. آماده‌سازی OTP، provider اعلان SMS/Push/MFA/Payment/Child Safety را فعال نمی‌کند؛ آن‌ها خارج GAP-02 هستند.

۴. تمام migrationها با principal مجاز و دستور EF موجود در `SQL_SERVER_SECURITY.md` اجرا و history بررسی شوند؛ fallback factory به DB عادی پذیرفته نیست.

۵. حساب تست از قبل، پروفایل و عضویت/نقش سازمانی مجاز داشته باشد؛ ابزار نقش/عضویت/پرونده خودکار نمی‌سازد. برای یک تلاش کد اشتباه و سپس صحیح، policy آزمایش باید حداقل دو تلاش تازه را اجازه دهد. provider retry یا SMS دوم خودکار ندارد.

در ترمینال تعاملی اپراتور، شماره از environment خارج Git یا prompt مخفی خوانده می‌شود؛ **شماره، OTP یا token در command line/چت وارد نشوند**. برای اجرای زنده، هر سه تأیید لازم‌اند:

```powershell
$env:MAKAN_STAGING_LIVE_OTP = 'YES'
& .\tools\production\staging-core-smoke.ps1 `
  -BaseUrl 'https://staging.example.test' `
  -ApplicationCommit '<40_HEX_DEPLOYED_COMMIT>' `
  -ConfirmIsolatedStaging -LiveOtp `
  -MembershipId '<EXISTING_MEMBERSHIP_GUID>' `
  -OrganizationId '<EXISTING_ORGANIZATION_GUID>' -Role Manager `
  -ForeignOrganizationPersonId '<KNOWN_OTHER_ORGANIZATION_PERSON_GUID>' `
  -CheckStorage -LogPath '<APPROVED_LOG_WINDOW_FILE>'
```

همه placeholderها خارج Git با داده محیط جایگزین شوند. متغیر اختیاری شماره `MAKAN_STAGING_TEST_PHONE` است؛ در نبود آن prompt مخفی نمایش داده می‌شود. ServiceToken فقط روی Backend لازم است؛ ابزار client آن را به API نمی‌فرستد. اپراتور دریافت SMS همان challenge را با YES تأیید و کد را مخفی وارد می‌کند.

ابزار HTTP redirect را دنبال نمی‌کند، خطای خام/body/credential را چاپ نمی‌کند و HTTP غیرمنتظره یا مرحله اجرا‌نشده را موفق اعلام نمی‌کند. challenge باید 202، یک wrong OTP باید 400/OTP_INVALID و verify واقعی باید 200 باشد. سپس profile/workspaces، انتخاب عضویت موجود، read کلاس، cross-org 404، دو اتصال WebSocket و در صورت تأیید، upload/download/delete **فایل آزمایشی خودش** بررسی می‌شوند. metadata فایل حذف‌شده ممکن است طبق lifecycle فعلی باقی بماند.

خروجی JSON فقط status/زمان/مدت عملیات/شماره ماسک‌شده و commit/environment اعلام‌شده اپراتور دارد؛ commit و environment سرور را از راه دور اثبات نمی‌کند. مدت challenge، مدت کل API است نه زمان اختصاصی provider. ابزار یک نشست واقعی ایجاد می‌کند؛ نشست و فایل آزمایشی باید طبق workflow موجود و اختیار اپراتور مدیریت شوند.

`LogPath` باید log مجاز و کامل بازه smoke باشد. بررسی فقط مقادیر حساس شناخته‌شده و patternهای رایج را می‌سنجد، متن match را چاپ نمی‌کند و جای بررسی IIS/reverse-proxy/APM/ترافیک خروجی provider را نمی‌گیرد. transcript/HTTP body logging حساس نباید فعال باشد. browser query-token پشتیبانی نشده و تغییر نکرده است؛ ابزار WebSocket از Authorization header .NET استفاده می‌کند.

## ۷. تست آفلاین ابزارها

```powershell
& .\tools\production\test-staging-tooling.ps1
```

اجرا شد: PASS. syntax هر دو ابزار، نبود opt-in، URL غیرHTTPS/دارای query/credential/path، نبود تأیید migration و helper HTTP با HttpMessageHandler ساختگی آزمایش شدند؛ پاسخ 202، header احراز هویت، خطای مورد انتظار و عدم افشای body خطا بررسی شد. هیچ شبکه/SMS اجرا نمی‌شود. فایل‌های PowerShell برای پشتیبانی متن فارسی در Windows PowerShell 5.1 دارای UTF-8 BOM هستند.

## ۸. Validation و Git

restore/build/test پس از اصلاح: PASS؛ صفر warning/error. Unit=384، Integration=623، Architecture=14؛ جمع **1021 passed، صفر failed/skipped**. نسبت به baseline، ۱۶ case برای اعتبارسنجی Staging و بسته‌بودن Swagger اضافه شد. تست‌های عادی از recording/fake provider موجود استفاده می‌کنند؛ تست آفلاین PowerShell جداگانه است و به تعداد xUnit اضافه شمرده نمی‌شود.

final build/test، scope commit، SHA-256 فایل‌های قبلی و push/remote در گزارش پایان مرحله ثبت می‌شوند. commit آماده‌سازی، GAP-02 را از نظر عملیاتی نمی‌بندد.

## ۹. اقدام بعد و معیار closure

Blockerهای دقیق: HTTPS معتبر نداریم؛ readiness هدف 404 است؛ commit/environment و migration هدف اثبات نشده‌اند؛ دریافت/verify واقعی SMS و log window اجرا نشده‌اند. دسترسی server-side برای اصلاح binding/deployment ارائه نشده و هیچ تغییر remote انجام نشده است.

ادامه همین GAP-02: مسئول سرور domain/TLS، نسخه deployment و readiness را آماده کند؛ سپس smoke تعاملی با داده و secretهای خارج Git، login/org read/SignalR/storage و بررسی log اجرا و این سند با شواهد واقعی به‌روزرسانی شود. تا آن زمان GAP-02 باز است، Pilot/Production همچنان NOT_READY هستند و GAP-03 یا AI خودکار شروع نمی‌شوند.
