# curl examples — real Controller contracts, without fake fixtures

**اجرای عمومی امن:** `curl/safe-health.sh` یا `curl/safe-health.ps1`. Secret روی command line یا تاریخچهٔ shell ننویسید؛ برای OTP و Refresh از Postman Environment با متغیر Secret و TLS معتبر استفاده کنید.

## GET profile

```bash
# Only on your authorized HTTPS test environment.
curl --silent --show-error --fail-with-body \
  -H "Authorization: Bearer ${MAKAN_ACCESS_TOKEN}" \
  -H "Accept: application/json" \
  "${MAKAN_BASE_URL}/api/v1/me"
```

## GET available workspaces

```bash
curl --silent --show-error --fail-with-body \
  -H "Authorization: Bearer ${MAKAN_ACCESS_TOKEN}" \
  "${MAKAN_BASE_URL}/api/v1/workspaces/me"
```

## OTP challenge — REAL SMS, opt-in, requires test phone

```bash
# WARNING: sends a real OTP/SMS if provider is live.
# Run only when authorized and using HTTPS; phone must come from an approved test fixture.
printf '%s' '{"phoneNumber":"<APPROVED_TEST_PHONE>"}' |
curl --silent --show-error \
  -H "Content-Type: application/json" \
  -X POST --data-binary @- \
  "${MAKAN_BASE_URL}/api/v1/auth/otp/challenges"
```

## OTP verify — input Contract

```json
{"challengeId":"<CHALLENGE_ID_FROM_RESPONSE>","phoneNumber":"<SAME_TEST_PHONE>","code":"<RECEIVED_CODE>","deviceName":"manual-acceptance"}
```

Call `POST /api/v1/auth/otp/verify` in Postman. Do not paste a real received code/token in logs, code blocks, tickets or chats.

## Create order — ONLY authorized isolated test data

```json
{"planVersionId":"<REAL_PURCHASABLE_PLAN_VERSION_ID>","clientOperationId":"<NEW_UUID>"}
```

Request `POST /api/v1/commerce/orders`: first creation `201`, exact replay `200` with `replayed=true`; collision on same `clientOperationId` with different payload `409`. A payment gateway is not implied.

## File upload — actual multipart field name

```bash
# Must have MAKAN_BASE_URL HTTPS and MAKAN_ACCESS_TOKEN assigned securely.
curl --silent --show-error \
  -H "Authorization: Bearer ${MAKAN_ACCESS_TOKEN}" \
  -F 'File=@./small-approved-test.txt;type=text/plain' \
  "${MAKAN_BASE_URL}/api/v1/files"
```

## Messaging — body scaffold from SendMessageCommand

```json
{"clientMessageId":"<NEW_UUID>","text":"manual-acceptance-test","isSilent":false}
```

Send `POST /api/v1/conversations/{conversationId}/messages` in Postman only using actual conversation membership and authorization, then retry the **same** idempotency ID on a test conversation and inspect history for duplicates.

**توجه:** چند نمونهٔ بالا برای Copy/Review هستند. اجرای آنها به Authorization، پیش‌نیاز محیط و دادهٔ واقعی وابسته است؛ هیچ موفقیتی از پیش تضمین نشده است.
