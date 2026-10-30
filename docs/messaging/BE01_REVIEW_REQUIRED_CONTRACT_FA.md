# BE-01 — قرارداد وضعیت ReviewRequired و ردیابی ایمنی

## تصمیم محصول و علت واقعی

`PRODUCT_DECISION_BLOCKED=true`. علت شناسایی‌شده، خرابی Upload یا تأییدشدن فایل نیست: `InternalMakanContentSafetyProvider` موتور قواعد متن است و بایت رسانه را اسکن نمی‌کند. برای عملیات دارای مخاطب محافظت‌شده، رسانه با دلیل داخلی `Other.UnsupportedMedia` و نسخهٔ `makan-child-safety-v1-rules-1`، تصمیم `ReviewRequired` می‌گیرد. این علت داخلی به Client افشا نمی‌شود.

Product هنوز انتخاب نکرده که این فایل‌ها با انسان بررسی شوند، به موتور async دیگری بروند یا برای Messaging رد شوند. هیچ‌یک از این سه گزینه در این مرحله پیاده‌سازی یا قطعی نشده است. وجود `SafetyIncident` و پروندهٔ خصوصی Moderation از قبل، به معنی وجود صف تأیید رسانه، SLA یا حق release نیست. مسیر فعلی و همهٔ Safety/Authorization checkها حفظ شده‌اند.

## دو وضعیت مستقل

| وضعیت | معنی واقعی |
|---|---|
| FileAsset.Status=Ready | bytes ذخیره و اعتبارسنجی اولیهٔ Storage کامل است؛ تأیید Safety یا مجوز attach/download نیست |
| HTTP 422 + safetyState=PendingReview | نتیجهٔ ReviewRequired همان تلاش؛ mutation اعمال نشده و فایل برای این تلاش تحویل‌پذیر نیست |
| HTTP 422 + safetyState=Rejected | تصمیم Blocked همان تلاش؛ با PendingReview یکی نیست |
| HTTP 200 و receipt معتبر | فقط اثر commit‌شدهٔ عملیات مجاز؛ نه یک Approved سراسری و دائمی برای Asset |
| HTTP 503 MESSAGE_SAFETY_UNAVAILABLE | بررسی ایمنی در دسترس نیست؛ موفقیت یا PendingReview جعلی تولید نمی‌شود |

PendingReview وضعیت پاسخ تلاش ارسال است؛ enum جدیدی به FileAsset یا Message اضافه نشده است. Message/MessageAttachment برای تلاش ردشده ساخته نمی‌شوند. تغییر ردشدهٔ Edit هم پیام قبلی را بازنویسی نمی‌کند. فایل Pending که binding مجاز دیگری ندارد، از مسیر عادی metadata/content برای گیرنده 404 می‌گیرد. این قرارداد مجوز معتبر یک binding قدیمی و مستقل را خودکار لغو نمی‌کند.

## پاسخ امن و قابل اقدام

`POST /api/v1/conversations/{conversationId}/messages` همان route و همان code/status قبلی را حفظ می‌کند. ProblemDetails برای ReviewRequired یا Blocked این افزوده‌ها را دارد:

```json
{
  "status": 422,
  "code": "MESSAGE_SAFETY_REVIEW_REQUIRED",
  "safetyState": "PendingReview",
  "operationState": "NotApplied",
  "newMessageCreated": false,
  "newAttachmentsBound": false,
  "recipientDeliveryGranted": false,
  "recipientDownloadGranted": false,
  "requiredAction": "KEEP_DRAFT_NO_AUTO_RETRY",
  "retryable": false
}
```

وقتی رخداد واقعاً ثبت شده باشد، `safetyReference` از نوع UUID و شناسهٔ opaque همان رخداد نیز برمی‌گردد. `correlationId`، `traceId` و header موجود `X-Correlation-ID` برای هر درخواست باقی‌اند. مقادیر نمونهٔ واقعی شناسه‌ها یا credential در این سند درج نشده‌اند.

Client پیش‌نویس را نگه دارد، وضعیت Sent/Delivered یا لینک قابل دانلود گیرنده نسازد و retry خودکار نکند. PendingReview وعدهٔ شروع Human Review، تکمیل async یا انتشار خودکار نیست. در Blocked، state برابر Rejected است. `new*` فقط اثر تازهٔ همین تلاش را توصیف می‌کند؛ پیام‌های قدیمی یا bindingهای مجاز قبلی را انکار نمی‌کند.

## Retry و ممیزی

همان User/Conversation/ClientMessageId با همان payload، تصمیم و safetyReference قبلی را می‌دهد. payload متفاوت با همان شناسه 409 MESSAGE_IDEMPOTENCY_CONFLICT است. SafetyIncident، ModerationCase و EvidenceAttachment موجود، کلید یکتا و transaction قبلی را reuse می‌کنند؛ retry پیام، پیوست قابل ارسال یا مصرف دوم ایجاد نمی‌کند. بستن/Resolved شدن پرونده یا Incident به معنی Allowed نیست؛ retry بعد از بستن هم همان تصمیم محافظتی را می‌گیرد.

ردیابی عملیاتی: correlation/trace درخواست → لاگ کمینهٔ SafetyReference → SafetyIncident → ModerationCase/ModerationEvidenceAttachments → FileAsset. PolicyCodes/ProviderReference خصوصی، نسخه و دلیل را حفظ می‌کنند. لاگ جدید فقط reference، decision، reason محدود `UnsupportedMedia` یا `ContentPolicy`، ContentKind، Replayed، OperationState و نبود اثر تازه را دارد. متن، نام فایل، Hash محتوا، شماره، هویت کودک، provider reference خام، policy codes خام، Token و exception وارد این لاگ نمی‌شوند.

دسترسی case-scoped و audit موجود پلتفرم تغییر نکرده است. School Manager مجوز تازه‌ای نمی‌گیرد. شناسهٔ SafetyReference مجوز خواندن Case/Asset ایجاد نمی‌کند. endpoint تأیید/رد/Release جدیدی ساخته نشده و Migration تازه لازم نشده است.

## وضعیت پذیرش

تست‌های `MessagingSafetyReviewContractTests.cs` از Provider داخلی واقعی استفاده می‌کنند: Minor/Unknown، پاسخ امن و correlation، retry ترتیبی/هم‌زمان، عدم مصرف و binding تکراری، عدم دانلود/تحویل برای گیرنده، concealment برای outsider، بستن پرونده بدون release، تفکیک Blocked از متن Allowed و OpenAPI. آمار نهایی suite و Git در گزارش validation همین مرحله ثبت می‌شود؛ UI یا سرور واقعی از این تست‌ها نتیجه گرفته نمی‌شود.

```text
PRODUCT_DECISION_BLOCKED=true
BE01_ROOT_CAUSE_IDENTIFIED=true
BE01_SAFETY_BYPASSED=false
BE01_HUMAN_REVIEW_IMPLEMENTED=false
BE01_PRODUCT_DECISION_REQUIRED=true
BE01_RELEASE_BLOCKING_FOR_ATTACHMENT=true
```

قابلیت release پیوست در مسیر محافظت‌شده مسدود است؛ اصلاحات مستقل Messaging/Realtime به این تصمیم آینده وابسته نشده‌اند. هیچ Safety switch خاموش نشده و AI یا اسکنر رسانهٔ تازه‌ای فعال نشده است.
