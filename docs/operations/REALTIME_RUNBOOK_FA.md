# راهنمای عملیات Realtime جاری

این سند دستور اجرای خودکار روی IIS یا دیتابیس‌های MakanApp/MakanApp_Test نیست. در این مأموریت فقط TestServer و SQL disposable استفاده شدند. هیچ Secret، OTP، JWT، Authorization، cursor، شماره، متن پیام، AI payload، SQL password یا ConnectionString را log یا گزارش نکنید.

## گیرندهٔ آنلاین Inbox را نمی‌بیند

1. حساب/نشست فعال و Workspace واقعی همان Session را بررسی کنید؛ اختلاف Role به‌تنهایی Personal را منع نمی‌کند.
2. listenerها پیش از Snapshot نصب و Hub /hubs/messaging وصل باشد. گروه نشست خودکار است؛ پیام اول به subscription قبلی Conversation وابسته نیست.
3. ContextRevision notification و Snapshot جاری برابر باشد؛ event قدیمی را به Context تازه اعمال نکنید.
4. receipt و GET مجاز messages/summary را با EventId، ResourceId و trace امن تطبیق دهید؛ GET مدیریتی Group/Channel را برای وجود Direct به کار نبرید.
5. پس از reconnect، account replay اجرا شود؛ SignalR reconnect خودِ recovery نیست. HasMore و RetryAfterMilliseconds رعایت شوند.

اگر رویداد رسیده ولی UI عوض نشده، invalidate/refetch، فیلتر/صفحه و dedup بررسی شوند؛ بدون شاهد، علت رخداد خارجی به Client نسبت داده نمی‌شود. Owner bugs در Fixture baseline بازتولید نشدند.

## تشخیص Direct

EfMessagingStore.Diagnostics با DIRECT_RECIPIENT_NOT_AVAILABLE، DenialCategory و TraceId جست‌وجو می‌شود. دسته‌ها NotFound، Suspended، ProfileIncomplete، ContextMismatch، AgeRestricted، PrivacyDenied، RelationshipRequired و Blocked هستند. اطلاعات مقصد در این لاگ نیست؛ علت خصوصی به پاسخ عمومی اضافه نشود.

## دو Outbox موجود و recovery

MessagingRealtimeOutboxProcessor و OutboxProcessor موجود، batch حداکثر ۵۰ و سقف ۱۰ تلاش دارند. claim lease سی ثانیه و budget ارسال invalidation ده ثانیه است. شاخهٔ invalidation در OutboxProcessor شبکه را خارج تراکنش اجرا و قبل/بعد ارسال token/lease را بررسی می‌کند. crash بعد از ارسال می‌تواند همان EventId را دوباره منتشر کند؛ مصرف idempotent است. شکست یا cancellation با Success عوض نمی‌شود.

Outbox عمومی با polling موجود حدود دو ثانیه کار می‌کند؛ این عدد تضمین p95 نیست. ProcessedAt به معنی receipt همهٔ Browserها نیست؛ برای حساب آفلاین یا فاقد مخاطب مجاز، replay/Snapshot مرجع recovery است. Worker قدیمی حق completion claim تازه ندارد. retry/poison از زیرساخت قبلی استفاده می‌کنند؛ queue/worker موازی ساخته نشد.

این queryها صرفاً نمونهٔ خواندنی برای اپراتور مجاز آینده‌اند؛ اینجا روی سرور اجرا نشده‌اند:

```sql
SELECT COUNT_BIG(*) AS PendingCount, MIN(OccurredAtUtc) AS OldestPendingAtUtc
FROM platform.OutboxMessages
WHERE MessageType = N'platform.realtime-invalidation.v1'
  AND ProcessedAtUtc IS NULL AND AttemptCount < 10;

SELECT COUNT_BIG(*) AS ExhaustedCount
FROM platform.OutboxMessages
WHERE MessageType = N'platform.realtime-invalidation.v1'
  AND ProcessedAtUtc IS NULL AND AttemptCount >= 10;

SELECT COUNT_BIG(*) AS PendingCount, MIN(OccurredAtUtc) AS OldestPendingAtUtc
FROM messaging.RealtimeOutbox
WHERE DispatchedAtUtc IS NULL AND AttemptCount < 10;
```

Payload یا User/Session IDs را برای عیب‌یابی عمومی dump نکنید. Attempt/Claim را با UPDATE دستی صفر نکنید. برگرداندن terminal item بدون بررسی علت یا تغییر clock/timeout برای سبزشدن آزمون مجاز فرض نمی‌شود.

## Replay عقب‌مانده یا resync

MIN_ACTIVE_ROWVERSION جلوی حذف transaction دیرCommitشده را می‌گیرد. transaction طولانی می‌تواند cut را عقب نگه دارد. HasMore=true همراه صفحهٔ خالی و retryAfter، وضعیت معتبر انتظار است؛ با loop بی‌مکث یا جلو بردن cursor از push آن را دور نزنید. cut SQL کوتاه Serializable است؛ خطای deadlock/timeout، 503 امن و حفظ cursor قبلی می‌دهد. Query plan و هزینهٔ scan زیر بار واقعی باید جدا سنجیده شوند؛ index یا schema تازه برای این کار ساخته نشده است.

409 REALTIME_RESYNC_REQUIRED یعنی Snapshot مجاز/Context تازه لازم است. token خراب، نشست/کاربر دیگر، عمر ۲۴ ساعت، تغییر Context/Role/Guardian/Refresh یا keyring می‌توانند علت فنی باشند. خطای 401 از مسیر ورود موجود پیگیری می‌شود. تغییر keyring، احراز هویت را Mock نمی‌کند. keyring host باید برای تداوم cursorها با روش خصوصیِ مناسب host نگه‌داری شود؛ محیط تست از provider حافظه‌ای استفاده می‌کند و در این کار تنظیمات خصوصی سرور تغییر نکرده‌اند.

هیچ cleanup خودکار تازه‌ای برای Outbox منبع اضافه نشده است. پاک‌سازی آینده به طراحی retention، کف replay و epoch/resync نیاز دارد؛ حذف مستقیم ردیف‌ها می‌تواند recovery را بشکند.

## metricهای واقعی

Meter موجود MakanApp.Workers، makan.worker.batches/items/failures/outcomes/duration و makan.queue.items/oldest_age را دارد. claim/retry/exhaustion/lease recovery همان WorkerTelemetry ثبت می‌شوند.

Meter جدید MakanApp.Realtime شامل makan.realtime.replay.requests، resync.required، authorization.rejections، invalidation.deliveries، connections.started، connections.active و replay.duration است. label outcome محدود به success/resync/invalid/denied/failed/other است؛ شناسه/متن/Token label نمی‌شود. connections.active شمار transport است، نه کاربر مجاز. started شامل اتصال تازه و اتصال مجدد است و counter مستقلِ تشخیص reconnect ادعا نمی‌شود. deliveries تحویل invocation به گروه نشست است، نه read receipt کاربر.

TraceId producer HTTP در invalidation کمینه وجود دارد؛ برای تمام پیام‌های تاریخی، trace پایدار تازه یا CommitAt دقیق SQL ساخته نشده است. زمان publisher و دریافت در fixture ثبت شد؛ browser render و SLO تولید از آن نتیجه گرفته نمی‌شوند.

## مرزهای امنیت و انتشار

Role/Membership/Guardian/Enrollment و scope جاری قبل از payload آینده کنترل می‌شوند؛ گروه قدیمی دلیل مجوز نیست. Parent چت خصوصی کودک/roster کامل نمی‌گیرد و Manager مالی شخصی اعضا را نمی‌بیند. پایان رابطه history مجاز را خودکار حذف نمی‌کند.

Ready فایل مجوز Safety/Attach/Download نیست. Minor/Unknown رسانه را ReviewRequired نگه می‌دارد؛ بستن Moderation مجوز ارسال نیست. retry ارسال ردشده Safety را دور نمی‌زند.

برای BE-01 ابتدا correlationId/traceId و safetyReference پاسخ 422 را به لاگ SafetyReference متصل کنید. Reason محدود UnsupportedMedia به موتور متن فعلی اشاره دارد؛ PolicyCodes/ProviderReference کامل فقط در Persistence خصوصی و با دسترسی مصوب بررسی شوند. پرونده/Attachment evidence را برای recipient grant تلقی نکنید و Status را با SQL دستی Allowed یا Ready نکنید. workflow تأیید رسانه هنوز PRODUCT_DECISION_BLOCKED است؛ [قرارداد وضعیت و محدودهٔ Audit](../messaging/BE01_REVIEW_REQUIRED_CONTRACT_FA.md).

No Write در زمان پایان Subscription/Grace، event زمان‌محور جدیدی ندارد؛ UI زمان سررسید فقط refetch زمان‌بندی کند و نتیجهٔ سرور را مرجع بگیرد. AI runtime، Provider واقعی، Push دستگاه، چند replica/backplane، IIS/TLS/Proxy، slow consumer/storm و p95/p99 رسمی gate جدا دارند. این مرحله Commit/Push/Publish/Deploy یا Migration سرور نیست.
