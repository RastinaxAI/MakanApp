# گزارش توسعه‌دهنده — بازبینی رسانه و تکمیل تحویل فایل

تاریخ `2026-11-22`؛ مبنا `37757c78c987e2d2a3770188474af13cd9d2244c`؛ Branch: `feature/product-v6-backend-alignment`. شناسهٔ نهایی Commit و نتیجهٔ Push در Manifest خارجی Release و گزارش نهایی ثبت می‌شود؛ سند tracked نمی‌تواند Hash خودش را در همان Commit دربر بگیرد. دو دور Full Regression و کنترل SQL همین نسخه موفق شده‌اند؛ شواهد اجرایی خصوصی و نتایج خلاصهٔ زیر مرجع Validation هستند.

## ۱. خلاصه مرحله

Upload صحیح قبلاً فایل Ready می‌ساخت ولی موتور متن، همهٔ رسانه‌ها را unsupported و ReviewRequired می‌دانست. بستن پروندهٔ قبلی نیز تأیید محتوا نبود؛ پس PDF/Image به پیام و دانلود گیرنده نمی‌رسید. درخواست جدید، بازبینی انسانی را صریحاً تصویب کرد. اکنون پروندهٔ واقعی و یکتای فایل، تصمیم صریح کارشناس، خواندن وضعیت مالک و Send مجدد همان فایل فراهم است؛ هیچ Safe فرضی یا خاموش‌کردن Safety وجود ندارد.

Open Access، Commerce Enforcement خاموش و AI نامحدود محفوظ‌اند. نه سن جعلی ثبت شده و نه Subscription/Quota جدیدی ساخته شده است.

## ۲. معماری Onion

Domain، شکل پرونده و تصمیم immutable را می‌شناسد و به EF/HTTP وابسته نیست. Application پورت `IMediaSafetyProvider`، قرارداد وضعیت و Use Case خواندن مالک را دارد. Infrastructure تصمیم SQL، تطبیق Hash، transaction/locking و ترکیب موتور متن با تصمیم رسانه را اجرا می‌کند. Api فقط ورودی HTTP، نشست، ProblemDetails، DI و OpenAPI را نگه می‌دارد. سیستم همان Modular Monolith با .NET9/EF9/SQL Server است.

وابستگی: `Api → Application → Domain`؛ پیاده‌سازی Portها در Infrastructure از Application/Domain استفاده می‌کند. Controller هیچ Query یا شرط Safety دامنه ندارد.

## ۳. فایل‌های مهم جدید

| مسیر | مسئولیت، دلیل ایجاد و نکتهٔ یادگیری |
|---|---|
| `MakanApp.Application/Messaging/MediaSafetyContracts.cs` | پورت رسانه، نتیجه و Use Case `MediaSafetyService`؛ مجوز دانلود گیرنده با حق مشاهدهٔ وضعیت خصوصی Uploader یکسان نیست |
| `MakanApp.Infrastructure/Messaging/ManualMediaSafetyProvider.cs` | خواندن تصمیم ثبت‌شده؛ Allowed فقط با اثرانگشت همان فایل معتبر است، نه صرف وجود پرونده |
| `MakanApp.Infrastructure/Messaging/ReviewedMediaContentSafetyProvider.cs` | ترکیب caption متنی و تصمیم هر فایل؛ Approval فایل، متن ناامن را تأیید نمی‌کند |
| `MakanApp.Infrastructure/Messaging/EfMessagingStore.MediaReviews.cs` | یک Case به‌ازای فایل، ثبت Evidence/Incident، بازبینی مجاز Retry و کنترل نهایی قبل از Send |
| `MakanApp.Infrastructure/Platform/EfPlatformEducationSafetyStore.MediaReviews.cs` | Hint غیرمجوزدهنده برای ترتیب قفل یکسان و نمایش وضعیت در جزئیات Case |
| `MakanApp.Infrastructure/Persistence/Migrations/20261122085930_AddMessagingMediaReviews.cs` و `.Designer.cs` | schema افزایشی بازبینی؛ State تصمیم باید پایدار و تحت قیود SQL باشد |
| `tests/MakanApp.UnitTests/Messaging/MediaSafetyReviewTests.cs` | دوازده حالت خالص Domain/provider |
| `tests/MakanApp.IntegrationTests/MessagingMediaReviewAcceptanceTests.cs` | نوزده سناریوی SQL/API/SignalR/ACL واقعی محلی |
| `docs/messaging/MEDIA_SAFETY_REVIEW_FRONTEND_HANDOFF_FA.md` | قرارداد کاربردی Upload تا تصمیم و دانلود، بدون تغییر Frontend |

## ۴. فایل‌های مهم تغییرکرده

- `ModerationCase.cs` و enumهای Source/Action: Source جدید MediaReview، snapshot فایل و تصمیم یک‌باره؛ Resolve/Dismiss مجوز ضمنی ایجاد نمی‌کنند.
- `ModerationCaseConfiguration.cs`، `ModerationActionConfiguration.cs` و Snapshot: FK، unique filtered index، CHECKهای شکل و تصمیم؛ rowversion موجود reuse شده است.
- `EfMessagingStore.ChildSafety.cs`: تشخیص محدود Incident تاریخیِ unsupported و ارزیابی مجدد؛ Hash درخواست همچنان ثابت است. XLOCK فایل پیش از خواندن Serializable از deadlock تبدیل S→U جلوگیری می‌کند. لاگ فقط هنگام نتیجهٔ نهایی همان تلاش ثبت می‌شود.
- `EfMessagingStore.Advanced.Send.cs`: قبل از Persist، تصمیم جاری و اثرانگشت پرونده دوباره چک می‌شوند.
- `EfPlatformEducationSafetyStore.cs` و `.ChildSafety.cs`: reuse صف، Claim، Evidence، action و Audit موجود؛ تصمیم رسانه نیازمند Review+Enforce و Reviewer واگذارشده است، self-approval ممنوع است.
- `EfFileAssetBoundAccessResolver.cs` و `FileAssetBindingQuery.cs`: مالکِ فایل در صف دسترسی محدود خود را حفظ می‌کند؛ binding ماژول دیگر/Evidence نامرتبط، راه دورزدن ACL نیست. خود Case نیز مرجع retention/cleanup محسوب می‌شود.
- `MessageContentSafetyGate.cs`، `ContentSafetyAbstractions.cs` و `MessagingException.cs`: نتیجهٔ واقعی مسیر رسانه و پاسخ قابل اقدام؛ رقابت Approval با نتیجهٔ Pending قدیمی، دوباره در SQL حل می‌شود.
- `PlatformEducationSafetyContracts.cs` و `PlatformEducationSafetyService.cs`: فیلد افزایشی `mediaReview` و پذیرش actionهای صریح در Use Case موجود.
- `DependencyInjection.cs`: Internal اکنون Provider ترکیبی و Media Provider scoped را مصرف می‌کند؛ تنظیم امن/قدیمی Unavailable یا DevelopmentAllow تغییر نکرده است.
- `FilesController.cs`، `GlobalExceptionHandler.cs` و `FileAndMessagingOperationTransformer.cs`: GET وضعیت مالک، افزودهٔ امن ProblemDetails و قرارداد OpenAPI.
- اسناد BE-01، Open Access، API Guide/Reference، قرارداد Frontend و دفتر موانع/تصمیم‌ها: تصمیم مسدود تاریخی با workflow انسانی مصوب جایگزین شده؛ ادعای استقرار اضافه نشده است.

## ۵. جریان اجرا با نام کلاس واقعی

```text
HTTP Upload → FilesController → StorageService → FileAsset → EF/Blob → Ready
HTTP Send → ConversationsController → MessagingService
→ MessageContentSafetyGate → ReviewedMediaContentSafetyProvider
→ InternalMakanContentSafetyProvider(caption) + ManualMediaSafetyProvider(file)
→ EfMessagingStore.RecordSafetyIncidentAsync → RecordMediaReviewOutcomeAsync
→ ModerationCase.CreateForMediaReview + Evidence + SafetyIncident → SQL Commit
→ 422 PendingReview، بدون Message

HTTP Claim/Decision → PlatformModerationController → PlatformEducationSafetyService
→ EfPlatformEducationSafetyStore → ModerationCase.DecideMedia
→ SQL + ModerationAction + AdminAudit → Approved/Rejected

HTTP Retry همان Send → همان Safety Gate + مجوز جاری
→ EfMessagingStore.SendMessageAsync → Message/Attachment + Outbox → Commit
→ Dispatcher/Hub موجود → ConversationChanged → REST Delta/History
→ FilesController.Download → StorageService + ACL Binding → bytes گیرنده
```

نام متدهای Store از پیاده‌سازی واقعی است؛ payload خصوصی داخل Event قرار نمی‌گیرد. تأیید فایل خودش پیام نمی‌فرستد.

## ۶. دیتابیس

یک Migration: `20261122085930_AddMessagingMediaReviews`. جدول تازه‌ای ساخته نشده؛ هفت ستون nullable به `messaging.ModerationCases` افزوده شده‌اند: `MediaFileAssetId`, `MediaSha256Hash`, `MediaContentType`, `MediaSizeBytes`, `MediaDecision`, `MediaReviewedByUserId`, `MediaReviewedAtUtc`.

PK قبلی Case حفظ شده؛ FKهای فایل و Reviewer به `storage.FileAssets` و `identity.Users` با Restrict وصل‌اند. `UX_ModerationCases_MediaFileAssetId` فقط برای مقدار غیرNULL یکتا است؛ پرونده‌های قدیمی NULL می‌مانند. CHECKهای Source/SourceShape/MediaReview، شکل دقیق سه نوع پرونده، اثرانگشت معتبر، اندازهٔ مثبت و تصمیم/Reviewer/Time معتبر را تحمیل می‌کنند. `IS NOT NULL` صریح برای جلوگیری از عبور SQL UNKNOWN لازم است. Action Typeهای9/10 افزوده شده‌اند. هیچ Backfill خودکارِ Approval انجام نمی‌شود.

rowversion موجود برای optimistic concurrency استفاده می‌شود. ایجاد/تصمیم و Send transaction کوتاه دارند؛ provider متن/رسانه قبل از transaction فراخوانی می‌شود و نتیجهٔ رسانه داخل transaction دوباره بررسی می‌شود. ترتیب مشترک قفل گفتگو → Fileهای مرتب → Case است. Migration صرفاً روی SQL موقت ایزولهٔ تست اعمال می‌شود؛ روی MakanApp، MakanApp_Test یا سرور اجرا نمی‌شود. Down روی پروندهٔ Source جدید جای rollback عملیاتی نیست؛ Backup و نسخهٔ سازگار باید مبنا باشند.

## ۷. Endpointها

تنها Route جدید `GET /api/v1/files/{fileId}/messaging-review` است: Bearer/نشست جاری، Uploader و Scope معتبر؛ پاسخ200 با FileAssetId/State/AttachmentAllowed/ReviewReference/ReviewedAtUtc، نامجاز404. GET هیچ Case ایجاد نمی‌کند.

Upload، Send، Metadata/Content و Routeهای `/api/admin/v1/moderation/cases` موجود reuse شده‌اند. تصمیم از `POST .../{caseId}/actions` با ExpectedVersion/Reason/Action می‌آید؛ enumهای افزایشی MediaApproved/MediaRejected هستند. خواندن/Claim نیازمند ModerationReview؛ تصمیم افزون بر آن ModerationEnforce و Reviewer جاری و غیرمالک لازم دارد. Version قدیمی409 و عدم اختیار403/Concealment موجود حفظ‌اند. Pending و Rejected در Send422، Unavailable503، payload متفاوت همان عملیات409 می‌دهند. جزئیات هر Request/Response و UX در Handoff همین مرحله آمده است.

## ۸. مفاهیم فنی استفاده‌شده

Composition به‌جای جایگزینی موتور متن، Port/Adapter برای رسانه، optimistic concurrency با rowversion، locking ترتیب‌دار SQL Server، unique filtered index، CHECK سه‌ارزشی SQL، snapshot اثرانگشت، idempotency، current-state authorization و transactional outbox موجود به‌کار رفته‌اند. MediatR/CQRS/Generic Repository، queue دوم یا دیتابیس جدیدی اضافه نشده است.

## ۹. تصمیم‌های طراحی و Trade-off

قرار دادن تصمیم روی FileAsset، توسعهٔ ModerationCase یا ساخت subsystem مستقل ممکن بود. Case موجود انتخاب شد چون صف، Reviewer، Evidence، Audit و concurrency از قبل داشت؛ هفت ستون nullable از تغییر معنای FileAsset.Status و ساخت صف دوم جلوگیری می‌کند. هزینه، نیاز به اپراتور دارای مجوز و انتظار انسانی است؛ SLA خودکار یا اسکن بایت نداریم.

یک Case جهانی برای هر FileAsset، از تکثیر کار کارشناس در گفتگوهای مختلف جلوگیری می‌کند؛ مجوز هر Send همچنان مستقل است. Approval به اثرانگشت immutable بسته است و تصمیم نهایی بازنویسی نمی‌شود. Snapshot قدیمیِ SafetyIncident برای ممیزی حفظ می‌شود؛ موفقیت جدید از Case صریح می‌آید، نه دستکاری تصمیم تاریخی.

## ۱۰. تست‌ها

۱۲ Unit: عدم Resolve/Dismiss Pending، Reviewer واگذارشده و تصمیم نهایی یک‌باره، تطبیق فایل/Hash/MIME/Size، ورودی fingerprint نامعتبر، چهار نتیجهٔ واقعی Provider و caption ناامن.

۱۹ Integration: شش مسیر PDF/JPEG/PNG × caption/فایل‌تنها با نام فارسی، Upload/Ready، عدم دانلود قبل از تصمیم، تصمیم API پلتفرم، ارسال همان فایل، SignalR invalidation، Delta/History، دانلود بایت‌به‌بایت؛ سپس رقابت دو گفتگو و پروندهٔ یکتا، رد، مجوز Enforce، self-approval و Version کهنه، constraint SQL، cross-org، چندفایلی، رقابت Approval/Send، binding ماژول دیگر، revoke نقش و تغییر اثرانگشت. همه از SQL Server ایزوله و Provider اصلیِ ترکیبی استفاده می‌کنند. معماری با16 تست موجود کنترل می‌شود؛ عدد Architecture از تست نمایشی جدید افزایش نیافته است.

در تست اولیه،404 مالک پس از Evidence-binding شناسایی و با fallback محدود owner حل شد؛ سپس deadlock واقعی SQL در ارسال هم‌زمان چندگفتگو با ترتیب XLOCK ثابت اصلاح شد. رگرسیون بعدی ثبت دوبارهٔ لاگ همان Incident را پیدا کرد؛ logging در preflightِ نیازمند ارزیابی مجدد حذف شد و نتیجه فقط یک بار ثبت می‌شود. هیچ assertion حذف یا تستی disable نشده است. شواهد اجراهای ناموفق خصوصی حفظ‌اند.

## ۱۱. نتیجهٔ Validation

نتیجهٔ واقعی: Clean و Restore موفق؛ Build Release و Final Build هر دو موفق با Warning=0 و Error=0. Focused Integration برابر1014/1014 موفق و هر دو Full Regression برابر3653/3653 موفق‌اند: Unit=1467، Integration=2170 و Architecture=16؛ Failed=0 و Skipped=0. تست جدید31 مورد است:12 Unit و19 Integration. هفت تست تاریخی BE-01 پس از اصلاح logging نیز مستقل موفق شدند.

`dotnet ef migrations has-pending-model-changes` با Startup پروژهٔ Infrastructure و بدون اتصال به دیتابیس، تغییر Pending ندارد. SQL کامل78 Migration، هشت سناریوی واقعیِ نصب تازه/upgrade/retry و قیود را در LocalDBهای تازه و ایزوله گذرانده و Hash اسکریپت اجراشده دقیقاً با فایل تحویلی یکی است؛ فقط دیتابیس‌های موقتی ساخته‌شدهٔ همان ابزار پاک شده‌اند. روی MakanApp، MakanApp_Test یا سرور Migration اجرا نشده است. این8 بررسی SQL جدا از3653 تست Suite گزارش می‌شوند.

پوشش رگرسیون شامل File/Storage، Media Review، Moderation، Safety/Block/Authorization، Academic Attachment، Realtime/Outbox/SQL isolation، AI/Knowledge و Open Access است؛ گروه‌ها هم‌پوشانی دارند و به تعداد کل اضافه نمی‌شوند. مسیر واقعی فایل با Provider ترکیبی اصلی و تصمیم API پلتفرم روی میزبان SQL/API/SignalR محلی اثبات شده است؛ پذیرش IIS مقصد، اپراتور انسانی واقعی یا AI Production از این نتیجه استنتاج نمی‌شود.

## ۱۲. Git

پیام موردنظر: `fix(files): complete media safety review and attachment delivery`؛ فقط فایل‌های همین مرحله stage می‌شوند. سه اسکریپت AI از پیش Untracked و تمام13 فایل محافظت‌شده با SHA256 baseline/backup مقایسه می‌شوند. Config خصوصی، OTP/MeliPayamak، Publish Profile، SQL isolation و Frontend تغییر نمی‌کنند. Commit/Push/Fetch verification فقط بعد از Validation موفق انجام و Hash واقعی در Manifest و پاسخ نهایی درج می‌شود؛ force-push/merge به main مجاز نیست.

## ۱۳. محدودیت‌های فعلی

موتور خودکار رسانه، SLA/تخصیص خودکار اپراتور، Reopen تصمیم نهایی و انتشار خودکار پس از تأیید ساخته نشده‌اند. متن همچنان قواعد محدود خودش را دارد. Approval پیام، Academic/RAG/Avatar/Library را تأیید نمی‌کند. تنظیم AI/Knowledge Production خودسرانه فعال نمی‌شود. هیچ اتصال مدیریتی، Migration اصلی یا Deploy انجام نمی‌شود.

## ۱۴. نکات آموزشی

1. Ready Storage، تصمیم Safety و Authorization سه مفهوم مستقل‌اند.
2. Idempotency در SQL با unique index/transaction کامل می‌شود؛ check قبل از write کافی نیست.
3. تأیید رسانه باید به محتوای دقیق بسته باشد؛ FileId تنها برای تصمیم امنیتی کافی نیست.
4. نقش دیروز یا Claim پرونده، جای مجوز جاری هنگام Action/Send را نمی‌گیرد.
5. SQL CHECK با NULL می‌تواند UNKNOWN شود؛ IS NOT NULL برای invariant مهم صریح باشد.
6. ترتیب یکسان قفل در Send و Moderation، deadlock را با طراحی حل می‌کند؛ افزایش timeout یا retry علاج اصلی نیست.
7. تست واقعی باید مسیر گیرنده و بایت دانلودی را نیز اثبات کند؛201 Upload به‌تنهایی Delivery را ثابت نمی‌کند.

## ۱۵. مسیر پیشنهادی مطالعه

`FilesController` و `PlatformModerationController` → `MediaSafetyService` و `MessageContentSafetyGate` → `ModerationCase` → `IMediaSafetyProvider` → `ReviewedMediaContentSafetyProvider` و `ManualMediaSafetyProvider` → `EfMessagingStore.MediaReviews` و `EfPlatformEducationSafetyStore.MediaReviews` → Binding Resolver → EF Config/Migration → Unit → Integration Acceptance → Handoff کلاینت.

## ۱۶. مرحلهٔ بعد

نصب دستی بستهٔ Test پس از Backup/Migration بازبینی‌شده و پذیرش با کارشناس پلتفرمِ دارای Review+Enforce؛ سپس بررسی Health، صف واقعی، Pending/Approval/Rejected و SignalR/Download در IIS مقصد. انتخاب Provider خودکار، SLA عملیاتی یا گسترش Knowledge باید Task جدا باشند.
