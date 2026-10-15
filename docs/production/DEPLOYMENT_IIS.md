# استقرار MakanApp روی IIS

## پیش‌نیاز

- Windows Server پشتیبانی‌شده، IIS و .NET 9 Hosting Bundle نصب و پس از نصب IIS restart شده باشد.
- WebSocket Protocol برای SignalR فعال باشد.
- App Pool با `No Managed Code` و identity اختصاصی اجرا شود.
- مسیر publish و مسیر persistent storage جدا باشند؛ App Pool فقط روی storage و محل log عملیاتی مجوز لازم را داشته باشد.
- SQL Server از اینترنت عمومی قابل دسترسی نباشد.
- در IIS 10، `requestFiltering removeServerHeader="true"` و حذف `X-Powered-By` را در تنظیمات سایت اعمال کنید؛ Kestrel نیز `AddServerHeader=false` دارد. این کار کاهش disclosure است، نه ادعای جلوگیری کامل از fingerprinting.

## تنظیمات محیط

انتشار فعلی فقط `ASPNETCORE_ENVIRONMENT=Production` دارد؛ فایل `appsettings.Staging.json` به درخواست مالک حذف شده است. نسخهٔ قابل‌اشتراک کانفیگ در `MakanApp.Api/appsettings.Production.json.example` است. فایل واقعی `MakanApp.Api/appsettings.Production.json` محلی و در `.gitignore` است؛ حذف آن از index گیت به معنی حذف فایل فیزیکی یا داده‌های سرور نیست. روی clone جدید، نمونه را کپی و مقادیر محرمانه را فقط روی محیط مجاز وارد کنید. فایل نمونه به‌تنهایی آمادهٔ اجرا نیست.

مقادیر می‌توانند در فایل خصوصی بالا یا از IIS Configuration Editor، environmentVariables یا secret store تأمین شوند. environment متغیرهای هم‌نام بر JSON اولویت دارند؛ مقادیر قدیمی IIS ممکن است کانفیگ فایل جدید را override کنند. هیچ رمز، token یا connection string واقعی در Git یا مستند مشترک ثبت نشود.

```text
ConnectionStrings__MakanDatabase
Identity__SecurityKey
Storage__RootPath
AllowedHosts
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

AllowedHosts، CORS، proxy، مسیر مطلق storage، طول کلید Identity، Authentication و Authorization همچنان اعتبارسنجی می‌شوند. اعتماد سراسری به proxy یا CORS wildcard اضافه نشده است. فایل خصوصی از آخرین آدرس اعلام‌شدهٔ مالک (`http://94.184.32.84`) استفاده می‌کند؛ این آدرس با اتصال به سرور دوباره بررسی نشده است. فقط همان origin در CORS آمده؛ اگر وب‌کلاینت روی origin دیگری است، آدرس دقیق آن باید به allowlist اضافه شود. CORS روی اپ native/Postman محدودیت مرورگر ایجاد نمی‌کند و مجوز کسب‌وکاری نیست.

پس از آماده شدن HTTPS، `RequireHttps=true` و `AllowInsecureHttp=false` و originهای HTTPS تنظیم شوند. پس از نصب گواهی معتبر SQL نیز `TrustServerCertificate=False` و `AllowUntrustedServerCertificate=false` شوند. استفاده از حساب SQL پرقدرت موجود صرفاً بازیابی کانفیگ درخواست‌شده است؛ جایگزینی آن با runtime principal کم‌اختیار هنوز وظیفهٔ اپراتور است، نه کاری که این تغییر روی سرور انجام دهد.

Web SDK فایل خصوصی `appsettings.Production.json` موجود را همراه Publish کپی می‌کند. artifact محلی حاوی این فایل محرمانه است و نباید در Git، لینک عمومی یا log قرار گیرد. پیش از انتقال امن، وجود فایل واقعی و نبود فایل Staging در خروجی تازه را بررسی کنید؛ محتویات secret را چاپ نکنید. نمونهٔ `.json.example` جایگزین فایل واقعی نیست. با انتشار در پوشهٔ تازه، از باقی ماندن Staging قدیمی جلوگیری می‌شود؛ روی سرور حذف/تغییری خودکار انجام نشده است.

`MakanApp.Api/web.config` تنظیم Production و مسیر پیش‌فرض بالا را همراه Publish مخصوص IIS منتقل می‌کند؛ Linux/Docker این فایل IIS را مصرف نمی‌کنند و همچنان مسیر volume و محافظ معتبر خود را از environment می‌گیرند. `Messaging:ContentSafetyProvider=Internal` در فایل خصوصی و نمونه موجود است و کلید provider خارجی لازم ندارد. مقادیر SMS، اتصال SQL و Secretهای فعلی سرور را هنگام کپی فایل‌ها حفظ کنید؛ فایل نمونه جایگزین کامل کانفیگ اختصاصی سرور نیست. کد adapter ملی‌پیامک و منطق OTP در این تغییر دست نخورده‌اند.

پیش از استفاده از MFA، پوشهٔ KeyRing را بیرون Publish و Storage بسازید و فقط به هویت App Pool و اپراتور مجاز دسترسی لازم بدهید. بدون certificate، محافظ Windows از DPAPI استفاده می‌کند؛ هویت App Pool باید ثابت و `Load User Profile` مناسب باشد. اگر MFA قبلاً فعال بوده، همان مسیر و کلیدهای قبلی را در `web.config` نگه دارید؛ تعویض مسیر، حذف پوشه یا کپی XML به ماشین دیگر بازیابی secretهای TOTP را تضمین نمی‌کند. این فایل پوشه/ACL یا تنظیمات App Pool سرور را خودکار ایجاد نمی‌کند. جزئیات در [قرارداد GAP-04](../release/GAP_04_BACKOFFICE_TOTP_MFA.md) است.

## فایل SQL مایگریشن‌ها

Migration تازه‌ای برای این تغییر کانفیگ لازم نیست. دو Migration موجود مرتبط با MFA و Child Safety عبارت‌اند از `20261015073418_AddPlatformTotpMfa` و `20261015091336_EnforceApprovedMessengerChildSafety`. اگر پایگاه هدف قدیمی‌تر از GAP-01 است، اسکریپت محدود این دو مرحله کافی نیست. خروجی کامل idempotent را بدون اتصال به دیتابیس می‌توان تولید کرد:

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

## Gate شکست و rollback

استقرار شکست‌خورده است اگر startup fail-fast شود، readiness سبز نشود، migration ناقص باشد، smoke test خطا دهد، 5xx/latency غیرعادی شود یا worker backlog رشد پایدار داشته باشد. در این حالت traffic را متوقف و artifact قبلی را برگردانید. downgrade migration را کورکورانه اجرا نکنید؛ بسته به compatibility از forward-fix یا restore تأییدشده استفاده کنید و trace ID/logهای بازه را نگه دارید.

## smoke زنده GAP-02

شواهد و دستور ابزارهای opt-in در [گزارش GAP-02](../release/GAP_02_STAGING_CORE_SMOKE_VALIDATION.md) آمده‌اند. ابزار قدیمی `smoke-test.ps1` همچنان OTP زنده اجرا نمی‌کند؛ ابزار جدا `staging-core-smoke.ps1` به HTTPS معتبر، تأیید صریح و ورود مخفی OTP توسط اپراتور نیاز دارد. normal `dotnet test` هرگز SMS واقعی نمی‌فرستد.

publish بررسی‌شده Web SDK، `web.config` را با `AspNetCoreModuleV2`، `processPath=dotnet`، `hostingModel=inprocess` و `stdoutLogEnabled=false` تولید می‌کند؛ وجود artifact اثبات نصب module یا WebSocket روی سرور نیست. برای artifact رسمی از source تمیز commit تأییدشده و secret provisioning کنترل‌شده استفاده کنید، نه worktree دارای تغییرات نامرتبط. artifact بدون secret قابل‌اشتراک است؛ نسخهٔ خصوصی حاوی JSON واقعی فقط برای انتقال امن اپراتور است و عمومی/commit نشود.
