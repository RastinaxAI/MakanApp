# AI نامحدود و پرداخت Deferred در نسخهٔ جاری

> تکمیل Adaptive v1 از پایهٔ `95cef1b`: [قرارداد جاری](ADAPTIVE_AI_FRONTEND_HANDOFF_FA.md)
> Profile اختیاری، History خصوصی و SSE را اضافه می‌کند و policy تجاری این سند را تغییر
> نمی‌دهد. دفتر عملیات پاسخ را cache نمی‌کند؛ **History مستقل و فقط زوج کامل** اکنون متن
> مجاز را برای ادامهٔ Conversation نگه می‌دارد. بنابراین «پاسخ خصوصی ذخیره نمی‌شود» در
> بخش تاریخی زیر فقط دربارهٔ receipt/ledger است. Account Deletion موجود History را پاک
> می‌کند؛ retention خودکار نیازمند `PRODUCT_RETENTION_DECISION_REQUIRED` است.

تاریخ تصمیم مالک: `2026-11-18`؛ Source پایه
`d397d73c5f532f7492614e95925ceaa1850a8150`؛ شاخه `feature/product-v6-backend-alignment`.
این تصمیم بر توضیحات تجاریِ انتشار جاری در اسناد قدیمی اولویت دارد؛ کد و دادهٔ Commercial حذف نمی‌شوند.

## ۱. خلاصهٔ مرحله

پیش‌تر AI به Subscription، Entitlement.Ai، AiTextRequests یا FreeTrial صریح با سقف۳ در روز
نیاز داشت؛ policy محلی ai-chat هم۲۰ درخواست در دقیقه بود. اکنون `AiUsage:Mode=Unmetered`
فقط این محدودیت‌های تجاری/تعدادی را برمی‌دارد. هر حساب فعال با نشست و Context مجاز می‌تواند
از AI استفاده کند. پرداخت‌های Runtime این نسخه Deferred و Provider=`Unavailable` است.

## ۲. معماری

Domain معنای عملیات و حسابداری را نگه می‌دارد. Application سیاست استفاده، مجوز داده و
اجرای use case را هماهنگ می‌کند. Infrastructure همان SQL Reservation/Ledger و adapter
بالادستی را به کار می‌گیرد. Api فقط Composition، OpenAPI و policy named rate limiter را
تغییر می‌دهد؛ هیچ شرط تجاری در Controller اضافه نشده است. Dependency همچنان
`Api → Application → Domain` و `Infrastructure → Application/Domain` است.

## ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت / دلیل / نکتهٔ آموزشی |
|---|---|
| `MakanApp.Application/Ai/AiUsageOptions.cs` | Mode صریح و canonical کردن Payer؛ پیش‌فرض کد Commercial است، انتخاب تنظیمات با مجوز داده یکی نیست |
| `MakanApp.Domain/Commerce/UsageAccountingMode.cs` | تفکیک Commercial، FreeTrial و Unmetered؛ رکورد عملیات نامحدود نباید Trial یا برداشت سهمیه نامیده شود |
| `MakanApp.Infrastructure/Persistence/Migrations/20261118065656_AddUnmeteredAiOperations.cs` | تغییر افزایشیِ لازم برای بیان صریح رکورد بدون Subscription؛ حفظ معنای تاریخچه مهم‌تر از پرهیز ظاهری از Migration است |
| `tests/MakanApp.IntegrationTests/UnmeteredAiAcceptanceTests.cs` | HTTP/SQL واقعی،۲۵ عملیات، سقف واقعی۲۰، payer/replay/هم‌زمانی/خطا و سازگاری Commercial |
| `tests/MakanApp.IntegrationTests/UnmeteredAiRoleAcceptanceTests.cs` | چهار نقش سازمانی بدون اشتراک، لغو عضویت، کلاس بیگانه و دانش‌آموز فاقد Enrollment |
| `tests/MakanApp.IntegrationTests/UnmeteredKnowledgeAcceptanceTests.cs` | RAG بدون اشتراک فعال، ACL مالک، Archive و بازبینی مجوز پس از Retrieve |
| `tests/MakanApp.IntegrationTests/UnmeteredAiMigrationTests.cs` | Fresh/Upgrade روی LocalDB تازه، constraint، یکتایی و محافظ rollback تاریخچه |
| `tests/MakanApp.IntegrationTests/MakanAppWebApplicationFactory.AiUsage.cs` | میزبان مستقل برای Mode و Environment=Test؛ توکن fixture/Provider کنترل‌شده reuse می‌شوند، SQL واقعی است |
| `tests/MakanApp.UnitTests/Ai/AiUsagePolicyTests.cs` | معنای Mode/Payer و قواعد خالص عملیات پایدار |

## ۴. فایل‌های مهم تغییرکرده

- `CommercialSubscriptionService`: ابتدا حساب/نشست/Context و مالکیت کلاس را resolve می‌کند؛
  سپس در Unmetered شرط تجاری را کنار می‌گذارد. مدیریت سایر قراردادها و ظرفیت‌ها حفظ شده است.
- `AiInvocationExecutor` و `KnowledgeAuthorizationService`: Payer معتبر در Unmetered اثر
  تجاری ندارد؛ Context، Scope، Fingerprint، Pin و reauthorization همچنان اجرا می‌شوند.
- `EfCommercialStore.Reservations.cs`: در همان transaction و lock مالک، عملیات نامحدود ثبت
  می‌شود؛ Preflight=`Allowed:true,Remaining:null`؛ عملیات Unmetered در Trial آینده شمرده نمی‌شود.
- `UsageReservation`، `UsageLedgerEntry.Reservations` و دو EF configuration: علامت صریح
  حسابداری، قیود و نگاشت DTO؛ unique ConsumerUserId/OperationId و rowversion قبلی حفظ‌اند.
- `Program.cs`: فقط policy `ai-chat` با Mode نامحدود `NoLimiter` می‌شود؛ محدودیت OTP، فایل و
  Admin تغییر نمی‌کند. Controllerها و `[Authorize]` حفظ شده‌اند.
- `AiInvocationOperationTransformer` و `KnowledgeContractTransformer`: توضیح Mode و Payer
  بدون شرط پرداخت/Remaining؛ خطای واقعی Upstream همچنان خطاست.
- base و دو template عمومی appsettings: بخش جدید Unmetered؛ private Test/Production فقط همین
  بخش را می‌گیرند. Secret، OTP، DB، Storage، SMS، timeout، AI Enabled و Service Auth دست‌نخورده‌اند.
- fixture اصلی Integration صریحاً Commercial است تا Regression قدیمی واقعاً همان رفتار را
  بسنجد. دو Probe موجود برای پذیرش Mode واقعی گسترش یافته‌اند؛ ابزار موازی ساخته نشده است.

## ۵. جریان اجرا

`POST /api/v1/ai/chat` → `AiController` → `AiChatService` → `PersonalAiChatIntent` یا Context
سازمانی مجاز → `AiInvocationExecutor` → `CommercialSubscriptionService` →
`EfCommercialStore.ReserveAsync` → SQL Server/قفل و unique OperationId → commit رزرو →
`AiServiceClient` → validation پاسخ → Finalize در SQL → نتیجه → HTTP.

Knowledge: `KnowledgeIntegrationController` → `KnowledgeIntegrationService` →
`KnowledgeAuthorizationService` / SourceVersion دقیق و ACL → همان reserve/finalize برای RAG
→ `AiKnowledgeClient` → بازبینی مجدد منبع و Citation → پاسخ موجود. Retrieve به‌تنهایی مصرف
تولید متن ثبت نمی‌کند. هیچ تماس طولانی شبکه داخل transaction رزرو نگه داشته نمی‌شود.

## ۶. دیتابیس و Migration

Migration جدید فقط `IsUnmetered bit NOT NULL DEFAULT 0` را به `commerce.UsageReservations`
و `commerce.UsageLedger` اضافه می‌کند؛ مقدار پیش‌فرض برای حفظ معنای **داده‌های قبلی** است.
قید رزرو، سه حالت مجزا را می‌پذیرد؛ Unmetered باید Individual=Consumer، IsTrial=false،
Subscription/PlanVersion=null، QuotaKey=AiTextRequests و Amount=1 داشته باشد. قید دفتر
هم Reservation معتبر و همین معنای Unmetered را لازم می‌داند. PK/FK و uniqueها حذف نشده‌اند.

`Amount=1` شمارندهٔ یک عملیات است؛ برداشت Quota نیست. `AccountingMode` محاسبه‌شده در
Domain و DTO صریح است. Owner فردی در این حالت مالک رکورد عملیات است؛ پرداخت‌کنندهٔ مالی
نیست؛ ContextId سازمانی همچنان از مجوز واقعی می‌آید. ستون‌های قدیمی بازه برای سازگاری
حفظ‌اند: از زمان ایجاد تا MaxValue به‌عنوان پنجرهٔ بی‌انقضای **عملیات**، نه دورهٔ اشتراک.
Preflight هیچ PeriodEnd مالی یا Remaining عددی نمی‌سازد.

Up دادهٔ قبلی را تغییر/حذف نمی‌کند و rowversion آن را حفظ می‌کند. Down وقتی رکورد نامحدود
وجود دارد با `51000` رد می‌شود تا معنای تاریخچه از دست نرود. پذیرش فقط روی دیتابیس‌های
GUIDدار `MakanApp_UnmeteredMigration_*` و fixture ایزوله اجرا می‌شود؛ دیتابیس عادی یا سرور
در این Task تغییر نمی‌کند. قبل از استقرار آینده باید این Migration با workflow مصوب اعمال شود.

## ۷. Endpoint و قرارداد Frontend/Admin

Route یا Controller تازه‌ای ایجاد نشده است.

| Method / Route | ورودی و نتیجه | اعتبارسنجی / مجوز / خطا |
|---|---|---|
| `POST /api/v1/ai/chat` | SendAiChatCommand؛ AiChatResult200 | نشست/حساب/Context معتبر، اندازهٔ پیام؛ OperationId ثابت برای Retry؛ Payer enum اختیاری و بدون اثر تجاری |
| مسیرهای نقش موجود `students/analyze`، `teachers/classes/{classId}/summary`، `parents/child-summary`، `managers/school-summary` | DTO موجود نقش؛200 | عضویت/نقش/Enrollment/TeacherAssignment/Guardian جاری؛ ID مجوز نیست؛ خطای Context پیش از Upstream |
| `POST /api/v1/commerce/ai/simple-text/preflight` | AiQuotaPreflightCommand؛ Allowed=true، Remaining=null، UsageMode=Unmetered | حتی Preflight به نشست و Context یا عضویت PersonalClass نیاز دارد؛ مجوز محتوای آموزشی اعطا نمی‌کند |
| `POST /api/v1/ai/knowledge/retrieve` | KnowledgeQueryCommand و SourceVersion دقیق؛ پاسخ Retrieve موجود | منبع خصوصی/سازمان/کلاس/Archive/Revoke و Library policy حفظ؛ منبع بیگانه404 |
| `POST /api/v1/ai/knowledge/rag/answer` | همان Query و ClientOperationId؛ پاسخ grounded موجود | body.Payer فقط enum؛ replay تکمیل‌شده409؛ منبعِ مالکِ آرشیوشده409 NotReady؛ محتوای ممنوع برنمی‌گردد |
| مسیرهای موجود `/sources/{id}/versions/{version}/knowledge/*` | Ingest/Status/Delete و DTO قبلی | مجوز writer، نسخهٔ جاری، وضعیت/Safety فایل، حد اندازه و budget polling حفظ |

Frontend: AI نیازمند Login است؛ خرید اشتراک، انتخاب Payer، شمارندهٔ Remaining و UI تجاری
QuotaExceeded در این نسخه استفاده نشوند. `X-AI-Payer` و body.Payer معتبر برای compatibility
پذیرفته می‌شوند؛ Payer نامعتبر رد می‌شود. Upstream429 به معنی محدودیت واقعی سرویس است و
باید پیام خطای استاندارد نشان داده شود؛ Backend آن را موفق یا Retry خودکار نمی‌کند.

Purchase/Renew/Upgrade/Downgrade نیازمند پرداخت/Pay/Zibal redirect از flow قابل‌استفاده پنهان
شوند. Order/Payment/Outbox/Fulfillment و School Application حفظ‌اند؛ جریان نیازمند پرداخت
Deferred است. هیچ Free Provisioning، Merchant یا URL فرضی ساخته نمی‌شود. تنظیمات کامل
پرداخت و ClientReturnUrl واقعی فقط برای انتشار آینده لازم‌اند.

## ۸. مفاهیم استفاده‌شده

Options و Mode صریح؛ تفکیک Authorization از مجوز تجاری؛ canonical کردن ورودی سازگار؛
transaction کوتاه/lock/unique و rowversion؛ durable intent، replay و outcome نامعلوم؛
قیود SQL؛ Provider کنترل‌شده در Integration و Smoke محدود واقعی؛ طبقه‌بندی Scope انتشار.

## ۹. تصمیم‌ها و محدودیت تضمین

حذف Reservation یا نام‌گذاری دروغین FreeTrial کنار گذاشته شد؛ دو علامت افزایشی، معنای دادهٔ
تاریخی و عملیات نامحدود را جدا می‌کنند. هزینه، یک Migration و دو ستون است؛ زیرساخت موازی
ساخته نمی‌شود. Enum پیش‌فرض Commercial است، ولی تنظیم این Release صریحاً Unmetered است.
برگشت به Commercial همان Trial۳/روز، Entitlement/Quota و limiter۲۰/دقیقه را فعال می‌کند.

یک Consumer/OperationId در SQL یک intent دارد؛ payload متفاوت409؛ duplicate هم‌زمان فقط
یک بار Provider را فراخوانی می‌کند. Consumed/Reserved/Unknown/Released حفظ شده‌اند؛ Timeout
پس از شروع، Unknown و Retry بعدی409 است. این تضمین، ادعای exactly-once سراسری شبکه نیست:
در قطع مبهم، دوباره‌فرستادن خودکار ممنوع است و recovery موجود نتیجه را تعیین می‌کند.
پاسخ خصوصی AI ذخیره نمی‌شود؛ replay موفق قبلی409 AI_OPERATION_ALREADY_COMPLETED است.

## ۱۰. تست‌ها

Unit قواعد خالص Domain/Payer را می‌سنجد. Integration از HTTP و SQL Server واقعی با Provider
کنترل‌شده استفاده می‌کند؛ Environment=Test سقف واقعی۲۰ را دارد، نه استثنای۱۰۰۰۰ fixture
Testing.۲۵ عملیات نامحدود از آن عبور می‌کنند و Commercial درخواست۲۱ را429 می‌کند.۲۰
duplicate هم‌زمان یک پاسخ موفق/یک invocation دارند؛ خطای upstream429 و Timeout/ResponseInvalid
به ترتیب Released/Unknown می‌شوند و هیچ‌کدام پاسخ موفق یا Retry نمی‌سازند.

چهار نقش بدون اشتراک، لغو عضویت و کلاس/دانش‌آموز غیرمجاز آزموده می‌شوند. Knowledge بدون
اشتراک فعال با ACL، Archive پس از Retrieval و replay آزموده می‌شود. Migration دو حالت Fresh
و Upgrade را دارد. Regression تجاری/زیبال و۱۴ Architecture test مستقل حفظ‌اند.

## ۱۱. Validation

شواهد خصوصی و Git-ignored: `.artifacts/ai-unmetered-20261118-d397d73`؛ مقدار Secret، JWT،
Authorization، درخواست یا پاسخ خصوصی در Evidence منتشرشده وجود ندارد.

| بررسی اجراشده | نتیجه |
|---|---|
| clean / restore / Release build | موفق؛ Build با صفر Warning و Error |
| Build هر دو Probe | موفق؛ صفر Warning و Error |
| Focused |۷۴۱ Unit +۹۱۰ Integration +۷ Architecture =۱۶۵۸ موفق؛ Failed/Skipped=0 |
| تست‌های جدید policy |۳۷ case موفق؛ شامل Fresh/Upgrade Migration و مقایسهٔ limiter واقعی Commercial/Unmetered |
| Zibal در Focused |۱۱۷ case موفق؛ بدون پرداخت زنده یا تغییر Provider |
| Snapshot |۴۳۸ path /۴۹۴ operation /۷۸۵ schema /۹۱ Controller /۱۵۱ جدول /۷۶ Migration؛ DatabaseConnected=false |
| EF pending model |هیچ تغییرِ بدون Migration وجود ندارد |
| فایل‌های محافظت‌شده |هر۱۲ Backup معتبر؛۹ فایل byte-identical و۳ config فقط با تفاوت مجاز AiUsage؛ سایر trackedها محفوظ |
| Full |۱۳۸۶ Unit +۲۰۹۸ Integration +۱۴ Architecture =۳۴۹۸ موفق؛ Failed/Skipped=0؛۱۱۷ Zibal و۸۲ Realtime موفق |
| Final Build |موفق؛ صفر Warning و Error |
| Final Test |۱۳۸۶ Unit +۲۰۹۸ Integration +۱۴ Architecture =۳۴۹۸ موفق؛ Failed/Skipped=0؛۱۱۷ Zibal و۸۲ Realtime موفق |

Personal Backend→AI واقعی:200، modelAlias=`education-fast`، elapsed=`3432.2ms`،
requestId=`bea4c3d636e34595a75bf1bf31d95c3a`،
correlationId=`personal-live-08219f80b9304f0f9acabbd156618cfb`؛ یک تماس، یک رزرو و یک
ثبت نتیجهٔ Unmetered، بدون Subscription/Order؛ replay409 و بدون تماس دوم.

Knowledge واقعی در Environment=Test: Health/Ready200، Ingest202، Ready/Retrieve/RAG200،
replay409، یک Generation و یک نتیجهٔ Unmetered بدون اشتراک فعال؛ حساب دیگر404 و صفر تماس
خارجی؛ فقط QA تازهٔ همین اجرا پاک‌سازی شد. AI و Knowledge Service Auth هر دو false و timeout35s
حفظ شدند. آزمون۲۵ درخواست و۲۰ duplicate از Provider کنترل‌شده استفاده کرد، نه سرویس واقعی.

در دور اولیهٔ تست جدید، دو انتظار404 برای Archive با Contract موجود409 NotReady ناسازگار
بودند؛ انتظار و بررسی ErrorCode اصلاح شد، guard امنیتی یا Contract محصول تغییر نکرد.

نتیجهٔ دو TRX کامل و ExitCodeهای واقعی با هم تطبیق داده شدند. هر دو دور،۳۷ تست جدید را
اجرا کردند؛۱۴ Architecture موجود حفظ‌اند. Migration جدید در دو سناریوی Fresh/Upgrade
روی SQL disposable پذیرفته شد و pending model وجود ندارد. پذیرش حاضر آمادگی نصب Test
است؛ ادعای نصب، خرید بانکی یا پذیرش Windows Backend مقصد نیست.

```text
CURRENT_RELEASE_PAYMENTS=DEFERRED_CURRENT_RELEASE
TEST_PAYMENTS_PROVIDER=Unavailable
TEST_ZIBAL_CONFIGURATION=NOT_REQUIRED_CURRENT_RELEASE
AI_USAGE_MODE=UNMETERED
AI_PAYMENT_REQUIRED=NO
AI_SUBSCRIPTION_REQUIRED=NO
AI_ENTITLEMENT_REQUIRED=NO
AI_TEXT_REQUEST_QUOTA=UNLIMITED
AI_FREE_TRIAL_DAILY_LIMIT=NOT_APPLICABLE
AI_LOCAL_HTTP_RATE_LIMIT=DISABLED
TEST_AI_RUNTIME=PASS
TEST_KNOWLEDGE_RUNTIME=PASS
AI_SERVICE_AUTH=DISABLED
KNOWLEDGE_SERVICE_AUTH=DISABLED
MIGRATIONS_CREATED=1
MIGRATION_ACCEPTANCE=PASS
PENDING_MODEL_CHANGES=NO
CURRENT_RELEASE_CODE_GAPS=0
CURRENT_RELEASE_CONFIGURATION_BLOCKERS=0
CURRENT_RELEASE_ACCEPTANCE=READY_FOR_TEST_DEPLOY
USER_INPUT_REQUIRED=NONE
MERGE_TO_MAIN=NOT_RUN
TEST_DEPLOYMENT=NOT_RUN
PRODUCTION_DEPLOYMENT=NOT_RUN
```

## ۱۲. Git

Commit پیاده‌سازی: `c53c67db7681e6ad652df4c8ce328c883ef2bef0` با پیام
`feat(ai): enable unmetered current-release ai access`؛۳۱ فایل کد/تست/تنظیم عمومی/ابزار،
پس از موفقیت هر دو دور کامل ثبت شدند. این گزارش و هفت سند مرتبط در Commit جداگانه با پیام
`docs(product-v6): defer payments and finalize free ai release policy` نگه‌داری می‌شوند.

شاخه=`feature/product-v6-backend-alignment`، remote=`origin` و
upstream=`origin/feature/product-v6-backend-alignment` است. private Test/Production و سه
فایل unfinished AI خارج از Commit هستند. hash نهایی مستندات و نتیجهٔ واقعی Push/Remote
verification در پیام پایان مرحله ثبت می‌شود؛ این سند ادعای Push پیش از اجرای فرمان نمی‌کند.
main ادغام و IIS/سرور Deploy نشده‌اند.

## ۱۳. محدودیت‌های جاری

Mode هنگام startup خوانده می‌شود؛ تغییر آن باید با restart کنترل‌شدهٔ میزبان انجام شود.

پرداخت/Store Billing/Push Runtime/Paid Provisioning آموزشگاه Deferred هستند؛ PersonalClass
Assignments/Exams طبق تصمیم قبلی Deferred می‌مانند. ساخت/ذخیرهٔ منبع و ظرفیت Storage خارج
از سیاست AI هستند؛ Library AI و Safety bypass مجاز نشده‌اند. Production AI Runtime فعال
نمی‌شود. Smoke محلی به AI مستقر، شاهد Deploy یا دسترسی از Backend Server مقصد نیست.

## ۱۴. نکات آموزشی

1. رایگان‌بودن AI مجوز داده ایجاد نمی‌کند؛ بررسی دسترسی باید مستقل و جاری بماند.
2. دفتر عملیات برای Idempotency لزوماً دفتر بدهی یا مصرف Quota نیست.
3. محدودیت واقعی را در محیطی آزمون کنید که استثنای تست آن را از قبل بالا نبرده باشد.
4. رکورد نامحدود نباید در بازگشت آینده به Commercial به مصرف Trial تبدیل شود.
5. Timeout پس از ارسال با شکست قطعی پیش از اجرا فرق دارد؛ Unknown را کورکورانه Retry نکنید.
6. قابلیت Deferred باید در قرارداد انتشار روشن باشد؛ حذف کد یا افزودن تنظیم فرضی لازم نیست.

## ۱۵. مسیر مطالعه

`AiController` → `AiChatService` → `AiInvocationExecutor` → `AiUsageOptions` /
`CommercialSubscriptionService` → `UsageReservation` → `EfCommercialStore.Reservations`
→ دو EF Configuration → Migration → تست‌های Unmetered. سپس
`KnowledgeIntegrationController` → `KnowledgeAuthorizationService` → `AiKnowledgeClient`
→ UnmeteredKnowledgeAcceptanceTests و دو Probe پذیرش را بخوانید.

## ۱۶. مرحلهٔ بعد

پس از Commit/Push و تأیید Source، Task مستقلِ ساخت بسته/استقرار Test و اعمال Migration روی
مقصد مجاز اجرا شود؛ سپس Health و اتصال AI/Knowledge از همان Backend مقصد بررسی شود.
پرداخت، Push، آموزش PersonalClass، تغییر Service Auth یا فعال‌سازی Production با آن مخلوط نشوند.
