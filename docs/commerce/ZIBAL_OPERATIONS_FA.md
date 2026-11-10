# عملیات و انتشار زیبال

## مرز آمادگی

در این مرحله فقط Backend و تست‌ها تکمیل می‌شوند. هیچ استقرار IIS، تغییر دیتابیس MakanApp/MakanApp_Test یا پرداخت واقعی مجاز/اجراشده فرض نشود. تا Merchant واقعی، HTTPS عمومی Callback، آدرس ثابت بازگشت، ثبت IP/domain در پنل و smoke واقعی تأیید نشده‌اند، `REAL_PAYMENT_READY=NO` است. نتیجهٔ واقعی Build/Test و آزمایش رسمی در گزارش نهایی ثبت می‌شود؛ این سند ادعای PASS محیط مقصد ندارد.

## تنظیمات

تنظیم پایه و دو فایل `.example` شکل یکسان `Payments:Zibal` دارند. پیش‌فرض Provider=Unavailable و Merchant خالی حفظ شده تا کانفیگ ناقص بی‌صدا پرداخت را فعال نکند. هنگام فعال‌سازی Test، Merchant را صریحاً `zibal` قرار دهید و URLها را تکمیل کنید؛ فایل‌های خصوصی Production/Test و secretهای فعلی دست‌نخورده‌اند. این بلوک در فایل خصوصی یا Environment مقصد قرار می‌گیرد:

```json
{
  "Payments": {
    "Provider": "Zibal",
    "InitiationClaimLeaseSeconds": 30,
    "Zibal": {
      "BaseUrl": "https://gateway.zibal.ir",
      "Merchant": "",
      "CallbackUrl": "",
      "ClientReturnUrl": "",
      "AllowedClientReturnOrigins": [],
      "TimeoutSeconds": 15
    }
  }
}
```

رشته‌های خالی باید توسط اپراتور با مقادیر معتبر همان محیط تکمیل شوند؛ با انتخاب Zibal و خالی‌بودنشان Startup عمداً fail-fast است. مقدار Merchant واقعی را در Git، چت، Swagger یا log نگذارید. نام متغیرها:

```text
Payments__Provider
Payments__Zibal__BaseUrl
Payments__Zibal__Merchant
Payments__Zibal__CallbackUrl
Payments__Zibal__ClientReturnUrl
Payments__Zibal__AllowedClientReturnOrigins__0
Payments__Zibal__TimeoutSeconds
```

Test: محیط `Test` یا fixture `Testing`، Merchant عمومی `zibal`، دیتابیس آزمایشی مجزا، Callback و صفحهٔ بازگشت Test. Production/Staging: Merchant واقعی، Callback و Return با HTTPS. `zibal` در Production/Staging/Development رد می‌شود. Test با Merchant واقعی هم رد می‌شود. محیط Test را به دیتابیس Production وصل نکنید؛ Startup فقط از نام Environment نمی‌تواند مالکیت/محتوای یک SQL Server را اثبات کند. درآمد یا تحویل آزمایشی فقط در دیتابیس Test معنا دارد و درآمد واقعی نیست.

Callback باید دقیقاً مسیر `/api/integrations/v1/commerce/payments/zibal/callback` داشته باشد؛ query/fragment/userinfo تنظیمی مجاز نیست. Return باید HTTP(S)، بدون query/fragment/userinfo و با origin برابر یکی از AllowedClientReturnOrigins باشد. origin شامل scheme/host/port است؛ مسیر جداگانهٔ Return ثابت است. Client نمی‌تواند Return، Callback یا BaseUrl را تغییر دهد. در Production هر دو URL باید HTTPS باشند؛ استثنای HTTP قدیمیِ host مجوز HTTP درگاه تولید نیست. BaseUrl فقط origin رسمی زیبال است؛ Redirect در HttpClient خاموش و زمان عملیات ۵ تا ۳۰ ثانیه محدود است.

## استقرار و Migration

۱. Backup دیتابیس و release فعلی را طبق Runbook موجود انجام دهید؛ دسترسی محیط مقصد این Task فراهم نیست.
۲. history واقعی مقصد را بخوانید و SQL نسخهٔ مناسب را بازبینی کنید. Migration این مرحله `20261110081503_AddZibalPaymentIntegration` است. Up فقط شش ستون nullable و جدول PaymentAttemptEvents با FK Restrict و index می‌افزاید؛ backfill یا دادهٔ مالی حدسی ندارد. Migrationهای قبلی تغییر نکرده‌اند.
۳. Script idempotent را با exporter موجود تهیه کنید؛ اجرای آن روی مقصد وظیفهٔ اپراتور مجاز است:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/production/export-migration-sql.ps1 -OutputPath .artifacts/zibal-deployment/migrations-safe.sql -FromMigration 20261104224922_AddSchoolMembershipManagementOperations -ToMigration 20261110081503_AddZibalPaymentIntegration -Configuration Debug -NoBuild
```

پارامترهای واقعی exporter را در همان فایل بخوانید؛ از SQL عمومی EF که خطای compile ستون تازه می‌دهد به‌جای exporterِ دارای batch/deferred SQL استفاده نکنید. اجرای Migration در startup اضافه نشده است. Down دادهٔ تاریخچه/مراجع تازه را حذف می‌کند؛ بعد از پرداخت واقعی rollback خودکار schema انجام ندهید. rollback binary باید با schema additive سازگار باشد؛ رسید Paid، Outbox یا Subscription را پاک نکنید.
۴. مقادیر خصوصی محیط، Merchant و نام دیتابیس را بدون نمایش secret کنترل کنید. Test و Production نباید connection مشترک داشته باشند. تنظیمات SMS، AI، OTP و roleها برای این کار عوض نمی‌شوند.
۵. HTTPS ingress را به callback مجاز کنید؛ این GET نیازمند Service JWT/نشست یا Basic Auth سراسری نیست، ولی محدودیت IP درخواست دارد. business APIها همچنان نشست و مجوز جاری می‌خواهند. Reverse proxy باید IP واقعی را فقط از proxyهای مورد اعتماد بگیرد؛ UFW/IIS را در این Task تغییر نداده‌ایم.
۶. در پنل زیبال، domain سایت/Callback و IP خروجی سرور را مطابق [راهنمای رسمی](https://help.zibal.ir/article/how-get-and-set-up-online-payment-gateway/) ثبت کنید. محدودیت Referer/domain و IP را برای محیط واقعی جدا بررسی کنید؛ Host دلخواه ارسالی Client را به upstream کپی نمی‌کنیم.

## Smoke رسمی Test

Unit/Integration اصلی اینترنت را صدا نمی‌زنند. آزمایش رسمی با Merchant عمومی، اقدام جدا و با برچسب Test است. در محیط Test دارای دیتابیس مستقل، سفارش آزمایشی موجود بسازید و از Initiate، URL `/start/{trackId}`، callback، status و receipt استفاده کنید. مبلغ نمونه ۱۰۰۰ ریال فقط دادهٔ تست است و قیمت محصول واقعی نیست. فرم یا تأیید بانک واقعی را خودکار نکنید و با Merchant واقعی آزمایش خودسرانه انجام ندهید.

فقط برای کنترل قرارداد Request بدون انجام پرداخت، در ترمینال خصوصیِ اپراتور و با Callback صحیح Test:

```powershell
$testCallback = Read-Host 'آدرس کامل Callback محیط Test'
$requestBody = @{
  merchant = 'zibal'
  amount = 1000
  callbackUrl = $testCallback
  description = 'MakanApp official sandbox contract check'
  orderId = [Guid]::NewGuid().ToString('N')
} | ConvertTo-Json
$reply = Invoke-RestMethod -Method Post -Uri 'https://gateway.zibal.ir/v1/request' -ContentType 'application/json' -Body $requestBody -TimeoutSec 15
[pscustomobject]@{ Result = $reply.result; TrackIdPresent = ($reply.trackId -gt 0) }
```

این probe فقط upstream Request را بررسی می‌کند؛ چون Order/Attempt محلی ایجاد نکرده، پذیرش E2E یا تحویل اشتراک نیست. پیام خام Provider یا کارت را چاپ/ذخیره نکنید. اگر شبکه/IP/domain مانع شد، همان مانع را ثبت کنید؛ تست fake را Live PASS نام‌گذاری نکنید. برای پرداخت واقعی ابتدا Test E2E کامل و مجوز مالی مستقل لازم است.

## Smoke سمت Backend Test پس از استقرار توسط مالک

آدرس شناخته‌شدهٔ Test برابر `http://94.184.32.84:8090` است؛ این سند تأیید استقرار تازه در آن نیست. HTTP برای credential نشست محرمانگی شبکه ندارد؛ دستور دارای نشست را فقط روی مسیر آزمایشی مورد اعتماد و حساب/دادهٔ Test اجرا کنید و برای انتشار مالی Production از HTTPS استفاده کنید. ابتدا Binary/Schema تازه، محیط Test، اتصال مستقل MakanApp_Test و تنظیمات کامل Merchant تست را کنترل کنید. session را از جریان ورود موجود در ترمینال خصوصی تهیه کنید؛ token/OTP را در چت یا فایل مشترک نگذارید.

نمونهٔ خرید شخصی با پلن آزمایشیِ منتشرشده و قیمت معتبرِ موجود؛ این فرمان‌ها در این Task روی سرور اجرا نشده‌اند:

```powershell
$testApi = 'http://94.184.32.84:8090'
if ([string]::IsNullOrWhiteSpace($env:MakanTest__AccessToken)) { throw 'نشست Test در محیط خصوصی آماده نیست.' }
$testHeaders = @{ Authorization = 'Bearer ' + $env:MakanTest__AccessToken }
$versionId = Read-Host 'PlanVersionId منتشرشدهٔ شخصیِ مجاز در دیتابیس Test'
$orderBody = @{ planVersionId = $versionId; clientOperationId = [Guid]::NewGuid().ToString() } | ConvertTo-Json
$order = Invoke-RestMethod -Method Post -Uri "$testApi/api/v1/commerce/personal/orders" -Headers $testHeaders -ContentType 'application/json' -Body $orderBody
$paymentBody = @{ clientOperationId = [Guid]::NewGuid().ToString() } | ConvertTo-Json
$payment = Invoke-RestMethod -Method Post -Uri "$testApi/api/v1/commerce/personal/orders/$($order.order.orderId)/payments" -Headers $testHeaders -ContentType 'application/json' -Body $paymentBody
$attemptId = $payment.paymentAttempt.paymentAttemptId
if ($payment.paymentAttempt.status -eq 'PendingVerification') { Start-Process -FilePath $payment.paymentAttempt.paymentUrl }
$state = Invoke-RestMethod -Method Get -Uri "$testApi/api/v1/commerce/payment-attempts/$attemptId" -Headers $testHeaders
$state | Select-Object -ExpandProperty PaymentAttempt | Select-Object PaymentAttemptId, Status, FailureCode
```

اگر پاسخ گم شد، همان orderBody/paymentBody و شناسهٔ عملیات قبلی را حفظ کنید؛ دوباره Guid نسازید. پس از بازگشت از مرورگر یا callbackِ گم‌شده، بازیابی همان تلاش، بدون درخواست ایجاد مجدد پرداخت:

```powershell
$recovered = Invoke-RestMethod -Method Post -Uri "$testApi/api/v1/commerce/payment-attempts/$attemptId/verify" -Headers $testHeaders
$recovered | Select-Object Status
if ($recovered.order.status -eq 'Paid') {
  $receipt = Invoke-RestMethod -Method Get -Uri "$testApi/api/v1/commerce/personal/orders/$($order.order.orderId)/receipt" -Headers $testHeaders
  $receipt | Select-Object OrderId, Amount, Currency, FulfillmentState, ProviderReference, SettlementReference
}
```

تکرار callback/recovery باید همان پرداخت را بدهد؛ تغییر قیمت catalog نباید مبلغ/عنوان رسید را عوض کند؛ حساب دیگر و Manager سازمان دیگر باید رد شوند. برای خرید سازمانی مسیر `/orders` و Workspace/Manager جاری همان سازمان لازم است. در Production ابتدا فقط health/readiness، startup guard و read API مجاز را بررسی کنید؛ Merchant تست هرگز پذیرفته نشود. اجرای مالی واقعی، مقدار پرداخت و پنل merchant به مجوز مالی/عملیاتی مستقل نیاز دارند و در این Task تست نشده‌اند.

## بازیابی و Incident

- `Unknown`: هرگز Failed قطعی فرض نشود. همان Attempt را با API مجاز بازیابی کنید؛ خودکار پرداخت تازه نسازید. POST Request هیچ retry کور ندارد؛ lease منقضی به Unknown می‌رود.
- Request timeout بدون trackId و callback مفقود: API اجازهٔ حدس trackId یا charge مجدد نمی‌دهد. پشتیبانی با گزارش رسمی پنل و شناسهٔ N-format تلاش بررسی کند؛ reconciliation مخرب/تطبیق دستی Paid از این مرحله وجود ندارد.
- Verify timeout/5xx/JSON ناقص: Unknown؛ با همان مرجع Recover. کد ۲۰۱ فقط با Inquiry کامل result=100/status=1 و مبلغ/orderId صحیح قابل تسویه است. Inquiryِ pending، failed، refunded یا اطلاعات ناقص پرداخت جدید نمی‌سازد.
- callback جعلی: success/status مرجع نیستند؛ provider/reference/order correlation و پاسخ server-to-server لازم‌اند. قبل از اثبات، callback نمی‌تواند trackId یک تلاش فاقد مرجع را تصاحب کند.
- `Paid + SubscriptionActivationPending`: دوباره پرداخت نکنید؛ Outbox و SourceOrderId را بررسی کنید. handler فعلی idempotent است؛ تغییر Paid به Failed برای retry یا refund ممنوع است.
- تغییر Provider پیش‌فرض: تلاش قبلی با Provider ذخیره‌شده بازیابی می‌شود؛ تا تعیین تکلیف تلاش‌های باز، تنظیمات/credential همان Provider را حذف نکنید. Merchant متعلق به حساب دیگری را برای تطبیق همان trackId جایگزین نکنید.
- `refNumber` با `trackId` متفاوت است. ProviderPaidAt فاقد timezone حدس زده نمی‌شود؛ زمان‌های Backend UTC هستند. مقادیر تاریخی nullable فاقد مدرک، null باقی می‌مانند.

برای پیگیری از OrderId، PaymentAttemptId، trace/correlation و PaymentAttemptEvents استفاده کنید. callback timestamp نخستین دریافت را نگه می‌دارد؛ تکرارها رخداد جدید ولی تسویه/تحویل تکراری ندارند. جدول history append-only در مرز SaveChanges است و payload/provider message/card/merchant ندارد؛ History API فقط ۱۰۰ رخداد اخیر را می‌خواند. حفظ/حذف دوره‌ای این داده هنوز policy جدا می‌خواهد. SQL مستقیم توسط DBA از guard برنامه عبور می‌کند؛ مجوز DB و backup مستقل لازم‌اند.

Admin Finance مسیر read و permission قبلی خود را دارد و وضعیت Order/Attempt/Fulfillment، زمان و مراجع masked را می‌بیند. UI مدیریت Secret/Provider در DB ساخته نشده است. readiness تراکنش بانکی نمی‌سازد؛ اعتبارسنجی تنظیمات در Startup انجام می‌شود و سلامت بانک فقط از آزمایش کنترل‌شده/پایش عملیات معلوم می‌شود.

`REFUND=OUT_OF_SCOPE_FOR_THIS_TASK`؛ Refund/Reversal، native store receipts، قیمت/دوره/سقف حدسی، Share Plan و سازمان‌سازی جدید از این Task جدا هستند. پرداخت، Membership/RoleAssignment/Enrollment ایجاد نمی‌کند. فایل‌های AI و MeliPayamak این مرحله تغییر نمی‌کنند.
