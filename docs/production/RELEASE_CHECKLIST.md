# Release Checklist

## پیش از انتشار

- [ ] `dotnet restore`, دو بار build/test نهایی و `git diff --check` سبز است.
- [ ] `dotnet ef migrations has-pending-model-changes` بدون pending model است.
- [ ] SQL با `tools/production/export-migration-sql.ps1` تولید و با `tools/production/test-migration-sql.ps1 -ConfirmLocalDisposableDatabase` روی LocalDB ایزوله آزمایش شده؛ hash فایل تحویلی با ScriptSha256 گزارش یکسان است.
- [ ] artifact از commit/branch مورد انتظار ساخته شده و secret ندارد.
- [ ] فایل خصوصی `appsettings.Production.json` خارج از Git و از نمونه جداست؛ اگر به artifact انتقال اپراتور اضافه شده، artifact محرمانه و غیرقابل‌اشتراک است. `appsettings.Staging.json` در خروجی تازه وجود ندارد.
- [ ] انتشار HTTP-only فقط با تأیید صریح دو استثنای مستقل `Hosting:AllowInsecureHttp` و `Database:AllowUntrustedServerCertificate` انجام می‌شود؛ این تأیید جای TLS امن، حساب SQL کم‌اختیار یا smoke زنده را نمی‌گیرد.
- [ ] Production blockers و قابلیت‌های unavailable به ذی‌نفعان اعلام شده‌اند.
- [ ] backup DB و storage گرفته و reference آن ثبت شده است.
- [ ] runtime و migration principal جدا و connection TLS معتبر است.
- [ ] CORS origins، AllowedHosts، known proxy، storage root و write permission تأیید شده‌اند.
- [ ] .NET 9 Hosting Bundle، IIS WebSocket، HTTPS binding، firewall و log collector آماده‌اند.

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
