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
