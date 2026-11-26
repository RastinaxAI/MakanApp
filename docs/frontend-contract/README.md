# قرارداد جاری مصرف Backend برای Web و Mobile

بازبینی `2026-11-26` با مبنای Git `source@f8178a47de94eae0ec2431adebcb9256a9789786`؛ قرارداد واقعی Backend برای مصرف‌کننده، بدون تغییر Frontend.

پوشش: 498 عملیات HTTP، 442 مسیر، 796 schema؛ 153 جدول و 78 Migration.

[HTML مستقل](MakanApp-Frontend-Backend-Contract.html)، [PDF](MakanApp-Frontend-Backend-Contract.pdf)، [مرجع API](../backend-api-reference.md)، [راهنمای بک‌اند](../backend-api-guide.md).

## شروع اتصال

Base اصلی /api/v1 و Backoffice /api/admin/v1 است. Bearer opaque در Authorization header؛ GUID، enum رشته‌ای، UTC و RowVersion/Base64 از DTO واقعی گرفته شوند. شکل paging همهٔ مسیرها یکسان نیست؛ کارت همان عملیات را بخوانید. cookie session و هدر عمومی Idempotency-Key نداریم.

OTP/Refresh و Profile را کامل و Workspace معتبر را انتخاب کنید. Role/OrganizationId/ChildId انتخاب‌گرند؛ سرور رابطهٔ جاری را مجاز می‌کند. Assistant context از Workspace مدرسه مستقل است و تغییر نقش fallback شخصی پنهان نمی‌سازد. Platform Admin با Manager مدرسه متفاوت است؛ Guardian حق چت خصوصی ندارد.

## سیاست دسترسی جاری

Commerce:EnforcementEnabled=false و AiUsage:Mode=Unmetered. Summary دارای enforcementEnabled=false را با NoSubscription/Quota تاریخی قفل نکنید؛ age/DOB شرط عمومی قابلیت نیست. ACL، Privacy، Block، Safety، ظرفیت واقعی کلاس و حدود فنی فایل پابرجا هستند. پرداخت/فروش آموزشگاه و Push بیرونی برای نسخهٔ جاری Deferred؛ PersonalClass Assignment/Exam با دو Feature flag مستقل خاموش‌اند. [Open Access](../product/OPEN_ACCESS_FILE_DELIVERY_FA.md)، [Scope](../product-v6/CURRENT_RELEASE_SCOPE_FA.md).

## نقشهٔ صفحات و قراردادهای تخصصی

| جریان | رفتار و قرارداد |
|---|---|
| Profile / Phone / Workspace | Bio/DOB/Avatar، تغییر شماره، نشست و بافت مجاز؛ [v6](../product-v6/BACKEND_V6_FRONTEND_HANDOFF_FA.md) |
| School application | پرونده، مدرک خصوصی، اصلاح/بررسی و مدیر اولیهٔ صریح؛ پرداخت Runtime Deferred؛ [قرارداد](../product-v6/SCHOOL_APPLICATION_FRONTEND_HANDOFF_FA.md) |
| Organization / Guardian | مدیریت نقش با محافظ آخرین مدیر، Directory/roster، رضایت والد و Lesson؛ [مدرسه](../product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md)، [نقش](../product/ORGANIZATION_ROLE_MANAGEMENT_20261105_FA.md) |
| PersonalClass | درخواست/تأیید، عضویت، نقش محلی، انتقال مسئولیت؛ آموزش مستقل خاموش؛ [قرارداد](../product/PERSONAL_CLASS_V2_ACADEMIC_FRONTEND_HANDOFF_FA.md) |
| منابع آموزشی | Source/version/processing، ACL جاری و Archive/Revoke؛ Library عمومی/AI بسته؛ [فایل و Library](../product-v6/FILES_LIBRARY_FRONTEND_HANDOFF_FA.md) |
| اعلان | makan://notifications/{notificationId} سپس GET /notifications/{id}/target؛ permission فعلی، مقصد غیرمجاز404؛ [قرارداد](../product-v6/NOTIFICATION_FRONTEND_HANDOFF_FA.md) |
| حذف حساب | درخواست/پیگیری و freeze نشست با checkpoint/recovery؛401 به معنی Completed نیست؛ [قرارداد](../product-v6/ACCOUNT_DELETION_FRONTEND_HANDOFF_FA.md) |
| Commerce / Admin | Owner از Payer و Role جدا؛ Plan/Quote/Order واقعی حفظ؛ gate مالی خاموش؛ [قرارداد](../product-v6/COMMERCE_CURRENT_POLICY_FRONTEND_HANDOFF_FA.md) |

## تکلیف، آزمون و تحویل

Upload فایل را جدا از Attach و Submit نشان دهید. برای Published Assignment از PATCH /academic/assignments/{assignmentId}/submission-policy استفاده کنید: dueAtUtc، allowLateSubmission، expectedAssignmentRowVersion و expectedVersionRowVersion، هر چهار فیلد اجباری. تمدید کوتاه نمی‌کند؛ تغییر Late به‌تنهایی با عین مهلت موجود مجاز است. expectedVersionها را از GET جاری بگیرید؛412 نیازمند refetch و تصمیم مجدد است. ویرایش عمومی تکلیف Draft-only باقی است.

409 SUBMISSION_DEADLINE_PASSED دارای requiredAction=REFETCH_ASSIGNMENT_AND_ATTEMPT، operationState=NotApplied و retryable=false است. Draft و FileAssetId موفق را نگه دارید؛ Upload دوباره/Retry خودکار نکنید. با Late مجاز، صرف گذشت مهلت مانع UI نباشد. isLate رسید نهایی تاریخی است و با مهلت تازه دوباره محاسبه نمی‌شود. [جزئیات](../product/ASSIGNMENT_SUBMISSION_POLICY_FRONTEND_HANDOFF_FA.md).

آزمون از QuestionVersion و attachmentPolicy تثبیت‌شده استفاده می‌کند؛ Attach/Detach دارای clientOperationId، writeLeaseVersion و expectedRevisionNumber است. هر mutation نسخهٔ پاسخ/AnswerSetVersion را عوض می‌کند؛ Finalize نسخهٔ جاری و مهلت سرور را می‌سنجد. مجوز Teacher/Manager برای پاسخ نهایی و Guardian برای دادهٔ خام یکی نیست. [پیوست آموزشی](../product/ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md#frontend_handoff).

## Messenger، Home و Replay

Home از GET /conversations/account، تاریخچه از مسیر messages همان گفتگو و مجوز جاری حساب می‌آید؛ سوییچ Workspace/Role تاریخچهٔ حساب را حذف نمی‌کند. [History و ورود تازه](../product-v6/MESSENGER_HISTORY_FRONTEND_HANDOFF_FA.md). Account inbox replay با scoped replay یکی نیست؛ cursorها را جابه‌جا نکنید.

listenerهای ConversationChanged/ClientInvalidated را پیش از Snapshot نصب کنید؛ bootstrap /realtime/changes بدون cursor boundary می‌دهد، سپس Snapshot و drain تا HasMore=false. صفحهٔ خالی با HasMore=true و RetryAfterMilliseconds پایان نیست. ContextRevision فقط مقایسهٔ برابری و EventId برای dedup است؛ StreamVersion نسخهٔ business نیست.409 REALTIME_RESYNC_REQUIRED یعنی Snapshot تازه؛503 REALTIME_TEMPORARILY_UNAVAILABLE یعنی RETRY_SAME_CURSOR. Push وضعیت grade/unread را جای REST بازنویسی نمی‌کند.

SignalR /hubs/messaging با Bearer header و مرورگر LongPolling/withCredentials:false است؛ query-token پشتیبانی نمی‌شود. reconnect نیازمند subscribe تازه و Replay است. Typing/Presence گذرا و تک‌instance‌اند؛ backplane/Load محیط هدف از تست محلی نتیجه نمی‌شود. [قرارداد Event](../product/REALTIME_EVENT_CONTRACT_FA.md).

## فایل و بازبینی رسانه

POST /files?workspaceScope=Personal با multipart/form-data و file؛ boundary را client بسازد. Ready فقط موفقیت ذخیره است. فایل پیام باید kind=File/Image متناسب و attachmentIds داشته باشد؛ clientMessageId/payload در Retry ثابت بمانند. سقف فعلی۱۰MiB، MIMEهای PDF/JPEG/PNG/text/plain و بررسی signature/extension پابرجا هستند.

Send اولیهٔ رسانهٔ بدون تصمیم422 و پروندهٔ موجود Moderation می‌سازد. مالک GET /files/{fileId}/messaging-review را برای NotRequested/PendingReview/Approved/Rejected مصرف کند. Pending=WAIT_FOR_MEDIA_REVIEW؛ Rejected=MEDIA_REJECTED_KEEP_DRAFT؛ retryable=false و اثر جدید Message/Binding/Delivery/Download=false. پس از Approval، ارسال همان intent مجاز دوباره بررسی می‌شود؛ تأیید خودش پیام نمی‌فرستد. پروندهٔ متنی ReviewRequired خودکار آزاد نمی‌شود.

کارشناس Platform پس از Claim/Evidence با Review+Enforce و expectedVersion/reason، MediaApproved/MediaRejected ثبت می‌کند. self-approval و نسخهٔ کهنه رد می‌شوند؛ Resolve اداری Approval نیست. گیرنده فقط بعد Binding و ACL جاری فایل را با Bearer و private/no-store می‌گیرد؛ Pending/Rejected برای گیرنده404 است. [Handoff رسانه](../messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md).

## Chat، History و Streaming هوشمند

POST /ai/chat با message/operationId و conversationId اختیاری شروع می‌شود؛ Profile/Grade/فایل اجباری نیست. assistantProfile اختیاری و بدون اثر مجوز است. History مستقل از Messenger و متعلق به Owner/Context است؛ GET /ai/conversations/{id}/history پنجرهٔ زوج‌های کامل موفق، حداکثر۲۰ turn/۴۰۰۰ هرturn/۱۲۰۰۰ مجموع را می‌دهد. پاسخ no-store است؛ تغییر بافت مجوز تاریخچهٔ قبلی نمی‌سازد.

General بدون منبع؛ SourcesOnly بدون fallback؛ Auto/SourceFocused منتظر منبع آماده. fallback عمومی فقط با General صریح و provenance روشن، هرگز برای منبع نامجاز. selectedSources نسخهٔ دقیق را pin می‌کند؛ Citation از evidence واقعی است و Library برای AI بسته می‌ماند.

POST /ai/chat/stream همان body و Bearer با Accept:text/event-stream است. fetch incremental و decoder UTF-8 لازم است؛ start یک‌بار، delta متوالی و done/error. HTTP200 و EOF بدون done موفق نیستند؛ partial در History کامل ثبت نمی‌شود. خطا پیش از start از ProblemDetails و پس از start از event:error خوانده شود. RAG stream رسمی ندارد؛409 AI_SOURCE_STREAMING_NOT_SUPPORTED نیازمند انتخاب مسیر غیرجریانی با operationId تازه است.

Retry همان body اولیه/endpoint/operationId است. ALREADY_COMPLETED برابر409؛ History را بخوانید. IN_PROGRESS/OUTCOME_UNKNOWN اجرای خودکار تازه نمی‌خواهند. AI نامحدود، idempotency/ACL یا محدودیت واقعی upstream را حذف نمی‌کند. Service Auth هر دو سرویس false و Runtime تابع config محیط است. [قرارداد کامل Adaptive](../product-v6/ADAPTIVE_AI_FRONTEND_HANDOFF_FA.md).

## خطا، HTTP و میزبانی

پاسخ preflight با اجرای AI متفاوت است: `POST /api/v1/ai/sources/preflight` می‌تواند200 با `authorized=true`، `retrievalAvailable=false` و `errorCode=AI_SOURCE_RETRIEVAL_NOT_AVAILABLE` بدهد؛ authorized فقط مجوز است، نه موفقیت Retrieval/RAG. همان ناتوانی در اجرای واقعی409 می‌شود. envelope قابلیت از ProblemDetails جدا تفسیر شود.

خطای DNS/TLS/offline/CORS یا Abort ممکن است status قابل خواندن نداشته باشد؛500 ساختگی نسازید. پاسخ HTML یا JSON نامعتبر با200 موفقیت نیست. پاسخ خالی یا BFF با401 بدون code باید fallback امن و حفظ status/correlation داشته باشد، نه parser exception. HEAD بدنه ندارد و Range نامعتبر416 قرارداد فایل خودش را حفظ می‌کند؛ تمام پاسخ‌ها را JSON اجباری فرض نکنید.

[قرارداد تفصیلی خطای Frontend](../../reports/api-error-audit/20261125-api-errors-ac9a239-01/FRONTEND_ERROR_HANDOFF_FA.md) ترتیب تفسیر و مرز پیشنهادهای UI را توضیح می‌دهد.

400/404/405/413/415 framework زیر /api به‌ترتیب REQUEST_INVALID/RESOURCE_NOT_FOUND/METHOD_NOT_ALLOWED/REQUEST_TOO_LARGE/UNSUPPORTED_MEDIA_TYPE دارند؛ کد دامنه بازنویسی نمی‌شود. HEAD بدنه ندارد.401 نشست،403 مجوز،404 پنهان‌سازی/نبود و409/412 تضاد را از code/requiredAction همان عملیات تفسیر کنید. correlationId/traceId و X-Correlation-ID را برای پشتیبانی نگه دارید؛ محتوای خصوصی یا Token لاگ نشود.

Retry-After معتبر خطاهای429/503 AI/Knowledge به ثانیهٔ گرد‌شده بالا حفظ می‌شود؛ POST را فقط با قواعد idempotency همان عملیات تکرار کنید. Content-Type application/problem+json و Accept نامناسب نباید به parse متن خطا منجر شود. پاسخ خطای SSE از قرارداد خودش پیروی می‌کند.

CORS از config محیط مستقل است؛ AllowAnyOrigin=true اجازهٔ credentials نمی‌دهد و false allowlist معتبر لازم دارد. REST با credentials:omit و SignalR با LongPolling/withCredentials:false مصرف شود. /health/ready فقط config/SQL/Storage است؛ Provider اختیاری، Push دستگاه، Data Protection ring و SLO مستقل پذیرفته شوند. [استقرار](../deployment-windows-linux-docker.md)، [Runbook](../operations/PRODUCTION_CONFIGURATION_RUNBOOK.md).

## نگه‌داری و مسیر مطالعه

راهنمای بک‌اند → مرجع HTTP/DTO → Controller → Application → Domain/Port → Infrastructure/EF → Migration → تست. ابزار Snapshot بدون SQL/worker/Provider اجرا می‌شود؛ HTML/PDF این سند کاتالوگ کامل API را ضمیمه دارد. اعداد تست تاریخی شاهد validation امروز نیستند؛ [وضعیت جاری](../CURRENT_PROJECT_STATUS.md)، [فهرست اسناد](../documentation-inventory.csv).
