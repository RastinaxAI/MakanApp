# Release Checklist

<!-- CURRENT-REVIEW:BEGIN -->
بازبینی `2026-10-18` از `main@ef67624`؛ [وضعیت جاری](../CURRENT_PROJECT_STATUS.md)، [ماتریس آمادگی](PRODUCTION_READINESS.md) و [runbook IIS](DEPLOYMENT_IIS.md) مبنای gateها هستند. این بازبینی deployment، credential واقعی یا load/restore محیط هدف را اثبات نمی‌کند.
<!-- CURRENT-REVIEW:END -->

بازبینی جاری ۲۰۲۶-۱۰-۱۵ از main@80d6a14: این چک‌لیست gate انتشار امن/عمومی است؛ پذیرش استثنای HTTP توسط مالک، تیک TLS یا closure GAP-02 نیست. فایل Staging برای انتشار جاری نداریم. راهنمای اجرایی [IIS](DEPLOYMENT_IIS.md) و [ماتریس وضعیت جاری](PRODUCTION_READINESS.md) را کنار این فهرست بخوانید.

## پیش از انتشار

- [ ] `dotnet restore`, دو بار build/test نهایی و `git diff --check` سبز است.
- [ ] روی source workspace دارای SDK، `dotnet ef migrations has-pending-model-changes --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --context MakanDbContext --configuration Release --no-build` بدون pending model است؛ Api startup درست ابزار EF این repository نیست. این check schema دیتابیس مقصد را بررسی یا mutate نمی‌کند و داخل publish سرور اجرا نمی‌شود.
- [ ] SQL با `tools/production/export-migration-sql.ps1` تولید و با `tools/production/test-migration-sql.ps1 -ConfirmLocalDisposableDatabase` روی LocalDB ایزوله آزمایش شده؛ hash فایل تحویلی با ScriptSha256 گزارش یکسان است.
- [ ] artifact از commit/branch مورد انتظار ساخته شده و secret ندارد.
- [ ] فایل خصوصی `appsettings.Production.json` خارج از Git و از نمونه جداست؛ اگر به artifact انتقال اپراتور اضافه شده، artifact محرمانه و غیرقابل‌اشتراک است. `appsettings.Staging.json` در خروجی تازه وجود ندارد.
- [ ] انتشار HTTP-only فقط با تأیید صریح دو استثنای مستقل `Hosting:AllowInsecureHttp` و `Database:AllowUntrustedServerCertificate` انجام می‌شود؛ این تأیید جای TLS امن، حساب SQL کم‌اختیار یا smoke زنده را نمی‌گیرد.
- [ ] Production blockers و قابلیت‌های unavailable به ذی‌نفعان اعلام شده‌اند.
- [ ] backup DB و storage گرفته و reference آن ثبت شده است.
- [ ] runtime و migration principal جدا و connection TLS معتبر است.
- [ ] CORS origins، AllowedHosts، known proxy، storage root و write permission تأیید شده‌اند.
- [ ] .NET 9 Hosting Bundle، IIS WebSocket، HTTPS binding، firewall و log collector آماده‌اند.
- [ ] key ring فعلی MFA، هویت ثابت App Pool، Load User Profile، ACL و روش بازیابی DPAPI/certificate حفظ و بررسی شده‌اند؛ Identity key در deploy عادی عوض نشده است.

## migration و rollout

- [ ] traffic در صورت نیاز drain شده است.
- [ ] migration با principal مخصوص و بدون startup auto-migration اجرا شده است.
- [ ] `__EFMigrationsHistory` و schema هدف بررسی شده است.
- [ ] نسخه‌ی جدید side-by-side deploy و App Pool recycle شده است.
- [ ] liveness/readiness سبز و startup log فاقد secret است.

## smoke و پذیرش

- [ ] `tools/production/smoke-test.ps1` علیه staging/target مجاز اجرا شده است.
- [ ] login/OTP واقعی فقط در staging مجاز با HTTPS معتبر، opt-in صریح و ورود دستی کد بررسی و شواهد sanitized ثبت شده است؛ normal dotnet test ارسال واقعی ندارد.
- [ ] [شواهد GAP-02](../release/GAP_02_STAGING_CORE_SMOKE_VALIDATION.md) به‌روز است؛ HTTP-only، readiness 404 یا نبود دریافت/verify واقعی SMS، closure محسوب نشده است.
- [ ] profile، workspace، یک read اصلی و boundaryهای 401/403 بررسی شده‌اند.
- [ ] SignalR negotiate، DB read و storage probe قابل استفاده‌اند.
- [ ] 5xx، latency، worker backlog و provider degraded status مشاهده شده‌اند.

## rollback

- [ ] معیار شکست و تصمیم‌گیر rollback مشخص است.
- [ ] artifact قبلی و تنظیمات سازگار موجود است.
- [ ] migration backward compatibility ارزیابی شده؛ downgrade خودکار فرض نشده است.
- [ ] برای schema ناسازگار، forward-fix یا restore نقطه‌ی سازگار DB+storage تعیین شده است.
- [ ] trace IDها، log window و incident notes پیش از rollback حفظ می‌شوند.

## gateهای افزوده Messenger

- [ ] migrationها تا AddConversationChecklists بررسی و فقط با principal مجاز محیط اجرا شده‌اند.
- [ ] Web Push/FCM با credential/device واقعی آزمایش و P1_PUSH_RUNTIME_PENDING با شاهد رفع شده است.
- [ ] expiry/reconnect و privacy حالت‌های Typing/Presence روی topology هدف بررسی شده‌اند.
- [ ] dispatch/cancel/recovery پیام زمان‌بندی‌شده و تغییر مجوز پس از ثبت intent بررسی شده‌اند.
- [ ] Topic، Poll/Quiz و Checklist با role و optimistic concurrency معتبر smoke شده‌اند.
- [ ] scale-out بدون backplane/state مشترک ادعا نشده و P3 رسانه بدون PASS واقعی S منتشر نمی‌شود.

## پذیرش PH — تفکیک کد از سرور

مبنای کد PH در [ممیزی مراحل](../operations/PRODUCTION_HARDENING_AUDIT.md) ثبت می‌شود؛
خانه‌های این بخش تا اجرای واقعی روی مقصد **NOT_RUN / OPERATIONAL_VALIDATION_PENDING** هستند.
نام تست محلی، publish موفق یا remote برابر، شاهد reboot/recycle سرور نیست.

### پیش‌شرط و شواهد مجاز

- [ ] commit/hash artifact، UTC، نام محیط مجاز و اپراتور ثبت شده‌اند؛ config خصوصی، publish فعلی،
  DB/storage/ring و reference backup حفظ شده‌اند؛ فایل backup محرمانه به evidence عمومی اضافه نشده است.
- [ ] هویت App Pool، profile، مسیر/محافظ ring، ApplicationName و Identity key قبلی حفظ شده‌اند؛
  payload مصنوعیِ پیش از restart در همان purpose/provider بعد از restart قابل بازکردن است.
  secret TOTP/OTP یا XML واقعی کلید در گزارش چاپ نمی‌شود.
- [ ] تنظیم‌های [IIS Runtime](DEPLOYMENT_IIS.md#قرارداد-runtime-و-shutdown-در-ph-5)،
  Hosting Bundle نسخهٔ ۹، bitness، maxProcesses=1، warm-up مشروط و budgetهای جدا بررسی شده‌اند؛
  ping و rapid-fail برای سبزکردن تست خاموش نشده‌اند.
- [ ] در اولین rollout، worker legacy drain/stop و خروج PID قبلی تأیید شده است؛
  رقابت دو نسخهٔ hardened جای ایمنی old/new report writer را نمی‌گیرد.
- [ ] traffic و حساب/سازمان/فایل مصنوعی مجاز مشخص است؛ reset attempt، حذف history/ring،
  قطعی عمدی SQL زنده یا ارسال OTP/Bearer روی HTTP مجاز نیست.

### probe عمومی کم‌اطلاعات

فرمان زیر فقط دستور اپراتور است، در این تحویل علیه سرور اجرا نشده است.
origin را خود اپراتور تأیید کند؛ `http://94.184.32.84` فقط آخرین مرجع اعلام‌شدهٔ قبلی است، نه
آدرس دوباره‌تأییدشده. روی HTTP فقط همین probe ناشناس را اجرا کنید؛ موفقیت آن TLS امن یا login را ثابت نمی‌کند.
هیچ certificate bypass مانند `SkipCertificateCheck` به نمونه اضافه نکنید.

```powershell
$baseUri = [Uri](Read-Host 'Origin دقیق API بدون مسیر، query یا credential')
if (!$baseUri.IsAbsoluteUri -or $baseUri.Scheme -notin @('http','https') -or
    $baseUri.AbsolutePath -ne '/' -or $baseUri.UserInfo -or $baseUri.Query -or $baseUri.Fragment) {
    throw 'Origin معتبر لازم است.'
}
function Get-MakanCoreProbe {
    param([ValidateSet('/health/live','/health/ready')][string]$Route)
    $status = $null
    $safeBody = $false
    try {
        $response = Invoke-WebRequest -Uri ([Uri]::new($baseUri, $Route)) `
            -UseBasicParsing -TimeoutSec 20 -MaximumRedirection 0 -ErrorAction Stop
        $status = [int]$response.StatusCode
        $safeBody = $response.Content.Trim() -in @('Healthy','Unhealthy')
    } catch {
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        # exception، body و header خام وارد evidence نمی‌شوند.
    }
    [pscustomobject]@{Utc=[DateTime]::UtcNow;Route=$Route;Status=$status;ExpectedMinimalBody=$safeBody}
}
Get-MakanCoreProbe '/health/live'
Get-MakanCoreProbe '/health/ready'
```

انتظار: live برابر 200/Healthy مستقل از SQL و ready برابر 200/Healthy فقط با config/ring/SQL/storage
سالم؛ failure core باید 503 باشد. redirect/404/timeout/بدنهٔ غیرمنتظره را PASS ننامید.
برای detail فقط پس از آماده‌شدن HTTPS یا کانال مدیریتی امن و نشست مجاز، health/jobs با `OperationsRead`
و notifications با `NotificationsRead` مشاهده شوند؛ credential در command history یا evidence ذخیره نشود.

### سناریوهای چرخهٔ عمر با مجوز maintenance

اسامی تأییدشده و module/backup از runbook IIS لازم‌اند. فرمان‌های زیر disruption دارند؛ اپراتور
فقط سناریوی تصویب‌شده را اجرا کند، نه همهٔ بلوک را کورکورانه. هیچ `Restart-Computer`، kill process
یا Stop SQL برای اجرای خودکار ارائه نشده است.

```powershell
# فقط پس از تعیین siteName/poolName معتبر و کنترل‌شده در runbook IIS:
Get-WebAppPoolState -Name $poolName
& "$env:windir\system32\inetsrv\appcmd.exe" list wp "/apppool.name:$poolName"

# سناریوی recycle کنترل‌شدهٔ نسخهٔ hardened، با پنجرهٔ مجاز:
Restart-WebAppPool -Name $poolName
Get-MakanCoreProbe '/health/live'
Get-MakanCoreProbe '/health/ready'

# سناریوی جداگانهٔ Stop/Start کامل؛ توقف سایت به‌تنهایی توقف worker نیست:
Stop-Website -Name $siteName
Stop-WebAppPool -Name $poolName
Get-WebAppPoolState -Name $poolName
& "$env:windir\system32\inetsrv\appcmd.exe" list wp "/apppool.name:$poolName"
# پس از تأیید خروج process و ثبت شواهد، نه بلافاصله و بدون بررسی:
Start-WebAppPool -Name $poolName
Start-Website -Name $siteName
Get-MakanCoreProbe '/health/live'
Get-MakanCoreProbe '/health/ready'
```

اگر command با نام نامعتبر، عدم دسترسی، نبود نقش یا timeout شکست خورد، علت ثابت/کد و UTC ثبت شود،
نه config dump. یک probe بلافاصله بعد از start ممکن است هنوز unready باشد؛ recovery را تا window
مصوب اپراتور مشاهده کنید، ولی failure مداوم یا crash loop را با retry نامحدود پنهان نکنید.

| سناریو | شاهد مورد نیاز؛ همه فعلاً NOT_RUN |
|---|---|
| Fresh start | process درست، دو health، دسترسی core و key ring/storage پایدار |
| Graceful Stop/Start | خروج process، budget bounded، بازیابی claim منقضی و یک اثر durable مصنوعی |
| Hardened recycle/overlap | PIDهای کوتاه‌مدت، claim/fencing صحیح، canonical report و عدم اثر durable تکراری |
| Rapid restart در محیط آزمایشی مجاز | recovery نهایی، backoff/سقف retry و نبود hot loop؛ crash اجباری مجوز مستقل می‌خواهد |
| Reboot در maintenance | backup و راه بازگشت پیشاپیش؛ autostart/health/ring/storage/lease recovery پس از reboot |
| SignalR/Presence | قطع state حافظه‌ای مورد انتظار، reconnect و SQL Delta؛ hint از‌دست‌رفته تحویل قطعی نامیده نشود |

برای هر سطر، UTC شروع/پایان، commit، status، counts و reference شواهد sanitized ثبت شود؛
تحویل Push خارجی at-least-once است و provider acceptance جای دریافت دستگاه یا unique business effect نیست.

### alert، cleanup و recovery

- [ ] collector/exporter واقعاً به meterها متصل و age/availability snapshot دیده شده است؛ gauge
  مشترک صف در overlap دو instance sum نمی‌شود و Quiet Hours اعلان overdue ساختگی نمی‌سازد.
- [ ] canary بی‌خطر و کنترل‌شده تا دریافت واقعی alert توسط اپراتور آزمایش شده؛ بدون آن
  `OBSERVABILITY_EXPORTER` و `ALERT_DELIVERY` برابر `PENDING_INFRASTRUCTURE` می‌مانند.
- [ ] cleanup محدود فقط پس از grace/config صریح، inventory، ACL و تأیید owner فعال شده؛
  پیش‌فرض Disabled و `POLICY_PENDING` failure برنامه نامیده نشده است.
- [ ] job backup، off-server copy، failure alert و restore drill مقصد با RPO/RTO مصوب شاهد دارند؛
  backup موفق/VERIFYONLY یا drill LocalDB جای این موارد را نمی‌گیرد.

تا اجرای موفق gateهای واقعی، نتیجهٔ تحویل کد `PRODUCTION_SERVER_ACCEPTANCE_REQUIRED` است؛
HTTP-only همچنان `BACKEND_HTTPS=PENDING_INFRASTRUCTURE` و انتشار عمومی امن تأیید نشده است.
