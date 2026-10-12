# STEP 14C — سخت‌سازی رفتار پیام‌رسان و وضعیت گفتگو

این سند قرارداد پیاده‌سازی‌شده در STEP 14C را ثبت می‌کند. تصمیم‌های پایه‌ی
`MESSENGER_V2_PRODUCT_DECISIONS.md` حفظ شده‌اند و این مرحله فقط behaviorهای پیام، تاریخچه،
دسترسی عمومی، permissionهای گفتگو و state خصوصی هر کاربر را تکمیل می‌کند.

## 1. مرز مرحله

در این مرحله موارد زیر پیاده‌سازی شدند:

- پنجره‌ی دقیق یک‌ساعته‌ی Edit و sender Delete For Everyone بر اساس `SentAtUtc` و `TimeProvider`؛
- حذف moderator بدون محدودیت زمانی، فقط برای Owner یا Admin دارای `DeleteMessages`؛
- Delete For Me بدون محدودیت زمانی و با visibility خصوصی کاربر؛
- `FullHistory` و `FromJoin` با snapshot پایدار `HistoryVisibleFromSequence`؛
- `Private` و `Public` مستقل از `Personal` و `Organization`؛
- discovery و self-join محدود، صفحه‌بندی‌شده و authorization-aware؛
- permissionهای granular برای Admin و capabilityهای قابل enforce برای عضو Group؛
- archive، mute، pin گفتگو و manually-unread به‌صورت per-user؛
- فیلتر و pagination فهرست گفتگو و pagination اعضا؛
- اعمال visibility یکسان در History، Search، Shared Media، File، Reply، Forward و Delta.

Saved Messages عمداً پیاده‌سازی نشد. مدل فعلی `ConversationType` فقط Direct/Group/Channel دارد و
افزودن Saved نیازمند تغییر discriminator، همه‌ی switchهای authorization، invariant زوج Direct،
قواعد participant و uniqueness اختصاصی User است. پیاده‌سازی آن در این مرحله ریسک ایجاد راه فرعی
در invariantهای تثبیت‌شده را داشت؛ بنابراین به STEP 14E منتقل شد.

## 2. قواعد پیام

مهلت Edit و sender Delete For Everyone چنین است:

```text
nowUtc <= SentAtUtc + 1 hour  => allowed
nowUtc >  SentAtUtc + 1 hour  => rejected
```

زمان کلاینت ورودی تصمیم نیست و Edit قبلی deadline را تمدید نمی‌کند. Admin حق ویرایش محتوای شخص
دیگر را ندارد. حذف moderator از مسیر Delete For Everyone استفاده می‌کند، اما فقط Owner یا Admin
دارای `DeleteMessages` می‌تواند پیام دیگری را—even بعد از یک ساعت—به tombstone تبدیل کند.
`DeletedByUserId` و `MessagingChangeEvent.ActorUserId` عامل و زمان را نگه می‌دارند. revisionهای
داخلی حذف نمی‌شوند و endpoint عمومی revision history وجود ندارد.

مسیر صریح حذف عمومی
`DELETE /api/v1/conversations/{conversationId}/messages/{messageId}/for-everyone`
است. مسیر قدیمی بدون suffix برای سازگاری همان معنی Delete For Everyone را حفظ کرده است. مسیر
`.../for-me` یک `MessageUserVisibility` یکتا برای Message/User می‌سازد و retry آن idempotent است.

## 3. visibility واحد پیام

پیام برای کاربر فقط وقتی قابل استفاده است که:

- participant فعال باشد و scope/workspace معتبر بماند؛
- `Sequence >= HistoryVisibleFromSequence` باشد؛
- tombstone عمومی نباشد، مگر در History که placeholder tombstone نمایش داده می‌شود؛
- برای همان کاربر در `MessageUserVisibilities` مخفی نشده باشد.

این predicate در History، Search، Media، دانلود FileAsset، منبع Forward، هدف Reply، عملیات
Reaction/Pin/Report و Delta اعمال می‌شود. اگر reply source برای caller مخفی یا قبل از boundary باشد،
فقط placeholder با `isUnavailable=true` و بدون متن، نوع یا هویت فرستنده برمی‌گردد.

## 4. تاریخچه Group

`GroupHistoryPolicy` دو مقدار دارد:

- `FullHistory`: boundary عضو برابر `1`؛
- `FromJoin`: boundary عضو جدید برابر `Conversation.NextMessageSequence` در لحظه join.

تغییر setting فقط joinهای آینده را تحت تأثیر قرار می‌دهد. boundary اعضای موجود بازنویسی نمی‌شود.
Direct این policy را مصرف نمی‌کند. Channel فعلاً `FullHistory` ثابت دارد؛ policy مستقل subscriber
برای Channel به STEP 14E موکول شده است.

## 5. Public و Private

`ConversationVisibility` از `ConversationScope` جداست. Personal Public برای حساب معتبر Makan
قابل کشف است. Organization Public فقط در workspace همان سازمان و با Membership فعال قابل کشف و
join است. دانستن Guid برای Private مجوز نمی‌سازد. `SystemManagedAcademic` همواره Private و
غیرقابل self-join است و membership مرجع آموزشی خود را حفظ می‌کند.

badge سازمان فقط `OrganizationId` و `OrganizationName` دارد. MembershipId، RoleAssignment یا
اطلاعات billing در DTO قرار نمی‌گیرند.

## 6. permission و capability

Owner اختیار ضمنی کامل مدیریت گفتگو دارد. Admin فقط permissionهای صریح زیر را دارد:

- `ManageMembers`
- `RemoveMembers`
- `ManageAdmins`
- `DeleteMessages`
- `PinMessages`
- `PublishMessages`
- `ManageHistory`
- `ManageVisibility`

Admin نمی‌تواند permissionی را که خودش ندارد به Admin دیگر بدهد. update permission با rowversion و
`expectedVersion` انجام می‌شود. Member permission مدیریتی ندارد. capabilityهای enforceشده‌ی Group
عبارت‌اند از `SendMessages`، `SendMedia` و `InviteMembers`. در Channel عضو عادی publish نمی‌کند؛
Owner همیشه و Admin فقط با `PublishMessages` منتشر می‌کند.

Backfill Adminهای قبلی دقیقاً برابر رفتار قابل دسترس قبل از 14C است:

```text
ManageMembers | RemoveMembers | PinMessages | PublishMessages = 51
```

`DeleteMessages`، `ManageAdmins`، `ManageHistory` و `ManageVisibility` به آن‌ها خودکار داده نمی‌شود.
برای ارتقای Admin جدید، مقدار پیش‌فرض `None` است و permissionها باید صریحاً انتخاب شوند؛ مقدار `51`
فقط برای backfill سازگار Adminهای موجود و demotion ناشی از انتقال مالکیت نگه داشته شده است.

## 7. state خصوصی گفتگو

`ConversationUserState` با کلید `(ConversationId, UserId)` نگه‌داری می‌شود و شامل archive، pin،
mute typed، manually-unread، زمان update و rowversion است. archive دیگر lifecycle مشترک گفتگو را
تغییر نمی‌دهد؛ `Conversation.Status.Archived` فقط برای legacy/internal lifecycle باقی مانده و
endpoint عمومی archive آن را mutate نمی‌کند. پیام جدید archive کاربر را خودکار باز نمی‌کند.

Mute یا `Permanent` است یا `Until` با timestamp صریح UTC؛ `DateTime.MaxValue` استفاده نمی‌شود.
Mute پیام، unread، persistence یا SignalR را متوقف نمی‌کند و فقط state لازم برای notification
policy آینده را فراهم می‌کند. producer اعلان پیام در این مرحله ساخته نشده است.

Mark Unread فقط `IsManuallyUnread` را فعال می‌کند و `LastReadMessageSequence` را عقب نمی‌برد.
Mark Read واقعی این flag را پاک می‌کند. تغییر state خصوصی با ChangeEvent دارای `AudienceUserId`
همان کاربر منتشر می‌شود و برای participantهای دیگر broadcast نمی‌شود.

## 8. APIهای مهم

| Method | Route | قرارداد |
|---|---|---|
| `GET` | `/api/v1/conversations?filter=&page=&pageSize=` | فیلتر All/Direct/Group/Channel/Unread/Archived؛ سقف صفحه 100 |
| `GET` | `/api/v1/conversations/discover?type=&q=&page=&pageSize=` | discovery امن Public در context جاری |
| `POST` | `/api/v1/conversations/{id}/join` | self-join Public؛ Private/system-managed مخفی می‌ماند |
| `PATCH` | `/api/v1/conversations/{id}/settings` | Visibility/History/Capabilities با rowversion |
| `GET` | `/api/v1/conversations/{id}/members?page=&pageSize=` | اعضای فعال، محدود و صفحه‌بندی‌شده |
| `PATCH` | `/api/v1/conversations/{id}/members/{userId}/permissions` | permissionهای Admin با rowversion |
| `DELETE` | `/api/v1/conversations/{id}/messages/{messageId}/for-everyone` | sender در یک ساعت یا moderator مجاز |
| `DELETE` | `/api/v1/conversations/{id}/messages/{messageId}/for-me` | مخفی‌سازی خصوصی و idempotent |
| `POST/DELETE` | `/api/v1/conversations/{id}/archive` | archive/unarchive خصوصی |
| `POST/DELETE` | `/api/v1/conversations/{id}/state/pin` | pin/unpin خصوصی گفتگو |
| `PUT/DELETE` | `/api/v1/conversations/{id}/mute` | mute typed/unmute |
| `POST` | `/api/v1/conversations/{id}/mark-unread` | state نمایشی بدون rollback رسید خواندن |

خطاهای پایدار افزوده‌شده عبارت‌اند از `MESSAGE_EDIT_WINDOW_EXPIRED`،
`MESSAGE_DELETE_WINDOW_EXPIRED`، `CONVERSATION_HISTORY_NOT_VISIBLE`،
`CONVERSATION_PERMISSION_REQUIRED`، `CONVERSATION_NOT_JOINABLE` و
`CONVERSATION_STATE_CONFLICT`.

## 9. SQL Server و migration

Migration این مرحله `20261012212650_HardenMessengerConversationBehavior` است و:

- ستون‌های Visibility، GroupHistoryPolicy و MemberCapabilities را به Conversations اضافه می‌کند؛
- ستون‌های HistoryVisibleFromSequence و AdminPermissions را به ConversationParticipants می‌افزاید؛
- جدول‌های `messaging.ConversationUserStates` و `messaging.MessageUserVisibilities` را می‌سازد؛
- check constraintهای enum/flags/shape، PK/FK، index discovery/list و rowversion state را تعریف می‌کند؛
- ستون‌های موجود را ابتدا nullable می‌افزاید، سپس با Private=1، FullHistory=1، boundary=1 و permission
  سازگار backfill و در پایان NOT NULL می‌کند؛
- Groupهای قبلی capability ارسال متن/رسانه (`3`) و Channel/Direct مقدار `0` می‌گیرند؛
- archiveهای مشترک قدیمی را به state شخصی تبدیل نمی‌کند، چون مالک archive تاریخی مبهم است.

Migration تولید و inspect شده، اما startup آن را خودکار اجرا نمی‌کند و در این مرحله روی دیتابیس
developer/production اعمال نشده است. تست Integration migration را فقط روی LocalDB ایزوله اجرا می‌کند.

## 10. محدودیت‌های عمدی

- Saved Messages و Channel history policy مستقل در STEP 14E باقی مانده‌اند.
- invite link قابل revoke، view count کانال، retention/legal policy و moderation dashboard اضافه نشده‌اند.
- poll و permission مستقل Link وجود ندارد، چون backend فعلی آن‌ها را enforce نمی‌کند.
- محدودیت محصولی hard-coded برای اندازه فایل یا تعداد اعضا اضافه نشده است. سقف configurable فایل
  فقط Technical Safety Limit ورودی HTTP است، نه Product Limit.
- notification producer پیام هنوز وجود ندارد؛ mute state برای مصرف آینده آماده است.
