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

Git فاز سوم انجام شد: `f93a9ed98bd2674b257accd03324b8d797eaec56`، ۳۷ فایل مرتبط، پیام بالا، branch=`messenger/p0` و upstream=`origin/main`؛ push/fetch و برابری remote تأیید، worktree تمیز و فایل‌های AI بیرون commit بودند.

## فاز چهارم: P2-11 — Topics

### مسئله و معماری

قبل از این فاز هر پیام فقط به conversation وصل بود؛ موضوع، وضعیت بسته و مجوز مدیریت آن نداشتیم. `ConversationTopic.cs` در Domain lifecycle و validation خالص، `ConversationTopicContracts.cs`/`ConversationTopicService.cs` در Application قرارداد/context/version و use case، `EfMessagingStore.Topics.cs` در Infrastructure query/قفل/authority/audit، و `ConversationTopicsController.cs` در Api فقط HTTP را دارند. `MessageContentSafetyGate.cs` helper داخلیِ مشترک واقعی است؛ رفتار سابق از `MessagingService` منتقل شده تا کپی دوم safety ساخته نشود. جهت وابستگی Onion عوض نشده است.

### فایل‌های تغییرکرده و اجرای واقعی

`Message` فقط TopicId اختیاری immutable می‌گیرد. command/history/Forward/resultهای Messaging فیلد اختیاری سازگار دارند. `MessagingService` validation، hash و mapping آن‌ها را متصل می‌کند؛ hash قدیمی بدون Topic تغییر نمی‌کند. partialهای Send/ChildSafety/Reads/Sync به‌ترتیب same-group/open، safety purpose مخصوص metadata، فیلتر قبل از pagination و visibility رویداد موضوع را کنترل می‌کنند. permission و enum change با CHECKهای SQL هماهنگ‌اند؛ DI و GlobalExceptionHandler قرارداد خطا را متصل می‌کنند.

HTTP → `ConversationTopicsController` → `ConversationTopicService` → context/preauthorization → `MessageContentSafetyGate`/provider خارج transaction → `EfMessagingStore.Topics` → قفل Conversation → session/current TeacherAssignment یا ManageTopics/GAP-05 → `ConversationTopic` → SQL/audit/TopicChanged → commit → Outbox قبلی → Delta/SignalR با audience جاری → DTO. title مسدود به SafetyIncident/ModerationCase فعلی می‌رود، نه پیام جعلی یا چت خصوصی قابل مرور برای Manager.

### دیتابیس، endpoint و طراحی

Migration `20261016183900_AddConversationTopics`: Topics با PK Guid، alternate key Id+ConversationId، FK به Conversation/User، title nvarchar(160)، datetime2 UTC، status/revision CHECK و rowversion. TopicId nullable روی Messages، FK مرکب و index Conversation/Topic/Sequence جلوی ارجاع گروه دیگر و pagination نامرتبط را می‌گیرند. پیام تاریخی backfill نمی‌شود. ManageTopics=4096 فقط flag جدید است؛ old Admin خودکار ارتقا نمی‌گیرد. TopicChanged=13 از همان log/outbox استفاده می‌کند. rollback دارای guard پیش از حذف داده است؛ تولید migration به معنی apply سرور نیست.

GET/POST موضوع‌ها، PUT موضوع نسخه‌دار، topicId در Send/history و destinationTopicId در Forward با request/response/validation/auth و خطاهای 400/403/404/409/412 در Handoff این فاز مستندند. Close حذف پیام نیست؛ انتخاب status Open همان reopen مجاز است. trade-off: cursor و history همچنان conversation-owned هستند و Topic مرز امنیتی موازی یا Exam آکادمیک نمی‌شود. topic-level pin/unread مستقل یا archived lifecycle ایجاد نشده‌اند؛ محدودهٔ این فاز Open/Closed مصوب است.

### تست، نکات آموزشی و مطالعه

`ConversationTopicTests` چهار Unit خالص برای lifecycle/UTC/validation/permission و TopicId immutable دارند. `MessagingP2TopicsEndpointsTests` و `MessagingP2TopicsSafetyTests` سیزده Integration SQL/HTTP، از جمله FK واقعی، Close×Send، FromJoin، Forward، دو نشست، permission revocation، authority معلم و عدم اختیار Manager/Student، provider fail-closed و لغو رابطه حین provider دارند. همهٔ تست‌های هدفمند passed، failed/skipped=0؛ suite کامل و Git پس از نتیجهٔ واقعی ثبت می‌شوند. خطای نام متد و هشدار async تست قبل از validation رفع شدند.

۱. nullable FK مرکب جلوی reference بین دو aggregate ناسازگار را می‌گیرد. ۲. permission جدید را با اعطای خودکار به Adminهای قدیمی اشتباه نگیرید. ۳. preauthorization قبل از network، reauthorization داخل transaction را حذف نمی‌کند. ۴. metadata کاربرساخته هم محتوای قابل‌بررسی است، ولی Message نیست. ۵. فیلتر pagination در query واقعی و فیلتر audience یک event هر دو لازم‌اند. ۶. تغییر fingerprint عمومی می‌تواند retry نسخهٔ قبلی را بشکند؛ extension باید سازگاری را حفظ کند.

مسیر مطالعه: Controller → TopicService/Contracts → ConversationTopic/Message/Permission → MessageContentSafetyGate → EfMessagingStore.Topics و Send/ChildSafety → Configuration → Migration → Unit/Integration. گام مستقل بعدی scheduled/silent است؛ Poll/Quiz منتظر قرارداد پرسیده‌شده‌اند، Checklist طبق استثنای prompt deferred است و هیچ‌یک نباید قبل از پیاده‌سازی موفق اعلام شوند.

Validation واقعی فاز چهارم: restore و build/final build موفق با صفر warning/error؛ هر دو دور Unit=522، Integration=871، Architecture=14، Total=1407 passed، failed/skipped=0. EF تغییر معوق ندارد و migration فقط در LocalDB ایزولهٔ تست اعمال شده است. چهار Unit و سیزده Integration افزوده شدند. commit متمرکز با پیام `feat(messaging): add versioned group topics` پس از review انجام می‌شود؛ hash/remote به نتیجهٔ واقعی Git وابسته است. محدودیت‌های فاز و مرز نبود تغییر AI/SMS/config/server/frontend همچنان برقرارند.

Git فاز چهارم: `7ebe826cd78ef4a49518e9c05e54c88836393675`، ۳۶ فایل مرتبط، همان پیام؛ branch=`messenger/p0`، upstream=`origin/main`، push/fetch و برابری remote موفق، worktree تمیز و فایل‌های AI بیرون commit بودند.

## فاز پنجم: P2-15 تا P2-17 — زمان‌بندی و بی‌صدا

### خلاصه، لایه‌ها و فایل‌های تازه

قبلاً فقط ارسال فوری و Mute گیرنده داشتیم؛ اکنون یک قصد ارسالِ مستقل تا dispatch در SQL پایدار می‌ماند و Silent اختیار همان send است. `ScheduledMessage.cs` در Domain lifecycle، اختیار ثبت‌شده، محتوای typed، فرزندان attachment/mention، lease و retry محدود را دارد؛ EF/HTTP/Timer ندارد. `ScheduledMessageContracts.cs` پورت و DTO، `ScheduledMessageService.cs` validation/context/version، `ScheduledMessageProcessor.cs` orchestration dispatch و `MessageCommandValidation.cs` validation مشترک واقعی را در Application نگه می‌دارند.

`EfMessagingStore.Scheduling.cs` ثبت/خواندن خصوصی/لغو و `EfMessagingStore.ScheduledDispatch.cs` claim/recovery/proof/failure را در Infrastructure دارند. `ScheduledMessageConfiguration.cs` سه mapping و `ScheduledMessageDispatcher.cs` میزبان worker scoped هستند. `ScheduledMessagesController.cs` فقط authenticated HTTP را map می‌کند. actor/context از ورودی client پذیرفته نمی‌شود و worker احراز هویت یا نشست موازی نمی‌سازد؛ authority پایدار از قصد قبلی و بررسی جاری منابع می‌آید.

### تغییرات مهم و جریان اجرا

`Message`، command/Forward/history و fingerprint، IsSilent اختیاری سازگار گرفته‌اند؛ default پیام سابق false و hash درخواست سابق بدون قابلیت جدید ثابت است. validation و reply/mention/file/Topic reference از مسیر فوری استخراج شده تا queue قرارداد ناسازگار دوم نسازد. private ScheduledClaim در قرارداد داخلی Store است، نه body عمومی API؛ token/context/body/hash و وضعیت جاری در SQL دوباره تطبیق داده می‌شوند. فایل factory تست فقط worker تازه را برای اجرای قطعی دستی برمی‌دارد؛ providerهای AI/SMS آن تغییر نکردند.

ثبت: HTTP → `ScheduledMessagesController` → `ScheduledMessageService` → context/validation → `EfMessagingStore.ScheduleAsync` → قفل Conversation/نشست/مجوز/ref جاری → `ScheduledMessage.Create` → SQL/audit → DTO Scheduled، بدون Message/Notification.

dispatch: `ScheduledMessageDispatcher` → `ScheduledMessageProcessor` → claim تک‌کاری SQL و commit lease → context ثبت‌شدهٔ سروری → `MessageContentSafetyGate` و provider خارج transaction → `EfMessagingStore.SendMessageAsync` → قفل Conversation→Scheduled + current account/role/relationship/Block/privacy/attachment/topic و token lease → `Message.Create` + `ScheduledMessage.Dispatch` + Outbox در یک transaction → commit → MessageCreated/SignalR/Unread/اعلان داخلی. source session ممکن است پایان یافته باشد، اما هیچ permission قدیمی تضمین نمی‌شود؛ mutation نشست لغوشده فوراً 401 است.

### دیتابیس، قرارداد و تصمیم‌ها

Migration `20261016190927_AddScheduledAndSilentMessages`: ScheduledMessages با PK Guid، unique conversation/user/clientMessageId، FK به conversation/user/source membership/subject و Topic/Reply/DispatchedMessage هم‌گروه، binary(32) hash، UTC datetime2، version، CHECK lifecycle/lease/context/attempt/time و index due/list؛ دو جدول composite-key attachment/mention با FK واقعی. متن pending در Message ساخته نمی‌شود؛ payload typed و محدود است، نه JSON اجرایی. referenceهای محتوا immutable هستند و split query صف، محتوای دو نسخهٔ متفاوت را ترکیب نمی‌کند.

فقط یک lease در هر نوبت گرفته می‌شود؛ claim کردن بیست کار و انتظار ترتیبی provider، lease کارهای آخر را می‌سوزاند. lease سی ثانیه، polling دو ثانیه، حداکثر پنج تلاش و backoff ۵/۱۰/۲۰/۴۰ است. کارِ crashشدهٔ تلاش آخر هم terminal می‌شود. cancel با rowversion، و در رقابت Cancelled→بدون پیام یا Dispatched→یک پیام است. edit/reschedule اختیاری این فاز ساخته نشده؛ cancel/recreate شناسهٔ تازه می‌خواهد. این trade-off، محتوای immutable و retry معنایی روشن می‌دهد.

سه endpoint ثبت/صف خود/لغو، body، response، 400/401/404/409/412 و وضعیت loading/retry در Handoff آمده‌اند. حداکثر صد Pending برای sender/conversation زیر lock است. initial permission فقط precondition ثبت است؛ dispatch permission مستقل و جاری است. خطای دائمی Failed، خطای موقت Scheduled با LastFailureCode و هرگز Sent نیست. queue خصوصی با REST/version همگراست، نه event گروهی.

`EfMessagingStore.Completion.cs` همان NotificationIntent موجود را با `muted || message.IsSilent` علامت می‌زند؛ `NotificationDeliveryProcessor.Authorization.cs` نیز IsSilent جاری را می‌سنجد. حذف notification داخلی یا تغییر SMS provider لازم نبود. Silent، temporary/hidden یا Mute نیست و پس از edit پیام عادی نیز flag آن عوض نمی‌شود. rollback migration در حضور دادهٔ واقعی از ابتدا توقف می‌کند و backup/تصمیم عملیاتی لازم دارد؛ روی سرور اجرا نشده است.

### تست‌ها، validation، نکات و مطالعه

`ScheduledMessageTests` شش Unit برای زمان/lease/terminal/reclaim/backoff/crash آخر و immutable Silent دارد. چهار فایل Integration `MessagingP2ScheduledEndpointsTests`، `MessagingP2ScheduledSafetyTests`، `MessagingP2ScheduledRecoveryTests` و `MessagingP2SilentEndpointsTests` بیست‌ویک حالت SQL/HTTP/Hub دارند: UTC due و retry پس از موعد، عدم ارسال زودهنگام با ID رزرو، دو worker، cancel×dispatch، cancel در provider، lease قدیمی در مرز SQL، منقضی/بازیابی و failure قدیمی، دو نشست، revoke، Block، membership در حین provider، نقش channel، Topic بسته، فایل حذف‌شده، fail-closed Minor/Unknown و Silent همراه normal regression. تست fault-injection Outbox نشان می‌دهد حتی پس از Save اولیه، rollback پیام/receipt اتفاق می‌افتد و tracker ثبت خطا آن را دوباره ذخیره نمی‌کند؛ retry فقط یک پیام/اعلان می‌سازد. تست Silent با Hub واقعی و provider ضبط‌شونده است، نه دریافت واقعی FCM/Web Push.

هدفمند شش Unit و بیست‌ویک Integration passed، failed/skipped=0؛ build اصلاح‌شده صفر warning/error است. خطای namespace/property تست و فرض نامعتبر Manager در fixture اصلاح شدند؛ policy تضعیف نشد. suite کامل/Git بعد از نتیجهٔ واقعی به ممیزی افزوده می‌شوند؛ هیچ موفقیت نهایی یا production acceptance از هدفمند به‌تنهایی نتیجه نمی‌شود.

۱. durable intent با نشست آنلاین یکی نیست. ۲. lease قدیمی حتی با محتوای درست اختیار commit ندارد. ۳. claimها را متناسب با زمان provider بگیرید، نه صرفاً تعداد batch. ۴. rollback SQL، ChangeTracker را به وضعیت قبل برنمی‌گرداند؛ ثبت خطا نباید entity شکست‌خورده را دوباره Save کند. ۵. Silent را در دو نقطهٔ intent و delivery gate اعمال کنید و internal notification را حفظ کنید. ۶. idempotency پس از موعد هم باید معنی روشن داشته باشد. ۷. workerای که آخرین تلاش آن crash کرده، به recovery terminal نیاز دارد.

مسیر مطالعه: ScheduledMessagesController → ScheduledMessageService/Contracts → ScheduledMessage → ScheduledMessageProcessor → MessageContentSafetyGate → Scheduling/ScheduledDispatch و Send → Configuration → Migration → Unit/Integration. بعد از بسته‌شدن Git این فاز، Search/پذیرش نهایی و hardening جاری ادامه دارد؛ Poll/Quiz تا پاسخ قرارداد و Checklist طبق استثنا جدا می‌مانند. server، AI/RAG، ملی‌پیامک و frontend از این مرحله خارج‌اند.

درس واقعی suite کامل: هدفمند ۲۱ مورد passed بود، اما race claim/cancel در دور کامل SQL 1205 داد؛ Unit=528، Integration=891 passed/1 failed، Architecture=14، skipped=0. queue-index UPD lock و مسیر Cancel با ترتیب متفاوت/upgrade، کافی نبودند. اکنون candidate readonly فقط hint است و claim نیز Conversation→Scheduled را رعایت و بعد از قفل، due/lease/status را دوباره می‌سنجد؛ Cancel با UPDLOCK/HOLDLOCK از conversion جلوگیری می‌کند. پنج round هم‌زمان با start barrier، بدون ضعیف‌کردن assertion، coverage regression را تقویت کردند. ۲۵ Integration هدفمندِ اصلاح‌شده passed هستند؛ suite کامل اصلاح‌شده هنوز در حال اجراست و commit/push تا نتیجه مجاز نیست. این نمونه نشان می‌دهد صرف استفاده از lock و transaction، معادل ترتیب صحیح قفل‌ها نیست.

Validation واقعی نهایی فاز پنجم: restore و build/final build موفق با صفر warning/error؛ هر دو suite کامل اصلاح‌شده Unit=528، Integration=896، Architecture=14، Total=1438 passed، failed/skipped=0. EF تغییر معوق ندارد؛ migration فقط روی fixtureهای SQL ایزوله اعمال شد. ۶ Unit و ۲۵ Integration افزوده شدند؛ خطای واقعی ابتدا fix و دوباره full test شد، نه مخفی/skip. commit با پیام `feat(messaging): add durable scheduled and silent messages` پس از review است؛ hash و تأیید remote فقط پس از اجرای Git گزارش می‌شوند. محدودیت‌های cancel/recreate، نبود distributed presence و real Push مستقل همچنان پابرجا هستند.

Git واقعی فاز پنجم: `4de7237233dcc8865672849f35e0cad06d6010b7` با همین پیام، branch=`messenger/p0`، remote=`origin`، upstream=`origin/main`، push/fetch موفق و HEAD==origin/main؛ ۳۶ فایل فقط همان فاز و worktree تمیز. وضعیت‌های pending مستقل تغییر نکردند.

## فاز ششم — Search و بازبینی مرزهای جاری

۱. خلاصه: فیلترهای واقعاً قابل اتکا و مرزهای revalidation/نسخه تکمیل شدند. قبلاً senderهای صفحهٔ Search تک‌تک بارگیری و Card subject فقط با شناسه validate می‌شد؛ اکنون batch SQL و همان visibility پروفایل برای همهٔ مخاطبان جاری اعمال می‌شود. نشست لغوشده در حین Safety نیز مجوز commit نیست.

۲. معماری: Api فقط binding/route و body metadata دارد؛ Application selector/normalization و orchestration Safety را مالک است؛ Infrastructure query، current authorization و lock SQL را دارد. Domain/Schema جدید لازم نشد؛ predicate موجود کشف به `MessengerRelationshipQuery.DiscoverableAccounts` منتقل شد، نه به Controller یا Domain وابسته به EF.

۳. فایل مهم جدید: `MakanApp.Infrastructure/Messaging/EfMessagingStore.ContactCards.cs`، batch بررسی subject/audience و جلوگیری از مسیر کشف فرعی است. شش فایل `tests/MakanApp.IntegrationTests/MessagingP2{SearchEndpoints,Revalidation,PresentationCoherence,RestrictionState,MetadataSafety,OpenApi}Tests.cs` شواهد رفتار واقعی‌اند؛ test helper جای تصمیم محصول/مجوز نیست.

۴. فایل‌های تغییرکرده: Safety query/service/contracts و MessagesController selectorهای تازه دارند؛ Advanced.Send/Mutations/ChildSafety نشست و Card را دوباره می‌سنجند؛ Presentation transaction/قفل هم‌جهت و parent-child coherence دارد؛ AdvancedManagement endpoint self و Safety مشخصات را دارد؛ Topics retry دقیق را بدون provider اضافه اداره می‌کند. Directory predicate قبلی را reuse می‌کند و policy پروفایل عوض نشده است.

۵. جریان اجرا: `MessagesController` → `MessagingSafetyService` → context/selector validation → `EfMessagingStore.SearchMessagesAsync` → SQL authorization/filter/count/page → `LoadSafeIdentitiesAsync` batch → DTO/HTTP. مسیر مشخصات: `ConversationAdvancedManagementController` → service → PrepareInfo → `MessageContentSafetyGate`/provider خارج transaction → UpdateInfo با قفل/نشست/مجوز/نسخه/audience تازه → SQL/Outbox → HTTP. metadata پیام ساختگی نیست.

۶. دیتابیس: migration تازه ندارد. read/write مجموعه‌ها از قفل مشترک مالک یا Conversation استفاده می‌کنند؛ preauthorization خارج transaction سپس reauthorization زیر قفل، ترتیب معکوس Message→Conversation را حذف می‌کند. rowversion همچنان مانع lost update است. fixture تلاش برای Deleted کردن فایل retained را با CHECK واقعی رد کرد؛ کد/constraint نگه‌داری برای تست عوض نشد.

۷. Endpointها: Search سه فیلتر اختیاری HasAttachment/HasLinks/TopicId دارد، bool false مکمل واقعی و empty Guid/enum/cursor/limit خطای 400 است؛ inaccessible فقط count صفر است. GET `.../{id}/me/restriction` فقط self، بدون reason، نسخه nullable برای نبود سطر، 401 نشست نامعتبر/404 عضو حذف‌شده دارد. mutation مشخصات همان contract قبلی و stale=412 را حفظ و Safety fail-closed را اضافه می‌کند. Handoff جزئیات و نمونهٔ قابل اجرا دارد.

۸. مفاهیم واقعی: current-state authorization پس از I/O، projection/batch به‌جای N+1، rowversion مجموعه، lock ordering، complement filter و concealment. Card historical پاک نمی‌شود؛ اجازهٔ ارسال تازه با حق نگه‌داری تاریخچه یکی نیست.

۹. تصمیم طراحی: raw-string سقف ۱۰۲۴ پیش از normalization در برابر trim/NFKC ورودی بزرگ است؛ سقف نهایی نام/label همان ۸۰/۳۲. body metadata ۶۴ KiB policy دارد. coarse lock مجموعه به‌جای retry کور یا snapshot نصفه، وضوح و consistency را با هزینهٔ serialization همان مالک/گفتگو می‌دهد. hasLinks تنها نشانگر متن واقعی است، نه crawler، URL validation یا موتور جدید Search.

۱۰. تست‌ها: ۲۸ Integration جدید شامل ۶ Search، ۹ revalidation، ۴ coherence، ۲ restriction و ۷ metadata/OpenAPI؛ SQL واقعی، HTTP، دو context و barrier race دارند. شمارندهٔ query فقط tag غیرحساس را می‌شمارد. چهار حالت revoke در provider، هیچ Message/Revision/Incident تازه‌ای نمی‌سازند؛ durable intent مستقل از source session ولی بدون session جعلی اجرا می‌شود. tuple نسخه/child در GET×PUT، انقضا بین validation و transaction و permission تغییرکرده در provider اثبات می‌شوند. contract metadata آزمون server/proxy Production نیست؛ Unit/Architecture تازهٔ بی‌معنا ساخته نشد.

۱۱. Validation: هدفمندهای ۲۱ و ۷ همگی passed، failed/skipped=0 و build آخر صفر warning/error است. Search اولیه یک fixture failure و تست attribute یک compile error داشتند که با SQL invariant/interface واقعی اصلاح شدند. suiteهای کامل و EF بعد از نتیجهٔ واقعی در ممیزی ثبت می‌شوند؛ تا آن زمان موفقیت نهایی/Git ادعا نمی‌شود.

۱۲. Git: commit فاز ششم فقط بعد از دو suite کامل و review انجام می‌شود؛ hash/push واقعی در ممیزی خواهد آمد. فایل‌های AI/MeliPayamak/appsettings/frontend و workspace اصلی دست‌نخورده‌اند.

۱۳. محدودیت: Poll/Quiz اکنون قرارداد نهایی مصوب دارند ولی از این commit پیاده‌شده حساب نمی‌شوند؛ Checklist، presence چندگرهی و دریافت واقعی Push همچنان deferred هستند. reverse proxy/بدنهٔ HTTP روی سرور تست نشده و migration سرور اجرا نشده است.

۱۴. نکات آموزشی: ۱) await شبکه فرصت تغییر مجوز است. ۲) یک subject قابل مشاهده برای sender الزاماً برای همهٔ recipientها مجاز نیست. ۳) rowversion parent بدون read consistency children کافی نیست. ۴) آزمون ناموفق constraint را دور نزنید. ۵) counter ثابت query از ثبت SQL حساس بهتر است. ۶) intent پایدار را با نشست آنلاین یا credential یکی نگیرید.

۱۵. مطالعه: MessagesController/AdvancedManagementController → MessagingSafetyService/AdvancedManagementService → همان Domain موجود → feature ports → Safety/ContactCards/Presentation store → configurationهای موجود → شش فایل تست بالا؛ migration جدیدی برای خواندن وجود ندارد.

۱۶. ادامه: فاز مستقل Poll/Quiz مطابق قرارداد Freezeشده، سپس ماتریس پذیرش و regression کل P2. anonymous/nonanonymous و correct answer نیازمند projection صریح و transaction SQL هستند؛ Exam/Grade، frontend یا AI نباید با آن مخلوط شوند.

بازبینی بعدیِ همین فاز، دو regression مهم دیگر اضافه کرد (مجموع ۳۰ Integration تازه): عضو تازهٔ کلاس پیش از درخواست یا حین provider هنوز participant ذخیره‌شده ندارد. هر دو تست ابتدا واقعاً 200 به‌جای 404 دادند؛ صرف سبزبودن دو suite قبلیِ ۱۴۶۶تایی کافی نبود. `EnsureContactCardAudienceAsync` اکنون برای کلاس رسمی از ClassMembers جاری استفاده می‌کند، نه projection ناقص participantها. درس: authorization روی read model همگام‌شونده می‌تواند عضو واقعی را جا بیندازد؛ منبع عضویت جاری و guard نهایی را با زمان‌بندی مشخص تست کنید. نتایج اصلاح‌شده در ممیزی جدا ثبت می‌شوند و تا آن زمان commit/push نداریم.

نتیجهٔ واقعی نسخهٔ اصلاح‌شده: هدفمند P2 برابر ۱۲۵ Integration passed؛ restore موفق، build/final build صفر warning/error، هر دو full/final suite برابر **528 Unit + 926 Integration + 14 Architecture = 1468 passed** و failed/skipped=0. EF تغییر معوق ندارد؛ migration جدیدی ساخته نشده است. شواهد `phase6-fixed-full` و `phase6-fixed-final` هستند. سی تست Integration تازه‌اند؛ Unit/Architecture بدون تغییر حفظ شدند. پس از بازبینی diff، فقط همین ۴۰ فایل مرتبط برای commit با پیام `fix(messaging): harden advanced search and current authorization` انتخاب می‌شوند؛ نتیجهٔ push و hash باید پس از Git واقعی ثبت شود.

Git واقعی فاز ششم: `bf133ffbf5d9f4bfb2804035657e4c60cda27f5a`، همان پیام، ۴۰ فایل مرتبط، branch=`messenger/p0`، upstream=`origin/main` و remote=`origin`؛ commit/push/fetch و برابری HEAD با origin/main موفق و worktree تمیز بود.

## فاز هفتم — Poll / Quiz و پذیرش محدود محلی

### ۱. خلاصه

قبلاً پیام تعاملی رأی‌گیری وجود نداشت. اکنون Poll با حریم خصوصی صریح/انتخاب یک یا چند گزینه و رأی متغیر نسخه‌دار، Quiz با پاسخ نهایی/کلید محافظت‌شده، Close نهایی و Forward متنی مصوب وجود دارد. پیام عمومی فقط پرسش است؛ نمایش گزینه‌ها/نتیجه از endpoint مجاز مستقل می‌آید. این قابلیت Messenger است، نه سامانهٔ آزمون آموزشی.

### ۲. معماری

Domain صاحب تعریف immutable، گزینه، lifecycle Poll و قواعد پاسخ نهایی است؛ به EF/HTTP وابسته نیست. Application صاحب DTO/validation/hash، resolve context و هماهنگی Safety بیرون transaction است. Infrastructure صاحب جدول و transaction SQL، projection محرمانه، current-state authorization، query محدود و Outbox است. Api فقط route، احراز هویت، status، rate/body metadata و فراخوانی سرویس دارد. وابستگی Onion تغییر نکرده و interface عمومی repository اضافه نشده است.

### ۳. فایل‌های مهم جدید

| مسیر | مسئولیت، دلیل و نکتهٔ آموزشی |
|---|---|
| `MakanApp.Domain/Messaging/ConversationPollDefinition.cs` | پرسش/گزینه‌های normalizeشده و snapshot readonly؛ ورودی mutable caller نباید تعریف منتشرشده را عوض کند |
| `MakanApp.Domain/Messaging/ConversationPoll.cs` | Open/Closed نهایی، validation انتخاب و revision؛ state machine و permission دو موضوع جدا هستند |
| `MakanApp.Domain/Messaging/ConversationPollOption.cs` | گزینهٔ شماره‌دار وابسته به تعریف؛ شمارهٔ گزینه یک‌مبنا و محلیِ Poll است |
| `MakanApp.Domain/Messaging/ConversationPollVote.cs` | یک پاسخ نهایی Quiz یا نسخه‌های رأی Poll؛ retry همان انتخاب با تغییر واقعی فرق دارد |
| `MakanApp.Domain/Messaging/ConversationPollSelection.cs` | انتخاب چندگزینه‌ای؛ count افراد از parent رأی می‌آید، نه count انتخاب‌ها |
| `MakanApp.Domain/Messaging/ConversationQuizAnswer.cs` | کلید جدا؛ جداکردن persistence projection احتمال افشای تصادفی را کم می‌کند |
| `MakanApp.Application/Messaging/ConversationPollContracts.cs` | DTO و port اختصاصی use case؛ EF entity به HTTP فرستاده نمی‌شود |
| `MakanApp.Application/Messaging/ConversationPollService.cs` | validation، idempotency hash و بررسی تمام گزینه‌ها/Explanation؛ I/O provider بیرون قفل SQL است |
| `MakanApp.Api/Controllers/ConversationPollsController.cs` | پنج endpoint نازک، body/rate policy و identity server-side |
| `MakanApp.Infrastructure/Messaging/EfMessagingStore.Polls.cs` | قفل واحد Conversation، revalidation و mutation/query اتمیک؛ Vote×Close linearization روشن دارد |
| `MakanApp.Infrastructure/Messaging/EfMessagingStore.PollVoters.cs` | projection فقط NonAnonymous، scope جاری، paging و یک identity batch |
| `MakanApp.Infrastructure/Messaging/EfMessagingStore.PollForward.cs` | کپی متنی مصوب پرسش/گزینه، بدون حتی query کلید یا رأی |
| `MakanApp.Infrastructure/Persistence/Configurations/Messaging/ConversationPoll*Configuration.cs` و `ConversationQuizAnswerConfiguration.cs` | پنج configuration واقعی؛ PK/FK/unique/check/index/rowversion از rule برنامه جدا و مکمل آن‌اند |
| `MakanApp.Infrastructure/Persistence/Migrations/20261016211112_AddConversationPollsAndQuiz.cs` | migration قابل بازتولید با Down guard پیش از حذف داده |
| `tests/MakanApp.UnitTests/Messaging/ConversationPollTests.cs` | ۱۶ حالت خالص تعریف/lifecycle/انتخاب/UTC/immutability |
| `tests/MakanApp.IntegrationTests/MessagingP2Poll{Endpoints,Authorization,Concurrency,SafetyAndDelivery}Tests.cs` | تست HTTP/SQL/SignalR/provider/concurrency واقعی، نه EF InMemory |
| `tests/MakanApp.IntegrationTests/MessagingP2FinalRegressionTests.cs` | چهار smoke محدود: ۹ اتصال، ۲۱ job، ۳۵ پیام Topic، sender/date/media |

### ۴. فایل‌های تغییرکرده

MessageKind.Poll و PollChanged افزوده شدند؛ Message و هر دو مسیر Edit از بازنویسی تعریف منتشرشده جلوگیری می‌کنند. Send عمومی/Schedule، Poll ناقص نمی‌سازند. MessagingService فقط mapper داخلی receipt را reuse می‌کند؛ GlobalExceptionHandler خطای پایدار Poll را به status مناسب می‌برد. DbContext/DI/config/snapshot مدل جدید را ثبت می‌کنند. Forward پیش و پس از Safety از همان کپی canonical استفاده می‌کند. Search/Reply عمومی فقط پرسش دارند. Delta، PollChanged را با همان پنجرهٔ پیام و حذف شخصی/عمومی فیلتر می‌کند. AbuseReport همان پروندهٔ scoped را با پرسش و گزینه‌ها، نه کلید، snapshot می‌کند. CHECK قدیمی moderation با ContactCard=6 ناسازگار بود؛ اکنون 6/7 با آزمون واقعی پذیرفته می‌شوند، بدون تضعیف provider. OpenAPI test مسیرها/enum/body/projection جدید را پوشش می‌دهد.

### ۵. جریان اجرا

`HTTP POST` → `ConversationPollsController.Create` → `ConversationPollService.CreateAsync` → `ConversationPollDefinition.Create` → `IConversationPollStore.PreparePollCreationAsync` → `MessageContentSafetyGate` و provider خارج transaction → `EfMessagingStore.CreatePollAsync` → قفل Conversation/نشست/مجوز/رابطهٔ جاری → `CreateMessageAsync` و Poll/Options/Key → SQL Save/Notification/Outbox/commit → receipt → HTTP 200. رأی از همان Controller/Service به `VotePollAsync`، validation Domain، parent رأی/selection و event کمینه می‌رود؛ GET پس از مجوز فقط projection لازم را می‌خواند.

### ۶. دیتابیس

پنج جدول `messaging.ConversationPolls/ConversationPollOptions/ConversationPollVotes/ConversationPollSelections/ConversationQuizAnswers` هستند. PollId همان MessageId و FK مرکب با ConversationId است؛ کلید گزینه `(PollId,Number)` و رأی `(PollId,UserId)` و انتخاب `(PollId,UserId,OptionNumber)` است. FK مرکب انتخاب را به رأی و گزینهٔ همان Poll می‌بندد. alternate key `(Id,IsQuiz)` مانع key/vote با نوع ناسازگار و FK CorrectOption مانع شمارهٔ ناموجود می‌شود. rowversion Poll و Vote جداست. CHECKها bounds/options/hash/lifecycle/UTC-order/revision و Quiz.SingleChoice تعریف را حفظ می‌کنند؛ دقیقاً یک key هنگام create اتمیک و تک‌انتخابی/immutable بودن پاسخ در Domain/use case enforce می‌شوند، نه CHECK ساختگی چندسطره. indexها paging/count انتخاب‌ها را پشتیبانی می‌کنند. همهٔ readهای جزئیات و Vote/Close از قفل Conversation هم‌جهت استفاده می‌کنند؛ coarse serialization همان گفتگو trade-off روشن consistency است. Down guard دادهٔ تازه را حذف نمی‌کند؛ apply روی سرور انجام نشده است.

### ۷. Endpointها

POST `.../conversations/{id}/polls` تعریف با ClientMessageId/privacy/Topic/Silent را می‌گیرد و MessageReceipt می‌دهد؛ متن/گزینه/Quiz نامعتبر 400 و تعریف تکراری متفاوت 409 است. GET `.../messages/{messageId}/poll` جزئیات scoped و نسخهٔ Poll را می‌دهد. POST `.../poll/vote` گزینه و own version را می‌گیرد؛ stale=412، Quiz تغییرکرده یا Closed=409. POST close نسخهٔ Poll می‌خواهد؛ creator با Send یا Owner/Admin با DeleteMessages می‌بندد. GET voters فقط NonAnonymous و عضو مجاز است؛ paging حداکثر۱۰۰ و بدون phone/key. همگی authenticated با session جاری و current resource permission، body ۶۴ KiB و rate موجودند؛ concealment=404، revoked=401، forbidden=403، Safety blocked/review=422 و unavailable protected=503. Handoff نمونه و تمام DTOها/خطاها را دارد.

### ۸. مفاهیم استفاده‌شده

immutable snapshot، projection کمینه، optimistic concurrency مستقل برای aggregate و رأی، transaction/قفل هم‌جهت، unique/FK، idempotency semantic، fail-closed، Content Safety post-I/O revalidation، invalidation event و batch query واقعاً استفاده شدند. CQRS/MediatR/GenericRepository، صف جدید یا سرویس خارجی اضافه نشد.

### ۹. تصمیم‌ها و trade-off

پاسخ صحیح در جدول جدا به‌جای فیلد message/option عمومی است؛ هزینهٔ query بعد از پاسخ را می‌پذیریم تا query قبل از پاسخ اصلاً وجود نداشته باشد. Forward متنی در برابر live-copy یا انتقال کلید طبق تأیید مالک انتخاب شد. Close با نسخهٔ aggregate در برابر last-writer-wins است؛ Vote برنده، Close را به refresh/412 می‌برد. API همه‌منظوره Send برای payload رأی‌گیری استفاده نشد تا orphan Poll/Message ممکن نشود؛ مسیر ایجاد پیام/Outbox موجود reuse شد. Anonymous محرمانگی API است، نه حذف شناسهٔ موردنیاز integrity در SQL. هیچ استثنای جدید افشای کلید برای creator یا Closed ساخته نشد.

### ۱۰. تست‌ها

۱۶ Unit خالص و ۴۵ Integration تازه‌اند (۴۱ Poll/Quiz و ۴ smoke). Integration شامل PK duplicate واقعی 2601/2627، FK واقعی547، رأی هم‌زمان ۱۲ حساب/دو دستگاه یک User، چهار round Vote×Close، version lost-update، rowversion مستقل، FromJoin/Clear/DeleteForMe/DeleteForEveryone، NonAnonymous صفحه‌دار، Anonymous identity concealment، QuizKey صفر query پیش پاسخ، سه count واقعی ماژول آموزشی قبل/بعد، rollback کامل Outbox و retry، Safety واقعیِ گزینهٔ unsafe و Card ReviewRequired، revoke حین provider، Block/relationship/Minor/Unknown، Report/Forward و Hub/Delta است. OpenAPI قبلی توسعه یافت؛ Architecture جدید بی‌معنا اضافه نشد و suite موجود Onion را بررسی می‌کند. T01–T50 به متدهای واقعی در ممیزی نگاشت‌اند؛ Checklist deferred است، نه skipped-test.

### ۱۱. Validation واقعی تا این نقطه

restore/build موفق و صفر warning/error؛ EF pending model changes: NONE. Unit هدفمند ۱۶ passed و Integration نهایی P2 برابر۱۷۰ passed، failed/skipped=0. دو round هدفمند نخست fixture/expectation failure واقعی داشتند و یک compile error نام property تست اصلاح شد؛ شواهد کامل آن در ممیزی حفظ‌اند. suite کامل اول در حال اجراست؛ نتیجهٔ آن و final suite فقط پس از پایان واقعی افزوده می‌شوند. دریافت واقعی Push و تست سرور ادعا نمی‌شوند.

### ۱۲. Git

فقط فایل‌های P2 در worktree مستقل مجازند. commit پیشنهادی `feat(messaging): add polls quizzes and p2 acceptance` پس از full/final build/test و review diff/cached انجام می‌شود؛ تا آن زمان hash/push ادعا نمی‌شود. شش commit پیشین و remote verification واقعی در ممیزی ثبت شده‌اند؛ AI/config/MeliPayamak/frontend/server دست‌نخورده‌اند.

### ۱۳. محدودیت‌ها

Checklist منتظر قرارداد محصول مجزاست؛ Recording، تعریف قابل ویرایش، Reopen Poll، retract vote، auto-close و Scheduled Poll API ندارند. ویرایش موعد Scheduled طبق optional بودن prompt با cancel/recreate جایگزین است. Topic مستقل unread/pin، presence/backplane/rate-limit چندگرهی، AV/CDR و دریافت واقعی Push تکمیل ادعا نمی‌شوند. Poll Search روی پرسش/فیلتر نوع است؛ index متن گزینه‌ها یا موتور جست‌وجوی جدید اضافه نشده است.

### ۱۴. نکات آموزشی

۱) SQL source-of-truth با افشای همهٔ ستون‌هایش یکی نیست. ۲) unique رأی والد و FK انتخاب، با validation تک‌گزینه‌ای و transaction مکمل‌اند. ۳) نسخهٔ کل Poll و نسخهٔ رأی یک نفر دو conflict متفاوت دارند. ۴) Retry همان پاسخ بعد از Close، اثر تازه نیست. ۵) query نکردن secret از redact دیرهنگام مطمئن‌تر است. ۶) MessageHiddenForUser مجاز را با نشت event اشتباه نگیرید. ۷) تست provider واقعی در کنار fake، ناسازگاری CHECKهای نوع را آشکار می‌کند.

### ۱۵. مسیر مطالعه

ConversationPollsController → ConversationPollService/Contracts → ConversationPollDefinition/Poll/Vote/QuizAnswer → IConversationPollStore → EfMessagingStore.Polls/PollVoters/PollForward → پنج EF Configuration → AddConversationPollsAndQuiz → ConversationPollTests → چهار Poll Integration و FinalRegression → ماتریس پذیرش/Handoff.

### ۱۶. ادامه

بعد از validation و Git این فاز، evidence نهایی P2 و remote verification بسته می‌شوند؛ سپس فقط با درخواست مستقل، قرارداد Checklist یا محیط واقعی Push/scale-out بررسی شود. قابلیت‌های P3/P4، AI/RAG، پرداخت، SMS عمومی و frontend نباید در این تحویل مخلوط شوند.

درس تکمیلی قبل از commit: دور کامل اول ۱۵۲۹ passed بود، اما review CHECK جدول تازه، مسئلهٔ سه‌ارزشی SQL را آشکار کرد. regression مستقیم Closed بدون زمان واقعاً شکست خورد (بدون exception)، و FK QuizKey→Poll عادی درست رد شد؛ `poll-null-before-fix` برابر۱ passed/۱ failed. SQL CHECK، UNKNOWN را مانند FALSE رد نمی‌کند؛ شرط nullable ضروری باید `IS NOT NULL` صریح داشته باشد. configuration و migration/Designer/snapshot تازهٔ همین فاز هم‌زمان اصلاح شدند. Domain/API قبلاً timestamp درست می‌دادند، ولی این جای integrity دیتابیس را نمی‌گیرد. با دو تست تازه، فاز هفتم اکنون۱۶ Unit و۴۷ Integration دارد؛ suiteهای کامل اصلاح‌شده باید دوباره اجرا شوند، نه اینکه نتیجهٔ سبز قبلی به نسخهٔ جدید نسبت داده شود.

Validation نهایی واقعی: تست SQL اصلاح‌شده۲ passed؛ restore موفق، build/final build صفر warning/error، هر دو suite کامل روی source ثابت **544 Unit + 973 Integration + 14 Architecture = 1531 passed**، failed/skipped=0. Integration اول حدود۷:۴۴ و نهایی۱۲:۳۲ بود؛ EF بدون تغییر معوق. در کل P2 نسبت به baseline، ۴۵ Unit و۱۷۲ Integration افزوده شد؛ جمع۲۱۷، بدون test شمارش‌ساز یا skip. source فاز هفتم۱۶ Unit و۴۷ Integration تازه دارد و OpenAPI موجود توسعه یافته است. PDF دقیقاً همان hash اولیه و فهرست تغییرات۵۳ فایل صرفاً مربوط به این مرحله است؛ نتیجهٔ واقعی Git بعد از اجرای آن در ممیزی ثبت می‌شود.

Git واقعی فاز هفتم: `9e35bb3ba53ee93cfa555a024792ce50fcef6c86`، پیام `feat(messaging): add polls quizzes and p2 acceptance`،۵۳ فایل فقط P2، branch=`messenger/p0`، remote=`origin`، upstream=`origin/main`؛ commit/push/fetch و HEAD==origin/main موفق و worktree تمیز بود. تمام هفت commit فازها push و remote-verified هستند؛ تغییر بعد از این SHA فقط ثبت همین شواهد مستندات است. نتیجهٔ code/product برابر `MESSENGER_P2_CODE_COMPLETE`، نه تأیید Production/دریافت Push/scale-out است. SHA آخرین commit مستندات و تطبیق نهایی remote در گزارش تحویل اعلام می‌شود.

برای مطالعهٔ کل P2: Handoff → EphemeralService و state/dispatcher → PresentationService/Store → AdvancedManagementService و Invite/JoinRequest → TopicService → ScheduledMessageService/Processor و dispatch → Search/Safety/ContactCards → PollService/Store → configuration/migration هر بخش → تست‌های نگاشت‌شدهٔ T01–T50. این ترتیب فرق state گذرا، دادهٔ presentation و intent پایدار را روشن می‌کند. مرحلهٔ بعد فقط با درخواست مستقل، محیط Web Push/FCM واقعی یا قرارداد Checklist است؛ AI/RAG، frontend و P3/P4 بخشی از این تحویل نیستند.
