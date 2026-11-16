# ماتریس محصول و مالکیت Commerce — BE-17

بازبینی `2026-11-16` از مبنای `f030ab7`؛ اعداد قیمت/سقف فروش در این سند تعیین نمی‌شوند.
Source اعتبارسنجی: `3561610336e88b225da5463c959a640d4bcf9ae5`؛ پذیرش کد طبق سیاست جاری است.
حق‌ها و سقف‌ها از Published PlanVersion معتبر می‌آیند؛ نام Plan جای policy نیست.

| خانواده | Audience | Owner | Payer/مرجع هزینه | Entitlements | منبع Quota | مسیر خرید/پرداخت | تصمیم یا تنظیم باز |
|---|---|---|---|---|---|---|---|
| اشتراک Individual | Individual | UserId همان حساب | همان حساب | نسخهٔ Published | Period/Reservation یا ظرفیت همان User | personal/quote → personal/orders → payments → Verify → Fulfillment | دادهٔ مصوب Admin؛ D05 حق مشترک ایجاد نمی‌کند |
| اشتراک Organization | Organization | OrganizationId معتبر | قرارداد سازمان؛ Actor خرید Manager مجاز | نسخهٔ سازمانی | ظرفیت سازمان یا Period قرارداد | organization/quote → commerce/orders → payments → Verify → Fulfillment | Legacy فقط پس از قرارداد معتبر مصوب تحت کنترل جدید می‌رود |
| تأسیس آموزشگاه | Organization برای هر دو نوع | Organization خروجی Provisioning | متقاضی مجاز، قیمت سروری | نسخهٔ اختصاصی خدمت | قرارداد سازمان ایجادشده | school-applications/{id}/quote و orders → Commerce Payment → Provisioning | Production mapping/terms بسته؛ No Seed |
| میزبانی PersonalClass | Individual | HostUserId | Individual میزبان؛ مدیر محلی لزوماً مالک مالی نیست | PersonalClassHosting | کلاس/فراگیر distinct/فایل scope میزبان | همان اشتراک شخصی؛ IPG موازی ندارد | Gateهای Academic false؛ D07 Deferred مصوب |
| AI Individual | Individual | مصرف‌کننده | Individual همان User | Ai + quota صریح | رزرو/ledger دورهٔ حساب | اشتراک شخصی + Preflight/Reservation | Runtime/Deploy جدا از اثبات محلی |
| AI Organization | Organization | سازمان Context معتبر | Organization؛ انتخاب Individual همان Actor فقط صریح | Ai + مجوز مستقل داده | رزرو/ledger همان قرارداد | همان اشتراک/پرداخت سازمانی؛ بدون fallback پنهان | Subscription جای Workspace/Role/Resource ACL نیست |
| FreeTrial | Individual؛ بدون PlanVersion/Subscription پولی | همان حساب مصرف‌کننده | Trial صریح همان User | سیاست موجود simple text، نه مجوز آموزشی | رزرو/ledger روزانهٔ User با مرز تهران | Order/Payment ندارد | Family/device/role سهمیهٔ جدا نمی‌سازند |

اگر در AI سازمانی Payer صریح Individual باشد، مالک مالی همان User و مجوز سازمانی همچنان لازم است؛
این انتقال مالکیت یا اشتراک سهمیه نیست. Report Export چنین fallbackی ندارد.

## نگاشت حق و حداقل Quota فعلی انتشار

| Entitlement | Quota لازم فعلی | مرز |
|---|---|---|
| Academic | ActiveClasses و ActiveLearners | حق تجاری؛ permission آموزشی مستقل لازم است |
| Assignments | قاعدهٔ تازه‌ای بر validation فعلی افزوده نشده | AssignmentFileBytes در صورت تعریف، ظرفیت تکلیف/Submission را گزارش می‌کند |
| Exams | قاعدهٔ تازه‌ای بر validation فعلی افزوده نشده | Gate و permission مستقل محفوظ |
| Messaging | Quota تازه حدس زده نشده | انقضای تجاری، Messaging شخصی را خودکار نمی‌بندد |
| Reports | ReportExports | رزرو/مصرف سازمانی، بدون fallback شخصی/Trial |
| Storage | StorageBytes | یک فایل در چند reference دو بار شمرده نمی‌شود |
| Ai | AiTextRequests | مصرف دوره‌ای؛ Unknown ظرفیت را نگه می‌دارد |
| PersonalClassHosting | ActiveClasses، ActiveLearners، StorageBytes، AssignmentFileBytes | فقط Individual؛ ظرفیت کلاس برای Host |
| PersonalSources | SourceCount و StorageBytes | فقط Individual صاحب منبع؛ Library را فعال نمی‌کند |

Organization plan با PersonalClassHosting/PersonalSources فعال validation انتشار را رد می‌کند.
Quota حذف‌شده با Limit=null متفاوت است؛ اولی configuration ناقص، دومی Unlimited صریح است.
صفر به معنی صفر است؛ entitlement خاموش با quota موجود حق مصرف ایجاد نمی‌کند.

## مرجع ظرفیت و مصرف

کلاس/فراگیر از دادهٔ authoritative شمارش می‌شوند؛ membership بدون Learner صندلی نمی‌خورد.
فایل از FileAsset.Ready متعلق به scope شمرده می‌شود؛ اتصال تکلیف/Submission با EXISTS بررسی می‌شود،
پس تعدد نسخه/پاسخ bytes را تکثیر نمی‌کند. در Individual فقط فایل کلاس‌های Host و اتصال‌های
منابع Personal همان User مرجع‌اند؛ uploader فایل کلاس به‌تنهایی payer نیست.
Ready/Retained تا تغییر معتبر lifecycle شمرده می‌شود؛ Archive یا پایان رابطه مجوز purge نیست.
عدد ledger تاریخی CurrentCapacity دوباره به ظرفیت زنده جمع نمی‌شود.

PeriodicConsumption از Subscription/Period، ledger مصرف و Reserved/Unknown خوانده می‌شود.
AI/Export شبکه‌ای داخل transaction نیست؛ intent متفاوت conflict و Unknown بدون قطعیت آزاد نمی‌شود.
نتیجهٔ دیررس روی همان reservation و دورهٔ اولیه نهایی می‌شود.

## Availability و تغییر قرارداد

Hidden از Catalog حذف است؛ disabled new purchase خرید تازه را می‌بندد؛ disabled renewal
تمدید مقصد را حتی با تغییر Plan مسدود می‌کند. Published Version درجا ویرایش نمی‌شود.
قیمت، واحد، نسخه و دورهٔ Order سروری و frozen هستند؛ تغییر Mapping آموزشگاه آن‌ها را بازنویسی نمی‌کند.
Renewal دستی است؛ Upgrade از الگوریتم دقیق فعلیِ prorated price/quota استفاده می‌کند؛
Downgrade در دورهٔ بعد اجرا و مازاد ظرفیت گزارش می‌شود، نه حذف.

## مرز سیاست و محیط

FAMILY_SHARED_SUBSCRIPTION=PRODUCT_DECISION_REQUIRED و FAMILY_QUOTA_POOLING=PRODUCT_DECISION_REQUIRED؛
اشتراک/تجمیع خانوادگی عمداً پیاده نشده است. دو GuardianRelation برای یک والد،
subscription/usage/receipt فرزندان را merge یا قابل‌خواندن نمی‌کند.

PRODUCTION_SCHOOL_COMMERCIAL_CONFIGURATION=BLOCKED_CONFIGURATION؛ دادهٔ مصوب واقعی لازم است.
Catalog عملیاتی Production در این Task خوانده/Seed نشده؛ نبود دادهٔ محیط، code failure نیست.
CafeBazaar/Myket همچنان BLOCKED و iOS نیازمند تصمیم Product است؛ BE-18 اجرا نشده است.
Library عمومی، ReviewRequired، Gateهای BE-25، AI Service Auth و تنظیم‌های خصوصی تغییر نکردند.
شمار تصمیم‌های باز همان۱۲ و BE24_FULL_ACCEPTANCE=FAIL است.

[قرارداد Frontend/Admin](COMMERCE_CURRENT_POLICY_FRONTEND_HANDOFF_FA.md) و
[گزارش تست و طراحی](COMMERCE_BE17_DEVELOPER_REPORT_FA.md) مرجع جزئیات‌اند.
