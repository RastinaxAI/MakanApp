# مرجع Endpointها و قراردادهای API ماکان

این مرجع مکمل [راهنمای استفاده از بک‌اند](backend-api-guide.md) است. قراردادهای HTTP از Controllerهای فعلی و مدل‌های متصل به آن‌ها آمده‌اند؛ مدل‌های داخلی persistence در فهرست قراردادها قرار ندارند.

در این نسخه 141 عملیات HTTP در 22 Controller وجود دارد. مسیرهای `/health`، `/openapi/v1.json`، `/swagger` و Hub در این شمارش نیستند.

همه مسیرها کامل نوشته شده‌اند. پارامترهای داخل `{...}` از مسیر، موارد ستون Query از URL و Command از بدنه JSON دریافت می‌شوند؛ تنها Upload از form-data استفاده می‌کند. همه عملیات به‌جز درخواست و تأیید OTP نیازمند `Authorization: Bearer <accessToken>` هستند.

ستون پاسخ فقط status موفق تعریف‌شده در Controller را نشان می‌دهد. خطاهای مشترک در راهنما و فهرست کدها در پایان این مرجع آمده‌اند. DTO و Enum قابل کلیک‌اند. علامت `?` یا عبارت «یا null» نشان‌دهنده nullable بودن قرارداد است؛ قواعد الزامی‌بودن و lifecycle در سرویس نیز بررسی می‌شوند.

<a id="reference-reading"></a>
## چگونه از این مرجع استفاده کنیم؟

این سند مکمل آموزشی [راهنمای جامع پروژه](backend-api-guide.md) است. برای شناخت چرایی مدل و جریان‌ها ابتدا راهنما را بخوانید؛ اینجا قرارداد و محل اجرای هر عملیات را پیدا کنید. مبنای سورس `main@f0ff3be` در `2026-10-12` است: ۱۴۱ عملیات HTTP در ۲۲ Controller و ۱۲۸ route متمایز؛ ۱۸۷ مدل مرتبط HTTP شامل ۱۸۲ مدل JSON، چهار مدل query و یک multipart، به‌علاوه یک DTO رویداد Hub و ۴۵ enum.

### مسیر پیدا کردن یک قرارداد

۱. در فهرست حوزه‌ها route را پیدا کنید؛ جدول هر Controller برای مرور سریع است.

۲. در «کارت‌های کامل عملیات» همان Method/Route را باز کنید؛ هدف، context، قواعد شاخص، خطا، source و نمونه درخواست/پاسخ را ببینید.

۳. لینک DTO را دنبال کنید؛ جدول فیلدها نوع، null، حضور در schema و معنی را نشان می‌دهد. DTO تودرتو و enum هم لینک دارند.

۴. برای قانون ترکیبی، service و port لینک‌شده را بخوانید. OpenAPI شکل انتقال را توضیح می‌دهد؛ همه قواعد دامنه و امنیت از schema قابل استنتاج نیستند.

### قراردادهای مشترک انتقال

| موضوع | قرارداد فعلی |
|---|---|
| آدرس پایه | نمونه توسعه `https://localhost:7063`؛ API کسب‌وکاری زیر `/api/v1` |
| JSON | نام propertyها camelCase؛ enum به صورت string نام enum؛ C# `Guid` در انتقال UUID |
| احراز هویت | `Authorization: Bearer <accessToken>`؛ توکن opaque نشست، نه JWT؛ فقط درخواست/تأیید OTP عمومی‌اند |
| مجوز | نشست، workspace، membership/role جاری و رابطه منبع؛ role ورودی یا شناسه سازمان اثبات مجوز نیست |
| request body | JSON برای Commandها؛ تنها upload از multipart/form-data با فیلد File استفاده می‌کند |
| تاریخ/زمان | DateTime رویدادهای UTC با ISO 8601 و Z؛ DateOnly به شکل YYYY-MM-DD؛ ساعت محلی الگو همراه timezone |
| نسخه SQL | byte[] rowversion در response به شکل Base64؛ expected version مطابق body/query همان عملیات |
| idempotency | clientOperationId/clientMessageId/clientReportId اختصاصی؛ هدر عمومی Idempotency-Key وجود ندارد |
| فهرست‌ها | قرارداد همه listها یکسان نیست؛ برخی آرایه، برخی page، برخی cursor هستند |
| حذف | status/DTO همان endpoint ملاک است؛ DELETE همیشه204 یا حذف فیزیکی نیست |
| مسیر شناسه | `:guid` در template Controller یک constraint مسیر است؛ در URL مقدار UUID قرار می‌گیرد |
| اتصال مرورگر | CORS wildcard بدون credentials؛ تنظیم cookie include با این میزبان سازگار نیست |

### required با nullable و قواعد سرویس فرق دارد

`required` در جدول قرارداد از OpenAPI گرفته شده است؛ یعنی schema چه propertyهایی را حاضر می‌خواهد. nullable از type فعلی DTO استخراج شده است. هیچ‌کدام جای بررسی `Guid.Empty`، طول رشته، range، رابطه و state در service/Domain را نمی‌گیرد. برای query، اختیاری بودن parameter و مقدار پیش‌فرض از signature/query model می‌آید؛ آن DTO بدنه JSON نیست. برای multipart، File الزامی است و binary stream DTO JSON ندارد.

نبود property الزامی، نوع نامعتبر، UUID نامعتبر یا binding شکست‌خورده ممکن است پیش از اجرای سرویس400 ایجاد کند. کد enum معتبر نیز ممکن است برای عملیات مشخص ممنوع باشد؛ مثلاً هر EnrollmentStatus برای پایان ثبت‌نام مجاز نیست. از داشتن یک عضو enum، endpoint تغییر به آن وضعیت را نتیجه نگیرید.

### خطا و اقدام مناسب

خطای Application از [GlobalExceptionHandler](../MakanApp.Api/Errors/GlobalExceptionHandler.cs) به ProblemDetails تبدیل می‌شود. نمونه زیر مصنوعی است:

```json
{
  "type": "urn:makan:problem:auth-required",
  "title": "Authentication required",
  "status": 401,
  "detail": "برای ادامه باید وارد شوید.",
  "code": "AUTH_REQUIRED",
  "traceId": "<request-trace-id>"
}
```

| status | معنی معمول | اقدام |
|---|---|---|
| 400 | binding/validation/filter نامعتبر | داده را با قرارداد و errors/code اصلاح کنید |
| 401 | نشست نامعتبر، منقضی یا غایب | ورود معتبر؛ provider SMS با503 سبب logout کاربر نیست |
| 403 | نقش/context/رابطه مجاز نیست | context و مجوز جاری را بررسی کنید |
| 404 | منبع دیده نمی‌شود یا context لازم انتخاب نشده | وجود جهانی منبع را از نتیجه استنتاج نکنید |
| 409 | تضاد state، عملیات تکراری ناسازگار، ظرفیت یا lease | code را بخوانید؛ retry کور نکنید |
| 410 | challenge/دعوت منقضی یا از دسترس خارج شده | جریان جدید متناسب حوزه |
| 412 | نسخه rowversion قدیمی | refetch و بازبینی تغییرات |
| 413 / 415 | اندازه یا نوع فایل | فایل مطابق policy انتخاب کنید |
| 422 | رد درخواست توسط provider SMS | خطای provider را از اعتبارسنجی محلی400 تفکیک کنید |
| 429 | محدودیت محلی/شماره یا provider | source code خطا و زمان مجاز retry را بررسی کنید |
| 500 / 503 | خطای ناشناخته / سرویس فنی آماده نیست | traceId و log مجاز؛ قبل retry اثر قبلی را بررسی کنید |

کارت endpoint «خطاهای شاخص» را دارد؛ فهرست کامل literalها در انتهای سند است. خطاهای binding، middleware و خرابی زیرساخت می‌توانند به آن اضافه شوند. literal تعریف‌شده الزاماً در یک مسیر runtime استفاده نشده است؛ catalog این تفاوت را نشان می‌دهد. فرمت ProblemDetails خطای model binding می‌تواند `errors` داشته باشد و همیشه code اختصاصی ندارد.

### مثال‌ها چه چیزی را اثبات می‌کنند؟

UUID، زمان، نام، rowversion و داده آموزشی مثال‌ها مصنوعی‌اند. `<accessToken>`، `<OTP_FROM_SMS>` و `<request-trace-id>` placeholder هستند؛ credential واقعی نیستند. نمونه‌ها از نظر شکل JSON با قرارداد کنترل می‌شوند، اما نیاز به account/context/resource/deadline و version معتبر را حذف نمی‌کنند. برای write، شناسه و نسخه را از response واقعی آخرین read بگیرید. نمونه پاسخ خالی موفق برای یک گزارش یعنی نتیجه صفر در scope مجاز، نه داده ساختگی مرتبط.

<a id="reference-source-map"></a>
## از route به کد، SQL و تست

برای هر کارت، Controller/Action، متد Application و port/adapter استخراج‌شده درج شده است. در سرویس‌های partial، فایل کلاس اصلی نقطه ورود مطالعه است؛ عملیات می‌تواند در فایل هم‌نام تکمیلی پیاده شود. نام service و interface را در کل حوزه جست‌وجو کنید. در [نقشه کد راهنما](backend-api-guide.md#code-map) رابطه لایه‌ها و در [دیتابیس](database-current-structure.md) جدول/constraintهای دقیق توضیح داده شده‌اند.

این مرجع مسیر source را کنار قرارداد می‌آورد؛ محل فایل جای تضمین رفتار نیست. برای تغییر concurrency، idempotency یا privacy علاوه بر Controller، تست‌های Integration همان حوزه را بررسی کنید. entityهای Domain و MakanDbContext، قرارداد JSON عمومی نیستند.


## فهرست بخش‌ها

- [ورود و نشست](#api-auth)
- [پروفایل](#api-profile)
- [فضای کاری](#api-workspaces)
- [دعوت](#api-invitations)
- [سرپرست](#api-guardian)
- [دوره آموزشی](#api-academicperiods)
- [درس](#api-courses)
- [کلاس و ثبت‌نام](#api-classes)
- [برنامه آموزشی](#api-schedule)
- [جلسه و حضور و غیاب](#api-sessions)
- [تکلیف](#api-assignments)
- [پاسخ تکلیف](#api-submissions)
- [ارزیابی تکلیف](#api-evaluations)
- [آزمون و پاسخ](#api-exams)
- [تصحیح آزمون](#api-examgradings)
- [فایل](#api-files)
- [گفت‌وگو و پیام](#api-conversations)
- [جست‌وجوی پیام](#api-messages)
- [مسدودسازی و رسید گزارش](#api-messagingsafety)
- [گزارش پیام](#api-messagereports)
- [داشبوردهای نقش‌محور](#api-dashboards)
- [گزارش‌های تفصیلی](#api-reports)
- [کارت‌های کامل عملیات و مسیر کد](#endpoint-details)
- [SignalR و مسیرهای فنی](#technical-contracts)
- [قراردادهای ورودی و خروجی](#contracts)
- [مقادیر Enum](#enums)
- [کدهای خطا](#error-codes)

<a id="api-auth"></a>
## ورود و نشست

درخواست و تأیید OTP عمومی‌اند؛ خروج به Bearer نیاز دارد. خروج نشست جاری را باطل می‌کند.

منبع: [AuthController](../MakanApp.Api/Controllers/AuthController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/otp/challenges` | — | [RequestOtpCommand](#type-requestotpcommand) | `202` [RequestOtpResult](#type-requestotpresult) |
| `POST` | `/api/v1/auth/otp/verify` | — | [VerifyOtpCommand](#type-verifyotpcommand) | `200` [VerifyOtpResult](#type-verifyotpresult) |
| `POST` | `/api/v1/auth/logout` | — | — | `204` بدون بدنه |

<a id="api-profile"></a>
## پروفایل

نشست معتبر؛ فقط پروفایل خود کاربر.

منبع: [ProfileController](../MakanApp.Api/Controllers/ProfileController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/me` | — | — | `200` [CurrentUserResult](#type-currentuserresult) |
| `PATCH` | `/api/v1/me/profile` | — | [CompleteProfileCommand](#type-completeprofilecommand) | `200` [CurrentUserResult](#type-currentuserresult) |

<a id="api-workspaces"></a>
## فضای کاری

نشست معتبر؛ انتخاب سازمان و نقش فقط از عضویت‌های مجاز خود کاربر.

منبع: [WorkspacesController](../MakanApp.Api/Controllers/WorkspacesController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/workspaces/me` | — | — | `200` آرایه [WorkspaceResult](#type-workspaceresult) |
| `POST` | `/api/v1/workspaces/select` | — | [SelectWorkspaceCommand](#type-selectworkspacecommand) | `200` [AccessContext](#type-accesscontext) |
| `GET` | `/api/v1/workspaces/current` | — | — | `200` [AccessContext](#type-accesscontext) |

<a id="api-invitations"></a>
## دعوت

نشست معتبر؛ دعوت‌های متعلق به حساب جاری. پذیرش، workspace را خودکار انتخاب نمی‌کند.

منبع: [InvitationsController](../MakanApp.Api/Controllers/InvitationsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/invitations` | — | — | `200` آرایه [InvitationResult](#type-invitationresult) |
| `POST` | `/api/v1/invitations/{invitationId}/accept` | — | — | `200` [AcceptInvitationResult](#type-acceptinvitationresult) |
| `POST` | `/api/v1/invitations/{invitationId}/decline` | — | — | `200` [DeclineInvitationResult](#type-declineinvitationresult) |

<a id="api-guardian"></a>
## سرپرست

نشست معتبر با workspace سازمانی، نقش Parent و رابطه فعال همان سازمان.

منبع: [GuardianController](../MakanApp.Api/Controllers/GuardianController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/guardian/children` | — | — | `200` آرایه [AuthorizedChildResult](#type-authorizedchildresult) |
| `POST` | `/api/v1/guardian/children/{organizationPersonId}/select` | — | — | `200` [AccessContext](#type-accesscontext) |
| `GET` | `/api/v1/guardian/relations/{organizationPersonId}` | — | — | `200` [GuardianRelationSummaryResult](#type-guardianrelationsummaryresult) |

<a id="api-academicperiods"></a>
## دوره آموزشی

نشست معتبر با نقش Manager سازمان جاری.

منبع: [AcademicPeriodsController](../MakanApp.Api/Controllers/AcademicPeriodsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/periods` | — | [CreateAcademicPeriodCommand](#type-createacademicperiodcommand) | `201` [AcademicPeriodResult](#type-academicperiodresult) |

<a id="api-courses"></a>
## درس

نشست معتبر با نقش Manager سازمان جاری.

منبع: [CoursesController](../MakanApp.Api/Controllers/CoursesController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/courses` | — | [CreateCourseCommand](#type-createcoursecommand) | `201` [CourseResult](#type-courseresult) |

<a id="api-classes"></a>
## کلاس و ثبت‌نام

نوشتن فقط Manager؛ خواندن برای Manager، Teacher منتسب یا Student ثبت‌نام‌شده. Parent در فهرست کلاس‌ها پشتیبانی نمی‌شود.

منبع: [ClassesController](../MakanApp.Api/Controllers/ClassesController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/classes` | — | [CreateClassCommand](#type-createclasscommand) | `201` [ClassResult](#type-classresult) |
| `GET` | `/api/v1/academic/classes` | — | — | `200` آرایه [ClassResult](#type-classresult) |
| `POST` | `/api/v1/academic/classes/{classId}/enrollments` | — | [EnrollLearnerCommand](#type-enrolllearnercommand) | `201` [EnrollmentResult](#type-enrollmentresult) |
| `POST` | `/api/v1/academic/classes/{classId}/enrollments/{enrollmentId}/end` | — | [EndEnrollmentCommand](#type-endenrollmentcommand) | `200` [EnrollmentResult](#type-enrollmentresult) |
| `POST` | `/api/v1/academic/classes/{classId}/teachers` | — | [AssignTeacherCommand](#type-assignteachercommand) | `201` [TeacherAssignmentResult](#type-teacherassignmentresult) |
| `POST` | `/api/v1/academic/classes/{classId}/teachers/{teacherAssignmentId}/end` | — | — | `200` [TeacherAssignmentResult](#type-teacherassignmentresult) |

<a id="api-schedule"></a>
## برنامه آموزشی

نوشتن برای Manager یا Teacher منتسب؛ خواندن برنامه متناسب با نقش و رابطه کلاس، و برای Parent با context فرزند معتبر.

منبع: [ScheduleController](../MakanApp.Api/Controllers/ScheduleController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/academic/schedule` | `fromUtc`: `DateTime`، `toUtc`: `DateTime` | — | `200` آرایه [SessionResult](#type-sessionresult) |
| `POST` | `/api/v1/academic/classes/{classId}/schedule-rules` | — | [CreateScheduleRuleCommand](#type-createschedulerulecommand) | `201` [ScheduleRuleResult](#type-scheduleruleresult) |
| `PATCH` | `/api/v1/academic/schedule-rules/{scheduleRuleId}` | — | [UpdateScheduleRuleCommand](#type-updateschedulerulecommand) | `200` [ScheduleRuleResult](#type-scheduleruleresult) |

<a id="api-sessions"></a>
## جلسه و حضور و غیاب

مدیریت جلسه و دفتر حضور و غیاب برای Manager یا Teacher منتسب؛ جزئیات جلسه برای نقش دارای دسترسی به کلاس.

منبع: [SessionsController](../MakanApp.Api/Controllers/SessionsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/classes/{classId}/sessions` | — | [CreateSessionCommand](#type-createsessioncommand) | `201` [SessionResult](#type-sessionresult) |
| `GET` | `/api/v1/academic/sessions/{sessionId}` | — | — | `200` [SessionResult](#type-sessionresult) |
| `PATCH` | `/api/v1/academic/sessions/{sessionId}` | — | [UpdateSessionCommand](#type-updatesessioncommand) | `200` [SessionResult](#type-sessionresult) |
| `POST` | `/api/v1/academic/sessions/{sessionId}/cancel` | — | [SessionVersionCommand](#type-sessionversioncommand) | `200` [SessionResult](#type-sessionresult) |
| `POST` | `/api/v1/academic/sessions/{sessionId}/complete` | — | [SessionVersionCommand](#type-sessionversioncommand) | `200` [SessionResult](#type-sessionresult) |
| `GET` | `/api/v1/academic/sessions/{sessionId}/attendance` | — | — | `200` [SessionAttendanceResult](#type-sessionattendanceresult) |
| `PUT` | `/api/v1/academic/sessions/{sessionId}/attendance` | — | [RecordAttendanceCommand](#type-recordattendancecommand) | `200` آرایه [AttendanceEntryResult](#type-attendanceentryresult) |
| `PATCH` | `/api/v1/academic/sessions/{sessionId}/attendance/{attendanceId}` | — | [CorrectAttendanceCommand](#type-correctattendancecommand) | `200` [AttendanceEntryResult](#type-attendanceentryresult) |

<a id="api-assignments"></a>
## تکلیف

ساخت، ویرایش و انتشار برای Manager یا Teacher منتسب. خواندن بر اساس نقش، وضعیت انتشار و رابطه کلاس/فرزند محدود است.

منبع: [AssignmentsController](../MakanApp.Api/Controllers/AssignmentsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/classes/{classId}/assignments` | — | [CreateAssignmentDraftCommand](#type-createassignmentdraftcommand) | `201` [AssignmentResult](#type-assignmentresult) |
| `GET` | `/api/v1/academic/classes/{classId}/assignments` | — | — | `200` آرایه [AssignmentResult](#type-assignmentresult) |
| `GET` | `/api/v1/academic/assignments` | `classId`: `Guid` | — | `200` آرایه [AssignmentResult](#type-assignmentresult) |
| `GET` | `/api/v1/academic/assignments/{assignmentId}` | — | — | `200` [AssignmentResult](#type-assignmentresult) |
| `PATCH` | `/api/v1/academic/assignments/{assignmentId}` | — | [UpdateAssignmentDraftCommand](#type-updateassignmentdraftcommand) | `200` [AssignmentResult](#type-assignmentresult) |
| `POST` | `/api/v1/academic/assignments/{assignmentId}/publish` | — | [PublishAssignmentCommand](#type-publishassignmentcommand) | `200` [PublishAssignmentResult](#type-publishassignmentresult) |

<a id="api-submissions"></a>
## پاسخ تکلیف

عملیات پاسخ برای Student مالک و واجد شرایط؛ فهرست submitted-attempts برای ارزیاب مجاز. contentVisible قرارداد نمایش محتوای تلاش را مشخص می‌کند.

منبع: [SubmissionsController](../MakanApp.Api/Controllers/SubmissionsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/assignments/{assignmentId}/attempts` | — | — | `201` [SubmissionAttemptResult](#type-submissionattemptresult) |
| `GET` | `/api/v1/academic/assignments/{assignmentId}/attempts/me` | — | — | `200` آرایه [SubmissionAttemptResult](#type-submissionattemptresult) |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}` | — | — | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| `PATCH` | `/api/v1/academic/submission-attempts/{attemptId}/draft` | — | [SaveSubmissionDraftCommand](#type-savesubmissiondraftcommand) | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| `POST` | `/api/v1/academic/submission-attempts/{attemptId}/attachments` | — | [AttachSubmissionFileCommand](#type-attachsubmissionfilecommand) | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| `DELETE` | `/api/v1/academic/submission-attempts/{attemptId}/attachments/{fileAssetId}` | `expectedRowVersion`: `string` | — | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| `POST` | `/api/v1/academic/submission-attempts/{attemptId}/submit` | — | [FinalSubmitAssignmentCommand](#type-finalsubmitassignmentcommand) | `200` [SubmissionReceipt](#type-submissionreceipt) |
| `GET` | `/api/v1/academic/assignments/{assignmentId}/submitted-attempts` | — | — | `200` آرایه [SubmissionAttemptResult](#type-submissionattemptresult) |

<a id="api-evaluations"></a>
## ارزیابی تکلیف

صف و mutationهای ارزیابی برای Manager یا Teacher منتسب؛ result برای Student مالک و guardian-result برای Parent با context فرزند معتبر.

منبع: [EvaluationsController](../MakanApp.Api/Controllers/EvaluationsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/academic/evaluations/queue` | `assignmentId`: `Guid?`، `classId`: `Guid?`، `reviewStatus`: [EvaluationReviewStatus](#type-evaluationreviewstatus) یا null، `isLate`: `bool?` | — | `200` آرایه [EvaluationQueueItemResult](#type-evaluationqueueitemresult) |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation/submission` | — | — | `200` [SubmissionForEvaluationResult](#type-submissionforevaluationresult) |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation` | — | — | `200` [EvaluatorEvaluationResult](#type-evaluatorevaluationresult) |
| `PUT` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation` | — | [SaveEvaluationDraftCommand](#type-saveevaluationdraftcommand) | `200` [EvaluatorEvaluationResult](#type-evaluatorevaluationresult) |
| `POST` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation/release` | — | [ReleaseEvaluationCommand](#type-releaseevaluationcommand) | `200` [GradeReleaseResult](#type-gradereleaseresult) |
| `POST` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation/corrections` | — | [CorrectReleasedEvaluationCommand](#type-correctreleasedevaluationcommand) | `201` [EvaluatorEvaluationResult](#type-evaluatorevaluationresult) |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/result` | — | — | `200` [StudentReleasedResult](#type-studentreleasedresult) |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/guardian-result` | — | — | `200` [ParentReleasedResult](#type-parentreleasedresult) |

<a id="api-exams"></a>
## آزمون و پاسخ

طراحی و انتشار برای Manager یا Teacher منتسب؛ شروع، پاسخ و رسید برای Student مالک تلاش. preview و فهرست قواعد جداگانه دارند که در راهنما آمده‌اند.

منبع: [ExamsController](../MakanApp.Api/Controllers/ExamsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/classes/{classId}/exams` | — | [CreateExamDraftCommand](#type-createexamdraftcommand) | `201` [ExamEditorDto](#type-exameditordto) |
| `GET` | `/api/v1/academic/exams` | — | — | `200` آرایه [StudentExamSummary](#type-studentexamsummary) |
| `GET` | `/api/v1/academic/exams/{examId}/editor` | — | — | `200` [ExamEditorDto](#type-exameditordto) |
| `GET` | `/api/v1/academic/exams/{examId}/teacher-preview` | — | — | `200` [ExamTeacherPreview](#type-examteacherpreview) |
| `PATCH` | `/api/v1/academic/exams/{examId}` | — | [UpdateExamDraftCommand](#type-updateexamdraftcommand) | `200` [ExamEditorDto](#type-exameditordto) |
| `POST` | `/api/v1/academic/exams/{examId}/questions` | — | [AddExamQuestionCommand](#type-addexamquestioncommand) | `201` [ExamEditorDto](#type-exameditordto) |
| `PATCH` | `/api/v1/academic/exams/{examId}/questions/{questionId}` | — | [UpdateExamQuestionCommand](#type-updateexamquestioncommand) | `200` [ExamEditorDto](#type-exameditordto) |
| `DELETE` | `/api/v1/academic/exams/{examId}/questions/{questionId}` | — | [DeleteExamQuestionCommand](#type-deleteexamquestioncommand) | `204` بدون بدنه |
| `GET` | `/api/v1/academic/exams/{examId}/student-preview` | — | — | `200` [StudentSafeExamPreview](#type-studentsafeexampreview) |
| `POST` | `/api/v1/academic/exams/{examId}/publish` | — | [PublishExamCommand](#type-publishexamcommand) | `200` [ExamEditorDto](#type-exameditordto) |
| `POST` | `/api/v1/academic/exams/{examId}/versions` | — | [CreateNextExamVersionCommand](#type-createnextexamversioncommand) | `201` [ExamEditorDto](#type-exameditordto) |
| `POST` | `/api/v1/academic/exams/{examId}/attempts/start` | — | [StartExamCommand](#type-startexamcommand) | `201` [StudentExamAttemptDto](#type-studentexamattemptdto) |
| `GET` | `/api/v1/academic/exams/{examId}/attempts/me/active` | — | — | `200` [StudentExamAttemptDto](#type-studentexamattemptdto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}` | — | — | `200` [StudentExamAttemptDto](#type-studentexamattemptdto) |
| `GET` | `/api/v1/academic/exams/{examId}/attempts/me` | — | — | `200` آرایه [StudentExamAttemptSummaryDto](#type-studentexamattemptsummarydto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/write-lease` | — | — | `200` [ExamWriteLeaseDto](#type-examwriteleasedto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/write-lease/transfer` | — | — | `200` [ExamWriteLeaseDto](#type-examwriteleasedto) |
| `PUT` | `/api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}` | — | [SaveExamAnswerCommand](#type-saveexamanswercommand) | `200` [ExamAnswerReceiptDto](#type-examanswerreceiptdto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}/answers` | — | — | `200` [StudentExamAnswersDto](#type-studentexamanswersdto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/finalize` | — | [FinalizeExamCommand](#type-finalizeexamcommand) | `200` [ExamFinalReceiptDto](#type-examfinalreceiptdto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}/receipt` | — | — | `200` [ExamFinalReceiptDto](#type-examfinalreceiptdto) |

<a id="api-examgradings"></a>
## تصحیح آزمون

صف، تصحیح و انتشار برای Manager یا Teacher منتسب؛ نتیجه برای Student مالک یا Parent با context فرزند. فقط تلاش Finalized قابل تصحیح است.

منبع: [ExamGradingsController](../MakanApp.Api/Controllers/ExamGradingsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/academic/exams/{examId}/grading` | `classId`: `Guid?`، `status`: [ExamGradingQueueStatus](#type-examgradingqueuestatus) یا null | — | `200` آرایه [ExamGradingQueueItemDto](#type-examgradingqueueitemdto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}/grading` | — | — | `200` [ExamGradeDto](#type-examgradedto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/grading` | — | — | `200` [ExamGradeDto](#type-examgradedto) |
| `PATCH` | `/api/v1/academic/exam-attempts/{attemptId}/grading/questions/{attemptQuestionId}` | — | [GradeExamQuestionCommand](#type-gradeexamquestioncommand) | `200` [ExamGradeDto](#type-examgradedto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/grading/complete` | — | [CompleteExamGradeCommand](#type-completeexamgradecommand) | `200` [ExamGradeDto](#type-examgradedto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/grade/release` | — | [ReleaseExamGradeCommand](#type-releaseexamgradecommand) | `200` [ExamGradeReleaseDto](#type-examgradereleasedto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/grade/corrections` | — | [CorrectReleasedExamGradeCommand](#type-correctreleasedexamgradecommand) | `201` [ExamGradeDto](#type-examgradedto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}/result` | — | — | `200` [StudentExamResultDto](#type-studentexamresultdto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}/guardian-result` | — | — | `200` [GuardianExamResultDto](#type-guardianexamresultdto) |

<a id="api-files"></a>
## فایل

نشست معتبر؛ مالکیت، scope و مجوز منبعی که فایل به آن متصل است بررسی می‌شود. دانلود stream و آپلود multipart است.

منبع: [FilesController](../MakanApp.Api/Controllers/FilesController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/files` | — | form-data: [UploadFileRequest](#type-uploadfilerequest) | `201` [FileAssetResult](#type-fileassetresult) |
| `GET` | `/api/v1/files/{fileId}` | — | — | `200` [FileAssetResult](#type-fileassetresult) |
| `GET` | `/api/v1/files/{fileId}/content` | — | — | `200` باینری فایل |
| `DELETE` | `/api/v1/files/{fileId}` | `expectedRowVersion`: `string` | — | `200` [FileAssetResult](#type-fileassetresult) |

<a id="api-conversations"></a>
## گفت‌وگو و پیام

نشست معتبر؛ عضویت گفت‌وگو و policy ارتباط/مدیریت جاری بررسی می‌شود. Owner/Admin/Member نقش‌های خود گفت‌وگو هستند.

منبع: [ConversationsController](../MakanApp.Api/Controllers/ConversationsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/conversations/groups` | — | [CreateManagedConversationCommand](#type-createmanagedconversationcommand) | `200` [ManagedConversationResult](#type-managedconversationresult)؛ `201` [ManagedConversationResult](#type-managedconversationresult) |
| `POST` | `/api/v1/conversations/channels` | — | [CreateManagedConversationCommand](#type-createmanagedconversationcommand) | `200` [ManagedConversationResult](#type-managedconversationresult)؛ `201` [ManagedConversationResult](#type-managedconversationresult) |
| `POST` | `/api/v1/conversations/direct` | — | [StartDirectConversationCommand](#type-startdirectconversationcommand) | `200` [DirectConversationResult](#type-directconversationresult)؛ `201` [DirectConversationResult](#type-directconversationresult) |
| `GET` | `/api/v1/conversations` | — | — | `200` آرایه [ConversationSummaryResult](#type-conversationsummaryresult) |
| `GET` | `/api/v1/conversations/{conversationId}/messages` | `beforeSequence`: `long?`، `limit`: `int?` | — | `200` [ConversationMessagePageResult](#type-conversationmessagepageresult) |
| `POST` | `/api/v1/conversations/{conversationId}/messages` | — | [SendMessageCommand](#type-sendmessagecommand) | `200` [MessageReceiptResult](#type-messagereceiptresult) |
| `PATCH` | `/api/v1/conversations/{conversationId}/messages/{messageId}` | — | [EditMessageCommand](#type-editmessagecommand) | `200` [MessageMutationResult](#type-messagemutationresult) |
| `DELETE` | `/api/v1/conversations/{conversationId}/messages/{messageId}` | `expectedVersion`: `string` | — | `200` [MessageMutationResult](#type-messagemutationresult) |
| `POST` | `/api/v1/conversations/{conversationId}/messages/{messageId}/forward` | — | [ForwardMessageCommand](#type-forwardmessagecommand) | `200` [MessageReceiptResult](#type-messagereceiptresult) |
| `POST` | `/api/v1/conversations/{conversationId}/messages/{messageId}/reactions` | — | [AddReactionCommand](#type-addreactioncommand) | `200` [MessageReactionResult](#type-messagereactionresult) |
| `DELETE` | `/api/v1/conversations/{conversationId}/messages/{messageId}/reactions/{reaction}` | — | — | `204` بدون بدنه |
| `POST` | `/api/v1/conversations/{conversationId}/pins/{messageId}` | — | — | `200` [ConversationPinResult](#type-conversationpinresult) |
| `DELETE` | `/api/v1/conversations/{conversationId}/pins/{messageId}` | — | — | `200` [ConversationPinResult](#type-conversationpinresult) |
| `GET` | `/api/v1/conversations/{conversationId}/media` | `kind`: [MessageKind](#type-messagekind) یا null، `beforeSequence`: `long?`، `limit`: `int?` | — | `200` [ConversationMediaPageResult](#type-conversationmediapageresult) |
| `GET` | `/api/v1/conversations/{conversationId}/changes` | `afterCursor`: `string?`، `limit`: `int?` | — | `200` [ConversationChangePageResult](#type-conversationchangepageresult) |
| `POST` | `/api/v1/conversations/{conversationId}/read` | — | [AdvanceConversationCursorCommand](#type-advanceconversationcursorcommand) | `200` [ConversationCursorStateResult](#type-conversationcursorstateresult) |
| `POST` | `/api/v1/conversations/{conversationId}/delivered` | — | [AdvanceConversationCursorCommand](#type-advanceconversationcursorcommand) | `200` [ConversationCursorStateResult](#type-conversationcursorstateresult) |
| `GET` | `/api/v1/conversations/{conversationId}` | — | — | `200` [ManagedConversationResult](#type-managedconversationresult) |
| `POST` | `/api/v1/conversations/{conversationId}/members` | — | [AddConversationMemberCommand](#type-addconversationmembercommand) | `200` [ManagedConversationResult](#type-managedconversationresult) |
| `DELETE` | `/api/v1/conversations/{conversationId}/members/{targetUserId}` | — | — | `200` [ManagedConversationResult](#type-managedconversationresult) |
| `POST` | `/api/v1/conversations/{conversationId}/leave` | — | — | `200` [ManagedConversationResult](#type-managedconversationresult) |
| `PATCH` | `/api/v1/conversations/{conversationId}/members/{targetUserId}/role` | — | [ChangeConversationMemberRoleCommand](#type-changeconversationmemberrolecommand) | `200` [ManagedConversationResult](#type-managedconversationresult) |
| `POST` | `/api/v1/conversations/{conversationId}/ownership-transfers` | — | [StartOwnershipTransferCommand](#type-startownershiptransfercommand) | `201` [OwnershipTransferResult](#type-ownershiptransferresult) |
| `POST` | `/api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/accept` | — | — | `200` [OwnershipTransferResult](#type-ownershiptransferresult) |
| `POST` | `/api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/decline` | — | — | `200` [OwnershipTransferResult](#type-ownershiptransferresult) |
| `POST` | `/api/v1/conversations/{conversationId}/archive` | — | — | `200` [ManagedConversationResult](#type-managedconversationresult) |

<a id="api-messages"></a>
## جست‌وجوی پیام

نشست معتبر؛ جست‌وجو فقط در پیام‌ها و scopeهای قابل دسترسی کاربر.

منبع: [MessagesController](../MakanApp.Api/Controllers/MessagesController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/messages/search` | `q`: `string?`، `conversationId`: `Guid?`، `kind`: [MessageKind](#type-messagekind) یا null، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `cursor`: `string?`، `limit`: `int?` | — | `200` [MessageSearchPageResult](#type-messagesearchpageresult) |

<a id="api-messagingsafety"></a>
## مسدودسازی و رسید گزارش

نشست معتبر؛ فهرست Block و رسید Report متعلق به خود کاربر.

منبع: [MessagingSafetyController](../MakanApp.Api/Controllers/MessagingSafetyController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/messaging/blocks/{userId}` | — | — | `200` [UserBlockResult](#type-userblockresult)؛ `201` [UserBlockResult](#type-userblockresult) |
| `DELETE` | `/api/v1/messaging/blocks/{userId}` | — | — | `204` بدون بدنه |
| `GET` | `/api/v1/messaging/blocks` | — | — | `200` آرایه [BlockedUserResult](#type-blockeduserresult) |
| `GET` | `/api/v1/messaging/reports/{reportId}` | — | — | `200` [AbuseReportReceiptResult](#type-abusereportreceiptresult) |

<a id="api-messagereports"></a>
## گزارش پیام

نشست معتبر و دسترسی به پیام قابل گزارش؛ clientReportId شناسه retry است.

منبع: [MessageReportsController](../MakanApp.Api/Controllers/MessageReportsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/conversations/{conversationId}/messages/{messageId}/reports` | — | [ReportMessageCommand](#type-reportmessagecommand) | `201` [AbuseReportReceiptResult](#type-abusereportreceiptresult) |

<a id="api-dashboards"></a>
## داشبوردهای نقش‌محور

فضای سازمانی و نقش فعال متناظر Student/Parent/Teacher/Manager؛ Parent بدون فرزند انتخاب‌شده پاسخ خالی 200 می‌گیرد. Teacher فقط کلاس‌های منتسب را می‌بیند.

منبع: [DashboardsController](../MakanApp.Api/Controllers/DashboardsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/dashboards/student` | — | — | `200` [StudentDashboardDto](#type-studentdashboarddto) |
| `GET` | `/api/v1/dashboards/parent` | — | — | `200` [ParentDashboardDto](#type-parentdashboarddto) |
| `GET` | `/api/v1/dashboards/teacher` | — | — | `200` [TeacherDashboardDto](#type-teacherdashboarddto) |
| `GET` | `/api/v1/dashboards/manager` | — | — | `200` [ManagerDashboardDto](#type-managerdashboarddto) |

<a id="api-reports"></a>
## گزارش‌های تفصیلی

فضای سازمانی و نقش فعال متناظر؛ Parent به فرزند انتخاب‌شده نیاز دارد. Teacher فقط کلاس منتسب؛ Manager فقط سازمان جاری. فهرست‌ها pageNumber=1 و pageSize=20 با سقف100 دارند؛ بازه UTC و sort مطابق allowlist هر گزارش است.

منبع: [ReportsController](../MakanApp.Api/Controllers/ReportsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/reports/student/attendance` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [AttendanceReportPage&lt;StudentAttendanceReportItem&gt;](#type-attendancereportpage-studentattendancereportitem-) |
| `GET` | `/api/v1/reports/student/assignments` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [ReportPage&lt;StudentAssignmentReportItem&gt;](#type-reportpage-studentassignmentreportitem-) |
| `GET` | `/api/v1/reports/student/exams` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [ReportPage&lt;StudentExamReportItem&gt;](#type-reportpage-studentexamreportitem-) |
| `GET` | `/api/v1/reports/student/grades` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [ReportPage&lt;StudentReleasedGradeReportItem&gt;](#type-reportpage-studentreleasedgradereportitem-) |
| `GET` | `/api/v1/reports/parent/attendance` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [AttendanceReportPage&lt;ParentAttendanceReportItem&gt;](#type-attendancereportpage-parentattendancereportitem-) |
| `GET` | `/api/v1/reports/parent/assignments` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [ReportPage&lt;ParentAssignmentReportItem&gt;](#type-reportpage-parentassignmentreportitem-) |
| `GET` | `/api/v1/reports/parent/exams` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [ReportPage&lt;ParentExamReportItem&gt;](#type-reportpage-parentexamreportitem-) |
| `GET` | `/api/v1/reports/teacher/classes/{classId}/attendance` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [AttendanceReportPage&lt;TeacherAttendanceReportItem&gt;](#type-attendancereportpage-teacherattendancereportitem-) |
| `GET` | `/api/v1/reports/teacher/classes/{classId}/assignments` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [ReportPage&lt;TeacherAssignmentReportItem&gt;](#type-reportpage-teacherassignmentreportitem-) |
| `GET` | `/api/v1/reports/teacher/classes/{classId}/exams` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [ReportPage&lt;TeacherExamReportItem&gt;](#type-reportpage-teacherexamreportitem-) |
| `GET` | `/api/v1/reports/teacher/classes/{classId}/learners/{learnerId}` | — | — | `200` [TeacherLearnerOverviewDto](#type-teacherlearneroverviewdto) |
| `GET` | `/api/v1/reports/manager/enrollments` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?`، `courseId`: `Guid?`، `academicPeriodId`: `Guid?`، `status`: [EnrollmentStatus](#type-enrollmentstatus) یا null | — | `200` [ReportPage&lt;ManagerEnrollmentReportItem&gt;](#type-reportpage-managerenrollmentreportitem-) |
| `GET` | `/api/v1/reports/manager/attendance` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?` | — | `200` [ReportPage&lt;ManagerAttendanceReportItem&gt;](#type-reportpage-managerattendancereportitem-) |
| `GET` | `/api/v1/reports/manager/assessments` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?`، `courseId`: `Guid?`، `academicPeriodId`: `Guid?`، `kind`: [DetailedReportAssessmentKind](#type-detailedreportassessmentkind) یا null | — | `200` [ReportPage&lt;ManagerAssessmentReportItem&gt;](#type-reportpage-managerassessmentreportitem-) |
| `GET` | `/api/v1/reports/manager/classes/{classId}` | — | — | `200` [ManagerClassPerformanceDto](#type-managerclassperformancedto) |
| `GET` | `/api/v1/reports/manager/teacher-workload` | `pageNumber`: `int`، `pageSize`: `int`، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `classId`: `Guid?`، `sort`: `string?`، `courseId`: `Guid?`، `academicPeriodId`: `Guid?` | — | `200` [ReportPage&lt;ManagerTeacherWorkloadReportItem&gt;](#type-reportpage-managerteacherworkloadreportitem-) |


<a id="endpoint-details"></a>
## کارت‌های کامل عملیات و مسیر کد

کارت‌ها قرارداد انتقال، قواعد شاخص و مسیر مطالعه کد را کنار هم می‌آورند. وابستگی‌های سرویس ممکن است میان چند متد مشترک باشند؛ ذکر adapter به معنی فراخوانی همه portها در همان متد نیست. برای قواعد کامل، service و تست حوزه را بخوانید.

<a id="operation-001"></a>
### 001. درخواست کد ورود

```http
POST /api/v1/auth/otp/challenges
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Identity`؛ `AuthController.RequestOtp` |
| احراز هویت و context | عمومی؛ بدون token؛ اعتبارسنجی OTP و rate limit |
| قواعد شاخص | شماره ایران با ارقام فارسی/عربی/انگلیسی normalize می‌شود؛ محدودیت درخواست محلی و provider مستقل‌اند. provider کد را تولید/ارسال می‌کند و فقط hash در دیتابیس ذخیره می‌شود. retry خودکار ارسال مجاز نیست. |
| ورودی | [RequestOtpCommand](#type-requestotpcommand) |
| خروجی موفق | `202` [RequestOtpResult](#type-requestotpresult) |
| خطاهای شاخص | [PHONE_INVALID](#error-phone-invalid) (400)، [OTP_RATE_LIMITED](#error-otp-rate-limited) (429)، [SMS_PROVIDER_AUTHENTICATION_FAILED](#error-sms-provider-authentication-failed) (503)، [SMS_PROVIDER_REJECTED](#error-sms-provider-rejected) (422)، [SMS_PROVIDER_UNAVAILABLE](#error-sms-provider-unavailable) (503)، [SMS_RATE_LIMITED](#error-sms-rate-limited) (429) |
| Controller | [AuthController.cs](../MakanApp.Api/Controllers/AuthController.cs) |
| متدهای service | `identityService.RequestOtpAsync` |
| Application | [IdentityService.cs](../MakanApp.Application/Identity/IdentityService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IIdentityStore` | `EfIdentityStore` | [EfIdentityStore.cs](../MakanApp.Infrastructure/Identity/EfIdentityStore.cs) |
| `IIdentitySecurity` | `IdentityCryptography` | [IdentityCryptography.cs](../MakanApp.Infrastructure/Identity/IdentityCryptography.cs) |
| `IOtpDeliveryProvider` | `DevelopmentOtpProvider` / `MeliPayamakOtpProvider` / `UnavailableOtpDeliveryProvider`؛ انتخاب با `Sms:Provider` | [provider توسعه/Unavailable](../MakanApp.Infrastructure/Identity/DevelopmentOtpProvider.cs) و [MeliPayamakOtpProvider](../MakanApp.Infrastructure/Identity/Otp/MeliPayamak/MeliPayamakOtpProvider.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/auth/otp/challenges HTTP/1.1
Host: localhost:7063
Accept: application/json
Content-Type: application/json
```

```json
{
  "phoneNumber": "+989120000000"
}
```

نمونه پاسخ موفق؛ status `202`:

```json
{
  "challengeId": "671bc7d1-186c-566f-87c4-a59c1eb9f8f9",
  "expiresAtUtc": "2026-11-01T09:05:00Z",
  "resendAvailableAtUtc": "2026-11-01T09:00:30Z"
}
```

<a id="operation-002"></a>
### 002. تأیید کد و ساخت نشست

```http
POST /api/v1/auth/otp/verify
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Identity`؛ `AuthController.VerifyOtp` |
| احراز هویت و context | عمومی؛ بدون token؛ اعتبارسنجی OTP و rate limit |
| قواعد شاخص | challengeId و شماره همان درخواست؛ code رشته با طول پیکربندی‌شده4..64، trim و تبدیل ارقام فارسی/عربی. شش‌رقمی‌بودن ثابت فرض نشود؛ ارسال همان challenge پس از مصرف نشست دوم ایجاد نمی‌کند. |
| ورودی | [VerifyOtpCommand](#type-verifyotpcommand) |
| خروجی موفق | `200` [VerifyOtpResult](#type-verifyotpresult) |
| خطاهای شاخص | [OTP_INVALID](#error-otp-invalid) (400)، [OTP_EXPIRED](#error-otp-expired) (410)، [OTP_ALREADY_USED](#error-otp-already-used) (409)، [OTP_TOO_MANY_ATTEMPTS](#error-otp-too-many-attempts) (429) |
| Controller | [AuthController.cs](../MakanApp.Api/Controllers/AuthController.cs) |
| متدهای service | `identityService.VerifyOtpAsync` |
| Application | [IdentityService.cs](../MakanApp.Application/Identity/IdentityService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IIdentityStore` | `EfIdentityStore` | [EfIdentityStore.cs](../MakanApp.Infrastructure/Identity/EfIdentityStore.cs) |
| `IIdentitySecurity` | `IdentityCryptography` | [IdentityCryptography.cs](../MakanApp.Infrastructure/Identity/IdentityCryptography.cs) |
| `IOtpDeliveryProvider` | `DevelopmentOtpProvider` / `MeliPayamakOtpProvider` / `UnavailableOtpDeliveryProvider`؛ انتخاب با `Sms:Provider` | [provider توسعه/Unavailable](../MakanApp.Infrastructure/Identity/DevelopmentOtpProvider.cs) و [MeliPayamakOtpProvider](../MakanApp.Infrastructure/Identity/Otp/MeliPayamak/MeliPayamakOtpProvider.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/auth/otp/verify HTTP/1.1
Host: localhost:7063
Accept: application/json
Content-Type: application/json
```

```json
{
  "challengeId": "671bc7d1-186c-566f-87c4-a59c1eb9f8f9",
  "phoneNumber": "+989120000000",
  "code": "<OTP_FROM_SMS>"
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "accessToken": "<synthetic-access-token>",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-12-01T09:00:00Z",
  "user": {
    "id": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
    "phoneNumber": "+989120000000",
    "isProfileComplete": true,
    "firstName": "کاربر",
    "lastName": "نمونه",
    "displayName": "کاربر نمونه",
    "username": "sample.user"
  }
}
```

<a id="operation-003"></a>
### 003. ابطال نشست جاری

```http
POST /api/v1/auth/logout
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Identity`؛ `AuthController.Logout` |
| احراز هویت و context | عمومی فقط برای OTP؛ logout با نشست معتبر |
| قواعد شاخص | نشست جاری revoke می‌شود؛ logout همه دستگاه‌ها نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `204` بدون بدنه |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401) |
| Controller | [AuthController.cs](../MakanApp.Api/Controllers/AuthController.cs) |
| متدهای service | `identityService.LogoutAsync` |
| Application | [IdentityService.cs](../MakanApp.Application/Identity/IdentityService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IIdentityStore` | `EfIdentityStore` | [EfIdentityStore.cs](../MakanApp.Infrastructure/Identity/EfIdentityStore.cs) |
| `IIdentitySecurity` | `IdentityCryptography` | [IdentityCryptography.cs](../MakanApp.Infrastructure/Identity/IdentityCryptography.cs) |
| `IOtpDeliveryProvider` | `DevelopmentOtpProvider` / `MeliPayamakOtpProvider` / `UnavailableOtpDeliveryProvider`؛ انتخاب با `Sms:Provider` | [provider توسعه/Unavailable](../MakanApp.Infrastructure/Identity/DevelopmentOtpProvider.cs) و [MeliPayamakOtpProvider](../MakanApp.Infrastructure/Identity/Otp/MeliPayamak/MeliPayamakOtpProvider.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/auth/logout HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `204`:

```http
HTTP/1.1 204 No Content
```

<a id="operation-004"></a>
### 004. دریافت حساب جاری

```http
GET /api/v1/me
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Identity`؛ `ProfileController.GetCurrentUser` |
| احراز هویت و context | نشست معتبر؛ فقط حساب جاری |
| قواعد شاخص | اطلاعات فقط حساب جاری؛ نبود پروفایل کامل وضعیت onboarding است، نه مجوز ساخت نقش سازمانی. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [CurrentUserResult](#type-currentuserresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401) |
| Controller | [ProfileController.cs](../MakanApp.Api/Controllers/ProfileController.cs) |
| متدهای service | `identityService.GetCurrentUserAsync` |
| Application | [IdentityService.cs](../MakanApp.Application/Identity/IdentityService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IIdentityStore` | `EfIdentityStore` | [EfIdentityStore.cs](../MakanApp.Infrastructure/Identity/EfIdentityStore.cs) |
| `IIdentitySecurity` | `IdentityCryptography` | [IdentityCryptography.cs](../MakanApp.Infrastructure/Identity/IdentityCryptography.cs) |
| `IOtpDeliveryProvider` | `DevelopmentOtpProvider` / `MeliPayamakOtpProvider` / `UnavailableOtpDeliveryProvider`؛ انتخاب با `Sms:Provider` | [provider توسعه/Unavailable](../MakanApp.Infrastructure/Identity/DevelopmentOtpProvider.cs) و [MeliPayamakOtpProvider](../MakanApp.Infrastructure/Identity/Otp/MeliPayamak/MeliPayamakOtpProvider.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/me HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "phoneNumber": "+989120000000",
  "isProfileComplete": true,
  "firstName": "کاربر",
  "lastName": "نمونه",
  "displayName": "کاربر نمونه",
  "username": "sample.user"
}
```

<a id="operation-005"></a>
### 005. تکمیل یا ویرایش مشخصات مجاز

```http
PATCH /api/v1/me/profile
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Identity`؛ `ProfileController.CompleteProfile` |
| احراز هویت و context | نشست معتبر؛ فقط حساب جاری |
| قواعد شاخص | نام/نام‌خانوادگی ۱..۱۰۰؛ displayName تا ۲۰۰؛ username لاتین ۳..۳۲ و یکتا. |
| ورودی | [CompleteProfileCommand](#type-completeprofilecommand) |
| خروجی موفق | `200` [CurrentUserResult](#type-currentuserresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [PROFILE_VALIDATION_FAILED](#error-profile-validation-failed) (400)، [USERNAME_ALREADY_EXISTS](#error-username-already-exists) (409) |
| Controller | [ProfileController.cs](../MakanApp.Api/Controllers/ProfileController.cs) |
| متدهای service | `identityService.CompleteProfileAsync` |
| Application | [IdentityService.cs](../MakanApp.Application/Identity/IdentityService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IIdentityStore` | `EfIdentityStore` | [EfIdentityStore.cs](../MakanApp.Infrastructure/Identity/EfIdentityStore.cs) |
| `IIdentitySecurity` | `IdentityCryptography` | [IdentityCryptography.cs](../MakanApp.Infrastructure/Identity/IdentityCryptography.cs) |
| `IOtpDeliveryProvider` | `DevelopmentOtpProvider` / `MeliPayamakOtpProvider` / `UnavailableOtpDeliveryProvider`؛ انتخاب با `Sms:Provider` | [provider توسعه/Unavailable](../MakanApp.Infrastructure/Identity/DevelopmentOtpProvider.cs) و [MeliPayamakOtpProvider](../MakanApp.Infrastructure/Identity/Otp/MeliPayamak/MeliPayamakOtpProvider.cs) |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/me/profile HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "firstName": "کاربر",
  "lastName": "نمونه",
  "displayName": "کاربر نمونه",
  "username": "sample.user"
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "phoneNumber": "+989120000000",
  "isProfileComplete": true,
  "firstName": "کاربر",
  "lastName": "نمونه",
  "displayName": "کاربر نمونه",
  "username": "sample.user"
}
```

<a id="operation-006"></a>
### 006. فهرست فضاها و نقش‌های معتبر خود

```http
GET /api/v1/workspaces/me
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Organization`؛ `WorkspacesController.GetMyWorkspaces` |
| احراز هویت و context | نشست معتبر؛ عضویت و نقش خود کاربر |
| قواعد شاخص | فضای شخصی و فضاهای حاصل از عضویت/نقش معتبر خود حساب؛ فهرست همه سازمان‌ها نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;WorkspaceResult&gt;](#type-workspaceresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403) |
| Controller | [WorkspacesController.cs](../MakanApp.Api/Controllers/WorkspacesController.cs) |
| متدهای service | `organizationService.GetMyWorkspacesAsync` |
| Application | [OrganizationService.cs](../MakanApp.Application/Organization/OrganizationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IOrganizationStore` | `EfOrganizationStore` | [EfOrganizationStore.cs](../MakanApp.Infrastructure/Organization/EfOrganizationStore.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/workspaces/me HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "workspaceType": "Organization",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "organizationName": "آموزشگاه نمونه",
    "membershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
    "role": "Student",
    "displayName": "دانش‌آموز — آموزشگاه نمونه",
    "status": "Active"
  }
]
```

<a id="operation-007"></a>
### 007. ثبت فضای کاری و نقش در نشست

```http
POST /api/v1/workspaces/select
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Organization`؛ `WorkspacesController.SelectWorkspace` |
| احراز هویت و context | نشست معتبر؛ عضویت و نقش خود کاربر |
| قواعد شاخص | Organization نیازمند membershipId و role معتبر خود؛ Personal با nullها؛ انتخاب child پاک می‌شود. |
| ورودی | [SelectWorkspaceCommand](#type-selectworkspacecommand) |
| خروجی موفق | `200` [AccessContext](#type-accesscontext) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [MEMBERSHIP_NOT_ACTIVE](#error-membership-not-active) (403)، [ROLE_NOT_ACTIVE](#error-role-not-active) (403) |
| Controller | [WorkspacesController.cs](../MakanApp.Api/Controllers/WorkspacesController.cs) |
| متدهای service | `organizationService.SelectWorkspaceAsync` |
| Application | [OrganizationService.cs](../MakanApp.Application/Organization/OrganizationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IOrganizationStore` | `EfOrganizationStore` | [EfOrganizationStore.cs](../MakanApp.Infrastructure/Organization/EfOrganizationStore.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/workspaces/select HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "workspaceType": "Organization",
  "membershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
  "role": "Student"
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "actorId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "sessionId": "c9669a44-0ec7-5647-9277-a60ffa1a10c4",
  "workspaceType": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "membershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
  "activeRole": "Student",
  "subjectOrganizationPersonId": null
}
```

<a id="operation-008"></a>
### 008. دریافت context معتبر جاری

```http
GET /api/v1/workspaces/current
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Organization`؛ `WorkspacesController.GetCurrentAccessContext` |
| احراز هویت و context | نشست معتبر؛ عضویت و نقش خود کاربر |
| قواعد شاخص | context جاری با عضویت و نقش و child مجدداً validate می‌شود. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [AccessContext](#type-accesscontext) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403) |
| Controller | [WorkspacesController.cs](../MakanApp.Api/Controllers/WorkspacesController.cs) |
| متدهای service | `accessContextResolver.ResolveAsync` |
| Application | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs)، [OrganizationService.cs](../MakanApp.Application/Organization/OrganizationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IOrganizationService` | `OrganizationService` | [OrganizationService.cs](../MakanApp.Application/Organization/OrganizationService.cs) |
| `IGuardianStore` | `EfGuardianStore` | [EfGuardianStore.cs](../MakanApp.Infrastructure/Guardian/EfGuardianStore.cs) |
| `IOrganizationStore` | `EfOrganizationStore` | [EfOrganizationStore.cs](../MakanApp.Infrastructure/Organization/EfOrganizationStore.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/workspaces/current HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "actorId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "sessionId": "c9669a44-0ec7-5647-9277-a60ffa1a10c4",
  "workspaceType": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "membershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
  "activeRole": "Student",
  "subjectOrganizationPersonId": null
}
```

<a id="operation-009"></a>
### 009. فهرست دعوت‌های حساب

```http
GET /api/v1/invitations
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Organization`؛ `InvitationsController.GetMyInvitations` |
| احراز هویت و context | نشست معتبر؛ دعوت متعلق به حساب جاری |
| قواعد شاخص | دعوت‌های متعلق به حساب جاری؛ نتیجه فهرست، مجوز پذیرش دعوت متعلق به دیگری نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;InvitationResult&gt;](#type-invitationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [INVITATION_NOT_FOUND](#error-invitation-not-found) (404) |
| Controller | [InvitationsController.cs](../MakanApp.Api/Controllers/InvitationsController.cs) |
| متدهای service | `organizationService.GetMyInvitationsAsync` |
| Application | [OrganizationService.cs](../MakanApp.Application/Organization/OrganizationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IOrganizationStore` | `EfOrganizationStore` | [EfOrganizationStore.cs](../MakanApp.Infrastructure/Organization/EfOrganizationStore.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/invitations HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "id": "5758c8a1-fdd3-5242-b56f-b4cb2f88d84b",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "organizationName": "آموزشگاه نمونه",
    "invitedByUserId": "527d7696-f197-517f-9c41-922ac6e06b3b",
    "role": "Student",
    "status": "Pending",
    "expiresAtUtc": "2026-11-01T09:05:00Z"
  }
]
```

<a id="operation-010"></a>
### 010. پذیرش دعوت و تأمین عضویت/نقش

```http
POST /api/v1/invitations/{invitationId}/accept
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Organization`؛ `InvitationsController.AcceptInvitation` |
| احراز هویت و context | نشست معتبر؛ دعوت متعلق به حساب جاری |
| قواعد شاخص | فقط مخاطب؛ سازمان فعال؛ accept تکراری همان نتیجه؛ ثبت‌نام/انتخاب فضا خودکار نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [AcceptInvitationResult](#type-acceptinvitationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [INVITATION_NOT_FOUND](#error-invitation-not-found) (404)، [INVITATION_EXPIRED](#error-invitation-expired) (410)، [INVITATION_REVOKED](#error-invitation-revoked) (410) |
| Controller | [InvitationsController.cs](../MakanApp.Api/Controllers/InvitationsController.cs) |
| متدهای service | `organizationService.AcceptInvitationAsync` |
| Application | [OrganizationService.cs](../MakanApp.Application/Organization/OrganizationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IOrganizationStore` | `EfOrganizationStore` | [EfOrganizationStore.cs](../MakanApp.Infrastructure/Organization/EfOrganizationStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `invitationId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/invitations/5758c8a1-fdd3-5242-b56f-b4cb2f88d84b/accept HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "invitationId": "5758c8a1-fdd3-5242-b56f-b4cb2f88d84b",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "organizationName": "آموزشگاه نمونه",
  "membershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
  "roleAssignmentId": "d4b89a63-b579-59af-be13-c7f823916d0c",
  "role": "Student",
  "alreadyAccepted": false
}
```

<a id="operation-011"></a>
### 011. رد دعوت

```http
POST /api/v1/invitations/{invitationId}/decline
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Organization`؛ `InvitationsController.DeclineInvitation` |
| احراز هویت و context | نشست معتبر؛ دعوت متعلق به حساب جاری |
| قواعد شاخص | پذیرفته‌شده را نمی‌توان decline کرد؛ وضعیت مؤثر expiry لحاظ می‌شود. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [DeclineInvitationResult](#type-declineinvitationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [INVITATION_NOT_FOUND](#error-invitation-not-found) (404)، [INVITATION_ALREADY_ACCEPTED](#error-invitation-already-accepted) (409) |
| Controller | [InvitationsController.cs](../MakanApp.Api/Controllers/InvitationsController.cs) |
| متدهای service | `organizationService.DeclineInvitationAsync` |
| Application | [OrganizationService.cs](../MakanApp.Application/Organization/OrganizationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IOrganizationStore` | `EfOrganizationStore` | [EfOrganizationStore.cs](../MakanApp.Infrastructure/Organization/EfOrganizationStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `invitationId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/invitations/5758c8a1-fdd3-5242-b56f-b4cb2f88d84b/decline HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "invitationId": "5758c8a1-fdd3-5242-b56f-b4cb2f88d84b",
  "status": "Declined"
}
```

<a id="operation-012"></a>
### 012. فهرست فرزندان مجاز سازمان جاری

```http
GET /api/v1/guardian/children
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Guardian`؛ `GuardianController.GetMyChildren` |
| احراز هویت و context | Organization / Parent با رابطه معتبر همان سازمان |
| قواعد شاخص | نقش Parent در سازمان؛ لیست معتبر همان سازمان، حتی برای اصلاح child قبلی. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;AuthorizedChildResult&gt;](#type-authorizedchildresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [PARENT_ROLE_REQUIRED](#error-parent-role-required) (403) |
| Controller | [GuardianController.cs](../MakanApp.Api/Controllers/GuardianController.cs) |
| متدهای service | `guardianService.GetMyAuthorizedChildrenAsync` |
| Application | [GuardianService.cs](../MakanApp.Application/Guardian/GuardianService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IOrganizationService` | `OrganizationService` | [OrganizationService.cs](../MakanApp.Application/Organization/OrganizationService.cs) |
| `IGuardianStore` | `EfGuardianStore` | [EfGuardianStore.cs](../MakanApp.Infrastructure/Guardian/EfGuardianStore.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/guardian/children HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "learnerOrganizationPersonId": "ca792355-953b-56c9-8371-63d9a1122800",
    "displayName": "کاربر نمونه",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "organizationName": "آموزشگاه نمونه",
    "relationStatus": "Active"
  }
]
```

<a id="operation-013"></a>
### 013. ثبت فرزند منتخب در نشست

```http
POST /api/v1/guardian/children/{organizationPersonId}/select
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Guardian`؛ `GuardianController.SelectChildContext` |
| احراز هویت و context | Organization / Parent با رابطه معتبر همان سازمان |
| قواعد شاخص | organizationPersonId همان فرزند برگشتی children؛ رابطه فعال در سازمان جاری. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [AccessContext](#type-accesscontext) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CHILD_CONTEXT_NOT_FOUND](#error-child-context-not-found) (404)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403)، [PARENT_ROLE_REQUIRED](#error-parent-role-required) (403) |
| Controller | [GuardianController.cs](../MakanApp.Api/Controllers/GuardianController.cs) |
| متدهای service | `guardianService.SelectChildContextAsync` |
| Application | [GuardianService.cs](../MakanApp.Application/Guardian/GuardianService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IOrganizationService` | `OrganizationService` | [OrganizationService.cs](../MakanApp.Application/Organization/OrganizationService.cs) |
| `IGuardianStore` | `EfGuardianStore` | [EfGuardianStore.cs](../MakanApp.Infrastructure/Guardian/EfGuardianStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `organizationPersonId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/guardian/children/ca792355-953b-56c9-8371-63d9a1122800/select HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "actorId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "sessionId": "c9669a44-0ec7-5647-9277-a60ffa1a10c4",
  "workspaceType": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "membershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
  "activeRole": "Parent",
  "subjectOrganizationPersonId": "ca792355-953b-56c9-8371-63d9a1122800"
}
```

<a id="operation-014"></a>
### 014. خلاصه رابطه سرپرستی مجاز

```http
GET /api/v1/guardian/relations/{organizationPersonId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Guardian`؛ `GuardianController.GetRelationSummary` |
| احراز هویت و context | Organization / Parent با رابطه معتبر همان سازمان |
| قواعد شاخص | خلاصه رابطه خود با همان پرونده سازمانی؛ وجود رابطه دیگران پنهان می‌شود. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [GuardianRelationSummaryResult](#type-guardianrelationsummaryresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [GUARDIAN_RELATION_NOT_FOUND](#error-guardian-relation-not-found) (404)، [PARENT_ROLE_REQUIRED](#error-parent-role-required) (403) |
| Controller | [GuardianController.cs](../MakanApp.Api/Controllers/GuardianController.cs) |
| متدهای service | `guardianService.GetGuardianRelationSummaryAsync` |
| Application | [GuardianService.cs](../MakanApp.Application/Guardian/GuardianService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IOrganizationService` | `OrganizationService` | [OrganizationService.cs](../MakanApp.Application/Organization/OrganizationService.cs) |
| `IGuardianStore` | `EfGuardianStore` | [EfGuardianStore.cs](../MakanApp.Infrastructure/Guardian/EfGuardianStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `organizationPersonId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/guardian/relations/ca792355-953b-56c9-8371-63d9a1122800 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "relationId": "37db6870-8812-58d2-bed2-c099d8b125af",
  "learnerOrganizationPersonId": "ca792355-953b-56c9-8371-63d9a1122800",
  "displayName": "کاربر نمونه",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "organizationName": "آموزشگاه نمونه",
  "status": "Active",
  "validFromUtc": "2026-11-01T09:00:00Z",
  "createdAtUtc": "2026-11-01T09:00:00Z"
}
```

<a id="operation-015"></a>
### 015. ساخت دوره آموزشی

```http
POST /api/v1/academic/periods
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `AcademicPeriodsController.Create` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | Manager؛ title غیرخالی تا ۲۰۰؛ endDate >= startDate. |
| ورودی | [CreateAcademicPeriodCommand](#type-createacademicperiodcommand) |
| خروجی موفق | `201` [AcademicPeriodResult](#type-academicperiodresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [MANAGER_ROLE_REQUIRED](#error-manager-role-required) (403)، [ACADEMIC_PERIOD_INVALID](#error-academic-period-invalid) (400) |
| Controller | [AcademicPeriodsController.cs](../MakanApp.Api/Controllers/AcademicPeriodsController.cs) |
| متدهای service | `academicService.CreateAcademicPeriodAsync` |
| Application | [AcademicService.cs](../MakanApp.Application/Academic/AcademicService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicStore` | `EfAcademicStore` | [EfAcademicStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicStore.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/periods HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "title": "کلاس نمونه",
  "startDate": "2026-11-01",
  "endDate": "2026-12-01"
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "1b2f71c0-92af-512f-8397-452ed77fb53a",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "title": "کلاس نمونه",
  "startDate": "2026-11-01",
  "endDate": "2026-12-01",
  "status": "Active"
}
```

<a id="operation-016"></a>
### 016. ساخت درس سازمان

```http
POST /api/v1/academic/courses
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `CoursesController.Create` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | Manager؛ title غیرخالی تا ۲۰۰؛ بدون academicPeriodId در command. |
| ورودی | [CreateCourseCommand](#type-createcoursecommand) |
| خروجی موفق | `201` [CourseResult](#type-courseresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [MANAGER_ROLE_REQUIRED](#error-manager-role-required) (403)، [COURSE_INVALID](#error-course-invalid) (400) |
| Controller | [CoursesController.cs](../MakanApp.Api/Controllers/CoursesController.cs) |
| متدهای service | `academicService.CreateCourseAsync` |
| Application | [AcademicService.cs](../MakanApp.Application/Academic/AcademicService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicStore` | `EfAcademicStore` | [EfAcademicStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicStore.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/courses HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "title": "کلاس نمونه"
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "3800a30c-b29b-57ad-9544-76c274362f67",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "title": "کلاس نمونه",
  "status": "Active"
}
```

<a id="operation-017"></a>
### 017. ساخت کلاس با دوره و درس

```http
POST /api/v1/academic/classes
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `ClassesController.Create` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | Manager؛ دوره/درس همان سازمان؛ title تا ۲۰۰؛ capacity>0؛ activateImmediately تعیین Draft/Active. |
| ورودی | [CreateClassCommand](#type-createclasscommand) |
| خروجی موفق | `201` [ClassResult](#type-classresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CLASS_INVALID](#error-class-invalid) (400)، [ACADEMIC_PERIOD_NOT_FOUND](#error-academic-period-not-found) (404)، [COURSE_NOT_FOUND](#error-course-not-found) (404)، [MANAGER_ROLE_REQUIRED](#error-manager-role-required) (403) |
| Controller | [ClassesController.cs](../MakanApp.Api/Controllers/ClassesController.cs) |
| متدهای service | `academicService.CreateClassAsync` |
| Application | [AcademicService.cs](../MakanApp.Application/Academic/AcademicService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicStore` | `EfAcademicStore` | [EfAcademicStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicStore.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/classes HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "academicPeriodId": "1b2f71c0-92af-512f-8397-452ed77fb53a",
  "courseId": "3800a30c-b29b-57ad-9544-76c274362f67",
  "title": "کلاس نمونه",
  "capacity": 20,
  "activateImmediately": true
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "academicPeriodId": "1b2f71c0-92af-512f-8397-452ed77fb53a",
  "academicPeriodTitle": "دوره نمونه",
  "courseId": "3800a30c-b29b-57ad-9544-76c274362f67",
  "courseTitle": "درس نمونه",
  "title": "کلاس نمونه",
  "capacity": 20,
  "status": "Active"
}
```

<a id="operation-018"></a>
### 018. فهرست کلاس‌های قابل دسترسی

```http
GET /api/v1/academic/classes
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `ClassesController.GetClasses` |
| احراز هویت و context | Organization / Manager، Teacher منتسب، Student enrolled؛ Parent ممنوع |
| قواعد شاخص | Manager سازمان؛ Teacher منتسب؛ Student enrolled؛ Parent این مسیر را ندارد. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;ClassResult&gt;](#type-classresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ACADEMIC_READ_NOT_ALLOWED](#error-academic-read-not-allowed) (403) |
| Controller | [ClassesController.cs](../MakanApp.Api/Controllers/ClassesController.cs) |
| متدهای service | `academicService.GetClassesAsync` |
| Application | [AcademicService.cs](../MakanApp.Application/Academic/AcademicService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicStore` | `EfAcademicStore` | [EfAcademicStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicStore.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/classes HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "id": "c16b36b1-18ac-51ec-be7e-e518764ad372",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "academicPeriodId": "1b2f71c0-92af-512f-8397-452ed77fb53a",
    "academicPeriodTitle": "دوره نمونه",
    "courseId": "3800a30c-b29b-57ad-9544-76c274362f67",
    "courseTitle": "درس نمونه",
    "title": "کلاس نمونه",
    "capacity": 20,
    "status": "Active"
  }
]
```

<a id="operation-019"></a>
### 019. ثبت‌نام پرونده دانش‌آموز

```http
POST /api/v1/academic/classes/{classId}/enrollments
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `ClassesController.EnrollLearner` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | Manager؛ کلاس Active؛ پرونده همان سازمان؛ ظرفیت و یکتایی enrollment فعال اتمیک. |
| ورودی | [EnrollLearnerCommand](#type-enrolllearnercommand) |
| خروجی موفق | `201` [EnrollmentResult](#type-enrollmentresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CLASS_CAPACITY_EXCEEDED](#error-class-capacity-exceeded) (409)، [ENROLLMENT_ALREADY_ACTIVE](#error-enrollment-already-active) (409)، [LEARNER_NOT_FOUND](#error-learner-not-found) (404)، [CLASS_NOT_ACTIVE](#error-class-not-active) (409) |
| Controller | [ClassesController.cs](../MakanApp.Api/Controllers/ClassesController.cs) |
| متدهای service | `academicService.EnrollLearnerAsync` |
| Application | [AcademicService.cs](../MakanApp.Application/Academic/AcademicService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicStore` | `EfAcademicStore` | [EfAcademicStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/enrollments HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "learnerOrganizationPersonId": "ca792355-953b-56c9-8371-63d9a1122800"
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "learnerOrganizationPersonId": "ca792355-953b-56c9-8371-63d9a1122800",
  "status": "Active",
  "enrolledAtUtc": "2026-11-01T09:00:00Z",
  "endedAtUtc": null
}
```

<a id="operation-020"></a>
### 020. پایان ثبت‌نام با حفظ سابقه

```http
POST /api/v1/academic/classes/{classId}/enrollments/{enrollmentId}/end
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `ClassesController.EndEnrollment` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | Manager؛ enrollment همان کلاس؛ finalStatus فقط Completed/Withdrawn. |
| ورودی | [EndEnrollmentCommand](#type-endenrollmentcommand) |
| خروجی موفق | `200` [EnrollmentResult](#type-enrollmentresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ENROLLMENT_NOT_ACTIVE](#error-enrollment-not-active) (409)، [CLASS_NOT_FOUND](#error-class-not-found) (404) |
| Controller | [ClassesController.cs](../MakanApp.Api/Controllers/ClassesController.cs) |
| متدهای service | `academicService.EndEnrollmentAsync` |
| Application | [AcademicService.cs](../MakanApp.Application/Academic/AcademicService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicStore` | `EfAcademicStore` | [EfAcademicStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |
| `enrollmentId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/enrollments/1b1378f1-12be-5d66-81ab-b8896898c3fa/end HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "finalStatus": "Completed"
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "learnerOrganizationPersonId": "ca792355-953b-56c9-8371-63d9a1122800",
  "status": "Completed",
  "enrolledAtUtc": "2026-11-01T09:00:00Z",
  "endedAtUtc": "2026-11-01T09:00:00Z"
}
```

<a id="operation-021"></a>
### 021. انتساب عضویت معلم به کلاس

```http
POST /api/v1/academic/classes/{classId}/teachers
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `ClassesController.AssignTeacher` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | Manager؛ membership معلم فعال و نقش Teacher همان سازمان؛ رابطه فعال تکراری رد. |
| ورودی | [AssignTeacherCommand](#type-assignteachercommand) |
| خروجی موفق | `201` [TeacherAssignmentResult](#type-teacherassignmentresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [TEACHER_NOT_ALLOWED](#error-teacher-not-allowed) (403)، [TEACHER_ASSIGNMENT_ALREADY_ACTIVE](#error-teacher-assignment-already-active) (409) |
| Controller | [ClassesController.cs](../MakanApp.Api/Controllers/ClassesController.cs) |
| متدهای service | `academicService.AssignTeacherAsync` |
| Application | [AcademicService.cs](../MakanApp.Application/Academic/AcademicService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicStore` | `EfAcademicStore` | [EfAcademicStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/teachers HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "teacherMembershipId": "e5cb7247-c89f-539a-bc78-dfd99fc09e53"
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "d7d6f2ee-0110-5f2c-8f7c-499fc717a580",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "teacherMembershipId": "e5cb7247-c89f-539a-bc78-dfd99fc09e53",
  "status": "Active",
  "assignedAtUtc": "2026-11-01T09:00:00Z",
  "endedAtUtc": null
}
```

<a id="operation-022"></a>
### 022. پایان انتساب معلم

```http
POST /api/v1/academic/classes/{classId}/teachers/{teacherAssignmentId}/end
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `ClassesController.EndTeacherAssignment` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | Manager؛ انتساب همان کلاس؛ body ندارد؛ پایان رابطه با حفظ سابقه. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [TeacherAssignmentResult](#type-teacherassignmentresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [TEACHER_ASSIGNMENT_NOT_ACTIVE](#error-teacher-assignment-not-active) (409)، [CLASS_NOT_FOUND](#error-class-not-found) (404) |
| Controller | [ClassesController.cs](../MakanApp.Api/Controllers/ClassesController.cs) |
| متدهای service | `academicService.EndTeacherAssignmentAsync` |
| Application | [AcademicService.cs](../MakanApp.Application/Academic/AcademicService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicStore` | `EfAcademicStore` | [EfAcademicStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |
| `teacherAssignmentId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/teachers/d7d6f2ee-0110-5f2c-8f7c-499fc717a580/end HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "d7d6f2ee-0110-5f2c-8f7c-499fc717a580",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "teacherMembershipId": "e5cb7247-c89f-539a-bc78-dfd99fc09e53",
  "status": "Ended",
  "assignedAtUtc": "2026-11-01T09:00:00Z",
  "endedAtUtc": "2026-11-01T09:00:00Z"
}
```

<a id="operation-023"></a>
### 023. برنامه جلسات در بازه UTC

```http
GET /api/v1/academic/schedule
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `ScheduleController.GetSchedule` |
| احراز هویت و context | Organization؛ نوشتن Manager/Teacher منتسب؛ خواندن رابطه کلاس/child معتبر |
| قواعد شاخص | fromUtc/toUtc الزامی UTC؛ to>from و حداکثر یک سال؛ خواندن بر اساس رابطه کلاس/child. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;SessionResult&gt;](#type-sessionresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SESSION_TIME_INVALID](#error-session-time-invalid) (400)، [SESSION_NOT_FOUND](#error-session-not-found) (404) |
| Controller | [ScheduleController.cs](../MakanApp.Api/Controllers/ScheduleController.cs) |
| متدهای service | `sessionService.GetScheduleAsync` |
| Application | [AcademicSessionService.cs](../MakanApp.Application/Academic/AcademicSessionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicSessionStore` | `EfAcademicSessionStore` | [EfAcademicSessionStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicSessionStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `fromUtc` | query | `DateTime` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/schedule?fromUtc=2026-11-01T00%3A00%3A00Z&toUtc=2026-11-08T00%3A00%3A00Z HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "id": "21e7d33b-2774-5284-a4d1-daa30ee91152",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
    "classTitle": "کلاس نمونه",
    "scheduleRuleId": null,
    "title": "کلاس نمونه",
    "startUtc": "2026-11-01T09:00:00Z",
    "endUtc": "2026-11-01T09:30:00Z",
    "timeZoneId": "Asia/Tehran",
    "meetingUrl": "https://example.com/meeting",
    "status": "Scheduled",
    "cancelledAtUtc": null,
    "completedAtUtc": null,
    "rowVersion": "AAAAAAAAAAE="
  }
]
```

<a id="operation-024"></a>
### 024. ساخت الگوی تکرار و جلسات آینده

```http
POST /api/v1/academic/classes/{classId}/schedule-rules
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `ScheduleController.CreateRule` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب همان کلاس |
| قواعد شاخص | روز معتبر، مدت ۱..۱۴۴۰، بازه حداکثر یک سال، timezone معتبر؛ رخداد آینده بدون تداخل. |
| ورودی | [CreateScheduleRuleCommand](#type-createschedulerulecommand) |
| خروجی موفق | `201` [ScheduleRuleResult](#type-scheduleruleresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SESSION_TIME_INVALID](#error-session-time-invalid) (400)، [CLASS_SESSION_CONFLICT](#error-class-session-conflict) (409)، [TEACHER_SESSION_CONFLICT](#error-teacher-session-conflict) (409) |
| Controller | [ScheduleController.cs](../MakanApp.Api/Controllers/ScheduleController.cs) |
| متدهای service | `sessionService.CreateScheduleRuleAsync` |
| Application | [AcademicSessionService.Schedule.cs](../MakanApp.Application/Academic/AcademicSessionService.Schedule.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/schedule-rules HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "localDayOfWeek": "Sunday",
  "localStartTime": "12:30:00",
  "durationMinutes": 30,
  "timeZoneId": "Asia/Tehran",
  "effectiveFrom": "2026-11-01",
  "effectiveUntil": "2026-12-01",
  "sessionTitle": "جلسه نمونه",
  "meetingUrl": "https://example.com/meeting"
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "ab11b85b-28f4-5efc-9692-01531499c21c",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "localDayOfWeek": "Sunday",
  "localStartTime": "12:30:00",
  "durationMinutes": 30,
  "timeZoneId": "Asia/Tehran",
  "effectiveFrom": "2026-11-01",
  "effectiveUntil": "2026-12-01",
  "sessionTitle": "جلسه نمونه",
  "meetingUrl": "https://example.com/meeting",
  "status": "Active",
  "generatedSessionCount": 5,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-025"></a>
### 025. اصلاح الگو و رخدادهای آینده

```http
PATCH /api/v1/academic/schedule-rules/{scheduleRuleId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `ScheduleController.UpdateRule` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب همان کلاس |
| قواعد شاخص | قواعد create به‌علاوه applyFromDate آینده و expectedRowVersion؛ گذشته بازنویسی نمی‌شود. |
| ورودی | [UpdateScheduleRuleCommand](#type-updateschedulerulecommand) |
| خروجی موفق | `200` [ScheduleRuleResult](#type-scheduleruleresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SCHEDULE_RULE_NOT_FOUND](#error-schedule-rule-not-found) (404)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412)، [CLASS_SESSION_CONFLICT](#error-class-session-conflict) (409) |
| Controller | [ScheduleController.cs](../MakanApp.Api/Controllers/ScheduleController.cs) |
| متدهای service | `sessionService.UpdateScheduleRuleAsync` |
| Application | [AcademicSessionService.Schedule.cs](../MakanApp.Application/Academic/AcademicSessionService.Schedule.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `scheduleRuleId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/academic/schedule-rules/ab11b85b-28f4-5efc-9692-01531499c21c HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "localDayOfWeek": "Sunday",
  "localStartTime": "12:30:00",
  "durationMinutes": 30,
  "timeZoneId": "Asia/Tehran",
  "effectiveFrom": "2026-11-01",
  "effectiveUntil": "2026-12-01",
  "applyFromDate": "2026-11-01",
  "sessionTitle": "جلسه نمونه",
  "meetingUrl": "https://example.com/meeting",
  "expectedRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "ab11b85b-28f4-5efc-9692-01531499c21c",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "localDayOfWeek": "Sunday",
  "localStartTime": "12:30:00",
  "durationMinutes": 30,
  "timeZoneId": "Asia/Tehran",
  "effectiveFrom": "2026-11-01",
  "effectiveUntil": "2026-12-01",
  "sessionTitle": "جلسه نمونه",
  "meetingUrl": "https://example.com/meeting",
  "status": "Active",
  "generatedSessionCount": 5,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-026"></a>
### 026. ساخت جلسه مستقل

```http
POST /api/v1/academic/classes/{classId}/sessions
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `SessionsController.Create` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب همان کلاس |
| قواعد شاخص | Manager/Teacher منتسب؛ شروع آینده UTC، پایان بعد از شروع، timezone و URL معتبر. |
| ورودی | [CreateSessionCommand](#type-createsessioncommand) |
| خروجی موفق | `201` [SessionResult](#type-sessionresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SESSION_TIME_INVALID](#error-session-time-invalid) (400)، [CLASS_SESSION_CONFLICT](#error-class-session-conflict) (409)، [TEACHER_SESSION_CONFLICT](#error-teacher-session-conflict) (409) |
| Controller | [SessionsController.cs](../MakanApp.Api/Controllers/SessionsController.cs) |
| متدهای service | `sessionService.CreateSessionAsync` |
| Application | [AcademicSessionService.cs](../MakanApp.Application/Academic/AcademicSessionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicSessionStore` | `EfAcademicSessionStore` | [EfAcademicSessionStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicSessionStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/sessions HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "title": "کلاس نمونه",
  "startUtc": "2026-11-01T09:00:00Z",
  "endUtc": "2026-11-01T09:30:00Z",
  "timeZoneId": "Asia/Tehran",
  "meetingUrl": "https://example.com/meeting"
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "21e7d33b-2774-5284-a4d1-daa30ee91152",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "scheduleRuleId": null,
  "title": "کلاس نمونه",
  "startUtc": "2026-11-01T09:00:00Z",
  "endUtc": "2026-11-01T09:30:00Z",
  "timeZoneId": "Asia/Tehran",
  "meetingUrl": "https://example.com/meeting",
  "status": "Scheduled",
  "cancelledAtUtc": null,
  "completedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-027"></a>
### 027. جزئیات جلسه مجاز

```http
GET /api/v1/academic/sessions/{sessionId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `SessionsController.Get` |
| احراز هویت و context | Organization؛ مدیریت و حضور Manager/Teacher منتسب؛ جزئیات برای خواننده مجاز کلاس |
| قواعد شاخص | نشست، workspace و رابطه معتبر با کلاس جلسه؛ SessionId خارج scope حق خواندن نمی‌دهد. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [SessionResult](#type-sessionresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SESSION_NOT_FOUND](#error-session-not-found) (404) |
| Controller | [SessionsController.cs](../MakanApp.Api/Controllers/SessionsController.cs) |
| متدهای service | `sessionService.GetSessionAsync` |
| Application | [AcademicSessionService.Attendance.cs](../MakanApp.Application/Academic/AcademicSessionService.Attendance.cs)، [AcademicSessionService.cs](../MakanApp.Application/Academic/AcademicSessionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicSessionStore` | `EfAcademicSessionStore` | [EfAcademicSessionStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicSessionStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `sessionId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/sessions/c9669a44-0ec7-5647-9277-a60ffa1a10c4 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "21e7d33b-2774-5284-a4d1-daa30ee91152",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "scheduleRuleId": null,
  "title": "کلاس نمونه",
  "startUtc": "2026-11-01T09:00:00Z",
  "endUtc": "2026-11-01T09:30:00Z",
  "timeZoneId": "Asia/Tehran",
  "meetingUrl": "https://example.com/meeting",
  "status": "Scheduled",
  "cancelledAtUtc": null,
  "completedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-028"></a>
### 028. ویرایش جلسه با کنترل نسخه

```http
PATCH /api/v1/academic/sessions/{sessionId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `SessionsController.Update` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب همان کلاس |
| قواعد شاخص | نسخه هشت‌بایتی Base64 جاری؛ جلسه قابل ویرایش و زمان آینده معتبر. |
| ورودی | [UpdateSessionCommand](#type-updatesessioncommand) |
| خروجی موفق | `200` [SessionResult](#type-sessionresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SESSION_NOT_ACTIVE](#error-session-not-active) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [SessionsController.cs](../MakanApp.Api/Controllers/SessionsController.cs) |
| متدهای service | `sessionService.UpdateSessionAsync` |
| Application | [AcademicSessionService.cs](../MakanApp.Application/Academic/AcademicSessionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicSessionStore` | `EfAcademicSessionStore` | [EfAcademicSessionStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicSessionStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `sessionId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/academic/sessions/c9669a44-0ec7-5647-9277-a60ffa1a10c4 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "title": "کلاس نمونه",
  "startUtc": "2026-11-01T09:00:00Z",
  "endUtc": "2026-11-01T09:30:00Z",
  "timeZoneId": "Asia/Tehran",
  "meetingUrl": "https://example.com/meeting",
  "expectedRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "21e7d33b-2774-5284-a4d1-daa30ee91152",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "scheduleRuleId": null,
  "title": "کلاس نمونه",
  "startUtc": "2026-11-01T09:00:00Z",
  "endUtc": "2026-11-01T09:30:00Z",
  "timeZoneId": "Asia/Tehran",
  "meetingUrl": "https://example.com/meeting",
  "status": "Scheduled",
  "cancelledAtUtc": null,
  "completedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-029"></a>
### 029. لغو جلسه

```http
POST /api/v1/academic/sessions/{sessionId}/cancel
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `SessionsController.Cancel` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب همان کلاس |
| قواعد شاخص | مجوز مدیریت جلسه و expectedRowVersion؛ تغییر وضعیت دامنه. |
| ورودی | [SessionVersionCommand](#type-sessionversioncommand) |
| خروجی موفق | `200` [SessionResult](#type-sessionresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SESSION_NOT_ACTIVE](#error-session-not-active) (409)، [SESSION_NOT_FOUND](#error-session-not-found) (404)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [SessionsController.cs](../MakanApp.Api/Controllers/SessionsController.cs) |
| متدهای service | `sessionService.CancelSessionAsync` |
| Application | [AcademicSessionService.cs](../MakanApp.Application/Academic/AcademicSessionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicSessionStore` | `EfAcademicSessionStore` | [EfAcademicSessionStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicSessionStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `sessionId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/sessions/c9669a44-0ec7-5647-9277-a60ffa1a10c4/cancel HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "expectedRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "21e7d33b-2774-5284-a4d1-daa30ee91152",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "scheduleRuleId": null,
  "title": "کلاس نمونه",
  "startUtc": "2026-11-01T09:00:00Z",
  "endUtc": "2026-11-01T09:30:00Z",
  "timeZoneId": "Asia/Tehran",
  "meetingUrl": "https://example.com/meeting",
  "status": "Cancelled",
  "cancelledAtUtc": "2026-11-01T09:00:00Z",
  "completedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-030"></a>
### 030. ثبت تکمیل جلسه

```http
POST /api/v1/academic/sessions/{sessionId}/complete
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `SessionsController.Complete` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب همان کلاس |
| قواعد شاخص | مجوز مدیریت جلسه، زمان و وضعیت مجاز و expectedRowVersion. |
| ورودی | [SessionVersionCommand](#type-sessionversioncommand) |
| خروجی موفق | `200` [SessionResult](#type-sessionresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SESSION_NOT_ACTIVE](#error-session-not-active) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [SessionsController.cs](../MakanApp.Api/Controllers/SessionsController.cs) |
| متدهای service | `sessionService.CompleteSessionAsync` |
| Application | [AcademicSessionService.cs](../MakanApp.Application/Academic/AcademicSessionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAcademicSessionStore` | `EfAcademicSessionStore` | [EfAcademicSessionStore.cs](../MakanApp.Infrastructure/Academic/EfAcademicSessionStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `sessionId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/sessions/c9669a44-0ec7-5647-9277-a60ffa1a10c4/complete HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "expectedRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "21e7d33b-2774-5284-a4d1-daa30ee91152",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "scheduleRuleId": null,
  "title": "کلاس نمونه",
  "startUtc": "2026-11-01T09:00:00Z",
  "endUtc": "2026-11-01T09:30:00Z",
  "timeZoneId": "Asia/Tehran",
  "meetingUrl": "https://example.com/meeting",
  "status": "Completed",
  "cancelledAtUtc": null,
  "completedAtUtc": "2026-11-01T09:00:00Z",
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-031"></a>
### 031. دفتر حضور و تاریخچه اصلاح

```http
GET /api/v1/academic/sessions/{sessionId}/attendance
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `SessionsController.GetAttendance` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب همان کلاس |
| قواعد شاخص | فقط Manager/Teacher منتسب؛ وضعیت NotRecorded برای ثبت‌نشده و revisions برای اصلاح. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [SessionAttendanceResult](#type-sessionattendanceresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ATTENDANCE_NOT_ALLOWED](#error-attendance-not-allowed) (403)، [SESSION_NOT_FOUND](#error-session-not-found) (404) |
| Controller | [SessionsController.cs](../MakanApp.Api/Controllers/SessionsController.cs) |
| متدهای service | `sessionService.GetSessionAttendanceAsync` |
| Application | [AcademicSessionService.Attendance.cs](../MakanApp.Application/Academic/AcademicSessionService.Attendance.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `sessionId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/sessions/c9669a44-0ec7-5647-9277-a60ffa1a10c4/attendance HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "sessionId": "c9669a44-0ec7-5647-9277-a60ffa1a10c4",
  "sessionStatus": "Scheduled",
  "entries": [
    {
      "attendanceId": "be2b6575-b733-5b4e-8eea-8fff6dd811e0",
      "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
      "learnerOrganizationPersonId": "ca792355-953b-56c9-8371-63d9a1122800",
      "status": "Present",
      "recordedAtUtc": "2026-11-01T09:00:00Z",
      "recordedByMembershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
      "rowVersion": "AAAAAAAAAAE="
    }
  ],
  "revisions": []
}
```

<a id="operation-032"></a>
### 032. ثبت اولیه حضور دانش‌آموزان

```http
PUT /api/v1/academic/sessions/{sessionId}/attendance
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `SessionsController.RecordAttendance` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب همان کلاس |
| قواعد شاخص | entries enrollmentهای مجاز؛ وضعیت Present/Absent/Late/Excused؛ پیش از شروع/لغوشده ممنوع. |
| ورودی | [RecordAttendanceCommand](#type-recordattendancecommand) |
| خروجی موفق | `200` [IReadOnlyCollection&lt;AttendanceEntryResult&gt;](#type-attendanceentryresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ATTENDANCE_ALREADY_RECORDED](#error-attendance-already-recorded) (409)، [ATTENDANCE_NOT_ALLOWED](#error-attendance-not-allowed) (403)، [SESSION_CANCELLED](#error-session-cancelled) (409) |
| Controller | [SessionsController.cs](../MakanApp.Api/Controllers/SessionsController.cs) |
| متدهای service | `sessionService.RecordAttendanceAsync` |
| Application | [AcademicSessionService.Attendance.cs](../MakanApp.Application/Academic/AcademicSessionService.Attendance.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `sessionId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PUT /api/v1/academic/sessions/c9669a44-0ec7-5647-9277-a60ffa1a10c4/attendance HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "entries": [
    {
      "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
      "status": "Present"
    }
  ]
}
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "attendanceId": "be2b6575-b733-5b4e-8eea-8fff6dd811e0",
    "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
    "learnerOrganizationPersonId": "ca792355-953b-56c9-8371-63d9a1122800",
    "status": "Present",
    "recordedAtUtc": "2026-11-01T09:00:00Z",
    "recordedByMembershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
    "rowVersion": "AAAAAAAAAAE="
  }
]
```

<a id="operation-033"></a>
### 033. اصلاح مستند یک حضور

```http
PATCH /api/v1/academic/sessions/{sessionId}/attendance/{attendanceId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Academic`؛ `SessionsController.CorrectAttendance` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب همان کلاس |
| قواعد شاخص | دلیل غیرخالی تا ۵۰۰، وضعیت قابل ثبت و expectedRowVersion؛ تاریخچه حفظ می‌شود. |
| ورودی | [CorrectAttendanceCommand](#type-correctattendancecommand) |
| خروجی موفق | `200` [AttendanceEntryResult](#type-attendanceentryresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ATTENDANCE_ALREADY_CHANGED](#error-attendance-already-changed) (409)، [ATTENDANCE_NOT_ALLOWED](#error-attendance-not-allowed) (403)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [SessionsController.cs](../MakanApp.Api/Controllers/SessionsController.cs) |
| متدهای service | `sessionService.CorrectAttendanceAsync` |
| Application | [AcademicSessionService.Attendance.cs](../MakanApp.Application/Academic/AcademicSessionService.Attendance.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `sessionId` | route | `UUID` | مطابق route و scope مجاز |
| `attendanceId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/academic/sessions/c9669a44-0ec7-5647-9277-a60ffa1a10c4/attendance/be2b6575-b733-5b4e-8eea-8fff6dd811e0 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "status": "Present",
  "correctionReason": "اصلاح پس از بررسی دوباره پاسخ",
  "expectedRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "attendanceId": "be2b6575-b733-5b4e-8eea-8fff6dd811e0",
  "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
  "learnerOrganizationPersonId": "ca792355-953b-56c9-8371-63d9a1122800",
  "status": "Present",
  "recordedAtUtc": "2026-11-01T09:00:00Z",
  "recordedByMembershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-034"></a>
### 034. ایجاد پیش‌نویس تکلیف

```http
POST /api/v1/academic/classes/{classId}/assignments
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `AssignmentsController.CreateDraft` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | title تا ۲۰۰ و description غیرخالی تا ۱۰۰۰۰؛ due UTC آینده؛ maxAttempts مثبت؛ maxScore مثبت دو اعشار. |
| ورودی | [CreateAssignmentDraftCommand](#type-createassignmentdraftcommand) |
| خروجی موفق | `201` [AssignmentResult](#type-assignmentresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ASSIGNMENT_NOT_ALLOWED](#error-assignment-not-allowed) (403)، [TEACHER_NOT_ASSIGNED](#error-teacher-not-assigned) (403)، [ASSIGNMENT_DESCRIPTION_INVALID](#error-assignment-description-invalid) (400)، [ASSIGNMENT_DUE_DATE_INVALID](#error-assignment-due-date-invalid) (400) |
| Controller | [AssignmentsController.cs](../MakanApp.Api/Controllers/AssignmentsController.cs) |
| متدهای service | `assignmentService.CreateDraftAsync` |
| Application | [AssignmentService.cs](../MakanApp.Application/Assessment/AssignmentService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAssignmentStore` | `EfAssignmentStore` | [EfAssignmentStore.cs](../MakanApp.Infrastructure/Assessment/EfAssignmentStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/assignments HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "title": "کلاس نمونه",
  "description": "توضیح نمونه برای تمرین آموزشی",
  "dueAtUtc": "2026-11-08T09:00:00Z",
  "allowLateSubmission": false,
  "maxAttempts": 2,
  "maxScore": 20
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "status": "Draft",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "publishedAtUtc": null,
  "versionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "versionNumber": 1,
  "title": "کلاس نمونه",
  "description": "توضیح نمونه برای تمرین آموزشی",
  "dueAtUtc": "2026-11-08T09:00:00Z",
  "allowLateSubmission": false,
  "maxAttempts": 2,
  "maxScore": 20,
  "assignmentRowVersion": "AAAAAAAAAAE=",
  "versionRowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-035"></a>
### 035. تکلیف‌های قابل مشاهده یک کلاس

```http
GET /api/v1/academic/classes/{classId}/assignments
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `AssignmentsController.GetForClass` |
| احراز هویت و context | Organization؛ طراحی Manager/Teacher منتسب؛ خواندن منتشرشده Student گیرنده/Parent مجاز |
| قواعد شاخص | فهرست متناسب role و رابطه کلاس؛ خواندن Student/Parent به انتشار و گیرنده معتبر محدود می‌شود. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;AssignmentResult&gt;](#type-assignmentresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ASSIGNMENT_NOT_FOUND](#error-assignment-not-found) (404)، [ASSIGNMENT_NOT_ALLOWED](#error-assignment-not-allowed) (403) |
| Controller | [AssignmentsController.cs](../MakanApp.Api/Controllers/AssignmentsController.cs) |
| متدهای service | `assignmentService.GetForClassAsync` |
| Application | [AssignmentService.Reads.cs](../MakanApp.Application/Assessment/AssignmentService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/assignments HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "id": "2c615bca-6e35-58e2-8082-551936ce7a78",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
    "classTitle": "کلاس نمونه",
    "status": "Published",
    "createdAtUtc": "2026-11-01T09:00:00Z",
    "updatedAtUtc": "2026-11-01T09:00:00Z",
    "publishedAtUtc": "2026-11-01T09:00:00Z",
    "versionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
    "versionNumber": 1,
    "title": "تکلیف نمونه",
    "description": "توضیح نمونه برای تمرین آموزشی",
    "dueAtUtc": "2026-11-08T09:00:00Z",
    "allowLateSubmission": false,
    "maxAttempts": 2,
    "maxScore": 20,
    "assignmentRowVersion": "AAAAAAAAAAE=",
    "versionRowVersion": "AAAAAAAAAAE="
  }
]
```

<a id="operation-036"></a>
### 036. دریافت تکلیف‌های کلاس انتخاب‌شده از query

```http
GET /api/v1/academic/assignments
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `AssignmentsController.GetAssignments` |
| احراز هویت و context | Organization؛ طراحی Manager/Teacher منتسب؛ خواندن منتشرشده Student گیرنده/Parent مجاز |
| قواعد شاخص | classId query الزامی و متعلق به کلاس قابل مشاهده؛ فهرست همه کلاس‌ها نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;AssignmentResult&gt;](#type-assignmentresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ASSIGNMENT_NOT_ALLOWED](#error-assignment-not-allowed) (403)، [CLASS_NOT_FOUND](#error-class-not-found) (404) |
| Controller | [AssignmentsController.cs](../MakanApp.Api/Controllers/AssignmentsController.cs) |
| متدهای service | `assignmentService.GetForClassAsync` |
| Application | [AssignmentService.Reads.cs](../MakanApp.Application/Assessment/AssignmentService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | query | `Guid` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/assignments?classId=c16b36b1-18ac-51ec-be7e-e518764ad372 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "id": "2c615bca-6e35-58e2-8082-551936ce7a78",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
    "classTitle": "کلاس نمونه",
    "status": "Published",
    "createdAtUtc": "2026-11-01T09:00:00Z",
    "updatedAtUtc": "2026-11-01T09:00:00Z",
    "publishedAtUtc": "2026-11-01T09:00:00Z",
    "versionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
    "versionNumber": 1,
    "title": "تکلیف نمونه",
    "description": "توضیح نمونه برای تمرین آموزشی",
    "dueAtUtc": "2026-11-08T09:00:00Z",
    "allowLateSubmission": false,
    "maxAttempts": 2,
    "maxScore": 20,
    "assignmentRowVersion": "AAAAAAAAAAE=",
    "versionRowVersion": "AAAAAAAAAAE="
  }
]
```

<a id="operation-037"></a>
### 037. جزئیات و نسخه قابل مشاهده تکلیف

```http
GET /api/v1/academic/assignments/{assignmentId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `AssignmentsController.Get` |
| احراز هویت و context | Organization؛ طراحی Manager/Teacher منتسب؛ خواندن منتشرشده Student گیرنده/Parent مجاز |
| قواعد شاخص | projection نقش‌محور؛ شناسه تکلیف به‌تنهایی اجازه خواندن نسخه و جزئیات محرمانه نمی‌دهد. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [AssignmentResult](#type-assignmentresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ASSIGNMENT_NOT_FOUND](#error-assignment-not-found) (404)، [ASSIGNMENT_NOT_ALLOWED](#error-assignment-not-allowed) (403) |
| Controller | [AssignmentsController.cs](../MakanApp.Api/Controllers/AssignmentsController.cs) |
| متدهای service | `assignmentService.GetAsync` |
| Application | [AssignmentService.Reads.cs](../MakanApp.Application/Assessment/AssignmentService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `assignmentId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/assignments/2c615bca-6e35-58e2-8082-551936ce7a78 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "status": "Published",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "publishedAtUtc": "2026-11-01T09:00:00Z",
  "versionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "versionNumber": 1,
  "title": "تکلیف نمونه",
  "description": "توضیح نمونه برای تمرین آموزشی",
  "dueAtUtc": "2026-11-08T09:00:00Z",
  "allowLateSubmission": false,
  "maxAttempts": 2,
  "maxScore": 20,
  "assignmentRowVersion": "AAAAAAAAAAE=",
  "versionRowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-038"></a>
### 038. ویرایش پیش‌نویس تکلیف

```http
PATCH /api/v1/academic/assignments/{assignmentId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `AssignmentsController.UpdateDraft` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | فقط Draft؛ همان قواعد create و دو نسخه assignment/version جاری. |
| ورودی | [UpdateAssignmentDraftCommand](#type-updateassignmentdraftcommand) |
| خروجی موفق | `200` [AssignmentResult](#type-assignmentresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ASSIGNMENT_NOT_DRAFT](#error-assignment-not-draft) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [AssignmentsController.cs](../MakanApp.Api/Controllers/AssignmentsController.cs) |
| متدهای service | `assignmentService.UpdateDraftAsync` |
| Application | [AssignmentService.cs](../MakanApp.Application/Assessment/AssignmentService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAssignmentStore` | `EfAssignmentStore` | [EfAssignmentStore.cs](../MakanApp.Infrastructure/Assessment/EfAssignmentStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `assignmentId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/academic/assignments/2c615bca-6e35-58e2-8082-551936ce7a78 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "title": "کلاس نمونه",
  "description": "توضیح نمونه برای تمرین آموزشی",
  "dueAtUtc": "2026-11-08T09:00:00Z",
  "allowLateSubmission": false,
  "maxAttempts": 2,
  "maxScore": 20,
  "expectedAssignmentRowVersion": "AAAAAAAAAAE=",
  "expectedVersionRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "status": "Draft",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "publishedAtUtc": null,
  "versionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "versionNumber": 1,
  "title": "کلاس نمونه",
  "description": "توضیح نمونه برای تمرین آموزشی",
  "dueAtUtc": "2026-11-08T09:00:00Z",
  "allowLateSubmission": false,
  "maxAttempts": 2,
  "maxScore": 20,
  "assignmentRowVersion": "AAAAAAAAAAE=",
  "versionRowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-039"></a>
### 039. انتشار نسخه و تثبیت گیرندگان

```http
POST /api/v1/academic/assignments/{assignmentId}/publish
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `AssignmentsController.Publish` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | Draft و deadline آینده؛ هر دو rowversion؛ snapshot enrollmentهای فعال. |
| ورودی | [PublishAssignmentCommand](#type-publishassignmentcommand) |
| خروجی موفق | `200` [PublishAssignmentResult](#type-publishassignmentresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ASSIGNMENT_ALREADY_PUBLISHED](#error-assignment-already-published) (409)، [ASSIGNMENT_NOT_DRAFT](#error-assignment-not-draft) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [AssignmentsController.cs](../MakanApp.Api/Controllers/AssignmentsController.cs) |
| متدهای service | `assignmentService.PublishAsync` |
| Application | [AssignmentService.cs](../MakanApp.Application/Assessment/AssignmentService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IAssignmentStore` | `EfAssignmentStore` | [EfAssignmentStore.cs](../MakanApp.Infrastructure/Assessment/EfAssignmentStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `assignmentId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/assignments/2c615bca-6e35-58e2-8082-551936ce7a78/publish HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "expectedAssignmentRowVersion": "AAAAAAAAAAE=",
  "expectedVersionRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "assignment": {
    "id": "2c615bca-6e35-58e2-8082-551936ce7a78",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
    "classTitle": "کلاس نمونه",
    "status": "Published",
    "createdAtUtc": "2026-11-01T09:00:00Z",
    "updatedAtUtc": "2026-11-01T09:00:00Z",
    "publishedAtUtc": "2026-11-01T09:00:00Z",
    "versionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
    "versionNumber": 1,
    "title": "تکلیف نمونه",
    "description": "توضیح نمونه برای تمرین آموزشی",
    "dueAtUtc": "2026-11-08T09:00:00Z",
    "allowLateSubmission": false,
    "maxAttempts": 2,
    "maxScore": 20,
    "assignmentRowVersion": "AAAAAAAAAAE=",
    "versionRowVersion": "AAAAAAAAAAE="
  },
  "recipientCount": 1
}
```

<a id="operation-040"></a>
### 040. ساخت یا بازیابی پیش‌نویس پاسخ

```http
POST /api/v1/academic/assignments/{assignmentId}/attempts
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `SubmissionsController.CreateOrResumeDraft` |
| احراز هویت و context | Organization / Student مالک و گیرنده تکلیف |
| قواعد شاخص | Student واجد recipient؛ draft موجود reuse؛ سقف تلاش و مهلت مطابق تکلیف. |
| ورودی | بدون بدنه |
| خروجی موفق | `201` [SubmissionAttemptResult](#type-submissionattemptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [ASSIGNMENT_RECIPIENT_NOT_FOUND](#error-assignment-recipient-not-found) (404)، [SUBMISSION_ATTEMPTS_EXHAUSTED](#error-submission-attempts-exhausted) (409)، [SUBMISSION_DEADLINE_PASSED](#error-submission-deadline-passed) (409) |
| Controller | [SubmissionsController.cs](../MakanApp.Api/Controllers/SubmissionsController.cs) |
| متدهای service | `submissionService.CreateOrResumeDraftAsync` |
| Application | [SubmissionService.cs](../MakanApp.Application/Assessment/SubmissionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `ISubmissionStore` | `EfSubmissionStore` | [EfSubmissionStore.cs](../MakanApp.Infrastructure/Assessment/EfSubmissionStore.cs) |
| `IFileAssetStore` | `EfFileAssetStore` | [EfFileAssetStore.cs](../MakanApp.Infrastructure/Storage/EfFileAssetStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `assignmentId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/assignments/2c615bca-6e35-58e2-8082-551936ce7a78/attempts HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "assignmentRecipientId": "fddad09e-e2f8-55fe-9cf0-978588cd85f6",
  "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
  "attemptNumber": 1,
  "status": "Draft",
  "contentVisible": true,
  "answerText": null,
  "attachments": [],
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "lastSavedAtUtc": null,
  "submittedAtUtc": null,
  "isLate": false,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-041"></a>
### 041. تلاش‌های خود برای تکلیف

```http
GET /api/v1/academic/assignments/{assignmentId}/attempts/me
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `SubmissionsController.GetMyAttempts` |
| احراز هویت و context | Organization / Student مالک و گیرنده تکلیف |
| قواعد شاخص | سوابق پاسخ‌های خود Student در scope و تکلیف مجاز؛ داده سایر دانش‌آموزان نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;SubmissionAttemptResult&gt;](#type-submissionattemptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SUBMISSION_NOT_FOUND](#error-submission-not-found) (404)، [SUBMISSION_NOT_ALLOWED](#error-submission-not-allowed) (403) |
| Controller | [SubmissionsController.cs](../MakanApp.Api/Controllers/SubmissionsController.cs) |
| متدهای service | `submissionService.GetMyAttemptsAsync` |
| Application | [SubmissionService.Reads.cs](../MakanApp.Application/Assessment/SubmissionService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `assignmentId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/assignments/2c615bca-6e35-58e2-8082-551936ce7a78/attempts/me HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "id": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
    "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
    "assignmentRecipientId": "fddad09e-e2f8-55fe-9cf0-978588cd85f6",
    "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
    "attemptNumber": 1,
    "status": "Draft",
    "contentVisible": true,
    "answerText": null,
    "attachments": [],
    "createdAtUtc": "2026-11-01T09:00:00Z",
    "lastSavedAtUtc": null,
    "submittedAtUtc": null,
    "isLate": false,
    "rowVersion": "AAAAAAAAAAE="
  }
]
```

<a id="operation-042"></a>
### 042. دریافت تلاش با سطح نمایش مجاز

```http
GET /api/v1/academic/submission-attempts/{attemptId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `SubmissionsController.GetAttempt` |
| احراز هویت و context | Organization؛ مالک یا خواننده مجاز؛ محتوای غیرنهایی طبق contentVisible |
| قواعد شاخص | مالکیت/رابطه و contentVisible براساس نقش و وضعیت تحویل؛ برای اصلاح draft، نسخه جاری response لازم است. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SUBMISSION_NOT_FOUND](#error-submission-not-found) (404)، [SUBMISSION_NOT_ALLOWED](#error-submission-not-allowed) (403) |
| Controller | [SubmissionsController.cs](../MakanApp.Api/Controllers/SubmissionsController.cs) |
| متدهای service | `submissionService.GetAttemptAsync` |
| Application | [SubmissionService.Reads.cs](../MakanApp.Application/Assessment/SubmissionService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "assignmentRecipientId": "fddad09e-e2f8-55fe-9cf0-978588cd85f6",
  "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
  "attemptNumber": 1,
  "status": "Draft",
  "contentVisible": true,
  "answerText": null,
  "attachments": [],
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "lastSavedAtUtc": null,
  "submittedAtUtc": null,
  "isLate": false,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-043"></a>
### 043. ذخیره متن پیش‌نویس پاسخ

```http
PATCH /api/v1/academic/submission-attempts/{attemptId}/draft
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `SubmissionsController.SaveDraft` |
| احراز هویت و context | Organization / Student مالک و گیرنده تکلیف |
| قواعد شاخص | فقط مالک Draft؛ answerText nullable تا ۵۰۰۰۰؛ expectedRowVersion. |
| ورودی | [SaveSubmissionDraftCommand](#type-savesubmissiondraftcommand) |
| خروجی موفق | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SUBMISSION_NOT_DRAFT](#error-submission-not-draft) (409)، [SUBMISSION_NOT_ALLOWED](#error-submission-not-allowed) (403)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [SubmissionsController.cs](../MakanApp.Api/Controllers/SubmissionsController.cs) |
| متدهای service | `submissionService.SaveDraftAsync` |
| Application | [SubmissionService.cs](../MakanApp.Application/Assessment/SubmissionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `ISubmissionStore` | `EfSubmissionStore` | [EfSubmissionStore.cs](../MakanApp.Infrastructure/Assessment/EfSubmissionStore.cs) |
| `IFileAssetStore` | `EfFileAssetStore` | [EfFileAssetStore.cs](../MakanApp.Infrastructure/Storage/EfFileAssetStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/draft HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "answerText": "پاسخ نمونه دانش‌آموز",
  "expectedRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "assignmentRecipientId": "fddad09e-e2f8-55fe-9cf0-978588cd85f6",
  "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
  "attemptNumber": 1,
  "status": "Draft",
  "contentVisible": true,
  "answerText": "پاسخ نمونه دانش‌آموز",
  "attachments": [],
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "lastSavedAtUtc": "2026-11-01T09:00:00Z",
  "submittedAtUtc": null,
  "isLate": false,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-044"></a>
### 044. اتصال فایل آماده به پاسخ

```http
POST /api/v1/academic/submission-attempts/{attemptId}/attachments
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `SubmissionsController.AttachFile` |
| احراز هویت و context | Organization / Student مالک و گیرنده تکلیف |
| قواعد شاخص | فایل Ready، مالک و scope مجاز؛ Draft و نسخه جاری. |
| ورودی | [AttachSubmissionFileCommand](#type-attachsubmissionfilecommand) |
| خروجی موفق | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SUBMISSION_FILE_NOT_READY](#error-submission-file-not-ready) (409)، [SUBMISSION_FILE_NOT_ALLOWED](#error-submission-file-not-allowed) (403)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [SubmissionsController.cs](../MakanApp.Api/Controllers/SubmissionsController.cs) |
| متدهای service | `submissionService.AttachFileAsync` |
| Application | [SubmissionService.cs](../MakanApp.Application/Assessment/SubmissionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `ISubmissionStore` | `EfSubmissionStore` | [EfSubmissionStore.cs](../MakanApp.Infrastructure/Assessment/EfSubmissionStore.cs) |
| `IFileAssetStore` | `EfFileAssetStore` | [EfFileAssetStore.cs](../MakanApp.Infrastructure/Storage/EfFileAssetStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/attachments HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "fileAssetId": "d3d55fe5-9f38-551b-9cbc-36d104ba44f8",
  "expectedRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "assignmentRecipientId": "fddad09e-e2f8-55fe-9cf0-978588cd85f6",
  "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
  "attemptNumber": 1,
  "status": "Draft",
  "contentVisible": true,
  "answerText": null,
  "attachments": [
    {
      "fileAssetId": "d3d55fe5-9f38-551b-9cbc-36d104ba44f8",
      "originalFileName": "sample.pdf",
      "contentType": "application/pdf",
      "sizeBytes": 1024,
      "fileStatus": "Ready",
      "attachedAtUtc": "2026-11-01T09:00:00Z"
    }
  ],
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "lastSavedAtUtc": null,
  "submittedAtUtc": null,
  "isLate": false,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-045"></a>
### 045. جداکردن فایل از پیش‌نویس

```http
DELETE /api/v1/academic/submission-attempts/{attemptId}/attachments/{fileAssetId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `SubmissionsController.RemoveFile` |
| احراز هویت و context | Organization / Student مالک و گیرنده تکلیف |
| قواعد شاخص | فقط Draft مالک؛ fileAssetId route؛ expectedRowVersion در query URL-encode شود. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SUBMISSION_NOT_DRAFT](#error-submission-not-draft) (409)، [SUBMISSION_FILE_NOT_ALLOWED](#error-submission-file-not-allowed) (403)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [SubmissionsController.cs](../MakanApp.Api/Controllers/SubmissionsController.cs) |
| متدهای service | `submissionService.RemoveFileAsync` |
| Application | [SubmissionService.cs](../MakanApp.Application/Assessment/SubmissionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `ISubmissionStore` | `EfSubmissionStore` | [EfSubmissionStore.cs](../MakanApp.Infrastructure/Assessment/EfSubmissionStore.cs) |
| `IFileAssetStore` | `EfFileAssetStore` | [EfFileAssetStore.cs](../MakanApp.Infrastructure/Storage/EfFileAssetStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |
| `fileAssetId` | route | `UUID` | مطابق route و scope مجاز |
| `expectedRowVersion` | query | `string` | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. |


نمونه درخواست مصنوعی:

```http
DELETE /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/attachments/d3d55fe5-9f38-551b-9cbc-36d104ba44f8?expectedRowVersion=AAAAAAAAAAE%3D HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "assignmentRecipientId": "fddad09e-e2f8-55fe-9cf0-978588cd85f6",
  "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
  "attemptNumber": 1,
  "status": "Draft",
  "contentVisible": true,
  "answerText": null,
  "attachments": [],
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "lastSavedAtUtc": null,
  "submittedAtUtc": null,
  "isLate": false,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-046"></a>
### 046. تحویل نهایی پاسخ و رسید

```http
POST /api/v1/academic/submission-attempts/{attemptId}/submit
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `SubmissionsController.FinalSubmit` |
| احراز هویت و context | Organization / Student مالک و گیرنده تکلیف |
| قواعد شاخص | حداقل متن/فایل؛ مهلت و late policy و نسخه؛ Submitted تکراری رسید موجود. |
| ورودی | [FinalSubmitAssignmentCommand](#type-finalsubmitassignmentcommand) |
| خروجی موفق | `200` [SubmissionReceipt](#type-submissionreceipt) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SUBMISSION_DEADLINE_PASSED](#error-submission-deadline-passed) (409)، [SUBMISSION_NOT_DRAFT](#error-submission-not-draft) (409)، [SUBMISSION_VERSION_MISMATCH](#error-submission-version-mismatch) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [SubmissionsController.cs](../MakanApp.Api/Controllers/SubmissionsController.cs) |
| متدهای service | `submissionService.FinalSubmitAsync` |
| Application | [SubmissionService.cs](../MakanApp.Application/Assessment/SubmissionService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `ISubmissionStore` | `EfSubmissionStore` | [EfSubmissionStore.cs](../MakanApp.Infrastructure/Assessment/EfSubmissionStore.cs) |
| `IFileAssetStore` | `EfFileAssetStore` | [EfFileAssetStore.cs](../MakanApp.Infrastructure/Storage/EfFileAssetStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/submit HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "expectedRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "submissionAttemptId": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "attemptNumber": 1,
  "submittedAtUtc": "2026-11-01T09:00:00Z",
  "isLate": false,
  "status": "Submitted",
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-047"></a>
### 047. تلاش‌های تحویل‌شده برای ارزیاب

```http
GET /api/v1/academic/assignments/{assignmentId}/submitted-attempts
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `SubmissionsController.GetSubmittedAttempts` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | فقط ارزیاب مجاز کلاس؛ پاسخ‌های Submitted. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;SubmissionAttemptResult&gt;](#type-submissionattemptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SUBMISSION_NOT_ALLOWED](#error-submission-not-allowed) (403)، [TEACHER_NOT_ASSIGNED](#error-teacher-not-assigned) (403) |
| Controller | [SubmissionsController.cs](../MakanApp.Api/Controllers/SubmissionsController.cs) |
| متدهای service | `submissionService.GetSubmittedAttemptsAsync` |
| Application | [SubmissionService.Reads.cs](../MakanApp.Application/Assessment/SubmissionService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `assignmentId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/assignments/2c615bca-6e35-58e2-8082-551936ce7a78/submitted-attempts HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "id": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
    "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
    "assignmentRecipientId": "fddad09e-e2f8-55fe-9cf0-978588cd85f6",
    "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
    "attemptNumber": 1,
    "status": "Submitted",
    "contentVisible": true,
    "answerText": "پاسخ نمونه دانش‌آموز",
    "attachments": [],
    "createdAtUtc": "2026-11-01T09:00:00Z",
    "lastSavedAtUtc": null,
    "submittedAtUtc": "2026-11-01T09:00:00Z",
    "isLate": false,
    "rowVersion": "AAAAAAAAAAE="
  }
]
```

<a id="operation-048"></a>
### 048. صف پاسخ‌های قابل ارزیابی

```http
GET /api/v1/academic/evaluations/queue
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `EvaluationsController.GetQueue` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | صف ارزیابی در scope Manager یا Teacher منتسب؛ داده در حال ارزیابی نتیجه عمومی Student نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;EvaluationQueueItemResult&gt;](#type-evaluationqueueitemresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EVALUATION_NOT_FOUND](#error-evaluation-not-found) (404)، [EVALUATION_NOT_ALLOWED](#error-evaluation-not-allowed) (403) |
| Controller | [EvaluationsController.cs](../MakanApp.Api/Controllers/EvaluationsController.cs) |
| متدهای service | `evaluationService.GetQueueAsync` |
| Application | [EvaluationService.Reads.cs](../MakanApp.Application/Assessment/EvaluationService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `assignmentId` | query | `Guid?` | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `reviewStatus` | query | `EvaluationReviewStatus?` | وضعیت صف ارزیابی تکلیف؛ enum اختصاصی EvaluationReviewStatus |
| `isLate` | query | `bool?` | وضعیت تأخیر تعیین‌شده توسط server acceptance time |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/evaluations/queue HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "submissionAttemptId": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
    "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
    "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
    "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
    "assignmentTitle": "تکلیف نمونه",
    "studentDisplayName": "دانش‌آموز نمونه",
    "attemptNumber": 1,
    "submittedAtUtc": "2026-11-01T09:00:00Z",
    "isLate": false,
    "maxScore": 20,
    "reviewStatus": "AwaitingEvaluation",
    "evaluationRevisionNumber": null,
    "score": null
  }
]
```

<a id="operation-049"></a>
### 049. متن و فایل‌های پاسخ برای ارزیاب

```http
GET /api/v1/academic/submission-attempts/{attemptId}/evaluation/submission
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `EvaluationsController.GetSubmission` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | خواندن پاسخ تحویل‌شده برای ارزیاب مجاز؛ خواندن پاسخ یک کلاس دیگر با role Teacher مجاز نمی‌شود. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [SubmissionForEvaluationResult](#type-submissionforevaluationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EVALUATION_NOT_FOUND](#error-evaluation-not-found) (404)، [EVALUATION_NOT_ALLOWED](#error-evaluation-not-allowed) (403) |
| Controller | [EvaluationsController.cs](../MakanApp.Api/Controllers/EvaluationsController.cs) |
| متدهای service | `evaluationService.GetSubmissionForEvaluationAsync` |
| Application | [EvaluationService.Reads.cs](../MakanApp.Application/Assessment/EvaluationService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/evaluation/submission HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "submissionAttemptId": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "assignmentTitle": "تکلیف نمونه",
  "studentDisplayName": "دانش‌آموز نمونه",
  "attemptNumber": 1,
  "submittedAtUtc": "2026-11-01T09:00:00Z",
  "isLate": false,
  "maxScore": 20,
  "answerText": "پاسخ نمونه دانش‌آموز",
  "attachments": [
    {
      "fileAssetId": "d3d55fe5-9f38-551b-9cbc-36d104ba44f8",
      "originalFileName": "sample.pdf",
      "contentType": "application/pdf",
      "sizeBytes": 1024,
      "fileStatus": "Ready",
      "attachedAtUtc": "2026-11-01T09:00:00Z"
    }
  ]
}
```

<a id="operation-050"></a>
### 050. آخرین نسخه ارزیابی خصوصی

```http
GET /api/v1/academic/submission-attempts/{attemptId}/evaluation
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `EvaluationsController.GetEvaluation` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | مدل ارزیاب مجاز می‌تواند draft و private note داشته باشد؛ برای Student/Parent از endpoint نتیجه استفاده کنید. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [EvaluatorEvaluationResult](#type-evaluatorevaluationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EVALUATION_NOT_FOUND](#error-evaluation-not-found) (404)، [EVALUATION_NOT_ALLOWED](#error-evaluation-not-allowed) (403) |
| Controller | [EvaluationsController.cs](../MakanApp.Api/Controllers/EvaluationsController.cs) |
| متدهای service | `evaluationService.GetEvaluationAsync` |
| Application | [EvaluationService.Reads.cs](../MakanApp.Application/Assessment/EvaluationService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/evaluation HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "3204d004-65bd-5284-8489-f4eac3f25bca",
  "submissionAttemptId": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "revisionNumber": 1,
  "score": 16,
  "maxScore": 20,
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "teacherPrivateNote": null,
  "status": "Draft",
  "createdByMembershipId": "888ea186-0fc3-575a-949b-122aa6ac97c9",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "supersedesEvaluationRevisionId": null,
  "correctionReason": null,
  "releasedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-051"></a>
### 051. ثبت پیش‌نویس نمره و بازخورد

```http
PUT /api/v1/academic/submission-attempts/{attemptId}/evaluation
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `EvaluationsController.SaveDraft` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | Submitted، ارزیاب مجاز؛ score از صفر تا maxScore دو اعشار؛ expectedRowVersion=null برای ایجاد اولیه. |
| ورودی | [SaveEvaluationDraftCommand](#type-saveevaluationdraftcommand) |
| خروجی موفق | `200` [EvaluatorEvaluationResult](#type-evaluatorevaluationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EVALUATION_SUBMISSION_NOT_FINAL](#error-evaluation-submission-not-final) (409)، [EVALUATION_ALREADY_RELEASED](#error-evaluation-already-released) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [EvaluationsController.cs](../MakanApp.Api/Controllers/EvaluationsController.cs) |
| متدهای service | `evaluationService.CreateOrUpdateDraftAsync` |
| Application | [EvaluationService.cs](../MakanApp.Application/Assessment/EvaluationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IEvaluationStore` | `EfEvaluationStore` | [EfEvaluationStore.cs](../MakanApp.Infrastructure/Assessment/EfEvaluationStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PUT /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/evaluation HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "score": 16,
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "teacherPrivateNote": null,
  "expectedRowVersion": null
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "3204d004-65bd-5284-8489-f4eac3f25bca",
  "submissionAttemptId": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "revisionNumber": 1,
  "score": 16,
  "maxScore": 20,
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "teacherPrivateNote": null,
  "status": "Draft",
  "createdByMembershipId": "888ea186-0fc3-575a-949b-122aa6ac97c9",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "supersedesEvaluationRevisionId": null,
  "correctionReason": null,
  "releasedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-052"></a>
### 052. انتشار نمره تکلیف

```http
POST /api/v1/academic/submission-attempts/{attemptId}/evaluation/release
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `EvaluationsController.Release` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | revision قابل انتشار و نسخه جاری؛ انتشار از ذخیره Draft مستقل است. |
| ورودی | [ReleaseEvaluationCommand](#type-releaseevaluationcommand) |
| خروجی موفق | `200` [GradeReleaseResult](#type-gradereleaseresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EVALUATION_NOT_READY_FOR_RELEASE](#error-evaluation-not-ready-for-release) (409)، [GRADE_RELEASE_CONFLICT](#error-grade-release-conflict) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [EvaluationsController.cs](../MakanApp.Api/Controllers/EvaluationsController.cs) |
| متدهای service | `evaluationService.ReleaseAsync` |
| Application | [EvaluationService.cs](../MakanApp.Application/Assessment/EvaluationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IEvaluationStore` | `EfEvaluationStore` | [EfEvaluationStore.cs](../MakanApp.Infrastructure/Assessment/EfEvaluationStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/evaluation/release HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "expectedRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "gradeReleaseId": "56d3204f-0e64-5da5-93fb-d1547456575a",
  "submissionAttemptId": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "evaluationRevisionId": "72226e51-a5bc-57ed-9146-65db40472f75",
  "revisionNumber": 1,
  "score": 16,
  "releasedAtUtc": "2026-11-02T09:00:00Z",
  "releasedByMembershipId": "a80c5e5a-7129-57e2-bd61-71b8bcca65ca",
  "evaluationRowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-053"></a>
### 053. ایجاد نسخه اصلاحی نمره منتشرشده

```http
POST /api/v1/academic/submission-attempts/{attemptId}/evaluation/corrections
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `EvaluationsController.Correct` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | reason غیرخالی تا ۱۰۰۰؛ نسخه Released جاری؛ revision جدید و حفظ نتیجه قبلی. |
| ورودی | [CorrectReleasedEvaluationCommand](#type-correctreleasedevaluationcommand) |
| خروجی موفق | `201` [EvaluatorEvaluationResult](#type-evaluatorevaluationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [GRADE_RELEASE_CONFLICT](#error-grade-release-conflict) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [EvaluationsController.cs](../MakanApp.Api/Controllers/EvaluationsController.cs) |
| متدهای service | `evaluationService.CorrectReleasedEvaluationAsync` |
| Application | [EvaluationService.cs](../MakanApp.Application/Assessment/EvaluationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IEvaluationStore` | `EfEvaluationStore` | [EfEvaluationStore.cs](../MakanApp.Infrastructure/Assessment/EfEvaluationStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/evaluation/corrections HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "score": 16,
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "teacherPrivateNote": null,
  "correctionReason": "اصلاح پس از بررسی دوباره پاسخ",
  "expectedReleasedEvaluationRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "3204d004-65bd-5284-8489-f4eac3f25bca",
  "submissionAttemptId": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "revisionNumber": 2,
  "score": 16,
  "maxScore": 20,
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "teacherPrivateNote": null,
  "status": "Draft",
  "createdByMembershipId": "888ea186-0fc3-575a-949b-122aa6ac97c9",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "supersedesEvaluationRevisionId": "e9ca01e3-9461-553e-8975-dfbf881279d5",
  "correctionReason": "اصلاح پس از بررسی دوباره پاسخ",
  "releasedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-054"></a>
### 054. نتیجه منتشرشده دانش‌آموز

```http
GET /api/v1/academic/submission-attempts/{attemptId}/result
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `EvaluationsController.GetStudentResult` |
| احراز هویت و context | Organization / Student مالک |
| قواعد شاخص | فقط Student مالک؛ قبل release خطای 404 GRADE_NOT_RELEASED. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [StudentReleasedResult](#type-studentreleasedresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [GRADE_NOT_RELEASED](#error-grade-not-released) (404)، [EVALUATION_NOT_ALLOWED](#error-evaluation-not-allowed) (403) |
| Controller | [EvaluationsController.cs](../MakanApp.Api/Controllers/EvaluationsController.cs) |
| متدهای service | `evaluationService.GetReleasedAssignmentResultAsync` |
| Application | [EvaluationService.Reads.cs](../MakanApp.Application/Assessment/EvaluationService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/result HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "submissionAttemptId": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "evaluationRevisionNumber": 1,
  "score": 16,
  "maxScore": 20,
  "learnerFeedback": "بازخورد منتشرشده نمونه",
  "releasedAtUtc": "2026-11-02T09:00:00Z"
}
```

<a id="operation-055"></a>
### 055. نتیجه قابل مشاهده والد

```http
GET /api/v1/academic/submission-attempts/{attemptId}/guardian-result
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `EvaluationsController.GetParentResult` |
| احراز هویت و context | Organization / Parent با child و relation معتبر |
| قواعد شاخص | فقط Parent و child معتبر؛ فقط guardianVisibleFeedback و نتیجه منتشرشده. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ParentReleasedResult](#type-parentreleasedresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [GRADE_NOT_RELEASED](#error-grade-not-released) (404)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403) |
| Controller | [EvaluationsController.cs](../MakanApp.Api/Controllers/EvaluationsController.cs) |
| متدهای service | `evaluationService.GetGuardianReleasedAssignmentResultAsync` |
| Application | [EvaluationService.Reads.cs](../MakanApp.Application/Assessment/EvaluationService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/submission-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/guardian-result HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "submissionAttemptId": "a639427c-0b29-5616-b6f9-46ed2a1b44d6",
  "assignmentId": "2c615bca-6e35-58e2-8082-551936ce7a78",
  "assignmentVersionId": "33e0cb5c-48df-5c1b-9b25-c2a7704b7ee9",
  "evaluationRevisionNumber": 1,
  "score": 16,
  "maxScore": 20,
  "guardianVisibleFeedback": "بازخورد منتشرشده نمونه",
  "releasedAtUtc": "2026-11-02T09:00:00Z"
}
```

<a id="operation-056"></a>
### 056. ساخت پیش‌نویس آزمون

```http
POST /api/v1/academic/classes/{classId}/exams
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.CreateDraft` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | بازه UTC مرتب؛ مدت/تلاش/maxScore مثبت؛ title تا ۲۰۰؛ description تا ۱۰۰۰۰. |
| ورودی | [CreateExamDraftCommand](#type-createexamdraftcommand) |
| خروجی موفق | `201` [ExamEditorDto](#type-exameditordto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_NOT_ALLOWED](#error-exam-not-allowed) (403)، [TEACHER_NOT_ASSIGNED](#error-teacher-not-assigned) (403) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.CreateDraftAsync` |
| Application | [ExamService.cs](../MakanApp.Application/Assessment/ExamService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamStore` | `EfExamStore` | [EfExamStore.cs](../MakanApp.Infrastructure/Assessment/EfExamStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/exams HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "title": "کلاس نمونه",
  "description": null,
  "availableFromUtc": "2026-11-01T09:00:00Z",
  "availableUntilUtc": "2026-11-01T10:00:00Z",
  "durationMinutes": 30,
  "maxAttempts": 2,
  "maxScore": 20,
  "randomizationPolicy": "None"
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "29d391de-01ad-5b49-8986-177d7566800c",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "status": "Draft",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "examRowVersion": "AAAAAAAAAAE=",
  "versionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "versionNumber": 1,
  "versionStatus": "Draft",
  "title": "کلاس نمونه",
  "description": null,
  "availableFromUtc": "2026-11-01T09:00:00Z",
  "availableUntilUtc": "2026-11-01T10:00:00Z",
  "durationMinutes": 30,
  "maxAttempts": 2,
  "maxScore": 20,
  "randomizationPolicy": "None",
  "publishedAtUtc": null,
  "versionRowVersion": "AAAAAAAAAAE=",
  "questions": []
}
```

<a id="operation-057"></a>
### 057. خلاصه آزمون‌های مجاز

```http
GET /api/v1/academic/exams
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.GetMyExams` |
| احراز هویت و context | Organization / Manager، Teacher منتسب، Student enrolled، Parent با child معتبر |
| قواعد شاخص | خلاصه‌های مجاز براساس role و enrollment/teacher assignment/child؛ DTO عمومی بدون key. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;StudentExamSummary&gt;](#type-studentexamsummary) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_NOT_ALLOWED](#error-exam-not-allowed) (403) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.GetMyExamsAsync` |
| Application | [ExamService.Reads.cs](../MakanApp.Application/Assessment/ExamService.Reads.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exams HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "id": "29d391de-01ad-5b49-8986-177d7566800c",
    "versionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
    "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
    "classTitle": "کلاس نمونه",
    "versionNumber": 1,
    "title": "آزمون نمونه",
    "description": null,
    "publicationState": "Published",
    "rules": {
      "availableFromUtc": "2026-11-01T09:00:00Z",
      "availableUntilUtc": "2026-11-01T10:00:00Z",
      "durationMinutes": 30,
      "maxAttempts": 2,
      "maxScore": 20,
      "randomizationPolicy": "None"
    }
  }
]
```

<a id="operation-058"></a>
### 058. دریافت مدل ویرایشگر با کلید پاسخ

```http
GET /api/v1/academic/exams/{examId}/editor
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.GetEditor` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | فقط Manager/Teacher منتسب؛ answer key خصوصی در DTO editor. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ExamEditorDto](#type-exameditordto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_NOT_FOUND](#error-exam-not-found) (404)، [EXAM_NOT_ALLOWED](#error-exam-not-allowed) (403)، [TEACHER_NOT_ASSIGNED](#error-teacher-not-assigned) (403) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.GetEditorAsync` |
| Application | [ExamService.Reads.cs](../MakanApp.Application/Assessment/ExamService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/editor HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "29d391de-01ad-5b49-8986-177d7566800c",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "status": "Draft",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "examRowVersion": "AAAAAAAAAAE=",
  "versionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "versionNumber": 1,
  "versionStatus": "Draft",
  "title": "آزمون نمونه",
  "description": null,
  "availableFromUtc": "2026-11-01T09:00:00Z",
  "availableUntilUtc": "2026-11-01T10:00:00Z",
  "durationMinutes": 30,
  "maxAttempts": 2,
  "maxScore": 20,
  "randomizationPolicy": "None",
  "publishedAtUtc": null,
  "versionRowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "id": "04636a02-95a9-52ea-a675-57c838a32619",
      "order": 1,
      "type": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴",
          "isCorrect": true
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵",
          "isCorrect": false
        }
      ],
      "rowVersion": "AAAAAAAAAAE="
    }
  ]
}
```

<a id="operation-059"></a>
### 059. پیش‌نمایش مجاز معلم

```http
GET /api/v1/academic/exams/{examId}/teacher-preview
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.GetTeacherPreview` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | فقط کارکنان مجاز کلاس؛ کلید پاسخ در مدل preview کارکنان قابل وجود است. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ExamTeacherPreview](#type-examteacherpreview) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_NOT_ALLOWED](#error-exam-not-allowed) (403)، [TEACHER_NOT_ASSIGNED](#error-teacher-not-assigned) (403) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.GetTeacherPreviewAsync` |
| Application | [ExamService.Reads.cs](../MakanApp.Application/Assessment/ExamService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/teacher-preview HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "exam": {
    "id": "29d391de-01ad-5b49-8986-177d7566800c",
    "versionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
    "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
    "classTitle": "کلاس نمونه",
    "versionNumber": 1,
    "title": "آزمون نمونه",
    "description": null,
    "publicationState": "Published",
    "rules": {
      "availableFromUtc": "2026-11-01T09:00:00Z",
      "availableUntilUtc": "2026-11-01T10:00:00Z",
      "durationMinutes": 30,
      "maxAttempts": 2,
      "maxScore": 20,
      "randomizationPolicy": "None"
    }
  },
  "questions": [
    {
      "id": "04636a02-95a9-52ea-a675-57c838a32619",
      "order": 1,
      "type": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴",
          "isCorrect": true
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵",
          "isCorrect": false
        }
      ],
      "rowVersion": "AAAAAAAAAAE="
    }
  ]
}
```

<a id="operation-060"></a>
### 060. ویرایش قواعد پیش‌نویس آزمون

```http
PATCH /api/v1/academic/exams/{examId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.UpdateDraft` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | فقط نسخه Draft، قواعد آزمون و expectedVersionRowVersion. |
| ورودی | [UpdateExamDraftCommand](#type-updateexamdraftcommand) |
| خروجی موفق | `200` [ExamEditorDto](#type-exameditordto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_NOT_DRAFT](#error-exam-not-draft) (409)، [EXAM_VERSION_LOCKED](#error-exam-version-locked) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.UpdateDraftAsync` |
| Application | [ExamService.cs](../MakanApp.Application/Assessment/ExamService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamStore` | `EfExamStore` | [EfExamStore.cs](../MakanApp.Infrastructure/Assessment/EfExamStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "title": "کلاس نمونه",
  "description": null,
  "availableFromUtc": "2026-11-01T09:00:00Z",
  "availableUntilUtc": "2026-11-01T10:00:00Z",
  "durationMinutes": 30,
  "maxAttempts": 2,
  "maxScore": 20,
  "randomizationPolicy": "None",
  "expectedVersionRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "29d391de-01ad-5b49-8986-177d7566800c",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "status": "Draft",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "examRowVersion": "AAAAAAAAAAE=",
  "versionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "versionNumber": 1,
  "versionStatus": "Draft",
  "title": "کلاس نمونه",
  "description": null,
  "availableFromUtc": "2026-11-01T09:00:00Z",
  "availableUntilUtc": "2026-11-01T10:00:00Z",
  "durationMinutes": 30,
  "maxAttempts": 2,
  "maxScore": 20,
  "randomizationPolicy": "None",
  "publishedAtUtc": null,
  "versionRowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "id": "04636a02-95a9-52ea-a675-57c838a32619",
      "order": 1,
      "type": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴",
          "isCorrect": true
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵",
          "isCorrect": false
        }
      ],
      "rowVersion": "AAAAAAAAAAE="
    }
  ]
}
```

<a id="operation-061"></a>
### 061. افزودن سؤال به نسخه پیش‌نویس

```http
POST /api/v1/academic/exams/{examId}/questions
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.AddQuestion` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | order مثبت یکتا؛ prompt تا ۲۰۰۰۰؛ score مثبت؛ objective دو گزینه یا بیشتر و یک صحیح؛ نسخه جاری. |
| ورودی | [AddExamQuestionCommand](#type-addexamquestioncommand) |
| خروجی موفق | `201` [ExamEditorDto](#type-exameditordto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_QUESTION_ORDER_INVALID](#error-exam-question-order-invalid) (409)، [EXAM_VERSION_LOCKED](#error-exam-version-locked) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.AddQuestionAsync` |
| Application | [ExamService.cs](../MakanApp.Application/Assessment/ExamService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamStore` | `EfExamStore` | [EfExamStore.cs](../MakanApp.Infrastructure/Assessment/EfExamStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/questions HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "order": 1,
  "type": "ObjectiveSingleChoice",
  "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
  "score": 20,
  "options": [
    {
      "order": 1,
      "text": "۴",
      "isCorrect": true
    },
    {
      "order": 2,
      "text": "۵",
      "isCorrect": false
    }
  ],
  "expectedVersionRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "29d391de-01ad-5b49-8986-177d7566800c",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "status": "Draft",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "examRowVersion": "AAAAAAAAAAE=",
  "versionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "versionNumber": 1,
  "versionStatus": "Draft",
  "title": "آزمون نمونه",
  "description": null,
  "availableFromUtc": "2026-11-01T09:00:00Z",
  "availableUntilUtc": "2026-11-01T10:00:00Z",
  "durationMinutes": 30,
  "maxAttempts": 2,
  "maxScore": 20,
  "randomizationPolicy": "None",
  "publishedAtUtc": null,
  "versionRowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "id": "04636a02-95a9-52ea-a675-57c838a32619",
      "order": 1,
      "type": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴",
          "isCorrect": true
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵",
          "isCorrect": false
        }
      ],
      "rowVersion": "AAAAAAAAAAE="
    }
  ]
}
```

<a id="operation-062"></a>
### 062. ویرایش سؤال و گزینه‌ها

```http
PATCH /api/v1/academic/exams/{examId}/questions/{questionId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.UpdateQuestion` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | نسخه سؤال و آزمون جاری؛ تغییر فقط Draft؛ گزینه‌ها متناسب نوع سؤال. |
| ورودی | [UpdateExamQuestionCommand](#type-updateexamquestioncommand) |
| خروجی موفق | `200` [ExamEditorDto](#type-exameditordto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_VERSION_LOCKED](#error-exam-version-locked) (409)، [EXAM_QUESTION_ORDER_INVALID](#error-exam-question-order-invalid) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.UpdateQuestionAsync` |
| Application | [ExamService.cs](../MakanApp.Application/Assessment/ExamService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamStore` | `EfExamStore` | [EfExamStore.cs](../MakanApp.Infrastructure/Assessment/EfExamStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |
| `questionId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/questions/04636a02-95a9-52ea-a675-57c838a32619 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "order": 1,
  "type": "ObjectiveSingleChoice",
  "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
  "score": 20,
  "options": [
    {
      "order": 1,
      "text": "۴",
      "isCorrect": true
    },
    {
      "order": 2,
      "text": "۵",
      "isCorrect": false
    }
  ],
  "expectedVersionRowVersion": "AAAAAAAAAAE=",
  "expectedQuestionRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "29d391de-01ad-5b49-8986-177d7566800c",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "status": "Draft",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "examRowVersion": "AAAAAAAAAAE=",
  "versionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "versionNumber": 1,
  "versionStatus": "Draft",
  "title": "آزمون نمونه",
  "description": null,
  "availableFromUtc": "2026-11-01T09:00:00Z",
  "availableUntilUtc": "2026-11-01T10:00:00Z",
  "durationMinutes": 30,
  "maxAttempts": 2,
  "maxScore": 20,
  "randomizationPolicy": "None",
  "publishedAtUtc": null,
  "versionRowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "id": "04636a02-95a9-52ea-a675-57c838a32619",
      "order": 1,
      "type": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴",
          "isCorrect": true
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵",
          "isCorrect": false
        }
      ],
      "rowVersion": "AAAAAAAAAAE="
    }
  ]
}
```

<a id="operation-063"></a>
### 063. حذف سؤال پیش‌نویس

```http
DELETE /api/v1/academic/exams/{examId}/questions/{questionId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.DeleteQuestion` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | DELETE دارای body JSON و دو expected rowversion؛ پاسخ 204. |
| ورودی | [DeleteExamQuestionCommand](#type-deleteexamquestioncommand) |
| خروجی موفق | `204` بدون بدنه |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_VERSION_LOCKED](#error-exam-version-locked) (409)، [EXAM_NOT_FOUND](#error-exam-not-found) (404)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.DeleteQuestionAsync` |
| Application | [ExamService.cs](../MakanApp.Application/Assessment/ExamService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamStore` | `EfExamStore` | [EfExamStore.cs](../MakanApp.Infrastructure/Assessment/EfExamStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |
| `questionId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
DELETE /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/questions/04636a02-95a9-52ea-a675-57c838a32619 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "expectedVersionRowVersion": "AAAAAAAAAAE=",
  "expectedQuestionRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `204`:

```http
HTTP/1.1 204 No Content
```

<a id="operation-064"></a>
### 064. پیش‌نمایش امن بدون answer key

```http
GET /api/v1/academic/exams/{examId}/student-preview
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.GetStudentPreview` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب؛ Student enrolled در window؛ Parent ممنوع |
| قواعد شاخص | staff مجاز یا Student با نسخه Published، window و enrollment؛ Parent مجاز نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [StudentSafeExamPreview](#type-studentsafeexampreview) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_STUDENT_PREVIEW_NOT_ALLOWED](#error-exam-student-preview-not-allowed) (403)، [EXAM_NOT_FOUND](#error-exam-not-found) (404) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.GetStudentPreviewAsync` |
| Application | [ExamService.Reads.cs](../MakanApp.Application/Assessment/ExamService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/student-preview HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "exam": {
    "id": "29d391de-01ad-5b49-8986-177d7566800c",
    "versionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
    "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
    "classTitle": "کلاس نمونه",
    "versionNumber": 1,
    "title": "آزمون نمونه",
    "description": null,
    "publicationState": "Published",
    "rules": {
      "availableFromUtc": "2026-11-01T09:00:00Z",
      "availableUntilUtc": "2026-11-01T10:00:00Z",
      "durationMinutes": 30,
      "maxAttempts": 2,
      "maxScore": 20,
      "randomizationPolicy": "None"
    }
  },
  "questions": [
    {
      "id": "04636a02-95a9-52ea-a675-57c838a32619",
      "order": 1,
      "type": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴"
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵"
        }
      ]
    }
  ]
}
```

<a id="operation-065"></a>
### 065. انتشار نسخه معتبر آزمون

```http
POST /api/v1/academic/exams/{examId}/publish
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.Publish` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | حداقل سؤال معتبر، جمع بارم برابر maxScore، نسخه‌های جاری، فقط Draft. |
| ورودی | [PublishExamCommand](#type-publishexamcommand) |
| خروجی موفق | `200` [ExamEditorDto](#type-exameditordto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_ALREADY_PUBLISHED](#error-exam-already-published) (409)، [EXAM_QUESTION_SET_INVALID](#error-exam-question-set-invalid) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.PublishAsync` |
| Application | [ExamService.cs](../MakanApp.Application/Assessment/ExamService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamStore` | `EfExamStore` | [EfExamStore.cs](../MakanApp.Infrastructure/Assessment/EfExamStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/publish HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "expectedExamRowVersion": "AAAAAAAAAAE=",
  "expectedVersionRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "29d391de-01ad-5b49-8986-177d7566800c",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "status": "Published",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "examRowVersion": "AAAAAAAAAAE=",
  "versionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "versionNumber": 1,
  "versionStatus": "Published",
  "title": "آزمون نمونه",
  "description": null,
  "availableFromUtc": "2026-11-01T09:00:00Z",
  "availableUntilUtc": "2026-11-01T10:00:00Z",
  "durationMinutes": 30,
  "maxAttempts": 2,
  "maxScore": 20,
  "randomizationPolicy": "None",
  "publishedAtUtc": "2026-11-01T09:00:00Z",
  "versionRowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "id": "04636a02-95a9-52ea-a675-57c838a32619",
      "order": 1,
      "type": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴",
          "isCorrect": true
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵",
          "isCorrect": false
        }
      ],
      "rowVersion": "AAAAAAAAAAE="
    }
  ]
}
```

<a id="operation-066"></a>
### 066. ساخت پیش‌نویس نسخه بعدی

```http
POST /api/v1/academic/exams/{examId}/versions
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.CreateNextVersion` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | نسخه Published مبنا و expectedExam/PublishedVersionRowVersion؛ نسخه جدید مستقل. |
| ورودی | [CreateNextExamVersionCommand](#type-createnextexamversioncommand) |
| خروجی موفق | `201` [ExamEditorDto](#type-exameditordto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_NOT_PUBLISHED](#error-exam-not-published) (409)، [EXAM_VERSION_LOCKED](#error-exam-version-locked) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examService.CreateNextVersionAsync` |
| Application | [ExamService.cs](../MakanApp.Application/Assessment/ExamService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamStore` | `EfExamStore` | [EfExamStore.cs](../MakanApp.Infrastructure/Assessment/EfExamStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/versions HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "expectedExamRowVersion": "AAAAAAAAAAE=",
  "expectedPublishedVersionRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "29d391de-01ad-5b49-8986-177d7566800c",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "status": "Published",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "examRowVersion": "AAAAAAAAAAE=",
  "versionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "versionNumber": 2,
  "versionStatus": "Draft",
  "title": "آزمون نمونه",
  "description": null,
  "availableFromUtc": "2026-11-01T09:00:00Z",
  "availableUntilUtc": "2026-11-01T10:00:00Z",
  "durationMinutes": 30,
  "maxAttempts": 2,
  "maxScore": 20,
  "randomizationPolicy": "None",
  "publishedAtUtc": null,
  "versionRowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "id": "04636a02-95a9-52ea-a675-57c838a32619",
      "order": 1,
      "type": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴",
          "isCorrect": true
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵",
          "isCorrect": false
        }
      ],
      "rowVersion": "AAAAAAAAAAE="
    }
  ]
}
```

<a id="operation-067"></a>
### 067. شروع یا بازیابی تلاش با operationId

```http
POST /api/v1/academic/exams/{examId}/attempts/start
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.StartAttempt` |
| احراز هویت و context | Organization / Student مالک attempt |
| قواعد شاخص | Student enrolled، Published، window مجاز، سقف تلاش؛ clientOperationId پایدار؛ snapshot نسخه. |
| ورودی | [StartExamCommand](#type-startexamcommand) |
| خروجی موفق | `201` [StudentExamAttemptDto](#type-studentexamattemptdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_NOT_AVAILABLE_YET](#error-exam-not-available-yet) (409)، [EXAM_WINDOW_CLOSED](#error-exam-window-closed) (409)، [EXAM_ATTEMPTS_EXHAUSTED](#error-exam-attempts-exhausted) (409)، [EXAM_START_IDEMPOTENCY_CONFLICT](#error-exam-start-idempotency-conflict) (409) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examAttemptService.StartAsync` |
| Application | [ExamAttemptService.cs](../MakanApp.Application/Assessment/ExamAttemptService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamAttemptStore` | `EfExamAttemptStore` | [EfExamAttemptStore.cs](../MakanApp.Infrastructure/Assessment/EfExamAttemptStore.cs) |
| `IExamQuestionOrderRandomizer` | `SecureExamQuestionOrderRandomizer` | [SecureExamQuestionOrderRandomizer.cs](../MakanApp.Infrastructure/Assessment/SecureExamQuestionOrderRandomizer.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/attempts/start HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "clientOperationId": "31cfe88a-21bf-51ee-b46e-5e75b259b798"
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "attemptNumber": 1,
  "startedAtUtc": "2026-11-01T09:00:00Z",
  "effectiveDeadlineUtc": "2026-11-01T09:30:00Z",
  "serverNowUtc": "2026-11-01T09:00:00Z",
  "status": "InProgress",
  "questions": [
    {
      "attemptQuestionId": "95d0fcd9-419d-5d7c-b860-11d0ea3e014c",
      "questionVersionId": "04636a02-95a9-52ea-a675-57c838a32619",
      "displayOrder": 1,
      "questionType": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴"
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵"
        }
      ]
    }
  ]
}
```

<a id="operation-068"></a>
### 068. تلاش فعال خود برای آزمون

```http
GET /api/v1/academic/exams/{examId}/attempts/me/active
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.GetMyActiveAttempt` |
| احراز هویت و context | Organization / Student مالک attempt |
| قواعد شاخص | تلاش فعال متعلق به Student جاری؛ شروع تلاش endpoint جدا با clientOperationId است. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [StudentExamAttemptDto](#type-studentexamattemptdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_ATTEMPT_NOT_FOUND](#error-exam-attempt-not-found) (404)، [EXAM_ATTEMPT_NOT_ALLOWED](#error-exam-attempt-not-allowed) (403) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examAttemptService.GetMyActiveAsync` |
| Application | [ExamAttemptService.cs](../MakanApp.Application/Assessment/ExamAttemptService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamAttemptStore` | `EfExamAttemptStore` | [EfExamAttemptStore.cs](../MakanApp.Infrastructure/Assessment/EfExamAttemptStore.cs) |
| `IExamQuestionOrderRandomizer` | `SecureExamQuestionOrderRandomizer` | [SecureExamQuestionOrderRandomizer.cs](../MakanApp.Infrastructure/Assessment/SecureExamQuestionOrderRandomizer.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/attempts/me/active HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "attemptNumber": 1,
  "startedAtUtc": "2026-11-01T09:00:00Z",
  "effectiveDeadlineUtc": "2026-11-01T09:30:00Z",
  "serverNowUtc": "2026-11-01T09:00:00Z",
  "status": "InProgress",
  "questions": [
    {
      "attemptQuestionId": "95d0fcd9-419d-5d7c-b860-11d0ea3e014c",
      "questionVersionId": "04636a02-95a9-52ea-a675-57c838a32619",
      "displayOrder": 1,
      "questionType": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴"
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵"
        }
      ]
    }
  ]
}
```

<a id="operation-069"></a>
### 069. تلاش و سؤال‌های تثبیت‌شده خود

```http
GET /api/v1/academic/exam-attempts/{attemptId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.GetAttempt` |
| احراز هویت و context | Organization / Student مالک attempt |
| قواعد شاخص | تلاش متعلق به Student و مجموعه سؤال تثبیت‌شده؛ پاسخ‌ها و answer key قراردادهای جدا دارند. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [StudentExamAttemptDto](#type-studentexamattemptdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_ATTEMPT_NOT_FOUND](#error-exam-attempt-not-found) (404)، [EXAM_ATTEMPT_NOT_ALLOWED](#error-exam-attempt-not-allowed) (403) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examAttemptService.GetAsync` |
| Application | [ExamAttemptService.cs](../MakanApp.Application/Assessment/ExamAttemptService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamAttemptStore` | `EfExamAttemptStore` | [EfExamAttemptStore.cs](../MakanApp.Infrastructure/Assessment/EfExamAttemptStore.cs) |
| `IExamQuestionOrderRandomizer` | `SecureExamQuestionOrderRandomizer` | [SecureExamQuestionOrderRandomizer.cs](../MakanApp.Infrastructure/Assessment/SecureExamQuestionOrderRandomizer.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "attemptNumber": 1,
  "startedAtUtc": "2026-11-01T09:00:00Z",
  "effectiveDeadlineUtc": "2026-11-01T09:30:00Z",
  "serverNowUtc": "2026-11-01T09:00:00Z",
  "status": "InProgress",
  "questions": [
    {
      "attemptQuestionId": "95d0fcd9-419d-5d7c-b860-11d0ea3e014c",
      "questionVersionId": "04636a02-95a9-52ea-a675-57c838a32619",
      "displayOrder": 1,
      "questionType": "ObjectiveSingleChoice",
      "prompt": "حاصل ۲ به‌علاوه ۲ چیست؟",
      "score": 20,
      "options": [
        {
          "id": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
          "order": 1,
          "text": "۴"
        },
        {
          "id": "23adb067-fe29-5b12-9a73-05f9fa9a9648",
          "order": 2,
          "text": "۵"
        }
      ]
    }
  ]
}
```

<a id="operation-070"></a>
### 070. سوابق تلاش‌های خود

```http
GET /api/v1/academic/exams/{examId}/attempts/me
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.GetMyAttempts` |
| احراز هویت و context | Organization / Student مالک attempt |
| قواعد شاخص | فهرست تلاش‌های همان Student برای آزمون مجاز؛ هر تلاش ID و deadline خودش را دارد. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;StudentExamAttemptSummaryDto&gt;](#type-studentexamattemptsummarydto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_ATTEMPT_NOT_FOUND](#error-exam-attempt-not-found) (404)، [EXAM_ATTEMPT_NOT_ALLOWED](#error-exam-attempt-not-allowed) (403) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examAttemptService.GetMyAttemptsAsync` |
| Application | [ExamAttemptService.cs](../MakanApp.Application/Assessment/ExamAttemptService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamAttemptStore` | `EfExamAttemptStore` | [EfExamAttemptStore.cs](../MakanApp.Infrastructure/Assessment/EfExamAttemptStore.cs) |
| `IExamQuestionOrderRandomizer` | `SecureExamQuestionOrderRandomizer` | [SecureExamQuestionOrderRandomizer.cs](../MakanApp.Infrastructure/Assessment/SecureExamQuestionOrderRandomizer.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/attempts/me HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
    "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
    "attemptNumber": 1,
    "startedAtUtc": "2026-11-01T09:00:00Z",
    "effectiveDeadlineUtc": "2026-11-01T09:30:00Z",
    "serverNowUtc": "2026-11-01T09:00:00Z",
    "status": "InProgress"
  }
]
```

<a id="operation-071"></a>
### 071. دریافت حق نوشتن برای نشست جاری

```http
POST /api/v1/academic/exam-attempts/{attemptId}/write-lease
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.AcquireWriteLease` |
| احراز هویت و context | Organization / Student مالک attempt |
| قواعد شاخص | Student مالک، InProgress، پیش از deadline؛ writer دیگر نباشد. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ExamWriteLeaseDto](#type-examwriteleasedto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_WRITE_LEASE_HELD_BY_OTHER_SESSION](#error-exam-write-lease-held-by-other-session) (409)، [EXAM_DEADLINE_PASSED](#error-exam-deadline-passed) (409)، [EXAM_ATTEMPT_NOT_WRITABLE](#error-exam-attempt-not-writable) (409) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examAnswerService.AcquireWriteLeaseAsync` |
| Application | [ExamAnswerService.cs](../MakanApp.Application/Assessment/ExamAnswerService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamAnswerStore` | `EfExamAnswerStore` | [EfExamAnswerStore.cs](../MakanApp.Infrastructure/Assessment/EfExamAnswerStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/write-lease HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "isCurrentSessionWriter": true,
  "writeLeaseVersion": 1,
  "canRequestTransfer": false,
  "acquiredAtUtc": "2026-11-01T09:00:00Z",
  "answerSetVersion": 1,
  "serverNowUtc": "2026-11-01T09:00:00Z"
}
```

<a id="operation-072"></a>
### 072. انتقال صریح حق نوشتن به نشست جاری

```http
POST /api/v1/academic/exam-attempts/{attemptId}/write-lease/transfer
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.TransferWriteLease` |
| احراز هویت و context | Organization / Student مالک attempt |
| قواعد شاخص | Student مالک، InProgress و قبل deadline؛ افزایش leaseVersion و انتقال صریح. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ExamWriteLeaseDto](#type-examwriteleasedto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_DEADLINE_PASSED](#error-exam-deadline-passed) (409)، [EXAM_ATTEMPT_NOT_WRITABLE](#error-exam-attempt-not-writable) (409) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examAnswerService.TransferWriteLeaseAsync` |
| Application | [ExamAnswerService.cs](../MakanApp.Application/Assessment/ExamAnswerService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamAnswerStore` | `EfExamAnswerStore` | [EfExamAnswerStore.cs](../MakanApp.Infrastructure/Assessment/EfExamAnswerStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/write-lease/transfer HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "isCurrentSessionWriter": true,
  "writeLeaseVersion": 1,
  "canRequestTransfer": false,
  "acquiredAtUtc": "2026-11-01T09:00:00Z",
  "answerSetVersion": 1,
  "serverNowUtc": "2026-11-01T09:00:00Z"
}
```

<a id="operation-073"></a>
### 073. ثبت revision پاسخ یک سؤال

```http
PUT /api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.SaveAnswer` |
| احراز هویت و context | Organization / Student مالک attempt |
| قواعد شاخص | سؤال attempt، نوع پاسخ سازگار، lease و expectedRevision؛ clientOperationId/payload ثابت. |
| ورودی | [SaveExamAnswerCommand](#type-saveexamanswercommand) |
| خروجی موفق | `200` [ExamAnswerReceiptDto](#type-examanswerreceiptdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_ANSWER_VERSION_CONFLICT](#error-exam-answer-version-conflict) (409)، [EXAM_WRITE_LEASE_STALE](#error-exam-write-lease-stale) (409)، [EXAM_ANSWER_IDEMPOTENCY_CONFLICT](#error-exam-answer-idempotency-conflict) (409)، [EXAM_DEADLINE_PASSED](#error-exam-deadline-passed) (409) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examAnswerService.SaveAnswerAsync` |
| Application | [ExamAnswerService.cs](../MakanApp.Application/Assessment/ExamAnswerService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamAnswerStore` | `EfExamAnswerStore` | [EfExamAnswerStore.cs](../MakanApp.Infrastructure/Assessment/EfExamAnswerStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |
| `attemptQuestionId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PUT /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/answers/95d0fcd9-419d-5d7c-b860-11d0ea3e014c HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "clientOperationId": "31cfe88a-21bf-51ee-b46e-5e75b259b798",
  "writeLeaseVersion": 1,
  "expectedRevisionNumber": null,
  "selectedOptionId": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
  "textAnswer": null
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examAttemptQuestionId": "95d0fcd9-419d-5d7c-b860-11d0ea3e014c",
  "answerRevisionId": "8b8482f0-b853-578e-84ea-e7653cab092d",
  "revisionNumber": 1,
  "acceptedAtUtc": "2026-11-01T09:00:00Z",
  "writeLeaseVersion": 1,
  "answerSetVersion": 1,
  "status": "Saved"
}
```

<a id="operation-074"></a>
### 074. پاسخ‌های پذیرفته‌شده و وضعیت lease

```http
GET /api/v1/academic/exam-attempts/{attemptId}/answers
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.GetMyAnswers` |
| احراز هویت و context | Organization / Student مالک attempt |
| قواعد شاخص | فقط مالک در Organization/Student؛ revisionهای موجود و lease فعلی؛ نمره ندارد. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [StudentExamAnswersDto](#type-studentexamanswersdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_ATTEMPT_NOT_FOUND](#error-exam-attempt-not-found) (404)، [EXAM_ATTEMPT_NOT_ALLOWED](#error-exam-attempt-not-allowed) (403) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examAnswerService.GetMyAnswersAsync` |
| Application | [ExamAnswerService.cs](../MakanApp.Application/Assessment/ExamAnswerService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamAnswerStore` | `EfExamAnswerStore` | [EfExamAnswerStore.cs](../MakanApp.Infrastructure/Assessment/EfExamAnswerStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/answers HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "answerSetVersion": 1,
  "writeLease": {
    "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
    "isCurrentSessionWriter": true,
    "writeLeaseVersion": 1,
    "canRequestTransfer": false,
    "acquiredAtUtc": "2026-11-01T09:00:00Z",
    "answerSetVersion": 1,
    "serverNowUtc": "2026-11-01T09:00:00Z"
  },
  "answers": [
    {
      "examAttemptQuestionId": "95d0fcd9-419d-5d7c-b860-11d0ea3e014c",
      "questionVersionId": "04636a02-95a9-52ea-a675-57c838a32619",
      "answerRevisionId": "8b8482f0-b853-578e-84ea-e7653cab092d",
      "revisionNumber": 1,
      "answerType": "ObjectiveSingleChoice",
      "selectedOptionId": "e5df6e3f-ce20-5d47-8498-5360f90bc13d",
      "textAnswer": null,
      "acceptedAtUtc": "2026-11-01T09:00:00Z"
    }
  ]
}
```

<a id="operation-075"></a>
### 075. نهایی‌سازی snapshot پاسخ‌ها

```http
POST /api/v1/academic/exam-attempts/{attemptId}/finalize
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.FinalizeAttempt` |
| احراز هویت و context | Organization / Student مالک attempt |
| قواعد شاخص | مالک و session فعال، lease و answerSetVersion جاری؛ deadline سخت؛ retry سازگار receipt ثابت. |
| ورودی | [FinalizeExamCommand](#type-finalizeexamcommand) |
| خروجی موفق | `200` [ExamFinalReceiptDto](#type-examfinalreceiptdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_DEADLINE_PASSED](#error-exam-deadline-passed) (409)، [EXAM_ANSWER_SET_VERSION_CONFLICT](#error-exam-answer-set-version-conflict) (409)، [EXAM_FINALIZE_IDEMPOTENCY_CONFLICT](#error-exam-finalize-idempotency-conflict) (409)، [EXAM_ALREADY_FINALIZED](#error-exam-already-finalized) (409) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examFinalizationService.FinalizeAsync` |
| Application | [ExamFinalizationService.cs](../MakanApp.Application/Assessment/ExamFinalizationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamFinalizationStore` | `EfExamFinalizationStore` | [EfExamFinalizationStore.cs](../MakanApp.Infrastructure/Assessment/EfExamFinalizationStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/finalize HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "clientOperationId": "31cfe88a-21bf-51ee-b46e-5e75b259b798",
  "expectedAnswerSetVersion": 1,
  "writeLeaseVersion": 1
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "attemptNumber": 1,
  "startedAtUtc": "2026-11-01T09:00:00Z",
  "finalizedAtUtc": "2026-11-01T09:20:00Z",
  "finalizedAnswerSetVersion": 1,
  "answeredQuestionCount": 1,
  "totalQuestionCount": 1,
  "status": "Finalized"
}
```

<a id="operation-076"></a>
### 076. بازیابی رسید نهایی آزمون

```http
GET /api/v1/academic/exam-attempts/{attemptId}/receipt
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamsController.GetFinalReceipt` |
| احراز هویت و context | Organization / Student مالک attempt |
| قواعد شاخص | فقط مالک؛ attempt باید Finalized باشد؛ receipt بدون نمره. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ExamFinalReceiptDto](#type-examfinalreceiptdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_ATTEMPT_NOT_FINALIZABLE](#error-exam-attempt-not-finalizable) (409)، [EXAM_ATTEMPT_NOT_FOUND](#error-exam-attempt-not-found) (404) |
| Controller | [ExamsController.cs](../MakanApp.Api/Controllers/ExamsController.cs) |
| متدهای service | `examFinalizationService.GetReceiptAsync` |
| Application | [ExamFinalizationService.cs](../MakanApp.Application/Assessment/ExamFinalizationService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamFinalizationStore` | `EfExamFinalizationStore` | [EfExamFinalizationStore.cs](../MakanApp.Infrastructure/Assessment/EfExamFinalizationStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/receipt HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "attemptNumber": 1,
  "startedAtUtc": "2026-11-01T09:00:00Z",
  "finalizedAtUtc": "2026-11-01T09:20:00Z",
  "finalizedAnswerSetVersion": 1,
  "answeredQuestionCount": 1,
  "totalQuestionCount": 1,
  "status": "Finalized"
}
```

<a id="operation-077"></a>
### 077. صف تصحیح تلاش‌های نهایی آزمون

```http
GET /api/v1/academic/exams/{examId}/grading
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamGradingsController.GetQueue` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | ارزیاب مجاز آزمون؛ classId/status اختیاری؛ فقط تلاش‌های قابل صف تصحیح. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;ExamGradingQueueItemDto&gt;](#type-examgradingqueueitemdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_GRADE_NOT_ALLOWED](#error-exam-grade-not-allowed) (403)، [TEACHER_NOT_ASSIGNED](#error-teacher-not-assigned) (403) |
| Controller | [ExamGradingsController.cs](../MakanApp.Api/Controllers/ExamGradingsController.cs) |
| متدهای service | `gradingService.GetQueueAsync` |
| Application | [ExamGradingService.Reads.cs](../MakanApp.Application/Assessment/ExamGradingService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `examId` | route | `UUID` | مطابق route و scope مجاز |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `status` | query | `ExamGradingQueueStatus?` | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exams/29d391de-01ad-5b49-8986-177d7566800c/grading HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
    "examId": "29d391de-01ad-5b49-8986-177d7566800c",
    "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
    "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
    "examTitle": "آزمون نمونه",
    "studentDisplayName": "دانش‌آموز نمونه",
    "attemptNumber": 1,
    "finalizedAtUtc": "2026-11-01T09:20:00Z",
    "maximumScore": 20,
    "status": "AwaitingGrading",
    "gradeRevisionNumber": null,
    "requiresManualReview": false
  }
]
```

<a id="operation-078"></a>
### 078. مدل تصحیح برای ارزیاب مجاز

```http
GET /api/v1/academic/exam-attempts/{attemptId}/grading
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamGradingsController.GetForGrading` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | ارزیاب مجاز؛ داده خصوصی تصحیح؛ وجود grade طبق lifecycle. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ExamGradeDto](#type-examgradedto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_GRADE_NOT_FOUND](#error-exam-grade-not-found) (404)، [EXAM_GRADE_ATTEMPT_NOT_FINALIZED](#error-exam-grade-attempt-not-finalized) (409) |
| Controller | [ExamGradingsController.cs](../MakanApp.Api/Controllers/ExamGradingsController.cs) |
| متدهای service | `gradingService.GetForGradingAsync` |
| Application | [ExamGradingService.Reads.cs](../MakanApp.Application/Assessment/ExamGradingService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/grading HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examGradeRevisionId": "fccf85a7-af09-5fb7-8d52-166a5477e639",
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "examTitle": "آزمون نمونه",
  "studentDisplayName": "دانش‌آموز نمونه",
  "attemptNumber": 1,
  "finalizedAtUtc": "2026-11-01T09:20:00Z",
  "revisionNumber": 1,
  "totalScore": 16,
  "maximumScore": 20,
  "status": "Draft",
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "evaluatorPrivateNote": null,
  "createdByMembershipId": "888ea186-0fc3-575a-949b-122aa6ac97c9",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "supersedesExamGradeRevisionId": null,
  "correctionReason": null,
  "releasedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "examAttemptQuestionId": "95d0fcd9-419d-5d7c-b860-11d0ea3e014c",
      "questionVersionId": "04636a02-95a9-52ea-a675-57c838a32619",
      "displayOrder": 1,
      "questionType": "Descriptive",
      "prompt": "روش حل مسئله را توضیح دهید.",
      "isAnswered": true,
      "selectedOptionText": null,
      "textAnswer": "پاسخ تشریحی نمونه",
      "awardedScore": 16,
      "maximumScore": 20,
      "gradingMode": "Manual",
      "isReviewed": true,
      "learnerFeedback": null,
      "evaluatorPrivateNote": null,
      "reviewedByMembershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
      "reviewedAtUtc": "2026-11-01T09:00:00Z"
    }
  ]
}
```

<a id="operation-079"></a>
### 079. ایجاد یا بازیابی draft نمره آزمون

```http
POST /api/v1/academic/exam-attempts/{attemptId}/grading
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamGradingsController.Initialize` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | فقط Finalized؛ draft موجود reuse؛ objective خودکار و descriptive دستی. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ExamGradeDto](#type-examgradedto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_GRADE_ATTEMPT_NOT_FINALIZED](#error-exam-grade-attempt-not-finalized) (409)، [EXAM_GRADE_ALREADY_RELEASED](#error-exam-grade-already-released) (409) |
| Controller | [ExamGradingsController.cs](../MakanApp.Api/Controllers/ExamGradingsController.cs) |
| متدهای service | `gradingService.CreateOrGetDraftAsync` |
| Application | [ExamGradingService.cs](../MakanApp.Application/Assessment/ExamGradingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamGradingStore` | `EfExamGradingStore` | [EfExamGradingStore.cs](../MakanApp.Infrastructure/Assessment/EfExamGradingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/grading HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examGradeRevisionId": "fccf85a7-af09-5fb7-8d52-166a5477e639",
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "examTitle": "آزمون نمونه",
  "studentDisplayName": "دانش‌آموز نمونه",
  "attemptNumber": 1,
  "finalizedAtUtc": "2026-11-01T09:20:00Z",
  "revisionNumber": 1,
  "totalScore": 0,
  "maximumScore": 20,
  "status": "Draft",
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "evaluatorPrivateNote": null,
  "createdByMembershipId": "888ea186-0fc3-575a-949b-122aa6ac97c9",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "supersedesExamGradeRevisionId": null,
  "correctionReason": null,
  "releasedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "examAttemptQuestionId": "95d0fcd9-419d-5d7c-b860-11d0ea3e014c",
      "questionVersionId": "04636a02-95a9-52ea-a675-57c838a32619",
      "displayOrder": 1,
      "questionType": "Descriptive",
      "prompt": "روش حل مسئله را توضیح دهید.",
      "isAnswered": true,
      "selectedOptionText": null,
      "textAnswer": "پاسخ تشریحی نمونه",
      "awardedScore": 0,
      "maximumScore": 20,
      "gradingMode": "Manual",
      "isReviewed": false,
      "learnerFeedback": null,
      "evaluatorPrivateNote": null,
      "reviewedByMembershipId": null,
      "reviewedAtUtc": null
    }
  ]
}
```

<a id="operation-080"></a>
### 080. ثبت نمره و بازخورد سؤال تشریحی

```http
PATCH /api/v1/academic/exam-attempts/{attemptId}/grading/questions/{attemptQuestionId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamGradingsController.GradeQuestion` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | سؤال Manual، score بین صفر و بارم؛ دو اعشار؛ نسخه grade جاری. |
| ورودی | [GradeExamQuestionCommand](#type-gradeexamquestioncommand) |
| خروجی موفق | `200` [ExamGradeDto](#type-examgradedto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_GRADE_SCORE_INVALID](#error-exam-grade-score-invalid) (400)، [EXAM_GRADE_ALREADY_RELEASED](#error-exam-grade-already-released) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [ExamGradingsController.cs](../MakanApp.Api/Controllers/ExamGradingsController.cs) |
| متدهای service | `gradingService.GradeQuestionAsync` |
| Application | [ExamGradingService.cs](../MakanApp.Application/Assessment/ExamGradingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamGradingStore` | `EfExamGradingStore` | [EfExamGradingStore.cs](../MakanApp.Infrastructure/Assessment/EfExamGradingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |
| `attemptQuestionId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/grading/questions/95d0fcd9-419d-5d7c-b860-11d0ea3e014c HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "awardedScore": 16,
  "learnerFeedback": null,
  "evaluatorPrivateNote": null,
  "expectedGradeRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examGradeRevisionId": "fccf85a7-af09-5fb7-8d52-166a5477e639",
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "examTitle": "آزمون نمونه",
  "studentDisplayName": "دانش‌آموز نمونه",
  "attemptNumber": 1,
  "finalizedAtUtc": "2026-11-01T09:20:00Z",
  "revisionNumber": 1,
  "totalScore": 16,
  "maximumScore": 20,
  "status": "Draft",
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "evaluatorPrivateNote": null,
  "createdByMembershipId": "888ea186-0fc3-575a-949b-122aa6ac97c9",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "supersedesExamGradeRevisionId": null,
  "correctionReason": null,
  "releasedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "examAttemptQuestionId": "95d0fcd9-419d-5d7c-b860-11d0ea3e014c",
      "questionVersionId": "04636a02-95a9-52ea-a675-57c838a32619",
      "displayOrder": 1,
      "questionType": "Descriptive",
      "prompt": "روش حل مسئله را توضیح دهید.",
      "isAnswered": true,
      "selectedOptionText": null,
      "textAnswer": "پاسخ تشریحی نمونه",
      "awardedScore": 16,
      "maximumScore": 20,
      "gradingMode": "Manual",
      "isReviewed": true,
      "learnerFeedback": null,
      "evaluatorPrivateNote": null,
      "reviewedByMembershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
      "reviewedAtUtc": "2026-11-01T09:00:00Z"
    }
  ]
}
```

<a id="operation-081"></a>
### 081. تکمیل بازبینی و آماده‌سازی انتشار

```http
POST /api/v1/academic/exam-attempts/{attemptId}/grading/complete
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamGradingsController.Complete` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | تمام سؤال‌های دستی بررسی‌شده؛ feedbackها تا ۱۰۰۰۰؛ expectedGradeRowVersion. |
| ورودی | [CompleteExamGradeCommand](#type-completeexamgradecommand) |
| خروجی موفق | `200` [ExamGradeDto](#type-examgradedto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_GRADE_QUESTION_NOT_REVIEWED](#error-exam-grade-question-not-reviewed) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [ExamGradingsController.cs](../MakanApp.Api/Controllers/ExamGradingsController.cs) |
| متدهای service | `gradingService.CompleteReviewAsync` |
| Application | [ExamGradingService.cs](../MakanApp.Application/Assessment/ExamGradingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamGradingStore` | `EfExamGradingStore` | [EfExamGradingStore.cs](../MakanApp.Infrastructure/Assessment/EfExamGradingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/grading/complete HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "evaluatorPrivateNote": null,
  "expectedGradeRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examGradeRevisionId": "fccf85a7-af09-5fb7-8d52-166a5477e639",
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "examTitle": "آزمون نمونه",
  "studentDisplayName": "دانش‌آموز نمونه",
  "attemptNumber": 1,
  "finalizedAtUtc": "2026-11-01T09:20:00Z",
  "revisionNumber": 1,
  "totalScore": 16,
  "maximumScore": 20,
  "status": "ReadyForRelease",
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "evaluatorPrivateNote": null,
  "createdByMembershipId": "888ea186-0fc3-575a-949b-122aa6ac97c9",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "supersedesExamGradeRevisionId": null,
  "correctionReason": null,
  "releasedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "examAttemptQuestionId": "95d0fcd9-419d-5d7c-b860-11d0ea3e014c",
      "questionVersionId": "04636a02-95a9-52ea-a675-57c838a32619",
      "displayOrder": 1,
      "questionType": "Descriptive",
      "prompt": "روش حل مسئله را توضیح دهید.",
      "isAnswered": true,
      "selectedOptionText": null,
      "textAnswer": "پاسخ تشریحی نمونه",
      "awardedScore": 16,
      "maximumScore": 20,
      "gradingMode": "Manual",
      "isReviewed": true,
      "learnerFeedback": null,
      "evaluatorPrivateNote": null,
      "reviewedByMembershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
      "reviewedAtUtc": "2026-11-01T09:00:00Z"
    }
  ]
}
```

<a id="operation-082"></a>
### 082. انتشار نمره آزمون

```http
POST /api/v1/academic/exam-attempts/{attemptId}/grade/release
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamGradingsController.Release` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | ReadyForRelease، نمره کامل؛ operationId/version ثابت؛ انتشار مستقل. |
| ورودی | [ReleaseExamGradeCommand](#type-releaseexamgradecommand) |
| خروجی موفق | `200` [ExamGradeReleaseDto](#type-examgradereleasedto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_GRADE_INCOMPLETE](#error-exam-grade-incomplete) (409)، [EXAM_GRADE_RELEASE_IDEMPOTENCY_CONFLICT](#error-exam-grade-release-idempotency-conflict) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [ExamGradingsController.cs](../MakanApp.Api/Controllers/ExamGradingsController.cs) |
| متدهای service | `gradingService.ReleaseAsync` |
| Application | [ExamGradingService.cs](../MakanApp.Application/Assessment/ExamGradingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamGradingStore` | `EfExamGradingStore` | [EfExamGradingStore.cs](../MakanApp.Infrastructure/Assessment/EfExamGradingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/grade/release HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "clientOperationId": "31cfe88a-21bf-51ee-b46e-5e75b259b798",
  "expectedGradeRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examGradeReleaseId": "6da3f9e7-0073-5619-915e-20c67149a007",
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examGradeRevisionId": "fccf85a7-af09-5fb7-8d52-166a5477e639",
  "gradeRevisionNumber": 1,
  "totalScore": 16,
  "maximumScore": 20,
  "releasedAtUtc": "2026-11-02T09:00:00Z",
  "releasedByMembershipId": "a80c5e5a-7129-57e2-bd61-71b8bcca65ca",
  "gradeRowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-083"></a>
### 083. ساخت نسخه اصلاحی نمره آزمون

```http
POST /api/v1/academic/exam-attempts/{attemptId}/grade/corrections
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamGradingsController.Correct` |
| احراز هویت و context | Organization / Manager یا Teacher منتسب |
| قواعد شاخص | Released فعلی، reason تا ۱۰۰۰ غیرخالی و expectedReleasedGradeRowVersion. |
| ورودی | [CorrectReleasedExamGradeCommand](#type-correctreleasedexamgradecommand) |
| خروجی موفق | `201` [ExamGradeDto](#type-examgradedto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_GRADE_NOT_RELEASED](#error-exam-grade-not-released) (409)، [EXAM_GRADE_RELEASE_CONFLICT](#error-exam-grade-release-conflict) (409)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [ExamGradingsController.cs](../MakanApp.Api/Controllers/ExamGradingsController.cs) |
| متدهای service | `gradingService.CorrectReleasedAsync` |
| Application | [ExamGradingService.cs](../MakanApp.Application/Assessment/ExamGradingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IExamGradingStore` | `EfExamGradingStore` | [EfExamGradingStore.cs](../MakanApp.Infrastructure/Assessment/EfExamGradingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/grade/corrections HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "correctionReason": "اصلاح پس از بررسی دوباره پاسخ",
  "expectedReleasedGradeRowVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "examGradeRevisionId": "fccf85a7-af09-5fb7-8d52-166a5477e639",
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "examTitle": "آزمون نمونه",
  "studentDisplayName": "دانش‌آموز نمونه",
  "attemptNumber": 1,
  "finalizedAtUtc": "2026-11-01T09:20:00Z",
  "revisionNumber": 2,
  "totalScore": 16,
  "maximumScore": 20,
  "status": "Draft",
  "learnerFeedback": null,
  "guardianVisibleFeedback": null,
  "evaluatorPrivateNote": null,
  "createdByMembershipId": "888ea186-0fc3-575a-949b-122aa6ac97c9",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "supersedesExamGradeRevisionId": "e9ca01e3-9461-553e-8975-dfbf881279d5",
  "correctionReason": "اصلاح پس از بررسی دوباره پاسخ",
  "releasedAtUtc": null,
  "rowVersion": "AAAAAAAAAAE=",
  "questions": [
    {
      "examAttemptQuestionId": "95d0fcd9-419d-5d7c-b860-11d0ea3e014c",
      "questionVersionId": "04636a02-95a9-52ea-a675-57c838a32619",
      "displayOrder": 1,
      "questionType": "Descriptive",
      "prompt": "روش حل مسئله را توضیح دهید.",
      "isAnswered": true,
      "selectedOptionText": null,
      "textAnswer": "پاسخ تشریحی نمونه",
      "awardedScore": 16,
      "maximumScore": 20,
      "gradingMode": "Manual",
      "isReviewed": true,
      "learnerFeedback": null,
      "evaluatorPrivateNote": null,
      "reviewedByMembershipId": "914099c9-0b35-5612-8a44-f5a00179add7",
      "reviewedAtUtc": "2026-11-01T09:00:00Z"
    }
  ]
}
```

<a id="operation-084"></a>
### 084. وضعیت انتشار و نتیجه دانش‌آموز

```http
GET /api/v1/academic/exam-attempts/{attemptId}/result
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamGradingsController.GetStudentResult` |
| احراز هویت و context | Organization / Student مالک |
| قواعد شاخص | Student مالک؛ نتیجه قبل release هم 200 با score=null و releaseStatus انتظار است. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [StudentExamResultDto](#type-studentexamresultdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [EXAM_GRADE_NOT_ALLOWED](#error-exam-grade-not-allowed) (403)، [EXAM_GRADE_ATTEMPT_NOT_FINALIZED](#error-exam-grade-attempt-not-finalized) (409) |
| Controller | [ExamGradingsController.cs](../MakanApp.Api/Controllers/ExamGradingsController.cs) |
| متدهای service | `gradingService.GetStudentResultAsync` |
| Application | [ExamGradingService.Reads.cs](../MakanApp.Application/Assessment/ExamGradingService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/result HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "attemptNumber": 1,
  "finalizedAtUtc": "2026-11-01T09:20:00Z",
  "releaseStatus": "AwaitingGrading",
  "gradeRevisionNumber": null,
  "releasedAtUtc": null,
  "score": null,
  "maximumScore": null,
  "learnerFeedback": null
}
```

<a id="operation-085"></a>
### 085. وضعیت انتشار و نتیجه مجاز والد

```http
GET /api/v1/academic/exam-attempts/{attemptId}/guardian-result
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Assessment`؛ `ExamGradingsController.GetGuardianResult` |
| احراز هویت و context | Organization / Parent با child معتبر |
| قواعد شاخص | Parent با child معتبر؛ DTO فقط نتیجه قابل انتشار والد. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [GuardianExamResultDto](#type-guardianexamresultdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403)، [EXAM_GRADE_NOT_FOUND](#error-exam-grade-not-found) (404) |
| Controller | [ExamGradingsController.cs](../MakanApp.Api/Controllers/ExamGradingsController.cs) |
| متدهای service | `gradingService.GetGuardianResultAsync` |
| Application | [ExamGradingService.Reads.cs](../MakanApp.Application/Assessment/ExamGradingService.Reads.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `attemptId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/academic/exam-attempts/a85f3deb-7daa-516b-a0b6-3164b91dbae9/guardian-result HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "examAttemptId": "a85f3deb-7daa-516b-a0b6-3164b91dbae9",
  "examId": "29d391de-01ad-5b49-8986-177d7566800c",
  "examVersionId": "ef832dbc-a56c-540d-8c8a-9652f46f7beb",
  "attemptNumber": 1,
  "finalizedAtUtc": "2026-11-01T09:20:00Z",
  "releaseStatus": "AwaitingGrading",
  "gradeRevisionNumber": null,
  "releasedAtUtc": null,
  "score": null,
  "maximumScore": null,
  "guardianVisibleFeedback": null
}
```

<a id="operation-086"></a>
### 086. بارگذاری فایل و ثبت metadata

```http
POST /api/v1/files
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Storage`؛ `FilesController.Upload` |
| احراز هویت و context | نشست معتبر؛ مالک، scope و مجوز منبع متصل |
| قواعد شاخص | multipart File؛ اندازه ۱..۱۰MiB و MIME مجاز پایه؛ بررسی محتوا؛ بدون URL عمومی. |
| ورودی | [UploadFileRequest](#type-uploadfilerequest) |
| خروجی موفق | `201` [FileAssetResult](#type-fileassetresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [FILE_TOO_LARGE](#error-file-too-large) (413)، [FILE_TYPE_NOT_ALLOWED](#error-file-type-not-allowed) (415)، [FILE_UPLOAD_FAILED](#error-file-upload-failed) (500)، [FILE_STORAGE_FAILED](#error-file-storage-failed) (503) |
| Controller | [FilesController.cs](../MakanApp.Api/Controllers/FilesController.cs) |
| متدهای service | `storageService.UploadFileAsync` |
| Application | [StorageService.cs](../MakanApp.Application/Storage/StorageService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IFileAssetStore` | `EfFileAssetStore` | [EfFileAssetStore.cs](../MakanApp.Infrastructure/Storage/EfFileAssetStore.cs) |
| `IFileAssetBoundAccessResolver` | `EfFileAssetBoundAccessResolver` | [EfFileAssetBoundAccessResolver.cs](../MakanApp.Infrastructure/Storage/EfFileAssetBoundAccessResolver.cs) |
| `IFileStorage` | `LocalFileStorage` / `UnavailableFileStorage`؛ طبق محیط و `Storage:RootPath` | [LocalFileStorage](../MakanApp.Infrastructure/Storage/LocalFileStorage.cs) و [UnavailableFileStorage](../MakanApp.Infrastructure/Storage/UnavailableFileStorage.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/files HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: multipart/form-data; boundary=makan-sample

--makan-sample
Content-Disposition: form-data; name="File"; filename="sample.pdf"
Content-Type: application/pdf

<binary content>
--makan-sample--
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "id": "d3d55fe5-9f38-551b-9cbc-36d104ba44f8",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "uploadedByUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "originalFileName": "sample.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 1024,
  "sha256Hash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "status": "Ready",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "completedAtUtc": "2026-11-01T09:00:00Z",
  "deletedAtUtc": null,
  "retainedAtUtc": null,
  "unattachedExpiresAtUtc": "2026-11-02T09:00:00Z",
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-087"></a>
### 087. دریافت وضعیت و مشخصات فایل مجاز

```http
GET /api/v1/files/{fileId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Storage`؛ `FilesController.GetMetadata` |
| احراز هویت و context | نشست معتبر؛ مالک، scope و مجوز منبع متصل |
| قواعد شاخص | مجوز scope و مالک یا منبع متصل؛ شناسه فایل مجوز نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [FileAssetResult](#type-fileassetresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [FILE_NOT_FOUND](#error-file-not-found) (404)، [FILE_NOT_ALLOWED](#error-file-not-allowed) (403) |
| Controller | [FilesController.cs](../MakanApp.Api/Controllers/FilesController.cs) |
| متدهای service | `storageService.GetFileMetadataAsync` |
| Application | [StorageService.cs](../MakanApp.Application/Storage/StorageService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IFileAssetStore` | `EfFileAssetStore` | [EfFileAssetStore.cs](../MakanApp.Infrastructure/Storage/EfFileAssetStore.cs) |
| `IFileAssetBoundAccessResolver` | `EfFileAssetBoundAccessResolver` | [EfFileAssetBoundAccessResolver.cs](../MakanApp.Infrastructure/Storage/EfFileAssetBoundAccessResolver.cs) |
| `IFileStorage` | `LocalFileStorage` / `UnavailableFileStorage`؛ طبق محیط و `Storage:RootPath` | [LocalFileStorage](../MakanApp.Infrastructure/Storage/LocalFileStorage.cs) و [UnavailableFileStorage](../MakanApp.Infrastructure/Storage/UnavailableFileStorage.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `fileId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/files/d3d55fe5-9f38-551b-9cbc-36d104ba44f8 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "d3d55fe5-9f38-551b-9cbc-36d104ba44f8",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "uploadedByUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "originalFileName": "sample.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 1024,
  "sha256Hash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "status": "Ready",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "completedAtUtc": "2026-11-01T09:00:00Z",
  "deletedAtUtc": null,
  "retainedAtUtc": null,
  "unattachedExpiresAtUtc": "2026-11-02T09:00:00Z",
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-088"></a>
### 088. دانلود محتوای باینری مجاز

```http
GET /api/v1/files/{fileId}/content
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Storage`؛ `FilesController.Download` |
| احراز هویت و context | نشست معتبر؛ مالک، scope و مجوز منبع متصل |
| قواعد شاخص | فایل Ready؛ مالک/مجوز binding جاری؛ response باینری. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` stream باینری فایل |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [FILE_NOT_FOUND](#error-file-not-found) (404)، [FILE_NOT_READY](#error-file-not-ready) (409)، [FILE_NOT_ALLOWED](#error-file-not-allowed) (403)، [FILE_STORAGE_FAILED](#error-file-storage-failed) (503) |
| Controller | [FilesController.cs](../MakanApp.Api/Controllers/FilesController.cs) |
| متدهای service | `storageService.DownloadFileAsync` |
| Application | [StorageService.cs](../MakanApp.Application/Storage/StorageService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IFileAssetStore` | `EfFileAssetStore` | [EfFileAssetStore.cs](../MakanApp.Infrastructure/Storage/EfFileAssetStore.cs) |
| `IFileAssetBoundAccessResolver` | `EfFileAssetBoundAccessResolver` | [EfFileAssetBoundAccessResolver.cs](../MakanApp.Infrastructure/Storage/EfFileAssetBoundAccessResolver.cs) |
| `IFileStorage` | `LocalFileStorage` / `UnavailableFileStorage`؛ طبق محیط و `Storage:RootPath` | [LocalFileStorage](../MakanApp.Infrastructure/Storage/LocalFileStorage.cs) و [UnavailableFileStorage](../MakanApp.Infrastructure/Storage/UnavailableFileStorage.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `fileId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/files/d3d55fe5-9f38-551b-9cbc-36d104ba44f8/content HTTP/1.1
Host: localhost:7063
Accept: application/octet-stream
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```http
HTTP/1.1 200 OK
Content-Type: application/pdf
Content-Disposition: attachment; filename="sample.pdf"

<binary content>
```

<a id="operation-089"></a>
### 089. حذف فایل بدون مصرف با کنترل نسخه

```http
DELETE /api/v1/files/{fileId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Storage`؛ `FilesController.Delete` |
| احراز هویت و context | نشست معتبر؛ مالک، scope و مجوز منبع متصل |
| قواعد شاخص | فایل خود با expectedRowVersion؛ retained/in-use قابل حذف نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [FileAssetResult](#type-fileassetresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [FILE_IN_USE](#error-file-in-use) (409)، [FILE_ALREADY_DELETED](#error-file-already-deleted) (409)، [FILE_NOT_ALLOWED](#error-file-not-allowed) (403)، [CONCURRENCY_CONFLICT](#error-concurrency-conflict) (412) |
| Controller | [FilesController.cs](../MakanApp.Api/Controllers/FilesController.cs) |
| متدهای service | `storageService.DeleteFileAsync` |
| Application | [StorageService.cs](../MakanApp.Application/Storage/StorageService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IFileAssetStore` | `EfFileAssetStore` | [EfFileAssetStore.cs](../MakanApp.Infrastructure/Storage/EfFileAssetStore.cs) |
| `IFileAssetBoundAccessResolver` | `EfFileAssetBoundAccessResolver` | [EfFileAssetBoundAccessResolver.cs](../MakanApp.Infrastructure/Storage/EfFileAssetBoundAccessResolver.cs) |
| `IFileStorage` | `LocalFileStorage` / `UnavailableFileStorage`؛ طبق محیط و `Storage:RootPath` | [LocalFileStorage](../MakanApp.Infrastructure/Storage/LocalFileStorage.cs) و [UnavailableFileStorage](../MakanApp.Infrastructure/Storage/UnavailableFileStorage.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `fileId` | route | `UUID` | مطابق route و scope مجاز |
| `expectedRowVersion` | query | `string` | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. |


نمونه درخواست مصنوعی:

```http
DELETE /api/v1/files/d3d55fe5-9f38-551b-9cbc-36d104ba44f8?expectedRowVersion=AAAAAAAAAAE%3D HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "id": "d3d55fe5-9f38-551b-9cbc-36d104ba44f8",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "uploadedByUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "originalFileName": "sample.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 1024,
  "sha256Hash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "status": "Deleted",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "completedAtUtc": "2026-11-01T09:00:00Z",
  "deletedAtUtc": "2026-11-01T09:00:00Z",
  "retainedAtUtc": null,
  "unattachedExpiresAtUtc": "2026-11-02T09:00:00Z",
  "rowVersion": "AAAAAAAAAAE="
}
```

<a id="operation-090"></a>
### 090. ساخت گروه با اعضای واجد شرایط

```http
POST /api/v1/conversations/groups
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.CreateGroup` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | creator eligible، title معتبر، اعضای اولیه eligible، clientOperationId/hash؛ duplicate سازگار 200. |
| ورودی | [CreateManagedConversationCommand](#type-createmanagedconversationcommand) |
| خروجی موفق | `200` [ManagedConversationResult](#type-managedconversationresult)؛ `201` [ManagedConversationResult](#type-managedconversationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CONVERSATION_CREATION_CONFLICT](#error-conversation-creation-conflict) (409)، [CONVERSATION_MANAGEMENT_NOT_ALLOWED](#error-conversation-management-not-allowed) (403) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.CreateGroupAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/groups HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "clientOperationId": "31cfe88a-21bf-51ee-b46e-5e75b259b798",
  "scope": "Organization",
  "title": "گروه نمونه",
  "description": null,
  "initialParticipantUserIds": [
    "d9ce59f5-ec1c-5115-91ab-3fd85f64431a"
  ]
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "type": "Group",
  "scope": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "title": "گروه نمونه",
  "description": null,
  "managementPolicy": "UserManaged",
  "status": "Active",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "archivedAtUtc": null,
  "participants": [
    {
      "participantId": "0307c9d2-b8ee-543c-8660-65c70b84bab7",
      "user": {
        "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
        "username": "sample.user",
        "displayName": "کاربر نمونه"
      },
      "role": "Owner",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    },
    {
      "participantId": "382320a3-1ff0-5866-841b-3e661c622cff",
      "user": {
        "userId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
        "username": "sample.other",
        "displayName": "مخاطب نمونه"
      },
      "role": "Member",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    }
  ],
  "alreadyExisted": false
}
```

<a id="operation-091"></a>
### 091. ساخت کانال با اعضای واجد شرایط

```http
POST /api/v1/conversations/channels
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.CreateChannel` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | همان قواعد ساخت گروه؛ Channel فقط Owner/Admin حق publish دارند. |
| ورودی | [CreateManagedConversationCommand](#type-createmanagedconversationcommand) |
| خروجی موفق | `200` [ManagedConversationResult](#type-managedconversationresult)؛ `201` [ManagedConversationResult](#type-managedconversationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CONVERSATION_CREATION_CONFLICT](#error-conversation-creation-conflict) (409)، [CONVERSATION_MANAGEMENT_NOT_ALLOWED](#error-conversation-management-not-allowed) (403) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.CreateChannelAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/channels HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "clientOperationId": "31cfe88a-21bf-51ee-b46e-5e75b259b798",
  "scope": "Organization",
  "title": "کانال نمونه",
  "description": null,
  "initialParticipantUserIds": [
    "d9ce59f5-ec1c-5115-91ab-3fd85f64431a"
  ]
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "type": "Channel",
  "scope": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "title": "کانال نمونه",
  "description": null,
  "managementPolicy": "UserManaged",
  "status": "Active",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "archivedAtUtc": null,
  "participants": [
    {
      "participantId": "0307c9d2-b8ee-543c-8660-65c70b84bab7",
      "user": {
        "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
        "username": "sample.user",
        "displayName": "کاربر نمونه"
      },
      "role": "Owner",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    },
    {
      "participantId": "382320a3-1ff0-5866-841b-3e661c622cff",
      "user": {
        "userId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
        "username": "sample.other",
        "displayName": "مخاطب نمونه"
      },
      "role": "Member",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    }
  ],
  "alreadyExisted": false
}
```

<a id="operation-092"></a>
### 092. ساخت یا بازیابی گفت‌وگوی مستقیم یکتا

```http
POST /api/v1/conversations/direct
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.StartDirect` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | جفت یکتا، Personal adult+grant یا سازمان/رابطه معتبر؛ بلاک دوطرفه کنترل می‌شود. |
| ورودی | [StartDirectConversationCommand](#type-startdirectconversationcommand) |
| خروجی موفق | `200` [DirectConversationResult](#type-directconversationresult)؛ `201` [DirectConversationResult](#type-directconversationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [DIRECT_RECIPIENT_NOT_AVAILABLE](#error-direct-recipient-not-available) (404)، [CONVERSATION_NOT_ALLOWED](#error-conversation-not-allowed) (403) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.StartOrGetDirectConversationAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/direct HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "targetUserId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
  "scope": "Organization"
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "type": "Direct",
  "scope": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "otherParticipant": {
    "userId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
    "username": "sample.other",
    "displayName": "مخاطب نمونه"
  },
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "alreadyExisted": false
}
```

<a id="operation-093"></a>
### 093. فهرست گفتگوهای فعال فضای جاری

```http
GET /api/v1/conversations
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.GetMine` |
| احراز هویت و context | participant فعال در scope فضای کاری جاری |
| قواعد شاخص | گفتگوهای مجاز در workspace جاری براساس participant فعال و policy scope؛ فهرست سراسری همه گفتگوها نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;ConversationSummaryResult&gt;](#type-conversationsummaryresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CONVERSATION_NOT_FOUND](#error-conversation-not-found) (404)، [ORGANIZATION_SCOPE_MISMATCH](#error-organization-scope-mismatch) (403) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.GetMyConversationsAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/conversations HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
    "type": "Direct",
    "scope": "Organization",
    "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
    "title": null,
    "otherParticipant": {
      "userId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
      "username": "sample.other",
      "displayName": "مخاطب نمونه"
    },
    "lastMessagePreview": "پیام نمونه",
    "lastMessageAtUtc": "2026-11-01T09:00:00Z",
    "lastMessageSequence": 1,
    "lastDeliveredMessageSequence": 1,
    "lastReadMessageSequence": 0,
    "unreadCount": 1
  }
]
```

<a id="operation-094"></a>
### 094. صفحه تاریخچه پیام‌ها

```http
GET /api/v1/conversations/{conversationId}/messages
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.GetMessages` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | participant فعال؛ scope مجاز؛ beforeSequence>0؛ limit ۱..۱۰۰. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ConversationMessagePageResult](#type-conversationmessagepageresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CONVERSATION_NOT_FOUND](#error-conversation-not-found) (404)، [ORGANIZATION_SCOPE_MISMATCH](#error-organization-scope-mismatch) (403) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.GetConversationMessagesAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `beforeSequence` | query | `long?` | شماره پیام مرزی برای صفحه تاریخچه؛ cursor تغییرات نیست. |
| `limit` | query | `int?` | سقف تعداد نتیجه صفحه طبق قواعد همین عملیات. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/messages?limit=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "messages": [
    {
      "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
      "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
      "senderUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
      "sender": {
        "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
        "username": "sample.user",
        "displayName": "کاربر نمونه"
      },
      "clientMessageId": "270ca0fa-a60a-5cd0-94e4-fe59c99f8551",
      "sequence": 1,
      "kind": "Text",
      "text": "پیام نمونه",
      "sentAtUtc": "2026-11-01T09:00:00Z",
      "editedAtUtc": null,
      "deletedAtUtc": null,
      "isEdited": false,
      "isDeleted": false,
      "reply": null,
      "isForwarded": false,
      "reactions": [],
      "mentions": [],
      "attachments": [],
      "isPinned": false,
      "version": "AAAAAAAAAAE="
    }
  ],
  "nextBeforeSequence": null
}
```

<a id="operation-095"></a>
### 095. ارسال پیام و دریافت رسید commit

```http
POST /api/v1/conversations/{conversationId}/messages
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.SendMessage` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | clientMessageId غیرخالی؛ Text غیرخالی/تا۴۰۰۰؛ انواع فایل با attachment مجاز؛ publish eligibility. |
| ورودی | [SendMessageCommand](#type-sendmessagecommand) |
| خروجی موفق | `200` [MessageReceiptResult](#type-messagereceiptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [MESSAGE_IDEMPOTENCY_CONFLICT](#error-message-idempotency-conflict) (409)، [MESSAGE_PUBLISH_NOT_ALLOWED](#error-message-publish-not-allowed) (403)، [MESSAGE_ATTACHMENT_NOT_READY](#error-message-attachment-not-ready) (409)، [CONVERSATION_ARCHIVED](#error-conversation-archived) (409) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.SendMessageAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/messages HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "clientMessageId": "270ca0fa-a60a-5cd0-94e4-fe59c99f8551",
  "kind": "Text",
  "text": "پیام نمونه",
  "attachmentIds": [],
  "replyToMessageId": null,
  "mentionedUserIds": []
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "clientMessageId": "270ca0fa-a60a-5cd0-94e4-fe59c99f8551",
  "sequence": 1,
  "kind": "Text",
  "sentAtUtc": "2026-11-01T09:00:00Z",
  "status": "Sent",
  "version": "AAAAAAAAAAE="
}
```

<a id="operation-096"></a>
### 096. ویرایش متن یا caption پیام خود

```http
PATCH /api/v1/conversations/{conversationId}/messages/{messageId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.EditMessage` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | فقط sender؛ متن/caption سازگار؛ expectedVersion جاری؛ tombstone قابل ویرایش نیست. |
| ورودی | [EditMessageCommand](#type-editmessagecommand) |
| خروجی موفق | `200` [MessageMutationResult](#type-messagemutationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [MESSAGE_NOT_EDITABLE](#error-message-not-editable) (409)، [MESSAGE_DELETED](#error-message-deleted) (409)، [MESSAGE_EDIT_CONFLICT](#error-message-edit-conflict) (412) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.EditMessageAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `messageId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/messages/97215dcf-e9d1-5d83-9a4a-62af56aa58ef HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "text": "متن ویرایش‌شده نمونه",
  "expectedVersion": "AAAAAAAAAAE="
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
  "sequence": 1,
  "editedAtUtc": "2026-11-01T09:00:00Z",
  "deletedAtUtc": null,
  "isDeleted": false,
  "version": "AAAAAAAAAAE="
}
```

<a id="operation-097"></a>
### 097. حذف پیام خود برای همه به شکل tombstone

```http
DELETE /api/v1/conversations/{conversationId}/messages/{messageId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.DeleteMessage` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | فقط sender؛ expectedVersion query؛ tombstone ایجاد می‌شود. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [MessageMutationResult](#type-messagemutationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [MESSAGE_NOT_ALLOWED](#error-message-not-allowed) (403)، [MESSAGE_DELETED](#error-message-deleted) (409)، [MESSAGE_EDIT_CONFLICT](#error-message-edit-conflict) (412) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.DeleteMessageAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `messageId` | route | `UUID` | مطابق route و scope مجاز |
| `expectedVersion` | query | `string` | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. |


نمونه درخواست مصنوعی:

```http
DELETE /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/messages/97215dcf-e9d1-5d83-9a4a-62af56aa58ef?expectedVersion=AAAAAAAAAAE%3D HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
  "sequence": 1,
  "editedAtUtc": null,
  "deletedAtUtc": "2026-11-01T09:00:00Z",
  "isDeleted": true,
  "version": "AAAAAAAAAAE="
}
```

<a id="operation-098"></a>
### 098. بازنشر مجاز پیام در مقصد هم‌scope

```http
POST /api/v1/conversations/{conversationId}/messages/{messageId}/forward
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.ForwardMessage` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | source قابل مشاهده و destination قابل ارسال در scope/org یکسان؛ ID جدید پایدار برای مقصد. |
| ورودی | [ForwardMessageCommand](#type-forwardmessagecommand) |
| خروجی موفق | `200` [MessageReceiptResult](#type-messagereceiptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [MESSAGE_FORWARD_NOT_ALLOWED](#error-message-forward-not-allowed) (403)، [MESSAGE_FORWARD_SCOPE_NOT_ALLOWED](#error-message-forward-scope-not-allowed) (403)، [MESSAGE_IDEMPOTENCY_CONFLICT](#error-message-idempotency-conflict) (409) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.ForwardMessageAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `messageId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/messages/97215dcf-e9d1-5d83-9a4a-62af56aa58ef/forward HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "destinationConversationId": "d53a8ee6-2b1e-58f6-ae75-bb6da16dc3e0",
  "clientMessageId": "270ca0fa-a60a-5cd0-94e4-fe59c99f8551"
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "clientMessageId": "270ca0fa-a60a-5cd0-94e4-fe59c99f8551",
  "sequence": 1,
  "kind": "Text",
  "sentAtUtc": "2026-11-01T09:00:00Z",
  "status": "Sent",
  "version": "AAAAAAAAAAE="
}
```

<a id="operation-099"></a>
### 099. ثبت یا تغییر واکنش خود

```http
POST /api/v1/conversations/{conversationId}/messages/{messageId}/reactions
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.AddReaction` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | enum معتبر؛ پیام موجود و غیرحذف؛ یک واکنش فعال برای خود کاربر. |
| ورودی | [AddReactionCommand](#type-addreactioncommand) |
| خروجی موفق | `200` [MessageReactionResult](#type-messagereactionresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [REACTION_NOT_ALLOWED](#error-reaction-not-allowed) (403)، [MESSAGE_DELETED](#error-message-deleted) (409) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.AddReactionAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `messageId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/messages/97215dcf-e9d1-5d83-9a4a-62af56aa58ef/reactions HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "reaction": "Like"
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
  "reaction": "Like",
  "createdAtUtc": "2026-11-01T09:00:00Z"
}
```

<a id="operation-100"></a>
### 100. حذف واکنش انتخاب‌شده خود

```http
DELETE /api/v1/conversations/{conversationId}/messages/{messageId}/reactions/{reaction}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.RemoveReaction` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | reaction route از enum؛ حذف واکنش خود؛ 204 بدون body. |
| ورودی | بدون بدنه |
| خروجی موفق | `204` بدون بدنه |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [REACTION_NOT_ALLOWED](#error-reaction-not-allowed) (403)، [CONVERSATION_NOT_FOUND](#error-conversation-not-found) (404) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.RemoveReactionAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `messageId` | route | `UUID` | مطابق route و scope مجاز |
| `reaction` | route | `MessageReactionType` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
DELETE /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/messages/97215dcf-e9d1-5d83-9a4a-62af56aa58ef/reactions/Like HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `204`:

```http
HTTP/1.1 204 No Content
```

<a id="operation-101"></a>
### 101. سنجاق‌کردن پیام طبق policy

```http
POST /api/v1/conversations/{conversationId}/pins/{messageId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.PinMessage` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | policy pin برحسب type و role؛ پیام موجود غیرحذف و conversation قابل تغییر. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ConversationPinResult](#type-conversationpinresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [PIN_NOT_ALLOWED](#error-pin-not-allowed) (403)، [MESSAGE_DELETED](#error-message-deleted) (409)، [MESSAGE_NOT_EDITABLE](#error-message-not-editable) (409) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.PinMessageAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `messageId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/pins/97215dcf-e9d1-5d83-9a4a-62af56aa58ef HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
  "pinnedByUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "pinnedAtUtc": "2026-11-01T09:00:00Z",
  "isPinned": true
}
```

<a id="operation-102"></a>
### 102. برداشتن سنجاق پیام

```http
DELETE /api/v1/conversations/{conversationId}/pins/{messageId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.UnpinMessage` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | همان policy pin؛ نتیجه isPinned=false. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ConversationPinResult](#type-conversationpinresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [PIN_NOT_ALLOWED](#error-pin-not-allowed) (403)، [MESSAGE_NOT_EDITABLE](#error-message-not-editable) (409) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.UnpinMessageAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `messageId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
DELETE /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/pins/97215dcf-e9d1-5d83-9a4a-62af56aa58ef HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
  "pinnedByUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "pinnedAtUtc": "2026-11-01T09:00:00Z",
  "isPinned": false
}
```

<a id="operation-103"></a>
### 103. فهرست رسانه‌ها و فایل‌های گفتگو

```http
GET /api/v1/conversations/{conversationId}/media
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.GetMedia` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | فیلتر kind رسانه و pagination history؛ دسترسی فایل و گفتگو. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ConversationMediaPageResult](#type-conversationmediapageresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CONVERSATION_NOT_FOUND](#error-conversation-not-found) (404)، [MESSAGE_KIND_INVALID](#error-message-kind-invalid) (400) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.GetConversationMediaAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `kind` | query | `MessageKind?` | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست |
| `beforeSequence` | query | `long?` | شماره پیام مرزی برای صفحه تاریخچه؛ cursor تغییرات نیست. |
| `limit` | query | `int?` | سقف تعداد نتیجه صفحه طبق قواعد همین عملیات. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/media?limit=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "items": [
    {
      "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
      "sequence": 1,
      "senderUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
      "sentAtUtc": "2026-11-01T09:00:00Z",
      "attachment": {
        "fileAssetId": "d3d55fe5-9f38-551b-9cbc-36d104ba44f8",
        "kind": "File",
        "fileName": "sample.pdf",
        "contentType": "application/pdf",
        "sizeBytes": 1024,
        "createdAtUtc": "2026-11-01T09:00:00Z"
      }
    }
  ],
  "nextBeforeSequence": null
}
```

<a id="operation-104"></a>
### 104. بازیابی تغییرات پایدار بعد از cursor

```http
GET /api/v1/conversations/{conversationId}/changes
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.GetChanges` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | cursor قابل decode همان گفتگو و نه جلوتر از وضع جاری؛ limit۱..۲۰۰. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ConversationChangePageResult](#type-conversationchangepageresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CHANGE_CURSOR_INVALID](#error-change-cursor-invalid) (400)، [CONVERSATION_NOT_FOUND](#error-conversation-not-found) (404) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.GetConversationChangesAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `afterCursor` | query | `string?` | cursor تغییرات همان گفتگو؛ قالب و scope سمت سرور بررسی می‌شود. |
| `limit` | query | `int?` | سقف تعداد نتیجه صفحه طبق قواعد همین عملیات. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/changes?limit=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "changes": [
    {
      "changeId": "c1becc56-7bc7-5214-9b8a-b5600f5a561e",
      "type": "MessageCreated",
      "resourceId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
      "resourceVersion": "AAAAAAAAAAE=",
      "cursor": "AQbDvmqVgGZYohsBH3-afmgAAAAAAAAAAQ",
      "occurredAtUtc": "2026-11-01T09:00:00Z",
      "actorUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
      "payloadVersion": 1
    }
  ],
  "nextCursor": "AQbDvmqVgGZYohsBH3-afmgAAAAAAAAAAQ",
  "hasMore": false
}
```

<a id="operation-105"></a>
### 105. پیش‌برد cursor خواندن خود

```http
POST /api/v1/conversations/{conversationId}/read
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.MarkRead` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | upToMessageSequence پیام موجود؛ cursor خود monotonic؛ read delivery را هم جلو می‌برد. |
| ورودی | [AdvanceConversationCursorCommand](#type-advanceconversationcursorcommand) |
| خروجی موفق | `200` [ConversationCursorStateResult](#type-conversationcursorstateresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [READ_CURSOR_INVALID](#error-read-cursor-invalid) (400)، [CONVERSATION_NOT_FOUND](#error-conversation-not-found) (404) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.MarkConversationReadAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/read HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "upToMessageSequence": 1
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "lastDeliveredMessageSequence": 1,
  "lastReadMessageSequence": 1,
  "unreadCount": 0,
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "version": "AAAAAAAAAAE="
}
```

<a id="operation-106"></a>
### 106. پیش‌برد cursor تحویل خود

```http
POST /api/v1/conversations/{conversationId}/delivered
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.AcknowledgeDelivery` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | upToMessageSequence موجود؛ فقط ack دریافت خود، نه همه recipients. |
| ورودی | [AdvanceConversationCursorCommand](#type-advanceconversationcursorcommand) |
| خروجی موفق | `200` [ConversationCursorStateResult](#type-conversationcursorstateresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [DELIVERY_CURSOR_INVALID](#error-delivery-cursor-invalid) (400)، [CONVERSATION_NOT_FOUND](#error-conversation-not-found) (404) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `messagingService.AcknowledgeConversationDeliveryAsync` |
| Application | [MessagingService.cs](../MakanApp.Application/Messaging/MessagingService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/delivered HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "upToMessageSequence": 1
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "lastDeliveredMessageSequence": 1,
  "lastReadMessageSequence": 0,
  "unreadCount": 1,
  "updatedAtUtc": "2026-11-01T09:00:00Z",
  "version": "AAAAAAAAAAE="
}
```

<a id="operation-107"></a>
### 107. جزئیات و اعضای گروه یا کانال

```http
GET /api/v1/conversations/{conversationId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.GetDetails` |
| احراز هویت و context | participant فعال Group/Channel؛ workspace/scope معتبر |
| قواعد شاخص | مسیر management برای Group/Channel؛ participant فعال و scope معتبر. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ManagedConversationResult](#type-managedconversationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CONVERSATION_MANAGEMENT_NOT_ALLOWED](#error-conversation-management-not-allowed) (403)، [CONVERSATION_NOT_FOUND](#error-conversation-not-found) (404) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.GetDetailsAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "type": "Group",
  "scope": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "title": "گروه نمونه",
  "description": null,
  "managementPolicy": "UserManaged",
  "status": "Active",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "archivedAtUtc": null,
  "participants": [
    {
      "participantId": "0307c9d2-b8ee-543c-8660-65c70b84bab7",
      "user": {
        "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
        "username": "sample.user",
        "displayName": "کاربر نمونه"
      },
      "role": "Owner",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    }
  ],
  "alreadyExisted": false
}
```

<a id="operation-108"></a>
### 108. افزودن عضو واجد شرایط

```http
POST /api/v1/conversations/{conversationId}/members
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.AddMember` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | Owner/Admin؛ مخاطب eligible؛ active duplicate به‌صورت کسب‌وکار کنترل می‌شود. |
| ورودی | [AddConversationMemberCommand](#type-addconversationmembercommand) |
| خروجی موفق | `200` [ManagedConversationResult](#type-managedconversationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [PARTICIPANT_ALREADY_ACTIVE](#error-participant-already-active) (409)، [CONVERSATION_MANAGEMENT_NOT_ALLOWED](#error-conversation-management-not-allowed) (403) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.AddMemberAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/members HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "userId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a"
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "type": "Group",
  "scope": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "title": "گروه نمونه",
  "description": null,
  "managementPolicy": "UserManaged",
  "status": "Active",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "archivedAtUtc": null,
  "participants": [
    {
      "participantId": "0307c9d2-b8ee-543c-8660-65c70b84bab7",
      "user": {
        "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
        "username": "sample.user",
        "displayName": "کاربر نمونه"
      },
      "role": "Owner",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    },
    {
      "participantId": "382320a3-1ff0-5866-841b-3e661c622cff",
      "user": {
        "userId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
        "username": "sample.other",
        "displayName": "مخاطب نمونه"
      },
      "role": "Member",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    }
  ],
  "alreadyExisted": false
}
```

<a id="operation-109"></a>
### 109. حذف عضو طبق نقش مدیریتی

```http
DELETE /api/v1/conversations/{conversationId}/members/{targetUserId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.RemoveMember` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | Owner غیرOwner را حذف می‌کند؛ Admin فقط Member؛ Owner قابل حذف نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ManagedConversationResult](#type-managedconversationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [LAST_OWNER_REQUIRED](#error-last-owner-required) (409)، [CONVERSATION_MANAGEMENT_NOT_ALLOWED](#error-conversation-management-not-allowed) (403)، [PARTICIPANT_NOT_FOUND](#error-participant-not-found) (404) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.RemoveMemberAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `targetUserId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
DELETE /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/members/d9ce59f5-ec1c-5115-91ab-3fd85f64431a HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "type": "Group",
  "scope": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "title": "گروه نمونه",
  "description": null,
  "managementPolicy": "UserManaged",
  "status": "Active",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "archivedAtUtc": null,
  "participants": [
    {
      "participantId": "0307c9d2-b8ee-543c-8660-65c70b84bab7",
      "user": {
        "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
        "username": "sample.user",
        "displayName": "کاربر نمونه"
      },
      "role": "Owner",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    }
  ],
  "alreadyExisted": false
}
```

<a id="operation-110"></a>
### 110. خروج از گروه یا کانال

```http
POST /api/v1/conversations/{conversationId}/leave
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.Leave` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | Owner باید ابتدا انتقال دهد؛ بقیه خروج و پایان participant. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ManagedConversationResult](#type-managedconversationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [LAST_OWNER_REQUIRED](#error-last-owner-required) (409)، [CONVERSATION_NOT_FOUND](#error-conversation-not-found) (404) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.LeaveAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/leave HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "type": "Group",
  "scope": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "title": "گروه نمونه",
  "description": null,
  "managementPolicy": "UserManaged",
  "status": "Active",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "archivedAtUtc": null,
  "participants": [
    {
      "participantId": "0307c9d2-b8ee-543c-8660-65c70b84bab7",
      "user": {
        "userId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
        "username": "sample.owner",
        "displayName": "مالک نمونه"
      },
      "role": "Owner",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    }
  ],
  "alreadyExisted": false
}
```

<a id="operation-111"></a>
### 111. تغییر نقش Admin/Member

```http
PATCH /api/v1/conversations/{conversationId}/members/{targetUserId}/role
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.ChangeMemberRole` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | فقط Owner؛ target غیرOwner؛ role فقط Admin/Member. |
| ورودی | [ChangeConversationMemberRoleCommand](#type-changeconversationmemberrolecommand) |
| خروجی موفق | `200` [ManagedConversationResult](#type-managedconversationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CONVERSATION_MANAGEMENT_NOT_ALLOWED](#error-conversation-management-not-allowed) (403)، [LAST_OWNER_REQUIRED](#error-last-owner-required) (409) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.ChangeMemberRoleAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `targetUserId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
PATCH /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/members/d9ce59f5-ec1c-5115-91ab-3fd85f64431a/role HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "role": "Admin"
}
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "type": "Group",
  "scope": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "title": "گروه نمونه",
  "description": null,
  "managementPolicy": "UserManaged",
  "status": "Active",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "archivedAtUtc": null,
  "participants": [
    {
      "participantId": "0307c9d2-b8ee-543c-8660-65c70b84bab7",
      "user": {
        "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
        "username": "sample.user",
        "displayName": "کاربر نمونه"
      },
      "role": "Owner",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    },
    {
      "participantId": "382320a3-1ff0-5866-841b-3e661c622cff",
      "user": {
        "userId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
        "username": "sample.other",
        "displayName": "مخاطب نمونه"
      },
      "role": "Admin",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    }
  ],
  "alreadyExisted": false
}
```

<a id="operation-112"></a>
### 112. درخواست انتقال مالکیت

```http
POST /api/v1/conversations/{conversationId}/ownership-transfers
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.StartOwnershipTransfer` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | فقط Owner؛ مقصد عضو فعال؛ انتقال Pending یکتا و قابل قبول. |
| ورودی | [StartOwnershipTransferCommand](#type-startownershiptransfercommand) |
| خروجی موفق | `201` [OwnershipTransferResult](#type-ownershiptransferresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [OWNERSHIP_TRANSFER_CONFLICT](#error-ownership-transfer-conflict) (409)، [LAST_OWNER_REQUIRED](#error-last-owner-required) (409) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.StartOwnershipTransferAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/ownership-transfers HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "targetUserId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a"
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "transferId": "1cec715c-bdf9-5b4a-8b51-ade73411a684",
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "fromUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "toUserId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
  "status": "Pending",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "acceptedAtUtc": null,
  "declinedAtUtc": null,
  "alreadyCompleted": false
}
```

<a id="operation-113"></a>
### 113. پذیرش انتقال توسط مقصد

```http
POST /api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/accept
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.AcceptOwnershipTransfer` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | فقط مقصد transfer معتبر؛ تغییر اتمیک Owner. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [OwnershipTransferResult](#type-ownershiptransferresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [OWNERSHIP_TRANSFER_CONFLICT](#error-ownership-transfer-conflict) (409)، [OWNERSHIP_TRANSFER_NOT_FOUND](#error-ownership-transfer-not-found) (404) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.AcceptOwnershipTransferAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `transferId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/ownership-transfers/1cec715c-bdf9-5b4a-8b51-ade73411a684/accept HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "transferId": "1cec715c-bdf9-5b4a-8b51-ade73411a684",
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "fromUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "toUserId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
  "status": "Accepted",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "acceptedAtUtc": "2026-11-01T09:00:00Z",
  "declinedAtUtc": null,
  "alreadyCompleted": false
}
```

<a id="operation-114"></a>
### 114. رد انتقال توسط مقصد

```http
POST /api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/decline
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.DeclineOwnershipTransfer` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | فقط مقصد؛ وضعیت انتقال معتبر؛ decline صریح. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [OwnershipTransferResult](#type-ownershiptransferresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [OWNERSHIP_TRANSFER_CONFLICT](#error-ownership-transfer-conflict) (409)، [OWNERSHIP_TRANSFER_NOT_FOUND](#error-ownership-transfer-not-found) (404) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.DeclineOwnershipTransferAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `transferId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/ownership-transfers/1cec715c-bdf9-5b4a-8b51-ade73411a684/decline HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "transferId": "1cec715c-bdf9-5b4a-8b51-ade73411a684",
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "fromUserId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
  "toUserId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
  "status": "Declined",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "acceptedAtUtc": null,
  "declinedAtUtc": "2026-11-01T09:00:00Z",
  "alreadyCompleted": false
}
```

<a id="operation-115"></a>
### 115. آرشیو گروه یا کانال

```http
POST /api/v1/conversations/{conversationId}/archive
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `ConversationsController.Archive` |
| احراز هویت و context | نشست معتبر؛ participant فعال و scope/سازمان معتبر؛ policy هر عملیات |
| قواعد شاخص | فقط Owner؛ حفظ تاریخچه و توقف mutationهای وابسته به Active. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ManagedConversationResult](#type-managedconversationresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [CONVERSATION_MANAGEMENT_NOT_ALLOWED](#error-conversation-management-not-allowed) (403)، [CONVERSATION_ARCHIVED](#error-conversation-archived) (409) |
| Controller | [ConversationsController.cs](../MakanApp.Api/Controllers/ConversationsController.cs) |
| متدهای service | `managementService.ArchiveAsync` |
| Application | [ConversationManagementService.cs](../MakanApp.Application/Messaging/ConversationManagementService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IConversationManagementStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/archive HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "type": "Group",
  "scope": "Organization",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "title": "گروه نمونه",
  "description": null,
  "managementPolicy": "UserManaged",
  "status": "Archived",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "archivedAtUtc": "2026-11-01T09:00:00Z",
  "participants": [
    {
      "participantId": "0307c9d2-b8ee-543c-8660-65c70b84bab7",
      "user": {
        "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
        "username": "sample.user",
        "displayName": "کاربر نمونه"
      },
      "role": "Owner",
      "status": "Active",
      "joinedAtUtc": "2026-11-01T09:00:00Z",
      "endedAtUtc": null
    }
  ],
  "alreadyExisted": false
}
```

<a id="operation-116"></a>
### 116. جست‌وجوی پیام و نام فایل در دامنه مجاز

```http
GET /api/v1/messages/search
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `MessagesController.Search` |
| احراز هویت و context | نشست معتبر؛ گفتگوهای مجاز در workspace جاری |
| قواعد شاخص | q نرمال غیرخالی تا۲۰۰؛ limit۱..۵۰؛ UTC مرتب؛ فقط scope و resources مجاز. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [MessageSearchPageResult](#type-messagesearchpageresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [SEARCH_QUERY_REQUIRED](#error-search-query-required) (400)، [SEARCH_QUERY_INVALID](#error-search-query-invalid) (400)، [SEARCH_CURSOR_INVALID](#error-search-cursor-invalid) (400) |
| Controller | [MessagesController.cs](../MakanApp.Api/Controllers/MessagesController.cs) |
| متدهای service | `safetyService.SearchMessagesAsync` |
| Application | [MessagingSafetyService.cs](../MakanApp.Application/Messaging/MessagingSafetyService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingSafetyStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `q` | query | `string?` | عبارت جست‌وجو؛ scope جاری و محدودیت متن اعمال می‌شود. |
| `conversationId` | query | `Guid?` | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد |
| `kind` | query | `MessageKind?` | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `cursor` | query | `string?` | نشانگر opaque؛ cursor جست‌وجو و Delta قابل تعویض نیستند |
| `limit` | query | `int?` | سقف تعداد نتیجه صفحه طبق قواعد همین عملیات. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/messages/search?q=%D9%86%D9%85%D9%88%D9%86%D9%87&limit=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "items": [
    {
      "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
      "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
      "conversationDisplayName": null,
      "sender": {
        "userId": "6bd1df05-a8b1-5c2d-b63f-1759b73247a5",
        "username": "sample.user",
        "displayName": "کاربر نمونه"
      },
      "snippet": "پیام نمونه",
      "kind": "Text",
      "sentAtUtc": "2026-11-01T09:00:00Z",
      "sequence": 1,
      "attachments": []
    }
  ],
  "totalCount": 1,
  "nextCursor": null
}
```

<a id="operation-117"></a>
### 117. مسدودسازی ارتباط مستقیم حساب

```http
POST /api/v1/messaging/blocks/{userId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `MessagingSafetyController.Block` |
| احراز هویت و context | نشست و context معتبر؛ Block/Report متعلق به خود حساب |
| قواعد شاخص | target موجود و غیرخود؛ Block فعال تکراری همان نتیجه؛ منع direct دوطرفه. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [UserBlockResult](#type-userblockresult)؛ `201` [UserBlockResult](#type-userblockresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [USER_BLOCK_NOT_ALLOWED](#error-user-block-not-allowed) (403) |
| Controller | [MessagingSafetyController.cs](../MakanApp.Api/Controllers/MessagingSafetyController.cs) |
| متدهای service | `safetyService.BlockUserAsync` |
| Application | [MessagingSafetyService.cs](../MakanApp.Application/Messaging/MessagingSafetyService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingSafetyStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `userId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/messaging/blocks/6bd1df05-a8b1-5c2d-b63f-1759b73247a5 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "blockId": "d7348d76-3312-5627-8dcc-69cc5b632570",
  "blockedUser": {
    "userId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
    "username": "sample.other",
    "displayName": "مخاطب نمونه"
  },
  "status": "Active",
  "createdAtUtc": "2026-11-01T09:00:00Z",
  "alreadyExisted": false,
  "version": "AAAAAAAAAAE="
}
```

<a id="operation-118"></a>
### 118. پایان مسدودی ایجادشده توسط خود

```http
DELETE /api/v1/messaging/blocks/{userId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `MessagingSafetyController.Unblock` |
| احراز هویت و context | نشست و context معتبر؛ Block/Report متعلق به خود حساب |
| قواعد شاخص | رفع Block ایجادشده توسط خود؛ بی‌اثر روی Block طرف مقابل؛ 204. |
| ورودی | بدون بدنه |
| خروجی موفق | `204` بدون بدنه |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [USER_BLOCK_NOT_ALLOWED](#error-user-block-not-allowed) (403) |
| Controller | [MessagingSafetyController.cs](../MakanApp.Api/Controllers/MessagingSafetyController.cs) |
| متدهای service | `safetyService.UnblockUserAsync` |
| Application | [MessagingSafetyService.cs](../MakanApp.Application/Messaging/MessagingSafetyService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingSafetyStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `userId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
DELETE /api/v1/messaging/blocks/6bd1df05-a8b1-5c2d-b63f-1759b73247a5 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `204`:

```http
HTTP/1.1 204 No Content
```

<a id="operation-119"></a>
### 119. فهرست مسدودی‌های فعال خود

```http
GET /api/v1/messaging/blocks
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `MessagingSafetyController.GetBlocks` |
| احراز هویت و context | نشست و context معتبر؛ Block/Report متعلق به خود حساب |
| قواعد شاخص | مسدودی‌های فعال ایجادشده توسط حساب جاری؛ endpoint moderation عمومی نیست. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [IReadOnlyCollection&lt;BlockedUserResult&gt;](#type-blockeduserresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401) |
| Controller | [MessagingSafetyController.cs](../MakanApp.Api/Controllers/MessagingSafetyController.cs) |
| متدهای service | `safetyService.GetBlockedUsersAsync` |
| Application | [MessagingSafetyService.cs](../MakanApp.Application/Messaging/MessagingSafetyService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingSafetyStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/messaging/blocks HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
[
  {
    "blockId": "d7348d76-3312-5627-8dcc-69cc5b632570",
    "user": {
      "userId": "d9ce59f5-ec1c-5115-91ab-3fd85f64431a",
      "username": "sample.other",
      "displayName": "مخاطب نمونه"
    },
    "createdAtUtc": "2026-11-01T09:00:00Z",
    "version": "AAAAAAAAAAE="
  }
]
```

<a id="operation-120"></a>
### 120. رسید گزارش متعلق به خود

```http
GET /api/v1/messaging/reports/{reportId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `MessagingSafetyController.GetReport` |
| احراز هویت و context | نشست و context معتبر؛ Block/Report متعلق به خود حساب |
| قواعد شاخص | receipt فقط برای reporter جاری؛ سایر کاربران وجود گزارش را نمی‌بینند. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [AbuseReportReceiptResult](#type-abusereportreceiptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [REPORT_MESSAGE_NOT_FOUND](#error-report-message-not-found) (404) |
| Controller | [MessagingSafetyController.cs](../MakanApp.Api/Controllers/MessagingSafetyController.cs) |
| متدهای service | `safetyService.GetMyReportAsync` |
| Application | [MessagingSafetyService.cs](../MakanApp.Application/Messaging/MessagingSafetyService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingSafetyStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `reportId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/messaging/reports/6a7f2d59-9ad0-523b-86e1-7eed5c5d8fdb HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "reportId": "6a7f2d59-9ad0-523b-86e1-7eed5c5d8fdb",
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
  "reason": "Harassment",
  "status": "Submitted",
  "createdAtUtc": "2026-11-01T09:00:00Z"
}
```

<a id="operation-121"></a>
### 121. ثبت گزارش محرمانه پیام

```http
POST /api/v1/conversations/{conversationId}/messages/{messageId}/reports
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Messaging`؛ `MessageReportsController.Report` |
| احراز هویت و context | نشست معتبر؛ پیام قابل گزارش و مجوز خواندن |
| قواعد شاخص | clientReportId ثابت، enum reason معتبر، description تا۲۰۰۰؛ پیام قابل گزارش. |
| ورودی | [ReportMessageCommand](#type-reportmessagecommand) |
| خروجی موفق | `201` [AbuseReportReceiptResult](#type-abusereportreceiptresult) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [REPORT_NOT_ALLOWED](#error-report-not-allowed) (403)، [REPORT_MESSAGE_NOT_FOUND](#error-report-message-not-found) (404)، [REPORT_IDEMPOTENCY_CONFLICT](#error-report-idempotency-conflict) (409) |
| Controller | [MessageReportsController.cs](../MakanApp.Api/Controllers/MessageReportsController.cs) |
| متدهای service | `safetyService.ReportMessageAsync` |
| Application | [MessagingSafetyService.cs](../MakanApp.Application/Messaging/MessagingSafetyService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IMessagingSafetyStore` | `EfMessagingStore` | [EfMessagingStore.cs](../MakanApp.Infrastructure/Messaging/EfMessagingStore.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `conversationId` | route | `UUID` | مطابق route و scope مجاز |
| `messageId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
POST /api/v1/conversations/6abec306-8095-5866-a21b-011f7f9a7e68/messages/97215dcf-e9d1-5d83-9a4a-62af56aa58ef/reports HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "clientReportId": "0fbfb4f4-8e79-5b0f-926e-d0d81bf0ee9b",
  "reason": "Harassment",
  "description": null
}
```

نمونه پاسخ موفق؛ status `201`:

```json
{
  "reportId": "6a7f2d59-9ad0-523b-86e1-7eed5c5d8fdb",
  "conversationId": "6abec306-8095-5866-a21b-011f7f9a7e68",
  "messageId": "97215dcf-e9d1-5d83-9a4a-62af56aa58ef",
  "reason": "Harassment",
  "status": "Submitted",
  "createdAtUtc": "2026-11-01T09:00:00Z"
}
```

<a id="operation-122"></a>
### 122. داشبورد دانش‌آموز

```http
GET /api/v1/dashboards/student
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `DashboardsController.GetStudent` |
| احراز هویت و context | Organization / Student؛ رابطه جاری سمت سرور |
| قواعد شاخص | هیچ organizationId/role/learnerId از کلاینت دریافت نمی‌شود. dataAsOfUtc زمان شروع خواندن است، نه snapshot تراکنشی. نمره و feedback برای Student فقط پس از انتشار؛ Teacher محدود به کلاس منتسب و Manager محدود به سازمان است. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [StudentDashboardDto](#type-studentdashboarddto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403) |
| Controller | [DashboardsController.cs](../MakanApp.Api/Controllers/DashboardsController.cs) |
| متدهای service | `dashboardService.GetStudentAsync` |
| Application | [DashboardService.cs](../MakanApp.Application/Reports/Dashboards/DashboardService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDashboardReadService` | `EfDashboardReadService` | [EfDashboardReadService.cs](../MakanApp.Infrastructure/Reports/Dashboards/EfDashboardReadService.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/dashboards/student HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "audience": "Student",
  "nextSession": null,
  "nearestAssignmentDeadline": null,
  "recentReleasedFeedback": [],
  "upcomingExams": [],
  "attendanceSummary": {
    "eligibleSessionCount": 0,
    "recordedCount": 0,
    "presentCount": 0,
    "absentCount": 0,
    "lateCount": 0,
    "excusedCount": 0,
    "notRecordedCount": 0
  },
  "pendingAssignmentCount": 0
}
```

<a id="operation-123"></a>
### 123. داشبورد والد

```http
GET /api/v1/dashboards/parent
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `DashboardsController.GetParent` |
| احراز هویت و context | Organization / Parent؛ رابطه جاری سمت سرور |
| قواعد شاخص | هیچ organizationId/role/learnerId از کلاینت دریافت نمی‌شود. dataAsOfUtc زمان شروع خواندن است، نه snapshot تراکنشی. بدون فرزند انتخاب‌شده: 200 با selectedLearner=null و لیست خالی؛ رابطه منقضی/revoked خطا می‌دهد. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ParentDashboardDto](#type-parentdashboarddto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403) |
| Controller | [DashboardsController.cs](../MakanApp.Api/Controllers/DashboardsController.cs) |
| متدهای service | `dashboardService.GetParentAsync` |
| Application | [DashboardService.cs](../MakanApp.Application/Reports/Dashboards/DashboardService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDashboardReadService` | `EfDashboardReadService` | [EfDashboardReadService.cs](../MakanApp.Infrastructure/Reports/Dashboards/EfDashboardReadService.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/dashboards/parent HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "audience": "Parent",
  "selectedLearner": null,
  "upcomingSessions": [],
  "assignments": [],
  "releasedAssignmentResults": [],
  "exams": [],
  "attendanceSummary": {
    "eligibleSessionCount": 0,
    "recordedCount": 0,
    "presentCount": 0,
    "absentCount": 0,
    "lateCount": 0,
    "excusedCount": 0,
    "notRecordedCount": 0
  },
  "attentionItemCount": 0
}
```

<a id="operation-124"></a>
### 124. داشبورد معلم

```http
GET /api/v1/dashboards/teacher
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `DashboardsController.GetTeacher` |
| احراز هویت و context | Organization / Teacher؛ رابطه جاری سمت سرور |
| قواعد شاخص | هیچ organizationId/role/learnerId از کلاینت دریافت نمی‌شود. dataAsOfUtc زمان شروع خواندن است، نه snapshot تراکنشی. نمره و feedback برای Student فقط پس از انتشار؛ Teacher محدود به کلاس منتسب و Manager محدود به سازمان است. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [TeacherDashboardDto](#type-teacherdashboarddto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403) |
| Controller | [DashboardsController.cs](../MakanApp.Api/Controllers/DashboardsController.cs) |
| متدهای service | `dashboardService.GetTeacherAsync` |
| Application | [DashboardService.cs](../MakanApp.Application/Reports/Dashboards/DashboardService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDashboardReadService` | `EfDashboardReadService` | [EfDashboardReadService.cs](../MakanApp.Infrastructure/Reports/Dashboards/EfDashboardReadService.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/dashboards/teacher HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "audience": "Teacher",
  "assignedClasses": [],
  "upcomingSessions": [],
  "attendancePending": [],
  "assignmentGradingQueue": {
    "awaitingEvaluationCount": 0,
    "draftEvaluationCount": 0,
    "totalPendingCount": 0
  },
  "examGradingQueue": {
    "awaitingGradingCount": 0,
    "manualReviewCount": 0,
    "readyForReleaseCount": 0,
    "totalPendingCount": 0
  }
}
```

<a id="operation-125"></a>
### 125. داشبورد مدیر

```http
GET /api/v1/dashboards/manager
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `DashboardsController.GetManager` |
| احراز هویت و context | Organization / Manager؛ رابطه جاری سمت سرور |
| قواعد شاخص | هیچ organizationId/role/learnerId از کلاینت دریافت نمی‌شود. dataAsOfUtc زمان شروع خواندن است، نه snapshot تراکنشی. نمره و feedback برای Student فقط پس از انتشار؛ Teacher محدود به کلاس منتسب و Manager محدود به سازمان است. |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ManagerDashboardDto](#type-managerdashboarddto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403) |
| Controller | [DashboardsController.cs](../MakanApp.Api/Controllers/DashboardsController.cs) |
| متدهای service | `dashboardService.GetManagerAsync` |
| Application | [DashboardService.cs](../MakanApp.Application/Reports/Dashboards/DashboardService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDashboardReadService` | `EfDashboardReadService` | [EfDashboardReadService.cs](../MakanApp.Infrastructure/Reports/Dashboards/EfDashboardReadService.cs) |


نمونه درخواست مصنوعی:

```http
GET /api/v1/dashboards/manager HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "organizationId": "6b68cf73-b82d-5e45-b91b-d4e823f00822",
  "audience": "Manager",
  "operationalCounts": {
    "activeClassCount": 0,
    "enrolledLearnerCount": 0,
    "teacherCount": 0
  },
  "onboarding": {
    "organizationBasicDataAvailable": false,
    "hasAcademicPeriod": false,
    "hasCourse": false,
    "hasTeacher": false,
    "hasLearner": false,
    "hasClass": false
  },
  "upcomingSessions": [],
  "sessionsMissingMeetingLink": [],
  "attendancePending": [],
  "pendingInvitationCount": 0,
  "assignmentGradingQueue": {
    "awaitingEvaluationCount": 0,
    "draftEvaluationCount": 0,
    "totalPendingCount": 0
  },
  "examGradingQueue": {
    "awaitingGradingCount": 0,
    "manualReviewCount": 0,
    "readyForReleaseCount": 0,
    "totalPendingCount": 0
  }
}
```

<a id="operation-126"></a>
### 126. گزارش حضور دانش‌آموز

```http
GET /api/v1/reports/student/attendance
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetStudentAttendance` |
| احراز هویت و context | Organization / Student |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. نمره و learnerFeedback فقط پس از release؛ Exam هیچ prompt/answer/key ندارد. summary روی کل داده فیلترشده است؛ NotRecorded با Absent تفاوت دارد.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [AttendanceReportPage&lt;StudentAttendanceReportItem&gt;](#type-attendancereportpage-studentattendancereportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetStudentAttendanceAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/student/attendance?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "summary": {
    "eligibleCount": 0,
    "recordedCount": 0,
    "presentCount": 0,
    "absentCount": 0,
    "lateCount": 0,
    "excusedCount": 0,
    "notRecordedCount": 0
  },
  "items": []
}
```

<a id="operation-127"></a>
### 127. گزارش تکلیف دانش‌آموز

```http
GET /api/v1/reports/student/assignments
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetStudentAssignments` |
| احراز هویت و context | Organization / Student |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. نمره و learnerFeedback فقط پس از release؛ Exam هیچ prompt/answer/key ندارد.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;StudentAssignmentReportItem&gt;](#type-reportpage-studentassignmentreportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetStudentAssignmentsAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/student/assignments?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="operation-128"></a>
### 128. گزارش آزمون دانش‌آموز

```http
GET /api/v1/reports/student/exams
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetStudentExams` |
| احراز هویت و context | Organization / Student |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. نمره و learnerFeedback فقط پس از release؛ Exam هیچ prompt/answer/key ندارد.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;StudentExamReportItem&gt;](#type-reportpage-studentexamreportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetStudentExamsAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/student/exams?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="operation-129"></a>
### 129. نمره‌های منتشرشده دانش‌آموز

```http
GET /api/v1/reports/student/grades
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetStudentGrades` |
| احراز هویت و context | Organization / Student |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. نمره و learnerFeedback فقط پس از release؛ Exam هیچ prompt/answer/key ندارد.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;StudentReleasedGradeReportItem&gt;](#type-reportpage-studentreleasedgradereportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetStudentGradesAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/student/grades?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="operation-130"></a>
### 130. گزارش حضور فرزند

```http
GET /api/v1/reports/parent/attendance
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetParentAttendance` |
| احراز هویت و context | Organization / Parent؛ فرزند انتخاب‌شده با رابطه معتبر |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. نبود child انتخاب‌شده 404 CHILD_CONTEXT_NOT_FOUND؛ feedback فقط guardian-visible پس از release. summary روی کل داده فیلترشده است؛ NotRecorded با Absent تفاوت دارد.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [AttendanceReportPage&lt;ParentAttendanceReportItem&gt;](#type-attendancereportpage-parentattendancereportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400)، [CHILD_CONTEXT_NOT_FOUND](#error-child-context-not-found) (404)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetParentAttendanceAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/parent/attendance?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "summary": {
    "eligibleCount": 0,
    "recordedCount": 0,
    "presentCount": 0,
    "absentCount": 0,
    "lateCount": 0,
    "excusedCount": 0,
    "notRecordedCount": 0
  },
  "items": []
}
```

<a id="operation-131"></a>
### 131. گزارش تکلیف فرزند

```http
GET /api/v1/reports/parent/assignments
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetParentAssignments` |
| احراز هویت و context | Organization / Parent؛ فرزند انتخاب‌شده با رابطه معتبر |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. نبود child انتخاب‌شده 404 CHILD_CONTEXT_NOT_FOUND؛ feedback فقط guardian-visible پس از release.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;ParentAssignmentReportItem&gt;](#type-reportpage-parentassignmentreportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400)، [CHILD_CONTEXT_NOT_FOUND](#error-child-context-not-found) (404)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetParentAssignmentsAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/parent/assignments?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="operation-132"></a>
### 132. گزارش آزمون فرزند

```http
GET /api/v1/reports/parent/exams
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetParentExams` |
| احراز هویت و context | Organization / Parent؛ فرزند انتخاب‌شده با رابطه معتبر |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. نبود child انتخاب‌شده 404 CHILD_CONTEXT_NOT_FOUND؛ feedback فقط guardian-visible پس از release.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;ParentExamReportItem&gt;](#type-reportpage-parentexamreportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400)، [CHILD_CONTEXT_NOT_FOUND](#error-child-context-not-found) (404)، [CHILD_CONTEXT_NOT_ALLOWED](#error-child-context-not-allowed) (403) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetParentExamsAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/parent/exams?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="operation-133"></a>
### 133. گزارش حضور کلاس معلم

```http
GET /api/v1/reports/teacher/classes/{classId}/attendance
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetTeacherAttendance` |
| احراز هویت و context | Organization / Teacher؛ انتساب فعال همان کلاس |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. classId مسیر authoritative selector است؛ ClassId در query این مسیرها برای تغییر کلاس مصرف نمی‌شود. کلاس/learner خارج دسترسی 404. summary روی کل داده فیلترشده است؛ NotRecorded با Absent تفاوت دارد.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [AttendanceReportPage&lt;TeacherAttendanceReportItem&gt;](#type-attendancereportpage-teacherattendancereportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetTeacherAttendanceAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/teacher/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/attendance?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "summary": {
    "eligibleCount": 0,
    "recordedCount": 0,
    "presentCount": 0,
    "absentCount": 0,
    "lateCount": 0,
    "excusedCount": 0,
    "notRecordedCount": 0
  },
  "items": []
}
```

<a id="operation-134"></a>
### 134. گزارش تکالیف کلاس معلم

```http
GET /api/v1/reports/teacher/classes/{classId}/assignments
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetTeacherAssignments` |
| احراز هویت و context | Organization / Teacher؛ انتساب فعال همان کلاس |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. classId مسیر authoritative selector است؛ ClassId در query این مسیرها برای تغییر کلاس مصرف نمی‌شود. کلاس/learner خارج دسترسی 404.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;TeacherAssignmentReportItem&gt;](#type-reportpage-teacherassignmentreportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetTeacherAssignmentsAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/teacher/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/assignments?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="operation-135"></a>
### 135. گزارش آزمون‌های کلاس معلم

```http
GET /api/v1/reports/teacher/classes/{classId}/exams
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetTeacherExams` |
| احراز هویت و context | Organization / Teacher؛ انتساب فعال همان کلاس |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. classId مسیر authoritative selector است؛ ClassId در query این مسیرها برای تغییر کلاس مصرف نمی‌شود. کلاس/learner خارج دسترسی 404.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;TeacherExamReportItem&gt;](#type-reportpage-teacherexamreportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetTeacherExamsAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/teacher/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/exams?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="operation-136"></a>
### 136. خلاصه فراگیر کلاس معلم

```http
GET /api/v1/reports/teacher/classes/{classId}/learners/{learnerId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetTeacherLearner` |
| احراز هویت و context | Organization / Teacher؛ انتساب فعال همان کلاس |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. classId مسیر authoritative selector است؛ ClassId در query این مسیرها برای تغییر کلاس مصرف نمی‌شود. کلاس/learner خارج دسترسی 404.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [TeacherLearnerOverviewDto](#type-teacherlearneroverviewdto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetTeacherLearnerAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |
| `learnerId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/teacher/classes/c16b36b1-18ac-51ec-be7e-e518764ad372/learners/0c86fa1f-d0c7-59d3-a9de-8adeb0422293 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "enrollmentId": "1b1378f1-12be-5d66-81ab-b8896898c3fa",
  "learnerOrganizationPersonId": "ca792355-953b-56c9-8371-63d9a1122800",
  "learnerDisplayName": "نمونه",
  "eligibleSessionCount": 0,
  "presentCount": 0,
  "absentCount": 0,
  "lateCount": 0,
  "excusedCount": 0,
  "notRecordedCount": 0,
  "assignmentCount": 0,
  "submittedAssignmentCount": 0,
  "releasedAssignmentCount": 0,
  "examCount": 0,
  "finalizedExamAttemptCount": 0,
  "releasedExamCount": 0
}
```

<a id="operation-137"></a>
### 137. گزارش ثبت‌نام سازمان

```http
GET /api/v1/reports/manager/enrollments
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetManagerEnrollments` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;ManagerEnrollmentReportItem&gt;](#type-reportpage-managerenrollmentreportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetManagerEnrollmentsAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |
| `courseId` | query | `Guid?` | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست |
| `academicPeriodId` | query | `Guid?` | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند |
| `status` | query | `EnrollmentStatus?` | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/manager/enrollments?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="operation-138"></a>
### 138. گزارش تجمیعی حضور سازمان

```http
GET /api/v1/reports/manager/attendance
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetManagerAttendance` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14. summary روی کل داده فیلترشده است؛ NotRecorded با Absent تفاوت دارد.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;ManagerAttendanceReportItem&gt;](#type-reportpage-managerattendancereportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetManagerAttendanceAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/manager/attendance?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="operation-139"></a>
### 139. گزارش ارزیابی‌های سازمان

```http
GET /api/v1/reports/manager/assessments
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetManagerAssessments` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;ManagerAssessmentReportItem&gt;](#type-reportpage-managerassessmentreportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetManagerAssessmentsAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |
| `courseId` | query | `Guid?` | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست |
| `academicPeriodId` | query | `Guid?` | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند |
| `kind` | query | `DetailedReportAssessmentKind?` | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/manager/assessments?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="operation-140"></a>
### 140. عملکرد کلاس سازمان

```http
GET /api/v1/reports/manager/classes/{classId}
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetManagerClass` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. میانگین درصد Assignment و Exam جداست؛ بدون نتیجه منتشرشده مقدار null و count صفر است.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ManagerClassPerformanceDto](#type-managerclassperformancedto) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetManagerClassAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `classId` | route | `UUID` | مطابق route و scope مجاز |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/manager/classes/c16b36b1-18ac-51ec-be7e-e518764ad372 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "classId": "c16b36b1-18ac-51ec-be7e-e518764ad372",
  "classTitle": "کلاس نمونه",
  "courseTitle": "درس نمونه",
  "activeLearnerCount": 0,
  "assignmentReleasedResultCount": 0,
  "assignmentAveragePercentage": null,
  "examReleasedResultCount": 0,
  "examAveragePercentage": null
}
```

<a id="operation-141"></a>
### 141. بار کاری معلمان سازمان

```http
GET /api/v1/reports/manager/teacher-workload
```

| موضوع | قرارداد |
| --- | --- |
| حوزه و Controller/Action | `Reports`؛ `ReportsController.GetManagerTeacherWorkload` |
| احراز هویت و context | Organization / Manager |
| قواعد شاخص | نقش فعال باید دقیقاً متناظر مسیر باشد؛ شناسه‌ها selector و داده محدود به رابطه/سازمان است. pageNumber پیش‌فرض1؛ pageSize پیش‌فرض20 و بازه1..100. بازه fromUtc/toUtc باید UTC و مرتب باشد. sort فقط از allowlist همین گزارش؛ شرح کامل بخش14.  |
| ورودی | بدون بدنه |
| خروجی موفق | `200` [ReportPage&lt;ManagerTeacherWorkloadReportItem&gt;](#type-reportpage-managerteacherworkloadreportitem-) |
| خطاهای شاخص | [AUTH_REQUIRED](#error-auth-required) (401)، [WORKSPACE_NOT_ALLOWED](#error-workspace-not-allowed) (403)، [REPORT_RESOURCE_NOT_FOUND](#error-report-resource-not-found) (404)، [REPORT_PAGINATION_INVALID](#error-report-pagination-invalid) (400)، [REPORT_DATE_RANGE_INVALID](#error-report-date-range-invalid) (400)، [REPORT_SORT_INVALID](#error-report-sort-invalid) (400) |
| Controller | [ReportsController.cs](../MakanApp.Api/Controllers/ReportsController.cs) |
| متدهای service | `reportService.GetManagerTeacherWorkloadAsync` |
| Application | [DetailedReportService.cs](../MakanApp.Application/Reports/Detailed/DetailedReportService.cs) |


| port سرویس | implementation ثبت‌شده | source |
| --- | --- | --- |
| `IAccessContextResolver` | `GuardianAccessContextResolver` | [GuardianAccessContextResolver.cs](../MakanApp.Application/Guardian/GuardianAccessContextResolver.cs) |
| `IDetailedReportReadService` | `EfDetailedReportReadService` | [EfDetailedReportReadService.cs](../MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService.cs) |


| نام | محل | نوع | ملاحظه |
| --- | --- | --- | --- |
| `pageNumber` | query | `int` | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. |
| `pageSize` | query | `int` | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. |
| `fromUtc` | query | `DateTime?` | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. |
| `toUtc` | query | `DateTime?` | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. |
| `classId` | query | `Guid?` | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس |
| `sort` | query | `string?` | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. |
| `courseId` | query | `Guid?` | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست |
| `academicPeriodId` | query | `Guid?` | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند |


نمونه درخواست مصنوعی:

```http
GET /api/v1/reports/manager/teacher-workload?pageNumber=1&pageSize=20 HTTP/1.1
Host: localhost:7063
Accept: application/json
Authorization: Bearer <accessToken>
```

نمونه پاسخ موفق؛ status `200`:

```json
{
  "dataAsOfUtc": "2026-11-01T09:00:00Z",
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "items": []
}
```

<a id="technical-contracts"></a>
## SignalR و مسیرهای فنی

این مسیرها خارج شمارش ۱۴۱ عملیات Controller هستند:

| مسیر/عملیات | قرارداد |
|---|---|
| `GET /health` | عمومی، liveness؛ وضعیت SQL/SMS/storage را probe نمی‌کند |
| `GET /openapi/v1.json` | OpenAPI عمومی همه محیط‌ها؛ از همین host خوانده می‌شود |
| `/swagger` | Swagger UI عمومی؛ security scheme Bearer در OpenAPI جاری ثبت نشده است |
| `/hubs/messaging` | Hub دارای Authorize؛ فقط Bearer header طبق SessionAuthenticationHandler |
| `SubscribeConversation(conversationId)` | بررسی مجوز جاری گفتگو پیش از عضویت connection در audience مربوط |
| `UnsubscribeConversation(conversationId)` | خروج connection از subscription؛ membership دیتابیس را پایان نمی‌دهد |
| `ConversationChanged` | notification با DTO MessagingRealtimeNotification؛ محتوا با Delta/HTTP بازیابی شود |

Hub Controller نیست؛ negotiate و transport از SignalR می‌آیند. WebSocket/SSE مرورگر ممکن است token را در query بفرستند و handler فعلی آن را نمی‌خواند. برای browser cross-origin، نمونه LongPolling دارای accessTokenFactory و withCredentials:false در قرارداد فرانت بررسی شود. دریافت event تضمین exactly-once ندارد؛ قطع اتصال و notification تکراری را با cursor/نسخه و Delta مدیریت کنید.

منابع: [MessagingHub](../MakanApp.Api/Realtime/MessagingHub.cs)، [SignalRMessagingRealtimeNotifier](../MakanApp.Api/Realtime/SignalRMessagingRealtimeNotifier.cs)، [راهنمای همگام‌سازی](backend-api-guide.md#messaging).

<a id="contracts"></a>
## قراردادهای ورودی و خروجی

required از schema OpenAPI و nullable از type DTO گزارش می‌شود. مدل query پارامتر URL است؛ multipart و Hub قراردادهای غیر JSON HTTP معمول‌اند. معنی/قاعده فیلد در کنار نوع آمده است؛ قواعد ترکیبی هر عملیات در کارت و service آن قرار دارد.

<a id="type-abusereportreceiptresult"></a>
### AbuseReportReceiptResult

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `reportId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه رسید گزارش خود؛ endpoint عمومی moderation وجود ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `conversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `messageId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پایدار پیام سرور؛ merge تاریخچه با این ID و clientMessageId محلی خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `reason` | [AbuseReportReason](#type-abusereportreason)<br>null: خیر؛ required در schema: بله | دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [AbuseReportStatus](#type-abusereportstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-academicperiodresult"></a>
### AcademicPeriodResult

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `startDate` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ آموزشی بدون timezone؛ قالب YYYY-MM-DD و پایان قبل از شروع نباشد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endDate` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ آموزشی بدون timezone؛ قالب YYYY-MM-DD و پایان قبل از شروع نباشد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [AcademicPeriodStatus](#type-academicperiodstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-acceptinvitationresult"></a>
### AcceptInvitationResult

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `invitationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه دعوت و حساب دعوت‌کننده؛ فقط مخاطب حق accept/decline دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationName` | `string`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `membershipId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه عضویت حساب در سازمان؛ از WorkspaceResult معتبر بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `roleAssignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه انتساب نقش به عضویت پس از پذیرش دعوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `role` | [OrganizationRole](#type-organizationrole)<br>null: خیر؛ required در schema: بله | نقش در context مربوط؛ OrganizationRole با ConversationParticipantRole متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `alreadyAccepted` | `bool`<br>null: خیر؛ required در schema: بله | علامت reuse عملیات موجود؛ از HTTP status به‌تنهایی استنباط نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-accesscontext"></a>
### AccessContext

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `actorId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه حساب actor؛ سرور از نشست معتبر تعیین می‌کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `userId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه حساب actor؛ سرور از نشست معتبر تعیین می‌کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sessionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نشست احرازشده؛ با شناسه جلسه آموزشی route اشتباه نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `workspaceType` | [WorkspaceType](#type-workspacetype)<br>null: خیر؛ required در schema: بله | Personal یا Organization؛ انتخاب سمت سرور ثبت می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `membershipId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه عضویت حساب در سازمان؛ از WorkspaceResult معتبر بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `activeRole` | [OrganizationRole?](#type-organizationrole)<br>null: بله؛ required در schema: بله | نقش در context مربوط؛ OrganizationRole با ConversationParticipantRole متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `subjectOrganizationPersonId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-addconversationmembercommand"></a>
### AddConversationMemberCommand

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `userId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه حساب actor؛ سرور از نشست معتبر تعیین می‌کند UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |


<a id="type-addexamquestioncommand"></a>
### AddExamQuestionCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `order` | `int`<br>null: خیر؛ required در schema: بله | ترتیب مثبت؛ displayOrder تلاش ترتیب frozen است اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `type` | [ExamQuestionType](#type-examquestiontype)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |
| `prompt` | `string`<br>null: خیر؛ required در schema: بله | متن سؤال غیرخالی تا۲۰۰۰۰ متن سؤال غیرخالی تا۲۰۰۰۰ |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست حداکثر دو رقم اعشار؛ سقف domain 9999999.99. نمره ارزیابی ۰..بارم؛ بارم طراحی >۰. |
| `options` | [IReadOnlyCollection&lt;ExamQuestionOptionCommand&gt;?](#type-examquestionoptioncommand)<br>null: بله؛ required در schema: بله | آرایه سؤال/گزینه همین DTO؛ مدل editor کلید دارد، student-safe ندارد آرایه مطابق DTO فرزند؛ یکتایی، تعلق و حدود تعداد در قواعد حوزه. |
| `expectedVersionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-addreactioncommand"></a>
### AddReactionCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `reaction` | [MessageReactionType](#type-messagereactiontype)<br>null: خیر؛ required در schema: بله | واکنش enum و aggregate مجاز؛ یک واکنش فعال هر کاربر از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |


<a id="type-advanceconversationcursorcommand"></a>
### AdvanceConversationCursorCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `upToMessageSequence` | `long`<br>null: خیر؛ required در schema: بله | cursor participant خود؛ عدد monotonic و پیام موجود؛ وضعیت گیرنده دیگر نیست اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |


<a id="type-assignteachercommand"></a>
### AssignTeacherCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `teacherMembershipId` | `Guid`<br>null: خیر؛ required در schema: بله | عضویت فعال دارای Teacher در همان سازمان؛ نه UserId معلم UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |


<a id="type-assignmentgradingqueuesummary"></a>
### AssignmentGradingQueueSummary

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `awaitingEvaluationCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد پاسخ Submitted بدون ارزیابی در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `draftEvaluationCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد ارزیابی پیش‌نویس در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalPendingCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد کل backlog همین دسته در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-assignmentresult"></a>
### AssignmentResult

منبع: [AssignmentContracts.cs](../MakanApp.Application/Assessment/AssignmentContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [AssignmentStatus](#type-assignmentstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `updatedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان آخرین تغییر به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `publishedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `versionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `versionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `description` | `string`<br>null: خیر؛ required در schema: بله | توضیح؛ nullable و سقف طول بین assignment/exam/report/conversation متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `dueAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | مهلت UTC تکلیف؛ هنگام طراحی/انتشار در آینده و هنگام submit مبنای late policy خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `allowLateSubmission` | `bool`<br>null: خیر؛ required در schema: بله | اجازه تحویل پس از مهلت؛ isLate همچنان توسط سرور تعیین می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxAttempts` | `int`<br>null: خیر؛ required در schema: بله | حد مثبت تلاش مجاز؛ تعداد تلاش با retry یک عملیات متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `versionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-attachsubmissionfilecommand"></a>
### AttachSubmissionFileCommand

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `fileAssetId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه فایل Ready؛ بارگذاری موفق به معنی attach یا submit نیست UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |
| `expectedRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-attendanceentryresult"></a>
### AttendanceEntryResult

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `attendanceId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه حضور ثبت‌شده؛ در NotRecorded می‌تواند null باشد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `enrollmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه ثبت‌نام؛ با learnerOrganizationPersonId متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [AttendanceStatus](#type-attendancestatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recordedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان ثبت حضور به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recordedByMembershipId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه عضویت سازمانی ثبت‌کننده این رویداد؛ UserId نیست و سرور تعیین می‌کند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `rowVersion` | `string?`<br>null: بله؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-attendancereportpage-parentattendancereportitem-"></a>
### AttendanceReportPage&lt;ParentAttendanceReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `summary` | [AttendanceReportSummary](#type-attendancereportsummary)<br>null: خیر؛ required در schema: بله | خلاصه کل مجموعه فیلترشده گزارش attendance، مستقل از صفحه جاری. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;ParentAttendanceReportItem&gt;](#type-parentattendancereportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-attendancereportpage-studentattendancereportitem-"></a>
### AttendanceReportPage&lt;StudentAttendanceReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `summary` | [AttendanceReportSummary](#type-attendancereportsummary)<br>null: خیر؛ required در schema: بله | خلاصه کل مجموعه فیلترشده گزارش attendance، مستقل از صفحه جاری. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;StudentAttendanceReportItem&gt;](#type-studentattendancereportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-attendancereportpage-teacherattendancereportitem-"></a>
### AttendanceReportPage&lt;TeacherAttendanceReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `summary` | [AttendanceReportSummary](#type-attendancereportsummary)<br>null: خیر؛ required در schema: بله | خلاصه کل مجموعه فیلترشده گزارش attendance، مستقل از صفحه جاری. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;TeacherAttendanceReportItem&gt;](#type-teacherattendancereportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-attendancereportsummary"></a>
### AttendanceReportSummary

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `eligibleCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد ردیف واجد شرایط کل فیلتر در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recordedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد حضور رسمی ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `presentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد حاضر در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `absentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد غایب ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lateCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تأخیر ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `excusedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد غیبت موجه در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `notRecordedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد فاقد رکورد حضور رسمی در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-attendancerevisionresult"></a>
### AttendanceRevisionResult

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `previousStatus` | [AttendanceStatus](#type-attendancestatus)<br>null: خیر؛ required در schema: بله | وضعیت پیش/پس از اصلاح حضور؛ تاریخچه برای audit حفظ می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `newStatus` | [AttendanceStatus](#type-attendancestatus)<br>null: خیر؛ required در schema: بله | وضعیت پیش/پس از اصلاح حضور؛ تاریخچه برای audit حفظ می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `correctedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `correctedByMembershipId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه عضویت سازمانی ثبت‌کننده این رویداد؛ UserId نیست و سرور تعیین می‌کند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `reason` | `string`<br>null: خیر؛ required در schema: بله | دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-authorizedchildresult"></a>
### AuthorizedChildResult

منبع: [GuardianContracts.cs](../MakanApp.Application/Guardian/GuardianContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `displayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی؛ در CompleteProfile اختیاری و حداکثر ۲۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationName` | `string`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `relationStatus` | [GuardianRelationStatus](#type-guardianrelationstatus)<br>null: خیر؛ required در schema: بله | شناسه/وضعیت رابطه سرپرستی؛ فعال‌بودن همچنان در سرور کنترل می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-blockeduserresult"></a>
### BlockedUserResult

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `blockId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه رابطه Block خود؛ رفع Block طرف مقابل از این API ممکن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `user` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult)<br>null: خیر؛ required در schema: بله | هویت امن پیام‌رسان؛ فقط userId/username/displayName خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `version` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-changeconversationmemberrolecommand"></a>
### ChangeConversationMemberRoleCommand

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `role` | [ConversationParticipantRole](#type-conversationparticipantrole)<br>null: خیر؛ required در schema: بله | نقش در context مربوط؛ OrganizationRole با ConversationParticipantRole متفاوت است از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |


<a id="type-classresult"></a>
### ClassResult

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `academicPeriodId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `academicPeriodTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `courseId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `courseTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `capacity` | `int`<br>null: خیر؛ required در schema: بله | ظرفیت مثبت کلاس؛ ظرفیت آزاد در client authoritative نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [ClassStatus](#type-classstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-completeexamgradecommand"></a>
### CompleteExamGradeCommand

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `guardianVisibleFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد مخصوص نمای والد پس از انتشار؛ تا۱۰۰۰۰ اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `evaluatorPrivateNote` | `string?`<br>null: بله؛ required در schema: بله | یادداشت خصوصی ارزیاب تا۱۰۰۰۰؛ در DTO عمومی/والد نمایش داده نشود اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `expectedGradeRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-completeprofilecommand"></a>
### CompleteProfileCommand

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `firstName` | `string`<br>null: خیر؛ required در schema: بله | نام/نام خانوادگی؛ برای تکمیل profile غیرخالی، حداکثر ۱۰۰ پس از trim نام/نام خانوادگی؛ برای تکمیل profile غیرخالی، حداکثر ۱۰۰ پس از trim |
| `lastName` | `string`<br>null: خیر؛ required در schema: بله | نام/نام خانوادگی؛ برای تکمیل profile غیرخالی، حداکثر ۱۰۰ پس از trim نام/نام خانوادگی؛ برای تکمیل profile غیرخالی، حداکثر ۱۰۰ پس از trim |
| `displayName` | `string?`<br>null: بله؛ required در schema: بله | نام نمایشی؛ در CompleteProfile اختیاری و حداکثر ۲۰۰ نام نمایشی؛ در CompleteProfile اختیاری و حداکثر ۲۰۰ |
| `username` | `string`<br>null: خیر؛ required در schema: بله | شناسه عمومی؛ profile طول ۳..۳۲ با حروف لاتین/رقم/نقطه/underscore و یکتا شناسه عمومی؛ profile طول ۳..۳۲ با حروف لاتین/رقم/نقطه/underscore و یکتا |


<a id="type-conversationchangepageresult"></a>
### ConversationChangePageResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `changes` | [IReadOnlyCollection&lt;ConversationChangeResult&gt;](#type-conversationchangeresult)<br>null: خیر؛ required در schema: بله | رویدادهای پایدار Delta با شناسه یکتا برای deduplication خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `nextCursor` | `string`<br>null: خیر؛ required در schema: بله | نشانگر opaque؛ cursor جست‌وجو و Delta قابل تعویض نیستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `hasMore` | `bool`<br>null: خیر؛ required در schema: بله | ادامه صفحه‌بندی Delta؛ فقط پس از اعمال صفحه nextCursor پایدار شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-conversationchangeresult"></a>
### ConversationChangeResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `changeId` | `Guid`<br>null: خیر؛ required در schema: بله | رویدادهای پایدار Delta با شناسه یکتا برای deduplication خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `type` | [MessagingChangeType](#type-messagingchangetype)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `resourceId` | `Guid`<br>null: خیر؛ required در schema: بله | منبع تغییر و نسخه آن؛ payload رویداد متن کامل پیام نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `resourceVersion` | `string?`<br>null: بله؛ required در schema: بله | منبع تغییر و نسخه آن؛ payload رویداد متن کامل پیام نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `cursor` | `string`<br>null: خیر؛ required در schema: بله | نشانگر opaque؛ cursor جست‌وجو و Delta قابل تعویض نیستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `occurredAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `actorUserId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه actor رویداد/فرستنده؛ null actor در رویداد ممکن است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `payloadVersion` | `int`<br>null: خیر؛ required در schema: بله | نسخه شکل رویداد؛ نوع ناشناخته باید باعث refresh امن شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-conversationcursorstateresult"></a>
### ConversationCursorStateResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `conversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lastDeliveredMessageSequence` | `long`<br>null: خیر؛ required در schema: بله | cursor participant خود؛ عدد monotonic و پیام موجود؛ وضعیت گیرنده دیگر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lastReadMessageSequence` | `long`<br>null: خیر؛ required در schema: بله | cursor participant خود؛ عدد monotonic و پیام موجود؛ وضعیت گیرنده دیگر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `unreadCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد authoritative پیام حذف‌نشده دیگران بعد از read cursor خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `updatedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان آخرین تغییر به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `version` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-conversationmediaitemresult"></a>
### ConversationMediaItemResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `messageId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پایدار پیام سرور؛ merge تاریخچه با این ID و clientMessageId محلی خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sequence` | `long`<br>null: خیر؛ required در schema: بله | شماره پیام در گفتگو؛ beforeSequence انحصاری، null پایان تاریخچه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `senderUserId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه actor رویداد/فرستنده؛ null actor در رویداد ممکن است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sentAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ارسال ثبت‌شده به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attachment` | [MessageAttachmentResult](#type-messageattachmentresult)<br>null: خیر؛ required در schema: بله | پیوست‌های مجاز resource؛ download همچنان authorization مستقل دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-conversationmediapageresult"></a>
### ConversationMediaPageResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `items` | [IReadOnlyCollection&lt;ConversationMediaItemResult&gt;](#type-conversationmediaitemresult)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `nextBeforeSequence` | `long?`<br>null: بله؛ required در schema: بله | شماره پیام در گفتگو؛ beforeSequence انحصاری، null پایان تاریخچه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-conversationmessagepageresult"></a>
### ConversationMessagePageResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `messages` | [IReadOnlyCollection&lt;ConversationMessageResult&gt;](#type-conversationmessageresult)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `nextBeforeSequence` | `long?`<br>null: بله؛ required در schema: بله | شماره پیام در گفتگو؛ beforeSequence انحصاری، null پایان تاریخچه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-conversationmessageresult"></a>
### ConversationMessageResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `messageId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پایدار پیام سرور؛ merge تاریخچه با این ID و clientMessageId محلی خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `conversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `senderUserId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه actor رویداد/فرستنده؛ null actor در رویداد ممکن است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sender` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult)<br>null: خیر؛ required در schema: بله | هویت امن پیام‌رسان؛ فقط userId/username/displayName خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `clientMessageId` | `Guid`<br>null: خیر؛ required در schema: بله | UUID ساخته کلاینت و ثابت در retry ارسال/forward همان payload خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sequence` | `long`<br>null: خیر؛ required در schema: بله | شماره پیام در گفتگو؛ beforeSequence انحصاری، null پایان تاریخچه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `kind` | [MessageKind?](#type-messagekind)<br>null: بله؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `text` | `string?`<br>null: بله؛ required در schema: بله | متن پیام/گزینه/caption؛ حد و nullability متناسب نوع DTO در قواعد حوزه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sentAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ارسال ثبت‌شده به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `editedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `deletedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isEdited` | `bool`<br>null: خیر؛ required در schema: بله | پرچم نمایش پیام؛ deleted باید محتوای cached را از نمایش خارج کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isDeleted` | `bool`<br>null: خیر؛ required در schema: بله | پرچم نمایش پیام؛ deleted باید محتوای cached را از نمایش خارج کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `reply` | [MessageReplySummaryResult?](#type-messagereplysummaryresult)<br>null: بله؛ required در schema: بله | خلاصه یا شناسه پیام مرجع همان گفتگو؛ deleted محتوا را آشکار نمی‌کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isForwarded` | `bool`<br>null: خیر؛ required در schema: بله | پرچم نمایش پیام؛ deleted باید محتوای cached را از نمایش خارج کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `reactions` | [IReadOnlyCollection&lt;MessageReactionSummaryResult&gt;](#type-messagereactionsummaryresult)<br>null: خیر؛ required در schema: بله | واکنش enum و aggregate مجاز؛ یک واکنش فعال هر کاربر خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `mentions` | [IReadOnlyCollection&lt;SafeMessagingIdentityResult&gt;](#type-safemessagingidentityresult)<br>null: خیر؛ required در schema: بله | هویت‌های امن mention رسمی؛ @ در متن به‌تنهایی کافی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attachments` | [IReadOnlyCollection&lt;MessageAttachmentResult&gt;](#type-messageattachmentresult)<br>null: خیر؛ required در schema: بله | پیوست‌های مجاز resource؛ download همچنان authorization مستقل دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isPinned` | `bool`<br>null: خیر؛ required در schema: بله | پرچم نمایش پیام؛ deleted باید محتوای cached را از نمایش خارج کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `version` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-conversationparticipantresult"></a>
### ConversationParticipantResult

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `participantId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه عضویت در گفتگو، مستقل از Membership سازمان خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `user` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult)<br>null: خیر؛ required در schema: بله | هویت امن پیام‌رسان؛ فقط userId/username/displayName خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `role` | [ConversationParticipantRole](#type-conversationparticipantrole)<br>null: خیر؛ required در schema: بله | نقش در context مربوط؛ OrganizationRole با ConversationParticipantRole متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [ConversationParticipantStatus](#type-conversationparticipantstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `joinedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان پایان رابطه به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-conversationpinresult"></a>
### ConversationPinResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `conversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `messageId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پایدار پیام سرور؛ merge تاریخچه با این ID و clientMessageId محلی خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pinnedByUserId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه حساب انجام‌دهنده رویداد؛ سرور از actor معتبر ثبت می‌کند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pinnedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isPinned` | `bool`<br>null: خیر؛ required در schema: بله | پرچم نمایش پیام؛ deleted باید محتوای cached را از نمایش خارج کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-conversationsummaryresult"></a>
### ConversationSummaryResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `conversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `type` | [ConversationType](#type-conversationtype)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `scope` | [ConversationScope](#type-conversationscope)<br>null: خیر؛ required در schema: بله | Personal/Organization گفتگو؛ scope با role یکسان نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string?`<br>null: بله؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `otherParticipant` | [SafeMessagingIdentityResult?](#type-safemessagingidentityresult)<br>null: بله؛ required در schema: بله | هویت امن پیام‌رسان؛ فقط userId/username/displayName خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lastMessagePreview` | `string?`<br>null: بله؛ required در schema: بله | متن کوتاه شده؛ preview تا۱۲۰ و snippet تا۲۴۰؛ HTML معتبر فرض نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lastMessageAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lastMessageSequence` | `long?`<br>null: بله؛ required در schema: بله | شماره پیام در گفتگو؛ beforeSequence انحصاری، null پایان تاریخچه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lastDeliveredMessageSequence` | `long`<br>null: خیر؛ required در schema: بله | cursor participant خود؛ عدد monotonic و پیام موجود؛ وضعیت گیرنده دیگر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lastReadMessageSequence` | `long`<br>null: خیر؛ required در schema: بله | cursor participant خود؛ عدد monotonic و پیام موجود؛ وضعیت گیرنده دیگر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `unreadCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد authoritative پیام حذف‌نشده دیگران بعد از read cursor خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-correctattendancecommand"></a>
### CorrectAttendanceCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `status` | [AttendanceStatus](#type-attendancestatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |
| `correctionReason` | `string`<br>null: خیر؛ required در schema: بله | دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد |
| `expectedRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-correctreleasedevaluationcommand"></a>
### CorrectReleasedEvaluationCommand

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست حداکثر دو رقم اعشار؛ سقف domain 9999999.99. نمره ارزیابی ۰..بارم؛ بارم طراحی >۰. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `guardianVisibleFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد مخصوص نمای والد پس از انتشار؛ تا۱۰۰۰۰ اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `teacherPrivateNote` | `string?`<br>null: بله؛ required در schema: بله | یادداشت خصوصی ارزیاب تا۱۰۰۰۰؛ در DTO عمومی/والد نمایش داده نشود اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `correctionReason` | `string`<br>null: خیر؛ required در schema: بله | دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد |
| `expectedReleasedEvaluationRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-correctreleasedexamgradecommand"></a>
### CorrectReleasedExamGradeCommand

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `correctionReason` | `string`<br>null: خیر؛ required در schema: بله | دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد |
| `expectedReleasedGradeRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-courseresult"></a>
### CourseResult

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [CourseStatus](#type-coursestatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-createacademicperiodcommand"></a>
### CreateAcademicPeriodCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API غیرخالی پس از trim؛ تا۱۶۰ در گفتگو و تا۲۰۰ در آموزش/ارزیابی. |
| `startDate` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ آموزشی بدون timezone؛ قالب YYYY-MM-DD و پایان قبل از شروع نباشد YYYY-MM-DD؛ اعتبار بازه در domain. |
| `endDate` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ آموزشی بدون timezone؛ قالب YYYY-MM-DD و پایان قبل از شروع نباشد YYYY-MM-DD؛ اعتبار بازه در domain. |


<a id="type-createassignmentdraftcommand"></a>
### CreateAssignmentDraftCommand

منبع: [AssignmentContracts.cs](../MakanApp.Application/Assessment/AssignmentContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API غیرخالی پس از trim؛ تا۱۶۰ در گفتگو و تا۲۰۰ در آموزش/ارزیابی. |
| `description` | `string`<br>null: خیر؛ required در schema: بله | توضیح؛ nullable و سقف طول بین assignment/exam/report/conversation متفاوت است Assignment: غیرخالی تا۱۰۰۰۰؛ Exam: اختیاری تا۱۰۰۰۰؛ Conversation/Report: اختیاری تا۱۰۰۰. |
| `dueAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | مهلت UTC تکلیف؛ هنگام طراحی/انتشار در آینده و هنگام submit مبنای late policy ISO 8601 با Z؛ قواعد بازه/مهلت در کارت endpoint. |
| `allowLateSubmission` | `bool`<br>null: خیر؛ required در schema: بله | اجازه تحویل پس از مهلت؛ isLate همچنان توسط سرور تعیین می‌شود اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `maxAttempts` | `int`<br>null: خیر؛ required در schema: بله | حد مثبت تلاش مجاز؛ تعداد تلاش با retry یک عملیات متفاوت است اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار حداکثر دو رقم اعشار؛ سقف domain 9999999.99. نمره ارزیابی ۰..بارم؛ بارم طراحی >۰. |


<a id="type-createclasscommand"></a>
### CreateClassCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `academicPeriodId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |
| `courseId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API غیرخالی پس از trim؛ تا۱۶۰ در گفتگو و تا۲۰۰ در آموزش/ارزیابی. |
| `capacity` | `int`<br>null: خیر؛ required در schema: بله | ظرفیت مثبت کلاس؛ ظرفیت آزاد در client authoritative نیست اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `activateImmediately` | `bool`<br>null: خیر؛ required در schema: بله | true برای Active هنگام create؛ false به Draft می‌رود؛ API فعال‌سازی بعدی وجود ندارد اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |


<a id="type-createcoursecommand"></a>
### CreateCourseCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API غیرخالی پس از trim؛ تا۱۶۰ در گفتگو و تا۲۰۰ در آموزش/ارزیابی. |


<a id="type-createexamdraftcommand"></a>
### CreateExamDraftCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API غیرخالی پس از trim؛ تا۱۶۰ در گفتگو و تا۲۰۰ در آموزش/ارزیابی. |
| `description` | `string?`<br>null: بله؛ required در schema: بله | توضیح؛ nullable و سقف طول بین assignment/exam/report/conversation متفاوت است Assignment: غیرخالی تا۱۰۰۰۰؛ Exam: اختیاری تا۱۰۰۰۰؛ Conversation/Report: اختیاری تا۱۰۰۰. |
| `availableFromUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد ISO 8601 با Z؛ قواعد بازه/مهلت در کارت endpoint. |
| `availableUntilUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد ISO 8601 با Z؛ قواعد بازه/مهلت در کارت endpoint. |
| `durationMinutes` | `int`<br>null: خیر؛ required در schema: بله | مدت برحسب دقیقه؛ rule حداکثر۱۴۴۰، exam قانون خودش را دارد اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `maxAttempts` | `int`<br>null: خیر؛ required در schema: بله | حد مثبت تلاش مجاز؛ تعداد تلاش با retry یک عملیات متفاوت است اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار حداکثر دو رقم اعشار؛ سقف domain 9999999.99. نمره ارزیابی ۰..بارم؛ بارم طراحی >۰. |
| `randomizationPolicy` | [ExamRandomizationPolicy](#type-examrandomizationpolicy)<br>null: خیر؛ required در schema: بله | None یا QuestionOrder؛ ترتیب frozen از attempt خوانده شود از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |


<a id="type-createmanagedconversationcommand"></a>
### CreateManagedConversationCommand

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `clientOperationId` | `Guid`<br>null: خیر؛ required در schema: بله | UUID ثابت عملیات ساخت گفتگو/شروع یا پاسخ/نهایی‌سازی/انتشار آزمون؛ scope هر عملیات در بخش۳۹ Guid غیرخالی؛ برای retry همان payload ثابت بماند. |
| `scope` | [ConversationScope](#type-conversationscope)<br>null: خیر؛ required در schema: بله | Personal/Organization گفتگو؛ scope با role یکسان نیست از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API غیرخالی پس از trim؛ تا۱۶۰ در گفتگو و تا۲۰۰ در آموزش/ارزیابی. |
| `description` | `string?`<br>null: بله؛ required در schema: بله | توضیح؛ nullable و سقف طول بین assignment/exam/report/conversation متفاوت است Assignment: غیرخالی تا۱۰۰۰۰؛ Exam: اختیاری تا۱۰۰۰۰؛ Conversation/Report: اختیاری تا۱۰۰۰. |
| `initialParticipantUserIds` | `IReadOnlyCollection<Guid>?`<br>null: بله؛ required در schema: بله | شناسه account افراد؛ باید eligibility/عضویت معتبر داشته باشند آرایه مطابق DTO فرزند؛ یکتایی، تعلق و حدود تعداد در قواعد حوزه. |


<a id="type-createnextexamversioncommand"></a>
### CreateNextExamVersionCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `expectedExamRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |
| `expectedPublishedVersionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-createschedulerulecommand"></a>
### CreateScheduleRuleCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `localDayOfWeek` | [DayOfWeek](#type-dayofweek)<br>null: خیر؛ required در schema: بله | روز هفته به نام English enum؛ شماره روز فارسی ارسال نشود از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |
| `localStartTime` | `TimeOnly`<br>null: خیر؛ required در schema: بله | ساعت محلی rule به شکل HH:mm:ss؛ با timezone همان rule تفسیر می‌شود HH:mm:ss؛ همراه timezone برای الگوی تکرار. |
| `durationMinutes` | `int`<br>null: خیر؛ required در schema: بله | مدت برحسب دقیقه؛ rule حداکثر۱۴۴۰، exam قانون خودش را دارد اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `timeZoneId` | `string`<br>null: خیر؛ required در schema: بله | شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran |
| `effectiveFrom` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ شروع/پایان الگو یا آغاز اعمال اصلاح؛ DateOnly YYYY-MM-DD؛ اعتبار بازه در domain. |
| `effectiveUntil` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ شروع/پایان الگو یا آغاز اعمال اصلاح؛ DateOnly YYYY-MM-DD؛ اعتبار بازه در domain. |
| `sessionTitle` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `meetingUrl` | `string?`<br>null: بله؛ required در schema: بله | لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند |


<a id="type-createsessioncommand"></a>
### CreateSessionCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API غیرخالی پس از trim؛ تا۱۶۰ در گفتگو و تا۲۰۰ در آموزش/ارزیابی. |
| `startUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع ISO 8601 با Z؛ قواعد بازه/مهلت در کارت endpoint. |
| `endUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع ISO 8601 با Z؛ قواعد بازه/مهلت در کارت endpoint. |
| `timeZoneId` | `string`<br>null: خیر؛ required در schema: بله | شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran |
| `meetingUrl` | `string?`<br>null: بله؛ required در schema: بله | لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند |


<a id="type-currentuserresult"></a>
### CurrentUserResult

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `phoneNumber` | `string`<br>null: خیر؛ required در schema: بله | شماره حساب جاری یا ورودی OTP؛ در هویت عمومی پیام‌رسان منتشر نمی‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isProfileComplete` | `bool`<br>null: خیر؛ required در schema: بله | مرجع تصمیم رفتن به تکمیل profile؛ از وجود یک فیلد استنباط نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `firstName` | `string?`<br>null: بله؛ required در schema: بله | نام/نام خانوادگی؛ برای تکمیل profile غیرخالی، حداکثر ۱۰۰ پس از trim خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lastName` | `string?`<br>null: بله؛ required در schema: بله | نام/نام خانوادگی؛ برای تکمیل profile غیرخالی، حداکثر ۱۰۰ پس از trim خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `displayName` | `string?`<br>null: بله؛ required در schema: بله | نام نمایشی؛ در CompleteProfile اختیاری و حداکثر ۲۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `username` | `string?`<br>null: بله؛ required در schema: بله | شناسه عمومی؛ profile طول ۳..۳۲ با حروف لاتین/رقم/نقطه/underscore و یکتا خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-dashboardassignmentitem"></a>
### DashboardAssignmentItem

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `dueAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | مهلت UTC تکلیف؛ هنگام طراحی/انتشار در آینده و هنگام submit مبنای late policy خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `allowLateSubmission` | `bool`<br>null: خیر؛ required در schema: بله | اجازه تحویل پس از مهلت؛ isLate همچنان توسط سرور تعیین می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [StudentAssignmentActionStatus](#type-studentassignmentactionstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-dashboardattendancependingitem"></a>
### DashboardAttendancePendingItem

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

بازبینی STEP 11A در `2026-10-15`: Expected و Recorded فقط Enrollmentهای فعال همان کلاس را می‌شمارند؛ حضور Enrollment پایان‌یافته نقص حضور عضو فعال دیگری را نمی‌پوشاند. این current-roster backlog است، نه بازسازی roster تاریخی. نبود Attendance رسمی، غیبت قطعی نیست.

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `sessionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه جلسه آموزشی؛ با نشست ورود UserSession متفاوت است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sessionTitle` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `startUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `expectedEnrollmentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد ثبت‌نام فعال مورد انتظار جلسه در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recordedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد حضور رسمی ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `missingCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد ثبت حضور باقی‌مانده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-dashboardattendancesummary"></a>
### DashboardAttendanceSummary

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `eligibleSessionCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد جلسه واجد شرایط حضور در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recordedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد حضور رسمی ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `presentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد حاضر در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `absentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد غایب ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lateCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تأخیر ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `excusedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد غیبت موجه در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `notRecordedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد فاقد رکورد حضور رسمی در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-dashboardclassitem"></a>
### DashboardClassItem

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `courseTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `academicPeriodTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `activeLearnerCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد فراگیر با ثبت‌نام فعال در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-dashboardexamitem"></a>
### DashboardExamItem

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

بازبینی STEP 11A در `2026-10-15`: کلید ردیف `examVersionId` است. فهرست محدود می‌تواند نسخه جاریِ هنوز باز و نسخه تاریخیِ دارای نتیجه منتشرشده را هم‌زمان داشته باشد. وضعیت تلاش، نمره و سقف آن فقط از همان نسخه خوانده می‌شوند؛ نمره18/20 نسخه قبلی با سقف10 نسخه جدید ترکیب نمی‌شود. `releasedScore` و `maximumScore` بدون ExamGradeRelease همان نسخه null هستند. هیچ سؤال، option یا کلید پاسخ در این DTO وجود ندارد.

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examId` | `Guid`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `availableFromUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `availableUntilUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `durationMinutes` | `int`<br>null: خیر؛ required در schema: بله | مدت برحسب دقیقه؛ rule حداکثر۱۴۴۰، exam قانون خودش را دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `latestAttemptStatus` | [ExamAttemptStatus?](#type-examattemptstatus)<br>null: بله؛ required در schema: بله | وضعیت آخرین تلاش آزمون در محدوده مجاز؛ null در نبود تلاش. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `resultReleaseStatus` | [ExamResultReleaseStatus?](#type-examresultreleasestatus)<br>null: بله؛ required در schema: بله | وضعیت انتشار نتیجه آزمون؛ نمره فقط با release قابل نمایش است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedScore` | `decimal?`<br>null: بله؛ required در schema: بله | نمره رسمی منتشرشده؛ null را صفر نمایش ندهید. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal?`<br>null: بله؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-dashboardlearner"></a>
### DashboardLearner

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `displayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی؛ در CompleteProfile اختیاری و حداکثر ۲۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-dashboardsessionitem"></a>
### DashboardSessionItem

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `sessionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه جلسه آموزشی؛ با نشست ورود UserSession متفاوت است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `startUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `timeZoneId` | `string`<br>null: خیر؛ required در schema: بله | شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `meetingUrl` | `string?`<br>null: بله؛ required در schema: بله | لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [SessionStatus](#type-sessionstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-declineinvitationresult"></a>
### DeclineInvitationResult

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `invitationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه دعوت و حساب دعوت‌کننده؛ فقط مخاطب حق accept/decline دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [InvitationStatus](#type-invitationstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-deleteexamquestioncommand"></a>
### DeleteExamQuestionCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `expectedVersionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |
| `expectedQuestionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-detailedreportquery"></a>
### DetailedReportQuery

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

مدل query با propertyهای ارث‌رسیده؛ بدنه JSON نیست.

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `pageNumber` | `int`<br>null: خیر؛ query اختیاری با default مدل | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `pageSize` | `int`<br>null: خیر؛ query اختیاری با default مدل | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `fromUtc` | `DateTime?`<br>null: بله؛ query اختیاری با default مدل | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `toUtc` | `DateTime?`<br>null: بله؛ query اختیاری با default مدل | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `classId` | `Guid?`<br>null: بله؛ query اختیاری با default مدل | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `sort` | `string?`<br>null: بله؛ query اختیاری با default مدل | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |


<a id="type-directconversationresult"></a>
### DirectConversationResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `conversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `type` | [ConversationType](#type-conversationtype)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `scope` | [ConversationScope](#type-conversationscope)<br>null: خیر؛ required در schema: بله | Personal/Organization گفتگو؛ scope با role یکسان نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `otherParticipant` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult)<br>null: خیر؛ required در schema: بله | هویت امن پیام‌رسان؛ فقط userId/username/displayName خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `alreadyExisted` | `bool`<br>null: خیر؛ required در schema: بله | علامت reuse عملیات موجود؛ از HTTP status به‌تنهایی استنباط نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-editmessagecommand"></a>
### EditMessageCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `text` | `string?`<br>null: بله؛ required در schema: بله | متن پیام/گزینه/caption؛ حد و nullability متناسب نوع DTO در قواعد حوزه اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `expectedVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-endenrollmentcommand"></a>
### EndEnrollmentCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `finalStatus` | [EnrollmentStatus](#type-enrollmentstatus)<br>null: خیر؛ required در schema: بله | پایان ثبت‌نام فقط Completed یا Withdrawn از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |


<a id="type-enrolllearnercommand"></a>
### EnrollLearnerCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |


<a id="type-enrollmentresult"></a>
### EnrollmentResult

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [EnrollmentStatus](#type-enrollmentstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `enrolledAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان پایان رابطه به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-evaluationqueueitemresult"></a>
### EvaluationQueueItemResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `submissionAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentTitle` | `string`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `studentDisplayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی دانش‌آموز در صف/ارزیابی مجاز؛ مجوز دسترسی به پرونده عمومی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submittedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان تحویل نهایی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isLate` | `bool`<br>null: خیر؛ required در schema: بله | وضعیت تأخیر تعیین‌شده توسط server acceptance time خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `reviewStatus` | [EvaluationReviewStatus](#type-evaluationreviewstatus)<br>null: خیر؛ required در schema: بله | وضعیت صف ارزیابی تکلیف؛ enum اختصاصی EvaluationReviewStatus خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `evaluationRevisionNumber` | `int?`<br>null: بله؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal?`<br>null: بله؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-evaluatorevaluationresult"></a>
### EvaluatorEvaluationResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submissionAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `revisionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `guardianVisibleFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد مخصوص نمای والد پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `teacherPrivateNote` | `string?`<br>null: بله؛ required در schema: بله | یادداشت خصوصی ارزیاب تا۱۰۰۰۰؛ در DTO عمومی/والد نمایش داده نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [EvaluationRevisionStatus](#type-evaluationrevisionstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdByMembershipId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه عضویت سازمانی ثبت‌کننده این رویداد؛ UserId نیست و سرور تعیین می‌کند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `updatedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان آخرین تغییر به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `supersedesEvaluationRevisionId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه نسخه منتشرشده‌ای که این correction جایگزین آن خواهد شد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `correctionReason` | `string?`<br>null: بله؛ required در schema: بله | دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `rowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-examanswerreceiptdto"></a>
### ExamAnswerReceiptDto

منبع: [ExamAnswerContracts.cs](../MakanApp.Application/Assessment/ExamAnswerContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examAttemptQuestionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه سؤال داخل attempt؛ questionVersionId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `answerRevisionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه ذخیره پذیرفته‌شده پاسخ؛ رسید retry به همین revision بازمی‌گردد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `revisionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `acceptedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان پذیرش سرور به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `writeLeaseVersion` | `long`<br>null: خیر؛ required در schema: بله | وضعیت و نسخه مجوز نوشتن وابسته به نشست؛ deviceId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `answerSetVersion` | `long`<br>null: خیر؛ required در schema: بله | شمارنده مجموعه پاسخ‌ها؛ finalize فقط نسخه پذیرفته‌شده جاری خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | `string`<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-exameditordto"></a>
### ExamEditorDto

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [ExamStatus](#type-examstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `versionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `versionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `versionStatus` | [ExamVersionStatus](#type-examversionstatus)<br>null: خیر؛ required در schema: بله | Draft/Published نسخه؛ با وضعیت attempt و نمره متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `description` | `string?`<br>null: بله؛ required در schema: بله | توضیح؛ nullable و سقف طول بین assignment/exam/report/conversation متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `availableFromUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `availableUntilUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `durationMinutes` | `int`<br>null: خیر؛ required در schema: بله | مدت برحسب دقیقه؛ rule حداکثر۱۴۴۰، exam قانون خودش را دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxAttempts` | `int`<br>null: خیر؛ required در schema: بله | حد مثبت تلاش مجاز؛ تعداد تلاش با retry یک عملیات متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `randomizationPolicy` | [ExamRandomizationPolicy](#type-examrandomizationpolicy)<br>null: خیر؛ required در schema: بله | None یا QuestionOrder؛ ترتیب frozen از attempt خوانده شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `publishedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `versionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `questions` | [IReadOnlyCollection&lt;ExamEditorQuestionDto&gt;](#type-exameditorquestiondto)<br>null: خیر؛ required در schema: بله | آرایه سؤال/گزینه همین DTO؛ مدل editor کلید دارد، student-safe ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-exameditoroptiondto"></a>
### ExamEditorOptionDto

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `order` | `int`<br>null: خیر؛ required در schema: بله | ترتیب مثبت؛ displayOrder تلاش ترتیب frozen است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `text` | `string`<br>null: خیر؛ required در schema: بله | متن پیام/گزینه/caption؛ حد و nullability متناسب نوع DTO در قواعد حوزه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isCorrect` | `bool`<br>null: خیر؛ required در schema: بله | کلید پاسخ فقط در authoring؛ هرگز در cache نمای دانش‌آموز/والد کپی نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-exameditorquestiondto"></a>
### ExamEditorQuestionDto

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `order` | `int`<br>null: خیر؛ required در schema: بله | ترتیب مثبت؛ displayOrder تلاش ترتیب frozen است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `type` | [ExamQuestionType](#type-examquestiontype)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `prompt` | `string`<br>null: خیر؛ required در schema: بله | متن سؤال غیرخالی تا۲۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `options` | [IReadOnlyCollection&lt;ExamEditorOptionDto&gt;](#type-exameditoroptiondto)<br>null: خیر؛ required در schema: بله | آرایه سؤال/گزینه همین DTO؛ مدل editor کلید دارد، student-safe ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `rowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-examfinalreceiptdto"></a>
### ExamFinalReceiptDto

منبع: [ExamFinalizationContracts.cs](../MakanApp.Application/Assessment/ExamFinalizationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examId` | `Guid`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `startedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `finalizedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان نهایی‌سازی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `finalizedAnswerSetVersion` | `long`<br>null: خیر؛ required در schema: بله | شمارنده مجموعه پاسخ‌ها؛ finalize فقط نسخه پذیرفته‌شده جاری خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `answeredQuestionCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد پاسخ دارای محتوا و کل سؤال‌های frozen در receipt خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalQuestionCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد پاسخ دارای محتوا و کل سؤال‌های frozen در receipt خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [ExamAttemptStatus](#type-examattemptstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-examgradedto"></a>
### ExamGradeDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examGradeRevisionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه revision نمره؛ history با اصلاح جایگزین نمی‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examId` | `Guid`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examTitle` | `string`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `studentDisplayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی دانش‌آموز در صف/ارزیابی مجاز؛ مجوز دسترسی به پرونده عمومی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `finalizedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان نهایی‌سازی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `revisionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalScore` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [ExamGradeRevisionStatus](#type-examgraderevisionstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `guardianVisibleFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد مخصوص نمای والد پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `evaluatorPrivateNote` | `string?`<br>null: بله؛ required در schema: بله | یادداشت خصوصی ارزیاب تا۱۰۰۰۰؛ در DTO عمومی/والد نمایش داده نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdByMembershipId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه عضویت سازمانی ثبت‌کننده این رویداد؛ UserId نیست و سرور تعیین می‌کند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `updatedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان آخرین تغییر به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `supersedesExamGradeRevisionId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه نسخه منتشرشده‌ای که این correction جایگزین آن خواهد شد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `correctionReason` | `string?`<br>null: بله؛ required در schema: بله | دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `rowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `questions` | [IReadOnlyCollection&lt;ExamQuestionGradeDto&gt;](#type-examquestiongradedto)<br>null: خیر؛ required در schema: بله | آرایه سؤال/گزینه همین DTO؛ مدل editor کلید دارد، student-safe ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-examgradereleasedto"></a>
### ExamGradeReleaseDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examGradeReleaseId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه رویداد انتشار پایدار نمره؛ با ذخیره draft متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examGradeRevisionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه revision نمره؛ history با اصلاح جایگزین نمی‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `gradeRevisionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalScore` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedByMembershipId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه عضویت سازمانی ثبت‌کننده این رویداد؛ UserId نیست و سرور تعیین می‌کند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `gradeRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-examgradingqueueitemdto"></a>
### ExamGradingQueueItemDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examId` | `Guid`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examTitle` | `string`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `studentDisplayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی دانش‌آموز در صف/ارزیابی مجاز؛ مجوز دسترسی به پرونده عمومی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `finalizedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان نهایی‌سازی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [ExamGradingQueueStatus](#type-examgradingqueuestatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `gradeRevisionNumber` | `int?`<br>null: بله؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `requiresManualReview` | `bool`<br>null: خیر؛ required در schema: بله | وجود پاسخ/بازبینی یا نیاز دستی؛ score صفر به‌تنهایی این حالت‌ها را تعیین نمی‌کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-examgradingqueuesummary"></a>
### ExamGradingQueueSummary

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

صف scope مجاز، همه attemptهای Finalized را در بر می‌گیرد، نه فقط آزمون دارای سؤال توصیفی؛ آزمون objective نیز می‌تواند در انتظار آغاز grading یا انتشار باشد. بازبینی Draft و ReadyForRelease از آخرین GradeRevision خوانده می‌شود و Released جزو pending نیست.

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `awaitingGradingCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تلاش در انتظار تصحیح در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `manualReviewCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تلاش نیازمند بازبینی دستی در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `readyForReleaseCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تصحیح آماده انتشار در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalPendingCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد کل backlog همین دسته در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-examquestiongradedto"></a>
### ExamQuestionGradeDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptQuestionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه سؤال داخل attempt؛ questionVersionId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `questionVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `displayOrder` | `int`<br>null: خیر؛ required در schema: بله | ترتیب مثبت؛ displayOrder تلاش ترتیب frozen است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `questionType` | [ExamQuestionType](#type-examquestiontype)<br>null: خیر؛ required در schema: بله | ObjectiveSingleChoice یا Descriptive؛ شکل پاسخ باید مطابق باشد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `prompt` | `string`<br>null: خیر؛ required در schema: بله | متن سؤال غیرخالی تا۲۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isAnswered` | `bool`<br>null: خیر؛ required در schema: بله | وجود پاسخ/بازبینی یا نیاز دستی؛ score صفر به‌تنهایی این حالت‌ها را تعیین نمی‌کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `selectedOptionText` | `string?`<br>null: بله؛ required در schema: بله | گزینه انتخاب‌شده از سؤال frozen؛ در پاسخ تشریحی null خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `textAnswer` | `string?`<br>null: بله؛ required در schema: بله | پاسخ تشریحی تا۲۰۰۰۰؛ در objective null، در descriptive غیرnull خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `awardedScore` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `gradingMode` | [ExamQuestionGradingMode](#type-examquestiongradingmode)<br>null: خیر؛ required در schema: بله | Objective خودکار از key یا Manual برای تشریحی خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isReviewed` | `bool`<br>null: خیر؛ required در schema: بله | وجود پاسخ/بازبینی یا نیاز دستی؛ score صفر به‌تنهایی این حالت‌ها را تعیین نمی‌کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `evaluatorPrivateNote` | `string?`<br>null: بله؛ required در schema: بله | یادداشت خصوصی ارزیاب تا۱۰۰۰۰؛ در DTO عمومی/والد نمایش داده نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `reviewedByMembershipId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه عضویت سازمانی ثبت‌کننده این رویداد؛ UserId نیست و سرور تعیین می‌کند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `reviewedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-examquestionoptioncommand"></a>
### ExamQuestionOptionCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `order` | `int`<br>null: خیر؛ required در schema: بله | ترتیب مثبت؛ displayOrder تلاش ترتیب frozen است اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `text` | `string`<br>null: خیر؛ required در schema: بله | متن پیام/گزینه/caption؛ حد و nullability متناسب نوع DTO در قواعد حوزه اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `isCorrect` | `bool`<br>null: خیر؛ required در schema: بله | کلید پاسخ فقط در authoring؛ هرگز در cache نمای دانش‌آموز/والد کپی نشود اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |


<a id="type-examteacherpreview"></a>
### ExamTeacherPreview

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `exam` | [StudentExamSummary](#type-studentexamsummary)<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `questions` | [IReadOnlyCollection&lt;ExamEditorQuestionDto&gt;](#type-exameditorquestiondto)<br>null: خیر؛ required در schema: بله | آرایه سؤال/گزینه همین DTO؛ مدل editor کلید دارد، student-safe ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-examwriteleasedto"></a>
### ExamWriteLeaseDto

منبع: [ExamAnswerContracts.cs](../MakanApp.Application/Assessment/ExamAnswerContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isCurrentSessionWriter` | `bool`<br>null: خیر؛ required در schema: بله | آیا این نشست در حال حاضر مجاز به نوشتن است؛ deadline نیز لحاظ شده خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `writeLeaseVersion` | `long`<br>null: خیر؛ required در schema: بله | وضعیت و نسخه مجوز نوشتن وابسته به نشست؛ deviceId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `canRequestTransfer` | `bool`<br>null: خیر؛ required در schema: بله | امکان درخواست transfer به نشست جاری؛ انتقال خودکار توصیه نمی‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `acquiredAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان دریافت lease به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `answerSetVersion` | `long`<br>null: خیر؛ required در schema: بله | شمارنده مجموعه پاسخ‌ها؛ finalize فقط نسخه پذیرفته‌شده جاری خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `serverNowUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان authoritative سرور برای countdown و سنجش اختلاف ساعت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-fileassetresult"></a>
### FileAssetResult

منبع: [StorageContracts.cs](../MakanApp.Application/Storage/StorageContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `uploadedByUserId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه حساب انجام‌دهنده رویداد؛ سرور از actor معتبر ثبت می‌کند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `originalFileName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی امن فایل؛ مسیر ذخیره‌سازی سرور نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `contentType` | `string`<br>null: خیر؛ required در schema: بله | MIME تأییدشده فایل؛ Content-Type دلخواه کلاینت قابل اعتماد نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sizeBytes` | `long`<br>null: خیر؛ required در schema: بله | اندازه برحسب بایت؛ حد upload پایه10485760 خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sha256Hash` | `string?`<br>null: بله؛ required در schema: بله | هش metadata محتوا، nullable قبل از تکمیل؛ credential یا URL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [FileAssetStatus](#type-fileassetstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `completedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `deletedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `retainedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان تثبیت retention به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `unattachedExpiresAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | انقضای فایل بدون binding؛ cleanup worker فعلی وجود ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `rowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-finalsubmitassignmentcommand"></a>
### FinalSubmitAssignmentCommand

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `expectedRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-finalizeexamcommand"></a>
### FinalizeExamCommand

منبع: [ExamFinalizationContracts.cs](../MakanApp.Application/Assessment/ExamFinalizationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `clientOperationId` | `Guid`<br>null: خیر؛ required در schema: بله | UUID ثابت عملیات ساخت گفتگو/شروع یا پاسخ/نهایی‌سازی/انتشار آزمون؛ scope هر عملیات در بخش۳۹ Guid غیرخالی؛ برای retry همان payload ثابت بماند. |
| `expectedAnswerSetVersion` | `long`<br>null: خیر؛ required در schema: بله | شمارنده مجموعه پاسخ‌ها؛ finalize فقط نسخه پذیرفته‌شده جاری برابر آخرین answerSetVersion پذیرفته‌شده. |
| `writeLeaseVersion` | `long`<br>null: خیر؛ required در schema: بله | وضعیت و نسخه مجوز نوشتن وابسته به نشست؛ deviceId نیست اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |


<a id="type-forwardmessagecommand"></a>
### ForwardMessageCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `destinationConversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |
| `clientMessageId` | `Guid`<br>null: خیر؛ required در schema: بله | UUID ساخته کلاینت و ثابت در retry ارسال/forward همان payload Guid غیرخالی؛ برای retry همان payload ثابت بماند. |


<a id="type-gradeexamquestioncommand"></a>
### GradeExamQuestionCommand

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `awardedScore` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست حداکثر دو رقم اعشار؛ سقف domain 9999999.99. نمره ارزیابی ۰..بارم؛ بارم طراحی >۰. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `evaluatorPrivateNote` | `string?`<br>null: بله؛ required در schema: بله | یادداشت خصوصی ارزیاب تا۱۰۰۰۰؛ در DTO عمومی/والد نمایش داده نشود اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `expectedGradeRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-gradereleaseresult"></a>
### GradeReleaseResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `gradeReleaseId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه رویداد انتشار پایدار نمره؛ با ذخیره draft متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submissionAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `evaluationRevisionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه revision نمره؛ history با اصلاح جایگزین نمی‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `revisionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedByMembershipId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه عضویت سازمانی ثبت‌کننده این رویداد؛ UserId نیست و سرور تعیین می‌کند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `evaluationRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-guardianexamresultdto"></a>
### GuardianExamResultDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examId` | `Guid`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `finalizedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان نهایی‌سازی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releaseStatus` | [ExamResultReleaseStatus](#type-examresultreleasestatus)<br>null: خیر؛ required در schema: بله | AwaitingGrading/AwaitingRelease/Released؛ score=null پیش از انتشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `gradeRevisionNumber` | `int?`<br>null: بله؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal?`<br>null: بله؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal?`<br>null: بله؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `guardianVisibleFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد مخصوص نمای والد پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-guardianrelationsummaryresult"></a>
### GuardianRelationSummaryResult

منبع: [GuardianContracts.cs](../MakanApp.Application/Guardian/GuardianContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `relationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/وضعیت رابطه سرپرستی؛ فعال‌بودن همچنان در سرور کنترل می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `displayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی؛ در CompleteProfile اختیاری و حداکثر ۲۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationName` | `string`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [GuardianRelationStatus](#type-guardianrelationstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `validFromUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان آغاز اعتبار رابطه به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-invitationresult"></a>
### InvitationResult

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationName` | `string`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `invitedByUserId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه دعوت و حساب دعوت‌کننده؛ فقط مخاطب حق accept/decline دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `role` | [OrganizationRole](#type-organizationrole)<br>null: خیر؛ required در schema: بله | نقش در context مربوط؛ OrganizationRole با ConversationParticipantRole متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [InvitationStatus](#type-invitationstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `expiresAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | انقضای challenge/نشست/دعوت در قرارداد مربوط؛ ساعت سرور مرجع است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-managedconversationresult"></a>
### ManagedConversationResult

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `conversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `type` | [ConversationType](#type-conversationtype)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `scope` | [ConversationScope](#type-conversationscope)<br>null: خیر؛ required در schema: بله | Personal/Organization گفتگو؛ scope با role یکسان نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `description` | `string?`<br>null: بله؛ required در schema: بله | توضیح؛ nullable و سقف طول بین assignment/exam/report/conversation متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `managementPolicy` | [ConversationManagementPolicy](#type-conversationmanagementpolicy)<br>null: خیر؛ required در schema: بله | None/UserManaged/SystemManagedAcademic؛ وضعیت دوم دارای API مدیریت فعلی است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [ConversationStatus](#type-conversationstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `archivedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `participants` | [IReadOnlyCollection&lt;ConversationParticipantResult&gt;](#type-conversationparticipantresult)<br>null: خیر؛ required در schema: بله | اعضای برگشتی مدل مدیریت؛ role پیام‌رسان مستقل از role سازمان خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `alreadyExisted` | `bool`<br>null: خیر؛ required در schema: بله | علامت reuse عملیات موجود؛ از HTTP status به‌تنهایی استنباط نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-managerassessmentreportitem"></a>
### ManagerAssessmentReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `kind` | [DetailedReportAssessmentKind](#type-detailedreportassessmentkind)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assessmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه Assignment یا Exam مطابق kind؛ نوع منبع را همراه شناسه نگه دارید. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `versionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `courseTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `deadlineUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | مهلت تکلیف یا پایان window آزمون مطابق kind؛ زمان UTC. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recipientCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد گیرندگان snapshot ساخته‌شده هنگام publish خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submittedOrFinalizedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تحویل تکلیف یا نهایی‌سازی آزمون مطابق kind در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedResultCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد نتیجه رسمی منتشرشده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pendingGradingCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد مورد نیازمند تصحیح/انتشار در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-managerassessmentreportquery"></a>
### ManagerAssessmentReportQuery

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

مدل query با propertyهای ارث‌رسیده؛ بدنه JSON نیست.

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `pageNumber` | `int`<br>null: خیر؛ query اختیاری با default مدل | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `pageSize` | `int`<br>null: خیر؛ query اختیاری با default مدل | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `fromUtc` | `DateTime?`<br>null: بله؛ query اختیاری با default مدل | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `toUtc` | `DateTime?`<br>null: بله؛ query اختیاری با default مدل | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `classId` | `Guid?`<br>null: بله؛ query اختیاری با default مدل | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `sort` | `string?`<br>null: بله؛ query اختیاری با default مدل | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `courseId` | `Guid?`<br>null: بله؛ query اختیاری با default مدل | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `academicPeriodId` | `Guid?`<br>null: بله؛ query اختیاری با default مدل | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `kind` | [DetailedReportAssessmentKind?](#type-detailedreportassessmentkind)<br>null: بله؛ query اختیاری با default مدل | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |


<a id="type-managerattendancereportitem"></a>
### ManagerAttendanceReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `courseTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `eligibleRecordCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد ردیف حضور مورد انتظار کلاس در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recordedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد حضور رسمی ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `presentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد حاضر در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `absentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد غایب ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lateCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تأخیر ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `excusedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد غیبت موجه در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `notRecordedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد فاقد رکورد حضور رسمی در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recordedAbsencePercentage` | `decimal?`<br>null: بله؛ required در schema: بله | درصد غیبت در حضورهای ثبت‌شده؛ notRecorded در مخرج نیست؛ مخرج صفر یعنی null. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-managerclassperformancedto"></a>
### ManagerClassPerformanceDto

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `courseTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `activeLearnerCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد فراگیر با ثبت‌نام فعال در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentReleasedResultCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد نتیجه رسمی تکلیف در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentAveragePercentage` | `decimal?`<br>null: بله؛ required در schema: بله | میانگین درصد نمرات منتشرشده تکلیف؛ در نبود نتیجه null. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examReleasedResultCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد نتیجه رسمی آزمون در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examAveragePercentage` | `decimal?`<br>null: بله؛ required در schema: بله | میانگین درصد نمرات منتشرشده آزمون، مستقل از تکلیف؛ در نبود نتیجه null. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-managerdashboarddto"></a>
### ManagerDashboardDto

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `audience` | [DashboardAudience](#type-dashboardaudience)<br>null: خیر؛ required در schema: بله | نقش مخاطب همان DTO؛ با activeRole تطبیق داده شود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `operationalCounts` | [ManagerOperationalCounts](#type-manageroperationalcounts)<br>null: خیر؛ required در schema: بله | شمارنده‌های عملیاتی همان سازمان؛ وضعیت پلتفرم یا سایر سازمان‌ها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `onboarding` | [ManagerOnboardingChecklist](#type-manageronboardingchecklist)<br>null: خیر؛ required در schema: بله | وضعیت وجود داده اولیه سازمان؛ داشتن checklist به معنی وجود API ساخت هر قلم نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `upcomingSessions` | [IReadOnlyCollection&lt;DashboardSessionItem&gt;](#type-dashboardsessionitem)<br>null: خیر؛ required در schema: بله | حداکثر10 جلسه برنامه‌ریزی‌شده از داده مجاز نقش. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sessionsMissingMeetingLink` | [IReadOnlyCollection&lt;DashboardSessionItem&gt;](#type-dashboardsessionitem)<br>null: خیر؛ required در schema: بله | جلسه‌های آینده فاقد meetingUrl؛ به معنی وجود endpoint ساخت لینک جلسه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attendancePending` | [IReadOnlyCollection&lt;DashboardAttendancePendingItem&gt;](#type-dashboardattendancependingitem)<br>null: خیر؛ required در schema: بله | جلسه‌های پایان‌یافته با ثبت حضور ناقص؛ هر ردیف missingCount دارد. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pendingInvitationCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد دعوت Pending در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentGradingQueue` | [AssignmentGradingQueueSummary](#type-assignmentgradingqueuesummary)<br>null: خیر؛ required در schema: بله | تعداد پاسخ‌های در انتظار ارزیابی و ارزیابی پیش‌نویس؛ متن پاسخ در این DTO نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examGradingQueue` | [ExamGradingQueueSummary](#type-examgradingqueuesummary)<br>null: خیر؛ required در schema: بله | تعداد تلاش‌های در انتظار تصحیح، بازبینی دستی یا انتشار؛ بدون پاسخ خصوصی. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-managerenrollmentreportitem"></a>
### ManagerEnrollmentReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `enrollmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه ثبت‌نام؛ با learnerOrganizationPersonId متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerDisplayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی فراگیر مجاز؛ هویت با learnerOrganizationPersonId مشخص می‌شود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `courseId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `courseTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `academicPeriodId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `academicPeriodTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [EnrollmentStatus](#type-enrollmentstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `enrolledAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان پایان رابطه به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-managerenrollmentreportquery"></a>
### ManagerEnrollmentReportQuery

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

مدل query با propertyهای ارث‌رسیده؛ بدنه JSON نیست.

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `pageNumber` | `int`<br>null: خیر؛ query اختیاری با default مدل | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `pageSize` | `int`<br>null: خیر؛ query اختیاری با default مدل | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `fromUtc` | `DateTime?`<br>null: بله؛ query اختیاری با default مدل | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `toUtc` | `DateTime?`<br>null: بله؛ query اختیاری با default مدل | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `classId` | `Guid?`<br>null: بله؛ query اختیاری با default مدل | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `sort` | `string?`<br>null: بله؛ query اختیاری با default مدل | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `courseId` | `Guid?`<br>null: بله؛ query اختیاری با default مدل | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `academicPeriodId` | `Guid?`<br>null: بله؛ query اختیاری با default مدل | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `status` | [EnrollmentStatus?](#type-enrollmentstatus)<br>null: بله؛ query اختیاری با default مدل | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |


<a id="type-manageronboardingchecklist"></a>
### ManagerOnboardingChecklist

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `organizationBasicDataAvailable` | `bool`<br>null: خیر؛ required در schema: بله | آیا اطلاعات پایه سازمان در سازمان وجود دارد؛ نشانگر onboarding و فاقد mutation است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `hasAcademicPeriod` | `bool`<br>null: خیر؛ required در schema: بله | آیا دوره آموزشی در سازمان وجود دارد؛ نشانگر onboarding و فاقد mutation است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `hasCourse` | `bool`<br>null: خیر؛ required در schema: بله | آیا درس در سازمان وجود دارد؛ نشانگر onboarding و فاقد mutation است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `hasTeacher` | `bool`<br>null: خیر؛ required در schema: بله | آیا معلم در سازمان وجود دارد؛ نشانگر onboarding و فاقد mutation است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `hasLearner` | `bool`<br>null: خیر؛ required در schema: بله | آیا فراگیر در سازمان وجود دارد؛ نشانگر onboarding و فاقد mutation است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `hasClass` | `bool`<br>null: خیر؛ required در schema: بله | آیا کلاس در سازمان وجود دارد؛ نشانگر onboarding و فاقد mutation است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-manageroperationalcounts"></a>
### ManagerOperationalCounts

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `activeClassCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد کلاس فعال در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `enrolledLearnerCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد فراگیر متمایز با ثبت‌نام فعال در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `teacherCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد عضویت Teacher فعال در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-managerteacherworkloadreportitem"></a>
### ManagerTeacherWorkloadReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `teacherMembershipId` | `Guid`<br>null: خیر؛ required در schema: بله | عضویت فعال دارای Teacher در همان سازمان؛ نه UserId معلم خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `teacherUserId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه حساب معلم مرتبط با teacherMembershipId؛ selector مجوز تازه ایجاد نمی‌کند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `teacherDisplayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی معلم؛ شناسه عضویت و حساب مستقل‌اند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `activeClassCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد کلاس فعال در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `completedSessionCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد جلسه پایان‌یافته غیرلغوشده در بازه زمانی workload؛ بازه روی همه شمارنده‌های دیگر اعمال نمی‌شود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attendanceMissingCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد جلسه دارای ثبت حضور ناقص؛ تعداد دانش‌آموز غایب نیست؛ در workload به fromUtc/toUtc محدود نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentPendingGradingCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد backlog تکلیف معلم در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examPendingGradingCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد backlog آزمون معلم در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-messageattachmentresult"></a>
### MessageAttachmentResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `fileAssetId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه فایل Ready؛ بارگذاری موفق به معنی attach یا submit نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `kind` | [MessageKind](#type-messagekind)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `fileName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی امن فایل؛ مسیر ذخیره‌سازی سرور نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `contentType` | `string`<br>null: خیر؛ required در schema: بله | MIME تأییدشده فایل؛ Content-Type دلخواه کلاینت قابل اعتماد نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sizeBytes` | `long`<br>null: خیر؛ required در schema: بله | اندازه برحسب بایت؛ حد upload پایه10485760 خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-messagemutationresult"></a>
### MessageMutationResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `messageId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پایدار پیام سرور؛ merge تاریخچه با این ID و clientMessageId محلی خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sequence` | `long`<br>null: خیر؛ required در schema: بله | شماره پیام در گفتگو؛ beforeSequence انحصاری، null پایان تاریخچه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `editedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `deletedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isDeleted` | `bool`<br>null: خیر؛ required در schema: بله | پرچم نمایش پیام؛ deleted باید محتوای cached را از نمایش خارج کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `version` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-messagereactionresult"></a>
### MessageReactionResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `messageId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پایدار پیام سرور؛ merge تاریخچه با این ID و clientMessageId محلی خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `reaction` | [MessageReactionType](#type-messagereactiontype)<br>null: خیر؛ required در schema: بله | واکنش enum و aggregate مجاز؛ یک واکنش فعال هر کاربر خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-messagereactionsummaryresult"></a>
### MessageReactionSummaryResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `reaction` | [MessageReactionType](#type-messagereactiontype)<br>null: خیر؛ required در schema: بله | واکنش enum و aggregate مجاز؛ یک واکنش فعال هر کاربر خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `count` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `reactedByCurrentUser` | `bool`<br>null: خیر؛ required در schema: بله | واکنش enum و aggregate مجاز؛ یک واکنش فعال هر کاربر خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-messagereceiptresult"></a>
### MessageReceiptResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `messageId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پایدار پیام سرور؛ merge تاریخچه با این ID و clientMessageId محلی خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `conversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `clientMessageId` | `Guid`<br>null: خیر؛ required در schema: بله | UUID ساخته کلاینت و ثابت در retry ارسال/forward همان payload خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sequence` | `long`<br>null: خیر؛ required در schema: بله | شماره پیام در گفتگو؛ beforeSequence انحصاری، null پایان تاریخچه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `kind` | [MessageKind](#type-messagekind)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sentAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ارسال ثبت‌شده به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [MessageReceiptStatus](#type-messagereceiptstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `version` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-messagereplysummaryresult"></a>
### MessageReplySummaryResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `messageId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پایدار پیام سرور؛ merge تاریخچه با این ID و clientMessageId محلی خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `senderUserId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه actor رویداد/فرستنده؛ null actor در رویداد ممکن است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `kind` | [MessageKind?](#type-messagekind)<br>null: بله؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `text` | `string?`<br>null: بله؛ required در schema: بله | متن پیام/گزینه/caption؛ حد و nullability متناسب نوع DTO در قواعد حوزه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isDeleted` | `bool`<br>null: خیر؛ required در schema: بله | پرچم نمایش پیام؛ deleted باید محتوای cached را از نمایش خارج کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-messagesearchitemresult"></a>
### MessageSearchItemResult

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `messageId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پایدار پیام سرور؛ merge تاریخچه با این ID و clientMessageId محلی خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `conversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `conversationDisplayName` | `string?`<br>null: بله؛ required در schema: بله | عنوان گفتگو در نتیجه جست‌وجو؛ ممکن است null باشد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sender` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult)<br>null: خیر؛ required در schema: بله | هویت امن پیام‌رسان؛ فقط userId/username/displayName خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `snippet` | `string?`<br>null: بله؛ required در schema: بله | متن کوتاه شده؛ preview تا۱۲۰ و snippet تا۲۴۰؛ HTML معتبر فرض نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `kind` | [MessageKind](#type-messagekind)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sentAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ارسال ثبت‌شده به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sequence` | `long`<br>null: خیر؛ required در schema: بله | شماره پیام در گفتگو؛ beforeSequence انحصاری، null پایان تاریخچه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attachments` | [IReadOnlyCollection&lt;MessageAttachmentResult&gt;](#type-messageattachmentresult)<br>null: خیر؛ required در schema: بله | پیوست‌های مجاز resource؛ download همچنان authorization مستقل دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-messagesearchpageresult"></a>
### MessageSearchPageResult

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `items` | [IReadOnlyCollection&lt;MessageSearchItemResult&gt;](#type-messagesearchitemresult)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `nextCursor` | `string?`<br>null: بله؛ required در schema: بله | نشانگر opaque؛ cursor جست‌وجو و Delta قابل تعویض نیستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-messagingrealtimenotification"></a>
### MessagingRealtimeNotification

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `conversationId` | `Guid`<br>null: خیر؛ DTO رویداد Hub | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `type` | [MessagingChangeType](#type-messagingchangetype)<br>null: خیر؛ DTO رویداد Hub | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `resourceId` | `Guid`<br>null: خیر؛ DTO رویداد Hub | منبع تغییر و نسخه آن؛ payload رویداد متن کامل پیام نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `resourceVersion` | `string?`<br>null: بله؛ DTO رویداد Hub | منبع تغییر و نسخه آن؛ payload رویداد متن کامل پیام نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `cursor` | `string`<br>null: خیر؛ DTO رویداد Hub | نشانگر opaque؛ cursor جست‌وجو و Delta قابل تعویض نیستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `occurredAtUtc` | `DateTime`<br>null: خیر؛ DTO رویداد Hub | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `payloadVersion` | `int`<br>null: خیر؛ DTO رویداد Hub | نسخه شکل رویداد؛ نوع ناشناخته باید باعث refresh امن شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-ownershiptransferresult"></a>
### OwnershipTransferResult

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `transferId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پیشنهاد انتقال مالکیت برای accept/decline مقصد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `conversationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گفتگو مبدا/مقصد؛ forward هر دو مجوز و scope سازگار می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `fromUserId` | `Guid`<br>null: خیر؛ required در schema: بله | حساب مقصد/مبدا رابطه؛ داشتن ID اثبات مجوز نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `toUserId` | `Guid`<br>null: خیر؛ required در schema: بله | حساب مقصد/مبدا رابطه؛ داشتن ID اثبات مجوز نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [OwnershipTransferStatus](#type-ownershiptransferstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `acceptedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان پذیرش سرور به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `declinedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `alreadyCompleted` | `bool`<br>null: خیر؛ required در schema: بله | علامت reuse عملیات موجود؛ از HTTP status به‌تنهایی استنباط نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-parentassignmentreportitem"></a>
### ParentAssignmentReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentStatus` | [AssignmentStatus](#type-assignmentstatus)<br>null: خیر؛ required در schema: بله | چرخه رسمی Assignment؛ Published و Closed در گزارش تاریخچه قابل مشاهده‌اند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `dueAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | مهلت UTC تکلیف؛ هنگام طراحی/انتشار در آینده و هنگام submit مبنای late policy خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submissionStatus` | [SubmissionAttemptStatus?](#type-submissionattemptstatus)<br>null: بله؛ required در schema: بله | وضعیت آخرین تلاش تکلیف؛ null یعنی submission متناظر وجود ندارد. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submittedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان تحویل نهایی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isLate` | `bool?`<br>null: بله؛ required در schema: بله | وضعیت تأخیر تعیین‌شده توسط server acceptance time خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedScore` | `decimal?`<br>null: بله؛ required در schema: بله | نمره رسمی منتشرشده؛ null را صفر نمایش ندهید. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `guardianVisibleFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد مخصوص نمای والد پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-parentattendancereportitem"></a>
### ParentAttendanceReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `sessionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه جلسه آموزشی؛ با نشست ورود UserSession متفاوت است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `courseTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sessionTitle` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `startUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [AttendanceStatus](#type-attendancestatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recordedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان ثبت حضور به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-parentdashboarddto"></a>
### ParentDashboardDto

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `audience` | [DashboardAudience](#type-dashboardaudience)<br>null: خیر؛ required در schema: بله | نقش مخاطب همان DTO؛ با activeRole تطبیق داده شود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `selectedLearner` | [DashboardLearner?](#type-dashboardlearner)<br>null: بله؛ required در schema: بله | مشخصات حداقلی فرزند انتخاب‌شده؛ null در داشبورد Parent یعنی ابتدا انتخاب فرزند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `upcomingSessions` | [IReadOnlyCollection&lt;DashboardSessionItem&gt;](#type-dashboardsessionitem)<br>null: خیر؛ required در schema: بله | حداکثر10 جلسه برنامه‌ریزی‌شده از داده مجاز نقش. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignments` | [IReadOnlyCollection&lt;DashboardAssignmentItem&gt;](#type-dashboardassignmentitem)<br>null: خیر؛ required در schema: بله | تکالیف مجاز فرزند با وضعیت عملیاتی؛ وجود Closed برای تاریخچه است، مجوز submit نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAssignmentResults` | [IReadOnlyCollection&lt;ParentReleasedAssignmentItem&gt;](#type-parentreleasedassignmentitem)<br>null: خیر؛ required در schema: بله | نتایج رسمی تکلیف فرزند فقط پس از GradeRelease با feedback مخصوص والد. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `exams` | [IReadOnlyCollection&lt;DashboardExamItem&gt;](#type-dashboardexamitem)<br>null: خیر؛ required در schema: بله | metadata امن آزمون‌های فرزند؛ بدون محتوای سؤال و پاسخ. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attendanceSummary` | [DashboardAttendanceSummary](#type-dashboardattendancesummary)<br>null: خیر؛ required در schema: بله | خلاصه حضور رسمی؛ notRecorded را غیبت محسوب نکنید. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attentionItemCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تکلیف Published با موعد آینده و بدون submission/grade؛ Closed فقط تاریخچه است و شمرده نمی‌شود. اعلان یا غیبت قطعی نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-parentexamreportitem"></a>
### ParentExamReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examId` | `Guid`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `availableFromUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `availableUntilUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `durationMinutes` | `int`<br>null: خیر؛ required در schema: بله | مدت برحسب دقیقه؛ rule حداکثر۱۴۴۰، exam قانون خودش را دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تلاش ثبت‌شده آزمون در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `latestAttemptStatus` | [ExamAttemptStatus?](#type-examattemptstatus)<br>null: بله؛ required در schema: بله | وضعیت آخرین تلاش آزمون در محدوده مجاز؛ null در نبود تلاش. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `latestFinalizedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedScore` | `decimal?`<br>null: بله؛ required در schema: بله | نمره رسمی منتشرشده؛ null را صفر نمایش ندهید. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `guardianVisibleFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد مخصوص نمای والد پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-parentreleasedassignmentitem"></a>
### ParentReleasedAssignmentItem

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `submissionAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentTitle` | `string`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `guardianVisibleFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد مخصوص نمای والد پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-parentreleasedresult"></a>
### ParentReleasedResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `submissionAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `evaluationRevisionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `guardianVisibleFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد مخصوص نمای والد پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-publishassignmentcommand"></a>
### PublishAssignmentCommand

منبع: [AssignmentContracts.cs](../MakanApp.Application/Assessment/AssignmentContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `expectedAssignmentRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |
| `expectedVersionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-publishassignmentresult"></a>
### PublishAssignmentResult

منبع: [AssignmentContracts.cs](../MakanApp.Application/Assessment/AssignmentContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `assignment` | [AssignmentResult](#type-assignmentresult)<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recipientCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد گیرندگان snapshot ساخته‌شده هنگام publish خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-publishexamcommand"></a>
### PublishExamCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `expectedExamRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |
| `expectedVersionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-recordattendancecommand"></a>
### RecordAttendanceCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `entries` | [IReadOnlyCollection&lt;RecordAttendanceEntryCommand&gt;](#type-recordattendanceentrycommand)<br>null: خیر؛ required در schema: بله | آرایه ردیف‌های حضور؛ هر entry با enrollmentId معتبر مشخص می‌شود آرایه مطابق DTO فرزند؛ یکتایی، تعلق و حدود تعداد در قواعد حوزه. |


<a id="type-recordattendanceentrycommand"></a>
### RecordAttendanceEntryCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `enrollmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه ثبت‌نام؛ با learnerOrganizationPersonId متفاوت است UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |
| `status` | [AttendanceStatus](#type-attendancestatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |


<a id="type-releaseevaluationcommand"></a>
### ReleaseEvaluationCommand

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `expectedRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-releaseexamgradecommand"></a>
### ReleaseExamGradeCommand

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `clientOperationId` | `Guid`<br>null: خیر؛ required در schema: بله | UUID ثابت عملیات ساخت گفتگو/شروع یا پاسخ/نهایی‌سازی/انتشار آزمون؛ scope هر عملیات در بخش۳۹ Guid غیرخالی؛ برای retry همان payload ثابت بماند. |
| `expectedGradeRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-reportmessagecommand"></a>
### ReportMessageCommand

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `clientReportId` | `Guid`<br>null: خیر؛ required در schema: بله | UUID ثابت گزارش پیام و payload یکسان در retry Guid غیرخالی؛ برای retry همان payload ثابت بماند. |
| `reason` | [AbuseReportReason](#type-abusereportreason)<br>null: خیر؛ required در schema: بله | دلیل enum گزارش یا متن اصلاح؛ نوع DTO تعیین‌کننده است، اصلاح غیرخالی لازم دارد از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |
| `description` | `string?`<br>null: بله؛ required در schema: بله | توضیح؛ nullable و سقف طول بین assignment/exam/report/conversation متفاوت است Assignment: غیرخالی تا۱۰۰۰۰؛ Exam: اختیاری تا۱۰۰۰۰؛ Conversation/Report: اختیاری تا۱۰۰۰. |


<a id="type-reportpage-managerassessmentreportitem-"></a>
### ReportPage&lt;ManagerAssessmentReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;ManagerAssessmentReportItem&gt;](#type-managerassessmentreportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-reportpage-managerattendancereportitem-"></a>
### ReportPage&lt;ManagerAttendanceReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;ManagerAttendanceReportItem&gt;](#type-managerattendancereportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-reportpage-managerenrollmentreportitem-"></a>
### ReportPage&lt;ManagerEnrollmentReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;ManagerEnrollmentReportItem&gt;](#type-managerenrollmentreportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-reportpage-managerteacherworkloadreportitem-"></a>
### ReportPage&lt;ManagerTeacherWorkloadReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;ManagerTeacherWorkloadReportItem&gt;](#type-managerteacherworkloadreportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-reportpage-parentassignmentreportitem-"></a>
### ReportPage&lt;ParentAssignmentReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;ParentAssignmentReportItem&gt;](#type-parentassignmentreportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-reportpage-parentexamreportitem-"></a>
### ReportPage&lt;ParentExamReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;ParentExamReportItem&gt;](#type-parentexamreportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-reportpage-studentassignmentreportitem-"></a>
### ReportPage&lt;StudentAssignmentReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;StudentAssignmentReportItem&gt;](#type-studentassignmentreportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-reportpage-studentexamreportitem-"></a>
### ReportPage&lt;StudentExamReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;StudentExamReportItem&gt;](#type-studentexamreportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-reportpage-studentreleasedgradereportitem-"></a>
### ReportPage&lt;StudentReleasedGradeReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;StudentReleasedGradeReportItem&gt;](#type-studentreleasedgradereportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-reportpage-teacherassignmentreportitem-"></a>
### ReportPage&lt;TeacherAssignmentReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;TeacherAssignmentReportItem&gt;](#type-teacherassignmentreportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-reportpage-teacherexamreportitem-"></a>
### ReportPage&lt;TeacherExamReportItem&gt;

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pageSize` | `int`<br>null: خیر؛ required در schema: بله | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `totalCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد aggregate یا نتیجه مجاز؛ totalCount جست‌وجو پیش از pagination خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `items` | [IReadOnlyCollection&lt;TeacherExamReportItem&gt;](#type-teacherexamreportitem)<br>null: خیر؛ required در schema: بله | آرایه نتایج؛ [] وضعیت خالی موفق است، با failure متفاوت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-requestotpcommand"></a>
### RequestOtpCommand

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `phoneNumber` | `string`<br>null: خیر؛ required در schema: بله | شماره حساب جاری یا ورودی OTP؛ در هویت عمومی پیام‌رسان منتشر نمی‌شود اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |


<a id="type-requestotpresult"></a>
### RequestOtpResult

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `challengeId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه challenge OTP که در verify با همان شماره استفاده می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `expiresAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | انقضای challenge/نشست/دعوت در قرارداد مربوط؛ ساعت سرور مرجع است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `resendAvailableAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | اولین زمان مجاز درخواست مجدد طبق پاسخ سرور خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-safemessagingidentityresult"></a>
### SafeMessagingIdentityResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `userId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه حساب actor؛ سرور از نشست معتبر تعیین می‌کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `username` | `string`<br>null: خیر؛ required در schema: بله | شناسه عمومی؛ profile طول ۳..۳۲ با حروف لاتین/رقم/نقطه/underscore و یکتا خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `displayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی؛ در CompleteProfile اختیاری و حداکثر ۲۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-saveevaluationdraftcommand"></a>
### SaveEvaluationDraftCommand

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست حداکثر دو رقم اعشار؛ سقف domain 9999999.99. نمره ارزیابی ۰..بارم؛ بارم طراحی >۰. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `guardianVisibleFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد مخصوص نمای والد پس از انتشار؛ تا۱۰۰۰۰ اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `teacherPrivateNote` | `string?`<br>null: بله؛ required در schema: بله | یادداشت خصوصی ارزیاب تا۱۰۰۰۰؛ در DTO عمومی/والد نمایش داده نشود اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `expectedRowVersion` | `string?`<br>null: بله؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-saveexamanswercommand"></a>
### SaveExamAnswerCommand

منبع: [ExamAnswerContracts.cs](../MakanApp.Application/Assessment/ExamAnswerContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `clientOperationId` | `Guid`<br>null: خیر؛ required در schema: بله | UUID ثابت عملیات ساخت گفتگو/شروع یا پاسخ/نهایی‌سازی/انتشار آزمون؛ scope هر عملیات در بخش۳۹ Guid غیرخالی؛ برای retry همان payload ثابت بماند. |
| `writeLeaseVersion` | `long`<br>null: خیر؛ required در schema: بله | وضعیت و نسخه مجوز نوشتن وابسته به نشست؛ deviceId نیست اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `expectedRevisionNumber` | `int?`<br>null: بله؛ required در schema: بله | null برای پاسخ اول؛ سپس شماره revision فعلی همان سؤال null فقط revision اول؛ سپس شماره جاری همان سؤال. |
| `selectedOptionId` | `Guid?`<br>null: بله؛ required در schema: بله | گزینه انتخاب‌شده از سؤال frozen؛ در پاسخ تشریحی null UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |
| `textAnswer` | `string?`<br>null: بله؛ required در schema: بله | پاسخ تشریحی تا۲۰۰۰۰؛ در objective null، در descriptive غیرnull پاسخ تشریحی تا۲۰۰۰۰؛ در objective null، در descriptive غیرnull |


<a id="type-savesubmissiondraftcommand"></a>
### SaveSubmissionDraftCommand

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `answerText` | `string?`<br>null: بله؛ required در schema: بله | متن پاسخ تکلیف؛ nullable و حداکثر۵۰۰۰۰؛ submit حداقل متن/فایل می‌خواهد متن پاسخ تکلیف؛ nullable و حداکثر۵۰۰۰۰؛ submit حداقل متن/فایل می‌خواهد |
| `expectedRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-scheduleruleresult"></a>
### ScheduleRuleResult

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `localDayOfWeek` | [DayOfWeek](#type-dayofweek)<br>null: خیر؛ required در schema: بله | روز هفته به نام English enum؛ شماره روز فارسی ارسال نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `localStartTime` | `TimeOnly`<br>null: خیر؛ required در schema: بله | ساعت محلی rule به شکل HH:mm:ss؛ با timezone همان rule تفسیر می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `durationMinutes` | `int`<br>null: خیر؛ required در schema: بله | مدت برحسب دقیقه؛ rule حداکثر۱۴۴۰، exam قانون خودش را دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `timeZoneId` | `string`<br>null: خیر؛ required در schema: بله | شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `effectiveFrom` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ شروع/پایان الگو یا آغاز اعمال اصلاح؛ DateOnly خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `effectiveUntil` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ شروع/پایان الگو یا آغاز اعمال اصلاح؛ DateOnly خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sessionTitle` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `meetingUrl` | `string?`<br>null: بله؛ required در schema: بله | لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [ScheduleRuleStatus](#type-schedulerulestatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `generatedSessionCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد رخداد تولیدشده توسط عملیات rule؛ با ظرفیت کلاس متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `rowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-selectworkspacecommand"></a>
### SelectWorkspaceCommand

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `workspaceType` | [WorkspaceType](#type-workspacetype)<br>null: خیر؛ required در schema: بله | Personal یا Organization؛ انتخاب سمت سرور ثبت می‌شود از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |
| `membershipId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه عضویت حساب در سازمان؛ از WorkspaceResult معتبر بگیرید UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |
| `role` | [OrganizationRole?](#type-organizationrole)<br>null: بله؛ required در schema: بله | نقش در context مربوط؛ OrganizationRole با ConversationParticipantRole متفاوت است از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |


<a id="type-sendmessagecommand"></a>
### SendMessageCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `clientMessageId` | `Guid`<br>null: خیر؛ required در schema: خیر | UUID ساخته کلاینت و ثابت در retry ارسال/forward همان payload Guid غیرخالی؛ برای retry همان payload ثابت بماند. |
| `kind` | [MessageKind](#type-messagekind)<br>null: خیر؛ required در schema: خیر | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |
| `text` | `string?`<br>null: بله؛ required در schema: خیر | متن پیام/گزینه/caption؛ حد و nullability متناسب نوع DTO در قواعد حوزه اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `attachmentIds` | `IReadOnlyCollection<Guid>?`<br>null: بله؛ required در schema: خیر | شناسه فایل Ready؛ بارگذاری موفق به معنی attach یا submit نیست آرایه مطابق DTO فرزند؛ یکتایی، تعلق و حدود تعداد در قواعد حوزه. |
| `replyToMessageId` | `Guid?`<br>null: بله؛ required در schema: خیر | خلاصه یا شناسه پیام مرجع همان گفتگو؛ deleted محتوا را آشکار نمی‌کند UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |
| `mentionedUserIds` | `IReadOnlyCollection<Guid>?`<br>null: بله؛ required در schema: خیر | شناسه account افراد؛ باید eligibility/عضویت معتبر داشته باشند آرایه مطابق DTO فرزند؛ یکتایی، تعلق و حدود تعداد در قواعد حوزه. |


<a id="type-sessionattendanceresult"></a>
### SessionAttendanceResult

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `sessionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه جلسه آموزشی موضوع دفتر حضور؛ با UserSession ورود متفاوت است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sessionStatus` | [SessionStatus](#type-sessionstatus)<br>null: خیر؛ required در schema: بله | وضعیت جلسه Scheduled/Cancelled/Completed خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `entries` | [IReadOnlyCollection&lt;AttendanceEntryResult&gt;](#type-attendanceentryresult)<br>null: خیر؛ required در schema: بله | آرایه ردیف‌های حضور؛ هر entry با enrollmentId معتبر مشخص می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `revisions` | [IReadOnlyCollection&lt;AttendanceRevisionResult&gt;](#type-attendancerevisionresult)<br>null: خیر؛ required در schema: بله | تاریخچه اصلاحات مجاز برای ارزیاب/مدیر همان resource خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-sessionresult"></a>
### SessionResult

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `scheduleRuleId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه rule مولد جلسه؛ برای جلسه مستقل می‌تواند null باشد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `startUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `timeZoneId` | `string`<br>null: خیر؛ required در schema: بله | شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `meetingUrl` | `string?`<br>null: بله؛ required در schema: بله | لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [SessionStatus](#type-sessionstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `cancelledAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `completedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `rowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-sessionversioncommand"></a>
### SessionVersionCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `expectedRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-startdirectconversationcommand"></a>
### StartDirectConversationCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `targetUserId` | `Guid`<br>null: خیر؛ required در schema: بله | حساب مقصد/مبدا رابطه؛ داشتن ID اثبات مجوز نیست UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |
| `scope` | [ConversationScope](#type-conversationscope)<br>null: خیر؛ required در schema: بله | Personal/Organization گفتگو؛ scope با role یکسان نیست از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |


<a id="type-startexamcommand"></a>
### StartExamCommand

منبع: [ExamAttemptContracts.cs](../MakanApp.Application/Assessment/ExamAttemptContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `clientOperationId` | `Guid`<br>null: خیر؛ required در schema: بله | UUID ثابت عملیات ساخت گفتگو/شروع یا پاسخ/نهایی‌سازی/انتشار آزمون؛ scope هر عملیات در بخش۳۹ Guid غیرخالی؛ برای retry همان payload ثابت بماند. |


<a id="type-startownershiptransfercommand"></a>
### StartOwnershipTransferCommand

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `targetUserId` | `Guid`<br>null: خیر؛ required در schema: بله | حساب مقصد/مبدا رابطه؛ داشتن ID اثبات مجوز نیست UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |


<a id="type-studentassignmentreportitem"></a>
### StudentAssignmentReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentStatus` | [AssignmentStatus](#type-assignmentstatus)<br>null: خیر؛ required در schema: بله | چرخه رسمی Assignment؛ Published و Closed در گزارش تاریخچه قابل مشاهده‌اند. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `dueAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | مهلت UTC تکلیف؛ هنگام طراحی/انتشار در آینده و هنگام submit مبنای late policy خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submissionStatus` | [SubmissionAttemptStatus?](#type-submissionattemptstatus)<br>null: بله؛ required در schema: بله | وضعیت آخرین تلاش تکلیف؛ null یعنی submission متناظر وجود ندارد. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submittedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان تحویل نهایی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isLate` | `bool?`<br>null: بله؛ required در schema: بله | وضعیت تأخیر تعیین‌شده توسط server acceptance time خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedScore` | `decimal?`<br>null: بله؛ required در schema: بله | نمره رسمی منتشرشده؛ null را صفر نمایش ندهید. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentattendancereportitem"></a>
### StudentAttendanceReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `sessionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه جلسه آموزشی؛ با نشست ورود UserSession متفاوت است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `courseTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sessionTitle` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `startUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [AttendanceStatus](#type-attendancestatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recordedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان ثبت حضور به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentcurrentexamanswerdto"></a>
### StudentCurrentExamAnswerDto

منبع: [ExamAnswerContracts.cs](../MakanApp.Application/Assessment/ExamAnswerContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptQuestionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه سؤال داخل attempt؛ questionVersionId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `questionVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `answerRevisionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه ذخیره پذیرفته‌شده پاسخ؛ رسید retry به همین revision بازمی‌گردد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `revisionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `answerType` | [ExamQuestionType](#type-examquestiontype)<br>null: خیر؛ required در schema: بله | ObjectiveSingleChoice یا Descriptive؛ شکل پاسخ باید مطابق باشد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `selectedOptionId` | `Guid?`<br>null: بله؛ required در schema: بله | گزینه انتخاب‌شده از سؤال frozen؛ در پاسخ تشریحی null خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `textAnswer` | `string?`<br>null: بله؛ required در schema: بله | پاسخ تشریحی تا۲۰۰۰۰؛ در objective null، در descriptive غیرnull خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `acceptedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان پذیرش سرور به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentdashboarddto"></a>
### StudentDashboardDto

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `audience` | [DashboardAudience](#type-dashboardaudience)<br>null: خیر؛ required در schema: بله | نقش مخاطب همان DTO؛ با activeRole تطبیق داده شود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `nextSession` | [DashboardSessionItem?](#type-dashboardsessionitem)<br>null: بله؛ required در schema: بله | نزدیک‌ترین جلسه Scheduled آینده در کلاس مجاز؛ نبود آن null است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `nearestAssignmentDeadline` | [DashboardAssignmentItem?](#type-dashboardassignmentitem)<br>null: بله؛ required در schema: بله | نزدیک‌ترین تکلیف قابل اقدام Published؛ Closed در این بخش نمی‌آید. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recentReleasedFeedback` | [IReadOnlyCollection&lt;StudentReleasedAssignmentItem&gt;](#type-studentreleasedassignmentitem)<br>null: خیر؛ required در schema: بله | حداکثر5 نتیجه تکلیف منتشرشده برای دانش‌آموز. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `upcomingExams` | [IReadOnlyCollection&lt;DashboardExamItem&gt;](#type-dashboardexamitem)<br>null: خیر؛ required در schema: بله | فهرست محدود metadata آزمون؛ فاقد سؤال/گزینه/کلید/پاسخ. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attendanceSummary` | [DashboardAttendanceSummary](#type-dashboardattendancesummary)<br>null: خیر؛ required در schema: بله | خلاصه حضور رسمی؛ notRecorded را غیبت محسوب نکنید. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `pendingAssignmentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تکلیف Published با موعد آینده، بدون submission و بدون grade؛ Closed یا گذشته حتی با allowLateSubmission در این count نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentexamanswersdto"></a>
### StudentExamAnswersDto

منبع: [ExamAnswerContracts.cs](../MakanApp.Application/Assessment/ExamAnswerContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `answerSetVersion` | `long`<br>null: خیر؛ required در schema: بله | شمارنده مجموعه پاسخ‌ها؛ finalize فقط نسخه پذیرفته‌شده جاری خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `writeLease` | [ExamWriteLeaseDto](#type-examwriteleasedto)<br>null: خیر؛ required در schema: بله | وضعیت و نسخه مجوز نوشتن وابسته به نشست؛ deviceId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `answers` | [IReadOnlyCollection&lt;StudentCurrentExamAnswerDto&gt;](#type-studentcurrentexamanswerdto)<br>null: خیر؛ required در schema: بله | فقط پاسخ‌های دارای revision پذیرفته‌شده؛ نبود ردیف یعنی هنوز revision ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentexamattemptdto"></a>
### StudentExamAttemptDto

منبع: [ExamAttemptContracts.cs](../MakanApp.Application/Assessment/ExamAttemptContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examId` | `Guid`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `startedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `effectiveDeadlineUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | حداقل پایان availability و شروع+مدت؛ در لحظه مساوی مهلت نوشتن پایان می‌یابد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `serverNowUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان authoritative سرور برای countdown و سنجش اختلاف ساعت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [ExamAttemptStatus](#type-examattemptstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `questions` | [IReadOnlyCollection&lt;StudentExamAttemptQuestionDto&gt;](#type-studentexamattemptquestiondto)<br>null: خیر؛ required در schema: بله | آرایه سؤال/گزینه همین DTO؛ مدل editor کلید دارد، student-safe ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentexamattemptoptiondto"></a>
### StudentExamAttemptOptionDto

منبع: [ExamAttemptContracts.cs](../MakanApp.Application/Assessment/ExamAttemptContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `order` | `int`<br>null: خیر؛ required در schema: بله | ترتیب مثبت؛ displayOrder تلاش ترتیب frozen است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `text` | `string`<br>null: خیر؛ required در schema: بله | متن پیام/گزینه/caption؛ حد و nullability متناسب نوع DTO در قواعد حوزه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentexamattemptquestiondto"></a>
### StudentExamAttemptQuestionDto

منبع: [ExamAttemptContracts.cs](../MakanApp.Application/Assessment/ExamAttemptContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `attemptQuestionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه سؤال داخل attempt؛ questionVersionId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `questionVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `displayOrder` | `int`<br>null: خیر؛ required در schema: بله | ترتیب مثبت؛ displayOrder تلاش ترتیب frozen است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `questionType` | [ExamQuestionType](#type-examquestiontype)<br>null: خیر؛ required در schema: بله | ObjectiveSingleChoice یا Descriptive؛ شکل پاسخ باید مطابق باشد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `prompt` | `string`<br>null: خیر؛ required در schema: بله | متن سؤال غیرخالی تا۲۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `options` | [IReadOnlyCollection&lt;StudentExamAttemptOptionDto&gt;](#type-studentexamattemptoptiondto)<br>null: خیر؛ required در schema: بله | آرایه سؤال/گزینه همین DTO؛ مدل editor کلید دارد، student-safe ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentexamattemptsummarydto"></a>
### StudentExamAttemptSummaryDto

منبع: [ExamAttemptContracts.cs](../MakanApp.Application/Assessment/ExamAttemptContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `startedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `effectiveDeadlineUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | حداقل پایان availability و شروع+مدت؛ در لحظه مساوی مهلت نوشتن پایان می‌یابد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `serverNowUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان authoritative سرور برای countdown و سنجش اختلاف ساعت خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [ExamAttemptStatus](#type-examattemptstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentexamreportitem"></a>
### StudentExamReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examId` | `Guid`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `availableFromUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `availableUntilUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `durationMinutes` | `int`<br>null: خیر؛ required در schema: بله | مدت برحسب دقیقه؛ rule حداکثر۱۴۴۰، exam قانون خودش را دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumAttempts` | `int`<br>null: خیر؛ required در schema: بله | سقف تلاش از نسخه آزمون؛ در کلاینت ثابت فرض نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تلاش ثبت‌شده آزمون در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `latestAttemptStatus` | [ExamAttemptStatus?](#type-examattemptstatus)<br>null: بله؛ required در schema: بله | وضعیت آخرین تلاش آزمون در محدوده مجاز؛ null در نبود تلاش. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `latestStartedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `latestFinalizedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedScore` | `decimal?`<br>null: بله؛ required در schema: بله | نمره رسمی منتشرشده؛ null را صفر نمایش ندهید. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentexamresultdto"></a>
### StudentExamResultDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examId` | `Guid`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `finalizedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان نهایی‌سازی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releaseStatus` | [ExamResultReleaseStatus](#type-examresultreleasestatus)<br>null: خیر؛ required در schema: بله | AwaitingGrading/AwaitingRelease/Released؛ score=null پیش از انتشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `gradeRevisionNumber` | `int?`<br>null: بله؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal?`<br>null: بله؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal?`<br>null: بله؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentexamrules"></a>
### StudentExamRules

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `availableFromUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `availableUntilUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `durationMinutes` | `int`<br>null: خیر؛ required در schema: بله | مدت برحسب دقیقه؛ rule حداکثر۱۴۴۰، exam قانون خودش را دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxAttempts` | `int`<br>null: خیر؛ required در schema: بله | حد مثبت تلاش مجاز؛ تعداد تلاش با retry یک عملیات متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `randomizationPolicy` | [ExamRandomizationPolicy](#type-examrandomizationpolicy)<br>null: خیر؛ required در schema: بله | None یا QuestionOrder؛ ترتیب frozen از attempt خوانده شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentexamsummary"></a>
### StudentExamSummary

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `versionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `versionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `description` | `string?`<br>null: بله؛ required در schema: بله | توضیح؛ nullable و سقف طول بین assignment/exam/report/conversation متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `publicationState` | [ExamVersionStatus](#type-examversionstatus)<br>null: خیر؛ required در schema: بله | Draft/Published نسخه؛ با وضعیت attempt و نمره متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `rules` | [StudentExamRules](#type-studentexamrules)<br>null: خیر؛ required در schema: بله | قواعد آزمون در StudentExamSummary؛ از نسخه برگشتی استفاده شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentreleasedassignmentitem"></a>
### StudentReleasedAssignmentItem

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `submissionAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentTitle` | `string`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentreleasedgradereportitem"></a>
### StudentReleasedGradeReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `kind` | [DetailedReportGradeKind](#type-detailedreportgradekind)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assessmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه Assignment یا Exam مطابق kind؛ نوع منبع را همراه شناسه نگه دارید. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `percentage` | `decimal`<br>null: خیر؛ required در schema: بله | درصد score/maximumScore ضربدر100؛ سقف نمره سراسری20 فرض نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentreleasedresult"></a>
### StudentReleasedResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `submissionAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `evaluationRevisionNumber` | `int`<br>null: خیر؛ required در schema: بله | شماره نسخه دامنه برای نمایش/کنترل مربوط؛ جایگزین rowversion SQL نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerFeedback` | `string?`<br>null: بله؛ required در schema: بله | بازخورد قابل مشاهده دانش‌آموز پس از انتشار؛ تا۱۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان انتشار نمره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentsafeexamoption"></a>
### StudentSafeExamOption

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `order` | `int`<br>null: خیر؛ required در schema: بله | ترتیب مثبت؛ displayOrder تلاش ترتیب frozen است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `text` | `string`<br>null: خیر؛ required در schema: بله | متن پیام/گزینه/caption؛ حد و nullability متناسب نوع DTO در قواعد حوزه خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentsafeexampreview"></a>
### StudentSafeExamPreview

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `exam` | [StudentExamSummary](#type-studentexamsummary)<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `questions` | [IReadOnlyCollection&lt;StudentSafeExamQuestion&gt;](#type-studentsafeexamquestion)<br>null: خیر؛ required در schema: بله | آرایه سؤال/گزینه همین DTO؛ مدل editor کلید دارد، student-safe ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-studentsafeexamquestion"></a>
### StudentSafeExamQuestion

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `order` | `int`<br>null: خیر؛ required در schema: بله | ترتیب مثبت؛ displayOrder تلاش ترتیب frozen است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `type` | [ExamQuestionType](#type-examquestiontype)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `prompt` | `string`<br>null: خیر؛ required در schema: بله | متن سؤال غیرخالی تا۲۰۰۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `options` | [IReadOnlyCollection&lt;StudentSafeExamOption&gt;](#type-studentsafeexamoption)<br>null: خیر؛ required در schema: بله | آرایه سؤال/گزینه همین DTO؛ مدل editor کلید دارد، student-safe ندارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-submissionattachmentresult"></a>
### SubmissionAttachmentResult

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `fileAssetId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه فایل Ready؛ بارگذاری موفق به معنی attach یا submit نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `originalFileName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی امن فایل؛ مسیر ذخیره‌سازی سرور نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `contentType` | `string`<br>null: خیر؛ required در schema: بله | MIME تأییدشده فایل؛ Content-Type دلخواه کلاینت قابل اعتماد نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sizeBytes` | `long`<br>null: خیر؛ required در schema: بله | اندازه برحسب بایت؛ حد upload پایه10485760 خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `fileStatus` | [FileAssetStatus](#type-fileassetstatus)<br>null: خیر؛ required در schema: بله | وضعیت FileAsset مرتبط؛ برای attach باید Ready باشد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attachedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-submissionattemptresult"></a>
### SubmissionAttemptResult

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentRecipientId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه گیرنده snapshot انتشار تکلیف؛ براساس enrollment زمان publish خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `enrollmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه ثبت‌نام؛ با learnerOrganizationPersonId متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [SubmissionAttemptStatus](#type-submissionattemptstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `contentVisible` | `bool`<br>null: خیر؛ required در schema: بله | اجازه نمایش محتوای attempt؛ false را با cache خصوصی دور نزنید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `answerText` | `string?`<br>null: بله؛ required در schema: بله | متن پاسخ تکلیف؛ nullable و حداکثر۵۰۰۰۰؛ submit حداقل متن/فایل می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attachments` | [IReadOnlyCollection&lt;SubmissionAttachmentResult&gt;](#type-submissionattachmentresult)<br>null: خیر؛ required در schema: بله | پیوست‌های مجاز resource؛ download همچنان authorization مستقل دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lastSavedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان آخرین ذخیره به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submittedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان تحویل نهایی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isLate` | `bool`<br>null: خیر؛ required در schema: بله | وضعیت تأخیر تعیین‌شده توسط server acceptance time خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `rowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-submissionforevaluationresult"></a>
### SubmissionForEvaluationResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `submissionAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentTitle` | `string`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `studentDisplayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی دانش‌آموز در صف/ارزیابی مجاز؛ مجوز دسترسی به پرونده عمومی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submittedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان تحویل نهایی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isLate` | `bool`<br>null: خیر؛ required در schema: بله | وضعیت تأخیر تعیین‌شده توسط server acceptance time خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `answerText` | `string?`<br>null: بله؛ required در schema: بله | متن پاسخ تکلیف؛ nullable و حداکثر۵۰۰۰۰؛ submit حداقل متن/فایل می‌خواهد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attachments` | [IReadOnlyCollection&lt;SubmissionAttachmentResult&gt;](#type-submissionattachmentresult)<br>null: خیر؛ required در schema: بله | پیوست‌های مجاز resource؛ download همچنان authorization مستقل دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-submissionreceipt"></a>
### SubmissionReceipt

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `submissionAttemptId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptNumber` | `int`<br>null: خیر؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submittedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان تحویل نهایی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isLate` | `bool`<br>null: خیر؛ required در schema: بله | وضعیت تأخیر تعیین‌شده توسط server acceptance time خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [SubmissionAttemptStatus](#type-submissionattemptstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `rowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-teacherassignmentreportitem"></a>
### TeacherAssignmentReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `assignmentId` | `Guid`<br>null: خیر؛ required در schema: بله | خود مدل تکلیف یا شناسه/عنوان آن؛ نسخه و گیرنده جدا هستند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه تثبیت‌شده؛ با شمارنده versionNumber یکی نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `dueAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | مهلت UTC تکلیف؛ هنگام طراحی/انتشار در آینده و هنگام submit مبنای late policy خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `enrollmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه ثبت‌نام؛ با learnerOrganizationPersonId متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerDisplayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی فراگیر مجاز؛ هویت با learnerOrganizationPersonId مشخص می‌شود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submissionAttemptId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه تلاش پاسخ یا شماره آن؛ retry همان تلاش را نگه دارد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submissionStatus` | [SubmissionAttemptStatus?](#type-submissionattemptstatus)<br>null: بله؛ required در schema: بله | وضعیت آخرین تلاش تکلیف؛ null یعنی submission متناظر وجود ندارد. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submittedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان تحویل نهایی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isLate` | `bool?`<br>null: بله؛ required در schema: بله | وضعیت تأخیر تعیین‌شده توسط server acceptance time خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `currentScore` | `decimal?`<br>null: بله؛ required در schema: بله | نمره جاری در گزارش کارکنان؛ ممکن است پیش‌نویس و هنوز برای Student/Parent نامرئی باشد. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `evaluationStatus` | [EvaluationRevisionStatus?](#type-evaluationrevisionstatus)<br>null: بله؛ required در schema: بله | وضعیت revision ارزیابی برای گزارش کارکنان؛ ممکن است هنوز منتشر نشده باشد. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isReleased` | `bool`<br>null: خیر؛ required در schema: بله | وجود انتشار رسمی نتیجه این ردیف؛ مجوز عمومی خواندن اطلاعات خصوصی نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-teacherassignmentresult"></a>
### TeacherAssignmentResult

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `id` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه resource موضوع همین DTO؛ با شناسه نوع دیگر جایگزین نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `teacherMembershipId` | `Guid`<br>null: خیر؛ required در schema: بله | عضویت فعال دارای Teacher در همان سازمان؛ نه UserId معلم خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [TeacherAssignmentStatus](#type-teacherassignmentstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignedAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان رویداد نام‌برده در این فیلد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان پایان رابطه به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-teacherattendancereportitem"></a>
### TeacherAttendanceReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `sessionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه جلسه آموزشی؛ با نشست ورود UserSession متفاوت است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `sessionTitle` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `startUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `endUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `enrollmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه ثبت‌نام؛ با learnerOrganizationPersonId متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerDisplayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی فراگیر مجاز؛ هویت با learnerOrganizationPersonId مشخص می‌شود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [AttendanceStatus](#type-attendancestatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `recordedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان ثبت حضور به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-teacherdashboarddto"></a>
### TeacherDashboardDto

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `audience` | [DashboardAudience](#type-dashboardaudience)<br>null: خیر؛ required در schema: بله | نقش مخاطب همان DTO؛ با activeRole تطبیق داده شود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignedClasses` | [IReadOnlyCollection&lt;DashboardClassItem&gt;](#type-dashboardclassitem)<br>null: خیر؛ required در schema: بله | حداکثر20 کلاس فعال دارای انتساب فعال Teacher؛ endpoint فهرست کامل کلاس‌ها جداست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `upcomingSessions` | [IReadOnlyCollection&lt;DashboardSessionItem&gt;](#type-dashboardsessionitem)<br>null: خیر؛ required در schema: بله | حداکثر10 جلسه برنامه‌ریزی‌شده از داده مجاز نقش. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attendancePending` | [IReadOnlyCollection&lt;DashboardAttendancePendingItem&gt;](#type-dashboardattendancependingitem)<br>null: خیر؛ required در schema: بله | جلسه‌های پایان‌یافته با ثبت حضور ناقص؛ هر ردیف missingCount دارد. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentGradingQueue` | [AssignmentGradingQueueSummary](#type-assignmentgradingqueuesummary)<br>null: خیر؛ required در schema: بله | تعداد پاسخ‌های در انتظار ارزیابی و ارزیابی پیش‌نویس؛ متن پاسخ در این DTO نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examGradingQueue` | [ExamGradingQueueSummary](#type-examgradingqueuesummary)<br>null: خیر؛ required در schema: بله | تعداد تلاش‌های در انتظار تصحیح، بازبینی دستی یا انتشار؛ بدون پاسخ خصوصی. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-teacherexamreportitem"></a>
### TeacherExamReportItem

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `examId` | `Guid`<br>null: خیر؛ required در schema: بله | مدل خلاصه یا شناسه/عنوان آزمون؛ attempt و version مستقل‌اند خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examVersionId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه نسخه frozen آزمون/سؤال در تلاش؛ از پاسخ سرور بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `availableUntilUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `maximumScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `enrollmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه ثبت‌نام؛ با learnerOrganizationPersonId متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerDisplayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی فراگیر مجاز؛ هویت با learnerOrganizationPersonId مشخص می‌شود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examAttemptId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه تلاش آزمون متعلق به actor/child مجاز خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `attemptStatus` | [ExamAttemptStatus?](#type-examattemptstatus)<br>null: بله؛ required در schema: بله | وضعیت تلاش آزمون این ردیف؛ null برای فراگیر فاقد تلاش. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `finalizedAtUtc` | `DateTime?`<br>null: بله؛ required در schema: بله | زمان نهایی‌سازی به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `currentScore` | `decimal?`<br>null: بله؛ required در schema: بله | نمره جاری در گزارش کارکنان؛ ممکن است پیش‌نویس و هنوز برای Student/Parent نامرئی باشد. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `gradeStatus` | [ExamGradeRevisionStatus?](#type-examgraderevisionstatus)<br>null: بله؛ required در schema: بله | وضعیت revision تصحیح آزمون برای کارکنان؛ با انتشار رسمی متفاوت است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `isReleased` | `bool`<br>null: خیر؛ required در schema: بله | وجود انتشار رسمی نتیجه این ردیف؛ مجوز عمومی خواندن اطلاعات خصوصی نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-teacherlearneroverviewdto"></a>
### TeacherLearnerOverviewDto

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `dataAsOfUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان شروع محاسبه در سرور به UTC؛ snapshot اتمیک تمام queryها نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classId` | `Guid`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `classTitle` | `string`<br>null: خیر؛ required در schema: بله | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `enrollmentId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه ثبت‌نام؛ با learnerOrganizationPersonId متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerOrganizationPersonId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه پرونده سازمانی دانش‌آموز/فرزند؛ UserId نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `learnerDisplayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی فراگیر مجاز؛ هویت با learnerOrganizationPersonId مشخص می‌شود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `eligibleSessionCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد جلسه واجد شرایط حضور در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `presentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد حاضر در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `absentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد غایب ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `lateCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تأخیر ثبت‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `excusedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد غیبت موجه در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `notRecordedCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد فاقد رکورد حضور رسمی در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `assignmentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تکلیف در محدوده گزارش در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `submittedAssignmentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تکلیف تحویل‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedAssignmentCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد نتیجه تکلیف منتشرشده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `examCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد آزمون در محدوده گزارش در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `finalizedExamAttemptCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد تلاش آزمون نهایی‌شده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `releasedExamCount` | `int`<br>null: خیر؛ required در schema: بله | تعداد نتیجه آزمون منتشرشده در scope مجاز؛ شمارنده سرور مرجع است. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-teacherworkloadreportquery"></a>
### TeacherWorkloadReportQuery

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

مدل query با propertyهای ارث‌رسیده؛ بدنه JSON نیست.

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `pageNumber` | `int`<br>null: خیر؛ query اختیاری با default مدل | شماره صفحه از1؛ در query پیش‌فرض1؛ صفر و منفی نامعتبر. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `pageSize` | `int`<br>null: خیر؛ query اختیاری با default مدل | اندازه صفحه؛ پیش‌فرض20؛ بازه مجاز1 تا100. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `fromUtc` | `DateTime?`<br>null: بله؛ query اختیاری با default مدل | ابتدای بازه inclusive به UTC؛ فیلتر زمان رویداد طبق نوع گزارش، نه همیشه زمان ایجاد. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `toUtc` | `DateTime?`<br>null: بله؛ query اختیاری با default مدل | انتهای بازه inclusive به UTC؛ نباید قبل fromUtc باشد. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `classId` | `Guid?`<br>null: بله؛ query اختیاری با default مدل | مرجع کلاس و نام آن؛ دسترسی وابسته به رابطه جاری همان کلاس پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `sort` | `string?`<br>null: بله؛ query اختیاری با default مدل | مرتب‌سازی allowlist همان endpoint؛ اختیاری و case-insensitive پس از trim؛ جدول بخش14. پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `courseId` | `Guid?`<br>null: بله؛ query اختیاری با default مدل | مرجع درس سازمان و عنوان آن؛ Course به AcademicPeriod مستقیم وابسته نیست پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |
| `academicPeriodId` | `Guid?`<br>null: بله؛ query اختیاری با default مدل | مرجع دوره کلاس و عنوان آن؛ دوره و درس دو مرجع موازی Class هستند پارامتر query؛ حذف مقدار از URL از null متنی متفاوت است. پیش‌فرض و فیلتر قابل‌اعمال در بخش14 و کارت API. |


<a id="type-updateassignmentdraftcommand"></a>
### UpdateAssignmentDraftCommand

منبع: [AssignmentContracts.cs](../MakanApp.Application/Assessment/AssignmentContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API غیرخالی پس از trim؛ تا۱۶۰ در گفتگو و تا۲۰۰ در آموزش/ارزیابی. |
| `description` | `string`<br>null: خیر؛ required در schema: بله | توضیح؛ nullable و سقف طول بین assignment/exam/report/conversation متفاوت است Assignment: غیرخالی تا۱۰۰۰۰؛ Exam: اختیاری تا۱۰۰۰۰؛ Conversation/Report: اختیاری تا۱۰۰۰. |
| `dueAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | مهلت UTC تکلیف؛ هنگام طراحی/انتشار در آینده و هنگام submit مبنای late policy ISO 8601 با Z؛ قواعد بازه/مهلت در کارت endpoint. |
| `allowLateSubmission` | `bool`<br>null: خیر؛ required در schema: بله | اجازه تحویل پس از مهلت؛ isLate همچنان توسط سرور تعیین می‌شود اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `maxAttempts` | `int`<br>null: خیر؛ required در schema: بله | حد مثبت تلاش مجاز؛ تعداد تلاش با retry یک عملیات متفاوت است اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار حداکثر دو رقم اعشار؛ سقف domain 9999999.99. نمره ارزیابی ۰..بارم؛ بارم طراحی >۰. |
| `expectedAssignmentRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |
| `expectedVersionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-updateexamdraftcommand"></a>
### UpdateExamDraftCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API غیرخالی پس از trim؛ تا۱۶۰ در گفتگو و تا۲۰۰ در آموزش/ارزیابی. |
| `description` | `string?`<br>null: بله؛ required در schema: بله | توضیح؛ nullable و سقف طول بین assignment/exam/report/conversation متفاوت است Assignment: غیرخالی تا۱۰۰۰۰؛ Exam: اختیاری تا۱۰۰۰۰؛ Conversation/Report: اختیاری تا۱۰۰۰. |
| `availableFromUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد ISO 8601 با Z؛ قواعد بازه/مهلت در کارت endpoint. |
| `availableUntilUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | پنجره شروع/دسترسی آزمون UTC؛ server clock تصمیم می‌گیرد ISO 8601 با Z؛ قواعد بازه/مهلت در کارت endpoint. |
| `durationMinutes` | `int`<br>null: خیر؛ required در schema: بله | مدت برحسب دقیقه؛ rule حداکثر۱۴۴۰، exam قانون خودش را دارد اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `maxAttempts` | `int`<br>null: خیر؛ required در schema: بله | حد مثبت تلاش مجاز؛ تعداد تلاش با retry یک عملیات متفاوت است اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `maxScore` | `decimal`<br>null: خیر؛ required در schema: بله | سقف نمره؛ عدد مثبت حداکثر9999999.99 با دو رقم اعشار حداکثر دو رقم اعشار؛ سقف domain 9999999.99. نمره ارزیابی ۰..بارم؛ بارم طراحی >۰. |
| `randomizationPolicy` | [ExamRandomizationPolicy](#type-examrandomizationpolicy)<br>null: خیر؛ required در schema: بله | None یا QuestionOrder؛ ترتیب frozen از attempt خوانده شود از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |
| `expectedVersionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-updateexamquestioncommand"></a>
### UpdateExamQuestionCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `order` | `int`<br>null: خیر؛ required در schema: بله | ترتیب مثبت؛ displayOrder تلاش ترتیب frozen است اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `type` | [ExamQuestionType](#type-examquestiontype)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |
| `prompt` | `string`<br>null: خیر؛ required در schema: بله | متن سؤال غیرخالی تا۲۰۰۰۰ متن سؤال غیرخالی تا۲۰۰۰۰ |
| `score` | `decimal`<br>null: خیر؛ required در schema: بله | نمره/بارم متناسب DTO؛ score نتیجه nullable پیش از انتشار آزمون، null صفر نیست حداکثر دو رقم اعشار؛ سقف domain 9999999.99. نمره ارزیابی ۰..بارم؛ بارم طراحی >۰. |
| `options` | [IReadOnlyCollection&lt;ExamQuestionOptionCommand&gt;?](#type-examquestionoptioncommand)<br>null: بله؛ required در schema: بله | آرایه سؤال/گزینه همین DTO؛ مدل editor کلید دارد، student-safe ندارد آرایه مطابق DTO فرزند؛ یکتایی، تعلق و حدود تعداد در قواعد حوزه. |
| `expectedVersionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |
| `expectedQuestionRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-updateschedulerulecommand"></a>
### UpdateScheduleRuleCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `localDayOfWeek` | [DayOfWeek](#type-dayofweek)<br>null: خیر؛ required در schema: بله | روز هفته به نام English enum؛ شماره روز فارسی ارسال نشود از enum مرتبط؛ محدودیت subset عملیات در کارت API (مثل finalStatus) اعمال می‌شود. |
| `localStartTime` | `TimeOnly`<br>null: خیر؛ required در schema: بله | ساعت محلی rule به شکل HH:mm:ss؛ با timezone همان rule تفسیر می‌شود HH:mm:ss؛ همراه timezone برای الگوی تکرار. |
| `durationMinutes` | `int`<br>null: خیر؛ required در schema: بله | مدت برحسب دقیقه؛ rule حداکثر۱۴۴۰، exam قانون خودش را دارد اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `timeZoneId` | `string`<br>null: خیر؛ required در schema: بله | شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran |
| `effectiveFrom` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ شروع/پایان الگو یا آغاز اعمال اصلاح؛ DateOnly YYYY-MM-DD؛ اعتبار بازه در domain. |
| `effectiveUntil` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ شروع/پایان الگو یا آغاز اعمال اصلاح؛ DateOnly YYYY-MM-DD؛ اعتبار بازه در domain. |
| `applyFromDate` | `DateOnly`<br>null: خیر؛ required در schema: بله | تاریخ شروع/پایان الگو یا آغاز اعمال اصلاح؛ DateOnly YYYY-MM-DD؛ اعتبار بازه در domain. |
| `sessionTitle` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `meetingUrl` | `string?`<br>null: بله؛ required در schema: بله | لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند |
| `expectedRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-updatesessioncommand"></a>
### UpdateSessionCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `title` | `string`<br>null: خیر؛ required در schema: بله | عنوان نمایشی؛ قواعد طول و الزامی‌بودن متناسب domain در کارت API غیرخالی پس از trim؛ تا۱۶۰ در گفتگو و تا۲۰۰ در آموزش/ارزیابی. |
| `startUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع ISO 8601 با Z؛ قواعد بازه/مهلت در کارت endpoint. |
| `endUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان واقعی جلسه با UTC و Z؛ پایان بعد از شروع ISO 8601 با Z؛ قواعد بازه/مهلت در کارت endpoint. |
| `timeZoneId` | `string`<br>null: خیر؛ required در schema: بله | شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran شناسه timezone قابل resolve در سرور، حداکثر ۱۰۰؛ مثال Asia/Tehran |
| `meetingUrl` | `string?`<br>null: بله؛ required در schema: بله | لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند لینک اختیاری HTTP/HTTPS تا۱۰۰۰؛ بازکردن آن حضور یا admission ایجاد نمی‌کند |
| `expectedRowVersion` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. نسخه جاری resource؛ Base64 هشت‌بایتی. nullable اولیه در SaveEvaluationDraftCommand مجاز است. |


<a id="type-uploadfilerequest"></a>
### UploadFileRequest

منبع: [FilesController.cs](../MakanApp.Api/Controllers/FilesController.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `File` | `IFormFile`<br>null: خیر؛ multipart File الزامی | بخش باینری multipart؛ نام field برابر File و Content-Type متناسب فایل اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |


<a id="type-userblockresult"></a>
### UserBlockResult

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `blockId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه رابطه Block خود؛ رفع Block طرف مقابل از این API ممکن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `blockedUser` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult)<br>null: خیر؛ required در schema: بله | هویت امن پیام‌رسان؛ فقط userId/username/displayName خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | [UserBlockStatus](#type-userblockstatus)<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `createdAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | زمان ایجاد به UTC؛ مقدار سرور مرجع است و null یعنی رویداد هنوز ثبت نشده/قابل ارائه نیست. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `alreadyExisted` | `bool`<br>null: خیر؛ required در schema: بله | علامت reuse عملیات موجود؛ از HTTP status به‌تنهایی استنباط نشود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `version` | `string`<br>null: خیر؛ required در schema: بله | نسخه opaque هشت‌بایتی Base64 همان resource؛ از آخرین پاسخ بگیرید، تولید یا increment نشود. خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-verifyotpcommand"></a>
### VerifyOtpCommand

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `challengeId` | `Guid`<br>null: خیر؛ required در schema: بله | شناسه challenge OTP که در verify با همان شماره استفاده می‌شود UUID معتبر و مرجع موجود/مجاز؛ مقدار انتخاب‌گر است، اثبات مجوز نیست. |
| `phoneNumber` | `string`<br>null: خیر؛ required در schema: بله | شماره حساب جاری یا ورودی OTP؛ در هویت عمومی پیام‌رسان منتشر نمی‌شود اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |
| `code` | `string`<br>null: خیر؛ required در schema: بله | کد دریافتی همان challenge؛ در log/cache عمومی یا نمونه ثابت ذخیره نشود اعتبارسنجی ترکیبی در کارت endpoint و فصل حوزه؛ نبود annotation به معنی پذیرش هر مقدار نیست. |


<a id="type-verifyotpresult"></a>
### VerifyOtpResult

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `accessToken` | `string`<br>null: خیر؛ required در schema: بله | توکن opaque نشست؛ credential محرمانه، فقط در مخزن امن محیط نگهداری شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `tokenType` | `string`<br>null: خیر؛ required در schema: بله | در پاسخ ورود Bearer؛ برای header Authorization خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `expiresAtUtc` | `DateTime`<br>null: خیر؛ required در schema: بله | انقضای challenge/نشست/دعوت در قرارداد مربوط؛ ساعت سرور مرجع است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `user` | [CurrentUserResult](#type-currentuserresult)<br>null: خیر؛ required در schema: بله | هویت امن پیام‌رسان؛ فقط userId/username/displayName خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="type-workspaceresult"></a>
### WorkspaceResult

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع و حضور | معنی و قاعده |
| --- | --- | --- |
| `workspaceType` | [WorkspaceType](#type-workspacetype)<br>null: خیر؛ required در schema: بله | Personal یا Organization؛ انتخاب سمت سرور ثبت می‌شود خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `organizationName` | `string?`<br>null: بله؛ required در schema: بله | شناسه/نام سازمان؛ مقدار nullable یعنی scope شخصی در قرارداد مربوط خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `membershipId` | `Guid?`<br>null: بله؛ required در schema: بله | شناسه عضویت حساب در سازمان؛ از WorkspaceResult معتبر بگیرید خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `role` | [OrganizationRole?](#type-organizationrole)<br>null: بله؛ required در schema: بله | نقش در context مربوط؛ OrganizationRole با ConversationParticipantRole متفاوت است خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `displayName` | `string`<br>null: خیر؛ required در schema: بله | نام نمایشی؛ در CompleteProfile اختیاری و حداکثر ۲۰۰ خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |
| `status` | `string`<br>null: خیر؛ required در schema: بله | وضعیت/نوع واقعی DTO؛ enum مرتبط را ببینید؛ وضعیت UI محلی جایگزین آن نیست خروجی سرور؛ فقط طبق نوع و nullability مصرف شود. |


<a id="enums"></a>
## مقادیر Enum

JSON از نام string enum استفاده می‌کند. جدول زیر عضوهای تعریف‌شده را نشان می‌دهد؛ subset مجاز هر عملیات و انتقال وضعیت را از service/Domain بررسی کنید.

<a id="type-abusereportreason"></a>
### AbuseReportReason

منبع: [AbuseReportReason.cs](../MakanApp.Domain/Messaging/AbuseReportReason.cs).

`Harassment`، `Spam`، `Threat`، `InappropriateContent`، `Impersonation`، `Other`

<a id="type-abusereportstatus"></a>
### AbuseReportStatus

منبع: [AbuseReportStatus.cs](../MakanApp.Domain/Messaging/AbuseReportStatus.cs).

`Submitted`، `UnderReview`، `Resolved`، `Dismissed`

<a id="type-academicperiodstatus"></a>
### AcademicPeriodStatus

منبع: [AcademicPeriodStatus.cs](../MakanApp.Domain/Academic/AcademicPeriodStatus.cs).

`Active`، `Closed`

<a id="type-assignmentstatus"></a>
### AssignmentStatus

منبع: [AssignmentStatus.cs](../MakanApp.Domain/Assessment/AssignmentStatus.cs).

`Draft`، `Published`، `Closed`، `Archived`

<a id="type-attendancestatus"></a>
### AttendanceStatus

منبع: [AttendanceStatus.cs](../MakanApp.Domain/Academic/AttendanceStatus.cs).

`NotRecorded`، `Present`، `Absent`، `Late`، `Excused`

<a id="type-classstatus"></a>
### ClassStatus

منبع: [ClassStatus.cs](../MakanApp.Domain/Academic/ClassStatus.cs).

`Draft`، `Active`، `Completed`، `Archived`

<a id="type-conversationmanagementpolicy"></a>
### ConversationManagementPolicy

منبع: [ConversationManagementPolicy.cs](../MakanApp.Domain/Messaging/ConversationManagementPolicy.cs).

`None`، `UserManaged`، `SystemManagedAcademic`

<a id="type-conversationparticipantrole"></a>
### ConversationParticipantRole

منبع: [ConversationParticipantRole.cs](../MakanApp.Domain/Messaging/ConversationParticipantRole.cs).

`Owner`، `Admin`، `Member`

<a id="type-conversationparticipantstatus"></a>
### ConversationParticipantStatus

منبع: [ConversationParticipantStatus.cs](../MakanApp.Domain/Messaging/ConversationParticipantStatus.cs).

`Active`، `Removed`، `Left`

<a id="type-conversationscope"></a>
### ConversationScope

منبع: [ConversationScope.cs](../MakanApp.Domain/Messaging/ConversationScope.cs).

`Personal`، `Organization`

<a id="type-conversationstatus"></a>
### ConversationStatus

منبع: [ConversationStatus.cs](../MakanApp.Domain/Messaging/ConversationStatus.cs).

`Active`، `Archived`

<a id="type-conversationtype"></a>
### ConversationType

منبع: [ConversationType.cs](../MakanApp.Domain/Messaging/ConversationType.cs).

`Direct`، `Group`، `Channel`

<a id="type-coursestatus"></a>
### CourseStatus

منبع: [CourseStatus.cs](../MakanApp.Domain/Academic/CourseStatus.cs).

`Active`، `Archived`

<a id="type-dashboardaudience"></a>
### DashboardAudience

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

`Student`، `Parent`، `Teacher`، `Manager`

<a id="type-dayofweek"></a>
### DayOfWeek

منبع: enum استاندارد System.DayOfWeek در .NET؛ Sunday=0 تا Saturday=6.

`Sunday`، `Monday`، `Tuesday`، `Wednesday`، `Thursday`، `Friday`، `Saturday`

<a id="type-detailedreportassessmentkind"></a>
### DetailedReportAssessmentKind

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

`Assignment`، `Exam`

<a id="type-detailedreportgradekind"></a>
### DetailedReportGradeKind

منبع: [DetailedReportContracts.cs](../MakanApp.Application/Reports/Detailed/DetailedReportContracts.cs).

`Assignment`، `Exam`

<a id="type-enrollmentstatus"></a>
### EnrollmentStatus

منبع: [EnrollmentStatus.cs](../MakanApp.Domain/Academic/EnrollmentStatus.cs).

`Active`، `Completed`، `Withdrawn`

<a id="type-evaluationreviewstatus"></a>
### EvaluationReviewStatus

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

`AwaitingEvaluation`، `Draft`، `Released`، `CorrectionDraft`

<a id="type-evaluationrevisionstatus"></a>
### EvaluationRevisionStatus

منبع: [EvaluationRevisionStatus.cs](../MakanApp.Domain/Assessment/EvaluationRevisionStatus.cs).

`Draft`، `Released`، `Superseded`

<a id="type-examattemptstatus"></a>
### ExamAttemptStatus

منبع: [ExamAttemptStatus.cs](../MakanApp.Domain/Assessment/ExamAttemptStatus.cs).

`InProgress`، `Expired`، `Finalized`

<a id="type-examgraderevisionstatus"></a>
### ExamGradeRevisionStatus

منبع: [ExamGradeRevisionStatus.cs](../MakanApp.Domain/Assessment/ExamGradeRevisionStatus.cs).

`Draft`، `ReadyForRelease`، `Released`، `Superseded`

<a id="type-examgradingqueuestatus"></a>
### ExamGradingQueueStatus

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

`AwaitingGrading`، `Draft`، `ReadyForRelease`، `Released`، `CorrectionDraft`

<a id="type-examquestiongradingmode"></a>
### ExamQuestionGradingMode

منبع: [ExamQuestionGradingMode.cs](../MakanApp.Domain/Assessment/ExamQuestionGradingMode.cs).

`Objective`، `Manual`

<a id="type-examquestiontype"></a>
### ExamQuestionType

منبع: [ExamQuestionType.cs](../MakanApp.Domain/Assessment/ExamQuestionType.cs).

`ObjectiveSingleChoice`، `Descriptive`

<a id="type-examrandomizationpolicy"></a>
### ExamRandomizationPolicy

منبع: [ExamRandomizationPolicy.cs](../MakanApp.Domain/Assessment/ExamRandomizationPolicy.cs).

`None`، `QuestionOrder`

<a id="type-examresultreleasestatus"></a>
### ExamResultReleaseStatus

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

`AwaitingGrading`، `AwaitingRelease`، `Released`

<a id="type-examstatus"></a>
### ExamStatus

منبع: [ExamStatus.cs](../MakanApp.Domain/Assessment/ExamStatus.cs).

`Draft`، `Published`

<a id="type-examversionstatus"></a>
### ExamVersionStatus

منبع: [ExamVersionStatus.cs](../MakanApp.Domain/Assessment/ExamVersionStatus.cs).

`Draft`، `Published`

<a id="type-fileassetstatus"></a>
### FileAssetStatus

منبع: [FileAssetStatus.cs](../MakanApp.Domain/Storage/FileAssetStatus.cs).

`Pending`، `Ready`، `Rejected`، `Deleted`

<a id="type-guardianrelationstatus"></a>
### GuardianRelationStatus

منبع: [GuardianRelationStatus.cs](../MakanApp.Domain/Guardian/GuardianRelationStatus.cs).

`Pending`، `Active`، `Ended`، `Revoked`

<a id="type-invitationstatus"></a>
### InvitationStatus

منبع: [InvitationStatus.cs](../MakanApp.Domain/Organization/InvitationStatus.cs).

`Pending`، `Accepted`، `Declined`، `Expired`، `Revoked`

<a id="type-messagekind"></a>
### MessageKind

منبع: [MessageKind.cs](../MakanApp.Domain/Messaging/MessageKind.cs).

`Text`، `Image`، `Video`، `Voice`، `File`

<a id="type-messagereactiontype"></a>
### MessageReactionType

منبع: [MessageReactionType.cs](../MakanApp.Domain/Messaging/MessageReactionType.cs).

`Like`، `Love`، `Laugh`، `Wow`، `Sad`

<a id="type-messagereceiptstatus"></a>
### MessageReceiptStatus

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

`Sent`

<a id="type-messagingchangetype"></a>
### MessagingChangeType

منبع: [MessagingChangeType.cs](../MakanApp.Domain/Messaging/MessagingChangeType.cs).

`MessageCreated`، `MessageEdited`، `MessageDeleted`، `ReactionChanged`، `PinChanged`، `ParticipantChanged`، `ConversationChanged`، `ReadCursorAdvanced`، `DeliveryCursorAdvanced`

<a id="type-organizationrole"></a>
### OrganizationRole

منبع: [OrganizationRole.cs](../MakanApp.Domain/Organization/OrganizationRole.cs).

`Student`، `Teacher`، `Parent`، `Manager`

<a id="type-ownershiptransferstatus"></a>
### OwnershipTransferStatus

منبع: [OwnershipTransferStatus.cs](../MakanApp.Domain/Messaging/OwnershipTransferStatus.cs).

`Pending`، `Accepted`، `Declined`، `Cancelled`، `Expired`

<a id="type-schedulerulestatus"></a>
### ScheduleRuleStatus

منبع: [ScheduleRuleStatus.cs](../MakanApp.Domain/Academic/ScheduleRuleStatus.cs).

`Active`، `Ended`

<a id="type-sessionstatus"></a>
### SessionStatus

منبع: [SessionStatus.cs](../MakanApp.Domain/Academic/SessionStatus.cs).

`Scheduled`، `Cancelled`، `Completed`

<a id="type-studentassignmentactionstatus"></a>
### StudentAssignmentActionStatus

منبع: [DashboardContracts.cs](../MakanApp.Application/Reports/Dashboards/DashboardContracts.cs).

`NotStarted`، `Draft`، `Submitted`، `SubmittedLate`، `Graded`

<a id="type-submissionattemptstatus"></a>
### SubmissionAttemptStatus

منبع: [SubmissionAttemptStatus.cs](../MakanApp.Domain/Assessment/SubmissionAttemptStatus.cs).

`Draft`، `Submitted`

<a id="type-teacherassignmentstatus"></a>
### TeacherAssignmentStatus

منبع: [TeacherAssignmentStatus.cs](../MakanApp.Domain/Academic/TeacherAssignmentStatus.cs).

`Active`، `Ended`

<a id="type-userblockstatus"></a>
### UserBlockStatus

منبع: [UserBlockStatus.cs](../MakanApp.Domain/Messaging/UserBlockStatus.cs).

`Active`، `Ended`

<a id="type-workspacetype"></a>
### WorkspaceType

منبع: [WorkspaceType.cs](../MakanApp.Domain/Organization/WorkspaceType.cs).

`Personal`، `Organization`

<a id="error-codes"></a>
## کدهای خطا

۲۰۶ literal شامل کدهای تعریف‌شده و UNEXPECTED_ERROR فهرست شده‌اند. خطای ناشناخته500 عمومی است؛ code خطا مبنای رفتار ماشین باشد و متن detail صرفاً برای توضیح کاربر. «استفاده در source» وجود ارجاع در کد را نشان می‌دهد، نه اثبات رسیدن به آن مسیر در تمام شرایط.

<a id="error-academic-period-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ACADEMIC_PERIOD_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-academic-period-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ACADEMIC_PERIOD_NOT_FOUND` / 404 | دوره تحصیلی معتبر پیدا نشد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-academic-read-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ACADEMIC_READ_NOT_ALLOWED` / 403 | دسترسی خواندن کلاس‌ها برای بافت فعلی مجاز نیست.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-assignment-already-published"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_ALREADY_PUBLISHED` / 409 | تکلیف قبلاً منتشر شده و نسخه منتشرشده تغییرپذیر نیست.؛ گیرندگان این نسخه قبلاً ثبت شده‌اند.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-assignment-attempts-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_ATTEMPTS_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-assignment-description-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_DESCRIPTION_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-assignment-due-date-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_DUE_DATE_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-assignment-max-score-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_MAX_SCORE_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-assignment-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_NOT_ALLOWED` / 403 | دسترسی به این تکلیف مجاز نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-assignment-not-draft"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_NOT_DRAFT` / 409 | تکلیف در وضعیت پیش‌نویس نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-assignment-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_NOT_FOUND` / 404 | تکلیف پیدا نشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-assignment-not-submittable"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_NOT_SUBMITTABLE` / 409 | تکلیف برای ارسال پاسخ فعال نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-assignment-recipient-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_RECIPIENT_NOT_FOUND` / 404 | مخاطب مجاز تکلیف پیدا نشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-assignment-title-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ASSIGNMENT_TITLE_REQUIRED` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-attendance-already-changed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ATTENDANCE_ALREADY_CHANGED` / 409 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-attendance-already-recorded"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ATTENDANCE_ALREADY_RECORDED` / 409 | حضور این ثبت‌نام قبلاً ثبت شده است.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-attendance-correction-reason-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ATTENDANCE_CORRECTION_REASON_REQUIRED` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-attendance-enrollment-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ATTENDANCE_ENROLLMENT_INVALID` / 400 | فهرست ثبت حضور معتبر نیست.؛ ثبت‌نام معتبر برای این جلسه پیدا نشد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-attendance-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ATTENDANCE_NOT_ALLOWED` / 403 | ثبت یا مشاهده حضور و غیاب این کلاس مجاز نیست.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-auth-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `AUTH_REQUIRED` / 401 | برای ادامه باید وارد شوید.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-change-cursor-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CHANGE_CURSOR_INVALID` / 400 | نشانگر تغییر معتبر نیست.؛ نشانگر تغییر از وضعیت فعلی گفتگو جلوتر است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-child-context-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CHILD_CONTEXT_NOT_ALLOWED` / 403 | بافت فرزند انتخاب‌شده دیگر مجاز نیست.؛ استفاده در source دیده شده | [GuardianErrorCodes.cs](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |


<a id="error-child-context-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CHILD_CONTEXT_NOT_FOUND` / 404 | فرزند مجاز در فضای سازمانی فعلی پیدا نشد.؛ برای مشاهده گزارش باید ابتدا فرزند را انتخاب کنید.؛ استفاده در source دیده شده | [GuardianErrorCodes.cs](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |


<a id="error-class-capacity-exceeded"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CLASS_CAPACITY_EXCEEDED` / 409 | ظرفیت کلاس تکمیل شده است.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-class-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CLASS_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-class-not-active"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CLASS_NOT_ACTIVE` / 409 | کلاس برای ثبت‌نام فعال نیست.؛ کلاس برای برنامه‌ریزی جلسه فعال نیست.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-class-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CLASS_NOT_FOUND` / 404 | کلاس پیدا نشد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-class-session-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CLASS_SESSION_CONFLICT` / 409 | کلاس در این بازه زمانی جلسه دیگری دارد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-concurrency-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CONCURRENCY_CONFLICT` / 412 | نسخه رکورد معتبر نیست؛ داده را دوباره دریافت کنید.؛ ارزیابی هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs)، [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs)، [GuardianErrorCodes.cs](../MakanApp.Application/Guardian/GuardianErrorCodes.cs)، [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs)، [OrganizationErrorCodes.cs](../MakanApp.Application/Organization/OrganizationErrorCodes.cs)، [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-conversation-archived"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CONVERSATION_ARCHIVED` / 409 | گفتگوی بایگانی‌شده پیام تازه نمی‌پذیرد.؛ گفتگوی بایگانی‌شده قابل تغییر نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-conversation-creation-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CONVERSATION_CREATION_CONFLICT` / 409 | این شناسه عملیات قبلاً برای درخواست ایجاد متفاوتی استفاده شده است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-conversation-management-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CONVERSATION_MANAGEMENT_NOT_ALLOWED` / 403 | برای خروج خودتان از endpoint خروج استفاده کنید.؛ اجازه مدیریت این گفتگو را ندارید.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-conversation-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CONVERSATION_NOT_ALLOWED` / 403 | دسترسی خواندن این گفتگو در زمینه فعلی مجاز نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-conversation-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CONVERSATION_NOT_FOUND` / 404 | گفتگو یافت نشد.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-conversation-role-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `CONVERSATION_ROLE_INVALID` / 400 | نقش درخواستی برای این عملیات معتبر نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-course-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `COURSE_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-course-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `COURSE_NOT_FOUND` / 404 | درس معتبر پیدا نشد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-delivery-cursor-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `DELIVERY_CURSOR_INVALID` / 400 | پیام متناظر با نشانگر در تاریخچه قابل مشاهده وجود ندارد.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-direct-conversation-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `DIRECT_CONVERSATION_CONFLICT` / 409 | ساخت هم‌زمان گفتگو با تعارض روبه‌رو شد؛ دوباره تلاش کنید.؛ اعضای گفتگوی مستقیم با هویت canonical آن سازگار نیستند.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-direct-recipient-not-available"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `DIRECT_RECIPIENT_NOT_AVAILABLE` / 404 | مخاطب موردنظر برای شروع گفتگو در دسترس نیست.؛ کاربر موردنظر برای عضویت در این گفتگو در دسترس نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-enrollment-already-active"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ENROLLMENT_ALREADY_ACTIVE` / 409 | ثبت‌نام فعال از قبل وجود دارد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-enrollment-not-active"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ENROLLMENT_NOT_ACTIVE` / 409 | وضعیت پایان ثبت‌نام معتبر نیست.؛ ثبت‌نام فعال پیدا نشد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-evaluation-already-released"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EVALUATION_ALREADY_RELEASED` / 409 | ارزیابی منتشرشده قابل ویرایش مستقیم نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-evaluation-correction-reason-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EVALUATION_CORRECTION_REASON_REQUIRED` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-evaluation-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EVALUATION_NOT_ALLOWED` / 403 | دسترسی به ارزیابی مجاز نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-evaluation-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EVALUATION_NOT_FOUND` / 404 | ارزیابی پیدا نشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-evaluation-not-ready-for-release"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EVALUATION_NOT_READY_FOR_RELEASE` / 409 | ارزیابی آماده انتشار وجود ندارد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-evaluation-score-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EVALUATION_SCORE_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-evaluation-submission-not-final"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EVALUATION_SUBMISSION_NOT_FINAL` / 409 | فقط تلاش نهایی‌شده قابل ارزیابی است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-already-finalized"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ALREADY_FINALIZED` / 409 | تلاش آزمون قبلاً با وضعیت دیگری نهایی شده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-already-published"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ALREADY_PUBLISHED` / 409 | نسخه جاری آزمون قبلاً منتشر شده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-answer-idempotency-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ANSWER_IDEMPOTENCY_CONFLICT` / 409 | شناسه عملیات ذخیره پاسخ الزامی است.؛ شناسه عملیات با payload دیگری قبلاً استفاده شده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-answer-key-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ANSWER_KEY_INVALID` / 400 | ترتیب گزینه‌های سؤال باید یکتا باشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-answer-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ANSWER_NOT_ALLOWED` / 403 | سؤال برای این تلاش آزمون تثبیت نشده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-answer-option-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ANSWER_OPTION_INVALID` / 400 | گزینه انتخاب‌شده متعلق به سؤال این تلاش نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-answer-set-version-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ANSWER_SET_VERSION_CONFLICT` / 409 | نسخه مجموعه پاسخ‌ها تغییر کرده است؛ پاسخ‌های پذیرفته‌شده را دوباره دریافت کنید.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-answer-type-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ANSWER_TYPE_INVALID` / 400 | ساختار پاسخ با نوع سؤال سازگار نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-answer-version-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ANSWER_VERSION_CONFLICT` / 409 | نسخه پاسخ تغییر کرده است؛ پاسخ جاری را دوباره دریافت کنید.؛ نسخه پاسخ با وضعیت جاری سازگار نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-attempts-exhausted"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ATTEMPTS_EXHAUSTED` / 409 | تعداد تلاش‌های مجاز آزمون تمام شده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-attempts-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ATTEMPTS_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-attempt-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ATTEMPT_NOT_ALLOWED` / 403 | دسترسی به تلاش آزمون مجاز نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-attempt-not-finalizable"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ATTEMPT_NOT_FINALIZABLE` / 409 | تلاش آزمون هنوز نهایی نشده است.؛ تلاش آزمون در وضعیت قابل نهایی‌سازی نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-attempt-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ATTEMPT_NOT_FOUND` / 404 | تلاش آزمون پیدا نشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-attempt-not-writable"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_ATTEMPT_NOT_WRITABLE` / 409 | تلاش آزمون قابل ویرایش نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-deadline-passed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_DEADLINE_PASSED` / 409 | مهلت ثبت پاسخ آزمون پایان یافته است.؛ مهلت نهایی‌سازی آزمون پایان یافته است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-duration-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_DURATION_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-finalize-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_FINALIZE_CONFLICT` / 409 | نهایی‌سازی آزمون با وضعیت جاری سازگار نیست.؛ نهایی‌سازی هم‌زمان آزمون با تداخل مواجه شد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-finalize-idempotency-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_FINALIZE_IDEMPOTENCY_CONFLICT` / 409 | شناسه عملیات نهایی‌سازی آزمون الزامی است.؛ شناسه عملیات نهایی‌سازی قبلاً با payload دیگری استفاده شده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-already-released"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_ALREADY_RELEASED` / 409 | نمره منتشرشده قابل ویرایش مستقیم نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-attempt-not-finalized"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_ATTEMPT_NOT_FINALIZED` / 409 | فقط Attempt نهایی‌شده قابل تصحیح است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-correction-reason-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_CORRECTION_REASON_REQUIRED` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-incomplete"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_INCOMPLETE` / 409 | نمره آزمون برای انتشار کامل نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_NOT_ALLOWED` / 403 | دسترسی به نمره آزمون مجاز نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_NOT_FOUND` / 404 | نمره آزمون پیدا نشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-not-released"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_NOT_RELEASED` / 409 | نمره منتشرشده پیدا نشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-question-not-reviewed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_QUESTION_NOT_REVIEWED` / 409 | همه سؤال‌های نیازمند بازبینی دستی هنوز بررسی نشده‌اند.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-release-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_RELEASE_CONFLICT` / 409 | وضعیت انتشار نمره هم‌زمان تغییر کرده است.؛ نمره یا انتشار هم‌زمان دیگری برای این آزمون ثبت شده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-release-idempotency-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_RELEASE_IDEMPOTENCY_CONFLICT` / 409 | شناسه عملیات انتشار قبلاً با درخواست دیگری استفاده شده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-release-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_RELEASE_NOT_ALLOWED` / 403 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ تعریف‌شده، ارجاع اجرایی استخراج نشد | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-grade-score-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_GRADE_SCORE_INVALID` / 400 | نمره باید در بازه مجاز سؤال و با حداکثر دو رقم اعشار باشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-max-score-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_MAX_SCORE_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_NOT_ALLOWED` / 403 | دسترسی به آزمون مجاز نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-not-available-yet"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_NOT_AVAILABLE_YET` / 409 | بازه شروع آزمون هنوز آغاز نشده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-not-draft"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_NOT_DRAFT` / 409 | تا پیش از انتشار نسخه جاری، نسخه جدید ساخته نمی‌شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_NOT_FOUND` / 404 | آزمون پیدا نشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-not-published"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_NOT_PUBLISHED` / 409 | آزمون منتشرشده‌ای برای شروع وجود ندارد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-question-order-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_QUESTION_ORDER_INVALID` / 409 | ترتیب سؤال در نسخه آزمون باید یکتا باشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-question-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_QUESTION_REQUIRED` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-question-score-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_QUESTION_SCORE_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-question-set-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_QUESTION_SET_INVALID` / 409 | مجموعه سؤال‌های نسخه آزمون معتبر نیست.؛ سیاست ترتیب سؤال‌های آزمون معتبر نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-randomization-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_RANDOMIZATION_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-score-total-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_SCORE_TOTAL_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-start-idempotency-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_START_IDEMPOTENCY_CONFLICT` / 409 | شناسه عملیات شروع آزمون الزامی است.؛ شناسه عملیات برای درخواست شروع دیگری استفاده شده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-student-preview-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_STUDENT_PREVIEW_NOT_ALLOWED` / 403 | محتوای پیش‌نمایش آزمون فقط برای دانش‌آموز مجاز یا مدیر/معلم کلاس قابل مشاهده است.؛ محتوای آزمون خارج از بازه فعال قابل مشاهده نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-title-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_TITLE_REQUIRED` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-version-locked"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_VERSION_LOCKED` / 409 | نسخه منتشرشده آزمون تغییرپذیر نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-window-closed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_WINDOW_CLOSED` / 409 | بازه شروع آزمون پایان یافته است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-window-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_WINDOW_INVALID` / 400 | پایان بازه آزمون هنگام انتشار باید در آینده باشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-write-lease-held-by-other-session"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_WRITE_LEASE_HELD_BY_OTHER_SESSION` / 409 | نشست دیگری write lease این تلاش را در اختیار دارد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-write-lease-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_WRITE_LEASE_REQUIRED` / 409 | برای ذخیره پاسخ باید write lease فعال دریافت شود.؛ برای نهایی‌سازی آزمون باید write lease فعال دریافت شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-exam-write-lease-stale"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `EXAM_WRITE_LEASE_STALE` / 409 | نسخه write lease منقضی شده است؛ وضعیت جاری را دوباره دریافت کنید.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-file-already-deleted"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `FILE_ALREADY_DELETED` / 409 | فایل قبلاً حذف شده است.؛ استفاده در source دیده شده | [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-file-empty"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `FILE_EMPTY` / 400 | فایل خالی قابل بارگذاری نیست.؛ استفاده در source دیده شده | [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-file-in-use"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `FILE_IN_USE` / 409 | فایل بخشی از یک سابقه رسمی است و قابل حذف نیست.؛ استفاده در source دیده شده | [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-file-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `FILE_NOT_ALLOWED` / 403 | دسترسی به فایل در فضای کاری فعلی مجاز نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs)، [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-file-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `FILE_NOT_FOUND` / 404 | فایل پیدا نشد.؛ استفاده در source دیده شده | [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-file-not-ready"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `FILE_NOT_READY` / 409 | فایل هنوز برای دریافت آماده نیست.؛ استفاده در source دیده شده | [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-file-storage-failed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `FILE_STORAGE_FAILED` / 503 | دسترسی به محتوای فایل در مخزن ممکن نیست.؛ ذخیره محتوای فایل ناموفق بود.؛ استفاده در source دیده شده | [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-file-too-large"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `FILE_TOO_LARGE` / 413 | حجم فایل از سقف مجاز بیشتر است.؛ حجم واقعی فایل از سقف مجاز بیشتر است.؛ استفاده در source دیده شده | [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-file-type-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `FILE_TYPE_NOT_ALLOWED` / 415 | نوع محتوای فایل مجاز نیست.؛ استفاده در source دیده شده | [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-file-upload-failed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `FILE_UPLOAD_FAILED` / 500 | اندازه واقعی فایل با اندازه اعلام‌شده یکسان نیست.؛ تکمیل بارگذاری فایل ناموفق بود.؛ استفاده در source دیده شده | [StorageErrorCodes.cs](../MakanApp.Application/Storage/StorageErrorCodes.cs) |


<a id="error-grade-not-released"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `GRADE_NOT_RELEASED` / 404 | نتیجه منتشرشده‌ای برای اصلاح وجود ندارد.؛ نتیجه منتشرشده پیدا نشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-grade-release-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `GRADE_RELEASE_CONFLICT` / 409 | یک اصلاح منتشرنشده از قبل وجود دارد.؛ وضعیت انتشار نمره هم‌زمان تغییر کرده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-guardian-relation-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `GUARDIAN_RELATION_NOT_FOUND` / 404 | رابطه سرپرستی مجاز پیدا نشد.؛ استفاده در source دیده شده | [GuardianErrorCodes.cs](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |


<a id="error-invitation-already-accepted"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `INVITATION_ALREADY_ACCEPTED` / 409 | این دعوت قبلاً پذیرفته شده است.؛ استفاده در source دیده شده | [OrganizationErrorCodes.cs](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |


<a id="error-invitation-expired"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `INVITATION_EXPIRED` / 410 | دعوت منقضی شده است.؛ استفاده در source دیده شده | [OrganizationErrorCodes.cs](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |


<a id="error-invitation-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `INVITATION_NOT_FOUND` / 404 | دعوت موردنظر پیدا نشد.؛ استفاده در source دیده شده | [OrganizationErrorCodes.cs](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |


<a id="error-invitation-revoked"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `INVITATION_REVOKED` / 410 | دعوت لغو شده است.؛ استفاده در source دیده شده | [OrganizationErrorCodes.cs](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |


<a id="error-last-owner-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `LAST_OWNER_REQUIRED` / 409 | مالک باید پیش از خروج، مالکیت را به عضو دیگری منتقل کند.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-learner-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `LEARNER_NOT_FOUND` / 404 | فراگیر معتبر پیدا نشد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-managed-conversation-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MANAGED_CONVERSATION_INVALID` / 400 | شناسه عملیات و scope معتبر الزامی است.؛ اعضای اولیه باید شناسه‌های یکتا و معتبر داشته باشند و شامل سازنده نباشند.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-manager-role-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MANAGER_ROLE_REQUIRED` / 403 | فضای سازمانی با نقش فعال مدیر لازم است.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-membership-not-active"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MEMBERSHIP_NOT_ACTIVE` / 403 | عضویت انتخاب‌شده فعال نیست.؛ استفاده در source دیده شده | [OrganizationErrorCodes.cs](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |


<a id="error-mention-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MENTION_NOT_ALLOWED` / 403 | فهرست منشن‌های پیام معتبر نیست.؛ یکی از کاربران منشن‌شده عضو قابل مشاهده این گفتگو نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-attachment-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_ATTACHMENT_NOT_ALLOWED` / 403 | فهرست پیوست‌های پیام معتبر نیست.؛ استفاده از این فایل در پیام مجاز نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-attachment-not-ready"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_ATTACHMENT_NOT_READY` / 409 | فایل پیوست هنوز آماده استفاده نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-attachment-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_ATTACHMENT_REQUIRED` / 400 | این نوع پیام حداقل یک پیوست می‌خواهد.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-client-id-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_CLIENT_ID_REQUIRED` / 400 | شناسه پایدار پیام کلاینت الزامی است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-deleted"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_DELETED` / 409 | پیام حذف‌شده قابل ویرایش نیست.؛ پیام قبلاً حذف شده است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-edit-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_EDIT_CONFLICT` / 412 | نسخه پیام معتبر نیست.؛ پیام هم‌زمان تغییر کرده است؛ نسخه تازه را دریافت کنید.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-empty"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_EMPTY` / 400 | متن پیام الزامی است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-forward-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_FORWARD_NOT_ALLOWED` / 403 | پیام حذف‌شده قابل فوروارد نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-forward-scope-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_FORWARD_SCOPE_NOT_ALLOWED` / 403 | انتقال پیام میان محدوده‌های متفاوت بدون مجوز خروج صریح مجاز نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-idempotency-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_IDEMPOTENCY_CONFLICT` / 409 | این شناسه پیام قبلاً برای محتوای دیگری استفاده شده است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-kind-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_KIND_INVALID` / 400 | فیلتر نوع رسانه معتبر نیست.؛ نوع پیام معتبر نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_NOT_ALLOWED` / 403 | فقط فرستنده اصلی می‌تواند پیام را برای همه حذف کند.؛ رابطه ارتباطی معتبر برای ارسال پیام وجود ندارد.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-not-editable"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_NOT_EDITABLE` / 409 | فقط فرستنده اصلی می‌تواند پیام را ویرایش کند.؛ پیام گفتگوی بایگانی‌شده قابل تغییر نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_NOT_FOUND` / 404 | پیام یافت نشد.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-page-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_PAGE_INVALID` / 400 | نشانگر ترتیب پیام معتبر نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-publish-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_PUBLISH_NOT_ALLOWED` / 403 | نقش فعلی اجازه انتشار پیام در این گفتگو را ندارد.؛ انتشار پیام در این نوع گفتگو مجاز نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-reply-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_REPLY_NOT_ALLOWED` / 403 | پیام مرجع پاسخ در این گفتگو در دسترس نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-message-too-long"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `MESSAGE_TOO_LONG` / 400 | متن پیام از طول مجاز بیشتر است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-organization-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ORGANIZATION_NOT_FOUND` / 404 | سازمان انتخاب‌شده در دسترس نیست.؛ سازمان دعوت در دسترس نیست.؛ استفاده در source دیده شده | [OrganizationErrorCodes.cs](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |


<a id="error-organization-scope-mismatch"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ORGANIZATION_SCOPE_MISMATCH` / 403 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs)، [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-otp-already-used"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `OTP_ALREADY_USED` / 409 | این کد یک‌بارمصرف قبلاً استفاده شده است.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-otp-expired"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `OTP_EXPIRED` / 410 | کد یک‌بارمصرف منقضی شده است.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-otp-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `OTP_INVALID` / 400 | کد یک‌بارمصرف معتبر نیست.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-otp-rate-limited"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `OTP_RATE_LIMITED` / 429 | برای درخواست مجدد کد کمی صبر کنید.؛ تعداد درخواست‌های کد بیش از حد مجاز است.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-otp-too-many-attempts"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `OTP_TOO_MANY_ATTEMPTS` / 429 | تعداد تلاش‌های ناموفق بیش از حد مجاز است.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-ownership-transfer-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `OWNERSHIP_TRANSFER_CONFLICT` / 409 | مالکیت باید به عضو دیگری منتقل شود.؛ وضعیت فعلی اعضا با درخواست انتقال مالکیت سازگار نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-ownership-transfer-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `OWNERSHIP_TRANSFER_NOT_FOUND` / 404 | درخواست انتقال مالکیت یافت نشد.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-parent-role-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `PARENT_ROLE_REQUIRED` / 403 | برای دسترسی به فرزند، فضای سازمانی با نقش فعال والد لازم است.؛ استفاده در source دیده شده | [GuardianErrorCodes.cs](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |


<a id="error-participant-already-active"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `PARTICIPANT_ALREADY_ACTIVE` / 409 | کاربر از قبل عضو فعال گفتگو است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-participant-not-active"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `PARTICIPANT_NOT_ACTIVE` / 403 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ تعریف‌شده، ارجاع اجرایی استخراج نشد | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-participant-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `PARTICIPANT_NOT_FOUND` / 404 | عضو گفتگو یافت نشد.؛ عضو فعال گفتگو یافت نشد.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-phone-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `PHONE_INVALID` / 400 | شماره تلفن معتبر نیست.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-pin-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `PIN_NOT_ALLOWED` / 403 | نقش فعلی اجازه مدیریت سنجاق‌های گفتگو را ندارد.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-profile-validation-failed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `PROFILE_VALIDATION_FAILED` / 400 | اطلاعات پروفایل معتبر نیست.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-reaction-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `REACTION_NOT_ALLOWED` / 403 | واکنش انتخاب‌شده مجاز نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-read-cursor-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `READ_CURSOR_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-realtime-subscription-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `REALTIME_SUBSCRIPTION_NOT_ALLOWED` / 403 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-report-date-range-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `REPORT_DATE_RANGE_INVALID` / 400 | بازه زمانی باید UTC باشد و زمان شروع از زمان پایان بیشتر نباشد.؛ استفاده در source دیده شده | [DetailedReportErrorCodes.cs](../MakanApp.Application/Reports/Detailed/DetailedReportErrorCodes.cs) |


<a id="error-report-idempotency-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `REPORT_IDEMPOTENCY_CONFLICT` / 409 | این شناسه گزارش قبلاً برای درخواست دیگری استفاده شده است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-report-message-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `REPORT_MESSAGE_NOT_FOUND` / 404 | گزارش یافت نشد.؛ پیام قابل گزارش یافت نشد.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-report-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `REPORT_NOT_ALLOWED` / 403 | درخواست گزارش پیام معتبر نیست.؛ توضیح گزارش از طول مجاز بیشتر است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-report-pagination-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `REPORT_PAGINATION_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [DetailedReportErrorCodes.cs](../MakanApp.Application/Reports/Detailed/DetailedReportErrorCodes.cs) |


<a id="error-report-resource-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `REPORT_RESOURCE_NOT_FOUND` / 404 | گزارش یا منبع در محدوده دسترسی فعلی پیدا نشد.؛ استفاده در source دیده شده | [DetailedReportErrorCodes.cs](../MakanApp.Application/Reports/Detailed/DetailedReportErrorCodes.cs) |


<a id="error-report-sort-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `REPORT_SORT_INVALID` / 400 | مرتب‌سازی درخواست‌شده برای این گزارش مجاز نیست.؛ استفاده در source دیده شده | [DetailedReportErrorCodes.cs](../MakanApp.Application/Reports/Detailed/DetailedReportErrorCodes.cs) |


<a id="error-role-not-active"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `ROLE_NOT_ACTIVE` / 403 | نقش انتخاب‌شده فعال نیست.؛ استفاده در source دیده شده | [OrganizationErrorCodes.cs](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |


<a id="error-schedule-rule-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SCHEDULE_RULE_NOT_FOUND` / 404 | برنامه تکرارشونده پیدا نشد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-search-cursor-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SEARCH_CURSOR_INVALID` / 400 | نشانگر جستجو معتبر نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-search-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SEARCH_NOT_ALLOWED` / 403 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ تعریف‌شده، ارجاع اجرایی استخراج نشد | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-search-query-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SEARCH_QUERY_INVALID` / 400 | پارامترهای جستجو معتبر نیستند.؛ تعداد نتایج جستجو معتبر نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-search-query-required"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SEARCH_QUERY_REQUIRED` / 400 | عبارت جستجو الزامی است.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-session-cancelled"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SESSION_CANCELLED` / 409 | حضور جلسه لغوشده قابل اصلاح نیست.؛ برای جلسه لغوشده حضور ثبت نمی‌شود.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-session-not-active"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SESSION_NOT_ACTIVE` / 409 | پیش از شروع جلسه نمی‌توان حضور ثبت کرد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-session-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SESSION_NOT_FOUND` / 404 | جلسه پیدا نشد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-session-time-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SESSION_TIME_CONFLICT` / 409 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ تعریف‌شده، ارجاع اجرایی استخراج نشد | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-session-time-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SESSION_TIME_INVALID` / 400 | جلسه جدید باید در آینده شروع شود.؛ بازه دریافت برنامه معتبر نیست.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-sms-provider-authentication-failed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SMS_PROVIDER_AUTHENTICATION_FAILED` / 503 | سرویس ارسال پیامک احراز هویت نشد.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-sms-provider-configuration-error"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SMS_PROVIDER_CONFIGURATION_ERROR` / 503 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ تعریف‌شده، ارجاع اجرایی استخراج نشد | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-sms-provider-rejected"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SMS_PROVIDER_REJECTED` / 422 | درخواست ارسال پیامک توسط سرویس‌دهنده پذیرفته نشد.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-sms-provider-unavailable"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SMS_PROVIDER_UNAVAILABLE` / 503 | سرویس ارسال پیامک پاسخ قابل استفاده‌ای برنگرداند.؛ ارسال پیامک در این محیط پیکربندی نشده است.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-sms-rate-limited"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SMS_RATE_LIMITED` / 429 | محدودیت ارسال سرویس پیامک فعال است؛ بعداً دوباره تلاش کنید.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-sms-recipient-blocked"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SMS_RECIPIENT_BLOCKED` / 422 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ تعریف‌شده، ارجاع اجرایی استخراج نشد | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-sms-recipient-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SMS_RECIPIENT_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ تعریف‌شده، ارجاع اجرایی استخراج نشد | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-submission-answer-invalid"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SUBMISSION_ANSWER_INVALID` / 400 | توضیح اختصاصی در محل throw و قرارداد حوزه بررسی شود.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-submission-attempts-exhausted"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SUBMISSION_ATTEMPTS_EXHAUSTED` / 409 | تعداد تلاش‌های مجاز این تکلیف تمام شده است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-submission-deadline-passed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SUBMISSION_DEADLINE_PASSED` / 409 | مهلت ارسال نهایی تکلیف گذشته است.؛ مهلت ایجاد یا ذخیره پیش‌نویس این تکلیف گذشته است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-submission-empty"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SUBMISSION_EMPTY` / 400 | برای ارسال نهایی، متن پاسخ یا حداقل یک فایل آماده لازم است.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-submission-file-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SUBMISSION_FILE_NOT_ALLOWED` / 403 | استفاده از این فایل در پاسخ مجاز نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-submission-file-not-ready"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SUBMISSION_FILE_NOT_READY` / 409 | فایل برای ارسال نهایی آماده نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-submission-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SUBMISSION_NOT_ALLOWED` / 403 | دسترسی به این تلاش ارسال مجاز نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-submission-not-draft"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SUBMISSION_NOT_DRAFT` / 409 | تلاش ارسال‌شده تغییرپذیر نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-submission-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SUBMISSION_NOT_FOUND` / 404 | تلاش ارسال پیدا نشد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-submission-version-mismatch"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `SUBMISSION_VERSION_MISMATCH` / 409 | نسخه یا مخاطب تلاش با تکلیف منتشرشده سازگار نیست.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-teacher-assignment-already-active"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `TEACHER_ASSIGNMENT_ALREADY_ACTIVE` / 409 | انتساب فعال معلم از قبل وجود دارد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-teacher-assignment-not-active"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `TEACHER_ASSIGNMENT_NOT_ACTIVE` / 409 | انتساب فعال معلم پیدا نشد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-teacher-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `TEACHER_NOT_ALLOWED` / 403 | عضویت معلم معتبر نیست یا نقش فعال معلم ندارد.؛ مدیریت جلسه برای کلاس انتخاب‌شده مجاز نیست.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-teacher-not-assigned"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `TEACHER_NOT_ASSIGNED` / 403 | معلم برای کلاس انتخاب‌شده TeacherAssignment فعال ندارد.؛ معلم برای کلاس این پاسخ TeacherAssignment فعال ندارد.؛ استفاده در source دیده شده | [AssessmentErrorCodes.cs](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |


<a id="error-teacher-session-conflict"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `TEACHER_SESSION_CONFLICT` / 409 | معلم یک جلسه متعارض دیگر دارد.؛ استفاده در source دیده شده | [AcademicErrorCodes.cs](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |


<a id="error-unexpected-error"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `UNEXPECTED_ERROR` / 500 | خطای پیش‌بینی‌نشده سرور.؛ استفاده در source دیده شده | [GlobalExceptionHandler.cs](../MakanApp.Api/Errors/GlobalExceptionHandler.cs) |


<a id="error-username-already-exists"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `USERNAME_ALREADY_EXISTS` / 409 | این نام کاربری قبلاً استفاده شده است.؛ استفاده در source دیده شده | [IdentityErrorCodes.cs](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |


<a id="error-user-block-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `USER_BLOCK_NOT_ALLOWED` / 403 | این کاربر قابل مسدودسازی نیست.؛ این کاربر قابل رفع مسدودی نیست.؛ استفاده در source دیده شده | [MessagingErrorCodes.cs](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |


<a id="error-workspace-not-allowed"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `WORKSPACE_NOT_ALLOWED` / 403 | فضای کاری انتخاب‌شده دیگر مجاز نیست.؛ داشبورد در فضای سازمانی و فقط برای نقش فعال متناظر قابل مشاهده است.؛ استفاده در source دیده شده | [OrganizationErrorCodes.cs](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |


<a id="error-workspace-not-found"></a>

| code / HTTP | توضیح و وضعیت استفاده | منبع |
| --- | --- | --- |
| `WORKSPACE_NOT_FOUND` / 404 | فضای کاری انتخاب‌شده پیدا نشد.؛ استفاده در source دیده شده | [OrganizationErrorCodes.cs](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |


برای فهم معماری، مسیرهای اجرا و توسعه به [راهنمای جامع](backend-api-guide.md) و برای تنظیمات عملیاتی به [سند استقرار](deployment-windows-linux-docker.md) مراجعه کنید. Markdown این سند مرجع نگه‌داری است؛ HTML/PDF جداگانه آن با ابزار tools/docs/build_backend_docs.py تولید می‌شوند.


<a id="handoff-validation"></a>
## شناسنامه بازبینی و نتیجه اعتبارسنجی

این تحویل در `2026-10-12` بر مبنای سورس `main@f0ff3be` انجام شده است. تغییرات این مرحله مستندات و ابزار ساخت خروجی‌اند؛ کد محصول، frontend و schema تغییر نکرده‌اند. هیچ migration تازه ساخته یا روی دیتابیس عادی/Production اعمال نشده است.

restore موفق بود؛ build اولیه و نهایی هر دو صفر warning و صفر error داشتند. در هر دو دور suite، ۶۶۵ تست اجرا و موفق شدند: ۲۳۴ Unit، ۴۱۷ Integration و ۱۴ Architecture؛ شکست و skipped صفر. Integration از LocalDB ایزوله fixture استفاده کرد؛ SMS واقعی یا استقرار روی سرور انجام نشد.

۱۴۱ عملیات و پارامترهای query با Controller و OpenAPI تازه میزبان تطبیق داده شدند؛ ۱۸۲ schema JSON و چهار مدل query کنترل شدند. ۱۹۰ نمونه JSON درخواست/پاسخ از نظر فیلد، nesting، type، enum، nullable، UUID، زمان و نسخه بررسی شدند. لینک‌های داخلی/مسیرهای source و تعداد ستون جدول‌ها نیز کنترل شدند. PDF هر کتاب با مرورگر رندر، شماره صفحه و نشانک اضافه و برای صفحه خالی و متن خارج صفحه بررسی شد؛ صفحات نماینده نیز بصری بازبینی شدند.

این بررسی، قرارداد نسخه مشخص مخزن را تأیید می‌کند. آمادگی داده اولیه سازمان، مجوز حساب‌های واقعی، اعتبار ServiceToken، تحویل SMS، storage پایدار و تنظیم proxy باید در محیط هدف جدا آزمایش شوند.
