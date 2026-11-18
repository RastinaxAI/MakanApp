# تحویل بستهٔ Test و وضعیت پذیرش IIS — 2026-11-18

Source باینری: `484e4484a4de24dc4e31041da5dd2c0306abbb63`؛ شاخه
`feature/product-v6-backend-alignment`. مالک در همین مرحله تأیید کرد که دسترسی مدیریتی
آماده نیست و **فقط بسته و دستورها آماده شوند**. این گزارش پذیرش انجام‌نشدهٔ سرور را PASS
نمی‌نامد؛ Production و دیتابیس‌های عادی تغییر نمی‌کنند.

## ۱. خلاصهٔ مرحله

هدف، بستهٔ تازه از Source مصوب و SQL معتبر برای Test است. وضعیت سرور از build/test محلی
جداست. پوشهٔ خروجی تازه و خصوصی:
`.artifacts/releases/20261118-484e448-test/`؛ هیچ artifact قدیمی reuse یا overwrite نمی‌شود.
بسته پس از Validation موفق ساخته و با محتوای publish مقایسه شد؛ نتیجهٔ نهایی محلی پایین
ثبت شده است. اجرای سرور طبق تصمیم مالک به اپراتور واگذار شد.

## ۲. معماری

Domain، Application، Infrastructure و Api کد جدید یا تغییر لایه‌بندی ندارند. همان .NET9،
EF Core9/SQL Server، Onion/Modular Monolith و Controllerهای موجود منتشر می‌شوند. این مرحله
فقط عملیات انتشار و مستندسازی است؛ حل مسئلهٔ Deploy دلیل ساخت abstraction یا endpoint تازه نیست.

## ۳. فایل‌های مهم ایجادشده

| تحویل | مسئولیت و نکتهٔ آموزشی |
|---|---|
| همین گزارش | تفکیک Source، خروجی محلی، دسترسی سرور و نتیجهٔ واقعی پذیرش |
| `migration-full-idempotent.sql` در Release | زنجیرهٔ۷۶ Migration موجود؛ کامپایل مؤخر DDL و توقف/rollback در خطا؛ raw EF SQL تحویل اجرایی نیست |
| `DEPLOY_TEST_IIS_FA.md` در Release | دستورهای اپراتور با مقصد/Backup/history واقعی؛ Site/Pool/مسیر حدس زده نمی‌شوند |
| `RELEASE_MANIFEST.json`، `MIGRATION_INVENTORY.json` و `SHA256SUMS` | اتصال artifact به Source/Hash و ثبت صریح وضعیت server-not-run؛ hash، آزمون مجوز یا سلامت business flow نیست |
| `MakanApp-Test-20261118-484e448.zip` | فقط خروجی Publish معتبر؛ private config به معنی مجازبودن انتشار عمومی نیست |

پوشهٔ `private-backup` داخل Release، فقط Backup محافظت‌شدهٔ رایانهٔ توسعه است؛ Backup
سرور نیست، نباید Deploy شود و نباید کل پوشهٔ Release را ZIP کرد.

## ۴. فایل‌های مهم تغییرکرده

کد، csproj، solution، profile، source web.config و appsettings اصلی تغییر نمی‌کنند.
اصلاح لازمِ web.config برای ASPNETCORE_ENVIRONMENT و DOTNET_ENVIRONMENT هر دو Test و
روشن‌شدن Swagger/OpenAPI فقط در خروجی Test انجام می‌شود. Secretها و تنظیم معتبر مقصد
با فایل خالی/template یا مقدار ماشین توسعه جایگزین نمی‌شوند.

## ۵. جریان اجرای تحویل

`484e448` → restore → Release build → full tests → بررسی مدل EF → `TestIis` publish
→ validation خروجی/JSON/XML/نسخهٔ DLL → ZIP خصوصی و Hash → اپراتور سرور Test:
کشف IIS/DB → توقف writer اختصاصی → Backup معتبر → history واقعی → SQL کامل →
جایگزینی باینری با حفظ config/Storage/ring → Start → Health و پذیرش business flow.
هیچ مرحلهٔ سرور با محیط محلی یا TestServer شبیه‌سازی‌شده جایگزین گزارش نمی‌شود.

## ۶. دیتابیس

Migration تازه ساخته نشده است. موجودی۷۶ و آخرین نسخه
`20261118065656_AddUnmeteredAiOperations` است؛ این نسخهٔ قبلی، علامت `IsUnmetered` را در
`commerce.UsageReservations` و `commerce.UsageLedger` نگه می‌دارد. این Task مدل، Table، PK/FK/index/constraint/rowversion
تازه ندارد و schema دیتابیس عادی را تغییر نمی‌دهد.

SQL کامل تازه از exporter موجود تولید شد؛ SHA256:
`426e13ce3bdc719c02b7eaa194fdcd605efa9d5b5fcbb1b68953475ebd5b5db7`.
هشت پذیرش مستقل SQL روی چهار دیتابیس GUIDدار LocalDB موفق شدند: fresh، current retry،
بازتولید خطای تاریخی207، upgrade20→76 با داده، retry بدون بازنویسی، upgrade v6، rollback
در خطای تزریق‌شده و رد ambient transaction بدون rollback تراکنش caller. این دیتابیس‌های
موقت طبق ابزار موجود برای شواهد retained می‌مانند؛ MakanApp/MakanApp_Test نیستند.

history واقعی Test Server خوانده نشده است؛ before/after، Backup SQL و Migration سرور
`NOT_RUN/UNKNOWN` هستند. پس از Backup، فقط MakanApp_Test واقعی مجاز است؛ هر نام دیگر،
history نامعلوم/ناشناخته یا خطای SQL → توقف و عدم Start برنامه. حذف history و ALTER دستی ممنوع است.

## ۷. Endpointها و پذیرش مقصد

API جدید ایجاد نشد. پیش از Deploy، GETهای عمومی `/health/live`، `/health/ready` و
`/openapi/v1.json` روی `http://94.184.32.84:8090` پاسخ200 دادند؛ Source مستقر احراز نشده
است و این نتایج، **پذیرش بستهٔ جدید نیستند**. هیچ login یا mutation روی سرور انجام نشد.

پس از نصب مجاز، Mock OTP، Chat بدون Subscription با نشست معتبر، Knowledge
Ingest/Ready/Retrieve/RAG و revoke، history/inbox/pagination دو حساب، SignalR و REST
reconcile، official ACL و upload/download/hash/restart persistence باید روی همان سرور
تست شوند. Contract دقیق ورودی‌ها/مجوزها در OpenAPI فعلی و runbook همین بسته مرجع است؛
token/cursor/payload خصوصی نباید در Transcript، history یا گزارش ثبت شود.

## ۸. مفاهیم استفاده‌شده

artifact تغییرناپذیر و Source provenance، SHA256، محیط مؤثر در IIS، private config overlay،
SQL idempotent با deferred compilation، تفاوت Backup و restore قابل‌اثبات، جدایی Test/Production،
و تفکیک Regression محلی از پذیرش سرور.

## ۹. تصمیم‌های طراحی

پروفایل موجود TestIis reuse می‌شود؛ ProductionIis، frontend یا migration دستی ساخته نمی‌شود.
ZIP شامل config خصوصی Test است و خارج Git/انتشار عمومی می‌ماند. هنگام Deploy باید config
واقعی سرور حفظ و فقط policy غیرمحرمانهٔ مصوب تطبیق داده شود؛ overwrite کورِ فایل خصوصی ممنوع است.
Source physical path/context و repository واقعی Data Protection باید حفظ شوند؛ مسیر ring
یا ApplicationName ساختگی به config اضافه نمی‌شود.

دسترسی WinRM آزمایشی روی5986 به علت TLS با کد12175 قبل از احراز هویت متوقف شد؛ بررسی
گواهی یا TrustedHosts تغییر نکرد. سپس مالک صریحاً دامنه را به بسته/دستورها محدود کرد.
Site/Pool/Physical Path، ring و baseline DB واقعی نامعلوم باقی‌اند؛ نام نمونه به جای واقعیت ثبت نمی‌شود.

## ۱۰. تست‌ها

تست محصول تازه نوشته یا تستی حذف/غیرفعال نشده است. Restore و Release build موفق و Build
صفر Warning/Error داشت. دور کامل نخست:۱۳۸۶ Unit +۲۰۹۸ Integration +۱۴ Architecture =۳۴۹۸
اجرا؛۳۴۹۷ موفق، یک شکست و صفر Skip. شکست:
`RevokingOneSessionStopsRestHubAndFuturePrivateEventsButKeepsOtherSessionWorking` در انتظار
۱۰ثانیه‌ای event نشست معتبر، با TimeoutException رخ داد.

کد و timeout تغییر نکردند. ابزار موجود Realtime در پنج process مستقل، هر بار۸ case و مجموع
۴۰/۴۰ موفق اجرا شد؛ خطا بازتولید نشد، اما علت آن `UNPROVEN` است و «رفع‌شده» ادعا نمی‌شود.
یک دور کامل کنترل‌شدهٔ مجدد برای Gate انتشار اجرا شد:۱۳۸۶ Unit،۲۰۹۸ Integration و۱۴
Architecture؛ مجموع۳۴۹۸ اجرا و۳۴۹۸ موفق، صفر شکست و صفر Skip. زمان Integration این دور
۳۱دقیقه و۱۴ثانیه بود. تمام۱۱۷ تست Zibal نیز پاس شدند؛ پرداخت زنده اجرا نشد. نتیجهٔ موفق
این دور جای شکست اولیه را نمی‌گیرد؛ `REALTIME_TIMEOUTS_OBSERVED=1` مربوط به دور نخست محلی است.

## ۱۱. Validation و Manifest

| بررسی | نتیجهٔ واقعی محلی |
|---|---|
| Restore | PASS، ExitCode=0 |
| Release build solution | PASS، صفر Warning و صفر Error |
| دور کامل نخست | ۳۴۹۷ موفق و۱ شکست از۳۴۹۸؛ صفر Skip |
| بازتولید محدود Realtime | ۴۰/۴۰ موفق در۵ process مستقل؛ علت Timeout نخست UNPROVEN |
| دور کامل کنترل‌شدهٔ نهایی | ۳۴۹۸/۳۴۹۸ موفق؛ صفر شکست و Skip |
| EF pending model changes | NO |
| SQL acceptance | ۸/۸ موفق، فقط LocalDB ایزوله |
| Publish با TestIis | PASS؛ compile پروژه‌های برنامه و publish، ExitCode=0 |
| ZIP | PASS؛۸۷ فایل، CRC سالم و byte/hash برابر با publish |
| فایل‌های ممنوع | هیچ PDB، سورس، assembly تست، Production/Development JSON وجود ندارد |
| محیط و تنظیم مؤثر | Test/Test؛ OTP=false/true؛ AI/Knowledge فعال بدون Service Auth؛ Unmetered |
| سایر policyها | Payment/Push unavailable؛ PersonalClass academics و AccountDeletion execution خاموش |
| Swagger | فقط در خروجی Test روشن؛ source config بدون تغییر |
| حفاظت از فایل‌ها | SHA256 هر۱۴ فایل و Backup یکسان؛ هر۱۸۳۶ فایل tracked قبلی بدون تغییر |

فایل خروجی: `MakanApp-Test-20261118-484e448.zip`، اندازه۱۴۷۵۰۳۰۵ بایت. نسخهٔ DLL:
`1.0.0+484e4484a4de24dc4e31041da5dd2c0306abbb63`.

```text
ZIP_SHA256=504aafebb7efa4e3c33a754cfa26b8e7abff9eda3f269d1d21a76692bcd1ea80
API_DLL_SHA256=f2911eab47c0c51e886cca8205754b6c333ff2e748967e042f364fc3ef6937eb
MIGRATION_SQL_SHA256=426e13ce3bdc719c02b7eaa194fdcd605efa9d5b5fcbb1b68953475ebd5b5db7
```

Manifest نام/Hash artifact، Source، موجودی Migration و سیاست محیط را ثبت می‌کند و همهٔ
فیلدهای واقعی سرور را NOT_RUN یا null نگه می‌دارد. فایل‌های موجود کنار ZIP شامل Backup
خصوصی و شواهد حجیم تست هم هستند؛ فقط تحویل‌های معرفی‌شده منتقل شوند.

## ۱۲. Git

Source باینری ثابت484e448 است. سه فایل قدیمی `tools/ai/provider_diagnostic.py`،
`run_exact_ai.py` و `test_provider_diagnostic.py`، همهٔ private configها و فایل‌های IDE/provider
با Backup/SHA256 محافظت و یکسان‌بودنشان تأیید شد؛ تغییر یا stage نمی‌شوند. تنها فایل قابل
ثبت این مرحله همین سند Deployment است؛ همهٔ گیت‌های محلی پیش از ثبت موفق شده‌اند.
artifact، ZIP، SQL خصوصی، لاگ و Secret وارد Git نمی‌شوند. Hash/پیام Commit و نتیجهٔ واقعی
Push در گزارش پایان و شواهد خصوصی Git ثبت می‌شود؛ Commit سند، Source باینری484e448 را تغییر نمی‌دهد.

## ۱۳. محدودیت‌های جاری

هیچ Backup، توقف/Restart IIS، Migration یا Deploy سرور انجام نشده است. Main merge و
Production publish/migration/deployment هم اجرا نمی‌شوند. Test Server acceptance، تعداد
خطاهای server log و تداوم Storage/ring نامعلوم‌اند؛ `READY_FOR_MAIN_MERGE=NO`.
پرداخت/Store/Push و PersonalClass academics Deferred می‌مانند؛ حذف مخرب حساب خاموش است.

## ۱۴. نکات آموزشی

1. پاسخ200 نسخهٔ قبلی، پذیرش artifact تازه را اثبات نمی‌کند.
2. Retest موفق، شکست ثبت‌شده و علت نامعلوم را حذف نمی‌کند؛ هر دو شاهد باید باقی بمانند.
3. SQL معتبر بدون history، Backup و تأیید مقصد، مجوز اجرای حدسی روی server نیست.
4. Identity key جای repository/context واقعی Data Protection را نمی‌گیرد.
5. ZIP خصوصی باید از publish ساخته شود؛ کل Release شامل Backup/log و فایل‌های غیرقابل Deploy است.
6. binary rollback و schema/data compatibility یک تصمیم واحد نیستند؛ Restore مخرب خودکار مجاز نیست.

## ۱۵. مسیر مطالعه

`TestIis.pubxml` → source web.config و config مؤثر Test →
`export-migration-sql.ps1` → `test-migration-sql.ps1` → Migration موجود
`AddUnmeteredAiOperations` → تست Realtime یادشده و `test-realtime-release.ps1` →
Manifest/Hash/راهنمای همین Release. هیچ Controller یا Domain جدیدی برای این تحویل وجود ندارد.

## ۱۶. مرحلهٔ بعد

اپراتور پس از تحویل بستهٔ معتبر، دسترسی و مقصد را بررسی، Backup واقعی را تهیه، history
Test را ثبت و فقط MakanApp_Test را با SQL معتبر ارتقا دهد؛ سپس نصب و چک‌لیست سرور را اجرا
و Evidence امن را ثبت کند. تا پذیرش واقعی، Merge به main و هر تغییر Production خارج از این مرحله است.

## وضعیت نهایی تحویل و موارد نیازمند اجرای مالک

مقادیر زیر نتیجهٔ همین Task هستند؛ NOT_RUN را به FAIL فنی یا PASS تبدیل نکنید. نام واقعی
دیتابیس سرور، history، Site/Pool، Backup و log count به علت نبود دسترسی مدیریتی احراز نشده‌اند.

```text
SOURCE_COMMIT=484e4484a4de24dc4e31041da5dd2c0306abbb63
BRANCH=feature/product-v6-backend-alignment
TEST_ARTIFACT=MakanApp-Test-20261118-484e448.zip
TEST_ARTIFACT_SHA256=504aafebb7efa4e3c33a754cfa26b8e7abff9eda3f269d1d21a76692bcd1ea80
MIGRATION_COUNT=76
LATEST_MIGRATION=20261118065656_AddUnmeteredAiOperations
PENDING_MODEL_CHANGES=NO
MIGRATION_SQL_ACCEPTANCE=PASS
TEST_DB=UNKNOWN; EXPECTED=MakanApp_Test
TEST_DB_MIGRATION_BEFORE=UNKNOWN
TEST_DB_MIGRATION_AFTER=NOT_RUN
TEST_DB_BACKUP=NOT_RUN
TEST_APP_BACKUP=NOT_RUN
TEST_STORAGE_BACKUP=NOT_RUN
TEST_DATA_PROTECTION_BACKUP=NOT_RUN
TEST_IIS_DEPLOYMENT=NOT_RUN_OWNER_DEPLOYMENT
HEALTH_LIVE=NOT_RUN_FOR_THIS_RELEASE
HEALTH_READY=NOT_RUN_FOR_THIS_RELEASE
OPENAPI=NOT_RUN_FOR_THIS_RELEASE
TEST_OTP_MOCK=NOT_RUN_ON_SERVER
PERSONAL_AI_NO_SUBSCRIPTION_LIVE=NOT_RUN_ON_SERVER
TEST_AI_RUNTIME=NOT_RUN_ON_SERVER
TEST_KNOWLEDGE_RUNTIME=NOT_RUN_ON_SERVER
KNOWLEDGE_INGEST=NOT_RUN_ON_SERVER
KNOWLEDGE_RETRIEVE=NOT_RUN_ON_SERVER
KNOWLEDGE_RAG=NOT_RUN_ON_SERVER
KNOWLEDGE_REVOKE=NOT_RUN_ON_SERVER
MESSENGER_HISTORY=NOT_RUN_ON_SERVER
ACCOUNT_INBOX=NOT_RUN_ON_SERVER
MESSENGER_PAGINATION=NOT_RUN_ON_SERVER
SIGNALR=NOT_RUN_ON_SERVER
STORAGE_UPLOAD=NOT_RUN_ON_SERVER
STORAGE_DOWNLOAD=NOT_RUN_ON_SERVER
STORAGE_RESTART_PERSISTENCE=NOT_RUN_ON_SERVER
OFFICIAL_ACL=NOT_RUN_ON_SERVER
PAYMENTS=DEFERRED_CURRENT_RELEASE
ZIBAL_RUNTIME=NOT_REQUIRED_CURRENT_RELEASE
ZIBAL_LIVE_PAYMENT=NOT_RUN
STORE_BILLING=DEFERRED
PUSH_RUNTIME=DEFERRED
PERSONAL_CLASS_ASSIGNMENTS=DEFERRED
PERSONAL_CLASS_EXAMS=DEFERRED
ACCOUNT_DELETION_EXECUTION=DISABLED
TEST_SERVER_ERRORS=UNKNOWN
REALTIME_TIMEOUTS_OBSERVED=1_LOCAL_INITIAL_RUN; SERVER=UNKNOWN
TEST_SERVER_ACCEPTANCE=NOT_RUN_BLOCKED_ACCESS
READY_FOR_MAIN_MERGE=NO
MERGE_TO_MAIN=NOT_RUN
PRODUCTION_PUBLISH=NOT_RUN
PRODUCTION_DB_MIGRATION=NOT_RUN
PRODUCTION_DEPLOYMENT=NOT_RUN
```
