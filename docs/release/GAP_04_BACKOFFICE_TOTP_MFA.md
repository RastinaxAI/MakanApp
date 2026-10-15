# GAP-04 — MFA و step-up واقعی Backoffice

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

تاریخ: ۲۰۲۶-۱۰-۱۵. تصمیم جاری release: TOTP Authenticator عامل اصلی است؛ این تصمیم دائمی یا منع افزودن روش‌های آینده نیست. provider شبکه‌ای، SMS MFA، email OTP، QR باینری، WebAuthn و frontend در این مرحله ساخته نشده‌اند.

## معماری و وضعیت قبل/بعد

قبلاً `IPlatformStepUpAuthorization` در runtime به `UnavailablePlatformStepUpAuthorization` متصل بود؛ فیلدهای `UserSession.AssuranceLevel` و `StepUpAtUtc` وجود داشتند ولی MFA واقعی نبودند. اکنون همان port با `EfPlatformMfaAssurance` پیاده می‌شود؛ `IPlatformMfaAssurance` gate خواندن Backoffice را مستقل از freshness کوتاه‌مدت کنترل می‌کند. کلاس قدیمی fail-closed برای سابقه/تست خود باقی است اما runtime به آن متصل نیست.

- Domain: `PlatformMfaCredential` و `PlatformMfaRecoveryCode`؛ pending/active، counter ضد replay، lockout، مصرف یک‌باره، reset؛ `UserSession.ClearMfaAssurance`.
- Application: `PlatformMfaService`، DTOها و portهای محافظ رمز/تراکنش؛ تصمیم permission/Owner، freshness و خروجی کمینه. `PlatformMfaState` context اختصاصی همین قابلیت است، نه Generic Repository یا EF abstraction عمومی.
- Infrastructure: Otp.NET `1.4.1` و `TotpMfaCryptography`؛ Data Protection abstraction `9.0.5`، EF store/configuration و SQL locking.
- Api: `PlatformMfaController`، اتصال Data Protection در composition root، permission handler، rate limit و ProblemDetails.

جریان ثبت: HTTP → `PlatformMfaController` → `PlatformMfaService` → `IPlatformMfaStore` → `EfPlatformMfaStore` → permission/session جاری SQL → Domain + `IPlatformMfaCryptography` → SQL + `AdminAuditLog` → پاسخ setup/تأیید.

جریان سطح محافظت‌شده: Bearer session → `PlatformPermissionAuthorizationHandler` → permission جاری → `IPlatformMfaAssurance` → Controller موجود. حساسیت عملیات موجود تغییر نکرد؛ همان `IPlatformStepUpAuthorization` در use case حساس freshness را می‌سنجد. Student/Parent/Teacher/Manager معمولی همچنان در `/api/v1` به MFA نیاز ندارند؛ Manager سازمان Platform Admin نیست.

## TOTP و enrollment

TOTP استاندارد SHA-1، شش رقم ASCII، گام ۳۰ ثانیه و پنجره دقیقاً ±۱ گام است. محاسبات اختصاصی ساخته نشده‌اند؛ Otp.NET تولید/verify را انجام می‌دهد و counter پذیرفته‌شده در SQL نگه‌داری می‌شود. یک counter برابر/قدیمی‌تر دوباره پذیرفته نمی‌شود؛ این guard میان نشست‌های همان کاربر نیز مشترک است. [کتابخانه](https://github.com/kspearrin/Otp.NET)، [RFC 6238](https://www.rfc-editor.org/rfc/rfc6238.html).

۱. کاربر با login معمول وارد می‌شود و باید Platform access جاری داشته باشد.
۲. start یک secret تصادفی ۱۶۰ بیتی می‌سازد؛ فقط protected value در SQL می‌رود. setup URI صرفاً در همان پاسخ start برمی‌گردد؛ GET وضعیت آن را دوباره نمایش نمی‌دهد.
۳. enrollment ID به User و session همان درخواست و expiry متصل است؛ start مجدد pending قبلی را جایگزین می‌کند، نه عامل فعال را. rate/lockout با start مجدد پاک نمی‌شوند.
۴. confirmation باید کد درست، enrollment ID، session و مهلت معتبر داشته باشد. تنها در این مرحله active می‌شود؛ assuranceهای قبلی پاک، نشست تأییدکننده MFA-assured و ده recovery code جدید یک‌بار نمایش داده می‌شوند.
۵. عامل فعال را نمی‌توان با یک نشست Primary تعویض کرد. re-enroll و تأیید آن به freshness معتبر نیاز دارند؛ عامل فعال قبلی تا تأیید عامل جدید معتبر باقی می‌ماند. confirmation خودش freshness ایجاد می‌کند؛ برای verify دوباره در نشست دیگر، کد مصرف‌شده همان گام زمانی قابل استفاده نیست و باید منتظر گام بعدی بود.

اگر پاسخ confirmation دارای recovery code گم شد، سرور آن کدها را دوباره نمایش نمی‌دهد؛ پس از احراز مجدد معتبر، regenerate کنید. کدهای قدیمی همگی نامعتبر می‌شوند. هیچ GET برای secret یا recovery plaintext وجود ندارد.

## assurance، reset و recovery

از فیلدهای موجود session استفاده می‌شود؛ MFA و freshness سمت SQL هستند، نه claim/selector کلاینت. MFA برای عمر session فعال معتبر می‌ماند، اما step-up تنها تا `StepUpAtUtc + StepUpLifetimeMinutes` و مشروط به session/account/credential معتبر و permission جاری قابل استفاده است. response زمان step-up جای مجوز جاری سرور نیست. refresh همان session، freshness جدید نمی‌سازد؛ نشست دیگر assurance خودکار نمی‌گیرد.

ده recovery code، هر کدام ۱۶۰ بیت تصادفی و نمایش hex، فقط هنگام confirm یا regenerate نمایش داده می‌شوند. SQL تنها SHA-256 با purpose و UserId را نگه می‌دارد؛ مقایسه ثابت‌زمان است. مصرف یک کد به همان نشست assurance/freshness می‌دهد و به‌صورت اتمیک با consumed marker و audit ثبت می‌شود. کد مصرف‌شده/نامعتبر خطای عمومی یکسان دارد.

reset روی کاربر پلتفرمی دیگر، فقط برای `PlatformOwner` دارای `platform.roles.manage` و عامل فعال/step-up تازه است؛ Reason لازم است. reset عامل و pending را غیرفعال، recoveryها را حذف و MFA assurance تمام sessionهای target را پاک می‌کند. account، Credential هویت، session token، Membership و نقش پلتفرمی target حذف نمی‌شوند؛ login معمول همچنان مستقل است و target باید دوباره enroll کند. آخرین Owner role از این مسیر حذف/تعلیق نمی‌شود؛ reset خود با این API ممنوع است. از دست رفتن TOTP و همه recovery codeهای آخرین Owner بدون نشست معتبر، `DEFERRED_OPERATIONAL_DECISION` است؛ bypass یا API پنهان وجود ندارد.

## SQL و migration

Migration: `20261015073418_AddPlatformTotpMfa`؛ migration شماره ۳۶. مدل session قبلی ستون تازه نمی‌گیرد.

| جدول | بخش‌های مهم |
|---|---|
| `platform.PlatformMfaCredentials` | PK/FK `UserId`، protected active/pending secret با `nvarchar(2048)`، active timestamp، enrollment ID/session/expiry، last counter، failure count/lockout و rowversion |
| `platform.PlatformMfaRecoveryCodes` | PK `Id`، FK `UserId` به credential، `CodeHash varchar(64)`، created/consumed timestamp، rowversion و unique index روی UserId+Hash |

CHECKهای active/pending مانع حالت ناقص هستند؛ attempts/counter غیرمنفی‌اند. FK مرکب enrollment session + UserId مانع اتصال pending به نشست کاربر دیگر است. FK User و session از نوع Restrict و recovery متعلق به aggregate از نوع Cascade است. رمز plaintext/OTP/recovery plaintext در هیچ ستون ذخیره نمی‌شود.

`EfPlatformMfaStore` تراکنش Serializable و `sp_getapplock` اختصاصی UserId با ترتیب ثابت actor/subject می‌گیرد؛ مصرف کد، counter، assurance و audit با هم commit می‌شوند. تلاش اشتباه و failure counter نیز پیش از پاسخ خطا commit می‌شوند؛ rollback خطا نباید brute-force counter را پاک کند. deadlock/rowversion conflict پاسخ 409 قابل retry امن می‌دهد. عملیات شبکه داخل transaction نیست؛ verify کاملاً محلی است. migration فقط در SQL ایزوله تست شده و به DB معمول/Production اعمال نشده است؛ startup migration نداریم.

## API قرارداد جاری

همه زیر `/api/admin/v1/mfa`، Bearer و Platform access جاری؛ این endpointهای محدود از gate قبلی MFA معاف‌اند تا enrollment/verify قابل انجام باشد، نه اینکه permission را bypass کنند. actor/session از principal سرور می‌آید؛ درخواست UserId برای self-operation ندارد. پاسخ‌ها no-store هستند.

| Method/Route | Request | پاسخ/قاعده |
|---|---|---|
| `GET /api/admin/v1/mfa` | ندارد | Enrolled، EnrollmentPending، SessionMfaVerified و StepUpValidUntilUtc؛ بدون setup/code |
| `POST /api/admin/v1/mfa/enrollment` | ندارد | وضعیت + Enrollment شامل EnrollmentId، SetupUri و ExpiresAtUtc؛ URI فقط این پاسخ |
| `POST /api/admin/v1/mfa/enrollment/confirm` | EnrollmentId، Code | پس از اثبات معتبر، وضعیت + ده recovery code یک‌بار |
| `POST /api/admin/v1/mfa/verify` | Code | TOTP درست و counter استفاده‌نشده → assurance/freshness همان session |
| `POST /api/admin/v1/mfa/recovery/verify` | Code | recovery معتبر و مصرف‌نشده → assurance همان session |
| `POST /api/admin/v1/mfa/recovery-codes/regenerate` | ندارد | MFA فعال و freshness لازم؛ ده code جدید و invalidation همه قبلی‌ها |
| `POST /api/admin/v1/mfa/users/{userId}/reset` | Reason | Owner مجاز + fresh؛ target پلتفرمی دیگر؛ بدون برگرداندن داده امنیتی او |

موفقیت همه عملیات فوق 200 است. OpenAPI از همین Controller/DTO و ProblemDetails statusها تولید می‌شود؛ schema EF entity به API داده نشده است.

نام memberها در جدول برای تطبیق C# آمده است؛ نام واقعی JSON برابر camelCase مانند `enrolled`، `enrollmentPending`، `sessionMfaVerified`، `stepUpValidUntilUtc`، `enrollmentId`، `setupUri`، `code` و `reason` است.

| code/status | معنا و رفتار client |
|---|---|
| `401` | login/session معتبر لازم است |
| `PLATFORM_ACCESS_DENIED / 403` | permission پلتفرم کافی نیست؛ اطلاعات MFA شخص دیگر افشا نشود |
| `MFA_REQUIRED / 403` | نقش/permission جاری وجود دارد ولی نشست MFA-assured نیست؛ status و enrollment/verify را اجرا کنید |
| `PLATFORM_STEP_UP_REQUIRED / 403` | کد موجود پروژه حفظ شده؛ معادل semantic «STEP_UP_REQUIRED»، verify مجدد و سپس retry mutation |
| `MFA_INVALID / 400` | کد، replay، enrollment/session/expiry نامعتبر؛ دلیل حساس تفکیک نمی‌شود |
| `MFA_RATE_LIMITED / 429` | شمار تلاش محدود؛ از تکرار فوری خودداری شود |
| `MFA_UNAVAILABLE / 503` | محافظ/key ring در دسترس نیست؛ bypass نکنید |
| `PLATFORM_ASSIGNMENT_CONFLICT / 409` | تضاد هم‌زمانی، retry با وضعیت جاری |
| `PLATFORM_REASON_REQUIRED / 400` | Reason معتبر برای reset لازم است |

URI، secret و recovery codes را در localStorage پایدار، URL وب، telemetry، console، screenshot مشترک یا log نگذارید. backend QR باینری تولید نمی‌کند؛ render احتمالی QR در frontend خارج این task است. خطاهای ModelState عمومی validation نیز ممکن‌اند.

## محدودیت تلاش و audit

policy API به نام `platform-mfa` با ۱۰ درخواست در دقیقه برای actor جاری (Testing سقف بزرگ‌تر قرارداد موجود) و limiter عمومی admin mutation ترکیب می‌شود. علاوه بر process-local limiter، SQL برای verify/recovery/confirm پنج تلاش ناموفق و lockout پنج دقیقه‌ای مشترک همان حساب دارد. restart یا تعویض session این counter را پاک نمی‌کند. start enrollment آن را reset نمی‌کند. برای چند instance، guard SQL مشترک است ولی limiter HTTP همچنان process-local است؛ rate limiter توزیع‌شده به این gap اضافه نشده است.

رویدادهای audit: `platform.mfa.enrollment.started`، `platform.mfa.enrolled`، `platform.mfa.verified`، `platform.mfa.recovery.used`، `platform.mfa.recovery.regenerated`، `platform.mfa.reset` و رویدادهای denied. Reason/reset، actor/session واقعی، subject و UTC/correlation ثبت می‌شوند؛ proof، secret، URI، hash و protected value در audit summary وجود ندارند. تلاش‌های ردشده پیش از authorization/schema validation الزاماً audit تجاری ندارند؛ ادعای audit همه requestها نمی‌شود.

## تنظیمات مرکزی

| نام | پیش‌فرض/حد |
|---|---|
| `BackofficeSecurity:StepUpLifetimeMinutes` | ۱۰؛ ۱ تا ۳۰ |
| `BackofficeSecurity:EnrollmentLifetimeMinutes` | ۱۰؛ ۱ تا ۳۰ |
| `BackofficeSecurity:MaximumFailedAttempts` | ۵؛ ۱ تا ۱۰ |
| `BackofficeSecurity:LockoutMinutes` | ۵؛ ۱ تا ۶۰ |
| `BackofficeSecurity:KeyRingPath` | در محیط عملیاتی از بیرون تنظیم شود؛ مسیر مطلق پایدار خارج artifact و StorageRoot |
| `BackofficeSecurity:CertificatePath` | اختیاری روی Windows؛ برای encrypt-at-rest قابل انتقال یا محیط غیر Windows لازم |
| `BackofficeSecurity:CertificatePassword` | فقط secret store/environment؛ هیچ مقدار نمونه واقعی در Git نیست |

برای environment variable از `__` به‌جای `:` استفاده کنید. تنظیم نامعتبر از حدود بالا رد می‌شود. هیچ مقدار secret یا تغییر ملی‌پیامک در appsettings این مرحله ایجاد نشده است.

## Data Protection در Production / IIS / container

`BackofficeMfaConfiguration` از application discriminator ثابت `MakanApp.BackofficeMfa.v1` و purpose اختصاصی + UserId استفاده می‌کند؛ جابه‌جایی ciphertext میان کاربران معتبر نیست. در Development/Testing از provider پیش‌فرض framework می‌توان استفاده کرد؛ این مجوز استفاده از key موقت در Production نیست.

در Staging/Production بدون `BackofficeSecurity__KeyRingPath`، برنامه معمول آموزشی همچنان بالا می‌آید اما ثبت/TOTP با `MFA_UNAVAILABLE` بسته است؛ provider موقت برای TOTP انتخاب نمی‌شود. recovery code از hash مستقل verify می‌شود و در صورت وجود credential/code معتبر به secret TOTP وابسته نیست؛ این عامل مستقل، bypass نیست.

Windows/IIS:

1. operator یک directory پایدار، خارج publish/web root و storage فایل ایجاد و ACL آن را فقط به identity اختصاصی برنامه و اپراتورهای کلید محدود کند.
2. `BackofficeSecurity__KeyRingPath` را در configuration امن همان محیط تنظیم کند؛ فولدر و keyها وارد publish/Git نشوند.
3. بدون CertificatePath، کلیدها با DPAPI current-user رمز می‌شوند؛ App Pool identity ثابت و profile قابل دسترسی، از جمله Load User Profile مناسب، لازم است. DPAPI به همان Windows identity/machine وابسته است؛ کپی XML به VPS تازه به‌تنهایی recovery کلید را تضمین نمی‌کند.
4. برای انتقال میان سرورها/ماشین‌ها، PFX دارای private key و password از secret store و نگه‌داری مستقل certificate/key ring لازم است. path/password واقعی commit نشوند؛ rotation باید توان بازکردن کلیدهای قدیمی را حفظ کند و تغییر کور certificate مجاز نیست.
5. restart، recycle و side-by-side deployment نباید root/discriminator/identity یا keyهای قدیمی را از دست بدهند. تست‌های این مرحله recreation provider و داده محافظت‌شده را بررسی می‌کنند؛ استقرار واقعی IIS ادعا نشده است.

Linux/container: root باید volume پایدار باشد و CertificatePath/private key قابل دسترسی امن داشته باشد؛ مسیر certificate نیز secret mount مستقل باشد، نه image. بدون محافظ معتبر و root پایدار عملیات TOTP fail-closed است. هماهنگی volume/ACL/secret injection، time synchronization و rotation کار اپراتور است، نه ساخت Docker/CI در GAP-04.

key ring و certificate اکنون دارایی recovery جدیدند؛ نگه‌داری و بازیابی مستقل آن‌ها باید پیش از rollout به برنامه عملیاتی مالک افزوده شود. ابزار/سند GAP-03 در این task تغییر نکرده است و DB backup به‌تنهایی secretهای TOTP را قابل decrypt نمی‌کند. [مستند Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview)، [محافظت کلید](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest).

## تست و مسیر مطالعه

`PlatformMfaTests` قواعد pending/session/expiry، replay، lockout، recovery lifecycle، بردار عمومی RFC، محدودیت skew و User-bound protection را بدون فایل/شبکه می‌سنجد. `PlatformMfaProtectionConfigurationTests` persistence واقعی key ring، حفاظت certificate و recreation در configuration عملیاتی را می‌سنجد. `PlatformMfaEndpointsTests` با EF SQL Server و port واقعی MFA/step-up (نه fake) enrollment/confirmation، دسترسی، freshness، recovery یک‌باره هم‌زمان، reset چند نشست، permission جاری، نقش‌های مدرسه و log-safe output را اجرا می‌کند. TimeProvider کنترل می‌شود؛ Thread.Sleep یا تماس provider وجود ندارد.

تست‌های قدیمی IAM عامل امنیتی را به‌صورت صریح در Testing شبیه‌سازی می‌کنند تا قواعد IAM جداگانه تست شوند؛ suite جدید هر دو port را با implementation واقعی جایگزین می‌کند. runtime هیچ تنظیم bypass/fake MFA ندارد.

شاهد validation کامل: restore موفق؛ build با صفر warning/error؛ Unit=392، Integration=644، Architecture=14؛ مجموع 1050 passed و صفر failed/skipped، در مقایسه با baseline برابر 1023. ۲۷ مورد جدید شامل ۸ Unit، ۱۵ SQL/API و ۴ integration محافظ/key ring است. EF pending-model check موفق بود. این اعداد مربوط به worktree شامل ۱۱ فایل قبلی AI است؛ آن فایل‌ها تغییر نکرده و جزو commit این مرحله نیستند. migration روی DB عادی/Production اجرا نشد و استقرار/اسکن دستی authenticator روی گوشی ادعا نمی‌شود.

مسیر مطالعه: `PlatformMfaController` → `PlatformMfaService`/contracts → `PlatformMfaCredential` و `UserSession` → `EfPlatformMfaStore`/`EfPlatformMfaAssurance` → `TotpMfaCryptography`/`BackofficeMfaConfiguration` → EF configuration → migration → دو suite تست.

محدودیت: TOTP phishing-resistant نیست؛ افزودن WebAuthn/SSO تصمیم آینده است، نه نشانه موقت/نامعتبر بودن انتخاب مصوب. last-owner offline recovery هنوز تصمیم عملیاتی باز است؛ MFA عمومی کاربران مدرسه، providerهای آینده و بستن GAP-05 در این مرحله انجام نشده‌اند.
