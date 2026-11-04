# قرارداد رفع Gapهای Backend — ۲۰۲۶-۱۱-۰۴

این سند افزودهٔ قرارداد Source است و مدرک استقرار روی سرور نیست. مبنای شروع `80c2e61296ca1b7ed855dc1e1183c4e8c19ad848` است. OpenAPI تولیدشدهٔ بستهٔ جدید مرجع schemaها خواهد بود؛ شمارنده‌های تاریخی HTML/PDF قبلی را با نسخهٔ جدید یکی نگیرید. پیاده‌سازی در Backend اصلی، .NET 9، EF Core 9، SQL Server و Onion باقی مانده است.

## قرارداد مشترک

Bearer همان نشست opaque موجود است. OrganizationId، UserId، ClassId و MembershipId فقط selector هستند. School Manager مجوز Platform ندارد و Platform permission جای مجوز محتوای خصوصی آموزشی را نمی‌گیرد. پاسخ خطا همان ProblemDetails با `code`، `traceId` و `correlationId` است. پس از Conflict نسخه‌ها را refetch کنید؛ overwrite یا retry خودکار با نسخهٔ ساختگی مجاز نیست.

## B01 — خواندن عضویت کلاس مستقل برای Platform

`GET /api/admin/v1/personal-classes/{classId}/memberships`

- مجوز: نشست معتبر و `platform.personal_classes.read`؛ Grant همچنان `platform.personal_classes.academic_permissions.manage` می‌خواهد.
- Query: `search` حداکثر ۱۰۰ نویسه، `role` از flags واقعی `Learner/Teacher/Manager`، `activeOnly=false`، `page=1` و `pageSize=30` با سقف ۱۰۰.
- پاسخ `items/page/pageSize/totalCount`؛ هر Item شامل `membershipId/personalClassId/userId/displayName/username/localRoles/status/joinedAtUtc/endedAtUtc/membershipVersion/canReceivePlatformAcademicGrant` است.
- `membershipVersion`، Base64 rowversion هشت‌بایتیِ همان دورهٔ عضویت است؛ `grantVersion` از DTO مجوز آموزشی می‌آید و مستقل است.
- عضویت پایان‌یافته قابل خواندن است ولی hint اعطای مجوز ندارد. hint فقط راهنمای UI است؛ mutation مجوز و وضعیت جاری را دوباره کنترل می‌کند.
- سیاست مصوب: **Platform فقط به عضویت Manager جاری Grant می‌دهد؛ Manager از اختیارات خودش به Teacher تفویض می‌کند.** نمایش Teacher در فهرست، مجوز Grant مستقیم Platform نیست.
- اولین Grant: `expectedGrantVersion=null`؛ Update/Revocation با نسخهٔ Grant جاری و نسخهٔ Membership جاری. لغو با `capabilities=None` و Reason است. همان `clientOperationId` و payload بازپخش می‌شود؛ قصد متفاوت Conflict دارد.
- خطاها: ۴۰۰ Query نامعتبر، ۴۰۱ نشست، ۴۰۳ permission، ۴۰۴ کلاس/دورهٔ نامجاز، ۴۰۹/۴۱۲ تضاد مطابق mutation موجود. شمارهٔ کامل و یادداشت خصوصی برگردانده نمی‌شود.

نمونهٔ فیلدهای یک ردیف؛ شناسه/نسخهٔ نمونه را در محیط واقعی استفاده نکنید و مقدار تازه را از GET بگیرید. فیلتر ترکیبی Role، عضویت دارای **حداقل یکی** از نقش‌های داده‌شده را انتخاب می‌کند.

```json
{
  "membershipId": "00000000-0000-0000-0000-000000000001",
  "personalClassId": "00000000-0000-0000-0000-000000000002",
  "userId": "00000000-0000-0000-0000-000000000003",
  "displayName": "مدیر نمونه", "username": "sample.manager", "localRoles": "Manager",
  "status": "Active", "membershipVersion": "AAAAAAAAAAE=", "canReceivePlatformAcademicGrant": true
}
```

نمونهٔ بدنهٔ Grant نخست روی `PUT /api/admin/v1/personal-classes/{classId}/academic-permissions/{membershipId}`؛ در Update مقدار `expectedGrantVersion` را نیز از خواندن Grant بگیرید:

```json
{
  "capabilities": "AuthorAssignments",
  "clientOperationId": "00000000-0000-0000-0000-000000000004",
  "expectedMembershipVersion": "AAAAAAAAAAE=", "expectedGrantVersion": null,
  "reason": "اعطای صریح اختیار طراحی تکلیف"
}
```

## B02 — بازیابی Intent سازمان‌سازی

`GET /api/admin/v1/provisioning?clientOperationId=provision-example&page=1&pageSize=30`

`clientOperationId` در مدل **string** با سقف ۱۰۰ است، UUID اجباری نیست. Trim و equality با collation فعلی SQL اعمال می‌شود؛ فیلتر پیش از Count و Paging است. شناسهٔ ناموجود صفحهٔ خالی ۲۰۰ می‌دهد. فیلتر `status` با lookup ترکیب می‌شود. پارامتر حاضر ولی خالی، مقدار طولانی، enum نامعتبر و paging خارج از حدود ۴۰۰ هستند.

Permission همان `platform.provisioning.read` است؛ scope فعلی آن global است و محدودیتِ مخترع به سازنده اضافه نشده است. `Requested/OrganizationCreated/SubscriptionAssigned/ManagerInvitationCreated/Completed/NeedsAttention` همان stateهای واقعی‌اند؛ وضعیت میانی را Completed نمایش ندهید. `failureCode` امن است؛ exception/SQL خام در DTO نیست. GET هیچ Retry یا ساخت سازمان انجام نمی‌دهد. POST با intent یکسان همان درخواست را resolve و با payload متفاوت Conflict می‌دهد. Index یکتای موجود استفاده شده و Migration جدید برای lookup ساخته نشده است.

## OPS-01 — Repair فضای رسمی

`Missing/attemptCount=0` به‌تنهایی علت دقیق سرور را اثبات نمی‌کند. Source تاریخی می‌تواند بدون intent/binding قبلی باشد؛ GET عمداً چیزی نمی‌سازد. برای علت محیط واقعی باید Migration history، Provision، Conversation binding و Outbox همان سازمان بررسی شوند. کد از قبل ابزار داخلی امن دارد و API موازی ساخته نشده است:

```powershell
dotnet MakanApp.OfficialSpacesMaintenance.dll --dry-run --organization-id <organization-guid> --output <new-dry-run.json>
dotnet MakanApp.OfficialSpacesMaintenance.dll --apply --organization-id <organization-guid> --approved-plan-sha256 <reviewed-planSha256> --output <new-apply.json>
```

Connection فقط از محیط امن فرایند با `ConnectionStrings__MakanDatabase` تأمین شود؛ در history فرمان یا گزارش عمومی ننویسید. فرمان‌ها را اپراتور SQL مجاز پس از Backup اجرا می‌کند. Hash از خروجی Dry-run همین محیط گرفته شود. هر `Conflict/ManualDecision` یا تغییر طرح، Apply را پیش از write متوقف می‌کند. نام فایل خروجی باید تازه باشد. طرح شامل سازمان و کلاس‌های آن است؛ suspended/نامشخص خودکار تعمیر نمی‌شود.

هر Purpose مستقل در transaction موجود repair می‌شود؛ rerun سالم‌ها را نگه می‌دارد، duplicate Group/Channel نمی‌سازد، عضویت لغوشده را بر اساس رابطهٔ قدیمی برنمی‌گرداند و فعالیت تاریخی را republish نمی‌کند. پس از Apply، Dry-run تازه و دو GET رسمی را بگیرید. شکست میان‌کار می‌تواند موفقیت مقصد قبلی را حفظ کند؛ Dry-run جدید و بررسی اپراتور لازم است، rollback حدسی نکنید. گزارش JSON و Hash را خصوصی نگه دارید. در این Task سرور تغییر نکرده است.

## OPS-02 — Activity و Receipt

`GET /api/v1/organization/classes/{classId}/official-spaces/activities?kind=Assignment&sourceVersionId={versionId}`

`revision` اکنون اختیاری است؛ نبود آن جدیدترین **رویداد ثبت‌شدهٔ همان منبع** را می‌خواند. برای مراجعهٔ دقیق تاریخی از `sourceRevision/eventId` در `publicationReceipt` استفاده کنید. مقدار حاضر کمتر از ۱، نوع نامعتبر یا شناسهٔ خالی: `400 ACADEMIC_ACTIVITY_QUERY_INVALID`.

علت قطعی یکی از 404های کد قبلی، تبدیل revision حذف‌شده به صفر و رد پیش از lookup بود؛ این علت مستقل از Provisioning است. برای منبع منتشرشدهٔ مجاز که رویداد تاریخی ندارد، پاسخ `404 ACADEMIC_ACTIVITY_PUBLICATION_MISSING` است. منبع ناموجود، Draft یا Context نامجاز همچنان `CONVERSATION_NOT_FOUND` می‌دهد؛ GET رویداد جدید نمی‌سازد. فهرست delivery همان قرارداد قبلی است: دو مقصد Group/Channel با `eventId/purpose/state/conversationId/messageId/attemptCount/lastFailureCategory` مستقل. Pending یا Failed را Delivered فرض نکنید.

## دعوت‌های ارسالی سازمان

`GET /api/v1/organization/invitations?status=Pending&invitationType=GuardianLink&page=1&pageSize=50`

فقط Managerِ Workspace سازمانی جاری؛ OrganizationId ورودی ندارد. پاسخ `dataAsOfUtc/page/pageSize/totalCount/items` است. سقف pageSize=100. نوع‌ها `Membership/PersonLink/GuardianLink`، status با انقضای مؤثر ساعت سرور و ترتیب CreatedAt نزولی سپس Id است. فیلترها قبل از Paging اعمال می‌شوند.

هر Item: `invitationId/invitationType/organizationId/destinationUserId/targetDisplayName/targetUsername/organizationPersonId/learnerOrganizationPersonId/role/status/createdAtUtc/expiresAtUtc/version/allowedActions`. شماره و credential برگردانده نمی‌شود. `Revoke` فقط برای دعوت Pending با مسیر revoke موجود PersonLink/GuardianLink پیشنهاد می‌شود. read مجوز تازهٔ Accept یا عضویت ایجاد نمی‌کند. برای Accept همچنان نشست حساب مقصد و consent قبلی لازم است؛ قفل و رفتار race قبلی حفظ شده‌اند. دعوت Membership مسیر revoke تازهٔ ساختگی ندارد و Action آن خالی است.

## مدیریت نقش سازمانی — تصمیم Phase 6 تکمیل شد

تصمیم صریح مالک در `2026-11-05`: Manager نقش Manager دیگر را هم مدیریت کند و محافظ آخرین مدیر لازم است. [قرارداد اجرایی Phase 6](ORGANIZATION_ROLE_MANAGEMENT_20261105_FA.md) چهار مسیر سازمانی Grant/Change/EndRole/EndMembership، نسخه‌های مستقل، رسید Retry و قفل مشترک Platform/School را شرح می‌دهد. عضویت تازه همچنان دعوت/پذیرش می‌خواهد؛ حساب و رابطه‌های آموزشی حذف نمی‌شوند. بستهٔ `ddccf20` متعلق به پیش از این تکمیل است؛ برای انتشار این قابلیت از بسته و SQL جدید آن استفاده کنید.

## سخت‌سازی Class سازمانی

- `POST /api/v1/academic/classes`: فیلد **الزامی** `clientOperationId`، UUID قصد ایجاد؛ scope یکتا `(OrganizationId, ActorUserId, ClientOperationId)` و SHA-256 payload شامل دوره، درس، عنوان trimشده، ظرفیت و ActivateImmediately. Retry همان قصد همان Class را برمی‌گرداند؛ اختلاف payload: `409 CLASS_CREATION_CONFLICT`. Replay نمای فعلی همان Class را برمی‌گرداند. حذف یا UUID خالی ۴۰۰ است. این تغییر لازمِ Contract باید پیش از انتشار با کلاینت هماهنگ شود؛ بدون شناسهٔ قصد، Backend نمی‌تواند Retry را از قصد مستقل تشخیص دهد. **کلاس‌های Legacy حفظ می‌شوند و برای آن‌ها شناسه یا نویسندهٔ ایجاد حدس زده نمی‌شود.**
- `PATCH /api/v1/academic/classes/{classId}` با `title/capacity/expectedVersion/reason`: فقط Manager همان سازمان، کلاس Draft/Active، Reason معتبر، نسخهٔ تازه و ظرفیت مثبت. ظرفیت کمتر از Enrollmentهای فعال: `409 CLASS_SETTINGS_CONFLICT`؛ هیچ Learner حذف نمی‌شود. قفل Class با مسیر Enrollment مشترک است. رویداد و Audit از زیرساخت موجود و همان transaction ثبت می‌شوند.
- ClassResult، EnrollmentResult و TeacherAssignmentResult اکنون `version` دارند؛ list کلاس نیز آن را می‌دهد.
- End enrollment: body قبلی به‌همراه `expectedVersion` اختیاری. End teacher: body افزایشی `{ "expectedVersion": "<server-version>" }`. برای کلاینت جدید ارسال نسخه لازم است؛ نبود body قبلی برای سازگاری محفوظ است. نسخهٔ کهنه همان `CONCURRENCY_CONFLICT` موجود را می‌گیرد. تاریخچه و User/asset حذف نمی‌شوند.
- schema افزایشی: سه ستون nullable قصد ایجاد روی `academic.Classes`، FK محدود به User، unique filtered index و CHECK همه‌یا‌هیچ. دادهٔ Legacy با actor/hash/operation حدسی backfill نمی‌شود. Migration: `20261104164717_HardenOrganizationClassCreationIntent`. روی دیتابیس عادی/سرور اعمال نشده است.

نمونهٔ `POST /api/v1/academic/classes`؛ در Retry همین UUID قصد و همان payload را نگه دارید. پاسخ همان ClassResult دارای `id/title/capacity/status/version` است:

```json
{
  "academicPeriodId": "00000000-0000-0000-0000-000000000005",
  "courseId": "00000000-0000-0000-0000-000000000006",
  "title": "کلاس نمونه", "capacity": 25, "activateImmediately": true,
  "clientOperationId": "00000000-0000-0000-0000-000000000007"
}
```

نمونهٔ PATCH همان کلاس با نسخهٔ خوانده‌شده:

```json
{"title":"عنوان جدید","capacity":30,"expectedVersion":"AAAAAAAAAAE=","reason":"تغییر مصوب ظرفیت"}
```

## B04 و SafeIdentity

`PlatformClassDetail.activeLearners` همان DTO موجود `PlatformClassPerson[]` است؛ UserId برای فراگیر بدون حساب nullable و `activeLearnersTruncated` محفوظ است. اصلاح در schema transformer انجام می‌شود، نه ساخت DTO موازی یا تغییر مدل SQL.

Read دعوت PersonalClass، `classTitle` و `inviter={userId,displayName,username}` را در همان DTO مجاز اضافه می‌کند. actor تاریخی از Audit **همان Invitation** گرفته می‌شود؛ مدیر فعلی جای دعوت‌کنندهٔ قبلی حدس زده نمی‌شود. Legacy بدون Audit قابل‌استناد `inviter=null` می‌گیرد. global UUID→Identity lookup وجود ندارد.

SchoolGuardianRelationDto فیلدهای `guardianDisplayName/guardianUsername/learnerDisplayName` را دارد؛ query از User/Person رابطه و learner همان سازمان می‌خواند و به داشتن Membership والد وابسته نیست. این تغییر حق خواندن چت، فایل خصوصی یا دادهٔ سازمان دیگر نمی‌دهد.

## گزارش محتوا و کشف Draft آزمون

`GET /api/v1/organization/teacher-content-activities`

فقط Manager جاری سازمان؛ Query: `teacherUserId/classId/kind/fromUtc/toUtc/page/pageSize`. نوع‌ها Lesson/Assignment/Exam؛ Session این گزارش محتوا نیست. بازه نیمه‌باز `[fromUtc,toUtc)` با UTC معتبر، سقف pageSize=100 و پیش‌فرض ۵۰. خروجی SchoolPage از **خود منابع/نسخه‌های منتشرشده** می‌آید: نویسندهٔ نسخه با نام Context مجاز، کلاس، نوع، sourceVersionId، revision، عنوان و PublishedAtUtc واقعی. منبع تاریخی بدون رویداد هم حذف نمی‌شود؛ `eventId=null` دارد. نویسنده با ناشر رویداد یکی فرض نمی‌شود. پیش‌نویس‌ها، تاریخچهٔ خصوصی ویرایش، بدنهٔ درس، پاسخ فراگیر و یادداشت ارزیاب گزارش نمی‌شوند؛ رویداد تاریخی حدس زده یا در GET ساخته نمی‌شود.

`GET /api/v1/academic/exams/discovery?classId={id}&status=Draft&search=...&page=1&pageSize=25`

فقط Manager یا Teacher با انتساب جاری در سازمانِ Workspace. فیلتر Status از `ExamVersionStatus` نسخهٔ جاری است؛ School Teacherهای همان کلاس مطابق semantics قبلی Draft را می‌بینند، فقط اسم author به‌تنهایی مجوز نمی‌سازد. query قبل از Count/Page؛ ترتیب UpdatedAt نزولی سپس ExamId. پاسخ `AcademicPage<StudentExamSummary>` با `items/page/pageSize/total/totalCount`، بدون Questions/AnswerKey/Editor است. endpoint آرایه‌ای قبلی `/academic/exams` حفظ شده و از قبل Draft مجاز را شامل می‌شد. Student/Parent وارد مدل discovery مدرس نمی‌شوند. پس از پایان Assignment، نتیجهٔ کلاس مدرس قطع می‌شود.

## نتایج، Commerce و تصمیم‌های باقی‌مانده

- نتیجهٔ آزمون از `CurrentReleasedRevision/CurrentRelease` و تکلیف از `CurrentReleasedEvaluation/CurrentGradeRelease` خوانده می‌شود. Draft correction منتشرنشده جای نتیجهٔ منتشرشده را نمی‌گیرد. Student فقط OwnResult و Parent فقط learner انتخاب‌شده با رابطهٔ معتبر؛ feedback دو audience جداست و private evaluator note در DTO آن‌ها نیست.
- نمایش پاسخ سؤال‌به‌سؤال و پاسخ کودک به Parent: `PRODUCT DECISION REQUIRED`؛ API تازه ساخته نشده است.
- Quote یک Preview است، نه تعهد قیمت امضاشده/QuoteId قابل مصرف. `expiresAtUtc` از قبل مقدار واقعی دارد؛ Order با PlanVersion و قیمت معتبر سرور دوباره محاسبه می‌شود. Client price authoritative نیست. `paymentAvailable/paymentUnavailableCode` اکنون از adapter موجود می‌آیند. `Configured` اثبات سلامت شبکه یا پرداخت موفق نیست؛ adapter واقعی فعلی Unavailable است. redirect/callback ادعایی موفقیت نمی‌سازد؛ فقط provider verification معتبر و Outbox موجود مجاز به fulfillment هستند.
- Support Ticket: `PRODUCT DECISION REQUIRED` برای مالک/صف دسترسی، SLA، lifecycle و retention. طرح پیشنهادی برای مرحلهٔ جدا: Ticket صاحب‌دار، Reply با نویسنده/زمان سرور، attachmentِ owned و آماده، read صفحه‌بندی‌شده با ACL، کلید قصد و concurrency، Outbox فقط پس از commit. در این Task Endpoint حدسی ساخته نشده است.
- Account deletion: پیش از اجرا Product/Legal باید re-authentication، cooling-off/cancel، retention حساب/پیام/آموزش/Audit، انتقال مالکیت resources و تسویهٔ subscription را تعیین کنند. طرح مستقل درخواست→بررسی وابستگی→انتظار قابل لغو→عملیات ثبت‌شده لازم است؛ delete cascade عمومی مجاز فرض نشده است.
- Dashboard aggregate/SLA و partial semantics: `PRODUCT DECISION REQUIRED`؛ B03 optimization است، API موازی ساخته نشده؛ خطا یا نبود permission به count=0 تبدیل نمی‌شود.

## Web Push و AI

provider فعلی Test/Production `Unavailable` است. template هر دو محیط شامل `Notifications:WebPush:VapidSubject/VapidPublicKey/VapidPrivateKey/TimeToLiveSeconds` شده است؛ مقدار secret خالی می‌ماند. تنظیم provider موجود برای آماده‌سازی آینده `WebPushAndFcm` است؛ فعال‌سازی نیازمند پیکربندی معتبر transport است. کلید خصوصی فقط در تنظیم private مقصد/secret store؛ هیچ کلیدی ساخته یا Commit نشده است.

قراردادهای موجود: `GET /api/v1/notifications/devices/web-push/configuration`، `POST /api/v1/notifications/devices/web-push`، فهرست دستگاه صفحه‌بندی‌شده و `DELETE /api/v1/notifications/devices/{registrationId}`. worker تحویل، session/registration جاری را بازبینی می‌کند و invalid endpoint را از مسیر موجود revoke می‌کند. configured=true اثبات دریافت نیست. پذیرش مرورگر واقعی: HTTPS و service worker معتبر→permission→subscription/register→ارسال از جریان اعلان مجاز→دریافت دستگاه→لغو permission/registration→عدم تحویل آینده و cleanup. محیط HTTP فعلی، مرورگر و subscription آماده برای این gate فراهم نشده‌اند؛ `PUSH_RUNTIME_ACCEPTANCE_PENDING`. اجباری یا اختیاری بودن Push تصمیم محصول است.

`AiService:Enabled=false` از تنظیم پایه برقرار است و در template دو محیط نیز صریح شده است. هیچ Provider/AI/STEP10G واقعی در این Task اجرا نشده؛ activation به پذیرش جداگانهٔ Gate/ACL/Quota/Usage/Source/Retrieval و provider نیاز دارد.

## Manual Acceptance نسخهٔ Test

۱. ابتدا SQL افزایشی بازبینی‌شده را اپراتور فقط روی دیتابیس مقصد درست پس از Backup اجرا و بستهٔ Test را با environment=Test نصب کند. OTP Mock، Swagger روشن و AI خاموش باقی بمانند؛ محیط Production Mock را نمی‌پذیرد.

۲. B01: Platform مجاز→Read memberships→انتخاب Manager→Grant نخست با دو نسخهٔ مستقل→Reload→Update→None/Revoke→نسخهٔ کهنه→Conflict؛ Teacher از Manager تفویض بگیرد، Grant مستقیم Platform به Teacher رد شود. حساب فاقد permission و عضویت کلاس دیگر رد شوند.

۳. B02: POST یک قصد→پاسخ کلاینت را کنار بگذارید→GET با همان string operation→همان request/outputIds→POST همان payload→Replay→payload متفاوت→Conflict؛ هیچ سازمان دوم ساخته نشود.

۴. Official Spaces: وضعیت Missing را ثبت→Dry-run→بازبینی Hash→Apply مجاز→Ready هر دو مقصد→Dry-run/Apply دوباره→همان شناسه‌ها و بدون duplicate. سپس Assignment تازه Publish→Receipt واقعی→Activity با/بدون revision→وضعیت مستقل Group/Channel. source نامجاز نباید اطلاعات بدهد.

۵. دعوت: Manager ارسال کند→Reload/ورود مجدد به همان Workspace→فیلتر نوع/status→خواندن نسخهٔ جاری→Accept/Revocation از مسیر نوع مربوط→خواندن state جدید؛ Manager سازمان دیگر داده را نبیند. برای مدیریت نقش، قرارداد Phase 6 افزودهٔ `2026-11-05` و دو نسخهٔ مستقل Membership/Role را رعایت کنید؛ محافظ آخرین مدیر را از هر دو مسیر Platform/School بررسی کنید.

۶. Class: یک clientOperationId→Create→Retry→همان Id→قصد متفاوت Conflict؛ GET نسخه→PATCH معتبر→نسخهٔ کهنه Conflict؛ ظرفیت کمتر از اعضای فعال رد→End Teacher/Enrollment با نسخهٔ همان رابطه→حفظ User و history؛ Cross-organization رد.

نتیجهٔ Test محلی و پذیرش واقعی سرور جدا ثبت می‌شوند. مالک استقرار/تست سرور را خودش انجام می‌دهد؛ این سند هیچ نتیجهٔ محیط واقعی را از وجود کد استنتاج نمی‌کند.

## نتیجهٔ اعتبارسنجی Source نهایی

Restore موفق؛ Build نهایی Debug با صفر Warning و Error موفق شد. اجرای نهایی کل Solution: **۱۰۷۷ Unit + ۱۷۳۴ Integration + ۱۴ Architecture = ۲۸۲۵ تست موفق**، صفر Failed و Skipped. Hash تعداد ۱۳۷۱ فایل Source/Project قبل و بعد از این اجرا برابر بود. نسبت به Baseline، ۱۳ اجرای Unit و ۱۷ اجرای Integration اضافه شده‌اند؛ تست‌های موجود Repair و Migration تاریخی نیز تقویت شده‌اند. خطاهای fixture قدیمی در اجرای اولیه رفع شدند و هیچ تستی حذف یا Skip نشد.

Migration کامل و ارتقای ۶۲→۶۳، هر کدام دو بار روی دو LocalDB موقت اجرا شدند؛ اجرای دوم NOOP، schema/history دو مسیر برابر و هر دو دیتابیس پاک شدند. pending model changes وجود ندارد. هیچ Migration روی `MakanApp`، `MakanApp_Test` یا سرور اجرا نشد.

اسکن Diff و Hash فایل‌های محافظت‌شده موفق بود. سه فایل AI از قبل Untracked، چهار appsettings خصوصی و User Secrets دست‌نخورده باقی ماندند. این شمارنده‌ها مربوط به انتشار `2026-11-04` هستند؛ Phase 6 در افزودهٔ `2026-11-05` ادامه یافته و گزارش اعتبارسنجی/Commit/بستهٔ خودش را دارد. پذیرش سرور بر عهدهٔ مالک است.
