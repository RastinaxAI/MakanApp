# بنیاد منابع آموزشی

## ۱. بازبینی وضعیت قبلی

مبنای این مرحله `main@42c51a3` بود. تنها تغییرهای قبلی خارج از Git سه فایل `tools/ai/provider_diagnostic.py`، `tools/ai/run_exact_ai.py` و `tools/ai/test_provider_diagnostic.py` بودند؛ از این فایل‌ها و چهار تنظیم خصوصی اصلی، نسخه و Hash خصوصی گرفته شد. این مرحله آن‌ها را تغییر نمی‌دهد.

`FileAsset` metadata فایل فیزیکی و وضعیت Pending/Ready/Rejected/Deleted را داشت. `FileSignatureValidator` امضای PDF/JPEG/PNG و UTF-8 فایل متنی را بررسی می‌کرد؛ Ready اثبات OCR، تأیید محتوای انسانی یا Index نیست. `ClassLesson` و پیوست‌هایش منبع درسی موجودند. پیوست‌های Submission و AnswerRevision، فایل پیام و گزارش Export قرارداد و ACL مستقل دارند. PersonalClass مدل مالکیت/عضویت مستقل است و به Class سازمانی تبدیل نمی‌شود. پیش از این مرحله Entity مستقلی برای منبع آموزشی چندبافتی وجود نداشت.

قرارداد AI موجود `AiSourceReference(Type, Id)` و منابع عملیاتی حوزه‌های قبلی را دارد؛ وجود این DTO اثبات وجود catalog اسناد آموزشی نیست. هیچ‌یک از آن قراردادها یا runtimeهای AI در این مرحله تغییر نمی‌کند. `QuotaKey.SourceCount` از قبل CurrentCapacity بود ولی meter نداشت؛ `EntitlementKey.PersonalSources` نیز از قبل تعریف شده بود. Outbox و account/session replay موجود reuse شدند.

## ۲. مدل Domain و لایه‌بندی

- Domain: `Knowledge/EducationalSource` مالکیت و هویت پایدار، وضعیت کلی، visibility و اشاره به شماره نسخهٔ جاری/منتشرشده را نگه می‌دارد. `EducationalSourceVersion` metadata، FileAsset reference، انتشار، Safety و پردازش را جدا نگه می‌دارد. این لایه EF/HTTP ندارد.
- Application: `EducationalSourceService` نشست فعال و بافت عملیات را resolve و ورودی catalog را validate می‌کند؛ `IEducationalSourceStore` پورت اختصاصی این قابلیت است. Repository عمومی یا CQRS جدید ایجاد نشده است.
- Infrastructure: `EfEducationalSourceStore` تراکنش، ACL جاری، قفل مالک/کلاس/فایل، quota، audit و invalidation را اجرا می‌کند؛ Configuration و Migration نیز اینجا هستند.
- Api: دو Controller باریک برای Source و مدیریت Library و نگاشت خطاها در ProblemDetails موجود.

```text
HTTP → EducationalSourcesController → EducationalSourceService
→ Account/Organization context → IEducationalSourceStore
→ EfEducationalSourceStore → Domain rules + SQL Server
→ Audit + existing Outbox در همان تراکنش → Commit → HTTP DTO
→ OutboxProcessor موجود → SignalR ClientInvalidated
→ refetch/replay با authorization جاری
```

## ۳. Personal

مالک از User احرازشده تعیین می‌شود؛ OwnerUserId ورودی عمومی نیست. نقش آموزشی و مخفی‌شدن کارت Personal، این مالکیت یا قابلیت را تغییر نمی‌دهند. حتی اگر انتخاب سازمانی قبلی پس از revoke نامعتبر باشد، حساب/نشست فعال برای Source شخصی خودش معتبر است. Client با `scope=Personal` یا resource واقعی، عملیات شخصی را انجام می‌دهد؛ role، payer و workspace منتخب نشست تغییر نمی‌کنند.

## ۴. Class

Class حتماً Class سازمانی است و FK مرکب تعلق آن به Organization را تضمین می‌کند. Teacher منتسب و Manager مجاز همان سازمان writer هستند. Student فقط نسخهٔ Published را با Enrollment فعال می‌خواند. Draft تازه، نسخهٔ Published قبلی را از دید Student جایگزین نمی‌کند. نقش Parent مجوز این قابلیت ایجاد نمی‌کند؛ سیاست فعلی `PARENT_CLASS_SOURCE_ACCESS=DENY` است.

## ۵. Organization

مالک Organization است؛ Teacher یا Manager سازنده مالک شخصی منبع نمی‌شود. مدیریت و خواندن فعلی محدود به Manager معتبر همان سازمان است. صرف Teacher بودن، مجوز انتشار Organization-wide نیست. مخاطب عمومی سازمان تا تصمیم محصول توسعه داده نشده است.

## ۶. Library و تصمیم تأییدشده

مجوز مشخص `platform.library.sources.manage` اضافه و فقط به نقش موجود PlatformOwner seed شده است؛ School Manager و نقش Operations از مجوز آموزشی قبلی، اختیار Library نمی‌گیرند. نقش تازه‌ای با نام حدسی PlatformAdmin ساخته نشده است.

طبق تأیید مالک محصول، کاربر واردشده metadata منابع Published/Public را در catalog می‌بیند؛ FileAssetId و مشخصات فایل در پاسخ عمومی Library نیست و دانلود عادی بسته است. این محدودیت با entitlement شخصی یا سازمانی دور زده نمی‌شود. Owner دارای permission مدیریت/دانلود دارد. Hidden از catalog حذف می‌شود، ولی reference مستقیم نسخهٔ Published هنوز metadata را برمی‌گرداند؛ Hidden به معنی اعطای مجوز دانلود نیست. Draft و Archived برای خوانندهٔ عادی قابل مشاهده نیستند. تجاری‌سازی Library همچنان Deferred است.

## ۷. مالکیت و جداسازی Scope

Scopeهای Personal/Class/Organization/Library با ستون‌های typed و CHECK مدل شده‌اند، نه OwnerId چندمعنایی بدون FK. Class Source دارای OrganizationId و ClassId است؛ Organization Source فقط OrganizationId؛ Personal فقط OwnerUserId؛ Library هیچ‌یک از این سه selector را ندارد. CreatedByUserId با مالک تجاری یکی فرض نمی‌شود. Scope/مالک پس از ساخت API تغییر ندارد. تطبیق Hash فایل یا تشابه عنوان هیچ merge یا اشتراک بین سازمان‌ها ایجاد نمی‌کند.

## ۸. Lifecycle و readiness

این‌ها مستقل‌اند:

| محور | حالت |
|---|---|
| وضعیت Source | Active / Inactive / Archived؛ API فعلی Archive را اجرا می‌کند |
| انتشار هر revision | Draft / Published |
| Safety/validation | PendingValidation / FileValidated / ReviewRequired / Rejected |
| پردازش آینده | NotRequested / Pending / Processing / Ready / Failed |

`FileReady` از وضعیت واقعی FileAsset می‌آید. `SourceReady` یعنی Source فعال، فایل Ready و اعتبارسنجی فایل معتبر است؛ Draft می‌تواند برای writer آمادهٔ استفادهٔ خصوصی باشد و هنوز به Student منتشر نشده باشد. `AiReady=false` و `CanUseForAi=false` در کل این Foundation برقرارند. `FileValidated` فقط سطح اعتبارسنجی فنی فعلی فایل را بیان می‌کند؛ ادعای malware scan یا تأیید محتوایی نیست.

ReviewRequired و Rejected با Ready فایل دور زده نمی‌شوند. فایل متصل به evidence ایمنی یا پاسخ خصوصی مصرف‌کنندهٔ قبلی برای ساخت Source پذیرفته نمی‌شود. هیچ مسیر Human Review یا دکمهٔ approve حدسی ساخته نشده است. GET وضعیت فقط می‌خواند و readiness فنی جاری را بدون write نشان می‌دهد.

## ۹. پردازش

OCR، extraction، embedding، vector database، indexing، progress درصدی و provider واقعی اجرا نمی‌شوند. وضعیت اولیهٔ پردازش `NotRequested` است؛ Job/worker/retry endpoint ساختگی نداریم. Domain ثبت وضعیت و خطای محدود `SOURCE_PROCESSING_FAILED` را برای قرارداد آینده دارد؛ API عمومی نمی‌تواند خود را Processing/Ready اعلام کند.

تست تزریق داخلی Failed ثابت می‌کند record و فایل غیر-AI باقی می‌مانند، خطای امن قابل مشاهده است و AiReady موفق گزارش نمی‌شود. طراحی Job idempotent، parser version، page map و index generation هنگام ورود processor واقعی، مرحلهٔ جداست.

## ۱۰. FileAsset و retention

Upload همان `/api/v1/files` است. Personal/Library با `workspaceScope=Personal` و Class/Organization در بافت سازمانی معتبر آپلود می‌شوند. MVP فقط PDF، JPEG، PNG و text/plain را از policy فعلی قبول می‌کند؛ WebP/Audio/Video به Source افزوده نشده‌اند.

هنگام binding، فایل باید متعلق به uploader احرازشده و بافت درست باشد. فایل متعلق به Source دیگر، Lesson، پاسخ Assignment/Exam، پیام، evidence، Avatar یا Report Export به‌طور خودکار promotion نمی‌شود. چند revision همان Source می‌توانند فایل خودش را reference کنند. rowversion فایل حتی برای Pending هنگام اتصال تغییر می‌کند تا حذف stale uploader با binding رقابت نکند.

Ready file retained می‌شود. بررسی binding در Storage و cleanup نیز revisionهای Source را می‌شناسد. دانلود عمومی فایل، ACL Source را دور نمی‌زند. Archive فقط metadata را آرشیو می‌کند؛ blob یا نسخه‌های قبلی را حذف نمی‌کند. هیچ تبدیل/backfill بزرگ روی فایل‌ها یا Lessonهای موجود انجام نشده است.

## ۱۱. Authorization و revoke

هر Source read/download و مسیر raw file، مجوز جاری را دوباره بررسی می‌کند. SourceId، FileAssetId، ClassId و OrganizationId selector هستند. Parent مجوز عمومی کلاس ندارد. پایان Enrollment، TeacherAssignment، Membership، role یا غیرفعال‌شدن کلاس/سازمان دسترسی سازمانی آینده را طبق School ACL موجود قطع می‌کند. Personal owned source به سازمان تبدیل یا حذف نمی‌شود.

نسخهٔ قدیمی Published فقط در همان مجوز جاری قابل خواندن است؛ reference تاریخی به‌تنهایی مجوز نیست. پاسخ منبع نامجاز 404 امن است. فایل متعلق به uploader دیگر یا بافت ناسازگار در Create با خطای امن رد می‌شود؛ اطلاعات آن فایل برگردانده نمی‌شود.

## ۱۲. Subscription و Quota

Personal creation به قرارداد Individual و PersonalSources و SourceCount همان User مربوط است. Class و Organization از قرارداد Organization، قابلیت Academic و SourceCount آن استفاده می‌کنند؛ Teacher payer نیست. Library منبع مدیریت‌شدهٔ Platform است و از quota شخصی ناشر کم نمی‌کند؛ این به معنی دانلود رایگان کاربر نیست.

Meter از شمارش Source identityهای واقعی ساخته شده است؛ revision جدید، retry و دو Card رسمی مصرف دوم ندارند. همهٔ Sourceهای مالک، شامل Archived/Inactive، فعلاً در ظرفیت می‌مانند؛ زمان آزادسازی ظرفیت حدس زده نشده است. تعریف quota و limit از PlanVersion معتبر می‌آید؛ قیمت/تعداد رایگان پیش‌فرض ساخته نشده است. کاتالوگ/قرارداد موجود به‌صورت خودکار تغییر نمی‌کند.

Create با قفل Actor، Class در صورت وجود، و مالک تجاری، سپس بررسی quota و Insert در transaction سریال می‌شود. آخرین ظرفیت با دو Create هم‌زمان، یک پذیرش دارد. اتمام سهمیهٔ Personal به Organization fallback نمی‌کند و برعکس. Legacy سازمان از قابلیت جدید، quota یا entitlement ساختگی نمی‌گیرد؛ تا قرارداد کامل و SourceCount مشخص نباشد Create جدید fail-closed است. رفتار قبلی سایر امکانات Legacy دست‌نخورده است.

`CommercialWritePolicy` موجود، writeهای Source سازمانی را هم با همان قواعد فعال/Grace/قفل تجاری بررسی می‌کند؛ Archive کاهش دسترسی است و از بستن منابع پس از اتمام قرارداد جلوگیری نمی‌شود. خواندن دادهٔ مجاز موجود با quota ایجاد جدید یکی فرض نشده است.

## ۱۳. Realtime

همان `platform.realtime-invalidation.v1` و OutboxProcessor، Hub، account/session stream و cursor موجود reuse شده‌اند. event payload فقط کلید، شناسهٔ مجاز، زمان/نسخه و trace دارد؛ عنوان، فایل یا محتوا در آن نیست.

- `knowledge.source` شخصی فقط برای صاحب حساب و نشست‌های معتبر اوست؛ live و `scope=Personal` replay با revision بافت Personal سازگارند.
- `knowledge.source` کلاس/سازمان در تحویل و replay دوباره School ACL جاری را بررسی می‌کند. Draft به Student نمی‌رسد؛ revoke آینده را قطع می‌کند.
- `knowledge.library` اعلان عمومیِ تغییر catalog بدون ResourceId یا محتواست. Draft فقط اعلان مدیریتی هدفمند دارد و permission جاری دوباره بررسی می‌شود.

invalidation دستور refetch است، نه delta مصرف quota یا unread. کلاینت EventId/StreamVersion را dedupe و snapshot را جایگزین کند. exactly-once فرض نشده است. انتخاب بافت و cursorهای Personal/Organization قابل جابه‌جایی نیستند. Source جدید Activity Card نمی‌سازد؛ Lesson/Assignment/Exam قبلی همان Cardهای قبلی را دارند.

## ۱۴. مرز AI و محتوای نامطمئن

Runtime AI خاموش است؛ DTOها و Service JWT موجود تغییر نکرده‌اند. FileReady و SourceReady مجوز invocation یا استفاده از quota AI نیستند. SourceId + revision و FileAsset reference برای citation آینده پایدارند؛ citation تولید نشده است. selected/used sources و اتصال آن‌ها به AI request در مرحلهٔ جدا خواهد بود.

محتوای فایل untrusted data است و نباید Authorization، role، system policy یا entitlement را تعیین کند. retrieval آینده باید مجوز جاری و نسخهٔ انتخابی را پیش از ارسال به هر provider دوباره بررسی کند. hash یکسان، منبع خصوصی دو سازمان را مشترک نمی‌کند.

## ۱۵. APIها و قرارداد

Route/DTO و UX stateهای واقعی در [راهنمای Frontend](EDUCATIONAL_SOURCES_FRONTEND_HANDOFF_FA.md) هستند. همهٔ APIها نشست معتبر می‌خواهند. Listها SQL pagination، count مجاز، ترتیب پایدار CreatedAtUtc نزولی و Id، فیلتر status/type و جست‌وجوی Title دارند. ی/ک عربی و نیم‌فاصله فقط در ستون جست‌وجو normalize می‌شوند؛ عنوان نمایشی حفظ می‌شود.

## ۱۶. خطا و concurrency

`SOURCE_NOT_FOUND`، `SOURCE_NOT_ALLOWED`، `SOURCE_NOT_READY`، `SOURCE_REQUEST_INVALID`، `SOURCE_VERSION_CONFLICT`، `SOURCE_QUOTA_EXCEEDED` و `SOURCE_IDEMPOTENCY_CONFLICT` به ProblemDetails موجود نگاشت می‌شوند. خطاهای قرارداد/entitlement همان Commerce codes موجود هستند.

Update/Publish/Archive/Visibility/NewVersion دارای ExpectedVersion از rowversion Source هستند. حتی timestamp یکسان در TimeProvider آزمون، update parent را برای تغییر rowversion انجام می‌دهد. stale request 409 می‌گیرد و تاریخچه بازنویسی نمی‌شود. Create با ClientOperationId مربوط به Actor و hash ورودی، lost-ACK safe است؛ payload متفاوت با همان کلید conflict دارد. تکرار mutation با token مصرف‌شده نیازمند refetch است؛ idempotency Create به معنی نادیده‌گرفتن concurrency mutation نیست.

## ۱۷. نسخه‌بندی و migration

Migration: `20261030133521_AddScopedEducationalSources`؛ baseline قبلی `20261030093927_AddOfficialOrganizationAndClassSpaces`؛ مجموع ۵۷ Migration.

دو جدول `knowledge.EducationalSources` و `knowledge.EducationalSourceVersions` افزوده شده‌اند. PK اول Id، PK نسخه SourceId/Number است. FKهای User/Organization/Class/FileAsset با Restrict، FK مرکب Class/Organization، CHECK مالک/بافت/state و unique Actor/ClientOperationId داریم. هر دو جدول rowversion دارند. شماره نسخهٔ جاری/منتشرشده در همان transaction سرویس به‌روز می‌شود؛ FK نسخه به Source از ایجاد نسخهٔ بدون منبع جلوگیری می‌کند.

Revision Published از نظر محتوای فایل و metadata از API immutable است؛ تغییر، revision تازه با همان SourceId می‌سازد. Draft قبلی نیز با ایجاد revision جدید حذف نمی‌شود و فقط writer به آن دسترسی دارد. Legacy data conversion یا auto migration در Startup نداریم. Up فقط schema/permission جدید می‌افزاید؛ Down پس از وجود Source، برای حفظ تاریخچه با 51132 متوقف می‌شود.

## ۱۸. تست و Validation

Unit قواعد مالکیت، ورودی ناسازگار، immutability، readiness/Safety، خطای پردازش و normalization را می‌سنجد. Integration روی SQL Server ایزوله، HTTP contract، quota آخرین ظرفیت، idempotency، cross-user/org/class، Parent، revoke، raw file guard، نسخه‌بندی، Library، pagination و replay/live audience را ثابت می‌کند. تست رقابت حذف stale فایل Pending نیز مرز واقعی SQL rowversion را می‌سنجد. Architecture suite جهت Onion را بررسی می‌کند.

Restore و Release Build نهایی موفق، Warning/Error صفر؛ Unit=976، Integration=1603، Architecture=14، Total=2593، Failed/Skipped=0. E2E نهایی Kestrel با 151 درخواست و SignalR واقعی PASS شد؛ full و upgrade Migration هرکدام دو اجرا روی SQL موقت PASS و pending model changes ندارد. شمارش بعد Commit و نتیجه Git در گزارش نهایی جدا ثبت می‌شود. هیچ Migration روی MakanApp، MakanApp_Test یا سرور، Publish، Deploy، SMS واقعی، پرداخت واقعی یا AI واقعی جزو این مرحله نیست.

## ۱۹. تصمیم‌های باز محصول

1. Parent Class Source: فعلاً DENY؛ گسترش مخاطب به تصمیم جدید نیاز دارد.
2. retention پس از Archive: blob/نسخه حفظ می‌شوند؛ مدت پاک‌سازی حدس زده نشده است.
3. مخاطب عمومی Organization: فعلاً Manager؛ Teacher عمومی یا Parent اضافه نشده است.
4. limit و قیمت پلن‌ها: فقط مقادیر واقعی قرارداد؛ دادهٔ آزمون سیاست فروش نیست.
5. OCR و ارزیابی کیفیت استخراج: Deferred.
6. indexing retention و حذف مشتقات: Deferred؛ index واقعی نداریم.
7. Library commercialization: دانلود عادی فعلاً بسته؛ مدل فروش/اعطای مجوز آینده Deferred.
8. سیاست آیندهٔ material خصوصی معلم پس از پایان Assignment: انتقال خودکار نداریم؛ مالک typed فعلی حفظ می‌شود.
9. آزادسازی SourceCount پس از Archive/حذف: فعلاً ظرفیت حفظ می‌شود تا تصمیم مصوب.

این فهرست جایگزین ۱۲ تصمیم باز Official Spaces یا سیاست GAP-05 نیست.

## ۲۰. فایل‌های مهم و نکات یادگیری

| فایل | مسئولیت / دلیل ایجاد | نکتهٔ آموزشی |
|---|---|---|
| Domain/Knowledge/EducationalSource.cs | هویت، مالک و lifecycle | سازندهٔ typed مالکیت ناسازگار را رد می‌کند |
| Domain/Knowledge/EducationalSourceVersion.cs | محتوای versioned و readiness مستقل | publication، safety و processing یک enum مصنوعی نیستند |
| Application/Knowledge/EducationalSourceService.cs | context و use case boundary | کارت پنهان، حساب غیرفعال نیست |
| Application/Knowledge/EducationalSourceContracts.cs | DTO و پورت اختصاصی | EF entity را مستقیماً به HTTP ندهید |
| Infrastructure/Knowledge/EfEducationalSourceStore.cs | write/transaction/quota/audit | check و insert زیر قفل مشترک مالک‌اند |
| Infrastructure/Knowledge/EfEducationalSourceStore.Reads.cs | catalog/read/download | جست‌وجو و فایل هم authorization می‌خواهند |
| Infrastructure/Persistence/Configurations/Knowledge/EducationalSourceConfiguration.cs | قید SQL و FK | application validation جای constraint را نمی‌گیرد |
| Api/Controllers/EducationalSourcesController.cs | transport | Controller وارد SQL یا منطق quota نمی‌شود |
| Api/Controllers/PlatformLibrarySourcesController.cs | catalog مدیریتی Library | School و Platform مرز جدا دارند |

همهٔ مسیرها نسبت به پروژهٔ لایهٔ نام‌برده هستند. فایل‌های تغییرکردهٔ مهم: Storage binding و cleanup برای حفظ فایل مرجع؛ EfCommercialStore برای meter واقعی؛ CommercialWritePolicy برای قواعد قرارداد سازمانی؛ RealtimeSyncService برای audience/replay؛ GlobalExceptionHandler برای کدهای پایدار؛ PlatformCatalog و seedها برای permission محدود؛ MakanDbContext/DI برای ثبت مدل و سرویس. Authentication، SMS، AI و frontend تغییر نکردند.

## ۲۱. trade-off و مسیر مطالعه

entity جدید در Knowledge در برابر استفاده از FileAsset انتخاب شد، چون فایل مالکیت/انتشار/version تجاری منبع را ندارد. هزینه دو جدول و queryهای ACL است. نسخهٔ immutable به‌جای overwrite، تاریخچه را حفظ می‌کند ولی نیازمند نمایش revision جاری/Published جداست. Archive با نگهداری ظرفیت، از quota reset حدسی جلوگیری می‌کند ولی آزادسازی ظرفیت را تا تصمیم بعدی محدود می‌گذارد. پردازش واقعی اجرا نشده تا Ready جعلی ایجاد نشود.

مسیر مطالعه: Controller → EducationalSourceService → قراردادها → دو Domain entity → EfEducationalSourceStore/Reads → Configuration → Migration → EducationalSourceTests → سه فایل EducationalSources*Tests. در پایان مسیرهای BoundAccessResolver و RealtimeSyncService را دنبال کنید تا bypass فایل و replay را ببینید.

## ۲۲. Git و مرحلهٔ بعد

فقط فایل‌های همین قابلیت پس از restore/build/test نهایی ثبت می‌شوند؛ AI dirty، private config، User Secrets، bin/obj و evidence خارج از Git می‌مانند. SHA، نتیجهٔ Push و شمارش قطعی در گزارش نهایی این Task پس از commit/remote verification اعلام می‌شود. این مرحله Publish یا Deploy ندارد.

مرحلهٔ بعد، تعیین سیاست retention/capacity release و Library commercialization و سپس طراحی processor واقعی با job idempotent، version binding و quality status است. AI request integration، OCR یا تبدیل انبوه Lesson/Attachmentها نباید بدون scope مستقل به این Foundation اضافه شوند.
