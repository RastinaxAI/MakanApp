# GAP-01 — پرونده سازمانی، اتصال رضایتی حساب و چرخه سرپرستی

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

تاریخ: ۲۰۲۶-۱۰-۱۵. دامنه: Backend؛ .NET 9، SQL Server، Onion Architecture در Modular Monolith. این سند فقط قابلیت پیاده‌شده GAP-01 را توضیح می‌دهد؛ اثبات استقرار Production یا ارسال واقعی پیامک نیست.

## مسئله و مرز مدل‌ها

قبل از این تغییر، `OrganizationPerson` و `GuardianRelation`، mapping، migration و قواعد دسترسی والد وجود داشتند، اما ایجاد/مدیریت عملیاتی آن‌ها از API مدرسه ممکن نبود. اکنون مدیر مجاز می‌تواند پرونده بسازد، جست‌وجو و ویرایش مجاز کند، دعوت اتصال بسازد و رابطه والد را مدیریت کند.

| مفهوم | مسئولیت و مرز |
|---|---|
| `Person` | نام جهانی شخص؛ ممکن است هنوز هیچ حسابی نداشته باشد |
| `User` | حساب و هویت ورود؛ مالک تغییر پروفایل حساب‌دار است |
| `OrganizationPerson` | پرونده شخص در یک سازمان؛ داشتن آن حساب/نقش/ثبت‌نام نمی‌سازد |
| `Invitation` | دعوت به حساب موجود؛ تحویل دعوت، پذیرش دعوت و اتصال پرونده مفاهیم جدا هستند |
| `Membership` / `RoleAssignment` | عضویت و نقش سازمانی؛ از جریان پذیرش دعوت موجود ایجاد یا بازاستفاده می‌شوند |
| `Enrollment` | ثبت‌نام در کلاس؛ فقط جریان Academic با کنترل ظرفیت آن را ایجاد می‌کند |
| `GuardianRelation` | مجوز رابطه والد/فراگیر در سازمان مشخص؛ تنها وضعیت معتبر و فعال مجوز خواندن می‌دهد |

ایجاد پرونده، یک `Person` جدید و یک `OrganizationPerson` فعال می‌سازد؛ هیچ `User`، `Credential`، `Membership`، `RoleAssignment`، `Enrollment` یا `TeacherAssignment` ضمنی ایجاد نمی‌شود. تطبیق بر اساس نام، تلفن مشترک خانوادگی یا شباهت اطلاعات وجود ندارد؛ دو نام برابر با دو شناسه عملیات متفاوت می‌توانند دو پرونده مستقل باشند.

## مالکیت نام جهانی

`OrganizationPersonService.UpdateAsync` فقط `FirstName`، `LastName` و `DisplayName` موجود روی `Person` را ویرایش می‌کند؛ مشروط به همه موارد زیر:

- پرونده فعال و در سازمان جاری مدیر باشد.
- هیچ `User` به `Person` متصل نباشد؛ حساب غیرفعال نیز این محافظ را رفع نمی‌کند.
- دقیقاً یک `OrganizationPerson` به شخص اشاره کند؛ ارجاع سازمان دیگر و پرونده پایان‌یافته نیز شمرده می‌شوند.
- هر دو نسخه `OrganizationPerson.RowVersion` و `Person.RowVersion` معتبر و مطابق باشند.

برای شخص حساب‌دار یا مشترک، پاسخ `403 ORGANIZATION_PERSON_NAMES_PROTECTED` است. مالک حساب باید از `PATCH /api/v1/me/profile` موجود استفاده کند. فیلدهای امنیتی، نام کاربری، تلفن تأییدشده، نقش، عضویت، ثبت‌نام و رابطه والد اصلاً در DTO ویرایش پرونده نیستند. `canEditNames` تنها راهنمای UI است؛ مجوز نهایی هنگام نوشتن دوباره بررسی می‌شود.

## ایجاد، جست‌وجو و پایان پرونده

`CreateOrganizationPersonCommand` دارای `ClientOperationId` است. hash نام‌های normalizeشده با SHA-256 ذخیره می‌شود؛ همان سازمان/عملیات/محتوا پاسخ بازپخش می‌دهد و همان عملیات با محتوای دیگر `409` می‌شود. این hash شناسه شخص یا ابزار تطبیق اشخاص نیست؛ دلیل Audit جزو fingerprint داده ایجاد نیست.

نام و نام خانوادگی پس از trim الزامی و حداکثر ۱۰۰ نویسه‌اند؛ نام نمایشی اختیاری و حداکثر ۲۰۰؛ دلیل عملیات الزامی و حداکثر ۵۰۰. لیست فقط سازمان جاری، با جست‌وجوی نام/نام خانوادگی/نام نمایشی، `page=1..100000` و `pageSize=1..100` است؛ پیش‌فرض اندازه ۵۰. ترتیب نام خانوادگی، نام و سپس Id است. لیست Guardian نیز به زمان ایجاد نزولی و سپس Id مرتب می‌شود. `totalCount` و صفحه، snapshot تراکنشیِ مشترک برای تمام صفحه‌ها نیستند.

پایان پرونده، `Active → Ended` موجود را به کار می‌برد. اگر Enrollment فعال دارد `409 ORGANIZATION_PERSON_IN_USE` برمی‌گردد؛ ابتدا ثبت‌نام از مسیر Academic پایان یابد. پایان، حذف فیزیکی، لغو Membership یا حذف GuardianRelation نیست. واکشی بعدی والد به علت غیرفعال شدن learner رد می‌شود. API بازفعال‌سازی/حذف پرونده در این مرحله وجود ندارد.

## اتصال حساب: رضایت صریح، نه merge

۱. مدیر پرونده فعالِ بدون حساب را انتخاب می‌کند و دعوتی با `DestinationUserId` حساب **موجود و فعال**، شناسه عملیات، زمان انقضای UTC در ۳۰ روز آینده و نسخه پرونده می‌سازد.

۲. دعوت همان `Invitation` موجود است و به سازمان، پرونده و مقصد متصل است. این مسیر learner-oriented است: نقش دعوت ثابت `Student` است؛ نقش دلخواه از ورودی دریافت نمی‌شود. نقش Parent/Teacher/Manager از جریان‌های مجاز قبلی می‌آید، نه از این API.

۳. مقصد پس از ورود، `GET /api/v1/invitations` را می‌خواند؛ فیلدهای افزوده `organizationPersonId`، `organizationPersonDisplayName` و `requiresPersonLinkConsent` امکان نمایش تأیید آگاهانه را می‌دهند.

۴. فقط همان کاربر، با نشست معتبر و پروفایل کامل، درخواست زیر را می‌فرستد:

```http
POST /api/v1/invitations/{invitationId}/accept
Authorization: Bearer <accessToken>
Content-Type: application/json

{"confirmPersonLink":true}
```

۵. `OrganizationService.AcceptInvitationAsync` سازمان/دعوت/مقصد/نشست را دوباره اعتبارسنجی می‌کند. `EfOrganizationStore.CompletePersonLinkAsync` فقط `OrganizationPerson.PersonId` را به `PersonId` **موجودِ حسابِ رضایت‌دهنده** متصل می‌کند. User، Person حساب، Person اولیه مدرسه و Credential تغییر یا حذف نمی‌شوند؛ Person اولیه محفوظ است و شناسه قبل/بعد در Audit ثبت می‌شود. این تغییر مرجع، ادغام اشخاص نیست؛ Id پرونده و FK تمام تاریخچه آموزشی ثابت می‌مانند. نام جاری پرونده پس از اتصال از پروفایل مالک خوانده می‌شود.

۶. در **همان تراکنش**، جریان قبلی دعوت Membership فعال و نقش Student را ایجاد یا بازاستفاده می‌کند و دعوت را Accepted می‌کند. ثبت‌نام یا TeacherAssignment ایجاد نمی‌شود. پاسخ پذیرش، `organizationPersonId` اختیاری را نیز دارد.

وجود حساب دیگر برای Person پرونده یا یک پرونده فعال دیگر برای Person حساب در همان سازمان، `409` است؛ ادغام/انتقال خودکار انجام نمی‌شود. یک حساب می‌تواند با دو پذیرش مستقل به دو پرونده در دو سازمان متصل شود؛ Id پرونده‌ها مستقل می‌مانند. دعوت عادی قبلی، بدون بدنه همچنان پذیرفته می‌شود؛ بدنه فقط برای دعوت دارای اتصال باید صریحاً true باشد. پروفایل ناقص `409 ACCOUNT_PROFILE_REQUIRED` است و پس از تکمیل از مسیر Identity می‌توان مجدداً پذیرفت.

ساخت دعوت یا پذیرش هم‌زمان، اثر کسب‌وکاری تکراری نمی‌سازد. بازپخش Accepted اتصال موجود را بررسی می‌کند و Audit دوم نمی‌سازد. ساخت دوباره با همان شناسه عملیات و مقصد/انقضای متفاوت `409` می‌دهد. برای یک پرونده تنها یک دعوت Pending اتصال وجود دارد؛ درخواست جدید هم‌مقصد می‌تواند همان دعوت را برگرداند. Pending منقضی پیش از جایگزینی، در همان تراکنش به Expired تبدیل می‌شود. قید قدیمی دعوت Pending برای مقصد/سازمان/نقش نیز برقرار است؛ دعوت عادیِ Pending متعارض باید از جریان قبلی تعیین تکلیف شود، نه اینکه بی‌صدا به دعوت اتصال تبدیل شود.

مدیر می‌تواند دعوت Pending اتصال را revoke کند. مقصد همچنان decline موجود را دارد. دعوت revoked/expired با `410` و declined طبق سیاست قبلی concealment با `404` قابل پذیرش نیست. این مرحله delivery اعلان/SMS جدید یا جست‌وجوی عمومی حساب‌ها پیاده نمی‌کند؛ مدیر باید شناسه مقصد را از جریان مجاز موجود در اختیار داشته باشد. تلفن در این قرارداد دریافت نمی‌شود.

## رابطه والد و فراگیر

`GuardianManagementService` از وضعیت‌های **موجود** `Pending / Active / Ended / Revoked` استفاده می‌کند:

- مدیر جاری مدرسه، learner فعال همان سازمان و حساب فعال با Membership و RoleAssignment فعال **Parent در همان سازمان** را انتخاب می‌کند. نداشتن این پیش‌شرط، `404` می‌دهد. ساخت رابطه مجوز Parent ایجاد نمی‌کند.
- ایجاد همیشه Pending است؛ انتخاب فرزند و اطلاعات آموزشی تا activation مجاز نیست.
- فعال‌سازی فقط Pending مجاز است؛ وضعیت فعلی learner و حساب/عضویت/نقش والد دوباره بررسی می‌شوند. این اقدام تأیید مدرسه پس از بررسی انسانی رابطه است؛ سیستم از نام خانوادگی، تلفن یا ادعای والد نسبت خانوادگی استنتاج نمی‌کند.
- End و Revoke رابطه را غیرمجاز می‌کنند، `EndedAtUtc` می‌گذارند و تاریخچه را حذف نمی‌کنند. تکرار activation یا پایانِ انجام‌شده، پاسخ فعلی با `replayed=true` دارد؛ حالت terminal دوباره activate نمی‌شود. بعد از terminal می‌توان رابطه Pending جدید ساخت که باز هم نیازمند activation است.
- `GuardianAccessContextResolver` موجود در درخواست بعدی dashboard/report/داده فرزند، relation و سازمان و learner فعال را recheck می‌کند؛ بعد از commit پایان/لغو، درخواست جدید با انتخاب قبلی `403` می‌گیرد و summary غیرمجاز `404` است. پاسخ در حال اجرای قبل از commit باطل‌سازی جادویی نمی‌شود.
- انتخاب Child A فقط داده A را می‌دهد؛ رابطه B مجوز خواندن A نیست. هیچ مجوز جدیدی برای خواندن چت خصوصی، مخاطبان یا داده پیام‌رسان کودک افزوده نشده است.

Parent از `GuardianController` موجود برای children/select/summary استفاده می‌کند، نه API مدیریت مدرسه. School Manager با Platform Admin برابر نیست؛ داشتن IAM پلتفرم جایگزین workspace سازمانی/عضویت معتبر/نقش فعال Manager نیست. عملیات جدیدی به Backoffice واگذار نشده است.

## Endpointها و قراردادها

تمام مسیرهای جدید `[Authorize]` هستند و از `SchoolManagementAccessResolver`، عضویت جاری و نقش فعال Manager سازمان جاری استفاده می‌کنند. هیچ `organizationId` ورودی محدوده نوشتن را تعیین نمی‌کند. تمام writeها `reason` دارند؛ readهای scoped برای شناسه سازمان دیگر `404` می‌دهند.

| Method | Route زیر `/api/v1` | ورودی / پاسخ موفق |
|---|---|---|
| POST | `/organization/people` | CreateOrganizationPersonCommand → OrganizationPersonDto؛ 201 یا 200 بازپخش |
| GET | `/organization/people` | search/page/pageSize → OrganizationPeoplePage؛ 200 |
| GET | `/organization/people/{id}` | OrganizationPersonDto؛ 200 |
| PATCH | `/organization/people/{id}` | UpdateOrganizationPersonNamesCommand → OrganizationPersonDto؛ 200 |
| POST | `/organization/people/{id}/end` | OnboardingStateCommand → OrganizationPersonDto؛ 200 |
| POST | `/organization/people/{id}/invitations` | CreatePersonLinkInvitationCommand → PersonLinkInvitationDto؛ 201 یا 200 |
| POST | `/organization/people/{id}/invitations/{invitationId}/revoke` | OnboardingStateCommand → PersonLinkInvitationDto؛ 200 |
| POST | `/organization/guardian-relations` | CreateSchoolGuardianRelationCommand → SchoolGuardianRelationDto؛ 201 یا 200 |
| GET | `/organization/guardian-relations` | learnerId/page/pageSize → SchoolGuardianRelationsPage؛ 200 |
| GET | `/organization/guardian-relations/{id}` | SchoolGuardianRelationDto؛ 200 |
| POST | `/organization/guardian-relations/{id}/activate` | OnboardingStateCommand → SchoolGuardianRelationDto؛ 200 |
| POST | `/organization/guardian-relations/{id}/end` | OnboardingStateCommand → SchoolGuardianRelationDto؛ 200 |
| POST | `/organization/guardian-relations/{id}/revoke` | OnboardingStateCommand → SchoolGuardianRelationDto؛ 200 |
| POST | `/invitations/{invitationId}/accept` | مسیر موجود؛ AcceptInvitationCommand اختیاری برای دعوت عادی، تأیید الزامی برای اتصال؛ AcceptInvitationResult، 200 |

DTO پرونده: `id, organizationId, personId, firstName, lastName, displayName, status, isAccountLinked, canEditNames, rowVersion, personRowVersion, replayed`.

DTO دعوت اتصال: `id, organizationPersonId, destinationUserId, role, status, expiresAtUtc, rowVersion, replayed`.

DTO رابطه مدرسه: `id, organizationId, guardianUserId, learnerOrganizationPersonId, status, validFromUtc, endedAtUtc, rowVersion, replayed`.

صفحه‌ها: `dataAsOfUtc, page, pageSize, totalCount, items`. Enumها در JSON نام رشته‌ای دارند. OpenAPI جاری در Development از Controller/DTOها تولید می‌شود؛ تست وجود pathها و نبود فیلدهای mutation هویت/مجوز را بررسی می‌کند.

### نمونه‌ها

شناسه‌ها و نسخه‌های زیر نمونه‌اند؛ GUID واقعی از پاسخ مرحله قبل، rowversion تازه و زمان انقضای آینده جایگزین شوند.

```json
{
  "clientOperationId": "11111111-1111-4111-8111-111111111111",
  "firstName": "سارا",
  "lastName": "نمونه",
  "displayName": "سارا نمونه",
  "reason": "تشکیل پرونده آموزشی"
}
```

پاسخ نمونه ایجاد:

```json
{
  "id": "22222222-2222-4222-8222-222222222222",
  "organizationId": "33333333-3333-4333-8333-333333333333",
  "personId": "44444444-4444-4444-8444-444444444444",
  "firstName": "سارا",
  "lastName": "نمونه",
  "displayName": "سارا نمونه",
  "status": "Active",
  "isAccountLinked": false,
  "canEditNames": true,
  "rowVersion": "AAAAAAAAAAE=",
  "personRowVersion": "AAAAAAAAAAI=",
  "replayed": false
}
```

ویرایش نام، هر دو نسخه را می‌خواهد:

```json
{
  "firstName": "سارا",
  "lastName": "نمونه‌اصلاح‌شده",
  "displayName": null,
  "expectedRowVersion": "AAAAAAAAAAE=",
  "expectedPersonRowVersion": "AAAAAAAAAAI=",
  "reason": "اصلاح ثبت نام"
}
```

ساخت دعوت اتصال برای پرونده انتخاب‌شده:

```json
{
  "clientOperationId": "55555555-5555-4555-8555-555555555555",
  "destinationUserId": "66666666-6666-4666-8666-666666666666",
  "expiresAtUtc": "2026-10-20T12:00:00Z",
  "expectedRowVersion": "AAAAAAAAAAE=",
  "reason": "درخواست اتصال با رضایت صاحب حساب"
}
```

ساخت رابطه (قبل از آن، عضویت و نقش Parent باید از جریان مجاز موجود آمده باشد):

```json
{
  "guardianUserId": "77777777-7777-4777-8777-777777777777",
  "learnerOrganizationPersonId": "22222222-2222-4222-8222-222222222222",
  "reason": "ثبت رابطه پس از بررسی مدرسه"
}
```

برای activate/end/revoke یا پایان پرونده، `OnboardingStateCommand`:

```json
{"expectedRowVersion":"AAAAAAAAAAM=","reason":"تأیید رابطه توسط مدیر مجاز"}
```

## خطا، هم‌زمانی و Audit

| HTTP | کد مهم و وضعیت |
|---|---|
| 400 | ORGANIZATION_ONBOARDING_INVALID؛ نام/دلیل/صفحه/نسخه بدشکل؛ PERSON_LINK_CONSENT_REQUIRED برای نبود رضایت |
| 401 | نشست معتبر موجود نیست |
| 403 | WORKSPACE_NOT_ALLOWED؛ نقش فعال Manager نیست؛ ORGANIZATION_PERSON_NAMES_PROTECTED؛ مجوز فرزند لغو شده |
| 404 | ORGANIZATION_PERSON_NOT_FOUND / SCHOOL_GUARDIAN_RELATION_NOT_FOUND؛ منبع غیرمجاز یا سازمان دیگر؛ مقصد نامعتبر بدون افشای وجود |
| 409 | ORGANIZATION_ONBOARDING_CONFLICT؛ nonce/payload یا unique conflict؛ ORGANIZATION_PERSON_ACCOUNT_LINK_CONFLICT؛ ACCOUNT_PROFILE_REQUIRED؛ ORGANIZATION_PERSON_IN_USE؛ SCHOOL_GUARDIAN_RELATION_STATE_CONFLICT |
| 410 | دعوت دیگر قابل پذیرش نیست؛ کدهای موجود Invitation حفظ شده‌اند |
| 412 | CONCURRENCY_CONFLICT؛ نسخه معتبر ولی قدیمی است |

خطاها همان ProblemDetails موجود با `code` هستند. برای retry عملیات انجام‌شده، پاسخ فعلی ممکن است پیش از بررسی نسخه قدیمی برگردد؛ این رفتار فقط برای همان حالتِ قبلاً تکمیل‌شده است، نه دور زدن مجوز/مالکیت.

تراکنش‌ها کوتاه و Serializable هستند؛ قفل‌های `UPDLOCK,HOLDLOCK` در Infrastructure قرار دارند. ویرایش نام، Person و ارجاعات/حساب آن را داخل تراکنش بررسی می‌کند. ترتیب Invitation → OrganizationPerson در دعوت/پذیرش هماهنگ است؛ ایجاد/تغییر Guardian ابتدا learner را قفل می‌کند. filtered uniqueهای موجود برای پرونده فعال و رابطه فعال حفظ شده‌اند؛ قید و FK جدید نیز اتصال invitation را هم‌سازمان نگه می‌دارند. SQL خطاهای unique به تعارض امن تبدیل می‌شوند. قفل توزیع‌شده‌ای افزوده نشده است.

write و Audit موفق در یک Save/تراکنش هستند. `AdminAuditLog` append-only موجود، actorUser/person/session، سازمان، هدف، دلیل، زمان UTC، correlation و تغییر وضعیت/شناسه قبل‌وبعد را نگه می‌دارد. نام‌ها، OTP، credential و تلفن در payload Audit خودکار کپی نمی‌شوند؛ اپراتور نیز نباید در reason داده حساس بنویسد. بازپخش موفق Audit اضافی تولید نمی‌کند. ثبت تلاش ناموفق جدید یا UI ممیزی مدرسه در scope این مرحله نیست.

## persistence و migration

Migration: `20261014214734_AddOperationalPersonGuardianOnboarding`؛ مجموع migrationها اکنون ۳۵ است.

- `organization.OrganizationPersons`: ستون nullable `CreationOperationId uniqueidentifier` و `CreationRequestHash varchar(64)`؛ CHECK جفت‌بودن مقادیر و طول ۶۴؛ filtered unique `(OrganizationId,CreationOperationId)` برای مقدار غیرnull.
- `organization.Invitations`: ستون nullable `OrganizationPersonId uniqueidentifier` و `ClientOperationId uniqueidentifier`؛ CHECK اتصال/عملیات با هم و نقش ثابت Student؛ filtered unique عملیات سازمانی و دعوت Pending هر پرونده.
- FK مرکب `(OrganizationId,OrganizationPersonId)` به کلید موجود `(OrganizationId,Id)` پرونده با Restrict؛ PKها، FK تاریخچه و rowversionهای موجود دست‌نخورده‌اند.
- index تک‌ستونی قدیمی `IX_Invitations_OrganizationId` با indexهای مرکب جدید پوشش داده شده و حذف می‌شود؛ حذف جدول/داده یا backfill وجود ندارد. ستون‌های جدید برای رکوردهای قدیمی null می‌مانند.
- جدول/وضعیت/قید تازه‌ای برای Guardian ایجاد نشده؛ composite FK و filtered unique فعال موجود کافی‌اند. هم‌زمانی Pending از قفل learner در API محافظت می‌شود.

migration فقط در SQL Server/LocalDB ایزوله تست اعمال شده است؛ دیتابیس عادی توسعه و Production در این کار migrate نشده‌اند. هیچ EnsureCreated یا auto-migrate در startup افزوده نشده است.

## مسیر کد و تست

جریان پرونده:
`OrganizationPeopleController → OrganizationPersonService → SchoolManagementAccessResolver → Person/OrganizationPerson → IOrganizationPersonStore → EfOrganizationPersonStore → SQL Server → DTO`.

جریان سرپرستی:
`SchoolGuardianRelationsController → GuardianManagementService → GuardianRelation → EfGuardianManagementStore + EfOrganizationPersonStore → SQL Server`؛ خواندن والد همچنان از `GuardianAccessContextResolver` موجود می‌گذرد.

جریان پذیرش:
`InvitationsController → OrganizationService → Invitation + CompletePersonLinkAsync → Membership/RoleAssignment موجود → Save/commit → AcceptInvitationResult`.

چهار فایل partial مجموعه `OrganizationPersonOnboardingEndpointsTests`، ۲۴ مورد Integration روی SQL Server دارند؛ چهار Unit در `OrganizationOnboardingModelTests` افزوده شده‌اند. سناریوها: create/get/search/enroll از API، عدم ساخت ضمنی حساب/نقش/ثبت‌نام، nonce و paging، stale version و رقابت ویرایش، محافظ نام جهانی، رضایت/پروفایل/رقابت پذیرش، حفظ تاریخچه و اشخاص، cross-org، چند سازمان، Parent و چند فرزند، Pending، activation، end/revoke و منع درخواست بعدی. دو مورد مستقیماً خطای SQL Server برای FK هم‌سازمان و نقش غیرStudent اتصال را اثبات می‌کنند؛ fixture فقط برای پیش‌شرط مدرسه/مدیر/عضویت یا حالت legacy/حمله استفاده می‌شود، نه برای خود workflow جدید onboarding.

اعتبارسنجی و تعداد نهایی در گزارش پایان مرحله ثبت می‌شوند؛ تست‌های provider از Recording/Fake موجود استفاده می‌کنند، نه SMS/AI زنده. MeliPayamak و تنظیمات آن، فایل‌های dirty قبلی AI و قراردادهای پیام‌رسان دست‌نخورده‌اند.

## نکات مصرف API و محدودیت‌ها

- ابتدا نشست، عضویت و workspace Manager معتبر را از مسیرهای قبلی برقرار کنید؛ سازمان از ورودی فرم تعیین نمی‌شود.
- قبل از ساخت، جست‌وجوی صفحه‌بندی‌شده انجام دهید؛ شباهت نام مجوز merge نیست.
- rowversion آخرین پاسخ را نگه دارید؛ روی 412 refresh و تصمیم کاربر لازم است، نه overwrite کور.
- برای دعوت اتصال، مشخصات سازمان و پرونده و رضایت آگاهانه را نمایش دهید؛ صرف بازکردن صفحه نباید accept ارسال کند.
- ساخت Pending Guardian را به‌عنوان «دسترسی فعال والد» نمایش ندهید. Parent باید workspace سازمان درست و child selector موجود را به‌کار ببرد.
- مسیر جدید، consent را به account موجود محدود می‌کند؛ تحویل دعوت، کشف عمومی حساب، unlink/merge، import انبوه، تغییر سن/سیاست Child Safety و اعطای خودکار نقش Parent پیاده نشده‌اند.
- هیچ Frontend خوانده/ساخته/تغییر داده نشده است؛ این نکات فقط قرارداد مصرف Backend هستند.

مرحله پیشنهادی بعد طبق audit: GAP-02، محیط staging و smoke واقعی OTP/core با اختیار و تنظیمات محیط هدف؛ نه تغییر دوباره provider ملی‌پیامک یا شروع AI در این مرحله.
