# Runbook پشتیبان‌گیری و بازیابی

بازبینی `2026-11-26` از `source@f8178a47de94eae0ec2431adebcb9256a9789786`.

## داده‌های افزودهٔ نسخهٔ جاری

مدل۱۵۳ جدول و۷۸ Migration تا 20261122085930_AddMessagingMediaReviews دارد. AI Conversations/Turns و Unmetered operations، SourceVersion و processing، درخواست/مرحله‌های حذف حساب، Official Spaces/PersonalClass و تصمیم رسانه در ModerationCases همراه دادهٔ قبلی backup شوند. فایل Evidence و FileId/SHA256/MIME/Size تأییدشده باید با Blob سازگار باقی بمانند؛ بازیابی metadata به نسخه‌ای با Blob متفاوت Approval معتبر نمی‌سازد.

Data Protection ring cursor و کلید Identity با custody امن و جدا نگه‌داری شوند. پس از Restore، Replay cursor قدیمی ممکن است resync بخواهد. Unknown عملیات AI/پرداخت را موفق یا قابل اجرای خودکار دوباره فرض نکنید. backup policy مجوز purge تاریخچه، حذف شواهد یا فعال‌سازی اجرای حذف حساب نیست.


## BE-23 — وضعیت جاری و شواهد محلی

Source مبنا `00bd8a9`،۷۵ Migration تا ExtendProductNotificationCoverage است؛ داده‌های جدید
AddressBook، School Application/Provisioning، AccountDeletion و Notification همراه SQL و bytes
متناظر backup می‌شوند. [گزارش BE-23](../product-v6/BE23_OPERATIONS_READINESS_REPORT_FA.md)
نتیجهٔ واقعی Drill را ثبت می‌کند؛ عددهای پایین تاریخچهٔ خودشان‌اند.

سه جزء SQL، Durable Storage و Data Protection Ring باید در نقطهٔ سازگار، پس از توقف writerها
حفظ شوند. private config/Identity key و custody محافظ‌های Windows/certificate جداگانه و محرمانه
backup شوند؛ Secret در manifest مشترک یا Git قرار نگیرد. مسیر Storage محیط‌ها جدا باقی بماند.

وجود `.bak` یا موفقیت VERIFYONLY جای RESTORE و بررسی برنامه نیست. پس از restore، history،
DBCC CHECKDB، metadata/hash/bytes، نشست و current ACL و بازشدن protected state قبلی سنجیده شوند.
Ring با ApplicationName/purpose و identity/protector سازگار لازم است؛ ساخت Ring جدید راه بازیابی نیست.
عوض‌کردن physical/content-root path ممکن است discriminator پیش‌فرض را عوض کند؛ مهاجرت path/identity
نیازمند rehearsal مستقل است. Ring منسوخ MFA با مصرف جاری AddressBook/cursor یکسان فرض نشود.

`LOCAL_RESTORE_DRILL` فقط با اجرای واقعی محلی گزارش می‌شود؛
`PRODUCTION_RESTORE_DRILL=NOT_RUN` و
`RPO_RTO=PRODUCT_OPERATIONS_DECISION_REQUIRED` باقی‌اند. زمان مشاهده‌شدهٔ یک Drill محلی SLA نیست.
برای IIS و طرح بازگشت به [Manifest](../product-v6/BE23_RELEASE_ARTIFACT_MANIFEST_FA.md) رجوع کنید؛
Down migration خودکار یا overwrite مقصد موجود مجاز نیست.

ابزار موجود دو ورودی اختیاری دارد: در Backup، `-DataProtectionRoot` و در Restore،
`-TargetDataProtectionRoot`. اگر Ring درخواست شده باشد، root باید جدا از Storage، application و
bundle باشد؛ مقصد Restore تازه و فاقد overlap است. Format2 فقط فهرست/hash/اندازه را در manifest
می‌نویسد و bytes کلید را در زیرپوشهٔ خصوصی key-ring نگه می‌دارد. missing/corrupt Ring پیش از
ساخت دیتابیس رد می‌شود؛ Format1 قدیمی بدون Ring خوانده می‌شود ولی بازیابی protected state را اثبات نمی‌کند.

این گزینه‌ها setting جدید Backend یا احیای MFA نیستند. مسیر Ring واقعی و custody آن را اپراتور
مشخص کند؛ ابزار محل Ring یا ApplicationName سرور را حدس نمی‌زند. محافظ DPAPI/certificate،
Windows profile و کلیدهای محافظ مستقل همچنان برای انتقال بین ماشین/identity لازم‌اند.

## افزودهٔ بازیابی نسخهٔ جاری — ۲۰۲۶-۱۰-۳۰

schema جاری78 Migration تا AddMessagingMediaReviews و153 جدول دارد. UsageReservations، SubscriptionAmendments و PersonalClassها همراه کل دادهٔ Commerce backup می‌شوند؛ Job و reservation را جدا به نقطهٔ زمانی ناسازگار نبرید. policy سؤال و AnswerRevisionAttachments به bytes Retained آزمون وابسته‌اند؛ DB+Storage سازگار و کنترل مجوز پس از restore لازم است. Detach مجوز حذف فایل تاریخی نیست.

Data Protection استاندارد host اکنون cursor حساب را محافظت می‌کند. persistence/custody کلیدهای جاری و هویت host در backup/restart بررسی شوند؛ ring MFA منسوخ با ring cursor یکی فرض نشود. بدون کلید معتبر، cursor قدیمی resync می‌خواهد و Client باید Snapshot مجاز تازه بگیرد؛ این مسیر را در drill هدف بررسی کنید. Identity key پایدار جایگزین ring نیست. [Runbook Realtime](../operations/REALTIME_RUNBOOK_FA.md).

بازبینی `2026-10-29`: RemovePlatformMfa حذف داده دارد؛ سه migration جدید بعدی DOB/Photo/School را اضافه می‌کنند. DB و bytes عکس/جزوه یک snapshot سازگار می‌خواهند؛ applied migration سرور از این سند تأیید نشده. [مسیر استقرار](../deployment-windows-linux-docker.md).

<!-- CURRENT-REVIEW:BEGIN -->
بازبینی `2026-11-26` از `source@f8178a47de94eae0ec2431adebcb9256a9789786`.؛ [وضعیت جاری](../CURRENT_PROJECT_STATUS.md)، [ماتریس آمادگی](PRODUCTION_READINESS.md) و [runbook IIS](DEPLOYMENT_IIS.md) مبنای gateها هستند. این بازبینی deployment، credential واقعی یا load/restore محیط هدف را اثبات نمی‌کند.
<!-- CURRENT-REVIEW:END -->

## مرزبندی جدید Test و Production

backup جاری هر محیط DB، Storage و تنظیمات/Identity key همان محیط را پوشش می‌دهد؛ ProfilePhoto و Lesson bytes در Storage هستند. schema یکسان مجوز مخلوط‌کردن Test و Production نیست. MFA و ring اختصاصی آن حذف شده‌اند؛ Data Protection استاندارد cursor نیاز مستقل دارد؛ backupهای تاریخی و protector قبلی با این کار حذف نمی‌شوند. cleanup terminal جای backup یا purge مصوب نیست.

## روش فعلی پس از GAP-03

روش قابل تکرار DB + storage، inventory کلیدها، مقصد جدید، ACL و شواهد drill کامل محلی در [GAP-03](../release/GAP_03_BACKUP_RESTORE_RECOVERY.md) آمده است. ابزار فعلی `tools/production/recovery/Invoke-MakanRecovery.ps1` است؛ SQL قدیمی STEP 16 پایین صرفاً شاهد تاریخی marker و دارای cleanup است، نه ابزار بازیابی کامل محصول. نمونه SQL پایین نیز روش پیشنهادی قدیمی است؛ ابزار جدید `COPY_ONLY, CHECKSUM` و فایل تازه بدون `INIT` می‌گیرد. هیچ job سرور، off-server copy یا RPO/RTO مصوب با این سند ادعا نمی‌شود.

## سیاست پیشنهادی

- Full backup زمان‌بندی‌شده با `CHECKSUM` و retention تأییدشده.
- اگر Recovery Model برابر Full است، log backup با فاصله متناسب با RPO و monitoring شکست job.
- `RESTORE VERIFYONLY` مفید است اما جای restore drill را نمی‌گیرد.
- مسیر `Storage__RootPath` جداگانه versioned/snapshot شود؛ secrets فقط با reference و روش بازیابی در secret manager نگه‌داری شوند.
- backupها encrypted، دسترسی‌محدود و خارج از همان failure domain سرور اصلی باشند.
- GAP-04 و PH-1 شاهد تاریخی نیاز ring بودند؛ در نسخه جاری MFA و ring اختصاصی آن حذف شده‌اند؛ حفاظت cursor از Data Protection استاندارد استفاده می‌کند. بازیابی backup پیش از RemovePlatformMfa یک سناریوی legacy مستقل است و به binary/schema سازگار نیاز دارد.
- فایل خصوصی appsettings.Production.json، کلید Identity و tokenهای provider فقط در نسخهٔ پشتیبان محرمانه با ACL و روش بازیابی کنترل‌شده نگه‌داری شوند؛ داخل artifact مشترک، log یا Git نباشند. انتشار جدید نباید کلید Identity یا داده/مسیر Storage را عوض کند.

### تکمیل custody کلید در PH-1

در وضعیت جاری، DataProtection مخصوص MFA تنظیم/وابستگی runtime نیست. اگر restore نسخهٔ تاریخی دارای MFA لازم شد، محافظ/ring آن backup فقط همراه همان binary/schema و custody مجاز بررسی می‌شود؛ ساخت ring تازه بازیابی داده قدیمی نیست. Down migration دادهٔ حذف‌شده MFA را برنمی‌گرداند.

probe حلقهٔ پایدار و جریان MFA متعلق به PH-1 تاریخی بودند و در نسخهٔ جاری حذف شده‌اند. پس از restore فعلی، config/SQL/Storage در readiness و OTP/session/permission در smoke مجاز بررسی شوند. restore نسخهٔ legacy دارای MFA باید با binary/schema و محافظ همان نسخه باشد؛ این بازبینی drill یا secret واقعی سرور را اجرا نمی‌کند.

## سازگاری DB و Storage

DB مرجع metadata و authorization فایل است و storage بایت‌ها را نگه می‌دارد. برای نقطه‌ی سازگار، ابتدا writeها drain شوند؛ زمان/شناسه‌ی backup DB و snapshot storage با هم ثبت شود. بازیابی فقط یکی از این دو می‌تواند metadata یتیم یا فایل بدون metadata ایجاد کند.

## Restore drill روی محیط disposable

1. یک دیتابیس با نام یکتای drill و storage directory خالی بسازید؛ هرگز DB توسعه یا Production را target نکنید.
2. full backup را با `WITH CHECKSUM` restore کنید و در صورت نیاز log chain را تا زمان هدف اعمال کنید.
3. snapshot storage متناظر را restore و مجوز identity برنامه را اعمال کنید.
4. با connection string موقت، migration history، row countهای پایه، `/health/ready` و smoke test read-only را بررسی کنید.
5. چند فایل مجاز را از API بخوانید و SHA-256 ذخیره‌شده/واقعی را تطبیق دهید.
6. نتیجه، زمان restore، RPO/RTO مشاهده‌شده و خطاها را ثبت کنید؛ سپس محیط disposable را طبق فرایند تأییدشده پاک کنید.

ابزار جاری برای recovery کامل، Invoke-MakanRecovery.ps1 در ابتدای همین سند است؛ اسکریپت restore-drill-localdb.sql فقط شاهد محدود تاریخی STEP 16 است و با cleanup روی مقصد عادی اجرا نشود. موفقیت LocalDB جای rehearsal توپولوژی واقعی، custodian کلیدها، off-server copy و RPO/RTO مصوب را نمی‌گیرد؛ این تحویل هیچ job یا restore سرور را اجرا نمی‌کند.

### نتیجه‌ی drill در STEP 16

در ۲۰۲۶-۱۰-۱۳ یک دیتابیس منبع disposable روی `(localdb)\\MSSQLLocalDB` ساخته، با `CHECKSUM` backup، با `MOVE` در دیتابیس disposable دوم restore و marker داده validate شد. خروجی `STEP16_RESTORE_DRILL_SUCCEEDED` بود و هر دو دیتابیس و فایل `.bak` موقت در cleanup حذف شدند. این نتیجه فقط محیط local را پوشش می‌دهد؛ restore از backup واقعی staging همراه با storage snapshot هنوز باید rehearsal شود.

## نمونه T-SQL عملیاتی

```sql
BACKUP DATABASE [MakanApp]
TO DISK = N'<secure-backup-path>\MakanApp_full.bak'
WITH COPY_ONLY, CHECKSUM, COMPRESSION, STATS = 10;

RESTORE VERIFYONLY
FROM DISK = N'<secure-backup-path>\MakanApp_full.bak'
WITH CHECKSUM;
```

مسیر باید نام تازهٔ تأییدشده داشته باشد؛ فایل backup قبلی overwrite نشود. edition، compression، encryption و credential را DBA تعیین می‌کند. نمونه، جای ابزار جاری و drill واقعی DB+storage+application را نمی‌گیرد.

## داده‌های افزوده P2

backup/restore سازگار با۵۵ migration شامل قصدهای Scheduled، Poll/Quiz و Checklist هم هست؛ قصد Dispatched را پس از recovery دوباره Message تازه نسازید. داده گذرای Typing/Presence backup دائمی یا تاریخچه حضور نیست. keyهای VAPID/FCM و Identity در custody جدا و امن حفظ شوند؛ حذف یا rotation آن‌ها از restore عادی استنتاج نشود.
