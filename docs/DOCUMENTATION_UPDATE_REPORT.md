# گزارش به‌روزرسانی اسناد پروژه

بازبینی `2026-10-30` از `main@e11d4ec22d499eb47f256e49ff2f1a81f2ea66f9`. این تحویل مستندات موجود را با دو commit جدید `8084485` و `e11d4ec` تطبیق می‌دهد. **فایل مستند/کد تازه‌ای برای محصول ایجاد نشده است**؛ کتاب‌های HTML/PDF موجود بازسازی و اسناد مرتبط اصلاح شده‌اند. مبنای مقایسهٔ کاتالوگ، آخرین تحویل اسناد `bf9a819` است.

## ۱. خلاصه مرحله

پیش‌تر راهنماها332 عملیات/570 schema و107 جدول/52 migration را توصیف می‌کردند و Commerce/Realtime/پیوست تازه در کاتالوگ عمومی نبود. اکنون365 عملیات،322 مسیر،63 Controller،608 schema،113 جدول و55 migration پوشش دارند؛ رفتار جاری با پیشنهادهای Product و شواهد تاریخی تفکیک شده است.

## ۲. معماری

هیچ لایهٔ محصول، business rule یا Dependency flow تغییر نکرده است. تنها اسناد و قالب sync_repository_docs.py ویرایش شدند. جهت Api → Application و Infrastructure، Application → Domain و Infrastructure → Application/Domain همان Onion در Modular Monolith است. توضیح معماری از کلاس‌های واقعی CommercialSubscriptionService، PersonalClassService، RealtimeSyncService و ExamAnswerService گرفته شده است.

## ۳. فایل‌های مهم ایجادشده

فایل tracked جدید نداریم. JSON/OpenAPI snapshot، لاگ، TRX و preview فقط در .artifacts نادیده‌گرفته‌شده‌اند. HTML/PDF دیتابیس نیز مطابق ignore موجود محلی‌اند؛ policy ignore تغییر نکرده است.

## ۴. فایل‌های مهم تغییرکرده

| فایل موجود | دلیل و اثر |
|---|---|
| [README.md](../README.md) | آمار جاری و مسیر مطالعهٔ قراردادهای جدید |
| [docs/CURRENT_PROJECT_STATUS.md](CURRENT_PROJECT_STATUS.md) | وضعیت واقعی قابلیت‌ها، gateها و نتیجهٔ اجرای همین بازبینی |
| [docs/DOCUMENTATION_UPDATE_REPORT.md](DOCUMENTATION_UPDATE_REPORT.md) | فهرست تغییرها و گزارش آموزشی همین مرحله |
| [docs/backend-api-guide.html](backend-api-guide.html) | خروجی بازتولیدشده از Markdown جاری با فهرست و hash منبع |
| [docs/backend-api-guide.md](backend-api-guide.md) | شرح Commerce/PersonalClass، replay، پیوست آزمون و اصلاح ادعاهای قدیمی |
| [docs/backend-api-guide.pdf](backend-api-guide.pdf) | خروجی بازتولیدشده از Markdown جاری با فهرست و hash منبع |
| [docs/backend-api-reference.html](backend-api-reference.html) | خروجی بازتولیدشده از Markdown جاری با فهرست و hash منبع |
| [docs/backend-api-reference.md](backend-api-reference.md) | کاتالوگ365 operation و608 schema به‌همراه خطاها و literal واقعی Enum |
| [docs/backend-api-reference.pdf](backend-api-reference.pdf) | خروجی بازتولیدشده از Markdown جاری با فهرست و hash منبع |
| [docs/database-current-structure.md](database-current-structure.md) | فرهنگ113 جدول،55 migration و قیود مدل فعلی |
| [docs/deployment-windows-linux-docker.md](deployment-windows-linux-docker.md) | schema جدید، Data Protection cursor و smoke هدف |
| [docs/documentation-inventory.csv](documentation-inventory.csv) | فهرست154 سند با baseline و تاریخ بازبینی جدید |
| [docs/frontend-contract/MakanApp-Frontend-Backend-Contract.html](frontend-contract/MakanApp-Frontend-Backend-Contract.html) | خروجی بازتولیدشده از Markdown جاری با فهرست و hash منبع |
| [docs/frontend-contract/MakanApp-Frontend-Backend-Contract.pdf](frontend-contract/MakanApp-Frontend-Backend-Contract.pdf) | خروجی بازتولیدشده از Markdown جاری با فهرست و hash منبع |
| [docs/frontend-contract/README.md](frontend-contract/README.md) | مصرف صحیح Snapshot/replay، قرارداد تجاری، فایل آموزشی و422 Safety |
| [docs/messaging/CURRENT_MESSENGER_STATUS.md](messaging/CURRENT_MESSENGER_STATUS.md) | وضعیت ClientInvalidated/replay و Product blocker رسانه |
| [docs/operations/PRODUCTION_CONFIGURATION_RUNBOOK.md](operations/PRODUCTION_CONFIGURATION_RUNBOOK.md) | همگام‌سازی قراردادهای استقرار/پشتیبان‌گیری/retention/worker و metric جدید |
| [docs/operations/RETENTION_AND_CLEANUP_POLICY.md](operations/RETENTION_AND_CLEANUP_POLICY.md) | همگام‌سازی قراردادهای استقرار/پشتیبان‌گیری/retention/worker و metric جدید |
| [docs/operations/WORKER_RELIABILITY_AND_RECOVERY.md](operations/WORKER_RELIABILITY_AND_RECOVERY.md) | همگام‌سازی قراردادهای استقرار/پشتیبان‌گیری/retention/worker و metric جدید |
| [docs/platform/BACKOFFICE_FRONTEND_HANDOFF.md](platform/BACKOFFICE_FRONTEND_HANDOFF.md) | permission و workflow قرارداد تجاری Admin |
| [docs/product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md](product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md) | اصلاح Current vs Proposed بدون بازنویسی ممیزی اولیه |
| [docs/production/BACKUP_RESTORE.md](production/BACKUP_RESTORE.md) | همگام‌سازی قراردادهای استقرار/پشتیبان‌گیری/retention/worker و metric جدید |
| [docs/production/DEPLOYMENT_IIS.md](production/DEPLOYMENT_IIS.md) | همگام‌سازی قراردادهای استقرار/پشتیبان‌گیری/retention/worker و metric جدید |
| [docs/production/OBSERVABILITY.md](production/OBSERVABILITY.md) | همگام‌سازی قراردادهای استقرار/پشتیبان‌گیری/retention/worker و metric جدید |
| [docs/production/PRODUCTION_READINESS.md](production/PRODUCTION_READINESS.md) | همگام‌سازی قراردادهای استقرار/پشتیبان‌گیری/retention/worker و metric جدید |
| [docs/production/PUBLISH_GUIDE_FA.md](production/PUBLISH_GUIDE_FA.md) | همگام‌سازی قراردادهای استقرار/پشتیبان‌گیری/retention/worker و metric جدید |
| [docs/production/RELEASE_CHECKLIST.md](production/RELEASE_CHECKLIST.md) | همگام‌سازی قراردادهای استقرار/پشتیبان‌گیری/retention/worker و metric جدید |
| [tools/docs/sync_repository_docs.py](../tools/docs/sync_repository_docs.py) | حفظ متن قابلیت‌های جاری در بازتولید بعدی |

خروجی‌های محلی بازسازی‌شدهٔ خارج Git: `docs/database-current-structure.html` و `docs/database-current-structure.pdf`. Handoffهای تخصصی که در دو commit جدید ایجاد شده بودند خوانده و به راهنماها متصل شدند؛ آمار اجرا و تصمیم‌های مرحله‌ای داخل آن‌ها به نتیجهٔ امروز تبدیل نشده است.

## ۵. جریان اجرای قابلیت

`DocumentationSnapshot.cs → میزبان Testing با IHostedService حذف‌شده → OpenAPI/ApiExplorer و IDesignTimeModel → openapi.json/repository-inventory.json → sync_repository_docs.py → Markdown → build_backend_docs.py → HTML/PDF → validate_repository_docs.py`.

استخراج metadata هیچ اتصال SQL، SMS، AI یا worker تجاری اجرا نمی‌کند. تست‌های محصول جدا با SQL LocalDB ایزوله اجرا می‌شوند. کاتالوگ transport جای مجوز رابطه، context و قانون دامنه نیست.

## ۶. دیتابیس

این کار migration یا مدل تازه نساخت. مدل خوانده‌شده113 جدول،1162 ستون،266 FK،332 index مستقل از key و298 CHECK دارد. آخرین migration AddExamAnswerAttachments است.

سه migration جدید، مالکیت/دوره/رزرو و PersonalClass، اتصال ReportExport به رزرو و policy سؤال/AnswerRevisionAttachments را می‌آورند. شش جدول افزوده و قیود، FK مرکب، rowversion و filtered unique index در فرهنگ SQL مستندند. قفل مالک برای quota/ظرفیت و قفل Attempt برای revision/Finalize توضیح داده شدند. ثبت Job+رزرو اتمیک و تماس شبکه خارج transaction است. blob Retained با Detach حذف نمی‌شود. applied schema سرور تأیید نشده است.

## ۷. Endpointها

نسبت به کاتالوگ قبلی 33 عملیات افزوده و 0 حذف شده است. Method/route، request/response، nullable/enum و status منتشرشدهٔ هرکدام در مرجع API بازتولید شده‌اند؛ validation و auth منبع در راهنما/Handoff تخصصی آمده‌اند.

| Method و Route افزوده | قرارداد مصرف |
|---|---|
| `DELETE /api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}/attachments/{fileAssetId}` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `DELETE /api/v1/personal-classes/{classId}/participants/{participantId}` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/admin/v1/commerce/history/{targetId}` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/admin/v1/commerce/organizations/{ownerId}/subscription` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/admin/v1/commerce/plan-versions/{versionId}/validation` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/admin/v1/commerce/users/{ownerId}/subscription` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/commerce/catalog` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/commerce/organization/subscription` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/commerce/personal/orders` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/commerce/personal/orders/{orderId}` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/commerce/personal/subscription` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/conversations/{conversationId}/summary` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/personal-classes` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/personal-classes/invitations` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/personal-classes/{classId}` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/personal-classes/{classId}/participants` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `GET /api/v1/realtime/changes` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/admin/v1/commerce/organizations/{ownerId}/commercial-contracts` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/admin/v1/commerce/users/{ownerId}/commercial-contracts` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}/attachments` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/commerce/ai/simple-text/preflight` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/commerce/organization/quote` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/commerce/organization/subscription/downgrade` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/commerce/personal/orders` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/commerce/personal/orders/{orderId}/payments` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/commerce/personal/quote` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/commerce/personal/subscription/downgrade` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/personal-classes` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/personal-classes/invitations/{invitationId}/accept` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/personal-classes/{classId}/archive` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `POST /api/v1/personal-classes/{classId}/invitations` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `PUT /api/admin/v1/commerce/plan-versions/{versionId}/draft` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |
| `PUT /api/admin/v1/commerce/plans/{planId}/availability` | request/response دقیق در مرجع؛ مجوز و خطا در Handoff همان حوزه |

Commercial API نشست/مالک مجاز و Admin permission می‌خواهد؛ خرید role نمی‌سازد. RealtimeChanges با bootstrap، limit با پیش‌فرض100/حداکثر200، cursor همان نشست/بافت و409/503 resync/retry تعریف شده است؛ limit معتبر1..200 است. Attach آزمون policy صریح، Ready، uploader/org، lease و revision می‌خواهد؛400 policy،404 concealment و409 نسخه/lease/deadline واقعی‌اند.endpoint ساختگی برای release رسانه ساخته نشده است.422 Safety پیام‌رسان NotApplied است.

## ۸. مفاهیم فنی استفاده‌شده

Metadata snapshot، تفاوت schema و business validation، قرارداد نسخه‌دار، reserve/settle، idempotency، Snapshot boundary، invalidation/refetch، cursor opaque و hash همبستگی PDF/HTML در همین اسناد توضیح داده شده‌اند. claim/rowversion Outbox ترتیب جهانی کسب‌وکاری نیست؛ event تکراری با EventId تشخیص داده می‌شود.

## ۹. تصمیم‌های طراحی

بازتولید کاتالوگ از کد به‌جای شمارش دستی، از حذف API/DTO جلوگیری می‌کند. متن جریان‌ها دستی و مرتبط باقی ماند تا قواعد کسب‌وکاری فقط از nullable/required حدس زده نشوند. ممیزی‌های اولیهٔ Product و نتایج تست تاریخی حفظ شدند، ولی Current vs Proposed و راهنمای خواندن آن‌ها اصلاح شدند. Data Protection cursor مصرف جدا از MFA حذف‌شده دارد؛ تنظیم خصوصی جدید یا probe ساختگی اختراع نشد.

## ۱۰. تست‌ها

تست تازهٔ محصول به کار مستندات اضافه نشد. suite موجود Unit قوانین Domain/Application، Integration میزبان/SQL/مجوز/idempotency و Architecture جهت وابستگی‌ها را بررسی می‌کند.12 تست موجود ابزار اسناد نیز اجرا می‌شوند. کیفیت PDF با کنترل page bounds/blank/bookmark/hash و بازبینی بصری صفحات مرتبط بررسی می‌شود؛ این‌ها جای UI/سرور واقعی نیستند.

## ۱۱. Validation

Restore موفق؛ Build اولیه و نهایی هر دو با **صفر warning و صفر error** موفق شدند. suite کامل اولیه و نهایی هر کدام **2478 تست موفق** داشتند:922 Unit،1542 Integration و14 Architecture؛ failed/skipped صفر. شمارنده‌های شش TRX کنترل شدند.12 تست ابزار اسناد هم موفق‌اند؛ تست تازهٔ محصول به کار مستندات اضافه نشد.

اجرای محصول روی checkout مستقل و LocalDB یکتای `MakanApp_DocsSync_20261030_c817e6d9` بود؛ نام موقت fixture پس از suite نهایی به bytes اصلی برگشت و وارد تحویل نیست. EF has-pending-model-changes تغییر معلق ندارد؛ هیچ migration روی دیتابیس معمول یا سرور اجرا نشده است.

کنترل365 operation،608 schema،113 جدول،55 migration و154 ورودی inventory موفق است؛ لینک شکسته و اختلاف Enum/OpenAPI صفر. PDFهای Guide/Reference/Frontend/Database به‌ترتیب 78 / 458 / 466 / 172 صفحه دارند؛ bookmark و SHA متناظر HTML کنترل شدند. کنترل صفحهٔ خالی و متن خارج صفحه موفق و صفحات Commerce/Replay/Attachment/SQL بصری بازبینی شدند. این‌ها پذیرش UI، دستگاه یا سرور واقعی نیستند.

## ۱۲. Git

Source baseline برابر e11d4ec است و با hash commit اسناد تفاوت دارد. سه فایل untracked قبلی tools/ai/provider_diagnostic.py، run_exact_ai.py و test_provider_diagnostic.py با hash اولیه حفظ و خارج stage هستند. تغییر موقت نام DatabaseName در fixture فقط checkout ایزوله است و پیش از تحویل bytes اصلی برمی‌گردد. فقط فایل‌های جدول بخش۴ stage می‌شوند؛ local commit و push طبق قواعد AGENTS فقط پس از موفقیت همهٔ بررسی‌ها و بازبینی انجام می‌شود. hash/message/branch/remote و نتیجهٔ واقعی Git در پاسخ تحویل ثبت می‌شوند؛ این سند خود hash آینده را حدس نمی‌زند. Publish/Deploy یا migration سرور انجام نشده است.

## ۱۳. محدودیت‌های فعلی

gateway پرداخت واقعی، قیمت/سقف تجاری نهایی، Runtime AI و lifecycle Source/RAG، Learning Profile/Badge، چرخهٔ مستقل Assignment/Exam کلاس شخصی، workflow release رسانه BE-01، Push دستگاه، backplane/keyring چند replica و SLO/storm واقعی در این کار ساخته یا تأیید نشده‌اند. تداوم key ring cursor، IIS/TLS/Proxy و restore/load هدف gate جدا دارند.

## ۱۴. نکات آموزشی

1. Entitlement اجازهٔ مصرف قابلیت است؛ Authorization داده هنوز به رابطه و context نیاز دارد.
2. Legacy با تاریخ نامعلوم را نباید Unlimited یا منقضی حدسی کرد؛ enforcement نقطهٔ ثبت واقعی دارد.
3. Capacity با ماه جدید reset نمی‌شود؛ reservation از مصرف قطعی و Unknown جداست.
4. SignalR hint است؛ Snapshot و replay با EventId و ContextRevision مانع اعمال دوباره یا دادهٔ بافت قدیمی می‌شوند.
5. Ready فایل به معنی Attach/Submit/Send یا Safety Approved نیست؛ هر binding مجوز جاری خود را دارد.
6. Detach revision را عوض می‌کند؛ حفظ blob تاریخچه به معنی grant دانلود تاریخچهٔ detached نیست.
7. ring حذف‌شدهٔ MFA با Data Protection مصرف‌شده برای cursor یکی نیست؛ readiness هم تداوم cursor را اثبات نمی‌کند.

## ۱۵. مسیر پیشنهادی مطالعه کد

CommercialSubscriptionsController → CommercialSubscriptionService/CommercialActorAccess → Subscription.Monthly.cs/UsageReservation → EfCommercialStore و EfReportExportJobStore → migrationهای29اکتبر → CommercialSubscriptionEndpointsTests.

RealtimeController/MessagingHub → RealtimeSyncService/RealtimeInvalidationWriter → RealtimeCursorProtector/SignalRInvalidationNotifier → OutboxDispatcher → AccountRealtimeReplayTests و AccountRealtimeConcurrentReplayTests.

ExamsController → ExamAnswerService.Attachments.cs → ExamAnswerAttachmentValidator → AnswerRevisionAttachment/ExamAnswerAttachmentPolicy → EfExamAnswerStore → AnswerRevisionAttachmentConfiguration و AddExamAnswerAttachments → AcademicExamAttachmentTests و AcademicExamAttachmentSecurityTests.

## ۱۶. مرحله بعد

با اسناد و نسخهٔ تأییدشده، ابتدا اتصال واقعی Frontend به bootstrap/replay و جریان Upload/Attach/Finalize در محیط مجاز تست شود؛ سپس smoke استقرار هدف و custody کلید cursor با درخواست مستقل انجام شود. تصمیم محصول BE-01 و آماده‌سازی درگاه/AI را با کار اسناد یا دورزدن Safety مخلوط نکنید.
