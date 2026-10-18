# ماتریس آزمون و پذیرش آیندهٔ P3 Media

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

> **به‌روزرسانی مرجع — ۲۰۲۶-۱۰-۱۷:** [P3 Media Policy v1](MESSENGER_P3_APPROVED_MEDIA_POLICY_V1.md) approved و [P3-S plan](MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_PLAN.md) آمادهٔ اجراست؛ تمام gateهای رسانه همچنان PLANNED_NOT_RUN هستند. جدول‌های P3-0 برای شواهد تاریخی حفظ شده‌اند؛ expected ناسازگار آن‌ها طبق بخش «اصلاح expectedها» در انتهای همین سند و [approval mapping](MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md#p3-policy-v1-owner-approval) جایگزین می‌شود. هیچ case جدید رسانه‌ای اجرا یا PASS اعلام نشده است.

تاریخ `2026-10-17`؛ مبنای کد `5e1da895c96065380e12d04341e483d4e78539c4`. این جدول **طرح آزمون آینده** است، نه تست افزوده‌شده یا نتیجهٔ PASS تماس. [معماری](MESSENGER_P3_MEDIA_ARCHITECTURE.md)، [تصمیم‌ها](MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md) و [roadmap](MESSENGER_P3_IMPLEMENTATION_ROADMAP.md) مرجع وابستگی‌اند.

## شواهد موجود و شیوهٔ نتیجه‌گذاری

baseline جدید P3-0: restore موفق؛ build صفر warning/error؛ **558 Unit + 1023 Integration + 14 Architecture = 1595 executed/passed**، failed=0، skipped=0. TRX در `.artifacts/messenger-p3-0/baseline`؛ این‌ها regression همین Backend‌اند و هیچ RTP/ICE/call test تازه ندارند. آخرین migration Checklist موجود است؛ P3 migration ایجاد/اعمال نشده است.

تکرار کامل نهایی P3-0 نیز پس از restore/build موفق با صفر warning/error، دقیقاً همین **1595 passed، failed/skipped=0** را داشت؛ TRX در `.artifacts/messenger-p3-0/final` و Integration حدود۵:۵۴. این دو اجرا با هم ۳۱۹۰ تست متفاوت محسوب نمی‌شوند؛ شمار suite همان۱۵۹۵ است و تمام gateهای تماس زیر همچنان PLANNED_NOT_RUN هستند.

تا اجرای هر مورد، وضعیت تمام ردیف‌های زیر **PLANNED_NOT_RUN** است. نبود زیرساخت در مرحلهٔ آینده = PENDING_ENVIRONMENT؛ وابستگی تصمیم باز = PENDING_OWNER_DECISION؛ آزمون اجراشده ناموفق = FAIL با دلیل، نه skipped مصنوعی. `LOCAL_CONTROL_PASS`، `CONTROLLED_MEDIA_PASS` و `REAL_DEVICE_NETWORK_PASS` سطوح متفاوت‌اند؛ فقط شواهد مربوط به همان سطح ثبت شوند. `P1_PUSH_RUNTIME_PENDING` عوض نشده است.

ثبت آیندهٔ نتیجه شامل gate ID، SHA source، policy/تصمیم مصوب، platform/SDK/OS pinشده، topology، تاریخ UTC، ابزار/روش، expected/actual، شمار pass/fail و evidence sanitized با custody/retention مصوب است. هیچ نام تست C# خیالی به‌عنوان موجود معرفی نشده است. SLO و بودجهٔ امنیتی revocation **پیش از اجرا** تصویب شوند؛ میانگین خوب جای p95/p99 یا failure rate نیست.

## Unit / SQL / Architecture

| Gate | سناریو | expected و شاهد لازم | لایه / وابستگی |
|---|---|---|---|
| U01 | transitionهای Ringing/Connecting/Active/Ended و همهٔ Resultها | transition غیرمجاز رد، terminal immutable، Started/Answered/Connected/Ended سازگار | Unit Domain؛ R12/R14 |
| U02 | UTC/deadline مرزی و ساعت client غلط | فقط UTC سرور، Accept دقیقاً پس از deadline ممنوع، duration از Active نه Ringing | Unit؛ R11–R13 |
| U03 | same operation/payload، operation متفاوت و payload conflict | receipt replay بدون ring/event تازه؛ payload مختلف conflict | Unit/Application + SQL تکمیلی |
| U04 | privacy Everyone/Contacts/Nobody و default | دفترچهٔ مقصد، Contacts viewer کافی نیست؛ پیام مجاز تماس را باز نکند | Unit policy؛ R04 |
| U05 | protected و guard Unavailable | stranger deny؛ policy مصوب live لازم؛ failure موفقیت ساختگی ندهد | Unit؛ A04/R05–R07 |
| U06 | grants و role/track | Viewer publish/data=false، Voice camera/share ندارد، admin/record/list grant client ندارد | Unit adapter contract؛ R09/R10/R17 |
| U07 | CallAudience و MessageAudience متفاوت / Direct غایب | Call مجاز به‌طور خودکار Send/Read پیام یا ساخت Contact/Direct ندهد؛ Message مجاز نیز Call ممنوع را باز نکند | Unit و SQL/HTTP؛ R20؛ Group/Live بدون Conversation رد شوند |
| S01 | دو Accept از دو session و دو tab یک session با barrier | تنها یک winner/leg/grant قابل استفاده، دیگران AnsweredElsewhere؛ SQL unique/CAS | SQL Integration واقعی؛ A07 |
| S02 | Accept×Cancel / Decline / Timeout | هر ترتیب تنها یک outcome معتبر و terminal ثابت؛ retry بدون دو اثر | SQL + HTTP؛ R12/R14 |
| S03 | Start دوباره و A↔B/cross-call busy race | logical operation یکتا و reservation مطابق policy، بدون دو تماس فعال ناخواسته | SQL؛ R14؛ lock order قطعی |
| S04 | FK/unique/check/rowversion و cross-org/device spoof | SQL خطای دقیق constraint؛ نسخه stale overwrite نکند؛ UserId/DeviceId فرد دیگر authority نشود | SQL؛ بدون EF InMemory |
| S05 | block/privacy/session/role/relationship revoke قبل commit/grant | mutation نامجاز ثبت نشود؛ token زمان preflight مجوز دائمی نیست | SQL/HTTP race؛ R15/R17 |
| S06 | outbox rollback و crash قبل/بعد effect provider | domain+intent اتمیک، idempotent teardown و claim recovery بدون ghost/duplicate call | SQL fault injection؛ A/C |
| S07 | migration fresh/upgrade و down با history | سابقه قدیمی حفظ، مدل pending ندارد؛ rollback مخرب بدون approval متوقف | SQL migration آینده، نه migration فعلی P3-0 |
| A01 | وابستگی Onion و boundary media | Domain/Application بدون SDK/EF/HTTP؛ Controllers نازک؛ SDK فقط Infrastructure | Architecture؛ ۱۴ تست موجود حفظ و فقط invariant واقعی تازه اضافه شود |

## HTTP / SignalR / Notification / Security

| Gate | سناریو | expected و شاهد لازم | لایه / وابستگی |
|---|---|---|---|
| H01 | Start و ring همهٔ endpointهای eligible | فقط account/session/context مجاز؛ expired/revoked/role منقضی هیچ hint نگیرد | HTTP/SignalR TestServer و مخاطب قطعی SQL |
| H02 | fresh connection، unsubscribe/reconnect، hint تکراری/گم‌شده/دیررس | snapshot/version/deadline همگرا؛ Ended با ring دیررس زنده نشود | SignalR integration؛ نه media PASS |
| H03 | Accept/retry/AnsweredElsewhere و نشت private state | فقط برنده grant، سایر deviceهای خود target توقف؛ outsider نه CallId/status/network info | SQL/Hub/HTTP |
| H04 | Bearer header فعلی و transport وب | LongPolling auth معتبر؛ query-token بی‌مجوز پذیرفته نشود؛ هر تغییر WS-auth قرارداد امنیت مستقل | TestServer + مرورگر origin واقعی جدا |
| H05 | API schema، string enum، version/400/401/404/409/412/429/503 | OpenAPI و wire یکسان؛ no-store grant؛ hidden/nonexistent بدون علت حساس | HTTP/OpenAPI، نه endpoint فرضی موجود |
| H06 | rate/cooldown و Start retry با بودجه | flood ring نسازد؛ same-operation effect تازه نه؛ End/Decline قابل استفاده؛ Retry-After واقعی | limiter کنترل‌شده و بعداً distributed/load؛ R16 |
| H07 | Push deadline گذشته / logout / device rotation / quiet hours | no stale ringing، no grant در payload، permission جاری بعد click؛ destination حساب دیگر منتقل نشود | notification SQL/controlled provider؛ R18 |
| SEC01 | CallId/ConversationId/room/device brute force و cross-org | concealment و denial بدون افشای رابطه/سن/اشغال‌بودن فرد | API + controlled media admission |
| SEC02 | revoked/expired session با media فعال و reconnect | denial Backend **و قطع واقعی media**؛ زمان قطع داخل budget مصوب | real provider؛ R17؛ TestServer کافی نیست |
| SEC03 | Block یا ban/remove/teacher assignment end وسط call | Direct پایان مطابق R15؛ shared group history/membership نابجا حذف نشود؛ media grants فعلی revoke | SQL + SFU clients؛ R05–R08/R15 |
| SEC04 | token stolen/replay/refreshed/old epoch، endpoint مستقیم SFU | non-bypassable admission؛ refresh داخلی SDK یا room قدیمی denial را دور نزند | امنیت C.1/C.2؛ شرط GO vendor |
| SEC05 | forged/duplicate/out-of-order webhook، timeout provider | signature/body/replay و generation guard؛ client Active مرجع نیست؛ retry bounded و reconcile | controlled HTTP + SFU |
| SEC06 | log/access-log/exception/Push/history/metrics بررسی | هیچ raw token/SDP/ICE/IP/phone/DOB/media و هیچ PII label؛ secret admin در client نیست | contract و مشاهدهٔ ops sanitized |
| SEC07 | reporter/Guardian/Manager/Owner/reviewer خودپرونده | private call monitoring/recording ندارند؛ report کمینه و پرونده/MFA/audit موجود | authorization integration + product-approved report contract |

## WebRTC کنترل‌شده و media واقعی

| Gate | سناریو | expected و شاهد لازم | محیط لازم |
|---|---|---|---|
| M01 | دو client و دو حساب تماس صوتی SFU | RTP دوطرفه و audible/fixture قابل تشخیص، setup/teardown و history درست؛ no controller frames | provider نسخه‌دار، TLS و دو client کنترل‌شده |
| M02 | Video و camera permission denied/re-enable | تصویر واقعی، mic/camera action صریح؛ camera بدون consent باز نشود | مرورگر/device، R10/consent |
| M03 | TURN-forced و UDP blocked | selected candidate pair واقعاً relay، TLS443 fallback اثبات، bytes/latency اندازه‌گیری | فایروال/lab مجاز؛ صرف config `relay` PASS نیست |
| M04 | STUN/NAT/CGNAT/symmetric NAT و دو network | موفقیت/شکست هر topology با selected transport و درصد setup ثبت | lab NAT + شبکهٔ واقعی جدا؛ بدون ادعای نمایندگی همهٔ ISPها |
| M05 | disconnect کنترل با media سالم / media ICE failure با کنترل سالم | graceهای جدا، revalidation و restart bounded، no permanent ghost | network fault injection و snapshot SQL/provider |
| M06 | حذف participant و replay token پیش/پس expiry و token auto-refresh | track دریافت/ارسال متوقف، initial/full rejoin denied و direct bypass بسته | **critical C/R17 GO gate**؛ webhook فقط مشاهده کافی نیست |
| M07 | share concurrency و removal/End وسط capture | فقط یک sharer مصوب، server publication بسته و UI capture متوقف، بدون recording | Web/Android/iOS هرکدام مستقل |
| M08 | provider crash/restart/drain، backend restart و state mismatch | SQL authority و reconciliation، call terminal دوباره زنده نشود؛ recovery بدون grant stale | محیط disposable مجاز، نه Production |
| G01 | Group audio/video با n participant مصوب | join/leave، user cap، permission/mute/remove و subscriptions واقعی؛ بدون mesh بزرگ | SFU/TURN و چند دستگاه/client، R08 |
| G02 | official class دو سازمان/دو مدرسه و enrollment revoke | authority جاری کلاس، Manager/Guardian شنوندهٔ ضمنی نیست؛ protected فقط با policy مصوب | SQL + SFU، R05–R08 |
| L01 | ChannelLive Host و Viewer دستکاری‌شده | Viewer هیچ audio/video/data غیرمجاز publish نکند؛ private metadata لو نرود | SFU، R09؛ UI کافی نیست |
| L02 | Host permission revoke/end/disconnect و viewer join دیررس | teardown واقعی، terminal و no rejoin؛ subscriber بعدی history نامجاز نگیرد | SFU/SQL و worker grace |

## پلتفرم، دستگاه، شبکه و عملیات

| Gate | سناریو | expected و شاهد لازم | مرز ادعا |
|---|---|---|---|
| P01 | دو دستگاه فیزیکی، caller/callee مستقل و callee چند session | پاسخ دستگاه اول، توقف دیگری، no dual leg؛ playback/record deviceها و microphone permission | emulator/two HttpClient جای آن نیست |
| P02 | Web React مرورگرهای نسخه‌دار | HTTPS secure context، permission/user activation، autoplay، tab hidden/closed، share support و source-specific audio | backend HTTP exception به معنی capture مجاز مرورگر نیست |
| P03 | RN Android واقعی | foreground/background/Doze/locked، incoming UI مجاز، camera/mic/MediaProjection، audio focus و Bluetooth/headset/speaker | OS/target SDK ثبت؛ native permission و store policy gate مستقل |
| P04 | RN iOS واقعی | PushKit/APNs VoIP/CallKit طبق قرارداد منتخب، ReplayKit extension، lock/terminated، audio route/interruption | FCM عمومی و P1 config ثابت نمی‌کنند VoIP Push آماده است |
| P05 | background incoming call روی Web/Android/iOS | notification به call جاری مجاز، decline/accept روی device، deadline گذشته بدون ring، mute/DND صحیح | **P1_PUSH_RUNTIME_PENDING** و runtime incoming-call هر platform جدا بسته شوند |
| Q01 | دو ISP/شبکهٔ مستقل، Wi-Fi↔mobile و roaming مجاز | setup/reconnect/quality با زمان/مسیر/transport و loss/jitter/RTT ثبت؛ consent و داده کمینه | دو شبکه به معنی پوشش تمام ایران نیست |
| Q02 | loss/jitter/latency و bandwidth محدود آزمایشگاهی | degradation و voice fallback صریح، no retry storm، grace و deadline ثابت، score/SLO از قبل تصویب‌شده | شرایط آزمایش از دادهٔ واقعی ISP تفکیک شود |
| Q03 | چندparticipant/group و Live viewer fan-out | CPU/NIC/packets/egress و device decode، bitrate/layers، TURN ratio، headroom و failure SLO | عدد capacity قبل benchmark یا از baseline Unit نتیجه نشود |
| O01 | TLS/firewall/public IP/ports و TURN auth | CA معتبر، renewal، direct port admission بسته، UDP/TCP/TLS عملیاتی و relay خصوصی/open ممنوع | تغییر سرور فقط با مجوز مستقل؛ P3-0 هیچ deploy ندارد |
| O02 | observability/alerts و secret custody | metrics کم‌cardinality، no content logs، revocation backlog/ghost alert و operator runbook | configured با سالم/تحویل‌شده یکی نیست |
| O03 | scale-out/backend یا SFU multi-node در صورت نیاز | node routing/drain و audience جاری؛ SQL race چندprocess؛ backplane/ephemeral جدا آزموده | Redis رسانه به معنی scale-out SignalR نیست |
| O04 | release/rollback/kill switch و backup metadata | قطع شروع/Join جدید و teardown امن، سازگاری schema، no raw recording، restore rehearsal مجاز | backup موفق≠recovery اثبات‌شده؛ production approval لازم |

## TestServer چه چیزی را ثابت نمی‌کند؟

TestServer و LocalDB برای HTTP/Hub، policy، SQL concurrency و idempotency مناسب‌اند؛ ولی handshake مرورگر تحت origin/TLS واقعی، UDP/NAT/ICE، relay انتخاب‌شده، DTLS-SRTP/RTP، کیفیت encode/decode، دستگاه صدا/دوربین، screen permission، Bluetooth، موبایل locked/terminated، APNs/FCM واقعی، مصرف battery و ظرفیت NIC را اثبات نمی‌کنند. HTTP mock provider حتی با نام LiveKit real media نیست. headless controlled media نیز جای آزمون دو دستگاه/دو شبکه و mobile background نیست.

بستن مرحلهٔ آینده به conjunction gateهای scope نیاز دارد: policy approved + کد/SQL/Hub PASS + media/security PASS + device/network/ops مربوط PASS + commit/push/remote verified. اگر scope فقط backend code باشد و runtime فراهم نباشد، CODE_COMPLETE/PENDING_ENVIRONMENT با نام دقیق gateها گزارش شود؛ protected features باز و recording/E2EE معوق به‌صورت unsupported باقی بمانند. این طرح، نتیجهٔ جدیدی برای P0/P1/P2 یا مجوز خودکار اجرای P3 ایجاد نمی‌کند.

## اصلاح expectedها طبق policy مصوب — هنوز اجرا نشده

| Gate / حوزه | expected جاری و test افزوده‌شدنی در فاز مربوط |
|---|---|
| U01/U02/S01/H03 | lifecycle شامل Created؛ first valid Accept transactional و فقط یک accepted leg؛ AnsweredElsewhere برای device بازنده نه پایان تماس برنده؛ ring پیش‌فرض30s/configurable؛ setup20s و busy policy قدیمی freeze نشده‌اند |
| U04/U05/G02/SEC03 | Adult Contacts از دفترچهٔ مقصد؛ protected public Nobody؛ Parent↔Child فقط رابطهٔ فعال+restrictions جاری، بدون monitoring/history access؛ Teacher↔Minor و Minor↔Minor private deny؛ Teacher↔Adult نیازمند رابطه+privacy؛ Unknown مانند Minor؛ class/group با scopeهای جاری و بدون Manager private escalation |
| U06/L01/L02 | ManageLive مستقل؛ Viewer واقعاً publish نمی‌کند؛ promotion فقط explicit/server-authorized/auditable؛ Host/CoHost/Speaker/Viewer طبق policy؛ ManageCalls+PublishMessages قدیمی مجوز Live نیست |
| G01/G02 | ManageCalls کمینه برای Owner/Admin مجاز؛ Member پیش‌فرض start/manage ندارد؛ join عضویت نمی‌سازد؛ cap pilot8 Video/16 Voice active/configurable؛ نه benchmark یا سقف permanent اعضا |
| M05/SEC02/SEC03 | grace15s/configurable برای media **و** signaling فقط network-only؛ session revoke/suspension/Block/remove/End/permission invalidation هیچ grace ندارد؛ 20/60 قدیمی کنار گذاشته شود |
| SEC04/M06 | initial token حداکثر30s و scope Call/Room/User/DeviceSession/role؛ token هنوز معتبر و refresh‌شده آزمایش شود؛ remove API به‌تنهایی کافی نیست؛ current check و قطع واقعی ادامهٔ رسانه لازم؛ PS04–PS06 در S مقدم‌اند |
| M03/O01 | TURN authenticated اجباری، UDP primary و TCP/TLS fallback، selected relay واقعی و credential منقضی/غلط؛ config صرف یا ICE TCP مستقیم PASS نیست |
| M07 | share یک track با سقف هم‌زمان1؛ Direct هر participant مجاز؛ Group/Live Host/CoHost یا participant صریحاً مجاز؛ remove/stop share را قطع کند |
| H05/history/SEC06 | user history امن SystemEvent یا equivalent در Messenger؛ مکانیزم جدید هنوز موجود نیست؛ فقط metadata؛ technical metadata30days/configurable نه legal/user-message deletion؛ recording/transcription/STT archive ممنوع |
| P05/Q03/O04 | P1_PUSH_RUNTIME_PENDING باقی؛ background reliable با device واقعی؛ Video8/Voice16 فقط pilot؛ Live viewer عدد تجاری ندارد و تا load پذیرش feature-gated است |

caseهای PS00–PS10 در [plan S](MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_PLAN.md) روش/شاهد/exit gate مشخص دارند؛ این نام‌ها تست C# موجود نیستند. S با synthetic actor و SFU واقعی فقط media-security را ثابت می‌کند، نه GAP-05 end-to-end یا lifecycle SQL آینده. `LIVEKIT_REJOIN_SECURITY_GATE=NOT_TESTED` و `P3_PRODUCTION_MEDIA_APPROVAL=NOT_GRANTED` باقی‌اند؛ failure اثبات‌شدهٔ امنیتی در اجرای آینده با `LIVEKIT_REJOIN_SECURITY_GATE_FAILED` گزارش شود. نبود محیط یا شاهد INCONCLUSIVE/PENDING است، نه PASS و نه failure اجراشدهٔ ساختگی.

## اجرای P3-S / توقف S1 — ۲۰۲۶-۱۰-۱۷

[گزارش واقعی preflight](MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_REPORT.md) همهٔ PS00–PS10 را با همان شناسه‌ها ثبت می‌کند: PS00=PENDING_ENVIRONMENT؛ PS01–PS10=NOT_RUN. Docker Linux daemon/WSL آماده نیستند و lab عمومیِ مجاز، DNS/TLS/TURN، firewall و دو client معرفی/احراز نشده‌اند؛ نتیجهٔ دستور توقف S1 **P3_S_INFRA_REQUIRED** است. هیچ rejoin/removal/permission/token/refresh/TURN/network runtime آزمایش نشده؛ token reuse و immediate revocation همچنان NOT_TESTED/NOT_PROVEN هستند، نه security FAIL یا PASS. هیچ gate تاریخی بالا با تست Backend بسته نمی‌شود.

S0 و final restore/build/full tests تازه هر دو موفق بودند: **558 Unit + 1023 Integration + 14 Architecture = 1595 passed** در هر دور، failed/skipped=0 و build صفر warning/error؛ TRX در `.artifacts/media-spike/p3-s-preflight/baseline` و `/final`، Integration به‌ترتیب حدود11:22 و6:08. بررسی docs/متن تاریخی و regression از REAL media test جدا هستند؛ تست تازهٔ رسانه اضافه نشده است. در نبود زیرساخت، ابزار و public Call feature ساخته نمی‌شوند؛ PSها در ادامه rename/حذف نشوند و P3-A پیش از PASS واقعی S آغاز نشود.
