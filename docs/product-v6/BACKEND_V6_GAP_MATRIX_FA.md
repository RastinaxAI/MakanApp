# ماتریس تطبیق Backend با Product v6

> وضعیت جاری در بخش «نهایی‌سازی انتخاب مدیر و آماده‌سازی» انتهای سند است. ردیف‌های Audit و تحویل قبلی با تاریخ و Commit خود حفظ شده‌اند؛ D01 و D17 اکنون بسته‌اند. تنظیم تجاری واقعی Production و محدودیت‌های دیگر v6 جدا هستند.

مبنای Audit پیش از تغییر Runtime: `e5bd3eae658991585ca09f581f6236351857b3b8`، تاریخ `2026-11-12`، شاخهٔ کاری `feature/product-v6-backend-alignment`. سه فایل untracked قبلی AI و تنظیمات خصوصی با Backup/هش محافظت شده‌اند. این سند وضعیت اولیه است؛ نتیجهٔ اجرای تغییرات و ارزیابی نهایی جدا ثبت می‌شوند.

منابع کامل خوانده‌شده: `01_BACKEND_TASKS_FA.md` نسخه۶ و `MakanApp_Product_Architecture_FA_v6.pdf`، ۵۱ صفحه. SHA256 فایل PDF: `2b8f817dd9d91097a9fc4e34e0564f37fd4ad6b4a771a53d78e7350201250609`. سند هدف محصول، شاهد پیاده‌سازی یا Deploy نیست. تصمیم صریح پرامپت دربارهٔ خاموش‌بودن Service Authentication دو سرویس AI بر متن قدیمی BE-19 اولویت دارد.

Snapshot تازهٔ محلی: 403 path، 454 operation، 732 schema، 82 Controller، 128 جدول و 65 Migration. SHA256 OpenAPI: `1a100ee9422348f74ffd557a1a06f3d86520e4c530a11522d9838baf904b3680`. ابزار موجود `tools/docs/MakanApp.DocumentationSnapshot/DocumentationSnapshot.cs` بدون اتصال DB و بدون Hosted Worker اجرا شد. Product v6 در PRD-55 به Snapshot بیرونی 735 schema اشاره دارد؛ این اختلاف به معنی وجود سه قابلیت اضافی در Source فعلی نیست. نتایج 3092 تست baseline مربوط به اجرای تأییدشدهٔ قبلی‌اند، نه تست تازهٔ این Task.

## BE-01 — Contract Audit

- Requirement: نقشهٔ همهٔ قراردادها، مدل، Migration و تست واقعی.
- Current Implementation / Evidence: Snapshot موجود و تازهٔ OpenAPI؛ `MakanDbContext`، Controllerها و تست‌های سه پروژه؛ همین ماتریس تمام ۲۵ کار را پوشش می‌دهد.
- Status: `IMPLEMENTED`.
- Gap / Required Change: ثبت اختلاف هدف/Source و نگه‌داشتن گزارش اولیه از نتیجهٔ نهایی جدا.
- Tests Needed: Snapshot نهایی و سازگاری قرارداد پس از تغییر؛ baseline موجود جای Regression تازه را نمی‌گیرد.
- Migration Needed: خیر.
- Product Decision Needed: خیر.

## BE-02 — Identity / Profile / Context

- Requirement: هویت ثابت، Bio، تغییر شمارهٔ امن و نشست معتبر؛ PRD-05..10.
- Current Implementation / Evidence: `IdentityService`، `IdentityContracts`، `UserCredential`، `UserSession` و `ProfileController`؛ OTP واقعی/Mock محافظت‌شده، refresh rotation، DOB و Avatar موجودند. `CompleteProfileCommand` و `CurrentUserResult` فاقد Bio و `IIdentityService` فاقد تغییر شماره است.
- Status: `PARTIAL`.
- Gap / Required Change: Bio اختیاری متن ساده و محدود؛ چرخهٔ مستقل تغییر شماره با OTP واقعی، یکتایی، audit و حفظ UserId.
- Tests Needed: حفظ قرارداد قدیمی Bio، اعتبارسنجی متن، OTP نامعتبر/منقضی/تکراری، Mock ممنوع در تغییر شماره، یکتایی/هم‌زمانی و ابطال نشست‌ها.
- Migration Needed: بله؛ Bio و persistence درخواست تغییر شماره.
- Product Decision Needed: حداقل سیاست نشست/تأیید تغییر شماره برای تأیید مالک ارسال شده؛ حداکثر Bio فقط safeguard فنی مستند خواهد بود.

## BE-03 — School Application

- Requirement: دو نوع درخواست، مدارک خصوصی، اصلاح/تصمیم و سپس Quote→Payment→Provisioning؛ PRD-46/47.
- Current Implementation / Evidence: `PlatformProvisioningRequest` و `EfPlatformOperationsStore.ProvisionAsync` سازمان/اشتراک/دعوت مدیر را مرحله‌ای و retry-safe می‌سازند. مدل یا API متقاضی با نام SchoolApplication وجود ندارد؛ Provisioning معادل درخواست تأسیس نیست.
- Status: `MISSING`.
- Gap / Required Change: پرونده/نسخه/مدرک/تصمیم با دو ظرفیت مستقل؛ Review با Permission و اتصال صریح تجاری پس از Approval؛ reuse همان Provisioning و Commerce.
- Tests Needed: owner/reviewer ACL، مدارک، اصلاح همان پرونده، ExpectedVersion، retry/رقابت تصمیم و یک Organization/Order/دعوت منطقی.
- Migration Needed: بله؛ افزودنی، بدون backfill حدسی درخواست یا ظرفیت.
- Product Decision Needed: حداقل فیلد/مدرک الزامی Submit برای دو نوع به مالک ارائه شده؛ قیمت/سهمیه/دوره باید دادهٔ صریح Admin باشد.

## BE-04 — Organization / Guardian

- Requirement: چندسازمانی/چندنقشی و رابطهٔ تأییدشده؛ لغو جاری.
- Current Implementation / Evidence: `Membership`، `RoleAssignment`، `GuardianRelation`، `OrganizationPeopleController`، `GuardianEndpointsTests` و `OrganizationPersonOnboardingEndpointsTests.Guardian`؛ دعوت و رضایت حساب از Membership/Enrollment جداست.
- Status: `IMPLEMENTED`.
- Gap / Required Change: reuse؛ هیچ merge براساس شماره/نام یا مجوز چت از Guardian ساخته نشود.
- Tests Needed: Regression والد نامرتبط، پایان عضویت/رابطه و context switch.
- Migration Needed: برای همین قابلیت خیر.
- Product Decision Needed: عضویت والد در فضای رسمی، مستقل از دسترسی آموزشی، هنوز باز است؛ سیاست موجود خودسرانه عوض نمی‌شود.

## BE-05 — Academic Core

- Requirement: ترم/دوره/کلاس سازمانی، Lesson، Session، Schedule و Attendance.
- Current Implementation / Evidence: DbSetهای `AcademicPeriod`، `Course`، `ClassLesson`، `Class`، `Enrollment`، `TeacherAssignment`، `Session`، `ScheduleRule` و `AttendanceRevision` در `MakanDbContext`؛ Controllerهای Academic و Session/Attendance موجودند.
- Status: `IMPLEMENTED`.
- Gap / Required Change: reuse؛ خاموشی PersonalClass نباید به این دامنه سرایت کند.
- Tests Needed: Regression جداسازی سازمان/کلاس و حضور/ظرفیت/نسخه.
- Migration Needed: برای همین قابلیت خیر.
- Product Decision Needed: قابلیت آموزشی پیشنهادی PersonalClass از این هستهٔ سازمانی جداست.

## BE-06 — Organization Assignments

- Requirement: Draft/Publish، Recipient، Submission، Attachment، Evaluation/Release/Correction.
- Current Implementation / Evidence: `Assignment`/`AssignmentVersion`، `SubmissionAttempt`، `EvaluationRevision`، `GradeRelease` و Controllerهای Assignment/Submission؛ FK/rowversion/رسید در مدل و تست موجودند.
- Status: `IMPLEMENTED`.
- Gap / Required Change: حفظ engine؛ Gate تنها برای AcademicClassKind.PersonalClass.
- Tests Needed: Organization همچنان کار کند؛ idempotency/score ceiling/deadline/Grade privacy و attachment regression.
- Migration Needed: برای Gate خیر.
- Product Decision Needed: سیاست‌های مصوب موجود حفظ شوند؛ عدد آموزشی تازه حدس زده نشود.

## BE-07 — Organization Exams

- Requirement: نسخه سؤال، Attempt/Deadline، WriteLease، AnswerRevision، Final Receipt و Grade Release.
- Current Implementation / Evidence: `ExamVersion`، `ExamAttempt`، `AnswerRevision`، `ExamFinalAnswer` و `ExamGradeRevision`؛ APIهای author/start/autosave/finalize/result موجودند.
- Status: `IMPLEMENTED`.
- Gap / Required Change: Gate PersonalClass به مسیرهای مشترکِ resource-id هم اعمال شود؛ هستهٔ سازمانی حفظ گردد.
- Tests Needed: زمان سرور، retry، نسخه، concurrency، نتیجهٔ منتشرشده و School regression.
- Migration Needed: برای Gate خیر.
- Product Decision Needed: قابلیت آموزشی مستقل فعلاً فعال نمی‌شود.

## BE-08 — Reports

- Requirement: شاخص‌های واقعی، فیلتر، pagination و export مجاز سازمانی.
- Current Implementation / Evidence: `DetailedReportService`، `ReportExportService`، `ReportExportJob` و `ReportsController`؛ جدول/worker خروجی موجود است.
- Status: `IMPLEMENTED`.
- Gap / Required Change: reuse و Regression؛ گزارش آموزشی PersonalClass با عدد نمونه یا scope حدسی ساخته نشود.
- Tests Needed: جمع دادهٔ واقعی، نسخهٔ نمره و ACL دانلود Export.
- Migration Needed: خیر، مگر Gap واقعی دیگری در تست ثابت شود.
- Product Decision Needed: دامنهٔ گزارش آموزشی PersonalClass باز است.

## BE-09 — PersonalClass Lifecycle

- Requirement: Draft→Submitted→UnderReview→NeedsChanges/Approved/Rejected و آماده‌سازی مستقل.
- Current Implementation / Evidence: `PersonalClassRequest` همین stateها و Cancelled را دارد؛ `PersonalClassRequestsController`، `PlatformPersonalClassRequestsController` و `PersonalClassV2RecoveryTests` موجودند.
- Status: `IMPLEMENTED`.
- Gap / Required Change: reuse؛ نه Organization صوری ساخته شود و نه درخواست موجود بازنویسی گردد.
- Tests Needed: نقش/مدرسه فهرست را تغییر ندهد، approve/retry و تاریخچهٔ مدیر.
- Migration Needed: برای رفتار موجود خیر.
- Product Decision Needed: restore/reopen و نگهداری تاریخچهٔ بایگانی طبق PRD-42 بازند؛ وضعیت تازه خودکار اضافه نمی‌شود.

## BE-10 — PersonalClass Membership

- Requirement: نقش محلی چندگانه، دعوت/JoinRequest، ظرفیت، تفویض و انتقال مدیر.
- Current Implementation / Evidence: `PersonalClassParticipant`، `PersonalClassInvitation`، `PersonalClassJoinRequest`، `PersonalClassManagerTransfer`؛ `PersonalClassV2EndpointsTests` و `PersonalClassInvitationIdentityTests`.
- Status: `IMPLEMENTED`.
- Gap / Required Change: reuse؛ Academic grant موجود با فعال‌بودن محصول یکی نشود.
- Tests Needed: آخرین مدیر، رقابت ظرفیت، revoke و عضویت عادی/Student.
- Migration Needed: خیر.
- Product Decision Needed: Scope پیشنهادی BE-25، نه عضویت Core.

## BE-11 — Official Spaces

- Requirement: جفت رسمی یکتا برای Organization/Class/PersonalClass، repair و current-source ACL.
- Current Implementation / Evidence: `OfficialSpaceProvision`، سه OfficialSourceKind، APIهای official-spaces/repair و `PersonalClassV2MessagingTests`؛ metadata رسمی از سرور است.
- Status: `IMPLEMENTED`.
- Gap / Required Change: reuse؛ read هیچ repair پنهان نکند و revoke منبع حفظ شود.
- Tests Needed: partial failure/retry، عدم جعل badge و عدم ورود بدون عضویت منبع.
- Migration Needed: خیر.
- Product Decision Needed: والد/کانال اطلاعیه از پیش‌فرض عضویت عام استنتاج نشود؛ PRD-28 باز است.

## BE-12 — Account Messaging

- Requirement: Inbox/DM/Read/Presence/Replay مستقل از نمای مدرسه.
- Current Implementation / Evidence: `MessagingOperationContextResolver` از قبل `PersonalAsync` و انتخاب context همان عملیات را دارد؛ account-inbox/changes و `PersonalMessagingRoleLifecycleTests` موجودند.
- Status: `PARTIAL`.
- Gap / Required Change: پذیرش چندنقشی v6 با یک ConversationId، دفترچه و PersonalClass ثابت؛ فقط نقص ثابت‌شده اصلاح شود.
- Tests Needed: Teacher A→Parent B، Session revoke، Personal vs Official ACL، ارسال/ویرایش/حذف/پیوست/Read/Presence/Replay.
- Migration Needed: فعلاً خیر.
- Product Decision Needed: stranger/age defaults جدید در PRD-12 بازند؛ سیاست مصوب جاری حفظ می‌شود و پیشنهاد Request Messaging خودکار فعال نمی‌شود.

## BE-13 — Personal Group / Channel

- Requirement: مدیریت شخصی، دعوت، پوشه، Saved، Poll و Scheduled با مجوز جاری.
- Current Implementation / Evidence: `Conversation`، `ChatFolder`، `ConversationPoll`، `ScheduledMessage` و تست‌های `MessagingP2*`؛ ownership/invite/participant APIها موجودند.
- Status: `IMPLEMENTED`.
- Gap / Required Change: reuse؛ شماره/عنوان مشابه مدرسه مجوز رسمی ایجاد نکند.
- Tests Needed: revoke در عملیات زمان‌دار و Group/Channel regression.
- Migration Needed: خیر.
- Product Decision Needed: پیشنهادهای جدید discoverability/stranger از سیاست فعلی جدا ثبت شوند.

## BE-14 — Address Book v2

- Requirement: نام و چند شماره، غیرعضو، consent/device sync/delta/tombstone؛ PRD-14..20.
- Current Implementation / Evidence: `UserContact` فقط OwnerUserId/ContactUserId/SavedName دارد؛ `MessagingDirectoryService` CRUD فقط حساب ثبت‌شده را می‌پذیرد. مدل Phone/Entry/Consent/Binding/Sync وجود ندارد.
- Status: `MISSING`.
- Gap / Required Change: دفترچهٔ ابری خصوصی، شمارهٔ معتبر کاننیکال، پیوند حساب مجاز جدا، نسخه/cursor/رسید per-item و tombstone بدون fake UserId؛ API قبلی حفظ شود.
- Tests Needed: owner/IDOR، چنددستگاهی، consent revoke، replay/conflict، partial batch و stale-device resurrection؛ عدم نشت نام/شماره در log.
- Migration Needed: بله، افزودنی؛ مخاطب قبلی حذف یا خودکار ادغام نشود.
- Product Decision Needed: مدت retention و رفتار حذف مخاطب از دفترچهٔ اصلی گوشی باز است؛ TTL یا purge خودکار حدس زده نمی‌شود.

## BE-15 — Storage / Library

- Requirement: FileAsset، readiness، مجوز منبع و دامنهٔ مستقل مدارک.
- Current Implementation / Evidence: `FileAsset`، `StorageService`، `FileAccessPolicy`، `LocalFileStorage` و `EducationalSourceService`؛ File/Library/Knowledge API و تست‌ها موجودند.
- Status: `PARTIAL`.
- Gap / Required Change: binding/ACL اختصاصی مدارک School Application و Support روی همین Storage؛ سیستم فایل موازی ساخته نشود.
- Tests Needed: owner/reviewer، وضعیت asset، revoke و حفاظت از مدرک در read/download مستقیم.
- Migration Needed: بله، در bindingهای ماژول جدید؛ schema فایل موجود بی‌دلیل تغییر نکند.
- Product Decision Needed: مجوز تجاری دانلود Library عادی طبق تصمیم موجود بسته می‌ماند؛ تصمیم تازه‌ای از v6 استنتاج نمی‌شود.

## BE-16 — Notifications

- Requirement: Invitation/Membership/Assignment/Exam/Payment/SchoolApplication/Support و Deep Link امن.
- Current Implementation / Evidence: `NotificationType` فعلاً هفت مقدار Academic/Message دارد؛ `NotificationService`، Preferences، Device/Delivery و Outbox موجودند.
- Status: `PARTIAL`.
- Gap / Required Change: رویدادهای دامنهٔ قطعیِ فاقد اعلان و target داخلی typed روی Outbox موجود؛ re-authorization و dedup حفظ شوند.
- Tests Needed: replay، revoke هنگام delivery/read، privacy و false feature gate PersonalClass.
- Migration Needed: در صورت نیاز CHECK enum/metadata موجود، افزودنی.
- Product Decision Needed: Push واقعی به credential/device وابسته است و runtime آماده فرض نمی‌شود؛ اعلان قابلیت پیشنهادی فعال نشود.

## BE-17 — Commerce Catalog

- Requirement: سه خانوادهٔ خدمت، PlanVersion/Price/Entitlement/Quota و پلن مشترک با هویت جدا.
- Current Implementation / Evidence: `Plan`، `PlanVersion`، `Subscription`، `Order`، `UsageReservation` و `CommercialSubscriptionService`؛ قیمت/سهمیه server-owned و قرارداد Legacy جداست.
- Status: `PARTIAL`.
- Gap / Required Change: داده/پیوند محصول خدمت تأسیس به پرونده و Quote؛ قیمت، مدت یا quota تازه seed نشوند.
- Tests Needed: frozen price/version، خرید بدون role grant، Quote پس از Approval و عدم اشتراک دادهٔ خصوصی خانواده.
- Migration Needed: برای پیوند تجاری School Application حسب مدل نهایی بله.
- Product Decision Needed: چندفرزندی، pooling/splitting، تعداد والد و اثر پایان رابطه بازند؛ shared quota با عدد حدسی ساخته نشود.

## BE-18 — Payments

- Requirement: Zibal وب و اعتبارسنجی server-side فروشگاه با همان Order/Attempt/Payment/Fulfillment.
- Current Implementation / Evidence: `ZibalPaymentGateway`، `OrderPaymentService`، `PaymentGatewayResolver`، `PaymentAttemptEvent` و `ZibalPaymentEndpointsTests`؛ مسیر Store Billing وجود ندارد.
- Status: `PARTIAL`.
- Gap / Required Change: Zibal حفظ/Regression؛ CafeBazaar/Myket فقط پس از بررسی مستند رسمی و بدون موفقیت Client-only؛ محیط/محصول/package/receipt کنترل شوند.
- Tests Needed: Zibal/201/Inquiry/Outbox و proof/mismatch/replay/restore فروشگاه در صورت اجرای adapter.
- Migration Needed: فقط برای metadata/رسید pinned فروشگاه در صورت نیاز.
- Product Decision Needed: iOS باز، Refund خارج scope؛ runtime فروشگاه به تنظیم معتبر وابسته است و حدس زده نمی‌شود.

## BE-19 — AI Gateway / Assistant Context

- Requirement: context داده صریح مستقل از SchoolViewContext و payer، با Source ACL و quota جاری.
- Current Implementation / Evidence: `AiRoleContextBuilders` از `IAccessContextResolver` فعلی Session استفاده می‌کند؛ `AiRoleContextService` revalidate دارد و `AiInvocationExecutor`/SourceSelection/Knowledge مجوز/مصرف را کنترل می‌کنند.
- Status: `PARTIAL`.
- Gap / Required Change: انتخاب صریح/ثبت‌شدهٔ Assistant و revalidation همان context بدون تغییر workspace؛ نقش مدرسه منبع را بی‌صدا عوض نکند.
- Tests Needed: role switch، revoke منبع/عضویت، payer مستقل و retry مصرف؛ transport کنترل‌شده با عدم ادعای AI واقعی.
- Migration Needed: در صورت ذخیرهٔ Assistant context، افزودنی.
- Product Decision Needed: PersonalClass AI تأیید صریح ندارد؛ اضافه/فعال نشود. `UseServiceAuthentication=false` هر دو سرویس قطعی است و تغییر نمی‌کند.

## BE-20 — Platform Admin

- Requirement: IAM/Audit و مدیریت least-privilege ماژول‌های جدید.
- Current Implementation / Evidence: `PlatformCatalog` و Controllerهای IAM/Users/Organizations/Provisioning/Education/Commerce/Finance/Moderation/Library/Operations وجود دارند؛ PlatformOwner با School Manager یکی نیست.
- Status: `PARTIAL`.
- Gap / Required Change: permission و صف School Application/Support؛ role حدسی PlatformManager ساخته نشود.
- Tests Needed: actor عادی/Manager، staff نامرتبط، Permission revoke، concurrency و audit.
- Migration Needed: seed permissionهای لازم و جداول ماژول جدید؛ least privilege.
- Product Decision Needed: دسترسی جدید خارج scope پرونده از Support/Review استنتاج نشود.

## BE-21 — Moderation / Support

- Requirement: رسیدگی موردی و Support Ticket مستقل؛ بدون مرور DM/Address Book.
- Current Implementation / Evidence: `ModerationCase`، `AbuseReport` و `PlatformModerationController` و Safety tests موجودند؛ SupportTicket یا صف عمومی پشتیبانی وجود ندارد.
- Status: `PARTIAL`.
- Gap / Required Change: Ticket/Message/Assignment/Attachment با owner/permission و audit؛ Moderation بازنویسی نشود.
- Tests Needed: مالک، Support مجاز، غیرمرتبط، file ACL، duplicate reply و تغییر assignment هم‌زمان.
- Migration Needed: بله؛ افزودنی.
- Product Decision Needed: reopen یا رفتار نامصوب وضعیت‌ها خودکار اضافه نشود؛ سیاست سن جاری حفظ شود.

## BE-22 — Security / Privacy / Deletion

- Requirement: current resource ACL، audit، consent و حذف با retention صریح.
- Current Implementation / Evidence: session/permission/rowversion/audit/outbox و تست‌های `PlatformAuthorization*` موجودند؛ Account Deletion lifecycle وجود ندارد.
- Status: `PARTIAL`.
- Gap / Required Change: foundation درخواست/وضعیت/ممیزی حذف و قرارداد revoke؛ هیچ حذف فیزیکی/ناشناس‌سازی حدسی یا نگهداری مالی حدسی اجرا نشود.
- Tests Needed: IDOR، revoke، correlation/no leak، retry/نسخه و حفاظت دادهٔ مالی/مدرسه/دفترچه.
- Migration Needed: برای درخواست و state، افزودنی.
- Product Decision Needed: `PRODUCT_LEGAL_DECISION_REQUIRED` برای دوره‌های جداگانهٔ Identity/Contacts/Messaging/Education/Documents/Payments/Audit/AI؛ اجرای حذف وابسته به این تصمیم است.

## BE-23 — Operations

- Requirement: SQL migration سازگار، backup/restore، worker recovery و کانفیگ جدا.
- Current Implementation / Evidence: `tools/production`، health/worker/outbox/metrics، Runbookهای `docs/production` و Storage isolation baseline.
- Status: `IMPLEMENTED`.
- Gap / Required Change: Migration جدید فقط روی DB disposable تست شود؛ schema عادی و سرور تغییر نکنند.
- Tests Needed: Migration acceptance و رگرسیون worker/No Leak متناسب تغییرات.
- Migration Needed: Migrationهای همین Task؛ نه اجرای عادی.
- Product Decision Needed: SLO و سلامت واقعی سرور بدون اجرا PASS نمی‌شوند.

## BE-24 — Full Acceptance

- Requirement: پذیرش جدید و full regression واقعی با حداقل baseline 3092.
- Current Implementation / Evidence: سه پروژهٔ xUnit و baseline تأییدشده وجود دارند؛ قابلیت‌های تازه هنوز در این Task اجرا نشده‌اند.
- Status: `PARTIAL`.
- Gap / Required Change: تست‌های جدید + final Release build/solution regression، Snapshot نهایی و Handoffهای کاربر/Admin.
- Tests Needed: تمام suiteها، School Application، Address Book، context switch و proposed gate.
- Migration Needed: تست تمام Migrationهای ساخته‌شده.
- Product Decision Needed: Server Deploy/AI runtime/Push بدون اجرای واقعی گزارش نمی‌شوند؛ این Task Deploy ندارد.

## BE-25 — Proposed PersonalClass Academics

- Requirement: `extra.assignments` و `extra.exams` پیش‌فرض false؛ School همچنان فعال؛ PRD-40/41/73.
- Current Implementation / Evidence: `PersonalClassAcademicsController` و engine مشترک Assignment/Exam قابلیت PersonalClass دارند؛ `PersonalClassAcademicGrant` مجوز داده‌ای دارد، اما Feature Gate قطعیِ محصول وجود ندارد.
- Status: `PROPOSED_FEATURE_MUST_BE_DISABLED`.
- Gap / Required Change: اولین تغییر Runtime، Gate واقعی مرکزی با پیش‌فرض false؛ مسیرهای generic resource-id، effective permission و اعلان/کارهای وابسته نیز نباید آن را دور بزنند. engine حذف نشود.
- Tests Needed: Personal assignment/exam disabled و School assignment/exam سالم؛ stale resource، granted role و generic path bypass؛ opt-in engine regression.
- Migration Needed: برای خود Gate خیر.
- Product Decision Needed: فعال‌سازی نهایی و دامنهٔ سایر آموزش‌های PersonalClass؛ در این Task خودکار فعال نمی‌شوند.

## ارزیابی نهایی Source و بنیادهای قابل‌تحویل

ردیف‌های بالا وضعیت **قبل از تغییر** را نگه می‌دارند؛ جدول زیر نتیجهٔ تغییرات Commit کد `e440c0143abee7cc40fb18223922cd3c06e22b26` است. وضعیت Product با وجود Endpoint یکی نیست. دو دور کامل سریالی هرکدام۳۱۵۷تست، صفر شکست/Skip و Buildهای بدون Warning/Error پاس شدند؛ نتیجهٔ [گزارش پیاده‌سازی](BACKEND_V6_IMPLEMENTATION_REPORT_FA.md) و گزارش نهایی Git مرجع‌اند. مرجع محدودیت و تصمیم‌های باز: [دفتر تصمیم‌ها](BACKEND_V6_OPEN_DECISIONS_FA.md).

| Task | وضعیت | شاهد و حد تحویل |
|---|---|---|
| BE-01 Contract audit | `PASS` | Snapshot تازه: ۴۲۷ مسیر، ۴۸۳ operation، ۷۷۲ schema، ۹۱ Controller؛ قرارداد Source با target محصول تفکیک شد. |
| BE-02 Identity/Profile | `PASS` | Bio اختیاری و phone-change با OTP واقعی، یکتایی/رقابت، حفظ UserId/نشست جاری و revoke سایر Refreshها؛ Mock نمی‌تواند Verify تغییر شماره را دور بزند. |
| BE-03 School Application | `PARTIAL` / `BLOCKED_PRODUCT_DECISION` | Draft/نسخه/مدرک خصوصی/Review/Audit آماده؛ D01 منتظر فهرست الزامی مالک، Submit۴۰۹؛ اتصال پرونده به Quote/Payment/Provisioning ساخته نشده است. |
| BE-04 Organization/Guardian | `PASS` در قرارداد مصوب جاری | flow موجود، جداسازی Person/Account و رضایت حساب محفوظ؛ Guardian مجوز Direct خصوصی نمی‌سازد. گسترش target فضای رسمی در D14 باز است. |
| BE-05 Academic core | `PASS` | مدل و مسیرهای سازمانی قبلی حفظ شدند؛ Gate صرفاً PersonalClass است. |
| BE-06 Assignments | `PASS` برای سازمان | نسخه/Submission/Attachment/Evaluation/Release موجود reuse؛ قابلیت پیشنهادی مستقل جدا و خاموش. |
| BE-07 Exams | `PASS` برای سازمان | freeze/deadline/lease/finalize/grade/release موجود حفظ؛ worker/Realtime از Gate Personal عبور نمی‌کنند. |
| BE-08 Reports | `PARTIAL` | گزارش/export سازمانی موجود؛ گزارش آموزشی مستقل target در D08 باز است و آمار ساختگی اضافه نشد. |
| BE-09 PersonalClass lifecycle | `PARTIAL` | چرخهٔ درخواست/تأیید و archive موجود حفظ؛ restore/reopen در D09 باز است. |
| BE-10 PersonalClass membership | `PASS` | نقش محلی/ظرفیت/دعوت/Join/مدیر آخر موجود؛ خرید دانشجو یا نقش سازمانی ساختگی اضافه نشد. |
| BE-11 Official spaces | `PARTIAL` | provision/repair و current-source ACL قبلی محفوظ؛ گسترش Parent در target تصمیم D14 می‌خواهد. |
| BE-12 Account messaging | `PASS` | پذیرش چهار Role با Conversation/Contact/AddressBook/PersonalClass ثابت، retry بدون اثر تکراری؛ پیاده‌سازی موجود بازنویسی نشد. |
| BE-13 Group/Channel | `PASS` در قرارداد مصوب جاری | امکانات فعلی P2 و دعوت اولیهٔ هفت‌روزه reuse؛ سیاست پیشنهادی stranger در D13 به‌صورت پنهان فعال نشد. |
| BE-14 Address Book v2 | `PARTIAL` در چرخهٔ کامل محصول | cloud sync/consent/multi-phone/cursor/tombstone/reference خصوصی پیاده؛ TTL/OS-delete در D03 حدس زده نشده و keyring عملیاتی باید پذیرفته شود. بنیاد Cloud Sync قابل‌تست است. |
| BE-15 Files/Library | `PARTIAL` | همان Storage با binding/ACL Support و مدارک؛ Library download عادی و media ReviewRequired با محدودیت مصوب محفوظ‌اند. |
| BE-16 Notifications | `PARTIAL` | پنج نوع جدید، target تایپ‌شده، dedup و reauthorization روی Outbox قبلی؛ همهٔ دعوت‌های شخصی به اعلان جدید وصل نشده و Push Runtime هنوز pending است. |
| BE-17 Commerce catalog | `PARTIAL` | کاتالوگ/قرارداد Legacy موجود حفظ؛ Shared multi-child quota و پیوند تجاری پروندهٔ آموزشگاه حدس زده نشدند. |
| BE-18 Payments | `PARTIAL` | Zibal موجود در Regression؛ CafeBazaar/Myket تا Contract رسمی server-verification، mapping و تنظیم امن `BLOCKED_CONFIGURATION`؛ iOS تصمیم باز و Refund خارج scope است. |
| BE-19 AI gateway | `PARTIAL` | Assistant context مستقل، current ACL، class/source pin و payer جدا پیاده؛ Generic Personal به Upstream سازگار وابسته؛ Service Auth هر دو سرویس خاموش، PersonalClass AI اضافه نشده. |
| BE-20 Platform Admin | `PASS` برای بنیادهای جدید | پنج permission و صف/عملیات پروندهٔ مجاز؛ PlatformOwner/Support موجود reuse، School Manager privilege تازه نمی‌گیرد. |
| BE-21 Moderation/Support | `PARTIAL` در چرخهٔ کامل محصول | Moderation/Safety قبلی محفوظ؛ Create/Read/Reply/Assign/Resolve/File ACL Support پیاده؛ reopen نهایی و تأیید media اختراع نشدند. |
| BE-22 Security/Deletion | `PARTIAL` / `BLOCKED_PRODUCT_DECISION` | ACL/نسخه/audit/consent و درخواست/صف حذف موجود؛ execution/retention در D04 قانونی باز، حذف/تاریخ حدسی نداریم. |
| BE-23 Operations | `PARTIAL` در پذیرش عملیاتی | Migrationهای افزودنی و config/Storage جدا محفوظ؛ Backup/ring/Push/سرور در این Task اجرا یا پذیرش نشده‌اند. |
| BE-24 Full acceptance | `PARTIAL` / `BLOCKED_PRODUCT_DECISION` | Full Regression محلی هر دو دور۳۱۵۷/۳۱۵۷ PASS است؛ پذیرش کل هدف Product، خصوصاً School end-to-end در سناریوی۹۱، به D01 و اتصال تجاری بعدی وابسته است و PASS اعلام نمی‌شود. |
| BE-25 Proposed academics | `DEFERRED_APPROVED` | Assignment/Exam پیش‌فرض false؛ Gate مرکزی برای generic path، capabilities، worker، Realtime و Notification؛ engine و تاریخچه باقی‌اند. |

هیچ ردیف PASS در این جدول ادعای Deploy، SMS/AI/Push واقعی، خرید Store یا آمادگی تمام Product v6 نیست. Test و Production private config و سه فایل AI قبلی حفظ شده‌اند؛ شش Migration جدید روی دیتابیس عادی یا سرور اجرا نشده است.

## به‌روزرسانی آموزشگاه — 2026-11-13

مرجع: پرامپت مصوب School Establishment با مبنای `1fd122c2bfa7930dbbf0a1325a3c360a2fa451a5`. [گزارش فنی و یادگیری](BACKEND_V6_IMPLEMENTATION_REPORT_FA.md#تکمیل-درخواست-آموزشگاه--2026-11-13)، [قرارداد متقاضی](SCHOOL_APPLICATION_FRONTEND_HANDOFF_FA.md) و [قرارداد Admin](SCHOOL_APPLICATION_ADMIN_HANDOFF_FA.md) جزئیات جاری را دارند. D01 بسته شده و Submit دیگر به نبود minimum requirements مسدود نیست.

| بخش | وضعیت واقعی | شاهد/محدودیت |
|---|---|---|
| Domain / LegalOrganization / IndividualTeacher | `PASS` در پذیرش متمرکز | دو فرم، دو ظرفیت نامنفی مستقل، مدارک مصوب، Submit/Cancel/NeedsChanges/resubmit و readonly پس از تصمیم نهایی |
| Documents / ACL / History | `PASS` در پذیرش متمرکز | همان FileAsset، binding retained، owner/reviewer جاری، منع cross-user و مخفی‌بودن Draft لغوشدهٔ ارسال‌نشده؛ history واقعی بدون backfill |
| Idempotency / Version / Review | `PASS` در پذیرش متمرکز | receipt/fingerprint، ExpectedVersion، قفل SQL، دلیل اجباری؛ رقابت تصمیم فقط یک برنده دارد |
| Commerce boundary / Order / Zibal | `PASS` در تست با داده/درگاه کنترل‌شده؛ تنظیم واقعی `BLOCKED_CONFIGURATION` | نگاشت دارای permission به Published PlanVersion، جداسازی خرید عادی، قیمت/نسخه frozen، correlation پرونده و Verify سرور |
| Durable provisioning intent / Recovery | `PASS` تا مرز تصمیم | Outbox موجود → PlatformProvisioningRequest موجود؛ failure/retry پرداخت را تغییر یا دو intent نمی‌سازد |
| Organization / Subscription / Initial Manager تکمیل این درخواست | `BLOCKED_PRODUCT_DECISION` | سیاست مدیر اولیه D17 باز؛ متقاضی مدیر فرض نشده و intent عمومی قابل resume نیست |
| BE03_SCHOOL_APPLICATION | `PARTIAL` در هدف کامل تحویل خدمت | تمام بخش‌های فنی قابل اجرا تا Paid/intent انجام شده؛ قیمت واقعی و تصمیم مدیر اولیه لازم‌اند |
| BE24_FULL_ACCEPTANCE | `FAIL` در هدف کامل Product v6 | موفقیت تست محلی یا همین پرونده، تصمیم‌های باز/پذیرش عملیاتی سایر حوزه‌ها را نمی‌بندد |

Migration `20261113065124_CompleteSchoolEstablishmentWorkflow` افزودنی است و فقط روی disposable SQL آزموده شده است. جدول تجاری، پروندهٔ تاریخی یا مدیر ساختگی Seed نشده؛ `MakanApp`، `MakanApp_Test` و سرور تغییر نکرده‌اند. feature gateهای BE25 و Service Auth هر دو سرویس AI خاموش و تنظیمات/SMS/AI قبلی محفوظ‌اند. Commit کد `b7ac095c3f5c0175254edadd6d5ef35f7b24215d`؛ منبع آخر۱۲۹۸ Unit +۱۸۸۴ Integration +۱۴ Architecture =۳۱۹۶ PASS، صفر Failed/Skipped و صفر Build Warning/Error دارد.۱۱۴ تست Zibal و Fresh/Upgrade/replay SQL در regression پاس شدند. این موفقیت محلی، دو blocker تنظیم قیمت/مدیر و BE24 را به PASS تبدیل نمی‌کند.

## نهایی‌سازی انتخاب مدیر و آماده‌سازی

مبنا `d9f493858b1ae3d1badceb6818d56aa1e34bb1cd` و پرامپت School Application Finalization در `2026-11-13` است؛ جدول قبلی وضعیت تحویل قبلی را نشان می‌دهد. [گزارش آموزشی](BACKEND_V6_IMPLEMENTATION_REPORT_FA.md)، [تحویل Admin](SCHOOL_APPLICATION_ADMIN_HANDOFF_FA.md) و [راهنمای تنظیم تجاری](SCHOOL_COMMERCIAL_CONFIGURATION_FA.md) مرجع جزئیات جاری‌اند.

| بخش | وضعیت پیاده‌سازی و شاهد | محدودیت محیط/محصول |
|---|---|---|
| انتخاب مدیر | `PASS` در آزمون متمرکز | متقاضی فقط proposed؛ Approval نیازمند انتخاب صریح حساب فعالِ دارای پروفایل کامل؛ مدیر جایگزین، history/audit و رقابت دو Reviewer آزموده‌اند |
| Commercial mapping / Quote / Order | `PASS` در آزمون متمرکز برای هر دو نوع | Published PlanVersion و قیمت سرور؛ قیمت/Quota/دورهٔ واقعی Production هنوز `BLOCKED_CONFIGURATION` و Seed حدسی ممنوع |
| Zibal / Payment / Outbox | `PASS` در آزمون کنترل‌شده | callback معادل Paid نیست؛ Server Verify و Payment موجود؛ تست۱۰ callback تکراری و۱۰ Verify بدون Payment/intent دوم |
| Provisioning سازمان/اشتراک/دعوت | `PASS` در آزمون متمرکز | همان PlatformProvisioningRequest و stageها، همان Outbox؛ اشتراک معتبر ماهانه، SourcePaymentId و دعوت با expiry سرور؛ Membership فقط پس از Accept |
| Recovery / Concurrency | `PASS` در آزمون متمرکز | شکست بعد از ایجاد اشتراک، intent قدیمی Paid و intent هنوز ساخته‌نشده، دو Worker، شناسه‌های ثابت، Receipt/list صحیح و بدون پرداخت مجدد؛ Worker School درخواست عمومی Platform را اجرا نمی‌کند |
| Migration | `20261113103326_FinalizeSchoolManagerAndPaidProvisioning` | nullable و بدون backfill مدیر/دادهٔ تجاری؛ unique/FK و Fresh/Upgrade/replay فقط SQL disposable؛ اجرای عادی/سرور `NOT_RUN` |
| BE03_SCHOOL_APPLICATION_IMPLEMENTATION | `PASS` | Commit Runtime=`1804f402be32e4d776216c17fcd76555fc5cf29f`، E2E کنترل‌شده و Regression کامل پاس |
| BE03_SCHOOL_APPLICATION | `PASS` در کد/پذیرش محلی | آمادگی کد/تست از تنظیم واقعی محیط جداست؛ Production هنوز نیازمند قرارداد مصوب است |
| BE24_FULL_ACCEPTANCE | `FAIL` | حذف واقعی/retention، shared multi-child quota، Store billing، گزارش/restore مستقل، گسترش Parent، Generic Personal AI و پذیرش عملیاتی Provider/سرور هنوز جدا یا محدودند |

Final Release Build با۰ Warning/۰ Error و Restore موفق بود؛ Final Tests=۱۳۰۸ Unit +۱۸۹۷ Integration +۱۴ Architecture =۳۲۱۹ Passed، Failed/Skipped=0؛۱۱۴ تست Zibal و دو تست School Migration در مجموعهٔ نهایی پاس شدند. Snapshot جدید۴۳۶ path/۴۹۲ operation/۷۸۰ schema،۱۴۹ جدول و۷۳ Migration دارد؛ OpenAPI SHA256=`ba5e08f0108fe8593e72b0b3c0874a18c7170a10166bd39a32eb656edcd57912`. `PRODUCTION_COMMERCIAL_CONFIGURATION=BLOCKED_CONFIGURATION`، `BE24_FULL_ACCEPTANCE=FAIL`؛ هیچ نتیجهٔ محلی، Deploy یا پرداخت واقعی ادعا نمی‌کند.

D17 بسته شد و۱۲ تصمیم Product/Legal باز باقی‌اند. تکمیل انتخاب خالیِ Legacy، تعویض مدیر نهایی نیست؛ `POST_APPROVAL_MANAGER_CHANGE=DEFERRED` حفظ می‌شود. AI/Knowledge Service Auth و PersonalClass Assignment/Exam پیش‌فرض خاموش، MeliPayamak و private configها دست‌نخورده‌اند. Merge main و Deploy اجرا نمی‌شوند.
