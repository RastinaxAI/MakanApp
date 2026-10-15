# STEP 12B — Notification Preferences + Delivery Channels Foundation

<!-- DOC-SYNC:BEGIN -->
> راهنمای خواندن، بازبینی `2026-10-15` از `main@80d6a14`: این فایل قرارداد/گزارش مرحله‌ای است. تصمیم‌های دامنه و اعداد validation/commit داخل آن متعلق به همان scope و تاریخ‌اند و با این به‌روزرسانی بازنویسی نشده‌اند. برای کاتالوگ و شمارش جاری از [README مخزن](../../README.md) و برای gateهای سرور از [آمادگی جاری](../production/PRODUCTION_READINESS.md) استفاده کنید؛ وجود کد یا تست، اثبات استقرار/ارسال واقعی provider نیست.
<!-- DOC-SYNC:END -->

این سند وضعیت واقعی پیاده‌سازی STEP 12B را ثبت می‌کند. اعلان داخلی STEP 12A همچنان منبع حقیقت کاربر است و شکست کانال خارجی آن را حذف یا نامعتبر نمی‌کند.

## جریان معماری

```text
Business transaction
-> platform.OutboxMessages
-> OutboxProcessor
-> notification.Notifications
-> NotificationDeliveryCoordinator
-> notification.NotificationDeliveryAttempts
-> NotificationDeliveryDispatcher
-> provider-neutral Push/SMS port
```

ساخت `Notification` و تمام `NotificationDeliveryAttempt`های آن در یک transaction انجام می‌شود. فراخوانی provider بعد از commit و خارج از transaction دیتابیس است.

## Preference و scope

هر preference با این کلید منطقی شناخته می‌شود:

```text
UserId + OrganizationId(nullable) + NotificationPreferenceCategory
```

- `OrganizationId = null`: فضای شخصی
- `OrganizationId != null`: فضای یک سازمان مشخص
- API برای scope سازمانی، عضویت فعال همان کاربر را دوباره بررسی می‌کند.
- `UserId` از session احراز‌شده می‌آید و در request وجود ندارد.
- نبودن ردیف به معنی استفاده از default مرکزی است؛ برای همه کاربران seed انبوه ساخته نمی‌شود.

دسته‌های allowlist:

```text
Messages, Assignments, Results, Sessions, Reports, Invitations, AccountSecurity
```

رخدادهای فعلی STEP 12A فقط به `Assignments` و `Results` map می‌شوند. دسته‌های دیگر foundation آینده‌اند و producer جدیدی در این مرحله ندارند.

Default کانال اختیاری `Push=true` و `Sms=false` است. In-app قابل خاموش‌کردن نیست، چون خود رکورد `Notification` منبع حقیقت durable است. `AccountSecurity` mandatory است و API اجازه خاموش‌کردن Push/SMS آن را نمی‌دهد؛ در حال حاضر producer امنیتی تازه‌ای اضافه نشده است.

## Quiet Hours و timezone

مدل فعلی timezone معتبر در سطح User یا Organization نداشت؛ timezoneهای موجود فقط متعلق به Session آموزشی بودند. بنابراین اجرای Quiet Hours فقط وقتی ممکن است که خود کاربر روی همان preference این سه مقدار را صریح ارائه کند:

```text
QuietHoursStartLocal
QuietHoursEndLocal
TimeZoneId
```

`TimeZoneId` باید توسط `TimeZoneInfo` روی host معتبر باشد. شروع بازه inclusive و پایان exclusive است. بازه عبوری از نیمه‌شب مانند `22:00 -> 07:00` پشتیبانی می‌شود. Quiet Hours فقط external delivery را `Deferred` می‌کند؛ اعلان داخلی و unread count بلافاصله موجودند. policy اجباری `AccountSecurity` از Quiet Hours عبور می‌کند.

Daily Report scheduling در این مرحله ساخته نشده است.

## Push device

`PushDeviceRegistration` از `UserSession` جدا است و شامل platform، token، هش SHA-256 برای تشخیص token فعال تکراری، زمان‌ها و `RevokedAtUtc` است. ارتباط ترکیبی `(UserSessionId, UserId)` جلوی اتصال دستگاه به نشست کاربر دیگر را در دیتابیس می‌گیرد.

Push token داده حساس است:

- در response API یا log برگردانده نمی‌شود.
- delivery attempt فقط `PushDeviceRegistration.Id` را نگه می‌دارد، نه token.
- token کامل فقط در رجیستری دستگاه برای adapter آینده نگهداری می‌شود؛ حفاظت SQL Server/backup/disk و دسترسی حداقلی در محیط مقصد الزامی است.
- device لغوشده یا device متصل به session منقضی/لغوشده برای delivery آینده resolve نمی‌شود.

هیچ provider واقعی FCM/APNs/Expo/OneSignal انتخاب نشده است. فقط `DevelopmentPushNotificationProvider` و `UnavailablePushNotificationProvider` وجود دارند.

## SMS عمومی و جداسازی OTP

`INotificationSmsProvider` مستقل از `IOtpDeliveryProvider` است. کد عمومی اعلان از `MeliPayamakOtpProvider` و مسیر `/api/send/otp/{service-token}` استفاده نمی‌کند.

SMS عمومی:

- مقصد را از `identity.UserCredentials` تأییدشده resolve می‌کند.
- شماره تلفن از frontend دریافت نمی‌شود.
- delivery attempt فقط `UserCredential.Id` را نگه می‌دارد.
- قرارداد provider بر پایه template key و پارامتر امن است، نه body دلخواه کاربر.
- adapter واقعی ملی‌پیامک تا دریافت قرارداد رسمی general SMS پیاده نشده است.

## Privacy و deep link

Push preview از متن داخلی Notification کپی نمی‌شود. policy فعلی متن عمومی آموزشی می‌سازد و score، answer، private note و مشخصات حساس فرزند را وارد lock screen نمی‌کند. SMS نیز فقط template key بسته دریافت می‌کند.

Deep link فقط به `notificationId` اشاره می‌کند:

```text
makan://notifications/{notificationId}
```

این لینک authorization نیست. endpoint منبع باید Membership، Enrollment یا GuardianRelation جاری را دوباره بررسی کند.

## Delivery attempt و retry

Lifecycle:

```text
Pending -> Processing -> Delivered
Pending/Deferred -> Processing -> Deferred -> ... -> Failed
Pending/Deferred -> Processing -> Suppressed
```

علت‌های مستقل شامل `SuppressedByPreference`، `NoDevice`، `NoSmsDestination`، `OrganizationAccessRevoked`، `DeviceRevoked`، `ProviderUnavailable`، `ProviderRejected` و `ProviderTimeout` هستند.

- worker با `UPDLOCK, READPAST, ROWLOCK` و lease منقضی claim می‌کند.
- batch پیش‌فرض ۵۰ و حداکثر تلاش ۳ است.
- backoff درجه‌دو بین ۵ تا ۳۰۰ ثانیه محدود می‌شود.
- timeout پیش‌فرض provider ده ثانیه است.
- failure دائمی و suppression retry نمی‌شوند.
- unique index روی `(NotificationId, Channel, DestinationReference)` effect تکراری worker را محدود می‌کند.
- `DeliveryAttemptId` شناسه ثابت idempotency برای adapter آینده است؛ شبکه دقیقاً-once تضمین نمی‌کند.

## API

| Method | Route | توضیح |
|---|---|---|
| `GET` | `/api/v1/notification-preferences?organizationId=` | effective settings همه دسته‌ها، default/explicit و mandatory indicators |
| `PUT` | `/api/v1/notification-preferences/{category}?organizationId=` | ساخت/تغییر تنظیم همان کاربر با rowversion |
| `POST` | `/api/v1/notifications/devices` | ثبت یا refresh idempotent token برای session جاری |
| `DELETE` | `/api/v1/notifications/devices/{registrationId}` | revoke دستگاه متعلق به کاربر جاری |

همه endpointها authenticated هستند. device متعلق به کاربر دیگر و scope غیرقابل دسترس با 404 پنهان می‌شوند. conflict هم‌زمانی preference برابر 409 است.

## SQL Server

Migration:

```text
20261012163226_AddNotificationPreferencesAndDelivery
```

جدول‌ها:

- `notification.NotificationPreferences`
- `notification.PushDeviceRegistrations`
- `notification.NotificationDeliveryAttempts`

هر سه `rowversion` دارند. filtered unique indexهای جداگانه برای personal و organization preference مانع duplicate scope می‌شوند. token فعال unique filtered index دارد و pending delivery index برای worker ساخته شده است. FKها به User، Organization، UserSession و Notification برقرارند.

Migration هنگام startup خودکار اجرا نمی‌شود و در این مرحله روی دیتابیس عادی/Production اعمال نشده است.

## Configuration

```text
Notifications:PushProvider = Development | Unavailable
Notifications:SmsProvider = Development | Unavailable
Notifications:Delivery:BatchSize
Notifications:Delivery:ClaimLeaseSeconds
Notifications:Delivery:IdlePollingSeconds
Notifications:Delivery:InitialRetryDelaySeconds
Notifications:Delivery:MaximumRetryDelaySeconds
Notifications:Delivery:ProviderTimeoutSeconds
Notifications:Delivery:MaximumAttempts
```

Development provider فقط در محیط `Development` یا `Testing` مجاز است. هیچ credential یا endpoint شبکه‌ای جدیدی وجود ندارد.

## موارد خارج از scope

- provider واقعی Push یا general SMS
- Email
- campaign/broadcast/marketing
- Backoffice و provider dashboard
- notification analytics
- AI و Daily Brief generation
- تغییر قرارداد OTP ملی‌پیامک
- endpoint عمومی جزئیات provider/retry
