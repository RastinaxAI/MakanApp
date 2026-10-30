# قرارداد Frontend منابع آموزشی

قرارداد جدید [انتخاب Source برای AI](AI_SOURCE_SELECTION_FRONTEND_HANDOFF_FA.md) فیلدهای additiveِ `aiReadiness/aiUnavailableReason`، Preflight مجوز و مرز RetrievalUnavailable را توضیح می‌دهد. این تغییر `aiReady` یا `canUseForAi` را true نمی‌کند.

این سند فقط APIهای پیاده‌سازی‌شدهٔ Backend را توصیف می‌کند؛ frontend در این مرحله تغییر نکرده است. همهٔ مسیرها authenticated هستند. منبع با فایل تفاوت دارد: upload موفق، به معنی منبع Published یا AI-ready نیست.

## ترتیب ساخت

1. `POST /api/v1/files` با multipart فعلی؛ Personal/Library با query `workspaceScope=Personal`، و Class/Organization در workspace سازمانی مجاز.
2. `POST /api/v1/sources` با FileAssetId دریافتی و ClientOperationId ثابت برای retry همان Create.
3. پاسخ Source را برای readiness و capabilities بررسی کنید.
4. برای انتشار کلاس/سازمان، Publish صریح با ExpectedVersion لازم است. صرف upload/Create، مجوز Student نیست.

## Routeهای واقعی

| Method | Route | Request / نتیجه |
|---|---|---|
| GET | `/api/v1/sources` | SourceCatalogQuery؛ SourcePage |
| POST | `/api/v1/sources` | CreateEducationalSourceCommand؛ 201 یا replay با 200 |
| GET | `/api/v1/sources/{id}?revision={n}` | EducationalSourceResult؛ revision اختیاری |
| GET | `/api/v1/sources/{id}/processing?revision={n}` | همان نتیجهٔ وضعیت؛ بدون write |
| PATCH | `/api/v1/sources/{id}/draft` | UpdateSourceDraftCommand؛ فقط Draft جاری |
| POST | `/api/v1/sources/{id}/versions` | CreateSourceVersionCommand؛ 201، هویت Source ثابت |
| POST | `/api/v1/sources/{id}/publish` | SourceMutationCommand؛ 200 |
| POST | `/api/v1/sources/{id}/archive` | SourceMutationCommand؛ 200 |
| PUT | `/api/v1/sources/{id}/visibility` | SourceVisibilityCommand؛ فقط Library manager |
| GET | `/api/v1/sources/{id}/content?revision={n}` | stream مجاز؛ private/no-store |
| GET | `/api/admin/v1/library/sources` | catalog مدیریتی شامل Draft/Hidden/Archived |
| POST | `/api/admin/v1/library/sources` | Create با Scope قطعی Library؛ Platform permission لازم |

ویرایش/نسخه/انتشار/نمایانی Library از همان resource routeهای Source استفاده می‌کند و permission Library در سرور بررسی می‌شود؛ عمومی‌بودن شکل route به معنی مجازبودن عملیات نیست. API DELETE یا OCR/retry/approve-content در این مرحله وجود ندارد.

## Query و pagination

`scope=Personal|Class|Organization|Library`؛ پیش‌فرض Personal. برای Class، `classId` لازم است. OrganizationId ورودی، مجوز یا مالک جدید نمی‌سازد؛ سازمان از context مجاز گرفته می‌شود. Source مربوط به PersonalClass در Scope Class سازمانی پذیرفته نمی‌شود.

`page=1`، `pageSize=50`؛ pageSize بین ۱ تا ۱۰۰، page تا ۱۰۰۰۰۰؛ `search` حداکثر ۱۰۰ نویسه؛ `status=Active|Inactive|Archived` و `sourceType=Document|Image` اختیاری‌اند. Title search در سرور انجام می‌شود و ی/ک عربی و نیم‌فاصله normalize می‌شوند. `SourcePage` شامل page/pageSize/totalCount/items است. فیلتر client روی یک صفحه جای جست‌وجوی سرور نیست. Student و catalog عادی Library با فیلتر نمی‌توانند Draft/Archived را ببینند.

## Requestها

```json
{
  "clientOperationId": "<new-operation-guid>",
  "scope": "Class",
  "classId": "<authorized-organization-class-guid>",
  "title": "عنوان منبع",
  "description": "توضیح اختیاری",
  "fileAssetId": "<uploaded-file-guid>",
  "reason": "دلیل عملیات"
}
```

Title تا ۲۰۰، Description تا ۲۰۰۰ و Reason الزامی تا ۵۰۰ نویسه است. OwnerUserId/CreatedByUserId/OrganizationId از سرور تعیین می‌شوند و در DTO ساخت ورودی نیستند. برای Personal/Organization/Library، ClassId ندهید. File باید متعلق به caller و بافت مجاز باشد؛ فایل مصرف‌شدهٔ پاسخ دانش‌آموز یا Scope دیگر را promotion نکنید.

Update Draft: `title / description / expectedVersion / reason`.

New Version: `fileAssetId / title / description / expectedVersion / reason`؛ برای تعویض فایل Pending یا محتوای Published نیز همان SourceId را نگه می‌دارد و revision تازه می‌سازد. Draft قبلی حذف نمی‌شود.

Publish/Archive: `expectedVersion / reason`.

Visibility: `visibility=Public|Hidden / expectedVersion / reason`؛ فقط Library.

## فیلدهای پاسخ

`EducationalSourceResult`:

```text
sourceId, scope, ownerUserId?, organizationId?, classId?
status, visibility, currentVersion, publishedVersion?, revision
title, description?, sourceType, publicationStatus
safetyStatus, processingStatus, processingFailureCode?
fileReady, sourceReady, aiReady
capabilities { canRead, canWrite, canPublish, canArchive, canDownload,
               canUseForAi, canCreateVersion, canChangeVisibility }
file? { fileAssetId, originalFileName, contentType, sizeBytes, status }
createdByUserId, createdAtUtc, publishedAtUtc?, version, replayed
```

`version` token opaque از rowversion Source است؛ با شمارهٔ revision اشتباه نشود. Student فقط revision Published مجاز را می‌بیند و currentVersion او شمارهٔ Published است؛ شماره/metadata Draft تازه افشا نمی‌شود. writer نسخهٔ Draft جاری را می‌بیند و می‌تواند revision قبلی را صریح بخواند. capabilityها از مجوز جاری می‌آیند؛ Client Role enum را به permission تبدیل نکند.

## وضعیت و UX

- Pending File یا PendingValidation: منبع آماده نیست؛ وضعیت فنی را نشان دهید، پیام «ارسال/پردازش موفق» نسازید.
- ReviewRequired/Rejected: Publish و Download مجاز نیستند؛ مسیر approve یا Human Review در این قرارداد وجود ندارد.
- FileValidated: اعتبارسنجی فنی فایل انجام شده؛ معادل تأیید انسانی یا OCR نیست.
- Processing=NotRequested: pipeline AI/extraction شروع نشده؛ progress درصدی نشان ندهید.
- Processing=Failed: error-safe code را نشان دهید؛ فایل غیر-AIِ مجاز ممکن است SourceReady باقی بماند. Retry endpoint نداریم؛ error را به success یا حذف Source تبدیل نکنید.
- SourceReady و Draft: writer می‌تواند استفادهٔ خصوصی کند؛ Student هنوز مجاز نیست.
- Published: مخاطب مجاز current-state می‌تواند بخواند؛ SourceId به‌تنهایی مجوز نیست.
- Archived: از catalog عادی مخاطب خارج است؛ تاریخچه و blob حذف نشده و ظرفیت فعلاً آزاد نمی‌شود.
- AiReady/CanUseForAi: در این مرحله false؛ upload یا visibility عمومی entitlement AI ایجاد نمی‌کند.

Library عادی فقط metadata دارد؛ `file=null` و `canDownload=false`. catalog Published/Public برای کاربر واردشده قابل مشاهده است؛ Hidden از list حذف می‌شود ولی reference Published، metadata معتبر را نگه می‌دارد. این رفتار اجازهٔ دانلود نمی‌دهد و مدل تجاری Library هنوز مشخص نشده است.

## ماتریس دسترسی

| Scope | ساخت/مدیریت | خواندن |
|---|---|---|
| Personal | همان User فعال با قرارداد/ظرفیت شخصی لازم برای Create | همان User؛ مستقل از کارت Personal و role |
| Class | TeacherAssignment معتبر یا Manager همان سازمان | writerها و Student با Enrollment فعال، فقط Published |
| Organization | Manager معتبر همان سازمان | فعلاً همان Managerهای مجاز؛ audience عمومی Deferred |
| Library | permission `platform.library.sources.manage`؛ seed فقط PlatformOwner | metadata Published/Public؛ دانلود عادی بسته |

Parent دسترسی عمومی Class Source ندارد. School Manager ادمین Library نیست. پایان رابطه، read/download آیندهٔ سازمانی را قطع می‌کند. مسیر `/api/v1/files/{id}/content` نیز همین ACL Source را رعایت می‌کند؛ دورزدن Source API با FileAssetId مجاز نیست.

## خطاها و retry

| کد | HTTP | اقدام |
|---|---:|---|
| SOURCE_NOT_FOUND | 404 | منبع ناموجود/نامجاز را یکسان مدیریت کنید |
| SOURCE_REQUEST_INVALID | 400 | ورودی/نسخهٔ token/query را اصلاح کنید |
| SOURCE_NOT_ALLOWED | 403 | فایل/بافت یا دانلود Library مجاز نیست؛ retry کور نکنید |
| SOURCE_NOT_READY | 409 | وضعیت فنی/Safety را بررسی کنید |
| SOURCE_VERSION_CONFLICT | 409 | refetch، مقایسه و تأیید دوبارهٔ تغییر |
| SOURCE_IDEMPOTENCY_CONFLICT | 409 | همان OperationId با payload متفاوت مجاز نیست |
| SOURCE_QUOTA_EXCEEDED | 409 | قرارداد همان مالک را نشان دهید؛ به مالک دیگر fallback نکنید |
| FEATURE_NOT_ENTITLED / SUBSCRIPTION_INACTIVE / QUOTA_REQUIRED | 403 یا 409 طبق Commerce | حق/قرارداد/تعریف quota معتبر لازم است |
| SOURCE_PROCESSING_FAILED | فیلد وضعیت، نه success پردازش | خطای امن پردازش آینده؛ فایل غیر-AI را بی‌دلیل حذف نکنید |

در Create، retry دقیق همان payload با همان ClientOperationId، Source و مصرف دوم نمی‌سازد و Replayed=true می‌گیرد. mutationها ExpectedVersion لازم دارند؛ پس از lost ACK یا 409، وضعیت تازه بگیرید. token مصرف‌شده را با token جدید بی‌تأیید تعویض نکنید.

## Realtime و بازگشت از قطع اتصال

Hub موجود `/hubs/messaging`، پیام `ClientInvalidated` و replay موجود `/api/v1/realtime/changes` استفاده می‌شوند. Source شخصی با `scope=Personal` cursor مستقل دارد. `knowledge.source` فقط ResourceId مجاز و stream/context version می‌دهد؛ list/get و در صورت نیاز quota summary را refetch کنید. `knowledge.library` بدون ResourceId فقط catalog را invalidate می‌کند.

EventId/StreamVersion را dedupe کنید؛ event تکراری نباید به معنی افزایش count، مصرف quota یا unread باشد. `hasMore`، `retryAfterMilliseconds` و resync موجود را رعایت کنید. عضویت و permission هنگام replay/delivery دوباره بررسی می‌شوند. پس از revoke، 404 منبع را با cache محلی دور نزنید.

Publish Source جدید، پیام یا Card رسمی خودکار نمی‌سازد. Lesson/Assignment/Exam قبلی مسیر Card خودشان را حفظ کرده‌اند. پاسخ‌ها و پیوست‌های دانش‌آموز هیچ‌گاه خودکار Source/Library/AI Source نمی‌شوند.

## موارد خارج از این قرارداد

OCR، extraction/index واقعی، AI invocation، Citation generation، paid Library grant، Human Review workflow، hard delete، آزادسازی ظرفیت Archive، Parent audience و audience گستردهٔ سازمان اجرا نشده‌اند. برای تصمیم‌های باز و شواهد backend به [سند پیاده‌سازی](EDUCATIONAL_SOURCES_IMPLEMENTATION_FA.md) مراجعه کنید.
