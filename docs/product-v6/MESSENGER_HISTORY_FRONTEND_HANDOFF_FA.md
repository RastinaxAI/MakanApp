# قرارداد تاریخچهٔ Messenger — MKN-BE-001

بازبینی `2026-11-17` از `b76e960` روی `feature/product-v6-backend-alignment`.
Source اصلاح‌شده و اعتبارسنجی‌شده: `718451ff3eba1831bd6d18c1e9693713a415d817`؛3451 تست موفق.
این سند از Backend و تست HTTP/SQL استخراج شده است؛ Frontend بررسی یا تغییر نکرده است.

## مسیر اصلی Home

`GET /api/v1/conversations/account?filter=All&limit=30&cursor=<opaque>`

این صندوق حساب است؛ گفتگوی شخصی و گفتگوی سازمان‌های مختلفِ دارای مجوز جاری را یکجا برمی‌گرداند.
پاسخ `items/nextCursor/hasMore` است؛ آیتم‌ها با `conversationId` در State به‌روزرسانی شوند.
فیلتر/Search/Limit در سرور اعمال می‌شوند. `filter=Archived` نمای آرشیو است؛ All آرشیوشده‌ها را کنار می‌گذارد.
ترتیب سراسری Pinned، آخرین فعالیت قابل مشاهده و Guid مطابق قرارداد SQL فعلی است.

مسیر قدیمی `GET /api/v1/conversations` آرایه و paging قبلی را حفظ می‌کند؛ پیش‌فرض از Workspace
انتخاب‌شده می‌آید. `scope=Personal` نمای شخصی صریح و `scope=Organization` بافت سازمانی معتبر می‌خواهد.
این مسیر جای Home یکپارچه نیست. برای ساخت صندوق حساب، فهرست‌های scoped را در کلاینت ادغام نکنید.

## بازکردن گفتگو

| Method / مسیر | مصرف |
|---|---|
| GET /api/v1/conversations/{id}/summary | عنوان/هویت امن، Preview، Unread، قابلیت‌ها و نشان سازمان |
| GET /api/v1/conversations/{id}/messages | صفحهٔ پیام‌های ماندگار و مجاز |
| GET /api/v1/conversations/{id}/changes | Delta همان گفتگو با afterCursor/limit موجود |
| GET /api/v1/realtime/account-inbox/changes | invalidation پایدار صندوق، بدون متن پیام |

نشست معتبر لازم است. برای History/Delta، سازمان و عضویت از خود گفتگو و مجوز جاری حساب استخراج
می‌شوند؛ انتخاب School Workspace دیگر، مجوز خواندنِ معتبر را پنهان نمی‌کند. Workspace نشست mutate نمی‌شود.
پایان Membership/Role/TeacherAssignment/Enrollment، حذف Participant یا غیرفعال‌شدن منبع رسمی،
خواندن آینده را با همان convention مخفی‌سازی404 می‌بندد؛ Cursor/Notification/شناسهٔ قدیمی مجوز نیست.

این اصلاح مجوز Send/Edit/Delete/مدیریت، Download یا SubscribeConversation را تغییر نمی‌دهد.
`GET /api/v1/conversations/{id}` جزئیات مدیریتی قدیمی همچنان قرارداد scoped خودش را دارد؛ شکست آن را
با شکست Summary/History یکی نکنید و برای خواندن History، موفقیت آن را پیش‌شرط نگذارید.
Read/Delivered cursor نیز عملیات مستقل موجود است؛ GET history به‌تنهایی آن‌ها را جلو نمی‌برد.

## Scroll-up و صفحه‌بندی

1. صفحهٔ تازه: `GET /api/v1/conversations/{id}/messages?limit=20`، بدون beforeSequence.
2. پاسخ `messages` درون هر صفحه به ترتیب صعودی Sequence است؛ صفحهٔ اول تازه‌ترین پیام‌ها را دارد.
3. برای قدیمی‌ترها، `beforeSequence` را دقیقاً برابر `nextBeforeSequence` پاسخ بگذارید.
4. وقتی `nextBeforeSequence=null` شد، History قدیمی‌ترِ مجاز تمام است.
5. صفحات قدیمی‌تر را قبل از صفحات تازه‌تر قرار دهید و با MessageId ادغام کنید؛ timestamp کلید ترتیب نیست.

Limit پیش‌فرض50 و بازهٔ فعلی1..100 است؛ beforeSequence در صورت وجود باید مثبت باشد.
مرز انحصاری است: `Sequence < beforeSequence`. مقدار را از تعداد پیام‌ها یا زمان دستگاه حدس نزنید.
پیام تازه در حین Scroll-up، مرز صفحهٔ قدیمی را جابه‌جا نمی‌کند؛ برای تازه‌ها Snapshot/Delta را بازیابی کنید.

نمونهٔ همان دادهٔ75پیامی در تست پذیرش با limit=20:

| beforeSequence درخواست | Sequenceهای پاسخ، صعودی | nextBeforeSequence |
|---|---|---|
| ارسال نشود | 56..75 | 56 |
| 56 | 36..55 | 36 |
| 36 | 16..35 | 16 |
| 16 | 1..15 | null |

## Visibility و حذف

`HistoryVisibleFromSequence` عضو و `HistoryHiddenThroughSequence` کاربر رعایت می‌شوند.
Delete-for-me پیام را برای همان کاربر از History و Preview کنار می‌گذارد؛ paging آن را برنمی‌گرداند.
Clear-history حذف عمومی SQL نیست؛ مرز شخصی را جلو می‌برد و پیام تازه پس از آن قابل مشاهده است.
Delete-for-everyone طبق قرارداد موجود tombstone با `isDeleted=true` و محتوای پاک‌شده می‌دهد؛
متن حذف‌شده از Cache قدیمی دوباره نمایش داده نشود. مجوز rejoin همان policy موجود است.
Block طبق سیاست جاری ارسال جدید را محدود می‌کند؛ History مجاز گذشته را خودکار حذف نمی‌کند.

## Refresh، ورود دوباره و دستگاه دوم

Cache محلی فقط بهینه‌سازی است. پس از Refresh/Login یا شروع دستگاه دوم، Inbox را با Cursor=null
و History را بدون beforeSequence بگیرید. محتوای SQL به User/Conversation تعلق دارد، نه Session.
Cursor فهرست حساب حداکثر24 ساعت و به User/Session/Filter/Search/Limit بسته است؛ انتقال آن به
دستگاه دوم400 MESSAGE_PAGE_INVALID می‌دهد. این خطا به معنی خالی‌بودن حساب نیست.

## Realtime و بازیابی

Hub موجود `/hubs/messaging` و `ClientInvalidated` با `key=messaging.account-inbox` حفظ شده‌اند.
این event و account-inbox replay فقط دستور Refetch هستند؛ `Conversation=null` در این feed طبیعی است.
برای محتوای پیام از History و Delta همان گفتگو استفاده کنید؛ full history را از invalidation نسازید.

Bootstrap موجود Replay را پیش از Snapshot بگیرید، Inbox/History را بارگذاری کنید، سپس از همان
مرز catch-up کنید تا فاصلهٔ Snapshot و اتصال پوشش داده شود. در Reconnect، Cursor معتبر قبلی
را برای همان نشست/View ادامه دهید. `hasMore/retryAfterMilliseconds` را رعایت کنید؛ Cursor را
فقط پس از اعمال موفق صفحه پیش ببرید. `REALTIME_RESYNC_REQUIRED` به bootstrap تازه نیاز دارد.

تحویل را exactly-once فرض نکنید. پیام با MessageId، ترتیب با Sequence و رویداد با EventId
ادغام می‌شود. Retry همان ClientMessageId نباید پیام دوم، Conversation دوم یا افزایش دوبارهٔ Unread بسازد.
Realtime می‌تواند قطع باشد و History همچنان از SQL قابل خواندن بماند.

## Empty و Error

| پاسخ | رفتار کلاینت |
|---|---|
| 200 با items/messages خالی | حالت خالی واقعی در همین فیلتر/مرز مجاز |
| 400 MESSAGE_PAGE_INVALID / CHANGE_CURSOR_INVALID | ورودی/نشانگر را اصلاح و مسیر مربوط را از ابتدا دریافت کنید |
| 401 | نشست معتبر لازم است؛ پیام «بدون گفتگو» نمایش ندهید |
| 404 CONVERSATION_NOT_FOUND | شناسه یا مجوز جاری قابل دسترسی نیست؛ State قدیمی مجوز ایجاد نمی‌کند |
| Realtime resync / موقتاً unavailable | Snapshot/Replay کنترل‌شده؛ Cursor قبلی و دادهٔ معتبر حفظ شوند |
| 5xx یا timeout شبکه | حالت Error/Retry؛ محتوای قبلی را به‌عنوان History خالی بازنویسی نکنید |

Backend در این Task خطای سرور/دیتابیس را به آرایهٔ خالی تبدیل نکرده است. نگاشت UI واقعی
در مخزن Frontend آزموده نشده؛ از این سند، وجود قطعی bug در Frontend استنتاج نشود.

مرجع پذیرش و محدودیت‌ها: [گزارش MKN-BE-001](MESSENGER_HISTORY_FIX_REPORT_FA.md).
