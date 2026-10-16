# گزارش آموزشی توسعه‌دهنده — Messenger P2

## فاز اول: P2-0 تا P2-4

### خلاصه و معماری

قبلاً Typing/Presence وجود نداشت؛ اکنون hint گذرا با TTL، چندنشستی و دو audience مستقل وجود دارد. Domain فقط قواعد تنظیم حریم خصوصی را نگه می‌دارد. `MessagingEphemeralService` در Application هماهنگ‌کنندهٔ مجوز جاری و انتشار است؛ پورت‌های آن به SQL یا SignalR وابسته نیستند. `InMemoryMessagingEphemeralState`، `EfMessagingStore.Ephemeral.cs` و `MessagingEphemeralDispatcher` در Infrastructure قرار دارند چون حافظهٔ process، query و worker جزئیات اجرا هستند. Hub، notifier مبتنی بر SignalR و Controller در Api هستند.

### فایل‌های مهم و جریان اجرا

- `MakanApp.Application/Messaging/MessagingEphemeralContracts.cs`: DTO و پورت state/access/notifier و سقف‌های معتبر؛ مرز Application و adapter را صریح می‌کند.
- `MakanApp.Application/Messaging/MessagingEphemeralService.cs`: Resolve نشست → مجوز جاری → state → بازبینی مجوز هنگام dispatch → notifier. hint قدیمی، مجوز جدید ایجاد نمی‌کند.
- `MakanApp.Infrastructure/Messaging/InMemoryMessagingEphemeralState.cs`: lock کوتاه برای mutation و aggregate چنداتصالی؛ I/O بیرون lock است. dispatch مجزا سریال شده تا hintها جابه‌جا ارسال نشوند.
- `MakanApp.Infrastructure/Messaging/EfMessagingStore.Ephemeral.cs`: reuse مجوزهای جاری GAP-05 و Peer Read؛ نه نسخهٔ موازی Child Safety و نه write به history.
- `MakanApp.Api/Realtime/SignalRMessagingEphemeralNotifier.cs`: انتشار به ConnectionIdهای دقیق مجاز، نه broadcast نامحدود group.
- `MakanApp.Api/Controllers/PeoplePresenceController.cs`: HTTP → Application → پورت EF/SQL و state → DTO → HTTP؛ هیچ قانون کسب‌وکار در Controller نیست.

`MessagingPrivacySettings` و مسیر Directory/Resolver/EF Configuration برای دو audience تغییر کردند. `MessagingService.SendMessageAsync` پس از موفقیت یا retry پذیرفته‌شده، Typing همان نشست را متوقف می‌کند. Hub lifecycle ثبت/حذف state را انجام می‌دهد؛ `Program.cs` و DI adapterها را متصل می‌کنند. fixture تست worker گذرا را دستی flush می‌کند تا ساعت و انتشار قطعی باشند؛ notifier آزمایشی انتشار واقعی SignalR را نیز اجرا می‌کند.

### دیتابیس و قرارداد

Migration `20261016163433_AddMessagingPresencePrivacy`: دو ستون int غیرnullable با Contacts=2، CHECK مقدار 1/2/3؛ PK/FK و rowversion موجود حفظ شده‌اند. این default تصمیم مصوب privacy است، نه زمان یا اطلاعات فعالیت. state حضور در SQL نیست. endpointها، DTO، مجوز و خطاهای واقعی در `MESSENGER_P2_FRONTEND_HANDOFF.md` آمده‌اند؛ همان سند بخشی از این گزارش است.

### تصمیم‌ها و trade-off

state تک‌process پشت پورت به‌جای Redis جدید انتخاب شد؛ ساده و bounded است اما چندسروری نیست. Last Seen حافظه‌ای بعد از restart/retention از بین می‌رود. audience مصوب Contacts با رابطهٔ معتبر ترکیب می‌شود؛ Everyone به‌تنهایی مجوز مشاهدهٔ غریبه نیست. انتشار Hidden ناشی از فعالیت مقصد حذف شده تا timing side-channel ایجاد نکند؛ در نتیجه کلاینت موظف به انقضای snapshot و refresh است. رویداد گذرا durable/Delta نیست و تحمل از‌دست‌رفتن آن بر TTL تکیه دارد.

### تست‌ها و نکات آموزشی

`MessagingEphemeralStateTests` با ساعت ورودی، aggregate، TTL، ظرفیت/بودجه، مالک اتصال و حفظ privacy در درخواست قدیمی را می‌سنجد؛ I/O ندارد. `MessagingP2EphemeralEndpointsTests` روی HTTP/Hub واقعی TestServer و SQL Server ایزوله، مجوز گیرنده، Block در dispatch، دو نشست، انقضای ناگهانی، لغو نشست، subscriber کانال، حذف عضو و لغو رابطهٔ Guardian برای Minor/Unknown را می‌سنجد؛ بنابراین Integration است. تست‌های Architecture موجود برای جهت وابستگی اجرا می‌شوند، نه تست بی‌معنای جدید صرفاً برای تعداد.

۱. عضویت SignalR group، مجوز امنیتی نیست. ۲. Online از lease معتبر می‌آید، نه User موجود. ۳. وضعیت چنداتصالی باید aggregate شود. ۴. فیلد اختیاری افزوده‌شده نباید PUT قدیمی را به reset مخفی تبدیل کند. ۵. زمان دقیق heartbeat می‌تواند privacy Online را دور بزند؛ این مسیر در DTO بسته است. ۶. تست اتصال باید پایان آماده‌شدن Hub را با یک فرمان تأیید کند، نه Sleep.

### مسیر مطالعه و ادامه

`PeoplePresenceController` → `MessagingHub` → `MessagingEphemeralService` → `MessagingPrivacySettings` → پورت‌های Ephemeral → state حافظه‌ای و partial EF → Configuration → Migration → Unit و Integration بالا. گام بعد سازمان‌دهی Saved و پوشه‌های شخصی است؛ هیچ پوشه‌ای نباید مجوز گفتگو ایجاد کند. Validation و Git هر فاز پس از اجرای واقعی به ممیزی و گزارش نهایی اضافه می‌شوند؛ این متن به‌تنهایی ادعای بسته‌شدن P2 نیست.

### Validation واقعی فاز اول

restore و build/final build موفق با صفر warning/error؛ Unit=507، Integration=820، Architecture=14، مجموع=1341 passed، failed/skipped=0. هشت Unit و نوزده Integration به baseline افزوده شدند. migration روی SQL Server تست ایزوله اجرا شد و `has-pending-model-changes` تغییری نیافت؛ روی سرور یا دیتابیس عادی اجرا نشده است. جزئیات دو دور و تشخیص فشار حافظه در ممیزی ثبت شده‌اند. پیام انتخاب‌شده برای commit متمرکز این فاز `feat(messaging): add private typing and presence leases` است؛ hash و نتیجهٔ push فقط پس از اجرای Git گزارش می‌شوند. محدودیت‌های فعلی: فقط تک‌process، Last Seen حافظه‌ای، بدون Recording، و باقی فازهای P2 هنوز در این commit نیستند.

Git فاز اول واقعاً انجام شد: commit `8d0b92ae33718113d522827f561e57520f392651` با همان پیام، شاخه `messenger/p0`، upstream `origin/main`، remote `origin` در `https://github.com/RastinaxAI/MakanApp.git`. push معمولی `HEAD:main` و fetch/برابری HEAD با origin/main موفق؛ ۳۶ فایل فقط همین فاز و PDF مرجع با bytes بدون تغییر وارد commit شدند. worktree تمیز بود؛ dirtyهای workspace اصلی AI نه تغییر کردند و نه stage شدند.

## فاز دوم: P2-5/P2-6 — Saved Labels و Folders

### خلاصه و لایه‌ها

قبلاً Saved canonical، رسانه و Forward مستقل داشتیم اما label و پوشهٔ شخصی نداشتیم. `ChatFolder`, `FolderConversationOverride`, `SavedMessageOrganization` و `SavedMessageLabel` در Domain، فقط invariantهای نام/نوع/ترتیب/برچسب را نگه می‌دارند. `MessagingPresentationService` در Application ورودی، نسخه و session/context را validate می‌کند؛ پورت `IMessagingPresentationStore` feature-specific است، نه generic repository. partial `EfMessagingStore.Presentation.cs` و Configurationها در Infrastructure مسئول transaction/قفل/FK/SQL هستند. Controllerهای `MessagingFoldersController` و `SavedMessageLabelsController` فقط transport را map می‌کنند.

### جریان و فایل‌های تغییرکرده

HTTP → Controller بالا → `MessagingPresentationService` → Domain validation → `IMessagingPresentationStore` → partial EF → SQL Server → Result → HTTP. `MakanDbContext` چهار DbSet، DI دو registration و handler سه کد خطا اضافه می‌کنند. `GetConversationListQuery` و مسیر `ConversationsController`/`MessagingService`/`EfMessagingStore.Reads.cs` selector اختیاری FolderId دارند؛ query مجاز قبلی پیش از pagination فیلتر می‌شود. `MessagingSafetyService` و queryهای Search/Controller `MessagesController` فیلتر SavedLabel را به همان مسیر مجاز قبلی اضافه می‌کنند؛ جست‌وجوی tag-only آگاهانه q خالی را می‌پذیرد، نه جست‌وجوی unrestricted تازه.

### دیتابیس و concurrency

Migration `20261016170915_AddMessagingPersonalOrganization`: `ChatFolders` با PK Guid، FK مالک، rowversion و index ترتیب مالک؛ `FolderConversationOverrides` با PK مرکب FolderId/ConversationId، FK پوشه Cascade و گفتگو Restrict، pin CHECK؛ `SavedMessageOrganizations` با PK/FK همان MessageId، مالک و rowversion؛ `SavedMessageLabels` با PK مرکب MessageId/Name، FK مجموعه و index Name/MessageId. طول، mask مجاز، revision مثبت و نام غیرخالی CHECK دارند. label canonical با collation باینری ذخیره می‌شود تا SQL و normalization اختلاف casing ایجاد نکنند.

mutationها transaction کوتاه Serializable دارند. قفل User فقط برای سریال‌کردن سقف پوشه و دستگاه‌های همان مالک است؛ Identity تغییر نمی‌کند. برچسب با قفل گفتگوی Saved و rowversion مجموعه تغییر می‌کند. revision عمداً حتی با ساعت ثابت تغییر می‌کند تا جایگزینی label/override نسخهٔ جدید بدهد. حذف فرزند و insert مجدد در دو Save داخل یک transaction انجام می‌شود؛ بین آن‌ها حالت نیمه‌کاره منتشر نمی‌شود. GET نسخه و فرزندان در RepeatableRead است تا version قدیمی کنار فرزند تازه برنگردد. مجوز overrideها batch بررسی می‌شود، نه دو query برای هر ConversationId.

### Endpointها، تصمیم‌ها و تست‌ها

تمام method/route/request/response/validation/auth/error واقعی در بخش جدید Handoff آمده‌اند. تغییر مجموعه با PUT کامل به‌جای API ریزدانهٔ بی‌نیاز انتخاب شد؛ trade-off سقف ۲۰۰ override و ده label، در برابر مدل ساده و rowversion واحد است. پوشهٔ حذف‌شده، چت را حذف نمی‌کند. tag منبع Forward را به metadata خصوصی تبدیل نمی‌کند و فقط copy مجاز را سازمان می‌دهد. multi-device پایدار از REST/version همگرا می‌شود؛ event/Delta مصنوعی ساخته نشد.

`MessagingPresentationTests` شش حالت Unit برای mask/revision، نام خالی/control، override نامعتبر و normalization دارد. `MessagingP2PresentationEndpointsTests` دوازده Integration واقعی SQL/HTTP برای canonical Saved، مالکیت، مخفی‌شدن پیام، جست‌وجوی label، نسخهٔ هم‌زمان، منشأ Forward پس از حذف عضو، CRUD پوشه بدون حذف چت، عدم grant با override، pagination/filter/pin، unread/archive، دو session و smoke بیست‌وپنج گفتگو دارد. هدفمند هر دوازده passed هستند؛ دو suite کامل و Git فقط پس از اتمام واقعی به ممیزی افزوده می‌شوند.

### نکات آموزشی و مطالعه

۱. UI selector مجوز نیست؛ query پایهٔ مجاز را فیلتر کنید. ۲. rowversion باید روی aggregate مجموعه باشد، نه فقط فرزندها. ۳. DELETE/INSERT کلید یکسان نیاز به مدیریت tracker و transaction دارد. ۴. نسخه و دادهٔ چندquery باید snapshot سازگار بدهند. ۵. کنترل سقف count در race به قفل/transaction نیاز دارد. ۶. pin/filter باید قبل از Skip/Take باشد.

ترتیب مطالعه: دو Controller → `MessagingPresentationService` → دو فایل Domain → پورت Presentation → partial EF → Configurationهای ChatFolder/Saved → Migration → دو فایل تست. محدودیت فعلی: label ساده، نه note/taxonomy، پوشه صرفاً ارائهٔ شخصی، بدون event پوشه؛ invite/join و مدیریت پیشرفته در گام مستقل بعد قرار دارند.

Validation واقعی فاز دوم: restore موفق، build و final build صفر warning/error؛ هر دو suite کامل Unit=513، Integration=832، Architecture=14، Total=1359 passed، failed/skipped=0؛ EF pending-model check موفق بدون تغییر معوق. شش Unit و دوازده Integration جدید اضافه شدند. fixture فقط دیتابیس تست را migrate کرد؛ سرور، appsettings، ملی‌پیامک، AI و frontend تغییر نکردند. پیام commit انتخاب‌شده `feat(messaging): add folders and advanced saved organization` است؛ hash و push بعد از Git گزارش می‌شوند.

Git فاز دوم انجام شد: `d317aa446443dc58811cfaa04970612c468f375d` با همان پیام، ۳۳ فایل فقط Saved/Folders/مستندات، branch=`messenger/p0` و remote/upstream=`origin/main`. push/fetch و برابری remote موفق، worktree تمیز و dirtyهای AI بیرون commit بودند.

## فاز سوم: P2-7 تا P2-10

### مسئله، لایه‌ها و فایل‌ها

قبلاً دعوت target-bound و permission پایه داشتیم، اما bearer link، approval queue، Ban و محدودیت فردی نداشتیم. نقش‌های قدیمی جایگزین نشدند. `ConversationInviteLink.cs` شامل دو entity لینک/درخواست و lifecycle است؛ `ConversationMemberRestriction.cs` اختیار ممنوع و انقضا را نگه می‌دارد. `ConversationParticipant.CanManage/CanRestrictMember` قاعدهٔ خالص Domain برای جلوگیری از escalation و محدودیت Owner/self است؛ `Conversation.UpdateInformation` فقط مشخصات Group/Channel مجاز را تغییر می‌دهد.

`ConversationAdvancedManagementContracts.cs` قراردادهای صریح و پورت feature-specific، و `ConversationAdvancedManagementService.cs` validation/context use case را در Application دارند. سه partial زیر مسئول SQL هستند: `EfMessagingStore.InviteLinks.cs` برای entropy/hash و join، `EfMessagingStore.JoinRequests.cs` برای تصمیم/نسخه، و `EfMessagingStore.AdvancedManagement.cs` برای capability/audit/info/restriction. دو Controller جدید `ConversationInviteLinksController` و `ConversationAdvancedManagementController` فقط HTTP را map می‌کنند.

### جریان اجرا و تغییرات مهم

HTTP → Controller → `ConversationAdvancedManagementService` → `IAccessContextResolver` → Domain validation → پورت → `EfMessagingStore` → قفل Conversation و revalidation نشست/role در SQL → eligibility و current GAP-05 → mutation/audit/نسخه → commit → DTO/HTTP. برای join، ParticipantChanged موجود از Outbox بعد از commit می‌رود؛ وضعیت خصوصی request/link به همهٔ اعضا broadcast نمی‌شود. صفحهٔ مدیریت از REST نسخهٔ تازه می‌گیرد.

مسیرهای موجود AddMember، JoinPublic و Invitation acceptance برای Ban بازبینی شدند؛ Send/Typing همان محدودیت فردی جاری را می‌سنجند. `ConversationManagementService.ChangeMemberRoleAsync` bit ناشناخته را قبل از رسیدن به exception دامنه رد می‌کند؛ این خطای واقعی 500 با تست پیدا شد، نه با تغییر انتظار تست مخفی شود. DI، DbContext، Configurationها و GlobalExceptionHandler فقط همین قابلیت را متصل می‌کنند؛ هیچ Identity/OTP/SMS/AI پیاده‌سازی تازه‌ای نداریم.

### دیتابیس و concurrency

لینک PK Guid و hash یکتای char(64)، creator، expiry، maximum/used count، approval، context سروری سازمان و rowversion دارد. Guid شناسهٔ لینک secret نیست؛ توکن ۳۲ بایت cryptographic است و SHA-256 یک nonce پُرentropy را نگه می‌دارد، نه hash نامناسب password. درخواست FK مرکب InviteLinkId+ConversationId دارد؛ شناسهٔ لینک دیگر را نمی‌توان به گفتگو چسباند. unique filtered `(ConversationId,UserId) WHERE Status=Pending` مانع duplicate pending است. restriction کلید مرکب ConversationId/UserId و revision/rowversion دارد. همهٔ FKها محدودند؛ join سازمانی Membership/Enrollment را تغییر نمی‌دهد.

قفل ترتیب‌دار Conversation در transaction کوتاه Serializable، مصرف آخرین slot، revoke/approval و permission/action را خطی می‌کند. source/target Membership/Role از snapshot سروری فقط selector هستند و با state جاری دوباره validate می‌شوند. Session نیز داخل transaction کنترل می‌شود تا auth اولیه به‌تنهایی مجوز ادامه نباشد. انقضا از ساعت سرور بعد از انتظار قفل سنجیده می‌شود. provider/network در transaction نیست؛ hash/RNG محلی و کوتاه‌اند. rollback migration در حضور flag جدید، به‌جای کاهش بی‌اجازهٔ permission متوقف می‌شود.

### قرارداد، تصمیم‌ها و محدودیت‌ها

جزئیات endpoint/request/response/auth/validation/error و loading/empty در Handoff همین فاز آمده‌اند. لینک چندمصرفی و queue با دعوت target-bound یکی نیستند، اما eligibility/رضایت/عضویت Messaging و audit موجود reuse شدند. Ban در جدول جدا و پایان عضویت موجود نگه داشته شد تا lifecycle تاریخی و نقش‌ها شکسته نشوند. Unban هیچ عضویت خودکاری ایجاد نمی‌کند. token plaintext در DB/audit/list نگهداری نمی‌شود؛ trade-off این است که پس از گم‌شدن پاسخ اولیه باید revoke/new-link انجام شود، نه بازیابی secret. حداکثر انقضا مطابق دعوت موجود ۳۰ روز و pageها bounded هستند.

این API مدیریت دستی Group رسمی کلاس یا private chat browsing پلتفرم نیست. role مدرسه، UserId، لینک قبلی یا GuardianRelation اختیار جدید برای private Direct کودک نمی‌آورند. Contact خود حساب محافظت‌شده طبق policy فعلی رابطهٔ رضایتی است؛ آزمون «غریبه» نباید آن را از قبل بسازد. باقی P2، scale-out و real Push همچنان از این فاز نتیجه نمی‌شوند.

### تست‌ها، validation و نکات آموزشی

`ConversationAdvancedManagementTests`: پنج Unit خالص برای expiry/limit/hash/context، terminal idempotency، permission مستقل و restriction/time. سه فایل `MessagingP2ManagementEndpointsTests`، `MessagingP2ManagementAuthorizationTests` و `MessagingP2ManagementSafetyTests`: بیست‌وشش Integration SQL/HTTP/Hub با race واقعی و FK واقعی؛ شمارهٔ 547 و constraint موردنظر تأیید می‌شوند، نه هر exception. حالت‌های FromJoin، protected audience، Contact revocation، creator-role revocation، approval×revoke، ban در دعوت قبلی و عدم ساخت Enrollment/Membership پوشش دارند. هدفمند همه passed هستند؛ دور کامل اول Unit=518، Integration=858، Architecture=14، Total=1390 passed، failed/skipped=0. build/restore اجراشده صفر warning/error دارند؛ suite نهایی و Git فقط بعد از نتیجه ثبت می‌شوند.

۱. credential لینک باید با شناسهٔ DB فرق کند. ۲. hashِ nonce پُرentropy با password hashing یک مسئله نیست. ۳. Pending عضو فعال نیست. ۴. context تأییدکننده جای context سازنده/درخواست‌کننده نیست. ۵. unique index بدون transaction کافی نیست تا چند جدولِ اثر join هماهنگ شوند. ۶. test fixture می‌تواند ناخواسته رابطهٔ رضایتی بسازد؛ ردشدن تست را با حذف policy اصلاح نکنید. ۷. ورودی enum ناشناخته باید خطای پایدار client بدهد، نه exception داخلی.

مسیر مطالعه: دو Controller → service/contract Application → سه قاعده/entity Domain → سه partial EF → Configurationها → `20261016174429_AddConversationInvitesAndRestrictions` → سه Integration و Unit بالا. گام بعد Topics و تعامل پیام است؛ مجوز معلم باید از authority کلاس فعلی بیاید، نه Admin کردن ساختگی همهٔ معلم‌ها، و Quiz با Exam آکادمیک یکی نیست.

نتیجهٔ واقعی نهایی فاز سوم: restore، build و final build موفق با صفر warning/error؛ هر دو suite کامل ۱۳۹۰ passed، شامل Unit=518، Integration=858 و Architecture=14، failed/skipped=0؛ EF تغییر معوق ندارد. پنج Unit و بیست‌وشش Integration جدید اضافه شدند. Git پس از بررسی diff با پیام `feat(messaging): add invite links and join requests` انجام می‌شود؛ hash/تأیید remote فقط پس از اجرا گزارش می‌شوند.
