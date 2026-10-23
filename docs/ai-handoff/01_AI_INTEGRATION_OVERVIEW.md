# نمای کلی تحویل Backend به تیم AI

## وضعیت جاری در 2026-10-23

این handoff اولیهٔ ۲۰۲۶-۱۰-۱۳ از نظر قرارداد v1 و مالکیت معتبر است؛ لیست نیازهای تیم AI در پایین، backlog تاریخی همان ممیزی است و وضعیت امروز آن از [Consolidation](../release/BACKEND_FINAL_CONSOLIDATION_FA.md) و [شاخص AI](08_AI_BACKEND_IMPLEMENTATION_STATUS.md) خوانده می‌شود. در `main@5885d11`، client سخت‌گیرانه، grounding، telemetry امن و probe رسمی opt-in وجود دارند. Service Auth واقعی قبلاً PASS شده؛ Parent Provider Timeout و Full-12 واقعی Deferred است. RAG source lifecycle بسته نشده و هیچ شبکه/Provider واقعی در همگام‌سازی اسناد اجرا نمی‌شود.

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

تاریخ: 2026-10-13

## نسخه‌ها و baseline

- Backend starting commit: `c878b000035f8bc1753b086e3b6f66cd78d274fd`
- Backend implementation commit before handoff docs: `9bc412189c0cf892d81d9801dd3e88bcf078d5f5`
- Backend final handoff commit: commit حاوی این بسته با پیام `docs(ai): prepare AI team integration handoff`؛ hash آن در گزارش اجرای Backend اعلام می‌شود.
- AI audited branch/commit: `main` / `c80fdb90c2fe5101a7b3ff193a02d3120bf7e5f9`
- Frozen contract version: `v1`

## معماری

```text
Web/Mobile
  -> Makan Backend user authentication/session
  -> Backend AccessContext + relationship authorization
  -> Backend minimum educational projection
  -> HTTPS + short-lived service JWT
  -> Makan AI FastAPI
  -> Prompt/Provider/structured response
  -> Backend strict response validation
  -> Web/Mobile
```

Frontend هیچ‌گاه AI private URL، service credential یا provider key را دریافت نمی‌کند. AI هیچ credential مربوط به SQL Server دریافت نمی‌کند و مستقیماً به دیتابیس Makan وصل نمی‌شود.

## مالکیت دائمی

Backend مالک user authentication، session، organization/membership/role، GuardianRelation، TeacherAssignment، Enrollment، authorization، SQL read، minimum context، public API، service credential creation، HTTP adapter، timeout/circuit و public error mapping است.

AI مالک FastAPI، verification سرویس، Pydantic schema، prompt/policy، LLM provider، structured generation، AI-side telemetry/tests و RAG/vector internals است.

## آنچه Backend تکمیل کرده است

- قرارداد frozen و mapping شناسه‌ها.
- JWT داخلی کوتاه‌عمر HS256 برای هویت سرویس Backend.
- `IAiServiceClient` و `AiServiceClient` با ۱۲ عملیات، timeout، circuit breaker و پاسخ strict.
- Generic Chat با authorization و rate limit اختصاصی.
- هفت Context Builder برای Student، Parent، Teacher و Manager.
- ۱۱ endpoint نقش‌محور و projection محدود داده منتشرشده/release‌شده.
- تست قرارداد، privacy، cross-scope و Fake-AI E2E.
- ممیزی RAG؛ source lifecycle به دلیل نبود تصمیم Product/Domain به‌صورت رسمی deferred شده است.

## مسئولیت فعلی تیم AI

تیم AI باید current `main` را با این بسته مقایسه و فقط gapها را در مخزن `RastinaxAI/MakanApp-AI` رفع کند. routeها و Pydantic modelهای اصلی در commit ممیزی‌شده عمدتاً با v1 سازگارند، ولی acceptance نهایی حداقل نیازمند این موارد است:

1. بررسی equality مقدار `sub` با service subject مورد انتظار، نه فقط non-empty بودن آن.
2. اعتبارسنجی `X-AI-Contract-Version: v1` روی تمام business routeهای AI.
3. بررسی سازگاری `X-Correlation-ID` با `correlationId` body در صورت وجود header و حفظ echo دقیق در response.
4. اجرای همه تست‌های service auth، schema، role/scope، provider failure و OpenAPI.
5. پیکربندی secret واقعی فقط در secret store/environment محیط اجرا؛ هیچ secret نباید commit شود.

Production RAG ingestion یا retrieval تا تصویب source lifecycle سمت Backend مجاز نیست.

## gate مرحله بعد

Backend STEP 10G فقط پس از دریافت commit نهایی تیم AI و عبارت `GO_FOR_BACKEND_STEP_10G` اجرا می‌شود.
