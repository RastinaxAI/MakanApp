# تصمیم‌های محصول Backoffice پلتفرم Makan

## 1. وضعیت سند

این سند قراردادهای الزام‌آور STEP 15 را freeze می‌کند. تغییر این تصمیم‌ها نیازمند تصمیم صریح محصول/امنیت و به‌روزرسانی مستند است.

## 2. مرز Backoffice

- Backoffice یک سطح مدیریتی پلتفرم با route پایه `/api/admin/v1` است.
- Backoffice از APIهای سازمانی `/api/v1` جداست.
- `OrganizationRole.Manager` هرگز به‌تنهایی Platform Admin نیست.
- `OrganizationRole` و `PlatformRole` دو catalog و دو authorization context مستقل هستند.
- shared admin account و credential مشترک ممنوع است؛ هر اقدام باید actor واقعی داشته باشد.
- Platform assignment به `UserId` متصل است. `PersonId` فقط اطلاعات تکمیلی actor است.
- Platform permission از وضعیت جاری SQL Server resolve می‌شود و token claim منبع authority نیست.
- اصل least privilege و deny-by-default اعمال می‌شود.

## 3. catalog قطعی نقش‌های پلتفرم

کلید نقش‌ها ثابت و case-sensitive در قرارداد داخلی هستند:

| Role key | هدف | مرز قطعی |
|---|---|---|
| `PlatformOwner` | مالک پلتفرم و مدیریت IAM/عملیات سراسری | بدون blanket access به private chat؛ عملیات حساس نیازمند step-up و audit |
| `Operations` | عملیات سازمان، کاربر، provisioning و سلامت سیستم | بدون IAM role management، finance mutation یا private content |
| `Support` | پشتیبانی read-oriented و revoke session | بدون impersonation، تغییر نقش پلتفرم، enforcement یا private content |
| `Finance` | Plan/Subscription/Order/Payment عملیات مجاز | بدون manual payment success، refund حدسی یا دسترسی پیام‌رسان |
| `Content` | نقش رزروشده برای عملیات محتوایی مصوب آینده | تا تعریف resource و policy مجوز عملیاتی دریافت نمی‌کند |
| `SecurityAudit` | مشاهده audit/security و metadata مجاز | read-only؛ بدون enforcement و بدون متن private chat |
| `AbuseReviewer` | claim/review پرونده‌های Safety | evidence فقط در پرونده مجاز؛ بدون enforcement عمومی |

افزودن role جدید خارج از این catalog در STEP 15 ممنوع است مگر با تصمیم محصول جدید.

## 4. catalog مجوزها

مجوزهای قطعی برنامه:

### STEP 15A

- `platform.access`
- `platform.roles.read`
- `platform.roles.manage`
- `platform.audit.read`
- `platform.security.read`

### STEP 15B

- `platform.organizations.read`
- `platform.organizations.manage`
- `platform.users.read`
- `platform.users.manage`
- `platform.sessions.manage`
- `platform.provisioning.read`
- `platform.provisioning.manage`

### STEP 15C

- `platform.education.read`
- `platform.education.manage`
- `platform.messaging.read_metadata`
- `platform.moderation.review`
- `platform.moderation.enforce`

### STEP 15D

- `platform.commerce.read`
- `platform.commerce.manage`
- `platform.finance.read`
- `platform.finance.manage`
- `platform.operations.read`
- `platform.operations.manage`
- `platform.notifications.read`

`platform.private_chats.read_all` یا مجوز هم‌معنا ایجاد نمی‌شود.

## 5. ماتریس نقش و مجوز هدف

علامت `✓` یعنی نقش می‌تواند آن permission را دریافت کند. نبود علامت deny است. `PlatformOwner` catalog کامل را دارد، ولی محدودیت‌های resource-level و privacy همچنان اعمال می‌شوند.

| Permission group | Owner | Operations | Support | Finance | Content | SecurityAudit | AbuseReviewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| access | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| roles.read/manage | ✓ | — | — | — | — | — | — |
| audit.read | ✓ | — | — | — | — | ✓ | — |
| security.read | ✓ | — | — | — | — | ✓ | — |
| organizations.read | ✓ | ✓ | ✓ | ✓ | — | — | — |
| organizations.manage | ✓ | ✓ | — | — | — | — | — |
| users.read | ✓ | ✓ | ✓ | — | — | — | — |
| users.manage | ✓ | ✓ | — | — | — | — | — |
| sessions.manage | ✓ | ✓ | ✓ | — | — | — | — |
| provisioning.read/manage | ✓ | ✓ | read | — | — | — | — |
| education.read/manage | ✓ | ✓ | read | — | — | — | — |
| messaging.read_metadata | ✓ | ✓ | ✓ | — | — | ✓ | ✓ |
| moderation.review | ✓ | — | — | — | — | — | ✓ |
| moderation.enforce | ✓ | — | — | — | — | — | — |
| commerce.read/manage | ✓ | read | — | ✓ | — | — | — |
| finance.read/manage | ✓ | read | — | ✓ | — | — | — |
| operations.read/manage | ✓ | ✓ | read | — | — | read | — |
| notifications.read | ✓ | ✓ | ✓ | — | — | ✓ | — |

توضیح ماتریس:

- واژه `read` یعنی فقط permission خواندنی همان گروه، نه manage.
- `Content` عمداً فقط `platform.access` دارد تا resource contract واقعی در برنامه آینده تصویب شود.
- داشتن permission گروهی هرگز resource-level guard، masking، case scope، step-up یا audit را حذف نمی‌کند.
- mappingها باید seed قطعی و قابل تست باشند؛ تغییر catalog با migration/release کنترل‌شده انجام می‌شود.

## 6. Platform Owner

- اولین Owner فقط با ابزار out-of-band برای یک `UserId` موجود ساخته می‌شود.
- bootstrap فقط وقتی مجاز است که هیچ assignment فعال `PlatformOwner` وجود نداشته باشد.
- bootstrap در startup اجرا نمی‌شود، secret ندارد، idempotent و concurrency-safe است و audit ثبت می‌کند.
- پس از ایجاد اولین Owner، مسیر bootstrap برای grant دوم بسته است.
- grant/revoke Ownerهای بعدی فقط با `platform.roles.manage`، reason معتبر، step-up موفق و audit انجام می‌شود.
- حذف یا suspend آخرین Owner فعال ممنوع و race-safe است.
- global بودن Owner به معنی خواندن unrestricted محتوای خصوصی نیست.

## 7. Step-up و MFA

- عملیات حساس نیازمند `IPlatformStepUpAuthorization` هستند.
- تصمیم مصوب release در GAP-04: TOTP Authenticator عامل اصلی MFA است؛ provider بیرونی، SMS MFA، email OTP و passkey در این مرحله وجود ندارند. این انتخاب محدودیت دائمی معماری نیست.
- تمام سطح محافظت‌شده Backoffice علاوه بر session و permission جاری، MFA همان نشست را می‌خواهد؛ endpointهای محدود enrollment/status/verify برای رفع همین gate از MFA قبلی معاف‌اند ولی Platform access جاری لازم دارند.
- نشست عادی با TOTP معتبر یا recovery code مصرف‌نشده به MFA-assured تبدیل می‌شود؛ عملیات حساس علاوه بر این به step-up تازه، پیش‌فرض قابل تنظیم ۱۰ دقیقه، نیاز دارند.
- secret TOTP فقط با Data Protection محافظت‌شده ذخیره می‌شود؛ key ring پایدار و محافظت‌شده محیط عملیاتی الزامی است. نبود محافظ معتبر، ثبت/تأیید TOTP را fail-closed می‌کند.
- reset فقط برای Owner مجاز با step-up تازه، Reason و audit، روی کاربر پلتفرمی دیگر انجام می‌شود؛ همه assuranceها و recovery codeهای target نامعتبر می‌شوند، بدون تغییر role assignment یا قواعد آخرین Owner.
- fake implementation فقط در محیط `Testing` برای تست قابل استفاده است.
- صرف داشتن OTP login، session تازه، role یا token claim معادل step-up نیست.
- عملیات حساس حداقل شامل role grant/revoke، suspend/reactivate سازمان یا کاربر، enforcement، subscription mutation و عملیات مالی mutation است.

## 8. Admin Audit

- `AdminAuditLog` append-only است؛ update/delete عمومی ندارد.
- هر تلاش حساس موفق یا ناموفق باید در صورت امکان audit شود.
- audit حداقل شامل ActorUserId، ActorPersonId در صورت وجود، ActorSessionId، action، target type/id، OrganizationId در صورت ارتباط، reason، زمان UTC، correlation id، result و summaryهای امن before/after است.
- summary نباید secret، OTP، session token، payment secret، متن private chat یا payload حساس کامل را نگه دارد.
- query audit paginated است و filter/sort فقط allowlist دارد.
- database audit جای application authorization یا observability log را نمی‌گیرد.

## 9. Organization و Provisioning

- create Organization، انتخاب/اتصال published PlanVersion و دعوت initial Manager مراحل جدا هستند.
- provisioning باید durable، resumable و با `ClientOperationId` idempotent باشد.
- failure یک مرحله نباید موفقیت مرحله دیگر را جعل کند.
- paid order به معنی provisioned Organization نیست.
- initial Manager از مسیر invitation ساخته می‌شود؛ role مستقیم پنهانی ممنوع است.
- حذف/تعلیق آخرین Manager فعال یک Organization ممنوع و concurrency-safe است.
- suspend Organization semantics باید پیش از mutation preview شود و reason/step-up/audit داشته باشد.
- reactivation نقش‌ها یا sessionهای قبلاً revoke‌شده را خودکار بازنمی‌گرداند.

## 10. User و Session

- User account status حداقل `Active` و `Suspended` است.
- suspended User نمی‌تواند session جدید بسازد و sessionهای قبلی او باید revoke شوند.
- reactivate کردن User session یا Platform/Organization role قبلی را خودکار restore نمی‌کند.
- phone در Backoffice به‌صورت masked نمایش داده می‌شود مگر قرارداد narrow و مجوز صریح آینده تصویب شود.
- suspend آخرین Owner یا آخرین Manager از مسیر User operation نیز باید رد شود.
- impersonation در STEP 15 وجود ندارد.

## 11. Messaging، Privacy و Moderation

- Backoffice عمومی فقط metadata امن پیام‌رسان را می‌بیند.
- هیچ نقش پلتفرمی، حتی Owner، مجوز unrestricted برای متن private Direct ندارد.
- متن snapshot و file evidence فقط برای پرونده Safety مجاز، reviewer مجاز و در همان case scope قابل مشاهده است.
- `AbuseReviewer` حق claim/review/resolve/dismiss در scope مجاز دارد؛ enforcement عمومی permission جدا می‌خواهد.
- `SecurityAudit` metadata و audit را read-only می‌بیند.
- Manager و Support بر اساس عنوان نقش به evidence دسترسی ندارند.
- هر enforcement نیازمند reason، permission، resource check و AdminAuditLog است.
- child-safety provider واقعی و سیاست minor برای Personal Group/Channel همچنان unresolved هستند و نباید در STEP 15 حدس زده شوند.

## 12. Commerce و Finance

- published `PlanVersion` immutable است؛ قیمت جدید با version جدید منتشر می‌شود.
- واحد پول فعلی IRR است و تغییر آن نیازمند تصمیم جداست.
- subscription mutation نیازمند reason، step-up و audit است.
- هیچ endpoint برای manual payment success ساخته نمی‌شود.
- reconciliation فقط با provider واقعی و response قابل verify مجاز است.
- provider فعلی `Unavailable` است؛ بنابراین مسیرهای نیازمند verification در Backoffice read-only/Unavailable می‌مانند.
- refund تا تصویب قرارداد lifecycle، accounting و provider خارج از STEP 15 است.

## 13. Operations، Notification، Health و AI

- عملیات notification فقط metadata/count امن را نمایش می‌دهد.
- retry دستی فقط برای job/delivery واقعاً retryable و با idempotency/claim safety مجاز است.
- health و job status باید وضعیت واقعی dependency/worker را گزارش کنند؛ success جعلی ممنوع است.
- secret و payload خصوصی در endpointهای عملیاتی نمایش داده نمی‌شود.
- چون AI provider واقعی در repository وجود ندارد، endpoint یا dashboard مصنوعی AI ساخته نمی‌شود.

## 14. Frontend و استقرار

- STEP 15 فقط backend و سند handoff API را شامل می‌شود.
- هیچ frontend repository clone، recreate یا modify نمی‌شود.
- landing page خارج از STEP 15 است.
- bootstrap، migration و seed نباید در startup به‌صورت ناامن اجرا شوند.

## 15. تصمیم‌های باز که مانع حدس‌زدن هستند

موارد زیر freeze نشده‌اند و هر پیاده‌سازی وابسته باید متوقف/محدود شود:

1. روش offline بازیابی آخرین Owner در فقدان TOTP و همه recovery codeها: `DEFERRED_OPERATIONAL_DECISION`؛ backdoor API ممنوع است. TOTP اصلی مصوب است؛ روش‌های MFA آینده در register مربوط به GAP-04 ثبت می‌شوند.
2. payment provider، reconciliation callback و refund lifecycle.
3. child-safety provider واقعی.
4. سیاست دقیق minor برای ساخت/عضویت Personal Group و Channel.
5. scope و resourceهای واقعی نقش `Content`.
6. هر نوع impersonation یا break-glass private-content access.

تا زمان تصمیم صریح، رفتار امن به‌ترتیب fail-closed، read-only یا عدم ایجاد endpoint است.
