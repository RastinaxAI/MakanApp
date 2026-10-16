# قرارداد مصرف Messenger P1

این سند فقط قرارداد Backend است؛ هیچ frontend یا service worker در این worktree ساخته/ویرایش نمی‌شود. افزوده‌های هر فاز پس از validation ثبت می‌شوند. مبنای قبلی [Handoff P0](MESSENGER_P0_FRONTEND_HANDOFF.md) و مرجع JSON، OpenAPI runtime همین build است.

## Peer Read و یک/دو تیک

- HTTP receipt پیام با `status=Sent` فقط commit سرور را ثابت می‌کند؛ می‌توان آن را server-accepted/یک تیک نمایش داد. Delivered یا دریافت SignalR به معنی خواندن نیست.
- `POST /api/v1/conversations/{id}/read` با `upToMessageSequence` فقط پس از اقدام معنادار کاربر اجرا شود؛ بازکردن background socket شاهد خواندن نیست. cursor واقعی هرگز عقب نمی‌رود؛ read خود فرستنده peer را read نکرده است.
- `GET /api/v1/conversations/{id}/read-states` با Bearer معتبر، `conversationId`، `peerReadThroughSequence`، `visibility` و `version` می‌دهد. برای Direct، `Visible` و sequence برابر آخرین پیام خود caller با read واقعی peer است؛ پیش از آن ۰. فقط پیام خود caller با sequence کمتر/برابر این مقدار و در تاریخچهٔ مجاز می‌تواند read/دو تیک باشد.
- `Hidden` یعنی sequence/version=null؛ علت Block، Privacy، سن/رابطه یا وضعیت peer را حدس نزنید و تیک read تازه نسازید. privacy دوطرفه Personal، active account/participant و eligibility جاری لازم‌اند. این وضعیت مجوز حذف history محلی یا افشای علت خصوصی نیست.
- Group، Channel و Saved پاسخ `NotSupported` با sequence/version خالی دارند؛ receipt همه اعضا یا شمار view کانال وجود ندارد.
- event همان `ConversationChanged` است با `type:"PeerReadStateChanged"` و `peerReadThroughSequence`. event به فرستندهٔ مجاز می‌رسد، نه تمام اعضا؛ `ReadCursorAdvanced` خصوصی همچنان فقط برای خود reader است. Delta نیز field nullable مذکور را دارد.
- پس از reconnect، snapshot read-state را تازه و Delta را از checkpoint پردازش‌شده دریافت کنید؛ eventها می‌توانند تکرار/تأخیر داشته باشند. sequence دریافت‌شدهٔ کمتر، state جدیدتر پردازش‌شده را عقب نبرد؛ در Hidden مجوز نمایش تازه را فرض نکنید. checkpoint فقط پس از پردازش کامل صفحه جلو می‌رود.
- receipt version، opaque navigation/fact version است؛ آن را ExpectedVersion برای POST read استفاده نکنید. قرارداد قدیمی read additive و بدون ExpectedVersion باقی است. ExpectedVersion مربوط به Edit/Settings قرارداد مستقل خودش را دارد.

نمونهٔ پاسخ Direct پیش از peer read:

```json
{"conversationId":"<guid>","peerReadThroughSequence":0,"visibility":"Visible","version":"<opaque>"}
```

خطاها: 401 نشست نامعتبر؛ 404 منبع ناموجود/خارج مجوز؛ POST read با sequence نامعتبر 400. hidden و unsupported پاسخ موفق اما فاقد رسید هستند. download/search/history/Delta همچنان مجوز جاری را مستقل بررسی می‌کنند؛ دانستن MessageId یا receipt اجازهٔ فایل نمی‌دهد.

## اعلان، Mute و تلاش مجدد

اعلان داخلی و Push یک state نیستند. ترجیحات از API موجود `/api/v1/notification-preferences` و category `Messages` مصرف می‌شوند؛ تنظیم شخصی و هر سازمان مستقل‌اند، In-app خاموش نمی‌شود. Quiet Hours با timezone صریح، ارسال خارجی را عقب می‌اندازد. category مؤثر Mention/Reply از pipeline موجود است؛ هیچ‌کدام Mute را خودکار دور نمی‌زنند.

Mute دائم/Until، پیام و unread و SignalR را قطع نمی‌کند؛ worker پیش از هر تلاش، Mute/Preferences و مجوز جاری منبع/دستگاه را دوباره می‌سنجد. تغییر آن‌ها پس از زمان‌بندی نیز مؤثر است. preview همه حساب‌ها، از جمله Minor/Unknown، عمومی و بدون متن پیام، شماره، نام فایل، سن، رابطه والد یا دلیل safety است.

`NotificationId` هویت منطقی پایدار است؛ retry پیام با همان ClientMessageId/payload اعلان داخلی دوم نمی‌سازد. شبکه دقیقاً یک‌بار نیست و client باید اعلان تکراری با همان شناسه/tag را تحمل کند. enum قدیمی Delivered در delivery داخلی به معنای پذیرش provider است؛ آن را تیک خواندن پیام نکنید. receipt پیام از SQL و peer-read فقط از قرارداد بخش قبل می‌آیند.

خطای موقت provider با backoff و سقف تلاش دوباره پردازش می‌شود. Retry-After رعایت می‌شود؛ invalid destination لغو می‌شود و retry بی‌نهایت ندارد. failure، اصل Message/Notification را حذف نمی‌کند. لغو session یا دسترسی، ارسال آینده را می‌بندد؛ اطلاعاتی که قبلاً به دستگاه/شبکه تحویل شده قابل پس‌گرفتن تضمینی نیست.
