# تحویل Frontend — درخواست تأسیس آموزشگاه

تاریخ: `2026-11-13`؛ مبنا: `1fd122c2bfa7930dbbf0a1325a3c360a2fa451a5` و پرامپت مصوب School Establishment. این سند قرارداد Backend است و مجوز تغییر Frontend یا ادعای Deploy نیست.

## جریان و مرز تحویل

```text
Account/Session → Draft → Upload/Bind → Submit → UnderReview
→ NeedsChanges → Edit همان پرونده → Submit
→ Rejected / Approved
Approved → نگاشت تجاری سرور → Quote → Order → Zibal → Server Verify
→ Paid → Outbox موجود → PlatformProvisioningRequest موجود
→ BlockedManagerPolicy
```

ساخت نهایی Organization/Subscription/دعوت مدیر در این مرحله بسته به تصمیم مدیر اولیه است. متقاضی به‌صورت ضمنی مدیر نمی‌شود. `Approved` به معنی `Paid` و `Paid` به معنی آماده‌بودن آموزشگاه نیست. `Ready` فقط پس از تکمیل واقعی Provisioning معتبر است؛ در پیاده‌سازی فعلی، intent درخواست تأسیس تا تعیین سیاست مدیر اولیه تکمیل نمی‌شود.

نگاشت تجاری هیچ دادهٔ پیش‌فرض ندارد. پس از Migration، Platform باید نسخهٔ منتشرشدهٔ معتبر و اختصاصی خدمت حقوقی/مدرس حقیقی را صریحاً تنظیم کند. نبود نگاشت پاسخ `409 SCHOOL_APPLICATION_COMMERCIAL_TERMS_UNAVAILABLE` دارد؛ Frontend نباید قیمت یا PlanVersion بسازد.

## فرم‌های مصوب

| نوع | الزامی هنگام Submit |
|---|---|
| `LegalOrganization` | `schoolName`، `legalEntityName`، دو ظرفیت صریح و حداقل یک مدرک `License` یا `OrganizationDocument` |
| `IndividualTeacher` | `schoolName`، `teachingHistory`، `workSamples`، دو ظرفیت صریح و حداقل یک مدرک `Degree`؛ هویت متقاضی از حساب واردشده |

ظرفیت‌ها عدد صحیح و نامنفی‌اند؛ صفر معتبر است. نبود ظرفیت در Draft مجاز است و هنگام Submit خطا دارد. `degreeField` توضیح اختیاری است؛ مدرک مرتبط با رشته/مدرک از FileAsset می‌آید. شناسهٔ ملی/ثبت/مالیات یا فیلد حقوقی حدسی الزامی نشده است. رشتهٔ نوع مدرک حساس به حروف و بدون فاصلهٔ اضافی ارسال شود. `TeachingEvidence`، `WorkSample` و `OtherSupportingDocument` برای اسناد تکمیلی قابل استفاده‌اند؛ جای مدرک الزامی را نمی‌گیرند. دسته‌بندی دقیق‌تر قانونی هنوز سیاست قطعی نیست.

حد طول‌ها: نام مدرسه و نام حقوقی۲۰۰، سابقه و نمونه‌کار۴۰۰۰، توضیح رشته۵۰۰؛ متن trim می‌شود و control character نامعتبر رد می‌شود. حداکثر۲۰ فایل متمایز در هر نسخه. ظرفیت اعلام‌شده مجوز یا سهمیهٔ تجاری خودکار ایجاد نمی‌کند.

## مدارک

۱. فایل را با `POST /api/v1/files?workspaceScope=Personal` و multipart field `file` بارگذاری کنید.
۲. `FileAssetResult.id` را در `documents` فرم ثبت کنید؛ فایل باید متعلق به همین حساب، Personal، آماده و برای binding تازه منقضی‌نشده باشد.
۳. مدرک در Draft فقط برای مالک قابل خواندن است. Reviewer دارای permission پس از Submit به نسخه‌های پرونده دسترسی دارد؛ لغو Draft ارسال‌نشده این دسترسی را ایجاد نمی‌کند.
۴. خواندن همان `GET /api/v1/files/{id}` و `/{id}/content` است. FileId مجوز نیست. Role سازمانی، Support بودن یا حساب دیگر دسترسی نمی‌سازد.
۵. فایل bound در تاریخچه retained است و از حذف عمومی محافظت می‌شود. پاک‌کردن از نسخهٔ جاری، مدرک نسخه‌های قبل را فیزیکی حذف نمی‌کند.

در Update، نبود/Null `documents` مدارک نسخهٔ قبل را نگه می‌دارد؛ `[]` مجموعهٔ نسخهٔ تازه را خالی می‌کند. دادهٔ فرم در `PUT` جایگزین کامل است، نه patch فیلدهای دلخواه.

## API متقاضی

همهٔ مسیرها نشست معتبر حساب فعال می‌خواهند. شناسهٔ پرونده، مالکیت ایجاد نمی‌کند.

| Method | Route | Request | Response / اثر |
|---|---|---|---|
| POST | `/api/v1/school-applications` | `CreateSchoolApplicationCommand` | ۲۰۱، `SchoolApplicationResult` در Draft |
| GET | همان مسیر | `status?`, `page=1`, `pageSize=50` | `SchoolApplicationPage`؛ فقط پرونده‌های خود حساب |
| GET | `/{id}` | `revision?` | جزئیات جاری یا snapshot فرم قدیمی؛ نسخهٔ قدیمی readonly است |
| PUT | `/{id}` | `UpdateSchoolApplicationCommand` | فقط Draft/NeedsChanges؛ یک revision جدید |
| POST | `/{id}/submit` | `SchoolApplicationActionCommand` | ۲۰۰، Submitted؛ validation و readiness دوباره بررسی می‌شوند |
| POST | `/{id}/cancel` | همان command | ۲۰۰، Cancelled؛ فقط پیش از تصمیم نهایی |
| GET | `/{id}/history` | `page`, `pageSize` | `SchoolApplicationHistoryPage`؛ actor داخلی Reviewer به متقاضی نمایش داده نمی‌شود |
| POST | `/{id}/quote` | `SchoolApplicationCommerceCommand` | `CommercialQuoteDto` از نگاشت سرور؛ فقط Approved |
| POST | `/{id}/orders` | همان command | `CreateOrderResult`؛ همان Order موجود Commerce |

مسیرهای نسبی جدول از `/api/v1/school-applications` شروع می‌شوند. `pageSize` حداکثر۱۰۰ و `page` حداکثر۱۰۰۰۰۰ است. cancel/submit نیازمند `clientOperationId` و `expectedVersion` هستند. Cancelled/Rejected قابل Submit/Edit نیستند؛ restore یا correction بعد از Approval ساخته نشده است.

نمونهٔ ساخت، با شناسه‌های غیرواقعی که باید از عملیات واقعی Client/File جایگزین شوند:

```json
{
  "clientOperationId": "11111111-1111-4111-8111-111111111111",
  "kind": "LegalOrganization",
  "data": {
    "schoolName": "نام آموزشگاه",
    "legalEntityName": "نام مؤسسه",
    "teachingHistory": null,
    "workSamples": null,
    "degreeField": null,
    "studentCapacity": 0,
    "teacherCapacity": 0
  },
  "documents": [{"fileAssetId": "22222222-2222-4222-8222-222222222222", "kind": "License"}]
}
```

Update شامل `clientOperationId`, `expectedVersion`, `data`, `documents?` است. Submit/Cancel شامل `clientOperationId`, `expectedVersion` و در صورت نیاز `applicantFeedback` است. Quote/Order فقط `clientOperationId`, `expectedVersion` می‌گیرند؛ `applicantUserId`، Price و PlanVersion ورودی این قرارداد نیستند.

## Read model و حالت‌های UX

`SchoolApplicationResult` شامل `status/revision/version/data/documents/decisions`، زمان‌ها، `canSubmit/canEdit/canCancel/missingRequirements/submissionLimitation/nextAction/progress` است.

| `nextAction` | رفتار Client |
|---|---|
| `CompleteDraft` | فیلدهای `missingRequirements` را کامل کنید |
| `Submit` | ارسال نهایی با نسخهٔ جاری |
| `ReviseAndResubmit` | feedback را نمایش دهید؛ همان پرونده اصلاح و دوباره Submit شود |
| `WaitForReview` | حالت انتظار؛ Edit غیرفعال |
| `WaitForCommercialConfiguration` | تنظیم قیمت سرور منتظر Platform است |
| `RequestQuote` | پیش‌نمایش شرایط معتبر سرور دریافت شود |
| `Pay` | Order جاری را از Commerce بخوانید و پرداخت را ادامه دهید |
| `AwaitProvisioning` | پرداخت دوباره درخواست نشود؛ وضعیت آماده‌سازی خوانده شود |
| `ReadCurrentRevision` | snapshot تاریخی readonly؛ برای اقدام، نسخهٔ جاری را بخوانید |
| `None` | اقدام خودکار بعدی نداریم؛ وضعیت نهایی و دلیل امن نمایش داده شود |

`progress` وضعیت تجاری، `orderId/paymentState` و وضعیت/شناسهٔ Provisioning و در آینده شناسهٔ Organization/Subscription تکمیل‌شده را جدا می‌دهد. `BlockedManagerPolicy` یعنی پرداخت محفوظ و نیازمند سیاست مدیر اولیه؛ خطای درگاه نیست. اطلاعات Reviewer در پاسخ مالک حداقل است؛ `applicantUserId` فقط در projection مجاز Admin پر می‌شود.

هنگام loading دکمهٔ تکراری نسازید؛ empty فهرست با failure یکی نیست. ۴۰۹ نسخه نیازمند refresh همان پرونده است؛ خطای شبکه نیازمند Retry همان operation است. اعلان موجود `SchoolApplicationUpdated` متن عمومی و target داخلی دارد؛ Client باید detail را دوباره از سرور بگیرد، نه از متن اعلان نتیجهٔ مالی بسازد.

## Commerce و پرداخت

`quote.previewOnly=true`؛ Quote ذخیرهٔ خصوصی یا سند قیمت تضمین‌شدهٔ مستقل نیست. Order دوباره شرایط جاری سرور را resolve و سپس قیمت/واحد/نسخه/شرایط را freeze می‌کند. تغییر نگاشت، Order ساخته‌شده را بازنویسی نمی‌کند. `SchoolApplicationId` در Quote و Order صریح است. مبلغ Order به `IRR` و `priceToman` در Quote تومان است؛ تبدیل UI نباید مرجع Verify شود.

مسیرهای موجود را reuse کنید:

```text
POST /api/v1/commerce/personal/orders/{orderId}/payments
  { clientOperationId }
GET /api/v1/commerce/personal/orders/{orderId}
GET /api/v1/commerce/payment-attempts/{attemptId}
POST /api/v1/commerce/payment-attempts/{attemptId}/verify
GET /api/v1/commerce/personal/orders/{orderId}/receipt
```

Callback زیبال همان مسیر integration موجود است؛ بازگشت مرورگر سند موفقیت نیست. نتیجهٔ server verification و Order را بخوانید. پرداخت نامطمئن را با ساخت تلاش/سفارش جدید دور نزنید.

یک Order جاری به‌ازای هر پرونده مجاز است. فقط Order منقضی که **هیچ PaymentAttempt** ندارد، با intent تازه و ClientOperationId تازه می‌تواند اتمیک Cancel و جایگزین شود. Retry شناسهٔ قبلی همان Order تاریخی Cancelled را می‌دهد؛ موعد قبلی تمدید نمی‌شود. وجود هر تلاش بانکی یا Paid بودن، جایگزینی را مسدود می‌کند تا مسیر recovery موجود وضعیت را روشن کند. سیاست refund یا جابه‌جایی پرداخت اختراع نشده است.

## خطا و Retry

| HTTP | Code مهم | اقدام |
|---|---|---|
| ۴۰۰ | `SCHOOL_APPLICATION_INVALID` | فرمت/محدودهٔ ورودی اصلاح شود |
| ۴۰۰ | `SCHOOL_APPLICATION_SUBMISSION_INCOMPLETE` | detail و missingRequirements خوانده شود |
| ۴۰۱ | قرارداد احراز هویت موجود | نشست معتبر لازم است؛ حساب معلق نیز در pipeline نشست رد می‌شود |
| ۴۰۳ | `SCHOOL_APPLICATION_PERMISSION_REQUIRED` | عملیات Platform/تجاری مجاز نیست |
| ۴۰۴ | `SCHOOL_APPLICATION_NOT_FOUND` / `SCHOOL_APPLICATION_FILE_UNAVAILABLE` | وجود resource خصوصی استنتاج نشود |
| ۴۰۹ | `SCHOOL_APPLICATION_CONFLICT` | نسخه/وضعیت/operation یا Order جاری بررسی شود |
| ۴۰۹ | `SCHOOL_APPLICATION_NOT_APPROVED` | پیش از تأیید پرداخت باز نشود |
| ۴۰۹ | `SCHOOL_APPLICATION_COMMERCIAL_TERMS_UNAVAILABLE` | تنظیم معتبر Platform لازم است |

کدهای پایدار Commerce/Storage/Identity نیز بدون تغییر برقرارند. `ProblemDetails` و correlation پاسخ را برای پشتیبانی نگه دارید؛ Token، مدرک، request body خصوصی یا اطلاعات بانکی را در analytics/log ثبت نکنید. همان operation با payload متفاوت conflict است. Retry موفق Submit/Review/Cancel رویداد و اعلان تکراری ایجاد نمی‌کند؛ پاسخ جزئیات می‌تواند نمای جاری پرونده باشد، نه snapshot قدیمیِ عین response.

محدودیت فعلی: نگاشت/قیمت واقعی بیرون از تست تنظیم نشده و سیاست مدیر اولیه تصمیم باز است. این سند پذیرش روی سرور، پرداخت واقعی یا آموزشگاه آماده را اعلام نمی‌کند.
