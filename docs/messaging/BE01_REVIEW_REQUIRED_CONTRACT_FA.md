# BE-01 — قرارداد جاری ReviewRequired و بازبینی رسانه

به‌روزرسانی مصوب `2026-11-22`: درخواست **MEDIA SAFETY REVIEW + FILE DELIVERY COMPLETION** مسیر انسانی را صریحاً مجاز کرد. وضعیت تاریخی `PRODUCT_DECISION_BLOCKED=true` برای انتخاب workflow رسانه با این تصمیم رفع شده است. [قرارداد کامل کلاینت/پلتفرم](MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md) مرجع جاری است؛ سیاست `messaging-20261101` و Open Access تغییری نکرده‌اند.

## علت و تصمیم

`InternalMakanContentSafetyProvider` همچنان فقط قواعد متن دارد. `Other.UnsupportedMedia → ReviewRequired` خرابی Upload نیست. Provider واقعیِ تنظیم Internal اکنون `ReviewedMediaContentSafetyProvider` است؛ caption را با همان موتور متن و فایل را با `ManualMediaSafetyProvider` بررسی می‌کند. هیچ موتور خودکار رسانه یا سرویس خارجی ادعا نمی‌شود.

نخستین Send مجاز برای فایل Ready بدون تصمیم، یک `ModerationCase` با Source=`MediaReview` ایجاد/بازاستفاده می‌کند. پرونده به فایل، SHA256، MIME و اندازه بسته است. `GET /api/v1/files/{fileId}/messaging-review` فقط برای Uploader مجاز، `NotRequested / PendingReview / Approved / Rejected` و `attachmentAllowed` را برمی‌گرداند؛ GET خودش پرونده نمی‌سازد.

## قرارداد عدم تحویل

Ready به معنی ذخیرهٔ موفق است؛ Approval Safety نیست. تا Pending یا Rejected، Message/MessageAttachment تازه، unread/preview یا Event ارسال ساخته نمی‌شود. گیرندهٔ فاقد Binding مستقل metadata/content را با404 دریافت می‌کند؛ مالک با Scope معتبر به فایل خودش دسترسی دارد، نه به جزئیات خصوصی Case.

برای مسیر رسانه، `422 MESSAGE_SAFETY_REVIEW_REQUIRED` دارای `safetyState=PendingReview`، `mediaReviewAvailable=true` و `requiredAction=WAIT_FOR_MEDIA_REVIEW` است. رد رسانه `422 MESSAGE_SAFETY_BLOCKED`، `safetyState=Rejected` و `requiredAction=MEDIA_REJECTED_KEEP_DRAFT` دارد. `operationState=NotApplied` و Flagهای `newMessageCreated/newAttachmentsBound/recipientDeliveryGranted/recipientDownloadGranted/retryable` همگی false هستند. `safetyReference` رخداد Send با `reviewReference` پرونده یکی نیست؛ هیچ‌یک مجوز دسترسی ایجاد نمی‌کنند. correlation/trace و header موجود `X-Correlation-ID` حفظ‌اند.

## تصمیم صریح و Retry

همان صف و Routeهای Platform Moderation استفاده می‌شوند. Reviewer باید پرونده را با Version جاری Claim کرده باشد؛ `MediaApproved` یا `MediaRejected` در `/actions` علاوه بر `ModerationReview` به `ModerationEnforce` نیاز دارد. self-approval، مجوز لغوشده، نسخهٔ کهنه و تصمیم دوم رد می‌شوند. بستن اداری پرونده به‌تنهایی Approval نیست؛ Pending قابل Resolve/Dismiss نیست. بعد از تصمیم، وضعیت محتوای فایل مستقل از InReview/Resolved اداری است.

پس از Approval همان فایل و همان ClientMessageId/payload قابل ارسال است؛ نه Upload دوباره لازم است و نه پیام خودکار ساخته می‌شود. payload متفاوت با همان شناسه409 می‌دهد. نقش، نشست، Block، عضویت، Scope، Safety caption و Binding جاری هنگام Send دوباره بررسی می‌شوند. Pending/Rejected با retry یا شناسهٔ جدید Safe نمی‌شوند. تصمیم متنی ReviewRequired همچنان `KEEP_DRAFT_NO_AUTO_RETRY` دارد و این workflow آن را آزاد نمی‌کند.

SafetyIncidentهای تاریخیِ شناخته‌شدهٔ unsupported-media و ReviewRequired می‌توانند برای همین تصمیم صریح دوباره ارزیابی شوند؛ Resolvedشدن پروندهٔ قدیمی کافی نیست. تصمیم Blocked یا متن بازتفسیر نمی‌شود. SafetyIncident قبلی برای ممیزی immutable باقی می‌ماند؛ برای فایل unique case وجود دارد، نه case تازه در هر Send.

## SQL، ممیزی و پذیرش

Migration `20261122085930_AddMessagingMediaReviews` همان ModerationCases را با هفت ستون nullable، Source جدید، FK، unique filtered index فایل و Check Constraint گسترش می‌دهد. rowversion موجود و ترتیب قفل مشترک گفتگو → فایل‌های مرتب → پرونده، رقابت ایجاد و تصمیم/Send را کنترل می‌کنند. Case/Evidence فایل را برای cleanup نگه می‌دارند. Actor، Reason، زمان و ModerationAction در Audit موجود ثبت می‌شوند؛ لاگ عمومی متن، نام فایل، Hash خصوصی، Secret یا ProviderReference خام ندارد.

۱۹ تست `MessagingMediaReviewAcceptanceTests` جریان PDF/JPEG/PNG، فارسی، caption/فایل‌تنها، تصمیم واقعی از API پلتفرم، Outbox/SignalR، Delta/History و دانلود بایت‌به‌بایت را روی SQL ایزوله می‌سنجند. دوازده تست `MediaSafetyReviewTests` قواعد خالص و Provider را پوشش می‌دهند؛ تست‌های تاریخی BE-01 نیز عدم تبدیل بستن پرونده به release و امنیت لاگ را حفظ می‌کنند. نتیجهٔ نهایی Regression در گزارش همین مرحله ثبت می‌شود؛ این شواهد Deploy سرور نیستند.

```text
PRODUCT_DECISION_BLOCKED=false
BE01_ROOT_CAUSE_IDENTIFIED=true
BE01_SAFETY_BYPASSED=false
BE01_HUMAN_REVIEW_IMPLEMENTED=true
BE01_PRODUCT_DECISION_REQUIRED=false
```

Provider خودکار آینده، SLA عملیاتی و Reopen تصمیم نهایی در این مرحله ساخته نشده‌اند. Academic/Knowledge مستقل‌اند؛ Approval رسانه ingest خودکار ایجاد نمی‌کند.
