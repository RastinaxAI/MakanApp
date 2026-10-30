# قرارداد جاری مصرف Backend برای Web و Mobile

بازبینی `2026-10-30` با مبنای Git `main@e11d4ec22d499eb47f256e49ff2f1a81f2ea66f9` و metadata فایل‌های working tree؛ این سند در مخزن Backend است و مجوز ساخت، clone یا تغییر frontend نیست.

پوشش جاری: 365 عملیات HTTP در 322 مسیر، 63 Controller، 608 schema، 113 جدول و 55 Migration. فهرست کامل کارت‌های HTTP و DTOها در HTML/PDF همین تحویل ضمیمه شده است.

- [HTML مستقل](MakanApp-Frontend-Backend-Contract.html) و [PDF](MakanApp-Frontend-Backend-Contract.pdf): قرارداد جاری و کاتالوگ کامل API.
- [مرجع Markdown قراردادها](../backend-api-reference.md)، [راهنمای جریان کد](../backend-api-guide.md)، [ساختار دیتابیس](../database-current-structure.md).
- [UX مرجع اصلی](../project-context/03_Makan_User_Flows_and_Screens_UX_v1.0.pdf) و [نقشه Flow](../project-context/04_Flow.png) نیازمندی‌اند، نه اثبات وجود screen یا frontend در این workspace. اطلاعات snapshot قدیمی ۱۴۱ عملیات/۲۰ Migration وضعیت جاری نیست.

## قرارداد مشترک انتقال و مجوز

- API اصلی /api/v1 و Backoffice مستقل /api/admin/v1 است. شناسهٔ GUID، enum رشته‌ای، زمان UTC و rowversion/Base64 را از response واقعی بگیرید؛ placeholderهای سند دادهٔ آمادهٔ محیط نیستند.
- Bearer در Authorization header ارسال می‌شود؛ cookie session، query-token برای SignalR و هدر عمومی Idempotency-Key نداریم. public بودن OTP/refresh مجوز دسترسی به resource نیست.
- role، organization، membership و child جاری را سرور resolve می‌کند؛ selectorهای client مدرک مجوز نیستند. 401 ورود، 403 منع مجوز، 404 پنهان‌سازی/نبود منبع، 409/412 تضاد و 429 محدودیت باید از ProblemDetails/code خوانده شوند.
- تمام listها یک شکل paging ندارند؛ پارامتر، بدنه، status و schema دقیق هر عملیات در مرجع جاری است. validation کسب‌وکاری فراتر از nullable/required در OpenAPI اجرا می‌شود.

DOC-CONTRACT-01 در نسخهٔ مبنا CLOSED_IN_CODE_AND_LOCAL_CONTRACT_TESTS است؛ اختلاف integer/String قبلی در snapshot جاری دیده نشد. OpenAPI با serializer رشته‌ای MVC هم‌تراز شده است؛ literalهای زیر از همان CLR/JsonOptions استخراج می‌شوند. این نتیجه شاهد انتشار OpenAPI روی سرور نیست؛ named Flags همهٔ ترکیب‌های مجاز یا ورودی‌های مجاز use case را تعریف نمی‌کند.

## ورود، نشست و OTP

Real OTP: با Otp:Enabled=true و Otp:AllowMockAuthentication=false، POST /api/v1/auth/otp/challenges پاسخ challengeId/expiresAtUtc/resendAvailableAtUtc می‌دهد؛ verify همان شناسه، شماره و کد رشته‌ای را می‌گیرد. کد در پاسخ/log نیست و طول شش‌رقمی ثابت فرض نشود. lifetime پایه ۳۰۰ ثانیه و resend سی ثانیه است؛ ارقام فارسی/عربی normalize، challenge قبلی supersede و مصرف مجدد رد می‌شود.

Mock: فقط در Development/Test/Testing با جفت false/true مجاز است؛ Production/Staging در startup آن را رد می‌کنند. شکل request/response ثابت است ولی challenge واقعی در SQL ساخته نمی‌شود و verify پس از اعتبارسنجی فرمت شماره/کد، مقدار OTP یا challenge را اثبات نمی‌کند. کد فعلی ۴ تا ۶۴ کاراکتر بدون control character می‌پذیرد. این رفتار مالکیت شماره را اثبات نمی‌کند؛ UI حق تشخیص Mock از شکل پاسخ یا ساخت bypass عمومی ندارد.

در Mock نیز حساب معلق رد می‌شود و نشست، workspace، عضویت و role عادی‌اند؛ شمارهٔ جدید می‌تواند مطابق مسیر قبلی User بسازد، اما نقش آموزشی یا Platform ساخته نمی‌شود. ورود دوباره session جدید می‌سازد؛ verify در Mock را idempotent فرض نکنید. تغییر flag نشست‌های قبلی را revoke نمی‌کند. انتخاب محیط و سیاست داده وظیفه deployment است؛ [راهنمای OTP](../dev/CONFIGURABLE_OTP_FA.md) و [Test محدود](../dev/SERVER_MOCK_OTP_FA.md).

VerifyOtpResult شامل accessToken، tokenType، expiresAtUtc، refreshToken، refreshExpiresAtUtc و user است. access پایه یک ساعت و refresh چرخشی با عمر مطلق ۳۰ روز است؛ refresh token و OTP دادهٔ محرمانه‌اند. POST /api/v1/auth/refresh و APIهای sessions/logout برای lifecycle موجودند. storage امن سمت client تصمیم اجرایی جدا می‌خواهد؛ credential در URL، screenshot یا telemetry قرار نگیرد.

## پروفایل شخصی و عکس

PATCH /api/v1/me/profile همان نام‌ها و Username را با dateOfBirth optional به شکل YYYY-MM-DD میلادی می‌گیرد؛ آینده400 PROFILE_VALIDATION_FAILED، omission/null حفظ مقدار قبلی و clear تعریف نشده. DOB شرط تکمیل یا تأیید سن نیست؛ People/Directory/AI DOB تازه نمی‌گیرند. DateOfBirth خوداظهاری با VerifiedBirthDate مستقل فرق دارد.

عکس: upload موجود در Personal -> Ready JPEG/PNG خود کاربر با OrganizationId=null -> PUT /api/v1/me/profile-photo {fileAssetId}. DELETE همان مسیر204 و تکرارپذیر است؛ فقط مرجع پاک می‌شود. profilePhoto در پاسخ self و هویت امن Messenger nullable است. downloadUrl عمومی نیست و دریافت Bearer لازم دارد؛ client token را در URL نگذارد. نمایش عکس تابع grant همان هویت است و مجوز Send/Discovery تازه نیست. جایگزینی عکس blob قبلی را حذف نمی‌کند. [قرارداد عکس](../product/PROFILE_PHOTO_IMPLEMENTATION_FA.md).

## سازمان، پرونده و فرزند

Manager مدرسه می‌تواند از /api/v1/organization/people پرونده بسازد/بخواند/جست‌وجو یا تغییر مجاز نام دهد؛ نام Person حساب‌دار یا مشترک از این API تغییر نمی‌کند. دعوت اتصال حساب نیازمند User موجود و پذیرش صریح authenticated است؛ merge خودکار بر اساس نام/تلفن نداریم. پرونده، User، Membership، RoleAssignment و Enrollment مفاهیم جدا هستند.

Manager می‌تواند با POST /organization/people/{learnerId}/guardian-invitations و دقیقاً یکی از username/phoneNumber حساب موجود، دعوت والد بسازد؛ reason، clientOperationId و expiry آینده تا۳۰روز لازم‌اند. پاسخ مدیر هویت مقصد را افشا نمی‌کند. Parent در inbox خود با accept و confirmGuardianRelation=true رضایت می‌دهد؛ Membership/Parent Role و GuardianRelation در workflow موجود ایجاد/reuse می‌شوند و Enrollment یا Account جدید ساخته نمی‌شود. تکرار دقیق receipt و payload متفاوت409 است؛ رابطهٔ پایان‌یافته با replay زنده نمی‌شود. مسیرهای قدیمی GuardianRelation نیز حفظ‌اند.

Parent از children/select موجود استفاده می‌کند و revoke رابطه، cache قبلی را مجاز نمی‌کند. GuardianRelation مجوز private chat یا همه فایل‌های کودک نیست. GET /organization/members برای Manager و GET کلاس learners/teachers برای Manager/Teacher منتسب، Directory و roster جاری می‌دهند؛ orgId از workspace resolve می‌شود. [قرارداد کامل مدرسه](../product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md).

## آموزش، تکلیف و آزمون

AcademicPeriod/Course/Class/Enrollment/TeacherAssignment و Session/Schedule/Attendance موجودند. ساخت Class فقط Manager و ساخت Session برای Teacher منتسب است؛ نبود حضور را Absent نمایش ندهید. roster صفحه‌بندی‌شده با list آرایه‌ای قبلی classes یکسان نیست و version جدید به معنی پارامتر ExpectedVersion تازه در End قدیمی نیست. Transfer مستقل اتمیک نداریم.

Class Lesson/Handout: GET/POST classes/{id}/lessons، GET/PATCH lessons/{id} و POST lessons/{id}/publish زیر /academic. Manager/Teacher منتسب می‌نویسند؛ Student دارای Enrollment فعال فقط Published می‌بیند و Draft برای او404/پنهان است؛ Parent مجوز مستقل ندارد. PATCH کل title/body/displayOrder/fileAssetIds/expectedVersion را جایگزین می‌کند، [] اتصال فایل را حذف می‌کند نه blob؛ Published immutable است. body متن ساده است و publish حداقل متن یا فایل می‌خواهد. فایل Ready سازمانی مجاز و دانلود با grant جاری کلاس است؛ URL قدیمی پایان Assignment/Enrollment را دور نمی‌زند. این Lesson، AI Source یا Grade نیست.

AssignmentVersion سقف MaxScore مستقل و مثبت دارد؛ score در بازهٔ 0..MaxScore است و سقف سراسری ۲۰ نداریم. نسخهٔ منتشرشده، submission receipt، evaluation revision و GradeRelease معنای تاریخی را حفظ می‌کنند؛ Student/Parent نمرهٔ منتشرنشده و note خصوصی نمی‌بینند.

ExamVersion/QuestionVersion، شروع Attempt، محدودیت تلاش، deadline سرور، question freeze، AnswerRevision/autosave، WriteLease، finalize receipt و grading/release/correction موجودند. قطع شبکه submit موفق ساختگی نیست؛ receipt و read جاری مرجع‌اند. clock دستگاه، autosave acknowledgement یا state محلی جای زمان سرور و finalization رسمی نیست. auto-finalize/background grace به‌عنوان قابلیت جاری ادعا نمی‌شود.

## Messenger و ایمنی کودک

Personal Direct/Group/Channel و Contacts/privacy بدون School Role مجاز با gateهای حساب/سن/رابطه/profile/block موجودند؛ پایان نقش، User/عکس/گفتگوی شخصی را حذف نمی‌کند. برای Profile ناقص messaging-status فقط ReadExistingConversations و ProfileCompletionRequired دارد؛ hint مجوز create/discovery نیست. پس از پایان role، انتخاب Personal از endpoint workspace موجود انجام می‌شود؛ fallback پنهان نداریم.

Policy مصوب GAP-05 حاکم است: UnknownAge مانند Minor محافظت می‌شود؛ Stranger -> Minor/Unknown ممنوع است؛ هر Send، Block و رابطه/عضویت/permission جاری را دوباره بررسی می‌کند. پایان رابطه تاریخچه را خودکار حذف نمی‌کند. گروه/کانال شخصی و Official Class Group قواعد جدا دارند؛ نقش Manager یا Guardian مجوز Direct browsing نیست.

Internal Makan Content Safety provider واقعی قاعده‌محور محدود متن است، نه AI یا ادعای اسکن معنایی کامل. unavailable در مسیر محافظت‌شده fail-closed و رسانه unsupported ReviewRequired است؛ خطای safety را Sent نمایش ندهید. ارزیابی سنِ خوداظهاری تأیید رسمی نیست و AGE_VERIFICATION_CONFLICT با retry کور overwrite نمی‌شود.

SignalR تغییر پایدار را اعلان می‌کند؛ SQL/Delta مرجع پیام و read-state هستند. TypingChanged و PresenceChanged hint گذرا با TTL و privacy جاری‌اند، نه Message/Delta. handler فقط Bearer header می‌خواند؛ browser از LongPolling و withCredentials:false طبق allowlist میزبان استفاده کند. reconnect نیازمند ثبت دوباره subscriptionهای لازم و replay حساب از cursor ذخیره‌شده است؛ Delta گفتگو نیز مستقل حفظ شده است. backplane و ephemeral state مشترک چند instance فراهم نیست.

## گزارش، اعلان و تجارت

چهار داشبورد نقش‌محور، گزارش‌های filtered/paginated و export خصوصی CSV با Queued/Processing/Completed/Failed وجود دارند. مجوز ایجاد، worker و download دوباره بررسی می‌شود؛ job pending را فایل آماده نمایش ندهید.

اعلان داخلی، outbox، preferences و device lifecycle موجودند. Web Push/VAPID و FCM HTTP v1 با Notifications:PushProvider=WebPushAndFcm پیاده شده‌اند؛ ثبت device، dispatch یا پذیرش provider به معنی رسیدن به دستگاه نیست. P1_PUSH_RUNTIME_PENDING برقرار است؛ SMS عمومی اعلان همچنان unavailable و از OTP ملی‌پیامک جداست.

Plan/PlanVersion/Subscription/Entitlement/Quota و Order/PaymentAttempt/verification lifecycle موجودند؛ gateway واقعی unavailable است. callback یا UI هرگز پرداخت را بدون تأیید server موفق اعلام نمی‌کند؛ refund/reconcile دستی ساختگی نسازید.

## اشتراک Individual/Organization و PersonalClass

GET /commerce/catalog با audience، GET /commerce/personal/subscription و organization/subscription و Quote/Order هر مالک را مصرف کنید. Manager فقط سازمان جاری و Platform فقط با permission مرکزی مجاز است؛ خرید نقش یا دادهٔ آموزشی را مجاز نمی‌کند. Order تازه201/replay200، قیمت نمایشی تومان و مبنای فعلی درگاه IRR است؛ gateway واقعی unavailable است. Quote حق ایجاد نمی‌کند.

دوره ماه تقویمی با لنگر ثابت، تمدید دستی و AutoRenew=false است؛ Upgrade بعد از پرداخت معتبر سررسید را ثابت و Downgrade دورهٔ بعد را هدف می‌گیرد. LegacyPendingContract/AwaitingValidContract به معنی Unlimited نیست؛ کنترل سازمان فقط با ثبت معتبر Admin شروع می‌شود. Grace سازمان هفت روز و سپس write آموزشی مشمول قفل است؛ خواندن مجاز، پیام‌رسان شخصی و پرداخت حفظ می‌شوند.

Quota را با Key/Kind/Unit/Limit/Consumed/Reserved/Remaining و IsUsageAvailable بخوانید. Limit حذف‌شده با صفر و null متفاوت است؛ meter ناموجود را مصرف صفر نمایش ندهید. CurrentCapacity ماهانه reset نمی‌شود. Preflight متن ساده AI فقط بررسی است؛ Trial سه موفقیت روزانه Account با مرز تهران بنیاد قرارداد است و Runtime AI فعال نشده است. انتخاب payer شخصی در Organization صریح و بدون fallback است. ReportExport با رزرو و operationId موجود idempotent است؛ Unknown را موفق/شکست قطعی ندانید.

PersonalClass زیر /api/v1/personal-classes شامل ساخت/فهرست/دعوت/پذیرش/اعضا/پایان/archive است؛ HostUserId مالک، ظرفیت میزبان و عضویت پایه بدون خرید دانشجو است. CanCreateEducationalContent=false و EducationalWorkflowStatus=NotAvailable؛ Assignment/Exam مستقل هنوز ندارد. [Handoff کاربر](../product/SUBSCRIPTION_FRONTEND_HANDOFF_FA.md) و [Admin](../product/SUBSCRIPTION_ADMIN_HANDOFF_FA.md).

## Replay حساب و invalidation

listenerهای ConversationChanged و ClientInvalidated را قبل از Snapshot نصب و Hub را وصل کنید. GET /api/v1/realtime/changes بدون cursor فقط boundary bootstrap می‌دهد؛ سپس Snapshotهای مجاز را بخوانید و از NextCursor تا HasMore=false replay کنید. limit پیش‌فرض100/حداکثر200 است. صفحهٔ خالی با HasMore=true و RetryAfterMilliseconds=100 پایان نیست؛ cursor را از push جلو نبرید. ContextRevision opaque فقط برای برابری و جداسازی context است؛ پاسخ دیررس بافت قبلی نیز کنار گذاشته شود.

بر EventId deduplicate و REST را refetch کنید؛ StreamVersion stamp فنی است و ResourceVersion نیست. unread/grade را با event افزایش یا overwrite نکنید. Direct/Saved را از /conversations/{id}/summary بخوانید؛ GET /conversations/{id} مدیریتی Group/Channel است. پس از reconnect همان cursor را replay کنید؛ cursor گفتگو با cursor حساب متفاوت است.

409 REALTIME_RESYNC_REQUIRED یعنی bootstrap و Snapshot مجاز تازه؛ 503 REALTIME_TEMPORARILY_UNAVAILABLE یعنی backoff و RETRY_SAME_CURSOR؛429 تابع Retry-After است. cursor به User/Session/ContextRevision و key ring استاندارد host بسته و عمرش24ساعت است؛ refresh/context change نیز resync می‌خواهد. [قرارداد Event](../product/REALTIME_EVENT_CONTRACT_FA.md)، [Handoff](../product/MESSAGING_REALTIME_FRONTEND_HANDOFF_FA.md) و [Runbook](../operations/REALTIME_RUNBOOK_FA.md).

## پیوست پاسخ آموزشی و Safety پیام‌رسان

Upload با POST /files و فیلد File، دریافت metadata و Ready، سپس Attach مجاز انجام می‌شود؛ Ready به‌تنهایی Submit/Send نیست. تکلیف همان SubmissionAttempt Draft/RowVersion موجود را دارد. آزمون attachmentPolicy را از سؤال نسخهٔ تثبیت‌شده می‌خواند؛ old question default غیرفعال و policy فعال سقف مثبت تعداد/مجموع بایت و MIME صریح می‌خواهد. TextRequired فقط تشریحی و زمان Finalize است؛ Objective گزینهٔ معتبر را پیش از Attach می‌خواهد.

Attach پاسخ آزمون: POST /academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}/attachments با fileAssetId/clientOperationId/writeLeaseVersion/expectedRevisionNumber؛ Detach با DELETE همان مسیر/fileId و queryهای operation/lease/revision. Save فایل قبلی را حفظ می‌کند؛ هر mutation revision و AnswerSetVersion تازه می‌دهد. برای Finalize نسخهٔ جاری و lease را بفرستید؛ زمان دستگاه و پایان Upload deadline را تمدید نمی‌کنند. Teacher/Manager مجاز فقط finalized و Parent/Guardian raw file نمی‌خوانند. [جریان کامل و خطاها](../product/ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md#frontend_handoff).

BE-01 در Messenger مستقل است:422 با safetyState=PendingReview یا Rejected و operationState=NotApplied اثر جدید پیام/پیوست/تحویل نمی‌سازد. Draft را حفظ و KEEP_DRAFT_NO_AUTO_RETRY را رعایت کنید؛ safetyReference فقط ردیابی است. Human Review یا release خودکار هنوز PRODUCT_DECISION_BLOCKED است؛ بستن case تصمیم Safety را Allowed نمی‌کند. [قرارداد امن](../messaging/BE01_REVIEW_REQUIRED_CONTRACT_FA.md).
## Backoffice و مجوز Platform

Platform IAM/permission، organization/user/provisioning، education read model، moderation، commerce/finance و operations API دارند؛ Manager مدرسه Platform Admin نیست. GET /api/admin/v1/me منبع capability است، نه مجوز client-side.

طبق تصمیم مالک در ۲۰۲۶-۱۰-۲۴، Authenticator/TOTP، Recovery و Step-Up از کد فعال حذف شده‌اند؛ صفحه و فراخوانی MFA نباید وجود داشته باشد. OTP → Session → GET /api/admin/v1/me؛ در 200 پنل و در 403 عدم دسترسی Platform. گزارش‌های GAP-04 تاریخی‌اند.

Moderation case-scoped است؛ permission/claim/escalation/session و reason قواعد خود را دارند. دسترسی عمومی به Direct، Contacts، evidence خارج case، impersonation یا payment-success دستی وجود ندارد. [تحویل تخصصی Backoffice](../platform/BACKOFFICE_FRONTEND_HANDOFF.md).

## AI و قابلیت‌های وابسته

۱۲ API عمومی AI از Backend و Bearer نشست موجود مصرف می‌شوند؛ AiService:Enabled پیش‌فرض false است. URL خصوصی AI، JWT سرویس و کلید Provider به client داده نمی‌شوند. سرور پیش از network، context مجاز نقش/سازمان/فرزند/کلاس را می‌سازد و پاسخ v1 را از نظر requestId/correlationId، فیلد ناشناخته و شناسه‌های مستند در همان context بررسی می‌کند.

بودجهٔ پیش‌فرض AiService:TimeoutSeconds برابر ۳۵ ثانیه و محدودهٔ تنظیم ۱..۱۸۰ است؛ client retry خودکار عمومی نسازد. 429 با AI_RATE_LIMITED، 503/خطای مدل یا اتصال با AI_SERVICE_UNAVAILABLE، 504 با AI_SERVICE_TIMEOUT و schema/grounding نامعتبر با AI_RESPONSE_INVALID نگاشت می‌شود؛ متن خام خطای Provider نمایش داده نمی‌شود. AI_CONTEXT_NOT_ALLOWED پیش از تماس خارجی 403 است؛ AI_FEATURE_DISABLED برابر 503 است. public Retry-After تضمین نشده است.

Service Auth واقعی در شواهد پیشین PASS شده؛ Parent Child Summary هنوز Provider Timeout دارد و Full-12 واقعی Deferred است. نتیجه Fake-AI یا health این gate را نمی‌بندد. RAG source lifecycle نیز deferred و core آموزشی مستقل از AI است. [وضعیت جاری AI](../ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md) و [شواهد Consolidation](../release/BACKEND_FINAL_CONSOLIDATION_FA.md).

## میزبانی و smoke

Swagger/OpenAPI در Production/Staging پیش‌فرض خاموش و با Swagger:Enabled=true فعال است؛ Test طبق Program آن‌ها را منتشر می‌کند. Cors:AllowAnyOrigin انتخاب مستقل از محیط است؛ base و نمونه Test/Production فعلی true با AllowedHosts=* هستند. true از AllowAnyOrigin/Header/Method و بدون credentials استفاده می‌کند؛ false فهرست معتبر لازم دارد. REST با credentials:omit و SignalR با LongPolling و withCredentials:false، نه query-token؛ موفقیت Postman رفتار مرورگر نیست. [قرارداد CORS](../production/CORS_CONFIGURATION_FA.md).

/health و /health/live فقط liveness و /health/ready config/SQL/Storage را می‌سنجد؛ probe و ring اختصاصی MFA حذف شده‌اند؛ Data Protection استاندارد host از cursor حساب حفاظت می‌کند و پایداری کلیدهایش مستقل از readiness بررسی می‌شود. provider اختیاری core را نمی‌بندد. مدل snapshot اثبات applied migration روی سرور نیست.


Job operations اکنون Queues تفکیک‌شده، تعدادها و سن قدیمی‌ترین کار را می‌دهد؛ notification operations شمارش pending/processing/deferred/failed و سن oldest و failureهای اخیر را دارد. health مدیریتی وضعیت dependencyهای جاری و FileCleanup را بدون path/key نشان می‌دهد. FileCleanup=Enabled فقط تنظیم است. export فقط پس از Completed و دانلود مجاز آماده است؛ Lost ACK یا retry worker موفقیت نمایشی جدید ایجاد نمی‌کند. [قرارداد Backoffice](../platform/BACKOFFICE_FRONTEND_HANDOFF.md).

انتشار فعلی مالک HTTP است و دو استثنای مستقل صریح دارد؛ default امن حفظ شده و TLS یا gate GAP-02 بسته نشده است. فایل Staging حذف و فایل Production واقعی خصوصی/ignored است. در mode=false، Origin باید دقیقاً در allowlist باشد؛ mode=true اجازه cookie credentials نمی‌دهد. موفقیت Postman رفتار مرورگر را اثبات نمی‌کند. Identity key/Storage در redeploy حفظ شوند؛ فایل‌های قدیمی key ring سرور تا cleanup مستقل حذف نشوند. [runbook](../production/DEPLOYMENT_IIS.md).

## روش نگه‌داری و شواهد

ابزار DocumentationSnapshot فقط TestServer بدون worker و مدل طراحی EF را می‌خواند؛ SMS/AI/SQL واقعی فراخوانی نمی‌شود. sync_repository_docs.py همهٔ method/routeها و ارجاع schemaها را تطبیق می‌دهد و build_backend_docs.py خروجی‌های HTML/PDF را می‌سازد. نام/فیلد/enum از metadata است؛ موفقیت سناریوی محصول یا محیط هدف از آن استنتاج نمی‌شود. [فهرست نگه‌داری اسناد](../documentation-inventory.csv).

## مسیر مطالعه

این راهنما -> مرجع جاری endpoint/DTO -> Controller واقعی -> سرویس Application -> Domain و port -> Infrastructure/EF configuration -> Migration -> Unit/Integration/Architecture tests. اسناد مرحله‌ای و PDFهای اصلی برای دلیل تصمیم و UX خوانده شوند؛ اعداد تست تاریخی به‌جای validation امروز استفاده نشوند.


## افزوده‌های جاری Messenger

[وضعیت یکپارچه P0/P1/P2 و gate رسانه](../messaging/CURRENT_MESSENGER_STATUS.md) و Handoffهای [P1](../messaging/MESSENGER_P1_FRONTEND_HANDOFF.md) و [P2](../messaging/MESSENGER_P2_FRONTEND_HANDOFF.md) مکمل کارت‌های کامل HTTP همین خروجی‌اند.

| حوزه | وضعیت جاری و مرجع |
|---|---|
| سن و شروع ارتباط | تأیید اولیهٔ رسمی سن، وضعیت امن self و Direct دو حساب Adult طبق GAP-05 در P0 موجودند؛ OTP/DOB خوداظهاری اثبات سن نیست |
| Peer Read | read-state مجاز Direct در P1؛ Group/Channel رسید per-member ندارند و Hidden علت خصوصی را افشا نمی‌کند |
| Web Push / FCM | transport واقعی و رجیستری خصوصی دستگاه در P1؛ `P1_PUSH_RUNTIME_PENDING` باقی است؛ SMS عمومی اعلان provider واقعی ندارد |
| Typing / Presence | lease و TTL واقعی با subscription و privacy جاری در P2؛ state گذرا در حافظه و تک‌instance، نه تاریخچه دائمی یا توزیع‌شده |
| Folder / Saved Labels | سازمان‌دهی خصوصی کاربر و قرارداد نسخه‌دار در P2؛ انتخاب پوشه/برچسب اجازه خواندن تازه نمی‌سازد |
| Invite / Join / Restriction | لینک دعوت، درخواست عضویت و محدودیت عضو طبق نقش و current policy در P2؛ token دعوت credential محرمانه است |
| Topics | lifecycle موضوع و topicId پیام در P2؛ موضوع حذف‌شده یا خارج scope انتخاب مجاز نیست |
| Scheduled / Silent | قصد ارسال پایدار، لغو و worker با بازبینی مجوز و safety هنگام dispatch؛ Scheduled به معنی Message/Sent نیست؛ Silent همان pipeline پیام با سیاست اعلان خودش است |
| Poll / Quiz | MessageKind.Poll و endpointهای مستقل Messenger؛ anonymous/multiple-choice و quiz یک‌بار پاسخ طبق قرارداد؛ کلید quiz قبل موعد انتشار عمومی افشا نمی‌شود |
| Checklist | MessageKind.Checklist=8؛ حالت Collaborative و Personal مستقل از Poll و Assessment؛ تعریف مشترک و وضعیت شخصی قرارداد جدا دارند |
| Search | فیلترهای hasAttachment/hasLinks/topicId و current authorization قبل paging؛ false نیز فیلتر است؛ link search واکشی URL نیست |
| Voice / Video / Live | policy رسانه مصوب است، ولی `P3_S_INFRA_REQUIRED`؛ LiveKit/TURN واقعی اجرا یا پذیرفته نشده‌اند؛ endpoint یا SDK تماس محصول از این مصوبه نتیجه نمی‌شود |


Hub اکنون Subscribe/UnsubscribeConversation، SetTyping، Heartbeat و Subscribe/UnsubscribePresence دارد؛ TypingChanged و PresenceChanged hint با TTL هستند، نه Message/Delta. بعد reconnect subscription تازه و Delta لازم است. default typing TTL=6s، refresh=3s، heartbeat=10s، lease=45s است؛ پیاده‌سازی حافظه‌ای تک‌instance و فاقد backplane است. Hidden اطلاعات private peer را افشا نمی‌کند. مجوز هر Send/Dispatch از وضعیت جاری ساخته می‌شود.
