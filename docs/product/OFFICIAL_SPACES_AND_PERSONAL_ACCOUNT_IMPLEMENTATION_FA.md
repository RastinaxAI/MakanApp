# حساب شخصی و فضاهای رسمی سازمان/کلاس

> این سند گزارش پیاده‌سازی و پذیرش فاز نخست است؛ HEAD، شمارش ۲۵۵۱ و `COMMIT=false` در بخش گزارش تاریخی به همان فاز تعلق دارند. فاز نهایی‌سازی ساخت Commit/Push و بسته‌های خصوصی Test/Production را جداگانه اعتبارسنجی می‌کند؛ نتیجهٔ واقعی و SHA نهایی در `RELEASE_REPORT_FA.md` و `validation-summary.json` بستهٔ Release ثبت می‌شوند. این مجوز شامل Deploy یا Migration دیتابیس‌های عادی/سرور نیست.

مبنای محصول: `MakanApp_Backend_Handoff_v2_Official_Spaces_FA.pdf` و تصمیم‌های صریح همین مرحله. مبنای کد هنگام شروع: `main / 9d70fa94eebcd59555e2872cb5fc921802583d27`؛ این HEAD یک Commit مستندات بعد از `e11d4ec` است. این سند اثبات استقرار روی سرور یا اجرای Frontend نیست.

## ۱. Audit و علت مسئله

حساب، عضویت، نقش، فضای منتخب و مالک اشتراک مستقل‌اند. `WorkspaceFactory` قبلاً همیشه کارت Personal فعال می‌ساخت و بیشتر سرویس‌های Messaging با `IAccessContextResolver` فضای منتخب نشست را برای عملیات شخصی هم مصرف می‌کردند. در نتیجه Teacher منتخب ممکن بود برای Direct، Saved، فایل، Poll یا Typing شخصی مجبور به انتخاب Personal شود.

Profile، Contacts، People، Privacy و Commerce شخصی از قبل مالکیت حساب را داشتند؛ از نو ساخته نشدند. مسیرهای Organization، Guardian، Academic و Commerce سازمانی همچنان به بافت مجاز جاری وابسته‌اند. هیچ `Role.Personal`، User دوم، انتخاب خودکار Role یا تغییر payer ایجاد نشده است.

## ۲. Visibility و نقش مؤثر

`GET /api/v1/workspaces/snapshot` یک snapshot با `Version` و `HasEffectiveEducationalRole` برمی‌گرداند. نقش مؤثر یعنی Organization، Membership و RoleAssignment فعال، بدون زمان پایان رابطه. Pending invitation نقش مؤثر نیست. وجود حداقل یک نقش مؤثر، `PersonalWorkspaceVisible=false` می‌دهد؛ پایان آخرین نقش آن را `true` می‌کند.

در حالت سازمان/عضویت تعلیق‌شده با نقش پایان‌نیافته، اگر نقش مؤثر دیگری وجود نداشته باشد، مقدار visibility برابر `null` و تصمیم `ProductDecisionRequired` است. این حالت با «بدون نقش» یکی نشده است. `CanEnter` و حالت واقعی هر رابطه در snapshot موجودند. نسخهٔ SHA-256 نشانگر تغییر snapshot است؛ permission token یا شمارهٔ ترتیبی قابل مقایسه نیست.

کارت Personal حتی هنگام پنهان‌بودن `CanEnter=true` دارد. مسیر قدیمی `/workspaces/me` برای سازگاری Personal و فضاهای قابل ورود را نگه می‌دارد و فیلدهای افزوده را هم برمی‌گرداند.

## ۳. بافت عملیات شخصی

`MessagingOperationContextResolver` ابتدا نشست فعالِ متعلق به همان حساب را از SQL اثبات می‌کند؛ سپس فقط برای عملیات مشخص، یک `AccessContext` موقت Personal می‌سازد. Session ذخیره‌شده یا فضای منتخب تغییر نمی‌کند. Storeهای موجود همچنان عضویت، current state، Block، consent، privacy، Safety، version و idempotency را بررسی می‌کنند.

| عملیات | انتخاب بافت |
| --- | --- |
| ساخت Direct/Group/Channel | `Scope=Personal` در قرارداد موجود؛ Organization طبق بافت جاری |
| خواندن/ارسال/مدیریت Conversation، Poll، Checklist، Topic، Schedule | تشخیص Personal از Conversation واقعی و مشارکت فعال خود کاربر؛ مسیر سازمانی با Resolver قبلی |
| Forward | تنها وقتی مبدأ و مقصد هر دو Personal و متعلق به مشارکت فعال actor باشند، بافت Personal؛ مسیر مختلط قواعد قبلی را نگه می‌دارد |
| Saved و Saved Labels، Block و گزارش‌های خود فرد | بافت حساب و نشست معتبر |
| Folder | پوشه و labels متعلق به حساب؛ overrideهای تماماً شخصی با اثبات مشارکت؛ override سازمانی همچنان در بافت سازمانی مجاز |
| دعوت و Link | Conversation/دعوت متعلق به target یا inviter و hash واقعی Link تعیین‌کنندهٔ scope است؛ رضایت، اعتبار، انقضا، سن و مجوز ورود در Store دوباره بررسی می‌شوند |
| Inbox، Search و Discovery | پارامتر افزودهٔ `scope=Personal`؛ بدون آن رفتار فضای منتخب حفظ می‌شود |
| Presence | حساب و سیاست Privacy؛ Typing برای Conversation واقعی و مجاز |
| Upload شخصی | `POST /api/v1/files?workspaceScope=Personal`؛ پیش‌فرض همان فضای منتخب |
| فایل شخصی موجود | `OrganizationId=null` با نشست معتبر؛ مالکیت، binding و ACL فایل همچنان اجباری |
| Replay حساب شخصی | `GET /api/v1/realtime/changes?scope=Personal` با cursor جدا از Organization |

کشف عمومی Group بدون `scope`، جست‌وجوی بدون Conversation/Scope و لیست JoinRequest بدون Conversation همچنان از فضای منتخب استفاده می‌کنند؛ این‌ها انتخاب ضمنی Personal نیستند. عملیات روی Conversation مشخص شخصی نیازی به انتخاب کارت Personal ندارند. نمایش/عدم نمایش کارت، یک قرارداد نمایش است و شرط Authorization نیست.

## ۴. مدل رسمی و مسیر وابستگی

`Conversation` موجود reuse شده است. `AcademicClassId` برای Group و Channel رسمی کلاس معتبر است. `IsOfficialOrganizationSpace` فضای رسمی سازمان را از SystemManaged قدیمیِ بدون binding صریح جدا می‌کند. ترکیب source واقعی، `OrganizationId`، purpose و `SystemManagedAcademic` مبنای رسمی‌بودن است؛ عنوان و JSON کلاینت هیچ authority ندارند.

سه مدل Domain اضافه شده‌اند:

- `OfficialSpaceProvision`: intent مستقل Group/Channel، وضعیت، تلاش و خطای دسته‌بندی‌شده.
- `AcademicActivityPublication`: رویداد انتشار با FK نوع‌دار Lesson، AssignmentVersion یا ExamVersion و revision مشخص.
- `AcademicActivityDelivery`: وضعیت هر مقصد، Message و Conversation نهایی؛ retry پس از موفقیت اثر جدید نمی‌سازد.

Domain به EF، HTTP، SignalR یا Outbox implementation وابسته نیست. Application قراردادها، Resolver و use case خواندن را دارد. Infrastructure SQL، EF configuration، capture محدود Storeها، projection و worker را دارد. Api فقط Controllerهای باریک و پارامترهای transport را نگه می‌دارد.

جریان واقعی:

```text
Controller
→ ClassLessonService / AssignmentService / ExamService
→ قواعد انتشار Domain
→ IAcademicActivityPublisher / OfficialSpaceEventWriter
→ تراکنش SQL: منبع + Publication + دو Delivery + Outbox موجود
→ Commit
→ OutboxProcessor / OfficialSpacesWorkHandler
→ EfOfficialSpacesStore / EfMessagingStore.DeliverAcademicActivityAsync
→ Message + MessagingChangeEvent + Outbox Realtime موجود
→ Commit
→ MessagingRealtimeDispatcher + account/session stream و replay موجود
```

## ۵. Provisioning، Repair و audience

ساخت Organization در مسیر provisioning پلتفرم و Class در Store آموزشی، دو intent در همان transaction ثبت می‌کند. Worker پس از Commit، Group و Channel مستقل را می‌سازد. قطع موقت Messaging، منبع آموزشی را rollback نمی‌کند. GETها هرگز Ensure یا Save اجرا نمی‌کنند.

`EnsurePairAsync` برای هر purpose تراکنش کوتاه دارد؛ برای منبع Class، ترتیب قفل Class سپس Organization با مسیر Academic و `CommercialWritePolicy` موجود یکسان است. برای source سازمانی فقط Organization قفل می‌شود. unique indexهای SQL از duplicate جلوگیری می‌کنند. مقصد موفق حفظ می‌شود و مقصد غایب قابل تکمیل است. منبع غیرفعال، binding مبهم یا Conversation آرشیوشده به `RepairRequired` با دستهٔ امن خطا می‌رود؛ حدس یا احیای خودکار صورت نمی‌گیرد. Retry محدود، lease، recovery و exhausted state از Outbox فعلی reuse می‌شوند. endpoint عمومی Repair/Ensure اضافه نشده است.

`OfficialSpaceAccessQuery` علت‌های فعلی دسترسی را از منبع معتبر استخراج می‌کند؛ جدول grant موازی ساخته نشده است:

| source | audience فعلی | ارسال |
| --- | --- | --- |
| Organization | فقط Manager فعال همان سازمان | Group و Channel با رعایت سیاست‌های فعلی |
| Class | Manager همان سازمان، TeacherAssignment معتبر، Enrollment معتبر Student | Group برای اعضای مجاز؛ Channel فقط Manager/Teacher مجاز |
| Parent | هیچ دسترسی گستردهٔ خودکار از GuardianRelation | اعطا نشده تا تصمیم محصول |

چند علت هم‌زمان با `ReasonId/Kind` در query قابل تفکیک‌اند. پایان یکی، علت دیگر را حذف نمی‌کند. همگام‌سازی فقط row مشارکت را materialize می‌کند؛ row قدیمیِ Active authority نیست. خواندن، ارسال، attachment audience، replay و تحویل Realtime دوباره source فعلی را بررسی می‌کنند. End سیستم در `EndedBySystem` ثبت می‌شود؛ شناسهٔ انسان ساختگی برای audit ساخته نشده است.

Manager به private Direct کودک یا دیگر اعضا دسترسی تازه نمی‌گیرد. Teacher/Class restriction سراسری Messenger به‌خاطر نیاز رسمی Manager گسترش نیافته است. Block و Safety فعلی حفظ شده‌اند؛ معنای دقیق Block در فضای مشترک همچنان تصمیم باز است.

## ۶. Metadata و capabilities

`ConversationSummaryResult.Official` از binding سرور ساخته می‌شود؛ `IsOfficial` از همین مقدار به دست می‌آید. ساخت عمومی گفتگو فیلد bind رسمی دریافت نمی‌کند و فیلد اضافه در JSON badge ایجاد نمی‌کند. انسان‌ها هیچ badge یا Role جدیدی نگرفته‌اند.

`CanRead/CanSend` حداقل قابلیت جاری مقصد آماده را بیان می‌کنند؛ Safety و نوع محتوا جداگانه اجرا می‌شوند. `CanManage=false` یعنی مدیریت دستی اعضا/مالکیت از API عمومی Group برای این فضاها وجود ندارد. مدیریت منبع سازمان/کلاس همچنان از APIهای School/Platform و مجوزهای قبلی انجام می‌شود.

## ۷. Activity Card و محرمانگی

فقط transition واقعی Publish، intent ایجاد می‌کند؛ Draft، پاسخ دانشجو، Autosave، Attachment پاسخ، Grade، evaluator note و نتیجهٔ منتشرنشده hook انتشار کارت ندارند. مقصد فقط Group و Channel همان Class است؛ بازنشر سازمانی انجام نشده است.

پیام در envelope موجود با `OriginKind=System` و `AcademicActivity` ساختاریافته تحویل می‌شود؛ نوع پایهٔ Message فعلاً Text است. sender ذخیره‌شده همان User آغازکننده برای FK و audit است، ولی `ActorKind=System` از binding انتشار می‌آید. عمومی `SendMessageCommand` قابلیت جعل آن را ندارد. Edit/Delete سراسری کارت از مسیر پیام کاربر رد می‌شود.

Card فقط title، شناسهٔ منبع/نسخه، revision، class/org، event/destination/purpose، actor آغازکننده، زمان UTC و زمان‌بندی مجاز دارد. متن درس، سؤال/گزینه/کلید آزمون، پاسخ، فایل خصوصی، نمره یا private note در Card قرار نمی‌گیرد. بازکردن resource reference همچنان ACL API منبع را لازم دارد؛ عضویت Conversation مجوز همهٔ فایل‌های کلاس نیست.

`AcademicActivityProjection` هنگام خواندن state جاری را بررسی می‌کند. نسخهٔ قدیمی `Superseded / IsActionable=false` می‌شود؛ event دیررس برای مقصدی که هنوز تحویل نشده، Superseded ثبت می‌شود و پیام قدیمی تازه نمی‌سازد. سیاست edit card در برابر correction message پیاده‌سازی نشده است.

برای اعلام «محتوا منتشر شده؛ تحویل کارت هنوز Pending است»، read API بر اساس نوع منبع، شناسهٔ نسخه و revision وجود دارد؛ لازم نیست کلاینت قبل از اولین کارت eventId را بداند. دوباره‌خواندن آن اثری در publish یا delivery ندارد.

## ۸. Dedupe، Outbox و Realtime

کلیدهای unique روی منبع/نسخهٔ Publication، `PublicationId+Purpose`، `PublicationId+ConversationId` و Message نهایی جلوی اثر تکراری را می‌گیرند. SQL منبع حقیقت است؛ exactly-once transport فرض نشده است. دو worker، lost ACK و retry با نتیجهٔ پایدار مدیریت می‌شوند. موفقیت Group با شکست Channel از بین نمی‌رود.

Outbox موازی و hook عمومی SaveChanges ساخته نشده‌اند. فقط Storeهای مشخص Organization، Academic و چهار write مرتبط Platform capture می‌کنند. اعلان آموزشی قبلی منبع حفظ شده؛ تحویل دو Card، دو Push آموزشی جدید ایجاد نمی‌کند. تحویل واقعی Push به دستگاه در این مرحله ادعا نشده است.

Account/session inbox stream، Conversation stream، rowversion cursor، replay و resync موجود reuse می‌شوند. عضویت جدید یک access invalidation کمینه بدون Resource/Organization خصوصی می‌گیرد تا inbox را refetch کند. MessageCreated برای هر مقصد، preview/order/unread را مستقل جلو می‌برد. خواندن یا mute یک مقصد، مقصد دیگر را تغییر نمی‌دهد.

Cursor به User/Session/Context/Operation scope بسته است. تغییر role/context یا استفادهٔ cursor شخصی در سازمان باید resync دهد. anchor نسخهٔ Personal این مرحله تغییر کرده است؛ cursorهای قدیمی ممکن است یک بار resync بخواهند. پس از revoke، تحویل و replay با state تازه بررسی می‌شوند.

## ۹. Legacy و Migration

منطق کامل در [برنامهٔ مهاجرت](OFFICIAL_SPACES_MIGRATION_PLAN_FA.md) آمده است. `PlanAsync` فقط خواندنی است؛ class Group قدیمی با FK و Type واقعی reuse می‌شود؛ نام مشابه کافی نیست. هیچ تاریخچه‌ای کپی و هیچ انتشار تاریخی بازپخش نمی‌شود. اجرای backfill در این مرحله فقط روی fixture ایزوله انجام شده است.

Migration: `20261030093927_AddOfficialOrganizationAndClassSpaces`. جدول‌های جدید، سه ستون افزوده، FKهای محدودکننده، rowversion، check و unique index دارد. index کلاس از source واحد به source+purpose گسترش یافته؛ داده یا جدول قدیمی حذف نمی‌شود. برای rollout هماهنگ، binary قدیمی که class Conversation را `Single` می‌خواند نباید هم‌زمان با فعال‌شدن دو مقصد باقی بماند. Down پس از استفاده با کد SQL `51131` رد می‌شود.

## ۱۰. تصمیم‌های باز

در نهایی‌سازی، CLI واقعی `tools/organization/MakanApp.OfficialSpacesMaintenance` اضافه شد. پیش‌فرض آن فقط planner خواندنی موجود است؛ `--apply` به سازمان مشخص، hash طرح جاری و نبود conflict نیاز دارد. ابزار Migration اجرا نمی‌کند، تاریخچه را کپی نمی‌کند و source قدیمی را دوباره publish نمی‌کند. اجرای این مرحله فقط روی fixture ایزوله است. تست فرایند واقعی، پیش‌فرض خواندنی و رد Apply بدون تأیید/با hash اشتباه را بررسی می‌کند. تست Repair در هر دو جهت و جدایی PersonalClass نیز به regression افزوده شده‌اند؛ شمارش نهایی باید از TRX فاز نهایی‌سازی خوانده شود.

تمام موارد زیر `OPEN_PRODUCT_DECISION` هستند؛ در این مرحله rule جدید حدسی نگرفته‌اند:

1. Parent در گروه کامل کلاس.
2. Parent در Channel کلاس.
3. audience گستردهٔ Group/Channel سازمان.
4. معنای دقیق Block در فضای مشترک رسمی.
5. تاریخچه بعد از پایان Enrollment/Role.
6. ویرایش Card در برابر correction message.
7. بازنشر فعالیت کلاس در Channel سازمان.
8. visibility نقش پایان‌نیافته با سازمان/عضویت Suspended.
9. مالکیت یادداشت خصوصی Teacher بعد از پایان assignment.
10. ingest خودکار یادداشت Teacher در AI.
11. retention نهایی.
12. اولویت نهایی notification/mute.

تا تعیین سیاست تاریخچه، current-state guard فعلی حفظ شده است؛ تاریخچه صرف پایان رابطه حذف نمی‌شود. Human badge یک gap مستقل است. Teacher Notes feature ساخته نشده و AI Runtime خاموش مانده است. دو Card در آینده باید reference یک منبع آموزشی باشند، نه دو منبع RAG؛ پیاده‌سازی ingestion/funding/streaming در این مرحله انجام نشده است.

## ۱۱. مسیر مطالعه و تست

مسیر پیشنهادی: `WorkspacesController` و `OfficialSpacesController` → `WorkspaceFactory` و `MessagingOperationContextResolver` → مدل‌های Domain جدید → `OfficialSpaceEventWriter` → `OfficialSpacesWorkHandler` → `EfOfficialSpacesStore` و `OfficialSpaceAccessQuery` → `EfMessagingStore.AcademicActivities` → EF configuration و Migration → تست‌های زیر.

- `PersonalVisibilityTests` و `MessagingOperationContextTests`: Visibility، چند نقش، تعلیق و projection موقت.
- `OfficialSpaceDomainTests`: binding، دو purpose، state و immutable receipt.
- `PersonalAccountVisibilityEndpointsTests` و `PersonalAccountResourceScopeTests`: نشست واقعی API و SQL ایزوله با نقش منتخب، فایل، Direct، Realtime و قابلیت‌های پیشرفته.
- `OfficialSpaceGrantSqlTests`: query واقعی SQL و revokeهای رابطه/حساب/کلاس/سازمان.
- `OfficialSpacesProvisioningTests`: دو worker، repair، منبع منتشرشده و payload حداقلی.
- `OfficialSpacesLifecycleAcceptanceTests`: commit/rollback، Outbox واقعی، نسخهٔ قدیمی، Parent، unique constraint و جلوگیری از جعل.
- `OfficialSpacesRealtimeTests`: SignalR داخل TestServer، offline replay، revoke، unread و mute مستقل.

تست‌های قدیمی مرتبط با Manager رسمی به تصمیم مصوب جدید تطبیق داده شدند؛ تست خصوصی‌بودن state دانشجو حفظ شد. fixture مهاجرت Checklist ستون‌های schema قدیمی را صریحاً درج می‌کند؛ تست race محدودیت عضو ساعت را پس از اثبات SQL نشست جلو می‌برد. هیچ تستی disable نشده است.

نتیجهٔ نهایی شمارش‌ها، matrix پذیرش و مرز اجرای واقعی در بخش‌های پایانی همین سند ثبت شده است. هیچ نتیجهٔ fixture به‌عنوان اجرای Frontend، Production یا دریافت Push واقعی معرفی نمی‌شود.

فایل‌های مهم و دلیل قرارگیری آن‌ها:

| مسیر | مسئولیت و دلیل ایجاد/تغییر | نکتهٔ آموزشی |
| --- | --- | --- |
| `MakanApp.Application/Messaging/MessagingOperationContextResolver.cs` | انتخاب بافت واقعی عملیات شخصی، بدون mutation نشست | scope با permission تفاوت دارد؛ بعد از projection نیز ACL لازم است |
| `MakanApp.Application/Messaging/OfficialSpacesContracts.cs` | port و DTO خواندنی رسمی/تحویل | Application قرارداد می‌دهد و Infrastructure آن را اجرا می‌کند |
| `MakanApp.Application/Organization/WorkspaceFactory.cs` | نمایش کارت و version snapshot | نمایش UI نباید اعتبار حساب یا داده را حذف کند |
| `MakanApp.Domain/Messaging/OfficialSpaceProvision.cs` | lifecycle آماده‌سازی هر purpose | وضعیت مقصدها مستقل است، نه یک flag مبهم برای کل جفت |
| `MakanApp.Domain/Messaging/AcademicActivityPublication.cs` | هویت immutable رویداد و منبع نوع‌دار | منبع آموزشی از پیام اعلان مستقل است |
| `MakanApp.Domain/Messaging/AcademicActivityDelivery.cs` | receipt و terminal state هر مقصد | retry نباید نتیجهٔ موفق قبلی را بازنویسی کند |
| `MakanApp.Infrastructure/Messaging/OfficialSpaceAccessQuery.cs` | علت‌های جاری دسترسی از روابط معتبر SQL | row مشارکت یا badge جای current authorization را نمی‌گیرد |
| `MakanApp.Infrastructure/Messaging/OfficialSpaceEventWriter.cs` | ثبت intent در تراکنش Source Store مشخص | hook محدود و صریح از SaveChanges سراسری قابل بازبینی‌تر است |
| `MakanApp.Infrastructure/Messaging/EfOfficialSpacesStore.cs` | خواندن، planner، Ensure و synchronization | ترتیب قفل و مشاهدهٔ binding جزئی از صحت عملیاتی‌اند |
| `MakanApp.Infrastructure/Messaging/EfMessagingStore.AcademicActivities.cs` | تحویل پایدار Card در SQL و Realtime موجود | side effect بعد از Commit باید idempotent باشد |
| `MakanApp.Api/Controllers/OfficialSpacesController.cs` | transport مسیرهای GET رسمی | Controller مجوز کسب‌وکار یا EF query ندارد |
| `MakanApp.Infrastructure/Persistence/Configurations/Messaging/OfficialSpacesConfiguration.cs` | FK، CHECK، unique index و rowversion | invariant مهم فقط در شرط Application رها نمی‌شود |
| `MakanApp.Infrastructure/Persistence/Migrations/20261030093927_AddOfficialOrganizationAndClassSpaces.cs` | ارتقای نسخه‌بندی‌شده و guard بازگشت | Migration با schema قدیمی و دادهٔ استفاده‌شده باید جدا آزموده شود |
| `tests/MakanApp.IntegrationTests/OfficialSpacesLifecycleAcceptanceTests.cs` | چرخهٔ واقعی Source/Outbox/SQL، race و محرمانگی | تست integration باید اثر و invariant را اثبات کند، نه صرفاً متد را دوباره اجرا کند |

در فایل‌های موجود `MessagingService`، سرویس‌های Poll/Checklist/Topic/Schedule، `StorageService` و `RealtimeSyncService` انتخاب بافت اصلاح شد؛ policyهای Store و فایل حذف نشدند. سه سرویس Publish فقط در نقطهٔ transition معتبر hook گرفتند. روش انتخاب‌شده query جاریِ access reason را به جدول grant موازی ترجیح می‌دهد: هزینهٔ query بیشتر است، ولی revoke به تأخیرِ materialization اعتماد نمی‌کند. تحویل مقصدها نیز به‌جای یک transaction بزرگ، receipt مستقل دارد و در عوض UI باید Pending جزئی را نمایش دهد.

جزئیات قرارداد کلاینت در [Frontend Handoff](OFFICIAL_SPACES_FRONTEND_HANDOFF_FA.md) آمده است.

## ۱۲. ماتریس B01 تا B45

شماره‌ها از دو جدول سه‌تایی سند Product تفکیک شده‌اند. «Backend» به معنی API/SQL/SignalR آزمایش‌شده است؛ loading، تب و cache واقعی Frontend در این Repository اجرا نشده‌اند. اجرای Kestrel لوکال نیز Production/سرور بیرونی محسوب نمی‌شود.

| شناسه | انتظار | شاهد Backend / مرز کلاینت |
| --- | --- | --- |
| B01 | بدون نقش: کارت نمایان | `PersonalVisibilityTests.NoRoleOrPendingInvitationDoesNotHidePersonal`؛ loading/error واقعی UI اجرا نشده |
| B02 | Pending: کارت نمایان | snapshot و `PersonalAccountVisibilityEndpointsTests`؛ Pending membership/role مؤثر ایجاد نمی‌کند |
| B03 | نقش مؤثر: کارت پنهان | چهار نقش در Unit و Teacher واقعی API در `TeacherWithHiddenPersonalCard...` |
| B04 | پایان Parent، Teacher معتبر بماند | query علت‌های مستقل و تست‌های چند نقش/چند سازمان؛ انتخاب UI جداست |
| B05 | نقش مؤثر سازمان دوم مانع نمایش اشتباه | `EndingOneRolePreservesParentInOtherOrganization` و تست API نقش‌های مستقل |
| B06 | پایان همهٔ نقش‌ها کارت را برگرداند | `LastRoleEndedRestoresCardAndChangesSnapshotVersion` و جریان API پایان آخرین نقش |
| B07 | چت/Contact بدون کارت Personal | `PersonalAccountResourceScopeTests` و Kestrel با Teacher منتخب و Direct شخصی |
| B08 | تعویض قابلیت نقش را تغییر ندهد | مقایسهٔ `AccessContext` قبل/بعد API شخصی؛ رفتار تب UI اجرا نشده |
| B09 | پاسخ دیررس دادهٔ سازمان قبل را ننویسد | نسخهٔ snapshot، cursor bound و resync/cross-context tests؛ generation و cache UI اجرا نشده |
| B10 | Organization/Class جدید دو مقصد صحیح | `OfficialSpacesOrganizationIntentRollsBackWithSourceAndCommittedPairUsesExistingWorker` و `OfficialSpacesClassCommitRecordsIntentAndReadWaitsForExistingOutboxWorker`؛ Kestrel مسیر عمومی ساخت را هم اجرا می‌کند |
| B11 | فراخوانی هم‌زمان همان جفت | `OfficialSpacesConcurrentEnsureCreatesExactlyOnePairPerResource` و unique index واقعی SQL |
| B12 | خرابی یک مقصد جفت دوم نسازد | `OfficialSpacesLegacyDryRunAndPartialRepairKeepGroupHistoryAndPublishNoHistoricalCards`؛ Pending از Ready بدون binding جداست |
| B13 | گفتگوی قدیمی با نوع صحیح حفظ شود | legacy Group در fixture و SQL upgrade از ۵۵ به ۵۶؛ route قدیمی فقط Group را می‌خواند |
| B14 | GET هیچ تغییری نسازد | شمارش Conversation قبل/بعد Pair/Planner/Delivery read؛ تست Ready بدون مقصد نیز side effect ندارد |
| B15 | Backfill بدون کپی/اسپم تاریخچه | legacy message محفوظ و Publication تاریخی صفر؛ SQL کامل و ارتقا دوبار اجرا شده‌اند |
| B16 | Teacher کلاس نامرتبط را نخواند/منتشر نکند | `OfficialSpacesReadConcealsCrossOrganizationAndDoesNotGrantParentOrUnassignedTeacher`، SQL grant tests و Kestrel unassigned Teacher با 404 |
| B17 | ساخت Class توسط Teacher رد؛ کلاس منتسب مجاز | regression `StabilizationAssignedTeacherCannotCreateOfficialClassAndStudentCannotReadFullRoster` و source ACL فعلی |
| B18 | Manager چت خصوصی نخواند | تست‌های private Direct و GAP-05 در suite کامل؛ grant Manager فقط روی binding رسمی اعمال شده است |
| B19 | رابطهٔ فرزند اول مجوز فرزند دوم نشود | regression `GuardianEndpointsTests` و آزمون‌های child context؛ Guardian authorization تغییر نکرده |
| B20 | Parent دسترسی گروه طبق سیاست صریح | `OfficialSpacesGuardianGetsNoBroadAccessAndRemovingOneReasonPreservesAnotherCurrentGrant`؛ Group/Channel گسترده اعطا نشده، سیاست نهایی باز است |
| B21 | پایان یک علت، علت دیگر را حذف نکند | Manager+Teacher بعد از پایان assignment، در SQL query و API/Sync واقعی تست شده است |
| B22 | Lesson/Handout یک Card در هر مقصد | theory `OfficialSpacesPublicationUsesStructuredSafeCardsAndTwoWorkerDedupe(Lesson)` و Kestrel |
| B23 | Assignment یک Card در هر مقصد | همان theory برای Assignment؛ private draft/attachment رویداد عمومی جدید نمی‌سازند |
| B24 | Exam یک Card در هر مقصد؛ Draft/Autosave صفر | همان theory برای Exam و `OfficialSpacesPrivateSubmissionAttachmentAndExamAutosaveNeverCreatePublicActivity` |
| B25 | retry و lost ACK اثر منبع/کارت دوم نسازد | دو handler هم‌زمان و اجرای دوباره؛ PK/unique receipt و idempotency قبلی منبع حفظ شده‌اند |
| B26 | تحویل مجدد Card را تکرار نکند | تعداد نهایی دو Message/Delivery و یک MessageCreated برای هر مقصد/event |
| B27 | رویداد قدیمی وضعیت جدید را برنگرداند | `OfficialSpacesOldPublicationCannotRestoreNewVersionAndSystemCardsCannotBeMutatedByUserApi` |
| B28 | پاسخ/نمره/کلید در Card نباشد | projection فقط metadata؛ marker سؤال/کلید در response نیست؛ draft و autosave خصوصی مسیر عمومی ندارند |
| B29 | لینک مستقیم فایل ACL مستقل داشته باشد | regression FileStorage، AcademicAssignmentAttachment و AcademicExamAttachment؛ Guardian raw attachment همچنان رد می‌شود |
| B30 | revoke روی صفحه و stream اعمال شود | query current-state در Read/Send، SQL revoke tests، `OfficialSpacesNewMembershipInvalidatesInboxAndEnrollmentRevocationFencesFutureActivity` |
| B31 | اولین پیام خارج از چت در inbox ظاهر شود | `OfficialSpacesManagerGetsFirstActivityWithoutOpeningConversationAndReplayHasNoDuplicateEffect` و WebSocket واقعی شخصی در Kestrel |
| B32 | reconnect با replay/reset بدون حذف تغییرات | همان تست رسمی و BCL WebSocket helper روی Kestrel؛ retry duplicate اثر دوم ندارد |
| B33 | context قبلی event به context جدید ندهد | `PersonalAccount...Realtime` و cursor/context revision regression؛ UI باید پاسخ قدیمی را کنار بگذارد |
| B34 | دو مقصد، Push تکراری و unread مشترک نسازند | Card delivery فقط تغییر Messaging می‌سازد؛ notification منبع reuse؛ unread مقصدها مستقل؛ دریافت واقعی Push دستگاه اجرا نشده |
| B35 | mute مستقل رعایت شود | `OfficialSpacesGroupReadDoesNotMarkChannelReadAndMuteRemainsPerConversation` و regressions mute/notification موجود |
| B36 | یادداشت خصوصی خودکار منتشر/ingest نشود | Teacher Notes ساخته نشده؛ hook عمومی وجود ندارد؛ هیچ AI invocation اضافه نشده است |
| B37 | دو اعلان یک منبع RAG باشند | **DEFERRED**؛ تنها source identity مشترک در metadata موجود است، ingestion اجرا نشده |
| B38 | منابع Ready مجاز و منابع مصرف‌شده مستقل گزارش شوند | **DEFERRED**؛ AI/resources runtime خارج از scope |
| B39 | Student/Parent به منابع خصوصی/answers AI نرسند | **DEFERRED** برای AI؛ ACL فعلی Backend در regression حفظ شده، AI PASS ادعا نمی‌شود |
| B40 | context در تب دیگر funding را عوض نکند | **DEFERRED** برای AI runtime؛ payer فردی/سازمانی Backend و Commerce tests حفظ شده‌اند |
| B41 | quota تمام‌شده از اشتراک دیگر کم نکند | **DEFERRED** برای AI runtime؛ Commerce موجود تغییر نکرده است |
| B42 | revoke، citations و stream بعدی AI را قطع کند | **DEFERRED**؛ AI streaming اجرا نشده است |
| B43 | عکس/PDF پاسخ آزمون با lease/revision/deadline | regression `AcademicExamAttachmentOnlyReloadFinalizeAndTeacherDownloadsExactBytes`، security و migration tests |
| B44 | roster بدون فعالیت و تمام صفحات | regression `SchoolRostersExistBeforeSessionsAndEndingOneTeacherPreservesOtherTeacherAndRole` و آزمون‌های paging/authorization |
| B45 | نتیجهٔ منتشرنشده و evaluator note پنهان بمانند | suite AssignmentEvaluation/ExamGrading، Guardian و GradeRelease؛ Card هیچ نتیجهٔ خصوصی ندارد |

اجرای واقعی Frontend، Production و Push دستگاه در تمام ردیف‌ها خارج از این پذیرش است؛ هیچ ردیف UI با fixture به‌عنوان PASS واقعی کلاینت معرفی نشده است.

## ۱۳. نکات آموزشی و نتیجهٔ بازبینی

1. پنهان‌کردن navigation state با حذف Account permission تفاوت دارد؛ projection موقت، mutation نشست نیست.
2. source binding و FK/unique index از اعتماد به title یا JSON کلاینت قوی‌ترند.
3. materialized participant جایگزین current-state authorization نیست؛ grantهای منبع در Read/Send/Delivery دوباره بررسی می‌شوند.
4. Outbox transport تکرارشونده است؛ رسید مستقل هر مقصد و unique constraint اثر کسب‌وکاری را idempotent می‌کنند.
5. ترتیب قفل باید با ماژول‌های مشترک هماهنگ باشد. پذیرش Kestrel، deadlock واقعی `Organization→Class` در برابر `Class→Organization` را نشان داد؛ worker جدید با ترتیب Academic/Quota هماهنگ شد و تست SQL هم‌زمانی برای آن اضافه شد. Commerce policy تغییر نکرد.
6. چند SELECT جدا ممکن است نمای ترکیبی بسازند؛ Ready صرف intent بدون binding معتبر نیست. پاسخ Pair و تست invariant برای این race اصلاح شدند.
7. تست مدل جاری نباید قبل از Migration به schema قدیمی ستون‌های جدید تحمیل کند؛ fixture قدیمی با ستون‌های همان schema درج می‌شود.

محدودیت عملیاتی: planner فعلی برای Audit داخلی سازمان مشخص مناسب است؛ backfill پرحجم سرور، ظرفیت‌سنجی، workflow تصمیم‌های باز، Human badge، Teacher Notes و AI ingest نیازمند مرحلهٔ مستقل‌اند. هیچ تخمین تجاری، سن یا دسترسی Parent اضافه نشده است.

## ۱۴. گزارش توسعه‌دهندهٔ پایان مرحله

۱. **Branch / HEAD:** مبنای شروع و پایان `main / 9d70fa94eebcd59555e2872cb5fc921802583d27` است. درخواست صریح این مرحله Commit/Push را ممنوع کرده؛ HEAD عمداً جلو نمی‌رود.

۲. **وضعیت اولیه:** هیچ تغییر tracked یا staged وجود نداشت؛ فقط سه فایل untracked قدیمی AI موجود بودند. manifest اولیه، hash فایل‌ها و نسخهٔ خصوصی هفت فایل محافظت‌شده در evidence همین مرحله نگه داشته شدند.

۳. **معماری:** .NET 9، EF Core 9، SQL Server و Onion/Modular Monolith حفظ شدند. مدل و state در Domain، use case/port در Application، SQL/worker در Infrastructure و transport در Api است.

۴. **مانع‌های شخصی یافت‌شده:** Selected workspace در Direct/Saved/Message operations، Group management، Poll/Checklist/Topic، Schedule، Folder/Labels، Typing، file access و feed شخصی اثر داشت. بخش‌های account-scoped قبلی Profile/Contact/Commerce بازنویسی نشدند.

۵. **علت P0:** navigation انتخاب‌شده به‌جای بافت واقعی یک عملیات شخصی مصرف می‌شد. حذف شرط از همهٔ Authorizationها راه‌حل انتخابی نبود؛ تشخیص مثبت حساب/نشست/منبع انتخاب شد.

۶. **پیاده‌سازی P0:** Resolver مرکزی برای هر عملیات واقعی Personal بافت موقت می‌سازد؛ Read/Send/Safety موجود همچنان اجرا می‌شود. شرط‌های Organization از Storeها حذف نشدند و Session ذخیره‌شده تغییر نمی‌کند.

۷. **Workspace contract:** snapshot جدید با version، states، canEnter و flag سطح حساب به Controller موجود اضافه شد. routeهای قدیمی حذف نشدند. flag سطح حساب در اقلام لیست نیز مقدار سازگار دارد.

۸. **نقش مؤثر:** Active Organization/Membership/Role بدون EndedAt؛ Pending مؤثر نیست. پایان آخرین نقش کارت را برمی‌گرداند؛ تعلیق مبهم با nullable visibility و تصمیم باز مشخص است.

۹. **Audit مسیر رسمی قدیمی:** route کلاس قبلاً یک Group دارای `AcademicClassId` و قفل کلاس می‌ساخت. همان Group reuse می‌شود؛ query قدیمی اکنون Type=Group دارد تا وجود Channel باعث خطای Single نشود.

۱۰. **Domain رسمی:** `OfficialSpaceProvision` intent و تلاش؛ `AcademicActivityPublication` منبع و نسخه؛ `AcademicActivityDelivery` رسید هر مقصد. `Conversation` همان aggregate موجود است و flag/binding سروری گرفته است.

۱۱. **دیتابیس:** سه جدول `messaging`، سه ستون افزوده، FKهای source/destination/actor، PK مرکب Delivery، unique indexهای purpose و نسخه و rowversion اضافه شده‌اند. Migration جدید دادهٔ قدیمی را حذف نمی‌کند.

۱۲. **Provisioning:** source و intent در یک transaction ثبت و ساخت گفتگو پس از Commit به Outbox موجود سپرده می‌شود. rollback آزمایشی منبع، intent را هم rollback کرد؛ هیچ شبکهٔ طولانی داخل آن transaction نیست.

۱۳. **Idempotency:** transport exactly-once فرض نشده؛ unique index و نتیجهٔ immutable در هر مقصد اثر تکراری را حذف می‌کنند. Source version و Message identity مستقل از retry شبکه‌اند.

۱۴. **Repair:** مقصد موجود حفظ و مقصد غایب تکمیل می‌شود؛ ابهام به ManualDecision می‌رود. GET، Repair انجام نمی‌دهد و Ready بدون binding مشاهده‌شده برنمی‌گرداند.

۱۵. **Organization spaces:** Group و Channel جدا، backend-owned و فعلاً فقط audience معتبر Manager همان سازمان دارند. مخاطب گستردهٔ سازمان تصمیم باز است.

۱۶. **Class spaces:** Group و Channel مستقل به همان Class/Org متصل‌اند. Teacher منتسب و Student دارای Enrollment معتبر در کنار Manager منبع، audience جاری‌اند.

۱۷. **علت‌های دسترسی:** `OfficialSpaceAccessQuery` علت ManagerRole/TeacherAssignment/Enrollment را از SQL جاری می‌سازد؛ grant موازی و کپی Role ساخته نشده. پایان یکی، علت معتبر دیگر را از بین نمی‌برد.

۱۸. **Manager:** فقط همان سازمان و source رسمی معتبر؛ Group/Channel مجاز را می‌خواند/ارسال می‌کند، اما private Direct دیگران را نمی‌بیند. مجوز ساخت/ویرایش Topic کلاس که قبلاً Teacher-only بود تغییر نکرد.

۱۹. **Teacher:** فقط assignment معتبر کلاس، current role/membership/account و org فعال؛ کلاس نامرتبط 404. تغییر scope شخصی، مجوز School را گسترش نمی‌دهد.

۲۰. **Student:** Enrollment معتبر، Group و Channel خواندنی و Group قابل ارسال؛ Channel publish ممنوع. پایان Enrollment، Read/Send/Realtime آینده را قطع می‌کند؛ پاسخ و فایل شخصی حذف نمی‌شوند.

۲۱. **Parent:** GuardianRelation مجوز خصوصی چت یا عضویت گستردهٔ کلاس نیست. تست Guardian واقعی، عدم دسترسی Group/Channel را اثبات می‌کند؛ سیاست نهایی Parent باز مانده است.

۲۲. **Metadata/badge:** `OfficialSpaceMetadata.From` فقط binding واقعی را می‌خواند. نام «Official» و فیلدهای تزریقی JSON نشان رسمی ایجاد نمی‌کنند؛ badge انسانی ساخته نشده است.

۲۳. **Card contract:** event/source/version/revision، class/org، destination/purpose، System actor و initiator، UTC/timing/title و state/actionability. بدنهٔ آموزشی، answers، key، attachment خصوصی و grade در projection نیستند.

۲۴. **Lesson:** `ClassLessonService.PublishAsync` پس از transition معتبر و پیش از Save/Commit، publication ثبت می‌کند. Retry نسخهٔ منتشرشده رویداد دوم تولید نمی‌کند؛ درس قدیمی در backfill بازپخش نمی‌شود.

۲۵. **Assignment:** `AssignmentService.PublishAsync` publication همان AssignmentVersion و موعد آن را ثبت می‌کند؛ recipient/notification قبلی حفظ شده‌اند. Draft پاسخ، upload و attach خصوصی Student هیچ publication تازه نمی‌سازند.

۲۶. **Exam:** `ExamService.PublishAsync` publication همان ExamVersion و زمان‌بندی مجاز را ثبت می‌کند. سؤال/گزینه/کلید در Card نیستند؛ Autosave و WriteLease در همان مسیر خصوصی موجود مانده‌اند. نسخهٔ قدیمی پس از نسخهٔ جدید Superseded می‌شود.

۲۷. **Outbox:** `OfficialSpacesWorkHandler` در `OutboxProcessor` موجود وصل شده؛ claim/lease/retry/recovery قبلی reuse شده‌اند. delivery دو مقصد جدا commit می‌شود تا موفقیت یکی با خرابی دیگری از بین نرود.

۲۸. **Realtime:** account/session stream و Conversation stream موجود حفظ شده‌اند. access invalidation برای refetch inbox، MessageCreated برای preview/order/unread و cursor/replay/resync برای reconnect استفاده می‌شوند؛ هیچ Hub موازی ساخته نشده است.

۲۹. **Dedupe:** دو worker و lost ACK با SQL واقعی آزمایش شدند؛ تعداد Message و ChangeEvent ثابت ماند. خواندن Group، unread یا mute Channel را تغییر نمی‌دهد.

۳۰. **Legacy/backfill:** planner خواندنی، binding نوع‌دار، حفظ history و عدم scan انتشار قدیمی. اجرای واقعی backfill سرور انجام نشده است؛ فقط fixture ایزوله و dry-run آزموده شدند.

۳۱. **Migration:** زنجیرهٔ صفر→۵۶ و ارتقای ۵۵→۵۶، هرکدام دوبار روی DB تازه PASS؛ گروه قدیمی مصنوعی حفظ و Card تاریخی صفر ماند. Down پس از استفاده با SQL 51131 و تست واقعی متوقف می‌شود. هیچ SQL روی دیتابیس‌های عادی یا سرور اجرا نشده است.

۳۲. **OpenAPI:** Controllerها و DTOهای افزوده از OpenAPI واقعی میزبان قابل مشاهده‌اند؛ schema و route فرضیِ بدون کد در قرارداد نهایی معرفی نشده‌اند. هیچ debug endpoint ساخته نشده است.

۳۳. **Frontend handoff:** نمایش کارت، operation scope، pair states، capabilities، source metadata، Card، خطا و Realtime در سند مجزا آمده‌اند. Frontend تغییر نکرده و رفتار واقعی تب/cache/UI ادعا نمی‌شود.

۳۴. **B01–B45:** ماتریس تفکیک‌شده در بخش ۱۲ است. B37–B42 صریحاً Deferred؛ policyهای باز با پیاده‌سازی حدسی جایگزین نشده‌اند.

۳۵. **Unit:** `954` تست نهایی؛ Domain، visibility و Resolver موقت را پوشش می‌دهند.

۳۶. **Integration:** `1583` تست نهایی؛ Host/API، SQL واقعی، migration، concurrency، ACL، Outbox و SignalR را پوشش می‌دهند. تست lock-order، transaction واقعی Academic/Commercial و worker هم‌زمان را اجرا می‌کند.

۳۷. **Architecture:** `14` تست نهایی؛ جهت وابستگی Onion حفظ شده است.

۳۸. **Failed/Skipped:** نتیجهٔ نهایی `0 / 0`؛ جمع تست‌های نهایی `2551`. نخستین regression شش fixture/انتظار قدیمی را پیدا کرد؛ اصلاح و دوباره اجرا شدند، هیچ تستی غیرفعال نشد.

۳۹. **Restore/Build/Warnings:** Restore و Release build واقعی اجرا شدند؛ نتیجهٔ نهایی `PASS`، warning/error نهایی `0/0`. Build میانی یک enum فاقد qualification و یک هم‌زمانی نامناسب build/test در Windows داشت که اصلاح شدند؛ نتیجهٔ نهایی جای آن‌ها را به‌عنوان تاریخچه پنهان نمی‌کند.

۴۰. **E2E:** `80` درخواست HTTP محلی روی Kestrel با دو نشست مستقل، WebSocket واقعی، first message بدون Subscribe دستی Conversation، offline replay و retry ثبت شد. منابع و حساب‌ها ساختگی، SQL تازه، OTP Mock محیط Test، SMS/Push/Payment خارجی و AI غیرفعال بودند. تعداد درخواست‌های helper نیز در شمارش لحاظ شده؛ WebSocket frame درخواست HTTP شمرده نشده است.

۴۱. **Diff:** `64 files changed, 815 insertions(+), 220 deletions(-)`؛ فایل‌های جدید همین task نیز جدا از diff tracked در manifest بازبینی شدند. فایل‌های generated EF جزو تغییر اسکیمای همین مرحله‌اند؛ `bin/obj/.artifacts` وارد index نشده‌اند.

۴۲. **Git نهایی:** همهٔ تغییرات این مرحله عمداً unstaged/uncommitted هستند. سه AI untracked قبلی و چهار appsettings خصوصی با hash اولیه یکسان‌اند؛ هیچ فایل baseline حذف نشده است. `COMMIT=false / PUSH=false` طبق درخواست، نه به دلیل شکست Git. branch و HEAD همان مبناست.

۴۳. **پذیرش:** مقادیر واقعی در بخش بعد ثبت می‌شوند. موفقیت Backend به معنی استقرار، پذیرش Frontend یا دریافت Push واقعی نیست.

۴۴. **تصمیم‌های باز:** هر ۱۲ تصمیم بخش ۱۰ محفوظ‌اند. قدم بعد، بازبینی همین diff و انتخاب workflow محصولی Parent/history/notification و سپس مجوز مستقل Commit/Publish/Server migration است؛ AI runtime، Teacher Notes و Human badge نباید در آن ادغام شوند.

## ۱۵. نتیجهٔ نهایی Validation و پذیرش

مقادیر زیر نتیجهٔ سناریوهای Backend/SQL ایزوله و میزبان Kestrel لوکال این مرحله‌اند؛ صفرهای leakage به payloadها و سناریوهای اجراشده اشاره دارند و ادعای اثبات امنیت تمام حالت‌های ممکن نیستند. پذیرش واقعی Frontend، سرور Production و Push دستگاه اجرا نشده‌اند.

| اعتبارسنجی | نتیجه |
| --- | --- |
| Restore / Release build | موفق؛ warning=0 و error=0 |
| Architecture | 14 موفق؛ شکست=0 و ردشده=0 |
| Unit | 954 موفق؛ شکست=0 و ردشده=0 |
| Integration | 1583 موفق؛ شکست=0 و ردشده=0 |
| مجموع | 2551 موفق |
| Kestrel لوکال / HTTP / WebSocket | 80 درخواست؛ دو نشست مستقل؛ موفق |
| SQL کامل و ارتقای 55→56 | هر دو دوبار موفق؛ دیتابیس‌های ایزولهٔ همان اجرا حذف شدند |
| تغییر مدلِ بدون Migration | وجود ندارد |
| Git | شاخهٔ main؛ HEAD=9d70fa94eebcd59555e2872cb5fc921802583d27؛ index خالی |

```text
PERSONAL_VISIBILITY_RULE_IMPLEMENTED=true
PENDING_ROLE_NOT_EFFECTIVE=true
ENDED_LAST_ROLE_RESTORES_PERSONAL_CARD=true
PERSONAL_PROFILE_WITH_ROLE_PASS=true
PERSONAL_CONTACTS_WITH_ROLE_PASS=true
PERSONAL_DIRECT_WITH_ROLE_PASS=true
PERSONAL_MESSAGING_WITH_ROLE_PASS=true
INDIVIDUAL_SUBSCRIPTION_WITH_ROLE_PASS=true
MULTI_ROLE_ISOLATION_PASS=true
CROSS_ORG_CONTEXT_ISOLATION_PASS=true
ORGANIZATION_OFFICIAL_GROUP_IMPLEMENTED=true
ORGANIZATION_OFFICIAL_CHANNEL_IMPLEMENTED=true
CLASS_OFFICIAL_GROUP_IMPLEMENTED=true
CLASS_OFFICIAL_CHANNEL_IMPLEMENTED=true
OFFICIAL_BINDING_UNIQUE=true
PROVISIONING_IDEMPOTENT=true
PARTIAL_REPAIR_PASS=true
READ_API_NO_SIDE_EFFECT=true
OFFICIAL_BADGE_SERVER_BACKED=true
CLIENT_CANNOT_SPOOF_OFFICIAL=true
TEACHER_ASSIGNMENT_SYNC_PASS=true
STUDENT_ENROLLMENT_SYNC_PASS=true
MANAGER_ACCESS_PASS=true
PARENT_BROAD_ACCESS_NOT_GRANTED=true
MULTIPLE_ACCESS_REASON_PASS=true
REVOCATION_PASS=true
LESSON_ACTIVITY_GROUP_PASS=true
LESSON_ACTIVITY_CHANNEL_PASS=true
ASSIGNMENT_ACTIVITY_GROUP_PASS=true
ASSIGNMENT_ACTIVITY_CHANNEL_PASS=true
EXAM_ACTIVITY_GROUP_PASS=true
EXAM_ACTIVITY_CHANNEL_PASS=true
ACTIVITY_IDEMPOTENCY_PASS=true
TWO_WORKER_DEDUPE_PASS=true
PARTIAL_DELIVERY_REPAIR_PASS=true
OLD_EVENT_DOES_NOT_ROLLBACK=true
REALTIME_REGRESSION_PASS=true
INBOX_OFFICIAL_SPACE_UPDATE_PASS=true
LEGACY_DRY_RUN_PASS=true
BACKFILL_NO_DUPLICATE_PASS=true
BACKFILL_NO_HISTORICAL_SPAM_PASS=true
MIGRATION_SAFE=true
BUILD_PASS=true
FULL_TEST_SUITE_PASS=true
DRAFT_PUBLICATION_COUNT=0
PRIVATE_STUDENT_DATA_LEAK=0
ANSWER_KEY_LEAK=0
PENDING_MODEL_CHANGES=false
AI_RUNTIME_ENABLED=false
COMMIT=false
PUSH=false
PUBLISH=false
DEPLOY=false
SERVER_MIGRATION=false
```

Evidence محلی در `.artifacts/official-spaces-20261030-d637b7ce` نگه داشته شده است: TRXهای نهایی، SQL کامل/ارتقا و نتیجهٔ اجرای دوباره، manifest/hashهای اولیه و نهایی، diff بازبینی‌شده و metadata امن HTTP/WebSocket. Secret/JWT/Authorization header یا پاسخ خصوصی در evidence امن runtime ذخیره نشده است. backup خصوصی تنظیمات از این evidence قابل اشتراک جداست و نباید منتشر شود.

تمام تغییرات این مرحله در working tree باقی مانده‌اند تا مالک پروژه آن‌ها را بازبینی کند. مطابق درخواست، هیچ Commit، Push، Publish، Deploy یا Migration روی دیتابیس‌های اصلی/سرور انجام نشده است.
