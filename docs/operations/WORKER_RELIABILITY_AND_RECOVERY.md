# قابلیت اطمینان و بازیابی Workerها

این سند قرارداد عملیاتی PH-4 برای workerهای واقعی همان API است؛ validation نهایی این
مرحله در [ممیزی PH](PRODUCTION_HARDENING_AUDIT.md) ثبت می‌شود. رقابت scopeها و اتصال‌های SQL
مستقل، مدل رقابت processها را می‌سنجد؛ جای آزمایش recycle یا reboot واقعی IIS نیست. SQL منبع حقیقت durable است؛ wake signal،
SignalR و Presence منبع تحویل قطعی نیستند.

## موجودی اولیهٔ تاریخی

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

### افزودهٔ محدود PH-6

`TerminalFileCleanupDispatcher` هشتمین worker است؛ پیش‌فرض `Storage:TerminalCleanup:Enabled=false`
و در این حالت هیچ cleanup I/O/SQL انجام نمی‌دهد. منبع آن فقط `storage.FileAssets` در وضعیت
Deleted/Rejected است؛ batch پیش‌فرض 25 با سقف 100، فاصلهٔ پیش‌فرض 60s با حدود 5..3600s و
grace بدون پیش‌فرض است. هر asset زیر قفل SQL جاری و بودجهٔ لغوشوندهٔ 10s بررسی می‌شود.
lease/retry durable تازه و SQL write ندارد؛ crash/retry از missing بودن bytes و metadata ثابت
بازیابی می‌شود. دو cursor نوبتی و process-local بین scopeها باقی می‌مانند؛ restart ممکن است rescan کند.

همهٔ referenceهای Submission/Message/Moderation/Scheduled و ReportExport (از جمله شناسهٔ canonical)
مانع حذف‌اند. فقط syscall حذف محلیِ کوچک زیر قفل مجاز است؛ syscall معیوب به‌زور متوقف‌شده فرض نشود.
failure یک candidate، failure counter/warning امن دارد و candidate بعدی را برای همیشه مسدود نمی‌کند.
`worker.items` این worker فقط تعداد Deleted است؛ شمارش کامل Examined/Missing/Skipped/Failed از
`makan.file_cleanup.results` با label محدود outcome می‌آید. هیچ SQL row purge از آن استنتاج نشود.
[سیاست، تنظیم‌ها و محدودیت‌های دقیق](RETENTION_AND_CLEANUP_POLICY.md) و تست‌های
`TerminalFileCleanupTests`، `TerminalFileCleanupFileSystemTests`، `TerminalFileCleanupOptionsTests`
و `TerminalFileCleanupDispatcherTests` مرجع این افزوده‌اند؛ activation واقعی همچنان جداست.

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
2. با permission فعلی `OperationsRead`، health/jobs و با `NotificationsRead`، notifications را بخوانید؛
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

## PH-4.0 — ممیزی مجدد پیش از تغییر کد

مبنای این ممیزی `0dad51784d4e0a2cf8b79957a674123dd1e72237` و worktree تمیز
`MakanApp-Backend-Production-Hardening` است؛ HEAD با `origin/main` تطبیق داده شد.
این بخش پیش از هر تغییر کد ثبت شده است. restore و build اولیهٔ Release موفق، warning/error صفر
و EF pending model changes برابر NONE است. baseline قبلاً اجراشده 676 Unit، 1223 Integration و
14 Architecture است؛ عدد 1913 اجرای مجدد baseline در این ممیزی نامیده نمی‌شود.

موجودی کامل فعلی **هشت BackgroundService و یک IHostedLifecycleService** است. جدول اولیهٔ بالا
به‌همراه افزودهٔ PH-6، polling، batch، lease، retry، idempotency و cleanup واقعی را بیان می‌کند.
طبقه‌بندی پیش از اصلاح:

| مورد | وضعیت | نتیجهٔ بررسی و کار لازم |
|---|---|---|
| `OutboxDispatcher` / `OutboxProcessor` | PARTIAL | transaction مشترک اثر/processed، claim تک‌کار، lease و repair محدود موجودند؛ lookup همهٔ recipientها باید به Any محدود تبدیل شود؛ retry بدون تغییر SQL نباید موفق شمرده شود؛ repair نهایی و outcomeها باید قابل مشاهده شوند. |
| `MessagingRealtimeOutboxDispatcher` / `MessagingRealtimeOutboxProcessor` | PARTIAL | claim نسل‌دار، timeout notifier و Delta پایدار موجودند؛ completion باید ساعت را پس از انتظار قفل بخواند؛ fan-out sessionها نباید کل مخاطبان را در حافظه نگه دارد؛ outcomeها و شواهد retry/poison تکمیل شوند. |
| `NotificationDeliveryDispatcher` / `NotificationDeliveryProcessor` | PARTIAL | token، rowversion، preference/mute/quiet-hour و authorization جاری موجودند؛ claim/renew/result/defer باید lease زنده را با ساعت تازه پس از قفل کنترل کنند، حتی اگر مالک بعدی هنوز claim نکرده باشد؛ fan-out دستگاه‌ها محدود و نتیجهٔ transitionها صریح شود. |
| `ScheduledMessageDispatcher` / `ScheduledMessageProcessor` | PARTIAL | SQL Serializable، ساعت پس از قفل، receipt/message/outbox اتمیک و مجوز جاری موجودند؛ repair poison نباید پایان کاذب batch بسازد؛ outcomeها و شواهد سقف batch/backoff تکمیل شوند. |
| `ReportExportDispatcher` / `ReportExportProcessor` | PARTIAL | claim نسل‌دار، publication قفل‌شده، Ready reuse و فایل مستقل هر تلاش موجودند؛ outcome/repair و تست progression/poison تکمیل شود؛ cancellation نوشتن CSV بررسی شود. |
| `TerminalFileCleanupDispatcher` | SAFE | پیش‌فرض خاموش؛ scan محدود، دو cursor، قفل referenceها، حذف idempotent bytes بدون حذف metadata؛ lease durable موضوعیت ندارد. تنظیم activation/grace تغییر نمی‌کند. |
| `MessagingEphemeralDispatcher` | SAFE | حافظه/TTL و drain محدود، gate محلی و authorization جاری؛ hint گذراست، SQL lease/recovery durable موضوعیت ندارد. این شاهد distributed Presence نیست. |
| `OperationalMetricsPublisher` | SAFE | snapshot فقط خواندنی و budget محدود؛ failure availability را false می‌کند و poll بعدی retry است؛ ادغام gauge چند instance باید max/dedup باشد. |
| `DataProtectionKeyRingStartupValidator` | SAFE | `StartingAsync` پیش از workerها fail-closed است، probe لغوشونده؛ queue/batch/lease/retry durable موضوعیت ندارد. |

### مرزهای concurrency و failure

- عمومی/realtime: claim در transaction کوتاه ReadCommitted با UPDLOCK/READPAST/READCOMMITTEDLOCK؛
  lease 30s، سقف 10؛ repair حداکثر 50. شکست نهایی همان attempt/error موجود است؛ مدل شکست جدید لازم نیست.
- Notification: batch پیش‌فرض 50 و حداکثر 200، lease پیش‌فرض 30s، provider timeout پیش‌فرض 10s؛
  سقف پیش‌فرض 3 و حداکثر 10، backoff نمایی از 5s تا 5min؛ Retry-After موجود حفظ می‌شود.
- Scheduled: batch 20، lease 30s، سقف 5، backoffهای 5/10/20/40s؛ مجوز در مرز send دوباره ارزیابی می‌شود.
- Report: یک job، lease پیش‌فرض 120s، سقف 3، retry پیش‌فرض 10s؛ repair حداکثر 100، CSV صفحه‌بندی‌شده و سقف row موجود.
- هیچ network call در transaction claim/result قرار نمی‌گیرد. shutdown کار جدید نمی‌گیرد؛
  نتیجهٔ پذیرفته‌شده فقط در budget مستقل 5s قابل ثبت است؛ کار ناتمام برای lease recovery می‌ماند.
- هیچ queue/history purge یا فعال‌سازی cleanup از این اصلاح نتیجه نمی‌شود. دستگاه یا مخاطب جدیدی
  به خاطر محدودسازی حافظه حذف نخواهد شد؛ pagination جای quota محصولی را نمی‌گیرد.

### ماتریس شواهد پیش از اصلاح

| Gate | شواهد موجود / شکاف مشخص |
|---|---|
| W01 | مسیر عادی Outbox، Notification، Scheduled و Report در endpoint/recovery testها موجود است. |
| W02/W09 | رقابت scope/connection مستقل برای عمومی، realtime، Scheduled، Report و cleanup موجود است؛ Notification به شاهد مستقل lease و مالک دیررس نیاز دارد. این شبیه‌سازی overlap است، نه recycle واقعی IIS. |
| W03/W04 | `OperationsWorkerRecoveryTests`، `MessagingP1NotificationDeliveryTests`، `MessagingP2ScheduledRecoveryTests` و `ReportExportRecoveryTests` recovery را می‌سنجند؛ fresh-clock پس از lock wait برای realtime/Notification تکمیل شود. |
| W05/W06 | transient/permanent و سقف Domain موجود است؛ progression واقعی SQL، poll زودرس و آخرین attempt برای صف‌های durable تکمیل شود. |
| W07 | poison عمومی و cleanup موجود است؛ مخلوط poison/چند کار سالم برای دیگر صف‌ها تکمیل شود. |
| W08 | cancellation، completion budget مستقل و rollback موجود است؛ owner منقضی نباید با renew/defer budget را تغییر دهد. |
| W10 | unique notification، subscription، message/receipt و report Ready reuse موجودند؛ ارسال خارجی at-least-once باقی است. |
| W11 | query/claim/repair محدود موجود است؛ آزمون مرز batch و حذف materialization کل مخاطبان/دستگاه‌ها لازم است. |
| W12 | authorization مجدد Notification، Scheduled، realtime audience و Report موجود است؛ خطای فنی مجوز نباید success شود. |
| W13 | `BlockAtDispatchDoesNotUseCreationTimePermission`، `BlockWhileSafetyIsRunningWinsBeforeCommit` و Notification current-state موجودند؛ Block به‌خودی‌خود سیاست حذف تاریخچه یا hint جدید ایجاد نمی‌کند. |
| W14 | حذف عضویت گروه/کانال و سازمان در Scheduled/Notification/realtime کنترل می‌شود؛ شواهد صریح مورد نیاز تکمیل شود. |
| W15 | provider/safety recovery و retry موجودند؛ تست‌های dependency سالم‌شده و metric snapshot تکمیل شوند. |

شمارنده‌های فعلی batch/items/failures/duration و gauge صف امن‌اند، اما items به معنی completion نیست.
اصلاح مورد نیاز، counter مستقل با worker/outcome محدود و ثبت صرفاً transition تأییدشده است؛
هیچ شناسهٔ کاربر/job/message/conversation وارد labelهای metric نمی‌شود؛ محتوا، exception خام و secret
در signalهای جدید ثبت نمی‌شوند. لاگ‌های قدیمیِ دارای شناسهٔ عملیاتی با این ممیزی حذف یا بازنویسی نشده‌اند.
نیاز به schema/migration جدید در ممیزی یافت نشد؛ پس از اصلاح دوباره EF model بررسی می‌شود.

### اصلاحات ممیزی مجدد و مسیر اجرای واقعی

- `OutboxProcessor`: lookup یک recipient با EXISTS؛ نتیجهٔ retry فقط از همان نسل قفل‌شده
  برمی‌گردد. commit گم‌شده، retry یا completion تأییدنشده در metric نمی‌سازد؛ unique SQL همچنان مرجع اثر است.
- `NotificationDeliveryCoordinator` و `EfNotificationPreferenceStore`: keyset صفحه‌های 128 دستگاه؛
  هر صفحه در **همان transaction موجود Outbox** ذخیره و فقط attemptهای همان صفحه detach می‌شوند.
  save صفحه commit نیست؛ شکست صفحهٔ بعد، notification و همهٔ attemptها را rollback می‌کند.
  بدون transaction مالک، persistence صفحه پیش از tracking رد می‌شود. recipient/device quota جدیدی نداریم.
- `MessagingRealtimeAudienceResolver` به صورت stream داخلی، صفحه‌های SQL حداکثر 128 session را
  با scope مجوز تازه برای هر صفحه ارزیابی می‌کند؛ `SignalRMessagingRealtimeNotifier` حداکثر 128 group
  در هر فراخوانی می‌فرستد. reader پیش از query مجوز یا ارسال بسته است. شکست صفحهٔ بعد ممکن است
  hint صفحهٔ قبلی را در retry تکرار کند؛ مصرف‌کننده همچنان از cursor/Delta authoritative بازیابی می‌کند.
- `MessagingRealtimeOutboxProcessor.FinalizeClaimAsync`: قفل کوتاه row، ساعت تازه، rowversion و
  lease زنده، سپس ثبت completion/retry؛ هیچ network call داخل این transaction نیست.
- `NotificationDeliveryProcessor`: claim ساعت را بعد از انتظار قفل می‌خواند؛ renew، result و quiet-hour
  defer همگی زیر قفل، token/status/expiry جاری را می‌سنجند. نبودن مالک بعدی، lease منقضی را معتبر نمی‌کند.
  `WaitAsync` انتظار provider را حتی اگر token را رعایت نکند محدود می‌کند؛ task دیررس فقط مشاهده می‌شود
  و SQL/receipt را تغییر نمی‌دهد. این کار توقف اجباری network I/O یا external exactly-once نیست.
- `ScheduledMessageClaim.IsTerminal` فقط قرارداد داخلی processor/store است. repair یک slot از همان
  batch 20 را مصرف و پردازش را ادامه می‌دهد؛ نه پایان کاذب صف داریم و نه repair loop نامحدود.
- `EfReportExportJobStore` claim/recovery/repair/result را پس از commit موفق می‌شمارد؛ publication و
  Ready reuse قبلی حفظ شده‌اند. `CsvReportExporter` token را به نوشتن header/row نیز می‌دهد.
- `WorkerTelemetry.RecordOutcome`: شمارندهٔ جدید با worker/outcome محدود؛ `worker.items` قبلی معنی
  completion نمی‌گیرد. ack نامعلوم می‌تواند کم‌شماری بسازد؛ ledger SQL با metric جایگزین نشده است.

این تغییرات در Application (قرارداد داخلی، orchestration و marker repair)، Infrastructure (SQL،
فنس مالکیت، provider wait و telemetry) و adapter SignalR در Api قرار دارند. Domain تغییر نکرده است؛
هیچ وابستگی EF/SQL از Infrastructure به Application/Domain منتقل نشده است. هیچ route، DTO عمومی،
جدول، index، constraint، migration یا تنظیم جدیدی ایجاد نشده است.

جریان اعلان: transaction کسب‌وکاری → `EfOutbox` → SQL → `OutboxProcessor` →
`NotificationDeliveryCoordinator` → صفحه‌های `EfNotificationPreferenceStore` → commit notification/attempt/processed →
`NotificationDeliveryProcessor` → بازبینی مجوز و live lease → provider خارج transaction → نتیجهٔ fenced در SQL.
جریان Scheduled: intent پایدار → `ScheduledMessageProcessor` → `EfMessagingStore` → safety/current authorization →
transaction واحد message/receipt/change/outbox/schedule → hint و اعلان با workerهای مستقل.

### شواهد تکمیلی W01–W15

| Gate | شاهد تکمیلی واقعی در کد تست |
|---|---|
| W01/W10 | `OutboxNormalBatchClaimsAtMostFiftyAndLeavesFollowingWorkUnclaimed`، `DeviceFanoutUsesBoundedPagesAndTrackingWhilePreservingOuterTransaction` و `Ph4ScheduledBatchProcessesTwentyOfTwentyOneWithoutDuplicateMessage` |
| W02/W09 | `RealtimeUnexpiredClaimCannotBeOwnedByASecondIndependentProcessor` و `IndependentWorkersCannotClaimSameLiveDeliveryAndDoNotPreclaimFollowingRows`؛ scope/connection مستقل، نه IIS واقعی |
| W03/W04 | `LostClaimCommitAcknowledgementDoesNotInventAConfirmedMetricAndCanRecover`، `LateOwnerCannotOverwriteSuccessorDeliveryEvenWhenItsProviderAccepted` و recoveryهای قبلی |
| W04 | `RealtimeCompletionAndRetryReadFreshLeaseAfterActualSqlLockWait` و `CompletionChecksFreshClockAfterRealSqlLockWaitWithoutSuccessor`؛ انتظار واقعی SQL با اتصال دوم و شاهد DMV |
| W05/W06 | `OutboxSqlBackoffProgressionTerminalCeilingAndPoisonProgressAreDeterministic`، `TransientSqlDeliveryRetriesFollowBackoffAndTerminateAtConfiguredCap` و `Ph4ScheduledSqlRetryProgressesFiveTenTwentyFortyThenFailsAndRejectsEarlyPoll`؛ Report retry ثابت 10s و سقف 3 در `ReportExportRecoveryTests` |
| W07 | `ExhaustedAndPermanentPoisonConsumeBoundedSlotsWithoutBlockingHealthyRows`، `Ph4ScheduledExhaustedPoisonDoesNotEndBatchBeforeMultipleHealthyItems` و `PermanentPoisonJobDoesNotPreventMultipleFollowingHealthyJobsFromCompleting` |
| W08 | `IgnoredProviderCancellationIsBoundedAndLateFaultCannotRewriteRecoveredDelivery`، cancellation تست‌های قبلی، CSV و notifier جدید |
| W10 | `GeneralCommitAcknowledgementLossCannotDuplicateDurableEffectOrInventRetryOutcome` و تست acknowledgement/publication قبلی Report؛ این شاهد تحویل exactly-once خارجی نیست |
| W11 | batch 51→50 عمومی/realtime، 21→20 Scheduled، `Ph4ScheduledRepairCountsTowardTheSameTwentyItemBatchLimit`، fan-out 261 دستگاه، `RealtimeAudienceUsesBoundedKeysetPagesWithoutLosingAuthorizedSessions` و `CsvReportExporterBoundsTests` |
| W12/W13 | تست‌های current-state Notification و Scheduled در بخش ممیزی؛ block/attachment/safety بعد از queue؛ هیچ سیاست مجوز جدیدی از PH-4 استنتاج نشده است |
| W14 | `RevokedOrganizationMembershipSuppressesQueuedDeliveryUsingCurrentSqlPreferenceScope` و تست‌های عضویت/نقش موجود |
| W15 | `HealthyDependencyAfterTransientFailureCompletesTheSameDurableJobExactlyOnce`، provider دیررس/worker جدید، safety موجود و `OperationalMetricsPublisherLifecycleTests` failure→healthy |

محدودیت‌های عملیاتی ثابت‌اند: Push واقعی، recycle/reboot IIS، collector/alert delivery و reconciliation
orphanها تست محیط واقعی جدا می‌خواهند. session/device افزوده‌شده پشت cursor تضمین snapshot جدیدی نمی‌گیرد؛
مجوز ارسال در مرز delivery دوباره سنجیده می‌شود. کل زمان fan-out یک notification همچنان تابع تعداد
دستگاه‌هاست؛ query، صفحه و tracker محدود شده‌اند، نه quota یا transaction تقسیم‌شدهٔ جدید.

### راهنمای آموزشی و فایل‌های مهم این اصلاح

فایل production تازه‌ای ساخته نشد؛ رفتارهای موجود اصلاح شدند. فایل‌های تازهٔ تست:

| مسیر در `tests/` | مسئولیت و نکتهٔ مطالعه |
|---|---|
| `MakanApp.IntegrationTests/NotificationWorkerRecoveryTests.cs` | claim دو اتصال، مالک دیررس، قفل واقعی و timeout؛ expiry به تنهایی نیز مالکیت را باطل می‌کند. |
| `MakanApp.IntegrationTests/OperationsWorkerRecoveryTests.OutboxHardening.cs` | batch، backoff، poison، ack نامعلوم و EXISTS؛ تعداد attempt با تعداد اثر کسب‌وکاری یکی نیست. |
| `MakanApp.IntegrationTests/Ph4NotificationFanoutTests.cs` | 261 دستگاه و rollback صفحهٔ بعد؛ flush داخل transaction با commit آن تفاوت دارد. |
| `MakanApp.IntegrationTests/Ph4ScheduledWorkerRecoveryTests.cs` | batch/repair بیست‌تایی و backoff SQL؛ repair نیز سهمیهٔ کار همان poll را مصرف می‌کند. |
| `MakanApp.IntegrationTests/MessagingRealtimeBoundedAudienceTests.cs` | SQL page واقعی و adapter SignalR؛ bounded stream نباید مخاطبان را به quota تبدیل کند. |
| `MakanApp.IntegrationTests/MessagingRealtimeAudienceTestExtensions.cs` | materialize کردن stream فقط برای assertion تست؛ جمع‌کردن همهٔ sessionها به production برنمی‌گردد. |
| `MakanApp.UnitTests/Reports/CsvReportExporterBoundsTests.cs` | سقف page/row و cancellation خالص، بدون ادعای اثبات SQL توسط mock. |

تست‌های موجود Report، telemetry و lifecycle نیز تقویت شدند. تست typed notifier در پروژهٔ Integration
قرار دارد ولی adapter با fake محدود را می‌سنجد؛ این شاهد شبکه یا SignalR server واقعی نیست. شواهد
claim/lease/constraint از تست‌های واقعی LocalDB می‌آیند، نه EF InMemory.

نکات آموزشی:

1. ترتیب درست `lock → fresh UTC → validate ownership → write → commit → outcome` است؛
   predicate زمانیِ ساخته‌شده قبل از lock wait می‌تواند به مالک منقضی اجازهٔ completion بدهد.
2. rowversion نسل را کنترل می‌کند؛ live lease و token همچنان لازم‌اند. «مالک بعدی هنوز نیامده» مجوز نیست.
3. acknowledgement گم‌شده شکست قطعی transaction نیست؛ dedup SQL از retry metric مهم‌تر است.
4. محدودسازی page و ChangeTracker بدون شکستن transaction، مصرف حافظه را کم می‌کند؛ برای این
   هدف نباید atomicity notification/Outbox یا تعداد دستگاه‌های مجاز تغییر کند.
5. cancellation پایان انتظار است، نه تضمین توقف syscall/provider؛ task دیررس نباید مسیر ذخیرهٔ دیگری داشته باشد.
6. stream و scope مجوزِ صفحه‌ای fan-out را محدود می‌کنند، اما hint جزئی قابل تکرار است و Delta مرجع می‌ماند.

مسیر مطالعه: `OutboxDispatcher.cs` → `NotificationDeliveryCoordinator.cs` →
`NotificationPreferenceAbstractions.cs` → `EfNotificationPreferenceStore.cs` →
`NotificationDeliveryDispatcher.cs` و `.Authorization.cs` → `EfMessagingStore.ScheduledDispatch.cs` →
`EfMessagingStore.Advanced.Send.cs` → `EfReportExportJobStore.cs` → `WorkerTelemetry.cs` → تست‌های جدول.
EF configuration و migrationهای فعلی فقط برای درک unique/rowversion خوانده می‌شوند؛ در این اصلاح
تغییر نکرده‌اند. مرحلهٔ بعد، اجرای کنترل‌شدهٔ سناریوی drain/recycle و مشاهدهٔ recovery روی محیط مجاز IIS
با collector واقعی است؛ توسعهٔ provider، AI، policy کودک یا retention جدید نباید با آن مخلوط شود.

### validation ممیزی مجدد

- restore موفق؛ Release build موفق با warning/error صفر.
- focused: 53 Unit و 118 Integration، failed/skipped صفر.
- regression کامل Release: 690 Unit، 1258 Integration و 14 Architecture؛ مجموع 1962،
  executed/passed برابر total، failed/skipped صفر. baseline برابر 1913 بود؛ 49 تست افزوده شد و هیچ تستی حذف نشد.
- EF `has-pending-model-changes` دوباره NONE؛ هیچ migration ساخته یا روی DB برنامه/سرور اجرا نشد.
- اصلاح fixtureهای جدید پیش از موفقیت: استفاده از UTC برای datetime2 خوانده‌شده، کنترل cancellation
  در مرز تست مالک منقضی و assertion غیرnullable. build هم‌زمان با testhost در Windows یک بار به file lock
  برخورد کرد؛ اجرای ترتیبی پس از اتمام testhost موفق شد، نه با suppress warning یا تغییر framework.
- final build موفق با warning/error صفر؛ final test نیز دوباره 690 Unit، 1258 Integration و
  14 Architecture را اجرا و پاس کرد؛ مجموع 1962، failed/skipped صفر.
- پس از اصلاح و این validation، صف‌های durable ممیزی‌شده در محدودهٔ شواهد محلی SAFE هستند؛
  lease/retry durable برای Ephemeral، snapshot publisher، cleanup scanner و startup validator
  NOT_APPLICABLE است. نتیجهٔ commit/push و تطبیق SHA در گزارش نهایی همان اجرا ارائه می‌شود.

اسناد TRX محلی در `tests/*/TestResults/ph4-focused-*.trx`، `ph4-regression.trx` و `ph4-final.trx`
تولید می‌شوند؛ generated artifact وارد Git نمی‌شود. DBهای GUID ایزوله برای شواهد باقی می‌مانند.
