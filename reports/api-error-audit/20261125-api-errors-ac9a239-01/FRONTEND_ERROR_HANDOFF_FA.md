# قرارداد مدیریت خطای Frontend

این سند راهنمای اتصال است؛ هیچ فایل Frontend تغییر نکرده و موفقیت UI ادعا نمی‌شود. جدول کدها از Source فعلی استخراج شده است، نه از حدس متن خطا. `userSafeMessage` و رفتار UI در جدول **پیشنهاد handoff** هستند؛ قرار نیست بدون پیاده‌سازی کلاینت اجرا شوند. وضعیت‌های `KNOWN_EXCEPTION_DEFAULT_400` نگاشت پیش‌فرض Exception شناخته‌شده‌اند و با تست تک‌تک endpointها اشتباه نشوند.

## ترتیب تفسیر

۱. ابتدا رسیدن پاسخ HTTP را بررسی کنید. DNS/TLS/offline/CORS/Mixed Content/Abort معمولاً status قابل خواندن ندارند؛ 500 ساختگی نسازید. ۲. Content-Type و Schema را بررسی کنید؛ HTML صفحهٔ ورود یا JSON خراب با HTTP 200 موفقیت نیست. ۳. `status`، `code`، `detail`، `traceId` و `correlationId` در ProblemDetails قرارداد موجودند؛ `errors` فقط در Validation الزام معنایی دارد. ۴. branch اصلی بر اساس `code` باشد؛ متن `detail` را parse نکنید. ۵. 2xx فقط لایهٔ Transport است؛ نتیجهٔ Business و وضعیت UX جدا نگهداری شوند.

روی پاسخِ خالی یا غیر JSON، status و correlation header را حفظ کنید و fallback امن نشان دهید. `{status:401}` ناشناس BFF با Origin واقعی مشاهده شده و ProblemDetails Backend نیست؛ در این حالت ورود مجدد پیشنهاد شود، نه parser exception یا Generic 500. پاسخ مستقیم Backend برای ورود، `401 AUTH_REQUIRED` است. سازگارکردن پاسخ BFF با code/trace/correlation، handoff به مالک Frontend است؛ مخزن آن در این workspace موجود نیست.

پاسخ `POST /api/v1/ai/sources/preflight` گزارش capability است، نه اجرای AI: `200` می‌تواند با `authorized=true`، `retrievalAvailable=false` و `errorCode=AI_SOURCE_RETRIEVAL_NOT_AVAILABLE` همراه باشد. همان کد در اجرای واقعیِ غیرقابل‌بازیابی `409` است؛ این دو envelope و مرحله متفاوت‌اند و اختلاف عمدی در ledger ثبت شده است. `authorized=true` به‌تنهایی موفقیت Retrieval یا RAG نیست.

## تغییرات محدود Backend

برای خطاهای framework فاقد business code: 400=`REQUEST_INVALID`، 404=`RESOURCE_NOT_FOUND`، 405=`METHOD_NOT_ALLOWED`، 413=`REQUEST_TOO_LARGE`، 415=`UNSUPPORTED_MEDIA_TYPE`. 404 اختصاصی دامنه/مخفی‌سازی، status واقعی، header `Allow` در 405 و field errors موجود جایگزین نمی‌شوند. HEAD بدنه ندارد؛ Range نامعتبر 416 نیز قرارداد فایل موجود خود را دارد و به JSON اجباری تبدیل نشده است. این تغییر هنوز روی سرور Deploy نشده است.

در خطاهای AI/Knowledge با HTTP 429 یا 503، اگر `RetryAfter` معتبر از قبل در Exception موجود باشد، `Retry-After` عددی به ثانیه حفظ می‌شود. مقدار منفی حذف، صفر حفظ و کسر ثانیه رو به بالا گرد می‌شود. وجود header، مجوز تکرار خودکار write یا ساخت OperationId تازه نیست. این مرحله Retry، timeout یا تنظیم Service Auth را تغییر نداده است.

## خطاهای مهم

| Code | HTTP | پیام و رفتار کلاینت |
| --- | --- | --- |
| SUBMISSION_DEADLINE_PASSED | 409 | مهلت تحویل این تکلیف به پایان رسیده است. Draft و فایل قبلی حفظ؛ Assignment و Attempt تازه خوانده شوند؛ Upload دوباره یا retry کور انجام نشود. |
| CONCURRENCY_CONFLICT | 412 | نسخهٔ تازه را بخوانید؛ ورودی کاربر را حفظ و اختلاف را حل کنید. |
| MESSAGE_SAFETY_REVIEW_REQUIRED | 422 | فایل منتظر بررسی است؛ Pending با Approved/Rejected یکی نیست. پیش‌نویس حفظ؛ نه Delivered و نه Downloadable برای گیرنده. |
| MESSAGE_SAFETY_BLOCKED | 422 | فایل تأیید نشده؛ retry ارسال انجام نشود. |
| AI_OPERATION_IN_PROGRESS | 409 | همان intent در جریان است؛ درخواست تولید تازه ندهید. |
| AI_OPERATION_ALREADY_COMPLETED | 409 | intent قبلاً کامل شده؛ history مجاز را تازه کنید. پاسخ خصوصی AI دوباره از سرور replay نمی‌شود. |
| AI_OPERATION_OUTCOME_UNKNOWN | 409 | نتیجه نامعلوم؛ intent را حفظ کنید و بدون reconciliation تولید دوباره نکنید. |
| AI_SERVICE_UNAUTHORIZED / AI_SERVICE_FORBIDDEN | 503 | خطای Service Auth سمت سرویس است؛ کاربر را به login loop نبرید و credential سرویس درخواست نکنید. |
| AI_SOURCE_PROCESSING | 409 | آماده‌سازی منبع ادامه دارد؛ وضعیت همان نسخه را بخوانید. |
| AI_SERVICE_TIMEOUT | 504 | مهلت سرویس تمام شد؛ با cancel کاربر یا timeout مرورگر یکی نیست. |

کد دقیق جدول از ledger می‌آید؛ برای مثال نام واقعی وضعیت نامعلوم عملیات در جدول کامل ملاک است. پیام انگلیسی عمومی یا Generic Conflict نباید کد اختصاصی Deadline را بپوشاند.

## SSE، صفحه و بازیابی

پیش از `start`، خطا HTTP ProblemDetails است. بعد از `start` ممکن است HTTP اولیه 200 بماند و `event:error` نتیجهٔ ناموفق را اعلام کند؛ متن partial را موفق یا نهایی نکنید. فقط `done` معتبر، نتیجهٔ موفق است. EOF بدون done/error، stream ناقص است؛ این طبقه‌بندی کلاینت است و HTTP 500 ساختگی نیست. `requestId` و correlation از start/header به interaction بسته شوند. delta قدیمی نباید state گفتگوی جدید را تغییر دهد. لغو کاربر، بستن تب و قطع شبکه از timeout بالادست جدا بمانند. stream را با OperationId تازه به‌طور خودکار شروع نکنید.

General/Auto/SourcesOnly و SourceIntent مستقل‌اند: در Auto با منبع انتخابی، پیش‌فرض SourceFocused است؛ Processing به‌صورت ضمنی General نمی‌شود. Auto+General می‌تواند fallback روشن و provenance مناسب بدهد؛ SourcesOnly هیچ fallback پنهان ندارد. upload موفق، File Ready، Messaging attachment، Academic attachment، Knowledge Source، ingest، retrieve و RAG مراحل متفاوت‌اند.

قرارداد Published Assignment Policy قبلی در Working Tree حفظ شده: `PATCH /api/v1/academic/assignments/{assignmentId}/submission-policy` با `dueAtUtc`، `allowLateSubmission`، `expectedAssignmentRowVersion` و `expectedVersionRowVersion`. فقط تمدید به UTC آینده مجاز است؛ allowLate مستقل تغییر می‌کند. رکوردهای Submitشده و receipt/IsLate تاریخی بازنویسی نمی‌شوند. این مسیر در OpenAPI زندهٔ این ممیزی وجود نداشت و نباید UI به انتشار آن فرض کند.

## Class / Workspace

در کد موجود، GET مجموعهٔ کلاس‌ها به Personal/Parent می‌تواند `403 ACADEMIC_READ_NOT_ALLOWED` بدهد؛ PATCH کلاس به Manager جاری نیاز دارد (`403 MANAGER_ROLE_REQUIRED`) و منبع ناموجود/خارج از سازمان می‌تواند `404 CLASS_NOT_FOUND` باشد. GET جزئیات عمومی کلاس `{id}` در قرارداد وجود ندارد؛ همان مسیر با PATCH تعریف شده و GET می‌تواند 405 باشد. concealment عمدی را به 403 تبدیل نکنید. این تحلیل کد، Root Cause رخداد واقعی 403/404 قبلی نیست؛ Method/Route/Workspace/role/code/correlation واقعی همچنان لازم است.

## وضعیت‌های خارج از تست زنده

حساب‌ها فقط از `MakanAppWebApplicationFactory`، `CreateMessagingUserAsync`، `CreateOfficialScenarioAsync` و helperهای فعلی ساخته می‌شوند. aliasهای گزارش: fixture-personal-01، fixture-student-01، fixture-parent-01، fixture-teacher-01، fixture-manager-01. هیچ شناسهٔ واقعی، شماره، OTP یا Token صادرشده به handoff منتقل نمی‌شود. تست SQL فقط روی دیتابیس ایزولهٔ Integration است. تمام رفتار UI دارای ورود، timeline کارت‌ها و شبکهٔ مرورگر بدون credential زنده، BLOCKED هستند.

مبانی HTTP: [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html#section-15) و [IANA](https://www.iana.org/assignments/http-status-codes). جدول زیر رفتار پیشنهادی کلاینت برای هر کد موجود است؛ هیچ status غیرمرتبط برای افزایش پوشش ساخته نشده است.

## جدول کامل کدها

| Code | HTTP | پیام فارسی پیشنهادی | Retry | Refetch | Auth | UI state / action |
| --- | --- | --- | --- | --- | --- | --- |
| ACADEMIC_ACTIVITY_PUBLICATION_MISSING | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACADEMIC_ACTIVITY_QUERY_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACADEMIC_PERIOD_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACADEMIC_PERIOD_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACADEMIC_QUERY_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACADEMIC_READ_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| ACCOUNT_DELETION_CONVERSATION_OWNERSHIP | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_EXECUTION_DISABLED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_INVARIANT_FAILED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_KNOWLEDGE_UNAVAILABLE | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_LAST_ORGANIZATION_MANAGER | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_LAST_PERSONAL_CLASS_MANAGER | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_LAST_PLATFORM_OWNER | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_RECOVERY_EXHAUSTED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_REFERENTIAL_CONFLICT | 400 | اطلاعات درخواست را بررسی کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| ACCOUNT_DELETION_SCHOOL_MANAGER_DEPENDENCY | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_DELETION_SOURCE_OWNERSHIP_CONFLICT | 400 | اطلاعات درخواست را بررسی کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| ACCOUNT_DELETION_STEP_FAILED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_PROFILE_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ACCOUNT_SUSPENDED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ADDRESS_BOOK_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| ADDRESS_BOOK_CONSENT_REQUIRED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ADDRESS_BOOK_CURSOR_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ADDRESS_BOOK_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ADDRESS_BOOK_SOURCE_STALE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AGE_SUBJECT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AGE_VERIFICATION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| AGE_VERIFICATION_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_CONTEXT_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_CONVERSATION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_CONVERSATION_SOURCE_BINDING_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| AI_EMBEDDING_UNAVAILABLE | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_FEATURE_DISABLED | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_INSUFFICIENT_EVIDENCE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_INVALID_REQUEST | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_KNOWLEDGE_INVALID_REQUEST | 422 | درخواست در وضعیت فعلی قابل انجام نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_KNOWLEDGE_POLLING_TIMEOUT | 504 | سرویس در زمان تعیین‌شده پاسخ نداد. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_KNOWLEDGE_RATE_LIMITED | 429 | کمی بعد دوباره تلاش کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_KNOWLEDGE_RESPONSE_INVALID | 502 | پاسخ سرویس معتبر نبود؛ نتیجه را دوباره بررسی کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_KNOWLEDGE_TIMEOUT | 504 | سرویس در زمان تعیین‌شده پاسخ نداد. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_KNOWLEDGE_UNAVAILABLE | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_OPERATION_ALREADY_COMPLETED | 409 | این درخواست قبلاً تکمیل شده است؛ تاریخچه را به‌روز کنید. | NO | YES | KEEP_SESSION | PRESERVE_INTENT_RECONCILE_AUTHORIZED_HISTORY_NO_NEW_ID |
| AI_OPERATION_IN_PROGRESS | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | PRESERVE_INTENT_RECONCILE_AUTHORIZED_HISTORY_NO_NEW_ID |
| AI_OPERATION_OUTCOME_UNKNOWN | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | PRESERVE_INTENT_RECONCILE_AUTHORIZED_HISTORY_NO_NEW_ID |
| AI_OPERATION_PREVIOUSLY_FAILED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | PRESERVE_INTENT_RECONCILE_AUTHORIZED_HISTORY_NO_NEW_ID |
| AI_RAG_CONTEXT_TOO_LARGE | 422 | درخواست در وضعیت فعلی قابل انجام نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_RAG_GENERATION_UNAVAILABLE | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_RAG_RESPONSE_INVALID | 502 | پاسخ سرویس معتبر نبود؛ نتیجه را دوباره بررسی کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_RATE_LIMITED | 429 | کمی بعد دوباره تلاش کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_RESPONSE_INVALID | 502 | پاسخ سرویس معتبر نبود؛ نتیجه را دوباره بررسی کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_SERVICE_FORBIDDEN | 503 | سرویس موقتاً در دسترس نیست. | NO | NO | KEEP_SESSION | CONTACT_SUPPORT_NO_USER_REAUTH |
| AI_SERVICE_TIMEOUT | 504 | سرویس در زمان تعیین‌شده پاسخ نداد. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_SERVICE_UNAUTHORIZED | 503 | سرویس موقتاً در دسترس نیست. | NO | NO | KEEP_SESSION | CONTACT_SUPPORT_NO_USER_REAUTH |
| AI_SERVICE_UNAVAILABLE | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| AI_SOURCE_CONTEXT_MISMATCH | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_SOURCE_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_SOURCE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_SOURCE_NOT_INGESTED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_SOURCE_NOT_READY | 409 | منبع هنوز آمادهٔ استفاده نیست. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| AI_SOURCE_OPERATION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| AI_SOURCE_PROCESSING | 409 | منبع هنوز در حال آماده‌سازی است. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| AI_SOURCE_PROCESSING_FAILED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| AI_SOURCE_REQUEST_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_SOURCE_RETRIEVAL_NOT_AVAILABLE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_SOURCE_STREAMING_NOT_SUPPORTED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AI_SOURCE_VERSION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| AI_STREAM_FAILED | 200 | جریان پاسخ کامل نشد؛ پاسخ ناقص را نهایی نکنید. | NO | YES | KEEP_SESSION | KEEP_PARTIAL_FAILURE_RECONCILE_HISTORY_NO_NEW_INTENT |
| AI_VECTOR_UNAVAILABLE | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| ASSIGNMENT_ALREADY_PUBLISHED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSIGNMENT_ATTEMPTS_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSIGNMENT_DESCRIPTION_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSIGNMENT_DUE_DATE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSIGNMENT_MAX_SCORE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSIGNMENT_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSIGNMENT_NOT_DRAFT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSIGNMENT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSIGNMENT_NOT_SUBMITTABLE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSIGNMENT_RECIPIENT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSIGNMENT_TITLE_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ASSISTANT_CONTEXT_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| ATTENDANCE_ALREADY_CHANGED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ATTENDANCE_ALREADY_RECORDED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ATTENDANCE_CORRECTION_REASON_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ATTENDANCE_ENROLLMENT_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ATTENDANCE_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AUTH_FORBIDDEN | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| AUTH_REQUIRED | 401 | برای ادامه دوباره وارد شوید. | NO | NO | REAUTH | REAUTHENTICATE |
| CHANGE_CURSOR_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CHILD_CONTEXT_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CHILD_CONTEXT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CLASS_CAPACITY_EXCEEDED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CLASS_CREATION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| CLASS_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CLASS_NOT_ACTIVE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CLASS_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CLASS_SESSION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| CLASS_SETTINGS_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| COMMERCE_CONTEXT_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| COMMERCE_METER_UNAVAILABLE | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| COMMERCE_RESOURCE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| COMMERCE_SUMMARY_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| COMMERCE_USAGE_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| COMMERCE_USAGE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONCURRENCY_CONFLICT | 412 | اطلاعات تغییر کرده است؛ نسخهٔ تازه را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| CONTACT_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| CONTACT_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONTACT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONTACT_SELF_NOT_ALLOWED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONTACT_TARGET_NOT_AVAILABLE | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_ADVANCED_MANAGEMENT_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_ARCHIVED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_CHECKLIST_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| CONVERSATION_CHECKLIST_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_CHECKLIST_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_CHECKLIST_STATE_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| CONVERSATION_CHECKLIST_STRUCTURE_IMMUTABLE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_CREATION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| CONVERSATION_HISTORY_NOT_VISIBLE | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_INVITATION_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_INVITE_LINK_UNAVAILABLE | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_MANAGEMENT_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_MEMBER_RESTRICTED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_NOT_JOINABLE | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_PERMISSION_REQUIRED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_POLL_CLOSED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_POLL_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| CONVERSATION_POLL_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_POLL_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_POLL_VOTE_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| CONVERSATION_POLL_VOTE_IMMUTABLE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_ROLE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_STATE_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| CONVERSATION_TOPIC_CLOSED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_TOPIC_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| CONVERSATION_TOPIC_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| COURSE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| COURSE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| DELIVERY_CURSOR_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| DIRECTORY_PAGE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| DIRECT_CONVERSATION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| DIRECT_RECIPIENT_NOT_AVAILABLE | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ENROLLMENT_ALREADY_ACTIVE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ENROLLMENT_NOT_ACTIVE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ENTITLEMENT_REQUIRED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EVALUATION_ALREADY_RELEASED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EVALUATION_CORRECTION_REASON_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EVALUATION_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EVALUATION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EVALUATION_NOT_READY_FOR_RELEASE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| EVALUATION_SCORE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EVALUATION_SUBMISSION_NOT_FINAL | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ALREADY_FINALIZED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ALREADY_PUBLISHED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ANSWER_IDEMPOTENCY_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| EXAM_ANSWER_KEY_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ANSWER_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ANSWER_OPTION_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ANSWER_SET_VERSION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| EXAM_ANSWER_TYPE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ANSWER_VERSION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| EXAM_ATTACHMENT_LIMIT_EXCEEDED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ATTACHMENT_NOT_ALLOWED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ATTEMPTS_EXHAUSTED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ATTEMPTS_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ATTEMPT_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ATTEMPT_NOT_FINALIZABLE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ATTEMPT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_ATTEMPT_NOT_WRITABLE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_DEADLINE_PASSED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| EXAM_DURATION_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_FINALIZE_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| EXAM_FINALIZE_IDEMPOTENCY_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| EXAM_GRADE_ALREADY_RELEASED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_GRADE_ATTEMPT_NOT_FINALIZED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_GRADE_CORRECTION_REASON_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_GRADE_INCOMPLETE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_GRADE_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_GRADE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_GRADE_NOT_RELEASED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_GRADE_QUESTION_NOT_REVIEWED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_GRADE_RELEASE_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| EXAM_GRADE_RELEASE_IDEMPOTENCY_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| EXAM_GRADE_RELEASE_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_GRADE_SCORE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_MAX_SCORE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_NOT_AVAILABLE_YET | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_NOT_DRAFT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_NOT_PUBLISHED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_QUESTION_ORDER_INVALID | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_QUESTION_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_QUESTION_SCORE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_QUESTION_SET_INVALID | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_RANDOMIZATION_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_SCORE_TOTAL_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_START_IDEMPOTENCY_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| EXAM_STUDENT_PREVIEW_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_TITLE_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_VERSION_LOCKED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_WINDOW_CLOSED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_WINDOW_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_WRITE_LEASE_HELD_BY_OTHER_SESSION | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_WRITE_LEASE_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| EXAM_WRITE_LEASE_STALE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| FEATURE_NOT_ENTITLED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| FILE_ALREADY_DELETED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| FILE_CONTENT_INVALID | 415 | نوع محتوای ارسال‌شده مجاز نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| FILE_EMPTY | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| FILE_IN_USE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| FILE_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| FILE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| FILE_NOT_READY | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| FILE_STORAGE_FAILED | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| FILE_TOO_LARGE | 413 | اندازهٔ فایل بیش از حد مجاز است. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| FILE_TYPE_NOT_ALLOWED | 415 | نوع محتوای ارسال‌شده مجاز نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| FILE_UPLOAD_FAILED | 500 | عملیات کامل نشد؛ با کد پیگیری به پشتیبانی اطلاع دهید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| GRADE_NOT_RELEASED | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| GRADE_RELEASE_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| GUARDIAN_INVITATION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| GUARDIAN_INVITATION_CONSENT_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| GUARDIAN_INVITATION_TARGET_NOT_AVAILABLE | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| GUARDIAN_RELATION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| IDENTITY_SESSION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| INVITATION_ALREADY_ACCEPTED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| INVITATION_EXPIRED | 410 | اعتبار این درخواست پایان یافته است. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| INVITATION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| INVITATION_REVOKED | 410 | اعتبار این درخواست پایان یافته است. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| LAST_OWNER_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| LEARNER_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| LESSON_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| LESSON_STATE_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| LESSON_VERSION_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| MANAGED_CONVERSATION_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MANAGER_ROLE_REQUIRED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MEMBERSHIP_NOT_ACTIVE | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MENTION_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_ATTACHMENT_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_ATTACHMENT_NOT_READY | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| MESSAGE_ATTACHMENT_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_CLIENT_ID_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_DELETED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_DELETE_WINDOW_EXPIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_EDIT_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| MESSAGE_EDIT_WINDOW_EXPIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_EMPTY | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_FORWARD_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_FORWARD_SCOPE_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_IDEMPOTENCY_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| MESSAGE_KIND_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_NOT_EDITABLE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_PAGE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_PUBLISH_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_REPLY_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGE_SAFETY_BLOCKED | 422 | این فایل برای ارسال تأیید نشده است. | NO | NO | KEEP_SESSION | KEEP_DRAFT_REJECTED_MEDIA_NO_AUTO_RETRY |
| MESSAGE_SAFETY_REVIEW_REQUIRED | 422 | فایل منتظر بررسی است؛ پیش‌نویس را نگه دارید. | NO | NO | KEEP_SESSION | KEEP_DRAFT_WAIT_EXISTING_REVIEW_NO_DELIVERY_NO_AUTO_RETRY |
| MESSAGE_SAFETY_UNAVAILABLE | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| MESSAGE_TOO_LONG | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGING_ACTIVITY_RATE_LIMITED | 429 | کمی بعد دوباره تلاش کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| MESSAGING_DISCOVERY_RATE_LIMITED | 429 | کمی بعد دوباره تلاش کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| MESSAGING_PRESENTATION_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| MESSAGING_PRESENTATION_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGING_PRESENTATION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MESSAGING_PRIVACY_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| MESSAGING_PRIVACY_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| METHOD_NOT_ALLOWED | 405 | روش این درخواست مجاز نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MODERATION_CASE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MODERATION_PERMISSION_REQUIRED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| MODERATION_STATE_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| ORDER_IDEMPOTENCY_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| ORDER_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORDER_MANAGEMENT_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORDER_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORDER_NOT_PAYABLE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORGANIZATION_COMMERCIALLY_LOCKED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORGANIZATION_LAST_MANAGER_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORGANIZATION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORGANIZATION_ONBOARDING_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| ORGANIZATION_ONBOARDING_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORGANIZATION_PERSON_ACCOUNT_LINK_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| ORGANIZATION_PERSON_IN_USE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORGANIZATION_PERSON_NAMES_PROTECTED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORGANIZATION_PERSON_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ORGANIZATION_SCOPE_MISMATCH | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| OTP_ALREADY_USED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| OTP_EXPIRED | 410 | اعتبار این درخواست پایان یافته است. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| OTP_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| OTP_RATE_LIMITED | 429 | کمی بعد دوباره تلاش کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| OTP_TOO_MANY_ATTEMPTS | 429 | کمی بعد دوباره تلاش کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| OWNERSHIP_TRANSFER_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| OWNERSHIP_TRANSFER_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PARENT_ROLE_REQUIRED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PARTICIPANT_ALREADY_ACTIVE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PARTICIPANT_NOT_ACTIVE | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PARTICIPANT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PAYMENT_AMOUNT_MISMATCH | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PAYMENT_ATTEMPT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PAYMENT_CURRENCY_MISMATCH | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PAYMENT_IDEMPOTENCY_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PAYMENT_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PAYMENT_PENDING_VERIFICATION | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PAYMENT_PROVIDER_UNAVAILABLE | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| PAYMENT_RATE_LIMITED | 429 | کمی بعد دوباره تلاش کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| PAYMENT_REFERENCE_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PAYMENT_REFERENCE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PEOPLE_RESOLVE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PEOPLE_SEARCH_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PERSONAL_CLASS_CAPACITY_REACHED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PERSONAL_CLASS_LAST_MANAGER_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PERSONAL_CLASS_MEMBERSHIP_INACTIVE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PERSONAL_CLASS_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PERSONAL_CLASS_NOT_READY | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PERSONAL_CLASS_PROVISIONING_FAILED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PERSONAL_CLASS_REQUEST_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PERSONAL_CLASS_REQUEST_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PERSONAL_CLASS_REVIEW_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PERSONAL_CLASS_VERSION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PERSON_LINK_CONSENT_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PERSON_NOT_DISCOVERABLE | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PHONE_CHANGE_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PHONE_CHANGE_DELIVERY_UNCONFIRMED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PHONE_CHANGE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PHONE_CHANGE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PHONE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PIN_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLAN_NOT_PURCHASABLE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLAN_TERMS_INCOMPLETE | 422 | درخواست در وضعیت فعلی قابل انجام نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_ACCESS_DENIED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_ACCOUNT_SUSPENDED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_ASSIGNMENT_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PLATFORM_ASSIGNMENT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_BOOTSTRAP_CLOSED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_EDUCATION_RESOURCE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_LAST_MANAGER_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_LAST_OWNER_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_MEMBERSHIP_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PLATFORM_MEMBERSHIP_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_MODERATION_CASE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_MODERATION_STATE_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PLATFORM_ORDER_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_ORGANIZATION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PLATFORM_ORGANIZATION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_PLAN_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PLATFORM_PLAN_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_PLAN_VERSION_NOT_PUBLISHED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_PROVISIONING_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PLATFORM_PROVISIONING_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_QUERY_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_REASON_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_ROLE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_SESSION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PLATFORM_SUBSCRIPTION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| PLATFORM_USER_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| PROFILE_VALIDATION_FAILED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| QUOTA_EXCEEDED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| QUOTA_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| RATE_LIMITED | 429 | کمی بعد دوباره تلاش کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| REACTION_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| READ_CURSOR_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REALTIME_INVALID_QUERY | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REALTIME_RESYNC_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REALTIME_SUBSCRIPTION_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REALTIME_TEMPORARILY_UNAVAILABLE | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| REFRESH_TOKEN_INVALID | 401 | برای ادامه دوباره وارد شوید. | NO | NO | REAUTH | REAUTHENTICATE |
| REFRESH_TOKEN_REUSED | 401 | برای ادامه دوباره وارد شوید. | NO | NO | REAUTH | REAUTHENTICATE |
| REPORT_DATA_NO_LONGER_AUTHORIZED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_DATE_RANGE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_EXPORT_FORMAT_UNSUPPORTED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_EXPORT_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_EXPORT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_EXPORT_NOT_READY | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| REPORT_EXPORT_ROW_LIMIT_EXCEEDED | 422 | درخواست در وضعیت فعلی قابل انجام نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_GENERATION_FAILED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_IDEMPOTENCY_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| REPORT_MESSAGE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_PAGINATION_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_RESOURCE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REPORT_SORT_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REQUEST_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| REQUEST_TOO_LARGE | 413 | اندازهٔ فایل بیش از حد مجاز است. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| RESOURCE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| ROLE_NOT_ACTIVE | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHEDULED_MESSAGE_CLAIM_LOST | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHEDULED_MESSAGE_CONFLICT | 412 | نسخهٔ تازهٔ اطلاعات را دریافت کنید. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SCHEDULED_MESSAGE_ID_RESERVED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHEDULED_MESSAGE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHEDULED_MESSAGE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHEDULE_RULE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_APPLICATION_COMMERCIAL_TERMS_UNAVAILABLE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_APPLICATION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SCHOOL_APPLICATION_FILE_UNAVAILABLE | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_APPLICATION_INITIAL_MANAGER_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_APPLICATION_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_APPLICATION_NOT_APPROVED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_APPLICATION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_APPLICATION_PERMISSION_REQUIRED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_APPLICATION_REQUIREMENTS_UNCONFIRMED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_APPLICATION_SUBMISSION_INCOMPLETE | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_GUARDIAN_RELATION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_GUARDIAN_RELATION_STATE_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SCHOOL_MEMBERSHIP_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_MEMBERSHIP_OPERATION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SCHOOL_MEMBERSHIP_STATE_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SCHOOL_REQUEST_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SCHOOL_ROLE_ASSIGNMENT_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SEARCH_CURSOR_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SEARCH_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SEARCH_QUERY_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SEARCH_QUERY_REQUIRED | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SESSION_CANCELLED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SESSION_DEVICE_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SESSION_NOT_ACTIVE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SESSION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SESSION_TIME_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SESSION_TIME_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SMS_PROVIDER_AUTHENTICATION_FAILED | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| SMS_PROVIDER_CONFIGURATION_ERROR | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| SMS_PROVIDER_REJECTED | 422 | درخواست در وضعیت فعلی قابل انجام نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SMS_PROVIDER_UNAVAILABLE | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| SMS_RATE_LIMITED | 429 | کمی بعد دوباره تلاش کنید. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| SMS_RECIPIENT_BLOCKED | 422 | درخواست در وضعیت فعلی قابل انجام نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SMS_RECIPIENT_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SOURCE_IDEMPOTENCY_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SOURCE_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SOURCE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SOURCE_NOT_READY | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SOURCE_QUOTA_EXCEEDED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SOURCE_REQUEST_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SOURCE_VERSION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SUBMISSION_ANSWER_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUBMISSION_ATTEMPTS_EXHAUSTED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUBMISSION_DEADLINE_PASSED | 409 | مهلت تحویل این تکلیف به پایان رسیده است. | NO | YES | KEEP_SESSION | KEEP_DRAFT_REFETCH_ASSIGNMENT_AND_ATTEMPT_CONTACT_TEACHER |
| SUBMISSION_EMPTY | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUBMISSION_FILE_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUBMISSION_FILE_NOT_READY | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SUBMISSION_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUBMISSION_NOT_DRAFT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUBMISSION_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUBMISSION_VERSION_MISMATCH | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUBSCRIPTION_INACTIVE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUBSCRIPTION_REQUIRED | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUPPORT_FILE_UNAVAILABLE | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUPPORT_PERMISSION_REQUIRED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUPPORT_REQUEST_INVALID | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| SUPPORT_TICKET_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| SUPPORT_TICKET_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| TEACHER_ASSIGNMENT_ALREADY_ACTIVE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| TEACHER_ASSIGNMENT_NOT_ACTIVE | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| TEACHER_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| TEACHER_NOT_ASSIGNED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| TEACHER_SESSION_CONFLICT | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | YES | KEEP_SESSION | REFETCH_PRESERVE_DRAFT |
| UNEXPECTED_ERROR | 500 | عملیات کامل نشد؛ با کد پیگیری به پشتیبانی اطلاع دهید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| UNSUPPORTED_MEDIA_TYPE | 415 | نوع محتوای ارسال‌شده مجاز نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| USERNAME_ALREADY_EXISTS | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| USER_BLOCK_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| WORKSPACE_NOT_ALLOWED | 403 | مجوز انجام این کار را ندارید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| WORKSPACE_NOT_FOUND | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| identity.phone-change.real | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| notification_concurrency_conflict | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| notification_device_conflict | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| notification_device_not_found | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| notification_not_found | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| notification_preference_concurrency_conflict | 409 | وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| notification_preference_invalid | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| notification_preference_mandatory | 422 | درخواست در وضعیت فعلی قابل انجام نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| notification_preference_scope_not_found | 404 | مورد درخواستی در دسترس نیست. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
| notification_push_unavailable | 503 | سرویس موقتاً در دسترس نیست. | CONDITIONAL | NO | KEEP_SESSION | WAIT_BACKOFF_RECHECK_OPERATION |
| notification_query_invalid | 400 | اطلاعات درخواست را بررسی کنید. | NO | NO | KEEP_SESSION | CORRECT_INPUT_OR_STOP |
