# تثبیت Regression کامل Realtime

مبنای بررسی: `66b0d0acdab47fae0b90175e0308f4232abe0a7f` روی شاخهٔ `feature/product-v6-backend-alignment`.

## ۱. خلاصهٔ مرحله

در اجرای قبلی، هفت تست انتظار `ConversationChanged` شکست خوردند؛ اجرای مستقل همان تست‌ها موفق بود. بازسازی کنترل‌شده نشان داد اتصال SQL برگشتی از Pool می‌تواند Isolation برابر `Serializable` را از تراکنش قبلی حفظ کند. Context تازه در نتیجه قفل‌هایی می‌گیرد که برای خواندن عادی در نظر گرفته نشده‌اند. انتظار SQL، بودجهٔ notifier را تمام می‌کند و پیام ثبت‌شده، اعلان زندهٔ مورد انتظار را در مهلت تست نمی‌رساند.

اصلاح، پیش‌فرض اتصال بازشدهٔ `MakanDbContext` را مشخص می‌کند. تراکنش‌های صریح و Ambient مالک Isolation خود باقی می‌مانند. هیچ مهلت، Retry یا مجوزی برای سبزکردن تست تغییر نکرده است.

## ۲. معماری

تغییر Runtime فقط در Infrastructure است: تنظیم Session متعلق به adapter SQL Server است. Domain، Application، Controller و قرارداد عمومی تغییر نکرده‌اند. کلاس بدون State جدید با DI به گزینه‌های `MakanDbContext` اضافه شده است؛ Domain/Application وابستگی EF یا SQL جدیدی نگرفته‌اند.

## ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت و نکتهٔ آموزشی |
|---|---|
| `MakanApp.Infrastructure/Persistence/SqlServerConnectionInitializationInterceptor.cs` | تعیین `READ COMMITTED` هنگام بازشدن اتصال Sync/Async؛ Context تازه لزوماً Session فیزیکی تازه نیست. |
| `tests/MakanApp.IntegrationTests/SqlServerConnectionIsolationTests.cs` | اثبات نشت Pool، حفظ تراکنش صریح/Ambient، قفل هم‌زمان و تحویل واقعی HTTP → SQL → SignalR. |
| `tests/MakanApp.IntegrationTests/RealtimeTestDiagnostics.cs` | ثبت محدود و امنِ میزبان، مرحلهٔ انتشار، Claim و کد SQL هنگام شکست؛ بدون پیام، Token، OTP یا SQL خام. |

## ۴. فایل‌های مهم تغییرکرده

- `MakanApp.Infrastructure/DependencyInjection.cs`: ثبت Singleton بدون State و افزودن Interceptor به Contextهای Runtime.
- `MakanAppWebApplicationFactory.cs` و `MakanAppWebApplicationFactory.Realtime.cs`: تشخیص تستی و Probe با دیتابیس GUID مستقل و Pool تک‌اتصالی برای بازسازی قطعی.
- `AccountRealtimeOutboxTests.cs`: بازسازی backlog آماده‌شده با جهش ساعت و بررسی تکمیل همان Claim در Batchهای محدود، با حفظ Assert دقیق دو انتشار و دو Attempt.
- شش فایل تست شامل هفت Case ناموفق: انتظار اصلی ۱۰/۱۵ثانیه‌ای حفظ شده؛ هنگام شکست، Snapshot امن نیز ارائه می‌شود.

فایل‌های خصوصی، تنظیمات MeliPayamak و سه فایل ناتمام AI در مبنای Git با SHA-256 حفاظت شدند. این فایل‌ها جزو Commit این مرحله نیستند.

## ۵. جریان اجرا

```text
HTTP POST /api/v1/conversations/{id}/messages
→ ConversationsController
→ MessagingService.SendMessageAsync
→ قواعد فعلی گفتگو/نشست/مجوز
→ IMessagingStore.SendMessageAsync / EfMessagingStore
→ MakanDbContext
→ SqlServerConnectionInitializationInterceptor در Open اتصال
→ SQL Server و Commit پیام/ChangeEvent/RealtimeOutbox
→ MessagingRealtimeOutboxProcessor
→ SignalRMessagingRealtimeNotifier و بازبینی Audience فعلی
→ MessagingHub / ConversationChanged
→ Delta موجود برای بازیابی پایدار
```

Interceptor جدید پیش از آغاز تراکنش صریح EF، پیش‌فرض Session را تنظیم می‌کند. در حضور `Transaction.Current` نیز مالکیت Ambient حفظ می‌شود.

## ۶. دیتابیس

Table، Column، FK، Index، Constraint و Migration جدیدی ایجاد نشده است. `rowversion` و Claim fencing قبلی حفظ شده‌اند. دستور Session هیچ گزینهٔ سراسری دیتابیس را تغییر نمی‌دهد؛ `READ COMMITTED` از تنظیم موجود `READ_COMMITTED_SNAPSHOT` تبعیت می‌کند.

Migrationها فقط در دیتابیس‌های خودکار و ایزولهٔ تست اجرا شدند. روی `MakanApp`، `MakanApp_Test` یا سرور، Migration اجرایی انجام نشده است.

## ۷. Endpointها

Endpoint جدید یا تغییر Contract نداریم. مسیرهای موجود مورد استفادهٔ پذیرش عبارت‌اند از ارسال پیام، دریافت Changes و Hub `/hubs/messaging` با `SubscribeConversation`. Authentication، Authorization، Context و ErrorCodeهای HTTP موجود حفظ شده‌اند.

## ۸. مفاهیم فنی

- Connection Pooling و تفاوت Context منطقی با Session فیزیکی SQL.
- Isolation سطح Session در برابر تراکنش صریح؛ حفظ `Serializable`، `RepeatableRead` و Ambient.
- قفل‌های Range/Key، RCSI و اثر انتظار SQL بر بودجهٔ اعلان زنده.
- Outbox پس از Commit، Claim/Lease و بازیابی؛ تحویل زنده exactly-once فرض نشده است.
- انتظار مبتنی بر Event و بازسازی قطعی با SQL؛ افزایش Timeout جایگزین اثبات علت نیست.

## ۹. تصمیم‌های طراحی و شواهد

در Probe با همان `Microsoft.Data.SqlClient` و EF پروژه، پس از Commit تراکنش `Serializable`، Context تازه روی همان SPID مقدار ۴ را دید. دو تست Commit/Rollback و مسیر Sync قبل از Fix با `Expected=2 / Actual=4` شکست خوردند. بازسازی قفل روی `Conversations` و `ConversationParticipants` نیز قبل از Fix به `Lock request time out` رسید.

برای اثبات مسیر واقعی، همان متد `AuthorizedParticipantReceivesInvalidationAndRecoversDurablyFromDelta` از یک Fixture خصوصی اجرا شد. پس از Commit پیام، Pool تک‌اتصالی با Isolation قبلی و یک نوشتن هم‌زمان روی Participant بازسازی شد. نسخهٔ مبنا پس از ۱۰ثانیه Fail شد: ناشر همان Host بود، انتشار Started شد، Worker فعال ماند، Claim هنوز Dispatch نشده بود و notifier لغو شد. پس از Fix، همان سناریو موفق شد.

مجموعهٔ هشت‌تستی با ثبت DI نسخهٔ مبنا: ۵ Fail و۳ Pass؛ با DI اصلاح‌شده: ۸ Pass. مقایسهٔ مبنا با فایل خصوصیِ `DependencyInjection.cs` از Git انجام شد؛ Source اصلی Reset یا جایگزین نشد.

تغییر در نقطهٔ بازشدن اتصال انتخاب شد تا خواندن‌ها به State تصادفی Pool وابسته نباشند. Trade-off یک دستور Session اضافی در هر Open است. تست‌ها ثابت می‌کنند تراکنش‌های تجاری صریح و Ambient تنزل نمی‌یابند. Parallelism سراسری Suite تغییر نکرده و Retry جدیدی اضافه نشده است.

در ترکیب با تست‌های Academic، یک وابستگی تستی دیگر نیز قطعی شد: `AccountRealtimeExpiredPublisherCannotCompleteRecoveredClaim` فرض می‌کرد دو انتشار خودش در یک Batch جا می‌شوند. جهش کنترل‌شدهٔ ساعت، کارهای تأخیریِ قبلی Fixture را نیز آماده می‌کرد و سقف۵۰تایی Batch می‌توانست پیش از انتشار دوم همان کار تمام شود. نسخهٔ قبلی با۵۰ کارِ تأخیری به‌صورت قطعی `Expected=2 / Actual=1` داد؛ حالت بدون backlog موفق بود. اکنون تست، Batchهای واقعی را تا `ProcessedAtUtc` همان شناسه پیش می‌برد؛ سقف تعداد Batch از تعداد ردیف‌ها محاسبه می‌شود. Assertهای دقیق دو انتشار و دو Attempt حفظ شده‌اند. این اصلاح Worker یا سیاست Retry را تغییر نمی‌دهد.

## ۱۰. تست‌ها و شکست‌های مبنا

| Case | فایل | شکست اصلی و مهلت | انتظار |
|---|---|---|---|
| `AuthorizedParticipantReceivesInvalidationAndRecoversDurablyFromDelta` | `MessagingRealtimeEndpointsTests.cs` | Timeout؛۱۰ثانیه؛مدت۱۰٫۵۴۸۸ | MessageCreated و بازیابی همان پیام از Delta |
| `RevokingOneSessionStopsRestHubAndFuturePrivateEventsButKeepsOtherSessionWorking` | `MessagingP1MultiDeviceAcceptanceTests.cs` | Timeout؛۱۰ثانیه؛مدت۱۰٫۴۵۷۱ | تحویل به نشست معتبر دیگر |
| `PersonalAccountExplicitReplayAndLiveWorkWithoutChangingSelectedTeacherOrganization` | `PersonalAccountResourceScopeTests.cs` | لغو Channel Read؛۱۵ثانیه؛مدت۱۵٫۲۶۹۲ | اعلان Personal با ContextRevision درست |
| `StabilizationTwoIndependentReceiverSessionsSynchronizeReadWithoutRegression` | `MessagingStabilizationAcceptanceTests.cs` | Timeout؛۱۵ثانیه؛مدت۱۵٫۲۷۵۳ | پیام در دو نشست و همگام‌شدن Read |
| `P2SilentStillPersistsUnreadRealtimeAndInternalNotificationButSuppressesOnlyExternalDelivery(true)` | `MessagingP2SilentEndpointsTests.cs` | Timeout؛۱۰ثانیه؛مدت۱۱٫۱۳۹۹ | اعلان زنده و اعلان داخلی برای Silent |
| همان Case با `false` | همان فایل | Timeout؛۱۰ثانیه؛مدت۱۱٫۰۱۰۷ | اعلان زنده با مسیر عادی |
| `RealtimeInvalidationUsesUtcTimestampAfterSqlReload` | `MessagingP1MediaWireTests.cs` | Timeout؛۱۰ثانیه؛مدت۱۰٫۷۵۳۶ | پیام و timestamp از نوع UTC |

کلاس هر هفت Case، `MessagingEndpointsTests` است. Stack Traceهای قبلی به انتظار Event رسیدند؛ Assertهای بعد از انتظار اجرا نشده بودند. وضعیت per-event SQL در اجرای تاریخی ذخیره نشده بود. Stack بدون PDB نیز در تست دونشستی، خط دقیقِ یکی از چند Wait را مشخص نمی‌کرد؛ این محدودیت با حدس پر نشده است. Snapshot جدید و بازسازی قطعی آن اطلاعات را برای شکست تازه ثبت می‌کنند.

هر هفت تست به SQL Server، TestServer/Hub Fixture و Worker مشترک Realtime وابسته‌اند. شناسه‌های User/Conversation/Operation اختصاصی‌اند. Handler پیش از Trigger ثبت شده و اتصال/Subscription انتظار شده‌اند. در زمان‌بندی اجرای تاریخی، هیچ‌یک از هفت Case با تست دیگری هم‌پوشانی نداشت. در بازسازی قطعی نیز یک Host/Worker کافی بود؛ غیرفعال‌سازی کل Parallelism توجیه نداشت.

بررسی چرخهٔ SignalR نشان داد `TaskCompletionSource`های همین انتظارها از `RunContinuationsAsynchronously` استفاده می‌کنند و اتصال‌ها با `await using` آزاد می‌شوند. در تست Personal، انتظار Channel با CancellationToken پانزده‌ثانیه‌ای محدود است؛ تأخیر کوتاهِ بعد از آن مربوط به قرارداد صفحه‌بندی Replay است، نه جایگزین همگام‌سازی اعلان. مسیرهای انتظار ناموفق، `Thread.Sleep`، `.Wait()` یا `.Result` ندارند. ارتباط Hub در این پذیرش از TestServer استفاده می‌کند؛ تعارض پورت عمومی شاهدی برای این شکست‌ها نبود.

در بازسازی ناموفق، انتشار از Host اصلی شروع شده بود، Worker فعال بود و Claim هنوز تحویل نشده بود؛ توقف زودهنگام Host یا ثبت دیرهنگام Handler علت آن بازسازی نبود. نمونهٔ منابع اجرای کامل نیز فقط شمار Thread/Handle/CPU را ثبت می‌کند. این نمونه به‌تنهایی اثبات نبودن هر نوع نشت منابع نیست؛ برای ThreadPool starvation، کمبود Socket یا نشت CancellationToken شاهد علّی به دست نیامد. علت اثبات‌شده با مشاهدهٔ Isolation همان SPID، قفل کنترل‌شده و شکست/موفقیت همان مسیر واقعی مشخص شد.

هشت تست SQL/SignalR جدید و یک حالت اضافی backlog، Integration هستند؛ Pool و قفل SQL با Unit یا EF InMemory اثبات نمی‌شوند. حالت‌های۰/۵۰ backlog قبل از اصلاح۱Pass/۱Fail و پس از اصلاح۲Pass داشتند. Unit و Architecture جدیدی برای تقلید پیاده‌سازی اضافه نشده‌اند.

## ۱۱. Validation

وضعیت نهایی این بخش: `PASS`؛ هر سه Full Regression پس از اصلاح موفق شدند.

| بررسی | نتیجهٔ ثبت‌شده |
|---|---|
| Clean / Restore | PASS |
| Release Build | PASS؛۰Warning،۰Error |
| Final Release Build | PASS؛۰Warning،۰Error |
| Pending Model Changes | NO |
| Realtime | ۱۰اجرای۹۷تستی؛جمع۹۷۰؛۰Fail،۰Skip |
| پوشش هفت Case تاریخی | هر هفت Case در هر ده اجرا؛۷۰اجرای موفق |
| Mixed | ۵اجرا:۶۹۹/۳۵۲/۲۷۲/۳۵۵/۱۷۰؛جمع۱۸۴۸؛۰Fail،۰Skip |
| Full Regression اول | PASS؛۳۶۰۸تست؛۰Fail،۰Skip |
| Full Regression دوم | PASS؛۳۶۰۸تست؛۰Fail،۰Skip |
| Full Regression سوم | PASS؛۳۶۰۸تست؛۰Fail،۰Skip |

هر یک از سه اجرای کامل شامل ۱۴۴۸ Unit، ۲۱۴۴ Integration و ۱۶ Architecture بوده است. افزایش ۹تستی نسبت به مبنای ۳۵۹۹، حاصل هشت Case جدید SQL/SignalR و یک Case اضافی backlog است. هر سه اجرا بدون تغییر Runtime/تست میان تکرارها انجام شدند. پنج ترکیب مستقل شامل Messaging، Academic، Organization/Session، Subscription/Deletion و Worker Recovery/Observability بودند.

اجرای تشخیصی قدیمی با۱Failure در `DeliveryCompletionSqlUsesFiniteIndependentBudgetAndLeavesRecoverableClaim` نگهداری شده و Gate موفق محسوب نشده است. چرخهٔ اول نیز پس از۱۰اجرای۹۴تستی موفق، در Mixed به فرض نادرست تکمیل در یک Batch رسید؛ نتیجهٔ ناموفق آن حفظ شد و چرخهٔ نهایی پس از اصلاح این فرض از ابتدا اجرا شد.

## ۱۲. Git

تمام Gateهای Build/Test عبور کرده‌اند. فایل‌های همین اصلاح با فهرست صریح Stage می‌شوند و Commit/Push روی همان شاخهٔ tracked انجام می‌شود. شناسه و نتیجهٔ قطعی Git در گزارش نهایی و `RELEASE_MANIFEST.json` بسته ثبت می‌شود. فایل‌های AI اولیه، تنظیمات خصوصی و Artifactها خارج از Stage می‌مانند؛ Merge به main و Force Push انجام نمی‌شوند.

## ۱۳. محدودیت‌ها

این نتیجه پذیرش محلی است. اتصال سرور، IIS Deploy، Migration سرور و پذیرش واقعی AI/Push در این مرحله اجرا نمی‌شوند. Fixture قدیمی Suite نام دیتابیس ثابت دارد؛ اجرای چند Process مستقلِ همان Suite به‌طور هم‌زمان جزو Gate این مرحله نیست. Probe جدید، دیتابیس مستقل دارد.

## ۱۴. نکات آموزشی

1. `new DbContext` تضمین‌کنندهٔ Session SQL تازه نیست.
2. گزینهٔ Session می‌تواند پس از Commit/Rollback و بازگشت به Pool باقی بماند.
3. Deadline اعلان می‌تواند به‌دلیل انتظار SQL تمام شود، در حالی که پیام قبلاً Commit شده است.
4. تنظیم پیش‌فرض باید پیش از تراکنش صریح انجام شود؛ Ambient نیز نباید تنزل یابد.
5. Race را با شرایط کنترل‌شده بازسازی کنید و شکست قبل از Fix را نگه دارید.
6. نتیجهٔ Focused را از Full Regression پایدار و پذیرش سرور جدا گزارش کنید.

## ۱۵. ترتیب مطالعه

`SqlServerConnectionIsolationTests` → `MakanAppWebApplicationFactory.Realtime` → تست اصلی `MessagingRealtimeEndpointsTests` → `MessagingRealtimeOutboxDispatcher/Processor` → `SignalRMessagingRealtimeNotifier` → `SqlServerConnectionInitializationInterceptor` → ثبت آن در `DependencyInjection` → Snapshot امن `RealtimeTestDiagnostics`.

## ۱۶. مرحلهٔ بعد

پس از تأیید Commit/Push، دو بستهٔ مستقل از همان Commit آماده می‌شوند. گام عملیاتی بعدی مالک، نصب و پذیرش دستی روی Test با Backup و بررسی History مقصد است؛ هیچ استقراری در این Task انجام نمی‌شود.
