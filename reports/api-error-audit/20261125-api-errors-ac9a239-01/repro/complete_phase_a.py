"""ثبت مستقل Phase A؛ هیچ source محصول تغییر نمی‌کند."""
import csv
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path.cwd()
REPORT = ROOT / 'reports/api-error-audit/20261125-api-errors-ac9a239-01'
PRIVATE = ROOT / '.artifacts/api-error-audit-20261125'
baseline = json.loads((PRIVATE / 'baseline.json').read_text())
protected = [{'path': x['path'], 'unchanged': hashlib.sha256((ROOT/x['path']).read_bytes()).hexdigest() == x['sha256']}
             for x in baseline['protectedFiles']]
assert all(x['unchanged'] for x in protected), 'PHASE_A_PRODUCT_SOURCE_MUST_BE_UNCHANGED'


def write(name, value):
    (REPORT/name).write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


rows = list(csv.DictReader((REPORT/'cases.csv').open(encoding='utf-8-sig', newline='')))
reasons = {
    'E': 'نیازمند نشست/fixture زنده و اجرای UI؛ صفحهٔ عمومی ورود مشاهده شد، ورود یا ارسال انجام نشد.',
    'U': 'نیازمند فایل fixture، UI دارای نشست و timeline انتقال/پردازش؛ Upload با Ready/Source/Attach یکی نیست.',
    'A': 'کارت‌های UI پشت ورود هستند؛ ۱۴ عملیات Backend استخراج شد، template یا کارت حدس زده نشد.',
    'I': 'پذیرش race سرتاسری و intentهای UI نیازمند fixture مجاز است؛ اجرای محلی بعداً جدا ثبت می‌شود.',
    'H': 'history و lifecycle صفحهٔ واردشده در دسترس نیست؛ تست محلی به معنی UI PASS نیست.',
    'C': 'بخش عمومی یا محلی فقط variant محدود را می‌پوشاند؛ پوشش کامل ورودی/نشست/ACL زنده مسدود است.',
    'S': 'شکست dependency واقعی به‌طور عمدی ایجاد نشده؛ دامنهٔ fault محلی جداگانه ثبت و تست می‌شود.',
    'N': 'برای پذیرش مرورگر، قطع شبکه/abort/redirect دارای نشست و recovery به fixture UI نیاز است.',
    'Q': 'دادهٔ نقش/AI واقعی در دسترس نیست؛ Open Access جاری از حالت تجاری قدیمی جداست.'}
for row in rows:
    row.update(status='BLOCKED', reason=reasons[row['id'][0]], transportResult='NOT_RUN_FULL_BASE_CASE',
        businessResult='NOT_RUN_FULL_BASE_CASE', userExperienceResult='BLOCKED_AUTHORIZED_UI',
        expectedBehaviorSource='references/cases-template.csv؛ سیاست جاری Open Access بر فرض تجاری قدیمی مقدم است.',
        evidencePaths='evidence/public-contract; evidence/public-browser.json; action-catalog.json')
    if row['id'] == 'C08':
        row.update(status='FAIL', evidenceKind='REAL_API', transportResult='HTTP_405',
            businessResult='BUSINESS_CODE_MISSING', reason='در GET مسیر POST واقعی، 405 بدون ProblemDetails/code مشاهده شد؛ قرارداد پایدار خطا ناقص است.',
            evidencePaths='evidence/public-probes/method.json; evidence/local-attempts/phase-a-http-04')
    elif row['id'] in {'C09', 'C15'}:
        row.update(status='FAIL', evidenceKind='LOCAL_INTEGRATION_FAULT', transportResult='HTTP_RESPONSE',
            businessResult='FRAMEWORK_CODE_MISSING', reason='در variant Content-Type نادرست یا Accept متفاوت، error code فریم‌ورک حفظ نشده است؛ UI هنوز اجرا نشده.',
            evidencePaths='evidence/local-attempts/phase-a-http-04; evidence/validation/phase-a-http-04.json')
    elif row['id'] in {'Q02', 'Q03'}:
        row.update(status='NOT_APPLICABLE', reason='برای Featureهای Open Access، کمبود/آخرین واحد سهمیه مسیر معتبر این Release نیست؛ تست‌های Metered تاریخی صرفاً regression حالت opt-in هستند.',
            userExperienceResult='NOT_APPLICABLE_OPEN_ACCESS', evidencePaths='MakanApp.Application/Ai/AiUsageOptions.cs; tests/MakanApp.IntegrationTests/OpenAccessKnowledgeAcceptanceTests.cs')
    elif row['id'] == 'H07':
        row.update(status='NOT_APPLICABLE', reason='در قرارداد فعلی AI Conversation هیچ endpoint تغییر نام/metadata وجود ندارد؛ قابلیت Messaging با AI Conversation یکی نیست.')
with (REPORT/'cases.csv').open('w', encoding='utf-8-sig', newline='') as stream:
    writer = csv.DictWriter(stream, fieldnames=list(rows[0])); writer.writeheader(); writer.writerows(rows)

issues = [
    dict(id='ERR-001', severity='Medium', owner='backend', confidence='CONFIRMED', phase='A', status='OPEN',
        summary='پاسخ‌های 404/405 خالی و 415 فریم‌ورک فاقد code پایدار هستند.',
        source=['MakanApp.Api/Program.cs','MakanApp.Api/Errors/ApiErrorCodes.cs'],
        evidence=['evidence/public-probes/method.json','evidence/public-probes/missing-route.json','evidence/validation/phase-a-http-04.json'],
        scope='404/405 زنده و محلی؛ 415 محلی. این نقص علت تاریخی Class 403/404 را ثابت نمی‌کند.',
        before='۵ تست قرارداد فریم‌ورک FAIL؛ status اصلی حفظ شده ولی code غایب است.'),
    dict(id='ERR-002', severity='Medium', owner='backend', confidence='CONFIRMED', phase='A', status='OPEN',
        summary='RetryAfter موجود در Exceptionهای AI/Knowledge در خروجی HTTP حذف می‌شود.',
        source=['MakanApp.Api/Errors/GlobalExceptionHandler.cs'],
        evidence=['evidence/validation/phase-a-http-04.json','evidence/local-attempts/phase-a-http-04'],
        scope='چهار HTTP fixture با 429/503 و بازهٔ مشخص؛ بدون تماس واقعی با AI.',
        before='چهار تست header FAIL؛ status/code صحیح ولی Retry-After غایب است.'),
    dict(id='DEPLOY-001', severity='Medium', owner='infrastructure', confidence='SUPPORTED', phase='A', status='EXTERNAL_PENDING',
        summary='قرارداد زنده با Working Tree یکسان نیست؛ deadline policy منتشر نشده است.',
        evidence=['api-contract-map.json'], scope='۴۹۷ عملیات زنده در برابر ۴۹۸ محلی؛ Build/Commit واقعی زنده UNKNOWN.'),
    dict(id='RUNTIME-001', severity='High', owner='unknown', confidence='UNKNOWN', phase='A', status='AWAITING_OWNER_EVIDENCE',
        summary='Root Cause تاریخی Deadline و Class 403/404 بدون Evidence SQL/Runtime هنوز قطعی نیست.',
        scope='assignment-incident-01 / attempt-incident-01 / file-incident-01؛ صرفاً Evidence تاریخیِ مرحله Attach، نه خطای Upload.',
        correlationId='2b00c7c1fb1fbd13916ed88dd2df0320', evidence=['docs/product/ASSIGNMENT_SUBMISSION_POLICY_FRONTEND_HANDOFF_FA.md'])]
write('evidence/issues-phase-a.json', issues)
write('evidence/protected-phase-a.json', {'passed': True, 'count': len(protected), 'files': protected})
write('evidence/public-browser.json', {
    'evidenceKind': 'REAL_UI_BACKEND', 'scope': 'ANONYMOUS_LOGIN_PAGE_ONLY_NO_AUTHENTICATED_BACKEND_ACTION',
    'provider': 'TinyFish', 'runId': '148174fd-cc95-4394-b841-90d4b907297b',
    'finalUrl': 'https://testapp.makanapp.org/enter', 'title': 'ورود | ماکان اپ',
    'signInRequired': True, 'aiActionCardsObservable': False,
    'visibleControls': ['شماره همراه', 'دریافت کد تأیید', 'بازگشت', 'راهنمای ورود'],
    'credentialsEntered': False, 'formSubmitted': False, 'uiScenariosPassed': 0,
    'transportResult': 'PUBLIC_PAGE_LOADED', 'businessResult': 'NOT_RUN_AUTHENTICATED',
    'userExperienceResult': 'LOGIN_PAGE_ONLY', 'durationSeconds': 33, 'retrievedAtUtc': datetime.now(timezone.utc).isoformat()})

(REPORT/'BACKEND_TICKETS_FA.md').write_text('''# یافته‌های Backend در Phase A

دو Issue قابل بازتولید `ERR-001` و `ERR-002` در `evidence/issues-phase-a.json` ثبت شده‌اند. نخست پنج variant قرارداد HTTP فریم‌ورک و سپس چهار variant header شکست خوردند. نتیجهٔ نهایی Phase A: ۲۴ موفق، ۹ ناموفق، صفر Skipped از ۳۳ تست.

تست اولیهٔ RateLimiter با ظرفیت صفر خود fixture را نامعتبر می‌کرد؛ خطای 500 آن **Issue محصول نیست**. fixture اصلاح شد: ابتدا درخواست سالم واقعاً تنها permit را مصرف می‌کند، سپس 429 آزمایش می‌شود. هر سه Accept در تست معتبر RateLimiter موفق‌اند و هیچ Fix برای این بخش پیشنهاد نشده است. اجرای `phase-a-http-03` برای شفافیت حفظ شده است.

هیچ schema، Auth، Safety، timeout یا منطق AI/Assignment در Phase A تغییر نکرد. hashes تمام ۵۷ مسیر محافظت‌شده یکسان است. metadata زنده با Working Tree متفاوت است؛ این موضوع باگ ۴۰۹ تاریخی را ثابت نمی‌کند.
''', encoding='utf-8')
(REPORT/'FRONTEND_AI_FINDINGS_FA.md').write_text('''# یافته‌های Frontend و AI

صفحهٔ عمومی به `/enter` می‌رود؛ ورود لازم است و هیچ کارت AI قابل مشاهده نیست. هیچ شماره، OTP یا نشست تولید/مصرف نشد. fixture امنِ نقش‌ها ارائه نشده است. رفتار کارت، spinner، toast، AbortController و ترتیب نمایش پاسخ‌ها BLOCKED است؛ Backend API، مشاهدهٔ UI عمومی و تست محلی جدا هستند.

درخواست ناشناس BFF بدون Origin، `403 ORIGIN_REJECTED` برگرداند. این درخواست مرورگر معمولی نیست و **دلیل اثبات خرابی CORS نیست**؛ Origin واقعی جدا آزمایش می‌شود. CORS preflight مستقیم API پاسخ 204 داد؛ این هم به‌تنهایی Mixed Content یا موفقیت مرورگر را ثابت نمی‌کند.

مقدار `template=null` در action catalog به معنی نبود کارت محصول نیست؛ قرارداد Backend template کارت‌ها را تعریف نمی‌کند. متن خطای مخصوص Deadline باید از business code انتخاب شود و زیر پیام Generic Conflict پنهان نشود. هیچ ادعای UI Fix یا AI production PASS وجود ندارد.
''', encoding='utf-8')
(REPORT/'REPORT_FA.md').write_text('''# گزارش Phase A — ممیزی خطای API

هر ۱۰۷ ID مرجع بدون حذف/ادغام در `cases.csv` ثبت شده است. ۳ Case یافتهٔ قرارداد دارند، ۳ مورد با علت مستند قابل اعمال نیست و ۱۰۱ Case برای پذیرش کامل دارای مانع UI/fixture/runtime هستند. این حسابرسی به معنی اجرای ۱۰۷ سناریوی زنده نیست.

مرجع‌ها ۳/۳ با manifest تطبیق دارند؛ نسخهٔ تکراری بدون `(1)` یکسان بود و فایل دارای `(1)` یافت نشد. قرارداد زنده ۴۹۷ عملیات و metadata محلی ۴۹۸ عملیات دارد. مسیر اضافی policy منتشرشده متعلق به WIP قبلی است؛ ۸ عملیات و ۴ schema نیز متفاوت‌اند. محیط از سوی مالک Test معرفی شده ولی نام محیط/Commit/Build مستقر از پاسخ عمومی قابل اثبات نیست.

`ERR-001`: حذف code در 404/405 و 415 فریم‌ورک؛ `ERR-002`: حذف Retry-After موجود در Exceptionهای AI/Knowledge. Evidence قبل از Fix: `phase-a-http-04`، ۳۳ تست، ۲۴ موفق و ۹ ناموفق. هیچ شکست مورد انتظار به موفقیت تبدیل نشده است.

Baseline قبلی ۳۷۳۸ تست فقط تاریخی است؛ Full Regression این Task هنوز اجرا نشده است. Incident قبلی Upload failure نیست، اما `RUNTIME_DEADLINE_ROOT_CAUSE=AWAITING_EVIDENCE` حفظ می‌شود. به SQL/سرور مدیریتی متصل نشده‌ایم. هیچ migration، commit، push، publish یا deploy انجام نشده است.

برای معنای HTTP از [RFC 9110 §15](https://www.rfc-editor.org/rfc/rfc9110.html#section-15) و [ثبت IANA](https://www.iana.org/assignments/http-status-codes) استفاده شد؛ status غیرمرتبط برای پوشش آماری ساخته نشده است. پاسخ 2xx بدون نتیجهٔ تجاری سالم، موفقیت محسوب نمی‌شود.
''', encoding='utf-8')
(REPORT/'coverage.md').write_text('''# پوشش Phase A

۱۰۷ پایه: ۳ FAIL، ۱۰۱ BLOCKED، ۳ NOT_APPLICABLE، صفر PASS و صفر NOT_RUN. «اجراشده» در این شمارش یعنی Case پایه با نتیجهٔ قطعی PASS/FAIL، نه تعداد درخواست‌های فرعی. انواع Evidence و تعداد درخواست‌ها در attempts.jsonl جدا محاسبه خواهند شد.

Timingهای 0/100/300/1000/3000/10000ms در UI زنده اجرا نشده‌اند. Race تکرارشوندهٔ زنده اجرا نشده؛ هیچ sleep، PASS یا x/10 فرضی ثبت نشده است. موردهای مستقل Backend با SQL ایزوله و fault محلی در مرحلهٔ validation اجرا می‌شوند.

کدهای 402/406/408/410/412/423/424/425/428/501/507 تنها با قرارداد/مسیر واقعی بررسی می‌شوند؛ نبود مسیر یا مشاهده، با ساختن خطای جعلی پوشانده نمی‌شود.
''', encoding='utf-8')
(REPORT/'RESUME_FA.md').write_text('''# ادامهٔ ممیزی

Phase A شواهد قابل دسترس را ثبت کرده است؛ پذیرش کل Audit مسدود به fixtureهای مجاز UI و Evidence Runtime مالک است. Fix فقط برای ERR-001 و ERR-002 مجاز و متکی به تست ناموفق است. Phase B بعدی: Fix محدود API، تست متمرکز، Full Regression و review. مجوز commit/push/publish به سبزشدن همه gateها وابسته است؛ تغییرات قبلی نباید وارد Commit شوند.
''', encoding='utf-8')
write('evidence/phase-a-completion.json', {
    'completedAtUtc': datetime.now(timezone.utc).isoformat(), 'phaseAAvailableEvidenceComplete': True,
    'auditReleaseGateComplete': False, 'baseCasesAccountedFor': len(rows), 'protectedFilesUnchanged': True,
    'productLogicChanged': False, 'confirmedBackendIssues': ['ERR-001','ERR-002'],
    'failingTestsBeforeFix': {'total':33,'passed':24,'failed':9,'skipped':0},
    'filesSha256': {p.relative_to(REPORT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                    for p in REPORT.iterdir() if p.is_file()}})
print('PHASE_A_RECORDED_NO_PRODUCT_LOGIC_CHANGE')
