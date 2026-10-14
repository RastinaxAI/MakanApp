# MakanApp Non-AI Backend Final Gap Audit

تاریخ ممیزی: ۲۰۲۶-۱۰-۱۵، منطقه زمانی `Asia/Tehran`. دامنه: فقط Backend فعلی؛ بدون تغییر محصول، تست، migration، معماری، Frontend یا AI.

به‌روزرسانی محدود پس از ممیزی، ۲۰۲۶-۱۰-۱۵: **GAP-01 در کد بسته شد**. شرح ممیزی و اعداد baseline در بخش‌های 2 و 3 تاریخی باقی مانده‌اند؛ فقط شواهد، وابستگی‌ها و نتیجه GAP-01 در این نسخه اصلاح شده‌اند. [سند پیاده‌سازی و قراردادها](../architecture/GAP_01_ORGANIZATION_PERSON_GUARDIAN_ONBOARDING.md) مرجع جزئیات است. این closure تأیید Production و تغییر اولویت سایر gapها نیست.

به‌روزرسانی محدود GAP-02، همان تاریخ: آماده‌سازی Staging و آزمون محلی schema/publish/startup انجام شد، اما **GAP-02 باز و BLOCKED_OPERATIONAL_VALIDATION است**. مالک نبود HTTPS مقصد را تأیید کرد؛ `/health/ready` آن 404 بود. هیچ OTP/token روی HTTP ارسال نشد. جزئیات و تفکیک PASS/FAIL/NOT_EXECUTED/BLOCKED در [شواهد GAP-02](GAP_02_STAGING_CORE_SMOKE_VALIDATION.md) آمده‌اند؛ اولویت سایر gapها و verdict انتشار تغییر نکرده‌اند.

## 1. Executive Summary

**Backend از یک foundation ساده بسیار جلوتر است، اما برای انتشار عمومی غیر AI هنوز `NOT_READY` است.** شماره STEP معیار تکمیل نیست. بخش عمده قواعد آموزشی، ارزیابی، پیام‌رسان، مجوز و گزارش واقعاً اجرا و تست می‌شوند؛ onboarding فراگیر/والد با GAP-01 از حالت fixture-only خارج شده، اما چند قابلیت به adapterهای unavailable متصل‌اند.

- سلامت worktree ممیزی‌شده: restore و build موفق؛ build بدون warning/error؛ **۹۷۷ تست موفق، صفر failed و صفر skipped**. این عدد شامل تست‌های Fake/Recording AI موجود نیز هست؛ هیچ AI یا provider خارجی زنده فراخوانی نشد.
- GAP-01 بسته شد: API مدرسه برای ساخت/get/search/update/end پرونده، دعوت اتصال حساب با رضایت صریح و create/activate/end/revoke رابطه والد اضافه شد؛ خود onboarding و ثبت‌نام از API در تست اجرا می‌شوند. نام Person حساب‌دار/مشترک از مدرسه قابل ویرایش نیست؛ حساب یا سابقه‌ای merge/حذف نمی‌شود. شواهد E27 و بخش 20.
- OTP ملی‌پیامک adapter واقعی دارد، اما نمونه Production به `Unavailable` متصل است و مدرک نسخه‌بندی‌شده smoke کامل ارسال واقعی و verify موفق در محیط هدف پیدا نشد. ادعای ارسال/تست دستی در گذشته با مدرک عملیاتی قابل بازتولید یکسان نیست.
- MFA/step-up، پرداخت واقعی، Push، SMS عمومی اعلان و Content Safety واقعی متصل نیستند. fail-closed بودن به معنی bypass امنیتی نیست؛ به معنی غیرقابل‌استفاده بودن مسیرهای مربوط است.
- ذخیره‌سازی runtime واقعاً `LocalFileStorage` است. Object Storage برای پایلوت تک‌سروری الزام ذاتی ندارد؛ دیسک پایدار، ACL، ظرفیت، backup و restore الزام دارند.
- export واقعی CSV است؛ PDF/Excel تولید نمی‌شود. صف، فایل و مجوز مجدد دانلود وجود دارند، اما expiration/purge خودکار وجود ندارد.
- Entitlement/Quota صرفاً entity نیست: resolve، محاسبه usage و ثبت metered usage وجود دارند؛ بااین‌حال consumer عملیاتی آن‌ها در ایجاد کلاس/Enrollment/upload/export پیدا نشد. محدودیت ظرفیت کلاس با quota تجاری اشتباه نشود.
- hardening واقعی HTTP و session وجود دارد؛ deployment واقعی IIS، پایش بیرونی، recovery کامل DB+storage و baseline بار اثبات نشده‌اند. CI/CD در فایل‌های مخزن وجود ندارد.

| حالت | حکم فعلی | معنی |
|---|---|---|
| MODE A — Internal Development | `READY` | برای توسعه و تست ایزوله با providerهای تست؛ نه اثبات عملکرد provider Production |
| MODE B — Controlled Pilot | `NOT_READY` | GAP-01 در کد بسته شده؛ gate محیط هدف و سایر شروط بخش 17 هنوز بازند |
| MODE C — General Production | `NOT_READY` | workflow، provider و شواهد عملیاتی الزامی هنوز کامل نیستند |

این سند certification امنیت، ممیزی زیرساخت سرور یا تضمین نبود همه باگ‌ها نیست. «شاهد پیدا نشد» به معنی مشاهده نکردن مدرک در مخزن است، نه ادعای اینکه فعالیتی خارج مخزن هرگز انجام نشده است.

## 2. Repository / Commit audited

| مورد | مقدار |
|---|---|
| Repository | `D:\Project\MakanApp\MakanApp-Backend` |
| Solution | `MakanApp.sln` |
| Branch / upstream | `main` / `origin/main` |
| HEAD پیش از ممیزی | `20ba75dcbd19210e78772cd3e388c52b85a00893` |
| local tracking ref در شروع | `origin/main = 20ba75dcbd19210e78772cd3e388c52b85a00893` |
| Remote | `origin`؛ `https://github.com/RastinaxAI/MakanApp.git` |
| آخرین تغییر محصول | `feat(reports): add role-based operational dashboards` |

مبنای source inspection، worktree موجود روی این HEAD است، نه clean checkout فرضی. سه فایل tracked زیر از پیش تغییر داشتند:

```text
MakanApp.Infrastructure/Ai/AiServiceClient.cs
tests/MakanApp.IntegrationTests/AiChatEndpointsTests.cs
tests/MakanApp.UnitTests/Ai/AiServiceClientTests.cs
```

هشت فایل untracked از پیش موجود:

```text
docs/ai-handoff/10_BACKEND_REAL_E2E_GATE.md
docs/ai-handoff/11_STEP10G_FAILURE_EVIDENCE_SANITIZED.md
tools/ai/MakanApp.AiIntegrationProbe/FailureEvidenceCapture.cs
tools/ai/MakanApp.AiIntegrationProbe/LiveAiObservation.cs
tools/ai/MakanApp.AiIntegrationProbe/LiveAiScenario.cs
tools/ai/MakanApp.AiIntegrationProbe/MakanApp.AiIntegrationProbe.csproj
tools/ai/MakanApp.AiIntegrationProbe/Program.cs
tools/ai/README.md
```

در شروع برای هر ۱۱ فایل SHA-256 در حافظه ثبت شد؛ مقایسه نهایی پس از validation نیز دست‌نخورده بودن هر ۱۱ فایل را تأیید کرد. این فایل‌ها stage، commit، stash، revert یا پاک نشدند. تنها خروجی این ممیزی همین سند است. الگوی عمومی `[Rr]elease/` در `.gitignore` پوشه مقصد را نادیده می‌گیرد؛ فقط همین فایل به‌صورت صریح با `git add -f -- docs/release/NON_AI_BACKEND_FINAL_GAP_AUDIT.md` قابل stage است و `.gitignore` تغییر نمی‌کند. این force-add محدود، force-push نیست. hash commit حامل سند و نتیجه push در گزارش نهایی می‌آیند؛ خود سند hash آینده خود را حدس نمی‌زند.

## 3. Audit methodology

ترتیب اعتبار شواهد: source جاری، تست جاری، migration، configuration و سپس مستندات. قواعد `AGENTS.md` و override SQL Server خوانده شدند؛ تعارض اسناد تاریخی به نفع کد فعلی گزارش شد، نه با تغییر خودسرانه محصول.

روش بررسی:

1. ثبت Git state و hash فایل‌های قبلی، بررسی history، solution، csproj، Controllerها و DI runtime.
2. دنبال‌کردن مسیر Controller → Application → Infrastructure و بررسی اینکه interface/entity واقعاً consumer دارد.
3. تطبیق قواعد tenant/resource با تست‌های مثبت/منفی، retry، concurrency، release و فایل.
4. بررسی چهار worker ثبت‌شده، claim/lease/retry، provider selection و نمونه Production.
5. بررسی migrationها، runbookهای SQL/IIS/recovery/load، publish profile و فایل‌های automation.
6. اجرای restore/build/test و بررسی pending model؛ بدون افزودن تست و بدون اصلاح حتی یک source file.

فرمان‌های validation واقعی:

```powershell
dotnet restore MakanApp.sln --nologo
dotnet build MakanApp.sln --configuration Debug --nologo --verbosity minimal
dotnet test MakanApp.sln --configuration Debug --nologo --verbosity minimal
dotnet ef migrations has-pending-model-changes --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --context MakanDbContext --configuration Debug --no-build
```

| مجموعه | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Unit | 380 | 0 | 0 |
| Integration | 583 | 0 | 0 |
| Architecture | 14 | 0 | 0 |
| مجموع | 977 | 0 | 0 |

restore موفق و dependencyها up-to-date بودند؛ build موفق با `0 Warning(s)` و `0 Error(s)`؛ EF گزارش داد مدل نسبت به آخرین migration تغییر ندارد. پس از ایجاد سند، final build و final test نیز دوباره موفق شدند: صفر warning/error و همان ۹۷۷ passed، صفر failed/skipped. این نتیجه، سلامت **worktree با تغییرات قبلی AI** است؛ صحت همان تغییرات به‌عنوان commit جدید این ممیزی ادعا نمی‌شود.

تست اصلی از دیتابیس اختصاصی LocalDB با نام `MakanApp_MessagingSafety_IntegrationTests_Step7E` استفاده می‌کند: `EnsureDeletedAsync → MigrateAsync → tests → EnsureDeletedAsync`. این lifecycle موجود fixture است، نه migration روی دیتابیس عادی توسعه/Production. providerهای OTP، payment، notification، safety و AI در fixture recording/fault-injecting هستند. بیشتر workerها برای تست پردازش قطعی از host تست حذف و processor مستقیم اجرا می‌شود؛ تست processor، جای آزمون restart/IIS واقعی را نمی‌گیرد.

### کاتالوگ شواهد

مسیرهای زیر نسبت به ریشه مخزن هستند. نام متد/کلاس برای پیدا کردن شاهد آمده است؛ وجود نام تست به‌تنهایی مدرک اجرای زنده provider نیست.

| شناسه | source/config اصلی | تست یا مدرک مکمل |
|---|---|---|
| E01 | `MakanApp.Infrastructure/DependencyInjection.cs`؛ ثبت concrete implementation و چهار `AddHostedService` | `MakanApp.sln` و csprojهای هفت پروژه |
| E02 | `MakanApp.Application/Identity/IdentityService.cs`؛ request/verify/refresh/logout؛ `MakanApp.Domain/Identity/OtpChallenge.cs` | `tests/MakanApp.IntegrationTests/IdentityEndpointsTests.cs`؛ قفل challenge، انقضا، ارقام محلی، replay/revoke |
| E03 | `MakanApp.Infrastructure/Identity/Otp/MeliPayamak/MeliPayamakOtpProvider.cs` و بخش `AddOtpDeliveryProvider` در E01 | `tests/MakanApp.UnitTests/Identity/MeliPayamakOtpProviderTests.cs`؛ response/error/timeout/log redaction با HTTP ساختگی |
| E04 | `MakanApp.Application/Organization/OrganizationService.cs`، `MakanApp.Infrastructure/Organization/EfOrganizationStore.cs` | `tests/MakanApp.IntegrationTests/OrganizationEndpointsTests.cs`؛ دعوت، نقش، workspace، پذیرش هم‌زمان |
| E05 | `MakanApp.Domain/Organization/OrganizationPerson.cs`؛ `MakanApp.Application/Academic/AcademicService.cs::EnrollLearnerAsync` | `tests/MakanApp.IntegrationTests/MakanAppWebApplicationFactory.OrganizationPerson.cs` و `OrganizationPersonPersistenceTests.cs` |
| E06 | `MakanApp.Application/Guardian/GuardianService.cs` و `GuardianAccessContextResolver.cs`؛ `MakanApp.Api/Controllers/GuardianController.cs` | `tests/MakanApp.IntegrationTests/GuardianEndpointsTests.cs`؛ fixture ایجاد/لغو رابطه را انجام می‌دهد |
| E07 | `MakanApp.Infrastructure/Academic/EfAcademicStore.cs`، `MakanApp.Application/Academic/AcademicSessionService.cs` | `AcademicEndpointsTests.cs` و `SessionAttendanceEndpointsTests.cs` در پروژه Integration؛ last-seat، composite FK، history و correction |
| E08 | `MakanApp.Application/Assessment/AssignmentService.cs`، `SubmissionService.cs`، `EvaluationService.cs` | `AssignmentEndpointsTests.cs`، `AssignmentAccessEndpointsTests.cs`، `SubmissionLifecycleEndpointsTests.cs`، `SubmissionConcurrencyEndpointsTests.cs`، `SubmissionSecurityEndpointsTests.cs`، `EvaluationLifecycleEndpointsTests.cs`، `EvaluationConcurrencyEndpointsTests.cs` و `EvaluationAuthorizationEndpointsTests.cs` |
| E09 | `MakanApp.Application/Storage/StorageService.cs`؛ `MakanApp.Infrastructure/Storage/LocalFileStorage.cs`، `FileSignatureValidator.cs`، `EfFileAssetBoundAccessResolver.cs` | `FileStorageEndpointsTests.cs`، `ProductionHostingTests.cs` و Unitهای Storage |
| E10 | `MakanApp.Application/Assessment/ExamService.cs`، `ExamAttemptService.cs`، `ExamAnswerService.cs`، `ExamFinalizationService.cs`، `ExamGradingService.cs` | Integrationهای `ExamEndpoints`، `ExamAccess`، `ExamAttempt`، `ExamAnswer`، `ExamFinalization` و `ExamGrading` |
| E11 | `MakanApp.Application/Messaging/MessagingService.cs`، `ConversationManagementService.cs`، `MessagingDirectoryService.cs`؛ partialهای `EfMessagingStore` | `MessagingEndpointsTests.cs` و مجموعه‌های Advanced، GroupChannel، Sync، BehaviorHardening، ContactsPrivacyDiscovery و Completion |
| E12 | `MakanApp.Infrastructure/Messaging/EfMessagingStore.ChildSafety.cs`؛ `CommunicationEligibilityPolicy.cs` و `ConversationManagementPolicy.cs` در Application/Messaging | `MessagingModerationChildSafetyEndpointsTests.cs`؛ Unknown/Minor، recheck پس از provider و evidence محدود |
| E13 | `MakanApp.Api/Realtime/MessagingHub.cs`، `SignalRMessagingRealtimeNotifier.cs`، `Authentication/SessionAuthenticationHandler.cs` و `Program.cs` | `MessagingRealtimeEndpointsTests.cs`، `MessagingSyncEndpointsTests.cs` |
| E14 | `MakanApp.Application/Reports/Dashboards/DashboardService.cs` و `MakanApp.Infrastructure/Reports/Dashboards/EfDashboardReadService*.cs` | `DashboardEndpointsTests.cs`، `.Hardening.cs` و Unit `Reports/DashboardServiceTests.cs` |
| E15 | `MakanApp.Application/Reports/Detailed/DetailedReportService*.cs` و `MakanApp.Infrastructure/Reports/Detailed/EfDetailedReportReadService*.cs` | detailed-report caseهای `DashboardEndpointsTests.cs` و `DetailedReportServiceTests.cs` |
| E16 | `MakanApp.Application/Reports/Exports/ReportExportService.cs`؛ `ReportExportDispatcher.cs`، `CsvReportExporter.cs`، `EfReportExportJobStore.cs` و `ReportExportOutputStore.cs` در Infrastructure/Reports/Exports | `ReportExportEndpointsTests.cs`؛ revocation download، exclusive claim و recovery؛ Unit CSV formula escaping |
| E17 | `MakanApp.Application/Notification/NotificationService.cs`، preference/delivery serviceها و `MakanApp.Infrastructure/Notification/NotificationDeliveryProviders.cs` | `NotificationEndpointsTests.cs`، `NotificationPreferenceEndpointsTests.cs` و Unit Notification |
| E18 | `MakanApp.Application/Commerce/CommerceService.cs`؛ `MakanApp.Infrastructure/Commerce/EfCommerceStore.cs` | `CommerceEndpointsTests.cs` و `CommerceDomainTests.cs` |
| E19 | `MakanApp.Application/Commerce/OrderPaymentService.cs`؛ `EfOrderPaymentStore.cs`، `EfSubscriptionActivationHandler.cs` و `UnavailablePaymentGateway.cs` در Infrastructure/Commerce | `OrderPaymentEndpointsTests.cs`؛ قیمت سرور، retry، verification/activation با Recording gateway |
| E20 | `MakanApp.Api/Authorization/PlatformPermissionAuthorizationHandler.cs`؛ service/storeهای Platform؛ `PlatformOwnerBootstrapRunner.cs` | `PlatformIamEndpointsTests.cs`، `PlatformOperationsEndpointsTests.cs`، `PlatformEducationSafetyEndpointsTests.cs`، `PlatformCommerceOperationsEndpointsTests.cs`، `PlatformBackofficeFinalAcceptanceTests.cs` |
| E21 | `MakanApp.Infrastructure/Platform/UnavailablePlatformStepUpAuthorization.cs`؛ guardهای `EnsureStepUpAsync` در Application/Platform | تست‌های Platform از `TestPlatformStepUpAuthorization` استفاده می‌کنند، نه MFA واقعی |
| E22 | `MakanApp.Infrastructure/Outbox/OutboxDispatcher.cs`؛ `Messaging/MessagingRealtimeOutboxDispatcher.cs`؛ `Notification/NotificationDeliveryDispatcher.cs` و E16 | Integrationهای Notification، MessagingRealtime، ReportExport و OrderPayment |
| E23 | `MakanApp.Infrastructure/Persistence/Migrations/`، `Configurations/`، `MakanDbContext.cs` و `MakanDbContextFactory.cs` | اجرای واقعی تمام migrationها در fixture؛ EF pending-model check موفق |
| E24 | `MakanApp.Api/Program.cs`، `Configuration/ProductionConfigurationValidator.cs`، `Hosting/*`، `Errors/GlobalExceptionHandler.cs`، `appsettings.Production.json` | `ProductionConfigurationValidationTests.cs`، `ProductionHostingTests.cs` و `HealthEndpointTests.cs` |
| E25 | `MakanApp.Api/Observability/*`، `Infrastructure/Identity/LoggingIdentitySecurityEventRecorder.cs` | تست header/correlation، redaction و قرارداد امن Platform؛ `docs/production/OBSERVABILITY.md` |
| E26 | `tools/production/sql/`، `smoke-test.ps1`، `load/k6-critical-apis.js`؛ `MakanApp.Api/Properties/PublishProfiles/FolderProfile.pubxml` | `docs/production/DEPLOYMENT_IIS.md`، `BACKUP_RESTORE.md`، `SQL_SERVER_SECURITY.md`، `LOAD_TESTING.md` و `RELEASE_CHECKLIST.md` |
| E27 | `OrganizationPeopleController`، `SchoolGuardianRelationsController`، `OrganizationPersonService`، `GuardianManagementService`، `EfOrganizationStore.PersonLink.cs` و migration `20261014214734_AddOperationalPersonGuardianOnboarding` | چهار فایل `OrganizationPersonOnboardingEndpointsTests*` با ۲۴ مورد Integration و `OrganizationOnboardingModelTests` با چهار Unit؛ API onboarding/enrollment/link، محافظ نام، چندفرزند، revoke/recheck، هم‌زمانی و constraints SQL |
| E28 | `ProductionConfigurationValidator` و `Program.cs`؛ تنظیمات Staging؛ ابزارهای `tools/production/*staging*` و `validate-local-migrations.ps1` | سند GAP-02: ۳۵ migration روی DB مستقل، publish ANCM، startup/health/ready محلی و 1021 تست؛ هدف HTTP-only و readiness404؛ OTP واقعی اجرا نشده |

## 4. Current architecture baseline

- هفت پروژه solution: Domain، Application، Infrastructure، Api و سه پروژه Unit/Integration/Architecture؛ همگی `.NET 9`. ابزار untracked AI در solution اضافه نشده است.
- ASP.NET Core Controller-based API؛ مسیرهای عمومی `/api/v1`، مدیریت پلتفرم `/api/admin/v1` و Hub در `/hubs/messaging`.
- شمارش source در ممیزی اولیه: **۴۲ Controller و ۲۵۳ attribute مربوط به HTTP action**، شامل AI. GAP-01 دو Controller و ۱۳ عملیات HTTP اضافه کرد. این عدد شمار attribute است، نه شمارش health/Hub.
- Onion در Modular Monolith: Domain خالص؛ Application دارای use case و port؛ Infrastructure دارای EF/provider/worker؛ Api دارای transport، auth و composition root. تست‌های Architecture سبز هستند.
- EF Core SQL Server `9.0.5`، یک `MakanDbContext` و schemaهای ماژولی؛ نه MySQL/PostgreSQL، نه migration خودکار startup.
- `AddSignalR()` بدون backplane؛ rate limiter حافظه‌ای process-local؛ فایل‌ها روی local disk؛ هر چهار worker در executable API. Redis، Worker executable مستقل و Object Storage adapter مشاهده نشدند.

## 5. Module readiness matrix

هر ردیف دقیقاً یک وضعیت از واژگان درخواست دارد. `COMPLETE_IN_CODE` فقط برای رفتار محدودِ نام‌برده است، نه تأیید Production کل ماژول. ستون Severity به شکاف مربوط اشاره می‌کند؛ `—` یعنی در آن رفتار مشخص شکاف release شناسایی نشد. عملیات واقعی سرور در این ممیزی انجام نشده است.

| Area | Status | Evidence | Automated tests | Real provider? | Operationally validated? | Severity | Next action |
|---|---|---|---|---|---|---|---|
| Auth/session/profile/revocation | COMPLETE_IN_CODE | E02، E24 | Identity و RefreshToken | نیاز به SQL؛ OTP ردیف بعد | LocalDB بله؛ سرور خیر | — | حفظ coverage و smoke هدف |
| MeliPayamak managed OTP | IMPLEMENTED_BUT_OPERATIONAL_VALIDATION_PENDING | E03 | HTTP contract ساختگی + API OTP | adapter واقعی؛ نمونه Prod unavailable | smoke زنده قابل استناد پیدا نشد | P0 | GAP-02 |
| Workspace/membership/invitation acceptance | COMPLETE_IN_CODE | E04 | tenant، نقش، پذیرش هم‌زمان | SQL واقعی در تست | عملیاتی خیر | — | onboarding از مسیر مجاز |
| OrganizationPerson onboarding | COMPLETE_IN_CODE | E05، E27 | ساخت/get/search/update/end و link از API؛ بدون ساخت ضمنی حساب/نقش | provider مطرح نیست | SQL ایزوله بله؛ مدرسه واقعی خیر | — | GAP-02 برای smoke هدف |
| Guardian read/select/recheck | COMPLETE_IN_CODE | E06، E27 | revoked/pending/cross-org؛ درخواست بعد از لغو API | provider مطرح نیست | رابطه از workflow جدید API | — | حفظ fail-closed |
| ایجاد/پایان/لغو رابطه والد توسط عامل مجاز | COMPLETE_IN_CODE | E27؛ Manager جاری مدرسه | Pending/activate/end/revoke، retry و چندفرزند | provider مطرح نیست | SQL ایزوله بله؛ مدرسه واقعی خیر | — | GAP-02 برای smoke هدف |
| Academic/Class/Enrollment/TeacherAssignment | COMPLETE_IN_CODE | E07، E27 | capacity، history، isolation و learner ساخته‌شده از API | SQL در تست | وابستگی fixture-only onboarding رفع شد؛ سرور خیر | — | smoke آموزش در GAP-02 |
| Session/schedule/official Attendance | COMPLETE_IN_CODE | E07 | timezone/conflict/correction/concurrency | meeting link یک URL است | اتصال meeting واقعی اثبات نشده | — | smoke برنامه و حضور |
| Assignment/submission/evaluation/release | COMPLETE_IN_CODE | E08 | lifecycle، authorization، concurrency، retry | provider لازم ندارد | روی fixture، نه مدارس واقعی | — | حفظ MaxScore همان نسخه |
| File lifecycle و local disk | IMPLEMENTED_BUT_OPERATIONAL_VALIDATION_PENDING | E09 | upload، ACL، signature، restart | LocalFileStorage واقعی | IIS/backup/capacity خیر | P0 | GAP-02، GAP-03 |
| Malware scan / AV / CDR | NOT_IMPLEMENTED | E09؛ فقط signature حداقلی | signature تست شده؛ scanner نیست | خیر | خیر | P1 | GAP-11 |
| Exam 8A–8E | COMPLETE_IN_CODE | E10 | frozen version، lease، finalize/retry، grading/release/correction | provider لازم ندارد | بار/قطعی واقعی خیر | — | GAP-14 برای اثبات ظرفیت |
| Messenger core و STEP 14 behavior | COMPLETE_IN_CODE | E11 | direct/group/channel، state، privacy، sync | safety جداست | فقط تست کنترل‌شده | — | حفظ scope فعلی |
| Child Safety provider | IMPLEMENTED_BUT_PROVIDER_MISSING | E12، E01 | recording allow/block/unavailable | فقط unavailable در Prod | خیر | P0 | GAP-05 |
| lifecycle معتبر طبقه سنی | FOUNDATION_ONLY | E12؛ `User.SetCommunicationAgeCategory` | fixture مقدار را تنظیم می‌کند | provider مطرح نیست | runtime setter ندارد | P0 | GAP-05 |
| policy گروه/کانال برای Minor | BLOCKED | E12؛ پارامتر age در CanCreate مصرف نمی‌شود | تست رفتار فعلی، نه policy جدید | تصمیم محصول لازم | خیر | P0 | GAP-05؛ بدون حدس policy |
| Moderation/report/block/evidence | COMPLETE_IN_CODE | E11، E12، E20 | case claim، scope، evidence، enforcement | تیم انسانی لازم | شیفت/SLA/retention اثبات نشده | P1 | GAP-20 |
| SignalR و reconnect عملیاتی | IMPLEMENTED_BUT_OPERATIONAL_VALIDATION_PENDING | E13 | .NET client + delta sync | backplane ندارد | IIS/browser/reconnect واقعی خیر | P1 | GAP-14 |
| Dashboards چهار نقش | COMPLETE_IN_CODE | E14 | isolation، release، official attendance، version | provider لازم ندارد | SQL تست؛ load خیر | — | عدم dependency به AI |
| Detailed reports | PARTIAL | E15؛ tie-break نسخه در paging کامل نیست | filter/page/audience؛ نه tie بین نسخه‌ها | provider لازم ندارد | load خیر | P2 | GAP-21، GAP-14 |
| Async export و download | PARTIAL | E16 | generation/claim/revoke/CSV privacy | local storage واقعی | expiry/purge پیاده نیست | P1 | GAP-10 |
| Internal notification center | COMPLETE_IN_CODE | E17، E22 | durable/read/unread/idempotency | REAL؛ SQL/outbox | فقط تست | — | حفظ truth داخلی |
| Notification preferences/devices/delivery | COMPLETE_IN_CODE | E17 | scope/quiet-hours/device/retry | کانال خارجی جداست | فقط تست | — | provider contract واقعی |
| Notification SMS | IMPLEMENTED_BUT_PROVIDER_MISSING | E01، E17 | recording/failure | خیر؛ Dev MOCK | خیر | P1 | GAP-12 |
| Notification Push | IMPLEMENTED_BUT_PROVIDER_MISSING | E01، E17 | recording/failure | خیر؛ Dev MOCK | خیر | P1 | GAP-12 |
| اعلان Session/Invitation/Report/AccountSecurity | FOUNDATION_ONLY | E17؛ enum/category با producer یکسان نیست | پوشش producerهای فعلی | کانال جداست | workflow رخداد کامل نیست | P2 | GAP-13 |
| Email | NOT_APPLICABLE | registration/adapter/config پیدا نشد | ندارد | ندارد | N/A | — | فقط با نیاز محصول جدید |
| Plan/version/subscription management | PARTIAL | E18، E20 | immutable version/effective time/override | provider لازم ندارد | renewal/cancel سلف‌سرویس نیست | P1 | GAP-07؛ تعریف lifecycle |
| Entitlement/Quota enforcement در business modules | FOUNDATION_ONLY | E18؛ مصرف فقط در Commerce/DI | خود سرویس/ledger تست دارد | provider لازم ندارد | enforcement سراسری نیست | P1 | GAP-07 |
| Order/payment/verification | IMPLEMENTED_BUT_PROVIDER_MISSING | E19 | Recording gateway، duplicate verification | خیر؛ Unavailable، نه پرداخت دستی | خیر | P0 | GAP-06 برای عرضه پولی |
| Backoffice/IAM/provisioning/operations | PARTIAL | E20، E21 | permission، last-owner/manager، audit | MFA/payment/safety کم است | staging واقعی اثبات نشده | P0 | GAP-04 و gateهای مرتبط |
| Platform MFA/step-up | IMPLEMENTED_BUT_PROVIDER_MISSING | E21 | Test provider؛ fail-closed واقعی | خیر | خیر | P0 | GAP-04 |
| چهار worker و durable retry | COMPLETE_IN_CODE | E22 | processor/claim/retry/recovery | به ازای side-effect متفاوت | IIS lifecycle خیر | P1 | GAP-09 |
| Schema/migration integrity | COMPLETE_IN_CODE | E23، E27 | ۳۵ migration روی empty LocalDB پس از GAP-01 | SQL Server provider واقعی | SQL هدف/permission/TLS خیر | P0 | GAP-02 |
| HTTP production safety | IMPLEMENTED_BUT_OPERATIONAL_VALIDATION_PENDING | E24 | Production factory و config validation | IIS/TLS بیرونی لازم | host تست؛ نه IIS واقعی | P0 | GAP-02 |
| Observability بیرونی | PARTIAL | E25 | metrics/header/privacy تست‌شده | sink/exporter/alert اثبات نشده | خیر | P1 | GAP-08 |
| Backup/restore | IMPLEMENTED_BUT_OPERATIONAL_VALIDATION_PENDING | E26 | drill محلی مستند | job عملیاتی اثبات نشده | LocalDB marker بله؛ DB+files خیر | P0 | GAP-03 |
| Windows/IIS deployment | IMPLEMENTED_BUT_OPERATIONAL_VALIDATION_PENDING | E24، E26، E28 | host/config Staging و artifact publish بررسی شد | ANCM web.config واقعی؛ نه deployment | هدف HTTP-only و ready404؛ IIS/TLS واقعی اثبات نشده | P0 | GAP-02 همچنان باز |
| CI/CD | NOT_IMPLEMENTED | tracked workflow/pipeline موجود نیست | اجرای محلی بله | N/A | خیر | P2 | GAP-15 |
| Load/performance baseline | IMPLEMENTED_BUT_OPERATIONAL_VALIDATION_PENDING | E26 | correctness concurrency جداست | k6 script موجود | نتیجه staging موجود نیست | P1 | GAP-14 |
| AI/Knowledge/RAG | DEFERRED_TO_AI_TRACK | فقط boundary اسناد موجود | suite موجود Fake AI | خارج این ممیزی | خارج این ممیزی | — | بخش 19 |

## 6. External provider matrix

`REAL/MOCK/NOT IMPLEMENTED/N/A` در این جدول وضعیت کانال یا adapter را توصیف می‌کند، نه readiness کل سیستم.

| Purpose | Interface | Runtime implementation / configuration | Production / Dev | Tests | Real smoke? | Blocker |
|---|---|---|---|---|---|---|
| OTP | `IOtpDeliveryProvider` | `MeliPayamakOtpProvider` با انتخاب `Sms:Provider=MeliPayamakOtp`؛ `Sms:MeliPayamakOtp:ServiceToken` و `Identity:SecurityKey` | adapter REAL؛ نمونه Prod `UnavailableOtpDeliveryProvider`؛ Dev `DevelopmentOtpProvider` MOCK | E02/E03؛ timeout/status/response/redaction | مدرک محیط هدف پیدا نشد؛ تماس زنده این audit ممنوع/انجام‌نشده | P0 ورود Production، GAP-02 |
| Internal notifications | `INotificationService` / `IOutbox` | `NotificationService`، `EfOutbox` و `OutboxProcessor` | REAL؛ provider شبکه‌ای لازم نیست | E17/E22 | SQL test بله؛ worker server خیر | provider blocker ندارد |
| Notification SMS | `INotificationSmsProvider` | `UnavailableNotificationSmsProvider`؛ `Notifications:SmsProvider` | Prod NOT IMPLEMENTED؛ Dev `DevelopmentNotificationSmsProvider` MOCK | recording + failures | خیر | P1، GAP-12 |
| Push | `IPushNotificationProvider` | `UnavailablePushNotificationProvider`؛ `Notifications:PushProvider` | Prod NOT IMPLEMENTED؛ Dev `DevelopmentPushNotificationProvider` MOCK | recording/device/retry | خیر | P1، GAP-12 |
| Payment | `IPaymentGateway` | `UnavailablePaymentGateway`؛ `Payments:Provider=Unavailable` | real gateway NOT IMPLEMENTED؛ `RecordingPaymentGateway` فقط تست | E19 | خیر | P0 عرضه پولی، GAP-06 |
| Platform step-up | `IPlatformStepUpAuthorization` | `UnavailablePlatformStepUpAuthorization` همیشه false | factor واقعی NOT IMPLEMENTED؛ Test provider فقط تست | E20/E21 | خیر | P0 مسیرهای حساس، GAP-04 |
| Child Safety | `IContentSafetyProvider` | `UnavailableContentSafetyProvider`؛ `Messaging:ContentSafetyProvider` | NOT IMPLEMENTED؛ `DevelopmentAllowContentSafetyProvider` فقط Dev/Testing | E12 | خیر | P0 Messenger با Minor/Unknown، GAP-05 |
| File/export bytes | `IFileStorage` / `IStorageReadinessProbe` | `LocalFileStorage`؛ `Storage:RootPath` | REAL local disk در Prod با root معتبر؛ نه object adapter | E09/E16؛ storage واقعی موقت | host restart تست شده؛ IIS/disaster خیر | backup/ACL P0؛ Object Storage P3 |
| Meeting links | فیلد/validation جلسه؛ نه SDK provider | URL ذخیره‌شده در Session و خروجی مجاز schedule | REAL metadata؛ ایجاد meeting/provider API پیاده نیست | E07؛ click حضور ثبت نمی‌کند | جلسه آنلاین واقعی اثبات نشده | provider خودکار فعلاً N/A؛ health جلسه smoke شود |
| Email | interface/adapter runtime یافت نشد | config/registration وجود ندارد | N/A برای scope فعلی؛ NOT IMPLEMENTED در صورت درخواست آینده | ندارد | خیر | blocker فعلی محسوب نشود |
| AI | بررسی نشده؛ فقط مرز | `DEFERRED_TO_AI_TRACK` | خارج دامنه | suite موجود Fake AI | audit جدید زنده ندارد | جدا از gate غیر AI |

ملی‌پیامک فعلی **فقط OTP** را پوشش می‌دهد. `MeliPayamakOtpProvider` جایگزین `INotificationSmsProvider` نشده است. انتخاب provider واقعی پرداخت، Push، SMS اعلان و safety در کد/اسناد به vendor مشخص و مصوب قابل اجرا نرسیده؛ نام vendor، schema callback یا credential جدید اختراع نمی‌شود.

## 7. Production blocker list

Severity وابسته به دامنه عرضه صریح است. P0های شرطی فقط وقتی قابلیت مربوط عرضه می‌شود الزام دارند؛ حذف قابلیت از scope باید واقعی، مصوب و قابل آزمون باشد، نه صرفاً وعده استفاده نکردن.

### P0 — Release blocker

| ID | شکاف و شاهد | شرط عبور |
|---|---|---|
| GAP-02 | آماده‌سازی و startup محلی اثبات شد؛ مقصد معرفی‌شده HTTPS ندارد و ready404 است؛ OTP/session/core زنده و SQL/IIS هدف اثبات نشده‌اند؛ E28 | HTTPS معتبر و readiness نسخه جاری، سپس SQL TLS/principals، secrets بیرونی، storage ACL و OTP send/receive/verify واقعی |
| GAP-03 | recovery واقعی داده مدرسه+storage+کلیدها اثبات نشده؛ E26 | job/retention/off-server backup و restore drill سازگار با شواهد RPO/RTO مصوب |
| GAP-04 | MFA واقعی وجود ندارد؛ مسیر حساس admin عمداً رد می‌شود؛ E21 | factor تأییدشده، verification/replay/freshness/session assurance و تست واقعی؛ بدون دورزدن fail-closed |
| GAP-05 | safety provider، lifecycle معتبر سن و policy Minor ناقص‌اند؛ E12 | تصمیم محصول و adapter/provider واقعی یا scope محدود مصوب و enforceشده؛ مسیر Unknown با allow ساختگی باز نشود |
| GAP-06 | درگاه، callback قابل اعتماد و reconciliation واقعی نداریم؛ E19 | فقط برای عرضه پولی: قرارداد provider، verify واقعی، duplicate/replay/mismatch، recovery وضعیت unknown و settlement ثبت‌شده |

GAP-01 از فهرست blockerهای باز خارج شد: workflow مجاز، Audit اتمیک، اتصال رضایتی بدون merge و قطع دسترسی والد در درخواست بعدی با تست SQL/API اثبات شدند (E27). سایر P0ها تغییر وضعیت نداده‌اند.

### P1 — Serious operational risk

| ID | شکاف | شاهد و محدودیت |
|---|---|---|
| GAP-07 | Entitlement/Quota در business mutations enforce نمی‌شود؛ lifecycle تجاری renewal/cancel/refund تعریف/تکمیل نشده | E18/E19؛ فقط Commerce/DI مصرف‌کننده port هستند؛ ظرفیت کلاس جای سقف پلن نیست. بدون وعده تجاری سقف‌دار پایلوت ممکن است؛ عرضه چنین وعده‌ای نیازمند closure است |
| GAP-08 | sink متمرکز، exporter، alert و مسئول incident اثبات نشده | E25/E26؛ Meter و ILogger موجودند، مقصد عملیاتی نه |
| GAP-09 | recovery دستی poison/final-failure و lifecycle IIS worker rehearsed نیست | E22؛ retry محدود است؛ بعد سقف claim نمی‌شود؛ admin operations فعلی read-only است |
| GAP-10 | export TTL/purge، cleanup فایل orphan/rejected/deleted، quota دیسک و retention jobها کامل نیست | E09/E16؛ timestamp انقضای unattached به معنی purge/download-expiry عمومی نیست |
| GAP-11 | AV/CDR/quarantine واقعی نداریم | E09؛ signature حداقلی و attachment download وجود دارد، ولی PDF/video پاک از malware تضمین نمی‌شوند؛ policy پذیرش فایل باید مصوب باشد |
| GAP-12 | Push و SMS عمومی اعلان تحویل واقعی ندارند | E17؛ Unavailable نتیجه Suppressed می‌دهد، Dev فقط موفقیت آزمایشی؛ in-app قابل استفاده می‌ماند |
| GAP-14 | ظرفیت و transport هدف اثبات نشده: load، reconnect، browser WebSocket/SSE، proxy/body limits و 429 | E13/E24/E26؛ تست‌های correctness و .NET client جای آزمون هدف را نمی‌گیرند |
| GAP-16 | رقابت request OTP و provider-accepted/DB-failed coverage کافی ندارد | E02؛ read/count → network → save بدون serialization درخواست؛ verify خودش transaction دارد. این ریسک source-based است، نه ادعای شکست بازتولیدشده در این audit |
| GAP-20 | retention/دسترسی/بازیابی audit، گردش کار تیم Moderation و شواهد account-security عملیاتی تثبیت نشده | E20/E25؛ audit append-only در EF است، نه tamper-proof در برابر DBA؛ refresh reuse فقط logging recorder دارد |

### P2 — Important improvement

| ID | شکاف | دلیل غیر P0 بودن |
|---|---|---|
| GAP-13 | producer اعلان برای همه categoryها وجود ندارد؛ Session/Invitation/Report/AccountSecurity کامل نیست | هفت رخداد موجود واقعاً تولید می‌شوند؛ بدون وعده آن جریان‌های جدید قابل عرضه محدود است |
| GAP-15 | CI/CD و artifact/migration automation بازتولیدپذیر نداریم | release دستی با gate و تأیید مستند ممکن است؛ workflow موجود جعل نشود |
| GAP-17 | drift اسناد قدیمی API/README/HTML/PDF | source فعلی ۴۲ Controller/۲۵۳ action attribute است؛ README هنوز snapshot قدیمی ۲۲/۱۴۱ و آخرین migration قدیمی را دارد؛ رفتار CORS/Swagger Production نیز در متن قدیمی متفاوت است |
| GAP-21 | ترتیب کاملاً یکتای paging گزارش‌های نسخه‌دار و تست tie کامل نیست | `EfDetailedReportReadService.StudentParent.cs::OrderExams` بعد تاریخ/عنوان فقط `ExamId` را tie-break می‌کند؛ دو ExamVersion منتشرشده همان آزمون می‌توانند هر دو کلید برابر داشته باشند. مسیر grades نیز پس از زمان فقط AssessmentId دارد. SQL ترتیب این ردیف‌ها را تضمین نمی‌کند؛ خطر تکرار/جاافتادن صفحه یا CSV باید با تست مشخص بررسی شود، نه اینکه در این audit شکست runtime ادعا شود |

### P3 — Future scalability / quality

| ID | شکاف | شرط نیاز |
|---|---|---|
| GAP-18 | SignalR backplane، limiter مشترک و اثبات چند instance وجود ندارد | قبل از scale-out؛ برای پایلوت تک instance الزام ذاتی نیست |
| GAP-19 | Object Storage adapter وجود ندارد | هنگام نیاز HA/چند instance/حجم؛ local persistent disk با recovery معتبر برای شروع مجاز است |

## 8. Operational validation still required

- staging جدا با داده مصنوعی و هویت‌های Student/Parent/Teacher/Manager/Platform، بدون SQL دستی برای ساخت رابطه کسب‌وکاری.
- ارسال **و دریافت و verify واقعی OTP** با رضایت و شماره تست مجاز؛ expire/resend/restart و عدم ثبت token/code در proxy/APM. صرف پاسخ موفق send کافی نیست.
- TLS دامنه و SQL، proxy واقعی، 401/403/404، revoked membership/session/GuardianRelation، production CORS و Swagger خاموش.
- upload/download و export روی persistent path، App Pool identity، restart/recycle، کمبود دیسک و restore فایل‌های مرتبط.
- migrate روی SQL Server هدف با principal جدا؛ بررسی `__EFMigrationsHistory` و smoke پس از migration. readiness فعلی فقط اتصال SQL و storage را می‌سنجد، نه آخرین نسخه schema.
- worker crash/restart، lease recovery، provider timeout، سقف retry و مسیر operator recovery؛ لاگ و alert قابل مشاهده باشند.
- refresh rotation/replay و revoke، آزمون UI-independent مرورگری transport واقعی SignalR و delta sync بعد از قطع شبکه.
- اجرای load مستقل و محدودِ staging برای auth، dashboard/report، messaging/SignalR، submission، exam autosave/finalize و worker backlog.
- backup/restore و app rollback rehearsal با release artifact و تنظیمات واقعی، نه فقط وجود runbook.

در این ممیزی هیچ server change، deployment، تماس SMS/پرداخت/safety، AI call یا load test انجام نشد.

## 9. Security gaps

### کنترل‌هایی که واقعاً موجودند

OTP فقط hash/salt ذخیره می‌شود؛ challenge انقضا، محدودیت تلاش و مصرف یک‌بار دارد. verify و refresh دارای transaction هستند؛ access/refresh opaque و hashed هستند؛ replay refresh نشست خانواده را revoke می‌کند. Account suspension و session/device revocation در runtime اعمال می‌شوند. authorization بر membership/role/resource جاری است، نه selector کلاینت. Parent هر بار GuardianRelation را recheck می‌کند؛ Manager سازمانی Platform Admin نیست. permission پلتفرم از DB resolve می‌شود؛ حتی PlatformOwner blanket private-content access ندارد.

OTP adapter logging فقط recipient ماسک‌شده دارد و HttpClient آن `RemoveAllLoggers()` می‌شود، چون token سرویس در path درخواست provider قرار می‌گیرد. خطای عمومی از ProblemDetails عبور می‌کند و stack trace به کلاینت داده نمی‌شود. نبود secret در فایل‌های appsettings بررسی‌شده تأیید شد؛ این به معنی ممیزی جامع تاریخچه Git یا اثبات secret store سرور نیست. User Secrets/متغیرهای واقعی محیط برای استخراج secret خوانده نشدند.

### شکاف‌های لازم برای بستن

- GAP-04: نشست دارای فیلد assurance است، اما provider runtime هرگز step-up را تأیید نمی‌کند؛ فیلد موجود معادل MFA نیست.
- GAP-05: User تازه `CommunicationAgeCategory.Unknown` است؛ مسیر runtime برای `SetCommunicationAgeCategory` پیدا نشد. presence یک عضو غیر Adult در گفتگو، safety evaluation می‌خواهد؛ Prod unavailable موجب رد امن ارسال می‌شود. این محدودیت فقط کودکان تأییدشده را درگیر نمی‌کند.
- `ConversationManagementAuthorizationPolicy.CanCreate` پارامتر سن را می‌گیرد ولی در تصمیم استفاده نمی‌کند؛ منع/اجازه گروه Minor را نباید از متن سند نتیجه گرفت. همین‌طور محدودیت‌های رابطه Teacher/Student در سازمان وجود دارند، اما policy کامل personal child/adult باید مصوب شود، نه فرض شود.
- GAP-11: امضای فایل و محدودیت نوع/حجم جای malware scanner را نمی‌گیرد.
- GAP-16: throttle per-phone request check و ارسال شبکه‌ای atomic نیستند؛ coverage هم‌زمانی و recovery پس از پذیرش provider نیاز است. محدودیت challenge و IP حاضر است؛ ادعای bypass قطعی بدون تست جدید نمی‌شود.
- GAP-20: audit EF append-only است؛ least-privilege، نگه‌داری، دسترسی operator و حفاظت backup/log در محیط باید اعمال شوند. impersonation/shared admin account راه‌حل gap نیست.

الزام حقوقی جدیدی خارج اسناد پروژه تعریف نشده است؛ retention/consent/appeal/SLA باید توسط مالک محصول تعیین شوند.

## 10. Data / DB / Migration gaps

وضعیت ممیزی اولیه: **۳۴ migration** از `20260929075711_InitialIdentity` تا `20261013084443_AddRefreshTokenRotationAndSessionAssurance`. در GAP-01 migration شماره ۳۵ با نام `20261014214734_AddOperationalPersonGuardianOnboarding` افزوده و روی empty LocalDB تست شد؛ دیتابیس عادی توسعه/Production migrate نشده است. چهار ستون nullable، سه filtered unique، دو CHECK و یک composite FK به Invitation/OrganizationPerson افزوده شده‌اند؛ جدول/داده‌ای حذف نشده و Guardian schema تغییر نکرده است.

نمونه integrity موجود: composite FK سازمان/منبع، filtered unique فعال برای membership/enrollment/guardian، `rowversion`، check constraint وضعیت/تاریخ/مقدار و `DeleteBehavior.Restrict` برای تاریخچه‌های مرتبط. `AssignmentVersion.MaxScore` و `EvaluationRevision.Score` دارای precision `decimal(9,2)` هستند؛ validation دامنه سقف همان نسخه را کنترل می‌کند. نمره آزمون نیز از version/سؤال منجمد خوانده می‌شود. timestampهای رویداد با UTC سرور و ستون‌های `datetime2(7)` مدل می‌شوند؛ DateOnly برای تقویم دوره با timestamp رویداد اشتباه نشود.

محدودیت‌ها:

- LocalDB نسخه و topology واقعی SQL Server Production، certificate، permission و workload آن را اثبات نمی‌کند.
- collation صریح در `OnModelCreating`/configuration مشاهده نشد؛ باید در ساخت DB محیط و تطبیق test/production ثبت شود، به‌ویژه username/search فارسی.
- migration bundle یا script نسخه‌بندی‌شده اجرای migrationها در مخزن پیدا نشد (۳۴ در ممیزی اولیه، ۳۵ پس از GAP-01)؛ اسکریپت‌های SQL موجود برای principal و drill هستند. فرمان EF و gate دستی مستند است، نه pipeline اجراشده.
- design-time factory در نبود تنظیم اتصال، fallback محلی دارد؛ migration در release باید با environment و target تأییدشده اجرا شود، نه اعتماد به fallback.
- rollback مستند، app rollback و DB forward-fix/restore بر اساس compatibility است؛ اجرای کور Down migration راهبرد ایمن فرض نشده است. rehearsal واقعی باقی است.
- dependency داده‌ای GAP-01 رفع شد: ساخت پرونده، دعوت اتصال و مدیریت رابطه والد اکنون workflow API با مجوز مدرسه دارند؛ درج دستی رابطه جایگزین مجاز این APIها نیست.

## 11. Infrastructure / deployment gaps

| مورد Windows Server | شاهد مخزن | باقی‌مانده |
|---|---|---|
| IIS / Hosting Bundle / App Pool | `DEPLOYMENT_IIS.md`؛ No Managed Code و identity اختصاصی | نصب .NET 9 Hosting Bundle، تنظیم IIS و recycle روی سرور تأیید نشده |
| publish / web.config | SDK Web؛ در GAP-02 publish محلی Release/Staging بررسی شد: ANCM V2، dotnet، inprocess، stdout=false | deployment واقعی IIS و artifact clean از commit هدف هنوز اثبات نشده است؛ publish محلی از worktree شامل dirtyهای قبلی AI بود |
| Environment / secret store | جای‌خالی امن و fail-fast اکنون برای Production و Staging | تزریق و ACL/rotation/backup secret در محیط واقعی هنوز لازم است |
| SQL / Firewall | TLS validation و template runtime/migration principal | certificate معتبر، principal least-privilege، firewall/private exposure و target history |
| Domain / DNS / HTTPS | HTTPS redirect، HSTS، AllowedHosts، known proxy validation | DNS، binding، certificate chain و forwarded-header واقعی |
| SignalR / WebSocket | Hub و negotiated auth موجود | WebSocket IIS، proxy timeout، reconnect و transport واقعی |
| Files / permissions | persistent root مطلق، write/delete readiness probe | ACL identity، ظرفیت، خارج publish بودن داده، backup و restore |
| Logs / health | correlation/metrics و live/ready | sink، alert، operator و smoke authenticated هدف |
| Update / rollback | side-by-side، migration جدا و gate در runbook | artifact قابل بازتولید، drain، smoke و rehearsal rollback |

نمونه Production عمداً بدون تنظیمات واقعی بالا نمی‌آید؛ این fail-fast صحیح است، نه باگ لازم‌الاصلاح در audit. CORS Production فقط originهای صریح HTTPS را می‌پذیرد؛ Swagger پیش‌فرض خاموش است. docs قدیمی که «همه محیط‌ها باز» می‌گویند authoritative نیستند.

CI/CD: `.github` workflow یا pipeline build/test/publish/deploy/approval در tracked files پیدا نشد. FolderProfile، script smoke و دستور publish در راهنما معادل CI نیستند. Dockerfile/compose نسخه‌بندی‌شده نیز یافت نشد؛ Docker صرفاً در راهنما آمده است و برای مقصد IIS فعلی الزام ندارد.

## 12. Provider gaps

- GAP-02: credential پایدار Identity و token OTP باید بیرون Git تزریق و روی محیط هدف smoke شوند. این سند هیچ مقدار secret، شماره واقعی، token یا connection string ندارد.
- GAP-04: factor/MFA، ثبت assurance و freshness واقعی نیازمند قرارداد و implementation است؛ Unavailable نباید با Test provider در Production عوض شود.
- GAP-05: provider safety و تصمیم سن/گروه نیازمند privacy، timeout و fail-closed contract هستند. نبود آن با `DevelopmentAllow` در Prod دور زده نمی‌شود؛ DI این انتخاب را رد می‌کند.
- GAP-06: vendor پرداخت تعیین نشده است. public callback/webhook adapter، authenticate/verify قرارداد آن و job reconciliation واقعی وجود ندارند. `IPaymentVerificationService` داخلی موجود، server-to-server verify را مطالبه می‌کند و duplicate business effect را با store/unique/Outbox مهار می‌کند؛ تست آن Recording است. پرداخت دستی موفق جعلی ساخته نشده است.
- GAP-12: Notification SMS و Push contract و adapter واقعی می‌خواهند؛ dev providerها فقط نتیجه آزمایشی می‌دهند. provider unavailable در وضعیت فعلی Suppressed است، نه صفی که قطعاً بعداً با تغییر config تحویل خواهد شد؛ برای recovery باید semantics صریح شود.
- Object Storage GAP-19 و Email فعلاً blocker پایه نیستند. external meeting provider خودکار نیز جزو workflow فعلی نیست؛ URLها دستی ثبت و با مجوز نمایش داده می‌شوند.

## 13. Background-worker gaps

هر چهار worker از `AddHostedService` در API شروع می‌شوند؛ executable Worker جدا وجود ندارد. App Pool idle/recycle/deploy می‌تواند پردازش را متوقف کند، ولی رکورد durable در SQL می‌ماند. polling/wake signal حافظه‌ای منبع حقیقت نیست.

| Worker | کار و claim | Retry / idempotency / failure | شکاف عملیاتی |
|---|---|---|---|
| `OutboxDispatcher` / `OutboxProcessor` | اعلان داخلی و SubscriptionActivation؛ batch=50، lease=30s، SQL `UPDLOCK/READPAST/ROWLOCK` | سقف 10؛ اثر اعلان با event/recipient یکتا و activation idempotent؛ failure و next attempt پایدار؛ cancellation جاری عبور می‌کند | after-cap item دیگر claim نمی‌شود؛ manual recovery و alert/retention نداریم |
| `MessagingRealtimeOutboxDispatcher` / processor | اعلان تغییر metadata؛ batch=50، lease=30s، polling و signal | سقف 10؛ retry delay محدود؛ timestamp/failure پایدار؛ delta cursor منبع بازیابی client است، نه exactly-once شبکه | IIS restart و چند node rehearsed نیست؛ backplane وجود ندارد؛ poison recovery نیاز است |
| `NotificationDeliveryDispatcher` / processor | claim token، lease و مقصد device/credential؛ provider بیرون transaction | provider timeout، retry محدود و delay افزایشی؛ Delivered/Suppressed/Failed پایدار؛ device revoke بررسی می‌شود | provider واقعی نیست؛ crash پس از side-effect و قبل save با provider واقعی باید idempotency-key contract داشته باشد |
| `ReportExportDispatcher` / processor | یک job با SQL claim؛ lease پیش‌فرض120s؛ wake/poll؛ scope مجدد پیش از تولید | حداکثر3؛ authorization failure دائمی؛ expired lease reclaim؛ فایل با ID job قابل reuse؛ cancellation/terminal failure | heartbeat/مدت کار در بار هدف اثبات نشده؛ list/retry دستی/TTL/purge نداریم |

تمام claimها قرار نیست exactly-once side-effect شبکه را تضمین کنند. Integration testهای processor/lease/crash simulation مفیدند، اما kill/recycle واقعی IIS و مصرف provider بیرونی هنوز لازم‌اند. worker برای file cleanup، retention audit/session/OTP، payment reconciliation یا scheduler روزانه پیدا نشد؛ وجود فیلد تاریخ به معنی job نیست.

## 14. Observability gaps

کد موجود: ILogger با template، `X-Correlation-ID` کنترل‌شده، ProblemDetails trace ID، سه instrument HTTP در Meter `MakanApp.Api` با route template و status class؛ بدون label هویتی. health عمومی کم‌جزئیات است: live فقط process، ready اتصال SQL و storage probe. endpointهای operations پلتفرم permissionدارند.

شاهد configuration یا اجرای sink مرکزی، trace/metric exporter، collector، dashboard عملیاتی، paging/alert routing یا retention log پیدا نشد. وجود Activity یا `AddMetrics` خروجی قابل مشاهده بیرونی را اثبات نمی‌کند. ready نبود providerهای اختیاری/MFA و migration version را نمی‌سنجد؛ سبز بودن آن، مجوز اعلام readiness کل محصول نیست.

GAP-08 باید مشاهده end-to-end یک request/correlation، خطای dependency، SQL/storage failure و worker backlog را اثبات کند. GAP-20 باید security eventها و audit access/retention را تعیین کند. برای path حاوی token OTP، proxy/APM آینده نیز باید redaction داشته باشد؛ حذف HttpClient logger به‌تنهایی تمام stack استقرار را پوشش نمی‌دهد.

## 15. Backup / restore status

وضعیت: `IMPLEMENTED_BUT_OPERATIONAL_VALIDATION_PENDING`، P0 برای داده واقعی مدرسه.

مدرک مثبت موجود است: `docs/production/BACKUP_RESTORE.md` اجرای ۲۰۲۶-۱۰-۱۳ را ثبت می‌کند؛ source DB disposable روی LocalDB، backup با checksum، restore با MOVE در DB دوم و validate marker با خروجی `STEP16_RESTORE_DRILL_SUCCEEDED`. SQL آن در `tools/production/sql/restore-drill-localdb.sql` موجود است. این audit آن drill یا cleanup را دوباره اجرا نکرد.

این شاهد صرفاً LocalDB و marker مصنوعی را ثابت می‌کند، نه recovery کامل سیستم. موارد فاقد مدرک هدف:

- backup job واقعی با retention/off-server copy/encryption و هشدار شکست؛ recovery model و log chain متناسب RPO مصوب.
- snapshot سازگار storage و DB، بازیابی فایل و تطبیق hash، session/membership/grade/Outbox و دسترسی پس از restore.
- بازیابی `Identity:SecurityKey` و دیگر secretها از مسیر امن؛ مقدار کلید در backup عمومی قرار نگیرد.
- RTO/RPO اندازه‌گیری‌شده روی topology واقعی و اپلیکیشن پس از restore؛ restore از سرور/دیسک از دست‌رفته.

وجود فایل backup و `VERIFYONLY` به‌تنهایی closure این gap نیست. هیچ job SQL Agent یا job VPS واقعی از داخل repository تأییدشدنی نبود.

## 16. Performance/load status

correctness concurrency موجود: آخرین ظرفیت کلاس و contention بیشتر، پذیرش دعوت هم‌زمان، rowversion جلسات/حضور، submission/release race، lease و finalize آزمون، duplicate message/payment و claim worker. این‌ها load baseline نیستند.

`tools/production/load/k6-critical-apis.js` و `LOAD_TESTING.md` موجودند. gate `CONFIRM_ISOLATED_STAGING=YES` و opt-in برای OTP/send دارد؛ read mix با آزمون autosave/finalize یا provider واقعی یکسان نیست. گزارش اجرای staging شامل hardware، dataset، throughput، p50/p95/p99، error/429/5xx و queue lag پیدا نشد. در این audit هیچ بار یا سرویس خارجی آزمایش نشد.

ریسک‌های مشخص برای GAP-14:

- limiterها process-local هستند و در محیط Testing سقف بالاتری دارند؛ تست per-phone OTP یا status ساختگی429، سقف واقعی Production را اثبات نمی‌کند.
- `SessionAuthenticationHandler` فقط `Authorization: Bearer` را می‌خواند؛ `access_token` query مصرف نمی‌شود. سازگاری روش واقعی client مرورگری WebSocket/SSE و reconnect باید در staging اثبات شود؛ از موفقیت .NET client، موفقیت همه browser transportها نتیجه نمی‌گیریم.
- upload limit در FormOptions و use case هست؛ حد JSON/body/timeout در تمام لایه‌های proxy/IIS/Kestrel و معادل رفتار413/429 باید ثبت و آزمون شود. وجود limit پیش‌فرض framework، policy صریح کل محصول نیست.
- report dataAsOfUtc یک timestamp گردآوری است، نه snapshot تراکنشی؛ CSV صفحه‌صفحه از داده جاری تولید می‌شود. bounds کد مفیدند، ولی latency/lease/SQL plan تحت بار هنوز اثبات نشده است.
- علاوه بر تغییر هم‌زمان داده، خود ترتیب paging باید یکتا باشد. در GAP-21، گزارش آزمون با دو نسخه دارای زمان/عنوان برابر فقط با ExamId مرتب می‌شود؛ این باگ اثبات‌شده تغییر سقف نمره داشبورد نیست، بلکه یک نقص تضمین ترتیب در source و شکاف تست است. نتیجه SQL با tie را نباید stable فرض کرد.
- نه Redis/backplane و نه scale-out limiter/storage مشترک داریم؛ GAP-18/19 قبل از چند instance بسته می‌شوند، نه الزاماً قبل از پایلوت تک instance.

## 17. Controlled Pilot readiness

**Verdict: `NOT_READY` در وضعیت اثبات‌شده فعلی.** توسعه داخلی سالم است، اما pilot با مدارس/کاربران واقعی بدون بستن gateهای زیر تأیید نمی‌شود:

1. GAP-01: **CLOSED در کد**؛ ایجاد/مدیریت learner و رابطه والد از API مجاز، بدون دست‌کاری DB کسب‌وکاری. اجرای همین جریان در محیط هدف همچنان بخشی از GAP-02 است.
2. GAP-02 و GAP-03: محیط هدف امن، ورود OTP واقعی، migration و storage صحیح، smoke و recovery پایه DB+files ثبت‌شده.
3. scope پایلوت مکتوب و enforceشده: تک instance، کاربران محدود و داده کنترل‌شده؛ Commerce پولی/Push/SMS اعلان وعده داده نشوند تا providerها آماده شوند. in-app می‌تواند کانال اعلان باشد.
4. اگر Backoffice حساس یا Messenger با Minor/Unknown عرضه می‌شوند، GAP-04/05 نیز قبل از pilot بسته شوند. صرف unavailable نگه‌داشتن بدون توضیح UX و محدودسازی واقعی کافی نیست. سن کاربران را با SQL دستی Adult نکنید.
5. مسئول عملیات و روش recovery failure، پایش حداقلی و baseline محدود بار/reconnect؛ پذیرش موقت ریسک P1 باید صریح و ثبت‌شده باشد.

پس از ثبت شواهد این شروط می‌توان حکم را به `CONDITIONALLY_READY` تغییر داد؛ این audit خودش چنین شواهدی تولید نکرده است. حذف billing از پایلوت، نبود workflow learner/guardian یا backup را جبران نمی‌کند.

## 18. General Production readiness

**Verdict: `NOT_READY`.** P0های مربوط به دامنه عرضه، ریسک‌های P1 بدون پذیرش عملیاتی، providerهای واقعی و load/recovery باید بسته شوند. برای عرضه پولی علاوه بر GAP-06، entitlement enforcement و lifecycle تجاری GAP-07 پیش‌شرط وعده پلن/سقف هستند.

اتمام STEP 16 یا سبز بودن ۹۷۷ تست جای deploy واقعی، اتصال provider، backup/restore و incident ownership را نمی‌گیرد. AI مستقل است؛ freeze آن مانع ممیزی/تکمیل core غیر AI نیست و موفقیت آینده AI هم این blockerها را برطرف نمی‌کند.

## 19. AI deferred items

تمام موارد زیر: `DEFERRED_TO_AI_TRACK`.

- STEP 10F-BE: lifecycle منبع Knowledge/RAG، publication/audience/supersession/delete propagation.
- STEP 10G: اصلاح failureهای E2E و پذیرش اتصال واقعی Backend↔AI.
- AI provider، prompts، structured output، DTO/contract و رفتار Copilot.
- embeddings، vector DB و Intelligence/recommendations.

## 20. Exact recommended closure roadmap

این ترتیب از gap واقعی می‌آید، نه شماره STEP قدیمی. در ممیزی اولیه همه موارد **پیشنهاد** بودند و در خود audit اجرا نشدند؛ GAP-01 پس از تأیید جداگانه مالک اجرا و در ادامه به‌روزرسانی شد. سایر مسیرهای «expected» محل بررسی/تغییر احتمالی‌اند، نه فایل ایجادشده یا طرح نهایی API. یک تیم می‌تواند پس از approval بعضی کارهای ops/provider مستقل را هم‌زمان پیش ببرد؛ این audit delegation یا تغییر خارجی انجام نمی‌دهد.

### GAP-01 — تکمیل onboarding فراگیر و Guardian lifecycle — CLOSED

- هدف/اهمیت: بستن dependency اصلی آموزش و والد؛ حذف نیاز به درج/تغییر دستی SQL.
- تصمیم مصوب: نام جهانی تنها برای Person بدون حساب و دقیقاً یک ارجاع سازمانی قابل ویرایش است؛ اتصال فقط با Invitation و رضایت صریح صاحب حساب در نشست معتبر؛ بدون matching یا merge. unlink در این مرحله پیاده نشده است.
- پیاده‌سازی: Domain/Organization، Application/Organization و Guardian، Infrastructure store/configuration و دو Controller مدرسه؛ از IAM پلتفرم برای عملیات مدرسه استفاده نشده است. قرارداد و مسیر مطالعه در سند E27 آمده‌اند.
- پذیرش اثبات‌شده: OTP/profile تستی → ساخت پرونده از API → Enrollment موجود → دعوت و پذیرش رضایتی → Guardian Pending/activate/select → dashboard/report؛ end/revoke درخواست بعدی را می‌بندد. دو سازمان، چندفرزند، duplicate/retry، stale rowversion، Audit و constraints SQL تست شدند. fixture فقط برای پیش‌شرط سازمان/مدیر/عضویت و حالت‌های legacy/حمله است، نه خود workflow جدید.
- Validation پس از پیاده‌سازی: restore و build موفق با صفر warning/error؛ **Unit 384 + Integration 607 + Architecture 14 = 1005 passed، صفر failed/skipped** (baseline برابر 977 بود؛ 28 مورد افزوده شد). این نتیجه worktree شامل فایل‌های قبلی AI است؛ آن فایل‌ها جزو commit GAP-01 نیستند. ارسال زنده SMS/AI یا عملیات سرور انجام نشده است.
- اولین gap باز پیشنهادی اکنون **GAP-02** است؛ verdict پایلوت/Production همچنان NOT_READY می‌ماند.

### GAP-02 — محیط IIS staging، secrets و live core smoke

- هدف/اهمیت: تبدیل کنترل‌های کد به محیط قابل ارزیابی و اثبات OTP واقعی.
- وابستگی: Windows/SQL staging ایزوله، دامنه/certificate، credential مصوب و شماره تست مجاز؛ GAP-01 برای سناریوی کامل مدرسه.
- محل/نوع کار: `docs/production`، `tools/production`، deployment configuration و gate schema؛ **ops + validation، code فقط اگر تست شواهد نقص نشان داد**.
- پذیرش: artifact commit مشخص، .NET9/IIS/WS، TLS معتبر، secret بیرون Git، principal جدا، ۳۵ migration پس از GAP-01، storage ACL، log redaction، send/receive/verify/resend/restart OTP، refresh/revoke و smoke چهار نقش ثبت شوند؛ health بدون schema کافی تلقی نشود.
- وضعیت پس از کار مجاز GAP-02: **BLOCKED_OPERATIONAL_VALIDATION**؛ شواهد E28 و سند مستقل ثبت شدند. validation مخزن 1021 passed، صفر failed/skipped و build بدون warning/error است؛ ۱۶ case اضافه شد. ابزار زنده از تست عادی جداست؛ MeliPayamak code/config و فایل‌های قبلی AI تغییر نکرده‌اند.
- مانع عملیاتی مشخص: کاربر مقصد HTTP معرفی و نبود HTTPS را تأیید کرد؛ health200، profile بدون token401، ready404 و HTTPS timeout مشاهده شدند. دسترسی server-side/نسخه deployment/log هدف ارائه نشد. هیچ ورود یا پیامک زنده اجرا نشد و GAP-02 بسته نیست.

### GAP-03 — backup و restore کامل DB + storage + secrets

- هدف/اهمیت: حفاظت از داده واقعی پیش از پذیرش مدرسه.
- وابستگی: محیط GAP-02، RPO/RTO/retention مصوب و محل backup جدا.
- محل/نوع کار: `tools/production/sql` و `docs/production/BACKUP_RESTORE.md`؛ **ops + drill**.
- پذیرش: job با هشدار شکست، off-server copy، restore محیط disposable، تطبیق فایل/hash و روابط رسمی/Outbox و بازیابی secret reference، RPO/RTO اندازه‌گیری‌شده؛ بدون حذف داده هدف عادی.

### GAP-04 — MFA/step-up واقعی Platform

- هدف/اهمیت: فعال‌شدن امن مسیرهای حساس، نه خاموش‌کردن guard.
- وابستگی: انتخاب عامل و policy freshness/recovery/rotation توسط مالک؛ محیط GAP-02.
- محل/نوع کار: Identity/session assurance، Platform port و adapter، auth transport و audit؛ **code + provider + ops**.
- پذیرش: enroll/verify عامل مستقل، replay/expired factor رد، assurance به session جاری سروری متصل، recovery و revoke امن، real smoke و تست منفی last-owner/permission؛ Development/Test adapter در Prod مجاز نباشد.

### GAP-05 — Child Safety واقعی و policy سن/گروه

- هدف/اهمیت: قابل‌استفاده شدن Messenger بدون بازکردن ناامن Unknown/Minor.
- وابستگی: policy محصول برای منبع سن، authority تغییر، personal child/adult، گروه/کانال و privacy/retention؛ provider approved.
- محل/نوع کار: `User` age lifecycle، Messaging policies/service/store و provider adapter، moderation operations؛ **product + code + provider + ops**.
- پذیرش: age معتبر بدون SQL دستی، Unknown fail-closed، policy سنی واقعاً در تصمیم مصرف، allow/block/review/timeout با provider واقعی، revoke پس از network و evidence محدود، workflow تیم انسانی آزموده شود.

### GAP-08 — پایش قابل مشاهده و alert حداقلی

- هدف/اهمیت: failure قبل از شکایت مدرسه آشکار شود؛ برای pilot کم‌ریسک مهم‌تر از قابلیت تزئینی است.
- وابستگی: محیط GAP-02 و انتخاب مقصد امن log/metric/trace.
- محل/نوع کار: Api/Observability، worker telemetry و `docs/production/OBSERVABILITY.md`؛ **ops + integration محدود**.
- پذیرش: correlation یک درخواست تا خطای dependency دیده شود؛ alert SQL/storage/5xx/queue age/poison با مسئول مشخص واقعاً دریافت شود؛ secret/PII در sink نباشد.

### GAP-09 — recovery عملیاتی workerها

- هدف/اهمیت: failure نهایی فقط در DB رها نشود.
- وابستگی: GAP-08، permission و سیاست retry دستی؛ برای mutation حساس GAP-04.
- محل/نوع کار: Outbox، Notification، Reports/Exports، Messaging outbox و Platform operations؛ **code + ops**.
- پذیرش: final-failure inventory، requeue محدود/مجاز/audited با idempotency، crash بعد اثر/قبل save، poison و cancellation و IIS recycle واقعی بدون business effect تکراری؛ transaction شبکه طولانی نشود.

### GAP-10 — retention و پاک‌سازی storage/export

- هدف/اهمیت: جلوگیری از رشد دیسک و نگه‌داری نامحدود داده؛ تفکیک history رسمی و فایل موقت.
- وابستگی: policy retention/حذف و GAP-03؛ فایل evidence/رسید رسمی نباید کورکورانه purge شود.
- محل/نوع کار: Storage/FileAsset، ReportExportJob/service/output و worker مجاز؛ **code + policy + ops**.
- پذیرش: expiry/download behavior صریح، purge قابل retry، cleanup rejected/orphan/deleted و output قدیمی، حفظ bound/retained/evidence، fault disk/delete و quota/alert آزموده شوند؛ retention audit/session/OTP نیز policy روشن داشته باشد.

### GAP-16 — بستن race و recovery درخواست OTP

- هدف/اهمیت: محدودیت درخواست و نتیجه SMS تحت retry/هم‌زمانی قابل اتکا شود.
- وابستگی: provider contract idempotency/timeout و محیط تست کنترل‌شده؛ بدون افزودن retry کور.
- محل/نوع کار: `IdentityService.RequestOtpAsync`، Identity store/challenge و تست‌ها؛ **code + tests**.
- پذیرش: درخواست هم‌زمان همان شماره، تغییر challenge قدیمی، provider-accepted/DB-failed و lost response با semantics مصوب آزموده شوند؛ سقف معتبر و عدم ارسال تکراری ناخواسته اثبات شود؛ حفظ عدم logging plaintext.

### GAP-11 — سیاست و pipeline امنیت فایل

- هدف/اهمیت: عبور از signature حداقلی به سیاست پذیرش فایل امن متناسب محصول.
- وابستگی: نوع فایل و scanner/privacy مصوب، quarantine/timeout و retention.
- محل/نوع کار: Storage port/service/provider و download/binding gates؛ **code + provider/ops**.
- پذیرش: فایل مشکوک قبل Ready/bind/download قابل مصرف نباشد؛ fail-closed/scan retry و failure بدون نشت محتوا، فایل نمونه امن و fault-injection؛ این کار با AI track یکی فرض نشود.

### GAP-14 — transport واقعی و baseline ظرفیت

- هدف/اهمیت: معلوم شود target چه بار و reconnectی را تحمل می‌کند.
- وابستگی: GAP-02/08، داده مصنوعی، profile سخت‌افزار و معیار پذیرش مصوب.
- محل/نوع کار: `tools/production/load`، Hosting/Auth/Realtime و runbook نتایج؛ **tests + ops**، code فقط برای نقص اثبات‌شده.
- پذیرش: browser WebSocket/SSE auth بدون نشت token، قطع/reconnect/delta، 413/429 و spoof proxy، auth/dashboard/report/message/assignment/exam autosave/finalize/worker load با p95/p99/error/backlog ثبت شود؛ بدون بار Production/provider واقعی ناخواسته.

### GAP-21 — تضمین ترتیب paging و export نسخه‌دار

- هدف/اهمیت: جلوگیری از ترتیب غیرقطعی بین ردیف‌های یک assessment/نسخه در مرز صفحه؛ حفظ completeness گزارش.
- وابستگی: تعریف کلید واقعی ردیف هر read model؛ بدون ادعای snapshot تراکنشی تازه یا تغییر قرارداد نمره.
- محل/نوع کار: `EfDetailedReportReadService.StudentParent.cs`، سایر orderهای گزارش، exporter و regression test؛ **code + tests**.
- پذیرش: دو نسخه یک آزمون با زمان/عنوان برابر و چند release با timestamp برابر، در pageSize کوچک ردیف تکراری/گمشده ندهند؛ unique tie-break نهایی از version/release/attempt identity مناسب، total count و خروجی CSV با مجموعه مورد انتظار تطبیق یابد؛ authorization/release rules حفظ شوند.

### GAP-06 — پرداخت واقعی، callback و reconciliation

- هدف/اهمیت: شرط عرضه پولی؛ مدل سفارش به‌تنهایی فروش واقعی نیست.
- وابستگی: provider و واحد/قرارداد مالی مصوب، credential/محیط تست، GAP-02/08؛ activation durable موجود reuse شود.
- محل/نوع کار: Commerce gateway adapter، callback transport، verification/store و reconciliation worker یا workflow مصوب؛ **code + provider + ops**.
- پذیرش: server-priced order، callback authentication، verify واقعی مبلغ/واحد/reference، duplicate/replay و callback قبل/بعد retry، lost response/unknown، reconciliation و activation بدون تکرار؛ هیچ manual success bypass.

### GAP-07 — enforcement پلن و lifecycle تجاری

- هدف/اهمیت: وعده quota/entitlement با mutation واقعی تطبیق داشته باشد.
- وابستگی: policy محدودیت/overage/renewal/cancel/refund مصوب؛ GAP-06 برای سناریوی پولی و GAP-04 برای override حساس.
- محل/نوع کار: `IEntitlementService` و Commerce store، مرزهای Academic/Storage/Reports و Platform؛ **code + product/provider decision**.
- پذیرش: غیرفعال/منقضی/فاقد entitlement و آخرین quota در mutation هم‌زمان بدون check-then-write ناامن کنترل شود؛ ledger retry/idempotent و تاریخچه plan/version حفظ؛ cancel/renewal و refund فقط مطابق قرارداد مصوب.

### GAP-12 — Push و SMS عمومی اعلان

- هدف/اهمیت: تحویل خارج برنامه با provider واقعی، مستقل از OTP.
- وابستگی: vendor/consent/template/قیمت مصوب، GAP-08/09 و محیط مجاز.
- محل/نوع کار: Notification channel adapters، DI/config و delivery processor؛ **code + provider + ops**.
- پذیرش: real send، device invalid/revoke، quiet hours/timezone، suppression/retry و crash-after-send با idempotency provider؛ وضعیت unavailable قبلی خودکار delivered فرض نشود؛ payload کمینه و secrets بیرونی.

### GAP-20 — عملیات Moderation، audit و account security

- هدف/اهمیت: کد case-scoped به فرایند مسئول‌دار و قابل حسابرسی تبدیل شود.
- وابستگی: مالک policy retention/appeal/SLA، تیم reviewer و GAP-04/05/08.
- محل/نوع کار: Platform/Moderation، AdminAudit، Identity security events و runbook؛ **policy + ops + code محدود**.
- پذیرش: permission جدا review/enforce، تمرین claim/escalation/resolve، دسترسی evidence کمینه، امنیت و retention audit/log، durable security-event مقصد مصوب و recovery قابل‌ردیابی؛ shared account/impersonation اضافه نشود.

### GAP-15 — CI و artifact/release automation

- هدف/اهمیت: تکرارپذیری validation و کنترل artifact/migration؛ جای طراحی مجدد نیست.
- وابستگی: runner با SQL Server ایزوله و دسترسی registry/artifact/staging، approval انتشار.
- محل/نوع کار: workflow جدید فقط با approval، publish/migration script و release checklist؛ **CI + ops**.
- پذیرش: restore/build/تمام تست‌های واقعی و model check، artifact بدون secrets، migration artifact target-guarded، staging smoke، approval و rollback gate؛ صفر تست یا fake DB جای SQL پذیرفته نشود.

### GAP-13 — producerهای اعلان تأییدشده باقی‌مانده

- هدف/اهمیت: category موجود با رخداد تجاری تولیدشده اشتباه نشود.
- وابستگی: نیاز محصول برای Session/Invitation/Report/AccountSecurity و GAP-09/12 در صورت کانال خارجی.
- محل/نوع کار: producer همان ماژول، `NotificationOutboxContract` و mapping؛ **code + tests**.
- پذیرش: در transaction اصلی Outbox ثبت شود، recipient/resource/deep-link جاری مجاز، retry بی‌اثر تکراری و عدم نمایش private data؛ رخداد بدون نیاز واقعی ساخته نشود.

### GAP-17 — همگام‌سازی قرارداد و راهنمای جاری

- هدف/اهمیت: توسعه‌دهنده/اپراتور به snapshot قدیمی تکیه نکند.
- وابستگی: قراردادهای نهایی مراحل بسته‌شده؛ source و OpenAPI واقعی.
- محل/نوع کار: README، API guide/reference، HTML/PDF و deployment docs؛ **docs + validation**.
- پذیرش: route/DTO/تعدادها با نسخه source مشخص، semantics expiry/provider/CORS/Swagger و limitations بدون ادعای کاذب؛ خروجی‌های تاریخی از سند جاری تفکیک شوند.

### GAP-18 — scale-out فقط پس از نیاز اثبات‌شده

- هدف/اهمیت: چند instance بدون گم‌شدن realtime یا چندبرابرشدن limiter.
- وابستگی: نتایج GAP-14، topology و backplane مصوب، storage مشترک مناسب.
- محل/نوع کار: Realtime/hosting/limiter و deployment؛ **code + provider + ops**.
- پذیرش: client روی node دیگر notification بگیرد، limiter مشترک، failover/lease تحت چند node، reconnect/delta و عدم duplication اثر رسمی؛ برای تک node این کار پیش‌نیاز تصنعی نباشد.

### GAP-19 — Object Storage در صورت نیاز واقعی

- هدف/اهمیت: persistence مشترک/HA و چرخه عمر حجم بالا؛ نه تغییر پایگاه داده اصلی.
- وابستگی: نیاز scale/HA، provider و policy region/retention/backup مصوب.
- محل/نوع کار: adapter پشت `IFileStorage`، readiness و deployment؛ **code + provider + ops**.
- پذیرش: parity upload/read/delete/hash، خصوصی بودن bytes، retry و authorization فایل/export/evidence، consistency DB+object و restore واقعی؛ SQL Server همچنان مرجع metadata و مجوز باقی بماند.

قاعده اجرای ممیزی اولیه: پس از ثبت سند و validation، فقط همین سند commit/push شد. GAP-01 با approval جداگانه مالک اجرا شده و commit آن فقط فایل‌های همین gap را شامل می‌شود؛ مقایسه hash و Git status باید حفظ همه تغییرات قبلی AI را تأیید کند. اجرای هر gap باز دیگر هنوز نیازمند approval بعدی مالک پروژه است.
