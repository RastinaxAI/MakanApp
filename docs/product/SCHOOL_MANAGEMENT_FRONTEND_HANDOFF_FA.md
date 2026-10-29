# قرارداد مدیریت آموزشگاه و محتوای کلاس

این سند قرارداد Backend همین Working Tree است؛ نشانهٔ استقرار روی سرور نیست. مسیر پایه `/api/v1`، احراز هویت Bearer و قالب خطا ProblemDetails است. عبارت `EXISTING` یعنی رفتار قبلی حفظ شده، `EXTENDED` یعنی توسعهٔ افزایشی قرارداد و `NEW` یعنی API این مرحله. شناسه‌ها UUID و زمان‌های رویداد UTC هستند.

## ۱. اعضای سازمان — NEW

`GET /organization/members?search=...&role=Teacher&status=Active&page=1&pageSize=50`

فقط Manager با Workspace سازمانی جاری. پارامتر OrganizationId وجود ندارد؛ انتخاب Workspace باید با Membership و Role مجاز انجام شود. پاسخ:

```json
{
  "dataAsOfUtc": "2026-10-28T12:00:00Z", "page": 1, "pageSize": 50, "totalCount": 1,
  "items": [{
    "userId": "00000000-0000-0000-0000-000000000001",
    "personId": "00000000-0000-0000-0000-000000000002",
    "organizationPersonId": null, "displayName": "مدرس آزمایشی", "username": "test.teacher",
    "profilePhoto": null, "membershipId": "00000000-0000-0000-0000-000000000003",
    "membershipStatus": "Active", "activeRoles": ["Teacher"],
    "roleAssignments": [{"roleAssignmentId":"00000000-0000-0000-0000-000000000004", "role":"Teacher", "status":"Active", "assignedAtUtc":"2026-10-28T12:00:00Z", "endedAtUtc":null, "version":"AAAAAAAAAAE="}],
    "version": "AAAAAAAAAAE="
  }]
}
```

Directory روی Membership است؛ Organization People همچنان پروندهٔ فراگیر و ممکن است بدون حساب باشد. یک User با یک Person، OrganizationPerson، Membership یا RoleAssignment یکی نیست. `organizationPersonId` فقط پروندهٔ فعال همین سازمان است و می‌تواند null باشد. `personId` و `username` نیز در حساب ناقص ممکن است null باشند. `activeRoles` برای Membership غیرفعال خالی است؛ سوابق و وضعیت واقعی RoleAssignment جدا برمی‌گردند. فیلتر role روی Assignment فعال و فیلتر status روی Membership است. رمز، تلفن، DOB و تنظیمات امنیتی در Directory نیستند. عکس فقط در صورت مجازبودن طبق سیاست موجود Avatar برمی‌گردد و null به معنی نداشتن مجوز یا نداشتن عکس نیز هست.

## ۲. فهرست و ساخت کلاس — EXISTING

- `GET /academic/classes`: آرایهٔ کلاس‌های مجاز طبق رفتار قبلی؛ صفحه‌بندی جدیدی به این قرارداد اضافه نشده است.
- `POST /academic/classes`: فقط Manager. Teacher همواره `403 MANAGER_ROLE_REQUIRED` می‌گیرد.

```json
{"academicPeriodId":"00000000-0000-0000-0000-000000000001","courseId":"00000000-0000-0000-0000-000000000002","title":"کلاس الف","capacity":20,"activateImmediately":true}
```

پاسخ 201 شامل `id/organizationId/academicPeriodId/academicPeriodTitle/courseId/courseTitle/title/capacity/status` است. Course و Period باید متعلق به سازمان جاری باشند. این مرحله API ویرایش، پایان یا Archive کلاس جدید ایجاد نمی‌کند.

## ۳. فراگیران کلاس — NEW

`GET /academic/classes/{classId}/learners?status=Active&search=...&page=1&pageSize=50`

Manager یا Teacher با Assignment فعال همان کلاس. Student و Parent مجاز نیستند. Enrollment تازه قبل از هر Session یا Assignment آموزشی دیده می‌شود. پاسخ صفحه‌بندی‌شده با ردیف زیر:

```json
{"enrollmentId":"00000000-0000-0000-0000-000000000001","learnerOrganizationPersonId":"00000000-0000-0000-0000-000000000002","personId":"00000000-0000-0000-0000-000000000003","userId":null,"displayName":"فراگیر","username":null,"profilePhoto":null,"status":"Active","enrolledAtUtc":"2026-10-28T12:00:00Z","endedAtUtc":null,"version":"AAAAAAAAAAE="}
```

## ۴. مدرسان کلاس — NEW

`GET /academic/classes/{classId}/teachers?status=Active&search=...&page=1&pageSize=50`

همان مجوز ردیف قبل. ردیف پاسخ شامل `teacherAssignmentId/teacherMembershipId/userId/personId/displayName/username/profilePhoto/status/assignedAtUtc/endedAtUtc/version` است. یک کلاس چند Assignment فعال برای مدرسان متفاوت دارد. پایان یک Assignment، نقش سازمانی Teacher یا Assignment مدرس دیگر را حذف نمی‌کند.

## ۵. Enrollment — EXISTING

- `POST /academic/classes/{classId}/enrollments` با `{"learnerOrganizationPersonId":"..."}` → 201.
- `POST /academic/classes/{classId}/enrollments/{enrollmentId}/end` با `{"finalStatus":"Withdrawn"}` یا `Completed` → 200.

Manager، کلاس و پروندهٔ فعال همان سازمان لازم‌اند. duplicate فعال، ظرفیت و تعارض کلاس با تراکنش و قیود SQL کنترل می‌شوند. پایان تکراری مطابق قرارداد قبلی 409 است. `version` جدید فهرست، قرارداد End قدیمی را به پذیرش ExpectedVersion تبدیل نمی‌کند؛ عملیات End موجود با بررسی وضعیت و قفل خود کار می‌کند. Transfer مستقل وجود ندارد: End قبلی سپس Enroll مقصد، دو درخواست مستقل و غیراتمیک‌اند. UI باید شکست مرحلهٔ دوم را نمایش دهد. پروندهٔ فراگیر و عضویت حساب مستقل‌اند؛ این مرحله قواعد قبلی Enrollment را بازطراحی نکرده است.

## ۶. Teacher Assignment — EXISTING

- `POST /academic/classes/{classId}/teachers` با `{"teacherMembershipId":"..."}` → 201.
- `POST /academic/classes/{classId}/teachers/{teacherAssignmentId}/end` بدون body → 200.

Manager باید Membership فعال Teacher در همان سازمان را انتخاب کند؛ UserId جای TeacherMembershipId نیست. مرجع Assignment از فهرست جدید گرفته شود. duplicate فعال و مرجع سازمان دیگر رد می‌شوند. عملیات موجود قفل و state validation خود را دارد؛ هیچ last-write-wins یا پارامتر نسخهٔ فرضی به API قدیمی اضافه نشده است.

## ۷. دعوت والد بدون UUID — NEW، با reuse سامانهٔ Invitation

`POST /organization/people/{learnerId}/guardian-invitations`

```json
{"clientOperationId":"00000000-0000-0000-0000-000000000001","username":"test.parent","phoneNumber":null,"expiresAtUtc":"2026-11-01T12:00:00Z","reason":"افزودن والد با تأیید صاحب حساب"}
```

دقیقاً یکی از username یا phoneNumber. Username به صورت exact و بدون حساسیت به بزرگی حروف با حساب موجود تطبیق داده می‌شود؛ `@` ابتدایی پذیرفته می‌شود. شمارهٔ ایرانی با normalizer موجود به تلفن تأییدشدهٔ همان حساب resolve می‌شود. fuzzy matching، فهرست عمومی حساب‌ها، ساخت User/Credential و Person merge نداریم. مخاطب باید حساب فعال موجود داشته باشد. انقضا UTC، پس از اکنون و حداکثر ۳۰ روز است. `reason` الزامی و حداکثر ۵۰۰ نویسه. نام/شماره/UUID حساب مقصد در پاسخ Manager افشا نمی‌شود:

```json
{"id":"00000000-0000-0000-0000-000000000001","organizationId":"00000000-0000-0000-0000-000000000002","learnerOrganizationPersonId":"00000000-0000-0000-0000-000000000003","status":"Pending","expiresAtUtc":"2026-11-01T12:00:00Z","version":"AAAAAAAAAAE=","replayed":false,"nextAction":"AWAIT_PARENT_CONFIRMATION"}
```

کد 201 برای ایجاد و 200 برای replay. کلید `clientOperationId` در سازمان برای دعوت‌های onboarding یکتا است؛ برای retry همان payload و همان کلید را ارسال کنید. استفادهٔ همان کلید برای مقصد/کودک/انقضای متفاوت 409 است. دعوت Pending همان والد/کودک reuse می‌شود؛ دعوت جدا برای کودک دیگر مستقل است. درخواست جدید محدودیت ۱۰ درخواست در دقیقه برای هر actor دارد. Username/Phone دو روش resolve همان use case‌اند و Messaging discovery/privacy را تغییر نمی‌دهند.

`POST /organization/guardian-invitations/{invitationId}/revoke` با `{"expectedRowVersion":"...","reason":"..."}` برای Manager. دعوت پذیرفته‌شده از این مسیر لغو نمی‌شود؛ برای آن از GuardianRelation End/Revoke موجود استفاده کنید. لغو تکراری رسید قبلی را برمی‌گرداند.

## ۸. تأیید والد — EXTENDED

`GET /invitations` موجود فقط inbox حساب جاری را می‌دهد. برای دعوت والد فیلدهای افزایشی `guardianLearnerOrganizationPersonId/guardianLearnerDisplayName/requiresGuardianConsent/nextAction` وجود دارند. نام کودک فقط در inbox مقصد است. `organizationPersonId` دعوت والد null است؛ آن فیلد متعلق به اتصال حساب فراگیر است.

والد در نشست احرازشدهٔ خود، مشخصات دعوت را می‌بیند و تأیید صریح می‌کند:

```http
POST /api/v1/invitations/{invitationId}/accept
Content-Type: application/json

{"confirmGuardianRelation":true}
```

پاسخ موجود با `guardianRelationId` افزایشی، `membershipId/roleAssignmentId/role=Parent/alreadyAccepted` است. بدون consent → 400. حساب مقصد اشتباه → 404. در یک تراکنش Invitation پذیرفته می‌شود، Membership/Role موجود reuse یا طبق workflow موجود ایجاد می‌شوند، GuardianRelation Pending ایجاد/reuse و Active می‌شود و Audit ثبت می‌شود. ایجاد Enrollment، TeacherAssignment، Credential، تغییر تلفن یا Merge شخص انجام نمی‌شود. پروفایل کامل حساب و پروندهٔ فعال کودک هنگام پذیرش لازم‌اند. عضویت تعلیق‌شده با این دعوت دور زده نمی‌شود.

`POST /invitations/{invitationId}/decline` موجود → وضعیت `Declined`. نام واقعی enumها `Pending/Accepted/Declined/Expired/Revoked` است؛ GuardianRelation جدا `Pending/Active/Ended/Revoked` دارد. انقضا در خواندن effective محاسبه و هنگام عمل ثبت می‌شود؛ Job جدیدی ایجاد نشده است. پذیرش تکراری همان رسید را می‌دهد و رابطهٔ Ended/Revoked یا Role پایان‌یافته را دوباره فعال نمی‌کند. نمایش دعوت فعلاً از inbox موجود است؛ SMS/Push جدیدی برای این flow اضافه نشده است.

## ۹. انتخاب فرزند و گزارش — EXISTING

- `GET /guardian/children`: فقط فرزندان با رابطهٔ معتبر در Workspace والد.
- `POST /guardian/children/{organizationPersonId}/select`: subject جاری با بازاعتبارسنجی رابطه.
- `GET /guardian/relations/{organizationPersonId}`: خلاصهٔ رابطهٔ مجاز.
- `GET /reports/parent/attendance`: گزارش برای subject مجاز انتخاب‌شده، نه ChildId دلخواه.

دو فرزند برای یک والد و چند والد برای یک کودک مستقل‌اند. پایان رابطهٔ اول، دسترسی کودک دوم را قطع نمی‌کند. GuardianRelation مجوز خواندن private chat کودک، Directory کامل یا همهٔ فایل‌ها نیست. مسیرهای قبلی `POST /organization/guardian-relations` و `/{id}/activate|end|revoke` حفظ شده‌اند؛ برای onboarding جدید نیازی به واردکردن GuardianUserId خام در UI نیست.

## ۱۰. Lesson / Handout — NEW

| Method | مسیر | نتیجه |
|---|---|---|
| GET | `/academic/classes/{classId}/lessons` | صفحهٔ درس‌های مجاز |
| POST | `/academic/classes/{classId}/lessons` | Draft، 201 یا replay 200 |
| GET | `/academic/lessons/{lessonId}` | درس مجاز |
| PATCH | `/academic/lessons/{lessonId}` | جایگزینی محتوای Draft |
| POST | `/academic/lessons/{lessonId}/publish` | Published یا رسید انتشار قبلی |

```json
{"clientOperationId":"00000000-0000-0000-0000-000000000001","title":"درس اول","body":"متن ساده جزوه","displayOrder":0,"fileAssetIds":[]}
```

عنوان trimشده ۱ تا ۲۰۰، متن plain text حداکثر ۵۰۰۰۰، order از ۰ تا ۱۰۰۰۰۰، حداکثر ۱۰ FileId غیرتکراری. Draft می‌تواند متن و فایل نداشته باشد؛ انتشار حداقل یکی از این دو را لازم دارد. UI متن را HTML مورداعتماد فرض نکند. CourseId از Class resolve می‌شود، ورودی مستقل نیست. پاسخ شامل `id/organizationId/classId/courseId/title/body/displayOrder/status/revisionNumber/version/createdByUserId/createdAtUtc/updatedAtUtc/publishedAtUtc/attachments/replayed` است.

PATCH جزئی و merge خودکار نیست؛ `title/body/displayOrder/fileAssetIds/expectedVersion` کل وضعیت قابل ویرایش را تعیین می‌کنند. آرایهٔ خالی فایل‌ها، اتصال قبلی را حذف می‌کند، نه blob را. `body:null` به متن خالی تبدیل می‌شود. Publication در این مرحله immutable است؛ ویرایش Published برابر 409. تاریخ انتشار و revision با retry دوباره نوشته نمی‌شوند. برای انتشار:

```json
{"expectedVersion":"AAAAAAAAAAE="}
```

Manager یا Teacher با Assignment فعال همان کلاس می‌تواند بنویسد. Student با Enrollment فعال و OrganizationPerson فعال فقط Published را می‌بیند؛ Draft در list حذف و در Get برابر 404 است. معلم نامرتبط، دانش‌آموز خارج کلاس و سازمان دیگر رد می‌شوند. Lesson منتشرشده به AI Source تبدیل نمی‌شود؛ Assignment/Exam/Assessment/Grade/Submission/Attendance بازطراحی نشده‌اند.

## ۱۱. Attachment — EXTENDED روی FileAsset موجود

ابتدا `POST /files` به صورت multipart با فیلد `File` در Workspace سازمانی. سپس FileAsset آمادهٔ همان سازمان و متعلق به actor به Draft جدید وصل شود. Teacher دوم با مجوز همان کلاس می‌تواند اتصال موجود را حفظ کند؛ پیوست جدید او باید فایل مجاز خودش باشد. فایل Personal/سازمان دیگر/فرستندهٔ دیگر برای اتصال جدید رد می‌شود؛ Pending/Rejected/Deleted قابل اتصال نیست.

```json
{"fileAssetId":"00000000-0000-0000-0000-000000000001","fileName":"handout.pdf","contentType":"application/pdf","sizeBytes":1200,"status":"Ready","isAvailable":true,"downloadUrl":"/api/v1/files/00000000-0000-0000-0000-000000000001/content"}
```

فایل unavailable دارای `downloadUrl:null` است. Physical path/StorageKey نمایش داده نمی‌شود. URL توکن عمومی نیست و دانلود به Bearer نیاز دارد. `GET /files/{id}` و `GET /files/{id}/content` در هر درخواست permission جاری را بررسی می‌کنند. uploader بودن پس از binding راه عبور از پایان Assignment نیست. پایان Enrollment/Assignment لینک قدیمی را برای درخواست تازه غیرمجاز می‌کند. فایل مشترک اگر از یک منبع مستقل دیگر مجوز معتبر داشته باشد طبق resolver موجود قابل خواندن می‌ماند. فایل متصل مستقیم حذف نمی‌شود (`409 FILE_IN_USE`)؛ cleanup موجود نیز اتصال Lesson را در نظر می‌گیرد. cache دانلود `private, no-store` باقی است.

## ۱۲. Session و Schedule — EXISTING / مستندات EXTENDED

`POST /academic/classes/{classId}/sessions` برای Manager یا Teacher با Assignment فعال همان کلاس. Teacher بدون Assignment یا پس از End نمی‌تواند Session جدید بسازد (`403 TEACHER_NOT_ALLOWED`). CreateClass همچنان Manager-only است.

`GET /academic/schedule?fromUtc=2026-11-01T00:00:00Z&toUtc=2026-11-08T00:00:00Z`

هر دو زمان اجباری، UTC و پایان بزرگ‌تر از آغاز با حداکثر یک سال فاصله‌اند. بدون range یا با تاریخ نامعتبر `400 SESSION_TIME_INVALID`. منطق DateTime/TimeZone قبلی تغییر نکرده است.

## ۱۳. ماتریس مجوز

| قابلیت | Manager | Teacher منتسب | Teacher نامرتبط | Student ثبت‌نام‌شده | Parent |
|---|---|---|---|---|---|
| Directory و کاتالوگ selector | بله | خیر | خیر | خیر | خیر |
| ساخت Class | بله | خیر | خیر | خیر | خیر |
| Roster کلاس | بله | بله | خیر | خیر | خیر |
| Session و نوشتن Lesson | بله | بله | خیر | خیر | خیر |
| خواندن Draft | بله | بله | خیر | خیر | خیر |
| Published و فایل درس | بله | بله | خیر | بله | خیر |
| گزارش فرزند | مجوز مدیریتی جدا | مجوز تدریس جدا | خیر | مجوز شخصی جدا | فقط رابطه و subject معتبر |
| Private chat دیگران | خیر | خیر | خیر | خیر | خیر |

تمام مجوزها به عضویت/نقش/رابطهٔ جاری وابسته‌اند؛ داشتن UUID کافی نیست. خواندن Roster/Lesson و عملیات محتوای جدید این مرحله برای Class فعال است. رابطهٔ Guardian یا Manager بودن مجوز عمومی پیام‌رسان نیست.

## ۱۴. خطاها

| Status | کد مهم | اقدام UI |
|---|---|---|
| 400 | `SCHOOL_REQUEST_INVALID` | اصلاح pagination، enum یا payload |
| 400 | `GUARDIAN_INVITATION_CONSENT_REQUIRED` | نمایش تأیید صریح |
| 400 | `SESSION_TIME_INVALID` | UTC range معتبر |
| 401 | `AUTH_REQUIRED` | ورود مجدد |
| 403 | `WORKSPACE_NOT_ALLOWED` | انتخاب Workspace مدیر برای مدیریت مدرسه |
| 403 | `MANAGER_ROLE_REQUIRED` | ساخت کلاس فقط Manager |
| 403 | `TEACHER_NOT_ALLOWED` | مدرس منتسب لازم است |
| 403 | `MEMBERSHIP_NOT_ACTIVE` | تعیین وضعیت عضویت از مدیریت |
| 404 | `CLASS_NOT_FOUND` / `LESSON_NOT_FOUND` / `FILE_NOT_FOUND` | منبع در دسترس نیست؛ وجود آن را حدس نزنید |
| 404 | `GUARDIAN_INVITATION_TARGET_NOT_AVAILABLE` | حساب/پروندهٔ مجاز در دسترس نیست |
| 409 | `LESSON_STATE_CONFLICT` / `GUARDIAN_INVITATION_CONFLICT` | reload یا شناسهٔ عملیات جدید برای عمل واقعاً جدید |
| 409 | `FILE_IN_USE` / `FILE_NOT_READY` | رفع وضعیت فایل |
| 410 | `INVITATION_EXPIRED` / `INVITATION_REVOKED` | دریافت دعوت معتبر جدید |
| 412 | `LESSON_VERSION_CONFLICT` / `CONCURRENCY_CONFLICT` | reload و تصمیم دوباره، نه overwrite |
| 429 | قرارداد rate limiter موجود | صبر طبق Retry-After |

ProblemDetails دارای `code` و correlation/trace موجود است. متن فارسی قابل نمایش است؛ تصمیم ماشینی با code انجام شود. 404 در منابع خصوصی می‌تواند به معنی نداشتن مجوز باشد.

## ۱۵. Pagination و selectorها — NEW

Directory، دو Roster، Lesson، `GET /academic/courses` و `GET /academic/periods` از `search/page/pageSize` استفاده می‌کنند. page پیش‌فرض ۱ و حداکثر ۱۰۰۰۰۰، pageSize پیش‌فرض ۵۰ و بازهٔ ۱ تا ۱۰۰، search trimشده تا ۱۰۰ نویسه. پاسخ `items/totalCount/page/pageSize/dataAsOfUtc` است. ترتیب نام/عنوان سپس Id؛ Lesson با DisplayOrder سپس Id. totalCount پس از فیلتر محاسبه می‌شود. این offset pagination است و snapshot چندصفحه‌ای در برابر تغییر هم‌زمان داده تضمین نمی‌شود.

کاتالوگ‌ها Manager-only و شامل ردیف‌های استفاده‌نشده‌اند. status پیش‌فرض Active؛ برای وضعیت دیگر enum معتبر بفرستید؛ `status=` برای همهٔ وضعیت‌ها به nullable bind می‌شود. Course وضعیت `Active/Archived`، AcademicPeriod وضعیت `Active/Closed`، Enrollment وضعیت `Active/Completed/Withdrawn`، TeacherAssignment وضعیت `Active/Ended`، Membership وضعیت `Active/Suspended/Ended` و RoleAssignment وضعیت `Active/Ended` دارد. enum مرجع نهایی در schema OpenAPI همان Build است.

## ۱۶. Concurrency، Idempotency و دیتابیس

درس دارای rowversion و revisionNumber جداست: اولی optimistic concurrency و دومی شمارندهٔ تغییرات محتوا/انتشار. ExpectedVersion باید Base64 هشت‌بایتی پاسخ آخر باشد. قفل کلاس و تراکنش کوتاه Serializable نوشتن‌ها را هماهنگ می‌کنند؛ کنترل Assignment داخل تراکنش انجام می‌شود. FileAsset هنگام binding قفل و rowversion آن touch می‌شود تا حذف با نسخهٔ کهنه موفق نشود. CreateLesson با سازمان/actor/clientOperationId و fingerprint payload یکتا است. Published immutable امکان replay انتشار بدون تغییر timestamp را می‌دهد.

دعوت والد همان Invitation است؛ قفل دعوت، revalidation نشست/حساب/کودک و تراکنش پذیرش موجود به کار می‌رود. unique filtered index برای Pending والد/کودک و unique سازمان/operation جلوی duplicate را می‌گیرد. GuardianRelation همچنان state/history خود را دارد. FK و CHECK رسید Guardian و نوع دعوت را نگه می‌دارند.

Migration افزایشی `20261028202239_CompleteSchoolManagement` جدول‌های `academic.ClassLessons` و `academic.ClassLessonAttachments` و دو فیلد nullable `GuardianLearnerOrganizationPersonId/AcceptedGuardianRelationId` در Invitation را اضافه می‌کند؛ قید و index Pending دعوت توسعه یافته است. دادهٔ هویتی/Avatar/DOB جابه‌جا یا backfill نمی‌شود. فقط دیتابیس‌های موقت ایزوله برای پذیرش استفاده می‌شوند؛ روی MakanApp/MakanApp_Test یا سرور Migration اجرا نشده است.

## ۱۷. مسیر عملی UI و حدود فعلی

Manager ابتدا Workspace و Directory، سپس Course/Period selectors، CreateClass، Enroll و AssignTeacher موجود را استفاده کند. برای Reload دو Roster جدید را بخواند؛ Teacher با TeacherAssignmentId فقط رابطهٔ همان کلاس را پایان می‌دهد، نه Role کل سازمان. برای والد، Student را انتخاب و username یا تلفن دقیق وارد کند؛ Parent در inbox دعوت را تأیید، Workspace Parent و Child را انتخاب کند. برای جزوه، Upload فایل، ساخت Draft، ویرایش با ExpectedVersion و Publish انجام شود. Student فقط Published را می‌خواند و فایل را با API احرازشده می‌گیرد.

`OPEN_PRODUCT_DECISION`: دسترسی Parent به درس/جزوهٔ کلاس هنوز مصوب نیست؛ در این مرحله پیش‌فرض deny است، حتی با GuardianRelation معتبر. ویرایش یا revision جدید Published، تاریخ انتشار خودکار، renderer HTML، تولید/ایندکس AI، دعوت حسابی که هنوز ساخته نشده و کانال جدید تحویل دعوت در محدودهٔ این مرحله نیستند. کلاس Edit/End مستقل نیز به این مرحله اضافه نشده است. افزودن این قابلیت‌ها نیاز به Task و قرارداد روشن جدا دارد.

## ۱۸. DOB در پروفایل شخصی — EXISTING

قابلیت موجود از Release قبلی حفظ شده است: `GET /me` پاسخ `CurrentUserResult` و `PATCH /me/profile` ورودی `CompleteProfileCommand` شامل `dateOfBirth` اختیاری با فرمت `YYYY-MM-DD` هستند. این مقدار روی Person و از نوع date-only است؛ به School Role، Organization یا تأیید سن وابسته نیست. نمونهٔ ورودی ساختگی:

```json
{"firstName":"کاربر","lastName":"آزمایشی","displayName":"پروفایل آزمایشی","username":"test.profile","dateOfBirth":"1990-01-02"}
```

نام، نام خانوادگی و username معتبر حداقل هویت Profile هستند؛ DOB شرط جدید تکمیل پروفایل نیست. تاریخ آینده رد می‌شود؛ null/حذف فیلد در PATCH تاریخ قبلی را پاک نمی‌کند. DOB خوداظهاری جای `VerifiedBirthDate` نیست و در Directory عمومی Messenger افشا نمی‌شود. [شرح پیاده‌سازی](PROFILE_AND_PERSONAL_MESSAGING_IMPLEMENTATION_FA.md).

## ۱۹. Avatar شخصی — EXISTING

عکس متعلق به User است و با گرفتن/پایان Role یا Membership حذف نمی‌شود. ابتدا `POST /files` در Workspace شخصی به صورت multipart با `File`؛ سپس `PUT /me/profile-photo` با `{"fileAssetId":"..."}`. فایل باید Ready، JPEG/PNG معتبر، شخصی و متعلق به همان User باشد. آدرس FileId یا دیدن عکس دیگری مجوز انتخاب آن به‌عنوان عکس خود نیست.

پاسخ `ProfilePhotoResult` شامل `fileAssetId/contentType/downloadUrl` است؛ دانلود با `GET /files/{fileId}/content` و احراز هویت انجام می‌شود. `DELETE /me/profile-photo` برابر 204، مرجع عکس را حذف می‌کند؛ Profile، DOB، Role و Account پاک نمی‌شوند. `GET /me` و DTOهای هویت مجاز Messenger دارای `profilePhoto` nullable هستند. خواندن عکس دیگران تابع Discovery/Privacy/Block/Age Safety موجود است؛ null را با دسترسی به فایل حدسی جایگزین نکنید. APIهای Roster جدید نیز همین مرز را رعایت می‌کنند. [شرح Avatar](PROFILE_PHOTO_IMPLEMENTATION_FA.md).

## ۲۰. Personal Messaging مستقل از سازمان و نقش — EXISTING

School Role و Organization Membership شرط Personal Messaging نیستند. کاربرِ دارای هویت لازم می‌تواند با `POST /workspaces/select` و `{"workspaceType":"Personal"}` فضای شخصی را انتخاب کند؛ `GET /conversations` و `GET /conversations/{conversationId}/messages` فقط منابع مجاز او را می‌دهند. `POST /conversations/direct` با `targetUserId` و `scope:"Personal"` همچنان از Privacy، Block، Communication Age/Safety و رابطهٔ مجاز جاری تبعیت می‌کند؛ UUID مقصد به‌تنهایی مجوز نیست. ارسال در `POST /conversations/{conversationId}/messages` با `clientMessageId` موجود انجام می‌شود.

پایان عضویت آموزشگاه، Profile و تاریخچهٔ مجاز Personal را حذف نمی‌کند و School Manager صرف نقش خود به private chat دسترسی ندارد. پروفایل ناقص ممکن است فهرست گفتگوهای مجاز قبلی را بخواند، اما مجوز ساخت Direct جدید را از این قابلیت نمی‌گیرد؛ `GET /me/messaging-status` راهنمای capability جاری است. Official Class Conversation از این جریان جدا و وابسته به مجوز آموزشی است. این Release چنین قواعدی را باز نمی‌کند و AI در هر دو بسته خاموش است.
