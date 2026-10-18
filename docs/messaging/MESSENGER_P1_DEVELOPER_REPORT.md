# گزارش آموزشی توسعه‌دهنده — Messenger P1

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

تاریخ: ۲۰۲۶-۱۰-۱۶. این گزارش فقط Backend موجود و تست‌های اجراشده را توضیح می‌دهد. مرجع جزئی API و تنظیم‌ها [Handoff P1](MESSENGER_P1_FRONTEND_HANDOFF.md)، شواهد مرحله‌ها و پذیرش T01 تا T35 [ممیزی](MESSENGER_P1_DELIVERY_AUDIT.md) و قرارداد گسترده‌تر [راهنمای API](../backend-api-guide.md) است. دریافت خارجی Push هنوز **P1_PUSH_RUNTIME_PENDING** است؛ هیچ deploy، دستگاه فیزیکی یا frontend ساخته/تغییر داده نشده است.

## ۱. خلاصهٔ مرحله

مبنای شروع `52181b6dda4b2c002a236c4c6f3aa193161cdaa9` و ۱۱۵۳ تست موفق بود. P0، ارسال/تاریخچه/Delta، state شخصی، فایل، Child Safety و اعلان داخلی را داشت، اما قرارداد Peer Read، provider واقعی Push و بخشی از reauthorization/recovery اعلان خارجی و شواهد دو-session کامل نبودند. اکنون Peer Read محدود Direct، رجیستری توسعه‌یافتهٔ Web/Android/Ios، transport استاندارد Web Push/VAPID و FCM HTTP v1، worker بازبینی‌شده و پذیرش محلی چنددستگاهی موجودند. دو ایراد بازتولیدشدهٔ UTF-8 و UTC اصلاح و rate limit mutationها تکمیل شدند.

یک تیک فقط receipt پایدار `Sent` سرور است؛ دو تیک فقط fact مجاز Peer Read برای پیام خود فرستنده است. Delivered خصوصی، Push پذیرفته‌شده و SignalR به‌خودی‌خود read انسان نیستند. Group/Channel/Saved، receipt per-member ندارند و `NotSupported` می‌دهند.

ماتریس دامنهٔ تحویل:

| حوزهٔ P1 | نتیجه و مرز |
|---|---|
| P1-0 / P1-1 | baseline ممیزی شد؛ read-state، event اختصاصی و recovery محدود به مجوز جاری |
| P1-2 / P1-3 / P1-4 | رجیستری موجود + Web Push/VAPID + FCM HTTP v1 در کد و تست transport محلی |
| P1-5 / P1-6 / P1-7 | preferences، Mute، Quiet Hours، dedupe، lease/retry/recovery و invalid-device |
| P1-8 | فقط runtime خارجی pending؛ پذیرش واقعی جعل نشده است |
| P1-9 / P1-10 | فایل خصوصی، UTF-8، MIME/signature، retention و Shared Media؛ بدون AV/CDR |
| P1-11 تا P1-16 | Reply/Edit/Delete/Reaction/Forward/Message Pin موجود ممیزی و رگرسیون؛ قواعد permission/version/Safety حفظ شدند |
| P1-17 / P1-18 | Archive/Conversation Pin/Mute/Manual Unread حساب‌محور؛ Read واقعی monotonic |
| P1-19 / P1-20 | Search مجاز SQL و صفحه‌بندی history/search/media/list با مرزهای مستند |
| P1-21 / P1-22 / P1-23 | دو session مستقل، revoke، Delta/reconnect و حریم رویداد خصوصی |
| P1-24 / P1-25 | GAP-05 حفظ شد؛ preview عمومی بدون متن/سن/مدرسه/رابطه/علت Safety |
| P1-26 / P1-27 / P1-28 / P1-29 | rate/Retry-After، OpenAPI رشته‌ای، handoff و ماتریس ۳۵ سناریوی محلی |

## ۲. معماری و جهت وابستگی

- Domain: enum رویداد و علت failure و رفتار محدود state دستگاه/تلاش ارسال؛ هیچ EF، HTTP، Firebase یا VAPID در Domain نیست.
- Application: use case و port، DTO خصوصی دستگاه/read-state و validation subscription؛ `MessagingWireTime` خروجی UTC را بدون جابه‌جایی ساعت حفظ می‌کند.
- Infrastructure: EF/SQL، قفل و تراکنش، migrations، worker، privacy resolverهای فعلی، SDK و HttpClient providerها و حفاظت اتصال Web Push.
- Api: Controllerهای نازک، route، authentication، ProblemDetails/OpenAPI و rate policy؛ هیچ query EF یا تصمیم کسب‌وکاری وارد Controller نشد.

جریان dependency همان `Api -> Application <- Infrastructure` و وابستگی Application/Infrastructure به Domain است. Composition Root در Api/Infrastructure implementationها را ثبت می‌کند. .NET 9، EF9 SQL Server، Onion در Modular Monolith و Controller API ثابت ماندند؛ MediatR، CQRS، repository عمومی یا subsystem اعلان دوم اضافه نشد.

## ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت، دلیل ایجاد و نکتهٔ آموزشی |
|---|---|
| `MakanApp.Infrastructure/Messaging/EfMessagingStore.PeerRead.cs` | snapshot/event مجاز Peer Read؛ cursor خصوصی peer را مستقیماً expose نمی‌کند؛ «read یک پیام قابل مشاهده» با «فعالیت آخر کاربر» فرق دارد |
| `MakanApp.Infrastructure/Notification/NotificationDeliveryProcessor.Authorization.cs` | reauthorization قبل از handoff خارجی؛ مجوز زمان enqueue کافی نیست |
| `MakanApp.Application/Notification/WebPushSubscriptionValidation.cs` | HTTPS/allowlist و کلید استاندارد P-256/Base64url؛ ورودی خراب نباید 500 یا SSRF بسازد |
| `MakanApp.Infrastructure/Notification/EfNotificationPreferenceStore.Devices.cs` | upsert و list خصوصی روی رجیستری قبلی؛ Serializable/قفل range، نه check-then-insert بدون محافظ |
| `MakanApp.Infrastructure/Notification/WebPushNotificationProvider.cs` | VAPID و رمزنگاری استاندارد از کتابخانه؛ crypto پروتکل دست‌نویس نشده است |
| `MakanApp.Infrastructure/Notification/FcmPushNotificationProvider.cs` | envelope HTTP v1 و تفکیک outcome؛ خطای project/credential مجوز revoke token سالم نیست |
| `MakanApp.Infrastructure/Notification/GoogleFcmAccessTokenProvider.cs` | فایل service_account خارجی و SDK OAuth cache/refresh؛ endpoint احراز هویت از JSON دلخواه پذیرفته نمی‌شود |
| `MakanApp.Infrastructure/Notification/WebPushNetworkPolicy.cs` | منع redirect/proxy، IP خصوصی و pin اتصال به DNS بررسی‌شده؛ HTTPS به‌تنهایی دفاع SSRF نیست |
| `MakanApp.Infrastructure/Notification/PushTransportOptions.cs` | اعتبارسنجی key pair، Subject، TTL و config؛ نداشتن credential، success ساختگی تولید نمی‌کند |
| `MakanApp.Infrastructure/Notification/PushProviderHttpPolicy.cs` | Retry-After، تفکیک status و سقف پاسخ؛ دادهٔ provider هم باید bounded باشد |
| `MakanApp.Infrastructure/Notification/PushWirePayload.cs` | payload عمومی، tag پایدار و navigation hint؛ DTO داخلیِ حاوی secret نباید به payload عمومی تبدیل شود |
| `MakanApp.Infrastructure/Notification/MessengerPushNotificationProvider.cs` | routing platform و availability امن روی port موجود؛ نه سامانهٔ notification موازی |
| `MakanApp.Infrastructure/Notification/NotificationPushDependencyInjection.cs` | تنظیم فنی transportها و HttpClient بدون logger URI/token؛ تفاوت composition با business logic |
| `MakanApp.Application/Messaging/MessagingWireTime.cs` | بازیابی Kind زمان UTC؛ `SpecifyKind` با تبدیل ساعت توسط `ToUniversalTime` یکی نیست |
| `MakanApp.Infrastructure/Persistence/Migrations/20261016072600_AddDirectPeerReadReceipts.cs` | CHECK نوع رویداد جدید |
| `MakanApp.Infrastructure/Persistence/Migrations/20261016074318_HardenNotificationDeliveryRecovery.cs` | CHECK علت‌های failure جدید |
| `MakanApp.Infrastructure/Persistence/Migrations/20261016081315_ExtendPushDeviceSubscriptions.cs` | دو ستون nullable و CHECK کلیدها، بدون مقصد/کلید جعلی |

فایل‌های تست مهم به‌ترتیب قابلیت: `MessagingP1ReadReceiptEndpointsTests.cs`، `MessagingP1NotificationDeliveryTests.cs`، `MessagingP1PushRegistrationTests.cs`، `MessagingP1PushTransportIntegrationTests.cs`، `MessagingP1MediaWireTests.cs`، `MessagingP1RateLimitTests.cs`، `MessagingP1MultiDeviceAcceptanceTests.cs` و `MessagingP1PerformanceAcceptanceTests.cs` زیر `tests/MakanApp.IntegrationTests`؛ Unitهای notification زیر `tests/MakanApp.UnitTests/Notification` هستند. این‌ها فایل‌های واقعی‌اند، نه برنامهٔ آینده.

## ۴. فایل‌های مهم تغییرکرده

`MessagingContracts`، interface/service و partialهای `EfMessagingStore`، fact Peer Read و event نوع ۱۲ را به Delta/outbox موجود وصل کردند. realtime فقط field nullable لازم را گرفت و audience دقیق همان authorization Delta را reuse کرد. `NotificationDeliveryDispatcher` همان worker را نگه داشت، claim تک‌item/renewal و recovery bounded افزود و در transaction کوتاه نتیجه را ثبت می‌کند. `EfNotificationPreferenceStore`، active account و هر دو expiry نشست را بررسی می‌کند.

`PushDeviceRegistration` و EF configuration به همان entity قبلی کلید Web و rowversion موجود را متصل کردند. Controller دستگاه، list خصوصی و POST Web و GET public-key را ارائه می‌کند؛ `GlobalExceptionHandler` خطای unavailable را به 503 map کرد. `DependencyInjection` فقط بخش Push و portهای مرتبط را افزود؛ تابع/تنظیم OTP ملی‌پیامک و AI تغییر نکرد. availability امن backoffice فقط اسم وضعیت transport را نشان می‌دهد، نه secret؛ `Configured` در آن معادل مشاهدهٔ delivery واقعی نیست.

`MessagingService`، `MessagingSafetyService`، `ConversationManagementService` و realtime mapper فقط Kind خروجی زمان‌های core را تصحیح کردند. `FileSignatureValidator` flush متن را اصلاح کرد؛ سیاست نوع/حجم/extension و SQL retention ضعیف نشد. `Program.cs` و Controller metadata policyهای mutation و Retry-After مرتبط را افزودند؛ OTP/AI/payment و ترتیب authentication دست‌نخورده ماندند. appsettings و solution تغییر نکردند.

## ۵. جریان اجرای واقعی قابلیت

Peer Read:

```text
POST read / GET read-states
-> ConversationsController
-> MessagingService + IAccessContextResolver (GuardianAccessContextResolver)
-> ConversationParticipant.AdvanceRead (monotonic)
-> EfMessagingStore.Sync / EfMessagingStore.PeerRead
-> SQL Server: cursor + event خصوصی خود reader + event محدود PeerRead
-> MessagingRealtimeOutboxProcessor
-> audience مجازِ فعلی + ConversationChanged
-> snapshot/Delta مجاز + HTTP response
```

Push:

```text
HTTP SendMessage
-> ConversationsController -> MessagingService
-> قواعد Domain و مجوز/Safety جاری
-> EfMessagingStore: Message + اعلان/outbox در transaction
-> SQL commit -> receipt Sent
-> NotificationDeliveryCoordinator -> DeliveryAttempt پایدار
-> NotificationDeliveryProcessor: claim + reauthorization + renewal
-> MessengerPushNotificationProvider
-> WebPushNotificationProvider یا FcmPushNotificationProvider
-> result: accepted / retry / failed / suppressed
-> SQL نتیجه + revoke فقط registration دارای همان rowversion
```

HTTP provider خارج transaction اجرا می‌شود. خطای شبکه Message را rollback نمی‌کند. ارسال بیرونی تا زمان recheck مجاز است، ولی Push پذیرفته‌شده را بعداً نمی‌توان تضمینی از شبکه پس گرفت؛ preview عمومی و REST مجاز پس از click لازم‌اند.

## ۶. دیتابیس و migration

هیچ table جدیدی ساخته نشد؛ همان ۸۸ table باقی‌اند. `messaging.ChangeEvents` در migration اول فقط CHECK نوع را از ۱–۱۱ به ۱–۱۲ گسترش می‌دهد. `notification.NotificationDeliveryAttempts` در migration دوم فقط CHECK علت failure را از ۱–۸ به ۱–۱۳ گسترش می‌دهد. migration سوم روی `notification.PushDeviceRegistrations` ستون‌های nullable `WebPushP256dh varchar(87)` و `WebPushAuth varchar(22)` و CHECK هم‌زمانی کلیدها/platform را می‌افزاید.

PK/FK، hash مقصد، filtered unique فعال و rowversion رجیستری قبلی reuse شدند؛ کلید خصوصی VAPID یا service-account در SQL مدل جدید قرار نگرفت. subscription endpoint/auth و device token دادهٔ حساس ذخیره‌شده‌اند؛ API/list/log/admin آن‌ها را افشا نمی‌کند و دسترسی/backup SQL باید محدود باشد. Web legacy بدون کلید حفظ می‌شود و برای ارسال نیازمند re-register است. migration، data wipe، default جعلی، backfill کلید یا revoke دسته‌جمعی انجام نمی‌دهد.

upsert با Serializable و UPDLOCK/HOLDLOCK، token بین حساب‌ها را منتقل نمی‌کند. تغییر کلید endpoint موجود version جاری می‌خواهد؛ retry همان intent شناسهٔ ثابت دارد. delivery lease/ClaimToken از claim دزدیده‌شده محافظت و revoke فقط rowversion مقصدِ ارسال‌شده را هدف می‌گیرد. unique اعلان/کانال/مقصد و logical notification پیام/گیرنده قبلی حفظ شدند.

تمام chain شامل ۴۱ migration در تست ایزوله اجرا شد؛ EF pending-model check موفق است. اسکریپت idempotent-safe و هفت regression SQL در ممیزی لینک شده‌اند؛ نه سرور و نه دیتابیس عادی migrate نشد. Down کلیدها را حذف و CHECKهای enum را تنگ می‌کند؛ با وجود دادهٔ جدید downgrade کورکورانه مجاز نیست و rollback نیازمند backup/برنامهٔ داده است.

## ۷. Endpointهای جدید و قراردادهای حساس

همهٔ مسیرهای زیر Bearer session معتبر می‌خواهند؛ UserId از caller است، نه body. request/response کامل و نمونه‌ها در Handoff/OpenAPI هستند.

| Method / Route | ورودی و پاسخ | validation / خطای مهم |
|---|---|---|
| GET `/api/v1/conversations/{id}/read-states` | `ConversationPeerReadStateResult`؛ Visible/Hidden/NotSupported و sequence/version nullable | current read/participant/account/history/privacy/eligibility؛ 404 خارج مجوز؛ بدون per-member Group receipt |
| GET `/api/v1/notifications/devices` | page/pageSize/includeRevoked -> `PushDevicePageResult` | فقط metadata خود حساب؛ pageSize تا ۱۰۰؛ هیچ token/endpoint/key در خروجی نیست |
| POST `/api/v1/notifications/devices/web-push` | endpoint/p256dh/auth/expectedVersion -> registration ID/metadata/version | HTTPS دقیق، P-256 و Base64url؛ 400 ورودی، 409 نسخه/مالک، 503 allowlist ناموجود، 429 |
| GET `/api/v1/notifications/devices/web-push/configuration` | `WebPushClientConfiguration` | فقط isConfigured/public key؛ false/null در نبود config لازم، نه private key |
| POST `/api/v1/notifications/devices`، موجود | Android/Ios + pushToken؛ افزودنی version/کلیدهای Web | owner/active session، token فعال حساب دیگر conflict، registration idempotent |
| DELETE `/api/v1/notifications/devices/{id}`، موجود | لغو -> 204 | فقط خود حساب؛ ناموجود/قبلاً لغوشده 404 |

read/delivered قبلی ExpectedVersion تازه نمی‌خواهند و monotonic هستند. Edit/Everyone Delete، expectedVersion فعلی و permission/window خودشان را دارند؛ stale Edit برابر 412 است. state شخصی با POST/DELETE archive و state/pin، PUT/DELETE mute و POST mark-unread مصرف می‌شود. ForMe فقط همان حساب، Everyone tombstone مجاز، Reaction effect هم‌ارز، Forward permission مبدأ/مقصد و Message Pin permission گفتگو را حفظ می‌کنند. Search و Shared Media فقط history و فایل/پیام مجاز جاری را می‌دهند؛ در Handoff route/request/error هرکدام توضیح داده شده است.

Send/Edit مشترک ۳۰۰، Add/Remove Reaction مشترک ۳۰۰، Forward=120، Search/Discovery هر policy=60، Upload=20 و registration=60 در دقیقه/کاربر/process هستند. 429 با Retry-After و CORS expose از 401 و policy rejection جداست. این‌ها limiter توزیع‌شده نیستند.

## ۸. مفاهیم فنی واقعاً استفاده‌شده

Read cursor با receipt HTTP، notification read، Delivered و fact Peer Read یک state نیست. rowversion برای write concurrency است؛ version opaque receipt یا Delta cursor قابل‌جایگزینی با آن نیست. Outbox اثر جانبی durable بعد از commit را ممکن می‌کند، نه exactly-once شبکه. lease و token مالکیت، recovery crash و محدودیت تلاش را کنترل می‌کنند. current authorization فقط نقش نیست: account/session، membership/role، رابطه، Block/Privacy، history و وضعیت فعلی منبع هم مهم‌اند.

Web Push از کتابخانهٔ استاندارد با رمزنگاری و VAPID استفاده می‌کند؛ FCM از HTTP v1 و SDK رسمی OAuth. این‌ها رمزنگاری سرتاسری چت نیستند. Retry-After، پاسخ bounded، عدم redirect و pin IP پس از DNS و جلوگیری از log secret، بخش واقعی adapter هستند. UTC ذخیره‌شده با جابه‌جایی ساعت منطقهٔ کاربر فرق دارد. دو HttpClient با یک token، تست دو session مستقل نیست؛ این مرحله دو VerifyOtp و دو row نشست را اثبات می‌کند.

## ۹. تصمیم‌های طراحی و trade-off

رجیستری/notification subsystem موجود گسترش یافت؛ مدل موازی به duplicate identity و lifecycle مبهم منجر می‌شد. SDK/transport استاندارد به‌جای crypto/OAuth دست‌نویس انتخاب شد؛ دو dependency فنی فقط در Infrastructure هزینهٔ آن‌اند. Worker SQL retry را مالک است؛ auto-retry کتابخانه خاموش و network خارج transaction است. exactly-once بیرونی وعده داده نمی‌شود؛ NotificationId/tag پایدار برای dedupe لازم است.

Peer Read محافظه‌کارانه به privacy/eligibility فعلی دوطرفه وابسته است؛ در Hidden علت خصوصی افشا نمی‌شود و history خودکار حذف نمی‌شود. preview همهٔ حساب‌ها عمومی است، نه فقط Minor؛ privacy بهتر در برابر preview غنی‌تر. allowlist دقیق و اتصال بدون proxy/redirect، SSRF را محدود می‌کند ولی محیط proxy-only/host جدید نیازمند config عملیاتی معتبر است. FCM service-account خارجی به file custody و rotation/restart نیاز دارد؛ frontend هرگز credential سرور نمی‌گیرد. iOS به APNs در Firebase و مرورگر به secure context نیاز دارد.

تنظیم فعال‌سازی `Notifications:PushProvider=WebPushAndFcm` است؛ VAPID از `Notifications:WebPush:VapidSubject/VapidPublicKey/VapidPrivateKey` و hostهای دقیق از `Notifications:WebPush:AllowedEndpointHosts` خوانده می‌شوند. FCM به `Notifications:Fcm:ProjectId/ServiceAccountPath` یا fallback مسیر `GOOGLE_APPLICATION_CREDENTIALS` نیاز دارد. جزئیات TTL، ACL، Windows/Linux/Docker و نمونه‌های غیرمحرمانه در Handoff آمده‌اند؛ config موجود انتشار تغییر نکرد. public VAPID key هویت application server است و با `p256dh/auth` متعلق به subscription مرورگر یکسان نیست؛ private VAPID و credential Firebase فقط سمت سرور می‌مانند.

Shared Media به‌خاطر فرض اشتباه بازنویسی نشد: constraint واقعی، fixture نامعتبر را رد کرد. تغییر test setup به assertion همان invariant، با خاموش‌کردن تست فرق دارد. state موجود User-scoped برای چند session درست بود؛ به‌جای تغییر بی‌دلیل Domain، تست پذیرش اضافه شد. list offset-based snapshot چندصفحه‌ای در همهٔ mutationها نیست؛ client باید refresh/dedupe مستند انجام دهد.

## ۱۰. تست‌ها و معنای آن‌ها

- Unit/HTTP-component: رفتار pure recovery، endpoint/کلید، crypto/header، payload حداقلی، VAPID pair/config، status/outcome، cancellation، Retry-After/response bound، IP/handler policy و signature/UTF-8. transportها HTTP کنترل‌شده دارند و مدعی تماس provider واقعی نیستند.
- Integration: Controllers/authorization/SQL constraints/migrations/rowversion، race/idempotency/restart، actual adapter به worker و SQL، دو session مستقل، Hub/Delta، download و privacy. LocalDB واقعی استفاده شد، نه EF InMemory.
- Architecture: ۱۴ تست موجود جهت Onion و عدم نشت implementation به Domain/Application را بررسی می‌کنند؛ برای افزایش عدد placeholder ساخته نشد.

پذیرش ۳۵ سناریو در ممیزی با نام تست واقعی map شده است. تست‌های جدید: Peer Read=11 Integration؛ recovery=2 Unit+19 Integration؛ registration=10 Unit+9 Integration؛ provider=65 Unit/component+12 Integration؛ media/rate=8 Unit+18 Integration؛ پذیرش نهایی=1 Unit+6 Integration. مجموع افزودهٔ واقعی **۸۶ Unit و ۷۵ Integration، یعنی ۱۶۱ case** است؛ Unit تکمیلی عدم مجوز خواندن فایل credential FCM را configuration failure می‌داند، نه خطای شبکه/token نامعتبر. تست قبلی حذف یا skip نشده است.

smoke محلی: ۱۰۵ گفتگو، ۴۱ پیام history/search، reconnect سه اتصال، race ۸ درخواست offline و ۱۲ درخواست notification، و ۱۸ delivery با batch حداکثر ۴. زمان‌های focused در ممیزی/ TRX هستند؛ ظرفیت Production، backplane یا دستگاه واقعی از آن‌ها نتیجه نمی‌شود.

## ۱۱. Validation واقعی

| اجرا | نتیجه |
|---|---|
| baseline | Unit=413، Integration=726، Architecture=14؛ total=1153؛ همه passed |
| فاز Peer Read | total=1164؛ full و final موفق |
| فاز recovery | total=1185؛ full و final موفق |
| فاز registration | final=1204؛ شامل اصلاح validation نقطهٔ نامعتبر Windows |
| فاز provider | final=1281؛ پس از تست‌های OAuth 408/502/504 |
| فاز media/rate | final=1307؛ focused/fix سپس یک full نهایی |
| final full اول P1 | restore و Build موفق؛ ۰ warning/error؛ Unit=499، Integration=801، Architecture=14؛ total=1314 passed، failed=0، skipped=0؛ شواهد `acceptance-reviewed` |
| final full دوم P1 | final Build موفق؛ ۰ warning/error؛ Unit=499، Integration=801، Architecture=14؛ total=1314 passed، failed=0، skipped=0؛ شواهد `acceptance-final` |
| SQL script | ۷ passed، ۰ failed؛ نصب/upgrade/retry/rollback/207/ambient transaction |
| مدل EF | no pending model changes؛ migrationهای جدید بررسی‌شده و SQL ایزوله |
| NuGet مستقیم/انتقالی Infrastructure | آسیب‌پذیری شناخته‌شده‌ای از منابع تنظیم‌شده گزارش نشد |
| OpenAPI محلی | ۲۹۱ operation، ۲۶۲ path، ۴۸ Controller؛ string enum/nullable/Flags و DTOهای افزوده |

شکست‌های red/setup و رفع آن‌ها در ممیزی ثبت‌اند: Base64 fixture، exception نقطهٔ نامعتبر Windows، mailto URI، token متعلق به host دیگر، fixtureِ خلاف retention، Count تست و قفل DLL ناشی از Build هم‌زمان با testhost. هیچ‌کدام success نامیده یا با تغییر .NET/خاموش‌کردن guard پنهان نشدند. هر دو اعتبارسنجی نهایی **صفر error/failure/skip** داشتند؛ نتایج TRX زیر `.artifacts/messenger-p1/tests` هستند. تست EF نهایی نیز پس از Build موفق دوباره اجرا شد؛ مدل pending ندارد.

پرچم‌های فنی مبتنی بر همین تست‌های محلی؛ نتیجهٔ commit/push جداگانه از Git واقعی گزارش می‌شود:

```text
MESSENGER_P1_READ_RECEIPTS = PASS
MESSENGER_P1_NOTIFICATION_CORE = PASS
MESSENGER_P1_WEB_PUSH_CODE = PASS
MESSENGER_P1_FCM_CODE = PASS
MESSENGER_P1_REAL_PUSH = PENDING_ENVIRONMENT
MESSENGER_P1_MEDIA = PASS
MESSENGER_P1_SEARCH = PASS
MESSENGER_P1_MESSAGE_ACTIONS = PASS
MESSENGER_P1_MULTI_DEVICE = PASS
MESSENGER_P1_OFFLINE_RECOVERY = PASS
MESSENGER_P1_CHILD_SAFETY = PASS
MESSENGER_P1_OPENAPI = PASS
```

## ۱۲. Git و محدودهٔ فایل‌ها

worktree مجاز `D:\Project\MakanApp\MakanApp-Backend-Messenger-P0`، branch=`messenger/p0`، upstream=`origin/main`، remote=`origin` با URL `https://github.com/RastinaxAI/MakanApp.git` است. branch نام متفاوت دارد و push عادیِ بدون force با `git push origin HEAD:main` انجام می‌شود. workspace اصلی و dirtyهای قبلی AI reset/stash/clean/تغییر/منتقل نشده‌اند. stage با مسیر صریح بود؛ هیچ `git add .` یا `git add -A` استفاده نشد.

| SHA | پیام | دامنه / نتیجهٔ remote |
|---|---|---|
| `8becc1f3dd90643faa6f214cdae7042cd6be0d68` | `feat(messaging): add peer read receipt contract` | ۱۸ فایل؛ push/fetch/ls-remote موفق |
| `90fea9b78bfe5f76e63e2eeff52b11b8048f5038` | `feat(notifications): harden messenger delivery recovery` | ۱۵ فایل؛ push/verify موفق |
| `a59bb740a06c921dd491e2a865737c652e896ed6` | `feat(notifications): add web push and mobile registrations` | ۲۱ فایل؛ push/verify موفق |
| `4856b284c59cdb490913268803e6aa2aba05c36e` | `feat(notifications): deliver web push and fcm transports` | ۲۴ فایل؛ push/verify موفق |
| `3f5a8f41e33f18db31e15ee4b17c6ba4b4227809` | `fix(messaging): harden media search and message actions` | ۱۴ فایل؛ push/verify موفق؛ مبنای آخرین فاز پذیرش |
| `ae664539927dd6e83d36711339d0e46da5861668` | `fix(messaging): close p1 acceptance and credential failure handling` | ۹ فایل؛ پذیرش، catch محدود FCM و پنج سند؛ push/fetch/ls-remote موفق و HEAD برابر origin/main |

commit پذیرش بالا، پس از validation دوباره، دو فایل تست پذیرش، اصلاح محدود طبقه‌بندی خطای دسترسی فایل FCM و Unit آن، و پنج سند مرتبط را ثبت کرد. commit مستندسازی همراه نسخهٔ جاری با پیام `docs(messaging): finalize p1 acceptance evidence` فقط نام واقعی resolver و شاهد commit پذیرش را در همین گزارش دقیق می‌کند؛ کد اجرایی تغییر نمی‌کند. SHA خود commit به‌صورت دوری داخل محتوای خودش نوشته نمی‌شود. hash نهایی، push و برابری HEAD/origin/main در خروجی واقعی Git و گزارش نهایی گفتگو اعلام می‌شوند؛ پیش از اجرای موفق، انجام‌شده تلقی نمی‌شوند. هیچ secret، frontend، bin/obj، کد/تنظیم MeliPayamak یا تغییر AI وارد این commitها نشده است.

## ۱۳. محدودیت‌های فعلی و موارد خارج تحویل

REAL-PUSH-01 تا 04 pending هستند: browser/subscription/VAPID عملیاتی، Firebase credential/FCM device و APNs iOS ارائه نشده‌اند. پذیرش actual background delivery/click روی دستگاه مشاهده نشده است. `Configured` یا پاسخ HTTP provider جای آن نیست. تغییر سرور، TLS، frontend/service worker و deploy در این کار انجام نشده‌اند. استثنای HTTP قبلی API، نیاز secure context مرورگر را رفع نمی‌کند.

Typing، Presence/Last Seen، Topics، Poll پیشرفته، Scheduled، folder و Saved Messages پیشرفته، general SMS، AV/CDR، distributed limiter و SignalR backplane خارج این P1 هستند. Calls/Video-call/Live/Stories، Bots/Mini Apps/Premium/Monetization و AI/RAG/Payments تازه نیز پیاده نشدند. Video attachment فعلی با تماس ویدئویی یکی نیست. runtime principal/secret ACL/backup عملیاتی، محدودیت‌های قبلی خودشان را دارند و با smoke محلی بسته نمی‌شوند. providerهای واقعی باید قبل از پذیرش عملیاتی تنظیم شوند؛ تلاش terminal قبلی خودکار replay نمی‌شود و admin resend تازه ساخته نشد.

در register محصول، Typing/Presence/Topics/Poll/Scheduled و پیشرفته‌های Saved در P2، تماس/Video-call/Live/Stories در P3 و Bots/Mini Apps/Premium/Monetization در P4 باقی‌اند. general SMS، AV/CDR و زیرساخت توزیع‌شده track عملیاتی مستقل‌اند؛ بسته‌شدن کد P1 مجوز خودکار پیاده‌سازی آن‌ها نیست.

## ۱۴. هفت نکتهٔ آموزشی

1. receipt commit، delivery transport و read انسان را در یک enum/state ادغام نکنید.
2. حق دسترسیِ زمان enqueue برای side effect دیرتر کافی نیست؛ پیش از dispatch مجوز جاری را دوباره بسنجید.
3. unique/index/rowversion/transaction و retry intent با هم جلوی اثر تکراری را می‌گیرند، نه یک check ساده یا امید به شبکه.
4. `datetime2` UTC را با metadata زمانی اشتباه نگیرید؛ بازیابی Kind نباید timestamp را چند ساعت جابه‌جا کند.
5. قبل از «fix» فرضی، invariant SQL را بخوانید؛ test fixture خلاف constraint به معنی bug production نیست.
6. SDK رسمی، secret خارجی و payload حداقلی مکمل یکدیگرند؛ SDK به‌تنهایی جلوی SSRF، log secret یا preview حساس را نمی‌گیرد.
7. دو session واقعی و مخاطب قطعی SQL را تست کنید؛ منفی‌بودن اتفاقی یک callback یا دو client با یک token، اثبات privacy چنددستگاهی نیست.

## ۱۵. مسیر پیشنهادی مطالعهٔ کد

برای Peer Read: `ConversationsController` -> `MessagingService.GetPeerReadStateAsync/MarkConversationReadAsync` -> `ConversationParticipant` -> `IMessagingStore` -> `EfMessagingStore.PeerRead/Sync` -> realtime outbox/audience -> migration اول -> `MessagingP1ReadReceiptEndpointsTests`.

برای Push: `NotificationDevicesController` -> `PushDeviceService` و contracts -> `PushDeviceRegistration` -> `IPushDeviceStore/IPushNotificationProvider` -> `EfNotificationPreferenceStore.Devices` -> `NotificationDeliveryProcessor` و partial Authorization -> router و Web/FCM adapter -> EF configuration/migrations -> Unit transport -> `MessagingP1PushTransportIntegrationTests`.

برای درک اصلاح و پذیرش: `FileSignatureValidator` و تست red/fix آن -> `MessagingWireTime` و mapperها -> `MessagingP1MediaWireTests` -> rate metadata/Program و تست 429 -> `MessagingP1MultiDeviceAcceptanceTests` -> `MessagingP1PerformanceAcceptanceTests`. فایل‌های Designer بزرگ، snapshot تولید EF هستند؛ شروع مطالعه از آن‌ها مناسب نیست.

## ۱۶. مرحلهٔ بعد

گام بعدی، **پذیرش عملیاتی Push موجود** با credential خارجی و دو حساب/مرورگر یا دستگاه مجاز در secure context است: بسته‌بودن صفحه، click به گفتگوی درست پس از login، Mute، revoke/invalid subscription و FCM/APNs واقعی مشاهده شوند. تا آن زمان REAL_PUSH_PASS نداریم و وضعیت runtime pending می‌ماند. Typing/Presence، AV/CDR، scale-out یا هر قابلیت P2 باید مرحلهٔ مستقل و مجوز محصولی خودش را داشته باشد؛ با retest همین transportها مخلوط نشود.
