# نقشهٔ اجرای پیشنهادی P3 Media

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

> **ترتیب جاری مصوب مالک — ۲۰۲۶-۱۰-۱۷:** [P3 Media Policy v1](MESSENGER_P3_APPROVED_MEDIA_POLICY_V1.md) و بخش «ترتیب مصوب جایگزین» در انتهای همین سند مرجع فعلی‌اند. متن P3-0 زیر، تاریخچهٔ پیشنهاد است؛ ترتیب C.1→A.1→C.2 قدیمی با **S→A→B→C→D→E→F** جایگزین شده است. این تحویل فقط [برنامهٔ اجرایی S](MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_PLAN.md) را آماده می‌کند؛ هیچ فاز رسانه‌ای اجرا نشده است.

تاریخ `2026-10-17`؛ کد مبنا `5e1da895c96065380e12d04341e483d4e78539c4`. این سند **برنامهٔ آینده، نه مجوز اجرا و نه فهرست قابلیت ساخته‌شده** است. [معماری](MESSENGER_P3_MEDIA_ARCHITECTURE.md)، [تصمیم‌های A/R/D](MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md) و [پذیرش](MESSENGER_P3_TEST_ACCEPTANCE_MATRIX.md) با هم خوانده شوند. Source، schema و config در P3-0 عوض نشده‌اند.

## ترتیب وابستگی و جلوگیری از تکمیل صوری

با پیشنهاد SFU برای 1:1، نمی‌توان Voice/Video را قبل از TURN/SFU واقعی Done نامید. حروف A تا F عنوان محصول‌اند، نه اجبار ترتیب زمانی:

1. approval تصمیم‌های وابسته و spike فنی R17 در **P3-C.1**؛ vendor/SDK نسخه‌دار و رفتار admission اثبات شود.
2. **P3-A.1**: کنترل و Domain/SQL/Signaling، با adapter کنترل‌شده فقط CODE gate؛ ادعای تماس واقعی ممنوع.
3. **P3-C.2**: integration واقعی، TURN و security/revocation؛ پس از آن **P3-A.2** با real two-client voice پذیرفته شود.
4. **P3-B**: video و screen track روی زیرساخت پذیرفته‌شده.
5. **P3-D**: Group، سپس **P3-E**: ChannelLive.
6. **P3-F**: پذیرش شبکه/دستگاه/عملیات و محدودیت‌های روشن release.

اگر R17 در primary رد شد، فقط integration وابسته متوقف و fallback به مالک ارائه شود؛ اسناد و تست‌های pure authorization می‌توانند مستقل ادامه یابند، ولی migration/call contract مبتنی بر vendor اثبات‌نشده freeze نشود. این P3-0 خود هیچ spike اجرایی را اجرا نمی‌کند.

## P3-A — Call Domain + Signaling + 1:1 Voice

- Scope: CallSession/Participant/DeviceSession کمینه، state و result جدا، privacy تماس، Start/Accept/Decline/Cancel/End، snapshot/history حداقلی، first-valid-accept SQL، busy/timeout/idempotency، hint به sessionهای eligible و reconciliation اثر media؛ ابتدا adult-only در صورت approval R19.
- Dependency: A01–A10، R01–R04 و R11–R20؛ P3-C.1 برای طراحی adapter و C.2 برای real media. policy protected تا تصویب R05–R07 فعال نشود. R20 استقلال StartCall از SendMessage و نساختن خودکار Direct/Contact را تعیین می‌کند.
- لایه‌ها: Domain قوانین state؛ Application use case/validation/ports؛ Infrastructure EF/SQL/provider/outbox؛ Api Controller نازک و SignalR event. repository عمومی، MediatR یا مدل Identity موازی نسازید.
- Persistence آینده: مدل پس از قرارداد نهایی؛ migration SQL Server inspect‌شده، PK/FK scope-bound، CHECK، unique operation/device winner و rowversion؛ fresh/upgrade/race واقعی. network بیرون transaction؛ startup auto-migrate ممنوع.
- تست: Unit state، SQL double Accept/Accept×Cancel×Deadline، two-session و two-tab، revoke/Block/privacy در نقطه commit/grant/dispatch، REST/Hub snapshot recovery؛ U/S/H/A ماتریس.
- زیرساخت: LocalDB ایزوله برای control؛ برای A.2 media host/TURN/TLS و دو client مجاز. هیچ SMS واقعی لازم نیست.
- DoD: A.1 فقط کنترل کد/تست، نه voice PASS؛ A.2 دوطرفه صوت واقعی با SFU، قطع/پایان و history صحیح، deny/evict/rejoin آزموده، حد runtime واضح؛ restore/build/full/final tests، review، focused commit/push/remote verify و گزارش فارسی. در نبود client/runtime، CODE_COMPLETE با gateهای باقی‌مانده، نه COMPLETE.

## P3-B — 1:1 Video + Screen Share

- Scope: camera track، consent و تغییر voice/video صریح، share source روی همان call، یک sharer مصوب و revoke/stop؛ تاریخچهٔ خام یا ضبط ندارد.
- Dependency: A.2 + C.2 و R10، privacy/safety همان call؛ اگر protected policy باز است فقط شاخهٔ مربوط خاموش بماند.
- تست: source grant camera/microphone/screen جدا، camera شروع خودکار نشود، consent denied/device-in-use، هم‌زمانی share، share×remove×End، foreground/background/route و browser/OS support در ماتریس M/P.
- زیرساخت: TLS معتبر، SFU ظرفیت video و دستگاه/مرورگرهای مجاز؛ هیچ SDK/frontend در backend task بی‌اجازه تغییر نکند.
- DoD: ویدئو/اشتراک واقعی و توقف server-enforced اثبات شود؛ feature unsupported صریح و بدون جعل success. Screen audio همهٔ platformها وعده داده نشود. security/regression و Git gate همان A.

## P3-C — TURN/SFU Integration و admission واقعی

- C.1 Scope: pin نسخهٔ platform/SDK و license review؛ .NET HTTPS/Twirp contract spike؛ بررسی token refresh/revocation و non-bypassable join/rejoin admission، scoped grants، eviction و fallback feasibility. اجازهٔ این آزمایش/زیرساخت باید در درخواست اجرای بعدی داده شود.
- C.2 Scope: adapter production با cancellation/timeout و secret custody، signed webhook/inbox dedupe، policy epoch، outbox effects/reconciliation، room lifecycle و graceful drain؛ authenticated STUN/TURN UDP/TCP/TLS و stats contentless. کلید admin به client نرسد.
- Dependency: R01–R03/R17، TLS/domain/IP/firewall مجاز و روش اتصال محیط؛ callback و ریسک residual revocation با مالک/security freeze شود. self-hosted TTL کوتاه جای gate admission نیست.
- تست: provider-controlled component، API رسمی نسخهٔ pinشده، دو client واقعی، token stolen/replayed/refreshed پس از revoke، direct-port bypass، webhook جعل/تکرار/جابجایی، crash قبل/بعد provider effect، TURN-forced/UDP-block و rejoin؛ M01–M08/Q/O ماتریس.
- زیرساخت: Linux media مستقل از IIS/SQL، public IP/CA، UDP/ICE/TURN TLS، monitoring/egress؛ media Redis فقط اگر topology چندگرهی مصوب است. خرید/deploy خارج درخواست صریح انجام نشود.
- DoD: تمام real-media/security gates اثبات؛ reject یا evict فقط log نشود، منع دریافت/ارسال واقعاً مشاهده گردد. در gate ناموفق primary انتخاب نهایی نیست؛ Janus/Coturn نیز بدون همین آزمون جایگزین امن نامیده نشود. قرارداد Backend همچنان provider-independent و SDK در Infrastructure بماند.

## P3-D — Group Voice / Video

- Scope: room به Conversation مجاز، current membership/role/ban، ManageCalls و محدودیت publish/share مصوب، join/leave/invite، host moderation، mute فقط خاموش‌کردن و remove از call، technical cap آزمایش‌شده. personal و class scope مستقل.
- Dependency: A/B/C و R08/R10/R15؛ **official/protected** به R05–R07 و policy live مصوب وابسته است. membership Messenger جای Enrollment/TeacherAssignment نیست.
- تست: SQL capacity last slot، two-node Accept/Join race در صورت scale-out، role/remove/ban/enrollment end در حین انتشار، SFU track revocation/rejoin، cross-org و teacher دو مدرسه، چند participant واقعی و decode/NIC/load.
- زیرساخت: SFU/TURN با headroom و cap pilot مصوب؛ برای تعداد node بیشتر routing/drain و backend backplane جدا پذیرفته شوند؛ shared presence فعلی معجزه‌وار distributed نمی‌شود.
- DoD: فقط زیرscopeهای policy-approved PASS، unapprovedها gate بسته؛ بدون mesh گروه بزرگ، بدون خودکار Admin کردن Teacher و بدون ورود Manager به call خصوصی. cap گروه تماس با محدودیت تجاری تعداد اعضای Messenger اشتباه نشود.

## P3-E — Channel Live

- Scope: Host/Viewer ابتدا، Host=Owner/Admin با authority مصوب؛ viewer receive-only در media layer، explicit authenticated join، private/public/organization isolation، پایان/Host failure و history کمینه.
- Dependency: C/D و R09/R13/R18؛ protected viewing نیازمند R05؛ CoHost/Speaker فعلاً D04. PublishMessages به‌تنهایی اجازهٔ Live محسوب نشود.
- تست: subscriber دستکاری‌شده نتواند publish کند، promotion بدون endpoint/consent ممکن نباشد، role revoke و host loss/end، private live metadata پنهان، viewer fan-out واقعی و bandwidth/subscribe limits.
- زیرساخت: ظرفیت egress/viewers/metrics و runbook پایان؛ CDN/HLS/ingress/recording بدون requirement جدید اضافه نشوند.
- DoD: نقش‌ها در SFU enforce، termination واقعی، viewer capacity benchmark و policy در release manifest؛ monetization/payment/recording خارج scope. client/device gates همچنان جدا.

## P3-F — Production Media Acceptance

- Scope: rehearsal مستقل با دو account/session مجاز، دو device و حداقل دو network، foreground/background/locked/terminated، TURN TLS fallback، network switch/loss/jitter، revocation/recovery، چندparticipant و ظرفیت، metrics/alerts/runbook/rollback/kill switch.
- Dependency: A–E فقط در scope مصوب، P1 real push با credentials/device و **incoming-call** integration جدا؛ امنیت HTTPS/WSS و admission/eviction قبلاً پذیرفته‌شده. دسترسی/اعمال سرور فقط با مجوز جدید.
- تست: تمام gateهای runtime ماتریس؛ اندازه‌گیری SLO از قبل موردتوافق، نه تعیین عدد موفقیت پس از آزمایش. evidence بدون token/SDP/media و محدود به consent.
- زیرساخت: deployment sandbox مصوب، domain/CA/firewall/NAT، Android/iOS واقعی، APNs/FCM/Web Push و remote access امن، capacity budget و مسئول عملیاتی.
- DoD: REAL PASS فقط با مشاهده و شواهد محیط؛ TestServer/شبیه‌ساز به‌جای آن نیست. نبود background runtime با برچسب pending و کاهش شفاف scope گزارش شود؛ برای claim کامل موبایل/background نمی‌توان pending را PASS حساب کرد. release فقط پس از regression/Git/remote و approvals مرتبط، نه از پایان P3-0.

## trackهای جدا و کنترل تغییر

Stories پس از call foundation با FileAsset/Privacy/Contacts/Block/Safety موجود compose شود؛ expiry/audience و viewer policy نیازمند درخواست مستقل. Bots/Mini Apps مطابق PRD v3 مسیر گستردهٔ P3 ولی independent هستند؛ token/revoke/opt-in/webhook و signed launch/CSP/sandbox/data isolation پیش‌نیازند. Business/Premium/monetization، recording/E2EE، AI/RAG و Payment/MeliPayamak به این مراحل افزوده نشوند.

هر فاز یک scope محدود، تصمیم‌های شناسه‌دار، تست‌های مشخص، migration فقط در صورت نیاز واقعی و commit متمرکز دارد. workflow اجرا: Implement → restore → build/fix → full test/fix → final build/test → diff/staged review → commit → push tracked branch → fetch/remote verify → گزارش آموزشی فارسی. هیچ فایل dirty قبلی AI یا frontend وارد تغییر/commit نشود. P3-0 فقط اسناد است و درخواست جاری اجازهٔ شروع P3-A تا F نیست.

## ترتیب مصوب جایگزین — P3 Media Policy v1

این بخش پیشنهادهای ناسازگار بالا را فقط در حدود approval صریح مالک supersede می‌کند؛ متن تاریخی حذف نشده است. [decision mapping](MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md#p3-policy-v1-owner-approval) مانع تأیید ضمنی جزئیات قدیمی است.

| ترتیب | فاز مصوب و scope | dependency / خروجی عبور |
|---|---|---|
| 1 | **P3-S — Media Security Spike**؛ LiveKit واقعی، token scope/initial TTL≤30s، removal/rejoin/refresh، TURN و faultها؛ بدون business rollout | [plan اجرایی](MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_PLAN.md)، lab مجاز؛ REAL security gate PASS و شواهد رسانه، نه صرف Build/TestServer |
| 2 | **P3-A — Call Control + 1:1 Voice**؛ Domain/SQL/state، signaling/control، multidevice، privacy/Block/Child Safety، media issuance و real voice | فقط پس از PASS S؛ قراردادهای جزئی freeze‌نشده پیش از پیاده‌سازی وابسته روشن شوند؛ legacy adult-only نباید رابطهٔ Guardian مصوب را دوباره بی‌دلیل deny کند |
| 3 | **P3-B — 1:1 Video + Screen Share**؛ SFU و یک share هم‌زمان با authority مصوب | A و S؛ consent/source grant و توقف/remove واقعی؛ بدون recording |
| 4 | **P3-C — Media Infrastructure Acceptance**؛ forced TURN، failure/reconnect/revocation، Web/Android/iOS واقعی در صورت دسترسی | A/B؛ grace شبکه15s برای هر دو media/control و هیچ grace امنیتی؛ دستگاه غایب pending، نه PASS |
| 5 | **P3-D — Group Voice/Video**؛ Group/Official bound، current membership/academic scopes، ManageCalls کمینه | S/A/B/C در scope مربوط؛ pilot Video8 و Voice16 active/configurable؛ join عضویت permanent نسازد |
| 6 | **P3-E — Channel Live**؛ ManageLive صریح، Viewer receive-only، promotion server-authorized و auditable | زیربنای پذیرفته‌شده؛ Host/CoHost/Speaker/Viewer طبق policy؛ PublishMessages یا ManageCalls جای ManageLive نیست |
| 7 | **P3-F — Production Media Acceptance**؛ real networks/load/observability/recovery/bandwidth/security/runtime deployment | تمام gateهای scope و محیط/دسترسی مجاز؛ Live viewer limit بعد benchmark و feature gate؛ background incoming call مستقل از foreground |
| 8 | **Stories**؛ track مستقل | پس از F با قرارداد و scope مستقل؛ core Voice/Video را معطل نکند |
| 9 | **Bots / Mini Apps**؛ track مستقل | پس از Stories طبق ترتیب مالک؛ identity/consent/sandbox قرارداد مستقل |

ماتریس تماس protected، Parent/Child معتبر و محدودیت Teacher/Minor از policy جدید خوانده شوند؛ خاموشی کلی R05 و D04 قدیمی دیگر مرجع نیستند. history امن در Messenger و technical metadata30days/configurable هدف آینده‌اند؛ این ثبت permission enum، SystemEvent، جدول یا تنظیم محصول ایجاد نمی‌کند. Recording/transcription/STT archive همچنان deferred/ممنوع در v1 است.

اگر REAL spike نتواند کنترل ادامهٔ تماس نامجاز پس از rejoin را رضایت‌بخش اثبات کند، `LIVEKIT_REJOIN_SECURITY_GATE_FAILED` و توقف media production approval؛ fallback نیز gate معادل می‌خواهد. در نبود محیط/شاهد، نتیجه pending یا inconclusive است و S پاس نمی‌شود. full A/B/D/E پیش از PASS S شروع نشوند؛ پایان plan به‌تنهایی اجازهٔ bypass gate نیست. `P1_PUSH_RUNTIME_PENDING` باقی است و reliability background از این ترتیب نتیجه نمی‌شود.

## اجرای درخواستی P3-S — توقف زیرساخت در S1، ۲۰۲۶-۱۰-۱۷

در شروع `3cfe196dc0de1bbb0c36653834fde544f1d1ed4e`، worktree تمیز و با origin/main برابر بود. [گزارش preflight و فهرست تأمین](MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_REPORT.md) نتیجهٔ جاری را ثبت می‌کند: Docker CLI موجود ولی Linux Engine در دسترس نیست؛ WSL distro ثبت‌شده ندارد و kernel مفقود است؛ Linux lab مجاز/دسترسی/IP/DNS/TLS/firewall و دو media client آماده معرفی/احراز نشده‌اند. S1 طبق prompt با **P3_S_INFRA_REQUIRED** متوقف است؛ S2 version pin و S3–S23 رسانه اجرا نشده‌اند. هیچ harness/deployment/API/migration محصول ایجاد نشد.

گام بعد تأمین اقلام بخش۳ گزارش و تکرار S1 است، نه P3-A یا fallback implementation. ریسک مستنداتی self-hosted token پابرجاست ولی window واقعی تأیید نشده؛ نتیجهٔ NOT_RUN را PASS یا LIVEKIT_REJOIN_SECURITY_GATE_FAILED ننامید. ترتیب مصوب و تمام تاریخچهٔ بالا حفظ‌اند؛ نتیجهٔ واقعی regression و Git در گزارش پس از اتمام ثبت می‌شود.
