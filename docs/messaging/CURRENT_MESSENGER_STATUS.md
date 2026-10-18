# وضعیت جاری Messenger: P0، P1، P2 و مرز P3

این بخش وضعیت کد ثبت‌شده تا `main@ef67624` در `2026-10-18` است. جدول‌های MISSING ابتدای ممیزی P2 و تعویق اولیه Checklist تاریخچهٔ شروع همان مرحله‌اند؛ وضعیت نهایی از [تحویل P2](MESSENGER_P2_FRONTEND_HANDOFF.md) و [ممیزی نهایی](MESSENGER_P2_DELIVERY_AUDIT.md) خوانده شود. پذیرش محلی Backend با deployment یا دریافت Push روی دستگاه یکی نیست.

| حوزه | وضعیت جاری و مرجع |
|---|---|
| سن و شروع ارتباط | تأیید اولیهٔ رسمی سن، وضعیت امن self و Direct دو حساب Adult طبق GAP-05 در P0 موجودند؛ OTP/DOB خوداظهاری اثبات سن نیست |
| Peer Read | read-state مجاز Direct در P1؛ Group/Channel رسید per-member ندارند و Hidden علت خصوصی را افشا نمی‌کند |
| Web Push / FCM | transport واقعی و رجیستری خصوصی دستگاه در P1؛ `P1_PUSH_RUNTIME_PENDING` باقی است؛ SMS عمومی اعلان provider واقعی ندارد |
| Typing / Presence | lease و TTL واقعی با subscription و privacy جاری در P2؛ state گذرا در حافظه و تک‌instance، نه تاریخچه دائمی یا توزیع‌شده |
| Folder / Saved Labels | سازمان‌دهی خصوصی کاربر و قرارداد نسخه‌دار در P2؛ انتخاب پوشه/برچسب اجازه خواندن تازه نمی‌سازد |
| Invite / Join / Restriction | لینک دعوت، درخواست عضویت و محدودیت عضو طبق نقش و current policy در P2؛ token دعوت credential محرمانه است |
| Topics | lifecycle موضوع و topicId پیام در P2؛ موضوع حذف‌شده یا خارج scope انتخاب مجاز نیست |
| Scheduled / Silent | قصد ارسال پایدار، لغو و worker با بازبینی مجوز و safety هنگام dispatch؛ Scheduled به معنی Message/Sent نیست؛ Silent همان pipeline پیام با سیاست اعلان خودش است |
| Poll / Quiz | MessageKind.Poll و endpointهای مستقل Messenger؛ anonymous/multiple-choice و quiz یک‌بار پاسخ طبق قرارداد؛ کلید quiz قبل موعد انتشار عمومی افشا نمی‌شود |
| Checklist | MessageKind.Checklist=8؛ حالت Collaborative و Personal مستقل از Poll و Assessment؛ تعریف مشترک و وضعیت شخصی قرارداد جدا دارند |
| Search | فیلترهای hasAttachment/hasLinks/topicId و current authorization قبل paging؛ false نیز فیلتر است؛ link search واکشی URL نیست |
| Voice / Video / Live | policy رسانه مصوب است، ولی `P3_S_INFRA_REQUIRED`؛ LiveKit/TURN واقعی اجرا یا پذیرفته نشده‌اند؛ endpoint یا SDK تماس محصول از این مصوبه نتیجه نمی‌شود |

### جریان‌های گذرا و پیام‌های پایدار

Hub `/hubs/messaging` اکنون شش invoke عمومی دارد: `SubscribeConversation`، `UnsubscribeConversation`، `SetTyping`، `Heartbeat`، `SubscribePresence` و `UnsubscribePresence`. قراردادهای client شامل `ConversationChanged`، `TypingChanged` و `PresenceChanged` است. `ConversationChanged` برای تغییر پایدار به read/Delta هدایت می‌کند؛ دو event دیگر فقط hint دارای expiry هستند. ارتباط Direct مجاز و privacy دوطرفه شرط Presence است؛ `Hidden` با timestamp خالی وضعیت private مقصد را افشا نمی‌کند.

پیش‌فرض‌های فعلی `MessagingEphemeralOptions`: typing TTL شش ثانیه، refresh سه ثانیه، heartbeat ده ثانیه، connection lease چهل‌وپنج ثانیه، Recently پنج دقیقه و نگهداری Last Seen یک روز. budget و سقف اتصال/subscription از همان options اعتبارسنجی می‌شوند. reconnect همهٔ subscriptionهای قبلی را برای اتصال جدید معتبر نمی‌کند؛ ثبت دوباره و بازیابی Delta لازم است. Group عضویت دائمی اجازهٔ send/read نمی‌سازد؛ نقش، account/session، Block، رابطه و visibility دوباره بررسی می‌شوند.

### پیام زمان‌بندی‌شده، Quiz و Checklist

ایجاد scheduled intent از `POST /api/v1/conversations/{id}/scheduled-messages` انجام می‌شود. حالت‌ها `Scheduled/Dispatched/Cancelled/Failed` هستند؛ `Dispatched` بعد از commit پیام واقعی و Outbox حاصل می‌شود. `clientMessageId` و payload/زمان یکسان همان intent را resolve می‌کنند؛ شناسهٔ قبلی با درخواست متفاوت conflict می‌دهد. ارسال فوری، رزرو شناسهٔ intent را دور نمی‌زند. logout به‌تنهایی لغو قصد پایدار نیست؛ مجوز mutation نشست فوراً سلب و مجوز حساب/رابطه در dispatch بازبینی می‌شود.

Poll/Quiz و Checklist APIهای Messenger و داده‌های همان scope هستند؛ از endpointها و tableهای Assessment برای آن‌ها استفاده نشود. history/search نمای خلاصه پیام را می‌دهند؛ جزئیات تعامل از endpoint مجاز خودش می‌آید. `expectedVersion` تعریف یا وضعیت رأی/تکمیل باید از response واقعی گرفته شود؛ retry نسخه قدیمی مجوز overwrite نیست. تغییر رأی/تکمیل کار، ارسال پیام یا unread/Push تازهٔ عمومی محسوب نمی‌شود مگر قرارداد همان event آن را صریح تعیین کند.

### مرز رسانه و زیرساخت

[policy رسانه P3](MESSENGER_P3_APPROVED_MEDIA_POLICY_V1.md) و [گزارش نیاز زیرساخت S](MESSENGER_P3_S_MEDIA_SECURITY_SPIKE_REPORT.md) مرجع‌اند. وضعیت جاری `LIVEKIT_REJOIN_SECURITY_GATE=NOT_TESTED` و `P3_PRODUCTION_MEDIA_APPROVAL=NOT_GRANTED` است. نصب CLI، موفقیت regression یا تأیید محصول، اثبات revocation روی SFU یا عبور TURN نیست. نیاز محیط S شامل Linux lab مجاز غیرProduction، Docker runtime، DNS/TLS، TURN احرازشده، دسترسی شبکه و دو client synthetic است؛ این به‌روزرسانی deployment، بازکردن پورت یا تغییر firewall انجام نمی‌دهد.

### مسیر مطالعه کد جدید

`MessagingEphemeralService → IMessagingEphemeralState/InMemoryMessagingEphemeralState` برای lease و privacy؛ `ScheduledMessageService/Processor → EfMessagingStore.Scheduling/ScheduledDispatch` برای intent و ارسال؛ `ConversationPollService` و `ConversationChecklistService` برای تعامل؛ `NotificationPushDependencyInjection`، `WebPushNotificationProvider` و `FcmPushNotificationProvider` برای transport. نام/فیلد/route دقیق هر HTTP در مرجع تولیدشده و قواعد کامل در Handoffهای [P0](MESSENGER_P0_FRONTEND_HANDOFF.md)، [P1](MESSENGER_P1_FRONTEND_HANDOFF.md) و [P2](MESSENGER_P2_FRONTEND_HANDOFF.md) آمده است.


[مرجع کامل HTTP و DTO](../backend-api-reference.md) — [راهنمای بک‌اند](../backend-api-guide.md) — [آمادگی Production](../production/PRODUCTION_READINESS.md).
