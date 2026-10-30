# اتصال Backend به AI Contract v1

مبنای این مرحله `eea1dde087cfc4788827a05c168ae6889b99e74e` و راهنمای رسمی `MakanApp_AI_Backend_Integration_Guide_FA.pdf` است. قرارداد مستقیم AI تغییر نکرده است. این مرحله کد و پذیرش محلی با سرویس کنترل‌شده را تکمیل می‌کند؛ اثبات اجرای Provider واقعی، فعال‌سازی Production یا رفع timeout قبلی نیست. [دستورالعمل مرحلهٔ بعد](AI_V1_SERVER_ACTIVATION_RUNBOOK_FA.md).

## معماری و جریان اجرا

```text
AiController
→ AiChatService / AiRoleContextService
→ ContextBuilder + SourceSelection authorization
→ AiInvocationExecutor
→ CommercialSubscriptionService.ReserveSimpleTextAsync
→ reauthorize current context
→ IAiServiceClient / AiServiceClient
→ HmacAiServiceCredentialProvider
→ AI HTTP Contract v1
→ defensive response validation
→ CommercialSubscriptionService.FinalizeAsync
→ public result
```

Application هماهنگی مجوز و سهمیه را مالک است؛ Infrastructure پیاده‌سازی SQL و HTTP را دارد. Domain تجاری، جدول‌ها و قرارداد JWT موجود reuse شده‌اند. Controller فقط ورودی HTTP و دو header اختیاری را تبدیل می‌کند. هیچ Provider adapter جدید، مخزن عمومی، MediatR یا schema جدیدی اضافه نشده است.

## نگاشت هر ۱۲ عملیات

تمام مسیرها `POST`، با نشست معتبر Backend و organization workspace جاری هستند. پیشوند دو ستون Route برابر `/api/v1/ai/` است. `studentId` در مسیر و payload، `OrganizationPersonId` است؛ `actor.userId` همان UserId حساب، `organizationId` همان OrganizationId و `schoolId` دقیقاً همان OrganizationId است.

| عملیات | Route عمومی Backend | Route سرویس AI | Context Builder / دادهٔ مجاز |
|---|---|---|---|
| Chat | `chat` | `chat` | `AiChatService`؛ Student/Parent/Teacher/Manager جاری، پیام محدودشدهٔ خود درخواست؛ Student با پروندهٔ فعال خود |
| Student Analysis | `students/analyze` | `students/analyze` | `StudentContextBuilder`؛ کلاس، حضور و گزارش آموزشی خود فرد |
| Study Plan | `study-plan` | `study-plan` | `StudentContextBuilder`؛ دادهٔ آموزشی خود فرد، goals و preferences محدود |
| Homework Explain | `homeworks/{assignmentId}/explain` | `homeworks/explain` | `StudentContextBuilder` + `EfAiScopeStore`؛ تکلیف منتشرشدهٔ قابل‌دسترسی در کلاس فعال |
| Exam Prepare | `exams/{examId}/prepare` | `exams/prepare` | `StudentContextBuilder` + `EfAiScopeStore`؛ metadata نسخهٔ منتشرشده؛ بدون سؤال و کلید پاسخ |
| Parent Child Summary | `parents/child-summary` | `parents/child-summary` | `ParentChildContextBuilder`؛ فرزند انتخاب‌شده و رابطهٔ Guardian فعال |
| Teacher Class Summary | `teachers/classes/{classId}/summary` | `teachers/class-summary` | `TeacherClassContextBuilder`؛ TeacherAssignment فعال همان کلاس |
| Teacher Student Insight | `teachers/classes/{classId}/students/{studentId}/insight` | `teachers/student-insight` | `TeacherStudentContextBuilder`؛ انتساب مدرس و enrollment همان فراگیر |
| Teacher Activity Suggestions | `teachers/classes/{classId}/activity-suggestions` | `teachers/activity-suggestions` | `TeacherClassContextBuilder`؛ هدف، زمان و مواد محدودشده و کلاس مجاز |
| Manager School Summary | `managers/school-summary` | `managers/school-summary` | `ManagerSchoolContextBuilder`؛ aggregate سازمان جاری |
| Manager Class Insight | `managers/classes/{classId}/insight` | `managers/class-insight` | `ManagerClassContextBuilder`؛ کلاس سازمان جاری |
| Manager Student Insight | `managers/classes/{classId}/students/{studentId}/insight` | `managers/student-insight` | `ManagerStudentContextBuilder`؛ کلاس و enrollment سازمان جاری |

Commandهای عمومی قبلی و نتیجه‌های عمومی حفظ شده‌اند: Chat، StudyPlan، HomeworkExplain و ActivitySuggestions body دارند؛ بقیه از route و نشست استفاده می‌کنند. `AiChatResult` همچنان قرارداد قبلی خود را دارد و metadata جدیدی به پاسخ عمومی آن تحمیل نشده است. سایر پاسخ‌ها metadata قراردادی موجود را دارند. پاسخ موفق همهٔ عملیات `200` است.

پیش از رزرو، input، نقش، سازمان، selectorها و روابط بررسی می‌شوند. پس از رزرو و بلافاصله پیش از HTTP، builder دوباره اجرا می‌شود؛ تغییر scope یا snapshot مجاز باعث توقف و آزادسازی رزرو می‌شود. `EfAiScopeStore` علاوه بر enrollment فعال، فعال‌بودن Class و OrganizationPerson و پایان‌نیافتن آن‌ها را برای انتخاب تکلیف/آزمون و کلاس‌های دانش‌آموز بررسی می‌کند. این بازبینی تا لحظهٔ dispatch است؛ ادعای تراکنش توزیع‌شده یا قفل SQL در طول HTTP نداریم.

## Privacy و مرز نقش‌ها

- Student فقط پرونده و گزارش‌های خود را دارد؛ شناسهٔ client مجوز نیست.
- Parent فقط فرزند انتخاب‌شده با GuardianRelation جاری را می‌بیند؛ تغییر/لغو رابطه دوباره بررسی می‌شود.
- Teacher به TeacherAssignment جاری و enrollment همان کلاس محدود است.
- Manager فقط سازمان جاری را دارد؛ نقش سازمانی دسترسی به Direct خصوصی یا Personal data ایجاد نمی‌کند.
- projectionهای نمره فقط نسخهٔ release‌شده را می‌خوانند. feedback خصوصی، پاسخ آزمون، teacher note و chat خصوصی وارد payload نمی‌شوند.
- OTP، access/refresh token، شمارهٔ کامل خصوصی، Contacts، moderation evidence، audit پلتفرم، اطلاعات پرداخت و secretها وارد request کسب‌وکار AI نمی‌شوند. کلید Service فقط برای امضای header احراز سرویس استفاده می‌شود.

## Service JWT، header و اعتبارسنجی

`IAiServiceCredentialProvider` و `HmacAiServiceCredentialProvider` بدون تغییر reuse شده‌اند. JWT با `HS256`، `typ=JWT` و `kid` معتبر فقط claimهای `iss/aud/sub/scope/iat/nbf/exp/jti` دارد. `iss/sub=makan-backend`، `aud=makan-ai` و `scope=makan.ai.invoke` هستند. زمان پیش‌فرض ۱۲۰ ثانیه و سقف قرارداد ۳۰۰ ثانیه است. UserId، Role، Organization، Source، Payer و Subscription در JWT قرار نمی‌گیرند.

Client موجود این headerها را یکجا می‌سازد. Handler ثبت‌شده در DI، redirect خودکار را دنبال نمی‌کند؛ پاسخ 3xx شکست امن است تا payload آموزشی به مقصد دیگری ارسال نشود. این رفتار با دو سرور loopback و خود HttpClient ثبت‌شده تست می‌شود:

```text
Authorization: Bearer <service credential; never log>
X-AI-Contract-Version: v1
Content-Type: application/json
X-Correlation-ID: exact body.correlationId
```

requestId و correlationId پاسخ باید دقیقاً با درخواست برابر باشند. schema سخت‌گیرانه، فیلدهای لازم، شناسه‌های مجاز و grounding موجود حفظ شده‌اند. `modelAlias` اکنون دقیقاً `education-fast` است؛ نام Provider یا مدل واقعی وابستگی Backend نیست. پاسخ ناقص، alias نامعتبر و شناسهٔ خارج از roster با `AI_RESPONSE_INVALID` متوقف می‌شود.

## سهمیه، پرداخت‌کننده و idempotency

`AiInvocationExecutor` هر ۱۲ مسیر source-less را به همان `CommercialSubscriptionService` و `UsageReservations`/`UsageLedger` متصل می‌کند. Preflight نهایی داخل transaction کوتاه و atomic متد Reserve انجام می‌شود؛ replay پیش از بررسی دوبارهٔ مانده شناسایی می‌شود تا retry آخرین واحد، به‌اشتباه درخواست تازه تلقی نشود. HTTP خارج از transaction است.

دو header عمومی اختیاری به OpenAPI اضافه شده‌اند؛ به AI forward نمی‌شوند:

| Header | معنا |
|---|---|
| `X-AI-Operation-Id` | GUID ثابت برای یک intent؛ برای retry همان مقدار را حفظ کنید. با `operationId` body در چهار Command موجود باید یکسان باشد. |
| `X-AI-Payer` | `Organization`، `Individual` یا `FreeTrial` مطابق مدل تجاری موجود |

بدون payer، رفتار موجود انتخاب Organization حفظ می‌شود؛ هیچ fallback خودکار به حساب فردی یا Trial نداریم. Individual فقط با اشتراک معتبر همان حساب و Trial فقط با انتخاب صریح و محدودیت موجود سه متن موفق روزانهٔ حساب طبق روز تهران کار می‌کنند. این ۱۲ عملیات متنی همان واحد موجود `AiTextRequests` را مصرف می‌کنند؛ قیمت یا entitlement جدیدی اختراع نشده است. Legacy Organization بدون قرارداد معتبر از این مسیر entitlement جدید دریافت نمی‌کند.

برای client قدیمی فاقد OperationId، درخواست تازه شناسهٔ تازه می‌گیرد؛ تضمین جلوگیری از اجرای دوباره فقط با ارسال ID ثابت ممکن است. fingerprint شامل نوع عملیات، actor، membership، scope و intent نرمال‌شده است؛ requestId، correlationId و گزارش متغیر سرور در intent نیستند. محتوای خصوصی پاسخ برای replay ذخیره نمی‌شود؛ فقط hash درخواست تجاری موجود استفاده می‌شود.

| وضعیت رزرو قبلی با همان ID | پاسخ retry | Provider جدید |
|---|---|---|
| Consumed | `409 AI_OPERATION_ALREADY_COMPLETED` | صفر |
| Reserved | `409 AI_OPERATION_IN_PROGRESS` | صفر |
| Unknown | `409 AI_OPERATION_OUTCOME_UNKNOWN` | صفر |
| Released | `409 AI_OPERATION_PREVIOUSLY_FAILED` | صفر |
| همان ID با intent/payer/context متفاوت | conflict تجاری موجود | صفر |

پاسخ HTTP معتبر و defensively validated → `Consumed`. شکست قطعی قبل از ارسال یا رد صریح auth/validation/rate-limit/503 → `Released`. timeout، قطع اتصال، پاسخ خراب یا نتیجهٔ مبهم پس از dispatch → `Unknown`؛ مصرف موفق ثبت نمی‌شود ولی hold موجود برای جلوگیری از duplicate نگه داشته می‌شود. Unknown موفق محسوب نمی‌شود و بدون تعیین نتیجه نباید با ID تازه تکرار شود. بازیابی خودکار reservation و cache خصوصی پاسخ در این مرحله ساخته نشده‌اند. finalize با CancellationToken مستقل و محدود به ده ثانیه انجام می‌شود تا قطع client حسابداری را لغو نکند؛ خطای finalize پنهان نمی‌شود.

## خطا و Observability

Client عمداً raw error body را نمی‌خواند؛ mapping بر مبنای status است. بنابراین کدهای هم‌معنای AI در یک status به کد پایدار عمومی Backend تبدیل می‌شوند.

| HTTP سرویس AI | معنی | کد Backend | HTTP عمومی Backend |
|---|---|---|---|
| 401 | Service auth | `AI_SERVICE_UNAUTHORIZED` | 503 |
| 403 | Service scope | `AI_SERVICE_FORBIDDEN` | 503 |
| 400/422 | invalid request/context | `AI_INVALID_REQUEST` | 400 |
| 429 | ظرفیت/rate limit | `AI_RATE_LIMITED` | 429 |
| 502 | پاسخ نامعتبر/internal AI | `AI_RESPONSE_INVALID` | 502 |
| 503 یا سایر شکست‌ها | unavailable | `AI_SERVICE_UNAVAILABLE` | 503 |
| 504 یا timeout محلی | provider/transport timeout | `AI_SERVICE_TIMEOUT` | 504 |

مجوز نامعتبر کسب‌وکار پیش از network، `AI_CONTEXT_NOT_ALLOWED` و HTTP 403 می‌دهد. غیرفعال‌بودن runtime، `AI_FEATURE_DISABLED` و 503 است. سهمیه/قرارداد نامعتبر از error codeهای Commerce موجود استفاده می‌کند. ProblemDetails شامل correlation امن است. timeout پیش‌فرض ۳۵ ثانیه، circuit breaker موجود و نبود retry خودکار حفظ شده‌اند.

log امن شامل operation، status در صورت وجود پاسخ HTTP، elapsed، requestId/correlationId در scope، error code و modelAlias پاسخ معتبر است. failure با Error ثبت می‌شود؛ JWT/header، secret، prompt/context، body پاسخ و دادهٔ خصوصی وارد log نمی‌شوند. telemetry موجود reuse می‌شود؛ pipeline logging عمومی تغییر نکرده است.

## Educational Sources

[بنیاد انتخاب منبع](AI_SOURCE_SELECTION_AND_AUTHORIZATION_FA.md) محفوظ است. انتخاب معتبر همچنان با `AI_SOURCE_RETRIEVAL_NOT_AVAILABLE` پیش از reservation و Provider متوقف می‌شود. selectedSourceIds، manifest، chunk و document به Contract v1 اضافه نشده‌اند. نبود انتخاب نیز همهٔ منابع را انتخاب نمی‌کند. کد SourceSelection و migration آن تغییر نکرده‌اند.

## پذیرش و تست

`ControlledAiV1Server` یک Kestrel loopback با کلید تصادفی موقت، بررسی واقعی HS256 و schema محدود است؛ هیچ adapter یا تماس Provider ندارد. تست‌های transport از `AiServiceClient` و credential provider واقعی استفاده می‌کنند. JWT معتبر همراه `{}` به `422 AI_INVALID_REQUEST` می‌رسد؛ فقدان/خرابی/انقضا/امضای نادرست/kid و issuer/audience/subject نادرست 401، scope ناقص 403 و header ناسازگار 422 می‌شوند.

تست‌ها علاوه بر ۱۲ DTO، alias/schema/correlation/grounding خراب، 429/502/503/504 و timeout کنترل‌شده را می‌پوشانند. تست SQL، مصرف آخرین واحد، retry و رقابت هم‌زمان، Trial، عدم fallback، runtime خاموش، role/header نامعتبر، لغو membership پس از Reserve، و Class/OrganizationPerson پایان‌یافته را بررسی می‌کند. تست‌های موجود privacy، نقش‌ها، SourceSelection و logging نیز در رگرسیون اجرا می‌شوند.

پذیرش مستقل محلی با Kestrel واقعی Backend + LocalDB موقت + AI کنترل‌شده انجام شد: **۱۲/۱۲ عملیات و Focused Three موفق**؛ ۱۷۶ درخواست HTTP و ۷۴ check؛ ۱۲ reservation مصرف‌شده، ۲۴ ledger row، صفر OTP واقعی، صفر SMS و صفر Provider واقعی. schema موجود با ۵۸ migration فقط در SQL ایزوله اعمال شد و دیتابیس پذیرش حذف شد. این نتیجه، PASS سرور واقعی نیست.

Restore و Release Build موفق و compiler warning/error صفر بودند. رگرسیون نهایی پیش از commit: **۱۰۱۳ Unit + ۱۶۵۱ Integration + ۱۴ Architecture = ۲۶۷۸ تست**، failed/skipped صفر؛ ۴۷ تست بیشتر از baseline. بررسی `HasPendingModelChanges()` موجود نیز در Integration پاس شد. پس از commit، Build و هر سه سوییت دوباره اجرا و نتیجهٔ واقعی آن‌ها همراه Git در گزارش پایان مرحله ثبت می‌شود.

## وضعیت انتشار

`AiService.Enabled=false` در فایل‌های خصوصی Test و Production حفظ شده است؛ path/type کلیدهای AI یکسان است. فعال‌بودن transport فقط به process تست کنترل‌شده تزریق شد. secretها و چهار فایل خصوصی appsettings دست‌نخورده‌اند. سه فایل AI از قبل dirty نیز خارج از تغییر و commit باقی می‌مانند.

`MIGRATION_REQUIRED=false`؛ هیچ مدل EF یا migration تغییر نکرده است. Publish، Deploy، migration سرور و تماس AI واقعی در این مرحله انجام نمی‌شوند. پذیرش عملیاتی WireGuard، credential واقعی، ظرفیت Provider، latency، Focused Three و Full12 روی سرور همچنان متعلق به مرحلهٔ بعد است.
