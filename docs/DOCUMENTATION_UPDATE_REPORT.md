# گزارش به‌روزرسانی اسناد پروژه

تاریخ `2026-10-23`؛ کد مبنا `main@5885d119f6f5b3477d9d4da2738316db623a3d1f`؛ مقایسه با تحویل مستندات `e23beec` در ۲۰۲۶-۱۰-۱۸. این فایل گزارش جدید تحویل است؛ اسناد قبلی جدول زیر اصلاح شده‌اند.

## ۱. خلاصه مرحله

اسناد اصلی با OTP دو محیط، سخت‌سازی PH، اصلاح‌های AI v1، قرارداد صف jobs و رفع deadlock مسدودسازی تطبیق داده شدند. پیش‌تر بخشی از آن‌ها هنوز وضعیت ۲۰۲۶-۱۰-۱۸ را داشتند؛ اکنون منبع Markdown و خروجی‌های منتشرشده یک مبنای مشخص دارند. APIها همچنان ۳۲۵ عملیات و ۲۹۰ مسیر در ۵۷ Controller هستند؛ schemaها با اضافه‌شدن PlatformJobQueueDetail از ۵۵۱ به ۵۵۲ رسیده‌اند.

## ۲. معماری

ساختار Onion/Modular Monolith، .NET 9 و SQL Server ثابت است. این مرحله راهنما، قالب تولید سند و تست ابزار را تغییر می‌دهد. metadata HTTP از ApiExplorer/OpenAPI در Api و metadata SQL از مدل طراحی EF در Infrastructure گرفته می‌شود؛ جهت وابستگی لایه‌های محصول تغییر نمی‌کند.

## ۳. فایل مهم ایجادشده

همین فایل، `docs/DOCUMENTATION_UPDATE_REPORT.md`، فهرست دقیق تغییرات و شاهد validation را برای تحویل به توسعه‌دهنده نگه می‌دارد. اصل مهم: سند جاری، قرارداد موضوعی و گزارش تاریخی نقش‌های متفاوت دارند؛ عدد تست قدیمی را نباید نتیجهٔ اجرای امروز خواند.

## ۴. فایل‌های قبلی به‌روز‌شده

۲۵ فایل قبلی مستندات/خروجی و دو ابزار به‌روز می‌شوند؛ با یک گزارش جدید، مجموع scope Commit برابر۲۸ فایل است. دو خروجی دیتابیس محلی/ignored جدا هستند.

| فایل | تغییر و اثر |
|---|---|
| [README.md](../README.md) | آمار ۵۵۲ schema، OTP دو محیط، ورودی‌های عملیات/AI و حدود provider |
| [CURRENT_PROJECT_STATUS.md](CURRENT_PROJECT_STATUS.md) | شاخص کد مبنا، تغییرات بیزینس، gateهای باز و validation همین بازبینی |
| [backend-api-guide.md](backend-api-guide.md) | Real/Mock، تنظیمات/startup، مسیر واقعی AI، PH، worker/retention و آخرین migration |
| [backend-api-reference.md](backend-api-reference.md) | کاتالوگ کامل فعلی، PlatformJobQueueDetail/queues و وابستگی جدید health Controller |
| [database-current-structure.md](database-current-structure.md) | مدل جاری ۱۰۷ جدول/۴۸ migration و مرز cleanup bytes با metadata |
| [frontend-contract/README.md](frontend-contract/README.md) | مصرف Real/Mock، خطا/grounding/gate AI، jobs و health؛ تولید از قالب اصلاح‌شده |
| [deployment-windows-linux-docker.md](deployment-windows-linux-docker.md) | profileهای Test/Production، مثال‌های Real OTP، ring/readiness، shutdown و rollout worker |
| [platform/BACKOFFICE_FRONTEND_HANDOFF.md](platform/BACKOFFICE_FRONTEND_HANDOFF.md) | فیلدهای واقعی jobs/notifications، permission، وضعیت DataProtection/FileCleanup و معنای HTTP200 |
| [messaging/CURRENT_MESSENGER_STATUS.md](messaging/CURRENT_MESSENGER_STATUS.md) | worker fencing/recovery و seek زوج فعال Block، با حفظ policy و gateهای P3 |
| [ai-handoff/01_AI_INTEGRATION_OVERVIEW.md](ai-handoff/01_AI_INTEGRATION_OVERVIEW.md) | مسیر جاری AI و جداسازی backlog تاریخی handoff از وضعیت امروز |
| [ai-handoff/05_AI_ERROR_AND_RESILIENCE_CONTRACT.md](ai-handoff/05_AI_ERROR_AND_RESILIENCE_CONTRACT.md) | بودجه، mapping خطای مدل، telemetry و شواهد واقعی مستقل |
| [ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md](ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md) | Service Auth قبلی PASS، Parent Provider Timeout و Full-12 Deferred؛ برچسب‌گذاری ۹۵۱ تست اولیه به‌عنوان تاریخچه |
| [production/DEPLOYMENT_IIS.md](production/DEPLOYMENT_IIS.md) | اتصال runbook به دو بسته، OTP محیطی و مبنای جاری |
| [production/PRODUCTION_READINESS.md](production/PRODUCTION_READINESS.md) | PH و انتشار محلی با حفظ gateهای واقعی سرور/AI |
| [production/RELEASE_CHECKLIST.md](production/RELEASE_CHECKLIST.md) | کنترل بسته/OTP، drain نسخه legacy export، cleanup خاموش و AI مستقل |
| [production/OBSERVABILITY.md](production/OBSERVABILITY.md) | لاگ امن Test و telemetry جاری AI با label محدود |
| [production/BACKUP_RESTORE.md](production/BACKUP_RESTORE.md) | جداسازی backup/restore داده، Storage، کلید و ring دو محیط |
| [dev/DEVELOPMENT_TEST_USERS_FA.md](dev/DEVELOPMENT_TEST_USERS_FA.md) | جایگزینی فرمان منسوخ DevelopmentBypass/کد ثابت با Mock/Real فعلی و مرزبندی fixture تاریخی |
| [documentation-inventory.csv](documentation-inventory.csv) | فهرست جاری اسناد و طبقه‌بندی operations/dev؛ تاریخ فهرست‌سازی اثبات بازنویسی همه اسناد تاریخی نیست |
| [sync_repository_docs.py](../tools/docs/sync_repository_docs.py) | قالب‌های جاری و فهرست‌سازی بدون بازنویسی پیش‌فرض تاریخچه؛ refresh برچسب‌ها opt-in |
| [test_documentation_tools.py](../tools/docs/test_documentation_tools.py) | دو regression برای حفظ bytes سند تاریخی و runbook جاری هنگام فهرست‌سازی/refresh |

خروجی‌های قبلی بازتولیدشده:

| سند | HTML | PDF | صفحات PDF |
|---|---|---|---:|
| راهنمای بک‌اند | [backend-api-guide.html](backend-api-guide.html) | [backend-api-guide.pdf](backend-api-guide.pdf) | ۷۱ |
| مرجع API | [backend-api-reference.html](backend-api-reference.html) | [backend-api-reference.pdf](backend-api-reference.pdf) | ۴۰۷ |
| قرارداد فرانت | [HTML](frontend-contract/MakanApp-Frontend-Backend-Contract.html) | [PDF](frontend-contract/MakanApp-Frontend-Backend-Contract.pdf) | ۴۱۲ |
| فرهنگ دیتابیس | [database-current-structure.html](database-current-structure.html) | [database-current-structure.pdf](database-current-structure.pdf) | ۱۵۷ |

دو خروجی دیتابیس طبق gitignore موجود فقط محلی‌اند؛ شش خروجی HTML/PDF سه سند دیگر نسخه‌بندی می‌شوند. فایل‌های اصلی محصول و شواهد مرحله‌ای که تصمیمشان تغییر نکرده، حفظ شده‌اند.

## ۵. جریان اجرا

`DocumentationSnapshot.cs → TestServer/ApiExplorer/OpenAPI + IDesignTimeModel → repository-inventory.json/openapi.json → sync_repository_docs.py → Markdown → build_backend_docs.py → HTML/PDF → validate_repository_docs.py`.

در host استخراج، IHostedServiceها حذف و SQL/SMS/AI غیرفعال‌اند. قرارداد داده از کد گرفته می‌شود؛ معنای OTP/مجوز/retention دستی با use case واقعی تطبیق داده شده است.

## ۶. دیتابیس

۱۰۷ جدول، ۱۰۵۵ ستون، ۲۳۹ FK، ۲۹۵ index مستقل از key و ۲۷۹ CHECK ثبت شده‌اند. تعداد migrationها ۴۸ و آخرین مورد `20261017033804_AddConversationChecklists` است. این مرحله schema یا migration ایجاد/اعمال نمی‌کند؛ بررسی pending-model بدون اتصال سرور انجام می‌شود. suiteهای Integration از SQL Server LocalDB ایزوله استفاده می‌کنند؛ این نتیجه وضعیت Applied سرور را اثبات نمی‌کند.

## ۷. Endpointها

هیچ route یا DTO محصول در این مرحله تغییر نکرده است. اصلاح مستندات به‌ویژه `POST /api/v1/auth/otp/challenges` و `/verify` برای رفتار Real/Mock، دوازده `POST /api/v1/ai/*` برای context/grounding/error، و `GET /api/admin/v1/operations/{health|jobs|notifications}` برای permission و فیلدهای جاری است. request، response، authentication و schema هر عملیات در مرجع API کامل آمده‌اند؛ policy Controller جای authorization منبع در Application را نمی‌گیرد.

## ۸. مفاهیم فنی

تولید قرارداد از runtime metadata؛ مدل طراحی EF بدون اتصال DB؛ hash سورس برای جلوگیری از snapshot قدیمی؛ تفاوت schema و قاعدهٔ کسب‌وکاری؛ bookmark/link PDF و تطبیق SHA-256 فایل HTML با metadata PDF.

## ۹. تصمیم‌های طراحی

کاتالوگ/فرهنگ SQL از metadata بازتولید می‌شوند تا field و enum حدس زده نشوند. راهنما و سیاست مصرف به‌صورت دستی نگه‌داری می‌شوند چون OpenAPI رفتار محیط یا gate عملیاتی را کامل توصیف نمی‌کند. `document_inventory` پیش‌فرض فقط فهرست‌سازی می‌کند؛ تغییر تاریخ برچسب همهٔ گزارش‌های قدیمی بازبینی واقعی محسوب نمی‌شود. `--refresh-historical-notices` امکان صریح قبلی را نگه می‌دارد و runbook جاری را بازنویسی نمی‌کند.

## ۱۰. تست‌ها

دو تست خالص Python اضافه شدند: حفظ bytes سند تاریخی در inventory معمولی، و حفظ تصمیم تاریخی/عدم تغییر runbook در refresh صریح. مجموع تست‌های ابزار ۱۲ است؛ SQL، شبکه یا secret ندارند. تست محصول تازه اضافه نشده؛ suiteهای موجود Unit، Integration با SQL واقعی و Architecture برای regression اجرا می‌شوند.

## ۱۱. Validation

Restore موفق بود. Build اولیه و نهایی هر دو موفق، صفر warning/error. دور اول و نهایی هرکدام ۲۲۳۹ تست موفق داشتند: ۸۷۳ Unit، ۱۳۵۲ Integration و ۱۴ Architecture؛ صفر failed/skipped. شش فایل TRX هر دو دور بررسی شدند؛ نام fixture فقط در checkout تست موقتاً یکتا بود و پس از اجرا به bytes اصلی بازگشت. تست ابزار ۱۲ موفق. pending-model وجود ندارد. کاتالوگ ۳۲۵ عملیات/۵۵۲ schema/۱۰۷ جدول/۴۸ migration کنترل شده؛ لینک شکسته و اختلاف Enum/OpenAPI صفر. PDFها bookmark دارند، هش HTML متناظر را حمل می‌کنند و صفحه خالی یا متن خارج صفحه ندارند؛ صفحات نماینده بصری بازبینی شده‌اند.

## ۱۲. Git

branch مقصد `main` و remote `origin` با upstream موجود `origin/main` است. پیام این تحویل `docs: align guides with current identity AI and operations`؛ commit/push فقط پس از موفقیت نهایی و کنترل allowlist انجام می‌شود و hash/نتیجه در گزارش نهایی گفتگو اعلام خواهد شد. سه فایل Python قبلی `tools/ai` و تنظیمات خصوصی وارد تحویل نمی‌شوند.

## ۱۳. محدودیت‌های فعلی

Service Auth واقعی AI در شاهد پیشین PASS است؛ Parent Provider Timeout و Full-12 واقعی Deferred باقی‌اند. Push دستگاه واقعی، P3/LiveKit rejoin، scale-out، TLS، recycle/reboot، restore/load، alert و custody پذیرش مستقل می‌خواهند. cleanup پیش‌فرض خاموش است و purge SQL ندارد. این مرحله provider پولی، deployment یا migration سرور را اجرا نمی‌کند.

## ۱۴. نکات آموزشی

۱. یکسان‌بودن DTO به معنی یکسان‌بودن تضمین احراز هویت Real/Mock نیست.

۲. fieldهای مرجع از OpenAPI واقعی گرفته می‌شوند؛ وجود `queues` در jobs را نباید برای notifications حدس زد.

۳. idempotency durable با تحویل exactly-once provider فرق دارد.

۴. readiness جاری ring/config/SQL/Storage را می‌سنجد؛ وجود کد، تحویل Push یا AI کامل نیست.

۵. بازسازی PDF کافی نیست؛ متن منبع، تاریخ/commit، لینک و hash باید سازگار باشند.

## ۱۵. مسیر مطالعه

[شاخص پروژه](CURRENT_PROJECT_STATUS.md) → راهنمای بک‌اند → مرجع API → قرارداد فرانت/Backoffice → تنظیمات OTP → پیکربندی PH/worker/retention → وضعیت AI → `DocumentationSnapshot.cs` → `sync_repository_docs.py` → `build_backend_docs.py` → `validate_repository_docs.py` → تست ابزار.

## ۱۶. مرحله بعد

برای پذیرش runtime، یک gate مشخص از ماتریس Production با target و مجوز صریح انتخاب شود؛ مثلاً Parent Provider Timeout/Full-12 یا lifecycle IIS. این انتخاب مجوز ضمنی deployment، تغییر Provider یا فعال‌سازی cleanup نیست. با تغییر بعدی کد/بیزینس، snapshot و بخش دستی مرتبط باید دوباره همگام شوند.
