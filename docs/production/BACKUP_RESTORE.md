# Runbook پشتیبان‌گیری و بازیابی

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
