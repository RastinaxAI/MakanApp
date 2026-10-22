# گزارش بازیابی و ادغام کنترل‌شدهٔ مخزن اصلی

## ۱. خلاصهٔ مرحله

پوشهٔ اصلی روی `main@90f3cf8` بود؛ یک Commit محلی و هجده Commit عقب‌مانده از
`origin/main@f84891a` داشت. از تلاش قطع‌شده، بیست فایل Remote وارد شده بودند؛
۱۲۶ فایل بدون تعارض باقی مانده و ۱۴ مسیر با کار ناتمام هم‌پوشانی داشتند.
هیچ Merge، Rebase، Cherry-pick یا Sequencer فعالی وجود نداشت و Index خالی بود.
این مرحله کد موجود را به پوشهٔ اصلی آورد؛ Authentication یا ماژول جدیدی طراحی نشد.

## ۲. معماری

.NET 9، EF Core 9، SQL Server، Onion، Modular Monolith و Controller API حفظ شدند.
Domain قواعد فعلی را نگه می‌دارد؛ Application در `IdentityService` مالک جریان
Request/Verify/Login/Register است. Infrastructure محیط، Options، Provider و EF را
می‌سازد. Api فقط ورودی HTTP، pipeline، Startup و تنظیمات را میزبانی می‌کند.
جهت وابستگی همچنان Api → Application → Domain و Infrastructure → Application/Domain است.

## ۳. فایل‌های مهم واردشده

- `MakanApp.Infrastructure/Identity/OtpAuthenticationOptions.cs`: binding و محافظ مرکزی محیط؛ فلگ به‌تنهایی مجوز Mock نیست.
- `MakanApp.Infrastructure/Identity/DevelopmentBypassOtpProvider.cs` و `DevelopmentOtpBypassOptions.cs`: سازگاری زیرساخت قبلی؛ فعال‌سازی legacy bypass با محافظ جدید رد می‌شود.
- `tests/MakanApp.UnitTests/Identity/MockOtpIdentityServiceTests.cs`: مرز فرمت ورودی، اثبات OTP و side effect را جدا می‌سنجد.
- `tests/MakanApp.IntegrationTests/ConfigurableOtpAuthenticationTests.cs` و `ServerMockOtpAuthenticationTests.cs`: HTTP، SQL، Token، Refresh، suspension و نبود افزایش مجوز را بررسی می‌کنند.
- فایل‌های Data Protection، Logging و Worker Recovery همان پیاده‌سازی Commitهای مصوب Remote هستند؛ subsystem موازی ساخته نشد.

## ۴. تغییرات مهم تکمیلی

- `MakanApp.Api/appsettings.Test.json.example` الگوی عمومی و فاقد Secret است.
  فایل واقعی Test و Production خصوصی، بدون تغییر بایت و ignored باقی ماندند.
- `TestConfigurationSchemaTests` و `ServerMockOtpConfigurationTests` الگو را می‌خوانند؛
  shape فایل خصوصیِ override به‌اشتباه معادل قرارداد کامل فرض نمی‌شود.
- `TestEnvironmentConfigurationTests` فقط فایل‌های عمومی را در ContentRoot موقت خودش
  بارگذاری می‌کند؛ credential واقعی یا تنظیمات شخصی وارد میزبان این تست نمی‌شوند.
- case صریح `987654` به Unit و HTTP Integration اضافه شد تا چهار مقدار خواسته‌شده
  همراه caseهای قبلی پوشش داشته باشند. هیچ OTP جادویی در پیاده‌سازی وجود ندارد.

## ۵. جریان اجرای واقعی

`AuthController.RequestOtp` → `IdentityService.RequestOtpAsync` →
`OtpOptions.Enabled` → در Real، `IOtpDeliveryProvider` و `OtpChallenge` و
`EfIdentityStore`/SQL؛ در Mock، فقط پاسخ سازگار بدون ارسال یا ذخیرهٔ Challenge.

`AuthController.VerifyOtp` → `IdentityService.VerifyOtpAsync` →
بررسی فرمت → در Real، قواعد `OtpChallenge.Verify`؛ در Mock، بدون مقایسهٔ کد →
تراکنش Serializable → یافتن/ساخت `User` و `UserCredential` →
کنترل suspension → `UserSession` و `RefreshToken` → پاسخ HTTP.
Access Token کاربر Opaque Bearer است، نه Service JWT مربوط به AI.

## ۶. دیتابیس

Migration، مدل EF و schema جدیدی ایجاد نشد. دیتابیس عادی MakanApp، Seed کاربران و IIS
دست‌کاری نشدند. Integrationها فقط دیتابیس SQL Server LocalDB اختصاصی fixture را طبق
روال موجود بازسازی و migrate کردند؛ این کار اثبات migration روی سرور نیست.
Unique credential، FKها و تراکنش‌های موجود بازاستفاده می‌شوند؛ EnsureCreated یا
اجرای خودکار migration در Startup اضافه نشد.

## ۷. Endpointها و قراردادها

- `POST /api/v1/auth/otp/challenges`: `RequestOtpCommand`؛ پاسخ 202 با
  `RequestOtpResult`؛ شماره normalize و validate می‌شود.
- `POST /api/v1/auth/otp/verify`: `VerifyOtpCommand`؛ پاسخ 200 با
  `VerifyOtpResult`؛ فرمت کد در هر دو Mode بررسی می‌شود.
- `POST /api/v1/auth/refresh` و `GET /api/v1/me`: همان قراردادهای موجود؛
  نشست، Refresh rotation و مجوزهای عادی حفظ می‌شوند.
در OTP هیچ route یا DTO جدیدی اضافه نشد؛ نقش، Membership و MFA از OTP مستقل‌اند.

## ۸. مفاهیم استفاده‌شده

Options و fail-closed Startup، Composition Root، Provider abstraction،
تراکنش SQL، Token rotation، تست HTTP با SQL واقعی، template خصوصی/عمومی و
تفکیک Git history، Index و Working Tree در این مرحله عملاً استفاده شدند.

## ۹. حل تعارض‌ها و تصمیم‌ها

- `AiServiceOptions`: مقصد پیش‌فرض کد خالی شد تا شبکه از روی حدس انتخاب نشود؛
  مقصد صریحِ محلی در appsettings حفظ شد. پیش‌فرض کاری ۳۵ ثانیه باقی ماند و سقف مجاز
  ۱۸۰ ثانیه از پیش‌فرض جدا شد تا تنظیم صریحِ قرارداد جدید هم معتبر بماند.
- `DependencyInjection`: ثبت OTP و زیرساخت Remote اضافه شد؛ fallback از Options
  خوانده می‌شود و در نسخهٔ کاری همچنان بودجهٔ محلی را نگه می‌دارد.
- `AiServiceClient`: Timeout خواندن body، IOException و caller cancellation حفظ شدند؛
  grounding و validation جدید و حذف InnerException خامِ قابل‌نشت نیز باقی ماندند.
- تست‌های AI دو طرف ترکیب شدند؛ همهٔ متدهای سه فایل تست محلی قبلی حفظ شدند.
  502 طبق قرارداد جدید به ResponseInvalid می‌رود؛ انتظار قدیمی متناقض و InlineData
  تکراری حذف شدند، نه پوشش یک سناریوی معتبر.
- `tools/ai`: failure-evidence و self-check آفلاین محلی با policy مقصد HTTPS مصوب،
  خاموش‌بودن redirect، parser امن و کنترل مثبت Auth در Remote ترکیب شدند.
- کل بخش AiService در appsettings با مقادیر اولیهٔ پشتیبان یکسان است، شامل Timeout=35.
  این overlay ناتمام کاری وارد Commit ادغام نمی‌شود؛ نسخهٔ Commit‌شدهٔ AI همان Remote است.

## ۱۰. تست‌ها و شواهد حفظ کار

اولین تست کامل، ۱۳۳۷ Integration و ۱۴ Architecture موفق داشت اما سه Unit شکست خورد:
default مقصد، تعارض default/max timeout و انتظار قدیمی 502. این ناسازگاری‌های واقعی
ادغام اصلاح شدند. دو Warning مربوط به InlineData تکراری نیز بدون suppress رفع شدند.
تمام ۸۵۸ Unit پس از اصلاح موفق شدند. ابزار AI Build شد و self-check موجود
`--verify-failure-sanitization` آفلاین PASS شد؛ هیچ Real AI call اجرا نشد.
این self-check جزو شمار تست‌های xUnit زیر محسوب نشده است.

## ۱۱. نتیجهٔ Validation نهایی

Restore موفق؛ Final Build موفق با صفر Warning و صفر Error.

| مجموعه | اجراشده/موفق | شکست | Skip |
| --- | ---: | ---: | ---: |
| Unit | ۸۵۸ | ۰ | ۰ |
| Integration | ۱۳۳۸ | ۰ | ۰ |
| Architecture | ۱۴ | ۰ | ۰ |
| مجموع | ۲۲۱۰ | ۰ | ۰ |

Integration نهایی ۱۱ دقیقه و ۲۵ ثانیه طول کشید. Counterهای هر سه TRX واقعاً بررسی شدند:
`tests/<Project>/TestResults/main-integration-recovery-final.trx`.
این خروجی‌ها ignored هستند و وارد Commit نمی‌شوند. تست‌ها روی نسخهٔ کاری ترکیب‌شدهٔ
پوشهٔ اصلی اجرا شدند؛ تعداد بیشتر از Remote شامل تست‌های محلیِ حفظ‌شده نیز هست.

## ۱۲. Git و پشتیبان

پشتیبان خصوصی ۲۷فایلی در
`.artifacts/otp-main-integration-private-20261022-c4a07152de09` حفظ شده است.
Manifest و SHA-256 همهٔ کپی‌ها پیش از بازیابی و پس از آن بررسی شدند.
از ۲۲ فایل Dirty اولیه، ۹ فایل عیناً باقی‌اند و ۱۳ مسیر با مقایسهٔ سه‌طرفه ترکیب شدند؛
نسخهٔ بایت‌به‌بایت اولیهٔ همهٔ آن‌ها همچنان در پشتیبان خصوصی موجود است.
تمام مقادیر Test و Production خصوصی و کد/تنظیمات MeliPayamak حفظ شدند.

درخت تاریخچهٔ ثبت‌شده از Merge محلی 90f3cf8 و Remote f84891a محاسبه می‌شود؛ overlay
ناتمام AI در Index عمومی قرار نمی‌گیرد. فایل Test واقعی از tracking خارج می‌ماند و
الگوی عمومی جای آن را می‌گیرد. Commit دووالدی، هر دو تاریخچه را حفظ می‌کند؛
هیچ reset/clean، force-checkout، force-push یا بازنویسی تاریخچه مجاز/انجام نشده است.
Hash واقعی Commit، نتیجهٔ Push و تطبیق HEAD با origin/main در گزارش نهایی گفتگو اعلام می‌شوند.

## ۱۳. محدودیت‌های فعلی

- کار ناتمام AI عمداً Dirty باقی می‌ماند؛ از این ادغام نمی‌توان پذیرش Production AI را نتیجه گرفت.
- حفظ فایل خصوصی Test به معنی مجازشدن Mock در Production نیست؛ آن محیط فقط true/false را می‌پذیرد.
- هیچ تست واقعی SMS، AI، دستگاه Push، سرور، Publish یا Deploy در این مرحله انجام نشد.
- فایل خصوصی از Clone دریافت نمی‌شود؛ فقط در نبود آن، الگو را کپی و Secret محیط مجاز را امن تأمین کنید.

## ۱۴. نکته‌های آموزشی

۱. سالم‌بودن Backup با وجود پوشه ثابت نمی‌شود؛ Hash و Manifest لازم‌اند.
۲. درخت Commit و فایل کاری یکسان نیستند؛ subset صحیح را باید آگاهانه ثبت کرد.
۳. default timeout با maximum timeout فرق دارد؛ یکی را جای دیگری اعتبارسنجی نکنید.
۴. shape فایل override خصوصی لزوماً قرارداد کامل نیست؛ template و runtime را جدا تست کنید.
۵. Mock فقط اثبات OTP را حذف می‌کند؛ suspension، session، Role و MFA را نباید دور بزند.
۶. Push موفق را با Remote HEAD بررسی کنید، نه صرفاً پیام آماده‌بودن تغییرات.

## ۱۵. مسیر پیشنهادی مطالعه

`AuthController` → `IdentityService` → `OtpChallenge` و `IdentityOptions` →
`OtpAuthenticationOptions` → `DependencyInjection` → `EfIdentityStore` →
`MockOtpIdentityServiceTests` → `ConfigurableOtpAuthenticationTests` →
`ServerMockOtpAuthenticationTests` → تست‌های template/Configuration.

## ۱۶. مرحلهٔ بعد

فقط با درخواست جداگانه، بازبینی کار ناتمام AI و تصمیم نهایی بودجهٔ اجرایی آن انجام شود.
انتشار IIS، تغییر Secret و تشخیص Timeout Provider نباید وارد این مرحلهٔ ادغام شوند.
