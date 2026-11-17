# ممیزی نهایی BE01–BE25 و پذیرش محدودهٔ Release جاری

> ممیزی Source و دو دور کامل Regression پایان یافت؛ تغییرات این Task فقط مستندات‌اند.
> تصمیم فعلی: `NOT_READY_FOR_TEST_DEPLOY` برای تنظیمات محلیِ بررسی‌شده؛ دو مانع CFG-01/02،
> سلامت کد و قابلیت‌های Deferred را نقض نمی‌کنند.

تاریخ `2026-11-17`؛ مخزن اصلی `D:\Project\MakanApp\MakanApp-Backend`.
Source پایه، Commit نهایی واقعی MKN-BE-001 است:
`14b0271a9d3aea840e23413dacae5300fc68ebed`، شاخه
`feature/product-v6-backend-alignment` و upstream همان شاخه در `origin`.
پس از Fetch، HEAD محلی و remote برابر بودند؛ main ادغام نشد.

## روش و حدود شاهد

ترتیب استناد: Source واقعی HEAD، تست قابل اجرا، مدل طراحی EF/Migration و Snapshot تازهٔ
OpenAPI؛ سپس قراردادها و تصمیم‌های مصوب. عنوان یک Task، فایل Roadmap یا وضعیت تاریخی
`MISSING/PARTIAL/BLOCKED` شاهد نبودن قابلیت در HEAD فعلی نیست.

ابزار موجود [DocumentationSnapshot](../../tools/docs/MakanApp.DocumentationSnapshot/DocumentationSnapshot.cs)
دوباره اجرا شد؛ هیچ SQL، Hosted Worker، SMS یا AI واقعی به‌واسطهٔ Snapshot فعال نشد.
خروجی امن در `.artifacts/final-be01-be25-audit-20261117-14b0271/snapshot` و Summary/هش
کنار آن ثبت است. این پوشه private و Git-ignored است؛ تولید Snapshot به معنی اتصال سرور نیست.

۱۲ فایل محافظت‌شده شامل سه فایل unfinished AI، چهار appsettings، دو فایل خصوصی IDE/Publish
و سه فایل provider ملی‌پیامک، با SHA-256 و کپی خصوصی بررسی شدند. از baseline MKN-BE-001
تغییری نداشتند. فایل‌های AI untracked باقی می‌مانند و وارد Commit این Audit نمی‌شوند.
روی `MakanApp`، `MakanApp_Test` یا سرور هیچ SQL یا Migration اجرا نمی‌شود؛ Integration
از LocalDB اختصاصی fixture و دیتابیس‌های disposable استفاده می‌کند.

تعبیر `PASS` در جدول‌های BE، پذیرش Source و آزمون محلیِ محدودهٔ نوشته‌شده است؛ اثبات Deploy،
خرید واقعی، ارسال Push، اجرای حذف Production یا پذیرش دستگاه کاربر نیست. نتیجهٔ آمادگیِ
نسخهٔ تنظیم‌شده از این وضعیت جداست؛ [Scope](CURRENT_RELEASE_SCOPE_FA.md) و
[Blockers](CURRENT_RELEASE_BLOCKERS_FA.md) مرجع این تفکیک‌اند.

## BE01 — Contract Audit

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | قرارداد واقعی OpenAPI، Controller/DTO، Domain، مدل SQL، Migration، تست و مستندات جاری |
| Current Implementation | Host موجود، Controllers، Application contracts و ابزار Snapshot، بدون بازسازی معماری یا API |
| API Evidence | Snapshot تازه:438 path /494 operation /783 schema /91 Controller؛ JSON serializer و enumها از Host استخراج می‌شوند |
| Domain Evidence | [DomainAssembly](../../MakanApp.Domain/Common/DomainAssembly.cs) و مدل‌های ماژول‌ها؛ وابستگی Onion با Architecture Tests سنجیده می‌شود |
| Persistence Evidence | [MakanDbContext](../../MakanApp.Infrastructure/Persistence/MakanDbContext.cs)، مدل151جدولی با Keys/FKs/Indexes/Checks و concurrency در inventory |
| Tests | [PlatformOpenApiAuthenticationContractTests](../../tests/MakanApp.IntegrationTests/PlatformOpenApiAuthenticationContractTests.cs)، [PersonalClassContractTests](../../tests/MakanApp.IntegrationTests/PersonalClassContractTests.cs) و سه پروژهٔ تست |
| Migration |75 Migration موجود؛ جدید0؛ آخرین=`20261114183321_ExtendProductNotificationCoverage`؛ Pending Model Changes=NO |
| Runtime Dependency | برای Snapshot فقط Host آزمایشی/metadata؛ اتصال DB و Provider لازم نشد |
| Product Decision | مرز Release جاری طبق همین پرامپت Freeze شده؛ وضعیت قدیمی بازنویسی تاریخی نمی‌شود |
| Current Release Scope | IN SCOPE؛ تنها ممیزی و مستندسازی، مگر نقص اثبات‌شدهٔ لازم |
| Final Status | `PASS` |
| Remaining Action | نگه‌داشتن Snapshot و scope این Source؛ بستهٔ قدیمی شاهد باینری همین Commit نیست |

## BE02 — Identity / Profile

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | UserId ثابت، Profile/Bio/DOB/Avatar، تغییر امن شماره، OTP، Session و Refresh rotation |
| Current Implementation | [IdentityService](../../MakanApp.Application/Identity/IdentityService.cs)، [PhoneChangeService](../../MakanApp.Application/Identity/PhoneChangeService.cs) و [ProfilePhotoService](../../MakanApp.Application/Identity/ProfilePhotoService.cs)؛ Bio اختیاریِ omitted، مقدار قبلی را حفظ می‌کند |
| API Evidence | `POST /api/v1/auth/otp/challenges` و `/otp/verify`؛ `POST /api/v1/auth/refresh`؛ `GET /api/v1/me`، `PATCH /api/v1/me/profile`، `PUT/DELETE /api/v1/me/profile-photo` و `POST /api/v1/me/phone-change/requests/{requestId}/verify` |
| Domain Evidence | User/Person، UserCredential، OtpChallenge، UserSession/RefreshToken و PhoneChangeRequest؛ تغییر شماره Account تازه نمی‌سازد |
| Persistence Evidence | `identity.Users/Persons/UserCredentials/UserSessions/RefreshTokens/PhoneChangeRequests`؛ [EfPhoneChangeStore](../../MakanApp.Infrastructure/Identity/EfPhoneChangeStore.cs) با Serializable/range lock، یکتایی شماره و audit، سایر نشست‌ها/Refreshها را revoke می‌کند |
| Tests | [IdentityEndpointsTests](../../tests/MakanApp.IntegrationTests/IdentityEndpointsTests.cs)، [ProductV6PhoneChangeTests](../../tests/MakanApp.IntegrationTests/ProductV6PhoneChangeTests.cs)، [ConfigurableOtpAuthenticationTests](../../tests/MakanApp.IntegrationTests/ConfigurableOtpAuthenticationTests.cs)، [ProfilePhotoEndpointsTests](../../tests/MakanApp.IntegrationTests/ProfilePhotoEndpointsTests.cs) |
| Migration | `20261112145324_AddProductV6ProfileAndAddressBook`، `20261112165826_AddVerifiedPhoneChangeRequests`؛ DOB/Avatar و Refresh migrations موجود محفوظ‌اند؛ جدید0 |
| Runtime Dependency | SQL؛ Storage برای Avatar؛ ملی‌پیامک واقعی برای OTP Production/تغییر شماره؛ Regression با provider کنترل‌شده |
| Product Decision | D02 بسته؛ OTP Test=false/true و Production=true/false؛ [Startup guard](../../MakanApp.Infrastructure/Identity/OtpAuthenticationOptions.cs) Production/Staging را از Mock منع می‌کند |
| Current Release Scope | IN SCOPE؛ تغییر Authentication یا ارسال SMS واقعی در این Audit انجام نمی‌شود |
| Final Status | `PASS` |
| Remaining Action | Smoke نشست/OTP در مقصد توسط اپراتور؛ Secretهای موجود و adapter ملی‌پیامک دست‌نخورده‌اند |

## BE03 — School Application

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Draft/Documents/Submit/NeedsChanges/Resubmit/Review/Approve/Reject، مدیر اولیه، Quote/Order/Zibal/Provisioning/Recovery |
| Current Implementation | [SchoolApplicationService](../../MakanApp.Application/Organization/SchoolApplicationService.cs)، [EfSchoolApplicationStore](../../MakanApp.Infrastructure/Organization/EfSchoolApplicationStore.cs) و [Paid Provisioning](../../MakanApp.Infrastructure/Platform/EfPlatformOperationsStore.SchoolProvisioning.cs)؛ Outbox در تراکنش پرداخت و checkpointهای بعد از Commit |
| API Evidence | `GET/POST /api/v1/school-applications`؛ `POST /api/v1/school-applications/{id}/submit`، `/quote`، `/orders`؛ `POST /api/admin/v1/school-applications/{id}/decisions`، `/initial-manager/confirm` و `/provisioning/retry` |
| Domain Evidence | [SchoolApplication](../../MakanApp.Domain/Organization/SchoolApplication.cs)، Revision/Document/Decision/History و PlatformProvisioningRequest؛ انتخاب مدیر نهایی صریح و immutable؛ دو ظرفیت required/non-negative، بدون حدس فیلد حقوقی تازه |
| Persistence Evidence | `organization.SchoolApplications/Revisions/Documents/Decisions/History/Operations`، `commerce.SchoolEstablishmentOfferings/Orders/Payments` و `platform.ProvisioningRequests/OutboxMessages`؛ operation/hash، version/rowversion و refs پرداخت/مدیر |
| Tests | [ProductV6SchoolApplicationTests](../../tests/MakanApp.IntegrationTests/ProductV6SchoolApplicationTests.cs)، [Zibal School Acceptance](../../tests/MakanApp.IntegrationTests/ZibalPaymentEndpointsTests.SchoolApplications.cs) و [PlatformProvisioningLookupTests](../../tests/MakanApp.IntegrationTests/PlatformProvisioningLookupTests.cs) |
| Migration | `20261112153121_AddVersionedSchoolApplications`، `20261113065124_CompleteSchoolEstablishmentWorkflow`، `20261113103326_FinalizeSchoolManagerAndPaidProvisioning`؛ جدید0 |
| Runtime Dependency | SQL، Storage مدارک، Plan/Price/Quota مصوب، درگاه و Worker؛ تست با دادهٔ تجاری ساختگی و درگاه کنترل‌شده |
| Product Decision | D01/D17 بسته؛ D15/D16/D18 گسترش آینده‌اند؛ قیمت/دوره/Quota حدسی ساخته نمی‌شود |
| Current Release Scope | IN SCOPE؛ فروش واقعی آموزشگاه فقط پس از Configuration تجاری معتبر؛ نبود آن به‌تنهایی کل Release را رد نمی‌کند |
| Final Status | `PASS` |
| Remaining Action | `PRODUCTION_SCHOOL_COMMERCIAL_CONFIGURATION=BLOCKED_CONFIGURATION`؛ تنظیم واقعی و پذیرش پرداخت/Provisioning مقصد؛ [راهنما](SCHOOL_COMMERCIAL_CONFIGURATION_FA.md) |

## BE04 — Organization / Guardian

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | چندسازمانی/چندنقشی، پایان عضویت، رابطهٔ Guardian و جدایی Child ACL |
| Current Implementation | OrganizationService/OrganizationPersonService، GuardianService و [SchoolMembershipManagementService](../../MakanApp.Application/Organization/SchoolMembershipManagementService.cs)؛ دعوت و رضایت صریح از Enrollment جداست |
| API Evidence | `GET /api/v1/workspaces/me`؛ `POST /api/v1/workspaces/select`؛ `GET /api/v1/guardian/children`؛ `POST /api/v1/guardian/children/{organizationPersonId}/select`؛ API مدیریت `/api/v1/organization/members/{membershipId}` و `/guardian-relations` |
| Domain Evidence | Membership/RoleAssignment/OrganizationPerson و GuardianRelation؛ Role سراسری User نداریم؛ Manager می‌تواند Manager دیگر را فقط با محافظ آخرین مدیر مدیریت کند |
| Persistence Evidence | `organization.Memberships/RoleAssignments/OrganizationPersons/MembershipManagementOperations`، `guardian.GuardianRelations`؛ [مدیریت عضویت](../../MakanApp.Infrastructure/Organization/EfSchoolMembershipManagementStore.cs) تراکنشی و [Guardian query](../../MakanApp.Infrastructure/Guardian/EfGuardianStore.cs) بر رابطه/سازمان/Person جاری متکی است |
| Tests | [OrganizationEndpointsTests](../../tests/MakanApp.IntegrationTests/OrganizationEndpointsTests.cs)، [GuardianEndpointsTests](../../tests/MakanApp.IntegrationTests/GuardianEndpointsTests.cs)، [OrganizationPersonOnboardingEndpointsTests](../../tests/MakanApp.IntegrationTests/OrganizationPersonOnboardingEndpointsTests.cs) و School Guardian/Role regression |
| Migration | `AddOrganizationMemberships`، `AddOrganizationPersons`، `AddGuardianRelations`، `20261014214734_AddOperationalPersonGuardianOnboarding`، `20261104224922_AddSchoolMembershipManagementOperations`؛ جدید0 |
| Runtime Dependency | SQL و نشست معتبر؛ هیچ تطبیق خودکار نام/شماره یا Person merge انجام نمی‌شود |
| Product Decision | Guardian به معنی دسترسی private Direct نیست؛ توسعهٔ والد در فضای رسمی D14 باز است |
| Current Release Scope | IN SCOPE؛ revoke جاری باید API/Realtime/File بعدی را نیز محدود کند |
| Final Status | `PASS` |
| Remaining Action | گسترش D14 تصمیم آینده است؛ scope فعلی با Source و Regression حفظ می‌شود |

## BE05 — Academic Core

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Period/Course/Class/Lesson/Session/Schedule/Attendance سازمانی |
| Current Implementation | AcademicService، [ClassLessonService](../../MakanApp.Application/Academic/ClassLessonService.cs) و AcademicSessionService با revision حضور |
| API Evidence | `/api/v1/academic/periods`، `/courses`، `/classes`؛ `GET/POST /api/v1/academic/classes/{classId}/lessons` و `/sessions`؛ `GET /api/v1/academic/schedule`؛ Attendance در SessionsController |
| Domain Evidence | AcademicPeriod/Course/Class/Enrollment/TeacherAssignment/ClassLesson/Session/ScheduleRule/AttendanceRevision؛ زمان رسمی سرور، برنامهٔ محلی/TimeZone و revision از هم جدا هستند |
| Persistence Evidence | جدول‌های متناظر `academic` با FKهای سازمان/کلاس، rowversion، unique فعال و transaction ظرفیت/حضور؛ [Attendance use case](../../MakanApp.Application/Academic/AcademicSessionService.Attendance.cs) Enrollment معتبر همان جلسه را می‌سنجد |
| Tests | [AcademicEndpointsTests](../../tests/MakanApp.IntegrationTests/AcademicEndpointsTests.cs)، [SessionAttendanceEndpointsTests](../../tests/MakanApp.IntegrationTests/SessionAttendanceEndpointsTests.cs) و تست‌های Lesson/School read |
| Migration | `20260930072644_AddAcademicFoundation`، `20260930123228_AddSessionsAndAttendance`، `20261028202239_CompleteSchoolManagement`؛ جدید0 |
| Runtime Dependency | SQL، نشست/عضویت/TeacherAssignment یا دسترسی آموزشی جاری |
| Product Decision | Gateهای PersonalClass به هستهٔ سازمانی تسری پیدا نمی‌کنند |
| Current Release Scope | IN SCOPE؛ خاموشی Academic مستقل موجب خاموشی School نمی‌شود |
| Final Status | `PASS` |
| Remaining Action | Smoke دادهٔ واقعی مقصد؛ ظرفیت یا Session واقعی جدید در Audit ساخته نمی‌شود |

## BE06 — Assignments سازمانی

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Draft/Publish/Recipients، Submission و Attachments، Evaluation/Release/Correction |
| Current Implementation | [AssignmentService](../../MakanApp.Application/Assessment/AssignmentService.cs)، SubmissionService و EvaluationService؛ Context جاری در transaction دوباره بررسی می‌شود |
| API Evidence | `POST /api/v1/academic/classes/{classId}/assignments`؛ `POST /api/v1/academic/assignments/{assignmentId}/attempts`؛ Submit و Evaluation/Release در SubmissionsController/EvaluationsController |
| Domain Evidence | Assignment/AssignmentVersion، AssignmentRecipient، SubmissionAttempt، [EvaluationRevision](../../MakanApp.Domain/Assessment/EvaluationRevision.cs) و GradeRelease؛ MaxScore نسخه‌ای، decimal(9,2) و سقف نمرهٔ معتبر؛ نسخهٔ منتشرشده درجا عوض نمی‌شود |
| Persistence Evidence | `assessment.Assignments/AssignmentVersions/AssignmentRecipients/SubmissionAttempts/SubmissionAttachments/EvaluationRevisions/GradeReleases`؛ unique attempt/operation، rowversion، قفل Recipient و رسید نهایی |
| Tests | [AssignmentEndpointsTests](../../tests/MakanApp.IntegrationTests/AssignmentEndpointsTests.cs)، [SubmissionConcurrencyEndpointsTests](../../tests/MakanApp.IntegrationTests/SubmissionConcurrencyEndpointsTests.cs)، [EvaluationLifecycleEndpointsTests](../../tests/MakanApp.IntegrationTests/EvaluationLifecycleEndpointsTests.cs) و [AcademicAssignmentAttachmentAcceptanceTests](../../tests/MakanApp.IntegrationTests/AcademicAssignmentAttachmentAcceptanceTests.cs) |
| Migration | `AddAssignmentFoundation`، `AddAssignmentSubmissions`، `AddAssignmentEvaluations` و گسترش scope/attachment موجود؛ جدید0 |
| Runtime Dependency | SQL، Storage برای پیوست و دسترسی جاری معلم/دانش‌آموز/Guardian طبق use case |
| Product Decision | PersonalClass Assignment برابر `DEFERRED_APPROVED`؛ Gate false، engine و تاریخچه محفوظ |
| Current Release Scope | IN SCOPE فقط مسیر Organization؛ ارزیابی/نمرهٔ تازهٔ ساختگی یا Rubric اضافه نمی‌شود |
| Final Status | `PASS` |
| Remaining Action | فعال‌سازی آیندهٔ PersonalClass موضوع Task مستقل است؛ مجوز file با صرف FileId ایجاد نمی‌شود |

## BE07 — Exams سازمانی

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Author/Publish، Attempt/Limit/Deadline/Freeze، Autosave/Lease، Finalize/Receipt، Grade/Release/Correction و Attachment |
| Current Implementation | ExamService، [ExamAttemptService](../../MakanApp.Application/Assessment/ExamAttemptService.cs)، ExamAnswerService، ExamFinalizationService و ExamGradingService |
| API Evidence | `POST /api/v1/academic/classes/{classId}/exams`؛ Start/Answers/WriteLease/Finalize در ExamsController؛ `POST /api/v1/academic/exam-attempts/{attemptId}/grade/release` و `/grade/corrections` |
| Domain Evidence | ExamVersion/QuestionVersion/ExamAttemptQuestion، [ExamAttempt](../../MakanApp.Domain/Assessment/ExamAttempt.cs)، AnswerRevision/ExamFinalAnswer/ExamGradeRevision؛ deadline از Min مدت و انتهای بازه، lease نسخه‌دار و AnswerSetVersion قفل‌شده |
| Persistence Evidence | جدول‌های Exam/Question/Attempt/Answer/Grade در `assessment`؛ receipt/operation/sequence، rowversion و Serializable برای رقابت شروع/پایان/ذخیره |
| Tests | [ExamAttemptEndpointsTests](../../tests/MakanApp.IntegrationTests/ExamAttemptEndpointsTests.cs)، [ExamAnswerEndpointsTests](../../tests/MakanApp.IntegrationTests/ExamAnswerEndpointsTests.cs)، [ExamFinalizationEndpointsTests](../../tests/MakanApp.IntegrationTests/ExamFinalizationEndpointsTests.cs)، [ExamGradingEndpointsTests](../../tests/MakanApp.IntegrationTests/ExamGradingEndpointsTests.cs) و AcademicExamAttachment tests |
| Migration | `AddExamAuthoringFoundation`، `AddExamAttempts`، `AddExamAnswerRevisions`، `AddExamFinalization`، `AddExamGradingAndRelease`، `20261030060025_AddExamAnswerAttachments`؛ جدید0 |
| Runtime Dependency | SQL، Storage و ساعت سرور؛ client time مجوز Deadline یا Grade ایجاد نمی‌کند |
| Product Decision | PersonalClass Exam برابر `DEFERRED_APPROVED`؛ Gate false، engine مشترک حذف نمی‌شود |
| Current Release Scope | IN SCOPE مسیر Organization؛ Quiz پیام‌رسان Exam آموزشی محسوب نمی‌شود |
| Final Status | `PASS` |
| Remaining Action | پذیرش Workflow مقصد پس از نصب؛ آزمون محلی ادعای اجرای امتحان Production نیست |

## BE08 — Reports

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Metrics واقعی، فیلتر/Page، ACL، Export/Download و Worker recovery |
| Current Implementation | DashboardService، DetailedReportService، [ReportExportService](../../MakanApp.Application/Reports/Exports/ReportExportService.cs) و [ReportExportProcessor](../../MakanApp.Infrastructure/Reports/Exports/ReportExportDispatcher.cs) موجود |
| API Evidence | `GET /api/v1/reports/manager/assessments`؛ `POST /api/v1/reports/exports`؛ `GET /api/v1/reports/exports/{jobId}` و `/download`؛ فقط CSV موجود |
| Domain Evidence | ReportExportJob با Claim/Attempt/Completion و snapshot فیلتر/زمان داده؛ گزارش از دادهٔ persisted و release معتبر ساخته می‌شود |
| Persistence Evidence | `reports.ReportExportJobs`، FileAssets و UsageReservation متصل؛ Worker پیش از تولید و انتشار ACL جاری را بررسی می‌کند؛ Retry خروجی قابل استفاده را reuse می‌کند |
| Tests | [ReportExportEndpointsTests](../../tests/MakanApp.IntegrationTests/ReportExportEndpointsTests.cs)، [ReportExportRecoveryTests](../../tests/MakanApp.IntegrationTests/ReportExportRecoveryTests.cs) و DetailedReport/Dashboard regression |
| Migration | `20261012142715_AddAsynchronousReportExports` و `20261029122246_BindReportExportsToCommercialReservations`؛ جدید0 |
| Runtime Dependency | SQL، Storage، Export Worker و نقش/منبع/Guardian جاری |
| Product Decision | D08: گسترش گزارش آموزشی PersonalClass آینده؛ TTL دانلود یا retention تازه حدس زده نمی‌شود |
| Current Release Scope | IN SCOPE گزارش سازمانی موجود؛ دادهٔ نمونه جای metric واقعی قرار نمی‌گیرد |
| Final Status | `PASS` |
| Remaining Action | پذیرش حجم/بازیابی مقصد؛ D08 بیرون محدودهٔ این Release |

## BE09 — PersonalClass Lifecycle

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Draft/Submitted/UnderReview/NeedsChanges/Approved/Rejected/Cancelled، Archive و Recovery |
| Current Implementation | PersonalClassFormationService، [PersonalClassLifecycleService](../../MakanApp.Application/Academic/PersonalClassLifecycleService.cs)، EfPersonalClassFormationStore و EfPersonalClassStore.Lifecycle |
| API Evidence | `GET/POST /api/v1/personal-class-requests`، Submit/Cancel/History همان پرونده؛ Review در `/api/admin/v1/personal-class-requests/{id}/review`؛ Activate/Suspend/Archive کلاس از مسیر موجود |
| Domain Evidence | [PersonalClassRequest](../../MakanApp.Domain/Academic/PersonalClassRequest.cs) و PersonalClass؛ تصمیم با Class/Manager نهایی منجمد، Request/Communication readiness از وضعیت آموزشی جدا |
| Persistence Evidence | `academic.PersonalClassRequests/PersonalClasses` و Audit؛ operation/hash/rowversion، Serializable و Source refs برای retry همان Approval؛ Archive تاریخچه را حذف نمی‌کند |
| Tests | [PersonalClassV2EndpointsTests](../../tests/MakanApp.IntegrationTests/PersonalClassV2EndpointsTests.cs)، [PersonalClassV2RecoveryTests](../../tests/MakanApp.IntegrationTests/PersonalClassV2RecoveryTests.cs)، [PersonalClassV2MigrationTests](../../tests/MakanApp.IntegrationTests/PersonalClassV2MigrationTests.cs) |
| Migration | `20261103073219_AddReviewedPersonalClassLifecycle`؛ جدید0 |
| Runtime Dependency | SQL؛ Hosting Entitlement و آماده‌بودن دو فضای رسمی طبق state فعلی |
| Product Decision | D09: Restore/Reopen بعد از Archive باز است؛ فعال‌سازی از Suspended با Restore کلاس Archived یکسان فرض نمی‌شود |
| Current Release Scope | IN SCOPE Lifecycle موجود؛ Organization صوری یا restore تازه ساخته نمی‌شود |
| Final Status | `PASS_CURRENT_POLICY` |
| Remaining Action | D09 برای توسعهٔ آینده؛ Recovery مصوب فقط همان checkpoint/Repair موجود است |

## BE10 — PersonalClass Membership

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | دعوت/JoinRequest، ظرفیت، نقش محلی، انتقال مدیر و Last Manager Guard |
| Current Implementation | PersonalClassLifecycleService و [EfPersonalClassStore](../../MakanApp.Infrastructure/Academic/EfPersonalClassStore.cs) با partialهای Admission/Lifecycle؛ Manager مسئول، Teacher و Learner مستقل از Host مالی |
| API Evidence | `POST /api/v1/personal-classes/{id}/join-requests` و `/manager-transfers`؛ `GET /api/v1/personal-classes/{id}/memberships`؛ دعوت/پذیرش و تصمیم انتقال از APIهای PersonalClass موجود |
| Domain Evidence | PersonalClassInvitation/Participant/JoinRequest/ManagerTransfer؛ JoinRequest فقط با فعال‌سازی صریح مدیر؛ دانستن ClassId محتوا یا عضویت نمی‌سازد |
| Persistence Evidence | جدول‌های متناظر `academic` با unique عضویت/operation و rowversion؛ range/class lock؛ ظرفیت فقط Learner، Legacy بدون ظرفیت صریح پذیرش تازه ندارد؛ مسئول آخر بدون انتقال پذیرفته‌شده/Archive خارج نمی‌شود |
| Tests | PersonalClassV2Endpoints/Recovery، [PersonalClassInvitationIdentityTests](../../tests/MakanApp.IntegrationTests/PersonalClassInvitationIdentityTests.cs)، [PlatformPersonalClassMembershipReadTests](../../tests/MakanApp.IntegrationTests/PlatformPersonalClassMembershipReadTests.cs) و رقابت‌های عضویت |
| Migration | `AddReviewedPersonalClassLifecycle` و `20261103074858_BindPersonalClassInvitationRoles`؛ جدید0 |
| Runtime Dependency | SQL، Hosting Subscription/Quota میزبان؛ Student برای صرف مشارکت اشتراک میزبان نمی‌خرد |
| Product Decision | مدیریت/مالکیت مالی و مجوز Academic یکسان نیست؛ Platform فقط Manager را grant می‌کند و Teacher از Manager مجاز delegation می‌گیرد |
| Current Release Scope | IN SCOPE عضویت/مدیریت؛ Academic gateهای BE25 همچنان false |
| Final Status | `PASS` |
| Remaining Action | Smoke دعوت/انتقال با حساب‌های مجاز مقصد؛ تغییر مالک مالی یا grant خودکار از انتقال مدیر استنتاج نشود |

## BE11 — Official Spaces

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | فضای رسمی Organization/Class/PersonalClass، یکتایی Group/Channel، Provision/Repair/Sync/Revoke و badge معتبر |
| Current Implementation | OfficialClassConversationService، OfficialSpace provisioning و [OfficialSpaceAccessQuery](../../MakanApp.Infrastructure/Messaging/OfficialSpaceAccessQuery.cs)؛ فضا system-managed است و از UI context مجوز نمی‌گیرد |
| API Evidence | `GET /api/v1/organization/official-spaces`؛ `GET /api/v1/organization/classes/{classId}/official-spaces`؛ PersonalClass Spaces/Repair و ابزار [OfficialSpacesMaintenance](../../tools/organization/MakanApp.OfficialSpacesMaintenance/MakanApp.OfficialSpacesMaintenance.csproj) |
| Domain Evidence | ConversationManagementPolicy.SystemManagedAcademic، OfficialSpaceProvision، Scope/Source و نوع Group/Channel؛ گروه دستی با نام مشابه badge رسمی نمی‌گیرد |
| Persistence Evidence | `messaging.OfficialSpaceProvisions/Conversations/ConversationParticipants`؛ unique Source/Type، state Provisioning و source grants از Role/Enrollment/TeacherAssignment یا عضویت کلاس مستقل جاری |
| Tests | PersonalClassV2Messaging، [MessengerHistoryVisibilityTests](../../tests/MakanApp.IntegrationTests/MessengerHistoryVisibilityTests.cs)، [MessengerHistoryPersonalClassTests](../../tests/MakanApp.IntegrationTests/MessengerHistoryPersonalClassTests.cs) و تست‌های OfficialSpaces |
| Migration | `20261030093927_AddOfficialOrganizationAndClassSpaces`، migrations موجود PersonalClass؛ جدید0 |
| Runtime Dependency | SQL، Worker/Repair صریح و Source معتبر؛ پایان منبع، دسترسی بعدی را می‌بندد |
| Product Decision | D14 توسعهٔ آیندهٔ والد باز؛ Guardian به‌تنهایی عضویت گسترده یا دسترسی چت خصوصی نمی‌سازد |
| Current Release Scope | IN SCOPE؛ Repair بدون مجوز یا recreate تاریخچه مجاز نیست |
| Final Status | `PASS_CURRENT_POLICY` |
| Remaining Action | Audit/Repair فقط در Task عملیاتی مجاز؛ این ممیزی هیچ repair یا backfill روی DB عادی اجرا نکرد |

## BE12 — Account Messaging / MKN-BE-001

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Account inbox، تاریخچهٔ persistent، fresh login/device، pagination/reconnect و همهٔ انواع گفتگو مستقل از role/workspace نمایشی با ACL جاری |
| Current Implementation | [AccountInboxService](../../MakanApp.Application/Messaging/AccountInboxService.cs)، MessagingService و [AccountInboxAccessQuery](../../MakanApp.Infrastructure/Messaging/AccountInboxAccessQuery.cs)؛ تغییر MKN فقط resolver خواندن Messages/Delta را از selected workspace جدا کرد |
| API Evidence | `GET /api/v1/conversations/account`، `/conversations/{conversationId}/summary`، `/messages`، `/changes` و `/api/v1/realtime/account-inbox/changes`؛ فهرست legacy `GET /api/v1/conversations` همچنان scoped است |
| Domain Evidence | Conversation/Participant/Message با UserId ثابت، Sequence، تاریخ عضویت و visibility؛ محتوای SQL به Session وابسته نیست، Cursor حساب به User/Session/filter/expiry بسته است |
| Persistence Evidence | `messaging.Conversations/ConversationParticipants/Messages/ChangeEvents/RealtimeOutbox`؛ منبع رسمی دوباره بررسی می‌شود؛ MKN هیچ جدول یا Migration تازه نداشت |
| Tests | [MessengerHistoryAcceptanceTests](../../tests/MakanApp.IntegrationTests/MessengerHistoryAcceptanceTests.cs)، Visibility/PersonalClass و [AccountInboxSummaryTests](../../tests/MakanApp.UnitTests/Messaging/AccountInboxSummaryTests.cs):75 پیام،4 صفحه،5 نوع گفتگو، login/device/reconnect، retry و6 نوع revoke |
| Migration | Messaging migrations موجود؛ MKN و این Audit جدید0 |
| Runtime Dependency | SQL و SignalR موجود؛ Replay حساب invalidation است، نه پیام؛ کلاینت محتوای authoritative را از REST/Delta مجاز می‌خواند |
| Product Decision | سیاست Messaging جاری محفوظ؛ Write/Download/Hub به بهانهٔ اصلاح History مجوز گسترده نمی‌گیرند |
| Current Release Scope | IN SCOPE؛ Commit کد=`718451ff3eba1831bd6d18c1e9693713a415d817`، Commit نهایی/مبنای Audit=`14b0271a9d3aea840e23413dacae5300fc68ebed` |
| Final Status | `PASS_CURRENT_POLICY` |
| Remaining Action | پذیرش UI/سرور جدا؛ [Handoff](MESSENGER_HISTORY_FRONTEND_HANDOFF_FA.md) و [گزارش](MESSENGER_HISTORY_FIX_REPORT_FA.md) مرز route/ACL/cursor را توضیح می‌دهند؛ Data Repair اجرا نشد |

## BE13 — Personal Groups / Channels / P2

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Group/Channel، Member/Admin، Invitations/JoinRequests، Folders/Saved، Poll/Quiz/Checklist و Scheduled/Silent Messages |
| Current Implementation | ConversationManagement/Invitation/AdvancedManagement/Topic/Poll/Checklist، MessagingPresentation و ScheduledMessageService؛ reuse زیرساخت موجود P2 |
| API Evidence | ConversationsController؛ `POST /api/v1/conversations/{conversationId}/invitations` و `/checklists`؛ `POST /api/v1/conversations/{conversationId}/messages/{messageId}/poll/vote` و `/poll/close`؛ `/scheduled-messages` و APIهای folders/saved موجود |
| Domain Evidence | ConversationInvitation/InitialTarget، Poll/Vote/QuizAnswer، Checklist/Item/PersonalState، ScheduledMessage؛ Poll Anonymous/NonAnonymous، Quiz تک‌پاسخ نهایی و Close نهایی؛ رأی SQL/تراکنش منبع حقیقت است |
| Persistence Evidence | جدول‌های Invitations/InviteLinks/JoinRequests/Polls/Selections/QuizAnswers/Checklists/Folders/Saved/Scheduled در `messaging`؛ unique operation/vote و نسخه/قفل Close؛ دعوت اولیه پیش‌فرض7روز، Retry انقضا را تمدید نمی‌کند |
| Tests | [MessagingP2FinalRegressionTests](../../tests/MakanApp.IntegrationTests/MessagingP2FinalRegressionTests.cs)، PollConcurrency/Authorization، ChecklistConcurrency/History، ScheduledRecovery و Management tests |
| Migration | `AddMessagingPersonalOrganization`، `AddConversationInvitesAndRestrictions`، `AddConversationTopics`، `AddScheduledAndSilentMessages`، `AddConversationPollsAndQuiz`، `AddConversationChecklists`؛ جدید0 |
| Runtime Dependency | SQL، Outbox/Worker زمان‌بندی و ACL/Block/Privacy/Safety جاری در ارسال/رأی/تحویل |
| Product Decision | قرارداد Poll/Quiz و Forward متنیِ بدون رأی/CorrectOption/Explanation Freeze شده؛ workflow تأیید ReviewRequired تازه ساخته نمی‌شود |
| Current Release Scope | IN SCOPE قابلیت‌های موجود؛ Voice/Video/Live از صرف P2 استنتاج نمی‌شود |
| Final Status | `PASS_CURRENT_POLICY` |
| Remaining Action | Smoke مقصد؛ تنظیم Presence/Typing موجود یا سیاست Safety در این Audit عوض نشد |

## BE14 — Address Book v2

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | مخاطب غیرعضو/چندشماره‌ای، Consent/Device Sync، Delta/Cursor/Tombstone، Multi-device، stale protection، Link خصوصی و حذف حساب |
| Current Implementation | [AddressBookService](../../MakanApp.Application/Messaging/AddressBookService.cs)، [EfAddressBookStore](../../MakanApp.Infrastructure/Messaging/EfAddressBookStore.cs) و [AddressBookProtection](../../MakanApp.Infrastructure/Messaging/AddressBookProtection.cs) |
| API Evidence | `PUT /api/v1/address-book/devices/consent`؛ `POST /api/v1/address-book/sync`؛ `GET /api/v1/address-book/delta` |
| Domain Evidence | AddressBookEntry/ContactPhone/ContactSyncDevice/ContactSyncOperation/ContactSyncState/ContactDeviceBinding؛ نام Manual با Sync دستگاه بی‌صدا عوض نمی‌شود؛ Tombstone resurrect نمی‌شود |
| Persistence Evidence | جدول‌های متناظر `messaging`؛ ProtectedContent، HMAC phone index، owner version/operation، Cursor رمز‌شده با Owner/Device/Consent/Version، تراکنش مالک و ExpectedServerVersion |
| Tests | [ProductV6AddressBookTests](../../tests/MakanApp.IntegrationTests/ProductV6AddressBookTests.cs)، ProductV6AccountContextTests، AccountDeletionAcceptanceTests و DataProtection persistence |
| Migration | `20261112145324_AddProductV6ProfileAndAddressBook` و lifecycle حذف موجود؛ جدید0 |
| Runtime Dependency | SQL، Identity key موجود و Data Protection Ring پایدار؛ محتوای دفترچه وارد Log یا Snapshot عمومی نمی‌شود |
| Product Decision | D12 بسته: Link فقط reference خصوصی مالک؛ مجوز Direct/ContactsAudience یا Person merge نمی‌سازد. D03 TTL/OS-delete باز است |
| Current Release Scope | IN SCOPE Sync صریح؛ نبود contact در snapshot دستگاه، حذف cloud فرض نمی‌شود |
| Final Status | `PASS_CURRENT_POLICY` |
| Remaining Action | Ring/Restore مقصد عملیاتی است؛ D03 تصمیم آینده، نه failure Sync فعلی |

## BE15 — Files / Library / Sources

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Bound ACL، رد direct File IDOR، Delete guard، Safety/ReviewRequired، Archive/AI revoke و Library Hidden |
| Current Implementation | StorageService، [EfFileAssetBoundAccessResolver](../../MakanApp.Infrastructure/Storage/EfFileAssetBoundAccessResolver.cs)، EfEducationalSourceStore و TerminalFileCleanupProcessor؛ owner fallback از ACL منبع حساس عبور نمی‌کند |
| API Evidence | FilesController؛ `GET/POST /api/v1/sources`، `GET /api/v1/sources/{id}` و Download/Archive/Version؛ `/api/admin/v1/library/sources` فقط با permission Platform |
| Domain Evidence | FileAsset، EducationalSource/Version و SafetyIncident؛ Ready/Retained فایل، Published/Visible منبع و Allowed Safety سه مفهوم مستقل‌اند |
| Persistence Evidence | `storage.FileAssets`، `knowledge.EducationalSources/Versions` و attachment refs؛ BindingQuery از حذف منبعِ referenced جلوگیری می‌کند؛ Cleanup terminal از Ready orphan/retention حدسی جداست |
| Tests | [FilesLibraryLifecycleTests](../../tests/MakanApp.IntegrationTests/FilesLibraryLifecycleTests.cs)، [FilesLibraryCaseBindingTests](../../tests/MakanApp.IntegrationTests/FilesLibraryCaseBindingTests.cs)، AcademicAssignment/ExamAttachment، [MessagingSafetyReviewContractTests](../../tests/MakanApp.IntegrationTests/MessagingSafetyReviewContractTests.cs) و Knowledge revoke |
| Migration | `AddFileStorageFoundation`، `20261030133521_AddScopedEducationalSources` و migrations attachment موجود؛ BE15/این Audit جدید0 |
| Runtime Dependency | SQL/Storage، provider داخلی Safety و Source ACL جاری |
| Product Decision | `LIBRARY_PUBLIC_DOWNLOAD=PRODUCT_POLICY_DISABLED`، `LIBRARY_AI=PRODUCT_POLICY_DISABLED`؛ تأیید رسانه `PRODUCT_DECISION_REQUIRED`؛ ReviewRequired نه Delivered است نه مجوز دانلود گیرنده |
| Current Release Scope | IN SCOPE سیاست فعلی؛ [ماتریس مجوز](FILES_LIBRARY_ACCESS_MATRIX_FA.md)؛ TTL/purge یا Human Review تازه ساخته نمی‌شود |
| Final Status | `PASS_CURRENT_POLICY` |
| Remaining Action | مرز دانلود Library، Hidden و workflow باز رسانه محفوظ؛ Storage مقصد جداگانه پذیرش می‌شود |

## BE16 — Notifications

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Event coverage، Durable Outbox، Dedup، Preference، Deep Link جاری، revoke/privacy و recovery |
| Current Implementation | [ProductNotificationWriter](../../MakanApp.Infrastructure/Notification/ProductNotificationWriter.cs)، EfNotificationStore، ProductNotificationAccess/AcademicNotificationAccessQuery و NotificationDeliveryProcessor؛ hook عمومی SaveChanges ساخته نشده |
| API Evidence | `GET /api/v1/notifications`؛ `POST /api/v1/notifications/{notificationId}/read`؛ `GET /api/v1/notifications/{notificationId}/target`؛ `/api/v1/notification-preferences` و `/api/v1/notifications/devices` |
| Domain Evidence | Notification، NotificationPreference، NotificationDeliveryAttempt و PushDeviceRegistration؛ reference رویداد با متن عمومی، بدون payload خصوصی منبع |
| Persistence Evidence | جدول‌های `notification` و OutboxMessages؛ SourceEvent/Recipient dedup، Claim/Attempt و current-access query پیش از Page/Target/Delivery؛ rollback رویداد از rollback تجاری جدا نمی‌شود |
| Tests | [Be16AcademicNotificationAccessTests](../../tests/MakanApp.IntegrationTests/Be16AcademicNotificationAccessTests.cs)، [Be16NotificationTransactionTests](../../tests/MakanApp.IntegrationTests/Be16NotificationTransactionTests.cs)، [Be16PersonalClassNotificationTests](../../tests/MakanApp.IntegrationTests/Be16PersonalClassNotificationTests.cs)، Be16ZibalNotification و NotificationWorkerRecovery |
| Migration | `AddNotificationFoundation`، `AddNotificationPreferencesAndDelivery`، `ExtendProductNotificationTargets` و `20261114183321_ExtendProductNotificationCoverage`؛ جدید0 |
| Runtime Dependency | SQL و Worker؛ WebPush/FCM/SMS عمومی برای پذیرش Runtime این نسخه لازم نیستند |
| Product Decision | پوشش موجود در [ماتریس رویداد](NOTIFICATION_EVENT_COVERAGE_FA.md)؛ Push/SMS Runtime طبق freeze جدید Deferred است و adapterها محفوظ‌اند |
| Current Release Scope | IN SCOPE اعلان داخلی و زیرساخت؛ `PRODUCTION_PUSH_RUNTIME=DEFERRED_CURRENT_RELEASE`؛ Live Web/FCM=`NOT_REQUIRED_CURRENT_RELEASE` |
| Final Status | `PASS` |
| Remaining Action | Smoke داخلی مقصد؛ device/credential واقعی Push در Task آینده؛ نتیجهٔ محلی «دریافت واقعی Push» نیست |

## BE17 — Commerce / Entitlement / Quota

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Catalog/Audience/Price/PlanVersion، Entitlement/Quota، خرید/تمدید/Upgrade/Downgrade، Reservation، School و Host مستقل |
| Current Implementation | CommercialSubscriptionService/CommercialAdminService، [EfCommercialStore](../../MakanApp.Infrastructure/Commerce/EfCommercialStore.cs) و [Capacity](../../MakanApp.Infrastructure/Commerce/EfCommercialStore.Capacity.cs)؛ frozen commercial terms در Order و تغییر نسخه‌دار قرارداد |
| API Evidence | `GET /api/v1/commerce/catalog`؛ Quote و Summary شخصی/سازمانی در CommercialSubscriptionsController؛ `POST /api/v1/commerce/orders`؛ قرارداد Platform در PlatformCommercialContractsController |
| Domain Evidence | Plan/PlanVersion/Price/Entitlement/Quota، SubscriptionOwner/Subscription، UsageReservation/Ledger و Order؛ Individual=UserId و Organization=OrganizationId؛ LegacyPendingContract فعال نامحدود واقعی نیست |
| Persistence Evidence | جدول‌های `commerce`؛ Lock مالک، Operation/period keys و تاریخ انجماد؛ ظرفیت فایل Ready/Retained با EXISTS یک‌بار شمرده می‌شود؛ quota تاریخی با capacity جاری مخلوط نمی‌شود |
| Tests | [Be17CommercialPolicyTests](../../tests/MakanApp.UnitTests/Commerce/Be17CommercialPolicyTests.cs)، CommercialSubscription/Commerce/Zibal acceptance؛ آزمون والد/دوفرزند جدایی Order/Payment/Usage را می‌سنجد |
| Migration | `AddCommerceFoundation`، `AddOrderAndPaymentFoundation`، `20261029111645_AddCommercialOwnershipPeriodsAndPersonalClasses` و موجود؛ BE17/این Audit جدید0 |
| Runtime Dependency | SQL و دادهٔ PlanVersion/Price/Quota مصوب؛ برای خرید واقعی Zibal config جدا لازم است |
| Product Decision | D05 باز؛ `FAMILY_SHARED_SUBSCRIPTION=PRODUCT_DECISION_REQUIRED` برای آینده؛ وضعیت Current Release برابر `DEFERRED_PRODUCT_DECISION`؛ Legacy بدون قرارداد معتبر commercial enforcement حدسی نمی‌گیرد |
| Current Release Scope | IN SCOPE سیاست مالکیت مستقل فعلی؛ [ماتریس](COMMERCE_PRODUCT_MATRIX_FA.md)؛ تاریخ/قیمت/Quota/مصرف گذشته حدسی backfill نمی‌شود |
| Final Status | `PASS_CURRENT_POLICY` |
| Remaining Action | تنظیم تجاری واقعی School جدا؛ Family/Pooling و Store Billing در این مرحله ساخته نمی‌شوند |

## BE18 — Payments / Zibal

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Zibal Request/Callback/Server Verify، بازیابی201/Inquiry، duplicate/concurrent callback، Payment/Outbox/Fulfillment |
| Current Implementation | [ZibalPaymentGateway](../../MakanApp.Infrastructure/Commerce/ZibalPaymentGateway.cs)، OrderPaymentService/Recovery و EfOrderPaymentStore؛ نتیجه201 به‌تنهایی موفقیت نیست و Inquiry باید Ref/Attempt/Amount/Currency معتبر داشته باشد |
| API Evidence | `POST /api/v1/commerce/orders/{orderId}/payments`؛ `GET /api/integrations/v1/commerce/payments/zibal/callback`؛ Recovery/Receipt در [PaymentRecoveryController](../../MakanApp.Api/Controllers/PaymentRecoveryController.cs)؛ Callback فقط محرک Verify است |
| Domain Evidence | Order/PaymentAttempt/Payment/PaymentAttemptEvent؛ Paid و Fulfillment state جدا؛ مبلغ/Plan/Period snapshot می‌شود؛ callback client یا redirect سند پرداخت نیست |
| Persistence Evidence | `commerce.Orders/PaymentAttempts/Payments/PaymentAttemptEvents` و OutboxMessages؛ claim کوتاه برای network و transaction نهایی اتمیک؛ unique settlement/اثر تجاری از تکرار جلوگیری می‌کند |
| Tests | [ZibalPaymentEndpointsTests](../../tests/MakanApp.IntegrationTests/ZibalPaymentEndpointsTests.cs)، [Zibal Acceptance](../../tests/MakanApp.IntegrationTests/ZibalPaymentEndpointsTests.Acceptance.cs)، [ZibalPaymentMigrationAcceptanceTests](../../tests/MakanApp.IntegrationTests/ZibalPaymentMigrationAcceptanceTests.cs) و Unit adapter/options؛117 case Zibal در baseline MKN موجود بود و در این ممیزی از TRX تازه سنجیده می‌شود |
| Migration | `20261110081503_AddZibalPaymentIntegration` و Order/Payment foundation موجود؛ جدید0 |
| Runtime Dependency | تنظیم معتبر [ZibalOptions](../../MakanApp.Infrastructure/Commerce/ZibalOptions.cs)، Provider و Callback قابل‌دسترسی؛ نبود آن CFG-01 است؛ آزمون محلی provider کنترل‌شده دارد |
| Product Decision | Zibal داخل محدوده؛ CafeBazaar/Myket/iOS برای این Release Deferred؛ Task لغوشدهٔ BE18 Store به معنی پیاده‌سازی Store نیست |
| Current Release Scope | IN SCOPE Zibal؛ `BE18_STORE_BILLING=DEFERRED_CURRENT_RELEASE`؛ هیچ Store receipt/intention/mapping تازه ساخته نشده |
| Final Status | `PASS_CURRENT_RELEASE` برای implementation/test Zibal؛ `BLOCKED_CONFIGURATION` برای فعال‌سازی مقصد فعلی |
| Remaining Action | CFG-01 و پذیرش Callback واقعی؛ `ZIBAL=PASS` در این گزارش، نتیجهٔ کد/تست است، نه تراکنش بانکی Production |

## BE19 — AI Gateway / Knowledge

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Actor شخصی/نقش سازمانی، Assistant Context، Payer مستقل، FreeTrial/Quota/Reservation، Source ACL، RAG، Retry و Unknown |
| Current Implementation | [AiChatService](../../MakanApp.Application/Ai/AiChatService.cs)، AiRoleContextService، [AiInvocationExecutor](../../MakanApp.Application/Ai/AiInvocationExecutor.cs)، AiSourceSelectionService و [KnowledgeAuthorizationService](../../MakanApp.Application/Knowledge/KnowledgeAuthorizationService.cs)؛ Context/Source پیش و پس از تماس کنترل می‌شوند |
| API Evidence | `POST /api/v1/ai/chat`، `/sources/preflight` و مسیرهای نقش؛ `GET/PUT /api/v1/ai/assistant-context`؛ `POST /api/v1/ai/knowledge/retrieve` و `/rag/answer`؛ عملیات ingest/status/delete نسخهٔ منبع موجود |
| Domain Evidence | AssistantContextSelection، AiSourceSelectionPin/Operation، UsageReservation؛ Actor Personal بدون Org scope؛ مالک مالی Individual/Organization با داشتن Role یکسان فرض نمی‌شود |
| Persistence Evidence | `intelligence.AssistantContextSelections`، `knowledge.AiSourceSelectionPins/Operations`، EducationalSources و `commerce.UsageReservations/UsageLedger`؛ همان Operation پس از Consumed برابر409، بدون ذخیرهٔ پاسخ خصوصی/تماس و مصرف دوباره؛ Unknown به Retry کور تبدیل نمی‌شود |
| Tests | [PersonalAiAcceptanceTests](../../tests/MakanApp.IntegrationTests/PersonalAiAcceptanceTests.cs)، [PersonalKnowledgeAcceptanceTests](../../tests/MakanApp.IntegrationTests/PersonalKnowledgeAcceptanceTests.cs)، [AiV1CommerceEndpointsTests](../../tests/MakanApp.IntegrationTests/AiV1CommerceEndpointsTests.cs)، [AiV1ControlledTransportTests](../../tests/MakanApp.IntegrationTests/AiV1ControlledTransportTests.cs) و Unit invocation/contract/error mapping |
| Migration | `20261030152943_AddAiSourceSelectionPins`، `20261112151714_AddSupportPrivacyAndAssistantContexts` و `20261112152027_EnforceAssistantAndDeletionSessionOwnership`؛ جدید0 |
| Runtime Dependency | SQL، upstream AI/Knowledge و configuration؛ Runtime محلی پیش‌فرض خاموش، CFG-02؛ هیچ Probe زنده، Full12 یا تماس Production در این ممیزی اجرا نشد |
| Product Decision | D11 بسته طبق قرارداد جاری Personal؛ `AiService:UseServiceAuthentication=false` و `AiKnowledge:UseServiceAuthentication=false` حفظ؛ Library AI همچنان policy-disabled؛ timeout35s دست‌نخورده |
| Current Release Scope | IN SCOPE Gateway/Knowledge با قرارداد فعلی؛ شواهد live مورخ2026-11-14 در [Handoff](PERSONAL_AI_FRONTEND_HANDOFF_FA.md) تاریخی‌اند و retest تازه معرفی نمی‌شوند |
| Final Status | `PASS` برای Gateway/آزمون محلی؛ `BLOCKED_CONFIGURATION` برای فعال‌سازی مقصد فعلی |
| Remaining Action | CFG-02 و آزمون از Backend مقصد؛ هیچ Service JWT/SigningKey تازه درخواست، چاپ، ذخیره یا لازم نمی‌شود |

## BE20 — Platform Admin

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Platform roles/permissions، Users، School/Commerce/Finance/Support/Moderation/Official Spaces/Audit |
| Current Implementation | [PlatformIamService](../../MakanApp.Application/Platform/PlatformIamService.cs)، PlatformOperations/Commerce/EducationSafety services و [EfPlatformIamStore](../../MakanApp.Infrastructure/Platform/EfPlatformIamStore.cs)؛ permission از snapshot جاری resolve می‌شود |
| API Evidence | `GET /api/admin/v1/me`؛ `/role-assignments`، `/users`، `/school-applications`، `/finance/orders`، `/moderation/cases` و `/operations/health` با Policyهای صریح |
| Domain Evidence | PlatformRole/Permission/RoleAssignment/RolePermission و AdminAuditLog؛ نقش سازمانی Manager به‌خودی‌خود permission پلتفرم ندارد |
| Persistence Evidence | جدول‌های `platform.PlatformRoles/PlatformPermissions/PlatformRoleAssignments/PlatformRolePermissions/AdminAuditLogs/ProvisioningRequests`؛ rowversion، reason/correlation و محافظ‌های مالک/عضویت موجود |
| Tests | [PlatformIamEndpointsTests](../../tests/MakanApp.IntegrationTests/PlatformIamEndpointsTests.cs)، [PlatformBackofficeFinalAcceptanceTests](../../tests/MakanApp.IntegrationTests/PlatformBackofficeFinalAcceptanceTests.cs)، [PlatformAuthorizationRegressionTests](../../tests/MakanApp.IntegrationTests/PlatformAuthorizationRegressionTests.cs) و آزمون‌های مجوز شخصی/آموزشی/مدیریتی |
| Migration | `AddPlatformIamAndAdminAudit` و migrations Platform Operations/Finance/Education موجود؛ `RemovePlatformMfa` تصمیم تاریخی Source است و در این Audit برنمی‌گردد؛ جدید0 |
| Runtime Dependency | SQL و نشست/permission جاری؛ endpoint مدیریت، Browse خصوصی Direct عمومی ایجاد نمی‌کند |
| Product Decision | Platform به Manager آموزشی grant می‌کند؛ Teacher از Manager مجاز تفویض می‌گیرد؛ Host/مالی/Manager به‌تنهایی مجوز Academic نیست |
| Current Release Scope | IN SCOPE مدیریت موجود؛ MFA یا نظام Role موازی اضافه نمی‌شود |
| Final Status | `PASS_CURRENT_POLICY` |
| Remaining Action | بررسی حساب/مجوز اپراتور مقصد؛ هیچ نقش یا User واقعی Seed نشد |

## BE21 — Support / Moderation

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Create/Read/Reply/Assign/Resolve تیکت، Attachment، Abuse/Moderation/Block/Safety |
| Current Implementation | [SupportTicketService](../../MakanApp.Application/Support/SupportTicketService.cs)، [EfSupportTicketStore](../../MakanApp.Infrastructure/Support/EfSupportTicketStore.cs)، ModerationService و PlatformEducationSafetyService؛ دسترسی منبع/Case جاری و audit |
| API Evidence | `GET/POST /api/v1/support/tickets`، Read/Reply مالک؛ مسیر مدیریت موجود با Support permission؛ `POST /api/v1/conversations/{conversationId}/messages/{messageId}/reports` و `/api/admin/v1/moderation/cases` |
| Domain Evidence | SupportTicket/Message/Operation/Attachment، AbuseReport/ModerationCase/Action/SafetyIncident/UserBlock؛ Reply پس از Resolve مجاز نیست و Reopen ضمنی ایجاد نمی‌کند |
| Persistence Evidence | جدول‌های `support` و Moderation/Abuse/Safety در `messaging`؛ ClientOperationId/Version/rowversion و Binding ACL برای فایل پشتیبانی؛ مدرک private فقط در مسیر مجاز Case |
| Tests | [ProductV6SupportPrivacyTests](../../tests/MakanApp.IntegrationTests/ProductV6SupportPrivacyTests.cs)، [PlatformEducationSafetyEndpointsTests](../../tests/MakanApp.IntegrationTests/PlatformEducationSafetyEndpointsTests.cs)، MessagingSafety/Moderation/Block regression |
| Migration | `20261112151714_AddSupportPrivacyAndAssistantContexts` و Moderation migrations موجود؛ جدید0 |
| Runtime Dependency | SQL، Storage و provider ایمنی داخلی؛ Safety unavailable در مسیر لازم fail-closed می‌ماند |
| Product Decision | D10 Reopen تیکت `PRODUCT_DECISION_REQUIRED`؛ Media Review Approval نیز تصمیم باز مستقل است؛ بستن Case تأیید ارسال فایل نیست |
| Current Release Scope | IN SCOPE workflow فعلی؛ Ticket Reopen/Human Review یا bypass تازه ساخته نمی‌شود |
| Final Status | `PASS_CURRENT_POLICY` |
| Remaining Action | تصمیم‌های آینده در Register/Scope؛ هیچ پرونده/گفتگوی واقعی در Audit بازبینی نشد |

## BE22 — Security / Account Deletion

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | درخواست/پیگیری حذف، Privacy retention، قطع دسترسی، حذف خصوصی و AI/Knowledge purge قابل پیگیری و بازیابی |
| Current Implementation | [AccountDeletionService](../../MakanApp.Application/Identity/AccountDeletionService.cs)، AccountDeletionWorkflow و [AccountDeletionProcessor](../../MakanApp.Infrastructure/Identity/AccountDeletionProcessor.cs)؛ claim/checkpoint/NeedsAttention، Fail/Retry با بودجه و audit امن |
| API Evidence | `POST/GET /api/v1/me/deletion-request`؛ Queue/Detail/Retry Platform Privacy؛ درخواستِ صرف، حذف اجراشده اعلام نمی‌شود |
| Domain Evidence | [AccountDeletionRequest](../../MakanApp.Domain/Identity/AccountDeletionRequest.cs) و AccountDeletionStep، PolicyVersion و ClaimToken/lease؛ تاریخچهٔ رسمی با دادهٔ خصوصی یکسان purge نمی‌شود |
| Persistence Evidence | `identity.AccountDeletionRequests/Steps` و `knowledge.AccountDeletionKnowledgePurges`؛ unique operation/current request، rowversion و module lifecycleها؛ دادهٔ آموزشی/پرداخت/Audit طبق سیاست فعلی نگه داشته می‌شود |
| Tests | [AccountDeletionAcceptanceTests](../../tests/MakanApp.IntegrationTests/AccountDeletionAcceptanceTests.cs) و partialهای Data/Commerce/Recovery؛ [AccountDeletionMigrationAcceptanceTests](../../tests/MakanApp.IntegrationTests/AccountDeletionMigrationAcceptanceTests.cs)، [AccountDeletionConfigurationTests](../../tests/MakanApp.IntegrationTests/AccountDeletionConfigurationTests.cs) |
| Migration | `20261114140323_AddAccountDeletionLifecycle` و FK/session foundation موجود؛ جدید0 |
| Runtime Dependency | SQL، Storage و Knowledge purge در اجرای مجاز؛ provider unavailable به success پنهان تبدیل نمی‌شود |
| Product Decision | D04 مدت retention/legal/backup باز؛ Production Execution خاموش می‌تواند بماند؛ درخواست و پیگیری بدون آن موجود است |
| Current Release Scope | IN SCOPE کد درخواست/workflow؛ اجرای واقعی حذف/تعیین مدت حقوقی در این ممیزی مجاز نیست |
| Final Status | `PASS` |
| Remaining Action | `PRODUCTION_ACCOUNT_DELETION_EXECUTION=BLOCKED_CONFIGURATION`؛ `RETENTION_DURATIONS=PRODUCT_LEGAL_CONFIGURATION_REQUIRED`؛ این‌ها implementation failure نیستند |

## BE23 — Operations / Release Readiness

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | Migration reproducible، Publish، Health، Worker/Recovery، drill محلی، Config parity و Artifact evidence |
| Current Implementation | [CoreReadinessHealthCheck](../../MakanApp.Api/Hosting/CoreReadinessHealthCheck.cs)، ProductionConfigurationValidator، ProductionLoggingSafety، WorkerTelemetry/CompletionBudget و [ابزار Recovery](../../tools/production/recovery/MakanRecovery.psm1) موجود |
| API Evidence | `GET /health/live` و `/health` کم‌اطلاعات؛ `GET /health/ready` شکست SQL/Storage را503 می‌کند؛ `/api/admin/v1/operations/health` با permission؛ داشتن endpoint اثبات exporter خارجی نیست |
| Domain Evidence | Claim/lease/stateهای Outbox/Export/Notification/Deletion/Scheduled، version/operation و checkpoint دامنه‌های مالک؛ event delivery exactly-once فرض نشده |
| Persistence Evidence | OutboxMessages/RealtimeOutbox/ReportExportJobs/NotificationDeliveryAttempts/AccountDeletionSteps؛ SQL lock و fencing مالک؛ Ring و فایل خارجی باید همراه SQL قابل Restore باشند |
| Tests | [RecoveryDrillTests](../../tests/MakanApp.IntegrationTests/RecoveryDrillTests.cs)، [Be23DataProtectionPersistenceTests](../../tests/MakanApp.IntegrationTests/Be23DataProtectionPersistenceTests.cs)، OperationsWorkerRecovery/NotificationWorkerRecovery/ReportExportRecovery، Health و information-leakage tests |
| Migration |75 موجود؛ Pending Model Changes=NO؛ [exporter SQL](../../tools/production/export-migration-sql.ps1) دوباره با NoBuild اجرا شد:75 Migration،1686 deferred operation، DatabaseExecuted=false و همان hash معتبر تاریخی؛ schema جدید یا اجرای Migration عادی0 |
| Runtime Dependency | .NET9/IIS، SQL، Ring/Storage/ACL و مانیتورینگ مقصد؛ TestIis/ProductionIis profile، config متقابل محیط و PDB را از publish جدا می‌کنند |
| Product Decision | وضعیت `PASS_LOCAL_READINESS` جای Production acceptance نیست؛ backup موجود به‌تنهایی Restore proof نیست |
| Current Release Scope | IN SCOPE آمادگی محلی؛ [Manifest تاریخی BE23](BE23_RELEASE_ARTIFACT_MANIFEST_FA.md) مربوط به Source=`52b596a` است، نه MKN/این HEAD؛ در این Audit ZIP/Publish/Deploy تازه ساخته نشد |
| Final Status | `PASS_LOCAL_READINESS` |
| Remaining Action | Ring/Storage/Production Restore/exporter=`OPERATIONAL_VALIDATION_PENDING`؛ دو Configuration لازم CFG-01/02 جدا؛ Source بعدی باید بستهٔ تازهٔ خود را داشته باشد |

## BE24 — دو پذیرش مستقل

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | تفکیک تکمیل کل Product آینده از آمادگی محدودهٔ قطعی Release فعلی |
| Current Implementation | قابلیت‌های موجود BE01–BE23، Gateهای BE25 و نتیجهٔ MKN-BE-001 از Source/تست/مدل ممیزی شدند؛ Deferred به failure کد تبدیل نمی‌شود |
| API Evidence | همان Snapshot494 operation؛ API فرضی آینده برای سبزکردن ماتریس اضافه نشد |
| Domain Evidence | وضعیت Scope/Policy سندی است؛ Entity یا state دامنهٔ تازه لازم ندارد |
| Persistence Evidence |151 جدول و75 Migration؛ هیچ مدل acceptance یا receipt جدید ساخته نشد |
| Tests | Full Regression و شمار suiteهای واقعی این گزارش؛ پذیرش سرور/خرید/Push/AI زنده اجرا نشده است |
| Migration | جدید0؛ Pending=NO |
| Runtime Dependency | دو Configurationِ دیده‌نشدهٔ لازم و هفت پذیرش عملیاتی جدا؛ server readiness از localhost نتیجه‌گیری نمی‌شود |
| Product Decision | Freeze صریح Store/Push/iOS/Family/PersonalClass Academic و Library policy؛12 تصمیم Register به‌طور جدا طبقه‌بندی می‌شوند |
| Current Release Scope | Full Future Product خارج از معیار جاری؛ Release جاری طبق Scope و Blockers سنجیده می‌شود |
| Final Status | `BE24_FULL_PRODUCT_ACCEPTANCE=DEFERRED_PRODUCT_SCOPE`؛ این مقدار استثنای صریح پرامپت برای BE24 است. `CURRENT_RELEASE_ACCEPTANCE=NOT_READY_FOR_TEST_DEPLOY` به‌دلیل CFG-01 و CFG-02 محلی |
| Remaining Action | تنظیم خصوصیِ قابل تأیید دو قابلیت داخل محدوده، ساخت بستهٔ جدید و پذیرش Test؛ هیچ merge/deploy در این Task |

## BE25 — PersonalClass Assignment / Exam

| مورد | شاهد / نتیجه |
|---|---|
| Requirement | حفظ engine موجود و خاموشی مصوب قابلیت آموزشی مستقل در Release جاری |
| Current Implementation | [PersonalClassAcademicFeatures](../../MakanApp.Application/Assessment/PersonalClassAcademicFeatures.cs) در resolver/API مشترک، Worker، Notification و Realtime اعمال می‌شود؛ صرف grant یا resource-id آن را دور نمی‌زند |
| API Evidence | PersonalClassAcademicsController و APIهای shared assessment موجود؛ feature-disabled باید درخواست مستقل را رد کند و مسیر سازمانی را سالم نگه دارد |
| Domain Evidence | AcademicScope/AcademicClassKind، grantهای صریح مدیر/تفویض مدرس؛ Host/Manager نقش آموزشی خودکار نمی‌گیرند |
| Persistence Evidence | `academic.PersonalClassAcademicGrants` و ContextKind/ContextId موجود در assessment؛ حفظ ردیف و تاریخچه هنگام خاموشی Gate |
| Tests | [ProductV6AcademicGateTests](../../tests/MakanApp.IntegrationTests/ProductV6AcademicGateTests.cs)، PersonalClassAcademicGrant/Assignment/Exam/Concurrency و School regression؛ fixture اختصاصی engine را می‌سنجد، نه فعال‌بودن عمومی Release |
| Migration | `20261103104624_ExtendAcademicEngineToPersonalClasses` از قبل وجود دارد؛ Gate/Audit migration تازه نمی‌خواهد |
| Runtime Dependency | در Release فعلی فعال نیست؛ اجرای engine فقط در میزبان تستِ صریح فعال‌شده |
| Product Decision | D07 برابر `DEFERRED_APPROVED`؛ AssignmentsEnabled=false و ExamsEnabled=false در تنظیم مؤثر حفظ شدند |
| Current Release Scope | DEFERRED_APPROVED؛ engine/داده حذف یا تضعیف نشد؛ هیچ فعال‌سازی آموزشی برای سبزکردن Audit |
| Final Status | `DEFERRED_APPROVED` |
| Remaining Action | Task صریح فعال‌سازی آینده، همراه مجوز/تجارت و پذیرش مستقل؛ با Organization engine مخلوط نشود |

## دوازده تصمیم باز Product/Legal

مرجع شناسه‌ها [Register موجود](BACKEND_V6_OPEN_DECISIONS_FA.md) است. شمار این جدول12 است؛
D01/D02/D12/D17 و D11 باز نیستند و D07 جداگانه Deferred مصوب است. Policyهای تکمیلی
ReviewRequired/Ready-orphan/Export TTL نیز حفظ می‌شوند؛ این ممیزی آن‌ها را بسته یا اجراشده
معرفی نمی‌کند و بدون تصمیم تازه، دامنهٔ Register دوازده‌گانه را تغییر نمی‌دهد.

| تصمیم | موضوع | Blocks Current Release? | Blocks Future Full Product? | وضعیت/دلیل |
|---|---|---|---|---|
| V6-D03 | TTL Tombstone/Sync و OS-delete دفترچه | خیر | بله، برای سیاست نهایی آن قابلیت | `PRODUCT_DECISION_REQUIRED`؛ حذف صریح و stale-device protection موجود کار می‌کند |
| V6-D04 | Retention/Legal hold/Backup/Deletion grace | خیر، با Execution خاموش | بله، برای اجرای کامل حقوقی | `PRODUCT_LEGAL_CONFIGURATION_REQUIRED`؛ Request/Privacy code موجود و سالم می‌ماند |
| V6-D05 | Family/Shared/Pooling | خیر | بله | `PRODUCT_DECISION_REQUIRED`؛ اشتراک جاری هر User مستقل است |
| V6-D06 | iOS Billing | خیر | بله | `DEFERRED_CURRENT_RELEASE`؛ تصمیم روش پرداخت آینده باز است |
| V6-D08 | گزارش آموزشی PersonalClass | خیر | بله | `PRODUCT_DECISION_REQUIRED`؛ گزارش Organization در محدوده است |
| V6-D09 | Restore/Reopen کلاس Archived | خیر | بله | `PRODUCT_DECISION_REQUIRED`؛ Archive/Recovery فعلی حفظ می‌شود |
| V6-D10 | Reopen تیکت Resolved | خیر | بله | `PRODUCT_DECISION_REQUIRED`؛ Create/Read/Reply/Assign/Resolve موجود معیار فعلی است |
| V6-D13 | target آیندهٔ Request/Stranger/Unknown/Minor | خیر | بله، برای رفتار تازه | `PRODUCT_DECISION_REQUIRED`؛ سیاست مصوب2026-11-01 با Privacy/Block/Safety حفظ است |
| V6-D14 | گسترش Parent در فضای رسمی کلاس | خیر | بله، برای گسترش پیشنهادی | `PRODUCT_DECISION_REQUIRED`؛ Guardian مجوز Direct خصوصی نمی‌شود |
| V6-D15 | مثبت‌بودن اجباری هر دو ظرفیت School | خیر | بله، برای تغییر target | `PRODUCT_DECISION_REQUIRED`؛ سیاست فعلی required/non-negative و صفر معتبر است |
| V6-D16 | جزئیات حقوقی/دسته‌بندی تازهٔ مدارک School | خیر | بله، برای گسترش target | `PRODUCT_DECISION_REQUIRED`؛ حداقل فعلی دو نوع اجرا شده؛ شمارهٔ حقوقی تازه حدس نمی‌شود |
| V6-D18 | Correction/Reopen پروندهٔ نهایی | خیر | بله | `PRODUCT_DECISION_REQUIRED`؛ Approved/Rejected/Cancelled درجا mutate یا restore نمی‌شود |

## Snapshot نهایی قرارداد و دیتابیس

| مقدار | نتیجهٔ تازه از ابزار موجود |
|---|---|
| SourceCommit | `14b0271a9d3aea840e23413dacae5300fc68ebed` |
| OpenAPI Paths |438 |
| OpenAPI Operations |494 |
| OpenAPI Schemas |783 |
| Controllers |91 |
| OpenAPI SHA-256 | `616e22cf15bc67b604ea12f3348c4612d0289ebaf413eb4d83635c65770cf4f0` |
| Tables |151 |
| Migrations |75 |
| Latest Migration | `20261114183321_ExtendProductNotificationCoverage` |
| Pending Model Changes |NO؛ فرمان واقعی EF با startup-project=Infrastructure و NoBuild |
| New Migrations / Schema Change |0 /0 |
| Snapshot DB / Workers / SMS / AI |همگی غیرفعال؛ DatabaseConnected=false و HostedWorkersEnabled=false |

SQL کامل idempotent با همان exporter موجود، دوباره زیر پوشهٔ شواهد تولید شد؛ SHA-256:
`5e0f24ae363bee047bae8d6cf1cc4473bacc8f2de5ceb0acaa9770509f445fa5`.
این با Manifest تاریخی BE23 برابر است: تغییر MKN و Audit، schema را عوض نکرده‌اند.
SQL تولیدشده روی دیتابیس عادی یا سرور اجرا نشده و baseline واقعی Migration سرور از این
مقایسه استنتاج نمی‌شود. ZIPهای Test/Production تاریخی `52b596a` نیز با SHA-256 Manifest
خود تطبیق داده شدند؛ هر دو سالم‌اند، اما Source جاری `14b0271` را ندارند.

نمونهٔ پشتوانهٔ SQL از مدل طراحی واقعی: `CK_AssignmentVersions_MaxScore` و rowversion،
`UX_PaymentAttempts_Order_ClientOperation`، `UX_Payments_Order` و
`UX_Payments_Provider_Reference`، `UX_PhoneChangeRequests_PendingUser` و
`IX_AddressBookEntries_OwnerUserId_ServerVersion`. نام‌ها از مدل استخراج شده‌اند؛ صرف وجود
Constraint به معنی اجرای آن روی Production نیست. Integration آن‌ها را روی SQL ایزوله می‌سنجد.

## Audit پیکربندی بدون افشای مقدار محرمانه

JSON واقعی هر دو محیط parse شد؛16 بخش سطح اول،27 leaf key، schema و nested structure برابرند.
پس از جایگزینی دقیق چهار مقدار مصوب، دو درخت JSON کامل برابر شدند؛ تفاوت‌ها فقط:

```text
ConnectionStrings:MakanDatabase
Otp:Enabled
Otp:AllowMockAuthentication
Storage:RootPath
```

Test، Mock OTP و Production، Real OTP دارند؛ Storage کاملاً جداست. وجود Connection String،
Identity.SecurityKey و MeliPayamakOtp.ServiceToken فقط به‌صورت boolean تأیید شد؛ مقدارشان
چاپ، بازنویسی، خالی یا staged نشد. Safe config و hashهای محافظت‌شده در شواهد خصوصی‌اند.

در تنظیم مؤثر فایل‌های محلی، Zibal unavailable/ناقص و AI/Knowledge خاموش‌اند؛ Service Auth هر
دو false است. Gateهای `Features:PersonalClass` هر دو false و AccountDeletion Execution=false،
GracePeriodHours=null باقی ماندند. هیچ override مرتبط در محیط همین فرایند دیده نشد. این
ممیزی تنظیمات واقعی IIS/SQL/محیط سرور را ندیده و ادعای پیکربندی‌نشدن آن سرور نمی‌کند.

## نتیجهٔ Release جاری و مرز موانع

کد جدیدی برای Store Billing، Push Runtime، Family، آموزش PersonalClass، Ticket Reopen،
تأیید رسانه یا Library AI نوشته نشد؛ قابلیت موجود هم حذف/ضعیف نشد. هیچ نقص تازهٔ اثبات‌شدهٔ
Source در محدودهٔ جاری پیدا نشد که به fix کد نیاز داشته باشد.

- `CURRENT_RELEASE_CODE_GAPS=0`: بر مبنای Source و دامنهٔ آزمون‌ها، نه ادعای کشف همهٔ خطاهای ممکن.
- `CURRENT_RELEASE_CONFIGURATION_BLOCKERS=2`: CFG-01 زیبال و CFG-02 فعال‌سازی AI/Knowledge؛
  محدوده و شاهد دقیق در [Blockers](CURRENT_RELEASE_BLOCKERS_FA.md) آمده است.
- `CURRENT_RELEASE_OPERATIONAL_VALIDATIONS=7`: OP-01..07 در Scope؛ چهار مورد آخر gateهای
  Production هستند و به‌تنهایی نصب Test را مسدود نمی‌کنند.
- `OPEN_PRODUCT_DECISIONS=12`: همان دوازده شناسهٔ Register؛ هیچ‌کدام با محدودهٔ Freezeشده
  فعلی، blocker تازهٔ Release نیستند.
- `BE24_FULL_PRODUCT_ACCEPTANCE=DEFERRED_PRODUCT_SCOPE`: تکمیل آینده با پذیرش این Release
  یک status مبهم مشترک ندارد.
- `CURRENT_RELEASE_ACCEPTANCE=NOT_READY_FOR_TEST_DEPLOY`: فقط به دلیل نبود تنظیمِ قابل‌تأیید
  دو قابلیت داخل محدوده در محیط محلیِ بررسی‌شده؛ این نتیجه failure کد، Store یا Push نیست.

پیش از ساخت/نصب بستهٔ پذیرش کامل Test، اپراتور باید تنظیم خصوصی معتبر همین دو مورد را
آماده کند. رمز، Merchant، token یا Service JWT در چت/Git لازم نیست. School Sales تا تنظیم
قرارداد معتبر می‌تواند بسته بماند؛ حذف واقعی نیز طبق اجازهٔ مالک می‌تواند خاموش بماند.

## گزارش آموزشی توسعه‌دهنده

### ۱. خلاصهٔ مرحله

مسئله، مخلوط‌شدن status تاریخی، هدف آینده، کد موجود و اجرای واقعی سرویس‌ها بود. قبل از این
ممیزی، ماتریس سطرهای MISSING/PARTIAL قدیمی و یک BE24 مبهم داشت. اکنون25 بخش با شاهد
API/Domain/SQL/Test/Migration، محدودهٔ Release مصوب،12 تصمیم آینده و Configuration/Operations
مستقل ثبت شده‌اند. این مرحله feature تازه نمی‌سازد؛ نبود defect اثبات‌شده، دلیل ساخت fix صوری نیست.

### ۲. معماری

هیچ‌یک از لایه‌های Domain/Application/Infrastructure/Api تغییر نکردند. Domain قواعد و state
خالص را دارد؛ Application use case و port را، Infrastructure پیاده‌سازی EF/SQL/Provider را،
Api Controller/Composition/HTTP را. Snapshot از Host واقعی metadata می‌گیرد؛ این ابزار
نمی‌تواند جای تست SQL، مجوز یا Runtime واقعی را بگیرد. .NET9، EF Core9، SQL Server، Onion،
Modular Monolith و Controller API محفوظ‌اند؛ Architecture Regression همین مرزها را می‌سنجد.

### ۳. فایل‌های مهم ایجادشده

| فایل | مسئولیت / دلیل / نکتهٔ آموزشی |
|---|---|
| `docs/product-v6/FINAL_BE01_BE25_AUDIT_FA.md` | شواهد Source و تصمیم پذیرش؛ status بدون مبنا یا تاریخ، برای Release قابل اتکا نیست |
| `docs/product-v6/CURRENT_RELEASE_SCOPE_FA.md` | تعریف IN SCOPE/DEFERRED/CONFIGURATION/OPERATIONS؛ خاموشی مصوب قابلیت آینده failure نیست |
| `docs/product-v6/CURRENT_RELEASE_BLOCKERS_FA.md` | فقط مانع واقعی و اقدام لازم؛ readiness فعال‌سازی با build سبز اثبات نمی‌شود |

شواهد اجرایی/Backup/هش‌ها زیر `.artifacts/final-be01-be25-audit-20261117-14b0271` خصوصی‌اند؛
فایل پروژه یا کد Runtime تازه محسوب نمی‌شوند و وارد Commit نیستند.

### ۴. فایل‌های مهم تغییرکرده

در `BACKEND_V6_GAP_MATRIX_FA.md` فقط بخش آغازین `FINAL CURRENT STATUS` افزوده می‌شود؛ تمام
تاریخچهٔ قبلی محفوظ است. README به سه مرجع تازه لینک می‌دهد. اثر این تغییرات، تعیین مرجع
خواندن و رفع ابهام Release است؛ Authentication، Domain، schema یا رفتار API تغییر نمی‌کند.

### ۵. جریان اجرای قابلیتِ موجودِ ممیزی‌شده

نمونهٔ واقعی History در Source فعلی:

```text
GET /api/v1/conversations/{conversationId}/messages
-> ConversationsController
-> MessagingService.GetConversationMessagesAsync
-> AccountInboxService.ResolveReadContextAsync
-> IAccountInboxStore.ResolveReadContextAsync
-> EfMessagingStore / AccountInboxAccessQuery
-> current User/Session/Participant/Official Source rules
-> SQL Server Messages + visibility/history boundary
-> ConversationMessagePageResult
-> HTTP 200 / ProblemDetails
```

Dependency به داخل Onion است؛ Application نوع EF یا SQL را نمی‌شناسد. انتخاب transient
ReadContext از منبع واقعی، Session Workspace را mutate نمی‌کند. Domain state و query مجوز
جاری با هم عمل می‌کنند؛ صرف داشتن ConversationId هیچ مجوزی ایجاد نمی‌کند.

### ۶. دیتابیس

جدول/ستون/PK/FK/Index/Constraint یا rowversion تازه‌ای ایجاد نشد.151 جدول و75 Migration
در مدل طراحی وجود دارد. نمونه‌های constraint و قفل‌های حساس در ردیف‌های BE و Snapshot ثبت‌اند.
Network خارجی در transaction تازه اجرا نشده، چون هیچ use case جدیدی نوشته نشده است.
فرمان EF، نبود Pending Model Changes را تأیید کرد؛ SQL idempotent تولید شد و اجرا روی DB عادی0
بود. Migration و Restore داخل Regression فقط روی دیتابیس‌های واضحاً ایزولهٔ تست انجام می‌شوند.

### ۷. Endpointها

هیچ endpoint یا DTO تازه/ویرایش‌شده نداریم. نمونهٔ History بالا ورودی route و query موجود
`beforeSequence/limit` و پاسخ صفحهٔ پیام دارد؛ نشست معتبر و دسترسی جاری حساب لازم است.
401 برای عدم احراز،400 برای selector/cursor نامعتبر و404 برای منبع غیرقابل‌دسترسی از empty200
جداست. Endpointهای مهم هر BE در جدول همان بخش‌اند؛ این فهرست جای schema کامل Snapshot نیست.

### ۸. مفاهیم واقعاً استفاده‌شده

Source-first audit، استخراج metadata واقعی از Host/EF، SHA-256 برای ردیابی مبنا و حفظ فایل،
تفکیک policy/implementation/configuration/operations، و Regression روی SQL واقعی استفاده شدند.
Hash ثابت شاهد عدم تغییر bytes است؛ به‌تنهایی صحت کسب‌وکار یا پذیرش Production را ثابت نمی‌کند.
هیچ CQRS/MediatR/Repository عمومی، abstraction جدید یا upgrade وابستگی ایجاد نشد.

### ۹. تصمیم‌های طراحی

به‌جای overwrite تاریخچه و یکی‌کردن همهٔ Blockedها، مرجع جدید در آغاز ماتریس قرار گرفت؛
مزیت آن حفظ chronology و روشن‌بودن Source هر شاهد است. هزینه، باقی‌ماندن اعداد تاریخی در
اسناد قبلی است که باید با تاریخ/Commit خود خوانده شوند. Build/Test پاک، جای تنظیم Zibal یا
فعال‌سازی AI را نمی‌گیرد. از طرف دیگر Push/Store/Families Deferred، code failure نسخه نیستند.
هیچ قیمت، Retention، URL یا Secret برای تغییر این نتیجه حدس زده نشد.

### ۱۰. تست‌ها

در این Audit تست تازه اضافه/تغییر/حذف/skip نشد؛ Source تغییر نکرده و تست مصنوعیِ آینهٔ سند
لازم نبود. Unit قواعد خالص، Integration قرارداد HTTP/SQL/Constraint/ACL/Concurrency/Recovery
و Architecture جهت dependency را می‌سنجد. تست‌های MKN شامل22 case Integration جدید و4 Unit
بودند و در HEAD پایه موجودند؛ این Audit آن‌ها را «تست جدید همین Task» گزارش نمی‌کند.
شمارش scopeهای متمرکز از caseهای واقعاً اجراشده استخراج می‌شود و به‌علت اشتراک سناریوها
قابل جمع با یکدیگر نیست؛ فقط جمع سه پروژه Total معتبر است.

### ۱۱. Validation

Clean و Restore واقعی موفق‌اند. Build اول و Final Build هر دو موفق، صفر Warning/Error.
دور کامل مستقل `full-r2` با exit0 پایان یافت:1369 Unit،2068 Integration و14 Architecture،
جمع3451 Passed، Failed=0 و Skipped=0؛117 تست Zibal نیز موفق‌اند. Integration این دور29m04s
زمان اجرا داشت. Final Test نیز با exit0، دقیقاً همین1369/2068/14، جمع3451، صفر Failed/Skipped
و117 Zibal موفق پایان یافت. هیچ case از دور اول تا نهایی کم/skip نشد؛ پوشش هر23 حوزه یکسان است.

| مرحله | نتیجه |
|---|---|
| Clean / Restore | `PASS` / `PASS` |
| Release Build | `PASS`؛ Warning=0، Error=0 |
| Full Regression اول |1369 Unit +2068 Integration +14 Architecture =3451 PASS |
| Final Release Build | `PASS`؛ Warning=0، Error=0 |
| Final Full Regression |1369 Unit +2068 Integration +14 Architecture =3451 PASS |
| Failed / Skipped هر دور |0 /0 |
| Zibal هر دور |117 PASS؛68 Unit و49 Integration |
| Realtime هر دور |82 case مرتبط موفق؛ timeout/failure مشاهده‌شده0 |
| EF Pending Model Changes |NO |
| Source / Test / Config تغییرکرده |0 /0 /0 |

سه TRX کامل در هر دور، counters/executed و SHA-256 آن‌ها، receipt خروج هر فرمان و تطبیق
1820 فایل tracked بررسی شدند. فقط README/Gap Matrix از فایل‌های قبلی تغییر کرده‌اند؛ سه
سند تازه، همین تحویل‌اند.12 فایل محافظت‌شده و نسخه‌های Backup آن‌ها همچنان hash اولیه را دارند.

فرمان‌ها از ریشهٔ مخزن اجرا شدند؛ NoBuild در Test از همان خروجی Build موفق استفاده می‌کند:

```powershell
dotnet clean MakanApp.sln -c Release --nologo
dotnet restore MakanApp.sln --nologo
dotnet build MakanApp.sln -c Release --no-restore --nologo
dotnet test MakanApp.sln -c Release --no-build --no-restore --nologo --logger trx --results-directory .artifacts/final-be01-be25-audit-20261117-14b0271/full-r2
# سپس Build و Test کامل نهایی، با مسیر شواهد جدا
dotnet ef migrations has-pending-model-changes --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --configuration Release --no-build --no-color
```

گزارش متمرکز زیر از caseهای اجراشدهٔ **هر دو Full Regression** و نگاشت نام متد/فایل Source
به حوزه استخراج شده است؛23 فرمان فیلترشدهٔ مستقل اجرا نشده و چنین ادعایی نمی‌شود. یک case
می‌تواند چند حوزه را پوشش دهد؛ جمع سطرها Total نیست. فایل‌های Summary/Cases و SHA-256 هر
TRX کنار شواهد نگه‌داری می‌شوند. شمارش جدول برای هر دور یکسان است؛ تمام سطرها Failed/Skipped صفر دارند.

| حوزه | Unit | Integration | Architecture | مجموع case مرتبط |
|---|---:|---:|---:|---:|
| Identity |194 |118 |0 |312 |
| Organization |39 |76 |0 |115 |
| Guardian |7 |28 |0 |35 |
| Academic |53 |63 |0 |116 |
| Assignments |37 |110 |0 |147 |
| Exams |69 |141 |0 |210 |
| Reports |34 |54 |0 |88 |
| PersonalClass |29 |50 |0 |79 |
| Messaging |223 |569 |0 |792 |
| Messenger History |8 |22 |0 |30 |
| Realtime |6 |76 |0 |82 |
| AddressBook |12 |4 |0 |16 |
| Files |96 |149 |0 |245 |
| Notifications |105 |125 |0 |230 |
| Commerce |128 |79 |0 |207 |
| Zibal |68 |49 |0 |117 |
| AI |294 |110 |0 |404 |
| Knowledge |107 |28 |0 |135 |
| Support |2 |2 |0 |4 |
| Moderation |42 |119 |0 |161 |
| AccountDeletion |19 |35 |0 |54 |
| Operations |27 |186 |0 |213 |
| Architecture |0 |0 |14 |14 |

دو شروع اولیه، گزارش کامل Integration و receipt خروج نداشتند و Unit تکراری در یک مسیر
شواهد باقی گذاشتند؛ هیچ‌کدام Full PASS شمرده نمی‌شوند. خروجی‌های ناقص حذف نشدند و در
`interrupted-validation-review.json` ثبت‌اند. دور مستقل `full-r2` با مسیر جدا و بررسی نبود
testhost موازی شروع شد؛ تنها گزارش کامل/exit معتبر شاهد پذیرش است.

شش Timeout تاریخی Realtime و شکست Lookup در دورهای BE15 در گزارش همان مرحله باقی‌اند.
در دور کامل تازه، Realtime failure/timeout و Lookup failure صفر مشاهده شد؛ علت تاریخی هنوز
اثبات نشده و ادعای fix آن مطرح نمی‌شود. هیچ timeout/retry/test یا Assertion برای
سبزشدن تغییر/حذف نشد. `REALTIME_RELEASE_BLOCKER=NO` فقط بر شاهد اجرا و scope جاری متکی است.

### ۱۲. Git

مبنا=`14b0271a9d3aea840e23413dacae5300fc68ebed`؛ شاخه و upstream=
`feature/product-v6-backend-alignment` / `origin/feature/product-v6-backend-alignment`؛
remote=`origin` در `https://github.com/RastinaxAI/MakanApp.git`.
فقط پنج فایل سندیِ همین Audit باید stage شوند؛ سه فایل untracked قبلی AI و تمام configهای
خصوصی خارج از Commit هستند. پیام مقرر:
`docs(product-v6): finalize current release acceptance audit`.
SHA خود این Commit، نتیجهٔ Push/Fetch و مقایسهٔ direct remote در گزارش نهایی و
`final-git-verification.json` ثبت می‌شود؛ سند داخل Commit نمی‌تواند SHA خودش را در خود داشته باشد.
main/Deploy اجرا نمی‌شوند و هیچ force/rewrite/stash/clean/reset مجاز یا لازم نیست.

### ۱۳. محدودیت‌های فعلی

این ممیزی سرور، Frontend، حساب واقعی، خرید بانکی، Push واقعی یا Live AI را اجرا/بازبینی نکرد.
هیچ Secret از User Secrets یا تنظیمات سرور برای پرکردن config حدس زده نشد. دو Configuration
داخل محدوده، فروش School، اجرای حذف/Retention و gateهای عملیاتی مستقل‌اند. علت ناپایداری
تاریخی Realtime با صرف اجرای سبز بسته اعلام نمی‌شود. جزئیات محدودیت هر BE در جدولش آمده است.

### ۱۴. نکات آموزشی

1. Feature موجود، تنظیم‌شده، فعال و پذیرفته‌شده در سرور، چهار ادعای متفاوت‌اند.
2. برای شمارش API، Host/ApiExplorer از grep تعداد Controller دقیق‌تر است؛ کلاس Controller
   هم‌فایل با کلاس دیگر نیز باید شمرده شود.
3. EF model snapshot و Constraint شاهد طراحی‌اند؛ اثبات SQL به Integration واقعی نیاز دارد.
4. Cursor و current ACL باید هم‌زمان بررسی شوند؛ پایداری پیام، مجوز دائمی تاریخچه نیست.
5. Partial/Interrupted test run نتیجهٔ پذیرش نیست؛ TRX کامل و exit باید با تعداد case تطبیق کنند.
6. Private config و unfinished work با hash محافظت می‌شوند؛ وجود dirty file دلیل stage کردن آن نیست.
7. Deferred و policy-disabled را از blocker جاری جدا کنید؛ در عین حال تنظیم واقعیِ قابلیت داخل
   محدوده را به‌خاطر سبزکردن ماتریس پنهان نکنید.

### ۱۵. مسیر پیشنهادی مطالعه

ابتدا Scope و Blockers، سپس جدول BE موردنظر و فایل‌های لینک‌شدهٔ آن را بخوانید. برای History:
ConversationsController → MessagingService → AccountInboxService/port → AccountInboxAccessQuery
→ EfMessagingStore.Reads → Message/Conversation/Participant → EF configurations موجود →
MessengerHistoryAcceptance/Visibility/PersonalClass tests. برای درگاه:
ZibalCallbackController → OrderPaymentService.Recovery → PaymentAttempt/Order →
ZibalPaymentGateway/EfOrderPaymentStore → Migration زیبال → Zibal acceptance tests.

### ۱۶. مرحلهٔ بعد

تنها اقدام بعدی لازم برای readiness کامل، فراهم‌کردن و validation امنِ Configuration دو مورد
CFG-01/02 و سپس Task صریح ساخت بستهٔ Source نهایی/نصب Test است. پس از آن OP-01..03 از همان
Backend مقصد اجرا شوند. main، Production Deploy، Store billing، Push Runtime، BE25 و تصمیم‌های
باز حقوقی/خانوادگی نباید به‌عنوان ادامهٔ ضمنی این Audit اجرا شوند.
