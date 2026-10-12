# STEP 13B — Order & Payment Foundation

این سند وضعیت واقعی Order، PaymentAttempt، Payment، verification و handoff اشتراک را ثبت می‌کند. Provider واقعی، callback عمومی، refund، Backoffice و provisioning سازمان در این مرحله پیاده‌سازی نشده‌اند.

## مرز معماری

مفاهیم از هم جدا هستند:

```text
Order
-> PaymentAttempt
-> IPaymentGateway
-> server-side verification
-> Payment + Paid Order + durable Outbox
-> Subscription activation handler
```

`Order` قصد خرید و شرایط تجاری frozen است. `PaymentAttempt` هر تماس مستقل با درگاه و حالت recoverable آن را نگه می‌دارد. `Payment` سند نهایی و immutable تسویه است. `Subscription` نتیجه تجاری پس از handoff است و provider هیچ‌گاه آن را مستقیم تغییر نمی‌دهد. Organization provisioning یک workflow آینده و مستقل باقی مانده است.

## قیمت versioned و Money

قیمت در `commerce.PlanVersionPrices` با رابطه یک‌به‌یک اختیاری به `PlanVersion` ذخیره می‌شود. فقط نسخه `Published` دارای قیمت قابل خرید است. نسخه‌های منتشرشده قدیمی backfill یا قیمت‌گذاری حدسی نشده‌اند و تا زمانی که نسخه قیمت‌دار تازه‌ای منتشر نشود `PLAN_NOT_PURCHASABLE` می‌دهند.

`PlanVersion.SetPrice` فقط در Draft مجاز است. مبلغ با `decimal(18,2)`، مثبت و حداکثر با دو رقم اعشار ثبت می‌شود. frontend فقط `PlanVersionId` می‌فرستد؛ Amount و Currency از نسخه در سرور resolve و روی Order کپی می‌شوند. انتشار نسخه بعدی مبلغ Order تاریخی را تغییر نمی‌دهد.

واحد canonical این foundation برابر `IRR` است و در SQL به‌صورت `char(3)` ذخیره و در API دقیقاً با متن `IRR` برگردانده می‌شود. Toman در persistence یا قرارداد پرداخت پذیرفته نمی‌شود. تبدیل نمایشی احتمالی ریال/تومان باید بعداً در policy/presentation صریح انجام شود.

## Order

Lifecycle authoritative:

```text
PendingPayment -> Paid
PendingPayment -> Cancelled
```

در این مرحله endpoint لغو یا expiration وجود ندارد؛ متد Domain لغو فقط Order پرداخت‌نشده را می‌پذیرد و refund با تغییر Status مدل نشده است. `OrganizationId` در مدل nullable است تا خرید پیش از provisioning آینده ممکن بماند، اما API فعلی فقط Organization فعال مدیر را از `AccessContext` می‌گیرد و هیچ OrganizationId دلخواهی از client نمی‌پذیرد.

Order شامل purchaser، PlanVersion، Amount/Currency frozen، `ClientOperationId`، hash درخواست، زمان‌های UTC و `rowversion` است. Unique Index روی `(PurchaserUserId, ClientOperationId)` retry یکسان را به همان Order برمی‌گرداند و reuse همان کلید با PlanVersion دیگر را conflict می‌کند.

## PaymentAttempt و حالت Unknown

هر Order می‌تواند چند PaymentAttempt تاریخی داشته باشد:

```text
Created
-> Initiating
-> PendingVerification
-> Succeeded
-> Failed
-> Unknown
```

`Unknown` به معنی «نتیجه قطعی نیست و باید reconcile شود» است؛ timeout هرگز معادل Failed فرض نمی‌شود. claim کوتاه `Initiating` با token و lease از تماس هم‌زمان دوباره برای همان attempt جلوگیری می‌کند. retry ساخت attempt با `(OrderId, ClientOperationId)` idempotent است. برای attempt تازه، مبلغ و واحد از Order کپی می‌شوند.

Provider reference با unique filtered index روی `(Provider, ProviderReference)` محافظت می‌شود. reference بین providerهای مختلف الزاماً globally unique فرض نشده است. FailureCode محدود و امن است؛ payload خام، signature، merchant secret و credential ذخیره یا در DTO منتشر نمی‌شود.

## تصمیم Payment مستقل

`commerce.Payments` جدا ایجاد شد، زیرا رکورد immutable تسویه برای reconciliation، audit و Refund/Reversal آینده ارزش مستقل دارد. Unique Indexهای OrderId و PaymentAttemptId تضمین می‌کنند هر Order فقط یک settlement نهایی و هر attempt فقط یک Payment داشته باشد. `(Provider, ProviderReference)` نیز روی Payment یکتا است.

## Provider abstraction و وضعیت واقعی integration

Application فقط `IPaymentGateway` را می‌شناسد:

```text
CreatePaymentAsync
VerifyPaymentAsync
```

پیاده‌سازی runtime فعلی `UnavailablePaymentGateway` است و `Payments:Provider=Unavailable` هیچ تماس شبکه‌ای ندارد. فقط `RecordingPaymentGateway` در پروژه Integration Test وجود دارد. URL، callback schema، signing algorithm یا credential هیچ provider واقعی اختراع نشده است. انتخاب هر نام دیگر در configuration هنگام startup رد می‌شود تا تنظیم اشتباه silently به پرداخت جعلی تبدیل نشود.

چون adapter و قرارداد callback واقعی تأیید نشده، endpoint عمومی callback ساخته نشده است. `IPaymentVerificationService` مرز داخلی قابل‌آزمون برای adapter آینده است؛ Manager نمی‌تواند status موفق، amount یا reference را به‌عنوان authority ثبت کند. adapter واقعی آینده باید callback را parse/authenticate کند و سپس verification server-to-server را از این مرز فراخوانی کند.

## مرز SQL transaction و تماس خارجی

Initiation به این ترتیب است:

```text
persist PaymentAttempt intent + commit
-> claim initiation + commit
-> call IPaymentGateway outside SQL transaction
-> persist reference/url/failure/unknown in a short transaction
```

بنابراین lock دیتابیس هنگام انتظار شبکه نگه داشته نمی‌شود. AttemptId شناسه پایدار درخواست provider است. timeout و cancellation پس از شروع call ابتدا به Unknown persist می‌شوند؛ automatic retry کور برای ایجاد charge انجام نمی‌شود.

Provider HTTP adapter واقعی وجود ندارد؛ هنگام افزودن آن باید `IHttpClientFactory`، timeout محدود و secret store استفاده شود.

## Verification و settlement

verification فقط به پاسخ authoritative provider تکیه می‌کند و Amount/Currency پاسخ را با Order مقایسه می‌کند. mismatch سفارش را Paid نمی‌کند و attempt را با حالت recoverable `Unknown` و error code امن نگه می‌دارد.

Settlement داخلی در transaction سریال‌پذیر انجام می‌شود:

```text
lock PaymentAttempt with UPDLOCK/HOLDLOCK
-> lock Order with UPDLOCK/HOLDLOCK
-> lock/check final Payment
-> verify reference + amount + currency
-> PaymentAttempt.Succeeded
-> Order.Paid
-> insert immutable Payment
-> enqueue commerce.subscription-activation-requested.v1
-> SaveChanges + commit
```

`rowversion` روی Order و PaymentAttempt optimistic concurrency را پوشش می‌دهد؛ lockها و Unique Indexها تضمین چند-instance را فراهم می‌کنند. دو verification هم‌زمان فقط یک settlement می‌سازند. callback/retry بعدی نتیجه `AlreadySettled` می‌گیرد و Payment، Outbox یا Subscription تکراری ایجاد نمی‌شود.

## handoff اشتراک و provisioning

پیام activation در همان transaction پرداخت داخل `platform.OutboxMessages` نوشته می‌شود. صف جدیدی ایجاد نشده است. `OutboxProcessor` نوع پیام را به `EfSubscriptionActivationHandler` هدایت می‌کند؛ handler در transaction همان پیام:

1. `SourceOrderId` قبلی را برای idempotency بررسی می‌کند.
2. Subscription فعال سازمان را با lock می‌گیرد.
3. Subscription قبلی را Cancel می‌کند و حذف نمی‌کند.
4. Subscription تازه را برای PlanVersion خریداری‌شده Active می‌کند.
5. Outbox را processed می‌کند.

Unique filtered index روی `Subscriptions.SourceOrderId` و index فعال سازمان از تکرار جلوگیری می‌کنند. چون billing period تأییدشده‌ای وجود ندارد، اشتراک ساخته‌شده end date ندارد؛ مدل دوره صورتحساب باید با شرایط تجاری versioned در مرحله جدا اضافه شود.

Order بلافاصله پس از settlement می‌تواند `SubscriptionActivationPending` باشد. پس از پردازش Outbox حالت read model برابر `SubscriptionActivated` می‌شود. برای Order آینده بدون Organization، حالت `ProvisioningPending` در قرارداد وجود دارد و activation سازمانی enqueue نمی‌شود. API فعلی چنین Orderی نمی‌سازد و provisioning، ایجاد سازمان و دعوت مدیر خارج از STEP 13B است.

Downgrade فقط Subscription جدید می‌سازد و داده آموزشی را حذف نمی‌کند. policy over-quota مرحله 13A بدون تغییر باقی است.

## API مدیر

همه مسیرها Bearer session معتبر، workspace سازمانی، Membership فعال و نقش جاری `Manager` می‌خواهند:

| Method | Route | ورودی/نتیجه |
|---|---|---|
| `POST` | `/api/v1/commerce/orders` | `planVersionId`, `clientOperationId`؛ Amount/Currency server-owned |
| `POST` | `/api/v1/commerce/orders/{orderId}/payments` | `clientOperationId`؛ ساخت/ادامه یک attempt و URL امن |
| `GET` | `/api/v1/commerce/orders/{orderId}` | Order، attemptهای امن و fulfillment state |
| `GET` | `/api/v1/commerce/orders?limit=20` | حداکثر ۱۰۰ Order جاری خریدار/سازمان |

POSTها rate limit مستقل `payment` دارند؛ rate limiting جای idempotency را نمی‌گیرد. Order سازمان/کاربر دیگر با 404 پنهان می‌شود. Student، Teacher و Parent مجاز نیستند. هیچ endpoint برای Plan administration، manual verification، activation مستقیم، Finance یا global order search وجود ندارد.

## SQL Server

Migration `20261012175353_AddOrderAndPaymentFoundation` این تغییرها را دارد:

- `commerce.PlanVersionPrices`: PK/FK برابر PlanVersionId، Amount `decimal(18,2)`، Currency `char(3)`، rowversion
- `commerce.Orders`: purchaser/organization/PlanVersion، lifecycle، frozen money، retry hash و rowversion
- `commerce.PaymentAttempts`: lifecycle، money، provider/reference، claim lease، retry hash و rowversion
- `commerce.Payments`: settlement immutable با unique Order/Attempt/ProviderReference
- `commerce.Subscriptions.SourceOrderId`: correlation nullable و unique filtered

Check Constraintها Amount مثبت، currency برابر IRR، enum range، hash length، lifecycle Order و سازگاری claim را enforce می‌کنند. Indexها برای purchaser، organization/status/date، attempts هر Order و reconciliation reference ایجاد شده‌اند. Migration قبلی STEP 13A و داده موجود ویرایش یا backfill نشده‌اند. Migration هنگام startup یا روی Production خودکار اجرا نمی‌شود.

## خطاهای پایدار مهم

```text
ORDER_INVALID
ORDER_NOT_FOUND
ORDER_NOT_PAYABLE
ORDER_IDEMPOTENCY_CONFLICT
ORDER_MANAGEMENT_NOT_ALLOWED
PLAN_NOT_PURCHASABLE
PAYMENT_INVALID
PAYMENT_ATTEMPT_NOT_FOUND
PAYMENT_IDEMPOTENCY_CONFLICT
PAYMENT_PROVIDER_UNAVAILABLE
PAYMENT_PENDING_VERIFICATION
PAYMENT_REFERENCE_INVALID
PAYMENT_REFERENCE_CONFLICT
PAYMENT_AMOUNT_MISMATCH
PAYMENT_CURRENCY_MISMATCH
PAYMENT_RATE_LIMITED
```

ProblemDetails جزئیات SQL، exception داخلی، secret یا payload provider را افشا نمی‌کند.

## Notification، امنیت و observability

در این مرحله پیام موفقیت کاربر تولید نشده است؛ Outbox فقط handoff durable اشتراک را حمل می‌کند تا duplicate notification ساخته نشود. notification پرداخت باید بعداً با متن privacy-safe و SourceEvent پایدار به infrastructure موجود اضافه شود.

هیچ secret پرداختی در appsettings نیست. Provider واقعی آینده باید credential را از Environment Variable، User Secrets یا Secret Manager بگیرد. DTOها merchant key، signature و payload خام ندارند. logging فعلی exceptionهای dispatcher و MessageId را ثبت می‌کند؛ adapter آینده فقط OrderId، PaymentAttemptId، Provider، reference امن و TraceId را structured log می‌کند.

## تست‌ها و مرزهای آینده

Unit Testها immutability قیمت، validation پول، frozen Order و Unknown را می‌سنجند. Integration Testها migration واقعی LocalDB، server-authoritative price، idempotency، tenant/role isolation، initiation، timeout، failure، amount/currency/reference verification، ده retry، concurrency دو verification، چند attempt، settlement و activation Outbox را پوشش می‌دهند. هیچ تستی provider واقعی را صدا نمی‌زند.

موارد عمداً خارج از این مرحله:

- provider واقعی و callback عمومی
- retry/reconciliation API برای Finance
- invoice، tax، coupon و accounting
- expiration خودکار Order و cancellation endpoint
- refund، reversal و chargeback
- billing period و renewal
- public pre-provisioning checkout
- PlatformRole و Backoffice Finance
- Notification کاربر برای پرداخت
- frontend checkout
- Contacts/Messenger STEP 14A
- AI billing/integration
