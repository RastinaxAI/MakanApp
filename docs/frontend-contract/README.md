# قرارداد جاری مصرف Backend برای Web و Mobile

بازبینی `2026-10-15` از `main@80d6a14f6592af7de2b1b327b857505d70c4922d`؛ این سند در مخزن Backend است و مجوز ساخت، clone یا تغییر frontend نیست.

پوشش جاری: 284 عملیات HTTP در 256 مسیر، ۴۸ Controller، 488 schema، ۸۸ جدول و ۳۷ Migration. فهرست کامل کارت‌های HTTP و DTOها در HTML/PDF همین تحویل ضمیمه شده است.

- [HTML مستقل](MakanApp-Frontend-Backend-Contract.html) و [PDF](MakanApp-Frontend-Backend-Contract.pdf): قرارداد جاری و کاتالوگ کامل API.
- [مرجع Markdown قراردادها](../backend-api-reference.md)، [راهنمای جریان کد](../backend-api-guide.md)، [ساختار دیتابیس](../database-current-structure.md).
- [UX مرجع اصلی](../project-context/03_Makan_User_Flows_and_Screens_UX_v1.0.pdf) و [نقشه Flow](../project-context/04_Flow.png) نیازمندی‌اند، نه اثبات وجود screen یا frontend در این workspace. اطلاعات snapshot قدیمی ۱۴۱ عملیات/۲۰ Migration وضعیت جاری نیست.

## قرارداد مشترک انتقال و مجوز

- API اصلی /api/v1 و Backoffice مستقل /api/admin/v1 است. شناسهٔ GUID، enum رشته‌ای، زمان UTC و rowversion/Base64 را از response واقعی بگیرید؛ placeholderهای سند دادهٔ آمادهٔ محیط نیستند.
- Bearer در Authorization header ارسال می‌شود؛ cookie session، query-token برای SignalR و هدر عمومی Idempotency-Key نداریم. public بودن OTP/refresh مجوز دسترسی به resource نیست.
- role، organization، membership و child جاری را سرور resolve می‌کند؛ selectorهای client مدرک مجوز نیستند. 401 ورود، 403 منع مجوز، 404 پنهان‌سازی/نبود منبع، 409/412 تضاد و 429 محدودیت باید از ProblemDetails/code خوانده شوند.
- تمام listها یک شکل paging ندارند؛ پارامتر، بدنه، status و schema دقیق هر عملیات در مرجع جاری است. validation کسب‌وکاری فراتر از nullable/required در OpenAPI اجرا می‌شود.

هشدار DOC-CONTRACT-01: OpenAPI خام این نسخه بعضی Enumها را integer معرفی می‌کند، در حالی که serializer واقعی MVC خروجی canonical رشته‌ای دارد. literalهای مرجع از همان JsonOptions استخراج شده‌اند؛ client را بدون کنترل این اختلاف از schema عددی تولید نکنید. runtime برنامه در این همگام‌سازی اصلاح نشده است.

## ورود، نشست و OTP

POST /api/v1/auth/otp/challenges -> challengeId/expiresAtUtc/resendAvailableAtUtc؛ سپس verify با همان challengeId/phoneNumber و code رشته‌ای. کد در پاسخ/log نیست و UI نباید طول ثابت شش‌رقمی فرض کند. lifetime پایه ۳۰۰ ثانیه و فاصله resend سی ثانیه است. ارقام فارسی/عربی normalize می‌شوند. challenge جدید قبلی را supersede می‌کند و OTP مصرف‌شده دوباره معتبر نیست.

VerifyOtpResult شامل accessToken، tokenType، expiresAtUtc، refreshToken، refreshExpiresAtUtc و user است. access پایه یک ساعت و refresh چرخشی با عمر مطلق ۳۰ روز است؛ refresh token و OTP دادهٔ محرمانه‌اند. POST /api/v1/auth/refresh و APIهای sessions/logout برای lifecycle موجودند. storage امن سمت client تصمیم اجرایی جدا می‌خواهد؛ credential در URL، screenshot یا telemetry قرار نگیرد.

## سازمان، پرونده و فرزند

Manager مدرسه می‌تواند از /api/v1/organization/people پرونده بسازد/بخواند/جست‌وجو یا تغییر مجاز نام دهد؛ نام Person حساب‌دار یا مشترک از این API تغییر نمی‌کند. دعوت اتصال حساب نیازمند User موجود و پذیرش صریح authenticated است؛ merge خودکار بر اساس نام/تلفن نداریم. پرونده، User، Membership، RoleAssignment و Enrollment مفاهیم جدا هستند.

مدیر از /api/v1/organization/guardian-relations رابطه Pending می‌سازد و با نسخهٔ جاری activate/end/revoke می‌کند. Parent از children و select موجود استفاده می‌کند؛ پس از revoke، cache قدیمی دسترسی نمی‌سازد. dashboard والد بدون انتخاب فرزند می‌تواند 200 خالی بدهد؛ Reports به context معتبر نیاز دارند. GuardianRelation هیچ مجوز private chat browsing نمی‌دهد.

## آموزش، تکلیف و آزمون

AcademicPeriod/Course/Class/Enrollment/TeacherAssignment، Session/Schedule/Attendance و revisionهای رسمی موجودند. Teacher فقط کلاس منتسب و Student/Parent فقط رابطهٔ مجاز خود را می‌بیند؛ نبود حضور را Absent نمایش ندهید.

AssignmentVersion سقف MaxScore مستقل و مثبت دارد؛ score در بازهٔ 0..MaxScore است و سقف سراسری ۲۰ نداریم. نسخهٔ منتشرشده، submission receipt، evaluation revision و GradeRelease معنای تاریخی را حفظ می‌کنند؛ Student/Parent نمرهٔ منتشرنشده و note خصوصی نمی‌بینند.

ExamVersion/QuestionVersion، شروع Attempt، محدودیت تلاش، deadline سرور، question freeze، AnswerRevision/autosave، WriteLease، finalize receipt و grading/release/correction موجودند. قطع شبکه submit موفق ساختگی نیست؛ receipt و read جاری مرجع‌اند. clock دستگاه، autosave acknowledgement یا state محلی جای زمان سرور و finalization رسمی نیست. auto-finalize/background grace به‌عنوان قابلیت جاری ادعا نمی‌شود.

## Messenger و ایمنی کودک

Contacts/profile privacy/discovery، Personal Direct، group/channel و مالکیت، revision/reaction/reply/mention، Saved Messages، state و delta sync وجود دارند. نام/شماره/UserId به‌تنهایی اجازهٔ CreateDirect/Send/Read نمی‌سازد.

Policy مصوب GAP-05 حاکم است: UnknownAge مانند Minor محافظت می‌شود؛ Stranger -> Minor/Unknown ممنوع است؛ هر Send، Block و رابطه/عضویت/permission جاری را دوباره بررسی می‌کند. پایان رابطه تاریخچه را خودکار حذف نمی‌کند. گروه/کانال شخصی و Official Class Group قواعد جدا دارند؛ نقش Manager یا Guardian مجوز Direct browsing نیست.

Internal Makan Content Safety provider واقعی قاعده‌محور محدود متن است، نه AI یا ادعای اسکن معنایی کامل. unavailable در مسیر محافظت‌شده fail-closed و رسانه unsupported ReviewRequired است؛ خطای safety را Sent نمایش ندهید. ارزیابی سنِ خوداظهاری تأیید رسمی نیست و AGE_VERIFICATION_CONFLICT با retry کور overwrite نمی‌شود.

SignalR اعلان تغییر است؛ SQL و delta/read رسمی مرجع‌اند. handler فقط Bearer header می‌خواند؛ مرورگر می‌تواند LongPolling همراه accessTokenFactory و withCredentials:false را در محیط مجاز آزمایش کند. دریافت event یا online بودن، receipt تحویل/خواندن مخاطب را به‌تنهایی ثابت نمی‌کند. scale-out/backplane آماده فرض نشود.

## گزارش، اعلان و تجارت

چهار داشبورد نقش‌محور، گزارش‌های filtered/paginated و export خصوصی CSV با Queued/Processing/Completed/Failed وجود دارند. مجوز ایجاد، worker و download دوباره بررسی می‌شود؛ job pending را فایل آماده نمایش ندهید.

اعلان داخلی، read state، durable outbox، preferences و device lifecycle API دارند. Push و SMS عمومی اعلان provider واقعی ندارند؛ آن‌ها را با OTP ملی‌پیامک اشتباه نگیرید.

Plan/PlanVersion/Subscription/Entitlement/Quota و Order/PaymentAttempt/verification lifecycle موجودند؛ gateway واقعی unavailable است. callback یا UI هرگز پرداخت را بدون تأیید server موفق اعلام نمی‌کند؛ refund/reconcile دستی ساختگی نسازید.

## Backoffice و MFA

Platform IAM/permission، organization/user/provisioning، education read model، moderation، commerce/finance و operations API دارند؛ Manager مدرسه Platform Admin نیست. GET /api/admin/v1/me منبع capability است، نه مجوز client-side.

TOTP/recovery/step-up واقعی از GAP-04 موجود است. /api/admin/v1/mfa شامل status، enrollment/confirm، verify، recovery/verify، recovery-codes/regenerate و reset مجاز است. SetupUri و recovery code فقط در پاسخ‌های اختصاصی یک‌بار دیده می‌شوند؛ secret در UI/log پایدار نماند. MFA_REQUIRED و PLATFORM_STEP_UP_REQUIRED را با جریان موجود حل کنید، نه bypass یا سیستم موازی.

Moderation case-scoped است؛ permission/claim/escalation/fresh MFA و reason قواعد خود را دارند. دسترسی عمومی به Direct، Contacts، evidence خارج case، impersonation یا payment-success دستی وجود ندارد. [تحویل تخصصی Backoffice](../platform/BACKOFFICE_FRONTEND_HANDOFF.md).

## AI و قابلیت‌های وابسته

۱۲ API Backend AI و قرارداد v1 وجود دارند، ولی AiService:Enabled پیش‌فرض false است. اعتبار provider، real Backend↔AI E2E و RAG source lifecycle بسته‌شده فرض نشوند. core آموزشی به AI وابسته نیست. فایل‌های dirty قبلی AI خارج از تحویل مستندات‌اند؛ این سند هیچ prompt/provider/security contract جدیدی ایجاد نمی‌کند.

## میزبانی و smoke

Swagger/OpenAPI در Production پیش‌فرض خاموش و CORS allowlist بدون credentials است. /health و /health/live فقط process و /health/ready، SQL + Storage را probe می‌کند؛ سلامت آن تحویل SMS/پرداخت/AI را ثابت نمی‌کند.

انتشار فعلی مالک HTTP است و دو استثنای مستقل صریح دارد؛ default امن حفظ شده و TLS یا gate GAP-02 بسته نشده است. فایل Staging حذف و فایل Production واقعی خصوصی/ignored است. Origin مجاز باید دقیقاً Origin کلاینت باشد؛ موفقیت Postman رفتار مرورگر را اثبات نمی‌کند. MFA key ring/Identity key/Storage در redeploy حفظ شوند. [runbook](../production/DEPLOYMENT_IIS.md).

## روش نگه‌داری و شواهد

ابزار DocumentationSnapshot فقط TestServer بدون worker و مدل طراحی EF را می‌خواند؛ SMS/AI/SQL واقعی فراخوانی نمی‌شود. sync_repository_docs.py همهٔ method/routeها و ارجاع schemaها را تطبیق می‌دهد و build_backend_docs.py خروجی‌های HTML/PDF را می‌سازد. نام/فیلد/enum از metadata است؛ موفقیت سناریوی محصول یا محیط هدف از آن استنتاج نمی‌شود. [فهرست نگه‌داری اسناد](../documentation-inventory.csv).

## مسیر مطالعه

این راهنما -> مرجع جاری endpoint/DTO -> Controller واقعی -> سرویس Application -> Domain و port -> Infrastructure/EF configuration -> Migration -> Unit/Integration/Architecture tests. اسناد مرحله‌ای و PDFهای اصلی برای دلیل تصمیم و UX خوانده شوند؛ اعداد تست تاریخی به‌جای validation امروز استفاده نشوند.
