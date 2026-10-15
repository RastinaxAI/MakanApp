# STEP 14E — تکمیل Messenger و پذیرش نهایی

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

## 1. دامنه و روش ممیزی

این سند re-audit نهایی همان ۱۲۷ requirement ثبت‌شده در STEP 14A است. وضعیت هر ردیف
فقط بر اساس کد، endpoint، migration و تست موجود در همین repository تعیین شده است.
اسناد UX فرانت‌اند فقط مرجع contract بوده‌اند و هیچ frontend ساخته یا تغییر داده نشده است.

تعریف وضعیت‌ها:

- `IMPLEMENTED`: رفتار backend و کنترل امنیتی لازم وجود دارد و شاهد اجرایی دارد.
- `PARTIAL`: بخش امن و قابل استفاده وجود دارد، ولی بخشی از همان requirement صریحاً باقی است.
- `DEFERRED_SECURE_DESIGN`: پیاده‌سازی بدون تصمیم consent/privacy/lifecycle امن مجاز نیست.
- `DEFERRED_ADVANCED`: قابلیت تکمیلی/آینده است و نبود آن مسیر اصلی Messenger را نمی‌شکند.
- `UNRESOLVED_PRODUCT_POLICY`: تصمیم محصول هنوز freeze نشده و نباید حدس زده شود.

## 2. نتیجه کمی و verdict

| وضعیت | تعداد |
|---|---:|
| `IMPLEMENTED` | 93 |
| `PARTIAL` | 19 |
| `MISSING` | 0 |
| `CONFLICTING` | 0 |
| `UNRESOLVED_PRODUCT_POLICY` | 1 |
| `DEFERRED_SECURE_DESIGN` یا `DEFERRED_ADVANCED` | 14 |
| **مجموع** | **127** |

**Release verdict: `BLOCKED_BY_PRODUCT_POLICY` برای production دارای کودک.** مسیرهای
اصلی Messenger و قابلیت‌های STEP 14E operational هستند، اما adapter واقعی Child
Safety و policy دقیق ساخت/عضویت Personal Group/Channel برای حساب کودک هنوز تصویب
نشده‌اند. runtime در Conversation حساس به کودک بدون provider واقعی fail-closed است.
Contact Sync، Avatar/Presence، invite link، album/link extraction و قابلیت‌های پیشرفته
نیز صریحاً deferred هستند و با پیاده‌سازی ساختگی پنهان نشده‌اند.

## 3. راهنمای شواهد

### Migrationها

- `M7A`: `20261004175932_AddDirectMessagingFoundation`
- `M7B`: `20261005160127_AddGroupAndChannelMessaging`
- `M7C`: `20261006135440_AddAdvancedMessagingFeatures`
- `M7D`: `20261006170623_AddMessagingRealtimeAndSync`
- `M7E`: `20261006200138_AddMessagingSafetyAndSearch`
- `M12A`: `20261012154542_AddNotificationFoundation`
- `M12B`: `20261012163226_AddNotificationPreferencesAndDelivery`
- `M14B`: `20261012203821_AddMessengerContactsPrivacyDiscovery`
- `M14C`: `20261012212650_HardenMessengerConversationBehavior`
- `M14D`: `20261012222340_AddMessagingModerationAndChildSafety`
- `M14E`: `20261013042337_CompleteMessengerV2`

### API و تست

- `A-DIR`: `ContactsController`، `PeopleController` و `MessagingPrivacyController`
- `A-CONV`: `ConversationsController` و endpointهای conversation/message/state
- `A-SEARCH`: `MessagesController.Search`
- `A-SAFE`: block/report و `ConversationModerationController`
- `A-NTF`: `NotificationsController` و workerهای Outbox/Delivery
- `T-DIR`: `MessagingContactsPrivacyDiscoveryEndpointsTests`
- `T-BEH`: `MessagingBehaviorHardeningEndpointsTests`
- `T-MOD`: `MessagingModerationChildSafetyEndpointsTests`
- `T-CMP`: `MessagingCompletionEndpointsTests`
- `T-MSG`: suiteهای Messaging endpoint/realtime/sync/safety
- `T-UNIT`: suiteهای Unit مدل و policy پیام‌رسانی
- `T-NTF`: تست‌های Notification/Outbox/Delivery

## 4. ماتریس نهایی Traceability

ستون DB نام migration مؤثر یا `—` برای قابلیت بدون تغییر schema است. ستون Security
کنترل اصلی و ستون Limitation مرز باقیمانده را ثبت می‌کند.

### Contacts — CNT

| ID | Requirement | Final status | Evidence | API | DB | Tests | Security | Limitation |
|---|---|---|---|---|---|---|---|---|
| CNT-01 | فهرست Contact ذخیره‌شده با profile امن | `IMPLEMENTED` | `UserContact` و projection امن | A-DIR | M14B | T-DIR | owner-only | — |
| CNT-02 | جست‌وجوی saved/display/username/phone مجاز | `IMPLEMENTED` | query محدود؛ phone فقط exact resolve | A-DIR | M14B | T-DIR | anti-enumeration و rate limit | partial phone search عمداً ندارد |
| CNT-03 | مرتب‌سازی الفبایی و recent/frequent | `PARTIAL` | list و recent پایدار | A-DIR | M14B | T-DIR | query bounded | frequent ranking وجود ندارد |
| CNT-04 | member/invitable فقط با discovery مجاز | `IMPLEMENTED` | `MessagingDirectoryService` | A-DIR | M14B | T-DIR | hidden و unknown هم‌شکل | — |
| CNT-05 | profile مجاز و شروع Direct از Contact | `IMPLEMENTED` | safe profile + global Direct | A-DIR/A-CONV | M14B | T-DIR | Privacy + Block recheck | — |
| CNT-06 | افزودن Contact با mobile یا Username | `IMPLEMENTED` | resolve امن سپس save با UserId | A-DIR | M14B | T-DIR | phone در body و exact | — |
| CNT-07 | saved name مستقل از profile | `IMPLEMENTED` | `UserContact.SavedName` | A-DIR | M14B | T-DIR/T-UNIT | private owner data | — |
| CNT-08 | جلوگیری از Contact تکراری | `IMPLEMENTED` | upsert سریال‌پذیر + unique index | A-DIR | M14B | T-DIR | race-safe | — |
| CNT-09 | ویرایش saved name مستقل | `IMPLEMENTED` | PATCH با rowversion | A-DIR | M14B | T-DIR | optimistic concurrency | — |
| CNT-10 | حذف Contact بدون حذف Direct history | `IMPLEMENTED` | aggregate و FK مستقل | A-DIR | M14B | T-DIR | no cascade to chat | — |
| CNT-11 | اشتراک Contact card فقط با داده مجاز | `IMPLEMENTED` | `MessageKind.ContactCard` و snapshot typed | A-CONV | M14E | T-CMP/T-UNIT | بدون Phone و بدون auto-direct | Avatar ندارد |
| CNT-12 | ساخت Group از Contactهای انتخابی | `IMPLEMENTED` | create با UserIdها و invite privacy | A-CONV | M7B/M14B | T-DIR/T-MSG | target privacy server-side | انتخاب UI خارج backend است |
| CNT-13 | دعوت unknown بدون افشای وجود حساب | `PARTIAL` | پاسخ مقصد نامجاز/ناموجود یکسان | A-DIR/A-CONV | M14B | T-DIR | anti-enumeration | SMS invite وجود ندارد |
| CNT-14 | sync address book فقط با consent | `DEFERRED_SECURE_DESIGN` | هیچ endpoint/schema ساخته نشد | — | — | — | consent باید صریح باشد | upload protocol تصویب نشده |
| CNT-15 | خاموش‌کردن address-book sync | `DEFERRED_SECURE_DESIGN` | وابسته به قرارداد sync | — | — | — | revoke نباید chat را بشکند | sync وجود ندارد |
| CNT-16 | حذف sync مستقل از Contact دستی | `DEFERRED_SECURE_DESIGN` | provenance هنوز freeze نشده | — | — | — | حذف منبعی لازم است | sync وجود ندارد |
| CNT-17 | resolve شماره تغییرکرده با account identity | `IMPLEMENTED` | Contact با UserId پایدار است | A-DIR | M14B | T-DIR | phone identity نیست | — |
| CNT-18 | failure sync Messenger را از کار نیندازد | `DEFERRED_SECURE_DESIGN` | subsystem عمداً ساخته نشد | — | — | — | failure isolation لازم است | sync وجود ندارد |
| CNT-19 | Block از profile/contact/chat | `IMPLEMENTED` | endpoint مشترک Block و reauthorization | A-DIR/A-SAFE | M7E/M14B | T-DIR/T-MSG | deny عمومی | — |
| CNT-20 | مشاهده و مدیریت Block list | `IMPLEMENTED` | GET/POST/DELETE block | A-SAFE | M7E | T-MSG | فقط blocker | سقف list فعلی bounded است |
| CNT-21 | Unblock، Contact حذف‌شده را بازنگرداند | `IMPLEMENTED` | aggregates مستقل | A-DIR/A-SAFE | M14B | T-DIR | no implicit restore | — |
| CNT-22 | Block ارسال Direct را منع و history را حفظ کند | `IMPLEMENTED` | send-time block check | A-CONV/A-SAFE | M7E/M14B | T-DIR/T-MSG | history پاک نمی‌شود | — |
| CNT-23 | Block عضویت Group را حذف نکند | `IMPLEMENTED` | group membership مستقل | A-CONV/A-SAFE | M7B/M7E | T-DIR/T-MSG | block direct-only | — |
| CNT-24 | Report حساب/پیام با evidence و description | `PARTIAL` | report پیام + case/evidence | A-SAFE | M7E/M14D | T-MOD | evidence کمینه و scoped | account-only report و appeal نیست |

### Profile Privacy — PRF

| ID | Requirement | Final status | Evidence | API | DB | Tests | Security | Limitation |
|---|---|---|---|---|---|---|---|---|
| PRF-01 | profile عمومی امن | `PARTIAL` | UserId/Username/DisplayName امن | A-DIR | M14B | T-DIR | field-level projection | Avatar و Bio نیست |
| PRF-02 | Who can see phone | `IMPLEMENTED` | visibility audience مستقل | A-DIR | M14B | T-DIR | default Nobody | — |
| PRF-03 | Who can find by phone | `IMPLEMENTED` | discovery audience مستقل | A-DIR | M14B | T-DIR | exact + indistinguishable | — |
| PRF-04 | Who can see online/last-seen | `DEFERRED_SECURE_DESIGN` | state ساختگی ایجاد نشد | — | — | — | privacy/expiry لازم است | multi-instance semantics تصویب نشده |
| PRF-05 | Who can see profile image | `DEFERRED_SECURE_DESIGN` | FileAsset profile ساخته نشد | — | — | — | download authorization لازم است | lifecycle/audience تصویب نشده |
| PRF-06 | Who may Direct/add to Group | `IMPLEMENTED` | privacyهای مستقل Direct/Invite | A-DIR/A-CONV | M14B | T-DIR | current-policy recheck | child creation policy جداست |
| PRF-07 | privacy یکسان در Search/Invite/Card | `IMPLEMENTED` | resolver مشترک؛ card داده کمینه | A-DIR/A-CONV | M14B/M14E | T-DIR/T-CMP | Phone leakage tests | surfaces deferred وجود ندارند |

### Conversation List — LST

| ID | Requirement | Final status | Evidence | API | DB | Tests | Security | Limitation |
|---|---|---|---|---|---|---|---|---|
| LST-01 | list متحد Direct/Group/Channel | `IMPLEMENTED` | `GetMyConversationsAsync` شامل Saved | A-CONV | M7A/M7B/M14E | T-MSG/T-CMP | participant + scope | هر workspace جداست |
| LST-02 | recent sort و pinned conversation | `IMPLEMENTED` | pinned-first و activity order | A-CONV | M14C | T-BEH | state خصوصی | — |
| LST-03 | unread count و unread mentions | `IMPLEMENTED` | SQL projection هر دو count | A-CONV | M14E | T-CMP | visibility/read watermark | — |
| LST-04 | filter All/Direct/Group/Channel/Unread | `IMPLEMENTED` | filter/page و Saved/Archived | A-CONV | M14C/M14E | T-BEH/T-CMP | server-side bounded | — |
| LST-05 | جست‌وجوی نام گفتگو و محتوای مجاز | `IMPLEMENTED` | list `q` + message search | A-CONV/A-SEARCH | M14E | T-CMP | current authorization | — |
| LST-06 | archive/unarchive بدون حذف history | `IMPLEMENTED` | `ConversationUserState` شخصی | A-CONV | M14C | T-BEH | طرف دیگر unaffected | — |
| LST-07 | mute/unmute Conversation | `IMPLEMENTED` | Permanent/Until state | A-CONV | M14C | T-BEH/T-CMP | فقط delivery خارجی | — |
| LST-08 | mark read/unread | `IMPLEMENTED` | cursor واقعی + flag نمایشی | A-CONV | M7D/M14C | T-BEH/T-MSG | read cursor عقب نمی‌رود | — |
| LST-09 | حذف از list شخصی با اثر روشن | `PARTIAL` | Archive از list عادی حذف؛ Clear History محتوا را می‌پوشاند | A-CONV | M14C/M14E | T-BEH/T-CMP | per-user only | delete-conversation جدا ندارد |
| LST-10 | New Direct/Group/Channel | `IMPLEMENTED` | سه create endpoint idempotent | A-CONV | M7A/M7B | T-MSG | scope/privacy checks | — |
| LST-11 | custom folders | `DEFERRED_ADVANCED` | feature آینده | — | — | — | schema حدس زده نشد | خارج MVP |
| LST-12 | state قابل فهم و multi-device | `IMPLEMENTED` | audience-specific ChangeEvent/Delta | A-CONV | M7D/M14C/M14E | T-BEH/T-MSG/T-CMP | private state broadcast نمی‌شود | — |

### Direct Messaging — DM

| ID | Requirement | Final status | Evidence | API | DB | Tests | Security | Limitation |
|---|---|---|---|---|---|---|---|---|
| DM-01 | شروع از Contact/Profile/Search/link | `IMPLEMENTED` | Contact/Profile/Search به Direct متصل | A-DIR/A-CONV | M14B | T-DIR | Privacy + Block | deep link کار frontend است |
| DM-02 | Direct موازی تکراری نباشد | `IMPLEMENTED` | canonical pair + unique/lock | A-CONV | M7A | T-MSG | race-safe | — |
| DM-03 | text/emoji/sticker/GIF/image/video/file/voice | `PARTIAL` | Text و media/file/voice؛ emoji متن | A-CONV | M7A/M7C | T-MSG | MIME/attachment auth | Sticker/GIF first-class نیست |
| DM-04 | Reply | `IMPLEMENTED` | same-conversation FK و placeholder | A-CONV | M7C | T-MSG/T-BEH | hidden source leak ندارد | — |
| DM-05 | Edit با window یک‌ساعته | `IMPLEMENTED` | `TimeProvider` و revision داخلی | A-CONV | M7C | T-BEH | server time + rowversion | — |
| DM-06 | Delete For Me/Everyone مستقل؛ Everyone یک ساعت | `IMPLEMENTED` | دو route و visibility خصوصی | A-CONV | M14C | T-BEH | tombstone/audit حفظ می‌شود | deadline For Me تعریف نشده |
| DM-07 | Forward امن | `IMPLEMENTED` | access source/destination و copy | A-CONV | M7C/M14C/M14E | T-MSG/T-CMP | clear/history/delete checks | — |
| DM-08 | Reaction | `IMPLEMENTED` | lifecycle deterministic | A-CONV | M7C | T-MSG | unique per user | — |
| DM-09 | Pin مجاز | `IMPLEMENTED` | message pin و policy | A-CONV | M7C/M14C | T-MSG/T-BEH | permission granular | — |
| DM-10 | search با content type/date/sender | `IMPLEMENTED` | sender/kind/date/conversation/cursor | A-SEARCH | M7E/M14E | T-MSG/T-CMP | visibility واحد | — |
| DM-11 | Sending/Delivered/Read semantics | `PARTIAL` | Sent receipt و monotonic Delivered/Read | A-CONV | M7D | T-MSG | server states تفکیک شده | Pending/Failed client-side است |
| DM-12 | Shared Media | `PARTIAL` | attachment media query امن | A-CONV | M7C/M14C/M14E | T-MSG/T-CMP | current visibility/file auth | link discovery نیست |
| DM-13 | clear history شخصی | `IMPLEMENTED` | monotonic watermark | A-CONV | M14E | T-CMP/T-UNIT | shared rows حذف نمی‌شود | — |
| DM-14 | mute/background؛ auto-delete/scheduled future | `IMPLEMENTED` | mute + Delta/reconnect | A-CONV | M7D/M14C | T-BEH/T-MSG/T-CMP | persistence مستقل از mute | future items خارج scope |

### Group — GRP

| ID | Requirement | Final status | Evidence | API | DB | Tests | Security | Limitation |
|---|---|---|---|---|---|---|---|---|
| GRP-01 | ساخت Group با title/avatar/description و حداقل عضو | `PARTIAL` | title/description/participants idempotent | A-CONV | M7B | T-MSG | target eligibility | Avatar و minimum product rule نیست |
| GRP-02 | ownership و transfer تأییدشده | `IMPLEMENTED` | start/accept/decline transaction | A-CONV | M7B | T-MSG | یک Owner و consent مقصد | — |
| GRP-03 | add/remove member با membership/privacy | `PARTIAL` | permission و invite privacy | A-CONV | M7B/M14B/M14C | T-DIR/T-BEH | current scope checks | child membership policy حل‌نشده |
| GRP-04 | invite link قابل revoke | `DEFERRED_ADVANCED` | token/schema ساخته نشد | — | — | — | hash/revoke لازم است | خارج completion امن فعلی |
| GRP-05 | Public/Private Group | `IMPLEMENTED` | visibility مستقل و discovery/join | A-CONV | M14C | T-BEH | private ID مجوز نیست | — |
| GRP-06 | permission granular Admin | `IMPLEMENTED` | `ConversationAdminPermission` flags | A-CONV | M14C/M14D | T-BEH/T-MOD | no privilege escalation | — |
| GRP-07 | capability مستقل text/media/link/poll/invite | `PARTIAL` | SendMessages/SendMedia/InviteMembers | A-CONV | M14C | T-BEH | server-side flags | Link/Poll capability وجود ندارد |
| GRP-08 | mute/restrict/ban عضو | `DEFERRED_ADVANCED` | remove و moderation case موجود | A-CONV/A-SAFE | M7B/M14D | T-MOD | audit لازم است | timed restrict/ban مدل نشده |
| GRP-09 | member/admin list با search | `PARTIAL` | list صفحه‌بندی‌شده | A-CONV | M7B/M14C | T-BEH/T-MSG | active participant only | search عضو ندارد |
| GRP-10 | Reply/Reaction/Mention/Pin/Search | `IMPLEMENTED` | همه interactionها enforce می‌شوند | A-CONV/A-SEARCH | M7C/M7E/M14C | T-MSG/T-BEH | visibility/permission | — |
| GRP-11 | leave؛ Owner اول ownership را حل کند | `IMPLEMENTED` | owner leave منع شده | A-CONV | M7B | T-MSG | invariant مالکیت | — |
| GRP-12 | history عضو جدید تابع setting | `IMPLEMENTED` | FullHistory/FromJoin snapshot | A-CONV | M14C | T-BEH | همه read surfaceها | Channel policy جدا ندارد |
| GRP-13 | Poll/Topics/Slow Mode | `DEFERRED_ADVANCED` | future enhancement | — | — | — | contract حدس زده نشد | خارج MVP |
| GRP-14 | audit اقدام moderator/admin | `IMPLEMENTED` | ChangeEvent + append-only action | A-CONV/A-SAFE | M7D/M14D | T-MOD | actor/time immutable | Platform backoffice STEP 15 |
| GRP-15 | Organization Group بدون bypass خارجی | `IMPLEMENTED` | membership/workspace checks | A-CONV | M7B/M14C | T-MSG/T-BEH | cross-org concealment | — |

### Channel — CHN

| ID | Requirement | Final status | Evidence | API | DB | Tests | Security | Limitation |
|---|---|---|---|---|---|---|---|---|
| CHN-01 | title/avatar/description و Public/Private | `PARTIAL` | title/description/visibility | A-CONV | M7B/M14C | T-BEH | scoped discovery | Avatar نیست |
| CHN-02 | Owner/publisher publish؛ subscriber read | `IMPLEMENTED` | PublishMessages policy | A-CONV | M7B/M14C | T-MSG/T-BEH | subscriber write denied | — |
| CHN-03 | permission granular channel | `IMPLEMENTED` | admin flags مستقل | A-CONV | M14C | T-BEH | least privilege | — |
| CHN-04 | text/image/video/file/album | `PARTIAL` | text و media/file | A-CONV | M7C | T-MSG | attachment authorization | album/grouped media نیست |
| CHN-05 | edit/delete/pin طبق permission | `IMPLEMENTED` | sender windows + moderator flags | A-CONV | M14C | T-BEH/T-MOD | edit برای دیگران نیست | — |
| CHN-06 | view-count semantics | `UNRESOLVED_PRODUCT_POLICY` | هیچ state ساختگی ایجاد نشد | — | — | — | viewer/device privacy نامشخص | نیازمند freeze محصول |
| CHN-07 | subscribe/unsubscribe/invite | `PARTIAL` | public self-join، leave و add member | A-CONV | M7B/M14C | T-BEH | private/org gates | invite link ندارد |
| CHN-08 | reaction در صورت enabled | `IMPLEMENTED` | reaction برای participant فعال | A-CONV | M7C | T-MSG | unique user/reaction | toggle disable ندارد |
| CHN-09 | linked discussion group | `DEFERRED_ADVANCED` | future enhancement | — | — | — | association حدس زده نشد | خارج MVP |
| CHN-10 | scheduled publishing/statistics | `DEFERRED_ADVANCED` | future enhancement | — | — | — | job/stat semantics نامشخص | خارج MVP |
| CHN-11 | Organization Channel boundary | `IMPLEMENTED` | membership/scope checks | A-CONV | M7B/M14C | T-MSG/T-BEH | cross-org deny | — |

### Message / Media — MSG

| ID | Requirement | Final status | Evidence | API | DB | Tests | Security | Limitation |
|---|---|---|---|---|---|---|---|---|
| MSG-01 | sender/conversation/content type/time | `IMPLEMENTED` | `Message` و server UTC | A-CONV | M7A/M7C | T-MSG | client time trusted نیست | — |
| MSG-02 | متن و formatting | `PARTIAL` | متن exact تا 4000 | A-CONV | M7A | T-MSG/T-UNIT | raw HTML contract ندارد | formatting schema نیست |
| MSG-03 | retry/double send بدون duplicate | `IMPLEMENTED` | ClientMessageId + payload hash | A-CONV | M7A/M7C | T-MSG/T-CMP | unique + reauthorize | — |
| MSG-04 | ordering پایدار و recovery | `IMPLEMENTED` | Message/Change sequence | A-CONV | M7D | T-MSG | SQL unique ordering | — |
| MSG-05 | Reply placeholder امن | `IMPLEMENTED` | unavailable source کمینه | A-CONV | M7C/M14C/M14E | T-MSG/T-CMP | متن/فرستنده نشت نمی‌کند | — |
| MSG-06 | edited marker + یک ساعت + history داخلی | `IMPLEMENTED` | revision و TimeProvider | A-CONV | M7C/M14C | T-BEH | history عمومی نیست | — |
| MSG-07 | Delete For Me/Everyone مستقل | `IMPLEMENTED` | tombstone + visibility row | A-CONV | M14C | T-BEH | audit حفظ می‌شود | — |
| MSG-08 | Forward مجوز source را دور نزند | `IMPLEMENTED` | source visibility recheck | A-CONV | M7C/M14C/M14E | T-MSG/T-CMP | conceal unauthorized | — |
| MSG-09 | Reaction deterministic per-user | `IMPLEMENTED` | unique filtered index | A-CONV | M7C | T-MSG | race-safe lifecycle | — |
| MSG-10 | فایل: size/type/access/safety | `PARTIAL` | technical cap/MIME/access + child text gate | A-CONV | M7C/M14D | T-MSG/T-MOD | ready-only binding | malware/attachment provider نیست |
| MSG-11 | retry امن upload | `PARTIAL` | Pending/Ready/Rejected؛ send فقط Ready | files/A-CONV | storage migration | storage/T-MSG | incomplete قابل ارسال نیست | upload operation reconciliation کامل نیست |
| MSG-12 | preview/download فقط مجاز فعلی | `IMPLEMENTED` | bound access resolver | files/A-CONV | M7C/M14C/M14E | T-MSG/T-CMP | authorization هر درخواست | — |
| MSG-13 | file/link در Shared Media | `PARTIAL` | فایل/رسانه query می‌شود | A-CONV | M7C/M14C/M14E | T-MSG/T-CMP | visibility واحد | link extraction/index نیست |
| MSG-14 | consistency بین دستگاه‌ها | `IMPLEMENTED` | Delta/SignalR/tombstone/private state | A-CONV | M7D/M14C/M14E | T-MSG/T-BEH/T-CMP | audience-specific sync | client Pending UX خارج backend |

### Messaging Notifications — NTF

| ID | Requirement | Final status | Evidence | API | DB | Tests | Security | Limitation |
|---|---|---|---|---|---|---|---|---|
| NTF-01 | notification Direct/Group/Channel با preference | `IMPLEMENTED` | Outbox message producer | A-NTF | M12A/M12B/M14E | T-CMP/T-NTF | active recipients only | — |
| NTF-02 | notification Mention/Reply | `IMPLEMENTED` | type priority per recipient | A-NTF | M14E | T-CMP | Mention بالاتر از Reply | — |
| NTF-03 | mute موقت/دائم | `IMPLEMENTED` | Until/Permanent و suppression | A-CONV/A-NTF | M14C/M12B | T-CMP | internal notification حفظ می‌شود | — |
| NTF-04 | privacy preview | `IMPLEMENTED` | generic title/body/template | A-NTF | M12B | T-CMP/T-UNIT | بدون content/file/phone | per-conversation preview setting ندارد |
| NTF-05 | unread count بین دستگاه‌ها | `IMPLEMENTED` | monotonic cursor + SQL count | A-CONV | M7D/M14E | T-MSG/T-CMP | per-user max | — |
| NTF-06 | Online/LastSeen/Typing best-effort | `DEFERRED_SECURE_DESIGN` | state/API ساخته نشد | — | — | — | privacy + expiry + multi-instance | تصمیم معماری لازم است |
| NTF-07 | جلوگیری عملی از notification duplicate | `IMPLEMENTED` | MessageId/User unique materialization | A-NTF | M12A | T-CMP/T-NTF | retry-safe outbox | delivery at-least-once با unique attempt |
| NTF-08 | خاموشی notification مانع persistence نشود | `IMPLEMENTED` | mute/preference فقط attempt را suppress می‌کند | A-NTF | M12B | T-CMP/T-NTF | network خارج transaction | — |

### Saved / Archive / Search — ORG

| ID | Requirement | Final status | Evidence | API | DB | Tests | Security | Limitation |
|---|---|---|---|---|---|---|---|---|
| ORG-01 | Saved Messages شخصی | `IMPLEMENTED` | `ConversationType.Saved` lazy singleton | A-CONV | M14E | T-CMP/T-UNIT | owner-only و no notification | — |
| ORG-02 | حذف Saved item منبع را حذف نکند | `IMPLEMENTED` | forward copy مستقل | A-CONV | M14E | T-CMP | source authorization/copy | — |
| ORG-03 | Archive فقط presentation | `IMPLEMENTED` | user state؛ lifecycle global untouched | A-CONV | M14C | T-BEH | per-user | — |
| ORG-04 | pin conversation و message جدا | `IMPLEMENTED` | state pin در برابر ConversationPin پیام | A-CONV | M7C/M14C | T-BEH/T-MSG | permissionها مستقل | — |
| ORG-05 | Search فقط مجاز فعلی | `IMPLEMENTED` | active access + visibility predicate | A-SEARCH | M7E/M14C/M14E | T-MSG/T-CMP | no stale cursor authority | — |
| ORG-06 | filter type/date/sender/conversation | `IMPLEMENTED` | همه filterها در query | A-SEARCH | M14E | T-CMP | bounded + authorized | — |
| ORG-07 | revocation فوراً Search/Media را محدود کند | `IMPLEMENTED` | current participant/membership checks | A-SEARCH/A-CONV | M14C | T-BEH/T-MSG | cached ID مجوز نیست | — |

### Acceptance Criteria — AC

| ID | Requirement | Final status | Evidence | API | DB | Tests | Security | Limitation |
|---|---|---|---|---|---|---|---|---|
| AC-01 | Contact تکراری row دوم نسازد | `IMPLEMENTED` | Serializable + unique | A-DIR | M14B | T-DIR | concurrency-safe | — |
| AC-02 | حذف Contact history را حذف نکند | `IMPLEMENTED` | aggregate مستقل | A-DIR | M14B | T-DIR | no cascade | — |
| AC-03 | blocked user Direct جدید تحویل ندهد | `IMPLEMENTED` | send-time effective block | A-CONV/A-SAFE | M7E/M14B | T-DIR/T-MSG | notification بعد از deny ساخته نمی‌شود | — |
| AC-04 | phone محدود از search/card نشت نکند | `IMPLEMENTED` | audience + card بدون Phone | A-DIR/A-CONV | M14B/M14E | T-DIR/T-CMP | raw response assertion | — |
| AC-05 | unauthorized user private Group را نبیند | `IMPLEMENTED` | Private discovery/read concealment | A-CONV/A-SEARCH | M14C | T-BEH | دانستن Guid کافی نیست | — |
| AC-06 | Channel subscriber publish نکند | `IMPLEMENTED` | PublishMessages required | A-CONV | M14C | T-BEH/T-MSG | server authorization | — |
| AC-07 | delete permission appoint-admin ندهد | `IMPLEMENTED` | permission flags مستقل | A-CONV | M14C/M14D | T-BEH/T-MOD | no privilege escalation | — |
| AC-08 | Send identity حداکثر یک Message | `IMPLEMENTED` | unique + payload match | A-CONV | M7A/M7C | T-MSG/T-CMP | retry safe | — |
| AC-09 | retry/reconnect duplicate نسازد | `IMPLEMENTED` | idempotent send + Delta dedupe contract | A-CONV | M7D | T-MSG/T-CMP | changeId/clientMessageId | — |
| AC-10 | Read state بین دستگاه‌ها سازگار | `IMPLEMENTED` | monotonic participant cursor | A-CONV | M7D | T-MSG | max semantics | — |
| AC-11 | Archive history را نابود نکند | `IMPLEMENTED` | archive state بدون حذف Message | A-CONV | M14C | T-BEH | reversible personal view | — |
| AC-12 | Organization A به Group B دسترسی نگیرد | `IMPLEMENTED` | scope/membership recheck | A-CONV/A-SEARCH | M7B/M14C | T-MSG/T-BEH | cross-org concealment | — |
| AC-13 | Parent/Manager بدون authority chat/Contacts را نبیند | `IMPLEMENTED` | owner/participant/reviewer policies | A-DIR/A-CONV/A-SAFE | M14B/M14D | T-DIR/T-MOD | role alone کافی نیست | — |
| AC-14 | revoke contact sync conversation را نشکند | `DEFERRED_SECURE_DESIGN` | sync عمداً وجود ندارد؛ Conversation مستقل طراحی شده | — | — | — | provenance/revoke باید جدا باشد | پس از تصویب sync قابل آزمون است |
| AC-15 | Search/File/Media فقط مجاز فعلی | `IMPLEMENTED` | visibility و authorization مشترک | A-SEARCH/A-CONV/files | M14C/M14E | T-BEH/T-CMP | revoke/clear فوری | — |

## 5. قابلیت‌های تکمیل‌شده در STEP 14E

### Saved Messages

`GET /api/v1/conversations/saved` در workspace شخصی یک Conversation یکتا را به‌صورت
lazy resolve می‌کند. `UX_Conversations_Saved_Owner` و transaction سریال‌پذیر مانع
ساخت ردیف دوم می‌شوند. فقط مالک participant است، ارسال/خواندن owner-only است و Saved
هیچ اعلان پیام تولید نمی‌کند.

### Clear History

`POST /api/v1/conversations/{conversationId}/clear-history` مقدار
`HistoryHiddenThroughSequence` را تا `NextMessageSequence - 1` جلو می‌برد. watermark
هرگز عقب نمی‌رود و در History، Search، Media، File download، Reply، Forward، Delta،
preview، unread و unread mention enforce می‌شود. عملیات حذف فیزیکی یا cascade ندارد.

### Durable Messaging Notifications

Send و Forward در همان transaction پیام، envelopeهای Notification را در Outbox ثبت
می‌کنند. materialization پس از commit انجام می‌شود. فرستنده و participant لغوشده حذف
می‌شوند، Organization membership جاری دوباره کنترل می‌شود، retry همان پیام effect
دوم ندارد و خطای provider پیام committed را rollback نمی‌کند. Mute فقط Push/SMS را
با علت typed suppress می‌کند.

### Search، unread mention و Contact Card

- list گفتگو `q` و filter `Saved` دارد؛ search پیام `senderUserId` را می‌پذیرد؛
- `UnreadMentionCount` از SQL و همان visibility/read watermark محاسبه می‌شود؛
- Contact Card یک Message typed با snapshot `UserId/Username/DisplayName` است، Phone
  ندارد و دریافت آن Contact یا Direct ایجاد نمی‌کند.

## 6. دیتابیس STEP 14E

Migration `20261013042337_CompleteMessengerV2`:

- `messaging.Conversations.SavedOwnerUserId uniqueidentifier NULL` با FK محدودکننده به
  `identity.Users`، filtered unique index و checkهای shape/type؛
- `messaging.ConversationUserStates.HistoryHiddenThroughSequence bigint NOT NULL`؛
  ابتدا nullable، سپس backfill صفر و بعد NOT NULL، بدون SQL default؛
- `messaging.Messages.SharedContactUserId/Username/DisplayName` با FK، index و check
  shape؛
- توسعه check enumهای Message، Conversation و Notification برای typeهای جدید؛
- هیچ table/column برای Contact Sync، Avatar، Presence یا View Count ساخته نشده است.

## 7. Validation

| بررسی | نتیجه واقعی |
|---|---|
| `dotnet restore MakanApp.sln` | موفق؛ همه projectها up-to-date |
| build اولیه | موفق پس از اصلاح یک comma در projection؛ `0` warning و `0` error |
| test اولیه | Unit `306/306`، Integration `510/510`، Architecture `14/14` |
| build نهایی | موفق؛ `0` warning و `0` error |
| test نهایی | مجموع `830/830` passed، failed=`0`، skipped=`0` |
| EF pending model | با startup خود Infrastructure موفق؛ `No changes have been made to the model since the last migration.` |
| Migration execution | در developer/production اجرا نشد؛ Integration Testها migrationها را فقط روی LocalDB ایزوله اجرا کردند |

اجرای diagnostic همان EF command با startup پروژه Api به‌دلیل copy نشدن assembly
`Microsoft.EntityFrameworkCore.Design` در output آن host شکست خورد؛ build خود solution
موفق بود. اجرای canonical با `MakanDbContextFactory` و startup پروژه Infrastructure
بدون تغییر package موفق شد و نبود pending model را تأیید کرد.

## 8. محدودیت‌ها و اقدام بعدی

- provider واقعی Child Safety، retention/legal policy، appeal/SLA و PlatformSafety
  operations نیازمند تصمیم مستقل‌اند؛
- Contact Sync نیازمند قرارداد consent، upload حداقلی/hashed، provenance و revoke است؛
- Avatar/Bio و Presence/Typing نیازمند lifecycle و privacy مستقل هستند؛
- view count کانال تنها مورد `UNRESOLVED_PRODUCT_POLICY` در ماتریس است؛
- invite link، album، link extraction، Poll/Topics/Slow Mode و scheduled publishing
  قابلیت‌های deferred هستند؛
- STEP 15 و هرگونه AI integration خارج از این مرحله باقی مانده است.
