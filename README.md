# ماکان‌اپ — MakanApp Backend

این مخزن فقط Backend ماکان را نگه می‌دارد. ساختار فعلی یک Modular Monolith با Onion Architecture روی ASP.NET Core و .NET 9 است. Persistence بر Entity Framework Core 9 و Microsoft SQL Server بنا شده است. Frontend در این workspace وجود ندارد.

اسناد مرجع در `docs/project-context` و قواعد کار در `AGENTS.md` قرار دارند.

## راهنمای استفاده از بک‌اند و API

- [راهنمای جامع پروژه و بک‌اند](docs/backend-api-guide.md) — [HTML](docs/backend-api-guide.html) / [PDF](docs/backend-api-guide.pdf): شناخت مدل و معماری، راه‌اندازی، نقشه سرویس‌ها، جریان واقعی کد، تراکنش، امنیت، تست و ادامه توسعه.
- [مرجع جاری Endpointها و قراردادها](docs/backend-api-reference.md) — [HTML](docs/backend-api-reference.html) / [PDF](docs/backend-api-reference.pdf): ۳۲۵ عملیات در ۵۷ Controller، پارامتر/بدنه/پاسخ واقعی، metadata احراز هویت و ۵۵۱ schema استخراج‌شده از OpenAPI.
- [قرارداد فرانت‌اند و بک‌اند برای React و React Native](docs/frontend-contract/README.md): سند مستقل HTML و PDF شامل قرارداد API، stateها، مجوزها، سناریوها، کاتالوگ صفحات و شکاف‌های UX با بک‌اند فعلی.
- [راهنمای استقرار Windows، Linux و Docker](docs/deployment-windows-linux-docker.md): تنظیمات Production، OTP ملی‌پیامک، storage پایدار، CORS/SignalR، migration، smoke test و rollback.
- [قرارداد داشبوردهای نقش‌محور STEP 11A](docs/architecture/STEP_11A_ROLE_BASED_DASHBOARDS.md): چهار endpoint موجود، مجوز سروری، بافت فرزند، نمره نسخه منتشرشده، حضور رسمی، حالت خالی و مرز مستقل از AI/Commerce؛ بازبینی `2026-10-15` بدون migration.

بازبینی `2026-10-18` از `main@ef67624`؛ **325 عملیات HTTP در 57 Controller و 290 مسیر؛ 551 schema، 107 جدول و 48 migration**. Health/OpenAPI/Hub در شمار عملیات Controller نیستند. اعداد از TestServer بدون worker و مدل طراحی EF آمده‌اند؛ هیچ provider/دیتابیس واقعی فراخوانی نشده است. [شاخص وضعیت پروژه و gateها](docs/CURRENT_PROJECT_STATUS.md).

Markdownها منبع دو کتاب و ساختار دیتابیس‌اند؛ HTML/PDF با ابزار `tools/docs/build_backend_docs.py` بازتولید می‌شوند. [فهرست و وضعیت نگه‌داری همه اسناد](docs/documentation-inventory.csv) فرق سند جاری، قرارداد مرحله‌ای، گزارش تاریخی، مرجع اصلی محصول و فایل dirty خارج تحویل را مشخص می‌کند. اعداد تست/commit گزارش‌های قدیمی تاریخچهٔ همان اجرا هستند و نباید به‌جای نتیجهٔ جاری خوانده شوند.

## وضعیت جاری قابلیت‌ها و استقرار

| حوزه | وضعیت کد و مرز عملیاتی |
|---|---|
| Identity / OTP | نشست access/refresh و مدیریت نشست؛ adapter واقعی OTP ملی‌پیامک؛ تست خودکار SMS واقعی نمی‌فرستد |
| Organization / Guardian | onboarding پرونده، دعوت اتصال حساب با رضایت، lifecycle سرپرستی؛ بدون merge یا ساخت ضمنی حساب |
| Academic / Assessment | دوره، درس، کلاس، ثبت‌نام، جلسات/حضور، تکلیف، آزمون و انتشار/اصلاح نمره |
| Messaging | Contacts/privacy، Direct/group/channel، نسخه پیام، sync و سیاست مصوب Child Safety؛ Guardian/Manager حق مرور Direct خصوصی ندارند |
| Reports / Notification | داشبورد، گزارش، export غیرهم‌زمان، اعلان/outbox/preferences؛ Web Push/VAPID و FCM واقعی در کد، runtime دستگاه pending؛ SMS اعلان unavailable |
| Commerce / Backoffice | Plan/Subscription/Order/Payment lifecycle و IAM/operations؛ gateway مالی واقعی unavailable؛ MFA/step-up واقعی TOTP موجود است |
| AI | ۱۲ API سمت Backend؛ پیش‌فرض غیرفعال، source lifecycle RAG deferred و real Backend↔AI E2E از شواهد commit‌شده بسته نشده است |
| Production | آماده‌سازی کد با آمادگی عملیاتی سرور یکی نیست؛ [ماتریس جاری](docs/production/PRODUCTION_READINESS.md) و gateهای باز ملاک‌اند |

انتشار فعلی مالک **Production روی HTTP** است؛ فایل Staging حذف شده. `appsettings.Production.json` واقعی محلی/ignored و نمونهٔ بدون secret آن `appsettings.Production.json.example` است. دو استثنای تأییدشدهٔ `Hosting:AllowInsecureHttp` و `Database:AllowUntrustedServerCertificate` فقط در فایل خصوصی این انتشار فعال‌اند؛ پیش‌فرض پایه امن است. HTTP رمزگذاری ندارد و این استثناها gate امن GAP-02 را نمی‌بندند. [مراحل دقیق سرور IIS](docs/production/DEPLOYMENT_IIS.md) شامل حفظ Storage، کلید Identity و key ring پایدار MFA است؛ هیچ اتصال/تغییر سرور با این اسناد ادعا نمی‌شود.

بخش‌های معرفی مراحل توسعه در ادامه سابقهٔ تکمیل همان مرحله‌اند؛ برای قرارداد جاری از مرجع API، مدل دیتابیس و اسناد عملیاتی بالا استفاده کنید. PDFهای اصلی محصول در project-context دست‌نخورده و فقط مرجع نیازمندی‌اند؛ MySQL قدیمی همچنان منسوخ است.

## تغییرات جاری Messenger

P0/P1/P2 تا Checklist در کد موجودند: Peer Read، Typing/Presence، Folder/Labels، Invite/Join، Topic، Scheduled/Silent، Poll/Quiz و Checklist. [وضعیت یکپارچه و منابع](docs/messaging/CURRENT_MESSENGER_STATUS.md). P1_PUSH_RUNTIME_PENDING و PENDING_SCALE_INFRA برقرارند؛ رسانه P3 در انتظار زیرساخت S است و runtime پذیرفته‌شده ندارد. اختلاف قدیمی Enum/OpenAPI در snapshot جاری دیده نشد.

## مسیرها

~~~text
Workspace:      D:\Project\MakanApp\MakanApp-Backend
Solution:       D:\Project\MakanApp\MakanApp-Backend\MakanApp.sln
API project:    D:\Project\MakanApp\MakanApp-Backend\MakanApp.Api\MakanApp.Api.csproj
Domain project: D:\Project\MakanApp\MakanApp-Backend\MakanApp.Domain\MakanApp.Domain.csproj
~~~

## ساختار Onion

~~~text
MakanApp.sln
MakanApp.Domain/
  Common/DomainAssembly.cs
MakanApp.Application/
  Common/ApplicationAssembly.cs
MakanApp.Infrastructure/
  Persistence/MakanDbContext.cs
  DependencyInjection.cs
MakanApp.Api/
  Controllers/
  Errors/GlobalExceptionHandler.cs
  Properties/launchSettings.json
  Program.cs
  appsettings.json
  appsettings.Development.json
  MakanApp.Api.http
tests/
  MakanApp.UnitTests/
  MakanApp.IntegrationTests/
  MakanApp.ArchitectureTests/
docs/project-context/
~~~

مسئولیت لایه‌ها:

- `MakanApp.Domain`: قوانین و مدل خالص دامنه؛ بدون وابستگی به framework یا لایه‌های بیرونی.
- `MakanApp.Application`: use caseها، DTOها و portهای موردنیاز؛ وابسته فقط به Domain.
- `MakanApp.Infrastructure`: EF Core، SQL Server و adapterهای بیرونی؛ وابسته به Application و Domain.
- `MakanApp.Api`: HTTP host، Controllerها، ProblemDetails، OpenAPI و composition root؛ وابسته به Application و Infrastructure.

جهت مجاز وابستگی:

~~~text
Api -> Infrastructure -> Application -> Domain
Api --------------------> Application
Infrastructure ----------------------> Domain
~~~

وابستگی معکوس مجاز نیست. Controllerها فقط در `MakanApp.Api/Controllers` قرار می‌گیرند و EF Core فقط در Infrastructure استفاده می‌شود.

## پیش‌نیازها

- Windows و Visual Studio 2022 یا VS Code با C# Dev Kit
- .NET SDK 9
- Microsoft SQL Server
- دسترسی به NuGet برای نخستین restore

تمام پروژه‌ها `net9.0` را هدف می‌گیرند. وجود SDK جدیدتر روی سیستم، TargetFramework پروژه را تغییر نمی‌دهد.

## SQL Server و EF Core

`MakanDbContext` در `MakanApp.Infrastructure/Persistence` قرار دارد و با `Microsoft.EntityFrameworkCore.SqlServer` در DI ثبت می‌شود. کلید اتصال:

~~~text
ConnectionStrings:MakanDatabase
~~~

`appsettings.Development.json` یک نمونه credential-free برای SQL Server محلی با Windows Authentication دارد. برای جایگزینی امن مقدار در توسعه:

~~~powershell
dotnet user-secrets set "ConnectionStrings:MakanDatabase" "Server=.;Database=MakanApp;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" --project MakanApp.Api/MakanApp.Api.csproj
~~~

در محیط‌های دیگر مقدار از secret store، environment یا فایل خصوصی دارای ACL تأمین می‌شود. ۴۸ Migration نسخه‌بندی‌شده داریم؛ آخرین شناسه `20261017033804_AddConversationChecklists` است. `Database.EnsureCreated` در برنامه استفاده نمی‌شود و migration هنگام startup اجرا نمی‌شود؛ اعمال آن عملیات کنترل‌شده و جداگانه است. [ساختار جاری دیتابیس](docs/database-current-structure.md) و ابزار SQL بررسی‌شده در [runbook IIS](docs/production/DEPLOYMENT_IIS.md) را ببینید؛ این تغییر مستندات Migration تازه نمی‌خواهد.

زیرساخت Order/Payment فعلاً provider واقعی ندارد. مقدار commit‌شده `Payments:Provider=Unavailable` است و secret پرداختی در appsettings نگهداری نمی‌شود. Integration Testها `RecordingPaymentGateway` داخلی را جایگزین می‌کنند و هیچ تماس مالی واقعی ندارند. جزئیات در `docs/architecture/STEP_13B_ORDER_AND_PAYMENT.md` ثبت شده است.

## API Foundation

- ASP.NET Core Controllers
- OpenAPI و Swagger UI در Development/Testing؛ در Production/Staging پیش‌فرض خاموش
- CORS allowlist صریح خارج توسعه؛ بدون cookie credentials؛ احراز هویت با Bearer token
- ProblemDetails و exception handling متمرکز
- HTTPS redirection مطابق `Hosting:RequireHttps`؛ غیرفعال‌سازی در Production فقط با استثنای صریح تأییدشده
- health checks
- DI composition در `MakanApp.Api/Program.cs`

| مسیر | محیط | کاربرد |
|---|---|---|
| `GET /health` و `/health/live` | همه محیط‌ها | فقط liveness |
| `GET /health/ready` | همه محیط‌ها | اتصال SQL و write/delete probe در Storage |
| `GET /openapi/v1.json` | Development/Testing یا فعال‌سازی صریح | سند OpenAPI |
| `/swagger` | Development/Testing یا فعال‌سازی صریح | Swagger UI؛ 404 در Production پیش‌فرض طبیعی است |

## Identity، OTP و پروفایل

مدل‌های `User`، `Person`، `UserCredential`، `OtpChallenge` و `UserSession` در schema با نام `identity` نگهداری می‌شوند. هویت User از شماره تلفن جدا است و نقش سازمانی در ثبت‌نام یا claim نشست قرار نمی‌گیرد.

| متد | مسیر | نیاز به ورود |
|---|---|---|
| `POST` | `/api/v1/auth/otp/challenges` | خیر |
| `POST` | `/api/v1/auth/otp/verify` | خیر |
| `POST` | `/api/v1/auth/logout` | بله |
| `GET` | `/api/v1/me` | بله |
| `PATCH` | `/api/v1/me/profile` | بله |

کد OTP از `IOtpDeliveryProvider` دریافت و بلافاصله با HMAC و salt محافظت می‌شود؛ plaintext آن در SQL Server ذخیره نمی‌شود. کد منقضی، یک‌بارمصرف و دارای محدودیت تلاش و ارسال مجدد است. هنگام verify باید `challengeId` همان درخواست همراه همان شماره ارسال شود؛ درخواست challenge جدید، challenge قبلی را supersede می‌کند. طول کد با `Identity:Otp:MinimumCodeLength` و `Identity:Otp:MaximumCodeLength` کنترل می‌شود و ارقام انگلیسی، فارسی یا عربی پیش از hash به ارقام ASCII تبدیل می‌شوند. نشست با Bearer token تصادفی کار می‌کند و فقط HMAC توکن در `identity.UserSessions` ذخیره می‌شود؛ logout همان نشست را revoke می‌کند.

انتخاب provider با کلید `Sms:Provider` انجام می‌شود. مقدار `Development` فقط در محیط‌های `Development` و `Testing` مجاز است؛ `DevelopmentOtpProvider` یک کد شش‌رقمی محلی می‌سازد، آن را در response یا log منتشر نمی‌کند و نگهداری موقت in-memory آن صرفاً برای توسعه و تست خودکار است. مقدار `Unavailable` درخواست را با خطای کنترل‌شده رد می‌کند. تست‌های خودکار هیچ پیامک پولی ارسال نمی‌کنند.

### کاربران تست و OTP توسعه

`Otp:Enabled` تنها کلید انتخاب OTP واقعی/Mock است. مقدار اولیهٔ فایل‌های base، Development و Production
`false` است؛ Mock علاوه بر آن به `Otp:AllowMockAuthentication=true` و محیط Development/Test/Testing نیاز دارد.
در Mock کد یا challenge واقعی ساخته نمی‌شود و Verify با هر کد واردشده از مسیر session/refresh معمول عبور می‌کند.
Production/Staging با OTP خاموش یا مجوز Mock، در startup رد می‌شوند؛ برای انتشار `Enabled=true` و
`AllowMockAuthentication=false` لازم است. `Identity:Otp:Mode=DevelopmentBypass` و فلگ‌های قدیمی فعال رد می‌شوند.
[تنظیمات، قرارداد، تست‌ها و گزارش آموزشی](docs/dev/CONFIGURABLE_OTP_FA.md).

### سرویس OTP مدیریت‌شده MeliPayamak

`MeliPayamakOtpProvider` قرارداد OTP مدیریت‌شده را فراخوانی می‌کند:

```text
POST https://console.melipayamak.com/api/send/otp/{service-token}
Content-Type: application/json

{"to":"09121234567"}
```

در این قرارداد MeliPayamak کد را تولید و پیامک می‌کند و پاسخ JSON شامل `code` و `status` را به backend می‌دهد. MakanApp کد را به‌صورت string opaque دریافت، اعتبارسنجی و فوراً hash می‌کند؛ کد provider هرگز به frontend یا log نمی‌رود. شماره canonical مانند `+989121234567` فقط در adapter به `09121234567` تبدیل می‌شود. درخواست retry خودکار ندارد و timeout محدود است، زیرا timeout پس از side effect می‌تواند به معنی ارسال‌شدن پیامک و گم‌شدن response باشد. TTL مستندی از provider در دست نیست؛ بنابراین lifetime پنج‌دقیقه‌ای فعلی Makan حفظ شده است.

تنظیمات غیرمحرمانه commit‌شده:

```json
{
  "Sms": {
    "Provider": "MeliPayamakOtp",
    "MeliPayamakOtp": {
      "BaseUrl": "https://console.melipayamak.com",
      "TimeoutSeconds": 10
    }
  }
}
```

مقادیر محرمانه را برای `MakanApp.Api` با User Secrets ثبت کنید:

```powershell
$keyBytes = New-Object byte[] 48
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($keyBytes)
$identityKey = [Convert]::ToBase64String($keyBytes)
dotnet user-secrets set "Identity:SecurityKey" $identityKey --project .\MakanApp.Api\MakanApp.Api.csproj | Out-Null
[Array]::Clear($keyBytes, 0, $keyBytes.Length)
$rng.Dispose()
Remove-Variable identityKey
dotnet user-secrets set "Sms:Provider" "MeliPayamakOtp" --project .\MakanApp.Api\MakanApp.Api.csproj
dotnet user-secrets set "Sms:MeliPayamakOtp:ServiceToken" "<SERVICE_TOKEN>" --project .\MakanApp.Api\MakanApp.Api.csproj
```

یا در محیط استقرار از متغیرهای زیر استفاده کنید:

```text
Identity__SecurityKey=<stable-secret-at-least-32-UTF8-bytes>
Sms__Provider=MeliPayamakOtp
Sms__MeliPayamakOtp__BaseUrl=https://console.melipayamak.com
Sms__MeliPayamakOtp__ServiceToken=<secret>
Sms__MeliPayamakOtp__TimeoutSeconds=10
```

`ServiceToken` و `Identity:SecurityKey` پایدار هنگام انتخاب `MeliPayamakOtp` در startup اجباری‌اند. کد برگشتی provider با `Identity:SecurityKey` به‌صورت HMAC محافظت می‌شود؛ تغییر یا تصادفی‌شدن این کلید بین request و verify باعث نامعتبرشدن challenge می‌شود. `ServiceToken` بخشی محرمانه از path درخواست است. logging پیش‌فرض typed `HttpClient` برای این client حذف شده و adapter نیز URI، token، کد برگشتی، body خام یا شماره کامل را log نمی‌کند. تنظیمات قدیمی `Username`، `Credential`، `From` و endpoint `rest.payamak-panel.com` دیگر برای OTP استفاده نمی‌شوند و حذف شده‌اند.

برای نگه‌داشتن provider محلی:

```text
Sms:Provider=Development
```

Smoke test زنده خودکار نیست و در `dotnet test` اجرا نمی‌شود. بعد از تنظیم secretها، اجرای API و تعیین صریح شماره آزمایشی، می‌توان آن را به‌صورت opt-in اجرا کرد:

```powershell
$env:MELIPAYAMAK_OTP_LIVE_TEST = "true"
$env:MELIPAYAMAK_OTP_LIVE_TEST_PHONE = "<explicit-test-phone>"
if ($env:MELIPAYAMAK_OTP_LIVE_TEST -ne "true" -or [string]::IsNullOrWhiteSpace($env:MELIPAYAMAK_OTP_LIVE_TEST_PHONE)) { throw "Live OTP test is not explicitly enabled." }
$body = @{ phoneNumber = $env:MELIPAYAMAK_OTP_LIVE_TEST_PHONE } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "https://localhost:7063/api/v1/auth/otp/challenges" -ContentType "application/json" -Body $body
```

این عملیات ممکن است هزینه پیامک داشته باشد. شماره آزمایشی، credential یا فایل `.env` حاوی secret را commit نکنید.

کلید `Identity:SecurityKey` باید خارج از Development/Testing و نیز هنگام استفاده از `MeliPayamakOtp` در هر محیط، از secret store تأمین شود و حداقل ۳۲ بایت UTF-8 داشته باشد. فقط provider محلی Development/Testing می‌تواند بدون این تنظیم از کلید تصادفی process استفاده کند؛ در آن حالت نشست‌ها و challengeهای OTP بعد از restart معتبر نمی‌مانند.

درخواست OTP علاوه بر محدودیت پایدار مبتنی بر شماره تلفن، policy داخلی ASP.NET Core مبتنی بر IP دارد. policy مبتنی بر IP در این مرحله in-memory و تک-instance است؛ استقرار چند-instance در آینده به rate limiter توزیع‌شده نیاز دارد.

## سازمان، عضویت، دعوت و فضای کاری

مدل‌های `Organization`، `OrganizationPerson`، `Membership`، `RoleAssignment` و `Invitation` در schema با نام `organization` نگهداری می‌شوند. `OrganizationPerson` رکورد سازمانی یک `identity.Person` است، `UserId` ندارد و اجازه می‌دهد شخص بدون حساب کاربری در یک یا چند سازمان رکورد مستقل داشته باشد. نقش سازمانی به عضویت تعلق دارد و از چهار مقدار پایدار `Student`، `Teacher`، `Parent` و `Manager` استفاده می‌کند؛ هیچ نقش سراسری روی User ذخیره نمی‌شود.

| متد | مسیر | نیاز به ورود |
|---|---|---|
| `GET` | `/api/v1/workspaces/me` | بله |
| `POST` | `/api/v1/workspaces/select` | بله |
| `GET` | `/api/v1/workspaces/current` | بله |
| `GET` | `/api/v1/invitations` | بله |
| `POST` | `/api/v1/invitations/{invitationId}/accept` | بله |
| `POST` | `/api/v1/invitations/{invitationId}/decline` | بله |

فضای شخصی مستقل از عضویت سازمانی همیشه در دسترس است. فضای سازمانی فقط از عضویت فعال، سازمان فعال و نقش فعال ساخته می‌شود. انتخاب فضا در همان `UserSession` ثبت می‌شود و در هر resolve مجدداً سمت سرور اعتبارسنجی می‌شود؛ شناسه سازمان یا نقش ارسالی Client به‌تنهایی مجوز نیست.

پذیرش دعوت در transaction با isolation سطح `Serializable` و lockهای SQL Server انجام می‌شود و تکرار پذیرش همان اثر قبلی را برمی‌گرداند. filtered unique indexها از عضویت فعال و نقش فعال تکراری جلوگیری می‌کنند و `rowversion` روی رکوردهای قابل تغییر تعارض stale write را آشکار می‌کند.

## سرپرست و بافت والد/فرزند

`GuardianRelation` در schema با نام `guardian` کاربر سرپرست را در محدوده همان سازمان به `OrganizationPerson` فراگیر متصل می‌کند. فقط رابطه `Active`، فضای سازمانی معتبر و نقش فعال `Parent` دسترسی می‌دهند؛ بنابراین نقش والد به‌تنهایی مجوز مشاهده همه فراگیران سازمان نیست. انتخاب فرزند در `UserSession.SelectedSubjectOrganizationPersonId` ذخیره می‌شود و در هر resolve دوباره رابطه، سازمان و وضعیت فراگیر بررسی می‌شوند.

| متد | مسیر | نیاز به ورود |
|---|---|---|
| `GET` | `/api/v1/guardian/children` | بله؛ فضای سازمانی با نقش Parent |
| `POST` | `/api/v1/guardian/children/{organizationPersonId}/select` | بله؛ رابطه فعال هم‌سازمانی |
| `GET` | `/api/v1/guardian/relations/{organizationPersonId}` | بله؛ رابطه فعال هم‌سازمانی |

در این مرحله endpoint عمومی برای ساخت یا مدیریت `GuardianRelation` وجود ندارد. migration با نام `AddGuardianRelations` جدول `guardian.GuardianRelations`، FK ترکیبی هم‌سازمانی، filtered unique index رابطه فعال و `rowversion` را ایجاد می‌کند. مجوزهای جزئی مشاهده نمره، حضور و غیاب، گزارش و ارتباطات به مرحله‌های بعدی مدل‌های آموزشی موکول شده‌اند.

## زیرساخت آموزشی

مدل‌های `AcademicPeriod`، `Course`، `Class`، `Enrollment` و `TeacherAssignment` در schema با نام `academic` نگهداری می‌شوند. ثبت‌نام به `OrganizationPerson` متصل است تا فراگیر بدون حساب کاربری نیز قابل مدیریت باشد؛ انتساب معلم به `Membership` فعال دارای نقش `Teacher` متصل است. FKهای ترکیبی، هم‌سازمانی بودن منابع را در خود SQL Server enforce می‌کنند و filtered unique indexها از ثبت‌نام فعال یا انتساب فعال تکراری جلوگیری می‌کنند.

عملیات نوشتن این مرحله به فضای سازمانی فعال با نقش `Manager` نیاز دارند. ظرفیت کلاس داخل transaction کوتاه `Serializable` و با lockهای `UPDLOCK, HOLDLOCK` کنترل می‌شود؛ بنابراین درخواست‌های هم‌زمان نمی‌توانند از آخرین صندلی عبور کنند. پایان ثبت‌نام یا انتساب، رکورد تاریخی را حذف یا بازنویسی نمی‌کند.

| متد | مسیر | مجوز |
|---|---|---|
| `POST` | `/api/v1/academic/periods` | Manager سازمان فعلی |
| `POST` | `/api/v1/academic/courses` | Manager سازمان فعلی |
| `POST` | `/api/v1/academic/classes` | Manager سازمان فعلی |
| `GET` | `/api/v1/academic/classes` | Manager: سازمان فعلی؛ Teacher: کلاس‌های منتسب؛ Student: کلاس‌های ثبت‌نام‌شده |
| `POST` | `/api/v1/academic/classes/{classId}/enrollments` | Manager سازمان فعلی |
| `POST` | `/api/v1/academic/classes/{classId}/enrollments/{enrollmentId}/end` | Manager سازمان فعلی |
| `POST` | `/api/v1/academic/classes/{classId}/teachers` | Manager سازمان فعلی |
| `POST` | `/api/v1/academic/classes/{classId}/teachers/{teacherAssignmentId}/end` | Manager سازمان فعلی |

خواندن کلاس‌های فرزند برای نقش `Parent` عمداً در این مرحله ارائه نشده است و تا اضافه‌شدن read model امن مبتنی بر Guardian context با خطای کنترل‌شده رد می‌شود.

## ارزیابی و انتشار نمره تکلیف

هر `AssignmentVersion` سقف نمره مستقل و اجباری `MaxScore decimal(9,2)` دارد. این مقدار هنگام ساخت/ویرایش Draft صریحاً دریافت می‌شود، SQL Default ندارد و پس از انتشار نسخه قابل تغییر درجا نیست. فقط `SubmissionAttempt` با وضعیت `Submitted` قابل ارزیابی است.

`EvaluationRevision` پیش‌نویس ارزیابی را از `GradeRelease` جدا نگه می‌دارد. ذخیره نمره یا بازخورد به معنی انتشار نیست. اصلاح نتیجه منتشرشده یک revision تازه با دلیل اصلاح می‌سازد؛ revision قبلی حذف یا بازنویسی نمی‌شود. `rowversion` و تراکنش کوتاه `Serializable` جلوی overwrite خاموش و انتشار هم‌زمان تکراری را می‌گیرند.

| متد | مسیر | مجوز/کاربرد |
|---|---|---|
| `GET` | `/api/v1/academic/evaluations/queue` | Manager سازمان یا Teacher دارای `TeacherAssignment` فعال |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation/submission` | مشاهده پاسخ نهایی برای ارزیاب مجاز |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation` | مشاهده revision فعلی توسط ارزیاب مجاز |
| `PUT` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation` | ساخت یا ذخیره Draft ارزیابی با `rowversion` |
| `POST` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation/release` | انتشار صریح و idempotent نتیجه |
| `POST` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation/corrections` | ساخت Draft اصلاحی با دلیل اجباری |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/result` | نتیجه منتشرشده برای Student مالک attempt |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/guardian-result` | نتیجه منتشرشده والد در Guardian context معتبر |

سه داده `LearnerFeedback`، `GuardianVisibleFeedback` و `TeacherPrivateNote` قراردادهای جدا دارند. DTO دانش‌آموز فقط بازخورد دانش‌آموز و DTO والد فقط بازخورد مجاز والد را دارد؛ `TeacherPrivateNote` در هیچ‌کدام serialize نمی‌شود.

## پیام‌رسانی مستقیم

ماژول `Messaging` زیرساخت پایدار گفت‌وگوی مستقیم دو `User` را در Scope شخصی یا سازمانی فراهم می‌کند. زوج کاربران به شکل canonical ذخیره می‌شود؛ بنابراین A+B و B+A در Scope یکسان یک Conversation هستند. Scope سازمانی فقط از `AccessContext` معتبر نشست تعیین می‌شود و Client نمی‌تواند `OrganizationId` را به‌عنوان مجوز تحمیل کند.

شروع تماس Personal فقط برای دو حساب Adult با `PersonalCommunicationGrant` فعال مجاز است. ارتباط سازمانی نیز بر Membership و Role فعال و در نقش‌های آموزشی بر Enrollment، TeacherAssignment و GuardianRelation معتبر متکی است. شماره تلفن در پاسخ‌های Messaging وجود ندارد و رد مخاطب محافظت‌شده با خطای عمومی انجام می‌شود. جزئیات کامل policy در `docs/architecture/STEP_7A_DIRECT_MESSAGING_FOUNDATION.md` ثبت شده است.

| متد | مسیر | کاربرد |
|---|---|---|
| `POST` | `/api/v1/conversations/direct` | ساخت یا resolve گفت‌وگوی مستقیم |
| `GET` | `/api/v1/conversations` | فهرست گفت‌وگوهای مجاز User |
| `GET` | `/api/v1/conversations/{conversationId}/messages` | تاریخچه پایدار بر اساس Sequence |
| `POST` | `/api/v1/conversations/{conversationId}/messages` | ارسال پایدار و retry-safe پیام متن |

`ClientMessageId` همراه با Conversation و Sender کلید idempotency است. retry با محتوای یکسان همان receipt را برمی‌گرداند و استفاده از همان شناسه برای متن متفاوت رد می‌شود. `Sequence` و `SentAtUtc` سمت سرور و داخل transaction SQL Server تخصیص می‌یابند. `Sent` فقط commit موفق در SQL Server است؛ `Delivered` با ACK صریح کلاینت و `Read` با cursor خواندن participant ثبت می‌شوند. SignalR فقط اعلان سبک می‌فرستد و بازیابی قطعی از Delta API انجام می‌شود.

برای اعمال migration روی database مجاز و ایزوله:

~~~powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj --startup-project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj
~~~

این فرمان را روی Production یا database عادی توسعه بدون فرایند migration مصوب اجرا نکنید.

## Restore، build و test

از ریشه workspace اجرا کنید:

~~~powershell
dotnet restore MakanApp.sln
dotnet build MakanApp.sln --configuration Debug --nologo
dotnet test MakanApp.sln --configuration Debug --nologo
~~~

- Unit Tests علاوه بر قواعد Identity و Organization، lifecycle رکوردهای `OrganizationPerson` و `GuardianRelation`، مدل‌های Academic/Assessment/Commerce و قواعد Domain و eligibility در Messaging را بررسی می‌کنند.
- Integration Tests migration واقعی، endpointهای Identity/Profile/Organization/Guardian/Academic/Assessment/Messaging/Commerce، Outbox، SignalR، Delta Sync، حریم خصوصی، tenant isolation، idempotency و concurrency را روی LocalDB اختصاصی `MakanApp_MessagingSafety_IntegrationTests_Step7E` بررسی و آن database را در پایان حذف می‌کنند؛ EF Core InMemory استفاده نمی‌شود.
- Architecture Tests جهت وابستگی Onion و نبود EF Core/ASP.NET Core در لایه‌های داخلی را enforce می‌کنند.

## اجرا

~~~powershell
dotnet run --project MakanApp.Api/MakanApp.Api.csproj --configuration Debug --launch-profile https
~~~

~~~text
https://localhost:7063/health
https://localhost:7063/swagger
https://localhost:7063/openapi/v1.json
~~~

## گروه و کانال پیام‌رسانی — STEP 7B

Group و Channel نوع‌های فعال `Conversation` هستند. ساخت آن‌ها با `ClientOperationId` retry-safe است و نقش‌های `Owner`، `Admin` و `Member`، خروج/حذف عضو، انتقال مالکیت با پذیرش مقصد و archive را پشتیبانی می‌کنند. همه اعضای فعال Group می‌توانند پیام بفرستند؛ در Channel فقط Owner/Admin منتشر می‌کنند.

مسیرهای مدیریت زیر به API اضافه شده‌اند:

- `POST /api/v1/conversations/groups`
- `POST /api/v1/conversations/channels`
- `GET /api/v1/conversations/{conversationId}`
- `POST /api/v1/conversations/{conversationId}/members`
- `DELETE /api/v1/conversations/{conversationId}/members/{userId}`
- `POST /api/v1/conversations/{conversationId}/leave`
- `PATCH /api/v1/conversations/{conversationId}/members/{userId}/role`
- `POST /api/v1/conversations/{conversationId}/ownership-transfers`
- `POST /api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/accept`
- `POST /api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/decline`
- `POST /api/v1/conversations/{conversationId}/archive`

جزئیات تصمیم‌های امنیتی، integrity دیتابیس و محدودیت‌های این مرحله در `docs/architecture/STEP_7B_GROUP_CHANNEL_MANAGEMENT.md` ثبت شده است. هر عبارت قدیمی مبنی بر پیاده‌سازی‌نشدن Group/Channel با این بخش منسوخ است. provisioning خودکار `SystemManagedAcademic` و قابلیت‌های realtime همچنان خارج از محدوده STEP 7B هستند. دیتابیس integration test فعلی `MakanApp_AdvancedMessaging_IntegrationTests_Step7C` است.

## پیام‌رسانی پیشرفته — STEP 7C

پیام‌های `Text`، `Image`، `Video`، `Voice` و `File` روی مدل واحد `Message` اجرا می‌شوند. ویرایش با `rowversion` و جدول `MessageRevisions` تاریخچه را حفظ می‌کند؛ حذف نیز به‌جای پاک‌کردن ردیف، tombstone امن می‌سازد. Reply، Forward محدود به Scope مجاز، واکنش محدود، منشن Participant فعال، سنجاق نقش‌محور و مرور رسانه‌های مجاز پشتیبانی می‌شوند.

پیوست‌ها از زیرساخت مشترک `FileAsset` استفاده می‌کنند. binding پیام فایل را retained می‌کند و دانلود دریافت‌کننده علاوه بر وضعیت فایل، دسترسی جاری به Message/Conversation را بررسی می‌کند. جزئیات مدل، endpointها، constraintها و محدودیت‌های مرحله در `docs/architecture/STEP_7C_ADVANCED_MESSAGING.md` ثبت شده است.

## همگام‌سازی و realtime پیام‌رسانی — STEP 7D

هر Conversation دو جریان مستقل دارد: `MessageSequence` ترتیب تاریخچه پیام را نگه می‌دارد و `ChangeSequence` تغییرات قابل بازیابی مانند ساخت، ویرایش و حذف پیام، واکنش، سنجاق و عضویت را مرتب می‌کند. mutation، `ChangeEvent` و پیام `RealtimeOutbox` در یک transaction ثبت می‌شوند. Dispatcher فقط بعد از commit یک invalidation سبک SignalR می‌فرستد؛ بنابراین SQL Server مرجع حقیقت باقی می‌ماند و event ازدست‌رفته با Delta قابل بازیابی است.

- `GET /api/v1/conversations/{conversationId}/changes`
- `POST /api/v1/conversations/{conversationId}/read`
- `POST /api/v1/conversations/{conversationId}/delivered`
- Hub: `/hubs/messaging`

cursorهای Delivered و Read به‌صورت monotonic و participant-level ذخیره می‌شوند و summary گفت‌وگو `UnreadCount` را از پیام‌های خوانده‌نشده دیگران محاسبه می‌کند. هر Delta request و Hub subscription دسترسی جاری و session را دوباره بررسی می‌کند. جزئیات transaction، Outbox، reconnect، retention و محدودیت scale-out در `docs/architecture/STEP_7D_MESSAGING_REALTIME_SYNC.md` ثبت شده است. Migration جاری Messaging برابر `AddMessagingRealtimeAndSync` و دیتابیس integration test ایزوله برابر `MakanApp_MessagingSync_IntegrationTests_Step7D` است.

Search، Block و Report پس از STEP 7D اضافه شده‌اند و در راهنمای API توضیح داده شده‌اند. Moderation، Push provider و صف mutation سمت کلاینت همچنان خارج از محدوده‌اند.

## Visual Studio

1. `MakanApp.sln` را باز کنید.
2. `MakanApp.Api` را Startup Project قرار دهید.
3. پیکربندی `Debug / Any CPU` و پروفایل `https` را انتخاب کنید.
4. Solution را build و اجرا کنید.
5. `/health` یا Swagger UI را بررسی کنید.

## محدودیت‌ها

محدودیت‌های جاری: gateway مالی واقعی، Push و SMS عمومی اعلان، AV/CDR و اسکن معنایی کامل رسانه، limiter توزیع‌شده و backplane چند-instance آماده نیستند. adapter OTP ملی‌پیامک، onboarding مدیر، Guardian lifecycle، گزارش/export، اعلان داخلی/outbox، Commerce/Backoffice و APIهای Backend AI اکنون وجود دارند؛ وجود کد به معنی smoke سرور یا real AI E2E نیست. Rubric ساختاریافته و auto-finalize آزمون جزو قابلیت‌های بسته‌شدهٔ این تحویل نیستند. [ممیزی gapها](docs/release/NON_AI_BACKEND_FINAL_GAP_AUDIT.md) و [register تصمیم‌ها](docs/release/DEFERRED_DECISIONS_AND_REMAINING_GAPS.md) مرجع محدودیت‌های باقی‌مانده‌اند.
