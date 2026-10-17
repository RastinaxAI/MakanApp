# نقشهٔ اجرای پیشنهادی P3 Media

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
