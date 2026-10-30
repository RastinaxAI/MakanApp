# انتخاب منبع و مجوز Retrieval در Backend

مبنای این مرحله `main@f1d69cd` و راهنمای رسمی `MakanApp_AI_Backend_Integration_Guide_FA.pdf`، Contract v1، تاریخ ۲۰۲۶/۱۰/۲۹ است. این سند رفتار کد را توضیح می‌دهد؛ اثبات استقرار یا Retrieval واقعی نیست. [قرارداد مصرف‌کننده](../product/AI_SOURCE_SELECTION_FRONTEND_HANDOFF_FA.md).

## نتیجهٔ Audit قرارداد

هر ۱۲ عملیات همچنان از `IAiServiceClient` و `HmacAiServiceCredentialProvider` موجود استفاده می‌کنند. Service JWT فقط `iss/aud/sub/scope/iat/nbf/exp/jti` و headerهای قراردادی `HS256/typ/kid` را دارد؛ scope همان `makan.ai.invoke` است. User/Role/Source/Payer claim اضافه نشده است. Bearer، `X-AI-Contract-Version: v1`، JSON و تطابق دقیق Correlation حفظ شده‌اند. URL، timeout، retry، failover و credential تغییر نکرده‌اند؛ alias قراردادی `education-fast` است و Provider واقعی مسئولیت AI Service است.

DTO عمومی Backend با DTO مستقیم AI یکی نیست. `selectedSourceIds` و `operationId` فقط به چهار Command عمومی افزوده شده‌اند؛ در هیچ request مستقیم AI، manifest، Source ID، chunk، document یا storage path ارسال نمی‌شود. درخواست بدون انتخاب، خودکار تمام منابع را برنمی‌دارد.

## ماتریس ۱۲ عملیات

همهٔ مسیرها POST و authenticated هستند. ستون AI نیز با `/api/v1/ai/` آغاز می‌شود. وضعیت Optional فقط در چهار مسیر دارای نیاز محتوایی و body موجود انتخاب شده است. NotApplicable یعنی در قرارداد جاری آن عملیات انتخاب آزاد منبع نداریم؛ الزام ساختگی برای مرحلهٔ آینده تعیین نشده است.

| عملیات | Route عمومی بعد از `/api/v1/ai/` | Route AI بعد از `/api/v1/ai/` | نقش و Context فعلی | انتخاب منبع | Context تجاری |
|---|---|---|---|---|---|
| Chat | `chat` | `chat` | Student/Parent/Teacher/Manager در سازمان جاری؛ Student با OrganizationPerson معتبر | Optional؛ Class source بدون Class context پذیرفته نمی‌شود | سازمان جاری؛ payer مستقل |
| Student Analysis | `students/analyze` | `students/analyze` | Student، enrollment و گزارش‌های مجاز | NotApplicable؛ تحلیل دادهٔ ساخت‌یافته | سازمان جاری |
| Study Plan | `study-plan` | `study-plan` | Student، کلاس‌های فعال و goals/preferences | Optional؛ Personal خود فرد و Organization طبق ACL، نه Class بدون binding | سازمان جاری |
| Homework Explain | `homeworks/{assignmentId}/explain` | `homeworks/explain` | Student؛ تکلیف Published و مجاز؛ Class از تکلیف استخراج می‌شود | Optional | سازمان تکلیف |
| Exam Prepare | `exams/{examId}/prepare` | `exams/prepare` | Student؛ آزمون Published مجاز، بدون answer key | NotApplicable؛ قرارداد فعلی metadata آزمون را دارد | سازمان آزمون |
| Parent Child Summary | `parents/child-summary` | `parents/child-summary` | Parent و GuardianRelation جاری همان کودک | NotApplicable؛ گزارش ساخت‌یافته، بدون چت/منبع خصوصی کودک | سازمان کودک |
| Teacher Class Summary | `teachers/classes/{classId}/summary` | `teachers/class-summary` | TeacherAssignment جاری همان Class | NotApplicable؛ گزارش کلاس | سازمان کلاس |
| Teacher Student Insight | `teachers/classes/{classId}/students/{studentId}/insight` | `teachers/student-insight` | TeacherAssignment و enrollment دانش‌آموز همان Class | NotApplicable؛ گزارش دانش‌آموز | سازمان کلاس |
| Teacher Activity Suggestions | `teachers/classes/{classId}/activity-suggestions` | `teachers/activity-suggestions` | TeacherAssignment جاری؛ هدف، مواد و زمان | Optional؛ Class دقیقاً همان route | سازمان کلاس |
| Manager School Summary | `managers/school-summary` | `managers/school-summary` | Manager و سازمان جاری | NotApplicable؛ aggregate | سازمان جاری |
| Manager Class Insight | `managers/classes/{classId}/insight` | `managers/class-insight` | Manager، Class همان سازمان | NotApplicable؛ aggregate | سازمان کلاس |
| Manager Student Insight | `managers/classes/{classId}/students/{studentId}/insight` | `managers/student-insight` | Manager، Class و enrollment همان سازمان | NotApplicable؛ گزارش دانش‌آموز | سازمان کلاس |

در baseline این ۱۲ Use Case مستقیماً Client را فراخوانی می‌کردند؛ Commerce preflight/reservation موجود بود اما coordinator سراسری برای invocation آن‌ها وجود نداشت. این مرحله رفتار source-less را به coordinator تجاری جدید منتقل نکرده است. بنابراین این سند ادعا نمی‌کند همهٔ ۱۲ مسیر از قبل metered بوده‌اند.

## مسیر واقعی و مرز Onion

`AiController` فقط HTTP را به Use Case می‌فرستد. `AiChatService` و سه مسیر مرتبط `AiRoleContextService` ابتدا input/context را بررسی کرده و `IAiSourceSelectionService.GuardAsync` را صدا می‌زنند. مجموعهٔ خالی بدون OperationId مستقیماً به مسیر قبلی بازمی‌گردد.

برای انتخاب غیرخالی:

1. `AiSourceSelection` حداکثر ۳۲ selector ورودی، نبود Guid.Empty، dedupe و ترتیب ثابت را اعمال می‌کند.
2. `AiSourceSelectionService` با resolver حساب و workspace، و builderهای Student/Teacher، Context سروری می‌سازد. AssignmentId/ClassId فقط selector هستند.
3. `EfAiSourceAuthorizationService` از ACL جاری `IEducationalSourceStore` استفاده می‌کند؛ `AiSourcePolicy` binding و readiness را بررسی می‌کند.
4. نسخهٔ Published قابل استفاده به `(SourceId, Number)` pin می‌شود. مدل واقعی SourceVersion شناسهٔ مستقل Guid ندارد؛ این زوج همان identity مرکب است.
5. `EfAiSourceSelectionStore` فقط fingerprint و pin را اتمیک ثبت/بازپخش می‌کند.
6. نشست، Context، رابطه و ACL دوباره خوانده می‌شوند؛ pin قبلی دوباره بررسی می‌شود.
7. `AuthorizedAiRetrievalPlan.RetrievalAvailable=false` است؛ پاسخ `409 AI_SOURCE_RETRIEVAL_NOT_AVAILABLE` پیش از Commerce و Provider صادر می‌شود.

Domain شامل invariantهای سابقهٔ انتخاب است؛ Application شامل قرارداد، policy، canonicalization و orchestration؛ Infrastructure شامل EF/SQL و reuse ACL؛ Api شامل route و ProblemDetails است. Application هیچ EF dependency نگرفته است.

Preflight جدید فقط خواندنی است و برای Chat در Personal context هم انتخاب شخصی را بررسی می‌کند. این به معنی اضافه‌شدن personal Chat inference به Contract v1 نیست؛ Chat بدون منبع همچنان محدودیت سازمانی baseline را دارد و Chat دارای منبع همیشه قبل از inference متوقف می‌شود.

## ACL و readiness

Personal فقط Owner است؛ Teacher/Manager یا payer مجوز منبع شخصی دیگران نیست. Class فقط مخاطب مجاز فعلی مطابق School ACL و همان Class context است؛ داشتن TeacherAssignment در دو کلاس مجوز مخلوط‌کردن آن‌ها نیست. Organization فقط همان سازمان جاری و ACL موجود است؛ در Foundation فعلی خواندن منبع سازمانی Manager-only است. نقش معتبر در سازمان دوم نیز مجوز واردکردن دادهٔ آن به request سازمان اول نیست.

Library حتی برای Platform Owner مجوز AI ندارد. `canUseForAi=false` و `aiUnavailableReason=ProductPolicyDisabled` است؛ metadata کاتالوگ Published/Public و محدودیت دانلود عادی حفظ شده‌اند. GuardianRelation دسترسی به منابع شخصی یا چت کودک ایجاد نمی‌کند.

انتخاب فقط از جدول EducationalSources انجام می‌شود. FileAsset، پاسخ Assignment/Exam، attachment پاسخ، answer key، private grade/feedback/note و Official Activity Card در این namespace منبع قابل انتخاب نیستند. گروه/کانال کارت منبع جدید تولید نمی‌کنند.

Draft، Archived/Inactive، فایل آماده‌نشده، Rejected/ReviewRequired و processing نوع Pending/Processing/Failed قابل استفاده نیستند. فقط نسخهٔ Published با SourceReady واقعی وارد manifest می‌شود؛ حتی Processing=Ready در دادهٔ آزمون، Retrieval واقعی ایجاد نمی‌کند. FileReady به معنی AiReady نیست. همهٔ `AiReady` و `CanUseForAi`ها در این Foundation false باقی می‌مانند.

## Manifest، نسخه و concurrency

`AuthorizedAiSourceManifest` شامل identity مرکب نسخه، Scope، OwnerId، title، readiness و AuthorizationRevision است؛ filepath، storage key، اتصال، token و محتوا ندارد. title دادهٔ غیرقابل اعتماد است و مجوز/tool/payer نمی‌سازد. AuthorizationRevision از rowversion منبع و نسخه ساخته می‌شود؛ گواهی اعتبار ACL یا bearer capability نیست. ACL همیشه دوباره خوانده می‌شود.

`knowledge.AiSourceSelectionOperations` کلید مرکب `(UserId, OperationId)`، Hash باینری ۳۲ بایتی و UTC دارد. `knowledge.AiSourceSelectionPins` کلید `(UserId, OperationId, SourceId)` و FK محدود به SourceVersion مرکب دارد. قفل User با `UPDLOCK,HOLDLOCK` ایجاد هم‌زمان را serialize می‌کند؛ متن request ذخیره نمی‌شود. این جدول quota ledger یا audit موازی نیست؛ تنها نگه‌دارندهٔ identity درخواست است. EF اجازهٔ تغییر/حذف سابقه را نمی‌دهد؛ Down بعد از استفاده fail-closed است.

Migration افزودنی: `20261030152943_AddAiSourceSelectionPins`؛ فقط دو جدول و index/FK/check اضافه می‌کند. دلیل persistence، تشخیص پایدار Operation conflict و حفظ V1 در retry بعد از Publish V2 است؛ cache حافظه‌ای این تضمین را نمی‌دهد. هیچ دادهٔ قدیمی backfill یا هیچ schema سرور تغییر نکرده است.

Hash شامل payload نرمال‌شدهٔ همان عملیات، مجموعهٔ canonical، User، workspace/org/membership/role/subject، Class/Assignment و نوع عملیات است. CorrelationId، session تازه و نسخهٔ Published جدید Hash را تغییر نمی‌دهند؛ retry همان intent نسخهٔ pin‌شده را حفظ می‌کند. تغییر source set یا payload/context برای همان OperationId → `409 AI_SOURCE_OPERATION_CONFLICT`. حذف تمام selectors با OperationId انتخاب قبلی نیز Conflict است. درخواست تازه بعد از Publish V2، V2 را resolve می‌کند. تاریخچه مجوز نیست؛ revoke/Archive/unready بودن pin باعث deny می‌شود.

## Commerce و سهمیه

پرداخت‌کننده مستقل از مجوز آموزشی است. قواعد موجود `CommercialSubscriptionService` و `EfCommercialStore` برای Entitlement، انتخاب صریح payer، AI quota، Reserve/Finalize و FreeTrial سه موفقیت روزانه با مرز تهران حفظ شده‌اند؛ fallback جدید وجود ندارد.

در این مرحله retrieval ناموجود است، پس source-aware execution به resolve payer یا reserve نمی‌رسد. Preflight وضعیت `NotEvaluatedRetrievalUnavailable` می‌دهد؛ این به معنی داشتن entitlement یا quota نیست. انتخاب نامجاز نیز قبل از Commerce رد می‌شود. Reservation count، موفقیت مصرف AI و تغییر SourceCount در هر دو حالت صفرند. ایجاد Source طبق Foundation مستقل از انتخاب آن سهمیهٔ ظرفیت می‌گیرد؛ archive نیز release تخمینی نمی‌سازد.

برای آینده، همین Hash canonical باید به Reserve موجود داده شود و نتیجهٔ قطعی مجری به Finalize موجود برود. اتصال به execution واقعی، تعیین قرارداد source/RAG و coordinator invocation خارج از این مرحله است؛ سامانهٔ موازی quota ساخته نشده است.

## امنیت، خطا و Observability

منبع ناموجود و غیرقابل مشاهده هر دو `404 AI_SOURCE_NOT_FOUND` دارند. `409 AI_SOURCE_CONTEXT_MISMATCH` فقط پس از احراز خواندن منبع و برای binding ناسازگار، `409 AI_SOURCE_NOT_READY` برای lifecycle/readiness، `403 AI_SOURCE_NOT_ALLOWED` برای Library مجاز به مشاهده، و `400 AI_SOURCE_REQUEST_INVALID` برای selector/OperationId نامعتبر است. خطاهای AI قبلی دست‌نخورده‌اند.

ProblemDetails و Correlation موجود reuse شده‌اند؛ Exception پیام ثابت امن دارد. Prompt، محتوای منبع/پاسخ دانش‌آموز، JWT، Service secret، credential و مسیر فیزیکی log نمی‌شوند. محتوای آموزشی هرگز authority برای تغییر Role، payer یا policy نیست؛ اکنون اصلاً خوانده یا به AI ارسال نمی‌شود. برای request متوقف‌شده، AI requestId یا modelAlias ساختگی تولید نمی‌شود.

## تست و محدودیت

`AiSourceSelectionTests` canonical/hash/plan/recheck و preflight بدون write را بررسی می‌کند؛ `AiSourcePolicyTests` binding و state را به‌صورت Unit پوشش می‌دهد. `AiSourceSelectionEndpointsTests` با SQL Server واقعی و fake Client شمارش‌دار، owner/outsider، سازمان/کلاس، revoke واقعی بین Pin و execution، replay/concurrent operation، نسخهٔ V1/V2 و مصرف صفر را بررسی می‌کند. Regressionهای JWT/header/grounding، Source، Commerce، Academic attachments، Official Spaces و Realtime موجود نیز باید اجرا شوند.

E2E محلی از Kestrel، LocalDB موقت، Mock OTP تست و Provider trap روی loopback استفاده می‌کند؛ لاگ فقط route/status/error/correlation/elapsed و نتایج امن دارد. این تست پذیرش Production AI نیست. نتیجهٔ دقیق و SHA در گزارش اجرای همان مرحله ثبت می‌شود.

Deferred: استخراج متن، OCR، chunking، embedding، vector DB، RAG retrieval واقعی، قرارداد Source با AI Team و فعال‌سازی AI Runtime. هیچ publish/deploy یا Migration روی `MakanApp`، `MakanApp_Test` یا سرور در این مرحله انجام نشده است.
