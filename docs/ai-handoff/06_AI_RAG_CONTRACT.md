# وضعیت قرارداد RAG

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

وضعیت رسمی:

```text
RAG_BACKEND_SOURCE_LIFECYCLE_DEFERRED
```

## علت دقیق

Backend فعلی `FileAsset` و blob lifecycle دارد، ولی Product/Domain هنوز Knowledge Source را تعریف نکرده است. `FileAssetStatus.Ready` publication، audience، class visibility، version، supersession، revoke یا delete propagation را ثابت نمی‌کند.

bindingهای فعلی فایل شامل داده‌های خصوصی مانند `MessageAttachment`، `ModerationEvidenceAttachment`، `SubmissionAttachment` و report export هستند. بنابراین auto-index بر اساس FileAsset یا OrganizationId ناامن است.

## قاعده فعلی تیم AI

- هیچ ingestion یا retrieval تولیدی از FileAsset انجام نشود.
- هیچ route عمومی knowledge/RAG برای Backend اضافه نشود.
- Messenger Direct، moderation evidence، submission خصوصی، AI chat خصوصی، payment/finance و platform audit هرگز خودکار index نشوند.
- `KnowledgeScope` داخلی AI authorization کسب‌وکاری ایجاد نمی‌کند.
- internal/in-memory RAG code می‌تواند برای تست داخلی باقی بماند، اما به routeهای production متصل نشود.

## dependency رفع defer

Product/Domain باید source type، owner، publish/revoke/delete actor، audience، version/hash، supersession، retention، deletion SLA، Outbox/idempotency و stale-index behavior را تصویب کند. سپس Backend مدل authoritative و authorized events/contracts را می‌سازد.

انتخاب production embedding provider و Vector DB نیز تصمیم AI/infrastructure بعدی است و در این handoff تصویب نشده است.

جزئیات audit در `docs/architecture/AI_STEP_10F_BACKEND_RAG_BOUNDARY.md` قرار دارد.
