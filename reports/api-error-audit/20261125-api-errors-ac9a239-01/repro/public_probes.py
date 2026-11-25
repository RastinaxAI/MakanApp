"""درخواست‌های عمومی و بدون credential؛ هیچ فرم ورود یا تغییر داده‌ای ارسال نمی‌شود."""
import json
import re
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

REPORT = Path.cwd() / 'reports/api-error-audit/20261125-api-errors-ac9a239-01'
OUT = REPORT / 'evidence/public-probes'
OUT.mkdir(exist_ok=True)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


opener = urllib.request.build_opener(NoRedirect())
api = 'http://94.184.32.84:8090'
web = 'https://testapp.makanapp.org'
probes = [
    ('missing-route', 'C03', 'GET', api + '/api/v1/audit-public-resource-does-not-exist', None, None),
    ('method', 'C08', 'GET', api + '/api/v1/ai/chat', None, None),
    ('ai-anonymous', 'C06', 'POST', api + '/api/v1/ai/chat', b'{}', None),
    ('source-anonymous', 'C06', 'POST', api + '/api/v1/ai/sources/preflight', b'{}', None),
    ('health', 'S11', 'GET', api + '/health/live', None, None),
    ('ready', 'S11', 'GET', api + '/health/ready', None, None),
    ('cors-preflight', 'N05', 'OPTIONS', api + '/api/v1/ai/chat', None,
        {'Origin': web, 'Access-Control-Request-Method': 'POST', 'Access-Control-Request-Headers': 'content-type'}),
    ('bff-health', 'N10', 'GET', web + '/api/client/backend/health/live', None, None),
    ('bff-ai-anonymous', 'C06', 'POST', web + '/api/client/backend/api/v1/ai/chat', b'{}', None),
    ('bff-ai-with-origin', 'N05', 'POST', web + '/api/client/backend/api/v1/ai/chat', b'{}', {'Origin':web}),
    ('enter', 'E01', 'GET', web + '/enter', None, None),
]
for sequence, (variant, case, method, url, payload, extra) in enumerate(probes, 1):
    target = OUT / (variant + '.json')
    if target.exists():
        print('PRESERVED_VARIANT=' + variant, flush=True)
        continue
    headers = {'Accept': 'application/json', 'X-Correlation-ID': 'audit-public-' + variant}
    if payload is not None: headers['Content-Type'] = 'application/json'
    headers.update(extra or {})
    event = dict(runId='20261125-api-errors-ac9a239-01', caseId=case, variantId=variant,
        evidenceKind='REAL_API', attempt=1, repetition=1, requestSequence=sequence,
        interactionId='public-' + variant, startedAtUtc=datetime.now(timezone.utc).isoformat(),
        method=method, endpoint=url, credentialsSent=False, requestHeaders=headers,
        requestPayload={} if payload is not None else None, retryCount=0, retrySource='NONE', backoffMs=0,
        stateBefore='ANONYMOUS_NO_FIXTURE', userExperienceResult='NOT_RUN_NO_BROWSER',
        expected='فقط مشاهدهٔ پاسخ عمومی؛ هیچ تغییر داده یا ورود انجام نشود.')
    started = time.perf_counter()
    try:
        try:
            response = opener.open(urllib.request.Request(url, data=payload, headers=headers, method=method), timeout=18)
        except urllib.error.HTTPError as exc:
            response = exc
        with response:
            raw = response.read(128_000)
            safe_headers = {k: v for k,v in response.headers.items() if k.lower() in {
                'content-type','x-correlation-id','x-request-id','allow','retry-after','access-control-allow-origin',
                'access-control-allow-methods','access-control-allow-headers','access-control-expose-headers'}}
            body = {}
            try:
                parsed = json.loads(raw)
                if isinstance(parsed, dict):
                    for key in ['status','code','traceId','correlationId','requestId']:
                        value = parsed.get(key)
                        if value is None or isinstance(value, (str, int)):
                            body[key] = value
                    if isinstance(parsed.get('errors'), dict): body['errorFields'] = list(parsed['errors'])
            except (ValueError, UnicodeDecodeError):
                pass
            location = response.headers.get('Location')
            if location:
                resolved = urllib.parse.urlsplit(urllib.parse.urljoin(url, location))
                event['redirectTarget'] = urllib.parse.urlunsplit((resolved.scheme, resolved.netloc, resolved.path, '', ''))
            event.update(httpStatus=response.status, responseHeaders=safe_headers, responsePayload=body,
                businessCode=body.get('code'), traceId=body.get('traceId'), correlationId=body.get('correlationId') or response.headers.get('X-Correlation-ID'),
                transportResult='HTTP_RESPONSE', businessResult=body.get('code') or 'NOT_EVALUATED',
                stateAfter='PUBLIC_RESPONSE_NO_AUTH_OR_MUTATION', verdict='OBSERVED', causeConfidence='OBSERVED_ONLY')
    except Exception as exc:
        reason = getattr(exc, 'reason', None)
        event.update(httpStatus=None, transportError=type(reason if isinstance(reason, Exception) else exc).__name__,
            transportResult='FAILED', businessResult='NOT_REACHED', stateAfter='NO_HTTP_RESPONSE', verdict='BLOCKED', causeConfidence='UNKNOWN')
    event['elapsedMs'] = round((time.perf_counter()-started)*1000, 2)
    target.write_text(json.dumps(event, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k:event.get(k) for k in ['variantId','httpStatus','businessCode','transportError','elapsedMs']}), flush=True)
