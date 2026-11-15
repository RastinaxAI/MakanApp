# ماتریس دسترسی و چرخهٔ فایل/منبع — BE-15

مبنا `54d07d2`؛ `2026-11-15`. تمام `GET /files/{id}` و `/content` به نشست/حساب معتبر نیاز دارند.
جدول مجوز بایت را بیان می‌کند؛ وجود FK یا FileId مجوز نیست. فایل bound از ACL مصرف‌کننده
خوانده می‌شود و اجازهٔ uploader جای آن را نمی‌گیرد. Download تنها برای File Ready است.

| Binding واقعی | Actor مجاز | Actor نامجاز/پس از revoke | مسیر و وضعیت | حذف عمومی |
|---|---|---|---|---|
| EducationalSourceVersion شخصی | OwnerUserId با SourceReady | حساب دیگر؛ Archive برای content | Source/raw، `200` مجاز، `404` raw نامجاز | `FILE_IN_USE` |
| Source سازمانی | Manager جاری همان سازمان | Teacher عمومی، سازمان دیگر، membership پایان‌یافته | Source/raw طبق School ACL | `FILE_IN_USE` |
| Source کلاس | Manager/Teacher منتسب؛ Student با Enrollment و نسخهٔ Published | Parent عمومی، دانشجوی Draft، Enrollment/Assignment پایان‌یافته | Source/raw؛ revoke=`404` | `FILE_IN_USE` |
| Library Source | مدیر دارای permission؛ عادی فقط metadata Public/Active/Published | School Manager بدون permission؛ Hidden/Draft/Archived برای عادی | عادی Source content=`403`، raw=`404`، metadata File=null | `FILE_IN_USE` |
| SchoolApplicationDocument | Applicant؛ Reviewer با permission جاری و پروندهٔ ارسال‌شده | حساب دیگر؛ Reviewer برای Draft خصوصی یا پس از revoke | raw طبق ACL جاری پرونده؛ نامجاز=`404` | `FILE_IN_USE` |
| SupportTicketAttachment | Owner تیکت؛ Staff با SupportRead جاری | کاربر دیگر؛ uploader کارشناس پس از revoke مگر Owner باشد | raw؛ نامجاز=`404` | `FILE_IN_USE` |
| AssignmentVersionAttachment | نویسنده/ارزیاب مجاز؛ Learner گیرندهٔ نسخهٔ منتشرشده | Draft برای Learner؛ کلاس/سازمان دیگر؛ enrollment پایان‌یافته | raw، AcademicContextResolver جاری | `FILE_IN_USE` |
| SubmissionAttachment | صاحب پاسخ با مشارکت جاری؛ ارزیاب فقط وقتی ContentVisible است | Peer، Parent، مدرس نامرتبط؛ Draft پنهان | raw از Submission ACL | `FILE_IN_USE` |
| AnswerRevisionAttachment | صاحب Attempt مجاز؛ ارزیاب مجاز پس از Finalize | Parent، Peer، مدرس نامرتبط؛ revision خارج از مجموعهٔ جاری/نهایی | raw؛ نگهداری تاریخچه مجوز تاریخچه نیست | `FILE_IN_USE` |
| MessageAttachment | participant جاری با history و message visibility | عضو حذف‌شده، پیام پنهان/حذف‌شده، scope دیگر | raw؛ مستقل از داشتن FileId | `FILE_IN_USE` |
| ModerationEvidenceAttachment | actor مجاز پرونده از مسیر تخصصی | uploader صرف، School Manager یا Guardian بدون مجوز پرونده | evidence-only raw=`404`؛ مسیر پرونده با مجوز و Audit جاری | `FILE_IN_USE` |
| ClassLessonAttachment | Manager/Teacher مجاز؛ Student مجاز برای درس Published | Draft، Parent، Enrollment/TeacherAssignment پایان‌یافته | raw؛ revoke=`404` | `FILE_IN_USE` |
| ProfilePhoto | صاحب حساب؛ مخاطب مجاز Avatar طبق privacy/تاریخچهٔ جاری | پس از Unlink برای دیگران؛ حساب/نشست نامعتبر | raw، scope شخصی؛ uploader فایل آزاد استثنای مستقل دارد | تا اتصال فعال `FILE_IN_USE` |
| ConversationPhoto | AccountInbox ACL و عضویت جاری | outsider، دعوت پذیرفته‌نشده، عضو حذف‌شده | raw؛ انتخاب Workspace مجوز نمی‌سازد | تا اتصال فعال `FILE_IN_USE` |
| ReportExportJob | requester با organization/membership/audience/subject جاری؛ Teacher Assignment جاری | actor یا context دیگر؛ شغل غیر Completed | raw و مسیر download گزارش | `FILE_IN_USE` حتی canonical JobId |
| ScheduledMessageAttachment | فرستندهٔ intent با participant و context جاری | گیرنده قبل از Dispatch، uploader پس از پایان عضویت | raw؛ مسیر Message پس از ارسال ACL مستقل دارد | `FILE_IN_USE` در تمام statusها |
| PersonalClass-bound upload | uploader با همان دورهٔ membership جاری؛ اگر bound، ACL آموزشی مصرف‌کننده | عضویت پایان‌یافته/دورهٔ جدید متفاوت، کلاس دیگر | raw؛ وابستگی PersonalClassId و MembershipId حفظ است | آزاد: مجاز با نسخه؛ bound: `FILE_IN_USE` |

## شواهد اجرایی و آزمون‌ها

| حوزه | تست‌های واقعی |
|---|---|
| Upload، hash، اندازه، Range، نام Unicode، session و حذف آزاد | FileStorageEndpointsTests و FileAcceptanceTests |
| binding بدون Retained marker برای پرونده | Be15PrivateCaseBindingWithoutRetentionStillPreventsDeletionAndSourcePromotion، دو حالت School/Support |
| Scheduled private/DELETE/source reuse/revoke | Be15ScheduledFileIsPrivateBoundAndCannotBeDeletedOrPromotedIntoSource |
| race واقعی SQL rowversion | Be15ScheduledBindingFencesAStaleFileDeletionWithSqlRowVersion؛ تست‌های موجود Source/Profile/Attachment نیز حفظ شدند |
| Archive و Range نامجاز | Be15SourceArchiveRevokesBothRoutesAndRangeWithoutDeletingHistoricalBytes |
| نسخه، quota، Source cross-user/org، Library Hidden | EducationalSourcesEndpointsTests، EducationalSourcesAuthorizationTests، EducationalSourcesSafetyTests |
| عدم laundering ایمنی | Be15PendingSafetyHoldCannotBeResetByAReadyFileOrVersionRetry؛ SafetyHoldCannotBeResetToPendingAndThenLaunderedIntoApproval |
| پاسخ Assignment/Exam و PersonalClass | AcademicAssignmentAttachmentAcceptanceTests، AcademicExamAttachmentTests/SecurityTests، PersonalClassAcademicAssignmentTests/ExamTests |
| Message و history | ReadyImageAndVoiceAreAuthorizedRetainedMediaWithoutStoragePathDisclosure؛ P2SearchRetainedAttachmentCannotBeDeletedOrAdvertisedWithoutAccess؛ DeleteForMeAlsoBlocksSharedMediaAndDirectFileAccessOnlyForCaller |
| پرونده و reviewer | SchoolEstablishmentWorkflowTests، ProductV6SchoolApplicationTests، ConversationReviewerDownloadsOnlyCaseAttachmentAndRevocationTakesEffectImmediately |
| Lesson/Photo/Report | SchoolFileOldLinkRechecksClassAuthorizationEvenForOriginalUploader؛ ProfilePhotoCannotBeDeletedThroughFileApiWhileActive؛ Policy20261101ConversationPhotoUsesCurrentMembershipAndNeverBecomesPublicByAssetId؛ StudentExportRunsAsynchronouslyAndDownloadsPrivateSafeCsv |
| Knowledge/AI مجوز جاری | KnowledgeIntegrationEndpointsTests، PersonalKnowledgeAcceptanceTests، AiSourceSelectionEndpointsTests؛ Provider کنترل‌شده است |
| cleanup | TerminalFileCleanupTests و TerminalFileCleanupFileSystemTests؛ بدون تغییر metadata یا purge Ready |

`FileLifecycleAssertions.BoundCannotDeleteAsync` در پذیرش‌های موجود، کد `FILE_IN_USE`، عدم تغییر
rowversion/status و باقی‌بودن bytes را اثبات می‌کند. در SQL ایزولهٔ تست، RetainedAtUtc موقتاً خالی
و در finally بازگردانده می‌شود؛ بنابراین خود binding و نه shortcut retention در هر خانواده آزموده می‌شود.
fixtureهای عادی از SQL Server/LocalDB تست
استفاده می‌کنند؛ EF InMemory شاهد این نتیجه نیست. نتیجهٔ قطعی اجرای کامل در گزارش توسعه‌دهنده ثبت می‌شود.

## تمایزهای ضروری

فایل unlinked با عکس عمومی یکی نیست؛ نسخهٔ اصلی شخصی ممکن است تحت مجوز uploader باقی بماند.
پروندهٔ Submitted که Cancelled شده، مطابق قرارداد تاریخی پرونده ممکن است هنوز برای reviewer
مجاز خواندنی باشد؛ Cancel را خودسرانه معادل حذف تاریخچه نکرده‌ایم.

مدل جاری ReportExportJob زمان انقضای تجاری دانلود ندارد؛ LeaseExpiresAtUtc زمان claim Worker
است، نه download TTL. زمان حذف/انقضای جدید حدس زده نشد. ACL جاری و status/ownership هر بار بررسی می‌شوند.

FileValidated از pipeline فنی Upload می‌آید؛ تضمین کیفیت PDF، OCR، اسکن بدافزار یا تأیید محتوایی
نیست. ReviewRequired/Rejected با Ready، Retry یا نسخهٔ جدیدِ همان فایل به approval تبدیل نمی‌شوند.

منبع Organization/Class به‌علت حذف سازنده پاک نمی‌شود؛ BE-22 فقط منبع Personal متعلق به حساب
حذف‌شونده را Archive و Knowledge آن را از workflow مصوب revoke/delete می‌کند.

پاک‌سازی فقط bytes terminal بدون reference، بدون Retained و با grace/config صریح است.
`UnattachedExpiresAtUtc` مجوز purge نیست. Library AI/دانلود عادی بسته و تأیید رسانه تصمیم باز است.
