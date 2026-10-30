# پیوست پاسخ دست‌نویس تکلیف و آزمون

تاریخ: ۲۰۲۶-۱۰-۳۰. محدوده: فقط Backend اصلی. این سند رفتار پیاده‌شده را از تصمیم‌های باز Product جدا می‌کند. اجرای مرورگر، Frontend، IIS، SMS واقعی، AI واقعی و دیتابیس‌های `MakanApp` و `MakanApp_Test` جزو این پذیرش نیستند.

## نتیجهٔ بررسی تکلیف

مسیر موجود `SubmissionService` از قبل Upload مشترک، ساخت Draft، اتصال/حذف فایل، پاسخ فقط با فایل، Reload، ارسال نهایی، RowVersion و نگه‌داری فایل ارسال‌شده را داشت. مدل‌های واقعی `SubmissionAttempt`، `SubmissionAttachment` و `FileAsset` هستند؛ سیستم موازی ساخته نشد.

ایراد با JPEG، PNG و PDF بازتولید شد: Upload، Ready، Attach، Reload، Submit و خواندن metadata معلم موفق بودند، ولی دانلود فایل توسط معلم مجاز `404` می‌داد. `EfFileAssetBoundAccessResolver` ارتباط `SubmissionAttachment` را نمی‌شناخت و فایل را فقط در مسیر دسترسی uploader بررسی می‌کرد. اصلاح در resolver فایل انجام شد؛ منطق ارسال تکلیف بازنویسی نشد. همان سه سناریو پس از اصلاح با مقایسهٔ SHA-256 موفق شدند.

دانلود تکلیف از مجوز جاری `SubmissionService.GetAttemptAsync` استفاده می‌کند؛ Student باید مالک با Enrollment و OrganizationPerson فعال باشد. معلم فقط پس از Submit و با TeacherAssignment جاری مجاز است. Manager فقط در سازمان جاری و همان دسترسی موجود به تکلیف ارسال‌شده مجاز است. Parent حتی با GuardianRelation، مجوز raw فایل نمی‌گیرد.

## شکاف قبلی آزمون و معماری انتخاب‌شده

قبلاً `SaveExamAnswerCommand` فقط گزینه یا متن می‌پذیرفت و ارتباط فایل با پاسخ آزمون وجود نداشت. پارامترهای این Command تغییر نکرده‌اند؛ عملیات مستقل Attach/Detach اضافه شده‌اند. فایل با `FileAssetId` ارجاع داده می‌شود؛ Base64، URL خارجی، مسیر فایل یا `SubmissionAttempt` مصنوعی وارد مدل پاسخ نشده است.

| لایه | فایل/نوع مهم | مسئولیت و نکتهٔ آموزشی |
|---|---|---|
| Domain | `Assessment/ExamAnswerAttachmentPolicy.cs` | سیاست تغییرناپذیر با سقف صریح مثبت و MIMEهای مجاز؛ مستقل از HTTP و EF |
| Domain | `Assessment/QuestionVersion.cs` | نگه‌داری policy روی نسخهٔ سؤال؛ کپی به نسخهٔ بعد، بدون بازنویسی نسخهٔ منتشرشده |
| Domain | `Assessment/AnswerRevisionAttachment.cs` | ارتباط فایل با یک نسخهٔ مشخص پاسخ، نه یک اشاره‌گر متغیر به آخرین فایل |
| Domain | `Assessment/AnswerRevision.cs` | پاسخ تشریحی فقط با فایل نیز `HasAnswer=true` است؛ سؤال Objective همچنان گزینهٔ معتبر می‌خواهد |
| Application | `Assessment/ExamAttachmentContracts.cs` | Commandهای Attach/Detach، policy و metadata امن |
| Application | `Assessment/ExamAnswerService.Attachments.cs` | کنترل نقش، lease، نسخه، idempotency و ایجاد revision جدید |
| Application | `Assessment/ExamAnswerAttachmentValidator.cs` | اعتبارسنجی یکسان فایل در Save، Attach و Finalize |
| Application | `Assessment/ExamService.Attachments.cs` | تبدیل policy درخواست به Domain و محدود کردن MIMEها به Storage فعلی |
| Application | `Assessment/ExamFinalizationService.cs` | اعتبارسنجی دوباره و تثبیت پاسخ‌های پذیرفته‌شده |
| Application | `Assessment/ExamGradingService.Helpers.cs` | نمایش فایل‌های نسخهٔ نهایی برای evaluator مجاز |
| Infrastructure | `Assessment/ExamAttachmentQuery.cs` | خواندن metadata بدون افشای مسیر ذخیره‌سازی |
| Infrastructure | `Assessment/EfExamAnswerStore.cs` | تراکنش و قفل تلاش، خواندن revision و جلوگیری از مصرف فایلِ context دیگر |
| Infrastructure | `Storage/EfFileAssetBoundAccessResolver.Academic.cs` | بررسی دسترسی جاری به فایل تکلیف و آزمون |
| Infrastructure | `Storage/TerminalFileCleanupProcessor.cs` | حفظ فایل‌هایی که به سابقهٔ revision متصل‌اند |
| Infrastructure | `Persistence/Configurations/Assessment/AnswerRevisionAttachmentConfiguration.cs` | PK، FK مرکب و indexهای SQL Server |
| Api | `Controllers/ExamsController.cs` | انتقال HTTP به use case؛ بدون SQL یا قانون کسب‌وکار |
| Api | `OpenApi/FileAndMessagingOperationTransformer.cs` | توضیح قرارداد جدید و خطاها با حفظ قرارداد binary قبلی |

جریان واقعی: `ExamsController` → `ExamAnswerService` → `ExamAnswerAttachmentValidator` و قواعد `ExamAttempt`/`AnswerRevision` → `IExamAnswerStore` و `IFileAssetStore` → `EfExamAnswerStore`/`EfFileAssetStore` → SQL Server → `ExamAnswerReceiptDto`. دانلود از `FilesController` → `StorageService` → resolver مجوز جاری → storage موجود عبور می‌کند. وابستگی Domain به لایهٔ بیرونی اضافه نشده است.

## سیاست پیوست و Snapshot

Policy هر `QuestionVersion` شامل `AttachmentsAllowed`، `TextRequired`، `AllowedContentTypes`، `MaximumCount` و `MaximumTotalBytes` است. فعال‌کردن policy مستلزم تعیین صریح سقف مثبت تعداد و مجموع بایت و فهرست MIME مجاز است. هیچ سقف تجاری، تعداد یا حجم سراسری حدسی اضافه نشده است. سقف موجود Storage برای هر فایل نیز مستقل اعمال می‌شود.

سؤال‌های قدیمی و درخواست ایجاد سؤال بدون policy: پیوست غیرفعال، متن طبق رفتار قبلی، سقف‌ها و MIME برابر null. برای تغییر Draft بدون تغییر policy، property آن را حذف کنید؛ برای غیرفعال‌کردن صریح، `attachmentsAllowed=false`، `textRequired=false` و سه فیلد محدودیت را null/خالی بفرستید.

سیاست فعال نمونهٔ آموزشی، صرفاً نمونه و نه Default سیستم:

```json
{
  "attachmentPolicy": {
    "attachmentsAllowed": true,
    "textRequired": false,
    "allowedContentTypes": ["image/jpeg", "image/png", "application/pdf"],
    "maximumCount": 3,
    "maximumTotalBytes": 4096
  }
}
```

مقدار واقعی سقف‌ها را سازندهٔ سؤال انتخاب می‌کند. فقط subset مجاز `Storage.AllowedContentTypes` پذیرفته می‌شود. WebP اضافه نشده است. JPEG/PNG/PDF طبق validation و signature موجود Storage بررسی می‌شوند. `TextRequired=true` فقط برای تشریحی مجاز است و هنگام Finalize اعمال می‌شود؛ Draft می‌تواند موقتاً متن خالی داشته باشد. Objective فقط با policy صریح، ابتدا ذخیرهٔ گزینهٔ معتبر و سپس Attach، گزینه+فایل را می‌پذیرد؛ نمره‌دهی Objective تغییر نمی‌کند.

`ExamAttemptQuestion.QuestionVersionId` به سؤال همان نسخهٔ تثبیت‌شده اشاره می‌کند. ساخت و انتشار نسخهٔ جدید، policy تلاش قبلی را تغییر نمی‌دهد. `CopyTo` سیاست را به سؤال جدید کپی می‌کند؛ Update نسخهٔ منتشرشده مجاز نیست.

## چرخهٔ فایل، Revision و هم‌زمانی

Upload همان `POST /api/v1/files` است. فایل باید Ready، دارای hash، متعلق به User جاری و همان سازمان، با MIME/حجم مجاز و قابل استفاده در همان Attempt باشد. فایل مصرف‌شده در Attempt دیگر یا Submission، Lesson، Message، ScheduledMessage، ModerationEvidence، Avatar و ReportExport برای واردکردن به این تلاش پذیرفته نمی‌شود. فایل unattached منقضی‌شده قابل اتصال نیست.

هر Attach/Detach یک `AnswerRevision` جدید با مجموعهٔ کامل فایل‌های جاری می‌سازد. Save متن/گزینه نیز فایل‌های قبلی را به revision تازه کپی می‌کند؛ حذف فایل فقط از راه Detach است. revision قبلی پاک یا mutate نمی‌شود. فایل از اولین اتصال معتبر Retain می‌شود تا تاریخچه و عملیات حذف فایل با هم مسابقه ندهند. حذف پیوست فایل فیزیکی را حذف نمی‌کند؛ سیاست حذف تاریخچه/آرشیو در این مرحله اضافه نشده است. حفظ blob به‌معنی مجوز دانلود revision تاریخیِ حذف‌شده نیست.

تراکنش Serializable و قفل `UPDLOCK,HOLDLOCK` موجود روی Attempt، Save/Attach/Detach/Finalize را مرتب می‌کند. `ExpectedRevisionNumber` برای اولین پاسخ null و پس از آن دقیقاً شمارهٔ revision جاری است. `WriteLeaseVersion` و مالک نشست جاری بررسی می‌شوند. انتقال lease، نشست قبلی را برای تغییر جدید نامعتبر می‌کند. `ClientOperationId` و hash نوع عملیات/فایل/نسخه/lease، retry همان درخواست را به receipt قبلی برمی‌گردانند؛ همان شناسه با payload متفاوت Conflict است. retry idempotent یک عملیات پذیرفته‌شده بعد از Finalize نیز receipt قبلی است و تغییر جدیدی ایجاد نمی‌کند.

`ExamAttempt.AnswerSetVersion` با هر mutation موفق یک واحد زیاد می‌شود؛ Outbox و invalidation قبلی `assessment.exam-attempt` پس از Commit reuse می‌شوند. payload فایل/متن به event اضافه نشده، stream یا Outbox جدیدی ساخته نشده و پاسخ HTTP لزوماً به معنی exactly-once delivery رویداد نیست.

## Finalize و تصحیح

همان `FinalizeExamCommand` شامل `ClientOperationId`، `ExpectedAnswerSetVersion` و `WriteLeaseVersion` باقی است. Finalize قبل از تثبیت، مالکیت، Enrollment، نشست، policy و Ready بودن فایل‌ها را دوباره زیر تراکنش بررسی می‌کند. زمان سرور پس از گرفتن قفل و پس از اعتبارسنجی دوباره خوانده می‌شود. شرط مهلت همان `now < EffectiveDeadlineUtc` است؛ ارفاق Upload یا late grace جدید نداریم. در لحظهٔ deadline، mutation و Finalize جدید رد می‌شوند.

`ExamFinalAnswer.AnswerRevisionId` دقیقاً revision نهایی و در نتیجه مجموعهٔ فایل همان revision را قفل می‌کند. فایل Pending/Rejected/Deleted مانع Finalize است؛ هیچ وضعیت موفق کاذب ساخته نمی‌شود. قواعد قدیمی اجازهٔ نهایی‌سازی سؤال بی‌پاسخ حفظ شده‌اند، مگر `TextRequired` صریح جدید. پاسخ تشریحی فقط با عکس، پاسخ‌داده‌شده و نیازمند review دستی نمره توسط evaluator موجود است؛ workflow انسانی Safety جدید ایجاد نشده است.

## مجوز و Privacy

هر metadata/download دوباره Session، workspace، نقش، سازمان و رابطهٔ جاری را بررسی می‌کند. Student فقط صاحب Attempt با Enrollment و OrganizationPerson فعال است. Teacher فقط با TeacherAssignment جاری و Attempt نهایی‌شده raw فایل را می‌خواند. Manager در سازمان جاری فقط مطابق مجوز صریح موجود تصحیح Attempt نهایی‌شده مجاز است. Parent و Guardian raw attachment نمی‌گیرند؛ read-model نتیجهٔ منتشرشدهٔ قبلی تغییر نکرده است. نسخهٔ تاریخی detached منبع مجوز نیست. شناسهٔ قابل حدس، scope شخصی یا سازمان دیگر مجوز نیست.

metadata پاسخ آزمون: `fileAssetId`، `originalFileName`، `contentType`، `sizeBytes`، `fileStatus`، `canDownload` و `downloadPath` داخلی. برای فایل غیرآماده `canDownload=false` و path برابر null است. مسیر فیزیکی، StorageKey، URL خارجی و کلید storage در این DTO نیست. endpoint دانلود همچنان bytes و Content-Type واقعی با cache خصوصی `no-store` برمی‌گرداند؛ Client باید Error JSON را از bytes موفق تفکیک کند.

`Ready` فقط وضعیت اعتبارسنجی FileAsset است. تکلیف از قبل مصرف‌کنندهٔ فایل آموزشی بود و به موتور text-only Messenger متصل نبود. پیوست آزمون از همین زیرساخت آموزشی استفاده می‌کند؛ مسیر فایل متصل به پروندهٔ Moderation قابل واردکردن نیست. هیچ check پیام‌رسان حذف/disable نشده است. `BE-01 ReviewRequired` همچنان مسدود به تصمیم Product است؛ این Task آن را حل یا به Approved تبدیل نمی‌کند و Human Review/AbuseReview جدیدی نمی‌سازد.

## دیتابیس و Migration

Migration: `20261030060025_AddExamAnswerAttachments`.

- جدول جدید `assessment.AnswerRevisionAttachments`: `AnswerRevisionId` و `FileAssetId` به‌عنوان PK مرکب، به‌همراه `OrganizationId`، `ExamAttemptId`، `ExamAttemptQuestionId` و `AttachedAtUtc`.
- FK چهارستونی به Alternate Key موجود AnswerRevision مانع اتصال metadata به سازمان/تلاش/سؤال دیگر است؛ FK به FileAsset و رفتار Restrict مانع حذف تصادفی رکورد مرجع است.
- index روی FileAsset، FK مرکب و `(OrganizationId, ExamAttemptId, FileAssetId)` برای مجوز و جست‌وجوی ارتباط.
- پنج ستون policy روی QuestionVersions: دو bool با default=false برای سازگاری دادهٔ قبلی، count/bytes/MIME nullable؛ هیچ Default عددی count/bytes ایجاد نشده است.
- CHECK policy: در حالت غیرفعال همهٔ محدودیت‌ها null؛ در حالت فعال محدودیت‌ها صریحاً NOT NULL و مثبت و MIME غیرخالی است. بررسی `IS NOT NULL` جلوی قبول سه‌حالتهٔ UNKNOWN در SQL CHECK را می‌گیرد.
- RowVersion فعلی Attempt و AttemptQuestion حفظ شده است؛ ارتباط immutable، RowVersion جدا لازم ندارد. قفل تلاش، optimistic revision و unique operation با هم استفاده می‌شوند.
- Up فقط additive است و پاسخ/سؤال قبلی را UPDATE/DELETE نمی‌کند. Down مربوط به همین schema جدید است؛ در این Task روی محیط واقعی اجرا نشده است.

تست Migration یک دیتابیس تصادفی با prefix `MakanApp_AcademicAttachment_Migration_` روی LocalDB می‌سازد، تا migration قبلی بالا می‌آورد، سؤال/پاسخ قدیمی درج می‌کند و سپس ارتقا و تکرار بدون تغییر داده را بررسی می‌کند. دیتابیس ساخته‌شده پس از تست حذف می‌شود؛ دیتابیس‌های کاربر دست‌نخورده‌اند.

## FRONTEND_HANDOFF

همهٔ مسیرها با Session احرازشده و context سازمانی جاری‌اند. `attemptQuestionId` از پاسخ Start/Get Attempt است و با `questionVersionId` جایگزین نمی‌شود.

### تکلیف

| اقدام | Method و Route واقعی | ورودی و نتیجه |
|---|---|---|
| ساخت Draft | `POST /api/v1/academic/assignments/{assignmentId}/attempts` | نتیجه `SubmissionAttemptResult`، RowVersion جاری |
| Upload | `POST /api/v1/files` | multipart فیلد `File`؛ `FileAssetResult` |
| بررسی وضعیت | `GET /api/v1/files/{fileId}` | فقط Ready را برای اتصال انتخاب کنید؛ Pending را موفق نهایی ندانید |
| اتصال | `POST /api/v1/academic/submission-attempts/{attemptId}/attachments` | `{fileAssetId, expectedRowVersion}`؛ نتیجه Draft و RowVersion جدید |
| حذف اتصال | `DELETE /api/v1/academic/submission-attempts/{attemptId}/attachments/{fileId}?expectedRowVersion=...` | حذف رابطهٔ Draft؛ RowVersion Base64 را URL-encode کنید |
| ذخیرهٔ متن | `PATCH /api/v1/academic/submission-attempts/{attemptId}/draft` | `{answerText, expectedRowVersion}`؛ فایل قبلی حفظ می‌شود |
| Reload | `GET /api/v1/academic/submission-attempts/{attemptId}` | متن و attachments اگر ContentVisible مجاز باشد |
| ارسال نهایی | `POST /api/v1/academic/submission-attempts/{attemptId}/submit` | `{expectedRowVersion}`؛ متن یا فایل Ready کافی است |
| دانلود معلم | `GET /api/v1/files/{fileId}/content` | bytes، فقط Submit معتبر و TeacherAssignment جاری |

### آزمون

| اقدام | Method و Route واقعی | ورودی و نتیجه |
|---|---|---|
| ایجاد/ویرایش سیاست | `POST /api/v1/academic/exams/{examId}/questions` و `PATCH /api/v1/academic/exams/{examId}/questions/{questionId}` | همان DTO قبلی به‌همراه `attachmentPolicy` اختیاری؛ optimistic RowVersionهای موجود |
| مشاهدهٔ سیاست | `GET /api/v1/academic/exam-attempts/{attemptId}` | `questions[].attachmentPolicy` از نسخهٔ تثبیت‌شده؛ در editor/student-preview نیز وجود دارد |
| Start | `POST /api/v1/academic/exams/{examId}/attempts/start` | `{clientOperationId}`؛ Attempt و AttemptQuestionId |
| lease | `POST /api/v1/academic/exam-attempts/{attemptId}/write-lease` | نسخهٔ lease و AnswerSetVersion؛ انتقال با `.../write-lease/transfer` طبق جریان موجود |
| Upload/Ready | `POST /api/v1/files` و `GET /api/v1/files/{fileId}` | همان جریان تکلیف؛ upload API جدید وجود ندارد |
| Attach | `POST /api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}/attachments` | `{fileAssetId, clientOperationId, writeLeaseVersion, expectedRevisionNumber}`؛ `ExamAnswerReceiptDto` |
| Detach | `DELETE /api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}/attachments/{fileId}?clientOperationId=...&writeLeaseVersion=...&expectedRevisionNumber=...` | Queryها الزامی‌اند؛ revision جدید بدون آن فایل |
| Save متن/گزینه | `PUT /api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}` | Command قبلی؛ فایل‌های جاری حفظ می‌شوند. گزینه+فایل: اول گزینه و سپس Attach |
| Reload | `GET /api/v1/academic/exam-attempts/{attemptId}/answers` | `answers[].attachments`؛ revision و AnswerSetVersion جاری را جایگزین cache کنید |
| Finalize | `POST /api/v1/academic/exam-attempts/{attemptId}/finalize` | `{clientOperationId, expectedAnswerSetVersion, writeLeaseVersion}`؛ receipt پایدار |
| receipt | `GET /api/v1/academic/exam-attempts/{attemptId}/receipt` | همان رسید نهایی، بدون نمرهٔ زودرس |
| تصحیح معلم | `POST /api/v1/academic/exam-attempts/{attemptId}/grading` سپس `GET` همان مسیر | ایجاد/خواندن Draft نمرهٔ موجود؛ `questions[].attachments` فقط از revision نهایی |
| دانلود | `GET /api/v1/files/{fileId}/content` | با احراز هویت جاری؛ دانلود URL عمومی دائمی نیست |

بعد از Conflict، پاسخ و lease جاری را Reload کنید؛ payload جدید را با operation جدید و ExpectedRevisionNumber درست بفرستید. برای retry شبکهٔ همان operation از همان شناسه و payload استفاده کنید. receipt پاسخ Attach یا Save را برای AnswerSetVersion نهایی‌سازی نگه دارید. هنگام پاسخ Pending/Rejected به دریافت‌کننده وعدهٔ فایل تحویل‌شده ندهید.

### خطاهای مهم

| وضعیت | Code واقعی | اقدام |
|---|---|---|
| 400 | `EXAM_ATTACHMENT_NOT_ALLOWED` | policy/MIME/شکل پیوست را بررسی کنید |
| 400 | `EXAM_ATTACHMENT_LIMIT_EXCEEDED` | تعداد یا مجموع بایت را کاهش دهید |
| 400 | `EXAM_ANSWER_TYPE_INVALID` | متن لازم یا گزینهٔ معتبر را مطابق سؤال فراهم کنید |
| 401 | خطای Session فعلی | احراز هویت دوباره؛ token در log ثبت نشود |
| 403 | `EXAM_ATTEMPT_NOT_ALLOWED` | context/نقش مجاز لازم است |
| 404 | `FILE_NOT_FOUND` یا `EXAM_ATTEMPT_NOT_FOUND` | وجود resource نامجاز افشا نمی‌شود |
| 409 | `FILE_NOT_READY` | اتصال/Finalize مجاز نیست؛ Approved فرض نکنید |
| 409 | `EXAM_ANSWER_VERSION_CONFLICT` | Reload پاسخ؛ merge آگاهانه و عملیات تازه |
| 409 | `EXAM_ANSWER_IDEMPOTENCY_CONFLICT` | operationId با payload دیگری استفاده شده است |
| 409 | `EXAM_WRITE_LEASE_REQUIRED`، `EXAM_WRITE_LEASE_STALE`، `EXAM_WRITE_LEASE_HELD_BY_OTHER_SESSION` | lease جاری را طبق قرارداد موجود حل کنید |
| 409 | `EXAM_DEADLINE_PASSED`، `EXAM_ATTEMPT_NOT_WRITABLE` | ویرایش دیرهنگام یا بعد از Finalize مجاز نیست |
| 409 | `EXAM_ANSWER_SET_VERSION_CONFLICT` | AnswerSetVersion فعلی را دریافت کنید |

## تست‌ها و حدود پذیرش

تست API واقعی با TestServer، SQL Server LocalDB موقت، storage موقت و Sessionهای مستقل Student/Teacher اجرا می‌شود؛ مرورگر/UI Frontend آزمایش نشده است. JPEG، PNG و PDF معتبر کوچک، دانلود bytes و مقایسهٔ SHA-256 دارند. recorder فقط method، route ماسک‌شده، status، correlation و تعداد HTTP را نگه می‌دارد؛ body، OTP و token ضبط نمی‌شوند.

Unitهای `ExamAttachmentModelTests`: سقف مثبت، MIME، immutable collection، policy سؤال قدیمی، freeze نسخهٔ منتشرشده و پاسخ attachment-only. Integrationهای `AcademicExamAttachmentTests` و `AcademicExamAttachmentSecurityTests`: جریان واقعی فایل، حفظ متن/فایل، Detach، فایل غیرآماده، مالکیت، tenant، policy، محدودیت، stale revision، lease، idempotency، Race، Finalize، معلم/Guardian، revoke، deadline، OpenAPI، Realtime و constraint. `AcademicExamAttachmentMigrationTests`: ارتقای دادهٔ قدیمی. `AcademicAssignmentAttachmentAcceptanceTests`: بازتولید/رفع دانلود، سه MIME، Guardian/revoke و Pending. تست‌های قبلی Assignment برای rowversion، retry، ownership و deadline نیز در suite کامل اجرا می‌شوند.

ماتریس EXAM-FILE-01 تا EXAM-FILE-20 به ترتیب با تست‌های JPEG/PNG، attachment-only، TextSave، Reload، Detach، NonReadyFile، ForeignStudent، DisabledQuestion، CountTotalBytes، ConcurrentAttachAndTextSave، LeaseTransfer، ConcurrentDuplicateAttach، Finalize E2E، FinalizeRevalidates، Teacher E2E، UnrelatedTeacher، CurrentGuardian، RevokedReaders و suite قبلی Exam به‌همراه Upgrade پوشش داده می‌شود. Commerce و Realtime همان suite موجود‌اند؛ quota جدیدی برای پیوست حدس زده نشده است.

نتیجهٔ کامل Validation و فهرست تفکیک‌شدهٔ تغییرات پس از اجرای نهایی در انتهای همین سند ثبت می‌شود.

## تصمیم‌های طراحی، محدودیت و مسیر مطالعه

انتخاب ارتباط با revision به‌جای جدول mutable مشترک پاسخ، کمی رکورد تاریخی بیشتر می‌سازد ولی Finalize و retry را قابل اثبات می‌کند. سقف explicit per-question به‌جای عدد جهانی، نیاز به تنظیم سازنده دارد ولی قرارداد قدیمی را نمی‌شکند. reuse فایل و دانلود موجود به‌جای upload عمومی مستقل، تعداد مسیرهای امنیتی را کم می‌کند. مجوز جاری، دانلود پس از revoke را می‌بندد ولی bytes قبلاً دانلودشده را از دستگاه دریافت‌کننده پس نمی‌گیرد.

تصمیم‌های باز: workflow نهایی رسانهٔ `ReviewRequired` در Messenger همچنان با Product است؛ raw فایل Parent مجاز نشده و deny باقی است؛ زمان حذف/آرشیو تاریخچهٔ فایل پاسخ سیاست جدیدی نگرفته است. هیچ ارفاق deadline، WebP، frontend preview، AI grading یا quota جدیدی اضافه نشده است. مرحلهٔ بعد فقط review همین diff و هماهنگی UI با قرارداد واقعی است؛ Commit/Publish/Migration سرور نیازمند Task بعدی است.

نکات آموزشی: PK/FK سازمانی با چک Application تکمیل می‌شود؛ RowVersion جای idempotency را نمی‌گیرد؛ ذخیرهٔ immutable مجموعهٔ فایل از lost update جلوگیری می‌کند؛ `Ready` و مجوز خواندن دو چیزند؛ snapshot معتبر به نسخهٔ منتشرشده ارجاع می‌دهد؛ SQL CHECK برای null باید صریح باشد.

مسیر مطالعه: `ExamsController` → `ExamAttachmentContracts` → `ExamAnswerService.Attachments` → `ExamAnswerAttachmentValidator` → `QuestionVersion`/`AnswerRevision` → `EfExamAnswerStore` → EF configuration → Migration → `EfFileAssetBoundAccessResolver.Academic` → تست‌های Academic. مسیر تکلیف را از `SubmissionService` و resolver مشترک بخوانید.

## نتیجهٔ اجرای قبلی، پیش از اجازهٔ Commit/Push

Restore موفق؛ Release Build اول و نهایی موفق با صفر Warning/Error. Regression اول: Unit=922، Integration=1541، Architecture=14، مجموع 2477 موفق. سپس تست ارتقای دادهٔ قدیمی نیز اضافه و اجرا شد. اجرای نهایی کامل: Unit=922، Integration=1542، Architecture=14، مجموع **2478** موفق؛ Failed=0، Skipped=0. ۵۲ تست جدید شامل ۱۲ Unit و ۴۰ Integration هستند. baseline واقعی محفوظ این Task برابر 2426 بود؛ عدد 2421 در درخواست، پنج تست BE-01 قبلی را شامل نمی‌شد.

اعتبارسنجی Migration: fresh schema در fixture و ارتقای دادهٔ قدیمی در LocalDB تصادفی موفق؛ تکرار Migration بدون تغییر داده موفق؛ `dotnet ef migrations has-pending-model-changes` موفق و بدون تغییر pending. ۵۴ Migration قبلی، شامل فایل‌های Designer آن‌ها، بدون تغییر باقی مانده‌اند. فقط Snapshot و Migration جدید تغییر کرده‌اند.

شش E2E با Sessionهای مستقل: JPEG/PNG/PDF تکلیف هرکدام ۲۲ درخواست، آزمون هرکدام ۲۵ درخواست؛ مجموع **۱۴۱ HTTP**. تطابق SHA-256 در ۶/۶ دانلود موفق بود. این نتیجه API→SQL→storage است؛ تست مرورگر یا سرور مستقر نیست. suite کامل، تست‌های قبلی Commerce/Realtime را نیز شامل شد؛ ۵۹ تست با عنوان مرتبط Realtime و ۹۵ تست با عنوان مرتبط Commerce/Commercial/Subscription/Entitlement/Quota موفق‌اند. این شمارش عنوان‌ها subset شواهد suite است، نه تعریف تازهٔ مرز ماژول.

بازتولید اولیهٔ Teacher Download سه شکست مورد انتظار داشت که پس از رفع resolver موفق شدند. در توسعهٔ تست، یک Route اشتباه PUT به PATCH واقعی اصلاح شد و تشخیص `DELETE` در تست Migration به `DELETE FROM` اصلاح شد تا `ON DELETE NO ACTION` را اشتباه حذف داده نداند. هیچ تستی حذف یا غیرفعال نشده است.

بررسی preservation: ۱۳۶۶ فایل baseline بدون مفقودی؛ ۱۸۰ فایل بکاپ با hash صحیح؛ ۹۱ dirty قبلی شامل ۸۸ Realtime/BE-01 و ۳ AI. ۸۶ فایل قبلی عیناً حفظ شده و پنج فایل هم‌پوشان با review محدود تغییر کرده‌اند. کانفیگ، Identity/OTP، SMS/MeliPayamak و AI تغییر نکرده‌اند. اسکن شواهد نهایی بدون یافتهٔ حساس است؛ User Secrets خوانده/نوشته نشده و هیچ credential در گزارش ذخیره نشده است.

طبق بند ۶۹ Task قبلی، در همان اجرا **Stage، Commit، Push، Publish، Deploy یا Migration سرور انجام نشد.** این گزارش تاریخی است؛ مجوز جدید در بخش نهایی‌سازی مشترک انتهای سند ثبت شده است. در پایان آن اجرای قبلی Commit hash/message جدید وجود نداشت؛ branch=`main` و HEAD برابر `8084485477e0f83c1133728c82afe5653d23e916` بود.

### ماتریس پذیرش اجرای قبلی

```text
ASSIGNMENT_ATTACHMENT_API_EXISTS=true
ASSIGNMENT_BUG_REPRODUCED=true
ASSIGNMENT_BUG_FIXED=true
ASSIGNMENT_REAL_E2E_PASS=true
ASSIGNMENT_ATTACHMENT_ONLY_PASS=true
ASSIGNMENT_TEACHER_DOWNLOAD_PASS=true
ASSIGNMENT_CROSS_USER_DENIED=true
EXAM_ATTACHMENT_IMPLEMENTED=true
EXAM_ATTACHMENT_CONTRACT_COMPLETE=true
EXAM_ATTACHMENT_ONLY_PASS=true
EXAM_JPEG_PASS=true
EXAM_PNG_PASS=true
EXAM_PDF_PASS=true
EXAM_ATTACH_PASS=true
EXAM_DETACH_PASS=true
EXAM_RELOAD_PASS=true
EXAM_FINALIZE_PASS=true
EXAM_FILE_OWNERSHIP_ENFORCED=true
EXAM_QUESTION_POLICY_ENFORCED=true
EXAM_PENDING_FILE_DENIED=true
EXAM_LEASE_ENFORCED=true
EXAM_REVISION_CONFLICT_PASS=true
EXAM_IDEMPOTENT_ATTACH_PASS=true
TEACHER_AUTHORIZED_DOWNLOAD_PASS=true
UNRELATED_TEACHER_DENIED=true
UNAUTHORIZED_PARENT_DENIED=true
FILE_PHYSICAL_PATH_NOT_EXPOSED=true
FILE_ID_ALONE_NOT_AUTHORIZATION=true
EXISTING_EXAMS_BACKWARD_COMPATIBLE=true
REALTIME_REGRESSION_PASS=true
COMMERCE_REGRESSION_PASS=true
MIGRATION_SAFE=true
BUILD_PASS=true
FULL_TEST_SUITE_PASS=true
PENDING_MODEL_CHANGES=false
COMMIT=false
PUSH=false
PUBLISH=false
DEPLOY=false
SERVER_MIGRATION=false
```

### گزارش ۳۷‌بندی اجرای قبلی

1. Branch: `main`.
2. HEAD اولیه و نهایی: `8084485477e0f83c1133728c82afe5653d23e916`؛ Commit جدید وجود ندارد.
3. Git اولیه: ۹۱ فایل dirty، index خالی؛ کار قبلی با کار این Task یکی فرض نشد.
4. تغییرهای قبلی Realtime: hashها و پنج هم‌پوشانی بازبینی شدند؛ hookهای موجود Store و Outbox حفظ شدند.
5. معماری تکلیف: `SubmissionService`، `SubmissionAttempt`، `SubmissionAttachment` و FileAsset موجود reuse شدند.
6. بازتولید باگ: بله، JPEG/PNG/PDF تا مرحلهٔ دانلود معلم رسیدند و 404 گرفتند.
7. Root cause تکلیف: resolver فایل، SubmissionAttachment را به‌عنوان binding مجاز نمی‌شناخت.
8. اصلاح تکلیف: افزودن تشخیص binding و reuse مجوز Read فعلی؛ منطق Submit بازنویسی نشد.
9. E2E تکلیف: سه MIME، attachment-only، reload، submit و دانلود دقیق بایت‌ها موفق؛ UI آزمایش نشده است.
10. شکاف قبلی آزمون: پاسخ فقط گزینه/متن بود؛ فایل رابطهٔ Domain نداشت.
11. مدل آزمون: `AnswerRevisionAttachment` immutable و policy در `QuestionVersion`؛ بدون fake Submission.
12. API پیوست آزمون: POST/DELETE مسیر answers/{attemptQuestionId}/attachments؛ جزئیات کامل در FRONTEND_HANDOFF.
13. Policy سؤال: فعال‌سازی صریح، MIME و سقف مثبت تعداد/کل بایت و TextRequired؛ بدون عدد Default حدسی.
14. Snapshot: Attempt به QuestionVersion منتشرشده اشاره می‌کند؛ نسخهٔ جدید سؤال، تلاش قبلی را عوض نمی‌کند.
15. FileAsset: Upload، validation، storage و download موجود؛ Retain برای سابقهٔ revision و محافظت cleanup.
16. Authorization: Student مالک و فعال، evaluator جاری پس از Finalize، Manager طبق مجوز موجود همان سازمان؛ Parent raw deny.
17. Lease: همان write lease موجود؛ انتقال نشست، writer قبلی را برای mutation جدید رد می‌کند.
18. Revision: هر Attach/Detach/Save نسخهٔ جدید؛ Save متن مجموعهٔ فایل جاری را حفظ می‌کند.
19. Idempotency: retry همان operation همان receipt؛ payload متفاوت با شناسهٔ قبلی Conflict؛ duplicate relation/effect ایجاد نمی‌شود.
20. Finalize: بررسی دوبارهٔ policy/فایل/lease/نسخه/مهلت سرور و freeze همان revision؛ ارفاق Upload اضافه نشده است.
21. Migration: `20261030060025_AddExamAnswerAttachments`، افزایشی و تست‌شده فقط روی LocalDB ایزوله؛ model pending=false.
22. OpenAPI: DTOها و مسیرهای واقعی، ProblemDetails، concurrency و binary download مستند و با تست بررسی شدند.
23. Frontend handoff: جدول‌های تکلیف و آزمون همین سند؛ تغییر Frontend انجام نشده است.
24. تست متمرکز جدید: ۵۲ مورد، شامل ۱۲ Unit و ۴۰ Integration؛ همه در اجرای نهایی موفق.
25. Unit نهایی: 922/922 موفق.
26. Integration نهایی: 1542/1542 موفق.
27. Architecture نهایی: 14/14 موفق؛ جهت Onion محفوظ است.
28. Failed/Skipped نهایی: 0/0.
29. Warning/Error Build نهایی: 0/0.
30. E2E: ۱۴۱ درخواست برای شش سناریوی مستقل؛ ۶۶ درخواست تکلیف و ۷۵ آزمون.
31. Hash: ۶/۶ دانلود با SHA-256 فایل ورودی برابر؛ body/secret ثبت نشده است.
32. Realtime regression: موفق؛ invalidation همان Attempt بعد از commit، بدون فایل/متن و با revoke جاری.
33. Commerce regression: موفق؛ foundation قبلی محفوظ، quota حدسی یا مصرف تجاری جدید اضافه نشده است.
34. `git diff --stat`: کل tracked worktree شامل کار قبلی، 71 files / 559 insertions / 106 deletions؛ فایل‌های جدید untracked در این آمار Git نیستند. فهرست مستقل این Task ۴۵ فایل است.
35. Git نهایی: changes عمداً uncommitted، index خالی، هیچ فایل قبلی حذف نشده است. source و فایل‌های private حفظ شدند؛ Commit/Push انجام نشد.
36. ماتریس پذیرش: بلوک بالا؛ همهٔ پذیرش‌های اجرایی موفق و عملیات انتشار/Git طبق محدوده false هستند.
37. تصمیم‌های باز Product: مسیر ReviewRequired پیام‌رسان، سیاست raw Parent و حذف/آرشیو تاریخی خارج از این Task باقی‌اند؛ Safety bypass و Human Safety Review جدید نداریم. قدم بعد review همین diff و تطبیق UI با قرارداد واقعی است.

## تفکیک فایل‌های تغییرکرده

`ACADEMIC_ATTACHMENT_CHANGED_FILES`: تغییرهای این Task نسبت به baseline خصوصی ابتدای کار؛ فهرست زیر شامل فایل جدید و تغییر فایل موجود است.

```text
MakanApp.Api/Controllers/ExamsController.cs
MakanApp.Api/Errors/GlobalExceptionHandler.cs
MakanApp.Api/OpenApi/FileAndMessagingOperationTransformer.cs
MakanApp.Application/Assessment/AssessmentErrorCodes.cs
MakanApp.Application/Assessment/ExamAnswerAbstractions.cs
MakanApp.Application/Assessment/ExamAnswerAttachmentValidator.cs
MakanApp.Application/Assessment/ExamAnswerContracts.cs
MakanApp.Application/Assessment/ExamAnswerService.Attachments.cs
MakanApp.Application/Assessment/ExamAnswerService.cs
MakanApp.Application/Assessment/ExamAttachmentContracts.cs
MakanApp.Application/Assessment/ExamAttemptContracts.cs
MakanApp.Application/Assessment/ExamAttemptService.cs
MakanApp.Application/Assessment/ExamContracts.cs
MakanApp.Application/Assessment/ExamFinalizationContracts.cs
MakanApp.Application/Assessment/ExamFinalizationService.cs
MakanApp.Application/Assessment/ExamGradingContracts.cs
MakanApp.Application/Assessment/ExamGradingService.Helpers.cs
MakanApp.Application/Assessment/ExamService.Attachments.cs
MakanApp.Application/Assessment/ExamService.Helpers.cs
MakanApp.Application/Assessment/ExamService.cs
MakanApp.Domain/Assessment/AnswerRevision.cs
MakanApp.Domain/Assessment/AnswerRevisionAttachment.cs
MakanApp.Domain/Assessment/ExamAnswerAttachmentPolicy.cs
MakanApp.Domain/Assessment/QuestionVersion.cs
MakanApp.Infrastructure/Assessment/EfExamAnswerStore.cs
MakanApp.Infrastructure/Assessment/EfExamFinalizationStore.cs
MakanApp.Infrastructure/Assessment/EfExamGradingStore.cs
MakanApp.Infrastructure/Assessment/ExamAttachmentQuery.cs
MakanApp.Infrastructure/Persistence/Configurations/Assessment/AnswerRevisionAttachmentConfiguration.cs
MakanApp.Infrastructure/Persistence/Configurations/Assessment/AnswerRevisionConfiguration.cs
MakanApp.Infrastructure/Persistence/Configurations/Assessment/QuestionVersionConfiguration.cs
MakanApp.Infrastructure/Persistence/MakanDbContext.cs
MakanApp.Infrastructure/Persistence/Migrations/20261030060025_AddExamAnswerAttachments.Designer.cs
MakanApp.Infrastructure/Persistence/Migrations/20261030060025_AddExamAnswerAttachments.cs
MakanApp.Infrastructure/Persistence/Migrations/MakanDbContextModelSnapshot.cs
MakanApp.Infrastructure/Storage/EfFileAssetBoundAccessResolver.Academic.cs
MakanApp.Infrastructure/Storage/EfFileAssetBoundAccessResolver.cs
MakanApp.Infrastructure/Storage/TerminalFileCleanupProcessor.cs
docs/product/ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md
tests/MakanApp.IntegrationTests/AcademicAssignmentAttachmentAcceptanceTests.cs
tests/MakanApp.IntegrationTests/AcademicAttachmentEvidence.cs
tests/MakanApp.IntegrationTests/AcademicExamAttachmentMigrationTests.cs
tests/MakanApp.IntegrationTests/AcademicExamAttachmentSecurityTests.cs
tests/MakanApp.IntegrationTests/AcademicExamAttachmentTests.cs
tests/MakanApp.UnitTests/Assessment/ExamAttachmentModelTests.cs
```

`PRE_EXISTING_REALTIME_DIRTY_FILES`: فایل‌های کارهای قبلی Realtime و BE-01؛ وجودشان در `git diff` به معنی ایجادشان در این Task نیست. پنج هم‌پوشانی شامل GlobalExceptionHandler، transformer فایل و سه ExamStore است؛ تغییرهای قبلی حفظ شده‌اند.

```text
MakanApp.Api/Controllers/ConversationsController.cs
MakanApp.Api/Controllers/RealtimeController.cs
MakanApp.Api/Errors/GlobalExceptionHandler.cs
MakanApp.Api/OpenApi/FileAndMessagingOperationTransformer.cs
MakanApp.Api/OpenApi/SchoolOperationTransformer.cs
MakanApp.Api/Program.cs
MakanApp.Api/Realtime/IMessagingHubClient.cs
MakanApp.Api/Realtime/MessagingHub.cs
MakanApp.Api/Realtime/RealtimeCursorProtector.cs
MakanApp.Api/Realtime/SignalRInvalidationNotifier.cs
MakanApp.Api/Realtime/SignalRMessagingRealtimeNotifier.cs
MakanApp.Application/Messaging/MessageContentSafetyGate.cs
MakanApp.Application/Messaging/MessagingAbstractions.cs
MakanApp.Application/Messaging/MessagingContracts.cs
MakanApp.Application/Messaging/MessagingException.cs
MakanApp.Application/Messaging/MessagingService.cs
MakanApp.Application/Realtime/RealtimeContracts.cs
MakanApp.Infrastructure/Academic/EfAcademicSessionStore.cs
MakanApp.Infrastructure/Academic/EfAcademicStore.cs
MakanApp.Infrastructure/Academic/EfClassLessonStore.cs
MakanApp.Infrastructure/Academic/EfPersonalClassStore.cs
MakanApp.Infrastructure/Assessment/EfAssignmentStore.cs
MakanApp.Infrastructure/Assessment/EfEvaluationStore.cs
MakanApp.Infrastructure/Assessment/EfExamAnswerStore.cs
MakanApp.Infrastructure/Assessment/EfExamAttemptStore.cs
MakanApp.Infrastructure/Assessment/EfExamFinalizationStore.cs
MakanApp.Infrastructure/Assessment/EfExamGradingStore.cs
MakanApp.Infrastructure/Assessment/EfExamStore.cs
MakanApp.Infrastructure/Assessment/EfSubmissionStore.cs
MakanApp.Infrastructure/Commerce/EfCommerceStore.cs
MakanApp.Infrastructure/Commerce/EfCommercialAdminStore.cs
MakanApp.Infrastructure/Commerce/EfCommercialStore.Reservations.cs
MakanApp.Infrastructure/Commerce/EfCommercialStore.cs
MakanApp.Infrastructure/Commerce/EfSubscriptionActivationHandler.cs
MakanApp.Infrastructure/DependencyInjection.cs
MakanApp.Infrastructure/Guardian/EfGuardianStore.cs
MakanApp.Infrastructure/Messaging/EfMessagingDirectoryStore.cs
MakanApp.Infrastructure/Messaging/EfMessagingStore.ChildSafety.cs
MakanApp.Infrastructure/Messaging/EfMessagingStore.Diagnostics.cs
MakanApp.Infrastructure/Messaging/EfMessagingStore.Management.Helpers.cs
MakanApp.Infrastructure/Messaging/EfMessagingStore.Reads.cs
MakanApp.Infrastructure/Messaging/EfMessagingStore.Safety.cs
MakanApp.Infrastructure/Messaging/EfMessagingStore.SafetyDiagnostics.cs
MakanApp.Infrastructure/Messaging/EfMessagingStore.Sync.cs
MakanApp.Infrastructure/Messaging/EfMessagingStore.cs
MakanApp.Infrastructure/Observability/RealtimeTelemetry.cs
MakanApp.Infrastructure/Organization/EfOrganizationPersonStore.cs
MakanApp.Infrastructure/Organization/EfOrganizationStore.cs
MakanApp.Infrastructure/Outbox/OutboxDispatcher.cs
MakanApp.Infrastructure/Realtime/AssessmentRealtimeAccess.cs
MakanApp.Infrastructure/Realtime/RealtimeInvalidationWriter.Assessment.cs
MakanApp.Infrastructure/Realtime/RealtimeInvalidationWriter.cs
MakanApp.Infrastructure/Realtime/RealtimeSyncService.cs
MakanApp.Infrastructure/Storage/EfFileAssetStore.cs
docs/backend-api-guide.md
docs/frontend-contract/README.md
docs/messaging/BE01_REVIEW_REQUIRED_CONTRACT_FA.md
docs/operations/REALTIME_RUNBOOK_FA.md
docs/product/BACKEND_ISSUES_RESOLUTION_MATRIX_FA.md
docs/product/MESSAGING_REALTIME_FRONTEND_HANDOFF_FA.md
docs/product/MESSAGING_REALTIME_IMPLEMENTATION_FA.md
docs/product/REALTIME_EVENT_CONTRACT_FA.md
tests/MakanApp.IntegrationTests/AccountRealtimeConcurrentReplayTests.cs
tests/MakanApp.IntegrationTests/AccountRealtimeContextTests.cs
tests/MakanApp.IntegrationTests/AccountRealtimeGuardianTests.cs
tests/MakanApp.IntegrationTests/AccountRealtimeOutboxTests.cs
tests/MakanApp.IntegrationTests/AccountRealtimePrivacyTests.cs
tests/MakanApp.IntegrationTests/AccountRealtimeReplayTests.cs
tests/MakanApp.IntegrationTests/AccountRealtimeScheduleTests.cs
tests/MakanApp.IntegrationTests/AccountRealtimeSchoolTests.cs
tests/MakanApp.IntegrationTests/AccountReplayTestClient.cs
tests/MakanApp.IntegrationTests/AssignmentRealtimeTests.cs
tests/MakanApp.IntegrationTests/BackendFileAndMessagingContractTests.cs
tests/MakanApp.IntegrationTests/CommercialSubscriptionRealtimeTests.cs
tests/MakanApp.IntegrationTests/ExamRealtimeTests.cs
tests/MakanApp.IntegrationTests/MakanAppWebApplicationFactory.Realtime.cs
tests/MakanApp.IntegrationTests/MakanAppWebApplicationFactory.cs
tests/MakanApp.IntegrationTests/MessagingRealtimeBoundedAudienceTests.cs
tests/MakanApp.IntegrationTests/MessagingSafetyReviewContractTests.cs
tests/MakanApp.IntegrationTests/MessagingStabilizationAcceptanceTests.cs
tests/MakanApp.IntegrationTests/MessagingStabilizationMediaTests.cs
tests/MakanApp.IntegrationTests/MessagingStabilizationSchoolBoundaryTests.cs
tests/MakanApp.IntegrationTests/NotificationEndpointsTests.cs
tests/MakanApp.IntegrationTests/RealtimeBoundaryGate.cs
tests/MakanApp.IntegrationTests/ReportExportRecoveryTests.cs
tests/MakanApp.IntegrationTests/StabilizationMediaFixtures.cs
tests/MakanApp.IntegrationTests/StabilizationObservation.cs
tests/MakanApp.UnitTests/Operations/RealtimeTelemetryTests.cs
```

`PRE_EXISTING_AI_DIRTY_FILES`: خارج از این Task و بدون تغییر byte.

```text
tools/ai/provider_diagnostic.py
tools/ai/run_exact_ai.py
tools/ai/test_provider_diagnostic.py
```

مرجع خصوصی بررسی: `.artifacts/academic-answer-attachments-20261030-184c7e9a/preservation-verification.json`، `academic-source-review.diff`، `validation-summary.json`، شش شاهد `final-http` و سه TRX زیر `final`.

## نهایی‌سازی مشترک Academic Attachments و Realtime

در درخواست جدید ۲۰۲۶-۱۰-۳۰، مالک ثبت تغییرات تأییدشدهٔ Realtime و Academic در یک Commit و Push به `main` را پس از موفقیت کامل validation مجاز کرد. مقادیر `COMMIT=false` و `PUSH=false` بالا صرفاً نتیجهٔ Task قبلی‌اند. Publish، Deploy، Migration سرور و فعال‌سازی AI همچنان خارج از محدوده‌اند.

پیاده‌سازی موجود دوباره ساخته یا تکثیر نشد. baseline این نهایی‌سازی ۱۳۱ فایل dirty داشت: ۱۲۸ فایل مرتبط تأییدشده و ۳ ابزار AI خارج از ثبت. فایل‌های مرتبط قبل از تغییر سند در artifact خصوصی بکاپ و SHA-256 آن‌ها ثبت شدند؛ تنظیمات خصوصی فقط hash شدند. منطق C#، تست‌ها، Migration و زیرساخت Realtime نسبت به پذیرش ۲۴۷۸تستی قبلی حفظ شده‌اند. [قرارداد Realtime](MESSAGING_REALTIME_FRONTEND_HANDOFF_FA.md) و [ماتریس مسائل](BACKEND_ISSUES_RESOLUTION_MATRIX_FA.md) همراه این سند نهایی شدند.

پذیرش تازهٔ پیش از Commit: Restore موفق؛ Release Build با صفر Warning/Error؛ Unit=922، Integration=1542، Architecture=14، مجموع 2478 موفق، Failed=0 و Skipped=0. ۵۲ تست Academic جدید، ۵۹ تست با عنوان Realtime و ۹۵ تست با عنوان Commerce/Commercial/Subscription/Entitlement/Quota در همین suite موفق‌اند. شش E2E مستقل با ۱۴۱ HTTP و تطابق SHA-256 فایل‌ها اجرا شد. این نتیجه پذیرش Backend محلی است؛ UI یا سرور مستقر را اثبات نمی‌کند. Pending Model Changes وجود ندارد. نتیجهٔ پس از Commit جداگانه با همان HEAD بررسی می‌شود.

فرمان‌ها و شواهد نهایی این اجرا در `.artifacts/academic-realtime-finalize-20261030-9057cc98` خصوصی‌اند. Git SHA و Push فقط پس از اجرای واقعی و موفق Build/Test روی Commit در گزارش نهایی اعلام می‌شوند. محدودیت‌های ReviewRequired پیام‌رسان، raw Parent، نگه‌داری تاریخی، UI، multi-replica و SLO با همین نهایی‌سازی بسته نمی‌شوند.
