# Ready-to-copy Prompt — AI Team

```text
Implement and validate the MakanApp AI side of AI Integration Contract v1.

REPOSITORY OWNERSHIP

Writable repository only:
RastinaxAI/MakanApp-AI

You MUST NOT modify, commit, push, rebase, reset, stash, or configure:
RastinaxAI/MakanApp

The Backend repository is read-only reference material.

STARTING POINT

1. Fetch current AI main and verify the working tree is clean.
2. Record branch, HEAD, and origin/main.
3. Read the complete Backend handoff package:
   docs/ai-handoff/01_AI_INTEGRATION_OVERVIEW.md
   docs/ai-handoff/02_AI_SERVICE_AUTH_CONTRACT.md
   docs/ai-handoff/03_AI_API_CONTRACT.md
   docs/ai-handoff/04_AI_CONTEXT_AND_PRIVACY_RULES.md
   docs/ai-handoff/05_AI_ERROR_AND_RESILIENCE_CONTRACT.md
   docs/ai-handoff/06_AI_RAG_CONTRACT.md
   docs/ai-handoff/07_AI_TEST_ACCEPTANCE_MATRIX.md
   docs/ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md
   docs/contracts/AI_INTEGRATION_CONTRACT_V1.md

AUDITED AI BASELINE

The Backend team audited AI main at:
c80fdb90c2fe5101a7b3ff193a02d3120bf7e5f9

Current AI source remains authoritative. Preserve working behavior and make only changes required by the frozen v1 contract.

IMPLEMENTATION REQUIREMENTS

1. Preserve FastAPI/Pydantic Prompt/Provider architecture owned by the AI team.
2. Support exactly the 12 internal POST operations documented in 03_AI_API_CONTRACT.md.
3. Keep camelCase JSON, extra=forbid, strict role-specific request validation, and structured responses.
4. Protect every AI business route with the short-lived HS256 Backend service JWT.
5. Verify alg/typ/kid/signature, issuer, audience, expected subject, scope, iat/nbf/exp, maximum lifetime and jti.
6. Accept current and previous key during controlled rotation. Never log keys or tokens.
7. Add/configure expected subject validation for makan-backend; non-empty sub alone is insufficient.
8. Validate X-AI-Contract-Version exactly as v1 on all business routes.
9. When X-Correlation-ID is present, require it to match body correlationId; preserve exact requestId/correlationId in the response.
10. Keep health/readiness behavior separate from protected business routes.
11. Do not weaken Backend scope rules. AI validation is defense in depth and never grants business authorization.
12. Do not query Makan SQL Server and do not request database credentials.
13. Do not accept or infer UserSession access tokens from frontend/mobile.
14. Preserve privacy rules: no Direct Messenger history, Contacts, moderation evidence, OTP/token/phone/payment secret, platform audit or raw private Student AI history for Parent/Teacher/Manager.
15. Preserve version-specific maxScore semantics; never assume a global score of 20.
16. Keep provider failure/error envelopes safe and free of raw provider exceptions or sensitive payload data.
17. Do not implement or expose production RAG/knowledge ingestion or retrieval. RAG_BACKEND_SOURCE_LIFECYCLE_DEFERRED is authoritative. Existing internal RAG tests/code may remain internal and disconnected.
18. Do not modify the Backend contract to fit an unsafe AI shortcut. If a blocker exists, report it precisely.

TESTS AND ACCEPTANCE

Add/update tests for all requirements in 07_AI_TEST_ACCEPTANCE_MATRIX.md, including:
- service auth and rotation
- expected subject
- contract/correlation headers
- all role schemas and role/scope mismatch
- privacy/raw-conversation rejection
- provider unavailable/timeout/rate limit/invalid structured output
- requestId/correlationId echo
- OpenAPI route/schema audit
- no public RAG endpoints

Run the complete AI test suite. Inspect generated OpenAPI and compare all paths/models with 03_AI_API_CONTRACT.md. Do not claim success for zero discovered tests.

GIT

Review git status and complete diff. Ensure no secrets, local env files, generated caches, Backend files or unrelated changes are staged. Commit and push to the current tracked AI branch. Never force-push or rewrite history.

FINAL REPORT

Return:
- starting AI commit
- final AI commit hash
- branch and remote push result
- files changed
- service-auth changes
- API/schema/OpenAPI impact
- exact test command and passed/failed/skipped counts
- remaining blockers
- confirmation that Backend was not modified
- whether Backend may execute STEP 10G

End the report exactly with one of these lines:

GO_FOR_BACKEND_STEP_10G

or

BLOCKED_BEFORE_BACKEND_STEP_10G
```
