"""ساخت handoff از ledger واقعی؛ توصیهٔ UI به‌وضوح از رفتار اجرایی تفکیک می‌شود."""
import json
from pathlib import Path

REPORT = Path.cwd()/'reports/api-error-audit/20261125-api-errors-ac9a239-01'
ledger = json.loads((REPORT/'api-error-ledger.json').read_text(encoding='utf-8'))
intro = '''# قرارداد مدیریت خطای Frontend

این سند راهنمای اتصال است؛ هیچ فایل Frontend تغییر نکرده و موفقیت UI ادعا نمی‌شود. جدول کدها از Source فعلی استخراج شده است، نه از حدس متن خطا. `userSafeMessage` و رفتار UI در جدول **پیشنهاد handoff** هستند؛ قرار نیست بدون پیاده‌سازی کلاینت اجرا شوند. وضعیت‌های `KNOWN_EXCEPTION_DEFAULT_400` نگاشت پیش‌فرض Exception شناخته‌شده‌اند و با تست تک‌تک endpointها اشتباه نشوند.

## ترتیب تفسیر

۱. ابتدا رسیدن پاسخ HTTP را بررسی کنید. DNS/TLS/offline/CORS/Mixed Content/Abort معمولاً status قابل خواندن ندارند؛ 500 ساختگی نسازید. ۲. Content-Type و Schema را بررسی کنید؛ HTML صفحهٔ ورود یا JSON خراب با HTTP 200 موفقیت نیست. ۳. `status`، `code`، `detail`، `traceId` و `correlationId` در ProblemDetails قرارداد موجودند؛ `errors` فقط در Validation الزام معنایی دارد. ۴. branch اصلی بر اساس `code` باشد؛ متن `detail` را parse نکنید. ۵. 2xx فقط لایهٔ Transport است؛ نتیجهٔ Business و وضعیت UX جدا نگهداری شوند.

روی پاسخِ خالی یا غیر JSON، status و correlation header را حفظ کنید و fallback امن نشان دهید. `{status:401}` ناشناس BFF با Origin واقعی مشاهده شده و ProblemDetails Backend نیست؛ در این حالت ورود مجدد پیشنهاد شود، نه parser exception یا Generic 500. پاسخ مستقیم Backend برای ورود، `401 AUTH_REQUIRED` است. سازگارکردن پاسخ BFF با code/trace/correlation، handoff به مالک Frontend است؛ مخزن آن در این workspace موجود نیست.

پاسخ `POST /api/v1/ai/sources/preflight` گزارش capability است، نه اجرای AI: `200` می‌تواند با `authorized=true`، `retrievalAvailable=false` و `errorCode=AI_SOURCE_RETRIEVAL_NOT_AVAILABLE` همراه باشد. همان کد در اجرای واقعیِ غیرقابل‌بازیابی `409` است؛ این دو envelope و مرحله متفاوت‌اند و اختلاف عمدی در ledger ثبت شده است. `authorized=true` به‌تنهایی موفقیت Retrieval یا RAG نیست.

## تغییرات محدود Backend

برای خطاهای framework فاقد business code: 400=`REQUEST_INVALID`، 404=`RESOURCE_NOT_FOUND`، 405=`METHOD_NOT_ALLOWED`، 413=`REQUEST_TOO_LARGE`، 415=`UNSUPPORTED_MEDIA_TYPE`. 404 اختصاصی دامنه/مخفی‌سازی، status واقعی، header `Allow` در 405 و field errors موجود جایگزین نمی‌شوند. HEAD بدنه ندارد؛ Range نامعتبر 416 نیز قرارداد فایل موجود خود را دارد و به JSON اجباری تبدیل نشده است. این تغییر هنوز روی سرور Deploy نشده است.

در خطاهای AI/Knowledge با HTTP 429 یا 503، اگر `RetryAfter` معتبر از قبل در Exception موجود باشد، `Retry-After` عددی به ثانیه حفظ می‌شود. مقدار منفی حذف، صفر حفظ و کسر ثانیه رو به بالا گرد می‌شود. وجود header، مجوز تکرار خودکار write یا ساخت OperationId تازه نیست. این مرحله Retry، timeout یا تنظیم Service Auth را تغییر نداده است.

## خطاهای مهم

| Code | HTTP | پیام و رفتار کلاینت |
| --- | --- | --- |
| SUBMISSION_DEADLINE_PASSED | 409 | مهلت تحویل این تکلیف به پایان رسیده است. Draft و فایل قبلی حفظ؛ Assignment و Attempt تازه خوانده شوند؛ Upload دوباره یا retry کور انجام نشود. |
| CONCURRENCY_CONFLICT | 412 | نسخهٔ تازه را بخوانید؛ ورودی کاربر را حفظ و اختلاف را حل کنید. |
| MESSAGE_SAFETY_REVIEW_REQUIRED | 422 | فایل منتظر بررسی است؛ Pending با Approved/Rejected یکی نیست. پیش‌نویس حفظ؛ نه Delivered و نه Downloadable برای گیرنده. |
| MESSAGE_SAFETY_BLOCKED | 422 | فایل تأیید نشده؛ retry ارسال انجام نشود. |
| AI_OPERATION_IN_PROGRESS | 409 | همان intent در جریان است؛ درخواست تولید تازه ندهید. |
| AI_OPERATION_ALREADY_COMPLETED | 409 | intent قبلاً کامل شده؛ history مجاز را تازه کنید. پاسخ خصوصی AI دوباره از سرور replay نمی‌شود. |
| AI_OPERATION_OUTCOME_UNKNOWN | 409 | نتیجه نامعلوم؛ intent را حفظ کنید و بدون reconciliation تولید دوباره نکنید. |
| AI_SERVICE_UNAUTHORIZED / AI_SERVICE_FORBIDDEN | 503 | خطای Service Auth سمت سرویس است؛ کاربر را به login loop نبرید و credential سرویس درخواست نکنید. |
| AI_SOURCE_PROCESSING | 409 | آماده‌سازی منبع ادامه دارد؛ وضعیت همان نسخه را بخوانید. |
| AI_SERVICE_TIMEOUT | 504 | مهلت سرویس تمام شد؛ با cancel کاربر یا timeout مرورگر یکی نیست. |

کد دقیق جدول از ledger می‌آید؛ برای مثال نام واقعی وضعیت نامعلوم عملیات در جدول کامل ملاک است. پیام انگلیسی عمومی یا Generic Conflict نباید کد اختصاصی Deadline را بپوشاند.

## SSE، صفحه و بازیابی

پیش از `start`، خطا HTTP ProblemDetails است. بعد از `start` ممکن است HTTP اولیه 200 بماند و `event:error` نتیجهٔ ناموفق را اعلام کند؛ متن partial را موفق یا نهایی نکنید. فقط `done` معتبر، نتیجهٔ موفق است. EOF بدون done/error، stream ناقص است؛ این طبقه‌بندی کلاینت است و HTTP 500 ساختگی نیست. `requestId` و correlation از start/header به interaction بسته شوند. delta قدیمی نباید state گفتگوی جدید را تغییر دهد. لغو کاربر، بستن تب و قطع شبکه از timeout بالادست جدا بمانند. stream را با OperationId تازه به‌طور خودکار شروع نکنید.

General/Auto/SourcesOnly و SourceIntent مستقل‌اند: در Auto با منبع انتخابی، پیش‌فرض SourceFocused است؛ Processing به‌صورت ضمنی General نمی‌شود. Auto+General می‌تواند fallback روشن و provenance مناسب بدهد؛ SourcesOnly هیچ fallback پنهان ندارد. upload موفق، File Ready، Messaging attachment، Academic attachment، Knowledge Source، ingest، retrieve و RAG مراحل متفاوت‌اند.

قرارداد Published Assignment Policy قبلی در Working Tree حفظ شده: `PATCH /api/v1/academic/assignments/{assignmentId}/submission-policy` با `dueAtUtc`، `allowLateSubmission`، `expectedAssignmentRowVersion` و `expectedVersionRowVersion`. فقط تمدید به UTC آینده مجاز است؛ allowLate مستقل تغییر می‌کند. رکوردهای Submitشده و receipt/IsLate تاریخی بازنویسی نمی‌شوند. این مسیر در OpenAPI زندهٔ این ممیزی وجود نداشت و نباید UI به انتشار آن فرض کند.

## Class / Workspace

در کد موجود، GET مجموعهٔ کلاس‌ها به Personal/Parent می‌تواند `403 ACADEMIC_READ_NOT_ALLOWED` بدهد؛ PATCH کلاس به Manager جاری نیاز دارد (`403 MANAGER_ROLE_REQUIRED`) و منبع ناموجود/خارج از سازمان می‌تواند `404 CLASS_NOT_FOUND` باشد. GET جزئیات عمومی کلاس `{id}` در قرارداد وجود ندارد؛ همان مسیر با PATCH تعریف شده و GET می‌تواند 405 باشد. concealment عمدی را به 403 تبدیل نکنید. این تحلیل کد، Root Cause رخداد واقعی 403/404 قبلی نیست؛ Method/Route/Workspace/role/code/correlation واقعی همچنان لازم است.

## وضعیت‌های خارج از تست زنده

حساب‌ها فقط از `MakanAppWebApplicationFactory`، `CreateMessagingUserAsync`، `CreateOfficialScenarioAsync` و helperهای فعلی ساخته می‌شوند. aliasهای گزارش: fixture-personal-01، fixture-student-01، fixture-parent-01، fixture-teacher-01، fixture-manager-01. هیچ شناسهٔ واقعی، شماره، OTP یا Token صادرشده به handoff منتقل نمی‌شود. تست SQL فقط روی دیتابیس ایزولهٔ Integration است. تمام رفتار UI دارای ورود، timeline کارت‌ها و شبکهٔ مرورگر بدون credential زنده، BLOCKED هستند.

مبانی HTTP: [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html#section-15) و [IANA](https://www.iana.org/assignments/http-status-codes). جدول زیر رفتار پیشنهادی کلاینت برای هر کد موجود است؛ هیچ status غیرمرتبط برای افزایش پوشش ساخته نشده است.

## جدول کامل کدها

| Code | HTTP | پیام فارسی پیشنهادی | Retry | Refetch | Auth | UI state / action |
| --- | --- | --- | --- | --- | --- | --- |
'''
lines = [intro]
for error in ledger['errors']:
    lines.append('| ' + ' | '.join([
        error['code'], str(error['httpStatus']), error['userSafeMessage'], error['retryable'],
        'YES' if error['requiresRefetch'] else 'NO', 'REAUTH' if error['requiresReauth'] else 'KEEP_SESSION',
        error['frontendAction']]) + ' |\n')
(REPORT/'FRONTEND_ERROR_HANDOFF_FA.md').write_text(''.join(lines), encoding='utf-8')
print('HANDOFF_CODES=' + str(len(ledger['errors'])))
