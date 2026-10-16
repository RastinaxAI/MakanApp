# ممیزی و شواهد تحویل Messenger P1

## مبنا و مرز

شروع ۲۰۲۶-۱۰-۱۶ از `52181b6dda4b2c002a236c4c6f3aa193161cdaa9`، برابر `origin/main` پس از fetch، در worktree تمیز `D:\Project\MakanApp\MakanApp-Backend-Messenger-P0`. شاخهٔ همین worktree `messenger/p0` و upstream موجود `origin/main` حفظ می‌شوند؛ برای commitهای P1 از push عادی `HEAD:main` استفاده می‌شود. workspace اصلی دارای کار commit‌نشدهٔ AI بازنویسی، reset، stash، clean یا منتقل نمی‌شود.

منابع: AGENTS؛ کد/تست/migration جاری؛ Master PRD فارسی v3 مورخ ۲۰۲۶-۱۰-۱۶؛ تصمیم‌های مصوب Messenger؛ GAP-05؛ سه سند P0؛ راهنمای API و register معوق. PRD هدف است، نه اثبات implementation. هیچ frontend، سرور، provider ملی‌پیامک یا AI/RAG در محدودهٔ تغییر نیست.

Baseline واقعی: restore و build موفق، صفر warning/error؛ Unit=413، Integration=726، Architecture=14، مجموع **1153 passed، failed=0، skipped=0**. شواهد: `.artifacts/messenger-p1/tests/baseline`. SQL فقط دیتابیس‌های اختصاصی fixture، نه پایگاه معمول توسعه یا Production.

## P1-0 — وضعیت پیش از تغییر

| حوزه | وضعیت مبنا | شاهد / شکاف |
|---|---|---|
| Read / Delivered خود کاربر | IMPLEMENTED | `ConversationParticipant`، `EfMessagingStore.Sync`؛ monotonic و خصوصی |
| Peer Read / B04 | MISSING | وضعیت و event عمومیِ امن برای خواندن توسط طرف مقابل وجود ندارد |
| ChangeEvent / Delta / reconnect | IMPLEMENTED | cursor گفتگو-bound، visibility جاری و Outbox پایدار P0 |
| SignalR audience | IMPLEMENTED | مجوز دقیق event از Delta، session group؛ نه broadcast عمومی private state |
| اعلان پیام و dedupe | IMPLEMENTED | `EnqueueMessageNotificationsAsync`، Outbox و Notification یکتای پیام/گیرنده؛ Mention > Reply > MessageReceived |
| Preference / Quiet Hours | IMPLEMENTED | `NotificationPreferenceResolver` و policy موجود؛ In-app مستقل |
| Mute در delivery | PARTIAL | suppression زمان تولید موجود است؛ re-evaluation در worker خارجی وجود ندارد |
| ثبت دستگاه | PARTIAL | `PushDeviceRegistration` و POST/DELETE موجود؛ Web Push keys و فهرست خصوصی دستگاه نداریم |
| Web Push / VAPID | MISSING | provider استاندارد واقعی وجود ندارد |
| FCM HTTP v1 | MISSING | provider واقعی وجود ندارد؛ Development/Unavailable معادل FCM نیست |
| دریافت واقعی Push | RUNTIME_PROVIDER_REQUIRED | نیازمند credential خارجی و subscription/device واقعی؛ تست محلی شاهد آن نیست |
| Worker اعلان خارجی | PARTIAL | batch/lease/retry موجود؛ reauthorization، invalid-device و recovery سقف تلاش باید بررسی/تکمیل شوند |
| FileAsset / upload / private download | IMPLEMENTED | Storage موجود و bound access؛ MIME/extension/signature و مجوز جاری، نه AV/CDR |
| Shared Media | IMPLEMENTED | query مجاز، history boundary و paging؛ regression تکمیلی لازم است |
| Reply / Edit / Delete | IMPLEMENTED | reference امن، یک‌ساعت سرور، rowversion، ForMe/Everyone مستقل |
| Reaction / Forward / Message Pin | IMPLEMENTED | مجوز منبع/مقصد و Safety؛ permission جاری و regression موجود |
| Archive / Conversation Pin / Mute | IMPLEMENTED | `ConversationUserState` خصوصی؛ پیام جدید archive را خودکار باز نمی‌کند |
| Manual Unread | IMPLEMENTED | state نمایشی، بدون عقب‌بردن cursor واقعی؛ MarkRead آن را پاک می‌کند |
| Search | IMPLEMENTED | SQL bounded و cursor، visibility جاری؛ بدون موتور موازی |
| چنددستگاهی | PARTIAL | persistence مشترک و private delta موجود؛ پذیرش دو session مستقل برای stateهای P1 تکمیل می‌شود |
| Child Safety | IMPLEMENTED | GAP-05 و Internal Makan، Unknown محافظت‌شده، fail-closed و منع guardian/manager private browsing |

Typing، Presence/LastSeen، Topics، Poll پیشرفته، Scheduled، Calls/Video/Live/Stories، Bots/Mini Apps/Premium/Monetization، SMS عمومی، AV/CDR، backplane و rate limiter توزیع‌شده خارج این P1 هستند.

## فاز اول — Peer Read

`GET /api/v1/conversations/{conversationId}/read-states` فقط پس از احراز نشست و مجوز خواندن جاری، `ConversationPeerReadStateResult` می‌دهد. برای Direct، sequence آخرین پیام خود caller که peer واقعاً read کرده و هنوز برای هر دو قابل مشاهده است برمی‌گردد. قبل از read مقدار Visible/0 است؛ Delivered، SignalR و read خود فرستنده شاهد peer نیستند. Group/Channel/Saved، `NotSupported/null` دارند و فهرست per-member ایجاد نشده است.

محافظه‌کاری privacy: دو participant فعال، حساب معتبر، نبود Block، privacy فعلی هر دو جهت Personal و eligibility جاری لازم‌اند؛ در نبود آن‌ها `Hidden` و sequence/version خالی است و علت خصوصی توضیح داده نمی‌شود. پایان رابطه یا تغییر سن، history موجود را حذف نمی‌کند ولی رسید جدید/بازپخش آن را مجاز نمی‌کند. Clear/ForMe/deletion و history boundary هر دو طرف اعمال می‌شوند؛ Guardian یا Manager راه میان‌بُر ندارند.

در تراکنش read، cursor خصوصی موجود حفظ و فقط در صورت پیشروی واقعی روی پیام peer، ChangeEvent جدا با `PeerReadStateChanged=12`، AudienceUserId اختصاصی فرستنده و reference همان Message ساخته می‌شود. sequence از Message پایدار استخراج می‌شود؛ تاریخ فعالیت مستقل peer، phone، سن یا فهرست اعضا در DTO نیست. Outbox همان transaction است. SignalR موجود `ConversationChanged` با type جدید و `peerReadThroughSequence` nullable را به sessionهای مجاز می‌دهد؛ private `ReadCursorAdvanced` همچنان self-only است. Delta همان fact را recover می‌کند و دریافت event به‌تنهایی مجوز جلو بردن checkpoint نیست.

نسخهٔ receipt یک نشانگر opaque برای همان fact است، نه rowversionِ قابل استفاده در write. POST read موجود ExpectedVersion نمی‌گیرد؛ حداکثر monotonic در transaction/قفل Conversation enforce می‌شود. conflict نسخه برای Edit/settingهای موجود بدون تغییر باقی است.

Migration `20261016072600_AddDirectPeerReadReceipts` فقط CHECK موجود `CK_ChangeEvents_Type` را برای مقدار ۱۲ گسترش می‌دهد؛ ستون/جدول/داده/Default جدید ندارد. Down با وجود ردیف type=12 نیازمند برنامهٔ سازگار داده‌ای است و rollback کور توصیه نمی‌شود.

تست جدید `MessagingP1ReadReceiptEndpointsTests.cs`: ۱۱ case محلی برای accepted/delivered/own-read، SignalR واقعی TestServer، reconnect/Delta، monotonic/retry، Block، Unknown/Minor/Privacy/عضو پایان‌یافته، ForMe، outsider، Group/Channel و OpenAPI. تست نخست بعد از اصلاح signature helper، نبود route را با 404 نشان داد؛ اجرای focused پس از implementation **11 passed** بود. نتیجهٔ full/final پس از اجرای واقعی در همین سند افزوده می‌شود؛ این بخش ادعای اتمام P1 یا Push واقعی نیست.

Validation فاز اول: restore موفق؛ build اولیه و نهایی صفر warning/error؛ هر دو اجرای full و final برابر **Unit=413، Integration=737، Architecture=14، Total=1164 passed، failed=0، skipped=0** بودند. شواهد در `read-full` و `read-final` زیر `.artifacts/messenger-p1/tests` قرار دارند. EF pending-model check موفق بود. migration از ابتدا در LocalDB ایزوله fixture اجرا شد؛ به دیتابیس عادی یا سرور اتصال/مهاجرت انجام نشد. مقایسهٔ Designer جدید با قبلی فقط نام/شناسه migration و CHECK جدید را نشان داد؛ جابه‌جایی ترتیب index یکسان در snapshot اثر مدل/DDL ندارد.
