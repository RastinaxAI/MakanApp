# برنامهٔ اجرایی P3-S — Media Security Spike

وضعیت `2026-10-17`: **PLAN_READY / EXECUTION_NOT_STARTED**. ورودی محصول: [Makan Messenger P3 Media Policy v1 — APPROVED_FOR_IMPLEMENTATION](MESSENGER_P3_APPROVED_MEDIA_POLICY_V1.md). مبنای کد پیش از ثبت policy: `47e620077c5809b6d8e1b6ad2515f1afec48c247`. این سند قرارداد انجام آزمایش بعدی است؛ tooling پیشنهادی زیر هنوز ایجاد نشده، LiveKit نصب/اجرا نشده و هیچ نتیجهٔ real media از Build/Test فعلی استنتاج نمی‌شود.

## ۱. سؤال، خروجی و مرز اجرای بعدی

سؤال تعیین‌کننده: آیا LiveKit Self-hosted با admission/rejoin-control انتخاب‌شده می‌تواند پس از invalid شدن مجوز جاری، participant فعال را remove کند، صدور token جدید را ببندد و با **توکن قبلیِ هنوز معتبر یا refresh‌شده** از تماس ادامه‌دار نامجاز جلوگیری کند؟ TTL اولیه30s پاسخ این سؤال نیست.

خروجی S: نسخه/پیکربندی pinشده، مسیر admission قابل‌اثبات، adapter آزمایشی کمینه، آزمون واقعی SFU/TURN و شواهد sanitized با نتیجهٔ هر case؛ سپس پیشنهاد GO/NO-GO برای P3-A. این مرحله Domain/جدول/endpoint عمومی Call، privacy migration، ring عمومی، frontend اپلیکیشن، Group/Live rollout، Stories یا Bots نمی‌سازد. رسانه از Controller/SignalR عبور نمی‌کند؛ کد/کانفیگ MeliPayamak و AI/RAG دست‌نخورده‌اند.

اجازهٔ policy یا اجرای harness مجوز ورود به سرور موجود کاربر، تغییر firewall، deploy روی IIS، خرید host یا تست با افراد واقعی نیست. پیش از عملیات خارجی، محیط آزمایش ایزوله و روش دسترسی مجاز باید مشخص شوند؛ در نبود آن، بخش آماده‌سازی محلی ممکن است ولی REAL gate برابر PENDING_ENVIRONMENT است، نه PASS یا شکست اثبات‌شدهٔ LiveKit.

## ۲. پیش‌نیاز اجرایی و مسئولیت‌ها

| ورودی لازم پیش از اجرا | مالک / کنترل |
|---|---|
| Linux media lab اختصاصی/مجاز، نسخه و image digest ثابت | اپراتور محیط؛ بدون latest شناور و بدون تغییر host عادی |
| public endpoint، domain/CA معتبر، UDP رسانه و TURN TCP/TLS fallback | اپراتور شبکه؛ port/topology مطابق مستندات رسمی همان نسخه، نه بازکردن همهٔ portها |
| Windows/.NET9 control harness و دو media client کنترل‌شده | توسعه‌دهندهٔ Backend؛ synthetic media/actor، بدون حساب واقعی کودک یا SMS |
| حداقل دو مسیر شبکهٔ مجاز برای تست relay؛ امکان UDP-block در lab | اپراتور آزمون؛ تغییر شبکه فقط در محیط آزمایشِ مشخص |
| API credential/admin endpoint، credential موقت TURN و مجوز resourceهای RunId | secret store محیط؛ نه چت، source، argument shell، TRX یا commit |
| authority store آزمایشی و clock قابل‌اندازه‌گیری | harness؛ state مستقل برای Call/Room/User/DeviceSession/role/revoked؛ نه دیتابیس معمولی MakanApp |
| اندازه‌گیری توقف publish/receive و دسترسی fault injection | توسعه‌دهنده/بازبین امنیت؛ معیار و روش **قبل اجرا** ثبت شود |

API/SDK/version، license و ویژگی‌های واقعی provider هنگام اجرای S از [مستندات token/grants](https://docs.livekit.io/frontends/reference/tokens-grants/)، [RoomService API](https://docs.livekit.io/reference/other/roomservice-api/)، [deployment](https://docs.livekit.io/transport/self-hosting/deployment/) و [ports/firewall](https://docs.livekit.io/transport/self-hosting/ports-firewall/) همان نسخه بررسی و pin شوند. اکنون نام package یا port ثابتِ آزموده‌نشده انتخاب نشده است. SDK/HTTP adapter فنی فقط در harness/Infrastructure مناسب؛ Domain/Application موجود وابستگی provider نگیرد.

## ۳. بسته‌های کاری کوچک و ترتیب انجام

| بسته | اقدام بعدی قابل اجرا | تحویل قابل بازبینی / شرط عبور |
|---|---|---|
| S-W1 — Preflight | ثبت git baseline و policy، تأیید lab، pin image/SDK، طرح secret/log/ownership | manifest با SHA/digest و مجوز lab؛ کمبود محیط صریحاً pending |
| S-W2 — Tooling | ساخت harness .NET9 مستقل، runner caseها و دو client آزمایشی با SDK رسمی | build/test ابزار، authority state و clock trace؛ نه public business Call |
| S-W3 — Transport baseline | اجرای SFU واقعی، join scoped، synthetic track دوطرفه و TURN authenticated | stats رسانه و selected candidate pair، بدون ذخیرهٔ raw media |
| S-W4 — Admission/security | اجرای caseهای old-token/refresh/revoke/fault و ارزیابی مسیر غیرقابل bypass | timeline current-check/remove/media-stop؛ شکست critical با status مشخص |
| S-W5 — Network/cleanup | forced TURN، UDP-block→TLS fallback، reconnect15s و no-grace امنیتی | transport و نتیجهٔ failureها؛ cleanup فقط resourceهای RunId |
| S-W6 — Gate/Git | sanitized evidence، بازبینی امنیت، regression، commit/push/remote verify | PASS/FAIL/PENDING تفکیک‌شده؛ اجازهٔ full A/B/D/E فقط بعد PASS واقعی S |

مسیرهای **پیشنهادی آینده، نه فایل موجود**:

```text
tools/media-spike/MakanApp.MediaSecuritySpike/       # harness مستقل .NET9
tools/media-spike/clients/                         # client آزمایشی synthetic، نه frontend محصول
tools/media-spike/Invoke-MakanMediaSpike.ps1        # runner با exit code قطعی
tools/media-spike/deploy/                          # templateهای lab، بدون secret
docs/messaging/MESSENGER_P3_S_SECURITY_SPIKE_RESULT.md
.artifacts/messenger-p3-s/<RunId>/                  # evidence محلی ignored و sanitized
```

توضیح‌های کد/اسکریپت آینده فارسی، نام‌ها انگلیسی باشند. ایجاد client آزمایشی محدود، مجوز clone/تغییر frontend یا native app نیست؛ اگر آزمون نیازمند پروژهٔ client واقعی شد، workspace و scope جدا لازم است. actor/authority آزمایشی فقط rejoin-control را می‌سنجد؛ اثبات GAP-05 یا workflow اصلی هنوز در P3-A با Identity/SQL واقعی لازم است.

## ۴. قرارداد اجرای runner آینده

runner بالا هنوز وجود ندارد؛ فرمان‌های این بخش **قرارداد interface برای ساخت S-W2** هستند و نباید اکنون اجرا یا به‌عنوان ابزار موجود گزارش شوند. config path فقط نام محل فایل محافظت‌شده است؛ secret یا JWT در command line قرار نگیرد. runner باید schema config، isolated-environment marker، ownership RunId، HTTPS/certificate و نسخه‌های pinشده را validate کند و در نقص preflight متوقف شود.

```powershell
# پس از ساخت tooling و تأمین محیط مجاز، نه در تحویل فعلی:
pwsh tools/media-spike/Invoke-MakanMediaSpike.ps1 -Phase Preflight -RunId <RunId> -ConfigPath <ProtectedLabConfigPath>
pwsh tools/media-spike/Invoke-MakanMediaSpike.ps1 -Phase Run -Case All -RunId <RunId> -ConfigPath <ProtectedLabConfigPath>
pwsh tools/media-spike/Invoke-MakanMediaSpike.ps1 -Phase VerifyEvidence -RunId <RunId> -ConfigPath <ProtectedLabConfigPath>
pwsh tools/media-spike/Invoke-MakanMediaSpike.ps1 -Phase Cleanup -RunId <RunId> -ConfigPath <ProtectedLabConfigPath>
```

config آزمایشی آینده شامل endpointها، secret-referenceها، image/SDK digest، admission mode، opaque actor/device mappings، evidence directory و network profile است؛ **appsettings فعلی MakanApp نیست**. `InitialJoinTokenMaxTtlSeconds<=30` و `NetworkReconnectGraceSeconds=15` مطابق policy اعتبارسنجی شوند. سقف token اولیه، عمر session فعال یا refresh داخلی را ثابت نمی‌کند. grantها کمینه باشند؛ credential admin هرگز client-side نشود.

runner نتیجهٔ هر case را PASS/FAIL/NOT_RUN/INCONCLUSIVE به همراه expected/actual و شناسهٔ evidence ثبت کند. exit0 فقط برای همهٔ gateهای اجباری PASS؛ خطا و pending با exit غیرصفر و علت جدا، نه test skippedِ موفق. Cleanup فقط room/participant/process/container دارای label/شناسه و ownership همان RunId را بعد از تطبیق read-only هدف حذف/متوقف کند؛ حذف broad، production schema یا دادهٔ کاربر ممنوع است.

## ۵. admission/rejoin-control؛ گزینهٔ قابل آزمون، نه قابلیت اثبات‌شده

مسیر مرجع آزمایش: Backend authority آزمایشی → مجوز participant/session/role جاری → grant scoped → admission SFU → کنترل publish/receive. invalidation باید state مرجع را ابتدا revoke کند، issuance جدید را ببندد، remove فعال را dispatch کند و **هر** rejoin را با authority جاری بسنجد. عملیات شبکه داخل transaction طولانی قرار نگیرد؛ retry/remove تکراری هم نباید مجوز را زنده کند.

گزینهٔ اول برای spike، admission gateway غیرقابل bypass برای مسیرهای join/rejoin/signaling است؛ mapping token/identity/room/leg و پوشش reconnect/SDK refresh باید با نسخهٔ واقعی اثبات شود. تنها قراردادن reverse proxy یا claim سفارشی چنین اثباتی نیست؛ raw media از gateway .NET عبور نکند. در نبود مسیر enforcement معتبر، unknown mapping یا guard unavailable باید fail-closed باشد.

گزینهٔ check-on-join و removal فوریِ server-authorized فقط در حد policy مالک قابل ارزیابی است: callback async به‌تنهایی یا TTL کوتاه نتیجهٔ PASS نمی‌سازد. latency، callback loss/reorder، retry flood، خطای eviction و امکان اتصال مستقیم به SFU باید آزمایش شوند؛ نبود کنترل مؤثرِ ادامهٔ انتشار/دریافت، NO-GO است. نتیجهٔ spike باید انتخاب و trade-off واقعی را ثبت کند؛ اکنون هیچ‌یک approved implementation یا feature آمادهٔ LiveKit نامیده نمی‌شوند.

token همچنان bearer credential است؛ bind شدن به leg منطقی به‌خودی‌خود اثبات مالکیت سخت‌افزار یا مصونیت از سرقت token نیست. raw token/refresh credential فقط در حافظهٔ client آزمایش نگه‌داری شود؛ برای evidence شناسهٔ یک‌بارمصرف case کافی است. آزمایش rejoin باید token **واقعاً هنوز معتبر** داشته باشد، نه منتظر انقضای آن بماند و بعد deny را موفقیت revocation بنامد.

## ۶. caseهای اجباری و شاهد لازم

تمام caseهای زیر فعلاً **PLANNED_NOT_RUN** هستند. fake provider، TestServer و گزارش remove API بدون مشاهدهٔ رسانه، REAL_MEDIA_SECURITY_SPIKE_PASS نیستند.

| Case | روش / ورودی | expected و شاهد لازم |
|---|---|---|
| PS00 — Preflight | TLS/versions/ownership/secret-redaction، endpoint control و SFU | lab مجاز و pinشده؛ admin interface عمومی بدون مجوز در دسترس نباشد؛ نقص = NOT_RUN |
| PS01 — Token scope | grant معتبر و tampered/expired؛ room/user/device-leg/role اشتباه | backend issuance و admission مطابق binding؛ expiry اولیه حداکثر30s؛ هیچ admin grant/credential به client نرسد |
| PS02 — Media grant | Voice فقط mic؛ Viewer receive-only؛ تلاش SDK مستقیم برای camera/screen/audio publish نامجاز | SFU/کنترل سرور منع کند، نه UI؛ auth scope و track stats ثبت شود |
| PS03 — Baseline واقعی | دو synthetic participant با consent آزمایش، ارسال/دریافت دوطرفه | اتصال واقعی SFU و شمار track/frame/packet؛ بدون ضبط محتوا؛ هنوز real-device acceptance محصول نیست |
| PS04 — Old-token rejoin | Block/remove/ban/session revoke/account suspension/permission revoke را جداگانه، بلافاصله پس از join اجرا؛ قبل از expiry token قبلی با SDK و مسیر مستقیم دوباره وصل شود | مجوز جاری invalid؛ active leg قطع؛ future issuance deny؛ هر rejoin check و remove/admission-deny؛ هیچ تماس ادامه‌دار نامجاز؛ expiry واقعی و زمان مراحل شاهد باشند |
| PS05 — Refresh/replay | با نسخهٔ واقعی بررسی refresh داخلی؛ سپس credential معتبر فعلی را پس از revoke، حتی بعد از گذشت30s اولیه، در همان/دیگر اتصال replay کن؛ reconnect تکراری و race با remove | کنترل به initial token محدود نباشد؛ role/room/leg replay bypass نشود؛ اگر refresh قابل آزمون/مشاهده نیست INCONCLUSIVE، نه PASS |
| PS06 — Control failure | authority/backend unavailable، callback گم/دیر/تکراری/جعلی/جابجا، eviction API failure، reconnect flood و harness restart | guards fail-closed و revocation قابل اتکا؛ صرف retry/log کافی نیست؛ مسیر حذف/جلوگیری از ادامهٔ رسانه واقعاً مشاهده شود |
| PS07 — TURN واقعی | forced relay؛ UDP-block؛ TURN TCP/TLS روی topology/CA مجاز؛ credential نامعتبر/expired | selected candidate pair واقعاً relay و transport ثبت شود؛ TCP ICE مستقیم معادل TURN TLS نیست؛ open relay/credential دائمی client ممنوع |
| PS08 — Grace و امنیت | network-only media و signaling جدا و هم‌زمان؛ مرز15s/بیشتر؛ سپس revoke درون همان grace | فقط interruption عادی grace دارد؛ invalidation امنیتی حتی در grace فوراً remove/deny شود؛ هیچ15s مهلت امنیتی ایجاد نشود |
| PS09 — Isolation | room دیگر، actor دیگر، device-leg دیگر، late join بعد End و publisher promotion خوداظهاری | هیچ permission از شناسه/claim غیرمعتبر یا stale role ساخته نشود؛ parent/child یا school authority از دادهٔ client استنتاج نشود |
| PS10 — Evidence/recovery | manifest و شمار caseها، log redaction، تکرار RunId/retry، cleanup هدفمند | هیچ مورد اجباری مفقود/skipped به PASS تبدیل نشود؛ secret/PII/SDP/raw media ذخیره نشود؛ فقط resource آزمایش همان RunId پاک شود |

PS02 نقش‌های آینده را صرفاً با grant آزمایشی می‌سنجد؛ این کار پیاده‌سازی ChannelLive، promotion عمومی یا domain role نیست. قوانین تفصیلی Guardian/Teacher/Minor، first-Accept SQL و call history، tests مستقل **P3-A/D/E** هستند و از نتیجهٔ S پاس نمی‌گیرند.

## ۷. اندازه‌گیری و criteria؛ no-grace با ادعای zero-latency یکی نیست

برای هر revoke/rejoin حداقل timestampهای UTC و elapsed monotonic این مراحل ثبت شوند: تغییر authority، شروع/نتیجهٔ issuance check، رویداد تلاش rejoin، current check، dispatch/ack removal، آخرین publish/receive مشاهده‌شده و پایان اتصال. اختلاف ساعت hostها و uncertainty ثبت شود؛ موفقیت command remove به معنی پایان رسانه نیست. بستهٔ bufferشده/در حال انتقال از جریان **ادامه‌دار** نامجاز تفکیک شود.

security grace برابر صفر است؛ تأخیر پردازش/شبکه باید اندازه‌گیری و صادقانه گزارش شود، نه اینکه به یک reconnect grace تازه تبدیل شود. روش اندازه‌گیری و معیار قابل‌قبول cessation قبل از اجرا با بازبین امنیت ثبت شود؛ از عدد ring30s، TTL30s یا grace15s برای مجازکردن رسانهٔ نامجاز استفاده نشود. عدد یا SLA تازه‌ای در این plan تصویب نشده است. اگر معیار/مشاهده برای اثبات کنترل کافی نیست، نتیجه INCONCLUSIVE است و production GO داده نشود؛ به‌صرف میانگین خوب fault path ردشده پنهان نماند.

## ۸. evidence، exit gate و نتیجهٔ صادقانه

گزارش نتیجهٔ آینده شامل SHA کد و policy، نسخه/digest provider/SDK، topology بدون secret، RunId، پیش‌نیازهای فراهم/ناقص، روش هر case، expected/actual، شمار pass/fail/not-run، timeline و selected transport sanitized، محدودیت و تصمیم admission باشد. packet capture/raw SDP/raw JWT/شماره/اطلاعات افراد در گزارش یا commit نیاید؛ synthetic media هم ذخیره نشود. گزارش عمومی فقط کمینهٔ آماری لازم را داشته باشد.

| نتیجهٔ S | شرط / پیامد |
|---|---|
| PASS | تمام PS00–PS10 واقعی و شاهد قابل بررسی؛ rejoin با credential معتبر/refresh‌شده و faultها کنترل‌شده؛ TURN fallback اثبات‌شده؛ build/test/Git موفق؛ سپس آمادهٔ شروع P3-A، نه Production approval یا mobile acceptance |
| FAIL امنیتی | آزمون واقعی نشان دهد کنترل ادامهٔ تماس نامجاز رضایت‌بخش نیست؛ نتیجه دقیق LIVEKIT_REJOIN_SECURITY_GATE_FAILED؛ توقف media production approval و بررسی Janus+Coturn با gate معادل، بدون تضعیف authorization |
| PENDING_ENVIRONMENT / NOT_RUN / INCONCLUSIVE | lab/کلاینت/دسترسی/شاهد کافی موجود نیست یا آزمایش قطعی نشده؛ نه PASS و نه ادعای شکست اجراشده؛ دقیقاً dependency/مشاهدهٔ لازم ثبت شود؛ A/B/D/E کامل شروع نشوند |

تمام suiteهای regression Backend نیز در اجرای آینده الزامی‌اند؛ هیچ شمار تست پیش‌بینی‌شده، اثبات تازهٔ رسانه نیست. پایان S نتیجهٔ مستقل و commit متمرکز دارد؛ با پایان یک script خودکار به business rollout نروید.

## ۹. validation و Git مرحلهٔ اجرایی آینده

در worktree Messenger، بدون لمس AI dirty workspace: `git status --short` و baseline؛ سپس build/test ابزار و REAL caseهای lab؛ پس از آن فرمان‌های موجود مخزن:

```powershell
dotnet restore MakanApp.sln
dotnet build MakanApp.sln --configuration Debug --nologo
dotnet test MakanApp.sln --configuration Debug --nologo
dotnet build MakanApp.sln --configuration Debug --nologo
dotnet test MakanApp.sln --configuration Debug --nologo
git status --short
git diff --stat
git diff
```

فقط فایل‌های S با مسیر صریح stage؛ `git diff --cached --stat` و `git diff --cached`، secret/generated-artifact review، commit متمرکز، push به upstream موجود، fetch و تطبیق SHA remote، سپس گزارش فارسی. نام branch و upstream **همان زمان** بررسی شوند؛ اکنون messenger/p0 به origin/main متصل است، نام محلی/remote یکسان فرض نشود. بدون upstream طبق AGENTS سؤال شود؛ هرگز force-push/تغییر تاریخچه یا stage فایل‌های AI انجام نشود. عدم موفقیت Build/Test مانع commit/push است؛ موفقیت آن‌ها جای REAL media gate نیست.

## ۱۰. وضعیت این تحویل مستنداتی

این plan اجرای S را آغاز نکرده؛ فایل/tool/endpoint/migration/config محصول ایجاد نشده است. مقادیر فریز‌شده در [policy](MESSENGER_P3_APPROVED_MEDIA_POLICY_V1.md)، تطبیق پیشنهادها در [decision register](MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md#p3-policy-v1-owner-approval)، ترتیب در [roadmap](MESSENGER_P3_IMPLEMENTATION_ROADMAP.md) و gateهای مراحل بعد در [test matrix](MESSENGER_P3_TEST_ACCEPTANCE_MATRIX.md) قرار دارند.

```text
P3_MEDIA_POLICY_V1_APPROVED
GO_FOR_P3_MEDIA_SECURITY_SPIKE
P3_S_EXECUTION=NOT_STARTED
LIVEKIT_REJOIN_SECURITY_GATE=NOT_TESTED
P3_PRODUCTION_MEDIA_APPROVAL=NOT_GRANTED
P1_PUSH_RUNTIME_PENDING
```
