# گزارش اصلاح Mock OTP — ۲۰۲۶-۱۰-۲۲

## ۱. علت و خلاصهٔ مسئله

قبل از اصلاح، بین source checkout و نسخهٔ پیاده‌سازی‌شده اختلاف واقعی وجود داشت: ریشهٔ اصلی روی `e23beec` است و commit قابلیت Mock یعنی `37fdd89` را ندارد؛ worktree تمیز روی `c94e7e9` آن را دارد. در `IdentityService` قدیمیِ ریشه، Request بدون شرط provider را فراخوانی و Verify همیشه challenge ذخیره‌شده و hash را مقایسه می‌کند؛ اصلاً branch مربوط به `Otp.Enabled` وجود ندارد. بنابراین publish از ریشهٔ قدیمی فلگ جدید را اجرا نمی‌کند. مسیر کار و انتشار این اصلاح `.artifacts/configurable-otp-20261021` است؛ فایل‌های dirty قبلی AI در ریشه جابه‌جا یا اصلاح نشدند.

دو نقص مستقل کد با تست قرمز بازتولید شد: سه Unit برای پذیرش اشتباه ترکیب true/true در Development/Test/Testing و سه Integration که نشان دادند کد کوتاه یا control character در Mock با HTTP 200 پذیرفته می‌شود. پس از اصلاح، همان تست‌ها باید سبز باشند. این‌ها علت قطعیِ مشاهده‌شده در کد محلی‌اند؛ بدون دسترسی به IIS، علت دقیق خطای گزارش‌شدهٔ سرور را قطعی اعلام نمی‌کنیم.

## ۲. اصلاحات دقیق

`OtpAuthenticationOptions.IsSafe` در محیط تست فقط دو فلگ مخالف را می‌پذیرد؛ false/false و true/true هر دو رد می‌شوند. `IdentityService.VerifyOtpAsync` اکنون فرمت کد را در هر دو حالت بررسی می‌کند، ولی در Mock همچنان challenge، hash، مقدار OTP، expiry و تعداد تلاش OTP را بررسی نمی‌کند. هیچ کد جادویی یا شرطی در Controller اضافه نشد.

## ۳. تنظیمات و تعارض Production

در base، Development و Test مقدار false/true وجود دارد. فایل Production و نمونهٔ آن کلیدهای صریح و همان binding را دارند، اما مقدار قبلیِ امن آن‌ها حفظ شد؛ درخواست تغییر Production توسط کنترل ایمنی اجرا رد شد و گزارش آن به مالک داده شد. هر مقدار false/true در Production/Staging حتی اگر دستی نوشته شود، با محافظ کد fail-closed است. تنظیم مجاز واقعی true/false است. هیچ secret یا config خصوصی سرور تغییر نکرد.

## ۴. تفاوت محیط‌ها

- Development: Mock مجاز با opt-in؛ User Secrets و provider توسعه قابل استفاده‌اند.
- Testing: محیط تست خودکار موجود با محدودیت نرخ افزایش‌یافته؛ پیشنهاد استقرار سرور نیست.
- Test: محیط مستقلِ تأییدشدهٔ مالک برای سرور صرفاً آزمایشی؛ Mock مجاز، نرخ HTTP معمول و key ring پایدار MFA الزامی، providerهای توسعه مجاز نیستند.
- Production/Staging: فقط Real OTP؛ Mock حتی با guard روشن غیرمجاز است.

## ۵. معماری و Dependency flow

Domain تغییر نکرد. Application مالک فرمت ورودی و ادامهٔ Login/Register است؛ Infrastructure مالک Options، محیط، provider و EF است؛ Api مالک startup diagnostics، تنظیمات و پروفایل IIS است. مسیر وابستگی Api → Application → Domain و adapterهای Infrastructure → Application/Domain حفظ شد؛ کتابخانهٔ تازه‌ای اضافه نشد.

## ۶. فایل‌های مهم

- `MakanApp.Application/Identity/IdentityService.cs`: فرمت مشترک Verify؛ تفاوت validation ورودی و اثبات مالکیت OTP.
- `MakanApp.Infrastructure/Identity/OtpAuthenticationOptions.cs`: رد تنظیم متناقض؛ انتخاب حالت باید deterministic باشد.
- `MakanApp.Api/Program.cs`: لاگ امن حالت مؤثر، فقط environment و booleanها؛ نه dump تنظیمات.
- `MakanApp.Api/appsettings.Test.json`: تنظیم غیرمحرمانهٔ Test؛ به‌تنهایی config کامل دیتابیس نیست.
- `MakanApp.Api/Properties/PublishProfiles/TestIis.pubxml`: ساخت بستهٔ Test و حذف فایل خصوصی Production/Development از خروجی؛ web.config اصلی دست‌نخورده است.
- `.gitignore`: فقط استثنای همین پروفایل غیرمحرمانهٔ pubxml؛ سایر پروفایل‌ها همچنان ignored هستند.
- `ProductionLoggingSafety.cs` و `EfCoreLoggingSafety.cs`: بازاستفادهٔ حفاظت لاگ برای محیط Test، با رخداد جایگزین امن.
- `MockOtpIdentityServiceTests.cs`: Unit رفتار واقعی Application با double مرز I/O و رمزنگاری واقعی.
- `ServerMockOtpConfigurationTests.cs`: Unit binding، تناقض، guard و ساختار پروفایل.
- `ServerMockOtpAuthenticationTests.cs`: HTTP واقعی در محیط Test، SQL موقت، session و MFA واقعی؛ بدون جایگزینی IdentityService/Options.
- `OtpConfigurationPrecedenceTests.cs`: رفتار providerهای واقعی Configuration و override محیطی.
- دو فایل `MakanAppWebApplicationFactory`: تست‌های قبلی صریحاً Real=true/false را اعمال و متغیرهای محیطی را بعد از اجرا restore می‌کنند.
- تست‌های ConfigurableOtp و EfCoreLoggingSafety و README/راهنماهای OTP و IIS متناسب با همین تغییر اصلاح شدند.

## ۷. جریان Request OTP

HTTP POST → `AuthController.RequestOtp` → `IdentityService.RequestOtpAsync` → `PhoneNumberNormalizer` → در Mock پاسخ سازگار `RequestOtpResult` بدون SMS/DB/OTP. شمارهٔ جدید و ساختگیِ دارای فرمت معتبر نیاز به حساب قبلی ندارد.

## ۸. جریان Verify OTP

HTTP POST → `AuthController.VerifyOtp` → `IdentityService.VerifyOtpAsync` → normalization شماره و فرمت کد → در Mock عبور از اثبات OTP → `IIdentityStore`/`EfIdentityStore` → transaction Serializable → SQL Server → `VerifyOtpResult` → HTTP 200.

فرمت موجود پس از trim و تبدیل ارقام فارسی/عربی به ASCII، پیش‌فرض ۴ تا ۶۴ کاراکتر و بدون control character است؛ الزام دقیقاً شش رقم اختراع نشد. مقادیر شش‌رقمی دلخواه از جمله صفرها معتبرند؛ کد یک‌رقمی دیگر معتبر نیست.

## ۹. Login/Register و Profile

شمارهٔ جدید همان `User.Create` و `UserCredential.CreateMobile` موجود را طی می‌کند؛ profile تازه کامل نیست. شمارهٔ موجود همان User را resolve می‌کند و حساب تکراری نمی‌سازد. حساب Suspended همچنان 403 می‌گیرد؛ Mock عضویت، نقش یا Enrollment ایجاد نمی‌کند.

## ۱۰. Session و JWT

توکن ورود کاربر opaque Bearer token موجود است، نه JWT؛ session/refresh واقعی ساخته و hash آن‌ها ذخیره می‌شود. Service JWT مربوط به AI مستقل است و تغییر نکرد. تغییر flag بعداً، session قبلی Mock را خودکار revoke نمی‌کند.

## ۱۱. Authorization و MFA

مجوز resource/organization/role موجود تغییر نکرد. ورود Mock سطح Primary دارد؛ حتی حساب PlatformOwner برای Backoffice به MFA واقعی نیاز دارد. GuardianRelation مجوز مشاهدهٔ چت خصوصی ایجاد نمی‌کند؛ هیچ سیاست Messenger تغییر نکرد.

## ۱۲. دیتابیس و Migration

هیچ table، column، PK/FK، index، constraint، rowversion mapping یا Migration جدیدی ایجاد نشد. Request Mock هیچ row نمی‌سازد؛ Verify فقط موجودیت‌های عادی User/Credential/Session/Refresh را طبق مسیر موجود ثبت می‌کند. تست‌ها روی SQL Server/LocalDB جدا اجرا می‌شوند؛ دیتابیس عادی MakanApp و سرور دست‌نخورده‌اند. بررسی EF با startup پروژهٔ Infrastructure به‌درستی «بدون تغییر مدل» داد؛ تلاش نخست با startup Api به علت در دسترس نبودن EF Design در آن host شکست خورده بود، نه به علت migration جدید.

## ۱۳. API و DTO

Route و شکل DTO تغییر نکرد: POST `/api/v1/auth/otp/challenges` با `RequestOtpCommand` → 202 `RequestOtpResult`؛ POST `/api/v1/auth/otp/verify` با `VerifyOtpCommand` → 200 `VerifyOtpResult` برای حساب فعال؛ refresh و me همان قرارداد موجودند. خطاهای مهم: فرمت شماره یا کد 400، suspension/MFA/permission با 403، بدون session معتبر 401 و محدودیت HTTP با 429. پارامتر/header/body جدیدی برای انتخاب Mock وجود ندارد.

## ۱۴. SMS

کد و تنظیمات MeliPayamak تغییر نکرد. در Mock، adapter ناموجود دفاعی ثبت می‌شود و مسیر Request هرگز delivery را فراخوانی نمی‌کند. تست‌ها با provider محلی/کنترل‌شده‌اند، نه پیامک واقعی. در Real همان provider abstraction، salt/hash، expiry و attempt limit اجرا می‌شود.

## ۱۵. محدودیت امنیتی و Trade-off

Mock به معنی امکان ورود به حسابِ شمارهٔ واردشده بدون اثبات مالکیت است؛ فقط داده/حساب تست و دسترسی شبکهٔ محدود مجاز است. هزینهٔ حفظ امنیت، نیاز به محیط جدا و کانفیگ مقصد است؛ guard Production برای راحتی publish حذف نشد. HTTP، token را در شبکه رمزگذاری نمی‌کند. Source config با IIS config یکسان فرض نشده و موفقیت تست محلی، موفقیت عملیاتی سرور نیست.

## ۱۶. Unit Tests

نتیجهٔ متمرکز: ۷۵ تست موفق، صفر شکست/skip. رگرسیون کامل اولیه و final run هر دو: ۸۲۷ Unit موفق، صفر شکست/skip. ۳۴ case تازه نسبت به `c94e7e9`: ۲۱ case در `MockOtpIdentityServiceTests` و ۱۳ case در `ServerMockOtpConfigurationTests`. علاوه بر config و normalization، تست‌ها خودِ IdentityService را اجرا می‌کنند: شمارهٔ جدید، حساب موجود، کدهای مختلف و صفرها، نبود SMS/hash/challenge در Mock، فرمت نامعتبر، suspension و حفظ مقایسه/expiry/attempt در Real.

## ۱۷. Integration Tests

نتیجهٔ متمرکز: ۵۳ تست موفق، صفر شکست/skip. رگرسیون اولیه: ۱۳۲۲ Integration موفق در ۹ دقیقه و ۴۸ ثانیه؛ final run: همان ۱۳۲۲ موفق در ۱۰ دقیقه و ۵۶ ثانیه، صفر شکست/skip. ۱۸ case تازه نسبت به `c94e7e9`: ۱۴ case در `ServerMockOtpAuthenticationTests`، سه case در `OtpConfigurationPrecedenceTests` و یک case امنیت لاگ EF برای Test. API واقعی با SQL Server شامل Request/Verify، refresh/me، حساب موجود، MFA، Production guard، override مؤثر و عدم نشت phone/code/token در لاگ اجرا شد.

## ۱۸. Architecture Tests

اجرای رگرسیون اولیه و نهایی: هر دو ۱۴ تست موفق، صفر شکست/skip؛ جهت وابستگی Onion حفظ شده است. Architecture test تازه‌ای لازم نبود، زیرا جهت وابستگی تغییر نکرد.

## ۱۹. Validation

Restore و final restore موفق؛ Debug build و final build موفق، صفر warning/error. خروجی Release محلی با پروفایل TestIis موفق؛ محیط خروجی Test، flags برابر false/true، Swagger روشن و فایل Production/Development خصوصی خارج از بسته بودند. رگرسیون کامل اول و final test جداگانه، هر دو: ۲۱۶۳ اجرا و موفق، صفر failed/skipped. هیچ تستی حذف، disable یا skip نشد. خطای compile در تلاش اولیهٔ draft قبلی مربوط به JSON configuration extension بدون dependency مناسب بود؛ با parser موجود BCL و بدون افزودن package رفع شد. تست‌های قرمز بازتولید نقص نیز قبل از اصلاح اجرا و پس از اصلاح سبز شدند.

## ۲۰. Git

Commit message این تغییر `fix(identity): validate mock OTP and isolate test deployment` است؛ hash دقیق و نتیجهٔ واقعی push/remote verification پس از ثبت در گزارش نهایی گفتگو اعلام می‌شوند تا self-reference ساختگی در سند ایجاد نشود. این سند پس از موفقیت واقعی build و دو اجرای کامل تست برای commit آماده شد؛ به‌تنهایی مدرک عملیات Git بعدی نیست.

## ۲۱. Branch و Remote

Branch کاری `identity/configurable-otp-20261021` با upstream موجود `origin/main`؛ remote مخزن `https://github.com/RastinaxAI/MakanApp.git`. فقط فایل‌های مرتبط همین اصلاح و ادامهٔ قبلی OTP stage می‌شوند؛ هیچ فایل dirty AI وارد commit نخواهد شد. نتیجهٔ push و remote verification بعد از اجرای واقعی اعلام می‌شود.

## ۲۲. IIS و مشاهدهٔ تنظیم مؤثر

[راهنمای اجرایی Test و ترتیب Configuration](SERVER_MOCK_OTP_FA.md) را بخوانید. در این مرحله هیچ تغییری روی سرور انجام نشده؛ owner باید private config دیتابیس آزمایشی، کلید پایدار، storage و key ring را برای Test تأمین کند. startup event `MockOtpAuthenticationEnabled` فقط محیط و booleanهای مؤثر را ثبت می‌کند و برای تشخیص حالت کافی است. تنظیم environment یا flag نیاز به restart دارد؛ تغییر source باید با publish نسخهٔ درست همراه باشد.

## ۲۳. نکات آموزشی و ترتیب مطالعه

۱. فایل JSON با مقدار مؤثر Configuration برابر نیست. ۲. فرمت ورودی با اثبات OTP تفاوت دارد. ۳. test double مرز I/O را جایگزین کند، نه متد تحت تست. ۴. Mock نباید تست Real را ناخواسته سبز کند. ۵. guard محیطی جای جداسازی شبکه و داده نیست. ۶. log امن باید قابل‌پیگیری بماند بدون نمایش دادهٔ ورود.

مسیر مطالعه: `AuthController` → `IdentityContracts`/`PhoneNumberNormalizer` → `IdentityService` → `OtpAuthenticationOptions` → `DependencyInjection` → `EfIdentityStore` → `Program`/`TestIis` → Unit جدید → API/Configuration tests → تست‌های قبلی Real OTP. Domain و EF configuration موجود صرفاً برای فهم مسیر ذخیره بررسی شوند؛ Migration تازه‌ای وجود ندارد.

## ۲۴. بدهی و موارد عمداً انجام‌نشده

روی IIS سرور اجرا یا بررسی runtime صورت نگرفت؛ secretها منتقل نشدند و MeliPayamak واقعی آزمایش نشد. Root checkout دارای تغییرات AI همچنان قدیمی است و بدون حل آن‌ها fast-forward نمی‌شود؛ برای publish از worktree/checkout تمیزِ اعلام‌شده استفاده کنید. هیچ اصلاح AI، Frontend، MFA موازی یا تغییر schema انجام نشد.

## ۲۵. قدم بعد

پس از موفقیت واقعی Commit/Push، صاحب سرور بستهٔ Test را روی محیط آزمایشی محدود مستقر و health، حالت مؤثر و ورود حساب تست را بررسی کند. تبدیل آن به سرویس واقعی باید مرحله‌ای جدا با OTP واقعی، session revocation برنامه‌ریزی‌شده و کانفیگ امن Production باشد.
