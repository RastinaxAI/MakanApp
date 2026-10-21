# ممیزی قرارداد زنده STEP10G

تاریخ: `2026-10-21`؛ مبنای Backend: `1ecde4043671da5bd20a8d16aede38fcc6917905`.
این سند پیش از تغییر کد همین retest ساخته شد. نتیجهٔ runtime و Validation در
[گزارش اجرا](STEP10G_REAL_AI_E2E_REPORT.md) ثبت می‌شود؛ mapping به معنی موفقیت مدل نیست.

## منبع و روش

- منبع واقعی: `https://195.177.255.98/openapi.json`؛ دریافت با بررسی عادی TLS و Parse واقعی JSON.
- OpenAPI: `3.1.0`؛ عنوان: `MakanApp AI Core Service`؛ نسخهٔ سرویس در سند: `1.0.0`.
- تعداد business operation: دقیقاً ۱۲ `POST`؛ build/revision واقعی AI از شمارهٔ OpenAPI استنتاج نمی‌شود.
- کد مقایسه‌شده: `AiController`، `IAiServiceClient`، `AiServiceContracts`،
  `AiRoleContextContracts`، `AiServiceClient`، `AiResponseGrounding`، builderها و تست‌های AI.
- Generic Chat چهار نقش دارد؛ پوشش ۱۲ operation ممکن است ۱۵ درخواست مثبت Backend داشته باشد.
- هیچ کدی در AI، Frontend، MeliPayamak یا کانفیگ خصوصی Production تغییر نمی‌کند.

## header، احراز هویت و status مشترک

برای تمام ردیف‌های جدول:

- `Authorization: Bearer <service-jwt>`؛ security برابر `BackendServiceJWT`.
- `X-AI-Contract-Version` اجباری، `const: v1`.
- `X-Correlation-ID` اختیاری در OpenAPI؛ Backend هنگام وجود شناسه، header/body را یکسان می‌فرستد.
- request و تمام responseهای مستند business از `application/json` استفاده می‌کنند.
- statusها: `200, 401, 403, 422, 429, 502, 503, 504`؛ schema همهٔ خطاها `ErrorResponse` است.
- `400/404/409/500` در این OpenAPI business مستند نیستند؛ پشتیبانی دفاعی adapter از آن‌ها
  به معنی وجود route/status جدید در AI نیست.

## ماتریس ۱۲ operation

پیشوند دقیق تمام routeها: `/api/v1/ai`. روش تمام ردیف‌ها `POST` است.
header/security/status مشترک بالا برای هر ردیف برقرار است. `R1/R2` در ادامه دقیق تعریف شده‌اند.

| Operation | Route پس از پیشوند | Request schema | Response schema | پیاده‌سازی Backend | Status | Mismatch / اقدام |
|---|---|---|---|---|---|---|
| Generic Chat | `/chat` | `ChatRequest` | `ChatResponse` | `ChatAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ حفظ validation و ثبت runtime |
| Student Analyze | `/students/analyze` | `StudentAnalysisRequest` | `StudentAnalysisResponse` | `AnalyzeStudentAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ همان اقدام |
| Study Plan | `/study-plan` | `StudyPlanRequest` | `StudyPlanResponse` | `CreateStudyPlanAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ همان اقدام |
| Homework Explain | `/homeworks/explain` | `HomeworkExplanationRequest` | `HomeworkExplanationResponse` | `ExplainHomeworkAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ همان اقدام |
| Exam Prepare | `/exams/prepare` | `ExamPreparationRequest` | `ExamPreparationResponse` | `PrepareExamAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ همان اقدام |
| Parent Child Summary | `/parents/child-summary` | `ChildSummaryRequest` | `ChildSummaryResponse` | `SummarizeChildAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ retest مستقل |
| Teacher Class Summary | `/teachers/class-summary` | `ClassSummaryRequest` | `ClassSummaryResponse` | `SummarizeTeacherClassAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ همان اقدام |
| Teacher Student Insight | `/teachers/student-insight` | `StudentInsightRequest` | `StudentInsightResponse` | `GetTeacherStudentInsightAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ همان اقدام |
| Teacher Activity Suggestions | `/teachers/activity-suggestions` | `ActivitySuggestionsRequest` | `ActivitySuggestionsResponse` | `SuggestTeacherActivitiesAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ retest مستقل |
| Manager School Summary | `/managers/school-summary` | `SchoolSummaryRequest` | `SchoolSummaryResponse` | `SummarizeManagerSchoolAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ همان اقدام |
| Manager Class Insight | `/managers/class-insight` | `ClassInsightRequest` | `ClassInsightResponse` | `GetManagerClassInsightAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ retest مستقل |
| Manager Student Insight | `/managers/student-insight` | `ManagerStudentInsightRequest` | `ManagerStudentInsightResponse` | `GetManagerStudentInsightAsync` | AI_CONTRACT_MISMATCH | R1/R2؛ همان اقدام |

### operationId واقعی

به ترتیب جدول:

```text
ai_chat_api_v1_ai_chat_post
analyze_api_v1_ai_students_analyze_post
study_plan_api_v1_ai_study_plan_post
explain_homework_api_v1_ai_homeworks_explain_post
prepare_exam_api_v1_ai_exams_prepare_post
child_summary_api_v1_ai_parents_child_summary_post
class_summary_api_v1_ai_teachers_class_summary_post
student_insight_api_v1_ai_teachers_student_insight_post
activity_suggestions_api_v1_ai_teachers_activity_suggestions_post
school_summary_api_v1_ai_managers_school_summary_post
class_insight_api_v1_ai_managers_class_insight_post
student_insight_api_v1_ai_managers_student_insight_post
```

## خلاصهٔ DTO و nested schema

نوع، JSON name، nullable/required، enum، طول، range و nested referenceهای هر schema زنده بررسی شدند.
نام فیلد و route ناشناخته‌ای برای پرکردن اختلاف ساخته نشد.

| خانواده | قرارداد واقعی |
|---|---|
| envelope | `requestId` اجباری، string با طول ۱..۱۲۸؛ `correlationId` اختیاری nullable با همان طول؛ `conversationId` فقط Chat، اختیاری nullable |
| Actor | `userId` اجباری، طول ۱..۱۲۸؛ `role` اجباری و enum دقیق `Student/Parent/Teacher/Manager` |
| Message / Answer | `text` اجباری non-empty؛ `language` پیش‌فرض `fa`، حداکثر ۶ و الگوی `^[a-z]{2,3}(-[A-Z]{2})?$` |
| Scope | Chat همهٔ selectorها اختیاری nullable؛ Student/Parent: organization/school/student اجباری، class اختیاری nullable |
| TeacherClass / ManagerClass | organization/school/class اجباری non-null |
| TeacherStudent / ManagerStudent | organization/school/class/student اجباری non-null |
| ManagerSchool | organization/school اجباری non-null |
| دادهٔ آموزشی | subjects/grades/homeworks/exams/activity آرایهٔ dictionaryهای `JsonValue`؛ class/student/schoolData شناسهٔ مخصوص خود را دارند؛ roster فقط در classData و classes فقط در schoolData |
| Aggregate | averageScore اختیاری nullable؛ completionRate/attendanceRate در ۰..۱؛ trend برابر Improving/Stable/Declining/Unknown یا null؛ countهای Manager نامنفی nullable |
| preferences / constraints | availableMinutes مثبت یا null؛ notes اختیاری non-empty یا null؛ materials/goals آرایهٔ stringهای non-empty |
| Insight / Risk | type اجباری non-empty؛ severity پیش‌فرض Info؛ entityType/entityId/title/description nullable؛ confidence در ۰..۱ یا null |
| Recommendation / SourceReference | title اجباری non-empty، description nullable؛ source دارای type/id اجباری non-empty و id حداکثر ۱۲۸ |
| PlanItem / TeachingActivity | title/reason اجباری؛ priority در PlanItem اجباری؛ objective در Activity اجباری؛ estimatedMinutes مثبت nullable؛ steps آرایهٔ string |
| Progress / AttentionArea / UpcomingItem | trend و description اجباری؛ area دارای title/description اجباری؛ upcoming دارای type=Homework/Exam، id/title اجباری و date nullable |
| ResponseMetadata | modelAlias اجباری non-empty و processingTimeMs اجباری integer نامنفی؛ Backend جاری هر دو را JsonRequired می‌داند |

تمام object DTOهای صریح `additionalProperties: false` دارند؛ dictionaryهای `JsonValue` عمداً پویا هستند.
Backend جاری camelCase، case-sensitive، عدد JSON strict، enum متنی strict، envelope matching و
identifier grounding دارد. scope/roster به‌تنهایی مجوز ساخت source جدید برای خروجی مدل نیست.

### R1 — optional بودن collectionهای پاسخ در OpenAPI

مسیرهای `components.schemas.<Response>.properties.recommendations/sources` در همهٔ ۱۲ پاسخ
وجود دارند ولی در `required` نیستند. سایر موارد:

| Response | collectionهای دیگری که در required نیستند |
|---|---|
| ChatResponse | insights, risks |
| StudentAnalysisResponse | strengths, weaknesses, risks |
| StudyPlanResponse | priorities, planItems |
| HomeworkExplanationResponse | steps, hints |
| ExamPreparationResponse | priorityTopics, preparationSteps, risks |
| ChildSummaryResponse | attentionAreas, upcomingItems, risks |
| ClassSummaryResponse / StudentInsightResponse / SchoolSummaryResponse / ClassInsightResponse / ManagerStudentInsightResponse | strengths, attentionAreas, risks |
| ActivitySuggestionsResponse | activities؛ همچنین TeachingActivity.steps اختیاری است |

قرارداد قبلی و Validator Backend حضور collectionها را لازم می‌دانند. omitted با `[]` یکسان نیست؛
optional بودن نیز اجازهٔ `null` نیست. وجود این اختلاف به‌تنهایی ثابت نمی‌کند runtime فیلدها را حذف می‌کند
یا علت خطاهای تاریخی همین بوده است. validation یا grounding برای عبور آزمایش ضعیف نمی‌شوند.
طبقه‌بندی `AI_CONTRACT_MISMATCH` مربوط به تعارض سند زنده با قرارداد Freezeشده است، نه اثبات bug مدل.

### R2 — scope در OpenAPI مستند نیست

scheme فقط HTTP Bearer با شرح HS256 و Backend identity است؛ رشتهٔ `makan.ai.invoke` در سند زنده
وجود ندارد. این مقدار از `02_AI_SERVICE_AUTH_CONTRACT.md` و generator موجود Backend می‌آید.
ErrorCodeهای 401/403 مستندند؛ scope یا claim جدید اختراع نمی‌شود. پذیرش واقعی کلید/scope فقط با
آزمون runtime ثابت می‌شود، نه با وجود تنظیمات محلی.

## ErrorResponse و واژگان خطا

`ErrorResponse.error` اجباری است؛ `ErrorDetail.code/message` اجباری و requestId اختیاری nullable است.
واژگان واقعی:

```text
AI_INVALID_REQUEST AI_INVALID_CONTEXT AI_UNSUPPORTED_ROLE AI_MODEL_UNAVAILABLE
AI_PROVIDER_TIMEOUT AI_RATE_LIMITED AI_CONTEXT_TOO_LARGE AI_RESPONSE_INVALID
AI_SAFETY_BLOCKED AI_INTERNAL_ERROR AI_SERVICE_UNAUTHORIZED AI_SERVICE_FORBIDDEN
```

Backend خطای خام AI/provider را forward نمی‌کند. mapping جاری: 401/403 به public 503؛
422 به 400؛ 429 به 429؛ 502/503 به 503؛ 504/timeout به 504؛ پاسخ نامعتبر به 502.
این status/codeهای عمومی Backend، واژگان تازهٔ Contract AI نیستند. Retry-After داخلی خوانده می‌شود
ولی public header تازه‌ای وعده داده نمی‌شود؛ POST retry خودکار ندارد.

## Health / Readiness

- `GET /api/v1/health`: operationId برابر `liveness_check_api_v1_health_get`.
- `GET /api/v1/ready`: operationId برابر `readiness_check_api_v1_ready_get`.
- هر دو در OpenAPI security اختصاصی ندارند، پاسخ مستند 200 است و schema آن‌ها `{}` است.
- معنای دقیق dependencyها یا شکل response از `{}` قابل استخراج نیست؛ DTO ابداع نمی‌شود.
- مشاهدهٔ عمومی این retest: هر دو 401/HTML با Basic challenge در Nginx؛ status آماده‌بودن برنامه معلوم نیست.
- این مانع جدا از Service JWT business است؛ برای دورزدن Basic توکن سرویس به health ارسال نمی‌شود.

## اقدام Backend در همین retest

1. ابزار C# موجود، با مجوز مالک، در Worktree تمیز ثبت و برای target HTTPS صریح و بدون redirect آماده می‌شود.
2. negative isolation و مشاهدهٔ امن حفظ می‌شوند؛ حالت تاریخی export کامل payload وارد ابزار ثبت‌شده نمی‌شود.
3. gap واقعی مشاهده‌پذیری AI با Activity/metadata امن و تست نشت بررسی می‌شود؛ سامانهٔ telemetry موازی ساخته نمی‌شود.
4. fallback آدرس داخل کد حذف و آدرس محیط از options/config خوانده می‌شود؛ فایل Production تغییر نمی‌کند.
5. گزارش اجرا واقعیت Auth/Happy Path و BLOCKEDها را جدا ثبت می‌کند؛ هیچ موفقیت تاریخی به اجرای جدید منتقل نمی‌شود.
