# حذف Platform MFA و Step-Up — تصمیم مالک ۲۰۲۶-۱۰-۲۴

## ۱. خلاصه و مرجع تصمیم

MFA جزو نیاز بیزینسی نیست. قابلیت Authenticator/TOTP، enrollment، verify، Recovery Code، reset و Step-Up از کد فعال حذف شده است؛ هیچ فلگ bypass جای آن ساخته نشده است.
قواعد MFA در اسناد تاریخی GAP-04/PH-1 یا PDFهای قدیمی دیگر قرارداد جاری نیستند. سایر قواعد امنیت، Child Safety و مرز Platform/School تغییری نکرده‌اند.

قبلاً حتی Owner با OTP و نشست معتبر به عامل دوم نیاز داشت. اکنون همان OTP موجود، Session معتبر، Platform role و permission واقعی مسیر ورود را تعیین می‌کنند.

## ۲. معماری

Onion، Modular Monolith، .NET 9 و EF Core 9/SQL Server حفظ شده‌اند:

- Domain: حالت عامل دوم از `UserSession` و Entityهای صرفاً MFA حذف شد؛ عمر، revocation، token hash، workspace و rowversion نشست باقی‌اند.
- Application: قرارداد/سرویس MFA و portِ Step-Up حذف شد؛ IAM، عملیات سازمان و Commerce همچنان validation، Reason و فراخوانی store مجاز دارند.
- Infrastructure: store/cryptography/assurance صرفاً MFA و رجیستریشن آن‌ها حذف شدند. در سن ارتباطی و خواندن شاهد، بررسی جاری User/Session داخل تراکنش مستقل از MFA حفظ شد.
- Api: Controller عامل دوم، policy محدودکنندهٔ مخصوص آن و تنظیم/پایش key ring حذف شدند؛ Authentication و permission handler اصلی برقرارند.

وابستگی‌ها همان `Api → Application/Infrastructure → Domain` هستند؛ هیچ وابستگی داخلی به EF/HTTP اضافه نشده است.

## ۳. فایل‌های مهم جدید

| مسیر | مسئولیت و نکتهٔ آموزشی |
|---|---|
| `MakanApp.Infrastructure/Persistence/Migrations/20261024184613_RemovePlatformMfa.cs` | حذف نسخه‌بندی‌شدهٔ schema؛ حذف Feature نباید تاریخچهٔ Migration را پاک کند |
| Designer همان Migration و `MakanDbContextModelSnapshot.cs` | مدل هدف ۱۰۵ جدول؛ منبع تشخیص pending model، نه گزارش schema سرور |
| `tests/MakanApp.IntegrationTests/PlatformAuthorizationRegressionTests.cs` | Owner واقعی با OTP، چهار نقش مدرسه، permission/reason/audit، نشست نامعتبر و نبود Route/OpenAPI عامل دوم |
| چهار فایل `PlatformAuthorizationChildSafetyTests/PersonalMessagingTests/PersonalDirectoryTests/PersonalRealtimeTests.cs` | تست‌های چندمنظورهٔ قبلی حفظ و fixture مشترک بدون عامل دوم بازنویسی شد؛ coverage Messaging حذف نشده است |
| `tests/MakanApp.UnitTests/Platform/PlatformSchemaRemovalTests.cs` | metadata مدل و فهرست دقیق عملیات Up/Down؛ بدون اتصال SQL، پس جای Integration واقعی را نمی‌گیرد |

## ۴. فایل‌های مهم تغییرکرده و حذف‌شده

`PlatformPermissionAuthorizationHandler` فقط actor احرازشده و permission واقعی را می‌سنجد.
`PlatformIamService`، `PlatformOperationsService` و `PlatformCommerceOperationsService` دیگر `EnsureStepUpAsync` ندارند؛ قواعد storeها، آخرین Owner/Manager، state، Reason و scope تغییر نکرده‌اند.
`EfCommunicationAgeStore` و `EfPlatformEducationSafetyStore.ChildSafety` همچنان وضعیت جاری permission و Session را پیش از اثر حساس بررسی می‌کنند؛ Role به‌تنهایی کافی نیست.
`CoreReadinessHealthCheck` و `PlatformSystemOperationsController` دیگر وضعیت key ring حذف‌شده را gate/گزارش نمی‌کنند؛ SQL/Storage و سایر healthها باقی‌اند.

حذف‌های صرفاً Feature:

- Api: `PlatformMfaController`، `BackofficeMfaConfiguration`، `DataProtectionKeyRingSettings`، `DataProtectionKeyRingStartupValidator` و `DataProtectionKeyRingProbe`.
- Application: `PlatformMfaContracts`، `PlatformMfaService` و `IPlatformStepUpAuthorization`.
- Domain: `PlatformMfaCredential`/Recovery و `AuthenticationAssuranceLevel`.
- Infrastructure: `EfPlatformMfaAssurance`، `EfPlatformMfaStore`، `TotpMfaCryptography`، `UnavailablePlatformStepUpAuthorization` و EF configuration عامل دوم.
- تست‌های منحصراً مربوط به enrollment/recovery/replay/key ring حذف شدند؛ تست‌های مستقل امنیت Platform/Messaging نگه داشته و همسو شدند.

## ۵. جریان اجرا

```text
AuthController → IdentityService → OTP/Session موجود
HTTP GET /api/admin/v1/me
→ SessionAuthenticationHandler
→ PlatformPermissionAuthorizationHandler
→ PlatformIamController.GetMe
→ PlatformIamService.GetMeAsync
→ EfPlatformIamStore.GetAuthorizationSnapshotAsync
→ SQL Server: User فعال + Platform assignment/permission جاری
→ PlatformMeResult
→ 200 با roles/permissions واقعی، یا 401/403
```

نام Role یا UserId فرستاده‌شده توسط client مجوز نیست. Role مدرسه حتی در Mock، Platform role ایجاد نمی‌کند.

## ۶. دیتابیس و هشدار حذف داده

Migration جدید: `20261024184613_RemovePlatformMfa`؛ Migration تاریخی `20261015073418_AddPlatformTotpMfa`، Designer آن و تمام Migrationهای تاریخی دیگر حفظ شده‌اند.

Up فقط این موارد را حذف می‌کند:

| مورد | اثر Apply |
|---|---|
| `platform.PlatformMfaRecoveryCodes` | حذف تمام hashهای Recovery، زمان مصرف و rowversion آن‌ها |
| `platform.PlatformMfaCredentials` | حذف Secret رمز‌شده و pending، وضعیت enrollment/فعال‌سازی، replay counter، تلاش و lockout و rowversion مربوط |
| `identity.UserSessions.AssuranceLevel` | حذف سطح عامل دوم؛ خود نشست‌ها حذف نمی‌شوند |
| `identity.UserSessions.StepUpAtUtc` | حذف زمان عامل دوم از نشست‌ها |
| `CK_UserSessions_AssuranceLevel` | حذف Constraint ستون حذف‌شده |

PK/FK/indexهای دو جدول همراه همان جدول‌ها حذف می‌شوند. User، Credential ورود OTP، Session، RefreshToken، Role/Permission/Assignment، Membership، Enrollment و Audit حذف نمی‌شوند.
Down فقط ساختار خالی را بازمی‌گرداند و ستون سطح را با مقدار معتبر ۱ ایجاد می‌کند؛ دادهٔ MFA و زمان‌های حذف‌شده قابل بازیابی با Down نیستند. بازیابی داده نیازمند Backup معتبر قبلی است.

**Migration روی دیتابیس اصلی لوکال، سرور یا محیط Test/Production اجرا نشده است.** فقط اجرای fixture خودکار و ایزولهٔ LocalDB برای تست با تأیید صریح مالک مجاز شد. اجرای عملیاتی، Backup و مجوز مستقل لازم دارد؛ نسخهٔ قدیمی وابسته به MFA هم‌زمان با Drop نباید فعال باشد.

## ۷. Endpointها و تغییر مصرف Frontend

| مسیر | رفتار جاری |
|---|---|
| `POST /api/v1/auth/otp/challenges` و `/verify` | قرارداد و قواعد Real/Mock موجود بدون تغییر |
| `GET /api/admin/v1/me` | Bearer session و `platform.access`؛ پاسخ 200 شامل `userId/personId/displayName/roles/permissions`؛ بدون نشست 401 و بدون permission برابر 403 |
| عملیات حساس Platform | permission-specific، Reason، state، scope، audit و concurrency موجود؛ بدون درخواست عامل دوم |
| `/api/admin/v1/mfa/*` | Controller و OpenAPI حذف؛ مسیر قابل مصرف نیست |

Frontend باید Authenticator، TOTP، enrollment، Recovery و handling خطاهای عامل دوم را حذف کند؛ پس از OTP مستقیماً `/api/admin/v1/me` را بخواند. این تحویل هیچ کد Frontend را تغییر نمی‌دهد.

## ۸. تنظیمات و وابستگی‌ها

`BackofficeSecurity` و `Security:DataProtection` دیگر consumer فعال ندارند؛ از الگوهای Test/Production حذف شدند. دو JSON خصوصی موجود از ابتدا این بخش‌ها را نداشتند و دست‌نخورده باقی ماندند؛ ساختار آن‌ها باید همچنان برابر باشد.
`BackofficeSecurity__KeyRingPath` از `MakanApp.Api/web.config` حذف شد؛ Environment و budget خاموشی و stdout قبلی حفظ شدند.
PackageReferenceهای صرفاً MFA یعنی `Otp.NET` و `Microsoft.AspNetCore.DataProtection.Abstractions` از Infrastructure و dependency مستقیم DataProtection تست Unit حذف شدند؛ ASP.NET Core shared framework تغییر نکرد.
کد/کانفیگ ملی‌پیامک، OTP، Service JWT/AI و Secretهای موجود تغییر نکرده‌اند.

## ۹. تصمیم طراحی و Trade-off

حذف Feature انتخاب شد، نه bypass یا شرط محیطی؛ کد مرده و خطاهای نامعتبر در Host باقی نمی‌مانند. هزینه، حذف امنیت عامل دوم و تخریب دادهٔ آن هنگام Apply Migration است؛ این تغییر صریحاً تصمیم بیزینسی مالک است.
حذف gate MFA نباید چک فعال‌بودن User/Session را که قبلاً در assurance مخفی بود حذف کند؛ این چک‌ها مستقل و در مسیرهای حساس حفظ شدند.
Data Protection فقط مصرف‌کنندهٔ MFA و probe همان قابلیت داشت؛ زیرساخت بلااستفادهٔ آن باقی نگه داشته نشد. کلیدهای قدیمی سرور فعلاً به‌دلیل rollback/backup حفظ می‌شوند.

## ۱۰. تست‌ها

- Unit: مدل جاری، فهرست دقیق Dropها و معتبر بودن Down؛ تست‌های OTP/Domain قبلی نیز اجرا می‌شوند.
- Integration: Owner دارای OTP/Session و permission بدون عامل دوم 200؛ Manager/Teacher/Parent/Student بدون Platform access برابر 403؛ actor فاقد permission و درخواست بدون Reason رد می‌شوند.
- نشست revoked/expired/absolute-expired و حساب suspended همچنان مجاز نیستند؛ store سن، Session ساختگی را حتی برای Owner رد می‌کند.
- Route حذف‌شده 404 و در OpenAPI غایب است؛ readiness دیگر به key ring قدیمی وابسته نیست.
- تست‌های Last Owner/Last Manager، حدود سازمان، Moderation و Realtime قبلی حفظ شده‌اند؛ test double عامل دوم نیز حذف شده است.
- Architecture: همان قواعد Onion بررسی می‌شوند؛ هیچ تستی Skip یا برای سبزشدن غیرفعال نشده است.

## ۱۱. Validation

Restore موفق بود. Buildهای Debug و Release هر دو با صفر Warning و صفر Error موفق شدند.

| اجرای کامل | Unit | Integration | Architecture | مجموع موفق | Failed / Skipped |
|---|---:|---:|---:|---:|---:|
| Debug | 866 | 1319 | 14 | 2199 | 0 / 0 |
| Release نهایی | 866 | 1319 | 14 | 2199 | 0 / 0 |

اجرای متمرکز اولیه ۲۳۳ موفق و دو شکست داشت: assertion تعداد متغیرهای IIS که با حذف متغیر MFA به ۱ رسید، و fixture جدید تعلیق که باید از `User.Suspend` برای رعایت Constraint موجود استفاده می‌کرد. هر دو اصلاح و در دو suite کامل بالا موفق شدند؛ هیچ Constraint یا تستی غیرفعال نشد.
EF pending model changes برابر NONE بود. Up روی LocalDB خودکار و ایزوله اجرا شد؛ Down فقط در metadata و تولید ساختار بازبینی شده و ادعای بازیابی داده ندارد.
هر ۹۶ فایل تاریخی Migration/Designer بدون تغییرند و جست‌وجوی کد فعال خارج از Migrationها هیچ ارجاع اجرایی MFA/Step-Up ندارد.
OpenAPI استخراج‌شده ۳۱۸ عملیات در ۵۶ Controller و ۵۴۷ schema دارد؛ مدل طراحی ۱۰۵ جدول و ۴۹ Migration دارد. استخراج metadata بدون SQL connection، worker، SMS و AI واقعی بود.
دوازده تست ابزار مستندسازی موفق‌اند؛ validator کاتالوگ، لینک‌ها، Enumها و hashهای HTML/PDF نیز موفق است؛ لینک شکسته یا اختلاف Enum وجود ندارد.
اسکن فایل‌های قابل Commit هیچ مقدار منطبق با Secretهای خصوصی پیدا نکرد؛ پنج فایل محافظت‌شده با SHA-256 backup اولیه برابرند.

## ۱۲. Git

فقط تغییرات مرتبط با حذف عامل دوم ثبت می‌شوند؛ سه فایل untracked قبلی AI و تنظیمات خصوصی خارج Commit می‌مانند. طبق درخواست این مرحله **Push انجام نمی‌شود**؛ hash و پیام Local Commit در گزارش نهایی اعلام می‌شوند.

## ۱۳. محدودیت و استقرار

هیچ Deploy، تغییر IIS، حذف key ring سرور یا Apply روی دیتابیس اصلی انجام نمی‌شود. فایل‌های قدیمی Backup/کلید و ZIPهای انتشار ۲۰۲۶-۱۰-۲۳ حفظ شده‌اند؛ ZIP قدیمی کد جدید را ندارد و باید در کار انتشار بعدی بازسازی شود.

## ۱۴. نکات آموزشی

۱. حذف یک gate امنیتی باید با ممیزی کنترل‌های جانبی آن همراه باشد.
۲. Authentication، permission و freshness عامل دوم مفاهیم جدا هستند؛ حذف آخری حذف دو مورد اول نیست.
۳. مدل جدید با پاک‌کردن Migration تاریخی ساخته نمی‌شود؛ Migration جدید تاریخچه را قابل بازتولید می‌کند.
۴. Down ساختار را برمی‌گرداند، نه دادهٔ Drop‌شده را.
۵. تست metadata اتصال SQL را اثبات نمی‌کند؛ suite ایزوله برای رفتار واقعی لازم است.
۶. مستندات/OpenAPI نیز قرارداد محصول‌اند و باید حذف endpoint را منعکس کنند.

## ۱۵. مسیر مطالعه کد

`PlatformIamController` → `PlatformPermissionAuthorizationHandler` → `PlatformIamService` → `EfPlatformIamStore` → `UserSession`/EF configuration → `RemovePlatformMfa` → `PlatformAuthorizationRegressionTests` → `PlatformSchemaRemovalTests`.

## ۱۶. مرحله بعد

بازبینی تغییر، Backup مقصد، تأیید جداگانهٔ Migration عملیاتی، ساخت Publish جدید و smoke مجاز سرور؛ هیچ‌یک با این حذف Feature به‌طور خودکار انجام نمی‌شود.
