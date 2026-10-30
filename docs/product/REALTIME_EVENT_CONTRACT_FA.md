# قرارداد Event و Replay حساب

وضعیت: قرارداد Backend تأییدشده برای نهایی‌سازی مشترک Realtime و Academic Attachments؛ نتیجهٔ Commit/Push از گزارش اجرای واقعی Git خوانده می‌شود و این سند شاهد استقرار نیست. /hubs/messaging، متدهای SubscribeConversation/UnsubscribeConversation/SetTyping/Heartbeat/SubscribePresence و ConversationChanged موجود حفظ شده‌اند. Client گروه مخاطب را انتخاب نمی‌کند.

## ConversationChanged موجود

فیلدها: conversationId، type، resourceId، resourceVersion، cursor، occurredAtUtc، payloadVersion، peerReadThroughSequence اختیاری و ContextRevision اختیاریِ همان نشست. ContextRevision یک hash opaque برای بررسی برابری است؛ عدد افزایشی یا قابل مرتب‌سازی نیست. Delta هر گفتگو همچنان changes/nextCursor/hasMore و ChangeId دارد. limit آن پیش‌فرض ۱۰۰ و حداکثر ۲۰۰ است. MessageSequence/ChangeSequence در دامنهٔ Conversation ترتیب می‌دهند؛ زمان به‌تنهایی order key نیست.

typeهای جاری: MessageCreated، MessageEdited، MessageDeleted، ReactionChanged، PinChanged، ParticipantChanged، ConversationChanged، ReadCursorAdvanced، DeliveryCursorAdvanced، MessageHiddenForUser، ConversationUserStateChanged، PeerReadStateChanged، TopicChanged، PollChanged و ChecklistChanged. scheduled send پس از ثبت مانند MessageCreated است. Typing/Presence گذرا و خارج durable ledger/replay هستند.

ContextRevision قبل از delivery از نشست و مجوز تازه گرفته می‌شود. هر گروه خروجی حداکثر ۱۲۸ نشست با revision یکسان دارد؛ در حالت معمول fingerprint نشست‌ها متفاوت است و fanout تک‌نشستی می‌شود. این هزینهٔ امنیتی اندازه‌گیری SLO چند replica نیست. payload نه Message body دارد و نه مجوز fetch ایجاد می‌کند.

## ClientInvalidated افزایشی

RealtimeInvalidation دارای EventId، Key، ResourceId اختیاری، ContextRevision، StreamVersion، OccurredAtUtc، PayloadVersion=1 و TraceId اختیاری است. TraceId در صورت وجود Activity producer ذخیره می‌شود؛ تمام مسیر قدیمی Messaging trace پایدار تازه نگرفته است. StreamVersion stamp فنی Outbox است؛ ResourceVersion تجاری نیست. با event هیچ شمارنده/نمره/فرم را مستقیم افزایش یا overwrite نکنید.

Keyهای موجود: access، commerce، school.directory، school.guardians، school.roster، school.class، school.lesson، school.schedule، assessment.assignment، assessment.exam، assessment.submission، assessment.grade، assessment.exam-grade و assessment.exam-attempt. فهرست مخاطب دقیق در Handoff است. عنوان، متن، شماره، User target، Child، پاسخ، نمره، private note و موجودی مالی در notification نیستند. ResourceId فقط پس از مجوز فعلی منبع ارائه می‌شود؛ access hint خود فرد ResourceId ندارد.

## GET /api/v1/realtime/changes

Query: cursor اختیاری و limit پیش‌فرض ۱۰۰، حداکثر ۲۰۰. بدون cursor ابتدا boundary بگیرید و Snapshot بخوانید؛ پاسخ bootstrap تاریخچه را برنمی‌گرداند. پاسخ: Changes، NextCursor، HasMore، Watermark، Epoch، ContextRevision و RetryAfterMilliseconds اختیاری. هر AccountChangeResult، EventId/StreamVersion و یکی از Conversation یا Invalidation دارد.

Watermark و NextCursor opaque و محافظت‌شده‌اند؛ مقدار رمز‌شده را lexicographic مقایسه نکنید. Epoch فعلی outbox-v1 است. Cursor به User+Session+ContextRevision بسته است؛ جعل، کاربر/نشست/بافت دیگر، عمر بیش از ۲۴ ساعت، epoch یا keyring ناسازگار و عقب‌رفتن وضعیت DB، resync می‌خواهند. revision از rowversion نشست، Membership/Role و Guardian انتخاب‌شده ساخته می‌شود؛ تغییر Refresh نیز ممکن است resync لازم کند. کلیدها در fixture فقط حافظه‌ای‌اند و در host واقعی از Data Protection استاندارد می‌آیند.

## Cut، tail و تکرار

sourceها همان platform.OutboxMessages با MessageType=platform.realtime-invalidation.v1 و messaging.RealtimeOutbox/ChangeEvents هستند. جدول replay تازه، Migration یا Outbox موازی نداریم. MIN_ACTIVE_ROWVERSION مرز exclusive است تا transaction دیرCommitشده گم نشود. cut کوتاه Serializable قبل از authorization materialize/commit می‌شود. Network داخل تراکنش نیست.

claim/retry/completion، rowversion Outbox را تغییر می‌دهند؛ بنابراین EventId یکسان می‌تواند با StreamVersion تازه تکرار شود. Consumer بر EventId/Cursor دامنه deduplicate می‌کند و REST را مرجع می‌گیرد. پردازش exactly-once شبکه ادعا نمی‌شود.

اگر tail Commit‌شده جلوتر از مرز امن مشاهده شود، HasMore=true باقی می‌ماند و صفحهٔ کم‌پیشرفت RetryAfterMilliseconds=100 می‌دهد؛ صفحهٔ خالی پایان قطعی نیست. تا HasMore=false replay ادامه یابد. transaction باز طولانی می‌تواند پیشرفت cut را عقب بیندازد؛ Client باید با budget و backoff همان cursor را نگه دارد. خطای deadlock/SQL timeout به 503 امن با RETRY_SAME_CURSOR نگاشت می‌شود؛ خطا Success نمی‌شود.

## Authorization و revoke

SQL ابتدا scope و audienceهای واضح را محدود می‌کند؛ سپس مجوز جاری read use case دوباره اعمال می‌شود. Class roster فقط Manager همان سازمان یا Teacher منتسب؛ Student/Parent roster کامل نمی‌گیرند. Draft و پاسخ خصوصی Owner-only/Staff-only می‌مانند. GradeRelease فقط published projection مجاز را قابل refetch می‌کند. Grade/Evaluation readهای موجود مرجع Guardian و نمره‌اند؛ Notification مجوز جدید نیست.

مجوز زنده در scope تازه بررسی می‌شود؛ Tracking یا عضویت قدیمی SignalR کافی نیست. تغییر Context هنگام replay پاسخ موفق قدیمی نمی‌گیرد. Own access hint پس از revoke صرفاً برای refetch/پاک‌کردن cache است. منابعی که قبلاً به‌درستی دریافت شده‌اند خودکار از دستگاه حذف نمی‌شوند.

## خطاها

401 احراز موجود؛ 400 REALTIME_INVALID_QUERY؛ 409 REALTIME_RESYNC_REQUIRED با requiredAction=REFETCH_AUTHORIZED_SNAPSHOTS و retryable=false؛ 503 REALTIME_TEMPORARILY_UNAVAILABLE با requiredAction=RETRY_SAME_CURSOR و retryable=true؛ 429 سیاست realtime-replay با budget عادی ۱۲۰ درخواست در دقیقه برای حساب. محیط آزمون از FixedWindow موجود پروژه استفاده می‌کند. Cache-Control=no-store روی پاسخ replay است.

## مرز نگه‌داری

تلاش ارسال ReviewRequired/Blocked، MessageCreated یا binding پیام تولید نمی‌کند و از خطای 422 نباید event موفقیت، unread یا delivery محلی ساخت. شناسهٔ `safetyReference` در ProblemDetails برای ردیابی خود تلاش است؛ event عمومی پرونده/دلیل خصوصی یا endpoint release تازه نیست. [قرارداد BE-01](../messaging/BE01_REVIEW_REQUIRED_CONTRACT_FA.md).

کد فعلی Outboxهای منبع را خودکار پاک نمی‌کند. replay به حفظ رکوردهای منبع تکیه دارد؛ این تصمیم نگه‌داری ابدی یا SLA آفلاین نامحدود نیست. قبل از هر cleanup باید کف replay/epoch و resync رسمی طراحی شود. Multi-replica، backplane، keyring مشترک و Load/Storm/SLO عملیاتی در این پذیرش محلی اثبات نشده‌اند.
