# راهنمای یکپارچهٔ مصرف PersonalClass v2

این راهنما قرارداد Backend واقعی در ۲۰۲۶-۱۱-۰۳ است؛ مجوز ساخت یا تغییر Frontend نیست. جزئیات DTOها در `/openapi/v1.json` و قواعد آموزشی قبلی در [راهنمای Academic](PERSONAL_CLASS_V2_ACADEMIC_FRONTEND_HANDOFF_FA.md) قرار دارند. در تعارض توصیف قدیمی با این مرحله، source و سند تولیدی مبنا هستند.

## نشست، context و قابلیت

همهٔ مسیرهای این راهنما، جز public Auth، نشست Bearer از نوع **Opaque** لازم دارند. نقش سازمانی یا selector ارسالی اثبات دسترسی نیست. PersonalClass در context شخصی قرار دارد؛ `organizationId=null` و `classRef.kind=PersonalClass` را با OrganizationClass مخلوط نکنید.

ورود Platform نیز از نشست موجود Backend استفاده می‌کند: login → نشست معتبر → `GET /api/admin/v1/me` → permissionهای جاری. منبع دسترسی، assignment فعال Platform IAM است؛ Organization Manager به معنی Platform Admin نیست. مخفی‌کردن UI امنیت ایجاد نمی‌کند و هر endpoint مجوز جاری را سمت سرور دوباره بررسی می‌کند.

| بازیگر | اختیار واقعی |
|---|---|
| حساب فعال | تشکیل draft درخواست؛ ایجاد عضویت یا کلاس با دانستن شناسه مجاز نمی‌شود |
| درخواست‌کننده | دیدن درخواست خود، ویرایش/submit/cancel طبق `capabilities` و state |
| Manager محلی | مدیریت کلاس و اعضا؛ اختیار آموزشی خودکار ندارد |
| Teacher محلی | اختیار آموزشی پیش‌فرض ندارد؛ فقط تفویض مؤثر همان کلاس |
| Learner محلی فعال | فعالیت منتشرشده و تلاش/نتیجهٔ مجاز خودش در membership period مربوط |
| Platform reviewer | بررسی تشکیل؛ این permission دسترسی خصوصی آموزشی نمی‌دهد |
| Parent/Guardian | هیچ دسترسی ضمنی به پیام، پاسخ یا نمرهٔ PersonalClass ندارد |
| School Manager/Teacher | هیچ دسترسی ضمنی به PersonalClass ندارد |

سه اختیار مستقل `AuthorAssignments`, `AuthorExams`, `Grade` از Platform به membership مدیر مشخص اعطا می‌شوند. مدیر فقط subset اختیار مؤثر خودش را به Teacher تفویض می‌کند. لغو grant مبنا، تفویض وابسته را بی‌اثر می‌کند. انتقال مدیریت grant را به مدیر جدید منتقل نمی‌کند. UI از capabilities پاسخ استفاده کند و بعد از تغییر دسترسی دوباره refetch کند.

## تشکیل و setup

در جدول‌ها `R=/api/v1/personal-class-requests` و `C=/api/v1/personal-classes` است. Request/Response نام DTO واقعی است؛ فیلدهای دقیق schema از OpenAPI خوانده شوند.

| Method / Route | Request | Response / status | پیش‌شرط |
|---|---|---|---|
| POST R | CreatePersonalClassRequestCommand: clientOperationId + details | PersonalClassRequestDto / 201 | حساب فعال |
| GET R | status?, page, pageSize | PersonalClassPage / 200 | درخواست‌های خود |
| GET R/{id} | — | PersonalClassRequestDto / 200 | درخواست خود |
| PUT R/{id} | EditPersonalClassRequestCommand | PersonalClassRequestDto / 200 | expectedVersion و state قابل ویرایش |
| POST R/{id}/submit | PersonalClassVersionCommand | PersonalClassRequestDto / 200 | draft/NeedsChanges معتبر |
| POST R/{id}/cancel | PersonalClassVersionCommand | PersonalClassRequestDto / 200 | state مجاز |
| GET R/{id}/history | page, pageSize | صفحهٔ PersonalClassDecisionDto / 200 | history قابل مشاهدهٔ خود، بدون actor پلتفرم |
| GET C/page | page, pageSize | صفحهٔ PersonalClassDto / 200 | عضویت جاری |
| GET C/{classId} | — | PersonalClassDto / 200 | context معتبر |
| PUT C/{id}/settings | ConfigurePersonalClassCommand | PersonalClassDto / 200 | مدیر جاری؛ capacity صریح و acceptingJoinRequests |
| POST C/{id}/activate یا /suspend یا /archive | PersonalClassReasonCommand | PersonalClassDto / 200 | مدیر جاری و transition معتبر |
| GET C/{id}/spaces | — | OfficialSpacesPairResult / 200 | دسترسی جاری |
| POST C/{id}/spaces/repair | RepairPersonalClassSpacesCommand | بدون بدنه / 202 | مدیر مجاز؛ expectedVersion؛ plan hash در صورت نیاز |
| GET C/{id}/spaces/backfill-plan | — | PersonalClassBackfillPlanRow / 200 | مدیر جاری |

stateهای درخواست از enum واقعی `PersonalClassRequestStatus` خوانده شوند. `NeedsChanges` پایان دائمی نیست؛ کاربر طبق capabilities ویرایش و submit می‌کند. Approved الزاماً به معنی Readyبودن فضا نیست؛ `progress`, `state` و پاسخ spaces جدا خوانده شوند. ایجاد مستقیم قدیمی `POST C` میان‌بری برای دورزدن approval نیست و رد می‌شود.

`details`: title غیرخالی تا 200، purpose غیرخالی تا 2000، description اختیاری تا 4000، suggestedCapacity حداقل 1؛ طول متن پس از Trim است. `suggestedManagerUserId` و `suggestedTeacherUserId` پیشنهادند و consent/authority ایجاد نمی‌کنند. دلیل تغییرات مدیریتی غیرخالی تا 500 است. ظرفیت فقط Learner را می‌شمارد؛ Manager/Teacher بدون Learner صندلی آموزشی مصرف نمی‌کنند.

## دعوت، درخواست عضویت و انتقال

| Method / Route | Request | Response | اختیار |
|---|---|---|---|
| POST C/{classId}/invitations | InvitePersonalClassStudentCommand | PersonalClassInvitationDto / 201 | مدیر/مجوز جاری مدیریت اعضا |
| GET C/invitations/page | page, pageSize | صفحهٔ دعوت‌ها / 200 | مقصد یا مدیر مجاز |
| POST C/invitations/{invitationId}/accept | بدون command | PersonalClassDto / 200 | مقصد همان دعوت معتبر |
| POST C/invitations/{id}/decline یا /revoke | PersonalClassReasonCommand | PersonalClassInvitationDto / 200 | مقصد/مدیر طبق عملیات |
| POST C/{id}/join-requests | CreatePersonalClassJoinCommand | PersonalClassJoinDto / 201 | پذیرش درخواست صریحاً باز باشد |
| GET C/join-requests | classId?, page, pageSize | صفحهٔ درخواست‌ها / 200 | بدون classId: خود؛ با classId: مدیر همان کلاس |
| POST C/join-requests/{id}/decision | DecidePersonalClassJoinCommand | PersonalClassJoinDto / 200 | مدیر جاری |
| GET C/{id}/memberships | page, pageSize | صفحهٔ PersonalClassMembershipDto / 200 | مدیر: roster؛ عضو دیگر: رکورد مجاز خود |
| PUT C/{id}/memberships/{memberId}/roles | ChangePersonalClassRolesCommand | PersonalClassMembershipDto / 200 | مدیر و version جاری |
| DELETE C/{id}/memberships/{memberId} | PersonalClassReasonCommand در body | بدون بدنه / 204 | مدیر؛ محافظ آخرین مدیر |
| POST C/{id}/manager-transfers | CreatePersonalClassTransferCommand | PersonalClassTransferDto / 201 | مدیر جاری؛ شناسهٔ قصد و target |
| GET C/manager-transfers | classId?, page, pageSize | صفحهٔ انتقال‌ها / 200 | طرف انتقال یا مدیر مجاز |
| POST C/manager-transfers/{id}/decision | DecidePersonalClassTransferCommand | PersonalClassTransferDto / 200 | consent مقصد طبق state |
| GET C/{classId}/academic-permissions | page, pageSize | صفحهٔ PersonalClassAcademicPermissionDto / 200 | مدیر جاری |
| PUT C/{classId}/academic-permissions/{membershipId} | SetPersonalClassAcademicPermissionCommand | PersonalClassAcademicPermissionDto / 200 | تفویض subset اختیار مؤثر مدیر |
| GET C/{classId}/history | page, pageSize | صفحهٔ PersonalClassHistoryItem / 200 | فقط مدیر جاری؛ بدون review داخلی پلتفرم |

نقش محلی را با نقش سازمانی یا grant آموزشی یکی نکنید. رد ظرفیت/پذیرش/رضایت را به موفقیت خوش‌بینانه تبدیل نکنید. پیش‌فرض ۷روزهٔ دعوت اعضای اولیهٔ Messenger، policy مستقل است و نباید برای دعوت PersonalClass یا Guardian فرض شود؛ `expiresAtUtc` واقعی پاسخ این حوزه را نمایش دهید.

## تکلیف، آزمون و فایل

`A=/api/v1/academic`. ساخت و لیست از class route شخصی و ادامهٔ چرخه از route مشترک resource انجام می‌شود. هیچ organization/enrollment ساختگی لازم نیست.

| جریان | Route / Request | Response و مجوز |
|---|---|---|
| ساخت تکلیف | POST A/personal-classes/{classId}/assignments؛ CreateAssignmentDraftCommand | AssignmentResult / 201؛ AuthorAssignments |
| لیست تکلیف | GET همان مسیر؛ AcademicListQuery | AcademicPage / 200؛ Learner یا AuthorAssignments/Grade مؤثر |
| ویرایش draft | PATCH A/assignments/{id}؛ UpdateAssignmentDraftCommand | AssignmentResult؛ دو rowversion جاری |
| انتشار | POST A/assignments/{id}/publish؛ PublishAssignmentCommand | PublishAssignmentResult؛ AuthorAssignments |
| تلاش تکلیف | POST A/assignments/{id}/attempts | SubmissionAttemptResult / 201؛ Learner مجاز |
| ذخیره متن | PATCH A/submission-attempts/{id}/draft؛ SaveSubmissionDraftCommand | SubmissionAttemptResult؛ expectedRowVersion |
| فایل پاسخ | POST A/submission-attempts/{id}/attachments؛ AttachSubmissionFileCommand | تلاش به‌روز؛ مالک draft و policy فعالیت |
| ثبت نهایی | POST A/submission-attempts/{id}/submit؛ FinalSubmitAssignmentCommand | SubmissionReceipt؛ نسخه/مهلت/تلاش مجاز |
| ارزیابی | PUT A/submission-attempts/{id}/evaluation؛ SaveEvaluationDraftCommand | EvaluatorEvaluationResult؛ Grade مؤثر |
| انتشار نتیجه | POST A/submission-attempts/{id}/evaluation/release؛ ReleaseEvaluationCommand | GradeReleaseResult؛ Grade مؤثر |
| اصلاح نتیجه | POST A/submission-attempts/{id}/evaluation/corrections؛ CorrectReleasedEvaluationCommand | revision جدید / 201؛ سپس release صریح |
| نتیجهٔ خود | GET A/submission-attempts/{id}/result | StudentReleasedResult؛ نتیجهٔ منتشرشده و دورهٔ عضویت مجاز |
| ساخت/لیست آزمون | POST/GET A/personal-classes/{classId}/exams | ExamEditorDto / 201 یا AcademicPage؛ مجوز مستقل AuthorExams/خواندن |
| سؤال | POST A/exams/{id}/questions؛ AddExamQuestionCommand | editor نسخهٔ به‌روز؛ AuthorExams |
| انتشار آزمون | POST A/exams/{id}/publish؛ PublishExamCommand | editor منتشرشده؛ دو rowversion |
| preview | GET A/exams/{id}/student-preview یا /teacher-preview | DTO مخصوص مخاطب؛ بدون افشای answer key به Learner |
| شروع | POST A/exams/{id}/attempts/start؛ StartExamCommand | StudentExamAttemptDto / 201؛ ClientOperationId، مهلت authoritative سرور |
| lease | POST A/exam-attempts/{id}/write-lease | ExamWriteLeaseDto؛ session و membership جاری |
| autosave | PUT A/exam-attempts/{id}/answers/{questionId} | revision؛ lease/version/ClientOperationId معتبر |
| پیوست پاسخ | POST A/exam-attempts/{id}/answers/{questionId}/attachments | revision/answerSetVersion؛ policy همان سؤال |
| finalize | POST A/exam-attempts/{id}/finalize؛ FinalizeExamCommand | receipt ثابت؛ answerSetVersion و lease معتبر |
| نمرهٔ draft | POST/GET A/exam-attempts/{id}/grading | ExamGradeDto؛ Grade مؤثر |
| بررسی پاسخ | PATCH A/exam-attempts/{id}/grading/questions/{questionId} | ExamGradeDto؛ expectedGradeRowVersion |
| تکمیل بررسی | POST A/exam-attempts/{id}/grading/complete | ExamGradeDto؛ انتشار خودکار نیست |
| انتشار/اصلاح | POST A/exam-attempts/{id}/grade/release یا /grade/corrections | release یا revision / 201 برای correction؛ Grade مؤثر |
| نتیجهٔ خود آزمون | GET A/exam-attempts/{id}/result | StudentExamResultDto؛ score پیش از release مخفی است |

نام DTO هر route را از schema تولیدی همان route بگیرید؛ جدول مسیر مصرف است و جایگزین فیلدهای کامل OpenAPI نیست. کنترل deadline، attempt limit، write lease و version در سرور برقرار است. نمرهٔ draft و یادداشت خصوصی به learner/Parent/Platform reviewer نمایش داده نمی‌شوند.

آپلود: `POST /api/v1/files` با multipart شامل `File` و `PersonalClassId`، سپس استفاده از `fileAssetId` در command مربوط. دریافت: `GET /api/v1/files/{id}/content`. Uploaderبودن یا دانستن FileId مجوز دانلود پاسخ دیگران نمی‌دهد؛ عضویت/فعالیت/attempt جاری دوباره بررسی می‌شود. policy تعداد/MIME/حجم سؤال و Storage هر دو لازم‌اند. ReviewRequired در Messaging مجوز عبور Safety در Academic یا برعکس نیست.

## صفحه‌بندی canonical و مهاجرت

| لیست | route canonical | default / bounds |
|---|---|---|
| درخواست تشکیل | GET R و نسخهٔ Admin | page=1؛ size=30، 1..100؛ page<=1000000 |
| کلاس‌های خود | GET C/page | همان Core |
| دعوت | GET C/invitations/page | همان Core |
| عضویت | GET C/{id}/memberships | همان Core |
| JoinRequest/Transfer/History/Grant | GET مسیرهای بالا | همان Core |
| تکلیف/آزمون کلاس | GET A/personal-classes/{id}/assignments یا /exams | page=1؛ size=25، 1..100؛ page<=100000؛ search خام<=200 |
| ارسال‌های تکلیف | GET A/assignments/{id}/submitted-attempts/page | همان Academic؛ ارزیاب مجاز |
| تلاش‌های خود تکلیف | GET A/assignments/{id}/attempts/me/page | همان Academic؛ مالک |
| صف ارزیابی | GET A/evaluations/queue/page?classKind=PersonalClass&classId={id} | همان Academic؛ Grade مؤثر |
| صف تصحیح آزمون | GET A/exams/{id}/grading/page | همان Academic؛ Grade مؤثر |
| تلاش‌های خود آزمون | GET A/exams/{id}/attempts/me/page | همان Academic؛ مالک |

هر صفحه `items`, `page`, `pageSize`, `totalCount` دارد. Academic نام قدیمی `total` را نیز برابر totalCount برمی‌گرداند. count و pagination در SQL و پس از اعمال access/filter انجام می‌شوند؛ ordering دارای tie-breaker شناسه است. هم‌زمانی درج/حذف می‌تواند snapshot صفحات بعدی را تغییر دهد؛ این قرارداد snapshot چنددرخواستی نیست.

raw-arrayهای قدیمی باقی‌اند. `GET C`، `C/invitations` و participants قدیمی، راه‌حل صفحه‌بندی جدید نیستند و Core قدیمی سقف 100 دارد. queue/attempt arrayهای قدیمی به‌تدریج با `/page` جایگزین شوند. کلاینت جدید همهٔ داده‌ها را با دریافت یک صفحهٔ اول کامل فرض نکند؛ حذف route قدیمی در این مرحله انجام نشده است.

## Retry، خطا و وضعیت UI

- همان قصد: همان ClientOperationId و payload؛ payload متفاوت با همان شناسه conflict است. receipt را از پاسخ server نگه دارید؛ پس از قطع شبکه عملیات جدید نسازید.
- expectedVersion در Core رشتهٔ Base64 rowversion است. **نسخهٔ کهنه Core برابر 409** است؛ در Academic وضعیت 412 در مسیرهای مربوط برقرار است. refetch و تصمیم مجدد لازم است؛ overwrite کور انجام ندهید.
- 401: refresh طبق contract یا ورود مجدد؛ 403: capability، permission یا authorization جاری کافی نیست؛ 404: پیام عمومی «در دسترس نیست»، بدون افشای وجود منبع.
- 400: خطای فیلد/فرمت؛ 409: state/capacity/idempotency؛ 422: policy واقعی؛ 429: رعایت Retry-After؛ 503: وابستگی موقت و retry کنترل‌شده با همان شناسهٔ قصد.
- loading را از empty مجاز جدا کنید؛ `items=[]` با totalCount=0 خطا نیست. provisioning Pending/Failed را Ready نمایش ندهید. نتیجهٔ منتشرنشده «صفر» نیست؛ score غایب/مخفی را «هنوز منتشر نشده» نمایش دهید.
- ProblemDetails را با `code` تفسیر کنید؛ traceId/correlationId فقط برای پشتیبانی، بدون توکن یا پاسخ خصوصی، نگه داشته شوند.

## Realtime و نگاشت Flow

قرارداد کامل: [کاتالوگ نسخهٔ 1](PERSONAL_CLASS_V2_EVENT_CATALOG_FA.md). account-level `ClientInvalidated`، replay و refetch موجود استفاده می‌شوند. reconnect بدون catch-up کافی نیست. روی `access`، capabilities و لیست‌های جاری را دوباره بخوانید؛ روی کلید آموزشی resource مربوط را refetch کنید.

تصویر `docs/project-context/04_Flow.png` جریان عمومی آموزشگاه را نشان می‌دهد و screen ID اختصاصی PersonalClass ندارد. جدول زیر flowهای تأییدشده را map می‌کند و صفحه/شناسهٔ ساختگی معرفی نمی‌کند.

| UI Flow | Route | Request → Response | Capability | Realtime key |
|---|---|---|---|---|
| درخواست تشکیل و اصلاح | R، /submit، /history | details/version → request/progress | capabilities درخواست | access |
| بررسی پلتفرم | /api/admin/v1/personal-class-requests/{id}/review | decision/reason/version → request | platform.personal_classes.review | access |
| تنظیم و مدیریت کلاس | C/{id}، settings، memberships | version/roles → detail/page | CanManageMembers/CanManageRoles | access |
| دعوت و درخواست عضویت | invitations و join-requests | intent/version → دعوت/درخواست | consent و اختیار جاری | access |
| فضای گروه/کانال | C/{id}/spaces | — → pair state | دسترسی جاری | access + Messaging stream موجود |
| تکلیف و پاسخ | assignments و submission-attempts | draft/receipt → وضعیت سرور | AuthorAssignments یا Learner | assessment.assignment/submission |
| ارزیابی و نتیجه | evaluation، result | score/version → release | Grade یا مالک نتیجه | assessment.grade |
| آزمون و پاسخ | exams، exam-attempts | intent/lease/revision → receipt | AuthorExams یا Learner | assessment.exam/exam-attempt |
| نتیجه و اصلاح آزمون | grading، grade، result | revision/release → نتیجه | Grade یا مالک نتیجه | assessment.exam-grade |
| تاریخچه مدیریت | C/{id}/history | page → safe metadata | مدیر جاری | پس از access/کلید آموزشی، refetch |

## AI و تصمیم‌های باز

AI Contract v1 حفظ شده؛ این مرحله AI Runtime فعال نمی‌کند. PersonalClass را بدون پشتیبانی صریح عملیات به context AI تزریق نکنید. retrieval منبع تا RAG vNext با `AI_SOURCE_RETRIEVAL_NOT_AVAILABLE` بسته است. config موجود، اثبات AI operational نیست. سیاست Owner عدم الزام Service JWT/WireGuard است؛ تغییر runtime فعلی نیازمند task مستقل است و اینجا انجام نشده است.

دسترسی تاریخی بعد از خروج، retention، آزمون در حال اجرا هنگام archive/suspend و emergency termination، مدیر اضافی، قیمت/payer، دسترسی ویژهٔ Parent، Study Studio و گزارش پیشرفته هنوز Product-open هستند. رفتار محافظه‌کار فعلی access جاری و منع نوشتن پس از revoke را به‌عنوان تصمیم نهایی نگه‌داری/خاتمه معرفی نکنید. `AGE_BUSINESS_DEPENDENCIES=0`؛ هیچ policy سن جدیدی در این مرحله وجود ندارد.

## پیوست: کدهای خطای واقعی PersonalClass و Assessment

این فهرست از `x-error-codes` سند واقعی همین مرحله استخراج شده است. وضعیت‌ها نگاشت handler فعلی، شامل fallback400، هستند. خانوادهٔ حوزه است؛ همهٔ کدها الزاماً از هر route صادر نمی‌شوند. خطاهای عمومی Auth/Storage/Commerce/Platform نیز طبق schema همان operation برقرارند.

| HTTP | Code |
|---|---|
| 400 | `ACADEMIC_PERIOD_INVALID` |
| 400 | `ACADEMIC_QUERY_INVALID` |
| 400 | `ASSIGNMENT_ATTEMPTS_INVALID` |
| 400 | `ASSIGNMENT_DESCRIPTION_INVALID` |
| 400 | `ASSIGNMENT_DUE_DATE_INVALID` |
| 400 | `ASSIGNMENT_MAX_SCORE_INVALID` |
| 400 | `ASSIGNMENT_TITLE_REQUIRED` |
| 400 | `EVALUATION_CORRECTION_REASON_REQUIRED` |
| 400 | `EVALUATION_SCORE_INVALID` |
| 400 | `EXAM_ANSWER_KEY_INVALID` |
| 400 | `EXAM_ANSWER_OPTION_INVALID` |
| 400 | `EXAM_ANSWER_TYPE_INVALID` |
| 400 | `EXAM_ATTACHMENT_LIMIT_EXCEEDED` |
| 400 | `EXAM_ATTACHMENT_NOT_ALLOWED` |
| 400 | `EXAM_ATTEMPTS_INVALID` |
| 400 | `EXAM_DURATION_INVALID` |
| 400 | `EXAM_GRADE_CORRECTION_REASON_REQUIRED` |
| 400 | `EXAM_GRADE_SCORE_INVALID` |
| 400 | `EXAM_MAX_SCORE_INVALID` |
| 400 | `EXAM_QUESTION_REQUIRED` |
| 400 | `EXAM_QUESTION_SCORE_INVALID` |
| 400 | `EXAM_RANDOMIZATION_INVALID` |
| 400 | `EXAM_SCORE_TOTAL_INVALID` |
| 400 | `EXAM_TITLE_REQUIRED` |
| 400 | `EXAM_WINDOW_INVALID` |
| 400 | `PERSONAL_CLASS_REQUEST_INVALID` |
| 400 | `SUBMISSION_ANSWER_INVALID` |
| 400 | `SUBMISSION_EMPTY` |
| 403 | `ACADEMIC_READ_NOT_ALLOWED` |
| 403 | `ASSIGNMENT_NOT_ALLOWED` |
| 403 | `EVALUATION_NOT_ALLOWED` |
| 403 | `EXAM_ANSWER_NOT_ALLOWED` |
| 403 | `EXAM_ATTEMPT_NOT_ALLOWED` |
| 403 | `EXAM_GRADE_NOT_ALLOWED` |
| 403 | `EXAM_GRADE_RELEASE_NOT_ALLOWED` |
| 403 | `EXAM_NOT_ALLOWED` |
| 403 | `EXAM_STUDENT_PREVIEW_NOT_ALLOWED` |
| 403 | `SUBMISSION_FILE_NOT_ALLOWED` |
| 403 | `SUBMISSION_NOT_ALLOWED` |
| 404 | `ACADEMIC_PERIOD_NOT_FOUND` |
| 404 | `ASSIGNMENT_NOT_FOUND` |
| 404 | `ASSIGNMENT_RECIPIENT_NOT_FOUND` |
| 404 | `EVALUATION_NOT_FOUND` |
| 404 | `EXAM_ATTEMPT_NOT_FOUND` |
| 404 | `EXAM_GRADE_NOT_FOUND` |
| 404 | `EXAM_NOT_FOUND` |
| 404 | `GRADE_NOT_RELEASED` |
| 404 | `PERSONAL_CLASS_NOT_FOUND` |
| 404 | `SUBMISSION_NOT_FOUND` |
| 409 | `ASSIGNMENT_ALREADY_PUBLISHED` |
| 409 | `ASSIGNMENT_NOT_DRAFT` |
| 409 | `ASSIGNMENT_NOT_SUBMITTABLE` |
| 409 | `EVALUATION_ALREADY_RELEASED` |
| 409 | `EVALUATION_NOT_READY_FOR_RELEASE` |
| 409 | `EVALUATION_SUBMISSION_NOT_FINAL` |
| 409 | `EXAM_ALREADY_FINALIZED` |
| 409 | `EXAM_ALREADY_PUBLISHED` |
| 409 | `EXAM_ANSWER_IDEMPOTENCY_CONFLICT` |
| 409 | `EXAM_ANSWER_SET_VERSION_CONFLICT` |
| 409 | `EXAM_ANSWER_VERSION_CONFLICT` |
| 409 | `EXAM_ATTEMPTS_EXHAUSTED` |
| 409 | `EXAM_ATTEMPT_NOT_FINALIZABLE` |
| 409 | `EXAM_ATTEMPT_NOT_WRITABLE` |
| 409 | `EXAM_DEADLINE_PASSED` |
| 409 | `EXAM_FINALIZE_CONFLICT` |
| 409 | `EXAM_FINALIZE_IDEMPOTENCY_CONFLICT` |
| 409 | `EXAM_GRADE_ALREADY_RELEASED` |
| 409 | `EXAM_GRADE_ATTEMPT_NOT_FINALIZED` |
| 409 | `EXAM_GRADE_INCOMPLETE` |
| 409 | `EXAM_GRADE_NOT_RELEASED` |
| 409 | `EXAM_GRADE_QUESTION_NOT_REVIEWED` |
| 409 | `EXAM_GRADE_RELEASE_CONFLICT` |
| 409 | `EXAM_GRADE_RELEASE_IDEMPOTENCY_CONFLICT` |
| 409 | `EXAM_NOT_AVAILABLE_YET` |
| 409 | `EXAM_NOT_DRAFT` |
| 409 | `EXAM_NOT_PUBLISHED` |
| 409 | `EXAM_QUESTION_ORDER_INVALID` |
| 409 | `EXAM_QUESTION_SET_INVALID` |
| 409 | `EXAM_START_IDEMPOTENCY_CONFLICT` |
| 409 | `EXAM_VERSION_LOCKED` |
| 409 | `EXAM_WINDOW_CLOSED` |
| 409 | `EXAM_WRITE_LEASE_HELD_BY_OTHER_SESSION` |
| 409 | `EXAM_WRITE_LEASE_REQUIRED` |
| 409 | `EXAM_WRITE_LEASE_STALE` |
| 409 | `GRADE_RELEASE_CONFLICT` |
| 409 | `PERSONAL_CLASS_CAPACITY_REACHED` |
| 409 | `PERSONAL_CLASS_LAST_MANAGER_REQUIRED` |
| 409 | `PERSONAL_CLASS_MEMBERSHIP_INACTIVE` |
| 409 | `PERSONAL_CLASS_NOT_READY` |
| 409 | `PERSONAL_CLASS_PROVISIONING_FAILED` |
| 409 | `PERSONAL_CLASS_REQUEST_CONFLICT` |
| 409 | `PERSONAL_CLASS_REVIEW_REQUIRED` |
| 409 | `PERSONAL_CLASS_VERSION_CONFLICT` |
| 409 | `SUBMISSION_ATTEMPTS_EXHAUSTED` |
| 409 | `SUBMISSION_DEADLINE_PASSED` |
| 409 | `SUBMISSION_FILE_NOT_READY` |
| 409 | `SUBMISSION_NOT_DRAFT` |
| 409 | `SUBMISSION_VERSION_MISMATCH` |
| 412 | `CONCURRENCY_CONFLICT` |
