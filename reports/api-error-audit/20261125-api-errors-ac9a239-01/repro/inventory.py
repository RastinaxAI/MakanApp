"""استخراج قرارداد و ledger از کد؛ این ابزار هیچ درخواست شبکه یا اتصال SQL ندارد."""
import csv
import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path

ROOT = Path.cwd()
REPORT = ROOT / 'reports/api-error-audit/20261125-api-errors-ac9a239-01'
EVIDENCE = REPORT / 'evidence'


def read(path):
    return path.read_text(encoding='utf-8-sig')


def save(name, value):
    (REPORT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


metadata_directory = EVIDENCE / ('final-contract' if (EVIDENCE/'final-contract/repository-inventory.json').exists() else 'current-contract')
snapshot = json.loads(read(metadata_directory / 'repository-inventory.json'))
local = json.loads(read(metadata_directory / 'openapi.json'))
live = json.loads(read(EVIDENCE / 'public-contract/live-openapi.sanitized.json'))
methods = {'get', 'post', 'put', 'patch', 'delete', 'head', 'options'}
operation_keys = lambda document: {(method.upper(), path) for path, item in document['paths'].items() for method in item if method in methods}


def normalized(value):
    if isinstance(value, dict):
        return {k: normalized(v) for k, v in value.items() if k not in {'example', 'examples', 'default', 'servers'}}
    if isinstance(value, list):
        return [normalized(v) for v in value]
    return value


local_keys, live_keys = operation_keys(local), operation_keys(live)
different = []
for method, path in sorted(local_keys & live_keys):
    a, b = normalized(local['paths'][path][method.lower()]), normalized(live['paths'][path][method.lower()])
    if a != b:
        different.append({'method': method, 'path': path})
schemas = local.get('components', {}).get('schemas', {})
live_schemas = live.get('components', {}).get('schemas', {})
schema_diff = [name for name in sorted(set(schemas) | set(live_schemas)) if normalized(schemas.get(name)) != normalized(live_schemas.get(name))]


def semantic(value, document, visited=()):
    if isinstance(value, dict):
        ref = value.get('$ref')
        if ref and ref.startswith('#/components/schemas/'):
            name = ref.rsplit('/', 1)[1]
            if name in visited:
                return {'recursiveSchema':True}
            target = document.get('components',{}).get('schemas',{}).get(name)
            if target is not None:
                return semantic(target, document, (*visited,name))
        return {k:semantic(v,document,visited) for k,v in value.items() if k not in {'example','examples','default','servers'}}
    if isinstance(value,list):return [semantic(v,document,visited) for v in value]
    return value


for item in different:
    path, method = item['path'], item['method'].lower()
    item['semanticDifference'] = semantic(local['paths'][path][method],local) != semantic(live['paths'][path][method],live)
operations = []
for item in snapshot['Operations']:
    path = re.sub(r':[^}/]+', '', item['Path']).split('?')[0]
    key = item['Method'], path
    detail = local.get('paths', {}).get(path, {}).get(item['Method'].lower(), {})
    operations.append(dict(item, NormalizedPath=path, ExistsLive=key in live_keys,
                           Responses=detail.get('responses', {}), RequestBody=detail.get('requestBody'),
                           Comparison='DIFFERENT' if any(x['method']==key[0] and x['path']==key[1] for x in different) else 'SAME' if key in live_keys else 'LOCAL_ONLY'))
save('api-contract-map.json', {
    'source': 'Working Tree metadata؛ این snapshot معادل Commit منتشرشده نیست.',
    'head': snapshot['SourceCommit'], 'localOperationCount': len(local_keys), 'liveOperationCount': len(live_keys),
    'localOnly': [{'method': m, 'path': p} for m, p in sorted(local_keys - live_keys)],
    'liveOnly': [{'method': m, 'path': p} for m, p in sorted(live_keys - local_keys)],
    'changedOperations': different, 'changedSchemas': schema_diff,
    'frontendContract': 'docs/frontend-contract/README.md؛ مدرک UI اجرایی نیست.',
    'liveBuild': 'UNKNOWN', 'liveCommit': 'UNKNOWN', 'liveEnvironment': 'OWNER_DESIGNATED_TEST_NOT_RUNTIME_ATTESTED',
    'operations': operations})

code_files = []
for directory in ['MakanApp.Api', 'MakanApp.Application', 'MakanApp.Domain']:
    code_files.extend(path for path in (ROOT / directory).rglob('*.cs') if not {'obj', 'bin'} & set(path.parts))
constants, declarations = {}, defaultdict(list)
for path in code_files:
    text = read(path)
    classes = list(re.finditer(r'public\s+(?:static\s+)?class\s+(\w*ErrorCodes)\b', text))
    for index, start in enumerate(classes):
        end = classes[index + 1].start() if index + 1 < len(classes) else len(text)
        for match in re.finditer(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"\r\n]+)"', text[start.end():end]):
            symbol = start[1] + '.' + match[1]
            constants[symbol] = match[2]
            declarations[match[2]].append({'symbol': symbol, 'path': path.relative_to(ROOT).as_posix(),
                                         'line': text.count('\n', 0, start.end() + match.start()) + 1})

handler = read(ROOT / 'MakanApp.Api/Errors/GlobalExceptionHandler.cs')
switch = handler.split('internal static int? GetKnownStatusCode', 1)[1].split('private static ProblemDetails CreateKnownProblem', 1)[0]
mapped = defaultdict(set)
for match in re.finditer(r'([^,{};]+?)=>\s*StatusCodes\.Status(\d{3})\w*', switch, re.S):
    selectors, status = match[1], int(match[2])
    for symbol in re.findall(r'\b\w*ErrorCodes\.\w+', selectors):
        if symbol in constants:
            mapped[constants[symbol]].add(status)
    for literal in re.findall(r'"([A-Z][A-Z0-9_]+)"', selectors):
        mapped[literal].add(status)
framework = ROOT/'MakanApp.Api/Errors/ApiProblemDetailsContract.cs'
if framework.exists():
    for match in re.finditer(r'(\d{3})\s*=>\s*(ApiErrorCodes\.\w+)', read(framework)):
        if match[2] in constants: mapped[constants[match[2]]].add(int(match[1]))
mapped['UNEXPECTED_ERROR'].add(500)
declarations['UNEXPECTED_ERROR'].append({'symbol':'unexpected-error literal', 'path':'MakanApp.Api/Errors/GlobalExceptionHandler.cs'})
explicit_map_count = sum(bool(v) for v in mapped.values())

families = {
    'Assessment': ['/api/v1/academic/assignments', '/api/v1/academic/submission-attempts', '/api/v1/academic/exams'],
    'Ai': ['/api/v1/ai'], 'Knowledge': ['/api/v1/knowledge', '/api/v1/ai'],
    'Identity': ['/api/v1/auth', '/api/v1/profile'], 'Messaging': ['/api/v1/conversations', '/api/v1/messages', '/hubs/messaging'],
    'Storage': ['/api/v1/files'], 'Academic': ['/api/v1/academic'], 'Commerce': ['/api/v1/commerce'],
    'Organization': ['/api/v1/organizations'], 'Platform': ['/api/admin/v1'],
    'Guardian': ['/api/v1/guardians'], 'Notification': ['/api/v1/notifications'],
    'Reporting': ['/api/v1/reports'], 'AddressBook': ['/api/v1/address-book'],
    'Realtime': ['/api/v1/realtime', '/hubs/messaging']}
specific_messages = {
    'SUBMISSION_DEADLINE_PASSED': 'مهلت تحویل این تکلیف به پایان رسیده است.',
    'MESSAGE_SAFETY_REVIEW_REQUIRED': 'فایل منتظر بررسی است؛ پیش‌نویس را نگه دارید.',
    'MESSAGE_SAFETY_BLOCKED': 'این فایل برای ارسال تأیید نشده است.',
    'AI_OPERATION_ALREADY_COMPLETED': 'این درخواست قبلاً تکمیل شده است؛ تاریخچه را به‌روز کنید.',
    'AI_SOURCE_PROCESSING': 'منبع هنوز در حال آماده‌سازی است.',
    'AI_SOURCE_NOT_READY': 'منبع هنوز آمادهٔ استفاده نیست.',
    'CONCURRENCY_CONFLICT': 'اطلاعات تغییر کرده است؛ نسخهٔ تازه را دریافت کنید.',
    'AUTH_REQUIRED': 'برای ادامه دوباره وارد شوید.'}
defaults = {400: 'اطلاعات درخواست را بررسی کنید.', 401: 'برای ادامه دوباره وارد شوید.',
            403: 'مجوز انجام این کار را ندارید.', 404: 'مورد درخواستی در دسترس نیست.',
            405: 'روش این درخواست مجاز نیست.', 409: 'وضعیت فعلی اجازهٔ انجام این کار را نمی‌دهد.',
            410: 'اعتبار این درخواست پایان یافته است.', 412: 'نسخهٔ تازهٔ اطلاعات را دریافت کنید.',
            413: 'اندازهٔ فایل بیش از حد مجاز است.', 415: 'نوع محتوای ارسال‌شده مجاز نیست.',
            422: 'درخواست در وضعیت فعلی قابل انجام نیست.', 429: 'کمی بعد دوباره تلاش کنید.',
            500: 'عملیات کامل نشد؛ با کد پیگیری به پشتیبانی اطلاع دهید.',
            502: 'پاسخ سرویس معتبر نبود؛ نتیجه را دوباره بررسی کنید.',
            503: 'سرویس موقتاً در دسترس نیست.', 504: 'سرویس در زمان تعیین‌شده پاسخ نداد.'}
ledger = []
for code in sorted(set(declarations) | set(mapped)):
    declaration = declarations.get(code, [])
    domain = declaration[0]['path'].split('/')[1] if declaration else 'Api'
    namespace = 'MakanApp.Application.' + domain + '.'
    candidates = sorted({'/' + '/'.join(item['Path'].strip('/').split('/')[:3])
        for item in snapshot['Operations'] if any((service or '').startswith(namespace) for service in item['Services'])})
    if declaration and declaration[0]['path'].startswith('MakanApp.Api/'):
        candidates = ['/api']
    statuses = sorted(mapped[code])
    status = statuses[0] if len(statuses) == 1 else None
    if status is None and not statuses and domain != 'Errors':
        status = 400
    # وضعیت‌های فریم‌ورک/Authentication بیرون از switch جدا ثبت می‌شوند.
    if code == 'AUTH_REQUIRED': status = 401
    if code == 'AUTH_FORBIDDEN': status = 403
    if code == 'RATE_LIMITED': status = 429
    refetch = status == 412 or any(x in code for x in ['CONFLICT', 'NOT_READY', 'PROCESSING', 'ALREADY_COMPLETED', 'DEADLINE'])
    reauth = status == 401 and code != 'AI_SERVICE_UNAUTHORIZED'
    retryable = 'CONDITIONAL' if status in {429, 502, 503, 504} else 'NO'
    action = 'REAUTHENTICATE' if reauth else 'REFETCH_PRESERVE_DRAFT' if refetch else 'WAIT_BACKOFF_RECHECK_OPERATION' if retryable == 'CONDITIONAL' else 'CORRECT_INPUT_OR_STOP'
    if code in {'AI_SERVICE_UNAUTHORIZED', 'AI_SERVICE_FORBIDDEN'}:
        retryable, reauth, action = 'NO', False, 'CONTACT_SUPPORT_NO_USER_REAUTH'
    if code in {'AI_OPERATION_IN_PROGRESS','AI_OPERATION_OUTCOME_UNKNOWN','AI_OPERATION_ALREADY_COMPLETED','AI_OPERATION_PREVIOUSLY_FAILED'}:
        retryable, action = 'NO', 'PRESERVE_INTENT_RECONCILE_AUTHORIZED_HISTORY_NO_NEW_ID'
    if code == 'MESSAGE_SAFETY_REVIEW_REQUIRED':
        action = 'KEEP_DRAFT_WAIT_EXISTING_REVIEW_NO_DELIVERY_NO_AUTO_RETRY'
    if code == 'MESSAGE_SAFETY_BLOCKED': action = 'KEEP_DRAFT_REJECTED_MEDIA_NO_AUTO_RETRY'
    if code == 'SUBMISSION_DEADLINE_PASSED': action = 'KEEP_DRAFT_REFETCH_ASSIGNMENT_AND_ATTEMPT_CONTACT_TEACHER'
    additional = [{'httpStatus':200,'endpoint':'POST /api/v1/ai/sources/preflight',
        'field':'AiSourcePreflightResult.ErrorCode','envelope':'CAPABILITY_REPORT_NOT_PROBLEM_DETAILS',
        'semantics':'Authorized=true می‌تواند همراه RetrievalAvailable=false و ErrorCode باشد؛ این پاسخ اجرای موفق AI نیست.',
        'source':'MakanApp.Application/Ai/AiSourceSelectionService.cs:PreflightAsync',
        'differenceIsIntentional':True}] if code == 'AI_SOURCE_RETRIEVAL_NOT_AVAILABLE' else []
    ledger.append(dict(code=code, domain=domain, httpStatus=status, additionalMappings=additional,
        source=declaration or [{'path': 'MakanApp.Api/Errors/GlobalExceptionHandler.cs', 'symbol': 'literal'}],
        statusSource='EXPLICIT_SWITCH' if statuses else 'AUTH_OR_RATE_PIPELINE' if code in {'AUTH_REQUIRED','AUTH_FORBIDDEN','RATE_LIMITED'} else 'KNOWN_EXCEPTION_DEFAULT_400',
        otherStatuses=statuses if len(statuses) > 1 else [], endpointFamilies=candidates,
        endpointFamilySource='CURRENT_CONTROLLER_DEPENDENCY_METADATA_CANDIDATES_NOT_PER_ENDPOINT_RUNTIME_PROOF',
        userSafeMessage=specific_messages.get(code, defaults.get(status, 'عملیات انجام نشد؛ اطلاعات درخواست را بررسی کنید.')),
        userSafeMessageSource='PROPOSED_FRONTEND_HANDOFF_NOT_REPLACEMENT_OF_SERVER_DETAIL',
        retryable=retryable, requiresRefetch=refetch, requiresReauth=reauth,
        terminal=retryable == 'NO', terminalScope='THIS_REQUEST_NOT_RESOURCE_LIFECYCLE',
        frontendAction=action, deliveryNotes='Retry خودکار نوشتن مجاز نیست؛ ابتدا نتیجهٔ OperationId و مجوز جاری بررسی شود.',
        verification='STATIC_CURRENT_CODE_NOT_ALL_CODES_RUNTIME_EXERCISED'))
ledger.append(dict(code='AI_STREAM_FAILED', domain='Ai', httpStatus=200,
    source=[{'path':'MakanApp.Api/Controllers/AiController.Streaming.cs','symbol':'catch after Response.HasStarted'}],
    statusSource='SSE_ERROR_EVENT_AFTER_HTTP_START_ONLY_NOT_PROBLEM_DETAILS', endpointFamilies=['/api/v1/ai/chat/stream'],
    userSafeMessage='جریان پاسخ کامل نشد؛ پاسخ ناقص را نهایی نکنید.',
    userSafeMessageSource='PROPOSED_FRONTEND_HANDOFF_NOT_REPLACEMENT_OF_SERVER_DETAIL', retryable='NO',
    requiresRefetch=True,requiresReauth=False,terminal=True,terminalScope='STREAM',
    frontendAction='KEEP_PARTIAL_FAILURE_RECONCILE_HISTORY_NO_NEW_INTENT',additionalMappings=[],
    verification='STATIC_SOURCE_PUBLIC_TERMINAL_FALLBACK_NOT_ALL_EXCEPTION_BRANCHES_EXERCISED'))
ledger.sort(key=lambda x:x['code'])
save('api-error-ledger.json', {'interpretation': 'status پیش‌فرض 400 فقط برای Exception شناخته‌شدهٔ رسیده به GlobalExceptionHandler است؛ mapping خارج از این مرز جداگانه است.',
    'currentErrorEnvelope': 'ProblemDetails: status, code, detail, traceId, correlationId؛ errors فقط validation.',
    'otherEnvelope':'SSE terminal error بعد از start می‌تواند HTTP200 داشته باشد؛ از ProblemDetails جداست.',
    'frontendAdviceIsProposal': True, 'count': len(ledger), 'errors': ledger})

actions = []
for operation in snapshot['Operations']:
    if operation['Controller'] != 'Ai' or operation['Method'] != 'POST':
        continue
    path = re.sub(r':[^}/]+', '', operation['Path'])
    role = ['Teacher'] if '/teachers/' in path else ['Manager'] if '/managers/' in path else ['Parent'] if '/parents/' in path else ['Student'] if any(x in path for x in ['/students/', '/homeworks/', '/exams/', '/study-plan']) else ['Personal','Student','Parent','Teacher','Manager']
    chat = path in {'/api/v1/ai/chat','/api/v1/ai/chat/stream'}
    actions.append(dict(actionId=operation['Action'], title=operation['Action'], template=None,
        templateSource='NOT_DEFINED_BY_BACKEND; UI_CATALOG_BLOCKED_AUTH', role=role, requiresFile=False,
        requiresSource='CONDITIONAL_BY_MODE_AND_SOURCE_INTENT' if chat else False,
        requiresFileMeaning='payload این endpoint فایل multipart نمی‌گیرد؛ نیاز کارت UI به انتخاب فایل قابل مشاهده نیست.',
        sourceBehavior='SourcesOnly و Auto+SourceFocused منبع لازم دارند؛ General/Auto+General با قواعد fallback موجود. در عملیات نقش‌محور، SelectedSourceIds طبق GuardAsync و preflight جاری است؛ قابلیت Retrieval از Authorized جداست.',
        endpoint=operation['Method'] + ' ' + path,
        enabledCondition='نشست معتبر + کانتکست مجاز جاری + AI فعال؛ منبع انتخابی باید قابل دسترسی و آماده باشد.',
        source=['MakanApp.Api/Controllers/AiController.cs','MakanApp.Api/Controllers/AiController.Streaming.cs', 'MakanApp.Application/Ai/AiRoleContextService.cs']))
save('action-catalog.json', {'backendActionsInventoried': True, 'uiCardsInventoried': 'BLOCKED_AUTHORIZED_FIXTURES',
    'interpretation': 'endpoint به معنی کارت UI یا template مصوب نیست؛ هیچ کارت یا prompt حدس زده نشده است.', 'actions': actions})
save('evidence/static-inventory.json', {'constants': len(constants), 'uniqueCodes': len(ledger),
    'explicitStatusMappings': explicit_map_count, 'multiStatusCodes': {k: sorted(v) for k,v in mapped.items() if len(v) > 1},
    'localOperations': len(local_keys), 'liveOperations': len(live_keys), 'changedOperations': len(different),
    'semanticChangedOperations': sum(x['semanticDifference'] for x in different),
    'intentionalCrossEnvelopeStatusDifferences':[{'code':'AI_SOURCE_RETRIEVAL_NOT_AVAILABLE','statuses':[200,409],
        'classification':'EXPECTED_CAPABILITY_REPORT_VS_INVOCATION_FAILURE_NOT_DEFECT',
        'source':'MakanApp.Application/Ai/AiSourceSelectionService.cs'}],
    'changedSchemas': schema_diff, 'backendActions': len(actions), 'migrations': len(snapshot['Migrations']),
    'latestMigration': snapshot['Migrations'][-1],
    'standards': ['https://www.rfc-editor.org/rfc/rfc9110.html#section-15', 'https://www.iana.org/assignments/http-status-codes']})
print(json.dumps({'codes': len(ledger), 'explicitMappings': explicit_map_count, 'actions': len(actions),
                  'localOnly': len(local_keys-live_keys), 'changedOperations': len(different), 'changedSchemas': len(schema_diff)}))
