# قرارداد کانفیگ Production

## تنظیمات افزودهٔ مبنای ۲۰۲۶-۱۰-۳۰

Commerce:QuoteLifetimeMinutes پیش‌فرض15 و Commerce:RenewalAfterGraceStartsAtPayment پیش‌فرضtrue است؛ این تنظیم‌ها نام/قیمت/سقف فروش یا مجوز پرداخت دستی نیستند. قرارداد Legacy بدون ثبت معتبر Admin خودکار enforce نمی‌شود. AiService:Enabled همچنان false است؛ Preflight یا Trial foundation تماس AI نمی‌سازد.

Program.cs از AddDataProtection استاندارد برای cursor حساب استفاده می‌کند؛ ring اختصاصی MFA حذف مانده است. مسیر خصوصی کلیدها/ACL/persistence باید متناسب host بررسی شود و تنظیم حذف‌شدهٔ Security:DataProtection دوباره اختراع نشود. Identity:SecurityKey جای ring cursor نیست. از دست‌رفتن ring، context change یا cursor بیش از24ساعت409/resync می‌خواهد؛ readiness فقط config/SQL/Storage است. [قرارداد و عملیات](REALTIME_RUNBOOK_FA.md).

Account replay پیش‌فرض100 و حداکثر200 رکورد و budget عادی120 درخواست در دقیقه برای حساب دارد؛ limiter درون process است. Cache-Control=no-store و عدم log cursor حفظ شوند. keyring مشترک/backplane/Load چند instance به‌صورت عملیاتی تأیید نشده‌اند.

بازبینی `2026-10-30` از `main@e11d4ec`: CORS از Configuration و مستقل از Environment است؛ [دو mode و overrideها](../production/CORS_CONFIGURATION_FA.md). OTP/Identity/Storage قرارداد فعال‌اند، نه تنظیمات صرفاً MFA حذف‌شده. ProfilePhoto/Lesson Storage همان زیرساخت موجود را مصرف می‌کنند و secret/مسیر جدیدی لازم ندارند.

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
