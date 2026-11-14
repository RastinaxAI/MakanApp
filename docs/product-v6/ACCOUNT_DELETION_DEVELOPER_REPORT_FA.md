# گزارش توسعه‌دهندهٔ حذف حساب — BE-22

تاریخ `2026-11-14`؛ مبنا `f8271d31027c6af8ffb44a299f857eb931e18113`؛ کد و پذیرش `a639319b70d7c07db3588cd91d674aed339c1756`؛ شاخه `feature/product-v6-backend-alignment`. این گزارش دربارهٔ کد واقعی Backend است؛ اجرای حذف روی سرور یا حذف زندهٔ Knowledge در آن ادعا نمی‌شود. نتیجهٔ Git تحویل مستندات و تطبیق HEAD ریموت در گزارش پایانی همین کار ارائه می‌شود.

## ۱. خلاصهٔ مرحله

قبلاً فقط رسید درخواست حذف وجود داشت؛ User فعال می‌ماند و حذف/ناشناس‌سازی اجرا نمی‌شد. اکنون state machine، هشت checkpoint، claim، recovery، اجرای بین‌ماژولی و ادامهٔ محدود توسط Owner اضافه شده است. هویت جاری خنثی، دسترسی‌ها بسته و دفترچهٔ خصوصی مالک پاک می‌شود؛ تاریخچهٔ مشترک، آموزشی، مالی و ممیزی باقی می‌ماند. حذف Personal Knowledge با تأیید قطعی و receipt مستقل انجام می‌شود.

اجرا همچنان پیش‌فرض خاموش است. `GracePeriodHours=null` جلوی حدس‌زدن مدت حقوقی را می‌گیرد و `true` بدون مقدار معتبر Startup را رد می‌کند.

## ۲. معماری

- **Domain:** `AccountDeletionRequest` وضعیت، claim، شکست و تکمیل را کنترل می‌کند؛ `AccountDeletionStep` checkpoint و `AccountDeletionKnowledgePurge` receipt هر نسخه را نگه می‌دارند. `User/Person/PhoneChangeRequest` رفتار ناشناس‌سازی/مسدودسازی را دارند و به EF/HTTP/AI وابسته نیستند.
- **Application:** `AccountDeletionService` ورودی/مجوز درخواست و ادامه را مدیریت می‌کند؛ `AccountDeletionWorkflow` ترتیب هشت مرحله را از طریق پورت داخلی هر ماژول هماهنگ می‌کند. هیچ SQL یا Provider در این لایه نیست.
- **Infrastructure:** پردازنده و lifecycle هر ماژول، EF/SQL Server، قفل، transaction، checkpoint و تماس Knowledge را اجرا می‌کنند. یک ماژول از قرارداد داخلی ماژول دیگر استفاده می‌کند؛ Controller یا Application مستقیماً جدول دیگر را تغییر نمی‌دهد.
- **Api:** Controller نازک، قرارداد HTTP و ProblemDetails؛ دو مسیر Detail و Retry افزوده شده‌اند.

Dependencyها همان `Domain <- Application <- Infrastructure` و `Api -> Application/Infrastructure` هستند. هیچ CQRS، MediatR، Generic Repository یا سرویس مستقل اضافه نشده است.

## ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت و نکتهٔ آموزشی |
|---|---|
| `MakanApp.Domain/Identity/AccountDeletionStep.cs` | PK منطقی Request/Step و حالت Pending/Processing/Completed/Failed؛ موفقیت مرحله یک رخداد پایدار است. |
| `MakanApp.Domain/Knowledge/AccountDeletionKnowledgePurge.cs` | command ثابت و نتیجهٔ قطعی هر Source/Version؛ CreatedBy مجوز حذف نیست. |
| `MakanApp.Application/Identity/AccountDeletionExecution.cs` | Options، خطاهای امن، context اجرای claim، پورت‌های lifecycle و workflow؛ orchestration از جزئیات SQL جداست. |
| `MakanApp.Infrastructure/Identity/AccountDeletionProcessor.cs` | schedule/claim/start/commit/fail/fence؛ تماس شبکه بیرون transaction و checkpoint مستقل برای recovery. |
| `MakanApp.Infrastructure/Identity/AccountDeletionDispatcher.cs` | Worker opt-in و scoped processing؛ خاموش‌بودن یعنی هیچ query اجرایی از این Worker. |
| `MakanApp.Infrastructure/Identity/IdentityAccountDeletionLifecycle.cs` | Freeze همهٔ نشست‌ها، رفع FK شماره، ناشناس‌سازی و lifecycle metadata عکس؛ بدون حذف bytes مستقیم. |
| `MakanApp.Infrastructure/Messaging/MessagingAccountDeletionLifecycle.cs` | ترتیب حذف FK دفترچهٔ مالک و unlink/version دادهٔ دیگران؛ بدون decrypt یا پاک‌کردن متن آنان. |
| `MakanApp.Infrastructure/Knowledge/AccountDeletionKnowledgePurger.cs` | اثبات DB مالک/claim/مرحله، archive منابع شخصی، delete با client موجود و ثبت ack معتبر. |
| `MakanApp.Infrastructure/{Organization,Academic,Guardian,Platform,Commerce,Notification,Ai}/*AccountDeletionLifecycle.cs` | هر ماژول مالک تغییرات خودش است؛ guard، پایان دسترسی یا حفظ تاریخچه متناسب با همان حوزه اجرا می‌شود. |
| `MakanApp.Infrastructure/Persistence/Configurations/Identity/AccountDeletionStepConfiguration.cs` و `Configurations/Knowledge/AccountDeletionKnowledgePurgeConfiguration.cs` | FK/PK/Unique/Check/rowversion؛ منطق SQL NULL نیز باید در Constraint لحاظ شود. |
| `tests/MakanApp.UnitTests/Identity/AccountDeletionStateTests.cs` | آزمون pure Domain/Application برای زمان، حالت، claim، هویت و مالکیت منبع. |
| `tests/MakanApp.IntegrationTests/AccountDeletionAcceptanceTests*.cs` | HTTP، SQL واقعیِ موقت، شکست/retry، دادهٔ مشترک و fake Knowledge؛ بدون پیامک یا حذف زنده. |
| `tests/MakanApp.IntegrationTests/AccountDeletionMigrationAcceptanceTests.cs` و `AccountDeletionConfigurationTests.cs` | Fresh/Upgrade، constraintهای واقعی و رد Startup ناامن. |

سه سند عملیاتی/قراردادی: [Handoff](ACCOUNT_DELETION_FRONTEND_HANDOFF_FA.md)، [Runbook](../operations/ACCOUNT_DELETION_RUNBOOK_FA.md) و [ماتریس داده](ACCOUNT_DELETION_DATA_MATRIX_FA.md).

## ۴. فایل‌های مهم تغییرکرده

`AccountDeletionRequest.cs` از receipt به aggregate اجرای پایدار تبدیل شد. `User.cs` markerهای حذف، نام داخلی یکتا و منع Reactivate دارد؛ `Person.cs` نام/DOB را خنثی می‌کند. `PhoneChangeRequest.CredentialId` nullable و terminal-only شد تا حذف Credential با تاریخچهٔ معتبر سازگار باشد.

`IdentityService.cs` برای OTP حساب موجود، `GetUserForUpdateAsync` را استفاده می‌کند؛ درنتیجه User از قبل trackشده نمی‌تواند وضعیت frozen تازه را پنهان کند. تولید/اعتبارسنجی OTP و provider بازطراحی نشده‌اند. کد/تنظیم MeliPayamak تغییر نکرده‌اند.

`EfMessagingStore.Eligibility/Advanced.Reads/Safety` نمایش تاریخی حسابِ واردشده به حذف را خنثی می‌کنند؛ Sender/Mention/Block target با lookup صرفاً Active دیگر کل تاریخچه را404 نمی‌کند. مسیر Create/Send/Discovery همچنان guard مستقل خود را دارد. نمایش batch نیز از لحظهٔ freeze نام/عکس قدیمی را برنمی‌گرداند.

`EfAccountDeletionStore` receipt idempotent، صف/Detail/Retry نسخه‌دار و Audit را دارد. `PlatformCatalog` و seedها permission Manage را فقط به Owner می‌دهند. `EfPlatformOperationsStore` Reactivate حساب حذف‌شونده را با خطای امن رد می‌کند. DI، telemetry و health محافظت‌شده Worker جدید را می‌شناسند. فقط بخش `AccountDeletion` به base settings و دو `.example` اضافه شده؛ سایر مقادیر پایه و تمام فایل‌های خصوصی حفظ شده‌اند.

## ۵. جریان واقعی اجرا

```text
HTTP POST /api/v1/me/deletion-request
-> AccountDeletionController
-> AccountDeletionService
-> EfAccountDeletionStore + AccountDeletionRequest/AccountDeletionStep
-> SQL Server: receipt + audit + schedule صریح
-> HTTP 202

AccountDeletionDispatcher
-> AccountDeletionProcessor: claim/start checkpoint
-> AccountDeletionWorkflow
-> lifecycle داخلی هر ماژول + Domain rules
-> SQL کوتاه + checkpoint اتمیک
-> AccountDeletionKnowledgePurger: prepare SQL / DeleteAsync شبکه / ack SQL
-> Finalize: تأیید همهٔ invariantها و هشت مرحله
-> SQL Completed + audit
```

پس از freeze، نشست درخواست‌کننده هم لغو می‌شود؛ HTTP401 بعدی اثبات Completed نیست. Owner وضعیت واقعی را از Detail می‌خواند.

## ۶. دیتابیس

Migration افزایشی `20261114140323_AddAccountDeletionLifecycle` دو جدول جدید دارد: `identity.AccountDeletionSteps` با PK مرکب RequestId/Step و `knowledge.AccountDeletionKnowledgePurges` با PK RequestId/SourceId/Version. command هر نسخه unique است؛ FK مرکب RequestId/OwnerUserId به Request و SourceId/Version به نسخهٔ منبع، تعلق receipt را ثابت می‌کند.

Request دارای Status، PolicyVersion، زمان‌های schedule/start/completion/attempt، تعداد تلاش، خطای امن، claim token/lease و Index صف است؛ rowversion قبلی حفظ شد. Requestهای قبلی Requested و هشت مرحلهٔ خالی می‌گیرند، نه Completed. دو marker nullable به Users و nullable شدن CredentialId با CHECK terminal افزوده شدند. CHECKها Status، claim pair، زمان تکمیل، نتیجهٔ قطعی Knowledge و ناسازگاری Reactivate/Anonymized را محافظت می‌کنند.

شروع/تلاش مرحله پیش از کار commit می‌شود؛ SQL عمل و checkpoint موفق یک transaction دارند. شبکه داخل SQL نیست. Down پس از شروع حذف/جداشدن Credential محافظ دارد؛ ناشناس‌سازی rollback هویت نیست. هیچ Migration روی MakanApp، MakanApp_Test یا سرور اجرا نشده است. مدل جاری۱۵۱ جدول/۷۴ Migration دارد؛ این عدد، schema واقعی سرور را اثبات نمی‌کند.

## ۷. Endpointها

دو مسیر User موجود باقی‌اند: POST با `clientOperationId` و202؛ GET فقط برای حساب نشست و200/204. سه مسیر Admin شامل Page، Detail و POST Retry هستند. Retry با `clientOperationId/expectedVersion` فقط برای NeedsAttention، با Read+Manage و بررسی مجدد مجوز/نشست در transaction کار می‌کند. خطاهای مهم400/401/403/404/409 و کدهای امن در [Handoff](ACCOUNT_DELETION_FRONTEND_HANDOFF_FA.md) آمده‌اند. Force Complete و public status-by-id وجود ندارند.

## ۸. مفاهیم فنی استفاده‌شده

Claim lease از اجرای هم‌زمان معتبر جلوگیری می‌کند؛ token و rowversion اجازهٔ ack Worker منقضی را نمی‌دهند. durable checkpoint مرز اجرای دوباره را مشخص می‌کند. idempotency به معنی نبودِ اثر تجاری تکراری است، نه وعدهٔ exactly-once شبکه. قفل والدهای دفترچه و افزایش نسخهٔ Owner جلوی lost update و cursor بدون تغییر را می‌گیرند. متن دیگران بدون decrypt همان‌طور حفظ می‌شود.

## ۹. تصمیم‌های طراحی

Hard Delete ریشه کنار گذاشته شد چون FKهای مالی/آموزشی/پیام به آن متکی‌اند. root retained و هویت خنثی هم تاریخچه را نگه می‌دارد و هم حساب جدیدِ همان شماره را از آن جدا می‌کند. نام داخلی با encoding تمام۱۲۸ بیت UserId ساخته و پیشوندش در username عمومی غیرمجاز است؛ نام ثابت مشترک، unique index را می‌شکست.

شبکه و SQL یک transaction توزیع‌شده ندارند. receipt مستقل هر نسخه، command ثابت و ack fenced جای آن را می‌گیرند. در قطع بعد از اثر خارجی، درخواست شبکه ممکن است تکرار شود؛ contract delete باید idempotent بماند. پاسخ Superseded یا صرف NotRequested محلی، نبود vector را ثابت نمی‌کند.

مسئولیت مدیر/Owner به‌جای جانشین صوری، NeedsAttention می‌گیرد. End membership با حذف Enrollment یا اعطای نقش جدید یکی نیست. هیچ refund، merge Person، انتقال تاریخچه به شمارهٔ جدید یا دورزدن Safety ساخته نشد.

## ۱۰. تست‌ها

۱۹ Unit و۳۵ Integration تازه افزوده شده‌اند؛ جمع suite نسبت به مبنای۳۲۶۹، به۳۳۲۳ رسیده است. Unitها شامل اعتبار gate/grace، deadline/lease، fencing، هشت checkpoint یکتا، resume، ناشناس‌سازی idempotent، حفظ RootId، آزادشدن نام، مالکیت Personal و detach امن PhoneChange هستند.

Focused SQL شامل receipt هم‌زمان و cross-user، gate خاموش، grace با ساعت سرور، همهٔ نشست‌ها/Refresh/Push و OTP قبلی، شماره/username تازه، claim هم‌زمان/منقضی، دفترچهٔ شش‌جدولی و Delta دیگران، پیام/Block/Support/Moderation، عضویت/Guardian و Submission/Exam history، آخرین مدیر/Owner، عکس و bytes، منابع چهار scope، Knowledge failure/multi-version/ack دیررس، permission/retry، پرداخت/Unknown usage، School Paid و Fresh/Upgrade است. double Knowledge دادهٔ واقعی را حذف نمی‌کند؛ SQL و transaction واقعی‌اند.

در آزمون رقابت دو Manager، `READPAST` ممکن است موقتاً claim دوم را ندهد؛ تست پس از پایان transactionهای claim یک بار دیگر همان صف را بررسی می‌کند و دو RequestId متفاوت می‌خواهد. پردازش Freeze همچنان هم‌زمان است و assertion نهاییِ یک Completed، یک NeedsAttention و دقیقاً یک حساب Active حفظ شده است. این رفتار با retry پیامک/شبکه یا افزایش Timeout آزمون فرق دارد.

تست Startup بدون grace رد می‌شود. تست SQL نشان می‌دهد Outcome=NULL برای receipt نهایی پذیرفته نیست؛ منطق سه‌ارزشی SQL با validation C# یکی نیست. آزمون‌های Architecture موجود همان مرز Onion را اجرا می‌کنند. هیچ تست حذف، Skip یا ضعیف نشده و Timeout جدیدی برای سبزکردن suite تعیین نشده است.

## ۱۱. Validation

`dotnet clean` و `dotnet restore MakanApp.sln` موفق بودند. خطاهای ابتدایی namespace/نام عضو/overload در تست‌های تازه و هشدار xUnit رفع شدند. Build نهایی `dotnet build MakanApp.sln --configuration Release --no-restore --nologo --verbosity minimal` موفق، با۰ Warning و۰ Error بود. focused کامل۳۵ SQL، دو بررسی نهایی constraint/recovery و شش بررسی مجدد نیز موفق بودند.

| دور کامل | Unit موفق | Integration موفق / کل | Architecture موفق | Failed | Skipped |
|---|---|---|---|---|---|
| اول | ۱۳۴۴ | ۱۹۶۰ / ۱۹۶۴ | ۱۴ | ۴ | ۰ |
| پس از اصلاح | ۱۳۴۴ | ۱۹۶۵ / ۱۹۶۵ | ۱۴ | ۰ | ۰ |
| نهایی، پس از Build نهایی | ۱۳۴۴ | ۱۹۶۵ / ۱۹۶۵ | ۱۴ | ۰ | ۰ |

دور اول چهار شکست داشت: سه Timeout Realtime و Seed ناسازگار User روی schema تاریخی School. Seed با `LegacyAcademicMigrationFixture.InsertAsync` اصلاح شد، بدون تغییر assertion یا schema تاریخی. سه Timeout با همان محافظ/زمان قبلی در اجرای جدا و دو suite کامل بعدی تکرار نشدند؛ علت قطعی آن‌ها اثبات نشده و ادعای اصلاح Realtime نداریم. Full دوم و نهایی هرکدام۳۳۲۳ PASS دارند؛ هر۱۱۴ تست Zibal، شامل۶۸ Unit و۴۶ Integration، موفق‌اند.

فرمان نهایی `dotnet test MakanApp.sln --configuration Release --no-build --no-restore --nologo --logger 'trx;LogFilePrefix=be22-final' --results-directory .artifacts/be22-deletion-20261114-f8271d3/final --verbosity minimal` با exit code0 پایان یافت. شمارنده‌های سه TRX به‌صورت مستقل کنترل شدند؛ شواهد و SHAها در `final-validation.json` همان پوشهٔ artifact والد هستند. زمان ثبت TRX نهایی Integration برابر `2026-11-14 20:28:10 +03:30` و مدت Integration۲۸دقیقه و۴۱ثانیه بود. هیچ Retry/Timeout تازه یا حذف/Skip تستی برای این نتیجه اضافه نشد.

Snapshot بدون SQL/Worker/SMS/AI:۴۳۸ مسیر،۴۹۴ operation،۹۱ Controller،۷۸۳ schema،۱۵۱ جدول،۷۴ Migration. Fresh/Upgrade روی LocalDB GUID موقت موفق است؛ normal database migration، Deploy و Live Knowledge Delete اجرا نشده‌اند. تلاش اولیهٔ EF با Api به‌علت نداشتن Design package مناسب نبود؛ Migration با Design-time Factory موجود Infrastructure تولید شد و Package/Framework تغییر نکرد.

OpenAPI همین Source Commit در `.artifacts/be22-deletion-20261114-f8271d3/snapshot-final/openapi.json` بازتولید شد. `SHA256=62CF7BB5084CFB5DB2B0A42788AAAA52D88B9CEF02920E5149262AD85013CB04`. مدل معلق EF وجود ندارد؛ این پذیرش، applied schema محیط واقعی را اثبات نمی‌کند.

۱۳ تست ابزار مستندسازی نیز موفق‌اند و جدا از۳۳۲۳ تست محصول شمرده می‌شوند. کنترل اسناد۴۹۴ operation،۷۸۳ schema،۱۵۱ جدول،۷۴ Migration و۲۰۴ ورودی inventory را تأیید کرد؛ لینک شکسته و اختلاف Enum/OpenAPI صفر. HTML/PDFهای Guide/Reference/Database/Frontend به‌ترتیب۷۹/۷۹۵/۲۳۹/۸۰۳ صفحه دارند؛ bookmark، SHA متناظر HTML، نبود صفحهٔ خالی و نبود متن بیرون صفحه کنترل شدند. چهار سند تازهٔ BE-22 نیز جداگانه برای لینک بررسی شدند. خروجی HTML/PDF دیتابیس طبق ignore موجود محلی است؛ سه کتاب دیگر tracked هستند. هیچ Frontend code تغییر نکرد.

## ۱۲. Git

Commit پیاده‌سازی `a639319b70d7c07db3588cd91d674aed339c1756` با پیام `feat(privacy): implement recoverable account deletion lifecycle`، شامل۵۷ فایل کد/تنظیم نمونه/Migration/تست است؛۲۷ فایل تازه و۳۰ فایل ویرایش‌شده. ۱۲۵۷۷ خط Designer بخشی از snapshot کاملِ معمول EF هستند، نه ایجاد ماژول‌های آینده. مستندات و خروجی‌های تولیدشده در Commit جدا ثبت می‌شوند تا مبنای Source کد با Commit تحویل سند اشتباه نشود.

شاخه `feature/product-v6-backend-alignment`، remote برابر `origin` و upstream برابر `origin/feature/product-v6-backend-alignment` است. HEAD و upstream پیش از تغییر و پس از fetch اولیه هر دو f8271d3 بودند. Push و fetch/تطبیق HEAD نهایی، پس از Commit مستندات انجام و نتیجهٔ واقعی در گزارش نهایی Git همین تحویل اعلام می‌شود؛ این پاراگراف ادعای Pushِ انجام‌نشده نیست.

سه فایل untracked قبلی AI حفظ‌اند: `tools/ai/provider_diagnostic.py`, `run_exact_ai.py`, `test_provider_diagnostic.py`. Backup/هش۱۰ فایل تأیید شد؛۹ فایل محافظت‌شده byte-identical و تنها تفاوت مجاز base JSON همان AccountDeletion است. ۱۱۱۷ فایل Runtime پس از رگرسیون نهایی با snapshot کد byte-identical بودند. فایل‌های خصوصی Development/Test/Production و سه فایل MeliPayamak تغییری ندارند؛ هیچ AI ناتمام در Commit نیست. merge به main و Deploy اجرا نمی‌شوند.

## ۱۳. محدودیت‌های فعلی

Production execution عمداً `BLOCKED_CONFIGURATION` است؛ مهلت/retention رسمی و legal hold/Backup همچنان نیازمند تنظیم/تصمیم رسمی‌اند. این مرز configuration به معنی نبود implementation نیست. Live Knowledge Delete برابر NOT_RUN و ذخیره/پاک‌سازی داخلی upstream/provider خارج از پذیرش محلی است. Data-action semantics مصوب اجرا شده‌اند؛ BE24 کل محصول به‌خاطر دیگر تصمیم‌ها/پذیرش‌های مستقل، PASS فرض نمی‌شود.

پروندهٔ Paid یا مدیر/مالک بدون انتقال مسئولیت به NeedsAttention می‌رود. داده‌های تاریخی و Backupها purge عمومی ندارند. حذف حساب، رشته‌های نام‌دار در تمام محتوای قدیمی کاربر را redact نمی‌کند. حساب تازه با همان شماره هیچ حق خودکاری به دادهٔ قدیمی ندارد.

## ۱۴. نکات آموزشی

1. از Actor/Session موجود، مالک منبع را در SQL اثبات کنید؛ بعد از revoke نشست، نشست جعلی راه‌حل نیست.
2. وضعیت Completed باید نتیجهٔ invariantها و checkpointها باشد، نه نتیجهٔ رسیدن Worker به آخر یک حلقه.
3. claim token بدون بررسی هنگام ack کافی نیست؛ Worker قدیمی ممکن است بعد از timeout هنوز پاسخ بدهد.
4. دفترچهٔ دیگری متعلق به اوست؛ حذف لینک حساب با حذف SavedName/Phone تفاوت دارد و Delta به افزایش نسخه نیاز دارد.
5. دادهٔ امنیتی/مالی/آموزشی را از entitlement جاری جدا نگه دارید؛ root retained و End رابطه جلوی FK شکسته را می‌گیرند.
6. SQL CHECK با NULL رفتار متفاوت دارد؛ شرط non-null صریح و آزمون SQL واقعی لازم است.
7. بازخوانیِ هویت تاریخی با مجوز Send/Discovery یکی نیست؛ خنثی‌سازی محدود، guardهای جاری را باز نمی‌کند.

## ۱۵. مسیر مطالعهٔ کد

`AccountDeletionController` → `AccountDeletionService` → `AccountDeletionRequest/Step` و `User.Anonymize` → پورت‌ها و `AccountDeletionWorkflow` → `EfAccountDeletionStore` → `AccountDeletionProcessor` → lifecycle Identity/Messaging/Knowledge و سایر ماژول‌ها → EF Configuration → Migration → `AccountDeletionStateTests` و `AccountDeletionAcceptanceTests` → Runbook.

## ۱۶. مرحلهٔ بعد

در کار عملیاتی مستقل، مهلت مصوب، Backup/Restore reconciliation، انتقال مسئولیت‌های باقی‌مانده و readiness مسیر Knowledge تعیین و سپس روی دادهٔ ساختگیِ مجاز اجرای محیط واقعی پذیرفته شود. فعال‌سازی Production، حذف واقعی منابع، اجرای Migration عادی یا purge حقوقی را با این Commit مخلوط نکنید.
