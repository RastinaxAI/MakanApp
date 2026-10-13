# قرارداد Error، Timeout و Resilience

## AI error envelope

```json
{
  "error": {
    "code": "AI_INVALID_REQUEST",
    "message": "Safe non-sensitive message.",
    "requestId": "req-123"
  }
}
```

Backend body خطای AI را به client عبور نمی‌دهد و از HTTP status برای mapping استفاده می‌کند.

## mapping upstream به Backend code

| AI/transport result | Backend code | Public status |
|---|---|---:|
| 400 یا 422 | `AI_INVALID_REQUEST` | 400 |
| 401 | `AI_SERVICE_UNAUTHORIZED` | 503 |
| 403 | `AI_SERVICE_FORBIDDEN` | 503 |
| 429 | `AI_RATE_LIMITED` | 429 |
| 504 یا client timeout | `AI_SERVICE_TIMEOUT` | 504 |
| 404، 409، 500، 502، 503 یا connection failure | `AI_SERVICE_UNAVAILABLE` | 503 |
| malformed JSON، content type نامعتبر، unknown response field، envelope/schema mismatch | `AI_RESPONSE_INVALID` | 502 |
| integration disabled | `AI_FEATURE_DISABLED` | 503 |

در context authorization failure قبل از network، Backend `AI_CONTEXT_NOT_ALLOWED` با status `403` می‌دهد. validation درخواست Backend با `AI_INVALID_REQUEST` و status `400` پاسخ می‌دهد.

## timeout و cancellation

- Backend default timeout: 15 seconds.
- مقدار معتبر configuration: 1 تا 120 ثانیه.
- caller cancellation بدون تبدیل به timeout حفظ می‌شود.
- timeout داخلی به `AI_SERVICE_TIMEOUT` تبدیل می‌شود.

## retry

`AiServiceClient` برای POSTهای AI retry خودکار ندارد. این تصمیم از duplicate provider work و latency غیرقابل‌کنترل جلوگیری می‌کند. اگر client عمومی request را retry کند، request جدید Backend یک `requestId` جدید می‌گیرد؛ AI نباید idempotency‌ای را که وجود ندارد ادعا کند.

## circuit breaker

- singleton process-local
- default threshold: سه failure متوالی
- default break: 30 seconds
- rangeهای معتبر: threshold از 1 تا 20 و break از 1 تا 300 ثانیه
- timeout، connection failure، statusهای 5xx و response نامعتبر failure گذرا ثبت می‌شوند.
- response موفق circuit را reset می‌کند.
- 4xx عادی به‌جز اثر mapping، circuit را باز نمی‌کنند.

این circuit distributed نیست و state میان instanceها share نمی‌شود.

## response validation

Backend موارد زیر را strict بررسی می‌کند:

- JSON content type
- unknown field ممنوع
- `requestId` و `correlationId` برابر request
- metadata و `modelAlias` معتبر
- `processingTimeMs >= 0`
- collectionهای الزامی non-null
- enum/range و fieldهای non-blank اختصاصی هر response

## tracing و telemetry

- header `X-Correlation-ID` و field `correlationId` ارسال می‌شوند.
- AI باید همان fieldها را بدون تغییر echo کند.
- Backend metricها: `makan.ai.client.requests` و `makan.ai.client.duration`.
- labelها فقط operation، outcome و stable error code هستند.
- token، payload، prompt، PII و provider body در metric ثبت نمی‌شوند.
- `Retry-After` upstream در exception داخلی خوانده می‌شود، ولی public ProblemDetails فعلی header جدیدی تولید نمی‌کند؛ client نباید وجود آن را فرض کند.
