# قرارداد بازبینی رسانه و تحویل فایل Messenger

تاریخ: `2026-11-22`؛ مبنا: `37757c78c987e2d2a3770188474af13cd9d2244c` و درخواست مصوب **MEDIA SAFETY REVIEW + FILE DELIVERY COMPLETION**. این درخواست، مسیر بازبینی انسانی را برای رسانه صریحاً مجاز کرده است. این سند جای تصمیم مسدود قبلی BE-01 را فقط برای همین مسیر می‌گیرد؛ شاهد نصب روی IIS نیست.

## علت و پیاده‌سازی واقعی

`InternalMakanContentSafetyProvider` موتور قواعد متن است؛ PDF/JPEG/PNG را اسکن نمی‌کند و به‌تنهایی `Other.UnsupportedMedia → ReviewRequired` می‌دهد. تنظیم موجود `Messaging:ContentSafetyProvider=Internal` اکنون به `ReviewedMediaContentSafetyProvider` وصل می‌شود: caption از موتور متن و فایل از `IMediaSafetyProvider` عبور می‌کند. پیاده‌سازی فعلی رسانه `ManualMediaSafetyProvider` است؛ تنها تصمیم صریح، ثبت‌شده و منطبق با اثرانگشت فایل در SQL مجوز Safety می‌دهد. اسکن خودکار، سرویس خارجی یا تضمین تشخیص کامل محتوا ادعا نمی‌شود.

## دو محور مستقل

| محور | مقدار | معنی |
|---|---|---|
| Storage | `FileAsset.Status=Ready` | بایت‌ها نوشته و اعتبارسنجی اولیهٔ نوع/اندازه/ساختار انجام شده است |
| بازبینی رسانه | `NotRequested` | هنوز پروندهٔ معتبر وجود ندارد؛ `attachmentAllowed=false` |
| بازبینی رسانه | `PendingReview` | پرونده ساخته شده ولی تصمیم نهایی محتوا ندارد؛ ارسال ممنوع |
| بازبینی رسانه | `Approved` | تصمیم صریح Allowed برای همین فایل؛ سایر مجوزهای ارسال همچنان باید بررسی شوند |
| بازبینی رسانه | `Rejected` | تصمیم Blocked یا عدم تطابق اثرانگشت؛ ارسال ممنوع |

State جدید به enum وضعیت Storage اضافه نشده است. `Approved` به معنی مالکیت یا عضویت و مجوز دانلود عمومی نیست. زمان، Hash، MIME و اندازه از Server می‌آیند.

## مسیر کلاینت

1. `POST /api/v1/files?workspaceScope=Personal` با Bearer و `multipart/form-data`، فیلد `file`. برای فایل سازمانی Scope/Context معتبر همان سازمان لازم است. نوع مجاز، اندازه، Signature، نام امن و ownership قبلی حفظ‌اند. نام فارسی مجاز است.
2. پاسخ موفق `201 FileAssetResult` و `status=Ready` را ذخیره کنید. فایل را دوباره Upload نکنید فقط چون Safety هنوز Pending است.
3. برای ارسال، `POST /api/v1/conversations/{conversationId}/messages` را با شناسهٔ پایدار عملیات و فایل Uploadشده فراخوانی کنید:

```json
{
  "clientMessageId": "<stable GUID>",
  "kind": "File",
  "text": null,
  "attachmentIds": ["<uploaded FileAssetId>"]
}
```

برای JPEG/PNG از `Image` استفاده کنید؛ caption اختیاری است. `Text` برای پیام دارای پیوست انتخاب صحیحی نیست. نمونهٔ بالا شناسهٔ واقعی ندارد. تمام فایل‌های یک پیام باید تأیید شده باشند؛ تأیید جزئی پیام نمی‌سازد.

4. نخستین تلاشِ مجاز برای رسانهٔ بدون تصمیم، پروندهٔ `MediaReview` را ایجاد/بازاستفاده می‌کند و `422 MESSAGE_SAFETY_REVIEW_REQUIRED` می‌دهد. یک فایل در چند درخواست/گفتگو نیز یک پروندهٔ منطقی دارد. قبل از این Send، GET وضعیت پرونده نمی‌سازد.
5. مالک فایل می‌تواند `GET /api/v1/files/{fileId}/messaging-review` را با نشست و Scope معتبر بخواند. پاسخ `200 MediaReviewStatusResult`:

```json
{
  "fileAssetId": "<file GUID>",
  "state": "PendingReview",
  "attachmentAllowed": false,
  "reviewReference": "<case GUID>",
  "reviewedAtUtc": null
}
```

این Route مخصوص Uploader است؛ داشتن مجوز دانلود یک پیام به‌تنهایی حق مشاهدهٔ این وضعیت خصوصی نمی‌دهد. ناموجود/نامجاز `404` است. نقش Reviewer، یادداشت داخلی و دلیل حساس به مالک افشا نمی‌شوند.
6. Pending را «فایل در حال بررسی است» نمایش دهید؛ Draft را نگه دارید و Sent/Delivered یا لینک گیرنده نسازید. درخواست ارسال را در Loop تکرار نکنید. بررسی وضعیت، تأیید محتوا یا ارسال خودکار نیست و SLA تضمین‌شده ندارد.
7. پس از `Approved` و `attachmentAllowed=true`، همان `clientMessageId` و همان payload قبلی می‌تواند دوباره ارسال شود؛ Upload تازه لازم نیست. ACL، نشست، Block، caption، عضویت و مجوز جاری دوباره بررسی می‌شوند. تغییر payload همان شناسه `409 MESSAGE_IDEMPOTENCY_CONFLICT` است. برای پیام واقعاً جدید شناسهٔ جدید لازم است.
8. در `Rejected` پیام «امکان ارسال این فایل وجود ندارد» نشان دهید؛ retry یا شناسهٔ جدید آن تصمیم را دور نمی‌زند. تصمیم محتوای پرونده نهایی است؛ workflow بازگشایی در این مرحله ساخته نشده است.

## پاسخ خطای امن

Code/statusهای موجود حفظ‌اند. در مسیر بازبینی رسانه، ProblemDetails علاوه بر `correlationId`/`traceId` و `X-Correlation-ID` شامل این موارد است:

| وضعیت | Code | safetyState | requiredAction |
|---|---|---|---|
| منتظر تصمیم | `MESSAGE_SAFETY_REVIEW_REQUIRED`، 422 | `PendingReview` | `WAIT_FOR_MEDIA_REVIEW` |
| رد محتوا | `MESSAGE_SAFETY_BLOCKED`، 422 | `Rejected` | `MEDIA_REJECTED_KEEP_DRAFT` |
| سرویس ایمنی ناموجود | `MESSAGE_SAFETY_UNAVAILABLE`، 503 | تصمیم موفق جعلی ندارد | رفتار خطای موجود |

برای دو ردیف اول `mediaReviewAvailable=true` و `operationState=NotApplied` است؛ `newMessageCreated`، `newAttachmentsBound`، `recipientDeliveryGranted`، `recipientDownloadGranted` و `retryable` همگی false هستند. این Flagها فقط اثر تازهٔ همان تلاش را توصیف می‌کنند. `safetyReference` شناسهٔ opaque رخداد Send است؛ با `reviewReference` پرونده یک مفهوم نیست. هیچ‌کدام مجوز خواندن Case یا فایل نیستند. ReviewRequired متن یا Provider دیگر همچنان `KEEP_DRAFT_NO_AUTO_RETRY` دارد و تأیید انسانی رسانه آن را آزاد نمی‌کند.

## مسیر کارشناس پلتفرم؛ Routeهای موجود

| Method / Route | ورودی و نتیجهٔ مهم |
|---|---|
| `GET /api/admin/v1/moderation/cases` | صف با filterهای موجود status/priority/page/pageSize؛ `source=MediaReview` نوع پرونده را مشخص می‌کند |
| `GET /api/admin/v1/moderation/cases/{caseId}` | جزئیات case-scoped؛ فیلد افزایشی `mediaReview` برای Source جدید |
| `POST /api/admin/v1/moderation/cases/{caseId}/start-review` | `expectedVersion`, `reason`؛ واگذاری پرونده به Reviewer جاری |
| `GET /api/admin/v1/moderation/cases/{caseId}/evidence/files/{fileAssetId}` | بایت همان Evidence با مجوز case-scoped، Cache خصوصی و Audit |
| `POST /api/admin/v1/moderation/cases/{caseId}/actions` | `expectedVersion`, `reason`, `action=MediaApproved` یا `MediaRejected`؛ پاسخ detail با نسخهٔ جدید |
| `POST /api/admin/v1/moderation/cases/{caseId}/resolve` | `expectedVersion`, `reason`, `internalNote` اختیاری؛ بستن اداری پس از تصمیم صریح |
| `POST /api/admin/v1/moderation/cases/{caseId}/dismiss` | همان قرارداد موجود؛ در پروندهٔ رسانه فقط پس از رد صریح |

Review permission برای خواندن/Claim لازم است؛ تصمیم محتوا علاوه بر آن `ModerationEnforce`، Reviewer واگذارشده و CaseStatus=InReview لازم دارد. مالک فایل نمی‌تواند آن را خودش تأیید کند. لغو نقش/مجوز پس از Claim نیز مؤثر است. Version قدیمی یا تصمیم دوم conflict می‌دهد؛ Resolution پرونده تصمیم Safety را Allowed نمی‌کند. پس از تصمیم، Case اداری تا Resolve همچنان InReview می‌ماند؛ `mediaReview.state` مرجع تصمیم فایل است. School Manager/Guardian به‌خاطر نقش سازمانی حق این کار یا مرور چت خصوصی نمی‌گیرند.

## Commit، Realtime و دانلود

تأیید فایل خودش Message، unread، preview یا Event ارسال نمی‌سازد. فقط Send مجاز و Commit معتبر، Message/Attachment و Outbox موجود را ثبت می‌کند. گیرنده از `ConversationChanged` کمینه برای invalidation استفاده و metadata پیوست را از Delta/History مجاز دریافت می‌کند؛ بایت خصوصی داخل SignalR فرستاده نمی‌شود. Retry موفق همان Message را برمی‌گرداند و پیام/Unread تکراری نمی‌سازد.

`GET /api/v1/files/{fileId}` و `GET /api/v1/files/{fileId}/content` برای گیرنده فقط پس از Binding پیام و با ACL جاری کار می‌کنند. Pending/Rejected فاقد پیام برای گیرنده 404 می‌ماند. مالک، با Scope معتبر خودش، دسترسی فایل در صف را حفظ می‌کند؛ این حق Case داخلی یا فایل ماژول دیگر نیست. نسخه، Range، نام دانلود و `private, no-store` قبلی محفوظ‌اند.

## ممیزی، SQL و مرزها

Migration لازم: `20261122085930_AddMessagingMediaReviews`. همان جدول `messaging.ModerationCases` با Source جدید، هفت ستون nullable، FK فایل/Reviewer، unique filtered index روی FileAssetId و قیود شکل/تصمیم گسترش یافته است؛ جدول صف موازی وجود ندارد. تصمیم به FileId/SHA256/MIME/Size گره دارد؛ اثرانگشت تغییرکرده Allowed نیست. rowversion و ترتیب قفل مشترک گفتگو → فایل‌های مرتب → پرونده، ایجاد هم‌زمان و رقابت Approval/Send را کنترل می‌کنند. Audit موجود، Actor/Time/Reason و اقدام MediaApproved/MediaRejected را ثبت می‌کند؛ محتوای خصوصی و Secret در لاگ عمومی درج نمی‌شوند.

Incident تاریخیِ شناخته‌شدهٔ `Other.UnsupportedMedia/ReviewRequired` فقط می‌تواند پس از تصمیم صریح همین مسیر دوباره ارزیابی شود؛ پروندهٔ قدیمیِ Resolved به‌تنهایی مجوز نیست. Blocked یا ReviewRequired متنی بازتفسیر نمی‌شود. Media Case فایل را برای cleanup نگه می‌دارد؛ binding ماژول دیگر و Evidence نامرتبط قابل استفاده برای آزادسازی فایل نیست.

Academic، Avatar، Library و Knowledge جریان/ACL مستقل دارند؛ Approval اینجا منبع RAG نمی‌سازد. Open Access، AI نامحدود، OTP/MeliPayamak، Config خصوصی، SQL isolation و مجوزهای مستقل تغییر نکرده‌اند. موتور خودکار آینده از همین Port قابل جایگزینی است ولی انتخاب Provider/credential، SLA انسانی و بازگشایی تصمیم خارج این مرحله‌اند.

## شواهد پذیرش

`MessagingMediaReviewAcceptanceTests.cs` با Provider ترکیبی واقعی، SQL Server ایزوله، API پلتفرم، SignalR و Outbox موجود، ۱۹ حالت را بررسی می‌کند: PDF/JPEG/PNG با نام فارسی و caption/فایل‌تنها، انتظار/رد/تأیید، retry، رقابت، download بایت‌به‌بایت، عدم نشتی کاربر/سازمان، مجوز جاری، self-approval، اثرانگشت و OpenAPI. `MediaSafetyReviewTests.cs` دوازده مورد Domain/provider را می‌سنجد. این شاهد محلی است؛ پذیرش IIS مقصد و عملیات انسانی واقعی سرور باید پس از نصب دستی جدا انجام شوند. نتیجهٔ کامل Build/Regression در Manifest انتشار و [گزارش مرحله](../release/MEDIA_SAFETY_REVIEW_LEARNING_FA.md) ثبت می‌شود.
