"""حسابداری Caseها مستقل از پذیرش زنده است؛ این ابزار هیچ درخواست شبکه‌ای نمی‌فرستد."""
import csv
import hashlib
import json
from collections import Counter
from pathlib import Path

REPORT = Path(__file__).resolve().parent.parent
ROOT = REPORT.parents[2]


def rows(path):
    with path.open(encoding='utf-8-sig', newline='') as stream:
        return list(csv.DictReader(stream))


cases = rows(REPORT / 'cases.csv')
template = rows(REPORT / 'references/cases-template.csv')
assert len(cases) == len(template) == 107, 'CASE_COUNT_MISMATCH'
assert len({case['id'] for case in cases}) == 107, 'CASE_ID_DUPLICATED'
expected = {case['id']: case for case in template}
for case in cases:
    for key in ['id', 'group', 'title', 'steps', 'invariant']:
        assert case[key] == expected[case['id']][key], 'BASE_CASE_CHANGED'
    assert case['status'] in {'PASS', 'FAIL', 'BLOCKED', 'NOT_APPLICABLE'}, 'UNACCOUNTED_CASE'
    assert case['reason'].strip(), 'CASE_REASON_MISSING'
    assert case['evidencePaths'].strip(), 'CASE_EVIDENCE_MISSING'
    # نشانی متد تست ممکن است پس از سمی‌کالن آمده باشد؛ مسیر فایل/پوشهٔ شاهد باید موجود باشد.
    paths = [item.strip() for item in case['evidencePaths'].split(';')]
    assert any((REPORT / path).exists() or (ROOT / path).exists() for path in paths), 'EVIDENCE_PATH_MISSING'

counts = dict(Counter(case['status'] for case in cases))
assert counts == {'PASS': 14, 'FAIL': 1, 'BLOCKED': 89, 'NOT_APPLICABLE': 3}, 'CASE_STATUS_CHANGED'
reference_manifest = json.loads((REPORT / 'references/manifest-sha256.json').read_text(encoding='utf-8-sig'))
for name, digest in reference_manifest.items():
    assert hashlib.sha256((REPORT / 'references' / name).read_bytes()).hexdigest() == digest, 'REFERENCE_HASH_MISMATCH'

required = ['REPORT_FA.md', 'BACKEND_TICKETS_FA.md', 'FRONTEND_AI_FINDINGS_FA.md',
            'FRONTEND_ERROR_HANDOFF_FA.md', 'action-catalog.json', 'api-contract-map.json',
            'api-error-ledger.json', 'cases.csv', 'attempts.jsonl', 'errors.csv', 'coverage.md',
            'evidence', 'repro', 'RESUME_FA.md', 'FINAL_STATUS.txt']
assert all((REPORT / name).exists() for name in required), 'REQUIRED_ARTIFACT_MISSING'

print(json.dumps({
    'BASE_CASES': 107, 'CASES_ACCOUNTED_FOR': len(cases), 'UNACCOUNTED_CASES': 0,
    'CASES_PASS': counts['PASS'], 'CASES_FAIL': counts['FAIL'],
    'CASES_BLOCKED': counts['BLOCKED'], 'CASES_NOT_APPLICABLE': counts['NOT_APPLICABLE'],
    'BLOCKED_REASON_MISSING': 0, 'FAIL_WITHOUT_EVIDENCE': 0,
    'MANIFEST_HASH_VERIFICATION': 'PASS', 'AUDIT_COMPLETE': 'YES',
    'LIVE_ACCEPTANCE_COMPLETE': 'NO', 'LIVE_FAILURE_COUNT': 1,
    'LIVE_FAILURE_STATUS': 'SUPPORTED',
    'RUNTIME_DEADLINE_ROOT_CAUSE': 'AWAITING_EVIDENCE',
    'NETWORK_REQUESTS': 0
}, indent=2))
