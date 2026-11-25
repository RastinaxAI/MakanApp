# قرارداد مهلت و پاسخ تکلیف منتشرشده

بازبینی کد `2026-11-23` بر مبنای `source@ac9a23968c1449c037cd1eb7f4bae6812252b73d` و تغییرات همین Working Tree. این سند قرارداد Backend است؛ Frontend و سرور تغییر نکرده‌اند. ۵۴ تست Policy/Deadline، مجموعهٔ متمرکز گسترده‌ترِ ۹۰ تست و Full Regression محلی با ۳۷۳۸ تست موفق‌اند. علت رخداد واقعی همچنان منتظر Evidence است؛ Commit/Push/Publish انجام نشده‌اند.

## مهلت و معنای خطا

`SubmissionDeadlinePolicy` قانون مشترک ایجاد Draft، ذخیره، اتصال، حذف پیوست و Submit است. زمان پذیرش از `TimeProvider` سرور می‌آید. `nowUtc <= dueAtUtc` مانع زمانی ندارد؛ `nowUtc > dueAtUtc && !allowLateSubmission` برابر `409 SUBMISSION_DEADLINE_PASSED` است. با `allowLateSubmission=true` مانع زمانی برداشته می‌شود؛ مجوز، وضعیت تلاش، سقف تلاش و Policy فایل همچنان بررسی می‌شوند. مهلت تکلیف به Age یا Subscription/Quota وابسته نشده است.

Upload و Attach دو درخواست مستقل‌اند. موفقیت `POST /api/v1/files` یعنی FileAsset ساخته شده است؛ `POST /api/v1/academic/submission-attempts/{attemptId}/attachments` باید قواعد تکلیف را نیز بررسی کند. شکست دومی، موفقیت اولی را به Upload Failure تبدیل نمی‌کند. زمان ثبت‌شده در خطای جدید، زمان بررسی همان عملیات پس از قفل‌های لازم است؛ زمان رخدادهای قدیمی را از آن استنتاج نکنید.

در ProblemDetails همین خطا، پس از تأیید مجوز منبع، این metadata افزوده می‌شود:

| فیلد | معنا |
|---|---|
| `assignmentId`, `assignmentVersionId` | تکلیف و نسخهٔ واقعیِ بررسی‌شده |
| `classId`, `classKind` | مرز واقعی `OrganizationClass` یا `PersonalClass` |
| `attemptId`, `attemptStatus` | تلاش جاری؛ هنگام ایجاد تلاش ممکن است null باشند |
| `requestTimeUtc`, `dueAtUtc`, `allowLateSubmission` | مقادیر مقایسهٔ مهلت در سرور |
| `assignmentStatus`, `assignmentVersionIsPublished` | وضعیت فعلی منبع |
| `requiredAction` | `REFETCH_ASSIGNMENT_AND_ATTEMPT` |
| `operationState`, `retryable` | `NotApplied` و `false` |
| `code`, `traceId`, `correlationId` | قرارداد قبلی خطا و ردیابی |

متن پاسخ یا فایل دانش‌آموز، شماره، Token و Connection String در این metadata نیستند. فیلدها فقط مربوط به همین خطا هستند؛ برای همهٔ پاسخ‌های 409 اجباری فرض نشوند.

## تغییر Policy توسط مدرس یا مدیر

```text
PATCH /api/v1/academic/assignments/{assignmentId}/submission-policy
```

نشست معتبر لازم است. برای سازمان، Manager فعلی یا Teacher دارای TeacherAssignment فعال همان کلاس مجاز است. نقش Teacher در کلاس دیگر اختیار نمی‌دهد. برای PersonalClass، عضویت و اختیار صریح و مؤثر `AuthorAssignments` لازم است؛ نقش Manager یا Teacher به‌تنهایی کافی نیست. اختیار Platform نیز مجوز خواندن یا تغییر خودکار محتوای آموزشی ایجاد نمی‌کند. Feature flagهای موجود PersonalClass حفظ شده‌اند.

ابتدا `GET /api/v1/academic/assignments/{assignmentId}` را بخوانید. بدنه دقیقاً چهار فیلد دارد:

```json
{
  "dueAtUtc": "2030-01-25T08:00:00Z",
  "allowLateSubmission": true,
  "expectedAssignmentRowVersion": "<assignmentRowVersion از GET جاری>",
  "expectedVersionRowVersion": "<versionRowVersion از همان GET>"
}
```

زمان نمونه را با مهلت واقعی و مجاز جایگزین کنید. برای تمدید، مهلت جدید باید از مهلت قبلی بزرگ‌تر و از زمان سرور جلوتر باشد؛ کوتاه‌کردن پذیرفته نیست. برای تغییر Late به‌تنهایی، عین `dueAtUtc` فعلی را ارسال کنید؛ گذشته‌بودن همان مهلت مانع این تغییر نیست. همهٔ فیلدها، حتی `false` صریح، اجباری‌اند. فیلد اضافی مانند `maxScore`، `title`، `description` یا `attachmentFileIds` رد می‌شود. `reason` در این قرارداد فیلد ورودی نیست؛ Audit دلیل ثابت عملیاتی دارد.

پاسخ موفق `200 AssignmentResult` است. همان `id` و `versionId` باقی می‌مانند؛ دو RowVersion تازه را نگه دارید. عنوان، محتوا، MaxScore، MaxAttempts، گیرندگان، متن/فایل تلاش و رسید قبلی تغییر نمی‌کنند. `PATCH /assignments/{assignmentId}` همچنان Draft-only است.

| پاسخ | رفتار |
|---|---|
| `400 ASSIGNMENT_DUE_DATE_INVALID` | زمان نامعتبر یا کوتاه‌کردن مهلت؛ ورودی اصلاح شود |
| `400 REQUEST_INVALID` | JSON ناقص یا فیلد خارج از قرارداد |
| `401` | نشست معتبر موجود نیست |
| `403 ASSIGNMENT_NOT_ALLOWED` یا `TEACHER_NOT_ASSIGNED` | نقش یا انتساب واقعی کافی نیست |
| `404 ASSIGNMENT_NOT_FOUND` | منبع وجود ندارد یا طبق مرز فعلی پنهان است؛ از 404 نبودن قطعی منبع را استنتاج نکنید |
| `409 ASSIGNMENT_NOT_SUBMITTABLE` | تکلیف/نسخه در وضعیت Published لازم نیست |
| `412 CONCURRENCY_CONFLICT` | حداقل یک نسخه stale/نامعتبر است؛ GET تازه و تصمیم مجدد کاربر لازم است |

`412` قرارداد استاندارد فعلی پروژه برای RowVersion است؛ با `409 SUBMISSION_DEADLINE_PASSED` یکی نشود. پس از پاسخ گم‌شده، Retry با نسخه‌های قدیمی ممکن است 412 بدهد. GET کنید؛ درخواست یکسان با نسخه‌های جاری، Audit و تغییر تازه نمی‌سازد. Last-write-wins یا تمدید خودکار وجود ندارد.

تغییر واقعی Policy و Audit در یک تراکنش ثبت می‌شوند. Audit موجود `AdminAuditLogs` شامل عامل، نشست، زمان، correlation، تکلیف/نسخه، کلاس و مقادیر قدیم/جدید است. دو writer با نسخهٔ یکسان نمی‌توانند بی‌صدا تغییر یکدیگر را بازنویسی کنند. Invalidation از مسیر موجود Assessment/Outbox پس از Commit عبور می‌کند؛ payload رخداد را جایگزین GET مجاز نکنید.

## اتصال Frontend

برای `code=SUBMISSION_DEADLINE_PASSED`، پیش از نگاشت عمومی status=409، پیام «مهلت تحویل این تکلیف به پایان رسیده است.» را نمایش دهید. توضیح کمکی می‌تواند «برای تمدید مهلت با مدرس خود هماهنگ کنید.» باشد. تکلیف و همان تلاش را با GETهای موجود دوباره بخوانید. Draft محلی، متن و FileAssetId حاصل Upload موفق را حفظ کنید؛ Retry خودکار یا Upload دوباره انجام نشود.

پس از تمدید یا فعال‌کردن Late، همان تلاش را ادامه دهید. فایل قبلی فقط تا وقتی Ready، در retention معتبر و برای همان مالک/Scope قابل اتصال است، قابل استفاده است؛ تمدید تکلیف به فایل پاک‌شده یا منقضی‌شده مجوز نمی‌دهد. اتصال و حذف، RowVersion پاسخ جدید تلاش را می‌خواهند. Submit رسید رسمی است؛ Retry تلاش قبلاً Submitشده همان رسید و `IsLate` تاریخی را نگه می‌دارد.

در نمایش رسید نهایی، `isLate` خودِ رسید مرجع است؛ آن را با مقایسهٔ دوبارهٔ `submittedAtUtc` و مهلتِ فعلی بازنویسی نکنید. تمدید بعدی نباید برچسب تاریخی ارسال را عوض کند.

برای نمایش هشدار، `dueAtUtc`، `allowLateSubmission` و `status` تازهٔ Backend را بخوانید. ساعت مرورگر صرفاً راهنمای UI است. در Late مجاز، Save/Attach/Remove/Submit را فقط به دلیل گذشته‌بودن مهلت مسدود نکنید؛ سایر قابلیت‌ها و مجوزها مستقل‌اند. هیچ UI در این Task پیاده‌سازی یا تست نشده است.

## Class و Workspace؛ مرز نتیجهٔ قابل اثبات

در Controller فعلی، `GET /api/v1/academic/classes/{classId}` عملیات تعریف‌شده نیست؛ همان Route برای PATCH است. بررسی محلی قبلی برای GET پاسخ 405 نشان داده است. این نتیجه، علت 403/404 رخداد محیط Test را ثابت نمی‌کند و تبدیل خطا یا ایجاد Route تازه برای پوشاندن آن انجام نشده است.

`GET /api/v1/academic/classes` فهرست مجازِ سازمان جاری است؛ workspace سازمانی، عضویت و نقش جاری لازم‌اند. Personal workspace و نقش Parent برای این فهرست `403 ACADEMIC_READ_NOT_ALLOWED` می‌گیرند. `GET /api/v1/personal-classes/{classId}` قرارداد مستقل PersonalClass است. برای صفحهٔ تکلیف، `GET /api/v1/academic/assignments/{assignmentId}` منبع واقعی ClassRef و Policy است. مسیر BFF کلاینت را با Route خود Backend یکی فرض نکنید.

شاخه‌های کد فعلی، پس از عبور از احراز هویت و اعتبار نشست، چنین‌اند؛ این جدول جای Evidence درخواست واقعی را نمی‌گیرد:

| Method و Route | شرط و پاسخ کد فعلی |
|---|---|
| `GET /api/v1/academic/classes` | Manager/Teacher/Student در Context سازمانی معتبر، فهرست محدود به اختیار واقعی می‌گیرند؛ بافت نامناسب `403 ACADEMIC_READ_NOT_ALLOWED` است |
| `GET /api/v1/academic/classes/{classId}` | GET تعریف نشده است؛ در پذیرش محلی همین Route با روش GET، پاسخ 405 دیده شد |
| `PATCH /api/v1/academic/classes/{classId}` | `AcademicService.UpdateClassSettingsAsync` نقش جاری Manager می‌خواهد؛ بافت فاقد آن `403 MANAGER_ROLE_REQUIRED` است؛ کلاس خارج از سازمان جاری یا ناموجود `404 CLASS_NOT_FOUND` است |
| `GET /api/v1/academic/classes/{classId}/learners` و `/teachers` | `SchoolReadService.RequireTeachingClassAsync` مجوز واقعی همان کلاس را می‌سنجد؛ ناموجود/غیرقابل مدیریت طبق قرارداد موجود `404 CLASS_NOT_FOUND` است |
| `GET /api/v1/academic/assignments/{assignmentId}` و `PATCH /api/v1/academic/assignments/{assignmentId}/submission-policy` | شناسهٔ منبع به Context واقعی resolve می‌شود؛ منبع سازمان دیگر یا منبع خصوصیِ نامجاز ممکن است `404 ASSIGNMENT_NOT_FOUND` باشد؛ در نوشتن، نقش یا TeacherAssignment ناکافی می‌تواند 403 بدهد |

403 را برای پوشاندن مشکل به 404 تبدیل نکنید؛ 404 موجود نیز نباید برای تشخیص قطعی نبودن منبع استفاده شود. پیش از نسبت‌دادن خطای مرورگر به Backend، Method، مسیر پس از عبور از BFF و correlationId همان پاسخ مشخص شوند.

هنگام تغییر Workspace، ID و پاسخ دیررس بافت قبلی را کنار بگذارید. OrganizationId، ActiveRole و ClassRef فقط selector هستند؛ سرور مجوز را resolve می‌کند. برای Root Cause یک 403/404 واقعی، Method، Route کامل، code، correlationId، Workspace/Role و نسخهٔ مستقر لازم است. بدون این Evidence، علت دقیق Runtime برابر UNKNOWN می‌ماند.

## وضعیت پذیرش

Full Regression محلی: Unit=1496، Integration=2226، Architecture=16، مجموع=3738، Failed=0، Skipped=0؛ Build با صفر Warning/Error. تمام ۹۰ مورد متمرکز در اجرای کامل نیز موفق‌اند. Pending Model Changes وجود ندارد و Migration جدید صفر است. اعلام تأیید علت Runtime به Evidence رخداد نیاز دارد و Final Regression پس از آن مرحلهٔ جداست. بررسی SQL واقعی فقط توسط مالک انجام می‌شود. `INCIDENT_READ_ONLY.sql` خصوصی است و شناسه‌های رخداد واقعی در مستندات مشترک یا Git ثبت نمی‌شوند. این مرحله تغییر Schema، Deploy، تغییر Safety یا تغییر کانفیگ AI/SMS ایجاد نکرده است. Migrationهای تست فقط در دیتابیس‌های ایزولهٔ خودکار تست اجرا شده‌اند؛ این اجرا تأیید Runtime محیط Test/Production نیست. این اعداد و وضعیت Git مربوط به پایان اجرای مستقل تکلیف در ۲۰۲۶/۱۱/۲۳ هستند. Full Regression بعدی با Unit=1499، Integration=2274، Architecture=16 و مجموع=3789 نیز موفق بود. مالک در ۲۰۲۶/۱۱/۲۵ ثبت مستقل تغییرات Policy و ساخت بسته پس از Regression همان Commit را مجاز کرد؛ نتیجهٔ Git و انتشار در manifest بستهٔ جدید ثبت می‌شود. `RUNTIME_DEADLINE_ROOT_CAUSE=AWAITING_EVIDENCE` مستقل باقی می‌ماند.
