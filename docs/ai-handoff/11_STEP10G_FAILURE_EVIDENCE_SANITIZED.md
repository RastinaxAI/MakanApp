# STEP 10G Failure Evidence — Sanitized

> شاهد تاریخیِ اجرای همان تاریخ است، نه وضعیت جاری. برای تفکیک implementation تکمیل‌شده،
> Service Auth تأییدشدهٔ قبلی و پذیرش واقعی Deferred به علت Provider Timeout،
> [گزارش Consolidation نهایی](../release/BACKEND_FINAL_CONSOLIDATION_FA.md) را بخوانید. هیچ PASS واقعی تازه‌ای از این بازبینی نتیجه نمی‌شود.

> این capture تاریخی محفوظ است. [اجرای جدید روی b346df8 در ۲۰۲۶-۱۰-۱۶](12_STEP10G_FINAL_REAL_E2E_REPORT.md) اکنون با AI_MODEL_UNAVAILABLE / configuration متوقف است؛ این نتیجه نه تکرار همان خطای تاریخی و نه اثبات رفع آن است. در اجرای جدید هیچ prompt، context کامل یا پاسخ خام provider ذخیره نشده است.

این سند فقط شواهد تشخیصی Backend را تحویل می‌دهد؛ اصلاح محصول یا تأیید پذیرش STEP 10G نیست. هر سه شکست از capture واقعی و sanitize‌شده‌ای که پیش از این درخواست در مخزن موجود بود استخراج شده‌اند. در این بازبینی هیچ درخواست جدیدی به AI ارسال نشد.

## 1. Evidence scope

- Backend commit: `8ae6882f82c62539d596827b829e056368da4427`.
- Branch: `main`؛ upstream: `origin/main`.
- مقدار local tracking ref مربوط به `origin/main` در بازبینی: `8ae6882f82c62539d596827b829e056368da4427`؛ برابر HEAD. در این کار fetch/push انجام نشد؛ این مقدار ادعای بررسی مجدد سرور remote نیست.
- Worktree: در شروع dirty؛ تغییرات قبلی adapter، تست‌ها و ابزار E2E موجود بودند. این درخواست فقط همین سند را تغییر می‌دهد؛ HEAD به‌تنهایی نماینده تمام کد uncommitted زمان capture نیست.
- Environment: `Local / Integration`؛ محیط میزبان fixture موجود: `Testing`.
- AI base URL استفاده‌شده در capture: `http://127.0.0.1:8000`؛ فاقد credential.
- Collection date/time: تاریخ capture واقعی `2026-10-14`؛ زمان شروع هر سه invocation در بخش اختصاصی آن عیناً حفظ شده است. پایان دقیق کل collection در artifact باقی نمانده: **Not available from retained Backend evidence.**
- زمان بازبینی سند، نه زمان اجرای مجدد: `2026-10-14T20:24:28Z`.
- Evidence source: نسخه موجودِ همین سند در شروع درخواست، شامل JSONهای دریافت‌شده از `HttpRequestMessage.Content` و `HttpResponseMessage.Content` در ابزار قبلی؛ SHA-256 نسخه ورودی: `AC269FA357AFCE44635B80F4D5B2CFDADF9CF265FDD40EB44C58722021992489`.

### منشأ شواهد و تصمیم درباره اجرای مجدد

1. گزارش اصلی [10_BACKEND_REAL_E2E_GATE.md](10_BACKEND_REAL_E2E_GATE.md) شکست سه operation را ثبت کرده، اما JSON کامل و requestId/correlationId آن invocationهای اولیه را نگه نداشته است؛ کد upstream مربوط به Parent نیز در اجرای اولیه ثبت نشده بود. برای آن داده‌های اولیه: **Not available from retained Backend evidence.**
2. capture تشخیصی بعدی، پیش از درخواست حاضر، هر سه operation را با همان مسیر مصوب `LiveAiScenario`، fixture و adapter واقعی اجرا کرده و شواهد کامل این سند را نگه داشته است. داده‌ها، شناسه‌ها و زمان‌های آن fixture تازه‌اند؛ این capture به‌عنوان همان invocation اولیه معرفی نمی‌شود.
3. جست‌وجوی Backend، شامل فایل‌های ignored و artifactهای موجود، فقط یک فایل با نام مقصد یافت. شش شناسه واقعی همین سه case فقط در نسخه موجود این سند یافت شدند. در فایل‌های `.log`، `.trx`، `.out`، `.jsonl`، `.har` و `.txt`، تطبیقی برای `STEP10G`، `AI_RESPONSE_INVALID` یا سه route هدف پیدا نشد.
4. harness، observer، sanitizer، تست‌های integration/unit، fixture و mapping فعلی بررسی شدند. برای زیباتر کردن evidence، اجرای واقعی تکرار نشد؛ تنها سه failure موجود مستند شده‌اند. هیچ شاهد سناریوی دیگری به این سه invocation نسبت داده نشده است.
5. پاسخ خامِ قبل از sanitization عمداً persist نشده بود؛ مقایسه مجدد با آن اکنون ممکن نیست. هر ۱۲ بلوک JSON sanitize‌شده نسخه ورودی بدون تغییر نگه داشته شده‌اند.

### Sanitization methodology

- capture قبلی، body واقعی HTTP را پیش از mapping در `AiServiceClient` در حافظه مشاهده کرده بود؛ درخواست مثال یا DTO ایده‌آل ساخته نشده است.
- نام و ترتیب propertyها، nesting، field presence/absence، آرایه و طول آن، nullability و نوع JSON حفظ شده‌اند. فقط مقادیر هویتی و متن آزاد با مقدار خنثی جایگزین شده‌اند.
- GUIDها نگاشت ثابت دارند؛ ارجاع یکسان در سه case یک GUID ساختگی یکسان دارد. عددها، booleanها، nullها، enumهای غیرحساس، سقف نمره و timestampهای موجود حفظ شده‌اند.
- timestampهای بدون suffix در body اصلی همچنان بدون suffix هستند؛ `Z` به آن‌ها اضافه نشده است. عدد به string یا string به عدد تبدیل نشده است.
- requestId و correlationId واقعی، تصادفی و opaque هستند و اطلاعات هویتی/secret ندارند؛ برای lookup تیم AI عیناً حفظ شده‌اند.
- capture قبلی تنها خروجی sanitize‌شده را منتشر کرده بود؛ header احراز هویت در آن ثبت نشده است. در این بازبینی، formatter، schema یا sanitizer تغییر نکرده است.

## 2. Security / sanitization statement

این فایل فاقد Service JWT و payload/signature آن، signing key، API key، password، connection string، database credential، secret، cookie، UserSession token، شماره تلفن و email واقعی، نام واقعی دانش‌آموز و سایر PII است. هیچ مکالمه خصوصی خام Student/Parent/Teacher/Manager، raw prompt یا context آزاد حساس در آن نیست.

متن‌های `Sample ...` و `Synthetic educational text.` و GUIDهای `00000000-...` جایگزین ساختگی‌اند، نه داده واقعی کاربر. پیام `AI structured response is invalid.` همان متن خطای عمومی و غیرحساس دریافت‌شده از AI است. نام خطا و traceهای مجاز برای حفظ ارزش تشخیصی حذف نشده‌اند.

## 3. Parent Child Summary

Endpoint: `POST /api/v1/ai/parents/child-summary`؛ این route داخلی Backend → AI است، نه لزوماً route عمومی.

### Invocation metadata

- Operation: `parent_child_summary`.
- requestId: `aa8520be38e745fdbe8195d424674754`.
- correlationId: `step10g-evidence-8c923d675594444f99b6674eace0cfd1`.
- Timestamp، شروع invocation به UTC: `2026-10-14T13:07:55.1739991+00:00`.
- AI environment: `Local / Integration`.
- AI base URL: `http://127.0.0.1:8000`.
- Duration: `8261.19 ms`؛ اندازه‌گیری واقعی observer با Stopwatch، شامل HTTP و خواندن body و sanitization در observer؛ این مقدار `processingTimeMs` مدل نیست.
- Backend public round-trip: `8672.95 ms`.
- Backend public endpoint، با class ID جایگزین‌شده در صورت وجود: `POST /api/v1/ai/parents/child-summary`.
- Source: capture واقعیِ قبلاً نگه‌داری‌شده؛ بازبینی حاضر این invocation را تکرار نکرده است.

### Exact sanitized Backend -> AI request

```json
{
  "requestId": "aa8520be38e745fdbe8195d424674754",
  "correlationId": "step10g-evidence-8c923d675594444f99b6674eace0cfd1",
  "actor": {
    "userId": "00000000-0000-0000-0000-000000000101",
    "role": "Parent"
  },
  "scope": {
    "organizationId": "00000000-0000-0000-0000-000000000102",
    "schoolId": "00000000-0000-0000-0000-000000000102",
    "classId": null,
    "studentId": "00000000-0000-0000-0000-000000000103"
  },
  "childData": {
    "subjects": [
      {
        "classId": "00000000-0000-0000-0000-000000000104",
        "title": "Sample Title"
      }
    ],
    "grades": [],
    "homeworks": [
      {
        "assignmentId": "00000000-0000-0000-0000-000000000105",
        "classId": "00000000-0000-0000-0000-000000000104",
        "title": "Sample Assignment",
        "dueAtUtc": "2026-10-17T13:07:54.5248582",
        "maxScore": 10
      }
    ],
    "exams": [
      {
        "examId": "00000000-0000-0000-0000-000000000106",
        "classId": "00000000-0000-0000-0000-000000000104",
        "title": "Sample Exam",
        "availableFromUtc": "2026-10-15T13:07:54.5248582",
        "availableUntilUtc": "2026-10-16T13:07:54.5248582",
        "maxScore": 30
      }
    ],
    "activity": []
  },
  "language": "fa"
}
```

### Exact sanitized AI -> Backend response

- HTTP status: `502`.
- Content-Type: `application/json`.
- سایر safe response headerها: **Not available from retained Backend evidence.**
- `error.code`: `AI_RESPONSE_INVALID`.
- `error.message`: `AI structured response is invalid.`.
- `error.requestId` دقیقاً برابر requestId ارسال‌شده است.
- ErrorResponse فیلد correlationId ندارد؛ چنین فیلدی به آن اضافه نشده است.

این body واقعی upstream است که پیش از mapping Backend مشاهده و sanitize شده بود، نه پاسخ نهایی Backend:

```json
{
  "error": {
    "code": "AI_RESPONSE_INVALID",
    "message": "AI structured response is invalid.",
    "requestId": "aa8520be38e745fdbe8195d424674754"
  }
}
```

### Backend error mapping

- AI HTTP: `502`.
- AI error.code مشاهده‌شده در boundary: `AI_RESPONSE_INVALID`.
- Backend mapped public code: `AI_SERVICE_UNAVAILABLE`.
- Frontend/API HTTP: `503`.
- mapping فعلی بر اساس HTTP status است؛ production body خطای AI را برای تعیین code عمومی parse نمی‌کند.
- `AiServiceClient.ThrowMappedFailure` پیش از deserialization و strict validation پاسخ موفق اجرا می‌شود. مشاهده code upstream در ابزار تشخیصی، تغییر رفتار adapter نیست.

پاسخ عمومی Backend با همان ساختار و مقادیر متنی/trace جایگزین‌شده:

```json
{
  "type": "Synthetic educational text.",
  "title": "Sample Title",
  "status": 503,
  "detail": "Synthetic educational text.",
  "code": "AI_SERVICE_UNAVAILABLE",
  "traceId": "Synthetic educational text."
}
```

### Safe Backend diagnostic evidence

این رویداد ثبت‌شده از observer و نتیجه HTTP Backend است؛ لاگ داخلی AI یا provider نیست:

```json
{
  "operation": "parent_child_summary",
  "aiHttpStatus": 502,
  "aiErrorCode": "AI_RESPONSE_INVALID",
  "backendHttpStatus": 503,
  "backendErrorCode": "AI_SERVICE_UNAVAILABLE",
  "requestId": "aa8520be38e745fdbe8195d424674754",
  "correlationId": "step10g-evidence-8c923d675594444f99b6674eace0cfd1",
  "aiDurationMs": 8261.19,
  "backendDurationMs": 8672.95,
  "validationCategory": "UpstreamErrorResponse_StatusMapping_SuccessDtoValidationNotReached"
}
```

- Exception type مشاهده‌شده در logger میزبان: `MakanApp.Application.Ai.AiServiceException`.
- Inner exception: `null`.
- Backend JSON path / line / byte position: `null` / `null` / `null`؛ این‌ها نبود خطای deserialize موفق در Backend را نشان می‌دهند، نه نبود خطای داخلی AI.
- request body و correlation header در capture قبلی با guard موجود برابر بررسی شدند؛ credential یا header محرمانه در سند نیست.

### Provider / model / runtime metadata

- در همین ErrorResponse هیچ provider/model alias، model name، AI service version، `processingTimeMs`، repair metadata یا runtime/version header نگه‌داری نشده است: **Not observable from Backend response/logs.**
- مشاهده readiness در بخش 6 جداست و به‌عنوان metadata همین response معرفی نمی‌شود.
- contract version ارسالی در capture طبق guard موجود: `v1`.

### AI-internal diagnosis visibility

Observed by Backend:

- AI یک ErrorResponse معتبر JSON با HTTP 502 و code/message/requestId بالا برگردانده است.
- Backend آن را به HTTP 503 / `AI_SERVICE_UNAVAILABLE` نگاشت کرده است؛ validation پاسخ موفق Backend اجرا نشده است.

Not observable by Backend:

- internal validation layer؛ **Not observable from Backend boundary.**
- internal provider raw output؛ **Not observable from Backend boundary.**
- exact Pydantic/JSON-schema failing path؛ **Not observable from Backend boundary.**
- expected type/value/schema و actual provider-produced type/value؛ **Not observable from Backend boundary.**
- bounded repair attempted/not attempted؛ **Not observable from Backend boundary.**
- repair result و علت نهایی داخلی `AI_RESPONSE_INVALID`؛ **Not observable from Backend boundary.**

Backend wire evidence cannot reveal internal AI validation path.

AI runtime sanitized validation log is required.

### AI runtime logs

AI runtime logs were not available from the Backend environment.

لاگ داخلی متناظر با همین requestId/correlationId در شواهد نگه‌داری‌شده Backend موجود نیست؛ مسیر schema یا رفتار repair حدس زده نشده است.

## 4. Teacher Activity Suggestions

Endpoint: `POST /api/v1/ai/teachers/activity-suggestions`؛ این route داخلی Backend → AI است، نه لزوماً route عمومی.

### Invocation metadata

- Operation: `teacher_activity_suggestions`.
- requestId: `819e4d0cd37f440c8ce85f7d2af40e43`.
- correlationId: `step10g-evidence-ce937cfe236b46b396df260093581006`.
- Timestamp، شروع invocation به UTC: `2026-10-14T13:08:03.7222346+00:00`.
- AI environment: `Local / Integration`.
- AI base URL: `http://127.0.0.1:8000`.
- Duration: `5040.92 ms`؛ اندازه‌گیری واقعی observer با Stopwatch، شامل HTTP و خواندن body و sanitization در observer؛ این مقدار `processingTimeMs` مدل نیست.
- Backend public round-trip: `5294.81 ms`.
- Backend public endpoint، با class ID جایگزین‌شده در صورت وجود: `POST /api/v1/ai/teachers/classes/00000000-0000-0000-0000-000000000104/activity-suggestions`.
- Source: capture واقعیِ قبلاً نگه‌داری‌شده؛ بازبینی حاضر این invocation را تکرار نکرده است.

### Exact sanitized Backend -> AI request

```json
{
  "requestId": "819e4d0cd37f440c8ce85f7d2af40e43",
  "correlationId": "step10g-evidence-ce937cfe236b46b396df260093581006",
  "actor": {
    "userId": "00000000-0000-0000-0000-000000000107",
    "role": "Teacher"
  },
  "scope": {
    "organizationId": "00000000-0000-0000-0000-000000000102",
    "schoolId": "00000000-0000-0000-0000-000000000102",
    "classId": "00000000-0000-0000-0000-000000000104"
  },
  "classData": {
    "classId": "00000000-0000-0000-0000-000000000104",
    "subjects": [
      {
        "classId": "00000000-0000-0000-0000-000000000104"
      }
    ],
    "grades": [],
    "homeworks": [
      {
        "assignmentId": "00000000-0000-0000-0000-000000000105",
        "title": "Sample Assignment",
        "dueAtUtc": "2026-10-17T13:07:54.5248582",
        "maxScore": 10
      }
    ],
    "exams": [
      {
        "examId": "00000000-0000-0000-0000-000000000106",
        "title": "Sample Exam",
        "availableUntilUtc": "2026-10-16T13:07:54.5248582",
        "maxScore": 30
      }
    ],
    "activity": [],
    "students": [
      {
        "studentId": "00000000-0000-0000-0000-000000000103"
      }
    ],
    "aggregates": {
      "averageScore": null,
      "scoreScale": null,
      "completionRate": 0,
      "attendanceRate": null,
      "trend": "Unknown"
    }
  },
  "objective": "Synthetic educational text.",
  "constraints": {
    "availableMinutes": 30,
    "materials": [
      "Synthetic educational text."
    ],
    "notes": null
  },
  "language": "fa"
}
```

### Exact sanitized AI -> Backend response

- HTTP status: `502`.
- Content-Type: `application/json`.
- سایر safe response headerها: **Not available from retained Backend evidence.**
- `error.code`: `AI_RESPONSE_INVALID`.
- `error.message`: `AI structured response is invalid.`.
- `error.requestId` دقیقاً برابر requestId ارسال‌شده است.
- ErrorResponse فیلد correlationId ندارد؛ چنین فیلدی به آن اضافه نشده است.

این body واقعی upstream است که پیش از mapping Backend مشاهده و sanitize شده بود، نه پاسخ نهایی Backend:

```json
{
  "error": {
    "code": "AI_RESPONSE_INVALID",
    "message": "AI structured response is invalid.",
    "requestId": "819e4d0cd37f440c8ce85f7d2af40e43"
  }
}
```

### Backend error mapping

- AI HTTP: `502`.
- AI error.code مشاهده‌شده در boundary: `AI_RESPONSE_INVALID`.
- Backend mapped public code: `AI_SERVICE_UNAVAILABLE`.
- Frontend/API HTTP: `503`.
- mapping فعلی بر اساس HTTP status است؛ production body خطای AI را برای تعیین code عمومی parse نمی‌کند.
- `AiServiceClient.ThrowMappedFailure` پیش از deserialization و strict validation پاسخ موفق اجرا می‌شود. مشاهده code upstream در ابزار تشخیصی، تغییر رفتار adapter نیست.

پاسخ عمومی Backend با همان ساختار و مقادیر متنی/trace جایگزین‌شده:

```json
{
  "type": "Synthetic educational text.",
  "title": "Sample Title",
  "status": 503,
  "detail": "Synthetic educational text.",
  "code": "AI_SERVICE_UNAVAILABLE",
  "traceId": "Synthetic educational text."
}
```

### Safe Backend diagnostic evidence

این رویداد ثبت‌شده از observer و نتیجه HTTP Backend است؛ لاگ داخلی AI یا provider نیست:

```json
{
  "operation": "teacher_activity_suggestions",
  "aiHttpStatus": 502,
  "aiErrorCode": "AI_RESPONSE_INVALID",
  "backendHttpStatus": 503,
  "backendErrorCode": "AI_SERVICE_UNAVAILABLE",
  "requestId": "819e4d0cd37f440c8ce85f7d2af40e43",
  "correlationId": "step10g-evidence-ce937cfe236b46b396df260093581006",
  "aiDurationMs": 5040.92,
  "backendDurationMs": 5294.81,
  "validationCategory": "UpstreamErrorResponse_StatusMapping_SuccessDtoValidationNotReached"
}
```

- Exception type مشاهده‌شده در logger میزبان: `MakanApp.Application.Ai.AiServiceException`.
- Inner exception: `null`.
- Backend JSON path / line / byte position: `null` / `null` / `null`؛ این‌ها نبود خطای deserialize موفق در Backend را نشان می‌دهند، نه نبود خطای داخلی AI.
- request body و correlation header در capture قبلی با guard موجود برابر بررسی شدند؛ credential یا header محرمانه در سند نیست.

### Provider / model / runtime metadata

- در همین ErrorResponse هیچ provider/model alias، model name، AI service version، `processingTimeMs`، repair metadata یا runtime/version header نگه‌داری نشده است: **Not observable from Backend response/logs.**
- مشاهده readiness در بخش 6 جداست و به‌عنوان metadata همین response معرفی نمی‌شود.
- contract version ارسالی در capture طبق guard موجود: `v1`.

### AI-internal diagnosis visibility

Observed by Backend:

- AI یک ErrorResponse معتبر JSON با HTTP 502 و code/message/requestId بالا برگردانده است.
- Backend آن را به HTTP 503 / `AI_SERVICE_UNAVAILABLE` نگاشت کرده است؛ validation پاسخ موفق Backend اجرا نشده است.

Not observable by Backend:

- internal validation layer؛ **Not observable from Backend boundary.**
- internal provider raw output؛ **Not observable from Backend boundary.**
- exact Pydantic/JSON-schema failing path؛ **Not observable from Backend boundary.**
- expected type/value/schema و actual provider-produced type/value؛ **Not observable from Backend boundary.**
- bounded repair attempted/not attempted؛ **Not observable from Backend boundary.**
- repair result و علت نهایی داخلی `AI_RESPONSE_INVALID`؛ **Not observable from Backend boundary.**

Backend wire evidence cannot reveal internal AI validation path.

AI runtime sanitized validation log is required.

### AI runtime logs

AI runtime logs were not available from the Backend environment.

لاگ داخلی متناظر با همین requestId/correlationId در شواهد نگه‌داری‌شده Backend موجود نیست؛ مسیر schema یا رفتار repair حدس زده نشده است.

## 5. Manager Class Insight

Endpoint: `POST /api/v1/ai/managers/class-insight`؛ این route داخلی Backend → AI است، نه لزوماً route عمومی.

### Invocation metadata

- Operation: `manager_class_insight`.
- requestId: `3a542b1ab6e84536b1ee18c8c02fcb87`.
- correlationId: `step10g-evidence-a5cf5820cc4c438cac870198366ccd1e`.
- Timestamp، شروع invocation به UTC: `2026-10-14T13:08:08.8344943+00:00`.
- AI environment: `Local / Integration`.
- AI base URL: `http://127.0.0.1:8000`.
- Duration: `2837.83 ms`؛ اندازه‌گیری واقعی observer با Stopwatch، شامل HTTP و خواندن body و sanitization در observer؛ این مقدار `processingTimeMs` مدل نیست.
- Backend public round-trip: `2909.3 ms`.
- Backend public endpoint، با class ID جایگزین‌شده در صورت وجود: `POST /api/v1/ai/managers/classes/00000000-0000-0000-0000-000000000104/insight`.
- Source: capture واقعیِ قبلاً نگه‌داری‌شده؛ بازبینی حاضر این invocation را تکرار نکرده است.

### Exact sanitized Backend -> AI request

```json
{
  "requestId": "3a542b1ab6e84536b1ee18c8c02fcb87",
  "correlationId": "step10g-evidence-a5cf5820cc4c438cac870198366ccd1e",
  "actor": {
    "userId": "00000000-0000-0000-0000-000000000108",
    "role": "Manager"
  },
  "scope": {
    "organizationId": "00000000-0000-0000-0000-000000000102",
    "schoolId": "00000000-0000-0000-0000-000000000102",
    "classId": "00000000-0000-0000-0000-000000000104"
  },
  "classData": {
    "classId": "00000000-0000-0000-0000-000000000104",
    "subjects": [
      {
        "classId": "00000000-0000-0000-0000-000000000104",
        "title": "Sample Title",
        "courseTitle": "Sample Course"
      }
    ],
    "grades": [],
    "homeworks": [],
    "exams": [],
    "activity": [],
    "students": [],
    "aggregates": {
      "averageScore": null,
      "scoreScale": "0-100 normalized percentage",
      "completionRate": null,
      "attendanceRate": null,
      "trend": "Unknown",
      "studentCount": 1,
      "classCount": null
    }
  },
  "language": "fa"
}
```

### Exact sanitized AI -> Backend response

- HTTP status: `502`.
- Content-Type: `application/json`.
- سایر safe response headerها: **Not available from retained Backend evidence.**
- `error.code`: `AI_RESPONSE_INVALID`.
- `error.message`: `AI structured response is invalid.`.
- `error.requestId` دقیقاً برابر requestId ارسال‌شده است.
- ErrorResponse فیلد correlationId ندارد؛ چنین فیلدی به آن اضافه نشده است.

این body واقعی upstream است که پیش از mapping Backend مشاهده و sanitize شده بود، نه پاسخ نهایی Backend:

```json
{
  "error": {
    "code": "AI_RESPONSE_INVALID",
    "message": "AI structured response is invalid.",
    "requestId": "3a542b1ab6e84536b1ee18c8c02fcb87"
  }
}
```

### Backend error mapping

- AI HTTP: `502`.
- AI error.code مشاهده‌شده در boundary: `AI_RESPONSE_INVALID`.
- Backend mapped public code: `AI_SERVICE_UNAVAILABLE`.
- Frontend/API HTTP: `503`.
- mapping فعلی بر اساس HTTP status است؛ production body خطای AI را برای تعیین code عمومی parse نمی‌کند.
- `AiServiceClient.ThrowMappedFailure` پیش از deserialization و strict validation پاسخ موفق اجرا می‌شود. مشاهده code upstream در ابزار تشخیصی، تغییر رفتار adapter نیست.

پاسخ عمومی Backend با همان ساختار و مقادیر متنی/trace جایگزین‌شده:

```json
{
  "type": "Synthetic educational text.",
  "title": "Sample Title",
  "status": 503,
  "detail": "Synthetic educational text.",
  "code": "AI_SERVICE_UNAVAILABLE",
  "traceId": "Synthetic educational text."
}
```

### Safe Backend diagnostic evidence

این رویداد ثبت‌شده از observer و نتیجه HTTP Backend است؛ لاگ داخلی AI یا provider نیست:

```json
{
  "operation": "manager_class_insight",
  "aiHttpStatus": 502,
  "aiErrorCode": "AI_RESPONSE_INVALID",
  "backendHttpStatus": 503,
  "backendErrorCode": "AI_SERVICE_UNAVAILABLE",
  "requestId": "3a542b1ab6e84536b1ee18c8c02fcb87",
  "correlationId": "step10g-evidence-a5cf5820cc4c438cac870198366ccd1e",
  "aiDurationMs": 2837.83,
  "backendDurationMs": 2909.3,
  "validationCategory": "UpstreamErrorResponse_StatusMapping_SuccessDtoValidationNotReached"
}
```

- Exception type مشاهده‌شده در logger میزبان: `MakanApp.Application.Ai.AiServiceException`.
- Inner exception: `null`.
- Backend JSON path / line / byte position: `null` / `null` / `null`؛ این‌ها نبود خطای deserialize موفق در Backend را نشان می‌دهند، نه نبود خطای داخلی AI.
- request body و correlation header در capture قبلی با guard موجود برابر بررسی شدند؛ credential یا header محرمانه در سند نیست.

### Provider / model / runtime metadata

- در همین ErrorResponse هیچ provider/model alias، model name، AI service version، `processingTimeMs`، repair metadata یا runtime/version header نگه‌داری نشده است: **Not observable from Backend response/logs.**
- مشاهده readiness در بخش 6 جداست و به‌عنوان metadata همین response معرفی نمی‌شود.
- contract version ارسالی در capture طبق guard موجود: `v1`.

### AI-internal diagnosis visibility

Observed by Backend:

- AI یک ErrorResponse معتبر JSON با HTTP 502 و code/message/requestId بالا برگردانده است.
- Backend آن را به HTTP 503 / `AI_SERVICE_UNAVAILABLE` نگاشت کرده است؛ validation پاسخ موفق Backend اجرا نشده است.

Not observable by Backend:

- internal validation layer؛ **Not observable from Backend boundary.**
- internal provider raw output؛ **Not observable from Backend boundary.**
- exact Pydantic/JSON-schema failing path؛ **Not observable from Backend boundary.**
- expected type/value/schema و actual provider-produced type/value؛ **Not observable from Backend boundary.**
- bounded repair attempted/not attempted؛ **Not observable from Backend boundary.**
- repair result و علت نهایی داخلی `AI_RESPONSE_INVALID`؛ **Not observable from Backend boundary.**

Backend wire evidence cannot reveal internal AI validation path.

AI runtime sanitized validation log is required.

### AI runtime logs

AI runtime logs were not available from the Backend environment.

لاگ داخلی متناظر با همین requestId/correlationId در شواهد نگه‌داری‌شده Backend موجود نیست؛ مسیر schema یا رفتار repair حدس زده نشده است.

## 6. Backend-observable environment metadata

این بخش تنها مشاهدات health/readiness نگه‌داری‌شده هنگام capture قبلی را بیان می‌کند؛ در بازبینی حاضر health/readiness نیز دوباره فراخوانی نشدند.

- `GET /api/v1/health`: HTTP `200`، `status=ok`، `service=MakanApp AI Core Service`.
- `GET /api/v1/ready`: HTTP `200`، `status=ready`، `provider=openrouter`، `checks.configuration=ok`، `issues=[]`.
- زمان دقیق این دو probe: **Not available from retained Backend evidence.**
- نام provider بالا فقط مقدار گزارش‌شده readiness است؛ از آن نمی‌توان مدل runtime، تعداد فراخوانی واقعی provider یا محتوای خروجی provider در سه case را نتیجه گرفت.
- Model alias / model name: **Not observable from Backend response/logs.**
- AI commit/version used: unable to prove from Backend runtime.
- version یا model از مستندات، Git مخزن AI یا تنظیمات آن استنتاج نشده است. در این درخواست هیچ دسترسی به مخزن AI انجام نشد.
- Content-Type و contract version مشاهده‌شده در بخش هر operation ثبت شده‌اند؛ سایر runtime headerها در retained capture موجود نیستند.

## 7. AI runtime logs and AI-team correlation

AI runtime logs were not available from the Backend environment.

جست‌وجو به فایل‌ها و artifactهای مجاز Backend محدود بود؛ هیچ دسترسی جدیدی به سیستم یا مخزن AI گرفته نشد. لاگ داخلی AI/provider برای این traceها در Backend نگه‌داری نشده است. رویدادهای observer در بخش‌های بالا جای لاگ runtime AI معرفی نمی‌شوند.

| Operation | requestId | correlationId |
|---|---|---|
| Parent Child Summary | `aa8520be38e745fdbe8195d424674754` | `step10g-evidence-8c923d675594444f99b6674eace0cfd1` |
| Teacher Activity Suggestions | `819e4d0cd37f440c8ce85f7d2af40e43` | `step10g-evidence-ce937cfe236b46b396df260093581006` |
| Manager Class Insight | `3a542b1ab6e84536b1ee18c8c02fcb87` | `step10g-evidence-a5cf5820cc4c438cac870198366ccd1e` |

تیم AI باید در صورت وجود، کوچک‌ترین excerpt sanitize‌شده متناظر را برای validation layer، failing path، expected/actual type، bounded repair و نتیجه آن استخراج کند؛ raw prompt/context، token و داده خصوصی نباید برای این کار منتشر شوند.

## 8. Diagnostic classification and limits

- هر سه body دریافت‌شده از AI، JSON معتبرِ envelope خطا هستند؛ malformed بودن خروجی داخلی مدل از آن‌ها قابل تشخیص نیست.
- mismatch در requestId پاسخ خطا دیده نشد. ErrorResponse correlationId ندارد؛ نبود این فیلد به‌تنهایی نتیجه‌گیری درباره success schema یا علت failure نیست.
- وجود missing field، invalid enum، wrong type، extra field، nullability mismatch یا اشکال reference validation در خروجی داخلی AI: **Not observable from Backend boundary.**
- تفاوت shape درخواست‌ها عیناً حفظ شده است: Parent دارای `classId: null` و `studentId` در scope است؛ Teacher/Manager scope دارای `classId` و فاقد فیلد `studentId` هستند. این فقط مشاهده shape است، نه تشخیص علت.
- پیام عمومی `AI_RESPONSE_INVALID` به‌تنهایی validation layer، failing JSON path، expected/actual value یا اجرای repair را ثابت نمی‌کند.
- capture موجود مربوط به fixture ساختگی است، نه داده production. متن و شناسه‌های sanitize‌شده برای بررسی shape مناسب‌اند؛ replay آن‌ها تضمین بازتولید همان پاسخ nondeterministic مدل نیست.
- وضعیت پذیرش STEP 10G همچنان BLOCKED است؛ موفقیت سه operation اعلام نشده است.

## 9. Validation and preserved boundaries

### نتایج تاریخی، نه اجرای این بازبینی

capture قبلی با مسیر موجود زیر انجام شده بود:

```powershell
dotnet build tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Debug --no-restore --nologo --verbosity minimal
dotnet run --project tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Debug --no-build -- --verify-failure-sanitization
dotnet run --project tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Debug --no-build -- --run-live --collect-failure-evidence
```

- مطابق artifact قبلی: build ابزار موفق با صفر warning/error، self-check آفلاین sanitizer موفق، سه درخواست واقعی هدف بدون retry؛ نتیجه هر سه AI 502 / Backend 503 بود.
- شمار 963 تست موفق در گزارش 10 مربوط به validation تاریخی همان مرحله است؛ این سند اجرای تازه آن تست‌ها را ادعا نمی‌کند.
- fixture موجود در اجرای قبلی فقط دیتابیس تست اختصاصی را ساخته و پس از پایان حذف کرده بود؛ migrationهای موجود در همان fixture replay شده بودند. این توضیح مجوز یا ادعای اجرای migration در بازبینی حاضر نیست.

### کار انجام‌شده در درخواست حاضر

- فقط همین سند از retained evidence به قالب جدید تبدیل شد؛ هیچ ابزار، تست، DTO، Controller، mapping، adapter، قرارداد v1 یا رفتار runtime تغییر نکرد.
- هیچ AI call، provider call، restore، build، test، migration یا اجرای fixture در این بازبینی انجام نشد؛ بررسی این تغییر مستندی به صحت JSON، حفظ عین بلوک‌های evidence، ارتباط traceها، sanitization و مقایسه fingerprint فایل‌ها محدود است.
- هر ۱۲ بلوک JSON نسخه ورودی عیناً حفظ شده‌اند؛ سه requestId/correlationId، زمان‌ها، durationها و code/statusها تغییر نکرده‌اند.
- مقایسه SHA-256 هر ۸۸۲ فایل tracked و untracked غیرignored با شروع این درخواست تأیید کرد که فقط همین سند تغییر کرده است؛ فایل جدید یا حذف‌شده‌ای وجود ندارد. وضعیت dirty قبلی باقی مانده و اصلاحات uncommitted قبلی byte-for-byte حفظ شده‌اند.
- بررسی مستندی موفق بود: هر ۱۲ بلوک JSON معتبر و عین نسخه ورودی، ارتباط requestId/correlationId و mapping هر سه case سازگار، code fenceها متوازن و بدون trailing whitespace. هیچ تطبیق شماره تلفن/email، JWT، provider-key pattern، credential header یا connection-string pattern در سند یافت نشد؛ این بررسی الگوها جای audit لاگ‌های داخلی AI نیست.
- `git diff --check` بدون خطای whitespace پایان یافت و index خالی است. هشدارهای LF→CRLF مربوط به سه فایل source/test از قبل dirty بودند؛ build جدیدی اجرا نشده و این پیام‌ها warning کامپایل نیستند.
- هیچ فایل source/test/tool، solution، schema، migration، appsettings یا سند دیگری تغییر نکرده است. هیچ frontend یا مخزن AI لمس نشده است.
- Stage: NO. Commit: NO. Push: NO. Reset/clean/checkout/discard: NO.
- این تحویل فقط evidence است؛ هیچ failure fix یا کاهش strictness قرارداد انجام نشده است.
