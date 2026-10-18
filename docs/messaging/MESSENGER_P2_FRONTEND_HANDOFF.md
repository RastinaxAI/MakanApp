# قرارداد تحویل Backend پیام‌رسان P2

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-18` از `main@ef67624`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

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

نقش‌های Owner/Admin/Member قبلی عوض نشده‌اند. سه flag واقعی اضافه شده‌اند: `ManageInvites=512` برای لینک/درخواست، `BanMembers=1024` برای محدودیت عضو، و `ChangeInfo=2048` برای عنوان/توضیح. All در این فاز تاریخی 4095 بود؛ با ManageTopics در فاز بعد، **All جاری=8191** است. permissionهای Adminهای قبلی خودکار زیاد نمی‌شوند و LegacyAdminDefault تغییر نکرده است. ManageInvites به معنی ManageAdmins یا PublishMessages نیست. subscriber کانال هنوز نمی‌تواند منتشر کند. محدودیت scope/عضویت/پنجرهٔ history و مرزهای Group رسمی کلاس پابرجاست؛ سیستم رسمی، با API مدیریت دستی عضو یا لینک override نمی‌شود.

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

## Scheduled / Silent — P2-15 تا P2-17

| Method / Route | Request | Response / رفتار |
|---|---|---|
| `POST /api/v1/conversations/{id}/scheduled-messages` | `{scheduledAtUtc,message:{clientMessageId,kind,text,attachmentIds,replyToMessageId,mentionedUserIds,contactUserId,topicId,isSilent}}` | intent خصوصی با Id/ClientMessageId/status/UTC/content/version؛ هنوز Message/Sent نیست |
| `GET /api/v1/conversations/{id}/scheduled-messages` | page=1/pageSize=30، سقف ۱۰۰ | فقط intentهای خود caller با current read access؛ `items/hasMore` |
| `POST /api/v1/conversations/{id}/scheduled-messages/{scheduleId}/cancel` | `{expectedVersion}` | لغو نسخه‌دار پیش از dispatch؛ تکرار Cancelled idempotent |

همه authenticated و rate-limited با `messaging-send` هستند؛ شناسهٔ User یا context از body گرفته نمی‌شود. زمان درخواست جدید باید UTC آینده باشد؛ زمان client، timestamp قبول یا Sent نیست. محتوا همان محدودیت و reference validation ارسال جاری را دارد. سقف ۱۰۰ intent Scheduled برای هر sender/conversation زیر قفل گفتگو کنترل می‌شود. clientMessageId رزرو دائمی اثر منطقی است: retry ساخت با همان بدنه/زمان، حتی پس از موعد، همان intent را می‌دهد؛ تغییر بدنه/موعد با شناسهٔ قبلی 412 است. Send/Forward فوری با شناسهٔ رزروشده اجازهٔ ارسال زودهنگام یا دوباره‌فرستادن intent لغوشده را ندارد (409).

حالت‌ها `Scheduled/Dispatched/Cancelled/Failed` هستند. Scheduled همراه LastFailureCode موقت هم هنوز Sent نیست؛ Dispatched فقط بعد از commit پیام واقعی، DispatchedMessageId و Outbox در یک transaction است. initial validation فقط ثبت قصد است؛ permission/role/membership/account/Block/privacy/GAP-05/Safety/attachment/topic در dispatch دوباره سنجیده می‌شوند. authority از workspace/Membership/Role سروریِ ثبت قصد می‌آید، نه نقش مدیر دیگر یا workspace جدید دستگاه. خروج از نشست به‌تنهایی قصد صریح پایدار را لغو نمی‌کند؛ لغو نشست فوراً حق REST mutation آن نشست را می‌گیرد و حساب/روابط همچنان در dispatch جاری کنترل می‌شوند. worker نشست ساختگی یا credential جدید نمی‌سازد.

worker با SQL source-of-truth، هر بار یک lease سی‌ثانیه‌ای، batch محدود بیست‌تایی و polling دوثانیه‌ای کار می‌کند؛ تنظیم اجباری جدید در appsettings ندارد. provider بیرون transaction است. retry موقت ۵/۱۰/۲۰/۴۰ ثانیه و حداکثر پنج تلاش است؛ Unavailable برای protected audience هیچ Message نمی‌سازد. خطای دائمی/ابطال مجوز Failed با code پایدار و بدون stack/content خام است. crash/restart از lease منقضی بازیابی می‌شود؛ حتی crash تلاش پنجم در Scheduled گیر نمی‌کند. worker قدیمی نمی‌تواند با token قبلی send کند یا وضعیت نهایی را بازنویسی کند.

Cancel و dispatch با ترتیب قفل Conversation→Scheduled هماهنگ‌اند. claim می‌تواند version را تازه کند؛ UI روی 412 باید GET کند و اگر هنوز Scheduled است، تصمیم لغو را با نسخهٔ تازه بفرستد. در رقابت، یا Cancelled و بدون پیام، یا Dispatched و یک پیام داریم؛ دریافت دو پاسخ به معنی دو اثر نیست. ویرایش/جابه‌جایی موعد intent در این فاز API ندارد (طبق optional بودن edit در prompt)؛ Cancel و ساخت قصد تازه با ClientMessageId تازه انجام شود. بعد از dispatch فقط Edit/Delete عادی و پنجرهٔ مصوب قبلی معتبر است.

اطلاعات صف حتی برای Owner/Admin گفتگوی دیگر، queue عمومی نیست؛ فقط صاحبِ دارای دسترسی جاری آن را می‌بیند. پاسخ شامل token lease یا hash نیست. وضعیت خصوصی Scheduled از REST/version همگرا می‌شود و به گروه broadcast نمی‌شود؛ پیام واقعی پس از dispatch همان MessageCreated/Delta/SignalR قبلی را دارد. حذف عضویت، دسترسی صف/ارسال تازه را می‌بندد، نه اینکه history را از SQL پاک کند.

`isSilent` اختیاری و پیش‌فرض false به Send و Forward افزوده شده؛ در history نیز flag واقعی وجود دارد. Silent intent مستقل از Mute گیرنده، immutable و بخشی از idempotency payload است؛ retry با Silent متفاوت 409 می‌دهد. پیام بی‌صدا همچنان ذخیره، Unread، SignalR و اعلان داخلی دارد؛ فقط delivery بیرونی در intent و دوباره در current delivery gate سرکوب می‌شود. provider SMS/OTP دست‌نخورده است. fingerprint قدیمیِ پیام عادی بدون flag جدید تغییر نمی‌کند.

خطاها: 400=`SCHEDULED_MESSAGE_INVALID`؛ 404=`SCHEDULED_MESSAGE_NOT_FOUND`/ConversationNotFound؛ 412=`SCHEDULED_MESSAGE_CONFLICT`؛ 409=`SCHEDULED_MESSAGE_ID_RESERVED`؛ نشست لغوشده 401 و current permission همان خطای فعلی Messaging است. `SCHEDULED_MESSAGE_CLAIM_LOST` گیت داخلی worker است، نه پارامتر قابل‌ارسال توسط client. loading/empty صف با []/HasMore=false نمایش داده شود؛ هیچ job در انتظار، indicator «ارسال شد» نمی‌گیرد.

Migration `20261016190927_AddScheduledAndSilentMessages`: سه جدول Scheduled/Attachments/Mentions، PK/FKهای واقعی، unique conversation+user+clientMessageId، rowversion، CHECKهای lifecycle/context/hash/lease/attempt/time، index صف و فهرست؛ IsSilent bit با false برای پیام‌های قدیمی. payload typed است، نه JSON اجرایی یا fake Message. Down در حضور قصد پایدار یا پیام Silent واقعی پیش از حذف ساختار توقف دارد؛ سرور/دیتابیس عادی migrate نشده‌اند.

## Search و سخت‌سازی قرارداد جاری — P2-18 و بازبینی P2-19 تا P2-30

`GET /api/v1/messages/search` علاوه بر q/senderUserId/conversationId/fromUtc/toUtc/kind/savedLabel/cursor/limit، سه selector اختیاری دارد: `hasAttachment=true|false` برای وجود پیوست READY؛ `hasLinks=true|false` برای وجود نشانگر http:// یا https:// در متن فعلی، بدون واکشی یا پردازش URL؛ `topicId` برای همان Topic مجاز. presence یک فیلتر جدید یا savedLabel اجازهٔ q خالی می‌دهد، نه جست‌وجوی بدون هیچ عبارت/فیلتر. مقدار false نیز فیلتر واقعیِ مکمل است. نمونه: `/api/v1/messages/search?hasLinks=true&hasAttachment=false&limit=20`. همهٔ فیلترها پیش از count/cursor/pagination در SQL و داخل current workspace/role/membership/history اعمال می‌شوند؛ 0/[] برای محدودهٔ غیرمجاز oracle وجود گفتگو نیست. Guid خالی، enum/cursor/limit نامعتبر 400 است. هویت senderها یک batch است، نه query به‌ازای هر نتیجه.

`GET /api/v1/conversations/{id}/me/restriction` فقط وضعیت محدودیت خود عضو دارای دسترسی جاری را می‌دهد: `{conversationId,userId,isBanned,deniedCapabilities,untilUtc,isEffective,version}`. نبود سطر برابر None/false/version=null است؛ انقضا IsEffective را false می‌کند، نه اینکه Ban عضویت را دوباره فعال کند. reason مدیر نمایش داده نمی‌شود و این route مجوز فهرست محدودیت دیگران نمی‌دهد. عضو حذف‌شده 404 می‌گیرد؛ capability پاسخ hint است و هر Send دوباره مجاز می‌شود.

Contact Card همان پروفایل عمومیِ مجاز موجود است، نه راه کشف تازه: source و همهٔ مخاطبان بالفعلِ مجاز باید بتوانند subject را مطابق رابطهٔ جاری ببینند. دانستن UserId یا رضایت subject فقط به فرستنده کافی نیست. هر Send/Forward و dispatch زمان‌بندی دوباره بررسی می‌شود؛ شکست 404 بدون افشای علت خصوصی است. کارت تاریخی حذف/بازنویسی نمی‌شود و هیچ شمارهٔ تأییدشده‌ای در کارت قرار نمی‌گیرد. منطق کشف پروفایل تغییر محصولی نکرده و predicate موجود reuse شده است.

در کلاس رسمی، مخاطب از Enrollment/TeacherAssignment جاری محاسبه می‌شود، نه صرفاً projection participantها؛ عضو ثبت‌نام‌شدهٔ تازه که هنوز در Messaging همگام نشده نیز باید بتواند subject کارت را ببیند. ثبت‌نام هم‌زمان در حین Safety، guard نهایی را دور نمی‌زند.

ویرایش عنوان/توضیح گروه یا کانال با audience محافظت‌شده از همان Safety و پروندهٔ scoped می‌گذرد؛ unavailable هیچ metadata تازه‌ای ثبت نمی‌کند. permission/رابطه/نشست پس از provider و داخل transaction دوباره سنجیده می‌شوند. retry دقیقِ Topic ثبت‌شده، با current permission و همان title، به providerِ در دسترس نیاز ندارد و Topic/event تازه نمی‌سازد. Close/Reopen Topic با Close نهایی Poll یکسان نیست.

GET پوشه/برچسب، parent/version و فرزندان همان نسخه را برمی‌گرداند؛ mutation و خواندن مجموعه با ترتیب قفل مشترک سریال می‌شوند. ورودی خام نام/label پیش از normalization سقف ۱۰۲۴ دارد و سقف نهایی ۸۰/۳۲ باقی است. controllerهای metadata فوق policy بدنهٔ ۶۴ KiB دارند؛ تست attribute/HTTP در TestServer اثبات محدودیت reverse proxy یا Kestrel روی سرور نیست. هیچ تنظیم جدید اجباری، migration یا event گروهی خصوصی افزوده نشده است.

## Poll / Quiz — قرارداد نهایی مصوب P2-12 و P2-13

ساخت فقط از endpoint اختصاصی زیر است؛ `kind=Poll` در Send عمومی یا Scheduled پذیرفته نمی‌شود. پاسخ ساخت همان `MessageReceiptResult` واقعی، `kind="Poll"` و status=200 است؛ برای خواندن تعریف و نسخهٔ مستقل Poll، GET جزئیات لازم است. پیام عمومی، Revision، Search، Reply و اعلان فقط متن پرسش را دارند؛ client گزینه‌ها را از GET مجاز می‌گیرد. server مجوز را از نشست و resource جاری تعیین می‌کند، نه `UserId` ارسالی.

| Method | Route | Request / Response |
|---|---|---|
| POST | `/api/v1/conversations/{conversationId}/polls` | `CreateConversationPollCommand` → `MessageReceiptResult` |
| GET | `/api/v1/conversations/{conversationId}/messages/{messageId}/poll` | `ConversationPollResult` |
| POST | مسیر GET + `/vote` | `{optionNumbers:[1],expectedVoteVersion:null}` → جزئیات تازه |
| POST | مسیر GET + `/close` | `{expectedVersion:"<PollVersion>"}` → جزئیات Closed |
| GET | مسیر GET + `/voters?optionNumber=1&page=1&pageSize=30` | فقط NonAnonymous؛ `ConversationPollVoterPageResult` |

نمونهٔ ساخت Poll:

```json
{
  "clientMessageId": "11111111-1111-4111-8111-111111111111",
  "question": "کدام زمان مناسب است؟",
  "options": ["شنبه", "یکشنبه"],
  "allowsMultipleAnswers": false,
  "isAnonymous": true,
  "isQuiz": false,
  "topicId": null,
  "isSilent": false
}
```

Quiz از همین endpoint با `isQuiz=true`، `allowsMultipleAnswers=false`، دقیقاً یک `correctOptionNumber` معتبرِ یک‌مبنا و `explanation` اختیاری ساخته می‌شود. `isAnonymous` باید حتی برای Quiz صریح باشد؛ null/حذف آن مجاز نیست. پرسش حداکثر ۳۰۰، ۲ تا ۱۰ گزینهٔ غیرتکراری با طول ۱۵۰، explanation حداکثر ۱۰۰۰؛ کنترل‌کاراکتر در پرسش/گزینه و ورودی خام بیش از ۴۰۰۰ رد می‌شود. explanation فقط در Quiz است و مانند پرسش/همهٔ گزینه‌ها زیر گیت Content Safety قرار دارد. بدنهٔ controller سقف ۶۴ KiB دارد.

`ConversationPollResult` شامل `messageId/conversationId/question/allowsMultipleAnswers/isAnonymous/isQuiz/status/createdAtUtc/closedAtUtc/options/totalVoters/ownVoteVersion/correctOptionNumber/explanation/version` است. هر گزینه `number/text/voteCount/isSelected` دارد؛ totalVoters تعداد افراد است، نه مجموع انتخاب‌های MultiChoice. enum وضعیت در HTTP و OpenAPI رشته‌ای Open/Closed است. loading تعریف با receipt یکی نیست؛ خطا یا 404 مجوز ساخت Poll ساختگی در frontend نیست. Empty Poll شمارش صفر و ownVoteVersion=null دارد.

Anonymous هویت رأی‌دهنده را در API عمومی، رویداد و voters پنهان می‌کند؛ حتی creator endpoint voters ندارد. Anonymous به معنی حذف UserId از SQL یا ناشناس‌سازی رمزنگاری‌شده در برابر اپراتور مجاز دیتابیس نیست. NonAnonymous فقط به عضو فعلیِ مجاز، با رعایت محدودهٔ history/clear/DeleteForMe، هویت کمینهٔ `SafeMessagingIdentityResult` و انتخاب‌ها را صفحه‌بندی می‌دهد؛ شمارهٔ تلفن یا کلید Quiz در این پاسخ نیست. حداکثر pageSize=100؛ optionNumber نیز selector است، نه مجوز.

ثبت رأی اول expectedVoteVersion=null می‌خواهد. تغییر رأی Poll نسخهٔ `ownVoteVersion` آخرین پاسخ را می‌خواهد؛ بدون آن یا با نسخهٔ کهنه 412 می‌گیرد. گزینه‌ها یکتا و معتبر و SingleChoice فقط یکی است. پاسخ Quiz برای هر User نهایی است؛ عوض‌کردن آن 409 است. retry همان انتخاب مرتب‌شده اثر تازه ندارد و همان نسخه را می‌دهد؛ حتی بعد از Close فقط بازیابی همان اثر قبلی است، نه رأی جدید/تغییر. SQL منبع نهایی است و دو دستگاه یک User نمی‌توانند رأی والد تکراری بسازند.

CorrectOption و Explanation فقط بعد از پاسخ نهایی **خود بیننده** قابل خواندن‌اند؛ پاسخ دیگران یا بسته‌شدن Quiz آن‌ها را برای فرد پاسخ‌نداده آشکار نمی‌کند. creator نیز در GET عمومیِ بدون رأی کلید را دریافت نمی‌کند؛ ورودی اولیهٔ خودش به معنای exception قراردادی در API نیست. Quiz هیچ ExamAttempt/Grade/Assessment آموزشی ایجاد نمی‌کند.

Close توسط creator با مجوز جاری ارسال، یا Owner/Admin با `DeleteMessages` جاری انجام می‌شود؛ subscriber کانال حق رأی دارد ولی حق انتشار یا بستن Poll دیگران ندارد. Close، `version` خود Poll را می‌گیرد، نه نسخهٔ Message یا OwnVote. هر رأی مؤثر نسخهٔ Poll را عوض می‌کند: اگر Vote برنده شود Close کهنه 412، و اگر Close برنده شود رأی تازه 409 است. retry Close پس از reauthorization نسخهٔ Closed موجود را می‌دهد. Reopen، ویرایش تعریف منتشرشده، حذف رأی، auto-close و زمان‌بندی Poll در P2 API ندارند؛ ویرایش پیام Poll با خطای پایدار قبلی MessageNotEditable/409 رد می‌شود.

Forward طبق تأیید نهایی مالک، یک **Message متنی مستقل** شامل پرسش و گزینه‌های شماره‌دار است؛ بدون رأی، CorrectOption و Explanation و بدون Poll زنده در مقصد. scope، metadata منبع، حق خواندن منبع، مجوز ارسال مقصد، Topic مقصد و Safety همان Forward قبلی را دارند. حذف یا بستن Poll مبدأ، کپی مستقل مقصد را mutate نمی‌کند. گزارش abuse شاهد همان پرسش/گزینه‌ها را در پروندهٔ scoped موجود نگه می‌دارد؛ مرور عمومی private chat یا دسترسی جدید School Manager/Guardian ساخته نشده است.

رویداد durable جدید `PollChanged=14` روی همان `ConversationChanged`، فقط invalidation شامل شناسه/نسخه/cursor است؛ payload رأی/کلید/متن و ActorUserId رأی‌دهنده ندارد. client پس از آن یا reconnect از Delta مجاز و GET جزئیات همگرا می‌شود. read تاریخی با حق تعامل جدید فرق دارد: Vote/Close/Publish وضعیت جاری account/session/role/member/Block/GAP-05/restriction را دوباره می‌سنجند. تاریخچه با پایان رابطه خودکار حذف نمی‌شود؛ PollChanged برای Message پنهان/پاک‌شده/بیرون FromJoin ارسال نمی‌شود. رأی، پیام تازه یا notification/unread تازه ایجاد نمی‌کند.

خطاها: 400=`CONVERSATION_POLL_INVALID`؛ 404=`CONVERSATION_POLL_NOT_FOUND` یا خطای concealment گفتگو؛ 409=`CONVERSATION_POLL_CLOSED`، `CONVERSATION_POLL_VOTE_IMMUTABLE` یا conflict تعریف ClientMessageId؛ 412=`CONVERSATION_POLL_CONFLICT` یا `CONVERSATION_POLL_VOTE_CONFLICT`. مجوز نامعتبر همان 403/404 موجود و session لغوشده 401 است؛ Content Safety مسدود/review برابر 422 و unavailable برای Minor/Unknown برابر 503 است. client نباید retry خطای نسخه را به overwrite خاموش تبدیل کند.

تمام routeهای این controller از `messaging-send` موجود، ۳۰۰ درخواست در دقیقه به‌ازای partition احراز‌شده در محیط معمول استفاده می‌کنند؛ discovery و Search هرکدام ۶۰ در دقیقه‌اند. budget Hub و cap اتصال/Presence جداست. این محدودیت‌ها تک‌process هستند، نه distributed enforcement یا benchmark Production. هیچ تنظیم اجباری appsettings یا سرویس خارجی تازه لازم نیست.

Migration `20261016211112_AddConversationPollsAndQuiz` پنج جدول Polls/Options/Votes/Selections/QuizAnswers، rowversion والد/رأی، PK/FK مرکب، unique هر User/Poll، CHECK وضعیت/گزینه/نوع و index صفحه دارد؛ پاسخ صحیح از projection عمومی جداست. ایجاد همهٔ گزینه‌ها/کلید و پیام/Revision/Notification/Outbox یک transaction است. SQL FK/unique اتصال و عدم duplicate را enforce می‌کند؛ تک‌گزینه‌ای بودن و immutable بودن پاسخ از قواعد Domain و transaction برنامه می‌آیند، نه ادعای CHECK چندسطره. Down پیش از حذف ساختار، در حضور رأی‌گیری/رویداد/شواهد جدید ایمن توقف می‌کند. فقط دیتابیس‌های تست ایزوله migrate می‌شوند؛ هیچ migration روی سرور یا دیتابیس عادی اجرا نشده است.

## Checklist v1 — قرارداد مصوب ۲۰۲۶-۱۰-۱۷

تصمیم مستقل مالک، تعویق قبلی `P2_CHECKLIST_REQUIRES_PRODUCT_DECISION` را رفع کرده است. نوع واقعی `MessageKind.Checklist=8` مستقل از Poll/Quiz و Assessment است. عنوان در Message و تعریف/آیتم/وضعیت در سه جدول Messenger قرار دارند؛ پاسخ history/search فقط عنوان را دارد و جزئیات از GET مجاز خوانده می‌شود. گیت نهایی تست/Git در ممیزی ثبت می‌شود؛ وجود این قرارداد ادعای deploy نیست.

### Endpoint و DTO

همهٔ مسیرها Bearer و session جاری، بدنهٔ حداکثر ۶۴ KiB و rate policy موجود `messaging-send` دارند. prefix زیر `/api/v1/conversations/{conversationId}` است.

| Method / مسیر | Request | Response |
|---|---|---|
| POST `/checklists` | `CreateConversationChecklistCommand`: clientMessageId، title، mode، items، topicId اختیاری، isSilent پیش‌فرض false | `MessageReceiptResult` موجود؛ نسخهٔ Message، نه نسخهٔ تعریف |
| GET `/messages/{messageId}/checklist` | بدون body | `ConversationChecklistResult` |
| PUT همان مسیر | `UpdateConversationChecklistCommand`: title، mode، کل items فعال به ترتیب، expectedVersion تعریف | `ConversationChecklistResult` |
| PUT `/messages/{messageId}/checklist/items/{itemId}` | `SetChecklistItemCommand`: isChecked صریح، expectedDefinitionVersion، expectedStateVersion اختیاری | `ConversationChecklistResult` |

`ChecklistItemInput={itemId,text}`؛ Guid پایدار، غیرخالی و یکتای هر آیتم را کلاینت تعیین می‌کند و retry ساخت همان شناسه‌ها را می‌فرستد. mode باید صریح `Personal` یا `Collaborative` باشد؛ null، string یا عدد ناشناخته رد می‌شود. enum در runtime و OpenAPI رشته‌ای است؛ lifecycle/status enum مصنوعی ندارد. سقف‌های فنی این v1: عنوان ۳۰۰ نویسه، ۱ تا ۲۰ آیتم، متن هر آیتم ۱۵۰، بدون control character و با Trim. این‌ها safeguard بدنه/SQL هستند، نه محدودیت تجاری PRD. متن تکراری با شناسهٔ متفاوت مجاز است.

```json
{
  "clientMessageId": "a5d978e2-8652-4321-a9ce-ab36eb600001",
  "title": "وسایل فردا",
  "mode": "Personal",
  "items": [
    { "itemId": "a5d978e2-8652-4321-a9ce-ab36eb600002", "text": "دفتر" },
    { "itemId": "a5d978e2-8652-4321-a9ce-ab36eb600003", "text": "کتاب" }
  ],
  "topicId": null,
  "isSilent": false
}
```

Result شامل `messageId/conversationId/title/mode/items/isCompleted/version/messageVersion` است. هر آیتم فقط `itemId/text/isChecked/stateVersion` دارد. **version** فقط نسخهٔ تعریف است؛ **messageVersion** برای actionهای عادی پیام مانند حذف؛ **stateVersion** نسخهٔ تیک همان آیتم است. این سه را جای هم نفرستید. هیچ `UserId` ورودی برای انتخاب مالک state وجود ندارد؛ extra UserId نیز authority ایجاد نمی‌کند.

### مالکیت، هم‌زمانی و retry

Personal: هر User وضعیت مستقل دارد؛ GET سازنده/Admin نیز فقط وضعیت خودش را می‌خواند. پیش از اولین تغییر، false و stateVersion=null است. ردیف پس از uncheck نگه داشته می‌شود تا null قدیمی نتواند تغییر دستگاه دیگر را overwrite کند. دو session همان User state یکسان را بازیابی می‌کنند؛ B وضعیت یا شمار تیک A را نمی‌بیند. Collaborative: هر آیتم یک وضعیت مشترک دارد و stateVersion از ابتدا موجود است؛ identity/time فرد تیک‌زن در DTO قرار ندارد. `isCompleted` از همهٔ آیتم‌های فعال و برای Personal فقط همان بیننده مشتق می‌شود؛ ستون Completed، Grade/Score/Exam/Submission ساخته نشده است.

Mutation ابتدا session، history، عضویت/role، محدودیت حساب/عضو، Block در Direct و رابطهٔ جاری GAP-05 را بررسی می‌کند؛ سپس expectedDefinitionVersion باید جاری باشد. equivalent check/uncheck با تعریف جاری، بدون رویداد یا تغییر نسخهٔ تازه برمی‌گردد، حتی اگر expectedStateVersion قدیمی باشد؛ این retry اثر جدید نیست. تغییر واقعی، نسخهٔ جاری state را می‌خواهد: Personal اولیه null، پس از آن مقدار پاسخ؛ Collaborative مقدار آیتم. درخواست staleِ متضاد 412 است. پس از تغییر تعریف نیز GET و تصمیم کاربر لازم است، نه overwrite خودکار.

تمام read/mutate/definition در transaction کوتاه Serializable و با قفل Conversation انجام می‌شوند. SQL PK/FK/rowversion مرجع‌اند؛ قفل حافظه‌ای وجود ندارد. رقابت بازنشستگی/تیک: اگر تیک اول commit شود state قدیمی با آیتم بازنشسته حفظ می‌شود؛ اگر تعریف اول commit شود، mutation با نسخهٔ تعریف قبلی 412 می‌گیرد. هیچ state به آیتم جایگزین نسبت داده نمی‌شود.

### مجوز و ویرایش تعریف

ساخت و ویرایش تعریف، مجوز جاری **ارسال متن** می‌خواهند؛ subscriber کانال می‌تواند تیک بزند، ولی بدون PublishMessages نمی‌تواند بسازد/تعریف را ویرایش کند. تعریف را Creator در پنجرهٔ یک‌ساعتهٔ مصوب Message یا Owner/Admin جاریِ گروه/کانال UserManaged با `DeleteMessages` می‌تواند تغییر دهد؛ برای کانال PublishMessages نیز لازم است. مالکِ فرستنده استثنای دورزدن پنجره ندارد. School Manager یا Guardian اختیار تازه‌ای نمی‌گیرند. تغییر تعریف Audit امن دارد؛ مجوز و نشست بعد از Content Safety دوباره از SQL خوانده می‌شوند، نه tracking قدیمی EF.

برای حفظ معنای تاریخی و جلوگیری از افشای تعامل شخصی از طریق نتیجهٔ ویرایش، **Mode از زمان انتشار ثابت است و متن هر ItemId نیز همیشه ثابت است**؛ این محافظ فقط بعد از مشاهدهٔ تیک دیگران فعال نمی‌شود. تغییر معنا: آیتم قدیمی را از لیست فعال حذف و آیتم تازه با Guid تازه اضافه کنید. افزودن، ترتیب جدید و عنوان جدید با expectedVersion مجازند؛ حذف، soft-retire است، نه حذف state. شناسهٔ بازنشسته دوباره فعال نمی‌شود. MessageRevision عنوان قبلی و ردیف آیتم متن/نسخهٔ ایجاد و بازنشستگی را نگه می‌دارند؛ API عادی تاریخچهٔ خصوصی یا آیتم بازنشسته را نمایش نمی‌دهد. برای Mode متفاوت Checklist مستقل بسازید. generic Edit پیام Checklist عمداً 409 می‌گیرد.

### Realtime، ارتباط با پیام و خطاها

`ChecklistChanged=15` روی event موجود `ConversationChanged` فقط messageId/resourceVersion/cursor و metadata کمینهٔ sync دارد؛ خود وضعیت/متن/actor تیک در payload نیست. تغییر Personal با `AudienceUserId` خود User و actor=null ثبت می‌شود؛ resolver حتی با audience hint عمومی نیز از Delta خصوصی عبور می‌کند. تغییر Collaborative و تعریف به اعضای فعلیِ مجاز می‌رسد. revoked session، FromJoin، Clear و DeleteForMe/Everyone در replay هم معتبرند. بعد از hint یا reconnect، Delta و GET را مصرف کنید؛ mutation تازهٔ Hub ساخته نشده است. تیک Notification، unread یا Message تازه نمی‌سازد؛ create همان Notification و Silent موجود را reuse می‌کند.

Forward فقط یک Message **Text مستقل** شامل عنوان و متن شماره‌دار آیتم‌های فعال است؛ بدون checked state، actor/time، ItemId، version، history یا Checklist زنده. ارجاع امن مبدأ همان قواعد visibility پیام موجود را دارد. DeleteForMe شخصی، DeleteForEveryone با مجوز/نسخه/پنجرهٔ فعلی، و Reply unavailable بدون متن آیتم/state هستند. Search فعلی روی **عنوان** و kind=Checklist است؛ index جدید متن آیتم ساخته نشده. گزارش abuse فقط snapshot تعریف را به پروندهٔ موجود می‌دهد؛ MFA، audit و case scope reviewer تغییر نکرده‌اند. همهٔ عنوان/متن آیتم در create/update برای audience محافظت‌شده از Internal Makan Safety موجود می‌گذرند؛ provider بیرون transaction، بازبینی نهایی داخل آن، شکست unavailable برابر fail-closed است.

خطاها: 400=`CONVERSATION_CHECKLIST_INVALID`؛ 404=`CONVERSATION_CHECKLIST_NOT_FOUND` یا concealment گفتگو؛ 412=`CONVERSATION_CHECKLIST_CONFLICT` برای تعریف و `CONVERSATION_CHECKLIST_STATE_CONFLICT` برای state؛ 409=`CONVERSATION_CHECKLIST_STRUCTURE_IMMUTABLE` برای Mode/معنای شناسه/بازفعال‌سازی یا idempotency conflict ساخت. مجوز 403/404، revoked=401، پنجرهٔ ویرایش همان کد قبلی، Safety blocked/review=422 و unavailable=503 است. کاربر loading/error/empty خود را از پاسخ واقعی بسازد؛ ساخت Checklist خالی یا completed ساختگی مجاز نیست.

Migration `20261017033804_AddConversationChecklists` مستقل است؛ startup migration یا appsettings/provider تازه ندارد. ارتقای نسخهٔ سرور باید طبق workflow migration موجود و پس از backup/approval انجام شود؛ این تحویل فقط LocalDB ایزوله را آزمایش کرده است. دریافت واقعی Web Push/FCM همچنان `P1_PUSH_RUNTIME_PENDING` و presence چندگرهی `PENDING_SCALE_INFRA` است.
