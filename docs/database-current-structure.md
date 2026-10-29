# ساختار فعلی دیتابیس MakanApp

مدل طراحی EF Core از working tree با مبنای Git `main@701cba057b358d9cf6d15d681389d0f8d22ac6d6`؛ بازبینی `2026-10-29`. این سند metadata سورس است، نه گزارش اتصال یا تأیید schema یک سرور.

**107 جدول، 1059 ستون، 243 FK، 302 index مستقل از key و 280 CHECK؛ 52 Migration.**

## مفاهیم و مرزها

مدل جاری شامل چهار migration پس از Checklist است: RemovePlatformMfa دو جدول MFA و ستون‌های AssuranceLevel/StepUpAtUtc نشست را حذف می‌کند؛ AddSelfDeclaredProfileDateOfBirth ستون date nullable به Persons، AddUserProfilePhoto مرجع nullable و unique filtered به Users، و CompleteSchoolManagement جدول‌های ClassLessons/ClassLessonAttachments و توسعه Invitation والد را می‌افزاید. آخرین migration 20261028202239_CompleteSchoolManagement است. اجرا روی دیتابیس عادی/سرور مرحلهٔ مستقل است؛ Down اطلاعات حذف‌شدهٔ MFA را بازنمی‌گرداند.

cleanup terminal فقط bytes مجاز بدون reference را با opt-in حذف می‌کند؛ مرجع فعال ProfilePhoto و Lesson attachment هم محافظت می‌شوند. metadata SQL پاک نمی‌شود و Ready-orphan reaper وجود ندارد. [سیاست retention](operations/RETENTION_AND_CLEANUP_POLICY.md).

- SQL Server پایگاه اصلی و MakanDbContext مرجع مدل است؛ schema مرز نام‌گذاری ماژول است، نه مجوز کاربر.
- PK همهٔ جدول‌ها الزاماً Id نیست؛ کلید واقعی هر جدول پایین آمده است. Alternate Key با index یکتا فرق دارد.
- rowversion توکن هم‌زمانی SQL است، نه تاریخ؛ nullable، PK/FK، unique/filtered index و CHECK از خود مدل طراحی خوانده شده‌اند.
- FK مرکب علاوه بر وجود ردیف، سازگاری scope را کنترل می‌کند؛ مجوز actor همچنان در Application لازم است.
- ویژگی IsConcurrencyToken نشان‌دهندهٔ کنترل optimistic concurrency است. تغییرناپذیری نسخه منتشرشده و سقف نمره قواعد Domain/Application هم دارند.
- این ابزار فقط Model/GetMigrations را می‌خواند؛ EnsureCreated، Migrate، اتصال SQL، خواندن داده یا secret ندارد.

[راهنمای معماری و جریان داده](backend-api-guide.md)، [SQL Server Override](project-context/01_SQLSERVER_ARCHITECTURE_OVERRIDE.md)، [Migration تحویلی](production/DEPLOYMENT_IIS.md).

## نقشه schemaها

| Schema | جدول |
|---|---|
| academic | 11 |
| assessment | 18 |
| commerce | 10 |
| guardian | 1 |
| identity | 7 |
| messaging | 42 |
| notification | 4 |
| organization | 5 |
| platform | 7 |
| reports | 1 |
| storage | 1 |

## Migrationهای نسخه‌بندی‌شده

| ترتیب | Migration | سورس |
|---|---|---|
| 1 | 20260929075711_InitialIdentity | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20260929075711_InitialIdentity.cs) |
| 2 | 20260929091455_AddOrganizationMemberships | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20260929091455_AddOrganizationMemberships.cs) |
| 3 | 20260929125215_AddOrganizationPersons | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20260929125215_AddOrganizationPersons.cs) |
| 4 | 20260929131234_AddGuardianRelations | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20260929131234_AddGuardianRelations.cs) |
| 5 | 20260930072644_AddAcademicFoundation | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20260930072644_AddAcademicFoundation.cs) |
| 6 | 20260930123228_AddSessionsAndAttendance | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20260930123228_AddSessionsAndAttendance.cs) |
| 7 | 20261004062022_AddAssignmentFoundation | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261004062022_AddAssignmentFoundation.cs) |
| 8 | 20261004070347_AddFileStorageFoundation | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261004070347_AddFileStorageFoundation.cs) |
| 9 | 20261004082259_AddAssignmentSubmissions | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261004082259_AddAssignmentSubmissions.cs) |
| 10 | 20261004170944_AddAssignmentEvaluations | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261004170944_AddAssignmentEvaluations.cs) |
| 11 | 20261004175932_AddDirectMessagingFoundation | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261004175932_AddDirectMessagingFoundation.cs) |
| 12 | 20261005160127_AddGroupAndChannelMessaging | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261005160127_AddGroupAndChannelMessaging.cs) |
| 13 | 20261006135440_AddAdvancedMessagingFeatures | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261006135440_AddAdvancedMessagingFeatures.cs) |
| 14 | 20261006170623_AddMessagingRealtimeAndSync | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261006170623_AddMessagingRealtimeAndSync.cs) |
| 15 | 20261006200138_AddMessagingSafetyAndSearch | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261006200138_AddMessagingSafetyAndSearch.cs) |
| 16 | 20261006202655_AddExamAuthoringFoundation | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261006202655_AddExamAuthoringFoundation.cs) |
| 17 | 20261007055602_AddExamAttempts | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261007055602_AddExamAttempts.cs) |
| 18 | 20261007063258_AddExamAnswerRevisions | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261007063258_AddExamAnswerRevisions.cs) |
| 19 | 20261007071007_AddExamFinalization | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261007071007_AddExamFinalization.cs) |
| 20 | 20261007075650_AddExamGradingAndRelease | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261007075650_AddExamGradingAndRelease.cs) |
| 21 | 20261012142715_AddAsynchronousReportExports | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261012142715_AddAsynchronousReportExports.cs) |
| 22 | 20261012154542_AddNotificationFoundation | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261012154542_AddNotificationFoundation.cs) |
| 23 | 20261012163226_AddNotificationPreferencesAndDelivery | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261012163226_AddNotificationPreferencesAndDelivery.cs) |
| 24 | 20261012171406_AddCommerceFoundation | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261012171406_AddCommerceFoundation.cs) |
| 25 | 20261012175353_AddOrderAndPaymentFoundation | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261012175353_AddOrderAndPaymentFoundation.cs) |
| 26 | 20261012203821_AddMessengerContactsPrivacyDiscovery | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261012203821_AddMessengerContactsPrivacyDiscovery.cs) |
| 27 | 20261012212650_HardenMessengerConversationBehavior | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261012212650_HardenMessengerConversationBehavior.cs) |
| 28 | 20261012222340_AddMessagingModerationAndChildSafety | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261012222340_AddMessagingModerationAndChildSafety.cs) |
| 29 | 20261013042337_CompleteMessengerV2 | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261013042337_CompleteMessengerV2.cs) |
| 30 | 20261013053224_AddPlatformIamAndAdminAudit | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261013053224_AddPlatformIamAndAdminAudit.cs) |
| 31 | 20261013055902_AddPlatformOrganizationUserOperations | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261013055902_AddPlatformOrganizationUserOperations.cs) |
| 32 | 20261013062956_AddPlatformEducationSafetyOperations | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261013062956_AddPlatformEducationSafetyOperations.cs) |
| 33 | 20261013064642_AddPlatformCommerceFinanceOperations | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261013064642_AddPlatformCommerceFinanceOperations.cs) |
| 34 | 20261013084443_AddRefreshTokenRotationAndSessionAssurance | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261013084443_AddRefreshTokenRotationAndSessionAssurance.cs) |
| 35 | 20261014214734_AddOperationalPersonGuardianOnboarding | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261014214734_AddOperationalPersonGuardianOnboarding.cs) |
| 36 | 20261015073418_AddPlatformTotpMfa | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261015073418_AddPlatformTotpMfa.cs) |
| 37 | 20261015091336_EnforceApprovedMessengerChildSafety | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261015091336_EnforceApprovedMessengerChildSafety.cs) |
| 38 | 20261016050425_AddPersonalCommunicationAgeVerification | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261016050425_AddPersonalCommunicationAgeVerification.cs) |
| 39 | 20261016072600_AddDirectPeerReadReceipts | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261016072600_AddDirectPeerReadReceipts.cs) |
| 40 | 20261016074318_HardenNotificationDeliveryRecovery | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261016074318_HardenNotificationDeliveryRecovery.cs) |
| 41 | 20261016081315_ExtendPushDeviceSubscriptions | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261016081315_ExtendPushDeviceSubscriptions.cs) |
| 42 | 20261016163433_AddMessagingPresencePrivacy | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261016163433_AddMessagingPresencePrivacy.cs) |
| 43 | 20261016170915_AddMessagingPersonalOrganization | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261016170915_AddMessagingPersonalOrganization.cs) |
| 44 | 20261016174429_AddConversationInvitesAndRestrictions | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261016174429_AddConversationInvitesAndRestrictions.cs) |
| 45 | 20261016183900_AddConversationTopics | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261016183900_AddConversationTopics.cs) |
| 46 | 20261016190927_AddScheduledAndSilentMessages | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261016190927_AddScheduledAndSilentMessages.cs) |
| 47 | 20261016211112_AddConversationPollsAndQuiz | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261016211112_AddConversationPollsAndQuiz.cs) |
| 48 | 20261017033804_AddConversationChecklists | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261017033804_AddConversationChecklists.cs) |
| 49 | 20261024184613_RemovePlatformMfa | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261024184613_RemovePlatformMfa.cs) |
| 50 | 20261028102653_AddSelfDeclaredProfileDateOfBirth | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261028102653_AddSelfDeclaredProfileDateOfBirth.cs) |
| 51 | 20261028164838_AddUserProfilePhoto | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261028164838_AddUserProfilePhoto.cs) |
| 52 | 20261028202239_CompleteSchoolManagement | [کد](../MakanApp.Infrastructure/Persistence/Migrations/20261028202239_CompleteSchoolManagement.cs) |

آخرین شناسه باید در history محیط هدف جدا بررسی شود؛ برنامه migration را خودکار اجرا نمی‌کند. فایل SQL اصلاح‌شدهٔ idempotent فقط پس از Backup و بازبینی اپراتور اجرا می‌شود.

## فرهنگ کامل جدول‌ها

### academic.AcademicPeriods

Entity: `MakanApp.Domain.Academic.AcademicPeriod`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EndDate | `date` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| StartDate | `date` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| Title | `nvarchar(200)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_AcademicPeriods | PK | Id |
| UQ_AcademicPeriods_OrganizationId_Id | Alternate Key | OrganizationId, Id |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId | organization.Organizations | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_AcademicPeriods_DateRange | `[EndDate] > [StartDate]` |
| CK_AcademicPeriods_Status | `[Status] IN (1, 2)` |

### academic.Attendance

Entity: `MakanApp.Domain.Academic.Attendance`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| EnrollmentId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RecordedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| RecordedByMembershipId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SessionId | `uniqueidentifier` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Attendance | PK | Id |
| UQ_Attendance_Context_Id | Alternate Key | OrganizationId, ClassId, SessionId, EnrollmentId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Attendance_OrganizationId_RecordedByMembershipId | OrganizationId, RecordedByMembershipId | False | — |
| IX_Attendance_OrganizationId_ClassId_EnrollmentId | OrganizationId, ClassId, EnrollmentId | False | — |
| UX_Attendance_Organization_Session_Enrollment | OrganizationId, SessionId, EnrollmentId | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, RecordedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |
| OrganizationId, ClassId, EnrollmentId | academic.Enrollments | OrganizationId, ClassId, Id | Restrict |
| OrganizationId, ClassId, SessionId | academic.Sessions | OrganizationId, ClassId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Attendance_Status | `[Status] IN (1, 2, 3, 4)` |

### academic.AttendanceRevisions

Entity: `MakanApp.Domain.Academic.AttendanceRevision`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AttendanceId | `uniqueidentifier` | خیر | خیر | Never |
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| CorrectedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CorrectedByMembershipId | `uniqueidentifier` | خیر | خیر | Never |
| EnrollmentId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| NewStatus | `int` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| PreviousStatus | `int` | خیر | خیر | Never |
| Reason | `nvarchar(500)` | خیر | خیر | Never |
| SessionId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_AttendanceRevisions | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_AttendanceRevisions_OrganizationId_CorrectedByMembershipId | OrganizationId, CorrectedByMembershipId | False | — |
| IX_AttendanceRevisions_OrganizationId_ClassId_SessionId_EnrollmentId_AttendanceId | OrganizationId, ClassId, SessionId, EnrollmentId, AttendanceId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, CorrectedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |
| OrganizationId, ClassId, SessionId, EnrollmentId, AttendanceId | academic.Attendance | OrganizationId, ClassId, SessionId, EnrollmentId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_AttendanceRevisions_NewStatus | `[NewStatus] IN (1, 2, 3, 4)` |
| CK_AttendanceRevisions_PreviousStatus | `[PreviousStatus] IN (1, 2, 3, 4)` |
| CK_AttendanceRevisions_StatusChanged | `[PreviousStatus] <> [NewStatus]` |

### academic.Classes

Entity: `MakanApp.Domain.Academic.Class`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AcademicPeriodId | `uniqueidentifier` | خیر | خیر | Never |
| ActivatedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Capacity | `int` | خیر | خیر | Never |
| CourseId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| Title | `nvarchar(200)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Classes | PK | Id |
| UQ_Classes_OrganizationId_Id | Alternate Key | OrganizationId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Classes_OrganizationId_AcademicPeriodId | OrganizationId, AcademicPeriodId | False | — |
| IX_Classes_OrganizationId_CourseId | OrganizationId, CourseId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId | organization.Organizations | Id | Restrict |
| OrganizationId, AcademicPeriodId | academic.AcademicPeriods | OrganizationId, Id | Restrict |
| OrganizationId, CourseId | academic.Courses | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Classes_Capacity | `[Capacity] > 0` |
| CK_Classes_Status | `[Status] IN (1, 2, 3, 4)` |

### academic.ClassLessonAttachments

Entity: `MakanApp.Domain.Academic.ClassLessonAttachment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| DisplayOrder | `int` | خیر | خیر | Never |
| FileAssetId | `uniqueidentifier` | خیر | خیر | Never |
| LessonId | `uniqueidentifier` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ClassLessonAttachments | PK | LessonId, FileAssetId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ClassLessonAttachments_FileAssetId | FileAssetId | False | — |
| IX_ClassLessonAttachments_OrganizationId_LessonId | OrganizationId, LessonId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| FileAssetId | storage.FileAssets | Id | Restrict |
| OrganizationId, LessonId | academic.ClassLessons | OrganizationId, Id | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_ClassLessonAttachments_Order | `[DisplayOrder] BETWEEN 0 AND 9` |

### academic.ClassLessons

Entity: `MakanApp.Domain.Academic.ClassLesson`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Body | `nvarchar(max)` | خیر | خیر | Never |
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| ClientOperationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedByUserId | `uniqueidentifier` | خیر | خیر | Never |
| CreationRequestHash | `nvarchar(64)` | خیر | خیر | Never |
| DisplayOrder | `int` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| PublishedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RevisionNumber | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| Title | `nvarchar(200)` | خیر | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ClassLessons | PK | Id |
| AK_ClassLessons_OrganizationId_Id | Alternate Key | OrganizationId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ClassLessons_CreatedByUserId | CreatedByUserId | False | — |
| IX_ClassLessons_OrganizationId_CreatedByUserId_ClientOperationId | OrganizationId, CreatedByUserId, ClientOperationId | True | — |
| IX_ClassLessons_OrganizationId_ClassId_Status_DisplayOrder | OrganizationId, ClassId, Status, DisplayOrder | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| CreatedByUserId | identity.Users | Id | Restrict |
| OrganizationId, ClassId | academic.Classes | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ClassLessons_Content | `LEN([Title]) BETWEEN 1 AND 200 AND LEN([Body]) <= 50000 AND [DisplayOrder] BETWEEN 0 AND 100000 AND [RevisionNumber] > 0` |
| CK_ClassLessons_Publication | `([Status]=1 AND [PublishedAtUtc] IS NULL) OR ([Status]=2 AND [PublishedAtUtc] IS NOT NULL)` |
| CK_ClassLessons_Status | `[Status] IN (1,2)` |

### academic.Courses

Entity: `MakanApp.Domain.Academic.Course`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| Title | `nvarchar(200)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Courses | PK | Id |
| UQ_Courses_OrganizationId_Id | Alternate Key | OrganizationId, Id |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId | organization.Organizations | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Courses_Status | `[Status] IN (1, 2)` |

### academic.Enrollments

Entity: `MakanApp.Domain.Academic.Enrollment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| EnrolledAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LearnerOrganizationPersonId | `uniqueidentifier` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Enrollments | PK | Id |
| UQ_Enrollments_OrganizationId_ClassId_Id | Alternate Key | OrganizationId, ClassId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Enrollments_OrganizationId_LearnerOrganizationPersonId | OrganizationId, LearnerOrganizationPersonId | False | — |
| UX_Enrollments_Active_Organization_Class_Learner | OrganizationId, ClassId, LearnerOrganizationPersonId | True | [Status] = 1 AND [EndedAtUtc] IS NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ClassId | academic.Classes | OrganizationId, Id | Restrict |
| OrganizationId, LearnerOrganizationPersonId | organization.OrganizationPersons | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Enrollments_Status | `[Status] IN (1, 2, 3)` |

### academic.ScheduleRules

Entity: `MakanApp.Domain.Academic.ScheduleRule`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DurationMinutes | `int` | خیر | خیر | Never |
| EffectiveFrom | `date` | خیر | خیر | Never |
| EffectiveUntil | `date` | خیر | خیر | Never |
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LocalDayOfWeek | `int` | خیر | خیر | Never |
| LocalStartTime | `time(0)` | خیر | خیر | Never |
| MeetingUrl | `nvarchar(1000)` | بله | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SessionTitle | `nvarchar(200)` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| TimeZoneId | `nvarchar(100)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ScheduleRules | PK | Id |
| UQ_ScheduleRules_OrganizationId_ClassId_Id | Alternate Key | OrganizationId, ClassId, Id |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ClassId | academic.Classes | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ScheduleRules_DateRange | `[EffectiveUntil] >= [EffectiveFrom]` |
| CK_ScheduleRules_Duration | `[DurationMinutes] > 0 AND [DurationMinutes] <= 1440` |
| CK_ScheduleRules_LocalDayOfWeek | `[LocalDayOfWeek] BETWEEN 0 AND 6` |
| CK_ScheduleRules_Status | `[Status] IN (1, 2)` |

### academic.Sessions

Entity: `MakanApp.Domain.Academic.Session`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CancelledAtUtc | `datetime2(7)` | بله | خیر | Never |
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| CompletedAtUtc | `datetime2(7)` | بله | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EndUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MeetingUrl | `nvarchar(1000)` | بله | خیر | Never |
| OccurrenceLocalDate | `date` | بله | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| ScheduleRuleId | `uniqueidentifier` | بله | خیر | Never |
| StartUtc | `datetime2(7)` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| TimeZoneId | `nvarchar(100)` | خیر | خیر | Never |
| Title | `nvarchar(200)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Sessions | PK | Id |
| UQ_Sessions_OrganizationId_ClassId_Id | Alternate Key | OrganizationId, ClassId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Sessions_OrganizationId_ClassId_ScheduleRuleId | OrganizationId, ClassId, ScheduleRuleId | False | — |
| UX_Sessions_ScheduleRule_Occurrence | OrganizationId, ScheduleRuleId, OccurrenceLocalDate | True | [Status] = 1 AND [ScheduleRuleId] IS NOT NULL AND [OccurrenceLocalDate] IS NOT NULL |
| IX_Sessions_Organization_Class_Time | OrganizationId, ClassId, StartUtc, EndUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ClassId | academic.Classes | OrganizationId, Id | Restrict |
| OrganizationId, ClassId, ScheduleRuleId | academic.ScheduleRules | OrganizationId, ClassId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Sessions_Status | `[Status] IN (1, 2, 3)` |
| CK_Sessions_TimeRange | `[EndUtc] > [StartUtc]` |

### academic.TeacherAssignments

Entity: `MakanApp.Domain.Academic.TeacherAssignment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AssignedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| TeacherMembershipId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_TeacherAssignments | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_TeacherAssignments_OrganizationId_TeacherMembershipId | OrganizationId, TeacherMembershipId | False | — |
| UX_TeacherAssignments_Active_Organization_Class_Teacher | OrganizationId, ClassId, TeacherMembershipId | True | [Status] = 1 AND [EndedAtUtc] IS NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ClassId | academic.Classes | OrganizationId, Id | Restrict |
| OrganizationId, TeacherMembershipId | organization.Memberships | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_TeacherAssignments_Status | `[Status] IN (1, 2)` |

### assessment.AnswerRevisions

Entity: `MakanApp.Domain.Assessment.AnswerRevision`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AcceptedAnswerSetVersion | `bigint` | خیر | خیر | Never |
| AcceptedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| AcceptedWriteLeaseVersion | `bigint` | خیر | خیر | Never |
| AnswerType | `int` | خیر | خیر | Never |
| ClientOperationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedBySessionId | `uniqueidentifier` | خیر | خیر | Never |
| ExamAttemptId | `uniqueidentifier` | خیر | خیر | Never |
| ExamAttemptQuestionId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| QuestionVersionId | `uniqueidentifier` | خیر | خیر | Never |
| RequestHash | `varchar(64)` | خیر | خیر | Never |
| RevisionNumber | `int` | خیر | خیر | Never |
| SelectedOptionId | `uniqueidentifier` | بله | خیر | Never |
| SupersedesAnswerRevisionId | `uniqueidentifier` | بله | خیر | Never |
| TextAnswer | `nvarchar(max)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_AnswerRevisions | PK | Id |
| UQ_AnswerRevisions_Organization_Attempt_Question_Id | Alternate Key | OrganizationId, ExamAttemptId, ExamAttemptQuestionId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_AnswerRevisions_CreatedBySessionId | CreatedBySessionId | False | — |
| UX_AnswerRevisions_Organization_Attempt_ClientOperation | OrganizationId, ExamAttemptId, ClientOperationId | True | — |
| IX_AnswerRevisions_OrganizationId_QuestionVersionId_SelectedOptionId | OrganizationId, QuestionVersionId, SelectedOptionId | False | — |
| IX_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId | OrganizationId, ExamAttemptId, ExamAttemptQuestionId, QuestionVersionId | False | — |
| UX_AnswerRevisions_Organization_AttemptQuestion_Revision | OrganizationId, ExamAttemptId, ExamAttemptQuestionId, RevisionNumber | True | — |
| IX_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId | OrganizationId, ExamAttemptId, ExamAttemptQuestionId, SupersedesAnswerRevisionId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| CreatedBySessionId | identity.UserSessions | Id | Restrict |
| OrganizationId, ExamAttemptId | assessment.ExamAttempts | OrganizationId, Id | Restrict |
| OrganizationId, QuestionVersionId, SelectedOptionId | assessment.QuestionOptions | OrganizationId, QuestionVersionId, Id | Restrict |
| OrganizationId, ExamAttemptId, ExamAttemptQuestionId, QuestionVersionId | assessment.ExamAttemptQuestions | OrganizationId, ExamAttemptId, Id, QuestionVersionId | Restrict |
| OrganizationId, ExamAttemptId, ExamAttemptQuestionId, SupersedesAnswerRevisionId | assessment.AnswerRevisions | OrganizationId, ExamAttemptId, ExamAttemptQuestionId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_AnswerRevisions_AnswerSetVersion | `[AcceptedAnswerSetVersion] > 0` |
| CK_AnswerRevisions_AnswerShape | `([AnswerType] = 1 AND [SelectedOptionId] IS NOT NULL AND [TextAnswer] IS NULL) OR ([AnswerType] = 2 AND [SelectedOptionId] IS NULL AND [TextAnswer] IS NOT NULL)` |
| CK_AnswerRevisions_AnswerType | `[AnswerType] IN (1, 2)` |
| CK_AnswerRevisions_RequestHash | `LEN([RequestHash]) = 64` |
| CK_AnswerRevisions_RevisionNumber | `[RevisionNumber] > 0` |
| CK_AnswerRevisions_TextLength | `[TextAnswer] IS NULL OR DATALENGTH([TextAnswer]) <= 40000` |
| CK_AnswerRevisions_WriteLeaseVersion | `[AcceptedWriteLeaseVersion] > 0` |

### assessment.AssignmentRecipients

Entity: `MakanApp.Domain.Assessment.AssignmentRecipient`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AssignmentId | `uniqueidentifier` | خیر | خیر | Never |
| AssignmentVersionId | `uniqueidentifier` | خیر | خیر | Never |
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EnrollmentId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_AssignmentRecipients | PK | Id |
| UQ_AssignmentRecipients_SubmissionScope_Id | Alternate Key | OrganizationId, AssignmentId, AssignmentVersionId, EnrollmentId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_AssignmentRecipients_Organization_Version_Enrollment | OrganizationId, AssignmentVersionId, EnrollmentId | True | — |
| IX_AssignmentRecipients_OrganizationId_ClassId_EnrollmentId | OrganizationId, ClassId, EnrollmentId | False | — |
| IX_AssignmentRecipients_OrganizationId_ClassId_AssignmentId_AssignmentVersionId | OrganizationId, ClassId, AssignmentId, AssignmentVersionId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ClassId, EnrollmentId | academic.Enrollments | OrganizationId, ClassId, Id | Restrict |
| OrganizationId, ClassId, AssignmentId, AssignmentVersionId | assessment.AssignmentVersions | OrganizationId, ClassId, AssignmentId, Id | Restrict |

### assessment.Assignments

Entity: `MakanApp.Domain.Assessment.Assignment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedByMembershipId | `uniqueidentifier` | خیر | خیر | Never |
| CurrentVersionNumber | `int` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| PublishedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Assignments | PK | Id |
| UQ_Assignments_OrganizationId_ClassId_Id | Alternate Key | OrganizationId, ClassId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Assignments_OrganizationId_CreatedByMembershipId | OrganizationId, CreatedByMembershipId | False | — |
| IX_Assignments_Organization_Class_Status_UpdatedAt | OrganizationId, ClassId, Status, UpdatedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ClassId | academic.Classes | OrganizationId, Id | Restrict |
| OrganizationId, CreatedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Assignments_CurrentVersionNumber | `[CurrentVersionNumber] > 0` |
| CK_Assignments_PublicationState | `([Status] = 1 AND [PublishedAtUtc] IS NULL) OR ([Status] IN (2, 3, 4) AND [PublishedAtUtc] IS NOT NULL)` |
| CK_Assignments_Status | `[Status] IN (1, 2, 3, 4)` |
| CK_Assignments_UpdatedAt | `[UpdatedAtUtc] >= [CreatedAtUtc]` |

### assessment.AssignmentVersions

Entity: `MakanApp.Domain.Assessment.AssignmentVersion`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AllowLateSubmission | `bit` | خیر | خیر | Never |
| AssignmentId | `uniqueidentifier` | خیر | خیر | Never |
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedByMembershipId | `uniqueidentifier` | خیر | خیر | Never |
| Description | `nvarchar(max)` | خیر | خیر | Never |
| DueAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MaxAttempts | `int` | خیر | خیر | Never |
| MaxScore | `decimal(9,2)` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| PublishedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Title | `nvarchar(200)` | خیر | خیر | Never |
| VersionNumber | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_AssignmentVersions | PK | Id |
| UQ_AssignmentVersions_Organization_Assignment_Id | Alternate Key | OrganizationId, AssignmentId, Id |
| UQ_AssignmentVersions_Organization_Class_Assignment_Id | Alternate Key | OrganizationId, ClassId, AssignmentId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_AssignmentVersions_OrganizationId_CreatedByMembershipId | OrganizationId, CreatedByMembershipId | False | — |
| UX_AssignmentVersions_Organization_Assignment_VersionNumber | OrganizationId, AssignmentId, VersionNumber | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, CreatedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |
| OrganizationId, ClassId, AssignmentId | assessment.Assignments | OrganizationId, ClassId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_AssignmentVersions_DueAt | `[DueAtUtc] > [CreatedAtUtc]` |
| CK_AssignmentVersions_MaxAttempts | `[MaxAttempts] > 0` |
| CK_AssignmentVersions_MaxScore | `[MaxScore] > 0` |
| CK_AssignmentVersions_PublishedDueAt | `[PublishedAtUtc] IS NULL OR [DueAtUtc] > [PublishedAtUtc]` |
| CK_AssignmentVersions_VersionNumber | `[VersionNumber] > 0` |

### assessment.EvaluationRevisions

Entity: `MakanApp.Domain.Assessment.EvaluationRevision`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CorrectionReason | `nvarchar(1000)` | بله | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedByMembershipId | `uniqueidentifier` | خیر | خیر | Never |
| GuardianVisibleFeedback | `nvarchar(max)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LearnerFeedback | `nvarchar(max)` | بله | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RevisionNumber | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Score | `decimal(9,2)` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| SubmissionAttemptId | `uniqueidentifier` | خیر | خیر | Never |
| SupersedesEvaluationRevisionId | `uniqueidentifier` | بله | خیر | Never |
| TeacherPrivateNote | `nvarchar(max)` | بله | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_EvaluationRevisions | PK | Id |
| UQ_EvaluationRevisions_Organization_Attempt_Id | Alternate Key | OrganizationId, SubmissionAttemptId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_EvaluationRevisions_OrganizationId_CreatedByMembershipId | OrganizationId, CreatedByMembershipId | False | — |
| UX_EvaluationRevisions_Attempt_RevisionNumber | OrganizationId, SubmissionAttemptId, RevisionNumber | True | — |
| UX_EvaluationRevisions_OneCurrentStatus | OrganizationId, SubmissionAttemptId, Status | True | [Status] IN (1, 2) |
| IX_EvaluationRevisions_OrganizationId_SubmissionAttemptId_SupersedesEvaluationRevisionId | OrganizationId, SubmissionAttemptId, SupersedesEvaluationRevisionId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, CreatedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |
| OrganizationId, SubmissionAttemptId | assessment.SubmissionAttempts | OrganizationId, Id | Restrict |
| OrganizationId, SubmissionAttemptId, SupersedesEvaluationRevisionId | assessment.EvaluationRevisions | OrganizationId, SubmissionAttemptId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_EvaluationRevisions_Correction | `([RevisionNumber] = 1 AND [SupersedesEvaluationRevisionId] IS NULL AND [CorrectionReason] IS NULL) OR ([RevisionNumber] > 1 AND [SupersedesEvaluationRevisionId] IS NOT NULL AND [CorrectionReason] IS NOT NULL)` |
| CK_EvaluationRevisions_RevisionNumber | `[RevisionNumber] > 0` |
| CK_EvaluationRevisions_Score | `[Score] >= 0` |
| CK_EvaluationRevisions_Status | `[Status] IN (1, 2, 3)` |

### assessment.ExamAttemptQuestions

Entity: `MakanApp.Domain.Assessment.ExamAttemptQuestion`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CurrentAnswerRevisionId | `uniqueidentifier` | بله | خیر | Never |
| DisplayOrder | `int` | خیر | خیر | Never |
| ExamAttemptId | `uniqueidentifier` | خیر | خیر | Never |
| ExamVersionId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| QuestionVersionId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ExamAttemptQuestions | PK | Id |
| UQ_ExamAttemptQuestions_Organization_Attempt_Id_QuestionVersion | Alternate Key | OrganizationId, ExamAttemptId, Id, QuestionVersionId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_ExamAttemptQuestions_Organization_Attempt_DisplayOrder | OrganizationId, ExamAttemptId, DisplayOrder | True | — |
| UX_ExamAttemptQuestions_Organization_Attempt_Question | OrganizationId, ExamAttemptId, QuestionVersionId | True | — |
| IX_ExamAttemptQuestions_OrganizationId_ExamVersionId_ExamAttemptId | OrganizationId, ExamVersionId, ExamAttemptId | False | — |
| IX_ExamAttemptQuestions_OrganizationId_ExamVersionId_QuestionVersionId | OrganizationId, ExamVersionId, QuestionVersionId | False | — |
| IX_ExamAttemptQuestions_OrganizationId_ExamAttemptId_Id_CurrentAnswerRevisionId | OrganizationId, ExamAttemptId, Id, CurrentAnswerRevisionId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ExamVersionId, ExamAttemptId | assessment.ExamAttempts | OrganizationId, ExamVersionId, Id | Cascade |
| OrganizationId, ExamVersionId, QuestionVersionId | assessment.QuestionVersions | OrganizationId, ExamVersionId, Id | Restrict |
| OrganizationId, ExamAttemptId, Id, CurrentAnswerRevisionId | assessment.AnswerRevisions | OrganizationId, ExamAttemptId, ExamAttemptQuestionId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ExamAttemptQuestions_DisplayOrder | `[DisplayOrder] > 0` |

### assessment.ExamAttempts

Entity: `MakanApp.Domain.Assessment.ExamAttempt`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AnswerSetVersion | `bigint` | خیر | خیر | Never |
| AttemptNumber | `int` | خیر | خیر | Never |
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| ClientOperationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EffectiveDeadlineUtc | `datetime2(7)` | خیر | خیر | Never |
| EnrollmentId | `uniqueidentifier` | خیر | خیر | Never |
| ExamId | `uniqueidentifier` | خیر | خیر | Never |
| ExamVersionId | `uniqueidentifier` | خیر | خیر | Never |
| ExpiredAtUtc | `datetime2(7)` | بله | خیر | Never |
| FinalizeClientOperationId | `uniqueidentifier` | بله | خیر | Never |
| FinalizedAnswerSetVersion | `bigint` | بله | خیر | Never |
| FinalizedAtUtc | `datetime2(7)` | بله | خیر | Never |
| FinalizedBySessionId | `uniqueidentifier` | بله | خیر | Never |
| FinalizeRequestHash | `varchar(64)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| StartedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| WriteLeaseAcquiredAtUtc | `datetime2(7)` | بله | خیر | Never |
| WriteLeaseVersion | `bigint` | خیر | خیر | Never |
| WriterSessionId | `uniqueidentifier` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ExamAttempts | PK | Id |
| UQ_ExamAttempts_Organization_Id | Alternate Key | OrganizationId, Id |
| UQ_ExamAttempts_Organization_ExamVersion_Id | Alternate Key | OrganizationId, ExamVersionId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ExamAttempts_FinalizedBySessionId | FinalizedBySessionId | False | — |
| IX_ExamAttempts_WriterSessionId | WriterSessionId | False | — |
| IX_ExamAttempts_OrganizationId_ClassId_EnrollmentId | OrganizationId, ClassId, EnrollmentId | False | — |
| IX_ExamAttempts_OrganizationId_ClassId_ExamId | OrganizationId, ClassId, ExamId | False | — |
| UX_ExamAttempts_Organization_Enrollment_ClientOperation | OrganizationId, EnrollmentId, ClientOperationId | True | — |
| UX_ExamAttempts_Organization_Exam_Enrollment_Active | OrganizationId, ExamId, EnrollmentId | True | [Status] = 1 |
| IX_ExamAttempts_OrganizationId_ExamId_ExamVersionId | OrganizationId, ExamId, ExamVersionId | False | — |
| UX_ExamAttempts_Organization_Exam_Enrollment_Number | OrganizationId, ExamId, EnrollmentId, AttemptNumber | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| FinalizedBySessionId | identity.UserSessions | Id | Restrict |
| WriterSessionId | identity.UserSessions | Id | Restrict |
| OrganizationId, ClassId, EnrollmentId | academic.Enrollments | OrganizationId, ClassId, Id | Restrict |
| OrganizationId, ClassId, ExamId | assessment.Exams | OrganizationId, ClassId, Id | Restrict |
| OrganizationId, ExamId, ExamVersionId | assessment.ExamVersions | OrganizationId, ExamId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ExamAttempts_AnswerSetVersion | `[AnswerSetVersion] >= 0` |
| CK_ExamAttempts_AttemptNumber | `[AttemptNumber] > 0` |
| CK_ExamAttempts_Deadline | `[EffectiveDeadlineUtc] > [StartedAtUtc]` |
| CK_ExamAttempts_ExpirationState | `([Status] IN (1, 3) AND [ExpiredAtUtc] IS NULL) OR ([Status] = 2 AND [ExpiredAtUtc] IS NOT NULL)` |
| CK_ExamAttempts_FinalizationState | `([Status] = 3 AND [FinalizedAtUtc] IS NOT NULL AND [FinalizedAnswerSetVersion] IS NOT NULL AND [FinalizeClientOperationId] IS NOT NULL AND [FinalizeRequestHash] IS NOT NULL AND [FinalizedBySessionId] IS NOT NULL) OR ([Status] <> 3 AND [FinalizedAtUtc] IS NULL AND [FinalizedAnswerSetVersion] IS NULL AND [FinalizeClientOperationId] IS NULL AND [FinalizeRequestHash] IS NULL AND [FinalizedBySessionId] IS NULL)` |
| CK_ExamAttempts_FinalizeRequestHash | `[FinalizeRequestHash] IS NULL OR LEN([FinalizeRequestHash]) = 64` |
| CK_ExamAttempts_FinalizedAnswerSetVersion | `[FinalizedAnswerSetVersion] IS NULL OR ([FinalizedAnswerSetVersion] >= 0 AND [FinalizedAnswerSetVersion] = [AnswerSetVersion])` |
| CK_ExamAttempts_FinalizedBeforeDeadline | `[FinalizedAtUtc] IS NULL OR [FinalizedAtUtc] < [EffectiveDeadlineUtc]` |
| CK_ExamAttempts_Status | `[Status] IN (1, 2, 3)` |
| CK_ExamAttempts_WriteLeaseState | `([WriterSessionId] IS NULL AND [WriteLeaseVersion] = 0 AND [WriteLeaseAcquiredAtUtc] IS NULL) OR ([WriterSessionId] IS NOT NULL AND [WriteLeaseVersion] > 0 AND [WriteLeaseAcquiredAtUtc] IS NOT NULL)` |
| CK_ExamAttempts_WriteLeaseVersion | `[WriteLeaseVersion] >= 0` |

### assessment.ExamFinalAnswers

Entity: `MakanApp.Domain.Assessment.ExamFinalAnswer`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AnswerRevisionId | `uniqueidentifier` | خیر | خیر | Never |
| ExamAttemptId | `uniqueidentifier` | خیر | خیر | Never |
| ExamAttemptQuestionId | `uniqueidentifier` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| QuestionVersionId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ExamFinalAnswers | PK | OrganizationId, ExamAttemptId, ExamAttemptQuestionId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ExamFinalAnswers_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId | OrganizationId, ExamAttemptId, ExamAttemptQuestionId, AnswerRevisionId | False | — |
| IX_ExamFinalAnswers_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId | OrganizationId, ExamAttemptId, ExamAttemptQuestionId, QuestionVersionId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ExamAttemptId | assessment.ExamAttempts | OrganizationId, Id | Restrict |
| OrganizationId, ExamAttemptId, ExamAttemptQuestionId, AnswerRevisionId | assessment.AnswerRevisions | OrganizationId, ExamAttemptId, ExamAttemptQuestionId, Id | Restrict |
| OrganizationId, ExamAttemptId, ExamAttemptQuestionId, QuestionVersionId | assessment.ExamAttemptQuestions | OrganizationId, ExamAttemptId, Id, QuestionVersionId | Restrict |

### assessment.ExamGradeReleases

Entity: `MakanApp.Domain.Assessment.ExamGradeRelease`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ClientOperationId | `uniqueidentifier` | خیر | خیر | Never |
| ExamAttemptId | `uniqueidentifier` | خیر | خیر | Never |
| ExamGradeRevisionId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| ReleasedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| ReleasedByMembershipId | `uniqueidentifier` | خیر | خیر | Never |
| RequestHash | `varchar(64)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ExamGradeReleases | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_ExamGradeReleases_Organization_Revision | OrganizationId, ExamGradeRevisionId | True | — |
| IX_ExamGradeReleases_OrganizationId_ReleasedByMembershipId | OrganizationId, ReleasedByMembershipId | False | — |
| UX_ExamGradeReleases_Attempt_ClientOperation | OrganizationId, ExamAttemptId, ClientOperationId | True | — |
| IX_ExamGradeReleases_OrganizationId_ExamAttemptId_ExamGradeRevisionId | OrganizationId, ExamAttemptId, ExamGradeRevisionId | False | — |
| IX_ExamGradeReleases_Attempt_ReleasedAtUtc | OrganizationId, ExamAttemptId, ReleasedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ReleasedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |
| OrganizationId, ExamAttemptId, ExamGradeRevisionId | assessment.ExamGradeRevisions | OrganizationId, ExamAttemptId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ExamGradeReleases_RequestHash | `LEN([RequestHash]) = 64` |

### assessment.ExamGradeRevisions

Entity: `MakanApp.Domain.Assessment.ExamGradeRevision`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CorrectionReason | `nvarchar(1000)` | بله | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedByMembershipId | `uniqueidentifier` | خیر | خیر | Never |
| EvaluatorPrivateNote | `nvarchar(max)` | بله | خیر | Never |
| ExamAttemptId | `uniqueidentifier` | خیر | خیر | Never |
| GuardianVisibleFeedback | `nvarchar(max)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LearnerFeedback | `nvarchar(max)` | بله | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RevisionNumber | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| SupersedesExamGradeRevisionId | `uniqueidentifier` | بله | خیر | Never |
| TotalScore | `decimal(9,2)` | خیر | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ExamGradeRevisions | PK | Id |
| UQ_ExamGradeRevisions_Organization_Attempt_Id | Alternate Key | OrganizationId, ExamAttemptId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ExamGradeRevisions_OrganizationId_CreatedByMembershipId | OrganizationId, CreatedByMembershipId | False | — |
| UX_ExamGradeRevisions_OneMutable | OrganizationId, ExamAttemptId | True | [Status] IN (1, 2) |
| UX_ExamGradeRevisions_Attempt_RevisionNumber | OrganizationId, ExamAttemptId, RevisionNumber | True | — |
| UX_ExamGradeRevisions_OneReleased | OrganizationId, ExamAttemptId, Status | True | [Status] = 3 |
| IX_ExamGradeRevisions_OrganizationId_ExamAttemptId_SupersedesExamGradeRevisionId | OrganizationId, ExamAttemptId, SupersedesExamGradeRevisionId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, CreatedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |
| OrganizationId, ExamAttemptId | assessment.ExamAttempts | OrganizationId, Id | Restrict |
| OrganizationId, ExamAttemptId, SupersedesExamGradeRevisionId | assessment.ExamGradeRevisions | OrganizationId, ExamAttemptId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ExamGradeRevisions_Correction | `([RevisionNumber] = 1 AND [SupersedesExamGradeRevisionId] IS NULL AND [CorrectionReason] IS NULL) OR ([RevisionNumber] > 1 AND [SupersedesExamGradeRevisionId] IS NOT NULL AND [CorrectionReason] IS NOT NULL)` |
| CK_ExamGradeRevisions_RevisionNumber | `[RevisionNumber] > 0` |
| CK_ExamGradeRevisions_Status | `[Status] IN (1, 2, 3, 4)` |
| CK_ExamGradeRevisions_TotalScore | `[TotalScore] >= 0` |
| CK_ExamGradeRevisions_UpdatedAt | `[UpdatedAtUtc] >= [CreatedAtUtc]` |

### assessment.ExamQuestionGrades

Entity: `MakanApp.Domain.Assessment.ExamQuestionGrade`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AwardedScore | `decimal(9,2)` | خیر | خیر | Never |
| EvaluatorPrivateNote | `nvarchar(max)` | بله | خیر | Never |
| ExamAttemptId | `uniqueidentifier` | خیر | خیر | Never |
| ExamAttemptQuestionId | `uniqueidentifier` | خیر | خیر | Never |
| ExamGradeRevisionId | `uniqueidentifier` | خیر | خیر | Never |
| GradingMode | `int` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| IsAnswered | `bit` | خیر | خیر | Never |
| IsReviewed | `bit` | خیر | خیر | Never |
| LearnerFeedback | `nvarchar(max)` | بله | خیر | Never |
| MaximumScore | `decimal(9,2)` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| QuestionVersionId | `uniqueidentifier` | خیر | خیر | Never |
| ReviewedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ReviewedByMembershipId | `uniqueidentifier` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ExamQuestionGrades | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ExamQuestionGrades_OrganizationId_ReviewedByMembershipId | OrganizationId, ReviewedByMembershipId | False | — |
| IX_ExamQuestionGrades_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId | OrganizationId, ExamAttemptId, ExamAttemptQuestionId, QuestionVersionId | False | — |
| UX_ExamQuestionGrades_Revision_AttemptQuestion | OrganizationId, ExamAttemptId, ExamGradeRevisionId, ExamAttemptQuestionId | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ReviewedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |
| OrganizationId, ExamAttemptId, ExamGradeRevisionId | assessment.ExamGradeRevisions | OrganizationId, ExamAttemptId, Id | Restrict |
| OrganizationId, ExamAttemptId, ExamAttemptQuestionId, QuestionVersionId | assessment.ExamAttemptQuestions | OrganizationId, ExamAttemptId, Id, QuestionVersionId | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ExamQuestionGrades_AwardedScore | `[AwardedScore] >= 0 AND [AwardedScore] <= [MaximumScore]` |
| CK_ExamQuestionGrades_GradingMode | `[GradingMode] IN (1, 2)` |
| CK_ExamQuestionGrades_MaximumScore | `[MaximumScore] > 0` |
| CK_ExamQuestionGrades_ObjectiveReviewed | `[GradingMode] <> 1 OR [IsReviewed] = 1` |
| CK_ExamQuestionGrades_ReviewState | `([IsReviewed] = 0 AND [ReviewedByMembershipId] IS NULL AND [ReviewedAtUtc] IS NULL) OR ([IsReviewed] = 1 AND [ReviewedByMembershipId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL)` |

### assessment.Exams

Entity: `MakanApp.Domain.Assessment.Exam`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ClassId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedByMembershipId | `uniqueidentifier` | خیر | خیر | Never |
| CurrentVersionNumber | `int` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LatestPublishedVersionNumber | `int` | بله | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Exams | PK | Id |
| UQ_Exams_OrganizationId_Id | Alternate Key | OrganizationId, Id |
| UQ_Exams_Organization_Class_Id | Alternate Key | OrganizationId, ClassId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Exams_OrganizationId_CreatedByMembershipId | OrganizationId, CreatedByMembershipId | False | — |
| IX_Exams_Organization_Class_Status | OrganizationId, ClassId, Status | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ClassId | academic.Classes | OrganizationId, Id | Restrict |
| OrganizationId, CreatedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Exams_CurrentVersionNumber | `[CurrentVersionNumber] > 0` |
| CK_Exams_PublicationState | `([Status] = 1 AND [LatestPublishedVersionNumber] IS NULL) OR ([Status] = 2 AND [LatestPublishedVersionNumber] IS NOT NULL)` |
| CK_Exams_PublishedVersion | `[LatestPublishedVersionNumber] IS NULL OR ([LatestPublishedVersionNumber] > 0 AND [LatestPublishedVersionNumber] <= [CurrentVersionNumber])` |
| CK_Exams_Status | `[Status] IN (1, 2)` |

### assessment.ExamVersions

Entity: `MakanApp.Domain.Assessment.ExamVersion`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AvailableFromUtc | `datetime2(7)` | خیر | خیر | Never |
| AvailableUntilUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedByMembershipId | `uniqueidentifier` | خیر | خیر | Never |
| Description | `nvarchar(max)` | بله | خیر | Never |
| DurationMinutes | `int` | خیر | خیر | Never |
| ExamId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MaxAttempts | `int` | خیر | خیر | Never |
| MaxScore | `decimal(9,2)` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| PublishedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RandomizationPolicy | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| Title | `nvarchar(200)` | خیر | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| VersionNumber | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ExamVersions | PK | Id |
| UQ_ExamVersions_OrganizationId_Id | Alternate Key | OrganizationId, Id |
| UQ_ExamVersions_Organization_Exam_Id | Alternate Key | OrganizationId, ExamId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ExamVersions_OrganizationId_CreatedByMembershipId | OrganizationId, CreatedByMembershipId | False | — |
| UX_ExamVersions_Organization_Exam_ActiveDraft | OrganizationId, ExamId | True | [Status] = 1 |
| UX_ExamVersions_Organization_Exam_VersionNumber | OrganizationId, ExamId, VersionNumber | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, CreatedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |
| OrganizationId, ExamId | assessment.Exams | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ExamVersions_Duration | `[DurationMinutes] > 0` |
| CK_ExamVersions_MaxAttempts | `[MaxAttempts] > 0` |
| CK_ExamVersions_MaxScore | `[MaxScore] > 0` |
| CK_ExamVersions_PublicationState | `([Status] = 1 AND [PublishedAtUtc] IS NULL) OR ([Status] = 2 AND [PublishedAtUtc] IS NOT NULL)` |
| CK_ExamVersions_Randomization | `[RandomizationPolicy] IN (1, 2)` |
| CK_ExamVersions_Status | `[Status] IN (1, 2)` |
| CK_ExamVersions_UpdatedAt | `[UpdatedAtUtc] >= [CreatedAtUtc]` |
| CK_ExamVersions_VersionNumber | `[VersionNumber] > 0` |
| CK_ExamVersions_Window | `[AvailableUntilUtc] > [AvailableFromUtc]` |

### assessment.GradeReleases

Entity: `MakanApp.Domain.Assessment.GradeRelease`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| EvaluationRevisionId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| ReleasedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| ReleasedByMembershipId | `uniqueidentifier` | خیر | خیر | Never |
| SubmissionAttemptId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_GradeReleases | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_GradeReleases_Organization_EvaluationRevision | OrganizationId, EvaluationRevisionId | True | — |
| IX_GradeReleases_OrganizationId_ReleasedByMembershipId | OrganizationId, ReleasedByMembershipId | False | — |
| IX_GradeReleases_OrganizationId_SubmissionAttemptId_EvaluationRevisionId | OrganizationId, SubmissionAttemptId, EvaluationRevisionId | False | — |
| IX_GradeReleases_Attempt_ReleasedAtUtc | OrganizationId, SubmissionAttemptId, ReleasedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ReleasedByMembershipId | organization.Memberships | OrganizationId, Id | Restrict |
| OrganizationId, SubmissionAttemptId, EvaluationRevisionId | assessment.EvaluationRevisions | OrganizationId, SubmissionAttemptId, Id | Restrict |

### assessment.QuestionOptions

Entity: `MakanApp.Domain.Assessment.QuestionOption`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| IsCorrect | `bit` | خیر | خیر | Never |
| Order | `int` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| QuestionVersionId | `uniqueidentifier` | خیر | خیر | Never |
| Text | `nvarchar(2000)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_QuestionOptions | PK | Id |
| UQ_QuestionOptions_Organization_QuestionVersion_Id | Alternate Key | OrganizationId, QuestionVersionId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_QuestionOptions_Organization_Question_Order | OrganizationId, QuestionVersionId, Order | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, QuestionVersionId | assessment.QuestionVersions | OrganizationId, Id | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_QuestionOptions_Order | `[Order] > 0` |

### assessment.QuestionVersions

Entity: `MakanApp.Domain.Assessment.QuestionVersion`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| ExamVersionId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| Order | `int` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| Prompt | `nvarchar(max)` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Score | `decimal(9,2)` | خیر | خیر | Never |
| Type | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_QuestionVersions | PK | Id |
| UQ_QuestionVersions_OrganizationId_Id | Alternate Key | OrganizationId, Id |
| UQ_QuestionVersions_Organization_ExamVersion_Id | Alternate Key | OrganizationId, ExamVersionId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_QuestionVersions_Organization_ExamVersion_Order | OrganizationId, ExamVersionId, Order | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, ExamVersionId | assessment.ExamVersions | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_QuestionVersions_Order | `[Order] > 0` |
| CK_QuestionVersions_Score | `[Score] > 0` |
| CK_QuestionVersions_Type | `[Type] IN (1, 2)` |

### assessment.SubmissionAttachments

Entity: `MakanApp.Domain.Assessment.SubmissionAttachment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AttachedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| FileAssetId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| SubmissionAttemptId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_SubmissionAttachments | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_SubmissionAttachments_FileAssetId | FileAssetId | False | — |
| UX_SubmissionAttachments_Attempt_FileAsset | OrganizationId, SubmissionAttemptId, FileAssetId | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| FileAssetId | storage.FileAssets | Id | Restrict |
| OrganizationId, SubmissionAttemptId | assessment.SubmissionAttempts | OrganizationId, Id | Restrict |

### assessment.SubmissionAttempts

Entity: `MakanApp.Domain.Assessment.SubmissionAttempt`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AnswerText | `nvarchar(max)` | بله | خیر | Never |
| AssignmentId | `uniqueidentifier` | خیر | خیر | Never |
| AssignmentRecipientId | `uniqueidentifier` | خیر | خیر | Never |
| AssignmentVersionId | `uniqueidentifier` | خیر | خیر | Never |
| AttemptNumber | `int` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EnrollmentId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| IsLate | `bit` | خیر | خیر | Never |
| LastSavedAtUtc | `datetime2(7)` | بله | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| SubmittedAtUtc | `datetime2(7)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_SubmissionAttempts | PK | Id |
| UQ_SubmissionAttempts_Organization_Id | Alternate Key | OrganizationId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_SubmissionAttempts_OneDraft | OrganizationId, AssignmentRecipientId, AssignmentVersionId | True | [Status] = 1 |
| IX_SubmissionAttempts_Assignment_Status_SubmittedAtUtc | OrganizationId, AssignmentId, Status, SubmittedAtUtc | False | — |
| UX_SubmissionAttempts_Recipient_Version_Number | OrganizationId, AssignmentRecipientId, AssignmentVersionId, AttemptNumber | True | — |
| IX_SubmissionAttempts_OrganizationId_AssignmentId_AssignmentVersionId_EnrollmentId_AssignmentRecipientId | OrganizationId, AssignmentId, AssignmentVersionId, EnrollmentId, AssignmentRecipientId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, AssignmentId, AssignmentVersionId | assessment.AssignmentVersions | OrganizationId, AssignmentId, Id | Restrict |
| OrganizationId, AssignmentId, AssignmentVersionId, EnrollmentId, AssignmentRecipientId | assessment.AssignmentRecipients | OrganizationId, AssignmentId, AssignmentVersionId, EnrollmentId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_SubmissionAttempts_AttemptNumber | `[AttemptNumber] > 0` |
| CK_SubmissionAttempts_State | `([Status] = 1 AND [SubmittedAtUtc] IS NULL AND [IsLate] = 0) OR ([Status] = 2 AND [SubmittedAtUtc] IS NOT NULL)` |
| CK_SubmissionAttempts_Status | `[Status] IN (1, 2)` |
| CK_SubmissionAttempts_Timestamps | `[LastSavedAtUtc] IS NULL OR [LastSavedAtUtc] >= [CreatedAtUtc]` |

### commerce.Orders

Entity: `MakanApp.Domain.Commerce.Order`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Amount | `decimal(18,2)` | خیر | خیر | Never |
| CancelledAtUtc | `datetime2(7)` | بله | خیر | Never |
| ClientOperationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Currency | `char(3)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | بله | خیر | Never |
| PaidAtUtc | `datetime2(7)` | بله | خیر | Never |
| PlanVersionId | `uniqueidentifier` | خیر | خیر | Never |
| PurchaserUserId | `uniqueidentifier` | خیر | خیر | Never |
| RequestPayloadHash | `binary(32)` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Orders | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Orders_PlanVersionId | PlanVersionId | False | — |
| UX_Orders_Purchaser_ClientOperation | PurchaserUserId, ClientOperationId | True | — |
| IX_Orders_Purchaser_CreatedAt | PurchaserUserId, CreatedAtUtc | False | — |
| IX_Orders_Organization_Status_CreatedAt | OrganizationId, Status, CreatedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId | organization.Organizations | Id | Restrict |
| PlanVersionId | commerce.PlanVersions | Id | Restrict |
| PurchaserUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Orders_Amount | `[Amount] > 0` |
| CK_Orders_Currency | `[Currency] = 'IRR'` |
| CK_Orders_Lifecycle | `([Status] = 1 AND [PaidAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = 2 AND [PaidAtUtc] IS NOT NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = 3 AND [PaidAtUtc] IS NULL AND [CancelledAtUtc] IS NOT NULL)` |
| CK_Orders_RequestPayloadHash | `DATALENGTH([RequestPayloadHash]) = 32` |
| CK_Orders_Status | `[Status] BETWEEN 1 AND 3` |

### commerce.PaymentAttempts

Entity: `MakanApp.Domain.Commerce.PaymentAttempt`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Amount | `decimal(18,2)` | خیر | خیر | Never |
| ClientOperationId | `uniqueidentifier` | خیر | خیر | Never |
| CompletedAtUtc | `datetime2(7)` | بله | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Currency | `char(3)` | خیر | خیر | Never |
| FailureCode | `nvarchar(100)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| InitiatedAtUtc | `datetime2(7)` | بله | خیر | Never |
| InitiationClaimedUntilUtc | `datetime2(7)` | بله | خیر | Never |
| InitiationClaimToken | `uniqueidentifier` | بله | خیر | Never |
| OrderId | `uniqueidentifier` | خیر | خیر | Never |
| PaymentUrl | `nvarchar(2048)` | بله | خیر | Never |
| Provider | `nvarchar(50)` | خیر | خیر | Never |
| ProviderReference | `nvarchar(200)` | بله | خیر | Never |
| RequestPayloadHash | `binary(32)` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| VerifiedAtUtc | `datetime2(7)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PaymentAttempts | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_PaymentAttempts_Order_ClientOperation | OrderId, ClientOperationId | True | — |
| IX_PaymentAttempts_Order_Status | OrderId, Status | False | — |
| UX_PaymentAttempts_Provider_Reference | Provider, ProviderReference | True | [ProviderReference] IS NOT NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrderId | commerce.Orders | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_PaymentAttempts_Amount | `[Amount] > 0` |
| CK_PaymentAttempts_Claim | `([Status] = 2 AND [InitiationClaimToken] IS NOT NULL AND [InitiationClaimedUntilUtc] IS NOT NULL) OR ([Status] <> 2 AND [InitiationClaimToken] IS NULL AND [InitiationClaimedUntilUtc] IS NULL)` |
| CK_PaymentAttempts_Currency | `[Currency] = 'IRR'` |
| CK_PaymentAttempts_RequestPayloadHash | `DATALENGTH([RequestPayloadHash]) = 32` |
| CK_PaymentAttempts_Status | `[Status] BETWEEN 1 AND 6` |

### commerce.Payments

Entity: `MakanApp.Domain.Commerce.Payment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Amount | `decimal(18,2)` | خیر | خیر | Never |
| Currency | `char(3)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrderId | `uniqueidentifier` | خیر | خیر | Never |
| PaidAtUtc | `datetime2(7)` | خیر | خیر | Never |
| PaymentAttemptId | `uniqueidentifier` | خیر | خیر | Never |
| Provider | `nvarchar(50)` | خیر | خیر | Never |
| ProviderReference | `nvarchar(200)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Payments | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_Payments_Order | OrderId | True | — |
| UX_Payments_Attempt | PaymentAttemptId | True | — |
| UX_Payments_Provider_Reference | Provider, ProviderReference | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrderId | commerce.Orders | Id | Restrict |
| PaymentAttemptId | commerce.PaymentAttempts | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Payments_Amount | `[Amount] > 0` |
| CK_Payments_Currency | `[Currency] = 'IRR'` |

### commerce.PlanEntitlements

Entity: `MakanApp.Domain.Commerce.PlanEntitlement`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Enabled | `bit` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| Key | `int` | خیر | خیر | Never |
| PlanVersionId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PlanEntitlements | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_PlanEntitlements_Version_Key | PlanVersionId, Key | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| PlanVersionId | commerce.PlanVersions | Id | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_PlanEntitlements_Key | `[Key] BETWEEN 1 AND 6` |

### commerce.PlanQuotaDefinitions

Entity: `MakanApp.Domain.Commerce.PlanQuotaDefinition`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| Key | `int` | خیر | خیر | Never |
| Limit | `bigint` | بله | خیر | Never |
| PlanVersionId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PlanQuotaDefinitions | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_PlanQuotaDefinitions_Version_Key | PlanVersionId, Key | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| PlanVersionId | commerce.PlanVersions | Id | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_PlanQuotaDefinitions_Key | `[Key] BETWEEN 1 AND 4` |
| CK_PlanQuotaDefinitions_Limit | `[Limit] IS NULL OR [Limit] >= 0` |

### commerce.Plans

Entity: `MakanApp.Domain.Commerce.Plan`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Code | `nvarchar(100)` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DisplayName | `nvarchar(200)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Plans | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_Plans_Code | Code | True | — |

### commerce.PlanVersionPrices

Entity: `MakanApp.Domain.Commerce.PlanVersionPrice`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Amount | `decimal(18,2)` | خیر | خیر | Never |
| Currency | `char(3)` | خیر | خیر | Never |
| PlanVersionId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PlanVersionPrices | PK | PlanVersionId |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| PlanVersionId | commerce.PlanVersions | Id | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_PlanVersionPrices_Amount | `[Amount] > 0` |
| CK_PlanVersionPrices_Currency | `[Currency] = 'IRR'` |

### commerce.PlanVersions

Entity: `MakanApp.Domain.Commerce.PlanVersion`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| PlanId | `uniqueidentifier` | خیر | خیر | Never |
| PublishedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| VersionNumber | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PlanVersions | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_PlanVersions_Plan_VersionNumber | PlanId, VersionNumber | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| PlanId | commerce.Plans | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_PlanVersions_Publication | `([Status] = 1 AND [PublishedAtUtc] IS NULL) OR ([Status] = 2 AND [PublishedAtUtc] IS NOT NULL)` |
| CK_PlanVersions_Status | `[Status] IN (1, 2)` |
| CK_PlanVersions_VersionNumber | `[VersionNumber] > 0` |

### commerce.Subscriptions

Entity: `MakanApp.Domain.Commerce.Subscription`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ActivatedAtUtc | `datetime2(7)` | بله | خیر | Never |
| CancelledAtUtc | `datetime2(7)` | بله | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EndsAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| PlanVersionId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SourceOrderId | `uniqueidentifier` | بله | خیر | Never |
| StartsAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Subscriptions | PK | Id |
| UQ_Subscriptions_Organization_Id | Alternate Key | OrganizationId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_Subscriptions_Organization_Active | OrganizationId | True | [Status] = 2 |
| IX_Subscriptions_PlanVersionId | PlanVersionId | False | — |
| UX_Subscriptions_SourceOrder | SourceOrderId | True | [SourceOrderId] IS NOT NULL |
| IX_Subscriptions_Organization_CreatedAt | OrganizationId, CreatedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId | organization.Organizations | Id | Restrict |
| PlanVersionId | commerce.PlanVersions | Id | Restrict |
| SourceOrderId | commerce.Orders | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Subscriptions_Cancellation | `([Status] = 4 AND [CancelledAtUtc] IS NOT NULL) OR ([Status] <> 4 AND [CancelledAtUtc] IS NULL)` |
| CK_Subscriptions_Dates | `[EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]` |
| CK_Subscriptions_Status | `[Status] BETWEEN 1 AND 4` |

### commerce.UsageLedger

Entity: `MakanApp.Domain.Commerce.UsageLedgerEntry`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Amount | `bigint` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OccurredAtUtc | `datetime2(7)` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| QuotaKey | `int` | خیر | خیر | Never |
| SourceOperationId | `uniqueidentifier` | خیر | خیر | Never |
| SubscriptionId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_UsageLedger | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_UsageLedger_Subscription_Quota_SourceOperation | SubscriptionId, QuotaKey, SourceOperationId | True | — |
| IX_UsageLedger_Organization_Subscription_Quota_OccurredAt | OrganizationId, SubscriptionId, QuotaKey, OccurredAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId, SubscriptionId | commerce.Subscriptions | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_UsageLedger_Amount | `[Amount] > 0` |
| CK_UsageLedger_QuotaKey | `[QuotaKey] BETWEEN 1 AND 4` |

### guardian.GuardianRelations

Entity: `MakanApp.Domain.Guardian.GuardianRelation`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| GuardianUserId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LearnerOrganizationPersonId | `uniqueidentifier` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| ValidFromUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_GuardianRelations | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_GuardianRelations_GuardianUserId | GuardianUserId | False | — |
| IX_GuardianRelations_OrganizationId_LearnerOrganizationPersonId | OrganizationId, LearnerOrganizationPersonId | False | — |
| UX_GuardianRelations_Active_Organization_Guardian_Learner | OrganizationId, GuardianUserId, LearnerOrganizationPersonId | True | [Status] = 2 AND [EndedAtUtc] IS NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| GuardianUserId | identity.Users | Id | Restrict |
| OrganizationId | organization.Organizations | Id | Restrict |
| OrganizationId, LearnerOrganizationPersonId | organization.OrganizationPersons | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_GuardianRelations_Status | `[Status] IN (1, 2, 3, 4)` |

### identity.CommunicationAgeVerifications

Entity: `MakanApp.Domain.Identity.CommunicationAgeVerification`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ActorUserId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | بله | خیر | Never |
| PreviousCategory | `int` | خیر | خیر | Never |
| ProposedCategory | `int` | خیر | خیر | Never |
| Reason | `nvarchar(500)` | خیر | خیر | Never |
| Source | `int` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |
| VerifiedBirthDate | `date` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_CommunicationAgeVerifications | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_CommunicationAgeVerifications_ActorUserId | ActorUserId | False | — |
| IX_CommunicationAgeVerifications_OrganizationId | OrganizationId | False | — |
| IX_CommunicationAgeVerifications_UserId_CreatedAtUtc_Id | UserId, CreatedAtUtc, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ActorUserId | identity.Users | Id | Restrict |
| OrganizationId | organization.Organizations | Id | Restrict |
| UserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_CommunicationAgeVerifications_Categories | `[PreviousCategory] IN (0,1,2) AND [ProposedCategory] IN (1,2)` |
| CK_CommunicationAgeVerifications_Source | `([Source]=1 AND [OrganizationId] IS NOT NULL) OR ([Source] IN (2,3) AND [OrganizationId] IS NULL)` |
| CK_CommunicationAgeVerifications_Status | `[Status] IN (1,2)` |

### identity.OtpChallenges

Entity: `MakanApp.Domain.Identity.OtpChallenge`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CodeHash | `varbinary(32)` | خیر | خیر | Never |
| ConsumedAtUtc | `datetime2(7)` | بله | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| ExpiresAtUtc | `datetime2(7)` | خیر | خیر | Never |
| FailedAttempts | `int` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MaxFailedAttempts | `int` | خیر | خیر | Never |
| NormalizedPhoneNumber | `nvarchar(32)` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Salt | `varbinary(16)` | خیر | خیر | Never |
| SupersededAtUtc | `datetime2(7)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_OtpChallenges | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_OtpChallenges_Phone_CreatedAtUtc | NormalizedPhoneNumber, CreatedAtUtc | False | — |

### identity.Persons

Entity: `MakanApp.Domain.Identity.Person`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DateOfBirth | `date` | بله | خیر | Never |
| DisplayName | `nvarchar(200)` | بله | خیر | Never |
| FirstName | `nvarchar(100)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LastName | `nvarchar(100)` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| UpdatedAtUtc | `datetime2(7)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Persons | PK | Id |

### identity.RefreshTokens

Entity: `MakanApp.Domain.Identity.RefreshToken`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| ExpiresAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| ReplacedByTokenId | `uniqueidentifier` | بله | خیر | Never |
| RevokedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SessionId | `uniqueidentifier` | خیر | خیر | Never |
| TokenHash | `varbinary(32)` | خیر | خیر | Never |
| UsedAtUtc | `datetime2(7)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_RefreshTokens | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_RefreshTokens_ReplacedByTokenId | ReplacedByTokenId | False | — |
| UX_RefreshTokens_TokenHash | TokenHash | True | — |
| IX_RefreshTokens_Session_ExpiresAtUtc | SessionId, ExpiresAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ReplacedByTokenId | identity.RefreshTokens | Id | Restrict |
| SessionId | identity.UserSessions | Id | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_RefreshTokens_Expiry | `[ExpiresAtUtc] > [CreatedAtUtc]` |
| CK_RefreshTokens_Lifecycle | `[UsedAtUtc] IS NULL OR [ReplacedByTokenId] IS NOT NULL` |

### identity.UserCredentials

Entity: `MakanApp.Domain.Identity.UserCredential`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| Kind | `int` | خیر | خیر | Never |
| NormalizedIdentifier | `nvarchar(32)` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| UserId | `uniqueidentifier` | خیر | خیر | Never |
| VerifiedAtUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_UserCredentials | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_UserCredentials_UserId | UserId | False | — |
| UX_UserCredentials_Kind_Identifier | Kind, NormalizedIdentifier | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| UserId | identity.Users | Id | Cascade |

### identity.Users

Entity: `MakanApp.Domain.Identity.User`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AccountStatus | `int` | خیر | خیر | Never |
| CommunicationAgeCategory | `int` | خیر | خیر | Never |
| CommunicationAgeVerificationStatus | `int` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MessagingRestrictedUntilUtc | `datetime2(7)` | بله | خیر | Never |
| NormalizedUsername | `nvarchar(32)` | بله | خیر | Never |
| PersonId | `uniqueidentifier` | بله | خیر | Never |
| ProfileCompletedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ProfilePhotoFileAssetId | `uniqueidentifier` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SuspendedAtUtc | `datetime2(7)` | بله | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Username | `nvarchar(32)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Users | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_Users_NormalizedUsername | NormalizedUsername | True | [NormalizedUsername] IS NOT NULL |
| IX_Users_PersonId | PersonId | True | [PersonId] IS NOT NULL |
| IX_Users_ProfilePhotoFileAssetId | ProfilePhotoFileAssetId | True | [ProfilePhotoFileAssetId] IS NOT NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| PersonId | identity.Persons | Id | Restrict |
| ProfilePhotoFileAssetId | storage.FileAssets | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Users_AccountStatus | `[AccountStatus] IN (1, 2)` |
| CK_Users_AgeVerificationStatus | `[CommunicationAgeVerificationStatus] IN (0,1,2)` |
| CK_Users_CommunicationAgeCategory | `[CommunicationAgeCategory] IN (0, 1, 2)` |
| CK_Users_Suspension | `([AccountStatus] = 1 AND [SuspendedAtUtc] IS NULL) OR ([AccountStatus] = 2 AND [SuspendedAtUtc] IS NOT NULL)` |

### identity.UserSessions

Entity: `MakanApp.Domain.Identity.UserSession`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AbsoluteExpiresAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DeviceName | `nvarchar(100)` | بله | خیر | Never |
| ExpiresAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| RevokedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SelectedMembershipId | `uniqueidentifier` | بله | خیر | Never |
| SelectedRole | `nvarchar(16)` | بله | خیر | Never |
| SelectedSubjectOrganizationPersonId | `uniqueidentifier` | بله | خیر | Never |
| TokenHash | `varbinary(32)` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_UserSessions | PK | Id |
| AK_UserSessions_Id_UserId | Alternate Key | Id, UserId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_UserSessions_TokenHash | TokenHash | True | — |
| IX_UserSessions_User_Revoked_AbsoluteExpiry | UserId, RevokedAtUtc, AbsoluteExpiresAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| UserId | identity.Users | Id | Cascade |

### messaging.AbuseReports

Entity: `MakanApp.Domain.Messaging.AbuseReport`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ClientReportId | `uniqueidentifier` | خیر | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Description | `nvarchar(1000)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MessageId | `uniqueidentifier` | خیر | خیر | Never |
| Reason | `int` | خیر | خیر | Never |
| ReportedContentSnapshot | `nvarchar(4000)` | بله | خیر | Never |
| ReportedMessageKind | `int` | خیر | خیر | Never |
| ReportedMessageVersion | `binary(8)` | خیر | خیر | Never |
| ReportedUserId | `uniqueidentifier` | خیر | خیر | Never |
| ReporterUserId | `uniqueidentifier` | خیر | خیر | Never |
| RequestPayloadHash | `binary(32)` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_AbuseReports | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_AbuseReports_ConversationId | ConversationId | False | — |
| IX_AbuseReports_ReportedUserId | ReportedUserId | False | — |
| IX_AbuseReports_MessageId_ConversationId | MessageId, ConversationId | False | — |
| UX_AbuseReports_Reporter_ClientReportId | ReporterUserId, ClientReportId | True | — |
| IX_AbuseReports_Status_CreatedAtUtc | Status, CreatedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Restrict |
| ReportedUserId | identity.Users | Id | Restrict |
| ReporterUserId | identity.Users | Id | Restrict |
| MessageId, ConversationId | messaging.Messages | Id, ConversationId | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_AbuseReports_MessageKind | `[ReportedMessageKind] IN (1, 2, 3, 4, 5, 6, 7, 8)` |
| CK_AbuseReports_Reason | `[Reason] IN (1, 2, 3, 4, 5, 6, 7)` |
| CK_AbuseReports_Status | `[Status] IN (1, 2, 3, 4)` |

### messaging.ChangeEvents

Entity: `MakanApp.Domain.Messaging.MessagingChangeEvent`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ActorUserId | `uniqueidentifier` | بله | خیر | Never |
| AudienceUserId | `uniqueidentifier` | بله | خیر | Never |
| ChangeSequence | `bigint` | خیر | خیر | Never |
| ChangeType | `int` | خیر | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OccurredAtUtc | `datetime2(7)` | خیر | خیر | Never |
| PayloadVersion | `int` | خیر | خیر | Never |
| ResourceId | `uniqueidentifier` | خیر | خیر | Never |
| ResourceVersion | `nvarchar(128)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ChangeEvents | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ChangeEvents_ActorUserId | ActorUserId | False | — |
| IX_ChangeEvents_AudienceUserId | AudienceUserId | False | — |
| UX_ChangeEvents_Conversation_Sequence | ConversationId, ChangeSequence | True | — |
| IX_ChangeEvents_Conversation_Audience_Sequence | ConversationId, AudienceUserId, ChangeSequence | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ActorUserId | identity.Users | Id | Restrict |
| AudienceUserId | identity.Users | Id | Restrict |
| ConversationId | messaging.Conversations | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ChangeEvents_PayloadVersion | `[PayloadVersion] = 1` |
| CK_ChangeEvents_Sequence | `[ChangeSequence] > 0` |
| CK_ChangeEvents_Type | `[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15)` |

### messaging.ChatFolders

Entity: `MakanApp.Domain.Messaging.ChatFolder`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ExcludedTypes | `int` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| IncludedTypes | `int` | خیر | خیر | Never |
| Name | `nvarchar(80)` | خیر | خیر | Never |
| OwnerUserId | `uniqueidentifier` | خیر | خیر | Never |
| Revision | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SortOrder | `int` | خیر | خیر | Never |
| UnreadOnly | `bit` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ChatFolders | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ChatFolders_OwnerUserId_SortOrder_Id | OwnerUserId, SortOrder, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OwnerUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ChatFolders_Name | `LEN(LTRIM(RTRIM([Name]))) > 0` |
| CK_ChatFolders_Revision | `[Revision] > 0` |
| CK_ChatFolders_SortOrder | `[SortOrder] BETWEEN 0 AND 10000` |
| CK_ChatFolders_Types | `([IncludedTypes] & ~15) = 0 AND ([ExcludedTypes] & ~15) = 0` |

### messaging.ConversationChecklistItems

Entity: `MakanApp.Domain.Messaging.ConversationChecklistItem`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ChecklistId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtRevision | `bigint` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | Never |
| IsChecked | `bit` | خیر | خیر | Never |
| Mode | `int` | خیر | خیر | Never |
| Position | `int` | خیر | خیر | Never |
| RetiredAtRevision | `bigint` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| StateRevision | `bigint` | خیر | خیر | Never |
| Text | `nvarchar(150)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationChecklistItems | PK | ChecklistId, Id |
| AK_ConversationChecklistItems_ChecklistId_Id_Mode | Alternate Key | ChecklistId, Id, Mode |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationChecklistItems_ChecklistId_Mode | ChecklistId, Mode | False | — |
| IX_ConversationChecklistItems_ChecklistId_RetiredAtRevision_Position | ChecklistId, RetiredAtRevision, Position | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ChecklistId, Mode | messaging.ConversationChecklists | Id, Mode | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationChecklistItems_Definition | `LEN(LTRIM(RTRIM([Text])))>0 AND [Position] BETWEEN 1 AND 20 AND [CreatedAtRevision]>0 AND ([RetiredAtRevision] IS NULL OR [RetiredAtRevision]>[CreatedAtRevision])` |
| CK_ConversationChecklistItems_State | `[StateRevision]>0 AND ([Mode]=2 OR ([Mode]=1 AND [IsChecked]=0 AND [StateRevision]=1))` |

### messaging.ConversationChecklistPersonalStates

Entity: `MakanApp.Domain.Messaging.ConversationChecklistPersonalState`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ChecklistId | `uniqueidentifier` | خیر | خیر | Never |
| IsChecked | `bit` | خیر | خیر | Never |
| ItemId | `uniqueidentifier` | خیر | خیر | Never |
| Mode | `int` | خیر | خیر | Never |
| Revision | `bigint` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationChecklistPersonalStates | PK | ChecklistId, ItemId, UserId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationChecklistPersonalStates_UserId | UserId | False | — |
| IX_ConversationChecklistPersonalStates_ChecklistId_ItemId_Mode | ChecklistId, ItemId, Mode | False | — |
| IX_ConversationChecklistPersonalStates_ChecklistId_UserId_ItemId | ChecklistId, UserId, ItemId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| UserId | identity.Users | Id | Restrict |
| ChecklistId, ItemId, Mode | messaging.ConversationChecklistItems | ChecklistId, Id, Mode | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationChecklistPersonalStates_Mode | `[Mode]=1 AND [Revision]>0` |

### messaging.ConversationChecklists

Entity: `MakanApp.Domain.Messaging.ConversationChecklist`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreationHash | `binary(32)` | خیر | خیر | Never |
| CreatorUserId | `uniqueidentifier` | خیر | خیر | Never |
| DefinitionRevision | `bigint` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | Never |
| Mode | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationChecklists | PK | Id |
| AK_ConversationChecklists_Id_Mode | Alternate Key | Id, Mode |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationChecklists_CreatorUserId | CreatorUserId | False | — |
| IX_ConversationChecklists_Id_ConversationId | Id, ConversationId | False | — |
| IX_ConversationChecklists_ConversationId_CreatedAtUtc_Id | ConversationId, CreatedAtUtc, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| CreatorUserId | identity.Users | Id | Restrict |
| Id, ConversationId | messaging.Messages | Id, ConversationId | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationChecklists_Definition | `[Mode] IN (1,2) AND [DefinitionRevision]>0 AND DATALENGTH([CreationHash])=32 AND [UpdatedAtUtc]>=[CreatedAtUtc]` |

### messaging.ConversationInvitations

Entity: `MakanApp.Domain.Messaging.ConversationInvitation`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CompletedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| ExpiresAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| InviterUserId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| TargetUserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationInvitations | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationInvitations_InviterUserId | InviterUserId | False | — |
| IX_ConversationInvitations_ConversationId_TargetUserId | ConversationId, TargetUserId | True | [Status]=1 |
| IX_ConversationInvitations_TargetUserId_Status_CreatedAtUtc | TargetUserId, Status, CreatedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Restrict |
| InviterUserId | identity.Users | Id | Restrict |
| TargetUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationInvitations_Pair | `[InviterUserId] <> [TargetUserId]` |
| CK_ConversationInvitations_Status | `[Status] IN (1,2,3,4,5)` |
| CK_ConversationInvitations_Times | `[ExpiresAtUtc] > [CreatedAtUtc] AND (([Status]=1 AND [CompletedAtUtc] IS NULL) OR ([Status]<>1 AND [CompletedAtUtc] IS NOT NULL))` |

### messaging.ConversationInviteLinks

Entity: `MakanApp.Domain.Messaging.ConversationInviteLink`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedByUserId | `uniqueidentifier` | خیر | خیر | Never |
| CreatorMembershipId | `uniqueidentifier` | بله | خیر | Never |
| CreatorRole | `int` | بله | خیر | Never |
| ExpiresAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MaximumUses | `int` | خیر | خیر | Never |
| RequiresApproval | `bit` | خیر | خیر | Never |
| RevokedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| TokenHash | `char(64)` | خیر | خیر | Never |
| UsedCount | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationInviteLinks | PK | Id |
| AK_ConversationInviteLinks_Id_ConversationId | Alternate Key | Id, ConversationId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationInviteLinks_CreatedByUserId | CreatedByUserId | False | — |
| IX_ConversationInviteLinks_CreatorMembershipId | CreatorMembershipId | False | — |
| IX_ConversationInviteLinks_TokenHash | TokenHash | True | — |
| IX_ConversationInviteLinks_ConversationId_CreatedAtUtc_Id | ConversationId, CreatedAtUtc, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Restrict |
| CreatedByUserId | identity.Users | Id | Restrict |
| CreatorMembershipId | organization.Memberships | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationInviteLinks_CreatorContext | `([CreatorMembershipId] IS NULL AND [CreatorRole] IS NULL) OR ([CreatorMembershipId] IS NOT NULL AND [CreatorRole] IS NOT NULL AND [CreatorRole] IN (1,2,3,4))` |
| CK_ConversationInviteLinks_Expiry | `[ExpiresAtUtc] > [CreatedAtUtc]` |
| CK_ConversationInviteLinks_Hash | `LEN([TokenHash]) = 64` |
| CK_ConversationInviteLinks_Uses | `[MaximumUses] BETWEEN 1 AND 10000 AND [UsedCount] BETWEEN 0 AND [MaximumUses]` |

### messaging.ConversationJoinRequests

Entity: `MakanApp.Domain.Messaging.ConversationJoinRequest`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CompletedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| ExpiresAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| InviteLinkId | `uniqueidentifier` | خیر | خیر | Never |
| RequesterMembershipId | `uniqueidentifier` | بله | خیر | Never |
| RequesterRole | `int` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationJoinRequests | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationJoinRequests_RequesterMembershipId | RequesterMembershipId | False | — |
| IX_ConversationJoinRequests_ConversationId_UserId | ConversationId, UserId | True | [Status] = 1 |
| IX_ConversationJoinRequests_InviteLinkId_ConversationId | InviteLinkId, ConversationId | False | — |
| IX_ConversationJoinRequests_ConversationId_CreatedAtUtc_Id | ConversationId, CreatedAtUtc, Id | False | — |
| IX_ConversationJoinRequests_UserId_CreatedAtUtc_Id | UserId, CreatedAtUtc, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Restrict |
| RequesterMembershipId | organization.Memberships | Id | Restrict |
| UserId | identity.Users | Id | Restrict |
| InviteLinkId, ConversationId | messaging.ConversationInviteLinks | Id, ConversationId | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationJoinRequests_Expiry | `[ExpiresAtUtc] > [CreatedAtUtc]` |
| CK_ConversationJoinRequests_Lifecycle | `([Status] = 1 AND [CompletedAtUtc] IS NULL) OR ([Status] <> 1 AND [CompletedAtUtc] IS NOT NULL)` |
| CK_ConversationJoinRequests_RequesterContext | `([RequesterMembershipId] IS NULL AND [RequesterRole] IS NULL) OR ([RequesterMembershipId] IS NOT NULL AND [RequesterRole] IS NOT NULL AND [RequesterRole] IN (1,2,3,4))` |
| CK_ConversationJoinRequests_Status | `[Status] IN (1,2,3,4,5)` |

### messaging.ConversationMemberRestrictions

Entity: `MakanApp.Domain.Messaging.ConversationMemberRestriction`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| DeniedCapabilities | `int` | خیر | خیر | Never |
| IsBanned | `bit` | خیر | خیر | Never |
| Revision | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| UntilUtc | `datetime2(7)` | بله | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationMemberRestrictions | PK | ConversationId, UserId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationMemberRestrictions_UserId | UserId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Restrict |
| UserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationMemberRestrictions_Capabilities | `[DeniedCapabilities] BETWEEN 0 AND 7` |
| CK_ConversationMemberRestrictions_Clear | `[IsBanned] = 1 OR [DeniedCapabilities] <> 0 OR [UntilUtc] IS NULL` |
| CK_ConversationMemberRestrictions_Revision | `[Revision] > 0` |

### messaging.ConversationOwnershipTransfers

Entity: `MakanApp.Domain.Messaging.ConversationOwnershipTransfer`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AcceptedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DeclinedAtUtc | `datetime2(7)` | بله | خیر | Never |
| FromParticipantId | `uniqueidentifier` | خیر | خیر | Never |
| FromUserId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| ToParticipantId | `uniqueidentifier` | خیر | خیر | Never |
| ToUserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationOwnershipTransfers | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_ConversationOwnershipTransfers_Pending | ConversationId | True | [Status] = 1 |
| IX_ConversationOwnershipTransfers_FromUserId | FromUserId | False | — |
| IX_ConversationOwnershipTransfers_ToUserId | ToUserId | False | — |
| IX_ConversationOwnershipTransfers_FromParticipantId_ConversationId_FromUserId | FromParticipantId, ConversationId, FromUserId | False | — |
| IX_ConversationOwnershipTransfers_ToParticipantId_ConversationId_ToUserId | ToParticipantId, ConversationId, ToUserId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Restrict |
| FromUserId | identity.Users | Id | Restrict |
| ToUserId | identity.Users | Id | Restrict |
| FromParticipantId, ConversationId, FromUserId | messaging.ConversationParticipants | Id, ConversationId, UserId | Restrict |
| ToParticipantId, ConversationId, ToUserId | messaging.ConversationParticipants | Id, ConversationId, UserId | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationOwnershipTransfers_Status | `[Status] IN (1, 2, 3, 4, 5)` |

### messaging.ConversationParticipants

Entity: `MakanApp.Domain.Messaging.ConversationParticipant`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AdminPermissions | `int` | خیر | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CursorUpdatedAtUtc | `datetime2(7)` | بله | خیر | Never |
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| EndedByUserId | `uniqueidentifier` | بله | خیر | Never |
| HistoryVisibleFromSequence | `bigint` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| JoinedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| LastDeliveredMessageSequence | `bigint` | خیر | خیر | Never |
| LastReadMessageSequence | `bigint` | خیر | خیر | Never |
| Role | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationParticipants | PK | Id |
| UQ_ConversationParticipants_Id_Conversation_User | Alternate Key | Id, ConversationId, UserId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_ConversationParticipants_ActiveOwner | ConversationId | True | [Role] = 1 AND [Status] = 1 AND [EndedAtUtc] IS NULL |
| IX_ConversationParticipants_EndedByUserId | EndedByUserId | False | — |
| UX_ConversationParticipants_Active_Conversation_User | ConversationId, UserId | True | [Status] = 1 AND [EndedAtUtc] IS NULL |
| IX_ConversationParticipants_UserId_Status | UserId, Status | False | — |
| IX_ConversationParticipants_ConversationId_UserId_HistoryVisibleFromSequence | ConversationId, UserId, HistoryVisibleFromSequence | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Restrict |
| EndedByUserId | identity.Users | Id | Restrict |
| UserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationParticipants_AdminPermissions | `[AdminPermissions] BETWEEN 0 AND 8191 AND (([Role] = 2) OR [AdminPermissions] = 0)` |
| CK_ConversationParticipants_CursorTimestamp | `([LastDeliveredMessageSequence] = 0 AND [LastReadMessageSequence] = 0 AND [CursorUpdatedAtUtc] IS NULL) OR ([LastDeliveredMessageSequence] > 0 AND [CursorUpdatedAtUtc] IS NOT NULL)` |
| CK_ConversationParticipants_Cursors | `[LastDeliveredMessageSequence] >= 0 AND [LastReadMessageSequence] >= 0 AND [LastReadMessageSequence] <= [LastDeliveredMessageSequence]` |
| CK_ConversationParticipants_HistoryBoundary | `[HistoryVisibleFromSequence] > 0` |
| CK_ConversationParticipants_Lifecycle | `([Status] = 1 AND [EndedAtUtc] IS NULL AND [EndedByUserId] IS NULL) OR ([Status] IN (2, 3) AND [EndedAtUtc] IS NOT NULL AND [EndedByUserId] IS NOT NULL)` |
| CK_ConversationParticipants_Role | `[Role] IN (1, 2, 3)` |
| CK_ConversationParticipants_Status | `[Status] IN (1, 2, 3)` |

### messaging.ConversationPins

Entity: `MakanApp.Domain.Messaging.ConversationPin`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MessageId | `uniqueidentifier` | خیر | خیر | Never |
| PinnedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| PinnedByUserId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| UnpinnedAtUtc | `datetime2(7)` | بله | خیر | Never |
| UnpinnedByUserId | `uniqueidentifier` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationPins | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationPins_PinnedByUserId | PinnedByUserId | False | — |
| IX_ConversationPins_UnpinnedByUserId | UnpinnedByUserId | False | — |
| UX_ConversationPins_Active_Conversation_Message | ConversationId, MessageId | True | [UnpinnedAtUtc] IS NULL |
| IX_ConversationPins_MessageId_ConversationId | MessageId, ConversationId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| PinnedByUserId | identity.Users | Id | Restrict |
| UnpinnedByUserId | identity.Users | Id | Restrict |
| MessageId, ConversationId | messaging.Messages | Id, ConversationId | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationPins_Lifecycle | `([UnpinnedAtUtc] IS NULL AND [UnpinnedByUserId] IS NULL) OR ([UnpinnedAtUtc] IS NOT NULL AND [UnpinnedByUserId] IS NOT NULL AND [UnpinnedAtUtc] >= [PinnedAtUtc])` |

### messaging.ConversationPollOptions

Entity: `MakanApp.Domain.Messaging.ConversationPollOption`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Number | `int` | خیر | خیر | Never |
| PollId | `uniqueidentifier` | خیر | خیر | Never |
| Text | `nvarchar(150)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationPollOptions | PK | PollId, Number |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| PollId | messaging.ConversationPolls | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationPollOptions_Value | `[Number] BETWEEN 1 AND 10 AND LEN(LTRIM(RTRIM([Text])))>0` |

### messaging.ConversationPolls

Entity: `MakanApp.Domain.Messaging.ConversationPoll`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AllowsMultipleAnswers | `bit` | خیر | خیر | Never |
| ClosedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ClosedByUserId | `uniqueidentifier` | بله | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatorUserId | `uniqueidentifier` | خیر | خیر | Never |
| DefinitionHash | `binary(32)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | Never |
| IsAnonymous | `bit` | خیر | خیر | Never |
| IsQuiz | `bit` | خیر | خیر | Never |
| OptionCount | `int` | خیر | خیر | Never |
| Revision | `bigint` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationPolls | PK | Id |
| AK_ConversationPolls_Id_IsQuiz | Alternate Key | Id, IsQuiz |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationPolls_ClosedByUserId | ClosedByUserId | False | — |
| IX_ConversationPolls_CreatorUserId | CreatorUserId | False | — |
| IX_ConversationPolls_Id_ConversationId | Id, ConversationId | False | — |
| IX_ConversationPolls_ConversationId_CreatedAtUtc_Id | ConversationId, CreatedAtUtc, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ClosedByUserId | identity.Users | Id | Restrict |
| CreatorUserId | identity.Users | Id | Restrict |
| Id, ConversationId | messaging.Messages | Id, ConversationId | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationPolls_Definition | `[OptionCount] BETWEEN 2 AND 10 AND DATALENGTH([DefinitionHash])=32 AND ([IsQuiz]=0 OR [AllowsMultipleAnswers]=0)` |
| CK_ConversationPolls_State | `[Revision]>0 AND [UpdatedAtUtc]>=[CreatedAtUtc] AND (([Status]=1 AND [ClosedAtUtc] IS NULL AND [ClosedByUserId] IS NULL) OR ([Status]=2 AND [ClosedAtUtc] IS NOT NULL AND [ClosedAtUtc]>=[CreatedAtUtc] AND [ClosedByUserId] IS NOT NULL))` |

### messaging.ConversationPollSelections

Entity: `MakanApp.Domain.Messaging.ConversationPollSelection`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| OptionNumber | `int` | خیر | خیر | Never |
| PollId | `uniqueidentifier` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationPollSelections | PK | PollId, UserId, OptionNumber |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationPollSelections_PollId_OptionNumber_UserId | PollId, OptionNumber, UserId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| PollId, OptionNumber | messaging.ConversationPollOptions | PollId, Number | Restrict |
| PollId, UserId | messaging.ConversationPollVotes | PollId, UserId | Restrict |

### messaging.ConversationPollVotes

Entity: `MakanApp.Domain.Messaging.ConversationPollVote`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| IsQuiz | `bit` | خیر | خیر | Never |
| PollId | `uniqueidentifier` | خیر | خیر | Never |
| Revision | `bigint` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationPollVotes | PK | PollId, UserId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationPollVotes_UserId | UserId | False | — |
| IX_ConversationPollVotes_PollId_IsQuiz | PollId, IsQuiz | False | — |
| IX_ConversationPollVotes_PollId_CreatedAtUtc_UserId | PollId, CreatedAtUtc, UserId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| UserId | identity.Users | Id | Restrict |
| PollId, IsQuiz | messaging.ConversationPolls | Id, IsQuiz | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationPollVotes_State | `[Revision]>0 AND ([IsQuiz]=0 OR [Revision]=1) AND [UpdatedAtUtc]>=[CreatedAtUtc]` |

### messaging.ConversationQuizAnswers

Entity: `MakanApp.Domain.Messaging.ConversationQuizAnswer`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CorrectOptionNumber | `int` | خیر | خیر | Never |
| Explanation | `nvarchar(1000)` | بله | خیر | Never |
| IsQuiz | `bit` | خیر | خیر | Never |
| PollId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationQuizAnswers | PK | PollId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationQuizAnswers_PollId_CorrectOptionNumber | PollId, CorrectOptionNumber | False | — |
| IX_ConversationQuizAnswers_PollId_IsQuiz | PollId, IsQuiz | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| PollId, CorrectOptionNumber | messaging.ConversationPollOptions | PollId, Number | Restrict |
| PollId, IsQuiz | messaging.ConversationPolls | Id, IsQuiz | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationQuizAnswers_IsQuiz | `[IsQuiz]=1` |

### messaging.Conversations

Entity: `MakanApp.Domain.Messaging.Conversation`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AcademicClassId | `uniqueidentifier` | بله | خیر | Never |
| ArchivedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ClientOperationId | `uniqueidentifier` | بله | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedByUserId | `uniqueidentifier` | بله | خیر | Never |
| CreationPayloadHash | `binary(32)` | بله | خیر | Never |
| Description | `nvarchar(1000)` | بله | خیر | Never |
| DirectUserHighId | `uniqueidentifier` | بله | خیر | Never |
| DirectUserLowId | `uniqueidentifier` | بله | خیر | Never |
| GroupHistoryPolicy | `int` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| ManagementPolicy | `int` | خیر | خیر | Never |
| MemberCapabilities | `int` | خیر | خیر | Never |
| NextChangeSequence | `bigint` | خیر | خیر | Never |
| NextMessageSequence | `bigint` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SavedOwnerUserId | `uniqueidentifier` | بله | خیر | Never |
| Scope | `int` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| Title | `nvarchar(160)` | بله | خیر | Never |
| Type | `int` | خیر | خیر | Never |
| Visibility | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Conversations | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Conversations_DirectUserHighId | DirectUserHighId | False | — |
| IX_Conversations_DirectUserLowId | DirectUserLowId | False | — |
| IX_Conversations_OrganizationId | OrganizationId | False | — |
| UX_Conversations_Saved_Owner | SavedOwnerUserId | True | [Type] = 4 |
| UX_Conversations_Creator_ClientOperationId | CreatedByUserId, ClientOperationId | True | [ManagementPolicy] = 1 |
| IX_Conversations_OrganizationId_AcademicClassId | OrganizationId, AcademicClassId | True | [AcademicClassId] IS NOT NULL |
| UX_Conversations_Direct_Scope_Organization_Pair | Scope, OrganizationId, DirectUserLowId, DirectUserHighId | True | [Type] = 1 |
| IX_Conversations_PublicDiscovery | Scope, OrganizationId, Visibility, Type, Status, CreatedAtUtc, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| CreatedByUserId | identity.Users | Id | Restrict |
| DirectUserHighId | identity.Users | Id | Restrict |
| DirectUserLowId | identity.Users | Id | Restrict |
| OrganizationId | organization.Organizations | Id | Restrict |
| SavedOwnerUserId | identity.Users | Id | Restrict |
| OrganizationId, AcademicClassId | academic.Classes | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Conversations_AcademicClass | `[AcademicClassId] IS NULL OR ([ManagementPolicy]=2 AND [Type]=2 AND [Scope]=2 AND [OrganizationId] IS NOT NULL)` |
| CK_Conversations_BehaviorShape | `([Type] IN (1, 4) AND [Visibility] = 1 AND [GroupHistoryPolicy] = 1 AND [MemberCapabilities] = 0) OR ([Type] = 2) OR ([Type] = 3 AND [GroupHistoryPolicy] = 1 AND [MemberCapabilities] = 0)` |
| CK_Conversations_DirectPair | `[Type] <> 1 OR ([DirectUserLowId] IS NOT NULL AND [DirectUserHighId] IS NOT NULL AND [DirectUserLowId] <> [DirectUserHighId])` |
| CK_Conversations_GroupHistoryPolicy | `[GroupHistoryPolicy] IN (1, 2)` |
| CK_Conversations_ManagedShape | `([Type] IN (1, 4) AND [ManagementPolicy] = 0 AND [Title] IS NULL) OR ([Type] IN (2, 3) AND [ManagementPolicy] IN (1, 2) AND LEN(LTRIM(RTRIM([Title]))) > 0 AND [DirectUserLowId] IS NULL AND [DirectUserHighId] IS NULL)` |
| CK_Conversations_ManagementPolicy | `[ManagementPolicy] IN (0, 1, 2)` |
| CK_Conversations_MemberCapabilities | `[MemberCapabilities] BETWEEN 0 AND 7` |
| CK_Conversations_NextChangeSequence | `[NextChangeSequence] > 0` |
| CK_Conversations_NextMessageSequence | `[NextMessageSequence] > 0` |
| CK_Conversations_SavedShape | `([Type] = 4 AND [Scope] = 1 AND [OrganizationId] IS NULL AND [SavedOwnerUserId] IS NOT NULL AND [DirectUserLowId] IS NULL AND [DirectUserHighId] IS NULL AND [ManagementPolicy] = 0 AND [Title] IS NULL) OR ([Type] <> 4 AND [SavedOwnerUserId] IS NULL)` |
| CK_Conversations_Scope | `[Scope] IN (1, 2)` |
| CK_Conversations_ScopeOrganization | `([Scope] = 1 AND [OrganizationId] IS NULL) OR ([Scope] = 2 AND [OrganizationId] IS NOT NULL)` |
| CK_Conversations_Status | `[Status] IN (1, 2)` |
| CK_Conversations_SystemManagedAcademic | `([ManagementPolicy] <> 2) OR ([Scope] = 2 AND [CreatedByUserId] IS NULL AND [ClientOperationId] IS NULL AND [CreationPayloadHash] IS NULL)` |
| CK_Conversations_SystemManagedVisibility | `[ManagementPolicy] <> 2 OR [Visibility] = 1` |
| CK_Conversations_Type | `[Type] IN (1, 2, 3, 4)` |
| CK_Conversations_UserManagedCreation | `([ManagementPolicy] <> 1) OR ([CreatedByUserId] IS NOT NULL AND [ClientOperationId] IS NOT NULL AND [CreationPayloadHash] IS NOT NULL)` |
| CK_Conversations_Visibility | `[Visibility] IN (1, 2)` |

### messaging.ConversationTopics

Entity: `MakanApp.Domain.Messaging.ConversationTopic`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedByUserId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| Revision | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| Title | `nvarchar(160)` | خیر | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationTopics | PK | Id |
| AK_ConversationTopics_Id_ConversationId | Alternate Key | Id, ConversationId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationTopics_CreatedByUserId | CreatedByUserId | False | — |
| IX_ConversationTopics_ConversationId_CreatedAtUtc_Id | ConversationId, CreatedAtUtc, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Restrict |
| CreatedByUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationTopics_Revision | `[Revision] > 0` |
| CK_ConversationTopics_Status | `[Status] IN (1,2)` |
| CK_ConversationTopics_Title | `LEN(LTRIM(RTRIM([Title]))) > 0` |

### messaging.ConversationUserStates

Entity: `MakanApp.Domain.Messaging.ConversationUserState`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ArchivedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| HistoryHiddenThroughSequence | `bigint` | خیر | خیر | Never |
| IsManuallyUnread | `bit` | خیر | خیر | Never |
| MutedUntilUtc | `datetime2(7)` | بله | خیر | Never |
| MuteMode | `int` | خیر | خیر | Never |
| PinnedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ConversationUserStates | PK | ConversationId, UserId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ConversationUserStates_User_List | UserId, ArchivedAtUtc, PinnedAtUtc, UpdatedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Cascade |
| UserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ConversationUserStates_HistoryHiddenThroughSequence | `[HistoryHiddenThroughSequence] >= 0` |
| CK_ConversationUserStates_MuteMode | `[MuteMode] IN (0, 1, 2)` |
| CK_ConversationUserStates_MuteShape | `([MuteMode] IN (0, 1) AND [MutedUntilUtc] IS NULL) OR ([MuteMode] = 2 AND [MutedUntilUtc] IS NOT NULL)` |

### messaging.FolderConversationOverrides

Entity: `MakanApp.Domain.Messaging.FolderConversationOverride`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| FolderId | `uniqueidentifier` | خیر | خیر | Never |
| Included | `bit` | خیر | خیر | Never |
| PinOrder | `int` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_FolderConversationOverrides | PK | FolderId, ConversationId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_FolderConversationOverrides_ConversationId | ConversationId | False | — |
| IX_FolderConversationOverrides_FolderId_PinOrder | FolderId, PinOrder | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Restrict |
| FolderId | messaging.ChatFolders | Id | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_FolderConversationOverrides_Pin | `[PinOrder] IS NULL OR ([Included] = 1 AND [PinOrder] BETWEEN 0 AND 199)` |

### messaging.MessageAttachments

Entity: `MakanApp.Domain.Messaging.MessageAttachment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| FileAssetId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| Kind | `int` | خیر | خیر | Never |
| MessageId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_MessageAttachments | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_MessageAttachments_FileAssetId | FileAssetId | False | — |
| UX_MessageAttachments_Message_File | MessageId, FileAssetId | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| FileAssetId | storage.FileAssets | Id | Restrict |
| MessageId | messaging.Messages | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_MessageAttachments_Kind | `[Kind] IN (2, 3, 4, 5)` |

### messaging.MessageMentions

Entity: `MakanApp.Domain.Messaging.MessageMention`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MentionedUserId | `uniqueidentifier` | خیر | خیر | Never |
| MessageId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_MessageMentions | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_MessageMentions_MentionedUserId | MentionedUserId | False | — |
| UX_MessageMentions_Message_User | MessageId, MentionedUserId | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| MentionedUserId | identity.Users | Id | Restrict |
| MessageId | messaging.Messages | Id | Restrict |

### messaging.MessageReactions

Entity: `MakanApp.Domain.Messaging.MessageReaction`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MessageId | `uniqueidentifier` | خیر | خیر | Never |
| ReactionType | `int` | خیر | خیر | Never |
| RemovedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_MessageReactions | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_MessageReactions_UserId | UserId | False | — |
| UX_MessageReactions_Active_Message_User | MessageId, UserId | True | [RemovedAtUtc] IS NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| MessageId | messaging.Messages | Id | Restrict |
| UserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_MessageReactions_Lifecycle | `[RemovedAtUtc] IS NULL OR [RemovedAtUtc] >= [CreatedAtUtc]` |
| CK_MessageReactions_Type | `[ReactionType] IN (1, 2, 3, 4, 5)` |

### messaging.MessageRevisions

Entity: `MakanApp.Domain.Messaging.MessageRevision`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AuthoredByUserId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MessageId | `uniqueidentifier` | خیر | خیر | Never |
| RevisionNumber | `int` | خیر | خیر | Never |
| Text | `nvarchar(4000)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_MessageRevisions | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_MessageRevisions_AuthoredByUserId | AuthoredByUserId | False | — |
| UX_MessageRevisions_Message_Number | MessageId, RevisionNumber | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| AuthoredByUserId | identity.Users | Id | Restrict |
| MessageId | messaging.Messages | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_MessageRevisions_Number | `[RevisionNumber] > 0` |

### messaging.Messages

Entity: `MakanApp.Domain.Messaging.Message`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ClientMessageId | `uniqueidentifier` | خیر | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CurrentRevisionNumber | `int` | خیر | خیر | Never |
| DeletedAtUtc | `datetime2(7)` | بله | خیر | Never |
| DeletedByUserId | `uniqueidentifier` | بله | خیر | Never |
| EditedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ForwardedFromMessageId | `uniqueidentifier` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| IsSilent | `bit` | خیر | خیر | Never |
| Kind | `int` | خیر | خیر | Never |
| ReplyToMessageId | `uniqueidentifier` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SearchText | `nvarchar(4000)` | بله | خیر | Never |
| SenderParticipantId | `uniqueidentifier` | خیر | خیر | Never |
| SenderUserId | `uniqueidentifier` | خیر | خیر | Never |
| SentAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Sequence | `bigint` | خیر | خیر | Never |
| SharedContactDisplayName | `nvarchar(200)` | بله | خیر | Never |
| SharedContactUserId | `uniqueidentifier` | بله | خیر | Never |
| SharedContactUsername | `nvarchar(32)` | بله | خیر | Never |
| Text | `nvarchar(4000)` | بله | خیر | Never |
| TopicId | `uniqueidentifier` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Messages | PK | Id |
| UQ_Messages_Id_Conversation | Alternate Key | Id, ConversationId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Messages_DeletedByUserId | DeletedByUserId | False | — |
| IX_Messages_ForwardedFromMessageId | ForwardedFromMessageId | False | — |
| IX_Messages_ReplyToMessageId | ReplyToMessageId | False | — |
| IX_Messages_SharedContactUserId | SharedContactUserId | False | — |
| UX_Messages_Conversation_Sequence | ConversationId, Sequence | True | — |
| IX_Messages_ReplyToMessageId_ConversationId | ReplyToMessageId, ConversationId | False | — |
| IX_Messages_TopicId_ConversationId | TopicId, ConversationId | False | — |
| UX_Messages_Conversation_Sender_ClientMessageId | ConversationId, SenderUserId, ClientMessageId | True | — |
| IX_Messages_Search_Access | ConversationId, SentAtUtc, Id | False | — |
| IX_Messages_ConversationId_TopicId_Sequence | ConversationId, TopicId, Sequence | False | — |
| IX_Messages_SenderParticipantId_ConversationId_SenderUserId | SenderParticipantId, ConversationId, SenderUserId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ConversationId | messaging.Conversations | Id | Restrict |
| DeletedByUserId | identity.Users | Id | Restrict |
| ForwardedFromMessageId | messaging.Messages | Id | Restrict |
| SharedContactUserId | identity.Users | Id | Restrict |
| ReplyToMessageId, ConversationId | messaging.Messages | Id, ConversationId | Restrict |
| TopicId, ConversationId | messaging.ConversationTopics | Id, ConversationId | Restrict |
| SenderParticipantId, ConversationId, SenderUserId | messaging.ConversationParticipants | Id, ConversationId, UserId | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Messages_ContactCardShape | `([Kind] = 6 AND [SharedContactUserId] IS NOT NULL AND LEN(LTRIM(RTRIM([SharedContactUsername]))) > 0 AND LEN(LTRIM(RTRIM([SharedContactDisplayName]))) > 0) OR ([Kind] <> 6 AND [SharedContactUserId] IS NULL AND [SharedContactUsername] IS NULL AND [SharedContactDisplayName] IS NULL)` |
| CK_Messages_Content | `([DeletedAtUtc] IS NOT NULL AND [DeletedByUserId] IS NOT NULL AND [Text] IS NULL) OR ([DeletedAtUtc] IS NULL AND [DeletedByUserId] IS NULL AND (([Kind] IN (1, 7, 8) AND [Text] IS NOT NULL AND LEN(LTRIM(RTRIM([Text]))) > 0) OR ([Kind] IN (2, 3, 4, 5) AND ([Text] IS NULL OR LEN(LTRIM(RTRIM([Text]))) > 0)) OR ([Kind] = 6 AND [Text] IS NULL)))` |
| CK_Messages_CurrentRevision | `[CurrentRevisionNumber] > 0` |
| CK_Messages_Kind | `[Kind] IN (1, 2, 3, 4, 5, 6, 7, 8)` |
| CK_Messages_Sequence | `[Sequence] > 0` |

### messaging.MessageUserVisibilities

Entity: `MakanApp.Domain.Messaging.MessageUserVisibility`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| HiddenAtUtc | `datetime2(7)` | خیر | خیر | Never |
| MessageId | `uniqueidentifier` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_MessageUserVisibilities | PK | MessageId, UserId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_MessageUserVisibilities_User_Message | UserId, MessageId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| MessageId | messaging.Messages | Id | Cascade |
| UserId | identity.Users | Id | Restrict |

### messaging.MessagingPrivacySettings

Entity: `MakanApp.Domain.Messaging.MessagingPrivacySettings`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DirectMessagesAudience | `int` | خیر | خیر | Never |
| GroupInvitesAudience | `int` | خیر | خیر | Never |
| LastSeenAudience | `int` | خیر | خیر | OnAdd |
| OnlineAudience | `int` | خیر | خیر | OnAdd |
| PhoneNumberDiscoveryAudience | `int` | خیر | خیر | Never |
| PhoneNumberVisibilityAudience | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_MessagingPrivacySettings | PK | UserId |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| UserId | identity.Users | Id | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_MessagingPrivacySettings_DirectMessagesAudience | `[DirectMessagesAudience] IN (1, 2, 3)` |
| CK_MessagingPrivacySettings_GroupInvitesAudience | `[GroupInvitesAudience] IN (1, 2, 3)` |
| CK_MessagingPrivacySettings_LastSeenAudience | `[LastSeenAudience] IN (1, 2, 3)` |
| CK_MessagingPrivacySettings_OnlineAudience | `[OnlineAudience] IN (1, 2, 3)` |
| CK_MessagingPrivacySettings_PhoneNumberDiscoveryAudience | `[PhoneNumberDiscoveryAudience] IN (1, 2, 3)` |
| CK_MessagingPrivacySettings_PhoneNumberVisibilityAudience | `[PhoneNumberVisibilityAudience] IN (1, 2, 3)` |

### messaging.ModerationActions

Entity: `MakanApp.Domain.Messaging.ModerationAction`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ActorUserId | `uniqueidentifier` | بله | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| ModerationCaseId | `uniqueidentifier` | خیر | خیر | Never |
| Type | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ModerationActions | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ModerationActions_ActorUserId | ActorUserId | False | — |
| IX_ModerationActions_Case_Created | ModerationCaseId, CreatedAtUtc, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ActorUserId | identity.Users | Id | Restrict |
| ModerationCaseId | messaging.ModerationCases | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ModerationActions_Type | `[Type] IN (1, 2, 3, 4, 5, 6, 7, 8)` |

### messaging.ModerationCases

Entity: `MakanApp.Domain.Messaging.ModerationCase`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AbuseReportId | `uniqueidentifier` | بله | خیر | Never |
| AssignedReviewerUserId | `uniqueidentifier` | بله | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DismissedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| InternalNote | `nvarchar(1000)` | بله | خیر | Never |
| Priority | `int` | خیر | خیر | Never |
| QueueScope | `int` | خیر | خیر | Never |
| ResolvedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ReviewStartedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SafetyIncidentId | `uniqueidentifier` | بله | خیر | Never |
| Source | `int` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ModerationCases | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_ModerationCases_AbuseReportId | AbuseReportId | True | [AbuseReportId] IS NOT NULL |
| UX_ModerationCases_SafetyIncidentId | SafetyIncidentId | True | [SafetyIncidentId] IS NOT NULL |
| IX_ModerationCases_Assigned_Status | AssignedReviewerUserId, Status | False | [AssignedReviewerUserId] IS NOT NULL |
| IX_ModerationCases_Conversation_Status_Created | ConversationId, Status, CreatedAtUtc | False | — |
| IX_ModerationCases_Queue_Status_Priority_Created | QueueScope, Status, Priority, CreatedAtUtc, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| AbuseReportId | messaging.AbuseReports | Id | Restrict |
| AssignedReviewerUserId | identity.Users | Id | Restrict |
| ConversationId | messaging.Conversations | Id | Restrict |
| SafetyIncidentId | messaging.SafetyIncidents | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ModerationCases_Lifecycle | `([Status] = 1 AND [AssignedReviewerUserId] IS NULL AND [ReviewStartedAtUtc] IS NULL AND [ResolvedAtUtc] IS NULL AND [DismissedAtUtc] IS NULL) OR ([Status] = 2 AND [AssignedReviewerUserId] IS NOT NULL AND [ReviewStartedAtUtc] IS NOT NULL AND [ResolvedAtUtc] IS NULL AND [DismissedAtUtc] IS NULL) OR ([Status] = 3 AND [AssignedReviewerUserId] IS NOT NULL AND [ReviewStartedAtUtc] IS NOT NULL AND [ResolvedAtUtc] IS NOT NULL AND [DismissedAtUtc] IS NULL) OR ([Status] = 4 AND [AssignedReviewerUserId] IS NOT NULL AND [ReviewStartedAtUtc] IS NOT NULL AND [ResolvedAtUtc] IS NULL AND [DismissedAtUtc] IS NOT NULL)` |
| CK_ModerationCases_Priority | `[Priority] IN (1, 2, 3)` |
| CK_ModerationCases_QueueScope | `[QueueScope] IN (1, 2)` |
| CK_ModerationCases_Source | `[Source] IN (1, 2)` |
| CK_ModerationCases_SourceShape | `([Source] = 1 AND [AbuseReportId] IS NOT NULL AND [SafetyIncidentId] IS NULL) OR ([Source] = 2 AND [AbuseReportId] IS NULL AND [SafetyIncidentId] IS NOT NULL)` |
| CK_ModerationCases_Status | `[Status] IN (1, 2, 3, 4)` |

### messaging.ModerationEvidenceAttachments

Entity: `MakanApp.Domain.Messaging.ModerationEvidenceAttachment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| FileAssetId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| ModerationCaseId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ModerationEvidenceAttachments | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ModerationEvidenceAttachments_FileAssetId | FileAssetId | False | — |
| UX_ModerationEvidenceAttachments_Case_File | ModerationCaseId, FileAssetId | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| FileAssetId | storage.FileAssets | Id | Restrict |
| ModerationCaseId | messaging.ModerationCases | Id | Restrict |

### messaging.PersonalCommunicationGrants

Entity: `MakanApp.Domain.Messaging.PersonalCommunicationGrant`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| GrantedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| HigherUserId | `uniqueidentifier` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LowerUserId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PersonalCommunicationGrants | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_PersonalCommunicationGrants_HigherUserId | HigherUserId | False | — |
| UX_PersonalCommunicationGrants_UserPair | LowerUserId, HigherUserId | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| HigherUserId | identity.Users | Id | Restrict |
| LowerUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_PersonalCommunicationGrants_DistinctUsers | `[LowerUserId] <> [HigherUserId]` |
| CK_PersonalCommunicationGrants_Status | `[Status] IN (1, 2, 3)` |

### messaging.RealtimeOutbox

Entity: `MakanApp.Infrastructure.Messaging.MessagingRealtimeOutboxMessage`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AttemptCount | `int` | خیر | خیر | Never |
| ChangeEventId | `uniqueidentifier` | خیر | خیر | Never |
| ClaimedUntilUtc | `datetime2(7)` | بله | خیر | Never |
| DispatchedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| NextAttemptAtUtc | `datetime2(7)` | بله | خیر | Never |
| OccurredAtUtc | `datetime2(7)` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_RealtimeOutbox | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_RealtimeOutbox_ChangeEvent | ChangeEventId | True | — |
| IX_RealtimeOutbox_Pending | DispatchedAtUtc, NextAttemptAtUtc, ClaimedUntilUtc, OccurredAtUtc | False | [DispatchedAtUtc] IS NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ChangeEventId | messaging.ChangeEvents | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_RealtimeOutbox_AttemptCount | `[AttemptCount] >= 0` |
| CK_RealtimeOutbox_Lifecycle | `[DispatchedAtUtc] IS NULL OR ([ClaimedUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)` |

### messaging.SafetyIncidents

Entity: `MakanApp.Domain.Messaging.SafetyIncident`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ActorUserId | `uniqueidentifier` | خیر | خیر | Never |
| ClientMessageId | `uniqueidentifier` | خیر | خیر | Never |
| ClosedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ContentHash | `binary(32)` | خیر | خیر | Never |
| ContentKind | `int` | خیر | خیر | Never |
| ContentSnapshot | `nvarchar(4000)` | بله | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Decision | `int` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| PolicyCodes | `nvarchar(1000)` | بله | خیر | Never |
| ProviderReference | `nvarchar(200)` | بله | خیر | Never |
| RequestPayloadHash | `binary(32)` | خیر | خیر | Never |
| RiskLevel | `nvarchar(50)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_SafetyIncidents | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_SafetyIncidents_ActorUserId | ActorUserId | False | — |
| IX_SafetyIncidents_Conversation_Created | ConversationId, CreatedAtUtc | False | — |
| UX_SafetyIncidents_Conversation_Actor_ClientMessageId | ConversationId, ActorUserId, ClientMessageId | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ActorUserId | identity.Users | Id | Restrict |
| ConversationId | messaging.Conversations | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_SafetyIncidents_ContentKind | `[ContentKind] IN (1, 2, 3, 4, 5, 6, 7, 8)` |
| CK_SafetyIncidents_Decision | `[Decision] IN (2, 3)` |
| CK_SafetyIncidents_Lifecycle | `([Status] = 1 AND [ClosedAtUtc] IS NULL) OR ([Status] IN (2, 3) AND [ClosedAtUtc] IS NOT NULL)` |
| CK_SafetyIncidents_Status | `[Status] IN (1, 2, 3)` |

### messaging.SavedMessageLabels

Entity: `MakanApp.Domain.Messaging.SavedMessageLabel`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| MessageId | `uniqueidentifier` | خیر | خیر | Never |
| Name | `nvarchar(32)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_SavedMessageLabels | PK | MessageId, Name |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_SavedMessageLabels_Name_MessageId | Name, MessageId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| MessageId | messaging.SavedMessageOrganizations | MessageId | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_SavedMessageLabels_Name | `LEN(LTRIM(RTRIM([Name]))) > 0` |

### messaging.SavedMessageOrganizations

Entity: `MakanApp.Domain.Messaging.SavedMessageOrganization`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| MessageId | `uniqueidentifier` | خیر | خیر | Never |
| OwnerUserId | `uniqueidentifier` | خیر | خیر | Never |
| Revision | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_SavedMessageOrganizations | PK | MessageId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_SavedMessageOrganizations_OwnerUserId_MessageId | OwnerUserId, MessageId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| MessageId | messaging.Messages | Id | Cascade |
| OwnerUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_SavedMessageOrganizations_Revision | `[Revision] > 0` |

### messaging.ScheduledMessageAttachments

Entity: `MakanApp.Domain.Messaging.ScheduledMessageAttachment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| FileAssetId | `uniqueidentifier` | خیر | خیر | Never |
| ScheduledMessageId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ScheduledMessageAttachments | PK | ScheduledMessageId, FileAssetId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ScheduledMessageAttachments_FileAssetId | FileAssetId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| FileAssetId | storage.FileAssets | Id | Restrict |
| ScheduledMessageId | messaging.ScheduledMessages | Id | Restrict |

### messaging.ScheduledMessageMentions

Entity: `MakanApp.Domain.Messaging.ScheduledMessageMention`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ScheduledMessageId | `uniqueidentifier` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ScheduledMessageMentions | PK | ScheduledMessageId, UserId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ScheduledMessageMentions_UserId | UserId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ScheduledMessageId | messaging.ScheduledMessages | Id | Restrict |
| UserId | identity.Users | Id | Restrict |

### messaging.ScheduledMessages

Entity: `MakanApp.Domain.Messaging.ScheduledMessage`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AttemptCount | `int` | خیر | خیر | Never |
| ClaimedUntilUtc | `datetime2(7)` | بله | خیر | Never |
| ClaimToken | `uniqueidentifier` | بله | خیر | Never |
| ClientMessageId | `uniqueidentifier` | خیر | خیر | Never |
| CompletedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ContactUserId | `uniqueidentifier` | بله | خیر | Never |
| ConversationId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DispatchedMessageId | `uniqueidentifier` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| IsSilent | `bit` | خیر | خیر | Never |
| Kind | `int` | خیر | خیر | Never |
| LastFailureCode | `nvarchar(100)` | بله | خیر | Never |
| MembershipId | `uniqueidentifier` | بله | خیر | Never |
| NextAttemptAtUtc | `datetime2(7)` | بله | خیر | Never |
| OrganizationId | `uniqueidentifier` | بله | خیر | Never |
| PayloadHash | `binary(32)` | خیر | خیر | Never |
| ReplyToMessageId | `uniqueidentifier` | بله | خیر | Never |
| Role | `int` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| ScheduledAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| SubjectOrganizationPersonId | `uniqueidentifier` | بله | خیر | Never |
| Text | `nvarchar(4000)` | بله | خیر | Never |
| TopicId | `uniqueidentifier` | بله | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |
| WorkspaceType | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ScheduledMessages | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ScheduledMessages_ContactUserId | ContactUserId | False | — |
| IX_ScheduledMessages_MembershipId | MembershipId | False | — |
| IX_ScheduledMessages_OrganizationId | OrganizationId | False | — |
| IX_ScheduledMessages_SubjectOrganizationPersonId | SubjectOrganizationPersonId | False | — |
| IX_ScheduledMessages_UserId | UserId | False | — |
| IX_ScheduledMessages_DispatchedMessageId_ConversationId | DispatchedMessageId, ConversationId | False | — |
| IX_ScheduledMessages_ReplyToMessageId_ConversationId | ReplyToMessageId, ConversationId | False | — |
| IX_ScheduledMessages_TopicId_ConversationId | TopicId, ConversationId | False | — |
| IX_ScheduledMessages_ConversationId_UserId_ClientMessageId | ConversationId, UserId, ClientMessageId | True | — |
| IX_ScheduledMessages_ConversationId_UserId_Status | ConversationId, UserId, Status | False | — |
| IX_ScheduledMessages_Status_ScheduledAtUtc_Id | Status, ScheduledAtUtc, Id | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ContactUserId | identity.Users | Id | Restrict |
| ConversationId | messaging.Conversations | Id | Restrict |
| MembershipId | organization.Memberships | Id | Restrict |
| OrganizationId | organization.Organizations | Id | Restrict |
| SubjectOrganizationPersonId | organization.OrganizationPersons | Id | Restrict |
| UserId | identity.Users | Id | Restrict |
| DispatchedMessageId, ConversationId | messaging.Messages | Id, ConversationId | Restrict |
| ReplyToMessageId, ConversationId | messaging.Messages | Id, ConversationId | Restrict |
| TopicId, ConversationId | messaging.ConversationTopics | Id, ConversationId | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ScheduledMessages_Attempts | `[AttemptCount] BETWEEN 0 AND 5` |
| CK_ScheduledMessages_Context | `([WorkspaceType]=1 AND [OrganizationId] IS NULL AND [MembershipId] IS NULL AND [Role] IS NULL AND [SubjectOrganizationPersonId] IS NULL) OR ([WorkspaceType]=2 AND [OrganizationId] IS NOT NULL AND [MembershipId] IS NOT NULL AND [Role] IS NOT NULL AND [Role] IN (1,2,3,4))` |
| CK_ScheduledMessages_Hash | `DATALENGTH([PayloadHash]) = 32` |
| CK_ScheduledMessages_Kind | `[Kind] IN (1,2,3,4,5,6)` |
| CK_ScheduledMessages_Lease | `([ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL) OR ([Status]=1 AND [ClaimToken] IS NOT NULL AND [ClaimedUntilUtc] IS NOT NULL)` |
| CK_ScheduledMessages_Lifecycle | `([Status]=1 AND [CompletedAtUtc] IS NULL AND [DispatchedMessageId] IS NULL) OR ([Status]=2 AND [CompletedAtUtc] IS NOT NULL AND [DispatchedMessageId] IS NOT NULL) OR ([Status] IN (3,4) AND [CompletedAtUtc] IS NOT NULL AND [DispatchedMessageId] IS NULL)` |
| CK_ScheduledMessages_Status | `[Status] IN (1,2,3,4)` |
| CK_ScheduledMessages_UTCOrder | `[ScheduledAtUtc] > [CreatedAtUtc]` |

### messaging.UserBlocks

Entity: `MakanApp.Domain.Messaging.UserBlock`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| BlockedUserId | `uniqueidentifier` | خیر | خیر | Never |
| BlockerUserId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_UserBlocks | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_UserBlocks_BlockedUserId | BlockedUserId | False | — |
| UX_UserBlocks_Active_DirectionalPair | BlockerUserId, BlockedUserId | True | [EndedAtUtc] IS NULL |
| IX_UserBlocks_Blocker_CreatedAtUtc | BlockerUserId, CreatedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| BlockedUserId | identity.Users | Id | Restrict |
| BlockerUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_UserBlocks_DistinctUsers | `[BlockerUserId] <> [BlockedUserId]` |
| CK_UserBlocks_Lifecycle | `([Status] = 1 AND [EndedAtUtc] IS NULL) OR ([Status] = 2 AND [EndedAtUtc] IS NOT NULL AND [EndedAtUtc] >= [CreatedAtUtc])` |
| CK_UserBlocks_Status | `[Status] IN (1, 2)` |

### messaging.UserContacts

Entity: `MakanApp.Domain.Messaging.UserContact`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ContactUserId | `uniqueidentifier` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| IsFavorite | `bit` | خیر | خیر | Never |
| OwnerUserId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SavedName | `nvarchar(200)` | بله | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_UserContacts | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_UserContacts_ContactUserId | ContactUserId | False | — |
| UX_UserContacts_Owner_Contact | OwnerUserId, ContactUserId | True | — |
| IX_UserContacts_Owner_IsFavorite | OwnerUserId, IsFavorite | False | — |
| IX_UserContacts_Owner_UpdatedAtUtc | OwnerUserId, UpdatedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ContactUserId | identity.Users | Id | Restrict |
| OwnerUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_UserContacts_DistinctUsers | `[OwnerUserId] <> [ContactUserId]` |

### notification.NotificationDeliveryAttempts

Entity: `MakanApp.Domain.Notification.NotificationDeliveryAttempt`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AttemptCount | `int` | خیر | خیر | Never |
| Channel | `int` | خیر | خیر | Never |
| ClaimedUntilUtc | `datetime2(7)` | بله | خیر | Never |
| ClaimToken | `uniqueidentifier` | بله | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DeliveredAtUtc | `datetime2(7)` | بله | خیر | Never |
| DestinationReference | `varchar(100)` | خیر | خیر | Never |
| FailureReason | `int` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LastAttemptAtUtc | `datetime2(7)` | بله | خیر | Never |
| NextAttemptAtUtc | `datetime2(7)` | بله | خیر | Never |
| NotificationId | `uniqueidentifier` | خیر | خیر | Never |
| ProviderMessageId | `nvarchar(200)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_NotificationDeliveryAttempts | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_NotificationDeliveryAttempts_StableDestination | NotificationId, Channel, DestinationReference | True | — |
| IX_NotificationDeliveryAttempts_Pending | Status, NextAttemptAtUtc, ClaimedUntilUtc, CreatedAtUtc | False | [Status] IN (1, 2, 3) |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| NotificationId | notification.Notifications | Id | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_NotificationDeliveryAttempts_AttemptCount | `[AttemptCount] >= 0` |
| CK_NotificationDeliveryAttempts_Channel | `[Channel] IN (1, 2)` |
| CK_NotificationDeliveryAttempts_Delivered | `([Status] = 4 AND [DeliveredAtUtc] IS NOT NULL) OR ([Status] <> 4 AND [DeliveredAtUtc] IS NULL)` |
| CK_NotificationDeliveryAttempts_FailureReason | `[FailureReason] IS NULL OR [FailureReason] BETWEEN 1 AND 13` |
| CK_NotificationDeliveryAttempts_Lifecycle | `([Status] = 2 AND [ClaimToken] IS NOT NULL AND [ClaimedUntilUtc] IS NOT NULL) OR ([Status] <> 2 AND [ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL)` |
| CK_NotificationDeliveryAttempts_Status | `[Status] BETWEEN 1 AND 6` |

### notification.NotificationPreferences

Entity: `MakanApp.Domain.Notification.NotificationPreference`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Category | `int` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | بله | خیر | Never |
| PushEnabled | `bit` | خیر | خیر | Never |
| QuietHoursEnabled | `bit` | خیر | خیر | Never |
| QuietHoursEndLocal | `time(0)` | بله | خیر | Never |
| QuietHoursStartLocal | `time(0)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SmsEnabled | `bit` | خیر | خیر | Never |
| TimeZoneId | `nvarchar(100)` | بله | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_NotificationPreferences | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_NotificationPreferences_OrganizationId | OrganizationId | False | — |
| UX_NotificationPreferences_User_Personal_Category | UserId, Category | True | [OrganizationId] IS NULL |
| UX_NotificationPreferences_User_Organization_Category | UserId, OrganizationId, Category | True | [OrganizationId] IS NOT NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId | organization.Organizations | Id | Restrict |
| UserId | identity.Users | Id | Cascade |

CHECKها:

| نام | SQL |
|---|---|
| CK_NotificationPreferences_Category | `[Category] BETWEEN 1 AND 7` |
| CK_NotificationPreferences_QuietHours | `([QuietHoursEnabled] = 0 AND [QuietHoursStartLocal] IS NULL AND [QuietHoursEndLocal] IS NULL AND [TimeZoneId] IS NULL) OR ([QuietHoursEnabled] = 1 AND [QuietHoursStartLocal] IS NOT NULL AND [QuietHoursEndLocal] IS NOT NULL AND [QuietHoursStartLocal] <> [QuietHoursEndLocal] AND [TimeZoneId] IS NOT NULL)` |

### notification.Notifications

Entity: `MakanApp.Domain.Notification.Notification`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Body | `nvarchar(500)` | خیر | خیر | Never |
| Category | `int` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | بله | خیر | Never |
| ReadAtUtc | `datetime2(7)` | بله | خیر | Never |
| RecipientUserId | `uniqueidentifier` | خیر | خیر | Never |
| ResourceId | `uniqueidentifier` | خیر | خیر | Never |
| ResourceType | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| SourceEventId | `uniqueidentifier` | خیر | خیر | Never |
| Title | `nvarchar(200)` | خیر | خیر | Never |
| Type | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Notifications | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Notifications_OrganizationId | OrganizationId | False | — |
| IX_Notifications_Recipient_Created | RecipientUserId, CreatedAtUtc | False | — |
| IX_Notifications_Recipient_ReadAt | RecipientUserId, ReadAtUtc | False | — |
| UX_Notifications_SourceEvent_Recipient | SourceEventId, RecipientUserId | True | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId | organization.Organizations | Id | Restrict |
| RecipientUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Notifications_Category | `[Category] IN (1, 2, 3)` |
| CK_Notifications_ReadAt | `[ReadAtUtc] IS NULL OR [ReadAtUtc] >= [CreatedAtUtc]` |
| CK_Notifications_ResourceType | `[ResourceType] IN (1, 2, 3, 4, 5)` |
| CK_Notifications_Type | `[Type] IN (1, 2, 3, 4, 5, 6, 7)` |

### notification.PushDeviceRegistrations

Entity: `MakanApp.Domain.Notification.PushDeviceRegistration`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LastSeenAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Platform | `int` | خیر | خیر | Never |
| PushToken | `nvarchar(2048)` | خیر | خیر | Never |
| RevokedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| TokenHash | `varbinary(32)` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |
| UserSessionId | `uniqueidentifier` | خیر | خیر | Never |
| WebPushAuth | `varchar(22)` | بله | خیر | Never |
| WebPushP256dh | `varchar(87)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PushDeviceRegistrations | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_PushDeviceRegistrations_ActiveToken | TokenHash | True | [RevokedAtUtc] IS NULL |
| IX_PushDeviceRegistrations_User_Active | UserId, RevokedAtUtc | False | — |
| IX_PushDeviceRegistrations_UserSessionId_UserId | UserSessionId, UserId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| UserId | identity.Users | Id | Cascade |
| UserSessionId, UserId | identity.UserSessions | Id, UserId | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_PushDeviceRegistrations_Platform | `[Platform] IN (1, 2, 3)` |
| CK_PushDeviceRegistrations_WebKeys | `([WebPushP256dh] IS NULL AND [WebPushAuth] IS NULL) OR ([Platform] = 3 AND [WebPushP256dh] IS NOT NULL AND LEN([WebPushP256dh]) = 87 AND [WebPushAuth] IS NOT NULL AND LEN([WebPushAuth]) = 22)` |

### organization.Invitations

Entity: `MakanApp.Domain.Organization.Invitation`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AcceptedAtUtc | `datetime2(7)` | بله | خیر | Never |
| AcceptedGuardianRelationId | `uniqueidentifier` | بله | خیر | Never |
| AcceptedMembershipId | `uniqueidentifier` | بله | خیر | Never |
| AcceptedRoleAssignmentId | `uniqueidentifier` | بله | خیر | Never |
| ClientOperationId | `uniqueidentifier` | بله | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DeclinedAtUtc | `datetime2(7)` | بله | خیر | Never |
| DestinationUserId | `uniqueidentifier` | خیر | خیر | Never |
| ExpiresAtUtc | `datetime2(7)` | خیر | خیر | Never |
| GuardianLearnerOrganizationPersonId | `uniqueidentifier` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| InvitedByUserId | `uniqueidentifier` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| OrganizationPersonId | `uniqueidentifier` | بله | خیر | Never |
| RevokedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Role | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Invitations | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Invitations_AcceptedGuardianRelationId | AcceptedGuardianRelationId | False | — |
| IX_Invitations_AcceptedMembershipId | AcceptedMembershipId | False | — |
| IX_Invitations_AcceptedRoleAssignmentId | AcceptedRoleAssignmentId | False | — |
| IX_Invitations_InvitedByUserId | InvitedByUserId | False | — |
| UX_Invitations_LinkOperation | OrganizationId, ClientOperationId | True | [ClientOperationId] IS NOT NULL |
| IX_Invitations_OrganizationId_GuardianLearnerOrganizationPersonId | OrganizationId, GuardianLearnerOrganizationPersonId | False | — |
| UX_Invitations_Pending_PersonLink | OrganizationId, OrganizationPersonId | True | [OrganizationPersonId] IS NOT NULL AND [Status] = 1 |
| UX_Invitations_Pending_Destination_Organization_Role | DestinationUserId, OrganizationId, Role | True | [Status] = 1 AND [GuardianLearnerOrganizationPersonId] IS NULL |
| UX_Invitations_Pending_Guardian_Learner | OrganizationId, DestinationUserId, GuardianLearnerOrganizationPersonId | True | [Status] = 1 AND [GuardianLearnerOrganizationPersonId] IS NOT NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| AcceptedGuardianRelationId | guardian.GuardianRelations | Id | Restrict |
| AcceptedMembershipId | organization.Memberships | Id | Restrict |
| AcceptedRoleAssignmentId | organization.RoleAssignments | Id | Restrict |
| DestinationUserId | identity.Users | Id | Restrict |
| InvitedByUserId | identity.Users | Id | Restrict |
| OrganizationId | organization.Organizations | Id | Restrict |
| OrganizationId, GuardianLearnerOrganizationPersonId | organization.OrganizationPersons | OrganizationId, Id | Restrict |
| OrganizationId, OrganizationPersonId | organization.OrganizationPersons | OrganizationId, Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Invitations_Expiry | `[ExpiresAtUtc] > [CreatedAtUtc]` |
| CK_Invitations_GuardianReceipt | `([GuardianLearnerOrganizationPersonId] IS NULL AND [AcceptedGuardianRelationId] IS NULL) OR ([GuardianLearnerOrganizationPersonId] IS NOT NULL AND (([Status]=2 AND [AcceptedGuardianRelationId] IS NOT NULL) OR ([Status]<>2 AND [AcceptedGuardianRelationId] IS NULL)))` |
| CK_Invitations_PersonLink | `([OrganizationPersonId] IS NULL AND [GuardianLearnerOrganizationPersonId] IS NULL AND [ClientOperationId] IS NULL) OR ([OrganizationPersonId] IS NOT NULL AND [GuardianLearnerOrganizationPersonId] IS NULL AND [ClientOperationId] IS NOT NULL AND [Role] = 1) OR ([OrganizationPersonId] IS NULL AND [GuardianLearnerOrganizationPersonId] IS NOT NULL AND [ClientOperationId] IS NOT NULL AND [Role] = 3)` |
| CK_Invitations_Role | `[Role] IN (1, 2, 3, 4)` |
| CK_Invitations_Status | `[Status] IN (1, 2, 3, 4, 5)` |

### organization.Memberships

Entity: `MakanApp.Domain.Organization.Membership`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ActivatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Memberships | PK | Id |
| UQ_Memberships_OrganizationId_Id | Alternate Key | OrganizationId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_Memberships_Active_User_Organization | UserId, OrganizationId | True | [Status] = 1 AND [EndedAtUtc] IS NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId | organization.Organizations | Id | Restrict |
| UserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_Memberships_Status | `[Status] IN (1, 2, 3)` |

### organization.OrganizationPersons

Entity: `MakanApp.Domain.Organization.OrganizationPerson`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ActivatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| CreationOperationId | `uniqueidentifier` | بله | خیر | Never |
| CreationRequestHash | `varchar(64)` | بله | خیر | Never |
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| PersonId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_OrganizationPersons | PK | Id |
| UQ_OrganizationPersons_OrganizationId_Id | Alternate Key | OrganizationId, Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_OrganizationPersons_PersonId | PersonId | False | — |
| UX_OrganizationPersons_CreationOperation | OrganizationId, CreationOperationId | True | [CreationOperationId] IS NOT NULL |
| UX_OrganizationPersons_Active_Organization_Person | OrganizationId, PersonId | True | [Status] = 1 AND [EndedAtUtc] IS NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId | organization.Organizations | Id | Restrict |
| PersonId | identity.Persons | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_OrganizationPersons_CreationRequest | `([CreationOperationId] IS NULL AND [CreationRequestHash] IS NULL) OR ([CreationOperationId] IS NOT NULL AND [CreationRequestHash] IS NOT NULL AND LEN([CreationRequestHash]) = 64)` |
| CK_OrganizationPersons_Status | `[Status] IN (1, 2)` |

### organization.Organizations

Entity: `MakanApp.Domain.Organization.Organization`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| Name | `nvarchar(200)` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_Organizations | PK | Id |

CHECKها:

| نام | SQL |
|---|---|
| CK_Organizations_Status | `[Status] IN (1, 2)` |

### organization.RoleAssignments

Entity: `MakanApp.Domain.Organization.RoleAssignment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AssignedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| MembershipId | `uniqueidentifier` | خیر | خیر | Never |
| Role | `int` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_RoleAssignments | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_RoleAssignments_Active_Membership_Role | MembershipId, Role | True | [Status] = 1 AND [EndedAtUtc] IS NULL |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| MembershipId | organization.Memberships | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_RoleAssignments_Role | `[Role] IN (1, 2, 3, 4)` |
| CK_RoleAssignments_Status | `[Status] IN (1, 2)` |

### platform.AdminAuditLogs

Entity: `MakanApp.Domain.Platform.AdminAuditLog`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Action | `nvarchar(150)` | خیر | خیر | Never |
| ActorPersonId | `uniqueidentifier` | بله | خیر | Never |
| ActorSessionId | `uniqueidentifier` | بله | خیر | Never |
| ActorUserId | `uniqueidentifier` | بله | خیر | Never |
| AfterSummary | `nvarchar(2000)` | بله | خیر | Never |
| BeforeSummary | `nvarchar(2000)` | بله | خیر | Never |
| CorrelationId | `nvarchar(100)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OccurredAtUtc | `datetime2(7)` | خیر | خیر | Never |
| OrganizationId | `uniqueidentifier` | بله | خیر | Never |
| Reason | `nvarchar(500)` | خیر | خیر | Never |
| Result | `int` | خیر | خیر | Never |
| TargetId | `nvarchar(100)` | خیر | خیر | Never |
| TargetType | `nvarchar(100)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_AdminAuditLogs | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_AdminAuditLogs_ActorPersonId | ActorPersonId | False | — |
| IX_AdminAuditLogs_ActorSessionId | ActorSessionId | False | — |
| IX_AdminAuditLogs_OccurredAtUtc | OccurredAtUtc | False | — |
| IX_AdminAuditLogs_OrganizationId | OrganizationId | False | — |
| IX_AdminAuditLogs_Action_OccurredAtUtc | Action, OccurredAtUtc | False | — |
| IX_AdminAuditLogs_Actor_OccurredAtUtc | ActorUserId, OccurredAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| ActorPersonId | identity.Persons | Id | Restrict |
| ActorSessionId | identity.UserSessions | Id | Restrict |
| ActorUserId | identity.Users | Id | Restrict |
| OrganizationId | organization.Organizations | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_AdminAuditLogs_Result | `[Result] IN (1, 2, 3)` |

### platform.OutboxMessages

Entity: `MakanApp.Infrastructure.Outbox.OutboxMessage`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AttemptCount | `int` | خیر | خیر | Never |
| ClaimedUntilUtc | `datetime2(7)` | بله | خیر | Never |
| ClaimToken | `uniqueidentifier` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LastErrorCode | `nvarchar(100)` | بله | خیر | Never |
| MessageType | `nvarchar(100)` | خیر | خیر | Never |
| NextAttemptAtUtc | `datetime2(7)` | بله | خیر | Never |
| OccurredAtUtc | `datetime2(7)` | خیر | خیر | Never |
| Payload | `nvarchar(max)` | خیر | خیر | Never |
| ProcessedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_OutboxMessages | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_Outbox_Pending | ProcessedAtUtc, NextAttemptAtUtc, ClaimedUntilUtc, OccurredAtUtc | False | [ProcessedAtUtc] IS NULL |

CHECKها:

| نام | SQL |
|---|---|
| CK_Outbox_AttemptCount | `[AttemptCount] >= 0` |
| CK_Outbox_Lifecycle | `[ProcessedAtUtc] IS NULL OR ([ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)` |

### platform.PlatformPermissions

Entity: `MakanApp.Domain.Platform.PlatformPermission`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| Description | `nvarchar(300)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| Key | `nvarchar(150)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PlatformPermissions | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_PlatformPermissions_Key | Key | True | — |

### platform.PlatformRoleAssignments

Entity: `MakanApp.Domain.Platform.PlatformRoleAssignment`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| EndedAtUtc | `datetime2(7)` | بله | خیر | Never |
| EndedByUserId | `uniqueidentifier` | بله | خیر | Never |
| GrantedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| GrantedByUserId | `uniqueidentifier` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| RoleId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| UserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PlatformRoleAssignments | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_PlatformRoleAssignments_EndedByUserId | EndedByUserId | False | — |
| IX_PlatformRoleAssignments_GrantedByUserId | GrantedByUserId | False | — |
| UX_PlatformRoleAssignments_Active_User_Role | UserId, RoleId | True | [Status] = 1 AND [EndedAtUtc] IS NULL |
| IX_PlatformRoleAssignments_Role_Status | RoleId, Status, EndedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| EndedByUserId | identity.Users | Id | Restrict |
| GrantedByUserId | identity.Users | Id | Restrict |
| RoleId | platform.PlatformRoles | Id | Restrict |
| UserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_PlatformRoleAssignments_Status | `[Status] IN (1, 2)` |

### platform.PlatformRolePermissions

Entity: `MakanApp.Domain.Platform.PlatformRolePermission`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| PermissionId | `uniqueidentifier` | خیر | خیر | Never |
| RoleId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PlatformRolePermissions | PK | RoleId, PermissionId |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_PlatformRolePermissions_PermissionId | PermissionId | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| PermissionId | platform.PlatformPermissions | Id | Restrict |
| RoleId | platform.PlatformRoles | Id | Restrict |

### platform.PlatformRoles

Entity: `MakanApp.Domain.Platform.PlatformRole`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| DisplayName | `nvarchar(150)` | خیر | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| Key | `nvarchar(100)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_PlatformRoles | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_PlatformRoles_Key | Key | True | — |

### platform.ProvisioningRequests

Entity: `MakanApp.Domain.Platform.PlatformProvisioningRequest`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| ClientOperationId | `nvarchar(100)` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| FailureCode | `nvarchar(100)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| InitialManagerUserId | `uniqueidentifier` | بله | خیر | Never |
| InvitationExpiresAtUtc | `datetime2(7)` | بله | خیر | Never |
| ManagerInvitationId | `uniqueidentifier` | بله | خیر | Never |
| OrganizationId | `uniqueidentifier` | بله | خیر | Never |
| OrganizationName | `nvarchar(200)` | خیر | خیر | Never |
| PlanVersionId | `uniqueidentifier` | بله | خیر | Never |
| Reason | `nvarchar(500)` | خیر | خیر | Never |
| RequestedByUserId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Status | `int` | خیر | خیر | Never |
| SubscriptionEndsAtUtc | `datetime2(7)` | بله | خیر | Never |
| SubscriptionId | `uniqueidentifier` | بله | خیر | Never |
| UpdatedAtUtc | `datetime2(7)` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ProvisioningRequests | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| UX_ProvisioningRequests_ClientOperationId | ClientOperationId | True | — |
| IX_ProvisioningRequests_InitialManagerUserId | InitialManagerUserId | False | — |
| IX_ProvisioningRequests_ManagerInvitationId | ManagerInvitationId | False | — |
| IX_ProvisioningRequests_OrganizationId | OrganizationId | False | — |
| IX_ProvisioningRequests_PlanVersionId | PlanVersionId | False | — |
| IX_ProvisioningRequests_RequestedByUserId | RequestedByUserId | False | — |
| IX_ProvisioningRequests_SubscriptionId | SubscriptionId | False | — |
| IX_ProvisioningRequests_Status_UpdatedAtUtc | Status, UpdatedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| InitialManagerUserId | identity.Users | Id | Restrict |
| ManagerInvitationId | organization.Invitations | Id | Restrict |
| OrganizationId | organization.Organizations | Id | Restrict |
| PlanVersionId | commerce.PlanVersions | Id | Restrict |
| RequestedByUserId | identity.Users | Id | Restrict |
| SubscriptionId | commerce.Subscriptions | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ProvisioningRequests_Status | `[Status] BETWEEN 1 AND 6` |

### reports.ReportExportJobs

Entity: `MakanApp.Domain.Reports.ReportExportJob`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| AcademicPeriodId | `uniqueidentifier` | بله | خیر | Never |
| AssessmentKind | `int` | بله | خیر | Never |
| AttemptCount | `int` | خیر | خیر | Never |
| Audience | `int` | خیر | خیر | Never |
| ClassId | `uniqueidentifier` | بله | خیر | Never |
| CompletedAtUtc | `datetime2(7)` | بله | خیر | Never |
| CourseId | `uniqueidentifier` | بله | خیر | Never |
| DataAsOfUtc | `datetime2(7)` | خیر | خیر | Never |
| EnrollmentStatus | `int` | بله | خیر | Never |
| ExpectedRowCount | `int` | خیر | خیر | Never |
| FailedAtUtc | `datetime2(7)` | بله | خیر | Never |
| FailureCode | `varchar(64)` | بله | خیر | Never |
| Format | `int` | خیر | خیر | Never |
| FromUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| LeaseExpiresAtUtc | `datetime2(7)` | بله | خیر | Never |
| MembershipId | `uniqueidentifier` | خیر | خیر | Never |
| NextAttemptAtUtc | `datetime2(7)` | بله | خیر | Never |
| OrganizationId | `uniqueidentifier` | خیر | خیر | Never |
| OutputFileAssetId | `uniqueidentifier` | بله | خیر | Never |
| ReportType | `int` | خیر | خیر | Never |
| RequestedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| RequestedBySessionId | `uniqueidentifier` | خیر | خیر | Never |
| RequestedByUserId | `uniqueidentifier` | خیر | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Sort | `varchar(32)` | خیر | خیر | Never |
| StartedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| SubjectOrganizationPersonId | `uniqueidentifier` | بله | خیر | Never |
| ToUtc | `datetime2(7)` | بله | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_ReportExportJobs | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_ReportExportJobs_MembershipId | MembershipId | False | — |
| IX_ReportExportJobs_OrganizationId | OrganizationId | False | — |
| UX_ReportExportJobs_OutputFileAsset | OutputFileAssetId | True | [OutputFileAssetId] IS NOT NULL |
| IX_ReportExportJobs_Owner_Organization_Requested | RequestedByUserId, OrganizationId, RequestedAtUtc | False | — |
| IX_ReportExportJobs_Status_NextAttempt_Requested | Status, NextAttemptAtUtc, RequestedAtUtc | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| MembershipId | organization.Memberships | Id | Restrict |
| OrganizationId | organization.Organizations | Id | Restrict |
| OutputFileAssetId | storage.FileAssets | Id | Restrict |
| RequestedByUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_ReportExportJobs_AssessmentKind | `[AssessmentKind] IS NULL OR [AssessmentKind] IN (1, 2)` |
| CK_ReportExportJobs_AttemptCount | `[AttemptCount] >= 0` |
| CK_ReportExportJobs_Audience | `[Audience] IN (1, 2, 3, 4)` |
| CK_ReportExportJobs_CompletedState | `([Status] = 3 AND [CompletedAtUtc] IS NOT NULL AND [OutputFileAssetId] IS NOT NULL AND [FailureCode] IS NULL) OR [Status] <> 3` |
| CK_ReportExportJobs_DateRange | `[FromUtc] IS NULL OR [ToUtc] IS NULL OR [FromUtc] <= [ToUtc]` |
| CK_ReportExportJobs_EnrollmentStatus | `[EnrollmentStatus] IS NULL OR [EnrollmentStatus] IN (1, 2, 3)` |
| CK_ReportExportJobs_ExpectedRowCount | `[ExpectedRowCount] >= 0` |
| CK_ReportExportJobs_FailedState | `([Status] = 4 AND [FailedAtUtc] IS NOT NULL AND [FailureCode] IS NOT NULL AND [OutputFileAssetId] IS NULL) OR [Status] <> 4` |
| CK_ReportExportJobs_Format | `[Format] = 1` |
| CK_ReportExportJobs_ProcessingLease | `([Status] = 2 AND [LeaseExpiresAtUtc] IS NOT NULL) OR [Status] <> 2` |
| CK_ReportExportJobs_ReportType | `[ReportType] BETWEEN 1 AND 14` |
| CK_ReportExportJobs_Status | `[Status] IN (1, 2, 3, 4)` |

### storage.FileAssets

Entity: `MakanApp.Domain.Storage.FileAsset`؛ مالک نگاشت: [EF Configurations](../MakanApp.Infrastructure/Persistence/Configurations).

| ستون | SQL type | NULL | Concurrency | تولید مقدار / SQL default |
|---|---|---|---|---|
| CompletedAtUtc | `datetime2(7)` | بله | خیر | Never |
| ContentType | `nvarchar(127)` | خیر | خیر | Never |
| CreatedAtUtc | `datetime2(7)` | خیر | خیر | Never |
| DeletedAtUtc | `datetime2(7)` | بله | خیر | Never |
| Id | `uniqueidentifier` | خیر | خیر | OnAdd |
| OrganizationId | `uniqueidentifier` | بله | خیر | Never |
| OriginalFileName | `nvarchar(255)` | خیر | خیر | Never |
| RejectedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RetainedAtUtc | `datetime2(7)` | بله | خیر | Never |
| RowVersion | `rowversion` | خیر | بله | OnAddOrUpdate |
| Sha256Hash | `varchar(64)` | بله | خیر | Never |
| SizeBytes | `bigint` | خیر | خیر | Never |
| Status | `int` | خیر | خیر | Never |
| StorageKey | `nvarchar(180)` | خیر | خیر | Never |
| UnattachedExpiresAtUtc | `datetime2(7)` | خیر | خیر | Never |
| UploadedByUserId | `uniqueidentifier` | خیر | خیر | Never |

کلیدها:

| نام | نوع | ستون‌ها |
|---|---|---|
| PK_FileAssets | PK | Id |

Indexها:

| نام | ستون‌ها | Unique | Filter |
|---|---|---|---|
| IX_FileAssets_OrganizationId | OrganizationId | False | — |
| UX_FileAssets_StorageKey | StorageKey | True | — |
| IX_FileAssets_Status_CreatedAtUtc | Status, CreatedAtUtc | False | — |
| IX_FileAssets_Status_UnattachedExpiresAtUtc | Status, UnattachedExpiresAtUtc | False | — |
| IX_FileAssets_Uploader_Organization_Status | UploadedByUserId, OrganizationId, Status | False | — |

روابط:

| ستون‌ها | مقصد | کلید مقصد | DeleteBehavior |
|---|---|---|---|
| OrganizationId | organization.Organizations | Id | Restrict |
| UploadedByUserId | identity.Users | Id | Restrict |

CHECKها:

| نام | SQL |
|---|---|
| CK_FileAssets_DeletedState | `([Status] = 4 AND [DeletedAtUtc] IS NOT NULL) OR [Status] <> 4` |
| CK_FileAssets_ReadyState | `([Status] = 2 AND [SizeBytes] > 0 AND [Sha256Hash] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL) OR [Status] <> 2` |
| CK_FileAssets_RejectedState | `([Status] = 3 AND [RejectedAtUtc] IS NOT NULL) OR [Status] <> 3` |
| CK_FileAssets_RetentionState | `[RetainedAtUtc] IS NULL OR [Status] = 2` |
| CK_FileAssets_SizeBytes | `[SizeBytes] >= 0` |
| CK_FileAssets_Status | `[Status] IN (1, 2, 3, 4)` |
| CK_FileAssets_UnattachedExpiry | `[UnattachedExpiresAtUtc] > [CreatedAtUtc]` |
