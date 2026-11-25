"""بازبینی مرجع‌ها، عدم نشت credential و حفظ WIP؛ هیچ مقدار خصوصی چاپ نمی‌شود."""
import csv
import difflib
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path.cwd()
REPORT = ROOT/'reports/api-error-audit/20261125-api-errors-ac9a239-01'
PRIVATE = ROOT/'.artifacts/api-error-audit-20261125'
baseline = json.loads((PRIVATE/'baseline.json').read_text())
protected = []
for entry in baseline['protectedFiles']:
    path = ROOT/entry['path']
    protected.append({'path':entry['path'],'unchanged':hashlib.sha256(path.read_bytes()).hexdigest()==entry['sha256']})
changes = [x['path'] for x in protected if not x['unchanged']]
expected = ['MakanApp.Api/Errors/GlobalExceptionHandler.cs','MakanApp.Api/Program.cs']
assert sorted(changes)==expected, 'UNEXPECTED_PROTECTED_CHANGE'
patches = []
for relative, backup, calls in [
    ('MakanApp.Api/Program.cs','Program.cs',[
        '        ApiProblemDetailsContract.AddFrameworkCode(context);\n',
        'app.UseStatusCodePages(ApiProblemDetailsContract.WriteEmptyErrorAsync);\n']),
    ('MakanApp.Api/Errors/GlobalExceptionHandler.cs','GlobalExceptionHandler.cs',[
        '        ApiProblemDetailsContract.PreserveRetryAfter(httpContext, exception);\n'])]:
    before=(PRIVATE/'before-fix'/backup).read_text(encoding='utf-8-sig')
    after=(ROOT/relative).read_text(encoding='utf-8-sig')
    reconstructed=after
    for call in calls:
        assert reconstructed.count(call)==1
        reconstructed=reconstructed.replace(call,'')
    assert reconstructed==before, 'PREEXISTING_HUNK_CHANGED'
    patches.extend(difflib.unified_diff(before.splitlines(keepends=True),after.splitlines(keepends=True),fromfile='a/'+relative,tofile='b/'+relative))
before=subprocess.check_output(['git','show','HEAD:MakanApp.Api/Errors/ApiErrorCodes.cs'],cwd=ROOT).decode('utf-8-sig').replace('\r\n','\n')
after=(ROOT/'MakanApp.Api/Errors/ApiErrorCodes.cs').read_text(encoding='utf-8-sig')
patches.extend(difflib.unified_diff(before.splitlines(keepends=True),after.splitlines(keepends=True),fromfile='a/MakanApp.Api/Errors/ApiErrorCodes.cs',tofile='b/MakanApp.Api/Errors/ApiErrorCodes.cs'))
new_source=ROOT/'MakanApp.Api/Errors/ApiProblemDetailsContract.cs'
patches.extend(difflib.unified_diff([],new_source.read_text(encoding='utf-8-sig').splitlines(keepends=True),fromfile='/dev/null',tofile='b/MakanApp.Api/Errors/ApiProblemDetailsContract.cs'))
(REPORT/'repro/backend-only-fixes.patch').write_text(''.join(patches),encoding='utf-8')

# مقدارها فقط در حافظه و برای تشخیص نشت به کار می‌روند؛ خود secret یا متن match ذخیره نمی‌شود.
secrets=set()
for name in ['appsettings.json','appsettings.Development.json','appsettings.Test.json','appsettings.Production.json']:
    document=json.loads((ROOT/'MakanApp.Api'/name).read_text(encoding='utf-8-sig'))
    def inspect(value,key=''):
        if isinstance(value,dict):
            for k,v in value.items():inspect(v,k)
        elif isinstance(value,str):
            if re.search(r'(?i)(password|securitykey|servicetoken|signingkey|currentkey|previouskey)$',key) and len(value)>=12:
                secrets.add(value)
            for match in re.finditer(r'(?i)(?:Password|Pwd)\s*=\s*([^;]+)',value):
                if len(match[1])>=8:secrets.add(match[1])
    inspect(document)
findings=[]
for path in REPORT.rglob('*'):
    if not path.is_file() or path.suffix.lower() not in {'.json','.jsonl','.csv','.md','.txt','.py','.patch'}:continue
    content=path.read_text(encoding='utf-8-sig',errors='replace')
    if any(secret in content for secret in secrets):
        findings.append({'path':path.relative_to(REPORT).as_posix(),'kind':'PRIVATE_VALUE_MATCH'})
    if re.search(r'eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]+',content):
        findings.append({'path':path.relative_to(REPORT).as_posix(),'kind':'JWT_SHAPE'})
    if re.search(r'(?i)Bearer\s+[A-Za-z0-9_.-]{24,}',content):
        findings.append({'path':path.relative_to(REPORT).as_posix(),'kind':'BEARER_VALUE_SHAPE'})
assert not findings,'EVIDENCE_LEAK_DETECTED_VALUES_NEVER_PRINTED'
reference_manifest=json.loads((REPORT/'references/manifest-sha256.json').read_text(encoding='utf-8-sig'))
assert all(hashlib.sha256((REPORT/'references'/name).read_bytes()).hexdigest()==digest for name,digest in reference_manifest.items()), 'REFERENCE_MANIFEST_MISMATCH'
export= json.loads((REPORT/'evidence/public-contract/openapi.json').read_text(encoding='utf-8-sig'))
assert hashlib.sha256((REPORT/'evidence/public-contract/live-openapi.sanitized.json').read_bytes()).hexdigest()==export['sanitizedSha256'], 'SANITIZED_SPEC_HASH_MISMATCH'
rows=list(csv.DictReader((REPORT/'cases.csv').open(encoding='utf-8-sig')))
assert len(rows)==107 and len({x['id'] for x in rows})==107
frozen=json.loads((PRIVATE/'final-source.json').read_text())
source_changes=[p for p,h in frozen.items() if not (ROOT/p).is_file() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
assert not source_changes,'FINAL_SOURCE_CHANGED_AFTER_FREEZE'
result={'protectedCount':len(protected),'unchangedProtectedCount':len(protected)-2,
    'changedOnlyForConfirmedFix':changes,'preexistingHunksPreserved':True,
    'privateConfigurationAndAiUnchanged':True,'secretLeakage':False,'sensitiveMatches':0,
    'casesUnique':107,'finalFrozenSourceFiles':len(frozen),'finalSourceUnchanged':True,
    'headUnchanged':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()==baseline['head'],
    'stagedPaths':subprocess.check_output(['git','diff','--cached','--name-only'],cwd=ROOT,text=True).splitlines()}
(REPORT/'evidence/final-scope-and-privacy-review.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
