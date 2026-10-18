# STEP 12A — اعلان داخلی و Outbox پایدار

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

اعلان داخلی منبع حقیقت ماندگار برای Notification Center است. کانال‌های بیرونی مانند Push، پیامک و Email در این مرحله وجود ندارند و خرابی احتمالی آن‌ها در مراحل بعد نباید رکورد داخلی را حذف کند.

## مرز ماژول و جریان اجرا

```text
AssignmentService / EvaluationService / ExamService / ExamGradingService
-> IOutbox.Enqueue(NotificationOutboxContract)
-> business state + platform.OutboxMessages در همان transaction
-> COMMIT
-> OutboxDispatcher
-> OutboxProcessor
-> notification.Notifications
-> NotificationsController
```

`MakanApp.Domain.Notification.Notification` مالک state خوانده‌شدن و metadata امن مقصد است. قراردادهای use case و portها در `MakanApp.Application.Notification` قرار دارند. EF Core، Outbox، processor و queryهای SQL Server در Infrastructure هستند و Controller فقط ورودی HTTP و شناسه کاربر احرازشده را به Application می‌دهد.

Outbox اختصاصی قدیمی `messaging.RealtimeOutbox` فقط pipeline تغییرات realtime پیام‌رسانی است و payload آن به `MessagingChangeEvent` وابسته است؛ business-event outbox عمومی محسوب نمی‌شود. این مرحله آن را دست‌کاری نکرده است. `platform.OutboxMessages` foundation عمومی eventهای کسب‌وکاری جدید است و Notification نخستین handler آن است. typeهای نسخه‌دار صریح‌اند و metadata نوع CLR یا `AssemblyQualifiedName` ذخیره نمی‌شود.

## مدل اعلان

هر رکورد شامل recipient، scope سازمانی اختیاری، category/type، عنوان و متن کوتاه، `ResourceType/ResourceId`، `CreatedAtUtc` و `ReadAtUtc` است. مقدار null در `ReadAtUtc` یعنی unread. `MarkRead` idempotent است و نخستین زمان خواندن را نگه می‌دارد.

Categoryهای محدود فعلی:

- `Educational`
- `Communication` برای توسعه آینده و بدون producer در این مرحله
- `AccountSystem` برای توسعه آینده و بدون producer در این مرحله

Typeهای واقعی این مرحله:

- `AssignmentPublished`
- `AssignmentGradeReleased`
- `ExamPublished`
- `ExamGradeReleased`

متن نتیجه شامل نمره، پاسخ، feedback خصوصی یا محتوای submission نیست. backend در این نسخه localization engine ندارد؛ بنابراین type ساخت‌یافته کنار متن فارسی کوتاه و امن ذخیره می‌شود تا client بتواند در آینده rendering را تکامل دهد.

## producer و resolution گیرنده

- انتشار تکلیف: User متناظر enrollment فعال همان کلاس.
- انتشار آزمون: User متناظر enrollment فعال همان کلاس.
- انتشار نتیجه تکلیف: learner مالک submission و GuardianRelationهای فعال همان learner در همان سازمان.
- انتشار نتیجه آزمون: learner مالک attempt و GuardianRelationهای فعال همان learner در همان سازمان.

relation والد در لحظه ایجاد event و داخل transaction سریال‌پذیر use case بررسی می‌شود. والد نامرتبط یا relation لغوشده recipient نیست. متن والد عمومی است و score، پاسخ و private note را افشا نمی‌کند. Teacher و Manager در این چهار event گیرنده نیستند؛ scope کاری یا operational event مستندی برای آن‌ها در جریان‌های فعلی وجود ندارد. Invitation endpoint ایجاد عمومی در کد فعلی وجود ندارد. تغییر/لغو Session وجود دارد، اما policy محصول درباره گیرنده‌های learner/parent/teacher و جزئیات قابل نمایش آن تثبیت نشده است؛ بنابراین به‌جای حدس‌زدن، producer آن به مرحله بعدی تصمیم محصول موکول شد.

## مجوز و scope

اعلان مجوز نیست. `OrganizationId` منبع notification را مشخص می‌کند و inbox می‌تواند با filter چند سازمان را نمایش دهد، اما `ResourceType/ResourceId` فقط navigation hint است. endpoint مقصد باید `AccessContext`، Membership، Enrollment، TeacherAssignment یا GuardianRelation جاری را دوباره ارزیابی کند. لغو دسترسی اعلان تاریخی را لزوماً حذف نمی‌کند و هرگز دسترسی را احیا نمی‌کند.

API فقط `UserId` استخراج‌شده از session معتبر را استفاده می‌کند و `RecipientUserId` از client نمی‌پذیرد. lookup برای mark-read همزمان شناسه اعلان و recipient را filter می‌کند؛ حدس شناسه کاربر دیگر با 404 پنهان می‌شود.

## API Notification Center

| Method | Route | رفتار |
|---|---|---|
| `GET` | `/api/v1/notifications` | فهرست صفحه‌بندی‌شده فقط برای کاربر جاری |
| `GET` | `/api/v1/notifications/unread-count` | `COUNT` مستقیم SQL برای badge |
| `POST` | `/api/v1/notifications/{notificationId}/read` | خواندن idempotent اعلان خود کاربر |

فیلترهای list عبارت‌اند از `page`، `pageSize` با سقف ۱۰۰، `unreadOnly`، `category` و `organizationId`. query با `AsNoTracking`، projection و ترتیب `(CreatedAtUtc DESC, Id DESC)` اجرا می‌شود و entity یا navigation graph را load نمی‌کند. unread count رکوردها را به حافظه منتقل نمی‌کند.

## دوام، idempotency و concurrency Outbox

producer ابتدا business state و پیام event را به همان `MakanDbContext` اضافه می‌کند و store موجود هر دو را در transaction جاری ذخیره می‌کند. پردازش at-least-once است:

- هر پیام `MessageId` مستقل دارد و شناسه پایدار رخداد را در `SourceEventId` payload حمل می‌کند.
- هر recipient یک پیام کوچک Outbox دارد و همه پیام‌های یک رخداد `SourceEventId` مشترک دارند؛ بنابراین اندازه payload با تعداد اعضای کلاس رشد نمی‌کند.
- unique index روی `(SourceEventId, RecipientUserId)` از materialization تکراری جلوگیری می‌کند.
- worker batch را با `UPDLOCK, READPAST, ROWLOCK` claim می‌کند.
- هر claim یک `ClaimToken`، lease سی‌ثانیه‌ای و `rowversion` دارد.
- notificationها و `ProcessedAtUtc` در یک transaction ثبت می‌شوند.
- claim منقضی بعد از restart قابل بازیابی است.
- خطا با backoff درجه‌دوم محدود به ۳۰۰ ثانیه، `NextAttemptAtUtc` و کد امن `notification_processing_failed` باقی می‌ماند؛ stack trace در جدول یا API ذخیره نمی‌شود.

Channel حافظه‌ای منبع حقیقت نیست و worker هر دو ثانیه SQL Server را poll می‌کند. realtime اعلان در STEP 12A فعال نشده است؛ در نتیجه شکست SignalR اثری بر قابلیت ندارد و Notification Center همیشه از SQL Server می‌خواند.

## SQL Server و migration

Migration: `20261012154542_AddNotificationFoundation`.

جدول `notification.Notifications` دارای FK محدودکننده به `identity.Users` و organization اختیاری، check constraint برای enumها و زمان read، `rowversion`، indexهای `(RecipientUserId, CreatedAtUtc)` و `(RecipientUserId, ReadAtUtc)` و unique deduplication است.

جدول عمومی `platform.OutboxMessages` type/payload نسخه‌دار، زمان‌های claim/retry/process، attempt، error code امن و `rowversion` دارد. filtered index pending مسیر polling را پشتیبانی می‌کند. migration در startup اجرا نمی‌شود و در این مرحله روی production اعمال نشده است.

## تست‌ها

Unit testها ایجاد notification، UTC، unread اولیه و idempotency read را بررسی می‌کنند. Integration testها migration واقعی LocalDB، ownership، anti-enumeration، scope دو سازمان، pagination/filter، unread count، claim concurrent، deduplication، persistence بین scopeهای تازه، failure قابل بازیابی و retry پس از repair را پوشش می‌دهند. تست‌های موجود publish/release نیز با producerهای متصل اجرا می‌شوند.

## خارج از محدوده STEP 12A

Push/FCM/APNs/Expo/OneSignal، پیامک عمومی، Email، preferences، Quiet Hours، SignalR اعلان، mark-all-read، scheduling، AI، Commerce و Backoffice پیاده‌سازی نشده‌اند. OTP ملی‌پیامک مستقل است و به‌عنوان کانال اعلان عمومی استفاده نمی‌شود.
