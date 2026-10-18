# قابلیت اطمینان و بازیابی Workerها

این سند قرارداد عملیاتی PH-4 برای workerهای واقعی همان API است؛ validation نهایی این
مرحله در [ممیزی PH](PRODUCTION_HARDENING_AUDIT.md) ثبت می‌شود. رقابت scopeها و اتصال‌های SQL
مستقل، مدل رقابت processها را می‌سنجد؛ جای آزمایش recycle یا reboot واقعی IIS نیست. SQL منبع حقیقت durable است؛ wake signal،
SignalR و Presence منبع تحویل قطعی نیستند.

## موجودی واقعی

تا PH-4 هفت `BackgroundService` وجود دارد. اعداد جدول از options/constant فعلی‌اند، نه SLA
محصول یا تنظیم مشاهده‌شدهٔ سرور. cleanup فایل هنوز جزو این موجودی نیست؛ PH-6 جداست.

| Worker و مسئولیت | منبع و مرز batch | lease و retry | شکست/بازیابی و cleanup |
|---|---|---|---|
| `OutboxDispatcher`؛ materialize اعلان و فعال‌سازی اشتراک | `platform.OutboxMessages`؛ حداکثر 50 کار | claim token و rowversion؛ lease 30s؛ سقف 10؛ backoff برابر `2*n*n` با حد 300s | نتیجهٔ SQL و processed در transaction مشترک؛ شکست نهایی با attempt/error متمایز؛ حذف خودکار history ندارد |
| `MessagingRealtimeOutboxDispatcher`؛ hint تغییر Messenger | `messaging.RealtimeOutbox` و `ChangeEvents`؛ حداکثر 50 | rowversion؛ lease 30s؛ سقف 10؛ backoff `2*n` با حد 30s | hint ممکن است تکرار شود؛ Delta SQL مرجع بازیابی؛ failure/lease بدون حذف ChangeEvent؛ history purge ندارد |
| `NotificationDeliveryDispatcher`؛ Push/SMS اعلان | `notification.NotificationDeliveryAttempts`؛ پیش‌فرض حداکثر 50 | یک claim در هر بار؛ token؛ lease پیش‌فرض 30s؛ timeout provider برابر 10s؛ سقف 3؛ backoff از 5s تا 5min | `Delivered`، `Failed`، `Suppressed` و `Deferred` صریح؛ Delivered پذیرش provider است، نه رسید دستگاه؛ claim آخرِ رهاشده terminal می‌شود؛ retention تصمیم جداست |
| `ScheduledMessageDispatcher`؛ اجرای intent زمان‌بندی‌شده | `messaging.ScheduledMessages`؛ حداکثر 20 | یک claim؛ token/rowversion؛ lease 30s؛ سقف 5؛ backoffهای 5/10/20/40s | receipt/message/outbox با هم commit؛ `Dispatched/Cancelled/Failed`؛ مجوز جاری دوباره کنترل می‌شود؛ purge ندارد |
| `ReportExportDispatcher`؛ خروجی CSV | `reports.ReportExportJobs` و FileAsset خصوصی؛ یک job | rowversion؛ lease پیش‌فرض 120s؛ سقف 3؛ فاصلهٔ retry پیش‌فرض 10s | `Completed/Failed`؛ فایل Ready قابل reuse؛ انقضای مجوز retry دائمی نیست؛ purge خروجی تصویب نشده |
| `MessagingEphemeralDispatcher`؛ Typing/Presence | حافظهٔ محدود؛ drain حداکثر 128 hint و poll یک‌ثانیه‌ای | مالکیت dispatch در حافظه؛ retry پایدار/lease SQL موضوعیت ندارد | Typing پیش‌فرض 6s، connection lease برابر 45s و last-seen حافظه‌ای 1 روز؛ coalescing/TTL؛ از دست‌رفتن hint در restart عمدی است |
| `OperationalMetricsPublisher`؛ snapshot aggregate | read modelهای jobs/notification؛ هیچ پیام یا side effect کسب‌وکاری نمی‌سازد | فاصلهٔ پیش‌فرض 30s، محدودهٔ 5..3600s و budget برابر 10s؛ lease/attempt SQL موضوعیت ندارد | failure دادهٔ قبلی را stale می‌کند؛ poll بعدی retry محدود زمانی است؛ history SQL جدیدی ندارد |

SMS این جدول **SMS اعلان** است؛ مسیر ورود/OTP ملی‌پیامک مستقل و در PH تغییر نکرده است.
options محیط می‌توانند defaultهای Notification/Report را در حدود validation موجود تغییر دهند؛
configuration تست با مقادیر کوچک، وضعیت واقعی سرور نامیده نمی‌شود.

## claim و مرز اثر

- Outbox عمومی و realtime در هر بار فقط یک کار را claim می‌کنند؛ انتظار برای کار اول
  lease چهل‌ونه کار بعدی را مصرف نمی‌کند. batch محدود به معنی پیش‌claim کل batch نیست.
- Outbox عمومی زیر قفل کوتاه SQL، token، processed و زمان lease جاری را بازبینی می‌کند.
  ساخت اعلان/اشتراک و processed در همان transaction است؛ شبکه داخل transaction اجرا نمی‌شود.
- realtime پیش از notifier، نسل claim/rowversion و lease را دوباره می‌خواند. notifier بودجه‌ای
  کمتر از lease دارد و ثبت نتیجهٔ قدیمی نمی‌تواند claim جدید را overwrite کند.
- اگر notifier پس از پایان budget هنوز تمام نشده باشد، batch همان‌جا پایان می‌یابد و در scope
  قبلی query/write دیگری اجرا نمی‌شود؛ claim برای recovery lease باقی می‌ماند و fault دیررس
  مشاهده می‌شود. این روش جلوی استفادهٔ هم‌زمان از DbContext را می‌گیرد، ولی کد غیرهمکار را
  متوقف یا network I/O را تضمین‌شده abort نمی‌کند؛ adapter باید cancellation را رعایت کند.
- repair کارهای رهاشدهٔ رسیده به سقف، bounded است؛ کار terminal دوباره به‌عنوان pending
  انتخاب نمی‌شود و poison item نباید کار سالم بعدی را برای همیشه متوقف کند.
- زمان معتبر، UTC سمت سرور و پس از گرفتن قفل است؛ زمان client یا زمانی که پیش از انتظار
  قفل خوانده شده، مجوز اثر جدید ایجاد نمی‌کند.

`WorkerCompletionBudget` برای ثبت مستقل نتیجه/failure حداکثر 5s فرصت می‌دهد؛
`CancellationToken.None` روش نامحدود نگه‌داشتن shutdown نیست. cancellation درخواست‌شده
failure تجاری تازه نمی‌سازد؛ claim ناتمام در SQL برای recovery باقی می‌ماند.
انقضای lease فقط امکان رقابت مجدد است، نه مجوز نادیده‌گرفتن نسل مالکیت در completion.

claim/repairهای دارای `READPAST` از locking سازگار با RCSI استفاده می‌کنند؛
`READCOMMITTEDLOCK` با `ROWLOCK` هم‌زمان اضافه نمی‌شود. این تصمیم، isolation کل دیتابیس را
عوض نمی‌کند و ادعای بازتولید خطای همهٔ مسیرهای قدیمی نیست. `READPAST` قفل row را رد می‌کند،
نه هر page/index lock را؛ timeout/cancellation و ترتیب قفل همچنان لازم‌اند.
[مرجع رسمی table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table).
repair عمومی/realtime transaction کوتاه و صریح `ReadCommitted` دارد؛ isolation قبلی connection
pooled نباید به اجرای خارج transaction نشت کند. تست regression همان connection را پس از
transaction کامل‌شدهٔ Serializable استفاده می‌کند؛ تغییر global isolation راه‌حل این مسئله نیست.

## خروجی گزارش و filesystem

`ReportExportOutputStore` برای هر تلاش، بایت‌ها را با key تصادفی مستقل و خارج transaction
SQL می‌نویسد؛ key canonical قبلی را overwrite یا حذف نمی‌کند. سپس transaction کوتاه، job
و نسل claim/lease را قفل و بازبینی می‌کند و metadata فایل را منتشر می‌کند.
شناسهٔ canonical همچنان `FileAsset.Id == ReportExportJob.Id` است؛ contract عمومی تغییر نمی‌کند.
Ready immutable است؛ فقط Pending قدیمی می‌تواند زیر همان guard به key تلاش جدید اشاره کند.
completion job نیز با همان نسل claim ثبت می‌شود؛ worker ازمالکیت‌افتاده، owner جدید را fail/retry نمی‌کند.

اگر بعد از Ready و قبل از Completed process متوقف شود، تلاش بعدی خروجی Ready را reuse
می‌کند. اگر acknowledgement commit گم شود، exception به معنی rollback قطعی نیست؛ candidate
ممکن است همان فایل canonical باشد و در catch عمومی حذف نمی‌شود. فقط loser/redundant بودنِ
قطعاً اثبات‌شده اجازهٔ best-effort حذف candidate خودش را می‌دهد.

SQL و filesystem transaction مشترک ندارند. crash پیش از انتشار metadata می‌تواند candidate
بدون reference باقی بگذارد؛ تعداد به تلاش‌های واقعی و سقف retry محدود است. برای مجموعهٔ نامحدود
jobها همچنان orphanها می‌توانند رشد کنند؛ این سند ادعای cleanup خودکار یا cross-store atomicity ندارد.
reconciliation آن‌ها به inventory و policy جدا نیاز دارد؛ root sweep مجاز نیست.
شکست best-effort حذف candidate قطعاً منتشرنشده، با stage ثابت `Cleanup` و warning امن
نمایان می‌شود؛ این signal به معنی اجرای reconciliation همهٔ orphanها نیست.

## حد idempotency و delivery خارجی

یک Notification/Message/receipt/Subscription durable نباید به خاطر retry دوباره ساخته شود؛
constraint، transaction و کلید منطقی ثابت این مرز را حفظ می‌کنند. آزمون concurrency محلی
روی SQL واقعی، رقابت claim و اثر durable را بررسی می‌کند.

پذیرش Push/SMS توسط provider و ثبت acknowledgement در SQL اتمیک نیستند. اگر provider
پذیرفته ولی process پیش از ثبت نتیجه از بین برود، ارسال دوباره ممکن است. شناسهٔ منطقی اعلان/
delivery و collapse/dedup tag پایدارند، اما **تحویل خارجی exactly-once تضمین نشده است**.
Sent یا پذیرش provider نیز اثبات دریافت دستگاه نیست؛ `P1_PUSH_RUNTIME_PENDING` مستقل باقی است.
realtime نیز at-least-once hint است و مصرف‌کننده با cursor/Delta حالت authoritative را می‌گیرد.
هیچ دستوری برای reset کور attempt یا موفق اعلام‌کردن دستی کار ارائه نمی‌شود.

## shutdown و هم‌پوشانی IIS

Host باید cancellation بدهد و فرصت bounded برای خروج داشته باشد. drain application، timeout
ANCM و timeout WAS/AppPool سه بودجهٔ جدا هستند؛ راهنمای دقیق [IIS](../production/DEPLOYMENT_IIS.md)
را اجرا کنید. کد background داخل API با idle/stop خود API می‌ایستد.

دو process hardened ممکن است هنگام overlapped recycle رقابت کنند؛ durable claim/fencing باید
ایمن بماند. این شاهد به معنی پشتیبانی scale-out SignalR/Presence نیست؛ حافظه process-local است.
دو publisher نیز می‌توانند همان backlog SQL را بخوانند؛ collector نباید gauge صف را بین آن‌ها
`sum` کند و backlog را دوبرابر نشان دهد. انتخاب instance یا aggregation مانند `max`/dedup
سیاست monitoring است؛ شمارندهٔ درخواست/تلاش با gauge مشترک صف یکسان نیست.

**اولین rollout این اصلاح:** باینری قدیمیِ report export پیش از fencing جدید filesystem می‌نویسد.
تست new/new، سازگاری old/new را ثابت نمی‌کند. اپراتور باید نسخهٔ legacy را drain/stop و خروج
process قدیمی را تأیید کند و سپس worker جدید را start کند. به این دلیل قابلیت‌های ایمنی IIS،
ping، rapid-fail protection یا overlapping recycle را به‌طور عمومی غیرفعال نکنید.
هیچ Stop/Recycle/Reboot واقعی در اجرای محلی PH انجام نشده است.

## عیب‌یابی و اقدام اپراتور

1. live و ready را جدا بخوانید؛ ready شرط config/ring/SQL/storage است، نه دریافت Push.
2. با permission فعلی `OperationsRead`، endpointهای jobs/notifications را بخوانید؛
   payload، credential، token یا connection string را برای شواهد استخراج نکنید.
3. count، oldest age، snapshot age/availability، failure جدید و کد ثابت worker را مقایسه کنید.
   صف آیندهٔ Scheduled، backlog موعدگذشته نیست؛ backoff Outbox همچنان pending است.
4. اگر SQL/storage/ring خراب است، علت را اصلاح و recovery خودکار محدود را مشاهده کنید؛
   SQL Server، key ring، metadata یا پیام‌ها را برای «سبز کردن» health پاک نکنید.
5. failed terminal نیازمند triage است؛ reset attempt و retry دستی بدون workflow مصوب مجاز نیست.
6. رشد history/orphan، موضوع retention و ظرفیت است؛ قانون حذف عمومی از این سند استنتاج نشود.

## شواهد و مسیر مطالعه

- `NotificationEndpointsTests`: dedup و retry Outbox موجود.
- `MessagingP1NotificationDeliveryTests`: claim منقضی/نهایی، cancellation، registration و مجوز جاری.
- `MessagingP2ScheduledEndpointsTests`، `MessagingP2ScheduledSafetyTests` و
  `MessagingP2ScheduledRecoveryTests`: claim/cancel/send race، permission جاری و rollback receipt.
- `MessagingEphemeralStateTests` و `MessagingP2EphemeralEndpointsTests`: TTL، محدودیت حافظه و reconnect.
- `WorkerDispatcherLogSafetyTests`: خروج شش dispatcher پس از failure/poll و لاگ امن.
- `OperationalMetricsPublisherLifecycleTests`: خروج worker هفتم در poll و I/O لغوشونده، بدون failure ساختگی.
- `ReportExportRecoveryTests` و `ReportExportFilePublicationTests`: generation fencing، I/O هم‌پوشان،
  commit دارای acknowledgement گم‌شده، guard Pending/Ready و recovery خروجی مستقل.
- `OperationsWorkerRecoveryTests` و `OutboxRecoveryStateTests`: RCSI واقعی، claim تک‌کار،
  repair محدود/عدم تکرار terminal، stale notifier، timeout، rollback و شکاف provider/SQL.

تست report مربوط به آخرین file lock، query واقعی را اجرا و سپس با interceptor ساعت را جلو
می‌برد؛ شاهد ترتیب fresh-clock است، نه ادعای lock wait واقعی با اتصال دوم. تست‌های recovery
از DataSource مجاز مصنوعی برای جداسازی persistence استفاده می‌کنند؛ authorization همچنان با
تست‌های endpoint موجود اثبات می‌شود. DB یکتای تست worker با نام
`MakanApp_Operations_Workers_<guid>` برای بررسی باقی می‌ماند؛ داده/تنظیم DB عادی تغییر نمی‌کند.

مسیر مطالعه: dispatcher → processor/use case → claim store و domain transition → EF configuration
و unique/rowversion → تست‌های recovery → این runbook. نتیجهٔ واقعی PH-4:
13 Unit و 86 Integration متمرکز؛ final suite برابر 644 Unit، 1120 Integration و 14 Architecture،
failed/skipped=0 و build بدون warning/error. این شامل 40 مورد جدید است؛ هیچ شاهد سرور از نام تست استنتاج نشود.
