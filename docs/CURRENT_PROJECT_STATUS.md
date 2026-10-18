# وضعیت جاری پروژه و مسیر مطالعه اسناد

بازبینی `2026-10-18` از `main@ef67624`؛ **325 عملیات HTTP در 57 Controller و 290 مسیر؛ 551 schema، 107 جدول و 48 migration**. این صفحه شاخص جاری است؛ گزارش‌های مرحله‌ای و PDFهای اصلی محصول تاریخچه و تصمیم‌های scope خودشان را حفظ می‌کنند.

## قابلیت‌های ثبت‌شده

Identity/refresh/session، Organization/Guardian onboarding، Academic/Assessment، report/export، Notification، Commerce lifecycle و Backoffice/MFA موجودند. Messenger P0/P1/P2 تا Checklist و transport Web Push/FCM اضافه شده‌اند. schema با آخرین migration `20261017033804_AddConversationChecklists` پوشش داده می‌شود. هیچ migration یا provider واقعی در این به‌روزرسانی اجرا نشده است.

## gateهایی که بسته اعلام نمی‌شوند

| gate | وضعیت |
|---|---|
| Web Push/FCM روی دستگاه واقعی | P1_PUSH_RUNTIME_PENDING |
| backplane و state مشترک چند instance | PENDING_SCALE_INFRA |
| رسانه P3 و آزمایش LiveKit/TURN | P3_S_INFRA_REQUIRED؛ rejoin security NOT_TESTED و Production approval NOT_GRANTED |
| AI v1 واقعی | پذیرش E2E کامل از شواهد commit‌شده احراز نشده؛ خطای provider با موفقیت mock/health رفع‌شده فرض نشود |
| RAG source lifecycle | DEFERRED؛ مجوز ingestion/index/source جدید ایجاد نشده |
| پرداخت واقعی | gateway unavailable |
| SMS عمومی اعلان | provider unavailable؛ مستقل از OTP ملی‌پیامک |
| عملیات TLS/backup/restore/load/secret custody | فقط با شاهد محیط هدف؛ regression کد کافی نیست |

## مسیر مطالعه

۱. [راهنمای بک‌اند](backend-api-guide.md) برای مدل و جریان اجرا.

۲. [مرجع API](backend-api-reference.md) برای تمام routeها، request/response و schema.

۳. [قرارداد فرانت](frontend-contract/README.md) و [وضعیت Messenger](messaging/CURRENT_MESSENGER_STATUS.md) برای مصرف و privacy.

۴. [فرهنگ دیتابیس](database-current-structure.md)، [استقرار](deployment-windows-linux-docker.md) و [آمادگی Production](production/PRODUCTION_READINESS.md).

۵. [فهرست نگه‌داری همه اسناد](documentation-inventory.csv) برای تشخیص مرجع جاری، شاهد مرحله‌ای و فایل اصلی محصول.

## حدود بازبینی

کاتالوگ از TestServer بدون worker و مدل طراحی EF استخراج شده؛ SQL/SMS/AI/provider واقعی تماس نداشته‌اند. اختلاف تاریخی DOC-CONTRACT-01 در snapshot فعلی دیده نمی‌شود و وضعیت آن CLOSED_IN_CODE_AND_LOCAL_CONTRACT_TESTS است؛ انتشار OpenAPI روی سرور جدا احراز می‌شود. شواهد و تغییرات ثبت‌نشده AI در workspace، بخشی از commit این به‌روزرسانی نیستند؛ endpoint یا deadline پیش‌نویس از آن‌ها منتشر نشده است.


## نتیجه اعتبارسنجی بازبینی 2026-10-18

restore موفق بود؛ build اولیه و نهایی هر دو صفر warning و صفر error داشتند. در هر دو دور suite، ۱۵۹۵ تست موفق شدند: ۵۵۸ Unit، ۱۰۲۳ Integration و ۱۴ Architecture؛ failed و skipped صفر. ده تست خالص ابزار مستندسازی هم موفق بودند. این نتایج روی checkout تمیز نسخه مبنا و دیتابیس fixture با نام یکتا به دست آمدند؛ پیش‌نویس‌های ثبت‌نشده AI وارد آن نشده‌اند.

کاتالوگ ۳۲۵ عملیات، ۵۵۱ schema، ۱۰۷ جدول و ۴۸ migration و فهرست ۱۱۱ ورودی اسناد کنترل شدند؛ لینک شکسته و اختلاف Enum/OpenAPI در snapshot صفر بود. HTML/PDF چهار کتاب بازتولید و هش هر PDF با HTML متناظر کنترل شد؛ صفحه خالی یا متن خارج صفحه دیده نشد و صفحات نماینده بصری بررسی شدند. دو فایل HTML/PDF دیتابیس طبق سیاست موجود ignored باقی‌اند؛ فایل‌های اصلی PDF محصول و شواهد مرحله‌ای حفظ شده‌اند.

این کار deployment، مهاجرت سرور، تماس پولی SMS/Push/AI، تأیید دستگاه واقعی، load test یا gate رسانه P3 را اجرا نمی‌کند. ۲۲ فایل کار قبلی workspace حفظ و از commit این تحویل کنار گذاشته شده‌اند. وضعیت pending عملیاتی جدول‌های بالا با نتیجه regression بسته اعلام نشده است.
