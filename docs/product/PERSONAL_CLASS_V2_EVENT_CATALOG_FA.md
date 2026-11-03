# کاتالوگ Realtime کلاس مستقل v2 — نسخهٔ 1

این کاتالوگ، قرارداد فعلی را مستند می‌کند. نام‌های مفهومی مانند `AssignmentPublished` رویداد SignalR جدید نیستند؛ به key واقعی زیر نگاشت می‌شوند.

## زیرساخت و envelope

Hub موجود: `/hubs/messaging`. نام پیام invalidation: **`ClientInvalidated`**. `PayloadVersion=1`.

```text
RealtimeInvalidation
  eventId: UUID
  key: string
  resourceId: UUID | null
  contextRevision: string
  streamVersion: string
  occurredAtUtc: UTC
  payloadVersion: 1
  traceId: string | null
```

`RealtimeInvalidationWriter` و Outbox موجود با نوع `platform.realtime-invalidation.v1` استفاده می‌شوند. Storeهای مشخص در تراکنش موجود capture می‌کنند؛ انتشار پس از commit معتبر انجام می‌شود. این مرحله Outbox، Hub یا hook عمومی SaveChanges جدیدی اضافه نمی‌کند.

## نگاشت رخداد به key واقعی

تمام ردیف‌های کلاس مستقل در account stream با context شخصی هستند؛ Organization context جایگزین آن نمی‌شود. «مجاز» در جدول یعنی حساب/نشست و رابطه/عضویت/اختیار **جاری**، نه صرفاً مجازبودن در زمان تولید event.

| تغییر مفهومی | key واقعی | مخاطب و resourceId | زمان و refetch کلاینت |
|---|---|---|---|
| FormationRequestChanged | `access` | درخواست‌کننده؛ `null` | ایجاد/ویرایش/ارسال؛ لیست و detail درخواست |
| FormationDecisionChanged | `access` | درخواست‌کننده، مدیر تعیین‌شده، و reviewerهای دارای permission خواندن برای درخواست غیردرفت؛ `null` | NeedsChanges/Approved/Rejected؛ detail/history درخواست و کلاس‌های خود |
| PersonalClassStateChanged | `access` | host، مدیر و اعضای فعال؛ `null` | configure/activate/suspend/archive؛ detail/capabilities و لیست‌ها |
| PersonalClassProvisioningChanged | `access` | اعضای فعالِ مخاطب invalidation زیرساخت موجود؛ `null` | پس از تغییر وضعیت ثبت‌شدهٔ provisioning؛ GET spaces و progress درخواست |
| ManagerChanged / TeacherChanged | `access` | host، مدیر و شخص متأثر طبق capture موجود؛ `null` | تغییر نقش/تصمیم انتقال؛ memberships، transfers، detail و capabilities |
| InvitationChanged | `access` | مدیر/host و مقصد دعوت؛ `null` | ایجاد/پذیرش/بستن؛ invitations/page و detail کلاس |
| JoinRequestChanged | `access` | مدیر/host و درخواست‌کننده؛ `null` | ایجاد/تصمیم؛ join-requests و memberships |
| MembershipChanged | `access` | مدیر/host و شخص متأثر؛ `null` | پذیرش/پایان/تغییر نقش؛ دسترسی و لیست اعضا |
| AcademicPermissionChanged | `access` | host، مدیر و اعضای فعال؛ `null` | اعطا/لغو/تفویض؛ capabilities و academic-permissions |
| AssignmentPublished و تغییر draft مجاز | `assessment.assignment` | مخاطب فعلی دارای AuthorAssignments؛ پس از انتشار Learnerهای مجاز نیز؛ AssignmentId | refetch detail و لیست assignments؛ draft به Learner ارسال نمی‌شود |
| SubmissionChanged | `assessment.submission` | مالک همان membership period و/یا ارزیاب مجاز مطابق نوع تغییر؛ SubmissionAttemptId | refetch own attempt یا submitted-attempts/evaluation queue |
| AssignmentResultReleased/Corrected | `assessment.grade` | ارزیاب و صاحب نتیجهٔ منتشرشده؛ SubmissionAttemptId | refetch result؛ تغییر draft فقط staff مجاز |
| ExamPublished و تغییر draft مجاز | `assessment.exam` | AuthorExams و، پس از انتشار، Learner مجاز؛ ExamId | refetch exam list/preview؛ answer key در event نیست |
| ExamAttemptChanged | `assessment.exam-attempt` | مالک همان membership period؛ ExamAttemptId | refetch attempt/receipt؛ event جای autosave response نیست |
| ExamResultReleased/Corrected | `assessment.exam-grade` | Grade مؤثر یا صاحب نتیجهٔ منتشرشده؛ ExamAttemptId | refetch grading یا result؛ draft grade برای staff مجاز |

تغییر Core ممکن است `commerce` را نیز فقط برای host تجاری invalidate کند. تغییر میزبان مالی از انتقال مدیر نتیجه‌گیری نمی‌شود. `access` عمداً coarse است و شناسهٔ کلاس/طرف مقابل را در payload broadcast نمی‌کند؛ کلاینت لیست‌های مجاز همان حساب را بازخوانی می‌کند.

card انتشار فعالیت در Group/Channel مسیر موجود Messaging و `ConversationChanged` را نیز به کار می‌گیرد. card فقط metadata عمومیِ فعالیت منتشرشده دارد؛ جایگزین event خصوصی نمره نیست. انتشار/retry نباید card یا پیام تکراری بسازد.

## Replay، reconnect و duplicate

```http
GET /api/v1/realtime/changes?scope=Personal&limit=100&cursor=<opaque-cursor>
Authorization: Bearer <session-token>
```

Response واقعی `AccountChangePageResult`: `changes`, `nextCursor`, `hasMore`, `watermark`, `epoch`, `contextRevision`, `retryAfterMilliseconds`.

1. Snapshot مجاز و cursor را بگیرید؛ streamVersion را opaque نگه دارید.
2. بعد از reconnect از آخرین cursor معتبر catch-up کنید و تا `hasMore=false` ادامه دهید؛ limit حداکثر 200 است.
3. تحویل **at-least-once** است. با eventId و streamVersion/contextRevision dedupe کنید؛ از هر event شمارندهٔ unread را کورکورانه افزایش ندهید.
4. snapshot/refetch منبع حقیقت است. چند invalidation پشت‌سرهم را می‌توان برای refetch ادغام کرد.
5. `409 REALTIME_RESYNC_REQUIRED`: snapshot و cursor تازه بگیرید. cursor قدیمی را بین حساب/نشست/context جابه‌جا نکنید.
6. `503 REALTIME_TEMPORARILY_UNAVAILABLE`: cursor قبلی را نگه دارید و طبق retry hint دوباره درخواست کنید؛ دادهٔ جدید را دریافت‌شده فرض نکنید.

`RealtimeSyncService` و audience موجود، مجوز را هنگام delivery/replay دوباره بررسی می‌کنند. revoke عضویت/اختیار نمی‌تواند با event قدیمی خنثی شود. rejoin یک membership period تازه است و مجوز خصوصیِ دورهٔ قبلی را خودکار احیا نمی‌کند.

## حریم خصوصی و پذیرش

payload شامل answer، draft grade، answer key، feedback خصوصی، phone، credential یا secret نیست. Parent/School role/Platform approval به‌تنهایی audience خصوصی ایجاد نمی‌کنند.

تست‌های `PersonalClassAcademicConcurrencyTests` و `PersonalClassV2MessagingTests` به‌علاوهٔ اجرای واقعی محلی Kestrel/SQL/دو WebSocket، publish، catch-up، dedupe/refetch، concealment و membership revoke را بررسی می‌کنند. در این مرحله پذیرش سرور خارجی یا مرورگر Frontend ادعا نمی‌شود.
