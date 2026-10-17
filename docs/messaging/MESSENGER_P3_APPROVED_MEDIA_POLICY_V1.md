# Makan Messenger P3 Media Policy v1

وضعیت محصول: **APPROVED_FOR_IMPLEMENTATION / APPROVED_AND_FROZEN**.

مالک تصمیم: Product Owner؛ تاریخ ثبت `2026-10-17`؛ منبع تأیید: درخواست صریح «P3 OWNER DECISIONS — APPROVED AND FROZEN». مبنای مخزن پیش از ثبت: `47e620077c5809b6d8e1b6ad2515f1afec48c247` در worktree مستقل Messenger. این approval محصول، **اثبات implementation، موفقیت P3-S یا پذیرش Production نیست**. اقدام این تحویل فقط ثبت سیاست و آماده‌کردن [برنامهٔ اجرایی P3-S](MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_PLAN.md) است.

## ۱. مرجع جاری و حفظ تاریخچه

این سند مقادیر مصوب P3 را در تعارض با پیشنهادهای [P3-0](MESSENGER_P3_MEDIA_ARCHITECTURE.md) جایگزین می‌کند؛ پیشنهادها، مقایسه‌ها و شواهد تاریخی حذف نمی‌شوند. [نگاشت تصمیم‌های قبلی به تأیید جدید](MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md#p3-policy-v1-owner-approval) مشخص می‌کند کدام قسمت واقعاً approved، تغییرکرده یا هنوز خارج تأیید است. تأیید این متن به معنی تأیید ضمنی همهٔ Rهای قبلی نیست.

.NET 9، ASP.NET Core Controllers، EF Core SQL Server، Onion Architecture و Modular Monolith ثابت‌اند. [GAP-05](../architecture/GAP_05_MESSENGER_CHILD_SAFETY.md) و مرز [Backoffice](../architecture/BACKOFFICE_PRODUCT_DECISIONS.md) حفظ می‌شوند؛ تصمیم‌های تماسِ رابطه‌محور این سند مجوز تغییر سن، نقش مدرسه، Discovery عمومی یا خواندن private chat نیستند. منبع رفتار اجراشده همچنان source/migration/tests است، نه این policy.

## ۲. معماری و مسیر رسانهٔ مصوب

| موضوع | قرارداد مصوب v1 |
|---|---|
| Primary | LiveKit Self-hosted |
| Fallback | Janus VideoRoom + Coturn؛ فقط در شکست acceptance امنیتی/عملیاتی LiveKit یا نیاز فنی اثبات‌شدهٔ بعدی |
| Backend | Windows/.NET موجود، مالک authorization/control/state و metadata رسمی |
| Media host | Linux اختصاصی، مستقل از ASP.NET Backend |
| Voice، Video، Group Voice/Video و Channel Live | همگی SFU؛ مسیر جداگانهٔ production P2P در v1 ساخته نشود |
| Screen Share | یک media track درون همان Call، از SFU |
| SignalR | signaling/control/state؛ نه مسیر عبور audio/video/screen frames |
| Controller | HTTP transport/use case؛ هیچ frame رسانه‌ای از Controller عبور نکند |

Fallback خودکار حین تماس نیست و نباید صرف انتخاب نام آن امن/آماده تلقی شود؛ gate امنیتی و operational acceptance گزینهٔ جایگزین هم لازم است. provider credential دائمی در client ممنوع است. در این تحویل هیچ binary/SDK یا media service نصب نشده است.

## ۳. STUN / TURN

TURN برای fallback Production **الزامی** است: authenticated، configurable، monitored و با endpoint عمومی قابل‌دسترسی. UDP مسیر اصلی و TCP/TLS fallback است. credential دائمی در client embed نشود. pilot می‌تواند TURN را روی همان Linux media host اختصاصی داشته باشد؛ طراحی باید جداسازی TURN را در آینده ممکن کند. این تصمیم، بازکردن firewall، تغییر IIS، خرید host یا deploy در این تحویل نیست.

## ۴. media authorization و LiveKit rejoin gate

Backend فقط **پس از بررسی مجوز جاری** media access بدهد. binding معادل این مقادیر الزامی است: `CallId / RoomId / UserId / DeviceSessionId / participant role`. لازم نیست UserId واقعی یا PII در claim عمومی provider نوشته شود؛ mapping امن server-side و شناسهٔ opaque می‌تواند همان binding را enforce کند. role/source grant باید با مجوز جاری منطبق باشد؛ permanent LiveKit administrative credential به client داده نشود.

**حداکثر TTL توکن initial join برابر۳۰ ثانیه است**؛ این سقف با عمر تماس، ring timeout یا grace یکی نیست. محدودکردن initial token، به معنی لغو خودکار participant فعال یا اثبات TTL توکن‌های refreshشدهٔ داخلی SFU نیست؛ رفتار واقعی باید در P3-S ثبت و کنترل شود.

جریان الزامی revocation:

```text
Block / remove / ban / session revoke / permission invalidation
-> Backend CallParticipant permission invalid
-> SFU participant removal
-> no future media-token issuance
-> current authorization checked on attempted rejoin
-> unauthorized participant immediately removed / admission denied
```

RemoveParticipant به‌تنهایی credential revocation فوری محسوب نمی‌شود. اتصال دوباره با توکن قدیمی هنوز معتبر باید از مکانیزم admission/rejoin-control انتخاب‌شده، وضعیت جاری Backend را بررسی کند؛ participant نامجاز نباید تماس ادامه‌دارِ خارج کنترل ایجاد کند. اصل ممنوعیت را با TTL، client cooperation یا callbackِ بررسی‌نشده جایگزین نکنید.

یک **REAL security spike** مستقل اجباری است. باید حذف/rejoin با توکن هنوز معتبر، token refresh داخلی، failure/retry/reconnect و قطع رسانه واقعاً آزمایش شوند؛ mock یا Build/TestServer این gate را پاس نمی‌کنند. اگر کنترل ادامهٔ تماس نامجاز به‌طور رضایت‌بخش اثبات نشد، approval رسانهٔ Production متوقف و نتیجه دقیقاً `LIVEKIT_REJOIN_SECURITY_GATE_FAILED` گزارش شود؛ authorization تضعیف نشود. «GO برای اجرای spike» هرگز به معنی «gate پاس شده» نیست.

## ۵. privacy و Child Safety مصوب تماس

Adult Personal: پیش‌فرض `WhoCanCallMe=Contacts`؛ مقدارهای قابل‌تنظیم می‌توانند از مدل `Everyone / Contacts / Nobody` فعلی پیروی کنند. Contacts از دفترچهٔ **مقصد** است، نه caller. Minor و Unknown در Personal Direct عمومی، `WhoCanCallMe=Nobody` دارند و از طریق privacy settings تماس زندهٔ عمومی برایشان فعال نمی‌شود. دانستن UserId مجوز نیست؛ Block ارتباط Direct جدید را در هر دو جهت منع می‌کند.

| رابطه/عملیات | قرارداد مصوب v1 |
|---|---|
| Parent ↔ Child | live calling فقط با GuardianRelation فعال و معتبر و current account/Block restrictions مجاز است |
| GuardianRelation | فقط مسیر ارتباط خود Parent/Child را پشتیبانی می‌کند؛ monitoring، silent listening، recording access و private chat history access نمی‌دهد |
| Teacher ↔ Minor Student | private 1:1 Voice/Video ممنوع |
| Teacher ↔ Unknown Student | مانند Minor محافظت شود؛ عنوان نقش اثبات Adult نیست |
| Teacher ↔ Adult Student | 1:1 فقط با رابطهٔ آموزشی جاری **و** target call privacy مجاز می‌تواند برقرار شود |
| Minor ↔ Minor | private Personal 1:1 ممنوع |
| Official / Class / Group call مجاز | حضور Minorها تابع current membership، TeacherAssignment/Enrollment مربوط، scope سازمان/کلاس و Child Safety فعلی است |
| Manager | نقش Manager مجوز خودکار private call ایجاد نمی‌کند |
| Stranger → Minor/Unknown | ممنوعیت موجود GAP-05 پابرجاست؛ privacy یا شناسه bypass نیست |

Parent/Child یک استثنای **صریح رابطه‌محور** همین approval نسبت به منع تماس عمومی protected است، نه تغییر Nobody به Everyone یا کشف عمومی کودک. binding رابطه باید server-authoritative و در همان منبع/سازمان معتبر باشد؛ Guardian خوداظهاری یا UserId خام مجوز نیست. برقراری تماس با یکی از طرف‌ها حق ورود به تماس او با دیگری نمی‌دهد.

قواعد مصوب تماس زنده، اجازهٔ واردکردن media به AI/RAG، ضبط برای Safety یا ادعای اسکن صوت/تصویر توسط موتور متن نیستند. محدودیت موتور متن موجود حفظ و صریح بیان شود؛ pre-call authorization و guards لازم در unavailable/ابهام مجوز fail-closed باشند. مجوزهای جدید live، گیت محتوای پیام ذخیره‌شده یا privacy تاریخچهٔ قدیمی را ضعیف نمی‌کنند.

## ۶. تماس 1:1، lifecycle و چنددستگاهی

هر participant دارای مجوز می‌تواند تماس واجد شرایط را آغاز کند؛ Voice و Video در v1 هدف مصوب‌اند. lifecycle اولیه: `Created → Ringing → Connecting → Active → Ended`. outcome/reason می‌تواند Missed/Declined/Cancelled/Failed/AnsweredElsewhere را تفکیک کند؛ state/outcome تکراریِ بی‌نیاز ساخته نشود. AnsweredElsewhere نتیجهٔ leg/device بازنده است و نباید تماس پذیرفته‌شدهٔ دستگاه برنده را به‌اشتباه پایان دهد.

ring timeout پیش‌فرض Direct برابر **۳۰ ثانیه** و configurable است. همهٔ deviceهای فعلاً eligible مخاطب می‌توانند ring شوند؛ first valid Accept باید **transactional** باشد. deviceهای دیگر event معادل `CallAnsweredElsewhere` بگیرند؛ دو device هرگز دو accepted call leg نسازند. receipt و retry اثر همان عملیات را حفظ کنند؛ ساعت client مرجع deadline نیست.

این policy نام مرحلهٔ Created را تصویب می‌کند؛ جزئیات persistence transient/durable آن در مدل آینده مشخص می‌شود و اکنون جدول/enum ساخته نشده است. Busy/call waiting، decline یک device در برابر کل دعوت و handoff caller به‌صرف سکوت این approval freeze نشده‌اند؛ برای P3-S dependency نیستند و در قرارداد P3-A نباید حدس زده شوند.

## ۷. reconnect و تقدم امنیت

پیش‌فرض grace برای **media و signaling هر دو۱۵ ثانیه** و configurable است؛ فقط وقفهٔ عادی موقت شبکه می‌تواند از آن استفاده کند. پیشنهاد قدیمی media۲۰s/control۶۰s دیگر مرجع v1 نیست. برای موارد زیر **هیچ grace وجود ندارد**:

- session revoke؛ account suspension؛ Block؛
- call participant removal؛ explicit call termination؛
- permission revocation که removal می‌خواهد.

security revocation بلافاصله مقدم است؛ grace شبکه نباید پنهانی تأخیر امنیتی شود. زمان پردازش واقعی detection→check→remove→media-stop باید در spike اندازه‌گیری و بدون موفقیت جعلی گزارش شود؛ هیچ مقدار تازه‌ای به‌عنوان «مهلت مجاز ادامهٔ تماس نامجاز» در این سند تصویب نشده است.

## ۸. Group Call و Channel Live

GroupCall به Group/Official conversation مجاز bound است؛ Conversation membership منبع حقیقت عضویت است و برای کلاس رسمی همان منابع جاری آموزشی authority آن هستند. JoinCall عضویت دائمی گفتگو نمی‌سازد. کمینهٔ permission صریح `ManageCalls` برای start/end/remove call participant است: Owner مجاز، Admin فقط با ManageCalls و Member به‌طور پیش‌فرض فاقد start/manage؛ اعضای مجاز طبق conversation policy جاری join می‌کنند. role مدرسه یا صرف cached participant جای منابع رسمی را نگیرد.

ChannelLive از GroupCall مستقل است. permission صریح `ManageLive` برای start/end لازم است؛ Owner/Admin دارای اختیار مؤثر ManageLive مجازند. Viewer به‌طور پیش‌فرض receive-only است و نباید audio/video منتشر کند. promotion به Speaker/CoHost باید explicit، server-authorized و auditable باشد. roleهای پیشنهادی مصوب برای طراحی عبارت‌اند از `Host / CoHost / Speaker / Viewer`؛ این تأیید، capability عددی یا endpoint موجود ایجاد نمی‌کند.

پس پیشنهاد تاریخی «ManageCalls+PublishMessages برای Live» یا «promotion همیشه deferred» مرجع جاری نیست. جزئیات transport باید sourceهای قابل انتشار را server-side enforce کند؛ UI یا role string ورودی authority نیست. permission موازی بی‌نیاز ساخته نشود؛ تاریخچهٔ Adminهای قبلی خودکار escalation نگیرد.

## ۹. Screen Share و Recording

Screen Share یک track داخل Call است؛ سقف هم‌زمان v1 برابر **۱**. در Direct هر participant مجاز می‌تواند share کند؛ در Group/Live، Host/CoHost یا participant صریحاً مجاز. توقف/حذف participant باید share او را پایان دهد. media grant و admission همان scope و permission را رعایت کنند؛ auto-recording ممنوع است. ساخت permission مستقل ShareScreen به‌خودی‌خود الزام این policy نیست؛ کمینهٔ authorization صریح کافی است.

`RECORDING=DEFERRED`. v1 نباید hidden/server/automatic recording، transcription یا speech-to-text archive بسازد. recording آینده approval مستقل Product، Legal، Privacy، Storage، Retention و Access Control می‌خواهد. کلید یا قابلیت Egress به client داده نشود؛ ضبط آزمون هم از خود این approval مجوز ذخیرهٔ صدای افراد واقعی نمی‌گیرد.

## ۱۰. history و ظرفیت pilot

history کاربر با Messenger history به‌شکل **SystemEvent امن یا مکانیزم معادل** integrate شود: call type، incoming/outgoing، answered/missed/declined، StartedAtUtc/EndedAtUtc و duration در صورت کاربرد. فقط metadata مجاز؛ بدون raw media یا اطلاعات شبکهٔ غیرضروری. projection هر بیننده باید مجوز منبع را حفظ کند، نه اینکه call receipt مجوز private history دیگری شود.

در source مبنای این ثبت، MessageKind فقط Text/Image/Video/Voice/File/ContactCard/Poll/Checklist دارد؛ SystemEvent و call-history mechanism هنوز پیاده نیستند. انتخاب سازگاری یا افزودن قرارداد history در P3-A انجام می‌شود؛ پیام Text جعلی یا API ساخته‌نشده را قابلیت موجود ننامید. retention پیش‌فرض **technical CallSession metadata برابر۳۰ روز، configurable** است؛ retention حقوقی تضمین‌شده یا مدت نگه‌داری پیام کاربر/audit/legal hold از آن استنتاج نشود. پیشنهاد قبلی «۳۰ روز نمای شخصی» با این تصمیم یکسان نیست.

| safeguard اولیه | مقدار مصوب | مرز ادعا |
|---|---|---|
| Group Video | ۸ active participants پیش‌فرض، configurable | pilot safety، نه ظرفیت Production یا سقف دائمی محصول |
| Group Voice | ۱۶ active participants پیش‌فرض، configurable | با تعداد کل اعضای Conversation یکی نیست |
| Channel Live viewers | runtime limit قابل‌تنظیم، بدون عدد تجاری freeze‌شده | rollout feature-gated تا real capacity/load acceptance |

## ۱۱. Push، مسیرهای جدا و ترتیب اجرا

`P1_PUSH_RUNTIME_PENDING` حفظ می‌شود. foreground ringing می‌تواند از realtime مجاز موجود استفاده کند؛ reliable mobile/background incoming calls تا real-device Push/background-call acceptance آمادهٔ Production نامیده نشوند. وضعیت فعلی WebPush/FCM، تأیید دریافت واقعی یا معادل APNs VoIP/CallKit نیست.

ترتیب مصوب و جایگزین ترتیب پیشنهادی P3-0:

1. **P3-S — Media Security Spike:** LiveKit self-hosted واقعی، token scope، removal/rejoin، TURN؛ بدون business rollout.
2. **P3-A — Call Control + 1:1 Voice:** Domain/persistence/state، signaling، چندdevice، privacy/Block/Child Safety، token issuance و real voice؛ فقط پس از PASS واقعی P3-S.
3. **P3-B — 1:1 Video + Screen Share**.
4. **P3-C — Media Infrastructure Acceptance:** TURN-forced، network failure/reconnect/revocation و real Web/Android/iOS در صورت دسترسی.
5. **P3-D — Group Voice/Video**.
6. **P3-E — Channel Live**.
7. **P3-F — Production Media Acceptance:** real networks، load، observability، recovery، bandwidth، security و runtime deployment مجاز.
8. پس از P3-F: Stories track، سپس Bots/Mini Apps track مستقل؛ نباید core Voice/Video را معطل کنند.

این تحویل P3-S را **برنامه‌ریزی** می‌کند، نه اجرا. اجرای بعدی S می‌تواند tooling/script ایزوله داشته باشد ولی public production call behavior را پنهانی فعال نکند. A/B/D/E پیش از پاس S شروع نشوند. هیچ مجوز تغییر server واقعی، AI/RAG، MeliPayamak، frontend، Payment یا فایل‌های dirty قبلی از این ثبت استنتاج نمی‌شود.

## ۱۲. وضعیت دقیق پس از ثبت

```text
P3_MEDIA_POLICY_V1_APPROVED
GO_FOR_P3_MEDIA_SECURITY_SPIKE
P3_S_EXECUTION=NOT_STARTED
LIVEKIT_REJOIN_SECURITY_GATE=NOT_TESTED
P3_PRODUCTION_MEDIA_APPROVAL=NOT_GRANTED
P1_PUSH_RUNTIME_PENDING
```

تفاوت آموزشی کلیدی: «تصمیم محصول approved»، «برنامهٔ اجرایی آماده»، «آزمون رسانه PASS» و «Production accepted» چهار نتیجهٔ جدا هستند. مسیر مطالعه: این policy → نگاشت تصمیم‌های قبلی → برنامهٔ P3-S → ماتریس و مدارک runtime آینده. build/test این ثبت فقط regression Backend است؛ اثر جدیدی در endpoint، Domain یا SQL ندارد.

## ۱۳. اعتبارسنجی همین ثبت مستنداتی

restore و build موفق با صفر warning/error؛ suite کامل اولیه و final build/test هر دو موفق: **558 Unit + 1023 Integration + 14 Architecture = 1595 executed/passed** در هر دور، failed=0 و skipped=0. این‌ها همان1595 تست موجودند، نه3190 تست متفاوت و نه تست جدید Call. TRXها در `.artifacts/messenger-p3-policy/full` و `.artifacts/messenger-p3-policy/final` نگه‌داری محلی شده‌اند؛ Integration به‌ترتیب حدود10:53 و6:03 طول کشید.

بررسی محدود اسناد شامل allowlist هفت‌سندی، حفظ متن تاریخی پنج سند قبلی،64 لینک نسبی، fenceها، نگاشت R01–R20، یکتایی PS00–PS10 و وضعیت‌های صریح NOT_STARTED/NOT_TESTED بود. بررسی عددها ارقام فارسی را نیز normalize کرد؛ Source/Test/SDK/config/sln/csproj و PDF مرجع تغییر نکردند. migration تازه یا apply روی دیتابیس عادی/سرور انجام نشد؛ تست‌های SQL فقط fixture ایزولهٔ موجود را اجرا کردند. Git ممکن است advisory تبدیل LF→CRLF بدهد؛ این پیام خطای build یا warning کامپایل نیست. نتیجهٔ واقعی commit/push/remote verification پس از انجام در گزارش پایان اعلام می‌شود؛ این فایل hash commitِ خودش را پیشاپیش جعل نمی‌کند.
