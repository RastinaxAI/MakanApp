# گزارش به‌روزرسانی اسناد Backend — ۲۰۲۶-۱۱-۲۶

مبنای کد `source@f8178a47de94eae0ec2431adebcb9256a9789786` روی `feature/product-v6-backend-alignment`؛ تحویل فقط مستندات و ابزار تولید آن‌هاست. شواهد ممیزی `reports/api-error-audit` و فایل‌های شخصی کاربر بازنویسی نشده‌اند.

## ۱. خلاصه مرحله

راهنماهای عمومی با الحاقیه‌های متعدد، بعضی قواعد سن/اشتراک، نبود Media Review، تعویق Source/RAG و رفتار قدیمی خطا را تکرار می‌کردند. اکنون روایت اصلی، مرجع API، قرارداد فرانت، بیزینس و استقرار بر اساس Source جاری هماهنگ‌اند. 41 فایل موجود به‌روزرسانی شد؛ فایل تازهٔ tracked ساخته نشد. HTML/PDF دو کتاب اصلی و قرارداد فرانت بازسازی و HTML/PDF دیتابیس طبق ignore قبلی محلی تولید شدند.

## ۲. معماری

کد Domain/Application/Infrastructure/Api تغییر نکرد. Source فعلی همچنان .NET9/EF9/SQL Server و Onion داخل Modular Monolith است. ابزار Snapshot میزبان Testing بدون HostedService و مدل طراحی EF را می‌خواند؛ تولید سند هیچ SQL connection، SMS یا AI call نمی‌سازد. آزمون‌های Integration جداگانه روی SQL تست ایزوله اجرا شدند.

## ۳. فایل‌های مهم ایجادشده

هیچ فایل تازه‌ای وارد Git نمی‌شود. فایل‌های اجرایی موقت، log/TRX و پیش‌نمایش‌ها در `.artifacts/docs-sync-20261126` محلی‌اند. دو خروجی Database HTML/PDF محلی/ignored می‌مانند. سه فایل از قبل موجود ابزار AI کاربر خارج این تغییر و بدون stage محفوظ‌اند.

## ۴. فایل‌های تغییرکرده

| مسیر | مسئولیت تغییر |
|---|---|
| [README.md](../README.md) | راهنمای یکپارچه، وضعیت جاری یا گزارش آموزشی/Validation |
| [docs/CURRENT_PROJECT_STATUS.md](../docs/CURRENT_PROJECT_STATUS.md) | راهنمای یکپارچه، وضعیت جاری یا گزارش آموزشی/Validation |
| [docs/DOCUMENTATION_UPDATE_REPORT.md](../docs/DOCUMENTATION_UPDATE_REPORT.md) | راهنمای یکپارچه، وضعیت جاری یا گزارش آموزشی/Validation |
| [docs/ai-handoff/05_AI_ERROR_AND_RESILIENCE_CONTRACT.md](../docs/ai-handoff/05_AI_ERROR_AND_RESILIENCE_CONTRACT.md) | راهنمای یکپارچه، وضعیت جاری یا گزارش آموزشی/Validation |
| [docs/ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md](../docs/ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md) | راهنمای یکپارچه، وضعیت جاری یا گزارش آموزشی/Validation |
| [docs/backend-api-guide.html](../docs/backend-api-guide.html) | خروجی بازسازی‌شده از Markdown جاری؛ RTL، فهرست، نشانک و SHA منبع |
| [docs/backend-api-guide.md](../docs/backend-api-guide.md) | راهنمای یکپارچه، وضعیت جاری یا گزارش آموزشی/Validation |
| [docs/backend-api-guide.pdf](../docs/backend-api-guide.pdf) | خروجی بازسازی‌شده از Markdown جاری؛ RTL، فهرست، نشانک و SHA منبع |
| [docs/backend-api-reference.html](../docs/backend-api-reference.html) | خروجی بازسازی‌شده از Markdown جاری؛ RTL، فهرست، نشانک و SHA منبع |
| [docs/backend-api-reference.md](../docs/backend-api-reference.md) | کاتالوگ۴۹۸ عملیات و۷۹۶ schema از OpenAPI واقعی |
| [docs/backend-api-reference.pdf](../docs/backend-api-reference.pdf) | خروجی بازسازی‌شده از Markdown جاری؛ RTL، فهرست، نشانک و SHA منبع |
| [docs/database-current-structure.md](../docs/database-current-structure.md) | فرهنگ۱۵۳ جدول و۷۸ Migration از مدل طراحی EF |
| [docs/deployment-windows-linux-docker.md](../docs/deployment-windows-linux-docker.md) | تنظیم مؤثر انتشار، Smoke هدف، Migration و مرز شواهد/بازیابی |
| [docs/documentation-inventory.csv](../docs/documentation-inventory.csv) | فهرست۲۳۲ سند و مبنای بازبینی |
| [docs/frontend-contract/MakanApp-Frontend-Backend-Contract.html](../docs/frontend-contract/MakanApp-Frontend-Backend-Contract.html) | خروجی بازسازی‌شده از Markdown جاری؛ RTL، فهرست، نشانک و SHA منبع |
| [docs/frontend-contract/MakanApp-Frontend-Backend-Contract.pdf](../docs/frontend-contract/MakanApp-Frontend-Backend-Contract.pdf) | خروجی بازسازی‌شده از Markdown جاری؛ RTL، فهرست، نشانک و SHA منبع |
| [docs/frontend-contract/README.md](../docs/frontend-contract/README.md) | قرارداد مصرف Client و Platform با مجوز، خطا، Retry و stateهای جاری |
| [docs/messaging/CURRENT_MESSENGER_STATUS.md](../docs/messaging/CURRENT_MESSENGER_STATUS.md) | راهنمای یکپارچه، وضعیت جاری یا گزارش آموزشی/Validation |
| [docs/operations/PRODUCTION_CONFIGURATION_RUNBOOK.md](../docs/operations/PRODUCTION_CONFIGURATION_RUNBOOK.md) | تنظیم مؤثر انتشار، Smoke هدف، Migration و مرز شواهد/بازیابی |
| [docs/operations/RETENTION_AND_CLEANUP_POLICY.md](../docs/operations/RETENTION_AND_CLEANUP_POLICY.md) | تنظیم مؤثر انتشار، Smoke هدف، Migration و مرز شواهد/بازیابی |
| [docs/operations/WORKER_RELIABILITY_AND_RECOVERY.md](../docs/operations/WORKER_RELIABILITY_AND_RECOVERY.md) | تنظیم مؤثر انتشار، Smoke هدف، Migration و مرز شواهد/بازیابی |
| [docs/platform/BACKOFFICE_FRONTEND_HANDOFF.md](../docs/platform/BACKOFFICE_FRONTEND_HANDOFF.md) | قرارداد مصرف Client و Platform با مجوز، خطا، Retry و stateهای جاری |
| [docs/product-v6/ADAPTIVE_AI_FRONTEND_HANDOFF_FA.md](../docs/product-v6/ADAPTIVE_AI_FRONTEND_HANDOFF_FA.md) | تفکیک سیاست جاری Open Access/AI/MediaReview از قرارداد مالی و شواهد تاریخی |
| [docs/product-v6/BACKEND_V6_ADMIN_HANDOFF_FA.md](../docs/product-v6/BACKEND_V6_ADMIN_HANDOFF_FA.md) | تفکیک سیاست جاری Open Access/AI/MediaReview از قرارداد مالی و شواهد تاریخی |
| [docs/product-v6/BACKEND_V6_FRONTEND_HANDOFF_FA.md](../docs/product-v6/BACKEND_V6_FRONTEND_HANDOFF_FA.md) | تفکیک سیاست جاری Open Access/AI/MediaReview از قرارداد مالی و شواهد تاریخی |
| [docs/product-v6/COMMERCE_CURRENT_POLICY_FRONTEND_HANDOFF_FA.md](../docs/product-v6/COMMERCE_CURRENT_POLICY_FRONTEND_HANDOFF_FA.md) | تفکیک سیاست جاری Open Access/AI/MediaReview از قرارداد مالی و شواهد تاریخی |
| [docs/product-v6/COMMERCE_PRODUCT_MATRIX_FA.md](../docs/product-v6/COMMERCE_PRODUCT_MATRIX_FA.md) | تفکیک سیاست جاری Open Access/AI/MediaReview از قرارداد مالی و شواهد تاریخی |
| [docs/product-v6/CURRENT_RELEASE_BLOCKERS_FA.md](../docs/product-v6/CURRENT_RELEASE_BLOCKERS_FA.md) | تفکیک سیاست جاری Open Access/AI/MediaReview از قرارداد مالی و شواهد تاریخی |
| [docs/product-v6/CURRENT_RELEASE_SCOPE_FA.md](../docs/product-v6/CURRENT_RELEASE_SCOPE_FA.md) | تفکیک سیاست جاری Open Access/AI/MediaReview از قرارداد مالی و شواهد تاریخی |
| [docs/product-v6/FILES_LIBRARY_FRONTEND_HANDOFF_FA.md](../docs/product-v6/FILES_LIBRARY_FRONTEND_HANDOFF_FA.md) | تفکیک سیاست جاری Open Access/AI/MediaReview از قرارداد مالی و شواهد تاریخی |
| [docs/product-v6/PERSONAL_AI_FRONTEND_HANDOFF_FA.md](../docs/product-v6/PERSONAL_AI_FRONTEND_HANDOFF_FA.md) | تفکیک سیاست جاری Open Access/AI/MediaReview از قرارداد مالی و شواهد تاریخی |
| [docs/product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md](../docs/product/MAKANAPP_PRODUCT_BUSINESS_MODEL_V2_FA.md) | تفکیک سیاست جاری Open Access/AI/MediaReview از قرارداد مالی و شواهد تاریخی |
| [docs/production/BACKUP_RESTORE.md](../docs/production/BACKUP_RESTORE.md) | تنظیم مؤثر انتشار، Smoke هدف، Migration و مرز شواهد/بازیابی |
| [docs/production/DEPLOYMENT_IIS.md](../docs/production/DEPLOYMENT_IIS.md) | تنظیم مؤثر انتشار، Smoke هدف، Migration و مرز شواهد/بازیابی |
| [docs/production/OBSERVABILITY.md](../docs/production/OBSERVABILITY.md) | تنظیم مؤثر انتشار، Smoke هدف، Migration و مرز شواهد/بازیابی |
| [docs/production/PRODUCTION_READINESS.md](../docs/production/PRODUCTION_READINESS.md) | تنظیم مؤثر انتشار، Smoke هدف، Migration و مرز شواهد/بازیابی |
| [docs/production/PUBLISH_GUIDE_FA.md](../docs/production/PUBLISH_GUIDE_FA.md) | تنظیم مؤثر انتشار، Smoke هدف، Migration و مرز شواهد/بازیابی |
| [docs/production/RELEASE_CHECKLIST.md](../docs/production/RELEASE_CHECKLIST.md) | تنظیم مؤثر انتشار، Smoke هدف، Migration و مرز شواهد/بازیابی |
| [tools/docs/build_backend_docs.py](../tools/docs/build_backend_docs.py) | رفع تولید قرارداد Controller چندفایلی، قالب‌های جاری یا آزمون و چاپ قابل نگه‌داری |
| [tools/docs/sync_repository_docs.py](../tools/docs/sync_repository_docs.py) | رفع تولید قرارداد Controller چندفایلی، قالب‌های جاری یا آزمون و چاپ قابل نگه‌داری |
| [tools/docs/test_documentation_tools.py](../tools/docs/test_documentation_tools.py) | رفع تولید قرارداد Controller چندفایلی، قالب‌های جاری یا آزمون و چاپ قابل نگه‌داری |

## ۵. جریان واقعی که مستند شد

`AiController → AiChatService → AiInvocationExecutor → AiServiceClient / IAiChatSources → EfAiConversationStore → SQL Server → JSON یا SSE`؛ Context و Source ACL پیش از تماس/ادامه کنترل می‌شوند و فقط زوج کامل وارد History می‌شود.

`FilesController → FileService` برای Upload/Ready از ارسال جداست. در ارسال رسانه، `ReviewedMediaContentSafetyProvider → ManualMediaSafetyProvider` تصمیم اثرانگشت‌دار Moderation موجود را مصرف می‌کند؛ Approval به‌تنهایی Message نمی‌سازد. `AssignmentsController → AssignmentService` تغییر محدود Policy Published را از ویرایش Draft جدا می‌کند. این کلاس‌ها از قبل وجود داشتند؛ این مرحله آن‌ها را پیاده‌سازی نکرد.

## ۶. دیتابیس

مدل جاری۱۵۳ جدول،۷۸ Migration تا `20261122085930_AddMessagingMediaReviews` دارد. جدول هر schema در راهنما نیز از همین مدل هماهنگ شد. PK/FK/index/CHECK/rowversion در فرهنگ SQL کامل آمده است. AI History، SourceVersion، AccountDeletion checkpoints و MediaReview باید با Blob و Evidence سازگار نگه‌داری شوند. Migration تازه ساخته یا روی دیتابیس عادی/سرور اجرا نشد؛ pending model changes وجود ندارد.

## ۷. Endpointهای مهم

کاتالوگ۴۹۸ operation،۴۴۲ مسیر،۹۱ Controller و۷۹۶ schema، Request/Response/Authentication/Policy منتشرشده را پوشش می‌دهد. قواعد دامنه فراتر از schema در راهنما توضیح داده شده‌اند:

- `POST /api/v1/ai/chat` و `/chat/stream`: Bearer و Context/Source مجاز، OperationId پایدار؛ SSE با done کامل است، RAG stream409 و Unknown بدون retry خودکار.
- `GET /api/v1/ai/conversations/{conversationId}/history`: فقط Owner/Context جاری؛ پنجرهٔ زوج کامل، no-store و نامنطبق404.
- `GET /api/v1/files/{fileId}/messaging-review`: وضعیت امن مالک؛ Pending/Rejected اثر تازهٔ تحویل ندارند. actions موجود Moderation با Claim/Review+Enforce/expectedVersion/reason تصمیم صریح می‌گیرد.
- `PATCH /api/v1/academic/assignments/{assignmentId}/submission-policy`: چهار فیلد اجباری، مجوز جاری مدرس/مدیر یا اختیار صریح PersonalClass و دو RowVersion؛412 برای نسخهٔ کهنه. خطای409 SUBMISSION_DEADLINE_PASSED در مسیرهای ثبت/پیوست پاسخ از SubmissionService می‌آید و Client باید Draft/FileAssetId را حفظ کند.
- framework `/api`:400/404/405/413/415 با code پایدار؛ کد دامنه محفوظ، HEAD بدون body و Retry-After معتبر AI/Knowledge روی429/503 منتقل می‌شود.

هیچ Endpoint تازه‌ای در این مرحله اضافه نشد.

## ۸. مفاهیم فنی استفاده‌شده

Source snapshot، استخراج ApiExplorer/OpenAPI، مدل طراحی EF، تفکیک سیاست Runtime از مدل مالی، Markdown به HTML RTL/PDF، نشانک و SHA منبع، و SQL تست ایزوله. Renderer شمارش صفحه، متن بیرون صفحه و مقصد لینک داخلی را کنترل می‌کند.

## ۹. تصمیم‌های طراحی

روایت جاری در کتاب‌ها یکپارچه شد و شواهد تاریخی مرحله‌ها دست‌نخورده ماند؛ صرف اضافه‌کردن هشدار بالای راهنما برای تناقض‌های اجرایی کافی نبود. قالب generator هم اصلاح شد تا بازتولید، سیاست قدیمی را برنگرداند. AiController اکنون partial است؛ لینک هر Action به فایل واقعی آن و لینک فهرست Controller به فایل اصلی می‌رود. جلد source را نشان می‌دهد و شاخهٔ feature را main معرفی نمی‌کند. فاصلهٔ فهرست چاپ کاهش یافت تا تک‌سطر آخر به صفحهٔ تقریباً خالی منتقل نشود؛ کنترل کیفیت PDF حذف نشد.

## ۱۰. تست‌ها

یک آزمون خالص ابزار افزوده شد: `test_partial_controller_uses_primary_source_file` هم فایل اصلی و هم Action داخل AiController.Streaming.cs را کنترل می‌کند؛ این Regression برای خرابی واقعی تولید مرجع است. مجموع۱۴ آزمون Python موفق‌اند. تست محصول تازه نوشته نشد؛ suiteهای موجود Unit/Integration/Architecture برای سلامت مبنا اجرا شدند.

## ۱۱. Validation واقعی

Restore موفق؛ Build اولیه و نهایی با صفر Warning و Error موفق. دو دور کامل هر کدام 3789 تست موفق، صفر Failed و Skipped دارند؛ Counters هر شش TRX خوانده شد:

| دور | مجموعه | اجراشده | موفق | شکست | ردشده |
|---|---|---:|---:|---:|---:|
| initial | Unit | 1499 | 1499 | 0 | 0 |
| initial | Integration | 2274 | 2274 | 0 | 0 |
| initial | Architecture | 16 | 16 | 0 | 0 |
| final | Unit | 1499 | 1499 | 0 | 0 |
| final | Integration | 2274 | 2274 | 0 | 0 |
| final | Architecture | 16 | 16 | 0 | 0 |

۱۴ آزمون ابزار موفق؛ EF has-pending-model-changes بدون تغییر معلق؛ پوشش۴۹۸ عملیات/۷۹۶ schema/۱۵۳ جدول/۷۸ Migration و۲۳۲ ورودی inventory کامل؛ لینک شکسته و اختلاف Enum/OpenAPI صفر. نشانک‌ها و SHA HTML داخل PDF تطبیق دارند؛ صفحهٔ خالی و متن خارج صفحه صفر. صفحات فارسی جریان AI و فهرست SQL بصری بازبینی شدند.

| خروجی | صفحه | نشانک |
|---|---:|---:|
| backend-api-guide | 79 | 139 |
| backend-api-reference | 800 | 1390 |
| database-current-structure | 243 | 157 |
| frontend-contract | 804 | 1399 |

تلاش نخست به علت آماده‌سازی محیط موفق نبود: نام دیتابیس انتخابی عبارت IntegrationTests مورد انتظار محافظ NotificationAcademicFixture را نداشت. پس از۱۵۵۸ تست Integration (۱۵۴۰ موفق و۱۸ شکست همین guard)، اجرای متعلق به این کار متوقف شد؛ این دور Aborted است و PASS گزارش نمی‌شود. محافظ تغییر نکرد؛ نام دیتابیس مستقل اصلاح،۱۸ تست آسیب‌دیده جدا پذیرفته و سپس دو دور کامل بالا اجرا شدند. log/TRX ناموفق برای شواهد محفوظ است.

اجرای صحیح در checkout جدا و LocalDB اختصاصی `MakanApp_DocsSync_IntegrationTests_20261126_f8178a4` بود. تغییر موقت نام fixture پس از آزمون‌ها به bytes اصلی برگشت و وارد تحویل نمی‌شود. محیط عادی توسعه و سرور مقصد تغییر نکردند. شواهد این مرحله در `.artifacts/docs-sync-20261126/validation-summary.json` محلی‌اند.

برای کاهش حجم بسیار زیاد TRX، فقط در process دور نهایی، سطح `Microsoft.EntityFrameworkCore.Database.Command` روی Warning گذاشته شد؛ لاگ اطلاعاتی SQL کم شد و Warning/Error و همهٔ تست‌ها/assertionها حفظ شدند. config پروژه یا کاربر تغییر نکرد و مقدار قبلی process در finally بازگردانده شد. دور کامل اصلاح‌شدهٔ اول با تنظیم Logging قبلی نیز موفق بود.

## ۱۲. Git

پیام commit این مرحله: `docs: refresh current backend frontend and deployment contracts`؛ فقط فایل‌های جدول بالا stage می‌شوند. شاخهٔ مقصد `feature/product-v6-backend-alignment` و remote=`origin` است. شناسه و نتیجهٔ واقعی Push پس از ثبت موفق در پیام نهایی تحویل اعلام می‌شود؛ Source hash بالای سند شناسهٔ commit اسناد نیست. هیچ Merge به main، Force push یا Deploy انجام نمی‌شود.

## ۱۳. محدودیت‌های فعلی

Runtime پرداخت، Paid Provisioning و Push بیرونی Deferred، آموزش مستقل PersonalClass خاموش و Library عمومی/AI بسته‌اند. AI/Knowledge Enabled تابع config محیط است؛ Service Auth دو سرویس false. RAG Streaming رسمی upstream موجود نیست. اجرای حذف حساب/cleanup نیازمند تنظیم مصوب؛ چند replica، Load/SLO، TLS، backup/restore، ring/restart و alert محیط هدف از تست محلی پذیرفته نمی‌شوند. Manual Review اسکن جامع خودکار AV/CDR نیست.

## ۱۴. نکات آموزشی

1. Open Access شرط مالی را برمی‌دارد؛ ACL و Safety و Feature flag مستقل پابرجا هستند.
2. Ready فایل، Approval رسانه و Message Sent سه وضعیت متفاوت‌اند.
3. RowVersion برای تضاد داده و OperationId برای تکرار همان intent است؛ جای هم را نمی‌گیرند.
4. در SSE، HTTP200 پیش از تکمیل اتفاق می‌افتد؛ فقط done پاسخ کامل را تأیید می‌کند.
5. مدل طراحی EF و OpenAPI قرارداد Source را ثابت می‌کنند؛ applied schema و readiness سرور شاهد جدا می‌خواهند.
6. PDF باید با HTML هم‌نسخه باشد؛ Hash، نشانک، صفحه‌بندی و خوانایی فارسی همگی بخشی از تحویل‌اند.
7. پاسخ200 در preflight فقط گزارش قابلیت است؛ authorized=true بدون retrievalAvailable، موفقیت اجرای RAG نیست. خطای شبکه بدون HTTP نیز نباید به500 ساختگی تبدیل شود.

## ۱۵. مسیر پیشنهادی مطالعه

[راهنمای بک‌اند](backend-api-guide.md) → [مرجع API](backend-api-reference.md) → [قرارداد فرانت](frontend-contract/README.md) → [AiController.Streaming.cs](../MakanApp.Api/Controllers/AiController.Streaming.cs) → [AiChatService.cs](../MakanApp.Application/Ai/AiChatService.cs) → [EfAiConversationStore.cs](../MakanApp.Infrastructure/Ai/EfAiConversationStore.cs) → [ManualMediaSafetyProvider.cs](../MakanApp.Infrastructure/Messaging/ManualMediaSafetyProvider.cs) → [ساختار SQL](database-current-structure.md) → [MessagingMediaReviewAcceptanceTests.cs](../tests/MakanApp.IntegrationTests/MessagingMediaReviewAcceptanceTests.cs) → [Runbook استقرار](deployment-windows-linux-docker.md).

## ۱۶. مرحله بعد

در صورت درخواست انتشار، از همین Source بسته و SQL تازه ساخته و Smoke مقصد طبق Runbook اجرا شود؛ فعال‌سازی پرداخت/Push، تغییر سیاست حذف و کار Frontend در آن مرحله خودکار ادغام نشوند. به‌روزرسانی مستندات مجوز Deploy نیست.
