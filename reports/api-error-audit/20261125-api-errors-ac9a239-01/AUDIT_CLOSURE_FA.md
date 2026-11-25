# بسته‌شدن حسابرسی و مرز انتشار

بازبینی ۲۰۲۶/۱۱/۲۵ با مجوز «CLOSE API ERROR AUDIT + PREPARE TEST RELEASE» انجام شد. هر ۱۰۷ Case تعیین تکلیف شده است: ۱۴ PASS، یک FAIL، ۸۹ BLOCKED با دلیل و سه NOT_APPLICABLE با شاهد. شناسه، عنوان، مراحل، invariant و نتیجهٔ هیچ Case عوض نشده است. ۵۵۵ checksum شواهد قبلی پیش از ویرایش گزارش‌ها بررسی شدند؛ checksumهای جاری پس از Closure دوباره تولید می‌شوند.

`AUDIT_COMPLETE=YES` فقط کامل‌بودن حسابرسی را بیان می‌کند. `LIVE_ACCEPTANCE_COMPLETE=NO` و `LIVE_FAILURE_COUNT=1` باقی می‌مانند. پاسخ زندهٔ 405 فاقد code بود، ولی Allow و routing درست بودند. نداشتن Fix محلی در نسخهٔ مستقر یک توضیح `SUPPORTED` است؛ Commit/Build مستقر معلوم نیست و علت استقرار را CONFIRMED نمی‌نامیم. هیچ ورود زنده، اجرای دوبارهٔ ۱۰۷ سناریو یا تغییر سرور در Closure انجام نمی‌شود.

در کنترل checkout تمیز مشخص شد قاعدهٔ سراسری text=auto و core.autocrlf می‌تواند بایت‌های Evidence را عوض کند و SHA256 مرجع را بشکند. فایل محلی `.gitattributes` فقط در همین پوشه، تبدیل متن را غیرفعال می‌کند. بایت‌های ورودی و Caseها حفظ شده‌اند؛ پس از ثبت این اصلاح، checksumها در checkout نهایی هم بررسی می‌شوند. این تغییر اثری بر line ending کد یا سایر اسناد مخزن ندارد.

`RUNTIME_DEADLINE_ROOT_CAUSE=AWAITING_EVIDENCE` مستقل باقی می‌ماند. قابلیت مصوب تمدید Published Deadline و تغییر مستقل AllowLateSubmission به Evidence تاریخی آن رخداد وابسته نیست. درخواست قبلی Upload موفق بوده و خطا در Attach رخ داده است؛ از این شاهد، dueAtUtc، allowLateSubmission یا وضعیت تاریخی تلاش را حدس نمی‌زنیم.

## دسته‌بندی تغییرات

| دسته | دامنه | تصمیم |
|---|---|---|
| A | Domain/Application/Infrastructure تکلیف، Controller، OpenAPI، تست‌های Published Policy و Deadline، Handoff و اسناد تولیدشدهٔ همین قرارداد | ثبت مستقل قابلیت تکلیف |
| B | ApiProblemDetailsContract، ApiErrorCodes و فراخوانی‌های محدود در Program/GlobalExceptionHandler | ثبت مستقل اصلاح قرارداد خطا |
| C | تست‌های HTTP/AI/race، اصلاح cleanup در DevelopmentTestUsersTests و metadata بررسی pending model | ثبت پوشش Audit؛ cleanup از نشت selector دیتابیس موقت و OTP بین fixtureها جلوگیری می‌کند |
| D | گزارش، ledger، referenceها و شواهد sanitised همین ممیزی | ثبت گزارش با تفکیک Local و Live |
| E | کار قبلی نامرتبط و backupهای خصوصی | حفظ و عدم ورود ناخواسته به Commit |
| F | سه فایل tools/ai/provider_diagnostic.py، run_exact_ai.py و test_provider_diagnostic.py | بدون تغییر و خارج از Commit |
| G | appsettings خصوصی و سایر تنظیمات موجود | بدون تغییر و خارج از Git؛ فقط نسخهٔ خصوصی بسته با مجوز قبلی مالک |
| H | کد و تنظیمات MeliPayamak | بدون تغییر و خارج از Commit |

Program و GlobalExceptionHandler بین A و B مشترک‌اند. سهم A از snapshot قبل از اصلاح Error Audit وارد index می‌شود و سپس سهم B جدا ثبت می‌شود؛ فایل Working Tree برای این جداسازی جایگزین نمی‌شود. وضعیت مسیرها در `evidence/closure-scope.json` ثبت می‌شود.

## قرارداد واقعی اصلاح خطا

- 400 → REQUEST_INVALID، 404 → RESOURCE_NOT_FOUND، 405 → METHOD_NOT_ALLOWED، 413 → REQUEST_TOO_LARGE، 415 → UNSUPPORTED_MEDIA_TYPE.
- کد اختصاصی موجود، از جمله SUBMISSION_DEADLINE_PASSED، جایگزین نمی‌شود. بدنهٔ نوشته‌شده، HEAD و مسیر غیر API بازنویسی نمی‌شوند.
- Retry-After فقط از دادهٔ typed خطای AI/Knowledge برای 429/503 نگه داشته می‌شود؛ مقدار قبلی header حفظ، مقدار منفی رد، صفر حفظ و کسر ثانیه رو به بالا گرد می‌شود. هیچ retry یا timeout جدیدی اضافه نشده است.
- status واقعی، traceId و correlationId حفظ می‌شوند؛ fixture خطای ناشناخته و canary اطلاعات خصوصی را نیز بررسی می‌کند.

## Source و Validation

Source فعلی با هر ۱۶۱۸ فایل C#/csproj اجرای موفق ۳۷۸۹ تست قبلی تطبیق داده شد. نتیجهٔ آن اجرا Unit=1499، Integration=2274 و Architecture=16، با صفر شکست و Skip بود. این نتیجه، جای Regression موردنیاز روی Commit نهایی را نمی‌گیرد.

پس از Commitهای مرتبط، checkout تمیز و جدا از همان Commit برای Restore، Release Build، Unit، Integration، Architecture و Final Build استفاده می‌شود. فقط دیتابیس‌های موقت ایزولهٔ تست مجازند؛ Migration روی MakanApp، MakanApp_Test یا SQL مقصد اجرا نمی‌شود. مدل جدید و Migration تازه‌ای در این اصلاح وجود ندارد.

## نتیجهٔ انتشار و پذیرش

FINAL_RESULT.json و FINAL_STATUS.txt این پوشه snapshot حسابرسی پیش از Commit هستند. برای جلوگیری از وابستگی دوری hash سند به Commit حاوی خودش، نتیجهٔ واقعی Commit، Regression نهایی، Push و بسته‌ها در RELEASE_MANIFEST.json و FINAL_STATUS.txt مسیر خصوصی `.artifacts/close-api-error-audit-20261125/release` ثبت می‌شود. وضعیت آینده در این سند PASS فرض نشده است.

پس از موفقیت exact committed tree و Push/Remote verification، دو ZIP مستقل Test و Production candidate ساخته می‌شوند. تنظیمات خصوصی، دیتابیس و Storage جدا حفظ می‌شوند. Swagger فقط در خروجی هر دو بسته طبق مجوز قبلی فعال می‌شود. Test دارای Mock OTP و AI/Knowledge فعال با Service Authentication خاموش است؛ Production دارای OTP واقعی و AI/Knowledge خاموش مطابق تنظیم فعلی است.

`READY_FOR_MANUAL_TEST_DEPLOY=YES` فقط پس از موفقیت Gateهای محلی و بسته‌سازی مجاز است. `TEST_LIVE_ACCEPTANCE=NOT_RUN_ON_FINAL_COMMIT` و `READY_FOR_MANUAL_PRODUCTION_DEPLOY=NO` باقی می‌مانند؛ Production ZIP صرفاً CANDIDATE است. Server connection، Migration مقصد، IIS Deployment، Test/Production Deployment و Merge به main انجام نمی‌شوند.
