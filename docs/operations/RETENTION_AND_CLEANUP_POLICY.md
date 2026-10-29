# سیاست نگه‌داری و پاک‌سازی محدود

بازبینی `2026-10-29` از `main@701cba0`: موجودی اولیه جدول‌های زیر تاریخچهٔ PH-6 است؛ فرهنگ جاری [SQL](../database-current-structure.md) شامل حذف دو جدول MFA و اضافه‌شدن ClassLessons/ClassLessonAttachments است و جمع فعلی۱۰۷ جدول/۵۲ migration دارد. DOB و مرجع Photo ستون‌های افزوده‌اند. cleanup همچنان فقط bytes terminal بدون reference است؛ User.ProfilePhotoFileAssetId و ClassLessonAttachment نیز guard حذف‌اند. Ready-orphan reaper و purge تاریخچه SQL اضافه نشده‌اند؛ برای جدول/ستون جدید retention حقوقی پیش‌فرض اختراع نشده است.

این موجودی از `MakanDbContextModelSnapshot` و ۱۰۷ جدول فعلی تهیه شده است؛ تعداد جدول، دورهٔ
نگه‌داری حقوقی یا مجوز حذف ایجاد نمی‌کند. هیچ تاریخ انقضا یا ستون `Processed/Expired` به‌تنهایی
مجوز purge نیست. PH-6 دادهٔ مالی، تحصیلی، امنیتی، moderation یا تاریخچهٔ Messenger را حذف نمی‌کند.

## طبقه‌بندی

| علامت | وضعیت | معنا |
|---|---|---|
| D | `DO_NOT_AUTO_DELETE` | خارج از cleanup عمومی؛ حذف فقط با workflow مستقلِ صریح و بررسی وابستگی‌ها |
| L | `LEGAL_POLICY_REQUIRED` | شواهد/امنیت/حریم خصوصی؛ تا سیاست مصوب حذف نشود |
| P | `PRODUCT_DECISION_REQUIRED` | دوره و semantics بازیابی/dedup/تاریخچه روشن نشده؛ حذف غیرفعال |
| C | `CONFIGURABLE_SAFE_TRANSIENT_DATA` | فقط دادهٔ فنی قطعاً بلااستفاده با guard و config صریح |
| A | `APPROVED_EXISTING_RETENTION` | TTL یا cleanup فنی موجود، نه سیاست حقوقی تازه |

## موجودی کامل جدول‌های SQL

نام‌ها عین mapping فعلی‌اند. دستهٔ یک جدول را به همهٔ فایل‌ها یا رابطه‌های آن تعمیم ندهید.

| Schema؛ تعداد | دسته | Tableها |
|---|---|---|
| academic؛ 9 | D | AcademicPeriods، Attendance، AttendanceRevisions، Classes، Courses، Enrollments، ScheduleRules، Sessions، TeacherAssignments |
| assessment؛ 18 | D | AnswerRevisions، AssignmentRecipients، Assignments، AssignmentVersions، EvaluationRevisions، ExamAttemptQuestions، ExamAttempts، ExamFinalAnswers، ExamGradeReleases، ExamGradeRevisions، ExamQuestionGrades، Exams، ExamVersions، GradeReleases، QuestionOptions، QuestionVersions، SubmissionAttachments، SubmissionAttempts |
| commerce؛ 10 | D | Orders، PaymentAttempts، Payments، PlanEntitlements، PlanQuotaDefinitions، Plans، PlanVersionPrices، PlanVersions، Subscriptions، UsageLedger |
| guardian؛ 1 | D | GuardianRelations |
| identity؛ 3 | D | Persons، UserCredentials، Users |
| identity؛ 1 | L | CommunicationAgeVerifications |
| identity؛ 3 | P | OtpChallenges، RefreshTokens، UserSessions |
| messaging؛ 32 | D | ChatFolders، ConversationChecklistItems، ConversationChecklistPersonalStates، ConversationChecklists، ConversationInvitations، ConversationInviteLinks، ConversationJoinRequests، ConversationMemberRestrictions، ConversationOwnershipTransfers، ConversationParticipants، ConversationPins، ConversationPollOptions، ConversationPolls، ConversationPollSelections، ConversationPollVotes، ConversationQuizAnswers، Conversations، ConversationTopics، ConversationUserStates، FolderConversationOverrides، MessageAttachments، MessageMentions، MessageReactions، MessageRevisions، Messages، MessageUserVisibilities، MessagingPrivacySettings، PersonalCommunicationGrants، SavedMessageLabels، SavedMessageOrganizations، UserBlocks، UserContacts |
| messaging؛ 5 | L | AbuseReports، ModerationActions، ModerationCases، ModerationEvidenceAttachments، SafetyIncidents |
| messaging؛ 5 | P | ChangeEvents، RealtimeOutbox، ScheduledMessages، ScheduledMessageAttachments، ScheduledMessageMentions |
| notification؛ 1 | D | NotificationPreferences |
| notification؛ 3 | P | NotificationDeliveryAttempts، Notifications، PushDeviceRegistrations |
| organization؛ 5 | D | Invitations، Memberships، OrganizationPersons، Organizations، RoleAssignments |
| platform؛ 6 | D | PlatformMfaCredentials، PlatformMfaRecoveryCodes، PlatformPermissions، PlatformRoleAssignments، PlatformRolePermissions، PlatformRoles |
| platform؛ 1 | L | AdminAuditLogs |
| platform؛ 2 | P | OutboxMessages، ProvisioningRequests |
| reports؛ 1 | P | ReportExportJobs |
| storage؛ 1 | D | FileAssets؛ metadata باقی می‌ماند، فقط bytes terminal واجد شرایط در بخش زیر C است |

جمع: **86 D + 7 L + 14 P = 107**. جدول فنی `__EFMigrationsHistory` نیز D است و خارج این شمارش مدل قرار دارد.
هیچ job عمومی `DELETE FROM` برای این فهرست ساخته نشده است.

### چرا موارد ظاهراً منقضی هنوز پاک نمی‌شوند؟

- OTP بخشی از محدودیت درخواست/تلاش است؛ حذف شتاب‌زده می‌تواند سابقهٔ throttle را از بین ببرد.
- refresh token مصرف‌شده برای تشخیص reuse/family revocation است؛ self-FK و session/MFA/device
  وابستگی دارند. پایان زمان اعتبار، مجوز حذف زنجیرهٔ امنیتی نیست.
- Outbox و delivery، کلید dedup و failure evidence دارند؛ حذف processed یا failed بدون قرارداد
  retry می‌تواند اثر کسب‌وکاری را دوباره بسازد یا امکان عیب‌یابی را بگیرد.
- ChangeEvent و Scheduled history به Delta، cursor، receipt و idempotency متصل‌اند؛ cancel/dispatch
  به معنی حذف تاریخی attachment یا intent نیست.
- export منقضی و فایل Ready هنوز metadata/مجوز/receipt دارند؛ انقضای download به معنی حذف bytes نیست.
- دادهٔ moderation و audit append-only با ابزار cleanup فنی ادغام نمی‌شود؛ سیاست حقوقی این مرحله اختراع نشده است.

## دادهٔ خارج SQL و مالک عملیاتی

| داده | دسته | رفتار/مالک |
|---|---|---|
| Typing/Presence/LastSeen حافظه‌ای | A | TTL و سقف حافظهٔ موجود؛ تاریخچهٔ SQL یا API purge جدید ندارد |
| stream موقت CSV با `DeleteOnClose` | A | lifecycle تولید موجود؛ crash/orphan عمومی را اثبات نمی‌کند |
| bytes فایل Deleted/Rejected بدون هیچ reference و بدون retention | C | تنها نامزد مکانیزم محدود PH-6؛ config پیش‌فرض خاموش و grace صریح لازم است |
| Ready/Pending، attachment یا فایل retained | D/P | در PH-6 حذف نمی‌شود؛ `UnattachedExpiresAtUtc` سیاست purge مصوب نیست |
| فایل `.uploading` فعال | D | امکان writer فعال؛ sweep عمومی ممنوع |
| `.uploading` رهاشده و candidate یتیم export | C برای مکانیزم آینده | این نسخه inventory/قفل انحصاری اثبات‌شده برای آن‌ها ندارد؛ **پاک نمی‌شوند** |
| key ring XML، PFX/private key و backup secret | D | custody و بازیابی مستقل؛ cleanup Storage نباید آن‌ها را ببیند |
| DB/storage backup و snapshot | L/P | DBA/مالک recovery؛ دوره، off-server copy و RPO/RTO نیازمند تصمیم مستقل |
| IIS/Event Log/collector | سیاست عملیاتی | rotation، سقف حجم و دسترسی توسط همان زیرساخت؛ برنامه file-log-deleter موازی نمی‌سازد |

artifact پایدار rate limiter در SQL وجود ندارد؛ limiter فعلی process-local است. نبود جدول
مستقل Push به معنی نبود رشد نیست: تلاش‌های Push در `NotificationDeliveryAttempts` هستند.

## مرز مکانیزم مجاز PH-6

مکانیزم فقط **بایت فیزیکی محلی** فایل `Deleted/Rejected` را پس از grace صریح بررسی می‌کند؛
هیچ ردیف، وضعیت، rowversion یا کلید SQL را تغییر نمی‌دهد. فایل Ready/Pending، retained،
رکورد متصل به هر Submission/Message/Moderation/Scheduled attachment و هر ReportExportJob
(هم `Id == FileAsset.Id` و هم `OutputFileAssetId`) در هر status مستثناست.

دسترسی فیزیکی و metadata تراکنش مشترک واقعی ندارند؛ missing بودن فایل terminal در retry
موفقیت idempotent حذف bytes است، نه موفقیت حذف metadata یا تحویل محصول. قفل SQL فقط رقابت
همان عملیات را fence می‌کند؛ FK به‌تنهایی مانع reference جدیدِ SQL دستی به asset terminal نیست.
workflowهای مجاز باید Ready/current-state را validate کنند؛ این کنترل با purge دور زده نمی‌شود.

حذف recursive، root sweep، UNC/network root و عبور از reparse point/junction/symlink مجاز نیست.
بررسی مسیر جای ACL قابل‌اعتماد را نمی‌گیرد؛ precheck قابل‌حمل .NET در برابر مهاجم OS که هم‌زمان
مسیر را جابه‌جا کند تضمین کامل TOCTOU ندارد. syscall محلیِ گیرکرده نیز با CancellationToken
به‌زور متوقف‌شده فرض نمی‌شود.

## قرارداد تنظیم و اجرا

کلیدهای جدید فقط `Storage:TerminalCleanup:*` هستند؛ معادل environment با `__` جدا می‌شود.
هیچ default جدیدی در JSON خصوصی سرور نوشته یا cleanup روی محیط اجرا فعال نشده است.

| تنظیم | پیش‌فرض/اعتبار | معنا |
|---|---|---|
| `Enabled` | false؛ bool معتبر | بدون opt-in، هیچ query یا حذف cleanup اجرا نمی‌شود |
| `GracePeriodHours` | بدون پیش‌فرض؛ در Enabled الزامی و مثبت | فاصله از timestamp نهایی Deleted/Rejected؛ سقف فنی جلوگیری از overflow، نه پیشنهاد retention |
| `BatchSize` | 25؛ از 1 تا 100 | حداکثر candidate بررسی‌شده، نه وعدهٔ تعداد حذف موفق |
| `IntervalSeconds` | 60؛ از 5 تا 3600 | فاصلهٔ عملیاتی batch؛ دورهٔ نگه‌داری محسوب نمی‌شود |

ورودی نامعتبر با نام setting و پیام ثابت رد می‌شود؛ مقدار خام وارد exception نمی‌شود.
cutoff از UTC سمت سرور ساخته و برای grace بسیار بزرگ به MinValue محدود می‌شود؛ underflow
به حذف بیشتر تبدیل نمی‌شود. هر asset budget لغوشوندهٔ مستقل ۱۰s، همراه caller cancellation دارد؛
این budget syscall غیرهمکار OS را به‌زور متوقف نمی‌کند.

`TerminalFileCleanupCursor` در host singleton است و با scope پردازنده از نو شروع نمی‌شود؛
دو cursor مستقل برای Rejected/Deleted و نوبت‌دهی بین آن‌ها، backlog یک وضعیت را مانع دیگری
نمی‌کند. پیمایش keyset با `(CreatedAtUtc,Id)` داخل Status از index موجود
`IX_FileAssets_Status_CreatedAtUtc` و PK خوشه‌ای استفاده می‌کند؛ OFFSET و migration جدید ندارد.
cursor بعد از skipped/missing/failure نیز جلو می‌رود؛ پایان یا restart می‌تواند دوباره دادهٔ قدیمی
را بررسی کند، ولی metadata و bytes حذف‌شده اثر مضاعف کسب‌وکاری ندارند.

`TerminalFileCleanupProcessor` در transaction کوتاه Serializable، asset را XLOCK می‌گیرد و
status/timestamp/reference را مجدداً بررسی می‌کند. queryهای نبود reference نیز تا پایان همان
transaction معتبرند. تنها I/O مجاز زیر قفل، حذف کوچک bytes روی دیسک محلی ثابت است؛ اسکن،
شبکه، antivirus یا تولید گزارش در آن transaction اجرا نمی‌شود. FileAsset و rowversion دست‌نخورده‌اند.
شکست SQL بعد از حذف bytes ممکن است outcome را Failed نشان دهد؛ retry به AlreadyMissing می‌رسد،
نه بازسازی/حذف metadata. قفل جلوی workflow مجاز هم‌زمان را می‌گیرد، نه reference SQL دستی آینده.

`TerminalFileCleanupDispatcher` هشتمین BackgroundService است؛ با config خاموش فوراً پایان می‌یابد.
warningهای ثابت و metric `makan.file_cleanup.results` فقط outcomeهای محدود
`Examined/Deleted/AlreadyMissing/Skipped/Failed/Other` دارند؛ path، file ID و key label یا log نیستند.
`FileCleanup` در health محافظت‌شده Disabled/Enabled **تنظیمی** است؛ Enabled شاهد موفقیت حذف نیست.
دو process می‌توانند یک candidate را ببینند؛ قفل per-asset و حذف idempotent هستند، cursor توزیع‌شده
و تحویل exactly-once از آن نتیجه نمی‌شود.

## تصمیم‌ها و عملیات باقی‌مانده

مالک محصول/امنیت باید برای هر P/L دوره، legal hold، حذف حساب، حد replay/Delta، evidence، backup
و روش audit حذف را جدا تعیین کند. اپراتور پیش از فعال‌کردن cleanup محدود، inventory candidate،
backup/ACL و grace را تأیید و count/failure را پایش کند؛ مقدار زمانی نمونهٔ مصوبِ ساختگی در این سند وجود ندارد.
تا config صریح و پذیرش محیط، `PH6_CLEANUP=POLICY_PENDING` است؛ وجود کد مجوز روشن‌کردن سرور نیست.

راهنمای [مشاهده‌پذیری](../production/OBSERVABILITY.md)، [بازیابی Worker](WORKER_RELIABILITY_AND_RECOVERY.md)
و [Backup/Restore](../production/BACKUP_RESTORE.md) مرز عملیات و حفاظت شواهد را کامل می‌کنند.
