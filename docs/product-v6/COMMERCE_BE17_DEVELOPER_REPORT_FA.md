# گزارش توسعه‌دهندهٔ BE-17 — نهایی‌سازی سیاست جاری Commerce

مبنا `f030ab7ccff23c3b124e989c8f664248abd35e38`؛ شاخهٔ `feature/product-v6-backend-alignment`.
شروع و پایان اعتبارسنجی `2026-11-16`؛ نتیجهٔ نهایی BE17_COMMERCE=PASS_CURRENT_POLICY است.
Source اعتبارسنجی و منتشرشده روی شاخه: `3561610336e88b225da5463c959a640d4bcf9ae5`.

## ۱. خلاصهٔ مرحله

Engine موجود Plan/PlanVersion/Order/Payment/Subscription/Usage حفظ شد. چهار فاصلهٔ واقعی رفع شدند:
Quote، SourceCount قابل‌شمارش را unavailable نشان می‌داد؛ meterهای bytes شخصی/تکلیف از بنیاد
قدیمی کامل نشده بودند؛ تغییر Plan می‌توانست RenewalAvailable=false مقصد را دور بزند؛ درخواست
Downgrade به Draft کامل، به‌جای conflict تجاری خطای عمومی500 می‌داد.
اکنون Quote/Summary ظرفیت زندهٔ مالک را می‌دهند و منع تمدید مستقل از تغییر Plan است.
Family/Shared Subscription ساخته نشد؛ D05 باز می‌ماند.

## ۲. معماری

تغییر runtime فقط در Infrastructure/Commerce است؛ Domain/Application/Api بازطراحی نشدند.
قواعد انتشار/نسخه/دوره در Domain، use case/port در Application، SQL/EF در Infrastructure و HTTP
در Controllerهای Api مانده‌اند. Query به Domain راه نیافته و Onion/.NET9/EF9/SQL Server محفوظ‌اند.

## ۳. فایل‌های مهم جدید

| مسیر | مسئولیت و دلیل | نکتهٔ آموزشی |
|---|---|---|
| MakanApp.Infrastructure/Commerce/EfCommercialStore.Capacity.cs | meter فایل در همان store، بدون Commerce موازی | EXISTS مانع double count referenceهاست |
| tests/MakanApp.UnitTests/Commerce/Be17CommercialPolicyTests.cs | ۱۵ case required quota، immutability، ماه شمسی | omission/zero/null سه معنا دارند |
| tests/MakanApp.IntegrationTests/CommercialSubscriptionEndpointsTests.Be17.cs | Catalog/availability/قیمت/frozen order/retry/limit | HTTP را همراه SQL واقعی بسنجید |
| tests/MakanApp.IntegrationTests/CommercialSubscriptionEndpointsTests.Be17Ownership.cs | Parent+دوChild و تمدید سازمان | رابطهٔ آموزشی، مجوز مالی نیست |
| tests/MakanApp.IntegrationTests/CommercialSubscriptionEndpointsTests.Be17Capacity.cs | bytes/SourceCount، reference مشترک، ledger تاریخی | ظرفیت زنده مصرف دوره‌ای نیست |
| COMMERCE_CURRENT_POLICY_FRONTEND_HANDOFF_FA.md و COMMERCE_PRODUCT_MATRIX_FA.md | قرارداد Client/Admin و مالک/payer | تنظیم محیط را از implementation جدا کنید |

## ۴. فایل‌های مهم تغییرکرده

EfCommercialStore.cs دو meter bytes را از query مشترک می‌خواند؛ unavailable قدیمی حذف شد؛
Summary و ScheduleDowngrade مقصد غیرقابل تمدید را معتبر فرض نمی‌کنند؛ Draft پیش از فراخوانی
Domain با PLAN_NOT_PURCHASABLE رد می‌شود و قرارداد فعال دست‌نخورده می‌ماند.
EfOrderPaymentStore.Quotes.cs برای SourceCount/bytes همان مرجع authoritative را دارد؛
Remaining محدود صفر می‌تواند باشد و Unlimited صریح null می‌ماند.
EfOrderPaymentStore.CommercialTerms.cs شرط RenewalAvailable مقصد را مستقل اعمال می‌کند؛
Order/Payment/Outbox همان زیرساخت موجودند. README و اسناد بنیاد/تصمیم‌ها به مرجع جاری ارجاع دارند.

## ۵. جریان اجرای واقعی

CommercialSubscriptionsController → CommercialSubscriptionService → resolve User/Workspace معتبر →
ICommercialStore → EfCommercialStore → FileCapacityAsync/SQL → CommercialSummaryDto → HTTP.

Quote/Order: Controller → OrderPaymentService → EfOrderPaymentStore →
SetCommercialOrderTermsAsync + PlanVersion/CommercialPeriods → transaction کوتاه SQL → DTO.
Payment معتبر → Payment/Outbox موجود → EfSubscriptionActivationHandler → قرارداد/Amendment همان مالک.
callback یا ادعای Client این زنجیره را Paid نمی‌کند.

## ۶. دیتابیس

Migration/جدول/ستون/PK/FK/index جدیدی نیست. schema فعلی۷۵ Migration تا
20261114183321_ExtendProductNotificationCoverage دارد؛ EF pending model ندارد.
meter به FileAssets، AssignmentVersionAttachments، SubmissionAttachments، PersonalClasses و
EducationalSources/Versions موجود query می‌زند. Ready/Retained شمرده و non-ready/terminal کنار گذاشته می‌شوند.
قفل مالک، Serializable/UPDLOCK/HOLDLOCK، rowversion و unique operation موجود تغییر نکردند.
MakanApp، MakanApp_Test و سرور migration نشدند؛ fixtureها فقط SQL ایزوله دارند.

## ۷. Endpointها

endpoint تازه‌ای نیست. Catalog/Summary همان DTO قبلی با meter درست دارند. Quote/Order قیمت
سروری دارند؛ Admin Create مبلغ IRR و Draft Edit مبلغ TOMAN می‌گیرد. Renewal/Downgrade منع
availability مقصد را رعایت می‌کنند؛ active contract حذف نمی‌شود. نشست، Manager سازمانی یا permission
صریح platform.commerce.read/manage مرز قبلی‌اند. جزئیات Method/Route/Request/Response/خطا در
[Handoff](COMMERCE_CURRENT_POLICY_FRONTEND_HANDOFF_FA.md) ثبت شده است.

## ۸. مفاهیم فنی

CurrentCapacity وضعیت موجود است و با ماه جدید صفر نمی‌شود؛ PeriodicConsumption به دوره/Reservation
بسته است. Order snapshot با Catalog زنده یکی نیست. Idempotency علاوه بر شناسه، payload/payer/context/quota
را مقایسه می‌کند. Unknown نتیجهٔ نامعلوم است، نه Released.

## ۹. طراحی و Trade-off

به‌جای counter/ledger فایل تازه، metadata و اتصال‌های فعلی خوانده می‌شوند؛ drift و Migration
تازه نداریم ولی query هزینه دارد. جمع روی FileAsset مانع تکثیر bytes در چند reference است.
فایل شخصی بی‌اتصال یا Library حدسی bill نمی‌شود. Host مالی با uploader یا مدیر فعلی اشتباه نمی‌شود.
Archive/Retained مجوز purge نیستند. meter خواندنی، gate سراسری Upload تازه نیست؛ Safety محفوظ است.
منع Renewal شرط مستقل مقصد است و شرط NewPurchaseAvailable برای انتقال Plan نیز باقی است.

## ۱۰. تست‌ها

۱۵ Unit جدید: هشت case required quota/omission/zero/null؛ پنج mutation نسخهٔ Published؛ دو مرز
ماه شمسی/تهران و بازیابی لنگر اصلی. این‌ها Pure Domain هستند.
۱۸ Integration جدید: دو Audience کاتالوگ، دو Availability، منع دورزدن تمدید با تغییر Plan، رد Draft کامل، واحد
Create/Edit/frozen order، سه limit، چهار retry-conflict، Parent+دوChild، تمدید سازمانی و دو scope bytes.
Host/SQL/EF/Authorization واقعی fixture دارند و payment provider کنترل‌شده است.
خانواده پروفایل را از API کامل و دو GuardianRelation فعال و سه اشتراک/پرداخت مستقل ایجاد می‌کند؛
مصرف کودک ماندهٔ والد/فرزند دیگر را عوض نمی‌کند؛ Order/Payment دیگری404 است.
ظرفیت فایل با دو Assignment، دو Submission، فایل مشترک/Retained و ledger تاریخی سنجیده شد؛
Pending/Rejected/Deleted و مالک دیگر وارد شمارش نمی‌شوند. Gateهای Academic در caseهای BE-17 خاموش‌اند.
تست‌های قبلی proration/grace/آخرین رزرو/Unknown/Legacy/School دو نوع/PersonalClass/AccountDeletion/
AI/Knowledge/Notification/Zibal در Full reuse می‌شوند. Architecture جدید لازم نبود؛ مجموعهٔ موجود حفظ شد.

## ۱۱. Validation واقعی

| مرحله | نتیجه |
|---|---|
| Clean / Restore | PASS |
| Release Build اصلاح‌شده | PASS؛ صفر Warning/Error |
| Focused Unit | ۱۵ موفق؛ صفر Failed/Skipped |
| Focused Integration نهایی | ۱۸ موفق؛ صفر Failed/Skipped |
| Full اول، پیش از تست تکمیلی Draft | ۱۳۶۵ Unit +۲۰۴۵ Integration +۱۴ Architecture =۳۴۲۴ موفق؛ صفر Failed/Skipped؛۱۱۷ Zibal موفق |
| Final Build | PASS؛ صفر Warning/Error |
| Final Tests پس از اصلاح Draft | ۱۳۶۵ Unit +۲۰۴۶ Integration +۱۴ Architecture =۳۴۲۵ اجرا و موفق؛ صفر Failed/Skipped |
| Zibal / Realtime observation نهایی | هر۱۱۷ Zibal موفق؛ صفر Realtime timeout/failure و صفر Lookup failure |
| EF model / Migration | مدل معلق ندارد؛ Migration جدید صفر |
| OpenAPI | ۴۳۸ Path،۴۹۴ Operation،۷۸۳ Schema؛ Hash بدون تغییر |

OpenAPI SHA-256: `616e22cf15bc67b604ea12f3348c4612d0289ebaf413eb4d83635c65770cf4f0`.
ثابت‌بودن Controller/DTO، Hash قرارداد را ثابت نگه داشته است.
شواهد خصوصی در `.artifacts/be17-commerce-20261116-f030ab7` محفوظ‌اند.
در BE-15 شش timeout مربوط به Realtime و یک Lookup failure تاریخی ثبت شده بود؛ BE-23 آن‌ها را
بازتولید نکرد و علت را UNPROVEN نگه داشت. مقدار مشاهده‌شدهٔ BE-17 از Full/Final واقعی گزارش
می‌شود؛ نتیجهٔ سبز فعلی به معنی اثبات رفع علت ناپایداری تاریخی نیست.
خطاهای آماده‌سازی مخفی نشده‌اند: assertion اولیه Currency DTO را enum فرض کرده بود و به string
IRR اصلاح شد؛ fixture خانواده Profile کامل نداشت و از API تکمیل شد؛ بلوک تکراری assertion ظرفیت
پیش از Full حذف شد. این موارد خطای Production معرفی نمی‌شوند؛ log/TRX دورهای قبلی محفوظ است.
پس از Full اول، تست Audience هر دو جهت خرید ناسازگار را با Context مجاز پوشش داد. بازبینی بعدی
Draft کامل را به‌طور واقعی با Expected=409 و Actual=500 بازتولید کرد؛ guard وضعیت Published
پیش از Domain افزوده شد و هر۱۸ case متمرکز موفق شدند. دور نهایی در حال اجرا برای همین اصلاح
متوقف و NOT_COMPLETED ثبت شد؛ از نتیجهٔ ناقص آن به‌عنوان Full موفق استفاده نمی‌شود. Full نهایی
Source اصلاح‌شده جداگانه در final-r2 موفق شد؛ هیچ failure/timeout با حذف artifact مخفی نشده است.
شمار نهایی از هر سه TRX به‌صورت streaming خوانده و Hash آن‌ها ثبت شد. هر۸ فایل Source/تست با
manifest کامپایل/اعتبارسنجی یکسان ماندند؛۱۲ فایل محافظت‌شده و Backup آن‌ها نیز بدون تغییرند.
۳۳ case جدید به Baseline۳۳۹۲ افزوده شده‌اند؛ focusedها یا دو دور Full برای افزایش شمار با هم جمع نشده‌اند.

## ۱۲. Git

سه فایل AI از قبل Untracked و خارج از Stage می‌مانند. ۱۲ فایل خصوصی/user/ملی‌پیامک/AI با
Backup خصوصی و SHA-256 محافظت شدند؛ appsettings و Secret تغییر نکرده‌اند.
Commit کد/تست: `3561610336e88b225da5463c959a640d4bcf9ae5` با پیام
`feat(commerce): finalize catalog entitlement and quota current policy`؛۸ فایل، شامل۴ runtime و۴ تست.
Push این Commit موفق و پس از Fetch، HEAD/Remote همین SHA تأیید شد؛ هیچ AI/config خصوصی وارد آن نشد.
مستندات در Commit مستقل با پیام `docs(commerce): finalize be17 product and frontend contracts`
ثبت می‌شوند؛ شناسهٔ نهایی تحویل و تأیید Remote آن در گزارش نهایی Git اعلام می‌شود.
Remote=origin در https://github.com/RastinaxAI/MakanApp.git؛
Upstream=origin/feature/product-v6-backend-alignment و Branch همان feature/product-v6-backend-alignment است.
Merge به main، Publish تازه و Deploy جزو این Task نیستند.

## ۱۳. محدودیت‌ها

D05 و۱۲ تصمیم Product/Legal بازند. FamilySubscription/SharedQuotaPool، Store Billing، refund یا
قیمت/Quota واقعی Seed نشده‌اند. School Production تنظیم مصوب می‌خواهد؛ Catalog واقعی Production
خوانده نشده و configured فرض نمی‌شود. BE24_FULL_ACCEPTANCE=FAIL باقی است. Library عمومی،
ReviewRequired، Service Auth خاموش AI و Gateهای BE-25 تغییر نکرده‌اند.

## ۱۴. نکات آموزشی

۱. واحد DTO را از contract بخوانید؛ تبدیل دوبار خطای مالی است.
۲. owner را از scope بگیرید؛ uploader/payer/host/manager/learner یکی نیستند.
۳. ظرفیت را از ledger دوره‌ای دوباره نشمارید؛ EXISTS از تکثیر reference جلوگیری می‌کند.
۴. Unknown را failure قطعی ننامید؛ نتیجهٔ دیررس همان دوره را نهایی می‌کند.
۵. Availability عرضهٔ آینده است؛ Order و قرارداد تاریخی را بازنویسی نکنید.
۶. fixture ناقص با business failure فرق دارد؛ هر دو شاهد واقعی می‌خواهند.
۷. موفقیت کد، قیمت‌گذاری مصوب یا استقرار/پیکربندی واقعی سرور نیست.

## ۱۵. مسیر مطالعه

CommercialSubscriptionsController → CommercialSubscriptionService/CommercialActorAccess →
PlanVersion.CommercialTerms/CommercialPeriods → CommercialContracts → EfCommercialStore/Capacity →
EfOrderPaymentStore.Quotes/CommercialTerms → EfSubscriptionActivationHandler →
Be17CommercialPolicyTests → سه فایل Integration BE17 → Product Matrix.
Migration تازه‌ای وجود ندارد؛ mappingهای EF Commerce/Storage/Assessment فعلی مرجع‌اند.

## ۱۶. مرحلهٔ بعد

BE-24 را در Task مستقل بر مبنای Source نهایی و تنظیم محیط مصوب بپذیرید. ثبت قیمت و School
Offering واقعی، Publish/Deploy و تصمیم D05 جدا هستند؛ هیچ‌کدام برای سبزکردن BE-17 حدس زده نمی‌شوند.
