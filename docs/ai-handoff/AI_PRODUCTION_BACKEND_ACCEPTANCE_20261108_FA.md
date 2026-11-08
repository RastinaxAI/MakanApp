# پذیرش Backend در برابر Production فعال AI — ۲۰۲۶-۱۱-۰۸

## وضعیت مصوب جاری

اصلاحیهٔ مالک پروژه بر توضیح‌های تاریخی Phase C مقدم است. تیم AI، Production را Active و Knowledge/Business health، Knowledge Ready، Qdrant health، Main Source Ready، Retrieval/RAG regression و Rollback checkpoint را تأیید کرده است. این‌ها ورودی عملیاتی مصوب این مرحله‌اند؛ Backend موظف نیست E5/Qdrant را دوباره deploy یا reconfigure کند. شواهد اجرای مستقل Backend جداگانه ثبت می‌شوند.

منابع Recovery، Shadow، Volume قدیمی، Checkpoint و مسیر Rollback منجمد هستند. اجازهٔ Cleanup همچنان `DO_NOT_ALLOW` است. این مرحله هیچ Knowledge/Error contract یا تنظیمی در سمت AI را تغییر نمی‌دهد.

## ترتیب پذیرش و دو بسته

`Local Full Acceptance → Test Package → Production Package → Test Deploy/Smoke → Production Deploy → Backend Traffic Resume → Post-cutover`.

دو بسته پس از موفقیت پذیرش محلی، از یک Commit و یک خروجی باینری Release ساخته می‌شوند؛ ساخت بستهٔ Production با استقرار یا بازگرداندن Traffic یکی نیست. در هر دو، Swagger و `AiService:Enabled` و `AiKnowledge:Enabled` روشن‌اند. Test از محیط Test، دیتابیس MakanApp_Test و Mock OTP با false/true استفاده می‌کند. Production محیط Production، دیتابیس MakanApp و OTP واقعی true/false دارد. Integration code و قراردادهای امنیتی یکسان‌اند.

appsettings خصوصی اصلی تغییر نمی‌کند؛ BaseUrl واقعی و Enabled فقط در خروجی بسته قرار می‌گیرد. نشانی placeholder قدیمی localhost مقصد AI فعال نیست. مقصد HTTPS مصوبِ پذیرش `https://195.177.255.98` است؛ timeout۳۵ثانیه، retry0 و Service JWT موجود تغییر نمی‌کنند. Knowledge طبق قرارداد مستقل جاری Auth اجباری ندارد؛ این موضوع مجوز حذف JWT Business نیست.

کلید Service JWT از User Secrets توسعه در ZIP کپی نمی‌شود. متادیتای غیرمحرمانهٔ auth می‌تواند مطابق تنظیمات معتبر باشد، اما `AiService__Authentication__CurrentKey` باید در secret store/Environment مجاز IIS مقصد تأمین شود. کلید، JWT و Header در چت، Git، evidence یا log ثبت نمی‌شوند. بستهٔ Enabled بدون secret مؤثر و تست‌شدهٔ مقصد، به‌تنهایی اثبات آمادگی سرور نیست.

## پذیرش محلی بدون تغییر Lifecycle AI

Probe `tools/ai/MakanApp.KnowledgeIntegrationProbe` فقط source QA غیرحساس تازه را ingest می‌کند؛ same-hash replay، hash conflict، status/poll/Ready، retrieve، exact allowlist، RAG/citation، foreign source deny و revoke Enrollment را بررسی می‌کند. Sourceهای قدیمی انتخاب نمی‌شوند. Delete، Supersede و Reindex از اجرای Probe حذف شده‌اند؛ Handler مستقل همان ابزار نیز این عملیات را پیش از شبکه رد می‌کند. APIهای اصلی lifecycle Backend و تست‌های کنترل‌شدهٔ آن‌ها تغییر نکرده‌اند.

در پایان، فقط SQL/Storage ایزولهٔ fixture محلی جمع می‌شود. نسخه‌های QA در AI باقی می‌مانند و شناسهٔ همان source/versionهای تازه در رویداد امن `qa_resources_preserved` ثبت می‌شود؛ پاک‌سازی خودکار یا اجازهٔ حذف Recovery از این رویداد نتیجه نمی‌شود.

```powershell
dotnet run --project tools/ai/MakanApp.AiIntegrationProbe --configuration Debug --no-build -- --run-live --allow-remote-https
dotnet run --project tools/ai/MakanApp.KnowledgeIntegrationProbe --configuration Debug --no-build -- --run-live --allow-remote-https
```

آدرس فرایندی و `AiLiveProbe__ApprovedOrigin` باید برابر مقصد HTTPS مصوب باشند؛ `KnowledgeLiveProbe__AssetsDirectory` فقط فایل QA داشته باشد. credential از User Secrets/Environment موجود خوانده می‌شود، نه آرگومان خط فرمان. secretها را برای اجرای نمونه چاپ نکنید. دستورات به LocalDB مستقلِ قابل‌حذف نیاز دارند و نباید به MakanApp یا MakanApp_Test برای Migration متصل شوند.

## Smoke روی Windows Backend

Test: `http://94.184.32.84:8090`؛ Production: `http://94.184.32.84`، فقط بعد از PASS واقعی Test Smoke.

در Test، startup، health/ready Backend، یک Business call مجاز، ingest QA→Ready→retrieve→RAG، exact citation، Worker/Queue/Pending و Retry safety بررسی شوند. تست کامل۱۲عملیات روی سرور لازم نیست دوباره اجرا شود. Production smoke فقط QA مجاز و غیرمخرب باشد. درخواست API بدون نشست، جایگزین یک Business call احرازشده نیست؛ صرف health200 نیز smoke AI نیست.

فقدان دسترسی مدیریتی یا credential مقصد با `BLOCKED_BY_ACCESS`/عدم اجرای smoke گزارش می‌شود؛ اطلاعات محرمانه از کاربر در چت درخواست نمی‌شود. نبود دسترسی، مجوز حدس‌زدن Worker سالم، Queue خالی یا Stuck=0 نیست.

## Gate بازگرداندن Traffic و Recovery

Cutover به معنی بازگرداندن درخواست‌های Backend به AI ازقبل‌فعال است. شرط آن: Local PASS، Test Smoke PASS، Production Backend Deploy PASS، Worker سالم، Queue پایدار، Stuck=0 و Retry safe. شمارش Reserved/Unknown ledger با صف Worker AI یکی نیست؛ بدون threshold رسمی فقط state/timestamp گزارش می‌شود. Pending/Failed کور حذف یا replay نشود.

پس از Resume، Business/Knowledge، latency/timeout/5xx، pending/retry/queue و نبود duplicate ingest/version/usage بررسی شود. فقط LiveAcceptance + Cutover + PostCutover همگی PASS اجازهٔ اعلام Cleanup می‌دهند؛ خود Backend منابع AI را حذف نمی‌کند. Rollback فقط به package/config قبلی Backend برمی‌گردد و Recovery AI دست‌نخورده باقی می‌ماند.
