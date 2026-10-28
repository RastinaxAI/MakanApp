# پیاده‌سازی عکس پروفایل شخصی MakanApp

تاریخ: `2026-10-28`؛ شاخهٔ مبنا `main`؛ HEAD شروع: `2bc18af08e6789186bcb9fd17b5dcbd2b1b0ac86`.

این سند قابلیت جدید Working Tree را شرح می‌دهد؛ در این مأموریت Commit، Push، Publish، Deploy، Migration دیتابیس عادی/سرور یا فراخوانی واقعی AI انجام نمی‌شود. بسته‌های Release قبلی شامل این قابلیت جدید نیستند.

## ۱. قاعدهٔ محصول و مالکیت

Avatar متعلق به **User / Personal Profile** است. مرجع آن روی `identity.Users` قرار دارد؛ به Student، Teacher، Parent، Manager، Membership، OrganizationPerson یا Organization تعلق ندارد.

- هر User حداکثر یک مرجع فعال و اختیاری دارد.
- گرفتن/تغییر/پایان Role یا عضویت، مرجع عکس را پاک نمی‌کند.
- داشتن عکس، پروفایل را کامل یا سن را تأیید نمی‌کند.
- `DateOfBirth` و `VerifiedBirthDate` تغییر نمی‌کنند.
- Official Badge مفهوم جداست و در این مأموریت پیاده‌سازی نشده است.

این درخواست صریح مالک، پیشنهاد قبلی Avatar در سند مدل محصول V2 را برای همین قابلیت تأیید می‌کند؛ سایر پیشنهادهای آن سند مجوز پیاده‌سازی پیدا نمی‌کنند.

## ۲. نتیجهٔ Audit پیش از پیاده‌سازی

```text
CURRENT_USER_PROFILE_ENTITY=User + Person
CURRENT_FILE_ASSET_ENTITY=MakanApp.Domain.Storage.FileAsset
CURRENT_UPLOAD_ENDPOINT=POST /api/v1/files
CURRENT_DOWNLOAD_ENDPOINT=GET /api/v1/files/{fileId}/content
FILE_OWNER_MODEL=UploadedByUserId + optional OrganizationId
IMAGE_VALIDATION_EXISTS=true; MIME allowlist + extension + magic signature
PROFILE_PHOTO_ALREADY_EXISTS=false
```

نام‌های واقعی قراردادهای هویت:

| ناحیه | قرارداد موجود |
|---|---|
| Self | `CurrentUserResult` |
| هویت امن Messenger | `SafeMessagingIdentityResult` و `SafeMessagingIdentityRecord` |
| People | `MessagingPublicProfileResult` داخل Search/Resolve/Public Profile |
| Contacts | `UserContactResult.Profile` و `RecentContactResult.Profile` |
| Direct و inbox | `DirectConversationResult.OtherParticipant` و `ConversationSummaryResult.OtherParticipant` |
| اعضا | `ConversationParticipantResult.User` |
| فرستنده و mention | `ConversationMessageResult.Sender` و `Mentions` |
| Presence | `PresenceResult`؛ فقط UserId و وضعیت/زمان، بدون هویت نمایشی |

`ContactCardResult` یک کارت snapshot موجود است؛ در این تغییر برای هر پیام snapshot عکس ساخته نشده است. بخش‌های فاقد هویت نمایشی، مثل ActorUserId یا Presence، صرفاً برای افزودن Avatar بزرگ نشده‌اند.

## ۳. ادغام با FileAsset موجود

جریان واقعی:

```text
POST /api/v1/files (multipart)
→ FilesController
→ StorageService / FileUploadPolicy / FileSignatureValidator
→ IFileStorage / LocalFileStorage
→ FileAsset Ready
→ PUT /api/v1/me/profile-photo
→ ProfilePhotoService
→ User.ProfilePhotoFileAssetId
```

هیچ upload subsystem، مخزن فیزیکی یا Base64 در جدول User اضافه نشده است. مسیر فیزیکی و StorageKey فقط در زیرساخت Storage باقی می‌مانند.

فرمت‌های این نسخه **JPEG و PNG** هستند. زیرساخت فعلی WebP را در extension/signature validation پشتیبانی نمی‌کند؛ این فرمت در این مرحله اضافه نشده است. SVG بدون sanitizer امن پذیرفته نمی‌شود. فایل غیرتصویری، پسوند/امضای نامعتبر و MIME خارج allowlist موجود رد می‌شوند.

سقف حجم از `Storage:MaxFileSizeBytes` موجود می‌آید؛ مقدار پایهٔ فعلی ۱۰ MiB است و این قابلیت سقف جدیدی تعریف نکرده است. تنظیمات خصوصی محیط‌ها تغییر نکرده‌اند. در fixture تست، سقف کوچک موجود حفظ و JPEG فقط به allowlist میزبان اختصاصی تست Avatar افزوده شد.

## ۴. مدل داده و Migration

```text
identity.Users.ProfilePhotoFileAssetId uniqueidentifier NULL
    → storage.FileAssets.Id
```

Migration واقعی EF Core:

`20261028164838_AddUserProfilePhoto`

- افزودن ستون nullable، بدون default یا backfill.
- FK با رفتار `Restrict`؛ حذف فیزیکی FileAsset دارای مرجع ممنوع است.
- index یکتای filtered روی `ProfilePhotoFileAssetId IS NOT NULL`؛ یک فایل به‌صورت هم‌زمان عکس فعال چند User نمی‌شود.
- PK و rowversion موجود User/FileAsset حفظ شده‌اند.
- هیچ ستون DOB، شاهد سن، OrganizationPerson یا مالکیت Messaging تغییر نکرده است.

کنترل مالکیت در use case اجرا می‌شود؛ داشتن GUID یا امکان دیدن فایل دیگری مجوز انتخاب آن به‌عنوان عکس خود نیست. Migration صرفاً schema را می‌سازد و هیچ عکس حدسی به کاربران قدیمی نسبت نمی‌دهد.

تست Migration فقط در LocalDB تصادفی و ایزوله اجرا می‌شود؛ `MakanApp`، `MakanApp_Test` و سرورها هدف آن نیستند.

## ۵. API مدیریت عکس

### Set / Replace

```http
PUT /api/v1/me/profile-photo
Content-Type: application/json
Authorization: Bearer <session-token>
```

```json
{
  "fileAssetId": "11111111-1111-1111-1111-111111111111"
}
```

پاسخ 200 از نوع `ProfilePhotoResult`:

```json
{
  "fileAssetId": "11111111-1111-1111-1111-111111111111",
  "contentType": "image/png",
  "downloadUrl": "/api/v1/files/11111111-1111-1111-1111-111111111111/content"
}
```

شناسهٔ نمونه واقعی نیست. کاربر از نشست احرازشده گرفته می‌شود؛ Request، UserId/Role/OrganizationId برای تعیین مالک ندارد.

فایل باید متعلق به همان User، از scope شخصی (`OrganizationId=null`)، Ready و JPEG/PNG باشد. فایل سازمانی صرفاً با uploader بودن، به عکس شخصی عمومی‌تر تبدیل نمی‌شود. Role برای انتخاب فایل شخصی لازم نیست.

انتخاب مجدد همان فایل idempotent است. تعویض با فایل دیگر تنها مرجع User را عوض می‌کند.

### Remove

```http
DELETE /api/v1/me/profile-photo
```

پاسخ `204 No Content`؛ تکرار حذف نیز موفق است. User، Person و FileAsset حذف نمی‌شوند. `GET /api/v1/me` بعد از حذف، `profilePhoto=null` می‌دهد.

endpoint مستقل انتخاب شد چون null/omission در PATCH فعلی پروفایل معنای حفظ مقدار دارد؛ حذف عکس به همان قرارداد مبهم تحمیل نشده است.

### خطاهای مهم

| حالت | نتیجه |
|---|---|
| نشست نامعتبر/منقضی/لغوشده یا حساب معلق | 401 طبق pipeline موجود |
| فایل ناموجود، Guid صفر، فایل دیگری یا فایل سازمانی | 404، `FILE_NOT_FOUND` |
| FileAsset در حالت Pending/Rejected/Deleted | 409، `FILE_NOT_READY` |
| فایل Ready غیرتصویری | 415، `FILE_TYPE_NOT_ALLOWED` |
| upload با امضای نامعتبر | 415، `FILE_CONTENT_INVALID` |
| حجم بیش از سقف فعلی | 413، `FILE_TOO_LARGE` |
| حذف عمومی فایلِ عکس فعال | 409، `FILE_IN_USE` |
| نسخهٔ قدیمی فایل در رقابت نوشتن | خطای concurrency موجود |

Guid با فرمت نامعتبر یا body نامعتبر همچنان از Model Binding/ProblemDetails موجود عبور می‌کند. خطاها path، credential یا جزئیات مخزن را برنمی‌گردانند.

## ۶. Self و هویت Messaging

فیلد `profilePhoto` به‌صورت additive به `CurrentUserResult`، `SafeMessagingIdentityResult` و `MessagingPublicProfileResult` افزوده شده است؛ null برای همهٔ کاربران قبلی معتبر است.

- پاسخ Login و `GET /api/v1/me` عکس فعلی را می‌گیرند.
- People، Contacts و Recent Contacts همان قرارداد مشترک را استفاده می‌کنند.
- Direct، inbox، اعضای Group/Channel، فرستنده، mention و search از هویت امن مشترک عکس را می‌گیرند.
- نگاشت هویت موجود در Block/Moderation و رأی‌دهندگان non-anonymous نیز فیلد مشترک را منتقل می‌کند؛ permission آن endpointها تغییر نکرده است.
- رأی‌دهندگان Anonymous افشا نمی‌شوند.

در پیام‌ها عکس snapshot ذخیره نشده است. خواندن دوبارهٔ پیام قدیمی به عکس فعلی Sender می‌رسد؛ تغییر یا حذف عکس، MessageId و ConversationId را عوض نمی‌کند.

## ۷. Privacy و authorization دانلود

قاعده: **نمایش عکس از مجوز نمایش هویت پیروی می‌کند؛ خود عکس مجوز Discovery یا Messaging نمی‌سازد.**

دانلود ناشناس اضافه نشده است. `EfFileAssetBoundAccessResolver` مرجع عکس فعال را می‌شناسد و در هر درخواست، grant جاری را بررسی می‌کند:

۱. خود مالک عکس؛ یا
۲. هویتی که با همان `MessengerRelationshipQuery.DiscoverableAccounts` و حداقل Profile فعلی قابل نمایش است؛ یا
۳. هویت قابل مشاهده در Conversation/تاریخچهٔ واقعاً مجاز، با همان participant، scope، history visibility، hidden-message و عضویت رسمی کلاس؛ یا
۴. هویت رأی‌دهنده در Poll غیرAnonymous که پیام آن واقعاً قابل خواندن است.

شناخت FileAssetId به‌تنهایی grant نیست. دسترسی به attachment دیگری همچنان از مصرف‌کننده و scope خودش بررسی می‌شود؛ استثنای شخصی فقط از مرجع فعال Avatar می‌آید.

نکتهٔ audit: در سیاست فعلی، Block یا `DirectMessagesAudience=Nobody` لزوماً نام عمومی/تاریخچهٔ مجاز را پنهان نمی‌کند؛ `CanStartDirect` و مجوز Send رد می‌شوند. این رفتار بازطراحی نشده است. Avatar همراه همان هویت مجاز می‌آید و قابلیت Send/خواندن چت دیگری یا دیدن شمارهٔ مخفی ایجاد نمی‌کند.

Minor/Unknown غریبه با اضافه‌کردن Avatar discoverable نمی‌شود. GuardianRelation مجوز دیدن چت خصوصی کودک نیست؛ School Manager نیز با Avatar دسترسی به Direct خصوصی نمی‌گیرد. مرز Official Class و SignalR تغییر نکرده است.

## ۸. transaction و هم‌زمانی

`ProfilePhotoService` از transaction سریال‌پذیر موجود Identity و قفل `UPDLOCK/HOLDLOCK` روی User و FileAsset استفاده می‌کند. User دوباره زیر قفل از DB بارگذاری می‌شود؛ مقدار tracked مرحلهٔ Authentication جای وضعیت تازه را نمی‌گیرد.

هنگام اتصال تازه، `EfFileAssetStore.TouchForBinding` یک update بدون تغییر مقدار Status انجام می‌دهد تا rowversion فایل تغییر کند. اگر Delete قبلاً فایل را با نسخهٔ قدیمی خوانده باشد، Save آن conflict می‌گیرد و blob حذف نمی‌شود. اگر Delete زودتر نهایی شده باشد، Set فایل Deleted را رد می‌کند.

دو Set هم‌زمان روی یک User سریال می‌شوند و در نهایت فقط یک FK وجود دارد. API برای این انتخاب ساده ETag جدیدی تعریف نکرده است؛ آخرین انتخاب موفق مرجع نهایی است.

هیچ تماس SMS/AI/HTTP یا ذخیرهٔ فایل بزرگ زیر transaction انتخاب عکس انجام نمی‌شود؛ upload مرحلهٔ جداگانهٔ موجود است.

## ۹. lifecycle فایل

- فایل جدید: upload موجود، Pending سپس Ready.
- عکس فعال: مرجع User، حذف از File API ممنوع و از terminal cleanup محافظت می‌شود.
- Replace/Remove: فقط مرجع عوض/پاک می‌شود؛ فایل قبلی کورکورانه physical delete نمی‌شود.
- فایل قبلی اگر مصرف‌کننده/retention دیگری دارد تابع همان lifecycle باقی می‌ماند.
- فایل قبلیِ بدون مصرف‌کننده همچنان از مسیر مجاز مالک قابل مدیریت است؛ Ready-orphan reaper در زیرساخت فعلی وجود ندارد و در این مأموریت ساخته نشده است.

`TerminalFileCleanupProcessor` فقط Deleted/Rejectedهای مجاز را بررسی می‌کند؛ مرجع User نیز به reference check آن اضافه شده است. TTL و RetainedAtUtc موجود بازتعریف نشده‌اند.

## ۱۰. Cache و SignalR

URL از FileAssetId ساخته می‌شود؛ با تصویر جدید، ID/URL جدید داریم و query timestamp تصادفی لازم نیست. پاسخ محتوای فایل احرازشده `Cache-Control: private, no-store` و `Vary: Authorization` دارد؛ cache مشترک نباید تصویر را مستقل از مجوز بازپخش کند.

Presence و notificationهای Hub، UserId/status یا شناسه و cursor تغییر را می‌فرستند؛ نام/عکس کامل در آن‌ها وجود نداشت. payload یا broadcast جدید Avatar اضافه نشده است. REST هویت فعلی را می‌دهد؛ UI بازشده باید پس از تغییر عکس، هویت را دوباره fetch کند.

## ۱۱. FRONTEND_HANDOFF

### Upload

`POST /api/v1/files` با `multipart/form-data` و field واقعی `file`. فایل را در Personal workspace بارگذاری کنید تا `OrganizationId=null` باشد. پاسخ 201 همان `FileAssetResult` موجود است؛ فقط FileAsset Ready خود کاربر قابل انتخاب است.

### Set Avatar

`PUT /api/v1/me/profile-photo` با `{ "fileAssetId": "..." }`؛ پاسخ 200 همان descriptor بالا است.

### Remove Avatar

`DELETE /api/v1/me/profile-photo`؛ پاسخ 204. پس از آن self/identity را refresh کنید.

### Current User

`GET /api/v1/me` → `profilePhoto` یا null.

### Messaging

| پاسخ | مسیر عکس |
|---|---|
| People Search | `items[].profilePhoto` |
| People Resolve | `profile.profilePhoto` |
| Public Profile | `profilePhoto` |
| Contacts | `items[].profile.profilePhoto` |
| Recent Contacts | `items[].profile.profilePhoto` |
| Direct | `otherParticipant.profilePhoto` |
| Conversation list | `[].otherParticipant.profilePhoto` |
| Members | `items[].user.profilePhoto` |
| Message history/search | `sender.profilePhoto` در هویت همان نتیجه |
| Mentions | `mentions[].profilePhoto` |

### نمایش تصویر احرازشده

`downloadUrl` مسیر نسبی API است و باید نسبت به base URL فعلی Backend resolve شود. چون endpoint Bearer authentication دارد، `<img src>` بدون Header مسیر ورود تازه‌ای پیدا نمی‌کند. Client می‌تواند با همان لایهٔ HTTP احرازشده تصویر را fetch، به Blob/Object URL تبدیل و در UI نمایش دهد؛ هنگام تغییر/خروج، Object URL قبلی آزاد شود. Token را داخل URL، local document یا log نگذارید.

### Null و تغییر permission

برای null، fallback initials/default avatar نمایش دهید. اگر دانلود 401/404 شد، تصویر قبلی را به‌عنوان مجوز معتبر نگه ندارید؛ session/هویت و مجوز جاری را دوباره بررسی کنید. فایل قبلی پس از Replace ممکن است دیگر برای viewer مجاز نباشد.

## ۱۲. تست‌ها و شواهد

تست‌های جدید:

- `UserProfilePhotoTests`: null، Set/Replace/Remove، idempotency، UTC، عدم تکمیل خودکار Profile و بقای عکس با Suspension/Age.
- `ProfilePhotoEndpointsTests`: JPEG/PNG واقعیِ مصنوعی، Non-image/مالک دیگر/شناسهٔ نامعتبر/حالت ناآماده، scope سازمانی، حجم و امضا، Replace/Remove، People/Contacts/Direct/Sender/Members، Block/Privacy، Minor/Unknown، Role/Membership، Suspension/Session، path/URL injection، anonymous و OpenAPI.
- تست rowversion، حذف با نسخهٔ قدیمی را پس از اتصال عکس رد می‌کند؛ تست دو Set هم‌زمان یک مرجع Ready باقی می‌گذارد.
- `ProfilePhotoMigrationTests`: ارتقای schema قبلی، nullable بودن کاربران قدیمی، حفظ DOB/FileAsset، تکرار apply، FK، unique reference و عدم حذف فیزیکی فایل bound.

دو تست قدیمی Migration برای درج دادهٔ legacy به فهرست ستون‌های همان schema محدود شدند؛ فرض «DOB همیشه آخرین Migration است» حذف شد. Migrationهای قبلی و قواعد DOB تغییر نکردند.

شواهد این اجرا در `.artifacts/profile-photo-20261028-f717c74a` هستند. نتایج واقعی Full Suite و Final Suite در گزارش نهایی ثبت می‌شوند؛ نتیجهٔ قبلی یا تعداد حدسی جای اجرای واقعی گزارش نمی‌شود.

## ۱۳. محدودیت‌های فعلی

- WebP و SVG در مسیر فعلی پشتیبانی نشده‌اند.
- اعتبارسنجی موجود magic signature است؛ full decode، resize، crop، پاک‌سازی EXIF، AV/CDR یا تشخیص محتوای تصویری جدید اضافه نشده است.
- Ready-orphan cleanup برای عکس‌های تعویض/حذف‌شده هنوز نداریم؛ حذف کورکورانه انجام نمی‌شود.
- URL ناشناس، Token داخل query یا signed URL دائمی وجود ندارد.
- پیام و کارت مخاطب، snapshot عکس جدید نمی‌گیرند؛ live Avatar-change broadcast هم اضافه نشده است.
- ورود به Organization با context قدیمیِ نامعتبر ممکن است مثل File API موجود نیازمند انتخاب دوبارهٔ Personal باشد؛ مالکیت عکس تغییر نمی‌کند و Set/Remove self به Role وابسته نیست.
- این اعتبارسنجی محلی، اثبات Deploy یا ارتقای دیتابیس واقعی نیست؛ وضعیت Commit/Publish را از گزارش Release متناظر بررسی کنید. دیتابیس واقعی فقط با مجوز و برنامهٔ انتشار مستقل ارتقا می‌یابد.

## ۱۴. مسیر مطالعهٔ کد

`ProfileController` → `ProfilePhotoService` → `User.SetProfilePhoto` → `IIdentityStore / IFileAssetStore` → `EfIdentityStore / EfFileAssetStore` → `UserConfiguration` → Migration → تست‌ها.

برای خواندن عکس: `FilesController` → `StorageService` → `EfFileAssetBoundAccessResolver` → queryهای هویت/Conversation مجاز → `IFileStorage`.

برای DTOها: `CurrentUserResult` و `SafeMessagingIdentityResult` → `MessagingProfileStoreRecord` → `EfMessagingDirectoryStore` و loaderهای batch در `EfMessagingStore`.

## ۱۵. نتیجهٔ Validation این اجرا

- Restore: موفق؛ Package یا نسخهٔ Framework تغییر نکرد.
- Build و Final Build نسخهٔ Release: موفق، صفر Warning و صفر Error.
- Focused Unit: ۹۳ موفق؛ Focused Integration نهایی: ۷۹ موفق؛ صفر Fail/Skip.
- Full Suite اول: Unit=886، Integration=1416، Architecture=14؛ مجموع 2316، Failed=0، Skipped=0.
- Final Full Suite: Unit=886، Integration=1416، Architecture=14؛ مجموع 2316، Failed=0، Skipped=0. هر دو نوبت کامل واقعاً اجرا و موفق شدند.
- EF pending model changes: ندارد.

تعداد تست‌های جدید ۴۴ مورد است: ۹ Unit و ۳۵ Integration. چهار شکست در هر یک از دو اجرای اولیهٔ focused Integration، مربوط به نبود JPEG در StorageOptions مستقیمِ fixture بودند؛ validation تولید ضعیف نشد و فقط allowlist میزبان اختصاصی تست، با حفظ سقف/مسیر/TTL قبلی، اصلاح شد. اجرای focused نهایی هر ۷۹ مورد را گذراند.

شواهد کامل، وضعیت Git و گزارش آموزشی مرحلهٔ اولیه در پوشهٔ `.artifacts/profile-photo-20261028-f717c74a` قرار دارند. در آن مرحلهٔ اولیه، طبق محدودهٔ همان درخواست، HEAD مبنا ثابت ماند و Stage/Commit انجام نشد؛ نهایی‌سازی و انتشار، مرحله‌ای مستقل با گزارش Release خودش است.
