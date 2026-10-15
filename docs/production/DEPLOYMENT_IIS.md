# استقرار MakanApp روی IIS

## پیش‌نیاز

- Windows Server پشتیبانی‌شده، IIS و .NET 9 Hosting Bundle نصب و پس از نصب IIS restart شده باشد.
- WebSocket Protocol برای SignalR فعال باشد.
- App Pool با `No Managed Code` و identity اختصاصی اجرا شود.
- مسیر publish و مسیر persistent storage جدا باشند؛ App Pool فقط روی storage و محل log عملیاتی مجوز لازم را داشته باشد.
- SQL Server از اینترنت عمومی قابل دسترسی نباشد.
- در IIS 10، `requestFiltering removeServerHeader="true"` و حذف `X-Powered-By` را در تنظیمات سایت اعمال کنید؛ Kestrel نیز `AddServerHeader=false` دارد. این کار کاهش disclosure است، نه ادعای جلوگیری کامل از fingerprinting.

## تنظیمات محیط

`ASPNETCORE_ENVIRONMENT=Production` یا برای محیط جدا `Staging` و مقادیر زیر در IIS Configuration Editor، environmentVariables یا secret store سرور تنظیم شوند. هر دو محیط کنترل fail-fast دارند؛ مقدار واقعی در repository نوشته نشود.

```text
ConnectionStrings__MakanDatabase
Identity__SecurityKey
Storage__RootPath
AllowedHosts
Cors__AllowedOrigins__0
ReverseProxy__Enabled=true
ReverseProxy__KnownProxies__0=127.0.0.1
Hosting__RequireHttps=true
Swagger__Enabled=false
Sms__Provider
Sms__MeliPayamakOtp__ServiceToken          # فقط اگر MeliPayamakOtp انتخاب شده باشد
Payments__Provider=Unavailable             # تا زمان adapter تأییدشده
Messaging__ContentSafetyProvider=Internal
BackofficeSecurity__KeyRingPath=C:\ProgramData\MakanApp\BackofficeMfaKeys
Notifications__PushProvider=Unavailable
Notifications__SmsProvider=Unavailable
```

`Identity__SecurityKey` حداقل ۳۲ بایت تصادفی و پایدار بین restartها باشد. rotation آن بدون برنامه، OTPهای ذخیره‌شده و token hashهای جدید را مختل می‌کند. connection string Production باید `Encrypt=True;TrustServerCertificate=False` داشته باشد و certificate SQL زنجیره‌ی قابل‌اعتماد داشته باشد.

`MakanApp.Api/web.config` تنظیم Production و مسیر پیش‌فرض بالا را همراه Publish مخصوص IIS منتقل می‌کند؛ Linux/Docker این فایل IIS را مصرف نمی‌کنند و همچنان مسیر volume و محافظ معتبر خود را از environment می‌گیرند. `Messaging:ContentSafetyProvider=Internal` در `appsettings.Production.json` موجود است و کلید provider خارجی لازم ندارد. مقادیر SMS، اتصال SQL و Secretهای فعلی سرور را هنگام کپی فایل‌ها حفظ کنید؛ فایل نمونه جایگزین کامل کانفیگ اختصاصی سرور نیست.

پیش از استفاده از MFA، پوشهٔ KeyRing را بیرون Publish و Storage بسازید و فقط به هویت App Pool و اپراتور مجاز دسترسی لازم بدهید. بدون certificate، محافظ Windows از DPAPI استفاده می‌کند؛ هویت App Pool باید ثابت و `Load User Profile` مناسب باشد. اگر MFA قبلاً فعال بوده، همان مسیر و کلیدهای قبلی را در `web.config` نگه دارید؛ تعویض مسیر، حذف پوشه یا کپی XML به ماشین دیگر بازیابی secretهای TOTP را تضمین نمی‌کند. این فایل پوشه/ACL یا تنظیمات App Pool سرور را خودکار ایجاد نمی‌کند. جزئیات در [قرارداد GAP-04](../release/GAP_04_BACKOFFICE_TOTP_MFA.md) است.

## فایل SQL مایگریشن‌ها

Migration تازه‌ای برای این تغییر کانفیگ لازم نیست. دو Migration موجود مرتبط با MFA و Child Safety عبارت‌اند از `20261015073418_AddPlatformTotpMfa` و `20261015091336_EnforceApprovedMessengerChildSafety`. اگر پایگاه هدف قدیمی‌تر از GAP-01 است، اسکریپت محدود این دو مرحله کافی نیست. خروجی کامل idempotent را بدون اتصال به دیتابیس می‌توان تولید کرد:

```powershell
dotnet ef migrations script 0 20261015091336_EnforceApprovedMessengerChildSafety --idempotent --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --context MakanDbContext --configuration Debug --output .artifacts/deployment/migrations-idempotent.sql
```

تولید فایل SQL به معنی اجرای آن نیست. قبل از اجرا، اپراتور باید دیتابیس انتخاب‌شده، Backup و `__EFMigrationsHistory` را بررسی و تغییرات pending را بازبینی کند؛ idempotent فقط به history ثبت‌شده متکی است و اختلاف schema دستی را اصلاح نمی‌کند. اجرا با migration principal جدا و توقف در اولین خطا انجام شود؛ این دستور هیچ `database update` یا startup migration ندارد.

## ترتیب استقرار

1. artifact امضاشده/تأییدشده و commit مورد انتظار را مشخص کنید.
2. backup دیتابیس و storage بگیرید و قابل‌خواندن بودن backup را بررسی کنید.
3. سایت را drain/stop کنید اگر migration با نسخه‌ی قبلی backward-compatible نیست.
4. migration را با migration principal اجرا کنید؛ application در startup migration اجرا نمی‌کند.
5. فایل‌های publish را side-by-side قرار دهید و binding را به نسخه‌ی جدید تغییر دهید.
6. identity App Pool را روی `Storage__RootPath` دارای Modify کنید؛ روی source/secrets حداقل دسترسی را نگه دارید.
7. TLS binding معتبر، redirect HTTP به HTTPS، HSTS و firewall را بررسی کنید.
8. `/health/live` و `/health/ready` و سپس `tools/production/smoke-test.ps1` را اجرا کنید.
9. Event Log/log collector، 5xx، latency و worker backlog را هنگام warm-up مشاهده کنید.

## Forwarded headers

برنامه فقط وقتی `ReverseProxy:Enabled=true` است forwarded headers را می‌خواند، `ForwardLimit=1` دارد و فقط IPهای `KnownProxies` را قبول می‌کند. IP proxy واقعی را صریح ثبت کنید؛ subnet عمومی یا اعتماد سراسری ممنوع است.

## Gate شکست و rollback

استقرار شکست‌خورده است اگر startup fail-fast شود، readiness سبز نشود، migration ناقص باشد، smoke test خطا دهد، 5xx/latency غیرعادی شود یا worker backlog رشد پایدار داشته باشد. در این حالت traffic را متوقف و artifact قبلی را برگردانید. downgrade migration را کورکورانه اجرا نکنید؛ بسته به compatibility از forward-fix یا restore تأییدشده استفاده کنید و trace ID/logهای بازه را نگه دارید.

## smoke زنده GAP-02

شواهد و دستور ابزارهای opt-in در [گزارش GAP-02](../release/GAP_02_STAGING_CORE_SMOKE_VALIDATION.md) آمده‌اند. ابزار قدیمی `smoke-test.ps1` همچنان OTP زنده اجرا نمی‌کند؛ ابزار جدا `staging-core-smoke.ps1` به HTTPS معتبر، تأیید صریح و ورود مخفی OTP توسط اپراتور نیاز دارد. normal `dotnet test` هرگز SMS واقعی نمی‌فرستد.

publish بررسی‌شده Web SDK، `web.config` را با `AspNetCoreModuleV2`، `processPath=dotnet`، `hostingModel=inprocess` و `stdoutLogEnabled=false` تولید می‌کند؛ وجود artifact اثبات نصب module یا WebSocket روی سرور نیست. نسخه‌ی تمیز commit تأییدشده را publish کنید، نه worktree دارای تغییرات نامرتبط. تنظیمات محرمانه را به artifact اضافه نکنید.
