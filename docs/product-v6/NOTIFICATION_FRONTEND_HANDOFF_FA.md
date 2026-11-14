# قرارداد اعلان، Deep Link و Push — BE-16

مبنا: شاخهٔ `feature/product-v6-backend-alignment`، Baseline=`76acf2024f51d39412d0d6f842f24afb5f26425a`. این سند قرارداد کد Backend است؛ نصب کلاینت، اجازهٔ سیستم‌عامل و دریافت واقعی Push را اثبات نمی‌کند. هیچ فایل Frontend تغییر نکرده است.

## جریان بازکردن اعلان

```text
Push / کلیک روی اعلان داخل برنامه
→ makan://notifications/{notificationId}
→ نشست معتبر کاربر
→ GET /api/v1/notifications/{notificationId}/target
→ current authorization
→ { kind, resourceId, apiRoute, requiredOrganizationId }
→ انتخاب صریح context لازم توسط کاربر
→ API مقصد با همان نشست و مجوز جاری
```

Payload هیچ مجوزی ایجاد نمی‌کند. مسیر دریافتی، API داخلی است؛ Screen کلاینت باید آن را به صفحهٔ خودش نگاشت کند. لینک مستقیم `makan://conversations/...` دیگر مقصد Push پیام نیست؛ `conversationId` اختیاری فقط hint است. همیشه ابتدا `notificationId` resolve شود. مقدار `apiRoute` را با host خارجی، URL دلخواه یا state قدیمی کلاینت جایگزین نکنید.

## APIهای موجود

همهٔ مسیرها به نشست فعال نیاز دارند. ورودی معتبر ولی متعلق به کاربر دیگر، در Read/Target با404 پنهان می‌شود.401 نیازمند ورود دوباره است؛404 می‌تواند ناشی از حذف، پایان دسترسی، دعوت منقضی یا context نامناسب باشد.

| Method و Route | ورودی | پاسخ و رفتار |
|---|---|---|
| `GET /api/v1/notifications` | `page`, `pageSize`, `unreadOnly`, `category`, `organizationId` اختیاری | `NotificationPage`: آیتم‌ها، صفحه، اندازه و تعداد؛ داده فقط برای مالک و منابع قابل نمایش |
| `GET /api/v1/notifications/unread-count` | `organizationId` اختیاری | `{ count }`؛ همان scope فهرست |
| `POST /api/v1/notifications/{id}/read` | بدون body | `NotificationItem`؛ Retry، `readAtUtc` اولیه را حفظ می‌کند |
| `GET /api/v1/notifications/{id}/target` | فقط شناسهٔ اعلان | `NotificationTargetResult` یا404؛ هیچ workspace/role/child به‌صورت ضمنی restore نمی‌شود |
| `GET /api/v1/notification-preferences` | `organizationId` اختیاری | دسته‌ها، defaults، quiet hours و `rowVersion` |
| `PUT /api/v1/notification-preferences/{category}` | `pushEnabled`, `smsEnabled`, `quietHoursEnabled`, `quietHoursStartLocal`, `quietHoursEndLocal`, `timeZoneId`, `expectedRowVersion` | تنظیم همان scope؛ زمان/منطقه نامعتبر400 و نسخهٔ متعارض409 |
| `GET /api/v1/notifications/devices` | `page`, `pageSize`, `includeRevoked` | متادیتای Device و Version؛ token، endpoint خصوصی و کلید subscription برنمی‌گردند |
| `GET /api/v1/notifications/devices/web-push/configuration` | ندارد | `{ isConfigured, vapidPublicKey }`؛ unavailable برابرfalse/null است |
| `POST /api/v1/notifications/devices/web-push` | `endpoint`, `p256dh`, `auth`, `expectedVersion` اختیاری | ثبت/به‌روزرسانی subscription وب برای UserSession جاری؛400/409/503 و rate limit ممکن است |
| `POST /api/v1/notifications/devices` | `platform`, `pushToken`، Version اختیاری؛ برای Web کلیدها نیز لازم‌اند | ثبت Android/iOS یا Web معتبر؛ token ورودی محرمانه است |
| `DELETE /api/v1/notifications/devices/{id}` | ندارد |204؛ فقط مالک می‌تواند revoke کند |

فیلدهای `NotificationItem`: `id`, `organizationId`, `category`, `type`, `title`, `body`, `resourceType`, `resourceId`, `createdAtUtc`, `readAtUtc`. صفحه از1 و pageSize بین1..100 است؛ query نامعتبر400 می‌گیرد. زمان‌ها UTC هستند؛ متن عمومی اعلان نباید جای Fetch دادهٔ مجاز مقصد را بگیرد. قرارداد کامل request/response در OpenAPI همان Source است.

## دسته‌ها و Preferences

| Typeها | Preference Category |
|---|---|
| `AssignmentPublished`, `ExamPublished` | `Assignments` |
| `AssignmentGradeReleased`, `ExamGradeReleased` | `Results` |
| `MessageReceived`, `MessageReply`, `MessageMention` | `Messages` |
| `OrganizationInvitation`, `ConversationInvitation`, `ConversationJoinRequestUpdated`, `PersonalClassInvitation`, `PersonalClassJoinRequestUpdated`, `PersonalClassManagerTransferUpdated` | `Invitations` |
| `MembershipChanged`, `OrderPaid`, `SupportUpdated`, `SchoolApplicationUpdated`, `PersonalClassRequestUpdated`, `PersonalClassMembershipChanged`, `PersonalClassStateChanged` | `ServiceUpdates` |

`NotificationCategory` سه مقدار Educational/Communication/AccountSystem دارد و با دستهٔ Preferences یکی نیست. Extension جدید AccountSystem است. پیش‌فرض دسته‌های قابل‌انتخاب Push=true و SMS=false است؛ `AccountSecurity` موجود Mandatory باقی مانده است. Preference یا Quiet Hours، receipt داخل برنامه را حذف نمی‌کند؛ quiet hours زمان تلاش بیرونی را عقب می‌اندازد. `IsExplicit=false` یعنی default مؤثر است.

## مقصدهای جدید

| ResourceType | `apiRoute` |
|---|---|
| `ConversationInvitation` | `/api/v1/messaging/invitations` |
| `ConversationJoinRequest` | برای درخواست‌کننده `/api/v1/messaging/join-requests`؛ برای مدیر مجاز `/api/v1/conversations/{conversationId}/join-requests` |
| `PersonalClassRequest` | `/api/v1/personal-class-requests/{id}` |
| `PersonalClassInvitation` | `/api/v1/personal-classes/invitations/page` |
| `PersonalClassJoinRequest` | درخواست‌کننده `/api/v1/personal-classes/join-requests`؛ مدیر مجاز همین مسیر با `?classId=...` |
| `PersonalClassManagerTransfer` | `/api/v1/personal-classes/manager-transfers` |
| `PersonalClassMembership` | `/api/v1/personal-classes/page`؛ پیام تغییر دسترسی، عضویت پایان‌یافته را احیا نمی‌کند |
| `PersonalClass` | `/api/v1/personal-classes/{id}` فقط با عضویت جاری؛ وضعیت Suspended/Archived ممکن است متادیتای فقط‌خواندنیِ مجاز داشته باشد، نه اختیار آموزشی یا Reopen |

دعوت Pending باید هنوز منقضی/پذیرفته/رد/لغو نشده و جاری قابل اقدام باشد. اعلان عضویت پایان‌یافته فقط می‌تواند به فهرست عمومیِ کلاس‌های خود کاربر هدایت کند؛ این با اجازهٔ ورود دوباره به کلاس فرق دارد. مالی‌بودن Host، پس از انتقال مدیریت، حق دریافت تغییرات کلاس ایجاد نمی‌کند.

مقصدهای قبلی Assignment/Exam/Result، Conversation، OrganizationInvitation، Membership، Order، Support و SchoolApplication حفظ شده‌اند. کارشناس Support/School فقط با permission جاری Route ادمین می‌گیرد؛ Role داخل Push یا داشتن شناسه کفایت نمی‌کند.

## سازمان و فرزند

در اعلان آموزشی سازمانی، ابتدا با `organizationId` اعلان و APIهای Workspace موجود، سازمان و نقش صحیح را صریحاً انتخاب کنید؛ برای Parent، فرزند مجاز نیز صریحاً انتخاب شود. سپس Target را resolve کنید. Backend انتخاب قبلی را خودکار بازنمی‌گرداند. اگر context اشتباه است، از404 وجود یا عدم وجود دادهٔ دیگری را استنتاج نکنید.

اگر Resolver، `requiredOrganizationId` برگرداند، انتخاب آن همچنان از مسیر صریح Workspace انجام می‌شود. تغییر سازمان بین دو درخواست، مجوز قبلی را نگه نمی‌دارد. پایان Membership/Student Role/Enrollment/Class یا Guardian جاری، Target آموزشی قدیمی را404 و تحویل بیرونی را Suppressed می‌کند. دادهٔ عمومی تاریخچه ممکن است باقی بماند؛ محتوا یا مجوز خصوصی در آن نیست.

## Foreground، Background و Retry

- Permission مرورگر/سیستم‌عامل فقط با تصمیم کاربر درخواست شود؛ رد Permission ورود یا دیدن Notification Center را مسدود نکند.
- در Web، پس از `isConfigured=true` از VAPID public key استفاده و subscription واقعی مرورگر را ثبت کنید؛ PublicKey ساختگی نسازید. کلید private هیچ‌وقت در API نیست.
- در Android/iOS، token همان نصب/پروژه را ثبت کنید؛ lifecycle و نمایش OS متعلق به کلاینت است. تنظیم APNs لازم برای iOS باید در پروژهٔ FCM/کلاینت آماده باشد.
- در Foreground، اعلان موجود را با `notificationId` به‌روزرسانی کنید. در Background، پس از کلیک و فعال‌شدن نشست، Target جاری را بگیرید. پردازش تکراری Push نباید unread یا آیتم تکراری بسازد.
- `tag` پایدار برابر شناسهٔ اعلان بدون خط تیره است؛ تحویل شبکه exactly-once تضمین نمی‌شود. ثبت Device قبلی را با Version معتبر به‌روزرسانی کنید.
- خاموشی Push یا نبود Device با شکست قابلیت In-app یکی نیست؛ فهرست و unread API fallback واقعی‌اند. در این مرحله event SignalR جدید برای Notification ساخته نشده است.
- Silent Message داخل برنامه می‌ماند ولی Push نمی‌گیرد؛ Muted Conversation و لغو دسترسی قبل از تحویل دوباره بررسی می‌شوند. دریافت Push قدیمی اجازهٔ خواندن پیام حذف‌شده یا context دیگری ایجاد نمی‌کند.
- پس از Logout/حذف حساب، token/session قدیمی را برای ثبت Device یا بازکردن هدف استفاده نکنید؛ Backend نیز نشست و Device جاری را بررسی می‌کند.

## وضعیت عملیاتی این مرحله

در فایل‌های خصوصی بررسی‌شدهٔ Test و Production، `Notifications:PushProvider=Unavailable` و `Notifications:SmsProvider=Unavailable` است؛ VAPID/FCM کامل موجود نیست. هیچ تنظیم خصوصی تغییر نکرد و Push واقعی اجرا نشد. Providerهای واقعی WebPush/FCM با HTTP کنترل‌شده تست می‌شوند. پیامک اعلان عمومی با OTP ملی‌پیامک جداست؛ تغییر این وضعیت نیازمند تنظیم امن و smoke مجاز طبق [Runbook](../operations/NOTIFICATION_PUSH_RUNBOOK_FA.md) است.
