# Runbook فعال‌سازی AI v1 روی Windows Backend

این سند دستورالعمل **مرحلهٔ بعد** است؛ فرمان‌های آن در مرحلهٔ اتصال محلی اجرا نشده‌اند. AI واقعی، Provider، Test/Production و IIS اکنون فعال یا تغییر داده نشده‌اند. [شرح قرارداد Backend](AI_V1_BACKEND_INTEGRATION_FA.md).

## پیش‌نیاز و حفاظت

اجرای این مرحله باید توسط مسئول مجاز و از **خود Windows Backend Server** انجام شود. مسیر مورد انتظار `10.77.0.1 → WireGuard → http://10.77.0.2:8001` است. Public Swagger یا اتصال از رایانهٔ توسعه جایگزین این gate نیست.

- ابتدا commit نهایی، بستهٔ قابل rollback، appsettings خصوصی و secret store را بررسی کنید؛ هیچ secretی در چت، Git، transcript، screenshot یا stdout ذخیره نشود.
- credential جدید همان issuer/audience/subject/scope قرارداد را داشته باشد؛ کلید و kid دو سمت باید در محیط امن هماهنگ باشند. JWT چاپ نکنید.
- اول Test با `MakanApp_Test` و سپس Production با `MakanApp` بررسی شود؛ connection stringها را با یکدیگر جایگزین نکنید. Mock OTP در Production همچنان ممنوع است.
- AI برای هر دو محیط تا رسیدن به gate مربوط `Enabled=false` بماند. private config schema را از تنظیمات موجود نگه دارید؛ secret واقعی را در template نگذارید.
- Probe رسمی یک ابزار .NET 9 SDK است. نقش‌های ساختگی آن به **LocalDB ایزولهٔ خودکار** نیاز دارند؛ به دیتابیس MakanApp یا MakanApp_Test متصلش نکنید. اگر SDK/LocalDB مجاز روی Backend آماده نیست، آن بخش را PASS اعلام نکنید؛ محیط اجرای جدا و مجاز روی همان مسیر شبکه آماده شود.

## ۱. فقط شبکه

```powershell
Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object IPAddress -eq '10.77.0.1' |
    Select-Object IPAddress,InterfaceAlias
Test-NetConnection 10.77.0.2 -Port 8001
```

اگر IP یا TCP برقرار نیست، توقف کنید؛ URL، timeout، JWT و credential را برای دورزدن مشکل تغییر ندهید. Auth/Contract هنوز تست نشده‌اند.

## ۲. Health و Ready

پس از TCP موفق، بدون header محرمانه:

```powershell
curl.exe --silent --show-error --max-time 10 --output NUL --write-out "health=%{http_code}\n" http://10.77.0.2:8001/api/v1/health
curl.exe --silent --show-error --max-time 10 --output NUL --write-out "ready=%{http_code}\n" http://10.77.0.2:8001/api/v1/ready
```

هر دو باید 200 باشند؛ readiness ناموفق را به‌عنوان مشکل credential گزارش نکنید. TLS عمومی را bypass نکنید؛ HTTP این مقصد فقط مسیر خصوصی مصوب WireGuard است.

## ۳. آماده‌کردن Probe

از source همان commit تأییدشده و در terminal خصوصی بدون transcript اجرا کنید:

```powershell
Set-Location 'D:\Project\MakanApp\MakanApp-Backend'
dotnet build tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
$env:AiService__BaseUrl = 'http://10.77.0.2:8001'
$env:AiLiveProbe__ApprovedOrigin = 'http://10.77.0.2:8001'
```

تنظیمات Authentication را از secret store مصوب یا environment امن همان process تأمین کنید. نام‌ها: `AiService__Authentication__Issuer`، `Audience`، `ServiceName`، `Scope`، `CurrentKeyId`، `CurrentKey` و `TokenLifetimeSeconds`. مقدار CurrentKey را در command history وارد نکنید و `Get-ChildItem Env:` یا `dotnet user-secrets list` چاپ نکنید. مقادیر قراردادی غیرمحرمانه: `makan-backend`، `makan-ai`، `makan-backend`، `makan.ai.invoke` و lifetime=120. تنظیم timeout موجود را حفظ کنید؛ آن را برای پاس‌کردن آزمون افزایش ندهید.

`--allow-wireguard` فقط همین مقصد دقیق و وجود IP محلی `10.77.0.1` را می‌پذیرد. اجازهٔ HTTP عمومی، مقصد دلخواه یا `--allow-remote-https` هم‌زمان نمی‌دهد. Probe تنظیم private appsettings را نمی‌نویسد؛ در حافظهٔ process از User Secrets و environment استفاده می‌کند.

## ۴. Service Auth Positive/Negative Control

```powershell
dotnet run --project tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Release --no-build -- --run-live --service-auth-only --allow-wireguard
if ($LASTEXITCODE -ne 0) { throw 'Service auth gate failed' }
```

انتظار: JWT معتبر + body نامعتبر → 422؛ JWT مفقود/خراب/منقضی/امضا یا kid و issuer/audience/subject نامعتبر → 401؛ scope ناقص → 403. از این gate، موفقیت Provider نتیجه نمی‌شود. failure را با requestId/correlation/status/code امن پیگیری کنید؛ token را چاپ نکنید.

## ۵. Focused Three

```powershell
dotnet run --project tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Release --no-build -- --run-live --operation focused_three --allow-wireguard
if ($LASTEXITCODE -ne 0) { throw 'Focused three failed; Full12 is not eligible' }
```

Parent Child Summary، Teacher Activity Suggestions و Manager Class Insight باید هر سه موفق باشند. Probe فقط در SQL ایزوله قرارداد و نقش ساختگی می‌سازد؛ schema/کاربر واقعی را تغییر نمی‌دهد. 504 یا timeout را ثبت و متوقف کنید؛ loop، retry، افزایش timeout یا تغییر Auth اضافه نکنید. ID، elapsed، alias و code امن برای AI Team کافی است.

## ۶. Full12 واقعی

فقط پس از سه موفقیت و مجوز اجرای این gate:

```powershell
dotnet run --project tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Release --no-build -- --run-live --allow-wireguard
if ($LASTEXITCODE -ne 0) { throw 'Full12 failed' }
```

موفقیت باید ۱۲/۱۲ و با alias `education-fast`، correlation معتبر، عدم نشت و role boundary درست باشد. source-selected در این نسخه همچنان unavailable است؛ Full12 مجوز فعال‌سازی Retrieval نیست. اجرای واقعی این دو مرحله هزینه/مصرف Provider واقعی دارد و در مرحلهٔ محلی فعلی انجام نشده است.

## ۷. فعال‌سازی محدود Test

پس از gateهای قبلی، در **تنظیمات خصوصی همان سایت Test** بخش موجود را حفظ و فقط `AiService:Enabled` را true کنید. BaseUrl همان مقصد خصوصی مصوب، ContractVersion=v1، timeout/circuit breaker موجود و Service Authentication مصوب بمانند. احراز کاربر، نقش، Child selection و مجوز تجاری را با حساب‌های تست تأیید کنید. caller برای هر intent یک `X-AI-Operation-Id` ثابت و payer صریح یا Organization پیش‌فرض می‌فرستد.

قرارداد معتبر AI و Quota باید توسط workflow موجود Admin فراهم شده باشد؛ entitlement حدسی یا backfill نسازید. یک موفقیت باید یک Consumed، retry همان ID باید 409 بدون تماس/مصرف دوباره، و منبع انتخاب‌شده باید unavailable بدون رزرو باشد. timeout/Unknown را با ID تازه دور نزنید.

این مرحله migration جدید ندارد. recycle لازم سایت فقط در پنجرهٔ مجاز انجام شود؛ runbook خودش IIS را تغییر نمی‌دهد.

## ۸. فعال‌سازی Production و پایش

فقط پس از پذیرش Test و تأیید مسئول انتشار، تغییر محدود Enabled در private Production انجام شود. صحت اتصال به MakanApp، Real OTP، Data Protection، Role/Membership/Guardian و قراردادهای تجاری را حفظ کنید. نرخ خطا/timeout، circuit rejection، elapsed، alias، reservationهای Reserved/Unknown و سهمیه را پایش کنید. raw prompt/context/response، token و Authorization را وارد logging نکنید. این راهنما مجوز دورزدن UnknownAge/Safety یا نقش نیست.

## ۹. Rollback

در صورت خطای شبکه، Provider، quota یا privacy: `AiService:Enabled=false` را در تنظیمات خصوصی محیط آسیب‌دیده اعمال و process را در پنجرهٔ مجاز reload کنید. درخواست‌های بعدی قبل از رزرو با FeatureDisabled رد می‌شوند. false کردن flag درخواست درحال‌اجرا را به‌صورت جادویی لغو یا مصرف قطعی قبلی را حذف نمی‌کند.

هیچ UsageLedger/Reservation را پاک نکنید. Unknown/Reserved را از روی شواهد امن و workflow داخلی موجود بررسی کنید؛ عملیات موفق را دوباره ارسال یا Consumed را برنگردانید. rollback schema لازم نیست؛ migration جدید وجود ندارد. کلید افشاشده باید با workflow امنیتی rotate شود، نه با قرار دادن مقدار جدید در Git.

در پایان evidence امن شامل commit مستقر، زمان UTC، gate اجراشده، status/code، requestId/correlationId، elapsed و modelAlias را ثبت کنید؛ هر gate اجرا‌نشده `PENDING` است، نه PASS.
