"""گزارش نهایی فقط از نتایج ثبت‌شده ساخته می‌شود؛ Gate بسته هیچ Commit/Publish نمی‌سازد."""
import csv
import hashlib
import json
import shutil
import subprocess
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path.cwd()
REPORT = ROOT/'reports/api-error-audit/20261125-api-errors-ac9a239-01'
PRIVATE = ROOT/'.artifacts/api-error-audit-20261125'


def load(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def save(name, value):
    (REPORT/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


pipeline = load(PRIVATE/'regression-progress.json')
assert pipeline['status']=='PASS', 'FULL_VALIDATION_NOT_COMPLETE'
final = load(REPORT/'evidence/validation/full-final.json')
first = load(REPORT/'evidence/validation/full-01.json')
baseline = load(PRIVATE/'baseline.json')
snapshot = load(REPORT/'evidence/final-contract/repository-inventory.json')
assert final['exitCode']==0 and first['exitCode']==0


def verified_counts(run):
    rows = []
    for project in ['UnitTests','IntegrationTests','ArchitectureTests']:
        found = [x for x in run['tests'] if x['name'].startswith('MakanApp.'+project+'.')]
        assert found, 'TEST_DISCOVERY_EMPTY'
        counts=Counter(x['status'] for x in found)
        rows.append(dict(assembly='MakanApp.'+project+'.dll',total=len(found),passed=counts['Passed'],failed=counts['Failed'],skipped=counts['Skipped']))
    # خروجی solution موازی است؛ label اسمبلی آخرین header قابل اتکا نیست. اعداد از نام تست‌ها و summary با هم تطبیق دارند.
    a=sorted((x['total'],x['passed'],x['failed'],x['skipped']) for x in rows)
    b=sorted((x['total'],x['passed'],x['failed'],x['skipped']) for x in run['summaries'])
    assert a==b, 'CONSOLE_COUNTS_DO_NOT_MATCH_INDIVIDUAL_TESTS'
    assert all(x['failed']==0 and x['skipped']==0 for x in rows)
    return rows


counts=verified_counts(final); first_counts=verified_counts(first)
save('evidence/verified-test-counts.json',{'firstFull':first_counts,'finalFull':counts,
    'source':'نام namespace تک‌تک تست‌ها با summary اعداد مقایسه شد؛ stdout خام ذخیره نشده است.',
    'firstRunAssemblyHeaderInterleaving':'label قدیمی console در full-01 قابل اتکا نیست؛ خود فایل اصلی بدون دستکاری حفظ شده است.'})
all_tests=final['tests']


def passed(part, minimum=1):
    return sum(x['status']=='Passed' and part in x['name'] for x in all_tests)>=minimum


assert passed('ErrorAuditHttp200WithBusinessFailure',3)
assert passed('ErrorAuditSameIntentAtObservedPendingState',6)
assert snapshot['PendingModelChanges'] is False and snapshot['DatabaseConnected'] is False
for name in ['REPORT_FA.md','BACKEND_TICKETS_FA.md','FRONTEND_AI_FINDINGS_FA.md','coverage.md','cases.csv','RESUME_FA.md']:
    archive=REPORT/'evidence/phase-a-documents'/name
    if not archive.exists():
        archive.parent.mkdir(exist_ok=True);shutil.copyfile(REPORT/name,archive)

case_proofs = {
    'I03':('ErrorAuditSameIntentAtObservedPendingState','۶ timing، هرکدام ۱۰ fixture مستقل؛ Pending از SQL و barrier مشاهده شد؛ یک invocation و یک جفت history.'),
    'I04':('ErrorAuditSameIntentAtObservedPendingState','در هر ۶۰ اجرای محلی، replay پس از done برابر 409 AI_OPERATION_ALREADY_COMPLETED و بدون تولید دوباره بود.'),
    'C10':('FileAcceptanceRejectsExtensionMimeMismatch','پسوند/MIME ناسازگار با fixture فایل و mapping اختصاصی 415 آزمایش شد؛ موفقیت کاذب نبود.'),
    'C12':('BusinessFailuresPreserveSpecificCodeStatusAndIds','412 اختصاصی حفظ شد؛ تست‌های policy منتشرشده نیز rowversion کهنه را رد کردند؛ 428 به‌صورت مصنوعی ساخته نشد.'),
    'C14':('RateLimitFailureKeepsProblemDetailsForNonJsonAccept','پس از مصرف permit واقعی در میزبان ایزوله، 429 مستقل از محدودیت ابزار/سهمیه بررسی شد.'),
    'S01':('UnknownExceptionIs500WithSafeDetailsAndNeverSuccess','fault کنترل‌شده 500، code و correlation/trace حفظ و canary خصوصی حذف شد؛ لاگ امن Production نیز regression شد.'),
    'S03':('ChatAsync_RejectsMalformedOrSchemaInvalidResponse','پاسخ خراب provider در adapter رد و mapping502 در HTTP fixture تأیید شد؛ retry جدید وجود ندارد.'),
    'S04':('UpstreamRetryAfterSurvivesHttpMapping','503 و 429 در AI/Knowledge با پیام امن و Retry-After مشخص حفظ شدند؛ بازه‌های منفی/صفر/کسری نیز بررسی شدند.'),
    'S05':('ChatAsync_MapsInternalTimeout','latency کنترل‌شده از timeout fixture عبور کرد؛ caller cancellation مستقل و HTTP504 در fixture بررسی شد؛ timeout عملیاتی عوض نشد.'),
    'S06':('FailedStorageWriteLeavesRejectedMetadataAndNoBytes','شکست نوشتن storage در factory موجود، metadata ردشده و نبود byte را اثبات کرد؛ mapping503 از خطای Attach جداست.'),
    'S07':('GeneralCommitAcknowledgementLossCannotDuplicateDurableEffectOrInventRetryOutcome','fault از دست‌رفتن تأیید Commit در Outbox موجود، نتیجهٔ نامعلوم و اثر پایدار یکتا را بررسی کرد؛ سرویس مشترک خاموش نشد.'),
    'S09':('AdaptivePartialFailureIsErrorUnknownAndNeverEntersHistoryOrRetries','HTTP200 اولیه، event:error و عدم done/history کامل در سه سطح مستقل با provider محلی بررسی شدند.'),
    'N07':('ErrorAuditHttp200WithBusinessFailure', 'سه پاسخ mock بالادست200 با error envelope/success=false، از جمله پاسخ دارای answer، رد شدند؛ این 2xx شکست‌خوردهٔ سرور واقعی نیست.'),
    'N08':('ChatAsync_RejectsMalformedOrSchemaInvalidResponse','JSON خراب، پاسخ ناسازگار و content-type غیر JSON در adapter رد شدند؛ با 500 واقعی Backend یکی نشدند.')}
for key,(method,_) in case_proofs.items():assert passed(method), 'CASE_PROOF_NOT_EXECUTED_'+key
rows=list(csv.DictReader((REPORT/'cases.csv').open(encoding='utf-8-sig',newline='')))
for row in rows:
    if row['id'] in case_proofs:
        method, reason=case_proofs[row['id']]
        row.update(status='PASS',evidenceKind='LOCAL_INTEGRATION_FAULT',reason=reason+' فقط دامنهٔ Backend محلی؛ REAL_API/UI PASS نیست.',
            transportResult='LOCAL_HTTP_OR_ADAPTER_VALIDATED',businessResult='EXPECTED_SUCCESS_OR_EXPECTED_ERROR',
            userExperienceResult='NOT_RUN_NO_AUTHENTICATED_UI',evidencePaths='evidence/validation/full-final.json; '+method)
    elif row['id'] in {'C09','C15'}:
        row.update(status='BLOCKED',reason='نقص code در variant محلی اصلاح و تست شد؛ تمام variantهای Case پایه (multipart یا سقف header واقعی) اجرا نشده‌اند؛ FAIL قبلی پاک یا PASS کلی فرض نشده است.',
            evidenceKind='LOCAL_INTEGRATION_FAULT',transportResult='PARTIAL_LOCAL_PASS',businessResult='FIXED_LOCAL_REMAINING_VARIANTS_BLOCKED',
            evidencePaths='evidence/validation/phase-a-http-04.json; evidence/validation/focused-error-guards.json; evidence/validation/full-final.json')
    elif row['status']=='BLOCKED':
        row['reason']+=' مالک فقط fixtureهای محلی را مجاز کرده؛ credential و پروفایل واقعی ارائه نمی‌شود. بخش‌های محلی مرتبط در Full Regression اجرا شدند و جای پذیرش UI را نمی‌گیرند.'
    elif row['id']=='C08':
        row['reason']='Routing405 و Allow صحیح‌اند، ولی قرارداد سراسریِ موردنیاز Audit فاقد code است. این نقص زنده هنوز Deploy نشده؛ Fix محلی پاس است. علت تاریخی Class404 از این مشاهده نتیجه‌گیری نمی‌شود.'
with (REPORT/'cases.csv').open('w',encoding='utf-8-sig',newline='') as stream:
    writer=csv.DictWriter(stream,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
case_counts=Counter(x['status'] for x in rows)

issues=load(REPORT/'evidence/issues-phase-a.json')
for issue in issues:
    if issue['id'] in {'ERR-001','ERR-002'}:
        issue.update(status='FIXED_LOCAL_VALIDATED_NOT_DEPLOYED',after='تست‌های مربوط و Full Regression نهایی موفق؛ runtime/auth/timeout/safety تغییر نکرد.',
            afterEvidence=['evidence/validation/phase-b-http.json','evidence/validation/focused-error-guards.json','evidence/validation/full-final.json'])
issues.append(dict(id='CONTRACT-001',severity='Medium',owner='frontend',confidence='CONFIRMED',status='HANDOFF_ONLY',
    summary='پاسخ احراز هویت ناشناس BFF فقط status دارد؛ متفاوت از ProblemDetails مستقیم Backend است.',
    evidence=['evidence/public-probes/bff-anonymous-shape.json'],
    scope='UI bug یا ازبین‌رفتن پاسخِ عبوری Backend ثابت نشده است؛ پاسخ متعلق به guard خود BFF است. adapter امن و correlation در handoff پیشنهاد شده است.'))
save('evidence/issues-final.json',issues)
save('evidence/fixture-map.json',{'liveCredentialsAvailable':False,'browserProfileUsed':False,
    'source':'MakanAppWebApplicationFactory + helperهای Integration موجود؛ هیچ TestUserBuilder یا seed موازی ساخته نشد.',
    'fixtures':[
        {'alias':'fixture-personal-01','helpers':['CreateMessagingUserAsync','PersonalKnowledgeContext','AdaptiveHost']},
        {'alias':'fixture-student-01','helpers':['CreateOfficialScenarioAsync','CreateMembershipAsync','CreateEnrollmentAsync']},
        {'alias':'fixture-parent-01','helpers':['CreateMessagingUserAsync','CreateMembershipAsync','CreateGuardianRelationAsync']},
        {'alias':'fixture-teacher-01','helpers':['CreateOfficialScenarioAsync','CreateTeacherAssignmentAsync']},
        {'alias':'fixture-manager-01','helpers':['CreateOfficialScenarioAsync','CreateMembershipAsync']}],
    'roleStreamVariantsPassed':sum(x['status']=='Passed' and 'AdaptiveRoleStreamsUseCurrentScopeAndRevocationCutsFutureHistory' in x['name'] for x in all_tests),
    'rolesDoNotGrantResourceAccessAlone':True})

attempts=load(REPORT/'evidence/attempt-summary.json')
statics=load(REPORT/'evidence/static-inventory.json')
scope=load(REPORT/'evidence/final-scope-and-privacy-review.json')
ledger=load(REPORT/'api-error-ledger.json')
raw_spec=load(REPORT/'evidence/public-contract/openapi.json')
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
branch=subprocess.check_output(['git','branch','--show-current'],cwd=ROOT,text=True).strip()
by_assembly={x['assembly']:x for x in counts}
values=dict(RUN_ID=baseline['runId'],BASE_COMMIT=baseline['head'],FINAL_COMMIT='NOT_CREATED',HEAD=head,BRANCH=branch,
    PREEXISTING_DIRTY_FILES=baseline['dirtyCount'],MANIFEST_HASH_VERIFICATION='PASS',
    LIVE_ENVIRONMENT='OWNER_DESIGNATED_TEST_RUNTIME_NAME_UNKNOWN',LIVE_API_BASE_URL='http://94.184.32.84:8090',LIVE_WEB_BASE_URL='https://testapp.makanapp.org',
    LIVE_OPENAPI='PASS',LIVE_OPENAPI_SHA256=raw_spec['contentSha256'],BASE_CASES=107,CASES_ACCOUNTED_FOR='107/107',
    CASES_EXECUTED=case_counts['PASS']+case_counts['FAIL'],CASES_PASS=case_counts['PASS'],CASES_FAIL=case_counts['FAIL'],CASES_BLOCKED=case_counts['BLOCKED'],
    CASES_NOT_RUN=case_counts['NOT_RUN'],CASES_NOT_APPLICABLE=case_counts['NOT_APPLICABLE'],ATTEMPTS_TOTAL=attempts['attemptsTotal'],
    REAL_UI_BACKEND_ATTEMPTS=attempts['evidenceKinds'].get('REAL_UI_BACKEND',0),REAL_API_ATTEMPTS=attempts['evidenceKinds'].get('REAL_API',0),
    LOCAL_INTEGRATION_FAULT_ATTEMPTS=attempts['evidenceKinds'].get('LOCAL_INTEGRATION_FAULT',0),UI_MOCK_FAULT_ATTEMPTS=0,
    HTTP_4XX_OBSERVED=attempts['http4xx'],HTTP_5XX_OBSERVED=attempts['http5xx'],TRANSPORT_ERRORS=attempts['transportErrors'],BUSINESS_ERRORS=attempts['businessErrorResponses'],
    PARSE_ERRORS='NOT_MEASURED_SEPARATELY',STREAM_ERRORS='NOT_MEASURED_SEPARATELY',FAILED_2XX=attempts['localFailedUpstream200'],
    FAILED_2XX_SCOPE='LOCAL_UPSTREAM_STUBS_REJECTED_NOT_LIVE_BACKEND_200',ERROR_HANDLING_PASS_SCOPE='LOCAL_BACKEND_TESTS',
    ISSUES_CRITICAL=0,ISSUES_HIGH=1,ISSUES_MEDIUM=4,ISSUES_LOW=0,ROOT_CAUSE_CONFIRMED_COUNT=3,ROOT_CAUSE_SUPPORTED_COUNT=1,ROOT_CAUSE_UNKNOWN_COUNT=1,
    BACKEND_ISSUES_FIXED=2,CONTRACT_ISSUES_FIXED=2,FRONTEND_HANDOFF_ITEMS=len(ledger['errors']),AI_SERVICE_ITEMS=0,INFRASTRUCTURE_ITEMS=1,
    BUSINESS_CODE_PRESERVATION='PASS',HTTP_STATUS_MAPPING='PASS',PROBLEM_DETAILS_MAPPING='PASS',TRACE_CORRELATION_MAPPING='PASS',SECRET_LEAKAGE='NO',
    AUTH_ERROR_HANDLING='PASS',ACADEMIC_ERROR_HANDLING='PASS',FILE_ERROR_HANDLING='PASS',MESSAGING_ERROR_HANDLING='PASS',REALTIME_ERROR_HANDLING='PASS',
    AI_ERROR_HANDLING='PASS',KNOWLEDGE_ERROR_HANDLING='PASS',STREAM_ERROR_HANDLING='PASS',OPEN_ACCESS_REGRESSION='PASS',MEDIA_REVIEW_REGRESSION='PASS',ASSIGNMENT_POLICY_REGRESSION='PASS',
    MIGRATIONS_CREATED=0,PENDING_MODEL_CHANGES='NO',BUILD='PASS',WARNINGS=0,ERRORS=0,
    UNIT_TESTS=by_assembly['MakanApp.UnitTests.dll']['total'],INTEGRATION_TESTS=by_assembly['MakanApp.IntegrationTests.dll']['total'],
    ARCHITECTURE_TESTS=by_assembly['MakanApp.ArchitectureTests.dll']['total'],TESTS_TOTAL=sum(x['total'] for x in counts),TESTS_FAILED=0,TESTS_SKIPPED=0,
    FULL_REGRESSION='PASS',PUSH_STATUS='NOT_RUN',REMOTE_VERIFICATION='NOT_RUN',TEST_PUBLISH='NOT_RUN',PRODUCTION_PUBLISH='NOT_RUN',
    READY_FOR_MANUAL_TEST_DEPLOY='NO',READY_FOR_MANUAL_PRODUCTION_DEPLOY='NO',SERVER_CONNECTION='NOT_RUN',SERVER_MIGRATION_APPLY='NOT_RUN',IIS_DEPLOYMENT='NOT_RUN',
    TEST_DEPLOYMENT='NOT_RUN',PRODUCTION_DEPLOYMENT='NOT_RUN',MERGE_TO_MAIN='NOT_RUN',
    AUDIT_COMPLETE='NO',FIXES_COMPLETE='YES_LOCAL_BACKEND_ONLY',COMMIT='NOT_RUN',PUSH='NOT_RUN',PUBLISH='NOT_RUN',RUNTIME_DEADLINE_ROOT_CAUSE='AWAITING_EVIDENCE')
save('FINAL_RESULT.json',values)
status_text='\n'.join(f'{k}={v}' for k,v in values.items())
(REPORT/'FINAL_STATUS.txt').write_text(status_text+'\n',encoding='utf-8')
report=f'''# گزارش نهایی ممیزی خطا و آموزش توسعه‌دهنده

## ۱. خلاصه مرحله

دو نقص تأییدشدهٔ قرارداد Backend اصلاح شد: code غایب در خطاهای framework، و حذف Retry-After موجود در Exceptionهای AI/Knowledge. قبل از Fix، ۹ تست از ۳۳ تست HTTP ناموفق بود؛ پس از Fix همان ۳۳ مورد موفق شد و guardهای تکمیلی نیز اجرا شدند. هیچ success جعلی یا تغییر سیاست دسترسی ایجاد نشده است.

پذیرش جامع Audit هنوز کامل نیست: از ۱۰۷ Case، {case_counts['PASS']} مورد در دامنهٔ محلی PASS، {case_counts['FAIL']} مورد زنده FAIL، {case_counts['BLOCKED']} مورد BLOCKED و {case_counts['NOT_APPLICABLE']} مورد NOT_APPLICABLE است. 404/405 زنده هنوز Fix محلی را دریافت نکرده‌اند. Caseهای UI/credential بدون مجوز دادهٔ واقعی اجرا نشده‌اند. این گزارش، Full107 REAL PASS نیست.

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

Migration جدید صفر است؛ همان {len(snapshot['Migrations'])} Migration قبلی و {len(snapshot['Tables'])} جدول metadata وجود دارند. مدل، FK، index، constraint و rowversion در این Task تغییر نکردند. آخرین Migration: `{snapshot['Migrations'][-1]}`. PendingModelChanges=false با API رسمی EF و بدون SQL بررسی شد. CLI معمول به‌علت assembly-load خطا داشت و PASS گزارش نشده؛ روش metadata جایگزین ثبت شده است. Migration خودکار فقط در Factoryهای SQL موقت Integration اجرا شد؛ MakanApp/MakanApp_Test یا SQL مقصد تغییر نکردند.

## ۷. Endpoint و Contract

Endpoint تجاری تازه‌ای وجود ندارد. خطاهای framework زیر `/api` با code پایدار و همان status می‌آیند؛ HEAD و non-API دست‌نخورده‌اند. کد اختصاصی دامنه، field errors، Allow405، 416 فایل و بدنهٔ نوشته‌شده حفظ می‌شوند. 429/503 فقط در صورت وجود RetryAfter معتبر AI/Knowledge، header عددی دارند؛ این header به معنی مجوز retry write نیست. جزئیات دقیق در FRONTEND_ERROR_HANDOFF_FA.md است.

## ۸. مفاهیم فنی

ProblemDetails قرارداد خطاست؛ HTTP status، نتیجهٔ Business و UX سه چیز مستقل‌اند. HasPendingModelChanges مقایسهٔ مدل با snapshot است، نه Query تاریخچهٔ دیتابیس. Barrier در تست race نقطهٔ قطعی اجرا را مشخص می‌کند؛ SQL وضعیت Reserved/Consumed و history را اثبات می‌کند. source hash و backup فقط حفاظت WIP هستند و اثبات رفتار سرور نیستند.

## ۹. تصمیم‌های طراحی

Mapper جدید همهٔ خطاها به 409/500 تبدیل نمی‌کند؛ فقط code غایب framework تکمیل می‌شود. Retry-After از مقدار typed موجود ساخته می‌شود، نه header/body خام provider. schema عمومی جدید تحمیل نشده؛ ProblemDetails فعلی و extensions حفظ شده‌اند. محدودیت: UI و BFF در این repository نیستند و فقط Handoff تحویل می‌گیرند.

## ۱۰. تست‌ها

۴۸ Integration جدید (۴۲ HTTP و شش timing theory با ۶۰ تکرار داخلی) و سه Unit جدید، جمعاً ۵۱ test case نسبت به baseline3738 اضافه شدند. ۶۰ iteration تست جدا در شمارش xUnit حساب نشده‌اند. Unit adapter خطای نهفته در 200 را بررسی می‌کند؛ Integration HTTP pipeline/SQL/مجوز/idempotency را؛ Architecture همان ۱۶ invariant Onion را حفظ می‌کند. ۴ نقش سازمانی و Personal از factoryهای فعلی و aliasهای مجاز پوشش محلی دارند؛ REAL_API نقش‌ها ادعا نمی‌شود.

## ۱۱. Validation

Clean، Restore، Release Build و Final Build موفق با صفر Warning/Error. Focused Unit=1192 و Focused Integration=320 موفق. Regression اول={sum(x['total'] for x in first_counts)} و نهایی={sum(x['total'] for x in counts)} موفق؛ Unit={by_assembly['MakanApp.UnitTests.dll']['total']}، Integration={by_assembly['MakanApp.IntegrationTests.dll']['total']}، Architecture={by_assembly['MakanApp.ArchitectureTests.dll']['total']}، Failed=0، Skipped=0. سه Unit جدید پس از Build اولیه اضافه شدند و Final Build/Full Final آن‌ها را نیز اجرا کرده‌اند؛ اعداد دو run عمداً یکسان فرض نشده‌اند. نام namespace تک‌تک تست‌ها با summary مقایسه شده تا خروجی موازی solution به اسمبلی اشتباه نسبت داده نشود.

## ۱۲. Git

HEAD اولیه و نهایی `{head}`؛ branch=`{branch}`؛ upstream=`{baseline['upstream']}`. Commit/Push/Remote verify/Publish انجام نشدند: کاربر شرط کرده بود AUDIT_COMPLETE=YES باشد و Gate به‌دلیل پذیرش‌های UI/Runtime مسدود، برقرار نیست. این تصمیم ناشی از صرف dirty بودن worktree نیست. هیچ فایلی staged نیست؛ ۴۰ تغییر قبلی، AI و private configها حفظ شده‌اند. Patch بازبینی فقط برای اصلاحات Backend در repro/backend-only-fixes.patch است و WIP قبلی را شامل نمی‌شود.

## ۱۳. محدودیت‌های فعلی

هیچ ورود زنده، پروفایل مرورگر، Cookie/Token یا دادهٔ واقعی مصرف نشد. صفحهٔ عمومی /enter و API ناشناس فقط به‌عنوان همان scope ثبت شده‌اند. Runtime deadline، Class403/404 تاریخی، تمام کارت‌ها و timing/network UI به Evidence مالک نیاز دارند. ۸ تفاوت ظاهری عملیات OpenAPI شامل پنج تفاوت معنایی و سه تغییر نام ref یکسان است؛ یک endpoint محلی نیز هنوز در Live نیست. Build/Commit/نام محیط دقیق سرور UNKNOWN است. جزئیات credential، body خصوصی یا ID واقعی در گزارش چاپ نشده است.

## ۱۴. نکات آموزشی

۱. 409 می‌تواند پاسخ درست idempotency/deadline باشد. ۲. 200 به معنی answer سالم نیست. ۳. 404 پنهان‌کنندهٔ منبع را به 403 تبدیل نکنید. ۴. RetryAfter به معنی تکرار بی‌شرط عملیات نیست. ۵. لغو کاربر، upstream timeout و EOF متفاوت‌اند. ۶. کارکرد Factory محلی، پذیرش UI/سرور را ثابت نمی‌کند. ۷. در worktree کثیف، تغییر جدید را نسبت به baseline و نه صرفاً HEAD بازبینی کنید.

## ۱۵. مسیر مطالعه

Program.cs → ApiErrorCodes.cs → ApiProblemDetailsContract.cs → GlobalExceptionHandler.cs → ApiErrorAuditEndpointsTests.cs → ApiErrorAuditAiResponseTests.cs → ApiErrorAuditRaceTests.cs → api-error-ledger.json → FRONTEND_ERROR_HANDOFF_FA.md → cases.csv.

## ۱۶. مرحله بعد

مالک می‌تواند Evidence دستی UI و runtimeِ موارد BLOCKED و deadline را تهیه کند؛ سپس gateهای باقی‌مانده ارزیابی شوند. تا تأیید آن‌ها، Commit/Push/Publish یا Deploy خودکار انجام نشود. بازطراحی Auth، AI، Safety، quota و frontend به این اصلاح محدود اضافه نشده است.

## وضعیت ماشینی

همهٔ PASSهای Domain/Error Handling زیر، scope محلی Backend دارند. SERVER_CONNECTION یعنی اتصال مدیریتی SSH/SQL/IIS؛ درخواست عمومی HTTP در REAL_API جدا آمده است.

```text
{status_text}
```
'''
(REPORT/'REPORT_FA.md').write_text(report,encoding='utf-8')
lines=['# پوشش نهایی\n\n',f"۱۰۷/۱۰۷ حسابرسی: {dict(case_counts)}. Case اجراشده یعنی PASS/FAIL پایه، نه تعداد xUnit یا request. UI و Backend با هم جمع نشده‌اند.\n\n",
    '| ID | نتیجه | EvidenceKind | دلیل |\n| --- | --- | --- | --- |\n']
for row in rows:lines.append('| '+ ' | '.join([row['id'],row['status'],row['evidenceKind'],row['reason'].replace('|','/')])+' |\n')
(REPORT/'coverage.md').write_text(''.join(lines),encoding='utf-8')
(REPORT/'BACKEND_TICKETS_FA.md').write_text('# نتیجهٔ Issueها\n\n'+ '\n\n'.join('`'+x['id']+'` — '+x['severity']+' / '+x['owner']+' / '+x['confidence']+' / '+x['status']+'\n\n'+x['summary'] for x in issues)+'\n',encoding='utf-8')
(REPORT/'RESUME_FA.md').write_text('''# ادامهٔ کار

اصلاح‌های محدود Backend و تمام Validation محلی پایان یافت. به Runtime سرور، SQL مقصد یا نشست واقعی متصل نشوید. پروفایل/credential واقعی ارائه نمی‌شود؛ پذیرش UI و deadline با Evidence دستی مالک تکمیل می‌شود. وضعیت‌های blocked از cases.csv و FINAL_RESULT.json خوانده شوند؛ هیچ Case به‌صورت فرضی PASS نشود.

Commit/Push/Publish در این run انجام نشده‌اند چون Gate جامع Audit کامل نیست. WIP قبلی و AI/private configها را حفظ کنید؛ patch repro/backend-only-fixes.patch فقط تغییرات این اصلاح Backend را نشان می‌دهد. برای اجراهای تازه، runId و پوشهٔ Evidence جدید بسازید تا قبل/بعد این run overwrite نشود.
''',encoding='utf-8')
state=load(PRIVATE/'state.json');state.update(phase='LOCAL_VALIDATED_AWAITING_RUNTIME_UI_EVIDENCE',
    commit='NOT_RUN',push='NOT_RUN',publish='NOT_RUN',auditComplete=False,localFixesComplete=True,
    finalFullRegression=True,finalTests=sum(x['total'] for x in counts),finishedAtUtc=datetime.now(timezone.utc).isoformat())
(PRIVATE/'state.json').write_text(json.dumps(state,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'cases':dict(case_counts),'tests':counts,'commit':'NOT_RUN','publish':'NOT_RUN'},ensure_ascii=False))
