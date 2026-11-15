# قرارداد فایل، Library و چرخهٔ منبع آموزشی — BE-15

مبنا: `54d07d2b2aaf1c19a3780f00dcd1cd5c51dec2e5`؛ تاریخ `2026-11-15`.
این سند رفتار Backend را توضیح می‌دهد؛ شاهد Deploy یا آزمون روی سرور نیست.

## شناسه، مجوز و دانلود

`FileAssetId` آدرس دائمی یا مجوز دانلود نیست. همیشه endpoint احرازشدهٔ Backend را صدا بزنید:

| عملیات | قرارداد موجود |
|---|---|
| آپلود | `POST /api/v1/files`؛ multipart با `File`، `workspaceScope` اختیاری و `PersonalClassId` اختیاری |
| metadata | `GET /api/v1/files/{fileId}` |
| بایت فایل | `GET /api/v1/files/{fileId}/content` |
| حذف فایل آزاد | `DELETE /api/v1/files/{fileId}?expectedRowVersion=...` |
| فهرست منبع | `GET /api/v1/sources`؛ scope و pagination فعلی |
| منبع و نسخه | `GET /api/v1/sources/{id}?revision=...` |
| دانلود منبع | `GET /api/v1/sources/{id}/content?revision=...` |

موفقیت دانلود بایت خام با MIME معتبر است، نه JSON. Range پس از مجوز جاری پردازش می‌شود:
`200` فایل کامل، `206` بازهٔ معتبر، `416` بازهٔ نامعتبر برای فایل مجاز. پاسخ خطای
ProblemDetails را Blob موفق ذخیره نکنید. دانلودها `Cache-Control: private, no-store`
و `Vary: Authorization` دارند. پاسخ `404` پس از revoke یا پنهان‌شدن منبع رفتار مورد انتظار است؛
با retry یا مسیر raw file آن را دور نزنید. `403` Library به معنی مجوز دانلود عادی نیست.

`FILE_IN_USE` با `409` برای فایل bound/retained است؛ حتی uploader نمی‌تواند سابقهٔ رسمی را
با DELETE عمومی حذف کند. فایل آزاد به uploader و scope جاری و rowversion صحیح نیاز دارد.
نسخهٔ کهنه `CONCURRENCY_CONFLICT` و حذف مجدد `FILE_ALREADY_DELETED` می‌گیرد؛
نام دقیق کد نسخه از قرارداد `StorageErrorCodes` پیروی می‌کند.

## وضعیت‌های مستقل

- `Pending`: آپلود کامل نشده است؛ فایل قابل دانلود/منبع قابل انتشار نیست.
- `Ready`: بایت کامل، اندازه و hash سمت سرور ثبت شده و اعتبارسنجی فنی Upload عبور کرده است.
- `Rejected`: شکست Upload/اعتبارسنجی؛ منبع آماده یا قابل استفاده برای AI نمی‌شود.
- `Deleted`: حذف منطقی؛ مسیر خواندن عمومی آن را `404` می‌کند.

`FileValidated` فقط سطح اعتبارسنجی فنی فعلی است؛ اسکن بدافزار یا تأیید انسانی نیست.
`ReviewRequired` نه Ready ایمنی است و نه وعدهٔ صف Human Review. فایل PendingReview پیام
نباید به گیرنده Delivered/Downloadable نمایش داده شود. این مرحله مسیر تأیید رسانه نمی‌سازد.

## منبع آموزشی

Scopeهای `Personal / Organization / Class / Library` مالکیت مستقل دارند. Organization/Class
متعلق به سازمان‌اند؛ خروج یا حذف حساب سازنده مالکیت را شخصی یا منبع را حذف نمی‌کند.
Parent از GuardianRelation مجوز عمومی Source کلاس یا فایل پاسخ خصوصی نمی‌گیرد.
کلاس مستقل همان PersonalClass است و به Class سازمانی تبدیل نمی‌شود.

ایجاد نسخه، انتشار، آرشیو و visibility از endpointهای موجود Source با Version و Reason انجام
می‌شوند. ویرایش نسخهٔ Published ممنوع است؛ نسخهٔ جدید بسازید. دانشجو Draft را نمی‌بیند.
Archive نسخه و blob را پاک نمی‌کند ولی دسترسی عادی، انتخاب AI و Knowledge/RAG را با ACL
جاری قطع می‌کند؛ باقی‌بودن بردار در upstream مجوز استفاده نیست.

SourceReady به Source فعال، File Ready و Safety=FileValidated نیاز دارد. Publication و
Knowledge Processing وضعیت‌های جدا هستند. انتخاب AI علاوه بر این‌ها Published و Knowledge Ready
و مجوز/سهمیهٔ جاری می‌خواهد. این مرحله AI Runtime را فعال یا تنظیم نمی‌کند.

## Library و تغییر صریح BE-15

کاربر عادی واردشده فقط metadata منبع `Public + Active + Published` را می‌بیند؛ `file=null`،
`CanDownload=false` و `CanUseForAi=false` باقی می‌مانند. School Manager به‌خودی‌خود Library Manager نیست.
مدیریت همان permission موجود `platform.library.sources.manage` را می‌خواهد.

طبق درخواست BE-15، Hidden اکنون هم از List و هم از Get مستقیم برای خوانندهٔ عادی پنهان است.
رفتار تاریخی نگه‌داشتن metadata با reference مستقیم، دیگر قرارداد جاری نیست.
نویسندهٔ مجاز همچنان تاریخچهٔ مدیریتی را طبق قرارداد می‌بیند. عادی‌کردن دانلود یا AI Library
در این مرحله انجام نشده است.

## عکس، زمان‌بندی و پرونده‌ها

عکس پروفایل پس از Unlink مجوز Avatar دیگران را از دست می‌دهد؛ uploader ممکن است هنوز طبق
قرارداد فایل آزاد شخصی خودش به نسخهٔ اصلی دسترسی داشته باشد. Unlink خودبه‌خود حذف بایت نیست.
عکس گفتگو فقط با عضویت/ACL جاری همان گفتگو خوانده می‌شود و با تغییر Workspace عمومی نمی‌شود.
پیوست Scheduled پیش از Dispatch فقط برای فرستندهٔ دارای دسترسی جاری است؛ گیرنده با FileId آن را نمی‌خواند.
شاهد صرفاً Moderation از endpoint تخصصی پرونده با مجوز پرونده و Audit جاری خوانده می‌شود، نه fallback uploader.

## همگام‌سازی و محدودیت‌ها

رویدادهای موجود `knowledge.source` و `knowledge.library` invalidation هستند؛ پس از دریافت،
snapshot مجاز را refetch کنید. revocation باید قبل از refetch/replay دوباره رعایت شود؛
event قدیمی مجوز جدید ایجاد نمی‌کند. هیچ event یا endpoint جدیدی برای این مرحله ساخته نشد.

`LIBRARY_PUBLIC_DOWNLOAD=PRODUCT_POLICY_DISABLED`؛ `LIBRARY_AI=PRODUCT_POLICY_DISABLED`؛
`MEDIA_REVIEW_APPROVAL=PRODUCT_DECISION_REQUIRED`؛ `READY_ORPHAN_PURGE=POLICY_PENDING`.

[ماتریس دسترسی و شواهد](FILES_LIBRARY_ACCESS_MATRIX_FA.md) و
[گزارش توسعه‌دهنده](FILES_LIBRARY_DEVELOPER_REPORT_FA.md) جزئیات را دارند.
