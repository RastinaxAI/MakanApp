# قرارداد نهایی Backend و Knowledge — ۲۰۲۶-۱۱-۰۸

این سند تغییر همین مرحله را ثبت می‌کند؛ گزارش‌های قدیمی و claim تیم AI مدرک نسخهٔ مستقر نیستند. مبنای شروع Backend `3668be0233f856b982ba7eddfeddc96af54d1c0b` بود. Client دوازده عملیات Business، Context builderها، Source ACL، Pin و Ledger سهمیه موجود بودند؛ Client و مسیر اجرایی Knowledge وجود نداشتند. [راهنمای اجرای مرحله](../ai-handoff/KNOWLEDGE_FINAL_INTEGRATION_RUNBOOK_FA.md).

## مرجع قرارداد و مشاهدهٔ واقعی

پرامپت مالک به Handoff مورخ ۲۰۲۶-۱۱-۰۷ اشاره می‌کند؛ PDFهای پیدا‌شدهٔ محلی Knowledge تاریخ ۲۰۲۶-۱۱-۰۵ و Business تاریخ ۲۰۲۶-۱۰-۲۹ دارند. محدودیت‌های تاریخی PDF، از جمله نبود Lifecycle/RAG، به کد جدید تحمیل نشد. Schema و router جاری `app/schemas/knowledge_v1.py`، `app/rag/knowledge_contract.py` و `app/rag/knowledge_http.py` از مخزن AI خوانده شدند. نسخهٔ اعلام‌شدهٔ `95ba30a...` واقعاً در Git وجود دارد، ولی نسخهٔ زنده از روی آن حدس زده نشد.

در بررسی واقعی، Business Health/Ready پاسخ200 دادند؛ OpenAPI عمومی۱۵مسیر و۱۲عملیات Business دارد. درخواست نامعتبر بدون JWT پاسخ422/AI_INVALID_REQUEST گرفت؛ این مشاهده با Auth اختیاریِ قرارداد فعلی سازگار است، ولی به‌تنهایی ترتیب enforcement برای درخواست معتبر را ثابت نمی‌کند. **Backend همان JWT موجود را حفظ می‌کند** و با credential موجود، تمام۱۲عملیات واقعی موفق شدند؛ سازوکار Auth جدید یا Credential خیالی ساخته نشد. گزینهٔ پیشنهادی حذف JWT Business در بازبینی خودکار رد شد و وارد Source نشد. timeout محلی۳۵ثانیه حفظ است؛ سیاست۳۰ثانیه/retry0 سمت Provider مربوط به AI است.

Knowledge مستقل با `X-AI-Contract-Version: knowledge-v1` مصرف می‌شود؛ `UseServiceAuthentication=false` همان قرارداد فعلی این سرویس است. قابلیت ارسال Credential موجود فقط برای تغییر صریح و مصوب بعدی آن قرارداد فراهم است. هیچ User Token به AI فرستاده نمی‌شود. BaseUrl از config خوانده می‌شود و URL عملیاتی در Client کدنویسی نشده است.

OpenAPI/Health/Ready اختصاصی Knowledge از prefix عمومی آزموده‌شده404 دادند؛ این پاسخ به معنی نبود سرویس نیست. Status، Ingest، Retrieve، RAG، Reindex، Supersede و Delete واقعی پاسخ معتبر دادند. فرض E5/Qdrant یا Commit/Image زنده از بیرون API تأیید نشد؛ Backend هیچ dimension، مدل فیزیکی یا SDK Vector DB ندارد.

## لایه‌ها و مالکیت

- Application: DTO/portهای `AiKnowledgeContracts`، قواعد دسترسی و Pin در `KnowledgeAuthorizationService`، orchestration در `KnowledgeIntegrationService` و projection وضعیت در `KnowledgeProcessingProjection`.
- Infrastructure: `AiKnowledgeClient` مسئول HTTP/JSON/multipart/hash/timeout/cancellation و اعتبارسنجی پاسخ؛ `AiKnowledgeOptions` مسئول config؛ `EfKnowledgeOperationAudit` مسئول ثبت metadata امن در Audit موجود.
- Api: `KnowledgeIntegrationController` فقط session/route/input را به use case می‌دهد؛ `KnowledgeContractTransformer` قرارداد عمومی و `GlobalExceptionHandler` خطای امن را بیان می‌کنند.
- Domain و مدل EF تغییر نکردند؛ همان `EducationalSource`/`EducationalSourceVersion` و invariantهای موجود استفاده می‌شوند. Migration جدید لازم نیست.

هیچ EF/SQL یا نقش کسب‌وکاری داخل Client نیست. Controller query یا مجوز دامنه ندارد. پردازش طولانی فایل در Runtime Knowledge است؛ Backend Worker/Queue موازی یا Vector Store ایجاد نمی‌کند.

## دسترسی و نسخه

هر selector `(SourceId, Version)` ابتدا از `IEducationalSourceStore` موجود resolve می‌شود: account/session/profile جاری، Scope واقعی، سازمان/کلاس مجاز، نسخهٔ منتشرشده، فایل آماده و Safety معتبر لازم‌اند. Library برای این مسیر بسته است؛ داشتن metadata کاتالوگ مجوز AI نیست. هر درخواست حداکثر۳۲منبع و یک نسخهٔ دقیق از هر Source دارد؛ نسخهٔ0، Guid خالی، wildcard یا آرایهٔ خالی رد می‌شود.

Scope ارسالی از Source مجاز ساخته می‌شود؛ `OwnerUserId` برای Personal همان حساب مالک است. شناسهٔ User و OrganizationPerson قابل جایگزینی نیستند. Backend قبل از RPC و بعد از دریافت پاسخ دوباره مجوز جاری را می‌خواند. revoke می‌تواند تحویل پاسخ درحال‌اجرا را قطع کند؛ حذف فوری Vector برای ACL لازم نیست.

Pin از جدول/قفل `IAiSourceSelectionStore` موجود استفاده می‌کند. User، Context، Payer، payload و نسخه‌های مرتب‌شده fingerprint می‌شوند؛ OperationId قبلی با payload متفاوت Conflict است. انتشار نسخه۲ به‌تنهایی نسخه۱ را در AI Supersede نمی‌کند. Lifecycle فقط با فرمان صریح writer و ExpectedVersion جاری Source اجرا می‌شود. Source آرشیوشده برای عملیات این مسیر قابل استفاده نیست؛ Archive مجوز retrieval ایجاد نمی‌کند.

## Transport و پاسخ

Ingest از فایل خود Backend و `DownloadAsync` مجاز byte دقیق می‌خواند، SHA-256 lowercase و طول واقعی می‌سازد و دو part `metadata: application/json` و `content: raw bytes` می‌فرستد. MIME فقط PDF/PNG/JPEG/TXT موجود است. همان SourceId/Version در URL و metadata استفاده می‌شود. همان identity/hash پس از202،200 می‌گیرد؛ hash متفاوت409/AI_SOURCE_VERSION_CONFLICT است. هیچ SourceId/Version تازه‌ای در Retry ساخته نمی‌شود.

Client هیچ write را خودکار retry نمی‌کند و redirect را دنبال نمی‌کند. پس از ACK گم‌شده، Status هویت دقیق برای reconciliation در دسترس است؛ ارسال مجددِ آگاهانهٔ همان intent همان hash دارد. Snapshot پاسخ خصوصی یا source bytes در SQL/Outbox/log ذخیره نمی‌شود. Staging پایدار، پردازش و بردارها متعلق به Knowledge هستند.

Status فقط با enum دقیق قرارداد پذیرفته می‌شود. تنها Ready برای Retrieve/RAG کافی است؛ Queued/Processing/ReindexRequired، failed، Deleted و Superseded مسدودند. polling با تعداد تلاش و مهلت کل محدود می‌شود؛ تمام‌شدن polling به معنی موفقیت یا failure قطعی پردازش نیست. PollingTimeout از HTTP Timeout جداست و caller cancellation حفظ می‌شود.

Retrieval باید requestId/correlationId همان درخواست، rank پیوسته، identity دقیق، chunkId و Citation منطبق داشته باشد؛ نتیجه خارج Allowlist، تکراری، نسخهٔ دیگر یا quote جعلی رد می‌شود. RAG قبل از generation شواهد واقعی را retrieve می‌کند و Citation هر Claim را با همان شواهد مقایسه می‌کند. Answer باید فقط اتصال متن Claimهای grounded باشد و Citation سطح بالا union مرتب آن‌ها باشد. InsufficientEvidence با200 و بدون Claim/Citation قابل قبول است؛ پاسخ بی‌مدرکِ fallback پذیرفته نیست. دادهٔ Source یا businessContext جای دستور امنیتی Backend نیست. Client عمومی businessContext دلخواه به این API اضافه نمی‌کند؛ فعلاً آرایهٔ خالی server-owned ارسال می‌شود.

## سهمیه و Retry

همان `CommercialSubscriptionService`، `UsageReservations` و `UsageLedgerEntries` استفاده می‌شوند. Ingest/Reindex/Retrieve به Preflight معتبر AI نیاز دارند؛ generation debit تازه برای آن‌ها ساخته نمی‌شود. خواندن Status و فرمان پایان نسخه مجوز داده/مدیریت لازم دارند و به ایجاد سهمیهٔ تازه وابسته نیستند.

RAG همان واحد موجود `simple_text_request` را یک بار Reserve می‌کند؛ Retrieve داخلی و Answer دو برداشت نیستند. پاسخ معتبر200، از جمله نتیجهٔ معتبر InsufficientEvidence، طبق واحد درخواست موجود Consumed است. شکست قبل از generation Released و نتیجهٔ مبهم پس از شروع generation Unknown می‌ماند؛ refund یا اجرای دوباره حدس زده نمی‌شود. Retry همان OperationId تکمیل‌شده409/AI_OPERATION_ALREADY_COMPLETED است؛ Reserved/Unknown/Released نیز اثر تکراری ایجاد نمی‌کنند. قیمت یا Subscription نامحدود جدید ساخته نشده است.

`GET /sources/{id}/processing` وقتی gate بسته است رفتار قبلی را حفظ می‌کند؛ هنگام فعال‌بودن، Status واقعی را با همان DTO موجود project می‌کند. `AiReady` آمادگی فنی و `CanUseForAi` مجوز تجاری/جاری را نیز درنظر می‌گیرد. List/Get معمولی برای هر ردیف تماس شبکه‌ای نمی‌سازند؛ نتیجهٔ قدیمی آن‌ها مجوز اجرای AI نیست. این projection داخل SQL cache نمی‌شود.

## API عمومی افزوده‌شده

همهٔ مسیرها Bearer/session فعال، ProblemDetails، rate limit موجود `ai-chat` و مجوز داخلی دارند. `ClientOperationId` و `Payer` در body هستند؛ Headerهای مسیر Business AI برای این API استفاده نمی‌شوند.

| Method / Route با prefix `/api/v1` | Request | Response و نکته |
|---|---|---|
| POST `/sources/{sourceId}/versions/{version}/knowledge/ingest` | KnowledgeMutationCommand | 202/200 KnowledgeProcessingStatus؛ writer، نسخهٔ Source و Reason |
| GET همان prefix با `/status?wait=false` | selector و wait اختیاری | 200 KnowledgeProcessingStatus؛ پاسخ no-store؛ wait محدود |
| POST همان prefix با `/delete` | KnowledgeMutationCommand | 200 Deleted؛ هویت دقیق؛ فایل Backend از این API حذف نمی‌شود |
| POST همان prefix با `/supersede` | KnowledgeMutationCommand | 200 Superseded؛ فرمان صریح و terminal |
| POST همان prefix با `/reindex` | KnowledgeMutationCommand | 202 Queued؛ raw staging در AI حفظ می‌شود |
| POST `/ai/knowledge/retrieve` | KnowledgeQueryCommand | 200 Ok/Empty؛ فقط SourceVersionهای مجاز |
| POST `/ai/knowledge/rag/answer` | KnowledgeQueryCommand | 200 Ok/InsufficientEvidence؛ Claim/Citation معتبر یا خطا |

Query شامل ClientOperationId، Query، Sources با `(sourceId,version)`، MaxResults پیش‌فرض۸ در بازه۱..۲۰، IncludeQuotePreview، Language فعلی fa/en و Payer اختیاری است. سقف Query۴۰۰۰کاراکتر است. RequestId و Correlation مورد اعتماد را Backend می‌سازد/عبور می‌دهد. Mutation به ExpectedVersion همان Source aggregate و Reason نیاز دارد؛ آن را با شمارهٔ Version یا GrantVersion مخلوط نکنید.

این مسیر اجرایی source-aware از دو endpoint جدید Retrieve/RAG استفاده می‌کند. مسیر تاریخی `AiSourceSelectionService` و `/ai/sources/preflight` برای افزودن `selectedSourceIds` به دوازده عملیات Business تغییر نکرده و همچنان RetrievalUnavailable گزارش می‌کند؛ فعال‌شدن Knowledge آن قرارداد قدیمی را خودکار فعال نمی‌کند. Client باید از قرارداد جدید استفاده کند؛ Source text به DTOهای Business v1 تزریق نشده است.

## خطا، مشاهده‌پذیری و عملیات

Knowledge کدهای upstream را فقط همراه status مجاز whitelist می‌کند:409 برای NotIngested/Processing/ProcessingFailed/VersionConflict،422 برای InvalidRequest/ContextTooLarge،503 برای Vector/Embedding/GenerationUnavailable،502 برای RAG ResponseInvalid. جزئیات `AI_OCR_FAILED` فقط در ProcessingFailed نگه داشته می‌شود. خطاهای محلی KnowledgeTimeout/PollingTimeout/ResponseInvalid/Unavailable نام مستقل دارند؛429 نیز حفظ می‌شود. Body، Message خام Provider و Exception داخلی به Client یا Log کپی نمی‌شوند.

Business public mapping قبلی حفظ است؛ کد دقیق و معتبر AI در `AiServiceException.UpstreamErrorCode` جدا می‌ماند. برای نمونه AI_INTERNAL_ERROR/502 همچنان public AI_RESPONSE_INVALID و AI_PROVIDER_TIMEOUT/504 همان AI_SERVICE_TIMEOUT می‌شود. کد یا status دلخواه Provider وارد catalog نمی‌شود. Correlation ناسازگار با header به‌جای trim مخفی رد می‌شود.

Telemetry از Meter/Activity موجود AI با operationهای ثابت `knowledge_*` استفاده می‌کند. scope لازم RequestId/Correlation، status/code و latency است؛ body/query/chunk/Source bytes/token/OTP/JWT لاگ نمی‌شوند. درخواست و نتیجهٔ lifecycle در AdminAudit موجود با metadata محدود ثبت می‌شود؛ Requested به معنی ACK یا Ready نیست. هیچ Pending operation یا Ledger پاک/بی‌دلیل requeue نمی‌شود.

برای Usage Reserved/Unknown آستانهٔ خودکار Stuck در Source موجود نیست؛ count و زمان را بدون نام‌گذاری قطعی Stuck گزارش کنید. آستانهٔ polling تعریف Stuck Worker نیست. ممیزی SQL خواندنیِ محلی با Provider واقعی، برای هر دو config به18456 رسید؛ این وضعیت، دسترسی و وضعیت Production Server را ثابت نمی‌کند. Worker/Queue زندهٔ AI بدون دسترسی Admin قابل تأیید نبود. صفر فرض نمی‌شود.

## شواهد و حد پذیرش

Probe C# از HTTP واقعی Backend، SQL تازهٔ GUID و AI واقعی استفاده کرد: چهار MIME به Ready رسیدند؛ Retrieval/RAG grounded، replay200/409، hash conflict، reject منبع خصوصی و revoke Enrollment بدون حذف Vector موفق شدند. شش منبع QA همان اجرا در AI پاک و دیتابیس/Storage موقت حذف شدند. Business نیز۱۵درخواست موفق در۱۲عملیات، همراه کنترل‌های منفی نقش/رابطه داشت. دادهٔ synthetic، موفقیت محیط Windows Backend/WireGuard، SLA یا کیفیت فارسی OCR را ثابت نمی‌کند.

Test/Production config جدید در fragmentهای بدون secret، با default Disabled و ساختار یکسان ارائه می‌شود؛ private appsettings/User Secrets/MeliPayamak دست‌نخورده‌اند. Runtimeهای واقعی و traffic فعال نشده‌اند. HA/RPO/RTO همچنان `OPEN_OPERATIONAL_DECISION` است. تا تأیید Worker، صف، Pendingها، پذیرش Test سرور و مسیر واقعی Backend، Cutover و اجازهٔ حذف resource/checkpoint قدیمی ممنوع است.
