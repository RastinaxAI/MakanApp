# Open Access و قرارداد ارسال فایل — ۲۰۲۶/۱۱/۲۲

مرجع این تصمیم، درخواست مصوب «OPEN ACCESS + FILE SENDING HARDENING» مالک محصول است؛ مبنای Source، `67417677297d7f46466ad0ed061e26bfc76402dc` است. این سند Backend است و اثبات Deploy یا مجوز تغییر Frontend نیست.

## سیاست جاری

```text
Age-based feature gating: Disabled
Subscription enforcement: Disabled
Quota enforcement: Disabled
Current access model: Open Access
AI_USAGE_MODE=UNMETERED
AI_PAYMENT_REQUIRED=NO
AI_SUBSCRIPTION_REQUIRED=NO
```

`Commerce:EnforcementEnabled=false` در تنظیمات پایه، Development، Test و Production و نمونه‌های عمومی دو محیط صریح است. سایر مقادیر، Secretها و مسیرهای جداگانهٔ Storage حفظ‌اند. تغییر تنظیمات با راه‌اندازی مجدد Host اعمال می‌شود. در نبود این کلید، پیش‌فرض Options رفتار تجاری قبلی را حفظ می‌کند؛ فایل‌های این انتشار false دارند.

خاموش‌بودن کنترل، Subscription/Plan نامحدود یا قیمت، تاریخ و Quota حدسی در SQL نمی‌سازد. سوابق واقعی، Catalog، Order، Payment و APIهای مدیریت باقی‌اند. خطای عملیات مالیِ خود Commerce با Gate قابلیت عمومی یکی نیست. پرداخت زنده و Featureهای جداگانهٔ Deferred خودکار فعال نمی‌شوند.

دو پاسخ HTTP موجود `CommercialSummaryDto` و `ManagerCommerceSummaryDto` فیلد افزایشی `enforcementEnabled` دارند؛ نتیجهٔ داخلی Application به نام `CapacityCheckResult` نیز همین Flag را دارد و Endpoint تازه‌ای برای آن ساخته نشده است. false یعنی قرارداد مالی Gate محصول نیست؛ State و Quotaهای قدیمی همچنان Metadata واقعی‌اند. Client از NoSubscription یا Quota سابق، منع استفاده نسازد. این Flag مجوز هیچ کلاس، گفتگو، فایل یا Source نیست.

با کنترل تجاری خاموش، Mode قدیمی Commercial نیز در Composition Root به Unmetered resolve می‌شود. AI، History، Streaming، SourceIntent و Source ACL حفظ‌اند. رزرو نامحدود برای OperationId و جلوگیری از اجرای تکراری باقی است؛ SubscriptionId/PlanVersionId مالی ساخته نمی‌شود و تکرار عملیات موفق همان قرارداد 409 فعلی را دارد.

## سن، ACL و Safety

سیاست موجود `messaging-20261101`، Age/DOB/Unverified/Conflict را از مجوز عمومی جدا کرده است. دادهٔ سن، APIهای CommunicationAge و Audit برای سازگاری باقی‌اند؛ Adult یا DOB ساختگی ثبت نمی‌شود. نبود DOB یا تأیید سن به‌تنهایی NextAction منع Messaging ایجاد نمی‌کند.

حساب/نشست فعال، پروفایل واقعاً کامل، Privacy، Block، رضایت دعوت، عضویت، Role/Grant جاری و ACL منبع همچنان لازم‌اند. GuardianRelation حق مرور Direct خصوصی ایجاد نمی‌کند. ظرفیت واقعی آموزشی کلاس، سقف فنی فایل/بدنه، Anti-spam و Rate Limit امنیتی با Quota مالی یکی نیستند و حفظ می‌شوند.

## Upload واقعی

`POST /api/v1/files?workspaceScope=Personal` با Bearer معتبر و `multipart/form-data` استفاده می‌شود. Property واقعی `UploadFileRequest.File` است؛ نام فرم `file` قابل Bind است. Boundary را HTTP client بسازد؛ Content-Type کل درخواست را به MIME خود فایل تبدیل نکنید.

برای فایل پیام Personal، `workspaceScope=Personal` را صریح بفرستید، حتی اگر Organization انتخاب شده است. فایل سازمانی به Scope و Membership جاری همان Organization نیاز دارد؛ OrganizationId دلخواه مجوز نیست. `personalClassId` اختیاری فقط با عضویت جاری همان کلاس استفاده می‌شود.

پاسخ 201 از نوع `FileAssetResult` شامل `id`، `status`، `contentType`، `sizeBytes`، Hash، زمان‌ها و `rowVersion` است. پس از نوشتن اتمیک Blob، تطابق اندازه و ثبت Ready برمی‌گردد؛ تبدیل Pending به Ready در Upload صف Async ندارد. لغو/شکست نوشتن، 201 موفق نمی‌سازد.

حد مؤثر فعلی ۱۰ MiB و انواع مجاز `application/pdf`، `image/jpeg`، `image/png` و `text/plain` هستند. پسوند و Signature محتوا باید سازگار باشند؛ این بررسی، اسکن کامل محتوای فایل نیست. فایل خالی، بیش از سقف و نوع/پسوند ناسازگار رد می‌شود. نام فارسی و فاصله مجازند؛ StorageKey از نام نمایشی ساخته نمی‌شود. سقف Multipart برابر MaxFileSizeBytes به‌علاوهٔ ۶۵۵۳۶ بایت است. تنظیم IIS واقعی در این Task بررسی یا تغییر نشده است.

## Attachment واقعی

مسیر `POST /api/v1/conversations/{conversationId}/messages` از `SendMessageCommand` استفاده می‌کند:

```json
{
  "clientMessageId": "<persistent operation GUID>",
  "kind": "File",
  "text": null,
  "attachmentIds": ["<id from successful upload>"]
}
```

مقادیر بالا placeholder هستند؛ GUID واقعی لازم است. برای JPEG/PNG از Kind متناسب Image استفاده شود. Kind پیش‌فرض Text، Attachment نمی‌پذیرد؛ File-only با text=null از قبل پشتیبانی می‌شود. caption معتبر اختیاری است؛ فقط فاصله جای null نیست. سقف تعداد پیوست ۱۰ است.

فایل باید Ready، متعلق به فرستنده، در Scope مجاز و فاقد Binding ناسازگار باشد. Pending/Rejected/Deleted و فایل متصل‌نشدهٔ منقضی مجاز نیستند. شناسهٔ فایل دیگران مجوز نیست؛ Forward فقط با کنترل مبدأ/مقصدِ مسیر خودش انجام می‌شود. Retry همان ClientMessageId/Payload اثر تکراری نمی‌سازد و Payload متفاوت 409 می‌دهد.

## تکمیل مسیر رسانه با تصمیم انسانی مصوب

در `2026-11-22` مالک، **Manual Moderation Review** را برای رسانه تأیید کرد. علت قبلی واقعی بود: `InternalMakanContentSafetyProvider` فقط متن را بررسی می‌کند و رسانه را `Other.UnsupportedMedia → ReviewRequired` می‌دانست. موتور خام متن تغییر نکرده؛ اکنون تنظیم Internal، `ReviewedMediaContentSafetyProvider` را مصرف می‌کند که caption را با همان موتور و فایل را با `ManualMediaSafetyProvider` می‌سنجد. این مسیر ادعای اسکن خودکار ندارد و Safe فرضی نمی‌سازد.

Ready فقط Storage است. اولین Send مجاز پروندهٔ یکتای همان فایل در Moderation موجود می‌سازد و422 PendingReview می‌دهد. مالک از `GET /api/v1/files/{fileId}/messaging-review` وضعیت امن را می‌گیرد. کارشناس دارای Review+Enforce، پس از Claim و مشاهدهٔ Evidence، تصمیم صریح `MediaApproved` یا `MediaRejected` در Route موجود `/api/admin/v1/moderation/cases/{caseId}/actions` ثبت می‌کند. self-approval، نسخهٔ کهنه، مجوز لغوشده و Resolve کردن Pending مجاز نیستند.

Pending/Rejected اثر تازهٔ Message/Attachment/Delivery ندارند و برای گیرنده دانلود نمی‌شوند. Approval به اثرانگشت همان فایل گره دارد؛ پس از آن همان ClientMessageId و payload می‌تواند بدون Upload تازه ارسال شود. تأیید پرونده خودش پیام نمی‌فرستد و ACL/Block/Safety caption را دور نمی‌زند. Retry پیام/پرونده/مصرف تکراری نمی‌سازد. تصمیم‌های تاریخیِ بسته‌شده به‌تنهایی Approval محسوب نمی‌شوند.

قرارداد دقیق `NotRequested/PendingReview/Approved/Rejected`، requiredActionها، permissionها، Retry و UX در [Handoff رسانه](../messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md) است. مسیر واقعی PDF/JPEG/PNG، فارسی، caption و فایل‌تنها با Provider اصلی و SQL/Outbox/SignalR محلی تست شده است؛ نتیجهٔ کامل Regression در گزارش همین مرحله ثبت می‌شود و به معنی نصب روی IIS نیست. Open Access این سند بدون تغییر باقی است.

## Realtime، Download و جدایی دانش

پس از Send مجاز و Commit، Message/Attachment و Outbox در جریان موجود ثبت می‌شوند. `ConversationChanged` Invalidation کمینه است؛ گیرنده Metadata پیوست را از Changes/History مجاز می‌گیرد. Event، بایت فایل یا DTO کامل پیام را حمل نمی‌کند. Replay و کنترل Session/Scope جاری و عدم اثر تکراری حفظ‌اند.

- `GET /api/v1/files/{fileId}`: Metadata برای مالک یا Grant جاری Binding.
- `GET /api/v1/files/{fileId}/content`: Stream مجاز، نام دانلود، Content-Type/Length، Range و Cache خصوصی no-store.
- `DELETE /api/v1/files/{fileId}?expectedRowVersion=...`: مالک مجاز و نسخهٔ جاری؛ فایل سابقهٔ رسمی از مسیر عمومی حذف نمی‌شود.

بعد از Send موفق، عضو مجاز گفتگو دانلود می‌کند؛ URL قدیمی پایان Membership یا ACL را دور نمی‌زند. PendingReview هنوز Binding پیام ندارد و برای گیرنده 404 است. Upload پیام خودکار Source دانش نیست؛ Publish/ingest/RAG، جریان و ACL مستقل دارند.

خطاهای `FILE_EMPTY`، `FILE_TOO_LARGE`، `FILE_TYPE_NOT_ALLOWED`، `FILE_CONTENT_INVALID` و خطاهای مالکیت/Binding حفظ‌اند. خطای Safety از Upload جدا نمایش داده شود؛ 201 Upload را Send موفق نشان ندهید. Schema سن/Commerce حذف نشده و برای Open Access Migration لازم نیست؛ بازبینی رسانه با Migration مستقل `20261122085930_AddMessagingMediaReviews` اضافه شده است. نتیجهٔ واقعی Validation و Gate رسانه در گزارش مرحله ثبت می‌شود.
