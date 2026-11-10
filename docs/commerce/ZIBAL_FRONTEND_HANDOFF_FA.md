# قرارداد Backend زیبال برای کلاینت

این قرارداد مسیر Web / Direct IPG است. هیچ کد Frontend، خرید CafeBazaar/Myket/iOS، شهریه، کیف پول یا Refund در این مرحله ساخته نشده است. پرداخت، نقش یا مجوز تأسیس آموزشگاه نمی‌دهد. تأیید مدارک/سازمان، پرداخت و تحویل خدمت مستقل‌اند.

## جریان خرید

۱. با نشست معتبر، catalog/quote موجود را بخوانید؛ نسخه و مبلغ را سرور تعیین می‌کند.
۲. با `planVersionId` و `clientOperationId` یک سفارش بسازید. خرید شخصی و سازمانی مسیر جدا دارند.
۳. با `clientOperationId` ثابت برای همان اقدام، Initiate را فراخوانی و `paymentAttemptId` را نگه‌داری کنید.
۴. فقط `paymentUrl` برگشتی را در مرورگر خارجی/مسیر مجاز وب باز کنید؛ فرم کارت در ماکان نسازید. احراز بانک ممکن است جابه‌جایی برنامه بخواهد.
۵. Callback عمومی را زیبال به Backend می‌زند. Backend ابتدا Verify/Inquiry می‌کند و سپس با HTTP 303 به `ClientReturnUrl` ثابت برمی‌گرداند. پارامترهای بازگشت فقط `orderId` و `paymentAttemptId` هستند؛ هیچ `success=true` قابل اعتماد وجود ندارد.
۶. با نشست کاربر، status را بخوانید. بازگشت مرورگر، بسته‌شدن اپ، callback با success=1 یا کد ۲۰۱ زیبال به‌تنهایی نشانهٔ تسویه نیست.
۷. اگر callback نرسید یا اپ بسته شد، پس از ورود مجدد همان تلاش را Recover کنید؛ سفارش یا پرداخت تازه نسازید.

## APIها

پیشوند جدول `/api/v1/commerce` است. همهٔ مسیرهای جدول نشست opaque Bearer می‌خواهند. شخصی: خریدار همان سفارش؛ سازمانی: Manager دارای Membership/Role جاری سازمانِ Workspace. شناسهٔ منبع یا نقش در body مجوز نیست. status/recovery مالکیت را خودکار از تلاش تشخیص می‌دهد؛ context سازمانی اشتباه و حساب دیگر 404 می‌گیرند. Guardian یا رابطهٔ پلن مشترک دسترسی خودکار به رسید خصوصی نمی‌دهد.

| Method | مسیر | ورودی و نتیجه |
|---|---|---|
| POST | `/orders` | `{planVersionId, clientOperationId, purpose?, subscriptionId?}`؛ ایجاد 201، replay=200 |
| POST | `/personal/orders` | همان قرارداد برای مالک شخصی؛ ایجاد 201، replay=200 |
| POST | `/orders/{orderId}/payments` | `{clientOperationId}`؛ ایجاد 201، replay=200؛ `InitiatePaymentResult` |
| POST | `/personal/orders/{orderId}/payments` | همان قرارداد و وضعیت موجود 200؛ `InitiatePaymentResult` |
| GET | `/orders/{orderId}` | `OrderDto` و تلاش‌ها، 200 |
| GET | `/personal/orders/{orderId}` | سفارش شخصی، 200 |
| GET | `/orders?limit=20` و `/personal/orders?limit=20` | سوابق موجود؛ limit از ۱ تا ۱۰۰ |
| GET | `/payment-attempts/{attemptId}` | `PaymentAttemptDetailsDto` شامل Order، PaymentAttempt و حداکثر ۱۰۰ رخداد اخیر؛ 200 |
| POST | `/payment-attempts/{attemptId}/verify` | بدون body؛ `VerifyPaymentResult` با نتیجه و وضعیت جاری؛ 200 |
| GET | `/orders/{orderId}/receipt` | `PaymentReceiptDto` تاریخی؛ فقط سفارش Paid، 200 |
| GET | `/personal/orders/{orderId}/receipt` | همان رسید برای خریدار شخصی، 200 |

Callback مستقل: `GET /api/integrations/v1/commerce/payments/zibal/callback?success=1&status=2&trackId=...&orderId=...`؛ anonymous، محدودیت IP، بررسی server-to-server و 303 به آدرس ثابت. `orderId` در قرارداد زیبال **PaymentAttempt.Id با فرمت N، یعنی ۳۲ نویسه** است؛ با Order.Id داخلی اشتباه نشود. `success` فقط 0/1 و `trackId` رشتهٔ Int64 مثبت canonical است. پارامتر تکراری/غایب/نامعتبر 400 امن می‌دهد. callback با success=0 هم بررسی می‌شود.

## داده و حالت‌های UX

`Amount` مبلغ canonical ریال است. قرارداد عمومی decimal موجود تغییر نکرده؛ Adapter زیبال فقط ریال صحیح از ۱۰۰۰ تا ۴۰۰۰۰۰۰۰۰۰ را می‌پذیرد. تبدیل تومان صرفاً نمایشی است؛ کلاینت amount/currency/trackId را برای Verify تعیین نمی‌کند. سقف درگاه واقعی ممکن است پایین‌تر باشد.

`PaymentAttemptDto.ProviderReference` همان trackId **رشته‌ای** است؛ `SettlementReference` همان refNumber است. این دو را یکی نکنید. FailureCode، CallbackReceivedAtUtc، VerifiedAtUtc و ProviderPaidAtUtc افزوده شده‌اند. زمان Provider بدون timezone به UTC حدسی تبدیل نمی‌شود و در آن حالت ProviderPaidAtUtc=null است؛ PaidAtUtc داخلی زمان تسویهٔ Backend و UTC است.

| حالت | رفتار کلاینت |
|---|---|
| Created / Initiating | در حال شروع؛ با همان عملیات بخوانید/ادامه دهید؛ درخواست ایجاد جدید نسازید |
| PendingVerification | هنوز تسویه نشده؛ status یا Recovery همان تلاش |
| Unknown | نتیجه قطعی نیست؛ بازیابی همان تلاش، بدون شارژ تازه |
| Failed | شکست معتبر ثبت شده؛ اقدام جدید با عملیات جدید ممکن است مجاز باشد |
| Succeeded + Order.Paid | پول تأیید شده؛ نتیجهٔ Fulfillment را جدا نمایش دهید |
| SubscriptionActivationPending | پرداخت دوباره لازم نیست؛ تحویل Outbox در حال پیگیری است |
| SubscriptionActivated | اشتراک از مسیر فعلی فعال شده است |
| ProvisioningPending | مسیر Legacy/سازمان‌سازی مستقل؛ تسویه، تأیید تأسیس نیست |

`VerifyPaymentResult.Status` می‌تواند Settled، AlreadySettled، Failed، Unknown، AmountMismatch، CurrencyMismatch یا ReferenceConflict باشد. HTTP 200 این API به معنی موفقیت مالی همهٔ نتایج نیست؛ Status و Order.Status را بخوانید. Retry روی تلاش موفق، AlreadySettled و وضعیت جاری می‌دهد و با قطع Provider هم پرداخت دوباره نمی‌سازد.

## Idempotency و خطا

برای Retry همان اقدام، همان ClientOperationId را حفظ کنید. شناسهٔ سفارش در scope خریدار و شناسهٔ تلاش در scope سفارش است؛ تغییر Provider پیش‌فرض، تلاش قبلی را به Adapter دیگری نمی‌برد. پرداخت قبلیِ غیرقطعی، ایجاد تلاش جدید را با 409 `PAYMENT_PENDING_VERIFICATION` متوقف می‌کند. انقضای lease نیز مجوز POST مجدد به درگاه نیست.

خطاها ProblemDetails با `code`، `traceId` و `correlationId` هستند. متن پیام Provider یا stack/SQL به کلاینت منتقل نمی‌شود. 400: PAYMENT_INVALID / PAYMENT_REFERENCE_INVALID؛ 401: نشست نامعتبر؛ 403: نداشتن مجوز سفارش سازمانی؛ 404: منبع ناموجود/نامجاز؛ 409: ORDER_NOT_PAYABLE، PAYMENT_PENDING_VERIFICATION، PAYMENT_REFERENCE_CONFLICT یا تعارض عملیات؛ 429: PAYMENT_RATE_LIMITED؛ 503: PAYMENT_PROVIDER_UNAVAILABLE. با 503 سفارش Paid را به Failed تبدیل نکنید.

رسید از snapshot عنوان، نسخهٔ خریداری‌شده، مبلغ/ارز frozen، خریدار، سازمان، مراجع و زمان‌های تاریخی می‌آید. عنوان قدیمیِ فاقد snapshot برابر null می‌ماند؛ عنوان/قیمت catalog جدید جای آن قرار نمی‌گیرد. PlanVersion منتشرشده در Domain immutable است. رسید رایگانِ صریح مرجع بانکی ندارد.

کلاینت Merchant، payload Provider، شماره یا hash کارت را دریافت نمی‌کند. Admin Finance مجوز مستقل موجود خود را دارد؛ مراجع در آن read model طبق قرارداد قبلی masked هستند. Shared Plan به معنی حساب یا رسید مشترک نیست.

مرجع رسمی: [IPG زیبال](https://help.zibal.ir/ipg/) و [قرارداد جاری رسمی](https://api.zibal.ir/static/helpdocs/ipg.json)، بررسی‌شده در ۲۰۲۶-۱۱-۱۰. OpenAPI تولیدشده از host، مرجع نام و schema واقعی است. تست محلی جای پذیرش بانک/کلاینت یا استقرار Test/Production نیست.
