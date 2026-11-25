# بازتولید ممیزی

این پوشه ابزار ممیزی است، نه Publish یا مسیر Deploy. از ریشهٔ Backend اجرا شود. فایل‌های موجود Evidence را برای یک run تازه بازنویسی نکنید؛ runId و مسیر خروجی تازه لازم است.

## تست‌های اصلاح خطا

```powershell
dotnet restore MakanApp.sln
dotnet build MakanApp.sln --configuration Release --nologo
dotnet test tests/MakanApp.IntegrationTests/MakanApp.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~ApiErrorAuditEndpointsTests
dotnet test tests/MakanApp.UnitTests/MakanApp.UnitTests.csproj --configuration Release --no-build --filter FullyQualifiedName~ErrorAuditHttp200WithBusinessFailure
dotnet test tests/MakanApp.IntegrationTests/MakanApp.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~ErrorAuditSameIntentAtObservedPendingState
dotnet test MakanApp.sln --configuration Release --no-build
dotnet build MakanApp.sln --configuration Release --nologo
dotnet test MakanApp.sln --configuration Release --no-build
```

تست race شش timing دارد و در هرکدام ده fixture مستقل می‌سازد. SQL و barrier قبل از تأخیر مشاهده می‌شوند؛ تأخیر به‌تنهایی اثبات Pending نیست. Migration خودکار فقط روی دیتابیس‌های ایزولهٔ Factory فعلی اجرا می‌شود. به دیتابیس عادی یا سرور وصل نشوید.

## metadata بدون SQL

```powershell
dotnet run --project tools/docs/MakanApp.DocumentationSnapshot/MakanApp.DocumentationSnapshot.csproj --configuration Release -- .artifacts/error-audit-metadata-new-run
```

ابزار موجود، worker و AI/SMS واقعی را اجرا نمی‌کند. خروجی `PendingModelChanges` از API رسمی EF گرفته می‌شود. دستور CLI معمول در این run با مشکل load اسمبلی Design مواجه شد؛ نتیجهٔ ناموفق آن در validation حفظ شده و با PASS اشتباه نشده است.

## پردازش Evidence موجود

```powershell
python reports/api-error-audit/20261125-api-errors-ac9a239-01/repro/inventory.py
python reports/api-error-audit/20261125-api-errors-ac9a239-01/repro/collect_attempts.py
python reports/api-error-audit/20261125-api-errors-ac9a239-01/repro/build_handoff.py
python reports/api-error-audit/20261125-api-errors-ac9a239-01/repro/verify_evidence.py
```

`finalize_report.py` فقط پس از PASS واقعی تمام pipeline اجرا می‌شود. `complete_phase_a.py` مربوط به checkpoint پیش از Fix است و پس از تغییر source نباید دوباره اجرا شود. `public_probes.py` فقط درخواست عمومی و بدون credential دارد؛ این script هرگز ورود، Seed، AI invocation احرازشده یا تست UI نقش‌ها نیست و فایل‌های قبلی را دوباره اجرا نمی‌کند.

در اجرای این run، stdout و assertion payload خام ذخیره نشده‌اند؛ فقط نام تست، شمارش و Evidence پالایش‌شده ثبت شده است. پاسخ‌های race از timeline مستقل استخراج می‌شوند؛ برای duplicate/replay، timestamp شروع هر درخواست در helper قبلی export نشده و این محدودیت با `timePrecision` صریح است. این موارد trace کامل UI نیستند. درخواست‌های آماده‌سازی fixture و Auth داخلی در تعداد attemptها منظور نشده‌اند.

`backend-only-fixes.patch` فقط برای بازبینی تفاوت با baseline است. آن را کورکورانه روی Working Tree دارای WIP اعمال نکنید. تغییرات قبلی Deadline، AI و private configها نباید برای بازتولید حذف، reset، stash یا stage شوند.

## حسابداری نهایی

از `python reports/api-error-audit/20261125-api-errors-ac9a239-01/repro/verify_accounting.py` برای بررسی آفلاین ۱۰۷ مورد استفاده کنید. این ابزار `cases.csv` را عوض نمی‌کند. `finalize_report.py` و `verify_evidence.py` به baseline خصوصی اجرای تاریخی متکی‌اند؛ آن‌ها را پس از Closure/Commit برای بازنویسی گزارش جاری اجرا نکنید. نتیجهٔ واقعی انتشار جدید در manifest همان بسته ثبت می‌شود.
