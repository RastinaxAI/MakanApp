# تحویل Personal AI و Personal Knowledge/RAG

تاریخ: `2026-11-13`؛ مبنای شروع: `fc5b297d20cc1cb97fc3b75a1762427bf81e419e`، شاخهٔ `feature/product-v6-backend-alignment`.

**وضعیت: BE19_AI_GATEWAY=PARTIAL.** حساب عادیِ فعال، بدون نقش سازمانی، مجاز به انتخاب Assistant شخصی و استفادهٔ مجاز از Knowledge/RAG شخصی است. Generic Personal Chat تا اصلاح قرارداد خارجی `BLOCKED_EXTERNAL_CONTRACT` است؛ موفقیت تست کنترل‌شدهٔ Backend، پذیرش واقعی سرویس مستقر محسوب نمی‌شود.

## سه انتخاب مستقل

| مفهوم | مسئولیت | رفتار Personal |
|---|---|---|
| School Workspace | محیط کاری آموزشگاه/نقش جاری رابط | تغییرش Assistant ذخیره‌شده را تغییر نمی‌دهد. |
| Assistant Data Context | دامنهٔ دادهٔ AI، مستقل و متعلق به نشست | Actor همان حساب؛ OrganizationId، MembershipId، Role، SubjectOrganizationPersonId و ClassId تهی‌اند. |
| Commercial Payer | مالک هزینه و سهمیه | پیش‌فرض Individual؛ FreeTrial فقط انتخاب صریح؛ Organization نامجاز است. |

Payer هیچ حق خواندن منبع، نقش یا مجوز آموزشی ایجاد نمی‌کند. یک حساب در دو Session می‌تواند دو Assistant Context متفاوت داشته باشد؛ Tabهای دارای همان Session، انتخاب همان Session را به اشتراک می‌گذارند. کلاینت نباید با تغییر Workspace، Assistant را خودکار عوض کند.

## انتخاب Assistant

`GET /api/v1/ai/assistant-context` وضعیت همین نشست را برمی‌گرداند. برای انتخاب صریح:

```http
PUT /api/v1/ai/assistant-context
Content-Type: application/json
```

```json
{
  "kind": "Personal",
  "clientOperationId": "11111111-1111-4111-8111-111111111111"
}
```

نام فیلدها و enum نهایی در OpenAPI مرجع‌اند. برای تغییر انتخاب موجود، `expectedVersion` را از `version` آخرین پاسخ بفرستید؛ حذف یا کهنگی آن می‌تواند `409 ASSISTANT_CONTEXT_CONFLICT` بدهد. ورودی Organization/Membership/Role برای Personal ارسال نشود. در Organization/Class، انتخاب صریح Membership/Role/Class فقط selector است و سرور مجوز جاری را بررسی می‌کند. اگر انتخابی وجود نداشته باشد، رفتار سازگاری موجود، زمینهٔ فعلی را یک بار تثبیت می‌کند؛ برای UX قابل پیش‌بینی انتخاب را صریح کنید.

پاسخ Personal فعلاً `upstreamContextSupported=false` و `limitation=AI_PERSONAL_CONTEXT_UPSTREAM_CONTRACT_REQUIRED` دارد. این limitation مربوط به **Business Generic Chat** است؛ به معنی منع Knowledge/RAG شخصی نیست.

## Generic Chat و مانع خارجی

قرارداد فعلی: `POST /api/v1/ai/chat` با `message`، `conversationId` اختیاری، `operationId` اختیاری و `selectedSourceIds` اختیاری. Headerهای موجود:

- `X-AI-Operation-Id`: UUID ثابت intent؛ اگر `operationId` در body هم هست، هر دو یکسان باشند.
- `X-AI-Payer`: `Individual`، `FreeTrial` یا `Organization`؛ در Personal مقدار Organization خطای `COMMERCE_CONTEXT_NOT_ALLOWED` می‌دهد.

در Personal بدون منبع انتخابی، پاسخ فعلی **409 / AI_PERSONAL_CONTEXT_UPSTREAM_CONTRACT_REQUIRED** است؛ هیچ تماس Provider، رزرو یا مصرفی ایجاد نمی‌شود. نقش Student/Parent/Teacher/Manager یا سازمان جعلی برای عبور از این مرز ارسال نمی‌شود. Intent داخلی `PersonalAiChatIntent` نقش wire ندارد و Endpoint/Actor خارجی جدید نیست.

شاهد قرارداد در `2026-11-13`: آدرس اعلام‌شدهٔ `/api/v1/openapi.json` برابر404؛ [OpenAPI واقعی AI](https://195.177.255.98/openapi.json) برابر200 و `UserRole` فقط شامل چهار نقش آموزشی فوق است. `Actor.role` الزامی است. SHA256 سند دریافت‌شده: `fc6bf405aefcc2232e0e35503fbee43fd82313d785fe41658bd474f8ad61277b`. تا تأیید قرارداد role-free یا Actor عمومی در همان سرویس، Chat شخصی فعال تلقی نشود.

Organization Generic Chat همچنان scope/actor سازمانی معتبر می‌خواهد. تحلیل دانش‌آموز، برنامهٔ مطالعه، توضیح تکلیف/آزمون، خلاصهٔ والد و عملیات Teacher/Manager عمومی نشده‌اند؛ خرید اشتراک Individual یا انتخاب Personal این محدودیت‌ها را حذف نمی‌کند.

`conversationId` فقط شناسهٔ قرارداد موجود است؛ تاریخچهٔ Messenger، Direct خصوصی، Contacts یا Address Book به Context AI اضافه نمی‌شود. ذخیرهٔ تاریخچهٔ عمومی شخصی یا بازپخش متن پاسخ در این مرحله ساخته نشده است.

## منابع شخصی و مسیر عملی Knowledge

همان APIهای `sources` برای Upload/Create/Publish/Processing استفاده شوند. منبع قابل استفاده باید متعلق به همین حساب، Personal، Active، Published، SourceReady، FileReady و FileValidated باشد. version باید نسخهٔ دقیقِ انتخاب‌شده باشد؛ جدیدترین نسخه خودکار جایگزین نسخهٔ pinشده نمی‌شود. Library برای استفادهٔ AI باز نشده است.

`POST /api/v1/ai/sources/preflight` فقط مجوز و آمادگی انتخاب را توضیح می‌دهد؛ فراخوانی AI یا مصرف سهمیه نمی‌سازد. مسیر legacyِ `selectedSourceIds` در Business Chat هنوز `retrievalAvailable=false` و `AI_SOURCE_RETRIEVAL_NOT_AVAILABLE` می‌دهد. این مسیر با API عملی Knowledge یکی نیست و موفقیت Preflight اثبات اجرای Retrieval نیست.

| Endpoint | Request اصلی | نتیجه |
|---|---|---|
| `POST /api/v1/sources/{sourceId}/versions/{version}/knowledge/ingest` | `clientOperationId`، `expectedVersion`، `reason`؛ `payer` اختیاری |202 یا200 با وضعیت پردازش؛ bytes از Storage مجاز Backend خوانده می‌شوند. |
| `GET /api/v1/sources/{sourceId}/versions/{version}/knowledge/status?wait=false` | شناسه/نسخهٔ دقیق | وضعیت جاری؛ تا Ready، Query قابل اجرا نیست. |
| `POST /api/v1/ai/knowledge/retrieve` | `KnowledgeQueryCommand` |200 با evidence/citation مجاز؛ تولید متن و debit ندارد، ولی entitlement و مانده بررسی می‌شوند. |
| `POST /api/v1/ai/knowledge/rag/answer` | `KnowledgeQueryCommand` |200 با answer/claims/citations؛ یک واحد SimpleText از ledger موجود. |

نمونهٔ Query صرفاً ساختگی است:

```json
{
  "clientOperationId": "22222222-2222-4222-8222-222222222222",
  "query": "از منبع انتخابی توضیح بده",
  "sources": [{ "sourceId": "33333333-3333-4333-8333-333333333333", "version": 1 }],
  "maxResults": 8,
  "includeQuotePreview": false,
  "language": "fa",
  "payer": "Individual"
}
```

Knowledge از `clientOperationId` و `payer` در **body** استفاده می‌کند؛ Headerهای Business AI جایگزین این فیلدها نیستند. تعداد منابع1..32، هر SourceId یکتا، version مثبت، Query غیرخالی تا4000، maxResults بین1..20 و language برابرfa/en است. JWT نشست کاربر برای API Backend لازم است؛ این با Service Authentication بین Backend و AI تفاوت دارد.

Organization/Class source حتی برای Teacher/Manager دارای Workspace معتبر، در Assistant شخصی conceal می‌شود. تغییر انتخاب Assistant، لغو مجوز، غیرفعال‌شدن حساب، تغییر source/file/version یا readiness در ادامهٔ عملیات دوباره بررسی می‌شود؛ evidence نامجاز به کلاینت تحویل نمی‌شود. درخواست حذف حساب فعلاً request-only است؛ حذف اجراشده یا purge حدس زده نمی‌شود.

## سهمیه، Retry و UX

| وضعیت | رفتار Client |
|---|---|
| Loading | همان operationId و payload را حفظ کند؛ برای Timeout شناسهٔ تازه نسازد. |
| `SUBSCRIPTION_INACTIVE` / `ENTITLEMENT_REQUIRED` | اشتراک/حق معتبر لازم است؛ بدون انتخاب صریح به Trial یا سازمان fallback نکند. |
| `QUOTA_EXCEEDED` | وضعیت سهمیه را تازه بخواند و خرید/تمدید مجاز پیشنهاد دهد؛ Retry خودکار نکند. |
| منبع NotReady / نسخه ناسازگار | وضعیت Processing و نسخهٔ منبع را دوباره بخواند؛ منبع دیگری جایگزین نشود. |
| `AI_OPERATION_IN_PROGRESS` | اجرای همان intent در جریان است؛ تماس تکراری Provider ساخته نمی‌شود. |
| `AI_OPERATION_ALREADY_COMPLETED` |409؛ پاسخ خصوصی cache نشده و قابل بازگردانی نیست؛ مصرف تکراری انجام نمی‌شود. |
| `AI_OPERATION_OUTCOME_UNKNOWN` | نتیجهٔ تولید قطعی نیست؛ رزرو Unknown باقی می‌ماند و همان intent خودکار دوباره اجرا نمی‌شود. |
| `AI_OPERATION_PREVIOUSLY_FAILED` | عملیات قبلاً Released شده؛ همان intent دوباره اجرا نمی‌شود. |
| `COMMERCE_USAGE_CONFLICT` / `AI_SOURCE_OPERATION_CONFLICT` | همان شناسه با قصد، Payer یا نسخهٔ متفاوت استفاده شده؛ به‌جای Retry کور، ورودی/Context بررسی شود. |
| `AI_SOURCE_CONTEXT_MISMATCH` /403/404 | انتخاب/مجوز جاری تغییر کرده؛ خروجی خصوصی نمایش داده نشود و Context/منابع دوباره resolve شوند. |
| `AI_PERSONAL_CONTEXT_UPSTREAM_CONTRACT_REQUIRED` | پیام محدودیت سرویس نمایش داده شود؛ Role/Organization جعلی یا Retry مداوم ساخته نشود. |

شکست پیش از Generation رزرو را Released می‌کند؛ پاسخ موفق Consumed است؛ قطع انتقال پس از شروع Generation می‌تواند Unknown باشد. SQL مرجع نهایی است؛ رزرو موازی و ledger جدید ساخته نشده‌اند. Trial سهمیهٔ جدا و صریح دارد و با اشتراک Individual یکی نیست.

## امنیت و مشاهده‌پذیری

Service Authentication هر دو مسیر AI و Knowledge **DISABLED** مانده است؛ کلید/JWT/Authorization سرویس اضافه نشده. Authentication نشست Backend، ACL منبع و Commerce همچنان اجباری‌اند. prompt، متن منبع، شماره، مخاطبان، token و secret در metric ثبت نمی‌شوند.

در تنظیم پایه، دو فلگ `AiService:UseServiceAuthentication` و `AiKnowledge:UseServiceAuthentication` صریحاًfalse هستند؛ فایل‌های خصوصی Test/Production تغییر نکرده‌اند. `AiService:Enabled` و Runtime پیش‌فرض Knowledge همچنان خاموش‌اند. این Task آن‌ها را فعال نمی‌کند؛ پذیرش کنترل‌شدهٔ تست، به معنی فعال‌بودن RAG در سرور نیست. Client در محیط خاموش باید وضعیت Feature Disabled را نشان دهد و Retry یا fallback پنهان انجام ندهد.

سنجه‌های `makan.ai.execution.results` و `makan.ai.execution.duration` در Meter موجود `MakanApp.AiService` با tagهای محدود `operation/contextKind/payerKind/result`، Use Case را از سنجه‌های transport جدا می‌کنند. شکست یا مرز قرارداد success ثبت نمی‌شود. Correlation و OperationId برای پیگیری خطا استفاده شوند؛ متن خصوصی در گزارش خطا کپی نشود.

پذیرش این مرحله: Backend HTTP + SQL ایزوله + adapter واقعی Knowledge با transport کنترل‌شده. پذیرش زندهٔ Personal Chat یا Deploy سرور انجام نشده است. آمار نهایی Build/Test و Git در [گزارش پیاده‌سازی](BACKEND_V6_IMPLEMENTATION_REPORT_FA.md) و پاسخ نهایی این مرحله ثبت می‌شود.
