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
