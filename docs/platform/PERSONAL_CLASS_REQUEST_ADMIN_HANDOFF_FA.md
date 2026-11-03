# قرارداد مدیریت درخواست تشکیل کلاس مستقل

صف بررسی از IAM موجود Platform استفاده می‌کند. دو permission واقعی `platform.personal_classes.read` و `platform.personal_classes.review` اضافه شده‌اند. Seed نقش PlatformOwner این مجوزها را دارد؛ سایر نقش‌ها نیازمند تخصیص صریح در IAM هستند. School Manager و Teacher مجوز Platform ضمنی ندارند. هیچ میان‌بر بر اساس نام Role ساخته نشده است.

پیشوند `/api/admin/v1/personal-class-requests`:

| Method | مسیر | مجوز و پاسخ |
|---|---|---|
| GET | ریشه | read؛ فیلتر status و page/pageSize؛ Draft خصوصی متقاضی وارد صف نیست |
| GET | `/{id}` | read؛ جزئیات، پیشنهادهای مدیر/مدرس، تصمیم نهایی و Progress |
| GET | `/{id}/history` | read؛ Audit تصمیم‌ها با actor/time/reason |
| POST | `/{id}/review` | review؛ نسخه، Decision، Reason و FinalManagerUserId اختیاری فقط برای Approved |

Decision مجاز `UnderReview/NeedsChanges/Rejected/Approved` است. NeedsChanges به متقاضی اجازهٔ ویرایش و resubmit می‌دهد. Approved/Rejected تصمیم نهایی آن درخواست هستند؛ retry همان تصمیم و payload اثر تازه ندارد و تصمیم متفاوت با نسخه/وضعیت نامعتبر رد می‌شود. ثبت دلیل لازم است. نسخهٔ stale باید با بازخوانی پاسخ جاری رفع شود.

تأیید، قرارداد Hosting موجود متقاضی را بررسی می‌کند؛ قیمت/پرداخت‌کنندهٔ جدید حدس زده نمی‌شود. یک کلاس و intent آماده‌سازی دو فضای مستقل در همان تراکنش ثبت می‌شود. SuggestedTeacher صرفاً پیشنهاد باقی می‌ماند؛ دعوت صریح مدرس بعداً توسط مدیر کلاس انجام می‌شود.

اگر FinalManagerUserId نفر دیگری باشد، او ابتدا باید انتقال مسئولیت اولیه را در endpointهای شخصی `manager-transfers` بپذیرد. Platform نمی‌تواند رضایت او را جعل کند. تا آن زمان `Progress.ManagerAccepted=false` است و فعال‌سازی ممکن نیست. رد مسئولیت، انتصاب اجباری یا پایان اضطراری کلاس ایجاد نمی‌کند؛ این مسیرهای مداخله خارج از تصمیم فعلی‌اند.

Progress حالت هر مقصد و کلاس را نشان می‌دهد. ProvisioningFailed با Rejected فرق دارد: تصمیم محصولی تأیید شده، آماده‌سازی فنی نیازمند retry/repair است. GET هیچ ترمیمی انجام نمی‌دهد. مدیر پذیرفته‌شده از `POST /api/v1/personal-classes/{id}/spaces/repair` استفاده می‌کند؛ ابزار maintenance موجود نیز dry-run و apply کنترل‌شده دارد.

Audit موجود `AdminAuditLog` برای تصمیم، انتقال، نقش، پایان عضویت، Archive و Repair reuse شده است. محتوای خصوصی چت، شماره یا token در evidence قرار نمی‌گیرد. مشاهدهٔ این صف مجوز browse چت خصوصی یا Guardian access ایجاد نمی‌کند.

تست‌های مرز شامل درخواست حساب عادی/نقش‌های مدرسه، رد SchoolManager بدون permission، NeedsChanges/Resubmit، Reject بدون کلاس، تأیید هم‌زمان و پذیرش صریح مدیر منتخب هستند. سن در تشکیل/بررسی/عضویت دخیل نیست.
