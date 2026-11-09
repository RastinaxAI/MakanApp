# قرارداد جاری AI و انتشار خصوصی — ۲۰۲۶-۱۱-۰۹

تصمیم صریح مالک برای این انتشار: Business و Knowledge فعلی Service JWT نمی‌خواهند. این تصمیم بر الزام JWT در راهنماهای تاریخی مقدم است؛ نشست، Role/Context، ACL، Entitlement و Quota ورودی Backend همچنان الزامی‌اند. هیچ Authentication کاربر یا دسترسی منبع تغییر نکرده است.

کلید واقعی Business برابر `AiService:UseServiceAuthentication=false` و کلید Knowledge برابر `AiKnowledge:UseServiceAuthentication=false` است. در این حالت `AiServiceClient` نه CredentialProvider را فراخوانی می‌کند و نه Authorization header می‌سازد؛ CurrentKey لازم نیست. Headerهای Contract و Correlation و validation سخت‌گیرانهٔ پاسخ حفظ می‌شوند. مقدار پیش‌فرض Business برای تنظیمات قدیمی true است؛ بسته‌های جدید false را صریح تنظیم می‌کنند و مسیر JWT قدیمی برای محیطی که صریحاً آن را می‌خواهد باقی است.

Probe واقعی هم حالت صریح را می‌خواند و نبود Authorization را در قرارداد جاری بررسی می‌کند. Knowledge Probe فقط ingest/status/retrieve/rag برای QA تازه را مجاز می‌داند؛ Reindex/Delete/Supersede و منابع Recovery مسدود باقی‌اند. هیچ AI deploy یا تغییر E5/Qdrant مجاز نیست.

در `WaitForCompletionAsync` تعداد تلاش و deadline هر دو مستقل‌اند. ترکیب۲۰ تلاش با فاصله۵۰۰ms برای پاسخ سریع فقط حدود۹٫۵ثانیه فاصله ایجاد می‌کند و deadline۶۰s الزام حداقل انتظار نیست. بسته و Probe جدید از سقف موجود و معتبر۱۲۰ تلاش، فاصله۵۰۰ms و deadline۶۰s استفاده می‌کنند؛ این تصمیم از semantics و محدودهٔ Options مشتق شده است. HTTP timeout همچنان۳۵s و retry خودکار write صفر است. Statusهای terminal بلافاصله برمی‌گردند؛ caller cancellation حفظ می‌شود.

بسته‌های این انتشار خصوصی‌اند و فقط از appsettings خصوصی موجود، Connection String و Identity و ملی‌پیامک را حفظ می‌کنند؛ هیچ Secret وارد Source/Git، مستندات یا stdout نمی‌شود. Test و Production دیتابیس و Storage جدا دارند. فایل‌های اصلی خصوصی و سه فایل ناتمام AI دست‌نخورده می‌مانند. Swagger برای هر دو فعال است؛ محیط Test با Mock OTP و Production با OTP واقعی و محافظ Startup موجود است.

مستندات تحویل هر Release باید نام Commit نهایی، Hash ZIPها، تست‌های واقعی، وضعیت HTTP/SQL certificate/CORS و disabled بودن Payment/Notification عمومی را ثبت کنند. ساخت بسته اثبات Deploy یا سلامت Worker/Queue سرور نیست. Migration جدید این تغییر لازم ندارد؛ مدل و Endpoint عمومی عوض نمی‌شوند.
