# کاربران Development/Test و OTP قابل تنظیم

بازبینی جاری `2026-10-23` از `main@5885d11`؛ بخش ورود این فایل با قرارداد فعلی Otp همگام شده است. شناسه‌ها، SQL و اعداد validation مرحلهٔ اولیه تاریخچهٔ fixture هستند، نه دادهٔ تضمین‌شدهٔ محیط امروز.

> به‌روزرسانی Consolidation: پیش از Seed، اسکریپت فقط‌خواندنی
> `check-development-test-users-database.sql` مقصد، ستون‌ها و تاریخچهٔ EF را بررسی می‌کند.
> Seed موجود در این checkout همچنان فقط دیتابیس ایزوله با پیشوند
> `MakanApp_DevelopmentTestUsers` و تأیید connection را می‌پذیرد؛ اجازهٔ اجرا روی دیتابیس
> عادی گسترش نیافته است. نبود ستون‌های امنیتی، خطای صریح 51016 می‌دهد؛ بدنه پس از guard
> compile می‌شود تا خطای 207 پیام Migration را پنهان نکند. هیچ Schema به‌صورت دستی اصلاح نمی‌شود.

> بخش OTP این راهنمای قبلی با قرارداد [Configurable OTP](CONFIGURABLE_OTP_FA.md) جایگزین شده است.
> DevelopmentCode/TestUsers دیگر روش فعال‌سازی Authentication نیستند. دیتابیس دائمی LocalDB این مرحله نیز
> با تأیید مالک حذف شده است؛ برای Login دستی فقط MakanApp روی سرور محلی `.` مبناست. این سند مجوز ساخت DB تازه نیست.

گزارش تغییر شماره‌ها، تست‌ها و محدودیت حساب‌های موجود در [گزارش شماره‌های ایرانی](IRANIAN_SEED_PHONES_REPORT_FA.md) آمده است.

تاریخ: ۲۰۲۶-۱۰-۲۱. این قابلیت فقط برای محیط مستقل توسعه است؛ نه Login عمومی با هر کد.
نسخهٔ مبنا `211cd24` است. تغییرات در Worktree جداگانهٔ `.artifacts/development-test-users-20261021`
انجام شده‌اند؛ workspace اصلی دارای تغییرات AI است و بازنویسی/پاک‌سازی نشده است.

## ROLE DISCOVERY

| Role | Source | Database representation | Required relationships | Login supported / نکته |
|---|---|---|---|---|
| Student | Domain/Organization/OrganizationRole.cs | organization.RoleAssignments.Role=1 | User، Membership فعال، OrganizationPerson متصل به Person حساب، Enrollment فعال | OTP عادی؛ workspace دانش‌آموز |
| Teacher | همان enum | Role=2 | User، Membership و TeacherAssignment فعال روی Class | OTP عادی؛ نقش به‌تنهایی مجوز هر کلاس نیست |
| Parent | همان enum | Role=3 | User، Membership، GuardianRelation فعال به OrganizationPerson کودک | OTP عادی؛ انتخاب فرزند الزامی است |
| Manager | همان enum | Role=4 | Membership و RoleAssignment فعال در سازمان | School Manager ادمین پلتفرم نیست |
| PlatformOwner | Domain/Platform/PlatformCatalog.cs | platform.PlatformRoles.Key و PlatformRoleAssignments | User و انتساب فعال؛ permission برای Backoffice | OTP و نشست معتبر و permission |
| Operations | همان catalog | همان مدل | همان؛ permission از catalog موجود | هیچ مجوز جدیدی ساخته نمی‌شود |
| Support | همان catalog | همان مدل | همان | دسترسی محدود طبق permission موجود |
| Finance | همان catalog | همان مدل | همان | نه نقش سازمانی و نه دسترسی آموزشی خودکار |
| Content | همان catalog | همان مدل | همان | permission حفظ می‌شود |
| SecurityAudit | همان catalog | همان مدل | همان | مجوزها از RolePermission موجود می‌آیند |
| AbuseReviewer | همان catalog | همان مدل | همان؛ محدودیت‌های Moderation/Child Safety | به معنی private browsing آزاد نیست |

کاتالوگ پلتفرم از `PlatformRoleConfiguration` و `PlatformRolePermissionConfiguration` و migration
`20261013053224_AddPlatformIamAndAdminAudit` می‌آید. SQL نقش‌ها را با Key موجود resolve می‌کند و catalog را تغییر نمی‌دهد.
Owner/Admin/Member در Conversation و نقش‌های participant، مجوزهای context-specific هستند؛ نقش سراسری Login نیستند.
Personal workspace نیز Role جدید نیست. در مدل فعلی School جدول مستقلی برای این seed ندارد و همان Organization است.

## Authentication فعلی و مرز تغییر

- انتخاب فقط با `Otp:Enabled` و `Otp:AllowMockAuthentication` است: Real برابر true/false؛ Mock مجاز برابر false/true، فقط در Development/Test/Testing. Production/Staging هر ترکیب ناامن را در startup رد می‌کنند.
- در Real، `IdentityService.RequestOtpAsync → IOtpDeliveryProvider` و `VerifyOtpAsync → OtpChallenge.Verify` اجرا می‌شوند. کد فقط HMAC/salt در SQL دارد؛ عمر پایه۳۰۰s، resend۳۰s، سقف۵ درخواست در ساعت و۵ تلاش ناموفق است.
- در Mock، challenge واقعی در SQL و فراخوانی SMS نداریم؛ verify فقط فرمت شماره/کد را می‌سنجد. کد ثابت در User Secrets و allowlist قدیمی DevelopmentCode/TestUsers روش فعال‌سازی نیستند. `Identity:Otp:Mode=DevelopmentBypass` و فلگ‌های قدیمی bypass همچنان رد می‌شوند.
- `PhoneNumberNormalizer` قالب E.164 با۸..۱۵ رقم و normalize ارقام فارسی/عربی را می‌پذیرد. طول کد فعلی۴..۶۴ و بدون control character است؛ شماره و مقدار کد مجاز در Mock، مالکیت تلفن را ثابت نمی‌کند.
- `VerifyOtpAsync` در هر دو حالت حساب معلق را رد و session/refresh عادی صادر می‌کند. شمارهٔ جدید می‌تواند مطابق مسیر قبلی User بسازد؛ نقش، Membership، Enrollment یا Platform permission خودکار ساخته نمی‌شود. Mock محدود به۱۱ fixture یا allowlist شماره نیست.
- access token کاربر opaque است، نه JWT نقش‌دار؛ refresh از `/api/v1/auth/refresh` و workspace/permission از رابطه جاری resolve می‌شود. JWT سرویس AI موضوع جداست. تغییر flag نشست‌های قبلی را revoke نمی‌کند.

## دیتابیس و دادهٔ ثابت seed تاریخی

محیط تاریخی این fixture، `(localdb)\MSSQLLocalDB` و دیتابیس `MakanApp_DevelopmentTestUsers` بود؛ طبق گزارش مالک آن پایگاه دائمی حذف شده است. این نام اثبات وجود پایگاه امروز نیست و سند مجوز ساخت/Seed تازه نمی‌دهد.
این دیتابیس مستقل از `Server=.;Database=MakanApp` و Production است؛ برنامه خودکار migration/seed اجرا نمی‌کند.
شماره‌های fixture با درخواست مالک به قالب ایران تغییر کرده‌اند: ورودی محلی `09120000101` تا
`09120000111` و مقدار canonical ذخیره‌شده `+989120000101` تا `+989120000111`.
این شماره‌ها صرفاً نمونه‌اند و بدون مالک بودن آن‌ها تضمین نشده است؛ فقط با Mock OTP تأییدشدهٔ
Development/Test و بدون SMS استفاده شوند. تنظیم جاری در [راهنمای OTP](CONFIGURABLE_OTP_FA.md) است.
تغییر فایل SQL، کاربران دیتابیس موجود را تغییر نمی‌دهد؛ اجرای Seed و اصلاحات ناتمام سازگاری مقصد، کار جدا هستند.

| Role | Phone | UserId | Relationships | ورود فعلی |
|---|---|---|---|---|
| Student | +989120000101 | d3700000-0000-0000-0000-000000000001 | Org / Class / Enrollment | Mock مجاز؛ بدون کد ثابت |
| Teacher | +989120000102 | d3700000-0000-0000-0000-000000000002 | Org / Class / TeacherAssignment | Mock مجاز |
| Parent | +989120000103 | d3700000-0000-0000-0000-000000000003 | Org / GuardianRelation به Student بالا | Mock مجاز |
| Manager | +989120000104 | d3700000-0000-0000-0000-000000000004 | Org / Membership Manager | Mock مجاز |
| PlatformOwner | +989120000105 | d3700000-0000-0000-0000-000000000005 | PlatformRoleAssignment | Mock مجاز؛ permission واقعی لازم |
| Operations | +989120000106 | d3700000-0000-0000-0000-000000000006 | PlatformRoleAssignment | Mock مجاز؛ permission واقعی لازم |
| Support | +989120000107 | d3700000-0000-0000-0000-000000000007 | PlatformRoleAssignment | Mock مجاز؛ permission واقعی لازم |
| Finance | +989120000108 | d3700000-0000-0000-0000-000000000008 | PlatformRoleAssignment | Mock مجاز؛ permission واقعی لازم |
| Content | +989120000109 | d3700000-0000-0000-0000-000000000009 | PlatformRoleAssignment | Mock مجاز؛ permission واقعی لازم |
| SecurityAudit | +989120000110 | d3700000-0000-0000-0000-000000000010 | PlatformRoleAssignment | Mock مجاز؛ permission واقعی لازم |
| AbuseReviewer | +989120000111 | d3700000-0000-0000-0000-000000000011 | PlatformRoleAssignment | Mock مجاز؛ permission واقعی لازم |

شناسه‌های مشترک:

```text
Organization/School: d3800000-0000-0000-0000-000000000001
AcademicPeriod:      d3810000-0000-0000-0000-000000000001
Course:              d3820000-0000-0000-0000-000000000001
Class:               d3830000-0000-0000-0000-000000000001
Student OrgPerson:   d3740000-0000-0000-0000-000000000001
Teacher Membership:  d3730000-0000-0000-0000-000000000002
GuardianRelation:    d3840000-0000-0000-0000-000000000001
Enrollment:          d3850000-0000-0000-0000-000000000001
TeacherAssignment:   d3860000-0000-0000-0000-000000000001
```

Usernameهای `dev.Student` و سایر `dev.<RoleKey>` برای کامل‌بودن profile لازم‌اند، نه Password Login.
هیچ Password، MFA secret، age verification یا session آماده‌ای seed نمی‌شود؛ سن Unknown باقی می‌ماند.

## SSMS: اجرا و اطمینان از مقصد

۱. SSMS را باز و به `(localdb)\MSSQLLocalDB` با Windows Authentication وصل شوید.
۲. دیتابیس مستقل `MakanApp_DevelopmentTestUsers` را انتخاب کنید. نام مشابه Production/Backup را انتخاب نکنید.
۳. query زیر را اجرا کنید؛ نام و سرور را با محیط توسعهٔ مورد تأیید خود تطبیق دهید:

```sql
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName,
       SERVERPROPERTY('IsLocalDB') AS IsLocalDB;
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
```

نام Development به‌تنهایی اثبات غیرProduction بودن یک DB دلخواه نیست؛ اتصال مشخص LocalDB و دادهٔ آن را بررسی کنید.
۴. اگر دیتابیس تازهٔ دیگری می‌سازید، migrationهای موجود را ابتدا **فقط روی همان مقصد مستقل** اعمال کنید:

```powershell
$env:ConnectionStrings__MakanDatabase = 'Server=(localdb)\MSSQLLocalDB;Database=MakanApp_DevelopmentTestUsers;Integrated Security=True;TrustServerCertificate=True'
dotnet tool restore
dotnet tool run dotnet-ef database update --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --connection $env:ConnectionStrings__MakanDatabase
Remove-Item Env:ConnectionStrings__MakanDatabase
```

`Infrastructure` startup ابزار EF این repository است؛ Api به EF Design خصوصی آن ارجاع مستقیم ندارد.
اگر قبلاً برای متغیر process مقدار داشته‌اید، به‌جای Remove آن را به مقدار قبلی برگردانید.
برای دادهٔ تست migration تازه نسازید؛ این کار schema جدیدی ندارد.

۵. فایل [create-development-test-users.sql](create-development-test-users.sql) را در SSMS باز کنید.
در **همان connection**، ابتدا این تأیید را اجرا کنید، سپس خود فایل را کامل اجرا کنید:

```sql
EXEC sys.sp_set_session_context
    @key=N'MakanApp.DevelopmentSeedApproved', @value=N'DevelopmentOnly';
```

اسکریپت فقط DB با پیشوند `MakanApp_DevelopmentTestUsers` را می‌پذیرد؛ transaction، XACT_ABORT و
`sp_getapplock` دارد. اجرای تکراری duplicate نمی‌سازد و حساب/شمارهٔ موجود را merge نمی‌کند.
Suspension یا پایان انتساب نقش با اجرای دوباره خودکار رفع نمی‌شود. خروجی انتهای اسکریپت کاربران واقعی DB را نشان می‌دهد.

## Queryهای بررسی کاربران، نقش‌ها و رابطه‌ها

### تغییر شمارهٔ ۱۱ حساب تست موجود به قالب ایران

برای حساب‌هایی که قبلاً با شماره‌های نمونهٔ قبلی ساخته شده‌اند، Seed را دوباره اجرا نکنید.
پس از Backup، فقط روی دیتابیس توسعهٔ محلی و در همان پنجرهٔ SSMS، این تأییدها را تنظیم کنید:

```sql
USE [MakanApp];
EXEC sys.sp_set_session_context
    @key=N'MakanApp.DevelopmentSeedApproved', @value=N'DevelopmentOnly';
EXEC sys.sp_set_session_context
    @key=N'MakanApp.DevelopmentSeedDatabase', @value=N'MakanApp';
EXEC sys.sp_set_session_context
    @key=N'MakanApp.DevelopmentSeedPhoneUpdateApproved', @value=N'IranianFixturePhones';
```

سپس [update-development-test-user-phones-to-iranian.sql](update-development-test-user-phones-to-iranian.sql)
را در همان اتصال اجرا کنید. اسکریپت مالکیت دقیق User/Person/Credential و انتساب نقش‌ها را بررسی می‌کند؛
فقط شمارهٔ قبلیِ شناخته‌شده را تغییر می‌دهد و با شمارهٔ نامنتظر یا برخورد با حساب دیگر، کامل Rollback می‌شود.
اجرای مجدد صفر تغییر دارد. UserId، شخص اصلی، نقش‌ها، زمان‌های Verified/Created و sessionها تغییر نمی‌کنند؛
فقط rowversion همان Credentialهای واقعاً به‌روزشده به‌صورت خودکار تغییر می‌کند.
این اسکریپت راه تغییر شمارهٔ کاربران واقعی یا مجوز استفاده در Production نیست؛ SMS واقعی ارسال نکنید.

### بررسی حساب‌ها و رابطه‌ها

```sql
SELECT u.Id,u.Username,u.AccountStatus,c.NormalizedIdentifier,p.FirstName,p.LastName
FROM [identity].Users u
JOIN [identity].UserCredentials c ON c.UserId=u.Id
JOIN [identity].Persons p ON p.Id=u.PersonId
WHERE u.Username LIKE N'dev.%';

SELECT u.Username,m.OrganizationId,m.Status AS MembershipStatus,r.[Role],r.Status AS AssignmentStatus
FROM organization.Memberships m JOIN [identity].Users u ON u.Id=m.UserId
JOIN organization.RoleAssignments r ON r.MembershipId=m.Id
WHERE m.OrganizationId='d3800000-0000-0000-0000-000000000001';

SELECT u.Username,r.[Key],a.Status,a.EndedAtUtc
FROM platform.PlatformRoleAssignments a JOIN platform.PlatformRoles r ON r.Id=a.RoleId
JOIN [identity].Users u ON u.Id=a.UserId WHERE u.Username LIKE N'dev.%';

SELECT g.GuardianUserId,g.LearnerOrganizationPersonId,g.Status,g.ValidFromUtc,g.EndedAtUtc
FROM guardian.GuardianRelations g
WHERE g.Id='d3840000-0000-0000-0000-000000000001';
SELECT OrganizationId,ClassId,TeacherMembershipId,Status,EndedAtUtc FROM academic.TeacherAssignments
WHERE Id='d3860000-0000-0000-0000-000000000001';
SELECT OrganizationId,ClassId,LearnerOrganizationPersonId,Status,EndedAtUtc FROM academic.Enrollments
WHERE Id='d3850000-0000-0000-0000-000000000001';
```

Role سازمانی ۱=Student، ۲=Teacher، ۳=Parent، ۴=Manager است. این اعداد از enum فعلی‌اند؛ User.Role نداریم.

## Configuration و Login دستی

base، Development و Test فعلی OTP=false/true و الگوی Production=true/false دارند. انتخاب Production به‌تنهایی config پایهٔ Mock را امن نمی‌کند؛ override صریح Production لازم است. برای آزمایش دستی فقط پایگاه و دادهٔ توسعهٔ مجاز خود را انتخاب کنید؛ SQL fixture بالا را برای جبران نبود داده روی دیتابیس عادی اجرا نکنید.

در process محلی Development، پس از تأمین ConnectionStrings:MakanDatabase و Identity key/Storage مناسب همان محیط از تنظیمات امن، می‌توان حالت را صریح انتخاب کرد:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Otp__Enabled = 'false'
$env:Otp__AllowMockAuthentication = 'true'
dotnet run --project MakanApp.Api --no-launch-profile --urls http://127.0.0.1:5217
```

فرمان بالا مقدار یا مقصد اتصال نمی‌سازد و روی loopback است؛ آن را به سرور عمومی یا داده/کلید Production متصل نکنید. User Secrets این Api میان worktreeهای با همان UserSecretsId مشترک است؛ تغییر تنظیم محلی باید آگاهانه باشد. override فعال قدیمی DevelopmentBypass را از تنظیم مجاز همان process رفع کنید؛ مقدار secret را برای عیب‌یابی چاپ نکنید.

۱. `POST /api/v1/auth/otp/challenges` با شمارهٔ حساب آزمایشی مجاز؛ پاسخ202 شکل ChallengeId/expiry دارد، بدون OTP و بدون challenge SQL در Mock.

۲. `POST /api/v1/auth/otp/verify` با همان شکل قرارداد و code دارای فرمت مجاز؛ در Mock مقدار کد/شناسه اثبات OTP نیست. حساب موجود reuse می‌شود و حساب معلق403 می‌گیرد. در Mock تکرار verify می‌تواند session جدید صادر کند؛ آن را retry idempotent فرض نکنید.

۳. `GET /api/v1/workspaces/me` و انتخاب workspace سازمانی با MembershipId/Role واقعی. Parent فرزند مجاز را از guardian children انتخاب می‌کند؛ Backoffice MFA/step-up موجود را لازم دارد.

۴. token را در URL، log، screenshot یا فایل مشترک نگذارید. cache نقش و workspace مدرک مجوز نیست؛ سرور رابطه جاری را دوباره بررسی می‌کند.

کد/شماره با فرمت نامعتبر400 است. challenge ناموجود/منقضی/مصرف‌شده، تلاش غلط و محدودیت شماره از قواعد **Real** هستند؛ Mock challenge واقعی و شمارندهٔ OTP آن مسیر را نمی‌سازد، ولی rate limit میزبان و session/resource authorization باقی‌اند.

برای برگشت به Real هر دو flag را تغییر دهید و برنامه را دوباره اجرا کنید:

```powershell
$env:Otp__Enabled = 'true'
$env:Otp__AllowMockAuthentication = 'false'
```

Real در Development با Sms:Provider=Development کد تصادفی حافظه‌ای دارد و endpoint عمومی دریافت کد ندارد؛ Production از MeliPayamakOtp با config/secret موجود استفاده می‌کند. برای شماره‌های fixture بالا SMS واقعی ارسال نکنید. تغییر flag نشست‌های Mock را لغو نمی‌کند؛ پیش از انتقال محیط، نشست‌های آزمایشی با workflow موجود revoke شوند و DB/secret/ring محیط‌ها مستقل بمانند. [قرارداد کامل OTP](CONFIGURABLE_OTP_FA.md) و [سرور Test محدود](SERVER_MOCK_OTP_FA.md).

## Cleanup

ابتدا برنامهٔ تست و sessionهای کلاینت را متوقف کنید. روی همان DB توسعه و پس از همان session-context approval،
[cleanup-development-test-users.sql](cleanup-development-test-users.sql) را کامل اجرا کنید.
اسکریپت فقط accountهای fixture، session/refresh/challenge آن‌ها و روابط seed را در transaction حذف می‌کند؛
catalog نقش/permission را حذف نمی‌کند. با وجود وابستگی تازه مثل تکلیف، پیام، MFA یا audit دارای FK محدودکننده،
عملیات rollback می‌شود؛ برای عبور از FK، cascade، پاک‌سازی عمومی یا حذف تاریخچه اضافه نکنید.
این محدودیت عمدی است؛ وابستگی جدید باید جداگانه بررسی شود. ابتدا backup توسعه بگیرید؛ حذف داده بدون backup قابل بازیابی نیست.

## Validation و یادگیری

نتیجهٔ واقعی build/test، فایل‌های تغییرکرده، تست‌های اضافه و Git در
[گزارش توسعه‌دهنده](DEVELOPMENT_TEST_USERS_REPORT_FA.md) ثبت می‌شود.
هیچ نتیجهٔ تست کنترل‌شده به‌عنوان ارسال واقعی SMS یا استقرار Production گزارش نمی‌شود.
