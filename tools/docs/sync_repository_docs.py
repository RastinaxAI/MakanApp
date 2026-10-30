"""همگام‌سازی مرجع HTTP و مدل SQL از شواهد محلی، بدون اتصال به سرویس یا دیتابیس."""

from pathlib import Path
from collections import Counter
import argparse
import csv
import html
import hashlib
import json
import os
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
METHODS = {'get', 'post', 'put', 'patch', 'delete', 'head', 'options'}


def cell(value):
    return str(value if value is not None else '—').replace('|', '&#124;').replace('\n', ' ')


def table(headers, rows):
    return ['| ' + ' | '.join(headers) + ' |', '|' + '|'.join('---' for _ in headers) + '|'] + [
        '| ' + ' | '.join(cell(value) for value in row) + ' |' for row in rows]


def schema_label(schema):
    if not schema:
        return 'بدون schema منتشرشده'
    if '$ref' in schema:
        name = schema['$ref'].rsplit('/', 1)[-1]
        return f'[{name}](#contract-{name})'
    if 'items' in schema:
        return 'آرایه از ' + schema_label(schema['items'])
    for keyword in ('allOf', 'oneOf', 'anyOf'):
        if keyword in schema:
            return keyword + ': ' + ' / '.join(schema_label(item) for item in schema[keyword])
    result = schema.get('type', 'object')
    if schema.get('format'):
        result += ' / ' + schema['format']
    if schema.get('enum'):
        result += ' / ' + ', '.join(map(str, schema['enum']))
    if schema.get('additionalProperties'):
        result += ' / مقادیر دیکشنری: ' + schema_label(schema['additionalProperties'] if isinstance(schema['additionalProperties'], dict) else {})
    return result


def media(content):
    return '<br>'.join(f'`{kind}`: {schema_label(value.get("schema", {}))}' for kind, value in content.items()) or 'بدون بدنهٔ منتشرشده'


def canonical(path):
    return re.sub(r'\{([^}:]+):[^}]+\}', r'{\1}', path).rstrip('/')


def enum_contract_mismatches(api, inventory):
    by_name = {item['Name']: item for item in inventory['Enums']}
    return sorted(name for name, schema in api['components']['schemas'].items()
                  if name in by_name and schema.get('type') == 'integer'
                  and any(isinstance(value, str) for value in by_name[name]['Values']))


def enum_contract_notice(api, inventory):
    mismatches = enum_contract_mismatches(api, inventory)
    if mismatches:
        return ('DOC-CONTRACT-01: اختلاف Enum/OpenAPI هنوز در این snapshot وجود دارد: ' +
                '، '.join(mismatches) + '؛ literal مرجع از JsonOptions واقعی MVC استخراج می‌شود. client از نوع خام عددی، قرارداد canonical عددی استنتاج نکند.')
    return ('DOC-CONTRACT-01 در نسخهٔ مبنا CLOSED_IN_CODE_AND_LOCAL_CONTRACT_TESTS است؛ اختلاف integer/String قبلی در snapshot جاری دیده نشد. '
            'OpenAPI با serializer رشته‌ای MVC هم‌تراز شده است؛ literalهای زیر از همان CLR/JsonOptions استخراج می‌شوند. '
            'این نتیجه شاهد انتشار OpenAPI روی سرور نیست؛ named Flags همهٔ ترکیب‌های مجاز یا ورودی‌های مجاز use case را تعریف نمی‌کند.')


def review_notice(source, notice):
    source = re.sub(r'\n<!-- DOC-SYNC:BEGIN -->.*?<!-- DOC-SYNC:END -->\n', '', source, flags=re.S)
    title, separator, body = source.partition('\n')
    if not separator:
        raise ValueError('سند بدون عنوان/بدنه باید دستی بازبینی شود.')
    return title + '\n\n' + notice.strip() + '\n\n' + body.lstrip('\r\n')


def write_reference(api, inventory, baseline, date):
    metadata = {(item['Method'].lower(), canonical(item['Path'])): item for item in inventory['Operations']}
    operations = [(path, method, value) for path, item in api['paths'].items() for method, value in item.items() if method in METHODS]
    assert len(metadata) == len(operations) == len(inventory['Operations']), 'شمار عملیات OpenAPI و ApiExplorer متفاوت است.'
    lines = [
        '# مرجع جاری Endpointها و قراردادهای API ماکان', '',
        f'بازبینی `{date}` با مبنای Git `main@{baseline}` و metadata فایل‌های working tree؛ تولید از OpenAPI میزبان Testing و ApiExplorer، بدون SQL connection، worker، SMS یا AI واقعی.', '',
        f'پوشش: **{len(operations)} عملیات HTTP، {len(api["paths"])} مسیر متمایز، {len(set(item["Controller"] for item in metadata.values()))} Controller و {len(api["components"]["schemas"])} schema**. Health، Swagger و Hub در این شمارش نیستند.', '',
        'این مرجع جایگزین کاتالوگ قدیمی ۱۴۱ عملیات است. نام فیلد، nullable، request/response و enum از سند واقعی runtime آمده‌اند؛ لازم‌بودن کسب‌وکاری، authorization منبع و خطاهای lifecycle علاوه بر schema در Application/Domain بررسی می‌شوند. هیچ نمونه یا تست زنده‌ای از روی وجود schema موفق فرض نمی‌شود.', '',
        '## قرارداد مشترک و مرزهای امنیتی', '',
        '- ورود از Bearer session موجود است، نه JWT فرضی یا Role فرستاده‌شده توسط client. وضعیت anonymous هر عملیات از metadata خودش استخراج شده است.',
        '- policy درج‌شده، gate حمل‌ونقل است؛ OrganizationId، ChildId، UserId و resource ID فقط selector هستند. Application رابطه، نقش فعال و context جاری را دوباره resolve می‌کند.',
        '- GuardianRelation مجوز خواندن Direct خصوصی کودک نیست؛ School Manager نیز private chat browsing ندارد. Minor و Unknown در عملیات حساس Messenger محافظت می‌شوند؛ UserId به‌تنهایی مجوز ارتباط نیست.',
        '- مسیرهای `/api/admin/v1` از نقش Manager مدرسه جدا هستند؛ Platform OTP/Session و permission جاری دارد؛ عامل دوم حذف شده است.',
        '- POST را کور retry نکنید؛ شناسهٔ idempotency، payload و rowversion همان use case باید رعایت شوند. هدر عمومی Idempotency-Key وجود ندارد.',
        '- statusهای جدول فقط پاسخ‌های منتشرشده در OpenAPI هستند؛ خطای domain و gateهای pipeline می‌توانند پاسخ‌های بیشتری داشته باشند.',
        '- زمان سرور و UTC معتبر است؛ concurrency token دودویی در JSON به شکل Base64 است. enum رشته‌ای است و باید unknown آینده تحمل شود.', '',
        '[راهنمای جریان‌ها](backend-api-guide.md)، [قرارداد Backoffice](platform/BACKOFFICE_FRONTEND_HANDOFF.md)، [سیاست Child Safety](architecture/GAP_05_MESSENGER_CHILD_SAFETY.md)، [استقرار](production/DEPLOYMENT_IIS.md).', '',
        '## رفتار جاری فراتر از schema',
        '',
        'قرارداد OTP یکسان است ولی رفتار از Otp:Enabled/Otp:AllowMockAuthentication می‌آید: Production/Staging فقط true/false و Mock فقط در Development/Test/Testing با false/true. در Mock challenge واقعی ساخته نمی‌شود و مالکیت شماره ثابت نمی‌شود؛ نشست و مجوز منابع همچنان واقعی‌اند. راهنمای OTP و نمونه ورود را در [راهنمای بک‌اند](backend-api-guide.md#identity) بخوانید.',
        '',
        'عملیات مدیریتی health/jobs/notifications همان routeها را دارند؛ PlatformJobOperations.Queues جزئیات صف را اضافه می‌کند و PlatformNotificationOperations شمارش/سن و failureهای اخیر را می‌دهد. FileCleanup صرفاً وضعیت Enabled/Disabled تنظیمی است. HTTP 200 عملیات مدیریتی به‌تنهایی به معنی سالم‌بودن همه dependencyها نیست.',
        '',
        'AI v1 پاسخ را پیش از خروج از Backend به‌صورت strict و grounded بررسی می‌کند؛ 503 از Provider از جمله AI_MODEL_UNAVAILABLE به AI_SERVICE_UNAVAILABLE نگاشت می‌شود، body خام عبور نمی‌کند و retry خودکار POST وجود ندارد. Service Auth واقعیِ قبلی، Parent Provider Timeout و Full-12 Deferred در [وضعیت AI](ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md) تفکیک شده‌اند.',
        '',
        'پروفایل self دارای DOB nullable و ProfilePhoto است؛ Directory/roster/Guardian consent و Class Lesson به مدرسه اضافه شده\u200cاند. request/response دقیق هر operation پایین از metadata فعلی آمده؛ سیاست\u200cهای date-only/null، عکس Bearer، visibility Draft و رضایت والد در [راهنما](backend-api-guide.md) و [قرارداد مدرسه](product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md) توضیح داده شده\u200cاند.', '',
        'اشتراک Individual/Organization و بنیاد PersonalClass اکنون موجودند؛ خرید نقش نمی‌سازد، Legacy قبل از قرارداد معتبر Admin قفل تازه نمی‌گیرد و AI Runtime فعال نیست. [قرارداد کاربر](product/SUBSCRIPTION_FRONTEND_HANDOFF_FA.md) و [Admin](product/SUBSCRIPTION_ADMIN_HANDOFF_FA.md).', '',
        'Replay حساب در GET /api/v1/realtime/changes با bootstrap/cursor و ClientInvalidated افزوده شده است؛ Snapshot و مجوز read جاری مرجع‌اند. [قرارداد Event](product/REALTIME_EVENT_CONTRACT_FA.md).', '',
        'پیوست پاسخ آزمون از policy نسخهٔ سؤال، revision و write lease استفاده می‌کند؛ Ready مجوز Safety یا دانلود عمومی نیست. [قرارداد پیوست](product/ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md#frontend_handoff) و [ReviewRequired پیام‌رسان](messaging/BE01_REVIEW_REQUIRED_CONTRACT_FA.md).', '',
        '## فهرست Controllerها', ''
    ]
    counts = Counter(item['Controller'] for item in metadata.values())
    lines += table(['Controller', 'تعداد عملیات', 'سورس'], [(name, count, f'[{name}Controller](../MakanApp.Api/Controllers/{name}Controller.cs)') for name, count in sorted(counts.items())])
    for controller in sorted(counts):
        lines += ['', f'## {controller}', '']
        selected = [(path, method, operation, metadata[(method, canonical(path))]) for path, method, operation in operations if metadata[(method, canonical(path))]['Controller'] == controller]
        for path, method, operation, item in sorted(selected):
            lines += [f'### {method.upper()} {path}', '', f'کد واقعی: [{controller}Controller.{item["Action"]}](../MakanApp.Api/Controllers/{controller}Controller.cs).', '']
            auth = 'عمومی از نظر ورود؛ validation، محدودیت درخواست و قواعد اختصاصی همچنان برقرارند.' if item['Anonymous'] else 'نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.'
            lines += ['احراز هویت: ' + auth, '']
            if item['Policies']:
                lines += ['policy: ' + '، '.join(f'`{value}`' for value in item['Policies']), '']
            services = item.get('Services', [])
            if services:
                lines += ['وابستگی‌های واقعی Controller: ' + '، '.join(f'`{name}`' for name in services), '']
            parameters = operation.get('parameters', [])
            if parameters:
                lines += table(['پارامتر', 'محل', 'الزام در schema', 'نوع'], [(f'`{p["name"]}`', p.get('in'), 'بله' if p.get('required') else 'خیر', schema_label(p.get('schema', {}))) for p in parameters]) + ['']
            request = operation.get('requestBody')
            lines += ['بدنه درخواست: ' + (media(request.get('content', {})) + ('؛ required' if request.get('required') else '؛ optional در schema') if request else 'ندارد.'), '']
            lines += table(['HTTP status', 'معنا', 'بدنه'], [(status, value.get('description', ''), media(value.get('content', {}))) for status, value in operation.get('responses', {}).items()]) + ['']
    enum_groups = {}
    for item in inventory['Enums']:
        enum_groups.setdefault(item['Name'], []).append(item)
    assert not any(len(items) != 1 and name in api['components']['schemas'] for name, items in enum_groups.items()), 'نام Enum مبهم است؛ mapping صریح لازم است.'
    wire_enums = {name: items[0] for name, items in enum_groups.items() if len(items) == 1}
    lines += ['## قراردادهای JSON و Enum', '', 'Required زیر همان required در OpenAPI است؛ اختیاری بودن schema به معنی عبور از قواعد Application نیست. bounds فقط در صورت انتشار نشان داده می‌شوند.', '',
              enum_contract_notice(api, inventory), '']
    for name, schema in sorted(api['components']['schemas'].items()):
        lines += [f'<a id="contract-{html.escape(name, quote=True)}"></a>', f'### {name}', '']
        if name in wire_enums:
            item = wire_enums[name]
            lines += ['CLR واقعی: `' + item['ClrType'] + '`؛ Flags: ' + str(item['IsFlags']), '',
                      'literalهای JSON واقعی MVC: `' + json.dumps(item['Values'], ensure_ascii=False) + '`', '',
                      'نوع خام OpenAPI: `' + schema.get('type', 'object') + '`؛ اختلاف integer/String: ' + ('وجود دارد؛ هشدار DOC-CONTRACT-01' if name in enum_contract_mismatches(api, inventory) else 'در این snapshot دیده نشد') + '؛ قید use case و Flags جدا بررسی شود.', '']
        else:
            lines += [schema_label(schema), '']
        if schema.get('properties'):
            required = set(schema.get('required', []))
            rows = []
            for field, value in schema['properties'].items():
                constraints = {key: value[key] for key in ('minLength', 'maxLength', 'minimum', 'maximum', 'pattern', 'readOnly', 'writeOnly', 'default') if key in value}
                rows.append((f'`{field}`', schema_label(value), 'بله' if field in required else 'خیر', 'بله' if value.get('nullable') else 'خیر/تصریح نشده', json.dumps(constraints, ensure_ascii=False) if constraints else '—'))
            lines += table(['فیلد', 'نوع', 'required', 'nullable', 'قید منتشرشده'], rows) + ['']
        if any(key in schema for key in ('allOf', 'oneOf', 'anyOf')):
            lines += ['ساختار ترکیبی:', '```json', json.dumps(schema, ensure_ascii=False, indent=2), '```', '']
    lines += ['## کدهای خطای تعریف‌شده', '', 'این فهرست از فایل‌های ErrorCodes استخراج شده؛ تعریف یک literal به‌تنهایی اثبات امکان وقوع عمومی آن نیست. status فقط در صورت نگاشت صریح در GlobalExceptionHandler نشان داده شده است.', '']
    handler = (ROOT / 'MakanApp.Api/Errors/GlobalExceptionHandler.cs').read_text(encoding='utf-8-sig')
    statuses = dict(re.findall(r'(\w+ErrorCodes\.\w+)\s*=>\s*StatusCodes\.Status(\d+)', handler))
    rows = []
    for directory in ('MakanApp.Application', 'MakanApp.Domain', 'MakanApp.Api'):
        for path in sorted((ROOT / directory).rglob('*ErrorCodes.cs')):
            if {'bin', 'obj'} & set(path.parts):
                continue
            for member, code in re.findall(r'public const string (\w+) = "([^"]+)";', path.read_text(encoding='utf-8-sig')):
                relative = path.relative_to(ROOT).as_posix()
                rows.append((f'`{code}`', statuses.get(path.stem + '.' + member, 'طبق handler/مسیر فراخوانی'), f'[{path.stem}](../{relative})'))
    lines += table(['code', 'HTTP صریح', 'منبع'], sorted(rows))
    (ROOT / 'docs/backend-api-reference.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')


def write_database(inventory, baseline, date):
    tables = inventory['Tables']
    counts = {key: sum(len(item[key]) for item in tables) for key in ('Columns', 'ForeignKeys', 'Indexes', 'Checks')}
    lines = ['# ساختار فعلی دیتابیس MakanApp', '', f'مدل طراحی EF Core از working tree با مبنای Git `main@{baseline}`؛ بازبینی `{date}`. این سند metadata سورس است، نه گزارش اتصال یا تأیید schema یک سرور.', '',
             f'**{len(tables)} جدول، {counts["Columns"]} ستون، {counts["ForeignKeys"]} FK، {counts["Indexes"]} index مستقل از key و {counts["Checks"]} CHECK؛ {len(inventory["Migrations"])} Migration.**', '',
             '## مفاهیم و مرزها', '',
             'مدل جاری شامل چهار migration پس از Checklist است: RemovePlatformMfa دو جدول MFA و ستون‌های AssuranceLevel/StepUpAtUtc نشست را حذف می‌کند؛ AddSelfDeclaredProfileDateOfBirth ستون date nullable به Persons، AddUserProfilePhoto مرجع nullable و unique filtered به Users، و CompleteSchoolManagement جدول‌های ClassLessons/ClassLessonAttachments و توسعه Invitation والد را می‌افزاید. آخرین migration 20261028202239_CompleteSchoolManagement است. اجرا روی دیتابیس عادی/سرور مرحلهٔ مستقل است؛ Down اطلاعات حذف‌شدهٔ MFA را بازنمی‌گرداند.',
             '',
             'cleanup terminal فقط bytes مجاز بدون reference را با opt-in حذف می‌کند؛ مرجع فعال ProfilePhoto و Lesson attachment هم محافظت می‌شوند. metadata SQL پاک نمی‌شود و Ready-orphan reaper وجود ندارد. [سیاست retention](operations/RETENTION_AND_CLEANUP_POLICY.md).',
             '',
             '- SQL Server پایگاه اصلی و MakanDbContext مرجع مدل است؛ schema مرز نام‌گذاری ماژول است، نه مجوز کاربر.',
             '- PK همهٔ جدول‌ها الزاماً Id نیست؛ کلید واقعی هر جدول پایین آمده است. Alternate Key با index یکتا فرق دارد.',
             '- rowversion توکن هم‌زمانی SQL است، نه تاریخ؛ nullable، PK/FK، unique/filtered index و CHECK از خود مدل طراحی خوانده شده‌اند.',
             '- FK مرکب علاوه بر وجود ردیف، سازگاری scope را کنترل می‌کند؛ مجوز actor همچنان در Application لازم است.',
             '- ویژگی IsConcurrencyToken نشان‌دهندهٔ کنترل optimistic concurrency است. تغییرناپذیری نسخه منتشرشده و سقف نمره قواعد Domain/Application هم دارند.',
             '- این ابزار فقط Model/GetMigrations را می‌خواند؛ EnsureCreated، Migrate، اتصال SQL، خواندن داده یا secret ندارد.', '',
             '[راهنمای معماری و جریان داده](backend-api-guide.md)، [SQL Server Override](project-context/01_SQLSERVER_ARCHITECTURE_OVERRIDE.md)، [Migration تحویلی](production/DEPLOYMENT_IIS.md).', '', '## نقشه schemaها', '']
    lines += table(['Schema', 'جدول'], sorted(Counter(item['Schema'] for item in tables).items()))
    lines += ['', '## Migrationهای نسخه‌بندی‌شده', '']
    lines += table(['ترتیب', 'Migration', 'سورس'], [(i, name, f'[کد](../MakanApp.Infrastructure/Persistence/Migrations/{name}.cs)') for i, name in enumerate(inventory['Migrations'], 1)])
    lines += ['', 'آخرین شناسه باید در history محیط هدف جدا بررسی شود؛ برنامه migration را خودکار اجرا نمی‌کند. فایل SQL اصلاح‌شدهٔ idempotent فقط پس از Backup و بازبینی اپراتور اجرا می‌شود.', '', '## فرهنگ کامل جدول‌ها', '']
    for item in tables:
        lines += [f'### {item["Schema"]}.{item["Table"]}', '', f'Entity: `{item["Entity"]}`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).', '']
        lines += table(['ستون', 'SQL type', 'NULL', 'Concurrency', 'تولید مقدار / SQL default'], [(c['Name'], f'`{c["Type"]}`', 'بله' if c['IsNullable'] else 'خیر', 'بله' if c['IsConcurrencyToken'] else 'خیر', c['ValueGenerated'] + (' / ' + c['DefaultSql'] if c['DefaultSql'] else '')) for c in item['Columns']])
        lines += ['', 'کلیدها:', ''] + table(['نام', 'نوع', 'ستون‌ها'], [(key['Name'], 'PK' if key['Primary'] else 'Alternate Key', ', '.join(key['Columns'])) for key in item['Keys']])
        if item['Indexes']:
            lines += ['', 'Indexها:', ''] + table(['نام', 'ستون‌ها', 'Unique', 'Filter'], [(index['Name'], ', '.join(index['Columns']), index['IsUnique'], index['Filter']) for index in item['Indexes']])
        if item['ForeignKeys']:
            lines += ['', 'روابط:', ''] + table(['ستون‌ها', 'مقصد', 'کلید مقصد', 'DeleteBehavior'], [(', '.join(fk['Columns']), fk['Target'], ', '.join(fk['PrincipalColumns']), fk['Delete']) for fk in item['ForeignKeys']])
        if item['Checks']:
            lines += ['', 'CHECKها:', ''] + table(['نام', 'SQL'], [(check['Name'], f'`{check["Sql"]}`') for check in item['Checks']])
        lines += ['']
    (ROOT / 'docs/database-current-structure.md').write_text('\n'.join(lines).rstrip() + '\n', encoding='utf-8')


def write_frontend(inventory, api, baseline, date):
    lines = ['# قرارداد جاری مصرف Backend برای Web و Mobile', '',
             f'بازبینی `{date}` با مبنای Git `main@{baseline}` و metadata فایل‌های working tree؛ این سند در مخزن Backend است و مجوز ساخت، clone یا تغییر frontend نیست.', '',
             f'پوشش جاری: {len(inventory["Operations"])} عملیات HTTP در {len(api["paths"])} مسیر، {len({item["Controller"] for item in inventory["Operations"]})} Controller، {len(api["components"]["schemas"])} schema، {len(inventory["Tables"])} جدول و {len(inventory["Migrations"])} Migration. فهرست کامل کارت‌های HTTP و DTOها در HTML/PDF همین تحویل ضمیمه شده است.', '',
             '- [HTML مستقل](MakanApp-Frontend-Backend-Contract.html) و [PDF](MakanApp-Frontend-Backend-Contract.pdf): قرارداد جاری و کاتالوگ کامل API.',
             '- [مرجع Markdown قراردادها](../backend-api-reference.md)، [راهنمای جریان کد](../backend-api-guide.md)، [ساختار دیتابیس](../database-current-structure.md).',
             '- [UX مرجع اصلی](../project-context/03_Makan_User_Flows_and_Screens_UX_v1.0.pdf) و [نقشه Flow](../project-context/04_Flow.png) نیازمندی‌اند، نه اثبات وجود screen یا frontend در این workspace. اطلاعات snapshot قدیمی ۱۴۱ عملیات/۲۰ Migration وضعیت جاری نیست.', '',
             '## قرارداد مشترک انتقال و مجوز', '',
             '- API اصلی /api/v1 و Backoffice مستقل /api/admin/v1 است. شناسهٔ GUID، enum رشته‌ای، زمان UTC و rowversion/Base64 را از response واقعی بگیرید؛ placeholderهای سند دادهٔ آمادهٔ محیط نیستند.',
             '- Bearer در Authorization header ارسال می‌شود؛ cookie session، query-token برای SignalR و هدر عمومی Idempotency-Key نداریم. public بودن OTP/refresh مجوز دسترسی به resource نیست.',
             '- role، organization، membership و child جاری را سرور resolve می‌کند؛ selectorهای client مدرک مجوز نیستند. 401 ورود، 403 منع مجوز، 404 پنهان‌سازی/نبود منبع، 409/412 تضاد و 429 محدودیت باید از ProblemDetails/code خوانده شوند.',
             '- تمام listها یک شکل paging ندارند؛ پارامتر، بدنه، status و schema دقیق هر عملیات در مرجع جاری است. validation کسب‌وکاری فراتر از nullable/required در OpenAPI اجرا می‌شود.', '',
             enum_contract_notice(api, inventory), '',
             '## ورود، نشست و OTP', '',
             'Real OTP: با Otp:Enabled=true و Otp:AllowMockAuthentication=false، POST /api/v1/auth/otp/challenges پاسخ challengeId/expiresAtUtc/resendAvailableAtUtc می‌دهد؛ verify همان شناسه، شماره و کد رشته‌ای را می‌گیرد. کد در پاسخ/log نیست و طول شش‌رقمی ثابت فرض نشود. lifetime پایه ۳۰۰ ثانیه و resend سی ثانیه است؛ ارقام فارسی/عربی normalize، challenge قبلی supersede و مصرف مجدد رد می‌شود.',
             '',
             'Mock: فقط در Development/Test/Testing با جفت false/true مجاز است؛ Production/Staging در startup آن را رد می‌کنند. شکل request/response ثابت است ولی challenge واقعی در SQL ساخته نمی‌شود و verify پس از اعتبارسنجی فرمت شماره/کد، مقدار OTP یا challenge را اثبات نمی‌کند. کد فعلی ۴ تا ۶۴ کاراکتر بدون control character می‌پذیرد. این رفتار مالکیت شماره را اثبات نمی‌کند؛ UI حق تشخیص Mock از شکل پاسخ یا ساخت bypass عمومی ندارد.',
             '',
             'در Mock نیز حساب معلق رد می‌شود و نشست، workspace، عضویت و role عادی‌اند؛ شمارهٔ جدید می‌تواند مطابق مسیر قبلی User بسازد، اما نقش آموزشی یا Platform ساخته نمی‌شود. ورود دوباره session جدید می‌سازد؛ verify در Mock را idempotent فرض نکنید. تغییر flag نشست‌های قبلی را revoke نمی‌کند. انتخاب محیط و سیاست داده وظیفه deployment است؛ [راهنمای OTP](../dev/CONFIGURABLE_OTP_FA.md) و [Test محدود](../dev/SERVER_MOCK_OTP_FA.md).',
             '',
             'VerifyOtpResult شامل accessToken، tokenType، expiresAtUtc، refreshToken، refreshExpiresAtUtc و user است. access پایه یک ساعت و refresh چرخشی با عمر مطلق ۳۰ روز است؛ refresh token و OTP دادهٔ محرمانه‌اند. POST /api/v1/auth/refresh و APIهای sessions/logout برای lifecycle موجودند. storage امن سمت client تصمیم اجرایی جدا می‌خواهد؛ credential در URL، screenshot یا telemetry قرار نگیرد.', '',
             '## پروفایل شخصی و عکس',
             '',
             'PATCH /api/v1/me/profile همان نام‌ها و Username را با dateOfBirth optional به شکل YYYY-MM-DD میلادی می‌گیرد؛ آینده400 PROFILE_VALIDATION_FAILED، omission/null حفظ مقدار قبلی و clear تعریف نشده. DOB شرط تکمیل یا تأیید سن نیست؛ People/Directory/AI DOB تازه نمی‌گیرند. DateOfBirth خوداظهاری با VerifiedBirthDate مستقل فرق دارد.',
             '',
             'عکس: upload موجود در Personal -> Ready JPEG/PNG خود کاربر با OrganizationId=null -> PUT /api/v1/me/profile-photo {fileAssetId}. DELETE همان مسیر204 و تکرارپذیر است؛ فقط مرجع پاک می‌شود. profilePhoto در پاسخ self و هویت امن Messenger nullable است. downloadUrl عمومی نیست و دریافت Bearer لازم دارد؛ client token را در URL نگذارد. نمایش عکس تابع grant همان هویت است و مجوز Send/Discovery تازه نیست. جایگزینی عکس blob قبلی را حذف نمی‌کند. [قرارداد عکس](../product/PROFILE_PHOTO_IMPLEMENTATION_FA.md).',
             '',
             '## سازمان، پرونده و فرزند',
             '',
             'Manager مدرسه می‌تواند از /api/v1/organization/people پرونده بسازد/بخواند/جست‌وجو یا تغییر مجاز نام دهد؛ نام Person حساب‌دار یا مشترک از این API تغییر نمی‌کند. دعوت اتصال حساب نیازمند User موجود و پذیرش صریح authenticated است؛ merge خودکار بر اساس نام/تلفن نداریم. پرونده، User، Membership، RoleAssignment و Enrollment مفاهیم جدا هستند.', '',
             'Manager می‌تواند با POST /organization/people/{learnerId}/guardian-invitations و دقیقاً یکی از username/phoneNumber حساب موجود، دعوت والد بسازد؛ reason، clientOperationId و expiry آینده تا۳۰روز لازم‌اند. پاسخ مدیر هویت مقصد را افشا نمی‌کند. Parent در inbox خود با accept و confirmGuardianRelation=true رضایت می‌دهد؛ Membership/Parent Role و GuardianRelation در workflow موجود ایجاد/reuse می‌شوند و Enrollment یا Account جدید ساخته نمی‌شود. تکرار دقیق receipt و payload متفاوت409 است؛ رابطهٔ پایان‌یافته با replay زنده نمی‌شود. مسیرهای قدیمی GuardianRelation نیز حفظ‌اند.',
             '',
             'Parent از children/select موجود استفاده می‌کند و revoke رابطه، cache قبلی را مجاز نمی‌کند. GuardianRelation مجوز private chat یا همه فایل‌های کودک نیست. GET /organization/members برای Manager و GET کلاس learners/teachers برای Manager/Teacher منتسب، Directory و roster جاری می‌دهند؛ orgId از workspace resolve می‌شود. [قرارداد کامل مدرسه](../product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md).',
             '',
             '## آموزش، تکلیف و آزمون', '',
             'AcademicPeriod/Course/Class/Enrollment/TeacherAssignment و Session/Schedule/Attendance موجودند. ساخت Class فقط Manager و ساخت Session برای Teacher منتسب است؛ نبود حضور را Absent نمایش ندهید. roster صفحه‌بندی‌شده با list آرایه‌ای قبلی classes یکسان نیست و version جدید به معنی پارامتر ExpectedVersion تازه در End قدیمی نیست. Transfer مستقل اتمیک نداریم.',
             '',
             'Class Lesson/Handout: GET/POST classes/{id}/lessons، GET/PATCH lessons/{id} و POST lessons/{id}/publish زیر /academic. Manager/Teacher منتسب می‌نویسند؛ Student دارای Enrollment فعال فقط Published می‌بیند و Draft برای او404/پنهان است؛ Parent مجوز مستقل ندارد. PATCH کل title/body/displayOrder/fileAssetIds/expectedVersion را جایگزین می‌کند، [] اتصال فایل را حذف می‌کند نه blob؛ Published immutable است. body متن ساده است و publish حداقل متن یا فایل می‌خواهد. فایل Ready سازمانی مجاز و دانلود با grant جاری کلاس است؛ URL قدیمی پایان Assignment/Enrollment را دور نمی‌زند. این Lesson، AI Source یا Grade نیست.',
             '',
             'AssignmentVersion سقف MaxScore مستقل و مثبت دارد؛ score در بازهٔ 0..MaxScore است و سقف سراسری ۲۰ نداریم. نسخهٔ منتشرشده، submission receipt، evaluation revision و GradeRelease معنای تاریخی را حفظ می‌کنند؛ Student/Parent نمرهٔ منتشرنشده و note خصوصی نمی‌بینند.', '',
             'ExamVersion/QuestionVersion، شروع Attempt، محدودیت تلاش، deadline سرور، question freeze، AnswerRevision/autosave، WriteLease، finalize receipt و grading/release/correction موجودند. قطع شبکه submit موفق ساختگی نیست؛ receipt و read جاری مرجع‌اند. clock دستگاه، autosave acknowledgement یا state محلی جای زمان سرور و finalization رسمی نیست. auto-finalize/background grace به‌عنوان قابلیت جاری ادعا نمی‌شود.', '',
             '## Messenger و ایمنی کودک', '',
             'Personal Direct/Group/Channel و Contacts/privacy بدون School Role مجاز با gateهای حساب/سن/رابطه/profile/block موجودند؛ پایان نقش، User/عکس/گفتگوی شخصی را حذف نمی‌کند. برای Profile ناقص messaging-status فقط ReadExistingConversations و ProfileCompletionRequired دارد؛ hint مجوز create/discovery نیست. پس از پایان role، انتخاب Personal از endpoint workspace موجود انجام می‌شود؛ fallback پنهان نداریم.',
             '',
             'Policy مصوب GAP-05 حاکم است: UnknownAge مانند Minor محافظت می‌شود؛ Stranger -> Minor/Unknown ممنوع است؛ هر Send، Block و رابطه/عضویت/permission جاری را دوباره بررسی می‌کند. پایان رابطه تاریخچه را خودکار حذف نمی‌کند. گروه/کانال شخصی و Official Class Group قواعد جدا دارند؛ نقش Manager یا Guardian مجوز Direct browsing نیست.', '',
             'Internal Makan Content Safety provider واقعی قاعده‌محور محدود متن است، نه AI یا ادعای اسکن معنایی کامل. unavailable در مسیر محافظت‌شده fail-closed و رسانه unsupported ReviewRequired است؛ خطای safety را Sent نمایش ندهید. ارزیابی سنِ خوداظهاری تأیید رسمی نیست و AGE_VERIFICATION_CONFLICT با retry کور overwrite نمی‌شود.', '',
             'SignalR تغییر پایدار را اعلان می‌کند؛ SQL/Delta مرجع پیام و read-state هستند. TypingChanged و PresenceChanged hint گذرا با TTL و privacy جاری‌اند، نه Message/Delta. handler فقط Bearer header می‌خواند؛ browser از LongPolling و withCredentials:false طبق allowlist میزبان استفاده کند. reconnect نیازمند ثبت دوباره subscriptionهای لازم و replay حساب از cursor ذخیره‌شده است؛ Delta گفتگو نیز مستقل حفظ شده است. backplane و ephemeral state مشترک چند instance فراهم نیست.', '',
             '## گزارش، اعلان و تجارت', '',
             'چهار داشبورد نقش‌محور، گزارش‌های filtered/paginated و export خصوصی CSV با Queued/Processing/Completed/Failed وجود دارند. مجوز ایجاد، worker و download دوباره بررسی می‌شود؛ job pending را فایل آماده نمایش ندهید.', '',
             'اعلان داخلی، outbox، preferences و device lifecycle موجودند. Web Push/VAPID و FCM HTTP v1 با Notifications:PushProvider=WebPushAndFcm پیاده شده‌اند؛ ثبت device، dispatch یا پذیرش provider به معنی رسیدن به دستگاه نیست. P1_PUSH_RUNTIME_PENDING برقرار است؛ SMS عمومی اعلان همچنان unavailable و از OTP ملی‌پیامک جداست.', '',
             'Plan/PlanVersion/Subscription/Entitlement/Quota و Order/PaymentAttempt/verification lifecycle موجودند؛ gateway واقعی unavailable است. callback یا UI هرگز پرداخت را بدون تأیید server موفق اعلام نمی‌کند؛ refund/reconcile دستی ساختگی نسازید.', '',
             '## اشتراک Individual/Organization و PersonalClass',
             '',
             'GET /commerce/catalog با audience، GET /commerce/personal/subscription و organization/subscription و Quote/Order هر مالک را مصرف کنید. Manager فقط سازمان جاری و Platform فقط با permission مرکزی مجاز است؛ خرید نقش یا دادهٔ آموزشی را مجاز نمی‌کند. Order تازه201/replay200، قیمت نمایشی تومان و مبنای فعلی درگاه IRR است؛ gateway واقعی unavailable است. Quote حق ایجاد نمی‌کند.',
             '',
             'دوره ماه تقویمی با لنگر ثابت، تمدید دستی و AutoRenew=false است؛ Upgrade بعد از پرداخت معتبر سررسید را ثابت و Downgrade دورهٔ بعد را هدف می‌گیرد. LegacyPendingContract/AwaitingValidContract به معنی Unlimited نیست؛ کنترل سازمان فقط با ثبت معتبر Admin شروع می‌شود. Grace سازمان هفت روز و سپس write آموزشی مشمول قفل است؛ خواندن مجاز، پیام‌رسان شخصی و پرداخت حفظ می‌شوند.',
             '',
             'Quota را با Key/Kind/Unit/Limit/Consumed/Reserved/Remaining و IsUsageAvailable بخوانید. Limit حذف‌شده با صفر و null متفاوت است؛ meter ناموجود را مصرف صفر نمایش ندهید. CurrentCapacity ماهانه reset نمی‌شود. Preflight متن ساده AI فقط بررسی است؛ Trial سه موفقیت روزانه Account با مرز تهران بنیاد قرارداد است و Runtime AI فعال نشده است. انتخاب payer شخصی در Organization صریح و بدون fallback است. ReportExport با رزرو و operationId موجود idempotent است؛ Unknown را موفق/شکست قطعی ندانید.',
             '',
             'PersonalClass زیر /api/v1/personal-classes شامل ساخت/فهرست/دعوت/پذیرش/اعضا/پایان/archive است؛ HostUserId مالک، ظرفیت میزبان و عضویت پایه بدون خرید دانشجو است. CanCreateEducationalContent=false و EducationalWorkflowStatus=NotAvailable؛ Assignment/Exam مستقل هنوز ندارد. [Handoff کاربر](../product/SUBSCRIPTION_FRONTEND_HANDOFF_FA.md) و [Admin](../product/SUBSCRIPTION_ADMIN_HANDOFF_FA.md).',
             '',
             '## Replay حساب و invalidation',
             '',
             'listenerهای ConversationChanged و ClientInvalidated را قبل از Snapshot نصب و Hub را وصل کنید. GET /api/v1/realtime/changes بدون cursor فقط boundary bootstrap می‌دهد؛ سپس Snapshotهای مجاز را بخوانید و از NextCursor تا HasMore=false replay کنید. limit پیش‌فرض100/حداکثر200 است. صفحهٔ خالی با HasMore=true و RetryAfterMilliseconds=100 پایان نیست؛ cursor را از push جلو نبرید. ContextRevision opaque فقط برای برابری و جداسازی context است؛ پاسخ دیررس بافت قبلی نیز کنار گذاشته شود.',
             '',
             'بر EventId deduplicate و REST را refetch کنید؛ StreamVersion stamp فنی است و ResourceVersion نیست. unread/grade را با event افزایش یا overwrite نکنید. Direct/Saved را از /conversations/{id}/summary بخوانید؛ GET /conversations/{id} مدیریتی Group/Channel است. پس از reconnect همان cursor را replay کنید؛ cursor گفتگو با cursor حساب متفاوت است.',
             '',
             '409 REALTIME_RESYNC_REQUIRED یعنی bootstrap و Snapshot مجاز تازه؛ 503 REALTIME_TEMPORARILY_UNAVAILABLE یعنی backoff و RETRY_SAME_CURSOR؛429 تابع Retry-After است. cursor به User/Session/ContextRevision و key ring استاندارد host بسته و عمرش24ساعت است؛ refresh/context change نیز resync می‌خواهد. [قرارداد Event](../product/REALTIME_EVENT_CONTRACT_FA.md)، [Handoff](../product/MESSAGING_REALTIME_FRONTEND_HANDOFF_FA.md) و [Runbook](../operations/REALTIME_RUNBOOK_FA.md).',
             '',
             '## پیوست پاسخ آموزشی و Safety پیام‌رسان',
             '',
             'Upload با POST /files و فیلد File، دریافت metadata و Ready، سپس Attach مجاز انجام می‌شود؛ Ready به‌تنهایی Submit/Send نیست. تکلیف همان SubmissionAttempt Draft/RowVersion موجود را دارد. آزمون attachmentPolicy را از سؤال نسخهٔ تثبیت‌شده می‌خواند؛ old question default غیرفعال و policy فعال سقف مثبت تعداد/مجموع بایت و MIME صریح می‌خواهد. TextRequired فقط تشریحی و زمان Finalize است؛ Objective گزینهٔ معتبر را پیش از Attach می‌خواهد.',
             '',
             'Attach پاسخ آزمون: POST /academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}/attachments با fileAssetId/clientOperationId/writeLeaseVersion/expectedRevisionNumber؛ Detach با DELETE همان مسیر/fileId و queryهای operation/lease/revision. Save فایل قبلی را حفظ می‌کند؛ هر mutation revision و AnswerSetVersion تازه می‌دهد. برای Finalize نسخهٔ جاری و lease را بفرستید؛ زمان دستگاه و پایان Upload deadline را تمدید نمی‌کنند. Teacher/Manager مجاز فقط finalized و Parent/Guardian raw file نمی‌خوانند. [جریان کامل و خطاها](../product/ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md#frontend_handoff).',
             '',
             'BE-01 در Messenger مستقل است:422 با safetyState=PendingReview یا Rejected و operationState=NotApplied اثر جدید پیام/پیوست/تحویل نمی‌سازد. Draft را حفظ و KEEP_DRAFT_NO_AUTO_RETRY را رعایت کنید؛ safetyReference فقط ردیابی است. Human Review یا release خودکار هنوز PRODUCT_DECISION_BLOCKED است؛ بستن case تصمیم Safety را Allowed نمی‌کند. [قرارداد امن](../messaging/BE01_REVIEW_REQUIRED_CONTRACT_FA.md).',
             '## Backoffice و مجوز Platform', '',
             'Platform IAM/permission، organization/user/provisioning، education read model، moderation، commerce/finance و operations API دارند؛ Manager مدرسه Platform Admin نیست. GET /api/admin/v1/me منبع capability است، نه مجوز client-side.', '',
             'طبق تصمیم مالک در ۲۰۲۶-۱۰-۲۴، Authenticator/TOTP، Recovery و Step-Up از کد فعال حذف شده‌اند؛ صفحه و فراخوانی MFA نباید وجود داشته باشد. OTP → Session → GET /api/admin/v1/me؛ در 200 پنل و در 403 عدم دسترسی Platform. گزارش‌های GAP-04 تاریخی‌اند.', '',
             'Moderation case-scoped است؛ permission/claim/escalation/session و reason قواعد خود را دارند. دسترسی عمومی به Direct، Contacts، evidence خارج case، impersonation یا payment-success دستی وجود ندارد. [تحویل تخصصی Backoffice](../platform/BACKOFFICE_FRONTEND_HANDOFF.md).', '',
             '## AI و قابلیت‌های وابسته', '',
             '۱۲ API عمومی AI از Backend و Bearer نشست موجود مصرف می‌شوند؛ AiService:Enabled پیش‌فرض false است. URL خصوصی AI، JWT سرویس و کلید Provider به client داده نمی‌شوند. سرور پیش از network، context مجاز نقش/سازمان/فرزند/کلاس را می‌سازد و پاسخ v1 را از نظر requestId/correlationId، فیلد ناشناخته و شناسه‌های مستند در همان context بررسی می‌کند.',
             '',
             'بودجهٔ پیش‌فرض AiService:TimeoutSeconds برابر ۳۵ ثانیه و محدودهٔ تنظیم ۱..۱۸۰ است؛ client retry خودکار عمومی نسازد. 429 با AI_RATE_LIMITED، 503/خطای مدل یا اتصال با AI_SERVICE_UNAVAILABLE، 504 با AI_SERVICE_TIMEOUT و schema/grounding نامعتبر با AI_RESPONSE_INVALID نگاشت می‌شود؛ متن خام خطای Provider نمایش داده نمی‌شود. AI_CONTEXT_NOT_ALLOWED پیش از تماس خارجی 403 است؛ AI_FEATURE_DISABLED برابر 503 است. public Retry-After تضمین نشده است.',
             '',
             'Service Auth واقعی در شواهد پیشین PASS شده؛ Parent Child Summary هنوز Provider Timeout دارد و Full-12 واقعی Deferred است. نتیجه Fake-AI یا health این gate را نمی‌بندد. RAG source lifecycle نیز deferred و core آموزشی مستقل از AI است. [وضعیت جاری AI](../ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md) و [شواهد Consolidation](../release/BACKEND_FINAL_CONSOLIDATION_FA.md).',
             '',
             '## میزبانی و smoke', '',
             'Swagger/OpenAPI در Production/Staging پیش‌فرض خاموش و با Swagger:Enabled=true فعال است؛ Test طبق Program آن‌ها را منتشر می‌کند. Cors:AllowAnyOrigin انتخاب مستقل از محیط است؛ base و نمونه Test/Production فعلی true با AllowedHosts=* هستند. true از AllowAnyOrigin/Header/Method و بدون credentials استفاده می‌کند؛ false فهرست معتبر لازم دارد. REST با credentials:omit و SignalR با LongPolling و withCredentials:false، نه query-token؛ موفقیت Postman رفتار مرورگر نیست. [قرارداد CORS](../production/CORS_CONFIGURATION_FA.md).',
             '',
             '/health و /health/live فقط liveness و /health/ready config/SQL/Storage را می‌سنجد؛ probe و ring اختصاصی MFA حذف شده‌اند؛ Data Protection استاندارد host از cursor حساب حفاظت می‌کند و پایداری کلیدهایش مستقل از readiness بررسی می‌شود. provider اختیاری core را نمی‌بندد. مدل snapshot اثبات applied migration روی سرور نیست.',
             '',
             '',
             'Job operations اکنون Queues تفکیک‌شده، تعدادها و سن قدیمی‌ترین کار را می‌دهد؛ notification operations شمارش pending/processing/deferred/failed و سن oldest و failureهای اخیر را دارد. health مدیریتی وضعیت dependencyهای جاری و FileCleanup را بدون path/key نشان می‌دهد. FileCleanup=Enabled فقط تنظیم است. export فقط پس از Completed و دانلود مجاز آماده است؛ Lost ACK یا retry worker موفقیت نمایشی جدید ایجاد نمی‌کند. [قرارداد Backoffice](../platform/BACKOFFICE_FRONTEND_HANDOFF.md).',
             '',
             'انتشار فعلی مالک HTTP است و دو استثنای مستقل صریح دارد؛ default امن حفظ شده و TLS یا gate GAP-02 بسته نشده است. فایل Staging حذف و فایل Production واقعی خصوصی/ignored است. در mode=false، Origin باید دقیقاً در allowlist باشد؛ mode=true اجازه cookie credentials نمی‌دهد. موفقیت Postman رفتار مرورگر را اثبات نمی‌کند. Identity key/Storage در redeploy حفظ شوند؛ فایل‌های قدیمی key ring سرور تا cleanup مستقل حذف نشوند. [runbook](../production/DEPLOYMENT_IIS.md).', '',
             '## روش نگه‌داری و شواهد', '',
             'ابزار DocumentationSnapshot فقط TestServer بدون worker و مدل طراحی EF را می‌خواند؛ SMS/AI/SQL واقعی فراخوانی نمی‌شود. sync_repository_docs.py همهٔ method/routeها و ارجاع schemaها را تطبیق می‌دهد و build_backend_docs.py خروجی‌های HTML/PDF را می‌سازد. نام/فیلد/enum از metadata است؛ موفقیت سناریوی محصول یا محیط هدف از آن استنتاج نمی‌شود. [فهرست نگه‌داری اسناد](../documentation-inventory.csv).', '',
             '## مسیر مطالعه', '',
             'این راهنما -> مرجع جاری endpoint/DTO -> Controller واقعی -> سرویس Application -> Domain و port -> Infrastructure/EF configuration -> Migration -> Unit/Integration/Architecture tests. اسناد مرحله‌ای و PDFهای اصلی برای دلیل تصمیم و UX خوانده شوند؛ اعداد تست تاریخی به‌جای validation امروز استفاده نشوند.', '']
    current_messenger = ROOT / 'docs/messaging/CURRENT_MESSENGER_STATUS.md'
    if current_messenger.exists():
        current = current_messenger.read_text(encoding='utf-8-sig')
        capability_table = current[current.index('| حوزه'):current.index('### جریان‌های گذرا')]
        lines += ['', '## افزوده‌های جاری Messenger', '',
                  '[وضعیت یکپارچه P0/P1/P2 و gate رسانه](../messaging/CURRENT_MESSENGER_STATUS.md) و Handoffهای [P1](../messaging/MESSENGER_P1_FRONTEND_HANDOFF.md) و [P2](../messaging/MESSENGER_P2_FRONTEND_HANDOFF.md) مکمل کارت‌های کامل HTTP همین خروجی‌اند.', '',
                  capability_table.replace('(messaging/', '(../messaging/'),
                  'Hub اکنون Subscribe/UnsubscribeConversation، SetTyping، Heartbeat و Subscribe/UnsubscribePresence دارد؛ TypingChanged و PresenceChanged hint با TTL هستند، نه Message/Delta. بعد reconnect subscription تازه و Delta لازم است. default typing TTL=6s، refresh=3s، heartbeat=10s، lease=45s است؛ پیاده‌سازی حافظه‌ای تک‌instance و فاقد backplane است. Hidden اطلاعات private peer را افشا نمی‌کند. مجوز هر Send/Dispatch از وضعیت جاری ساخته می‌شود.', '']
    (ROOT / 'docs/frontend-contract/README.md').write_text('\n'.join(lines), encoding='utf-8')


def document_inventory(baseline, date, preserve_paths=(), refresh_historical_notices=False):
    paths = sorted({path for path in subprocess.check_output(
        ['git', 'ls-files', '--cached', '--others', '--exclude-standard', 'docs'], cwd=ROOT, text=True).splitlines()
        if path != 'docs/documentation-inventory.csv'})
    live = {'backend-api-guide.md', 'backend-api-reference.md', 'database-current-structure.md',
            'deployment-windows-linux-docker.md', 'frontend-contract/README.md',
            'CURRENT_PROJECT_STATUS.md', 'DOCUMENTATION_UPDATE_REPORT.md', 'messaging/CURRENT_MESSENGER_STATUS.md'}
    rows = []
    for relative in paths:
        path = ROOT / relative
        inner = path.relative_to(ROOT / 'docs').as_posix()
        generated = path.suffix in ('.html', '.pdf') and (inner.startswith('backend-api-') or inner.startswith('database-current-structure.') or inner.startswith('frontend-contract/'))
        if inner.startswith('project-context/') or (path.suffix == '.pdf' and not generated):
            kind, handling = 'مرجع اصلی/تاریخی محصول', 'حفظ اصل؛ بدون تغییر تصمیم‌ها یا فایل PDF/تصویر'
        elif generated:
            kind, handling = 'خروجی فنی جاری', 'بازتولید از source نگه‌داری‌شده؛ کنترل HTML/PDF جدا لازم است'
        elif inner == 'operations/PRODUCTION_HARDENING_AUDIT.md':
            kind, handling = 'ممیزی تاریخی عملیات', 'وضعیت شروع PH؛ قرارداد جاری از runbookهای operations و ماتریس Production خوانده شود'
        elif inner in live or inner.startswith(('production/', 'platform/', 'operations/')) or inner in {'product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md', 'product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md'} or inner in {'product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md', 'product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md'} or inner in {'dev/CONFIGURABLE_OTP_FA.md', 'dev/SERVER_MOCK_OTP_FA.md', 'dev/DEVELOPMENT_TEST_USERS_FA.md'}:
            kind, handling = 'راهنمای جاری', 'همگام‌سازی با کد؛ بدون ادعای deployment'
        elif inner.startswith('ai-handoff/') or inner.startswith('contracts/') or '/AI_' in inner:
            kind, handling = 'قرارداد/شاهد موضوعی AI', 'حفظ قرارداد و شواهد تاریخی؛ real E2E بسته‌شده فرض نشود'
        elif inner.startswith('architecture/') or inner.startswith('release/') or inner.startswith('messaging/'):
            kind, handling = 'قرارداد/گزارش مرحله‌ای', 'حفظ تصمیم و اعداد اجرای همان مرحله؛ رجوع به وضعیت جاری'
        else:
            kind, handling = 'سند موضوعی', 'حفظ محتوا و تفکیک تاریخچه از وضعیت جاری'
        if relative in preserve_paths:
            handling = 'کار قبلی کاربر؛ بدون بازنویسی و خارج تغییرات این همگام‌سازی'
        if refresh_historical_notices and relative not in preserve_paths and path.suffix == '.md' and inner not in live and not inner.startswith(('production/', 'platform/', 'operations/', 'dev/')):
            source = path.read_text(encoding='utf-8-sig')
            root_link = os.path.relpath(ROOT / 'README.md', path.parent).replace('\\', '/')
            readiness = os.path.relpath(ROOT / 'docs/production/PRODUCTION_READINESS.md', path.parent).replace('\\', '/')
            notice = f'\n<!-- DOC-SYNC:BEGIN -->\n> راهنمای خواندن، بازبینی `{date}` از `main@{baseline[:7]}`: این فایل {kind} است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن]({root_link}) و برای gateهای سرور از [آمادگی جاری]({readiness}) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.\n<!-- DOC-SYNC:END -->\n'
            path.write_text(review_notice(source, notice), encoding='utf-8')
        rows.append([relative, kind, handling, baseline, date])
    rows.append(['README.md', 'ورودی جاری مخزن', 'آمار و مسیر مطالعهٔ جاری؛ بخش‌های مرحله‌ای تاریخچه‌اند', baseline, date])
    for suffix in ('html', 'pdf'):
        relative = f'docs/database-current-structure.{suffix}'
        if relative not in paths:
            rows.append([relative, 'خروجی فنی محلی/ignored', 'بازتولید از مدل جاری؛ مطابق gitignore وارد commit نمی‌شود', baseline, date])
    for name in ('10_BACKEND_REAL_E2E_GATE.md', '11_STEP10G_FAILURE_EVIDENCE_SANITIZED.md', '12_STEP10G_FINAL_REAL_E2E_REPORT.md'):
        relative = 'docs/ai-handoff/' + name
        if (ROOT / relative).exists() and relative not in paths:
            rows.append([relative, 'فایل dirty قبلی AI', 'خارج تحویل؛ بدون تغییر، stage یا استنتاج closure', baseline, date])
    rows.append(['docs/documentation-inventory.csv', 'فهرست نگه‌داری', 'اسناد tracked، خروجی محلی و استثناهای dirty تفکیک شده‌اند', baseline, date])
    with (ROOT / 'docs/documentation-inventory.csv').open('w', encoding='utf-8-sig', newline='') as output:
        writer = csv.writer(output)
        writer.writerow(['Path', 'DocumentKind', 'Handling', 'SourceCommit', 'ReviewDate'])
        writer.writerows(rows)
    return len(rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--snapshot', type=Path, required=True)
    parser.add_argument('--reviewed-date', required=True)
    parser.add_argument('--preserve-path', action='append', default=[], help='سند دارای تغییر قبلی؛ بازنویسی نشود')
    parser.add_argument('--refresh-historical-notices', action='store_true', help='بازنویسی صریح برچسب تاریخی؛ پیش‌فرض فقط فهرست‌سازی است')
    args = parser.parse_args()
    inventory = json.loads((args.snapshot / 'repository-inventory.json').read_text(encoding='utf-8-sig'))
    baseline = inventory['SourceCommit']
    subprocess.run(['git', 'merge-base', '--is-ancestor', baseline, 'HEAD'], cwd=ROOT, check=True)
    current_sources = {path.relative_to(ROOT).as_posix() for directory in ('MakanApp.Api', 'MakanApp.Application', 'MakanApp.Domain', 'MakanApp.Infrastructure')
                       for path in (ROOT / directory).rglob('*.cs') if not {'bin', 'obj'} & set(path.parts)}
    assert current_sources == set(inventory['SourceFiles']), 'فایل کد اضافه/حذف شده؛ snapshot تازه لازم است.'
    assert inventory['SourceFiles'] and all(hashlib.sha256((ROOT / path).read_bytes()).hexdigest().upper() == digest for path, digest in inventory['SourceFiles'].items()), 'سورس از زمان snapshot تغییر کرده؛ ابتدا metadata تازه استخراج کنید.'
    api = json.loads((args.snapshot / 'openapi.json').read_text(encoding='utf-8-sig'))
    assert not any(inventory[key] for key in ('DatabaseConnected', 'HostedWorkersEnabled', 'SmsEnabled', 'AiEnabled'))
    schemas = api['components']['schemas']
    refs = re.findall(r'"\$ref"\s*:\s*"#/components/schemas/([^\"]+)"', json.dumps(api))
    assert set(refs) <= schemas.keys(), 'ارجاع schema حل‌نشده وجود دارد.'
    write_reference(api, inventory, baseline, args.reviewed_date)
    write_database(inventory, baseline, args.reviewed_date)
    write_frontend(inventory, api, baseline, args.reviewed_date)
    documents = document_inventory(baseline, args.reviewed_date, args.preserve_path, args.refresh_historical_notices)
    print(json.dumps({'baseline': baseline, 'documents': documents, 'operations': len(inventory['Operations']), 'schemas': len(schemas), 'tables': len(inventory['Tables']), 'migrations': len(inventory['Migrations']), 'databaseConnected': False}))


if __name__ == '__main__':
    main()
