"""همگام‌سازی مرجع HTTP و مدل SQL از شواهد محلی، بدون اتصال به سرویس یا دیتابیس."""

from pathlib import Path
from collections import Counter
from functools import lru_cache
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


@lru_cache(maxsize=None)
def controller_source(name, action=None):
    # چند Controller ممکن است در یک فایل باشند؛ نام type جای مسیر واقعی سورس نیست.
    declaration = re.compile(r'\bclass\s+' + re.escape(name + 'Controller') + r'\b')
    matches = [path for path in (ROOT / 'MakanApp.Api/Controllers').glob('*.cs')
               if declaration.search(path.read_text(encoding='utf-8-sig'))]
    if action and len(matches) > 1:
        # عملیات Controller چندفایلی باید به همان فایل متد پیوند بخورد.
        method = re.compile(r'\b' + re.escape(action) + r'\s*\(')
        matches = [path for path in matches if method.search(path.read_text(encoding='utf-8-sig'))]
    primary = ROOT / 'MakanApp.Api/Controllers' / (name + 'Controller.cs')
    if len(matches) > 1 and primary in matches:
        matches = [primary]
    if len(matches) != 1:
        raise ValueError(f'مسیر یکتای Controller پیدا نشد: {name}')
    return '../' + matches[0].relative_to(ROOT).as_posix()


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
        f'بازبینی `{date}` با مبنای Git `source@{baseline}` و metadata فایل‌های working tree؛ تولید از OpenAPI میزبان Testing و ApiExplorer، بدون SQL connection، worker، SMS یا AI واقعی.', '',
        f'پوشش: **{len(operations)} عملیات HTTP، {len(api["paths"])} مسیر متمایز، {len(set(item["Controller"] for item in metadata.values()))} Controller و {len(api["components"]["schemas"])} schema**. Health، Swagger و Hub در این شمارش نیستند.', '',
        'این مرجع جایگزین کاتالوگ قدیمی ۱۴۱ عملیات است. نام فیلد، nullable، request/response و enum از سند واقعی runtime آمده‌اند؛ لازم‌بودن کسب‌وکاری، authorization منبع و خطاهای lifecycle علاوه بر schema در Application/Domain بررسی می‌شوند. هیچ نمونه یا تست زنده‌ای از روی وجود schema موفق فرض نمی‌شود.', '',
        '## قرارداد مشترک و مرزهای امنیتی', '',
        '- ورود از Bearer session موجود است، نه JWT فرضی یا Role فرستاده‌شده توسط client. وضعیت anonymous هر عملیات از metadata خودش استخراج شده است.',
        '- policy درج‌شده، gate حمل‌ونقل است؛ OrganizationId، ChildId، UserId و resource ID فقط selector هستند. Application رابطه، نقش فعال و context جاری را دوباره resolve می‌کند.',
        '- GuardianRelation حق چت خصوصی کودک و Manager حق Platform نمی‌سازد. سن شرط عمومی قابلیت نیست؛ ACL، Privacy، Block و Safety جاری لازم‌اند.',
        '- مسیرهای `/api/admin/v1` از نقش Manager مدرسه جدا هستند؛ Platform OTP/Session و permission جاری دارد؛ عامل دوم حذف شده است.',
        '- POST را کور retry نکنید؛ شناسهٔ idempotency، payload و rowversion همان use case باید رعایت شوند. هدر عمومی Idempotency-Key وجود ندارد.',
        '- statusهای جدول فقط پاسخ‌های منتشرشده در OpenAPI هستند؛ خطای domain و gateهای pipeline می‌توانند پاسخ‌های بیشتری داشته باشند.',
        '- زمان سرور و UTC معتبر است؛ concurrency token دودویی در JSON به شکل Base64 است. enum رشته‌ای است و باید unknown آینده تحمل شود.', '',
        '[راهنمای جریان‌ها](backend-api-guide.md)، [قرارداد Backoffice](platform/BACKOFFICE_FRONTEND_HANDOFF.md)، [سیاست Child Safety](architecture/GAP_05_MESSENGER_CHILD_SAFETY.md)، [استقرار](production/DEPLOYMENT_IIS.md).', '',
        '## رفتار جاری فراتر از schema',
        '',
        'Open Access با Commerce:EnforcementEnabled=false و AI با AiUsage:Mode=Unmetered فعال\u200cسازی مالی نمی\u200cخواهند. enforcementEnabled در Summary مصرف شود؛ NoSubscription و Quota تاریخی مجوز قفل\u200cکردن قابلیت عادی نیستند. ACL و Feature flagهای مستقل پابرجا هستند؛ پرداخت/Push بیرونی و آموزش PersonalClass فعلاً Deferred. [سیاست جاری](product/OPEN_ACCESS_FILE_DELIVERY_FA.md).',
        '',
        'Chat/History/SSE و SourceVersion دقیق در [Adaptive AI](product-v6/ADAPTIVE_AI_FRONTEND_HANDOFF_FA.md) تشریح شده\u200cاند. SSE فقط با done موفق است؛ RAG stream پشتیبانی upstream ندارد. Retry عملیات موفق409 و Unknown بدون اجرای خودکار دوباره است. Service Auth هر دو سرویس در این انتشار false؛ Runtime تابع Enabled/config محیط است.',
        '',
        'Upload/Ready به معنی تأیید رسانه نیست. GET فایل/messaging-review برای مالک و تصمیم MediaApproved/MediaRejected در Moderation موجود است؛ خود Approval پیام نمی\u200cفرستد. [رسانه](messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md). PATCH تکلیف/submission-policy چهار فیلد اجباری و دو RowVersion می\u200cخواهد؛ Draft عمومی تغییر نکرده است. [تکلیف](product/ASSIGNMENT_SUBMISSION_POLICY_FRONTEND_HANDOFF_FA.md).',
        '',
        'خطاهای framework روی API برای400/404/405/413/415 code پایدار دارند؛ کد دامنه بازنویسی نمی\u200cشود. HEAD بدنه ندارد؛ SSE شروع\u200cشده event:error دارد. Retry-After معتبر AI/Knowledge روی429/503 حفظ می\u200cشود. statusهای کارت\u200cها پاسخ\u200cهای منتشرشده\u200cاند؛ تمام خطاهای مجوز/دامنه الزاماً در OpenAPI enumerate نشده\u200cاند.',
        '',
        'Home Messenger از صندوق حساب و History با ACL خود گفتگو است. Replay/Cursor با context و Data Protection فعلی؛ Event جای REST مجاز نیست. اعلان با makan://notifications/{notificationId} و target مجاز باز می\u200cشود. حذف حساب checkpoint/recovery دارد و execution پیش\u200cفرض خاموش است. [v6](product-v6/BACKEND_V6_FRONTEND_HANDOFF_FA.md)، [History](product-v6/MESSENGER_HISTORY_FRONTEND_HANDOFF_FA.md)، [حذف](product-v6/ACCOUNT_DELETION_FRONTEND_HANDOFF_FA.md).',
        '',
        'OTP واقعی true/false و Mock فقط Development/Test/Testing با false/true است؛ Mock مالکیت شماره را ثابت نمی\u200cکند. پروفایل، تغییر شماره، عضویت/Guardian consent و School application در مسیرهای زیر آمده\u200cاند. [راهنمای جریان\u200cها](backend-api-guide.md).',
        '## فهرست Controllerها', ''
    ]
    counts = Counter(item['Controller'] for item in metadata.values())
    lines += table(['Controller', 'تعداد عملیات', 'سورس'], [(name, count, f'[{name}Controller]({controller_source(name)})') for name, count in sorted(counts.items())])
    for controller in sorted(counts):
        lines += ['', f'## {controller}', '']
        selected = [(path, method, operation, metadata[(method, canonical(path))]) for path, method, operation in operations if metadata[(method, canonical(path))]['Controller'] == controller]
        for path, method, operation, item in sorted(selected):
            lines += [f'### {method.upper()} {path}', '', f'کد واقعی: [{controller}Controller.{item["Action"]}]({controller_source(controller, item["Action"])}).', '']
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
    lines = ['# ساختار فعلی دیتابیس MakanApp', '', f'مدل طراحی EF Core از working tree با مبنای Git `source@{baseline}`؛ بازبینی `{date}`. این سند metadata سورس است، نه گزارش اتصال یا تأیید schema یک سرور.', '',
             f'**{len(tables)} جدول، {counts["Columns"]} ستون، {counts["ForeignKeys"]} FK، {counts["Indexes"]} index مستقل از key و {counts["Checks"]} CHECK؛ {len(inventory["Migrations"])} Migration.**', '',
             '## مفاهیم و مرزها', '',
             'آخرین Migration سورس 20261122085930_AddMessagingMediaReviews است. جدول کامل پایین مرجع ترتیب است؛ افزوده\u200cهای v6 شامل School application، PersonalClass، Source/Knowledge، Account deletion و AI history هستند. AddMessagingMediaReviews همان ModerationCases را با تصمیم اثرانگشت\u200cدار توسعه داده است. RemovePlatformMfa تاریخی حذف داده دارد و Down داده را بازیابی نمی\u200cکند. Migration خودکار startup نداریم؛ applied schema سرور مستقل بررسی شود.', '',
             'Blob، metadata، نسخهٔ Source، Evidence رسانه و History AI باید با سیاست retention ماژول خود نگه‌داری شوند. terminal cleanup عمومی جای حذف حساب، حذف Knowledge یا purge تاریخچه نیست. [retention](operations/RETENTION_AND_CLEANUP_POLICY.md).', '',
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


FRONTEND_CONTRACT = """## شروع اتصال

Base اصلی /api/v1 و Backoffice /api/admin/v1 است. Bearer opaque در Authorization header؛ GUID، enum رشته‌ای، UTC و RowVersion/Base64 از DTO واقعی گرفته شوند. شکل paging همهٔ مسیرها یکسان نیست؛ کارت همان عملیات را بخوانید. cookie session و هدر عمومی Idempotency-Key نداریم.

OTP/Refresh و Profile را کامل و Workspace معتبر را انتخاب کنید. Role/OrganizationId/ChildId انتخاب‌گرند؛ سرور رابطهٔ جاری را مجاز می‌کند. Assistant context از Workspace مدرسه مستقل است و تغییر نقش fallback شخصی پنهان نمی‌سازد. Platform Admin با Manager مدرسه متفاوت است؛ Guardian حق چت خصوصی ندارد.

## سیاست دسترسی جاری

Commerce:EnforcementEnabled=false و AiUsage:Mode=Unmetered. Summary دارای enforcementEnabled=false را با NoSubscription/Quota تاریخی قفل نکنید؛ age/DOB شرط عمومی قابلیت نیست. ACL، Privacy، Block، Safety، ظرفیت واقعی کلاس و حدود فنی فایل پابرجا هستند. پرداخت/فروش آموزشگاه و Push بیرونی برای نسخهٔ جاری Deferred؛ PersonalClass Assignment/Exam با دو Feature flag مستقل خاموش‌اند. [Open Access](../product/OPEN_ACCESS_FILE_DELIVERY_FA.md)، [Scope](../product-v6/CURRENT_RELEASE_SCOPE_FA.md).

## نقشهٔ صفحات و قراردادهای تخصصی

| جریان | رفتار و قرارداد |
|---|---|
| Profile / Phone / Workspace | Bio/DOB/Avatar، تغییر شماره، نشست و بافت مجاز؛ [v6](../product-v6/BACKEND_V6_FRONTEND_HANDOFF_FA.md) |
| School application | پرونده، مدرک خصوصی، اصلاح/بررسی و مدیر اولیهٔ صریح؛ پرداخت Runtime Deferred؛ [قرارداد](../product-v6/SCHOOL_APPLICATION_FRONTEND_HANDOFF_FA.md) |
| Organization / Guardian | مدیریت نقش با محافظ آخرین مدیر، Directory/roster، رضایت والد و Lesson؛ [مدرسه](../product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md)، [نقش](../product/ORGANIZATION_ROLE_MANAGEMENT_20261105_FA.md) |
| PersonalClass | درخواست/تأیید، عضویت، نقش محلی، انتقال مسئولیت؛ آموزش مستقل خاموش؛ [قرارداد](../product/PERSONAL_CLASS_V2_ACADEMIC_FRONTEND_HANDOFF_FA.md) |
| منابع آموزشی | Source/version/processing، ACL جاری و Archive/Revoke؛ Library عمومی/AI بسته؛ [فایل و Library](../product-v6/FILES_LIBRARY_FRONTEND_HANDOFF_FA.md) |
| اعلان | makan://notifications/{notificationId} سپس GET /notifications/{id}/target؛ permission فعلی، مقصد غیرمجاز404؛ [قرارداد](../product-v6/NOTIFICATION_FRONTEND_HANDOFF_FA.md) |
| حذف حساب | درخواست/پیگیری و freeze نشست با checkpoint/recovery؛401 به معنی Completed نیست؛ [قرارداد](../product-v6/ACCOUNT_DELETION_FRONTEND_HANDOFF_FA.md) |
| Commerce / Admin | Owner از Payer و Role جدا؛ Plan/Quote/Order واقعی حفظ؛ gate مالی خاموش؛ [قرارداد](../product-v6/COMMERCE_CURRENT_POLICY_FRONTEND_HANDOFF_FA.md) |

## تکلیف، آزمون و تحویل

Upload فایل را جدا از Attach و Submit نشان دهید. برای Published Assignment از PATCH /academic/assignments/{assignmentId}/submission-policy استفاده کنید: dueAtUtc، allowLateSubmission، expectedAssignmentRowVersion و expectedVersionRowVersion، هر چهار فیلد اجباری. تمدید کوتاه نمی‌کند؛ تغییر Late به‌تنهایی با عین مهلت موجود مجاز است. expectedVersionها را از GET جاری بگیرید؛412 نیازمند refetch و تصمیم مجدد است. ویرایش عمومی تکلیف Draft-only باقی است.

409 SUBMISSION_DEADLINE_PASSED دارای requiredAction=REFETCH_ASSIGNMENT_AND_ATTEMPT، operationState=NotApplied و retryable=false است. Draft و FileAssetId موفق را نگه دارید؛ Upload دوباره/Retry خودکار نکنید. با Late مجاز، صرف گذشت مهلت مانع UI نباشد. isLate رسید نهایی تاریخی است و با مهلت تازه دوباره محاسبه نمی‌شود. [جزئیات](../product/ASSIGNMENT_SUBMISSION_POLICY_FRONTEND_HANDOFF_FA.md).

آزمون از QuestionVersion و attachmentPolicy تثبیت‌شده استفاده می‌کند؛ Attach/Detach دارای clientOperationId، writeLeaseVersion و expectedRevisionNumber است. هر mutation نسخهٔ پاسخ/AnswerSetVersion را عوض می‌کند؛ Finalize نسخهٔ جاری و مهلت سرور را می‌سنجد. مجوز Teacher/Manager برای پاسخ نهایی و Guardian برای دادهٔ خام یکی نیست. [پیوست آموزشی](../product/ACADEMIC_ANSWER_ATTACHMENTS_IMPLEMENTATION_FA.md#frontend_handoff).

## Messenger، Home و Replay

Home از GET /conversations/account، تاریخچه از مسیر messages همان گفتگو و مجوز جاری حساب می‌آید؛ سوییچ Workspace/Role تاریخچهٔ حساب را حذف نمی‌کند. [History و ورود تازه](../product-v6/MESSENGER_HISTORY_FRONTEND_HANDOFF_FA.md). Account inbox replay با scoped replay یکی نیست؛ cursorها را جابه‌جا نکنید.

listenerهای ConversationChanged/ClientInvalidated را پیش از Snapshot نصب کنید؛ bootstrap /realtime/changes بدون cursor boundary می‌دهد، سپس Snapshot و drain تا HasMore=false. صفحهٔ خالی با HasMore=true و RetryAfterMilliseconds پایان نیست. ContextRevision فقط مقایسهٔ برابری و EventId برای dedup است؛ StreamVersion نسخهٔ business نیست.409 REALTIME_RESYNC_REQUIRED یعنی Snapshot تازه؛503 REALTIME_TEMPORARILY_UNAVAILABLE یعنی RETRY_SAME_CURSOR. Push وضعیت grade/unread را جای REST بازنویسی نمی‌کند.

SignalR /hubs/messaging با Bearer header و مرورگر LongPolling/withCredentials:false است؛ query-token پشتیبانی نمی‌شود. reconnect نیازمند subscribe تازه و Replay است. Typing/Presence گذرا و تک‌instance‌اند؛ backplane/Load محیط هدف از تست محلی نتیجه نمی‌شود. [قرارداد Event](../product/REALTIME_EVENT_CONTRACT_FA.md).

## فایل و بازبینی رسانه

POST /files?workspaceScope=Personal با multipart/form-data و file؛ boundary را client بسازد. Ready فقط موفقیت ذخیره است. فایل پیام باید kind=File/Image متناسب و attachmentIds داشته باشد؛ clientMessageId/payload در Retry ثابت بمانند. سقف فعلی۱۰MiB، MIMEهای PDF/JPEG/PNG/text/plain و بررسی signature/extension پابرجا هستند.

Send اولیهٔ رسانهٔ بدون تصمیم422 و پروندهٔ موجود Moderation می‌سازد. مالک GET /files/{fileId}/messaging-review را برای NotRequested/PendingReview/Approved/Rejected مصرف کند. Pending=WAIT_FOR_MEDIA_REVIEW؛ Rejected=MEDIA_REJECTED_KEEP_DRAFT؛ retryable=false و اثر جدید Message/Binding/Delivery/Download=false. پس از Approval، ارسال همان intent مجاز دوباره بررسی می‌شود؛ تأیید خودش پیام نمی‌فرستد. پروندهٔ متنی ReviewRequired خودکار آزاد نمی‌شود.

کارشناس Platform پس از Claim/Evidence با Review+Enforce و expectedVersion/reason، MediaApproved/MediaRejected ثبت می‌کند. self-approval و نسخهٔ کهنه رد می‌شوند؛ Resolve اداری Approval نیست. گیرنده فقط بعد Binding و ACL جاری فایل را با Bearer و private/no-store می‌گیرد؛ Pending/Rejected برای گیرنده404 است. [Handoff رسانه](../messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md).

## Chat، History و Streaming هوشمند

POST /ai/chat با message/operationId و conversationId اختیاری شروع می‌شود؛ Profile/Grade/فایل اجباری نیست. assistantProfile اختیاری و بدون اثر مجوز است. History مستقل از Messenger و متعلق به Owner/Context است؛ GET /ai/conversations/{id}/history پنجرهٔ زوج‌های کامل موفق، حداکثر۲۰ turn/۴۰۰۰ هرturn/۱۲۰۰۰ مجموع را می‌دهد. پاسخ no-store است؛ تغییر بافت مجوز تاریخچهٔ قبلی نمی‌سازد.

General بدون منبع؛ SourcesOnly بدون fallback؛ Auto/SourceFocused منتظر منبع آماده. fallback عمومی فقط با General صریح و provenance روشن، هرگز برای منبع نامجاز. selectedSources نسخهٔ دقیق را pin می‌کند؛ Citation از evidence واقعی است و Library برای AI بسته می‌ماند.

POST /ai/chat/stream همان body و Bearer با Accept:text/event-stream است. fetch incremental و decoder UTF-8 لازم است؛ start یک‌بار، delta متوالی و done/error. HTTP200 و EOF بدون done موفق نیستند؛ partial در History کامل ثبت نمی‌شود. خطا پیش از start از ProblemDetails و پس از start از event:error خوانده شود. RAG stream رسمی ندارد؛409 AI_SOURCE_STREAMING_NOT_SUPPORTED نیازمند انتخاب مسیر غیرجریانی با operationId تازه است.

Retry همان body اولیه/endpoint/operationId است. ALREADY_COMPLETED برابر409؛ History را بخوانید. IN_PROGRESS/OUTCOME_UNKNOWN اجرای خودکار تازه نمی‌خواهند. AI نامحدود، idempotency/ACL یا محدودیت واقعی upstream را حذف نمی‌کند. Service Auth هر دو سرویس false و Runtime تابع config محیط است. [قرارداد کامل Adaptive](../product-v6/ADAPTIVE_AI_FRONTEND_HANDOFF_FA.md).

## خطا، HTTP و میزبانی

پاسخ preflight با اجرای AI متفاوت است: `POST /api/v1/ai/sources/preflight` می‌تواند200 با `authorized=true`، `retrievalAvailable=false` و `errorCode=AI_SOURCE_RETRIEVAL_NOT_AVAILABLE` بدهد؛ authorized فقط مجوز است، نه موفقیت Retrieval/RAG. همان ناتوانی در اجرای واقعی409 می‌شود. envelope قابلیت از ProblemDetails جدا تفسیر شود.

خطای DNS/TLS/offline/CORS یا Abort ممکن است status قابل خواندن نداشته باشد؛500 ساختگی نسازید. پاسخ HTML یا JSON نامعتبر با200 موفقیت نیست. پاسخ خالی یا BFF با401 بدون code باید fallback امن و حفظ status/correlation داشته باشد، نه parser exception. HEAD بدنه ندارد و Range نامعتبر416 قرارداد فایل خودش را حفظ می‌کند؛ تمام پاسخ‌ها را JSON اجباری فرض نکنید.

[قرارداد تفصیلی خطای Frontend](../../reports/api-error-audit/20261125-api-errors-ac9a239-01/FRONTEND_ERROR_HANDOFF_FA.md) ترتیب تفسیر و مرز پیشنهادهای UI را توضیح می‌دهد.

400/404/405/413/415 framework زیر /api به‌ترتیب REQUEST_INVALID/RESOURCE_NOT_FOUND/METHOD_NOT_ALLOWED/REQUEST_TOO_LARGE/UNSUPPORTED_MEDIA_TYPE دارند؛ کد دامنه بازنویسی نمی‌شود. HEAD بدنه ندارد.401 نشست،403 مجوز،404 پنهان‌سازی/نبود و409/412 تضاد را از code/requiredAction همان عملیات تفسیر کنید. correlationId/traceId و X-Correlation-ID را برای پشتیبانی نگه دارید؛ محتوای خصوصی یا Token لاگ نشود.

Retry-After معتبر خطاهای429/503 AI/Knowledge به ثانیهٔ گرد‌شده بالا حفظ می‌شود؛ POST را فقط با قواعد idempotency همان عملیات تکرار کنید. Content-Type application/problem+json و Accept نامناسب نباید به parse متن خطا منجر شود. پاسخ خطای SSE از قرارداد خودش پیروی می‌کند.

CORS از config محیط مستقل است؛ AllowAnyOrigin=true اجازهٔ credentials نمی‌دهد و false allowlist معتبر لازم دارد. REST با credentials:omit و SignalR با LongPolling/withCredentials:false مصرف شود. /health/ready فقط config/SQL/Storage است؛ Provider اختیاری، Push دستگاه، Data Protection ring و SLO مستقل پذیرفته شوند. [استقرار](../deployment-windows-linux-docker.md)، [Runbook](../operations/PRODUCTION_CONFIGURATION_RUNBOOK.md).

## نگه‌داری و مسیر مطالعه

راهنمای بک‌اند → مرجع HTTP/DTO → Controller → Application → Domain/Port → Infrastructure/EF → Migration → تست. ابزار Snapshot بدون SQL/worker/Provider اجرا می‌شود؛ HTML/PDF این سند کاتالوگ کامل API را ضمیمه دارد. اعداد تست تاریخی شاهد validation امروز نیستند؛ [وضعیت جاری](../CURRENT_PROJECT_STATUS.md)، [فهرست اسناد](../documentation-inventory.csv).
"""


def write_frontend(inventory, api, baseline, date):
    lines = ['# قرارداد جاری مصرف Backend برای Web و Mobile', '',
             f'بازبینی `{date}` با مبنای Git `source@{baseline}`؛ قرارداد واقعی Backend برای مصرف‌کننده، بدون تغییر Frontend.', '',
             f'پوشش: {len(inventory["Operations"])} عملیات HTTP، {len(api["paths"])} مسیر، {len(api["components"]["schemas"])} schema؛ {len(inventory["Tables"])} جدول و {len(inventory["Migrations"])} Migration.', '',
             '[HTML مستقل](MakanApp-Frontend-Backend-Contract.html)، [PDF](MakanApp-Frontend-Backend-Contract.pdf)، [مرجع API](../backend-api-reference.md)، [راهنمای بک‌اند](../backend-api-guide.md).', '',
             FRONTEND_CONTRACT]
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
            notice = f'\n<!-- DOC-SYNC:BEGIN -->\n> راهنمای خواندن، بازبینی `{date}` از `source@{baseline[:7]}`: این فایل {kind} است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن]({root_link}) و برای gateهای سرور از [آمادگی جاری]({readiness}) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.\n<!-- DOC-SYNC:END -->\n'
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
