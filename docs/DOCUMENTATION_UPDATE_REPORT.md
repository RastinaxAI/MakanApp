# گزارش به‌روزرسانی اسناد پروژه

تاریخ `2026-10-29`؛ کد مبنا `main@701cba057b358d9cf6d15d681389d0f8d22ac6d6`. مبنای اسناد قبلی شامل حذف MFA در۲۰۲۶-۱۰-۲۴ بود؛ پروفایل، عکس و مدرسهٔ ثبت‌شده پس از آن اکنون در کتاب‌ها همگام شده‌اند. این فایل گزارش جاری است و نسخهٔ قبلی آن در Git باقی است.

## ۱. خلاصه مرحله

کاتالوگ از۳۱۸ به۳۳۲ عملیات،۵۶ به۵۹ Controller،۲۸۳ به۲۹۱ مسیر و۵۴۷ به۵۷۰ schema رسیده است. تعداد جدول‌ها از۱۰۵ به۱۰۷ و migrationها از۴۹ به۵۲ رسیده؛ Source/SDK/architecture ثابت‌اند. اسناد اکنون رفتار DOB، Photo، Personal Messaging، مدیریت مدرسه و CORS را توضیح می‌دهند؛ پیشنهادهای محصول به‌عنوان قابلیت اجرایی منتشر نشده‌اند.

## ۲. معماری

محصول تغییری در این مرحله ندارد. ApiExplorer/OpenAPI از Api و مدل طراحی EF از Infrastructure برای استخراج قرارداد خوانده شده‌اند؛ Application/Domain مصدر قواعد DOB، Lesson، consent و مالکیت‌اند. راهنما، قالب سند و نمونه استقرار با این قواعد تطبیق داده شده‌اند. .NET9، EF9، SQLServer و Onion/Modular Monolith ثابت‌اند.

## ۳. فایل‌های ایجادشده

فایل source جدیدی در این تحویل ایجاد نشده؛ اسناد موجود به‌روز شده‌اند. snapshot، log و تصاویر بازبینی زیر .artifacts محلی و ignored هستند. خروجی HTML/PDF دیتابیس نیز طبق gitignore موجود محلی است.

## ۴. فایل‌های اصلاح‌شده

30 فایل در scope commit، شامل اسناد و خروجی‌های زیر و یک ابزار تولید سند؛ افزون بر آن دو خروجی دیتابیس ignored بازتولید شده‌اند:

| مسیر | تغییر و اثر |
|---|---|
| [README.md](../README.md) | آمار و مسیر مطالعه جاری؛ Profile/School و مرز پیشنهادهای V2 |
| [docs/CURRENT_PROJECT_STATUS.md](CURRENT_PROJECT_STATUS.md) | شاخص نسخه، امکانات واقعاً موجود، migrationها، gateها و validation جدید |
| [docs/DOCUMENTATION_UPDATE_REPORT.md](DOCUMENTATION_UPDATE_REPORT.md) | گزارش جاری همین همگام‌سازی و فهرست فایل‌ها؛ منبع نسخه قبلی در Git حفظ است |
| [docs/backend-api-guide.html](backend-api-guide.html) | بازتولید خروجی از Markdown جاری؛ bookmark/link و تطبیق هش HTML/PDF |
| [docs/backend-api-guide.md](backend-api-guide.md) | DOB/Photo، استقلال حساب، Directory/roster، رضایت والد و Lesson؛ اصلاح ادعاهای منسوخ |
| [docs/backend-api-guide.pdf](backend-api-guide.pdf) | بازتولید خروجی از Markdown جاری؛ bookmark/link و تطبیق هش HTML/PDF |
| [docs/backend-api-reference.html](backend-api-reference.html) | بازتولید خروجی از Markdown جاری؛ bookmark/link و تطبیق هش HTML/PDF |
| [docs/backend-api-reference.md](backend-api-reference.md) | تمام۳۳۲ عملیات و۵۷۰ schema فعلی با فیلدها و metadata واقعی |
| [docs/backend-api-reference.pdf](backend-api-reference.pdf) | بازتولید خروجی از Markdown جاری؛ bookmark/link و تطبیق هش HTML/PDF |
| [docs/database-current-structure.md](database-current-structure.md) | ۱۰۷ جدول/۵۲ migration؛ حذف MFA و schema افزوده Profile/School |
| [docs/deployment-windows-linux-docker.md](deployment-windows-linux-docker.md) | حذف الزام key ring و secret/volume منسوخ از نمونه‌ها؛ CORS دو mode و rollout schema |
| [docs/dev/DEVELOPMENT_TEST_USERS_FA.md](dev/DEVELOPMENT_TEST_USERS_FA.md) | ورود Backoffice با permission جاری بدون MFA؛ حفظ fixture تاریخی |
| [docs/dev/SERVER_MOCK_OTP_FA.md](dev/SERVER_MOCK_OTP_FA.md) | حذف نیاز key ring/DPAPI/PFX و به‌روزرسانی CORS و نسخه migration در Test |
| [docs/documentation-inventory.csv](documentation-inventory.csv) | ۱۴۴ ورودی و تشخیص سند جاری/موضوعی/تاریخی |
| [docs/frontend-contract/MakanApp-Frontend-Backend-Contract.html](frontend-contract/MakanApp-Frontend-Backend-Contract.html) | بازتولید خروجی از Markdown جاری؛ bookmark/link و تطبیق هش HTML/PDF |
| [docs/frontend-contract/MakanApp-Frontend-Backend-Contract.pdf](frontend-contract/MakanApp-Frontend-Backend-Contract.pdf) | بازتولید خروجی از Markdown جاری؛ bookmark/link و تطبیق هش HTML/PDF |
| [docs/frontend-contract/README.md](frontend-contract/README.md) | null/date-only، Bearer عکس، consent، Draft/Published، CORS و تفاوت listها |
| [docs/messaging/CURRENT_MESSENGER_STATUS.md](messaging/CURRENT_MESSENGER_STATUS.md) | Personal بدون School Role، hint پروفایل ناقص و عکس تابع هویت مجاز |
| [docs/operations/PRODUCTION_CONFIGURATION_RUNBOOK.md](operations/PRODUCTION_CONFIGURATION_RUNBOOK.md) | تنظیمات CORS/Storage فعلی و حذف dependency صرفاً MFA |
| [docs/operations/RETENTION_AND_CLEANUP_POLICY.md](operations/RETENTION_AND_CLEANUP_POLICY.md) | guard عکس و Lesson، تغییر inventory جدول‌ها و حفظ cleanup خاموش |
| [docs/operations/WORKER_RELIABILITY_AND_RECOVERY.md](operations/WORKER_RELIABILITY_AND_RECOVERY.md) | startup validator حذف‌شده و guard جدید فایل؛ تضمین‌های worker ثابت |
| [docs/platform/BACKOFFICE_FRONTEND_HANDOFF.md](platform/BACKOFFICE_FRONTEND_HANDOFF.md) | ورود بدون عامل دوم و جدایی permission پلتفرم از مدیر/مدرس مدرسه |
| [docs/product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md](product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md) | DOB/Photo اکنون implemented؛ بازماندن proposalهای فردی/Badge/Source/quota و حفظ ممیزی اولیه |
| [docs/production/BACKUP_RESTORE.md](production/BACKUP_RESTORE.md) | backup سازگار DB+Storage عکس/جزوه؛ legacy MFA و عدم بازیابی داده با Down |
| [docs/production/DEPLOYMENT_IIS.md](production/DEPLOYMENT_IIS.md) | binary/schema جدید، MFA حذف‌شده و ارجاع دو mode CORS |
| [docs/production/OBSERVABILITY.md](production/OBSERVABILITY.md) | readiness config/SQL/Storage بدون ring حذف‌شده و signal صحیح503 |
| [docs/production/PRODUCTION_READINESS.md](production/PRODUCTION_READINESS.md) | مرز نسخه جدید، server acceptance و بسته‌های قبلی |
| [docs/production/PUBLISH_GUIDE_FA.md](production/PUBLISH_GUIDE_FA.md) | هماهنگی بسته و migration فعلی با حفظ OTP Test/Production |
| [docs/production/RELEASE_CHECKLIST.md](production/RELEASE_CHECKLIST.md) | smoke Profile/School، تنظیم CORS و rollout migration حذف داده |
| [tools/docs/sync_repository_docs.py](../tools/docs/sync_repository_docs.py) | قالب پایدار Profile/Photo/School/CORS و طبقه‌بندی مراجع محصول |

## ۵. جریان اجرا

`DocumentationSnapshot.cs → TestServer/ApiExplorer/OpenAPI + IDesignTimeModel → openapi.json/repository-inventory.json → sync_repository_docs.py → Markdown → build_backend_docs.py → HTML/PDF → validate_repository_docs.py`.

IHostedServiceها در host استخراج حذف‌اند؛ اتصال SQL، تماس SMS/AI و worker کسب‌وکاری اجرا نمی‌شود. معنی قرارداد از کد بررسی می‌شود؛ وجود schema اثبات مجوز یا deployment نیست.

## ۶. دیتابیس

مدل فعلی۱۰۷ جدول،۱۰۵۹ ستون،۲۴۳ FK،۳۰۲ index مستقل از key و۲۸۰ CHECK دارد. آخرین migration `20261028202239_CompleteSchoolManagement` است. سه migration جدید پس از حذف MFA، DOB nullable در Persons، مرجع اختیاری Photo روی User با FK Restrict/unique filtered و جدول‌های ClassLessons/ClassLessonAttachments و توسعه دعوت والد را اضافه می‌کنند.

RemovePlatformMfa قبلی دو جدول PlatformMfaCredentials/RecoveryCodes و دو ستون AssuranceLevel/StepUpAtUtc را Drop می‌کند؛ Down بازیابی داده نیست. startup migration و EnsureCreated اضافه نشده و دیتابیس عادی/سرور در این تحویل مقصد اجرا نیست. کنترل pending-model مستقل از applied schema سرور است.

## ۷. Endpointها

عملیات افزوده نسبت به کاتالوگ قبلی (14 مورد) از metadata واقعی استخراج شده‌اند:

| Method/Route | قرارداد |
|---|---|
| `DELETE /api/v1/me/profile-photo` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `GET /api/v1/academic/classes/{classId}/learners` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `GET /api/v1/academic/classes/{classId}/lessons` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `GET /api/v1/academic/classes/{classId}/teachers` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `GET /api/v1/academic/courses` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `GET /api/v1/academic/lessons/{lessonId}` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `GET /api/v1/academic/periods` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `GET /api/v1/organization/members` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `PATCH /api/v1/academic/lessons/{lessonId}` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `POST /api/v1/academic/classes/{classId}/lessons` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `POST /api/v1/academic/lessons/{lessonId}/publish` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `POST /api/v1/organization/guardian-invitations/{invitationId}/revoke` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `POST /api/v1/organization/people/{learnerId}/guardian-invitations` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |
| `PUT /api/v1/me/profile-photo` | قرارداد کامل در مرجع API و سیاست مصرف در Handoff تخصصی |

PATCH پروفایل dateOfBirth افزایشی با null/omission=حفظ و آینده400 دارد؛ DOB با شاهد سن فرق دارد. عکس PUT/DELETE self و دانلود Bearer تابع grant هویت است. دعوت والد consent صریح، reason، expiry و clientOperationId می‌خواهد؛ retry receipt مجوز احیای رابطه پایان‌یافته نیست. Lesson فقط برای Manager/Teacher منتسب قابل نوشتن، Published immutable و Draft برای Student پنهان است؛ Parent مجوز مستقل ندارد. Request/Response، status، auth و schema دقیق در مرجع و Handoff مدرسه موجودند؛ OpenAPI مجوز منبع را جایگزین نمی‌کند.

## ۸. مفاهیم فنی

schema از metadata؛ تمایز date-only از timestamp؛ nullable و omission؛ مالکیت User مستقل از Membership؛ authorization دانلود بر مبنای grant جاری؛ idempotency receipt در برابر rowversion؛ حذف metadata/reference در برابر حذف blob؛ archive پیشنهاد در برابر قابلیت اجرایی.

## ۹. تصمیم‌های طراحی

نام/field/enum از OpenAPI و جدول/constraint از EF استخراج شد؛ قواعد کسب‌وکاری در قالب‌ها و راهنمای دستی نگه داشته شدند. اسناد اصلی محصول و شواهد تاریخی تغییر تصمیم داده نمی‌شوند؛ مدل V2 برای DOB/Photo اصلاح شده ولی quota/individual commerce/Source هنوز proposal است. نمونه Docker حالت محدود CORS را صریح false انتخاب می‌کند؛ پیش‌فرض فعلی true را پنهان نمی‌کند. secret یا کلید حذف‌شدهٔ MFA به‌عنوان نیاز جاری توصیه نمی‌شود؛ backup قدیمی نیز با این کار حذف نشده است.

## ۱۰. تست‌ها

تست محصول جدیدی برای این مرحله اسناد اضافه نشده است؛ suite موجود Unit/Integration SQLServer/Architecture و۱۲ تست ابزار مستندسازی اجرا می‌شوند. کنترل سند تمام method/route/schema/Enum، جدول/migration، file/internal link و HTML/PDF hash را تطبیق می‌دهد. صفحات DOB/Photo/Lesson/SQL به‌صورت بصری نیز دیده شده‌اند.

## ۱۱. Validation

Restore موفق؛ Build اولیه و نهایی هر دو صفر warning/error. هر دو suite کامل **۲۳۳۹ تست موفق** داشتند: ۸۹۳ Unit،۱۴۳۲ Integration و۱۴ Architecture؛ failed/skipped صفر. شش TRX دو دور کنترل شدند. دوازده تست ابزار مستندسازی هم موفق‌اند؛ تست محصول تازه‌ای در این کار اسناد اضافه نشده است.

اجرا روی checkout مستقل و LocalDB تست با نام یکتا بود؛ تغییر موقت نام fixture بعد از اجرا به bytes اصلی بازگشت و وارد commit نیست. EF pending-model ندارد؛ migration روی DB عادی یا سرور اعمال نشده است.

کنترل۳۳۲ عملیات/۵۷۰ schema/۱۰۷ جدول/۵۲ migration و۱۴۴ ورودی inventory موفق؛ لینک شکسته و اختلاف Enum/OpenAPI صفر. PDFهای Guide/Reference/Frontend/Database به‌ترتیب۷۴/۴۲۱/۴۲۸/۱۵۹ صفحه دارند؛ bookmark و hash HTML متناظر کنترل شده و صفحه خالی/متن خارج صفحه صفر است. صفحات جدید DOB/Photo/Lesson/SQL بصری بازبینی شدند؛ gateهای runtime از این نتایج محلی بسته اعلام نشده‌اند.

## ۱۲. Git

branch `main`، remote `origin` و upstream `origin/main` موجودند. پیش از کار main چهار commit منتشرنشده داشت: b87fbd8،2bc18af،d889e67 و701cba0؛ این‌ها source مبنای سندند و در مرحله جاری دوباره پیاده‌سازی نمی‌شوند. پیام commit اسناد `docs: refresh profile school and deployment documentation` است. hash نهایی پس از commit در گفتگو اعلام می‌شود.

طبق AGENTS.md / Git Completion Rule بند۱۲، Push کار قبلی کاربر مجاز نیست؛ push این branch همهٔ آن چهار commit را هم منتشر می‌کند، پس برای آن تأیید صریح مالک لازم است. این تحویل هیچ force-push، history rewrite، Publish، Deploy یا schema سرور انجام نمی‌دهد.

## ۱۳. محدودیت‌های فعلی

gateهای Push دستگاه، P3/LiveKit rejoin، scale-out، TLS، restore/load/alert و پذیرش AI کامل با تست محلی بسته نمی‌شوند. Service Auth واقعی پیشین PASS؛ Parent Provider Timeout و Full-12 Deferred هستند. RAG/source lifecycle، اشتراک فردی، Badge، Learning Profile و quotaهای AI/Source هنوز پیاده‌شده فرض نمی‌شوند. cleanup پیش‌فرض خاموش و Ready-orphan reaper غایب است.

## ۱۴. نکات آموزشی

۱. افزودن DOB به DTO خوداظهاری، authority تأیید سن نمی‌سازد.

۲. عکس User با Role عوض نمی‌شود و URL آن مجوز دانلود نیست.

۳. End عضویت، نگه‌داری دادهٔ شخصی و ادامه دسترسی رسمی سه مسئلهٔ جدا هستند.

۴. Draft visibility و consent والد، فقط required/nullable schema نیستند؛ قواعد Application لازم‌اند.

۵. برابرشدن عدد۱۰۷ جدول با snapshot قدیمی، یکسان‌بودن schema را ثابت نمی‌کند.

۶. PDF تازه باید از آخرین Markdown باشد و هش HTML را حمل کند؛ تاریخ جدید به‌تنهایی سند تازه نیست.

## ۱۵. مسیر مطالعه

[وضعیت جاری](CURRENT_PROJECT_STATUS.md) → [راهنما](backend-api-guide.md) → [مرجع API](backend-api-reference.md) → [قرارداد فرانت](frontend-contract/README.md) → اسناد Profile/Photo/School در product → ProfileController/ProfilePhotoService و ClassLessonsController/ClassLessonService → Domain/port → EF store/config/migration → تست‌های مربوط → استقرار و recovery.

## ۱۶. مرحله بعد

پس از تعیین تکلیف Push commitهای قبلی، یک پذیرش محیط هدف با مجوز مشخص انتخاب شود؛ rollout schema52، دانلود عکس/جزوه با revocation مجوز یا AI Parent gate نمونه‌های مستقل‌اند. پیاده‌سازی پیشنهادهای V2 یا migration/deployment از به‌روزرسانی این اسناد مجوز نمی‌گیرد.
