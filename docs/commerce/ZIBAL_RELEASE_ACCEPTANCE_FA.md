# پذیرش تکمیلی و انتشار دو محیط زیبال

## ۱. خلاصه مرحله

مبنای این پذیرش `0e27cd5fd66d01825c9974e84bfa13be05451e25` روی `feature/zibal-ipg` است. Adapter و چرخهٔ پرداخت قبلاً وجود داشتند؛ این مرحله اثبات بازیابی با نشست تازه، دو Verify هم‌زمان، تعارض trackId، شکست Inquiry و سازگاری SQL با دادهٔ تاریخی را تکمیل می‌کند. هیچ کد runtime، Endpoint، مدل یا Migration تازه‌ای ایجاد نشده است.

## ۲. معماری

Domain/Application/Infrastructure/Api دست‌نخورده‌اند. تست‌های جدید در Integration هستند چون رفتار مورد اثبات ترکیبی از HTTP، نشست واقعی تست، SQL Server، constraint و Outbox است؛ stub فقط مرز شبکهٔ زیبال را جایگزین می‌کند. تست Unit به‌تنهایی رقابت SQL یا حفظ داده هنگام Up را اثبات نمی‌کند. جهت Onion و Modular Monolith همان قبلی است.

## ۳. فایل‌های ایجادشده

- `tests/MakanApp.IntegrationTests/ZibalPaymentEndpointsTests.Acceptance.cs`: بازیابی پس از بستن client و ورود دوباره، دو recovery هم‌زمان و برخورد شناسهٔ Provider بین دو سفارش. نکتهٔ آموزشی: با retry باید اثر تجاری را بشماریم، نه فقط HTTP 200 را.
- `tests/MakanApp.IntegrationTests/ZibalPaymentMigrationAcceptanceTests.cs`: ساخت دیتابیس GUIDدار اختصاصی LocalDB، افزودن داده با schema قبلی، Up، اجرای مجدد SQL و بررسی حفظ داده/rowversion. نکته: fixture قدیمی نباید به ستون جدید وابسته باشد.
- همین سند: مرز شواهد، مسیر مطالعه و محدودیت پذیرش را ثبت می‌کند؛ گزارش و راهنمای هر انتشار کنار ZIP خصوصی آن ساخته می‌شوند.

## ۴. فایل‌های تغییرکرده

- `ZibalPaymentEndpointsTests.Helpers.cs`: ورود دوباره با همان شمارهٔ تست، رزرو trackId و پاسخ‌های کنترل‌شدهٔ Inquiry 500/بدنهٔ خالی اضافه شده‌اند. رفتار سرویس واقعی تغییر نکرده است.
- `ZibalPaymentEndpointsTests.cs`: دو حالت شکست Provider و assertion نشت Callback/token به تست موجود افزوده شده‌اند. ورودی‌ها تصادفی و صرفاً در حافظهٔ fixture هستند؛ credential واقعی استفاده نمی‌شود.

## ۵. جریان واقعی مورد آزمون

```text
HTTP
-> CommerceController / PaymentRecoveryController / ZibalCallbackController
-> OrderPaymentService
-> Order / PaymentAttempt / Payment
-> IPaymentGateway / IOrderPaymentStore
-> ZibalPaymentGateway / EfOrderPaymentStore
-> SQL Server: Payment + Order.Paid + Outbox
-> Payment status / Receipt / HTTP

Outbox worker -> EfSubscriptionActivationHandler -> Subscription
```

نام کلاس‌ها مربوط به پیاده‌سازی موجود است؛ در این مرحله کلاس runtime تازه‌ای ساخته نشده است. Callback تنها hint است. Verify و در حالت 201، Inquiry باید مبلغ، Order correlation و وضعیت معتبر Provider را ثابت کنند. Adapter به‌صورت مستقیم Subscription نمی‌سازد. پاسخ Paid می‌تواند هنوز `SubscriptionActivationPending` باشد؛ worker پس از Commit و مستقل از پاسخ HTTP، fulfillment را تکمیل می‌کند.

## ۶. دیتابیس و Migration

Migration موجود `20261110081503_AddZibalPaymentIntegration` مبنای schema است. شش ستون nullable و جدول `commerce.PaymentAttemptEvents` افزوده شده‌اند؛ دادهٔ تاریخی مالی backfill نمی‌شود. FK جدول event حذف زنجیره‌ای ندارد؛ CHECK نوع event و unique indexهای پرداخت و rowversion سفارش/تلاش حفظ می‌شوند.

سه حالت پذیرش واقعی SQL عبارت‌اند از:

1. baseline قبل از زیبال + پرداخت تاریخی → EF Up → اجرای دوبارهٔ delta.
2. همان baseline → delta Up → اجرای دوبارهٔ delta.
3. دیتابیس خالی → SQL کامل → درج دادهٔ پرداخت → اجرای دوبارهٔ SQL کامل.

در هر حالت، سفارش Paid، تلاش Succeeded، پرداخت، مبلغ/مرجع/زمان قدیمی و rowversion موجود ثابت می‌مانند؛ ستون‌های تازه NULL و history مصنوعی صفر است. FK و CHECK نامعتبر واقعاً با SQL 547 رد می‌شوند. SHA فایل SQL اجراشده ثبت و با فایل تحویلی مقایسه می‌شود. فقط دیتابیس اختصاصی GUIDدار LocalDB ساخته و پس از تست حذف می‌شود؛ `MakanApp`، `MakanApp_Test` و دیتابیس سرور تغییر نمی‌کنند.

## ۷. قرارداد HTTP

مسیرهای دقیق و schemaها در OpenAPI همان Release هستند. Callback موجود `GET /api/integrations/v1/commerce/payments/zibal/callback` بدون نشست است؛ پارامترهای Provider مجوز تسویه نیستند. status/recovery/receipt نشست فعال و مالکیت حساب یا مجوز جاری سازمان می‌خواهند. cross-user/cross-org با 404 پنهان می‌شوند. تعارض trackId، `PAYMENT_REFERENCE_CONFLICT` است؛ Provider غیرفعال پاسخ امن `PAYMENT_PROVIDER_UNAVAILABLE` می‌دهد. Endpoint یا DTO تازه‌ای در این مرحله اضافه نشده است.

| Method و Route | ورودی و خروجی | اعتبارسنجی و مجوز |
|---|---|---|
| `GET /api/integrations/v1/commerce/payments/zibal/callback` | query شامل orderId تلاش با فرمت N، trackId مثبت canonical، success و status؛ پاسخ عادی 303 به Return ثابت | anonymous و rate-limited؛ query تکراری/نامعتبر رد؛ پرداخت فقط با اثبات Provider؛ تنظیم ناقص 503 |
| `GET /api/v1/commerce/payment-attempts/{attemptId}` | Guid؛ `PaymentAttemptDetailsDto` با Order/Attempt/history امن، 200 | نشست و مالک/Manager مجاز جاری؛ 401 یا 404 |
| `POST /api/v1/commerce/payment-attempts/{attemptId}/verify` | Guid و بدون body؛ `VerifyPaymentResult`، 200 | نشست، مالکیت و rate limit؛ replay، conflict یا unavailable مطابق ProblemDetails |
| `GET /api/v1/commerce/orders/{orderId}/receipt` | Guid؛ `PaymentReceiptDto`، 200 | مجوز سازمانی جاری و سفارش Paid؛ ناموجود/غیرمجاز 404 |
| `GET /api/v1/commerce/personal/orders/{orderId}/receipt` | Guid؛ `PaymentReceiptDto`، 200 | مالک حساب شخصی؛ ناموجود/غیرمجاز 404 |

## ۸. مفاهیم استفاده‌شده

Idempotency به معنی یک اثر محلی پایدار است؛ نه تضمین یک‌بار تحویل شبکه. verify 201 بدون Inquiry معتبر، اثبات پرداخت نیست. Outbox اثر پرداخت و درخواست fulfillment را اتمیک نگه می‌دارد، ولی پردازش تحویل ممکن است تکرار شود و handler باید idempotent باشد. script idempotent با اجرای دو بار روی SQL واقعی بررسی می‌شود؛ صرف وجود عبارت IF کافی نیست.

## ۹. تصمیم‌های طراحی

به‌جای تغییر مجدد Authentication، نشست تازه با fixture موجود ایجاد شده است. به‌جای reset دیتابیس توسعه، fixture LocalDB نام‌گذاری و کنترل مالکیت دارد. به‌جای فعال‌سازی ناقص Zibal، بستهٔ فاقد Merchant/Callback معتبر با `Payments:Provider=Unavailable` ساخته می‌شود. این انتخاب امکان شروع امن برنامه را حفظ می‌کند و پرداخت واقعی را عمداً فعال نمی‌کند.

## ۱۰. تست‌ها

هشت case جدید Integration وجود دارد: سه case در فایل Acceptance، دو ورودی جدید نظریهٔ InvalidProviderProof و سه case مهاجرت. تست نشت موجود نیز برای callback dump و bearer token تقویت شده است. تست‌های قبلی برای fake/duplicate callback، 201 recovery، callback+recovery race، amount/order mismatch، مالکیت، immutable history و شکست/بازیابی fulfillment دوباره اجرا می‌شوند. هیچ تستی Skip یا ضعیف نشده است.

## ۱۱. Validation

Clean/Restore/Build اولیه و final Debug Build با صفر Warning/Error موفق شدند. focused Commerce Unit برابر ۱۰۶ و Integration برابر ۶۸ موفق بودند؛ تست تقویت‌شدهٔ نشت اطلاعات نیز سه case موفق داشت. رگرسیون اول و دور نهایی کامل، هر دو: Unit=1250، Integration=1828، Architecture=14؛ مجموع 3092، شکست=0 و Skip=0. نتیجه از Counters واقعی TRX و حضور متدهای پذیرش تطبیق داده شده است؛ اجرای اولیه با فیلتر اشتباه که صفر تست پیدا کرد، موفقیت تست محسوب نشده و اجرای صحیح سه‌حالته جایگزین آن است.

## ۱۲. Git و آثار انتشار

Commit این مرحله فقط چهار فایل تست بالا و این سند را شامل می‌شود. سه فایل untracked AI، چهار فایل تنظیمات واقعی و User Secrets با SHA-256 baseline کنترل می‌شوند و وارد Commit نمی‌شوند. hash نهایی Commit، Push و remote verification در گزارش خصوصی همان Release درج می‌شود. main خودکار merge نمی‌شود.

دو ZIP باید از یک Release build و یک publish با `--no-build` ساخته شوند. کد و DLL مشترک یکسان‌اند؛ تنظیمات محیط و web.config جدا هستند. SQL کامل/delta، OpenAPI، راهنمای Test/Production، Rollback و SHA256SUMS کنار ZIPها قرار می‌گیرند. شروع موفق هر ZIP استخراج‌شده فقط روی loopback و SQL/Storage موقت بررسی می‌شود.

## ۱۳. محدودیت‌ها

شاهد رسمی قبلی HTTP200/result100 فقط `ZIBAL_LIVE_REQUEST_SMOKE` است؛ پرداخت بانکی End-to-End نیست. public Test callback و Merchant/HTTPS معتبر Production آماده نیستند، بنابراین Live Callback/Verify و Bank E2E اجرا نشده‌اند. بسته‌ها Provider غیرفعال دارند. دسترسی مدیریتی سرور نداریم؛ Deploy، Queue و Workers مقصد تأیید نشده‌اند. AI/Knowledge در کانفیگ بسته فعال و Service Authentication هر دو خاموش می‌مانند؛ پاسخ زندهٔ آن‌ها در این Task آزمایش نمی‌شود. Secretهای موجود فقط در ZIP خصوصیِ مجاز حفظ می‌شوند، نه در Git یا گزارش.

## ۱۴. نکات آموزشی

1. success ظاهری Callback را با authority سرور اشتباه نگیرید.
2. در رقابت، تعداد Payment، Outbox و Subscription را کنار status پاسخ کنترل کنید.
3. replay مالی نباید نقش، عضویت یا دسترسی آموزشی بسازد.
4. سازگاری Migration را با رکورد واقعاً دارای schema قبلی آزمایش کنید.
5. hash فایل تحویلی را با SQL واقعاً اجراشده تطبیق دهید.
6. Build مشترک، تفاوت کد بین محیط‌ها را حذف می‌کند؛ مجوزها و کانفیگ همچنان محیطی‌اند.

## ۱۵. مسیر مطالعه

`CommerceController`/`PaymentRecoveryController`/`ZibalCallbackController` → `OrderPaymentService` → `Order`/`PaymentAttempt` → `ZibalPaymentGateway` → `EfOrderPaymentStore` → Migration زیبال → `ZibalPaymentEndpointsTests` و `.Acceptance` → `ZibalPaymentMigrationAcceptanceTests` → `ZIBAL_OPERATIONS_FA.md` → گزارش Release.

## ۱۶. مرحله بعد

اپراتور مجاز باید Backup، schema، IIS و smoke مقصد را طبق اسناد همان Release اجرا کند. فعال‌سازی واقعی زیبال نیازمند Merchant و public HTTPS callback/return معتبر و پذیرش بانکی جداست. Deploy یا فعال‌سازی پرداخت با موفقیت تست محلی یکی فرض نشود.
