# گزارش تغییر شماره‌های کاربران Seed به قالب ایران

## ۱. خلاصهٔ مرحله

شماره‌های ۱۱ نقش در fixture از نمونهٔ قبلی به `09120000101` تا `09120000111` تغییر کردند.
SQL مقدار canonical یعنی `+989120000101` تا `+989120000111` را نگه می‌دارد.
در ادامه، مالک تغییر شمارهٔ همان ۱۱ حساب تست موجود در دیتابیس محلی را هم صریحاً تأیید کرد.
برای آن Update محدود و مستقل ساخته شد؛ ایجاد User تازه یا تغییر شمارهٔ کاربران واقعی در محدوده نیست.

## ۲. معماری

Domain، Application، Infrastructure و Api تغییری ندارند. `PhoneNumberNormalizer` موجود در Application
فرمت محلی و ارقام فارسی را به همان شناسهٔ canonical تبدیل می‌کند. جهت وابستگی Onion و مسیر DI حفظ شدند.

## ۳. فایل‌های مهم ایجادشده

همین `docs/dev/IRANIAN_SEED_PHONES_REPORT_FA.md` نتیجه و مرز تغییر را ثبت می‌کند؛
نکتهٔ مهم، جداکردن آماده‌بودن فایل از اجرای واقعی Seed است.
`docs/dev/update-development-test-user-phones-to-iranian.sql` فقط Credentialهای شناخته‌شدهٔ fixture را
با تأییدهای صریح connection، بررسی مالکیت و نقش، قفل و transaction به‌روزرسانی می‌کند؛
این ابزار جای workflow تغییر شمارهٔ کاربران واقعی نیست.

## ۴. فایل‌های مهم تغییرکرده

- `docs/dev/create-development-test-users.sql`: فقط شماره‌های جدول موقت Seed عوض شدند؛ شناسه‌ها و نقش‌ها ثابت‌اند.
- `docs/dev/cleanup-development-test-users.sql`: تولید شماره با Seed هم‌راستا شد تا هدف پاک‌سازی اشتباه نباشد.
- `docs/dev/DEVELOPMENT_TEST_USERS_FA.md`: جدول و نمونهٔ شماره‌ها و هشدار عدم ارسال SMS اصلاح شدند.
- `tests/MakanApp.UnitTests/Identity/PhoneNumberNormalizerTests.cs`: پوشش normalization هر ۱۱ شماره.
- `tests/MakanApp.IntegrationTests/DevelopmentTestUsersTests.cs`: بررسی Credentialهای SQL، ورود Mock با سه فرمت
  و محافظ‌های Update حساب موجود، حفظ metadata/نقش‌ها، idempotency، collision و concurrency.

فقط تغییر شماره در نسخهٔ ناتمام قدیمی Seed و راهنمایش نیز منعکس شد؛ اصلاحات قبلی آن Worktree
و تست‌های قبلی ناتمام، وارد Commit این مرحله نمی‌شوند و نهایی‌شده محسوب نمی‌شوند.

## ۵. جریان اجرای قابلیت

در تست جدید: HTTP Verify → `AuthController` → `IdentityService.VerifyOtpAsync` →
`PhoneNumberNormalizer` → مسیر Mock موجود → portهای Identity و persistence موجود → SQL Server ایزوله →
همان User از قبل Seedشده → session/token عادی → HTTP 200.
کلاس اجرایی جدیدی ساخته نشده است و دیتابیس اصلی در این مرحله Seed نمی‌شود.
مسیر عملیات تأییدشدهٔ حساب موجود: Backup → سه تأیید connection → transaction و
`sp_getapplock` → بررسی User/Person/Credential/Role → UPDATE فقط شماره → COMMIT → بررسی تعداد و ثبات داده‌های دیگر.

## ۶. دیتابیس

تعریف هیچ Table، Column، PK/FK، Index، Constraint یا rowversion تغییر نکرده و Migration جدید لازم نیست.
شماره‌های جدید در fixture مربوط به `identity.UserCredentials.NormalizedIdentifier` هستند.
guard مقصد، تأیید connection، transaction، قفل Seed و برخوردسنجی موجود تغییر نکردند.
کاربر دارای شناسهٔ fixture و شمارهٔ قدیمی، خودکار بازنویسی یا merge نمی‌شود.
فقط با Update جداگانهٔ مصوب، `NormalizedIdentifier` همان ۱۱ Credential تغییر می‌کند؛ SQL مقدار rowversion
همان ردیف‌های تغییرکرده را خودکار جلو می‌برد. VerifiedAtUtc/CreatedAtUtc، UserId، نقش‌ها و sessionها حفظ می‌شوند.
خواندن و بررسی مقادیر قبلی/مقصد داخل همان transaction با `UPDLOCK/HOLDLOCK` انجام می‌شود؛
اجرای هم‌زمان نیز با قفل مشترک با Seed/Cleanup serialize می‌شود. retry روی مقدار ایرانی UPDATE ندارد.

## ۷. Endpointها

API جدید یا تغییر Contract نداریم. تست جدید `POST /api/v1/auth/otp/verify` را با
`VerifyOtpCommand` اجرا می‌کند: شمارهٔ محلی/بین‌المللی/فارسی، شناسهٔ challenge خالی و متن آزمایشیِ کد.
در Mock مجاز نتیجه 200 و همان `VerifyOtpResult` است؛ بررسی‌های account/session/authorization قبلی حفظ می‌شوند.
این تست، ورود Production بدون OTP یا تغییر سیاست MFA را مجاز نمی‌کند.

## ۸. مفاهیم فنی استفاده‌شده

Normalization یعنی شکل‌های مختلف یک شماره، یک شناسهٔ یکسان تولید کنند؛ نه اینکه چند User ساخته شود.
Fixture دادهٔ کنترل‌شدهٔ تست است؛ idempotency مانع ایجاد دوبارهٔ کاربران و رابطه‌ها می‌شود.
Unavailable provider در تست جدید اثبات می‌کند موفقیت ورود به ارسال SMS وابسته نبوده است.
تأیید مستقل `DevelopmentSeedPhoneUpdateApproved` مانع کافی‌بودن تأیید عمومی Seed برای تغییر Credential می‌شود.
rowversion و snapshot ردیف‌ها در تست اثبات می‌کنند داده‌های حفاظت‌شده یا retry بی‌دلیل بازنویسی نشده‌اند.

## ۹. تصمیم طراحی

به جای تغییر validator یا قرارداد API، دادهٔ fixture با قالب پشتیبانی‌شدهٔ موجود عوض شد.
شمارهٔ محلی برای ورود و canonical برای SQL انتخاب شدند. بدون مالک بودن این شماره‌ها تضمین نشده است؛
برخلاف بازهٔ نمونهٔ قبلی، فقط استفادهٔ Mock و بدون ارسال واقعی پیامک مناسب است.
به جای تغییر رفتار idempotent Seed یا ساخت Migration دادهٔ تست، Update مستقل انتخاب شد؛
مزیت آن محدوده و تأیید روشن است و هزینه‌اش یک ابزار دستی مخصوص fixture، نه API عمومی، است.

## ۱۰. تست‌ها

- ۱۱ حالت Unit در `DevelopmentSeedIranianPhones_NormalizeToTheirCanonicalIdentifiers`: local و canonical هر نقش برابرند.
- ۳ حالت Integration در `IranianSeedPhone_InMockMode_ResolvesExistingAccountWithoutSms`: ورودی محلی، canonical و فارسی،
  همان User را برمی‌گردانند؛ تعداد User همچنان ۱۱، جدول OTP خالی و فراخوانی provider صفر است.
- تست `Seed_IsIdempotent_AndCreatesRequiredRelationships` تقویت شد تا Credential هر ۱۱ User را با شمارهٔ مورد انتظار مقایسه کند.
- تست‌های قبلی تمام نقش‌ها، MFA، collision، cleanup و guard با همین شماره‌های جدید اجرا می‌شوند.
- ۷ حالت Integration برای `IranianPhoneUpdate_*`: سه تأیید مستقل، حفظ حساب غیرfixture/نقش/metadata و
  idempotency، collision با حساب دیگر، شمارهٔ فعلی نامنتظر و اجرای هم‌زمان. دادهٔ عادی تست در سناریوی حفظ
  حساب دیگر ساخته می‌شود؛ هیچ تستی برای این کار به دیتابیس اصلی وصل نمی‌شود.
- تست Architecture تازه لازم نبود؛ تست‌های موجود در regression اجرا می‌شوند.

## ۱۱. Validation

- Restore موفق و Build اولیه/نهایی با صفر Warning و صفر Error موفق شدند.
- تست متمرکز: ۱۹ Unit و ۲۴ Integration، همگی موفق و بدون Skip.
- Regression کامل: ۷۹۳ Unit، ۱۲۹۷ Integration و ۱۴ Architecture؛ مجموع ۲۱۰۴ موفق، صفر Failed/Skipped.
- اجرای نهاییِ مرحلهٔ صرفاً فایل برای Integration خروجی کامل قابل‌تأیید نداشت؛ موفق شمرده نشد.
- پس از تأیید تغییر حساب موجود، SQL و ۷ حالت تست اضافه و Restore/Build دوباره با صفر Warning/Error موفق شدند.
- هر ۳۱ تست متمرکز Integration نسخهٔ جدید موفق شدند.
- Build نهایی نسخهٔ شامل Update با صفر Warning و صفر Error موفق شد.
- اجرای کامل نهایی: ۷۹۳ Unit، ۱۳۰۴ Integration و ۱۴ Architecture؛ مجموع ۲۱۱۱ اجرا/موفق، صفر Failed و صفر Skipped.
  زمان Integration نهایی ۱۱ دقیقه و ۹ ثانیه بود؛ فایل‌های TRX در TestResults هر پروژه ثبت شدند و جزو Commit نیستند.
- EF pending model changes: هیچ تغییری نسبت به آخرین Migration وجود ندارد؛ Migration اجرا یا تولید نشد.
- بررسی فقط‌خواندنی دیتابیس محلی در این مرحله: ۱۲ User شامل ۱۱ fixture موجود با شماره‌های قبلی؛
  ایجاد این حساب‌ها توسط این مرحله انجام نشده است. پس از تأیید صریح مالک، دقیقاً ۱۱ Credential به شمارهٔ ایرانی تغییر کردند؛
  تعداد User بعد از عملیات همچنان ۱۲ و تعداد fixture دارای شمارهٔ ایرانی متناظر، ۱۱ است.
- Backup از نوع COPY_ONLY با CHECKSUM و سپس RESTORE VERIFYONLY موفق بود؛ فایل در پوشهٔ پیش‌فرض Backup SQL Server:
  `MakanApp_before_iranian_fixture_phones_20261022_9510580656ce4ba483b4a757edabe13d.bak`.
- مقایسهٔ SHA-256 هفده snapshot قبل/بعد، بدون ثبت دادهٔ خصوصی: تمام Userها و Personها، Credentialهای خارج fixture،
  metadata زمان/مالکیت Credentialهای fixture، Membership/Roleها، رابطه‌های آموزشی/Guardian، session/refresh/OTP
  و Migration history بدون تغییر بودند. فقط شماره و rowversion همان ۱۱ Credential تغییر کردند.

## ۱۲. Git

پیام مورد نظر: `fix(identity): use Iranian phones for development role fixtures`.
شاخهٔ اجرای تغییر: `identity/configurable-otp-20261021` با upstream موجود `origin/main`.
فقط پنج فایل تغییرکرده، SQL جدید Update و این گزارش در Commit هستند؛ dirtyهای AI و اصلاحات قبلی Seed خارج می‌مانند.
Hash و نتیجهٔ واقعی Push/remote verification در گزارش نهایی گفتگو اعلام می‌شوند.

## ۱۳. محدودیت‌های فعلی

هیچ User جدید یا SMS واقعی ایجاد/ارسال نشد؛ شمارهٔ ۱۱ fixture موجود روی `Server=.;Database=MakanApp` طبق تأیید تغییر کرد.
اسکریپت ایجاد اولیهٔ Commitشده هنوز guard مقصد قدیمی را دارد؛ اصلاح سازگاری Seed با دیتابیس اصلی در Worktree قبلی ناتمام است.
تغییر شماره‌ها آن اصلاحات را تأیید یا تمام نمی‌کند. هیچ دیتابیس دستی/دائمی تازه‌ای ساخته نمی‌شود؛
تست Integration از دیتابیس موقت ایزولهٔ خود استفاده می‌کند و به دیتابیس اصلی متصل نمی‌شود.

## ۱۴. نکات آموزشی

۱. شمارهٔ نمایشی و شناسهٔ canonical باید به یک Credential برسند.
۲. تغییر دادهٔ fixture باید در Cleanup و تست‌های رابطه‌ها نیز منعکس شود.
۳. آزمون ورود باید UserId و تعداد User را هم بسنجد، نه فقط HTTP 200.
۴. موفقیت Mock اثبات ارسال SMS نیست و نباید به عنوان آن گزارش شود.
۵. Seed، Migration و ایجاد واقعی کاربران سه کار متفاوت‌اند.
۶. تغییر Credentialهای موجود باید ابزار و تأیید مستقل داشته باشد؛ Seed نباید بی‌صدا آن‌ها را بازنویسی کند.

## ۱۵. مسیر مطالعه

جدول راهنمای کاربران → `create-development-test-users.sql` → `PhoneNumberNormalizer` →
`PhoneNumberNormalizerTests` → `update-development-test-user-phones-to-iranian.sql` →
`DevelopmentTestUsersTests` → `cleanup-development-test-users.sql`.
EF Configuration و Migration جدیدی برای مطالعه در این تغییر وجود ندارد.

## ۱۶. مرحلهٔ بعد

مرحلهٔ بعد می‌تواند تست دستی workspace هر نقش در Mock مجاز باشد؛ توکن یا کد را در گزارش/Log ذخیره نکنید.
اگر ایجاد fixture در دیتابیس دیگری لازم شد، اصلاحات ناتمام guard/سازگاری Seed جداگانه نهایی شوند.
تغییر OTP Production، MFA، MeliPayamak و AI نباید با این تغییر دادهٔ تست مخلوط شود.
