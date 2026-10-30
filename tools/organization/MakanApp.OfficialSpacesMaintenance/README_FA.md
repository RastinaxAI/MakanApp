# ابزار نگهداری فضاهای رسمی

این ابزار از همان planner و Ensure موجود Backend استفاده می‌کند؛ API عمومی، Outbox موازی یا Migration خودکار نمی‌سازد. اجرای آن فقط برای اپراتور مجاز با دسترسی SQL است. نام، تلفن، محتوا و credential در خروجی نمی‌آیند.

پیش‌فرض فقط خواندنی است؛ حتی بدون `--dry-run` هیچ write انجام نمی‌شود. Connection String فقط از متغیر محیطی `ConnectionStrings__MakanDatabase` در محیط امن اپراتور خوانده می‌شود؛ آن را در فرمان یا گزارش قرار ندهید. schema جدید باید قبلاً با فرایند مستقل Migration نصب شده باشد.

```powershell
dotnet run --project tools/organization/MakanApp.OfficialSpacesMaintenance --configuration Release -- --dry-run --organization-id <organization-guid> --output plan.json
```

خروجی شامل منابع، binding فعلی، مقصد غایب، action، conflict و `planSha256` است. گزارش برنامهٔ مصوب همان سازمان را بازبینی کنید. وجود conflict اجرای write آن محدوده را رد می‌کند. تغییر منابع/binding بین تصویب و اجرا، hash را نامعتبر می‌کند.

فقط با مجوز مستقل اپراتور، `--apply`، سازمان مشخص و hash دقیق طرح بازبینی‌شده، Ensure موجود فراخوانی می‌شود:

```powershell
dotnet MakanApp.OfficialSpacesMaintenance.dll --apply --organization-id <organization-guid> --approved-plan-sha256 <reviewed-plan-hash> --output apply-result.json
```

هر purpose تراکنش مستقل و uniqueness SQL دارد؛ موفقیت قبلی حفظ می‌شود و retryِ Ensure پیام تاریخی یا Conversation تکراری نمی‌سازد. اگر اجرای جزئی متوقف شد، ابتدا Dry-run تازه بگیرید؛ hash قبلی را کورکورانه تکرار نکنید. Runtime عادی Outbox/Realtime پس از Deploy معتبر، invalidationهای ذخیره‌شده را پردازش می‌کند.

این مرحله فقط روی fixture ایزوله اجرا می‌شود؛ این سند مجوز اجرای ابزار روی MakanApp، MakanApp_Test یا سرور نیست. هیچ تاریخچه، Lesson/Assignment/Exam قدیمی یا private message برای تولید Card اسکن نمی‌شود. Parent و مخاطب گستردهٔ سازمان همچنان تابع تصمیم‌های باز محصول‌اند.
