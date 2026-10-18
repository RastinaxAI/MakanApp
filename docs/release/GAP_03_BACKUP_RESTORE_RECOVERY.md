# GAP-03 — پشتیبان‌گیری و بازیابی قابل‌آزمایش

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

تاریخ بررسی: ۲۰۲۶-۱۰-۱۵. محدوده: Backend، بدون تغییر schema، provider، AI یا سیاست محصول.

## ۱. نتیجه و مرز ادعا

پیش از این مرحله، STEP 16 تنها backup/restore یک جدول marker روی LocalDB را ثابت می‌کرد. اکنون ابزار تکرارپذیر و drill واقعی SQL Server + فایل + ASP.NET Core + authorization وجود دارد. معیار این درخواست، بازیابی کامل در محیط disposable است؛ این شاهد **گواه آماده‌بودن زیرساخت Production، IIS، job زمان‌بندی‌شده یا off-server copy نیست**.

`RPO/RTO NOT YET PRODUCT-APPROVED`

هیچ recovery model، retention، policy حذف کاربر/پیام، migration یا تنظیمات ملی‌پیامک تغییر نکرده است. GAP-05 و سایر gapها در این مرحله بسته نشده‌اند.

## ۲. فهرست دارایی‌های بازیابی

پنجره مجاز اتلاف داده برای همه موارد پایدار زیر هنوز مصوب نیست. «پوشش» به معنای پوشش ابزار/drill است، نه وجود job فعال در سرور مالک.

| منبع واقعی | مالک/محتوا | روش backup و restore | حساس؟ | پوشش فعلی |
|---|---|---|---|---|
| SQL Server / `MakanDbContext` | Identity: Person، User، Credential، OTP hash، Session، RefreshToken؛ Organization، Membership، RoleAssignment، Invitation، Guardian | full backup تمام DB؛ restore به DB جدید | بله؛ PII و hash امنیتی | تمام جداول؛ نمونه‌های مصنوعی و session موجود بررسی شدند |
| همان DB | Academic: دوره/درس/کلاس، Enrollment، TeacherAssignment، Session، Attendance و revision | همان full backup؛ حفظ PK/FK/index/constraint/rowversion | بله | داده آموزشی و تعداد سطر تمام جداول تطبیق داده شدند |
| همان DB | Assessment: Assignment/Version/Recipient/Submission/Evaluation/GradeRelease؛ Exam/Version/Question/Attempt/Answer/FinalReceipt/GradeRelease | همان full backup؛ نه export ناقص از چند جدول | بله | assignment و exam با نمره منتشرشده در fixture موجودند |
| همان DB | Messaging: conversation، participant، message/revision/attachment، visibility، contact/privacy، moderation/evidence، delta cursor و realtime outbox | همان full backup؛ تاریخچه و tombstone حفظ می‌شوند | بسیار حساس | پیام/metadata مصنوعی بررسی شدند؛ محتوای واقعی کاربر استفاده نشد |
| همان DB | Notification، preferences، delivery attempts؛ `platform.OutboxMessages`، `messaging.RealtimeOutbox`؛ `reports.ReportExportJobs` | همان full backup؛ وضعیت claim/retry/completed نیز داده است | بله | notification و retry یک outbox واقعی حفظ شدند؛ صف‌ها reset نشدند |
| همان DB | Commerce و Platform IAM، provisioning و `platform.AdminAuditLogs` | همان full backup؛ audit نباید حذف/بازنویسی شود | بله | تمام جدول‌ها پوشش دارند؛ audit marker مصنوعی بررسی شد؛ پرداخت واقعی اجرا نشد |
| همان DB | `storage.FileAssets`، metadata و SHA-256؛ `dbo.__EFMigrationsHistory` | در SQL backup؛ با bytes و artifact مقایسه می‌شوند | بله | ۳۵ migration و metadata دو فایل Ready تطبیق یافتند |
| دیسک محلی `Storage:RootPath` | `LocalFileStorage`: upload، attachment، evidence و CSV خروجی `ReportExportOutputStore` | snapshot تمام فایل‌های root در توقف writerها؛ بازگردانی در root جدید | بسیار حساس | دو فایل دارای metadata + یک orphan مصنوعی؛ hash و size بررسی شدند |
| artifact و تنظیمات محیط | Api/.NET 9، migrationهای همان release، IIS binding/identity، CORS/proxy/hosts و انتخاب provider | artifact نسخه‌بندی‌شده و فهرست تنظیمات خارج DB؛ provision مجدد توسط اپراتور | بخشی حساس | ابزار باینری/config را backup نمی‌کند؛ در checklist الزامی‌اند |
| secret store / محیط خارج Git | کلید پایدار Identity و secretهای فعال محیط | بازیابی از مخزن امن مستقل با reference، نه کپی در manifest | بله | در drill فقط کلید تصادفی مشترک در حافظه؛ secret واقعی خوانده/کپی نشده است |

Object Storage adapter در runtime فعلی وجود ندارد. محل export جدا از storage نیست. کلید فایل واقعی از `FileUploadPolicy.CreateStorageKey` به شکل `yyyy/MM/{FileAssetId:N}-{randomGuid:N}` ساخته می‌شود؛ نام اصلی فایل مسیر فیزیکی نیست. `RetainedAtUtc` نشانگر حفظ تاریخچه است؛ Status همچنان Ready می‌ماند.

## ۳. SQL Server: راهبرد پایلوت و آینده

راهبرد ساده فعلی، **full backup مستقل با توقف نوشتن برنامه و snapshot فایل متناظر** است. ابزار `COPY_ONLY, CHECKSUM` می‌گیرد و سپس `RESTORE VERIFYONLY ... WITH CHECKSUM` اجرا می‌کند. COPY_ONLY مبنای differential موجود را تغییر نمی‌دهد؛ ابزار نه `INIT/FORMAT` دارد و نه backup قبلی را overwrite می‌کند. COMPRESSION به‌صورت اجباری فرض نشده است چون edition/محیط باید بررسی شود. [مرجع Microsoft](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/copy-only-backups-sql-server)

VERIFYONLY جای restore واقعی و بررسی برنامه را نمی‌گیرد. restore با `FILELISTONLY` نام تمام فایل‌های منطقی را کشف می‌کند، برای هر D/L، `MOVE` به فایل جدید در مسیر پیش‌فرض instance می‌سازد و با CHECKSUM/RECOVERY بازیابی می‌کند؛ سپس `DBCC CHECKDB` اجرا می‌شود. FILESTREAM یا نوع ناشناخته به DBA ارجاع می‌شود، نه حدس مسیر. [مرجع Microsoft](https://learn.microsoft.com/ga-ie/sql/relational-databases/backup-restore/restore-a-database-to-a-new-location-sql-server)

تناوب full backup باید با مالک تصویب شود. فعلاً job یا PITR/log chain پیاده/فعال نشده و full backup تضمین point-in-time recovery نیست. در آینده، اگر RPO کوتاه‌تر لازم شد، DBA باید recovery model مناسب، full/log chain، زمان‌بندی، monitoring، ظرفیت log و restore زنجیره را طراحی و جداگانه drill کند؛ صرف FULL بودن recovery model به معنی فعال‌بودن log backup نیست. ابزار فعلی recovery model را تغییر نمی‌دهد و `COPY_ONLY` جای برنامه مدیریت log در FULL را نمی‌گیرد.

## ۴. سازگاری DB و فایل

قبل از backup، همه writerها را واقعاً drain/stop کنید: تمام App Pool/instanceها، workerها، uploadها، jobهای export، cleanup و ابزارهای DDL/DML خارجی. `-WritersStopped` تأیید اپراتور است، نه مکانیزم توقف خودکار IIS یا distributed snapshot. توقف باید تا پایان SQL backup، کپی فایل و ثبت manifest ادامه یابد.

ابزار ابتدا metadata/فایل‌ها را بررسی، full backup را verify و همه bytes را کپی می‌کند؛ hash/size مبدأ و مقصد و metadata فایل دوباره تطبیق داده می‌شوند. این کنترل‌ها جای توقف writerها را نمی‌گیرند. نام bundle و timestamp، DB و فایل را به یک واحد recovery متصل می‌کنند.

`manifest.json` شامل نسخه قالب، نام DB، زمان UTC، SHA-256 فایل `.bak`، migration IDs، StorageKey/Status/Size/Hash و تمام فایل‌های snapshot است؛ **هیچ secret/config value یا نام/شماره شخص در manifest درج نمی‌شود**، اما خود backup و metadata همچنان حساس‌اند. digest manifest باید جدا از bundle در کاتالوگ امن اپراتور ثبت شود؛ hash کنار فایل، امضای دیجیتال یا حفاظت مستقل در برابر مهاجم نیست.

اعتبارسنجی fail-closed است:

- تمام FileAssetهای Ready، از جمله retained، باید bytes با hash/size صحیح داشته باشند.
- هر فایل ثبت‌شده snapshot باید پس از restore حاضر و صحیح باشد؛ فایل اضافه نیز mismatch است.
- فایل بدون metadata با `UnreferencedFiles` گزارش و حفظ می‌شود؛ orphan خودکار حذف نمی‌شود.
- metadata غیر Ready با bytes غایب، جدا با `NonReadyMissing` گزارش می‌شود؛ Pending/Rejected/Deleted لزوماً فایل قابل دانلود ندارند.
- مسیر نسبی، `..`، مسیر مطلق، ADS، نام رزروشده Windows و junction/symlink رد می‌شوند.

## ۵. secretها و تنظیماتی که باید دوباره provision شوند

| نام واقعی | نقش و اقدام recovery |
|---|---|
| `ConnectionStrings__MakanDatabase` | اتصال runtime به **DB بازیابی‌شده**؛ principal runtime با least privilege، جدا از backup/restore/migration operator؛ SQL TLS معتبر در Staging/Production |
| `Identity__SecurityKey` | HMAC-SHA256 برای OTP، session و refresh-token hashes؛ JWT signing key نیست. همان کلید پایدار از مخزن امن لازم است؛ کلید متفاوت session/OTP قدیمی را نامعتبر می‌کند. حداقل ۳۲ بایت UTF-8 طبق guard موجود |
| `Sms__Provider` و در صورت فعال بودن `Sms__MeliPayamakOtp__ServiceToken` | اتصال OTP ملی‌پیامک؛ token فقط از secret store. BaseUrl و تنظیمات OTP باید مطابق محیط معتبر باشند. کد/config آن تغییر نکرده و drill SMS زنده ندارد |
| `Storage__RootPath` و گزینه‌های storage/export | مسیر پایدار جدید و ACL identity برنامه؛ گزینه‌های اندازه/type/lifecycle همان release. root قدیمی را تصادفی متصل نکنید |
| `ASPNETCORE_ENVIRONMENT`، `AllowedHosts`، `Cors__AllowedOrigins__*`، `Hosting__RequireHttps`، `ReverseProxy__*`، `Swagger__Enabled` | تنظیمات fail-fast و HTTP/TLS/proxy؛ از inventory استقرار تأییدشده، نه Development به‌جای Production |
| `Payments__Provider`، `Messaging__ContentSafetyProvider`، `Notifications__PushProvider`، `Notifications__SmsProvider` | adapterهای runtime فعلی real payment/general push/SMS/safety secret ندارند؛ حالت Unavailable را با secret یا provider ساختگی عوض نکنید |
| `AiService__Enabled`، `AiService__BaseUrl` و `AiService__Authentication__CurrentKeyId` / `AiService__Authentication__CurrentKey` در صورت فعال بودن | فقط inventory وابستگی موجود؛ بازیابی referenceها توسط مالک AI. هیچ فایل/secret/قرارداد AI در GAP-03 تغییر نکرده و AI در drill بررسی نشده است |
| IIS HTTPS certificate/private key، SQL trust chain، Windows service/App Pool identity و SQL logins/grants | دارایی زیرساخت خارج DB برنامه؛ با رویه مدیریت کلید/حساب محیط بازیابی شوند. backup دیتابیس، Windows identity یا login سرور جدید را نمی‌سازد |

فایل‌های فعلی `LocalFileStorage` encryption اختصاصی برنامه ندارند؛ کلید file encryption یا Data Protection key ring ساختگی به inventory اضافه نشده است. اگر DBA در محیط واقعی TDE/backup encryption فعال کرده باشد، certificate/private key مربوط به SQL نیز باید با رویه DBA و جدا از backup قابل بازیابی باشد؛ این قابلیت در drill محلی ادعا نشده است. SecurityKey واقعی، ServiceToken و secret store مالک خوانده نشده‌اند.

## ۶. ابزار، امنیت و مرز پلتفرم

مسیر ابزار: `tools/production/recovery/`.

- `MakanRecovery.psm1`: backup، manifest/hash، ACL، مقصد امن، restore و integrity؛ SQL با timeout محدود، بدون چاپ connection string.
- `Invoke-MakanRecovery.ps1`: ورودی‌های اجباری Backup/Restore؛ خطا با exit 1 و مرحله امن `PHASE`، بدون raw provider error.
- `Test-MakanRecovery.ps1`: آزمون آفلاین guardها، nested manifest، missing/corrupt bytes و حفظ orphan.

این نسخه مخصوص **Windows PowerShell 5.1 و SQL Server محلی همان host** است؛ LocalDB نیز پشتیبانی می‌شود. اتصال راه‌دور، UNC، SQL Server لینوکسی یا object store با این ابزار ادعای پشتیبانی ندارند؛ DBA باید روی host سازگار یا با runbook متناظر کار کند. نسخه SQL مقصد باید قابلیت restore نسخه مبدأ را داشته باشد؛ downgrade نسخه SQL راه recovery نیست.

bundle و storage مقصد باید **از قبل وجود نداشته باشند** و خارج publish/web root و جدا از هم باشند. هیچ پارامتر overwrite/REPLACE، DROP، rollback کاربران، purge یا cleanup وجود ندارد. DB مقصد فقط `MakanApp_Recovery_[A-Za-z0-9_]{8,80}` و نام جدید است. در شکست، منابع جزئی حفظ می‌شوند؛ برای retry نام و مسیر تازه انتخاب کنید و cleanup را با تأیید جدا انجام دهید.

ACL پوشه bundle قبل از نوشتن داده محدود می‌شود: current operator، SYSTEM و در صورت نیاز identity صریح سرویس SQL. برای SQL معمولی `-SqlServiceIdentity` لازم است؛ برای LocalDB identity اپراتور کافی است. مسیر parent، دیسک SQL و مسیر انتقال نیز باید توسط اپراتور بررسی شوند. App Pool نباید اجازه خواندن bundle داشته باشد. ابزار rootهای ارائه‌شده را از نظر هم‌پوشانی با ApplicationRoot کنترل می‌کند؛ اپراتور باید همه web rootهای دیگر میزبان را هم بررسی کند. route جدیدی برای backup ساخته نشده است.

backup فعلی رمزگذاری نرم‌افزاری جدید انجام نمی‌دهد. ACL، encryption-at-rest و انتقال رمز‌شده باید با ابزار/سیاست تأییدشده محیط اعمال شوند؛ فایل `.bak` و فایل‌های کپی‌شده plaintext حساس‌اند. کپی صرف روی دیسک همان VPS، حفاظت در برابر از دست رفتن سرور نیست.

`REQUIRED_FROM_PROJECT_OWNER`:

- مقصد/storage/provider خارج سرور و failure domain مستقل؛ هیچ vendor انتخاب نشده است.
- retention مصوب backup، مسئول job، تناوب و هشدار شکست/تأیید دریافت کپی.
- انتظار encryption و custody/recovery کلید آن.
- access model، اپراتورهای مجاز، محل امن ثبت manifest digest و آزمایش دسترسی اضطراری.
- RPO/RTO کسب‌وکاری، window توقف و rehearsal روی topology واقعی.

backup retention مستقل از retention داده کاربر، tombstone و moderation evidence است؛ سند Messenger هنوز بعضی سیاست‌های محصول را حل‌نشده می‌داند. در این مرحله هیچ مدت قانونی، حذف خودکار یا retention محصول تعریف نشده است. نمونه عملیاتی پیشنهادی می‌تواند «full backup در هر پنجره مورد توافق» باشد؛ تعداد نسخه‌ها/مدت نگه‌داری باید تصویب شوند.

## ۷. Runbook دقیق

۱. اپراتور مجاز، incident/reason، release commit، نسخه SQL/.NET 9، فضای کافی، دسترسی SQL برای backup/restore/create DB و مسیرهای خارج وب را ثبت کند. محیط مقصد disposable و outbound آن ایزوله باشد؛ restore داده واقعی به محیط عمومی ممنوع است. artifact سازگار و secret references را از محل امن فراهم کنید؛ از worktree آلوده artifact Production نسازید.

۲. اتصال source را از secret store در متغیر process با نام انتخابی مثلاً `MAKAN_RECOVERY_CONNECTION` قرار دهید؛ مقدار را در فرمان، history، transcript یا Git ننویسید. writerهای همه instanceها/workerها را متوقف کنید. نام یکتا و مسیر تازه انتخاب کنید. مثال مسیرها غیرمحرمانه‌اند و باید با میزبان تطبیق داده شوند:

```powershell
$bundle = Join-Path 'D:\MakanRecovery' ('pilot-' + [Guid]::NewGuid().ToString('N'))
powershell -NoProfile -File .\tools\production\recovery\Invoke-MakanRecovery.ps1 `
  -Backup -ConnectionEnvironmentVariable MAKAN_RECOVERY_CONNECTION `
  -StorageRoot 'D:\MakanData\files' -ApplicationRoot 'D:\MakanSite' `
  -BundlePath $bundle -WritersStopped -SqlServiceIdentity 'NT SERVICE\MSSQLSERVER'
if ($LASTEXITCODE -ne 0) { throw 'backup کامل نیست؛ writerها را طبق تصمیم incident مدیریت کنید.' }
```

identity مثال بالا فقط برای instance پیش‌فرض معمول است؛ identity واقعی سرویس را بررسی کنید. در LocalDB این پارامتر حذف می‌شود. خروجی موفق `BACKUP_VERIFIED` و `ManifestSha256` است؛ digest را مستقل از bundle نگه دارید. پس از موفقیت کامل، writerها با رویه استقرار از سر گرفته شوند. انتقال bundle به مقصد خارج سرور و تأیید digest/قابل‌خواندن‌بودن آن مرحله الزامی اپراتور است، نه کاری که این script انجام داده باشد.

۳. در محیط recovery، bundle سالم و digest مستقل را فراهم کنید؛ credential اپراتور target ممکن است با source متفاوت باشد. متغیر اتصال ابزار باید به instance مقصد محلی اشاره کند و Initial Catalog آن یک نام non-system معتبر و متفاوت از نام DB مقصد باشد؛ script فرمان restore را روی master اجرا می‌کند و DB موجود را تغییر نمی‌دهد. app و worker مقصد هنوز اجرا نشوند؛ egress providerها مسدود بماند.

۴. فهرست migrationهای **artifact قابل اعتماد همان release** را تهیه کنید؛ آن را از خود backup استخراج نکنید چون در آن صورت compatibility با باینری اثبات نمی‌شود. از ریشه source متناظر artifact:

```powershell
Get-ChildItem .\MakanApp.Infrastructure\Persistence\Migrations -Filter '*.cs' |
  Where-Object { $_.BaseName -match '^\d{14}_' -and $_.Name -notlike '*.Designer.cs' } |
  Sort-Object BaseName | ForEach-Object BaseName |
  Set-Content -LiteralPath 'D:\MakanRecovery\artifact-migrations.txt' -Encoding UTF8

$targetDatabase = 'MakanApp_Recovery_Drill_' + [Guid]::NewGuid().ToString('N')
$targetFiles = Join-Path 'D:\MakanRecovery' ('files-' + [Guid]::NewGuid().ToString('N'))
powershell -NoProfile -File .\tools\production\recovery\Invoke-MakanRecovery.ps1 `
  -Restore -ConnectionEnvironmentVariable MAKAN_RECOVERY_CONNECTION `
  -BundlePath $bundle -ApplicationRoot 'D:\MakanSite' `
  -TargetDatabase $targetDatabase -TargetStorageRoot $targetFiles `
  -ExpectedManifestHash '<digest-from-independent-approved-catalog>' `
  -MigrationListPath 'D:\MakanRecovery\artifact-migrations.txt'
if ($LASTEXITCODE -ne 0) { throw 'restore کامل نیست؛ محیط را منتشر نکنید.' }
```

خروجی موفق `RESTORE_VERIFIED`، بازه UTC و integrity counts است. عدم تطابق migrations باعث توقف می‌شود، نه `database update` خودکار. ابزار هیچ migration جدیدی اعمال نمی‌کند؛ upgrade احتمالی موضوع release جداگانه است.

۵. DBA runtime principal و mapping/grants مناسب را روی DB جدید بررسی کند. اپراتور به identity اختصاصی برنامه Modify روی **root فایل جدید** و حداقل دسترسی روی artifact بدهد؛ اتصال app را به DB جدید و StorageRoot را به مسیر جدید تغییر دهد. SQL login/password واقعی از مخزن امن و با TLS معتبر، نه از fixture LocalDB، تأمین شود. همان Identity SecurityKey را provision کنید؛ key اشتباه یا گم‌شده، sessionهای قبلی را قابل تأیید نمی‌کند. plan رسمی rotation/خروج اجباری کاربران موضوع مستقل امنیت است.

۶. برنامه را با artifact و environment واقعی و قواعد [IIS](../production/DEPLOYMENT_IIS.md) بالا بیاورید. startup migration یا downgrade اجرا نکنید. بررسی کنید:

- `/health/live` و `/health/ready` هر دو 200؛ readiness فقط DB connect و storage probe است، پس تطابق schema مستقل بالا همچنان لازم است.
- account معتبر، read مجاز همان سازمان، عدم read سازمان دیگر و Guardian معتبر/نامعتبر.
- دانلود فایل مجاز و CSV با hash/bytes صحیح؛ user بدون مجوز نباید با داشتن FileAssetId دسترسی بگیرد.
- تعداد و روابط موجودیت‌های آموزشی و نمره منتشرشده، audit/history و صف‌ها مطابق snapshot.

برای اجرای قابل تکرار بدون SMS زنده، تست زیر تمام این مراحل را روی داده مصنوعی و test provider موجود اجرا می‌کند:

```powershell
dotnet test tests/MakanApp.IntegrationTests --configuration Debug --nologo `
  --filter FullyQualifiedName~RecoveryDrillTests
```

این تست OTP در حافظه را فقط از fixture Testing می‌خواند؛ endpoint افشای OTP یا auth bypass Production ساخته نشده است. در سرور واقعی، ابزار [smoke](../production/RELEASE_CHECKLIST.md) و روش امن تأییدشده اپراتور باید استفاده شود؛ development provider را به Production نبرید.

۷. پیش از آزادکردن workerهای واقعی، network sandbox و providerها را بررسی کنید. Outbox/NotificationDelivery/RealtimeOutbox و export jobها از همان حالت snapshot برمی‌گردند؛ leaseها با زمان سرور و منطق claim موجود منقضی می‌شوند. هیچ reset عمومی status، تغییر processed marker یا replay دستی انجام ندهید. side effect خارجی بعد از backup ممکن است با DB عقب‌رفته دوباره درخواست شود؛ reconciliation و idempotency provider و تصمیم اپراتور لازم است، نه تضمین exactly-once. تست GAP-03 بقا و retry state را بررسی می‌کند، نه ارسال واقعی یا disaster-replay تمام workerها؛ GAP-09 جداست.

۸. زمان شروع/پایان، حجم داده، health، خطاها و نتیجه دسترسی را در evidence امن ثبت کنید. هیچ session/OTP/token در سند مشترک قرار نگیرد. تا پایان پذیرش محیط، DB/فایل جزئی را حفظ کنید. حذف منابع drill فقط با بررسی نام/مسیر مطلق و تأیید صریح؛ script عمداً cleanup ندارد.

## ۸. شواهد drill انجام‌شده

اجرای واقعی: **YES**، SQL Server LocalDB `(localdb)\MSSQLLocalDB`، DB منبع/مقصد یکتای disposable با تمام migrationهای پروژه؛ نه DB معمول Development یا Production. app میزبان واقعی ASP.NET Core TestServer با Controllers و EF SQL Server است؛ نه IIS/Kestrel یا ترافیک زنده. تمام workerها فقط در fixture recovery متوقف‌اند؛ production runtime تغییر نکرده است.

برای DB منبع و مقصد همین شاهد، `sys.databases.recovery_model_desc` به‌صورت read-only برابر `SIMPLE` و state برابر `ONLINE` مشاهده شد؛ log backup/PITR در این drill وجود ندارد.

یک اجرای موفق ثبت‌شده با run ID `4c4c3dfd3466431383a1003f119b397c`:

| شاهد | مقدار |
|---|---|
| backup شروع UTC | `2026-10-14T23:23:25.3757337Z` |
| restore شروع UTC | `2026-10-14T23:23:27.8241203Z` |
| پایان SQL+file verification UTC | `2026-10-14T23:23:28.3717942Z` |
| پایان smoke UTC | `2026-10-14T23:23:31.0622Z` |
| restore + smoke | `3.7959823 s`؛ provisioning/seed/source backup داخل این مدت نیست |
| DB | ۸۵ جدول با count برابر، ۳۵ migration برابر artifact؛ CHECKDB موفق |
| فایل | ۳ فایل؛ ۲ Ready شامل CSV و retained upload؛ ۱ orphan حفظ‌شده؛ missing غیر Ready = صفر |
| health و account | live=200، ready=200، session قبل backup پس از restore=200، کلید اشتباه=401، OTP تستی جدید موفق |
| authorization | read مجاز=200؛ cross-org=404؛ parent مجاز=200؛ child نامرتبط=404؛ پس از revoke=403 |

کلید مشترک تصادفی فقط در حافظه تست بود و وارد evidence/manifest/Git نشد. fixture جدید علاوه بر کنترل بالا، کلید اشتباه را با 401 بررسی می‌کند. تکرار restore با همان مقصد نیز رد می‌شود. هر اجرای suite دوباره DB+storage مستقل می‌سازد و evidence جدید را خارج Git در `%TEMP%\MakanApp_RecoveryDrill_<runId>\evidence.json` می‌نویسد. DBها، bundle و فایل‌های مصنوعی برای بررسی حفظ می‌شوند؛ هیچ داده مادی در این مرحله حذف نشد.

این مدت کوچک، SLA یا RTO قراردادی نیست؛ اندازه داده و topology واقعی متفاوت است. بازیابی از نابودی کامل VPS، انتقال encrypted/off-server، secret store واقعی و principalهای IIS در این محیط آزمایش نشده‌اند.

## ۹. تست‌ها و مسیر مطالعه

دو تست Integration در `RecoveryDrillTests.cs` اضافه شده‌اند؛ یکی ۲۴ assertion واقعی ابزار آفلاین را اجرا می‌کند، دیگری backup/restore SQL و فایل و app/auth را. fixtureهای موجود برای داده‌های Domain استفاده شده‌اند؛ test-only constructor کلید پایدار و DB/storage مستقل می‌گیرد و workerها را برای snapshot متوقف می‌کند. fixture معمول قبلی، API runtime و dependency direction تغییری ندارند. migration جدید: **ندارد**.

validation کامل اول پس از اضافه‌شدن ابزار: restore موفق، build با صفر warning/error، Unit=384، Integration=625 و Architecture=14؛ مجموع 1023 passed، صفر failed/skipped. تست‌های آفلاین مستقل نیز ۲۴ کنترل موفق داشتند. نتیجه اجرای نهایی و Git در گزارش تحویل ثبت می‌شود؛ این counts به worktree شامل تغییرات AI قبلی تعلق دارند، نه ادعای تغییر یا commit آن فایل‌ها.

مسیر مطالعه: `Invoke-MakanRecovery.ps1` → `MakanRecovery.psm1` → `RecoveryDrillTests.cs` → `MakanAppWebApplicationFactory.Recovery.cs` → fixture اصلی → `LocalFileStorage` و `ReportExportOutputStore` → `FileAsset`/configuration → migrations موجود. نکته آموزشی: DB backup با file snapshot یکی نیست؛ hash صحت bytes، CHECKDB ساختار SQL، و smoke صحت مجوز/کاربرد را بررسی می‌کنند و هیچ‌کدام جانشین دیگری نیستند.

## ۱۰. اقدام بعدی و محدودیت

مالک باید مقصد خارج سرور، encryption/access/retention و RPO/RTO را تصویب و job با alert و rehearsal روی سرور واقعی را اجرا کند. ابزار انتخاب vendor یا schedule نمی‌کند. پس از این موضوع، GAP-04 (MFA/step-up واقعی) طبق audit با تصمیم provider مشخص مطرح است؛ GAP-05 همچنان به policy مستقل نیاز دارد. هیچ ادعای بسته‌شدن همه ریسک‌های عملیاتی recovery یا آمادگی عمومی محصول از این مرحله استنتاج نمی‌شود.
