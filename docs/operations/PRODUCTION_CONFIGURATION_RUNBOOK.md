# قرارداد کانفیگ Production

بازبینی `2026-11-26` از `source@f8178a47de94eae0ec2431adebcb9256a9789786`.

## مبنای جاری انتشار و تنظیمات

قرارداد این نسخه۴۹۸ عملیات/۴۴۲ مسیر/۷۹۶ schema و مدل۱۵۳ جدول/۷۸ Migration دارد؛ آخرین `20261122085930_AddMessagingMediaReviews`. مدل طراحی و Build موفق، applied schema یا بستهٔ نصب‌شدهٔ سرور نیست. Manifestهای بستهٔ قبلی تاریخچه‌اند؛ برای این Source باید artifact/SQL تازه ساخته و hash مقصد با همان بسته کنترل شود.

| تنظیم | سیاست این انتشار |
|---|---|
| Commerce:EnforcementEnabled | false؛ کنترل مالی قابلیت عادی خاموش، ACL/ظرفیت آموزشی و Safety محفوظ |
| AiUsage:Mode | Unmetered؛ OperationId و Unknown/Retry و ACL منابع پابرجا |
| Payments:Provider | Unavailable؛ Zibal/Store/Paid Provisioning در Runtime Deferred |
| AiService:Enabled / AiKnowledge:Enabled | تابع override محیط؛ base=false. شاهد Test محلی تاریخ‌دار است و وضعیت IIS مقصد را اثبات نمی‌کند |
| AiService:UseServiceAuthentication / AiKnowledge:UseServiceAuthentication | false طبق سیاست جاری؛ User Bearer به upstream منتقل نمی‌شود |
| Features:PersonalClass:AssignmentsEnabled / ExamsEnabled | false؛ Open Access این gateهای مستقل را باز نمی‌کند |
| AccountDeletion:ExecutionEnabled / Storage:TerminalCleanup:Enabled | execution/cleanup پیش‌فرض خاموش؛ بدون سیاست مصوب روشن نشوند |

تنظیم مؤثر پس از JSON و environment override کنترل شود؛ نبود Commerce:EnforcementEnabled در Options برابرtrue است. Secret، ConnectionString و کلید خصوصی در سند یا Git درج نشود. فایل‌های خصوصی محیط را با example یا فایل محیط دیگر جایگزین نکنید.

### Smoke پس از نصب مجاز

1. Environment، Source/Artifact hash، EF history، Health/live/ready و نسخهٔ قرارداد مقصد ثبت شود؛ startup Migration خودکار ندارد.
2. OTP mode، Bearer/refresh، CORS و مجوز منفی منابع؛ false بودن Enforcement و عدم قفل UI از Quota قدیمی کنترل شود.
3. AI معمولی و SSE: header Bearer، عدم buffer پاسخ در Proxy، flush واقعی delta، cancellation و done/error؛ HTTP200 تنها کافی نیست. RAG فقط غیرجریانی و ACL Source جاری است. timeout را برای پنهان‌کردن خطا افزایش ندهید.
4. فایل: Upload→Ready، PendingReview، تصمیم انسانی مجاز، Send/Retry و دریافت بایت با ACL جاری. Approval خودکار یا Resolve به‌جای تصمیم صریح مجاز نیست. Migration AddMessagingMediaReviews قبل binary وابسته اعمال شود.
5. تکلیف Published: Policy با دو RowVersion،412 کهنه،409 مهلت و حفظ Draft/FileAssetId؛ code خطا و X-Correlation-ID/Retry-After از Proxy عبور کنند.
6. Replay، restart و Data Protection ring پایدار، Worker recovery و Backup/Restore هدف مستقل بررسی شوند؛ مقادیر خصوصی در log نریزند.

این به‌روزرسانی هیچ Publish/Deploy، تماس Provider یا SQL سرور اجرا نمی‌کند. پذیرش Backend مقصد با اجرای تست ایزوله یکی نیست.


## تنظیمات پایدار Commerce و Replay

Commerce:QuoteLifetimeMinutes پیش‌فرض15 و Commerce:RenewalAfterGraceStartsAtPayment پیش‌فرضtrue است؛ این تنظیم‌ها نام/قیمت/سقف فروش یا مجوز پرداخت دستی نیستند. قرارداد Legacy بدون ثبت معتبر Admin خودکار enforce نمی‌شود. AiService/AiKnowledge در base غیرفعال و در هر محیط تابع override هستند؛ AI Unmetered و Enforcement=false سیاست انتشارند. Preflight به‌تنهایی تماس AI نیست.

Program.cs از AddDataProtection استاندارد برای cursor حساب استفاده می‌کند؛ ring اختصاصی MFA حذف مانده است. مسیر خصوصی کلیدها/ACL/persistence باید متناسب host بررسی شود و تنظیم حذف‌شدهٔ Security:DataProtection دوباره اختراع نشود. Identity:SecurityKey جای ring cursor نیست. از دست‌رفتن ring، context change یا cursor بیش از24ساعت409/resync می‌خواهد؛ readiness فقط config/SQL/Storage است. [قرارداد و عملیات](REALTIME_RUNBOOK_FA.md).

Account replay پیش‌فرض100 و حداکثر200 رکورد و budget عادی120 درخواست در دقیقه برای حساب دارد؛ limiter درون process است. Cache-Control=no-store و عدم log cursor حفظ شوند. keyring مشترک/backplane/Load چند instance به‌صورت عملیاتی تأیید نشده‌اند.

این سند مرجع تنظیم‌های سخت‌سازی PH است؛ اجرای IIS، providerهای موجود و rollout در
[راهنمای canonical IIS](../production/DEPLOYMENT_IIS.md) و بازیابی در
[Backup/Restore](../production/BACKUP_RESTORE.md) باقی می‌مانند.
فایل نمونه `MakanApp.Api/appsettings.Production.json.example` **secret ندارد و آمادهٔ اجرا نیست**.
هیچ کانفیگ یا secret واقعی سرور در این مرحله خوانده، چاپ یا تغییر داده نشده است.

## حذف زیرساخت صرفاً MFA — ۲۰۲۶-۱۰-۲۴

Platform MFA و Step-Up با تصمیم مالک حذف شده‌اند. `BackofficeSecurity` و `Security:DataProtection` دیگر قرارداد فعال تنظیمات نیستند.
Startup و readiness به key ring مخصوص MFA وابسته نیستند؛ SQL، Storage، OTP، Session و اعتبارسنجی Production همچنان برقرارند.
هیچ پوشه، XML یا گواهی قدیمی سرور در این مرحله حذف نمی‌شود؛ cleanup فقط پس از استقرار نسخهٔ جدید و تأیید Health، با درخواست مستقل مجاز است.
Migration جدید `20261024184613_RemovePlatformMfa` دو جدول MFA و دو ستون Session را Drop می‌کند؛ قبل از اجرای دستی، Backup و توقف نسخهٔ قدیمی لازم است. [اثر دقیق و قرارداد جدید](../platform/PLATFORM_AUTHENTICATION_CHANGE_FA.md).

## مدیریت secret

SQL credential/Connection String، `Identity:SecurityKey`، توکن OTP ملی‌پیامک،
کلید سرویس AI، credential Push و رمز PFX تنها در محیط مجاز تأمین می‌شوند.
نام تنظیم، نه مقدار واقعی، باید در ticket/runbook ثبت شود.
environment هم‌نام بر JSON اولویت دارد؛ مقدار قدیمی IIS می‌تواند مقدار فایل را override کند.
فایل خصوصی `appsettings.Production.json` ignored است؛ در clone تازه باید جداگانه provision شود.
artifact اپراتوریِ شامل این فایل محرمانه است و نباید در لینک عمومی یا Git قرار گیرد.

کلید Identity و Secretهای فعال را در deploy عادی نچرخانید. stable بودن لازم است، ولی کافی برای
امنیت نیست: دسترسی کم‌اختیار، custody، backup، rotation مصوب و آزمایش بازیابی لازم‌اند.
ممیزی tracked config فعلی جای اسکن و remediation تاریخچهٔ قدیمی یا rotation secret افشاشده
را نمی‌گیرد؛ history در این کار بازنویسی نمی‌شود.

## rollout و gateهای مستقل

### snapshot عملیاتی — PH-3

`Observability:OperationalMetrics:SnapshotIntervalSeconds` فاصلهٔ refresh وضعیت صف‌هاست؛
پیش‌فرض ۳۰ و محدودهٔ مجاز ۵ تا ۳۶۰۰ ثانیه است. معادل محیطی آن
`Observability__OperationalMetrics__SnapshotIntervalSeconds` است؛ مقدار نامعتبر
در startup با پیام ثابت رد می‌شود. هر refresh حداکثر budget ده‌ثانیه‌ای و cancellation
مشترک با shutdown دارد؛ این worker فقط aggregateهای SQL موجود را می‌خواند و اثر کسب‌وکاری ندارد.

`OperationalMetricsPublisher` snapshot را در حافظه نگه می‌دارد؛ callbackهای gauge هیچ
SQL، شبکه یا I/O ندارند. شکست refresh، آخرین مقادیر را به صفر تبدیل نمی‌کند؛
`makan.operational.snapshot.available=0` و سن آخرین موفقیت، stale بودن داده را نشان می‌دهند.
پیش از اولین موفقیت، queue خالیِ ساختگی منتشر نمی‌شود. snapshot و limiter/presence
همچنان process-local هستند؛ این تغییر exporter/collector، داشبورد بیرونی یا تحویل alert ایجاد نمی‌کند.
تعداد items برای worker جمع‌آوری metrics، تعداد خلاصه‌صف‌های خوانده‌شده است، نه پیام تحویل‌شده.

### ترتیب rollout

تنظیم اختیاری PH-5، `Hosting:ShutdownTimeoutSeconds` یا
`Hosting__ShutdownTimeoutSeconds`، فقط عدد صحیح ۵ تا ۹۰ ثانیه می‌پذیرد.
نبود آن مقدار نهایی Generic Host/override استاندارد `shutdownTimeoutSeconds` را حفظ می‌کند؛
پیش‌فرض نسخهٔ ۹ برابر ۳۰s است و JSON مشترک کلید پیش‌فرض تازه‌ای برای پوشاندن override قبلی ندارد.
مقدار نهایی خارج budget با خطای ثابت رد می‌شود. این setting با ANCM `shutdownTimeLimit=120`
و timeout مستقل WAS یکی نیست؛ [runbook IIS](../production/DEPLOYMENT_IIS.md#قرارداد-runtime-و-shutdown-در-ph-5)
دستور اپراتوری بررسی آن‌ها را دارد. هیچ تغییری در secret یا کانفیگ خصوصی لازم نیست مگر انتخاب صریح این override.

cleanup جدید PH-6 پیش‌فرض خاموش است؛ `Storage:TerminalCleanup:Enabled` را برای «تکمیل deploy»
خودکار true نکنید. `GracePeriodHours` فقط با تصمیم صریح و مقدار مثبت تعیین می‌شود؛ مقدار پیش‌فرض
حقوقی وجود ندارد. `BatchSize` و `IntervalSeconds` فقط cadence فنی‌اند. تنظیم نامعتبر با پیام ثابت
رد می‌شود و health مقدار Disabled/Enabled تنظیمی را نشان می‌دهد، نه موفقیت runtime.
[سیاست کامل و همهٔ کلیدها](RETENTION_AND_CLEANUP_POLICY.md#قرارداد-تنظیم-و-اجرا) مرجع‌اند؛
دیگر retentionهای محصول/امنیت و log rotation زیرساخت با این flag فعال نمی‌شوند.

1. commit مورد انتظار و backup سازگار DB/Storage و تنظیمات خصوصی را ثبت کنید؛ محتوا/secret چاپ نشود.
2. تنظیمات خصوصی و Secretهای فعال مقصد را حفظ کنید؛ نمونهٔ عمومی را روی آن‌ها overwrite نکنید.
3. مسیر Storage و ACL آن را پیش از start با identity واقعی AppPool آماده کنید.
4. در صورت fail شدن startup، config/ACL/custody را اصلاح کنید؛ validator را خاموش نکنید.
5. `/health/live` و `/health/ready` و سپس OTP و ورود Platform مجاز را از مسیر مجاز بررسی کنید.
6. restart/recycle/reboot و انتقال ماشین، gateهای اپراتوری جدا هستند؛ روی این رایانه اجرا نشده‌اند.

استثنای مصوب HTTP و اعتماد به certificate SQL در انتشار قبلی حفظ می‌شوند؛ این سند
آن‌ها را محیط امن اعلام نمی‌کند. TLS و اعتماد معتبر SQL همچنان gate مستقل Production هستند.
Startup migration و `EnsureCreated` اضافه نشده‌اند؛ migration تنها با principal و تأیید اپراتور.
