# گزارش اصلاح تاریخچهٔ Messenger — MKN-BE-001

مبنای ثبت‌شده `b76e960f67824e90d93597a9685ed147b0d4a8a1`، شاخه
`feature/product-v6-backend-alignment`، بازبینی `2026-11-17`.
Root cause اثبات‌شده: `BACKEND_CONTEXT_AUTHORIZATION_BUG`.
Source اعتبارسنجی و Push‌شده: `718451ff3eba1831bd6d18c1e9693713a415d817`.
نتیجهٔ پذیرش Backend: `MKN_BE_001=PASS` و `BE12_ACCOUNT_MESSAGING=PASS_CURRENT_POLICY`.

## ۱. خلاصهٔ مرحله و بازتولید

نشانهٔ گزارش‌شده «History چت‌ها را نداریم» بود. persistence از قبل وجود داشت؛ Conversation،
Participant، Message، Sequence و Outbox مدل‌های فعلی‌اند. تفاوت میان دو List نیز از قبل وجود داشت:
account inbox مستقل از Workspace و legacy list وابسته به scope.

پیش از اصلاح، تست WorkspaceRoleSwitchDoesNotHideAuthorizedAccountHistory روی Source مبنا
عملاً شکست خورد: پس از Send موفق و مشاهدهٔ گفتگو در account inbox، History گفتگوی رسمی
سازمان الف در Workspace سازمان ب Expected=200 / Actual=404 بود. log/TRX همان اجرای ناموفق محفوظ است.
هیچ API، کوئری یا استقرار Frontend برای حدس‌زدن منشأ نشانه تغییر داده نشد.

اکنون History و Delta خواندنی همان Resolver مجوز Inbox را reuse می‌کنند. پیام در SQL جدید
خوانده می‌شود؛ تعلق محتوا به نشست یا حافظهٔ SignalR ایجاد نشده است.

## ۲. معماری

Domain و Api تغییر نمی‌کنند. Application روی IAccountInboxStore متد ResolveReadContextAsync
دارد؛ AccountInboxService نشست را پیش و پس از Resolve بازبینی می‌کند. Infrastructure همان
AccountInboxAccessQuery.ResolveReadContextAsync را اجرا می‌کند. این مسیر قبلاً در Replay حساب
استفاده می‌شد؛ ACL موازی یا Resolver حدسی تازه ساخته نشده است.

## ۳. فایل‌های مهم ایجادشده

| فایل | مسئولیت / نکتهٔ آموزشی |
|---|---|
| tests/MakanApp.IntegrationTests/MessengerHistoryAcceptanceTests.cs | Send/SQL، نشست تازه، دستگاه دوم،75 پیام، Reconnect و revoke؛ HTTP success را با persistence تازه بسنجید |
| tests/MakanApp.IntegrationTests/MessengerHistoryVisibilityTests.cs | دو Organization، چهار نوع Personal، مرز عضویت، حذف شخصی و tombstone؛ visibility باید میان List و History سازگار بماند |
| tests/MakanApp.IntegrationTests/MessengerHistoryPersonalClassTests.cs | فضای رسمی کلاس مستقل با School Workspace نامرتبط؛ کلاس مستقل، سازمان فرضی نیست |
| MESSENGER_HISTORY_FRONTEND_HANDOFF_FA.md | مسیر Home، paging، Cursor و merge واقعی؛ Cache منبع حقیقت نیست |

## ۴. فایل‌های مهم تغییرکرده

AccountInboxService.cs: port/read context و بازبینی نشست؛ context در حافظهٔ عملیات است و نشست را
بازنویسی نمی‌کند. MessagingService.cs فقط در GetConversationMessagesAsync و
GetConversationChangesAsync از این مسیر استفاده می‌کند. Send/Edit/Delete/Management همان
ResolveConversationContextAsync قبلی را دارند. EfMessagingStore.Reads.cs فقط delegation به
Resolver SQL موجود را اضافه می‌کند؛ query پیام‌ها و ترتیب آن بازنویسی نشده‌اند.

AccountInboxSummaryTests.cs چهار Unit برای مرز نشست/مجوز دارد. ConversationSummaryWorkspaceTests.cs
انتظار تاریخی ردشدن History را طبق مجوز صریح این Task اصلاح می‌کند؛ هم‌زمان ردشدن Send،
جزئیات scoped، دانلود فایل و Hub در Workspace نامرتبط را همچنان اثبات می‌کند.
PersonalAccountVisibilityEndpointsTests.cs نیز خواندن سازمان ب با Membership واقعی را از ارسال
در Workspace سازمان الف جدا می‌سنجد؛ پس از پایان همان Membership، خواندن دوباره404 است.
MessagingP0RealtimeAuthorizationTests.cs منع مخاطب Realtime را نگه می‌دارد و برای Role/Organization
لغوشده، پاسخ History را طبق مخفی‌سازی حساب404 می‌سنجد. سه case قدیمی در Regression نخست
به همین انتظارات قبلی برخوردند؛ هیچ assertion امنیتی حذف یا تستی disable نشده است.
README و Gap Matrix مرجع جاری را اضافه می‌کنند؛ تاریخچهٔ وضعیت‌های قبلی حذف نمی‌شود.

## ۵. جریان اجرا

HTTP GET → ConversationsController → MessagingService.GetConversationMessagesAsync →
AccountInboxService.ResolveReadContextAsync → MessagingOperationContextResolver.PersonalAsync
برای اعتبار نشست → IAccountInboxStore → EfMessagingStore → AccountInboxAccessQuery → SQL Server.
سپس GetConversationMessagesAsync در Store، Participant، scope، منبع رسمی و visibility را
دوباره می‌سنجد و ConversationMessagePageResult را برمی‌گرداند. GET changes همین context
خواندن را می‌گیرد؛ نوشتن همچنان با context عملیاتی قبلی اجرا می‌شود.

## ۶. دیتابیس و شواهد

جدول/ستون/Index/Migration جدیدی نیست. Messages مرجع اصلی است؛ هیچ ChatHistory کپی ساخته نشد.
قید یکتای UX_Messages_Conversation_Sequence ترتیب ماندگار و
UX_Messages_Conversation_Sender_ClientMessageId retry ارسال را پوشش می‌دهند.
History query روی ConversationId/Sequence با Take محدود است؛ NextBeforeSequence با وجود
پیام قدیمی‌تر مجاز محاسبه می‌شود. Resolve چند query محدود دارد و full-table materialization
یا query جدا به‌ازای هر پیام اضافه نمی‌کند. Index تازه بدون evidence عملکرد ساخته نشده است.

پذیرش SQL در scope تازه Conversation/Participant/Message، Sender/Sequence/SentAt را بررسی می‌کند.
آرشیو/Block/Clear-history/پایان عضویت به حذف عمومی ردیف‌ها تبدیل نشده‌اند. تست‌ها فقط LocalDB
ایزوله دارند؛ MakanApp، MakanApp_Test و دیتابیس سرور بررسی یا repair نشده‌اند.
DATA_REPAIR_REQUIRED=NO برای bug بازتولیدشدهٔ query/context؛ ادعای ممیزی همهٔ دادهٔ Production نیست.

## ۷. Endpointها و Contract

Endpoint یا DTO تازه وجود ندارد. GET account همان items/nextCursor/hasMore است؛ GET messages
همان beforeSequence/limit و messages/nextBeforeSequence است؛ GET changes همان afterCursor است.
تغییر semantics خواندن: Workspace منتخب، مجوز خواندنِ مستقل و فعلی حساب را پنهان نمی‌کند.
404 منبع ناموجود/غیرمجاز،401 نشست نامعتبر و400 ورودی/Cursor نامعتبر حفظ شده‌اند.
جزئیات route، ورودی، پاسخ، Visibility و خطا در [Handoff](MESSENGER_HISTORY_FRONTEND_HANDOFF_FA.md).

## ۸. مفاهیم فنی

context انتخابی UI با ACL منبع یکی نیست. Cursor نشست‌محور می‌تواند امن بماند، در حالی که
محتوا account-based است. History روی Sequence keyset دارد؛ timestamp authority ترتیب نیست.
account inbox replay فقط invalidation است و محتوا از REST/Delta خوانده می‌شود.

## ۹. تصمیم‌های طراحی

راه‌های بررسی‌شده: تغییر Workspace کاربر، بازکردن همهٔ عملیات سازمانی، یا استخراج context فقط
برای خواندن. راه سوم انتخاب شد؛ Resolver موجود SQL reuse و scope تغییر محدود به دو read شد.
هزینهٔ چند query کوتاه در برابر تفکیک درست account/context پذیرفته شد. مجوز جاری منبع،
پروفایل/نشست و Participant حفظ می‌شوند؛ دانستن شناسه یا داشتن Cursor کافی نیست.

## ۱۰. تست‌ها و نگاشت پذیرش

| معیار | شواهد واقعی |
|---|---|
| BE01 discovery/history | ExistingConversationAppearsInAccountInboxAndHistory، SQL در scope تازه |
| BE02 refresh/login | ConfirmedMessageSurvivesFreshLoginSession با Logout و OTP همان حساب |
| BE03 دستگاه دوم | SecondDeviceReceivesSameAuthorizedHistory؛ Cursor نشست اول رد می‌شود |
| BE04 paging | HistoryPaginationHasNoMissingOrDuplicateMessages؛75 پیام،10 ارسال همزمان،4 صفحه20تایی |
| BE05 reconnect | ReconnectReconcilesDurableHistoryWithoutDuplicate؛ Hub واقعی TestHost، replay و REST/Delta |
| BE06 انواع | Direct/Group/Channel و Official Group/Channel، به‌علاوه کلاس مستقل |
| BE07 زمینه و revoke | WorkspaceRoleSwitch و دو Organization؛ Membership/Role/Participant/TeacherAssignment/Enrollment/Organization revoke |
| BE08 حالت خالی/visibility | 200 خالی در برابر400/401/404؛ FromJoin، for-me، clear و tombstone |

چهار Unit جدید: نشست غیرفعال قبل lookup، مجوز جاری ناموجود404، context خواندنی واقعی بدون
SelectWorkspace و revoke نشست حین lookup. Integration از Host/EF/SQL واقعی ایزوله استفاده می‌کند؛
SMS در تست با provider کنترل‌شده است. Architecture موجود اجرا می‌شود؛ تغییر Domain/Api نداریم.

## ۱۱. Validation

| مرحله | نتیجهٔ واقعی |
|---|---|
| Clean / Restore | PASS؛ هر دو ExitCode=0 |
| Release Build اصلاح‌شده | PASS؛ صفر Warning/Error |
| Unit متمرکز Context/Summary | 14 موفق؛ شامل4 case جدید |
| پذیرش متمرکز اولیه | 22 موفق؛ پیش از افزوده‌شدن case تکمیلی حساب خارجی |
| Regression گستردهٔ نهایی Messaging | 224 Unit +681 Integration موفق؛ صفر Failed/Skipped؛ Architecture در این فیلتر انتخاب نمی‌شود |
| Full اول، پیش از تقویت assertionهای SQL هر نوع | 1369 Unit +2068 Integration +14 Architecture =3451 موفق؛ صفر Failed/Skipped |
| Zibal / Realtime در Full اول | 117 Zibal موفق؛ صفر Realtime timeout/failure و صفر Lookup failure |
| بررسی تکمیلی مستقیم SQL | 7 case موفق؛ Direct/Group/Channel، رسمی سازمان/کلاس و PersonalClass؛ Sender/Sequence/SentAt و Participantهای فعال در scope تازه |
| Final Build | PASS؛ صفر Warning/Error |
| Final Tests پس از تکمیل SQL | 1369 Unit +2068 Integration +14 Architecture =3451 اجرا و موفق؛ صفر Failed/Skipped |
| Zibal / Realtime نهایی | هر117 Zibal موفق؛ صفر Realtime timeout/failure و صفر Lookup failure |
| EF model | بدون تغییر معلق؛ Migration جدید صفر |
| OpenAPI | 438 Path،494 Operation،783 Schema؛ Hash قرارداد بدون تغییر |

OpenAPI SHA-256: `616e22cf15bc67b604ea12f3348c4612d0289ebaf413eb4d83635c65770cf4f0`.
تعدادها فقط همان دور کامل‌اند؛ focused و full با هم جمع نشده‌اند.26 case جدید شامل4 Unit و22
Integration به baseline3425 افزوده شده‌اند. چهار case قدیمی در سه method طبق قرارداد جدید
بازبینی شده‌اند؛ هیچ‌کدام حذف یا disable نشده‌اند.

اجرای ناموفقِ بازتولید قبل اصلاح حفظ شده است. خطای آماده‌سازی تست جدید در Build نیز پنهان
نشد: MessageDatabaseRecord.Id با MessageId DTO اشتباه شده بود و assertion تک‌عضوی به
Assert.Single اصلاح شد. تست جدید Reconnect ابتدا به‌اشتباه انتظار محتوای پیام از account-inbox
invalidation داشت؛ با EventId واقعی SQL و REST/Delta اصلاح شد، نه با تغییر قرارداد Replay.
خطای دورهای قبلی با موفقیت نهایی جمع یا از شواهد حذف نمی‌شود.
پس از Full اول، بررسی مستقیم SQL برای هر نوع گفتگو صریح‌تر شد؛ کد runtime تغییر نکرد.
دور نهاییِ در حال اجرا برای همین تکمیل تست متوقف و NOT_COMPLETED ثبت شد؛ آن را Full موفق
گزارش نمی‌کنیم. هر7 case تکمیلی SQL موفق شد و Final Build/Test در final-r2 به Source تکمیل‌شده مربوط است.
نتیجهٔ final-r2 با ExitCode=0 پایان یافت؛ سه TRX با parser streaming خوانده و SHA-256 آن‌ها
ثبت شد. هر10 فایل runtime/test با manifest ورودیِ Build نهایی برابر ماندند.
ناپایداری تاریخی BE-15 شامل6 Realtime timeout و1 Lookup failure است؛ نتیجهٔ سبز دور تازه
به‌تنهایی علت تاریخی را اثبات نمی‌کند. مقدار مشاهده‌شدهٔ واقعی در نتایج نهایی ثبت می‌شود.

## ۱۲. Git و حفاظت

Commit کد و تست: `718451ff3eba1831bd6d18c1e9693713a415d817` با پیام
`fix(messaging): restore persistent account conversation history`؛10 فایل شامل3 runtime و7 تست.
Stage با manifest Source نهایی و diff کامل تطبیق داده شد. Push این Commit موفق و پس از Fetch،
HEAD و Remote همین SHA بودند. هیچ تنظیمات خصوصی، Secret یا فایل ناتمام AI وارد Commit نشد.
Remote=origin در `https://github.com/RastinaxAI/MakanApp.git` و
Upstream=`origin/feature/product-v6-backend-alignment` است.
چهار فایل مستندات در Commit مستقل با پیام
`docs(messaging): hand off messenger history contract and evidence` تحویل می‌شوند؛
شناسهٔ نهایی مستندات و تأیید Remote در گزارش نهایی Git اعلام می‌شود.
Merge به main و Deployment انجام نشده‌اند.

Baseline سه فایل AI Untracked داشت؛ همان‌ها خارج Commit می‌مانند.12 فایل تنظیمات خصوصی،
user config، ملی‌پیامک و AI همراه SHA-256 و backup خصوصی محافظت شده‌اند.
شواهد در `.artifacts/mkn-be001-history-20261117-b76e960`، Git-ignored و با ACL خصوصی نگه‌داری می‌شوند.

## ۱۳. محدودیت‌ها

Frontend/مرورگر واقعی یا سرور Deploy نشده‌اند؛ ساختار State UI فعلی بررسی نشده است. bug Backend
اثبات شد و برای حل آن تغییر اجباری DTO/route یا کد Frontend لازم نیست؛ مصرف قرارداد باید
با Handoff تطبیق داده شود. مجوز فایل، Hub، نوشتن و GET جزئیات scoped مستقل باقی‌اند.
BE18 متوقف‌شده، AI، Commerce، Safety، سیاست‌های Product باز و BE24 در این Task تغییر نمی‌کنند.

## ۱۴. نکات آموزشی

1. ابتدا persistence و query را جدا آزمایش کنید؛ 404 الزاماً به معنی پاک‌شدن داده نیست.
2. List و بازکردن History باید از یک مرجع ACL جاری استفاده کنند.
3. اصلاح read دلیل بازکردن write یا دانلود نیست.
4. Session-bound Cursor را برای multi-device عمومی نکنید؛ دستگاه تازه از Snapshot شروع می‌کند.
5. صفحه‌های صعودیِ History از بازه‌های نزولی می‌آیند؛ ترتیب merge را با Sequence بسازید.
6. invalidation پیام نیست؛ assertion تست باید با Contract واقعی هماهنگ باشد.

## ۱۵. مسیر مطالعه

ConversationsController → MessagingService.GetConversationMessagesAsync/GetConversationChangesAsync →
AccountInboxService → IAccountInboxStore → EfMessagingStore.Reads → AccountInboxAccessQuery →
OfficialSpaceAccessQuery → MessageConfiguration → سه فایل MessengerHistory → AccountInboxSummaryTests.

## ۱۶. مرحلهٔ بعد

تطبیق مستقل مصرف Home/Summary/History در Frontend طبق Handoff و سپس Final BE01–BE25 Audit.
Store Billing، P3، Deploy و تغییر policyهای Safety به این اصلاح وارد نمی‌شوند.
