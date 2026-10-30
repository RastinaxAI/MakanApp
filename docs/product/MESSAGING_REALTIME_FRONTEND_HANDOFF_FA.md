# قرارداد اتصال Frontend به Backend Realtime جاری

این سند Handoff قرارداد Backend تأییدشدهٔ بستهٔ مشترک Realtime و Academic Attachments است؛ شاهد استقرار روی سرور نیست. Frontend تغییر نکرده است. احراز موجود Bearer در Authorization و نشست معتبر لازم است؛ Token را در query، log یا اسناد قرار ندهید. API پایه /api/v1 و Hub همان /hubs/messaging است.

## Contact و Direct

PeopleController در POST /api/v1/people/resolve هویت مجاز را با username یا phoneNumber دقیق resolve می‌کند. POST /api/v1/contacts دفترچهٔ خود کاربر را ذخیره می‌کند. برای POST /api/v1/conversations/direct از Profile.UserId/ContactUserId استفاده کنید، نه ContactId/PersonId/MembershipId. شماره‌های معادل یک Account می‌دهند؛ discovery و consent را از CanStartDirect و پاسخ واقعی بخوانید.

Personal نیازمند School Role نیست. Context را با API موجود Workspace صریح انتخاب کنید؛ OrganizationId و Role از Client اثبات مجوز نیستند. Contacts audience مربوط به دفترچهٔ مقصد است. Nobody، Block، حساب/سن/رابطه و Safety همچنان enforce می‌شوند. 404 عمومی DIRECT_RECIPIENT_NOT_AVAILABLE علت خصوصی را افشا نمی‌کند.

GET /conversations/{id} مسیر مدیریت Group/Channel است. برای Direct/Saved و دیگر گفتگوهای مجاز از GET /conversations/{id}/summary و برای history از /messages استفاده کنید. Summary به صفحهٔ اول یا Archived وابسته نیست؛ 404 آن هنوز می‌تواند concealment باشد.

## شروع امن و Snapshot race

1. listenerهای ConversationChanged و ClientInvalidated را نصب و Hub را وصل کنید. Session group خودکار است؛ برای پیام اول نیاز به بازکردن قبلی گفتگو یا SubscribeConversation نیست.
2. GET /api/v1/realtime/changes بدون cursor را بگیرید؛ Changes خالی در این پاسخ فقط bootstrap است. NextCursor و ContextRevision را نگه دارید.
3. Snapshotهای مجاز را، شامل همهٔ صفحه‌های موردنیاز Inbox، بگیرید. notificationهای همزمان را در buffer محدود نگه دارید.
4. از NextCursor، GET /api/v1/realtime/changes?cursor=...&limit=100 را تا HasMore=false ادامه دهید؛ RetryAfterMilliseconds را رعایت کنید. EventIdهای تکراری را حذف و queryهای مربوط را refetch کنید.
5. پس از تأیید ContextRevision پاسخ، Snapshot را اعمال کنید. پیام زنده و replay cursor یکی نیستند؛ با رسیدن push، cursor بازیابی را جلو نبرید.

اگر cut موقتاً عقب باشد، صفحهٔ خالی با HasMore=true و RetryAfterMilliseconds=100 ممکن است. این حالت پایان replay نیست. بعد از قطع اتصال، همان cursor ذخیره‌شده را replay کنید؛ reconnect به‌تنهایی recovery نیست. cursor conversation قدیمی با cursor حساب قابل جایگزینی نیست.

## رفتار idempotent و version-aware

EventId ثابت و Consumer idempotent است؛ شبکه exactly-once نیست. StreamVersion ترتیب انتقال همان رکورد فنی است و با claim دوباره عوض می‌شود. ResourceVersion/Sequence در دامنهٔ خودش و نسخهٔ پاسخ REST مرجع‌اند. اعلان را patch نمره یا افزایش +1 Unread فرض نکنید؛ notification فقط invalidation است.

پس از پیام، summary مجاز را refetch و بر اساس ConversationId در map جایگزین کنید. Unread/Preview/Order را از پاسخ بگیرید. فیلتر Direct/Group/Archived و paging باید در Query سرور حفظ شوند؛ event خارج فیلتر را مستقیم به صفحه تزریق نکنید. یک کلید در دو صفحه دو Conversation نمی‌سازد. ACK گم‌شدهٔ Send را با همان clientMessageId و payload یکسان retry/reconcile کنید.

ConversationChanged اکنون ContextRevision همان نشست را نیز دارد. رویداد با revision قدیمی را به Context تازه اعمال نکنید. تغییر Workspace/Child/Account، درخواست‌های pending و buffer قبلی را با generation محلی جدا می‌کند. این مرحله تمام پاسخ‌های قدیمی REST را globally برچسب نزده است؛ Client باید پاسخ دیررس درخواست قبلی را نیز کنار بگذارد. فرم باز را با refetch بی‌صدا overwrite نکنید؛ expectedVersion/RowVersion و 409/412 جاری حفظ‌اند.

## خطا و backoff

| نتیجه | اقدام |
|---|---|
| 401 | نشست را طبق جریان موجود بازیابی/ورود کنید؛ cursor مجوز ورود نیست |
| 409 REALTIME_RESYNC_REQUIRED | بافت معتبر را bootstrap و Snapshot مجاز تازه بگیرید؛ buffer قدیمی را اعمال نکنید |
| 503 REALTIME_TEMPORARILY_UNAVAILABLE | retryable=true و requiredAction=RETRY_SAME_CURSOR؛ با backoff همان cursor را نگه دارید |
| 429 | Retry-After موجود را رعایت کنید؛ حساب را با loop بدون مکث poll نکنید |
| 400 REALTIME_INVALID_QUERY | limit باید ۱ تا ۲۰۰ باشد؛ ورودی را اصلاح کنید |

روی resync، requiredAction=REFETCH_AUTHORIZED_SNAPSHOTS و retryable=false است؛ mutation قبلی را کورکورانه دوباره نسازید. cursor حداکثر ۲۴ ساعت، وابسته به نشست/بافت و opaque است. Refresh نشست، تغییر رابطه/Role یا keyring می‌تواند resync معتبر ایجاد کند. هیچ cursor، Token، payload خصوصی یا Header احراز در log نباشد. پاسخ replay Cache-Control=no-store دارد.

## کدام event چه چیزی را refetch می‌کند؟

| Key | Queryهای مربوط و مرز مخاطب |
|---|---|
| access | Bootstrap/Capabilities/Workspace و cacheهای وابستهٔ خود شخص؛ بدون افشای علت Block یا شناسهٔ Child |
| school.directory / school.guardians | فهرست مدیریتی همان سازمان برای Manager؛ roster کامل Student/Parent باز نشده |
| school.roster | roster همان کلاس برای Manager/Teacher منتسب |
| school.class / school.schedule | Class، Calendar/Dashboard مجاز همان کلاس |
| school.lesson | Staff برای Draft؛ Student فقط Published و Enrollment فعال؛ Parent مجوز تازه نگرفته |
| assessment.assignment / assessment.exam | فهرست/جزئیات مجاز؛ Draft و AnswerKey در اعلان نیستند |
| assessment.submission / assessment.exam-attempt | پاسخ/تلاش متعلق به خود فرد؛ evaluator فقط طبق read use case مجاز |
| assessment.grade / assessment.exam-grade | Grade فقط پس از release برای Student/Guardian مجاز؛ private note هرگز در event نیست |
| commerce | Summary/Quota صاحب Individual یا Manager مجاز همان Organization؛ نه مالی میزبان/اعضای دیگر |

Parent صرف GuardianRelation به چت خصوصی کودک دسترسی ندارد. Role یا Subscription مجوز خواندن منبع تازه نمی‌سازد. بعد از revoke، دریافت آینده مجوز جاری را دوباره می‌سنجد. اطلاعاتی که قبلاً دریافت شده‌اند با قطع ارتباط از حافظهٔ دستگاه پس گرفته نمی‌شوند؛ cache وابسته باید با access hint یا خطای جاری پاک/غیرفعال شود.

## فایل و رسانه

Upload موفق، receipt ارسال پیام نیست. Ready فایل، آماده‌بودن byte storage است؛ Safety، Attach و Download مجوزهای مستقل‌اند. طبق تصمیم مالک، رسانهٔ Minor/Unknown فعلاً ReviewRequired می‌ماند؛ UI نباید آن را Sent نشان دهد. بستن Moderation تأیید ارسال فایل نیست و مسیر release رسانه هنوز تصمیم باز است.

برای 422های Safety، `safetyState` برابر PendingReview یا Rejected و `operationState=NotApplied` است. `newMessageCreated`، `newAttachmentsBound`، `recipientDeliveryGranted` و `recipientDownloadGranted` همگی false هستند. `requiredAction=KEEP_DRAFT_NO_AUTO_RETRY` و `retryable=false`: پیش‌نویس حفظ شود، retry خودکار و نمایش Sent/Delivered/دانلود گیرنده ساخته نشود. `safetyReference` ثبت‌شده در retry همان payload پایدار می‌ماند؛ correlationId هر درخواست مستقل است. PendingReview نه Approved/Attachable است و نه وعدهٔ صف انسانی یا تکمیل async. [قرارداد و تصمیم باز BE-01](../messaging/BE01_REVIEW_REQUIRED_CONTRACT_FA.md).

GET /files/{fileId}/content بایت خام/Blob می‌دهد. 200 کامل و 206 Range معتبر روی stream قابل seek است؛ Content-Type واقعی، Content-Disposition و Content-Range را بخوانید. خطای application/problem+json را به‌عنوان فایل ذخیره نکنید. fileId یا لینک قدیمی مجوز نیست؛ دسترسی در هر دانلود بازبینی می‌شود.

پیش‌فرض Storage شامل PDF/JPEG/PNG/Text و حداکثر ۱۰ MiB است؛ allowlist محیط مرجع است و در این مرحله تغییر نکرد. MP4 فقط با MIME مجاز و ftyp معتبر؛ validator فعلی تضمین Codec/پخش H.264 ندارد و WebM را تأیید نمی‌کند. پذیرش ویدئو/codec واقعی و اسکن رسانهٔ کودک را PASS فرض نکنید.

## Deferred

AI stream/runtime، Push واقعی دستگاه، چند replica/backplane، UI render/فرم باز و storm/SLO عملیاتی اینجا پذیرفته نشده‌اند. Event زمان‌محور پایان Grace بدون Write جدید ساخته نشده؛ از PeriodEndsAt/GraceEndsAt فقط برای زمان‌بندی refetch استفاده کنید و مجوز را از سرور بگیرید.

## قرارداد نهایی حساب و گفتگو

| مسیر/رویداد واقعی | قرارداد |
|---|---|
| `GET /api/v1/realtime/changes?cursor=...&limit=100` | `AccountChangePageResult`: `changes`، `nextCursor`، `hasMore`، `watermark`، `epoch`، `contextRevision` و `retryAfterMilliseconds` اختیاری؛ limit بین ۱ و ۲۰۰ |
| `AccountChangeResult` | `eventId`، `streamVersion` و یکی از `conversation` یا `invalidation`؛ `EventId` مرجع dedup حساب است |
| `ClientInvalidated` در Hub | `RealtimeInvalidation`: `eventId`، `key`، `resourceId` اختیاری، `contextRevision`، `streamVersion`، `occurredAtUtc`، `payloadVersion=1` و `traceId` اختیاری |
| `ConversationChanged` در Hub | `MessagingRealtimeNotification` موجود، به‌همراه ContextRevision نشست؛ reconnect حساب، جای delta گفتگو را حذف نمی‌کند |
| `GET /api/v1/conversations/{conversationId}/changes?afterCursor=...&limit=100` | `ConversationChangePageResult`: `changes`، `nextCursor`، `hasMore`؛ هر change دارای `changeId` و cursor همان گفتگو است |

epoch فعلی حساب `outbox-v1` است. StreamVersion نسخهٔ انتقال/rowversion Outbox است؛ با claim/retry می‌تواند تغییر کند. ResourceVersion و cursor/sequence گفتگو را با آن مقایسهٔ سراسری نکنید. ContextRevision فقط برای برابری است. duplicate در account replay با EventId و در conversation delta با ChangeId شناسایی می‌شود؛ unread را از پاسخ authoritative جایگزین کنید و افزایش محلی تکراری نسازید.

دادهٔ حساب به User/Session/Context بسته است؛ چند نشست مجاز همان User می‌توانند event دریافت کنند ولی cursor یکدیگر را reuse نمی‌کنند. در تغییر بافت، snapshot و buffer قبلی کنار گذاشته می‌شوند. برای خطای resync، bootstrap و snapshot مجاز تازه لازم است؛ reconnect ساده یا replay cursor گفتگو در endpoint حساب کافی نیست.

برای پیوست آموزشی، پس از `assessment.submission` یا `assessment.exam-attempt` مجاز، REST read مربوط را refetch کنید. ready شدن storage به‌تنهایی به معنی Attach یا Submit نیست. [FRONTEND_HANDOFF پیوست‌ها](ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md#frontend_handoff) مسیر کامل Upload/Ready/Attach/Detach/Save/Reload/Finalize/Download را مشخص می‌کند. Raw attachment برای Guardian مجوز تازه ندارد و معلم revoke‌شده با event قدیمی مجوز دانلود نمی‌گیرد.

پذیرش تازهٔ پیش از Commit: Restore موفق؛ Release Build با صفر Warning/Error؛ Unit=922، Integration=1542، Architecture=14، مجموع 2478 موفق، Failed=0 و Skipped=0. ۵۲ تست Academic جدید، ۵۹ تست با عنوان Realtime و ۹۵ تست با عنوان Commerce/Commercial/Subscription/Entitlement/Quota در همین suite موفق‌اند. شش E2E مستقل با ۱۴۱ HTTP و تطابق SHA-256 فایل‌ها اجرا شد. این نتیجه پذیرش Backend محلی است؛ UI یا سرور مستقر را اثبات نمی‌کند. Pending Model Changes وجود ندارد. نتیجهٔ پس از Commit جداگانه با همان HEAD بررسی می‌شود.
