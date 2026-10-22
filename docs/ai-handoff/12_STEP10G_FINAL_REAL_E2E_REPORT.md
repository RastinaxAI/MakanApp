# STEP10G — گزارش retest واقعی AI v1 و مانع پذیرش

> شاهد تاریخیِ اجرای همان تاریخ است، نه وضعیت جاری. برای تفکیک implementation تکمیل‌شده،
> Service Auth تأییدشدهٔ قبلی و پذیرش واقعی Deferred به علت Provider Timeout،
> [گزارش Consolidation نهایی](../release/BACKEND_FINAL_CONSOLIDATION_FA.md) را بخوانید. هیچ PASS واقعی تازه‌ای از این بازبینی نتیجه نمی‌شود.

تاریخ: ۲۰۲۶-۱۰-۱۶؛ محیط: Local Integration؛ وضعیت: **AI_V1_INTEGRATION_BLOCKED**.

## ۱. نتیجهٔ اجرایی و نسخه‌ها

سه عملیات focused واقعاً از Controller، session/authorization، context builder، SQL Server تست، credential provider و HTTP adapter Backend به AI نسخهٔ دقیق مصوب رسیدند. هر سه با `AI 503 / AI_MODEL_UNAVAILABLE` و رویداد provider با دستهٔ `configuration` شکست خوردند؛ Backend مطابق قرارداد `503 / AI_SERVICE_UNAVAILABLE` داد. هیچ پاسخ Fake برای جایگزین این نتیجه استفاده نشد.

- Backend starting HEAD: `ab0090108e6fcd26b6160307219d331c5c8c23cf`.
- Backend final HEAD این اجرای متوقف‌شده: `ab0090108e6fcd26b6160307219d331c5c8c23cf`؛ تغییرات این مرحله هنوز commit نشده‌اند.
- Branch / remote / upstream: `main` / `origin` / `origin/main`.
- Remote: `https://github.com/RastinaxAI/MakanApp.git`.
- در شروع، `git fetch origin` موفق و `HEAD == origin/main` بود. این equality مربوط به commit مبناست، نه push تغییرات جدید.
- AI exact tested commit: `b346df8f55d165f58bd2ad5dc4c8d5c971af72b0` با پیام `fix(ai): align STEP10G references with backend evidence`.
- HEAD مخزن AI محلی، جدا از نسخهٔ تست‌شده: `3b11ab175e532ea658a4e39514fe86b2905038a0`، branch `main`، worktree تمیز؛ تغییر نکرد.

## ۲. baseline و مالکیت ۱۱ فایل قبلی

پیش از تغییر، مسیر و SHA-256 تمام ۱۱ فایل dirty مرتبط ثبت شد: `.artifacts/ai-final-20261016/baseline.json`. این مرحله اجازهٔ بررسی/تکمیل آن‌ها را دارد، ولی وجودشان دلیل صحت فرض نشد.

| فایل | نوع / نتیجهٔ بازبینی |
|---|---|
| `MakanApp.Infrastructure/Ai/AiServiceClient.cs` | production؛ اصلاح قبلی timeout بدنه و IOException لازم است و حفظ شد؛ منطق قرارداد/validation سست نشد |
| `tests/MakanApp.UnitTests/Ai/AiServiceClientTests.cs` | regression؛ تست‌های قبلی حفظ و تست deadline پیش‌فرض و content type اضافه شد |
| `tests/MakanApp.IntegrationTests/AiChatEndpointsTests.cs` | regression؛ mappingهای قبلی حفظ، چندفرزندی/چندمدرسه‌ای و استقلال Identity در شکست AI افزوده شد |
| `docs/ai-handoff/10_BACKEND_REAL_E2E_GATE.md` | شاهد تاریخی؛ فقط یادداشت ارجاع به اجرای جدید اضافه شد |
| `docs/ai-handoff/11_STEP10G_FAILURE_EVIDENCE_SANITIZED.md` | capture تاریخی؛ bodyها/traceهای سابق حفظ و فقط یادداشت وضعیت جدید اضافه شد |
| `tools/ai/MakanApp.AiIntegrationProbe/FailureEvidenceCapture.cs` | ابزار تشخیصی قدیمی؛ بدون تغییر حفظ شد؛ حالت capture کامل آن در retest جدید اجرا نشد |
| `tools/ai/MakanApp.AiIntegrationProbe/LiveAiObservation.cs` | ابزار؛ خروجی امن status/code/alias و trace بدون ذخیره payload تکمیل شد |
| `tools/ai/MakanApp.AiIntegrationProbe/LiveAiScenario.cs` | ابزار؛ focused مستقل سه‌موردی و نتیجهٔ زمان‌دار هر مورد اضافه شد |
| `tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj` | ابزار opt-in خارج solution؛ بدون تغییر حفظ شد |
| `tools/ai/MakanApp.AiIntegrationProbe/Program.cs` | composition تست؛ انتخاب loopback از کانفیگ، deadline ۳۵، حالت Auth-only و جلوگیری از انتشار لاگ fixture |
| `tools/ai/README.md` | روش بازتولید، محدودیت و تفکیک gateها به‌روز شد |

فایل نامرتبطی stage نشد. کلیدها، User Secrets، MeliPayamak، کانفیگ خصوصی Production، MFA و کد frontend تغییر نکردند. در appsettings عمومی فقط `AiService:TimeoutSeconds` تغییر کرد؛ SMS section دست‌نخورده است.

## ۳. اثبات نسخهٔ runtime بدون تغییر مخزن AI

`tools/ai/run_exact_ai.py` با `git archive` دقیقاً commit مصوب را خواند و ۱۶۰ فایل را زیر `.artifacts/ai-final-20261016/runtime-b346df8/source` استخراج کرد. کد استخراج‌شده تغییر نکرد؛ bytecode غیرفعال بود. از virtualenv موجود AI استفاده شد و هیچ dependency نصب/ارتقا داده نشد.

process خودمان روی `http://127.0.0.1:8011` و PID ثبت‌شده در `runtime-evidence.json` اجرا شد. سرویس قبلی پورت ۸۰۰۰ دست‌نخورده ماند. manifest شامل revision، هش archive، هش هر فایل، port/PID، alias و تنظیمات غیرحساس است؛ هیچ secret ندارد. تنظیمات موجود `.env` مخزن AI فقط در حافظه خوانده شدند؛ داخل artifact کپی نشدند. هیچ checkout، fetch، reset، commit یا push در مخزن AI انجام نشد.

این روش از تطبیق صرف OpenAPI قوی‌تر است: همان فایل‌های archive ثابت، مستقیماً توسط process مشخص import شدند؛ SHA خوداظهاری از سرویس ناشناس مبنا نبود. پس از اجرا نیز هش هر ۱۶۰ فایل با manifest برابر و تعداد اختلاف صفر بود.

## ۴. سه شکست تاریخی — retest واقعی مستقل

همهٔ routeهای جدول **public Backend route** هستند؛ routeهای internal AI برای Teacher/Manager نام‌های frozen خود را دارند. مقادیر `{id}` فقط placeholder گزارش‌اند؛ API عمومی تغییر نکرده است.

| عملیات | Backend POST endpoint | Backend | AI | Backend code | AI code | زمان Backend |
|---|---|---:|---:|---|---|---:|
| Parent Child Summary | `/api/v1/ai/parents/child-summary` | 503 | 503 | AI_SERVICE_UNAVAILABLE | AI_MODEL_UNAVAILABLE | 639.29 ms |
| Teacher Activity Suggestions | `/api/v1/ai/teachers/classes/{id}/activity-suggestions` | 503 | 503 | AI_SERVICE_UNAVAILABLE | AI_MODEL_UNAVAILABLE | 555.26 ms |
| Manager Class Insight | `/api/v1/ai/managers/classes/{id}/insight` | 503 | 503 | AI_SERVICE_UNAVAILABLE | AI_MODEL_UNAVAILABLE | 381.20 ms |

| عملیات | requestId | correlationId | نتیجه |
|---|---|---|---|
| Parent | `93df4d1a6435443c98dc2cb46344191b` | `step10g-29b2da9b067d45e68e17b7ae8ae6f3ac` | FAIL؛ provider configuration |
| Teacher | `4459cee94bdb48a080cec6bd51c85dc2` | `step10g-c77ff1614e0042048c834f0626c98f31` | FAIL؛ provider configuration |
| Manager | `a08441e0ac87428290de3a25db87bafb` | `step10g-01f5363f767d45e7bdccefe42ffffce2` | FAIL؛ provider configuration |

پیش از capture جدول، یک اجرای سه‌موردی اولیه هم به همین سه provider failure رسید، اما bug خروجی ابزار مانع چاپ ردیف‌های Backend شد: مقدار Console.Out به‌علت initialization دیرهنگام static، پس از suppression گرفته شده بود. این اشکال فقط در ابزار اصلاح شد و اجرای ثبت‌شدهٔ بالا مستقل تکرار شد. مجموع این دو اجرا شش درخواست business به AI است؛ هر رویداد provider attempt برابر ۱ و retrying=false داشت. این تکرار دستی تشخیصی بود، نه retry خودکار adapter.

### حدود تشخیص

رویدادهای امن AI عبارت‌اند از `provider.attempt` و `provider.failed` با `failureCategory=configuration` و `api.request.completed` با `AI_MODEL_UNAVAILABLE`. این دسته در adapter AI می‌تواند از خطاهای پیکربندی/دسترسی مختلف ساخته شود. **HTTP دقیق provider، علت دقیق credential/account/model و raw response ثبت نشده‌اند و حدس زده نمی‌شوند.**

هیچ رویداد structured.validation.failed برای این شش invocation ثبت نشد؛ نتیجهٔ فعلی اثبات رفع یا تکرار همان شکست validation تاریخی نیست. علت تاریخی همچنان **Historical root cause proven = NO** است؛ raw output/runtime diagnostics آن invocationهای قدیمی در دسترس نیستند.

## ۵. ماتریس کامل ۱۲ عملیات

طبق ترتیب مصوب، اجرای کامل فقط پس از موفقیت سه focused مجاز است. بنابراین هیچ نتیجهٔ ۹ عملیات قدیمی به اجرای جدید منتقل نشده است.

| عملیات | نتیجهٔ فعلی / شاهد |
|---|---|
| Generic Chat | NOT_RUN_FOCUSED_GATE_FAILED؛ تست‌های mock جای real PASS نیستند |
| Student Analyze | NOT_RUN_FOCUSED_GATE_FAILED |
| Study Plan | NOT_RUN_FOCUSED_GATE_FAILED |
| Homework Explain | NOT_RUN_FOCUSED_GATE_FAILED |
| Exam Prepare | NOT_RUN_FOCUSED_GATE_FAILED |
| Parent Child Summary | FAIL؛ جدول focused |
| Teacher Class Summary | NOT_RUN_FOCUSED_GATE_FAILED |
| Teacher Student Insight | NOT_RUN_FOCUSED_GATE_FAILED |
| Teacher Activity Suggestions | FAIL؛ جدول focused |
| Manager School Summary | NOT_RUN_FOCUSED_GATE_FAILED |
| Manager Class Insight | FAIL؛ جدول focused |
| Manager Student Insight | NOT_RUN_FOCUSED_GATE_FAILED |

نتیجهٔ این نوبت: صفر PASS تجاری، سه FAIL، نه NOT RUN. هیچ ۱۲/۱۲ یا real structured-response acceptance ادعا نمی‌شود.

## ۶. Service Auth و headerها روی AI واقعی

JWT کلید جاری با `HmacAiServiceCredentialProvider` واقعی Backend تولید شد: HS256؛ iss/sub=makan-backend؛ aud=makan-ai؛ scope=makan.ai.invoke؛ token کوتاه‌عمر. Authorization، Content-Type=application/json و X-AI-Contract-Version=v1 ارسال شدند. هیچ UserSession token به AI forward نشد.

| سناریو | AI status / code | نتیجه |
|---|---|---|
| current key با body عمداً نامعتبر | 422 / AI_INVALID_REQUEST | PASS فقط برای authentication؛ provider اجرا نمی‌شود |
| missing auth | 401 / AI_SERVICE_UNAUTHORIZED | PASS |
| malformed token | 401 / AI_SERVICE_UNAUTHORIZED | PASS |
| expired | 401 / AI_SERVICE_UNAUTHORIZED | PASS |
| bad signature | 401 / AI_SERVICE_UNAUTHORIZED | PASS |
| unknown kid | 401 / AI_SERVICE_UNAUTHORIZED | PASS |
| wrong issuer | 401 / AI_SERVICE_UNAUTHORIZED | PASS |
| wrong audience | 401 / AI_SERVICE_UNAUTHORIZED | PASS |
| wrong subject | 401 / AI_SERVICE_UNAUTHORIZED | PASS |
| valid signature بدون scope لازم | 403 / AI_SERVICE_FORBIDDEN | PASS |
| missing contract header | 422 / AI_INVALID_REQUEST | PASS |
| wrong contract header | 422 / AI_INVALID_REQUEST | PASS |
| correlation header بدون body متناظر | 422 / AI_INVALID_REQUEST | PASS |
| previous-key rotation | N/A؛ previous slot در runtime پیکربندی نشده | نتیجهٔ ساختگی ندارد |

متن همهٔ 401ها دقیقاً `Invalid service credential.` و متن 403 برابر `Insufficient service permission.` بررسی شد. ۱۳ case واقعی موفق‌اند؛ endpoint auth-only با `{}` به orchestration/provider نرسید. رسیدن focusedهای مجاز به provider نیز شاهد جداگانهٔ پذیرفته‌شدن credential فعلی است.

## ۷. Authorization، چندمدرسه‌ای، چندفرزندی و privacy

اجرای مستقل `--authorization-only` از API واقعی Backend، SQL Server تست و session/AccessContext موجود عبور کرد. هر ۱۲ مورد موفق و شمار outbound AI برابر صفر بود:

parent_cross_child؛ teacher_cross_class؛ teacher_cross_org؛ teacher_cross_student_without_enrollment؛ manager_cross_school؛ manager_foreign_learner؛ role_mismatch؛ unpublished_homework؛ revoked_guardian؛ revoked_membership؛ suspended_membership؛ suspended_user.

denial همان 403/404 مصوب و برای suspended user برابر 401 است. AI authority مجوز نشد؛ پایان رابطه/عضویت در درخواست بعدی دوباره ارزیابی شد.

تست‌های جدید Integration، با recording client و نه real-provider PASS:

- `ParentSummary_MultipleChildren_OnlySelectedChildEducationalDataIsProjected`: دو فرزند، دو کلاس، دو تکلیف مجزا و مقیاس‌های ۱۰/۳۰؛ انتخاب A→B→A؛ assertion روی childId، assignmentId، maxScore و نبود canary/شناسه فرزند و تکلیف دیگر در JSON واقعی DTO.
- `TeacherSummary_MultipleSchools_ReResolvesWorkspaceAndClassOnEveryRequest`: یک حساب Teacher با دو membership/assignment؛ انتخاب A→B→A؛ school/organization/class هر بار جاری و کلاس مدرسه دیگر قبل از client رد می‌شود.
- تست‌های خطای dependency اکنون پس از هر خطای AI، `GET /api/v1/me` را نیز موفق می‌خواهند؛ این استقلال Identity را ثابت می‌کند، نه load همه ماژول‌ها در outage.

actual HTTP payload سه focused قبل از ارسال، در حافظه با guard موجود برای role، organization/school/class/student، headerها، credential/session/OTP/phone، foreign organization و draft sentinel بررسی شد. هیچ payload در artifact جدید persist نشده است. لاگ AI فقط از sink با field/value allowlist عبور کرد؛ metric labelهای Backend محدود به operation/outcome/error.code هستند.

Projectionها فقط ports گزارش آموزشی و داده منتشرشده/release‌شده را مصرف می‌کنند؛ raw AI history، Messenger Direct، contacts، moderation evidence، payment secret و platform audit query نمی‌شوند. بررسی کد و guardهای محدود، ادعای canary جامع برای همهٔ مخازن خصوصی یا ممیزی همهٔ sinkهای production نیست. پذیرش سراسری privacy/correlation موفق هنوز به real response موفق و اجرای کامل وابسته است.

## ۸. grounding و score/version

مجموعهٔ مستقل **۳۰ تست** `test_step10g_evidence_alignment.py` از archive دقیق b346df8 با unittest اجرا و موفق شد؛ صفر fail/skip. provider این تست‌ها مصنوعی است؛ این عدد جای provider smoke یا suite گزارش‌شدهٔ ۴۷۲تایی تیم AI نیست. تست تاریخی freeze نیز با git objectهای فقط‌خواندنی مخزن AI و worktree archive اجرا شد؛ هیچ تست غیرفعال نشد.

coverage شامل assignmentId/examId، subjectId/gradeId صریح، رد classId به‌عنوان Subject، selected studentId، Parent upcoming، نبود تولید ID از title/name/index/hash، رد malformed/extra/unknown reference، budget/date strict و عدم mutation درخواست است.

**اختلاف مستند:** source همین commit هنگام وجود هم‌زمان generic `id` و alias، `id` را ترجیح می‌دهد؛ درخواست این مرحله اولویت assignmentId/examId را می‌خواهد. تست‌های ۳۰تایی رفتار واقعی generic-first را هم تثبیت می‌کنند، نه اینکه خلاف آن را اثبات کنند. Backend فعلی برای Homework/Exam فقط type-specific ID می‌فرستد؛ هیچ generic id برای دورزدن validation اضافه نشده است. این موضوع به مالک AI برای هم‌ترازی قرارداد تعلق دارد؛ این مرحله مجاز به تغییر AI نیست و خطای configuration فعلی نیز به آن نسبت داده نمی‌شود.

نمره: Unit مربوط به `StudentContextBuilder` با score/maxScoreهای 8.5/10، 17.25/20 و 17.25/30 اجرا شد؛ مقیاس سراسری ۲۰ فرض نمی‌شود. integration چندفرزندی، مقیاس تکلیف published هر فرزند را از SQL به DTO کنترل می‌کند. queryهای `EfAiScopeStore` و گزارش‌های released grade، MaxScore را از AssignmentVersion/ExamVersion مرتبط می‌گیرند. live fixture همان Homework=10 و Exam=30 را دارد؛ evaluation release تازه‌ای در live fixture ساخته نشده و تست live تاریخچه چندنسخه‌ای grade ادعا نمی‌شود.

## ۹. timeout، resilience و correlation

default پیشین ۱۵ ثانیه از timeout کنترل‌شدهٔ ۳۰ثانیه‌ای AI کمتر بود. تست جدید default ابتدا واقعاً FAIL شد: expected=35، actual=15. `AiServiceOptions`، fallback DI و فقط `AiService:TimeoutSeconds` در appsettings عمومی به ۳۵ تغییر کردند؛ حاشیه ۵ثانیه‌ای برای شبکه و error mapping است. range معتبر 1..120 و قابلیت تنظیم حفظ شدند. HttpClient نامحدود و linked deadline در AiServiceClient مرجع timeout است؛ POST retry اضافه نشد.

| مورد | شاهد و حدود نتیجه |
|---|---|
| timeout پیش/پس از header | Unit fault-injection؛ PASS؛ caller cancellation مستقل حفظ می‌شود |
| 429 | Unit بدون retry و Retry-After داخلی؛ public mapping Integration؛ PASS |
| 503 / AI_RESPONSE_INVALID upstream 502 | Unit mapping بدون عبور raw body؛ Integration public semantics؛ PASS؛ خطای واقعی فعلی AI503 نیز Backend503 شد |
| connection failure پیش/پس از header | Unit، sanitization و call count=1؛ PASS |
| malformed JSON / valid JSON invalid schema / envelope mismatch | Unit، strict نام‌های شناخته‌شده و identity؛ PASS |
| unexpected content type | تست جدید Unit؛ AI_RESPONSE_INVALID و یک call؛ PASS |
| circuit opening/recovery/reset | Unit با ساعت کنترل‌شده؛ PASS؛ outage/load سرویس واقعی inject نشده است |
| non-AI functionality | GET me پس از شکست AI در Integration؛ PASS محدود به Identity |
| correlation | سه trace واقعی Backend→AI در safe eventها یکسان‌اند؛ success echo تا frontend به‌علت نبود پاسخ موفق NOT PROVEN |

بدنهٔ FastAPI/provider به frontend پاس داده نشد. strict response validation و کدهای عمومی frozen حفظ شدند. تست fault-injection را real provider resilience نام‌گذاری نمی‌کنیم.

## ۱۰. provider smoke، health و عملیات سرور

کلید OpenRouter در تنظیمات امن موجود بود؛ عدم وجود secret علت توقف نبود. real provider smoke واقعاً انجام و **FAIL / configuration** شد؛ alias=`education-fast`، model mapping موجود AI=`openai/gpt-4o-mini`. زمان provider برای Parent/Teacher/Manager ثبت‌شده به‌ترتیب 310.92، 271.82 و 319.92 ms بود؛ زمان Backend در جدول focused است. key، JWT، prompt، پاسخ provider و داده آموزشی خام ذخیره یا چاپ نشدند.

`GET /api/v1/health`: 200 / ok. `GET /api/v1/ready`: 200 / ready / configuration=ok. این readiness صرفاً بررسی local settings است و دسترسی واقعی OpenRouter را اثبات نمی‌کند؛ شکست smoke هم‌زمان این تفاوت را نشان می‌دهد.

هیچ دسترسی به Windows Server، IIS/ARR، دامنه/HTTPS، firewall، پورت خارجی ۸۰۰۱، Windows Service/restart، crash recovery، reboot یا لاگ production انجام نشد. همهٔ این gateها **PENDING_OPERATIONAL_DEPLOYMENT** هستند. loopback بودن process محلی فقط محیط تست را اثبات می‌کند.

در پایان فقط process متعلق به همین ابزار با بررسی PID/command line/listener متوقف شد. listener قبلی `127.0.0.1:8000` با PID اولیهٔ ۳۲۶۰ همچنان باقی ماند. هیچ سرویس کاربر یا سرور restart/stop نشد.

## ۱۱. Build / Test / Fix

| دور | Unit | Integration | Architecture | کل | failed / skipped |
|---|---:|---:|---:|---:|---|
| Baseline | 413 | 701 | 14 | 1128 | 0 / 0 |
| پس از تغییر و fix | 417 | 703 | 14 | 1134 | 0 / 0 |
| final | 417 | 703 | 14 | 1134 | 0 / 0 |

restore و build/final build موفق، صفر warning/error؛ build نهایی ابزار مستقل نیز موفق و self-check آفلاین sanitizer سبز بود. focused AI Unit پیش از دو case جدید نمره، ۶۲ موفق بود. AI regression مستقل grounding=30 PASS. تست default timeout عمداً ابتدا red شد و پس از fix سبز است؛ هشت failure نخست Integration جدید ناشی از route اشتباه تست `GET /me/profile` بود که به route واقعی `GET /me` اصلاح شد؛ این defect برنامه معرفی نمی‌شود.

فرمان‌ها:

```powershell
dotnet restore MakanApp.sln --nologo
dotnet build MakanApp.sln --configuration Debug --nologo --no-restore
dotnet test MakanApp.sln --configuration Debug --nologo --no-build --no-restore --logger 'trx;LogFilePrefix=ai-final-full' --results-directory .artifacts/ai-final-20261016/tests/full
dotnet build MakanApp.sln --configuration Debug --nologo
dotnet test MakanApp.sln --configuration Debug --nologo --no-build --no-restore --logger 'trx;LogFilePrefix=ai-final-final' --results-directory .artifacts/ai-final-20261016/tests/final
```

database/schema/migration جدید، EnsureCreated، production migration یا direct AI SQL access ایجاد نشد. fixture موجود فقط دیتابیس اختصاصی `MakanApp_MessagingSafety_IntegrationTests_Step7E` روی LocalDB را با migrationهای موجود می‌سازد/حذف می‌کند؛ اجرای live و suite هم‌زمان روی این دیتابیس انجام نشدند.

## ۱۲. Git، شواهد و اقدام لازم

به‌علت شکست واقعی focused gate و شرط Phase 19 برای موفقیت کامل validation، stage/commit/push این super step انجام نشده است. سبز بودن regression Backend جای پذیرش واقعی AI گذاشته نمی‌شود. Commit hash/message جدید، push result و remote verification مربوط به تغییرات این مرحله: **NOT PERFORMED — REAL E2E GATE FAILED**. commit مبنا همچنان ab00901 است؛ no force/reset/clean/history rewrite.

شواهد محلی ignored: baseline.json؛ focused-three.jsonl؛ service-auth.jsonl؛ authorization.jsonl؛ tests/baseline، tests/full و tests/final؛ runtime-b346df8/runtime-evidence.json و safe-events.jsonl. این artifactها حاوی secret یا full context/provider output نیستند. فایل‌های product PDF و frontend خارج scope باقی ماندند.

بازبینی پایانی ۲۰ فایل مرتبط، صفر تطبیق secret واقعی، index خالی و `git diff --check` موفق بود. worktree مخزن AI تمیز و HEAD آن بدون تغییر است. هشدارهای LF/CRLF Git، هشدار کامپایل نیستند؛ buildها واقعاً صفر warning/error داشتند.

برای ادامه: مالک محیط باید اعتبار/دسترسی provider و مدل را در secret store بررسی کند؛ مقدار key در چت ارسال نشود. سپس runtime دقیق همین commit با تنظیمات امن اصلاح‌شده دوباره اجرا، سه focused تکرار و فقط در صورت موفقیت، ماتریس کامل ۱۲تایی و دیگر gateهای acceptance تکمیل شوند. اختلاف اولویت generic/type-specific identifier نیز باید با مالک AI هم‌تراز شود؛ کد AI از این workspace اصلاح نمی‌شود.

مسیر مطالعه: AiController → AiRoleContextService/context builderها → EfAiScopeStore و report queryها → HmacAiServiceCredentialProvider → AiServiceClient → Unit/Integration AI tests → LiveAiScenario/LiveAiObservation/ServiceAuthProbe → run_exact_ai.py.

RAG وضعیت مصوب خود را دارد: **RAG_BACKEND_SOURCE_LIFECYCLE_DEFERRED**. هیچ production RAG، vector ingestion، automatic indexing، private Messenger/finance/audit indexing یا public RAG route اضافه نشده است. نبود RAG مانع مصنوعی پذیرش Core نیست.

```text
AI_CORE_V1_INTEGRATION_COMPLETE = NO
REAL_AI_END_TO_END_VALIDATED = NO
RAG_PRODUCTION = DEFERRED
AI_PRODUCTION_RUNTIME_READY = NO
OVERALL_MAKAN_PRODUCTION_READY = NO
```

AI_V1_INTEGRATION_BLOCKED

## Provider Unblock Retest

تاریخ: ۲۰۲۶-۱۰-۱۶. این بخش نتیجهٔ درخواست جدید «AI PROVIDER UNBLOCK + FINAL STEP10G REAL RETEST» است؛ شواهد blocked قبلی در بالا عمداً حفظ شده‌اند. نتیجهٔ جدید جایگزین ادعاهای تاریخی نشده و موفقیت E2E ایجاد نمی‌کند.

### مبنا و پیکربندی مؤثر

Backend روی `main` با upstream برابر `origin/main` است. پس از fetch، `HEAD` و `origin/main` هر دو برابر `ab0090108e6fcd26b6160307219d331c5c8c23cf` بودند. وضعیت، diff و SHA-256 هر ۲۰ فایل dirty قبلی پیش از تغییر ثبت شدند. کد Production، قرارداد، route، DTO، ErrorCode و strict validation در این تلاش تغییر نکردند.

آزمون مستقیم از همان رایانه، شبکه، venv موجود AI و تنظیمات مؤثر اجرای قبلی استفاده کرد. secret موجود فقط در حافظه خوانده شد؛ هیچ مقدار key، Authorization، JWT، prompt یا raw provider body چاپ یا ذخیره نشد.

| تنظیم | مقدار مشاهده‌شده |
|---|---|
| AI source هدف | `b346df8f55d165f58bd2ad5dc4c8d5c971af72b0` |
| صحت snapshot | SHA-256 تمام ۱۶۰ فایل و archive همان commit تأیید شد؛ هیچ فایل AI تغییر نکرد |
| `AI_PROVIDER` | `openrouter` |
| `AI_MODEL_ALIAS` | `education-fast` |
| مدل resolve‌شده توسط `resolve_model` همان source | `openai/gpt-4o-mini` |
| base URL واقعی adapter | `https://openrouter.ai/api/v1` |
| `REQUEST_TIMEOUT_SECONDS` | 30 |
| `PROVIDER_MAX_RETRIES` / retry داخلی SDK | 0 / 0 |
| `OPENROUTER_API_KEY` | موجود؛ فقط presence بررسی شد، نه اعتبار آن |
| Python / OpenAI SDK | 3.12.7 / 3.31.0 |

ابزار جدید `tools/ai/provider_diagnostic.py` از `OpenRouterProvider.generate` واقعی همین snapshot استفاده می‌کند. client با همان تنظیمات adapter ساخته می‌شود؛ hook پاسخ صرفاً status و Content-Type را مشاهده می‌کند. schema، provider result و Service Auth دست‌کاری نشده‌اند. تنظیمات TLS و شبکه نیز تغییر نکردند.

### جداسازی واقعی provider و علت اثبات‌شده

دو درخواست مستقل و حداقلی واقعاً ارسال شدند؛ هر کدام یک پاسخ HTTP دریافت کردند و هیچ completion معتبری بازنگشت:

| درخواست | زمان UTC | HTTP | Content-Type | تأخیر | نتیجه |
|---|---|---|---|---:|---|
| direct smoke | `2026-10-16T04:27:16.718322+00:00` | 403 | `application/json` | 505.82 ms | FAIL |
| diagnostic با استخراج حداقلی پیام عمومی | `2026-10-16T04:28:31.799570+00:00` | 403 | `application/json` | 510.79 ms | FAIL |

پیام عمومی دقیق پاسخ دوم: **`Access denied by security policy.`** مقدار header سرور `cloudflare` بود و فقط وجود `cf-ray` ثبت شد، نه مقدار آن. کد عمومی مستقل و قابل‌استفاده‌ای از error envelope استخراج نشد.

طبقه‌بندی مبتنی بر این شواهد: **G — `HTTP403_SECURITY_POLICY_REJECTION`**. رسیدن پاسخ HTTPS/JSON نشان می‌دهد این دو تلاش خطای DNS یا TLS نبودند. این شواهد به‌تنهایی خراب/منقضی بودن کلید، کمبود اعتبار، نامعتبر بودن مدل، محدودیت کشور یا قاعدهٔ دقیق edge/account را ثابت نمی‌کنند؛ هیچ‌کدام به‌عنوان علت قطعی گزارش نمی‌شوند. مستندات رسمی نیز نمونه‌هایی از 403 ناشی از سیاست‌ها دارد، نه صرفاً authentication؛ این مرجع، قاعدهٔ خاص این پاسخ را تعیین نمی‌کند: [OpenRouter Guardrails](https://openrouter.ai/docs/guides/features/guardrails/overview).

در دو artifact اصلی، label اولیهٔ `PROVIDER_HTTP403_RESTRICTION_UNCLASSIFIED` دست‌نخورده باقی ماند. پس از مشاهدهٔ پیام عمومی، classifier و تست آن تکمیل شدند و همان شاهد ذخیره‌شده **آفلاین** به دستهٔ بالا طبقه‌بندی شد؛ این بازطبقه‌بندی درخواست سوم یا smoke موفق نیست.

### اصلاح پیکربندی و اقدام دقیق مالک

**اصلاح اعمال‌شده: NONE.** local key خالی نبود و URL/model mapping با adapter واقعی تطبیق داشت؛ شاهدی برای تغییر ایمن تنظیمات محلی به دست نیامد. تغییر کلید، retry، proxy/VPN، دورزدن policy، تغییر SDK یا خاموش‌کردن TLS انجام نشد.

مالک مجاز حساب OpenRouter و مسئول شبکه باید رد دسترسی این ماشین/مسیر خروجی را با پشتیبانی یا کنترل‌های مجاز سرویس بررسی و برطرف کنند. اطلاعات امن لازم برای پیگیری: endpoint `POST https://openrouter.ai/api/v1/chat/completions`، مدل `openai/gpt-4o-mini`، زمان UTC جدول، HTTP 403، پیام عمومی بالا و metadata محدود پاسخ. API key، header، JWT و محتوای آموزشی نباید در چت/تیکت عمومی قرار گیرند. از این شواهد نمی‌توان دستور مشخصی برای تغییر یک قاعدهٔ dashboard یا تعویض key تجویز کرد.

پس از رفع مجاز محدودیت، همان smoke باید از همین محیط با فایل خروجی جدید اجرا شود. فقط completion معتبر از provider واقعی، مجوز ادامه به AI HTTP smoke، سه focused و سپس همهٔ ۱۲ عملیات را می‌دهد.

### نتیجهٔ gateها و ممیزی identifier

| gate | نتیجهٔ تلاش فعلی |
|---|---|
| `REAL_OPENROUTER_SMOKE` | FAIL؛ دو پاسخ 403، صفر completion معتبر |
| AI HTTP smoke موفق | NOT_RUN؛ provider gate عبور نکرد |
| Parent Child Summary | NOT_RUN؛ اجرای قبلی FAIL بود، موفقیت تازه ادعا نمی‌شود |
| Teacher Activity Suggestions | NOT_RUN؛ اجرای قبلی FAIL بود |
| Manager Class Insight | NOT_RUN؛ اجرای قبلی FAIL بود |
| focused مجموع | 0/3 موفق تأییدشده در تلاش فعلی؛ 0 اجرا |
| ماتریس کامل | 0/12 موفق تأییدشده در تلاش فعلی؛ 0 اجرا |
| historical `AI_RESPONSE_INVALID` | `NOT_PROVEN`؛ پاسخ موفق واقعی provider هنوز وجود ندارد |
| Service Auth | PASS تاریخی؛ ۱۳ سناریوی ثبت‌شده، در این تلاش تکرار نشد |
| Authorization negative | PASS تاریخی؛ ۱۲/۱۲ ثبت‌شده، اجرای live تازه انجام نشد |
| Privacy | کنترل‌های محدود و تست‌های قبلی PASS؛ پذیرش جامع real E2E همچنان NOT_PROVEN |

ممیزی typed-ID روی شکل **واقعاً ثبت‌شدهٔ** سه payload قبلی در گزارش ۱۱ انجام شد، نه payload ساختگی: Homework فقط `assignmentId` و Exam فقط `examId` دارد؛ هیچ occurrence هم‌زمان generic `id` با typed ID دیده نشد. projection فعلی نیز همین شکل را می‌سازد و در این تلاش تغییر نکرده است. payload live تازه به‌علت شکست provider gate تولید نشد.

طبق Case 1 پرامپت جدید، نتیجهٔ precedence برای این شکل: **`NON_BLOCKING_DISCREPANCY`**. رفتار generic-first در AI b346df8 بدهی فنی مشروط به وجود هر دو فیلد است، نه مانع فعلی؛ جبران Backend، ساخت `id` یا تبدیل `classId` به `subjectId` انجام نشد. این clarification، الزام توقف اضافه‌ای از اختلاف مستند قبلی ایجاد نمی‌کند.

### Validation، فایل‌ها و Git

baseline فعلی دوباره اجرا شد: `dotnet build MakanApp.sln --configuration Debug --nologo` موفق با صفر warning/error؛ `dotnet test MakanApp.sln --configuration Debug --nologo --no-build --no-restore` موفق. شمارها از TRX همین تلاش:

| Unit | Integration | Architecture | کل | failed | skipped |
|---:|---:|---:|---:|---:|---:|
| 417 | 703 | 14 | 1134 | 0 | 0 |

۹ تست مستقل ابزار تشخیص نیز با `python -B -m unittest discover -s tools/ai -p test_provider_diagnostic.py -v` موفق شدند؛ این‌ها Unit آفلاین classifier/sanitizer هستند و به شمار .NET اضافه یا real smoke معرفی نمی‌شوند. coverage شامل تفکیک auth/billing/rate/403/404، عدم حدس علت، ورودی خراب، redaction و پیام واقعی security policy است. مرحلهٔ final regression پس از real E2E طبق Phase 10 **NOT_RUN** است، چون gate قبلی شکست خورد؛ baseline جای آن نام‌گذاری نمی‌شود.

فایل‌های جدید این تلاش فقط `tools/ai/provider_diagnostic.py` و `tools/ai/test_provider_diagnostic.py` هستند؛ تغییر سند حاضر فقط append این بخش است. شواهد محلی ignored در `.artifacts/ai-provider-unblock-20261016` شامل baseline، TRX، دو پاسخ metadata-only و diagnosis آفلاین است. هیچ migration، schema، frontend، MeliPayamak، production config یا source مخزن AI تغییر نکرد.

به‌دلیل شرط صریح Phase 12: **Stage = NOT_RUN؛ Commit = NONE؛ Push = NOT_RUN**. سبز بودن ۱۱۳۴ تست Backend مجوز عبور از provider gate نیست. تغییرات قبلی حفظ و خارج index باقی می‌مانند؛ commit مبنا همان `ab0090108e6fcd26b6160307219d331c5c8c23cf` است.

بازبینی پایانی: hash هر ۱۹ فایل baseline غیر از این گزارش عیناً برابر ماند؛ متن کامل قبلی این گزارش نیز به‌صورت prefix دست‌نخورده تأیید شد. index خالی و `git diff --check` موفق است؛ هشدارهای EOL، هشدار build نیستند. اسکن هشت فایل ابزار/گزارش/شواهد جدید با secretهای واقعی موجود صفر تطبیق داشت. worktree مخزن AI تمیز و HEAD آن همان `3b11ab175e532ea658a4e39514fe86b2905038a0` باقی ماند. تنها دو فایل ابزار جدید، علاوه بر ۲۰ فایل dirty قبلی، در وضعیت Git دیده می‌شوند.

gateهای باقیمانده: رفع مجاز security-policy rejection؛ direct provider PASS؛ protected AI HTTP smoke؛ focused 3/3؛ همهٔ ۱۲ عملیات؛ ارزیابی مجدد historical validation و success correlation؛ final restore/build/test و final build/test؛ review/commit/push/remote verification. deployment gateهای پیشین Windows Service، IIS/ARR/HTTPS، firewall/private port، recovery/reboot و production logging همچنان `PENDING_OPERATIONAL_DEPLOYMENT` هستند؛ هیچ عملیاتی روی سرور انجام نشد.

```text
AI_CORE_V1_INTEGRATION_COMPLETE = NO
REAL_AI_END_TO_END_VALIDATED = NO
RAG_PRODUCTION = DEFERRED
AI_PRODUCTION_RUNTIME_READY = NO
OVERALL_MAKAN_PRODUCTION_READY = NO
```

AI_PROVIDER_CONFIGURATION_ACTION_REQUIRED
