# وضعیت جاری پروژه و مسیر مطالعه اسناد

بازبینی `2026-10-29` از `main@701cba0`؛ **332 عملیات HTTP در 59 Controller و 291 مسیر؛ 570 schema، 107 جدول و 52 migration**. آخرین migration `20261028202239_CompleteSchoolManagement` است. کاتالوگ از source commit‌شده و بدون SQL/worker/provider واقعی استخراج شده؛ وضعیت سرور از این شمارش معلوم نمی‌شود.

## تغییرات کد و بیزینس

| حوزه | وضعیت واقعی |
|---|---|
| Platform | MFA/step-up، endpoint و key ring اختصاصی حذف؛ OTP/session/role/permission و reason/audit فعال‌اند؛ [تصمیم](platform/PLATFORM_AUTHENTICATION_CHANGE_FA.md) |
| پروفایل | DOB nullable date-only self؛ omission/null حفظ مقدار، آینده رد؛ شاهد سن مستقل؛ [گزارش](product/PROFILE_AND_PERSONAL_MESSAGING_IMPLEMENTATION_FA.md) |
| عکس | مرجع اختیاری User به FileAsset شخصی Ready JPEG/PNG؛ PUT/DELETE self و دانلود Bearer تابع هویت مجاز؛ [قرارداد](product/PROFILE_PHOTO_IMPLEMENTATION_FA.md) |
| Personal Messaging | بدون School Role با gateهای سن/رابطه/privacy؛ Profile ناقص hint خواندن دارد؛ پایان نقش داده شخصی را حذف نمی‌کند |
| مدرسه | Directory، roster کلاس، دعوت والد با consent، Lesson/attachment و انتشار؛ [قرارداد فرانت](product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md) |
| CORS | AllowAnyOrigin مستقل از محیط؛ پایه/نمونه‌ها true، mode=false allowlist معتبر لازم دارد؛ بدون credentials؛ [قرارداد](production/CORS_CONFIGURATION_FA.md) |
| عملیات | claim/fencing/repair و cleanup محدود opt-in، shutdown/metrics؛ probe صرفاً MFA حذف شده؛ [runbook](operations/PRODUCTION_CONFIGURATION_RUNBOOK.md) |

## آنچه هنوز پیاده‌شده فرض نمی‌شود

مدل محصول V2 برای Learning Profile، Badge، اشتراک/خرید فردی، accounting تعویض پلن، quotaهای AI/Source و Educational Source/RAG lifecycle پیشنهاد است. DOB، عکس و مدیریت مدرسه مصوب و موجودند؛ مجوز آن‌ها به دیگر پیشنهادها تعمیم ندارد. [ماتریس جاری/پیشنهادی](product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md).

| gate | وضعیت |
|---|---|
| Push دستگاه واقعی | P1_PUSH_RUNTIME_PENDING |
| state/backplane چند instance | PENDING_SCALE_INFRA |
| P3/LiveKit/TURN | P3_S_INFRA_REQUIRED؛ rejoin NOT_TESTED و Production approval NOT_GRANTED |
| AI واقعی | Service Auth قبلی PASS؛ Parent Provider Timeout و Full-12 Deferred |
| RAG، پرداخت واقعی، SMS عمومی اعلان | lifecycle یا provider مربوط موجود/تأییدشده فرض نشده؛ OTP ملی‌پیامک جداست |
| TLS، recycle/reboot، backup/restore/load و alert | پذیرش محیط هدف مستقل از regression محلی لازم دارد |

## دیتابیس و مرز استقرار

چهار migration پس از Checklist: RemovePlatformMfa، AddSelfDeclaredProfileDateOfBirth، AddUserProfilePhoto و CompleteSchoolManagement. دو جدول MFA حذف و دو جدول Lesson اضافه شده‌اند؛ جمع۱۰۷ با بازبینی قدیمی۱۰۷ برابر است، اما schema یکسان نیست. RemovePlatformMfa داده را حذف می‌کند و Down بازیابی داده نیست. startup migration/EnsureCreated نداریم و این مرحله روی DB عادی یا سرور migration/seed اجرا نمی‌کند.

## مسیر مطالعه

[راهنمای بک‌اند](backend-api-guide.md) → [مرجع API](backend-api-reference.md) → [قرارداد فرانت](frontend-contract/README.md) → اسناد تخصصی Profile/Photo/School بالا → [فرهنگ SQL](database-current-structure.md) → [استقرار](deployment-windows-linux-docker.md) و [Production](production/PRODUCTION_READINESS.md).

[inventory همه اسناد](documentation-inventory.csv) قرارداد جاری و شاهد تاریخی را جدا می‌کند؛ نتایج تست/commit گزارش‌های قدیمی به‌عنوان نتیجهٔ امروز تکرار نشده‌اند. [گزارش تحویل و فهرست فایل‌های اصلاح‌شده](DOCUMENTATION_UPDATE_REPORT.md).

## Validation بازبینی جاری

Restore موفق؛ Build اولیه و نهایی هر دو صفر warning/error. هر دو suite کامل **۲۳۳۹ تست موفق** داشتند: ۸۹۳ Unit،۱۴۳۲ Integration و۱۴ Architecture؛ failed/skipped صفر. شش TRX دو دور کنترل شدند. دوازده تست ابزار مستندسازی هم موفق‌اند؛ تست محصول تازه‌ای در این کار اسناد اضافه نشده است.

اجرا روی checkout مستقل و LocalDB تست با نام یکتا بود؛ تغییر موقت نام fixture بعد از اجرا به bytes اصلی بازگشت و وارد commit نیست. EF pending-model ندارد؛ migration روی DB عادی یا سرور اعمال نشده است.

کنترل۳۳۲ عملیات/۵۷۰ schema/۱۰۷ جدول/۵۲ migration و۱۴۴ ورودی inventory موفق؛ لینک شکسته و اختلاف Enum/OpenAPI صفر. PDFهای Guide/Reference/Frontend/Database به‌ترتیب۷۴/۴۲۱/۴۲۸/۱۵۹ صفحه دارند؛ bookmark و hash HTML متناظر کنترل شده و صفحه خالی/متن خارج صفحه صفر است. صفحات جدید DOB/Photo/Lesson/SQL بصری بازبینی شدند؛ gateهای runtime از این نتایج محلی بسته اعلام نشده‌اند.

سه فایل Python محلی ابزار AI حفظ و از scope تغییرات کنار گذاشته شده‌اند؛ تنظیم خصوصی یا فایل Frontend تغییر نمی‌کند. چهار commit قبلی main هنوز روی upstream منتشر نشده‌اند؛ Push این تحویل بدون تعیین تکلیف آن‌ها مجوز انتشار خودکار کار قبلی کاربر نیست.
