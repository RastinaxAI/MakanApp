# تصمیم‌های محصول/امنیت لازم پیش از اجرای P3 Media

تاریخ `2026-10-17`؛ مبنا `5e1da895c96065380e12d04341e483d4e78539c4`. سند همراه: [معماری و شواهد](MESSENGER_P3_MEDIA_ARCHITECTURE.md). **هیچ ردیف پیشنهادی این سند approval مالک محسوب نمی‌شود**. عنوان Freeze در P3-0 یعنی ثبت مرزهای مصوب و مشخص‌کردن تصمیم‌های باز، نه تأیید خودکار انتخاب vendor یا قواعد تماس کودک. PRD target است؛ call runtime هنوز وجود ندارد.

## APPROVED_FROM_EXISTING_POLICY

این موارد از قواعد موجود یا الزام صریح درخواست P3-0 می‌آیند؛ در scope تماس هنوز implementation تازه ندارند.

| ID / تصمیم | مقدار مرجع | چرا / منبع | اثر امنیتی | اثر بر implementation آینده |
|---|---|---|---|---|
| A01 معماری پایه | .NET9، Controllers، Onion/Modular Monolith، EF9 SQL Server | AGENTS و SQL Server override | authority سرور و persistence روشن | بدون تغییر framework/primary DB و بدون CQRS/MediatR/GenericRepository تازه |
| A02 مرز رسانه | SignalR فقط control؛ هیچ frame در Backend | درخواست P3-0، فصل۱۷ PRD | پرهیز از مسیر رسانهٔ بی‌مجوز و log محتوا | adapter زیرساخت media، نه media processing داخل Controller |
| A03 هویت و authorization | current session/account/scope/resource؛ شناسه selector | GAP-05 و تصمیم‌های Messenger | UserId یا عضویت group authorization دائمی نیست | reuse Identity/relationship، کنترل تازه برای هر call action |
| A04 protected و stranger | Unknown مانند Minor؛ stranger→Minor/Unknown ممنوع | GAP-05 مصوب، بدون بازگشایی ۱۴ تصمیم | منع دورزدن با تماس و Everyone | سن مؤثر/verification و رابطهٔ جاری reuse |
| A05 privacy خصوصی | Guardian/Manager/PlatformOwner شنود یا private browsing عمومی ندارند | GAP-05 و Backoffice | بدون observer یا recording مخفی | report/case محدود، MFA/step-up موجود؛ نه سیستم موازی |
| A06 Block و history | Block ارتباط مستقیم تازه را منع می‌کند؛ history/shared membership خودکار حذف نمی‌شوند | PRD فصل۷ و GAP-05 | Block نه data wipe و نه مجوز bypass از Group | recheck Direct تازه؛ رفتار live فعال در R15 جدا |
| A07 چنددستگاهی | first valid Accept wins؛ دیگر deviceها AnsweredElsewhere | الزام صریح Phase9 همین درخواست؛ نه قابلیت موجود P1 | منع دو leg مستقل/دو grant برنده | SQL concurrency، receipt idempotent و آزمون دو session/دو اتصال |
| A08 credential | کوتاه‌عمر و scope-bound؛ admin secret فقط server | درخواست P3-0 و AGENTS | عدم نشت token/کلید، بدون E2EE ساختگی | grant کمینه و no-store/no-log؛ enforcement واقعی در R17 |
| A09 تاریخچه | فقط metadata ضروری؛ raw media ذخیره نشود | PRD فصل۱۷ و درخواست P3-0 | minimization و عدم نشت network IDs | projection viewer-specific، retention هنوز باز |
| A10 پذیرش | TestServer/Build اثبات media واقعی نیست؛ P1 runtime pending حفظ | شواهد P1/P2 و درخواست P3-0 | جلوگیری از ادعای قابلیت/امنیت اثبات‌نشده | [ماتریس](MESSENGER_P3_TEST_ACCEPTANCE_MATRIX.md) و gate دستگاه/شبکه |

## RECOMMENDED_FOR_OWNER_APPROVAL

**وضعیت تمام Rها: AWAITING_OWNER_APPROVAL**. اعداد زیر پیشنهاد pilot هستند، نه default اعمال‌شده، نه SLA و نه محدودیت تجاری. هر تصمیم می‌تواند با مقدار/دلیل جایگزین تأیید شود. تصمیم unresolved فقط زیرمرحلهٔ وابسته را متوقف می‌کند؛ adult-only و سند معماری لزوماً منتظر سیاست همهٔ گروه‌ها نمی‌مانند.

| ID / تصمیم | مقدار پیشنهادی | چرا | اثر امنیتی | اثر implementation / gate |
|---|---|---|---|---|
| R01 مسیر 1:1 | SFU از ابتدا، همان مسیر Group/Live؛ P2P production اولیه نه | یک transport و کنترل room/eviction | کاهش افشای peer IP و کنترل متمرکز؛ بدون ادعای E2EE | هزینهٔ bandwidth بیشتر؛ real call به P3-C وابسته |
| R02 platform | primary LiveKit self-hosted؛ fallback Janus VideoRoom+Coturn مشروط | SDKهای client و room API در برابر هزینهٔ ساخت پایین‌سطح | **مشروط به R17**؛ هیچ vendor مصوب تلقی نشود | spike .NET/API، license، native SDK و revocation؛ fallback پذیرش مجدد می‌خواهد |
| R03 TURN/topology | Linux media جدا از Windows Backend؛ embedded TURN pilot، endpoint/IP TLS جدا؛ جداسازی host relay هنگام رشد | UDP/TCP/TLS fallback و عدم اشغال IIS/SQL | authenticated relay، private management و no open relay | domain/CA/IP/firewall/egress؛ Coturn موازی فقط در fallback یا نیاز مصوب |
| R04 WhoCanCallMe | Verified Adult=Contacts؛ protected=Nobody، gate protected بسته | تماس رضایت جدا از پیام است | Contacts از دفترچهٔ مقصد؛ Everyone guard سنی را برنمی‌دارد | field/DTO/migration جدید فقط در implementation؛ backfill پس از approval |
| R05 تماس live Minor/Unknown | تا تصویب سیاست رسانهٔ هم‌زمان، شروع و join/publish/receive محافظت‌شده خاموش | provider متن، ایمنی صوت/تصویر زنده را بررسی نمی‌کند | fail-closed؛ منع ارسال رسانه به AI/RAG یا ضبط پنهان | به P3-A adult-only مانع نمی‌دهد؛ protected جزء ادعای COMPLETE نباشد |
| R06 Teacher↔Student | فقط همان assignment/enrollment/org جاری، **به‌علاوه** رضایت/شرایط live مصوب؛ فعلاً deny | نقش Teacher مجوز همهٔ دانش‌آموزها نیست | زمان تماس، آغازگر، audio/video و safeguards باید صریح تعیین شوند | پیش از زیرمرحلهٔ protected، قرارداد جدا؛ رضایت متن کافی نیست |
| R07 Guardian↔Child / same-class peers | رابطهٔ معتبر ضروری ولی کافی نیست؛ تا تصویب consent live خاموش | رابطه، مجوز گوش‌دادن خودکار نیست | فقط همان child/class؛ دو protected نیازمند guards هر دو | scope و تغییر رابطه در Start/Accept/Join/reconnect؛ authority مدرسه/guardian توسعه نیابد |
| R08 GroupCall | Owner/Admin با ManageCalls شروع/end/moderate؛ عضو فعلی join با consent؛ کلاس رسمی Teacher جاری؛ ابتدا ۸ participant فنی pilot | کمینه‌سازی permission و ظرفیت آزمایش‌پذیر | invite فقط عضو/رضایت مجاز؛ mute دیگران بدون unmute اجباری؛ protected تابع R05–R07 | دو مفهوم جدید ManageCalls و ShareScreen پیشنهاد، نه enum موجود؛ cap قابل‌تنظیم، نه member cap تجاری؛ خروج آخرین عضو/Host پایان پس از grace |
| R09 ChannelLive | Owner یا Admin با ManageCalls+PublishMessages Host؛ subscriber فعلی Viewer receive-only؛ مرحله اول بدون promotion | تفکیک broadcast از جلسه | publish=false در SFU، نه فقط UI؛ private و org scope جاری | public anonymous viewing نه؛ Host loss بعد grace پایان؛ protected viewing/publishing تابع R05 |
| R10 Screen Share | یک sharer هم‌زمان، consent محلی و permission؛ Live فقط Host | کنترل سادهٔ track و جلوگیری از overload | revoke/remove/End publication را ببندد؛ auto-recording نه | source-bound grant، reservation SQL و آزمایش Web/Android/iOS؛ share audio سازگاری جدا |
| R11 history/retention | نمای شخصی ۳۰ روز به‌عنوان پیشنهاد اولیه؛ audit/حقوقی مستقل و بدون حذف خودکار تا تصویب کامل | حداقل داده، امکان گزارش مزاحمت | parent/manager history شخص ثالث نمی‌گیرند؛ حذف رابطه data wipe نیست | duration از Active، incoming/outgoing viewer-specific؛ deletion/appeal/backup باید قبل job تصویب شوند |
| R12 ring/setup deadline | ring۳۰s؛ Connecting۲۰s، هر دو از UTC سرور | جلوگیری از زنگ/اتصال ghost | push دیررس ring تازه نکند؛ error علت خصوصی ندهد | timestamp پایدار، worker expiry و Accept×Timeout SQL؛ تایمر client مرجع نیست |
| R13 reconnect | رسانه grace۲۰s؛ کنترلِ قطع با رسانهٔ سالم حداکثر۶۰s و revalidation؛ revoke هیچ grace ندارد | قطع SignalR با قطع رسانه یکی نیست | denial/unavailable guard لازم fail-closed؛ تمام rejoinها R17 | SDK ICE recovery + snapshot SQL + reconciliation، retry bounded؛ SLO پس از آزمون |
| R14 busy/decline/multi-device | فقط یک تماس پذیرفته/فعال برای هر User؛ decline یک device کل دعوت را رد کند؛ caller فقط device آغازگر؛ Saved/self ممنوع | حذف ambiguity رقابت و مزاحمت زنگ | Busy با پاسخ عمومی و بدون افشای تماس طرف؛ Accept اول معتبر طبق A07 | reservation SQL با ترتیب قفل User/Conversation/Call مستند؛ handoff و call waiting خارج نسخه اول |
| R15 Block/تغییر policy حین تماس | Direct فعال با Block هر طرف پایان؛ shared group خودکار حذف نشود؛ تا تعیین سیاست co-presence، pair ناسازگار اجازهٔ join/media تازه نگیرد | حفظ قاعدهٔ Block خصوصی و محافظ live | نیازی به حذف تاریخچه نیست؛ عضو مشترک اجازهٔ تماس خصوصی نمی‌سازد | eviction و admission/reconcile؛ انتخاب mute/subscription محدود مشترک به تصمیم/توان platform وابسته |
| R16 abuse budgets | hypothesis pilot: Start۳/min، Invite۱۰، Join۲۰، Decline۳۰، control۱۲۰؛ cooldown زوج و pending cap پس از آزمایش | اعداد صرفاً نقطه شروع سنجش‌اند | anti-spam و جلوگیری از denial با flood؛ End/Decline مسیر امن باقی بمانند | config آینده، Retry-After و replay بدون ring تازه؛ چندnode budget مشترک نیازمند تصمیم زیرساخت |
| R17 admission/revocation | primary فقط پس از اثبات منع join/rejoin با grant لغوشده و قطع leg فعال، شامل token refresh داخلی SFU | self-hosted LiveKit token revoke آماده ندارد | **TTL کوتاه یا webhook async کافی نیست**؛ budget قطع و ریسک باقیمانده باید owner/security تصویب کنند | spike non-bypassable signaling admission، policy epoch، eviction/reconcile و fault tests؛ اگر نشد GO نیست، نه تضعیف guard |
| R18 notification تماس / DND | پیش‌فرض احترام به DND/Quiet Hours تماس، بدون bypass از Mention/Mute پیام؛ generic Push بدون grant، expiry برابر ring deadline | خبررسانی با consent تماس و privacy مرتبط است | caller identity/child/مدرسه در preview حداقلی افشا نشود | category/payload/TTL جدید روی subsystem موجود؛ APNs VoIP/CallKit مستقل از FCM معمولی و gate واقعی P1 |
| R19 مرز تحویل اول | adult-only، بدون ضبط؛ explicit feature gate؛ Web foreground ابتدا، موبایل/background gate جدا | مرز قابل‌اثبات در برابر ادعای کل target | محافظت‌شده‌ها hidden/deny و قابلیت unsupported شفاف | CODE_COMPLETE با runtime pending متفاوت؛ صدور مجوز implementation مستقل لازم |
| R20 استقلال مجوز تماس از پیام | 1:1 از target مجاز حتی بدون Direct پیام؛ ConversationId فقط optional و در صورت وجود pair/scope معتبر | WhoCanCallMe با DirectMessagesAudience یکی نیست | StartCall حق Send/Read تاریخچه، Contact یا Direct تازه ایجاد نکند؛ guardهای مشترک stricter حفظ | CallSession 1:1 بدون Conversation مجاز، Group/Live bound اجباری؛ predicate Call مستقل، نه reuse مجوز ارسال متن |

مالک لازم نیست به همهٔ حوزه‌ها یک‌جا جواب دهد: برای آغاز adult-only کنترل، R01–R04، R11–R20 و حد فنی مرتبط لازم‌اند؛ برای Group/Live/Share تصمیم‌های R08–R10، و برای protected حتماً R05–R07 و safeguards مربوط نیز لازم‌اند. R17 قبل از ادعای integration امن gate فنی است، نه نیاز به تغییر ۱۴ تصمیم قبلی Child Safety.

## DEFERRED

این موارد در نسخهٔ نخست پیشنهادی اجرا نمی‌شوند؛ approval معماری تماس اجازهٔ پیاده‌سازی آن‌ها نیست.

| ID / تصمیم | مقدار پیشنهادی | چرا | اثر امنیتی | اثر implementation |
|---|---|---|---|---|
| D01 Recording | DEFERRED، خاموش | consent/notice/retention/legal/access مستقل | بدون ضبط/transcript/observer پنهان | Egress و recording storage اکنون خرید/فعال نشوند |
| D02 E2EE / Secret Call | DEFERRED | key lifecycle/client compatibility/audit مستقل | TLS/DTLS-SRTP ادعای E2EE نیست | پروتکل جدا، نه flag تبلیغاتی |
| D03 Call waiting/device handoff | DEFERRED | lifecycle/رقابت بیشتر | token دو دستگاه فعال بدون قرارداد صادر نشود | first-accept semantics نسخهٔ اول حفظ |
| D04 CoHost/Speaker/promotion | DEFERRED در ChannelLive اول | Host/Viewer کافی برای baseline broadcast | ارتقای publisher نیازمند رضایت و grant به‌روز | مدل role بی‌مصرف اکنون نسازید |
| D05 Recording-based moderation / live classifier | DEFERRED | provider و consent مستقل | Safety متن با اجازهٔ تحلیل رسانه/AI یکی نیست | protected gate R05 با این تعویق bypass نشود |
| D06 Stories | track مستقل P3 پس از call foundation | audience/expiry/interaction policy جدید | FileAsset و Contacts مجوز خودکار story نیستند | schema/endpoint کامل در P3-0 ساخته نشود |
| D07 Bots / Mini Apps | track مستقل بعد از foundation مطابق PRD v3 | identity/token/webhook/opt-in/sandbox مستقل | ارث‌بردن role/chat دسترسی ممنوع | signed launch و data isolation نیازمند طرح جدا؛ freeze تماس را معطل نکند |
| D08 Premium/Business/Monetization/SIP | خارج این تحویل | مالی/حقوقی/تلفن عمومی مستقل | credential/payment/privacy دامنهٔ جدا | Commerce/MeliPayamak/AI تغییر نکنند |
| D09 multi-node backend presence/backplane | تصمیم عملیاتی مستقل | baseline یک process است | اتصال node دیگر مجوز/زنده‌بودن call را اثبات نمی‌کند | Redis media ≠ backplane ماکان؛ SQL همچنان authority |

## ثبت approval آینده

برای هر R، ID، مقدار پذیرفته‌شده/جایگزین، مالک/تاریخ، دامنهٔ adult/protected، threat و test gate ثبت شود؛ فقط همان ردیف به وضعیت مصوب جدید منتقل گردد. پیش‌فرض‌های پیام/Presence یا قبول Master PRD به معنی قبول اعداد pilot/vendor/live-safety نیستند. تصمیم باز، blocker **اجرای وابسته** است، نه blocker آماده‌بودن این ممیزی برای تصمیم مالک. وضعیت این مرحله پس از validation و Git، `P3_MEDIA_ARCHITECTURE_READY_FOR_OWNER_DECISIONS` است، نه «تماس آماده».
