# گزارش توسعه‌دهندهٔ BE-15 — فایل و منبع آموزشی

تاریخ `2026-11-15`؛ مبنا `54d07d2b2aaf1c19a3780f00dcd1cd5c51dec2e5`؛ شاخه `feature/product-v6-backend-alignment`.

## ۱. خلاصهٔ مرحله

Storage، Source، Library، Knowledge و cleanup از قبل وجود داشتند. مسئله ساختن سیستم دوم نبود:
فهرست اتصال‌های DELETE با فهرست خواندن یکسان نبود؛ Scheduled binding شناسایی نمی‌شد؛ ساخت Source
فایل School/Support/Scheduled را مستثنا نکرده بود. Hidden نیز در Get مستقیم metadata را حفظ می‌کرد.
اکنون فهرست واقعی مشترک است؛ retention و SQL rowversion اتصال Scheduled را fence می‌کنند؛
Hidden مطابق درخواست BE-15 برای کاربر عادی در Get هم پنهان است. Safety hold از مسیر Pending قابل laundering نیست.

## ۲. معماری

Domain: فقط قانون انتقال Safety در EducationalSourceVersion محدود شد؛ EF/HTTP اضافه نشد.
Application: StorageService برای DELETE از پورت موجود IsBoundAsync استفاده می‌کند.
Infrastructure: FileAssetBindingQuery وابستگی‌های واقعی SQL را یک‌جا می‌پرسد؛ Resolver، SourceStore
و TerminalCleanup آن را reuse می‌کنند. ScheduledStore در transaction موجود فایل را retain/touch می‌کند.
Controller جدید یا منطق SQL/OTP در Api ایجاد نشد. .NET9، EF Core9، SQL Server و Onion ثابت‌اند.
در Api فقط دانلودهای موجود Reports/ConversationModeration/PlatformModeration هدرهای
`private, no-store` و `Vary: Authorization` گرفتند؛ مجوز تخصصی و Route تغییر نکرد.

## ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت و دلیل | نکتهٔ آموزشی |
|---|---|---|
| MakanApp.Infrastructure/Storage/FileAssetBindingQuery.cs | فهرست واحد FK/reference واقعی برای read/delete/bind/cleanup | query داخلی مشترک، بدون GenericRepository یا چرخهٔ DI بین Source و File |
| tests/MakanApp.IntegrationTests/FileLifecycleAssertions.cs | پذیرش مشترک DELETE با rowversion واقعی SQL | فقط HTTP status کافی نیست؛ عدم تغییر رکورد و bytes هم بررسی می‌شود |
| tests/MakanApp.IntegrationTests/FilesLibraryLifecycleTests.cs | Scheduled/Archive/Safety/race | revoke با URL و Range قدیمی و رقابت SQL باید آزموده شود |
| tests/MakanApp.IntegrationTests/FilesLibraryCaseBindingTests.cs | School/Support با marker قدیمیِ خالی | نبود RetainedAtUtc مجوز حذف resource دارای FK نیست |
| docs/product-v6/FILES_LIBRARY_ACCESS_MATRIX_FA.md | ماتریس binding/actor/route/revoke | وجود نام فایل یا SourceId به معنی capability نیست |
| docs/product-v6/FILES_LIBRARY_FRONTEND_HANDOFF_FA.md | قرارداد قابل‌اقدام Client | خطای ProblemDetails را فایل موفق ذخیره نکنید |

## ۴. فایل‌های مهم تغییرکرده

- StorageService.cs: DELETE همهٔ bindingها را از IsBoundAsync می‌پرسد، علاوه بر retention موجود.
- EfFileAssetBoundAccessResolver.cs: inventory مشترک و خواندن خصوصی intent با ACL فعلی Sender؛ هیچ permission تازهٔ Moderation نمی‌دهد.
- TerminalFileCleanupProcessor.cs: همان فهرست reference؛ grace، قفل، مسیر و وضعیت‌های مجاز تغییر نکردند.
- EfEducationalSourceStore.cs: جلوگیری از bind فایل پرونده/زمان‌بندی و پنهان‌کردن Hidden برای عادی.
- EfMessagingStore.Scheduling.cs: Retain و touch فایل در transaction قصد ارسال، برای FK و rowversion race.
- EducationalSourceVersion.cs: hold نمی‌تواند به Pending برگردد و بعد FileValidated شود.
- تست‌های پذیرش Academic/PersonalClass/Report/Moderation/ConversationPhoto: assertion حذف bound افزوده شد؛ مجوز و deadline اصلی حفظ‌اند.
- EducationalSourcesAuthorizationTests: انتظار تاریخی Hidden مستقیم به 404 قرارداد جدید تغییر کرد.
- MessagingP2ScheduledSafetyTests: تزریق خرابی فقط در SQL تست، چون DELETE عادی retained اکنون با constraint درست رد می‌شود.
- ReportsController، ConversationModerationController و PlatformModerationController: دانلود خصوصی cache نشود؛ تست‌های دریافت مجاز، هدرهای واقعی HTTP را بررسی می‌کنند.

## ۵. جریان اجرا

خواندن: HTTP → FilesController → StorageService.GetAuthorizedAsync → IsBoundAsync →
EfFileAssetBoundAccessResolver → ACL مصرف‌کنندهٔ جاری در SQL → FileStorage.OpenReadAsync → stream.
حذف: HTTP → StorageService.DeleteFileAsync → uploader/scope → Retained یا IsBound →
FILE_IN_USE؛ فقط فایل آزاد با rowversion صحیح MarkDeleted/Save و سپس حذف bytes می‌شود.
Scheduled: Store در transaction موجود، فایل معتبر را Retain و Status را modified می‌کند؛
ساخت intent و تغییر rowversion با Commit رسمی می‌شوند. stale writer با ConcurrencyConflict شکست می‌خورد.
Source: EducationalSourcesController → EducationalSourceService → EfEducationalSourceStore →
Domain + SQL + Audit/Outbox موجود → Commit → DTO؛ invalidation موجود باعث refetch مجاز می‌شود.

## ۶. دیتابیس

Migration، جدول، ستون، FK یا index جدید لازم نیست. FileAsset.RowVersion، RetainedAtUtc،
constraint موجود CK_FileAssets_RetentionState و referenceهای موجود کافی‌اند. SQL همچنان منبع
حقیقت است. قفل/transaction bind موجود و optimistic concurrency فایل به جای check-then-write بی‌محافظند.
هیچ migration روی MakanApp، MakanApp_Test یا سرور اجرا نمی‌شود؛ تست‌ها DB مختص خود را دارند.

## ۷. Endpointها

API جدیدی ساخته نشد. Upload/metadata/content/DELETE موجود و Source List/Get/draft/version/
publish/archive/visibility/content reuse شدند. درخواست‌های تغییر Source همان Version/Reason فعلی
را دارند؛ errorهای NotFound/NotReady/NotAllowed/Conflict ثابت‌اند. Download Range پس از auth است.
Library عادی metadata دارد و download ندارد. جزئیات route و HTTP در Handoff و ماتریس آمده است.

## ۸. مفاهیم فنی

Binding، ownership و upload provenance سه مفهوم جدا هستند. Retention مانع پاک‌سازی سابقه است،
ولی جای ACL را نمی‌گیرد. Rowversion تغییر binding هم‌زمان را تشخیص می‌دهد. Source publication،
file validation و Knowledge processing سه محور مستقل‌اند. Outbox موجود invalidation را پس از Commit منتشر می‌کند.

## ۹. تصمیم‌ها و trade-off

به جای تکرار queryها یا تزریق دو Store به هم، query داخلی مشترک انتخاب شد؛ هزینهٔ آن چند AnyAsync
روی index/FKهای موجود است و مزیت، جلوگیری از drift امنیتی است. هیچ schema برای history عکس یا
دانلود expiry حدسی اضافه نشد. Moderation از endpoint تخصصی با مجوز پرونده و Audit موجود استفاده می‌کند؛
raw FileId به permission تازه تبدیل نشده است. Read-only metadata مدیریتی Archived با مجوز دانلود یکی نیست.
تغییر Hidden صریحاً از درخواست BE-15 است؛ رفتار تاریخی 200 در گزارش قدیمی شاهد قرارداد جاری نیست.

## ۱۰. تست‌ها

دو Unit و هشت case جدید Integration اضافه شدند. Unitها راه برگشت hold به Pending را می‌بندند.
تست‌های Integration جدید، Scheduled private
download، source reuse، DELETE بدون marker، race SQL، Archive/Range و Safety version retry را
می‌سنجند. assertionهای جدید روی پذیرش‌های واقعی موجود، FILE_IN_USE و عدم حذف bytes را اثبات می‌کنند.
آزمون‌های Architecture بدون تغییر قواعد Onion اجرا می‌شوند. تست‌های AI/Knowledge و providerها
کنترل‌شده‌اند؛ از موفقیت محلی، اجرای AI/SMS/Push واقعی استنتاج نمی‌شود.

شکست مشاهده‌شدهٔ نخست: P2ScheduledAttachmentRevokedAfterQueueCannotBePublished در Fixture
با CK_FileAssets_RetentionState برخورد کرد؛ constraint سالم بود. تزریق خرابی خارجی در SQL ایزوله
جایگزین mutation عادی Fixture شد؛ assertionهای عدم ارسال/Failed بدون تغییر حفظ شدند. اجرای
تشخیصی و خروجی‌های قبلی خصوصی نگهداری می‌شوند؛ شکست با rerun پنهان نمی‌شود.

## ۱۱. Validation

Clean/Restore، Build و Build نهایی واقعاً موفق، با صفر Warning و Error بودند.
اجرای نهایی کامل: **۱۳۴۸ Unit +۲۰۲۶ Integration +۱۴ Architecture =۳۳۸۸ Passed**،
Failed=0 وSkipped=0؛ مدت Integration نهایی۳۵دقیقه و۴۵ثانیه است. هر۱۱۷ تست Zibal پاس‌اند.
EF مدل معلق ندارد و Migration جدید ایجاد نشده است. Focused جای full نیست.
پس از این اجرا فقط مستندات و یک توضیح کد دربارهٔ مجوز جاری Moderation اصلاح شد؛
منطق اجرایی، Assertionها، Timeoutها و تنظیمات عوض نشدند.

شواهد مرحله‌ای، مستقل از نتیجهٔ نهایی:

| اجرا | نتیجهٔ واقعی |
|---|---|
| Focused پس از اصلاح Fixture فایل retained | ۵۶ Unit +۱۳۹ Integration، همه موفق، بدون Skip |
| نخستین Full | ۱۳۴۸ Unit و۱۴ Architecture موفق؛ Integration برابر۲۰۱۹ موفق،۷ ناموفق از۲۰۲۶؛ جمع۳۳۸۸، بدون Skip |
| Build نسخهٔ جاری | موفق، صفر Warning و Error |
| Focused نسخهٔ جاری | ۱۵۵ Integration موفق؛ شامل هفت شکست قبلی، guardهای SQL بدون marker و Cache خصوصی |
| Full نسخهٔ جاری با Console عادی | ۱۳۴۸ Unit و۱۴ Architecture موفق؛ ۲۰۲۵ Integration موفق ویک ناموفق؛ جمع۳۳۸۸، بدون Skip |
| تشخیص مستقل Lookup با همان کد | یک Integration موفق در۳ثانیه؛ مجوز و Assertion تغییر نکردند |
| Build نهایی | موفق، صفر Warning و Error |
| Full نهایی با Console کم‌حجم | ۱۳۴۸ Unit +۲۰۲۶ Integration +۱۴ Architecture =۳۳۸۸ موفق؛ صفر Failed/Skipped؛ هر۱۱۷ تست Zibal موفق |

در Full نخست شش انتظار Realtime به Timeout یا لغو ناشی از budget تست رسید و
AccountRealtimeExpiredPublisherCannotCompleteRecoveredClaim با Expected=2/Actual=1 شکست خورد.
نام دقیق شش انتظار ناموفق Realtime در همان اجرا:

| تست | خطای مشاهده‌شده |
|---|---|
| PersonalAccountExplicitReplayAndLiveWorkWithoutChangingSelectedTeacherOrganization | OperationCanceledException |
| OfficialSpacesManagerGetsFirstActivityWithoutOpeningConversationAndReplayHasNoDuplicateEffect | OperationCanceledException |
| RevokingOneSessionStopsRestHubAndFuturePrivateEventsButKeepsOtherSessionWorking | TimeoutException |
| StabilizationTwoIndependentReceiverSessionsSynchronizeReadWithoutRegression | TimeoutException |
| P2SilentStillPersistsUnreadRealtimeAndInternalNotificationButSuppressesOnlyExternalDelivery(silent: False) | TimeoutException |
| P2SilentStillPersistsUnreadRealtimeAndInternalNotificationButSuppressesOnlyExternalDelivery(silent: True) | TimeoutException |

نام، متن خطا و TRX این اجرا خصوصی حفظ شده‌اند. Fixture سازندهٔ ReplayProcessor چهار dependency
اختیاری جدید میزبان را نداشت؛ اکنون همان handlerهای واقعی و ProductNotificationAccess را از DI
می‌گیرد، تا workflowهای دیگر دیتابیس تست به علت handler مفقود poison نشوند. Production Dispatcher،
سقف تلاش، Timeout، Assertion و قواعد authorization برای سبزکردن آزمون تغییر نکردند.
موفقیت Focused به‌تنهایی اثبات رفع ناپایداری زیر بار Full نیست؛ Full نهایی موفق شد، اما علت قطعی
هر شش Timeout دور اول اثبات نشده است. `REALTIME_TIMEOUTS_OBSERVED=6` گزارش می‌شود.

هفت شکست دور اول در Full دوم تکرار نشدند. شکست دوم، Timeout درخواست در حلقهٔ فیلترهای
نامعتبر ProvisioningLookupValidatesFilterAndRequiresPlatformReadRatherThanOrganizationRole،
خط۸۳ PlatformProvisioningLookupTests.cs بود؛ دریافت OpenAPI در خط۸۴ هنوز آغاز نشده بود.
این خطا Assertion مخالف permission یا Filter نبود. Console عادی صدها مگابایت خروجی داشت؛
اثر فشار I/O/منابع یک احتمال است، نه علت قطعی اثبات‌شده. اجرای نهایی به Console کم‌حجم
برگشته است؛ کد، سقف HTTP و Assertion این تست تغییر نکردند. لاگ و TRX هر دو دور ناموفق حفظ‌اند.

## ۱۲. Git

Commit کد و تست: `d0b2c3085b12a07798c7f3f70f16e20146d68458` با پیام
`feat(storage): harden bound file authorization and lifecycle`؛ شامل۲۸ فایل،۱۰ فایل اجرایی
و۱۸ فایل تست/کمک‌تست است. Branch=`feature/product-v6-backend-alignment`،
Remote=`origin`، URL=`https://github.com/RastinaxAI/MakanApp.git` و
Upstream=`origin/feature/product-v6-backend-alignment`.
Push این Commit موفق شد؛ پس از fetch، HEAD و upstream و نتیجهٔ مستقیم ls-remote همگی
`d0b2c3085b12a07798c7f3f70f16e20146d68458` بودند. مستندات در Commit جداگانهٔ همین مرحله
ثبت می‌شوند؛ SHA نهایی آن در گزارش نهایی و git log قابل مشاهده است.
سه فایل ناتمام tools/ai، چهار appsettings خصوصی و سه فایل MeliPayamak با Backup و SHA-256
محافظت شده‌اند؛۱۰ فایل و Backupهایشان بدون تغییرند. هیچ credential، bytes یا فایل generated/private
وارد Commit کد نشده است. فایل‌های dirty باقی‌ماندهٔ اولیه دقیقاً این‌ها هستند:

```text
tools/ai/provider_diagnostic.py
tools/ai/run_exact_ai.py
tools/ai/test_provider_diagnostic.py
```

Merge main، Publish، Deploy و migration دیتابیس‌های معمول جزو این مرحله نیستند.

## ۱۳. محدودیت‌های فعلی

دانلود عادی Library و Library AI طبق Product بسته‌اند. ReviewRequired فرایند تأیید جدید ندارد.
Ready orphan purge و retention حقوقی/دانلود TTL جدید اختراع نشده‌اند. FileValidated فقط validation
فنی Upload است؛ malware/quality scan و OCR نیست. فایل Unlinkشدهٔ Avatar ممکن است هنوز با مجوز
فایل آزاد شخصی برای uploader قابل خواندن/حذف باشد؛ مجوز عمومی عکس قطع است.

## ۱۴. نکات آموزشی

۱. FileId selector است، نه مجوز. ۲. delete و cleanup باید همهٔ referenceها را بشناسند.
۳. فایل متصل بدون marker نیز bound است. ۴. تغییر binding باید stale deletion را fence کند.
۵. وضعیت‌های Safety را از مسیر واسط هم محافظت کنید. ۶. تست Fixture می‌تواند با constraint درست
بشکند؛ برای سبزکردن تست محافظ Production را حذف نکنید. ۷. policy-disabled با implementation failure یکی نیست.

## ۱۵. مسیر مطالعه

FilesController → StorageService → StorageAbstractions → FileAsset → FileAssetBindingQuery →
EfFileAssetBoundAccessResolver/Academic → EfEducationalSourceStore/Reads → EducationalSourceVersion →
EfMessagingStore.Scheduling → FileAssetConfiguration → FilesLibraryLifecycleTests/CaseBindingTests →
تست‌های موجود Knowledge/Academic/Photo/TerminalCleanup. Migration تازه‌ای برای خواندن وجود ندارد.

## ۱۶. مرحلهٔ بعد

Product دربارهٔ تأیید رسانهٔ ReviewRequired و تجاری‌سازی Library و retention آینده تصمیم دهد.
این تصمیم‌ها با implementation جاری یا فعال‌سازی AI Runtime و Deploy مخلوط نشوند.
