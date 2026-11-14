# قرارداد مصرف حذف حساب — BE-22

بازبینی `2026-11-14`؛ مبنا، تصمیم صریح BE-22 و پیاده‌سازی روی `feature/product-v6-backend-alignment` است. این سند شاهد اجرای حذف روی سرور نیست. Frontend در این مرحله تغییر نکرده است.

## وضعیت و مرز قابلیت

حذف حساب، حذف فیزیکی `User` یا `Person` نیست. حساب مسدود و هویت جاری خنثی می‌شود؛ نشست‌ها و دسترسی‌ها پایان می‌یابند. دادهٔ خصوصی دفترچه پاک می‌شود و حذف منابع شخصی از Knowledge با checkpoint مستقل پیگیری می‌شود. پیام مشترک، آموزش، پرداخت و شواهد حفظ می‌شوند؛ جزئیات در [ماتریس داده](ACCOUNT_DELETION_DATA_MATRIX_FA.md) است.

پیش‌فرض مشترک و نمونهٔ Test/Production:

```json
"AccountDeletion": {
  "ExecutionEnabled": false,
  "GracePeriodHours": null
}
```

برای اجرا، اپراتور باید `ExecutionEnabled=true` و مقدار ساعتی صریح و مصوب تعیین کند. عدد حقوقی پیش‌فرض وجود ندارد. Test فقط در fixture ایزوله با مهلت صفر اجرا می‌شود؛ این عدد پیشنهاد Production نیست. فایل‌های خصوصی Test/Production تغییر نکرده‌اند.

## درخواست و وضعیت صاحب حساب

| Method / Route | قرارداد |
|---|---|
| `POST /api/v1/me/deletion-request` | نشست معتبر؛ بدنه فقط `clientOperationId` غیرخالی؛ پاسخ `202` از نوع `AccountDeletionStatusResult` |
| `GET /api/v1/me/deletion-request` | فقط درخواست حسابِ نشست جاری؛ `200` یا در نبود درخواست `204` |

Client نباید `userId`، زمان اجرا، وضعیت نهایی، scope منابع یا مهلت را تعیین کند. وجود نقش سازمانی یا کامل‌بودن پروفایل شرط ثبت درخواست نیست؛ حساب و نشست باید معتبر باشند.

فیلدهای پاسخ: `id`, `requestedAtUtc`, `status`, `executionEnabled`, `scheduledExecutionAtUtc`, `policyVersion`, `startedAtUtc`, `completedAtUtc`, `attemptCount`, `safeFailureCode`, `failureStep`, `version`. `version` همان `rowversion` به‌صورت Base64 است؛ Token ورود نیست.

| `status` | رفتار کلاینت |
|---|---|
| `Requested` | رسید ثبت شده است؛ وقتی اجرا غیرفعال است، زمان حدسی نمایش ندهید. |
| `Scheduled` | فقط `scheduledExecutionAtUtc` ذخیره‌شدهٔ سرور را نمایش دهید؛ ساعت دستگاه مرجع اجرا نیست. |
| `Processing` | اجرای مرحله‌ای شروع شده؛ ممکن است Knowledge هنوز تأیید نکرده باشد. |
| `NeedsAttention` | اجرای کامل اثبات نشده است؛ مرحله/کد امن را نمایش دهید و به مسیر پشتیبانی عملیاتی ارجاع دهید. |
| `Completed` | هر هشت checkpoint و invariant نهایی تأیید شده‌اند؛ معنی آن پاک‌شدن همهٔ تاریخچه‌ها یا Backupها نیست. |

قبلاً پاسخ receipt از رشتهٔ `PRODUCT_LEGAL_DECISION_REQUIRED` استفاده می‌کرد؛ وضعیت جاری همان lifecycle بالاست. زمان‌های گذشته با Migration به موفقیت تبدیل نمی‌شوند.

Retry همان Create، حتی با OperationId دیگر، درخواست دوم یا deadline تازه ایجاد نمی‌کند؛ هر User فقط یک lifecycle دارد. Retry شبکه مجوز ساخت حساب، نقش یا پیام تازه نیست.

## ابطال نشست و نمایش هویت

در `FreezeAccount` همهٔ نشست‌ها، شامل نشست درخواست‌کننده، و تمام Refresh Tokenهای آن‌ها revoke می‌شوند. درخواست‌های بعدی کاربر پاسخ `401` می‌گیرند؛ از روی این پاسخ، `Completed` استنتاج نکنید. رسید اولیه برای ارجاع نگه داشته شود و وضعیت عملیاتی فقط از صف مجاز Backoffice پیگیری شود. endpoint عمومیِ پیگیری با شناسه یا نشست جعلی اضافه نشده است.

خوانندهٔ مجازِ پیام تاریخی، از زمان freeze هویت «حساب حذف‌شده»، username داخلی خنثی و بدون عکس می‌بیند. این نمایش مجوز Discovery، Send یا مشاهدهٔ پروفایل حساب غیرفعال ایجاد نمی‌کند. متن پیام و snapshotهای محتواییِ تاریخی بازنویسی نمی‌شوند. Blockهای ایمنی باقی می‌مانند و نمایش حساب حذف‌شده در فهرست Block نیز خنثی است.

شمارهٔ Credential بعد از ناشناس‌سازی از حساب قبلی جدا می‌شود. ثبت‌نام دوباره با همان شماره، `UserId` تازه می‌سازد و هیچ عضویت، پیام، دفترچه، پرونده یا سابقهٔ مالی حساب قبلی را منتقل نمی‌کند. username قبلی نیز رزرو دائمی نمی‌شود.

## Backoffice

| Method / Route | Permission و نتیجه |
|---|---|
| `GET /api/admin/v1/privacy/deletion-requests?page=1&pageSize=50` | `platform.privacy_requests.read`؛ صفحه تا۱۰۰، فقط شناسه و وضعیت امن |
| `GET /api/admin/v1/privacy/deletion-requests/{requestId}` | همان Read؛ `AccountDeletionDetail` شامل `item` و هشت `steps` |
| `POST /api/admin/v1/privacy/deletion-requests/{requestId}/retry` | Read و `platform.privacy_requests.manage`؛ فعلاً Manage فقط برای Platform Owner؛ پاسخ `200` |

بدنهٔ Retry: `clientOperationId` تازه و `expectedVersion` از پاسخ جاری. فقط `NeedsAttention` قابل ادامه است. مجوز و نشست جاری داخل transaction دوباره بررسی می‌شوند. تکرار همان عملیاتِ ادامه، حتی پس از تکمیل، receipt قبلی را می‌شناسد؛ مرحلهٔ موفق و تاریخ اصلی را تغییر نمی‌دهد.

Detail هر مرحله `step`, `status`, `attemptCount`, `startedAtUtc`, `completedAtUtc`, `safeFailureCode` دارد. `status` مرحله یکی از `Pending/Processing/Completed/Failed` است؛ تعداد Claim با تعداد حذف منبع یکسان نیست.

هیچ endpoint برای Force Complete، حذف SQL دستی، دورزدن آخرین مدیر یا ساخت هویت/نشست جانشین وجود ندارد.

## خطاها

| HTTP / Code | اقدام |
|---|---|
| `400` ورودی نامعتبر | شناسهٔ عملیات غیرخالی و نسخهٔ غیرخالی با طول حداکثر۳۲ را ارسال کنید. |
| `401` نشست نامعتبر/لغوشده | از Refresh برای احیای حساب حذف‌شونده استفاده نکنید. |
| `403` permission نامعتبر | نقش Platform با permission واقعی را بررسی کنید؛ نقش School مجوز مدیریت Privacy نیست. |
| `404 ACCOUNT_DELETION_NOT_FOUND` | درخواست برای مسیر مدیریتی قابل دسترسی نیست. |
| `409 ACCOUNT_DELETION_CONFLICT` | قالب Base64 نسخه باید معتبر و نسخه/وضعیت با پاسخ جاری یکسان باشد؛ Detail تازه را بخوانید و وضعیت را سمت Client overwrite نکنید. |
| `409 ACCOUNT_DELETION_EXECUTION_DISABLED` | اپراتور باید gate و مهلت مصوب را تعیین کند؛ UI نباید اجرای موفق نشان دهد. |

کدهای `safeFailureCode` مثل `ACCOUNT_DELETION_LAST_ORGANIZATION_MANAGER`, `ACCOUNT_DELETION_LAST_PERSONAL_CLASS_MANAGER`, `ACCOUNT_DELETION_LAST_PLATFORM_OWNER`, `ACCOUNT_DELETION_CONVERSATION_OWNERSHIP`, `ACCOUNT_DELETION_SCHOOL_MANAGER_DEPENDENCY` و `ACCOUNT_DELETION_KNOWLEDGE_UNAVAILABLE` نتیجهٔ Worker هستند؛ با خطای HTTP درخواست Create یکی نیستند. انتقال مسئولیت و پذیرش دعوت فقط از workflow موجود و با رضایت انجام می‌شود.

خطاها با ProblemDetails و correlation موجود باقی می‌مانند؛ شماره، نام قبلی، متن خصوصی، فایل، credential و پاسخ خام AI در دلیل یا log حذف ثبت نمی‌شوند.

منابع: [Service](../../MakanApp.Application/Identity/AccountDeletionService.cs)، [Workflow](../../MakanApp.Application/Identity/AccountDeletionExecution.cs)، [Controller](../../MakanApp.Api/Controllers/AccountDeletionController.cs)، [Runbook](../operations/ACCOUNT_DELETION_RUNBOOK_FA.md).
