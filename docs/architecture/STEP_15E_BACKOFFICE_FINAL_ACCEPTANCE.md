# STEP 15E — پذیرش نهایی امنیت و قراردادهای Backoffice

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## نتیجه اجرایی

مبنای ممیزی کد واقعی `main@eebf02c` در تاریخ `2026-10-13` است. verdict نهایی:

```text
READY_FOR_STAGING_WITH_BLOCKERS
```

Backoffice برای آزمون staging، read modelها و سناریوهای کنترل‌شده‌ای که provider خارجی نمی‌خواهند قابل استفاده است. برای Production کامل آماده نیست، زیرا:

1. `UnavailablePlatformStepUpAuthorization` عمداً تمام عملیات نیازمند step-up را fail-closed می‌کند؛ MFA/re-auth provider مصوب وجود ندارد.
2. `UnavailablePaymentGateway` provider واقعی پرداخت، verification/reconciliation و refund ندارد.
3. `UnavailableContentSafetyProvider` در Production مسیرهای child-sensitive را fail-closed می‌کند؛ provider واقعی Child Safety متصل نیست.
4. AI integration و telemetry وجود ندارد؛ فقط وضعیت صادقانه‌ی `NotIntegrated/DependencyPending` گزارش می‌شود.
5. full production infrastructure hardening شامل trusted proxy، CORS نهایی، secret store، monitoring بیرونی، backup/restore و deployment gate در STEP 16 باقی می‌ماند.

## بازبینی ماتریس STEP 15-0

هیچ مورد `CONFLICTING` توضیح‌نداده‌ای باقی نمانده است.

| IDها | وضعیت نهایی | شاهد واقعی |
|---|---|---|
| IAM-01 تا IAM-09 | `IMPLEMENTED` | `/me`، role catalog/assignment، grant/revoke، bootstrap، last-owner و audit append-only |
| IAM-10 | `PARTIAL` | port و fail-closed guard وجود دارد؛ provider Production وجود ندارد |
| ORG-01 تا ORG-06 | `IMPLEMENTED` | فهرست/detail، create، suspend/reactivate، membership/role، last-manager و دعوت Manager |
| PRV-01 تا PRV-03 | `IMPLEMENTED` | state پایدار provisioning، idempotency و اتصال PlanVersion منتشرشده |
| USR-01 تا USR-05 | `IMPLEMENTED` | global user، phone mask، suspend/reactivate، رد auth و revoke نشست |
| EDU-01 و EDU-02 | `IMPLEMENTED` | read modelهای global، context سازمان و detail امن |
| EDU-03 | `DEFERRED` | permission مدیریت freeze شده، اما use case mutation سراسری امن و مصوب وجود ندارد |
| MSG-01 | `IMPLEMENTED` | metadata امن و صفحه‌بندی‌شده |
| MSG-02 | `IMPLEMENTED` به‌عنوان boundary | blanket private-content API عمداً ساخته نشده است |
| MOD-01 تا MOD-05 | `IMPLEMENTED` | PlatformSafety queue، claim، resolve/dismiss، evidence case-scoped و enforcement جدا |
| COM-01 تا COM-03 | `IMPLEMENTED` | Plan/Version، immutability، Subscription read/override و حفظ تاریخچه |
| FIN-01 | `IMPLEMENTED` | global order/payment read با reference mask‌شده |
| FIN-02 | `IMPLEMENTED` به‌عنوان منع | هیچ manual payment success route وجود ندارد |
| FIN-03 | `DEFERRED` | provider واقعی برای reconciliation وجود ندارد |
| FIN-04 | `DEFERRED` | قرارداد محصول/provider برای refund تصویب نشده است |
| OPS-01 | `IMPLEMENTED` | شمارنده و failure metadata امن اعلان |
| OPS-02 | `PARTIAL` | retry خودکار worker موجود است؛ manual retry امن برای failure نهایی وجود ندارد |
| OPS-03 | `IMPLEMENTED` | health و job summary محافظت‌شده |
| AI-01 | `DEFERRED` | integration واقعی وجود ندارد و endpoint جعلی ساخته نشده است |

## معماری Platform IAM

`PlatformRole` و `PlatformRoleAssignment` از `OrganizationRole` و `RoleAssignment` جدا هستند. permission handler برای هر درخواست `/api/admin/v1` assignment و permission فعال را از SQL Server می‌خواند؛ revoke بدون صدور token جدید مؤثر می‌شود. `PlatformOwner` نیز privacy/resource ruleها را bypass نمی‌کند.

```text
Bearer UserSession
-> ASP.NET Core Authorization Policy
-> database-backed PlatformPermissionHandler
-> thin Controller
-> Application Service
-> Domain rule / Application port
-> EF Core + SQL Server
-> AdminAuditLog for mutation
```

bootstrap اولین Owner out-of-band، idempotent و با قفل SQL Server انجام می‌شود. پس از اولین Owner فعال بسته است. grant/revoke نقش و تعلیق Owner با guard آخرین Owner محافظت می‌شوند.

## catalog نقش‌ها

| نقش | مسئولیت نهایی |
|---|---|
| `PlatformOwner` | مالک پلتفرم و catalog کامل permissionها؛ بدون blanket private-data access |
| `Operations` | عملیات سازمان/کاربر/provisioning/education و read امن Commerce/Finance/Ops |
| `Support` | read امن سازمان/کاربر/education/metadata/Ops و revoke نشست |
| `Finance` | مدیریت Plan/Subscription و read مالی؛ بدون User/Safety/Ops management |
| `Content` | فقط access پایه؛ قابلیت محتوایی عمومی هنوز مصوب نشده است |
| `SecurityAudit` | audit/security و metadata/Ops امن؛ بدون mutation |
| `AbuseReviewer` | بررسی پرونده PlatformSafety؛ بدون enforcement یا امور سازمان/مالی |

## ماتریس نهایی permission

علامت ✓ یعنی mapping seed شده است؛ نبود علامت deny است.

| Permission | Owner | Operations | Support | Finance | Content | SecurityAudit | AbuseReviewer |
|---|---:|---:|---:|---:|---:|---:|---:|
| `platform.access` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `platform.roles.read` | ✓ |  |  |  |  |  |  |
| `platform.roles.manage` | ✓ |  |  |  |  |  |  |
| `platform.audit.read` | ✓ |  |  |  |  | ✓ |  |
| `platform.security.read` | ✓ |  |  |  |  | ✓ |  |
| `platform.organizations.read` | ✓ | ✓ | ✓ | ✓ |  |  |  |
| `platform.organizations.manage` | ✓ | ✓ |  |  |  |  |  |
| `platform.users.read` | ✓ | ✓ | ✓ |  |  |  |  |
| `platform.users.manage` | ✓ | ✓ |  |  |  |  |  |
| `platform.sessions.manage` | ✓ | ✓ | ✓ |  |  |  |  |
| `platform.provisioning.read` | ✓ | ✓ | ✓ |  |  |  |  |
| `platform.provisioning.manage` | ✓ | ✓ |  |  |  |  |  |
| `platform.education.read` | ✓ | ✓ | ✓ |  |  |  |  |
| `platform.education.manage` | ✓ | ✓ |  |  |  |  |  |
| `platform.messaging.read_metadata` | ✓ | ✓ | ✓ |  |  | ✓ | ✓ |
| `platform.moderation.review` | ✓ |  |  |  |  |  | ✓ |
| `platform.moderation.enforce` | ✓ |  |  |  |  |  |  |
| `platform.commerce.read` | ✓ | ✓ |  | ✓ |  |  |  |
| `platform.commerce.manage` | ✓ |  |  | ✓ |  |  |  |
| `platform.finance.read` | ✓ | ✓ |  | ✓ |  |  |  |
| `platform.finance.manage` | ✓ |  |  | ✓ |  |  |  |
| `platform.operations.read` | ✓ | ✓ | ✓ |  |  | ✓ |  |
| `platform.operations.manage` | ✓ | ✓ |  |  |  |  |  |
| `platform.notifications.read` | ✓ | ✓ | ✓ |  |  | ✓ |  |
| `platform.ai.read` | ✓ | ✓ |  |  |  |  |  |

وجود manage permission بدون endpoint به معنی قابلیت اجرا نیست. `platform.finance.manage`، `platform.operations.manage` و `platform.ai.read` قرارداد IAM را freeze می‌کنند، اما موفقیت پرداخت، rerun job و AI endpoint بدون use case/provider واقعی ساخته نشده‌اند.

## inventory نهایی API

### IAM و Audit

- `GET /api/admin/v1/me`
- `GET /api/admin/v1/roles`
- `GET|POST /api/admin/v1/role-assignments`
- `DELETE /api/admin/v1/role-assignments/{assignmentId}`
- `GET /api/admin/v1/audit-logs`

### Organization، User و Provisioning

- `GET|POST /api/admin/v1/organizations`
- `GET /api/admin/v1/organizations/{organizationId}`
- `GET /api/admin/v1/organizations/{organizationId}/suspension-impact`
- `POST /api/admin/v1/organizations/{organizationId}/suspend`
- `POST /api/admin/v1/organizations/{organizationId}/reactivate`
- `GET /api/admin/v1/organizations/{organizationId}/memberships`
- `POST /api/admin/v1/organizations/{organizationId}/invitations`
- `PUT /api/admin/v1/organizations/{organizationId}/memberships/{membershipId}/role`
- `DELETE /api/admin/v1/organizations/{organizationId}/memberships/{membershipId}`
- `GET|POST /api/admin/v1/provisioning`
- `GET /api/admin/v1/users`
- `GET /api/admin/v1/users/{userId}`
- `POST /api/admin/v1/users/{userId}/suspend`
- `POST /api/admin/v1/users/{userId}/reactivate`
- `POST /api/admin/v1/users/{userId}/sessions/revoke`
- `POST /api/admin/v1/users/{userId}/sessions/{sessionId}/revoke`

### Education، Messaging و Safety

- `GET /api/admin/v1/education/classes`
- `GET /api/admin/v1/education/classes/{classId}`
- `GET /api/admin/v1/education/teachers`
- `GET /api/admin/v1/education/enrollments`
- `GET /api/admin/v1/education/assignments`
- `GET /api/admin/v1/education/exams`
- `GET /api/admin/v1/messaging/conversations`
- `GET /api/admin/v1/moderation/cases`
- `GET /api/admin/v1/moderation/cases/{caseId}`
- `POST /api/admin/v1/moderation/cases/{caseId}/start-review`
- `POST /api/admin/v1/moderation/cases/{caseId}/resolve`
- `POST /api/admin/v1/moderation/cases/{caseId}/dismiss`
- `POST /api/admin/v1/moderation/cases/{caseId}/enforce-content-removal`
- `GET /api/admin/v1/moderation/cases/{caseId}/evidence/files/{fileAssetId}`

### Commerce، Finance و Operations

- `GET|POST /api/admin/v1/commerce/plans`
- `GET /api/admin/v1/commerce/plans/{planId}`
- `POST /api/admin/v1/commerce/plans/{planId}/versions`
- `POST /api/admin/v1/commerce/plan-versions/{planVersionId}/publish`
- `GET /api/admin/v1/commerce/subscriptions`
- `POST /api/admin/v1/commerce/organizations/{organizationId}/subscription-override`
- `GET /api/admin/v1/finance/orders`
- `GET /api/admin/v1/finance/orders/{orderId}`
- `GET /api/admin/v1/operations/notifications`
- `GET /api/admin/v1/operations/jobs`
- `GET /api/admin/v1/operations/health`

OpenAPI از `/openapi/v1.json` ارائه می‌شود و تست پذیرش وجود مسیرهای اصلی و نبود surfaceهای ممنوع را بررسی می‌کند.

## امنیت، حریم خصوصی و cross-organization

- user عادی و Manager سازمانی پیش از lookup منبع با `403` از Backoffice رد می‌شوند.
- Platform readهای global فقط با permission صریح مجازند و هر ردیف context سازمان دارد.
- تغییر نقش Membership با ترکیب `OrganizationId + MembershipId` انجام می‌شود و سازمان دیگر را mutate نمی‌کند.
- آخرین Manager در پایان role/membership و suspend User با transaction و قفل محافظت می‌شود؛ تست رقابت صفر Manager را رد می‌کند.
- شماره تلفن در User projection mask می‌شود.
- messaging metadata فاقد متن پیام، participant identity، attachment و Direct pair است.
- evidence فقط از case مجاز و attachment متصل به همان case قابل دانلود است.
- provider reference مالی mask و Payment URL از read model حذف شده است.
- Notification Ops مقصد، شماره، token، title/body و provider message id را برنمی‌گرداند.
- هیچ AI prompt/response ذخیره یا نمایش داده نمی‌شود، چون integration وجود ندارد.
- AdminAudit شامل actor/session/action/target/reason/correlation و خلاصه‌ی محدود before/after است؛ API update/delete ندارد و DbContext حذف audit را رد می‌کند.

## integrity و concurrency

- last Owner، last Manager، شماره PlanVersion، membership mutation، provisioning و subscription override در transactionهای کوتاه و قفل‌های SQL Server محافظت می‌شوند.
- `rowversion` برای mutationهای version-sensitive استفاده می‌شود.
- provisioning با `ClientOperationId` و payload hash retry-safe است.
- PlanVersion منتشرشده immutable است.
- subscription override رکورد قبلی را لغو و رکورد جدید می‌سازد؛ downgrade داده آموزشی یا فایل را حذف نمی‌کند.
- موفقیت Payment فقط از verification provider و قواعد Domain می‌آید؛ Backoffice route دستی ندارد.

## ممیزی performance

- تمام listهای admin pagination سمت server و سقف page size دارند.
- sort ورودی فقط در queryهایی پذیرفته می‌شود که allowlist صریح دارند؛ سایر فهرست‌ها ترتیب ثابت server دارند.
- readها از projection و `AsNoTracking` استفاده می‌کنند.
- read modelهای Plan detail، Subscription page و Order page در STEP 15E به queryهای batch تبدیل شدند و N+1 وابسته به تعداد ردیف ندارند.
- خروجی notification failure به ۵۰ مورد محدود است.
- دو detail expansion هنوز برای Production بزرگ نیازمند pagination مستقل‌اند: history نسخه‌های یک Plan و memberهای فعال Class detail. این مورد blocker staging نیست و باید در STEP 16 یا یک hardening کوچک پیش از بار Production حل شود.

## شواهد تست پذیرش

| حوزه | تست‌های اجرایی شاخص |
|---|---|
| platform auth و revoke فوری | `AnonymousAndOrganizationManagerCannotAccessBackofficeButOwnerCan`, `OrganizationManagerNeverBecomesPlatformAdminAndRevocationTakesEffectImmediately` |
| bootstrap و last Owner | `BootstrapIsConcurrentSafeIdempotentAndClosesAfterFirstOwner`, `ConcurrentRevocationsCannotRemoveLastPlatformOwner` |
| last Manager/concurrency | `ConcurrentMembershipEndsCannotRemoveAllManagersAndFinalManagerCannotBeSuspended` |
| provisioning E2E | `ProvisioningRetryCreatesOrganizationSubscriptionAndManagerInvitationExactlyOnce` |
| cross-org و global user | `MembershipRoleChangeTargetsOnlyExplicitOrganization`, `OrganizationAndUserReadModelsAreGlobalPaginatedAndMaskPhone`, `HostileResourceIdentifiersRemainBehindPlatformAuthorization` |
| moderation/privacy | `PlatformOwnerDoesNotBypassMessengerContactsOrModerationPrivacy`, `PlatformModerationIsCaseScopedAuditedAndEnforcementRequiresSeparatePermission` |
| finance/commerce | چهار تست `PlatformCommerceOperationsEndpointsTests` |
| system ops/audit | `OperationsExposeSafeNotificationAndHonestHealthStateWithoutAiEndpoint`, `OperationalAndAuditContractsAreProtectedAndDoNotExposeCredentials` |
| OpenAPI | `OpenApiContainsFinalBackofficeContractWithoutForbiddenSurfaces` |

## جداسازی Landing

این backend هیچ Landing CMS، Leads، Blog، FAQ CMS، FastAPI یا PostgreSQL دریافت نکرده است. Landing Admin یک product/deployment جداست و نباید از Platform IAM یا `/api/admin/v1` به‌عنوان مجوز ضمنی برای merge شدن دو سامانه استفاده کند.

## موارد deferred و تصمیم‌نشده

- provider و UX واقعی MFA/step-up
- provider پرداخت، callback/reconciliation و refund
- provider واقعی Child Safety و policy نگه‌داری/appeal/SLA
- AI/Copilot integration، data governance و telemetry
- mutation سراسری Education بدون دورزدن Domain use case
- manual retry امن برای notification/job failure نهایی
- قابلیت عملیاتی مشخص برای نقش `Content`
- impersonation و shared admin account عمداً ممنوع‌اند

## STEP 16 handoff

STEP 16 باید full production/security/deployment hardening را جداگانه انجام دهد:

1. انتخاب و اتصال MFA provider و ثبت assurance در session؛
2. payment provider واقعی با callback verification، reconciliation و refund contract مصوب؛
3. Child Safety provider واقعی، retention/appeal/SLA؛
4. secret store، least-privilege SQL principal، migration principal جدا و rotation؛
5. trusted proxy/forwarded headers، CORS allowlist، TLS و security headers؛
6. metrics/tracing/alerting بیرونی، queue lag و provider SLO؛
7. backup/restore drill برای SQL Server و file storage؛
8. load/performance test و pagination برای nested detail collectionها؛
9. deployment gates، rollback و smoke test staging/production.

AI STEP 10 نباید با STEP 16 یا Backoffice مخلوط شود. تا زمان تعریف provider، consent/data policy، retention و telemetry، وضعیت authoritative همان `NotIntegrated/DependencyPending` است.
