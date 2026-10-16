# قرارداد مصرف Messenger P1

این سند فقط قرارداد Backend است؛ هیچ frontend یا service worker در این worktree ساخته/ویرایش نمی‌شود. افزوده‌های هر فاز پس از validation ثبت می‌شوند. مبنای قبلی [Handoff P0](MESSENGER_P0_FRONTEND_HANDOFF.md) و مرجع JSON، OpenAPI runtime همین build است.

## دستگاه‌ها و subscription خصوصی

همان رجیستری `PushDeviceRegistration` موجود گسترش یافته است؛ subsystem دوم ساخته نشده است. همهٔ مسیرهای زیر Bearer session فعال می‌خواهند و مالک را از نشست می‌گیرند، نه از body:

| Method / Route | ورودی | خروجی / خطا |
|---|---|---|
| `GET /api/v1/notifications/devices` | `page=1&pageSize=30&includeRevoked=false`؛ سقف pageSize برابر ۱۰۰ | `PushDevicePageResult` فقط metadata خود کاربر، `isActive` و `version` |
| `POST /api/v1/notifications/devices` | `platform:"Android"` یا `"Ios"`، `pushToken`؛ `expectedVersion` اختیاری | شناسهٔ ثابت registration، platform، زمان‌ها و version؛ نه token |
| `POST /api/v1/notifications/devices/web-push` | `endpoint`، `p256dh`، `auth`، `expectedVersion` اختیاری | همان `PushDeviceRegistrationResult` امن |
| `DELETE /api/v1/notifications/devices/{registrationId}` | شناسهٔ متعلق به خود کاربر | `204`؛ شناسهٔ دیگران/قبلاً لغوشده `404` |

کلیدهای subscription استاندارد Base64url هستند: public P-256 معتبر ۶۵ بایت و auth برابر ۱۶ بایت؛ سرور شکل canonical بدون padding را ذخیره می‌کند. مقصد فقط HTTPS پورت ۴۴۳، بدون userinfo/fragment/IP literal و در allowlist دقیق `Notifications:WebPush:AllowedEndpointHosts` پذیرفته می‌شود؛ wildcard یا suffix-match وجود ندارد. نبود allowlist، `503 notification_push_unavailable` می‌دهد. این مرحله هنوز ارسال واقعی provider را ادعا نمی‌کند.

ثبت مجدد همان token/endpoint و همان کلیدها برای همان حساب idempotent و دارای شناسهٔ ثابت است. برای **تغییر کلیدهای endpoint موجود**، version جاری از list/آخرین پاسخ لازم است؛ نسخهٔ قدیمی `409 notification_preference_concurrency_conflict` می‌دهد. token فعالِ حساب دیگر منتقل نمی‌شود (`409 notification_device_conflict`). تغییر token واقعی، ثبت مقصد جدید و سپس لغو شناسهٔ قدیمی است. session جدید همان حساب می‌تواند registration را refresh کند؛ آخرین session ثبت‌کننده، عمر دسترسی مقصد را تعیین می‌کند. لغو/انقضای همان session ارسال آینده را متوقف می‌کند؛ دستگاه‌های سایر sessionها مستقل‌اند.

فهرست فقط `id/platform/createdAtUtc/lastSeenAtUtc/revokedAtUtc/isActive/version` می‌دهد؛ endpoint، token، auth، p256dh، hash و session credential افشا نمی‌شوند. زمان‌ها UTC با پسوند `Z` هستند. mutationها سقف ۶۰ درخواست در دقیقه برای هر کاربر دارند؛ `429` و `Retry-After` نشانهٔ throttling هستند، نه خطای permission. خطای ورودی `400 notification_preference_invalid` است. خود list مجوز خواندن چت یا فایل نمی‌دهد.

Migration `20261016081315_ExtendPushDeviceSubscriptions` فقط دو ستون nullable و CHECK سازگاری کلیدها را به جدول موجود اضافه می‌کند. رکورد Web قدیمی بدون کلید حفظ می‌شود، اما برای ارسال Web Push باید subscription معتبر مجدداً ثبت شود؛ داده یا کلید ساختگی backfill نمی‌شود. Down کلیدها را حذف می‌کند و برای rollback عملیاتی نیازمند بررسی/backup است.

## Web Push و FCM واقعی در کد

`Notifications:PushProvider=WebPushAndFcm`، platform برابر Web را به `WebPushNotificationProvider` و Android/Ios را به `FcmPushNotificationProvider` می‌فرستد. Development همچنان فقط Development/Testing است؛ Unavailable موفقیت ساختگی نمی‌دهد. نبود/خرابی credential، وضعیت unavailable/configuration failure می‌دهد؛ ثبت subscription، SignalR، Sent و حتی پذیرش provider، اثبات نمایش روی دستگاه نیستند. در این تحویل credential و دستگاه واقعی فراهم نشده‌اند: **P1_PUSH_RUNTIME_PENDING**.

`GET /api/v1/notifications/devices/web-push/configuration` با session معتبر فقط `{isConfigured,vapidPublicKey}` می‌دهد؛ نبود تنظیم لازم، false/null است. private key، مسیر credential و token سرور هرگز در این DTO نیستند. این endpoint فقط آمادگی تنظیم‌شدهٔ Web transport را اعلام می‌کند، نه صحت نهایی شبکه یا مجوز مرورگر.

گردش Web: پس از ورود، config عمومی را بگیرید؛ frontend تحت مالکیت خودش با اقدام صریح کاربر permission و subscription استاندارد مرورگر/service worker را دریافت می‌کند؛ endpoint و کلیدهای Base64url را به POST web-push می‌دهد و شناسه/version پاسخ را نگه می‌دارد. برای rotation همان endpoint نسخهٔ جاری لازم است؛ برای endpoint جدید ثبت تازه و لغو registration قدیمی انجام شود. هنگام logout، registration را در صورت دسترسی لغو کنید؛ حتی اگر کلاینت offline باشد، session منقضی/لغوشده در worker مجاز به ارسال آینده نیست. مرورگر/PWA به secure context مانند HTTPS یا localhost مجاز نیاز دارد؛ استثنای HTTP قبلی API این محدودیت مرورگر و mixed-content را رفع نمی‌کند.

گردش موبایل: SDK کلاینت Firebase token مقصد را دریافت و فقط با session خود کاربر به POST devices با `platform:"Android"` یا `"Ios"` ثبت می‌کند. تغییر token، ثبت مقصد جدید و لغو شناسهٔ قدیمی است. service-account JSON و OAuth access token **فقط در Backend** هستند. برای iOS، permission دستگاه و credential معتبر APNs در پروژهٔ Firebase لازم‌اند؛ هیچ فایل/کد موبایل در این repository ساخته نشده است. [مرجع رسمی FCM](https://firebase.google.com/docs/cloud-messaging/send/v1-api).

Payload خارجی برای همهٔ حساب‌ها، از جمله Minor/Unknown، عمومی و حداقلی است: notificationId، type، tag، عنوان/متن عمومی، deepLink و فقط برای پیام conversationId. نام فرستنده، متن خصوصی، شماره، اسم فایل، سن، رابطهٔ والد، مدرسه و علت safety/moderation ارسال نمی‌شوند. token FCM صرفاً مقصد ضروری envelope است، نه metadata اعلان. Web payload با استاندارد Web Push رمز می‌شود؛ این کار E2EE چت نیست. [کتابخانهٔ transport](https://github.com/tpeczek/Lib.Net.Http.WebPush).

برای پیام، navigation target برابر `makan://conversations/{conversationId}` و برای سایر اعلان‌ها `makan://notifications/{notificationId}` است. این‌ها URI معنایی Backend هستند، نه ادعای وجود route وب یا handler آماده در frontend. service worker/app باید پس از ورود آن‌ها را به صفحهٔ مجاز خودش map کند و دادهٔ تازه را از REST بگیرد؛ شناسه یا payload Push مجوز چت نیست. اعلان‌های تکراری با tag ثابت NotificationId جایگزین/dedupe شوند؛ ACK provider نه Read Receipt است و نه exactly-once شبکه.

تنظیم‌های عملیاتی، فقط از secret/config محیط و **نه با commit credential**:

| Setting | مقدار/رفتار |
|---|---|
| `Notifications:PushProvider` | `WebPushAndFcm` برای transport واقعی |
| `Notifications:WebPush:VapidSubject` | URI عمومی تماس از نوع mailto یا HTTPS؛ نمونهٔ غیرواقعی `mailto:push-ops@example.test` |
| `Notifications:WebPush:VapidPublicKey` | public P-256 متناظر subscription مرورگر؛ در API عمومی authenticated قابل دریافت |
| `Notifications:WebPush:VapidPrivateKey` | secret خارجی Base64url؛ هرگز در repo، Docker image، frontend یا log قرار نگیرد |
| `Notifications:WebPush:AllowedEndpointHosts` | آرایهٔ host دقیق provider مورداعتماد؛ بدون scheme، path یا wildcard؛ از subscription واقعی و مرجع provider تعیین شود |
| `Notifications:WebPush:TimeToLiveSeconds` | پیش‌فرض ۳۶۰۰؛ بازهٔ ۰ تا ۸۶۴۰۰ |
| `Notifications:Fcm:ProjectId` | شناسهٔ واقعی پروژهٔ Firebase |
| `Notifications:Fcm:ServiceAccountPath` | مسیر مطلق فایل service-account خارجی؛ fallback نام محیط `GOOGLE_APPLICATION_CREDENTIALS` |
| `Notifications:Fcm:TimeToLiveSeconds` | پیش‌فرض ۳۶۰۰؛ بازهٔ ۰ تا ۸۶۴۰۰ |

در environment variables جداکنندهٔ `:` را `__` بنویسید؛ مثلاً `Notifications__PushProvider`. برای آرایه، `Notifications__WebPush__AllowedEndpointHosts__0` یک host دقیق واقعی است، نه دامنهٔ نمونه. روی Windows فایل credential خارج Publish و با Read محدود به identity اجرای IIS/سرویس باشد؛ روی Linux/Docker مسیر مطلق و secret mount فقط‌خواندنی با کمترین ACL لازم استفاده شود. نمونهٔ مسیر غیرمحرمانه: `C:\MakanSecrets\firebase-service-account.json` یا `/run/secrets/firebase-service-account.json`. فایل JSON نباید در Git، build context، image یا command output وارد شود. پس از rotation کلیدهای server، process را با تنظیم جدید restart کنید؛ SDK توکن OAuth را در حافظه cache/refresh می‌کند.

FCM فقط service_account همان ProjectId، token URI ثابت Google و scope محدود Firebase Messaging را می‌پذیرد؛ external-account، پروژهٔ دیگر یا token server دلخواه رد می‌شود. Web Push redirect و proxy ندارد؛ DNS مقصد باید فقط IP عمومی بدهد و همان IP بررسی‌شده به socket وصل می‌شود؛ TLS validation استاندارد خاموش نمی‌شود. بنابراین direct HTTPS egress به provider لازم است و محیط proxy-only نیازمند تصمیم عملیاتی مستقل است. HttpClient loggers این دو transport خاموش‌اند تا endpoint/token در URI یا header وارد log نشود.

۴۰۴/۴۱۰ Web و خطای typed `UNREGISTERED`/token-invalid در FCM مقصد را با محافظ rowversion لغو می‌کنند. خطای credential/project/payload عمومی باعث لغو token سالم نمی‌شود؛ ۴۲۹ با حداقل یک دقیقه و Retry-After و خطای موقت با backoff محدود retry می‌شوند. مقدار غیرمعقول Retry-After بیش از یک روز terminal failure است، نه overflow یا ارسال زودهنگام. بدنهٔ خطای Web وارد exception نمی‌شود و پاسخ FCM سقف ۱۶ KiB دارد. [تفکیک خطاهای رسمی FCM](https://firebase.google.com/docs/cloud-messaging/error-codes).

پذیرش قبلی provider را نمی‌توان پس از revoke از شبکه پس گرفت؛ preview عمومی و مجوز مجدد REST ضروری‌اند. TTL و بودجهٔ retry محدودند. پس از اصلاح config، تلاش‌های terminal قدیمی خودکار بازپخش نمی‌شوند؛ endpoint مدیریتی resend ساخته نشده است. هیچ appsettings موجود، تنظیم OTP ملی‌پیامک، سرور یا AI در این فاز تغییر نکرده است.

## Peer Read و یک/دو تیک

- HTTP receipt پیام با `status=Sent` فقط commit سرور را ثابت می‌کند؛ می‌توان آن را server-accepted/یک تیک نمایش داد. Delivered یا دریافت SignalR به معنی خواندن نیست.
- `POST /api/v1/conversations/{id}/read` با `upToMessageSequence` فقط پس از اقدام معنادار کاربر اجرا شود؛ بازکردن background socket شاهد خواندن نیست. cursor واقعی هرگز عقب نمی‌رود؛ read خود فرستنده peer را read نکرده است.
- `GET /api/v1/conversations/{id}/read-states` با Bearer معتبر، `conversationId`، `peerReadThroughSequence`، `visibility` و `version` می‌دهد. برای Direct، `Visible` و sequence برابر آخرین پیام خود caller با read واقعی peer است؛ پیش از آن ۰. فقط پیام خود caller با sequence کمتر/برابر این مقدار و در تاریخچهٔ مجاز می‌تواند read/دو تیک باشد.
- `Hidden` یعنی sequence/version=null؛ علت Block، Privacy، سن/رابطه یا وضعیت peer را حدس نزنید و تیک read تازه نسازید. privacy دوطرفه Personal، active account/participant و eligibility جاری لازم‌اند. این وضعیت مجوز حذف history محلی یا افشای علت خصوصی نیست.
- Group، Channel و Saved پاسخ `NotSupported` با sequence/version خالی دارند؛ receipt همه اعضا یا شمار view کانال وجود ندارد.
- event همان `ConversationChanged` است با `type:"PeerReadStateChanged"` و `peerReadThroughSequence`. event به فرستندهٔ مجاز می‌رسد، نه تمام اعضا؛ `ReadCursorAdvanced` خصوصی همچنان فقط برای خود reader است. Delta نیز field nullable مذکور را دارد.
- پس از reconnect، snapshot read-state را تازه و Delta را از checkpoint پردازش‌شده دریافت کنید؛ eventها می‌توانند تکرار/تأخیر داشته باشند. sequence دریافت‌شدهٔ کمتر، state جدیدتر پردازش‌شده را عقب نبرد؛ در Hidden مجوز نمایش تازه را فرض نکنید. checkpoint فقط پس از پردازش کامل صفحه جلو می‌رود.
- receipt version، opaque fact version است؛ آن را Delta cursor یا ExpectedVersion برای POST read استفاده نکنید. قرارداد قدیمی read additive و بدون ExpectedVersion باقی است. ExpectedVersion مربوط به Edit/Settings قرارداد مستقل خودش را دارد.

نمونهٔ پاسخ Direct پیش از peer read:

```json
{"conversationId":"<guid>","peerReadThroughSequence":0,"visibility":"Visible","version":"<opaque>"}
```

خطاها: 401 نشست نامعتبر؛ 404 منبع ناموجود/خارج مجوز؛ POST read با sequence نامعتبر 400. hidden و unsupported پاسخ موفق اما فاقد رسید هستند. download/search/history/Delta همچنان مجوز جاری را مستقل بررسی می‌کنند؛ دانستن MessageId یا receipt اجازهٔ فایل نمی‌دهد.

## فایل، رسانه، جست‌وجو و عملیات پیام

فایل خصوصی از مسیر موجود `POST /api/v1/files` به‌صورت multipart با فیلد `File` بارگذاری و سپس با `attachmentIds` به SendMessage داده می‌شود. فقط FileAsset آماده، مالکیت/مجوز جاری و scope سازگار قابل اتصال‌اند؛ فایلِ فرد دیگر، نام فایل یا شناسه به‌تنهایی مجوز نیست. دریافت bytes فقط `GET /api/v1/files/{id}/content` با session و مجوز فعلی است؛ URL عمومی دائمی یا storage key در پاسخ پیام/Shared Media نداریم. حذف عضو یا از دست‌دادن history/ForMe در لحظهٔ download اعمال می‌شود. فایل پیوست‌شده retained است؛ CHECK موجود SQL اجازهٔ downgrade آن به Pending/Rejected/Deleted را نمی‌دهد. این invariant برای عبور دادن تست تغییر نکرده است.

| MessageKind | MIME و پسوند قابل پشتیبانی کد |
|---|---|
| Image | image/png و .png؛ image/jpeg و .jpg/.jpeg |
| Video | video/mp4 و .mp4 |
| Voice | audio/mpeg و .mp3 |
| File | application/pdf و .pdf؛ text/plain UTF-8 و .txt |

مجاز بودن نهایی به `Storage:AllowedContentTypes` و سقف اندازهٔ همان محیط نیز بستگی دارد؛ Video/Voice با افزودن صرف enum فعال نمی‌شوند. codecهای Ogg/Opus/WebM/WAV یا فایل اجرایی/arbitrary generic-file در این قرارداد ادعا نشده‌اند. signature حداقلی، MIME و extension بررسی می‌شوند؛ بررسی ۵۱۲ بایتِ ابتدای متن UTF-8 اکنون مرز نیمه‌کارهٔ نویسه را با EOF اشتباه نمی‌گیرد، ولی UTF-8 نامعتبر داخل نمونه یا انتهای واقعی ناقص همچنان رد می‌شود. این بررسی decoder رسانه یا اسکن کل فایل نیست؛ **AV/CDR = FUTURE PRODUCTION HARDENING** و هیچ فایل malware-free اعلام نمی‌شود.

`GET /conversations/{id}/media?kind=Image&beforeSequence=...&limit=...` زیر `/api/v1`، history/ForMe/deletion و عضویت فعلی را اعمال می‌کند. limit تعداد **پیام** است؛ یک پیام با چند پیوست می‌تواند چند item بدهد. nextBeforeSequence را مصرف و با `(messageId,fileAssetId)` dedupe کنید؛ تعداد item را جای cursor نگذارید. همهٔ پیوست‌های یک پیام در همان صفحه هستند. ترتیب داخل صفحه بر پایهٔ sequence و شناسهٔ attachment پایدار است.

Reply در SendMessage با `replyToMessageId` همان گفتگو است. پیام اصلیِ حذف‌شده، hidden یا خارج history به reference امن `isDeleted/isUnavailable` و بدون متن/نوع قبلی تبدیل می‌شود؛ وجود MessageId مجوز نقل متن پنهان نیست. Edit با `PATCH /conversations/{id}/messages/{messageId}` و `text/expectedVersion`، sender/current permission، مهلت یک‌ساعتهٔ سرور، نسخه و Safety فعلی را بررسی می‌کند؛ نسخهٔ قدیمی `412` است. revision تاریخی برای کاربر عادی به‌عنوان متن فعلی برنمی‌گردد.

Delete For Me مسیر `DELETE .../messages/{messageId}/for-me`، فقط caller و همهٔ sessionهای خودش را پنهان می‌کند؛ peer/history او تغییر نمی‌کند. Delete For Everyone مسیر `DELETE .../messages/{messageId}/for-everyone?expectedVersion=...` و alias قبلی DELETE پیام است؛ قواعد sender/مهلت یا moderation مجاز فعلی پابرجا هستند. خروجی tombstone نوع/متن/پیوست قبلی ندارد؛ رکورد تاریخی یا شاهد moderation خودکار پاک نمی‌شود. پیام pin‌شدهٔ حذف‌شده ممکن است فقط به‌صورت tombstone قابل شناسایی بماند، نه متن یا فایل افشاشده.

Reaction با POST `.../messages/{messageId}/reactions` و `reaction` و حذف با DELETE `.../reactions/{reaction}` است؛ فقط پیام مجاز و قابل مشاهده، اثر هم‌ارز idempotent، و نتیجهٔ `reactedByCurrentUser` وابسته به خود حساب است. Forward با POST `.../messages/{messageId}/forward` و `destinationConversationId/clientMessageId`، مجوز مبدأ/مقصد، تاریخچه، Block/Privacy، Safety و فایل را دوباره بررسی می‌کند؛ متن/پیوست آماده‌شده پیش از Safety باید با محتوای جاری یکسان باشد. مقصد، جایگزین مجوز نیست. Message Pin با POST/DELETE `.../pins/{messageId}` عمومیِ درون گفتگو و نیازمند permission فعلی است؛ با pin شخصی گفتگو یکسان نیست.

جست‌وجوی موجود `GET /api/v1/messages/search?q=...&conversationId=...&cursor=...&limit=...` فقط گفتگو/پیام/فایل مجاز فعلی و ویرایش فعلی را برمی‌گرداند؛ نسخهٔ قبلی، ForMe، Clear و history غیرمجاز snippet نمی‌سازند. cursor opaque به query/scope بسته است؛ برای query یا گفتگو دیگر reuse نشود. موتور همان SQL bounded و normalization فارسی موجود است؛ موتور جست‌وجوی موازی اضافه نشده است.

زمان‌های core Messaging در receipt تکراری، history، search/media، list، user-state، cursor-state، Group/Channel و realtime اکنون UTC با `Z` هستند. timestamp از ساعت client نمی‌آید؛ تغییر صرفاً بازیابی Kind از دست‌رفتهٔ زمان UTC ذخیره‌شده است و مقدار timestamp/deadline دیتابیس جابه‌جا نشده است.

## Rate limit و قرارداد خطا

| عملیات | سقف هر کاربر در هر دقیقه / هر process |
|---|---|
| Send و Edit، مشترک | ۳۰۰ |
| Add/Remove Reaction، مشترک | ۳۰۰ |
| Forward | ۱۲۰ |
| Search / Discovery | هر policy برابر ۶۰، حفظ‌شده |
| Upload | ۲۰، حفظ‌شده |
| ثبت/لغو دستگاه | ۶۰ |

در Testing سقف‌های named policy برای آزمون عملکردی بالا هستند؛ تست قرارداد 429 با limiter کوچک کنترل‌شده اجرا می‌شود، نه ادعای اندازه‌گیری quota واقعی Production. `Retry-After` پاسخ 429 برای writeهای Messaging و Upload نیز مانند discovery/device ارائه و از CORS expose می‌شود. backoff/retry کلاینت موقت است؛ auth نامعتبر `401`، منبع خارج مجوز عموماً `404`، policy rejection مطابق کد stable قبلی، concurrency مانند Edit برابر `412` یا device برابر `409` است. در 429 توکن را logout/rotate نکنید و ارسال را با همان ClientMessageId/payload retry کنید. کنترل‌های OTP/AI/پرداخت تغییر نکرده‌اند. limiter و SignalR backplane توزیع‌شده خارج این P1 هستند.

## state شخصی، چنددستگاهی و reconnect

Archive از POST/DELETE `/api/v1/conversations/{id}/archive`، pin شخصی از POST/DELETE `.../state/pin`، Mute از PUT/DELETE `.../mute` و Mark Unread از POST `.../mark-unread` مصرف می‌شوند. هرکدام state **حساب** در همان گفتگو هستند، نه state مستقلِ هر tab/session و نه تنظیم کل گفتگو. `filter=Archived` برای فهرست archive است؛ pin شخصی ترتیب caller را تغییر می‌دهد، نه permission پیام را. پیام جدید گفتگو را خودکار از archive خارج نمی‌کند. Mute پیام، unread و SignalR را متوقف نمی‌کند.

Mark Unread فقط `isManuallyUnread` را روشن می‌کند؛ `lastReadMessageSequence` واقعی عقب نمی‌رود و Peer Read قبلی جعل/بازنویسی نمی‌شود. `filter=Unread`، unread واقعی یا manual را نشان می‌دهد. Mark Read واقعی همان manual flag را حتی وقتی cursor از قبل روی همان sequence بوده پاک می‌کند. بنابراین روی `ReadCursorAdvanced` فقط بر اساس مساوی بودن ResourceVersion، refresh user-state را کنار نگذارید؛ ChangeId/cursor رویداد و snapshotهای REST مرجع‌اند.

Read/Delivered خصوصی، Archive/Mute/Pin شخصی، Manual Unread و ForMe به sessionهای مجاز **همان User** می‌رسند؛ peer آن‌ها را در Delta/Hub دریافت نمی‌کند. Peer Read رویداد اختصاصی و محدود خودش را دارد، نه broadcast cursor خصوصی. دستگاه‌ها با GET رجیستری refresh می‌شوند؛ event جدیدِ device-state اختراع نشده است. Reaction عمومیِ مجاز است، اما `reactedByCurrentUser` برای هر حساب از سرور محاسبه می‌شود. ForMe در همهٔ sessionهای خود حساب history/media/download را پنهان می‌کند، نه در حساب peer.

در reconnect، REST فهرست/stateهای جاری و Delta از آخرین checkpoint **پردازش‌شده** را بخوانید. Hub یک invalidation hint با امکان تکرار، تأخیر یا ازدست‌رفتن است؛ نه منبع حقیقت یا تضمین delivery شبکه. پیام pending محلی با ClientMessageId ثابت و payload یکسان retry شود؛ استفادهٔ همان شناسه برای payload متفاوت conflict است. هنگام timeout پس از commit، replay همان receipt/message را recover می‌کند؛ message ID تازه نسازید. ChangeId/cursor برای dedupe و ترتیب پردازش رویداد است؛ دریافت hint به‌تنهایی مجوز جلو بردن checkpoint نیست.

History و Shared Media از beforeSequence انحصاری، Search از cursor query-bound و Delta از cursor conversation-bound استفاده می‌کنند. ورود پیام جدید بین صفحات history، بخش قدیمیِ قبل از anchor را جابه‌جا نمی‌کند؛ برای پیام‌های تازه از Delta/head refresh استفاده کنید. conversation/member/device listهای offset-based snapshot تراکنشی چندصفحه‌ای نیستند؛ در traversal آرام tie-breaker پایدار دارند، اما پس از mutation/فعالیت جدید از page=1 refresh و بر اساس شناسه dedupe کنید. ادعای بدون‌تغییر بودن چندصفحه در برابر همهٔ mutationهای هم‌زمان نمی‌شود.

revoke یک session باید REST آن را 401 و Subscribe/handshake Hub آن را نامعتبر کند؛ session دیگر همان حساب مستقل می‌ماند. مخاطب event هنگام dispatch دوباره از SQL resolve می‌شود، حتی اگر connection قدیمی هنوز بسته نشده باشد. client پس از 401 session منقضی، cache خصوصی را طبق lifecycle ورود خودش پاک کند؛ Guardian/Manager از داشتن شناسه یا رابطه، مجوز مرور Direct خصوصی نمی‌گیرند. از اتصال، read cursor یا recent contact، Typing/Presence/Last Seen نسازید.

پذیرش محلی این قرارداد با دو ورود OTP آزمایشی مستقل، دو UserSession در SQL و Hub واقعی TestServer/LongPolling انجام شده است؛ دستگاه فیزیکی، سرویس‌ورکر واقعی و deploy تست نشده‌اند. سناریوهای بیشتر و ماتریس P1-T01 تا P1-T35 در [ممیزی پذیرش](MESSENGER_P1_DELIVERY_AUDIT.md) و گزارش آموزشی در [گزارش توسعه‌دهنده](MESSENGER_P1_DEVELOPER_REPORT.md) هستند.

## اعلان، Mute و تلاش مجدد

اعلان داخلی و Push یک state نیستند. ترجیحات از API موجود `/api/v1/notification-preferences` و category `Messages` مصرف می‌شوند؛ تنظیم شخصی و هر سازمان مستقل‌اند، In-app خاموش نمی‌شود. Quiet Hours با timezone صریح، ارسال خارجی را عقب می‌اندازد. category مؤثر Mention/Reply از pipeline موجود است؛ هیچ‌کدام Mute را خودکار دور نمی‌زنند.

Mute دائم/Until، پیام و unread و SignalR را قطع نمی‌کند؛ worker پیش از هر تلاش، Mute/Preferences و مجوز جاری منبع/دستگاه را دوباره می‌سنجد. تغییر آن‌ها پس از زمان‌بندی نیز مؤثر است. preview همه حساب‌ها، از جمله Minor/Unknown، عمومی و بدون متن پیام، شماره، نام فایل، سن، رابطه والد یا دلیل safety است.

`NotificationId` هویت منطقی پایدار است؛ retry پیام با همان ClientMessageId/payload اعلان داخلی دوم نمی‌سازد. شبکه دقیقاً یک‌بار نیست و client باید اعلان تکراری با همان شناسه/tag را تحمل کند. enum قدیمی Delivered در delivery داخلی به معنای پذیرش provider است؛ آن را تیک خواندن پیام نکنید. receipt پیام از SQL و peer-read فقط از قرارداد بخش قبل می‌آیند.

خطای موقت provider با backoff و سقف تلاش دوباره پردازش می‌شود. Retry-After رعایت می‌شود؛ invalid destination لغو می‌شود و retry بی‌نهایت ندارد. failure، اصل Message/Notification را حذف نمی‌کند. لغو session یا دسترسی، ارسال آینده را می‌بندد؛ اطلاعاتی که قبلاً به دستگاه/شبکه تحویل شده قابل پس‌گرفتن تضمینی نیست.

availability امن Backoffice فقط `Configured`، `PartiallyConfigured` یا `Unavailable` است؛ مثلاً تنظیم معتبر فقط Web و نه FCM می‌تواند وضعیت جزئی بدهد. این metadata، سلامت credential در سرویس بیرونی یا رسیدن Push به دستگاه را ثابت نمی‌کند. token/subscription فعال حساب دیگر به‌طور خودکار هنگام تعویض حساب منتقل نمی‌شود و conflict برابر 409 است؛ پیش از logout مقصد متعلق به حساب قبلی را طبق lifecycle رجیستری لغو و با session حساب جدید ثبت کنید.
