# گزارش آموزشی Adaptive Assistant v1

مبنا: `95cef1b72695e51a6c851c9958ac5a026adba387`، شاخهٔ
`feature/product-v6-backend-alignment`. قرارداد AI از commit دقیق
`8bd0c8981dc49f339773ced6024e70e47c4ebdd2` بررسی شد؛ انتخاب Provider و fallback، Service Auth،
MeliPayamak، تنظیمات خصوصی و فایل‌های ناتمام AI تغییر نکرده‌اند.

## ۱. خلاصهٔ مرحله

Chat قبلی حافظهٔ SQL مستقل، Profile تطبیقی و SSE نداشت و انتخاب Source در Chat به
retrieval عملی وصل نبود. اکنون شروع سادهٔ بدون فایل/Profile/نقش، Profile اختیاری، پنجرهٔ
تاریخچهٔ مجاز، Streaming واقعی و orchestration صریح General/Auto/SourcesOnly وجود دارد.
پیاده‌سازی Backend با استقرار و پذیرش واقعی سرور برابر نیست.

## ۲. معماری

- Domain: `AiConversation` و `AiConversationTurn` هویت، ترتیب، زوج کامل، متن معتبر، سقف
  پاسخ و immutable بودن نسخه‌های منابع را نگه می‌دارند؛ EF/HTTP به Domain وارد نشده است.
- Application: `AiChatService` جریان use case را هدایت می‌کند؛ `AiChatSources` از مجوز
  Knowledge موجود استفاده می‌کند. Profile، پنجرهٔ History و portهای persistence/stream اینجاست.
- Infrastructure: `EfAiConversationStore` تراکنش/قفل/SQL و `AiServiceClient`/`AiSseReader`
  transport واقعی و parser افزایشی را پیاده می‌کنند.
- Api: Controller ورودی/پاسخ و flush کردن SSE را انجام می‌دهد؛ قواعد مالکیت، OTP، SQL و
  Source authorization داخل Controller قرار نگرفته‌اند. Composition Root همان DI فعلی است.

جهت وابستگی ثابت است: Domain ← Application ← Infrastructure؛ Api فقط Application و
Infrastructure را برای composition می‌شناسد. هیچ CQRS، MediatR، Generic Repository یا
microservice اضافه نشده است.

## ۳. فایل‌های مهم جدید

| مسیر از ریشه | مسئولیت و دلیل ایجاد | نکتهٔ آموزشی |
|---|---|---|
| `MakanApp.Domain/Intelligence/AiConversation.cs` | مالک، fingerprint، sequence و SourceVersionهای Conversation مستقل AI | تاریخچهٔ AI نباید از جدول پیام خصوصی Messenger ساخته شود |
| `MakanApp.Domain/Intelligence/AiConversationTurn.cs` | رزرو metadata بدون متن؛ تکمیل زوج معتبر User/Assistant | partial یا failed prompt به معنی completed history نیست |
| `MakanApp.Application/Ai/AiAdaptiveContracts.cs` | Profile سخت‌گیرانه، Mode/SourceIntent و پنجرهٔ History | شمارش Unicode با UTF-16 length یکی نیست |
| `MakanApp.Application/Ai/AiConversationContracts.cs` | port مالکیت و persistence، هویت Context و خطاها | selector مجوز نیست و Session تازه می‌تواند همان مالک/بافت را ادامه دهد |
| `MakanApp.Application/Ai/AiChatService.Preparation.cs` | validation، زمینهٔ جاری، hash intent، Conversation و History | history و ID تولیدشدهٔ سرور نباید hash قصد کاربر را در Retry تغییر دهند |
| `MakanApp.Application/Ai/AiChatSources.cs` | reuse مجوز/Pin/Status/Retrieve/RAG موجود و fallback صریح | SourceIntent سبک سؤال است؛ حق خواندن فایل ایجاد نمی‌کند |
| `MakanApp.Application/Ai/AiStreamingContracts.cs` | چهار event مستقل از HttpResponseMessage | Application رویداد تایپ‌شده می‌بیند؛ HTTP در Api/Infrastructure می‌ماند |
| `MakanApp.Application/Ai/AiInvocationExecutor.Streaming.cs` | نگه‌داشتن همان Reserve/Finalize برای عمر stream | ledger موازی و دوباره‌فراخوانی Provider لازم نیست |
| `MakanApp.Infrastructure/Ai/AiSseReader.cs` | decoder UTF-8 و parser محدود، ترتیب و terminal سخت‌گیرانه | یک network read لزوماً یک event یا یک نویسهٔ کامل نیست |
| `MakanApp.Infrastructure/Ai/AiServiceClient.Streaming.cs` | ResponseHeadersRead، timeout/breaker موجود و cancellation | شکست stream حتی بعد از HTTP200 موفقیت محسوب نمی‌شود |
| `MakanApp.Infrastructure/Ai/EfAiConversationStore.cs` | SQL و transaction کوتاه؛ lock مالک و Conversation | current-state authorization پیش از خواندن متن و تکمیل دوباره بررسی می‌شود |
| `MakanApp.Infrastructure/Persistence/Configurations/Ai/AiConversationConfiguration.cs` | mapping هر دو جدول، FK/unique/check/rowversion و JSON pins | قیود مهم را به بررسی Application محدود نکنید |
| `MakanApp.Api/Controllers/AiController.Streaming.cs` | HTTP stream و History خود مالک | قبل از start، ProblemDetails؛ پس از آن error event امن |
| `MakanApp.Api/OpenApi/AdaptiveAiSchemaTransformer.cs` | بیان واقعی bounds و Profile اختیاری در OpenAPI | قرارداد مستند باید با validation runtime تطبیق داشته باشد |

Migration و Designer آن با نام `20261118164500_AddAdaptiveAiConversations` و فایل‌های
تستِ بخش۱۰ نیز جدید هستند. [Handoff](ADAPTIVE_AI_FRONTEND_HANDOFF_FA.md) مرجع کاربرد Client است.

## ۴. فایل‌های مهم تغییرکرده

`AiChatContracts.cs` ورودی را با فیلدهای اختیاری و خروجی را با provenance/citations توسعه
می‌دهد. `AiServiceContracts.cs` برای Chat، context مناسب Profile و History دارد؛ DTOهای
نقش‌محور به صورت گسترده تغییر نکرده‌اند. `IAiServiceClient` متد StreamChatAsync گرفته است.

`AiInvocationExecutor` همچنان مالک ledger موجود است. fingerprint معتبرِ ساخته‌شده توسط
use case داخلی را می‌پذیرد و هنگام partial output حتی خطایی مثل429 را Released/Success
فرض نمی‌کند. محدودیت generic پاسخ برداشته شد چون نتیجهٔ RAG metadata جعلی مدل ندارد؛
اعتبارسنجی Provider همچنان در adapter موجود انجام می‌شود.

`KnowledgeAuthorizationService` همان ACL را برای بررسی انتخابِ درحال‌پردازش reuse می‌کند؛
مسیر Knowledge قبلی همچنان readiness سخت‌گیرانهٔ خودش را دارد. `AiSourceSelectionService`
در Preflightِ Chat، قابلیت Runtime موجود را اعلام می‌کند؛ عملیات نقش‌محور اضافه نشده‌اند.

`MakanDbContext` دو DbSet دارد؛ `DependencyInjection` دو port جدید را ثبت می‌کند و همان
AssistantContextResolver را به use case می‌دهد. `AiAccountDeletionLifecycle` پاک‌سازی
History حساب را به workflow حذف حساب موجود اضافه می‌کند. `GlobalExceptionHandler` و
OpenAPI خطاهای جدید را معرفی می‌کنند؛ OTP یا MFA عوض نشده است. README و چهار سند
تحویل/محدودهٔ جاری، به قرارداد جدید ارجاع می‌دهند و شواهد تاریخی را جعل نمی‌کنند.

## ۵. جریان واقعی اجرا

```text
HTTP Request
→ AiController.Chat / StreamChat
→ AiChatService.Prepare
→ AssistantContextResolver + AiConversationIdentity
→ AiChatSources.PrepareAsync
→ EfAiConversationStore.OpenAsync / ReadHistoryAsync
→ AiInvocationExecutor.ExecuteAsync / ExecuteStreamAsync
→ CommercialSubscriptionService.ReserveForAssistantAsync
→ بازبینی Context و Source ACL
→ AiServiceClient.ChatAsync / StreamChatAsync یا AiKnowledgeClient
→ اعتبارسنجی قرارداد / AiSseReader
→ AiConversationTurn.Complete در EfAiConversationStore.CompleteAsync
→ SQL Server
→ Finalize موجود
→ HTTP Result / SSE done
```

در stream، start/delta تدریجی تحویل می‌شوند؛ فقط done معتبر اجازهٔ ذخیرهٔ زوج و Consumed
دارد. خواندن متن و ثبت آن در transaction کوتاه است؛ شبکهٔ AI/Knowledge بیرون آن اجرا می‌شود.

## ۶. دیتابیس

`intelligence.AiConversations`: PK=`Id`، FK=`OwnerUserId→identity.Users` با Restrict،
کلید جایگزین `(Id,OwnerUserId)`، fingerprint از نوعbinary(32)، SourcePins از نوع JSON
محدود، NextSequence، UTC timestamps و rowversion. Index مالک/زمان/ID برای خواندن مالک وجود
دارد؛ قید NextSequence مثبت/فرد و ISJSON نیز اضافه شده‌اند.

`intelligence.AiConversationTurns`: PK=`Id`، composite FK روی `(ConversationId,OwnerUserId)`
برای جلوگیری از مالک متفاوت، unique روی `(ConversationId,Sequence)` و
`(OwnerUserId,OperationId,Role)`، IntentHash، Role، متن nullable، IsCompleted و زمان‌های UTC.
User sequence فرد و Assistant زوج است؛ pending فقط User با Text=null است؛ completed باید
متن و CompletedAtUtc داشته باشد. قید byte در SQL دفاع اضافی است؛ حد واقعی Unicode در
Domain و پنجرهٔ history اعمال می‌شود.

مالک با UPDLOCK/HOLDLOCK و ID گفتگو با sp_getapplock در همان transaction قفل می‌شوند؛
ReadCommitted از range-lock غیرضروری lookup خالی جلوگیری می‌کند. FK/unique منبع نهایی
integrity هستند. تکمیل تکراری یک operation، زوج دوم ایجاد نمی‌کند. User/Assistant هر دو
با یک SaveChanges ثبت می‌شوند؛ prompt ناموفق فقط metadata دارد. EF باید entity را **پیش
از تغییر JSON pins** attach کند تا snapshot قبلی و تغییر واقعی ذخیره شوند.

Migration فقط دو جدول/قیود/indexها را اضافه می‌کند؛ دادهٔ قدیمی Backfill یا حذف نمی‌شود.
Down در صورت وجود History/Conversation با51000 متوقف می‌شود. اجرای Migration روی پایگاه
عادی یا سرور در این Task مجاز نیست. SQL idempotent با کامپایل مؤخر و rollback در خطا
از ابزار موجود تولید می‌شود؛ پذیرش روی LocalDB GUIDدار است.

## ۷. Endpointها

| Method/Route | ورودی/پاسخ | Validation و مجوز/خطای مهم |
|---|---|---|
| `POST /api/v1/ai/chat` | SendAiChatCommand → AiChatResult200 با ConversationId canonical و provenance/citations | پیام تا۴۰۰۰ code point، Profile اختیاری؛ نشست/حساب/بافت و Source جاری؛400/401/403/404/409/502/503/504 |
| `POST /api/v1/ai/chat/stream` | همان ورودی → text/event-stream | همان مجوز؛ start/delta/done/error؛ خطای پیش از start عادی و پس از آن terminal error؛ RAG stream409 |
| `GET /api/v1/ai/conversations/{conversationId}/history` | پنجرهٔ۲۰ turn در AiConversationHistoryResult | فقط مالک و بافت جاری؛ بدون Payer یا عملیات تولید؛ مالک/بافت متفاوت404 و revoke رد می‌شود |

Routeهای OTP، Knowledge، تحلیل نقش و Endpointهای Messenger عوض نشده‌اند. Profile مدیریت
جدا ندارد. کد تعارض intent قبلی `COMMERCE_USAGE_CONFLICT` حفظ شده است.

## ۸. مفاهیم استفاده‌شده

Optional DTO و property converter سخت‌گیرانه، async iterator، incremental Decoder،
ResponseHeadersRead، backpressure با await/flush، CancellationToken، bounded buffer،
مالکیت و current-state authorization، fingerprint، rowversion/unique/transaction،
idempotency و Unknown، exact version pin و provenance صریح. Provider fallback همچنان
فقط مسئولیت AI upstream است؛ Event Sourcing یا سیستم پیام/ledger موازی نداریم.

## ۹. تصمیم‌ها و trade-off

History در Messenger ذخیره نشد تا private DM و حافظهٔ AI مخلوط نشوند. Context fingerprint
نشست را حذف می‌کند ولی نقش/کلاس/Subject مؤثر را نگه می‌دارد؛ مالک در نشست جدید ادامه
می‌دهد، در بافت دیگر نمی‌تواند History را بخواند. History به صورت زوج کامل انتخاب می‌شود؛
زوج دارای answer طولانی به جای truncate معنایی از پنجره حذف می‌شود.

پاسخ نهایی تا۳۲۷۶۸ code point در Backend محدود است چون upstream سقف کل نداشت؛ تجاوز
خطاست، نه truncate یا پاسخ موفق. done تا EOF معتبر upstream نگه داشته می‌شود تا event
پس از terminal موفق گزارش نشود؛ deltaهای قبل از آن همچنان واقعی و incremental هستند.

SourceIntentِ تأییدشده سؤال عمومی را از فایل‌محور جدا می‌کند. Auto عمومی می‌تواند fallback
با reason روشن داشته باشد؛ SourceFocused و SourcesOnly پاسخ عمومی مخفی نمی‌گیرند.
از آنجا که RAG رسمی streaming ندارد، خطای قابلیت از fake streaming صادقانه‌تر است.

دفتر عملیات پاسخ را بازپخش نمی‌کند؛ endpoint History مستقل فقط پنجرهٔ مجاز را می‌خواند.
at-most-one invocation برای یک intent با exactly-once شبکه یکی نیست؛ شکست مبهم Unknown
است و دوباره‌فرستادن خودکار ممنوع می‌ماند.

## ۱۰. تست‌ها

- Unit: `AiAdaptiveContractsTests` حدود/نوع Profile، Unicode، پنجره، هویت بافت، زوج کامل
  و immutable بودن SourceVersion را اثبات می‌کند. بخش جدید `AiServiceClientStreamingTests`
  UTF-8 چندتکه/CRLF، تمام ترتیب‌های نامعتبر، EOF، payload ناشناخته، سقف buffer، cancel،
  error امن و Personal scope نامعتبر را با transport کنترل‌شده می‌سنجد.
- Integration: `AdaptiveAiEndpointsTests`، `AdaptiveAiHistoryTests` و `AdaptiveAiSourcesTests`
  از HTTP و SQL واقعی، mock Provider و مجوز جاری استفاده می‌کنند: continuity/نشست جدید،
  مالک/Context/Role revoke، Historyهای طولانی، intent پایدار،۱۰ duplicate stream،
  partial Unknown، cancel واقعی HttpClient، عدم نشت log، Ready/Processing/Empty evidence،
  ACL منبع، Pin و نبود RAG stream.
- `AdaptiveAiOpenApiTests` قرارداد قابل‌مشاهدهٔ endpoint و bounds را بررسی می‌کند.
- `ChatAsync_ClassSwitchAfterProviderIsDeniedEvenWhenClassIsHiddenFromJson` و
  `AdaptiveAiContextRaceTests` تعویض کلاس پس از Provider/وسط stream را بررسی می‌کنند؛
  Context امنیتی با record comparison سنجیده می‌شود تا JsonIgnore کلاس را حذف نکند.
- SQL: `AdaptiveAiMigrationTests` Fresh/Upgrade، FK/check/unique،۱۰ تکمیل هم‌زمان و رقابت
  دو مالک روی یک Client GUID را اثبات می‌کند؛ EF InMemory استفاده نشده است.
- `AccountDeletionAdaptiveAiTests` از workflow کامل BE22، پاک‌شدن History مالک، حفظ History
  حساب دیگر و عدم ارث‌بری با شمارهٔ بازیافتی را می‌سنجد.
- Architecture: `AdaptiveAiBoundaryTests` port بدون EF/HTTP و استقلال از Messenger را
  اضافه می‌کند؛۱۴ invariant قبلی Onion نیز حفظ‌اند.

Regression یک اختلاف واقعی پیدا کرد: changed intent باید همان COMMERCE_USAGE_CONFLICT
قبلی را بدهد. تست قدیمی حفظ شد و کد اصلاح شد؛۲۰ درخواست هم‌زمان پس از اصلاح PASS شدند.
تست persistence نیز نشان داد تغییر SourcePins باید قبل از snapshot درست EF انجام شود؛
پس از اصلاح، Archive منبع خواندن/ادامهٔ History وابسته را مسدود می‌کند.
تست تازهٔ تعویض کلاس نیز ابتدا به‌طور واقعی Fail شد: مقایسهٔ JSON فیلد JsonIgnore را
نمی‌دید و خطایی صادر نمی‌شد. مقایسهٔ record جایگزین شد؛ Snapshot عمومیِ DTO به‌جای هویت
امنیتی استفاده نمی‌شود. این اصلاح در scope خود Adaptive Chat/Source selection محدود ماند.

## ۱۱. Validation

Restore موفق بود. Buildهای Debug در دو دور کامل و همچنین زنجیرهٔ Clean/Restore/Build
حالت Release موفق شدند؛ تمام Buildهای نهایی صفر Warning و صفر Error داشتند.

| اجرا | Unit | Integration | Architecture | مجموع | Failed | Skipped |
|---|---:|---:|---:|---:|---:|---:|
| Regression کامل پس از رفع تعارض کد خطا | ۱۴۴۷ | ۲۱۳۴ | ۱۶ | ۳۵۹۷ | ۰ | ۰ |
| اجرای کامل نهایی، پس از اصلاح مقایسهٔ بافت کلاس | ۱۴۴۸ | ۲۱۳۵ | ۱۶ | ۳۵۹۹ | ۰ | ۰ |

نسبت به baseline اعلام‌شدهٔ۳۴۹۸، تعداد۱۰۱ تست بیشتر اجرا شد. دو تست آخر مربوط به
تعویض کلاس پس از Provider و وسط stream هستند؛ پس از اصلاح،۳۷ تست متمرکز Integration
و سپس کل suite نهایی اجرا شدند.۱۱۷ تست Zibal نیز در Regression نهایی PASS هستند؛
تست‌های قبلی Knowledge، Messenger و Realtime در همان suite کامل باقی مانده‌اند.

یک Migration تازه ساخته شد و مجموع Migrationها۷۷ است. بررسی pending model changes
هیچ تغییری نشان نداد. پذیرش SQL کامل idempotent در۸ سناریو، از جمله نصب تازه، ارتقا،
Retry و rollback خطا، روی LocalDB موقت موفق شد؛ چهار DB تازهٔ همان اجرای SQL پس از
بررسی نام و مالکیت ایجاد، پاک شدند. دیتابیس‌های عادی `MakanApp` و `MakanApp_Test`
و هیچ SQL سروری تغییر نکردند.

`BACKEND_SERVER_TEST=NOT_RUN_BY_POLICY`، `BACKEND_SERVER_SQL=NOT_RUN_BY_POLICY` و
`BACKEND_SERVER_DEPLOYMENT=NOT_RUN_BY_POLICY` حفظ شدند.
`AI_SERVER_LIVE_ACCEPTANCE=NOT_REQUIRED`؛ این نتایج محلی، پذیرش واقعی سرور نیستند.

## ۱۲. Git

فقط فایل‌های همین قابلیت و اسناد/تست/Migration آن وارد Commit می‌شوند. سه فایل ناتمام
`tools/ai/provider_diagnostic.py`، `run_exact_ai.py` و `test_provider_diagnostic.py` و تمام
مقادیر خصوصی خارج Commit هستند؛ manifest SHA-256 چهارده فایل محافظت‌شده بررسی می‌شود.
Commit hash، message، نتیجهٔ Push شاخهٔ tracked و تطبیق remote در پاسخ نهایی و manifest
خصوصی Release ثبت می‌شوند؛ main ادغام یا force-push نمی‌شود.

## ۱۳. محدودیت‌های فعلی

RAG streaming رسمی، Profile Management، مرور تاریخچه توسط Admin و retention خودکار
ساخته نشده‌اند. `PRODUCT_RETENTION_DECISION_REQUIRED` برای زمان نگه‌داری خودکار باز است؛
حذف صریح حساب از workflow موجود History را purge می‌کند. Runtime اجرای حذف حساب و
AI Production مطابق تنظیمات موجود حفظ می‌شود؛ تست opt-in ایزوله، فعال‌سازی سرور نیست.
Payments/Push همچنان Deferred هستند. هیچ Backend Server/SQL/IIS در این Task لمس نمی‌شود.

## ۱۴. نکات آموزشی

1. محل converter اهمیت دارد؛ converter عمومی enum نباید case حساس proficiency را نرم کند.
2. Attach بعد از تغییر entity می‌تواند snapshot اشتباه بسازد و JSON pins را ذخیره نکند.
3. JSON projection معیار کامل Context امنیتی نیست؛ فیلد JsonIgnore هم باید در مقایسهٔ record دیده شود.
4. stream200 بدون terminal معتبر، پاسخ موفق نیست؛ partial نیز completed history نیست.
5. Decode UTF-8، parse SSE و validate معنای event سه مسئولیت مرتبط اما متفاوت‌اند.
6. Source ACL باید پس از Query و پیش از ذخیره/خواندن History نیز جاری باشد.
7. تست SQL واقعی، FK/lock/race را اثبات می‌کند؛ Unit به‌تنهایی رفتار EF را تضمین نمی‌کند.

## ۱۵. مسیر پیشنهادی مطالعه

`AiController.Streaming` → `AiChatService` و `Preparation` → `AiConversation` /
`AiConversationTurn` → `IAiConversationStore` و `IAiChatSources` →
`AiInvocationExecutor.Streaming` → `AiChatSources` → `AiServiceClient.Streaming` /
`AiSseReader` → `EfAiConversationStore` → EF Configuration → Migration → تست‌های بالا.

## ۱۶. مرحلهٔ بعد

پس از دریافت بسته‌ها، نصب Test و Migration روی `MakanApp_Test` فقط توسط مالک و در Task
عملیاتی مجاز انجام شود؛ سپس Chat، SSE و Source واقعی از همان Backend بررسی شوند.
Production، تغییر Service Auth، پرداخت/Push و تصمیم retention با این پذیرش مخلوط نشوند.
