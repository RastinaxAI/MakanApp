# گزارش توسعه‌دهندهٔ BE-16 — اعلان، Deep Link و تحویل

Baseline=`76acf2024f51d39412d0d6f842f24afb5f26425a`؛ شاخهٔ `feature/product-v6-backend-alignment`. این گزارش دربارهٔ کد همین مرحله است؛ تنظیم و دریافت زندهٔ Provider از موفقیت کد جدا گزارش می‌شود.

## ۱. خلاصهٔ مرحله

foundation اعلان، Preferences، Device، Outbox، WebPush/FCM و recovery از قبل وجود داشت. شکاف واقعی، اتصال بعضی Workflowهای موجود به اعلان، Route یکدست Push و بررسی کامل دسترسی جاریِ اعلان آموزشی بود. سیستم دوم ساخته نشد.

اکنون دعوت/Join پیام‌رسان و Review/Invitation/Join/Transfer/Membership/State کلاس مستقل از Storeهای مالک به Outbox موجود وصل‌اند. تغییر یک نقش سازمانی دو receipt منطقی نمی‌سازد. Push فقط لینک شناسهٔ اعلان دارد و Target آموزشی بعد از پایان دسترسی404 می‌شود. runtime کانال‌های بیرونی بدون credential/device واقعی فعال فرض نشده است.

## ۲. معماری

Domain فقط هشت مقدار تازهٔ NotificationType/ResourceType دارد؛ هیچ EF/HTTP/Provider dependency نگرفته است. Application نگاشت Outbox type و Preference Category همان foundation را گسترش می‌دهد. Infrastructure مسئول capture در transaction مشخص، SQL authorization، Route resolver، wire payload و دو CHECK دیتابیس است. Api همان Controllerهای نازک و endpointهای موجود را نگه می‌دارد؛ DI constructorها وابستگی‌های واقعی را تأمین می‌کند.

جهت وابستگی `Api → Application → Domain` و `Infrastructure → Application/Domain` حفظ شد. .NET9، EF9، SQL Server و Modular Monolith/Onion ثابت‌اند؛ SaveChanges hook عمومی، Generic Repository، MediatR یا Provider موازی اضافه نشده است.

## ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت / دلیل | نکتهٔ آموزشی |
|---|---|---|
| `MakanApp.Infrastructure/Notification/ProductNotificationWriter.PersonalClasses.cs` | تبدیل تغییرهای واقعی کلاس به Intent امن در transaction Store | Save میانیِ داخل transaction هم باید event مربوط به همان تغییر را capture کند |
| `MakanApp.Infrastructure/Notification/ProductNotificationWriter.Messaging.cs` | دعوت و Join/Decision گفتگو؛ Recipient محدود به مقصد/مدیر مجاز | عضو نشدنِ متقاضی با داشتن receipt تناقض ندارد |
| `MakanApp.Infrastructure/Notification/ProductNotificationAccess.Workflows.cs` | Query مجوز جاریِ Resourceهای جدید برای list/target/delivery | شرط SQL باید قبل از pagination اعمال شود، نه فیلتر حافظه‌ای پس از صفحه‌بندی |
| `MakanApp.Infrastructure/Notification/AcademicNotificationAccessQuery.cs` | Published/Release، کلاس، دورهٔ مشارکت، Role و Guardian جاری | Membership فعال به‌تنهایی حق دریافت نمره ایجاد نمی‌کند |
| `MakanApp.Infrastructure/Persistence/Migrations/20261114183321_ExtendProductNotificationCoverage.cs` و Designer | گسترش دو CHECK با snapshot واقعی EF | Enum عددی وقتی CHECK محدود دارد به migration نیازمند است |
| `tests/MakanApp.IntegrationTests/NotificationAcademicFixture.cs` و partial Factory متناظر | گراف معتبر Publish/Release روی SQL موقت | ResourceId تصادفی، آزمون مجوز یک منبع واقعی نیست |
| `tests/MakanApp.IntegrationTests/Be16*Notification*.cs` و `Be16AcademicNotificationAccessTests.cs` | پذیرش API، SQL، rollback/retry/revoke و migration | assertion باید اثر تجاری و نبود تحویل نامجاز را بسنجد |
| `tests/MakanApp.UnitTests/Notification/NotificationContractCoverageTests.cs` | نگاشت همهٔ Typeها و whitelist/link همهٔ Pushها | پوشش enum کامل با معنا و قرارداد لازم است، نه فقط شمار تست |

Handoff، ماتریس رویداد و Runbook در کنار همین گزارش، دلیل تنظیم و حدود پذیرش را توضیح می‌دهند؛ مشخصات جامع HTTP/DB از Snapshot کد بازتولید می‌شود.

## ۴. فایل‌های مهم تغییرکرده

- `NotificationType`, `NotificationResourceType`, `NotificationContracts`, `NotificationPreferencePolicy`: هشت Type/Resource افزوده شد، مقادیر قدیمی ثابت و event nameها `.v1` باقی ماندند. دستهٔ Mandatory فعلی دست‌نخورده است.
- `EfPersonalClassStore`, بخش Admission و `EfPersonalClassFormationStore`: writer در Save مالک و پیش از Save میانیِ approval/transfer فراخوانی می‌شود؛ intermediate state بیرون transaction commit نمی‌شود.
- `EfMessagingStore`, بخش Creation و Management.Helpers: دعوت‌های اولیه نیز همان writer را می‌گیرند؛ retry ایجاد، عمر دعوت را تمدید یا event تازه ایجاد نمی‌کند.
- `ProductNotificationWriter`: RoleAssignmentهای مرتبط با یک Membership aggregate می‌شوند. اثرانگشت شامل ID/نوع/وضعیت/rowversion نقش‌هاست؛ تغییر واقعی تازه از retry متمایز می‌ماند.
- `ProductNotificationAccess`, `EfNotificationTargetResolver`, `NotificationDeliveryProcessor.Authorization`: مجوز Resourceهای جدید و آموزش جاری دوباره سنجیده می‌شود؛ Preference scope قدیمی، کد `OrganizationAccessRevoked` را همچنان حفظ می‌کند.
- `EfNotificationStore.GetForUpdateAsync`: `AsTracking()` صریح پس از query مرکب؛ NoTracking یک subquery می‌توانست تغییر ReadAt را ذخیره‌نشده بگذارد.
- `PushWirePayload`: لینک پیام هم canonical NotificationId شد؛ عنوان/بدنهٔ ورودی خصوصی هنوز وارد wire payload نمی‌شود.
- `NotificationConfiguration`, `MakanDbContextModelSnapshot`: فقط CHECK type/resource گسترش یافت. fixtureهای قدیمی Preferences، `NotificationWorkerRecoveryTests` و `OperationsWorkerRecoveryTests.SeedDeliveryAsync` به منبع آموزشی واقعی منتقل شدند؛ assertionهای امنیتی و cancellation/recovery حفظ‌اند.
- `tools/docs/sync_repository_docs.py`: Template اعلان به Handoff جاری و جریان NotificationId اشاره می‌کند؛ قابلیت Frontend یا Provider واقعی ساخته نشده است.

## ۵. جریان اجرای قابلیت

```text
HTTP business operation
→ Controller موجود / Application use case موجود
→ EfPersonalClassStore یا EfMessagingStore
→ Domain state transition
→ ProductNotificationWriter capture محدود
→ business rows + platform.OutboxMessages در همان transaction
→ commit SQL Server
→ OutboxDispatcher → OutboxProcessor
→ Notification.Create + NotificationDeliveryCoordinator
→ NotificationDeliveryProcessor
→ مجوز جاری + Preferences/Quiet Hours + Device/Session جاری
→ WebPushNotificationProvider / FcmPushNotificationProvider خارج transaction تجاری
→ receipt وضعیت تلاش
```

```text
GET /api/v1/notifications/{id}/target
→ NotificationsController
→ NotificationService.ResolveTargetAsync
→ owner/session check
→ EfNotificationTargetResolver
→ ProductNotificationAccess + AcademicNotificationAccessQuery
→ use case آموزشی موجود برای context فعلی
→ NotificationTargetResult یا ProblemDetails404
```

Guardian حق مرور Direct خصوصی کودک ندارد. Host مالی پس از انتقال مدیریت، خودکار Recipient تغییرات کلاس یا صاحب اختیار آموزشی نیست.

## ۶. دیتابیس

جدول تازه یا دادهٔ تجاری backfill نشد. جدول موجود `[notification].[Notifications]` همان PK از نوع Guid، FKهای User/Organization، عنوان/بدنهٔ عمومی، UTC و rowversion را دارد. قید یکتای `(SourceEventId, RecipientUserId)` receipt تکراری را منع می‌کند؛ indexهای recipient/created و recipient/read برای list/unread موجودند.

Migration فقط `CK_Notifications_Type` را از1..12 به1..20 و `CK_Notifications_ResourceType` را از1..10 به1..18 گسترش می‌دهد. rowهای قدیمی و ReadAt/rowversion آن‌ها تغییر نمی‌کنند. Down پس از وجود receipt یا envelope جدید با51000 رد می‌شود؛ صف materializeنشده هم به قرارداد جدید وابسته است و برای downgrade حذف/تبدیل نمی‌شود. پذیرش Fresh/Upgrade روی SQL GUID موقت است؛ normal DB و سرور migration نشده‌اند.

Outbox و attemptهای موجود، transaction/unique index/claim token/lease/fencing خود را نگه داشته‌اند. ارسال شبکه در transaction تجاری نیست؛ durable retry جای ادعای exactly-once شبکه را می‌گیرد.

## ۷. Endpointها

API تازه لازم نبود. فهرست کامل ورودی/خروجی/خطاها در [Handoff](NOTIFICATION_FRONTEND_HANDOFF_FA.md) است. List/Unread فقط دادهٔ قابل نمایش مالک را می‌دهند؛ MarkRead زمان اولیه را در retry حفظ می‌کند؛ Target با404 نبود مجوز/منبع را پنهان می‌کند و workspace قدیمی را restore نمی‌کند.

Preferences از endpoint موجود با scope شخصی/سازمانی و rowversion، Quiet Hours و category معتبر استفاده می‌کند. Device registration از نشست معتبر و Version/Host/keys معتبر استفاده می‌کند؛ خطاهای400/409/503 و rate limit پابرجاست. Device list هیچ token/subscription secret برنمی‌گرداند. همهٔ Controllerها همان مرز HTTP→Application را حفظ می‌کنند.

## ۸. مفاهیم فنی همین مرحله

Transactional Outbox، شناسهٔ پایدار رویداد، dedup Receipt، rowversion و optimistic concurrency، ترکیب IQueryable و tracking، authorization جاری، metadata عمومی در برابر محتوای خصوصی، Claim/lease/fencing موجود و تفاوت readiness با پذیرش دریافت واقعی، مفاهیم واقعی این تغییرند. Worker recovery یا Provider جدیدی به‌عنوان معماری تازه معرفی نشده است.

## ۹. تصمیم‌های طراحی

به‌جای hook عمومی DbContext، Storeهای مالک صریحاً capture می‌کنند؛ دامنهٔ اثر و transaction قابل بازبینی می‌ماند، ولی افزودن Workflow آینده نیازمند اتصال صریح خودش است. Notification با ResourceType تایپ‌شده route می‌گیرد؛ title/body مبنای routing نیست.

برای سوابق آموزشی، خود API تاریخچه بازطراحی نشد. اعلان نیازمند دسترسی جاری، قبل از Route و تحویل همان query مشترک را اجرا می‌کند؛ receipt عمومی می‌تواند برای Audit بماند، بدون احیای عضویت قدیمی. عملیات کلاس Archived ممکن است متادیتای فقط‌خواندنی مجاز داشته باشد؛ این اختیار Reopen یا آموزش ایجاد نمی‌کند.

برای تغییر Role، پایان نقش قدیمی و افزودن نقش جدید یک event aggregate است. برای Push، فقط NotificationId لینک canonical است و ConversationId hint باقی می‌ماند. ذخیرهٔ متن خصوصی بیشتر یا اثر تجاری تازه برای Notification اضافه نشد.

## ۱۰. تست‌ها

2 Unit و53 Integration تازه افزوده شده‌اند؛ شمار suite از3323 به3378 رسیده است. Unitها map یکتای event type برای همهٔ enumها، category، dedup recipient و whitelist/canonical link همهٔ Pushها را می‌سنجند. Integrationهای تازه شامل review بدون متن خصوصی، دعوت/Join/Decision، انتقال مدیر، عدم مجوز Host مالی، retry state، rollback شش Producer کلاس و دو Producer پیام‌رسان، یک اعلان Role replacement، چهار نوع آموزشی در چهار revoke و Guardian جاری، Support permission revoke، School review و callback بدون proof/verify تکراری Zibal هستند.

Fresh/Upgrade، حفظ rowversion و ReadAt، CHECK invalid values و downgrade guard با SQL واقعی تست می‌شوند. تست‌های baseline Notification/Preferences/Workers/P1/WebPush/FCM/BE22 و همهٔ حوزه‌های دیگر نیز در suite کامل باقی‌اند. Providerها fake/recording یا HTTP کنترل‌شده‌اند؛ SMS/Push واقعی برای پاس‌کردن suite ارسال نمی‌شود. Architecture همان14 تست مرز Onion موجود است؛ تست معماری نمایشی تازه ساخته نشده است.

## ۱۱. Validation

Clean/Restore موفق‌اند؛ آخرین Release Build با0 Warning/0 Error است. خطاهای نام عضو/متد در تست‌های تازه و هشدار xUnit2031 اصلاح شدند؛ API برای درست‌کردن تست تغییر نکرد. آخرین focused شامل2 Unit و90 Integration، همگی Passed و بدون Skipped است؛ صفر تست Architecture در فیلتر focused، به‌عنوان پذیرش Architecture محسوب نشده است. دو حالت Privacy/Ban و سه حالت accepted/revoked/expired دعوت سازمانی نیز در این پذیرش هستند.

در بررسی focused، خطای tracking ReadAt و ترتیب failure reason اصلاح شدند. تست جدید نشان داد بعضی use caseهای تاریخی پس از پایان Enrollment/Class هنوز Read مجاز دارند؛ Resolver اعلان اکنون guard جاری مشترک دارد و Target404 مورد انتظار پاس شد. assertionهای تازهٔ Push نیز با channel مشخص شدند، چون coordinator مستقلِ SMS پیش‌فرض، یک attempt Suppressed معتبر می‌سازد؛ رکورد SMS حذف یا تست امنیتی ضعیف نشد.

دور کامل اول:1346 Unit و14 Architecture موفق؛ Integration برابر2009 Passed از2013، چهار Failed و صفر Skipped. مدت Integration49دقیقه و55ثانیه بود. زمان/نام دقیق چهار شکست در `.artifacts/be16-notifications-20261114-76acf20/full-first-summary.json` و TRX اصلی ثبت شده است. همه در `OperationsWorkerRecoveryTests` بودند: `AcceptedProviderResultStillPersistsWhenCallerCancelsBeforeReturn`، `ProviderAcceptanceBeforeSqlFailureCanRetrySameLogicalDeliveryWithoutDuplicateNotification`، `DeliveryCompletionSqlUsesFiniteIndependentBudgetAndLeavesRecoverableClaim` و `ProviderCancellationWithIOExceptionDoesNotPersistFalseFailure`.

علت هر چهار مورد، Seed با AssignmentId تصادفی بود: authorization جدید پیش از Provider آن را suppress می‌کرد، پس نقطهٔ تزریق failure/cancellation اصلاً اجرا نمی‌شد. `SeedDeliveryAsync` اکنون Assignment Published، participant و recipient واقعیِ SQL دارد؛ هیچ assertion یا timeout تغییر نکرد. کل Recovery و پذیرش تازه در focused90تایی پس از اصلاح پاس شد. timeout ده‌ثانیه‌ای آن تست، Realtime timeout نبود؛ `REALTIME_TIMEOUTS_OBSERVED=0` تا این مرحله است. 13 تست ابزار مستندسازی نیز جدا موفق‌اند. EF مدل معلق ندارد و Designer کامل با Snapshot تطبیق داده شد.

| دور | Unit موفق | Integration موفق / کل | Architecture موفق | Failed | Skipped |
|---|---|---|---|---|---|
| اول | 1346 | 2009 /2013 |14 |4 |0 |
| کامل پس از اصلاح |1346 |2018 /2018 |14 |0 |0 |
| نهایی، پس از Build نهایی |1346 |2018 /2018 |14 |0 |0 |

دو دور موفق هرکدام3378 Passed دارند. Integration دور اصلاح‌شده27دقیقه و9ثانیه و نهایی28دقیقه و34ثانیه طول کشید؛ TRX نهایی در `2026-11-15 00:48:04 +03:30` ثبت شد. همهٔ117 تست Zibal، شامل68 Unit و49 Integration، در هر دو دور موفق‌اند. شمارنده‌ها و SHA256 سه TRX نهایی مستقل کنترل و در `final-validation.json` artifact مرحله ثبت شدند؛ source و تست‌های1537فایل پس از Validation با manifest یکسان‌اند. هیچ تست حذف/Skip، هیچ timeout افزایش و هیچ safety/auth guard برای سبزشدن suite غیرفعال نشد. در تمام دورهای BE-16 هیچ Realtime timeout مشاهده نشد.

Snapshot از Commit کد این مرحله بدون SQL/Hosted Worker/SMS/AI تولید شد:438 path،494 operation،91 Controller،783 schema،151 جدول و75 Migration؛ `OpenAPI SHA256=616e22cf15bc67b604ea12f3348c4612d0289ebaf413eb4d83635c65770cf4f0`. قبول Fresh/Upgrade و هر دو guard Down به معنی اجرای Migration روی محیط عادی یا پذیرش دریافت واقعی Push نیست.

کنترل اسناد494 operation،783 schema،151 جدول،75 Migration و208 مدخل inventory را تأیید کرد؛ لینک شکسته و اختلاف Enum/OpenAPI صفر است. HTML/PDFهای Guide/Reference/Database/Frontend به‌ترتیب79/796/240/804 صفحه دارند؛ bookmark، تطابق SHA منبع HTML، نبود صفحهٔ خالی و نبود متن بیرون صفحه کنترل شدند. لینک نسبی Template Frontend ابتدا در Markdown و HTML نامعتبر بود؛ خود Template اصلاح و همان کتاب بازتولید شد، سپس validator کامل و13 تست ابزار مستندسازی پاس شدند. این13 تست جدا از3378 تست محصول هستند. خروجی HTML/PDF دیتابیس طبق ignore موجود محلی است و وارد Commit نمی‌شود؛ سایر کتاب‌های tracked به‌روز شده‌اند. هیچ Frontend code تغییر نکرده است.

## ۱۲. Git

Commit کد `f4f4d9feb6184d62e033adfb612e86587fb3c3f6` با پیام `feat(notifications): complete event coverage and target authorization` شامل38فایل کد/Migration/تست است. 12577 خط Designer، snapshot کامل و معمول EF است؛ با بدنهٔ ModelSnapshot تطبیق داده شد و به معنی ساخت ماژول تازه نیست. Diff عادی، cached diff و Secretهای خصوصی شناخته‌شده کنترل شدند؛ هیچ مقدار محرمانه وارد Commit نشد.

شاخه `feature/product-v6-backend-alignment`، remote `origin` و upstream `origin/feature/product-v6-backend-alignment` هستند. Push پیاده‌سازی موفق و پس از fetch، HEAD و tracked remote هر دو دقیقاً `f4f4d9feb6184d62e033adfb612e86587fb3c3f6` بودند؛ این checkpoint واقعی، پیش از Commit جداگانهٔ اسناد ثبت شده است. اسناد از همین Source Commit تولید و جدا ثبت/Push می‌شوند؛ SHA و تطبیق HEAD پایانی در گزارش نهایی تحویل اعلام می‌شود. main merge و Deploy اجرا نشده‌اند.

سه فایل untracked قبلی AI، چهار appsettings واقعی و سه فایل ملی‌پیامک با SHA256/Backup خصوصی محافظت شده‌اند؛ هر10فایل و نسخهٔ Backup آن‌ها byte-identical تأیید شدند. فایل‌های AI باقی‌مانده `tools/ai/provider_diagnostic.py`، `tools/ai/run_exact_ai.py` و `tools/ai/test_provider_diagnostic.py` هستند و در Commit نیستند. هیچ `git add .`، stash/reset/clean یا تغییر Source ناتمام AI انجام نشده است.

## ۱۳. محدودیت‌های فعلی

Production Push و Notification SMS عمومی `BLOCKED_CONFIGURATION` هستند. VAPID/FCM و Device واقعی برای smoke آماده نیستند؛ `LIVE_WEB_PUSH=NOT_RUN` و `LIVE_FCM_PUSH=NOT_RUN`. پیامک OTP ملی‌پیامک جدا و تغییرنکرده است. رسید داخلی یا HTTP پذیرفته‌شدهٔ Provider، دریافت نهایی روی OS را تضمین نمی‌کند.

BE24 کل Product v6 همچنان FAIL است و12 تصمیم باز قبلی خودکار بسته نمی‌شوند. قابلیت‌های آموزشی پیشنهادی PersonalClass، AI runtime، سیاست ReviewRequired، retention و Deploy در این مرحله فعال نشده‌اند. Notification session-security یا academic grant پیشنهادی بدون contract تازه ساخته نشده است.

## ۱۴. نکات آموزشی

1. Outbox باید با تغییر تجاری commit یا rollback شود؛ ساختن اعلان در Controller پس از Response، دوام نمی‌دهد.
2. Recipient snapshot و Permission جاری دو زمان متفاوت دارند؛ در تحویل و کلیک، مجوز را دوباره بسنجید.
3. ترکیب query EF می‌تواند tracking را از subquery تغییر دهد؛ mutation را صریح و با تست persistence اثبات کنید.
4. retry یک transition با چند row، نباید چند رویداد منطقی برای یک نفر بسازد.
5. log/payload عمومی را از متن خصوصی جدا کنید؛ لیست سفید wire payload از پاک‌سازی تک‌تک fieldهای حساس قابل اتکاتر است.
6. `ProviderUnavailable`، `NoDevice`، Quiet Hours و revoke شکست‌های هم‌معنا نیستند؛ failure reason برای اقدام عملی مهم است.
7. lease بدون fencing نتیجهٔ دیررس را مهار نمی‌کند؛ invalid registration قدیمی هم نباید token به‌روزشده را revoke کند.

## ۱۵. مسیر مطالعهٔ کد

`NotificationsController` → `NotificationService`/contracts → Domain Notification/enums → `ProductNotificationWriter` و دو partial → Store مالک/transaction → `ProductNotificationAccess`/`AcademicNotificationAccessQuery` → `EfNotificationTargetResolver` → `NotificationDeliveryCoordinator`/Processor → wire/Providerهای موجود → EF Configuration/Migration → تست‌های Be16/Notification/P1/WorkerRecovery → [ماتریس رویداد](NOTIFICATION_EVENT_COVERAGE_FA.md) و [Runbook](../operations/NOTIFICATION_PUSH_RUNBOOK_FA.md).

## ۱۶. مرحلهٔ بعد

در کار عملیاتی مستقل، Provider واقعی، credential خصوصی، allowlist، Device تست مجاز و کلاینت آماده شود؛ Web و FCM جدا با Deep Link و revoke smoke شوند. اجرای Migration/Deploy، ارسال به Device واقعی و بسته‌شدن BE24 را با موفقیت کد این Commit مخلوط نکنید.
