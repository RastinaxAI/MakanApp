# تنظیمات Test و پاک‌سازی محدود مخزن اصلی

## محدوده و نتیجهٔ تنظیمات

این کار فقط در `D:\Project\MakanApp\MakanApp-Backend` انجام شده است؛ هیچ worktree دیگری همگام یا حذف نشده است.

فایل واقعی `MakanApp.Api/appsettings.Test.json` کنار فایل‌های پایه، Development و Production ساخته شد. مرجع این بار خود فایل خصوصی Production در همین checkout اصلی است، نه نمونهٔ عمومی و نه فایل worktree دیگر. تمام بخش‌ها، نام کلیدها، nesting، آرایه‌ها، نوع و مقدارهای غیر OTP دقیقاً برابرند. بخش OTP:

```json
{
  "Otp": {
    "Enabled": false,
    "AllowMockAuthentication": true
  }
}
```

Production فعلی نیز همین مقدارهای OTP را داشت؛ بنابراین کپی دقیق آن نیازی به تغییر Production نداشت. هر دو JSON parse شدند و مقایسهٔ تمام مقدارها بدون چاپ آن‌ها موفق بود. Hash فایل Production با baseline اولیه برابر است.

## مرز secret و Git

Production واقعی credential دارد. ابتدا کنترل محیط کپی آن‌ها را رد کرد؛ پس از تأیید صریح مالک که این اتصال فقط آزمایشی است، مقدارهای کامل فقط به فایل خصوصی Test همین رایانه منتقل شدند. هیچ مقدار secret در این سند یا log چاپ نشده است.

تنها تغییر `.gitignore`، افزودن مسیر دقیق `/MakanApp.Api/appsettings.Test.json` است. Test خصوصی **untracked و ignored** می‌ماند؛ Git نباید نسخهٔ واقعی آن را حمل کند. clone جدید این فایل خصوصی را دریافت نمی‌کند و باید مقدارهای مجاز محیط خود را به‌صورت امن تأمین کند. buildهای Web SDK ممکن است appsettings را در bin/obj خصوصی و ignored کپی کنند؛ آن خروجی‌ها را عمومی نکنید.

در تاریخچهٔ فعلی `origin/main` همین نام فایل به‌صورت عمومی نسخه‌بندی شده است. `.gitignore` به‌تنهایی فایل tracked را خصوصی نمی‌کند. پیش از هر همگام‌سازی آینده باید تکلیف فایل خصوصی/الگو و تعارض‌ها جداگانه مشخص شود؛ در این کار merge/pull/reset/stash انجام نشده است.

## پاک‌سازی انجام‌شده

فقط دو پوشهٔ زیر حذف شدند:

| مسیر | تعداد فایل | اندازهٔ بایت |
| --- | ---: | ---: |
| MakanApp.Domain/bin/Release | ۳ | ۴۲۷۹۶۱ |
| MakanApp.Application/bin/Release | ۵ | ۳۳۵۲۰۶۷ |

جمع: ۸ فایل و ۳۷۸۰۰۲۸ بایت، حدود ۳٫۶ MiB. محتویات فقط DLL/PDB و deps.json تولیدی SDK بودند. DLL/PDBها byte-for-byte با نسخه‌های باقی‌مانده در `MakanApp.Api/bin/Release/net9.0` برابر بودند؛ metadata وابستگی فقط project reference داشت. هر دو مسیر ignored و فاقد فایل tracked، reparse point، تنظیمات یا secret بودند و process فعالی از آن مسیرها اجرا نمی‌شد. مسیرهای مطلق پیش از حذف با allowlist محدود داخل مخزن تطبیق داده شدند. فایل‌ها قابل بازتولید با Build Release هستند؛ نسخه‌های API نیز حفظ شدند. از blanket cleanup یا `git clean` استفاده نشد.

## موارد حفظ‌شده و دلیل

- هر ۹ worktree ثبت‌شده حفظ شد؛ قدیمی یا detached بودن، دلیل کافی برای disposable بودن نیست.
- ۲۲ فایل dirty قبلی AI، شامل source، تست، سند و ابزار untracked، حفظ می‌شوند و hash آن‌ها با baseline کنترل می‌شود؛ هیچ‌کدام متعلق به این Commit نیست.
- `publish`، `.artifacts/publish` و بستهٔ تازهٔ `f84891a` حفظ شدند؛ deployment artifact هستند و بعضی خروجی‌های قدیمی appsettings خصوصی دارند.
- `.artifacts/database-backups`، `.artifacts/deployment` و SQLهای migration حفظ شدند؛ backup/deployment یا شواهد عملیاتی‌اند.
- `.artifacts/jwt-debug-20261021`، خروجی‌های AI/Step10G، اسناد تولیدشده، IDE state و سایر bin/obj حفظ شدند؛ ممکن است secret، evidence، پیکربندی خصوصی یا تنها نسخهٔ کار ناتمام داشته باشند.
- پوشهٔ داخلی `MakanApp` حفظ شد؛ از نام/خالی‌بودن ظاهری نمی‌توان مجوز حذف workspace فعال را استنباط کرد.

## معماری و محدودیت رفتار

هیچ Controller، service، منطق Authentication/OTP، Domain، model، EF configuration، migration، solution یا package تغییر نکرد. این مرحله صرفاً یک فایل تنظیمات خصوصی و قاعدهٔ Git مربوط به آن را اضافه می‌کند؛ وجود فلگ در JSON به‌تنهایی اثبات نمی‌کند کد قدیمی این checkout آن را مصرف می‌کند. این کار implementation جدید Mock OTP یا حفاظت‌های موجود در worktree جدید را به checkout قدیمی وارد نکرده است.

دیتابیس عادی، سرویس SMS و سرور اجرا/تغییر داده نشدند. تست‌ها از LocalDB و providerهای تست موجود استفاده می‌کنند. Migration جدید ساخته یا روی دیتابیس عادی اعمال نشد.

## Validation

Restore و Build اولیهٔ مخزن اصلی موفق شدند؛ Restore و Build نهایی نیز موفق بودند و هر دو Build صفر warning/error داشتند. مجموعهٔ کامل تست‌ها دو بار بدون filter اجرا شد و هر دو بار نتیجهٔ زیر را داشت:

| مجموعه | اجراشده/موفق | ناموفق | Skip |
| --- | ---: | ---: | ---: |
| Unit | ۵۷۱ | ۰ | ۰ |
| Integration | ۱۰۲۸ | ۰ | ۰ |
| Architecture | ۱۴ | ۰ | ۰ |
| مجموع | ۱۶۱۳ | ۰ | ۰ |

Integration اول ۶ دقیقه و ۲۶ ثانیه و نهایی ۶ دقیقه و ۶ ثانیه طول کشید. counterهای هر شش فایل TRX کنترل شدند؛ مسیر خروجی‌ها `tests/<Project>/TestResults/original-test-config-cleanup-regression.trx` و `original-test-config-cleanup-final.trx` است و وارد Git نمی‌شوند. تست جدیدی اضافه نشد؛ برابری واقعی تنظیمات و معتبر بودن JSON با بررسی مستقیم و بدون چاپ مقدارها تأیید شد. عدد ۱۶۱۳ متعلق به همین checkout اصلی است و نباید با ۲۱۹۴ worktree جدید جایگزین شود.

## Git و مانع Push

branch اصلی `main` با upstream برابر `origin/main` است. پیش از این کار `main` محلی ۱۸ Commit از Remote عقب بود. همگام‌سازی آن، فایل‌های Authentication و تغییرات AI را درگیر می‌کند؛ مالک صریحاً انتخاب کرد که این کار فقط تنظیمات/پاک‌سازی باشد و در صورت نیاز Push به ادغام، متوقف شود.

فقط `.gitignore` و همین گزارش برای ثبت مرتبط‌اند؛ Test خصوصی و تمام تغییرات AI باید از staging خارج بمانند. در صورت موفقیت Validation، فقط همین تغییرات ثبت می‌شوند. نتیجهٔ واقعی Commit/Push و شناسهٔ Commit در گزارش نهایی گفتگو ثبت می‌شود. هیچ force-push یا بازنویسی تاریخچه مجاز نیست.

کنترل محیط چاپ diff کاملِ تغییرات قبلی AI را، حتی با redaction حدسی، به علت احتمال افشای credential رد کرد. آن diff چاپ نشد؛ برای فایل‌های نامرتبط، وضعیت/نام مسیرها و hashهای baseline بررسی می‌شود. diff کاملِ staged محدود به همین دو فایلِ بدون secret جداگانه بازبینی می‌شود؛ این محدودیت نباید به‌عنوان بازبینی محتوایی تغییرات ناتمام AI گزارش شود.

## نکته‌های آموزشی و مطالعه

۱. برابری فایل محیط با Production لزوماً به معنی قابلیت commit کردن آن نیست؛ مرز secret مستقل است.
۲. ignored بودن برای فایل tracked اثر حفاظتی ندارد؛ پیش از تغییر branch باید index و Remote هم بررسی شوند.
۳. stale بودن یک worktree یا publish به معنی بی‌ارزش بودن فایل‌های خصوصی آن نیست.
۴. build artifact قابل حذف را با بررسی محتوا، duplicate hash، وضعیت Git، مسیر واقعی و استفادهٔ جاری اثبات کنید.
۵. Validation روی checkout قدیمی، جایگزین همگام‌سازی کد آن با Remote نیست.

مسیر مطالعه: `.gitignore` → فایل خصوصی Test و Production فقط در محیط امن → `git status`/`git worktree list` → TRXهای این checkout → گزارش اختلاف تاریخچه. مرحلهٔ بعد، در صورت درخواست جداگانه، طراحی همگام‌سازی main با حفظ AI و مرزبندی فایل خصوصی است؛ ادغام Authentication یا بازآرایی سرویس‌ها نباید پنهانی وارد این کار شود.
