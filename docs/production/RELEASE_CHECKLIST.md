# Release Checklist

## پیش از انتشار

- [ ] `dotnet restore`, دو بار build/test نهایی و `git diff --check` سبز است.
- [ ] `dotnet ef migrations has-pending-model-changes` بدون pending model است.
- [ ] artifact از commit/branch مورد انتظار ساخته شده و secret ندارد.
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
- [ ] login/OTP فقط به‌صورت دستی در test environment و بدون SMS واقعی خودکار بررسی شده است.
- [ ] profile، workspace، یک read اصلی و boundaryهای 401/403 بررسی شده‌اند.
- [ ] SignalR negotiate، DB read و storage probe قابل استفاده‌اند.
- [ ] 5xx، latency، worker backlog و provider degraded status مشاهده شده‌اند.

## rollback

- [ ] معیار شکست و تصمیم‌گیر rollback مشخص است.
- [ ] artifact قبلی و تنظیمات سازگار موجود است.
- [ ] migration backward compatibility ارزیابی شده؛ downgrade خودکار فرض نشده است.
- [ ] برای schema ناسازگار، forward-fix یا restore نقطه‌ی سازگار DB+storage تعیین شده است.
- [ ] trace IDها، log window و incident notes پیش از rollback حفظ می‌شوند.
