# STEP 15A — بنیان Platform IAM و Platform Owner

## 1. نتیجه مرحله

این مرحله یک مرز مستقل برای مدیریت پلتفرم ایجاد می‌کند. نقش‌های سازمانی قبلی بدون تغییر باقی مانده‌اند و `OrganizationRole.Manager` هیچ دسترسی خودکاری به Backoffice ندارد.

قابلیت‌های اضافه‌شده:

- catalog ثابت Platform Role و Platform Permission
- assignment نقش پلتفرم به `UserId` موجود
- authorization policy مبتنی بر permission جاری SQL Server
- `GET /api/admin/v1/me`
- مشاهده role catalog و assignmentها
- grant/revoke امن role با reason و step-up gate
- bootstrap خارج از HTTP برای اولین Platform Owner
- محافظت race-safe از آخرین Owner
- Admin Audit append-only در مسیر Application/EF
- query صفحه‌بندی‌شده و filter/sort allowlist برای audit
- تست صریح عدم عبور Platform Owner از privacy پیام‌رسان

## 2. مرز معماری

### Domain

`MakanApp.Domain/Platform` شامل مدل‌های خالص زیر است:

- `PlatformRole`
- `PlatformPermission`
- `PlatformRolePermission`
- `PlatformRoleAssignment`
- `AdminAuditLog`
- catalogهای ثابت key و deterministic ID

Domain به EF Core، ASP.NET Core یا SQL Server وابسته نیست.

### Application

`MakanApp.Application/Platform` شامل contractها، serviceهای use case و portهای زیر است:

- `PlatformIamService`
- `PlatformBootstrapService`
- `IPlatformIamStore`
- `IPlatformPermissionResolver`
- `IPlatformStepUpAuthorization`

Application دلیل عملیات، query bounds، sort allowlist و step-up را validate می‌کند.

### Infrastructure

`EfPlatformIamStore` queryها و transactionهای SQL Server را پیاده‌سازی می‌کند. عملیات bootstrap و grant/revoke در transaction با `Serializable` و lockهای `UPDLOCK, HOLDLOCK` اجرا می‌شوند. برای جلوگیری از deadlock، revoke ابتدا role catalog row و سپس assignment را با ترتیب ثابت lock می‌کند.

`UnavailablePlatformStepUpAuthorization` پیش‌فرض production است و همیشه fail-closed برمی‌گرداند. این کلاس MFA واقعی نیست.

### API

`PlatformPermissionAuthorizationHandler` در هر درخواست permission را از `IPlatformPermissionResolver` و در نتیجه از DB جاری می‌خواند. permission در session token ذخیره نشده است؛ بنابراین revoke در درخواست بعدی فوراً مؤثر است.

Controllerها فقط actor/session را از principal می‌خوانند، transport را map می‌کنند و use case را فراخوانی می‌کنند.

## 3. catalog اولیه

Roleهای seedشده:

- `PlatformOwner`
- `Operations`
- `Support`
- `Finance`
- `Content`
- `SecurityAudit`
- `AbuseReviewer`

Permissionهای STEP 15A:

- `platform.access`
- `platform.roles.read`
- `platform.roles.manage`
- `platform.audit.read`
- `platform.security.read`

`PlatformOwner` همه permissionهای این مرحله را دارد. `SecurityAudit` فقط access/audit/security read دارد. roleهای دیگر در این مرحله فقط `platform.access` دارند تا mappingهای عملیاتی در مرحله‌ای که resource واقعی اضافه می‌شود توسعه پیدا کنند.

هیچ permission با معنای `private_chats.read_all` وجود ندارد.

## 4. مدل دیتابیس

Migration:

`20261013053224_AddPlatformIamAndAdminAudit`

تمام جدول‌های جدید در schema برابر `platform` هستند.

### `PlatformRoles`

- PK: `Id uniqueidentifier`
- `Key nvarchar(100)` با unique index
- `DisplayName nvarchar(150)`
- catalog با IDهای deterministic seed می‌شود.

### `PlatformPermissions`

- PK: `Id uniqueidentifier`
- `Key nvarchar(150)` با unique index
- `Description nvarchar(300)`
- فقط metadata غیرمحرمانه seed می‌شود.

### `PlatformRolePermissions`

- PK مرکب: `(RoleId, PermissionId)`
- FKهای Restrict به role و permission

### `PlatformRoleAssignments`

- PK: `Id`
- FK `UserId` به `identity.Users`
- FK `RoleId` به `platform.PlatformRoles`
- `GrantedByUserId` و `EndedByUserId` اختیاری
- lifecycle با `Status`، `GrantedAtUtc` و `EndedAtUtc`
- `rowversion` برای concurrency
- filtered unique index روی `(UserId, RoleId)` برای assignment فعال
- index روی role/status برای شمارش و lock محافظ آخرین Owner

### `AdminAuditLogs`

- actor شامل User/Person/Session در صورت وجود
- action، target، Organization اختیاری، reason، UTC time، correlation، result و summary امن
- check constraint برای result
- indexهای زمان، actor و action
- هیچ API برای update/delete وجود ندارد.
- `MakanDbContext` هر update/delete tracked روی audit را رد می‌کند.

## 5. جریان authorization

```text
HTTP Request
-> SessionAuthenticationHandler
-> PlatformPermissionAuthorizationHandler
-> PlatformIamService.HasPermissionAsync
-> EfPlatformIamStore.GetAuthorizationSnapshotAsync
-> SQL Server active assignments + role permissions
-> allow / 401 / 403
-> Controller
-> Application use case
-> Infrastructure transaction
-> SQL Server
-> HTTP Response
```

کاربر unauthenticated پاسخ `401` می‌گیرد. کاربر authenticated بدون permission، از جمله Organization Manager عادی، پاسخ `403` می‌گیرد.

## 6. endpointها

### `GET /api/admin/v1/me`

- permission: `platform.access`
- response: UserId، PersonId اختیاری، display identity امن، roleهای فعال و permissionهای مؤثر
- credential، phone، token یا secret بازگردانده نمی‌شود.

### `GET /api/admin/v1/roles`

- permission: `platform.roles.read`
- response: catalog role و permission mapping فعلی

### `GET /api/admin/v1/role-assignments`

- permission: `platform.roles.read`
- filter: UserId، RoleKey و ActiveOnly
- pagination محدود به حداکثر 100 آیتم

### `POST /api/admin/v1/role-assignments`

- permission: `platform.roles.manage`
- request: UserId، RoleKey و Reason
- step-up الزامی
- idempotent برای assignment فعال یکسان
- عملیات موفق audit می‌شود.

### `DELETE /api/admin/v1/role-assignments/{assignmentId}`

- permission: `platform.roles.manage`
- body: Reason
- step-up الزامی
- پایان آخرین Platform Owner با `PLATFORM_LAST_OWNER_REQUIRED` رد می‌شود.
- پایان تکراری assignment قبلاً تمام‌شده replay امن است.

### `GET /api/admin/v1/audit-logs`

- permission: `platform.audit.read`
- filterهای actor/action/target/result/time
- pagination server-side
- sort allowlist: `occurredAtUtc`، `action` و `result`

## 7. bootstrap اولین Owner

bootstrap endpoint عمومی ندارد و در startup به‌صورت خودکار اجرا نمی‌شود. فرمان عملیاتی:

```powershell
dotnet run --project MakanApp.Api -- platform bootstrap-owner --user-id <existing-user-guid>
```

این فرمان از connection/configuration همان محیط استفاده می‌کند و فقط User موجود را می‌پذیرد.

رفتار:

1. transaction با isolation برابر Serializable باز می‌شود.
2. row ثابت `PlatformOwner` با `UPDLOCK, HOLDLOCK` lock می‌شود.
3. User موجود بودن بررسی می‌شود.
4. assignmentهای Owner فعال lock می‌شوند.
5. اگر Owner وجود ندارد، اولین assignment و audit در یک transaction ثبت می‌شوند.
6. تکرار برای همان User idempotent است.
7. اگر Owner دیگری موجود باشد، مسیر با `PLATFORM_BOOTSTRAP_CLOSED` بسته می‌شود.

هیچ User مشخصی seed نشده و هیچ secret یا grant خودکار در configuration وجود ندارد.

## 8. last-owner و concurrency

همه mutationهای role روی role row مربوطه lock هماهنگ می‌گیرند. در revoke Owner، شمارش assignmentهای فعال زیر همان transaction و lock انجام می‌شود. بنابراین دو revoke هم‌زمان نمی‌توانند هر دو با snapshot قدیمی تصمیم بگیرند و تعداد Owner فعال را به صفر برسانند.

filtered unique index نیز grant هم‌زمان duplicate برای یک User/Role را در سطح SQL Server محدود می‌کند.

## 9. Step-up و وضعیت MFA

`IPlatformStepUpAuthorization` مرز فنی step-up است. provider واقعی MFA هنوز انتخاب نشده است.

- Production/default: `UnavailablePlatformStepUpAuthorization` و fail-closed
- Testing: fake قابل کنترل و فقط در test host
- OTP login به‌عنوان MFA/step-up ادعا نشده است.
- TOTP، WebAuthn یا SMS MFA حدس زده یا پیاده‌سازی نشده‌اند.

نتیجه: read endpointها قابل استفاده‌اند، اما mutation حساس در production تا اتصال provider واقعی عمداً با `PLATFORM_STEP_UP_REQUIRED` بسته است.

## 10. Audit و privacy

reason غیرخالی و محدود برای grant/revoke الزامی است. summary فقط شناسه User، role key و status را ثبت می‌کند و token/credential/private message در آن قرار نمی‌گیرد.

Platform permission handler فقط روی `/api/admin/v1` اعمال شده است و هیچ تغییری در authorization APIهای عادی پیام‌رسان ایجاد نمی‌کند. تست regression اثبات می‌کند Platform Owner خارج از membership/participant جاری نمی‌تواند:

- پیام‌های گفت‌وگوی دیگران را بخواند؛
- contactهای دیگران را در contact list خود ببیند؛
- پرونده/evidence moderation را از مسیر conversation-scoped بخواند.

## 11. تست‌های مرحله

Unit:

- lifecycle انتساب role
- الزام UTC
- validation audit reason/correlation
- ثابت بودن catalog و نبود permission خصوصی
- fail-closed بودن provider پیش‌فرض step-up

Integration:

- 401 برای anonymous و 403 برای Organization Manager
- دسترسی Platform Owner پس از bootstrap
- bootstrap concurrent، idempotent و بسته‌شدن پس از اولین Owner
- role grant/revoke و اثر فوری revoke از DB
- step-up fail-closed و reason اجباری
- رقابت revoke و حفاظت از آخرین Owner
- read/query audit و append-only guard
- sort allowlist
- privacy regression پیام‌رسان/contact/moderation

## 12. محدودیت‌های عمدی

- MFA واقعی هنوز متصل نیست.
- عملیات Organization/User/Provisioning مربوط به STEP 15B است.
- Platform Safety Queue مربوط به STEP 15C است.
- Commerce/Finance/Operations مربوط به STEP 15D است.
- هیچ frontend یا landing page ایجاد نشده است.
