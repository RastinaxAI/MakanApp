# ممیزی و تحویل P0 پیام‌رسان ماکان

## مبنای مستقل و مرز کار

تاریخ شروع: ۲۰۲۶-۱۰-۱۶. worktree مستقل `MakanApp-Backend-Messenger-P0`، شاخه `messenger/p0` با upstream برابر `origin/main` و commit مبنا `ab0090108e6fcd26b6160307219d331c5c8c23cf` است. ۲۲ فایل dirty workspace قبلی AI پیش از شروع hash شدند؛ reset/stash/clean/revert یا انتقال تغییرات AI انجام نمی‌شود.

منابع محصول: Master PRD فارسی نسخه ۳.۰ و `Makan-Messenger-Backend-Handoff.pdf` مورخ ۲۰۲۶-۱۰-۱۶ از اسناد موجود مالک؛ قراردادهای versioned مخزن `MESSENGER_V2_PRODUCT_DECISIONS.md` و `GAP_05_MESSENGER_CHILD_SAFETY.md`. PRD رفتار هدف است، نه اثبات implementation. تصمیم‌های مصوب GAP-05 بر پیشنهادهای قدیمی‌ترِ حل‌نشده مقدم‌اند. هیچ frontend یا فایل مخزن AI وارد این worktree نشده است.

مالک در پاسخ جاری صریحاً پذیرش را به **تست محلی** محدود کرده است: هیچ deploy، mutation یا تست سرور انجام نمی‌شود و local Integration، REAL SERVER PASS نام‌گذاری نمی‌شود.

## P0-0 — ماتریس ممیزی پیش از تغییر کد

این جدول baseline را توصیف می‌کند؛ نتیجهٔ validation و تحویل فازها در بخش‌های بعد با شاهد واقعی افزوده می‌شود.

| حوزه | وضعیت مبنا | شاهد فعلی و gap واقعی |
|---|---|---|
| تأیید اولیه سن حساب شخصی | MISSING | `CommunicationAgeController` فقط School verify و Platform conflict resolve دارد؛ مسیر Unverified بدون مدرسه وجود ندارد |
| وضعیت پیام‌رسانی خود کاربر | MISSING | `ProfileController` وضعیت verification/action را برنمی‌گرداند؛ endpoint self-only معادل وجود ندارد |
| Contacts | IMPLEMENTED | `MessagingDirectoryService`، `EfMessagingDirectoryStore`، owner/target unique، rowversion، CRUD/recent و تست‌های Contact موجود |
| Discovery | PARTIAL | search/resolve/profile و rate limit موجود؛ مسیر شروع حساب شخصی به تأیید سن وابسته و phone خروجی Username باید با P0 تطبیق شود |
| Privacy | IMPLEMENTED | `MessagingPrivacyPolicy`/resolver؛ Everyone/Contacts/Nobody از دید مقصد؛ phone visibility و discovery مستقل |
| Block | IMPLEMENTED | `EfMessagingStore.Safety` و final send؛ history/عضویت Group مستقل؛ تست‌های block و race موجود |
| Canonical Direct | IMPLEMENTED | `DirectUserPair`، unique index زوج/scope، Serializable و UPDLOCK/HOLDLOCK، تست رقابت SQL Server |
| B02 آغاز ارتباط اول | PARTIAL | policy Adult/Verified به grant یا school نیاز ندارد؛ باید از تأیید اولیه واقعی تا discovery/contact/direct آزمایش شود |
| پیام پایدار و idempotency | IMPLEMENTED | `EfMessagingStore.Advanced.Send`؛ ClientMessageId، unique sequence، پیام/change/outbox اتمیک؛ تست‌های retry/concurrency موجود |
| SignalR و گیرندگان جاری | PARTIAL | session-group invalidation و subscription authorization موجود؛ تطابق کامل session/account/scope جاری با REST نیازمند regression است |
| Delta و reconnect | IMPLEMENTED | `MessagingChangeCursor` و `EfMessagingStore.Sync`؛ cursor conversation-bound و private audience، تست paging/recovery موجود |
| Group و lifecycle عضو | IMPLEMENTED | storeهای Management، Owner/Admin/Member، add/remove/leave/transfer، permission و history policy؛ coverage موجود |
| Channel پایه | IMPLEMENTED | PublishMessages مستقل و منع subscriber؛ send/read/media/member lifecycle موجود |
| state گفتگو / Saved | IMPLEMENTED | state خصوصی archive/mute/pin/clear و Saved یکتا؛ public broadcast برای state خصوصی مجاز نیست |
| فایل و رسانه | IMPLEMENTED | FileAsset آماده، MessageAttachment، download authorization جاری؛ تست‌های revocation و visibility موجود؛ redesign لازم نیست |
| Child Safety | IMPLEMENTED | سن مؤثر از GAP-05، رابطه/رضایت، دعوت، گروه رسمی، Internal safety fail-closed و regressionهای موجود |
| OpenAPI / JSON enum | PARTIAL | HTTP/Hub از JsonStringEnumConverter استفاده می‌کنند؛ `AddOpenApi()` فاقد هم‌ترازی اختصاصی schema/flags است؛ B08 باید با تست واقعی ثابت/رفع شود |
| پذیرش end-to-end دو حساب شخصی | MISSING | تست‌های fixture موجود عمدتاً سن را مستقیم seed می‌کنند؛ زنجیرهٔ تأیید مجاز اولیه، discovery و تبادل/بازیابی باید جدا اثبات شود |

## برنامهٔ تغییرهای محدود

Validation مبنای همین worktree: restore و build موفق با صفر warning/error؛ Unit=404، Integration=698، Architecture=14؛ مجموع **1116 passed، صفر failed، صفر skipped**. شاهد TRX در `.artifacts/messenger-p0/tests/baseline` است. شمار ۱۱۳۴ تلاش AI به این baseline committed تعلق ندارد؛ تغییرهای commit‌نشدهٔ آن عمداً منتقل نشده‌اند.

۱. reuse مدل و evidence سن GAP-05، افزودن initial verification مدیریتی با UsersManage، MFA/Step-up، audit و نسخه؛ حفظ resolve فعلی و عدم ساخت مدرسه/حساب جعلی.
۲. endpoint وضعیت self با actionهای مشتق از policy جاری، نه مجوز دائمی یا دادهٔ کاربر دیگر.
۳. regression و اصلاح فقط gapهای واقعی discovery/contact/direct؛ عدم ساخت consent subsystem تازه برای Adultهای مجاز.
۴. regression و اصلاح گیرندگان realtime/session، retry و reconnect؛ نگهداری SQL به‌عنوان منبع حقیقت.
۵. Group/Channel، فایل، Child Safety و cross-scope؛ اصلاح فقط defect اثبات‌شده، نه بازنویسی.
۶. هم‌ترازی OpenAPI و JSON، پذیرش محلی T01–T23 در scope P0، handoff و deferred register، validation و commit/pushهای منطقی جداگانه.

Push واقعی هر commit فقط پس از موفقیت Build/Test و بازبینی staged diff انجام می‌شود؛ push عادی `HEAD:main` از شاخهٔ مستقل و fetch/برابری HEAD با origin/main، بدون force یا جابه‌جایی شاخهٔ main در workspace AI است.

## فاز اول — P0-1 و P0-2

تأیید اولیهٔ شخصی و وضعیت self پیاده شدند. `CommunicationAgeController → CommunicationAgeService → ICommunicationAgeStore → EfCommunicationAgeStore → CommunicationAgeVerification / SQL Server / AdminAuditLog` مسیر واقعی است. Permission، MFA و fresh Step-up موجود reuse می‌شوند؛ initial و resolve متفاوت‌اند. نسخه جاری و UPDLOCK/HOLDLOCK در transaction سریال‌پذیر از تأیید هم‌زمان محافظت می‌کنند. هیچ DOB در DTO عمومی یا audit summary قرار نمی‌گیرد.

Migration `20261016050425_AddPersonalCommunicationAgeVerification` فقط CHECK منبع شاهد موجود را برای `PlatformVerification=3` و OrganizationId خالی توسعه می‌دهد. جدول، ستون، PK/FK یا SQL default تازه ایجاد نمی‌شود. Down در صورت وجود شاهد source=3 نمی‌تواند بدون تصمیم داده‌ای به constraint قدیمی برگردد؛ حذف شاهد یا rollback کور پیشنهاد نمی‌شود. migration در SQL Server LocalDB ایزوله تست شد، نه developer/Production. EF pending-model check موفق و تغییر ناخواستهٔ مدل وجود ندارد.

تست‌ها: ۹ Unit در `PersonalMessagingStatusTests` و ۶ Integration در `PlatformMfaPersonalMessagingEndpointsTests`؛ Adult/Minor، self-only/revoked session، نبود school side effect، TOTP واقعی/freshness، initial روی Verified/Conflict، نسخه کهنه، audit و رقابت دو تأیید را پوشش می‌دهند. ابتدا ۵ case واقعاً به علت نبود route با 404 شکست خوردند. یک فرض نادرست تست درباره داشتن JSON در denial عادی permission اصلاح شد؛ رفتار عمومی permission پروژه تغییر نکرد.

Validation کامل پس از اصلاح: restore/build موفق، صفر warning/error؛ Unit=413، Integration=704، Architecture=14، مجموع **1131 passed، صفر failed، صفر skipped**. شواهد TRX در `.artifacts/messenger-p0/tests/eligibility-full` است. این فقط نتیجهٔ فاز اول است؛ P0 کامل و پذیرش دوحسابی هنوز ادعا نمی‌شود.

نکات آموزشی این فاز: OTP مالکیت شماره است نه اثبات سن؛ state machine اولیه و حل تعارض دو عملیات‌اند؛ rowversion با idempotency یکی نیست؛ check و append شاهد باید اتمیک باشند؛ UI capability hint جای authorization مقصد را نمی‌گیرد. مسیر مطالعه: Controller → service/status policy → مدل شاهد → store → EF configuration/migration → Unit و Integration نام‌برده.

فاز اول در `ed1ea0b7c6319ccf18d3e4fd0a18780870b87066` با پیام `fix(messaging): complete personal messaging eligibility` ثبت شد. Build و همهٔ ۱۱۳۱ تست پس از commit نیز موفق شدند؛ push عادی و fetch، برابری HEAD با origin/main را تأیید کردند.

## فاز دوم — Discovery / Contacts / Direct

`PlatformMfaPersonalDirectoryEndpointsTests` پنج case پذیرش واقعی HTTP/SQL محلی اضافه می‌کند. دو Adult با OTP آزمایشی، تکمیل پروفایل، تأیید اولیهٔ مجاز و TOTP واقعی وارد می‌شوند؛ discovery دوطرفه، Contact یک‌طرفه، add تکراری، rename/favorite/reload، stale version، حذف بدون حذف history و شش درخواست هم‌زمان A↔B روی یک Direct بررسی می‌شوند. هیچ Membership یا PersonalCommunicationGrant برای این زنجیره ساخته نمی‌شود؛ بنابراین **B02 به Message Request یا مدل رضایت جدید نیاز ندارد**.

چهار case ابتدا شکست واقعی داشتند: خروج شمارهٔ عمومی از Username discovery، کشف Adult نامرتبط توسط دو حالت Minor/Unknown و تبلیغ قابلیت ساخت Direct برای actor محدودشده. اصلاح‌ها محدود به mapping خروجی Application و query/transaction موجود Infrastructure هستند. Username search/resolve مستقل از تنظیم visibility شماره، `phoneNumber=null` می‌دهد؛ resolve دقیق شماره و profile/contact مستقل همچنان privacy مصوب مقصد را رعایت می‌کنند. discovery عمومی فقط میان دو Verified Adult است؛ مسیر رابطه/رضایت موجود برای حساب محافظت‌شده حذف نشده است. محدودیت جاری actor در ساخت Direct و hint همان capability اعمال می‌شود؛ history قبلی حذف نمی‌شود. Controller، قرارداد ورودی و schema دیتابیس تغییر ندارند و migration جدیدی لازم نیست.

نکات آموزشی: سیاست output redaction با اجازهٔ discovery یکی نیست؛ eligibility باید هر دو سوی ارتباط را بسنجد؛ Contact جای Membership نیست؛ canonical pair باید SQL invariant داشته باشد؛ hint خروجی مجوز دائمی محسوب نمی‌شود. مسیر مطالعه: `PeopleController` → `MessagingDirectoryService` → `EfMessagingDirectoryStore` → `MessengerRelationshipQuery` → `EfMessagingStore` → تست پذیرش دوحسابی فوق.

اجرای کامل، فرض شکننده‌ای در تست قدیمی `OrganizationAndUserReadModelsAreGlobalPaginatedAndMaskPhone` را آشکار کرد: داده‌های suite مشترک با ساعت جلوتر می‌توانند target را از ۱۰۰ حساب صفحهٔ اول خارج کنند. فقط خود تست اصلاح شد تا صفحه‌ها را تا TotalCount بخواند و یکتایی نتایج و masking همان target را همچنان بررسی کند؛ رفتار Backoffice برای سبزکردن تست تغییر نکرد.

پس از اصلاح، Build با صفر warning/error و اجرای کامل با Unit=413، Integration=709، Architecture=14، مجموع **1136 passed، صفر failed/skipped** موفق شدند؛ TRX در `.artifacts/messenger-p0/tests/directory-fixed` است. hash هر ۲۲ فایل dirty workspace اصلی با baseline یکسان باقی ماند.

فاز دوم در `3c39d8bb10be330250529c0ad9222d1af15fd0a5` ثبت شد. Build/Test کامل پس از commit نیز ۱۱۳۶ موفق داشت؛ push عادی و fetch برابری HEAD با origin/main را تأیید کردند.

## فاز سوم — تحویل پایدار، Realtime و Retry

هفت case جدید محلی در `MessagingP0RealtimeAuthorizationTests` و `PlatformMfaPersonalRealtimeEndpointsTests` اضافه شدند. قبل از اصلاح، پنج case واقعاً شکست خوردند: حساب معلق، انقضای مطلق نشست، پایان نقش انتخاب‌شده، سازمان معلق و نبود Retry-After در 429. revocation موجود و زنجیرهٔ دو حساب شخصی از قبل pass شدند؛ برای رفتار سالم subsystem موازی ساخته نشد.

`MessagingRealtimeAudienceResolver` ابتدا candidateهای معتبر SQL را با حساب فعال و هر دو انقضای نشست فیلتر می‌کند، سپس همان `MessagingService.AuthorizeRealtimeSubscriptionAsync` متعلق به REST/Hub را reuse می‌کند. scope تازهٔ authorization برای هر event مانع ماندگاری UserSession tracked از event قبلی می‌شود. denial دامنه‌ای یک نشست، تحویل بقیه را متوقف نمی‌کند؛ SQL/خطای فنی/cancellation بلعیده نمی‌شوند و مسیر retry موجود Outbox باقی است. هزینهٔ فعلی چند query برای هر نشست است؛ این انتخاب صحت P0 است، نه ادعای load/scale یا fan-out چند instance. مجوز در لحظهٔ تحویل ارزیابی می‌شود؛ رویداد قبلاً تحویل‌شده قابل پس‌گرفتن نیست.

در پذیرش دوحسابی، receiver پیش از وجود Direct وصل است و بدون Subscribe قبلی اولین MessageCreated را می‌گیرد؛ پاسخ موفق POST عمداً پس از ثبت در سرور دور انداخته و خطای شبکه شبیه‌سازی می‌شود. retry ترتیبی/هم‌زمان همان ClientMessageId فقط یک Message، یک Change و یک اثر اعلان دارد؛ payload متفاوت 409 است. reply و sequence، قطع اتصال، تخلیهٔ outbox در زمان قطع، reconnect و بازیابی/تکرار امن Delta و history بررسی می‌شوند. TOTP و age verification واقعی‌اند؛ SMS آزمایشی است و TestServer/LocalDB **شاهد سرور مستقر نیستند**.

هدر Retry-After از metadata واقعی limiter برای discovery/search محاسبه و در CORS همراه X-Correlation-ID قابل خواندن شد؛ Origin/credential policy گسترش نیافت و OTP/AI rate limit تغییر نکرد. تست 429 از یک limiter کوچک injectشده فقط روی people استفاده می‌کند تا قرارداد handler/header را اثبات کند، نه عدد quota محیط Production. migration جدید لازم نیست.

Validation این فاز: focused=10 موفق؛ restore و Build با صفر warning/error؛ Unit=413، Integration=716، Architecture=14، مجموع **1143 passed، صفر failed/skipped**. TRX کامل در `.artifacts/messenger-p0/tests/realtime-full` قرار دارد. schema یا package تغییری نکرد.

فاز سوم در `b99b0e5c4ebaae06ff736b01130c20386b47c629` ثبت شد؛ Build/Test پس از commit نیز ۱۱۴۳ موفق، push عادی و fetch/remote verification موفق بودند.

## فاز چهارم — Group/Channel، مرز تاریخچه و رویداد خصوصی

پنج case جدید در `MessagingP0GroupChannelAcceptanceTests` اضافه شدند. حذف عضو Group و Channel از راه API، دانلود فایل قبل از حذف، منع history/media/download/Delta/search/edit/delete/send/Hub پس از حذف و ادامهٔ دسترسی مالک بررسی می‌شود. ۳۱ گفتگوی هم‌زمان‌نما با timestamp برابر در چند صفحه بدون حذف/تکرار خوانده می‌شوند؛ refetch پس از فعالیت جدید ترتیب را اصلاح می‌کند. این شاهد snapshot اتمیک چند صفحه در زمان mutation هم‌زمان نیست؛ قرارداد موجود offset-based باقی است.

دو تست ابتدا شکست واقعی داشتند: بازپخش MessageCreated پیش از مرز FromJoin به عضو جدید می‌رسید؛ و با حذف عمدی audience hint از رویداد خصوصی ReadCursor، notifier به طرف دیگر نیز می‌فرستاد. Search/History/Media/File/Delta خودشان مرز را رعایت می‌کردند. اصلاح حداقلی، policy جدید نیست: port داخلی `IMessagingRealtimeAudienceResolver` اکنون notification کامل را دریافت می‌کند و `SignalRMessagingRealtimeNotifier` آن را عبور می‌دهد. resolver در scope تازه با همان `MessagingService.GetConversationChangesAsync` و limit=1، visibility دقیق cursor/type/resource را می‌سنجد. بنابراین همان قواعد FromJoin، Clear، DeleteForMe و audience خصوصی Delta برای Realtime هم مرجع‌اند؛ SQL rule موازی کپی نشده است. cursor خراب fail-closed و خطای فنی است.

تست بازپخش هم مسیر SignalR واقعی محلی و هم مجموعهٔ قطعی sessionهای مجاز SQL را بررسی می‌کند؛ نبود callback به‌تنهایی شاهد عدم نشت نیست. private read cursor همچنان self-only است؛ این اصلاح **B04 Peer Read Receipt را پیاده نمی‌کند**. Endpoint، JSON wire، permission، lifecycle، storage infrastructure و schema دیتابیس تغییر ندارند. برای Channel همچنان قرارداد فعلی Sender برقرار است و posting ناشناس یا هویت تازهٔ Channel ساخته نشده است. Owner/Admin-Publish/Subscriber و قابلیت SendMedia از policy و تست‌های موجود reuse می‌شوند.

نکات آموزشی: مجوز container با مجوز آیتم یکی نیست؛ audience hint جای authorization رویداد را نمی‌گیرد؛ replay باید visibility فعلی را بسنجد؛ صفحه‌بندی پایدار به tie-breaker نیاز دارد؛ حذف Contact/عضو/پیام سه lifecycle جدا هستند. مسیر مطالعه: notifier → audience resolver → `MessagingService.GetConversationChangesAsync` → `EfMessagingStore.Sync` → file bound-access resolver → تست‌های P0 و Group/Channel موجود.

Validation کامل فاز چهارم: focused=11 موفق؛ restore/build موفق با صفر warning/error؛ Unit=413، Integration=721، Architecture=14، مجموع **1148 passed، صفر failed/skipped**. شاهد TRX در `.artifacts/messenger-p0/tests/group-full` است. schema و package عوض نشدند و migration تازه لازم نبود.
