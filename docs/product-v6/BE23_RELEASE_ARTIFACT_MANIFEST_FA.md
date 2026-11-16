# Manifest انتشار و اجرای مجدد BE-23

Baseline=`00bd8a98cbaedf30a198018952a6741e58d10cc4`؛
Branch=`feature/product-v6-backend-alignment`؛ Build=`Release`؛ Target=`net9.0`.
این سند مسیر و شواهد انتشار محلی را ثبت می‌کند؛ Deploy یا migration سرور انجام نمی‌شود.

## وضعیت تولید artifact

Source آزموده‌شده: `52b596a9892ae44e1ff6640b94516e96e4b2586c`؛ تاریخ ساخت `2026-11-16`.
مسیر خروجی خصوصی در مخزن اصلی:

```text
.artifacts/releases/20261116-52b596a-be23/
```

هر دو Publish، اجرای واقعی DLL منتشرشده و بازرسی ZIP موفق شدند. DLL دو محیط یکسان است؛
هر محیط فقط JSON متناظر خود و web.config صحیح را دارد. هیچ PDB، source یا assembly تست در ZIP نیست.
Source ZIP جزو تحویل نیست. بسته‌های دارای تنظیمات خصوصی، **PRIVATE / DO_NOT_DISTRIBUTE** هستند.

| تحویل | مقدار/نتیجه |
|---|---|
| Test ZIP | MakanApp-Test-20261116-52b596a.zip |
| Production ZIP | MakanApp-Production-20261116-52b596a.zip |
| Full idempotent SQL | migration-full-idempotent.sql؛ هشت پذیرش مستقل SQL موفق |
| OpenAPI | openapi.json؛۴۳۸ path،۴۹۴ operation،۷۸۳ schema |
| شمار/آخرین Migration | ۷۵ / 20261114183321_ExtendProductNotificationCoverage |
| Server migration baseline | UNKNOWN |
| migration-delta.sql با فرض مقصد واقعی | تولید نمی‌شود؛ history سرور موجود نیست |
| rollback واقعی سرور | اجرا نشده؛ artifact و config فعلی سرور باید توسط اپراتور حفظ شوند |

Hashهای SHA-256:

```text
Test ZIP: 0070520a296ba65fcccc2ea5be2502b307d52a8e50580fe1aaa0237ffda5ff51
Production ZIP: bc3af754c7dc5f4eea0f85f53336f838dfa68f6fef340e024547c65bd0d62f65
Full SQL: 5e0f24ae363bee047bae8d6cf1cc4473bacc8f2de5ceb0acaa9770509f445fa5
OpenAPI: 616e22cf15bc67b604ea12f3348c4612d0289ebaf413eb4d83635c65770cf4f0
API DLL (both): cbc9fa247b31d9bb7e573588708bd53abeb966e1f2c0666289a206609fb8845c
```

`RELEASE_MANIFEST.json`، `BINARY_SHA256.json` و `SHA256SUMS` کنار ZIPها هستند.
`PUBLISH_AND_ROLLBACK_FA.md` راهنمای مستقل همین تحویل است. دو ZIP قدیمی محلی در
`rollback-candidates` با hash اصلی تطبیق داده شدند؛ فقط کاندید محلی‌اند و نسخهٔ مستقر سرور فرض نمی‌شوند.
Source بسته‌ها Commit کد بالاست؛ Commit نهایی مستندات، DLLها را تغییر نمی‌دهد.

Validation نهایی: Restore و Build موفق، صفر Warning/Error؛۱۳۵۰ Unit،۲۰۲۸ Integration و۱۴ Architecture،
جمع۳۳۹۲ موفق، صفر Failed/Skipped؛ هر۱۱۷ تست Zibal موفق. پس از حذف guard تکراری تست، Build و۲۹ focused موفق.
ده اجرای مستقل Realtime هرکدام۸ case موفق داشتند؛ علت ناپایداری تاریخی هنوز UNPROVEN است.
در اجرای نهایی خود خروجی Test و Production: live/ready=200، me بدون نشست=401 و فایل private config=404؛
Mode واقعی Host به‌ترتیب Mock و Real تأیید شد. Smoke فقط با SQL/Storage disposable و providerهای
Unavailable در process اجرا شد؛ هیچ فایل خصوصی یا تنظیم خروجی برای این آزمون تغییر نکرد.

## تفاوت محیط‌ها

| موضوع | Test | Production |
|---|---|---|
| نام محیط در web.config | Test | Production |
| OTP | Enabled=false / AllowMockAuthentication=true | Enabled=true / AllowMockAuthentication=false |
| DB مورد انتظار | MakanApp_Test؛ مقدار خصوصی موجود حفظ می‌شود | MakanApp؛ مقدار خصوصی موجود حفظ می‌شود |
| Storage مصوب | C:\MakanAppData\Test\Storage | C:\MakanAppData\Production\Storage |
| تنظیمات محیط دیگر | در بسته نیست | در بسته نیست |
| داده/secret/کلیدها | اختصاصی محیط تست | اختصاصی محیط عملیاتی |

Schema برابر است؛ Connection String و کلیدهای واقعی در این سند یا Manifest عمومی نوشته نمی‌شوند.
فایل‌های private موجود خالی، حدسی یا با template جایگزین نمی‌شوند.
کد و تنظیم ملی‌پیامک، AI Service Auth و Knowledge Service Auth دست‌نخورده‌اند.

## Migrationهای بعد از آخرین بستهٔ محلی زیبال

آخرین بستهٔ محلی در پوشهٔ `20261110-e5bd3ea-zibal-final-dual-storage-isolated` از
Source=`e5bd3eae658991585ca09f581f6236351857b3b8`، Migration آخر
`20261110081503_AddZibalPaymentIntegration` را گزارش می‌کند. این شاهد نسخهٔ سرور نیست.

```text
20261112145324_AddProductV6ProfileAndAddressBook
20261112151714_AddSupportPrivacyAndAssistantContexts
20261112152027_EnforceAssistantAndDeletionSessionOwnership
20261112153121_AddVersionedSchoolApplications
20261112160026_ExtendProductNotificationTargets
20261112165826_AddVerifiedPhoneChangeRequests
20261113065124_CompleteSchoolEstablishmentWorkflow
20261113103326_FinalizeSchoolManagerAndPaidProvisioning
20261114140323_AddAccountDeletionLifecycle
20261114183321_ExtendProductNotificationCoverage
```

ترتیب را EF تعیین می‌کند؛ تغییر دستی history یا schema برای عبور از constraint مجاز نیست.
BE-15 و خود BE-23 Migration جدید ندارند. rollback محدود جایی آزموده می‌شود که Down موجود
اجازه دهد؛ Down محافظت‌شدهٔ Notification در صورت وجود receipt/envelope جدید عمداً رد می‌شود.

## تولید SQL و آزمون مستقل

از ریشهٔ مخزن و source معتبر، پس از Release Build موفق:

```powershell
dotnet ef migrations has-pending-model-changes --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --configuration Release --no-build
& ./tools/production/export-migration-sql.ps1 -Configuration Release -NoBuild -OutputPath .artifacts/my-release/migration-full-idempotent.sql
& ./tools/production/test-migration-sql.ps1 -Configuration Release -ConfirmLocalDisposableDatabase
```

خروجی exporter از `dotnet ef migrations script 0 <latest> --idempotent` به دست می‌آید و برای
جلوگیری از compile زودرس DDL/backfill به قالب اجرایی ایمن تبدیل می‌شود. فایل raw EF صرفاً مرجع است.
هیچ‌یک از فرمان‌های بالا DB عادی برنامه را migration نمی‌کنند؛ ابزار آزمون فقط LocalDB با نام یکتا می‌سازد.

برای اجرای واقعی، ابتدا روی اتصال تازه، بدون تراکنش باز، DB مقصد و history را به‌صورت خواندنی بررسی کنید:

```sql
SELECT DB_NAME() AS DatabaseName, @@TRANCOUNT AS OpenTransactions, XACT_STATE() AS TransactionState;
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
```

سپس Backup و hash فایل تأیید و با migration principal جدا، SQL کامل را در SSMS یک‌جا اجرا کنید؛
SQLCMD Mode لازم نیست. روش خط فرمان با اتصال امن provision‌شده و بدون رمز در command history:

```powershell
sqlcmd -S '<approved-sql-instance>' -d '<approved-database>' -E -I -b -i '.\migration-full-idempotent.sql'
if ($LASTEXITCODE -ne 0) { throw 'Migration failed; do not start rollout.' }
```

نمونهٔ `-E` فقط برای Integrated Security مجاز است؛ روش SQL credential باید خصوصی و مطابق DBA
تأمین شود. `-b` شکست SQL را به شکست فرمان تبدیل می‌کند؛ constraint یا دادهٔ ناسازگار دور زده نشود.
در SSMS، تب Messages و history پس از اجرا نیز بررسی شوند. rollback خودکار دیتابیس ارائه نمی‌شود.

Deltaهای کاندید فقط با نام صریح baseline محلی/مبدأ مشخص و hash جدا تحویل می‌شوند؛ هیچ‌کدام
`migration-delta.sql` تأییدشدهٔ مقصد واقعی نامیده نمی‌شوند. اگر history مقصد آن مبدأ را تأیید نکرد،
کاندید را اجرا نکنید؛ SQL کامل idempotent هم اختلاف schema دستی را ترمیم نمی‌کند.

## گرفتن Publish توسط توسعه‌دهنده

فایل‌های خصوصی هر دو محیط باید از قبل با روش امن موجود باشند؛ نمونهٔ عمومی credential ندارد.
outputهای تازه انتخاب کنید تا فایل محیط قبلی باقی نماند:

```powershell
dotnet restore MakanApp.sln
dotnet build MakanApp.sln --configuration Release --nologo
dotnet test MakanApp.sln --configuration Release --no-build --logger 'console;verbosity=minimal'
dotnet publish MakanApp.Api/MakanApp.Api.csproj --configuration Release --no-restore -p:PublishProfile=TestIis --output .artifacts/my-release/test
dotnet publish MakanApp.Api/MakanApp.Api.csproj --configuration Release --no-restore -p:PublishProfile=ProductionIis --output .artifacts/my-release/production
```

وجود MakanApp.Api.dll، deps/runtimeconfig، web.config و JSON محیط متناظر بررسی شود؛
Test/Development، Source، bin/obj داخلی، test assembly و PDB در بستهٔ Production قرار نگیرند.
برای Hash از `Get-FileHash -Algorithm SHA256` استفاده کنید؛ SHA256SUMS جای encryption یا custody امن نیست.

## Backup و Restore با ابزار فعلی

Connection String را فقط در environment امن عملیات provision کنید؛ مقدار آن را در فرمان یا سند ننویسید.
نمونهٔ فرمان با pathهای خصوصیِ از قبل تأییدشده و توقف واقعی همهٔ writerها:

```powershell
& ./tools/production/recovery/Invoke-MakanRecovery.ps1 -Backup -ConnectionEnvironmentVariable MAKAN_RECOVERY_CONNECTION -BundlePath $privateBundle -ApplicationRoot $applicationRoot -StorageRoot $storageRoot -DataProtectionRoot $approvedRingRoot -WritersStopped
& ./tools/production/recovery/Invoke-MakanRecovery.ps1 -Restore -ConnectionEnvironmentVariable MAKAN_RECOVERY_CONNECTION -BundlePath $privateBundle -ApplicationRoot $applicationRoot -TargetDatabase $newApprovedRecoveryDatabase -TargetStorageRoot $newStorageRoot -TargetDataProtectionRoot $newRingRoot -ExpectedManifestHash $verifiedManifestHash -MigrationListPath $artifactMigrationList
```

ابزار فعلی فقط SQL محلی Windows و مقصد تازه با prefix کنترل‌شدهٔ Recovery را پشتیبانی می‌کند؛
برای SQL راه‌دور یا Production، DBA و runbook مستقل لازم‌اند. هیچ فرمان این سند در این Task
روی سرور اجرا نمی‌شود. اگر Ring در bundle باشد، حذف آن یا معرفی‌نکردن مقصد به failure پیش از
ساخت دیتابیس منجر می‌شود؛ tool کلیدها یا secretها را چاپ نمی‌کند. رمزگذاری/محافظت کلیدها و
ApplicationName/purpose مؤثر در مقصد باید مستقل از وجود فایل‌ها پذیرفته شوند.

## جایگزینی در IIS؛ فقط دستور اپراتور

۱. Commit/ZIP/hash، history مقصد و compatibility نسخهٔ قبلی را ثبت کنید.
۲. پوشهٔ برنامهٔ فعلی، private config، Identity key، SQL، Storage و Ring/protector context را امن Backup کنید.
۳. traffic و App Pool را در maintenance window متوقف و خروج processهای قبلی را تأیید کنید.
۴. فایل برنامهٔ تأییدشده را جایگزین کنید؛ config خصوصی سرور، Storage و Ring را overwrite/delete نکنید.
۵. Migration را جدا با principal مخصوص و طرح بازگشت اجرا/بررسی کنید؛ Startup migration وجود ندارد.
۶. محیط Test/Production، overrideهای IIS، هویت App Pool، profile، path و ApplicationName/purpose سازگار را حفظ کنید.
۷. App Pool را Start و `/health/live`، `/health/ready` و Smoke مجاز را بررسی کنید.
۸. backlog، خطا، schema/history و نشانه‌های Realtime را پایش کنید؛ provider/دستگاه واقعی gate جداست.

برای حفظ discriminator پیش‌فرض Data Protection، تعویض بی‌بررسی physical/content-root path مجاز نیست.
IIS profile/DPAPI/certificate و master-key custody می‌توانند در بازیابی Ring مؤثر باشند؛ XML تنها
برای انتقال به هویت/ماشین دیگر تضمین کافی نیست. setting منسوخ MFA برای رفع این موضوع نسازید.

## Rollback و Blockerها

Rollback برنامه: traffic را متوقف، artifact شکست‌خورده و evidence را حفظ، نسخهٔ قبلی تأییدشده
و config سازگار را بازگردانید و Health/Smoke را تکرار کنید. artifact قدیمی محلی صرفاً کاندید است؛
آخرین نسخهٔ مستقر سرور و compatibility آن در این Task اثبات نشده‌اند.
Rollback برنامه به معنی Down migration نیست؛ برای schema ناسازگار از forward-fix یا Restore مصوب
DB+Storage+Ring سازگار استفاده شود. DB عادی یا فایل‌های تاریخی برای سبزکردن Health حذف نشوند.

Push runtime/SMS عمومی، اجرای حذف حساب، شرایط تجاری واقعی آموزشگاه، retention حقوقی،
CafeBazaar/Myket/iOS، exporter/alert و RPO/RTO همان gateهای مستقل فعلی‌اند.
Library عمومی یا تأیید ReviewRequired با این انتشار فعال نمی‌شوند. `BE24_FULL_ACCEPTANCE=FAIL`.

[گزارش آموزشی و Validation](BE23_OPERATIONS_READINESS_REPORT_FA.md)،
[چک‌لیست](../production/RELEASE_CHECKLIST.md)، [IIS](../production/DEPLOYMENT_IIS.md)،
[Backup/Restore](../production/BACKUP_RESTORE.md) و
[Worker Recovery](../operations/WORKER_RELIABILITY_AND_RECOVERY.md) مرجع تکمیل شواهد هستند.
