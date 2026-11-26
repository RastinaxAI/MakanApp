# آمادگی انتشار و مرز پذیرش عملیاتی

بازبینی `2026-11-26` از `source@f8178a47de94eae0ec2431adebcb9256a9789786`.

## دامنهٔ نسخه

Open Access و AI Unmetered سیاست جاری‌اند. کد پرداخت/Verify/Recovery محفوظ اما Runtime پرداخت، Paid Provisioning، Store Billing و Push بیرونی Deferred هستند. PersonalClass Assignment/Exam با دو flag مستقل خاموش و Library عمومی/AI بسته است. فقدان credential قابلیت Deferred را مانع هسته معرفی نکنید؛ [Scope](../product-v6/CURRENT_RELEASE_SCOPE_FA.md).

## ماتریس پذیرش

| موضوع | وضعیت قابل استناد و اقدام |
|---|---|
| قرارداد/مدل |۴۹۸ عملیات،۴۴۲ مسیر،۷۹۶ schema،۱۵۳ جدول،۷۸ Migration؛ Source@f8178a4، آخرین 20261122085930_AddMessagingMediaReviews؛ applied schema مقصد جدا |
| Build/Test | نتیجهٔ اجرای این بازبینی در [گزارش اسناد](../DOCUMENTATION_UPDATE_REPORT.md)؛ اعداد تاریخی Audit را نتیجهٔ امروز نخوانید |
| AI/Knowledge | Chat/History/SSE و Source/RAG موجود؛ Test محلی→upstream سابقاً پذیرفته؛ Enabled واقعی و مسیر شبکهٔ IIS مقصد جدا |
| Safety رسانه | Manual Review با Provider ترکیبی و تصمیم اثرانگشت‌دار موجود؛ Pending/Rejected تحویل ندارد؛ [قرارداد](../messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md) |
| پرداخت و Push | DEFERRED_CURRENT_RELEASE؛ Provider پرداخت Unavailable؛ OTP ملی‌پیامک حوزهٔ جدا |
| حذف حساب | lifecycle/checkpoint/recovery موجود؛ execution پیش‌فرض خاموش و retention حقوقی/محصولی نیازمند تنظیم مصوب |
| امنیت و HTTP | Bearer/ACL، ProblemDetails/code، CORS، rate limit امنیتی و Retry-After محفوظ؛ رفتار Proxy/مرورگر باید آزموده شود |
| Migration | فقط اجرای کنترل‌شده با Backup، SQL تازه و history مقصد؛ startup خودکار ممنوع؛ rollback binary با rollback داده یکی نیست |
| Worker / IIS | claim/fencing و recovery محلی موجود؛ idle/preload/recycle/reboot، drain نسخه قبلی و stop budget روی مقصد بررسی شوند |
| Realtime / Data Protection | cursor با User/Session/Context و ring host؛ استمرار ring و resync پس از rollback جدا از health/ready |
| Observability | metric/trace محلی موجود؛ exporter، dashboard و دریافت واقعی alert مقصد نیازمند پذیرش جدا |
| Storage / Backup | DB، Blob، SourceVersion، AI history و Evidence رسانه سازگار نگه‌داری شوند؛ Restore واقعی محیط هدف مستقل |
| Scale / P3 | backplane، چند replica، Load/SLO و LiveKit/TURN از regression محلی پذیرفته نمی‌شوند |

## ترتیب نصب و بازگشت

[IIS](DEPLOYMENT_IIS.md) و [Windows/Linux/Docker](../deployment-windows-linux-docker.md) مرجع اجرا، [انتشار دو بسته](PUBLISH_GUIDE_FA.md) مرجع artifact و [Backup/Restore](BACKUP_RESTORE.md) مرجع بازیابی‌اند. Secretهای مقصد محفوظ، بسته/SQL تازه hash و فایل config مؤثر بررسی شوند. /health/ready فقط config/SQL/Storage است؛ موفقیت آن readiness همه Providerها یا SLO را تضمین نمی‌کند.

Manual Media Review اسکن جامع خودکار AV/CDR نیست. AI Unmetered محدودیت فنی یا ACL را حذف نمی‌کند. سن metadata است و Privacy/Block/Safety مستقل‌اند. [تنظیمات](../operations/PRODUCTION_CONFIGURATION_RUNBOOK.md)، [Realtime](../operations/REALTIME_RUNBOOK_FA.md)، [حذف حساب](../operations/ACCOUNT_DELETION_RUNBOOK_FA.md).

هیچ Deploy/Publish، تغییر سرور، فعال‌سازی Provider یا Merge به main در این به‌روزرسانی انجام نشده است. شواهد تاریخ‌دار [Test](../operations/TEST_RUNTIME_READINESS_FA.md) و [ممیزی BE01–BE25](../product-v6/FINAL_BE01_BE25_AUDIT_FA.md) فقط دامنهٔ همان اجرا را اثبات می‌کنند.
