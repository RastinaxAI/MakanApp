# ممیزی و شواهد تحویل Messenger P1

## فاز پنجم — رسانه، wire UTC و محدودسازی mutation

دو ایراد واقعی با تست red بازتولید شدند: `FileSignatureValidator` پایان نمونهٔ ۵۱۲ بایتی را EOF فرض می‌کرد و فارسی/emoji سالم را 415 می‌داد؛ همچنین `datetime2` در بازخوانی SQL، DateTimeKind را از دست می‌داد و receipt تکراری/history/Delta/search/media/state و SignalR زمان بدون Z می‌دادند. decoder اکنون فقط در EOF واقعی flush می‌شود؛ UTF-8 خراب داخل نمونه، NUL و انتهای واقعی ناقص همچنان مردودند. `MessagingWireTime` فقط Kind مقدارِ از قبل UTC را هنگام ساخت result بازیابی می‌کند؛ ToUniversalTime، زمان client، تغییر داده/ساعت یا migration وجود ندارد. Outbox realtime نیز همان timestamp UTC را با Kind درست می‌فرستد.

یک فرض اولیه دربارهٔ Shared Media **ایراد قابل بازتولید نبود**: تغییر فایل پیوست‌شدهٔ retained به Pending/Rejected/Deleted را `CK_FileAssets_RetentionState` با SQL 547 رد کرد. guard حذف/ضعیف نشد و fixture با SQL نامعتبر مجبور به عبور نشد. سه تست جدید همین constraint و باقی‌ماندن دو صفحهٔ سالم را اثبات می‌کنند. query Shared Media بازنویسی نشده است؛ paging چندپیوستی، ForMe، دسترسی خصوصی و پنج مسیر نوع رسانه با API/SQL واقعی تست شدند. فایل‌های signature fixture اثبات decoder یا malware-free بودن نیستند؛ AV/CDR آینده است.

Reply/Edit/Delete/Reaction/Forward/MessagePin موجود ممیزی شدند؛ source/destination authorization، history، retention، version، window یک‌ساعته، Safety و تطبیق snapshot پیش از Forward حفظ می‌شوند. بازنویسی این قواعد یا معماری لازم نشد. rate policy برای Send/Edit مشترک ۳۰۰، Reaction مشترک ۳۰۰ و Forward برابر ۱۲۰ در دقیقه/کاربر/process افزوده شد. Search=60، Upload=20 و device=60 قبلی حفظ شدند؛ پاسخ write/upload اکنون Retry-After را expose می‌کند. OTP/AI/payment quota و ترتیب authentication تغییر نکرده‌اند. محدودسازی توزیع‌شده یا ظرفیت Production ادعا نمی‌شود.

۸ Unit جدید (۴ case UTF-8 و ۴ signature رسانه) و ۱۸ Integration/contract شامل constraint، فایل خصوصی، UTC HTTP/SignalR، پنج نوع upload/send/download، صفحهٔ چندپیوستی و 429 اضافه شدند. red اولیهٔ Unit دو شکست سالم UTF-8 داشت؛ red Integration نخست سه خطای fixtureِ خلاف constraint و سه شکست واقعی داشت؛ پس از اصلاح fixture، ۳ passed/۳ failed واقعی ثبت شد. پس از fix، ۶ مورد اصلی و سپس focused گسترش‌یافتهٔ **۱۸ passed** و مجموعهٔ signature **۱۱ passed** شدند. تست یا constraint برای سبزکردن suite خاموش نشده است. full/final پس از اتمام اجرا ثبت می‌شود.

Git فاز provider پیش از شروع این تغییر: `4856b284c59cdb490913268803e6aa2aba05c36e`، `feat(notifications): deliver web push and fcm transports`؛ ۲۴ فایل مرتبط، push/fetch/ls-remote برابر HEAD و worktree تمیز. هشدار ignore پوشهٔ release مانع stage نسخهٔ tracked همان register نشد؛ staged list دقیقاً ۲۴ مسیر مصوب داشت و فایل AI/OTP وارد commit نشد.

Validation فاز پنجم: پس از focused/fix، restore و Build نهایی موفق با صفر warning/error؛ suite کامل نهایی **Unit=498، Integration=795، Architecture=14، Total=1307 passed، failed=0، skipped=0** در `media-full` اجرا شد. این فاز focused و یک اجرای کامل نهایی دارد، نه ادعای دو اجرای full جدا. EF pending-model check موفق و migration جدید لازم نبود. validation نهایی کل P1 دوبار در پایان پذیرش اجرا خواهد شد. هیچ دادهٔ عادی توسعه/سرور یا provider واقعی SMS/AI/Push در این فاز استفاده/تغییر نشد.

## فاز چهارم — transport استاندارد Web Push و FCM

`MessengerPushNotificationProvider` روی همان port ارسال موجود، Web و Android/Ios را route می‌کند؛ اعلان/Outbox/رجیستری دوم ساخته نشده است. `WebPushNotificationProvider` از `Lib.Net.Http.WebPush 3.3.1` با VAPID و رمزنگاری استاندارد استفاده می‌کند. auto-retry کتابخانه خاموش است؛ claim/retry/recovery همان SQL worker فاز دوم مالک تلاش‌هاست. `FcmPushNotificationProvider` HTTP v1 ثابت Google و OAuth با SDK رسمی `Google.Apis.Auth 1.73.0` دارد. نصب این دو وابستگی فقط در Infrastructure است؛ Domain/Application به SDK یا HTTP implementation وابسته نشده‌اند. restore و بررسی مستقیم/انتقالی NuGet از منابع تنظیم‌شده هیچ آسیب‌پذیری شناخته‌شده‌ای گزارش نکردند.

`GoogleFcmAccessTokenProvider` فقط فایل خارجی service_account همان پروژه و token URI ثابت را می‌پذیرد؛ scope Firebase Messaging و SDK cache/refresh استفاده می‌شوند. هیچ credential واقعی، کلید private، device token واقعی یا فایل service-account به Git اضافه نشده است. تنظیم‌های موجود appsettings، ملی‌پیامک/OTP و AI/RAG تغییر نکرده‌اند. GET configuration جدید فقط public VAPID key/وضعیت را به session مجاز می‌دهد؛ DTO ورودی دستگاه و delivery در ToString نیز secret ندارند.

`WebPushNetworkPolicy` علاوه بر allowlist HTTPS، در connect callback همهٔ پاسخ‌های DNS را عمومی بودن بررسی و socket را به همان IP وصل می‌کند؛ redirect، proxy و literal/private/loopback/link-local/transition/special IP مجاز نیستند. TLS validation استاندارد حفظ می‌شود. `WebPushResponseSanitizingHandler` بدنهٔ خطای provider را پیش از ورود به exception کتابخانه حذف می‌کند. FCM پاسخ bounded دارد؛ loggerهای HttpClient این transportها حذف شده‌اند. این کد جایگزین firewall/egress policy عملیاتی یا آزمون DNS محیط واقعی نیست.

`PushWirePayload` فقط شناسه‌های لازم و preview عمومی تولید می‌کند و حتی title/body/deepLink دلخواهِ delivery داخلی را بازتاب نمی‌دهد. navigation پیام `makan://conversations/{id}` است؛ نه frontend route ساخته‌شده و نه مجوز resource. tag/Topic/APNs collapse-id ثابت NotificationId کمک به dedupe می‌کنند، ولی exactly-once یا read اثبات نمی‌شود. worker کلید و platform واقعی همان registration ذخیره‌شده را به adapter می‌دهد؛ invalid-registration با همان محافظ rowversion revoke می‌شود. Auth/config failure token سالم را revoke نمی‌کند؛ retry موقت bounded و Retry-After محترم است. ضرب backoff پیش از saturation overflow نمی‌کند. TTL پیش‌فرض یک ساعت و حداکثر یک روز است؛ پذیرفته‌شدن Push امکان recall تضمینی ندارد.

۶۵ Unit/HTTP-component برای crypto/header، config، pair mismatch، privacy، error mapping، cancellation، سقف پاسخ/Retry-After و IP/handler policy اضافه شدند. نخست یک guard نادرست دربارهٔ AbsolutePath در URI نوع mailto باعث ۱۲ شکست تست جدید شد؛ guard با ساختار واقعی URI اصلاح شد و هر ۶۲ تست اولیه پاس شدند. سه case بازبینی برای 408/502/504 در token server نیز افزوده شدند تا خطای موقت OAuth به خطای دائمی credential تبدیل نشود؛ focused توسعه‌یافته ۶۵ passed است. ۱۲ Integration، رجیستری/API واقعی، Outbox و SQL worker را با composition واقعی Infrastructure و HTTP کنترل‌شده برای هر سه platform آزمایش می‌کنند؛ revoke/retry/auth failure و منع credential نوع/پروژه/endpoint نامعتبر هم بررسی می‌شوند. یک شکست 401 مربوط به token host آزمایشی دیگر با ایجاد session صحیح همان host در setup رفع شد؛ authentication ضعیف نشد. focused SQL نهایی ۱۲ passed است.

Git رجیستری پیش از این فاز: `a59bb740a06c921dd491e2a865737c652e896ed6`، `feat(notifications): add web push and mobile registrations`؛ ۲۱ فایل مرتبط، push/fetch/ls-remote موفق و worktree تمیز بود. Validation کامل transport پس از اتمام اجرا ثبت می‌شود. وضعیت خارجی فعلی **P1_PUSH_RUNTIME_PENDING**؛ Web/device واقعی، دریافت background، click و APNs روی محیط واقعی مشاهده نشده‌اند و REAL_PUSH_PASS ادعا نمی‌شود.

Validation transport: restore موفق؛ full و final اولیه هر دو Unit=487، Integration=777، Architecture=14 و Total=1278 passed بودند. پس از سه case تکمیلی token-server، Build نهایی صفر warning/error و suite کامل بازبینی‌شده **Unit=490، Integration=777، Architecture=14، Total=1281 passed، failed=0، skipped=0** بود. شواهد: `push-full`، `push-final`، `push-oauth-reviewed` و `push-final-reviewed` زیر `.artifacts/messenger-p1/tests`. EF pending-model check موفق است؛ این فاز migration تازه ندارد. هیچ پیامک، provider خارجی واقعی، سرور یا دیتابیس معمول توسعه در این تست‌ها تماس/تغییر داده نشده است.

## فاز سوم — گسترش رجیستری موجود

`PushDeviceService` ورودی Web Push و نسخه را validate می‌کند و `EfNotificationPreferenceStore.Devices` ثبت را در تراکنش Serializable با UPDLOCK/HOLDLOCK روی hash مقصد انجام می‌دهد. token فعال بین حساب‌ها منتقل نمی‌شود؛ refresh هم‌حساب و retry هم‌زمان شناسهٔ ثابت دارند. تغییر کلید Web به rowversion جاری نیاز دارد. رجیستری جدید یا کپی Identity/Session ساخته نشده است.

Controller موجود اکنون list خصوصی و POST web-push دارد؛ POST موبایل/DELETE قبلی حفظ شده‌اند. list فقط metadata امن و زمان UTC می‌دهد. پیکربندی تازه فقط allowlist عمومی host مقصد است؛ appsettings، OTP، AI و سرور تغییر نکرده‌اند. اعتبارسنجی endpoint HTTPS و کلید P-256 در Application است؛ حفاظت DNS/socket و ارسال استاندارد رمز‌شده متعلق به فاز provider بعدی‌اند و هنوز ادعا نمی‌شوند.

Migration `20261016081315_ExtendPushDeviceSubscriptions` دو ستون nullable `varchar(87)` و `varchar(22)` و CHECK هماهنگی platform/کلیدها را به جدول موجود اضافه می‌کند. Web قدیمیِ بدون کلید باقی می‌ماند؛ re-register لازم است. PK/FK، unique active token hash و rowversion موجود reuse شده‌اند. Down دادهٔ کلیدها را حذف می‌کند؛ اجرای کورکورانهٔ آن توصیه نمی‌شود. هیچ migration روی دیتابیس معمول توسعه یا سرور اعمال نشده است.

۱۰ Unit برای endpoint/کلید و ۹ Integration برای CRUD، حریم خصوصی، دو نشست واقعی یک حساب، race موبایل/Web، rotation با نسخه، pagination، پاسخ throttling و OpenAPI اضافه شده‌اند. focused نخست ۸ passed و اجرای قبلی به همراه ۹ regression موجود ۱۵ passed بود. fixture اولیه Base64 معمولی را به Base64url اصلاح کرد؛ سپس assert دقیق نقطهٔ خارج منحنی نشان داد Windows به‌جای CryptographicException، PlatformNotSupportedException می‌دهد. مسیر production این خطای محدود را به validation تبدیل می‌کند، نه bypass؛ یک agreement واقعی نیز import تنبل را می‌سنجد. ورودی نقطهٔ نامعتبر باید HTTP 400 بدهد، نه 500. اجرای Unit اصلاح‌شده ۱۰ passed است. نتیجهٔ full/final جدا پس از اجرا ثبت می‌شود.

Validation فاز سوم: restore موفق؛ full نخست Unit=425، Integration=764، Architecture=14 و مجموع 1203 passed داشت. پس از افزودن تست throttling و دقیق‌کردن تست منحنی، شکست Unit بالا دیده و رفع شد؛ اجرای قدیمی آن suite متوقف و suite کامل دوباره اجرا شد. نتیجهٔ نهایی **Unit=425، Integration=765، Architecture=14، Total=1204 passed، failed=0، skipped=0**، در `registration-final-fixed` است. Build نهایی صفر warning/error و EF pending-model check موفق بود. یک build زودهنگام هنگام اجرای testhost به قفل DLL ویندوز با MSB3027 برخورد کرد؛ بدون تغییر امنیت/نسخه، پس از پایان تست و با اجرای ترتیبی رفع شد. شواهد شکست‌ها حذف نشده‌اند و به‌جای validation موفق گزارش نمی‌شوند.

Git فاز دوم که پیش از شروع رجیستری بسته شد: `90fea9b78bfe5f76e63e2eeff52b11b8048f5038` با پیام `feat(notifications): harden messenger delivery recovery`؛ push و fetch/ls-remote موفق و remote برابر HEAD بود. commit فاز سوم پس از review همین validation انجام می‌شود؛ ارسال خارجی واقعی همچنان تأیید نشده است.

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

Git فاز اول: `8becc1f3dd90643faa6f214cdae7042cd6be0d68`، `feat(messaging): add peer read receipt contract`؛ ۱۸ فایل مرتبط، push عادی به origin/main موفق و fetch/ls-remote برابری remote با HEAD را تأیید کردند. worktree پس از push تمیز بود.

## فاز دوم — پایداری و مجوز جاری اعلان خارجی

`NotificationDeliveryProcessor` همان worker قبلی است، نه subsystem دوم. پیش از هر handoff خارجی، preference همان category/scope، عضویت/سازمان، Mute فعلی و mute-until، Quiet Hours و حساب/نشست/دستگاه جاری بررسی می‌شوند. برای پیام، reference واقعی Message، participant فعال، FullHistory/FromJoin، Clear/ForMe/deletion، گروه رسمی از منابع کلاس، Block/Privacy Direct و رابطهٔ محافظت‌شده جاری لازم‌اند. context انتخابیِ tab را به مجوز Push تبدیل نکردیم؛ دستگاه به صاحب حساب و session خودش متصل است و مجوز عضویت منبع مستقلاً سنجیده می‌شود.

Mute و منع delivery پیام، unread، اعلان داخلی، Delta یا SignalR را حذف نمی‌کنند. Mention/Reply override دلخواه برای Mute ساخته نشد. preview فعلی برای همه، از جمله Minor/Unknown، همان متن عمومیِ بدون متن پیام/مدرسه/رابطه/سن است. بررسی قبل از handoff، تضمین پس‌گرفتن Push قبلاً پذیرفته‌شده در شبکه نیست.

هر دور حداکثر BatchSize را پردازش می‌کند، اما claim درست قبل از هر item گرفته و بعد از authorization دوباره با همان ClaimToken تثبیت می‌شود؛ ۵۰ درخواست شبکه دیگر همگی lease سی‌ثانیه‌ای یکسان از ابتدای batch ندارند. ClaimLease باید از ProviderTimeout بیشتر باشد. انقضای claim بعد از crash، از SQL و scope تازه بازیابی می‌شود. claim رهاشده در سقف تلاش، بدون ارسال چهارم به Failed می‌رود. cancellation واقعی worker، claim پایدارِ قابل بازیابی باقی می‌گذارد و موفقیت جعلی نمی‌سازد.

retry شبکه bounded و exponential است؛ RateLimited حداقل یک دقیقه فاصله دارد و Retry-After provider رعایت می‌شود. Quiet Hours تازه برای اعلان از قبل queued، بدون مصرف بودجهٔ تماس provider Deferred می‌شود. مقصد permanent-invalid، در transaction ثبت نتیجه لغو می‌شود؛ فقط اگر rowversion همان registration ارسال‌شده هنوز جاری باشد، بنابراین پاسخ قدیمی provider، subscription تازه‌شده را revoke نمی‌کند. قید unique اعلان/کانال/مقصد موجود، dedupe پیام/گیرنده و transaction پیام بازنویسی نشده‌اند.

Migration `20261016074318_HardenNotificationDeliveryRecovery` فقط CHECK علت خطای delivery را از ۱–۸ به ۱–۱۳ گسترش می‌دهد؛ schema جدید و backfill ندارد. علت‌های تازه: InvalidRegistration، ProviderAuthenticationFailed، ProviderRateLimited، ProviderConfigurationInvalid و ResourceAccessRevoked. Delivered در مدل delivery قدیمی یعنی پذیرش provider، نه دریافت دستگاه یا read انسان؛ این فاز دریافت واقعی Push را ثابت نمی‌کند.

تست‌های این فاز: دو Unit خالص state و ۱۹ Integration برای تغییر state پس از schedule، لغو عضو Group/Channel، replay/restart در زیر/روی سقف، cancellation، invalid destination، رقابت refresh، Retry-After و preview Minor. چهار شکست نخست focused مربوط به `DateTimeKind.Unspecified` برگشتی SQL در setup ساعت تست بودند؛ با بازشناسی صریح UTC فقط در setup اصلاح شدند، نه حذف اعتبارسنجی Domain. اجرای میانیِ ۱۱ case جدید به همراه ۹ regression ترجیحات، ۲۰ passed داشت؛ focused توسعه‌یافته ۱۹ passed و دو Unit هم موفق‌اند.

Validation فاز دوم: restore موفق؛ full و final هر دو **Unit=415، Integration=756، Architecture=14، Total=1185 passed، failed=0، skipped=0**. هر دو build صفر warning/error؛ شواهد `worker-full` و `worker-final` زیر `.artifacts/messenger-p1/tests`. EF pending-model check موفق و Designer فقط metadata نام/شناسه و CHECK علت خطا را نسبت به migration قبلی تغییر داد. هیچ provider واقعی، SMS، AI یا سرور در این validation تماس/تغییر نگرفت.
