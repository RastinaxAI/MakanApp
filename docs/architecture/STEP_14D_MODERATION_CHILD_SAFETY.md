# STEP 14D — زیرساخت تعدیل محتوا و ایمنی کودک

## هدف و مرز مرحله

این مرحله گزارش تخلف موجود را از یک receipt صرف به یک جریان عملیاتی قابل بررسی تبدیل می‌کند و پیش از ثبت پیام در گفتگوهای child-sensitive یک release gate مستقل از vendor قرار می‌دهد. هیچ Platform Role، Backoffice، appeal، SLA، retention duration یا provider واقعی در این مرحله ساخته نشده است. صف `PlatformSafety` فقط در دیتابیس آماده شده و API عمومی/عملیاتی آن به STEP 15 موکول است.

## Routing گزارش

- گزارش عادی در Group/Channel کاربرمدیریت‌شده به `ConversationModerators` با اولویت `Normal` می‌رود.
- Personal Direct و `SystemManagedAcademic` به `PlatformSafety` می‌روند.
- دلیل `ChildSafetyConcern = 7` در هر نوع گفتگو به `PlatformSafety` با اولویت `Critical` می‌رود.
- شماره enumهای قبلی تغییر نکرده‌اند.
- هر `AbuseReport` دقیقاً یک `ModerationCase` دارد. retry همان `ClientReportId` پرونده یا action دوم نمی‌سازد.

Migration برای گزارش‌های قبلی یک پرونده قطعی می‌سازد. نوع Direct، مدیریت سیستمی یا Child Safety به Platform و بقیه به Conversation Moderators route می‌شوند. چون پیش از 14D هیچ endpoint انتقال status وجود نداشت، backfill با وضعیت `Pending` انجام می‌شود.

## مجوز بازبین گفتگو

`ConversationAdminPermission.ReviewReports = 256` به‌عنوان flag بعدی اضافه شده است. مقادیر قبلی و `LegacyAdminDefault = 51` ثابت مانده‌اند و Adminهای موجود backfill نمی‌شوند.

- Owner مجوز ضمنی دارد.
- Admin فقط با `ReviewReports` مجاز است.
- `DeleteMessages` مجوز review نیست و `ReviewReports` مجوز حذف پیام نیست.
- Member، Organization Manager بدون participant/permission، participant لغوشده، کاربر گزارش‌شده و درخواست cross-conversation/cross-scope مجاز نیستند.
- بررسی دسترسی در هر list/detail/mutation/download دوباره اجرا می‌شود؛ نگه‌داشتن URL یا metadata قبلی مجوز نیست.
- گزارش کاربر گزارش‌شده از list او حذف می‌شود و detail آن 404 است تا تعارض منافع و existence leak ایجاد نشود.

## پرونده و audit

`ModerationCase` منبع typed برابر `UserReport` یا `AutomatedSafety` دارد و constraint دیتابیس دقیقاً یکی از `AbuseReportId` و `SafetyIncidentId` را الزامی می‌کند. lifecycle آن:

```text
Pending -> InReview -> Resolved | Dismissed
```

claim و بستن پرونده با transaction سریال‌پذیر، rowversion و بازبین assigned انجام می‌شود. یادداشت داخلی حداکثر ۱۰۰۰ نویسه است و در DTO بازبین یا receipt گزارش‌دهنده برنمی‌گردد. `ModerationAction` append-only و بدون rowversion/update/delete API است و Created، ReviewStarted، Resolved و Dismissed را با actor/time ثبت می‌کند. status `AbuseReport` هم‌زمان با case به `UnderReview/Resolved/Dismissed` منتقل می‌شود؛ receipt گزارش‌دهنده هیچ reviewer، note، queue، risk یا provider metadata ندارد.

## کمینه شاهد

شاهد گزارش فقط snapshot همان پیام انتخاب‌شده است: sender، kind، متن جاری، نسخه پیام و پیوست‌های همان پیام. پیام همسایه، history گفتگو، phone، credential، Membership، Guardian یا AI data خوانده نمی‌شود. snapshot بعد از edit، tombstone یا Delete For Me ثابت می‌ماند.

`ModerationEvidenceAttachment` فقط relation پرونده به `FileAsset` را نگه می‌دارد و بایت را کپی نمی‌کند. FileAsset retain می‌شود. endpoint اختصاصی evidence در هر دانلود مجوز بازبین را دوباره بررسی می‌کند. فایل تلاش مسدودشده چون به Message متصل نیست، از مسیر عادی `/files` قابل دریافت نیست.

## release gate ایمنی کودک

Application port برابر `IContentSafetyProvider` است و فقط تصمیم typed `Allowed`، `Blocked` یا `ReviewRequired` را می‌پذیرد. adapter واقعی انتخاب نشده است:

- `DevelopmentAllowContentSafetyProvider` فقط در Development/Testing قابل انتخاب است.
- `UnavailableContentSafetyProvider` default امن محیط‌های دیگر است.
- Integration Test از provider ضبط‌کننده بدون شبکه استفاده می‌کند.

گفتگو child-sensitive است اگر SQL `EXISTS` حداقل یک participant فعال با `CommunicationAgeCategory.Minor` یا `Unknown` پیدا کند. Unknown عمداً Adult تلقی نمی‌شود. Adult-only بدون تماس provider به مسیر عادی می‌رود.

جریان ارسال:

```text
HTTP Send/Forward
-> MessagingService.ValidateSend / PrepareForwardMessageSafetyAsync
-> EfMessagingStore.PrepareMessageSafetyAsync (authorization + SQL EXISTS)
-> IContentSafetyProvider.EvaluateAsync (خارج transaction)
-> Allowed: EfMessagingStore.SendMessageAsync / ForwardMessageAsync
   (authorization و شرط child-sensitive دوباره + commit Message)
-> Blocked/ReviewRequired: RecordSafetyIncidentAsync
   -> بدون Message
   -> SafetyIncident + Critical PlatformSafety ModerationCase + evidence
-> HTTP ProblemDetails امن
```

این gate برای محتوای تازه در گفتگوی child-sensitive فعال است و هر دو مسیر Send و
Forward را پوشش می‌دهد. در Direct سن دو طرف و در Group/Channel همه participantهای
فعال با SQL `EXISTS` بررسی می‌شوند. بررسی دوباره در transaction نهایی مانع دورزدن
gate در صورت تغییر هم‌زمان participant با رده سنی `Minor` یا `Unknown` می‌شود.

provider authorization نیست. write نهایی پس از پاسخ provider دوباره participant، scope، Block، privacy/publication و attachment را کنترل می‌کند. اگر کاربر در فاصله ارزیابی خارج شود، Message ثبت نمی‌شود.

Unavailable، timeout، exception یا پاسخ نامعتبر fail-closed است: `MESSAGE_SAFETY_UNAVAILABLE` و 503، بدون Message و بدون SafetyIncident. Block/ReviewRequired به‌ترتیب `MESSAGE_SAFETY_BLOCKED` و `MESSAGE_SAFETY_REVIEW_REQUIRED` با 422 هستند و provider reference/risk/policy code را افشا نمی‌کنند.

`SafetyIncident` با `(ConversationId, ActorUserId, ClientMessageId)` یکتا است. payload یکسان همان outcome را می‌دهد و provider دوباره فراخوانی نمی‌شود؛ payload متفاوت `MESSAGE_IDEMPOTENCY_CONFLICT` است. `ReviewRequired` هیچ‌وقت خودکار Message قدیمی را منتشر نمی‌کند.

## API بازبین گفتگو

| Method | Route | رفتار |
|---|---|---|
| `GET` | `/api/v1/conversations/{conversationId}/moderation/cases` | فهرست bounded با `status`، `priority`، `page` و `pageSize<=100` |
| `GET` | `/api/v1/conversations/{conversationId}/moderation/cases/{caseId}` | detail کمینه همان case |
| `POST` | `.../{caseId}/start-review` | claim با `expectedVersion` |
| `POST` | `.../{caseId}/resolve` | resolve فقط توسط reviewer assigned |
| `POST` | `.../{caseId}/dismiss` | dismiss فقط توسط reviewer assigned |
| `GET` | `.../{caseId}/evidence/files/{fileAssetId}` | دانلود شاهد با re-authorization جاری |

این API فقط `ConversationModerators` را نشان می‌دهد. API صف PlatformSafety عمداً وجود ندارد.

## دیتابیس

Migration `20261012222340_AddMessagingModerationAndChildSafety` جدول‌های زیر را در schema `messaging` ایجاد می‌کند:

- `ModerationCases`: source/queue/priority/status، assignment، timestamps، note داخلی و rowversion؛
- `ModerationActions`: audit append-only؛
- `SafetyIncidents`: decision، metadata محدود provider، content/payload hash، snapshot، status و rowversion؛
- `ModerationEvidenceAttachments`: اتصال یکتای case/file.

FKها Restrict هستند. unique indexهای source و incident idempotency، index صف/status/priority/time، conversation/status، assigned/status، incident conversation/time و evidence تعریف شده‌اند. constraint مجوز Admin از 255 به 511 توسعه یافته و هیچ Admin موجودی `ReviewReports` دریافت نمی‌کند.

## محدودیت‌های عمدی

- PlatformSafety API، PlatformRole و Backoffice در STEP 15 هستند.
- provider واقعی، vendor SDK و MakanApp-AI استفاده نشده‌اند.
- report resolution پیام را خودکار حذف نمی‌کند.
- notification گزارش ساخته نشده است؛ reporter با status endpoint وضعیت را می‌بیند.
- appeal، escalation SLA، legal hold و retention duration حدس زده نشده‌اند.
- dashboard عمومی moderation، action delete/update و API حذف case وجود ندارند.
