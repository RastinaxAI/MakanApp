# اشتراک، حق استفاده و سهمیه — پیاده‌سازی و قرارداد Backend

تاریخ: ۲۰۲۶/۱۰/۲۹

وضعیت: پیاده‌سازی، regression کامل و تکرار نهایی Build/Test اصلاح Legacy مطابق تصمیم صریح مالک محصول موفق‌اند. فروش پلن پولی همچنان به تعیین مقادیر و شرایط Product و راه‌اندازی درگاه نیاز دارد؛ استقرار در این درخواست انجام نشده است.

## محدوده و تصمیم‌های تأییدشده

مرجع Product، فایل MakanApp_Product_Decisions_FA.pdf نسخهٔ ۱، ۲۹ اکتبر ۲۰۲۶ است؛ تمام ۱۹ صفحه خوانده شد.

دو ابهام اولیه با پاسخ صریح مالک محصول بسته شدند:

1. در این مرحله، بنیاد PersonalClass شامل مالکیت، دعوت، عضویت و قرارداد Entitlement/Quota ساخته می‌شود. چرخهٔ کامل Assignment/Exam، محتوای آموزشی و فایل کلاس مستقل، مرحلهٔ جدا است.
2. کنترل تجاری سازمان فقط پس از ثبت قرارداد معتبر توسط ادمین فعال می‌شود. برای سازمان بدون قرارداد و اشتراک قدیمی بدون سررسید، تاریخ پایان یا سهمیهٔ پولی حدس زده نمی‌شود.

مخزن: D:/Project/MakanApp/MakanApp-Backend؛ شاخهٔ شروع: main؛ HEAD شروع: bf9a819ffd2ce96ea14360f0eba0235c760e72b4.

مرحلهٔ پیاده‌سازی با محدودیت بدون Commit/Push/Publish انجام شد. در درخواست نهایی‌سازی بعدی، یک Commit محلی و Publish هر دو محیط مجاز شدند؛ Push، Deploy، Migration روی MakanApp/MakanApp_Test/سرور، فعال‌سازی AI و تماس Production AI همچنان ممنوع‌اند. نتیجهٔ نهایی‌سازی در گزارش Release ثبت می‌شود.

## Audit پیش از تغییر

| موضوع | وضعیت قبل از این درخواست |
|---|---|
| معماری | Onion در Modular Monolith؛ .NET 9، EF Core 9، SQL Server و Controller |
| مالک Subscription | فقط Organization؛ OrganizationId اجباری |
| خرید | فقط مدیر فعال سازمان انتخاب‌شده |
| نسخهٔ پلن | قیمت، Entitlement و Quota پس از انتشار از متدهای Domain قابل تغییر نبودند |
| کنترل انتشار | دوره، قیمت و وابستگی سهمیه‌ها به‌صورت کامل کنترل نمی‌شدند |
| دوره | activation پس از پرداخت اشتراک بدون تاریخ پایان می‌ساخت |
| مصرف | ظرفیت از ماژول‌ها و ReportExports از ledger خوانده می‌شد |
| رزرو | Reserved/Unknown/Released، دوره و Consumer وجود نداشتند |
| عرضه | Audience، نمایش، خرید تازه و تمدید مستقل وجود نداشتند |
| تغییر پلن | جایگزینی اشتراک؛ بدون proration یا کاهش زمان‌بندی‌شده |
| کلاس مستقل | وجود نداشت؛ Class/Enrollment/Assignment/Exam به Organization و کلیدهای ترکیبی آن وابسته بودند |
| Export واقعی | صف و worker وجود داشتند؛ به quota متصل نبودند |
| Messaging | در مسیرهای Messaging مصرف‌کنندهٔ IEntitlementService یافت نشد؛ gate تجاری تازه‌ای روی Messaging گذاشته نشد |

Plan، PlanVersion، PlanVersionPrice، PlanEntitlement، PlanQuotaDefinition، Subscription، UsageLedgerEntry، Order، PaymentAttempt، Payment، Outbox و AdminAuditLog موجود استفاده و توسعه داده شدند. درگاه Runtime همچنان UnavailablePaymentGateway است؛ ارائه‌دهندهٔ واقعی پرداخت فرض یا اضافه نشده است.

## معماری و فایل‌های مهم

| لایه / فایل نسبی | مسئولیت و نکتهٔ آموزشی |
|---|---|
| Domain/Commerce/CommercialOwnership.cs | دو مالک typed، زمینه، نوع سهمیه و وضعیت رزرو؛ خریدار با مالک یکی فرض نمی‌شود |
| Domain/Commerce/CommercialPeriods.cs | ماه تقویمی با لنگر ثابت، مرز روز تهران و محاسبهٔ دقیق کسری |
| Domain/Commerce/Plan.Availability.cs | تفکیک نمایش، خرید تازه و تمدید از حق قرارداد موجود |
| Domain/Commerce/PlanVersion.CommercialTerms.cs | دوره و شرایط فروش، هشدار قابل تنظیم و Publish Guard |
| Domain/Commerce/Subscription.Monthly.cs | مالک User/Organization، دوره، کنترل تجاری و کاهش برنامه‌ریزی‌شده |
| Domain/Commerce/SubscriptionAmendment.cs | سند تغییر نسخه، زمان اثر و نسبت ارتقا بدون حذف مصرف |
| Domain/Commerce/UsageReservation.cs | انتقال وضعیت، حفظ Unknown و جلوگیری از بازنویسی نتیجهٔ قطعی |
| Domain/Commerce/UsageLedgerEntry.Reservations.cs | رویدادهای immutable مصرف با مالک، مصرف‌کننده، Context، نسخه و دوره |
| Domain/Academic/PersonalClass*.cs | کلاس User-owned و عضویت ناشی از پذیرش صریح دعوت |
| Domain/Reports/ReportExportJob.Commerce.cs | اتصال Job به رزرو و شناسهٔ درخواست؛ بدون وابستگی Domain به EF |
| Application/Commerce/CommercialActorAccess.cs | استفاده از Identity port فعلی برای نشست و حساب فعال؛ حق شخصی به نقش سازمانی وابسته نمی‌شود |
| Application/Commerce/CommercialSubscriptionService.cs | انتخاب مالک/زمینه، Preflight و use case داخلی رزرو متن ساده |
| Application/Commerce/CommercialAdminService.cs | مجوز مرکزی، تنظیم Draft/عرضه و ثبت قرارداد مدیریت‌شده |
| Application/Academic/PersonalClassService.cs | اعتبارسنجی و use caseهای کلاس مستقل |
| Infrastructure/Commerce/EfCommercialStore*.cs | قفل مالک، شمارش SQL، رزرو اتمیک، نهایی‌سازی و Summary |
| Infrastructure/Commerce/EfCommercialAdminStore.cs | transaction، rowversion و Audit تغییرات مالی |
| Infrastructure/Commerce/EfOrderPaymentStore.*.cs | توسعهٔ همان مسیر خرید با Quote، Individual و تغییر پلن |
| Infrastructure/Commerce/EfSubscriptionActivationHandler.cs | اعمال اثر پرداخت از Order معتبر، در Outbox موجود |
| Infrastructure/Commerce/CommercialWritePolicy.cs | کنترل نهایی writeهای آموزشی و ظرفیت سازمان مدیریت‌شده |
| Infrastructure/Academic/EfPersonalClassStore.cs | ظرفیت مشترک میزبان و پذیرش هم‌زمان دعوت |
| Infrastructure/Reports/Exports/EfReportExportJobStore.cs | ثبت اتمیک Job و رزرو، fencing و تطبیق نتیجهٔ terminal |
| Api/Controllers/CommercialSubscriptionsController.cs | قراردادهای HTTP شخصی/سازمانی و Preflight |
| Api/Controllers/PersonalClassesController.cs | HTTP کلاس مستقل؛ بدون SQL یا قواعد سهمیه در Controller |
| Api/Controllers/PlatformCommercialContractsController.cs | endpointهای مرکزی با policyهای CommerceRead/CommerceManage |

پیشوند چهار پوشهٔ لایه در جدول بالا، MakanApp. است؛ برای مثال Domain/Commerce یعنی MakanApp.Domain/Commerce.

جهت وابستگی عوض نشده است: Api به Application/Infrastructure، Infrastructure به Application/Domain، و Application به Domain وابسته است. Domain به HTTP، EF، SQL Server، SMS یا AI provider وابسته نشده است.

## Account، نقش و قرارداد

خرید، Teacher/Student/Manager رسمی، Membership سازمانی، RoleAssignment، Enrollment یا Badge ایجاد نمی‌کند. برای عملیات سازمانی ابتدا Context معتبر و نقش/مجوز بررسی می‌شود؛ سپس قرارداد تجاری آن سازمان.

Individual به User تعلق دارد و تغییر نقش یا سازمان مالک آن را عوض نمی‌کند. مسیرهای شخصی جدید، اعتبار نشست و حساب را با CommercialActorAccess می‌سنجند؛ انتخاب قبلی یک workspace سازمانیِ دیگر، جای مالکیت شخصی را نمی‌گیرد.

Manager برای امور مالی فقط سازمان فعال و مجاز خودش را می‌بیند. Platform Admin نیز از permissionهای موجود استفاده می‌کند؛ School Manager مجوز مدیریت Global Plan نمی‌گیرد.

## PersonalClass

مدل جدا انتخاب شد، زیرا تبدیل Class موجود به کلاس بدون سازمان، FKهای ترکیبی Academic و Assessment را درگیر می‌کرد. Organization صوری ساخته نمی‌شود و مالک تجاری سوم نداریم.

- HostUserId مالک کلاس است.
- ساخت کلاس به PersonalClassHosting و ظرفیت ActiveClasses میزبان نیاز دارد.
- دعوت به User مشخص متصل است؛ فقط همان User با نشست معتبر می‌تواند بپذیرد.
- پذیرش دعوت، فقط PersonalClassParticipant می‌سازد؛ نقش یا Enrollment سازمانی نمی‌سازد.
- دانشجو برای مشاهدهٔ رکورد کلاس و عضویت پایه، اشتراک شخصی پولی لازم ندارد.
- ActiveLearners دانشجوی یکتای عضو کلاس‌های فعال یک میزبان است؛ عضویت همان دانشجو در دو کلاس دوباره شمرده نمی‌شود.
- پذیرش هم‌زمان آخرین صندلی با قفل مشترک میزبان کنترل می‌شود.
- بایگانی کلاس و پایان عضویت، ظرفیت را کاهش می‌دهند و به تمدید اشتراک مجبور نمی‌شوند.
- DTO کلاس شامل قیمت، SubscriptionId یا ماندهٔ مالی میزبان نیست.
- AI دانشجو، منبع شخصی/Trial خودش را استفاده می‌کند و به ماندهٔ میزبان دسترسی ندارد.

CanCreateEducationalContent فعلاً false و EducationalWorkflowStatus برابر NotAvailable است. این مقادیر عمداً ادعای وجود Assignment/Exam مستقل نمی‌کنند. مشارکت کامل آموزشی، فایل و Feedback مستقل مطابق تصمیم مالک محصول موکول شده‌اند.

## Plan و PlanVersion

Plan دارای Audience برابر Individual یا Organization و سه تنظیم مستقل CatalogVisible، NewPurchaseAvailable و RenewalAvailable است. تغییر عرضه زمان اثر و Audit دارد؛ Hidden قرارداد فعال را لغو نمی‌کند.

PlanVersion شامل قیمت، Entitlement، Quota، Calendar ماهانه، SaleTermsReference و آستانه‌های اختیاری هشدار است. تغییر نسخهٔ Published از مسیر Draft update رد می‌شود؛ نسخهٔ جدید، قرارداد جاری را خودکار جابه‌جا نمی‌کند.

Publish Guard این موارد را کنترل می‌کند:

- قیمت تعیین شده باشد؛ صفر فقط با ExplicitlyFree پذیرفته می‌شود.
- تقویم ماهانه و مرجع شرایط فروش صریح باشند.
- Entitlementها تعیین شده باشند.
- Quotaهای وابسته به قابلیت فعال وجود داشته باشند.
- PersonalClassHosting و PersonalSources به قرارداد Organization فروخته نشوند.
- Unset با صفر یا Unlimited یکی نباشد؛ null فقط در یک quota definition موجود، Unlimited صریح است.

در ورودی JSON مربوط به PlatformPlanQuotaItem، حضور Limit اجباری است: حذف Limit خطای ۴۰۰ می‌دهد، Limit=0 سقف صفر است و Limit=null، Unlimited صریح. JsonRequired فقط روی همین فیلد اعمال شده و تنظیم عمومی JSON یا Authentication تغییر نکرده است.

هیچ نام، قیمت یا سقف پولی واقعی Seed نشده است. اعداد تست، مقادیر تجاری تأییدشده نیستند.

## پول، دوره و تغییر پلن

برای حفظ سوابق قبلی، persistence و قرارداد فعلی درگاه همچنان IRR هستند. Quote، Catalog، Summary و فیلدهای نمایشی Order مقدار تومان را از سرور می‌دهند. PriceToman در ویرایش Draft سمت سرور به IRR تبدیل می‌شود؛ مرورگر مبلغ نهایی Order را تعیین نمی‌کند.

برداشت خودکار وجود ندارد. RenewalMode برابر Manual و AutoRenew برابر false است. واحد موردنیاز هر درگاه واقعی باید هنگام ساخت adapter همان درگاه صریح تعیین شود؛ provider جدید فرض نشده است.

دوره یک ماه تقویمی است، با Calendar ثبت‌شده در PlanVersion و BillingAnchorUtc/BillingPeriodIndex. لنگر اصلی حفظ می‌شود؛ کوتاه‌بودن یک ماه، لنگر ماه‌های بعد را دائماً جابه‌جا نمی‌کند.

Quote یک پیش‌نمایش با اعتبار محدود است و حق ایجاد نمی‌کند. برای شروع دورهٔ تازه، StartsUponVerifiedPayment مشخص می‌کند که تاریخ نمایشی تخمینی است؛ آغاز واقعی از پرداخت تأییدشده ثبت می‌شود. تمدید زودهنگام روی سررسید موجود قرار می‌گیرد و سهمیهٔ آینده پیش از شروع آزاد نمی‌شود.

Upgrade:

- فقط تغییر غیرکاهنده و سازگار در میانهٔ دوره پذیرفته می‌شود.
- پس از پرداخت معتبر اعمال می‌شود و due date ثابت می‌ماند.
- InitialPlanVersionId، رزروها و ledger حذف نمی‌شوند؛ SubscriptionAmendment نسخهٔ قبلی/بعدی و Order را نگه می‌دارد.
- زمان اثر، زمان اعمال پس از پرداخت است؛ مبنای proration از Order.CreatedAtUtc و RemainingTicks حفظ می‌شود.
- افزایش ظرفیت جاری کامل است؛ افزایش سهمیهٔ مصرفی نسبت به زمان باقی‌مانده محاسبه می‌شود.
- کسر سهمیه با BigInteger حفظ و فقط سقف نهایی floor می‌شود.
- مبلغ تجمعی ارتقا به نزدیک‌ترین تومان با half-up گرد و مبلغ قبلی از آن کم می‌شود؛ ارتقاهای متوالی drift ایجاد نمی‌کنند.
- بردار آزمون: ۱۰۰ به ۲۰۰ در نیمهٔ دوره، سقف ۱۵۰؛ با مصرف ۸۰ و رزرو ۱۰، ماندهٔ ۶۰. این اعداد فقط test vector هستند.

Downgrade فقط ScheduledPlanVersionId را برای دورهٔ بعد ثبت می‌کند؛ حقوق دورهٔ جاری فوراً کم نمی‌شوند. تمدید همچنان پرداخت دستی می‌خواهد. دادهٔ مازاد حذف نمی‌شود؛ مشاهده و کاهش ظرفیت ممکن و افزودن ظرفیت بیش از سقف رد می‌شود. دورهٔ آینده‌ای که قبلاً پرداخت شده، با Schedule دوباره بازنویسی نمی‌شود.

تغییر تقویم در قرارداد جاری/تمدید این نسخه پشتیبانی نمی‌شود؛ نباید به‌صورت پنهان دورهٔ مالی را عوض کند.

## فعال‌سازی کنترل سازمان و Grace

Migration برای سوابق قدیمی CommercialControlEnabled=false می‌گذارد و EndsAtUtc را حدس نمی‌زند. سازمان فاقد Subscription نیز خودکار قفل نمی‌شود.

ثبت قرارداد معتبر از endpoint مرکزی، پس از بررسی permission و rowversion، نقطهٔ فعال‌شدن کنترل است. قرارداد مدیریت‌شده با مسیر override قدیمی به حالت بدون کنترل برنمی‌گردد.

تصمیم تکمیلی Legacy:

- MigrationStatus سازمان پیش از قرارداد معتبر Admin برابر AwaitingValidContract است؛ در Individual مقدار NotApplicable دارد.
- رکورد Legacy در Summary جدید با State=LegacyPendingContract و LegacySubscriptionId شناخته می‌شود؛ SubscriptionId، PlanVersionId قرارداد جاری، قیمت، دوره، Entitlements و Quotas ساخته یا حدس زده نمی‌شوند.
- سازمان بدون رکورد اشتراک، State=NoSubscription و MigrationStatus=AwaitingValidContract دارد؛ صرف خواندن گزارش برای آن Subscription نمی‌سازد.
- در Summary قدیمی، State قبلی برای سازگاری مشتریان موجود حفظ می‌شود؛ MigrationStatus=AwaitingValidContract و HasVerifiedCommercialContract=false صریحاً نشان می‌دهند که این State به معنی قرارداد تجاری معتبر نیست. مقادیر مستند تاریخیِ کلیدهای قدیمی نیز حفظ می‌شوند. AI/PersonalClassHosting/PersonalSources و quotaهای جدید از Legacy حق نمی‌گیرند.
- مسیرهای تجاری جدید، Legacy را قرارداد مؤثر نمی‌شمارند؛ وجود EndsAtUtc=null به معنی Unlimited واقعی یا سهمیهٔ تازه نیست.
- ثبت موفق Admin، MigrationStatus را Completed می‌کند و CommercialEnforcementFromUtc زمان واقعی ثبت/شروع قرارداد را نشان می‌دهد. لنگر صریح دوره می‌تواند قدیمی‌تر باشد، اما تاریخ کنترل تجاری عقب برده نمی‌شود.
- رکورد قبلی با تاریخ‌ها و مصرف اصلی نگه داشته و پایان وضعیت آن در زمان ثبت قرارداد Audit می‌شود؛ مصرف به قرارداد تازه منتقل یا تاریخ گذشته تخمین زده نمی‌شود.
- وضعیت مهاجرت و تاریخ کنترل از دادهٔ موجود قرارداد محاسبه می‌شوند؛ این اصلاح به Migration دیتابیس تازه نیاز ندارد.

| مالک / سابقه | MigrationStatus | State در Summary جدید | قرارداد تجاری تأییدشده |
|---|---|---|---|
| Organization با سابقهٔ بدون قرارداد مدیریت‌شده | AwaitingValidContract | LegacyPendingContract | false |
| Organization بدون Subscription | AwaitingValidContract | NoSubscription یا PendingPayment | false |
| Organization پس از ثبت معتبر Admin | Completed | وضعیت واقعی قرارداد، مانند Active/GracePeriod/Expired | true؛ فعال‌بودن جدا از معتبر بودن قرارداد است |
| Individual | NotApplicable | وضعیت واقعی اشتراک شخصی | مطابق قرارداد شخصی |

LegacyPendingContract به معنی Lock آموزشی یا الزام خرید برای عملیات قبلی نیست. Frontend باید وضعیت مهاجرت را به‌عنوان نیاز به ثبت قرارداد توسط Platform Admin نشان دهد؛ خرید یا تفسیر تاریخ نامعلوم به Unlimited جای آن را نمی‌گیرد.

فایل‌های اصلاح تکمیلی: Subscription.Monthly.cs برای وضعیت محاسبه‌شده و زمان واقعی کنترل؛ CommercialContracts.cs و CommerceContracts.cs برای قرارداد گزارش؛ EfCommercialStore برای جداسازی مسیرهای جدید از Legacy؛ CommercePolicy/CommerceService/EfCommerceStore برای حفظ کلیدهای قبلی و رد حق تازه؛ EfCommercialAdminStore برای اعتبارسنجی و Audit گذار. هیچ endpoint یا جدول تازه‌ای برای این اصلاح ساخته نشده است.

جریان خواندن: CommercialSubscriptionsController -> CommercialSubscriptionService -> EfCommercialStore.SummaryAsync -> دادهٔ تاریخی/وضعیت مهاجرت -> HTTP Response. جریان ثبت: PlatformCommercialContractsController -> CommercialAdminService -> EfCommercialAdminStore.RegisterAsync -> Subscription.CreateMonthly -> SQL transaction + AdminAudit -> گزارش Completed. قفل مشترک مالک و ETag قبلی از جایگزینی هم‌زمان جلوگیری می‌کنند.

برای قرارداد مدیریت‌شده:

- هفت روز پس از پایان، فعالیت موجود در حدود قرارداد قبلی ادامه دارد؛ دوره یا AI quota تازه ساخته نمی‌شود.
- ساخت کلاس/دوره/درس و ایجاد ظرفیت یا دسترسی تازه در Grace مجاز نیست.
- پس از Grace، write آموزشی رد و خواندن دادهٔ مجاز حفظ می‌شود.
- پایان Enrollment، TeacherAssignment، Membership و OrganizationPerson و قطع رابطهٔ نادرست Guardian از کنترل کاهش دسترسی عبور می‌کنند.
- تغییر نام Person مربوط به حساب شخصی به کنترل تجاری آموزشگاه متصل نمی‌شود؛ تغییر مدرسه‌ای Person بدون حساب، scope سازمان را رعایت می‌کند.
- Messaging در CommercialWritePolicy قرار نگرفته است؛ مجوزها، Safety و Block قبلی همچنان حاکم‌اند.
- Saved Messages مانند قبل به workspace شخصی نیاز دارد؛ این شرط با انقضای اشتراک یکی نیست.
- تمدید، پرداخت و پشتیبانی تجاری مسدود نمی‌شوند.

مهلت سازمان به Individual تعمیم داده نمی‌شود. انقضای Individual، حساب پایه و پیام‌رسان شخصی را حذف نمی‌کند.

تنظیم‌های اختیاری سیستم: Commerce:QuoteLifetimeMinutes و Commerce:RenewalAfterGraceStartsAtPayment. مقدار پیش‌فرض فنی اعتبار Quote برابر ۱۵ دقیقه است؛ شروع دوباره پس از Grace از پرداخت، یک پیشنهاد اجرایی قابل تنظیم است. هیچ فایل appsettings برای این مرحله بازنویسی نشده است.

## Quota، شمارش و رزرو

| کلید | نوع | منبع/وضعیت |
|---|---|---|
| ActiveClasses | CurrentCapacity | کلاس فعال همان مالک؛ به شروع ماه وابسته نیست |
| ActiveLearners | CurrentCapacity | دانشجوی distinct در عضویت کلاس‌های فعال همان مالک |
| StorageBytes سازمان | CurrentCapacity | FileAssetهای Ready سازمان در مدل موجود |
| StorageBytes کلاس مستقل | CurrentCapacity | قرارداد سقف موجود؛ workflow فایل مستقل در این مرحله نیست |
| AssignmentFileBytes | CurrentCapacity | بنیاد قرارداد؛ meter اجرای فایل مستقل هنوز موجود نیست |
| SourceCount | CurrentCapacity | بنیاد قرارداد؛ lifecycle منابع شخصی خارج از این مرحله |
| AiTextRequests | PeriodicConsumption | رزرو/مصرف دورهٔ دقیق یا روز Trial |
| ReportExports | PeriodicConsumption | رزرو متصل به Job و مصرف پس از خروجی موفق |

برای meterهای خارج از دامنه، IsUsageAvailable=false و Remaining=null است. این وضعیت نباید صفرِ مصرف قطعی یا Unlimited تلقی شود. تلاش از مسیر جدید افزایش ظرفیت با meter ناموجود، COMMERCE_METER_UNAVAILABLE می‌دهد. روی Upload عمومی موجود، gate سراسری تازه‌ای که Messaging را قطع کند نصب نشده است.

آستانه‌های WarningPercent و CriticalWarningPercent اختیاری و versioned هستند. مقدارهای ۸۰/۹۵ در تست‌ها نمونه‌اند و Business Constant فروش نیستند.

جریان رزرو:

Authorize -> resolve one payer/context -> transaction کوتاه -> قفل مالک -> بررسی retry و مانده -> Reserved + ledger event -> commit -> اجرا بیرون transaction -> Consumed/Released/Unknown.

کلید تکرار UsageReservation برابر ConsumerUserId + OperationId است. Owner، Context، QuotaKey، Trial و PayloadHash نیز باید مطابق درخواست قبلی باشند. تغییر منبع برای یک عملیات دارای رزرو، برداشت دوم ایجاد نمی‌کند و conflict است.

Unknown سهمیه را نگه می‌دارد؛ timeout معادل failure نیست. نتیجهٔ دیررس به همان Subscription، PlanVersion و Period اولیه متصل می‌ماند. نهایی‌سازی از port داخلی است و endpoint عمومی برای اعلام موفقیت مصرف توسط مشتری ساخته نشده است.

متد legacy ثبت مصرف مستقیم برای قرارداد مدیریت‌شده رد می‌شود؛ این قرارداد باید از رزرو/نهایی‌سازی استفاده کند. read model قدیمی نیز دورهٔ Scheduled مؤثر، سقف prorated و Reserved را رعایت می‌کند.

## Trial و انتخاب Payer

Trial دقیقاً سه پاسخ موفق سادهٔ متنی در روز برای Account است؛ device، session، نقش، سازمان یا PersonalClass سقف جدید ایجاد نمی‌کنند. retry همان OperationId مصرف دوم ندارد. File/Image/Audio یا پردازش سنگین داخل این سهمیه قرار نگرفته است.

مرز روز، ۰۰:۰۰ تهران است. دادهٔ timezone قدیمی Windows در تست یک خطای یک‌ساعته نشان داد؛ محاسبهٔ تجاری از قاعدهٔ فعلی تهران بدون DST از ۲۰۲۳ استفاده می‌کند. مرجع: [توضیح نگهدارندگان IANA](https://lists.iana.org/hyperkitty/list/tz%40iana.org/message/KU35AF3J6H6NOGGWBS7FOFPIULA7VDHD/). تغییر آیندهٔ قاعدهٔ رسمی زمان نیازمند به‌روزرسانی همین محاسبه و تست‌هاست.

| زمینه | منبع |
|---|---|
| استفادهٔ شخصی | Individual خود User |
| کلاس و ظرفیت دانشجوی کلاس مستقل | Individual میزبان |
| AI دانشجوی کلاس مستقل | Individual یا Trial خود دانشجو |
| قابلیت سازمانی | Organization همان Context |
| AI سازمانی پیش‌فرض | همان Organization |
| AI سازمانی با انتخاب صریح | Individual همان User، بدون انتقال مجوز داده |
| Report Export | Organization؛ بدون fallback شخصی یا Trial |

FreeTrial انتخاب مستقل دارد. پس از اتمام اعتبار سازمان یا فرد، انتقال پنهان به منبع دیگر وجود ندارد. Preflight، Payer، Owner، واحد، مقدار، مانده و پایان دوره را می‌دهد. AuthorizesEducationalData=false صریحاً می‌گوید این پاسخ جای مجوز داده/عملیات آموزشی را نمی‌گیرد.

AI Runtime فعال نشده است. مسیر رزرو متن ساده، بنیاد داخلی قابل‌آزمون است؛ به‌عنوان اجرای واقعی AI یا اتصال Production گزارش نمی‌شود.

## Export، retry و Recovery

ReportExportService ابتدا مجوز و دامنهٔ گزارش را از data source موجود می‌گیرد. برای سازمان مدیریت‌شده، EfReportExportJobStore در یک transaction، Job و رزرو ReportExports را ثبت می‌کند.

ClientOperationId به CreateReportExportCommand اضافه شده است. Frontend جدید باید آن را پیش از ارسال تولید و در retry حفظ کند. همان User/Operation با همان payload و Context به همان Job برمی‌گردد؛ payload متفاوت conflict است. برای سازگاری مشتری قدیمی، نبود کلید به شناسهٔ تازهٔ سروری تبدیل می‌شود؛ retry امن پس از گم‌شدن پاسخ بدون کلیدِ تولیدشده در مشتری قابل تضمین نیست.

CSV بیرون transaction ساخته می‌شود. فقط transition موفق و دارای claim معتبر، رزرو را Consumed می‌کند. retry یا از دست‌رفتن lease برداشت تازه ندارد. شکست terminal پس از fencing، رزرو را Released می‌کند؛ worker قدیمی دیگر نمی‌تواند همان Job را Completed کند.

تطبیق bounded برای Jobهای Completed/Failed و رزروهای مانده وجود دارد. خطای نامعلوم به‌صرف گذشت زمان فوراً آزاد نمی‌شود. Jobهای قدیمی بدون رزرو، retroactive charge نمی‌شوند.

## APIهای کاربر و Frontend

همهٔ مسیرها نشست معتبر می‌خواهند. مسیر سازمانیِ مالی، Manager همان workspace را می‌خواهد؛ شناسهٔ دلخواه در query مجوز یا مالکیت ایجاد نمی‌کند.

| Method | Route | ورودی/نتیجهٔ مهم |
|---|---|---|
| GET | /api/v1/commerce/catalog?audience=Individual یا Organization | فقط عرضهٔ عمومیِ قابل خرید و کامل |
| GET | /api/v1/commerce/personal/subscription | قرارداد و ماندهٔ خود User |
| GET | /api/v1/commerce/organization/subscription | قرارداد سازمان فعال مدیر |
| POST | /api/v1/commerce/personal/quote | PlanVersionId، Purpose، SubscriptionId؛ قیمت و سقف مؤثر سروری |
| POST | /api/v1/commerce/organization/quote | همان قرارداد، با مالک سازمان مجاز |
| POST | /api/v1/commerce/personal/orders | PlanVersionId، ClientOperationId، Purpose و SubscriptionId در صورت نیاز |
| GET | /api/v1/commerce/personal/orders | فهرست متعلق به خود User |
| GET | /api/v1/commerce/personal/orders/{orderId} | Order و fulfillment state مجاز |
| POST | /api/v1/commerce/personal/orders/{orderId}/payments | initiation موجود؛ بدون اعلام مبلغ موفق از مشتری |
| POST | /api/v1/commerce/personal/subscription/downgrade | نسخهٔ هدف، ExpectedVersion و Reason |
| POST | /api/v1/commerce/organization/subscription/downgrade | همان عملیات برای سازمان مجاز |
| POST | /api/v1/commerce/ai/simple-text/preflight | ContextType، ContextId و Payer اختیاری؛ اجرا/برداشت انجام نمی‌دهد |
| POST / GET | /api/v1/personal-classes | ساخت با Title/ClientOperationId یا فهرست مجاز |
| GET | /api/v1/personal-classes/{classId} | رکورد مجاز، بدون امور مالی میزبان |
| POST | /api/v1/personal-classes/{classId}/archive | فقط میزبان؛ بدون حذف داده |
| POST | /api/v1/personal-classes/{classId}/invitations | DestinationUserId و ClientOperationId؛ فقط میزبان |
| GET | /api/v1/personal-classes/invitations | دعوت‌های متعلق به خود User |
| POST | /api/v1/personal-classes/invitations/{id}/accept | پذیرش توسط مقصد دعوت |
| GET | /api/v1/personal-classes/{classId}/participants | میزبان: فهرست؛ دانشجو: رکورد خودش |
| DELETE | /api/v1/personal-classes/{classId}/participants/{participantId} | خروج خود فرد یا پایان‌دادن توسط میزبان |
| POST | /api/v1/reports/exports | قرارداد قبلی + ClientOperationId اختیاری برای retry امن |

APIهای قبلی /api/v1/commerce/summary و orders سازمانی حفظ شده‌اند. گزارش‌های قدیمی نیز از مسیرهای قبلی خوانده/دانلود می‌شوند. پاسخ ساخت Order تازه ۲۰۱ و replay آن ۲۰۰ است؛ ساخت Job گزارش همان ۲۰۲ قبلی را حفظ می‌کند. فهرست‌های سادهٔ جدید فعلاً حداکثر ۱۰۰ مورد دارند.

خطاهای قابل تفکیک:

| Code | HTTP | مفهوم |
|---|---|---|
| AUTH_REQUIRED | 401 | نشست معتبر نیست |
| COMMERCE_CONTEXT_NOT_ALLOWED یا خطای موجود workspace/role | 403 | مجوز زمینه وجود ندارد؛ خرید جای نقش نیست |
| FEATURE_NOT_ENTITLED | 403 | قابلیت در قرارداد نیست |
| QUOTA_EXCEEDED | 409 | منبع انتخاب‌شده ماندهٔ کافی ندارد |
| ORGANIZATION_COMMERCIALLY_LOCKED | 409 | write آموزشی پس از Grace قفل است |
| COMMERCE_USAGE_CONFLICT | 409 | اختلاف payload/context/payer یا نسخهٔ هم‌زمان |
| COMMERCE_RESOURCE_NOT_FOUND | 404 | منبع مجاز یافت نشد؛ receipt مالک دیگر داده نمی‌شود |
| PLAN_TERMS_INCOMPLETE | 422 | شرایط لازم قرارداد تعیین نشده‌اند |
| COMMERCE_METER_UNAVAILABLE | 503 | مسیر meter مربوط هنوز در این دامنه قابل اجرا نیست |

خطای AUTH_REQUIRED از catalog موجود Identity استفاده می‌کند. Publish ناقص در مسیر Platform فعلی با conflict همان catalog مرکزی رد می‌شود.

ProblemDetails و trace/correlation موجود حفظ شده‌اند؛ Secret، OTP، JWT، محتوای Messenger یا payload خصوصی AI وارد لاگ/ledger نشده است.

Summary، Stateهای NoSubscription، PendingPayment، Active، QuotaWarning، QuotaExceeded، GracePeriod، Expired، ScheduledDowngrade و LegacyPendingContract را می‌دهد. MigrationStatus و HasVerifiedCommercialContract، انتظار قرارداد معتبر را از اشتراک واقعی جدا می‌کنند. HasPendingPayment کنار حق فعال قبلی قرار می‌گیرد و آن حق را قطع نمی‌کند. Frontend باید IsUsageAvailable، نسخه، دوره و selected payer را بخواند و از نام پلن مجوز نسازد.

## APIهای Admin

ساخت Plan/Version، انتشار و فهرست‌های قبلی PlatformCommerceController حفظ شده‌اند. Audience به ساخت Plan و شرایط ماهانه به ساخت Version اضافه شده‌اند. ورودی قدیمی PriceAmount بر حسب IRR باقی است؛ ویرایش جدید Draft، PriceToman می‌گیرد.

پایه: /api/admin/v1/commerce.

| Method | Route | مجوز |
|---|---|---|
| PUT | plans/{planId}/availability | CommerceManage + ExpectedVersion + Reason |
| PUT | plan-versions/{versionId}/draft | CommerceManage؛ قیمت/تقویم/هشدار/Entitlement/Quota |
| GET | plan-versions/{versionId}/validation | CommerceRead؛ MissingFields و CanPublish |
| POST | organizations/{ownerId}/commercial-contracts | CommerceManage؛ ثبت نخستین قرارداد معتبر و فعال‌سازی کنترل |
| POST | users/{ownerId}/commercial-contracts | CommerceManage؛ قرارداد معتبر Individual |
| GET | organizations/{ownerId}/subscription | CommerceRead؛ مشاهدهٔ مرکزی سازمان مشخص |
| GET | users/{ownerId}/subscription | CommerceRead؛ مشاهدهٔ مرکزی User مشخص |
| GET | history/{targetId} | CommerceRead؛ سوابق با actor، زمان، reason و before/after |

ثبت قرارداد، PlanVersionId، BillingAnchorUtc، BillingPeriodIndex، ExpectedCurrentVersion در صورت وجود سابقهٔ فعال و Reason می‌خواهد. دورهٔ ثبت‌شده باید در زمان ثبت معتبر باشد. این مسیر override آزادِ حقوق پرداخت‌شده یا refund نیست.

## SQL Server و Migration

دو Migration واقعی:

1. 20261029111645_AddCommercialOwnershipPeriodsAndPersonalClasses
2. 20261029122246_BindReportExportsToCommercialReservations

جدول‌های تازه: academic.PersonalClasses، PersonalClassInvitations، PersonalClassParticipants؛ commerce.UsageReservations و SubscriptionAmendments.

Subscription به دو مالک توسعه یافته و InitialPlanVersionId، BillingAnchorUtc، BillingPeriodIndex، CommercialControlEnabled و ScheduledPlanVersionId دارد. UsageLedger نسخه، Consumer، Context، Period، Reservation، Status و ReasonCode دارد.

یکتایی‌ها و FKهای مهم:

- Plan.Code و PlanId/VersionNumber قبلی حفظ شده‌اند.
- کلید مالک Subscription برابر OwnerType/OwnerId/Id است.
- FK ترکیبی ledger و reservation، مالک/قرارداد را متصل می‌کند.
- FK ledger به reservation، Owner و Consumer را نیز رعایت می‌کند.
- ConsumerUserId/OperationId رزرو یکتا است.
- ReservationId/Status در ledger یکتا و رویداد Consumed برای عملیات قرارداد یکتا است.
- SourceOrderId اشتراک و OrderId سند ارتقا یکتا هستند.
- عضویت فعال PersonalClass/Student یکتا است.
- ReportExportJob دارای کلید یکتای RequestedByUserId/ClientOperationId و FK ترکیبی Organization/Consumer به reservation است.
- rowversion و transaction کوتاه کنار constraintها استفاده شده‌اند.

Backfill فقط مالک سازمانی، نسخهٔ شناخته‌شده و مصرف قطعی قدیمی را منتقل می‌کند. Consumer ناشناخته و end date ناشناخته حدس زده نمی‌شوند. SQL پویا در backfill از مشکل compile ستون تازه در اسکریپت idempotent جلوگیری می‌کند.

Down در صورت وجود دادهٔ جدید قرارداد/رزرو/کلاس یا idempotency گزارش متوقف می‌شود تا تاریخچه بی‌صدا حذف نشود.

هر دو Migration روی LocalDB موقت با دادهٔ legacy آزموده شده‌اند. اسکریپت idempotent مربوط به Migration اول، شامل backfill، با اجرای batchهای SQL نیز بررسی شده است؛ Migration دوم و اجرای تکراری زنجیره از مسیر EF آزموده شده‌اند. فایل SQL افزایشی تولیدیِ هر دو Migration:
.artifacts/subscription-20261029-fae9be2e/sql/subscription-20261029.sql

این اسکریپت افزایشی از 20261028202239_CompleteSchoolManagement تا دو Migration جدید است؛ جایگزین زنجیرهٔ Migrationهای قبلی یا اسکریپت نصب دیتابیس خالی نیست.

این فایل روی MakanApp، MakanApp_Test یا سرور اجرا نشده است. اجرای Migration در startup اضافه نشده است.

## تست‌ها و ماتریس پذیرش

فایل‌های جدید:

- tests/MakanApp.UnitTests/Commerce/CommercialProductRulesTests.cs
- tests/MakanApp.IntegrationTests/CommercialSubscriptionEndpointsTests*.cs
- tests/MakanApp.IntegrationTests/CommercialSubscriptionMigrationTests.cs

اصلاح Legacy دو Unit و سه Integration به موارد اولیه اضافه کرد. CommercialSubscriptionEndpointsTests.Legacy.cs نبود قرارداد معتبر، تداوم write قبلی، عدم حق جدید از quota تاریخی حتی با Limit=null، رد ثبت ناقص و گذار معتبر Admin بدون بازنویسی تاریخ/مصرف را بررسی می‌کند. تست JSON و تست HTTP ویرایش Draft نیز حذف Limit را در مقابل صفر و null صریح می‌سنجند.

fixtureهای قبلی Commerce برای ارائهٔ صریح قیمت/دورهٔ معتبر به‌روز شدند؛ تستی برای عبور از Publish Guard غیرفعال نشده است. تست Recovery موجود فقط dependency جدید Store را دریافت کرده است.

مجموع پیاده‌سازی پایه و اصلاح Legacy، ۱۶ مورد Unit و ۱۹ تست Integration جدید دارد؛ ۱۸ تست Integration مسیر HTTP/use case و یک تست مهاجرت دادهٔ legacy را پوشش می‌دهند. تست Architecture تازه‌ای لازم نشد و مجموعهٔ موجود برای حفظ جهت وابستگی‌ها اجرا و تأیید شد.

| پذیرش | شواهد تست |
|---|---|
| ۱. خرید Individual بدون سازمان | IndividualPurchaseWithoutOrganizationActivatesExactlyOnceAndCreatesNoRole |
| ۲ و ۳. Host مستقل بدون نقش سازمانی | PersonalClassParticipationNeedsHostContractButNoStudentPurchaseAndNeverExposesHostFinance |
| ۴ و ۵. عضویت پایهٔ دانشجو بدون خرید و بدون امور مالی میزبان | همان تست؛ چرخهٔ Assignment/Exam مستقل طبق تصمیم، deferred است |
| ۶. AI دانشجو از خودش | همان تست، Trial کلاس و ثابت‌ماندن ماندهٔ میزبان |
| ۷. مدیر فقط سازمان خودش | OrganizationPayerNeverFallsBackAndPersonalPayerNeverGrantsOrganizationAuthorization و تست‌های قبلی |
| ۸ و ۹. Hidden و immutability | HiddenPlanAndNewVersionPreserveActiveContractAndPublishedTerms |
| ۱۰. خطای Feature و Quota متفاوت | FeatureNotEntitledAndQuotaExceededAreDifferentAndCrossAccountReceiptsAreHidden |
| ۱۱. آخرین واحد هم‌زمان | ConcurrentLastAiUnitReservesOnceAndUnknownStaysInOriginalDayUntilLateFinalization |
| ۱۲. retry یک بار مصرف | TrialCountsThreeSuccessfulRequestsAcrossContextsAndRetryOnlyOnce |
| ۱۳. حفظ مصرف در Upgrade | ImmediatePaidUpgradeProduces150And60PreservesReservationsAndRepeatedPaymentHasOneEffect |
| ۱۴. پیام‌رسان پس از انقضا | AdminRegistrationActivatesLockAfterGraceButReadMessagingAndAccessReductionContinue |
| ۱۵ و ۱۶. روز تهران، Account و retry بین دستگاه‌ها | TrialCountsThreeSuccessfulRequestsAcrossContextsAndRetryOnlyOnce و Unit مرز روز |
| ۱۷ تا ۱۹. عدم fallback، انتخاب صریح و حفظ مجوز داده | OrganizationPayerNeverFallsBackAndPersonalPayerNeverGrantsOrganizationAuthorization |
| ۲۰ و ۲۱. Lock پس از هفت روز و کاهش دسترسی | AdminRegistrationActivatesLockAfterGraceButReadMessagingAndAccessReductionContinue |
| ۲۲. حفظ قرارداد با نسخهٔ تازه | HiddenPlanAndNewVersionPreserveActiveContractAndPublishedTerms |
| ۲۳ و ۲۴. سقف ۱۵۰ و ماندهٔ ۶۰ | ImmediatePaidUpgradeProduces150And60PreservesReservationsAndRepeatedPaymentHasOneEffect |
| ۲۵. تأیید تکراری پرداخت و سهمیهٔ آینده | IndividualPurchaseWithoutOrganizationActivatesExactlyOnceAndCreatesNoRole و EarlyPaidRenewalStaysInFutureAndScheduledDowngradeRetainsExistingCapacity |
| ۲۶. کاهش پلن بدون حذف مازاد | EarlyPaidRenewalStaysInFutureAndScheduledDowngradeRetainsExistingCapacity |
| آخرین صندلی و دانشجوی یکتا | HostLearnerCapacityIsUniqueAcrossClassesAndConcurrentLastSeatHasOneWinner |
| حفظ حق شخصی پس از پایان نقش | LosingOrganizationRoleDoesNotInvalidateIndividualContractOrPersonalClassHostAccess |
| انتشار ناقص، مرز Admin/Manager و رد قیمت خارج از محدوده بدون Overflow | AdminCannotPublishUnsetTermsAndSchoolManagerCannotManageGlobalPlans |
| Export اتمیک و retry | ReportExportReservesWithJobAndConcurrentRetryConsumesOnceAfterSuccessfulGeneration |
| terminal fence و آزادسازی معتبر | ReportExhaustedLeaseReleasesOnlyAfterTerminalFenceAndStaleWorkerCannotConsume |
| حفظ legacy و FK/rollback | MigrationPreservesIndefiniteLegacyContractsAndLedgerAndRejectsDestructiveRollback |
| Legacy ناقص، گزارش متمایز و نبود سهمیهٔ تازه | IncompleteLegacyContractPreservesCompatibilityWithoutNewActiveContractOrAiQuota |
| گذار Admin با حفظ تاریخ و مصرف | OnlyValidAdminContractCompletesLegacyMigrationWithoutRewritingHistoricalDatesOrUsage |
| سازمان بدون قرارداد، بدون ساخت رکورد یا Lock حدسی | OrganizationWithoutSubscriptionAwaitsExplicitContractWithoutSyntheticSubscriptionOrWriteLock |
| زمان واقعی کنترل و Limit صریح | LegacyMigrationStatusDoesNotInventDatesAndManagedEnforcementStartsWhenRegistered و QuotaJsonRequiresExplicitLimitAndDistinguishesZeroFromUnlimited |

تست‌های HTTP با TestServer و SQL Server واقعیِ LocalDB اجرا می‌شوند؛ این‌ها پذیرش خارجی روی سرور یا AI Runtime نیستند. شمار درخواست HTTP از خروجی SUBSCRIPTION_HTTP_REQUESTS تست‌ها استخراج می‌شود.

## Validation

- Restore و Release Build آخرین کد: PASS، صفر Warning و صفر Error.
- تست‌های متمرکز Unit مربوط به Commerce: ۳۰ PASS.
- تست‌های متمرکز Integration مربوط به Commerce/Payment/Export: ۷۲ PASS.
- ورودی decimal.MaxValue با 400 / COMMERCE_USAGE_INVALID رد می‌شود و حذف Limit از JSON خطای ۴۰۰ می‌دهد؛ صفر و null صریح جدا هستند.
- اجرای کامل پس از اصلاح Legacy و تکرار نهایی Build/Test هر دو موفق‌اند؛ Failed/Skipped/Warning/Error صفر هستند.
- pending model changes: NONE؛ Migrationهای معوق در دیتابیس ایزوله پس از اجرای زنجیره صفر بودند. دیتابیس‌های عادی این مرحله بررسی/تغییر داده نشده‌اند.

| اجرای نهایی Release پس از اصلاح Legacy | اجراشده | موفق | ناموفق | Skipped |
|---|---:|---:|---:|---:|
| Unit | 909 | 909 | 0 | 0 |
| Integration | 1451 | 1451 | 0 | 0 |
| Architecture | 14 | 14 | 0 | 0 |
| مجموع | 2374 | 2374 | 0 | 0 |

هر ۱۶ تست Unit جدید و ۱۹ Integration جدید در اجرای کامل نهایی پس از اصلاح Legacy PASS هستند. ۱۸ سناریوی HTTP جدید جمعاً ۱۸۱ درخواست ثبت کردند؛ سه سناریوی Legacy از این تعداد، ۲۴ درخواست‌اند. این تعداد فقط درخواست‌های سناریوهای تازهٔ همین قابلیت است؛ اجرای خارجی، AI واقعی یا کل درخواست‌های سایر تست‌های مخزن را بیان نمی‌کند.

فرمان‌های چرخهٔ اعتبارسنجی:

```powershell
dotnet restore MakanApp.sln --nologo
dotnet build MakanApp.sln --configuration Release --no-restore --nologo
dotnet test MakanApp.sln --configuration Release --no-build --no-restore --nologo --logger trx --results-directory .artifacts/subscription-legacy-20261029-a7e69844/regression
dotnet build MakanApp.sln --configuration Release --no-restore --nologo
dotnet test MakanApp.sln --configuration Release --no-build --no-restore --nologo --logger trx --results-directory .artifacts/subscription-legacy-20261029-a7e69844/final
```

گزارش امن نهایی در .artifacts/subscription-legacy-20261029-a7e69844/validation-final.json و نتیجهٔ اجرای قبل از آن در regression-summary.json ذخیره شدند. SHA-256 هر ۹۴ فایل کد مرتبط با نسخهٔ Buildشده تطبیق دارد؛ کد در حین اجرای نهایی تغییر نکرده است. شواهد .artifacts محلی و Git-ignored هستند.

در پیاده‌سازی پایهٔ پیشین، اجرای کامل اولیه یک خطای tracking رکورد تازهٔ quota در Draft پیدا کرد؛ آن اصلاح و دوباره تأیید شد. نتیجهٔ شکست اولیه به‌عنوان موفقیت شمرده نمی‌شود.

## Git و حفظ کار قبلی

این بخش سابقهٔ پایان پیاده‌سازی پیش از نهایی‌سازی است: در آن مرحله Commit/Push/Publish/Deploy انجام نشد و HEAD تغییر نکرد. درخواست نهایی‌سازی، یک Commit محلی و دو Publish را مجاز کرده است؛ Source Commit و نتیجهٔ واقعی از گزارش Release خوانده شوند. Push و Deploy مجاز نشده‌اند.

بازبینی Git: شاخهٔ main در bf9a819ffd2ce96ea14360f0eba0235c760e72b4؛ ۴۹ فایل tracked مرتبط تغییر کرده و ۴۷ فایل مرتبط جدیدند؛ فایل حذف‌شده یا staged نداریم. git diff --check موفق است. README فقط برای معرفی همین قابلیت، تصمیم Legacy و تفکیک آمار پیش از آن اصلاح شد.

فایل‌های AI قبلی که باید بدون تغییر باقی بمانند:

- tools/ai/provider_diagnostic.py
- tools/ai/run_exact_ai.py
- tools/ai/test_provider_diagnostic.py

تنظیمات MeliPayamak، appsettings خصوصی، User Secrets و مسیرهای AI تغییر داده نشده‌اند. بررسی نهایی ۱۲۷۸ فایل خط مبنا، تغییر همان ۴۹ فایل مرتبط و نبود فایل قبلیِ مفقود را تأیید کرد؛ هر ۳۲ فایل کنترل‌شدهٔ AI/SMS/appsettings/وابستگی‌ها بدون تغییرند. ۱۶ فایل پشتیبان خصوصی نیز با hash اصلی تطبیق دارند. نتیجه در preservation-final.json و فهرست diff در شواهد خصوصی مرحله نگه‌داری می‌شود. هیچ فایل Frontend دست‌کاری نشده است.

نتیجهٔ تصمیم Legacy در سناریوهای آزموده‌شده:

```text
NO_GUESSED_COMMERCIAL_DATA=true
LEGACY_ORGANIZATIONS_NOT_BROKEN=true
COMMERCIAL_ENFORCEMENT_AFTER_VALID_CONTRACT=true
```

## محدودیت‌ها و پیش‌شرط‌های فروش

- Assignment/Exam/Feedback و فایل آموزشی PersonalClass هنوز workflow مستقل ندارند؛ این محدودیت مصوب این مرحله است.
- lifecycle منابع شخصی و meter فایل مستقل، مرحلهٔ بعد هستند.
- AI Runtime فعال یا به اجرای Production متصل نشده است.
- adapter واقعی پرداخت، refund خودکار و موتور تخفیف اضافه نشده‌اند.
- نام، قیمت و سقف‌های پولی، شرایط بازپرداخت/تخفیف، سیاست نگهداری و شرایط قابل ارائهٔ فروش باید پیش از عرضه نهایی شوند.
- پیشنهادهای اجرایی مانند هشدار و بازهٔ Quote قابل تنظیم‌اند؛ تقویم و مرجع شرایط باید صریح در نسخه باشند.
- پرداخت دارای شرط منقضی/متعارض یا هم‌پوشانی دوره، حق مضاعف نمی‌سازد؛ بررسی عملیاتی لازم دارد و refund خودکار وعده داده نمی‌شود.
- هیچ استقرار یا Migration روی دیتابیس‌های واقعی این مرحله انجام نشده است.

## نکات آموزشی و مسیر مطالعه

۱. نقش و پرداخت دو پرسش متفاوت‌اند؛ هر دو باید در جای خود بررسی شوند.
۲. تاریخچهٔ مالی با نسخهٔ immutable و سند Amendment حفظ می‌شود، نه با حذف مصرف هنگام تعویض پلن.
۳. SUM صحیح بدون قفل مشترک، تضمین آخرین واحد نیست.
۴. Unknown را نباید با failure یکی گرفت؛ fencing و reconciliation از مصرف یا تحویل دوگانه جلوگیری می‌کنند.
۵. شناسهٔ تکرار باید به Actor/Owner/Context و hash متصل باشد.
۶. تاریخ UTC ذخیره می‌شود، اما مرز تجاری روز/ماه با زمان و تقویم قرارداد محاسبه می‌شود.
۷. Anonymous بودن selector یا دانستن UserId هیچ‌کدام مجوز مالی یا آموزشی ایجاد نمی‌کنند.

ترتیب مطالعه: CommercialSubscriptionsController و PersonalClassesController -> CommercialSubscriptionService / CommercialActorAccess / PersonalClassService -> PlanVersion و Subscription و UsageReservation -> ICommercialStore -> EfCommercialStore و EfPersonalClassStore -> OrderPaymentService / activation handler -> ReportExportService و EfReportExportJobStore -> Configurationها و دو Migration -> تست‌های یادشده.

مرحلهٔ بعد: چرخهٔ آموزشی واقعی PersonalClass و اتصال فایل/ظرفیت میزبان، سپس نهایی‌کردن مقادیر و شرایط فروش. فعال‌سازی AI، درگاه واقعی و استقرار باید مجوز و پذیرش جدا داشته باشند.
