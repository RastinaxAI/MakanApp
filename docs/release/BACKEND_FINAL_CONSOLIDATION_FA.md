# نهایی‌سازی پیاده‌سازی Backend و تفکیک پذیرش واقعی AI

تاریخ بررسی: ۲۰۲۶-۱۰-۲۲. مخزن اصلی: `D:\Project\MakanApp\MakanApp-Backend`.

## ۱. خلاصهٔ مرحله

نقطهٔ شروع `main@f8d7c25`، برابر `origin/main`، با ۲۰ فایل Dirty/Untracked بود.
ادغام ۱۸ Commit قبلی از قبل انجام شده بود؛ در این مرحله دوباره Merge نشد.
هدف، بازبینی و ثبت بخش‌های کاملِ باقیماندهٔ Backend است، نه اصلاح Provider یا استقرار سرور.
پیاده‌سازی و Regression محلی از پذیرش واقعی Production AI دو gate مستقل‌اند.

نتیجهٔ نهایی محلی: `Backend Implementation = PASS`؛ پذیرش واقعی AI همچنان `DEFERRED` است.
موفقیت این مرحله، اثبات استقرار سرور یا رفع Timeout خارجی نیست.

## ۲. معماری

.NET 9، EF Core 9.0.5، SQL Server، Onion، Modular Monolith و Controller API حفظ شدند.
Domain تغییر نکرد. Application از قبل مالک Use Case، AccessContext، projection و authorization
است؛ Infrastructure مالک `AiServiceClient`، credential سرویس، EF و ابزارهای فنی است.
Api از نشست معتبر، actor/session را می‌گیرد و ورودی HTTP را به Application می‌دهد.
هیچ وابستگی معکوس، Package جدید، Microservice یا سیستم Auth موازی ایجاد نشد.

## ۳. فایل‌های مهم نهایی‌شده/ایجادشده

- `tools/ai/MakanApp.AiIntegrationProbe/FailureEvidenceCapture.cs`: پالایش Evidence تشخیصیِ صریح، نه logger عمومی برنامه؛ استفادهٔ مجاز فقط با fixture ساختگی است.
- `tests/MakanApp.UnitTests/Ai/FailureEvidenceCaptureTests.cs`: self-check، حذف canary/credential/شناسهٔ اصلی، رد trace حساس و fail-closed بودن metadata را بدون شبکه می‌آزماید.
- `docs/dev/check-development-test-users-database.sql`: بررسی فقط‌خواندنی مقصد، ستون‌های امنیتی و تاریخچهٔ EF؛ schema را اصلاح نمی‌کند.
- گزارش‌های `10_BACKEND_REAL_E2E_GATE.md`، `11_STEP10G_FAILURE_EVIDENCE_SANITIZED.md` و `12_STEP10G_FINAL_REAL_E2E_REPORT.md`: شواهد تاریخی پالایش‌شده در مسیر رسمی docs؛ حکم اجرای جدید نیستند.

## ۴. فایل‌های مهم تغییرکرده

- `AiServiceOptions.cs` و `AiServiceClient.cs`: پیش‌فرض کاری موجود ۳۵ ثانیه از سقف تنظیم موجود ۱۸۰ ثانیه جدا شد؛ افزایش Timeout برای عبور از Provider انجام نشد.
- `appsettings.json`: تنظیمات عمومی موجود AI ثبت می‌شوند؛ credential واقعی ندارند. مقصد loopback در این فایل، default توسعه و قابل override است، نه IP Production در Source Code.
- تست‌های `AiChatEndpointsTests` و `AiRoleContextBuilderTests`: جداسازی چندفرزند/چندسازمان و نگاشت شناسه‌ها/MaxScore نسخه‌ای را پوشش می‌دهند.
- `AiServiceClientTests`: Timeout خواندن body، قطع اتصال، Cancellation، correlation، Content-Type و بازیابی Circuit را می‌سنجد.
- `tests/MakanApp.UnitTests/MakanApp.UnitTests.csproj`: همان الگوی Link موجود برای آزمودن sanitizer را گسترش می‌دهد؛ پروژهٔ live وارد Solution نمی‌شود.
- `create-development-test-users.sql`: ابتدا Schema بررسی می‌شود و بدنهٔ ثابت پس از guard compile می‌شود؛ مقادیر کاربر در SQL concatenate نمی‌شوند.
- `.gitignore`: فقط `.env`، `*.agekey` و خروجی‌های صریح `*.decrypted`/`*.decrypted.json` اضافه شدند؛ منبع واقعی به‌صورت گسترده پنهان نشد.
- README ابزار، register تصمیم‌ها و سرصفحهٔ شواهد تاریخی به وضعیت جدید ارجاع می‌دهند؛ شواهد قدیمی حذف نشدند.

## ۵. جریان اجرای واقعی

`AiController` → `AuthenticatedSession` → `AiChatService` یا `AiRoleContextService`
→ `IAccessContextResolver` → Context Builder نقش → `EfAiScopeStore` و read serviceهای
محدودشدهٔ SQL Server → DTO حداقلی → `HmacAiServiceCredentialProvider` →
`AiServiceClient` → AI خارجی → validation سخت‌گیرانه و `AiResponseGrounding` → HTTP Response.

Authorization پیش از تماس AI انجام می‌شود. GuardianRelation مجوز چت خصوصی کودک نیست؛
Parent/Teacher/Manager تاریخچهٔ خام AI یا Messenger خصوصی را دریافت نمی‌کنند.

## ۶. دیتابیس و Migration

هیچ Entity، EF Configuration، schema یا Migration جدیدی اضافه نشد.
اسکریپت Seed همچنان فقط پیشوند دیتابیس `MakanApp_DevelopmentTestUsers` و تأیید صریح
connection را می‌پذیرد؛ نسخهٔ Worktree که اجازهٔ دیتابیس عادی را گسترش می‌داد منتقل نشد.
نبود جدول/ستون لازم خطای 51016 می‌دهد؛ خطای SQL 207 نباید پیام Migration را بپوشاند.
تست ستون ناموجود، خطا را به متن همان اسکریپت تزریق می‌کند؛ Constraint و Schema واقعی را دست‌کاری نمی‌کند.
تراکنش، applock، IDهای fixture، کنترل برخورد و عدم merge حساب‌ها حفظ شدند.

EF CLI 9.0.5 با Infrastructure به‌عنوان project/startup و `MakanDbContext` اجرا شد:

```powershell
dotnet ef migrations list --project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj --startup-project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj --context MakanDbContext --configuration Debug --no-build --no-connect --json
dotnet ef migrations has-pending-model-changes --project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj --startup-project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj --context MakanDbContext --configuration Debug --no-build
```

۴۸ Migration موجود است؛ آخرین `20261017033804_AddConversationChecklists`.
Pending model changes وجود ندارد. به دلیل `--no-connect`، وضعیت Applied دیتابیس مقصد
تعیین نشده است. شروع از Api به نبود reference ابزار Design برخورد کرد؛ استفاده از
DesignTimeFactory موجود Infrastructure بدون افزودن dependency مسئله را حل کرد.
هیچ Migration/Seed روی دیتابیس عادی یا سرور اجرا نشد؛ تست‌ها فقط fixture ایزوله را به‌کار می‌گیرند.

## ۷. دوازده عملیات و Endpointها

همه `POST`، زیر `/api/v1/ai` و با Session معتبر، rate limit موجود و مجوز جاری Backend هستند.
شناسهٔ مسیر فقط selector است. 400 برای ورودی نامعتبر، 403 برای context غیرمجاز و
خطاهای dependency مطابق قرارداد موجود نگاشت می‌شوند. Route یا DTO جدیدی ساخته نشد.

| عملیات | Route عمومی | Request | Response |
| --- | --- | --- | --- |
| Generic Chat | `/chat` | `SendAiChatCommand` | `AiChatResult` |
| Student Analyze | `/students/analyze` | context سرور | `AiStudentAnalysisResponse` |
| Study Plan | `/study-plan` | `CreateStudyPlanCommand` | `AiStudyPlanResponse` |
| Homework Explain | `/homeworks/{assignmentId}/explain` | `ExplainHomeworkCommand` | `AiHomeworkExplanationResponse` |
| Exam Prepare | `/exams/{examId}/prepare` | شناسه + context سرور | `AiExamPreparationResponse` |
| Parent Child Summary | `/parents/child-summary` | فرزند انتخاب‌شدهٔ مجاز | `AiParentChildSummaryResponse` |
| Teacher Class Summary | `/teachers/classes/{classId}/summary` | شناسه + context سرور | `AiRoleInsightResponse` |
| Teacher Student Insight | `/teachers/classes/{classId}/students/{studentId}/insight` | شناسه‌ها + context سرور | `AiRoleInsightResponse` |
| Teacher Activity Suggestions | `/teachers/classes/{classId}/activity-suggestions` | `SuggestTeacherActivitiesCommand` | `AiActivitySuggestionsResponse` |
| Manager School Summary | `/managers/school-summary` | سازمان جاری | `AiRoleInsightResponse` |
| Manager Class Insight | `/managers/classes/{classId}/insight` | شناسه + context سرور | `AiRoleInsightResponse` |
| Manager Student Insight | `/managers/classes/{classId}/students/{studentId}/insight` | شناسه‌ها + context سرور | `AiRoleInsightResponse` |

Student از User→Person→OrganizationPerson فعال و Enrollment فعال resolve می‌شود.
Parent رابطهٔ Guardian جاری و فرزند انتخابی دارد. Teacher باید TeacherAssignment فعال و
برای Student، Enrollment همان کلاس را داشته باشد. Manager فقط در سازمان جاری عمل می‌کند؛
PlatformOwner به‌تنهایی Manager آموزشی نیست. Cross-org/child/class/student در تست‌های منفی
پیش از تماس AI رد می‌شوند؛ تست‌های چندفرزندی/چندسازمانی بازبینی هر Request را نیز اثبات می‌کنند.

## ۸. JWT، Privacy و Observability

`HmacAiServiceCredentialProvider` بدون تغییر باقی ماند: HS256، `kid` معتبر، issuer/subject
`makan-backend`، audience `makan-ai`، scope `makan.ai.invoke`، `iat/nbf/exp/jti`،
TTL پیش‌فرض ۱۲۰ و بازهٔ مجاز ۳۰ تا ۳۰۰ ثانیه. کلید در حافظه مصرف می‌شود؛ JWT شامل PII نیست.
User Token به AI forward نمی‌شود. Auth واقعی قبلاً PASS شده بود؛ این مرحله آن را دوباره اجرا نمی‌کند.

`requestId` برای درخواست تازه تولید و `correlationId` از میزبان عبور می‌کند؛ هر دو در
پاسخ دقیقاً بررسی می‌شوند. `X-AI-Contract-Version=v1`، DTO سخت‌گیرانه، منع field ناشناخته،
Grounding و MaxScore نسخه‌ای حفظ شدند. Private message، OTP، credential و EF graph در projection نیستند.

Tracing از `ActivitySource("MakanApp.AiService")` و metricهای
`makan.ai.client.requests`، `makan.ai.client.duration` و `makan.ai.client.circuit.rejections`
استفاده می‌کند. labelها محدود به operation/outcome/error.code هستند؛ شناسه‌ها فقط در
trace/log scope مجاز قرار می‌گیرند، نه label پرکاردینالیتی. Prompt/body/JWT/key چاپ نمی‌شوند.
صحت instrumentation محلی، اثبات نصب exporter یا دریافت alert روی سرور نیست.

OTP واقعی همچنان با `Enabled=true / AllowMockAuthentication=false` از provider موجود و
اعتبارسنجی Challenge استفاده می‌کند. Mock فقط در Development/Test/Testing و با
`false/true` مجاز است؛ Production/Staging در Startup آن را رد می‌کنند. فلگ‌های legacy
مجوز موازی نمی‌سازند. Mock نقش، Membership، Session policy یا MFA را دور نمی‌زند.
ملی‌پیامک و تنظیمات خصوصی آن تغییر نکردند؛ هیچ SMS واقعی در Validation ارسال نمی‌شود.

## ۹. تصمیم‌های Resilience

بودجهٔ کاری موجود ۳۵ ثانیه و سقف تنظیم ۱۸۰ ثانیه بدون افزایش حفظ شدند.
هیچ Retry، Auth bypass، تغییر Contract، پاسخ ساختگی runtime یا workaround برای Provider اضافه نشد.
Timeout شامل خواندن body است و caller cancellation با Timeout داخلی یکی نمی‌شود.
429→`AI_RATE_LIMITED`، 503/قطع اتصال→`AI_SERVICE_UNAVAILABLE`، پاسخ malformed→`AI_RESPONSE_INVALID`
و 504→`AI_SERVICE_TIMEOUT` است. متن خام خطای Provider به Client منتقل نمی‌شود.
Circuit موجود process-local با threshold=3 و break=30s است؛ پاسخ موفق آن را reset می‌کند.
این مکانیزم distributed یا Exactly-once نیست؛ Runtime acceptance را نباید از آن نتیجه گرفت.

## ۱۰. تست‌ها و اصلاح‌های Validation

تست‌های کامل قبلی AI و OTP حفظ شدند. پنج case Unit برای sanitizer اضافه شد؛ هیچ‌کدام
Secret، DB یا شبکهٔ واقعی لازم ندارد. سه case Integration، missing-column guard را روی
SQL Server ایزوله و اسکریپت با fault injection می‌سنجند؛ تست‌های Seed/شمارهٔ ایرانی/برخورد/هم‌زمانی حذف نشدند.
تست‌های مجوز HTTP از Fake AI استفاده می‌کنند؛ عمداً جایگزین Real AI Acceptance گزارش نمی‌شوند.

در اجرای اولیه، using تست جدید جا افتاده بود و یک fixture از charset نامعتبر استفاده می‌کرد؛
هر دو اصلاح شدند. rename ستون دارای Constraint نیز روش مناسبی برای fixture نبود؛ با
fault injection متن SQL جایگزین شد، نه حذف یا Skip کردن تست. هیچ Warning suppress نشد.

## ۱۱. Validation واقعی

- Restore: PASS.
- Build پس از اصلاح: PASS، صفر Warning/Error.
- Focused Unit: ۲۴۱ موفق، صفر Failed/Skipped.
- Focused Integration: ۱۰۲ موفق، صفر Failed/Skipped؛ Probe Build و self-check آفلاین نیز PASS.
- Full Regression اول: ۸۶۳ Unit، ۱۳۴۱ Integration و ۱۴ Architecture؛ مجموع ۲۲۱۸ موفق، صفر Failed/Skipped.
- Final Build: PASS، صفر Warning و صفر Error؛ زمان ۳٫۴۳ ثانیه.
- Final Tests: PASS؛ Counterهای هر شش TRX دور اول و نهایی جداگانه کنترل شدند.

| مجموعهٔ نهایی | اجراشده/موفق | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Unit | ۸۶۳ | ۰ | ۰ |
| Integration | ۱۳۴۱ | ۰ | ۰ |
| Architecture | ۱۴ | ۰ | ۰ |
| مجموع | ۲۲۱۸ | ۰ | ۰ |

Integration دور اول ۱۴ دقیقه و ۵۹ ثانیه و نهایی ۱۰ دقیقه و ۴۲ ثانیه طول کشید.
خروجی‌ها `tests/<Project>/TestResults/final-consolidation-regression.trx` و
`final-consolidation-final.trx` هستند؛ ignored و خارج از Commit می‌مانند.
Probe مستقل نیز بدون Warning/Error Build شد؛ self-check آفلاین جزو شمار اضافهٔ تست‌ها شمرده نمی‌شود.

## ۱۲. Audit فایل‌ها، Worktreeها، Secret و Git

پشتیبان خصوصی جدید از ۲۰ تغییر اصلی و پنج فایل Worktree توسعه تهیه و SHA-256 آن‌ها
بررسی شد؛ پشتیبان ۲۷فایلی مرحلهٔ قبلی حذف یا بازنویسی نشد. تنظیمات خصوصی Production/Test
ignored و بدون تغییر باقی می‌مانند. هیچ Secret واقعی به فایل عمومی منتقل نمی‌شود.

اسکن اولیه ۱۲۳۲ فایل عمومی داشت؛ هشت یافته به placeholder مستند یا fixture تست مربوط بود.
اسکن تکمیلی ۱۲۳۵ فایل، گزارش جدید را هم صریحاً پوشش داد و یافتهٔ واقعی تازه‌ای نداشت.
مقایسهٔ درون‌حافظه‌ای با مقدارهای خصوصی، صفر تطابق نشان داد؛ مقدار یا hash Secret چاپ/ذخیره نشد.
metadata کلید، JWT یا credential نیست. هیچ Production slot در metadata مربوط به AI User Secrets
این بررسی تشخیص داده نشد؛ Secret محیط تغییر نکرد و credential محیط Server از این نتیجه استنتاج نمی‌شود.
پشتیبان ۲۵فایلی جدید، پشتیبان ۲۷فایلی قبلی، سه Python محلی، پنج فایل Worktree توسعه و
۱۱۴ فایل دو Worktree مستندسازی با Hash کنترل شدند و محفوظ ماندند. پیش از Stage، اسکن و
بازبینی Diff انجام می‌شود؛ پاک‌سازی کورکورانه یا حذف Backup/کانفیگ مجاز نیست.

| Worktree | نتیجهٔ Audit و تصمیم |
| --- | --- |
| STEP10G | بدون Dirty و بدون Commit خارج از main؛ Probe معتبر از قبل موجود است، دوباره کپی نمی‌شود |
| configurable OTP | بدون Dirty و تمام تاریخچه داخل main؛ Real/Mock و Production guard موجودند |
| development test users | پنج Dirty؛ فقط preflight امن و checker منتقل می‌شود؛ گسترش scope دیتابیس و نسخهٔ قدیمی تست‌ها منتقل نمی‌شوند |
| docs-sync | ۹۷ فایل معادل main؛ هفت سند با نسخهٔ جدیدتر PH در main تفاوت دارند؛ بازنویسیِ عقب‌گرد انجام نمی‌شود |
| backend-handbook | یک فایل معادل و نه فایل draft/خروجی متفاوت؛ محفوظ و خارج از Commit این Task |
| سایر worktreeهای Backend | Commit خارج از main ندارند؛ تغییری داده نمی‌شوند |

تصمیم ۲۰ فایل اولیه پیش از Stage:

| File | Purpose | Commit? | Reason |
| --- | --- | --- | --- |
| `MakanApp.Api/appsettings.json` | default عمومی AI | بله | بدون Secret و با بودجهٔ موجود |
| `MakanApp.Infrastructure/Ai/AiServiceClient.cs` | سقف validation | بله | جداکردن سقف از default |
| `MakanApp.Infrastructure/Ai/AiServiceOptions.cs` | Options | بله | حفظ default کاری |
| `docs/ai-handoff/05_AI_ERROR_AND_RESILIENCE_CONTRACT.md` | قرارداد خطا | بله | سازگار با کد |
| `docs/release/DEFERRED_DECISIONS_AND_REMAINING_GAPS.md` | وضعیت محدودیت‌ها | بله | مانع خارجی مستقل |
| `tests/MakanApp.IntegrationTests/AiChatEndpointsTests.cs` | مجوز چندبافتی | بله | تست واقعی HTTP/SQL |
| `tests/MakanApp.UnitTests/Ai/AiRoleContextBuilderTests.cs` | Grounding/MaxScore | بله | تست قواعد projection |
| `tests/MakanApp.UnitTests/Ai/AiServiceClientTests.cs` | Resilience | بله | تست deterministic adapter |
| `tools/ai/MakanApp.AiIntegrationProbe/LiveAiObservation.cs` | مشاهدهٔ Probe | بله | Evidence صریح |
| `tools/ai/MakanApp.AiIntegrationProbe/LiveAiScenario.cs` | سناریو | بله | اجرای کنترل‌شده |
| `tools/ai/MakanApp.AiIntegrationProbe/Program.cs` | ورودی ابزار | بله | self-check و guard |
| `tools/ai/MakanApp.AiIntegrationProbe/ServiceAuthProbe.cs` | تست Auth | بله | بدون تغییر Auth برنامه |
| `tools/ai/README.md` | راهنمای ابزار | بله | رفع تناقض تاریخی |
| `docs/ai-handoff/10_BACKEND_REAL_E2E_GATE.md` | شاهد تاریخی | بله | نگهداری رسمی |
| `docs/ai-handoff/11_STEP10G_FAILURE_EVIDENCE_SANITIZED.md` | Evidence پالایش‌شده | بله | بدون Secret |
| `docs/ai-handoff/12_STEP10G_FINAL_REAL_E2E_REPORT.md` | گزارش تاریخی | بله | تفکیک وضعیت جدید |
| `tools/ai/MakanApp.AiIntegrationProbe/FailureEvidenceCapture.cs` | sanitizer | بله | پوشش Unit دارد |
| `tools/ai/provider_diagnostic.py` | Debug Provider | خیر | خارج از تحویل Backend، محفوظ |
| `tools/ai/run_exact_ai.py` | اجرای Snapshot AI | خیر | خارج از scope، محفوظ |
| `tools/ai/test_provider_diagnostic.py` | تست ابزار محلی | خیر | وابسته به مورد خارج از تحویل |

فایل‌های تکمیلی محدودند: قواعد ignore، Link/تست sanitizer، preflight/checker/تست/یادداشت Seed،
سرصفحهٔ گزارش قدیمی AI، لینک‌های شاخص و گزارش جاری؛ مجموعاً ۲۸ فایل تأییدشده‌اند.
قاعدهٔ قدیمی `[Rr]elease/` این گزارش جدید را ignore می‌کند؛ فقط همین فایل عمومی پس از
اسکن با مسیر صریح Stage می‌شود، نه با تغییر گستردهٔ ignore یا اضافه‌کردن Artifactها.
`.artifacts`، bin/obj/TRX، private config، .env، key یا JWT
Stage نمی‌شوند. Commit فقط پس از Validation موفق است؛ hash و نتیجهٔ واقعی Push/Fetch و تطبیق
Remote HEAD در گزارش نهایی گفتگو اعلام می‌شوند. Force-push، reset/clean و Deploy انجام نمی‌شود.

## ۱۳. محدودیت‌ها و پذیرش خارجی

آخرین focused retest واقعیِ قبلی، مطابق شواهد/تصمیم اعلام‌شدهٔ مالک:

| عملیات | نتیجهٔ قبلی | طبقه‌بندی |
| --- | --- | --- |
| Parent Child Summary | دو بار 504 / `AI_PROVIDER_TIMEOUT` نزدیک ۹۰ ثانیه | `REPRODUCIBLE_PROVIDER_TIMEOUT` |
| Teacher Activity Suggestions | Timeout سپس 200 | `TRANSIENT_RECOVERY` |
| Manager Class Insight | دو بار 200 | `CONSISTENT_PASS` |

Service Auth قبلاً با credential معتبر PASS شده بود؛ منفی‌های token/claim، 401 و scope غایب،
403 داشتند. این Consolidation هیچ Live AI request، Full-12، اجرای Provider، تغییر AI repository،
SMS واقعی یا Deploy ندارد. `Real AI Production Acceptance = DEFERRED` و
`FULL_12_REAL_E2E = DEFERRED`؛ مانع Parent Provider Timeout، blocker مستقل Backend Commit نیست.
Production RAG lifecycle، Push runtime و Media/infra gateهای قبلی از این مرحله بسته نمی‌شوند.
ابزارهای Python و draftهای سایر Worktreeها عمداً باقی می‌مانند؛ CLEAN بودن همهٔ Workspaceها ادعا نمی‌شود.

## ۱۴. نکته‌های آموزشی

۱. implementation، تست deterministic و پذیرش Provider سه شاهد متفاوت‌اند.
۲. default timeout با سقف مجاز configuration یکی نیست؛ Retry خطا را اثبات یا رفع نمی‌کند.
۳. Request/Response باید هم شناسه و هم referenceهای مجاز همان context را رعایت کنند.
۴. guard قبل از INSERT لزوماً جلوی compile زودهنگام SQL را نمی‌گیرد؛ compilation مرز جدا دارد.
۵. sanitizer را با canary و مسیر fail-closed تست کنید؛ Regex تنها جایگزین allowlist نیست.
۶. Worktree قدیمی می‌تواند کار معتبر داشته باشد؛ فقط جزء سازگار را منتقل کنید، نه همهٔ فایل‌ها را.

## ۱۵. مسیر پیشنهادی مطالعه

`AiController` → `AiChatService`/`AiRoleContextService` → `AiRoleContextBuilders`
→ `EfAiScopeStore` و read serviceهای گزارش → DTOهای `AiServiceContracts`/`AiRoleContextContracts`
→ `HmacAiServiceCredentialProvider` → `AiServiceClient`/`AiResponseGrounding`
→ `AiServiceCircuitBreaker`/`AiServiceTelemetry` → تست‌های AI → Probe و sanitizer → تست‌های Seed.

## ۱۶. مرحلهٔ بعد

تیم AI علت Provider Timeout عملیات Parent Child Summary را برطرف کند؛ سپس با درخواست جداگانه
یک focused_three واقعی اجرا شود. فقط پس از ۳/۳ موفق، Full-12 واقعی قابل بررسی است.
تغییر Service Auth، افزایش Timeout، Retry یا deploy خودکار نباید وارد این مرحله شود.
