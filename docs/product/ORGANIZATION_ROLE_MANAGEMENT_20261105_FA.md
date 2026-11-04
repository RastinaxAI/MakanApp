# مدیریت نقش سازمانی — قرارداد Phase 6

تصمیم مالک در `2026-11-05`: **Manager می‌تواند Manager دیگری را هم در همان سازمان مدیریت کند؛ آخرین مدیر فعال باید محفوظ بماند.** این قرارداد مرز بازِ Phase 6 در [گزارش Gap قبلی](BACKEND_GAP_RESOLUTION_20261104_FA.md) را جایگزین می‌کند. سیاست PersonalClass همچنان جداست: Platform به Manager همان کلاس اختیار آموزشی می‌دهد و Manager به Teacher تفویض می‌کند.

## مرز اختیار

فقط نشست فعال حساب فعال، Workspace سازمانی با Membership و RoleAssignment فعال Manager و سازمان فعال مجاز است. OrganizationId ورودی ندارد؛ هدف از `membershipId` باید متعلق به سازمان جاری باشد. Teacher، Student، Parent و Platform Owner در Workspace شخصی از این مسیر مجوز مدیریت سازمان نمی‌گیرند. مدیر می‌تواند Teacher/Student/Parent/Manager را روی **عضویت فعال موجود** اعطا، تغییر یا پایان دهد. هیچ User، Credential، شمارهٔ تأییدشده، Membership یا Enrollment تازه ساخته نمی‌شود؛ عضویت تازه همچنان دعوت و پذیرش صریح می‌خواهد.

مدیر جایگزین یعنی User فعال + Membership فعال همان سازمان + دورهٔ Manager فعال و پایان‌نیافته. حساب معلق، نقش پایان‌یافته یا عضویت پایان‌یافته کافی نیست. پایان نقش Manager، تغییر آن یا پایان Membership دارای Manager، بدون مدیر جایگزین رد می‌شود؛ همین محافظ برای تغییرات خود مدیر نیز برقرار است. قفل سازمان با مسیر Platform مشترک است؛ رقابت دو درخواست نباید سازمان را بدون مدیر بگذارد.

## خواندن نسخه‌ها

از مسیر موجود `GET /api/v1/organization/members?search=...&page=1&pageSize=50` استفاده کنید. پاسخ `SchoolPage<SchoolMemberResult>` است، سقف pageSize برابر ۱۰۰ و Context همان سازمان است.

- `SchoolMemberResult.version`: rowversion هشت‌بایتی **دورهٔ Membership**؛ برای `expectedMembershipVersion` و پایان Membership استفاده می‌شود.
- `roleAssignments[].roleAssignmentId/version`: شناسه و rowversion **دورهٔ RoleAssignment**؛ برای Change/EndRole لازم است.
- MembershipVersion نسخهٔ مجموعهٔ نقش‌ها نیست؛ افزودن نقش دیگر الزاماً آن را تغییر نمی‌دهد. نسخه‌ها را با هم عوض نکنید. Change، نقش قبلی را پایان می‌دهد و نقش مقصد موجود را استفاده می‌کند یا دورهٔ جدید می‌سازد؛ history درجا بازنویسی نمی‌شود.

## Endpointها

همهٔ مسیرها Bearer نشست opaque موجود، مجوز جاری Manager و `reason` با طول trimشدهٔ ۱ تا ۵۰۰ می‌خواهند. UUID قصد نباید خالی باشد. تمام پاسخ‌های موفق **۲۰۰** با DTO نام‌دار `SchoolMembershipMutationResult` هستند.

| Method | Route | ورودی |
|---|---|---|
| POST | `/api/v1/organization/members/{membershipId}/roles` | `role/clientOperationId/expectedMembershipVersion/reason` |
| PUT | `/api/v1/organization/members/{membershipId}/roles/{roleAssignmentId}` | `toRole/clientOperationId/expectedMembershipVersion/expectedRoleVersion/reason` |
| DELETE | `/api/v1/organization/members/{membershipId}/roles/{roleAssignmentId}` | body: `clientOperationId/expectedMembershipVersion/expectedRoleVersion/reason` |
| DELETE | `/api/v1/organization/members/{membershipId}` | body: `clientOperationId/expectedVersion/reason` |

`role/toRole` یکی از Manager/Teacher/Student/Parent است. Change به همان نقش ۴۰۰ است؛ برای تضمین وجود یک نقش از Grant استفاده کنید. Grant نقش از قبل فعال، دورهٔ دوم نمی‌سازد؛ قصد تازهٔ معتبر می‌تواند نتیجهٔ همان دوره را بدهد.

پاسخ: `membershipId/status/membershipVersion/activeRoleAssignments/resultRoleAssignmentId/clientOperationId/operationCompletedAtUtc/replayed`. آرایهٔ activeRoleAssignments حداکثر چهار نقش مجازِ فعال دارد و هرکدام شناسه و نسخهٔ مستقل دارند؛ تاریخچهٔ نامحدود در این پاسخ کپی نمی‌شود. `resultRoleAssignmentId` برای Grant/Change نقش مقصد و برای EndRole دورهٔ پایان‌یافته است؛ برای EndMembership null است. UserId/تلفن/Token/محتوای خصوصی تازه‌ای در این DTO اضافه نشده است.

نمونهٔ Grant؛ UUID و Version واقعی را از جریان خود بگیرید، این اعداد فقط نمونه‌اند:

```json
{
  "role": "Manager",
  "clientOperationId": "00000000-0000-0000-0000-000000000011",
  "expectedMembershipVersion": "AAAAAAAAAAE=",
  "reason": "اعطای اختیار مدیریت به همکار مجاز"
}
```

نمونهٔ Change دورهٔ Manager به Teacher؛ پس از آن باید نسخه و roleAssignmentId نقش مقصد را از پاسخ/GET بخوانید:

```json
{
  "toRole": "Teacher",
  "clientOperationId": "00000000-0000-0000-0000-000000000012",
  "expectedMembershipVersion": "AAAAAAAAAAE=",
  "expectedRoleVersion": "AAAAAAAAAAI=",
  "reason": "تغییر مسئولیت سازمانی با حفظ مدیر جایگزین"
}
```

## Retry و Concurrency

کلید قصد `(OrganizationId, ActorUserId, ClientOperationId)` است و چهار نوع عملیات یک namespace مشترک دارند. Hash شامل نوع، هدف، دورهٔ نقش، نقش مقصد، نسخه‌های canonical و دلیل trimشده است. با یک شناسه، تغییر نوع/هدف/payload خطای ۴۰۹ دارد. Reason و Version هم جزو قصدند؛ برای قصد اصلاح‌شده شناسهٔ تازه بسازید.

Retry همان قصد پیش از بررسی نسخهٔ stale، رسید ثبت‌شده را پیدا می‌کند؛ `replayed=true` و **وضعیت جاری هدف** را بدون ایجاد نقش، Audit یا Outbox مجدد برمی‌گرداند. پاسخ خصوصی ذخیره نمی‌شود؛ نتیجهٔ تاریخی درجا بازسازی نمی‌شود. اگر نقش بعداً پایان یافته باشد، Retry Grant قدیمی آن را زنده نمی‌کند. `operationCompletedAtUtc` زمان سرورِ همان رسید نخست است و تمدید نمی‌شود.

مجوز جاری در Replay نیز لازم است. مدیری که اختیار خودش را پایان داده یا عضویتش لغو شده، با Retry صاحب اختیار دوباره نمی‌شود. پس از قطع پاسخ، همان درخواست را فقط با نشست/Context معتبر تکرار کنید. نسخهٔ کهنه در **قصد تازه** ۴۱۲ است و باید از GET تازه خوانده شود؛ overwrite خودکار ممنوع است.

تراکنش کوتاه Serializable: قفل سازمان → بازبینی حساب/نشست/Context/نقش Manager → قفل Membership هدف → رسید/نسخهٔ دو دوره → محافظ آخرین مدیر → mutation → Audit و Outbox موجود → Commit. هیچ SMS/AI/شبکه‌ای داخل آن اجرا نمی‌شود. دامنه و کلید یکتای SQL از duplicate جلوگیری می‌کنند. کنترل تجاری موجود در SaveChanges حفظ شده است؛ مجوز مدیریتی Entitlement یا ظرفیت تجاری تازه اعطا نمی‌کند.

## اثر پایان نقش و Membership

EndRole فقط همان دوره را پایان می‌دهد؛ Membership، نقش‌های دیگر، حساب، Credential، فایل شخصی، Enrollment، TeacherAssignment و GuardianRelation حذف نمی‌شوند. EndMembership نقش‌های فعال همان دوره را نیز پایان می‌دهد. دسترسی آیندهٔ School/Assessment و delivery/replay از وضعیت جاری دوباره مجوز می‌گیرد. نشست صرف از بین نمی‌رود؛ کاربر می‌تواند با حساب فعال، Workspace شخصی مجاز را انتخاب کند.

رابطه‌های آموزشی و Guardian همچنان مفاهیم جدا هستند. انتساب آموزشی باقی‌مانده، بدون نقش و Membership معتبر مجوز نیست. اعطای صریح مجدد یک نقش، مطابق مدل قبلی دسترسی را بر اساس رابطه‌های **هنوز معتبر** ارزیابی می‌کند؛ این API رابطهٔ آموزشی/Guardian تازه یا مجوز PersonalClass ایجاد نمی‌کند. هیچ تغییری در Safety، MeliPayamak یا AI انجام نشده است.

Realtime از `RealtimeInvalidationWriter.CaptureSchoolAsync` و `OfficialSpaceEventWriter.CaptureSchoolAsync` موجود در همان تراکنش استفاده می‌کند. رخداد حداقلی access/directory/sync پس از Commit پردازش می‌شود؛ event مدرک مجوز نیست، مصرف‌کننده dedupe و REST refetch می‌کند. Retry، رخداد تکراری جدید تولید نمی‌کند.

## خطاها

| Status | code مهم | رفتار Client |
|---|---|---|
| 400 | `ORGANIZATION_ONBOARDING_INVALID` یا `REQUEST_INVALID` | UUID/enum/نسخه/Reason را اصلاح کند |
| 401 | `AUTH_REQUIRED` | نشست معتبر لازم است |
| 403 | `WORKSPACE_NOT_ALLOWED` | انتخاب Context یا مجوز جاری را بررسی کند؛ Role claim کافی نیست |
| 404 | `SCHOOL_MEMBERSHIP_NOT_FOUND` / `SCHOOL_ROLE_ASSIGNMENT_NOT_FOUND` | هدف ناموجود یا خارج از scope؛ existence حدس نزند |
| 409 | `ORGANIZATION_LAST_MANAGER_REQUIRED` | ابتدا مدیر جایگزین فعال تعیین شود |
| 409 | `SCHOOL_MEMBERSHIP_STATE_CONFLICT` | وضعیت Membership/دورهٔ نقش را refetch کند |
| 409 | `SCHOOL_MEMBERSHIP_OPERATION_CONFLICT` | همان کلید را برای payload تازه استفاده نکند |
| 412 | `CONCURRENCY_CONFLICT` | نسخهٔ همان دوره را دوباره بخواند |
| 403/409 | `FEATURE_NOT_ENTITLED` / `ORGANIZATION_COMMERCIALLY_LOCKED` / `QUOTA_EXCEEDED` | workflow تجاری موجود؛ این API آن را bypass نمی‌کند |

خطا همان ProblemDetails موجود با code/traceId/correlationId است؛ خطای واقعی به موفقیت تبدیل نمی‌شود.

## دیتابیس و انتشار

Migration افزایشی `20261104224922_AddSchoolMembershipManagementOperations` فقط جدول `organization.MembershipManagementOperations` را اضافه می‌کند. Timestamp نام Migration بر اساس UTC ابزار EF است؛ تصمیم/تحویل این مرحله در تاریخ محلی `2026-11-05` انجام می‌شود. ستون‌های اصلی: Id، OrganizationId، ActorUserId، MembershipId، ClientOperationId، Kind، RequestHash(varchar64)، ResultRoleAssignmentId و CompletedAtUtc.

PK روی Id، unique intent، FK مرکب OrganizationId/MembershipId، FK Actor به User و FK نتیجه به RoleAssignment همگی Restrict هستند. CHECK نوع، طول Hash و تطابق nullable بودن نتیجه با نوع عملیات برقرار است. رسید immutable و append-only است، پس rowversion قابل ویرایش برای خودش لازم ندارد؛ Membership/RoleAssignment همان rowversion قبلی را دارند. Down پس از استفادهٔ رسید متوقف می‌شود؛ حذف تاریخچهٔ Idempotency برای Rollback خودکار مجاز نیست.

هیچ Backfill تاریخ/شخص/نقش حدسی وجود ندارد. مجموع Migrationها ۶۴ است. هیچ Migration از طرف عامل روی `MakanApp`، `MakanApp_Test` یا سرور اعمال نمی‌شود؛ فقط SQL و بستهٔ مستقل تحویل می‌شود. Backup/Deploy و پذیرش سرور با مالک است.

## پذیرش و مسیر مطالعه

Manager مجاز → GET اعضا و دو نسخهٔ مستقل → Grant و Retry هم‌زمان → یک دوره/رسید/Audit → Change و حفظ تاریخچه → EndRole → قطع دسترسی آینده و حفظ حساب → EndMembership و Retry. سپس LastManager در هر سه نوع حذف، دو self-removal، حذف رقابتی مدیر دیگر و رقابت Platform/School بررسی شوند. حساب معلق جایگزین نباشد؛ Context قدیمی بعد از revoke حساب/نشست/عضویت/نقش/سازمان رد شود. Cross-org و شناسهٔ نقشِ Membership دیگر ۴۰۴، نقش غیرManager ۴۰۳ و نسخهٔ stale ۴۱۲ باشند. نتیجهٔ واقعی اجراها در گزارش Validation مرحله ثبت می‌شود؛ وجود این سند اثبات Deploy نیست.

مطالعه: `OrganizationMembershipManagementController → SchoolMembershipManagementService → ISchoolMembershipManagementStore → EfSchoolMembershipManagementStore → RoleAssignment/Membership/OrganizationMembershipOperation → OrganizationMembershipOperationConfiguration → Migration → SchoolMembershipManagementTests`.

## نتیجهٔ اعتبارسنجی مرحله

Restore، Build و Final Build موفق، صفر Warning و Error. دو اجرای کاملِ Solution هرکدام **۱۰۸۶ Unit + ۱۷۵۴ Integration + ۱۴ Architecture = ۲۸۵۴ تست موفق**، صفر Failed و Skipped داشتند. نسبت به Baseline، ۹ Unit و ۲۰ Integration افزوده شده‌اند. Hash هر ۱۳۸۰ فایل Source/Project از شروع رگرسیون تا پایان Final Test ثابت بود؛ هیچ تستی حذف یا Skip نشده است.

مدل EF با آخرین Migration سازگار است. SQL کامل و ارتقای ۶۳→۶۴ هرکدام دو بار روی دیتابیس LocalDB موقت اجرا شدند؛ اجرای دوم NOOP و مقایسهٔ کامل ستون‌ها، Indexها، FKها، CHECKها، Defaultها و کل history دو مسیر برابر بود. دیتابیس‌های موقت ابزار حذف شدند. فایل‌های خصوصی و سه AI Untracked قبلی دست‌نخورده‌اند. هیچ Migration روی `MakanApp`، `MakanApp_Test` یا سرور اجرا نشده است. نتیجهٔ Git و پذیرش خود ZIP/سرور در گزارش انتشار همراه ثبت می‌شود؛ تست محلی جای پذیرش مالک روی سرور نیست.
