# برنامه‌ی Load و Performance

اسکریپت `tools/production/load/k6-critical-apis.js` فقط باید روی staging یا SQL Server ایزوله اجرا شود. `CONFIRM_ISOLATED_STAGING=YES` gate اجباری است؛ Production مجاز نیست.

انتشار جاری مالک Production/HTTP است و فایل appsettings.Staging.json حذف شده؛ این تغییر هیچ load test یا محیط staging تازه‌ای را فعال نمی‌کند. نام تاریخی پارامتر ایمنی ابزار حفظ شده است. نبود محیط ایزوله دلیل مجاز شدن تست بار روی Production نیست و تأیید استثنای HTTP، نتیجهٔ Performance یا smoke امن ایجاد نمی‌کند.

## داده و سناریوها

- access tokenهای test برای نقش‌های Student، Manager و Platform operator.
- conversation و class دارای داده‌ی مصنوعی و قابل حذف.
- read mix: conversation list، contacts، dashboard، report، commerce summary، message history و backoffice list.
- authentication burst: فقط با `ENABLE_OTP_BURST=YES` و provider/test phone کنترل‌شده؛ به‌صورت پیش‌فرض خاموش است.
- send message: export جداگانه‌ی `sendMessage`، فقط با `ENABLE_MESSAGE_SEND=YES` و conversation test.
- report creation و file upload باید در test جدا با cleanup و سقف داده اجرا شوند؛ در read mix پیش‌فرض mutation ندارند.

نمونه اجرا:

```powershell
$env:BASE_URL = 'https://staging-api.example.test'
$env:CONFIRM_ISOLATED_STAGING = 'YES'
$env:ACCESS_TOKEN = '<ephemeral-test-token>'
$env:ADMIN_ACCESS_TOKEN = '<ephemeral-test-admin-token>'
$env:CONVERSATION_ID = '<test-conversation-id>'
k6 run tools/production/load/k6-critical-apis.js
```

برای send controlled:

```powershell
$env:ENABLE_MESSAGE_SEND = 'YES'
k6 run -e CONFIRM_ISOLATED_STAGING=YES -e BASE_URL=$env:BASE_URL -e ACCESS_TOKEN=$env:ACCESS_TOKEN -e CONVERSATION_ID=$env:CONVERSATION_ID --exec sendMessage tools/production/load/k6-critical-apis.js
```

## خروجی مورد نیاز

p50، p95، p99، throughput، error rate، تعداد 429/5xx و queryهای کند SQL ثبت شوند. threshold موجود صرفاً smoke guard برای 5xx است و SLA محصول محسوب نمی‌شود. SLA و ظرفیت تنها پس از baseline چند مرحله‌ای و با hardware/profile واقعی تعیین می‌شوند.

در STEP 16 baseline اجرا نشد، چون staging ایزوله و k6 در اختیار این workspace تأیید نشده بود. این محدودیت باید پیش از Production بسته شود.
