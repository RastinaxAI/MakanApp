# تحویل قرارداد حساب شخصی و فضاهای رسمی

این سند فقط قرارداد Backend است؛ هیچ Frontend در این مرحله تغییر یا اجرا نشده است. پاسخ‌های role/context قبلی نباید پس از تغییر فضای منتخب روی state جدید اعمال شوند. کلاینت باید generation درخواست را نگه دارد و پاسخ دیررس نسل قبلی را کنار بگذارد.

## کارت Personal

`GET /api/v1/workspaces/snapshot` با پاسخ `WorkspaceSnapshotResult`، `Cache-Control: no-store` و Authorization معمول نشست:

```json
{
  "version": "opaque-snapshot-version",
  "hasEffectiveEducationalRole": true,
  "personalWorkspaceVisible": false,
  "personalVisibilityDecision": "Hidden",
  "workspaces": []
}
```

آرایهٔ واقعی، Personal و روابط آموزشی همان حساب را با `workspaceType / organizationId / membershipId / role / membershipStatus / roleStatus / organizationStatus / isEffective / canEnter / isVisible` دارد. شناسه‌ها selector هستند؛ کلاینت permission نمی‌سازد. Personal role ندارد.

- `false`: کارت Personal پنهان؛ Profile/Contacts/Personal Messaging/Individual Commerce همچنان در دسترس.
- `true`: کارت Personal نمایان.
- `null + ProductDecisionRequired`: حالت تعلیق حل‌نشده؛ آن را بدون نقش یا خطای شبکه فرض نکنید.
- loading/error را به snapshot «بدون نقش» تبدیل نکنید؛ refresh معتبر بگیرید.
- `version` را opaque بدانید؛ ترتیب زمانی از مقدار hash استنباط نکنید.

Select/current موجود حفظ شده‌اند. ورود به Contact، Profile یا Direct شخصی نباید برای تغییر role و payer، `/workspaces/select` را صدا بزند.

## عملیات شخصی با Teacher/Manager/Student/Parent منتخب

Profile، Contact، People، Privacy و Individual Commerce همان مسیرهای قبلی را دارند. برای ساخت Direct و Group/Channel شخصی، `scope: Personal` بفرستید. عملیات Conversation مشخص از روی منبع واقعی به بافت شخصی می‌روند؛ role منتخب دست‌نخورده می‌ماند.

قراردادهای افزوده:

| مسیر | کاربرد |
| --- | --- |
| `GET /api/v1/conversations?scope=Personal` | inbox شخصی در هر فضای منتخب |
| `GET /api/v1/conversations/discover?scope=Personal` | کشف Group/Channel شخصی با همان privacy و age policy |
| `GET /api/v1/messages/search?scope=Personal&q=...` | جست‌وجوی شخصی؛ فیلترهای قبلی باقی‌اند |
| `GET /api/v1/realtime/changes?scope=Personal` | cursor و replay جریان شخصی |
| `POST /api/v1/files?workspaceScope=Personal` | upload شخصی با multipart قبلی |

بدون scope صریح، لیست‌ها و upload رفتار فضای منتخب قبلی را حفظ می‌کنند. cursor شخصی و سازمانی قابل جابه‌جایی نیستند. Forward مختلط تابع قواعد قبلی است؛ اضافه‌شدن scope شخصی مجوز انتقال آزاد دادهٔ سازمانی نیست.

## جفت رسمی

| Method | Route | مجوز |
| --- | --- | --- |
| GET | `/api/v1/organization/official-spaces` | Manager معتبر همان سازمان |
| GET | `/api/v1/organization/classes/{classId}/official-spaces` | Manager همان سازمان، Teacher منتسب یا Student دارای Enrollment معتبر |
| GET | `/api/v1/organization/classes/{classId}/official-spaces/activities/{eventId}` | همان مجوز جاری کلاس |
| GET | `/api/v1/organization/classes/{classId}/official-spaces/activities?kind=Lesson&sourceVersionId={id}&revision={n}` | lookup تحویل با منبع/نسخه بدون نیاز به دانستن قبلی eventId |

برای `Lesson` شناسهٔ خود Lesson و revision پاسخ انتشار را بفرستید؛ برای Assignment/Exam شناسهٔ نسخه و version number را بفرستید. مقدار نامعتبر، event غایب یا منبع نامجاز با خطای امن ConversationNotFound پنهان می‌شود. GET هیچ provisioning یا repair ایجاد نمی‌کند.

پاسخ Pair:

```json
{
  "sourceKind": "Class",
  "sourceId": "00000000-0000-0000-0000-000000000001",
  "organizationId": "00000000-0000-0000-0000-000000000002",
  "version": "opaque-pair-version",
  "group": {
    "conversationId": null,
    "state": "Pending",
    "capabilities": { "canRead": false, "canSend": false, "canManage": false },
    "official": null,
    "attemptCount": 0,
    "lastFailureCategory": null
  },
  "channel": {
    "conversationId": null,
    "state": "Pending",
    "capabilities": { "canRead": false, "canSend": false, "canManage": false },
    "official": null,
    "attemptCount": 0,
    "lastFailureCategory": null
  }
}
```

این شناسه‌ها صرفاً نمونهٔ غیرحساس‌اند. `Missing` یعنی هنوز intent یا binding موجود نیست؛ `Pending` منتظر worker؛ `Ready` مقصد فعال موجود؛ `RepairRequired` نیازمند بررسی/تلاش عملیاتی است. یک مقصد ممکن است Ready و دیگری Pending/RepairRequired باشد؛ مقصد موفق را حذف یا دوباره ایجاد نکنید. endpoint عمومی Ensure/Repair وجود ندارد. `CanManage=false` از مدیریت دستی اعضا/مالکیت رسمی جلوگیری می‌کند؛ مدیریت School منبع از APIهای قبلی مستقل است.

ممکن است مقصد `Ready` باشد ولی `canRead=false` بماند، چون projection مشارکتِ رابطهٔ تازه هنوز توسط worker همگام نشده است. در این فاصله منبع رسمی موجود است اما ورود کاربر آماده نیست؛ refetch پس از access invalidation انجام شود. اضافه‌شدن مشارکت، `version` جفت را هم تغییر می‌دهد. کلاینت نباید فقط از `state=Ready` مجوز ورود نتیجه بگیرد.

`official` و `ConversationSummaryResult.official` شامل `sourceKind/sourceId/organizationId/purpose/displayLabel/version` هستند. badge فقط از metadata واقعی سرور نمایش داده شود؛ title، نقش انسانی، route یا متن پیام مبنا نیستند. Teacher/Manager/Student/Parent badge انسانی تازه نگرفته‌اند.

## Activity Card

پیام از API Messages معمول دریافت می‌شود و علاوه بر envelope قبلی:

```text
originKind = System
academicActivity = {
  eventId, kind, sourceId, sourceVersionId, sourceRevision,
  organizationId, classId, destinationConversationId, destinationPurpose,
  actorKind, initiatedByUserId, occurredAtUtc, title,
  dueAtUtc, availableFromUtc, availableUntilUtc, state, isActionable
}
```

نوع پایهٔ Message همچنان Text است؛ Card را از metadata بسازید. منبع بازشدنی از `kind/sourceId/sourceVersionId` انتخاب می‌شود و API منبع مجوز را دوباره بررسی می‌کند. هیچ permission از صرف وجود Card حاصل نمی‌شود.

در `Superseded / isActionable=false`، action نسخهٔ قدیمی را غیرفعال کنید و منبع جاری را refetch کنید. UI برای دو Card یک منبع، source identity یکسان و destination/message identity جدا نگه دارد. متن، answers، answer key، private grade یا note در Card وجود ندارد و نباید از فیلدهای دیگر حدس زده شود.

انتشار منبع و تحویل کارت مستقل‌اند. موفقیت Publish را با انتظار تحویل Channel به شکست تبدیل نکنید؛ Deliveryهای Pending را با read API پیگیری کنید. نتیجهٔ Delivered شامل `eventId/purpose/conversationId/messageId/attemptCount/lastFailureCategory` است. پس از lost ACK دوباره‌خوانی/ retry موجود اثر دوم نمی‌سازد.

## Realtime و خطاها

Hub و account/session stream موجود reuse شده‌اند. `ConversationChanged`، access invalidation، replay/cursor/resync همان قرارداد پایه را دارند. access invalidation ممکن است بدون ResourceId باشد؛ snapshot و inbox متناظر را refetch کنید. پس از تغییر context، cursor قبلی را روی context جدید مصرف نکنید؛ Personal stream مستقل نگه داشته شود.

هر پیام با MessageId/EventId و version در state کلاینت dedupe شود. رویداد تکراری unread را دوباره زیاد نکند. خواندن/آرشیو/mute در Group، Channel را تغییر نمی‌دهد. Reconnect فقط اتصال نیست؛ replay را تا cut معتبر/hasMore و retryAfter موجود ادامه دهید یا resync بگیرید.

خطاهای موجود از ProblemDetails و code فعلی می‌آیند: نشست نامعتبر `401`، منبع نامجاز `404 / CONVERSATION_NOT_FOUND`، ارسال Channel بدون مجوز `403`، cursor ناسازگار `409 / REALTIME_RESYNC_REQUIRED`، version conflict و idempotency conflict طبق قرارداد Messaging قبلی. `ReviewRequired` برای رسانهٔ Minor/Unknown همچنان قابل تحویل نیست؛ این مرحله مسیر Human Review تازه یا Safety bypass ندارد.

Role revoke به معنی حذف تاریخچهٔ ذخیره‌شده نیست. دسترسی آینده طبق current state قطع می‌شود. Parent از GuardianRelation دسترسی گستردهٔ Group/Channel یا private Direct نمی‌گیرد. تصمیم‌های باز و محدودیت‌های AI/Teacher Notes در سند Implementation ثبت شده‌اند.
