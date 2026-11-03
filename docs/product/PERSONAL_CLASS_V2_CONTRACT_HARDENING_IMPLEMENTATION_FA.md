# گزارش پیاده‌سازی و یادگیری — PersonalClass v2 Contract Hardening

## ۱. خلاصه

مبنای این مرحله `main` در `8ed3fad6ea464ad02c90fecb295eda2606abca64` بود. Core و Academic از قبل وجود داشتند؛ این مرحله قرارداد مصرف، صفحه‌بندی صف‌ها، Audit آموزشی و history امن را کامل می‌کند. schema کسب‌وکاری یا قابلیت آموزشی موازی ساخته نشده است.

در OpenAPI واقعی مبنا، 426 operation بدون operationId و scheme قابل مصرف و هفت پاسخ موفق بدون content دیده شد. اکنون 433 operation، شناسه‌های یکتا، Bearer واقعی Opaque، metadata مجوز Platform، خطای typed و محدودیت ورودی وجود دارند. رویدادهای فعلی با نام واقعی و قواعد replay/refetch مستند شده‌اند.

## ۲. معماری و dependency

- **Domain:** بدون تغییر؛ invariants، Entityها و EF model قبلی حفظ شدند.
- **Application:** قرارداد صفحه‌بندی، use case خواندن history و port کوچک `IAcademicAuditWriter`؛ هیچ EF/HTTP dependency اضافه نشد.
- **Infrastructure:** queryهای SQL دارای Count/Skip/Take و projection امن history؛ writer Audit از `AdminAuditLog` و transaction جاری استفاده می‌کند.
- **Api:** metadata OpenAPI، نگاشت HTTP و خطای 401/403، دو Controller نازک history و پنج route صفحه‌بندی additive.

```text
HTTP Request
→ PersonalClassHistoryController / PlatformPersonalClassHistoryController
→ PersonalClassHistoryService
→ CommercialActorAccess + IPlatformPermissionResolver
→ IPersonalClassHistoryStore
→ EfPersonalClassHistoryStore
→ MakanDbContext / SQL Server
→ PersonalClassPage<PersonalClassHistoryItem>
→ HTTP 200
```

مسیر نوشتن Audit آموزشی:

```text
AssignmentsController / EvaluationsController / ExamsController / ExamGradingsController
→ AssignmentService / EvaluationService / ExamService / ExamGradingService
→ قواعد Domain + authorization جاری + transaction موجود
→ IAcademicAuditWriter.Record
→ EfAcademicAuditWriter → AdminAuditLog
→ SaveChanges + Commit
→ نتیجهٔ HTTP
```

Audit و Outbox هم‌مرز عملیات معتبر باقی می‌مانند؛ تماس خارجی در transaction جدیدی قرار نگرفته است.

## ۳. فایل‌های مهم ایجادشده

مسیرها نسبت به ریشهٔ مخزن هستند.

| فایل | مسئولیت / دلیل / نکتهٔ آموزشی |
|---|---|
| `MakanApp.Api/OpenApi/ApiContractDocumentTransformer.cs` | scheme واقعی نشست و schema مشترک ProblemDetails؛ قرارداد باید implementation را توصیف کند |
| `MakanApp.Api/OpenApi/ApiContractOperationTransformer.cs` | شناسهٔ deterministic، metadata امنیت، کدهای خطا و پاسخ binary؛ metadata از source استخراج می‌شود |
| `MakanApp.Api/OpenApi/PersonalClassInputSchemaTransformer.cs` | limits واقعی و normalization متن؛ maxLength خام با طول Trimشده برابر نیست |
| `MakanApp.Api/OpenApi/OpenApiDocumentCultureMiddleware.cs` | تولید عدد invariant فقط در OpenAPI؛ culture نباید JSON decimal را خراب کند |
| `MakanApp.Application/Academic/PersonalClassHistoryService.cs` | DTO، port و use case history؛ actor/session و permission جدا بررسی می‌شوند |
| `MakanApp.Infrastructure/Academic/EfPersonalClassHistoryStore.cs` | projection SQL صفحه‌ای از Audit موجود؛ review داخلی و نتیجهٔ خصوصی فیلتر می‌شوند |
| `MakanApp.Api/Controllers/PersonalClassHistoryController.cs` | history مدیر جاری؛ Controller فقط actor/transport را به use case می‌دهد |
| `MakanApp.Api/Controllers/PlatformPersonalClassHistoryController.cs` | history metadata پلتفرم با policy مستقل |
| `MakanApp.Application/Assessment/IAcademicAuditWriter.cs` | port ثبت شش رخداد انتشار/نتیجه؛ نیازی به repository عمومی نیست |
| `MakanApp.Infrastructure/Assessment/EfAcademicAuditWriter.cs` | Audit PersonalClass در transaction جاری، بدون answer/score/private note |
| `tests/MakanApp.IntegrationTests/PersonalClassContractTests.cs` | پنج سناریوی قرارداد، bounds، SQL pagination، error/idempotency و privacy/history |
| `tests/MakanApp.UnitTests/Assessment/PersonalAcademicContractBoundaryTests.cs` | سه حالت تعداد پیوست 0/20/21 برای قاعدهٔ واقعی |

پنج سند این مرحله: همین گزارش، [OpenAPI changelog](PERSONAL_CLASS_V2_OPENAPI_CHANGELOG_FA.md)، [کاتالوگ رویداد](PERSONAL_CLASS_V2_EVENT_CATALOG_FA.md)، [Frontend handoff](PERSONAL_CLASS_V2_COMPLETE_FRONTEND_HANDOFF_FA.md) و [Admin handoff](../platform/PERSONAL_CLASS_V2_ADMIN_HANDOFF_FA.md).

## ۴. فایل‌های مهم تغییرکرده

| گروه | تغییر و اثر |
|---|---|
| `Program.cs`، `SessionAuthenticationHandler.cs`، `ApiErrorCodes.cs` | ثبت transformerها و ProblemDetails امن حتی با Accept نامنطبق؛ قواعد ورود/مجوز تغییر نکردند |
| `GlobalExceptionHandler.cs` | دسترسی internal به نگاشت status برای reuse مستندات؛ رفتار handler ثابت است |
| `EvaluationsController.cs`، `ExamGradingsController.cs`، `SubmissionsController.cs`، `ExamsController.cs` | routeهای page با DTO صریح؛ arrayهای قبلی باقی‌اند |
| `PersonalClassLifecycleController.cs`، `PersonalClassesController.cs`، `PlatformCommercialContractsController.cs` | مستندسازی status واقعی 204/202 بدون بدنه |
| `PersonalAcademicReadService.cs` | alias محاسبه‌شدهٔ totalCount با حفظ total |
| `EvaluationService.Reads.cs`، `ExamGradingService.Reads.cs`، `SubmissionService.Reads.cs` و portهای متناظر | reuse authorization مشترک برای route قدیمی و جدید |
| `EfEvaluationStore.cs`، `EfExamGradingStore.cs`، `EfSubmissionStore.cs`، `EfExamAttemptStore.cs` | Count و page در SQL؛ ترتیب ثابت با tie-breaker |
| چهار service انتشار/ارزیابی | ثبت Audit پس از checkهای replay و پیش از commit |
| `PersonalClassService.cs` و Store Core | انتقال session به پذیرش/دعوت و Audit؛ metadata تغییر نقش/انتقال/پایان عضویت |
| `DependencyInjection.cs` | ثبت implementationهای history و Audit |
| تست‌های Academic و helper مشترک | assertions یک‌باربودن Audit/عدم محتوای خصوصی؛ fixture اختیاری تاریخچهٔ approval را حذف نمی‌کند |
| `AccountRealtimeOutboxTests.cs` | تثبیت ساعت retry و محدودکردن crash به event هدف؛ صحت پیام مستقل در همان batch نیز بررسی می‌شود |

## ۵. جریان اجرا و قرارداد HTTP

دو history endpoint جدید و پنج read صفحه‌ای جدید، `GET` و پاسخ `200` دارند؛ درخواست mutation جدیدی اضافه نشده است. controllerهای قبلی Account/Academic همان مدل نشست و authorization را دارند.

| Route | ورودی | پاسخ | مجوز / خطای مهم |
|---|---|---|---|
| `/api/v1/personal-classes/{classId}/history` | page، pageSize | PersonalClassPage<PersonalClassHistoryItem> | مدیر جاری؛ 404 برای دیگران؛ 400 query |
| `/api/admin/v1/personal-classes/{classId}/history` | page، pageSize | همان projection امن | platform.personal_classes.read؛ 403 مجوز جاری ناکافی و 404 |
| `/api/v1/academic/evaluations/queue/page` | EvaluationQueueQuery | AcademicPage<EvaluationQueueItemResult> | Grade/authority context؛ classKind=PersonalClass صریح |
| `/api/v1/academic/exams/{examId}/grading/page` | ExamGradingQueueQuery | AcademicPage<ExamGradingQueueItemDto> | Grade مؤثر/TeacherAssignment مربوط برای سازمان |
| `/api/v1/academic/assignments/{assignmentId}/attempts/me/page` | page، pageSize | AcademicPage<SubmissionAttemptResult> | مالک و دورهٔ عضویت مجاز |
| `/api/v1/academic/assignments/{assignmentId}/submitted-attempts/page` | page، pageSize | همان نوع page | ارزیاب مجاز همان activity |
| `/api/v1/academic/exams/{examId}/attempts/me/page` | page، pageSize | AcademicPage<StudentExamAttemptSummaryDto> | مالک جاری؛ پاسخ/سؤال کامل در این summary نیست |

نام schema پاسخ هر مسیر مرجع نهایی OpenAPI است. جزئیات flows قبلی، Requestها، Responseها، errors و محدودیت‌ها در راهنمای یکپارچه آمده‌اند.

## ۶. دیتابیس

`MIGRATION_REQUIRED=false`. هیچ table/column/PK/FK/index/constraint/rowversion جدیدی تعریف نشده است. `AdminAuditLog` و جداول Academic/Core موجود reuse می‌شوند. history یک read projection است، نه جدول موازی.

انتشار و release/correction همان transaction کوتاه قبلی را نگه می‌دارند. writer آموزشی خارج transaction خطا می‌دهد تا ثبت Audit به‌اشتباه از اثر اصلی جدا نشود. replay موفق قبل از ثبت اثر دوم برمی‌گردد. schema فقط در SQL تصادفی و ایزولهٔ تست اعتبارسنجی شده؛ MakanApp، MakanApp_Test و سرور دست‌نخورده‌اند.

## ۷. تصمیم‌های طراحی

- به‌جای JWT فرضی، scheme Opaque انتخاب شد چون توکن واقعی نشست همین است.
- به‌جای شکستن array قدیمی، page route جدید و totalCount additive اضافه شد؛ هزینه، نگه‌داری دو قرارداد تا مهاجرت client است.
- به‌جای سیستم Audit دوم، port محدود روی table و transaction موجود قرار گرفت؛ وابستگی Application به EF ایجاد نشد.
- history مدیر جاری از Audit موجود projection می‌گیرد؛ actor/correlation داخلی و review Platform در آن نیست. Platform read نیز دسترسی نتیجهٔ خصوصی نمی‌دهد.
- به‌جای event جدید برای هر مفهوم، keyهای واقعی `access` و `assessment.*` مستند شدند؛ client باید refetch انجام دهد و exactly-once فرض نکند.
- normalized text با pattern/extension مستند شد؛ SDKهایی که extension را نمی‌خوانند باید خطای سرور را نیز مدیریت کنند.

## ۸. تست‌ها و پذیرش

پنج fact جدید Integration، host/HTTP واقعی تست، SQL Server و metadata تولیدی را بررسی می‌کنند: سند معتبر و یکتا، 401/403/400 typed، min/max/over-max/time/count، tieهای pagination و scope، صف/attempt و history/Audit خصوصی. سه case Unit قاعدهٔ count پیوست را بدون HTTP/SQL بررسی می‌کنند. assertions تست‌های قبلی assignment/exam/concurrency، release/correction و عدم duplicate Audit را کامل می‌کنند. 14 تست Architecture قبلی dependencyهای Onion را نگه می‌دارند.

E2E جداگانهٔ محلی با Kestrel، SQL تصادفی و دو WebSocket واقعی: تشکیل→submit→approval→active→عضویت→تکلیف و فایل→ارزیابی و release/correction→آزمون و فایل→finalize/grade/release/correction→reconnect/replay→لغو عضویت و منع REST/file/autosave. نتیجه: **121 درخواست HTTP ثبت‌شده در سناریوی اصلی، 14 درخواست در Probe مستقل، 33 check اصلی و پذیرش WebSocket، PASS**. Parent با GuardianRelation واقعی نیز دسترسی ضمنی نگرفت؛ Platform reviewer و School Manager نتوانستند پاسخ/نتیجهٔ خصوصی بخوانند. 62 migration موجود روی DB ایزوله اعمال و DB پس از اجرا حذف شد؛ هیچ SMS واقعی یا AI runtime فعال نشد.

| پذیرش | شاهد |
|---|---|
| C01 | Opaque Bearer، anonymous explicit، metadata permission، 401/403 واقعی |
| C02 | 433 شناسهٔ یکتا، summary/description برای 136 عملیات هدف |
| C03 | MakanProblemDetails، کدهای واقعی و fallback400، Safety schema حفظ‌شده |
| C04 | empty/min/max/over-max، attachment count و dueAt سرور |
| C05 | SQL page/count/order، binary/206/416، 204 و 202 واقعی |
| C06 | replay همان قصد، payload متفاوت conflict، stale version صریح |
| C07 | AI config با operational برابر نیست؛ Runtime این مرحله خاموش |
| C08 | concealment، Parent/Platform/School privacy، revoke/replay و Audit بی‌محتوا |

## ۹. Validation نهایی

Restore موفق بود. Build نهایی Release با **0 warning و 0 error** موفق شد. نتیجهٔ کامل نهایی: **Unit=1064، Integration=1716، Architecture=14، Total=2794، Failed=0، Skipped=0**. پنج fact قرارداد نیز در اجرای متمرکز نهایی 5/5 موفق بودند. تست‌های migration موجود، نبود pending model changes را تأیید کردند؛ migration تازه‌ای وجود ندارد.

اجرای اول regression به شکست یک تست قدیمی crash/retry Realtime برخورد؛ اجرای متمرکز مجدد همان تست همراه با پنج fact قرارداد، 6/6 موفق شد. اجرای کامل بعدی 1715/1716 Integration را عبور داد و assertion جدیدِ کاتالوگ خطای route آموزشی شخصی را آشکار کرد؛ انتخاب catalog بر اساس پیشوند Academic اصلاح شد. پس از اصلاح، اجرای کامل 2794/2794 بالا موفق شد. شکست‌های قبلی به‌عنوان موفقیت محاسبه نشده‌اند.

در بررسی پس از Commit، ناپایداری تست crash/retry دوباره ظاهر شد: تعداد کل batch برابر 2 بود، ولی تست 1 فرض می‌کرد. loop واقعی Outbox ساعت را در هر claim می‌خواند و retry اول backoff دوثانیه‌ای دارد؛ تست قبلی هم از زمان واقعی استفاده می‌کرد و هم crash را به همهٔ eventها تزریق می‌کرد. تست اصلاح شد: ساعت پیش از dispatch ثابت می‌شود، یک event مستقل عمداً در همان batch قرار می‌گیرد، crash فقط برای event هدف است، تلاش اول هدف دقیقاً 1 و پیام مستقل completed است؛ سپس ساعت صریحاً 3 ثانیه جلو می‌رود و همان event با AttemptCount=2 کامل می‌شود. assertionهای transaction خالی، دریافت دوبارهٔ همان EventId و replay حفظ شده‌اند. timeout و منطق Production تغییر نکرده‌اند. نتیجهٔ بازاجرای این اصلاح و Commit تکمیلی در گزارش نهایی درج می‌شود.

Build اصلاح با خروجی خصوصی جدا و 0 warning/error و دو تست متمرکز Outbox با نتیجهٔ 2/2 موفق شد. اجرای متمرکز، به‌اشتباه با regression قبلی هم‌زمان اجرا شد؛ fixture اصلی نام ثابت `MakanApp_MessagingSafety_IntegrationTests_Step7E` دارد. این DB از دیتابیس‌های عادی جداست، ولی بین دو فرایند تست مستقل نیست. 42 شکست بعدیِ همان regression پس از شروع اجرای موازی ثبت شدند؛ آن اجرا با مجموع 43 شکست، موفق محسوب نشد. برای gate نهایی، تمام `dotnet test`های SQL به‌صورت سریال اجرا می‌شوند. مسیر خروجی جدا (`--artifacts-path`) فقط اسمبلی‌ها را جدا می‌کند و جایگزین ایزوله‌بودن دیتابیس نیست. هیچ تغییر جبرانی در Authentication، تنظیمات یا منطق آموزشی برای این شکست‌های ناشی از تداخل داده نشده است.

تست CLI مربوط به Official Spaces، configuration را از چیدمان استاندارد `bin/Release/net9.0` استنتاج می‌کند. بنابراین اجرای کامل با خروجی جایگزین پس از آشکارشدن این فرض مسیر متوقف شد و نتیجهٔ موفقی برای آن ثبت نشد. gateهای کامل نهایی فقط با Restore/Build/Test استاندارد مخزن و بدون TestHost هم‌زمان اجرا می‌شوند؛ برای پنهان‌کردن خطای محیط، assertion ابزار یا منطق Production تغییر داده نشده است.

بازاجرای استاندارد و سریال پس از اصلاح Outbox موفق شد: Restore و Build با 0 warning/error؛ focused Outbox برابر 2/2؛ مجموعهٔ کامل Unit=1064، Integration=1716، Architecture=14، Total=2794، Failed=0، Skipped=0. ثبت اصلاح تست در Commit تکمیلی و gate پس از آن از همین نتیجه پیروی می‌کنند؛ نتیجهٔ نهایی Push/remote فقط پس از اجرای واقعی اعلام می‌شود.

OpenAPI نهاییِ Kestrel: 433 operation، 136 عملیات هدف، 4751 مرجع داخلی معتبر، missing/duplicate operationId=0 و untyped 200/201=0. اسکن تغییرات، نشت مقدار config خصوصی یا JWT واقعی پیدا نکرد؛ hash هر هشت فایل محافظت‌شده ثابت ماند. C01 تا C08 با شواهد بالا موفق‌اند. اعتبارسنجی مستقل پس از Commit و نتیجهٔ Git/remote در گزارش نهایی همان مرحله، پس از اجرای واقعی ثبت می‌شوند.

## ۱۰. Git و حفاظت تغییرات

سه فایل ناتمام قبلی `tools/ai/provider_diagnostic.py`, `tools/ai/run_exact_ai.py`, `tools/ai/test_provider_diagnostic.py` خارج scope هستند. hash اولیهٔ آن‌ها، چهار appsettings خصوصی و User Secrets برای مقایسه ثبت شده؛ هیچ مقدار secret در سند یا diff عمومی چاپ نمی‌شود. artifactهای خصوصی اجرای تست وارد commit نمی‌شوند.

پیام پیشنهادی Commit: `feat(personal-class): harden contracts and realtime integration`. hash، branch، push و تطبیق remote در گزارش نهایی پس از اجرای واقعی اعلام می‌شوند. push فقط اگر remote عقب نمانده باشد؛ force-push و ادغام نامرتبط مجاز نیست.

## ۱۱. محدودیت‌های فعلی

دسترسی تاریخی بعد از خروج و retention، running exam هنگام suspend/archive و emergency termination، سقف مدیر اضافی، قیمت/payer، Parent ویژه، Study Studio و گزارش پیشرفته هنوز تصمیم Product نمی‌شوند. رفتار فعلی conservative است: access جاری دوباره بررسی می‌شود، revoke نوشتن/دانلود آینده را می‌بندد، record تاریخی حذف نمی‌شود. این رفتار سیاست نهایی retention/termination نیست.

AI Contract v1 و خطای retrieval `AI_SOURCE_RETRIEVAL_NOT_AVAILABLE` حفظ شدند. سیاست Owner عدم الزام Service JWT/WireGuard است، اما runtime/auth/config AI در این مرحله تغییر نکرده است. frontend، انتشار، deploy و migration سرور انجام نشده‌اند. dependency یا نسخهٔ .NET/EF عوض نشده است. `AGE_BUSINESS_DEPENDENCIES=0`.

## ۱۲. نکات آموزشی

1. OpenAPI باید از host واقعی fetch و parse شود؛ compile موفق، قرارداد JSON معتبر را اثبات نمی‌کند.
2. Authentication metadata جای authorization روی resource را نمی‌گیرد؛ Platform permission و نقش محلی نیز جدا هستند.
3. Count/Filter/Order/Skip/Take باید روی query مجاز SQL انجام شوند؛ materialize و سپس page قابل اتکا نیست.
4. Audit با اثر اصلی transaction مشترک دارد و باید پس از replay check ثبت شود.
5. read model امن از projection محدود ساخته می‌شود؛ حذف فیلد خصوصی در UI محافظ Backend نیست.
6. invalidation منبع داده نیست؛ at-least-once، cursor و refetch برای convergence لازم‌اند.
7. schema normalization و culture بخشی از قراردادند؛ وضعیت واقعی خطا را برای زیباترشدن سند عوض نکنید.

## ۱۳. مسیر مطالعه

1. `PersonalClassHistoryController.cs` و `PlatformPersonalClassHistoryController.cs`
2. `PersonalClassHistoryService.cs`
3. `PersonalClassAcademicPermissionService.cs` و resolver موجود Academic
4. `EfPersonalClassHistoryStore.cs`
5. `IAcademicAuditWriter.cs` → `EfAcademicAuditWriter.cs` → محل فراخوانی در serviceهای انتشار/release
6. `EfEvaluationStore.cs` و queryهای صفحه‌بندی دیگر
7. سه transformer و middleware OpenAPI
8. `PersonalClassContractTests.cs` و تست‌های Academic اصلاح‌شده

## ۱۴. مرحلهٔ بعد

مصرف قرارداد یکپارچه در task مجزای Frontend/Admin و پذیرش محیط استقرار؛ تصمیم‌های باز Product باید جدا freeze شوند. AI runtime، RAG، قیمت‌گذاری یا سیاست retention نباید به این hardening اضافه شوند.
