# پیاده‌سازی Backend Realtime پس از تأیید طرح محدود

مرجع شروع `main@8084485477e0f83c1133728c82afe5653d23e916` است. این سند قرارداد Backend تأییدشده برای نهایی‌سازی مشترک Realtime و Academic Attachments را توصیف می‌کند؛ شاهد استقرار نیست. مجوز جدید، Commit/Push مشترک پس از validation کامل و تکرار Build/Test روی Commit را شامل می‌شود؛ Publish، Deploy و Migration روی MakanApp یا MakanApp_Test در این کار مجاز نیستند. دو PDF بیست و بیست‌وشش‌صفحه‌ای Issues/Product Realtime و تصمیم صریح مالک مبنا هستند؛ Matrix اولیه پیش از تغییر در artifact محفوظ است. سند Product رفتار هدف را بیان می‌کند و اثبات کد آماده نیست.

## تصمیم‌های تأییدشده

طرح محدود پس از بازبینی مالک تأیید شد. hook عمومی SaveChanges ساخته نشد. Storeهای مشخص، invalidation کمینه را پیش از Save موجود در همان Unit of Work ثبت می‌کنند. هر دو Outbox موجود reuse شدند؛ queue، worker یا جدول موازی ساخته نشد. رسانهٔ Minor/Unknown طبق تصمیم مالک همچنان ReviewRequired است؛ طراحی مسیر تأیید رسانه تصمیم بازِ موکول‌شده است و مانع اجرای Realtime نیست. Safety یا قواعد GAP-05 باز یا ضعیف نشدند.

## نتیجهٔ Owner bug و اصلاحات قرارداد

BUG-MSG-01 و BUG-MSG-02 روی HEAD شروع با Fixture مجاز بازتولید نشدند؛ پنج سناریوی اولیه پیش از اصلاح Backend پاس بودند. علت محیط خارجی نامعلوم است و رفع ساختگی ادعا نمی‌شود. مخاطب دستی/Resolve شماره/Username، پیام اول در Inbox باز، پاسخ زنده، Preview/Unread/Order، دو نشست مستقل و Offline با SQL موقت آزموده شدند.

ContactId رکورد دفترچه است؛ UserContactResult.ContactUserId و Profile.UserId همان User واقعی مقصدند. مسیر واقعی: PeopleController.Resolve → MessagingDirectoryService.ResolvePersonAsync → PhoneNumberNormalizer → EfMessagingDirectoryStore → ContactsController.Save → MessagingDirectoryService.SaveContactAsync → ConversationsController → MessagingService.StartOrGetDirectConversationAsync → EfMessagingStore. شمارهٔ ملی، +98، 98، 0098، ارقام فارسی/عربی، فاصله و خط تیره به یک حساب/Contact می‌رسند. افزودن یک‌طرفه Contact رضایت متقابل نیست؛ Contacts audience بر دفترچهٔ مقصد تکیه دارد.

| Rule | فایل/کلاس/متد واقعی | نتیجه |
|---|---|---|
| حساب و پروفایل مقصد | EfMessagingStore.Eligibility.cs / LoadUserSnapshotAsync | حساب فعال، PersonId، Username و ProfileCompletedAtUtc بررسی می‌شوند |
| سن/رابطه | CommunicationEligibilityPolicy.CanStartOrSend و EfMessagingStore.LoadEligibilityFactsAsync | DOB خوداظهاری Adult تأییدشده نیست؛ رابطهٔ جاری Minor/Unknown لازم است |
| Personal/Organization | CommunicationEligibilityPolicy.CanUsePersonal / CanUseOrganization | Context سرور مرجع است؛ Role آموزشی برای Personal اجباری نیست |
| Privacy/Contact | MessagingPrivacyPolicy.Allows و TargetHasSavedRequester | Nobody/Contacts/Everyone با دیگر محافظ‌ها ترکیب می‌شوند |
| Block دوطرفه | EfMessagingStore.Safety.cs / HasEffectiveUserBlockAsync | شروع/ارسال تازه طبق قواعد موجود رد می‌شود؛ history خودکار حذف نمی‌شود |
| Discovery | MessagingDirectoryService.ResolvePersonAsync | کشف هویت مجوز Direct ایجاد نمی‌کند |
| تشخیص داخلی | EfMessagingStore.Diagnostics.cs / LogDirectPolicyDenialAsync | فقط Category/Code/TraceId؛ بدون شماره، نام، شناسهٔ مخاطب یا Token؛ پاسخ عمومی همان 404 است |

GET /api/v1/conversations/{conversationId}/summary از Query/DTO مجاز Inbox، با predicate شناسه قبل از paging استفاده می‌کند. Archived مانع deep-link نیست؛ participant و scope جاری لازم‌اند. GET بدون /summary همچنان مدیریت Group/Channel است. Binary/Range/OpenAPI فایل نیز اصلاح شد؛ Student/Parent به roster مدیریتی دسترسی تازه نگرفتند.

## معماری و جریان واقعی

Domain و مدل/مهاجرت تغییر نکردند. Application قراردادهای Realtime و portهای حفاظت cursor و delivery را تعریف می‌کند. Infrastructure آداپتر SQL/replay و Writer را دارد. Api، Controller، Data Protection، SignalR و Error contract را متصل می‌کند. .NET 9، EF Core 9، SQL Server، Onion، Modular Monolith و Controllerها حفظ شدند.

```text
HTTP mutation → Application use case / Domain → Store مشخص
→ RealtimeInvalidationWriter → IOutbox / EfOutbox → Save + DB commit
→ OutboxProcessor (claim commit) → SignalRInvalidationNotifier
→ RealtimeSyncService / مجوز جاری → گروه نشست → ClientInvalidated → refetch مجاز
```

پیام‌رسان همان ChangeEvents/RealtimeOutbox، Dispatcher و ConversationChanged را نگه می‌دارد. SignalRMessagingRealtimeNotifier برای هر نشست از IRealtimeSessionScope مجوز/ContextRevision جاری می‌گیرد. گروه قبلی یا UserId به‌تنهایی مجوز نیست. Session group هنگام اتصال خودکار است؛ پیام اول به SubscribeConversation قبلی متکی نیست.

## نقاط محدود ثبت

| حوزه | Store/Writer و رویداد |
|---|---|
| Organization/Guardian | EfOrganizationStore، EfOrganizationPersonStore و EfGuardianStore؛ Directory، Role/Membership، رابطه و انتخاب Context |
| Academic | EfAcademicStore، EfAcademicSessionStore، EfClassLessonStore؛ Class/Enrollment/TeacherAssignment، Lesson Draft/Published و Session |
| Assessment | EfAssignmentStore، EfSubmissionStore، EfEvaluationStore و Storeهای Exam/Attempt/Answer/Finalization/Grading؛ فقط metadata شناسه/دامنه |
| Messaging | SaveSyncAsync، SaveSafetyAsync، SaveManagementAsync و Saveهای مشخص EfMessagingDirectoryStore؛ دسترسی خود فرد و Privacy/Contact/Block |
| Commerce | EfCommercialStore، Reservations، AdminStore، EfCommerceStore و EfSubscriptionActivationHandler؛ قرارداد، رزرو/نهایی‌سازی و مصرف موجود |
| PersonalClass | EfPersonalClassStore؛ ظرفیت میزبان و دسترسی خود طرف‌ها، بدون ایجاد چرخهٔ آموزشی جدید |
| Storage | EfFileAssetStore؛ ورود/خروج Ready در meter واقعی Storage سازمانی؛ مصرف فردیِ پیاده‌سازی‌نشده از روی uploader حدس زده نمی‌شود |

Writer دادهٔ تجاری را تغییر نمی‌دهد و Subscription، Entitlement، Quota، Role یا Enrollment تازه اعطا نمی‌کند. حقوق Legacy و دو Migration Commerce قبلی محفوظ‌اند؛ قیمت/سهمیه/تاریخ تخمینی ساخته نشد. Capture، publish شبکه نیست؛ rollback تجاری Outbox همان عملیات را نیز rollback می‌کند. شبکه خارج تراکنش و completion فقط با claim معتبر است. خرابی بعد از ارسال می‌تواند ارسال مجدد همان EventId ایجاد کند.

## Replay بدون Migration تازه

GET /api/v1/realtime/changes روی دو جدول موجود platform.OutboxMessages و messaging.RealtimeOutbox و rowversionهای موجود کار می‌کند. جدول و index جدیدی ساخته نشد. نتیجهٔ مدل EF و شمارش تست نهایی در گزارش TRX ثبت می‌شود؛ migrationها فقط به‌صورت خودکار در fixture ایزوله اعمال شدند.

MIN_ACTIVE_ROWVERSION مرز exclusive امن است؛ @@DBTS تنها، transaction دیرCommitشده را ممکن است حذف کند. cut SQL در تراکنش کوتاه Serializable materialize و commit می‌شود؛ بررسی مجوز و شبکه خارج آن‌اند. payload Outbox immutable و EventId ثابت است، ولی claim/retry rowversion را عوض می‌کند؛ بنابراین StreamVersion نسخهٔ Entity نیست و event تکراری مجاز است.

یک race واقعی در تست پیدا و رفع شد: اگر tail ثبت‌شده جلوتر از cut امن باشد، HasMore نباید false شود. اکنون HasMore=true و در صفحهٔ بدون پیشرفت کافی RetryAfterMilliseconds=100 برمی‌گردد. cursor از مرز امن عبور نمی‌کند. Gate آزمایشی پس از محاسبهٔ watermark، این حالت را قطعی بازتولید می‌کند. Client باید backoff را رعایت کند، نه اینکه صفحهٔ خالی را پایان قطعی فرض کند.

Cursor با Data Protection رمز/امضا و به User، Session، ContextRevision و epoch بسته می‌شود. revision شامل rowversion نشست، Membership/Role و رابطهٔ انتخاب‌شدهٔ Guardian است. این انتخاب محافظه‌کارانه باعث می‌شود Refresh نشست نیز گاهی resync بخواهد؛ schema مخصوص context epoch اضافه نشد. اعتبار cursor حداکثر ۲۴ ساعت است، نه وعدهٔ Offline نامحدود. کلیدهای test فقط Ephemeral در حافظه‌اند؛ برای host واقعی Data Protection استاندارد استفاده می‌شود. تغییر/از‌دست‌رفتن keyring، resync می‌خواهد و session احراز هویت را تبدیل به Mock نمی‌کند.

## امنیت مخاطب

Scope اولیه در SQL محدود و مجوز جاری قبل از delivery/replay دوباره بررسی می‌شود. Assignment/Exam/Submission/Grade از read use caseهای Application موجود مجوز می‌گیرند؛ قوانین نمره یا Guardian کپی نشدند. Draft نمره/درس/پاسخ، AnswerKey، private note، roster و جزئیات مالی در notification نیستند. Access hint خود شخص بعد از revoke فقط key=access و ResourceId=null دارد. تاریخچهٔ مجاز با پایان رابطه خودکار حذف نمی‌شود.

## محدودیت‌های باقی‌مانده

افزودهٔ BE-01: علت داخلی UnsupportedMedia شناسایی و Contract/Observability تکمیل شد؛ `PRODUCT_DECISION_BLOCKED=true` برای workflow نهایی پیوست برقرار است. پاسخ 422، PendingReview را از Rejected جدا می‌کند و `safetyReference` پایدار را به لاگ کمینه و شواهد خصوصی قبلی وصل می‌کند. هیچ release، صف انسانی یا تغییر enum/Schema ساخته نشد؛ [قرارداد دقیق](../messaging/BE01_REVIEW_REQUIRED_CONTRACT_FA.md).

پذیرش UI واقعی/فرم باز، IIS/Proxy، چند replica/backplane، load/storm و SLO رسمی اجرا نشده‌اند. AI runtime و lifecycle منابع/stream در این کار فعال نشدند. invalidation زمان‌محور انقضا/Grace بدون هیچ Write، scheduler تازه ندارد؛ وضعیت authoritative همچنان از API سرور خوانده می‌شود. هم‌بستگی Trace در producerهای HTTP جدید موجود است، اما زمان دقیق Commit SQL و tracing دائمی تمام مسیر قدیمی پیام‌رسان اضافه نشده‌اند. رسانهٔ محافظت‌شده عمداً ReviewRequired می‌ماند.

## نهایی‌سازی همراه پیوست‌های آموزشی — ۲۰۲۶-۱۰-۳۰

[پیوست تکلیف و پاسخ آزمون](ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md) همان invalidation موجود `assessment.submission` و `assessment.exam-attempt` را reuse می‌کند. Attach/Detach آزمون، parent Attempt و AnswerSetVersion را در همان transaction معتبر تغییر می‌دهد؛ Actor/Session/Context و مجوز خواندن در تحویل و replay دوباره بررسی می‌شوند. payload عکس، متن پاسخ، AnswerKey و مسیر فایل در event نیست. Schema جدید فقط برای پیوست/سیاست سؤال آزمون است؛ Realtime همچنان جدول، Outbox، hook عمومی یا worker موازی ندارد.

پذیرش تازهٔ پیش از Commit: Restore موفق؛ Release Build با صفر Warning/Error؛ Unit=922، Integration=1542، Architecture=14، مجموع 2478 موفق، Failed=0 و Skipped=0. ۵۲ تست Academic جدید، ۵۹ تست با عنوان Realtime و ۹۵ تست با عنوان Commerce/Commercial/Subscription/Entitlement/Quota در همین suite موفق‌اند. شش E2E مستقل با ۱۴۱ HTTP و تطابق SHA-256 فایل‌ها اجرا شد. این نتیجه پذیرش Backend محلی است؛ UI یا سرور مستقر را اثبات نمی‌کند. Pending Model Changes وجود ندارد. نتیجهٔ پس از Commit جداگانه با همان HEAD بررسی می‌شود.

۳ فایل AI و هر ۴ فایل تنظیمات از scope ثبت خارج‌اند؛ Production/Test خصوصی و ignored هستند، پایه/Development از قبل tracked و بدون تغییرند. checksum کد و تست تأییدشدهٔ قبلی حفظ شده است. SHA و وضعیت Push، پس از اجرای واقعی Git و Build/Test روی HEAD، در گزارش اجرای نهایی ارائه می‌شوند؛ وجود این سند به‌تنهایی اثبات Push یا پذیرش سرور نیست. BE-01، AI زنده، UI، multi-replica و SLO همچنان با محدودیت‌های بالا باقی‌اند.
