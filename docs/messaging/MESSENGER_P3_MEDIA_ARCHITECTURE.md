# معماری پیشنهادی رسانهٔ زندهٔ Messenger — P3-0

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

> **مرجع جاری پس از تأیید مالک — ۲۰۲۶-۱۰-۱۷:** [P3 Media Policy v1](MESSENGER_P3_APPROVED_MEDIA_POLICY_V1.md) اکنون APPROVED_FOR_IMPLEMENTATION است؛ [نگاشت تأییدها](MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md#p3-policy-v1-owner-approval) مشخص می‌کند کدام پیشنهاد تاریخی جایگزین شده است. متن این ممیزی برای حفظ شواهد P3-0 باقی می‌ماند؛ مقدارهای فعلی عبارت‌اند از initial TTL≤30s، ring30s، network grace15s برای هر دو media/signaling، ManageCalls برای Group و ManageLive برای Live، cap pilot Video8/Voice16 و ماتریس Child Safety رابطه‌محور مصوب. ترتیب فعلی S→A→B→C→D→E→F است؛ [برنامهٔ P3-S](MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_PLAN.md) آماده می‌شود ولی REAL rejoin gate هنوز NOT_TESTED و Production approval داده نشده است. نتیجهٔ تاریخی این سند را وضعیت امروز policy یا تماس پیاده‌شده تلقی نکنید.

تاریخ ممیزی: `2026-10-17`؛ مبنا: `5e1da895c96065380e12d04341e483d4e78539c4`، شاخهٔ `messenger/p0` و upstream موجود `origin/main`. این سند **معماری پیشنهادی و ممیزی** است؛ تماس، endpoint، مدل، migration، media SDK، TURN یا استقرار تازه ایجاد نشده است. پیشنهادهای محصول فقط پس از تصمیم صریح مالک معتبر می‌شوند؛ [ثبت تصمیم‌ها](MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md) مرجع وضعیت هر تصمیم است.

## ۱. منابع، اعتبار و baseline

Master PRD v3.0 چهارده‌صفحه‌ای [PDF مرجع](MakanApp_Messenger_Master_PRD_v3.0_FA.pdf)، به‌خصوص فصل ۱۷/صفحهٔ ۸، target مصوب است، نه مدرک implementation. SHA256 فایل: `22ADBBFE2DE53B168CDF964FB2FA3B907057CB9780237F011D8634921B8A1BBD`. قواعد سخت‌گیرانهٔ [GAP-05](../architecture/GAP_05_MESSENGER_CHILD_SAFETY.md)، [تصمیم‌های Messenger](../architecture/MESSENGER_V2_PRODUCT_DECISIONS.md)، [مرز Backoffice](../architecture/BACKOFFICE_PRODUCT_DECISIONS.md)، AGENTS و SQL Server override حفظ می‌شوند. تأیید دوبارهٔ Child Safety قبلی خواسته نمی‌شود؛ سیاست خاص تماس زنده هنوز از آن استنتاج نمی‌شود.

هر سه Handoff، Delivery Audit و Developer Report از P0/P1/P2 و [راهنمای API](../backend-api-guide.md) خوانده شدند. بندهای تاریخی راهنما دربارهٔ نبود refresh، CORS باز، نبود Presence یا Safety provider جاری نیستند؛ source و افزوده‌های جدید Handoffها مرجع‌اند. این اختلاف‌های تاریخی در این مرحله بازنویسی نشده‌اند.

Baseline دوباره اجرا شد: `dotnet restore MakanApp.sln` موفق؛ `dotnet build MakanApp.sln --configuration Debug --nologo` با صفر warning/error؛ suite کامل `--no-build --logger trx --results-directory .artifacts/messenger-p3-0/baseline` برابر **558 Unit + 1023 Integration + 14 Architecture = 1595 passed، failed=0، skipped=0**. Integration حدود ۶:۲۰ بود. SQL فقط fixtureهای ایزولهٔ تست است؛ دیتابیس عادی و سرور تغییر نکردند. P0/P2 تحویل کد دارند؛ P1 همچنان `MESSENGER_P1_CODE_COMPLETE / P1_PUSH_RUNTIME_PENDING` است.

## ۲. ممیزی realtime موجود و مرز reuse

مسیرهای زیر نسبت به ریشهٔ مخزن‌اند و implementation truth این مبنا هستند:

| منبع فعلی | رفتار واقعی | استفادهٔ ممکن و محدودیت P3 |
|---|---|---|
| `MakanApp.Api/Realtime/MessagingHub.cs` و `IMessagingHubClient.cs` | `/hubs/messaging`، `[Authorize]`، Subscribe/UnsubscribeConversation، SetTyping، Heartbeat، Subscribe/UnsubscribePresence؛ eventهای ConversationChanged/TypingChanged/PresenceChanged | زیرساخت کنترل و notification؛ هیچ StartCall/SDP/media forwarding ندارد |
| `MakanApp.Api/Authentication/SessionAuthenticationHandler.cs` | Bearer opaque از header، UserId/SessionId از Identity؛ query `access_token` مصرف نمی‌شود | REST و LongPolling فعلی قابل reuse؛ WebSocket مرورگرِ token-query را موجود فرض نکنید |
| `SignalRMessagingRealtimeNotifier.cs` و `MakanApp.Infrastructure/Messaging/MessagingRealtimeAudienceResolver.cs` | مخاطب SQL جاری، هر دو expiry نشست، account، workspace، نقش/عضویت و visibility دقیق همان event از Delta؛ ارسال به session-group | الگوی audience، نه کپی بی‌تغییر resolver پیام برای call event فاقد Message/Delta |
| `EfMessagingStore.Sync.cs` و `MessagingRealtimeOutboxDispatcher.cs` | cursor گفتگو-bound، FromJoin/Clear/ForMe، private audience؛ outbox، claim/retry محدود؛ شبکه دقیقاً یک‌بار نیست | زیرساخت durable invalidation و recovery؛ snapshot تماس و expiry زنگ باید قرارداد مستقل داشته باشند |
| `MessagingEphemeralService.cs`، `InMemoryMessagingEphemeralState.cs` و `EfMessagingStore.Ephemeral.cs` | Typing با TTL، Presence چنداتصالی، privacy مقصد، state فقط یک process | hint و registry اتصال؛ **نه مرجع پذیرش تماس، busy یا call history**؛ backplane/shared presence نداریم |
| `MakanApp.Domain/Identity/UserSession.cs` | UserId، DeviceName، access/absolute expiry، revoke، workspace و rowversion؛ refresh access را در همان session می‌چرخاند | هویت نشست موجود reuse؛ ConnectionId و DeviceName هویت امن دستگاه فیزیکی نیستند |
| `MakanApp.Domain/Notification/PushDeviceRegistration.cs` | مقصد Web/Android/Ios، UserSessionId، token hash، revoke و rowversion؛ refresh می‌تواند session ثبت‌کننده را عوض کند | مقصد Push، نه attested device؛ Push token≠توکن ورود یا اجازهٔ AcceptCall |
| `NotificationDeliveryProcessor.Authorization.cs` و WebPush/FCM providerهای Infrastructure | بازبینی account/preference/mute/quiet-hours/resource/device، retry و preview کمینه | transport و lease reuse؛ category، deadline کوتاه و lifecycle incoming-call هنوز وجود ندارند؛ SMS OTP مصرف نشود |
| `MessagingPrivacyPolicy.cs`، `EfMessagingPrivacyResolver.cs` و `MessagingPrivacySettings.cs` | Everyone/Contacts/Nobody؛ Contacts یعنی دفترچهٔ مقصد؛ Online/LastSeen پیش‌فرض Contacts | مدل audience قابل reuse؛ **WhoCanCallMe فعلاً وجود ندارد**؛ DirectMessagesAudience مجوز تماس نیست |
| `EfMessagingStore.Safety.cs` و `MessengerRelationshipQuery.cs` | Block دوطرفه در Direct، روابط جاری کلاس/Guardian/رضایت؛ عضویت رسمی از Enrollment/TeacherAssignment | predicate رابطه و concealment reuse؛ رابطه شرط لازم است، نه رضایت تماس زنده |
| `ConversationAdminPermission.cs` / `GroupMemberCapability.cs` | Admin mask تا All=8191؛ ManageMembers/RemoveMembers/ManageAdmins/PublishMessages/ManageInvites و غیره؛ member: SendMessages/SendMedia/InviteMembers | اختیار متن/فایل، خودکار اختیار broadcast یا دستکاری میکروفن نیست |
| `InternalMakanContentSafetyProvider.cs` / `MessageContentSafetyGate.cs` | قواعد محدود متن؛ رسانه unsupported به ReviewRequired؛ protected failure fail-closed | ایمنی عنوان/دعوت متنی reuse؛ **اسکن realtime صدا/تصویر یا مجوز دورزدن Safety نیست** |
| Platform moderation موجود | پروندهٔ محدود، MFA/step-up GAP-04 و AdminAudit؛ بدون private browsing عمومی | رسیدگی metadata/report مجاز؛ نه شنود تماس، ضبط پنهان یا MFA موازی |

جست‌وجوی `CallSession/StartCall/AcceptCall/WhoCanCallMe/LiveKit/mediasoup/Coturn/RTCPeerConnection` در چهار پروژه و tests نتیجه‌ای نداشت؛ snapshot EF نیز call/media room ندارد. آخرین migration `20261017033804_AddConversationChecklists` است. `MessageKind.Voice/Video` فقط پیوست ذخیره‌شده است. هیچ تست فعلی، انتقال RTP یا تماس واقعی را ثابت نمی‌کند.

شواهد reuse: `MessagingP0RealtimeAuthorizationTests`، `MessagingP1MultiDeviceAcceptanceTests`، `MessagingP2EphemeralEndpointsTests`، `MessagingContactsPrivacyDiscoveryEndpointsTests` و `MessagingChildSafety*Tests`؛ به‌ویژه revoked session، expiry، دو session، Block بعد از enqueue و لغو رابطهٔ Guardian. این‌ها baseline اجراشده‌اند، نه تست تماس جدید.

## ۳. دو مسیر مستقل و معماری Onion

پیشنهاد `R01/R02`: **SFU برای همهٔ تماس‌ها، حتی 1:1**؛ Backend همچنان Modular Monolith .NET 9/Controllers/SQL Server است. media server ابزار زیرساختی است، نه تجزیهٔ دامنه به microserviceها.

| مسیر | مسئولیت |
|---|---|
| Client → HTTPS REST / SignalR → Application → Domain/port → Infrastructure → SQL | هویت، privacy، Block، GAP-05، state/participant، ownership، پذیرش رقابتی، history کمینه، audit و صدور مجوز کوتاه‌عمر |
| Client → SDK signaling رسانه روی WSS → SFU | negotiation و کنترل transport خود provider؛ SDP/ICE در Hub دوم تکرار نشود |
| Client → WebRTC مستقیم به SFU، یا Client → TURN → SFU → participant مجاز | ICE، NAT، DTLS-SRTP، RTP/RTCP، forwarding، adaptation و simulcast/SVC مطابق platform |

فریم صوت/تصویر نه از Controller، نه از Hub و نه از SQL عبور کند. Domain پیشنهادی فقط state/invariant؛ Application مالک Call use case و port؛ Infrastructure مالک EF/provider adapter/secret و reconciliation؛ Api مالک Controller نازک و hintهای SignalR است. در P3-0 هیچ‌کدام ساخته نشده‌اند.

### P2P، mesh و SFU

| گزینه | مزیت | هزینه/ریسک و توصیهٔ این ممیزی |
|---|---|---|
| 1:1 P2P + TURN fallback | مسیر مستقیم ممکن است server egress کمتری داشته باشد | NAT/ICE و reconnect مستقل؛ افشای آدرس شبکه به peer؛ لغو مجوز از سمت سرور نمی‌تواند peer بدخواه را تضمینی قطع کند؛ ارتقا به گروه مسیر دوم می‌خواهد |
| 1:1 SFU | همان مسیر گروه، کنترل room و eviction، peerها به یکدیگر متصل نمی‌شوند | پهنای باند و وابستگی به SFU بیشتر؛ SFU IP کلاینت را می‌بیند؛ E2EE فرض نیست؛ **توصیهٔ اصلی مشروط** |
| Group mesh | بدون SFU برای نمونهٔ بسیار کوچک | برای n نفر upload هر نفر تقریباً `(n−1)b`؛ بار دستگاه و اتصال‌ها رشد می‌کند؛ برای هدف گروه/Live انتخاب نشود |
| Group SFU / Channel Live | ارسال هر publisher به SFU و subscription انتخابی؛ کنترل speaker/viewer | هزینهٔ egress، ظرفیت هر room و عملیات media نیازمند benchmark؛ گروه صوت، ویدئو، screen track و Live از همین زیرساخت |

TURN حتی با SFU لازم است؛ direct-to-SFU نیز ممکن است پشت firewall برقرار نشود. هیچ ادعای برتری کیفیت در شبکهٔ ایران بدون اندازه‌گیری نداریم. مبنای فنی: [WebRTC TURN](https://webrtc.org/getting-started/turn-server) و [تعریف SFU در mediasoup](https://mediasoup.org/documentation/overview/). این انتخاب تحلیل پروژه است، نه توصیهٔ قطعی vendor.

## ۴. مقایسهٔ platformها — بدون نصب یا انتخاب نهایی

منابع رسمی آنلاین در `2026-10-17` بررسی شدند؛ نسخهٔ executable/SDK باید در spike آینده pin و دوباره بررسی شود، نه اینکه صفحهٔ rolling به binary مستقر نسبت داده شود.

| گزینه | پیچیدگی و .NET | React / React Native | Voice/Video/Group/Share/Live و ضبط آینده | TURN، scale، observability، deployment و ریسک |
|---|---|---|---|---|
| Self-hosted LiveKit | متوسط؛ .NET adapter روی HTTPS RoomService/Twirp و JWT استاندارد؛ SDK مدیریت .NET در فهرست رسمی بررسی‌شده دیده نشد؛ community SDK نیازمند review مستقل | JS/React و RN رسمی؛ native setup و دستگاه واقعی لازم | room/track برای هر پنج هدف؛ Egress سرویس جدا برای recording/stream export، فعلاً خاموش | TURN داخلی integrated؛ Linux media host، metrics و مدیریت cluster؛ Redis برای distributed؛ CPU/NIC و تک‌گرهی بودن هر room محدودیت؛ Apache-2.0 core، عملیات/upgrade با ما |
| Self-hosted Janus | زیاد؛ API HTTP/WebSocket و plugin/handle lifecycle؛ .NET REST adapter ممکن، نه media داخل .NET | janus.js برای وب؛ RN نیازمند bridge/WebRTC integration و پذیرش مستقل، هم‌ارز SDK RN آماده فرض نشود | VideoRoom SFU برای گروه/Live و screen track؛ AudioBridge در صورت نیاز mixing؛ recording خام/plugin و postprocessing، نه workflow رضایت آماده | Coturn خارجی و routing/room placement/metrics integration بیشتر؛ Linux و dependencyهای native؛ GPLv3 و بررسی حقوقی توزیع/قرارداد؛ عملیات زیاد |
| Self-hosted mediasoup | بسیار زیاد؛ library سطح پایین Node.js/C++ یا Rust، نه سرور آمادهٔ C#؛ به media adapter/host جدا نیاز دارد | mediasoup-client وب؛ RN با react-native-webrtc و registerGlobals | SFU و broadcast/track قابل ساخت؛ recorder با pipeline خارجی؛ call product/signaling را تیم می‌سازد | TURN جدا، worker/router placement و metrics با تیم؛ ISC؛ کنترل بیشتر در برابر سطح نگه‌داری بزرگ‌تر |
| Custom signaling + Coturn + P2P | برای demo کم، برای محصول امن زیاد؛ کنترل از .NET ولی SDP/ICE/retry/گوشه‌های protocol با تیم | WebRTC وب و RN native bridge؛ رفتار چندسکویی را باید ساخت | 1:1 و share ممکن؛ Group/Live با صرف Coturn به SFU تبدیل نمی‌شود؛ recording جدا | relay عمومی، auth/quota/abuse و telemetry با ما؛ Coturn BSD-3-Clause، وابستگی browser/native؛ fallback جامع P3 نیست |
| Managed LiveKit Cloud | عملیات زیرساخت کمتر، همان adapter/SDK خانواده؛ backend authority حذف نمی‌شود | همان خانوادهٔ client SDK | نیازهای room/track و سرویس‌های managed؛ recording همچنان نیازمند approval | revocation امکانات متفاوت از self-hosted؛ هزینه/egress/region/vendor؛ دسترسی تجاری، قرارداد، data residency و دسترسی ایران باید قبل خرید واقعاً تأیید شوند |

مراجع هر ردیف: [LiveKit SDK catalog](https://docs.livekit.io/reference/)، [RoomService API](https://docs.livekit.io/reference/other/roomservice-api/)، [LiveKit core license](https://github.com/livekit/livekit)، [Egress مستقل](https://docs.livekit.io/transport/self-hosting/egress/)، [Janus VideoRoom](https://janus.conf.meetecho.com/docs/videoroom.html)، [Janus transport](https://janus.conf.meetecho.com/docs/rest.html)، [Janus platform/license](https://janus.conf.meetecho.com/docs/README.html)، [mediasoup scaling](https://mediasoup.org/documentation/v3/scalability/)، [RN integration](https://mediasoup.org/documentation/v3/mediasoup-client/react-native/)، [ISC](https://mediasoup.org/license/)، [Coturn license](https://github.com/coturn/coturn/blob/master/LICENSE)، [Self-hosted/Cloud differences](https://docs.livekit.io/transport/self-hosting/).

**پیشنهاد primary: LiveKit self-hosted، مشروط به gate امنیتی بخش ۶. fallback: Janus VideoRoom + Coturn، فقط پس از spike native-client، authorization و license.** fallback تعویض خودکار حین یک تماس نیست؛ adapter/قرارداد/پذیرش مجدد لازم دارد. Managed گزینهٔ تجاری مستقل است، نه راه خودکار دورزدن gate یا تصمیم حقوقی. نصب Janus/Node/SDK، خرید سرویس و ایجاد service جدید در این مرحله مجاز نیست.

## ۵. NAT، STUN/TURN و topology هدف

Backend فعلی طبق [runbook IIS](../production/DEPLOYMENT_IIS.md) Windows/IIS و انتشار تاریخی HTTP با استثنای صریح است. دسترسی جاری سرور، TLS، IP، firewall یا ظرفیت از راه دور بررسی نشده‌اند. استثنای HTTP مجوز media insecure یا credential در شبکه نیست؛ real-call gate به HTTPS/WSS معتبر نیاز دارد. [W3C Media Capture](https://www.w3.org/TR/mediacapture-streams/) API capture را به secure context محدود می‌کند.

پیشنهاد: Windows Backend/SQL خصوصی حفظ؛ یک Linux SFU مستقل از IIS/SQL و یک endpoint TURN با IP عمومی/گواهی مستقل. برای pilot، TURN داخلی LiveKit روی همان **media host** مجازِ پیشنهادی است، نه روی ماشین Backend. به‌خاطر دو listener TLS روی 443، IP دوم یا L4 LB مناسب برای TURN لازم است؛ reverse proxy معمولی HTTP، TURN را proxy نمی‌کند. جداسازی physical TURN در رشد، نیازمند تصمیم و benchmark است؛ اجبار به Coturn موازی کنار TURN داخلی نداریم.

| مسیر گزینهٔ LiveKit بررسی‌شده | پورت/شرط آینده، نه تنظیم اعمال‌شده |
|---|---|
| API/SignalR ماکان و SDK signaling رسانه | public TCP443 با TLS؛ LiveKit داخلی7880 فقط پشت proxy/LB و admission boundary، نه public admin API |
| ICE/UDP مستقیم SFU | یا UDP50000–60000، یا UDP7882 mux؛ این دو حالت جایگزین‌اند، range در mux استفاده نمی‌شود |
| ICE/TCP fallback | TCP7881 روی media host؛ با TURN/TLS یکی نیست |
| STUN / embedded TURN/UDP | UDP3478؛ امکان UDP443 طبق طراحی firewall، نه پیش‌فرض اجباری ماکان |
| TURN/TLS | public TCP443 روی endpoint مستقل؛ backend TURN5349 پشت L4 LB؛ بدون LB طبق مرجع باید listener443 تنظیم شود |
| مدیریت، SQL و metrics | فقط شبکهٔ مدیریتی/خصوصی؛ هیچ admin secret یا SQL port عمومی |

اعداد فقط برای گزینهٔ LiveKit و مرجع فعلی‌اند؛ [جدول رسمی ports](https://docs.livekit.io/transport/self-hosting/ports-firewall/) و [TLS/TURN deployment](https://docs.livekit.io/transport/self-hosting/deployment/). در fallback Coturn، listener متعارف3478 UDP/TCP و TLS5349 یا public443 و range relay محدودِ مصوب لازم است؛ range با ظرفیت تعیین می‌شود و **range LiveKit را به Coturn نسبت ندهید**. IP عمومی ثابت/1:1 mapping، advertised candidate صحیح، فایروال ورودی و خروجی متناظر، گواهی CA معتبر و تمدید/هشدار انقضا پیش‌نیازند.

TURN ناشناس/open relay ممنوع؛ embedded auth همان provider reuse شود. برای Coturn، credential کوتاه‌عمر timestamp-bound با shared secret فقط server-side و شناسهٔ opaque، quota/bandwidth/allocation cap، منع relay به مقصدهای خصوصی/loopback/management و محافظ reflection لازم‌اند. credential موقت room authorization نیست و باید caller مجاز آن را بگیرد؛ secret دائمی هرگز client/log نمی‌شود. [Coturn credential protocol](https://github.com/coturn/coturn/blob/master/README.turnserver). UDP مسیر ترجیحی تجربی است؛ TCP/TLS fallback می‌تواند latency و head-of-line افزایش دهد؛ هیچ تضمین اتصال همهٔ ISPها نداریم.

STUN برای کشف mapping/candidate در ICE است، نه انتقال رسانه، احراز participant یا جایگزین TURN. endpoint تحت کنترل استقرار/همان provider ترجیح دارد؛ عمومی‌بودن سرویس ثالث را فرض نکنید. در NAT/firewall ناسازگار، صرف دریافت candidate عمومی کافی نیست و relay واقعی باید در آزمون انتخاب شود. مانیتور expiry credential، allocation failure، relay bytes و saturation جدا از موفقیت HTTP Backend باشد.

در scale-out LiveKit، Redis طبق مرجع برای routing/message bus لازم است؛ این store زیرساخت media است، نه جایگزین SQL Server و نه اثبات shared presence/backplane ماکان. room باید در یک node جا شود؛ cluster بیشتر الزاماً ظرفیت یک Live بزرگ را زیاد نمی‌کند. [مرز distributed LiveKit](https://docs.livekit.io/transport/self-hosting/distributed/).

## ۶. media grant، revocation و شرط انتخاب primary

پیشنهاد grant: roomOpaqueId، participantOpaqueId متناظر **CallDeviceSession برنده**، publish/subscribe و sourceهای صریح؛ Voice فقط microphone، Viewer فقط subscribe و publishData=false. roomCreate/list/admin/record/ingress مجوز client نیستند. نام واقعی/تلفن/مدرسه/سن در room/identity/metadata provider قرار نگیرد. امضای استاندارد JWT در Infrastructure و کلید provider فقط secret store؛ access token ماکان همچنان همان opaque Identity است.

**کشف مهم ممیزی:** مرجع فعلی LiveKit، revocation توکن را Cloud-only می‌داند. در self-hosted، RemoveParticipant/UpdateParticipant توکن قبلی را باطل نمی‌کنند؛ server token را refresh می‌کند و exp فقط initial connect را محدود می‌کند. بنابراین TTL کوتاه یا ندادن token تازه در Backend، به‌تنهایی منع rejoin یا لغو active transport نیست. [token lifecycle رسمی](https://docs.livekit.io/frontends/reference/tokens-grants/).

`R17` یک gate اجباری قبل پذیرش امنیتی integration است، نه قابلیت موجود: prototype یک admission boundary برای **تمام join/rejoin/full reconnect** و enforcement قطع participant فعال در media layer باید ساخته/تست شود. پیشنهاد spike: SDK signaling endpoint فقط از gateway کنترل‌شده قابل دسترس باشد، token امضاشده به room/identity نگاشت و نشست/CallDeviceSession/policy epoch جاری از authority سرور بررسی شود؛ unknown/refreshed token ناسازگار fail-closed و مسیر مستقیم7880 قابل bypass نباشد. gateway فریم RTP عبور نمی‌دهد و SDP/ICE را در Application دوباره طراحی نمی‌کند. سازگاری gateway با refresh داخلی SDK، resumed connection و failover **اثبات نشده است**؛ صرف proxy عمومی HTTP این کنترل را ایجاد نمی‌کند.

لغو باید در SQL epoch/Denied را اتمیک ثبت، صدور grant را قطع، و فرمان idempotent eviction/permission-update پس از commit ارسال کند؛ outbox/reconciliation با deadline محدود اثر provider را تأیید کند. رویداد signed provider صرفاً مشاهده است؛ webhook async به‌تنهایی مانع دریافت حتی یک فریم نامجاز نمی‌شود. race grant×Block/accept×revoke و فاصلهٔ commit→evict اندازه‌گیری و با budget امنیتی مصوب بسته شود. اگر enforcement با نسخهٔ انتخابی و ریسک مصوب قابل اثبات نبود، primary **GO نیست**؛ fallback فقط پس از اثبات همین invariantها یا پیشنهاد platform سازگار به مالک. این نتیجه audit را متوقف نمی‌کند، ولی تحویل تماس با guard خیالی را ممنوع می‌کند. Janus HMAC token نیز revoke‌پذیری فوری تضمین نمی‌کند؛ [محدودیت auth Janus](https://janus.conf.meetecho.com/docs/auth.html).

## ۷. کوچک‌ترین مدل پیشنهادی و state machine

همهٔ اسم‌های این بخش **طرح آینده‌اند، نه کلاس/جدول موجود**:

| مدل | دادهٔ لازم و invariant پیشنهادی |
|---|---|
| CallSession | Id، ConversationId الزامی برای Group/Live و اختیاری برای 1:1، scope/OrganizationId سروری، InitiatorUserId و target ثابت 1:1، mode=Voice/Video/GroupVoice/GroupVideo/Live، State، Result، Created/Answered/Ended UTC، deadline زنگ، Revision/rowversion و room mapping خصوصی |
| CallParticipant | کلید(CallId,UserId)، role محدود، وضعیت Invited/Accepted/Joined/Left/Removed، AcceptedCallDeviceSessionId، زمان‌ها؛ participant تماس جای ConversationParticipant/Membership نیست |
| CallDeviceSession | شناسهٔ server-minted، CallId/UserId/UserSessionId و connection binding موقت، winning/closed status، grant epoch و expiry؛ دو tab یک session باید قابل تفکیک باشند؛ Push registration اختیاری مقصد است، نه authority |

history یک projection از همین داده است؛ جدول event-sourcing/CallStateHistory از پیش لازم نیست. فقط transitionهای مهم و اثر post-commit از audit/outbox نسخه‌دار موجود با قرارداد جدید استفاده کنند؛ room/webhook reference کمینه برای dedupe/reconcile نیاز واقعی است. SQL Server مرجع state، PK/FK مرکبِ call+participant+device و scope، CHECK UTC/terminal، unique idempotency `(InitiatorUserId,ClientOperationId)` و hash، rowversion و قفل کوتاه ترتیب‌دار لازم‌اند. هیچ migration اکنون ساخته نشود. busy reservation اگر تصویب شد باید در SQL برای Userها با ترتیب Guid قطعی سریال شود؛ lock حافظه کافی نیست.

پیشنهاد state کمینه: `Ringing → Connecting → Active → Ended`؛ Created فقط داخل transaction ساخت است و public state جدا لازم ندارد. `Result` نهایی یکی از Completed/Missed/Declined/Busy/Cancelled/Failed/Revoked است؛ Missed و Declined state موازیِ Ended نمی‌شوند. Active یعنی هر دو participant موردنیاز طبق مشاهدهٔ معتبر provider Joined شده‌اند، نه Accept HTTP یا ادعای client؛ کیفیت شنیدن انسان با آن اثبات نمی‌شود.

| رخداد | از → به / نتیجه |
|---|---|
| Start مجاز Direct | ایجاد Ringing با deadline سرور و attempt idempotent |
| نخستین Accept مجاز قبل deadline | Ringing→Connecting؛ ثبت winner همان transaction؛ grant بعد از recheck |
| Decline / Cancel قبل Accept | Ringing→Ended/Declined یا Cancelled؛ هیچ token/media صادر نشود |
| deadline بدون Accept | Ringing→Ended/Missed؛ دریافت Push/ACK دستگاه به معنی Answer نیست |
| busy واقعی طبق reservation مصوب | پایان بدون ring با Busy؛ response عمومی علت خصوصی فرد را فاش نکند |
| join معتبر هر دو طرف | Connecting→Active؛ AnsweredAt زمان Accept و ConnectedAt کمینه در صورت نیاز جدا؛ duration متصل از Active تا Ended، نه مدت زنگ |
| failure اتصال قبل Active | Connecting→Ended/Failed پس از deadline؛ history مدت رسانه صفر |
| End / revoke / پایان grace رسانه | Connecting یا Active→Ended با نتیجهٔ درست؛ grantها قطع و media teardown |
| event/retry دیررس | Ended هرگز دوباره باز نمی‌شود؛ session جدید تماس تازه با operation تازه |

Group/Live پس از Start مستقیم Connecting هستند؛ join نخست مجاز و media آماده، Active می‌کند. subscriberها به‌صورت انبوه ring نمی‌شوند؛ اعلان opt-in با deadline و Join صریح پیشنهاد است. پایان با Host/مجوز مدیریت یا idle deadline مصوب؛ آخرین عضو یا انتقال host سیاست مشخص `R08/R09` می‌خواهد.

## ۸. جریان 1:1 و چنددستگاهی

StartCall از A → احراز نشست و trusted context → بررسی هر دو account، privacy مستقل B، Block دوطرفه و رابطه/سیاست سنی → transaction ساخت CallSession/دو participant/intent زنگ با deadline → notification فقط session/device مجاز B → B با نشست احرازشده Accept → زیر lock همان CallSession و participant، recheck state/deadline/session/privacy/relation → CAS ثبت تنها winner → commit → grant برنده و caller پس از recheck → WebRTC/SFU → Active → End/teardown → history کمینه.

پیشنهاد تکمیلی R20: شروع 1:1 از profile/contact مجاز به ایجاد Direct پیام وابسته نباشد. `DirectMessagesAudience=Nobody` یا نداشتن SendMessages نباید خودکار WhoCanCallMe مجاز را بی‌اثر یا معکوس کند؛ predicate تماس مستقل و stricter guardهای مشترک reuse شوند، نه فراخوانی کور EnsureCanSendAdvancedAsync. تماس با target مجاز، Conversation پیام، Contact یا دسترسی تاریخچهٔ آن را خودکار نسازد. ConversationId اختیاری اگر supplied شد باید canonical زوج/همان scope و قابل‌خواندن باشد؛ بدون آن هم scope از workspace سروری و طرف‌ها ثابت‌اند. Group/Live همیشه conversation-bound باقی می‌مانند. این استقلال پیشنهاد جدید است و به approval R20 نیاز دارد.

اولین **Accept معتبرِ commit‌شده** برنده است؛ همان device retry همان receipt می‌گیرد، دیگر دستگاه‌ها CallAnsweredElsewhere و هیچ grant مستقل دریافت نمی‌کنند. Accept×Cancel، Accept×Decline و Accept×Timeout یک نقطهٔ خطی‌شدن SQL دارند. caller نیز فقط device آغازگر leg فعال دارد؛ handoff به device دیگر و call waiting در این نسخه توصیه به defer دارند. decline از یک device به‌عنوان رد کل دعوت، busy یک تماس فعال برای هر User و منع تماس با خود/Saved پیشنهادهای `R14` هستند، نه سیاست موجود.

Ringing همهٔ sessionهای **فعلاً eligible** به معنی همهٔ حساب‌ها یا افشای Online نیست. دستگاه offline فقط Push معتبر، بدون room grant، دریافت می‌کند؛ اگر اعلان پس از deadline برسد، snapshot وضعیت Ended را می‌دهد و دوباره زنگ نمی‌زند. caller/callee disconnect در Ringing deadline را عقب نمی‌اندازد. در Active، قطع SignalR به‌تنهایی قطع رسانه نیست؛ revoke/Block یا policy denial grace ندارند.

## ۹. privacy و Child Safety — بدون approval ضمنی

WhoCanCallMe با همان PrivacyAudience سه‌تایی پیشنهاد می‌شود؛ adult verified پیش‌فرض Contacts **از دفترچهٔ مقصد**، protected پیش‌فرض Nobody و feature gate بسته تا تصمیم تماس کودک. Everyone رابطهٔ لازم را حذف نمی‌کند؛ تماس مجاز به معنی دسترسی Contacts، LastSeen یا گفتگوی شخص ثالث نیست. GUID شناخته‌شده و وجود Direct جای Call authorization نمی‌گیرند. Block جدید در هر دو جهت Direct تازه را منع می‌کند؛ پایان تماس Direct فعال هنگام Block پیشنهاد مستقل `R15` است. Block در shared group، عضویت یا تاریخچه را خودکار حذف نکند؛ راه‌حل co-presence رسانه‌ای باید صریح انتخاب شود.

| سناریو | قاعدهٔ قبلی که reuse می‌شود | تصمیم خاص تماس که هنوز لازم است |
|---|---|---|
| Adult stranger→Minor/Unknown | ممنوعیت stranger communication؛ Unknown مانند Minor | ممنوع باقی بماند؛ مجوز تازه پیشنهاد نشده |
| Teacher↔Student | assignment/enrollment/class/org فعال شرط لازم؛ عنوان Teacher کافی نیست | آیا صوت/تصویر هم‌زمان مجاز است؟ زمان/آغازگر/رضایت و safeguardهای live؛ تا approval deny |
| Guardian↔Child خودش | رابطهٔ جاری فقط همان Child، نه دیگران | رضایت child/guardian، voice/video و محدودیت آغاز؛ relationship به‌تنهایی اجازهٔ live نیست |
| same-class peers | رابطهٔ واقعی کلاس، نه عضویت Group دلخواه | دوطرفه بودن consent، media mode و گروه مجاز؛ تا approval deny |
| Official class GroupCall | audience جاری از Enrollment/TeacherAssignment؛ Manager عضو/شنوندهٔ خودکار نیست | teacher-hosted، رضایت، policy live و handling Safety unavailable؛ تنها این زیرمرحله منتظر تصمیم می‌ماند |
| Personal group دارای protected | private، رابطهٔ معتبر با همهٔ افراد و پذیرش صریح | consent تماس مستقل و live safety؛ دعوت group اجازهٔ میکروفن/دوربین نیست |
| Channel Live | منع ایجاد/انتشار کانال شخصی Minor/Unknown حفظ | مشاهدهٔ Live توسط protected، stage promotion و public exposure نیازمند تصمیم؛ default پیشنهادی خاموش |

Internal text Safety نمی‌تواند جریان بی‌نهایت صدا/تصویر را قبل از deliver بررسی کند؛ قطعه‌قطعه‌کردن در AI/RAG یا ضبط پنهان راه‌حل مصوب نیست. در نبود live policy و محافظ لازم، protected call **Unavailable/deny**، نه Allowed ساختگی. guardian/manager/reviewer حتی با MFA حق مانیتور، شنود یا دانلود private call ندارند. moderation تماس در ابتدا فقط report مزاحمت و metadata کمینه، با author/scoped access و audit؛ evidence رسانه‌ای نیازمند recording policy جدا است.

## ۱۰. Group، Channel Live، Screen Share و Recording

پیشنهاد `R08`: GroupCall به همان Conversation مجاز وصل؛ Join/current membership، ban/restriction/account و رابطهٔ جاری را دوباره بررسی کند. برای مدیریت فقط دو مفهوم تازهٔ پیشنهادی `ManageCalls` (start/end/moderate) و `ShareScreen` کافی است؛ ShareScreen برای Member با setting محدود و consent، ولی شروع توسط Member در نسخهٔ اول deny باشد؛ هیچ flag خودکار به Admin قدیمی داده نشود. InviteMembers/ManageInvites به‌عنوان شرط دعوت reuse شود ولی به کاربر خارج Conversation حق join ندهد؛ دعوت عضو جدید همان lifecycle رضایت موجود را می‌خواهد. RemoveMembers می‌تواند شرط اضافهٔ remove از خود گفتگو باشد؛ remove از call لزوماً اخراج گفتگو نیست. Ban و حذف گفتگو، media را revoke کنند.

پیشنهاد ماتریس Group: Owner/Admin دارای ManageCalls شروع/دعوت مجاز/قطع انتشار فرد/حذف از تماس/پایان؛ Member فعلی با consent وارد و microphone/camera خودش را کنترل کند؛ کلاس رسمی فقط TeacherAssignment جاری Host، نه Manager صرف. mute دیگری مجوز **unmute اجباری** نیست؛ camera/mic بدون اقدام محلی روشن نمی‌شود. این flagها هنوز در enum موجود نیستند و عدد bit برایشان freeze نشده است.

پیشنهاد `R09`: ChannelLive جدا از Group؛ ابتدا فقط Host و Viewer. Host برابر Owner یا Admin با **ManageCalls و PublishMessages** جاری؛ Viewer عضو مجاز receive-only با publish=false حتی در SDK دستکاری‌شده. CoHost/Speaker/promotion در نسخهٔ اول deferred؛ در صورت تأیید بعدی، promotion با رضایت شخص و grant به‌روزشده، demotion با قطع track و revocation لازم است. پایان فقط Host/مدیر دارای مجوز؛ Host disconnect پس از grace مصوب پایان می‌دهد. Private فقط subscriber فعلی؛ Public مجوز anonymous join نیست و authenticated explicit join و privacy/GAP-05 لازم‌اند. Premium/monetization وارد این طرح نمی‌شود.

پیشنهاد `R10`: Screen Share یک source track همان call است، نه CallType یا Message جدید. ابتدا یک sharer هم‌زمان با SQL reservation و role/share permission؛ برای 1:1 طرف دارای consent، Group با ShareScreen، Live فقط Host. revoke/remove/End فوراً publication همان source را می‌بندد؛ توقف محلی نیز لازم است، ولی امنیت به همکاری client متکی نباشد. share audio روی همهٔ platformها یکسان نیست؛ notification، پنجره‌ها و اطلاعات خصوصیِ نمایشگر ریسک افشا دارند. [Screen sharing رسمی LiveKit](https://docs.livekit.io/transport/media/screenshare/).

`RECORDING=DEFERRED`: هیچ auto-Egress، ضبط سرور، transcript، فایل raw یا observer پنهان. آینده فقط با consent معتبر، اعلان دائمیِ قابل مشاهده، storage خصوصی، retention/delete/legal review، scope دانلود و abuse evidence policy؛ FileAsset فعلی به‌تنهایی این‌ها را حل نمی‌کند. E2EE نیز claim این معماری نیست؛ DTLS-SRTP انتقال تا SFU معادل رمزگذاری سرتاسری participantها نیست.

## ۱۱. history و قرارداد پیشنهادی کنترل

history فقط CallId/type/طرف‌های کمینه، StartedAt/AnsweredAt/EndedAt UTC، Result و duration اتصال سروری؛ incoming/outgoing نسبت به viewer، Missed فقط دعوت پذیرفته‌نشدهٔ همان target، Declined جدا از Missed. group roster عمومیِ تاریخی به همه یا join notification برای هر subscriber تولید نشود؛ projection بر اساس مشارکت/مجوز واقعی. IP، SDP، ICE candidate، token، device fingerprint و raw media در history عمومی نیستند. retention پیشنهاد ۳۰ روز نمای شخصی (`R11`)، نه policy حقوقی مصوب یا job ساخته‌شده؛ security audit retention جدا و هنوز نیازمند تصمیم است. پایان رابطه به معنی حذف خودکار سابقه نیست؛ حق خواندن تاریخی نیز باید مستقل کنترل شود.

قراردادهای زیر **API پیشنهادی، نه route موجود یا OpenAPI تحویل‌شده** هستند. REST منبع mutation باشد و SignalR hint کنترل؛ Hub mutation موازی لازم نیست:

| عمل آینده | مسیر پیشنهادی زیر `/api/v1` / ورودی | نتیجه و مجوز |
|---|---|---|
| StartCall 1:1 | POST `/calls`؛ targetUserId/clientOperationId/type و conversationId اختیاری | receipt CallId/state/version/deadline؛ current call privacy/scope، بدون ساخت Direct پیام؛ hash retry؛ 201/200 |
| Start GroupCall/Live | POST `/conversations/{id}/calls`؛ clientOperationId/type | receipt، current membership/host permission/scope و hash retry؛ 201/200 |
| snapshot/history | GET `/calls/{id}`؛ GET `/calls?cursor=&limit=` | projection فقط صاحب/participant مجاز؛ نه room secret |
| Accept/Decline/Cancel/End | POST `/calls/{id}/accept|decline|cancel|end`؛ clientOperationId، expectedVersion در تغییر واقعی، callDeviceSession reference server-bound | receipt state/version، first valid accept؛ caller برای Cancel، target برای Accept/Decline، participant/Host برای End مطابق نوع |
| Join/Leave | POST `/calls/{id}/join|leave`؛ operation/device binding | current member/role، leg یکتا؛ leave لزوماً End گروه نیست |
| media authorization | POST `/calls/{id}/media-grants`؛ device leg همان نشست | grant کوتاه‌عمر فقط برنده/عضو فعلی؛ Cache-Control:no-store؛ reconnect reauthorization؛ token داخل broadcast نیست |

شناسهٔ actor/OrganizationId/role از client authority نیست. proposal خطا: 400 ورودی،401 نشست،404 concealment،409 state/idempotency/answered-elsewhere،412 نسخه،429 بودجه،503 رسانه/guard unavailable؛ codeهای جدید باید هنگام implementation نهایی و تست شوند، نه اکنون به client قرارداد موجود اعلام شوند. retry terminalِ هم‌ارز پس از reauthorization همان receipt است؛ payload متفاوت conflict؛ stale Accept حتی با idempotency قدیمی مجوز رسانهٔ تازه ندارد.

event پیشنهادی `CallRinging/CallAccepted/CallDeclined/CallCancelled/CallEnded/ParticipantJoined/ParticipantLeft/CallAnsweredElsewhere` با EventId/CallId/Revision/UTC/payloadVersion و audience دقیق. snapshot REST پس از reconnect مرجع همگرایی است؛ event دیررس Ringing پس از End با revision/deadline کنار گذاشته شود. eventهای تماس را بدون تصمیم به MessageSequence یا cursor پیام تبدیل نکنید؛ notification.CallId مجوز نیست. ICE/SDP negotiation در SDK/SFU باقی می‌ماند. webhook provider به HTTPS خصوصی/مجاز، signature روی body خام، replay/dedupe bounded و تطبیق room/epoch نیاز دارد؛ هیچ callback عمومی فعلاً ساخته نشده است.

## ۱۲. امنیت، abuse و reconnect

| تهدید | کنترل پیشنهادی و آزمون لازم |
|---|---|
| spam/repeated ring / brute force CallId | بودجهٔ caller و caller-target، cooldown، cap دعوت pending، recipient opt-out/Block/report، پاسخ concealment؛ IP فقط مکمل هویت |
| unauthorized/cross-org join | authorization منبع در Start/Accept/Join/Grant/dispatch، room opaque و scope trusted؛ دعوت لینک یا دانستن CallId کافی نیست |
| replay/stolen grant | TLS، no-store/no-log، TTL کوتاه، identity/device/room/epoch، admission gate و eviction بخش۶؛ JWT bearer به‌تنهایی proof-of-possession نیست |
| revoke/Block/remove/stale permissions | invalidation اتمیک، قطع grant و media capability جاری، reconciliation محدود؛ عضویت قدیمی/track منتشرشده authority دائمی نیست |
| spoofed provider/client event | client گزارش Active مرجع نیست؛ webhook signature/dedupe/order/room-generation، query provider و state SQL؛ late join تماس Ended را زنده نکند |
| private-data leakage | raw SDP/candidate/IP/token/phone/DOB نه log عمومی و نه Push؛ access log proxy query رسانه redact/omit شود؛ metrics کم‌cardinality |
| exhaustion | سقف connections/rooms/participants/tracks/bitrate، bounded body/commands، provider timeout/lease، backpressure و kill switch؛ no network داخل SQL transaction |

بودجه‌های پیشنهادی آزمایش `R16`، به‌ازای caller authenticated در pilot: Start حداکثر۳/min، Invite۱۰/min، Join۲۰/min، Decline۳۰/min، control signaling۱۲۰/min؛ cooldown همان زوج و cap ring هم‌زمان باید با محصول freeze شوند. این اعداد فقط hypothesis قابل‌تنظیم آزمایش‌اند، **نه config موجود یا default قطعی Production**؛ anti-spam کم‌حجم آزمایش و SLO/abuse دادهٔ واقعی باید آن‌ها را اصلاح کند. retry operation هم‌ارز ring تازه نسازد؛ Decline/End امنیتی حتی زیر flood مسیر bounded قابل استفاده داشته باشند. limiter تک‌process فعلی برای چند node کافی نیست؛ distributed budget بدون dependency/approval تازه فرض نشود.

reconnect پیشنهادی `R12/R13`: ring timeout۳۰s، setup deadline۲۰s و media disconnect grace۲۰s فقط مقادیر pilot نیازمند تأیید؛ هیچ تمدید بی‌نهایت با heartbeat. SignalR reconnect → نشست معتبر → snapshot call و subscription جاری؛ media reconnect/ICE restart را SDK اجرا کند ولی admission مجدد را دور نزند. قطع کنترل با media سالم state را فوراً End نمی‌کند؛ پیشنهاد deadline جدا برای کنترل۶۰s با revalidation/reconcile. اگر guard امنیتی لازم unavailable یا revoke قطعی شد grace اعمال نشود. پس از پایان deadline، worker bounded SQL تماس ghost را End و provider را teardown کند؛ restart با durable state/lease، نه حضور حافظه‌ای، recover شود. افت video می‌تواند با اقدام روشن به voice کاهش یابد، نه تغییر consent پنهان یا بازکردن camera.

## ۱۳. Push و پلتفرم‌های client

P1_PUSH_RUNTIME_PENDING یک dependency عملیاتی واقعی است. registry/providerهای معمول موجود لزوماً APNs VoIP یا incoming-call system UI نیستند؛ نوع token/topic/expiry/category تماس نیازمند افزودهٔ مستند در همان subsystem است، نه تبدیل FCM معمولی iOS به PushKit. اعلانِ deadline گذشته نباید ring شود، room JWT در Push نیست، endpoint مقصد دوباره authorization می‌کند. mute/quiet-hours/call DND و priority override تصمیم `R18` است؛ Mute پیام خودکار معنای رد تماس ندارد.

| platform هدف | Voice/Video | Screen | background، permissions و دستگاه |
|---|---|---|---|
| Web React | SDK JS موجود در vendor؛ HTTPS، microphone/camera permission و autoplay/user action | getDisplayMedia، انتخاب صریح هر بار، support مرورگر/اشتراک audio متفاوت | تب hidden/بسته و suspend browser تضمین ring نمی‌دهند؛ permission denied، device-in-use، reconnect و Web Push واقعی آزمایش شوند |
| RN Android | native WebRTC SDK/manifest/runtime permissions؛ صوت/دوربین و audio focus | MediaProjection و foreground service مناسب و consent کاربر | app lifecycle/Doze/FCM و incoming-call integration؛ Bluetooth/headset/speaker و تماس تلفنی هم‌زمان روی سخت‌افزار تست شوند |
| RN iOS | native SDK، camera/mic consent و audio session | ReplayKit؛ sharing تمام سیستم نیازمند broadcast extension و setup مستقل | PushKit/APNs VoIP با CallKit الزامات خاص دارد؛ FCM عمومی جای آن نیست؛ lock/terminated، route/Bluetooth/interruption و foreground/background واقعی لازم |

مرجع platform: [RN رسمی LiveKit](https://docs.livekit.io/transport/sdk-platforms/react-native/)، [W3C Screen Capture](https://www.w3.org/TR/screen-capture/)، [Android MediaProjection](https://developer.android.com/media/grow/media-projection)، [Android foreground service types](https://developer.android.com/develop/background-work/services/fgs/service-types)، [Apple PushKit/CallKit](https://developer.apple.com/documentation/pushkit/responding-to-voip-notifications-from-pushkit). این جدول feasibility هدف است، نه audit frontend موجود؛ هیچ repository/کد frontend خوانده، clone یا تغییر نکرده است. Expo Go/native-module compatibility و حداقل OS/browser باید در spike client مستقل نسخه‌دار شوند؛ backend مجوز ارتقای client framework نمی‌دهد.

## ۱۴. شبکهٔ ایران، observability و ظرفیت

هیچ سنجش ISP یا رسانهٔ واقعی در این مرحله انجام نشده است. ریسک‌های نیازمند آزمایش: UDP محدود، symmetric NAT/CGNAT، Wi-Fi/شبکهٔ موبایل و جابه‌جایی بین آن‌ها، latency داخل کشور/میان مراکز، هزینهٔ پهنای باند، loss/jitter/RTT، مسیر TURN/TLS و تغییرات ISP/زمان روز. این‌ها فرض‌های مهندسی برای ماتریس آزمون‌اند، نه ادعای فراوانی در ایران یا تضمین کیفیت تلگرام. مکان media host فقط پس از آزمایش دو شبکه/ISP و مقایسه مسیر داخلی/خارجی انتخاب شود؛ TLS443 به معنی عبور تضمینی از هر شبکه نیست.

metrics پیشنهادی بدون محتوا: call attempts، answer rate با denominator تماس مجاز، setup p50/p95/p99، active calls/participants، ICE failure، درصد bytes/time relay، SFU errors، disconnect/reconnect rate، duration و aggregate loss/jitter/RTT. تفکیک کنترل/مدیا و provider/node/version کافی است؛ UserId/CallId/IP label عمومی نباشد. trace محدود pseudonymous با ACL، TTL و audit؛ raw SDP/media/room token حتی در exception عمومی ممنوع. alertهای saturation NIC/CPU، TURN allocations/egress، setup failure، revocation backlog/deadline، ghost calls و گواهی منقضی لازم‌اند. نبود metric به معنی صفر خطا گزارش نشود.

فرمول‌های تقریبی، بدون سربار و بدون وعدهٔ ظرفیت: `b` bitrate یک publisher و `n` شمار participant؛ retransmit/FEC/DTLS و simulcast جدا اضافه می‌شوند.

| مسیر | ingress/egress و bottleneck |
|---|---|
| 1:1 P2P واقعی | upload هر طرف b، download b؛ media server تقریباً صفر، signaling جدا |
| 1:1 تمام‌رلهٔ TURN | در relay مجموع ingress≈2b، egress≈2b، aggregate≈4b؛ دو relay جدا مسیر را مضاعف می‌کنند؛ egress با جمع دو جهت اشتباه نشود |
| SFU گروه همه‌به‌هم | ingress≈n×b، egress≈n(n−1)b؛ با subscription انتخابی k، download هر بیننده≈k×b؛ simulcast ingress مجموع لایه‌هاست، نه b یک لایه |
| Live با p publisher و v viewer | ingress≈Σ bitrate لایه‌های publisher؛ egress≈Σ bitrate انتخابی هر viewer، تقریب تک‌منبع `v×b`؛ screen/audio اضافه شوند |

TURN جلوی SFU ممکن است همان ترافیک را روی NIC/مسیر دیگری دوباره relay کند؛ billing کل media host و TURN را جدا حساب کنید. payload byte نرخ مصرف شبکهٔ واقعی نیست. ظرفیت نهایی نیازمند CPU، packet rate، NIC تمام‌دوطرفه، codec/layers، device decode، subscribe pattern، retransmit، TLS relay، room placement و headroom failure benchmark است. هیچ عدد کاربر هم‌زمان قطعی تعیین نشده است.

## ۱۵. فهرست تأمین زیرساخت آینده

| زمان نیاز | اقلام دقیق و مسئولیت عملیاتی |
|---|---|
| اکنون P3-0 | هیچ خرید، credential، نصب یا deploy لازم نیست؛ فقط approval تصمیم‌ها و حفظ مدارک audit |
| اولین real voice/video | domain API و media/turn جدا، CA TLS و renewal، HTTPS/WSS، Linux media host مستقل از Windows IIS/SQL، public IP/mapping، TURN authenticated/UDP+TLS443، firewall بخش۵، bandwidth/egress budget، provider secret custody/rotation، admission/revocation spike، monitor/alerts و دو client مجاز |
| Group | capacity آزمایش‌شدهٔ SFU و technical participant cap، admission/reconciliation قابل اتکا، native clients مجاز؛ برای چند node media، Redis خصوصی نسخه/مجوز بررسی‌شده و routing/drain؛ backend backplane/shared state جدا تصمیم می‌خواهد |
| Channel Live | ظرفیت viewer/egress و محدودسازی publisher، moderation metadata و termination runbook، viewer load و public/private gate؛ CDN/HLS یا ingest بیرونی خودکار لازم/مصوب نیست |
| background incoming call | credential/device واقعی Web Push/FCM، APNs VoIP/CallKit در صورت انتخاب، consent/OS integration و آزمون بسته/قفل؛ P1 runtime gate بدون تغییر |
| فقط recording آینده | Egress/recorder worker، private object storage، encryption/key custody، retention/delete/backup، legal consent و access/audit؛ **اکنون تهیه/فعال نشوند** |

## ۱۶. Stories، Bots و Mini Apps

Stories track مستقل پس از call foundation: FileAsset/Storage و privacy/Contacts/Block، Safety و Notification قابل compose‌اند؛ اما expiry/feed/audience snapshot-vs-current، replies، viewer privacy، moderation و حذف derivatives قرارداد جدید می‌خواهند. وجود upload یا Channel معادل Stories نیست؛ schema یا endpoint آن اینجا طراحی/ساخته نمی‌شود.

Bots/Mini Apps طبق PRD v3 در مسیر گستردهٔ P3 هستند؛ برچسب P4 در registerهای تاریخی به معنی تصمیم جدید نیست. این مرحله فقط مرز را ثبت می‌کند: Bot identity مستقل، token قابل revoke، webhook امضاشده و egress/SSRF guard، rate و opt-in scope؛ Mini App signed launch context با expiry/replay defense، CSP/sandbox و data isolation. هیچ ارث خودکار permission کاربر/Admin، دسترسی پیام کودک یا payment capability ندارند؛ اجرا/طراحی کاملشان نباید freeze معماری تماس را عقب بیندازد. Business/Premium/Monetization خارج این scope می‌مانند.

## ۱۷. خروجی، محدودیت و مطالعه

تنها چهار سند P3 و append محدود [register معوق](../release/DEFERRED_DECISIONS_AND_REMAINING_GAPS.md) تغییر می‌کنند؛ historical P0/P1/P2، Source/Test، appsettings، sln/csproj، migration و MeliPayamak/AI/Payment دست‌نخورده‌اند. تست تازه‌ای ساخته نشده؛ نتایج baseline فقط regression موجودند. قرارداد آینده را با runtime API اشتباه نگیرید.

درس‌های این audit: signaling≠media؛ expiry≠revocation؛ session≠device/Push destination؛ رابطهٔ مجاز متن≠رضایت تماس زنده؛ SFU scale-out≠ظرفیت نامحدود room؛ build/TestServer≠اثبات NAT/کیفیت. مسیر مطالعه: بخش۲ و فایل‌های موجود آن → بخش۶ و۱۵ → [تصمیم‌های مالک](MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md) → [roadmap](MESSENGER_P3_IMPLEMENTATION_ROADMAP.md) → [ماتریس پذیرش](MESSENGER_P3_TEST_ACCEPTANCE_MATRIX.md). گام بعد فقط approval تصمیم‌های dependency همان زیرمرحله است، نه اجرای خودکار P3-A یا deploy.

### Validation نهایی همین ممیزی

پس از نوشتن اسناد، restore و build دوباره موفق با **صفر warning/error**؛ full final suite نیز **558 Unit + 1023 Integration + 14 Architecture = 1595 passed**، failed/skipped=0، Integration حدود۵:۵۴، TRX در `.artifacts/messenger-p3-0/final`. دو دور کامل روی source/test بدون تغییر بودند؛ هیچ تست جدید تماس ساخته یا با mock به‌جای media واقعی PASS نشد. بررسی allowlist دقیق پنج سند، حفظ byte-equivalent خطوط قبلی register، لینک‌های نسبی، شناسه/ارجاع تصمیم‌ها، fenceها، `git diff --check` و عدم تغییر source/config/project/historical P0–P2 موفق بود؛ hash PDF همان مبناست. migration/schema جدید و apply سرور نداریم؛ fixtureها فقط chain موجود را در SQL تست ایزوله اجرا کردند. hash commit و نتیجهٔ push/fetch/remote پس از اجرای واقعی در گزارش نهایی اعلام می‌شوند؛ محتوا self-hash اختراع نمی‌کند.
