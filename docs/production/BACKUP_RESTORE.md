# Runbook پشتیبان‌گیری و بازیابی

## سیاست پیشنهادی

- Full backup زمان‌بندی‌شده با `CHECKSUM` و retention تأییدشده.
- اگر Recovery Model برابر Full است، log backup با فاصله متناسب با RPO و monitoring شکست job.
- `RESTORE VERIFYONLY` مفید است اما جای restore drill را نمی‌گیرد.
- مسیر `Storage__RootPath` جداگانه versioned/snapshot شود؛ secrets فقط با reference و روش بازیابی در secret manager نگه‌داری شوند.
- backupها encrypted، دسترسی‌محدود و خارج از همان failure domain سرور اصلی باشند.

## سازگاری DB و Storage

DB مرجع metadata و authorization فایل است و storage بایت‌ها را نگه می‌دارد. برای نقطه‌ی سازگار، ابتدا writeها drain شوند؛ زمان/شناسه‌ی backup DB و snapshot storage با هم ثبت شود. بازیابی فقط یکی از این دو می‌تواند metadata یتیم یا فایل بدون metadata ایجاد کند.

## Restore drill روی محیط disposable

1. یک دیتابیس با نام یکتای drill و storage directory خالی بسازید؛ هرگز DB توسعه یا Production را target نکنید.
2. full backup را با `WITH CHECKSUM` restore کنید و در صورت نیاز log chain را تا زمان هدف اعمال کنید.
3. snapshot storage متناظر را restore و مجوز identity برنامه را اعمال کنید.
4. با connection string موقت، migration history، row countهای پایه، `/health/ready` و smoke test read-only را بررسی کنید.
5. چند فایل مجاز را از API بخوانید و SHA-256 ذخیره‌شده/واقعی را تطبیق دهید.
6. نتیجه، زمان restore، RPO/RTO مشاهده‌شده و خطاها را ثبت کنید؛ سپس محیط disposable را طبق فرایند تأییدشده پاک کنید.

اسکریپت disposable برای این کار در `tools/production/sql/restore-drill-localdb.sql` قرار دارد. نتیجه‌ی اجرای STEP 16 باید در گزارش نهایی ثبت شود؛ موفقیت LocalDB فقط صحت پایه‌ی backup/restore را نشان می‌دهد و جای rehearsal روی topology واقعی staging را نمی‌گیرد.

### نتیجه‌ی drill در STEP 16

در ۲۰۲۶-۱۰-۱۳ یک دیتابیس منبع disposable روی `(localdb)\\MSSQLLocalDB` ساخته، با `CHECKSUM` backup، با `MOVE` در دیتابیس disposable دوم restore و marker داده validate شد. خروجی `STEP16_RESTORE_DRILL_SUCCEEDED` بود و هر دو دیتابیس و فایل `.bak` موقت در cleanup حذف شدند. این نتیجه فقط محیط local را پوشش می‌دهد؛ restore از backup واقعی staging همراه با storage snapshot هنوز باید rehearsal شود.

## نمونه T-SQL عملیاتی

```sql
BACKUP DATABASE [MakanApp]
TO DISK = N'<secure-backup-path>\MakanApp_full.bak'
WITH CHECKSUM, COMPRESSION, INIT, STATS = 10;

RESTORE VERIFYONLY
FROM DISK = N'<secure-backup-path>\MakanApp_full.bak'
WITH CHECKSUM;
```

مسیر، edition، encryption و credential باید توسط DBA محیط تعیین شود.
