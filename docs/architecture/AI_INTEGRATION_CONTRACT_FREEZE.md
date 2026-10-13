# قرارداد Freeze شده Backend و AI

نسخه قرارداد: `v1`

## قواعد عمومی

- Media type: `application/json`
- naming: `camelCase`
- unknown field در AI request مردود است.
- همه زمان‌ها ISO-8601 و UTC هستند.
- شناسه‌ها UUID هستند.
- `requestId` برای idempotent tracing هر invocation اجباری است.
- `correlationId` از Backend به AI منتقل می‌شود و در پاسخ/telemetry حفظ می‌شود.
- احراز کاربر فقط در Backend؛ احراز caller در AI فقط با service JWT.
- AI پاسخ advisory می‌دهد و هیچ domain write یا authorization decision انجام نمی‌دهد.

## Service endpoints موجود و Freeze شده

| Capability | AI route | Role | Context اجباری |
|---|---|---|---|
| Generic chat | `POST /api/v1/ai/chat` | چهار نقش | actor، scope مجاز، message، context کمینه |
| Student analysis | `POST /api/v1/ai/students/analyze` | Student | organization/school/student و داده خود دانش‌آموز |
| Study plan | `POST /api/v1/ai/study-plan` | Student | goal/preferences و داده منتشرشده/آزادشده |
| Homework explanation | `POST /api/v1/ai/homeworks/explain` | Student | homework منتشرشده و question کاربر |
| Exam preparation | `POST /api/v1/ai/exams/prepare` | Student | exam منتشرشده؛ بدون answer key خصوصی |
| Child summary | `POST /api/v1/ai/parents/child-summary` | Parent | child انتخاب‌شده با رابطه فعال |
| Class summary | `POST /api/v1/ai/teachers/class-summary` | Teacher | class دارای teacher assignment فعال |
| Student insight | `POST /api/v1/ai/teachers/student-insight` | Teacher | learner عضو class مجاز |
| Activity suggestions | `POST /api/v1/ai/teachers/activity-suggestions` | Teacher | class مجاز و constraints |
| School summary | `POST /api/v1/ai/managers/school-summary` | Manager | organization فعال جاری |
| Manager class insight | `POST /api/v1/ai/managers/class-insight` | Manager | class متعلق به organization جاری |
| Manager student insight | `POST /api/v1/ai/managers/student-insight` | Manager | learner در organization جاری |

health/readiness می‌توانند public بمانند؛ تمام routeهای `/api/v1/ai/*` باید از STEP 10B service authentication بخواهند.

## Backend public API

برای generic chat قرارداد public زیر Freeze می‌شود:

```text
POST /api/v1/ai/chat
Authorization: Bearer <user access token>
X-Correlation-ID: optional
```

Request:

```json
{
  "message": "string, required",
  "conversationId": "uuid, optional"
}
```

Response موفق `200`:

```json
{
  "requestId": "uuid",
  "correlationId": "string",
  "conversationId": "uuid|null",
  "answer": { "text": "string", "language": "fa" },
  "insights": [],
  "risks": [],
  "recommendations": [],
  "sources": [],
  "metadata": { "modelAlias": "string", "processingTimeMs": 0 }
}
```

Backend فیلدهای `actor`، `role`، `scope` و `context` را خودش می‌سازد و از client قبول نمی‌کند.

## خطاهای پایدار Backend

| Code | HTTP | معنا |
|---|---:|---|
| `ai_feature_disabled` | 503 | قابلیت خاموش است |
| `ai_context_not_allowed` | 403 | context یا role مجاز نیست |
| `ai_invalid_request` | 400 | ورودی client نامعتبر است |
| `ai_rate_limited` | 429 | محدودیت invocation |
| `ai_service_unauthorized` | 503 | credential داخلی پذیرفته نشده؛ جزئیات به client نشت نمی‌کند |
| `ai_service_unavailable` | 503 | service/provider/circuit unavailable |
| `ai_service_timeout` | 504 | deadline داخلی سپری شد |
| `ai_response_invalid` | 502 | JSON/schema پاسخ معتبر نیست |

خطاهای AI به ProblemDetails عمومی ترجمه می‌شوند؛ body خام، prompt، credential و provider detail به client منتقل نمی‌شود.

## سازگاری

- تغییر breaking به schema/route/error نیازمند contract version جدید است.
- افزودن field اختیاری فقط پس از contract test هر دو repo مجاز است.
- Backend باید response ناسازگار را fail closed تلقی کند.
- timeout و cancellation باید end-to-end منتقل شوند.
