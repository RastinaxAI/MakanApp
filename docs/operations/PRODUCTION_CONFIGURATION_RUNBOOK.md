# قرارداد کانفیگ Production

این سند مرجع تنظیم‌های سخت‌سازی PH است؛ اجرای IIS، providerهای موجود و rollout در
[راهنمای canonical IIS](../production/DEPLOYMENT_IIS.md) و بازیابی در
[Backup/Restore](../production/BACKUP_RESTORE.md) باقی می‌مانند.
فایل نمونه `MakanApp.Api/appsettings.Production.json.example` **secret ندارد و آمادهٔ اجرا نیست**.
هیچ کانفیگ یا secret واقعی سرور در این مرحله خوانده، چاپ یا تغییر داده نشده است.

## Data Protection — PH-1

| تنظیم | معنا و قاعده |
|---|---|
| `Security:DataProtection:ApplicationName` | نام پایدار؛ پیش‌فرض سازگار دقیقاً `MakanApp.BackofficeMfa.v1` است. تغییر آن عملیات deploy عادی نیست. |
| `Security:DataProtection:KeyRingPath` | مسیر مطلق خارجی؛ جدا از publish و Storage. در host عملیاتی Production/Staging لازم است. نمونهٔ غیرمحرمانه: `C:\MakanApp\DataProtectionKeys`. |
| `Security:DataProtection:CertificatePath` | مسیر مطلق PFX دارای private key برای محافظت قابل‌بازیابی، به‌ویژه Linux/انتقال بین ماشین‌ها؛ خارج از publish و Git نگهداری شود. |
| `Security:DataProtection:CertificatePassword` | فقط secret store/environment محیط مجاز؛ چاپ در دستور، log، history ترمینال یا Git ممنوع. |

متغیرهای environment از `__` استفاده می‌کنند؛ مثلاً
`Security__DataProtection__KeyRingPath`. برای path/PFX/password، کلیدهای قدیمی
`BackofficeSecurity:*` همچنان معتبرند. اگر هر دو خانواده مقدار داشته باشند،
مقادیر باید یکسان باشند؛ تعارض به‌جای انتخاب پنهانی ring جدید، با پیام امن رد می‌شود.
مسیرها پیش از مقایسه normalize می‌شوند. تنظیم قبلی `web.config` معتبر است؛
صرف افزودن این قرارداد دلیل جابه‌جایی ring موجود نیست.

`BackofficeMfaConfiguration` همان provider سراسری را ثبت می‌کند؛ provider MFA موازی
ساخته نشده و purpose فعلی `MakanApp.PlatformMfa.v1.<userIdN>` حفظ شده است.
`DataProtectionKeyRingStartupValidator` پیش از شروع hosted workerها،
`DataProtectionKeyRingProbe` را اجرا می‌کند: دسترسی فایل و protect/unprotect واقعی.
مسیر صریح نامعتبر یا ring عملیاتی ناموجود/غیرقابل‌استفاده، startup را با خطای ثابت
متوقف می‌کند؛ path/password/inner exception در خطای برنامه قرار نمی‌گیرند.
Development/Testing بدون ring عملیاتی همچنان قابل اجرا هستند؛ این استثنا مجوز استفاده
از محیط Development روی سرور نیست.

probe جاری نباید پوشهٔ حذف‌شده را بازسازی یا گم‌شدن XML را با کلید cacheشده پنهان کند.
کلیدهای قدیمی را هنگام publish حذف نکنید؛ rotation معمول Data Protection، مجوز حذف
کلیدهای تاریخی نیست. تست recreation provider معادل اثبات reboot واقعی سرور نیست.

### انتخاب محافظ و بازیابی

- filesystem محل persistence است، نه رمزگذاری در حالت سکون؛ ACL لازم است.
- مسیر Windows بدون certificate، برای سازگاری از **DPAPI کاربر جاری** استفاده می‌کند؛
  نه DPAPI LocalMachine. identity/Profile ثابت IIS لازم است. XML به‌تنهایی روی ماشین
  یا identity جدید کافی نیست؛ از قابلیت مهاجرت خودکار ادعا نمی‌شود.
- محافظ PFX موجود از private key گواهی استفاده می‌کند؛ backup امن private key، رمز،
  ring و ACL مقصد و rehearsal بازیابی لازم‌اند. PFX عملیاتی از Git و artifact عمومی خارج است.
  چون adapter موجود PFX قابل‌بازیابی را پشتیبانی می‌کند، مسیر موازی certificate store/thumbprint
  در این فاز اضافه نشده است؛ هیچ تغییر پنهان به محافظ ring قبلی انجام نمی‌شود.
- افزودن PFX جدید، XML قدیمی DPAPI را خودکار قابل‌حمل نمی‌کند. مهاجرت محافظ نیازمند
  دسترسی به محافظ قبلی و برنامهٔ custody/recovery مستقل است.
- MFA بدون کلید قبلی با «ساخت کلید تازه» بازیابی نمی‌شود؛ reset/re-enrollment فقط از
  workflow مصوب و audited خود MFA مجاز است، نه دست‌کاری مستقیم SQL.

## مدیریت secret

SQL credential/Connection String، `Identity:SecurityKey`، توکن OTP ملی‌پیامک،
کلید سرویس AI، credential Push و رمز PFX تنها در محیط مجاز تأمین می‌شوند.
نام تنظیم، نه مقدار واقعی، باید در ticket/runbook ثبت شود.
environment هم‌نام بر JSON اولویت دارد؛ مقدار قدیمی IIS می‌تواند مقدار فایل را override کند.
فایل خصوصی `appsettings.Production.json` ignored است؛ در clone تازه باید جداگانه provision شود.
artifact اپراتوریِ شامل این فایل محرمانه است و نباید در لینک عمومی یا Git قرار گیرد.

کلید Identity و ring را در deploy عادی نچرخانید. stable بودن لازم است، ولی کافی برای
امنیت نیست: دسترسی کم‌اختیار، custody، backup، rotation مصوب و آزمایش بازیابی لازم‌اند.
ممیزی tracked config فعلی جای اسکن و remediation تاریخچهٔ قدیمی یا rotation secret افشاشده
را نمی‌گیرد؛ history در این کار بازنویسی نمی‌شود.

## rollout و gateهای مستقل

1. commit مورد انتظار و backup سازگار DB/Storage/ring را ثبت کنید؛ محتوا/secret چاپ نشود.
2. مسیر و محافظ قبلی را از تنظیم مجاز سرور حفظ کنید؛ نمونهٔ جدید را روی فایل خصوصی overwrite نکنید.
3. external directory/ACL را پیش از start با identity واقعی AppPool آماده کنید.
4. در صورت fail شدن startup، config/ACL/custody را اصلاح کنید؛ validator را خاموش نکنید.
5. `/health/live` و `/health/ready` و سپس MFA موجود را از مسیر مجاز بررسی کنید.
6. restart/recycle/reboot و انتقال ماشین، gateهای اپراتوری جدا هستند؛ روی این رایانه اجرا نشده‌اند.

استثنای مصوب HTTP و اعتماد به certificate SQL در انتشار قبلی حفظ می‌شوند؛ این سند
آن‌ها را محیط امن اعلام نمی‌کند. TLS و اعتماد معتبر SQL همچنان gate مستقل Production هستند.
Startup migration و `EnsureCreated` اضافه نشده‌اند؛ migration تنها با principal و تأیید اپراتور.
