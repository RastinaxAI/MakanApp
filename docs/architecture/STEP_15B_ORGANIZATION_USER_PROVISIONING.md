# STEP 15B — عملیات سراسری سازمان، کاربر و Provisioning

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## وضعیت و مرز مرحله

این مرحله قابلیت‌های Backoffice برای مشاهده و مدیریت امن `Organization` و `User` و همچنین provisioning پایدار سازمان را اضافه می‌کند. مرزهای ثابت زیر حفظ شده‌اند:

- `OrganizationRole.Manager` هیچ دسترسی Platform ایجاد نمی‌کند.
- همه routeهای این مرحله زیر `/api/admin/v1` و متکی به `PlatformPermission` جاری در SQL Server هستند.
- هیچ API برای خواندن Contacts، متن گفت‌وگوی خصوصی، OTP، token نشست یا credential ساخته نشده است.
- ایجاد سازمان، اتصال `PlanVersion` منتشرشده و دعوت مدیر اولیه سه مرحله مستقل provisioning هستند.
- پرداخت موفق معادل provision شدن سازمان نیست.
- مدیریت Plan و تغییر قیمت در این مرحله وجود ندارد و به STEP 15D تعلق دارد.

## مجوزها و نگاشت نقش‌ها

مجوزهای افزوده‌شده:

- `platform.organizations.read`
- `platform.organizations.manage`
- `platform.users.read`
- `platform.users.manage`
- `platform.sessions.manage`
- `platform.provisioning.read`
- `platform.provisioning.manage`

نگاشت frozen این مرحله:

| نقش | Organizations | Users | Sessions | Provisioning |
|---|---|---|---|---|
| `PlatformOwner` | read/manage | read/manage | manage | read/manage |
| `Operations` | read/manage | read/manage | manage | read/manage |
| `Support` | read | read | manage | read |
| `Finance` | read | — | — | — |
| سایر نقش‌ها | — | — | — | — |

Permissionها از assignmentهای فعال دیتابیس resolve می‌شوند. User معلق حتی با assignment فعال Platform قابل احراز هویت یا دریافت permission نیست.

## مدل داده

### User account state

`User` دارای وضعیت صریح زیر شد:

- `Active`
- `Suspended`

فیلدهای persistence:

- `AccountStatus int NOT NULL`
- `SuspendedAtUtc datetime2(7) NULL`
- `rowversion` موجود همچنان برای concurrency استفاده می‌شود.

Constraintهای دیتابیس فقط مقدار ۱ یا ۲ را می‌پذیرند و سازگاری `SuspendedAtUtc` با status را enforce می‌کنند. migration ابتدا ستون status را nullable می‌افزاید، رکوردهای موجود را به `Active` backfill می‌کند و سپس ستون را `NOT NULL` می‌کند. SQL default دائمی ایجاد نشده است.

تعلیق User همه sessionهای فعال را revoke می‌کند. فعال‌سازی مجدد session revokeشده یا RoleAssignment پایان‌یافته را بازنمی‌گرداند. `IdentityService` هم در OTP verification و هم در opaque bearer authentication وضعیت حساب را بررسی می‌کند.

### PlatformProvisioningRequest

جدول `platform.ProvisioningRequests` وضعیت orchestration را durable نگه می‌دارد:

- `Requested`
- `OrganizationCreated`
- `SubscriptionAssigned`
- `ManagerInvitationCreated`
- `Completed`
- `NeedsAttention`

`ClientOperationId` دارای unique index است. شناسه‌های `OrganizationId`، `SubscriptionId` و `ManagerInvitationId` نتیجه هر stage را ثبت می‌کنند؛ بنابراین retry همان درخواست stage تکمیل‌شده را دوباره ایجاد نمی‌کند. payload متفاوت با همان `ClientOperationId` با conflict رد می‌شود.

هر stage در transaction مستقل SQL Server commit می‌شود. شکست stage بعدی موفقیت stage قبلی را جعل یا rollback نمی‌کند و request به `NeedsAttention` می‌رود. retry با payload اصلی از آخرین نتیجه ثبت‌شده ادامه پیدا می‌کند.

## جریان Provisioning

```text
POST /api/admin/v1/provisioning
-> PlatformProvisioningController
-> PlatformOperationsService
-> IPlatformStepUpAuthorization (در صورت اتصال Subscription)
-> EfPlatformOperationsStore
-> PlatformProvisioningRequest(Requested)
-> Organization.Create
-> PlatformProvisioningRequest(OrganizationCreated)
-> Subscription.CreatePending + Activate (PlanVersion منتشرشده، اختیاری)
-> PlatformProvisioningRequest(SubscriptionAssigned)
-> Invitation.Create(role=Manager، اختیاری)
-> PlatformProvisioningRequest(ManagerInvitationCreated)
-> PlatformProvisioningRequest(Completed)
-> AdminAuditLogهای append-only
-> HTTP 201 یا HTTP 200 برای replay
```

مدیر اولیه مستقیم به `Membership` یا `RoleAssignment` متصل نمی‌شود. او دعوت را از `/api/v1/invitations/{id}/accept` می‌پذیرد و جریان موجود Organization سپس `Membership` و `RoleAssignment(Manager)` را ایجاد می‌کند.

## حفاظت Last Manager و Last Owner

عملیات زیر با transaction سطح `Serializable` و `UPDLOCK/HOLDLOCK` محافظت می‌شوند:

- تغییر آخرین نقش `Manager` به نقش دیگر
- پایان عضویت آخرین Manager
- تعلیق Userی که آخرین Manager فعال یک یا چند سازمان است
- تعلیق آخرین `PlatformOwner` فعال

همه عملیات تغییر Manager ابتدا row سازمان را قفل می‌کنند. تعلیق User سازمان‌های تحت مدیریت را با ترتیب پایدار قفل می‌کند و فقط Managerهای متعلق به Userهای `Active` را جایگزین معتبر می‌داند. ترتیب قفل‌ها برای کاهش deadlock با عملیات IAM و Organization ثابت است.

## Read modelها و حریم خصوصی

فهرست‌های Organization و User server-side paginated هستند؛ sort فقط از allowlist پذیرفته می‌شود و queryها با projection و batch query اجرا می‌شوند. برای هر ردیف query جداگانه اجرا نمی‌شود.

Organization summary شامل status، تعداد member/class فعال، خلاصه Manager، آخرین Subscription و زمان ایجاد است. detail شامل role count، class capacity summary و timestampهای فعالیت امن است. suspension impact فقط countهای membership، class، subscription، invitation و provisioning را برمی‌گرداند و محتوای Messenger را لمس نمی‌کند.

User summary شامل identity نمایشی، شماره masked، account state، زمان ایجاد آخرین session، badge نقش‌های Platform و تعداد عضویت فعال است. User detail فقط profile امن، وضعیت mobile verification، عضویت‌ها و نقش‌های سازمانی، خلاصه Guardian، metadata نشست‌ها، Platform roleها و referenceهای audit را برمی‌گرداند. شماره کامل تلفن و token نشست هرگز در response نیست.

## APIها

| Method | Route | Permission | رفتار اصلی |
|---|---|---|---|
| GET | `/api/admin/v1/organizations` | `platform.organizations.read` | فهرست global با filter/page/sort |
| POST | `/api/admin/v1/organizations` | `platform.organizations.manage` | ایجاد idempotent سازمان با `ClientOperationId` |
| GET | `/api/admin/v1/organizations/{id}` | `platform.organizations.read` | detail امن سازمان |
| GET | `/api/admin/v1/organizations/{id}/suspension-impact` | `platform.organizations.read` | preview بدون محتوای خصوصی |
| POST | `/api/admin/v1/organizations/{id}/suspend` | `platform.organizations.manage` + step-up | تعلیق بدون حذف داده |
| POST | `/api/admin/v1/organizations/{id}/reactivate` | `platform.organizations.manage` + step-up | فعال‌سازی صریح |
| GET | `/api/admin/v1/organizations/{id}/memberships` | `platform.organizations.read` | عضویت‌های scope همان سازمان |
| POST | `/api/admin/v1/organizations/{id}/invitations` | `platform.organizations.manage` | دعوت User موجود |
| PUT | `/api/admin/v1/organizations/{id}/memberships/{membershipId}/role` | `platform.organizations.manage` | تغییر صریح نقش در همان سازمان |
| DELETE | `/api/admin/v1/organizations/{id}/memberships/{membershipId}` | `platform.organizations.manage` | پایان عضویت و roleهای فعال |
| GET | `/api/admin/v1/provisioning` | `platform.provisioning.read` | فهرست stage/status requestها |
| POST | `/api/admin/v1/provisioning` | `platform.provisioning.manage` | orchestration قابل retry؛ اتصال Subscription نیازمند step-up |
| GET | `/api/admin/v1/users` | `platform.users.read` | فهرست global با PII masked |
| GET | `/api/admin/v1/users/{id}` | `platform.users.read` | detail امن کاربر |
| POST | `/api/admin/v1/users/{id}/suspend` | `platform.users.manage` + step-up | تعلیق و revoke نشست‌ها |
| POST | `/api/admin/v1/users/{id}/reactivate` | `platform.users.manage` + step-up | فعال‌سازی بدون restore نشست/نقش پایان‌یافته |
| POST | `/api/admin/v1/users/{id}/sessions/{sessionId}/revoke` | `platform.sessions.manage` | revoke یک نشست بدون token |
| POST | `/api/admin/v1/users/{id}/sessions/revoke` | `platform.sessions.manage` | revoke همه نشست‌های فعال بدون token |

همه mutationها `Reason` bounded و audit دارند. خطاها از `ProblemDetails` و کد پایدار استفاده می‌کنند؛ از جمله `PLATFORM_LAST_MANAGER_REQUIRED`، `PLATFORM_LAST_OWNER_REQUIRED`، `PLATFORM_PROVISIONING_CONFLICT` و `ACCOUNT_SUSPENDED`.

## Migration

Migration این مرحله:

`AddPlatformOrganizationUserOperations`

این migration:

- account state را با backfill امن به `identity.Users` اضافه می‌کند؛
- جدول durable provisioning، FKها، `rowversion` و unique index عملیات را می‌سازد؛
- هفت permission جدید و role mappingهای frozen را seed می‌کند؛
- check constraintهای account state و provisioning status را اضافه می‌کند.

## تست‌های پذیرش این مرحله

تست‌های Unit lifecycle صریح User، Organization و provisioning state machine را پوشش می‌دهند. تست‌های Integration موارد زیر را اثبات می‌کنند:

- read model سراسری Organization/User و masking تلفن؛
- retry provisioning بدون Organization/Subscription/Invitation تکراری؛
- پذیرش دعوت مدیر اولیه و ایجاد Manager role از جریان موجود؛
- impact preview، step-up و audit تعلیق/فعال‌سازی سازمان؛
- isolation عضویت Teacher در دو سازمان؛
- رقابت هم‌زمان برای پایان دو Manager و باقی ماندن دقیقاً یک Manager؛
- رد حذف نقش، پایان عضویت و تعلیق آخرین Manager؛
- تعلیق User، revoke نشست، رد opaque session و رد OTP login در حالت suspended؛
- حفاظت Last Owner در مسیر تعلیق User؛
- revoke یک نشست و همه نشست‌ها بدون افشای token؛
- دسترسی read و session revoke برای Support و رد organization mutation؛
- ثبت audit برای mutationهای حساس.

## محدودیت‌های عمدی

- provider واقعی MFA/step-up هنوز انتخاب نشده و implementation تولید fail-closed است؛ بنابراین mutationهای حساس در Production تا STEP 16/provider مصوب قابل اجرا نیستند.
- ارسال واقعی notification دعوت در این مرحله اضافه نشده است؛ lifecycle دعوت موجود منبع حقیقت است.
- Plan administration، payment reconciliation و subscription override خارج از provisioning به STEP 15D تعلق دارند.
- هیچ impersonation، full-phone permission یا private-message access وجود ندارد.
