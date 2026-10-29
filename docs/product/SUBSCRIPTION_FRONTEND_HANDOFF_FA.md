# قرارداد Frontend بنیاد اشتراک و سهمیه

این سند از Controllerها و DTOهای فعلی استخراج شده است. مسیر پایه `/api/v1` است و همهٔ عملیات تجاری نشست معتبر می‌خواهند. خرید، نقش آموزشی یا دسترسی به دادهٔ سازمان ایجاد نمی‌کند.

## مسیرهای شخصی و سازمانی

| Method | Route | قرارداد |
|---|---|---|
| GET | /api/v1/commerce/catalog?audience=Individual | پلن‌های کامل، نمایان و مجاز به خرید تازه |
| GET | /api/v1/commerce/catalog?audience=Organization | کاتالوگ سازمانی؛ مالک خریدار با مالک قرارداد یکی فرض نشود |
| GET | /api/v1/commerce/personal/subscription | قرارداد، دوره، Entitlements و Quotas خود کاربر |
| GET | /api/v1/commerce/organization/subscription | فقط سازمان جاری و مجاز Manager |
| POST | /api/v1/commerce/personal/quote | PlanVersionId، Purpose، SubscriptionId در صورت نیاز؛ قیمت و اعتبار Quote سروری |
| POST | /api/v1/commerce/organization/quote | همان قرارداد برای مالک سازمانی |
| POST / GET | /api/v1/commerce/personal/orders | ساخت با ClientOperationId یا فهرست سفارش‌های خود کاربر |
| GET | /api/v1/commerce/personal/orders/{orderId} | سفارش مجاز؛ سفارش کاربر دیگر پنهان می‌ماند |
| POST | /api/v1/commerce/personal/orders/{orderId}/payments | initiation موجود؛ درگاه واقعی این Release unavailable است |
| POST | /api/v1/commerce/personal/subscription/downgrade | PlanVersionId، ExpectedVersion و Reason؛ اثر در دورهٔ بعد |
| POST | /api/v1/commerce/organization/subscription/downgrade | همان عملیات با مجوز Manager |
| POST | /api/v1/commerce/ai/simple-text/preflight | ContextType، ContextId و Payer؛ فقط بررسی، بدون AI یا برداشت |

سفارش و پرداخت سازمانی موجود در `/api/v1/commerce/orders` حفظ شده است. ساخت Order تازه 201 و replay همان درخواست 200 است. Quote پیش‌نمایش است و حق ایجاد نمی‌کند؛ Frontend مبلغ نهایی را تعیین نمی‌کند. تمدید دستی و AutoRenew=false است؛ نمایش قیمت تومان است.

## وضعیت و سهمیه

Stateهای Summary جدید: NoSubscription، PendingPayment، Active، QuotaWarning، QuotaExceeded، GracePeriod، Expired، ScheduledDowngrade و LegacyPendingContract.

MigrationStatus سازمان بدون قرارداد معتبر Admin برابر AwaitingValidContract است. LegacyPendingContract قفل تازهٔ آموزشی نیست. HasVerifiedCommercialContract=false و نبود قیمت/دوره/سهمیه نباید Unlimited یا پلن رایگان تلقی شود. پس از ثبت معتبر Admin، MigrationStatus=Completed و CommercialEnforcementFromUtc زمان واقعی کنترل را نشان می‌دهد. Individual مقدار NotApplicable دارد.

برای هر Quota، Key/Kind/Unit/Limit/Consumed/Reserved/Remaining را بخوانید. CurrentCapacity با ماه جدید صفر نمی‌شود. Remaining=null تنها با IsUnlimited و IsUsageAvailable تفسیر شود؛ meter ناموجود، مصرف صفر یا موجودی نامحدودِ قطعی نیست. هشدارها از تنظیم نسخه می‌آیند.

Trial فقط بنیاد سه عملیات موفق متن ساده در روز/Account با مرز تهران است؛ AI Runtime خاموش است. Device، نقش و سازمان Trial تازه نمی‌سازند. retry یک OperationId مصرف دوباره ندارد. در Organization، منبع پیش‌فرض Organization است؛ انتخاب Individual صریح است و دادهٔ آموزشی را مجاز نمی‌کند. pooling و fallback خودکار نداریم.

## PersonalClass

`POST/GET /api/v1/personal-classes`، `GET /{classId}`، `POST /{classId}/archive`، `POST /{classId}/invitations`، `GET /invitations`، `POST /invitations/{invitationId}/accept`، `GET /{classId}/participants` و `DELETE /{classId}/participants/{participantId}` زیر همین پایه هستند.

ساخت با Title و ClientOperationId و دعوت با DestinationUserId و ClientOperationId انجام می‌شود. میزبان User است؛ Organization صوری یا نقش سازمانی ساخته نمی‌شود. ظرفیت از میزبان و AI دانشجو از خود دانشجو است. دانشجو برای عضویت پایه خرید لازم ندارد و امور مالی میزبان را نمی‌بیند. CanCreateEducationalContent=false و چرخهٔ مستقل Assignment/Exam هنوز خارج از Release است.

## رفتارهای مهم و خطا

Upgrade پس از پرداخت معتبر، سررسید را ثابت نگه می‌دارد؛ افزایش مصرف دوره‌ای prorated و ظرفیت جاری کامل است. Downgrade دورهٔ بعد و دادهٔ مازاد محفوظ است. Grace سازمان هفت روز است؛ سپس نوشتن آموزشی قفل می‌شود، اما خواندن مجاز، پیام‌رسان شخصی، پرداخت و کاهش دسترسی حفظ می‌شوند. برای Individual مهلت سازمان فرض نشود.

AUTH_REQUIRED=401؛ COMMERCE_CONTEXT_NOT_ALLOWED و FEATURE_NOT_ENTITLED=403؛ QUOTA_EXCEEDED و ORGANIZATION_COMMERCIALLY_LOCKED و COMMERCE_USAGE_CONFLICT=409؛ COMMERCE_RESOURCE_NOT_FOUND=404؛ PLAN_TERMS_INCOMPLETE=422؛ COMMERCE_METER_UNAVAILABLE=503. نبود مجوز را CTA خرید تفسیر نکنید. ProblemDetails و correlation/trace موجود استفاده شوند.

Export موجود در `POST /api/v1/reports/exports`، ClientOperationId اختیاری دارد؛ replay باید همان payload/context را نگه دارد. وضعیت Unknown، مصرف یا شکست قطعی نیست. هیچ endpoint عمومی برای اعلام موفقیت مصرف توسط مشتری وجود ندارد.
