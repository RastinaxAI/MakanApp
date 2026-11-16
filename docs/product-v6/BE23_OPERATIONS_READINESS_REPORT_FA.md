# گزارش عملیات و آمادگی انتشار BE-23

مبنا `00bd8a98cbaedf30a198018952a6741e58d10cc4`؛ شاخه
`feature/product-v6-backend-alignment`؛ تاریخ شروع `2026-11-15`.
این گزارش پذیرش محلی است؛ عملیات IIS، migration دیتابیس عادی و پذیرش Production اجرا نشدند.
Validation و تولید بسته‌ها در `2026-11-16` پایان یافت؛ Source آزموده‌شده `52b596a` است.

## ۱. خلاصهٔ مرحله

موتور Migration، profileهای IIS، Health، Outbox/Worker و ابزار Backup/Restore از قبل موجودند.
مسئلهٔ BE-23 تطبیق این زیرساخت با نسخهٔ جاری و اثبات artifact قابل اجراست؛ سیستم دوم ساخته نمی‌شود.
فایل‌های خصوصی و سه ابزار ناتمام AI در Backup خصوصی و SHA-256 محافظت شده‌اند.
وضعیت نهایی مرحله: `BE23_OPERATIONS=PASS_LOCAL_READINESS`؛ پذیرش کامل BE-24 هنوز انجام نشده است.

## ۲. معماری

Domain/Application و قراردادهای عمومی تغییر نمی‌کنند. profile انتشار در Api قرار دارد؛ ابزارهای
PowerShell فقط مسئول عملیات کنترل‌شده‌اند. تست‌های ماندگاری، کلاس‌های واقعی
`AddressBookProtection` و `RealtimeCursorProtector` را مصرف می‌کنند.
SqlServer/EF Core9/.NET9 و جهت وابستگی Onion محفوظ‌اند؛ PowerShell وارد Domain نمی‌شود.

## ۳. فایل‌های مهم جدید

| مسیر | مسئولیت | نکتهٔ آموزشی |
|---|---|---|
| `tools/production/test-realtime-release.ps1` | تکرار کنترل‌شدهٔ سناریوهای قبلی در processهای مستقل و حفظ تمام TRXها | نتیجهٔ آخر نباید شکست دور قبلی را پنهان کند |
| `tests/MakanApp.IntegrationTests/Be23DataProtectionPersistenceTests.cs` | اثبات restart/restore Ring با purposeهای واقعی و رد مرز اشتباه | داشتن Identity key جای Ring را نمی‌گیرد |
| `docs/product-v6/BE23_RELEASE_ARTIFACT_MANIFEST_FA.md` | معرفی بسته‌ها، SQL، Hash، اجرای مجدد و مرز rollback | Publish موفق به معنی Deploy نیست |

## ۴. فایل‌های مهم تغییرکرده

- `test-migration-sql.ps1`: Configuration انتخاب‌شده به exporter موجود منتقل می‌شود؛ آزمون Release نباید DLL قدیمی Debug را مصرف کند.
- `TestIis.pubxml` و `ProductionIis.pubxml`: سمبل خود Api و پروژه‌های وابسته در خروجی سرور کپی نمی‌شود؛ Environment و مرز فایل‌های خصوصی حفظ‌اند.
- `MakanRecovery.psm1` و `Invoke-MakanRecovery.ps1`: Ring اختیاری در bundle خصوصی، manifest نسخه۲ و مقصد مستقلِ بدون overwrite؛ نسخه۱ همچنان قابل خواندن است.
- `MakanAppWebApplicationFactory.Recovery` و `RecoveryDrillTests`: فقط drill صریح، Ring موقت پایدار دارد؛ SQL/bytes/کلید و protected state واقعی در یک چرخه بازیابی می‌شوند.
- `WorkerDispatcherLogSafetyTests`: دو case برای AccountDeletion/ExamDeadline، توقف bounded و لاگ بدون exception خام را می‌سنجند؛ هیچ Processor واقعی فراخوانی نمی‌شود.
- Runbookهای IIS، Backup/Restore، Worker و Release Checklist: تعدادها و مرزهای واقعی v6 جدا از تاریخچه ثبت می‌شوند.

## ۵. جریان اجرای عملیات

Source → Clean/Restore/Release Build → آزمون‌های SQL/HTTP/Architecture →
`export-migration-sql.ps1` → EF SQL → آزمون دیتابیس مستقل →
TestIis/ProductionIis → artifact جدا → اجرای loopback با overrideهای موقت SQL/Storage →
`CoreReadinessHealthCheck` → نتیجهٔ کم‌اطلاعات → ZIP خصوصی و Hash.

Migration و Deploy دو عملیات جدا هستند. برنامه در Startup، Migrate یا EnsureCreated اجرا نمی‌کند.

## ۶. دیتابیس و Migration

موجودی فیزیکی و designerها: **۷۵ Migration**؛ آخرین
`20261114183321_ExtendProductNotificationCoverage`؛ شناسهٔ تکراری یا designer مفقود یافت نشد.
جدول/ستون/Index/Migration جدیدی در BE-23 ایجاد نشد؛ مدل جاری ۱۵۱ جدول دارد.

تغییرات بعد از آخرین بستهٔ محلی زیبال شامل بنیاد v6، School Application، Provisioning،
Account Deletion و پوشش Notification هستند؛ فهرست دقیق در Manifest است.
آخرین بستهٔ محلی، شاهد applied migration سرور نیست: `SERVER_MIGRATION_BASELINE=UNKNOWN`.

SQL نهایی از exporter موجود تولید می‌شود؛ history حفظ و عملیات با EXEC به‌صورت مؤخر کامپایل
می‌شوند تا backfill ستون جدید پیش از DDL کامپایل نشود. XACT_ABORT/TRY-CATCH خطای واقعی را
پنهان نمی‌کنند. ابزار دادهٔ تجاری نامعتبر را خودکار اصلاح نمی‌کند.

EF pending model ندارد؛ هشت پذیرش SQL موفق‌اند: Fresh، retry جاری، بازتولید خطای۲۰۷ خروجی خام،
Upgrade از Exam Grading با داده، retry بدون بازنویسی پیام، Upgrade از v6 پیش از AccountDeletion،
rollback خطای تزریق‌شده و رد تراکنش caller بدون rollback آن. Hash SQL تحویلی با فایل واقعاً آزموده‌شده برابر است.
فقط LocalDB مختص تست مجاز است؛ MakanApp، MakanApp_Test و سرور دست‌نخورده‌اند.

## ۷. Endpointها و Startup منتشرشده

| عملیات موجود | قرارداد/مجوز و هدف پذیرش |
|---|---|
| `GET /health/live` و `/health` | ناشناس، کم‌اطلاعات و مستقل از SQL/Storage |
| `GET /health/ready` | ناشناس؛ config لازم + SQL + Storage؛ شکست dependency برابر503 |
| `GET /api/v1/me` | بدون نشست401؛ Health مجوز API عمومی نمی‌سازد |
| `GET /openapi/v1.json` | طبق Swagger policy؛ Snapshot مستندات بدون worker/provider تولید می‌شود |

هیچ endpoint، DTO یا bypass امنیتی جدید ساخته نمی‌شود. Smoke خروجی روی loopback، SQL و Storage
disposable است؛ هیچ OTP/Push/پرداخت/حذف حساب واقعی اجرا نمی‌شود. OTP واقعی Production از guard
محیط و Options موجود استفاده می‌کند؛ برای تست provider از مرز کنترل‌شده استفاده می‌شود.
Candidate اصلاح‌شده و سپس بستهٔ نهایی هر دو محیط واقعاً اجرا شدند: live/ready=200 با بدنهٔ Healthy،
me بدون نشست=401 و درخواست مستقیم فایل private config=404. Mode واقعی Test=Mock و
Production=Real از marker همان Host تأیید شد. Production با false/true در Startup رد شد.
Stop فرایندهای CLI با Ctrl+C انجام شد؛ Console parent exit1 داد و marker توقف graceful در log
تأیید نشد، بنابراین این Stop شاهد graceful shutdown همهٔ Workerها نامیده نمی‌شود.
قرارداد توقف/بودجه از۱۷ تست متمرکز Worker و تست‌های Host نهایی جدا اثبات می‌شود؛ IIS recycle اجرا نشد.

## ۸. مفاهیم فنی استفاده‌شده

Liveness فقط زنده‌بودن process است؛ Readiness آماده‌بودن dependencyهای core است و همهٔ schema
و providerها را اثبات نمی‌کند. تحویل at-least-once می‌تواند تکرار شود؛ اثر منطقی با کلید، constraint
و transaction idempotent می‌ماند. claim lease مالکیت موقت است؛ مالک منقضی حق overwrite ندارد.
Backup وجود نسخهٔ داده است؛ Restore proof یعنی بازکردن واقعی آن، بررسی SQL/bytes و مجوز برنامه.

## ۹. تصمیم‌های طراحی و مرز پذیرش

زیرساخت فعلی reuse می‌شود؛ افزایش timeout/retry، غیرفعال‌کردن assertion و حذف failure مجاز نیست.
تداوم Ring با ApplicationName/purpose و identity/protector سازگار بررسی می‌شود؛ setting حذف‌شدهٔ
MFA بازنمی‌گردد. Production فعلاً AddDataProtection استاندارد دارد؛ محل/ACL و discriminator مؤثر
سرور از این رایانه اثبات نمی‌شوند. تغییر path/identity هنگام Deploy باید قبلاً rehearsal شود؛
تغییر ApplicationName یا ساخت Ring تازه، دادهٔ قبلی را بازیابی نمی‌کند.

دانلود/AI عمومی Library، تأیید ReviewRequired، مدت retention و RPO/RTO حدس زده نمی‌شوند.
نبود Push credential خطای کد عملیات نیست؛ `CODE_READY`، تنظیم و پذیرش محیط جدا گزارش می‌شوند.

## ۱۰. تست‌ها، Workerها و مشاهدهٔ Realtime

دو case Integration و دو case Unit جدید اضافه شدند. Integrationها، host بازسازی‌شده و Ring بازیابی‌شده را با کلیدهای مصنوعی می‌سنجند؛ Ring،
ApplicationName، owner و purpose نادرست باید CryptographicException بدهند. XML/مقدار محافظت‌شده
یا دادهٔ خصوصی در گزارش نیست. تست کامل Recovery نیز روی دیتابیس source/target یکتای LocalDB،
BACKUP/VERIFYONLY/RESTORE و CHECKDB واقعی، bytes و نشست/ACL، نشانگر و AddressBook محافظت‌شده را
می‌سنجد. نبود مقصد Ring و خرابی فایل آن پیش از ایجاد دیتابیس رد و DB_ID تهی اثبات می‌شود.

| BackgroundService واقعی | منبع اثر و بازیابی |
|---|---|
| OutboxDispatcher | claim/lease/rowversion؛ Notification/Subscription/Provisioning با اثر SQL idempotent؛ invalidation خارج transaction |
| MessagingRealtimeOutboxDispatcher | hint قابل تکرار؛ Delta/replay SQL و مجوز جاری مرجع‌اند |
| NotificationDeliveryDispatcher | claim منقضی، backoff، ثبت نتیجهٔ fenced؛ پذیرش provider رسید دستگاه نیست |
| ScheduledMessageDispatcher | intent پایدار، current ACL/Safety و receipt اتمیک؛ intent لغوشده ارسال نمی‌شود |
| ReportExportDispatcher | output canonical، فایل مستقل هر تلاش و Ready reuse؛ stale writer نمی‌تواند نتیجهٔ جدید را overwrite کند |
| AccountDeletionDispatcher | پیش‌فرض خاموش؛ checkpoint/lease و command idempotent برای Knowledge |
| ExamDeadlineDispatcher | جست‌وجوی bounded آزمون موعدگذشته و finalizer idempotent موجود؛ lease صف جدا اختراع نشده |
| MessagingEphemeralDispatcher | حافظه/TTL؛ Typing/Presence durable نیستند |
| OperationalMetricsPublisher | snapshot فقط خواندنی؛ failure مقدار قبلی را stale می‌کند |
| TerminalFileCleanupDispatcher | پیش‌فرض خاموش؛ terminal بدون reference/retention و grace صریح؛ purge Ready ندارد |

Payment Recovery یک dispatcher مستقل تازه نیست؛ verify/inquiry و handlerهای fulfillment/provisioning
موجود آزموده می‌شوند. همهٔ Workerها token توقف دارند؛ budget Host، ANCM و WAS سه مفهوم جدا هستند.
تست متمرکز جدید Recovery/OTP/DataProtection برابر۱۲ موفق و تست shutdown/logging برابر۱۷ موفق است.
نتیجهٔ نهایی مجموعهٔ کامل recoveryها در Final Regression ثبت می‌شود.

در BE-15 شش Realtime timeout و یک Lookup failure در دور بعد ثبت شده بود. در BE-23 پنج process
مقدماتی و پنج process با رانر اصلاح‌شده، هرکدام۸ case را پاس کردند؛ جمع۸۰ case، بدون timeout
یا Lookup failure. ثبت ExitCode رانر مقدماتی null بود و خود اسکریپت exit1 داد؛ این خطای ثبت
شواهد با شکست آزمون اشتباه نمی‌شود. پنج اجرای بعدی ExitCode=0 و TRX کامل دارند. Parse فایل
PowerShell فارسی نیز با UTF-8 BOM برای Windows PowerShell5.1 اصلاح شد.

`REALTIME_FLAKY_ROOT_CAUSE=UNPROVEN`؛ این اجراها ادعای رفع قطعی ناپایداری قبلی نیستند.
Full Regression بدون Probe موازی اجرا شد؛ اسکن هر سه TRX نهایی صفر شکست، صفر skipped و
صفر Realtime timeout/Lookup failure را تأیید کرد. این نتیجه علت شکست‌های تاریخی را مشخص نمی‌کند.

## ۱۱. Validation واقعی

| Gate | نتیجهٔ فعلی |
|---|---|
| Clean / Restore | PASS |
| Build اولیه و Build پس از دو تست جدید | PASS؛ صفر Warning/Error |
| Focused Data Protection | ۲ Integration موفق؛ صفر Failed/Skipped |
| Focused Realtime/Lookup | ۱۰ process ×۸ case موفق؛ پنج دور با ExitCode ثبت‌شدهٔ۰ |
| Full اول، پیش از تکمیل Ring در ابزار Recovery | ۱۳۴۸ Unit +۲۰۲۸ Integration +۱۴ Architecture =۳۳۹۰ موفق؛ صفر Failed/Skipped؛ زمان فرمان۱۸۸۷ثانیه |
| Final Build | PASS؛ صفر Warning/Error |
| Final Tests | ۱۳۵۰ Unit +۲۰۲۸ Integration +۱۴ Architecture =۳۳۹۲ اجرا و موفق؛ صفر Failed/Skipped |
| EF/model و SQL تحویلی | مدل معلق ندارد؛۸ پذیرش SQL موفق؛۷۵ Migration |
| Test/Production Publish و startup واقعی خروجی نهایی | PASS؛ بدون symbol/source/test artifact؛ configهای خصوصی عیناً محفوظ؛ API DLL دو محیط برابر؛ هر دو ZIP سالم |
| Backup/Restore مستقل و Storage | PASS؛ Ring و protected state در drill کامل؛ read/write/delete در دو directory مصنوعی |
| Zibal / logging / worker recovery | PASS؛ هر ۱۱۷ تست Zibal موفق؛ آزمون‌های recovery و لاگ امن در Final Full موفق |
| بازسنجی پس از review نهایی | Build بدون Warning/Error؛۱۲ Integration و۱۷ Unit موفق، جمع۲۹ |
| سازگاری ابزار Backup قدیمی | Restore واقعی Format1 موفق؛ Format2 نیز SQL/Storage/Ring را بازیابی کرد |

شواهد خصوصی و logهای کم‌حجم در `.artifacts/be23-operations-20261115-00bd8a9` هستند.
شمارهای بالا از TRX واقعی خوانده شدند؛ اجرای Realtime تکراری و focusedها به شمار Full اضافه نشده‌اند.
تنها تغییر کد پس از Full، حذف یک guard کاملاً تکراری در factory تست بود؛ guard اولیه با همان
شرط و exception محفوظ است. Build و۲۹ تست متمرکزِ بالا پس از همین حذف دوباره موفق شدند.

شکست‌های مرحلهٔ آماده‌سازی نیز حفظ‌اند: بازرسی Candidate نخست سه PDB وابسته را پیدا کرد؛
target انتشار تکمیل و خروجی تازه r2 بدون PDB ساخته شد. Smoke نخست به علت نتیجهٔ null انتخاب
State در pipeline PowerShell، override موقت درست نداشت و Bind با Socket10013 شکست خورد؛
انتخاب صریح تک‌محیط و guard DB/Storage/Loopback اصلاح شد و هر دو Smoke واقعی بعدی موفق شدند.
Source/private config، timeout، Service Auth یا authorization برای عبور این موارد تغییر نکردند.
در بررسی Format1 نیز مقدار ماندهٔ `$LASTEXITCODE` ابتدا نتیجهٔ runner را ناموفق نشان داد؛ خروجی
مستقل Restore و JSON پذیرش، موفقیت واقعی را تأیید کردند. این مشکل ثبت شاهد، خطای Restore معرفی نمی‌شود.

## ۱۲. Git و فایل‌های محفوظ

مبنای شروع و tracked Remote برابر `00bd8a98cbaedf30a198018952a6741e58d10cc4` بودند؛ فقط این سه فایل dirty وجود داشتند:

```text
tools/ai/provider_diagnostic.py
tools/ai/run_exact_ai.py
tools/ai/test_provider_diagnostic.py
```

۱۲ فایل شامل تنظیمات خصوصی، دو فایل user، کد ملی‌پیامک و AI با SHA-256 و Backup محافظت شدند.
هیچ artifact، secret، فایل AI ناتمام یا config خصوصی Stage نمی‌شود.
Commit کد و تست: `52b596a9892ae44e1ff6640b94516e96e4b2586c` با پیام
`ops(release): harden product-v6 release readiness`؛۱۱ فایل، شامل profileها، ابزار عملیات و تست‌ها.
Commit مستندات با پیام `docs(ops): finalize product-v6 deployment and rollback runbooks`
روی همان شاخه ثبت می‌شود؛ SHA دقیق و نتیجهٔ Push/Remote در گزارش نهایی Git ارائه می‌شوند.
Upstream برابر `origin/feature/product-v6-backend-alignment` و Remote برابر
`https://github.com/RastinaxAI/MakanApp.git` است؛ config خصوصی و سه فایل AI جزو هیچ‌یک از این Commitها نیستند.
Merge به main، IIS Deploy و migration دیتابیس‌های عادی انجام نمی‌شوند.

## ۱۳. محدودیت‌های فعلی

- Production Ring/Storage/ACL و Restore واقعی، پذیرش عملیاتی آینده می‌خواهند.
- Push/Notification SMS، اجرای حذف حساب و شرایط تجاری واقعی آموزشگاه به تنظیم مجاز وابسته‌اند.
- CafeBazaar/Myket قرارداد/تنظیم معتبر و iOS تصمیم Product می‌خواهند.
- Exporter/alert delivery به زیرساخت متصل نشده‌اند؛ وجود Meter شاهد دریافت alert نیست.
- Root cause ناپایداری Realtime فعلاً UNPROVEN است؛ RPO/RTO رسمی تعیین نشده‌اند.
- `BE24_FULL_ACCEPTANCE=FAIL` و شمار تصمیم‌های Product/Legal همچنان۱۲ است.

## ۱۴. نکات آموزشی

۱. نسخهٔ DLL انتخاب‌شده برای SQL باید با Configuration تست یکسان باشد.
۲. قبل از Delta، history واقعی مقصد را بخوانید؛ بستهٔ محلی شاهد سرور نیست.
۳. Schema برابرِ Test/Production به معنی OTP، DB، Storage یا secret برابر نیست.
۴. نگه‌داری Ring بدون identity/protector/ApplicationName سازگار کافی نیست.
۵. application rollback با Down migration یا بازیابی دادهٔ حذف‌شده یکی نیست.
۶. failureهای میانی و مشکل رانر را مستقل از نتیجهٔ نهایی ثبت کنید.
۷. Publish موفق، local readiness است؛ Production acceptance به محیط واقعی نیاز دارد.

## ۱۵. مسیر پیشنهادی مطالعه

Profileهای TestIis/ProductionIis → ProductionConfigurationValidator/OtpAuthenticationOptions →
CoreReadinessHealthCheck → export-migration-sql/test-migration-sql → OutboxProcessor و Workerها →
RecoveryDrillTests و ابزار MakanRecovery → Be23DataProtectionPersistenceTests →
test-realtime-release → Manifest و Runbookهای عملیات.

## ۱۶. مرحلهٔ بعد

اپراتور در مأموریت جدا، history/backup و custody کلیدهای محیط هدف را ثبت و بستهٔ تأییدشده را
با maintenance window مستقر کند. سپس Health، Smoke، restart/recycle، monitoring و Restore هدف
پذیرفته شوند. فعال‌سازی حذف حساب، Push، قیمت واقعی، retention یا Library با این انتشار مخلوط نشوند.
