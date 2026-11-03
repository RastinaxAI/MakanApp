# قرارداد کلاینت برای PersonalClass v2 Academic

این سند مربوط به Backend است؛ هیچ فایل Frontend تغییر نکرده است. نام routeها از Controllerهای واقعی گرفته شده‌اند. خروجی OpenAPI برنامه مرجع schema کامل DTOهاست.

## Context و اختیار

`GET /api/v1/personal-classes/{classId}`، قابلیت‌های مؤثر `canAuthorAssignments`، `canAuthorExams`، `canGrade` و `canViewOwnResults` را برمی‌گرداند. Manager/Teacher بدون Grant آموزشی صریح اختیاری ندارند. این booleanها فقط راهنمای UI هستند؛ server در هر request مجوز جاری را بررسی می‌کند.

`ClassRef` در منابع آموزشی به شکل `{ "kind": "PersonalClass", "classId": "<guid>" }` است. `organizationId` و `enrollmentId` برای کلاس مستقل null هستند؛ به آن‌ها شناسهٔ ساختگی ندهید. `personalClassMembershipId` دورهٔ عضویت واقعی تلاش/فایل است. انتخاب workspace سازمانی برای account، مجوز اضافی PersonalClass ایجاد نمی‌کند.

تمام routeهای این سند نیازمند session معتبرند. استفاده از ClassId، AttemptId یا FileAssetId به‌تنهایی مجوز نیست. 404 در دسترسی غیرمجاز ممکن است برای پنهان‌سازی وجود منبع برگردد.

مدیریت Grant در `GET/PUT /api/v1/personal-classes/{classId}/academic-permissions[/{membershipId}]` برای مدیر محلی است؛ مسیر Platform با پیشوند `/api/admin/v1` همان ساختار را دارد. PUT شامل `capabilities`, `clientOperationId`, `expectedMembershipVersion`, `expectedGrantVersion` و `reason` است. مقدار `None` اختیار را لغو می‌کند. GET صفحه‌بندی دارد و تاریخچهٔ Grant با `isEffective` و `revokedAtUtc` را برمی‌گرداند؛ فعال‌بودن یک سطر قدیمی را حدس نزنید.

## تکلیف

| Method | Route بعد از `/api/v1/academic` | ورودی و خروجی اصلی |
|---|---|---|
| POST | `/personal-classes/{classId}/assignments` | `CreateAssignmentDraftCommand` → `AssignmentResult`، وضعیت 201 |
| GET | `/personal-classes/{classId}/assignments` | `page/pageSize/search/published` → `AcademicPage<AssignmentResult>` |
| GET | `/assignments/{assignmentId}` | نسخهٔ قابل مشاهدهٔ مجاز |
| PATCH | `/assignments/{assignmentId}` | `UpdateAssignmentDraftCommand`؛ دو RowVersion تکلیف/نسخه |
| POST | `/assignments/{assignmentId}/publish` | `PublishAssignmentCommand` → تکلیف، RecipientCount و PublicationReceipt |
| POST | `/assignments/{assignmentId}/attempts` | ایجاد/بازیابی Draft همان مخاطب؛ 201 و `SubmissionAttemptResult` |
| GET | `/assignments/{assignmentId}/attempts/me` | تلاش‌های قابل مشاهدهٔ همان هنرجو |
| GET | `/submission-attempts/{attemptId}` | دادهٔ مجاز تلاش؛ `ContentVisible` را رعایت کنید |
| PATCH | `/submission-attempts/{attemptId}/draft` | `answerText`, `expectedRowVersion` |
| POST | `/submission-attempts/{attemptId}/attachments` | `fileAssetId`, `expectedRowVersion` |
| DELETE | `/submission-attempts/{attemptId}/attachments/{fileAssetId}` | query `expectedRowVersion` |
| POST | `/submission-attempts/{attemptId}/submit` | `expectedRowVersion` → `SubmissionReceipt` پایدار |
| GET | `/evaluations/queue` | `classKind=PersonalClass`, `classId`, `assignmentId?`, `reviewStatus?`, `isLate?`, `page?`, `pageSize?` |
| GET | `/submission-attempts/{attemptId}/evaluation/submission` | پاسخ نهایی فقط برای Grade مؤثر |
| GET / PUT | `/submission-attempts/{attemptId}/evaluation` | DTO evaluator / ذخیرهٔ Draft نمره |
| POST | `/submission-attempts/{attemptId}/evaluation/release` | `expectedRowVersion`؛ انتشار صریح |
| POST | `/submission-attempts/{attemptId}/evaluation/corrections` | نمره، feedback، دلیل و RowVersion نمرهٔ منتشرشده؛ 201 |
| GET | `/submission-attempts/{attemptId}/result` | فقط نتیجهٔ Release‌شدهٔ هنرجوی صاحب تلاش |

Draft تکلیف: `title`, `description`, `dueAtUtc`, `allowLateSubmission`, `maxAttempts`, `maxScore`, `attachmentFileIds?`. MaxScore مثبت و دقیقاً متعلق به نسخه است؛ سقف جهانی ۲۰ وجود ندارد. در Update، null بودن `attachmentFileIds` پیوست‌ها را حفظ و آرایهٔ خالی آن‌ها را حذف می‌کند. نسخهٔ منتشرشده قابل تغییر نیست.

DTO نمرهٔ evaluator دارای `learnerFeedback`, `guardianVisibleFeedback`, `teacherPrivateNote` است؛ این سه فیلد یکسان نیستند. در PersonalClass مسیر Guardian دسترسی نتیجه ایجاد نمی‌کند. DTO هنرجو فقط `score`, `maxScore`, `learnerFeedback`, شمارهٔ revision و زمان Release دارد. `CreatedByPersonalMembershipId` و `ReleasedByPersonalMembershipId` در پاسخ کارکنان، دورهٔ عضویت عامل را مشخص می‌کنند.

## آزمون

| Method | Route بعد از `/api/v1/academic` | رفتار |
|---|---|---|
| POST | `/personal-classes/{classId}/exams` | ساخت Draft با قرارداد موجود، 201 |
| GET | `/personal-classes/{classId}/exams` | صفحه‌بندی `page/pageSize/search/published` |
| GET | `/exams/{examId}/editor` | Editor فقط با AuthorExams |
| GET | `/exams/{examId}/teacher-preview` | preview تألیف، شامل کلید سؤال برای عامل مجاز |
| PATCH | `/exams/{examId}` | ویرایش Draft و ExpectedVersionRowVersion |
| POST | `/exams/{examId}/questions` | افزودن سؤال و ExpectedVersionRowVersion |
| PATCH / DELETE | `/exams/{examId}/questions/{questionId}` | نسخهٔ سؤال و نسخهٔ آزمون برای concurrency |
| POST | `/exams/{examId}/publish` | ExpectedExamRowVersion و ExpectedVersionRowVersion |
| POST | `/exams/{examId}/versions` | نسخهٔ Draft بعدی از نسخهٔ منتشرشده، 201 |
| GET | `/exams/{examId}/student-preview` | بدون AnswerKey/IsCorrect؛ فقط بازهٔ مجاز فعلی |
| POST | `/exams/{examId}/attempts/start` | `clientOperationId` → تلاش، زمان سرور و deadline ثابت، 201 |
| GET | `/exams/{examId}/attempts/me/active` | بازیابی تلاش فعال |
| GET | `/exams/{examId}/attempts/me` | تاریخچهٔ تلاش‌های همان دورهٔ مشارکت |
| GET | `/exam-attempts/{attemptId}` | سؤال‌های freezeشده، بدون کلید پاسخ |
| POST | `/exam-attempts/{attemptId}/write-lease` | دریافت writer lease |
| POST | `/exam-attempts/{attemptId}/write-lease/transfer` | انتقال صریح نویسنده به نشست جاری |
| GET | `/exam-attempts/{attemptId}/answers` | پاسخ‌های پذیرفته‌شده و AnswerSetVersion |
| PUT | `/exam-attempts/{attemptId}/answers/{attemptQuestionId}` | `SaveExamAnswerCommand` و receipt revision |
| POST | `/exam-attempts/{attemptId}/answers/{attemptQuestionId}/attachments` | FileAssetId، ClientOperationId، WriteLeaseVersion، ExpectedRevisionNumber |
| DELETE | `/exam-attempts/{attemptId}/answers/{attemptQuestionId}/attachments/{fileAssetId}` | query عملیات/lease/revision |
| POST | `/exam-attempts/{attemptId}/finalize` | ClientOperationId، ExpectedAnswerSetVersion، WriteLeaseVersion |
| GET | `/exam-attempts/{attemptId}/receipt` | receipt پایدار نهایی |
| GET | `/exams/{examId}/grading` | صف `classId?/status?/page?/pageSize?` برای Grade |
| GET / POST | `/exam-attempts/{attemptId}/grading` | خواندن/آغاز Draft نمره و objective grading |
| PATCH | `/exam-attempts/{attemptId}/grading/questions/{attemptQuestionId}` | نمرهٔ تشریحی، feedback، private note، ExpectedGradeRowVersion |
| POST | `/exam-attempts/{attemptId}/grading/complete` | پایان بررسی دستی با RowVersion |
| POST | `/exam-attempts/{attemptId}/grade/release` | ClientOperationId و ExpectedGradeRowVersion |
| POST | `/exam-attempts/{attemptId}/grade/corrections` | دلیل و ExpectedReleasedGradeRowVersion؛ سپس بررسی/Complete/Release |
| GET | `/exam-attempts/{attemptId}/result` | وضعیت انتشار؛ تا Release، Score و feedback خصوصی پنهان |

نوع سؤال همان `ObjectiveSingleChoice` و `Descriptive` است. attachment-only فقط برای سؤال تشریحی با policy صریح `attachmentsAllowed=true` و `textRequired=false` پذیرفته می‌شود. اجازه، تعداد، نوع و حجم را از AttachmentPolicy همان نسخه بخوانید.

همهٔ زمان‌ها UTC و authority با Server است. Start تکراری deadline را تمدید نمی‌کند. FinalReceipt شامل `finalizationMode` است: `Manual` یا `Deadline`. در حالت Deadline فقط آخرین revisionهای پذیرفته‌شده تثبیت می‌شوند؛ upload یا پاسخ ارسال‌نشده وارد نتیجه نمی‌شود. دستگاه قدیمی با lease/revision قدیمی نباید UI موفق نشان دهد.

## صفحه‌بندی و Retry

فهرست‌های جدید کلاس مستقل `items/page/pageSize/total` دارند؛ Page از ۱، PageSize پیش‌فرض ۲۵ و حداکثر ۱۰۰ است. sort با شناسهٔ ثانویه پایدار است. صف‌های ارزیابی موجود شکل آرایهٔ قبلی را حفظ می‌کنند و در PersonalClass پیش‌فرض ۲۵ ردیف از SQL می‌گیرند؛ برای صفحات بعد page را افزایش دهید.

پس از 409 یا 412 آخرین snapshot/RowVersion را بخوانید و قصد کاربر را دوباره تطبیق دهید. همان عملیاتِ پاسخ آزمون باید همان ClientOperationId و همان payload را Retry کند. FinalSubmit/Finalize ACK ازدست‌رفته با همان intent رسید قبلی را می‌دهد. Retry انتشار PersonalClass کارت تکراری ایجاد نمی‌کند.

## File و Realtime

ابتدا `POST /api/v1/files` با multipart فیلد `File` و `PersonalClassId` انجام دهید. فقط فایل Ready همان context قابل attach است. `GET /api/v1/files/{fileId}/content` در هر بار درخواست ACL جاری را کنترل می‌کند. فایل Draft هنرجو برای evaluator قابل دریافت نیست. پس از پایان عضویت، UI نباید URL ذخیره‌شده را مجوز معتبر بداند.

Hub موجود `/hubs/messaging` و event `ClientInvalidated` استفاده می‌شوند. کلیدها: `assessment.assignment`, `assessment.submission`, `assessment.grade`, `assessment.exam`, `assessment.exam-attempt`, `assessment.exam-grade`. payload محتوای خصوصی ندارد؛ منبع مجاز را refetch کنید. cursor `/api/v1/realtime/changes?scope=Personal` را نگه دارید؛ reconnect به‌تنهایی replay نیست. در `REALTIME_RESYNC_REQUIRED` snapshot مجاز و cursor تازه بگیرید. EventId/StreamVersion را برای پردازش idempotent استفاده کنید؛ event تکراری نباید شمارنده را دوبار افزایش دهد.

Official Group و Channel برای هر انتشار یک Activity Card دارند؛ ClassRef مقصد را مشخص می‌کند. کارت نمرهٔ شخصی یا feedback عمومی ایجاد نمی‌شود.

## خطاها و محدودیت‌ها

ProblemDetails شامل `code`, `traceId`, `correlationId` است. کدهای Assessment موجود reuse می‌شوند؛ از HTTP status به‌تنهایی پیام محصولی نسازید. مثال: `ASSIGNMENT_RECIPIENT_NOT_FOUND`, `SUBMISSION_DEADLINE_PASSED`, `SUBMISSION_ATTEMPTS_EXHAUSTED`, `EXAM_WRITE_LEASE_STALE`, `EXAM_WRITE_LEASE_HELD_BY_OTHER_SESSION` و خطای precondition نسخه. پیغام واقعی را از قرارداد همان endpoint دریافت کنید.

در Archive، شروع/نوشتن/انتشار جدید متوقف می‌شود. پایان قطعی تلاش بازِ کلاس بایگانی/معلق هنوز تصمیم محصول می‌خواهد. پس از rejoin دورهٔ عضویت تازه است و تلاش قبلی دوباره فعال نمی‌شود. pricing مستقل و AI grading خارج از این مرحله‌اند.
