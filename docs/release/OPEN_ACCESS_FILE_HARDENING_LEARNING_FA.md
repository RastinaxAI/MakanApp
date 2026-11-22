# گزارش توسعهٔ Open Access و بررسی ارسال فایل

مبنای مرحله: `67417677297d7f46466ad0ed061e26bfc76402dc`؛ شاخه `feature/product-v6-backend-alignment`؛ درخواست مصوب مالک محصول در ۲۰۲۶/۱۱/۲۲.

## ۱. خلاصهٔ مرحله

کنترل تجاری پیش از این فقط برای AI نامحدود کنار رفته بود؛ Source شخصی، میزبانی کلاس مستقل و نوشتن آموزشی سازمانِ دارای قرارداد مدیریت‌شده هنوز به Plan/Quota وابسته بودند. اکنون یک تنظیم مرکزی، این کنترل‌های تجاری را برای قابلیت‌های عادی خاموش می‌کند. سوابق مالی واقعی، مجوز منبع و محدودیت‌های مستقل آموزشی/امنیتی حذف نشده‌اند.

منع عمومی مبتنی بر Age/DOB از قبل در سیاست `messaging-20261101` حذف شده بود؛ پیاده‌سازی موازی ساخته نشد. مشکل واقعی Attachment نیز مشخص شد: فایل Ready است، اما موتور واقعی هر رسانه را `Other.UnsupportedMedia → ReviewRequired` می‌کند. این نتیجه به سن/Subscription وابسته نیست و برای موفق‌شدن ارسال، بدون موتور رسانه یا تصمیم تأیید قابل اجرا، دور زده نشده است.

## ۲. معماری

- Application: `CommercialOptions` سیاست را نگه می‌دارد؛ `CommerceService` و `CommercialSubscriptionService` تصمیم تجاری و Context مجاز را جدا نگه می‌دارند.
- Infrastructure: `MakanDbContext` کنترل تجاری SaveChanges را مشروط می‌کند؛ Storeهای Commerce و PersonalClass همان تصمیم را مصرف می‌کنند. بررسی Append-only Audit قبل از این شرط باقی است.
- Api: `CommercialAccessSchemaTransformer` معنای Flag پاسخ را در OpenAPI توضیح می‌دهد. Controller یا منطق OTP/Authentication تغییر نکرده است.
- Domain: هیچ Entity، Rule یا Schema حذف یا تغییر نکرده است؛ Plan/Subscription واقعی و ظرفیت آموزشی همچنان معنای مستقل دارند.

## ۳. فایل‌های مهم ایجادشده

| مسیر | مسئولیت و نکتهٔ آموزشی |
|---|---|
| `MakanApp.Api/OpenApi/CommercialAccessSchemaTransformer.cs` | توضیح bool عمومی `enforcementEnabled`؛ یک پاسخ Metadata نباید خودکار مجوز منبع تلقی شود. |
| `tests/MakanApp.UnitTests/Commerce/OpenAccessCommerceTests.cs` | نبود Subscription، Counter واقعی، اعتبارسنجی ورودی و بازگشت به Enforcement را مستقل از HTTP می‌سنجد. |
| `tests/MakanApp.IntegrationTests/OpenAccessCommerceAcceptanceTests.cs` | قرارداد OpenAPI، Startup نامعتبر، سازمان با قرارداد لغوشده و کلاس بدون Plan را با ACL واقعی بررسی می‌کند. |
| `tests/MakanApp.IntegrationTests/OpenAccessKnowledgeAcceptanceTests.cs` | Upload/Source/Publish/Ingest/RAG بدون Plan، Source ACL و Replay را با SQL واقعی و Transport کنترل‌شده می‌سنجد. |
| `docs/product/OPEN_ACCESS_FILE_DELIVERY_FA.md` | قرارداد کلاینت، multipart، Scope، Kind، Ready، Safety و دانلود را روشن می‌کند. |

## ۴. فایل‌های مهم تغییرکرده

`CommercialOptions.cs` کلید `EnforcementEnabled` دارد. پیش‌فرض کلاس true برای حفظ رفتار نبود تنظیم است؛ فایل‌های جاری مقدار false را صریح دارند. `DependencyInjection.cs` همان Options را تزریق می‌کند و AI معتبر را در حالت خاموش به Unmetered resolve می‌کند؛ Mode نامعتبر به Validator می‌رسد و پنهان نمی‌شود.

`CommerceService.cs` تصمیم Feature/Capacity را از Subscription جدا می‌کند؛ Summary واقعی و متدهای خواندن/ثبت سوابق مالی باقی‌اند. `CommercialSubscriptionService.cs` پس از احراز Actor و Context، عملیات AI را نامحدود resolve می‌کند؛ Reservation منطقی و Idempotency حفظ‌اند.

`EfCommercialStore.cs` و `.Reservations.cs` شرط مالی ظرفیت/Export را خاموش می‌کنند. `EfPersonalClassStore.cs` همچنان حساب فعال Host، Manager، عضویت، رضایت و ظرفیت صریح کلاس را بررسی می‌کند. `MakanDbContext.cs` فقط Guard تجاری و تراکنش مربوط به آن را مشروط می‌کند؛ تراکنش‌های صریح Storeها و Audit تغییر نکرده‌اند.

دو Summary عمومی و نتیجهٔ داخلی `CapacityCheckResult` Flag تصمیم را جدا از Metadata قرارداد دارند؛ Endpoint تازه ساخته نشده است. `Program.cs` فقط Transformer سند را ثبت می‌کند. Fixture قدیمی به‌صورت صریح Enforcement=true دارد تا Regression مالی آینده حفظ شود؛ میزبان‌های جدید false را جدا می‌آزمایند. هیچ تستی Skip یا غیرفعال نشده است.

## ۵. جریان اجرا

```text
HTTP /api/v1/academic/courses
→ CoursesController
→ AcademicService.CreateCourseAsync / IAccessContextResolver.ResolveAsync
→ مجوز Manager و قواعد Domain موجود
→ EfAcademicStore و تراکنش منبع
→ MakanDbContext.SaveChangesAsync
→ Append-only Audit همیشه؛ Guard تجاری فقط با Enforcement=true
→ SQL Server
→ CourseResult → HTTP
```

AI: `CommercialSubscriptionService.ResolveContextAsync` ابتدا Actor/Session و Context را احراز می‌کند؛ سپس Context نامحدود به `EfCommercialStore.ReserveCoreAsync` می‌رود. Reservation بدون Subscription/Plan و با OperationId ثبت می‌شود؛ Finalize/Retry قرارداد فعلی را حفظ می‌کنند. منبع دانش، پیش از Provider و هنگام بازبینی، ACL جدا دارد.

## ۶. دیتابیس

Migration، Table، Column، FK، Index یا Constraint تازه نداریم. تاریخ/قیمت/Quota حدسی و قرارداد نامحدود ساخته نشده است. `rowversion`، تراکنش‌های کلاس/پذیرش/دعوت، Fencing و Append-only Audit قبلی حفظ‌اند. Fix قبلی تنظیم `READ COMMITTED` اتصال Pool تغییر نکرده است.

Migration خودکار فقط جزئی از Fixtureهای LocalDB موقت تست است؛ `MakanApp`، `MakanApp_Test` و سرور مقصد تغییر نکرده‌اند.

## ۷. Endpointها

Endpoint تازه نداریم. Summaryهای `GET /api/v1/commerce/personal/subscription`، `GET /api/v1/commerce/organization/subscription` و `GET /api/v1/commerce/summary` bool افزایشی `enforcementEnabled` دارند؛ مالکیت/Manager بودن همان قبل است. `State=NoSubscription` با false مانع محصول نیست و Subscription واقعی هم نمی‌سازد.

Upload همان `POST /api/v1/files` با فرم `file`، Scope مجاز و پاسخ 201/`FileAssetResult` است. `GET /files/{id}`، `GET /files/{id}/content` و DELETE با نسخهٔ جاری، ACL قبلی را نگه می‌دارند. Send همان `POST /conversations/{id}/messages` با Kind و attachmentIds واقعی است؛ File-only با text=null پشتیبانی می‌شود، اما هنوز Safety لازم است.

## ۸. مفاهیم فنی

Options Pattern، DI، Policy مرکزی، تفکیک مجوز منبع از مجوز تجاری، Metadata در برابر Availability، Idempotency/Reservation نامحدود، Startup Validation، SQL واقعیِ ایزوله، و OpenAPI Schema Transformer در این مرحله استفاده شدند.

## ۹. تصمیم‌های طراحی و شواهد

دادهٔ Commerce پاک نشد و ifهای پراکنده در Controller اضافه نشد. Guardهای مالی در مرزهای واقعی خودشان از Options مشترک تصمیم می‌گیرند. بازگشت به Enforcement با تنظیم و Restart ممکن است؛ AI_MODE معتبر و سیاست مستقل AI نیز باید مطابق انتشار آینده تنظیم شوند.

Read/Write منبع بدون Plan با جعل Subscription حل نشد. تست کلاس، پذیرش بدون خرید را اثبات می‌کند، اما عضو فاقد Manager و نفر اضافه بر ظرفیت آموزشی را رد می‌کند. تست سازمان نشان می‌دهد همان درخواست با Enforcement روشن و قرارداد Cancelled رد می‌شود و با حالت خاموش و Manager معتبر موفق است؛ حساب خارج سازمان همچنان رد می‌شود.

در فایل، Upload هم‌زمان Ready را ثبت می‌کند؛ امضا، اندازه، پسوند، نام فارسی و Stream به خودی خود تأیید Safety نیستند. Root Cause در Provider واقعی، نه Race حدسی Pending→Ready، پیدا شد. تغییر Kind پیش‌فرض Text یا Scope اشتباه، قرارداد کلاینت است؛ مجوز فایل با حدس MIME یا حذف Validation ساخته نشد. موتور رسانه یا Workflow تأیید هنوز معرفی/تصویب نشده است.

## ۱۰. تست‌ها

هفت Unit جدید: سه Entitlement بدون Subscription، بازگشت Enforcement، حفظ Counter و دو حالت ورودی نامعتبر. Integration جدید، دو Summary قابل دسترسی HTTP، دو حالت Startup نامعتبر، Course با قرارداد Cancelled، کلاس بدون Plan و RAG بدون Plan با Source خصوصی را می‌آزماید. تست موجود ۲۵ درخواست HTTP، اکنون دو حالت مستقل Unmetered صریح و Commercial با Enforcement=false را پوشش می‌دهد.

هفت تست موجود BE-01 با Provider واقعی موفق بودند: این موفقیت، صحت PendingReview/عدم تحویل/عدم مصرف تکراری را اثبات می‌کند، نه Send موفق رسانه. مثبت‌های Pipeline با Provider کنترل‌شده به‌عنوان پذیرش Production ثبت نمی‌شوند. تست جدید از EF InMemory برای رفتار SQL استفاده نمی‌کند.

## ۱۱. Validation

اعتبارسنجی Source موفق است؛ پذیرش واقعی ارسال رسانه همچنان مسدود است.

- Clean/Restore/Release Build: موفق؛ Build آخر صفر Warning و Error.
- دو اجرای مستقل Full Regression، هرکدام: Unit=۱۴۵۵، Integration=۲۱۵۱، Architecture=۱۶، مجموع=۳۶۲۲؛ صفر Failed/Skipped.
- Regression ترکیبی: ۱۰۰۶ Integration موفق، صفر Failure/Skip.
- Focused پس از بازبینی: ۸ Integration موفق، صفر Failure/Skip.
- EF Pending Model Changes: ندارد؛ خروجی واقعی فرمان: `No changes have been made to the model since the last migration.`
- از TRX نهایی: ۲۷ تست Storage، ۲۵ Academic Attachment، ۵۶ Realtime، ۳۷ Outbox و ۸ Isolation موفق‌اند. این گروه‌ها همپوشانی دارند و به مجموع تست‌ها اضافه نمی‌شوند.
- Source اجرایی در طول دو دور ثابت بود؛ Hash نه فایل محافظت‌شده بدون تغییر و چهار کانفیگ اولیه از نظر همهٔ مقادیر جز فلگ جدید یکسان‌اند. JSON شش کانفیگ معتبر و بدون کلید تکراری است؛ بررسی مقادیر خصوصی در ۲۹ فایل متنیِ Commit هیچ تطابقی پیدا نکرد.

اولین Focused به‌علت درخواست Self-invite در یک تست جدید، 400 گرفت؛ ورودی تست برای بررسی ACL واقعی اصلاح شد. Warning مربوط به overload صحیح `Assert.Single` نیز رفع شد و suppress نشده است. شواهد اجراهای ناموفق حفظ شده‌اند.

## ۱۲. Git

پیام Commit این مرحله: `feat(access): disable commercial gates and document file safety blocker`؛ شاخه `feature/product-v6-backend-alignment` با upstream برابر `origin/feature/product-v6-backend-alignment` است. فقط ۳۰ فایل کامل همین مرحله برای Stage انتخاب شده‌اند؛ سه فایل AI ناتمام، تنظیمات خصوصی و Artifactها خارج‌اند. شناسهٔ Commit شامل همین سند، نتیجهٔ واقعی Push و تطبیق Remote در گزارش تحویل و `RELEASE_MANIFEST.json` ثبت می‌شوند تا شناسهٔ خودارجاع داخل Commit ساخته نشود. Force Push و Merge به main جزو این کار نیستند.

## ۱۳. محدودیت‌های فعلی

گیت واقعی ارسال رسانه، به قابلیت موتور Safety وابسته و هنوز باز است. Safety bypass، Human Review، Scanner فرضی یا تأیید خودکار براساس Signature ساخته نشده است. این مانع، رفع‌شده یا PASS اعلام نمی‌شود. بسته‌های این مرحله تا رفع آن، کاندید با `READY_FOR_MANUAL_TEST_DEPLOY=NO` و `READY_FOR_MANUAL_PRODUCTION_DEPLOY=NO` هستند. پذیرش AI این مرحله محلی و با Transport کنترل‌شده است؛ AI واقعی، سرور، IIS و Migration مقصد اجرا نشده‌اند. Library و منابع دیگر همچنان ACL اختصاصی دارند؛ Open Access مالکیت محتوا ایجاد نمی‌کند. قواعد عملیات مالی Commerce و Paid Provisioning و Featureهای صریحاً Deferred نیز حفظ شده‌اند؛ فلگ Open Access مجوز ساخت پرداخت یا قرارداد فرضی نیست.

## ۱۴. نکات آموزشی

1. خاموش‌بودن مالی با Subscription نامحدود در SQL فرق دارد.
2. ابتدا Actor/Context/منبع را مجاز کنید؛ سپس دربارهٔ هزینه تصمیم بگیرید.
3. Counter و State واقعی را برای ساخت Availability جعل نکنید.
4. Options معتبر را Override کنید؛ مقدار نامعتبر نباید با Override پنهان شود.
5. Ready ذخیره‌سازی، تأیید محتوایی یا تحویل پیام نیست.
6. تست با Provider کنترل‌شده، پذیرش Provider واقعی Production نیست.

## ۱۵. ترتیب مطالعه

`CommercialOptions` → ثبت DI و تنظیم AI → `CommercialSubscriptionService` → `EfCommercialStore.RequireCapacityAsync` → `MakanDbContext.SaveChangesAsync` → `EfPersonalClassStore.RequireHosting` → تست‌های OpenAccess → `FilesController`/`StorageService` → `InternalMakanContentSafetyProvider`/`MessageContentSafetyGate` → تست‌های BE-01 و قرارداد کلاینت.

## ۱۶. مرحلهٔ بعد

برای بستن گیت فایل، قرارداد واقعی موتور Safety رسانه یا مسیر تأیید قابل اجرا لازم است. تا آن زمان، PendingReview و عدم تحویل حفظ می‌شوند. Deploy دستی متعلق به مالک است؛ تست سبز به‌تنهایی این مانع محصولی/قابلیتی را رفع نمی‌کند.
