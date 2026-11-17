# آمادگی Runtime محیط Test و گزارش توسعه‌دهنده

## بازپذیرش AI/Knowledge محیط Test — 2026-11-18

مبنای این بازپذیرش `f20cfcbbf381d1adde1ad1a9d3471ea9d064088e` روی
`feature/product-v6-backend-alignment` است؛ HEAD و Remote پس از Fetch برابر بودند. بخش‌های
پایین‌تر، گزارش مرحلهٔ قبلی از `f704a5c` را حفظ می‌کنند؛ شمارش این اجرا جدا ثبت می‌شود.

### ۱. خلاصهٔ مرحله

هدف، تأیید دوبارهٔ Runtime واقعی Test بدون تغییر Zibal یا Production است. تنظیم خصوصی Test
از قبل صحیح بود؛ هیچ مقدار یا فلگی در آن بازنویسی نشد. Personal Chat و زنجیرهٔ Knowledge با
Backend محلی، SQL/Storage ایزوله و adapter واقعی به سرویس مستقر دوباره موفق شدند.

### ۲. معماری

Domain/Application/Infrastructure/Api و ابزارها تغییر نکردند. ابزارهای موجود HTTP واقعی
Controller/Application را با SQL Server موقت و client واقعی AI/Knowledge اجرا می‌کنند.
وابستگی ابزار تست به fixture Integration وارد لایه‌های Onion محصول نشده است.

### ۳. فایل‌های ایجادشده

فایل جدیدی برای محصول یا تست ایجاد نشد. شواهد اجرا و Backup خصوصی در
`.artifacts/test-ai-knowledge-20261118-f20cfcb` نگه‌داری می‌شوند و وارد Git نمی‌شوند.

### ۴. فایل‌های تغییرکرده

فقط همین سند و `docs/product-v6/CURRENT_RELEASE_BLOCKERS_FA.md` با شاهد تازه به‌روز می‌شوند.
هر۱۲ فایل محافظت‌شده و نسخهٔ Backup آن‌ها hash یکسان دارند؛ این بار حتی private Test و
config عمومی نیز تغییر نکردند. Production، ملی‌پیامک و سه فایل unfinished AI محفوظ‌اند.

### ۵. جریان اجرا

Personal: `AiController` → `AiChatService` → `PersonalAiChatIntent` / `AiInvocationExecutor`
→ `AiServiceClient` → AI → validation پاسخ → SQL UsageLedger → HTTP200.
Knowledge: `KnowledgeIntegrationController` → `KnowledgeIntegrationService` /
`KnowledgeAuthorizationService` → `AiKnowledgeClient` → AI → کنترل Source/Version و
دفتر مصرف → پاسخ موجود. Backend session به AI ارسال نمی‌شود.

### ۶. دیتابیس

Migration، جدول، ستون، Index یا Constraint جدیدی ایجاد نشد. دیتابیس‌های عادی `MakanApp` و
`MakanApp_Test` و دیتابیس سرور تغییر نکردند؛ Migrationهای موجود فقط روی دیتابیس disposable
fixture اجرا شدند. منابع SQL/Storage موقت و فقط منبع QA ساخته‌شدهٔ همین پذیرش پاک‌سازی شدند.

### ۷. Endpointها و پذیرش واقعی

Endpoint جدیدی ساخته نشد؛ قراردادهای بخش۷ گزارش قبلی reuse شدند. در اجرای تازه:

| بررسی | نتیجه |
|---|---|
| upstream `/api/v1/health` / `/api/v1/ready` |200 /200؛ TLS معتبر بدون bypass و redirect |
| Personal `POST /api/v1/ai/chat` | Backend200 / AI200؛ modelAlias=`education-fast`؛ elapsed3281.6ms |
| قرارداد پاسخ Personal | RequestId و CorrelationId منطبق، Answer غیرخالی و Language مطابق validator موجود Backend؛ محتوا در گزارش ذخیره نشد |
| scope شخصی | role=`Personal`؛ چهار selector سازمان/مدرسه/کلاس/دانشجو تهی؛ نقش آموزشی جعلی0 |
| مصرف Personal | reservation1، consumption1، reserve ledger1، AI call1؛ replay409 و مصرف دوم0 |
| Startup میزبان `Test` | live200 / ready200؛ SQL/Storage ایزوله؛ درگاه غیرفعال |
| Knowledge Ingest / Status |202 /200 و `Ready` با poll/timeout موجود |
| Retrieve / RAG |200 /200؛ فقط SourceId/Version مجاز، grounding معتبر |
| Knowledge usage / replay | consumed1 و RAG call1؛ replay409 |
| حساب دیگر / cleanup |404 پیش از upstream؛ cleanup200 /`Deleted` فقط QA تازه؛ منابع قدیمی دست‌نخورده |

Personal از fixture موجود `Testing` و Knowledge با private Test واقعی در میزبان صریح `Test`
اجرا شدند. این شاهد از رایانهٔ توسعه به AI مستقر است؛ پذیرش IIS یا Windows Backend مقصد نیست.

### ۸. مفاهیم فنی

Availability دانش به فعال‌بودن هر دو Runtime وابسته است. Service Authentication دو سرویس
همچنان disabled است؛ CurrentKey/CurrentKeyId مقداردهی نشدند و Service JWT، Bearer Service
Token یا Authorization upstream فعال نشد. timeout35s و حدود content/response/poll ثابت‌اند.
RequestId/CorrelationId برای پیگیری کافی‌اند؛ درخواست/پاسخ خصوصی و credential ثبت نمی‌شوند.

### ۹. تصمیم طراحی

تنظیم درست موجود حفظ شد و از Probe/fixture/testهای موجود استفاده شد؛ راهکار موازی و تست
تکراری ساخته نشد. Zibal مطابق تصمیم مالک `Unavailable` ماند؛ Frontend Test هنوز ClientReturnUrl
واقعی ندارد. Callback شناخته‌شدهٔ Backend جای URL کلاینت فرض نشده و تنظیم درگاه تغییر نکرد.

### ۱۰. تست‌ها

تست تازه یا تغییر Assertion لازم نبود. `PersonalAiInvocationTests.IntentCannotSmuggleSchoolOrStudentScope`
هر چهار selector غیرتهی را رد می‌کند؛ `PersonalContextRejectsForeignIdentityAndEveryOrganizationSelector`
مرز هویت/نشست/عضویت/نقش را می‌سنجد. خطای جاری `AI_CONTEXT_NOT_ALLOWED` قبل از upstream است.

`AiV1ControlledTransportTests` قراردادهای Student/Teacher/Parent/Manager را با HTTP/serializer
واقعی و transport کنترل‌شده بررسی می‌کند؛ این Regression پذیرش زندهٔ نقش‌های سازمانی نیست.
`AiV1CommerceEndpointsTests` مالک Individual/Organization، revoke و exactly-once را می‌سنجد.

`KnowledgeForeignSourceIsConcealedBeforeHttpPinAndQuota` و
`KnowledgeCrossOrganizationAndEndedTeacherAssignmentNeverExpandScope` از دسترسی حساب/سازمان
دیگر جلوگیری می‌کنند. `KnowledgeRevocationDuringRetrievalConcealsReturnedPrivateEvidence`
revoke حین پاسخ را می‌سنجد؛ `PersonalKnowledgeCurrentSourceAndAccountStateAreRecheckedBeforeOutbound`
Archive/تعلیق را پیش از RAG رد می‌کند. `AiSourcesOperationConflictAndVersionPinningSurviveNewPublishedRevision`
منبع Archived را در Chat رد می‌کند. هر۱۵ متد پذیرشِ لازم، شامل caseهای Theory، در TRX کامل
اجرای متمرکز پیدا و Passed بودنشان تأیید شدند.

### ۱۱. Validation

| بررسی | نتیجهٔ این اجرا |
|---|---|
| Clean / Restore / Release Build |PASS /PASS /PASS؛ Warning0، Error0 |
| Build هر دو Probe |PASS؛ بدون Warning/Error |
| Real Personal / Knowledge |PASS /PASS؛ هرکدام یک اجرای کنترل‌شده |
| Focused |1547 PASS؛695 Unit +845 Integration +7 Architecture؛ Failed/Skipped=0 |
| Full regression |3461 PASS؛1377 Unit +2070 Integration +14 Architecture؛ Failed/Skipped=0 |
| Final Release Build |PASS؛ Warning0، Error0 |
| Final regression |3461 PASS؛1377 Unit +2070 Integration +14 Architecture؛ Failed/Skipped=0 |
| EF pending model changes / Migration جدید |NO /0 |
| Production AI / Knowledge configuration |UNCHANGED /UNCHANGED |
| Zibal configuration / Provider |BLOCKED_CONFIGURATION /Unavailable؛ بدون تغییر |

شمارش مرحلهٔ قبلی جای نتیجهٔ جدید قرار نگرفت. سه TRX کامل هر دور با SAX به‌صورت stream خوانده
شدند؛ caseها، counters، SHA-256 و receipt خروج۱۲ فرمان با هم تطبیق یافتند. هر۱۵ متد پذیرش
لازم در Focused و هر دو دور کامل Passed بودند. تمام۱۸۲۵ فایل tracked مبنا، به‌جز دو سند همین
مرحله، hash اولیه دارند؛ هر۱۲ فایل محافظت‌شده و Backup آن‌ها نیز byte-for-byte یکسان‌اند.
Store Billing، Push Runtime و PersonalClass Assignment/Exam همچنان Deferred و AccountDeletion
execution خاموش‌اند. تست تازه، تغییر کد، تغییر config یا Migration جدید این مرحله0 است.

```text
TEST_AI_RUNTIME=PASS
TEST_KNOWLEDGE_RUNTIME=PASS
AI_SERVICE_AUTH=DISABLED
KNOWLEDGE_SERVICE_AUTH=DISABLED
PRODUCTION_AI_CONFIG=UNCHANGED
PRODUCTION_KNOWLEDGE_CONFIG=UNCHANGED
TEST_PAYMENTS_PROVIDER=Unavailable
TEST_ZIBAL_CONFIGURATION=BLOCKED_CONFIGURATION
CURRENT_RELEASE_CODE_GAPS=0
CURRENT_RELEASE_CONFIGURATION_BLOCKERS=1
CURRENT_RELEASE_ACCEPTANCE=NOT_READY_FOR_TEST_DEPLOY
USER_INPUT_REQUIRED=Payments:Zibal:ClientReturnUrl
```

| حوزهٔ الزامی اجرای متمرکز | case موفق |
|---|---:|
| AI |404 |
| Personal AI |46 |
| Organization AI |93 |
| Usage/Quota |70 |
| Knowledge |140 |
| Files/Sources |205 |
| Configuration |121 |

یک case ممکن است در چند حوزه باشد؛ جمع سطرها Total نیست. این شمارش از TRX و نگاشت متد/فایل
Source استخراج شده است؛ تعداد مستقل برای هر Role یا اجرای زندهٔ سازمانی ادعا نمی‌شود.

### ۱۲. Git

فقط دو سند مرتبط باید stage شوند؛ private config، evidence و فایل‌های ناتمام AI خارج از
Commit هستند. پیام مقرر `docs(operations): verify test ai and knowledge runtime` است؛ branch و
upstream همان `feature/product-v6-backend-alignment` / `origin/feature/product-v6-backend-alignment`
و remote=`origin` هستند. SHA، Push و remote verification پس از validation در گزارش نهایی
ثبت می‌شوند؛ خود Commit نمی‌تواند SHA خودش را در فایل داخل Commit ثبت کند.

### ۱۳. محدودیت‌ها

زیبال تنها Configuration blocker Test است. سه پذیرش مقصد یعنی Smoke نسخهٔ نصب‌شده، اتصال
از خود Backend مقصد به AI/Knowledge و Callback واقعی Test هنوز مستقل‌اند. Production تغییر
نکرده و Deploy، Publish، main merge، Full-12 و پرداخت مالی در این Task اجرا نمی‌شوند.

### ۱۴. نکات آموزشی

1. تنظیمی که از قبل صحیح است، برای اثبات کارکرد نیاز به بازنویسی ندارد.
2. پاسخ200 باید همراه با تطبیق قرارداد، scope، citations و دفتر مصرف بررسی شود.
3. رد scope در Application پیش از شبکه نیز شاهد مؤثر حفاظت داده است؛ لایهٔ شاهد را روشن بنویسید.
4. نقش سازمانی، scope داده و مالک مالی سه مفهوم جدا هستند.
5. revoke حین retrieval نیز باید پاسخ خصوصی را پنهان کند؛ مجوز اولیه دائمی نیست.
6. پذیرش زندهٔ محلی با پذیرش مسیر شبکه و IIS مقصد یکسان نیست.

### ۱۵. مسیر مطالعه

`AiController` → `AiChatService` → `PersonalAiChatIntent` → `AiInvocationExecutor` →
`AiServiceClient.ValidateEnvelope/ValidateChatResponse` → `PersonalAiInvocationTests`؛ سپس
`KnowledgeIntegrationController` → `KnowledgeIntegrationService` → `KnowledgeAuthorizationService`
→ `AiKnowledgeClient` → `PersonalKnowledgeAcceptanceTests` / `KnowledgeIntegrationEndpointsTests`.
ابزارهای موجود `LivePersonalAiScenario` و `TestRuntimeAcceptance` شاهد زنده را به این جریان متصل می‌کنند.

### ۱۶. مرحلهٔ بعد

AI/Knowledge نیازمند secret یا Service JWT جدید نیستند. تنها ورودی باقی‌ماندهٔ Test Payment
نام `Payments:Zibal:ClientReturnUrl` است؛ مالک تأیید کرده Frontend Test هنوز آن را Deploy
نکرده است. مقدار فرضی ساخته نمی‌شود و `CURRENT_RELEASE_ACCEPTANCE=NOT_READY_FOR_TEST_DEPLOY`
تا تکمیل و validation درگاه حفظ می‌شود. پس از آن، بسته/استقرار در Task صریح خودش انجام شود.

---

تاریخ `2026-11-17`؛ مبنا `f704a5c3a0a0c808335ce05f4c8f6863e04b2235`؛ شاخه
`feature/product-v6-backend-alignment`. این گزارش پیگیری Configuration/Runtime پس از
[Audit نهایی](../product-v6/FINAL_BE01_BE25_AUDIT_FA.md) است و Feature جدید محصول نیست.

## ۱. خلاصهٔ مرحله و تصمیم فعلی

در ممیزی قبلی، AI/Knowledge خصوصی Test فعال نبودند و تنظیم Zibal کامل نبود. اکنون هر دو
Runtime با Root مصوب در فایل خصوصی Test فعال‌اند. Personal Chat واقعی200 و زنجیرهٔ
Ingest/Ready/Retrieve/RAG واقعی موفق شد. Startup در Environment=`Test` و هر دو Health200
بودند. تنظیم درگاه هنوز به یک ورودی واقعی کلاینت نیاز دارد؛ این مقدار حدس زده نشد.

| Gate | نتیجهٔ قابل‌اثبات |
|---|---|
| Zibal implementation | در Source موجود؛ نتیجهٔ Regression این اجرا در بخش۱۱ ثبت می‌شود |
| Test Zibal configuration | `BLOCKED_CONFIGURATION`؛ Provider فعلاً `Unavailable` |
| Merchant / Callback / ClientReturn / Return Allowlist فایل Test | هر چهار مقدار `MISSING`؛ فقط URL کلاینت ورودی نامعلوم است |
| Test AI BaseUrl / Knowledge BaseUrl | `CONFIGURED` / `CONFIGURED` |
| Test AI / Knowledge Enabled | `true` / `true` |
| Service Authentication هر دو سرویس | `DISABLED`؛ Header/کلید/JWT فعال نشد |
| Knowledge حدود پردازش و timeout | حفظ‌شده؛ timeout35s، poll20، interval500ms، budget60s، content10MiB، response2MiB |
| Production Zibal | `BLOCKED_CONFIGURATION` |
| Production AI / Knowledge | هر دو `OPERATIONAL_ACTIVATION_PENDING` |
| Code gaps / Test configuration blockers / Test operational validations | `0 / 1 / 3` |
| تصمیم | `NOT_READY_FOR_TEST_DEPLOY` |

قیمت/Plan/Quota تجاری واقعی ساخته نشد. Store Billing، Push و PersonalClass Academic همچنان
Deferred هستند؛ اجرای حذف حساب خاموش است. نبود تنظیم Production مانع اضافی Test نیست.

## ۲. معماری و جهت وابستگی

لایه‌های Domain/Application/Infrastructure محصول و Controllerها تغییر نکردند. فایل عمومی
Composition Root فقط پیش‌فرض‌های از قبل موجود `AiKnowledgeOptions` را صریح می‌کند؛ Runtime
پایه همچنان غیرفعال است. فایل خصوصی Test همین پیش‌فرض‌ها را برای محیط مصوب override می‌کند.

ابزار پذیرش زیر `tools/ai` از fixture موجود Integration، Controller/Application و adapter
واقعی `AiKnowledgeClient` استفاده می‌کند. این وابستگی مخصوص ابزار تست است؛ Domain به HTTP،
EF یا پروژهٔ تست وابسته نشده است. Architecture Tests جهت Onion را مستقل بررسی می‌کنند.

## ۳. فایل مهم ایجادشده

[TestRuntimeAcceptance.cs](../../tools/ai/MakanApp.KnowledgeIntegrationProbe/TestRuntimeAcceptance.cs)
حالت `--test-runtime` ابزار موجود را اجرا می‌کند. فایل پایه و private Test را می‌خواند،
Options واقعی را validate می‌کند، SQL/Storage را به منابع تازهٔ ایزوله هدایت می‌کند و همان
قراردادهای موجود HTTP را با actor شخصی می‌سنجد. محتوای AI، نشست، Secret و Header ثبت نمی‌شود.

نکتهٔ آموزشی: پذیرش adapter واقعی را می‌توان از دیتابیس عملیاتی جدا کرد؛ این شاهد جای
استقرار فیزیکی IIS یا دسترسی شبکه از خود Windows Backend Server را نمی‌گیرد.

## ۴. فایل‌های مهم تغییرکرده

| فایل | تغییر و دلیل |
|---|---|
| `MakanApp.Api/appsettings.json` | پیش‌فرض‌های غیرفعال Knowledge و حدود موجود صریح شدند تا هر دو محیط، بخش و کلیدهای مؤثر مشترک داشته باشند؛ Secret اضافه نشد |
| `MakanApp.Api/appsettings.Test.json` | فایل خصوصی و Git-ignored؛ فقط دو بخش AI/Knowledge با Root مصوب فعال شدند؛ هیچ مقدار شناخته‌شدهٔ خصوصی خالی نشد |
| `tools/ai/MakanApp.KnowledgeIntegrationProbe/Program.cs` | انتخاب حالت جدید پذیرش و reuse helperهای ساخت actor/source؛ حالت قدیمی حفظ شد |
| `tools/ai/MakanApp.KnowledgeIntegrationProbe/KnowledgeAcceptanceHandler.cs` | cleanup اختیاری فقط برای شناسه/نسخهٔ QA تازهٔ همین اجرا؛ حالت پیش‌فرض Lifecycle همچنان منجمد است |
| `tests/MakanApp.UnitTests/Knowledge/KnowledgeAcceptanceHandlerTests.cs` | پنج case برای cleanup دقیق و منع Supersede/Reindex |
| `tests/MakanApp.UnitTests/Identity/TestConfigurationSchemaTests.cs` | مقایسهٔ تنظیم مؤثر base+environment و سه case کنترل overlay؛ schema/type و OTP همچنان بررسی می‌شوند |
| `tests/MakanApp.IntegrationTests/TestEnvironmentConfigurationTests.cs` | دو case برای وجود بخش‌های لازم در هر دو profile عمومی و پیش‌فرض غیرفعال بدون Service Auth |
| اسناد Scope/Blockers/Audit، README و سرآیند Gap Matrix | جایگزینی وضعیت جاری با شاهد Runtime؛ سابقهٔ ممیزی قبلی حفظ شد |

Production خصوصی، Development، فایل‌های خصوصی IDE/Publish، adapter ملی‌پیامک و سه فایل
unfinished AI با هش اولیه حفظ شدند. Backup خصوصی۱۲ فایل نیز دست‌نخورده و قابل‌تأیید است.

## ۵. جریان واقعی اجرای قابلیت

Personal Chat با ابزار موجود `MakanApp.AiIntegrationProbe` اجرا شد:

```text
HTTP Personal Chat
-> AiController
-> AiChatService / PersonalAiChatIntent / AiInvocationExecutor
-> AiServiceClient
-> AI مستقر با Contract v1 و بدون Service Authorization
-> اعتبارسنجی پاسخ / ثبت مصرف
-> HTTP 200
```

نام کلاس‌های جریان Knowledge از Source موجود است:

```text
HTTP Ingest / Status / Retrieve / RAG
-> KnowledgeIntegrationController
-> KnowledgeIntegrationService / KnowledgeAuthorizationService
-> Source ACL / Individual quota / OperationId
-> AiKnowledgeClient
-> AI مستقر با knowledge-v1 و بدون Authorization
-> SQL Server ایزوله: وضعیت منبع و دفتر مصرف
-> پاسخ موجود HTTP
```

این ابزار API جدید محصول یا مسیر Authentication موازی نمی‌سازد. Backend user session
فقط برای فراخوانی API خود Backend به‌کار می‌رود و به سرویس AI ارسال یا در شاهد چاپ نمی‌شود.

## ۶. دیتابیس و جداسازی

Table/Column/PK/FK/Index/Constraint یا Migration جدید ایجاد نشد؛75 Migration موجود محفوظ‌اند.
مدل EF pending ندارد. تست‌ها فقط Migrationهای موجود را روی SQL Server/LocalDB موقت fixture
اعمال می‌کنند. روی `MakanApp`، `MakanApp_Test`، Production یا سرور، Migration اجرا نشد.

در پذیرش Knowledge، connection پیش از اجرا باید prefix اختصاصی `MakanApp_KnowledgeProbe_`
داشته باشد. فقط connection و StorageRoot میزبان Test به منابع موقت همان fixture override
شدند؛ فایل خصوصی تغییری برای SQL/Storage نکرد. منابع محلی پس از اجرا dispose شدند.

پایگاه مقصد و Storage خصوصی مصوب محفوظ‌اند: Test از Production جداست. فایل خصوصی Production
byte-for-byte یکسان ماند؛ هیچ credential آن وارد Git نشد.

## ۷. Endpointهای استفاده‌شده

هیچ Endpoint یا Contract جدید ایجاد نشده است. جدول، مسیرهایی است که در این پذیرش استفاده شدند:

| Method / Route | ورودی و پاسخ | مجوز / خطای مهم |
|---|---|---|
| `GET /health/live` و `/health/ready` | بدون payload؛200 در میزبان ایزوله | readiness محلی، شاهد IIS مقصد نیست |
| `POST /api/v1/ai/chat` | درخواست Personal موجود؛ پاسخ200؛ محتوای خصوصی در شاهد ثبت نمی‌شود | actor حساب عادی، scope سازمان/مدرسه/کلاس/دانشجو تهی؛ replay همان operation برابر409 |
| `POST /api/v1/sources/{id}/versions/{version}/knowledge/ingest` | mutation با OperationId و Version؛202 | مالک مجاز، نسخه و concurrency موجود |
| `GET /api/v1/sources/{id}/versions/{version}/knowledge/status?wait=true` | وضعیت200 و `Ready` | همان Source ACL؛ poll budget موجود حفظ شد |
| `POST /api/v1/ai/knowledge/retrieve` | query و allowlist صریح Source/Version؛200 | source حساب دیگر404 و تماس upstream صفر |
| `POST /api/v1/ai/knowledge/rag/answer` | query شخصی؛200 و citations همان Source/Version | replay409؛ یک مصرف و یک تماس RAG |
| `POST /api/v1/sources/{id}/versions/{version}/knowledge/delete` | OperationId و Version فعلی؛200 و `Deleted` | فقط QA تازهٔ همین اجرا؛ منابع قدیمی دست‌نخورده‌اند |

Callback رسمی Zibal همان `/api/integrations/v1/commerce/payments/zibal/callback` است؛ تماس
ورودی واقعی از درگاه یا پرداخت مالی در این Task انجام نشد.

## ۸. مفاهیم فنی استفاده‌شده

- تنظیم مؤثر از فایل پایه و override محیط ساخته می‌شود؛ نبود کلید در raw Production ممکن است
  با ارث‌بری مقدار معتبر پایه جبران شود. مقایسهٔ صرف دو فایل override شاهد Runtime نیست.
- Options واقعی همراه با Environment اعتبارسنجی می‌شوند؛ Test Merchant قرارداد مخصوص خود
  را دارد و Production policy مستقل می‌ماند.
- source allowlist، ACL جاری و scope شخصی با هم بررسی می‌شوند؛ داشتن SourceId مجوز نیست.
- UsageReservation/Ledger و OperationId مانع مصرف مجدد پس از replay می‌شوند؛ پاسخ خصوصی
  ذخیره نمی‌شود و رفتار مصوب409 حفظ می‌گردد.
- در ابزار پذیرش، cleanup با شناسه/نسخهٔ تازهٔ صادرشده از Backend محدود می‌شود؛ allowlist
  از ورودی CLI یا فایل شناسه‌های قدیمی گرفته نمی‌شود.

## ۹. تصمیم‌های طراحی و Trade-off

Runtime Test باید واقعاً فعال باشد، اما Source عمومی نباید وابسته به credential یا فعال‌سازی
سرور شود. بنابراین تنظیم خصوصی Test فعال و پیش‌فرض عمومی/Production غیرفعال حفظ شد. هزینهٔ
این تصمیم، لزوم انتقال تنظیم خصوصی مناسب توسط اپراتور هنگام Publish/Deploy است.

پذیرش از Host واقعی ASP.NET Core در TestServer و adapter HTTP واقعی استفاده می‌کند؛ SQL،
Storage و دیگر providerهای fixture کنترل‌شده‌اند. این روش بدون دستکاری دادهٔ مشتری Contract
و Runtime را می‌سنجد، ولی مسیر شبکه/ACL/AppPool سرور مقصد را اثبات نمی‌کند.

Merchant و BaseUrl تست Zibal قرارداد مشخص دارند و Backend Origin نیز در سند استقرار موجود
است. URL برگشت کلاینت واقعی پیدا نشد؛ فعال‌سازی یک‌قسمتی درگاه startup را نامعتبر می‌کند.
در نتیجه Provider تا تکمیل مجموعهٔ معتبر `Unavailable` ماند؛ URL حدسی یا callback اشتباه
جایگزین مسیر Frontend نشد. تنها ورودی لازم در بخش پایانی آمده است.

## ۱۰. تست‌های اضافه‌شده و شاهد واقعی

پنج Unit case مربوط به transport guard ثابت می‌کنند cleanup فقط برای SourceId/Version دقیق
مجاز است و این مجوز، Supersede/Reindex را باز نمی‌کند. تست پیشین Authorization header و
مسدودبودن Lifecycle پیش‌فرض حفظ شده‌اند؛ Service Auth دوباره فعال نشده است.

سه Unit case overlay، حفظ کلیدهای ارث‌رسیده، override مقدار محیط و تشخیص کلید اضافی/نوع
اشتباه را می‌سنجند. تست مقایسهٔ Runtime از raw override به تنظیم مؤثر تغییر کرد؛ تست دقیق
schema الگوهای عمومی، duplicate keys و guard OTP حذف یا سست نشد.

دو Integration case تنظیمات، profileهای عمومی هر دو محیط را با Configuration واقعی bind
می‌کنند و بخش‌ها/کلیدها، contract و وضعیت امن پیش‌فرض را می‌سنجند. CI به فایل خصوصی وابسته
نمی‌شود. Architecture case جدیدی لازم نبود؛ مجموعهٔ کامل موجود اجرا می‌شود.

پذیرش زندهٔ این Task جدا از تست‌های fake transport است:

| مرحله | شاهد امن |
|---|---|
| Personal Chat | Backend200 / AI200، modelAlias=`education-fast`، elapsed3540.4ms |
| Personal scope | `ordinaryAccount=true`؛ چهار scope سازمانی تهی، payer=`Individual` |
| Personal exactly-once | reservation1، consumption1، reserve ledger1، AI call1؛ replay409 |
| Test startup | Environment=`Test`، live200 و ready200؛ Zibal هنوز غیرفعال |
| Ingest / Status |202 /200 و `Ready`؛ تنها متن مصنوعی QA |
| Retrieve |200؛ allowlist دقیق، elapsed299.5721ms |
| RAG |200؛ claims/citations همان منبع، elapsed1770.4665ms؛ consumed1 و RAG call1 |
| Cross-user / Retry | حساب دوم404 پیش از upstream؛ replay409 و مصرف دوم صفر |
| Cleanup |200 /`Deleted`؛ فقط QA تازه؛ oldResourcesTouched=false |

Personal Chat با fixture موجود Environment=`Testing` و Knowledge با حالت جدید صریح
Environment=`Test` اجرا شدند؛ هیچ‌کدام نصب IIS نیستند. requestId/correlationId/elapsed امن
در evidence خصوصی موجود است؛ درخواست/پاسخ AI، token، Header و دادهٔ خصوصی ثبت نشده‌اند.

## ۱۱. Validation

تمام فرمان‌های این جدول واقعاً اجرا شدند؛ receipt خروج۱۳ فرمان، شمارش caseها و SHA-256
سه TRX کامل هر دور بررسی شدند. شمارش ممیزی قبلی جای نتیجهٔ اجرای جاری قرار نگرفته است.

| فرمان / بررسی | نتیجه |
|---|---|
| Clean / Restore | `PASS / PASS` |
| Release Build پس از تغییرها | `PASS`؛ Warning0، Error0 |
| Focused |1408 PASS؛671 Unit +730 Integration +7 Architecture؛ Failed/Skipped=0 |
| Full regression |3461 PASS؛1377 Unit +2070 Integration +14 Architecture؛ Failed/Skipped=0 |
| Final Release Build | `PASS`؛ Warning0، Error0 |
| Final full regression |3461 PASS؛1377 Unit +2070 Integration +14 Architecture؛ Failed/Skipped=0 |
| Zibal در هر دور کامل |117 PASS؛68 Unit +49 Integration؛ پرداخت مالی `NOT_RUN` |
| Realtime در هر دور کامل |82 case مرتبط PASS؛ failure/timeout مشاهده‌شده0 |
| EF pending model changes | `NO` |
| Migration جدید / اجرای Migration دیتابیس عادی | `0 / NOT_RUN` |

نسبت به baseline، هشت Unit و دو Integration اضافه شدند؛ مجموع از3451 به3461 رسید. نتیجهٔ
هر دو دور کامل یکسان است. این شاهد علت خطاهای تاریخی Realtime را حل‌شده اعلام نمی‌کند.

دو شروع متمرکز اولیه به دلیل فرض قدیمی raw schema شکست خوردند و receipt کامل Integration
نداشتند؛ PASS حساب نشدند. مقایسهٔ تنظیم مؤثر اصلاح شد؛ خروجی ناقص و بررسی آن محفوظ است. runner خصوصی
نیز اکنون stderr ابزار را به‌جای قطع میانی، همراه exit code نهایی ثبت می‌کند. هیچ تستی
disable نشد و timeout/retry محصول برای سبزشدن تغییر نکرد.

مسیر evidence خصوصی و Git-ignored: `.artifacts/test-runtime-20261117-f704a5c`.
TRX کامل، counters/caseها، exit receipt، hash و وضعیت فایل‌های محافظت‌شده آنجا نگه‌داری می‌شوند.

| حوزهٔ الزامی در Focused | case موفق |
|---|---:|
| Zibal |117 |
| Commerce |194 |
| AI |404 |
| Knowledge |140 |
| Configuration |121 |
| Operations |184 |

یک case می‌تواند در چند حوزه باشد؛ جمع سطرها Total نیست. این شمارش از TRX کامل همین فرمان
متمرکز و نام متد/فایل Source استخراج شده است.

فرمان‌های Validation از ریشهٔ مخزن، با اجرای ترتیبی و بدون testhost هم‌زمان:

```powershell
dotnet clean MakanApp.sln --configuration Release --nologo
dotnet restore MakanApp.sln --nologo
dotnet build MakanApp.sln --configuration Release --no-restore --nologo
dotnet test MakanApp.sln --configuration Release --no-build --no-restore --nologo
# پس از موفقیت، Build و Test کامل نهایی را نیز اجرا کنید.
dotnet ef migrations has-pending-model-changes --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --configuration Release --no-build --no-color
```

برای تکرار کنترل‌شدهٔ پذیرش Knowledge، فقط پس از تأیید مقصد و با private Test آماده، از
ریشهٔ مخزن اجرا شود. این فرمان منبع QA تازه روی سرویس مصوب می‌سازد و فقط همان را پاک می‌کند؛
فایل تنظیمات یا مقدار Secret را در کنسول نمایش ندهید:

```powershell
dotnet build tools/ai/MakanApp.KnowledgeIntegrationProbe/MakanApp.KnowledgeIntegrationProbe.csproj --configuration Release --nologo
$testSettings = Get-Content MakanApp.Api/appsettings.Test.json -Raw | ConvertFrom-Json
$previousApprovedOrigin = $env:AiLiveProbe__ApprovedOrigin
try {
    $env:AiLiveProbe__ApprovedOrigin = $testSettings.AiKnowledge.BaseUrl
    dotnet run --project tools/ai/MakanApp.KnowledgeIntegrationProbe/MakanApp.KnowledgeIntegrationProbe.csproj --configuration Release --no-build -- --run-live --allow-remote-https --test-runtime
}
finally {
    $env:AiLiveProbe__ApprovedOrigin = $previousApprovedOrigin
}
```

این فرمان smoke محلی است؛ به‌جای پذیرش عملیاتی Backend مقصد گزارش نشود. تکرار خودکار،
افزایش timeout یا loop برای تبدیل شکست به PASS بخشی از این Runbook نیست.

## ۱۲. Git

مبنا=`f704a5c3a0a0c808335ce05f4c8f6863e04b2235`؛ branch/upstream=
`feature/product-v6-backend-alignment` / `origin/feature/product-v6-backend-alignment`؛ remote=`origin`.
تنها config عمومی امن، تست‌ها، ابزار پذیرش کامل و اسناد مرتبط برای Commit انتخاب می‌شوند.
private Test/Production، evidence و سه فایل AI ناتمام وارد Commit نمی‌شوند. پیام در نظرگرفته‌شده:
`test(operations): validate Test AI and Knowledge runtime`.
SHA نهایی، نتیجهٔ Push و تطبیق HEAD/remote در گزارش نهایی و receipt خصوصی ثبت خواهد شد؛
سند داخل Commit نمی‌تواند hash خودش را در خود ثبت کند. Push فقط پس از Build/Test موفق مجاز است.

## ۱۳. محدودیت‌های فعلی

تنظیم Zibal کامل نیست و پرداخت واقعی اجرا نشد؛ `READY_FOR_TEST_DEPLOY` ادعا نمی‌شود.
سه پذیرش مقصد یعنی نصب/Smoke Test، اتصال همان Backend به AI/Knowledge و Callback درگاه
هنوز اجرا نشده‌اند. چهار gate Production یعنی Ring/Restart، Storage/ACL، Restore drill
و observability جدا هستند. Production config و سرویس سرور تغییر نکردند.

Store Billing/Push Runtime/PersonalClass Assignment/Exam Deferred، Library عمومی/AI طبق
سیاست خاموش و AccountDeletion execution خاموش‌اند. قیمت/سهمیه/Retention حدسی ایجاد نشد.
SMS واقعی، Full-12، تغییر Service Auth، merge به main، Publish و Deploy انجام نشدند.

## ۱۴. نکات آموزشی

1. سلامت implementation، کامل‌بودن configuration و پذیرش live سه شاهد مستقل‌اند.
2. برابری schema را برای تنظیم مؤثر بررسی کنید؛ ارث‌بری فایل پایه بخشی از رفتار میزبان است.
3. HTTP200 کافی نیست؛ scope، citations، payer و دفتر مصرف نیز باید بررسی شوند.
4. retry بعد از موفقیت باید اثر تجاری دوم نسازد؛409 مصوب نیز نتیجهٔ idempotent معتبر است.
5. cleanup پذیرش زنده باید به دادهٔ ساخته‌شدهٔ همان اجرا محدود شود.
6. شکست یا قطع یک اجرای تست را با خروجی ناقص PASS گزارش نکنید؛ exit و TRX کامل لازم‌اند.
7. Secret و unfinished work با backup/hash محافظت می‌شوند و نباید همراه docs/test stage شوند.

## ۱۵. مسیر پیشنهادی مطالعهٔ کد

ابتدا `appsettings.json` و Options واقعی `AiServiceOptions`، `AiKnowledgeOptions` و
`ZibalOptions`؛ سپس `TestEnvironmentConfigurationTests` و `TestConfigurationSchemaTests`.
برای پذیرش، `TestRuntimeAcceptance` → `KnowledgeAcceptanceHandler` → `KnowledgeIntegrationController`
→ `KnowledgeIntegrationService` → `IAiKnowledgeClient` → `AiKnowledgeClient` → persistence موجود
Source/Usage → تست‌های `KnowledgeAcceptanceHandlerTests` و Knowledge Integration را بخوانید.
این مرحله EF configuration یا Migration تازه ندارد.

## ۱۶. مرحلهٔ بعد

پس از دریافت URL واقعی بازگشت، تنظیم کامل Zibal Test باید یکجا اعمال و با Validator واقعی
بررسی شود. Origin allowlist از همان URL و Callback از Origin مستند Backend ساخته می‌شود؛
credential لازم نیست در چت یا Git قرار گیرد. سپس gate آمادگی دوباره محاسبه و ساخت بسته/
استقرار Test در Task صریح اجرا شود. Production activation و قابلیت‌های Deferred وارد این
Task نمی‌شوند.

## USER_INPUT_REQUIRED

`Payments:Zibal:ClientReturnUrl`
