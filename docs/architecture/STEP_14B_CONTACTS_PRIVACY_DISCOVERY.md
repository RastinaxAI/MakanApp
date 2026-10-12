# STEP 14B — Contacts، Profile Privacy، Discovery و Global Personal Direct

این سند قرارداد backend پیاده‌سازی‌شده در STEP 14B را ثبت می‌کند. تصمیم‌های پایه از
`MESSENGER_V2_PRODUCT_DECISIONS.md` گرفته شده‌اند و این مرحله فقط محدوده Contacts،
Privacy، Discovery و Personal Direct را پوشش می‌دهد.

## 1. دامنه مرحله

قابلیت‌های زیر اضافه شدند:

- دفترچه مخاطبان خصوصی هر کاربر با نام ذخیره‌شده و Favorite؛
- فهرست Recent بر اساس فعالیت واقعی Personal Direct؛
- جست‌وجوی محدود و صفحه‌بندی‌شده بر اساس Username یا DisplayName؛
- resolve دقیق Username یا Phone Number با رعایت Privacy؛
- پروفایل عمومی حداقلی و بدون افشای فیلدهای حساس؛
- تنظیم Privacy برای Direct، نمایش شماره، کشف با شماره و دعوت گروه شخصی؛
- Global Personal Direct بدون نیاز به Organization، Adult بودن یا
  `PersonalCommunicationGrant`؛
- حفظ Block به‌عنوان منع مستقل و عمومی ارتباط.

Contact sync، Frequent contacts، Avatar، Bio، Presence، کارت Contact داخل پیام، SMS
invite و قابلیت‌های STEP 14C تا 14E عمداً خارج از این مرحله‌اند.

## 2. مدل دامنه

### `UserContact`

هر رکورد متعلق به `OwnerUserId` است و فقط همان مالک می‌تواند آن را مشاهده یا تغییر
دهد. `ContactUserId` هویت حساب مقصد است؛ شماره تلفن فقط credential کشف است و هویت
Contact محسوب نمی‌شود. `SavedName` حداکثر ۲۰۰ کاراکتر و فقط برای مالک قابل مشاهده
است. `IsFavorite`، زمان‌های UTC و `RowVersion` نیز نگهداری می‌شوند.

Invariantها:

- ذخیره خود کاربر مجاز نیست؛
- برای هر `(OwnerUserId, ContactUserId)` فقط یک رکورد وجود دارد؛
- حذف Contact هیچ Conversation، Message یا Block را حذف نمی‌کند؛
- Contact یک‌طرفه است: ذخیره‌کردن B توسط A، A را Contact متعلق به B نمی‌کند.

### `MessagingPrivacySettings`

هر کاربر حداکثر یک ردیف Privacy دارد. نبود ردیف به معنی اعمال defaults دامنه است و
باعث seed شدن رکورد برای همه کاربران نمی‌شود.

| تنظیم | مقدار پیش‌فرض |
|---|---|
| Direct Messages | `Everyone` |
| Phone Number Visibility | `Nobody` |
| Phone Number Discovery | `Nobody` |
| Personal Group Invites | `Contacts` |

Audienceهای معتبر `Everyone`، `Contacts` و `Nobody` هستند. در حالت `Contacts`،
تصمیم بر اساس دفترچه مخاطبان مالک تنظیم گرفته می‌شود: اگر مقصد، درخواست‌کننده را
ذخیره کرده باشد دسترسی برقرار است. این semantics آگاهانه یک‌طرفه است.

## 3. مدل داده SQL Server

Migration: `20261012203821_AddMessengerContactsPrivacyDiscovery`

### `messaging.UserContacts`

- PK: `Id` از نوع `uniqueidentifier`
- FKهای `OwnerUserId` و `ContactUserId` به `identity.Users` با delete behavior برابر
  `Restrict`
- `SavedName nvarchar(200) NULL`
- `IsFavorite bit NOT NULL`
- `CreatedAtUtc` و `UpdatedAtUtc` از نوع `datetime2(7)`
- `RowVersion rowversion NOT NULL`
- unique index روی `(OwnerUserId, ContactUserId)`
- indexهای `(OwnerUserId, IsFavorite)` و `(OwnerUserId, UpdatedAtUtc)`
- check constraint برای متفاوت‌بودن Owner و Contact

### `messaging.MessagingPrivacySettings`

- PK/FK مشترک: `UserId` به `identity.Users` با delete behavior برابر `Cascade`
- چهار ستون Audience از نوع `int` با check constraint محدود به `1, 2, 3`
- `CreatedAtUtc` و `UpdatedAtUtc` از نوع `datetime2(7)`
- `RowVersion rowversion NOT NULL`

هیچ SQL default یا seed برای Privacy ایجاد نشده است. Migration ساخته و inspect شده،
اما در این مرحله روی هیچ دیتابیس عادی/production اجرا نشده است.

## 4. سیاست Privacy و Safety

ترتیب مؤثر تصمیم برای Personal Direct چنین است:

1. حساب و پروفایل مقصد باید قابل استفاده باشند؛
2. Block دوطرفه مؤثر بررسی می‌شود؛
3. Privacy فعلی مقصد بررسی می‌شود؛
4. Conversation موجود resolve یا Conversation جدید ایجاد می‌شود.

همین سیاست هنگام ارسال پیام دوباره ارزیابی می‌شود؛ در نتیجه ایجاد قبلی Conversation
مجوز دائمی ایجاد نمی‌کند. تغییر Privacy یا Block، ارسال جدید را متوقف می‌کند ولی
تاریخچه committed را پاک نمی‌کند. پاسخ منع از کد عمومی
`DIRECT_RECIPIENT_NOT_AVAILABLE` استفاده می‌کند تا علت Block، Privacy یا نبود مقصد
افشا نشود.

`PersonalCommunicationGrant` برای سازگاری migration/history در مدل قدیمی باقی مانده
است، اما دیگر ورودی authorization مربوط به Personal Direct نیست. قواعد سنی نیز از
Direct شخصی حذف شده‌اند؛ سیاست ایمنی حساب کودک باید پیش از release عمومی در یک مرحله
مستقل نهایی شود.

Organization Direct بدون تغییر باقی مانده و همچنان از Membership، Role، Enrollment،
Teacher Assignment و Guardian Relation استفاده می‌کند.

برای دعوت عضو به Personal Group، `GroupInvitesAudience` مقصد اعمال می‌شود. قواعد
گروه سازمانی تغییری نکرده‌اند و Block به‌تنهایی عضویت Group موجود را بازنویسی نمی‌کند.

## 5. Discovery و جلوگیری از enumeration

- `GET /api/v1/people/search` فقط query معنادار و صفحه محدود را می‌پذیرد؛ جست‌وجوی
  خالی یا بیش از حد مجاز رد می‌شود.
- Phone Number هرگز در URL قرار نمی‌گیرد؛ resolve شماره فقط با body در
  `POST /api/v1/people/resolve` انجام می‌شود.
- شماره با `PhoneNumberNormalizer` نرمال و فقط به‌صورت exact match جست‌وجو می‌شود؛
  partial phone search وجود ندارد.
- شماره مخفی و شماره ناموجود هر دو پاسخ امن و یکسان
  `{ "isDiscoverable": false, "profile": null }` می‌دهند.
- `PhoneNumberDiscoveryAudience` امکان پیدا کردن حساب با شماره را کنترل می‌کند و
  `PhoneNumberVisibilityAudience` نمایش شماره در DTO را؛ این دو مستقل‌اند.
- همه endpointهای People احراز هویت می‌خواهند و policy rate limit
  `messaging-discovery` دارند: در محیط عادی ۶۰ درخواست در دقیقه به ازای IP و در
  Testing سقف بالاتر برای اجرای تست‌ها.
- DTO عمومی فقط `UserId`، `Username`، `DisplayName`، شماره مجازشده، وضعیت Contact،
  Favorite و `CanStartDirect` را برمی‌گرداند. Phone پنهان، membership، role، age،
  grant و جزئیات Privacy بازگردانده نمی‌شوند.

هیچ cache برای نتیجه Phone Discovery استفاده نشده است؛ بنابراین تغییر Privacy در
درخواست بعدی مؤثر است. pipeline عادی نیز body شماره را log نمی‌کند و تست integration
عدم ثبت شماره خام را بررسی می‌کند.

## 6. قرارداد Endpointها

همه مسیرها زیر `/api/v1` و نیازمند session معتبر هستند.

| Method | Route | رفتار اصلی |
|---|---|---|
| `GET` | `/contacts?q=&favoritesOnly=&page=&pageSize=` | فهرست خصوصی Contacts با filter و pagination |
| `GET` | `/contacts/recent?page=&pageSize=` | مخاطبان اخیر Personal Direct بر اساس آخرین فعالیت |
| `POST` | `/contacts` | ذخیره idempotent یک Contact؛ جدید `201` و موجود `200` |
| `PATCH` | `/contacts/{contactUserId}` | تغییر SavedName/Favorite با `expectedVersion` |
| `DELETE` | `/contacts/{contactUserId}` | حذف فقط Contact و پاسخ `204` |
| `GET` | `/people/search?q=&page=&pageSize=` | جست‌وجوی Username/DisplayName با projection امن |
| `POST` | `/people/resolve` | resolve دقیق Username یا Phone Number |
| `GET` | `/people/{userId}` | پروفایل عمومی حداقلی |
| `GET` | `/messaging/privacy` | settings صریح یا defaults مؤثر |
| `PUT` | `/messaging/privacy` | ایجاد/به‌روزرسانی settings با optimistic concurrency |

نمونه ذخیره Contact:

```json
{
  "contactUserId": "00000000-0000-0000-0000-000000000000",
  "savedName": "نام خصوصی من"
}
```

نمونه resolve شماره:

```json
{
  "username": null,
  "phoneNumber": "09120000000"
}
```

نمونه تغییر Privacy:

```json
{
  "directMessagesAudience": "Contacts",
  "phoneNumberVisibilityAudience": "Nobody",
  "phoneNumberDiscoveryAudience": "Contacts",
  "groupInvitesAudience": "Contacts",
  "expectedVersion": "<Base64 rowversion یا null برای اولین ثبت>"
}
```

صفحه پیش‌فرض ۲۰ و سقف صفحه ۵۰ است. query جست‌وجوی People حداقل ۲ و حداکثر ۱۰۰
کاراکتر دارد. conflict نسخه با HTTP `412` گزارش می‌شود.

## 7. جریان اجرا و لایه‌ها

```text
HTTP Request
-> ContactsController / PeopleController / MessagingPrivacyController
-> MessagingDirectoryService
-> MessagingPrivacyPolicy و قواعد Domain
-> IMessagingDirectoryStore / IMessagingPrivacyResolver
-> EfMessagingDirectoryStore / EfMessagingPrivacyResolver
-> MakanDbContext
-> SQL Server
-> Application Result DTO
-> HTTP Response
```

Domain فقط entity و enum و invariantها را دارد. Application قراردادها، use case،
validation و policy را مالک است. Infrastructure query، transaction، EF configuration
و SQL Server concurrency را پیاده می‌کند. Api فقط transport، authentication، rate
limit و mapping خطا را نگه می‌دارد.

## 8. همزمانی و تراکنش

- Contact uniqueness علاوه بر check کاربردی با unique index SQL Server تضمین شده است.
- first-save همزمان داخل transaction با isolation `Serializable` انجام می‌شود و
  constraint نهایی پایگاه داده مرجع صحت است.
- update Contact و Privacy از `rowversion` و `expectedVersion` استفاده می‌کنند.
- اولین PUT همزمان Privacy فقط یک ردیف می‌سازد و درخواست رقیب conflict می‌گیرد.
- queryهای list/search/recent با projection و pagination در SQL اجرا می‌شوند و
  collection کامل در حافظه materialize نمی‌شود.

## 9. UX stateهای مورد انتظار

کلاینت باید برای Contacts، Recent و People Search حالت‌های loading، empty، error و
pagination را مستقل نشان دهد. `isDiscoverable=false` به معنی «نتیجه قابل نمایش نیست»
است و نباید بین hidden و nonexistent تفاوت UX یا پیام ایجاد کند. مقدار
`canStartDirect=false` باید اقدام شروع Direct را غیرفعال کند، بدون نمایش علت داخلی.
Conflict نسخه نیازمند دریافت مجدد settings/contact و بازبینی تغییر است.

## 10. موارد باقیمانده

- child/minor safety policy و release gate؛
- Contact sync و revoke مستقل از Conversation؛
- Frequent contact ranking؛
- Avatar، Bio و Presence؛
- Contact card message و SMS invite؛
- قابلیت‌های list/group/history و Saved/Archive در STEP 14C؛
- moderation در STEP 14D و hardening تکمیلی در STEP 14E.
