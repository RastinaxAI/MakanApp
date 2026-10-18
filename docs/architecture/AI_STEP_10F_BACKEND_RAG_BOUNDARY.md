# مرز Backend برای Knowledge و RAG در STEP 10F-BE

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/شاهد موضوعی AI است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

تاریخ بازبینی: 2026-10-13

نسخه قرارداد: `v1`

وضعیت: `RAG_BACKEND_SOURCE_LIFECYCLE_DEFERRED`

## نتیجه

پیاده‌سازی runtime برای ingestion، update، revoke یا delete منبع RAG در این مرحله عمداً انجام نشد. مدل فعلی Backend فقط lifecycle ذخیره‌سازی یک blob را تعریف می‌کند و هیچ مدل محصولی authoritative برای Knowledge Source ندارد. ساختن چنین مدلی بدون تصمیم Product/Domain می‌توانست فایل خصوصی را به‌اشتباه قابل index یا retrieval کند.

قاعده قطعی این مرحله:

```text
FileAsset != Knowledge Source
```

`FileAssetStatus.Ready` فقط موفقیت ذخیره و اعتبار پایه محتوای فایل را نشان می‌دهد؛ این وضعیت مجوز انتشار، audience، قابلیت استفاده آموزشی، نسخه فعال، supersession یا اجازه ورود به RAG نیست.

## شواهد Backend

### FileAsset و Storage

`MakanApp.Domain/Storage/FileAsset.cs` فقط این lifecycle را دارد:

- `Pending`
- `Ready`
- `Rejected`
- `Deleted`

metadata موجود شامل uploader، organization اختیاری، نام، storage key، content type، اندازه، SHA-256، زمان‌ها و `rowversion` است. هیچ‌یک از فیلدهای زیر وجود ندارند:

- source category مورد تأیید Product
- publication state
- retrieval audience
- class visibility
- source version مستقل
- superseded source
- knowledge revocation
- index synchronization state
- deletion propagation ownership

`StorageService` و `IFileAssetBoundAccessResolver` دسترسی upload/download/delete فایل را مدیریت می‌کنند؛ این قراردادها به فایل اجازه Knowledge ingestion نمی‌دهند.

### bindingهای موجود

bindingهای واقعی فایل در کد جاری عبارت‌اند از:

- `MessageAttachment`: پیوست پیام و بالقوه شامل Direct Message خصوصی.
- `ModerationEvidenceAttachment`: مدرک حساس moderation.
- `SubmissionAttachment`: پاسخ خصوصی دانش‌آموز برای تکلیف.
- `ReportExportJob.OutputFileAssetId`: خروجی خصوصی گزارش برای درخواست‌کننده مجاز.

در مدل‌های جاری Assignment، Exam، Organization و Class هیچ binding عمومی و منتشرشده‌ای که semantics منبع Knowledge را تعریف کند یافت نشد. وجود `OrganizationId` روی `FileAsset` نیز به‌تنهایی audience یا مجوز retrieval ایجاد نمی‌کند.

## منابع ممنوع

این دسته‌ها نباید به‌صورت خودکار index شوند:

- پیوست Direct Message یا هر history خصوصی Messenger
- مدرک moderation یا child-safety
- submission خصوصی دانش‌آموز
- مکالمه خصوصی AI
- فایل مالی، پرداخت یا reconciliation
- artifact خصوصی platform audit
- خروجی گزارش شخصی یا role-scoped
- هر `FileAsset` صرفاً به دلیل `Ready` بودن

## شواهد مخزن AI

مخزن AI در commit ممیزی‌شده `c80fdb90c2fe5101a7b3ff193a02d3120bf7e5f9` دارای قراردادها و سرویس‌های داخلی مانند `KnowledgeDocument`، `KnowledgeScope`، `KnowledgeDocumentManifest` و `KnowledgeRagLifecycleService` است، اما:

- هیچ route عمومی FastAPI برای knowledge/RAG/ingest ندارد.
- storageهای فعلی production persistence نیستند.
- اتصال به Backend، SQL Server یا FileAsset وجود ندارد.
- خود مستند AI تصریح می‌کند authorization و source ownership باید بیرون از RAG داخلی حل شود.
- scope داخلی AI مجوز کسب‌وکاری ایجاد نمی‌کند.

بنابراین internal lifecycle سمت AI جایگزین source lifecycle سمت Backend نیست.

## تصمیم معماری

Backend همچنان authority نهایی برای indexability و retrieval scope است. AI فقط پس از دریافت یک قرارداد صریح و مجاز از Backend می‌تواند پردازش و retrieval انجام دهد. در این مرحله موارد زیر عمداً ایجاد نشدند:

- Domain entity یا جدول `KnowledgeSource`
- migration
- endpoint مدیریت منبع
- Outbox event برای ingestion/revoke/delete
- DTO یا متد جدید در `IAiServiceClient`
- production embedding/vector provider
- تست ساختگی برای رفتاری که Domain آن هنوز تصویب نشده است

## تصمیم‌های لازم برای رفع defer

پیش از پیاده‌سازی runtime باید Product/Domain حداقل این موارد را تصویب کند:

1. دسته‌های مجاز source و مالک هر دسته.
2. actorهای مجاز برای publish، update، revoke و delete.
3. audience دقیق: organization، class، role و در صورت نیاز student.
4. تفاوت draft، published، revoked، superseded و deleted.
5. هویت پایدار source و قاعده version/content hash.
6. رفتار تغییر scope یا انتقال کلاس.
7. propagation حذف و SLA خارج‌کردن داده از index.
8. retention، audit و recovery policy.
9. transaction/outbox و idempotency میان SQL Server و AI service.
10. رفتار retrieval هنگام pending، failed یا stale بودن index.

پس از تصویب این تصمیم‌ها، Backend می‌تواند یک مدل Domain واقعی و قرارداد حداقلی مانند source identity، type، organization/class scope، version، content hash، visibility و revoked/deleted state بسازد و با Outbox، Fake-AI contract tests و کنترل cross-organization آن را پیاده‌سازی کند.

## وضعیت نهایی STEP 10F-BE

```text
RAG_BACKEND_SOURCE_LIFECYCLE_DEFERRED
```

این defer مانع تکمیل بخش‌های Backend مربوط به service authentication، HTTP adapter، Generic Chat و role-specific context نیست. بسته handoff باید این dependency را صریحاً به تیم AI منتقل کند و تیم AI نیز نباید بدون قرارداد source از FileAssetها ingestion تولیدی انجام دهد.
