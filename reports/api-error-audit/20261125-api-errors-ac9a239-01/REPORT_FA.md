# گزارش نهایی ممیزی خطا و آموزش توسعه‌دهنده

## ۱. خلاصه مرحله

دو نقص تأییدشدهٔ قرارداد Backend اصلاح شد: code غایب در خطاهای framework، و حذف Retry-After موجود در Exceptionهای AI/Knowledge. قبل از Fix، ۹ تست از ۳۳ تست HTTP ناموفق بود؛ پس از Fix همان ۳۳ مورد موفق شد و guardهای تکمیلی نیز اجرا شدند. هیچ success جعلی یا تغییر سیاست دسترسی ایجاد نشده است.

پذیرش زنده هنوز کامل نیست؛ حسابرسی Audit کامل است: از ۱۰۷ Case، 14 مورد در دامنهٔ محلی PASS، 1 مورد زنده FAIL، 89 مورد BLOCKED و 3 مورد NOT_APPLICABLE است. پاسخ‌های 404/405 زنده فاقد code بودند؛ سازگاری این مشاهده با نداشتن Fix محلی SUPPORTED است، چون Commit مستقر معلوم نیست. Caseهای UI/credential بدون مجوز دادهٔ واقعی اجرا نشده‌اند. این گزارش، Full107 REAL PASS نیست.

## ۲. معماری

تغییر اجرایی فقط در Api است. Domain/Application/Infrastructure و مدل SQL تغییر جدیدی نکردند. ApiProblemDetailsContract مسئول تکمیل قرارداد HTTP است؛ قواعد منبع و مجوز در use case موجود باقی‌اند. مسیر: HTTP → Controller/ModelBinding → Application و قواعد موجود → Exception شناخته‌شده → GlobalExceptionHandler → ProblemDetails. خطای routing خالی از StatusCodePages و همان قرارداد ProblemDetails عبور می‌کند.

## ۳. فایل‌های مهم جدید

- `MakanApp.Api/Errors/ApiProblemDetailsContract.cs`: code عمومی فقط هنگام نبود code اختصاصی؛ تکمیل پاسخ خالی API؛ نگه‌داری RetryAfter عددی. نکته: وضعیت، مجوز یا بدنهٔ نوشته‌شده جایگزین نمی‌شود.
- `tests/MakanApp.IntegrationTests/ApiErrorAuditEndpointsTests.cs`: pipeline واقعی میزبان کنترل‌شده بدون SQL/provider واقعی؛ ۴۲ تست قرارداد، Accept، 401، 403/404/412، validation، retry و حریم خصوصی.
- `tests/MakanApp.IntegrationTests/ApiErrorAuditRaceTests.cs`: شش timing، هرکدام ده تکرار با SQL ایزوله، barrier و fixture مستقل؛ اثبات sleep جای مشاهدهٔ state نیست.
- `tests/MakanApp.UnitTests/Ai/ApiErrorAuditAiResponseTests.cs`: سه پاسخ HTTP200 با خطای تجاری نباید answer موفق شوند؛ adapter و privacy بررسی می‌شوند.
- `cases.csv`، ledger و Handoff: traceability سناریو، کد واقعی و رفتار پیشنهادی UI، جدا از رفتار اجراشده.

## ۴. فایل‌های مهم تغییرکرده

`ApiErrorCodes.cs` چهار کد framework اضافه می‌کند. `Program.cs` دو فراخوانی محدود برای ProblemDetails/StatusCodePages دارد. `GlobalExceptionHandler.cs` یک فراخوانی حفظ header دارد. همهٔ محتوای قبلیِ این دو فایل مشترک با حذف همین فراخوانی‌ها، دقیقاً بازسازی و با backup مقایسه شد. ابزار موجود `DocumentationSnapshot.cs` فقط PendingModelChanges را با API رسمی EF به خروجی metadata اضافه می‌کند.

## ۵. جریان اجرا

در خطای دامنه: Controller → Application → Exception جاری → GlobalExceptionHandler → حفظ status/code و RetryAfter معتبر → ProblemDetails همراه trace/correlation → HTTP. در 404/405 خالی: routing → ApiProblemDetailsContract.WriteEmptyErrorAsync → IProblemDetailsService → fallback امن JSON در صورت نیاز. هیچ endpoint تشخیصی محصول ساخته نشد؛ Controller fault فقط در ApplicationPart میزبان تست ثبت می‌شود.

## ۶. دیتابیس

Migration جدید صفر است؛ همان 78 Migration قبلی و 153 جدول metadata وجود دارند. مدل، FK، index، constraint و rowversion در این Task تغییر نکردند. آخرین Migration: `20261122085930_AddMessagingMediaReviews`. PendingModelChanges=false با API رسمی EF و بدون SQL بررسی شد. CLI معمول به‌علت assembly-load خطا داشت و PASS گزارش نشده؛ روش metadata جایگزین ثبت شده است. Migration خودکار فقط در Factoryهای SQL موقت Integration اجرا شد؛ MakanApp/MakanApp_Test یا SQL مقصد تغییر نکردند.

## ۷. Endpoint و Contract

Endpoint تجاری تازه‌ای وجود ندارد. خطاهای framework زیر `/api` با code پایدار و همان status می‌آیند؛ HEAD و non-API دست‌نخورده‌اند. کد اختصاصی دامنه، field errors، Allow405، 416 فایل و بدنهٔ نوشته‌شده حفظ می‌شوند. 429/503 فقط در صورت وجود RetryAfter معتبر AI/Knowledge، header عددی دارند؛ این header به معنی مجوز retry write نیست. جزئیات دقیق در FRONTEND_ERROR_HANDOFF_FA.md است.

## ۸. مفاهیم فنی

ProblemDetails قرارداد خطاست؛ HTTP status، نتیجهٔ Business و UX سه چیز مستقل‌اند. HasPendingModelChanges مقایسهٔ مدل با snapshot است، نه Query تاریخچهٔ دیتابیس. Barrier در تست race نقطهٔ قطعی اجرا را مشخص می‌کند؛ SQL وضعیت Reserved/Consumed و history را اثبات می‌کند. source hash و backup فقط حفاظت WIP هستند و اثبات رفتار سرور نیستند.

## ۹. تصمیم‌های طراحی

Mapper جدید همهٔ خطاها به 409/500 تبدیل نمی‌کند؛ فقط code غایب framework تکمیل می‌شود. Retry-After از مقدار typed موجود ساخته می‌شود، نه header/body خام provider. schema عمومی جدید تحمیل نشده؛ ProblemDetails فعلی و extensions حفظ شده‌اند. محدودیت: UI و BFF در این repository نیستند و فقط Handoff تحویل می‌گیرند.

## ۱۰. تست‌ها

۴۸ Integration جدید (۴۲ HTTP و شش timing theory با ۶۰ تکرار داخلی) و سه Unit جدید، جمعاً ۵۱ test case نسبت به baseline3738 اضافه شدند. ۶۰ iteration تست جدا در شمارش xUnit حساب نشده‌اند. Unit adapter خطای نهفته در 200 را بررسی می‌کند؛ Integration HTTP pipeline/SQL/مجوز/idempotency را؛ Architecture همان ۱۶ invariant Onion را حفظ می‌کند. ۴ نقش سازمانی و Personal از factoryهای فعلی و aliasهای مجاز پوشش محلی دارند؛ REAL_API نقش‌ها ادعا نمی‌شود.

## ۱۱. Validation

Clean، Restore، Release Build و Final Build موفق با صفر Warning/Error. Focused Unit=1192 و Focused Integration=320 موفق. Regression اول=3786 و نهایی=3789 موفق؛ Unit=1499، Integration=2274، Architecture=16، Failed=0، Skipped=0. سه Unit جدید پس از Build اولیه اضافه شدند و Final Build/Full Final آن‌ها را نیز اجرا کرده‌اند؛ اعداد دو run عمداً یکسان فرض نشده‌اند. نام namespace تک‌تک تست‌ها با summary مقایسه شده تا خروجی موازی solution به اسمبلی اشتباه نسبت داده نشود.

## ۱۲. Git

HEAD اجرای اولیه، پیش از Closure، `ac9a23968c1449c037cd1eb7f4bae6812252b73d`؛ branch=`feature/product-v6-backend-alignment`؛ upstream=`origin/feature/product-v6-backend-alignment`. در پایان اجرای اولیه Commit/Push/Remote verify/Publish انجام نشده بودند. در بازبینی ۲۰۲۶/۱۱/۲۵ تعریف Gate اصلاح شد: AUDIT_COMPLETE یعنی تعیین تکلیف ۱۰۷ مورد، نه موفقیت زندهٔ همهٔ آن‌ها. مالک اکنون Commit/Push و ساخت دو بسته را پس از Validation همان Commit مجاز کرده است؛ این فایل snapshot پیش از این عملیات است و نتیجهٔ واقعی در RELEASE_MANIFEST.json بسته ثبت می‌شود. هیچ فایلی staged نیست؛ ۴۰ تغییر قبلی، AI و private configها حفظ شده‌اند. Patch بازبینی فقط برای اصلاحات Backend در repro/backend-only-fixes.patch است و WIP قبلی را شامل نمی‌شود.

## ۱۳. محدودیت‌های فعلی

هیچ ورود زنده، پروفایل مرورگر، Cookie/Token یا دادهٔ واقعی مصرف نشد. صفحهٔ عمومی /enter و API ناشناس فقط به‌عنوان همان scope ثبت شده‌اند. Runtime deadline، Class403/404 تاریخی، تمام کارت‌ها و timing/network UI به Evidence مالک نیاز دارند. ۸ تفاوت ظاهری عملیات OpenAPI شامل پنج تفاوت معنایی و سه تغییر نام ref یکسان است؛ یک endpoint محلی نیز هنوز در Live نیست. Build/Commit/نام محیط دقیق سرور UNKNOWN است. جزئیات credential، body خصوصی یا ID واقعی در گزارش چاپ نشده است.

## ۱۴. نکات آموزشی

۱. 409 می‌تواند پاسخ درست idempotency/deadline باشد. ۲. 200 به معنی answer سالم نیست. ۳. 404 پنهان‌کنندهٔ منبع را به 403 تبدیل نکنید. ۴. RetryAfter به معنی تکرار بی‌شرط عملیات نیست. ۵. لغو کاربر، upstream timeout و EOF متفاوت‌اند. ۶. کارکرد Factory محلی، پذیرش UI/سرور را ثابت نمی‌کند. ۷. در worktree کثیف، تغییر جدید را نسبت به baseline و نه صرفاً HEAD بازبینی کنید.

## ۱۵. مسیر مطالعه

Program.cs → ApiErrorCodes.cs → ApiProblemDetailsContract.cs → GlobalExceptionHandler.cs → ApiErrorAuditEndpointsTests.cs → ApiErrorAuditAiResponseTests.cs → ApiErrorAuditRaceTests.cs → api-error-ledger.json → FRONTEND_ERROR_HANDOFF_FA.md → cases.csv.

## ۱۶. مرحله بعد

مالک می‌تواند Evidence دستی UI و runtimeِ موارد BLOCKED و deadline را تهیه کند؛ سپس gateهای باقی‌مانده ارزیابی شوند. موارد مسدود مانع بسته‌شدن حسابداری Audit نیستند؛ Commit/Push/Publish با مجوز جدید و پس از Regression روی exact committed tree انجام می‌شوند. Deploy در این کار مجاز نیست و پذیرش Production به استقرار و پذیرش زندهٔ Test وابسته می‌ماند. بازطراحی Auth، AI، Safety، quota و frontend به این اصلاح محدود اضافه نشده است.

## وضعیت ماشینی

این وضعیت snapshot پیش از Commit و بسته‌سازی است؛ نتیجهٔ واقعی Git/Publish در manifest بستهٔ جدید ثبت می‌شود. PASSهای خطا scope محلی Backend دارند. SERVER_CONNECTION یعنی اتصال مدیریتی؛ HTTP عمومی اجرای اولیه جدا ثبت شده است.

```text
RUN_ID=20261125-api-errors-ac9a239-01
BASE_COMMIT=ac9a23968c1449c037cd1eb7f4bae6812252b73d
FINAL_COMMIT=NOT_CREATED
BRANCH=feature/product-v6-backend-alignment
PREEXISTING_DIRTY_FILES=40
MANIFEST_HASH_VERIFICATION=PASS
LIVE_ENVIRONMENT=OWNER_DESIGNATED_TEST_RUNTIME_NAME_UNKNOWN
LIVE_API_BASE_URL=http://94.184.32.84:8090
LIVE_WEB_BASE_URL=https://testapp.makanapp.org
LIVE_OPENAPI=PASS
LIVE_OPENAPI_SHA256=eb71d2b33b2c34c430350de3bda001b313ff087bb2021cd44eaf0cf7bceecc1a
BASE_CASES=107
CASES_ACCOUNTED_FOR=107
CASES_EXECUTED=15
CASES_PASS=14
CASES_FAIL=1
CASES_BLOCKED=89
CASES_NOT_RUN=0
CASES_NOT_APPLICABLE=3
ATTEMPTS_TOTAL=961
REAL_UI_BACKEND_ATTEMPTS=1
REAL_API_ATTEMPTS=15
LOCAL_INTEGRATION_FAULT_ATTEMPTS=945
UI_MOCK_FAULT_ATTEMPTS=0
HTTP_4XX_OBSERVED=536
HTTP_5XX_OBSERVED=54
TRANSPORT_ERRORS=0
BUSINESS_ERRORS=580
PARSE_ERRORS=NOT_MEASURED_SEPARATELY
STREAM_ERRORS=NOT_MEASURED_SEPARATELY
FAILED_2XX=3
ISSUES_CRITICAL=0
ISSUES_HIGH=1
ISSUES_MEDIUM=4
ISSUES_LOW=0
ROOT_CAUSE_CONFIRMED_COUNT=3
ROOT_CAUSE_SUPPORTED_COUNT=1
ROOT_CAUSE_UNKNOWN_COUNT=1
BACKEND_ISSUES_FIXED=2
CONTRACT_ISSUES_FIXED=2
FRONTEND_HANDOFF_ITEMS=475
AI_SERVICE_ITEMS=0
INFRASTRUCTURE_ITEMS=1
BUSINESS_CODE_PRESERVATION=PASS
HTTP_STATUS_MAPPING=PASS
PROBLEM_DETAILS_MAPPING=PASS
TRACE_CORRELATION_MAPPING=PASS
SECRET_LEAKAGE=NO
AUTH_ERROR_HANDLING=PASS
ACADEMIC_ERROR_HANDLING=PASS
FILE_ERROR_HANDLING=PASS
MESSAGING_ERROR_HANDLING=PASS
REALTIME_ERROR_HANDLING=PASS
AI_ERROR_HANDLING=PASS
KNOWLEDGE_ERROR_HANDLING=PASS
STREAM_ERROR_HANDLING=PASS
OPEN_ACCESS_REGRESSION=PASS
MEDIA_REVIEW_REGRESSION=PASS
ASSIGNMENT_POLICY_REGRESSION=PASS
MIGRATIONS_CREATED=0
PENDING_MODEL_CHANGES=NO
BUILD=PASS
WARNINGS=0
ERRORS=0
UNIT_TESTS=1499
INTEGRATION_TESTS=2274
ARCHITECTURE_TESTS=16
TESTS_TOTAL=3789
TESTS_FAILED=0
TESTS_SKIPPED=0
FULL_REGRESSION=PASS
PUSH_STATUS=NOT_RUN
REMOTE_VERIFICATION=NOT_RUN
TEST_PUBLISH=NOT_RUN
PRODUCTION_PUBLISH=NOT_RUN
READY_FOR_MANUAL_TEST_DEPLOY=NO
READY_FOR_MANUAL_PRODUCTION_DEPLOY=NO
SERVER_CONNECTION=NOT_RUN
SERVER_MIGRATION_APPLY=NOT_RUN
IIS_DEPLOYMENT=NOT_RUN
TEST_DEPLOYMENT=NOT_RUN
PRODUCTION_DEPLOYMENT=NOT_RUN
MERGE_TO_MAIN=NOT_RUN
AUDIT_COMPLETE=YES
FIXES_COMPLETE=YES_LOCAL_BACKEND_ONLY
COMMIT=NOT_RUN
PUSH=NOT_RUN
PUBLISH=NOT_RUN
RUNTIME_DEADLINE_ROOT_CAUSE=AWAITING_EVIDENCE
HEAD=ac9a23968c1449c037cd1eb7f4bae6812252b73d
FAILED_2XX_SCOPE=LOCAL_UPSTREAM_STUBS_REJECTED_NOT_LIVE_BACKEND_200
ERROR_HANDLING_PASS_SCOPE=LOCAL_BACKEND_TESTS
UNACCOUNTED_CASES=0
BLOCKED_REASON_MISSING=0
FAIL_WITHOUT_EVIDENCE=0
LIVE_ACCEPTANCE_COMPLETE=NO
LIVE_FAILURE_COUNT=1
LIVE_FAILURE_STATUS=SUPPORTED
NETWORK_REQUESTS=0
GIT_STATUS_SCOPE=PRE_COMMIT_AUDIT_CLOSURE_SNAPSHOT
RELEASE_EXECUTION_STATUS=PENDING_EXACT_COMMIT_VALIDATION
TEST_LIVE_ACCEPTANCE=NOT_RUN_ON_FINAL_COMMIT
LIVE_FAILURE_REASON=deployed response lacks stable code; compatible with missing local fix, but deployed commit/build is unknown
PRODUCTION_ARTIFACT_STATUS=CANDIDATE
```

## تفکیک حسابرسی از پذیرش زنده

هر ۱۰۷ مورد تعیین تکلیف شده است: ۱۴ PASS، یک FAIL، ۸۹ BLOCKED با دلیل و سه NOT_APPLICABLE با شاهد. وضعیت و شناسهٔ هیچ Case تغییر نکرده است. `AUDIT_COMPLETE=YES` و `LIVE_ACCEPTANCE_COMPLETE=NO` مستقل‌اند. علت تاریخی deadline همچنان `AWAITING_EVIDENCE` است. جزئیات در `AUDIT_CLOSURE_FA.md` قرار دارد.
