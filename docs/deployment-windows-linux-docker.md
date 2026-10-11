# راهنمای استقرار MakanApp در Windows، Linux و Docker

این سند روش استقرار backend فعلی MakanApp و تأمین امن تنظیمات محرمانه را توضیح می‌دهد. مثال‌ها برای `.NET 9`، `SQL Server` و provider واقعی `MeliPayamakOtp` نوشته شده‌اند.

> هیچ `ServiceToken`، connection string، password یا `Identity:SecurityKey` واقعی نباید داخل repository، image، Dockerfile، Compose file یا artifact مشترک قرار بگیرد.

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

Swagger UI در `/swagger` و سند OpenAPI در `/openapi/v1.json` در Production نیز بدون ورود در دسترس هستند. احراز هویت و مجوز endpointهای API همچنان اعمال می‌شوند.

CORS برای همه originها، متدها و headerها باز است و تنظیم جداگانه‌ای در appsettings لازم ندارد. درخواست‌های مرورگر باید Bearer token را در header بفرستند و از `credentials: "include"` استفاده نکنند. برای کلاینت مرورگر SignalR در اتصال cross-origin، مقدار `withCredentials: false` لازم است؛ این حالت برای استقرار فعلی روی یک IIS در نظر گرفته شده و وابستگی به cookie برای sticky session ندارد.

## 2. فایل server-only با appsettings.Production.json

اگر نگه‌داری تنظیمات داخل فایل برای عملیات سرور ساده‌تر است، فایل زیر را **فقط روی سرور و بعد از publish** کنار `MakanApp.Api.dll` بسازید:

```json
{
  "ConnectionStrings": {
    "MakanDatabase": "<PRODUCTION_SQL_SERVER_CONNECTION_STRING>"
  },
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
  }
}
```

قواعد این فایل:

- مقدارهای داخل `<...>` حتماً روی سرور جایگزین شوند.
- فایل داخل repository ساخته، commit یا push نشود.
- فایل همراه Docker image ساخته نشود.
- فقط account اجرای برنامه و Administrator/root به آن دسترسی خواندن داشته باشند.
- deploy بعدی نباید آن را overwrite یا حذف کند.
- برنامه باید با environment برابر `Production` اجرا و بعد از تغییر فایل restart شود.

این فایل plaintext است و secret vault محسوب نمی‌شود؛ امنیت آن به ACL سیستم‌عامل و محدودکردن دسترسی سرور وابسته است.

## 3. استقرار روی Windows Server با IIS

### 3.1 پیش‌نیاز

1. .NET 9 Hosting Bundle را روی Windows Server نصب کنید.
2. یک Application Pool اختصاصی، برای مثال `MakanApp`, بسازید.
3. گزینه `.NET CLR Version` در Application Pool را روی `No Managed Code` قرار دهید.
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

محتوای `artifacts\MakanApp.Api` را به `C:\Sites\MakanApp` منتقل کنید. فایل server-only را بعد از انتقال بسازید یا از نسخه محافظت‌شده قبلی نگه دارید.

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

قبل از اجرای فرمان‌ها، نام دقیق Application Pool و مسیرها را بررسی کنید. دسترسی write فقط برای مسیر storage لازم است؛ فایل تنظیمات فقط read باشد.

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
Identity__SecurityKey="<AT_LEAST_32_UTF8_BYTES_RANDOM_SECRET>"
Sms__Provider=MeliPayamakOtp
Sms__MeliPayamakOtp__BaseUrl=https://console.melipayamak.com
Sms__MeliPayamakOtp__ServiceToken="<MELIPAYAMAK_OTP_SERVICE_TOKEN>"
Sms__MeliPayamakOtp__TimeoutSeconds=10
Storage__RootPath=/var/lib/makanapp/storage
```

Permission فایل:

```bash
sudo chown root:root /etc/makanapp/makanapp.env
sudo chmod 600 /etc/makanapp/makanapp.env
```

فایل environment را داخل repository یا artifact قرار ندهید.

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
appsettings.Production.json
**/appsettings.Production.json
secrets
**/secrets
*.env
.env*
```

هیچ secret نباید با `ARG` یا `ENV` داخل Dockerfile قرار بگیرد؛ این مقادیر می‌توانند در metadata یا history image باقی بمانند.

## 6. Docker Compose با appsettings server-only

این روش با کد فعلی مستقیماً کار می‌کند. فایل تنظیمات خارج از repository روی host نگه‌داری و به‌صورت read-only داخل container mount می‌شود.

مثال Linux host:

```yaml
services:
  makanapp-api:
    image: registry.example.com/makanapp-api:latest
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: "8080"
    ports:
      - "127.0.0.1:5080:8080"
    volumes:
      - /etc/makanapp/appsettings.Production.json:/app/appsettings.Production.json:ro
      - /var/lib/makanapp/storage:/data/storage
```

در فایل mountشده مقدار storage باید چنین باشد:

```json
{
  "Storage": {
    "RootPath": "/data/storage"
  }
}
```

روی Windows host با Linux container، source مسیر bind mount می‌تواند برای مثال این باشد:

```yaml
volumes:
  - C:/MakanApp/config/appsettings.Production.json:/app/appsettings.Production.json:ro
  - C:/MakanApp/data/storage:/data/storage
```

دسترسی Docker daemon ذاتاً حساس است؛ فقط administratorهای مجاز باید به host، Compose project و فایل تنظیمات دسترسی داشته باشند.
همچنین UID کاربر non-root داخل container باید اجازه خواندن فایل تنظیمات و نوشتن در مسیر storage را داشته باشد.

## 7. Docker Compose با Docker Secrets

Compose secret را به‌صورت فایل زیر `/run/secrets` در اختیار همان service قرار می‌دهد. برنامه فعلی `AddKeyPerFile` ندارد؛ بنابراین entrypoint زیر مقدار فایل‌ها را فقط هنگام شروع process به Environment Variable تبدیل می‌کند.

فایل‌ها را خارج از repository ایجاد کنید:

```text
/etc/makanapp/secrets/database_connection_string
/etc/makanapp/secrets/identity_security_key
/etc/makanapp/secrets/melipayamak_otp_service_token
```

Compose:

```yaml
services:
  makanapp-api:
    image: registry.example.com/makanapp-api:latest
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_HTTP_PORTS: "8080"
      Sms__Provider: MeliPayamakOtp
      Sms__MeliPayamakOtp__BaseUrl: https://console.melipayamak.com
      Sms__MeliPayamakOtp__TimeoutSeconds: "10"
      Storage__RootPath: /data/storage
    secrets:
      - database_connection_string
      - identity_security_key
      - melipayamak_otp_service_token
    volumes:
      - /var/lib/makanapp/storage:/data/storage
    entrypoint:
      - /bin/sh
      - -c
      - |
        export ConnectionStrings__MakanDatabase="$$(cat /run/secrets/database_connection_string)"
        export Identity__SecurityKey="$$(cat /run/secrets/identity_security_key)"
        export Sms__MeliPayamakOtp__ServiceToken="$$(cat /run/secrets/melipayamak_otp_service_token)"
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
```

نکات:

- `$$` برای جلوگیری از interpolation زودهنگام Compose استفاده شده است.
- secretها داخل image یا Compose file قرار ندارند.
- هیچ command نباید مقدار secret را `echo` یا log کند.
- Compose standalone فایل secret را از host mount می‌کند؛ permission و امنیت host همچنان مهم است.
- برای orchestratorهای production از secret manager همان پلتفرم استفاده کنید.

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

1. `GET /health` را بررسی کنید.
2. log startup را از نظر خطای connection string، `Identity:SecurityKey` یا `ServiceToken` بررسی کنید.
3. migration مقصد را تأیید کنید؛ health فعلی تضمین‌کننده سلامت SQL Server نیست.
4. یک درخواست OTP فقط با شماره آزمایشی مجاز ارسال کنید.
5. پاسخ باید `202 Accepted` و شامل `challengeId`، `expiresAtUtc` و `resendAvailableAtUtc` باشد.
6. پاسخ نباید OTP، `ServiceToken` یا URL provider را نمایش دهد.
7. کد پیامک‌شده را از endpoint verify بررسی کنید و بعد از تست آن را منتشر یا log نکنید.

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

## 13. چک‌لیست نهایی

- [ ] build و test قبل از publish موفق بوده‌اند.
- [ ] artifact هیچ secret واقعی ندارد.
- [ ] environment برنامه `Production` است.
- [ ] connection string فقط به database مقصد مورد تأیید اشاره می‌کند.
- [ ] `Identity:SecurityKey` پایدار و حداقل ۳۲ بایت است.
- [ ] `Sms:Provider=MeliPayamakOtp` تنظیم شده است.
- [ ] `ServiceToken` خارج از Git و image قرار دارد.
- [ ] فایل secret یا appsettings server-only دارای ACL محدود است.
- [ ] migrationها جداگانه و کنترل‌شده اعمال شده‌اند.
- [ ] storage روی مسیر/volume پایدار قرار دارد.
- [ ] TLS در reverse proxy/IIS تنظیم شده است.
- [ ] health و OTP smoke test بعد از استقرار انجام شده‌اند.
- [ ] logها حاوی OTP، token یا connection string نیستند.

## 14. منابع رسمی

- [ASP.NET Core configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0)
- [Host ASP.NET Core on Windows with IIS](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-9.0)
- [Host ASP.NET Core on Linux with Nginx](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-9.0)
- [.NET container images](https://learn.microsoft.com/en-us/dotnet/core/docker/container-images)
- [Docker Compose secrets](https://docs.docker.com/compose/how-tos/use-secrets/)
