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
