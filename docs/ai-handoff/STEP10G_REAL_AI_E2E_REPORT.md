# گزارش واقعی STEP10G — Backend به AI مستقر

تاریخ: `2026-10-21`؛ وضعیت فعلی: **STEP10G = BLOCKED**.
این گزارش نتیجهٔ همین اجراست، نه انتقال موفقیت‌های Fake یا شواهد تاریخی.

## نسخه و محیط

- Backend مبنا: `1ecde4043671da5bd20a8d16aede38fcc6917905`، آخرین origin/main هنگام شروع.
- Worktree تمیز: `.artifacts/step10g-retest-20261021`؛ branch: `ai/step10g-retest-20261021`؛ upstream: `origin/main`.
- Workspace اصلی روی `main@e23beec` و ۲۲ فایل dirty قبلی است؛ آن فایل‌ها خارج از این تحویل حفظ می‌شوند.
- محیط: Backend واقعی در ASP.NET Core TestServer، session/SQL Server LocalDB اختصاصی، HTTP واقعی به AI مستقر.
- BaseUrl واقعی Auth: `https://195.177.255.98`؛ OpenAPI واقعی: `https://195.177.255.98/openapi.json`.
- عنوان AI: MakanApp AI Core Service؛ نسخهٔ سند سرویس 1.0.0 و OpenAPI 3.1.0؛ revision runtime قابل اثبات نبود.
- معماری Production همچنان Backend → WireGuard → `http://10.77.0.2:8001` است.
- هیچ key، JWT، OTP، session token، body خصوصی یا Credential در گزارش وجود ندارد.

## Contract و پیش‌نیازها

[ممیزی ۱۲ operation و nested DTOها](STEP10G_AI_CONTRACT_AUDIT.md) پیش از implementation نوشته شد.
OpenAPI قابل دریافت است؛ TLS با اعتبارسنجی عادی موفق است. `AI_SERVICE_AUTH_CONFIGURED=true` فقط
وجود پیکربندی قابل استفاده برای generator است، نه پذیرش آن توسط AI.

| ID | Scenario | Expected | Actual | نتیجه |
|---|---|---|---|---|
| C01 | OpenAPI واقعی | JSON قابل Parse | 3.1.0 / service 1.0.0 / دوازده POST | PASS |
| C02 | header نسخه | required const v1 | در هر ۱۲ operation مستند است | PASS در ممیزی؛ runtime مستقل BLOCKED |
| C03 | correlation header | استخراج دقیق | optional در OpenAPI؛ Backend header/body و echo را بررسی می‌کند | PASS محلی؛ runtime مثبت BLOCKED |
| C04 | scope لازم | makan.ai.invoke | در قرارداد Backend هست؛ در OpenAPI زنده نیست | AI_CONTRACT_MISMATCH R2 |
| C05 | collectionهای پاسخ | سازگاری requiredness | OpenAPI omitted را مجاز و Backend رد می‌کند | AI_CONTRACT_MISMATCH R1 |
| H01 | GET /api/v1/health | پاسخ قابل بررسی طبق سند | 401 HTML / Basic challenge در Nginx | BLOCKED |
| H02 | GET /api/v1/ready | پاسخ قابل بررسی طبق سند | 401 HTML / Basic challenge در Nginx | BLOCKED |

Schema هر دو Health/Ready برابر `{}` است؛ DTO یا معنای dependency اختراع نشده و JWT برای دورزدن Basic ارسال نشده است.

## Service Auth واقعی

Auth تنها در process ابزار و از secure configuration موجود خوانده شد؛ هیچ مقدار تنظیمی عوض نشد.
generator فعلی HS256، issuer/subject makan-backend، audience makan-ai، scope makan.ai.invoke،
iat/nbf/exp/jti/kid و عمر پیش‌فرض ۱۲۰ ثانیه دارد. token cache ندارد؛ برای invocation تازه می‌سازد.
کلید قبلی سمت Backend برای امضا لازم نیست؛ پذیرش دو کلید هنگام rotation مسئولیت AI است و در این اجرا ثابت نشد.

جدول زیر اجرای ابزار اصلاح‌شده است. body این سه probe عمداً `{}` بود و provider invocation معتبر نمی‌سازد.

| ID | Scenario | Actor / Path | Expected | Actual | زمان ms | نتیجه |
|---|---|---|---|---|---:|---|
| A01 | Missing JWT | service / POST /api/v1/ai/chat | 401 AI_SERVICE_UNAUTHORIZED | همان | 546.34 | PASS |
| A02 | Malformed JWT | service / همان route | 401 AI_SERVICE_UNAUTHORIZED | همان | 115.87 | PASS |
| A03 | Current credential control | Backend service / همان route | 422 AI_INVALID_REQUEST | 401 AI_SERVICE_UNAUTHORIZED | 114.78 | FAIL؛ مانع محیط |
| A04 | expired/signature/kid/issuer/audience/subject | service | 401 طبق قرارداد | NOT_RUN پس از شکست کنترل معتبر | — | BLOCKED |
| A05 | valid credential without scope | service | 403 AI_SERVICE_FORBIDDEN | NOT_RUN؛ کلید معتبر اثبات نشد | — | BLOCKED |
| A06 | missing/wrong version و correlation mismatch | service | خطای کنترل‌شدهٔ قرارداد | NOT_RUN | — | BLOCKED |
| A07 | previous key / rotation | service | پذیرش کلید قبلی در پنجرهٔ مجاز | NOT_RUN؛ محیط rotation مهیا نیست | — | BLOCKED |

علت دقیق 401 از پاسخ امن معلوم نیست؛ mismatch امضا، kid، claim یا ساعت حدس زده نمی‌شود.
نیاز بعدی هماهنگ‌سازی secure configuration دو سمت توسط مالک/تیم AI است، بدون ارسال Secret در چت.
اجرای اولیهٔ نسخهٔ قدیمی ابزار، پس از چند negative به JsonReaderException روی پاسخ غیرJSON رسید؛
HTTP status آن پاسخ capture نشده بود و حدس زده نشد. ابزار اکنون status را مستقل از Parse حفظ می‌کند،
رویداد امن NON_CONTRACT_RESPONSE دارد و بدون کنترل معتبر، negativeهای claim را PASS جا نمی‌زند.
هیچ retry یا flood برای ایجاد 429/503 انجام نشد.

## هر ۱۲ Happy Path

تمام actor/resourceها synthetic هستند. پیشوند Backend و AI در جدول `/api/v1/ai` است؛ تمام روش‌ها POST.
برای ردیف‌های اجرا‌نشده، HTTP/requestId/correlationId/duration و contract validation runtime برابر N/A است.
علت مشترک BLOCKED، شکست A03 است؛ NOT_RUN به معنی FAIL مدل یا رفع شکست تاریخی نیست.

| ID | Scenario / Actor | Backend Path | AI Operation | Expected | Actual / نتیجه |
|---|---|---|---|---|---|
| E01 | Generic Chat / Student, Parent, Teacher, Manager | /chat | /chat | 200، envelope و نقش/سازمان معتبر | NOT_RUN / BLOCKED |
| E02 | Student Analyze / self | /students/analyze | /students/analyze | 200، دادهٔ learner خود | NOT_RUN / BLOCKED |
| E03 | Study Plan / Student | /study-plan | /study-plan | 200، برنامهٔ مجاز | NOT_RUN / BLOCKED |
| E04 | Homework Explain / Student | /homeworks/{assignmentId}/explain | /homeworks/explain | 200، recipient مجاز | NOT_RUN / BLOCKED |
| E05 | Exam Prepare / Student | /exams/{examId}/prepare | /exams/prepare | 200، بدون answer key | NOT_RUN / BLOCKED |
| E06 | Parent Child Summary / linked child | /parents/child-summary | /parents/child-summary | 200، رابطهٔ فعال و دادهٔ released | NOT_RUN / BLOCKED؛ retest اختصاصی انجام نشد |
| E07 | Teacher Class Summary / assigned class | /teachers/classes/{classId}/summary | /teachers/class-summary | 200، کلاس جاری | NOT_RUN / BLOCKED |
| E08 | Teacher Student Insight / enrolled learner | /teachers/classes/{classId}/students/{studentId}/insight | /teachers/student-insight | 200، assignment و enrollment معتبر | NOT_RUN / BLOCKED |
| E09 | Teacher Activities / assigned class | /teachers/classes/{classId}/activity-suggestions | /teachers/activity-suggestions | 200، فعالیت آموزشی برای کلاس مجاز | NOT_RUN / BLOCKED؛ retest اختصاصی انجام نشد |
| E10 | Manager School Summary / current org | /managers/school-summary | /managers/school-summary | 200، سازمان جاری | NOT_RUN / BLOCKED |
| E11 | Manager Class Insight / current org class | /managers/classes/{classId}/insight | /managers/class-insight | 200، کلاس مجاز | NOT_RUN / BLOCKED؛ retest اختصاصی انجام نشد |
| E12 | Manager Student Insight / current org learner | /managers/classes/{classId}/students/{studentId}/insight | /managers/student-insight | 200، learner مجاز | NOT_RUN / BLOCKED |

نتیجه: **۱۲/۱۲ mapped، صفر/۱۲ Real AI Happy Path passed**. هیچ inference جعلی جای این نتیجه قرار نگرفت.

## Authorization و Tenant Isolation واقعی Backend

حالت authorization-only روی Controller و SQL واقعیِ دیتابیس تست، با AI target بستهٔ loopback اجرا شد.
برای هر ردیف انتظار 403/404 (و suspended user برابر 401) و عدم افزایش شمار HTTP خروجی بود؛
هر ۱۶ مورد PASS و `realAiCalls=0` شد. این‌ها integration محلی Backend هستند، نه پاسخ مثبت AI مستقر.

| ID | Scenario | Actor | Backend Path پس از /api/v1 | نتیجه |
|---|---|---|---|---|
| N01 | unrelated child | Parent | /guardian/children/{id}/select | PASS |
| N02 | foreign organization child | Parent | همان selector | PASS |
| N03 | foreign published homework | Student | /ai/homeworks/{id}/explain | PASS |
| N04 | foreign published exam | Student | /ai/exams/{id}/prepare | PASS |
| N05 | unassigned class | Teacher | /ai/teachers/classes/{id}/summary | PASS |
| N06 | foreign class | Teacher | همان route | PASS |
| N07 | unassigned activity | Teacher | /ai/teachers/classes/{id}/activity-suggestions | PASS |
| N08 | unenrolled student | Teacher | /ai/teachers/classes/{id}/students/{id}/insight | PASS |
| N09 | foreign school class | Manager | /ai/managers/classes/{id}/insight | PASS |
| N10 | foreign learner | Manager | /ai/managers/classes/{id}/students/{id}/insight | PASS |
| N11 | role mismatch | Student | /ai/managers/school-summary | PASS |
| N12 | unpublished homework | Student | /ai/homeworks/{id}/explain | PASS |
| N13 | revoked guardian | Parent | /ai/parents/child-summary | PASS |
| N14 | ended membership | Teacher | /ai/teachers/classes/{id}/summary | PASS |
| N15 | suspended membership | Manager | /ai/managers/school-summary | PASS |
| N16 | suspended account | Student | /ai/students/analyze | PASS؛ 401 |

Privacy همان اجرا PASS بود. Generic Chat فقط Message/ConversationId می‌پذیرد؛ actor و scope را از
AccessContext معتبر می‌سازد و context آموزشی آن Empty است. prompt فرمانی برای query تازه، اجرای SQL،
خواندن history یا دادن مجوز نیست. builderهای Parent/Teacher/Manager دادهٔ private chat یا credentials
را query نمی‌کنند؛ release و MaxScore نسخه حفظ می‌شوند. بررسی مثبت payload/Trace واقعی مدل تا رفع Auth معلق است.

نکتهٔ Generic Chat: `conversationId` فعلاً یک شناسهٔ opaque ورودی/echo است؛ Backend با آن history
را query نمی‌کند و در این slice resolver مالکیت Copilot history وجود ندارد. از OpenAPI نمی‌توان ثابت کرد
AI با این ID هیچ حافظه‌ای نمی‌خواند؛ بنابراین authorization تاریخچهٔ آینده یا privacy حافظهٔ AI ادعا نمی‌شود.
اگر بعداً history فعال شود، ID تنها مجوز نیست و قرارداد مالکیت باید مستقل و صریح باشد؛ در این مرحله
هیچ endpoint/history store یا query تازه‌ای برای حدس‌زدن این رفتار ساخته نشده است.

## Resilience و Observability

| حوزه | شاهد این تحویل | حد نتیجه |
|---|---|---|
| Timeout / slow body / cancellation | AiServiceClientTests؛ timeout محدود و CTS تا خواندن body | PASS کنترل‌شده؛ نه fault تزریق‌شده به AI عمومی |
| 429 | status/code و Retry-After داخلی، یک invocation | PASS محلی؛ public header تازه وعده داده نمی‌شود |
| 503 / connection failure | mapping امن، بدون raw exception/provider body | PASS محلی |
| malformed/schema/grounding | JSON/metadata/enum/envelope/source checks | PASS محلی؛ رخداد مدل جدیدی مشاهده نشد |
| Circuit | همان singleton موجود؛ threshold=3 و break=30؛ تست open/recovery و یک rejection بدون HTTP دوباره | PASS محلی؛ single-probe half-open توزیع‌شده ادعا نمی‌شود |
| RequestId / CorrelationId | server-generated requestId و correlation جاری، header/body/echo؛ Activity و log scope | PASS محلی؛ echo مثبت AI مستقر BLOCKED |
| Tracing | ActivitySource MakanApp.AiService، child از Backend، status واقعی و code امن | PASS Unit؛ collector نصب/تست نشده |
| Metrics | requests/duration با operation/outcome/error.code؛ circuit.rejections جدا | PASS Unit؛ timeout/rate/failure از همان tags قابل شمارش‌اند |
| Logging | success و failure ساختاریافته، شکست با Error، بدون Exception/raw URL/body/header | PASS Unit با sentinelهای نشت |

Retry خودکار در Controller/Application/adapter اضافه نشده است. تنظیم/رفتار retry واقعی Nginx از
OpenAPI معلوم نیست و تغییر نکرده است. استقلال Identity بعد از خطای AI در تست موجود /me بررسی می‌شود؛
Regression عمومی ماژول‌ها جای قطع واقعی WireGuard/AI و آزمون همهٔ ماژول‌ها نیست.

## Validation و Git

Restore و Build اولیه موفق؛ ۱۴۹ focused AI Unit موفق، صفر failed/skipped و Build صفر warning/error.
در اولین focused، یک تست timeout به fallback آدرس وابسته بود و fail شد؛ URL تستی صریح شد، assertion
بودجهٔ ۱۸۰ ثانیه و تعداد درخواست حفظ شدند. دو warning سبک Assert.Single نیز اصلاح شدند، نه suppress.
Regression کامل: **۷۴۰ Unit + ۱۲۵۸ Integration + ۱۴ Architecture = ۲۰۱۲ passed**؛ failed/skipped=0.
Final restore، Final build Solution و build ابزار هر دو موفق و بدون warning/error بودند؛
Authorization-only پس از build نهایی نیز با ۱۶ negative و privacy، صفر HTTP خروجی، موفق تکرار شد.
Final test suite نیز موفق تمام شد: **۷۴۰ Unit + ۱۲۵۸ Integration + ۱۴ Architecture = ۲۰۱۲ passed**؛
failed=0 و skipped=0. نتیجهٔ هر سه پروژه در `TestResults/step10g-retest-final.trx` ثبت شد؛
اتمام Integration در `2026-10-21T16:20:13+03:30` بود. بنابراین هر دو اجرای کامل Regression و Final موفق بودند.

## محدودیت و ادامه

- Auth A03 مانع اصلی Happy Path است؛ علت دقیق نیازمند بررسی secure config سمت AI/Backend است.
- Health/Ready عمومی 401 و اختلاف‌های R1/R2 مستقل باقی‌اند.
- key rotation، 403 با credential معتبر، guardهای نسخه و تمام پاسخ‌های مدل NOT_RUN هستند.
- هیچ migration تازه، تغییر Schema، دسترسی AI به SQL، تغییر MeliPayamak، تغییر Frontend یا استقرار سرور انجام نشد.
- Public/Production RAG: `RAG_BACKEND_SOURCE_LIFECYCLE_DEFERRED`؛ ProductionReady=NO.
- گام بعد: هماهنگی Auth بدون افشای Secret، Auth-only، سه focused مستقل و سپس ۱۵ invocation مثبت برای ۱۲ operation.

## گزارش آموزشی توسعه‌دهنده

### ۱. خلاصه مرحله

پیش‌تر runner فقط localhost را قبول می‌کرد و پاسخ غیرJSON می‌توانست evidence را از بین ببرد.
اکنون target صریح HTTPS، کنترل معتبر Auth، ثبت امن status، isolation بیشتر و شواهد مکتوب داریم؛
موفقیت مدل هنوز نداریم و BLOCKED با موفقیت کد یکسان نیست.

### ۲. معماری

تغییر runtime فقط Infrastructure است: transport/telemetry و انتخاب config متعلق به این لایه‌اند.
Domain، Application، Controller و dependency flow فعلی تغییر نکردند. runner/test پروژهٔ عملیاتی مستقل است، نه business module تازه.

### ۳. فایل‌های مهم جدید

- `tools/ai/MakanApp.AiIntegrationProbe/Program.cs`: composition ابزار موجود؛ opt-in و scope اجرای امن را مالک است.
- `LiveAiTargetPolicy.cs`: guard خالص target؛ دو تأیید مستقل flag/config و منع credential/query در URL را آموزش می‌دهد.
- `LiveAiErrorContract.cs`: parsing خروجی تشخیصی با allowlist؛ خرابی HTML نباید status را گم یا body را افشا کند.
- `LiveAiScenario.cs`: session، داده synthetic، Controller و SQL واقعی؛ انتخاب test mode را از production جدا می‌کند.
- `LiveAiObservation.cs`: payload/metric فقط در حافظه؛ خروجی metadata به‌جای dump محتوا.
- `ServiceAuthProbe.cs`: کنترل معتبر قبل از تفسیر negativeها؛ 401 مشترک، اثبات claim validation نیست.
- سه فایل Unit جدید در `tests/MakanApp.UnitTests/Ai`: policy، parsing امن و telemetry/failure logging را مستقل از شبکه می‌سنجند.
- دو سند STEP10G و `tools/ai/README.md`: قرارداد، اجرا و روش بازتولید بدون Secret.

### ۴. فایل‌های مهم تغییرکرده

`AiServiceClient.cs`، `AiServiceTelemetry.cs` و `AiServiceOptions.cs` فقط Activity/log/circuit rejection
و حذف fallback آدرس را اضافه می‌کنند. `AiServiceClientTests.cs` آدرس synthetic صریح می‌دهد؛
Unit csproj دو helper خالص runner را Link می‌کند، نه اینکه وابستگی Web host به Unit اضافه کند.

### ۵. جریان اجرا

`LiveAiScenario → AiController → AiChatService/AiRoleContextService → AccessContext و ContextBuilderها
→ گزارش/EF و SQL Server تست → HmacAiServiceCredentialProvider → AiServiceClient
→ AiResponseGrounding → Application result → HTTP response`.
در negativeها جریان پیش از HTTP AI قطع شد. در کنترل Auth مستقیم، AI credential را رد کرد؛ جریان مثبت تا مدل طی نشد.

### ۶. دیتابیس

Schema/Table/PK/FK/Index/rowversion جدید نداریم. دادهٔ تست از Domain موجود ساخته و migrationهای موجود
فقط روی LocalDB اختصاصی fixture اجرا می‌شوند؛ Production و دیتابیس عادی توسعه مقصد تست نیستند.

### ۷. Endpointها

هیچ endpoint عمومی جدید یا قرارداد Controller عوض نشد. ۱۲ route، request/response و status در ممیزی
و routeهای Backend در جدول E ثبت‌اند. session/role/resource authorization همان قواعد موجود است.

### ۸. مفاهیم واقعی

Options، Service JWT، CancellationToken، ActivitySource، Meter، ILogger scope، TestServer، LocalDB،
negative authorization و ErrorResponse allowlist واقعاً استفاده شدند؛ RAG، ML، microservice و retry framework جدید ساخته نشد.

### ۹. تصمیم‌های طراحی

به‌جای runner موازی، کد موجود با مجوز مالک در Worktree تمیز ثبت شد. حالت تاریخی export payload حذف شد؛
هزینهٔ آن از دست‌دادن dump تشخیصی است ولی خطر ذخیرهٔ داده خصوصی کمتر است. helperهای خالص با Compile Link
تست می‌شوند تا Unit به Console/Integration host وابسته نشود. Collector جدید اضافه نشد؛ میزبان باید ActivitySource را subscribe کند.

### ۱۰. تست‌ها

۱۹ Unit policy، ۲۱ Unit error-contract و ۱۰ Unit telemetry/options جدیدند. آن‌ها loopback/HTTPS opt-in،
userinfo/query/fragment، codeهای مجاز، HTML/JSON خراب، child span، failure Error log، status و عدم نشت را می‌سنجند.
۱۶ negative integration در runner ثابت می‌کنند context غیرمجاز هیچ HTTP خروجی نمی‌سازد؛ با تست مدل اشتباه نشوند.

### ۱۱. Validation

فقط نتایج بخش Validation معتبرند؛ testهای واقعی BLOCKED به Pass تبدیل نشده‌اند. تست‌های معمولی CI به Secret یا AI زنده وابسته نیستند.

### ۱۲. Git

فقط فایل‌های همین Worktree و ابزار C# تأییدشده وارد تحویل می‌شوند؛ hash نهایی و نتیجه Push در گزارش نهایی گفتگو
و با `git log -1 -- docs/ai-handoff/STEP10G_REAL_AI_E2E_REPORT.md` قابل پیگیری‌اند. فایل‌های dirty اصلی stage نمی‌شوند.

### ۱۳. محدودیت‌ها

Auth واقعی، schema اختلاف‌دار، readiness نامعلوم و نبود شاهد مدل، مستقل از موفقیت build باقی‌اند.
کلید خصوصی از هیچ فایل AI برداشت نمی‌شود و schema برای عبور مدل سست نشده است.

### ۱۴. نکات آموزشی

1. وجود Secret یعنی configured، نه authenticated.
2. body نامعتبر می‌تواند آزمون header را به‌اشتباه سبز کند؛ guard باید مستقل سنجیده شود.
3. error logger باید metadata بسازد، نه Exception/response خام را serialize کند.
4. requestId در Trace مفید است ولی در Metric label کاردینالیتی نامحدود ایجاد می‌کند.
5. وجود OpenAPI یا health=200، اثبات Happy Path مدل نیست.
6. آدرس محیط config است؛ default داخل کد نباید به محیط دیگری وصل شود.

### ۱۵. ترتیب مطالعه

`tools/ai/README → LiveAiTargetPolicy → Program → ServiceAuthProbe → LiveAiScenario
→ AiController → ContextBuilderها → AiServiceClient → AiServiceTelemetry → سه Unit جدید → گزارش اجرا`.

### ۱۶. مرحله بعد

فقط رفع اعتماد Service Auth در محیط امن و retest همین مسیر؛ افزودن RAG، تغییر authorization،
دادن دسترسی SQL به AI، Deploy سرور یا ادعای Production Ready در این مرحله مجاز/اثبات‌شده نیست.
