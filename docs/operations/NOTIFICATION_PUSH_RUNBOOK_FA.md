# راهنمای عملیاتی Notification / Web Push / FCM — BE-16

این راهنما برای زیرساخت موجود Backend است. Test و Production باید credential، پروژه/Device و تنظیم خصوصی جدا داشته باشند. Config واقعی، SQL عادی، IIS و سرور در BE-16 تغییر نکردند. دریافت واقعی Push در این مرحله اجرا نشده است.

## موجودی امن محیط بررسی‌شده

| مورد | Test | Production |
|---|---|---|
| `Notifications:PushProvider` | `Unavailable` | `Unavailable` |
| VAPID/FCM کامل | خیر | خیر |
| `Notifications:SmsProvider` | `Unavailable` | `Unavailable` |
| Live WebPush/FCM | `NOT_RUN` | `NOT_RUN` |

این جدول مربوط به فایل‌های خصوصی محلی بررسی‌شده است؛ تنظیم واقعی IIS/Container/Server فقط در همان محیط قابل تأیید است. credential یا وضعیت سرور از روی فایل Source حدس زده نمی‌شود. پیامک OTP ملی‌پیامک از Notification SMS عمومی جداست؛ `Sms:Provider` و کد/Secret آن تغییر نکرده‌اند.

## انتخاب Provider

`Notifications:PushProvider` سه حالت دارد:

| مقدار | نتیجه |
|---|---|
| `Unavailable` | In-app برقرار؛ ارسال بیرونی Suppressed با `ProviderUnavailable`؛ configuration عمومی وب false/null |
| `Development` | فقط Development/Testing مجاز؛ adapter آزمایشی receipt ساختگیِ توسعه می‌دهد و اثبات Push واقعی نیست |
| `WebPushAndFcm` | `MessengerPushNotificationProvider` براساس Platform، Web را به WebPush و Android/iOS را به FCM می‌فرستد |

در adapter واقعی، `Availability` برحسب کامل‌بودن تنظیمات `Configured`، `PartiallyConfigured` یا `Unavailable` است. این property تست دریافت دستگاه نیست و endpoint سلامت جدیدی برای آن ساخته نشده است. تنظیم ناقص provider با `ProviderConfigurationInvalid` terminal می‌شود؛ نبود تنظیم با `ProviderUnavailable` suppress می‌شود. برای فعال‌کردن Provider، مقدار معتبر واقعی باید در secret store/config خصوصی همان محیط ثبت و host دوباره بارگذاری شود؛ hot reload این singletonها تضمین نشده است.

## Web Push

کلیدهای موجود:

```text
Notifications:PushProvider
Notifications:WebPush:VapidSubject
Notifications:WebPush:VapidPublicKey
Notifications:WebPush:VapidPrivateKey
Notifications:WebPush:TimeToLiveSeconds
Notifications:WebPush:AllowedEndpointHosts
```

`VapidSubject` باید mailto معتبر یا HTTPS معتبر باشد. PublicKey، نقطهٔ uncompressed P-256 با65 بایت و PrivateKey اسکالر32 بایتی، هر دو base64url و از یک جفت معتبرند. TTL موجود بین0 و86400 ثانیه است؛ default3600. PrivateKey وارد Git، گزارش، API یا Log نمی‌شود؛ API فقط PublicKey معتبر را برمی‌گرداند. Rotation بدون برنامه ممکن است subscriptionهای کلاینت را نیازمند ثبت دوباره کند.

`AllowedEndpointHosts` در Configuration به `PushDeviceOptions.AllowedWebPushHosts` نگاشت می‌شود و آرایهٔ host دقیق و مجازِ push service است؛ URL کامل، wildcard یا مقصد دلخواه کاربر وارد آن نشود. Backend endpoint HTTPS روی443، بدون userinfo/fragment و با keys معتبر را می‌پذیرد. `WebPushNetworkPolicy` DNS و IP عمومی را بررسی و همان IP تأییدشده را به socket متصل می‌کند؛ redirect/proxy خودکار و شبکهٔ خصوصی/loopback مجاز نیستند. برای رفع مشکل provider، این guards را خاموش نکنید. Allowlist باید با subscription واقعی مرورگر مجاز هماهنگ باشد؛ host نمونه‌ای را صرفاً برای PASS کپی نکنید.

وب به Service Worker، Permission کاربر و subscription واقعی احتیاج دارد. Backend این فایل‌های Frontend را نمی‌سازد. `GET /api/v1/notifications/devices/web-push/configuration` باید با نشست معتبر بررسی شود؛ false/null یعنی ابتدا Configuration را حل کنید، نه اینکه کلید جعلی بسازید.

## FCM

```text
Notifications:Fcm:ProjectId
Notifications:Fcm:ServiceAccountPath
Notifications:Fcm:TimeToLiveSeconds
GOOGLE_APPLICATION_CREDENTIALS
```

`ServiceAccountPath` اولویت دارد و fallback همان `GOOGLE_APPLICATION_CREDENTIALS` است؛ مسیر باید absolute و فایل خارج از Git/webroot/بستهٔ عمومی، فقط برای هویت سرویس قابل‌خواندن باشد. PrivateKey یا JSON واقعی را در appsettings اشتراکی یا Runbook نگذارید. ServiceAccount باید `type=service_account` و project_id مطابق ProjectId داشته باشد؛ token_uri فقط Google OAuth ثابت است. فایل بزرگ‌تر از16384 بایت و قالب/پروژهٔ ناسازگار رد می‌شود.

OAuth scope موجود `https://www.googleapis.com/auth/firebase.messaging` است. SDK رسمی cache/refresh توکن را مدیریت می‌کند؛ raw token نباید ثبت شود. دسترسی لازم همان پروژه و FCM HTTP v1 باید در محیط واقعی آماده باشد. Android/iOS باید token متعلق به همان پروژه و تنظیم OS صحیح داشته باشند؛ بدون device واقعی، موفقیت adapter کنترل‌شده را دریافت نهایی اعلام نکنید.

## SMS اعلان عمومی

`Notifications:SmsProvider` فعلاً فقط Development مجازِ توسعه یا Unavailable را پشتیبانی می‌کند؛ قرارداد Provider عمومی Production هنوز جداست. OTP ملی‌پیامک را به این adapter وصل نکنید و OTP/token را در پیام اعلان عمومی نگذارید. وضعیت فعلی `PRODUCTION_NOTIFICATION_SMS=BLOCKED_CONFIGURATION` است، نه خرابی Provider ورود.

## Health، Readiness و مشاهده‌پذیری

- `/health` و `/health/live` liveness موجود هستند. `/health/ready` تنظیمات Production، SQL و Storage اصلی را می‌سنجد. Readiness سالم اثبات credential یا دریافت Push نیست؛ unavailable بودن کانال اختیاری نباید SQL/Storage را خراب گزارش کند.
- Workerها `MakanApp.Workers` Meter موجود را استفاده می‌کنند: `makan.worker.batches`, `makan.worker.items`, `makan.worker.failures`, `makan.worker.outcomes`, `makan.worker.duration`, `makan.queue.items`, `makan.queue.oldest_age` و snapshot availability/age. نام Worker تحویل `NotificationDelivery` است؛ جزئیات Metric contract از ابزار Observability موجود خوانده شود.
- برای عیب‌یابی فقط ID اعلان/attempt، status، failure reason، attempt count، timestamps و correlation/trace مجاز را نگه دارید. body پیام/Support/نمره، push token، endpoint subscription، OTP، Authorization، credential، private key و response body Provider را ثبت نکنید.
- خطاهای loop با کدهای امن `NOTIFICATION_DELIVERY_SCHEMA_UNAVAILABLE` یا `NOTIFICATION_DELIVERY_LOOP_FAILED` در Error ثبت می‌شوند. خطای واقعی را با Success یا خاموش‌کردن تست پنهان نکنید.

## صف، Retry و Recovery

زنجیرهٔ پایدار: transaction تجاری + Outbox موجود → materialize یک Notification به‌ازای SourceEventId/Recipient → attemptهای Push/SMS → Claim/lease/fencing → ارزیابی مجدد مجوز و Preference → Provider خارج transaction → ذخیرهٔ نتیجه با token مالک Claim.

`Notifications:Delivery` گزینه‌های موجود `BatchSize`, `ClaimLease`, `IdlePollingInterval`, `InitialRetryDelay`, `MaximumRetryDelay`, `ProviderTimeout`, `MaximumAttempts` را دارد. defaultها به‌ترتیب50،30ثانیه،2ثانیه،5ثانیه،5دقیقه،10ثانیه و3 هستند. Lease باید از timeout بیشتر باشد. تغییر این مقادیر بدون بررسی recovery و ظرفیت انجام نشود؛ BE-16 مقدار تازه‌ای تنظیم نکرده است.

Transient، از جمله خطای شبکه/429/timeout، با Retry محدود و RetryAfter سازگار زمان‌بندی می‌شود؛ Permanent terminal است. Provider auto-retry موازی اضافه نشده است. Crash پس از claim با انقضای lease بازیابی می‌شود؛ نتیجهٔ worker قدیمی با fencing نمی‌تواند نتیجهٔ مالک جدید را بازنویسی کند. Crash پس از پذیرش شبکه و قبل از ذخیرهٔ receipt می‌تواند تحویل تکراری بسازد؛ exactly-once بیرونی ادعا نمی‌شود و tag/notificationId برای dedup کلاینت پایدار است.

همان SQL، Notificationهای pending، تاریخ آخرین تلاش، failure reason و age صف را با ابزار عملیات مجاز بررسی کنید؛ rowهای Claim/Status را دستی reset یا attempt موفق را برای replay به Pending برنگردانید. Quiet Hours ممکن است Deferred قانونی بسازد و retry خطای Provider نیست. `NoDevice`، `SuppressedByPreference`، `DeviceRevoked`، `OrganizationAccessRevoked` و `ResourceAccessRevoked` از خرابی Provider جدا هستند.

## Device و InvalidRegistration

ثبت به UserSession جاری تعلق دارد؛ session revoked/expired یا حساب frozen/deleted اجازهٔ ثبت/تحویل ندارد. حذف حساب BE-22 Deviceها را revoke و تلاش‌های بیرونی را suppress می‌کند؛ preference از حساب حذف‌شده به حساب تازه منتقل نمی‌شود.

وقتی Provider `InvalidRegistration` قطعی می‌دهد، lifecycle موجود registration همان نسخه را revoke می‌کند. نتیجهٔ قدیمی حق revokeکردن token تازهٔ همان Device را ندارد؛ rowversion مقایسه می‌شود. credential auth failure یا rate limit را به‌اشتباه invalid device تفسیر نکنید. پاک‌سازی خودسرانهٔ tokenهای واقعی یا ارسال آزمایشی به Device کاربر عادی انجام نشود.

## Migration

`20261114183321_ExtendProductNotificationCoverage` فقط دو CHECK مربوط به Type/ResourceType را گسترش می‌دهد؛ PK/FK/rowversion/unique dedup قبلی ثابت است. Upgrade رسیدها را تغییر نمی‌دهد. Down پس از وجود receipt یا envelope جدید، حتی پیش از materialization، با51000 متوقف می‌شود تا قرارداد دادهٔ صف از بین نرود؛ forward fix راه بازگشت امن است. Acceptance روی دو دیتابیس GUIDدار LocalDB انجام می‌شود؛ Migration روی `MakanApp`، `MakanApp_Test` یا سرور در این Task اجرا نشده است. اجرا روی هر محیط، مجوز و برنامهٔ انتشار مستقل لازم دارد.

## Smoke واقعیِ مجاز در مرحلهٔ بعد

1. فقط حساب و Device تست با رضایت مالک، محیط مشخص و credential خصوصی همان محیط آماده شود؛ migration/Deployment طبق روال مستقل انجام شود.
2. Configuration عمومی وب یا FCM project/file بدون چاپ secret بررسی شود؛ allowlist و outbound HTTPS/DNS صحیح باشند.
3. با نشست همان حساب، Device ثبت و یک رویداد تجاری مجاز ایجاد شود؛ IDهای امنِ event/notification/attempt ثبت شوند.
4. ابتدا receipt داخل برنامه، unread و mark-read تکراری بررسی شود؛ سپس رسید واقعی روی دستگاه، حالت foreground/background و Deep Link canonical بررسی شود.
5. Target با نشست جاری مجدداً resolve شود؛ مالک دیگر404 بگیرد. با قطع عضویت/Guardian یا revoke Device پیش از dispatch، Provider نباید تحویل جدید بگیرد.
6. سناریوی preference-off، quiet hours، token نامعتبر و retry فقط در محیط تست کنترل شود؛ token/محتوا یا خطای خام Provider وارد گزارش نشود.
7. نتیجهٔ واقعی Web و FCM جدا ثبت شود. تا اجرای این مراحل، `LIVE_WEB_PUSH=NOT_RUN` و `LIVE_FCM_PUSH=NOT_RUN` باقی می‌مانند.

قرارداد کلاینت: [Notification Handoff](../product-v6/NOTIFICATION_FRONTEND_HANDOFF_FA.md). ماتریس Producer/ACL: [Event Coverage](../product-v6/NOTIFICATION_EVENT_COVERAGE_FA.md).
