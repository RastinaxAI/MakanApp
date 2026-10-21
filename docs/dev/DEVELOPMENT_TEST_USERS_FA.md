# کاربران Development/Test و OTP قابل تنظیم

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
| PlatformOwner | Domain/Platform/PlatformCatalog.cs | platform.PlatformRoles.Key و PlatformRoleAssignments | User و انتساب فعال؛ MFA برای Backoffice | OTP اولیه؛ سپس MFA موجود |
| Operations | همان catalog | همان مدل | همان؛ permission از catalog موجود | هیچ مجوز جدیدی ساخته نمی‌شود |
| Support | همان catalog | همان مدل | همان | دسترسی محدود طبق permission موجود |
| Finance | همان catalog | همان مدل | همان | نه نقش سازمانی و نه دسترسی آموزشی خودکار |
| Content | همان catalog | همان مدل | همان | MFA حفظ می‌شود |
| SecurityAudit | همان catalog | همان مدل | همان | مجوزها از RolePermission موجود می‌آیند |
| AbuseReviewer | همان catalog | همان مدل | همان؛ محدودیت‌های Moderation/Child Safety | به معنی private browsing آزاد نیست |

کاتالوگ پلتفرم از `PlatformRoleConfiguration` و `PlatformRolePermissionConfiguration` و migration
`20261013053224_AddPlatformIamAndAdminAudit` می‌آید. SQL نقش‌ها را با Key موجود resolve می‌کند و catalog را تغییر نمی‌دهد.
Owner/Admin/Member در Conversation و نقش‌های participant، مجوزهای context-specific هستند؛ نقش سراسری Login نیستند.
Personal workspace نیز Role جدید نیست. در مدل فعلی School جدول مستقلی برای این seed ندارد و همان Organization است.

## Authentication فعلی و مرز تغییر

- `POST /api/v1/auth/otp/challenges` → `IdentityService.RequestOtpAsync` → `IOtpDeliveryProvider`.
- `POST /api/v1/auth/otp/verify` → همان use case و `OtpChallenge.Verify` → session و refresh عادی.
- OTP فقط به‌صورت hash و salt در `[identity].[OtpChallenges]` ذخیره می‌شود؛ پاسخ HTTP کد را نشان نمی‌دهد.
- پیش‌فرض عمر ۳۰۰ ثانیه، فاصلهٔ درخواست ۳۰ ثانیه، حداکثر ۵ درخواست در ساعت و ۵ تلاش ناموفق است.
  در Testing، فاصله طبق رفتار قبلی صفر است؛ rate limiting و تلاش‌های ناموفق حذف نشده‌اند.
- `PhoneNumberNormalizer` قالب E.164 با ۸ تا ۱۵ رقم را می‌پذیرد و ارقام فارسی/عربی را normalize می‌کند.
- access token کاربر **opaque session token است، نه JWT نقش‌دار**. refresh از `/api/v1/auth/refresh` انجام می‌شود.
  JWT سرویس AI موضوع جداگانه‌ای است و تغییر نکرده است.
- Role از عضویت/انتساب معتبر و workspace جاری resolve می‌شود؛ claim جعلی Role ایجاد نمی‌شود.
- `Mode=Real` یعنی bypass خاموش؛ انتخاب SMS همچنان مطابق `Sms:Provider` قبلی است. در Development، provider
  موجود Development کد تصادفی تستی می‌سازد؛ در Production، MeliPayamakOtp همان adapter/config قبلی است.

## دیتابیس و دادهٔ ثابت seed

محیط دستی ایجادشده: سرور `(localdb)\MSSQLLocalDB`، دیتابیس `MakanApp_DevelopmentTestUsers`.
این دیتابیس مستقل از `Server=.;Database=MakanApp` و Production است؛ برنامه خودکار migration/seed اجرا نمی‌کند.
شماره‌ها از بازهٔ غیرقابل‌سرویس‌دهی 555-0100..0199 انتخاب شده‌اند، نه از شمارهٔ کاربر واقعی؛
[مرجع NANPA](https://nanpa.com/numbering/555-line-numbers). ارسال SMS برای آن‌ها هدف این قابلیت نیست.

| Role | Phone | UserId | Relationships | Test OTP |
|---|---|---|---|---|
| Student | +12025550101 | d3700000-0000-0000-0000-000000000001 | Org / Class / Enrollment | از User Secrets؛ در سند نیست |
| Teacher | +12025550102 | d3700000-0000-0000-0000-000000000002 | Org / Class / TeacherAssignment | همان تنظیم محلی |
| Parent | +12025550103 | d3700000-0000-0000-0000-000000000003 | Org / GuardianRelation به Student بالا | همان تنظیم محلی |
| Manager | +12025550104 | d3700000-0000-0000-0000-000000000004 | Org / Membership Manager | همان تنظیم محلی |
| PlatformOwner | +12025550105 | d3700000-0000-0000-0000-000000000005 | PlatformRoleAssignment | همان تنظیم؛ MFA جدا |
| Operations | +12025550106 | d3700000-0000-0000-0000-000000000006 | PlatformRoleAssignment | همان تنظیم؛ MFA جدا |
| Support | +12025550107 | d3700000-0000-0000-0000-000000000007 | PlatformRoleAssignment | همان تنظیم؛ MFA جدا |
| Finance | +12025550108 | d3700000-0000-0000-0000-000000000008 | PlatformRoleAssignment | همان تنظیم؛ MFA جدا |
| Content | +12025550109 | d3700000-0000-0000-0000-000000000009 | PlatformRoleAssignment | همان تنظیم؛ MFA جدا |
| SecurityAudit | +12025550110 | d3700000-0000-0000-0000-000000000010 | PlatformRoleAssignment | همان تنظیم؛ MFA جدا |
| AbuseReviewer | +12025550111 | d3700000-0000-0000-0000-000000000011 | PlatformRoleAssignment | همان تنظیم؛ MFA جدا |

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

پیش‌فرض base و Development و نمونهٔ Production عمداً `Identity:Otp:Mode=Real` است.
`appsettings.Development.json` فقط allowlist دقیق ۱۱ شماره/UserId را دارد و DevelopmentCode خالی است.
برای فعال‌سازی، در **نسخهٔ همین تغییر** از root Worktree اجرا کنید؛ Secret واقعی/کد ورود را در chat یا Git قرار ندهید:

```powershell
dotnet user-secrets set "Identity:Otp:DevelopmentCode" "<YOUR_LOCAL_TEST_CODE>" --project MakanApp.Api
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Identity__Otp__Mode = 'DevelopmentBypass'
$env:ConnectionStrings__MakanDatabase = 'Server=(localdb)\MSSQLLocalDB;Database=MakanApp_DevelopmentTestUsers;Integrated Security=True;TrustServerCertificate=True'
dotnet run --project MakanApp.Api --no-launch-profile --urls http://127.0.0.1:5217
```

فرمان set بالا **الگو با placeholder** است؛ برای مقدار واقعی از Manage User Secrets یا ورودی امن استفاده کنید
تا کد در shell history ثبت نشود. User Secrets این Api بین Worktreeهای دارای همان UserSecretsId مشترک است.
این تغییر Mode/ConnectionString فقط process ترمینال است؛ روی سرور یا کانفیگ دائمی Production اعمال نمی‌شود.
برای یک محیط Testing/Test، TestUsers و DevelopmentCode را صریحاً در config همان محیط بدهید؛ فایل Development خودکار در آن محیط بارگذاری نمی‌شود.

قرارداد تنظیم:

```json
{
  "Identity": {
    "Otp": {
      "Mode": "DevelopmentBypass",
      "DevelopmentCode": "",
      "TestUsers": [
        { "PhoneNumber": "+12025550101", "UserId": "d3700000-0000-0000-0000-000000000001" }
      ]
    }
  }
}
```

این fragment ساختار را نشان می‌دهد؛ allowlist کامل در فایل Development است و Code فقط از تنظیم محلی می‌آید.
کد باید رقم ASCII در بازهٔ طول OTP موجود باشد؛ شش رقم انتخاب مناسبی برای تست است.
Mode نامعتبر، Code خالی/نامعتبر، allowlist خالی/تکراری و محیط غیرمجاز در startup رد می‌شوند.
در Production و Staging حتی با Code/allowlist معتبر، DevelopmentBypass مجاز نیست.

برای Login:

1. `POST /api/v1/auth/otp/challenges` با `{"phoneNumber":"+12025550101"}`؛ پاسخ 202 شامل ChallengeId و انقضاست، نه Code.
2. `POST /api/v1/auth/otp/verify` با ChallengeId قبلی، همان PhoneNumber و کد تنظیم‌شدهٔ محلی؛ پاسخ 200 توکن session/refresh عادی است.
3. توکن را فقط در حافظهٔ کلاینت مجاز نگه دارید؛ آن را در query string، فایل نمونه، log یا گزارش نگذارید.
4. `GET /api/v1/workspaces/me` و سپس `POST /api/v1/workspaces/select` با WorkspaceType=Organization، MembershipId و Role صحیح.
5. Parent از `GET /api/v1/guardian/children` و `POST /api/v1/guardian/children/{id}/select` فرزند مجاز را انتخاب می‌کند.
6. Backoffice از MFA/step-up موجود استفاده می‌کند؛ OTP تست، session را MultiFactor نمی‌کند. `/api/admin/v1/me` بدون MFA همچنان 403 می‌دهد.

خطاها: Code اشتباه یا binding نامعتبر 400، حساب Suspended برابر 403، challenge مصرف‌شده 409،
منقضی 410 و سقف تلاش/درخواست 429 است. کد دلخواه، شمارهٔ غیرallowlist، حساب seedنشده یا Credential حذف‌شده موفق نمی‌شود.
همهٔ permission/tenant/resource/Guardian/TeacherAssignment و Child Safety فعلی حفظ شده‌اند.

برای خاموش‌کردن، process را متوقف و Mode را Real کنید؛ سپس برنامه را دوباره اجرا کنید:

```powershell
$env:Identity__Otp__Mode = 'Real'
```

challenge تست به‌دلیل domain separation در hash، در Mode=Real قابل مصرف نیست؛ حتی اگر اشتباهاً DB و SecurityKey مشترک باشند.
تغییر Deployment environment به Production به معنی مهاجرت fixture به Production نیست؛ DB/secretهای محیط‌ها باید مستقل بمانند.

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
