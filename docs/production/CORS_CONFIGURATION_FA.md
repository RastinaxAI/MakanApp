# تنظیم CORS و Host Filtering

تصمیم مالک در ۲۰۲۶-۱۰-۲۷: حالت باز یا محدود CORS فقط از Configuration تعیین می‌شود؛
نام محیط، از جمله `Test` یا `Testing`، مجوز ضمنی ایجاد نمی‌کند.
این تغییر به لایهٔ Api تعلق دارد و جریان Onion، Controller، Authentication، Session و Permission را تغییر نمی‌دهد.

## تنظیم فعلی Test و Production

هر دو فایل خصوصی `MakanApp.Api/appsettings.Test.json` و `MakanApp.Api/appsettings.Production.json`
و نمونه‌های بدون Secret آن‌ها این مقادیر غیرمحرمانه را دارند:

```json
{
  "Cors": {
    "AllowAnyOrigin": true,
    "AllowedOrigins": []
  },
  "AllowedHosts": "*"
}
```

فایل پایه نیز فلگ true را صریح دارد؛ فایل Development بخش CORS جداگانه ندارد و آن را به ارث می‌برد.
نبود خود فلگ در تنظیمات مؤثر به معنی false است، نه مجوز وابسته به محیط.
فایل‌های خصوصی در Git وارد نمی‌شوند و Connection Stringها و Secretهای آن‌ها با تغییر CORS دست‌نخورده می‌مانند.
دیتابیس‌های عملیاتی مالک `MakanApp` و `MakanApp_Test` هستند؛ این تغییر هیچ اتصال یا schema را تغییر نمی‌دهد.

در حالت true، Default Policy از `AllowAnyOrigin/AllowAnyHeader/AllowAnyMethod` استفاده می‌کند؛
فهرست AllowedOrigins در تصمیم استفاده نمی‌شود. `Access-Control-Allow-Origin: *` برمی‌گردد.
`AllowCredentials()` وجود ندارد و اضافه نشده است. بازبودن CORS یا پذیرش Host دلخواه، ورود و مجوز API نیست.

## حالت محدود

```json
{
  "Cors": {
    "AllowAnyOrigin": false,
    "AllowedOrigins": [
      "https://makanapp.ir",
      "https://admin.makanapp.ir"
    ]
  }
}
```

با false، فهرست خالی یا مقدار خالی/wildcard باعث خطای Startup می‌شود؛ مقدار `*` داخل allowlist
نباید فلگ false را به حالت باز تبدیل کند. برای Host Filtering نیز مستقل، مثلاً `AllowedHosts=api.makanapp.ir` تنظیم کنید.
در Production/Staging قواعد قبلی Origin معتبر، HTTPS و استثنای صریح HTTP در حالت محدود حفظ شده‌اند.
فلگ CORS مجوز خاموش‌کردن HTTPS، SQL encryption، proxy allowlist یا کنترل‌های هویتی نیست.

در حالت محدود، Methodها همان `GET, POST, PUT, PATCH, DELETE, OPTIONS` و Headerهای مجاز همان
`authorization, content-type, x-client-version, X-Correlation-ID` هستند.
در هر دو حالت، `Retry-After` و `X-Correlation-ID` expose می‌شوند و Preflight Max-Age برابر یک ساعت است.
OPTIONS معتبر در CORS رسیدگی می‌شود؛ درخواست اصلی همچنان به Authentication و Authorization می‌رود.

## تقدم Configuration و اعمال تغییر

فایل پایه، سپس فایل همان Environment خوانده می‌شود؛ Environment Variable و command line می‌توانند آن‌ها را override کنند.
نام محیط سرور آزمایشی مالک `Test` است و فایل آن `appsettings.Test.json` است.
در `web.config` مخزن و Publish Profileها override مخصوص CORS/AllowedHosts وجود ندارد.
متغیرهای مؤثر مهم: `Cors__AllowAnyOrigin`، `Cors__AllowedOrigins__0` و `AllowedHosts`.
برای فهرست چندتایی از indexهای بعدی استفاده کنید؛ overrideهای قدیمی لیست را نیز بررسی کنید.
ثبت Policy در Startup انجام می‌شود؛ پس از تغییر مقادیر، برنامه را در پنجرهٔ عملیاتی مناسب restart/recycle کنید.
در این مرحله روی IIS یا سرور هیچ تنظیمی اعمال نمی‌شود و Publish تازه‌ای ساخته نمی‌شود؛ ZIPهای قبلی این تغییر را ندارند.
تنظیمات جدید باید همراه binary سازگار منتقل شوند؛ Validator نسخهٔ قبلی می‌تواند این مقادیر را رد کند.

## قانون schema و مسیر مطالعه

هر Key جدید Production باید با همان Path و Type در Test هم باشد؛ مقادیر می‌توانند متفاوت باشند.
`CorsConfigurationSchemaTests` تمام Path/Typeهای نمونه‌های نسخه‌بندی‌شده را با هم مقایسه می‌کند؛
Booleanهای true/false یک Type هستند. فایل‌های خصوصی نیز هنگام اجرای این کار جداگانه بررسی می‌شوند.

برای مطالعه: بخش CORS در `MakanApp.Api/Program.cs` →
`ProductionConfigurationValidator` → تنظیمات پایه/نمونه‌ها →
تست‌های CORS در `ProductionHostingTests` → `ProductionConfigurationValidationTests` →
`CorsConfigurationSchemaTests`.

تفکیک مفاهیم: Origin شامل scheme/host/port مرورگر است؛ AllowedHosts برای Host درخواست است؛
Authorization مجوز Actor/Resource را بررسی می‌کند. هیچ‌کدام جای دیگری را نمی‌گیرد.
اجرای focused/full test، سلامت سرور واقعی یا سیاست شبکهٔ آن را اثبات نمی‌کند.
