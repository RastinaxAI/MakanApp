"""تجمیع شواهد پالایش‌شده؛ stdout، توکن یا payload خام خوانده/ذخیره نمی‌شود."""
import csv
import json
from datetime import datetime, timedelta
from collections import Counter
from pathlib import Path

REPORT = Path.cwd() / 'reports/api-error-audit/20261125-api-errors-ac9a239-01'
attempts = []
for path in sorted((REPORT/'evidence/public-probes').glob('*.json')):
    item = json.loads(path.read_text()); item['evidencePath'] = path.relative_to(REPORT).as_posix(); attempts.append(item)
for path in sorted((REPORT/'evidence/public-contract').glob('*.json')):
    if path.name in {'index.json','live-openapi.sanitized.json'}: continue
    item = json.loads(path.read_text()); item.update(phase='A', purpose='PUBLIC_CONTRACT_DISCOVERY', caseId=None,
        evidencePath=path.relative_to(REPORT).as_posix(), requestSequence=1, attempt=1, repetition=1); attempts.append(item)
for folder in sorted((REPORT/'evidence/local-attempts').glob('*')):
    # اجراهای اولیهٔ ساخت fixture محفوظ‌اند اما در شمارش معتبر audit ادغام نمی‌شوند.
    if folder.name in {'phase-a-http-02','phase-a-http-03'}: continue
    for path in sorted(folder.glob('*.json')):
        item = json.loads(path.read_text()); item.update(phase='A' if folder.name.startswith('phase-a') else 'B',
            evidencePath=path.relative_to(REPORT).as_posix())
        if 'timeline' in item:
            # این فایل خلاصهٔ interaction است؛ پاسخ‌های مشاهده‌شده جدا می‌شوند تا با تعداد request یکی فرض نشود.
            for stage in item['timeline']:
                if 'httpStatus' not in stage: continue
                sequence = {'SSE_DONE':1,'DUPLICATE_WHILE_PENDING':2,'COMPLETED_REPLAY':3,'HEALTHY_NEW_INTENT_AFTER':4}[stage['stage']]
                event = {k:v for k,v in item.items() if k not in {'timeline','httpStatus','businessCode','elapsedMs','backoffMs'}}
                event.update(recordType='OBSERVED_RESPONSE_WITH_PARTIAL_REQUEST_TIMING',
                    caseId='I04' if stage['stage']=='COMPLETED_REPLAY' else item['caseId'],
                    sourceInteractionCaseId=item['caseId'], attempt=sequence, requestSequence=sequence,
                    scenarioStartedAtUtc=item['startedAtUtc'], startedAtUtc=item['startedAtUtc'] if sequence==1 else None,
                    completedAtUtc=(datetime.fromisoformat(item['startedAtUtc'])+timedelta(milliseconds=stage['elapsedMs'])).isoformat(),
                    timePrecision='SCENARIO_MONOTONIC_ELAPSED_REQUEST_START_NOT_CAPTURED_FOR_DUPLICATE_REPLAY',
                    elapsedFromScenarioStartMs=stage['elapsedMs'], httpStatus=stage['httpStatus'],
                    businessCode=stage.get('code'), businessResult=stage.get('code') or 'SUCCESS_VALIDATED',
                    responsePayload={'stage':stage['stage'],'status':stage['httpStatus'],'code':stage.get('code')},
                    userAlias='fixture-personal-01', fixtureAliasScope=item['variantId']+'-'+str(item['repetition']),
                    configuredObservationDelayMs=item['backoffMs'], backoffMs=0,
                    correlationId=None,traceId=None,requestId=None,
                    traceCapture='NOT_EXPORTED_BY_EXISTING_RACE_HELPER_NO_IDENTIFIER_INVENTED')
                attempts.append(event)
        else:
            attempts.append(item)
browser_path = REPORT/'evidence/public-browser.json'
if browser_path.exists():
    item = json.loads(browser_path.read_text()); item.update(caseId=None, purpose='PUBLIC_LOGIN_INSPECTION',
        evidencePath=browser_path.relative_to(REPORT).as_posix(), httpStatus=None); attempts.append(item)
for item in attempts:
    item.setdefault('elapsedFromScenarioStartMs', item.get('elapsedMs'))
    item.setdefault('transportResult', 'OBSERVED'); item.setdefault('businessResult', 'NOT_EVALUATED')
    item.setdefault('userExperienceResult', 'NOT_RUN_NO_AUTHENTICATED_BROWSER')
    item.setdefault('retryCount', 0); item.setdefault('retrySource', 'NONE'); item.setdefault('backoffMs', 0)
    item.setdefault('stateBefore', 'ANONYMOUS_PUBLIC_READ'); item.setdefault('stateAfter', 'OBSERVED_NO_MUTATION')
    item.setdefault('rootCause', 'NOT_ASSIGNED_OR_SEE_ISSUES_LEDGER')
with (REPORT/'attempts.jsonl').open('w', encoding='utf-8') as stream:
    for item in attempts: stream.write(json.dumps(item, ensure_ascii=False)+'\n')
fields = ['runId','caseId','variantId','attempt','evidenceKind','method','endpoint','httpStatus','businessCode',
    'transportError','expected','verdict','causeConfidence','evidencePath']
with (REPORT/'errors.csv').open('w', encoding='utf-8-sig', newline='') as stream:
    writer=csv.DictWriter(stream, fieldnames=fields); writer.writeheader()
    for item in attempts:
        if (item.get('httpStatus') or 0) >= 400 or item.get('transportError'):
            writer.writerow({key: json.dumps(item.get(key), ensure_ascii=False) if isinstance(item.get(key),(dict,list)) else item.get(key) for key in fields})
summary = {'attemptsTotal': len(attempts), 'evidenceKinds': dict(Counter(x['evidenceKind'] for x in attempts)),
    'http4xx': sum(400 <= (x.get('httpStatus') or 0) <= 499 for x in attempts),
    'http5xx': sum((x.get('httpStatus') or 0) >= 500 for x in attempts),
    'transportErrors': sum(bool(x.get('transportError')) for x in attempts),
    'businessErrorResponses': sum(bool(x.get('businessCode')) and (x.get('httpStatus') or 0)>=400 for x in attempts),
    'localFailedUpstream200':sum(x.get('httpStatus')==200 and x.get('businessResult')=='REJECTED_NOT_SUCCESS' for x in attempts),
    'partialRequestTimingRecords':sum(x.get('recordType')=='OBSERVED_RESPONSE_WITH_PARTIAL_REQUEST_TIMING' for x in attempts),
    'fixtureSetupAuthRequestsIncluded':False,
    'excludedHarnessVersions': ['phase-a-http-02: نگاشت Case اولیه ناقص؛ شواهد محفوظ.',
        'phase-a-http-03: RateLimiter نامعتبرِ fixture؛ Issue محصول نیست.'],
    'interpretation': 'تعداد attempt برابر تعداد Case یا تعداد تست نیست؛ rawOutputPersisted=false.'}
(REPORT/'evidence/attempt-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(summary, ensure_ascii=False))
