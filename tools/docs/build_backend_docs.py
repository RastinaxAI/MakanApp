"""ساخت HTML/PDF جداگانه دو کتاب بک‌اند از Markdownهای نسخه‌بندی‌شده."""

from pathlib import Path
from html import escape, unescape
import argparse
import hashlib
import json
import os
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / '.artifacts/backend-handbook'
DOCUMENTS = {
    'backend-api-guide': ('راهنمای جامع پروژه و بک‌اند ماکان', 'شناخت محصول، معماری، جریان اجرا و توسعه'),
    'backend-api-reference': ('مرجع کامل API و قراردادهای ماکان', 'قرارداد انتقال، قواعد و مسیر کد هر عملیات'),
}

CSS = '''
:root{--ink:#20363b;--muted:#60757a;--accent:#147780;--line:#d9e6e8;--paper:#fff}
*{box-sizing:border-box}html{scroll-padding-top:70px}body{margin:0;color:var(--ink);background:#f1f5f6;font:15px/1.95 Tahoma,"Segoe UI",sans-serif}
a{color:var(--accent);text-decoration:none;overflow-wrap:anywhere}a:hover{text-decoration:underline}
.readerbar{position:sticky;top:0;z-index:4;display:flex;align-items:center;justify-content:space-between;padding:13px 25px;background:#163a42;color:#fff}
.readerbar a{color:#fff}.actions{display:flex;gap:16px;align-items:center}.actions button{background:transparent;color:#fff;border:1px solid #8db1b6;padding:6px 12px;border-radius:6px;cursor:pointer;font:inherit}
.layout{display:grid;grid-template-columns:285px minmax(0,1120px);gap:24px;max-width:1450px;margin:24px auto;padding:0 20px;direction:rtl}
aside{position:sticky;top:80px;align-self:start;max-height:calc(100vh - 105px);overflow:auto;background:#fff;border:1px solid var(--line);border-radius:10px;padding:18px;font-size:12px;line-height:1.8}
aside h2{font-size:16px;margin:0 0 12px}aside ul{padding-right:16px}aside li{margin:7px 0}aside ul ul{font-size:11px}
main{background:var(--paper);border:1px solid var(--line);border-radius:10px;padding:45px 50px;min-width:0}
.cover{padding:32px 0 45px;border-top:7px solid var(--accent);border-bottom:1px solid var(--line);margin-bottom:38px}
.brand{font:700 15px "Segoe UI",sans-serif;letter-spacing:1.3px;color:var(--accent);direction:ltr;text-align:left}.cover h1{font-size:32px;line-height:1.7;margin:30px 0 14px}.subtitle{font-size:18px;color:var(--muted)}.edition{margin-top:30px;font-size:13px}.reading-note{padding:16px;background:#eef6f7;border-right:4px solid var(--accent);margin:30px 0}
h1{font-size:28px;margin:12px 0 24px}h2{font-size:22px;line-height:1.7;margin:45px 0 18px;padding-bottom:10px;border-bottom:1px solid var(--line)}h3{font-size:18px;line-height:1.8;margin:30px 0 15px}h4{font-size:15px;margin:22px 0 12px}p{margin:14px 0}li{margin:6px 0}strong{font-weight:700}
code{font:12px/1.7 Consolas,"Courier New",monospace;direction:ltr;unicode-bidi:isolate;background:#edf3f4;border-radius:3px;padding:2px 4px;overflow-wrap:anywhere}pre{direction:ltr;text-align:left;background:#eef3f5;border:1px solid #dce6eb;border-radius:6px;padding:18px;line-height:1.65;overflow-wrap:anywhere;white-space:pre-wrap;max-width:100%}pre code{background:transparent;padding:0;font-size:11.5px;white-space:pre-wrap;word-break:break-word;unicode-bidi:normal}
.table-wrap{overflow:auto;margin:18px 0}table{border-collapse:collapse;width:100%;font-size:12px;line-height:1.9;table-layout:fixed}th,td{border:1px solid var(--line);padding:10px 12px;vertical-align:top;overflow-wrap:anywhere}th{background:#e8f2f4;text-align:right;font-weight:700}tbody tr:nth-child(even){background:#fafcfc}table code{font-size:10.8px}blockquote{margin:18px 0;padding:10px 20px;border-right:4px solid var(--accent);background:#f4f8f9}hr{border:0;border-top:1px solid var(--line);margin:30px 0}.print-toc{display:none}
@media(max-width:1000px){.layout{display:block;padding:0 12px}aside{position:static;max-height:360px;margin-bottom:18px}main{padding:25px 22px}.readerbar{padding:12px}.brand{font-size:12px}}
@page{size:A4;margin:16mm 14mm 17mm}
@media print{body{background:#fff;font-size:10pt;line-height:1.75;-webkit-print-color-adjust:exact;print-color-adjust:exact}.readerbar,aside{display:none}.layout{display:block;margin:0;padding:0;max-width:none}main{border:0;border-radius:0;padding:0}.cover{break-after:page;min-height:235mm;padding-top:18mm}.cover h1{font-size:30pt}.subtitle{font-size:16pt}.edition{font-size:11pt}.reading-note{font-size:10pt}h1{font-size:22pt}h2{font-size:17pt;margin-top:24pt}h3{font-size:13pt;margin-top:18pt}h4{font-size:11pt}h1,h2,h3,h4{break-after:avoid}p,li{orphans:3;widows:3}table{font-size:8.5pt;line-height:1.7}th,td{padding:6pt}table code{font-size:7.6pt}thead{display:table-header-group}tr{break-inside:avoid}.table-wrap{overflow:visible;margin:12pt 0}pre{padding:11pt;font-size:8pt;break-inside:auto}pre code{font-size:8pt;line-height:1.6}code{font-size:8pt}a{color:#145e68}.cover p{margin:16pt 0}}
'''


def html_document(name, markdown_module):
    source = (ROOT / 'docs' / f'{name}.md').read_text(encoding='utf-8')
    baseline = re.search(r'main@([0-9a-f]{7,40})', source)
    reviewed = re.search(r'`(\d{4}-\d{2}-\d{2})`', source)
    if not baseline or not reviewed:
        raise RuntimeError(f'مبنای سورس یا تاریخ بازبینی در Markdown {name} مشخص نیست.')
    edition = (baseline.group(1), reviewed.group(1))
    renderer = markdown_module.Markdown(extensions=['extra', 'toc'], extension_configs={'toc': {'toc_depth': '2-3'}})
    body = renderer.convert(source)
    navigation = renderer.toc
    headings = []

    def heading(match):
        level, old_id, title = match.groups()
        stable_id = f'heading-{len(headings) + 1}'
        headings.append((int(level), stable_id, re.sub(r'<[^>]+>', '', unescape(title))))
        return f'<h{level} id="{stable_id}">{title}</h{level}>'

    old_ids = re.findall(r'<h([23]) id="([^"]+)">(.*?)</h\1>', body, re.S)
    body = re.sub(r'<h([23]) id="([^"]+)">(.*?)</h\1>', heading, body, flags=re.S)
    for (_, old_id, _), (_, stable_id, _) in zip(old_ids, headings):
        navigation = navigation.replace(f'href="#{old_id}"', f'href="#{stable_id}"')
        body = body.replace(f'href="#{old_id}"', f'href="#{stable_id}"')
    for document in DOCUMENTS:
        body = body.replace(f'href="{document}.md', f'href="{document}.html')
    body = body.replace('<table>', '<div class="table-wrap"><table>').replace('</table>', '</table></div>')
    title, subtitle = DOCUMENTS[name]
    cover = f'''<section class="cover"><div class="brand">MAKANAPP / BACKEND ENGINEERING</div><h1>{title}</h1><p class="subtitle">{subtitle}</p><p class="edition">نسخه بررسی: <code>main @ {edition[0]}</code><br>تاریخ بازبینی: <code>{edition[1]}</code></p><p class="reading-note">کتاب فارسی برای توسعه‌دهنده بک‌اند؛ بر مبنای کد واقعی، SQL Server و قراردادهای فعلی. این فایل بدون اینترنت قابل مطالعه است. برای جست‌وجو از Ctrl+F استفاده کنید.</p></section>'''
    print_style = '@media print{.print-toc{display:block;break-after:page}.print-toc .toc{column-count:2;column-gap:9mm;font-size:8pt}.print-toc li{margin:4pt 0}.print-toc ul{padding-right:12pt}}'
    output = f'''<!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{title}</title><style>{CSS}{print_style}</style></head><body><header class="readerbar"><span>ماکان · کتاب بک‌اند</span><div class="actions"><a href="{name}.pdf" download>دریافت PDF</a><button type="button" onclick="window.print()">چاپ</button></div></header><div class="layout"><aside aria-label="فهرست مطالب"><h2>فهرست مطالب</h2>{navigation}</aside><main>{cover}<section class="print-toc"><h2>فهرست کتاب</h2>{navigation}</section>{body}</main></div></body></html>'''
    path = ROOT / 'docs' / f'{name}.html'
    path.write_text(output, encoding='utf-8')
    identifiers = re.findall(r'\bid="([^"]+)"', output)
    assert len(identifiers) == len(set(identifiers)), f'شناسه تکراری در {name}'
    links = re.findall(r'href="#([^"]+)"', output)
    assert set(links) <= set(identifiers), f'لینک داخلی نامعتبر در {name}'
    assert not re.search(r'<(?:script|link|img)\b[^>]+(?:src|href)="https?://', output), 'وابستگی آنلاین نباید در HTML باشد'
    return path, headings, edition, {'htmlBytes': path.stat().st_size, 'internalLinks': len(links), 'anchors': len(identifiers), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def find_browser(configured):
    candidates = [Path(configured)] if configured else [
        Path(os.environ.get('PROGRAMFILES(X86)', 'C:/Program Files (x86)')) / 'Microsoft/Edge/Application/msedge.exe',
        Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Microsoft/Edge/Application/msedge.exe',
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise RuntimeError('مسیر مرورگر پیدا نشد؛ از --browser برای مسیر Edge/Chromium استفاده کنید.')


def pdf_document(name, html_path, headings, edition, browser, pdf):
    raw = WORK / f'{name}-raw.pdf'
    arguments = [str(browser), '--headless', '--disable-gpu', '--no-first-run', '--no-pdf-header-footer', f'--user-data-dir={WORK / (name + "-browser")}', f'--print-to-pdf={raw}', html_path.as_uri()]
    flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
    with (WORK / f'{name}-browser.log').open('w', encoding='utf-8') as log:
        subprocess.run(arguments, stdout=log, stderr=log, creationflags=flags, check=True, timeout=240)
    if not raw.exists():
        raise RuntimeError(f'مرورگر PDF برای {name} نساخت.')
    document = pdf.open(raw)
    destinations = {}
    for page in document:
        for link in page.get_links():
            if link.get('nameddest') and link.get('page', -1) >= 0:
                destinations[link['nameddest']] = link['page'] + 1
    toc = []
    missing = []
    for level, identifier, title in headings:
        if identifier not in destinations:
            missing.append(identifier)
        else:
            toc.append([level - 1, title, destinations[identifier]])
    assert not missing, f'مقصد نشانک پیدا نشد: {missing[:8]}'
    document.set_toc(toc)
    blank, outside = [], []
    for index, page in enumerate(document, 1):
        if len(page.get_text().strip()) < 30:
            blank.append(index)
        for block in page.get_text('dict')['blocks']:
            for line in block.get('lines', []):
                for span in line['spans']:
                    x0, y0, x1, y1 = span['bbox']
                    if min(x0, y0) < -1 or x1 > page.rect.width + 1 or y1 > page.rect.height + 1:
                        outside.append([index, span['text'][:60]])
        page.insert_textbox(pdf.Rect(34, page.rect.height - 28, page.rect.width - 34, page.rect.height - 14), f'MakanApp | {name} | {index} / {len(document)}', fontname='helv', fontsize=7, align=1, color=(.35, .43, .48))
    assert not blank, f'صفحات خالی: {blank}'
    assert not outside, f'متن بیرون صفحه: {outside[:8]}'
    title, subtitle = DOCUMENTS[name]
    document.set_metadata({'title': title, 'author': 'MakanApp Backend', 'subject': subtitle + f'؛ main@{edition[0]}؛ {edition[1]}', 'creator': 'Markdown / Microsoft Edge / PyMuPDF'})
    target = ROOT / 'docs' / f'{name}.pdf'
    document.save(target, garbage=1, deflate=True)
    selected = sorted(set([0, len(document) // 2, len(document) - 1] + [destinations[headings[i][1]] - 1 for i in (0, min(len(headings) - 1, 10))]))
    for index in selected:
        document[index].get_pixmap(matrix=pdf.Matrix(1.2, 1.2)).save(WORK / f'{name}-page-{index + 1}.png')
    result = {'pdfPages': len(document), 'bookmarks': len(toc), 'blankPages': blank, 'textOutsidePage': outside, 'pdfBytes': target.stat().st_size, 'renderedPages': [index + 1 for index in selected]}
    document.close()
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--deps', type=Path, help='پوشه نصب جداگانه وابستگی‌های Python')
    parser.add_argument('--pdf', action='store_true', help='ساخت PDF علاوه بر HTML')
    parser.add_argument('--browser', help='مسیر Edge/Chromium برای چاپ PDF')
    parser.add_argument('--document', choices=list(DOCUMENTS), help='ساخت تنها یک سند')
    args = parser.parse_args()
    if args.deps:
        sys.path.insert(0, str(args.deps.resolve()))
    import markdown
    WORK.mkdir(parents=True, exist_ok=True)
    browser = find_browser(args.browser) if args.pdf else None
    if args.pdf:
        import pymupdf
    report = {}
    for name in ([args.document] if args.document else DOCUMENTS):
        path, headings, edition, result = html_document(name, markdown)
        if args.pdf:
            result.update(pdf_document(name, path, headings, edition, browser, pymupdf))
        report[name] = result
        print(json.dumps({name: result}, ensure_ascii=False), flush=True)
    (WORK / 'document-audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
