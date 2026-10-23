# راهنمای استقرار MakanApp در Windows، Linux و Docker

این سند روش استقرار backend فعلی MakanApp و تأمین امن تنظیمات محرمانه را توضیح می‌دهد. مثال‌ها برای `.NET 9`، `SQL Server` و provider واقعی `MeliPayamakOtp` نوشته شده‌اند.

مبنای جاری `main@5885d11` در `2026-10-23`؛ 325 عملیات HTTP، 107 جدول و 48 migration؛ آخرین migration `20261017033804_AddConversationChecklists`. این سند مثال استقرار است؛ اجرای واقعی سرور را اثبات نمی‌کند. [راهنمای بسته‌های IIS](production/PUBLISH_GUIDE_FA.md) و [وضعیت جاری](CURRENT_PROJECT_STATUS.md).

انتشار انتخابی فعلی مالک Production روی HTTP است؛ appsettings.Staging.json حذف شده. فایل واقعی Production محلی و ignored است و نمونهٔ عمومی .json.example بدون secret نسخه‌بندی می‌شود. دو استثنای Hosting:AllowInsecureHttp و Database:AllowUntrustedServerCertificate فقط با تأیید صریح مالک در فایل خصوصی این انتشار فعال‌اند؛ پیش‌فرض امن تغییر نکرده است. HTTP، OTP و توکن را رمزگذاری نمی‌کند. این تصمیم gate امن [GAP-02](release/GAP_02_STAGING_CORE_SMOKE_VALIDATION.md) را نمی‌بندد و ابزارهای HTTPS-only را دور نمی‌زند. [مراحل جاری IIS](production/DEPLOYMENT_IIS.md) مبنای Windows است؛ نمونه‌های Linux/Docker حالت امن و مقادیر مختص محیط می‌خواهند.

> هیچ ServiceToken، connection string، password یا Identity:SecurityKey واقعی نباید وارد Git، image، Dockerfile، Compose file یا artifact مشترک شود. فایل خصوصی ignored محلی مجاز، ولی خروجی حاوی آن محرمانه و فقط برای انتقال امن اپراتور است.

## تغییرات جاری که قبل از deploy باید خوانده شوند

| محیط | OTP | بسته و حدود |
|---|---|---|
| Development/Test/Testing | Mock فقط false/true؛ Real برابر true/false | Mock مالکیت شماره را اثبات نمی‌کند؛ داده و secret Production مجاز نیست |
| Production/Staging | فقط true/false | unsafe OTP در startup رد می‌شود؛ تغییر نام محیط درمان خطای Production نیست |

IIS دو profile مستقل `TestIis` و `ProductionIis` دارد. فرمان‌های عمومی Linux/Docker این سند Production واقعی فرض می‌کنند؛ `Otp__Enabled=true` و `Otp__AllowMockAuthentication=false` را در تنظیمات امن همان محیط تأمین کنید. JSON مشترک فعلی false/true است و به‌تنهایی برای Production کافی نیست. templateهای Test/Production هم‌شکل‌اند، نه هم‌مقدار؛ فقط در نبود فایل خصوصی از example استفاده کنید.

`/health/ready` config، key ring، SQL و Storage را بررسی می‌کند. key ring خارجی با ApplicationName ثابت `MakanApp.BackofficeMfa.v1` و محافظ قبلی حفظ شود؛ probe startup و readiness برای آن فعال است. DPAPI کاربر جاری به identity/profile وابسته است؛ PFX/ring/password بازیابی مستقل می‌خواهند. [پیکربندی canonical](operations/PRODUCTION_CONFIGURATION_RUNBOOK.md).

بودجه Host با override اختیاری `Hosting:ShutdownTimeoutSeconds` در بازه ۵..۹۰s و ANCM با `shutdownTimeLimit=120` مستقل‌اند؛ WAS/idle/preload هم روی سرور بررسی می‌شوند. worker داخل host با توقف آن می‌ایستد. نخستین rollout report-export fencing جدید، legacy process را drain/stop می‌خواهد؛ scale-out realtime/Presence تأیید نشده است. [worker recovery](operations/WORKER_RELIABILITY_AND_RECOVERY.md).

`Storage:TerminalCleanup:Enabled=false` را صرفاً برای کامل‌شدن deploy روشن نکنید. grace حقوقی/محصولی پیش‌فرض ندارد؛ فقط bytes محلی Deleted/Rejected بدون reference مشمول‌اند، نه Ready/Pending یا SQL history. [retention](operations/RETENTION_AND_CLEANUP_POLICY.md). metric/trace محلی جای exporter، alert، reboot، load یا restore محیط هدف نیست.

AI اختیاری با بودجهٔ پیش‌فرض ۳۵s و بدون retry خودکار POST است؛ Service Auth واقعی قبلی PASS، Parent Provider Timeout و Full-12 Deferred هستند. base/private URL، کلید و JWT به frontend ندهید؛ [وضعیت AI](ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md).

## 1. تنظیمات الزامی Production

برنامه در Production حداقل به این مقادیر نیاز دارد:

| کلید .NET | کاربرد | محرمانه |
|---|---|---|
| `ConnectionStrings:MakanDatabase` | اتصال runtime به SQL Server | بله |
| `Identity:SecurityKey` | HMAC مربوط به OTP و session؛ پایدار و حداقل ۳۲ بایت UTF-8 | بله |
| `Sms:Provider` | برای ارسال واقعی برابر `MeliPayamakOtp` | خیر |
| `Sms:MeliPayamakOtp:BaseUrl` | برابر `https://console.melipayamak.com` | خیر |
| `Sms:MeliPayamakOtp:ServiceToken` | credential سرویس OTP ملی‌پیامک | بله |
| `Sms:MeliPayamakOtp:TimeoutSeconds` | timeout درخواست؛ مقدار پیشنهادی `10` | خیر |
| `Storage:RootPath` | محل پایدار فایل‌های آپلودشده | بسته به محیط |
| `AllowedHosts` / `Cors:AllowedOrigins` | Host و origin صریح؛ بدون wildcard | خیر |
| `ReverseProxy:Enabled` / `KnownProxies` | فعال و IP واقعی proxy مورد اعتماد | خیر |
| `Hosting:RequireHttps` / `AllowInsecureHttp` | پیش‌فرض true / false؛ استثنای HTTP فقط Production | خیر |
| `Database:AllowUntrustedServerCertificate` | پیش‌فرض false؛ Encrypt=False همیشه رد است | خیر |
| `Security:DataProtection:KeyRingPath` | مسیر مطلق پایدار خارج Publish/Storage؛ alias قبلی BackofficeSecurity معتبر و تعارض رد می‌شود | مسیر عملیاتی |
| `Security:DataProtection:CertificatePath/CertificatePassword` | محافظ PFX؛ Windows بدون PFX از DPAPI کاربر جاری، Linux نیازمند محافظ قابل‌بازیابی؛ aliasهای قبلی معتبرند | password محرمانه |

در Environment Variable، جداکننده `:` با `__` جایگزین می‌شود:

```text
ConnectionStrings__MakanDatabase
Identity__SecurityKey
Sms__Provider
Sms__MeliPayamakOtp__BaseUrl
Sms__MeliPayamakOtp__ServiceToken
Sms__MeliPayamakOtp__TimeoutSeconds
Storage__RootPath
```

Environment Variable تنظیم هم‌نام داخل JSON را override می‌کند. این رفتار برای نگه‌داشتن تنظیمات عمومی در JSON و secretها خارج از artifact مناسب است.

با تنظیم یک مسیر مطلق در `Storage:RootPath`، ذخیره‌سازی محلی در Production فعال می‌شود. فایل‌ها از طریق endpointهای دارای کنترل دسترسی دریافت می‌شوند؛ این مسیر نباید داخل پوشه publish یا web root باشد. برای اجرای چند نمونه برنامه، دیسک محلی هر سرور به‌تنهایی کافی نیست و باید storage مشترک در نظر گرفته شود.

Swagger UI و OpenAPI در Production/Staging پیش‌فرض خاموش‌اند؛ فعال‌سازی صریح همراه با کنترل شبکه/دسترسی لازم است. APIهای حفاظت‌شده همچنان Bearer و مجوز جاری می‌خواهند.

CORS امن Production و Staging فقط origin صریح HTTPS می‌پذیرد؛ HTTP فقط با Hosting:AllowInsecureHttp در Production مجاز است. wildcard، cookie credentials، proxy نامعتبر و bypass Authentication از این استثنا نتیجه نمی‌شوند. Host و proxy باید با محیط واقعی منطبق باشند؛ Origin و UserId مجوز کسب‌وکاری نیستند.

### 1.1 رفتار واقعی گزینه‌ها و محدودیت‌های میزبان

| مورد | رفتار فعلی |
|---|---|
| `Sms:Provider` در appsettings پایه | `Unavailable`؛ صرف Production بودن پیامک واقعی را فعال نمی‌کند |
| `Sms:Provider` در Development | فقط با OTP واقعی انتخاب می‌شود؛ Mock با false/true provider ارسال OTP را ثبت/فراخوانی نمی‌کند |
| `MeliPayamakOtp` | `ServiceToken` و `Identity:SecurityKey` پایدار حتی در Development الزامی‌اند |
| `Sms:MeliPayamakOtp:TimeoutSeconds` | پیش‌فرض10؛ فقط1 تا60؛ مقدار نامعتبر startup را رد می‌کند |
| `Sms:MeliPayamakOtp:BaseUrl` | HTTPS origin بدون credential، path، query یا fragment |
| `Identity:Otp:MinimumCodeLength` / `MaximumCodeLength` | پیش‌فرض4 / 64؛ بازه معتبر حداکثر256؛ UI نباید کد شش‌رقمی ثابت فرض کند |
| `Storage:RootPath` خالی در Production/Staging | startup fail-fast می‌شود؛ مسیر مطلق و پایدار لازم است |
| `Storage:RootPath` غیرخالی | `LocalFileStorage`؛ مسیر باید مطلق و قابل نوشتن باشد؛ خطا ممکن است هنگام resolve سرویس رخ دهد |
| Storage defaults | سقف فایل10MiB؛ PDF/PNG/JPEG/plain text؛ metadata انقضای Unattached برابر24ساعت است، purge عمومی نیست. cleanup محدود terminal فقط با opt-in و grace صریح |
| `/health` | فقط liveness؛ SQL، SMS، permission دیسک و outbox را probe نمی‌کند |
| Swagger/OpenAPI | در Production/Staging پیش‌فرض خاموش؛ فعال‌سازی صریح لازم است |
| CORS | allowlist صریح؛ HTTPS امن پیش‌فرض و HTTP فقط با استثنای مستقل Production؛ بدون AllowCredentials |
| Multi-instance | shared storage و SignalR backplane در این نسخه فراهم نشده؛ استقرار تک‌instance مبنای این سند است |

کلید HMAC در deploy معمولی ثابت بماند. تغییر آن هم OTP ذخیره‌شده و هم نشست‌های موجود را نامعتبر می‌کند. کد OTP توسط provider تولید و ارسال می‌شود؛ backend همان کد normalize‌شده را hash می‌کند. درخواست SMS retry خودکار ندارد و timeout لزوماً اثبات نرسیدن پیامک نیست. URL provider شامل token است؛ علاوه بر log برنامه، proxy خروجی و ابزار APM نیز نباید URL/body حساس را ثبت کنند.

## 2. فایل server-only با appsettings.Production.json

فایل خصوصی می‌تواند روی ماشین مجاز اپراتور یا سرور کنار DLL نگه‌داری شود. نام واقعی appsettings.Production.json و در Git ignored است؛ Web SDK فایل موجود را هنگام Publish کپی می‌کند. برای clone تازه از نمونهٔ عمومی کپی بگیرید و مقادیر را فقط خصوصی وارد کنید. قالب امن کامل زیر نمونه است، نه مقادیر واقعی انتشار فعلی:

```json
{
  "ConnectionStrings": {
    "MakanDatabase": "<PRODUCTION_SQL_SERVER_CONNECTION_STRING>"
  },
  "Otp": { "Enabled": true, "AllowMockAuthentication": false },
  "Identity": {
    "SecurityKey": "<AT_LEAST_32_UTF8_BYTES_RANDOM_SECRET>"
  },
  "Sms": {
    "Provider": "MeliPayamakOtp",
    "MeliPayamakOtp": {
      "BaseUrl": "https://console.melipayamak.com",
      "ServiceToken": "<MELIPAYAMAK_OTP_SERVICE_TOKEN>",
      "TimeoutSeconds": 10
    }
  },
  "Storage": {
    "RootPath": "<PERSISTENT_STORAGE_PATH>"
  },
  "AllowedHosts": "api.example.test",
  "Cors": {
    "AllowedOrigins": ["https://app.example.test"]
  },
  "Hosting": {
    "RequireHttps": true,
    "AllowInsecureHttp": false
  },
  "Database": {
    "AllowUntrustedServerCertificate": false
  },
  "ReverseProxy": {
    "Enabled": true,
    "KnownProxies": ["127.0.0.1"]
  },
  "Swagger": {
    "Enabled": false
  },
  "Messaging": {
    "ContentSafetyProvider": "Internal"
  },
  "BackofficeSecurity": {
    "KeyRingPath": "<PERSISTENT_MFA_KEY_RING_OUTSIDE_STORAGE_AND_PUBLISH>"
  }
}
```

قواعد این فایل:

- مقدارهای داخل `<...>` حتماً روی سرور جایگزین شوند.
- فایل واقعی محلی می‌تواند در مسیر ignored پروژه باشد، ولی هرگز stage/commit/push نشود؛ نمونهٔ عمومی جای فایل خصوصی نیست.
- فایل همراه Docker image ساخته نشود.
- فقط account اجرای برنامه و Administrator/root به آن دسترسی خواندن داشته باشند.
- deploy بعدی نباید مقادیر قبلی را با نمونهٔ خالی، کلید تصادفی یا فایل محیط دیگری جایگزین کند. خروجی حاوی JSON واقعی محرمانه و غیرقابل‌اشتراک است.
- برنامه باید با environment برابر `Production` اجرا و بعد از تغییر فایل restart شود.

این فایل plaintext است و secret vault محسوب نمی‌شود؛ امنیت آن به ACL سیستم‌عامل و محدودکردن دسترسی سرور وابسته است.

فایل خصوصی Windows فعلی RequireHttps=false، AllowInsecureHttp=true و AllowUntrustedServerCertificate=true دارد. آن را بدون تغییر مسیرها، proxy و certificate به Linux/container کپی نکنید. پس از آماده شدن TLS معتبر، RequireHttps=true، هر دو استثنا=false، SQL TrustServerCertificate=False و originهای HTTPS تنظیم شوند. ارتباط ملی‌پیامک همچنان HTTPS است و با HTTP بودن API عوض نمی‌شود.

## 3. استقرار روی Windows Server با IIS

### 3.1 پیش‌نیاز

1. .NET 9 Hosting Bundle را روی Windows Server نصب کنید.
2. یک Application Pool اختصاصی، برای مثال `MakanApp`, بسازید.
3. گزینه `.NET CLR Version` در Application Pool را روی `No Managed Code` قرار دهید.
   برای x64، Enable 32-Bit Applications=false و برای DPAPI، Load User Profile=true باشد؛ هویت قبلی App Pool را عوض نکنید. WebSocket Protocol نیز بررسی شود.
4. یک مسیر استقرار مشخص مانند مسیر زیر بسازید:

```text
C:\Sites\MakanApp
```

### 3.2 publish

روی build machine از ریشه repository اجرا کنید:

```powershell
dotnet restore MakanApp.sln
dotnet publish .\MakanApp.Api\MakanApp.Api.csproj `
  --configuration Release `
  --output .\artifacts\MakanApp.Api `
  --nologo
```

از سورس تمیز commit تأییدشده به پوشهٔ تازه Publish بگیرید. همین سایت/App Pool را برای جایگزینی کنترل‌شده متوقف و خروجی کامل را منتقل کنید؛ فقط JSON را روی DLL قدیمی کپی نکنید. فایل واقعی Production و web.config باید همراه خروجی باشند و Staging قدیمی در خروجی تازه نباشد. Backup دیتابیس، Storage، کانفیگ و custody کلیدهای MFA را حفظ کنید. فایل خصوصی فقط با انتقال امن و ACL محدود جابه‌جا شود.

### 3.3 تنظیم appsettings مخصوص سرور

فایل زیر را فقط روی سرور ایجاد کنید:

```text
C:\Sites\MakanApp\appsettings.Production.json
```

از template بخش ۲ استفاده کنید. برای `Storage:RootPath` یک مسیر خارج از پوشه publish پیشنهاد می‌شود:

```text
C:\MakanAppData\Storage
```

در نتیجه deploy جدید فایل‌های کاربران را حذف نمی‌کند.

### 3.4 محدودکردن ACL

نام Application Pool مثال زیر `MakanApp` است؛ در صورت تفاوت، آن را جایگزین کنید:

```powershell
icacls "C:\Sites\MakanApp\appsettings.Production.json" /inheritance:r
icacls "C:\Sites\MakanApp\appsettings.Production.json" `
  /grant:r "BUILTIN\Administrators:(F)" "IIS AppPool\MakanApp:(R)"

New-Item -ItemType Directory -Path "C:\MakanAppData\Storage" -Force
icacls "C:\MakanAppData\Storage" `
  /grant "IIS AppPool\MakanApp:(OI)(CI)(M)"
```

قبل از فرمان، نام App Pool و مسیرها را بررسی کنید. کانفیگ read-only و Storage قابل نوشتن باشد. مسیر پایدار MFA نیز بیرون Publish و StorageRoot لازم است؛ پیش‌فرض web.config برابر C:\ProgramData\MakanApp\BackofficeMfaKeys است. اگر قبلاً MFA فعال شده، همان مسیر/کلیدها و هویت Windows حفظ شوند؛ پوشهٔ خالی جایگزین کلیدهای قبلی نشود. App Pool روی key ring مجوز لازم داشته باشد، نه Everyone. DPAPI به هویت/ماشین وابسته است؛ انتقال XML به سرور دیگر کافی نیست.

### 3.5 environment برنامه

در `web.config` تولیدشده توسط publish فقط environment غیرمحرمانه را می‌توان مشخص کرد:

```xml
<aspNetCore processPath="dotnet"
            arguments=".\MakanApp.Api.dll"
            stdoutLogEnabled="false"
            hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
  </environmentVariables>
</aspNetCore>
```

قرار دادن `ServiceToken` در `web.config` پیشنهاد نمی‌شود، چون deploy می‌تواند آن را overwrite کند. اگر `appsettings.Production.json` استفاده می‌شود، token همان‌جا و فقط روی سرور باقی بماند.

web.config نسخهٔ جاری Production و BackofficeSecurity__KeyRingPath را دارد؛ مقادیر نمونهٔ بالا جایگزین کور نسخهٔ موجود نشوند. DOTNET_ENVIRONMENT و ASPNETCORE_ENVIRONMENT سرور نباید متناقض باشند؛ environment و متغیرهای هم‌نام قدیمی می‌توانند انتخاب فایل یا مقدار JSON را عوض کنند. secretها را برای بررسی چاپ نکنید.

پس از تغییر تنظیمات، Application Pool را recycle کنید:

```powershell
Restart-WebAppPool -Name "MakanApp"
```

### 3.6 Windows Service

پروژه فعلی مشخصاً برای Windows Service host پیکربندی نشده است. برای Windows Server، IIS مسیر آماده‌تر است. تبدیل رسمی به Windows Service باید در یک مرحله کدنویسی مستقل با lifecycle، working directory، logging و recovery policy بررسی شود.

اگر برنامه موقتاً به‌صورت مستقیم اجرا می‌شود، working directory باید پوشه publish باشد:

```powershell
Set-Location C:\Sites\MakanApp
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet .\MakanApp.Api.dll --urls "http://127.0.0.1:5080"
```

این روش برای نگه‌داری production بدون service manager مناسب نیست.

## 4. استقرار روی Linux با systemd

### 4.1 مسیرهای پیشنهادی

```text
/opt/makanapp/app                 فایل‌های publish
/etc/makanapp/makanapp.env       secretها و configuration runtime
/var/lib/makanapp/storage        فایل‌های آپلودشده
/var/lib/makanapp/mfa-keys       key ring پایدار MFA، جدا از Storage
/etc/makanapp/mfa-protection.pfx محافظ certificate با private key و دسترسی محدود
```

یک user بدون shell تعاملی بسازید:

```bash
sudo useradd --system --home /opt/makanapp --shell /usr/sbin/nologin makanapp
sudo mkdir -p /opt/makanapp/app /etc/makanapp /var/lib/makanapp/storage
sudo chown -R makanapp:makanapp /opt/makanapp /var/lib/makanapp
```

### 4.2 فایل EnvironmentFile

فایل `/etc/makanapp/makanapp.env` را فقط روی سرور بسازید:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5080
ConnectionStrings__MakanDatabase="<PRODUCTION_SQL_SERVER_CONNECTION_STRING>"
Otp__Enabled=true
Otp__AllowMockAuthentication=false
Identity__SecurityKey="<AT_LEAST_32_UTF8_BYTES_RANDOM_SECRET>"
Sms__Provider=MeliPayamakOtp
Sms__MeliPayamakOtp__BaseUrl=https://console.melipayamak.com
Sms__MeliPayamakOtp__ServiceToken="<MELIPAYAMAK_OTP_SERVICE_TOKEN>"
Sms__MeliPayamakOtp__TimeoutSeconds=10
Storage__RootPath=/var/lib/makanapp/storage
AllowedHosts=api.example.test
Cors__AllowedOrigins__0=https://app.example.test
ReverseProxy__Enabled=true
ReverseProxy__KnownProxies__0=127.0.0.1
Hosting__RequireHttps=true
Hosting__AllowInsecureHttp=false
Database__AllowUntrustedServerCertificate=false
Swagger__Enabled=false
Messaging__ContentSafetyProvider=Internal
BackofficeSecurity__KeyRingPath=/var/lib/makanapp/mfa-keys
BackofficeSecurity__CertificatePath=/etc/makanapp/mfa-protection.pfx
BackofficeSecurity__CertificatePassword="<PRIVATE_PFX_PASSWORD>"
```

Permission فایل:

```bash
sudo chown root:root /etc/makanapp/makanapp.env
sudo chmod 600 /etc/makanapp/makanapp.env
```

فایل environment را داخل repository یا artifact قرار ندهید.

دامنه، origin، اتصال SQL و IP proxy مثال‌اند و باید با محیط واقعی عوض شوند. key ring جداگانه ساخته و writable برای user سرویس باشد؛ PFX دارای private key و password معتبر را user سرویس فقط بخواند. در host عملیاتی Linux/container، نبود ring پایدار یا محافظ قابل‌استفاده startup را رد می‌کند؛ خرابی بعد از شروع نیز readiness را unhealthy می‌کند. DPAPI Windows به Linux منتقل نمی‌شود. سایر APIهای آموزشی جایگزین مجوز MFA نیستند.

### 4.3 واحد systemd

فایل `/etc/systemd/system/makanapp.service`:

```ini
[Unit]
Description=MakanApp Backend API
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=makanapp
Group=makanapp
WorkingDirectory=/opt/makanapp/app
ExecStart=/usr/bin/dotnet /opt/makanapp/app/MakanApp.Api.dll
EnvironmentFile=/etc/makanapp/makanapp.env
Restart=on-failure
RestartSec=5
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
```

فعال‌سازی:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now makanapp
sudo systemctl status makanapp --no-pager
```

برای مشاهده log بدون چاپ فایل secret:

```bash
sudo journalctl -u makanapp -n 200 --no-pager
```

برای TLS عمومی، برنامه را پشت reverse proxy مانند Nginx قرار دهید و Kestrel را فقط روی loopback نگه دارید.

### 4.4 TLS termination، آدرس IP و SignalR

Program.cs خارج Testing طبق Hosting:RequireHttps redirect می‌کند و UseForwardedHeaders مشروط به ReverseProxy:Enabled و KnownProxies است. پشت TLS termination، scheme و IP واقعی باید بررسی شوند؛ redirect loop، scheme اشتباه و تجمیع کاربران زیر IP پروکسی نشانهٔ تنظیم ناقص‌اند. limiter OTP از RemoteIpAddress استفاده می‌کند. HTTP داخلی Kestrel پشت proxy امن، مجوز فعال کردن استثنای HTTP عمومی نیست.

نمونه systemd و Compose به‌تنهایی proxyهای مورد اعتماد را تعیین نمی‌کنند؛ مقادیر واقعی و صریح محیط را تزریق کنید. اعتماد عمومی به header اینترنت راه‌حل نیست. [راهنمای رسمی Forwarded Headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-9.0) مبنای بررسی زیرساخت باشد.

بازبودن CORS پشتیبانی query-token را اضافه نمی‌کند. `SessionAuthenticationHandler` فقط Authorization header را می‌خواند؛ browser WebSocket/SSE که token را در query می‌فرستد با قرارداد فعلی سازگار نیست. مسیر قابل بررسی مرورگر، SignalR LongPolling با `accessTokenFactory` و `withCredentials:false` است؛ نمونه در قرارداد فرانت‌اند آمده است. در proxy، درخواست‌های negotiate/poll/send/delete و timeout اتصال باید عبور کنند. پشتیبانی cookie-based affinity یا چند instance از این مثال قابل نتیجه‌گیری نیست. [ملاحظات رسمی امنیت SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-9.0) این تفاوت transport و credential را توضیح می‌دهد.

## 5. Dockerfile پیشنهادی

این بخش template است و در repository فعلی Dockerfile ایجاد نمی‌کند.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY MakanApp.Domain/MakanApp.Domain.csproj MakanApp.Domain/
COPY MakanApp.Application/MakanApp.Application.csproj MakanApp.Application/
COPY MakanApp.Infrastructure/MakanApp.Infrastructure.csproj MakanApp.Infrastructure/
COPY MakanApp.Api/MakanApp.Api.csproj MakanApp.Api/

RUN dotnet restore MakanApp.Api/MakanApp.Api.csproj

COPY . .
RUN dotnet publish MakanApp.Api/MakanApp.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "MakanApp.Api.dll"]
```

`.dockerignore` پیشنهادی:

```dockerignore
.git
.vs
**/bin
**/obj
artifacts
.artifacts
docs
appsettings.Production.json
**/appsettings.Production.json
**/*.pfx
**/*.key
secrets
**/secrets
*.env
.env*
```

هیچ secret نباید با `ARG` یا `ENV` داخل Dockerfile قرار بگیرد؛ این مقادیر می‌توانند در metadata یا history image باقی بمانند.

## 6. Docker Compose با appsettings server-only

این روش پس از تکمیل کلیدهای الزامی و مقادیر واقعی محیط با قرارداد configuration سازگار است؛ Docker/Compose در این workspace اجرا و validate نشده‌اند. فایل تنظیمات خارج repository روی host نگه‌داری و read-only mount می‌شود. فایل خصوصی Windows را با مسیرهای C: یا DPAPI به Linux کپی نکنید.

مثال Linux host:

```yaml
services:
  makanapp-api:
    image: registry.example.com/makanapp-api:<IMMUTABLE_RELEASE_TAG>
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: "8080"
    ports:
      - "127.0.0.1:5080:8080"
    volumes:
      - /etc/makanapp/appsettings.Production.json:/app/appsettings.Production.json:ro
      - /var/lib/makanapp/storage:/data/storage
      - /var/lib/makanapp/mfa-keys:/data/mfa-keys
      - /etc/makanapp/mfa-protection.pfx:/run/secrets/makan-mfa.pfx:ro
```

در فایل mountشده، علاوه بر Host/CORS/proxy/SQL/Identity/provider بخش ۲، مسیرها و محافظ Linux چنین تنظیم شوند:

```json
{
  "Storage": {
    "RootPath": "/data/storage"
  },
  "BackofficeSecurity": {
    "KeyRingPath": "/data/mfa-keys",
    "CertificatePath": "/run/secrets/makan-mfa.pfx",
    "CertificatePassword": "<PRIVATE_PFX_PASSWORD>"
  }
}
```

روی Windows host با Linux container، source مسیر bind mount می‌تواند برای مثال این باشد:

```yaml
volumes:
  - C:/MakanApp/config/appsettings.Production.json:/app/appsettings.Production.json:ro
  - C:/MakanApp/data/storage:/data/storage
  - C:/MakanApp/data/mfa-keys:/data/mfa-keys
  - C:/MakanApp/config/mfa-protection.pfx:/run/secrets/makan-mfa.pfx:ro
```

دسترسی Docker daemon ذاتاً حساس است؛ فقط administratorهای مجاز باید به host، Compose project و فایل تنظیمات دسترسی داشته باشند.
همچنین UID کاربر non-root داخل container باید اجازه خواندن فایل تنظیمات و نوشتن در مسیر storage را داشته باشد.
همان UID/GID واقعی باید روی mfa-keys مجوز لازم و روی PFX مجوز read داشته باشد؛ فایل root-only با mode=600 الزاماً برای non-root قابل خواندن نیست. مجوز جهانی راه‌حل نیست. volumeهای key ring و storage مستقل، PFX/password محرمانه و identity پایدارند. proxy در Docker الزاماً 127.0.0.1 نیست؛ IP واقعیِ قابل اعتماد را تعیین کنید.

## 7. Docker Compose با Docker Secrets

Compose secret را به‌صورت فایل زیر `/run/secrets` در اختیار همان service قرار می‌دهد. برنامه فعلی `AddKeyPerFile` ندارد؛ بنابراین entrypoint زیر مقدار فایل‌ها را فقط هنگام شروع process به Environment Variable تبدیل می‌کند.

فایل‌ها را خارج از repository ایجاد کنید:

```text
/etc/makanapp/secrets/database_connection_string
/etc/makanapp/secrets/identity_security_key
/etc/makanapp/secrets/melipayamak_otp_service_token
/etc/makanapp/secrets/mfa_certificate_password
```

Compose:

```yaml
services:
  makanapp-api:
    image: registry.example.com/makanapp-api:<IMMUTABLE_RELEASE_TAG>
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: "8080"
      Sms__Provider: MeliPayamakOtp
      Sms__MeliPayamakOtp__BaseUrl: https://console.melipayamak.com
      Sms__MeliPayamakOtp__TimeoutSeconds: "10"
      Storage__RootPath: /data/storage
      AllowedHosts: api.example.test
      Cors__AllowedOrigins__0: https://app.example.test
      ReverseProxy__Enabled: "true"
      ReverseProxy__KnownProxies__0: <TRUSTED_PROXY_IP>
      Hosting__RequireHttps: "true"
      Hosting__AllowInsecureHttp: "false"
      Database__AllowUntrustedServerCertificate: "false"
      Swagger__Enabled: "false"
      Messaging__ContentSafetyProvider: Internal
      Otp__Enabled: "true"
      Otp__AllowMockAuthentication: "false"
      BackofficeSecurity__KeyRingPath: /data/mfa-keys
      BackofficeSecurity__CertificatePath: /run/secrets/makan-mfa.pfx
    secrets:
      - database_connection_string
      - identity_security_key
      - melipayamak_otp_service_token
      - mfa_certificate_password
    volumes:
      - /var/lib/makanapp/storage:/data/storage
      - /var/lib/makanapp/mfa-keys:/data/mfa-keys
      - /etc/makanapp/mfa-protection.pfx:/run/secrets/makan-mfa.pfx:ro
    entrypoint:
      - /bin/sh
      - -c
      - |
        export ConnectionStrings__MakanDatabase="$$(cat /run/secrets/database_connection_string)"
        export Identity__SecurityKey="$$(cat /run/secrets/identity_security_key)"
        export Sms__MeliPayamakOtp__ServiceToken="$$(cat /run/secrets/melipayamak_otp_service_token)"
        export BackofficeSecurity__CertificatePassword="$$(cat /run/secrets/mfa_certificate_password)"
        exec dotnet MakanApp.Api.dll
    ports:
      - "127.0.0.1:5080:8080"

secrets:
  database_connection_string:
    file: /etc/makanapp/secrets/database_connection_string
  identity_security_key:
    file: /etc/makanapp/secrets/identity_security_key
  melipayamak_otp_service_token:
    file: /etc/makanapp/secrets/melipayamak_otp_service_token
  mfa_certificate_password:
    file: /etc/makanapp/secrets/mfa_certificate_password
```

نکات:

- `$$` برای جلوگیری از interpolation زودهنگام Compose استفاده شده است.
- secretها داخل image یا Compose file قرار ندارند.
- هیچ command نباید مقدار secret را `echo` یا log کند.
- Compose standalone فایل secret را از host mount می‌کند؛ permission و امنیت host همچنان مهم است.
- برای orchestratorهای production از secret manager همان پلتفرم استفاده کنید.
- مجوز UID/GID روی volumeها و خواندن فایل‌های secret را مثل بخش ۶ بررسی کنید. Placeholder IP proxy باید جایگزین شود؛ trust عمومی یا مقدار نمونه مجاز نیست. هر نمونهٔ Linux/Docker دستورالعمل بررسی‌نشدهٔ محیط است، نه ادعای deployment موفق.

## 8. SQL Server در سرور و Docker

### خارج از Docker

`ConnectionStrings:MakanDatabase` باید به SQL Server واقعی production اشاره کند. `Server=.` فقط وقتی درست است که SQL Server روی همان Windows host و در context قابل‌دسترسی برنامه اجرا شود.

### داخل Docker

داخل container، `localhost` یا `.` به خود container اشاره می‌کند. اگر SQL Server service دیگری در همان Compose network است، از نام service استفاده کنید:

```text
Server=sqlserver,1433;Database=MakanApp;User Id=<USER>;Password=<PASSWORD>;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True
```

این رشته فقط template است. certificate، authentication و firewall باید متناسب با زیرساخت واقعی تنظیم شوند. password داخل Compose یا Git قرار نگیرد.

اگر SQL Server روی host اجرا می‌شود، آدرس آن باید از داخل container قابل resolve و دسترسی باشد؛ `Server=.` مناسب نیست.

## 9. Migration

MakanApp migration را در startup خودکار اجرا نمی‌کند. این تصمیم باید حفظ شود.

قبل از جابه‌جایی traffic:

1. از دیتابیس backup بگیرید.
2. connection string مقصد را صریح تنظیم کنید.
3. migrationها را از یک محیط کنترل‌شده دارای .NET SDK اجرا کنید.
4. نتیجه را بررسی کنید.
5. سپس نسخه جدید API را فعال کنید.

فرمان repository:

```powershell
$env:ConnectionStrings__MakanDatabase = '<TARGET_SQL_SERVER_CONNECTION_STRING>'
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj `
  --startup-project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj
```

Migration را داخل startup عادی container یا IIS قرار ندهید؛ اجرای هم‌زمان چند instance می‌تواند عملیات schema را مبهم و پرخطر کند.

شاهد تاریخی GAP-02 شامل ۳۵ Migration تا AddOperationalPersonGuardianOnboarding بود. اکنون 48 migration تا 20261015091336_EnforceApprovedMessengerChildSafety داریم؛ وجود آن‌ها در سورس یا تست LocalDB به معنی اعمال روی SQL هدف نیست. این همگام‌سازی docs Migration تازه نمی‌خواهد. اگر اسکریپت قبلی روی سرور اجرا شده، history را بررسی و تغییر تازهٔ schema فرض نکنید.

برای artifact قابل بازبینی، از ریشه مخزن و SDK سازگار SQL idempotent بسازید؛ این فرمان به‌خودی‌خود آن را روی سرور اجرا نمی‌کند:

```powershell
& ./tools/production/export-migration-sql.ps1 -OutputPath .artifacts/deployment/migrations-idempotent-safe.sql
```

ابزار خروجی موجود را overwrite نمی‌کند؛ نام تازه انتخاب کنید. فایل خام قدیمی در backfill ستون‌های Visibility/GroupHistoryPolicy/MemberCapabilities خطای compile 207 داشت؛ فایل اصلاح‌شده با کامپایل مؤخر، history guard و rollback تراکنش فقط پس از Backup/بازبینی اجرا شود. دستور آزمون مستقل و SHA در [runbook IIS](production/DEPLOYMENT_IIS.md) آمده است. runtime principal به DDL یا db_owner نیاز ندارد. EF از MakanDbContextFactory و اتصال design-time می‌خواند، نه فایل خصوصی API؛ startup پروژهٔ Infrastructure و manifest ریشه استفاده شود. تولید SQL اتصال/اجرای سرور نیست.

برای rollback، ابتدا سازگاری نسخه قبلی کد با schema فعلی را بررسی کنید. برگشت image/binary به معنی برگشت دیتابیس نیست؛ migration معکوس یا restore داده به تأیید عملیاتی و ارزیابی از دست‌رفتن داده نیاز دارد. SQL Server و مسیر Storage باید هر دو backup و روش restore آزموده‌شده داشته باشند؛ backup فقط فایل‌های publish کافی نیست.

## 10. بررسی بعد از استقرار

### بررسی configuration بدون نمایش secret

Windows PowerShell:

```powershell
$token = [Environment]::GetEnvironmentVariable(
  "Sms__MeliPayamakOtp__ServiceToken",
  "Machine")

"ServiceToken configured: $(-not [string]::IsNullOrWhiteSpace($token))"
Remove-Variable token
```

Linux EnvironmentFile:

```bash
sudo test -s /etc/makanapp/makanapp.env \
  && echo "Environment file exists and is not empty"
```

این بررسی‌ها نباید مقدار secret را چاپ کنند.

### health و smoke test

1. محیط انتخاب‌شده و OTP جفت صحیح را بدون چاپ secret بررسی کنید؛ Test و Production را روی داده مشترک اجرا نکنید.
2. GET /health/live برای process و GET /health/ready برای config/ring/SQL/storage باید 200 شوند؛ readiness provider خارجی یا کامل‌بودن schema را اثبات نمی‌کند.
3. applied migrationهای مقصد را با handoff نسخه تطبیق دهید؛ هیچ migration خودکار startup وجود ندارد.
4. در Production/Real فقط با شمارهٔ مجاز و تأیید صریح هزینه، OTP واقعی بخواهید؛ در Test/Mock SMS نباید فراخوانی شود.
5. پاسخ challenge برابر202 و بدون کد/credential است؛ سپس verify، me و دسترسی مجاز را بررسی کنید. HTTP401 بدون نشست برای مسیر حفاظت‌شده انتظار می‌رود.
6. با permissionهای مجاز، عملیات health/jobs/notifications، سن صف و snapshot stale را بررسی کنید؛ 200 مدیریتی به‌تنهایی healthy همه dependencyها نیست.
7. receiver واقعی Push/AI، recycle/reboot، backup/restore و alert تحویل‌شده gateهای جدا با شاهد محیط هدف هستند؛ از این smoke نتیجه نمی‌شوند.

### قرارداد فرانت‌اند و تست‌های عملیاتی پس از انتشار

۱. `/swagger/index.html` و `/openapi/v1.json` در Production/Staging با تنظیم پیش‌فرض باید404 بدهند. شمارش قدیمی۱۴۱ عملیات متعلق به snapshot اولیه سند است، نه API فعلی.

۲. preflight از origin واقعی فرانت‌اند را روی **HTTPS نهایی** بررسی کنید. در PowerShell برای جلوگیری از alias، `curl.exe` و در Linux `curl` استفاده شود:

```powershell
curl.exe -i -X OPTIONS 'https://api.example.com/api/v1/files' `
  -H 'Origin: https://app.example.com' `
  -H 'Access-Control-Request-Method: POST' `
  -H 'Access-Control-Request-Headers: authorization,content-type'
```

انتظار برای origin موجود در allowlist:204، `Access-Control-Allow-Origin` برابر همان origin، method/header مجاز و نبود `Access-Control-Allow-Credentials`. origin خارج allowlist نباید اجازه بگیرد. درخواست محافظت‌شده بدون token همچنان401 است؛ رفتار مرورگر/proxy جدا بررسی شود.

۳. با حساب و سازمان آزمایشی از پیش مجاز، workspace نقش متناظر را انتخاب و dashboard همان نقش را باز کنید. Parent بدون فرزند انتخاب‌شده در dashboard پاسخ200 خالی می‌دهد ولی `/reports/parent/...` قبل انتخاب فرزند404 با `CHILD_CONTEXT_NOT_FOUND` دارد. نمره منتشرنشده و یادداشت خصوصی نباید در Student/Parent دیده شوند؛ نقش اشتباه403 است.

۴. با فایل کوچک مجاز، upload، metadata و download را بررسی کنید؛ سپس پس از restart/redeploy همان فایل باید قابل دانلود باشد. مسیر فیزیکی را public/static نکنید؛ مجوز منبع از API اعمال می‌شود. محدودیت body در IIS/proxy باید سربار multipart را هم پوشش دهد؛ حد نهایی اندازه فایل در برنامه10MiB پیش‌فرض است.

۵. با دو نشست آزمایشی مجاز، ارسال پیام، دریافت event، قطع/اتصال و بازیابی از Delta را بررسی کنید. SignalR فقط notification است؛ موفقیت dispatch شرط پایدارشدن پیام نیست. خطا یا backlog `MessagingRealtimeOutbox` را از log/SQL مجاز پیگیری کنید؛ endpoint مانیتورینگ عمومی outbox وجود ندارد.

۶. در ورود واقعی، restart با کلید ثابت را روی محیط آزمایشی بررسی کنید؛ code رشته است و ارقام فارسی/عربی normalize می‌شوند. تست‌های خودکار provider از HTTP stub استفاده می‌کنند و موفقیت آن‌ها اثبات اعتبار token، اعتبار حساب یا تحویل SMS واقعی نیست.

## 11. Rotation و incident response

اگر `ServiceToken` افشا شد:

1. token را در پنل ملی‌پیامک revoke/rotate کنید.
2. مقدار جدید را فقط در محل secret سرور جایگزین کنید.
3. process برنامه را restart کنید.
4. logها و Git history را برای افشای احتمالی بررسی کنید.
5. token قدیمی را دوباره فعال نکنید.

اگر `Identity:SecurityKey` تغییر کند، hash نشست‌های موجود دیگر معتبر نخواهد بود و کاربران احتمالاً باید دوباره وارد شوند. این کلید را هنگام deploy عادی تصادفی تعویض نکنید.

## 12. خطاهای رایج

| نشانه | علت محتمل | اقدام |
|---|---|---|
| خطای `ServiceToken must be supplied` | token به process نرسیده یا environment اشتباه است | منبع تنظیم و restart process را بررسی کنید |
| خطای `Identity:SecurityKey must be supplied` با provider واقعی | کلید پایدار HMAC تنظیم نشده است | یک کلید ثابت حداقل ۳۲ بایتی در secret store همان محیط تنظیم و process را restart کنید |
| provider برابر `Unavailable` است | `Sms:Provider` در Production تنظیم نشده | مقدار را `MeliPayamakOtp` قرار دهید |
| SMS می‌رسد ولی کد بعد از restart همیشه invalid است | challenge با `Identity:SecurityKey` قبلی hash شده است | کلید پایدار تنظیم و OTP جدید درخواست کنید؛ challenge قدیمی قابل بازیابی نیست |
| برنامه بعد از deploy sessionها را رد می‌کند | `Identity:SecurityKey` تغییر کرده است | از کلید پایدار همان محیط استفاده کنید |
| `Invalid object name` | migration روی database runtime اعمال نشده | connection string migration/runtime را مقایسه کنید |
| فایل‌های upload بعد از recreate container حذف می‌شوند | storage volume ندارد | مسیر `Storage:RootPath` را روی volume پایدار قرار دهید |
| SQL داخل container در دسترس نیست | connection string هنوز `Server=.` یا `localhost` است | hostname قابل‌دسترسی SQL Server را استفاده کنید |
| تغییر JSON اثر ندارد | environment اشتباه یا process restart نشده است | `ASPNETCORE_ENVIRONMENT` و restart را بررسی کنید |
| `SMS_PROVIDER_REJECTED` با422 | provider درخواست را نپذیرفته | شماره و وضعیت سرویس را بررسی کنید؛ ارسال کور تکرار نشود |
| `SMS_RATE_LIMITED` با429 | محدودیت provider، مستقل از محدودیت محلی OTP | فاصله مجاز و وضعیت سرویس را بررسی کنید |
| `SMS_PROVIDER_AUTHENTICATION_FAILED` با503 | credential سرویس پیامک رد شده | secret سمت backend بررسی شود؛ این خطا logout کاربر نیست |
| upload با `FILE_STORAGE_FAILED` و503 | RootPath در Production خالی است یا storage آماده نیست | مسیر مطلق پایدار و permission سرویس را تنظیم کنید |
| مسیر نسبی storage یا access denied | LocalFileStorage نمی‌تواند root را آماده کند | مسیر مطلق و ACL/UID؛ liveness کافی نیست، readiness و log امن را بررسی کنید |
| REST کار می‌کند ولی browser SignalR وصل نمی‌شود | query-token WebSocket/SSE یا cookie همراه CORS wildcard | LongPolling دارای header و withCredentials:false در origin واقعی بررسی شود |
| داشبورد والد خالی ولی گزارش والد404 است | child انتخاب نشده | ابتدا Guardian child context انتخاب و هر دو query تازه شوند |
| پس از تغییر نقش، گزارش403 است | activeRole با مسیر گزارش یکسان نیست | workspace/role جاری و cache context را بررسی کنید |
| همه OTPها پشت proxy به محدودیت IP می‌خورند | RemoteIpAddress همان proxy است | trust و forwarded headers متناسب محیط بررسی شود |

## 13. چک‌لیست نهایی

- [ ] build و test قبل از publish موفق بوده‌اند.
- [ ] artifact هیچ secret واقعی ندارد.
- [ ] اگر فایل واقعی Production به خروجی انتقال اپراتور اضافه شده، آن خروجی محرمانه و غیرقابل‌اشتراک است؛ داخل image و Git قرار نمی‌گیرد.
- [ ] environment برنامه `Production` است.
- [ ] connection string فقط به database مقصد مورد تأیید اشاره می‌کند.
- [ ] `Identity:SecurityKey` پایدار و حداقل ۳۲ بایت است.
- [ ] `Sms:Provider=MeliPayamakOtp` تنظیم شده است.
- [ ] `ServiceToken` خارج از Git و image قرار دارد.
- [ ] فایل secret یا appsettings server-only دارای ACL محدود است.
- [ ] migrationها جداگانه و کنترل‌شده اعمال شده‌اند.
- [ ] storage روی مسیر/volume پایدار قرار دارد.
- [ ] TLS در reverse proxy/IIS تنظیم شده است.
- [ ] اگر انتشار HTTP با تأیید مالک انجام می‌شود، دو استثنا و خطر plaintext ثبت و gate امن TLS/GAP-02 باز مانده است؛ HTTP را امن اعلام نکرده‌ایم.
- [ ] key ring پایدار MFA و محافظ DPAPI/certificate، ACL و identity قبلی حفظ شده‌اند؛ DB Backup به‌تنهایی کافی فرض نشده است.
- [ ] health و OTP smoke test بعد از استقرار انجام شده‌اند.
- [ ] logها حاوی OTP، token یا connection string نیستند.
- [ ] preflight و REST واقعی با Bearer و بدون cookie بررسی شده‌اند.
- [ ] LongPolling، reconnect و Delta در مرورگر محیط هدف بررسی شده‌اند.
- [ ] Storage پس از restart باقی می‌ماند و backup آن با دیتابیس هماهنگ است.
- [ ] داشبوردها/گزارش‌ها با نقش و child context معتبر smoke test شده‌اند.
- [ ] tag نسخه ثابت، artifact قبلی و برنامه rollback سازگار موجود است.

## 14. منابع رسمی

- [ASP.NET Core configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0)
- [Host ASP.NET Core on Windows with IIS](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-9.0)
- [Host ASP.NET Core on Linux with Nginx](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-9.0)
- [.NET container images](https://learn.microsoft.com/en-us/dotnet/core/docker/container-images)
- [Docker Compose secrets](https://docs.docker.com/compose/how-tos/use-secrets/)


## تغییرات عملیاتی Messenger و Push — بازبینی 2026-10-23

مبنای این بخش `main@5885d11` در `2026-10-23` است. کاتالوگ جاری ۳۲۵ عملیات، ۵۷ Controller، ۵۵۲ schema، ۱۰۷ جدول و ۴۸ migration دارد. آماده‌بودن schema با پیکربندی یا تحویل واقعی provider متفاوت است؛ آخرین migration `20261017033804_AddConversationChecklists` است. [وضعیت جاری پروژه](CURRENT_PROJECT_STATUS.md) و Handoffهای [P1](messaging/MESSENGER_P1_FRONTEND_HANDOFF.md) و [P2](messaging/MESSENGER_P2_FRONTEND_HANDOFF.md) قراردادهای مصرف را مشخص می‌کنند.

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

`P3_S_INFRA_REQUIRED`، `LIVEKIT_REJOIN_SECURITY_GATE=NOT_TESTED` و `P3_PRODUCTION_MEDIA_APPROVAL=NOT_GRANTED` وضعیت فعلی‌اند. رسانهٔ صوتی/تصویری/Live محصول هنوز deployment پذیرفته‌شده ندارد. spec مصوب LiveKit/SFU/TURN را با runtime نصب‌شده اشتباه نگیرید؛ جزئیات Linux lab، DNS/TLS، TURN و دو client در [گزارش S](messaging/MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_REPORT.md) است.

بعد از انتشار Backend، روی حساب/سازمان آزمایشی از پیش مجاز، علاوه بر core قبلی این موارد را بررسی کنید: تأیید self/سن طبق policy، Direct و Peer Read مجاز/Hidden، expiry و reconnect Typing/Presence، registration/revoke Push، تأخیر و cancel/dispatch intent، topic و محدودیت عضو، Poll/Quiz و دو حالت Checklist. این‌ها دستور smoke هستند؛ در این مرحله روی سرور واقعی اجرا نشده‌اند. فایل خصوصی Production، کلیدهای OTP/MFA و موارد استثنای HTTP/SQL قبلی بدون تصمیم تازه تغییر نکنند.
