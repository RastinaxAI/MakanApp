# گزارش آموزشی توسعه‌دهنده — Messenger P0

تاریخ ۲۰۲۶-۱۰-۱۶. این گزارش فقط کد موجود و آزمون‌های محلی همین تحویل را توضیح می‌دهد. [ماتریس پذیرش و validation](MESSENGER_P0_DELIVERY_AUDIT.md)، [قرارداد مصرف](MESSENGER_P0_FRONTEND_HANDOFF.md). نتیجهٔ Git نهایی پس از commit/push در گزارش تحویل اعلام می‌شود؛ hash فایلِ داخل همان commit نمی‌تواند به خود آن commit اشاره کند.

## ۱. خلاصه مرحله

قبل از این کار، بیشتر Messenger وجود داشت، اما User شخصی بدون مدرسه مسیر امن تأیید اولیهٔ سن نداشت؛ UI وضعیت self نمی‌گرفت؛ چند شرط discovery/realtime با سیاست مصوب یکسان نبودند و OpenAPI Enum عددی نادرست داشت. اکنون مسیر تأیید مجاز و audited، self status، ارتباط نخست دو Adult، redaction شماره، منع discovery عمومی caller محافظت‌شده و مجوز دقیق هر event در کنار قرارداد اصلاح‌شده وجود دارند. subsystem سالمِ Contacts/Direct/Group/Channel/Storage بازنویسی نشد.

## ۲. معماری

Domain فقط source جدیدِ شاهد را می‌شناسد و قواعد سن موجود را نگه می‌دارد. Application مالک DTO، وضعیت self، use case و port است. Infrastructure صاحب SQL/EF، transaction، evidence/audit و انتخاب گیرندهٔ Realtime است. Api فقط transport، Controller، header/CORS و metadata OpenAPI را دارد. Domain به EF/HTTP وابسته نشد؛ Application به Infrastructure وابسته نشد؛ MediatR/CQRS/Generic Repository اضافه نشد.

وابستگی ایستا: Api → Application و Infrastructure؛ Infrastructure → Application/Domain؛ Application → Domain. فراخوانی runtime از port برمی‌گردد، نه اینکه Application implementation فنی را بشناسد.

## ۳. فایل‌های مهم ایجادشده

مسیرها نسبت به ریشهٔ worktree مستقل Messenger هستند.

| فایل | مسئولیت و دلیل ایجاد | نکتهٔ آموزشی |
|---|---|---|
| `MakanApp.Application/Messaging/MessagingStatus.cs` | snapshot/result/action و policy راهنمای self | capability hint مجوز دائمی نیست؛ Unknown/Conflict نباید Adult فرض شوند |
| `MakanApp.Api/OpenApi/StringEnumSchemaTransformer.cs` | تکمیل type:string مفقود در schema مطابق MVC واقعی | عیب schema را با تغییر wire صحیح یا upgrade بی‌دلیل حل نکنید |
| `MakanApp.Infrastructure/Persistence/Migrations/20261016050425_AddPersonalCommunicationAgeVerification.cs` | توسعهٔ CHECK منبع شاهد، نه مدل سن جدید | migration باید کمینه و inspectشده باشد؛ Down با دادهٔ جدید لزوماً امن نیست |
| فایل Designer همین migration | snapshot طراحی تولیدشدهٔ EF | تغییرات مکانیکی بزرگ را با مدل قبلی مقایسه کنید؛ هزاران خط generated الزاماً feature بزرگ نیست |
| `tests/MakanApp.UnitTests/Messaging/PersonalMessagingStatusTests.cs` | ۹ تست pure policy | Unit را از SQL و HTTP مستقل نگه دارید |
| `tests/MakanApp.IntegrationTests/PlatformMfaPersonalMessagingEndpointsTests.cs` | ۶ case initial/self/MFA/audit/concurrency | OTP آزمایشی با TOTP واقعی و تأیید مجاز دو مفهوم‌اند |
| `tests/MakanApp.IntegrationTests/PlatformMfaPersonalDirectoryEndpointsTests.cs` | ۵ case discovery/contact/canonical Direct بدون مدرسه | جریان واقعی از شروع حساب، نقص seed مستقیمِ سن را در acceptance جبران می‌کند |
| `tests/MakanApp.IntegrationTests/MessagingP0RealtimeAuthorizationTests.cs` | ۵ case حساب/نشست/role/org جاری | منطق گیرندهٔ realtime هم به reauthorization نیاز دارد |
| `tests/MakanApp.IntegrationTests/PlatformMfaPersonalRealtimeEndpointsTests.cs` | دو case زنجیرهٔ دوحسابی و 429 | پاسخ گم‌شده پس از commit و header limiter را واقعی شبیه‌سازی کنید |
| `tests/MakanApp.IntegrationTests/MessagingP0GroupChannelAcceptanceTests.cs` | ۵ case حذف عضو، فایل/history/realtime/private event و چند صفحه | نبود callback به‌تنهایی شاهد امنیت نیست؛ receiverهای SQL را هم بسنجید |
| `tests/MakanApp.IntegrationTests/MessagingOpenApiContractTests.cs` | ۵ case enum/nullable/Flags/unknown permission | schema، serializer و نتیجهٔ permission باید با هم تست شوند |
| `docs/messaging/MESSENGER_P0_FRONTEND_HANDOFF.md` | قرارداد مصرف واقعی | API خیالی یا Peer Read ساخته‌نشده را مستند نکنید |
| `docs/messaging/MESSENGER_P0_DELIVERY_AUDIT.md` | baseline، defectهای red، فازها و T01–T23 | شاهد محلی را REAL SERVER PASS ننامید |

## ۴. فایل‌های مهم تغییرکرده

| فایل | تغییر، دلیل و اثر |
|---|---|
| `MakanApp.Domain/Identity/CommunicationAgeVerification.cs` | `PlatformVerification=3` کنار sourceهای قبلی؛ evidence جدید در همان مدل است |
| `MakanApp.Application/Messaging/CommunicationAgeService.cs` | initial/platform read/self؛ validation/use-case، نه EF یا Controller logic |
| `MakanApp.Api/Controllers/CommunicationAgeController.cs` | سه route جدید با mapping نازک و policy UsersManage برای مسیر Platform |
| `MakanApp.Infrastructure/Messaging/EfCommunicationAgeStore.cs` | MFA/Step-up جاری، قفل User، initial-only، audit/evidence اتمیک و query self |
| `MakanApp.Infrastructure/Persistence/Configurations/Identity/CommunicationAgeVerificationConfiguration.cs` | CHECK source=3 با OrganizationId خالی |
| `MakanApp.Infrastructure/Persistence/Migrations/MakanDbContextModelSnapshot.cs` | همان تغییر CHECK؛ مدل pending باقی نماند |
| `MakanApp.Application/Messaging/MessagingDirectoryService.cs` | خروج شماره در Username search/resolve حذف شد؛ privacy profile/phone مستقل ماند |
| `MakanApp.Infrastructure/Messaging/EfMessagingDirectoryStore.cs` | discovery عمومی دوطرفهٔ Verified Adult و hint محدودیت ارسال جاری |
| `MakanApp.Infrastructure/Messaging/EfMessagingStore.cs` | همان guard محدودیت actor در transaction ساخت Direct |
| `MakanApp.Application/Messaging/MessagingAbstractions.cs` | port داخلی audience، notification کامل را می‌گیرد تا مجوز آیتم گم نشود |
| `MakanApp.Infrastructure/Messaging/MessagingRealtimeAudienceResolver.cs` | حساب/نشست فعال و Delta دقیق در scope تازه؛ نه policy موازی کپی‌شده |
| `MakanApp.Api/Realtime/SignalRMessagingRealtimeNotifier.cs` | عبور notification کامل به resolver؛ فقط sessionهای مجاز تحویل می‌گیرند |
| `MakanApp.Api/Program.cs` | Retry-After/CORS exposure و ثبت JSON/OpenAPI هماهنگ؛ quota OTP/AI، Origin و credentials عوض نشد |
| `tests/MakanApp.IntegrationTests/PlatformOperationsEndpointsTests.cs` | تست قدیمی به‌جای فرض صفحهٔ اول، paging کامل می‌کند؛ رفتار Backoffice تغییر نکرد |
| راهنمای API، frontend-contract README، ساختار DB، تصمیم‌های Messenger و register معوق | افزودهٔ P0 و مرز baseline/موارد آینده؛ HTML/PDF قدیمی تولیدنشده را جاری معرفی نمی‌کنند |

## ۵. جریان اجرای قابلیت

تأیید اولیه:

```text
HTTP POST
→ CommunicationAgeController.VerifyInitial
→ CommunicationAgeService.VerifyInitialAsync / validation
→ CommunicationAgeVerification.Classify
→ ICommunicationAgeStore
→ EfCommunicationAgeStore.VerifyCoreAsync
→ UsersManage + GAP-04 MFA/fresh Step-up
→ SQL Server Serializable + UPDLOCK/HOLDLOCK + rowversion
→ User classification + CommunicationAgeVerification + AdminAuditLog
→ commit → CommunicationAgeResult → HTTP 200
```

ارسال/دریافت:

```text
ConversationsController.SendMessage
→ MessagingService → current permission / Content Safety
→ EfMessagingStore → Message + revision/change/outbox → SQL commit
→ MessageReceiptResult → HTTP 200
→ MessagingRealtimeOutboxDispatcher / MessagingRealtimeOutboxProcessor
→ SignalRMessagingRealtimeNotifier → MessagingRealtimeAudienceResolver
→ MessagingService.GetConversationChangesAsync (exact event visibility)
→ authorized session groups → ConversationChanged
→ client fetches/processes REST Delta → persists nextCursor
```

## ۶. دیتابیس

جدول تازه نداریم. `identity.CommunicationAgeVerifications` از قبل PK/FK و شاهد append-only داشت؛ CHECK `CK_CommunicationAgeVerifications_Source` اکنون source=1 با organization غیرخالی و source=2/3 با organization خالی را می‌پذیرد. ستون/PK/FK/index/SQL default/backfill تازه ایجاد نشد. `Users.RowVersion` نسخهٔ تغییر سن و transaction کوتاه Serializable از race محافظت می‌کند؛ evidence و audit در همان commit هستند. DOB پاسخ عمومی یا audit summary نیست.

در Direct/Send از unique pair، unique ClientMessageId/sequence و transaction موجود reuse شد. network dispatch بیرون transaction است. migration جدید در fixture SQL Server LocalDB ایزوله از ابتدا اجرا شد؛ developer DB معمولی و Production migrate نشدند. Down با source=3 نیازمند تصمیم داده‌ای است؛ حذف شاهد راه rollback خودکار نیست.

## ۷. Endpointها

سه افزودهٔ واقعی: GET `/api/admin/v1/users/{userId}/communication-age`؛ POST `/api/admin/v1/users/{userId}/communication-age/verifications`؛ GET خود کاربر `/api/v1/me/messaging-status`. routeهای مدیریتی UsersManage + MFA + Step-up تازه، request تأیید `verifiedBirthDate/expectedVersion/reason`، response classification/status/version دارند. initial روی Verified/Conflict=409، نسخهٔ کهنه=412، ورودی نامعتبر=400، عدم مجوز=403 است. self به نشست معتبر محدود و فاقد selector کاربر دیگر است.

همهٔ endpointهای مهم Contacts/Discovery/Privacy/Direct/Messages/Group/Channel/Files/Hub، request/response، concurrency، authorization و خطاهای جاری در [Handoff](MESSENGER_P0_FRONTEND_HANDOFF.md) آمده‌اند. permission عادی middleware ممکن است body خالی داشته باشد؛ کلاینت نباید وجود code را در هر 403 فرض کند.

## ۸. مفاهیم فنی استفاده‌شده

شاهد append-only با policy سن؛ optimistic concurrency با rowversion؛ Serializable و update lock برای invariant حساس؛ canonical relational key؛ idempotency با شناسهٔ پایدار payload-bound؛ transactional outbox و تحویل حداقل یک‌بار؛ event-level authorization؛ Delta opaque cursor؛ CORS exposed header؛ schema transformer مطابق serializer. این مرحله CQRS/MediatR، پیام‌رسان E2EE، AI moderation یا Push واقعی ندارد.

## ۹. تصمیم‌های طراحی

- مدل سن موازی/اعتماد به OTP انتخاب نشد؛ source و workflow موجود توسعه یافت تا policy کودک تضعیف نشود. trade-off: تأیید اپراتور هنوز عملیات انسانیِ معتبر لازم دارد.
- برای B02 سیستم Message Request تازه لازم نشد؛ پس از حل initial verification، Adultهای مجاز از policy موجود عبور کردند.
- به‌جای duplicate کردن query امنیتی، Realtime همان Delta با limit=1 را reuse کرد؛ trade-off، queryهای بیشتر به‌ازای session است و load/scale-out ادعا نمی‌شود.
- retry را دقیقاً به همان ClientMessageId و payload متصل نگه داشتیم؛ commit شدن DB با دریافت HTTP response یکی نیست.
- JSON صحیح به عدد تبدیل نشد و framework ارتقا نیافت؛ global HTTP options و transformer کوچک، metadata موجود را اصلاح کردند.
- کد در worktree تمیز مستقل نوشته شد؛ trade-off این است که workspace اصلی AI عمداً به branch جدید جابه‌جا/به‌روز نشده است.

## ۱۰. تست‌ها

۹ Unit تازه فقط policy self را می‌سنجند. ۲۸ Integration تازه از HTTP، SQL Server/constraint، TOTP واقعی، concurrency، SignalR LongPolling، retry و schema استفاده می‌کنند. ۱۴ Architecture موجود همچنان جهت Onion و نبود dependency فنی در لایهٔ داخلی را enforce می‌کنند؛ Architecture تازه یا تست صرفاً شمارشی ایجاد نشد.

از جمله defectهای red پیش از fix: routeهای اولیهٔ غایب، شماره در Username، discovery caller محافظت‌شده، hint/ساخت Direct actor محدود، گیرندهٔ realtime با حساب/role/org نامعتبر، Retry-After غایب، event پیش از FromJoin، hint خصوصی گم‌شده و schema عددی/type غایب. فرض‌های نادرست تست دربارهٔ body عادی 403، صفحهٔ اول عمومی و status bit ناشناخته اصلاح شدند، نه اینکه رفتار امن برنامه برای تست تغییر کند.

## ۱۱. Validation

baseline همین worktree: Unit=404، Integration=698، Architecture=14؛ مجموع 1116 موفق. اجرای اول کامل نهایی: restore/build موفق، صفر warning/error؛ Unit=413، Integration=726، Architecture=14؛ **1153 executed/passed، صفر failed/skipped**. خروجی TRX و تکرارهای بعدی در audit ثبت می‌شوند؛ تعداد اجرای AI workspace دیگر به این baseline نسبت داده نشده است.

تکرار کامل نهایی نیز همین **۱۱۵۳ موفق و صفر failed/skipped** داشت؛ Build دوباره صفر warning/error بود. ۳۷ full route Handoff با OpenAPI واقعی تطبیق داده شد و هیچ مسیر ناشناخته نداشت.

`dotnet ef migrations has-pending-model-changes --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --context MakanDbContext --configuration Debug --no-build` موفق بود. startup Api به EF Design خصوصی دسترسی نداشت؛ فرمان مطابق روش موجود مخزن اصلاح شد، package اضافه نشد. snapshot قرارداد بدون SQL/provider/worker، 287 operation، 259 path، 491 schema، 88 جدول و 38 migration داد.

## ۱۲. Git

مبنا `ab0090108e6fcd26b6160307219d331c5c8c23cf`، worktree مستقل `D:\Project\MakanApp\MakanApp-Backend-Messenger-P0`، branch `messenger/p0`، remote `origin`، upstream `origin/main` است. push عادی HEAD:main انجام می‌شود؛ main workspace اصلی با dirty AI دست‌کاری نمی‌شود. چهار commit زیر پس از Build/Test پیش و پس از commit، push و remote verification موفق شدند:

| hash | پیام |
|---|---|
| `ed1ea0b7c6319ccf18d3e4fd0a18780870b87066` | `fix(messaging): complete personal messaging eligibility` |
| `3c39d8bb10be330250529c0ad9222d1af15fd0a5` | `fix(messaging): stabilize contacts discovery and direct creation` |
| `b99b0e5c4ebaae06ff736b01130c20386b47c629` | `fix(messaging): harden durable realtime message flow` |
| `3b5569bbdd0010b646b5f7fb9a357e552c28b9f3` | `fix(messaging): enforce group history and private realtime visibility` |

فاز آخر همین فایل، OpenAPI، تست قرارداد و اسناد مرتبط را شامل می‌شود؛ hash نهایی و تطابق remote در گزارش تحویل پس از انجام واقعی اعلام می‌شوند. ۲۲ فایل dirty قبلی اصلی از آغاز hash شده‌اند و هیچ فایل AI/SMS/appsettings/bin/obj در commitهای P0 وارد نشده است. force-push، rewrite، stash/reset/clean انجام نشد.

## ۱۳. محدودیت‌های فعلی

تست‌ها محلی‌اند، نه deployed-server evidence؛ SMS provider واقعی در این پذیرش تماس نگرفته است. provider/config MeliPayamak و AI/RAG تغییر نکردند. B04 Peer Read، B05 Push، B06 Typing، B07 Presence/Last Seen، backplane و قابلیت‌های پیشرفته در register معوق‌اند، نه blocker P0. Handoff جدید جای بازتولید ادعاییِ HTML/PDF قدیمی را گرفته است. رفتار UI/draft واقعی frontend در این workspace آزموده نمی‌شود.

## ۱۴. نکات آموزشی

1. اثبات مالکیت شماره با اثبات سن یا مجوز resource یکی نیست.
2. UI hint، Contact یک‌طرفه و شناسهٔ GUID هیچ‌کدام مجوز دائمی نیستند.
3. authorization container برای یک event/private item کافی نیست؛ همان visibility منبع حقیقت را reuse کنید.
4. response گم‌شده می‌تواند پشت خود commit موفق داشته باشد؛ retry با شناسهٔ تازه اثر تکراری می‌سازد.
5. outbox، تحویل دقیقاً یک‌بار SignalR نمی‌دهد؛ Delta، checkpoint پس از پردازش و deduplication لازم‌اند.
6. metadata را در برابر serializer واقعی تست کنید؛ Flags ترکیبی و nullable به تست مستقل نیاز دارند.
7. تست shared-database نباید فرض کند دادهٔ خودش حتماً در صفحهٔ اول است؛ state و clock fixture روی ordering اثر دارند.

## ۱۵. مسیر پیشنهادی مطالعه

`CommunicationAgeController` → `CommunicationAgeService`/`MessagingStatusPolicy` → `CommunicationAgeVerification` → `ICommunicationAgeStore` → `EfCommunicationAgeStore` → EF configuration/migration → تست‌های MFA شخصی؛ سپس `MessagingDirectoryService`/`EfMessagingDirectoryStore` → Direct/Send موجود → `MessagingRealtimeOutboxProcessor` → notifier/audience resolver → `EfMessagingStore.Sync` → تست دوحسابی و Group/Channel؛ در پایان `StringEnumSchemaTransformer` و تست قرارداد OpenAPI را بخوانید.

## ۱۶. مرحلهٔ بعد

این مرحله پس از Git gate متوقف می‌شود. گام پیشنهادی مستقل، مصرف Handoff توسط client در workspace مجاز خودش و انتخاب P1 مشخص، مثلاً B04 با قرارداد privacy/read-state مصوب، است؛ Push/Presence/RAG یا کار سرور را نباید بی‌اجازه داخل ادامهٔ P0 مخلوط کرد.
