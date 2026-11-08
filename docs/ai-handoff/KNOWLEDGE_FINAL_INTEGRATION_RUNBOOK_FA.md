# اجرای Test و Cutover کنترل‌شدهٔ AI/Knowledge — ۲۰۲۶-۱۱-۰۸

> اصلاحیهٔ جاری مالک: Phase C در Production فعال است و Recovery منجمد می‌ماند. [دستور پذیرش و بسته‌های روشن Test/Production](AI_PRODUCTION_BACKEND_ACCEPTANCE_20261108_FA.md) بر دستور تاریخی «هر دو بسته Disabled بمانند» در این گزارش مقدم است. تا PASS سرور، خود Traffic واقعی تغییر نمی‌کند. AI deploy/reconfigure یا حذف منابع در محدوده نیست.

این راهنما مجوز خودکار تغییر Production نیست. Source این مرحله به AI واقعی متصل و با دادهٔ synthetic آزموده شده است؛ تنظیمات خصوصی و Traffic واقعی تغییر نکرده‌اند. [قرارداد و محدودیت‌های پیاده‌سازی](../architecture/AI_KNOWLEDGE_INTEGRATION_20261108_FA.md).

## حفظ وضعیت و تنظیمات

- Commit/بستهٔ قبلی، config خصوصی، Storage و Data Protection را برای rollback حفظ کنید. هیچ checkpoint یا resource قدیمی AI حذف نشود.
- فقط fragmentهای `AiKnowledge.Test.example.json` و `AiKnowledge.Production.example.json` را به‌عنوان الگوی کلیدها استفاده کنید؛ کل appsettings مقصد را با آن‌ها جایگزین نکنید. fragmentها secret ندارند و هر دو Disabled هستند.
- Gate اجرایی Knowledge ترکیب `AiService:Enabled` و `AiKnowledge:Enabled` است. singleton/Options هنگام startup خوانده می‌شوند؛ تغییر config باید در پنجرهٔ مجاز با reload/recycle کنترل‌شده انجام شود.
- قبل از gate کامل، هر دو false بمانند. Test از MakanApp_Test و Mock OTP و Production از MakanApp و Real OTP استفاده کند. MeliPayamak، Session، Permissionها و کلیدهای Data Protection موجود را حفظ کنید؛ عامل دومِ حذف‌شده را دوباره فعال نکنید.
- Business همان Service JWT موجود را می‌فرستد؛ بدون چاپ Key/JWT هماهنگی تنظیمات را کنترل کنید. برای Knowledge قرارداد فعلی Auth اجباری ندارد؛ `UseServiceAuthentication=false` آن مربوط به همین سرویس است. تغییر Auth یا ingress بدون قرارداد مصوب انجام نشود.

## دسترسی و شبکهٔ واقعی

روی خود Windows Backend Server ابتدا فقط بخوانید:

```powershell
Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object IPAddress -eq '10.77.0.1' |
    Select-Object IPAddress, InterfaceAlias
Test-NetConnection 10.77.0.2 -Port 8001
```

BaseUrl فعلی و مصوب Business را به‌خاطر timeout شبکه عوض نکنید. مسیر عمومی مصوب برای Knowledge و runner در Handoff برابر `https://195.177.255.98` است؛ TLS معتبر لازم است و `-k` یا bypass گواهی استفاده نشود. public Status/Ready واقعی را به نسخهٔ فیزیکی image/model تبدیل نکنید. اگر Runtime production مسیر خصوصی Knowledge جدا دارد، فقط آدرس مصوب و قابل دسترسی همان محیط را از تنظیمات Ops استفاده کنید؛ پورت/URL حدسی نسازید.

## ممیزی Worker، صف و writeهای نامعلوم

Backend صف پردازش فایل جدا ندارد؛ Knowledge staging/worker مالک پردازش است. از Ops شواهد امن همان Runtime بگیرید: نسخه/image واقعی، Worker process/last success/last failure، depth، Pending/InProgress/Failed/Retry، قدیمی‌ترین timestamp و DeadLetter در صورت وجود. نبود endpoint عمومیِ این اطلاعات، عدد صفر نیست.

برای ledger Backend، روی **DB واقعی همان محیط** فقط این SELECTهای خواندنی را اجرا کنید:

```sql
SELECT DB_NAME() AS DatabaseName;
SELECT Status, COUNT_BIG(*) AS ReservationCount, MIN(CreatedAtUtc) AS OldestCreatedAtUtc
FROM commerce.UsageReservations WHERE QuotaKey = 5
GROUP BY Status ORDER BY Status;
SELECT COUNT_BIG(*) AS PendingKnowledgeOutbox
FROM platform.OutboxMessages
WHERE ProcessedAtUtc IS NULL AND MessageType LIKE 'knowledge.%';
```

Statusهای Reservation:1Reserved،2Consumed،3Released،4Unknown. این شمارنده‌ها Queue داخلی AI نیستند. Source آستانهٔ Stuck خودکار برای Reserved/Unknown ندارد؛ زمان و state را `POTENTIALLY_STUCK` گزارش کنید، نه اینکه عمر حدسی به آن بدهید. Unknown را از روی timeout مصرف‌نشده فرض و آزاد/حذف نکنید. عملیات Knowledge با همان sourceId/version از Status reconcile شود؛ Source/Version تازه یا retry storm نسازید. Business stateless پاسخ خصوصی cache ندارد و اجرای مجدد همان intent409 است.

ممیزی محلی C# موجود فقط configهای localhost و نام MakanApp/MakanApp_Test را می‌پذیرد و secret چاپ نمی‌کند:

```powershell
dotnet run --project tools/ai/MakanApp.KnowledgeIntegrationProbe --configuration Debug --no-build -- --audit-local-pending '<repository-root>'
```

این دستور وضعیت سرور را از سیستم توسعه ثابت نمی‌کند. در اجرای این مرحله هر دو اتصال محلی SQL18456 دادند؛ credential جدید در چت درخواست یا در Git ثبت نشود.

## پذیرش Test قبل از تغییر Production

۱. بستهٔ Test همان Commit validated را deploy و Health/Swagger/OpenAPI و config مؤثر را تأیید کنید. Migration این مرحله ندارد؛ History صرفاً بررسی شود و SQL بی‌نیاز اجرا نشود.

۲. در window مجازِ Test، برای حساب/منابع QA با قرارداد AI معتبر، BaseUrlها را از تنظیمات مصوب و secret store تأمین و Gateهای Test را صریحاً باز کنید. نمونهٔ قرارداد تجاری Probe فقط fixture است؛ Plan/قیمت Production از آن seed نشود.

۳. Source منتشرشده و FileAsset مجاز بسازید؛ Ingest→202→Status محدود→Ready؛ همان intent/hash→200؛ Retrieve با نسخهٔ دقیق؛ RAG و Citation؛ retry همان ClientOperationId→409 بدون مصرف دوباره. Library همچنان بسته باشد. snapshot خصوصی یا Source bytes در شواهد نگذارید.

۴. حساب دوم و کلاس QA مستقل: منبع خارج از دسترسی قبل از RPC رد شود؛ پس از revoke Enrollment همان SourceVersion از Backend رد شود، حتی وقتی Status آن در AI هنوز Ready است. روی Production user data این آزمون انجام نشود.

۵. Client/Provider/SDK retry loop اضافه نشود. budget Backend۳۵ثانیه و سیاست Provider اعلام‌شده۳۰ثانیه/retry0 تغییر نکند. polling خواندن محدود با retry generation متفاوت است.

Probe واقعی فقط با `--run-live --allow-remote-https` و `AiKnowledge__BaseUrl` و `AiLiveProbe__ApprovedOrigin` همسان و فایل‌های QA غیرحساس در `KnowledgeLiveProbe__AssetsDirectory` کار می‌کند. به LocalDB ایزوله نیاز دارد؛ آن را به MakanApp یا MakanApp_Test برای Migration وصل نکنید. کد runner همان target policy قبلی را reuse می‌کند. Host آزمایشی خودش را فعال می‌کند و config/IIS را تغییر نمی‌دهد؛ این یک ابزار پذیرش است، نه روش Cutover. اجرای جاری هیچ Delete/Supersede/Reindex نمی‌فرستد و منبع‌های تازهٔ QA را در AI حفظ می‌کند؛ رفتار cleanup گزارش قبلی تاریخی است.

## Cutover، پایش و Rollback

تا زمانی که Contract، Business12، Knowledge Ready، Worker/Queue، نبود write نامعلوم بحرانی، Retry safety، Ingest/Retrieve/RAG/Citation/ACL/Revocation و پذیرش Test واقعی همگی تأیید نشده‌اند، Traffic بسته بماند. از همین دو Gate موجود استفاده کنید؛ canary ساختگی یا feature تازه لازم نیست. در صورت تغییر وضعیت، قبل/بعد health/ready/queue/errors/latency را با همان scope مقایسه کنید.

در failure بحرانی Gateهای محیط آسیب‌دیده را به حالت قبلی برگردانید و در پنجرهٔ مجاز reload کنید. False کردن Gate، مصرف قطعی یا درخواست درحال‌اجرا را پاک نمی‌کند. Ledger، pending operation، staging یا checkpoint قدیمی را حذف نکنید. rollback schema لازم نیست چون Migration جدید نداریم.

فقط پس از `BACKEND_AI_LIVE_ACCEPTANCE=PASS`، `TRAFFIC_CUTOVER=PASS` و `POST_CUTOVER_VERIFICATION=PASS` می‌توان اجازهٔ Cleanup منابع قدیمی را اعلام کرد؛ خود runner چنین منابعی را حذف نمی‌کند. تا آن زمان `AI_OLD_RESOURCES_CLEANUP=DO_NOT_ALLOW` باقی بماند. HA/RPO/RTO به تصمیم عملیاتی جدا نیاز دارد.
