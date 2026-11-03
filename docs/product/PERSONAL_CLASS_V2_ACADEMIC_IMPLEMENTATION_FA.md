# پیاده‌سازی آموزشی PersonalClass v2

تاریخ: ۲۰۲۶-۱۱-۰۳. مبنای آغاز: `7399df41bbbddd8ef791c373b9608b51ce3867fe`.

این سند وضعیت پیاده‌سازی و شواهد Validation محلی همین مرحله را توضیح می‌دهد. نتایج زیر از اجرای واقعی آزمون‌ها گرفته شده‌اند؛ انتشار روی سرور جزو این مرحله نیست.

## تصمیم مصوب مجوز آموزشی

نقش Manager به‌تنهایی اختیار آموزشی نمی‌دهد. Platform با permission مستقل `platform.personal_classes.academic_permissions.manage`، سه قابلیت `AuthorAssignments`، `AuthorExams` و `Grade` را به **دورهٔ عضویت مشخص مدیر جاری** اعطا یا لغو می‌کند. این permission برای Owner در catalog اولیه موجود است؛ نقش Platform به‌تنهایی مجوز خواندن پاسخ خصوصی ایجاد نمی‌کند.

مدیر فقط زیرمجموعهٔ اختیار مؤثر خود را به Teacher همان کلاس تفویض می‌کند. Grant فرزند به شناسهٔ Grant والد متصل است. لغو/تعویض Grant والد، پایان عضویت، حذف نقش لازم یا انتقال مدیریت، تفویض وابسته را باطل می‌کند. اعطای دوبارهٔ اختیار به مدیر، Grant قدیمی مدرس را زنده نمی‌کند. مدیر جدید باید مستقلاً اختیار بگیرد.

ثبت تغییر شامل عامل، دلیل، زمان، نشست و correlation در `AdminAuditLogs` است. `ClientOperationId` و hash درخواست تکرارپذیری را کنترل می‌کنند؛ تغییر payload با همان شناسه مجاز نیست. RowVersion عضویت و Grant از اعمال تغییر روی وضعیت قدیمی جلوگیری می‌کنند.

## معماری و فایل‌های اصلی

| لایه / فایل | مسئولیت و دلیل |
|---|---|
| `MakanApp.Domain/Assessment/AcademicClassRef.cs` | مرجع نوع‌دار OrganizationClass/PersonalClass و مشارکت واقعی Enrollment/عضویت محلی؛ بدون شناسهٔ سازمان یا Enrollment ساختگی |
| `MakanApp.Domain/Academic/PersonalClassAcademicGrant.cs` | اختیار صریح، دورهٔ عضویت، وابستگی تفویض و لغو |
| `MakanApp.Domain/Assessment/AssignmentVersionAttachment.cs` | پیوست نسخهٔ تکلیف و جلوگیری از تغییر نسخهٔ منتشرشده |
| `MakanApp.Application/Assessment/AcademicAccessContext.cs` | قرارداد مشترک resolve/revalidate مجوز و context |
| `MakanApp.Infrastructure/Assessment/AcademicContextResolver.cs` | ترکیب نشست، کلاس، دورهٔ عضویت و Grant مؤثر؛ بازبینی داخل تراکنش |
| `MakanApp.Infrastructure/Assessment/AcademicContextQuery.cs` | projection مشترک با query جدا و محدود به context واقعی؛ Person اختیاری در حساب شخصی |
| `MakanApp.Application/Academic/PersonalClassAcademicPermissionService.cs` | validation و use case اعطا/لغو اختیار |
| `MakanApp.Infrastructure/Academic/EfPersonalClassAcademicPermissionStore.cs` | تراکنش SQL، idempotency، RowVersion، Audit و invalidation |
| `MakanApp.Application/Assessment/PersonalAcademicReadService.cs` و `MakanApp.Infrastructure/Assessment/EfPersonalAcademicReadStore.cs` | فهرست‌های فیلترشده و صفحه‌بندی روی سرور |
| `MakanApp.Api/Controllers/PersonalClassAcademicsController.cs` | entry point کلاس مستقل؛ ادامهٔ چرخه از endpointهای مشترک موجود |
| `MakanApp.Api/Controllers/PersonalClassAcademicPermissionsController.cs` و `PlatformPersonalClassAcademicPermissionsController.cs` | جداسازی route تفویض مدیر از route اعطای Platform؛ authority از نشست و permission واقعی می‌آید |
| `MakanApp.Infrastructure/Persistence/Configurations/Assessment/AcademicScopeConfiguration.cs` | CHECK/FKهای مشترک scope و دورهٔ مشارکت؛ جلوگیری از اتصال PersonalClass به Enrollment یا کلاس دیگر |
| `MakanApp.Infrastructure/Assessment/AcademicFileBindingQuery.cs` | جلوگیری از reuse یک فایل پاسخ در فعالیت یا حوزهٔ دیگر و نشت از ACL ضعیف‌تر |
| `MakanApp.Api/OpenApi/PersonalAcademicOperationTransformer.cs` | شرح مجوز، صفحه‌بندی، receipt و ProblemDetails برای routeهای واقعی |
| `MakanApp.Infrastructure/Assessment/ExamDeadlineFinalizer.cs` و `ExamDeadlineDispatcher.cs` | تثبیت پاسخ‌های قبلاً پذیرفته‌شده در سررسید؛ بازیابی بعد از توقف process |

Dependency flow همان Onion است: Api → Application → قراردادهای Application؛ Infrastructure پیاده‌سازی SQL را فراهم می‌کند و Domain مستقل از EF/HTTP باقی مانده است. Controllerها مجوز تجاری یا query SQL ندارند.

## موتور مشترک و جریان اجرا

تکلیف: `PersonalClassAcademicsController` → `AssignmentService` → `AcademicContextResolver` → `Assignment`/`AssignmentVersion` → `EfAssignmentStore` → SQL Server. انتشار، Recipient را از عضویت‌های Learner جاری snapshot می‌گیرد. `SubmissionService` همان موتور Draft، پیوست و FinalSubmit موجود را با دورهٔ عضویت واقعی استفاده می‌کند. `EvaluationService` نمرهٔ Draft، Release صریح و Correction با revision جدید را حفظ می‌کند.

آزمون: `ExamService` نسخه و سؤال‌های immutable را منتشر می‌کند. `ExamAttemptService` نسخهٔ منتشرشده، ترتیب سؤال‌ها و deadline سرور را تثبیت می‌کند. `ExamAnswerService` نسخهٔ پاسخ، ClientOperationId و WriteLease را کنترل می‌کند. `ExamFinalizationService` پاسخ‌های نهایی و receipt را ثبت می‌کند. `ExamGradingService` تصحیح objective را از کلید همان نسخه انجام می‌دهد؛ سؤال تشریحی نیازمند ارزیابی دستی و سپس Complete/Release است.

سرویس‌های Assessment، Storeها و DTOهای قبلی توسعه یافته‌اند؛ موتور موازی PersonalClassAssignment/PersonalClassExam ساخته نشده است. مسیرهای Organization حفظ شده‌اند. تغییرات nullable موردنیاز در گزارش‌های سازمانی و `EfAiScopeStore` فقط سازگاری نوع reference هستند؛ AI Runtime و قرارداد فراخوانی تغییر نکرده‌اند.

## دیتابیس و مهاجرت

Migration: `20261103104624_ExtendAcademicEngineToPersonalClasses`.

- رکوردهای مشترک آموزشی `ContextKind` و `ContextId` دارند. `OrganizationId` یا `PersonalClassId` واقعی با CHECK انحصاری کنترل می‌شود.
- `ParticipantId` با `EnrollmentId` یا `PersonalClassMembershipId` واقعی و نوع context سازگار است؛ FK مرکب از اتصال دورهٔ عضویت کلاس دیگر جلوگیری می‌کند.
- Actorهای آموزشی برای سازمان از Membership و برای کلاس مستقل از PersonalClassMembership استفاده می‌کنند؛ ستون‌های مستقل قابل اشتباه‌گرفتن نیستند.
- `academic.PersonalClassAcademicGrants`: یکتایی Grant فعال برای کلاس/عضویت، یکتایی عملیات عامل، FK والد و RowVersion.
- `assessment.AssignmentVersionAttachments`: FK نسخه و FileAsset، یکتایی نسخه/فایل؛ نسخهٔ منتشرشده immutable است.
- `storage.FileAssets`: دو ستون nullable برای کلاس مستقل و دورهٔ عضویت بارگذار؛ فایل بدون scope یا کلاس دیگر قابل attach نیست.
- `ExamAttempts.AutoFinalizedAtUtc` فقط پردازش سررسید را مشخص می‌کند؛ `FinalizedAtUtc` در این حالت همان deadline سرور است. ClientOperationId یا نشست جعلی ساخته نمی‌شود.
- برای دادهٔ سازمانی موجود، ستون context ابتدا nullable اضافه می‌شود و فقط از OrganizationId/EnrollmentId موجود پر می‌شود؛ سپس NOT NULL و constraints اعمال می‌شوند. دادهٔ تجاری یا هویت حدسی تولید نمی‌شود.
- Down در صورت وجود دادهٔ اختصاصی جدید با خطا متوقف می‌شود تا حذف اطلاعات جدید پنهان نشود.

مهاجرت عادی هنگام startup اجرا نمی‌شود. آزمون‌ها فقط LocalDB ایزوله و قابل‌حذف دارند؛ `MakanApp`، `MakanApp_Test` و سرور هدف این مرحله نیستند.

## هم‌زمانی، deadline و بازیابی

بازبینی نوشتن در Serializable انجام می‌شود. قفل کلاس و نشست پیش از خواندن‌هایی که بعداً به قفل نوشتن نیاز دارند گرفته می‌شود تا دو درخواست هم‌زمان در ارتقای S به U بن‌بست نسازند. در PersonalClass ترتیب با چرخهٔ عضویت سازگار است: میزبان → کلاس → نشست. Queryها به context واقعی محدودند.

Worker سررسید فقط تلاش‌های PersonalClass فعال و موعدرسیده را در batch محدود پردازش می‌کند؛ وضعیت SQL منبع بازیابی است. چند worker با قفل تلاش، فقط یک final outcome می‌سازند. پاسخ تازه، grace period، زمان دستگاه یا credential جعلی وارد receipt نمی‌شود. مسیر resume/receipt نیز بازیابی لازم را انجام می‌دهد؛ بازیابی sync فهرست فقط تلاش‌های همان هنرجوی جاری را بررسی می‌کند.

تنظیمات `Academic:ExamDeadline`: `Enabled=true`، `IntervalSeconds=15` و `BatchSize=50` به‌صورت default کد؛ validator بازه را کنترل می‌کند. هیچ فایل خصوصی appsettings تغییر نکرده است.

## حریم خصوصی و فایل

هنرجو فقط نسخهٔ منتشرشدهٔ مجاز، تلاش خودش در همان دوره و نتیجهٔ Release‌شده را می‌بیند. Person/DOB/سن/VerificationStatus منبع مجوز آموزشی نیست. پاسخ شخص دیگر، کلید درست، یادداشت خصوصی و نمرهٔ منتشرنشده در DTO هنرجو قرار نمی‌گیرند.

پیوست باید Ready، از نوع مجاز و در context صحیح باشد. مالکیت و binding به فعالیت دیگر کنترل می‌شود. فایل پاسخ Draft برای evaluator قابل دانلود نیست؛ پس از Submit/Finalize با اختیار Grade و مجوز جاری قابل خواندن است. هر دانلود عضویت فعلی و scope را دوباره بررسی می‌کند. پایان عضویت و rejoin، فایل یا تلاش دورهٔ قبلی را خودکار قابل دسترسی نمی‌کند.

Platform Admin و School Manager بیرون کلاس دسترسی ضمنی به محتوای آموزشی خصوصی ندارند. GuardianRelation مجوز نتیجهٔ PersonalClass ایجاد نمی‌کند.

## Activity و Realtime

انتشار موفق از `OfficialSpaceEventWriter` و Outbox موجود استفاده می‌کند. `EfMessagingStore.DeliverAcademicActivityAsync` برای هر Group/Channel یک کارت idempotent می‌سازد. نتیجه/نمره/بازخورد خصوصی کارت عمومی نمی‌شوند.

`RealtimeInvalidationWriter` کلیدهای موجود `assessment.assignment`، `assessment.submission`، `assessment.grade`، `assessment.exam`، `assessment.exam-attempt` و `assessment.exam-grade` را با مخاطب و دورهٔ عضویت محلی می‌سازد. `AssessmentRealtimeAccess` هنگام ارسال و replay همان مجوز فعلی use case را بازبینی می‌کند. payload فقط invalidation است؛ پاسخ هنرجو و نمره را حمل نمی‌کند. `ClientInvalidated` روی Hub موجود و `/api/v1/realtime/changes` برای catch-up استفاده می‌شوند؛ delivery دقیقاً یک‌بار فرض نمی‌شود.

## محدودیت‌های صریح

- سیاست نهایی پایان تلاش باز هنگام Archive/Suspend کلاس همچنان تصمیم باز محصول است؛ نوشتن جدید متوقف و رکوردهای قبلی حفظ می‌شوند، worker برای این کلاس‌ها نتیجهٔ حدسی نمی‌سازد.
- دسترسی تاریخی پس از پایان و عضویت دوباره خودکار فعال نمی‌شود؛ سیاست اعطای دسترسی تاریخی جدید خارج از این مرحله است.
- Assignment منتشرشده قابل ویرایش نیست؛ endpoint جداگانهٔ ساخت نسخهٔ بعدی Assignment در قرارداد فعلی وجود ندارد. Exam از CreateNextVersion موجود استفاده می‌کند.
- قیمت‌گذاری مستقل PersonalClass، AI grading، proctoring، attendance، question bank و گزارش پیشرفته ساخته نشده‌اند.
- هیچ Publish یا Deploy در این مرحله انجام نمی‌شود.

## نکات آموزشی و مسیر مطالعه

۱. Role با Capability یکسان نیست؛ Grant باید به دورهٔ عضویت و منبع تفویض متصل باشد.
۲. یک موتور مشترک به معنی ساخت Organization جعلی نیست؛ reference نوع‌دار و FK واقعی لازم است.
۳. Snapshot پاسخ/کلید نمره باید به نسخهٔ immutable متصل بماند.
۴. بررسی مجوز پیش از تراکنش کافی نیست؛ وضعیت داخل تراکنش بازبینی می‌شود.
۵. قفل خواندنِ Serializable قبل از قفل نوشتن می‌تواند سبب بن‌بست شود؛ ترتیب قفل بخشی از طراحی است.
۶. پذیرفتن پاسخ، نهایی‌سازی و انتشار نمره سه مرز مستقل‌اند.
۷. Event فقط refetch را اعلام می‌کند؛ هر read/replay مجوز جاری را دوباره کنترل می‌کند.

ترتیب مطالعه: Controllerهای PersonalClassAcademics/AcademicPermissions → Application Serviceها → AcademicClassRef/PersonalClassAcademicGrant → AcademicContextResolver → Storeهای Assessment → EF Configuration → Migration → تست‌های `PersonalClassAcademic*`.

## شواهد Validation و Git

`dotnet restore MakanApp.sln` موفق شد. Buildهای Release اولیه و نهایی با صفر Warning و صفر Error موفق شدند. اجرای کامل نهایی پیش از Commit:

| مجموعه | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Unit | ۱۰۶۱ | ۱۰۶۱ | ۰ | ۰ |
| Integration | ۱۷۱۱ | ۱۷۱۱ | ۰ | ۰ |
| Architecture | ۱۴ | ۱۴ | ۰ | ۰ |
| مجموع | ۲۷۸۶ | ۲۷۸۶ | ۰ | ۰ |

Baseline برابر ۲۷۶۸ بود؛ ۴ Unit و ۱۴ مورد Integration افزوده شدند. از موارد Integration جدید، ۱۳ مورد مختص PersonalClass Academic و یک مورد مهاجرت از baseline همین مرحله است. افزون بر آن، ۱۶ تست متمرکز شامل رفتار جدید، مهاجرت‌های تاریخی و تست Realtime با موفقیت اجرا شدند. Pending model changes در آزمون مهاجرت وجود نداشت.

در اجراهای اولیه، بن‌بست ارتقای قفل، تفاوت error code دسترسی بین زمینه‌ها و fixture ناسازگار با schema تاریخی کشف و اصلاح شدند. یک اجرای میانی نیز در تست قدیمی `AccountRealtimeLiveContextRevisionAndDuplicateOldEventCannotRollBackAuthoritativeInbox` هنگام انتظار رویداد timeout داشت؛ همان تست بدون افزایش timeout یا تغییر کد آن در اجرای متمرکز و اجرای کامل نهایی موفق شد. علت قطعی آن timeout اثبات نشده و به‌عنوان اصلاح قطعی Realtime گزارش نمی‌شود.

بازبینی secret روی همهٔ فایل‌های جدید و خطوط افزوده، شامل تست و migration، موردی نیافت. هش هر ۸ فایل محافظت‌شده با baseline یکسان بود؛ سه فایل قبلی `tools/ai/provider_diagnostic.py`، `tools/ai/run_exact_ai.py` و `tools/ai/test_provider_diagnostic.py` خارج از Commit این مرحله‌اند. فایل‌های خصوصی appsettings و User Secrets تغییر نکرده‌اند.

پیام Commit این مرحله `feat(personal-class): add assignment and exam workflows` است. طبق درخواست، بعد از Commit دوباره Release Build و هر سه suite اجرا می‌شوند و تنها سپس با کنترل ahead/behind و بدون force-push همگام‌سازی انجام می‌شود. SHA، نتیجهٔ واقعی اجرای پس از Commit و تأیید Remote در گزارش نهایی عملیات ثبت می‌شوند.

## شواهد اجرای واقعی محلی

آخرین اجرای E2E روی Kestrel واقعی و SQL Server LocalDB تصادفی و ایزوله موفق شد: **۱۲۹ درخواست HTTP، دو اتصال واقعی SignalR WebSocket و ۲۶ بررسی موفق**. این تعداد شامل درخواست‌های انتظار آمادگی کارت‌ها و negotiate است؛ همهٔ پاسخ‌ها الزاماً 2xx نیستند، چون رد صحیح دسترسی نیز معیار پذیرش است.

حساب Parent دارای نقش سازمانی Parent و GuardianRelation معتبر به همان هنرجو نیز ساخته شد. با انتخاب زمینهٔ فرزند، مسیرهای نتیجهٔ عادی، Guardian، Editor آزمون و فایل آموزشی PersonalClass همچنان 404 دادند. این آزمون نشان می‌دهد رابطهٔ والد–فرزند مجوز آموزشی کلاس مستقل نیست.

دو اتصال WebSocket، انتشار واقعی، invalidation پیش‌نویس شخصی، قطع/اتصال دوباره، catch-up، پنهان‌ماندن پیش‌نویس همتا و قطع دسترسی پس از پایان عضویت را بررسی کردند. رکوردهای تلاش باز حفظ شدند. همهٔ ۶۲ migration در دیتابیس خالی ایزوله اعمال شدند و همان دیتابیس در پایان پاک شد. هیچ SMS واقعی، فراخوانی AI، Deploy یا تغییر دیتابیس اصلی انجام نشد.

شواهد امن محلی در `.artifacts/personal-class-academic-20261103-27cb1f94/runtime/host-result.json` و `http-evidence.json` هستند. این فایل‌ها status، کد خطا، correlation، زمان و مسیر بدون شناسهٔ منبع دارند؛ credential، payload خصوصی و پاسخ آموزشی در آن‌ها ذخیره نشده است. ابزار محلی و داده‌های ساختگی وارد source محصول نشده‌اند.

## نگاشت پذیرش به آزمون واقعی

آزمون‌های PersonalClass در فایل‌های `tests/MakanApp.IntegrationTests/PersonalClassAcademic*.cs` قرار دارند و از همان fixture واقعی SQL Server و API استفاده می‌کنند. نام کلاس مشترک آزمون، `CommercialSubscriptionEndpointsTests` است؛ راه‌اندازی قرارداد میزبان و کلاس از helperهای موجود reuse می‌شود. Unitهای `PersonalAcademicModelTests` فقط قواعد Domain را بدون HTTP/SQL بررسی می‌کنند. Architectureهای موجود جهت وابستگی Onion را کنترل می‌کنند.

| پذیرش | رفتار بررسی‌شده | محل شاهد |
|---|---|---|
| A01 | مدیر محلی بدون Organization/Teacher فقط پس از Grant می‌سازد و منتشر می‌کند | `PersonalAcademicManagerNeedsExplicitGrantAndPublishesOnlyToActiveLocalLearners` و HTTP واقعی |
| A02 | حساب عادی و حساب دارای نقش Student سازمانی از عضویت محلی یکسان استفاده می‌کنند | E2E واقعی، دو مخاطب Learner/Peer با context متفاوت |
| A03 | اختیار کلاس دیگر یا School Manager دسترسی نمی‌دهد | `PersonalAcademicAuthoritiesAndFilesCannotCrossIndependentClasses` و HTTP واقعی |
| A04 | Draft، پاسخ دیگری و private note پنهان است | `PersonalAcademicManagerNeedsExplicitGrantAndPublishesOnlyToActiveLocalLearners` و `PersonalAcademicAssignmentAttachmentSubmissionReleaseCorrectionAndPrivateAcl` |
| A05 | دو Publish هم‌زمان یک snapshot و دو کارت مقصد می‌سازند | `PersonalAcademicConcurrentPublishCreatesOneSnapshotAndTwoIdempotentActivityCards` |
| A06 | فایل دیگری و فایل Pending قابل اتصال نیست | `PersonalAcademicAssignmentAttachmentSubmissionReleaseCorrectionAndPrivateAcl` |
| A07 | Retry ارسال نهایی، رسید و اثر قبلی را حفظ می‌کند | همان آزمون Assignment و E2E HTTP |
| A08 | دو ذخیره با یک RowVersion، فقط یک موفقیت دارند | همان آزمون Assignment، درخواست‌های هم‌زمان واقعی |
| A09 | deadline، سقف تلاش و Archive بر سرور اعمال می‌شوند | `PersonalAcademicAssignmentServerDeadlineAttemptLimitAndArchivePreserveHistory` |
| A10 | Draft نمره تا Release پنهان است | آزمون کامل Assignment و HTTP واقعی |
| A11 | Correction، revision جدید با تاریخچه می‌سازد؛ دو اصلاح هم‌زمان conflict دارند | آزمون کامل Assignment و شمارش SQL revisionها |
| A12 | End/Rejoin به تلاش، فایل و Recipient قبلی دسترسی نمی‌دهد | آزمون کامل Assignment و `PersonalAcademicMembershipEndRacingWithDraftSavePreservesRecordsAndFencesFutureWrites` |
| B01 | آزمون objective/descriptive بدون سازمان جعلی ساخته می‌شود | `PersonalAcademicExamVersionLeaseAttachmentsFinalizeGradingAndCorrection` |
| B02 | Editor/کلید پاسخ/نمرهٔ دیگری در دسترس هنرجو نیست | همان آزمون Exam و HTTP واقعی |
| B03 | Start هم‌زمان و Retry همان تلاش و deadline را می‌دهند | همان آزمون Exam |
| B04 | Active/Resume و تغییر نشست، زمان تثبیت‌شدهٔ سرور را عوض نمی‌کنند | همان آزمون Exam؛ deadline در receipt تثبیت‌شده |
| B05 | revision قدیمی و پاسخ دیررس overwrite نمی‌کنند | آزمون کامل Exam و آزمون Deadline |
| B06 | پس از انتقال WriteLease، نویسندهٔ قبلی رد می‌شود | آزمون کامل Exam با دو نشست |
| B07 | Retry Finalize همان رسید را برمی‌گرداند | آزمون کامل Exam و HTTP واقعی |
| B08 | deadline بدون Client قابل بازیابی و دو پردازش هم‌زمان تک‌اثر است | `PersonalAcademicExamDeadlineFinalizationIsRecoverableConcurrentAndDoesNotInventAnswers` |
| B09 | نسخهٔ Draft بعدی، ExamVersion تلاش قبلی را تغییر نمی‌دهد | آزمون کامل Exam |
| B10 | objective خودکار، descriptive دستی، Release و Correction مستقل‌اند | آزمون کامل Exam و HTTP واقعی |
| B11 | Archive/End عضویت، نوشتن را می‌بندد و رکورد حفظ می‌شود | دو حالت `PersonalAcademicOpenExamStopsSafelyAfterArchiveOrMembershipEnd` |
| B12 | Platform و Parent دارای GuardianRelation معتبر اختیار خواندن نتیجهٔ خصوصی نمی‌دهند | آزمون کامل Exam و E2E واقعی Parent؛ پاسخ endpoint و File ACL |

شاهد Realtime مستقل: `PersonalAcademicDraftReplayIsPrivateAndRevocationStopsHistoricalReplay` و اتصال واقعی WebSocket در اجرای محلی. رویداد منتشرشده، پیش‌نویس شخصی، قطع و اتصال دوباره، catch-up، عدم مشاهدهٔ پیش‌نویس همتا و fence لغو عضویت بررسی می‌شوند.

شاهد migration: `AcademicExamUpgradePreservesOldQuestionsAndAnswersWithoutGuessedLimits` از دو baseline تاریخی، از جمله migration موجود در `7399df4`، با دادهٔ غیرخالی اجرا می‌شود. fixture تاریخی فقط ستون‌های واقعی همان schema را با پارامتر SQL درج می‌کند؛ entity جدید را به‌زور در جدول قدیمی ذخیره نمی‌کند. سؤال، پاسخ، deadline، تکلیف، Recipient و Submission قبلی پس از upgrade بررسی می‌شوند. Empty→latest در fixture اصلی و E2E اجرا می‌شود.

## ماتریس پذیرش فنی

مقادیر نشت و duplicate مربوط به سناریوهای اجراشده هستند. اختیار Manager در این ماتریس همواره به معنی Manager دارای Grant صریح است. نتیجهٔ Git در گزارش نهایی جداگانه ثبت می‌شود.

```text
PERSONAL_CLASS_ASSIGNMENT_IMPLEMENTED=true
PERSONAL_CLASS_EXAM_IMPLEMENTED=true
SHARED_ACADEMIC_ENGINE_REUSED=true
NO_PARALLEL_ACADEMIC_DOMAIN=true
NO_FAKE_ORGANIZATION=true
NO_FAKE_ENROLLMENT=true
AGE_BUSINESS_DEPENDENCIES=0
MANAGER_CAN_AUTHOR_ASSIGNMENT=true
MANAGER_CAN_AUTHOR_EXAM=true
OPTIONAL_TEACHER_PASS=true
ASSIGNMENT_VERSION_PINNING_PASS=true
ASSIGNMENT_ATTEMPT_PASS=true
ASSIGNMENT_ATTACHMENT_PASS=true
ASSIGNMENT_FINAL_RECEIPT_PASS=true
ASSIGNMENT_EVALUATION_PASS=true
ASSIGNMENT_RESULT_RELEASE_PASS=true
ASSIGNMENT_CORRECTION_PASS=true
EXAM_VERSION_PINNING_PASS=true
EXAM_START_RETRY_PASS=true
EXAM_WRITE_LEASE_PASS=true
EXAM_ATTACHMENT_PASS=true
EXAM_FINALIZE_PASS=true
OBJECTIVE_GRADING_PASS=true
DESCRIPTIVE_GRADING_PASS=true
EXAM_RESULT_RELEASE_PASS=true
EXAM_CORRECTION_PASS=true
PRIVATE_DATA_LEAK_COUNT=0
ANSWER_KEY_LEAK_COUNT=0
CROSS_CLASS_LEAK_COUNT=0
MEMBERSHIP_REVOCATION_PASS=true
PERSONAL_MESSAGING_PRESERVED=true
ASSIGNMENT_ACTIVITY_CARD_PASS=true
EXAM_ACTIVITY_CARD_PASS=true
ACTIVITY_DUPLICATE_COUNT=0
REALTIME_PASS=true
FILE_ACL_PASS=true
A01_A12_PASS=12/12
B01_B12_PASS=12/12
ORGANIZATION_ACADEMIC_REGRESSION_PASS=true
MESSAGING_REGRESSION_PASS=true
PERSONAL_CLASS_CORE_REGRESSION_PASS=true
EDUCATIONAL_SOURCE_REGRESSION_PASS=true
AI_REGRESSION_PASS=true
COMMERCE_REGRESSION_PASS=true
MIGRATION_REQUIRED=true
MIGRATION_PASS=true
PENDING_MODEL_CHANGES=false
BUILD_PASS=true
FULL_TEST_PASS=true
E2E_PASS=true
SECRET_SCAN_PASS=true
PUBLISH=false
DEPLOY=false
SERVER_MIGRATION=false
```

## تصمیم‌ها و مرحلهٔ بعد

به‌جای ساخت موتور دوم، reference مشترک نوع‌دار انتخاب شد؛ هزینهٔ آن تغییر FK/indexهای Assessment و nullable شدن شناسهٔ سازمان در منابع Personal است. در مقابل، قواعد موجود Finalize، lease، نمره و receipt دوباره‌نویسی نشدند. Grant نسخه‌دار و وابسته به دورهٔ عضویت از boolean دائمی روی User دقیق‌تر است، ولی کلاینت باید RowVersion و `isEffective` را رعایت کند.

برای خواندن کلید ثابت میزبان در چرخهٔ عضویت، `READCOMMITTED` فقط در lookup اولیه استفاده شده است تا قفل S پیش از قفل میزبان نگه داشته نشود؛ مجوز، وضعیت و mutation همچنان زیر قفل‌های تراکنش Serializable بررسی می‌شوند. قرارداد قفل میزبان → کلاس با عملیات آموزشی مشترک است.

گام بعدی محصولی، تعیین پایان قطعی تلاش باز هنگام Archive/Suspend و سیاست دسترسی تاریخی است. این تصمیم نباید با قیمت‌گذاری جدید، AI grading یا انتشار روی سرور مخلوط شود.
