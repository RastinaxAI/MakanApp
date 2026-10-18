# امنیت SQL Server

<!-- CURRENT-REVIEW:BEGIN -->
بازبینی `2026-10-18` از `main@ef67624`؛ [وضعیت جاری](../CURRENT_PROJECT_STATUS.md)، [ماتریس آمادگی](PRODUCTION_READINESS.md) و [runbook IIS](DEPLOYMENT_IIS.md) مبنای gateها هستند. این بازبینی deployment، credential واقعی یا load/restore محیط هدف را اثبات نمی‌کند.
<!-- CURRENT-REVIEW:END -->

## اصل least privilege

برنامه نباید با `sa`، Windows Administrator یا `db_owner` اجرا شود. دو principal جدا لازم است:

- Runtime principal: فقط `CONNECT` و DML روی schemaهای برنامه؛ بدون `ALTER` و `CONTROL`.
- Migration principal: credential جدا با دسترسی DDL فقط در پنجره‌ی deployment؛ در connection string runtime قرار نمی‌گیرد.

اسکریپت‌های template در `tools/production/sql/create-runtime-user.sql` و `create-migration-user.sql` قرار دارند. loginها باید پیش‌تر توسط DBA و با secret خارج repository ساخته شوند. پس از migration، سایت فقط با runtime principal بالا می‌آید.

## TLS و شبکه

- connection string Production: `Encrypt=True;TrustServerCertificate=False`.
- استثنای مصوب انتشار فعلی فقط با Database:AllowUntrustedServerCertificate=true در Production، پذیرش TrustServerCertificate=True را مجاز می‌کند؛ Encrypt=False همچنان رد است. رمزگذاری با تأیید هویت گواهی یکی نیست و این استثنا توصیهٔ امنیتی یا مجوز حساب پرقدرت runtime محسوب نمی‌شود. پس از فراهم شدن گواهی معتبر هر دو مقدار به حالت امن برگردند؛ [قرارداد انتشار](DEPLOYMENT_IIS.md).
- certificate SQL Server باید SAN درست و trust chain معتبر روی Windows Server داشته باشد.
- پورت 1433/instance port فقط از subnet یا host برنامه و مسیر مدیریت خصوصی مجاز باشد؛ exposure عمومی ممنوع است.
- SSMS فقط از RDP/local/private management path استفاده کند.
- login auditing و failed login monitoring فعال شود؛ password و connection string وارد log نشوند.

## اجرای migration

```powershell
$env:ConnectionStrings__MakanDatabase = '<migration-principal-connection-string>'
dotnet ef database update --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --context MakanDbContext --configuration Release
dotnet ef migrations has-pending-model-changes --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --context MakanDbContext --configuration Release
```

قبل و بعد از اجرا نام migration در `__EFMigrationsHistory` بررسی شود. migration principal فقط در job امن deployment در دسترس باشد و سپس secret آن از process پاک شود.

برای handoff فایل SQL/SSMS، ابزار export-migration-sql.ps1 و تست مستقل test-migration-sql.ps1 در [runbook IIS](DEPLOYMENT_IIS.md) مرجع‌اند؛ خروجی خام قبلی برای backfill ستون تازه خطای compile 207 داشت. ۳۷ Migration موجود تا EnforceApprovedMessengerChildSafety داریم؛ همگام‌سازی اسناد Migration جدیدی ایجاد یا اجرا نمی‌کند.
