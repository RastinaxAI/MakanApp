# ماتریس پذیرش تست AI Integration

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## شواهد Backend

| حوزه | تست/شاهد | نتیجه Backend |
|---|---|---|
| service JWT | `HmacAiServiceCredentialProviderTests` | PASS |
| HTTP adapter، status، timeout، malformed response، circuit | `AiServiceClientTests` | PASS |
| strict camelCase payload و forbidden field | `AiRoleContractTests` | PASS |
| role resolution و Generic Chat | `AiChatServiceTests` | PASS |
| Student/Parent/Teacher/Manager authorization | `AiRoleContextBuilderTests` | PASS |
| public endpoints، cross-org، revoked/suspended | `AiChatEndpointsTests` | PASS |
| واقعی‌ترین مسیر مستقل Backend تا HTTP fake | `ManagerSchoolSummary_EndToEnd_UsesHmacCredentialHttpAdapterAndFakeAi` | PASS |

اجرای focused نهایی:

- AI Unit: 51 passed، 0 failed، 0 skipped.
- AI Integration: 17 passed، 0 failed، 0 skipped.
- Fake-AI E2E مستقل: 1 passed، 0 failed، 0 skipped؛ این تست جزئی از 17 تست Integration است.

کل solution: 370 Unit + 567 Integration + 14 Architecture = 951 passed، 0 failed، 0 skipped.

## acceptance الزامی تیم AI

تیم AI باید حداقل این تست‌ها را در مخزن خودش داشته و اجرا کند:

1. missing/malformed/expired token، bad signature، unknown `kid`، issuer/audience/subject اشتباه.
2. current و previous key؛ حذف previous پس از rotation.
3. scope غایب -> 403 و credential نامعتبر -> 401.
4. contract header غایب/نامعتبر و correlation header/body mismatch.
5. unknown JSON field و schema/type/enum نامعتبر برای هر ۱۲ operation.
6. Student role و StudentScope consistency.
7. Parent role و rejection داده raw conversation.
8. Teacher role، class/student ID consistency و privacy.
9. Manager role، school/class/student ID consistency و privacy.
10. role mismatch و scope mismatch.
11. requestId/correlationId echo دقیق.
12. provider unavailable، timeout، rate limit و structured response invalid.
13. OpenAPI شامل فقط routeهای v1 مورد انتظار و بدون route عمومی RAG/knowledge.
14. نبود SQL Server client/credential/query در runtime AI.
15. عدم log کردن service token، key، prompt/context خام یا PII.

RAG production acceptance در این تحویل `N/A / DEFERRED` است؛ تست‌های internal RAG نباید به معنای production integration گزارش شوند.
