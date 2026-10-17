# تصمیم‌های مصوب و موارد باز برای بازبینی نهایی

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

تاریخ ثبت GAP-04: ۲۰۲۶-۱۰-۱۵. این register مجوز پیاده‌سازی موارد آینده نیست.

**تصمیم جاری:** TOTP Authenticator روش اصلی Backoffice MFA است؛ پیاده‌سازی واقعی، مستقل از provider بیرونی و پشت portهای قابل جایگزینی است. TOTP انتخاب مصوب است، نه placeholder ناامن یا موقت. جزئیات در [قرارداد GAP-04](GAP_04_BACKOFFICE_TOTP_MFA.md) است.

| مورد | پیاده‌سازی جاری | دلیل defer | گزینه آینده | اثر Production | اولویت |
|---|---|---|---|---|---|
| WebAuthn / Passkeys | TOTP + recovery | خارج release مصوب و نیازمند credential/device lifecycle | عامل مقاوم‌تر به phishing در همان مرز MFA | TOTP فعلی معتبر است؛ upgrade با rollout مستقل | P2 |
| SMS fallback | SMS فقط برای login OTP موجود؛ MFA پیامکی نداریم | delivery/privacy/cost و قواعد fallback تصویب نشده | provider/consent مصوب، بدون downgrade پنهان | نبود SMS مانع اجرای TOTP نیست | P2 |
| enterprise identity / SSO | Identity موجود + TOTP؛ IAM پلتفرم ثابت | نیاز سازمانی و trust/link/recovery contract مشخص نیست | OIDC/SSO پس از تصویب | مهاجرت نباید roleهای سازمان و پلتفرم را یکی کند | P2 |
| last-owner offline recovery | recovery code یک‌بار؛ Owner دیگر می‌تواند reset کند | فقدان TOTP و همه codeها نیازمند authority/عملیات اضطراری صریح است | رویه offline audited با احراز هویت مالک، نه API backdoor | `DEFERRED_OPERATIONAL_DECISION`؛ ریسک قفل‌شدن آخرین Owner | P1 پیش از rollout |
| key custody / migration میان سرورها | key ring پایدار + DPAPI Windows یا certificate | destination/backup/rotation/مسئول عملیاتی به محیط وابسته است | secret/certificate custody و rehearsal انتقال مستقل | پیش‌نیاز استفاده عملیاتی MFA؛ از دست‌رفتن کلید می‌تواند re-enrollment بخواهد | P1 پیش از rollout |
| سن و Child Safety / GAP-05 | سیاست v1 مصوب، age verification audited، رابطه/رضایت و Internal Makan اجرا شده‌اند | تصمیم P0 قبلی رفع شده؛ محدودیت‌های ارتقای زیر مستقل‌اند | توسعه پشت همان مرزها | closure کد، اثبات deployment یا دقت معنایی کامل نیست | رفتار جاری الزامی |

## افزوده GAP-05 — رفتار جاری و گزینه‌های ارتقا

Internal Makan Content Safety تصمیم مصوب جاری است؛ موتور قواعد متن با `PolicyVersion`، نتیجه صریح و Fail-closed وجود دارد. جای خالی AI با موفقیت ساختگی پر نشده است. رسانه فاقد پشتیبانی معنایی، ReviewRequired و غیرقابل انتشار معمولی است. منبع جاری: [قرارداد GAP-05](../architecture/GAP_05_MESSENGER_CHILD_SAFETY.md).

| مورد | پیاده‌سازی جاری | دلیل defer | گزینه آینده | اثر Production | اولویت |
|---|---|---|---|---|---|
| بازبینی حقوقی آستانه ۱۸ | مرز محصول مصوب ۱۸، مستقل از نقش؛ Unknown محافظت‌شده | تأیید قوانین همه حوزه‌ها در scope کدنویسی نیست | بازبینی حقوقی/محصولی بازار هدف و policy نسخه‌دار | ادعای compliance حقوقی نمی‌شود؛ تغییر نیازمند rollout/test است | P1 پیش از عرضه هر بازار |
| richer age assurance/provider | شاهد مدیر مجاز، تعارض auditable و حل Owner با MFA تازه | provider هویتی جدید تصویب نشده | مدرک/consent/provider قوی‌تر پشت گردش موجود | شماره تلفن یا DOB خوداظهاری مدرک سن نیست | P1 |
| درخواست پیام عمومی برای Minor | Stranger Direct ممنوع؛ هیچ message-request عمومی وجود ندارد | خارج قرارداد v1 | فقط با consent/abuse/notification مصوب | رفتار پیش‌فرض باید همچنان deny باشد | P2 |
| گروه عمومی یا کانال Minor | ساخت عمومی/کانال و publish کانال ممنوع؛ گروه خصوصی و رضایت محدود موجود | محصول v1 صریحاً منع کرده است | تصمیم محصول جدید با guard و moderation متناسب | تا تصویب، ممنوعیت server-side حفظ شود | P2 |
| classifier داخلی قوی‌تر | قواعد قطعی محدود متن؛ Known pattern و لینک/رسانه unsupported به review می‌روند | مدل/ارزیابی معنایی/بودجه/زبان‌های هدف تصویب نشده | classifier یا ML بازبینی‌شده داخلی پشت IContentSafetyProvider | false positive/negative واقعی‌اند؛ queue انسانی و ظرفیت آن لازم است | P1 |
| provider بیرونی Safety | هیچ vendor یا انتقال محتوا به بیرون وجود ندارد | privacy/قرارداد/region/هزینه/دقت مستقل لازم است | adapter مصوب پشت همان port | بدون approval، raw private content خارج نمی‌رود | P2 |
| اسکن معنایی تصویر/ویدئو/صدا/فایل | ReviewRequired، evidence محدود و بدون انتشار عادی در مسیر محافظت‌شده | موتور متن bytes را تحلیل نمی‌کند | pipeline مصوب با quarantine، دسترسی و failure policy | صرف MIME/signature اثبات ایمنی محتوا یا AV نیست؛ GAP-11 مستقل است | P1 |
| appeal، SLA و retention پیشرفته | پرونده، claim، escalation، action و audit موجودند؛ حذف خودکار نداریم | مهلت/نگه‌داری حقوقی و عملیات تیم نیازمند تصمیم‌اند | workflow مصوب و job/guard قابل تست | GAP-20 و نگه‌داری storage همچنان بازند | P1 |

دیگر gapهای پرداخت، Push/SMS اعلان، worker recovery، observability، CI/CD و AI در [ممیزی غیر AI](NON_AI_BACKEND_FINAL_GAP_AUDIT.md) یا track مستقل خود باقی‌اند. این register برای هیچ‌کدام implementation یا approval تازه ادعا نمی‌کند.

## استثناهای عملیاتی انتشار HTTP — ۲۰۲۶-۱۰-۱۵

مالک پس از اعلام خطر، دو استثنای جداگانهٔ Hosting:AllowInsecureHttp و Database:AllowUntrustedServerCertificate را فقط برای انتشار فعلی Production تأیید کرد. defaults امن باقی‌اند؛ Encrypt=False، wildcard CORS، proxy نامعتبر و bypass مجوز مجاز نشده‌اند. فایل Staging حذف و فایل واقعی Production محلی/ignored شده است. این تصمیم نه سیاست جدید دامنه است، نه تأیید runtime principal پرقدرت و نه closure smoke امن GAP-02.

بدهی عملیاتی: HTTPS معتبر، گواهی SQL قابل اعتماد، runtime principal کم‌اختیار، custody/ACL و recovery واقعی کلیدهای MFA، نگه‌داری فایل خصوصی، Origin دقیق وب‌کلاینت و smoke محیط هدف. پس از آماده شدن TLS، RequireHttps=true، هر دو استثنا=false و TrustServerCertificate=False شوند. [مراحل اجرایی](../production/DEPLOYMENT_IIS.md). شواهد محلی کد و SQL جای rehearsal یا مشاهدهٔ سرور را نمی‌گیرند.

## DOC-CONTRACT-01 — اختلاف نمایش Enum در OpenAPI و MVC

در همگام‌سازی جاری، schema خام MessageKind و OrganizationRole نوع integer داشت، ولی serialize همان CLR Enum با JsonOptions واقعی MVC خروجی رشته‌ای تولید کرد. literalهای مرجع مستندات از serializer واقعی استخراج و نوع خام OpenAPI جدا نشان داده شد. این اختلاف می‌تواند client generator را به نوع عددی نادرست هدایت کند؛ وجود schema عددی به معنی خروجی عددی Controller نیست. JsonStringEnumConverter ممکن است ورودی عددی را هم بپذیرد؛ canonical output و قواعد enum/Flags باید جدا حفظ شوند.

وضعیت تاریخیِ کار مستندسازی: OPEN_RUNTIME_OPENAPI_ALIGNMENT؛ آن کار مجوز اصلاح runtime نداشت. وضعیت جاری Messenger P0 در ۲۰۲۶-۱۰-۱۶: **CLOSED_IN_CODE_AND_LOCAL_CONTRACT_TESTS**. HTTP JSON options با MVC هم‌تراز و type مفقود در .NET 9 با transformer کوچک تکمیل شد؛ enum/nullable/Flags اصلی حفظ شدند. `MessagingOpenApiContractTests`، JSON واقعی MVC/Hub، response واقعی Privacy و رد نام/bit ناشناخته را بررسی می‌کند. این closure اثبات انتشار OpenAPI روی سرور نیست.

## مرز تحویل Messenger P0 — ۲۰۲۶-۱۰-۱۶

مالک آزمون این تحویل را فقط محلی خواست؛ deploy یا REAL SERVER PASS مجاز/انجام‌شده نیست. [Handoff دقیق](../messaging/MESSENGER_P0_FRONTEND_HANDOFF.md) و [ممیزی پذیرش](../messaging/MESSENGER_P0_DELIVERY_AUDIT.md) مرجع‌اند. این register مجوز اجرای خودکار P1 نیست.

| شناسه | وضعیت جاری | شاهد یا مرز |
|---|---|---|
| B01 تأیید اولیهٔ سن شخصی | حل‌شده در کد | endpoint UsersManage + MFA تازه، شاهد append-only، rowversion/audit؛ بدون مدرسهٔ جعلی یا اعتماد به OTP/DOB خوداظهاری |
| B02 اولین ارتباط Adult | حل‌شده در تست دوحسابی محلی | تأیید اولیه، کشف دوطرفه، Contact اختیاری و Direct canonical؛ Message Request یا consent subsystem جدید لازم نشد |
| B03 وضعیت self | حل‌شده در کد | GET /api/v1/me/messaging-status؛ capability hint خود caller، نه مجوز دائمی یا وضعیت دیگران |
| B08 Enum/OpenAPI | حل‌شده در کد/تست قرارداد | string/nullable/Flags بدون تغییر wire صحیح MVC/SignalR |
| B04 Peer Read Receipt | **CLOSED_IN_P1_CODE_AND_LOCAL_TESTS** | GET read-states و event اختصاصی Direct با current privacy/eligibility/history؛ cursorهای read/delivered خود کاربر همچنان خصوصی؛ Group/Channel per-member receipt ندارند؛ مرجع: Handoff P1 |
| B05 Push واقعی | P1 code/local transport tests؛ runtime pending | Web Push/VAPID و FCM HTTP v1، رجیستری و worker پایدار پیاده شده‌اند؛ credential/device واقعی ارائه نشده است. P1_PUSH_RUNTIME_PENDING؛ Sent/SignalR/provider acceptance به معنی دریافت دستگاه نیست |
| B06 Typing | P2 / خارج تحویل مصوب P1 جاری | command/event/TTL ساختگی وجود ندارد؛ به طراحی محدودیت/expiry مستقل نیاز دارد |
| B07 Presence / Last Seen | P2 / خارج تحویل مصوب P1 جاری | online/last-seen از اتصال، recent contact یا read cursor حدس زده نمی‌شود؛ privacy و multi-device/expiry مستقل لازم است |
| SignalR چند instance / backplane | ارتقای عملیاتی مستقل | P0 منبع حقیقت SQL/Delta و invalidation تک‌instance را دارد؛ ادعای scale-out/load انجام نمی‌شود |
| Topics / Poll پیشرفته | P2 | خارج basic Group؛ مدل/endpoint placeholder ساخته نشده |
| Scheduled messages / folders پیشرفته | P2 | نیازمند lifecycle و مجوز محصول مستقل؛ خارج P0 |
| Calls / Video / Stories | P3 | سرویس تماس/رسانهٔ زنده و حالت جعلی تولید نشده |
| Bots / Mini Apps / Premium / Monetization | P4 | خارج این تحویل و بدون API یا ادعای اجرایی |
| Secret Chat / E2EE / RAG | خارج P0 | هیچ ادعای رمزگذاری سرتاسری یا قابلیت AI تازه ساخته نشد |

این موارد معوق **blocker تکمیل P0 نیستند**. پذیرش/مهاجرت محیط Production، TLS، custody کلیدهای MFA و صحت providerهای واقعی همچنان عملیات مستقل خود را می‌خواهند؛ تست محلی به‌جای آن‌ها گزارش نمی‌شود. کد/تنظیم MeliPayamak و کار commit‌نشدهٔ AI این مرحله تغییر نکرده‌اند.

## مرز تحویل Messenger P1 — ۲۰۲۶-۱۰-۱۶

این مرحله با درخواست مستقل مالک اجرا شد: Peer Read محافظت‌شدهٔ Direct، Web Push/VAPID و FCM HTTP v1، رجیستری موجودِ گسترش‌یافته، recovery/reauthorization اعلان، اصلاح UTF-8/UTC، rate mutation و پذیرش دو session مستقل. [ممیزی و ۳۵ سناریوی محلی](../messaging/MESSENGER_P1_DELIVERY_AUDIT.md)، [Handoff تنظیم‌ها و API](../messaging/MESSENGER_P1_FRONTEND_HANDOFF.md) و [گزارش آموزشی/validation](../messaging/MESSENGER_P1_DEVELOPER_REPORT.md) شواهد دقیق‌اند. provider acceptance یا SignalR، دریافت دستگاه/read انسان نیست. REAL-PUSH-01 تا 04 به‌علت نبود credential و مرورگر/دستگاه عملیاتی **PENDING_ENVIRONMENT / P1_PUSH_RUNTIME_PENDING** باقی‌اند؛ تست محلی جای آن‌ها نمی‌نشیند.

موارد معوق صریح:

- P2: Typing، Presence/Last Seen، Topics، Poll پیشرفته، Scheduled Message، folder و Saved Messages پیشرفته. Saved پایه موجود با قابلیت پیشرفتهٔ پیاده‌نشده یکی نیست.
- P3: Calls، Video-call، Live و Stories؛ video attachment موجود ادعای تماس/پخش زنده نیست.
- P4: Bots، Mini Apps، Premium و Monetization.
- trackهای مستقل: SMS عمومی اعلان، AV/CDR، SignalR backplane و rate limiting توزیع‌شده؛ crypto پروتکل Push هم E2EE چت نیست. هیچ‌یک در P1 به‌طور ضمنی پیاده یا تأیید نشده‌اند.

سه migration فقط CHECK نوع event/failure و دو ستون nullable کلید Web subscription را تغییر می‌دهند؛ جدول/سامانهٔ notification موازی و کلید جعلی وجود ندارد. ارتقای SQL و retry/rollback در LocalDB ایزوله تست شدند، نه روی دیتابیس عادی یا سرور. appsettings، ملی‌پیامک، AI/RAG و dirtyهای worktree اصلی تغییر نکردند. گام بعدی فقط پذیرش عملیاتی transport موجود است؛ قابلیت‌های معوق به مجوز مستقل نیاز دارند.

## پیشرفت مستقل Messenger P2 — بخش گذرا

مجوز مستقل P2 و Master PRD v3.0 دریافت شده‌اند؛ وضعیت تاریخی P0/P1 بالا حذف نمی‌شود. B06/B07 اکنون پیاده‌سازی تک‌process، TTL/heartbeat، privacy مصوب Contacts، چندنشستی و بازبینی GAP-05/Block دارند؛ شواهد validation و commit در [ممیزی P2](../messaging/MESSENGER_P2_DELIVERY_AUDIT.md) ثبت می‌شوند. باقی قابلیت‌های P2 تا تکمیل فاز مربوطه ادعای تحویل ندارند.

- `DISTRIBUTED_EPHEMERAL_STATE=DEFERRED` و `MESSENGER_P2_DISTRIBUTED_PRESENCE=PENDING_SCALE_INFRA`: state/backplane مشترک وجود ندارد؛ restart حافظه را پاک می‌کند.
- Recording indicator هنوز ساخته نشده؛ افزودن فیلد نمایشی بدون semantics مجاز نیست.
- `P2_CHECKLIST_REQUIRES_PRODUCT_DECISION`: PRD نام Checklist را دارد، اما actor/item/ownership/update contract روشن ندارد؛ طبق استثنای prompt، بقیهٔ P2 ادامه دارند.
- `P1_PUSH_RUNTIME_PENDING` بدون تغییر؛ Web Push/FCM واقعی روی دستگاه ثابت نشده‌اند.

افزودهٔ مستقل P2-5/P2-6: label-set نسخه‌دار روی Saved موجود و پوشه‌های مالک‌محور با override/filter/pin پیاده شدند؛ final validation و commit در ممیزی P2 جدا ثبت می‌شوند. source-of-truth سازمان‌دهی REST است، نه event/Delta مصنوعی. این پیشرفت، دعوت پیشرفته/Topics/Poll/Scheduling یا کل P2 را بسته اعلام نمی‌کند.

افزودهٔ مستقل P2-7 تا P2-10: مدیریت مشخصات/محدودیت/ban، permissionهای مستقل، bearer invite و JoinRequest خصوصی پیاده و هر دو دور کامل ۱۳۹۰ تست موفق شدند. migration بررسی‌شده `20261016174429_AddConversationInvitesAndRestrictions` است؛ شواهد و Git در ممیزی P2 ثبت می‌شوند. Topics/Poll/Scheduling و Checklist هنوز از این افزوده نتیجه نمی‌شوند؛ deferredهای scale-out و real Push پابرجا هستند.

افزودهٔ مستقل P2-11: Topics حداقلی Group با ManageTopics، Open/Closed/reopen، پیام/Forward همان Group و history/Delta مجاز پیاده شده‌اند. ۴ Unit و ۱۳ Integration هدفمند موفق‌اند؛ نتیجهٔ suite کامل/Git در ممیزی P2 مرجع است. Topic-level pin/unread مستقل و Archived lifecycle ساخته نشده‌اند؛ پیام‌ها از read/pin موجود استفاده می‌کنند. Poll/Quiz تا پاسخ قرارداد aggregate-only/رأی/نمایش correct answer در حال تصمیم‌اند؛ Checklist طبق استثنای صریح prompt deferred و بقیهٔ کار ادامه دارند.

افزودهٔ مستقل P2-15 تا P2-17: Scheduled durable با SQL lease/retry/cancel و Silent مستقل از Mute پیاده شده‌اند. ویرایش موعد intent طبق optional بودن در prompt ساخته نشده؛ cancel/recreate با شناسهٔ تازه قرارداد واقعی است. round اول کامل، deadlock claim/cancel پیدا کرد و اصلاح ترتیب قفل در validation مجدد است؛ تا موفقیت suite کامل و Git، بسته‌شدن این فاز ادعا نمی‌شود. وضعیت Poll/Quiz، Checklist، P1_PUSH_RUNTIME_PENDING و distributed presence تغییری نکرده است.

Validation اصلاح‌شدهٔ این فاز تکمیل شد: هر دو suite کامل ۱۴۳۸ passed، صفر failed/skipped/warning/error و EF بدون تغییر معوق. Git/hash/تأیید remote در ممیزی P2 پس از اجرا ثبت می‌شود؛ این نتیجه هنوز معادل بسته‌شدن کل P2 یا Poll/Quiz نیست.

Git Scheduled/Silent با `4de7237233dcc8865672849f35e0cad06d6010b7` push/fetch و HEAD==origin/main تأیید شد. فاز Search و hardening، فیلترهای واقعی/batch identity، current Card visibility، revoke حین Safety، self restriction و coherence مجموعه را افزوده است؛ ۲۸ Integration هدفمند passed و suite کامل/Git فاز مستقل در ممیزی ثبت می‌شوند.

مالک قرارداد نهایی Poll/Quiz را Freeze کرد: Anonymous/NonAnonymous قابل انتخاب، هویت فقط برای اعضای مجاز در NonAnonymous، رأی Poll متغیر تا Close نهایی، Quiz یک پاسخ نهایی immutable/CorrectOption محافظت‌شده/Explanation اختیاری و بدون Exam/Grade. عبارت «در انتظار قرارداد aggregate-only» بالا تاریخی است و دیگر blocker تصمیم نیست؛ پیاده‌سازی مستقل هنوز باید با code/migration/tests اثبات شود. Checklist همچنان `P2_CHECKLIST_REQUIRES_PRODUCT_DECISION`؛ P1 real Push، distributed presence/backplane/rate limiting، AV/CDR، P3/P4 و AI/RAG هیچ ادعای تکمیل تازه‌ای ندارند.

Search/current-state hardening با `bf133ffbf5d9f4bfb2804035657e4c60cda27f5a` پس از دو suite کامل ۱۴۶۸ passed و EF بدون تغییر معوق، commit/push/fetch و remote verification موفق دارد. دو regression کلاس رسمی ابتدا شکست خوردند و با ClassMembers جاری اصلاح شدند؛ نتیجهٔ ۳۰ تست تازه و مرز workspace در ممیزی ثبت است.

Poll/Quiz اکنون code/migration واقعی `20261016211112_AddConversationPollsAndQuiz`، ۱۶ Unit و ۴۵ Integration تازه دارد؛ تمام ۱۷۰ Integration هدفمند P2 موفق شدند و suite کامل نهایی در جریان است. تصمیم Forward نیز نهایی شد: فقط متن مستقل پرسش/گزینه‌ها، بدون votes/correct/explanation و بدون رأی‌گیری زندهٔ مقصد. قرارداد قبلی «در انتظار تصمیم Poll/Quiz» دیگر blocker نیست. status کل P2 تنها بعد از full/final suite و commit/push/remote verification در ممیزی بسته می‌شود؛ این ثبت هدفمند جای Gate نهایی نیست.

تعویق‌های صریح باقی‌اند: Checklist (`P2_CHECKLIST_REQUIRES_PRODUCT_DECISION`)، Recording، ویرایش موعد Scheduled اختیاری به‌جای cancel/recreate، Scheduled Poll/auto-close/retract/Reopen Poll، unread/pin مستقل Topic، scale-out state/backplane/rate-limit (`PENDING_SCALE_INFRA`)، Web Push/FCM واقعی (`P1_PUSH_RUNTIME_PENDING`)، AV/CDR، Voice/Video/Group Call/Live/Screen Share، Stories، Bots/Mini Apps، Premium/Monetization، SMS عمومی، پرداخت و AI/RAG. هیچ‌کدام با تست محلی یا fake Push تکمیل محسوب نمی‌شوند.

Validation نهایی کد P2 انجام شد: دو suite کامل اصلاح‌شده۱۵۳۱ passed، صفر failed/skipped/warning/error، EF بدون تغییر معوق؛ ۱۷۲ Integration P2 و۴۵ Unit تازه نسبت به baseline. نقص CHECK با SQL UNKNOWN ابتدا با regression ناموفق واقعی اثبات و سپس با IS NOT NULL صریح اصلاح شد. شمار نهایی فاز Poll/Quiz۱۶ Unit و۴۷ Integration تازه است؛ نتیجهٔ Git و بسته‌شدن تحویل فقط پس از commit/push/fetch در ممیزی ثبت می‌شود. هیچ تغییری برای deferredهای بالا یا دریافت واقعی Push ادعا نمی‌شود.

بسته‌شدن کد P2: `9e35bb3ba53ee93cfa555a024792ce50fcef6c86` با پیام `feat(messaging): add polls quizzes and p2 acceptance` commit/push/fetch و HEAD==origin/main موفق دارد؛ worktree پس از آن تمیز بود. وضعیت `MESSENGER_P2_CODE_COMPLETE` است. تنها ادامهٔ همین تحویل، ثبت نهایی شواهد مستندات است و source/config/schema عوض نمی‌شود. Checklist=DEFERRED_PRODUCT_DECISION، P1 real Push=PENDING_ENVIRONMENT و distributed presence=PENDING_SCALE_INFRA باقی‌اند؛ این‌ها مجوز اجرای خودکار فاز بعد یا ادعای runtime نیستند. جزئیات Git، شش migration، همهٔ Gateها و مسیر T01–T50 در ممیزی P2 آمده‌اند.

## رفع تصمیم Checklist v1 — درخواست مستقل ۲۰۲۶-۱۰-۱۷

قرارداد «P2 CHECKLIST CLOSURE» از مالک دریافت و APPROVED شده است؛ `P2_CHECKLIST_REQUIRES_PRODUCT_DECISION` و Checklist=DEFERRED در بندهای تاریخی بالا دیگر تصمیم باز جاری نیستند. Personal به‌ازای User خصوصی، Collaborative مشترک، completion مشتق، rowversion/transaction و history-safe structural edit، Forward صرفاً متن مستقل و بدون state، مجوز جاری/GAP-05 و moderation پرونده‌محور مبنا هستند. Mode و معنای ItemId منتشرشده ثابت، جایگزینی با soft-retire + ID تازه است؛ retirement وضعیت قبلی را پاک نمی‌کند و شرط آن به مشاهدهٔ state دیگران وابسته نیست.

کد، سه جدول و migration واقعی `20261017033804_AddConversationChecklists`، ۱۴ Unit و ۵۰ Integration تازه اکنون وجود دارند؛ دور هدفمند همهٔ ۲۲۲ Integration P2 موفق شده است. نتیجهٔ کامل/نهایی، commit/push و remote verification در [ممیزی P2](../messaging/MESSENGER_P2_DELIVERY_AUDIT.md) پس از اجرا ثبت می‌شوند؛ تا آن gateها، صرف approval به معنی تحویل نهایی نیست. این درخواست مستقل فقط Checklist را می‌بندد؛ توسعهٔ خودکار حوزه‌های دیگر مجاز نیست.

`MESSENGER_P1_REAL_PUSH=PENDING_ENVIRONMENT`، `P1_PUSH_RUNTIME_PENDING` و `MESSENGER_P2_DISTRIBUTED_PRESENCE=PENDING_SCALE_INFRA` بدون تغییرند. Recording، AV/CDR، scale-out/backplane/rate-limit، دریافت واقعی Web Push/FCM، P3/P4/Calls، frontend، AI/RAG و پرداخت از این کار نتیجهٔ تازه نمی‌گیرند. سرور یا تنظیم MeliPayamak و appsettings تغییر نکرده است؛ محدودیت پنجرهٔ ویرایش، عنوان-only Search و نبود API عمومی تاریخچهٔ آیتم‌های retired در Handoff صریح‌اند.

پذیرش نهایی محلی Checklist تکمیل شد: دو suite کامل **558 Unit + 1023 Integration + 14 Architecture = 1595 passed**، صفر failed/skipped/warning/error، EF بدون تغییر معوق. بنابراین `MESSENGER_P2_CHECKLIST=PASS` و این تصمیم محصول دیگر gap باز نیست. وضعیت Commit/Push/remote واقعی و حکم نهایی تحویل در گزارش پایانی ثبت می‌شود؛ کد بدون آن گیت Git تحویل نهایی محسوب نمی‌شود. pending محیط Push و زیرساخت Presence دقیقاً همان مقادیر بالا باقی‌اند.

## P3-0 — ممیزی معماری Voice / Video / Group Call / Screen Share / Live

تاریخ `2026-10-17`؛ مبنای committed/remote برابر `5e1da895c96065380e12d04341e483d4e78539c4` و worktree مستقل Messenger در شروع تمیز بود. P0/P2 کد کامل دارند؛ P1 همچنان `MESSENGER_P1_CODE_COMPLETE / P1_PUSH_RUNTIME_PENDING` است. baseline همین ممیزی دوباره اجرا شد: restore/build موفق، صفر warning/error، **558 Unit + 1023 Integration + 14 Architecture = 1595 passed**، failed/skipped=0. این شواهد تماس واقعی یا استقرار را اثبات نمی‌کنند.

چهار خروجی documentation-only: [معماری رسانه و شواهد کد](../messaging/MESSENGER_P3_MEDIA_ARCHITECTURE.md)، [تصمیم‌های لازم مالک](../messaging/MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md)، [نقشهٔ اجرا و dependency](../messaging/MESSENGER_P3_IMPLEMENTATION_ROADMAP.md)، [ماتریس آزمون آینده](../messaging/MESSENGER_P3_TEST_ACCEPTANCE_MATRIX.md). هیچ Domain/Controller/API/Migration/SDK/TURN config/deployment تازه‌ای ایجاد نشد. تاریخی P0/P1/P2 بازنویسی نشد؛ MeliPayamak، AI/RAG، Payment، appsettings و frontend خارج تغییرند.

| موضوع | وضعیت جاری و مرز |
|---|---|
| control plane قابل reuse | Identity/UserSession، current authorization، session-targeted SignalR، SQL/outbox، Block/Privacy/Contacts/Group permissions و GAP-05؛ هیچ‌کدام call runtime نیستند |
| P2P/SFU/platform | پیشنهاد R01–R03: SFU حتی 1:1، primary LiveKit self-hosted و fallback Janus+Coturn؛ **AWAITING_OWNER_APPROVAL** و بدون نصب/خرید |
| revoke رسانه / R17 | gate فنی مهم: self-hosted LiveKit حذف participant را معادل باطل‌شدن token نمی‌داند؛ TTL/refresh و rejoin نیازمند admission غیرقابل bypass و eviction/reconciliation اثبات‌شده؛ primary تا آن آزمون GO نیست |
| call privacy / سن / consent | R04–R07/R15 هنوز تصمیم خاص live می‌خواهند؛ Unknown مانند Minor، stranger deny و منع guardian/manager monitoring مصوب قبلی حفظ‌اند؛ Safety متن مجوز audio/video زنده نمی‌دهد |
| group / Live / share | R08–R10/R14 برای permission، cap فنی، Host/Viewer، first valid accept و share source؛ همه فقط طرح آینده، نه API یا permission موجود |
| history / timeout / reconnect / abuse / DND | R11–R13/R16/R18/R19 پیشنهاد pilot قابل‌تصویب‌اند؛ اعداد، retention، release scope و production SLO خودکار freeze نشده‌اند |
| استقلال Call/Message | R20 پیشنهاد شروع 1:1 با target مجاز و Conversation اختیاری است؛ مجوز تماس نه Send/Read پیام می‌سازد و نه Contact/Direct خودکار؛ Group/Live conversation-bound می‌مانند |
| recording / E2EE | DEFERRED؛ بدون ضبط، transcript، observer پنهان یا ادعای E2EE |
| operational gates | HTTPS/WSS/CA، Linux media و authenticated TURN/SFU، public IP/UDP/TCP/TLS، bandwidth، monitor و دستگاه/شبکهٔ واقعی برای مرحلهٔ اجرایی لازم‌اند؛ اکنون provision نشده‌اند |
| P1 و چندگرهی | `P1_PUSH_RUNTIME_PENDING` و `MESSENGER_P2_DISTRIBUTED_PRESENCE=PENDING_SCALE_INFRA` بدون تغییر؛ APNs VoIP/CallKit incoming-call با FCM معمولی یکی نیست |
| Stories / Bots / Mini Apps | track مستقل بعد از call foundation؛ طبق PRD v3 target گستردهٔ P3 هستند؛ دسته‌بندی P4 تاریخی بالا جای این target را نمی‌گیرد؛ Business/Premium/Monetization جدا و هیچ implementation تازه‌ای ندارند |

وضعیت خروجی audit پس از validation و commit/push/remote verification: `P3_MEDIA_ARCHITECTURE_READY_FOR_OWNER_DECISIONS`؛ **نه تماس پیاده‌شده و نه مجوز اجرای خودکار P3-A..F**. جدول سه‌دستهٔ APPROVED_FROM_EXISTING_POLICY / RECOMMENDED_FOR_OWNER_APPROVAL / DEFERRED در سند تصمیم‌ها مرجع نهایی این ممیزی است؛ تصمیم باز فقط زیرمرحلهٔ وابسته را متوقف می‌کند. نتیجهٔ واقعی Git و validation نهایی در گزارش پایان اعلام می‌شود، نه self-hash اختراعی در فایل همان commit.

## تأیید مالک P3 Media Policy v1 — ۲۰۲۶-۱۰-۱۷

مرجع جاری: [سیاست مصوب](../messaging/MESSENGER_P3_APPROVED_MEDIA_POLICY_V1.md) با وضعیت **APPROVED_FOR_IMPLEMENTATION** و [نگاشت دقیق approval](../messaging/MESSENGER_P3_PRODUCT_DECISIONS_REQUIRED.md#p3-policy-v1-owner-approval). ردیف‌های AWAITING و اعداد P3-0 بالا شواهد تاریخی‌اند و در حدود تصمیم صریح مالک supersede شده‌اند؛ تاریخچه حذف نشده است. LiveKit Self-hosted primary، Janus VideoRoom+Coturn fallback مشروط، Windows/.NET control و Linux media اختصاصی، SFU همهٔ مسیرها و TURN authenticated مصوب‌اند؛ هنوز هیچ media runtime نصب یا پذیرفته نشده است.

| موضوع | وضعیت جاری پس از ثبت policy |
|---|---|
| token/rejoin | initial TTL≤30s و scope Call/Room/User/DeviceSession/role؛ removal تنها revoke نیست؛ REAL gate در **P3-S** اجباری و اکنون NOT_TESTED؛ شکست اثبات‌شده = LIVEKIT_REJOIN_SECURITY_GATE_FAILED، نه تضعیف auth |
| privacy/Child Safety | Adult Contacts؛ protected public Nobody؛ Parent/Child با GuardianRelation فعال و restrictions جاری؛ Teacher↔Minor و Minor↔Minor private deny؛ کلاس/گروه با scope آموزشی جاری؛ بدون monitoring/private browsing و بدون تغییر GAP-05 |
| permission/limit | ManageCalls برای Group؛ ManageLive مستقل برای Live و promotion صریح/server-authorized/auditable؛ share1؛ pilot Video8/Voice16 active/configurable؛ عدد تجاری Live قبل load ممنوع |
| ring/grace/history | ring30s؛ media و signaling grace15s فقط شبکه، security no-grace؛ history امن Messenger در آینده؛ technical metadata30days/configurable نه legal retention یا پاک‌کردن پیام |
| scope جاری | فقط ثبت policy و [برنامهٔ اجرایی S](../messaging/MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_PLAN.md)؛ tooling/SDK/Domain/endpoint/migration/config/deployment جدید ایجاد نشده؛ MeliPayamak و AI/RAG/dirty workspace خارج تغییر |
| dependency اجرا | S→A→B→C→D→E→F؛ بعد Stories سپس Bots/Mini Apps؛ full A/B/D/E پیش از PASS واقعی S آغاز نشوند؛ plan فعلی به معنی شروع S نیست |
| موارد باقی | recording/transcription/STT archive همچنان deferred/ممنوع v1؛ P1_PUSH_RUNTIME_PENDING و PENDING_SCALE_INFRA تغییر نکرده؛ rate/busy/setup/DND جزئیات freeze‌نشده طبق mapping فقط قرارداد وابستهٔ آینده را منتظر می‌گذارند |

وضعیت این ثبت `P3_MEDIA_POLICY_V1_APPROVED / GO_FOR_P3_MEDIA_SECURITY_SPIKE` است، نه تماس CODE_COMPLETE یا REAL PASS. `P3_S_EXECUTION=NOT_STARTED`، `LIVEKIT_REJOIN_SECURITY_GATE=NOT_TESTED` و `P3_PRODUCTION_MEDIA_APPROVAL=NOT_GRANTED` صریح‌اند. نتایج regression/Git فقط پس از اجرای واقعی در گزارش پایان ثبت می‌شوند.

## P3-S — نیاز زیرساخت در S1، ۲۰۲۶-۱۰-۱۷

درخواست اجرای spike از `3cfe196dc0de1bbb0c36653834fde544f1d1ed4e` آغاز شد؛ وضعیت فعلی **P3_S_INFRA_REQUIRED** است، نه gate امنیتی شکست‌خورده. [گزارش و فهرست دقیق provisioning](../messaging/MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_REPORT.md) شواهد read-only را دارد: Docker CLI27.4.0 بدون Linux daemon، WSL بدون distro/kernel آماده و نبود Linux lab مجازِ معرفی/احرازشده با IP/DNS/TLS/TURN/firewall و دو client. مالک هم نبود محیط آماده را صریحاً تأیید کرد. S2–S23 اجرا نشده‌اند؛ PS00=PENDING_ENVIRONMENT و PS01–PS10=NOT_RUN. S0 و final restore/build/full tests هر دو موفق بودند:1595 passed در هر دور، صفر failed/skipped/warning/error؛ هیچ تست تازهٔ رسانه یا deployment از آن نتیجه نمی‌شود.

موارد لازم برای ادامه: host غیرProduction با Docker قابل‌دسترسی، دسترسی مجاز، endpoint عمومی و TLS، طرح محدود پورت‌ها، authenticated TURN UDP+TCP/TLS، دو client synthetic و شرایط fault شبکه، authority/callback آزمایشی و secret/evidence custody. صرف نصب CLI یا رفع WSL این gate کامل را پاس نمی‌کند. version/image/digest هنوز انتخاب/مستقر نشده‌اند؛ هیچ پورت/سرور/firewall تغییر نکرد و ابزار/SDK/Call endpoint/migration محصول ساخته نشد. policy و شواهد P3-0 حفظ شدند؛ AI dirty workspace، MeliPayamak و frontend خارج تغییرند.

مجوز محصول همچنان APPROVED_FOR_IMPLEMENTATION است ولی `LIVEKIT_REJOIN_SECURITY_GATE=NOT_TESTED` و Production media approval=NOT_GRANTED؛ P3-A/B/D/E پیش از PASS واقعی S آغاز نشوند. `P1_PUSH_RUNTIME_PENDING` و PENDING_SCALE_INFRA بدون تغییرند. گام بعد فقط تأمین زیرساخت و تکرار S1 است؛ window واقعی، FAIL امنیتی یا fallback اجراشده از مستندات vendor استنتاج نمی‌شود.
