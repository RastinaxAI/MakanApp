# STEP 15-0 — ممیزی قراردادهای Backoffice پلتفرم

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## 1. هدف و دامنه

این سند وضعیت واقعی backend را در baseline زیر ثبت می‌کند:

- Branch: `main`
- Commit: `6128d23b0adb35699ad5d40c438b3234b0c0dcad`
- معماری: Onion Architecture در Modular Monolith
- Runtime: `.NET 9` و ASP.NET Core Controller-based API
- Persistence: EF Core 9 و Microsoft SQL Server
- تعداد تست‌های baseline: 830 تست (`306 Unit`، `510 Integration`، `14 Architecture`)

هدف STEP 15-0 فقط ممیزی و freeze کردن قراردادهای لازم برای Backoffice است. در این مرحله endpoint، مدل دامنه، migration یا مجوز اجرایی جدیدی ایجاد نشده است.

تعریف وضعیت‌ها:

- `IMPLEMENTED`: قابلیت و کنترل امنیتی لازم در کد فعلی وجود دارد.
- `PARTIAL`: بخشی قابل استفاده وجود دارد، اما قرارداد Backoffice کامل نیست.
- `MISSING`: قابلیت لازم در backend وجود ندارد.
- `DEFERRED`: آگاهانه به مرحله بعد یا تصمیم محصول/Provider منتقل شده است.
- `CONFLICT`: رفتار فعلی با مرز Backoffice ناسازگار است و نباید مستقیم بازاستفاده شود.

## 2. جمع‌بندی اجرایی

backend فعلی یک سامانه عملیاتی مبتنی بر کاربر و workspace سازمانی است. احراز هویت، Organization membership، نقش‌های سازمانی، آموزش، ارزیابی، پیام‌رسان، اعلان، گزارش و Commerce وجود دارند؛ اما هیچ Platform IAM، Platform Administrator، مسیر `/api/admin/v1`، provisioning سراسری، audit مدیریتی append-only یا سیاست step-up وجود ندارد.

نتیجه کلیدی:

1. `OrganizationRole.Manager` فقط مدیر یک سازمان است و Platform Admin نیست.
2. شناسه principal فعلی `UserId` است؛ Platform role نیز باید به `UserId` متصل شود، نه به `PersonId` یا `MembershipId`.
3. نقش‌های پلتفرم نباید داخل token قرار گیرند؛ مجوز مؤثر باید در هر درخواست حساس از SQL Server خوانده شود تا revoke فوری باشد.
4. Backoffice یک سطح API مستقل با prefix برابر `/api/admin/v1` خواهد داشت.
5. Platform Owner دسترسی سراسری مدیریتی دارد، اما مجوز blanket برای محتوای گفت‌وگوی خصوصی ندارد.
6. payment provider فعلی `UnavailablePaymentGateway` است؛ عملیات مالی STEP 15D تا اتصال provider واقعی read-only می‌ماند.
7. فناوری MFA هنوز انتخاب نشده است؛ عملیات حساس production باید از طریق abstraction مستقل fail-closed شوند.
8. تصمیم child-safety provider و سیاست دقیق minor برای Group/Channel همچنان unresolved است و STEP 15 حق حدس‌زدن آن را ندارد.

## 3. موجودی معماری و ماژول‌ها

### 3.1 Domain

ماژول‌های دامنه موجود:

- `Identity`: `User`، `Person`، `UserCredential`، `OtpChallenge` و `UserSession`
- `Organization`: `Organization`، `OrganizationPerson`، `Membership`، `RoleAssignment` و `Invitation`
- `Guardian`: رابطه ولی/فرزند
- `Academic`: دوره، درس، کلاس، ثبت‌نام، مدرس، جلسه، برنامه و حضورغیاب
- `Assessment`: تکلیف، submission/evaluation و چرخه کامل Exam
- `Messaging`: گفت‌وگو، participant، پیام، state، privacy، block/report، moderation و safety incident
- `Reports`: job خروجی گزارش
- `Notification`: اعلان، preference، device و delivery attempt
- `Commerce`: Plan/PlanVersion، entitlement/quota، subscription، order/payment attempt/payment
- `Storage`: metadata فایل

Platform IAM و Admin Audit در Domain وجود ندارند.

### 3.2 Application

Use caseهای فعلی از serviceهای صریح استفاده می‌کنند و MediatR/CQRS/Generic Repository وجود ندارد. authorization عمدتاً از طریق `IAccessContextResolver` و storeهای همان ماژول انجام می‌شود. قرارداد مستقل برای موارد زیر وجود ندارد:

- Platform permission resolution
- Platform step-up authorization
- bootstrap اولین Platform Owner
- admin audit query/write
- global organization/user operations
- organization provisioning saga/state machine
- platform moderation queue
- platform commerce/operations read models

### 3.3 Infrastructure

`MakanDbContext` تمام aggregateهای فعلی را با EF Core نگه می‌دارد. schema `platform` اکنون فقط برای durable Outbox نیز استفاده می‌شود؛ وجود این schema به معنی وجود Platform IAM نیست.

زیرساخت‌های فعلی:

- SQL Server از طریق `UseSqlServer`
- opaque session token با hash ذخیره‌شده در DB
- OTP development و MeliPayamak/Unavailable بر اساس configuration
- local/unavailable file storage
- durable general outbox
- SignalR realtime outbox
- notification delivery worker
- report export worker
- payment gateway فقط `UnavailablePaymentGateway`
- content-safety development allow یا production unavailable/fail-closed

### 3.4 API

API فعلی همه در `/api/v1` است. هیچ Controller در `/api/admin/v1` وجود ندارد. Controllerهای مهم فعلی:

- Identity: `AuthController`، `ProfileController`
- Organization: `WorkspacesController`، `InvitationsController`
- Academic/Assessment: Controllerهای periods/courses/classes/sessions/assignments/submissions/evaluations/exams/gradings
- Messaging: `ConversationsController`، `ContactsController`، `PeopleController`، privacy/safety/report/moderation Controllerها
- Reports: `DashboardsController` و `ReportsController`
- Notification: Controllerهای notification/preference/device
- Commerce: `CommerceController`
- Storage: `FilesController`

## 4. ممیزی Identity و Authentication

### 4.1 معنای identityهای موجود

| مفهوم | معنای authoritative | استفاده مجاز در Backoffice |
|---|---|---|
| `UserId` | حساب قابل احراز هویت و subject اصلی session | کلید `PlatformRoleAssignment` و actor اصلی audit |
| `PersonId` | پروفایل انسانی اختیاری متصل به User | نمایش نام در projection/audit؛ نه principal مجوز |
| `UserSession.Id` | session جاری opaque bearer | actor session در audit و revoke session |
| `MembershipId` | عضویت یک User در یک Organization | فقط authorization سازمانی؛ نه Platform IAM |
| `RoleAssignment.Id` | انتساب `OrganizationRole` به Membership | فقط نقش سازمانی |
| `OrganizationPerson.Id` | هویت شخص در مرز یک Organization | subject آموزشی/Guardian؛ نه login identity |

تصمیم: Platform IAM به `UserId` متصل می‌شود. در audit، `PersonId` در صورت وجود به‌عنوان snapshot/reference تکمیلی ذخیره می‌شود.

### 4.2 رفتار فعلی authentication

- OTP در `POST /api/v1/auth/otp/challenges` درخواست و در `POST /api/v1/auth/otp/verify` تأیید می‌شود.
- خروجی verify یک session token opaque است؛ raw token در DB نگهداری نمی‌شود و hash آن در `UserSession` ذخیره می‌شود.
- `SessionAuthenticationHandler` در هر درخواست session را resolve می‌کند.
- claims فعلی فقط `NameIdentifier=UserId` و `makan:session_id` هستند.
- logout session جاری را revoke می‌کند.
- refresh-token جداگانه وجود ندارد.
- MFA/step-up جداگانه وجود ندارد.
- User account status برای `Active/Suspended` وجود ندارد؛ بنابراین authentication فعلی نمی‌تواند کاربر suspended را رد کند.
- role/permission پلتفرم در token یا DB وجود ندارد.

### 4.3 gapهای امنیتی Identity

| نیاز | وضعیت | توضیح | مرحله هدف |
|---|---|---|---|
| Platform role/permission | `MISSING` | هیچ مدل، store، policy یا endpoint ندارد | 15A |
| DB-current permission check | `MISSING` | مجوز پلتفرم وجود ندارد | 15A |
| revoke فوری platform role | `MISSING` | نیازمند query در هر درخواست است | 15A |
| step-up برای عملیات حساس | `MISSING` | فناوری MFA unresolved است | 15A با fail-closed production |
| bootstrap اولین Owner | `MISSING` | grant در startup ممنوع است | 15A |
| جلوگیری از حذف آخرین Owner | `MISSING` | باید race-safe باشد | 15A |
| User Active/Suspended | `MISSING` | status حساب در مدل فعلی نیست | 15B |
| revoke همه sessionهای User | `PARTIAL` | session قابل revoke است، API سراسری ندارد | 15B |
| masked admin user projection | `MISSING` | Backoffice read model ندارد | 15B |

## 5. ممیزی Organization و Provisioning

### 5.1 وضعیت فعلی

- `Organization` حالت `Active/Suspended` دارد و method تعلیق موجود است؛ مسیر reactivation سراسری وجود ندارد.
- `Membership` و `RoleAssignment` lifecycle مستقل و `rowversion` دارند.
- role catalog سازمانی شامل Student/Teacher/Parent/Manager است.
- `Invitation` برای یک `DestinationUserId` موجود ساخته می‌شود و acceptance به‌صورت serializable و idempotent Membership و RoleAssignment می‌سازد.
- API عمومی فقط workspaceهای خود کاربر، انتخاب workspace و invitationهای خود کاربر را ارائه می‌کند.
- API برای create organization، issue invitation، مدیریت membership/role و last-manager protection وجود ندارد.
- provisioning سازمان، subscription و invitation اولیه workflow مجزا و resumable ندارند.

### 5.2 مرزهای الزامی

- `OrganizationRole.Manager != PlatformRole.PlatformOwner`
- Manager فقط در Organization فعال و Membership معتبر خودش اختیار دارد.
- Platform role نباید در `RoleAssignment` سازمانی ذخیره شود.
- Platform Owner نباید برای عملیات عادی سازمانی membership جعلی دریافت کند.
- ساخت Organization، اتصال subscription و دعوت Manager سه مرحله جدا هستند و موفقیت payment معادل provisioning کامل نیست.

## 6. ممیزی Commerce

قابلیت‌های موجود:

- Plan و immutable/published PlanVersion foundation
- entitlement/quota و usage ledger
- Subscription سازمانی
- Order، PaymentAttempt و Payment
- activation از طریق durable Outbox
- API مدیر سازمان برای summary، create/list/get order و initiate payment
- idempotency با `ClientOperationId` در جریان‌های order/payment

gapهای Backoffice:

- CRUD/version lifecycle سراسری Plan در API وجود ندارد.
- read model سراسری subscription/order/payment وجود ندارد.
- عملیات تغییر subscription همراه reason/step-up/admin audit وجود ندارد.
- reconciliation endpoint وجود ندارد.
- refund contract وجود ندارد و نباید حدس زده شود.
- provider واقعی پرداخت متصل نیست؛ `UnavailablePaymentGateway` ایجاد و verify را unavailable می‌کند.

تصمیم: تا provider واقعی و قرارداد آن تصویب نشده، STEP 15D فقط read-only finance/commerce را operational می‌کند و هیچ مسیر «manual mark paid» یا refund نمی‌سازد.

## 7. ممیزی Messaging و Moderation

Moderation فعلی conversation-scoped است:

- route: `/api/v1/conversations/{conversationId}/moderation/cases`
- reviewer باید در همان conversation مجوز مدیریت داشته باشد.
- evidence فقط پس از همان authorization قابل دریافت است.
- case دارای status، priority، assigned reviewer، rowversion و lifecycle review/resolve/dismiss است.
- reported content snapshot و attachment evidence وجود دارند.

این قابلیت جای Platform Safety Queue را نمی‌گیرد. Backoffice به queue سراسری نیاز دارد، اما دسترسی آن باید case-scoped باشد.

مرزهای freeze‌شده:

- هیچ permission با مفهوم `private_chats.read_all` ساخته نمی‌شود.
- `PlatformOwner` نیز blanket access به متن Direct خصوصی ندارد.
- `SecurityAudit` فقط metadata و admin/security audit را می‌بیند و حق enforcement ندارد.
- `AbuseReviewer` فقط caseهای claim‌شده/مجاز و evidence همان case را می‌بیند.
- enforcement نیازمند permission جداگانه، reason و AdminAuditLog است.
- Manager یا Support به علت عنوان نقش، به private conversation یا evidence دسترسی نمی‌گیرند.

## 8. ممیزی Reports، Notifications، Workers، Health و Storage

### Reports

- dashboardهای Student/Parent/Teacher/Manager و detailed reportهای scoped موجودند.
- report export job، CSV exporter، output store و dispatcher موجودند.
- read model سراسری Platform برای education/operations وجود ندارد.

### Notifications

- internal notification، unread count، preferences، push device و delivery attempt موجودند.
- delivery worker با claim/retry محدود وجود دارد.
- Backoffice فقط safe aggregate/count و retry کنترل‌شده برای وضعیت‌های retryable نیاز دارد؛ مشاهده payload حساس blanket مجاز نیست.

### Background processing

- `OutboxDispatcher`
- `NotificationDeliveryDispatcher`
- `ReportExportDispatcher`
- `MessagingRealtimeOutboxDispatcher`

این workerها lease/claim و retry دارند، اما admin operations read model یا API سلامت تفصیلی ندارند.

### Health

- فقط `/health` با `AddHealthChecks()` وجود دارد.
- health check اختصاصی SQL Server، storage، queue depth یا provider status ثبت نشده است.
- STEP 15D فقط وضعیت واقعی را گزارش می‌کند و health مصنوعی یا success جعلی نمی‌سازد.

### Storage

- FileAsset metadata، authorization resolver و local/unavailable provider وجود دارند.
- Backoffice نباید bypass عمومی download ایجاد کند.
- evidence download فقط در scope پرونده moderation مجاز خواهد بود.

## 9. موجودی Persistence و Migration

در baseline، 30 migration تا migration زیر وجود دارد:

`20261013042337_CompleteMessengerV2`

هیچ‌یک از جدول‌های زیر وجود ندارند:

- `platform.PlatformRoles`
- `platform.PlatformPermissions`
- `platform.PlatformRolePermissions`
- `platform.PlatformRoleAssignments`
- `platform.AdminAuditLogs`
- جدول/ستون lifecycle برای provisioning سراسری
- account status برای User

Migrationهای هدف برنامه:

- STEP 15A: `AddPlatformIamAndAdminAudit`
- STEP 15B: `AddPlatformOrganizationUserOperations`
- STEP 15C/15D: فقط در صورت نیاز واقعی مدل؛ نام migration پس از تثبیت مدل انتخاب می‌شود.

## 10. ممیزی تست

baseline شامل این گروه‌هاست:

- `MakanApp.UnitTests`: 306 تست
- `MakanApp.IntegrationTests`: 510 تست
- `MakanApp.ArchitectureTests`: 14 تست
- مجموع: 830 تست

تست مستقیمی برای Platform IAM، `/api/admin/v1`، bootstrap Owner، last-owner، global provisioning، user suspension، Platform Safety Queue یا Backoffice Finance/Ops وجود ندارد؛ چون این قابلیت‌ها هنوز وجود ندارند.

## 11. API Gap Matrix

### 11.1 Platform IAM و Audit

| ID | قرارداد موردنیاز | وضعیت فعلی | شواهد/Gap | مرحله هدف |
|---|---|---|---|---|
| IAM-01 | `GET /api/admin/v1/me` | `MISSING` | فقط `/api/v1/me` برای پروفایل شخصی وجود دارد | 15A |
| IAM-02 | role catalog | `MISSING` | Platform role وجود ندارد | 15A |
| IAM-03 | permission catalog | `MISSING` | authorization policy پلتفرم وجود ندارد | 15A |
| IAM-04 | list role assignments | `MISSING` | فقط Organization RoleAssignment موجود است | 15A |
| IAM-05 | grant/revoke role | `MISSING` | نیازمند reason، step-up و audit | 15A |
| IAM-06 | first-owner bootstrap | `MISSING` | باید out-of-band و one-time باشد | 15A |
| IAM-07 | last-owner protection | `MISSING` | باید concurrency-safe باشد | 15A |
| IAM-08 | append-only admin audit | `MISSING` | Outbox جای audit مدیریتی نیست | 15A |
| IAM-09 | paged/filterable audit query | `MISSING` | allowlist sort/filter لازم است | 15A |
| IAM-10 | production step-up | `MISSING` | provider هنوز unresolved؛ fail-closed | 15A |

### 11.2 Organization، User و Provisioning

| ID | قرارداد موردنیاز | وضعیت فعلی | شواهد/Gap | مرحله هدف |
|---|---|---|---|---|
| ORG-01 | global organization list/detail | `MISSING` | فقط workspaceهای actor | 15B |
| ORG-02 | create organization | `MISSING` | API ندارد | 15B |
| ORG-03 | suspend/reactivate organization | `PARTIAL` | Domain suspend دارد؛ API/policy ندارد | 15B |
| ORG-04 | membership/role management | `MISSING` | invitation acceptance فقط self-service | 15B |
| ORG-05 | last Manager protection | `MISSING` | race-safe guard ندارد | 15B |
| ORG-06 | invite initial Manager | `PARTIAL` | Invitation aggregate هست؛ issuer flow نیست | 15B |
| PRV-01 | durable/resumable provisioning | `MISSING` | state machine ندارد | 15B |
| PRV-02 | idempotent client operation | `MISSING` | provisioning operation id ندارد | 15B |
| PRV-03 | assign published PlanVersion | `PARTIAL` | Commerce مدل دارد، orchestration ندارد | 15B |
| USR-01 | global user list/detail | `MISSING` | فقط self profile/directory safe profile | 15B |
| USR-02 | masked phone | `MISSING` | admin projection ندارد | 15B |
| USR-03 | suspend/reactivate account | `MISSING` | User status ندارد | 15B |
| USR-04 | auth rejects suspended | `MISSING` | session authentication status را ندارد | 15B |
| USR-05 | revoke user sessions | `PARTIAL` | session مدل دارد؛ global endpoint ندارد | 15B |

### 11.3 Education، Messaging و Safety

| ID | قرارداد موردنیاز | وضعیت فعلی | شواهد/Gap | مرحله هدف |
|---|---|---|---|---|
| EDU-01 | global education read models | `MISSING` | همه queryها workspace-scoped هستند | 15C |
| EDU-02 | safe cross-org detail | `MISSING` | Platform authorization ندارد | 15C |
| EDU-03 | mutation via existing domain rules | `PARTIAL` | use caseهای سازمانی موجودند؛ adapter پلتفرم نیست | 15C |
| MSG-01 | safe messaging metadata | `MISSING` | global metadata API ندارد | 15C |
| MSG-02 | private content blanket access | `CONFLICT` | چنین دسترسی نباید ساخته شود | هرگز |
| MOD-01 | global safety queue | `MISSING` | queue فعلی conversation-scoped است | 15C |
| MOD-02 | claim case | `PARTIAL` | start-review scoped موجود است | 15C |
| MOD-03 | resolve/dismiss | `PARTIAL` | scoped موجود است؛ Platform permission/audit ندارد | 15C |
| MOD-04 | case-scoped evidence | `PARTIAL` | scoped موجود است؛ PlatformSafety path ندارد | 15C |
| MOD-05 | enforcement permission | `MISSING` | roleهای پلتفرم وجود ندارند | 15C |

### 11.4 Commerce، Finance و Operations

| ID | قرارداد موردنیاز | وضعیت فعلی | شواهد/Gap | مرحله هدف |
|---|---|---|---|---|
| COM-01 | Plan/PlanVersion admin | `MISSING` | Domain هست، Backoffice API نیست | 15D |
| COM-02 | published version immutability | `IMPLEMENTED` | Domain foundation موجود است | 15D reuse |
| COM-03 | subscription admin | `MISSING` | manager summary فقط خواندنی است | 15D |
| FIN-01 | global orders/payments read | `MISSING` | فقط Organization manager scope | 15D |
| FIN-02 | manual payment success | `CONFLICT` | ممنوع؛ provider authoritative لازم است | هرگز |
| FIN-03 | provider reconciliation | `DEFERRED` | provider فعلی Unavailable است | بعد از provider واقعی |
| FIN-04 | refund | `DEFERRED` | قرارداد محصول/provider ندارد | خارج STEP 15 |
| OPS-01 | safe notification operations | `MISSING` | user endpoints/worker موجود؛ admin API نیست | 15D |
| OPS-02 | retry only retryable jobs | `PARTIAL` | worker retry دارد؛ admin command ندارد | 15D |
| OPS-03 | system health/jobs | `PARTIAL` | `/health` ساده و workerها موجودند | 15D |
| AI-01 | AI operations | `DEFERRED` | integration واقعی وجود ندارد؛ endpoint جعلی ممنوع | خارج STEP 15 |

## 12. ترتیب اجرای مصوب

1. **STEP 15A:** Platform IAM، permission policy، bootstrap Owner، step-up abstraction و AdminAuditLog.
2. **STEP 15B:** global Organization/User operations و provisioning durable.
3. **STEP 15C:** education read models، messaging metadata و Platform Safety queue با evidence case-scoped.
4. **STEP 15D:** Commerce/Finance/Operations read models و mutationهای مجاز؛ payment همچنان provider-bound.
5. **STEP 15E:** acceptance نهایی، ماتریس نقش/مجوز، OpenAPI/handoff و release verdict.

هر مرحله باید مجوزها را از DB جاری resolve کند، reason/audit عملیات حساس را ثبت کند، boundaryهای privacy را حفظ کند و تست concurrency/authorization متناسب داشته باشد.

## 13. موارد عمداً حل‌نشده

- انتخاب فناوری/provider MFA؛ فقط abstraction و fail-closed در 15A مجاز است.
- payment provider واقعی، reconciliation و refund.
- child-safety provider واقعی.
- سیاست نهایی ساخت/عضویت minor در Personal Group/Channel.
- landing page یا هر frontend.
- AI/Copilot integration.
- impersonation/shared admin account.

این موارد نباید در زیرمرحله‌های STEP 15 با رفتار حدسی یا success جعلی پوشانده شوند.
