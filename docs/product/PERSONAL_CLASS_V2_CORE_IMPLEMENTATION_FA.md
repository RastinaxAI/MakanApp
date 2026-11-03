# هستهٔ کلاس مستقل نسخهٔ ۲

مبنای این مرحله `154c437`، سند `MakanApp_Backend_API_Gap_Analysis_FA_v2.pdf` و تصمیم صریح مالک محصول دربارهٔ فعال‌سازی JoinRequest و ظرفیت Learner است. این سند رفتار واقعی Backend را شرح می‌دهد؛ پیاده‌سازی آموزش مستقل Assignment/Exam یا قیمت‌گذاری جدید نیست.

## مسئله و مرز

مسیر قبلی `POST /api/v1/personal-classes` مستقیماً کلاس میزبان را می‌ساخت. اکنون این مسیر با `409 PERSONAL_CLASS_REVIEW_REQUIRED` درخواست تشکیل را لازم می‌کند. حساب فعال بدون نقش مدرسه می‌تواند درخواست ثبت کند. هیچ Organization، Enrollment، TeacherAssignment یا نقش سازمانی از تشکیل کلاس ساخته نمی‌شود. هیچ تصمیم این مسیر به DOB، سن، AgeClassification یا AgeVerification وابسته نیست.

## جریان واقعی و لایه‌ها

`PersonalClassRequestsController → PersonalClassFormationService → IPersonalClassFormationStore → EfPersonalClassFormationStore → PersonalClassRequest / PersonalClass → SQL Server`

Domain چرخهٔ درخواست، نقش‌ها و انتقال مسئولیت را نگه می‌دارد. Application ورودی، نشست و مجوز پلتفرم را اعتبارسنجی و قراردادها را تعریف می‌کند. Infrastructure تراکنش، قفل، سهمیه، Audit و Outbox را اجرا می‌کند. Controller فقط HTTP را نگاشت می‌کند. .NET 9، EF Core 9، SQL Server و Onion تغییر نکرده‌اند.

درخواست `Draft → Submitted → UnderReview → Approved/NeedsChanges/Rejected` دارد؛ شروع بررسی اختیاری است. NeedsChanges قابل ویرایش و ارسال مجدد است؛ Draft/NeedsChanges/Submitted قابل لغو طبق Domain هستند. `ClientOperationId` و SHA-256 ورودی نرمال‌شده تکرار ایجاد را از استفادهٔ مجدد با payload متفاوت جدا می‌کنند. تغییر نسخهٔ درخواست با `rowversion` کنترل می‌شود. Submit کلاس نمی‌سازد.

تأیید در تراکنش Serializable، بعد از کنترل مجدد مجوز Platform و قرارداد Hosting، یک کلاس می‌سازد. تأیید تکراریِ همان تصمیم اثر دوباره ندارد. کلاس ابتدا `Provisioning` است. Outbox موجود دو مقصد را آماده می‌کند؛ سپس `ReadyForSetup` و فعال‌سازی صریح مدیر به `Active` می‌رسد. شکست آماده‌سازی `ProvisioningFailed` است؛ Suspend و Archive نیز صریح هستند.

## مسئولیت و عضویت محلی

- درخواست‌کننده مدیر پیش‌فرض است. انتخاب شخص دیگر توسط Platform ابتدا یک `PersonalClassManagerTransfer` منتظر پذیرش می‌سازد؛ تا پذیرش، آن شخص عضو یا مدیر نمی‌شود.
- `PersonalClassParticipant` موجود همان Membership محلی است؛ نام فیزیکی `StudentUserId` برای حفظ داده باقی مانده، اما API جدید `UserId` و LocalRoles می‌دهد.
- نقش‌های پرچمی `Learner=1`، `Teacher=2`، `Manager=4` قابل ترکیب‌اند. Teacher اختیاری است و پیشنهاد مدرس در درخواست، عضویت یا اختیار خودکار نمی‌سازد.
- دعوت فقط Learner/Teacher یا ترکیب آن‌ها می‌دهد. افزودن/حذف Manager از API نقش‌ها ممکن نیست؛ انتقال با رضایت مقصد انجام می‌شود. تعداد مدیران اضافی حدس زده نشده است.
- تفویض `PublishCommunication=1` و `ManageCommunication=2` فقط برای نقش Teacher معتبر و پیش‌فرض None است. مدیر از نقش محلی خود اختیار می‌گیرد.
- مدیر مسئول تا انتقال پذیرفته‌شده یا Archive نمی‌تواند عضویت خود را پایان دهد. Host تجاری با انتقال مدیر عوض نمی‌شود.
- JoinRequest فقط پس از `AcceptingJoinRequests=true` صریح مدیر ثبت می‌شود. دانستن شناسهٔ کلاس، دسترسی به محتوا یا عضویت نمی‌دهد؛ پیش‌فرض همهٔ کلاس‌ها بسته است.
- فقط عضویت فعال دارای Learner ظرفیت آموزشی مصرف می‌کند. ظرفیت با سهمیهٔ Hosting متفاوت است و هر دو در پذیرش کنترل می‌شوند. Legacy ظرفیت حدسی نمی‌گیرد.

## هم‌زمانی و SQL

Migrationهای جدید:

1. `20261103073219_AddReviewedPersonalClassLifecycle`
2. `20261103074858_BindPersonalClassInvitationRoles`

جدول‌های جدید `academic.PersonalClassRequests`، `PersonalClassJoinRequests` و `PersonalClassManagerTransfers` دارای PK، FK محدودشده و `rowversion` هستند. کلاس، عضویت، دعوت، Conversation و OfficialSpaceProvision به‌صورت additive توسعه یافته‌اند.

Unique indexها از درخواست تکراری حساب/Operation، بیش از یک JoinRequest منتظر، بیش از یک انتقال منتظر، بیش از یک مدیر فعال و بیش از یک عضویت فعال حساب در کلاس جلوگیری می‌کنند. FK مرکب JoinRequest، عضویت نتیجه را به همان کلاس و حساب متصل می‌کند. وضعیت‌ها، نقش‌ها و مقادیر ظرفیت CHECK دارند.

ترتیب قفل پذیرش‌ها Host تجاری سپس کلاس است. درخواست Join علاوه بر آن قفل تراکنشی `sp_getapplock` برای حساب/Operation می‌گیرد تا یک Operation روی دو کلاس هم‌زمان دو نتیجه نسازد. پذیرش دعوت و تأیید Join زیر قفل کلاس به یک عضویت می‌رسند و intentهای دیگر با `MembershipEstablished` بسته می‌شوند. پایان عضویت رکورد را حذف نمی‌کند.

## فضاها، بازیابی و مجوز

`OfficialSourceKind.PersonalClass` با `PersonalClassId` واقعی و `OrganizationId=null` استفاده می‌شود. Group و Channel `SystemManagedAcademic` هستند و User Owner ندارند. Unique binding کلاس/نوع، دقیقاً یک مقصد از هر نوع می‌دهد.

`EfOfficialSpacesStore.PersonalClass` هر مقصد را در تراکنش کوتاه مستقل آماده می‌کند. شکست ذخیرهٔ Channel به `RepairRequired/PersistenceFailure` و AttemptCount تبدیل می‌شود؛ Group قبلی باقی می‌ماند. Worker شکست را موفق اعلام نمی‌کند. Retry مقصد آماده را بازسازی نمی‌کند. GET هیچ create/repair/backfill ندارد.

`OfficialSpaceAccessQuery` عضویت جاری را منبع Read/Publish/Manage می‌گیرد. حذف عضویت بلافاصله Read، Send، Download جدید، Replay و مخاطب تحویل Realtime را می‌بندد؛ وابسته به پایان یافتن projection Worker نیست. گفتگوهای شخصی مستقل تغییر نمی‌کنند. Archive/Suspend انتشار تازه و تعامل نوشتنی را می‌بندند؛ خواندن تاریخچه برای عضو همچنان مجاز محفوظ است.

Group برای اعضای جاری امکان تعامل معمول دارد؛ Channel برای مدیر یا Teacher با تفویض صریح قابل انتشار است. Privacy، Block، پروفایل، نشست، محدودیت Messaging و Safety موجود همچنان معتبرند. Guardian یا Teacher مدرسه از رابطهٔ بیرون کلاس مجوز نمی‌گیرد.

## Realtime و Audit

Outbox و `RealtimeInvalidationWriter` موجود reuse می‌شوند؛ stream جدیدی ساخته نشده است. تغییر درخواست، نقش، عضو، دعوت، Join، انتقال، وضعیت کلاس و فضاها invalidation کمینهٔ `access` و در موارد مرتبط `commerce` می‌سازند. محتوا، شماره و متن خصوصی درخواست در event نیست. Frontend پس از invalidation قرارداد مربوط را دوباره می‌خواند؛ تحویل exactly-once فرض نشده است.

تصمیم‌ها و عملیات مدیریتی در `AdminAuditLog` موجود با actor/session/target/reason/time/correlation ثبت می‌شوند. تاریخچهٔ درخواست برای متقاضی ActorUserId بررسی‌کننده را پنهان می‌کند؛ Platform دارای مجوز تاریخچهٔ مدیریتی را می‌بیند.

## فایل‌ها و مسیر مطالعه

1. `MakanApp.Api/Controllers/PersonalClassRequestsController.cs` و `PlatformPersonalClassRequestsController.cs`: مرز درخواست‌کننده/بررسی‌کننده.
2. `MakanApp.Application/Academic/PersonalClassFormationService.cs`: validation و مجوز.
3. `MakanApp.Domain/Academic/PersonalClassRequest.cs`، `PersonalClass.cs` و `PersonalClassParticipant.cs`: چرخه و قواعد خالص.
4. `PersonalClassJoinRequest.cs` و `PersonalClassManagerTransfer.cs`: قصد، تصمیم و عضویتِ متفاوت.
5. `MakanApp.Infrastructure/Academic/EfPersonalClassFormationStore.cs` و partialهای `EfPersonalClassStore`: تراکنش، idempotency و ظرفیت.
6. `MakanApp.Infrastructure/Messaging/EfOfficialSpacesStore.PersonalClass.cs` و `OfficialSpaceAccessQuery.cs`: provisioning و مجوز جاری.
7. Configurationهای Academic/Messaging و دو Migration: دفاع SQL و تبدیل Legacy.
8. `PersonalClassV2EndpointsTests.cs`، `PersonalClassV2MessagingTests.cs`، `PersonalClassV2RecoveryTests.cs` و `PersonalClassV2MigrationTests.cs`: HTTP/SQL، race، revoke و بازیابی؛ Unitهای `PersonalClassRequestTests` و `PersonalClassLifecycleTests` قواعد خالص را می‌سنجند.

## تصمیم‌های باز و محدودیت‌ها

G19 شامل قیمت‌گذاری و پرداخت‌کنندهٔ جدید باز است؛ Hosting فعلی حفظ شده است. Assignment/Exam/Grade، Source و AI خودکار برای PersonalClass فعال نشده‌اند. Parent دسترسی ویژه نمی‌گیرد. سیاست retention پس از خروج، پایان اضطراری، مدیر اضافی و قدرت پیش‌فرض Teacher در این مرحله تعیین نشده‌اند. رد مسئولیت توسط مدیر جایگزین او را مجبور به پذیرش نمی‌کند؛ مسیر مداخلهٔ اضطراری یا جایگزینی اجباری ایجاد نشده است.

نکات آموزشی: قصد عضویت با عضویت یکی نیست؛ RowVersion جای تراکنش ظرفیت را نمی‌گیرد؛ projection چت منبع مجوز نیست؛ Retry باید نتیجهٔ قبلی را حفظ کند؛ Backfill مالکیت مبهم را نباید حدس بزند؛ وضعیت شکست عملیاتی باید قابل مشاهده و ترمیم باشد.

مرحلهٔ بعد تکمیل چرخهٔ آموزشی مستقل پس از تصمیم محصولی مربوط است؛ قیمت‌گذاری و دسترسی Parent نباید ضمن آن بدون تصمیم صریح اضافه شوند.

## شواهد اعتبارسنجی پیش از Commit

Restore و Build نهایی Release موفق، Warning=0 و Error=0 بودند. گزارش قطعی TRX شامل Unit=1057، Integration=1697 و Architecture=14 است؛ مجموع 2768 با Failed=0 و Skipped=0، نسبت به baseline مجموع 36 اجرای تست بیشتر. تستی غیرفعال نشده است. دو مسیر Migration و نبود Pending Model Changes نیز تأیید شدند.

سناریوی واقعی با Kestrel، SQL موقت مستقل و WebSocket واقعی SignalR اجرا شد: 66 درخواست HTTP شامل حساب عادی، صف و جزئیات Platform، NeedsChanges، resubmit، approve، reject، رد SchoolManager، provisioning، دعوت و عضویت، تحویل زنده، retry و revoke/replay. دیتابیس موقت بعد از آزمون حذف شد؛ Real OTP rows=0، AI Runtime=false و تغییر دیتابیس عادی=false بود. نخستین اجرای ابزار E2E به‌دلیل ناقص بودن quotaهای fixture متوقف شد؛ با تکمیل همان دادهٔ ساختگی، اجرای نهایی موفق شد.

Evidence محلی در `.artifacts/personal-class-v2-20261103-91ea2492` است. اسکن Source، secret واقعی نیافت؛ Hash هر 8 فایل محافظت‌شدهٔ AI/تنظیمات خصوصی ثابت ماند. نتیجهٔ Commit، رگرسیون پس از Commit و Remote verification در گزارش نهایی این مرحله ثبت می‌شود؛ این سند به‌تنهایی ادعای Deploy یا Push نیست.
