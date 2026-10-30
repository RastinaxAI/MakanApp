# استقرار MakanApp روی IIS

## مبنای جاری Realtime، اشتراک و پیوست پاسخ — ۲۰۲۶-۱۰-۳۰

مبنای کد `main@e11d4ec`، 365 عملیات HTTP، 608 schema، 113 جدول و55 migration است؛ آخرین `20261030060025_AddExamAnswerAttachments`. سه migration پس از CompleteSchoolManagement به‌ترتیب AddCommercialOwnershipPeriodsAndPersonalClasses، BindReportExportsToCommercialReservations و AddExamAnswerAttachments هستند. اولی مالکیت/دوره/رزرو و PersonalClass، دومی اتصال Export به رزرو و سومی policy سؤال و AnswerRevisionAttachments را می‌افزاید. از SQL export تازه و history واقعی مقصد استفاده کنید؛ بستهٔ قدیمی خودکار schema/binary جدید ندارد. در این بازبینی Publish/Deploy یا migration سرور اجرا نشده است.

Data Protection استاندارد host اکنون برای cursor حساب در RealtimeCursorProtector مصرف می‌شود. تداوم cursor پس از restart/redeploy به persistence و دسترسی درست key ring وابسته است؛ Identity:SecurityKey جای آن را نمی‌گیرد. در IIS پروفایل/هویت App Pool و مسیر امن کلیدها، و در Linux/Docker مکان پایدار خارج از filesystem موقت container بررسی شوند. کد AddDataProtection پیش‌فرض دارد و setting خصوصی تازه‌ای برای محل ring یا ApplicationName اضافه نشده است؛ متغیر ساختگی Security:DataProtection نسازید. از دست‌رفتن کلید یا context/epoch ناسازگار،409 REALTIME_RESYNC_REQUIRED و Snapshot تازه می‌خواهد؛ readiness این تداوم را probe نمی‌کند. این مصرف جدید، بازگشت MFA/Step-Up نیست. چند replica/backplane و اشتراک کلیدها هنوز gate مستقل‌اند. [Runbook Realtime](../operations/REALTIME_RUNBOOK_FA.md).

Outbox عمومی اکنون invalidation را پس از Commit و بیرون transaction ارسال می‌کند؛ crash می‌تواند همان EventId را تکرار کند. replay به Outboxهای موجود تکیه دارد؛ processed بودن مجوز purge نیست. bootstrap پیش از Snapshot، قطع/وصل و replay همان cursor، ContextRevision، صفحهٔ خالی با HasMore=true و RetryAfterMilliseconds و خطای503 با حفظ cursor در smoke هدف بررسی شوند. cursor/credential/payload در log نباشند.

کنترل تجاری Legacy خودکار فعال نمی‌شود؛ ثبت قرارداد معتبر Admin نقطهٔ enforcement است. دورهٔ ماهانه، Grace هفت‌روزهٔ سازمان، رزرو ReportExport و تفاوت meter unavailable/Unlimited در smoke بررسی شوند؛ بدون درخواست مستقل قرارداد واقعی نسازید. قیمت/سقف پولی یا gateway واقعی و AI runtime با این اسناد فعال نمی‌شوند.

DB و Storage پیوست آزمون، تکلیف، Lesson و Photo با هم backup/restore شوند. AnswerRevision تاریخی و فایل Retained با Detach حذف فیزیکی نمی‌شوند؛ Parent/Guardian و Teacher لغوشده grant raw attachment ندارند. BE-01 رسانهٔ Messenger همچنان Product-blocked است. [پیوست آموزشی](../product/ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md) و [اشتراک](../product/SUBSCRIPTION_FRONTEND_HANDOFF_FA.md).

بازبینی هماهنگی `2026-10-30` / `main@e11d4ec`: آخرین schema مخزن `20261030060025_AddExamAnswerAttachments`،۵۵ migration است؛ Profile/School، Commerce، Realtime و پیوست پاسخ آزمون به binary/schema سازگار نیاز دارند. بسته‌های قبلی خودبه‌خود این تغییرات را ندارند. MFA و ring اختصاصی آن حذف، CORS تابع AllowAnyOrigin و OTP Test=false/true، Production=true/false است. [استقرار نسخهٔ جاری](../deployment-windows-linux-docker.md) و [مدرسه](../product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md). پذیرش سرور یا اجرای migration با این بازبینی انجام نشده است.

> مبنای بازبینی جاری `2026-10-23` / `main@5885d11` است. برای انتخاب و حفظ کانفیگ دو بسته از [Publish Guide](PUBLISH_GUIDE_FA.md) و برای نتیجهٔ محلی از [Release Readiness](BACKEND_RELEASE_READINESS_FA.md) استفاده کنید. Production OTP=true/false و Test=false/true؛ schema یکسان به معنی secret/مقدار یکسان نیست. health و worker و ring این runbook به [شاخص وضعیت جاری](../CURRENT_PROJECT_STATUS.md) ارجاع دارند.

بازبینی `2026-10-23` از `main@5885d11`؛ شمارش و gateهای جاری در [وضعیت پروژه](../CURRENT_PROJECT_STATUS.md) است. این متن اثبات استقرار روی سرور نیست.

## پیش‌نیاز

- Windows Server پشتیبانی‌شده، IIS و .NET 9 Hosting Bundle نصب و پس از نصب IIS restart شده باشد.
- WebSocket Protocol برای SignalR فعال باشد.
- App Pool با `No Managed Code` و identity اختصاصی اجرا شود.
- مسیر publish و مسیر persistent storage جدا باشند؛ App Pool فقط روی storage و محل log عملیاتی مجوز لازم را داشته باشد.
- SQL Server از اینترنت عمومی قابل دسترسی نباشد.
- در IIS 10، `requestFiltering removeServerHeader="true"` و حذف `X-Powered-By` را در تنظیمات سایت اعمال کنید؛ Kestrel نیز `AddServerHeader=false` دارد. این کار کاهش disclosure است، نه ادعای جلوگیری کامل از fingerprinting.

## تنظیمات محیط

برای سرور صرفاً آزمایشی، پروفایل مستقل `TestIis` با محیط `Test` و [راهنمای Mock OTP](../dev/SERVER_MOCK_OTP_FA.md) اضافه شده است؛ این پروفایل جایگزین انتشار Production در ادامهٔ این سند نیست.

انتشار عادی در این سند `ASPNETCORE_ENVIRONMENT=Production` دارد؛ فایل `appsettings.Staging.json` به درخواست مالک حذف شده است. نسخهٔ قابل‌اشتراک کانفیگ در `MakanApp.Api/appsettings.Production.json.example` است. فایل واقعی `MakanApp.Api/appsettings.Production.json` محلی و در `.gitignore` است؛ حذف آن از index گیت به معنی حذف فایل فیزیکی یا داده‌های سرور نیست. روی clone جدید، نمونه را کپی و مقادیر محرمانه را فقط روی محیط مجاز وارد کنید. فایل نمونه به‌تنهایی آمادهٔ اجرا نیست.

مقادیر می‌توانند در فایل خصوصی بالا یا از IIS Configuration Editor، environmentVariables یا secret store تأمین شوند. environment متغیرهای هم‌نام بر JSON اولویت دارند؛ مقادیر قدیمی IIS ممکن است کانفیگ فایل جدید را override کنند. هیچ رمز، token یا connection string واقعی در Git یا مستند مشترک ثبت نشود.

```text
ConnectionStrings__MakanDatabase
Identity__SecurityKey
Storage__RootPath
AllowedHosts
Cors__AllowAnyOrigin
Cors__AllowedOrigins__0
ReverseProxy__Enabled=true
ReverseProxy__KnownProxies__0=127.0.0.1
Hosting__RequireHttps=true
Hosting__AllowInsecureHttp=false
Database__AllowUntrustedServerCertificate=false
Swagger__Enabled=false
Sms__Provider
Sms__MeliPayamakOtp__ServiceToken          # فقط اگر MeliPayamakOtp انتخاب شده باشد
Payments__Provider=Unavailable             # تا زمان adapter تأییدشده
Messaging__ContentSafetyProvider=Internal
BackofficeSecurity__KeyRingPath=C:\ProgramData\MakanApp\BackofficeMfaKeys
Notifications__PushProvider=Unavailable
Notifications__SmsProvider=Unavailable
```

`Identity__SecurityKey` حداقل ۳۲ بایت تصادفی و پایدار بین restartها باشد. rotation آن بدون برنامه، OTPهای ذخیره‌شده و token hashهای جدید را مختل می‌کند. حالت امن پیش‌فرض SQL، `Encrypt=True;TrustServerCertificate=False` و certificate دارای زنجیرهٔ قابل‌اعتماد است.

### انتشار HTTP با استثنای صریح مالک

برای انتشار فعلی HTTP-only، مالک دو استثنای جداگانه را پس از اعلام خطر تأیید کرده است. تنها فایل خصوصی Production این مقادیر را فعال می‌کند:

```json
{
  "Hosting": { "RequireHttps": false, "AllowInsecureHttp": true },
  "Database": { "AllowUntrustedServerCertificate": true }
}
```

`Hosting:AllowInsecureHttp` اجازه می‌دهد redirect غیرفعال و origin صریح HTTP استفاده شود. روی HTTP، OTP، پیام‌ها و توکن‌های ورود در شبکه رمزگذاری نمی‌شوند؛ این یک استثنای پذیرفته‌شده است، نه محیط امن یا اثبات Release Readiness. `Database:AllowUntrustedServerCertificate` فقط پذیرش `TrustServerCertificate=True` را مجاز می‌کند؛ `Encrypt=False` همچنان رد می‌شود و هویت گواهی SQL تأیید نخواهد شد. دو flag مستقل‌اند، در Staging اثر ندارند و با حذف/false شدن به کنترل سخت‌گیرانه بازمی‌گردند. startup فقط هشدارهای ثابت و بدون secret ثبت می‌کند.

طبق تصمیم صریح مالک در ۲۰۲۶-۱۰-۲۷، فایل‌های Test و Production با `Cors:AllowAnyOrigin=true`، `Cors:AllowedOrigins=[]` و `AllowedHosts="*"` تنظیم می‌شوند. تصمیم CORS به نام Environment وابسته نیست؛ برای محدودسازی، فلگ را false و Originهای دقیق را وارد کنید. CORS باز، header و method دلخواه را مجاز می‌کند ولی cookie credentials فعال نمی‌شود. کنترل‌های proxy، SQL، storage، Identity، Authentication و Authorization حفظ می‌شوند؛ اعتماد عمومی به proxy اضافه نشده است. متغیرهای محیطی و IIS همچنان می‌توانند JSON را override کنند و وضعیت واقعی سرور در این تغییر بررسی نشده است. [راهنمای CORS و تقدم تنظیمات](CORS_CONFIGURATION_FA.md) را بخوانید.

پس از آماده شدن HTTPS، `RequireHttps=true` و `AllowInsecureHttp=false` و originهای HTTPS تنظیم شوند. پس از نصب گواهی معتبر SQL نیز `TrustServerCertificate=False` و `AllowUntrustedServerCertificate=false` شوند. استفاده از حساب SQL پرقدرت موجود صرفاً بازیابی کانفیگ درخواست‌شده است؛ جایگزینی آن با runtime principal کم‌اختیار هنوز وظیفهٔ اپراتور است، نه کاری که این تغییر روی سرور انجام دهد.

Web SDK فایل خصوصی `appsettings.Production.json` موجود را همراه Publish کپی می‌کند. artifact محلی حاوی این فایل محرمانه است و نباید در Git، لینک عمومی یا log قرار گیرد. پیش از انتقال امن، وجود فایل واقعی و نبود فایل Staging در خروجی تازه را بررسی کنید؛ محتویات secret را چاپ نکنید. نمونهٔ `.json.example` جایگزین فایل واقعی نیست. با انتشار در پوشهٔ تازه، از باقی ماندن Staging قدیمی جلوگیری می‌شود؛ روی سرور حذف/تغییری خودکار انجام نشده است.

`MakanApp.Api/web.config` تنظیم Production و مسیر پیش‌فرض بالا را همراه Publish مخصوص IIS منتقل می‌کند؛ Linux/Docker این فایل IIS را مصرف نمی‌کنند و همچنان مسیر volume و محافظ معتبر خود را از environment می‌گیرند. `Messaging:ContentSafetyProvider=Internal` در فایل خصوصی و نمونه موجود است و کلید provider خارجی لازم ندارد. مقادیر SMS، اتصال SQL و Secretهای فعلی سرور را هنگام کپی فایل‌ها حفظ کنید؛ فایل نمونه جایگزین کامل کانفیگ اختصاصی سرور نیست. کد adapter ملی‌پیامک و منطق OTP در این تغییر دست نخورده‌اند.

### حذف وابستگی MFA از Host — ۲۰۲۶-۱۰-۲۴

کد فعال دیگر Authenticator/TOTP، Recovery و Step-Up ندارد. متغیر `BackofficeSecurity__KeyRingPath` از `web.config` حذف شده و Startup/ready نیازمند key ring قدیمی نیستند.
پوشه و کلیدهای قدیمی سرور فعلاً حفظ شوند؛ این تحویل هیچ cleanup یا Deploy سروری انجام نمی‌دهد. OTP/Session و مجوزهای واقعی Platform بدون تغییر باقی‌اند.
ورود Owner: OTP → Session → `GET /api/admin/v1/me`؛ پاسخ 200 فقط با Platform permission معتبر است. چهار نقش مدرسه به‌تنهایی 403 می‌گیرند.

## فایل SQL مایگریشن‌ها

Migration جدید `20261024184613_RemovePlatformMfa` برای حذف دو جدول MFA و دو ستون Session تولید شده است. اجرای آن روی سرور یا دیتابیس اصلی مجاز/انجام‌شده نیست؛ فقط تست خودکار ایزوله با تأیید مالک آن را اعمال می‌کند. Migrationهای قبلی از جمله `20261015073418_AddPlatformTotpMfa` حفظ شده‌اند. خروجی کامل برای بازبینی:

```powershell
& ./tools/production/export-migration-sql.ps1 -OutputPath .artifacts/deployment/migrations-idempotent-safe.sql
```

خروجی خام `dotnet ef migrations script --idempotent` در Migrationهای دارای SQL backfill ممکن است با خطای 207 روبه‌رو شود: SQL Server پیش از اجرای ADD COLUMN، ارجاع UPDATE به ستون تازه را در همان batch کامپایل می‌کند. فایل قدیمی `migrations-idempotent.sql` تحویلی این مشکل را داشت و نباید استفاده شود. ابزار بالا شرط‌های history و SQL هر عملیات را حفظ می‌کند، ولی کامپایل عملیات را با EXEC تا زمان اجرا عقب می‌اندازد. قالب EF ناشناخته یا فهرست Migration ناسازگار رد می‌شود؛ Migrationهای C# یا مدل تغییر نمی‌کنند.

فایل نهایی یک batch بدون نیاز به SQLCMD mode است؛ آن را کامل در پنجره/اتصال تازه SSMS اجرا کنید. اگر `@@TRANCOUNT` یا `XACT_STATE()` صفر نباشند، پیش از تغییر دیتابیس متوقف می‌شود و تراکنش قبلی را rollback نمی‌کند. `XACT_ABORT` و TRY/CATCH، خطای اجرای تراکنش همین فایل را rollback و ادامه را متوقف می‌کنند. فایل خروجی موجود overwrite نمی‌شود؛ برای تولید مجدد، نام جدید انتخاب کنید. خروجی خام EF فقط برای بازبینی نگه‌داری می‌شود، نه اجرا روی سرور.

تولید فایل SQL به معنی اجرای آن نیست. قبل از اجرا، اپراتور باید دیتابیس انتخاب‌شده، Backup و `__EFMigrationsHistory` را بررسی و تغییرات pending را بازبینی کند؛ idempotent فقط به history ثبت‌شده متکی است و اختلاف schema دستی را اصلاح نمی‌کند. اجرا با migration principal جدا انجام شود؛ این دستور هیچ `database update` یا startup migration ندارد.

پس از build پروژه، آزمون واقعی فایل SQL را می‌توان جداگانه اجرا کرد:

```powershell
& ./tools/production/test-migration-sql.ps1 -ConfirmLocalDisposableDatabase
```

این تست فقط روی `(localdb)\MSSQLLocalDB` و دیتابیس‌های تازه با نام `MakanApp_SqlScript_<guid>` کار می‌کند؛ Connection String برنامه را مصرف نمی‌کند. نصب خالی، اجرای مجدد، بازتولید خطای خروجی قدیمی، ارتقا از `20261007075650_AddExamGradingAndRelease` با پیام/اعضای موجود، حفظ داده در retry، rollback خطای عمدی و رد تراکنش قبلی بررسی می‌شوند. دیتابیس‌های ایزوله برای بررسی باقی می‌مانند و هیچ DROP DATABASE انجام نمی‌شود. موفقیت `dotnet test` به‌تنهایی جای این اجرای فایل SQL تحویلی را نمی‌گیرد.

## ترتیب استقرار

1. artifact امضاشده/تأییدشده و commit مورد انتظار را مشخص کنید.
2. backup دیتابیس و storage بگیرید و قابل‌خواندن بودن backup را بررسی کنید.
3. سایت را drain/stop کنید اگر migration با نسخه‌ی قبلی backward-compatible نیست.
4. migration را با migration principal اجرا کنید؛ application در startup migration اجرا نمی‌کند.
5. فایل‌های publish را side-by-side قرار دهید و binding را به نسخه‌ی جدید تغییر دهید.
6. identity App Pool را روی `Storage__RootPath` دارای Modify کنید؛ روی source/secrets حداقل دسترسی را نگه دارید.
7. در حالت امن TLS binding، redirect و HSTS را بررسی کنید؛ در انتشار HTTP تأییدشده، خطر و فعال بودن دو استثنای بالا را ثبت کنید و آماده‌بودن امن را ادعا نکنید. firewall همچنان لازم است.
8. اپراتور `/health/live` و `/health/ready` را بررسی کند. ابزار `tools/production/smoke-test.ps1` فقط HTTPS می‌پذیرد و برای این انتشار HTTP تغییر داده نشده است؛ اجرای آن یا smoke زندهٔ OTP در این مرحله ادعا نمی‌شود.
9. Event Log/log collector، 5xx، latency و worker backlog را هنگام warm-up مشاهده کنید.

## Forwarded headers

برنامه فقط وقتی `ReverseProxy:Enabled=true` است forwarded headers را می‌خواند، `ForwardLimit=1` دارد و فقط IPهای `KnownProxies` را قبول می‌کند. IP proxy واقعی را صریح ثبت کنید؛ subnet عمومی یا اعتماد سراسری ممنوع است.

## قرارداد Runtime و Shutdown در PH-5

این بخش دستور اپراتور است؛ هیچ فرمان IIS، restart یا reboot در تحویل PH روی سرور اجرا نشده است.
Backend همچنان .NET 9 و `inprocess` است؛ نصب Hosting Bundle متناظر و bitness سازگار باید روی مقصد تأیید شود.
هر برنامه App Pool اختصاصی داشته باشد. `maxProcesses=1` الزام topology فعلی SignalR، Presence و limiter حافظه‌ای است؛
leaseهای SQL مجوز Web Garden یا scale-out عمومی نیستند.

`Hosting:ShutdownTimeoutSeconds` اختیاری و بین ۵ تا ۹۰ ثانیه است. نبود آن، مقدار نهایی Generic Host را حفظ می‌کند
(پیش‌فرض framework برابر ۳۰ ثانیه و override قدیمی `shutdownTimeoutSeconds` همچنان معتبر است).
مقدار صریح جدید اولویت دارد و مقدار نهایی خارج بازه رد می‌شود؛ مقدار خام نامعتبر وارد پیام خطا نمی‌شود.
در JSON مشترک default جدیدی اضافه نشده تا override قبلی بی‌خبر پوشانده نشود.

سه budget مستقل را اشتباه نگیرید:

| مرز | قرارداد این نسخه | وظیفهٔ اپراتور |
|---|---|---|
| Generic Host | مقدار معتبر ۵ تا ۹۰، پیش‌فرض ۳۰ ثانیه | مشاهدهٔ shutdown واقعی workerهای همین نسخه |
| ANCM | `shutdownTimeLimit="120"` در `web.config` | حفظ این attribute هنگام ترکیب کانفیگ خصوصی؛ عمدتاً مسیر `app_offline.htm` |
| WAS/App Pool | تنظیم سرور، نه attribute برنامه | بررسی budget مستقل؛ پیشنهاد عملیاتیِ قابل بازبینی ۱۵۰ ثانیه، نه مقدار پیش‌فرض ۹۰ |

این‌ها بودجه‌های مستقل‌اند، نه SLA یا تضمین خاتمهٔ I/O معیوب. Host بودجهٔ لغو/انتظار cooperative دارد،
نه timer کشتن اجباری process؛ termination توسط ANCM/WAS تابع مسیر مستقل خودشان است. نتیجه‌های کوتاه worker بودجهٔ مستقل ۵s دارند؛
lease منقضی کار نیمه‌تمام را recover می‌کند. SignalR هنگام recycle قطع و reconnect/Delta از SQL لازم می‌شود.
برای جزئیات و محدودیت at-least-once به [بازیابی Worker](../operations/WORKER_RELIABILITY_AND_RECOVERY.md) رجوع کنید.

### بررسی و تنظیم محدود App Pool توسط اپراتور

نمونهٔ زیر برای **Windows PowerShell 5.1 Desktop، 64-bit و مدیریتی** روی Windows Server است؛
اجرای مستقیم در `pwsh` با compatibility module، پذیرش provider `IIS:` را ثابت نمی‌کند.
نام‌های نمونه را با سایت/App Pool تأییدشده عوض کنید؛ Pool نباید میزبان برنامهٔ دیگری باشد و اپراتور
باید این اختصاصی‌بودن را در inventory/IIS Manager پیش از mutation تأیید کند.
مرحلهٔ اول فقط بررسی است؛ backup شامل تنظیمات محرمانهٔ IIS است و نباید در Git یا evidence عمومی قرار گیرد.

```powershell
if ($PSVersionTable.PSEdition -ne 'Desktop' -or $PSVersionTable.PSVersion -lt [Version]'5.1' -or
    ![Environment]::Is64BitProcess) {
    throw 'این نمونه به Windows PowerShell 5.1 Desktop مدیریتی 64-bit نیاز دارد.'
}
Import-Module ServerManager
Import-Module WebAdministration
Get-WindowsFeature Web-Server, Web-AppInit, Web-WebSockets, Web-Scripting-Tools |
    Select-Object Name, InstallState
dotnet --list-runtimes
Get-WebGlobalModule -Name AspNetCoreModuleV2 | Select-Object Name

$siteName = 'MakanApp'
$poolName = 'MakanApp'
if ($siteName -notmatch '^[A-Za-z0-9_.-]+$' -or $poolName -notmatch '^[A-Za-z0-9_.-]+$') {
    throw 'نام نمونه را با نام دقیق و معتبر محیط جایگزین کنید.'
}
$poolPath = "IIS:\AppPools\$poolName"
$sitePath = "IIS:\Sites\$siteName"
if (!(Test-Path -LiteralPath $poolPath) -or !(Test-Path -LiteralPath $sitePath)) {
    throw 'سایت یا App Pool تأییدشده یافت نشد؛ برنامه یا سایت تازه نسازید.'
}
$actualPool = (Get-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' `
    -Filter "system.applicationHost/sites/site[@name='$siteName']/application[@path='/']" `
    -Name applicationPool).Value
if ($actualPool -ne $poolName) {
    throw 'سایت ریشه از App Pool انتخاب‌شده استفاده نمی‌کند؛ پیش از تغییر mapping را بررسی کنید.'
}
Get-Item -LiteralPath $poolPath |
    Select-Object Name, managedRuntimeVersion, startMode,
        @{Name='MaxProcesses';Expression={$_.processModel.maxProcesses}},
        @{Name='IdleTimeout';Expression={$_.processModel.idleTimeout}},
        @{Name='LoadUserProfile';Expression={$_.processModel.loadUserProfile}},
        @{Name='ShutdownLimit';Expression={$_.processModel.shutdownTimeLimit}},
        @{Name='PingEnabled';Expression={$_.processModel.pingingEnabled}},
        @{Name='RapidFailProtection';Expression={$_.failure.rapidFailProtection}}
Backup-WebConfiguration -Name ('MakanApp-PH-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
```

پس از بازبینی backup، مالکیت سایت/Pool و maintenance window، تنظیم‌های محدود زیر را اعمال کنید؛
هویت Windows را تغییر ندهید، چون DPAPI موجود به همان identity/profile وابسته است.

```powershell
Set-ItemProperty -LiteralPath $poolPath -Name managedRuntimeVersion -Value ''
Set-ItemProperty -LiteralPath $poolPath -Name startMode -Value AlwaysRunning
Set-ItemProperty -LiteralPath $poolPath -Name processModel.maxProcesses -Value 1
Set-ItemProperty -LiteralPath $poolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty -LiteralPath $poolPath -Name processModel.shutdownTimeLimit -Value ([TimeSpan]::FromSeconds(150))
```

`LoadUserProfile` در بلوک بالا عمداً تغییر نمی‌کند. برای ring موجودِ DPAPI، همان identity و
profile مؤثر قبلی حفظ شود؛ true کردن این flag به‌تنهایی روش بازیابی یا ارتقای امن نیست.
روی App Pool تازه، تنظیم مناسب profile باید همراه protector/ACL/custody و آزمایش decrypt همان
payload قبلی تعیین شود. تغییر profile موجود فقط پس از recovery rehearsal مستقل و تأیید maintenance مجاز است.

Idle Timeout صفر برای workerهای داخل API است؛ Ping، Rapid-Fail Protection، محدودیت crash و مانیتورینگ را خاموش نکنید.
recycle برنامه‌ریزی‌شده را با بار و maintenance هماهنگ کنید، نه اینکه recovery را با حذف همهٔ safetyها جایگزین کنید.
overlap موقت دو process در recycle با Web Garden دائمی متفاوت است؛ Redis/backplane در این مرحله ایجاد نشده است.

### Warm-up فقط با نقش نصب‌شده

اگر `Web-AppInit` نصب و سایت ریشهٔ Backend تأیید شده است، اپراتور می‌تواند preload را فعال کند.
وجود section در `web.config` بدون نصب/آزاد بودن نقش می‌تواند 500.19 بدهد؛ بنابراین این section به artifact اضافه نشده است.

```powershell
if ((Get-WindowsFeature Web-AppInit).InstallState -ne 'Installed') {
    throw 'ابتدا نقش Application Initialization باید توسط اپراتور آماده شود.'
}
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' `
    -Filter "system.applicationHost/sites/site[@name='$siteName']/application[@path='/']" `
    -Name preloadEnabled -Value $true
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $siteName `
    -Filter 'system.webServer/applicationInitialization' -Name doAppInitAfterRestart -Value $true
$warmup = Get-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $siteName `
    -Filter "system.webServer/applicationInitialization/add[@initializationPage='/health/live']"
if (!$warmup) {
    Add-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $siteName `
        -Filter 'system.webServer/applicationInitialization' -Name '.' `
        -Value @{initializationPage='/health/live'}
}
```

warm-up liveness جای readiness، دسترسی SQL یا شروع موفق provider را نمی‌گیرد.
AlwaysRunning بدون preload/درخواست اولیه لزوماً startup برنامه را ثابت نمی‌کند.
اگر سایت چند application دارد یا section قفل است، command را کورکورانه ادامه ندهید؛ تنظیم فقط application تأییدشده لازم است.

### ACL، logging و پذیرش Runtime

publish فقط Read/Execute، Storage مجوز لازمِ محدود و محل log فقط در صورت استفاده مجوز write بخواهد؛ Secretهای فعال نیز فقط برای identity مجاز قابل خواندن باشند.
دسترسی broad مانند Everyone یا تغییر identity موجود مجاز نیست. ACL و custody قبلی پیش از هر تغییر ثبت شوند؛
key ring قدیمی MFA دیگر پیش‌نیاز Host نیست و در این مرحله حذف نمی‌شود. stdout و ANCM debug logging دائمی خاموش بمانند؛
Event Log و collector چرخش/سقف حجم/دسترسی مصوب می‌خواهند. IIS access log باید query، cookie، Authorization و custom header حساس را ثبت نکند؛
Failed Request Tracing یا dump خام کانفیگ روش evidence عمومی نیست.

برای اولین rollout اصلاح Report Export، پیش از راه‌اندازی نسخهٔ جدید، نسخهٔ legacy را drain/stop و خروج PIDهای قبلی را تأیید کنید؛
آزمون worker جدید/جدید، هم‌زمانی نسخهٔ قدیمیِ بدون fence را اثبات نمی‌کند. backup فعلی publish/config/DB/storage/ring حفظ شود.
Start، Stop/Start، recycle، rapid restart، overlap و reboot فقط با تأیید maintenance و معیار بازیابی در
[چک‌لیست پذیرش](RELEASE_CHECKLIST.md) اجرا شوند؛ موفقیت تست محلی جای این gateها نیست.

TLS معتبر همچنان `BACKEND_HTTPS=PENDING_INFRASTRUCTURE` است؛ استثنای HTTP قبلی، CORS allowlist و
Swagger opt-in حفظ شده‌اند. فعال‌بودن Swagger خصوصیِ انتشار قبلی نه حذف و نه از سرور دوباره احراز شده است.
headerهای API موجود (`nosniff`، `no-referrer` و Permissions-Policy) و استثنای Swagger در CSP حفظ می‌شوند؛
هیچ cookie احراز هویت یا CSP ویژهٔ frontend اضافه نشده است.

مراجع رسمی تنظیم‌های IIS: [ANCM](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/aspnet-core-module?view=aspnetcore-9.0)،
[Process Model](https://learn.microsoft.com/en-us/iis/configuration/system.applicationhost/applicationpools/add/processmodel/)،
[Application Initialization](https://learn.microsoft.com/en-us/iis/configuration/system.webserver/applicationinitialization/).

## Gate شکست و rollback

استقرار شکست‌خورده است اگر startup fail-fast شود، readiness سبز نشود، migration ناقص باشد، smoke test خطا دهد، 5xx/latency غیرعادی شود یا worker backlog رشد پایدار داشته باشد. در این حالت traffic را متوقف و artifact قبلی را برگردانید. downgrade migration را کورکورانه اجرا نکنید؛ بسته به compatibility از forward-fix یا restore تأییدشده استفاده کنید و trace ID/logهای بازه را نگه دارید.

## smoke زنده GAP-02

شواهد و دستور ابزارهای opt-in در [گزارش GAP-02](../release/GAP_02_STAGING_CORE_SMOKE_VALIDATION.md) آمده‌اند. ابزار قدیمی `smoke-test.ps1` همچنان OTP زنده اجرا نمی‌کند؛ ابزار جدا `staging-core-smoke.ps1` به HTTPS معتبر، تأیید صریح و ورود مخفی OTP توسط اپراتور نیاز دارد. normal `dotnet test` هرگز SMS واقعی نمی‌فرستد.

publish بررسی‌شده Web SDK، `web.config` را با `AspNetCoreModuleV2`، `processPath=dotnet`، `hostingModel=inprocess` و `stdoutLogEnabled=false` تولید می‌کند؛ وجود artifact اثبات نصب module یا WebSocket روی سرور نیست. برای artifact رسمی از source تمیز commit تأییدشده و secret provisioning کنترل‌شده استفاده کنید، نه worktree دارای تغییرات نامرتبط. artifact بدون secret قابل‌اشتراک است؛ نسخهٔ خصوصی حاوی JSON واقعی فقط برای انتقال امن اپراتور است و عمومی/commit نشود.


## تغییرات عملیاتی Messenger و Push — بازبینی 2026-10-29

مبنای جاری این بخش `2026-10-30` / `main@e11d4ec`؛ ۳۶۵ عملیات،۶۳ Controller،۶۰۸ schema،۱۱۳ جدول و۵۵ migration. آخرین20261030060025_AddExamAnswerAttachments است. schema با delivery provider یا applied migration سرور یکی نیست؛ [وضعیت جاری](../CURRENT_PROJECT_STATUS.md) و Handoffهای P1/P2 مرجع‌اند.

### انتخاب transport اعلان

`Notifications:PushProvider=WebPushAndFcm` provider واقعی Web Push/VAPID و FCM HTTP v1 را ثبت می‌کند. کلیدهای تنظیمات زیر از کد DI/Options هستند؛ مقادیر واقعی فقط در secret/config محیط مجاز قرار بگیرند:

| کلید | مسئولیت |
|---|---|
| `Notifications:WebPush:VapidSubject` | subject معتبر VAPID مطابق validation موجود |
| `Notifications:WebPush:VapidPublicKey` | کلید عمومی EC؛ برای client مجاز قابل مصرف |
| `Notifications:WebPush:VapidPrivateKey` | secret امضای VAPID؛ هرگز در frontend/log/مخزن نباشد |
| `Notifications:WebPush:AllowedEndpointHosts` | allowlist دقیق hostهای HTTPS مقصد subscription؛ بدون آن ثبت Web Push آماده نیست |
| `Notifications:WebPush:TimeToLiveSeconds` | TTL transport با حدود معتبر options |
| `Notifications:Fcm:ProjectId` | پروژه مورد انتظار FCM |
| `Notifications:Fcm:ServiceAccountPath` | مسیر مطلق فایل credential با ACL محدود؛ fallback فقط `GOOGLE_APPLICATION_CREDENTIALS` |
| `Notifications:Fcm:TimeToLiveSeconds` | TTL پیام FCM طبق options |

وجود فایل credential یا معتبر بودن شکل VAPID، اعتبار حساب/provider یا تحویل دستگاه را اثبات نمی‌کند. worker خطای timeout/credential/registration را طبق طبقه‌بندی جاری نگه می‌دارد؛ secret، endpoint کامل subscription، registration token یا body پیام در log منتشر نشود. `Unavailable` موفقیت ساختگی نمی‌دهد؛ Development provider فقط برای Development/Testing است. رجیستری دستگاه به حساب مالک و چرخه logout/switch account وابسته است؛ حساب جدید نباید device فعال حساب قبلی را تصاحب کند.

وضعیت runtime هنوز `P1_PUSH_RUNTIME_PENDING` است. برای بستن این gate باید Web Push و FCM در محیط مجاز با credential و دستگاه واقعی، revoke/expiry، retry/recovery و اثبات دریافت آزمایش شوند. ثبت subscription، Sent، SignalR و provider acceptance جای این شاهد را نمی‌گیرند. SMS عمومی اعلان و gateway پرداخت واقعی همچنان unavailable‌اند؛ SMS ورود ملی‌پیامک مسیر جدا دارد.

Web Push مرورگر به Service Worker و secure context/HTTPS معتبر در محیط واقعی نیاز دارد. استثنای HTTP انتشار قبلی، این شرط یا gate دریافت دستگاه را رفع نمی‌کند. FCM روی iOS نیز پیکربندی بستر/APNs مربوط را می‌خواهد؛ داشتن credential سرور به‌تنهایی پیکربندی client را ثابت نمی‌کند. برای lifecycle دقیق به Handoff P1 رجوع شود.

### worker و چند instance

scheduled-message dispatcher و notification/outbox workerها به migration، ساعت UTC و دسترسی SQL درست نیاز دارند. intent Scheduled را Sent گزارش نکنید؛ ارسال واقعی پس از بازبینی دوباره permission، Block، Child Safety، attachment و topic اتفاق می‌افتد. reboot/recovery و رقابت worker باید در محیط مقصد smoke شوند؛ بازیابی notification از SQL/Delta است.

typing/presence در `InMemoryMessagingEphemeralState` تک‌instance‌اند. پیش‌فرض heartbeat ده ثانیه و lease چهل‌وپنج ثانیه است؛ idle/connection/proxy timeout محیط نباید این قرارداد را بی‌خبر قطع کند. reconnect باید subscription جدید بسازد. تنظیم چند worker دیتابیس به معنی وجود backplane و state مشترک Presence نیست؛ `PENDING_SCALE_INFRA` همچنان برقرار است. هیچ scale-out اثبات‌نشده‌ای از sticky session نتیجه نشود.

### gateهای رسانه و smokeهای لازم

`P3_S_INFRA_REQUIRED`، `LIVEKIT_REJOIN_SECURITY_GATE=NOT_TESTED` و `P3_PRODUCTION_MEDIA_APPROVAL=NOT_GRANTED` وضعیت فعلی‌اند. رسانهٔ صوتی/تصویری/Live محصول هنوز deployment پذیرفته‌شده ندارد. spec مصوب LiveKit/SFU/TURN را با runtime نصب‌شده اشتباه نگیرید؛ جزئیات Linux lab، DNS/TLS، TURN و دو client در [گزارش S](../messaging/MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_REPORT.md) است.

بعد از انتشار Backend، روی حساب/سازمان آزمایشی از پیش مجاز، علاوه بر core قبلی این موارد را بررسی کنید: تأیید self/سن طبق policy، Direct و Peer Read مجاز/Hidden، expiry و reconnect Typing/Presence، registration/revoke Push، تأخیر و cancel/dispatch intent، topic و محدودیت عضو، Poll/Quiz و دو حالت Checklist. این‌ها دستور smoke هستند؛ در این مرحله روی سرور واقعی اجرا نشده‌اند. فایل خصوصی Production، کلیدهای OTP و تنظیمات خصوصی و موارد استثنای HTTP/SQL قبلی بدون تصمیم تازه تغییر نکنند.
