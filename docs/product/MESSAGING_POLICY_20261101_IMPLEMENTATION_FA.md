# پیاده‌سازی سیاست پیام‌رسان ۲۰۲۶/۱۱/۰۱

مرجع محصول: `MakanApp_Backend_Messaging_Policy_FA_20261101.pdf` و تأیید صریح مالک محصول در همین مرحله. مبنای شروع `main@166a4aa` بود. این سند پیاده‌سازی Backend را توضیح می‌دهد؛ استقرار یا پذیرش روی سرور انجام نشده است. سه فایل ناتمام `tools/ai/*.py` و چهار تنظیم خصوصی API خارج از تغییرات‌اند.

## تصمیم محصول و تغییر معنای قرارداد

**BREAKING SEMANTIC CHANGE:** فیلدهای سن، DOB و وضعیت تأیید سن دیگر مجوز امکانات عمومی پیام‌رسان نیستند. شکل انتقال فیلدهای قدیمی تا حد ممکن حفظ شده است. `verificationStatus` و `effectiveAgeCategory` فقط واقعیت پروفایل‌اند؛ نه دلیل نمایش دکمه، منع ارسال یا اعطای دسترسی.

حساب و نشست فعال، هویت ضروری پروفایل، PrivacyAudience، Block، عضویت معتبر، مجوز منبع، وضعیت محدودیت و Content Safety همچنان مستقل اجرا می‌شوند. DOB ساختگی، Adult ساختگی، تأیید ساختگی، حذف دادهٔ سن و Backfill سن نداریم. سن قانونیِ مدل قدیمی صرفاً برای نگه‌داری واقعیت تأییدشده باقی است؛ آستانه‌ای برای Student Plan ساخته نشده است.

گزارش تطبیق همهٔ hitهای Source در `AGE_AUTHORIZATION_AUDIT_FA.md` کنار شواهد خصوصی اجرای همین مرحله است. تاریخچهٔ Migrationها بازنویسی نشده است. `ConversationManagementAuthorizationPolicy` از قبل پارامتر سن را مصرف نمی‌کرد؛ امضای آن برای سازگاری حفظ شده است. Factهای قدیمی در `CommunicationEligibilityFacts` و `MessagingDirectoryAbstractions` باقی‌اند و دیگر تصمیم مجوز سنی نمی‌سازند.

## معماری و فایل‌های اصلی

| لایه / فایل | مسئولیت و دلیل |
|---|---|
| Application / `CommunicationEligibilityPolicy.cs` | قواعد مجوز Personal/Organization؛ حذف فقط شرط سن و حفظ قواعد واقعی رابطهٔ سازمانی و Privacy |
| Application / `MessagingStatus.cs` | راهنمای Self؛ نسخهٔ Policy در Version، بدون NextAction مربوط به تأیید سن |
| Application / `ConversationCapabilities.cs` | محاسبهٔ خالص قابلیت مؤثر از واقعیت‌های مجوز؛ هیچ EF یا HTTP در آن نیست |
| Application / `ConversationPermissionCatalog.cs` | کاتالوگ ۱۳ permission اتمی Admin و ۳ capability عضو؛ وضعیت تفویض و Policy رسمی |
| Application / `AccountInboxService.cs` | اعتبارسنجی فیلتر و Cursor حساب/نشست؛ نگاشت خلاصهٔ مشترک با مسیر scoped |
| Application / `ConversationPhotoService.cs` | قرارداد عکس، بررسی شناسه/نسخه و استفاده از Port ذخیره‌سازی |
| Domain / `ConversationInitialTarget.cs` | Receipt پایدار هر مخاطب اولیه؛ Pending دعوت با عضو فعال متفاوت است |
| Domain / `Conversation.cs` | Binding عکس و عملیات محدود اطلاعات رسمی؛ Roster/مالکیت آموزشی حفظ می‌شود |
| Domain / `Session.cs` و `AcademicActivityPublication.cs` | Revision واقعی چرخهٔ جلسه و Snapshot رویداد عمومی آن |
| Infrastructure / `EfMessagingStore.Capabilities.cs` | خواندن batch مجوزهای جاری، نقش رسمی، محدودیت حساب و عضو |
| Infrastructure / `AccountInboxAccessQuery.cs` | Query مشترک منابع قابل خواندن حساب؛ منبع ACL صندوق، Replay و عکس |
| Infrastructure / `AccountInboxRealtimeService.cs` | نمای حساب روی همان Outboxهای موجود، Cursor مستقل و بازبینی مجوز هر رویداد |
| Infrastructure / `EfMessagingStore.Photos.cs` | تراکنش، قفل فایل/گفتگو، rowversion، Audit و جلوگیری از اتصال فایل خصوصیِ متعلق به جریان دیگر |
| Infrastructure / `EfOfficialSpacesStore.cs` | استفادهٔ مجدد از جفت رسمی، مدیریت منبع‌محور، Rename و اعلان Ready پس از تعمیر |
| Infrastructure / `OfficialSpaceEventWriter.cs` | Receipt انتشار و ثبت Session در همان تراکنش منبع و Outbox قبلی |
| Api / `ConversationPhotosController.cs` | انتقال HTTP؛ بدون EF، SQL یا منطق مجوز کسب‌وکاری |
| Api / `ConversationsController.cs` و `RealtimeController.cs` | مسیرهای افزایشی صندوق حساب، permission catalog و Replay |

جریان واقعی صندوق: `ConversationsController.Account → AccountInboxService → IAccountInboxStore → EfMessagingStore.ReadAccountInboxAsync → AccountInboxAccessQuery → SQL Server → ConversationSummaryMapping → HTTP`.

جریان عکس: `ConversationPhotosController → ConversationPhotoService → MessagingOperationContextResolver → IConversationPhotoStore → EfMessagingStore.SetPhotoAsync → Domain Conversation.SetPhoto → SaveManagementAsync / AppendChangeAsync → SQL Commit → Outbox → SignalR`.

جریان فعالیت: `AcademicSessionService / ClassLessonService / AssignmentService / ExamService → IAcademicActivityPublisher → OfficialSpaceEventWriter → SQL Commit → OutboxProcessor / OfficialSpacesWorkHandler → AcademicActivityDelivery → Message سیستم → Replay/SignalR`.

## حذف شرط سن و تفکیک Safety

Discovery هویت عمومی فقط با حساب فعال، هویت ضروری و Block دوطرفه انجام می‌شود. کشف شماره و نمایش شماره همچنان Privacy مستقل دارند؛ جست‌وجوی Username شماره را حمل نمی‌کند. کارت مخاطب فقط Username/DisplayName/Avatar مجاز دارد و Block هر مخاطب فعلی را پیش و پس از ارزیابی Safety دوباره بررسی می‌کند.

`EfMessagingStore.ChildSafety` اکنون همهٔ سن‌ها را به Provider می‌فرستد. Provider واقعی `InternalMakanContentSafetyProvider` تغییر نکرده است. Unavailable یعنی fail-closed؛ متن مسدود، ReviewRequired و پروندهٔ Safety مجوز ارسال نمی‌شوند. رسانهٔ پشتیبانی‌نشده همچنان ReviewRequired است؛ پیام، Attachment تحویل‌شده یا دانلود گیرنده ساخته نمی‌شود. Human Review جدید یا Safety bypass ساخته نشده است. ارسال/ویرایش/Forward، Topic، Poll و زمان‌بندی از همان کنترل جاری استفاده می‌کنند.

وضعیت Self با پروفایل واقعاً ناقص هنوز `ProfileCompletionRequired` می‌دهد. نبود DOB یا Unverified/Conflict به‌تنهایی مانع نیست. قابلیت Personal مستقل از Organization منتخب است؛ این استقلال نقش یا مجوز سازمانی نمی‌سازد. Version وضعیت با `messaging-20261101:` شروع می‌شود و opaque است.

## ساخت شخصی و دعوت اولیه

Ordinary، Student، Parent، Teacher و Manager با حساب/نشست و هویت لازم می‌توانند Group/Channel شخصی بسازند؛ سازنده Owner است. اختیار روی گفتگوی شخصی دیگران از عنوان شغلی مدرسه نتیجه نمی‌شود. ساخت Organization همچنان مجوز سازمانی قبلی را لازم دارد.

`ClientOperationId` و hash بدنهٔ موجود حفظ شدند: Retry یکسان همان گفتگو و Receipt را برمی‌گرداند؛ تغییر Payload با همان شناسه `CONVERSATION_CREATION_CONFLICT` است.

در Personal، مخاطب اولیهٔ مجاز دعوت واقعی دریافت می‌کند و تا پذیرش احرازشده Participant فعال نیست. طبق اصلاح مصوب `2026-11-02`، انقضای پیش‌فرض **۷ روز از زمان ایجاد دعوت در سرور** است. این Policy فقط متعلق به دعوت اولیهٔ Messaging است و با Guardian یا سایر Domainها مشترک نیست. در Organization، افزودن مجاز طبق قواعد قبلی انجام می‌شود. نتیجهٔ هر مخاطب: `Added`، `InvitationPending` یا `NotAvailable`. نتیجهٔ عمومی NotAvailable، علت خصوصی یا وجود حساب غیرقابل‌کشف را افشا نمی‌کند؛ شناسهٔ ورودی فقط در Receipt خود سازنده بازتاب داده می‌شود.

DTO ساخت `ExpiresAtUtc` دریافت نمی‌کند. زمان محاسبه‌شده در Invitation و Receipt ذخیره می‌شود و `initialTargets[].invitationExpiresAtUtc` همان مقدار واقعی را برمی‌گرداند. Retry با همان `ClientOperationId` و Payload، قبل یا بعد از انقضا، Invitation جدید نمی‌سازد و زمان قبلی را تغییر نمی‌دهد. Accept دعوت Pending در `now >= ExpiresAtUtc` مجاز نیست؛ ایجاد عملیات جدید با شناسهٔ تازه از Retry جداست. دعوت‌های قبلی با مهلت ذخیره‌شدهٔ خود باقی می‌مانند؛ Migration یا Backfill لازم نیست. سقف مستقل دعوت صریح، همچنان ۳۰ روز است.

Receipt اولیه فقط به سازنده نمایش داده می‌شود و Snapshot ایجاد است؛ پس از پذیرش، وضعیت جاری را از API دعوت/اعضا بخوانید. افزودن مستقیم عضو Personal نیازمند دعوت و پذیرش است و `CONVERSATION_INVITATION_REQUIRED` می‌دهد. Invite Link موجود، ورود اختیاری کاربر و Join Request قبلی حفظ شده‌اند. Retry پذیرش، عضو یا اثر دوم نمی‌سازد.

## فضاهای رسمی و مدیریت

`OfficialSpaceProvision`، Bindingها، Indexهای یکتا و Backfill CLI موجود reuse شده‌اند؛ برای هر منبع رسمی یک Group و یک Channel باقی می‌ماند. GET جفت رسمی، Side Effect ساخت ندارد. مقصد سالم هنگام خرابی مقصد دیگر بازسازی نمی‌شود. Rename کلاس/سازمان از Outbox موجود به همان ConversationIdها می‌رسد؛ عنوان شخصی تغییر نمی‌کند. تعمیر Binding موجود نیز Ready شدن را با invalidation کمینه به مخاطبان فعلی اعلام می‌کند.

Manager معتبر و هر Teacher دارای Assignment جاری، مدیریت محتوای رسمی دارند: Description/Photo، حذف/Pin پیام مجاز، انتشار در Channel و مدیریت Topic گروه. عنوان رسمی از منبع آموزشی می‌آید. API اطلاعات رسمی اجازهٔ تغییر مستقل Title نمی‌دهد.

Participant رسمی همچنان Member است؛ Owner جعلی، Admin جعلی یا UserManaged شدن نداریم. افزودن/حذف عضو، Admin، دعوت، Ban، Visibility، History و Enrollment از این قابلیت محتوا نتیجه نمی‌شوند. Roster از آموزش اداره می‌شود. Student با Enrollment جاری در گروه می‌فرستد و Channel را می‌خواند؛ مدیریت محتوا یا انتشار Channel ندارد. Parent صرف GuardianRelation عضو کامل کلاس نمی‌شود و به Direct خصوصی کودک دسترسی نمی‌گیرد.

پایان یک TeacherAssignment فقط همان دلیل دسترسی/مدیریت را قطع می‌کند. اگر Enrollment یا دلیل معتبر دیگری باقی باشد، حق همان دلیل حفظ می‌شود. Personal و نقش Parent تغییر نمی‌کنند. هر Send/Read/Replay/File مجوز جاری را دوباره می‌سنجد؛ حذف تاریخچهٔ ذخیره‌شده یا تصمیم جدید History Retention انجام نشده است.

`effectiveCapabilities` در Managed Detail، Summary و Official Pair افزوده شده است. `CanChangeSettings` محافظه‌کارانه اختیار کل تنظیمات را نشان می‌دهد و به سه permission `ManageHistory + ManageVisibility + ManageMembers` نیاز دارد؛ برای اختیارهای جزئی، permission catalog و پاسخ نهایی Endpoint ملاک‌اند. Capability وعدهٔ موفقیت Safety یا حذف کنترل Concurrency نیست. `CanManageMessages` در این مرحله اختیار حذف مجاز را بیان می‌کند؛ ویرایش متن دیگران ایجاد نشده است.

## صندوق حساب و ترتیب

`GET /api/v1/conversations/account` تمام Personal و Organizationهای فعلاً مجاز را یک‌جا از SQL می‌خواند. ادغام چند صفحهٔ scoped در حافظه نیست. ترتیب: Pinned، سپس زمان آخرین پیام قابل مشاهده یا زمان ایجاد، سپس Guid با ترتیب SQL Server؛ همه نزولی. Preview/Unread/FromJoin/Clear/ForMe از Query موجود reuse می‌شوند.

فیلتر نوع، Unread، Archived و Search روی کل Query پیش از Limit اعمال می‌شوند. Limit بین ۱ و ۱۰۰ و پیش‌فرض ۳۰ است. Cursor به User/Session/Filter/Search/Limit بسته و حداکثر ۲۴ ساعت معتبر است. هر صفحه مجوز جاری را دوباره می‌سنجد؛ پایان عضویت بین صفحات منابع بعدی را حذف می‌کند. Cursor از نوع keyset است و Snapshot ثابت زمانی نیست: با تغییر Pin یا فعالیت هم‌زمان، کلاینت باید invalidation را اعمال و صفحهٔ اول را تازه کند. در مجموعهٔ ثابت، تست ۹۹ گفتگو با ۳۳ گفتگو در هر سه scope نبود حذف/تکرار را بررسی می‌کند.

مسیر scoped قبلی، Workspace منتخب و Cursor آن تغییر نکرده‌اند. API جدید نتیجهٔ `items/nextCursor/hasMore` دارد و با آرایهٔ مسیر قدیمی اشتباه نشود. این پاسخ `no-store` است.

## Realtime و بازیابی

Hub `/hubs/messaging` و همان دو Outbox تراکنشی موجود باقی‌اند. `ConversationChanged` قبلی حفظ شده؛ برای نشست‌های مجاز در هر Workspace، `ClientInvalidated` با `key=messaging.account-inbox` اضافه می‌شود. این رویداد هیچ متن، شناسهٔ پیام یا Organization خصوصی ندارد؛ ResourceId تهی و کار آن درخواست مجدد صندوق است.

`GET /api/v1/realtime/account-inbox/changes` نمای مستقل AccountInbox دارد؛ Cursor آن با `realtime/changes` و Cursor صفحه‌بندی صندوق قابل تعویض نیست. Cursor به User/Session/Epoch بسته است و ۲۴ ساعت عمر دارد. `MIN_ACTIVE_ROWVERSION` و اسکن کوتاه SQL از عبور Cursor از Transaction هنوز Commit‌نشده جلوگیری می‌کنند. مجوز فعلی منبع و visibility واقعی Delta برای هر رویداد بررسی می‌شود. Session revoke، Membership/Role/Enrollment revoke و History Filter در Replay نیز رعایت می‌شوند.

بدون Cursor، ابتدا مرز Snapshot می‌گیرید. سپس صندوق را بخوانید و رویدادهای بعد از همان مرز را drain کنید. Reconnect باید Replay را drain کند؛ `hasMore` و `retryAfterMilliseconds` را رعایت کنید. Cursor نامعتبر/منقضی/نمای دیگر، `REALTIME_RESYNC_REQUIRED` با 409 می‌دهد؛ Snapshot را از ابتدا بگیرید. تحویل حداقل یک‌بار است؛ `(EventId, StreamVersion)` را dedupe کنید و صرف دریافت Event، Unread یا Message جدید نسازید. ContextRevision نمای حساب مستقل از Workspace منتخب است؛ Revisionهای scoped را با آن مقایسه نکنید.

## Receipt انتشار و فعالیت جلسه

پاسخ انتشار Lesson/Assignment/Exam و پاسخ تغییر جلسه، `publicationReceipt` شامل `eventId/activityKind/sourceId/sourceVersionId/sourceRevision/deliveryState` دارند. Receipt اثبات ثبت Publication است؛ Pending به معنی تحویل کامل نیست. وضعیت دو مقصد از API delivery موجود خوانده می‌شود. Retry منبع طبق قرارداد قبلی است: Lesson می‌تواند همان Receipt را بدهد؛ خطای وضعیت Already Published در Assignment/Exam برای Retry تغییر نکرده است و Activity دوم ساخته نمی‌شود.

Session واقعی با Revision برای `Scheduled/Rescheduled/Cancelled/Completed` به همان Publisher و دو مقصد رسمی متصل شد. ویرایش No-op Revision جدید نمی‌سازد؛ Schedule Rule هم فقط تغییر واقعی رخدادها را منتشر می‌کند. جلسهٔ قدیمی Revision صفر دارد تا رویداد تاریخی ساختگی ایجاد نشود. Card قدیمی Superseded و جلسهٔ لغوشده/تکمیل‌شده/منقضی Actionable نیست. زمان/وضعیت منبع فعلی ملاک است.

Draft، پاسخ دانشجو، Attachment پاسخ، کلید آزمون، Grade خصوصی، autosave و یادداشت ارزیاب وارد Card نمی‌شوند. مدل مستقل Published Educational Note در Source وجود ندارد: `PUBLISHED_EDUCATIONAL_NOTE_MODEL_ABSENT / PRODUCT_CONTRACT_REQUIRED`. برای آن Entity یا Endpoint فرضی ساخته نشده است.

## عکس و لینک

عکس Group/Channel از FileAsset تازهٔ Ready، JPEG/PNG معتبر و متعلق به مدیر مجاز همان scope است. فایل متعلق به دیگری یا متصل به Profile/Academic/Message/Scheduled/Moderation/Report/Conversation دیگر قابل استفاده نیست. Retained evidence یا فایل Pending مجاز نیست. Binding عکس، RowVersion فایل را هم تغییر می‌دهد تا حذف هم‌زمان با نسخهٔ قبل نتواند فایل متصل را پاک کند.

PUT/DELETE با نسخهٔ گفتگو، Audit و همان Outbox در تراکنش انجام می‌شوند. تکرار Binding یکسان No-op است؛ تغییر متفاوت با نسخهٔ قدیمی 412 است. دانلود از URL قبلی Storage با **ACL جاری Conversation** انجام می‌شود؛ دانستن FileAssetId، مالک‌بودن قبلی یا داشتن دعوت Pending کافی نیست. حذف عکس فقط Binding را برمی‌دارد؛ Blob فوراً حذف نمی‌شود.

Invite Link قبلی قرارداد لینک قابل اشتراک است. Handle عمومی جدید، رزرو نام، Rename یا نرمال‌سازی جدید حدس زده نشده‌اند.

## دیتابیس و Migration

Migration: `20261101202642_ExpandMessagingAccountPolicyAndMedia`، بعد از `20261030152943_AddAiSourceSelectionPins`؛ تعداد ۵۹.

- `messaging.ConversationInitialTargets`: کلید مرکب `(ConversationId, TargetUserId)`، FKهای Restrict گفتگو/دعوت و CHECK سازگاری Outcome و مشخصات دعوت. TargetUserId selector درخواست است و ممکن است وجود نداشته باشد؛ FK User ندارد تا NotAvailable وجود حساب را افشا نکند.
- `messaging.Conversations.PhotoFileAssetId`: nullable، FK Restrict FileAsset، Index یکتای فیلترشده و CHECK فقط Group/Channel. RowVersion فعلی گفتگو/فایل reuse شد.
- `academic.Sessions.ActivityRevision`: مقدار اولیهٔ صفر برای دادهٔ قبلی، بدون ساخت Activity تاریخی.
- `messaging.AcademicActivityPublications.SessionId/SessionAction`: nullable برای سازگاری دادهٔ قبلی، FK منبع سازمان/کلاس/جلسه، CHECK نوع منبع/Action و Index یکتای فیلترشده Session/Revision. Index قبلی Organization/Class با همان پیشوند و Session توسعه یافت.

Up داده یا ستون سن حذف نمی‌کند. اسکریپت safe/idempotent توسط ابزار موجود `tools/production/export-migration-sql.ps1` ساخته شد. ساخت از صفر، ارتقا از Baseline ۵۸، اجرای دوم No-op و تطابق fingerprint Schema/History در دو LocalDB موقت بررسی شد؛ هر دو حذف شدند. `MakanApp`، `MakanApp_Test` و سرورها تغییر نکردند. اجرای Migration در Startup اضافه نشده است. اسکریپت‌های نهایی و SHA256 در پوشهٔ شواهد خصوصی مرحله نگه‌داری می‌شوند.

## تست‌ها و شواهد

Unit جدید در `MessagingPolicyRevisionTests`، سیاست سن مستقل، capability رسمی/شخصی، محدودیت‌ها، کاتالوگ و دعوت ۳۰روزه را بررسی می‌کند. Integrationهای `MessagingPolicyRevisionContractTests`، `MessagingPolicyRevisionOfficialTests`، `MessagingPolicyRevisionRealtimeTests` و `MessagingPolicyRevisionAiTests` از ASP.NET Core و SQL Server واقعیِ ایزوله استفاده می‌کنند. تست Fake AI نبود DOB را در **بافت سازمانی مجاز موجود** همراه با Quota بررسی می‌کند؛ AI عمومیِ بدون مجوز سازمانی تازه‌ای ایجاد نشده است.

تست‌های قدیمی حذف یا Skip نشده‌اند. انتظار منع مبتنی بر سن به قرارداد جدید تغییر کرده؛ Negativeهای رضایت، Privacy، Block، نشست، Roster، Source ACL و Safety حفظ/دقیق‌تر شده‌اند. تست Contact Card از Block مخاطبان فعلی استفاده می‌کند؛ حذف Contact وقتی Privacy=Everyone است دیگر منع سنی ایجاد نمی‌کند.

پذیرش محلی: Kestrel واقعی، SQL موقت، شش حساب شامل پنج نوع درخواستی، OTP Mock مجاز فقط در Testing و Provider واقعی Internal Makan Safety؛ WebSocket واقعی SignalR، Replay آفلاین، Unread و Retry، سه Receipt انتشار. هیچ SMS یا AI واقعی فراخوانی نشده و شواهد فقط Status/Code/Correlation/Count دارند. شمارش دقیق نهایی و نتایج تکرار Build/Test در گزارش اجرا ثبت می‌شوند؛ شواهد محلی اثبات پذیرش سرور نیستند.

نتیجهٔ نهایی پیش از Commit، اجرای `2026-11-02`: Restore و Release Build موفق؛ صفر Warning/Error؛ **۱۰۳۷ Unit + ۱۶۷۵ Integration + ۱۴ Architecture = ۲۷۲۶** تست موفق، Failed/Skipped صفر. ۲۳ Case جدید Integration و ۲۴ Case اضافهٔ Unit نسبت به Baseline وجود دارد. اجرای آخر Kestrel **۱۱۲ درخواست HTTP، ۳۶ بررسی موفق و صفر شکست** داشت و WebSocket واقعی استفاده شد؛ تعداد درخواست Poll وضعیت Ready می‌تواند بین اجراها فرق کند. OpenAPI محلی ۳۸۸ عملیات/۳۴۲ مسیر/۶۵۱ schema دارد. SQL ایزوله و اجرای دوم No-op، PendingModelChanges=false، اسکن تغییرات و هفت Hash محافظت‌شده موفق‌اند. تکرار پس از Commit و نتیجهٔ Git از گزارش نهایی اجرا خوانده شوند.

## ماتریس ۱۸ سناریوی محصول

| سناریو | شاهد واقعی |
|---|---|
| A01 | Theory پنج نقش در `Policy20261101EveryAccountUsesGeneralMessagingWithoutDobOrAgeVerification` و ساخت پنج نوع حساب در Kestrel |
| A02/A03 | Unit ماتریس سن، Integration پنج نقش، Self Status و Direct پس از تغییر سن |
| A04 | `Policy20261101CopilotWithoutDobUsesFakeProviderAndKeepsQuotaAndRoleBoundaries` و تست‌های Source Selection قبلی |
| A05 | همان تست Quota و Regression Commerce/PersonalClass؛ هیچ Rule سن یا نقش جدید از Plan ساخته نشده |
| A06 | Regression هم‌زمانی/Indexهای Official Spaces در `OfficialSpacesLifecycleAcceptanceTests` |
| A07 | `Policy20261101TwoTeachersAndManagerHaveOfficialContentCapabilitiesWithoutOwningRoster` و سه مدیر محتوای واقعی در Kestrel |
| A08/A09 | همان تست پایان Assignment/Enrollment و حفظ دلیل دیگر/Parent/Personal؛ تست‌های Official Relation قبلی |
| A10 | Regression Backfill و Delivery مستقل دو مقصد؛ تست تعمیر Ready Binding موجود |
| A11/A12 | `Policy20261101CreationReceiptHasPrivatePerTargetOutcomesAndRetryCreatesOneInvitation`، Privacy/Block، پذیرش تکراری و هم‌زمان |
| A13 | `Policy20261101AccountInbox...` با ۹۹ گفتگو و ۳۳ مورد در هر scope، Cursor، Search و revoke بین صفحات |
| A14 | `Policy20261101AccountInvalidation...` با دو نشست مستقل؛ Kestrel/WebSocket واقعی بدون SubscribeConversation |
| A15 | Receipt واقعی Lesson/Assignment/Exam در Kestrel؛ Session lifecycle و Regression Superseded/Delivery قبلی |
| A16 | Negativeهای CrossOrg/Guardian/Files/Realtime قبلی و تست‌های جدید عکس و Cursor |
| A17 | Replay مستقل نمای حساب، Cursor غیرقابل‌تعویض، WebSocket قطع/وصل و Context منتخب دیگر |
| A18 | `Policy20261101AccountReplayUsesCurrentOrganizationGrantAcrossSelectedContextsAndRevocation`، حذف دسترسی عکس و Regression Source ACL |

همهٔ Aها الزاماً صرفاً با HTTP/Kestrel اجرا نشده‌اند؛ SQL Constraint/Concurrency و Fake AI شواهد Integration جدا دارند. نام کامل تست و نتیجهٔ واقعی از TRX اجرای نهایی خوانده شود؛ Prefixهای کوتاه جدول نام Endpoint فرضی نیستند.

## تصمیم‌های باز و مسیر مطالعه

Student Plan age/evidence، Parent full-class audience، Official history retention، Handle عمومی در صورت ناکافی‌بودن Invite Link و مدل Published Note نیازمند تصمیم Product‌اند. ReviewRequired همچنان برای Attachment مسدودکنندهٔ انتشار است و Workflow دستی ساخته نشده است. AI/RAG، MFA، MeliPayamak و تنظیمات خصوصی تغییر نکردند.

برای مطالعه: `ConversationsController / ConversationPhotosController → AccountInboxService / ConversationPhotoService → ConversationCapabilityPolicy / ConversationInitialTarget → EfMessagingStore.Reads / Capabilities / Photos → AccountInboxAccessQuery → AccountInboxRealtimeService → OfficialSpaceEventWriter → EF Configurations → Migration → تست‌های PolicyRevision`.

نکات آموزشی: Fact پروفایل با Authorization متفاوت است؛ Capability UI جای مجوز نهایی را نمی‌گیرد؛ Receipt ثبت با Delivery فرق دارد؛ Cursor باید به نمای داده و نشست بسته باشد؛ RowVersion به‌تنهایی جای قفل چندمنبعی را نمی‌گیرد؛ Consent دعوت با Roster آموزشی یکی نیست. مرحلهٔ بعد پذیرش و انتشار جداگانهٔ همین Commit پس از Backup/بازبینی تنظیمات است؛ در این مرحله Publish/Deploy انجام نمی‌شود و بستهٔ قدیمی `166a4aa` پس از Commit جدید نباید Deploy شود.
