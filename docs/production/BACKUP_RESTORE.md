# Runbook پشتیبان‌گیری و بازیابی

<!-- CURRENT-REVIEW:BEGIN -->
بازبینی `2026-10-18` از `main@ef67624`؛ [وضعیت جاری](../CURRENT_PROJECT_STATUS.md)، [ماتریس آمادگی](PRODUCTION_READINESS.md) و [runbook IIS](DEPLOYMENT_IIS.md) مبنای gateها هستند. این بازبینی deployment، credential واقعی یا load/restore محیط هدف را اثبات نمی‌کند.
<!-- CURRENT-REVIEW:END -->

## روش فعلی پس از GAP-03

روش قابل تکرار DB + storage، inventory کلیدها، مقصد جدید، ACL و شواهد drill کامل محلی در [GAP-03](../release/GAP_03_BACKUP_RESTORE_RECOVERY.md) آمده است. ابزار فعلی `tools/production/recovery/Invoke-MakanRecovery.ps1` است؛ SQL قدیمی STEP 16 پایین صرفاً شاهد تاریخی marker و دارای cleanup است، نه ابزار بازیابی کامل محصول. نمونه SQL پایین نیز روش پیشنهادی قدیمی است؛ ابزار جدید `COPY_ONLY, CHECKSUM` و فایل تازه بدون `INIT` می‌گیرد. هیچ job سرور، off-server copy یا RPO/RTO مصوب با این سند ادعا نمی‌شود.

## سیاست پیشنهادی

- Full backup زمان‌بندی‌شده با `CHECKSUM` و retention تأییدشده.
- اگر Recovery Model برابر Full است، log backup با فاصله متناسب با RPO و monitoring شکست job.
- `RESTORE VERIFYONLY` مفید است اما جای restore drill را نمی‌گیرد.
- مسیر `Storage__RootPath` جداگانه versioned/snapshot شود؛ secrets فقط با reference و روش بازیابی در secret manager نگه‌داری شوند.
- backupها encrypted، دسترسی‌محدود و خارج از همان failure domain سرور اصلی باشند.
- پس از GAP-04، key ring پایدار MFA و custody محافظ DPAPI/certificate نیز دارایی recovery هستند؛ DB backup به‌تنهایی TOTP را بازیابی نمی‌کند. DPAPI به همان هویت Windows/ماشین وابسته است و کپی XML به سرور دیگر کافی نیست.
- فایل خصوصی appsettings.Production.json، کلید Identity و tokenهای provider فقط در نسخهٔ پشتیبان محرمانه با ACL و روش بازیابی کنترل‌شده نگه‌داری شوند؛ داخل artifact مشترک، log یا Git نباشند. انتشار جدید نباید کلید Identity یا مسیر/کلیدهای MFA قبلی را عوض کند.

### تکمیل custody کلید در PH-1

Data Protection همچنان از provider قبلی با discriminator پیش‌فرض `MakanApp.BackofficeMfa.v1` و purposeهای MFA قبلی استفاده می‌کند. aliasهای `Security:DataProtection:*` روش جایگزین تنظیم‌اند، نه مجوز جابه‌جایی حلقه. تمام XMLهای لازم برای داده‌های تاریخی، protector، هویت/profile ویندوز یا PFX/private key/رمز و `ApplicationName` جزو inventory محرمانهٔ recovery باشند. DPAPI جاری به current-user وابسته است؛ XML تنها روی سرور دیگر قابل بازیابی فرض نشود. مسیر PFX موجود گزینهٔ قابل انتقال با custody مستقل است؛ certificate store/thumbprint یا rotation تازه‌ای در PH-1 ساخته نشده است.

startup عملیاتی اکنون وجود حلقهٔ پایدار قابل نوشتن و roundtrip حفاظت را الزام می‌کند؛ readiness probe پوشه/کلید گم‌شده را بازسازی نمی‌کند و حذف کلیدهای دیده‌شده در startup را حتی با provider cache تشخیص می‌دهد. این شاهد، decrypt تمام payloadهای تاریخی پس از disaster را ثابت نمی‌کند. پیش از بازکردن traffic پس از restore، با **همان** `ApplicationName` و purpose، یک payload مصنوعی ثبت‌شده پیش از backup را روی instance بازیابی‌شده بررسی کنید؛ از چاپ secret TOTP یا محتوای کلید در evidence خودداری شود. شواهد محلی recreate provider جای drill انتقال واقعی IIS/ماشین را نمی‌گیرند و هیچ job/off-server copy تازه‌ای اجرا نشده است.

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

backup/restore سازگار با ۴۸ migration شامل قصدهای Scheduled، Poll/Quiz و Checklist هم هست؛ قصد Dispatched را پس از recovery دوباره Message تازه نسازید. داده گذرای Typing/Presence backup دائمی یا تاریخچه حضور نیست. keyهای VAPID/FCM و MFA/Identity در custody جدا و امن حفظ شوند؛ حذف یا rotation آن‌ها از restore عادی استنتاج نشود.
