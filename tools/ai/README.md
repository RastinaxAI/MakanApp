# اجرای کنترل‌شدهٔ STEP10G

ابزار C# موجود با مجوز مالک در این تحویل ثبت شده است. پروژه بیرون از Solution می‌ماند؛
`dotnet test MakanApp.sln` هرگز به‌طور خودکار مدل واقعی یا Secret محیط را لازم ندارد.
کد قدیمی export کامل payload و ابزارهای Python استخراج/اجرای AI وارد این تحویل نشده‌اند.

## مرز امنیتی

- Target از `AiService:BaseUrl` در User Secrets همان Api یا Environment می‌آید؛ fallback داخل runner ندارد.
- loopback برای آزمون محلی مجاز است. target غیرمحلی باید HTTPS، بدون userinfo/path/query/fragment،
  دقیقاً برابر `AiLiveProbe:ApprovedOrigin` و همراه flag صریح `--allow-remote-https` باشد.
- TLS عادی سیستم فعال است؛ certificate bypass وجود ندارد. redirect در HTTP handler runner خاموش است.
- Service JWT فقط از `HmacAiServiceCredentialProvider` می‌آید؛ key/JWT/body در خروجی ثبت نمی‌شود.
- Provider OTP و داده‌ها تستی‌اند؛ ملی‌پیامک واقعی فراخوانی نمی‌شود.
- دیتابیس fixture اختصاصی `MakanApp_MessagingSafety_IntegrationTests_Step7E` روی LocalDB در آغاز و پایان
  حذف/با migration بازسازی می‌شود. **هم‌زمان با Integration suite یا runner دیگری اجرا نکنید.**
- این فرمان‌ها config فایل، IIS، AI repository یا سرور را تغییر نمی‌دهند.

## Build و تنظیمات غیرمحرمانهٔ همین process

```powershell
dotnet build tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Debug --nologo

$env:AiService__BaseUrl = 'https://195.177.255.98'
$env:AiLiveProbe__ApprovedOrigin = 'https://195.177.255.98'
```

`AiService:Authentication:Issuer/Audience/ServiceName/Scope/CurrentKeyId/CurrentKey/TokenLifetimeSeconds`
از secure configuration موجود می‌آیند. key واقعی را در command، source، Report یا چت ننویسید.
JWT فعلی Backend برابر HS256، issuer/subject `makan-backend`، audience `makan-ai` و scope
`makan.ai.invoke` است. رد 401 علت دقیق signature/kid/claim/clock را آشکار نمی‌کند؛ حدس نزنید.

## ترتیب اجرای واقعی

```powershell
dotnet run --project tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Debug --no-build -- --run-live --service-auth-only --allow-remote-https
```

Missing/Malformed و current-control با بدنهٔ `{}` اجرا می‌شوند و inference معتبر ایجاد نمی‌کنند.
current-control باید `422 / AI_INVALID_REQUEST` بدهد؛ یعنی credential پذیرفته شده و body عمداً نامعتبر است.
اگر این کنترل رد شد، claim/scope/header و business testها **NOT_RUN** می‌مانند؛ 401 با امضای نامعتبر،
اثبات بررسی issuer یا scope نیست. هر probe فقط یک درخواست، با فاصلهٔ ۱٫۱ ثانیه، و بدون retry دارد.
HTML، redirect یا JSON نامعتبر با status واقعی و کد ثابت `NON_CONTRACT_RESPONSE` ثبت می‌شوند؛ body چاپ نمی‌شود.

فقط بعد از کنترل معتبر، negativeهای signature/claim/scope و سه guard نسخه/correlation اجرا می‌شوند.
آزمون header بدنهٔ synthetic معتبر دارد تا خطای body با خطای header اشتباه نشود؛ اگر guard سرویس خراب باشد
ممکن است تا سه inference کوچک رخ دهد. این حالت load/fault injection نیست و خودکار تکرار نمی‌شود.
ابزار کلید قبلی را از هیچ مخزن/فایل AI برداشت نمی‌کند؛ rotation به تنظیم و شاهد مستقل محیط نیاز دارد.

پس از موفقیت Auth و بررسی محدودیت‌های Contract:

```powershell
dotnet run --project tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Debug --no-build -- --run-live --operation focused_three --allow-remote-https
```

هر سه Parent Child Summary، Teacher Activity Suggestions و Manager Class Insight مستقل ثبت می‌شوند.
شکست focused با `AI_RESPONSE_INVALID` یا خطای وابستگی، PASS نهایی نیست. سپس اجرای کامل:

```powershell
dotnet run --project tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Debug --no-build -- --run-live --allow-remote-https
```

اجرای کامل ۱۵ درخواست مثبت Backend دارد: Chat برای چهار نقش و ۱۱ operation دیگر. شمار درخواست AI
معادل تعداد قطعی invocation Provider نیست. scope، envelope، correlation، دادهٔ ممنوع و metric در حافظه
بررسی می‌شوند. خروجی فقط metadata امن و نتیجه است؛ وجود کد runner موفقیت مدل را ثابت نمی‌کند.

## Authorization-only بدون وابستگی به AI

این حالت را با مقصد بستهٔ loopback اجرا کنید؛ هر خروجی غیرمجاز به HTTP با شمار تماس صفر سنجیده می‌شود:

```powershell
$env:AiService__BaseUrl = 'http://127.0.0.1:1'
dotnet run --project tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj --configuration Debug --no-build -- --run-live --authorization-only
```

این حالت ۱۶ سناریوی منفی و privacy را روی Controller، session واقعی و SQL تست اجرا می‌کند؛
**Real AI Happy Path نیست**. پوشش Student شامل assignment/exam منتشرشدهٔ سازمان دیگر است؛
Parent فرزند نامرتبط/سازمان دیگر؛ Teacher کلاس/دانش‌آموز/فعالیت غیرمجاز؛ Manager کلاس/learner سازمان دیگر؛
و نیز role، draft، revocation و suspension. payload مثبت نیز نباید شناسهٔ assessmentهای سازمان دیگر را داشته باشد.

## Production و گزارش

Production همچنان وابستگی خصوصی `http://10.77.0.2:8001` از WireGuard دارد؛ تنظیمات بالا فقط process ابزارند.
فایل appsettings و Secret سرور از این دستورها ساخته یا جایگزین نمی‌شوند. Public/Production RAG همچنان DEFERRED است.
نتیجهٔ واقعی و مرز آزمون‌ها در `docs/ai-handoff/STEP10G_REAL_AI_E2E_REPORT.md` ثبت می‌شود.
