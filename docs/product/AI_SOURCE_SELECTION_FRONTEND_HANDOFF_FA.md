# قرارداد انتخاب منبع برای AI

این قابلیت Foundation مجوزدهی است؛ Retrieval واقعی هنوز وجود ندارد. Client فقط Backend را فراخوانی می‌کند. Service JWT، manifest داخلی و credential به Client داده نمی‌شوند. [شرح Backend و ماتریس ۱۲ عملیات](../ai-handoff/AI_SOURCE_SELECTION_AND_AUTHORIZATION_FA.md).

## Selector و scope

فهرست را از `GET /api/v1/sources?scope=Personal|Class|Organization|Library` بگیرید. Class به `classId` همان context و Organization به workspace جاری وابسته است. همهٔ APIها Bearer کاربر می‌خواهند؛ شناسهٔ ارسال‌شده مجوز نیست.

| Tab | دادهٔ قابل مشاهده | AI فعلی |
|---|---|---|
| Personal | فقط منابع خود حساب، حتی هنگام داشتن نقش آموزشی | false؛ Published معتبر هم RetrievalUnavailable است |
| Class | کلاس جاری، طبق assignment/enrollment/manager | false؛ Class context دقیق لازم است |
| Organization | policy فعلی Manager-only همان سازمان | false؛ هیچ cross-org مجاز نیست |
| Library | metadata منابع Published/Public | false؛ ProductPolicyDisabled؛ دانلود عادی بسته |

Response موجود Source اکنون `aiReadiness="Unavailable"` و `aiUnavailableReason` دارد. `SourceNotReady` یعنی readiness/انتشار/processing نامناسب؛ `RetrievalUnavailable` یعنی Source منتشر و مجاز اما زیرساخت AI Source نداریم؛ `ProductPolicyDisabled` مختص Library است. `capabilities.canUseForAi=false` و `aiReady=false` را واقعی نمایش دهید؛ upload موفق را AI-ready نشان ندهید. Client می‌تواند توضیح قابلیت آینده را نشان دهد، اما نباید اجرای AI متکی به فایل را موفق یا فعال نمایش دهد.

## Preflight واقعی

`POST /api/v1/ai/sources/preflight` با body:

```json
{
  "operation": "TeacherActivitySuggestions",
  "selectedSourceIds": ["11111111-1111-4111-8111-111111111111"],
  "classId": "22222222-2222-4222-8222-222222222222",
  "assignmentId": null
}
```

Operationها: `Chat`، `StudyPlan`، `HomeworkExplain`، `TeacherActivitySuggestions`. Chat و StudyPlan، ClassId/AssignmentId نمی‌گیرند؛ HomeworkExplain فقط AssignmentId و TeacherActivitySuggestions فقط ClassId می‌گیرد. سرور Class تکلیف را خودش resolve می‌کند. Preflight هیچ intent، سهمیه، Trial، پیامک یا AI call نمی‌سازد.

برای انتخاب معتبر و غیرخالی، HTTP 200:

```json
{
  "authorized": true,
  "retrievalAvailable": false,
  "selectedCount": 1,
  "errorCode": "AI_SOURCE_RETRIEVAL_NOT_AVAILABLE",
  "commercialStatus": "NotEvaluatedRetrievalUnavailable",
  "versions": [{ "sourceId": "11111111-1111-4111-8111-111111111111", "number": 1 }]
}
```

این پاسخ تأیید اجرای AI یا entitlement نیست؛ فقط انتخاب قابل خواندن بوده است. identity نسخه همان زوج SourceId/Number است. مجموعهٔ خالی بدون expand خودکار می‌ماند؛ status تجاری آن `UseExistingCommercePreflight` است. مجوز Preflight یک snapshot است و بعداً ممکن است revoke شود.

## درخواست اجرای چهار مسیر

فقط bodyهای زیر additive fieldهای اختیاری `selectedSourceIds` و `operationId` دارند:

- `POST /api/v1/ai/chat`: body قبلی Message/ConversationId.
- `POST /api/v1/ai/study-plan`: Goals/AvailableMinutes/Notes.
- `POST /api/v1/ai/homeworks/{assignmentId}/explain`: Question.
- `POST /api/v1/ai/teachers/classes/{classId}/activity-suggestions`: Objective/AvailableMinutes/Materials/Notes.

با selector غیرخالی، OperationId غیرخالی لازم است. حداکثر ۳۲ ورودی پذیرفته می‌شود؛ Guid.Empty رد و duplicateها حذف می‌شوند. ترتیب ورودی اهمیت ندارد. حتی انتخاب معتبر اکنون HTTP 409 و `AI_SOURCE_RETRIEVAL_NOT_AVAILABLE` می‌دهد؛ هیچ Provider یا Reservation فراخوانی نمی‌شود. سایر هشت AI endpoint قرارداد انتخاب منبع ندارند.

بدون selector، مسیر قبلی حفظ می‌شود. Chat source-less همچنان context سازمانی معتبر می‌خواهد؛ صرف امکان بررسی Personal source در Preflight، Personal inference ایجاد نمی‌کند. خطای `AI_FEATURE_DISABLED` برای AI خاموش حفظ شده است.

## Context، payer و quota

انتخاب workspace/class و دادهٔ مجاز با payer یکی نیست. Individual subscription مجوز فایل دیگران یا سازمان دیگر نمی‌دهد. Organization payer نیز مالک منابع شخصی دیگران نیست. هیچ fallback خودکار payer وجود ندارد.

فعلاً انتخاب منبع در Retrieval gate متوقف می‌شود؛ `NotEvaluatedRetrievalUnavailable` را quota کافی نمایش ندهید. برای source-less طبق قرارداد تجاری موجود از `POST /api/v1/commerce/ai/simple-text/preflight` با ContextType/ContextId/Payer استفاده کنید. Preflight تجاری فقط بررسی سهمیه است و `authorizesEducationalData=false` باقی می‌ماند. استفادهٔ موفق Trial روزانه حداکثر سه درخواست متن ساده در مرز روز تهران است. SourceCount ظرفیت ذخیرهٔ منابع است و با AiTextRequests یکی نیست؛ انتخاب، مصرف SourceCount ندارد.

## خطا، stale state و Retry

| وضعیت | Code | اقدام Client |
|---|---|---|
| 400 | AI_SOURCE_REQUEST_INVALID | Guid/تعداد/OperationId و selectorهای context را اصلاح کنید |
| 404 | AI_SOURCE_NOT_FOUND | منبع ناموجود یا غیرمجاز؛ از نمایش جزئیات وجود آن خودداری و فهرست را refresh کنید |
| 403 | AI_SOURCE_NOT_ALLOWED | Library AI policy بسته است؛ retry خودکار نکنید |
| 409 | AI_SOURCE_CONTEXT_MISMATCH | منبع متعلق به کلاس request نیست؛ context و انتخاب را دوباره بررسی کنید |
| 409 | AI_SOURCE_NOT_READY | انتشار، وضعیت فایل/processing یا archive را refresh کنید؛ صرف retry فایل را آماده نمی‌کند |
| 409 | AI_SOURCE_RETRIEVAL_NOT_AVAILABLE | قابلیت AI مبتنی بر منبع هنوز آماده نیست؛ source را بی‌صدا حذف و درخواست را تکرار نکنید |
| 409 | AI_SOURCE_OPERATION_CONFLICT | همان OperationId با payload/context/set متفاوت استفاده شده؛ برای قصد تازه ID تازه بسازید |

خطاهای نشست و `AI_CONTEXT_NOT_ALLOWED` قبلی نیز ممکن‌اند. ProblemDetails موجود از فیلد `code` استفاده می‌کند؛ `errorCode` در DTO موفق Preflight فیلدی مستقل است. correlation موجود را برای پیگیری نگه دارید؛ هیچ payload/توکن را برای diagnostics عمومی ثبت نکنید.

Retry همان intent باید OperationId و payload/context و مجموعهٔ منابع یکسان داشته باشد؛ `[A,B,A]` و `[B,A]` یک مجموعه‌اند. انتخاب جدید، متن جدید یا context جدید OperationId تازه می‌خواهد. حذف تمام selectorها از یک intent انتخاب‌شده نیز Conflict می‌دهد. اگر V2 منتشر شود، retry قبلی به V1 pin می‌ماند؛ intent جدید V2 را می‌بیند. این pin مجوز دائمی نیست و revoke/Archive/Rejected/ReviewRequired آن را غیرقابل استفاده می‌کند.

تغییرهای Source از invalidation و catch-up/replay موجود استفاده می‌کنند؛ event جدید یا stream پاسخ AI ساخته نشده است. پس از event یا تغییر context، فهرست و eligibility را از API دوباره بخوانید. Backend در هر execution مجوز جاری را دوباره می‌سنجد.

استخراج/OCR/chunking/embedding/vector/RAG و قرارداد Source مستقیم با AI Team مرحلهٔ آینده‌اند. frontend، تنظیمات خصوصی، MeliPayamak و AI Runtime در این مرحله تغییر نکرده‌اند.
