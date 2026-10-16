# قرارداد تحویل Backend پیام‌رسان P2

این سند فقط قابلیت‌های واقعاً اضافه‌شدهٔ P2 را شرح می‌دهد؛ جدول ممیزی شروع، فهرست قابلیت‌های تحویل‌شده نیست. قراردادهای P0/P1 و `P1_PUSH_RUNTIME_PENDING` همچنان معتبرند. frontend در این repository تغییر نکرده است.

## Typing و Presence — P2-1 تا P2-4

Hub موجود `/hubs/messaging` با Bearer/session معتبر استفاده می‌شود. هر اتصال پس از reconnect باید subscriptionهای مجاز را دوباره ثبت کند. `SubscribeConversation(conversationId)` همان قرارداد قبلی است؛ مجوز مشاهده و سپس subscription گذرا را بررسی می‌کند. عضویت در SignalR group دلیل مجوز نیست؛ گیرنده و فرستندهٔ hint دوباره از وضعیت جاری بررسی می‌شوند. [مستند رسمی groups](https://learn.microsoft.com/aspnet/core/signalr/groups).

| قرارداد | ورودی / خروجی | رفتار |
|---|---|---|
| `SetTyping` | `conversationId`, `isTyping` | نیازمند subscription همان اتصال و مجوز جاری ارسال؛ Saved و subscriber کانال مجاز نیستند |
| `TypingChanged` | `conversationId`, `userId`, `isTyping`, `expiresAtUtc` | فقط اتصال‌های مشترک مجاز، بدون متن/شماره/سن؛ Message یا Delta نیست |
| `Heartbeat` | بدون ورودی | lease همین اتصال را با ساعت سرور تمدید می‌کند؛ بعد از پایان lease باید reconnect شود |
| `SubscribePresence` | `userId` → `PresenceResult` | snapshot و ثبت مشاهده فقط در ارتباط Direct مجاز موجود و audience مجاز |
| `UnsubscribePresence` | `userId` | فقط subscription همین اتصال را پاک می‌کند؛ idempotent |
| `PresenceChanged` | `userId`, `state`, `lastSeenAtUtc`, `expiresAtUtc` | hint به گیرندهٔ دوباره‌مجازشده؛ فعالیت مقصد برای viewer مخفی broadcast نمی‌شود |
| `GET /api/v1/people/{userId}/presence` | `PresenceResult` | authenticated، rate limit موجود discovery؛ بدون رابطه/مجوز → `Hidden` با timestamp=null |

`PresenceResult.state` یکی از `Online`, `Recently`, `Offline`, `Hidden` است. Online از اتصال زنده و UserSession معتبر ساخته می‌شود، نه HTTP اخیر. یک disconnect با دستگاه زندهٔ دیگر Offline نمی‌کند. Recently پنجرهٔ تقریبی پنج‌دقیقه‌ای است؛ Offline ادعای تاریخچهٔ دائمی حضور نیست. timestamp دقیق فقط با مجوز Last Seen و وقتی اتصال معتبر Online نیست برمی‌گردد؛ زمان heartbeat کاربر Online از Last Seen لو نمی‌رود.

در `GET/PUT /api/v1/messaging/privacy` دو فیلد `onlineAudience` و `lastSeenAudience` از نوع `Everyone | Contacts | Nobody` اضافه شده‌اند. پیش‌فرض مصوب هر دو **Contacts** است؛ منظور دفترچهٔ مقصد است، نه دفترچهٔ viewer. حذف دو فیلد از PUT قدیمی مقدار جاری آن‌ها را حفظ می‌کند؛ `expectedVersion` و رفتار concurrency قبلی لازم‌اند. Everyone مجوز مستقل رابطه ایجاد نمی‌کند. Block هر طرف و از دست رفتن رابطهٔ مجاز Minor/Unknown مشاهدهٔ تازه را مخفی می‌کنند؛ Guardian/Manager به چت خصوصی دیگری دسترسی نمی‌گیرند.

TTL پیش‌فرض Typing شش ثانیه و حداقل refresh سه ثانیه است. با `false`، ارسال موفق پیام از همان نشست، unsubscribe، disconnect یا انقضا پاک می‌شود. اتصال دیگر همان کاربر ممکن است همچنان Typing باشد. کلاینت حتی بدون رویداد stop باید در `expiresAtUtc` indicator را پاک کند. Presence lease پیش‌فرض ۴۵ ثانیه و حداقل heartbeat ده ثانیه است؛ heartbeat حدود هر ۱۵ ثانیه مناسب این پیش‌فرض است. پس از انقضای snapshot، وضعیت را از REST تازه کنید؛ دریافت نکردن hint به معنی ادامهٔ Online نیست.

خطاهای Hub شامل `AUTH_REQUIRED`، کدهای جاری مجوز گفتگو و `MESSAGING_ACTIVITY_RATE_LIMITED` هستند. HTTP بدون نشست 401 و lookup بیش‌ازبودجه 429 دارد. خطای فنی پایگاه داده به موفقیت ساختگی تبدیل نمی‌شود. نه OTP و نه پیامک واقعی برای تست محلی این قابلیت ارسال نشده است.

اگر Online مخفی و فقط Last Seen مجاز باشد، تغییرات Recently بدون timestamp ناشی از اتصال/heartbeat نیز broadcast نمی‌شوند؛ ریتم رویداد نباید راه فرعی تشخیص فعالیت دقیق شود. snapshot مستقیم همچنان نتیجهٔ تقریبی مجاز را می‌دهد و پس از disconnect، زمان Last Seen مجاز قابل دریافت است.

## تنظیم‌های اختیاری گذرا

همگی زیر `Messaging:Ephemeral` هستند؛ هیچ appsettings یا secret موجود تغییر نکرده است. environment از `__` استفاده می‌کند.

| کلید | پیش‌فرض |
|---|---:|
| `TypingTtlSeconds` | 6 |
| `TypingRefreshSeconds` | 3 |
| `ConnectionLeaseSeconds` | 45 |
| `HeartbeatSeconds` | 10 |
| `RecentlyWindowSeconds` | 300 |
| `LastSeenRetentionSeconds` | 86400 |
| `MaximumConnections` | 10000 |
| `MaximumConnectionsPerUser` | 10 |
| `MaximumSubscriptionsPerConnection` | 50 برای هر یک از مجموعه‌های گفتگو/Presence |
| `CommandsPerMinute` | 120 برای هر اتصال |

مقادیر ناسازگار lease/TTL و سقف‌های غیرمجاز در startup رد می‌شوند. حافظه و pending hintها bounded هستند؛ در فشار hint ممکن است حذف شود و TTL مرجع پاک‌کردن آن است. command به‌جز heartbeat، lease را تمدید نمی‌کند.

Migration: `20261016163433_AddMessagingPresencePrivacy` فقط دو ستون audience با default مصوب Contacts و دو CHECK مقدار enum به `messaging.MessagingPrivacySettings` می‌افزاید؛ PK/FK/rowversion قبلی حفظ می‌شوند. در startup migration خودکار اجرا نمی‌شود. هیچ timestamp حضور، Typing یا event حضور در SQL ذخیره نمی‌شود.

`DISTRIBUTED_EPHEMERAL_STATE=DEFERRED` و `MESSENGER_P2_DISTRIBUTED_PRESENCE=PENDING_SCALE_INFRA`: پیاده‌سازی فعلی فقط یک process است؛ restart تاریخچهٔ Last Seen حافظه‌ای را از بین می‌برد و scale-out بدون state/backplane مشترک پشتیبانی‌شده ادعا نمی‌شود. Recording indicator هنوز قرارداد جدا ندارد و ساخته نشده است.

## Saved Labels و پوشه‌ها — P2-5/P2-6

Saved پایه همان `GET /api/v1/conversations/saved` و همان گفتگو/پیام مستقل موجود است. Tag جدید نه فضای ذخیره‌سازی دوم است، نه دسترسی مجدد به منبع Forward. متن/مدیا و Forward همچنان از مسیرهای موجود ارسال می‌شوند؛ metadata منبع غیرمجاز افشا نمی‌شود.

| Method / Route | Request | Response و قواعد |
|---|---|---|
| `GET /api/v1/conversations/saved/messages/{messageId}/labels` | بدون body | `{messageId,labels,version}`؛ بدون label-set، labels=[] و version=null؛ فقط پیام Saved قابل‌مشاهدهٔ خود caller |
| `PUT /api/v1/conversations/saved/messages/{messageId}/labels` | `{labels:["درس"],expectedVersion:null}` برای بار اول؛ سپس version جاری | جایگزینی کامل labelها؛ array خالی پاک‌کردن label است، نه حذف پیام؛ تغییر هم‌زمان → 412 |
| `GET /api/v1/messages/search?savedLabel=درس` | `savedLabel` با فیلترهای قبلی سازگار | فقط Saved خود caller؛ در این حالت q اختیاری است؛ بدون q و بدون label همچنان خطای قبلی |
| `GET /api/v1/messaging/folders` | بدون body | حداکثر ۲۰ تنظیم پوشهٔ همین کاربر با overrides و version؛ نه محتوای گفتگوها |
| `PUT /api/v1/messaging/folders/{folderId}` | نام، sortOrder، includedTypes، excludedTypes، unreadOnly، overrides، expectedVersion | ساخت با Guid پایدار کلاینت و expectedVersion=null؛ ویرایش با version جاری؛ پاسخ ChatFolderResult |
| `DELETE /api/v1/messaging/folders/{folderId}?expectedVersion=...` | version جاریِ URL-encoded | 204؛ فقط پوشه و overrideهایش حذف می‌شوند، نه گفتگو/پیام/عضویت |
| `GET /api/v1/conversations?folderId=...` | selector اختیاری، همراه filter/page/pageSize/q موجود | همان ConversationSummaryResultهای مجاز فعلی؛ فیلتر پوشه قبل از pagination در SQL اعمال می‌شود |

نمونهٔ body پوشه:

```json
{"name":"درس‌ها","sortOrder":0,"includedTypes":"Group, Channel","excludedTypes":"None","unreadOnly":false,"overrides":[],"expectedVersion":null}
```

نوع Flags برابر `None=0, Direct=1, Group=2, Channel=4, Saved=8, All=15` است؛ نام رشته‌ای canonical است و bit ناشناخته رد می‌شود. هر override برابر `{conversationId,included,pinOrder}` است؛ `pinOrder` اختیاری ۰ تا ۱۹۹ و فقط برای included=true مجاز است. explicit exclude بر قاعدهٔ type مقدم است؛ explicit include می‌تواند type filter را override کند، اما هرگز authorization/فیلتر archive/workspace را override نمی‌کند. pinOrder صعودی مقدم بر ترتیب قبلی فهرست است؛ در نبود آن ترتیب موجود حفظ می‌شود. sortOrder بین ۰ تا ۱۰۰۰۰، نام trimشده حداکثر ۸۰ نویسهٔ بدون control، overrides حداکثر ۲۰۰ و ConversationIdها یکتا هستند. پوشهٔ سیستمی hard-code نشده است.

هر پیام حداکثر ده label با طول ۳۲ دارد؛ trim، Unicode NFKC و uppercase invariant canonical می‌شود و تکرارها حذف می‌شوند. label، یادداشت آزاد جدید یا taxonomy سلسله‌مراتبی نیست. DeleteForMe/Clear/DeleteForEveryone و scope Saved برای خواندن، تغییر و جست‌وجو معتبرند. درخواست روی پیام غیرSaved یا مال دیگری 404 است. پوشهٔ غیرمالک 404، body/enum/version نامعتبر 400 و stale version برابر `MESSAGING_PRESENTATION_CONFLICT` با 412 است. GETها authenticated و mutationها rate-limited هستند؛ revoke نشست از pipeline موجود پیروی می‌کند.

سازمان‌دهی پایدار source-of-truth در REST است؛ دستگاه دوم بعد از refresh نسخه/labelهای تازه را می‌گیرد. در این فاز event ساختگی برای Folder/Label به گفتگوی نامرتبط تزریق نشده است؛ این تغییرات را از Delta پیام‌ها حدس نزنید. پس از timeout ساخت با Guid یکسان، تنظیم‌های جاری را GET کنید؛ expectedVersion=null پوشهٔ موجود را کورکورانه بازنویسی نمی‌کند.

Migration `20261016170915_AddMessagingPersonalOrganization` چهار جدول `messaging.ChatFolders`, `FolderConversationOverrides`, `SavedMessageOrganizations`, `SavedMessageLabels` را با FK، کلید مرکب، CHECK، index و rowversion مجموعه می‌سازد. آخرین دو جدول فقط سازمان‌دهی همان Message موجودند؛ canonical Saved جدید ایجاد نمی‌کنند. migration خودکار startup و apply روی سرور انجام نشده است.

## مدیریت پیشرفته، لینک و درخواست عضویت — P2-7 تا P2-10

نقش‌های Owner/Admin/Member قبلی عوض نشده‌اند. سه flag واقعی اضافه شده‌اند: `ManageInvites=512` برای لینک/درخواست، `BanMembers=1024` برای محدودیت عضو، و `ChangeInfo=2048` برای عنوان/توضیح. `All=4095` است؛ permissionهای Adminهای قبلی خودکار زیاد نمی‌شوند و LegacyAdminDefault تغییر نکرده است. ManageInvites به معنی ManageAdmins یا PublishMessages نیست. subscriber کانال هنوز نمی‌تواند منتشر کند. محدودیت scope/عضویت/پنجرهٔ history و مرزهای Group رسمی کلاس پابرجاست؛ سیستم رسمی، با API مدیریت دستی عضو یا لینک override نمی‌شود.

همهٔ routeهای زیر authenticated و دارای policy محدودیت نرخ `messaging-send` هستند. page پیش‌فرض ۱، pageSize پیش‌فرض ۳۰ و حداکثر ۱۰۰ است. `HasMore` فقط در محدودهٔ مجاز همان پاسخ محاسبه می‌شود.

| Method / Route | Request | پاسخ و مجوز |
|---|---|---|
| `POST /api/v1/conversations/{id}/invite-links` | `{clientLinkId,expiresAtUtc,maximumUses,requiresApproval}` | `{link,token,alreadyExisted}`؛ Owner/Admin با ManageInvites؛ توکن فقط بار اول |
| `GET /api/v1/conversations/{id}/invite-links` | page/pageSize | `{items,hasMore}`؛ فقط metadata و version؛ نه token و نه hash |
| `POST /api/v1/conversations/{id}/invite-links/{linkId}/revoke` | `{expectedVersion}` | metadata تازه؛ لغو دوباره idempotent؛ stale version=412 |
| `POST /api/v1/messaging/invite-links/redeem` | `{token}` | `{conversationId,alreadyMember,joinRequest}`؛ توکن در body، نه URL؛ هویت target از session |
| `GET /api/v1/conversations/{id}/join-requests` | page/pageSize | مدیریت درخواست فقط با ManageInvites |
| `GET /api/v1/messaging/join-requests` | page/pageSize | فقط درخواست‌های خود caller؛ بدون عنوان/متن چت، شماره یا سن |
| `POST /api/v1/messaging/join-requests/{requestId}/approve` | `{expectedVersion}` | فقط مدیر مجاز فعلی همان Group/Channel؛ revalidation کامل |
| `POST /api/v1/messaging/join-requests/{requestId}/reject` | `{expectedVersion}` | مدیر مجاز؛ بدون ایجاد عضویت |
| `POST /api/v1/messaging/join-requests/{requestId}/cancel` | `{expectedVersion}` | فقط صاحب درخواست؛ بدون ایجاد عضویت |
| `GET /api/v1/conversations/{id}/member-restrictions` | page/pageSize | فقط Owner/Admin دارای BanMembers؛ وضعیت/نسخه، بدون reason خام audit |
| `PUT /api/v1/conversations/{id}/members/{userId}/restriction` | `{isBanned,deniedCapabilities,untilUtc,reason,expectedVersion}` | محدودیت resource-bound؛ عضو/نقش سراسری، Enrollment و User تغییر نمی‌کنند |
| `PATCH /api/v1/conversations/{id}/info` | `{title,description,expectedVersion}` | فقط Owner/Admin دارای ChangeInfo؛ همان سقف‌های ۱۶۰/۱۰۰۰ نویسهٔ قبلی |

توکن ۳۲ بایت تصادفی cryptographic و ۴۳ نویسهٔ Base64url است؛ فقط SHA-256 آن در SQL می‌ماند. انقضا UTC، آینده و حداکثر ۳۰ روز مطابق دعوت موجود است؛ maximumUses از ۱ تا ۱۰۰۰۰، usedCount server-owned و CHECK استفاده <= سقف دارد. clientLinkId یک Guid idempotency است و **bearer secret نیست**. retry ساخت با همان شناسه/بدنه metadata همان رکورد و token=null می‌دهد؛ توکن گم‌شده از hash بازیابی نمی‌شود، باید لینک را لغو و با شناسهٔ تازه ایجاد کرد. توکن/هش در audit/list نیستند؛ کلاینت هم نباید token را log کند.

مالک از حالت loading ساخت لینک، فقط پس از پاسخ دارای token امکان copy دارد. token=null در retry به معنی لینک تازه یا توکن قابل‌بازیابی نیست. Expired/Revoked/Exhausted یا context/رابطهٔ نامعتبر، `CONVERSATION_INVITE_LINK_UNAVAILABLE` با 404 می‌دهند و نباید از آن سن/علت خصوصی حدس زد. لغو لینک، عضویت قبلاً پذیرفته‌شده را حذف نمی‌کند. عضو فعال در retry معتبر مصرف اضافه ندارد؛ درخواست Pending هم مصرف نمی‌کند. مصرف فقط هنگام ایجاد یک عضویت فعال تازه یا approval واقعی است و آخرین ظرفیت با lock گفتگو/transaction سریال می‌شود.

requiresApproval=true فقط `Pending` می‌سازد؛ target هنوز نمی‌تواند history بخواند. وضعیت‌ها `Pending/Approved/Rejected/Cancelled/Expired` هستند. انقضای مؤثر در read به‌صورت Expired دیده می‌شود؛ درخواست منقضی قابل approval یا بازنویسی با cancel/reject نیست. درخواست تکراری Pending همان شناسه را برمی‌گرداند. تکرار همان تصمیم نهایی idempotent است و عضویت حذف‌شده را دوباره فعال نمی‌کند. پس از لغو/پایان قبلی، درخواست تازه نیازمند اقدام صریح تازه است.

پذیرش لینک یک POST صریح از نشست معتبر target است؛ دانستن UserId جای آن نیست. پیش از join و approval، scope، حساب، نقش و permission فعلی سازنده، audience، حریم خصوصی target، رابطهٔ جاری GAP-05، Ban و وضعیت لینک بازبینی می‌شوند. در سازمان، Membership/Role سازنده و درخواست‌کننده از context **سروری** همان اقدام ذخیره و دوباره validate می‌شوند؛ context مدیر تأییدکننده به‌جای آن‌ها مصرف نمی‌شود. لینک/approval هیچ Membership سازمانی، User، Credential یا Enrollment نمی‌سازد. Contact ثبت‌شده در دفترچهٔ **خودِ حساب محافظت‌شده** طبق GAP-05 رابطهٔ رضایتی مصوب است؛ Contact یک‌طرفهٔ غریبه یا legacy grant به‌تنهایی مجوز نیست. Everyone در privacy هم بدون رابطهٔ لازم، Stranger→Minor/Unknown را مجاز نمی‌کند.

Ban، عضویت فعال Messaging را با همان lifecycle Removed پایان می‌دهد و سطر ممنوعیت جدا دارد؛ history در SQL حذف نمی‌شود، اما دسترسی عضو حذف‌شده مطابق P0 قطع می‌شود. همهٔ Join/Link/Add/دعوت قبلی و Send/Typing وضعیت را تازه می‌سنجند. Unban یا انقضای Ban، عضویت را خودکار زنده نمی‌کند؛ join صریح مجاز لازم است. `deniedCapabilities` از `SendMessages | SendMedia | InviteMembers` است؛ محدودیت فردی بر اختیار عمومی مقدم است. untilUtc=null یعنی دائمی، مقدار زمانی باید UTC آینده باشد؛ پاک‌کردن محدودیت یعنی isBanned=false، None و untilUtc=null. reason بین ۱ تا ۴۰۰ نویسهٔ بدون control فقط برای audit است. Admin بدون ManageAdmins نمی‌تواند Admin دیگر را محدود کند؛ Owner و خودِ actor محدودشدنی نیستند.

400 برای body/enum/bit ناشناخته، 401 برای نشست لغوشده، 403 برای permission یا `CONVERSATION_MEMBER_RESTRICTED`، 404 برای منبع غیرقابل‌دسترسی/لینک نامعتبر و 412 برای نسخهٔ کهنه است. ورودی bit ناشناخته در مسیر تغییر role نیز اکنون در Application با 400 رد می‌شود، نه exception دامنه با 500. loading/empty فهرست‌ها واقعاً [] و HasMore=false است؛ UI از داشتن شناسه، عضو بودن یا permission نتیجه نگیرد.

در این فاز وضعیت خصوصی لینک/درخواست/محدودیت با REST/version refresh می‌شود و به تمام اعضای گفتگو broadcast نمی‌شود. عضویت فعال تازه یا پایان عضویت همان ParticipantChanged موجود را با audience جاری تولید می‌کند؛ metadata لینک و وجود درخواست خصوصی داخل event عمومی قرار نمی‌گیرند. تغییر info همان ConversationChanged موجود است. تمام mutationهای جدید نشست و role سازمانی را داخل transaction نیز بازبینی می‌کنند؛ revoke پس از اولین auth نمی‌تواند گیت جاری را دور بزند.

Migration `20261016174429_AddConversationInvitesAndRestrictions` سه جدول لینک/درخواست/محدودیت، FK مرکب لینک+گفتگو، unique درخواست Pending، rowversion، CHECK استفاده/وضعیت/context و indexها را می‌سازد و CHECK permission را به ۴۰۹۵ گسترش می‌دهد. permission قدیمی backfill/upgrade خودکار نمی‌شود. Down وقتی flag جدید واقعاً استفاده شده باشد، پیش از حذف جدول با خطای روشن متوقف می‌شود؛ کاهش بی‌اجازهٔ permission راه rollback نیست و backup/تصمیم عملیاتی لازم است. هیچ migrate یا deploy روی سرور انجام نشده است.

## Topics — P2-11

Topic فقط در Group فعال است؛ Direct/Channel/Saved موضوع ندارند. permission مستقل `ManageTopics=4096` اضافه شده و `All=8191` است؛ LegacyAdminDefault و permissionهای ذخیره‌شده افزایش خودکار ندارند. Owner یا Admin با همین permission مدیریت می‌کند. در Group رسمی، اختیار مؤثر موضوع فقط از Teacher در context فعلی و TeacherAssignment جاری همان کلاس می‌آید؛ participantها به Admin تبدیل نمی‌شوند، Manager مدرسه عضو خودکار نیست و Student حق مدیریت ندارد.

| Method / Route | Request | Response / رفتار |
|---|---|---|
| `GET /api/v1/conversations/{id}/topics` | page=1/pageSize=30، حداکثر ۱۰۰ | `{items,hasMore,canManage}`؛ عنوان/وضعیت/سازنده/UTC/version؛ فقط محدودهٔ خواندن جاری |
| `POST /api/v1/conversations/{id}/topics` | `{clientTopicId,title}` | همان Topic؛ شناسه Guid ثابت و title trimشده ۱ تا ۱۶۰؛ retry یکسان `alreadyExisted=true` |
| `PUT /api/v1/conversations/{id}/topics/{topicId}` | `{title,status,expectedVersion}` | نسخهٔ جدید؛ status=`Open` یا `Closed`؛ بازگشایی صریح با permission فعلی |
| Send موجود | `topicId` اختیاری در body | فقط موضوع Open همان Group؛ TopicId پس از ایجاد پیام جابه‌جا نمی‌شود |
| Forward موجود | `destinationTopicId` اختیاری | محدودیت مقصد و گیت ایمنی موجود؛ موضوع مبدأ به مقصد نشت نمی‌کند |
| history موجود | `topicId` اختیاری query | فیلتر موضوع پیش از pagination و پس از مجوز/history/Clear/DeleteForMe |

همهٔ APIهای Topic authenticated، mutationها rate-limited با `messaging-send` و current-state/نسخه دارند. 400=`CONVERSATION_TOPIC_INVALID`، 404=`CONVERSATION_TOPIC_NOT_FOUND` یا ConversationNotFound برای دسترسی نامعتبر، 403 برای permission، 409=`CONVERSATION_TOPIC_CLOSED` برای ارسال تازه به موضوع بسته، و 412 برای نسخهٔ کهنه است. UI پس از 412 باید GET و تصمیم کاربر را تکرار کند، نه overwrite بی‌صدا. Loading/empty واقعاً صفحهٔ خالی است؛ canManage راهنمای UI است، نه مجوز bearer.

بستن موضوع پیام‌ها را حذف نمی‌کند؛ همان history مجاز قابل‌خواندن است. FromJoin، عنوان موضوع قدیمی و TopicChanged قدیمی را تا وجود محتوای قابل‌مشاهدهٔ آن موضوع مخفی می‌کند؛ افزودن topicId هیچ مسیر دورزدن history نمی‌سازد. حذف عضو/پایان assignment دسترسی تازه به موضوع، پیام و Delta را می‌بندد. `TopicChanged=13` فقط شناسه/نسخهٔ منبع و cursor موجود را می‌فرستد؛ کلاینت REST جاری را refresh می‌کند. همان revalidation مخاطب Delta/SignalR موجود، نه گروه broadcast بدون کنترل، استفاده می‌شود.

عنوان Topic در audience محافظت‌شده از همان Internal Content Safety و پروندهٔ moderation فعلی عبور می‌کند؛ Unavailable fail-closed است. برای عنوان نه Message ساختگی ساخته می‌شود و نه network داخل transaction است. مجوز و audience بعد از provider و داخل transaction دوباره کنترل می‌شوند. fingerprint پیام قدیمی بدون Topic تغییر نمی‌کند.

Migration `20261016183900_AddConversationTopics`: جدول Topics با PK/alternate key `(Id,ConversationId)`، FK، index، CHECK، rowversion؛ ستون nullable TopicId روی Messages و FK مرکب هم‌گروه، index فیلتر/ترتیب؛ گسترش CHECKهای permission/change type. هیچ backfill پیام تاریخی یا migration سرور انجام نشده است. rollback در حضور topic/event/permission تازه، پیش از تغییر داده متوقف می‌شود و تصمیم عملیاتی/backup لازم دارد. Topic Pin/Unread مستقل و Archived lifecycle در این فاز ادعا نمی‌شوند؛ پیام‌ها از pin/read موجود استفاده می‌کنند.
