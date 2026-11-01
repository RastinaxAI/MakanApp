# قرارداد جاری پیام‌رسان پس از سیاست ۲۰۲۶/۱۱/۰۱

این مرجع بر تصمیم‌های سنیِ قدیمی در Handoffهای P0/P1/P2، GAP-05 و Official Spaces مقدم است. سایر محافظ‌های مستقل آن اسناد معتبر می‌مانند. فقط Backend تغییر کرده است؛ Frontend اجرا، Clone یا ویرایش نشده است. [شرح پیاده‌سازی](MESSAGING_POLICY_20261101_IMPLEMENTATION_FA.md).

## تغییر معنا و سازگاری

**BREAKING SEMANTIC CHANGE:** `verificationStatus/effectiveAgeCategory/DateOfBirth` مجوز Messaging نیستند. فیلدهای قدیمی برای سازگاری باقی‌اند. Unknown، Minor، Unverified یا Conflict نباید دکمهٔ Direct/Group/Channel را صرفاً به دلیل سن خاموش کنند.

`GET /api/v1/me/messaging-status` همان Self-only Endpoint است. با پروفایل ضروری کامل و بدون محدودیت جاری، `nextAction=None` و `allowedActions` شامل `ReadExistingConversations/DiscoverUsers/CreateDirect/CreatePersonalGroup/CreatePersonalChannel` است؛ حتی اگر Workspace منتخب Organization باشد. کمبود واقعی نام/Username/پروفایل همچنان `ProfileCompletionRequired` و محدودیت واقعی حساب `MessagingRestrictionActive` است. Version با `messaging-20261101:` شروع می‌شود؛ آن را Parse یا با RowVersion سایر منابع مقایسه نکنید.

PrivacyAudience، Block، نشست، عضویت، TeacherAssignment، Enrollment، محدودیت کاربر و Safety همچنان بررسی می‌شوند. نقش مدرسه مجوز مدیریت گفتگوی شخصی دیگران نیست. Guardian به Direct خصوصی فرزند دسترسی اضافه نمی‌گیرد.

## ساخت Group/Channel و نتیجهٔ مخاطبان

همهٔ انواع حساب مجاز با `scope: Personal` از همان `POST /api/v1/conversations/groups` و `/channels` استفاده می‌کنند. نیازی به تغییر Workspace منتخب نیست. شکل Request حفظ شده است:

```json
{
  "clientOperationId": "<new-guid>",
  "scope": "Personal",
  "title": "عنوان گفتگو",
  "description": null,
  "initialParticipantUserIds": ["<target-guid>"],
  "visibility": "Private",
  "groupHistoryPolicy": "FullHistory"
}
```

201 برای ایجاد و 200 برای Retry یکسان. سازنده Owner است. پاسخ `ManagedConversationResult` علاوه بر فیلدهای قبلی `initialTargets/effectiveCapabilities/photo` دارد:

```json
{
  "initialTargets": [
    {
      "targetUserId": "<target-guid>",
      "outcome": "InvitationPending",
      "invitationId": "<invitation-guid>",
      "invitationExpiresAtUtc": "<server-utc>"
    }
  ]
}
```

- `InvitationPending`: دعوت واقعی با مهلت پیش‌فرض **۳۰ روز از ساعت سرور**؛ عضو فعال نیست. UI موفقیت ساخت را از پذیرفته‌شدن عضو جدا نشان دهد.
- `Added`: عضویت سازمانی طبق قواعد مجاز موجود؛ برای Personal، عضویت جدید از پذیرش دعوت می‌آید.
- `NotAvailable`: مخاطب ورودی برای این عمل در دسترس نیست. از آن وجود حساب، Block یا علت خصوصی را نتیجه نگیرید.

این آرایه Snapshot اولیه و فقط برای سازنده است؛ Retry همان Snapshot را برمی‌گرداند. وضعیت جاری دعوت را از `GET /api/v1/messaging/invitations` و اعضا بخوانید. API پذیرش همان `POST /api/v1/messaging/invitations/{invitationId}/accept` با نشست خود مخاطب و بدون بدنه است. Decline/Revoke نیز همان مسیرهای موجودند. افزودن مستقیم Personal در `/members` جای رضایت نیست و 409 `CONVERSATION_INVITATION_REQUIRED` می‌دهد.

ACK گم‌شده: همان `ClientOperationId` و Payload را دوباره بفرستید. Payload متفاوت با همان شناسه 409 `CONVERSATION_CREATION_CONFLICT` است؛ برای اصلاح عمدی Request شناسهٔ عملیات جدید بسازید. پذیرش دوباره، Participant دوم نمی‌سازد. Timestamp انقضا را از Response بخوانید.

## قابلیت مؤثر و کاتالوگ

Summary، Managed Detail و هر مقصد Official Pair، `effectiveCapabilities` دارند:

```json
{
  "canEditInfo": false,
  "canManageMembers": false,
  "canManageMessages": false,
  "canPublish": false,
  "canManageAdmins": false,
  "canChangeSettings": false,
  "canInviteMembers": false,
  "canManageInvites": false,
  "canBanMembers": false,
  "canManageTopics": false
}
```

این مقادیر را از Role یا سن حدس نزنید. حالت loading/error مجوز تازه‌ای ایجاد نمی‌کند. Capability راهنمای UI است؛ پاسخ نهایی API، نسخه، Safety و مجوز جاری ملاک‌اند. `canManageMessages` اختیار حذف مجاز است و امکان ویرایش پیام دیگران ایجاد نمی‌کند. `canManageMembers` برای حذف اعضای مجاز و `canInviteMembers` برای دعوت تفکیک شده‌اند. `canChangeSettings` اختیار کامل تنظیمات را نشان می‌دهد؛ ممکن است یک permission جزئی روی Endpoint مربوطه مجاز باشد.

`GET /api/v1/conversations/permissions` برای حساب واردشده، آرایهٔ descriptorهای واقعی permission اتمی با `key/category/flag/description/conversationTypes/ownerCanGrant/adminCanDelegate/appliesToUserManaged/appliesToSystemManagedAcademic/officialRelationshipDerivedOnly/delegationRequirement` می‌دهد. ۱۳ Admin Permission و ۳ Member Capability دارد. قابل‌تفویض‌بودن descriptor به معنی داشتن آن توسط Actor نیست. در Official، مدیریت منبع‌محور است و هیچ تفویض دستی Admin انجام نمی‌شود.

## فضاهای رسمی

APIهای موجود `/api/v1/organization/official-spaces` و `/api/v1/organization/classes/{classId}/official-spaces` مرجع شناسهٔ جفت رسمی هستند. Pending/RepairRequired را Ready فرض نکنید؛ GET ساخت تازه نمی‌کند. پس از invalidation، Pair/Summary را دوباره بخوانید. عنوان رسمی از نام منبع می‌آید و با همان ConversationId به‌روزرسانی می‌شود.

Manager و Teacherهای دارای Assignment معتبر: Description/Photo، حذف/Pin پیام مجاز، انتشار Channel و Topic گروه طبق قابلیت فعلی. Title مستقل تغییر نمی‌کند. اعضای رسمی Member می‌مانند؛ از Role Member نتیجه نگیرید که Teacher اختیار محتوا ندارد. اختیار roster/admin/invite/ban/settings رسمی از این مرحله اعطا نشده است؛ تغییر اعضای کلاس از آموزش انجام می‌شود. Student گروه را می‌فرستد و Channel را می‌خواند. Guardian به‌تنهایی عضو کلاس نیست.

با پایان Assignment/Enrollment/Role، هر درخواست مجوز فعلی را می‌سنجد. پایان یک دلیل، دلیل معتبر دیگر را پاک نمی‌کند. خطای 404/403 را با State قدیمی override نکنید. Personal مستقل باقی می‌ماند.

## صندوق یکپارچهٔ حساب

```text
GET /api/v1/conversations/account?filter=All&limit=30&search=<text>&cursor=<opaque>
```

پاسخ `items/nextCursor/hasMore` است. `items` همان Conversation Summary با Preview/Unread/Organization Badge/Official Metadata/Photo/Capabilities است. فیلترهای واقعی `All/Unread/Archived/Direct/Group/Channel/Saved` و Search روی کل SQL اعمال می‌شوند. Limit: ۱ تا ۱۰۰؛ پیش‌فرض ۳۰. Search حداکثر ۱۰۰ کاراکتر. Archived فقط آرشیوشده‌ها را برمی‌گرداند؛ سایر فیلترها آرشیوشده‌ها را کنار می‌گذارند.

ترتیب سراسری نزولی: Pinned، آخرین فعالیت قابل مشاهده یا زمان ایجاد، Guid به ترتیب SQL Server. لیست‌های scoped را در کلاینت برای ساخت این صندوق ادغام نکنید. Cursor حداکثر ۲۴ ساعت، به User/Session/Filter/Search/Limit بسته است؛ بعد از تغییر فیلتر یا Limit از صفحهٔ اول شروع کنید. Cursor خراب/نمای دیگر/کاربر دیگر 400 `MESSAGE_PAGE_INVALID` می‌دهد. Cursor فقط مرز keyset است و Snapshot ثابت در برابر تغییر هم‌زمان Activity/Pin نیست؛ پس از invalidation صفحهٔ اول را تازه کنید و State را با ConversationId upsert کنید.

Workspace منتخب عوض نمی‌شود. مسیر قدیمی `GET /api/v1/conversations` همان پاسخ آرایه‌ای و انتخاب scope قبلی را دارد. User/Role/Organization ID در Badge selector هستند؛ مجوز تازه‌ای نیستند. پس از revoke، صفحهٔ بعد و Refetch مجوز جاری را دوباره اعمال می‌کنند.

## Realtime صندوق و Replay

همان Hub `/hubs/messaging` با Authorization Header معتبر استفاده می‌شود. SubscribeConversation برای رویدادهای scoped قبلی حفظ شده است؛ برای دریافت invalidation صندوق حساب نیازی به بازکردن Chat نیست. پس از اتصال، `ClientInvalidated` با `key=messaging.account-inbox` را مصرف کنید:

```json
{
  "eventId": "<event-guid>",
  "key": "messaging.account-inbox",
  "resourceId": null,
  "contextRevision": "<opaque-account-view-revision>",
  "streamVersion": "<opaque-version>",
  "occurredAtUtc": "<server-utc>"
}
```

این رویداد متن یا شناسهٔ Message/Organization خصوصی ندارد. صندوق را Refetch کنید؛ دریافت Event به‌تنهایی نباید Unread را `+1` یا Message/Conversation جدید بسازد. به `ConversationChanged` قبلی نیز به‌عنوان قرارداد جدا گوش کنید. دو نوع رویداد ممکن است یک تغییر کسب‌وکاری را منعکس کنند؛ اثر را دوبار اعمال نکنید.

الگوی Cold Start/Reconnect:

1. بدون Cursor، `GET /api/v1/realtime/account-inbox/changes?limit=100`؛ مرز را از `nextCursor` بگیرید.
2. Snapshot صندوق را بخوانید؛ اتصال زنده را برقرار کنید.
3. از مرز مرحلهٔ ۱ Replay را drain کنید؛ `changes/nextCursor/hasMore/retryAfterMilliseconds` را رعایت کنید.
4. Eventهای تکراری را با `(eventId, streamVersion)` dedupe، صندوق را با ConversationId upsert و Cursor را پس از مصرف صفحه ذخیره کنید.
5. پس از Reconnect از Cursor مصرف‌شده ادامه دهید. Cursor منقضی/نامعتبر/بافت دیگر 409 `REALTIME_RESYNC_REQUIRED` است؛ Snapshot جدید بگیرید.

Replay جدید فقط نمای حساب است؛ `conversation` در Change تهی و `invalidation` حاضر است. Limit بین ۱ و ۲۰۰ است. Cursor به User/Session/Epoch/نما بسته است و با `/realtime/changes`، Cursor صفحه‌بندی صندوق یا نشست دیگر قابل تعویض نیست. ContextRevision این نما مستقل از Organization منتخب است. مجوز هر Event در Live و Replay جاری بررسی می‌شود. هیچ تضمین exactly-once وجود ندارد؛ حمل‌ونقل حداقل یک‌بار و Refetch مرجع State است.

## Publication Receipt و Card

Responseهای انتشار قبلی Lesson/Assignment/Exam و چرخهٔ Session، فیلد افزایشی `publicationReceipt` دارند:

```json
{
  "eventId": "<publication-guid>",
  "activityKind": "Lesson",
  "sourceId": "<source-guid>",
  "sourceVersionId": "<version-guid>",
  "sourceRevision": 1,
  "deliveryState": "Pending"
}
```

Pending یعنی ثبت در تراکنش، نه تحویل به دو مقصد. Delivery موجود را با Class/Event بخوانید. `Delivered/Superseded/Pending` بر اساس وضعیت واقعی دو مقصد است؛ برای علت و وضعیت هر مقصد از Response delivery موجود استفاده کنید. Retry انتشار مطابق خطاهای وضعیت قبلی همان Source است؛ انتشار دوباره را به‌عنوان راه ساخت Activity جدید به کار نبرید.

Session نوع جدید Card با `sessionAction=Scheduled/Rescheduled/Cancelled/Completed` دارد. Card قدیمی Superseded یا منبع منقضی/لغوشده Actionable نیست؛ از `isActionable/state` و دادهٔ Source فعلی استفاده کنید. متن خصوصی پاسخ، Attachment پاسخ، Grade، کلید آزمون و evaluator note در Card نداریم. Published Educational Note مستقل فعلاً مدل/قرارداد ندارد؛ UI مسیر خیالی نسازد.

## عکس گفتگو و لینک

| عملیات | Request / Response | کنترل و خطاهای مهم |
|---|---|---|
| `PUT /api/v1/conversations/{id}/photo` | `{fileAssetId, expectedVersion}` → `{conversationId, photo, version}` | Bearer، capability جاری، فایل Ready متعلق به Actor در scope مجاز، JPEG/PNG؛ 400 ورودی، 403 اختیار، 404 فایل/منبع غیرمجاز، 409 آماده‌نبودن، 412 نسخه، 415 نوع |
| `DELETE /api/v1/conversations/{id}/photo?expectedVersion=...` | نسخهٔ آخر → Photo تهی و Version | همان مجوز؛ حذف Binding بدون حذف Blob؛ 400/403/404/412 |
| `GET /api/v1/files/{fileAssetId}/content` | پاسخ فایل از `photo.downloadUrl` | ACL جاری گفتگو؛ دعوت Pending و FileAssetId به‌تنهایی کافی نیست؛ پس از revoke برابر 404 |

Upload همان Storage موجود است؛ در Workspace آموزشی برای فایل شخصی، `workspaceScope=Personal` را طبق قرارداد قبلی بفرستید. از ProfilePhoto به‌عنوان Binding عکس گفتگو استفاده نکنید. فایل متصل به پیام/آموزش/مدرک Moderation، Pending یا Retained قابل انتقال نیست. در Summary/Detail، `photo=null` یعنی عکس متصلِ قابل نمایش وجود ندارد. PUT تکراری همان Binding اثر دوم نمی‌سازد؛ جایگزینی متفاوت با Version قدیمی 412 است و باید Refetch شود.

Invite Link موجود قرارداد Share است؛ ایجاد/لغو، expiry، Join Request و مجوز جاری همان قواعد قبلی را دارند. Public Handle جدید نداریم. لینک محرمانهٔ دعوت را در Log/Telemetry ثبت نکنید.

## خطا، Safety و Stateهای UI

- 401: نشست نامعتبر؛ دادهٔ قبلی را به مجوز تبدیل نکنید.
- 403: مجوز/Block/محدودیت جاری؛ 404: نبود یا پنهان‌سازی منبع. علت خصوصی حدس زده نشود.
- 409: تضاد Idempotency، دعوت لازم، فایل ناآماده یا Resync؛ `code/errorCode` واقعی ProblemDetails را بخوانید.
- 412: نسخهٔ قدیمی؛ Refetch و تصمیم کاربر، بدون بازنویسی خودکار تغییر دیگران.
- 422 `MESSAGE_SAFETY_REVIEW_REQUIRED` یا `MESSAGE_SAFETY_BLOCKED`: پیام تحویل نشده است. PendingReview با Rejected یا Approved/Attachable یکی نیست؛ فایل را برای گیرنده Delivered/Downloadable نشان ندهید. Retry همان شناسه اثر یا مصرف تکراری نمی‌سازد.
- 503 `MESSAGE_SAFETY_UNAVAILABLE`: ارسال موفق نیست؛ Safety bypass یا نمایش موفقیت خوش‌بینانه نکنید.
- 429: Backoff طبق قرارداد موجود؛ Refresh مکرر جای Replay را نگیرد.

سن از خطاهای بالا نتیجه نمی‌شود. Stateهای `loading/empty/pending-invitation/pending-delivery/pending-review/rejected/revoked/stale-version/resync-required` جدا باشند. Payload خصوصی، شماره، JWT، OTP یا Secret در Log کلاینت یا Evidence قرار نگیرد. OpenAPI زندهٔ همین Build در `/openapi/v1.json` مرجع Route/DTO است؛ JSON خروجی محلی در شواهد مرحله ذخیره شده و پذیرش سرور محسوب نمی‌شود.

## محدودیت‌ها

برای Student Plan age/evidence، Parent full-class audience، Official history retention، Handle عمومی جدید، Published Note و workflow نهایی ReviewRequired تصمیم محصول حدس زده نشده است. AI/RAG، OTP، MFA و MeliPayamak تغییر نکردند. AI بدون DOB در بافت مجاز موجود با Fake Provider تست می‌شود؛ مجوز عمومی AI جدیدی ایجاد نشده است. این مرحله Publish یا Deploy ندارد.
