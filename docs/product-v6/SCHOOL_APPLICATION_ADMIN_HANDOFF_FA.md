# تحویل Admin — بررسی و تجارت درخواست آموزشگاه

تاریخ: `2026-11-13`. قرارداد متقاضی و نمونهٔ DTOها در [تحویل Frontend](SCHOOL_APPLICATION_FRONTEND_HANDOFF_FA.md) است. مبنا Source شاخهٔ `feature/product-v6-backend-alignment`؛ این مرحله Deploy ندارد.

## مجوزها و مرز دسترسی

- صف، جزئیات، تاریخچه و مدارک Submitشده: `platform.school_applications.read`.
- شروع بررسی و تصمیم: Read بالا به‌علاوهٔ `platform.school_applications.review`.
- خواندن نگاشت تجاری: Read و `platform.commerce.manage`.
- تغییر نگاشت: هر سه permission بالا، نشست معتبر و حساب فعال.
- تکمیل مدیر ثبت‌نشدهٔ Legacy و درخواست بازیابی Provisioning: Read + Review + `platform.provisioning.manage`؛ تصمیم مالی دستی یا تغییر مدیر نهایی مجاز نیست.
- PlatformOwner موجود این مجوزها را دارد؛ Support، Organization Manager یا Teacher به‌صورت ضمنی Reviewer نیستند. revocation در درخواست/خواندن فایل دوباره رعایت می‌شود.
- Draft و Draft لغوشدهٔ هرگز Submitنشده برای Reviewer مخفی‌اند. مدارک پرونده منبع دسترسی‌اند؛ uploader بودنِ حساب دیگر یا دانستن FileId مجوز نیست.

## صف، جزئیات و تاریخچه

| Method | Route | داده/رفتار |
|---|---|---|
| GET | `/api/admin/v1/school-applications` | فیلتر `status?`؛ pagination پیش‌فرض۱/۵۰، سقف۱۰۰ رکورد |
| GET | `/{id}` | `SchoolApplicationResult`، شناسهٔ متقاضی مجاز، فرم، مدارک، تصمیم، nextAction و progress جدا |
| GET | `/{id}?revision=1` | snapshot فرم و مدارک آن revision؛ readonly |
| GET | `/{id}/history` | action/revision/version/reason/UTC و ActorUserId؛ صفحه‌بندی |
| POST | `/{id}/review` | شروع بررسی، فقط Submitted |
| POST | `/{id}/decisions` | فقط UnderReview؛ NeedsChanges/Rejected/Approved |
| GET | `/offerings` | نگاشت‌های صریح موجود و Version؛ بدون default فرضی |
| PUT | `/offerings/{kind}` | نگاشت server-owned نوع پرونده به Published PlanVersion |
| POST | `/{id}/initial-manager/confirm` | فقط Approved قدیمی با انتخاب مدیرِ خالی؛ ExpectedVersion، reason، operation و حساب واجد شرایط لازم‌اند |
| POST | `/{id}/provisioning/retry` | فقط Paid تأییدشده؛ ادامهٔ checkpointهای موجود، بدون ورودی قیمت/پلن/مدیر |

Routeهای نسبی از `/api/admin/v1/school-applications` شروع می‌شوند. تاریخچهٔ قدیمی قبل از این Migration بازسازی حدسی نمی‌شود؛ revisionها و تصمیم‌های قبلی همچنان موجودند، history تازه از عملیات واقعی بعد از ارتقا نوشته می‌شود.

## بررسی و تصمیم

Request شروع بررسی و تصمیم همان `SchoolApplicationActionCommand` است:

```json
{
  "clientOperationId": "33333333-3333-4333-8333-333333333333",
  "expectedVersion": 3,
  "applicantFeedback": "دلیل روشن و قابل نمایش به متقاضی",
  "decision": "Approved",
  "initialManagerUserId": "66666666-6666-4666-8666-666666666666"
}
```

در `/review` فیلد `decision` لازم نیست؛ دلیل در `applicantFeedback` برای **تمام** اقدامات بررسی الزامی است و تا۲۰۰۰ کاراکتر مطابق قرارداد موجود پذیرفته می‌شود. این فیلد یادداشت محرمانهٔ داخلی نیست و به متقاضی نمایش داده می‌شود. رونوشت مدارک یا اطلاعات حساس را در آن قرار ندهید.

`NeedsChanges` همان پرونده را قابل اصلاح می‌کند. متقاضی فرم/مدارک را در revision تازه اصلاح و resubmit می‌کند؛ Reviewer بررسی را روی نسخهٔ جاری دوباره آغاز می‌کند. `Rejected` نهایی است و پرداخت/Provisioning ندارد. `Approved` فقط تصمیم پرونده است و Organization/Subscription/نقش/دعوت/Payment ایجاد نمی‌کند.

`proposedInitialManagerUserId` برابر متقاضی است و تأیید ضمنی نیست. Reviewer باید در همان درخواست Approved، متقاضی یا حساب فعالِ دارای پروفایل کامل دیگری را صریحاً انتخاب کند. نیاز به Membership یا نقش Manager قبلی نیست. حذف فیزیکی/نبود حساب یا تعلیق و پروفایل ناقص رد می‌شوند؛ درخواست حذفِ فعلاً بدون اجرای حقوقی، حذف نهایی حساب فرض نشده است. برای NeedsChanges/Rejected/شروع بررسی، مدیر ارسال نشود. انتخاب نهایی با reviewer، دلیل، UTC، revision و version در Decision/History ثبت می‌شود؛ دو انتخاب هم‌زمان تنها یک برنده دارند.

انتخاب نهایی با Edit معمولی عوض نمی‌شود. `POST_APPROVAL_MANAGER_CHANGE=DEFERRED` است. مسیر `initial-manager/confirm` فقط انتخاب خالیِ پروندهٔ Approved قدیمی را با `{clientOperationId,expectedVersion,initialManagerUserId,reason}` تکمیل می‌کند؛ انتخاب قبلی را جایگزین نمی‌کند. اگر قبلاً Paid است، همان intent را تکمیل و صف‌بندی می‌کند؛ پرداخت تازه لازم نیست. مسیر retry فقط `{clientOperationId,expectedVersion,reason}` می‌گیرد؛ تکرار همان operation نسخه/تاریخچه/کار اضافه نمی‌سازد.

نسخهٔ stale یا تصمیم هم‌زمان ۴۰۹ می‌دهد؛ detail را refresh کنید و تصمیم تازه را با شناسهٔ operation تازه ثبت کنید. Retry همان operation و همان payload تصمیم/History/اعلان دوم نمی‌سازد. برای اصلاح تصمیم تأییدشده یا لغوشده مسیر تازه‌ای اختراع نشده است.

## مدارک

`documents` شناسهٔ FileAsset و Kind است. دانلود همان `/api/v1/files/{fileId}/content` با نشست Reviewer مجاز است. فایل مالک دیگر، unready/deleted یا scoped به Organization/PersonalClass برای bind متقاضی پذیرفته نیست. اسناد نسخه‌های تاریخی retained و immutable باقی می‌مانند. بستن پرونده، مالکیت فایل یا دادهٔ متقاضی را به Reviewer منتقل نمی‌کند.

نوع حقوقی به حداقل یک License یا OrganizationDocument نیاز دارد؛ مدرس حقیقی به Degree، سابقه و نمونه‌کار. هر دو ظرفیت required و نامنفی‌اند، صفر مجاز است. فیلد حقوقی/دسته‌بندی قانونی دقیق‌تر، نیازمند تصمیم صریح آینده است؛ Reviewer/API نباید کد ملی، مالیاتی یا الزام مثبت‌بودن هر دو ظرفیت را به‌عنوان Policy فعلی معرفی کند.

## تنظیم خدمت و قیمت

ابتدا از Catalog مدیریتی موجود، PlanVersion منتشرشدهٔ اختصاصی خدمت تأسیس با audience سازمانی بسازید. قیمت، calendar ماهانه، مرجع شروط فروش، Entitlementها و Quotaهای لازم باید صریح باشند. این مرحله هیچ دادهٔ تجاری واقعی Seed نمی‌کند و قیمت از متقاضی گرفته نمی‌شود.

`GET /offerings` مقدار Version فعلی را می‌دهد؛ نبود نوع یعنی `expectedVersion=0` برای ایجاد. نمونهٔ نگاشت، با Guid فرضی که باید با نسخهٔ واقعی جایگزین شود:

```json
{
  "clientOperationId": "44444444-4444-4444-8444-444444444444",
  "expectedVersion": 0,
  "planVersionId": "55555555-5555-4555-8555-555555555555",
  "reason": "ثبت نسخهٔ مصوب خدمت تأسیس"
}
```

`kind` یکی از `LegalOrganization` یا `IndividualTeacher` است. PlanVersion فردی، منتشرنشده، فاقد شروط معتبر یا نسخه‌ای که قبلاً در اشتراک/سفارش عادی استفاده شده باشد، پذیرفته نیست. قیمت/سهمیهٔ مشتری فعلی برای تبدیل به خدمت تأسیس بازنویسی نمی‌شود.

نسخه‌های تخصیص‌یافته به این خدمات، از خرید عادی و Catalog اشتراک عادی جدا می‌مانند؛ سابقهٔ نگاشت هم حفظ می‌شود تا تغییر نگاشت، نسخهٔ قبلی را ناخواسته به خرید بدون Approval باز نکند. تغییر نگاشت فقط بر intentهای آینده اثر دارد. Retry عملیات نگاشت، نسخه و رسید تاریخی خودش را می‌دهد و نگاشت جاری را به عقب برنمی‌گرداند.

## پرداخت و آماده‌سازی

Quote/Order با همان Commerce موجود ساخته می‌شوند؛ correlation پرونده در `SchoolApplicationId` است. Price/Currency/PlanVersion/terms روی Order ثابت‌اند. callback زیبال به‌تنهایی سند پرداخت نیست؛ Verify سرور باید موفق باشد. مالی و رسیدهای موجود reuse شده‌اند.

| حالت | معنای عملیاتی |
|---|---|
| `BlockedConfiguration` | نگاشت خدمت هنوز ثبت نشده است؛ پرداخت باز نشود |
| `Configured` | نگاشت ثبت شده؛ شرایط قابل خرید هنگام Quote/Order دوباره validate می‌شوند |
| `FrozenOnOrder` | نسخه و مبلغ روی Order ثبت شده؛ تغییر بعدی نگاشت مبلغ قدیمی را تغییر نمی‌دهد |
| `PaidAwaitingProvisioning` | Order تسویه شده و intent durable در Outbox موجود منتظر اجراست |
| `BlockedManagerPolicy` | فقط پروندهٔ Legacy فاقد تأیید صریح مدیر؛ از مسیر محدود confirm تکمیل شود |
| `NeedsAttention` | Paid محفوظ؛ شکست مرحله و شناسه‌های قبلی قابل پیگیری‌اند؛ recovery مجاز لازم است |
| `Completed` | سازمان، اشتراک تجاری و دعوت مدیر ساخته شده‌اند؛ پذیرش دعوت جداست |

`EfSubscriptionActivationHandler` در تراکنش Outbox موجود، همان `PlatformProvisioningRequest` را با `SchoolApplicationId/SourceOrderId/SourcePaymentId` و مدیر نهایی ثبت می‌کند. کار `school.provisioning-requested.v1` توسط همان `OutboxProcessor`، خارج از تراکنش پیام و با تراکنش‌های کوتاهِ مرحله‌ای `EfPlatformOperationsStore` اجرا می‌شود. سازمان، اشتراک ماهانهٔ معتبر از پلن پرداخت‌شده و دعوت موجودِ سازمانی reuse می‌شوند. unique index و قفل درخواست مانع اثر تکراری‌اند؛ تحویل exactly-once فرض نمی‌شود. هر مرحله شناسهٔ خودش را commit می‌کند و شکست بعدی آن‌ها را حذف نمی‌کند. Paid هیچ‌گاه به Pending/درخواست پرداخت مجدد برنمی‌گردد.

انقضای دعوت فقط Backend-owned است: `Provisioning:InitialManagerInvitationLifetimeDays`، پیش‌فرض فنی ۷ روز و محدودهٔ معتبر۱..۳۰، از زمان اولین ساخت دعوت. این گزینه با دعوت Messaging یا Guardian مشترک نیست. Retry، deadline قبلی را تمدید نمی‌کند. Membership و RoleAssignment پس از پذیرش صریح از `POST /api/v1/invitations/{id}/accept` ساخته می‌شوند.

سیاست مدیر اولیه بسته شده است؛ انتساب خودکار متقاضی همچنان ممنوع است. فقط پرونده‌های قدیمیِ فاقد انتخاب با `INITIAL_SCHOOL_MANAGER_POLICY_REQUIRED` متوقف می‌مانند تا مسیر مجاز confirm اجرا شود. مسیر عمومی Provisioning حق تغییر manager/plan/شرایط همین intent را ندارد. Ready فقط از وضعیت Completed واقعی می‌آید.

فقط Order منقضی بدون PaymentAttempt با operation تازه جایگزین می‌شود؛ سفارش دارای تلاش بانکی، حتی نتیجهٔ نامطمئن، باید از recovery موجود پیگیری شود. حذف/Cancel پرداخت موفق یا تغییر سررسید تجاری به‌صورت حدسی مجاز نیست.

## Audit، اعلان و خطا

Create/Revision/Submit/Cancel/Review/Decision و نگاشت تجاری audit می‌شوند. ایجاد intentِ منتظر سیاست نیز audit سیستمی دارد. History پرونده action، actor، نسخه، دلیل قابل نمایش و UTC را ثبت می‌کند؛ فایل/Secret/Token/دادهٔ خصوصی در Log یا summary تکثیر نمی‌شود. اعلان‌های SchoolUpdated و OrderPaid عمومی‌اند و target داخلی هنگام بازشدن دوباره مجوز را بررسی می‌کند.

۴۰۰ ورودی ناقص/نامعتبر، ۴۰۱ نشست، ۴۰۳ permission، ۴۰۴ resource مخفی و ۴۰۹ نسخه/وضعیت/operation/تنظیم تجاری هستند. `SCHOOL_APPLICATION_CONFLICT` را با refresh و بررسی History حل کنید؛ تکرار خودکار یک تصمیم متفاوت مجاز نیست. `SCHOOL_APPLICATION_COMMERCIAL_TERMS_UNAVAILABLE` نیاز به تنظیم واقعی Admin دارد و نباید با قیمت فرضی دور زده شود.

## پذیرش عملیاتی باقی‌مانده

تست‌های محلی از LocalDB disposable و Zibal کنترل‌شده استفاده می‌کنند؛ هیچ تماس واقعی مالی/SMS/AI یا Deploy انجام نشده است. پیش از عرضهٔ تجاری، نگاشت واقعی دو خدمت طبق [راهنمای تنظیم تجاری](SCHOOL_COMMERCIAL_CONFIGURATION_FA.md) ثبت شود؛ قیمت واقعی حدس زده نشده است. سیاست مدیر اولیه مصوب و پیاده است. تصمیم جایگزینی مدیر/Correction پس از Approval، دسته‌بندی دقیق مدارک و هر الزام سخت‌گیرانه‌تر ظرفیت جدا می‌مانند. Migration/SQL این مرحله فقط پس از Backup و مجوز محیط هدف اجرا شود؛ این Task روی `MakanApp`، `MakanApp_Test` یا سرور اجرا نمی‌کند.
