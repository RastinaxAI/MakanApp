# STEP 10G — شواهد اتصال واقعی Backend به AI

> شاهد تاریخیِ اجرای همان تاریخ است، نه وضعیت جاری. برای تفکیک implementation تکمیل‌شده،
> Service Auth تأییدشدهٔ قبلی و پذیرش واقعی Deferred به علت Provider Timeout،
> [گزارش Consolidation نهایی](../release/BACKEND_FINAL_CONSOLIDATION_FA.md) را بخوانید. هیچ PASS واقعی تازه‌ای از این بازبینی نتیجه نمی‌شود.

> بازبینی جدید ۲۰۲۶-۱۰-۱۶: [گزارش retest نسخهٔ دقیق b346df8](12_STEP10G_FINAL_REAL_E2E_REPORT.md) مرجع وضعیت جاری است. هر سه درخواست focused به AI واقعی رسیدند، ولی با AI_MODEL_UNAVAILABLE و دستهٔ configuration شکست خوردند؛ رفع شکست تاریخی AI_RESPONSE_INVALID هنوز اثبات نشده است. بدنهٔ زیر شواهد تاریخی ۲۰۲۶-۱۰-۱۴ است و اعداد/traceهای آن به اجرای جدید نسبت داده نمی‌شوند.

تاریخ اجرا: 2026-10-14. محیط: `Local Integration`. وضعیت: **BLOCKED_AFTER_BACKEND_STEP_10G**.

این گزارش پذیرش کامل یا Production Ready نیست. نتیجه تست‌های Backend از نتیجه اتصال واقعی AI جداست.

## ۱. خلاصه مرحله و baseline

- Backend starting/final HEAD: `8ae6882f82c62539d596827b829e056368da4427`؛ commit جدید ساخته نشده است.
- branch: `main`؛ upstream: `origin/main`؛ baseline پس از fetch برابر HEAD و worktree در شروع clean بود.
- AI مورد انتظار handoff: `8b3d75c58288d4692603e32bf828fc5c9c40c5d9`.
- AI واقعی ممیزی‌شده: `59a3e2c9d91b101b83787b16e2367fc08f46d412`، branch `main`، worktree clean.
- commit مورد انتظار ancestor فعلی AI است. دو commit بعدی ابزار ارزیابی/مستندات‌اند؛ `app` و فایل‌های requirements نسبت به commit مورد انتظار تغییر نکرده‌اند.
- listener محلی به process مربوط به Python/Uvicorn و virtualenv همین مخزن AI وصل بود؛ فایل‌های Python قبل از شروع process نوشته شده بودند.
- OpenAPI دریافت‌شده از سرویس، پس از decode صحیح UTF-8، با OpenAPI تولیدشده **در حافظه و بدون نوشتن bytecode** از کد فعلی AI، صفر اختلاف ساختاری داشت: ۱۵ path و ۶۴ schema. فایل قدیمی `docs/handoff/openapi-v1.json` در AI، declarationهای جدید auth/header را ندارد و به‌تنهایی مدرک نسخه runtime نیست.
- AI یک endpoint عمومی برای اعلام Git SHA ندارد؛ تطبیق runtime از process/source/OpenAPI و رفتار قرارداد انجام شد، نه از ادعای self-reported SHA.
- BaseUrl: `http://127.0.0.1:8000`.
- `GET /api/v1/health`: `200`, `status=ok`.
- `GET /api/v1/ready`: `200`, `status=ready`.

قبلاً فقط اعتماد مستقل Backend و Fake-AI وجود داشت. اکنون شواهد واقعی مثبت و منفی، ابزار opt-in بازتولید، و دو اصلاح اثبات‌شده HTTP adapter داریم؛ سه عملیات واقعی هنوز gate را مسدود می‌کنند.

## ۲. معماری و dependency flow

تغییر production فقط در `Infrastructure/Ai/AiServiceClient` است. timeout و خطای transport مسئولیت Infrastructure هستند؛ هیچ business rule، DTO، Controller، Domain model یا dependency direction تغییر نکرده است.

```text
Synthetic HTTP client
-> session authentication
-> AiController
-> AiChatService / AiRoleContextService
-> GuardianAccessContextResolver / OrganizationService
-> StudentContextBuilder / ParentChildContextBuilder / Teacher*ContextBuilder / Manager*ContextBuilder
-> authorized report ports / EfAiScopeStore
-> MakanDbContext / isolated SQL Server
-> HmacAiServiceCredentialProvider
-> AiServiceClient
-> real AI FastAPI / provider or existing deterministic AI path
-> strict response validation
-> public HTTP response
```

وجود پاسخ `200` به‌تنهایی اثبات فراخوانی provider در هر operation نیست؛ بعضی مسیرهای AI برای داده ناکافی fallback قطعی دارند. تعداد درخواست AI با تعداد قطعی درخواست OpenRouter برابر فرض نشده است.

## ۳. فایل‌های مهم ایجادشده

| فایل | مسئولیت و نکته آموزشی |
|---|---|
| `tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj` | executable آزمایشی .NET 9 با reference به fixture موجود؛ خارج از solution تا تست عادی هزینه مدل ایجاد نکند. پکیج جدیدی اضافه نمی‌کند. |
| `tools/ai/MakanApp.AiIntegrationProbe/Program.cs` | composition root تست؛ خواندن secret بیرون Git، نصب adapter واقعی به‌جای recording client، و lifecycle دیتابیس تست. |
| `tools/ai/MakanApp.AiIntegrationProbe/LiveAiScenario.cs` | ورود OTP آزمایشی از API، workspace/guardian/enrollment/teacher fixture، publication، عملیات واقعی و اثبات denial پیش از network. |
| `tools/ai/MakanApp.AiIntegrationProbe/LiveAiObservation.cs` | بررسی payload/header/scope/MaxScore و metricها در حافظه؛ ثبت فقط نتیجه و error code امن، بدون token/payload. |
| `tools/ai/README.md` | فرمان اجرا، هزینه احتمالی، محدودیت subset و هشدار حذف دیتابیس اختصاصی تست. |
| همین گزارش | مرزبندی شواهد واقعی، fault-injection، موارد اجرا‌نشده و handoff blocker. |

## ۴. فایل‌های مهم تغییرکرده و ایرادهای بازتولیدشده

`MakanApp.Infrastructure/Ai/AiServiceClient.cs`:

1. با `ResponseHeadersRead`، دریافت header پایان پاسخ نیست. قبلاً خواندن stream و deserialization از cancellation کاربر استفاده می‌کردند و deadline داخلی پس از دریافت header اعمال نمی‌شد. اکنون همان linked timeout token تا پایان خواندن بدنه ادامه دارد.
2. قطع اتصال هنگام خواندن بدنه قبلاً `IOException` خام تولید می‌کرد؛ اکنون `IOException` و `HttpRequestException` این بخش به `AI_SERVICE_UNAVAILABLE` تبدیل و در circuit/metric ثبت می‌شوند.
3. لغو واقعی caller همچنان `OperationCanceledException` باقی می‌ماند؛ timeout داخلی `AI_SERVICE_TIMEOUT` است. هیچ retry اضافه نشده است.

دو regression test ابتدا عمداً اجرا شدند و شکست خوردند: timeout بدنه exception نمی‌داد و connection failure از نوع `IOException` بود. پس از اصلاح سبز شدند.

`tests/MakanApp.UnitTests/Ai/AiServiceClientTests.cs`: تست body timeout، body connection failure، caller cancellation، connection failure، correlation mismatch، circuit recovery/reset و سه status mapping اضافه شد.

`tests/MakanApp.IntegrationTests/AiChatEndpointsTests.cs`: نگاشت عمومی `AI_INVALID_REQUEST` به 400 و service-auth 401/403 به **503 عمومی Backend** نیز تست می‌شود.

## ۵. G2 — احراز هویت واقعی سرویس

| سناریو روی AI واقعی | نتیجه |
|---|---|
| کلید جاری | PASS؛ ابتدا auth-only probe با body نامعتبر به validation با 422 رسید؛ سپس درخواست‌های واقعی مجاز موفق شدند. |
| Authorization غایب | PASS: 401 / `AI_SERVICE_UNAUTHORIZED` |
| JWT malformed | PASS: 401 / `AI_SERVICE_UNAUTHORIZED` |
| expired | PASS: 401 / `AI_SERVICE_UNAUTHORIZED` |
| bad signature | PASS: 401 / `AI_SERVICE_UNAUTHORIZED` |
| unknown kid | PASS: 401 / `AI_SERVICE_UNAUTHORIZED` |
| wrong issuer | PASS: 401 / `AI_SERVICE_UNAUTHORIZED` |
| wrong audience | PASS: 401 / `AI_SERVICE_UNAUTHORIZED` |
| wrong subject | PASS: 401 / `AI_SERVICE_UNAUTHORIZED` |
| JWT معتبر بدون scope لازم | PASS: 403 / `AI_SERVICE_FORBIDDEN` |
| rotation | NOT EXECUTED؛ previous-key overlap/retirement بدون تغییر تنظیمات سرویس درحال اجرا قابل اثبات نبود. credential مشترک تغییر نکرد. |

negative auth/header probeها با PowerShell در حافظه اجرا شدند؛ خروجی تنها status، stable code و نتیجه بررسی echo بود. secret/JWT در خروجی یا فایل ذخیره نشد. پاسخ‌های این probeها key/token را echo نکردند.

شناسه سرویس همان قرارداد است: HS256، issuer/subject=`makan-backend`، audience=`makan-ai`، scope=`makan.ai.invoke`، lifetime پیش‌فرض ۱۲۰ ثانیه و range پشتیبانی‌شده ۳۰ تا ۳۰۰. JWT واقعی با `HmacAiServiceCredentialProvider` موجود تولید شد و UserSession token به AI forward نشد.

نام تنظیمات، بدون مقدار secret:

```text
AiService:BaseUrl
AiService:Authentication:Issuer
AiService:Authentication:Audience
AiService:Authentication:ServiceName
AiService:Authentication:Scope
AiService:Authentication:CurrentKeyId
AiService:Authentication:CurrentKey
AiService:Authentication:TokenLifetimeSeconds
```

## ۶. G3 — header و شناسه پاسخ

- نسخه معتبر `v1`، `application/json` و correlation مساوی در درخواست‌های موفق واقعی: PASS.
- missing/invalid contract version: هر دو `422 / AI_INVALID_REQUEST`.
- correlation header/body mismatch: `422 / AI_INVALID_REQUEST`.
- در عملیات موفق، `requestId` و `correlationId` خروجی Backend با مقادیر مشاهده‌شده در درخواست واقعی AI مقایسه شدند.
- رد response identity نامعتبر با تست مستقل adapter بررسی شد؛ این fault-injection است، نه خراب کردن پاسخ سرویس واقعی.

## ۷. G4 — نتیجه هر ۱۲ عملیات

routeهای جدول **Backend public API** هستند؛ route عمومی جدیدی ایجاد نشده است. همه `POST` هستند، session authentication و role/resource authorization موجود اجرا شده‌اند. request/response/schema کامل بدون تغییر در `03_AI_API_CONTRACT.md` و `AI_INTEGRATION_CONTRACT_V1.md` قرار دارد.

| عملیات | route عمومی | نتیجه واقعی |
|---|---|---|
| Generic Chat | `/api/v1/ai/chat` | PASS: Student، Parent، Teacher، Manager هر چهار مورد 200 |
| Student Analyze | `/api/v1/ai/students/analyze` | PASS: 200 |
| Study Plan | `/api/v1/ai/study-plan` | PASS: 200 |
| Homework Explain | `/api/v1/ai/homeworks/{assignmentId}/explain` | PASS: 200؛ MaxScore نسخه published برابر 10 مشاهده شد. |
| Exam Prepare | `/api/v1/ai/exams/{examId}/prepare` | PASS: 200؛ MaxScore نسخه published برابر 30 مشاهده شد. |
| Parent Child Summary | `/api/v1/ai/parents/child-summary` | FAIL: Backend 503 / `AI_SERVICE_UNAVAILABLE`؛ در اجرای تشخیصی AI برابر 502 بود. code دقیق upstream در این اجرا ثبت نشد؛ logger تشخیصی آن زمان آن را در allowlist نداشت. |
| Teacher Class Summary | `/api/v1/ai/teachers/classes/{classId}/summary` | PASS: 200 |
| Teacher Student Insight | `/api/v1/ai/teachers/classes/{classId}/students/{studentId}/insight` | PASS: 200 |
| Teacher Activity Suggestions | `/api/v1/ai/teachers/classes/{classId}/activity-suggestions` | FAIL: AI 502 / `AI_RESPONSE_INVALID` → Backend 503 / `AI_SERVICE_UNAVAILABLE` |
| Manager School Summary | `/api/v1/ai/managers/school-summary` | PASS: 200 |
| Manager Class Insight | `/api/v1/ai/managers/classes/{classId}/insight` | FAIL: AI 502 / `AI_RESPONSE_INVALID` → Backend 503 / `AI_SERVICE_UNAVAILABLE` |
| Manager Student Insight | `/api/v1/ai/managers/classes/{classId}/students/{studentId}/insight` | PASS: 200 |

۱۲ operation: **۹ PASS، ۳ FAIL**. با احتساب Chat چهار نقش، ۱۵ case مثبت اصلی داریم: ۱۲ موفق و ۳ ناموفق. علاوه بر آن، یک smoke اولیه Manager School موفق و یک diagnostic تکی Parent ناموفق اجرا شدند؛ مجموع HTTP business invocationهای مسیر Backend→AI برابر ۱۷ بود. این عدد شامل auth/header probeها و health نیست و شمار قطعی LLM call نیست.

Student مثبت کامل است؛ Parent، Teacher و Manager acceptance نقش‌محور هنوز partial هستند. موفقیت Chat آن نقش جایگزین عملیات تخصصی ناموفق نیست.

## ۸. G5 — authorization پیش از network

همه ۱۲ case زیر PASS بودند؛ در اجرای مستقل این matrix شمار درخواست واقعی AI برابر **صفر** ماند:

1. Parent cross-child؛ درخواست انتخاب child غیرمجاز رد شد.
2. Teacher cross-class.
3. Teacher cross-organization.
4. Teacher student بدون Enrollment در class.
5. Manager cross-school/foreign class.
6. Manager foreign learner.
7. role mismatch: Student روی endpoint Manager.
8. homework منتشرنشده.
9. GuardianRelation لغوشده در request بعدی.
10. Membership خاتمه‌یافته.
11. Membership suspended.
12. User suspended؛ 401 پیش از AI.

OrganizationPersonId از Person کاربر جدا resolve و در payload کنترل شد؛ UserId به‌جای learner ID استفاده نشد. تست‌های denial از HTTP endpoint موجود عبور کردند و هیچ داده سازمان عادی توسعه را نخواندند.

## ۹. G6 — resilience و محدودیت شواهد

| موضوع | شاهد |
|---|---|
| 400/422، 401/403، 429، 404/409/500/502/503، 504 | mapping در unit tests adapter؛ public mapping در integration tests؛ 502 واقعی AI نیز 503 عمومی شد. |
| timeout قبل/بعد header | fault-injection مستقل؛ اصلاح body timeout با red/green اثبات شد. |
| connection failure قبل/بعد header | fault-injection مستقل؛ پیام خام transport در پیام عمومی قرار نمی‌گیرد. |
| malformed JSON/schema و identity mismatch | تست مستقل adapter، نه دست‌کاری AI. |
| circuit | سه failure، short-circuit بدون call، گذشت ۳۰ ثانیه با ساعت تست، recovery و reset. state process-local است. |
| POST retry | adapter retry ندارد؛ تست call-count برای failureها یک invocation را ثابت می‌کند. |
| outage و سایر ماژول‌ها | regression کامل اجرا می‌شود؛ سناریوی live مختص قطع AI و هم‌زمانی Identity/Academic/Assessment/Messaging جداگانه اجرا نشده است. |

سرویس واقعی برای تزریق 429/timeout/outage دست‌کاری نشد. نتایج fault-injection نباید real-provider load/failure acceptance نامیده شوند.

## ۱۰. G7/G8 — privacy، telemetry و load

- بررسی actual outgoing context پیش از ارسال: scope مجاز، نقش، correlation، publication، سقف نمره و نبود مقدار secret/session/OTP/phone و draft sentinel. guard در حافظه است و payload را persist نمی‌کند.
- Backend log privacy در smoke، matrix منفی و diagnostic موفق Manager Student بررسی و PASS شد. در اجرای fail-fast، ادعای بررسی نهایی همه log snapshotها نشده است.
- metric واقعی Manager Student مشاهده شد: labelها محدود به `operation`, `outcome`, `error.code`؛ شناسه کاربر/کلاس/correlation label نبود. source allowlist AI نیز read-only بررسی شد.
- sink لاگ/trace واقعی process AI در دسترس probe نبود؛ نبود PII در **تمام** AI/provider logها قابل اثبات نشد. G7 سراسری هنوز PASS نیست.
- شواهد context محدود به fixture و field guard است؛ تمام انواع محتوای خصوصی Messenger/moderation/payment به‌صورت canary مستقل seed نشده‌اند. قرارداد و projectionهای موجود بررسی شدند؛ این جایگزین audit همه sinkها نیست.
- G8 = DEFERRED؛ محیط و بودجه load کنترل‌شده مشخص نبود. p50/p95/p99 یا نرخ خطای load ساخته نشده است.

## ۱۱. دیتابیس و API

Schema/table/column/PK/FK/index/constraint/rowversion جدیدی ساخته نشد. migration جدید وجود ندارد. fixture موجود schema را با migrationهای جاری روی دیتابیس تست جدا می‌سازد و سپس همان دیتابیس تست را حذف می‌کند. دیتابیس معمول توسعه و production دست‌نخورده‌اند.

داده fixture: کاربران و sessionهای ساختگی، Membership/RoleAssignment، OrganizationPerson، GuardianRelation، Class/Enrollment/TeacherAssignment و Assignment/Exam منتشرشده با سقف‌های 10 و 30. هیچ grading scale سراسری 20 فرض نشد. ارزیابی release‌شده جدیدی seed نشده؛ شواهد live سقف نمره به version منتشرشده مربوط است.

Contract v1 و همه APIهای عمومی حفظ شدند؛ پذیرش schema سست یا mapping جدید برای مخفی کردن خطای AI انجام نشد.

## ۱۲. تست‌ها و Validation

- Unit: خطای transport، deadline، cancellation، response identity، mapping و circuit؛ بدون SQL/provider واقعی.
- Integration: host/Controller/session/ProblemDetails روی SQL Server تست موجود.
- Architecture: invariants Onion موجود بدون تغییر.
- ابزار live: تست دستی opt-in با SQL واقعی و AI واقعی؛ جزو عدد xUnit نیست.

فرمان‌های اجراشده:

```powershell
dotnet restore MakanApp.sln --nologo
dotnet build MakanApp.sln --configuration Debug --nologo --verbosity minimal
dotnet test tests/MakanApp.UnitTests/MakanApp.UnitTests.csproj --configuration Debug --nologo --filter FullyQualifiedName~MakanApp.UnitTests.Ai --verbosity minimal
dotnet test MakanApp.sln --configuration Debug --nologo --verbosity minimal
```

restore و build اول PASS؛ build بدون warning/error. suite اول: **379 Unit + 570 Integration + 14 Architecture = 963 passed، 0 failed، 0 skipped**. اجرای focused میانی، قبل از اضافه شدن تست circuit recovery، 59 passed بود. دو تست regression قبل از fix واقعاً fail شدند و در شمار نتیجه نهایی ادغام نشده‌اند.

final build با همان فرمان دوباره موفق شد: **0 warning، 0 error**. final test نیز دوباره **379 Unit + 570 Integration + 14 Architecture = 963 passed، 0 failed، 0 skipped** بود؛ exit code هر دو فرمان 0. نتیجه آزمون‌های واقعی AI همچنان مستقل و ناموفق است و با سبز بودن این suite تغییر نمی‌کند.

فرمان‌های live و subset در `tools/ai/README.md` هستند. target همه پروژه‌ها .NET 9 باقی مانده؛ SDK نصب‌شده محیط `10.0.401` است و در این کار نصب یا تعویض نشده است.

## ۱۳. Git و مرز مالکیت

- commit جدید: **ندارد**؛ پیام commit: ندارد؛ push: **انجام نشد**.
- branch: `main`؛ remote/upstream: `origin/main`.
- دلیل: suite مستقل Backend سبز است ولی focused real gate سه failure دارد؛ موفقیت Backend regression را جای پذیرش STEP 10G نگذاشتیم.
- تغییرات محدود به adapter، تست‌های مرتبط، ابزار live و گزارش‌اند و uncommitted باقی می‌مانند.
- بازبینی نهایی: ۹ فایل مرتبط، `git diff --check` بدون خطای whitespace، صفر تطبیق secret جاری در فایل‌های تغییرکرده، صفر artifact تولیدی انتخاب‌شده و index خالی. پیام Git درباره تبدیل LF به CRLF، warning کامپایل نیست.
- AI modified = NO؛ AI repository در پایان بررسی clean و HEAD آن ثابت بود.
- Frontend modified = NO؛ هیچ clone، migration تولیدی، secret commit، تغییر solution یا تغییر appsettings انجام نشد.
- Direct AI SQL access = NO بر پایه source/dependency audit و نبود SQL credential در درخواست‌های بررسی‌شده؛ AI به دیتابیس fixture وصل نمی‌شود.

## ۱۴. تصمیم‌های طراحی و نکات آموزشی

1. `ResponseHeadersRead` فقط buffering را تغییر می‌دهد؛ timeout تا پایان deserialize باید ادامه داشته باشد.
2. caller cancellation و dependency timeout دو نتیجه متفاوت‌اند؛ linked token این دو را با catch filter جدا نگه می‌دارد.
3. auth سرویس و authorization کاربر مستقل‌اند؛ token سرویس مجوز دسترسی به learner ایجاد نمی‌کند.
4. health/readiness موفق، صحت structured output مدل را تضمین نمی‌کند.
5. AI 502 در این قرارداد باید Backend 503 شود؛ تغییر دادن status برای سبز کردن تست، اصلاح defect نیست.
6. live test باید opt-in، bounded، فاقد retry کور و روی دیتابیس اختصاصی باشد. trade-off ابزار جدا، نیاز به اجرای صریح build/run آن است.
7. payload inspection و metric/log check بهتر است در حافظه و با خروجی allowlist انجام شوند؛ برای debug نباید کل prompt یا JWT ذخیره شود.

## ۱۵. محدودیت‌ها و handoff لازم

blockerهای واقعی: Parent Child Summary با AI 502، Teacher Activity Suggestions و Manager Class Insight با AI 502 / `AI_RESPONSE_INVALID`؛ همچنین audit کامل runtime log/trace AI/provider هنوز انجام نشده است.

تیم AI باید schema/grounding تولیدشده این عملیات را بررسی کند. شاهد read-only مشخص برای Parent: `app/orchestration/parent_copilot.py` منابع و upcoming itemها را با `record["id"]` / `record.get("date")` می‌سنجد، در حالی که projection و مثال قرارداد Backend از `assignmentId`, `examId`, `dueAtUtc` و `availableUntilUtc` استفاده می‌کنند. این تفاوت برای بررسی تیم AI مهم است، اما علت قطعی خطای مشاهده‌شده بدون telemetry validator اعلام نمی‌شود. Backend نباید برای حل آن قرارداد frozen را خودسرانه عوض کند.

Rotation و load اجرا نشده‌اند؛ RAG عمداً deferred است. هیچ production/public RAG، auto-index یا قابلیت جدید AI ایجاد نشد.

```text
RAG status = RAG_BACKEND_SOURCE_LIFECYCLE_DEFERRED
RealAiEndToEndValidated = NO
ProductionReady = NO
```

## ۱۶. مسیر مطالعه و مرحله بعد

مسیر مطالعه: `AiController` → `AiRoleContextService` → builderهای موجود → `EfAiScopeStore` → `HmacAiServiceCredentialProvider` → تغییر `AiServiceClient.SendAsync` → `AiServiceClientTests` → `AiChatEndpointsTests` → `LiveAiScenario` و `LiveAiObservation`.

مرحله بعد، فقط رفع blocker توسط مالک AI و فراهم شدن دسترسی امن به شواهد log/trace است؛ سپس همین gate روی نسخه اعلام‌شده دوباره اجرا می‌شود. هیچ roadmap step بعدی، production rollout یا RAG وارد این کار نشده است.

BLOCKED_AFTER_BACKEND_STEP_10G
