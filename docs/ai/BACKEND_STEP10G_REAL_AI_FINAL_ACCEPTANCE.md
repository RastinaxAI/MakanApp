# STEP10G v2 — پذیرش Backend و مرز اجرای واقعی AI

تاریخ بررسی: `2026-10-18` (Asia/Tehran).

**وضعیت این اجرا: `AI_BACKEND_SERVER_EXECUTION_REQUIRED`.** تکمیل بخش محلی، معادل تکمیل AI Core نیست. هیچ درخواست این اجرا از Windows Backend Server به Ollama واقعی نرسیده است.

## ۱. مراجع، نسخه‌ها و محدوده

| مورد | مقدار / نوع شاهد |
|---|---|
| Backend شروع | `ef6762462bda4415a3d052454fcf53af7fa6b999`؛ پس از fetch اولیه برابر origin/main بود |
| تغییر مستقل remote در حین کار | `e23beec3e2b6fe3624c116436622bdf4364bf4f1`؛ فقط مستندات و ابزار مستندسازی؛ بدون هم‌پوشانی، fast-forward شد |
| worktree | `D:\Project\MakanApp\MakanApp-Backend-AI-Final` |
| branch / upstream | `ai/final-step10g-v2` / `origin/main` |
| مخزن Backend | `https://github.com/RastinaxAI/MakanApp.git` |
| AI source ممیزی‌شده | `964d7d4edf0b0a83344328ef144748438ab0a55e`؛ فقط خوانده شد؛ SHA runtime در این اجرا تأیید نشده |
| image هدف طبق تحویل | `ghcr.io/rastinaxai/makanapp-ai:964d7d4edf0b` |
| digest هدف طبق تحویل | `sha256:30e017e4b1bdcc0a88eaf248be1153252a67e8857ea2ceff996ebbb737448934`؛ مستقل روی سرور inspect نشده |
| handoff | `MakanApp_AI_Backend_Production_Handoff_FA_v2.0.pdf`؛ هر ۴۱ صفحه خوانده شد |
| محل PDF | `C:\Users\Diag\Downloads\Telegram Desktop\MakanApp_AI_Backend_Production_Handoff_FA_v2.0.pdf` |
| SHA256 سند | `404FDC61920FF94E4CEEC6BB6206DCD7A77C4DD6C5FAC640EE0904A5049B5F9B` |
| قرارداد | `v1`؛ DTOها با `app/schemas` و `tests/fixtures/openapi_step15_contract_v1.json` همان commit تطبیق داده شدند |

متن قدیمی `docs/handoff` مخزن AI در بعضی قسمت‌ها هنوز وضعیت پیش از S2S را شرح می‌دهد؛ برای اجرا، کد و OpenAPI commit مصوب و handoff v2.0 مبنا بودند. مدل، provider و alias تصمیم کسب‌وکار Backend نیستند. هیچ تغییر، checkout، نصب dependency، commit یا push در AI انجام نشد.

تغییرات قدیمی AI در worktree اصلی، از جمله کد، config، تست، ابزار و شواهد untracked آن، منتقل/برگردانده/stage نشدند. diff آن‌ها فقط خوانده شد؛ timeout قدیمی ۳۵ ثانیه عمداً منتقل نشد. شواهد تاریخی OpenRouter 403 حذف نشدند؛ آن رخداد، مانع provider فعلی محسوب نمی‌شود.

## ۲. شبکه، Secret و Service JWT

مسیر الزامی: Windows Backend `10.77.0.1` → WireGuard → AI `http://10.77.0.2:8001` → provider داخلی. Backend هیچ تماس مستقیمی با `127.0.0.1:11434` یا public IP سرویس AI ندارد.

در رایانهٔ توسعهٔ این اجرا، رابط `10.77.*` وجود نداشت؛ ping یک‌باره timeout و curl مستقیمِ بدون proxy برای health و ready هر دو `HTTP 000` دادند. این **شکست اتصال همین رایانه** است، نه اثبات خرابی سرور Production. gateهای واقعی سرور `NOT_RUN` هستند.

| بررسی واقعی | این اجرا | شاهد handoff تیم AI (اجرای مستقل ما نیست) |
|---|---|---|
| health / ready از Windows Backend | NOT_RUN | هر دو 200 |
| protected smoke / Ollama | NOT_RUN | 200؛ پیش/پس از reboot |
| no-JWT / missing-scope | NOT_RUN | 401 / 403 |
| rotation با previous key | NOT_RUN | نیازمند تنظیم rotation و اجرای کنترل‌شده |
| reboot persistence | NOT_RUN | Docker/Ollama/WireGuard/proxy پس از reboot بالا آمده‌اند |

ماتریس واقعی Auth هنوز کامل اجرا نشده: no JWT، malformed، expired، bad signature، unknown kid، wrong issuer، wrong audience، wrong subject باید 401؛ token معتبر بدون scope باید 403؛ token معتبر و DTO/context مجاز باید 200 بدهد. تست تولید JWT محلی، جای این ماتریس واقعی را نمی‌گیرد.

`HmacAiServiceCredentialProvider` برای هر درخواست HS256 با header دقیق `alg/typ/kid` می‌سازد. claims فقط `iss/aud/sub/scope/iat/nbf/exp/jti` هستند؛ `jti` یکتا، TTL پیش‌فرض ۱۲۰ و سقف ۳۰۰ ثانیه است. کلید، همان UTF-8 رشتهٔ مشترک است و Base64-decode نمی‌شود. user، role و organization داخل JWT نیستند.

نگاشت Secretها، **نام setting است نه مقدار**:

| AI | Backend environment معادل |
|---|---|
| `MAKAN_AI_AUTH_CURRENT_KEY_ID` | `AiService__Authentication__CurrentKeyId` |
| `MAKAN_AI_AUTH_CURRENT_KEY` | `AiService__Authentication__CurrentKey` |

در این مرحله هیچ secret واقعی خوانده، درخواست، چاپ، کپی به config یا commit نشد. تنظیمات همین shell، اثبات provisioning حساب IIS نیست؛ وضعیت secret سرویس Windows باید توسط operator و بدون نمایش مقدار بررسی شود.

اگر بررسی operator نشان داد کلیدها در context واقعی سرویس provision نشده‌اند، قبل از protected acceptance باید `AI_SECRET_HANDOFF_REQUIRED` ثبت و انتقال امن انجام شود؛ نبود آن‌ها در shell توسعه به‌تنهایی چنین حکمی دربارهٔ IIS نمی‌دهد.

## ۳. دوازده عملیات

همهٔ routeها `POST`، احراز هویت session و authorization جاری Backend دارند. ستون محلی فقط تست adapter با پاسخ ساختگی است؛ ۱۲/۱۲ واقعی نیست. پیشوند هر دو ستون route، `/api/v1/ai` است.

| عملیات | route عمومی Backend | route داخلی AI | محلی / واقعی |
|---|---|---|---|
| Chat | `/chat` | `/chat` | PASS / NOT_RUN |
| Student Analysis | `/students/analyze` | `/students/analyze` | PASS / NOT_RUN |
| Study Plan | `/study-plan` | `/study-plan` | PASS / NOT_RUN |
| Homework Explanation | `/homeworks/{assignmentId}/explain` | `/homeworks/explain` | PASS / NOT_RUN |
| Exam Preparation | `/exams/{examId}/prepare` | `/exams/prepare` | PASS / NOT_RUN |
| Parent Child Summary | `/parents/child-summary` | `/parents/child-summary` | PASS / NOT_RUN |
| Teacher Class Summary | `/teachers/classes/{classId}/summary` | `/teachers/class-summary` | PASS / NOT_RUN |
| Teacher Student Insight | `/teachers/classes/{classId}/students/{studentId}/insight` | `/teachers/student-insight` | PASS / NOT_RUN |
| Teacher Activity Suggestions | `/teachers/classes/{classId}/activity-suggestions` | `/teachers/activity-suggestions` | PASS / NOT_RUN |
| Manager School Summary | `/managers/school-summary` | `/managers/school-summary` | PASS / NOT_RUN |
| Manager Class Insight | `/managers/classes/{classId}/insight` | `/managers/class-insight` | PASS / NOT_RUN |
| Manager Student Insight | `/managers/classes/{classId}/students/{studentId}/insight` | `/managers/student-insight` | PASS / NOT_RUN |

سه شکست تاریخی: **Parent Child Summary، Teacher Activity Suggestions، Manager Class Insight** هر سه در این اجرا `NOT_RUN`؛ طبق PDF هر سه در تست تیم AI 200 گرفته‌اند. نتیجهٔ نقش‌های Student، Parent، Teacher و Manager در مسیر Production این اجرا همگی `NOT_RUN` است.

قراردادهای عمومی عوض نشدند: Chat با `SendAiChatCommand` و خروجی `AiChatResult`؛ Study Plan با goals/availableMinutes/notes؛ Homework با question و شناسهٔ مسیر؛ Teacher Activity با objective/availableMinutes/materials/notes؛ سایر عملیات بدون body آموزشیِ آزاد از client. Application خودش actor/scope/context را می‌سازد. پاسخ‌ها همان DTOهای `AiServiceContracts` و `AiRoleContextContracts` هستند. validation محلی 400، مجوز نامعتبر 403، dependency error طبق جدول پایین است. Frontend نباید AI داخلی را صدا بزند.

## ۴. Authorization، Grounding و Privacy

| سناریوی منفی | شاهد محلی | اجرای سرور |
|---|---|---|
| organization/workspace اشتباه | `Chat_MultiOrganizationScope_FollowsServerSelectedWorkspaceOnly`، resourceهای cross-organization | NOT_RUN |
| membership revoked / suspended | `Chat_PersonalOrRevokedWorkspace_IsDeniedWithoutCallingAi` / `Chat_SuspendedMembership_IsDeniedBeforeAiCall` | NOT_RUN |
| Parent cross-child / relation revoked | `GuardianEndpointsTests.ParentCannotSelectArbitraryLearner` / `ParentSummary_RequiresSelectedActiveRelation` | NOT_RUN |
| Teacher cross-school/class | `TeacherSummary_RequiresActiveAssignmentToRequestedClass` / `StudentInsight_DeniesForeignClassOrStudentWithoutEnrollmentBeforeAiCall` | NOT_RUN |
| Teacher student بدون enrollment | همان تست StudentInsight؛ AI اصلاً فراخوانی نمی‌شود | NOT_RUN |
| Manager cross-school/org/student | `ManagerClassInsight_RejectsCrossOrganizationClass` / همان تست StudentInsight | NOT_RUN |
| role mismatch | `ManagerSummary_DeniesRoleMismatchBeforeAiCall` | NOT_RUN |

`AccessContext`، resolver، report read services و `EfAiScopeStore` مالک مجوز هستند؛ JWT جای آن‌ها نیست. learner همان `OrganizationPerson.Id` است. شناسهٔ منبع فقط از payload مجاز همان درخواست پذیرفته می‌شود: `id` صریح اولویت دارد؛ نامعتبر بودنش با alias جبران نمی‌شود؛ در نبود آن فقط alias واقعی `assignmentId/examId/subjectId/gradeId` مجاز است. `Class` فقط در Manager School و `Student` فقط در insight انتخاب‌شده پذیرفته می‌شود؛ roster، `classId` و `assessmentId`، subject/grade جعلی نمی‌سازند.

محدودیت عمدی نگاشت موجود: projection درس فعلاً `classId` و grade report فعلاً `assessmentId` دارد؛ از این‌ها `subjectId/gradeId` ساخته نشد. در کد AI مصوب، StudentCopilot نسبت به Parent/Teacher/Manager در lookup منبع محدودتر است و `id` را می‌خواند؛ این تفاوت با aliasهای handoff ثبت می‌شود، نه با دستکاری AI یا ساختن id پنهان. source خالی مجاز است؛ اثبات grounded inference واقعی نیازمند retest است.

Contextها projection صریح و bounded از دادهٔ آموزشی‌اند؛ entity کامل، contacts، Messenger، attachment خصوصی، moderation، payment، audit، OTP، session token، signing key و چت خصوصی Student-AI به نقش دیگر فرستاده نمی‌شود. Chat فعلی `AiAuthorizedContext.Empty` دارد و history خصوصی به آن اضافه نشد. source/entity پاسخ خارج از مجموعهٔ مجاز و upcoming title/date نامنطبق رد می‌شود.

## ۵. Timeout، Retry، Error و Resilience

- options، appsettings و DI همگی ۱۸۰ ثانیه؛ HttpClient نیز finite است. همان budget تا پایان خواندن و deserialize بدنه جاری است.
- caller cancellation همان cancellation باقی می‌ماند؛ timeout داخلی `AI_SERVICE_TIMEOUT`؛ قطع اتصال/بدنه `AI_SERVICE_UNAVAILABLE` است.
- retry خودکار برای هیچ POST نداریم؛ در نتیجه 401/403/422/429/502/503/504 باعث retry storm نمی‌شوند. سقف «حداکثر یک retry» مجوز الزامی برای افزودن retry نیست.
- 429 circuit را باز نمی‌کند؛ timeout، transport، 5xx و پاسخ نامعتبر failure گذرا هستند. circuit موجود process-local با threshold=3 و break=30s حفظ شد؛ recovery محلی با clock کنترل‌شده تست شد.
- limiter موجود Backend برای AI برابر ۲۰ درخواست/دقیقه به ازای user (fallback IP)، queue=0 است؛ quota تجمعی سازمانیِ تازه و بدون تصمیم عددی ساخته نشد. Testing limit بالاتر دارد؛ تست adapter 429 اثبات ظرفیت Production نیست.
- ظرفیت AI طبق handoff: ۱ active + ۲ queued، انتظار تا ۶۰s، provider تا ۹۰s؛ busy کنترل‌شده خرابی سرور نیست. آزمایش بار واقعی در این اجرا انجام نشد.

بدنهٔ خام خطای dependency عمداً خوانده/به client عبور داده نمی‌شود؛ vocabulary سمت AI الزاماً همان public code قدیمی Backend نیست:

| AI status / code | Backend public status / code |
|---|---|
| 401 `AI_SERVICE_UNAUTHORIZED` | 503 `AI_SERVICE_UNAUTHORIZED` |
| 403 `AI_SERVICE_FORBIDDEN` | 503 `AI_SERVICE_FORBIDDEN` |
| 422 `AI_INVALID_REQUEST` یا `AI_INVALID_CONTEXT` | 400 `AI_INVALID_REQUEST` |
| 429 `AI_RATE_LIMITED` | 429 `AI_RATE_LIMITED` |
| 502 `AI_RESPONSE_INVALID` یا `AI_INTERNAL_ERROR` | 502 `AI_RESPONSE_INVALID` |
| 503 `AI_MODEL_UNAVAILABLE` | 503 `AI_SERVICE_UNAVAILABLE` |
| 504 `AI_PROVIDER_TIMEOUT` | 504 `AI_SERVICE_TIMEOUT` |

تغییر مستند این مرحله: 502 قبلاً به 503 عمومی می‌رفت؛ اکنون 502 امن و بدون body dependency می‌دهد. سایر codeهای عمومی حفظ شدند. تست خطای dependency سپس موفقیت `/api/v1/me` را هم بررسی می‌کند؛ dependency AI موجب ازکارافتادن مسیر غیر-AI نمی‌شود.

## ۶. Structured Output و Correlation

Response با unknown-field ممنوع، نام دقیق camelCase، اعداد strict، metadata الزامی، envelope مساوی request و کنترل non-null بررسی می‌شود. `processingTimeMs` غایب دیگر صفر پیش‌فرض محسوب نمی‌شود؛ severity عددی یا با case اشتباه پذیرفته نیست. missing field، wrong type، unknown field، malformed JSON، requestId/correlationId اشتباه، source نامجاز و null عضو collection تست دارند؛ validation برای مدل کوچک ضعیف نشد.

`CorrelationIdMiddleware` ورودی امن را به TraceIdentifier تبدیل می‌کند؛ `AiRoleContextService` requestId تازه می‌سازد؛ `AiServiceClient` correlation را یکسان در header/body می‌فرستد. تست HTTP محلی `corr-http-e2e` در مسیر fake adapter و `corr-ai-manager` در Controller را بررسی می‌کند. تست log ثبت‌نشدن متن پرسش، پاسخ و access token را می‌سنجد. metricها فقط operation/outcome/error.code/duration هستند؛ prompt/context/source catalog/JWT/PII label نمی‌شوند. برای trace واقعی باید requestId/correlationId خروجی ابزار با log امن AI همان درخواست تطبیق داده شود؛ هنوز انجام نشده.

خطای خام credential/network/JSON به exception امن ضمیمه نمی‌شود؛ generic logger میزبان ممکن است `Exception.ToString()` و InnerException را چاپ کند. تست transport و credential علاوه بر Message، همین ToString و نبود InnerException را می‌سنجد. این کار صرفاً سانسور UI نیست؛ مرز لاگ نیز محافظت می‌شود.

## ۷. Validation واقعی این اجرا

| اجرا | Unit | Integration | Architecture | جمع | Failed / Skipped |
|---|---:|---:|---:|---:|---|
| baseline تمیزِ کامپایل‌شده از SHA شروع | 558 | 1023 | 14 | 1595 | 0 / 0 |
| focused AI تکمیل‌شده | 99 | 28 | جدا در suite کامل | 127 | 0 / 0 |
| regression کامل پیش از دو تست severity | 604 | 1034 | 14 | 1652 | 0 / 0 |
| suite کامل پیش از پاک‌سازی InnerException | 606 | 1034 | 14 | 1654 | 0 / 0 |
| final build/test پس از اصلاح log | 606 | 1034 | 14 | 1654 | 0 / 0 |

restore و final build بعد از اصلاحات موفق، warnings=0 و errors=0. تمام ۱۶۵۴ تست نهایی واقعاً اجرا و موفق شدند؛ ۴۸ Unit و ۱۱ Integration نسبت به baseline اضافه شده‌اند. نتایج محلی در `.artifacts/step10g/final-verified` هستند؛ Integration نهایی ۷ دقیقه و ۲۵ ثانیه طول کشید. یک build میانی هنگام اجرای baseline به‌دلیل قفل DLL توسط testhost با MSB3021/MSB3027 شکست خورد؛ بدون توقف تست، پس از پایان آن build تکرار و موفق شد. این هشدارها پنهان نشدند.

بررسی EF با startup=Api ابتدا به‌دلیل PrivateAssets ابزار Design خطا داد؛ فرمان صحیح موجودِ پروژه، بدون اتصال/اعمال migration، موفق بود:

```powershell
dotnet ef migrations has-pending-model-changes --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --no-build
```

نتیجه: `No changes have been made to the model since the last migration.` هیچ migration جدید، تغییر جدول یا schema و اجرای migration وجود ندارد. آخرین migration موجود `20261017033804_AddConversationChecklists` است.

syntax ابزار PowerShell موفق؛ اجرای `-PreflightOnly` در همین رایانه دقیقاً با exit=2 و `AI_BACKEND_SERVER_EXECUTION_REQUIRED` متوقف شد. اجرای اصلی ابزار و توکن‌های واقعی `NOT_RUN` هستند.

## ۸. فرمان‌های دقیق Windows Backend Server

این فرمان‌ها **در این مرحله روی سرور اجرا نشده‌اند**. نه نیازمند دستکاری AI، نه restart/redeploy AI و نه migration هستند. ابتدا نسخهٔ Backend همین تغییرات از workflow انتشار مجاز مستقر شود؛ source فعلی به‌تنهایی نسخهٔ deployed را ثابت نمی‌کند.

```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object IPAddress -eq '10.77.0.1'
ping -n 4 10.77.0.2
curl.exe --noproxy '*' --connect-timeout 5 --max-time 10 -sS -o NUL -w 'health=%{http_code}\n' http://10.77.0.2:8001/api/v1/health
curl.exe --noproxy '*' --connect-timeout 5 --max-time 10 -sS -o NUL -w 'ready=%{http_code}\n' http://10.77.0.2:8001/api/v1/ready
```

برای process میزبان، فقط مقادیر غیرحساس زیر باید resolve شوند؛ ثبت آن‌ها در shell مدیر الزاماً تنظیم IIS نیست. secretهای دو محیط باید قبلاً توسط operator از کانال امن به نام‌های معادل منتقل شده باشند؛ مقدارشان را در command history/چت قرار ندهید:

```text
AiService__Enabled=true
AiService__BaseUrl=http://10.77.0.2:8001
AiService__TimeoutSeconds=180
AiService__ContractVersion=v1
AiService__Authentication__Issuer=makan-backend
AiService__Authentication__Audience=makan-ai
AiService__Authentication__ServiceName=makan-backend
AiService__Authentication__Scope=makan.ai.invoke
AiService__Authentication__TokenLifetimeSeconds=120
```

تنظیم appsettings مشترک عمداً `Enabled=false` و key خالی باقی می‌ماند؛ activation بدون provisioning امن انجام نشد. برای بررسی فقط وجود secret در همان shell/service context مجاز (بدون چاپ مقدار):

```powershell
foreach ($setting in @('AiService__Authentication__CurrentKeyId','AiService__Authentication__CurrentKey')) {
    [pscustomobject]@{ Setting=$setting; Configured=(-not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($setting))) }
}
```

ابزار `tools/ai/Invoke-Step10GBackendSmoke.ps1` را از checkout همین نسخه یا همراه آن به Windows Backend منتقل و اجرا کنید؛ هیچ secret داخل ابزار نیست. Base URL مثال، IIS همان ماشین است؛ اگر binding دیگری دارید URL صحیح HTTPS یا loopback را مشخص کنید. redirect دنبال نمی‌شود. اسکریپت فایل یا دیتابیس ایجاد نمی‌کند، ولی workspace/child انتخاب‌شدهٔ همان نشست‌های تست را تغییر می‌دهد.

```powershell
Set-Location 'D:\Project\MakanApp\MakanApp-Backend-AI-Final'
powershell.exe -NoProfile -File .\tools\ai\Invoke-Step10GBackendSmoke.ps1 -PreflightOnly
powershell.exe -NoProfile -File .\tools\ai\Invoke-Step10GBackendSmoke.ps1 -BackendBaseUrl 'http://127.0.0.1'
```

چهار حساب/نشست تست مجاز و دادهٔ آموزشی غیرخالی لازم است: Student با enrollment و assignment/exam منتشرشده، Parent با GuardianRelation فعال، Teacher با assignment و learner enrolled، Manager همان سازمان/کلاس. توکن‌ها فقط با SecureString در ترمینال خوانده می‌شوند؛ GUIDها باید واقعی باشند و از record مجاز پروژه بیایند. ابزار، no-JWT=401 و ۱۲ مسیر عمومی را ترتیبی/بدون retry اجرا و فقط status/requestId/correlationId چاپ می‌کند. empty-data deterministic response، یا صرف 200، اثبات inference نیست؛ log امن AI و image/digest باید مستقل تطبیق داده شوند.

این ابزار **ماتریس کامل JWT منفی، load/429 واقعی، fault injection، rotation و reboot را اجرا نمی‌کند**. این‌ها gateهای مستقل باز هستند؛ با اجرای ابزار به‌تنهایی `AI_CORE_INTEGRATION_COMPLETE` صادر نکنید. اختلال/بار فقط در پنجرهٔ کنترل‌شده و بدون مختل‌کردن AI عملیاتی انجام شود. مقدار JWT/secret یا raw body برای جمع‌آوری شاهد لازم نیست.

## ۹. گزارش آموزشی توسعه‌دهنده

### خلاصه مرحله

قبل: timeout کوتاه، خواندن بدنه بدون budget عملیاتی، metadata عددی غایب قابل پذیرش و source غیرخالی ولی بی‌ارتباط قابل قبول بود. بعد: budget واقعی ۱۸۰ ثانیه، validation سخت‌تر و مرز شفاف local در برابر real server داریم؛ قابلیت AI جدید اضافه نشد.

### معماری و جریان اجرا

Domain تغییر نکرد. Application مالک DTO/use case و مجوز موجود ماند؛ فقط required بودن metadata/priority واقعی مشخص شد. Infrastructure مالک HTTP timeout، wire-contract validation و source grounding است. Api فقط کانفیگ غیرحساس تغییر کرد؛ Controller و public route همان است.

جریان نمونه: `AiController.SummarizeChild` → `AiRoleContextService` → `ParentChildContextBuilder` → `IAccessContextResolver` و report port → SQL Server با مجوز جاری → `IAiServiceClient` → `HmacAiServiceCredentialProvider` → `AiServiceClient` → private AI → validation envelope/DTO → `AiResponseGrounding` → HTTP response. Service JWT مجوز آموزشی ایجاد نمی‌کند؛ network call داخل transaction نگه داشته نمی‌شود.

### فایل‌های مهم ایجادشده

- `MakanApp.Infrastructure/Ai/AiResponseGrounding.cs`: source/entity و upcoming item را با payload همان درخواست تطبیق می‌دهد؛ نکتهٔ آموزشی: شناسهٔ non-empty الزاماً مرجع معتبر نیست.
- `tools/ai/Invoke-Step10GBackendSmoke.ps1`: مشاهدهٔ امن ۱۲ مسیر عمومی روی Windows Server؛ نکته: secret/session token را به CLI argument، فایل یا خروجی تبدیل نکنید.
- همین سند: منبع نسخه‌ها، نتایج واقعی و فرمان‌های بازتولید؛ شاهد تیم دیگر را شاهد اجرای خود ننامید.

### فایل‌های مهم تغییرکرده

- `AiServiceOptions.cs`، `DependencyInjection.cs`، `MakanApp.Api/appsettings.json`: private BaseUrl و timeout=180 بدون secret؛ feature پیش‌فرض همچنان خاموش.
- `AiServiceClient.cs`: timeout بدنه، خطاهای transport امن، strict JSON، null عضو مجموعه و 502 mapping؛ مسیر موفق پس از grounding بازمی‌گردد.
- `AiServiceContracts.cs` و `AiRoleContextContracts.cs`: required بودن fieldهای واقعاً الزامی؛ مقدار صفر پیش‌فرض نباید field غایب را معتبر کند.
- `AiServiceClientTests.cs`، `HmacAiServiceCredentialProviderTests.cs`، `AiRoleContractTests.cs`، `AiChatEndpointsTests.cs`: پوشش قرارداد، JWT، privacy، scope و failure/recovery.
- `docs/ai-handoff/05_AI_ERROR_AND_RESILIENCE_CONTRACT.md`: رفتار عمومی واقعی و timeout جدید، بدون بازنویسی تاریخ OpenRouter.

### دیتابیس و Endpointها

هیچ entity، table، PK/FK/index/constraint/rowversion یا migration عوض نشد. تغییر API عمومی route/request نیست؛ پاسخ dependency با status 502 اکنون 502 امن است. مشخصات endpointها در جدول ۱۲ عملیات و DTOهای نام‌برده آمده است.

### مفاهیم و تصمیم‌های طراحی

CTS پیوندی، deadline بدنه را هم پوشش می‌دهد؛ HttpClient مستقل از آن finite است. serializer غایب‌بودن field الزامی را با مقدار default اشتباه نمی‌گیرد. source catalog از request بازسازی می‌شود، نه lookup جدید SQL. trade-off: یک serialization محدود اضافه برای defense-in-depth داریم، اما payload/log اضافی ذخیره نمی‌شود. no-retry موجود حفظ شد تا کار inference و هزینه تکراری نشود. provider-neutral ماندیم و Hmac provider/circuit/resolver موجود را reuse کردیم.

### تست‌ها و Validation

Unit: امضا و claims واقعی، jti یکتا/TTL، ۱۲ route، header/body correlation، نوع عدد، field غایب/ناشناخته، source/alias، selected student، upcoming title، timeout بدنه، قطع اتصال و circuit recovery. Integration: host/session واقعی و SQL Server تست ایزوله با AI ساختگی؛ suspension، cross-scope/enrollment، role mismatch، ProblemDetails و سلامت `/me`. Architecture: همان تست‌های dependency direction اجرا می‌شوند؛ تست بی‌معنا یا disabled اضافه نشد. اعداد نهایی در بخش Validation ثبت می‌شوند، نه از baseline حدس زده شوند.

### Git و محدودیت‌ها

این سند همراه تغییرات همان مرحله نسخه‌بندی می‌شود؛ خروجی واقعی commit/push و verification در گزارش نهایی گفتگو ثبت می‌شود. در checkout نهایی، `git log -1 --format='%H %s' -- docs/ai/BACKEND_STEP10G_REAL_AI_FINAL_ACCEPTANCE.md` commit دقیق این گزارش را می‌دهد؛ درج hash خودِ همان commit در متنش ممکن نیست. پیام scoped این مرحله `fix(ai): harden contract v1 integration for private runtime` است. فقط فایل‌های همین مرحله stage می‌شوند؛ pre-existing AI workspace در commit وارد نمی‌شود.

Production RAG برابر **DEFERRED** است؛ نه vector/Qdrant/index/upload عمومی و نه direct AI SQL اضافه شد. provider فعلی طبق handoff Ollama/`education-fast`/`qwen2.5:1.5b` با latency ده‌ها ثانیه و سخت‌افزار کوچک است؛ ظرفیت کنترل‌شده SLA پرتراکنش نیست. key provisioning، runtime digest، Auth منفی واقعی، ۱۲/۱۲ و سه مورد تاریخی، 429 واقعی و failure/recovery سرور هنوز شاهد مستقل ندارند؛ این‌ها به دسترسی/اجرای Windows Backend نیاز دارند، نه تغییر محصول.

### نکات آموزشی و مسیر مطالعه

۱. داشتن 200 یا تست fake، inference واقعی را ثابت نمی‌کند. ۲. cancellation کاربر را timeout ننامید. ۳. budget فقط تا header کافی نیست. ۴. اعتبار منبع را از payload مجاز بسنجید، نه شکل GUID. ۵. service identity را از مجوز کاربر جدا نگه دارید. ۶. error body dependency را به UI یا log نسپارید.

ترتیب مطالعه: `AiController` → `AiRoleContextService` → `AiRoleContextBuilders` → `EfAiScopeStore`/report services → `HmacAiServiceCredentialProvider` → `AiServiceClient` → `AiResponseGrounding` → چهار فایل تست تغییرکرده → ابزار smoke و همین ماتریس پذیرش. Domain/EF migration تازه‌ای برای مطالعهٔ این مرحله وجود ندارد.

مرحلهٔ بعد فقط اجرای gateهای باقی‌مانده روی Windows Backend و ثبت شواهد امن است؛ AI deployment سالم را صرفاً برای این سند restart نکنید و RAG/scale را با این مرحله مخلوط نکنید.

## ۱۰. پرچم‌های خروجی

`PASS`های بخش کد، فقط local code/tests هستند؛ flag واقعی با نبود محیط `NOT_RUN` است. final regression کامل واقعاً اجرا و موفق شده است.

```text
AI_CONTRACT_V1 = PASS
AI_WIREGUARD_CONNECTIVITY = NOT_RUN
AI_HEALTH_READY = NOT_RUN
AI_SERVICE_AUTH = NOT_RUN
AI_REAL_PROTECTED_SMOKE = NOT_RUN
AI_PREVIOUSLY_FAILING_3 = NOT_RUN
AI_ALL_12_OPERATIONS = NOT_RUN
AI_AUTHORIZATION_NEGATIVE = PASS
AI_IDENTIFIER_GROUNDING = PASS
AI_PRIVACY = PASS
AI_STRUCTURED_OUTPUT = PASS
AI_RATE_LIMIT_429 = NOT_RUN
AI_RESILIENCE = PASS
AI_CORRELATION = PASS
AI_BACKEND_REGRESSION = PASS
AI_DIRECT_SQL_ACCESS = NO
AI_PRODUCTION_RAG = DEFERRED
```

وضعیت نهایی این اجرا، با وجود موفقیت validation محلی، به‌دلیل نبود مسیر سرور:

`AI_BACKEND_SERVER_EXECUTION_REQUIRED`
