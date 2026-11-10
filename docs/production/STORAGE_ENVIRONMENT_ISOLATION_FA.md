# تفکیک Storage محیط‌های Test و Production

## تصمیم جاری

| محیط | فایل خصوصی | Storage:RootPath | دیتابیس موجود |
|---|---|---|---|
| Test | `MakanApp.Api/appsettings.Test.json` | `C:\MakanAppData\Test\Storage` | `MakanApp_Test` |
| Production | `MakanApp.Api/appsettings.Production.json` | `C:\MakanAppData\Production\Storage` | `MakanApp` |

این تصمیم جایگزین انتخاب قبلی مسیر مشترک برای Production است. هیچ‌کدام از این دو محیط نباید به `C:\MakanAppData\Storage` برگردند. فایل خصوصی هر محیط جدا و Git-ignored است. هنگام تغییر RootPath، Connection String، Identity key، SMS و سایر مقادیر موجود حفظ می‌شوند. نمونه‌های بدون secret نیز Root جدا دارند؛ تنظیمات Development تغییر نمی‌کند.

## نصب توسط اپراتور

1. قبل از تغییر نسخه، از تنظیمات خصوصی، دیتابیس و Storage فعال هر محیط Backup هماهنگ بگیرید و نوشتن/Cleanup همان محیط را متوقف کنید. تغییر RootPath فایل قبلی را جابه‌جا نمی‌کند. اگر فایل‌ها فعلاً در Root مشترک هستند، انتقال آن‌ها نیازمند inventory معتبر FileAsset/StorageKey هر دیتابیس و یک عملیات جدا و کنترل‌شده است؛ همهٔ فایل‌ها را کورکورانه به هر دو محیط کپی نکنید. قبل از تکمیل انتقال معتبر، نسخهٔ جدید را برای استفادهٔ عمومی شروع نکنید.
2. App Pool و هویت اجرای Test و Production جدا باشند. به هویت هر محیط فقط روی Root همان محیط دسترسی لازم بدهید؛ مجوز Modify مشترک روی والد `C:\MakanAppData` یا دسترسی متقابل به Root محیط دیگر ندهید. ACL واقعی باید در مقصد بررسی شود؛ این Task سرور را تغییر نمی‌دهد.
3. Rootها باید دو پوشهٔ فیزیکی مستقل باشند؛ junction، symbolic link، mount یا share مشترک نباید آن‌ها را به یک مقصد وصل کند. متفاوت بودن متن مسیرها به‌تنهایی استقلال OS را اثبات نمی‌کند.
4. `ASPNETCORE_ENVIRONMENT` و `DOTNET_ENVIRONMENT` در web.config باید محیط صحیح باشند. `Storage__RootPath` و تنظیمات تزریق‌شدهٔ IIS می‌توانند JSON را override کنند؛ override قدیمیِ مسیر مشترک را پیش از شروع اصلاح کنید، بدون چاپ secret.
5. با فایل تست مجاز، Upload/Download/Delete را در هر محیط بررسی کنید؛ فایل محیط دیگر باید ثابت بماند. Cleanup را فقط با دادهٔ terminal آزمایشی همان محیط بررسی کنید. تست محلی، اثبات ACL یا وضعیت فایل‌های واقعی سرور نیست.

## معماری و جریان موجود

`DependencyInjection`، `StorageOptions.RootPath` را به `LocalFileStorage` می‌دهد. Save/OpenRead/Delete زیر همان Root resolve می‌شوند. `TerminalFileCleanupProcessor` از `IFileStorage` همان host و داده‌های دیتابیس همان محیط استفاده می‌کند؛ بنابراین DB و Root هر دو باید محیطی بمانند. کنترل path traversal و محافظ مسیر terminal موجود حفظ شده‌اند؛ ACL قابل‌اعتماد همچنان لازم است.

فقط تنظیمات خصوصی، نمونهٔ Production و این راهنما تغییر می‌کنند. Domain، Application، Infrastructure، Controller، Endpoint و مدل دیتابیس تغییر ندارند. هیچ Migration، منطق Authentication یا Storage تازه‌ای ایجاد نمی‌شود. هیچ فایل ذخیره‌شده‌ای در این مرحله منتقل یا حذف نمی‌شود.

## اعتبارسنجی و آموزش

مقایسهٔ JSON باید نشان دهد در فایل‌های خصوصی فقط `Storage:RootPath` تغییر کرده است؛ Secretها وارد Git نمی‌شوند. Build/Test اجباری مخزن، Smoke بسته‌های استخراج‌شده، مقایسهٔ باینری و SHA256 نتایج جدا دارند و در گزارش همان Release ثبت می‌شوند. موفقیت محلی به معنی Deploy نیست.

نکات مطالعه: Root و DB باید با هم تفکیک شوند؛ environment variable بر JSON اولویت دارد؛ اشتراک هویت OS می‌تواند مرز فیزیکی را تضعیف کند؛ تغییر Root، migration محتوای فایل نیست؛ Backup تنظیمات خصوصی نباید عمومی یا Git-tracked شود.

مسیر مطالعه: `StorageOptions` → ثبت موجود در `DependencyInjection` → `LocalFileStorage` → `TerminalFileCleanupProcessor` → `TerminalFileCleanupFileSystemTests` و `FileStorageEndpointsTests` → راهنمای نصب Release. قدم بعد، بررسی inventory/ACL و نصب توسط اپراتور مجاز است؛ انتقال دادهٔ قبلی و تغییر schema با این اصلاح مخلوط نشوند.
