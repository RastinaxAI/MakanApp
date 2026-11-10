# گزارش توسعه‌دهندهٔ یکپارچه‌سازی زیبال

## ۱. خلاصه مرحله

قبل از این مرحله، Order/PaymentAttempt/Payment و تسویهٔ اتمیک و Outbox وجود داشتند، ولی runtime فقط Unavailable بود. اکنون Adapter واقعی زیبال، Callback غیرقابل اعتماد، Verify/Inquiry، بازیابی مجاز، رسید frozen و تاریخچهٔ امن اضافه شده‌اند. تسویهٔ محلی و تحویل اشتراک مستقل‌اند؛ هیچ نقش، قیمت یا قرارداد تجاری تازه‌ای حدس زده نشده است. فعال‌سازی درگاه Production هنوز به تنظیمات خصوصی و پذیرش عملیاتی نیاز دارد.

## ۲. معماری

Domain: قواعد state، انقضای lease بدون POST دوباره، مرجع بانکی و رویداد immutable. Application: هماهنگی Use Case، مجوز جاری مالک، snapshot/DTO و پورت Resolver. Infrastructure: HTTP/JSON زیبال، اعتبارسنجی تنظیمات، SQL/EF، قفل و Outbox موجود. Api: Query parsing، نشست، Rate Limiter، 303 و OpenAPI. Domain/Application وابستگی EF، ASP.NET یا SDK زیبال نگرفته‌اند. .NET 9، EF9، SQL Server و Controllers حفظ شده‌اند.

جهت وابستگی: Api → Application؛ Infrastructure → Application/Domain؛ Application → Domain. Api در Composition Root پیاده‌سازی Infrastructure را ثبت می‌کند.

## ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت و نکتهٔ آموزشی |
|---|---|
| `MakanApp.Domain/Commerce/PaymentAttemptEvent.cs` | metadata append-only؛ رخداد Callback با اثبات پرداخت یکسان نیست |
| `MakanApp.Application/Commerce/PaymentRecoveryContracts.cs` | پورت use case و read model؛ Contract، entity EF نیست |
| `MakanApp.Application/Commerce/OrderPaymentService.Recovery.cs` | مالکیت جاری، recovery و receipt؛ شناسه منبع مجوز نیست |
| `MakanApp.Infrastructure/Commerce/ZibalOptions.cs` | guard محیط، HTTPS، origin و timeout؛ Merchant در خروجی validation چاپ نمی‌شود |
| `MakanApp.Infrastructure/Commerce/ZibalPaymentGateway.cs` | typed HTTP، محدودیت body/deadline و mapping امن؛ کد ۲۰۱ نیاز به Inquiry دارد |
| `MakanApp.Infrastructure/Commerce/PaymentGatewayResolver.cs` | انتخاب Adapter بر اساس Provider ثبت‌شدهٔ تلاش، مستقل از پیش‌فرض جدید |
| `MakanApp.Infrastructure/Commerce/EfOrderPaymentStore.History.cs` | ترتیب قفل، timestamp نخستین callback و تاریخچهٔ مجاز |
| `MakanApp.Infrastructure/Persistence/Configurations/Commerce/PaymentAttemptEventConfiguration.cs` | FK Restrict، PK، index و check نوع رخداد |
| `MakanApp.Api/Controllers/PaymentRecoveryController.cs` | چهار عملیات HTTP دارای نشست، بدون EF/SQL یا اختیار تسویه از client |
| `MakanApp.Api/Controllers/ZibalCallbackController.cs` | parsing امن GET و redirect ثابت؛ success/status فقط hint هستند |
| `MakanApp.Api/OpenApi/CommercePaymentOperationTransformer.cs` | anonymous بودن Callback، چهار Query و 303، نشست recovery و خطاهای امن |
| `MakanApp.Infrastructure/Persistence/Migrations/20261110081503_AddZibalPaymentIntegration.cs` | تغییر additive؛ اطلاعات مالی قدیمی حدسی ساخته نمی‌شوند |
| `tests/MakanApp.UnitTests/Commerce/ZibalPaymentGatewayTests.cs` | قرارداد رسمی، خطا/timeout/JSON/مبلغ/تنظیمات و عدم افشای پیام خام |
| `tests/MakanApp.UnitTests/Commerce/PaymentRecoveryDomainTests.cs` | state، lease، absorbing success و مرجع immutable |
| `tests/MakanApp.IntegrationTests/ZibalPaymentEndpointsTests.cs` و `.Helpers.cs` | API + SQL واقعی با HTTP handler کنترل‌شده؛ concurrency و مجوز در مرز واقعی |

دو راهنمای [کلاینت](ZIBAL_FRONTEND_HANDOFF_FA.md) و [عملیات](ZIBAL_OPERATIONS_FA.md) نیز ایجاد شدند. Designer migration تولید EF است و بازنویسی دستی snapshot تجاری نیست.

## ۴. فایل‌های مهم تغییرکرده

`OrderPaymentService.cs` Resolver و recovery idempotent را reuse می‌کند. `OrderPaymentAbstractions.cs`، `OrderPaymentContracts.cs` و `PaymentGatewayAbstractions.cs` metadata جدید را به‌شکل افزایشی می‌دهند؛ سازنده‌های موجودِ DTO/Result حفظ شده‌اند. hash تلاش جدید به انتخاب Provider پیش‌فرض وابسته نیست و hash قدیمیِ معتبر نیز replay می‌شود.

`PaymentAttempt.cs` lease منقضی را Unknown می‌کند، callback نخستین را نگه می‌دارد و نتیجهٔ نامشخص دیررس را روی initiation/شکست قطعی نمی‌نویسد. `Payment.cs` مرجع بانکی/زمان معتبر Provider را از تلاش موفق کپی می‌کند. `Order.cs` عنوان محصول را یک‌بار freeze می‌کند؛ رسید قدیمیِ بدون مدرک null می‌ماند.

`EfOrderPaymentStore.cs` ایجاد تلاش تازه در حضور تلاش غیرقطعی را می‌بندد، Order را پیش از Attempt قفل می‌کند و Payment/Paid/Outbox را همان‌طور اتمیک نگه می‌دارد. `MakanDbContext.cs` تغییر/حذف Payment و history از SaveChanges را رد می‌کند. Configurationهای Order/Payment/Attempt و snapshot EF با Migration همگام‌اند.

`DependencyInjection.cs` Adapter/Resolver و Options را ثبت می‌کند؛ `Program.cs` محدودیت Callback و OpenAPI را اضافه می‌کند. `PlatformCommerceOperationsContracts.cs` و `EfPlatformCommerceOperationsStore.Finance.cs` مرجع بانکی masked و Fulfillment مستقل را به Finance موجود اضافه می‌کنند؛ هیچ permission حذف نشده است.

در `appsettings.json` فقط Payments تغییر کرده است. دو `.example` همان شکل امن را دارند؛ Provider پیش‌فرض Unavailable، Merchant/URL خالی و timeout=15 ثانیه است. فایل‌های خصوصی و مقادیر AI/SMS/OTP تغییر نکرده‌اند. README، وضعیت جاری و سند تاریخی STEP13B برای اشاره به مسیر واقعی تازه به‌روز شدند.

## ۵. جریان اجرا

```text
POST orders/payments
→ CommerceController یا CommercialSubscriptionsController
→ OrderPaymentService
→ EfOrderPaymentStore: intent + claim + commit
→ PaymentGatewayResolver / ZibalPaymentGateway
→ POST /v1/request خارج transaction
→ ProviderReference و PaymentUrl در SQL
→ مرورگر خارجی
→ ZibalCallbackController
→ OrderPaymentService.HandleCallbackAsync / VerifyPaymentAsync
→ ZibalPaymentGateway: verify و در صورت نیاز inquiry
→ EfOrderPaymentStore: lock Order → Attempt → بررسی مبلغ/مرجع
→ PaymentAttempt.Succeeded + Order.Paid + Payment + Outbox در یک commit
→ OutboxProcessor → EfSubscriptionActivationHandler
→ کلاینت: PaymentRecoveryController / receipt / status
```

برای callback گم‌شده یا بسته‌شدن اپ، کاربر واردشده همان Attempt را از Recovery ادامه می‌دهد. هیچ نیاز به ارسال دوبارهٔ مبلغ، trackId یا پیام موفقیت از Client نیست.

## ۶. دیتابیس

Migration `20261110081503_AddZibalPaymentIntegration`: دو ستون nullable به Payments (`SettlementReference nvarchar(200)` و `ProviderPaidAtUtc datetime2(7)`)، سه ستون nullable به PaymentAttempts (همان دو و `CallbackReceivedAtUtc`) و `Orders.ProductTitleSnapshot nvarchar(200) NULL` اضافه می‌کند. SQL Default یا backfill مالی وجود ندارد.

`commerce.PaymentAttemptEvents`: PK از نوع Guid؛ FK به PaymentAttempts با Restrict؛ Type با CHECK بازهٔ ۱..۸؛ زمان UTC، result/status، error code و correlation محدود. index روی `(PaymentAttemptId, OccurredAtUtc, Id)` برای history است. event و Payment نیاز به rowversion تازه ندارند، چون فقط append می‌شوند؛ rowversion و unique indexهای Order/Attempt/Payment موجود حفظ شده‌اند. guard برنامه جای مجوز DB را نمی‌گیرد و DBA با SQL مستقیم قادر به عبور از آن است.

همهٔ lockهای پرداخت Order سپس Attempt هستند. تماس زیبال داخل transaction نیست. تست SQL روی دیتابیس اختصاصی fixture اجرا می‌شود؛ هیچ Migration روی MakanApp، MakanApp_Test یا سرور اجرا نشده است.

## ۷. Endpointها

پنج عملیات تازه عبارت‌اند از GET Callback عمومی، GET Attempt، POST Attempt Verify، GET رسید سازمانی و GET رسید شخصی. Request/Response/Validation/مجوز/خطاها در [جدول قرارداد](ZIBAL_FRONTEND_HANDOFF_FA.md) آمده‌اند. APIهای Order/Quote/Initiate و مجوز فعلی آن‌ها reuse شده‌اند. Recovery فقط AttemptId می‌گیرد؛ HTTP 200 آن الزاماً Paid نیست و Status نتیجه باید خوانده شود. Callback فقط 303 با شناسه‌ها برمی‌گرداند و client باید status دارای نشست را بخواند.

## ۸. مفاهیم استفاده‌شده

typed HttpClient و CancellationToken؛ Options با fail-fast؛ Amount صحیح IRR در مرز Adapter؛ snapshot تاریخی؛ server-to-server verification؛ idempotency؛ قفل SQL با ترتیب ثابت؛ rowversion و uniqueness موجود؛ Outbox پس از commit؛ append-only audit؛ concealment با 404؛ bounded timeout/body؛ HTTP 303 و allowlist.

## ۹. تصمیم‌ها و Trade-off

- SDK قدیمی WebRequest/Newtonsoft انتخاب نشد؛ Adapter کوچک با HttpClient و System.Text.Json همان قرارداد رسمی جاری را پیاده می‌کند.
- ۲۰۱ به‌جای success فوری به Inquiry می‌رود؛ دادهٔ ناقص Unknown می‌ماند. این محافظ ممکن است پرداخت را تا recovery بعدی معلق نگه دارد، ولی خدمتِ بدون اثبات نمی‌دهد.
- POST ایجاد پرداخت retry خودکار ندارد؛ Unknown بدون trackId و callbackِ گم‌شده نیاز به بررسی پنل دارد. شارژ تکراری راه‌حلِ ابهام نیست.
- Provider ثابت در Attempt و Resolver ساده کافی است؛ زیرسیستم موازی درگاه/اشتراک ایجاد نشده است.
- timestamp رسمی بدون timezone به UTC حدسی تبدیل نمی‌شود. Backend زمان UTC تسویهٔ خودش را دارد؛ null تاریخی از زمان ساختگی دقیق‌تر است.
- پیش‌فرض Unavailable حفظ شد؛ نبود Merchant/URL واقعی با مقدار جعلی پنهان نمی‌شود.
- مراجع Finance مانند قرارداد قبلی masked مانده‌اند؛ Merchant و کارت اصلاً وارد persistence/DTO نشده‌اند.

## ۱۰. تست‌ها

Unit: Request 100 و خطاهای رسمی، trackId نامعتبر/overflow، حداقل/حداکثر/اعشار/ارز، HTTP 500/network/timeout/JSON ناقص، Verify100، Verify201→Inquiry، وضعیت‌های pending/failed/refund، correlation/amount/ref mismatch، cancellation، guard Production و SSRF/redirect/timeout، lease و immutable Payment.

Integration: API واقعی و Migration SQL، Callback موفق/جعلی/نامعتبر/لغو، ۱۰ بار تکرار، Callback هم‌زمان با recovery، ۲۰۱ پس از پرداخت سرور و بدون callback محلی، ناتوانی در تصاحب مرجع تلاش Unknown، cross-user/cross-org و Role revoke، رسید frozen، failure تحویل و retry Outbox بدون recharge، Provider switch با hash جدید و Legacy، عدم نشت Merchant/payload در پاسخ/history/log و metadata OpenAPI. HTTP واقعی بانک با handler جایگزین شده؛ SQL InMemory استفاده نشده است.

Architecture: suite موجود جهت وابستگی Onion را کنترل می‌کند؛ تست معماری تازه‌ای برای افزایش عدد ساخته نشده است.

## ۱۱. Validation

Restore و Build نهایی موفق‌اند: صفر Warning و Error. Unit متمرکز: ۷۲؛ Integration متمرکز شامل regression پرداخت قبلی: ۴۹؛ هر دو صفر Failed/Skipped. Regression نخست ۳۰۷۹ تست را گذراند. اجرای کامل نهایی روی آخرین Source: ۱۲۵۰ Unit + ۱۸۲۰ Integration + ۱۴ Architecture = ۳۰۸۴ Passed، صفر Failed و صفر Skipped. نسبت به baseline، ۷۲ Unit و ۳۶ Integration افزوده شده‌اند. هیچ تستی حذف/خاموش نشده است.

خطای نخستینِ نام property پلن و مشکلات fixture شامل کلید متفاوت نشست میان میزبان‌های تست، trackId تکراری شبیه‌ساز و انتخاب سفارش نامربوط در fault injection شناسایی و رفع شدند. دادهٔ تست و نام property واقعی اصلاح شد؛ کنترل امنیتی یا unique constraint برای سبزشدن تست حذف نشد. Smoke جداگانهٔ Composition Root نیز انتخاب Adapter و Resolver و رد Merchant تست در Production را بدون تماس DB/شبکه تأیید کرد.

EF pending model changes: NONE. OpenAPI: ۴۵۴ عملیات، ۸۲ Controller، ۱۲۸ جدول مدل و ۶۵ Migration؛ generation هیچ اتصال DB یا Worker نداشت. SQL کامل و delta با exporter idempotent موجود تولید شده‌اند؛ اجرای SQL روی سرور انجام نشده است.

آزمایش مستقل Merchant رسمی تست: HTTP200، result=100، trackId مثبت و URL استاندارد؛ بدون کارت/پول واقعی/تحویل درآمد. این probe فقط Request رسمی است، نه Live E2E بانک و Backend مستقر. مبنای قرارداد [JSON رسمی](https://api.zibal.ir/static/helpdocs/ipg.json) در ۲۰۲۶-۱۱-۱۰ است.

## ۱۲. Git

Baseline: `c4738bab71e26a97f591163798738cb5502f3e24`؛ شاخه `feature/zibal-ipg` با upstream `origin/feature/zibal-ipg`. پیام مورد استفادهٔ این مرحله `feat(commerce): integrate Zibal IPG payment gateway` است. فقط فایل‌های مرتبط پس از validation stage می‌شوند؛ سه فایل AIِ از پیش untracked خارج Commit می‌مانند. شناسهٔ Commit و نتیجهٔ Push/remote verification پس از ثبت، در پیام تحویل ارائه می‌شوند؛ این سند بخشی از همان Commit است و hash خودش را از پیش ندارد.

## ۱۳. محدودیت‌های فعلی

Production Merchant و Callback/ClientReturnUrl نهایی در این محیط آماده نیستند. Provider جاری عمداً Unavailable مانده؛ فعال‌سازی Zibal نیاز به تکمیل تنظیمات معتبر دارد. Test/Production Deploy و Live E2E بانکی اجرا نشده‌اند. مدیریت Secret در UI/DB، Refund، native store billing، reconciliation دستی Paid، قیمت و سهمیهٔ حدسی، Shared Plan و workflow تأسیس تازه خارج محدوده‌اند. هیچ AI/MeliPayamak/Frontend یا مجوز آموزشی تغییر نکرده است.

## ۱۴. نکات آموزشی

۱. Callback یک پیام ورودی غیرقابل اعتماد است؛ حتی success=0 جای بررسی معتبر سرور را نمی‌گیرد.
۲. timeout اثبات شکست نیست؛ retry ایجاد پرداخت می‌تواند شارژ دوم بسازد.
۳. ProviderReference و مرجع تسویه دو مفهوم مالی متفاوت‌اند.
۴. unique index، ترتیب lock و transaction مکمل idempotency هستند؛ Rate Limit جای آن‌ها را نمی‌گیرد.
۵. Paid و Activated را یکی نکنید؛ Outbox شکست تحویل را بدون پرداخت دوباره بازیابی می‌کند.
۶. رسید تاریخی باید از دادهٔ frozen بخواند؛ عنوان/قیمت catalog امروز مدرک دیروز نیست.
۷. آزمونِ handler + SQL، پذیرش local code است؛ هیچ جایگزینی برای ingress، Merchant/IP و تست واقعی مقصد ندارد.

## ۱۵. ترتیب مطالعهٔ کد

`CommerceController` / `CommercialSubscriptionsController` → `PaymentRecoveryController` / `ZibalCallbackController` → `OrderPaymentService` و `.Recovery` → `Order` / `PaymentAttempt` / `Payment` / `PaymentAttemptEvent` → `IPaymentGateway` / `IPaymentGatewayResolver` → `ZibalPaymentGateway` / `ZibalOptions` → `EfOrderPaymentStore` و `.History` → EF Configuration → Migration → Unit و Integration Tests.

## ۱۶. مرحلهٔ بعد

اپراتور باید Merchant خصوصی، domain/HTTPS، IP خروجی و Return allowlist را برای Test و Production جدا تکمیل کند؛ سپس بسته/Backup/Migration و smoke Test با مرورگر و callback واقعی انجام شود. پذیرش مالی Production پس از Test و با مجوز مستقل است. Refund، فروشگاه‌ها یا قواعد قیمت‌گذاری تازه را با این فعال‌سازی مخلوط نکنید.
