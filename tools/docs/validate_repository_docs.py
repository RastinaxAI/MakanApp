"""کنترل پوشش کاتالوگ، لینک‌های فایل و خروجی HTML؛ بدون SQL، provider یا تغییر فایل محصول."""

from pathlib import Path
from urllib.parse import unquote, urlsplit
import argparse
import csv
import hashlib
import json
import re
import subprocess
import sys
import build_backend_docs as build

ROOT = Path(__file__).resolve().parents[2]


def unresolved_links(path, source, html=False):
    if not html:
        source = re.sub(r'(?ms)^(```|~~~).*?^\1\s*$', '', source)
    targets = re.findall(r'href="([^\"]+)"', source) if html else re.findall(r'\[[^\]]*\]\(([^)\s]+)\)', source)
    failures = []
    for target in targets:
        uri = urlsplit(target)
        if uri.scheme or uri.netloc or not uri.path or uri.path.startswith('/'):
            continue
        resolved = path.parent / unquote(uri.path)
        if not resolved.exists():
            failures.append({'source': path.relative_to(ROOT).as_posix(), 'target': target})
    return failures


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--snapshot', type=Path, required=True)
    parser.add_argument('--pdf-deps', type=Path, help='با تعیین deps، تطابق PDF با HTML منبع هم بررسی می‌شود.')
    args = parser.parse_args()
    inventory = json.loads((args.snapshot / 'repository-inventory.json').read_text(encoding='utf-8-sig'))
    api = json.loads((args.snapshot / 'openapi.json').read_text(encoding='utf-8-sig'))
    reference = (ROOT / 'docs/backend-api-reference.md').read_text(encoding='utf-8-sig')
    catalog = re.findall(r'^### (GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS) (.+)$', reference, re.M)
    expected = {(method.upper(), path) for path, item in api['paths'].items() for method in item if method in {'get', 'post', 'put', 'patch', 'delete', 'head', 'options'}}
    assert len(catalog) == len(set(catalog)) and set(catalog) == expected, 'کاتالوگ HTTP کامل یا یکتا نیست.'
    assert all(f'id="contract-{name}"' in reference for name in api['components']['schemas']), 'schema مستندنشده وجود دارد.'
    wire_enums = {item['Name']: item for item in inventory['Enums']}
    enum_mismatches = [name for name, schema in api['components']['schemas'].items()
                       if name in wire_enums and schema.get('type') == 'integer'
                       and any(isinstance(value, str) for value in wire_enums[name]['Values'])]
    assert 'DOC-CONTRACT-01' in reference
    for name in enum_mismatches:
        assert json.dumps(wire_enums[name]['Values'], ensure_ascii=False) in reference, 'literal واقعی Enum مستند نشده است.'
    assert len({(item['Schema'], item['Table']) for item in inventory['Tables']}) == len(inventory['Tables']), 'مدل شامل table sharing است؛ شمارش باید بازبینی شود.'
    database = (ROOT / 'docs/database-current-structure.md').read_text(encoding='utf-8-sig')
    for item in inventory['Tables']:
        assert f'### {item["Schema"]}.{item["Table"]}' in database
    assert all(migration in database for migration in inventory['Migrations'])
    failures = []
    docs = subprocess.check_output(['git', 'ls-files', 'docs', 'README.md'], cwd=ROOT, text=True).splitlines()
    for relative in docs:
        path = ROOT / relative
        if path.suffix == '.md':
            failures.extend(unresolved_links(path, path.read_text(encoding='utf-8-sig')))
    for name in build.DOCUMENTS:
        path = build.document_path(name, 'html')
        source = path.read_text(encoding='utf-8-sig')
        identifiers = re.findall(r'\bid="([^\"]+)"', source)
        assert len(identifiers) == len(set(identifiers))
        assert set(re.findall(r'href="#([^\"]+)"', source)) <= set(identifiers)
        failures.extend(unresolved_links(path, source, html=True))
    with (ROOT / 'docs/documentation-inventory.csv').open(encoding='utf-8-sig', newline='') as source:
        rows = list(csv.DictReader(source))
    assert len({row['Path'] for row in rows}) == len(rows), 'فهرست سندها تکراری است.'
    assert all((ROOT / row['Path']).exists() for row in rows)
    pdf_outputs = {}
    if args.pdf_deps:
        sys.path.insert(0, str(args.pdf_deps.resolve()))
        import pymupdf
        for name in build.DOCUMENTS:
            html_sha = hashlib.sha256(build.document_path(name, 'html').read_bytes()).hexdigest()
            with pymupdf.open(build.document_path(name, 'pdf')) as document:
                assert document.metadata.get('keywords') == 'source-html-sha256:' + html_sha, 'PDF با HTML جاری یکسان نیست.'
                assert not document.is_encrypted and document.get_toc()
                pdf_outputs[name] = {'pages': len(document), 'bookmarks': len(document.get_toc()), 'sourceHtmlSha256': html_sha}
    report = {'operations': len(catalog), 'schemas': len(api['components']['schemas']), 'tables': len(inventory['Tables']),
              'migrations': len(inventory['Migrations']), 'inventoryEntries': len(rows), 'brokenLinks': failures,
              'enumOpenApiMismatches': enum_mismatches, 'pdfOutputs': pdf_outputs, 'databaseConnected': False, 'smsSent': False}
    (args.snapshot / 'documentation-validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))
    if failures:
        raise SystemExit('لینک‌های فایل باید پیش از تحویل اصلاح شوند.')


if __name__ == '__main__':
    main()
