# MakanApp — Manual API Acceptance Pack

**Release:** 2026-10-18 · **Repository:** `RastinaxAI/MakanApp` · **Code HEAD at extraction:** `49cb86ca897301c80c771d170825c1aec4a99226`.

## مرجع قابل‌ردیابی و دامنه

این بسته براساس `docs/backend-api-reference.md` ایجاد شده که از Runtime OpenAPI/ApiExplorer روی Snapshot `ef6762462bda4415a3d052454fcf53af7fa6b999` استخراج شده است. در نسخهٔ فعلی ۳۲۵ عملیات HTTP، ۲۹۰ مسیر و ۵۷ Controller ثبت شده‌اند؛ خروجی Collection از همان ۳۲۵ operation (method + route) ساخته شده است، نه از endpointهای پیشنهادی.

فاصلهٔ چهار commit بین Snapshot مستند و HEAD بررسی شده است: Controller جدید/تغییریافته‌ای در Diff آن چهار commit دیده نمی‌شود، اما **قراردادهای داخلی AI و تنظیمات Production تغییر کرده‌اند**. برای تأیید Contract دقیق artifact مستقر، خروجی `/openapi/v1.json` نسخهٔ واقعی (در محیط مجاز و با Swagger opt-in)، SHA build و این Collection باید مقایسه شوند؛ تا آن زمان ادعای Production PASS نداریم.

## فایل‌ها

| مسیر | کاربرد |
|---|---|
| `postman/MakanApp.ManualAcceptance.postman_collection.json` | تمام ۳۲۵ عملیات در ۵۷ پوشه؛ guard امنیتی و assertionهای status/response |
| `postman/MakanApp.SAFE.template.postman_environment.json` | متغیرها با مقدار خالی، token/OTP/phone از نوع Secret |
| `API_INVENTORY.csv` | فهرست تک‌به‌تک Controller، Method، Route، DTO، پارامتر، status و سورس |
| `SCENARIOS_FA.md` | گردش‌کار دستی فارسی، دادهٔ لازم، ورودی/خروجی، حالت منفی و شواهد |
| `curl/*.sh`, `curl/*.ps1` | نمونه‌های دقیق curl، با اجرای کنترل‌شده |
| `TEST_EXECUTION_LOG_FA.md` | ثبت PASS/FAIL/BLOCKED/NOT_RUN و شواهد |
| `VALIDATION.md` | چک پوشش/محدودیت‌ها/مرجع کد |

## شروع در Postman

۱. JSON Collection و JSON Environment را import و Environment را select کنید.

۲. فقط `baseUrl` را با endpoint واقعی محیط **مجاز تست** تنظیم کنید (آدرس واقعی Production را در ریپو ننویسید). `allowMutations=false` را تغییر ندهید تا سناریو و دادهٔ ایزوله آماده شوند. از `GET /health/live` و `GET /health/ready` با curl امن شروع کنید؛ Health در شمارش ۳۲۵ Controller نیست.

۳. برای احراز هویت به‌صورت اختیاری، روی محیط دارای TLS معتبر و شمارهٔ test، `allowMutations=true` کرده، `POST /api/v1/auth/otp/challenges` سپس `POST /api/v1/auth/otp/verify` را با کد **واقعاً دریافت‌شده** اجرا کنید. **هیچ bypass، OTP ثابت یا ارسال واقعی ناخواسته در pack وجود ندارد.** پس از verify، access/refresh در Environment محلی ذخیره می‌شود؛ از export آن با مقادیر حقیقی اجتناب کنید.

۴. برای سایر درخواست‌ها، IDها و Payloadها را از response مرحلهٔ قبل و قرارداد DTO پر کنید. `GET /api/v1/workspaces/me` سپس `POST /api/v1/workspaces/select` را با `workspaceType/membershipId/role` واقعی انجام دهید. تغییر Role با header دلخواه یا ID حدسی پذیرفته نیست.

۵. برای سناریوی منفی، `expectedStatus` را موقتاً به یک status یا چند status جداشده با کاما تنظیم کنید (مثلاً `401` یا `403,404`) و پس از تست **خالی کنید**؛ در حالت عادی فقط responseهای 2xx منتشرشده مبنای PASS هستند.

۶. قالب بدنهٔ هر request از فیلدهای **واقعی JSON DTO** تولید شده، اما مقادیر placeholder و برخی defaultها test-data معتبر نیستند. قبل از اجرای mutation آنها را با داده‌های سناریوی تأییدشده جایگزین کنید. Collection اگر متغیر template خالی باشد درخواست را skip می‌کند، نه PASS.

۷. در صورت بروز `403`/`404`، نقش، Workspace، resource ownership، تنظیمات provider و سیاست privacy را بررسی کنید؛ با صرف عوض‌کردن ID یا client header مجوز جدید ایجاد نمی‌شود. خطاهای دامنه عموماً ProblemDetails با `code` و `traceId` هستند.

## سیاست خطر و اثر جانبی

- POST/PUT/PATCH/DELETE و `GET /api/v1/conversations/saved` (در کد ممکن است conversation بسازد) به opt-in `allowMutations=true` نیاز دارند.
- بیرون loopback، ارسال token/OTP یا mutation از طریق HTTP پیش‌فرض مسدود است. `allowInsecureHttpWithSecrets` عمداً false است؛ در Production HTTP ریسک نشت credential وجود دارد. اولویت با HTTPS تست/محیط داخلی است.
- سناریوهای ارسال SMS، AI provider، payment gateway، Push/FCM، WebSocket روی IIS و Data Protection واقعی تا اخذ شاهد محیط هدف **NOT_RUN/BLOCKED** هستند، حتی اگر unit/integration سبز باشند.
- `expectedStatus` assertion را تنها برای همان یک case تغییر دهید؛ پاسخ 503 در سناریوی ارائه‌دهندهٔ غیرفعال، موفقیت سرویس واقعی نیست.
- هیچ داده، DB، User، Role، SMS یا سفارش در این تولیدکننده ایجاد نشده است؛ این بسته مستند/Collection است.

## قواعد قبولی

**PASS** فقط با پاسخ واقعی، status صحیح، داده/permission صحیح، traceId/time و ثبت نتیجه در log. **BLOCKED** هنگام نبود پیش‌نیاز، **FAIL** هنگام نقض Contract، **NOT_RUN** هنگام عدم اجرا. درخواست Skipشده در Postman هرگز PASS نیست.

برای جزئیات ۵۵۱ schema، nullable و enum دقیقاً به [مرجع نسخه‌شده](../../docs/backend-api-reference.md) مراجعه کنید؛ نسبت به skeleton کمینهٔ Collection اولویت دارد.
