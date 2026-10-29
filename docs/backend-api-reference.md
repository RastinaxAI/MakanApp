# مرجع جاری Endpointها و قراردادهای API ماکان

بازبینی `2026-10-29` با مبنای Git `main@701cba057b358d9cf6d15d681389d0f8d22ac6d6` و metadata فایل‌های working tree؛ تولید از OpenAPI میزبان Testing و ApiExplorer، بدون SQL connection، worker، SMS یا AI واقعی.

پوشش: **332 عملیات HTTP، 291 مسیر متمایز، 59 Controller و 570 schema**. Health، Swagger و Hub در این شمارش نیستند.

این مرجع جایگزین کاتالوگ قدیمی ۱۴۱ عملیات است. نام فیلد، nullable، request/response و enum از سند واقعی runtime آمده‌اند؛ لازم‌بودن کسب‌وکاری، authorization منبع و خطاهای lifecycle علاوه بر schema در Application/Domain بررسی می‌شوند. هیچ نمونه یا تست زنده‌ای از روی وجود schema موفق فرض نمی‌شود.

## قرارداد مشترک و مرزهای امنیتی

- ورود از Bearer session موجود است، نه JWT فرضی یا Role فرستاده‌شده توسط client. وضعیت anonymous هر عملیات از metadata خودش استخراج شده است.
- policy درج‌شده، gate حمل‌ونقل است؛ OrganizationId، ChildId، UserId و resource ID فقط selector هستند. Application رابطه، نقش فعال و context جاری را دوباره resolve می‌کند.
- GuardianRelation مجوز خواندن Direct خصوصی کودک نیست؛ School Manager نیز private chat browsing ندارد. Minor و Unknown در عملیات حساس Messenger محافظت می‌شوند؛ UserId به‌تنهایی مجوز ارتباط نیست.
- مسیرهای `/api/admin/v1` از نقش Manager مدرسه جدا هستند؛ Platform OTP/Session و permission جاری دارد؛ عامل دوم حذف شده است.
- POST را کور retry نکنید؛ شناسهٔ idempotency، payload و rowversion همان use case باید رعایت شوند. هدر عمومی Idempotency-Key وجود ندارد.
- statusهای جدول فقط پاسخ‌های منتشرشده در OpenAPI هستند؛ خطای domain و gateهای pipeline می‌توانند پاسخ‌های بیشتری داشته باشند.
- زمان سرور و UTC معتبر است؛ concurrency token دودویی در JSON به شکل Base64 است. enum رشته‌ای است و باید unknown آینده تحمل شود.

[راهنمای جریان‌ها](backend-api-guide.md)، [قرارداد Backoffice](platform/BACKOFFICE_FRONTEND_HANDOFF.md)، [سیاست Child Safety](architecture/GAP_05_MESSENGER_CHILD_SAFETY.md)، [استقرار](production/DEPLOYMENT_IIS.md).

## رفتار جاری فراتر از schema

قرارداد OTP یکسان است ولی رفتار از Otp:Enabled/Otp:AllowMockAuthentication می‌آید: Production/Staging فقط true/false و Mock فقط در Development/Test/Testing با false/true. در Mock challenge واقعی ساخته نمی‌شود و مالکیت شماره ثابت نمی‌شود؛ نشست و مجوز منابع همچنان واقعی‌اند. راهنمای OTP و نمونه ورود را در [راهنمای بک‌اند](backend-api-guide.md#identity) بخوانید.

عملیات مدیریتی health/jobs/notifications همان routeها را دارند؛ PlatformJobOperations.Queues جزئیات صف را اضافه می‌کند و PlatformNotificationOperations شمارش/سن و failureهای اخیر را می‌دهد. FileCleanup صرفاً وضعیت Enabled/Disabled تنظیمی است. HTTP 200 عملیات مدیریتی به‌تنهایی به معنی سالم‌بودن همه dependencyها نیست.

AI v1 پاسخ را پیش از خروج از Backend به‌صورت strict و grounded بررسی می‌کند؛ 503 از Provider از جمله AI_MODEL_UNAVAILABLE به AI_SERVICE_UNAVAILABLE نگاشت می‌شود، body خام عبور نمی‌کند و retry خودکار POST وجود ندارد. Service Auth واقعیِ قبلی، Parent Provider Timeout و Full-12 Deferred در [وضعیت AI](ai-handoff/08_AI_BACKEND_IMPLEMENTATION_STATUS.md) تفکیک شده‌اند.

پروفایل self دارای DOB nullable و ProfilePhoto است؛ Directory/roster/Guardian consent و Class Lesson به مدرسه اضافه شده‌اند. request/response دقیق هر operation پایین از metadata فعلی آمده؛ سیاست‌های date-only/null، عکس Bearer، visibility Draft و رضایت والد در [راهنما](backend-api-guide.md) و [قرارداد مدرسه](product/SCHOOL_MANAGEMENT_FRONTEND_HANDOFF_FA.md) توضیح داده شده‌اند.

## فهرست Controllerها

| Controller | تعداد عملیات | سورس |
|---|---|---|
| AcademicPeriods | 2 | [AcademicPeriodsController](../MakanApp.Api/Controllers/AcademicPeriodsController.cs) |
| AdminAuditLogs | 1 | [AdminAuditLogsController](../MakanApp.Api/Controllers/AdminAuditLogsController.cs) |
| Ai | 12 | [AiController](../MakanApp.Api/Controllers/AiController.cs) |
| Assignments | 6 | [AssignmentsController](../MakanApp.Api/Controllers/AssignmentsController.cs) |
| Auth | 7 | [AuthController](../MakanApp.Api/Controllers/AuthController.cs) |
| ClassLessons | 5 | [ClassLessonsController](../MakanApp.Api/Controllers/ClassLessonsController.cs) |
| Classes | 8 | [ClassesController](../MakanApp.Api/Controllers/ClassesController.cs) |
| Commerce | 5 | [CommerceController](../MakanApp.Api/Controllers/CommerceController.cs) |
| CommunicationAge | 6 | [CommunicationAgeController](../MakanApp.Api/Controllers/CommunicationAgeController.cs) |
| Contacts | 5 | [ContactsController](../MakanApp.Api/Controllers/ContactsController.cs) |
| ConversationAdvancedManagement | 4 | [ConversationAdvancedManagementController](../MakanApp.Api/Controllers/ConversationAdvancedManagementController.cs) |
| ConversationChecklists | 4 | [ConversationChecklistsController](../MakanApp.Api/Controllers/ConversationChecklistsController.cs) |
| ConversationInvitations | 5 | [ConversationInvitationsController](../MakanApp.Api/Controllers/ConversationInvitationsController.cs) |
| ConversationInviteLinks | 9 | [ConversationInviteLinksController](../MakanApp.Api/Controllers/ConversationInviteLinksController.cs) |
| ConversationModeration | 6 | [ConversationModerationController](../MakanApp.Api/Controllers/ConversationModerationController.cs) |
| ConversationPolls | 5 | [ConversationPollsController](../MakanApp.Api/Controllers/ConversationPollsController.cs) |
| ConversationTopics | 3 | [ConversationTopicsController](../MakanApp.Api/Controllers/ConversationTopicsController.cs) |
| Conversations | 42 | [ConversationsController](../MakanApp.Api/Controllers/ConversationsController.cs) |
| Courses | 2 | [CoursesController](../MakanApp.Api/Controllers/CoursesController.cs) |
| Dashboards | 4 | [DashboardsController](../MakanApp.Api/Controllers/DashboardsController.cs) |
| Evaluations | 8 | [EvaluationsController](../MakanApp.Api/Controllers/EvaluationsController.cs) |
| ExamGradings | 9 | [ExamGradingsController](../MakanApp.Api/Controllers/ExamGradingsController.cs) |
| Exams | 21 | [ExamsController](../MakanApp.Api/Controllers/ExamsController.cs) |
| Files | 4 | [FilesController](../MakanApp.Api/Controllers/FilesController.cs) |
| Guardian | 3 | [GuardianController](../MakanApp.Api/Controllers/GuardianController.cs) |
| Invitations | 3 | [InvitationsController](../MakanApp.Api/Controllers/InvitationsController.cs) |
| MessageReports | 1 | [MessageReportsController](../MakanApp.Api/Controllers/MessageReportsController.cs) |
| Messages | 1 | [MessagesController](../MakanApp.Api/Controllers/MessagesController.cs) |
| MessagingFolders | 3 | [MessagingFoldersController](../MakanApp.Api/Controllers/MessagingFoldersController.cs) |
| MessagingPrivacy | 2 | [MessagingPrivacyController](../MakanApp.Api/Controllers/MessagingPrivacyController.cs) |
| MessagingSafety | 4 | [MessagingSafetyController](../MakanApp.Api/Controllers/MessagingSafetyController.cs) |
| NotificationDevices | 5 | [NotificationDevicesController](../MakanApp.Api/Controllers/NotificationDevicesController.cs) |
| NotificationPreferences | 2 | [NotificationPreferencesController](../MakanApp.Api/Controllers/NotificationPreferencesController.cs) |
| Notifications | 3 | [NotificationsController](../MakanApp.Api/Controllers/NotificationsController.cs) |
| OfficialClassConversations | 1 | [OfficialClassConversationsController](../MakanApp.Api/Controllers/OfficialClassConversationsController.cs) |
| OrganizationMembers | 1 | [OrganizationMembersController](../MakanApp.Api/Controllers/OrganizationMembersController.cs) |
| OrganizationPeople | 7 | [OrganizationPeopleController](../MakanApp.Api/Controllers/OrganizationPeopleController.cs) |
| People | 3 | [PeopleController](../MakanApp.Api/Controllers/PeopleController.cs) |
| PeoplePresence | 1 | [PeoplePresenceController](../MakanApp.Api/Controllers/PeoplePresenceController.cs) |
| PlatformCommerce | 7 | [PlatformCommerceController](../MakanApp.Api/Controllers/PlatformCommerceController.cs) |
| PlatformEducation | 6 | [PlatformEducationController](../MakanApp.Api/Controllers/PlatformEducationController.cs) |
| PlatformFinance | 2 | [PlatformFinanceController](../MakanApp.Api/Controllers/PlatformFinanceController.cs) |
| PlatformIam | 5 | [PlatformIamController](../MakanApp.Api/Controllers/PlatformIamController.cs) |
| PlatformMessagingMetadata | 1 | [PlatformMessagingMetadataController](../MakanApp.Api/Controllers/PlatformMessagingMetadataController.cs) |
| PlatformModeration | 9 | [PlatformModerationController](../MakanApp.Api/Controllers/PlatformModerationController.cs) |
| PlatformOrganizations | 10 | [PlatformOrganizationsController](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs) |
| PlatformProvisioning | 2 | [PlatformProvisioningController](../MakanApp.Api/Controllers/PlatformProvisioningController.cs) |
| PlatformSystemOperations | 3 | [PlatformSystemOperationsController](../MakanApp.Api/Controllers/PlatformSystemOperationsController.cs) |
| PlatformUsers | 6 | [PlatformUsersController](../MakanApp.Api/Controllers/PlatformUsersController.cs) |
| Profile | 4 | [ProfileController](../MakanApp.Api/Controllers/ProfileController.cs) |
| Reports | 19 | [ReportsController](../MakanApp.Api/Controllers/ReportsController.cs) |
| SavedMessageLabels | 2 | [SavedMessageLabelsController](../MakanApp.Api/Controllers/SavedMessageLabelsController.cs) |
| Schedule | 3 | [ScheduleController](../MakanApp.Api/Controllers/ScheduleController.cs) |
| ScheduledMessages | 3 | [ScheduledMessagesController](../MakanApp.Api/Controllers/ScheduledMessagesController.cs) |
| SchoolGuardianInvitations | 2 | [SchoolGuardianInvitationsController](../MakanApp.Api/Controllers/SchoolGuardianInvitationsController.cs) |
| SchoolGuardianRelations | 6 | [SchoolGuardianRelationsController](../MakanApp.Api/Controllers/SchoolGuardianRelationsController.cs) |
| Sessions | 8 | [SessionsController](../MakanApp.Api/Controllers/SessionsController.cs) |
| Submissions | 8 | [SubmissionsController](../MakanApp.Api/Controllers/SubmissionsController.cs) |
| Workspaces | 3 | [WorkspacesController](../MakanApp.Api/Controllers/WorkspacesController.cs) |

## AcademicPeriods

### GET /api/v1/academic/periods

کد واقعی: [AcademicPeriodsController.Get](../MakanApp.Api/Controllers/AcademicPeriodsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `Search` | query | خیر | string |
| `Page` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `status` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolPageOfAcademicPeriodResult](#contract-SchoolPageOfAcademicPeriodResult)<br>`application/json`: [SchoolPageOfAcademicPeriodResult](#contract-SchoolPageOfAcademicPeriodResult)<br>`text/json`: [SchoolPageOfAcademicPeriodResult](#contract-SchoolPageOfAcademicPeriodResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### POST /api/v1/academic/periods

کد واقعی: [AcademicPeriodsController.Create](../MakanApp.Api/Controllers/AcademicPeriodsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

بدنه درخواست: `application/json`: [CreateAcademicPeriodCommand](#contract-CreateAcademicPeriodCommand)<br>`text/json`: [CreateAcademicPeriodCommand](#contract-CreateAcademicPeriodCommand)<br>`application/*+json`: [CreateAcademicPeriodCommand](#contract-CreateAcademicPeriodCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [AcademicPeriodResult](#contract-AcademicPeriodResult)<br>`application/json`: [AcademicPeriodResult](#contract-AcademicPeriodResult)<br>`text/json`: [AcademicPeriodResult](#contract-AcademicPeriodResult) |


## AdminAuditLogs

### GET /api/admin/v1/audit-logs

کد واقعی: [AdminAuditLogsController.GetAuditLogs](../MakanApp.Api/Controllers/AdminAuditLogsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.audit.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformIamService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `actorUserId` | query | خیر | string / uuid |
| `action` | query | خیر | string |
| `targetType` | query | خیر | string |
| `targetId` | query | خیر | string |
| `result` | query | خیر | integer / int32 |
| `fromUtc` | query | خیر | string / date-time |
| `toUtc` | query | خیر | string / date-time |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |
| `sortBy` | query | خیر | string |
| `sortDirection` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AdminAuditLogPage](#contract-AdminAuditLogPage)<br>`application/json`: [AdminAuditLogPage](#contract-AdminAuditLogPage)<br>`text/json`: [AdminAuditLogPage](#contract-AdminAuditLogPage) |


## Ai

### POST /api/v1/ai/chat

کد واقعی: [AiController.Chat](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

بدنه درخواست: `application/json`: [SendAiChatCommand](#contract-SendAiChatCommand)<br>`text/json`: [SendAiChatCommand](#contract-SendAiChatCommand)<br>`application/*+json`: [SendAiChatCommand](#contract-SendAiChatCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiChatResult](#contract-AiChatResult)<br>`application/json`: [AiChatResult](#contract-AiChatResult)<br>`text/json`: [AiChatResult](#contract-AiChatResult) |

### POST /api/v1/ai/exams/{examId}/prepare

کد واقعی: [AiController.PrepareExam](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiExamPreparationResponse](#contract-AiExamPreparationResponse)<br>`application/json`: [AiExamPreparationResponse](#contract-AiExamPreparationResponse)<br>`text/json`: [AiExamPreparationResponse](#contract-AiExamPreparationResponse) |

### POST /api/v1/ai/homeworks/{assignmentId}/explain

کد واقعی: [AiController.ExplainHomework](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `assignmentId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [ExplainHomeworkCommand](#contract-ExplainHomeworkCommand)<br>`text/json`: [ExplainHomeworkCommand](#contract-ExplainHomeworkCommand)<br>`application/*+json`: [ExplainHomeworkCommand](#contract-ExplainHomeworkCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiHomeworkExplanationResponse](#contract-AiHomeworkExplanationResponse)<br>`application/json`: [AiHomeworkExplanationResponse](#contract-AiHomeworkExplanationResponse)<br>`text/json`: [AiHomeworkExplanationResponse](#contract-AiHomeworkExplanationResponse) |

### POST /api/v1/ai/managers/classes/{classId}/insight

کد واقعی: [AiController.GetManagerClassInsight](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse)<br>`application/json`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse)<br>`text/json`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse) |

### POST /api/v1/ai/managers/classes/{classId}/students/{studentId}/insight

کد واقعی: [AiController.GetManagerStudentInsight](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `studentId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse)<br>`application/json`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse)<br>`text/json`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse) |

### POST /api/v1/ai/managers/school-summary

کد واقعی: [AiController.SummarizeManagerSchool](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse)<br>`application/json`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse)<br>`text/json`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse) |

### POST /api/v1/ai/parents/child-summary

کد واقعی: [AiController.SummarizeChild](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiParentChildSummaryResponse](#contract-AiParentChildSummaryResponse)<br>`application/json`: [AiParentChildSummaryResponse](#contract-AiParentChildSummaryResponse)<br>`text/json`: [AiParentChildSummaryResponse](#contract-AiParentChildSummaryResponse) |

### POST /api/v1/ai/students/analyze

کد واقعی: [AiController.AnalyzeStudent](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiStudentAnalysisResponse](#contract-AiStudentAnalysisResponse)<br>`application/json`: [AiStudentAnalysisResponse](#contract-AiStudentAnalysisResponse)<br>`text/json`: [AiStudentAnalysisResponse](#contract-AiStudentAnalysisResponse) |

### POST /api/v1/ai/study-plan

کد واقعی: [AiController.CreateStudyPlan](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

بدنه درخواست: `application/json`: [CreateStudyPlanCommand](#contract-CreateStudyPlanCommand)<br>`text/json`: [CreateStudyPlanCommand](#contract-CreateStudyPlanCommand)<br>`application/*+json`: [CreateStudyPlanCommand](#contract-CreateStudyPlanCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiStudyPlanResponse](#contract-AiStudyPlanResponse)<br>`application/json`: [AiStudyPlanResponse](#contract-AiStudyPlanResponse)<br>`text/json`: [AiStudyPlanResponse](#contract-AiStudyPlanResponse) |

### POST /api/v1/ai/teachers/classes/{classId}/activity-suggestions

کد واقعی: [AiController.SuggestTeacherActivities](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SuggestTeacherActivitiesCommand](#contract-SuggestTeacherActivitiesCommand)<br>`text/json`: [SuggestTeacherActivitiesCommand](#contract-SuggestTeacherActivitiesCommand)<br>`application/*+json`: [SuggestTeacherActivitiesCommand](#contract-SuggestTeacherActivitiesCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiActivitySuggestionsResponse](#contract-AiActivitySuggestionsResponse)<br>`application/json`: [AiActivitySuggestionsResponse](#contract-AiActivitySuggestionsResponse)<br>`text/json`: [AiActivitySuggestionsResponse](#contract-AiActivitySuggestionsResponse) |

### POST /api/v1/ai/teachers/classes/{classId}/students/{studentId}/insight

کد واقعی: [AiController.GetTeacherStudentInsight](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `studentId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse)<br>`application/json`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse)<br>`text/json`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse) |

### POST /api/v1/ai/teachers/classes/{classId}/summary

کد واقعی: [AiController.SummarizeTeacherClass](../MakanApp.Api/Controllers/AiController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Ai.IAiChatService`، `MakanApp.Application.Ai.IAiRoleContextService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse)<br>`application/json`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse)<br>`text/json`: [AiRoleInsightResponse](#contract-AiRoleInsightResponse) |


## Assignments

### GET /api/v1/academic/assignments

کد واقعی: [AssignmentsController.GetAssignments](../MakanApp.Api/Controllers/AssignmentsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IAssignmentService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | query | خیر | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [AssignmentResult](#contract-AssignmentResult)<br>`application/json`: آرایه از [AssignmentResult](#contract-AssignmentResult)<br>`text/json`: آرایه از [AssignmentResult](#contract-AssignmentResult) |

### GET /api/v1/academic/assignments/{assignmentId}

کد واقعی: [AssignmentsController.Get](../MakanApp.Api/Controllers/AssignmentsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IAssignmentService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `assignmentId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AssignmentResult](#contract-AssignmentResult)<br>`application/json`: [AssignmentResult](#contract-AssignmentResult)<br>`text/json`: [AssignmentResult](#contract-AssignmentResult) |

### PATCH /api/v1/academic/assignments/{assignmentId}

کد واقعی: [AssignmentsController.UpdateDraft](../MakanApp.Api/Controllers/AssignmentsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IAssignmentService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `assignmentId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateAssignmentDraftCommand](#contract-UpdateAssignmentDraftCommand)<br>`text/json`: [UpdateAssignmentDraftCommand](#contract-UpdateAssignmentDraftCommand)<br>`application/*+json`: [UpdateAssignmentDraftCommand](#contract-UpdateAssignmentDraftCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AssignmentResult](#contract-AssignmentResult)<br>`application/json`: [AssignmentResult](#contract-AssignmentResult)<br>`text/json`: [AssignmentResult](#contract-AssignmentResult) |

### POST /api/v1/academic/assignments/{assignmentId}/publish

کد واقعی: [AssignmentsController.Publish](../MakanApp.Api/Controllers/AssignmentsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IAssignmentService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `assignmentId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PublishAssignmentCommand](#contract-PublishAssignmentCommand)<br>`text/json`: [PublishAssignmentCommand](#contract-PublishAssignmentCommand)<br>`application/*+json`: [PublishAssignmentCommand](#contract-PublishAssignmentCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PublishAssignmentResult](#contract-PublishAssignmentResult)<br>`application/json`: [PublishAssignmentResult](#contract-PublishAssignmentResult)<br>`text/json`: [PublishAssignmentResult](#contract-PublishAssignmentResult) |

### GET /api/v1/academic/classes/{classId}/assignments

کد واقعی: [AssignmentsController.GetForClass](../MakanApp.Api/Controllers/AssignmentsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IAssignmentService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [AssignmentResult](#contract-AssignmentResult)<br>`application/json`: آرایه از [AssignmentResult](#contract-AssignmentResult)<br>`text/json`: آرایه از [AssignmentResult](#contract-AssignmentResult) |

### POST /api/v1/academic/classes/{classId}/assignments

کد واقعی: [AssignmentsController.CreateDraft](../MakanApp.Api/Controllers/AssignmentsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IAssignmentService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateAssignmentDraftCommand](#contract-CreateAssignmentDraftCommand)<br>`text/json`: [CreateAssignmentDraftCommand](#contract-CreateAssignmentDraftCommand)<br>`application/*+json`: [CreateAssignmentDraftCommand](#contract-CreateAssignmentDraftCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [AssignmentResult](#contract-AssignmentResult)<br>`application/json`: [AssignmentResult](#contract-AssignmentResult)<br>`text/json`: [AssignmentResult](#contract-AssignmentResult) |


## Auth

### POST /api/v1/auth/logout

کد واقعی: [AuthController.Logout](../MakanApp.Api/Controllers/AuthController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 204 | No Content | بدون بدنهٔ منتشرشده |

### POST /api/v1/auth/otp/challenges

کد واقعی: [AuthController.RequestOtp](../MakanApp.Api/Controllers/AuthController.cs).

احراز هویت: عمومی از نظر ورود؛ validation، محدودیت درخواست و قواعد اختصاصی همچنان برقرارند.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`

بدنه درخواست: `application/json`: [RequestOtpCommand](#contract-RequestOtpCommand)<br>`text/json`: [RequestOtpCommand](#contract-RequestOtpCommand)<br>`application/*+json`: [RequestOtpCommand](#contract-RequestOtpCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 202 | Accepted | `text/plain`: [RequestOtpResult](#contract-RequestOtpResult)<br>`application/json`: [RequestOtpResult](#contract-RequestOtpResult)<br>`text/json`: [RequestOtpResult](#contract-RequestOtpResult) |

### POST /api/v1/auth/otp/verify

کد واقعی: [AuthController.VerifyOtp](../MakanApp.Api/Controllers/AuthController.cs).

احراز هویت: عمومی از نظر ورود؛ validation، محدودیت درخواست و قواعد اختصاصی همچنان برقرارند.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`

بدنه درخواست: `application/json`: [VerifyOtpCommand](#contract-VerifyOtpCommand)<br>`text/json`: [VerifyOtpCommand](#contract-VerifyOtpCommand)<br>`application/*+json`: [VerifyOtpCommand](#contract-VerifyOtpCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [VerifyOtpResult](#contract-VerifyOtpResult)<br>`application/json`: [VerifyOtpResult](#contract-VerifyOtpResult)<br>`text/json`: [VerifyOtpResult](#contract-VerifyOtpResult) |

### POST /api/v1/auth/refresh

کد واقعی: [AuthController.Refresh](../MakanApp.Api/Controllers/AuthController.cs).

احراز هویت: عمومی از نظر ورود؛ validation، محدودیت درخواست و قواعد اختصاصی همچنان برقرارند.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`

بدنه درخواست: `application/json`: [RefreshSessionCommand](#contract-RefreshSessionCommand)<br>`text/json`: [RefreshSessionCommand](#contract-RefreshSessionCommand)<br>`application/*+json`: [RefreshSessionCommand](#contract-RefreshSessionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [RefreshSessionResult](#contract-RefreshSessionResult)<br>`application/json`: [RefreshSessionResult](#contract-RefreshSessionResult)<br>`text/json`: [RefreshSessionResult](#contract-RefreshSessionResult) |

### GET /api/v1/auth/sessions

کد واقعی: [AuthController.GetSessions](../MakanApp.Api/Controllers/AuthController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [UserSessionResult](#contract-UserSessionResult)<br>`application/json`: آرایه از [UserSessionResult](#contract-UserSessionResult)<br>`text/json`: آرایه از [UserSessionResult](#contract-UserSessionResult) |

### POST /api/v1/auth/sessions/revoke-others

کد واقعی: [AuthController.RevokeOtherSessions](../MakanApp.Api/Controllers/AuthController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 204 | No Content | بدون بدنهٔ منتشرشده |

### DELETE /api/v1/auth/sessions/{sessionId}

کد واقعی: [AuthController.RevokeSession](../MakanApp.Api/Controllers/AuthController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `sessionId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 204 | No Content | بدون بدنهٔ منتشرشده |


## ClassLessons

### GET /api/v1/academic/classes/{classId}/lessons

کد واقعی: [ClassLessonsController.List](../MakanApp.Api/Controllers/ClassLessonsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.ClassLessonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `Search` | query | خیر | string |
| `Page` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolPageOfClassLessonResult](#contract-SchoolPageOfClassLessonResult)<br>`application/json`: [SchoolPageOfClassLessonResult](#contract-SchoolPageOfClassLessonResult)<br>`text/json`: [SchoolPageOfClassLessonResult](#contract-SchoolPageOfClassLessonResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 404 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 409 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 412 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### POST /api/v1/academic/classes/{classId}/lessons

کد واقعی: [ClassLessonsController.Create](../MakanApp.Api/Controllers/ClassLessonsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.ClassLessonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateClassLessonCommand](#contract-CreateClassLessonCommand)<br>`text/json`: [CreateClassLessonCommand](#contract-CreateClassLessonCommand)<br>`application/*+json`: [CreateClassLessonCommand](#contract-CreateClassLessonCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [ClassLessonResult](#contract-ClassLessonResult)<br>`application/json`: [ClassLessonResult](#contract-ClassLessonResult)<br>`text/json`: [ClassLessonResult](#contract-ClassLessonResult) |
| 200 | OK | `text/plain`: [ClassLessonResult](#contract-ClassLessonResult)<br>`application/json`: [ClassLessonResult](#contract-ClassLessonResult)<br>`text/json`: [ClassLessonResult](#contract-ClassLessonResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 404 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 409 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 412 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### GET /api/v1/academic/lessons/{lessonId}

کد واقعی: [ClassLessonsController.Get](../MakanApp.Api/Controllers/ClassLessonsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.ClassLessonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `lessonId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ClassLessonResult](#contract-ClassLessonResult)<br>`application/json`: [ClassLessonResult](#contract-ClassLessonResult)<br>`text/json`: [ClassLessonResult](#contract-ClassLessonResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 404 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 409 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 412 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### PATCH /api/v1/academic/lessons/{lessonId}

کد واقعی: [ClassLessonsController.Update](../MakanApp.Api/Controllers/ClassLessonsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.ClassLessonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `lessonId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateClassLessonCommand](#contract-UpdateClassLessonCommand)<br>`text/json`: [UpdateClassLessonCommand](#contract-UpdateClassLessonCommand)<br>`application/*+json`: [UpdateClassLessonCommand](#contract-UpdateClassLessonCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ClassLessonResult](#contract-ClassLessonResult)<br>`application/json`: [ClassLessonResult](#contract-ClassLessonResult)<br>`text/json`: [ClassLessonResult](#contract-ClassLessonResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 404 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 409 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 412 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### POST /api/v1/academic/lessons/{lessonId}/publish

کد واقعی: [ClassLessonsController.Publish](../MakanApp.Api/Controllers/ClassLessonsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.ClassLessonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `lessonId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PublishClassLessonCommand](#contract-PublishClassLessonCommand)<br>`text/json`: [PublishClassLessonCommand](#contract-PublishClassLessonCommand)<br>`application/*+json`: [PublishClassLessonCommand](#contract-PublishClassLessonCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ClassLessonResult](#contract-ClassLessonResult)<br>`application/json`: [ClassLessonResult](#contract-ClassLessonResult)<br>`text/json`: [ClassLessonResult](#contract-ClassLessonResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 404 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 409 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 412 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |


## Classes

### GET /api/v1/academic/classes

کد واقعی: [ClassesController.GetClasses](../MakanApp.Api/Controllers/ClassesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [ClassResult](#contract-ClassResult)<br>`application/json`: آرایه از [ClassResult](#contract-ClassResult)<br>`text/json`: آرایه از [ClassResult](#contract-ClassResult) |

### POST /api/v1/academic/classes

کد واقعی: [ClassesController.Create](../MakanApp.Api/Controllers/ClassesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

بدنه درخواست: `application/json`: [CreateClassCommand](#contract-CreateClassCommand)<br>`text/json`: [CreateClassCommand](#contract-CreateClassCommand)<br>`application/*+json`: [CreateClassCommand](#contract-CreateClassCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [ClassResult](#contract-ClassResult)<br>`application/json`: [ClassResult](#contract-ClassResult)<br>`text/json`: [ClassResult](#contract-ClassResult) |

### POST /api/v1/academic/classes/{classId}/enrollments

کد واقعی: [ClassesController.EnrollLearner](../MakanApp.Api/Controllers/ClassesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [EnrollLearnerCommand](#contract-EnrollLearnerCommand)<br>`text/json`: [EnrollLearnerCommand](#contract-EnrollLearnerCommand)<br>`application/*+json`: [EnrollLearnerCommand](#contract-EnrollLearnerCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [EnrollmentResult](#contract-EnrollmentResult)<br>`application/json`: [EnrollmentResult](#contract-EnrollmentResult)<br>`text/json`: [EnrollmentResult](#contract-EnrollmentResult) |

### POST /api/v1/academic/classes/{classId}/enrollments/{enrollmentId}/end

کد واقعی: [ClassesController.EndEnrollment](../MakanApp.Api/Controllers/ClassesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `enrollmentId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [EndEnrollmentCommand](#contract-EndEnrollmentCommand)<br>`text/json`: [EndEnrollmentCommand](#contract-EndEnrollmentCommand)<br>`application/*+json`: [EndEnrollmentCommand](#contract-EndEnrollmentCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [EnrollmentResult](#contract-EnrollmentResult)<br>`application/json`: [EnrollmentResult](#contract-EnrollmentResult)<br>`text/json`: [EnrollmentResult](#contract-EnrollmentResult) |

### GET /api/v1/academic/classes/{classId}/learners

کد واقعی: [ClassesController.GetLearners](../MakanApp.Api/Controllers/ClassesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `Search` | query | خیر | string |
| `Page` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `status` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolPageOfClassLearnerResult](#contract-SchoolPageOfClassLearnerResult)<br>`application/json`: [SchoolPageOfClassLearnerResult](#contract-SchoolPageOfClassLearnerResult)<br>`text/json`: [SchoolPageOfClassLearnerResult](#contract-SchoolPageOfClassLearnerResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 404 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### GET /api/v1/academic/classes/{classId}/teachers

کد واقعی: [ClassesController.GetTeachers](../MakanApp.Api/Controllers/ClassesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `Search` | query | خیر | string |
| `Page` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `status` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolPageOfClassTeacherResult](#contract-SchoolPageOfClassTeacherResult)<br>`application/json`: [SchoolPageOfClassTeacherResult](#contract-SchoolPageOfClassTeacherResult)<br>`text/json`: [SchoolPageOfClassTeacherResult](#contract-SchoolPageOfClassTeacherResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 404 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### POST /api/v1/academic/classes/{classId}/teachers

کد واقعی: [ClassesController.AssignTeacher](../MakanApp.Api/Controllers/ClassesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [AssignTeacherCommand](#contract-AssignTeacherCommand)<br>`text/json`: [AssignTeacherCommand](#contract-AssignTeacherCommand)<br>`application/*+json`: [AssignTeacherCommand](#contract-AssignTeacherCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [TeacherAssignmentResult](#contract-TeacherAssignmentResult)<br>`application/json`: [TeacherAssignmentResult](#contract-TeacherAssignmentResult)<br>`text/json`: [TeacherAssignmentResult](#contract-TeacherAssignmentResult) |

### POST /api/v1/academic/classes/{classId}/teachers/{teacherAssignmentId}/end

کد واقعی: [ClassesController.EndTeacherAssignment](../MakanApp.Api/Controllers/ClassesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `teacherAssignmentId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [TeacherAssignmentResult](#contract-TeacherAssignmentResult)<br>`application/json`: [TeacherAssignmentResult](#contract-TeacherAssignmentResult)<br>`text/json`: [TeacherAssignmentResult](#contract-TeacherAssignmentResult) |


## Commerce

### GET /api/v1/commerce/orders

کد واقعی: [CommerceController.ListOrders](../MakanApp.Api/Controllers/CommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Commerce.ICommerceService`، `MakanApp.Application.Commerce.IOrderPaymentService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `limit` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [OrderListResult](#contract-OrderListResult)<br>`application/json`: [OrderListResult](#contract-OrderListResult)<br>`text/json`: [OrderListResult](#contract-OrderListResult) |

### POST /api/v1/commerce/orders

کد واقعی: [CommerceController.CreateOrder](../MakanApp.Api/Controllers/CommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Commerce.ICommerceService`، `MakanApp.Application.Commerce.IOrderPaymentService`

بدنه درخواست: `application/json`: [CreateOrderCommand](#contract-CreateOrderCommand)<br>`text/json`: [CreateOrderCommand](#contract-CreateOrderCommand)<br>`application/*+json`: [CreateOrderCommand](#contract-CreateOrderCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [CreateOrderResult](#contract-CreateOrderResult)<br>`application/json`: [CreateOrderResult](#contract-CreateOrderResult)<br>`text/json`: [CreateOrderResult](#contract-CreateOrderResult) |
| 201 | Created | `text/plain`: [CreateOrderResult](#contract-CreateOrderResult)<br>`application/json`: [CreateOrderResult](#contract-CreateOrderResult)<br>`text/json`: [CreateOrderResult](#contract-CreateOrderResult) |

### GET /api/v1/commerce/orders/{orderId}

کد واقعی: [CommerceController.GetOrder](../MakanApp.Api/Controllers/CommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Commerce.ICommerceService`، `MakanApp.Application.Commerce.IOrderPaymentService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `orderId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [OrderDto](#contract-OrderDto)<br>`application/json`: [OrderDto](#contract-OrderDto)<br>`text/json`: [OrderDto](#contract-OrderDto) |

### POST /api/v1/commerce/orders/{orderId}/payments

کد واقعی: [CommerceController.InitiatePayment](../MakanApp.Api/Controllers/CommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Commerce.ICommerceService`، `MakanApp.Application.Commerce.IOrderPaymentService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `orderId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [InitiatePaymentCommand](#contract-InitiatePaymentCommand)<br>`text/json`: [InitiatePaymentCommand](#contract-InitiatePaymentCommand)<br>`application/*+json`: [InitiatePaymentCommand](#contract-InitiatePaymentCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [InitiatePaymentResult](#contract-InitiatePaymentResult)<br>`application/json`: [InitiatePaymentResult](#contract-InitiatePaymentResult)<br>`text/json`: [InitiatePaymentResult](#contract-InitiatePaymentResult) |
| 201 | Created | `text/plain`: [InitiatePaymentResult](#contract-InitiatePaymentResult)<br>`application/json`: [InitiatePaymentResult](#contract-InitiatePaymentResult)<br>`text/json`: [InitiatePaymentResult](#contract-InitiatePaymentResult) |

### GET /api/v1/commerce/summary

کد واقعی: [CommerceController.GetSummary](../MakanApp.Api/Controllers/CommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Commerce.ICommerceService`، `MakanApp.Application.Commerce.IOrderPaymentService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagerCommerceSummaryDto](#contract-ManagerCommerceSummaryDto)<br>`application/json`: [ManagerCommerceSummaryDto](#contract-ManagerCommerceSummaryDto)<br>`text/json`: [ManagerCommerceSummaryDto](#contract-ManagerCommerceSummaryDto) |


## CommunicationAge

### GET /api/admin/v1/users/{userId}/communication-age

کد واقعی: [CommunicationAgeController.GetPlatform](../MakanApp.Api/Controllers/CommunicationAgeController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.users.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.CommunicationAgeService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [CommunicationAgeResult](#contract-CommunicationAgeResult)<br>`application/json`: [CommunicationAgeResult](#contract-CommunicationAgeResult)<br>`text/json`: [CommunicationAgeResult](#contract-CommunicationAgeResult) |

### POST /api/admin/v1/users/{userId}/communication-age/resolve

کد واقعی: [CommunicationAgeController.Resolve](../MakanApp.Api/Controllers/CommunicationAgeController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.users.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.CommunicationAgeService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [VerifyCommunicationAgeCommand](#contract-VerifyCommunicationAgeCommand)<br>`text/json`: [VerifyCommunicationAgeCommand](#contract-VerifyCommunicationAgeCommand)<br>`application/*+json`: [VerifyCommunicationAgeCommand](#contract-VerifyCommunicationAgeCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [CommunicationAgeResult](#contract-CommunicationAgeResult)<br>`application/json`: [CommunicationAgeResult](#contract-CommunicationAgeResult)<br>`text/json`: [CommunicationAgeResult](#contract-CommunicationAgeResult) |

### POST /api/admin/v1/users/{userId}/communication-age/verifications

کد واقعی: [CommunicationAgeController.VerifyInitial](../MakanApp.Api/Controllers/CommunicationAgeController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.users.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.CommunicationAgeService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [VerifyCommunicationAgeCommand](#contract-VerifyCommunicationAgeCommand)<br>`text/json`: [VerifyCommunicationAgeCommand](#contract-VerifyCommunicationAgeCommand)<br>`application/*+json`: [VerifyCommunicationAgeCommand](#contract-VerifyCommunicationAgeCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [CommunicationAgeResult](#contract-CommunicationAgeResult)<br>`application/json`: [CommunicationAgeResult](#contract-CommunicationAgeResult)<br>`text/json`: [CommunicationAgeResult](#contract-CommunicationAgeResult) |

### GET /api/v1/me/messaging-status

کد واقعی: [CommunicationAgeController.GetSelfStatus](../MakanApp.Api/Controllers/CommunicationAgeController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.CommunicationAgeService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MessagingStatusResult](#contract-MessagingStatusResult)<br>`application/json`: [MessagingStatusResult](#contract-MessagingStatusResult)<br>`text/json`: [MessagingStatusResult](#contract-MessagingStatusResult) |

### GET /api/v1/organization/people/{personId}/communication-age

کد واقعی: [CommunicationAgeController.Get](../MakanApp.Api/Controllers/CommunicationAgeController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.CommunicationAgeService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `personId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [CommunicationAgeResult](#contract-CommunicationAgeResult)<br>`application/json`: [CommunicationAgeResult](#contract-CommunicationAgeResult)<br>`text/json`: [CommunicationAgeResult](#contract-CommunicationAgeResult) |

### POST /api/v1/organization/people/{personId}/communication-age/verify

کد واقعی: [CommunicationAgeController.Verify](../MakanApp.Api/Controllers/CommunicationAgeController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.CommunicationAgeService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `personId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [VerifyCommunicationAgeCommand](#contract-VerifyCommunicationAgeCommand)<br>`text/json`: [VerifyCommunicationAgeCommand](#contract-VerifyCommunicationAgeCommand)<br>`application/*+json`: [VerifyCommunicationAgeCommand](#contract-VerifyCommunicationAgeCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [CommunicationAgeResult](#contract-CommunicationAgeResult)<br>`application/json`: [CommunicationAgeResult](#contract-CommunicationAgeResult)<br>`text/json`: [CommunicationAgeResult](#contract-CommunicationAgeResult) |


## Contacts

### GET /api/v1/contacts

کد واقعی: [ContactsController.GetMine](../MakanApp.Api/Controllers/ContactsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingDirectoryService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `q` | query | خیر | string |
| `favoritesOnly` | query | خیر | boolean |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [UserContactPageResult](#contract-UserContactPageResult)<br>`application/json`: [UserContactPageResult](#contract-UserContactPageResult)<br>`text/json`: [UserContactPageResult](#contract-UserContactPageResult) |

### POST /api/v1/contacts

کد واقعی: [ContactsController.Save](../MakanApp.Api/Controllers/ContactsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingDirectoryService`

بدنه درخواست: `application/json`: [SaveUserContactCommand](#contract-SaveUserContactCommand)<br>`text/json`: [SaveUserContactCommand](#contract-SaveUserContactCommand)<br>`application/*+json`: [SaveUserContactCommand](#contract-SaveUserContactCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [UserContactResult](#contract-UserContactResult)<br>`application/json`: [UserContactResult](#contract-UserContactResult)<br>`text/json`: [UserContactResult](#contract-UserContactResult) |
| 201 | Created | `text/plain`: [UserContactResult](#contract-UserContactResult)<br>`application/json`: [UserContactResult](#contract-UserContactResult)<br>`text/json`: [UserContactResult](#contract-UserContactResult) |

### GET /api/v1/contacts/recent

کد واقعی: [ContactsController.GetRecent](../MakanApp.Api/Controllers/ContactsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingDirectoryService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [RecentContactPageResult](#contract-RecentContactPageResult)<br>`application/json`: [RecentContactPageResult](#contract-RecentContactPageResult)<br>`text/json`: [RecentContactPageResult](#contract-RecentContactPageResult) |

### DELETE /api/v1/contacts/{contactUserId}

کد واقعی: [ContactsController.Delete](../MakanApp.Api/Controllers/ContactsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingDirectoryService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `contactUserId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 204 | No Content | بدون بدنهٔ منتشرشده |

### PATCH /api/v1/contacts/{contactUserId}

کد واقعی: [ContactsController.Update](../MakanApp.Api/Controllers/ContactsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingDirectoryService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `contactUserId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateUserContactCommand](#contract-UpdateUserContactCommand)<br>`text/json`: [UpdateUserContactCommand](#contract-UpdateUserContactCommand)<br>`application/*+json`: [UpdateUserContactCommand](#contract-UpdateUserContactCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [UserContactResult](#contract-UserContactResult)<br>`application/json`: [UserContactResult](#contract-UserContactResult)<br>`text/json`: [UserContactResult](#contract-UserContactResult) |


## ConversationAdvancedManagement

### PATCH /api/v1/conversations/{conversationId}/info

کد واقعی: [ConversationAdvancedManagementController.UpdateInfo](../MakanApp.Api/Controllers/ConversationAdvancedManagementController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateConversationInfoCommand](#contract-UpdateConversationInfoCommand)<br>`text/json`: [UpdateConversationInfoCommand](#contract-UpdateConversationInfoCommand)<br>`application/*+json`: [UpdateConversationInfoCommand](#contract-UpdateConversationInfoCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationInfoResult](#contract-ConversationInfoResult)<br>`application/json`: [ConversationInfoResult](#contract-ConversationInfoResult)<br>`text/json`: [ConversationInfoResult](#contract-ConversationInfoResult) |

### GET /api/v1/conversations/{conversationId}/me/restriction

کد واقعی: [ConversationAdvancedManagementController.GetOwnRestriction](../MakanApp.Api/Controllers/ConversationAdvancedManagementController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MemberRestrictionResult](#contract-MemberRestrictionResult)<br>`application/json`: [MemberRestrictionResult](#contract-MemberRestrictionResult)<br>`text/json`: [MemberRestrictionResult](#contract-MemberRestrictionResult) |

### GET /api/v1/conversations/{conversationId}/member-restrictions

کد واقعی: [ConversationAdvancedManagementController.GetRestrictions](../MakanApp.Api/Controllers/ConversationAdvancedManagementController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MemberRestrictionPageResult](#contract-MemberRestrictionPageResult)<br>`application/json`: [MemberRestrictionPageResult](#contract-MemberRestrictionPageResult)<br>`text/json`: [MemberRestrictionPageResult](#contract-MemberRestrictionPageResult) |

### PUT /api/v1/conversations/{conversationId}/members/{userId}/restriction

کد واقعی: [ConversationAdvancedManagementController.Restrict](../MakanApp.Api/Controllers/ConversationAdvancedManagementController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `userId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SetMemberRestrictionCommand](#contract-SetMemberRestrictionCommand)<br>`text/json`: [SetMemberRestrictionCommand](#contract-SetMemberRestrictionCommand)<br>`application/*+json`: [SetMemberRestrictionCommand](#contract-SetMemberRestrictionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MemberRestrictionResult](#contract-MemberRestrictionResult)<br>`application/json`: [MemberRestrictionResult](#contract-MemberRestrictionResult)<br>`text/json`: [MemberRestrictionResult](#contract-MemberRestrictionResult) |


## ConversationChecklists

### POST /api/v1/conversations/{conversationId}/checklists

کد واقعی: [ConversationChecklistsController.Create](../MakanApp.Api/Controllers/ConversationChecklistsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationChecklistService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateConversationChecklistCommand](#contract-CreateConversationChecklistCommand)<br>`text/json`: [CreateConversationChecklistCommand](#contract-CreateConversationChecklistCommand)<br>`application/*+json`: [CreateConversationChecklistCommand](#contract-CreateConversationChecklistCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MessageReceiptResult](#contract-MessageReceiptResult)<br>`application/json`: [MessageReceiptResult](#contract-MessageReceiptResult)<br>`text/json`: [MessageReceiptResult](#contract-MessageReceiptResult) |

### GET /api/v1/conversations/{conversationId}/messages/{messageId}/checklist

کد واقعی: [ConversationChecklistsController.Get](../MakanApp.Api/Controllers/ConversationChecklistsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationChecklistService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationChecklistResult](#contract-ConversationChecklistResult)<br>`application/json`: [ConversationChecklistResult](#contract-ConversationChecklistResult)<br>`text/json`: [ConversationChecklistResult](#contract-ConversationChecklistResult) |

### PUT /api/v1/conversations/{conversationId}/messages/{messageId}/checklist

کد واقعی: [ConversationChecklistsController.Update](../MakanApp.Api/Controllers/ConversationChecklistsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationChecklistService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateConversationChecklistCommand](#contract-UpdateConversationChecklistCommand)<br>`text/json`: [UpdateConversationChecklistCommand](#contract-UpdateConversationChecklistCommand)<br>`application/*+json`: [UpdateConversationChecklistCommand](#contract-UpdateConversationChecklistCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationChecklistResult](#contract-ConversationChecklistResult)<br>`application/json`: [ConversationChecklistResult](#contract-ConversationChecklistResult)<br>`text/json`: [ConversationChecklistResult](#contract-ConversationChecklistResult) |

### PUT /api/v1/conversations/{conversationId}/messages/{messageId}/checklist/items/{itemId}

کد واقعی: [ConversationChecklistsController.SetItem](../MakanApp.Api/Controllers/ConversationChecklistsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationChecklistService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |
| `itemId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SetChecklistItemCommand](#contract-SetChecklistItemCommand)<br>`text/json`: [SetChecklistItemCommand](#contract-SetChecklistItemCommand)<br>`application/*+json`: [SetChecklistItemCommand](#contract-SetChecklistItemCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationChecklistResult](#contract-ConversationChecklistResult)<br>`application/json`: [ConversationChecklistResult](#contract-ConversationChecklistResult)<br>`text/json`: [ConversationChecklistResult](#contract-ConversationChecklistResult) |


## ConversationInvitations

### POST /api/v1/conversations/{conversationId}/invitations

کد واقعی: [ConversationInvitationsController.Invite](../MakanApp.Api/Controllers/ConversationInvitationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationInvitationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateConversationInvitationCommand](#contract-CreateConversationInvitationCommand)<br>`text/json`: [CreateConversationInvitationCommand](#contract-CreateConversationInvitationCommand)<br>`application/*+json`: [CreateConversationInvitationCommand](#contract-CreateConversationInvitationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationInvitationResult](#contract-ConversationInvitationResult)<br>`application/json`: [ConversationInvitationResult](#contract-ConversationInvitationResult)<br>`text/json`: [ConversationInvitationResult](#contract-ConversationInvitationResult) |

### GET /api/v1/messaging/invitations

کد واقعی: [ConversationInvitationsController.List](../MakanApp.Api/Controllers/ConversationInvitationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationInvitationService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [ConversationInvitationResult](#contract-ConversationInvitationResult)<br>`application/json`: آرایه از [ConversationInvitationResult](#contract-ConversationInvitationResult)<br>`text/json`: آرایه از [ConversationInvitationResult](#contract-ConversationInvitationResult) |

### POST /api/v1/messaging/invitations/{invitationId}/accept

کد واقعی: [ConversationInvitationsController.Accept](../MakanApp.Api/Controllers/ConversationInvitationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationInvitationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `invitationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationInvitationResult](#contract-ConversationInvitationResult)<br>`application/json`: [ConversationInvitationResult](#contract-ConversationInvitationResult)<br>`text/json`: [ConversationInvitationResult](#contract-ConversationInvitationResult) |

### POST /api/v1/messaging/invitations/{invitationId}/decline

کد واقعی: [ConversationInvitationsController.Decline](../MakanApp.Api/Controllers/ConversationInvitationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationInvitationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `invitationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationInvitationResult](#contract-ConversationInvitationResult)<br>`application/json`: [ConversationInvitationResult](#contract-ConversationInvitationResult)<br>`text/json`: [ConversationInvitationResult](#contract-ConversationInvitationResult) |

### POST /api/v1/messaging/invitations/{invitationId}/revoke

کد واقعی: [ConversationInvitationsController.Revoke](../MakanApp.Api/Controllers/ConversationInvitationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationInvitationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `invitationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationInvitationResult](#contract-ConversationInvitationResult)<br>`application/json`: [ConversationInvitationResult](#contract-ConversationInvitationResult)<br>`text/json`: [ConversationInvitationResult](#contract-ConversationInvitationResult) |


## ConversationInviteLinks

### GET /api/v1/conversations/{conversationId}/invite-links

کد واقعی: [ConversationInviteLinksController.List](../MakanApp.Api/Controllers/ConversationInviteLinksController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [InviteLinkPageResult](#contract-InviteLinkPageResult)<br>`application/json`: [InviteLinkPageResult](#contract-InviteLinkPageResult)<br>`text/json`: [InviteLinkPageResult](#contract-InviteLinkPageResult) |

### POST /api/v1/conversations/{conversationId}/invite-links

کد واقعی: [ConversationInviteLinksController.Create](../MakanApp.Api/Controllers/ConversationInviteLinksController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateInviteLinkCommand](#contract-CreateInviteLinkCommand)<br>`text/json`: [CreateInviteLinkCommand](#contract-CreateInviteLinkCommand)<br>`application/*+json`: [CreateInviteLinkCommand](#contract-CreateInviteLinkCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [CreatedInviteLinkResult](#contract-CreatedInviteLinkResult)<br>`application/json`: [CreatedInviteLinkResult](#contract-CreatedInviteLinkResult)<br>`text/json`: [CreatedInviteLinkResult](#contract-CreatedInviteLinkResult) |

### POST /api/v1/conversations/{conversationId}/invite-links/{linkId}/revoke

کد واقعی: [ConversationInviteLinksController.Revoke](../MakanApp.Api/Controllers/ConversationInviteLinksController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `linkId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [RevokeInviteLinkCommand](#contract-RevokeInviteLinkCommand)<br>`text/json`: [RevokeInviteLinkCommand](#contract-RevokeInviteLinkCommand)<br>`application/*+json`: [RevokeInviteLinkCommand](#contract-RevokeInviteLinkCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [InviteLinkResult](#contract-InviteLinkResult)<br>`application/json`: [InviteLinkResult](#contract-InviteLinkResult)<br>`text/json`: [InviteLinkResult](#contract-InviteLinkResult) |

### GET /api/v1/conversations/{conversationId}/join-requests

کد واقعی: [ConversationInviteLinksController.ListRequests](../MakanApp.Api/Controllers/ConversationInviteLinksController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationJoinRequestPageResult](#contract-ConversationJoinRequestPageResult)<br>`application/json`: [ConversationJoinRequestPageResult](#contract-ConversationJoinRequestPageResult)<br>`text/json`: [ConversationJoinRequestPageResult](#contract-ConversationJoinRequestPageResult) |

### POST /api/v1/messaging/invite-links/redeem

کد واقعی: [ConversationInviteLinksController.Redeem](../MakanApp.Api/Controllers/ConversationInviteLinksController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

بدنه درخواست: `application/json`: [RedeemInviteLinkCommand](#contract-RedeemInviteLinkCommand)<br>`text/json`: [RedeemInviteLinkCommand](#contract-RedeemInviteLinkCommand)<br>`application/*+json`: [RedeemInviteLinkCommand](#contract-RedeemInviteLinkCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [InviteLinkRedemptionResult](#contract-InviteLinkRedemptionResult)<br>`application/json`: [InviteLinkRedemptionResult](#contract-InviteLinkRedemptionResult)<br>`text/json`: [InviteLinkRedemptionResult](#contract-InviteLinkRedemptionResult) |

### GET /api/v1/messaging/join-requests

کد واقعی: [ConversationInviteLinksController.MyRequests](../MakanApp.Api/Controllers/ConversationInviteLinksController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationJoinRequestPageResult](#contract-ConversationJoinRequestPageResult)<br>`application/json`: [ConversationJoinRequestPageResult](#contract-ConversationJoinRequestPageResult)<br>`text/json`: [ConversationJoinRequestPageResult](#contract-ConversationJoinRequestPageResult) |

### POST /api/v1/messaging/join-requests/{requestId}/approve

کد واقعی: [ConversationInviteLinksController.Approve](../MakanApp.Api/Controllers/ConversationInviteLinksController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `requestId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CompleteJoinRequestCommand](#contract-CompleteJoinRequestCommand)<br>`text/json`: [CompleteJoinRequestCommand](#contract-CompleteJoinRequestCommand)<br>`application/*+json`: [CompleteJoinRequestCommand](#contract-CompleteJoinRequestCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationJoinRequestResult2](#contract-ConversationJoinRequestResult2)<br>`application/json`: [ConversationJoinRequestResult2](#contract-ConversationJoinRequestResult2)<br>`text/json`: [ConversationJoinRequestResult2](#contract-ConversationJoinRequestResult2) |

### POST /api/v1/messaging/join-requests/{requestId}/cancel

کد واقعی: [ConversationInviteLinksController.Cancel](../MakanApp.Api/Controllers/ConversationInviteLinksController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `requestId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CompleteJoinRequestCommand](#contract-CompleteJoinRequestCommand)<br>`text/json`: [CompleteJoinRequestCommand](#contract-CompleteJoinRequestCommand)<br>`application/*+json`: [CompleteJoinRequestCommand](#contract-CompleteJoinRequestCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationJoinRequestResult2](#contract-ConversationJoinRequestResult2)<br>`application/json`: [ConversationJoinRequestResult2](#contract-ConversationJoinRequestResult2)<br>`text/json`: [ConversationJoinRequestResult2](#contract-ConversationJoinRequestResult2) |

### POST /api/v1/messaging/join-requests/{requestId}/reject

کد واقعی: [ConversationInviteLinksController.Reject](../MakanApp.Api/Controllers/ConversationInviteLinksController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationAdvancedManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `requestId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CompleteJoinRequestCommand](#contract-CompleteJoinRequestCommand)<br>`text/json`: [CompleteJoinRequestCommand](#contract-CompleteJoinRequestCommand)<br>`application/*+json`: [CompleteJoinRequestCommand](#contract-CompleteJoinRequestCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationJoinRequestResult2](#contract-ConversationJoinRequestResult2)<br>`application/json`: [ConversationJoinRequestResult2](#contract-ConversationJoinRequestResult2)<br>`text/json`: [ConversationJoinRequestResult2](#contract-ConversationJoinRequestResult2) |


## ConversationModeration

### GET /api/v1/conversations/{conversationId}/moderation/cases

کد واقعی: [ConversationModerationController.GetCases](../MakanApp.Api/Controllers/ConversationModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IModerationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `status` | query | خیر | string |
| `priority` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ModerationCasePageResult](#contract-ModerationCasePageResult)<br>`application/json`: [ModerationCasePageResult](#contract-ModerationCasePageResult)<br>`text/json`: [ModerationCasePageResult](#contract-ModerationCasePageResult) |

### GET /api/v1/conversations/{conversationId}/moderation/cases/{caseId}

کد واقعی: [ConversationModerationController.GetCase](../MakanApp.Api/Controllers/ConversationModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IModerationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `caseId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult)<br>`application/json`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult)<br>`text/json`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult) |

### POST /api/v1/conversations/{conversationId}/moderation/cases/{caseId}/dismiss

کد واقعی: [ConversationModerationController.Dismiss](../MakanApp.Api/Controllers/ConversationModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IModerationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `caseId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CompleteModerationCaseCommand](#contract-CompleteModerationCaseCommand)<br>`text/json`: [CompleteModerationCaseCommand](#contract-CompleteModerationCaseCommand)<br>`application/*+json`: [CompleteModerationCaseCommand](#contract-CompleteModerationCaseCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult)<br>`application/json`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult)<br>`text/json`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult) |

### GET /api/v1/conversations/{conversationId}/moderation/cases/{caseId}/evidence/files/{fileAssetId}

کد واقعی: [ConversationModerationController.DownloadEvidence](../MakanApp.Api/Controllers/ConversationModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IModerationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `caseId` | path | بله | string / uuid |
| `fileAssetId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | بدون بدنهٔ منتشرشده |

### POST /api/v1/conversations/{conversationId}/moderation/cases/{caseId}/resolve

کد واقعی: [ConversationModerationController.Resolve](../MakanApp.Api/Controllers/ConversationModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IModerationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `caseId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CompleteModerationCaseCommand](#contract-CompleteModerationCaseCommand)<br>`text/json`: [CompleteModerationCaseCommand](#contract-CompleteModerationCaseCommand)<br>`application/*+json`: [CompleteModerationCaseCommand](#contract-CompleteModerationCaseCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult)<br>`application/json`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult)<br>`text/json`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult) |

### POST /api/v1/conversations/{conversationId}/moderation/cases/{caseId}/start-review

کد واقعی: [ConversationModerationController.StartReview](../MakanApp.Api/Controllers/ConversationModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IModerationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `caseId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [StartModerationReviewCommand](#contract-StartModerationReviewCommand)<br>`text/json`: [StartModerationReviewCommand](#contract-StartModerationReviewCommand)<br>`application/*+json`: [StartModerationReviewCommand](#contract-StartModerationReviewCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult)<br>`application/json`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult)<br>`text/json`: [ModerationCaseDetailResult](#contract-ModerationCaseDetailResult) |


## ConversationPolls

### GET /api/v1/conversations/{conversationId}/messages/{messageId}/poll

کد واقعی: [ConversationPollsController.Get](../MakanApp.Api/Controllers/ConversationPollsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationPollService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationPollResult](#contract-ConversationPollResult)<br>`application/json`: [ConversationPollResult](#contract-ConversationPollResult)<br>`text/json`: [ConversationPollResult](#contract-ConversationPollResult) |

### POST /api/v1/conversations/{conversationId}/messages/{messageId}/poll/close

کد واقعی: [ConversationPollsController.Close](../MakanApp.Api/Controllers/ConversationPollsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationPollService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CloseConversationPollCommand](#contract-CloseConversationPollCommand)<br>`text/json`: [CloseConversationPollCommand](#contract-CloseConversationPollCommand)<br>`application/*+json`: [CloseConversationPollCommand](#contract-CloseConversationPollCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationPollResult](#contract-ConversationPollResult)<br>`application/json`: [ConversationPollResult](#contract-ConversationPollResult)<br>`text/json`: [ConversationPollResult](#contract-ConversationPollResult) |

### POST /api/v1/conversations/{conversationId}/messages/{messageId}/poll/vote

کد واقعی: [ConversationPollsController.Vote](../MakanApp.Api/Controllers/ConversationPollsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationPollService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [VoteConversationPollCommand](#contract-VoteConversationPollCommand)<br>`text/json`: [VoteConversationPollCommand](#contract-VoteConversationPollCommand)<br>`application/*+json`: [VoteConversationPollCommand](#contract-VoteConversationPollCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationPollResult](#contract-ConversationPollResult)<br>`application/json`: [ConversationPollResult](#contract-ConversationPollResult)<br>`text/json`: [ConversationPollResult](#contract-ConversationPollResult) |

### GET /api/v1/conversations/{conversationId}/messages/{messageId}/poll/voters

کد واقعی: [ConversationPollsController.Voters](../MakanApp.Api/Controllers/ConversationPollsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationPollService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |
| `optionNumber` | query | خیر | integer / int32 |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationPollVoterPageResult](#contract-ConversationPollVoterPageResult)<br>`application/json`: [ConversationPollVoterPageResult](#contract-ConversationPollVoterPageResult)<br>`text/json`: [ConversationPollVoterPageResult](#contract-ConversationPollVoterPageResult) |

### POST /api/v1/conversations/{conversationId}/polls

کد واقعی: [ConversationPollsController.Create](../MakanApp.Api/Controllers/ConversationPollsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationPollService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateConversationPollCommand](#contract-CreateConversationPollCommand)<br>`text/json`: [CreateConversationPollCommand](#contract-CreateConversationPollCommand)<br>`application/*+json`: [CreateConversationPollCommand](#contract-CreateConversationPollCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MessageReceiptResult](#contract-MessageReceiptResult)<br>`application/json`: [MessageReceiptResult](#contract-MessageReceiptResult)<br>`text/json`: [MessageReceiptResult](#contract-MessageReceiptResult) |


## ConversationTopics

### GET /api/v1/conversations/{conversationId}/topics

کد واقعی: [ConversationTopicsController.Get](../MakanApp.Api/Controllers/ConversationTopicsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationTopicService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationTopicPageResult](#contract-ConversationTopicPageResult)<br>`application/json`: [ConversationTopicPageResult](#contract-ConversationTopicPageResult)<br>`text/json`: [ConversationTopicPageResult](#contract-ConversationTopicPageResult) |

### POST /api/v1/conversations/{conversationId}/topics

کد واقعی: [ConversationTopicsController.Create](../MakanApp.Api/Controllers/ConversationTopicsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationTopicService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateConversationTopicCommand](#contract-CreateConversationTopicCommand)<br>`text/json`: [CreateConversationTopicCommand](#contract-CreateConversationTopicCommand)<br>`application/*+json`: [CreateConversationTopicCommand](#contract-CreateConversationTopicCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationTopicResult](#contract-ConversationTopicResult)<br>`application/json`: [ConversationTopicResult](#contract-ConversationTopicResult)<br>`text/json`: [ConversationTopicResult](#contract-ConversationTopicResult) |

### PUT /api/v1/conversations/{conversationId}/topics/{topicId}

کد واقعی: [ConversationTopicsController.Update](../MakanApp.Api/Controllers/ConversationTopicsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ConversationTopicService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `topicId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateConversationTopicCommand](#contract-UpdateConversationTopicCommand)<br>`text/json`: [UpdateConversationTopicCommand](#contract-UpdateConversationTopicCommand)<br>`application/*+json`: [UpdateConversationTopicCommand](#contract-UpdateConversationTopicCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationTopicResult](#contract-ConversationTopicResult)<br>`application/json`: [ConversationTopicResult](#contract-ConversationTopicResult)<br>`text/json`: [ConversationTopicResult](#contract-ConversationTopicResult) |


## Conversations

### GET /api/v1/conversations

کد واقعی: [ConversationsController.GetMine](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `filter` | query | خیر | string / All, Direct, Group, Channel, Unread, Archived, Saved |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |
| `q` | query | خیر | string |
| `folderId` | query | خیر | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [ConversationSummaryResult](#contract-ConversationSummaryResult)<br>`application/json`: آرایه از [ConversationSummaryResult](#contract-ConversationSummaryResult)<br>`text/json`: آرایه از [ConversationSummaryResult](#contract-ConversationSummaryResult) |

### POST /api/v1/conversations/channels

کد واقعی: [ConversationsController.CreateChannel](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

بدنه درخواست: `application/json`: [CreateManagedConversationCommand](#contract-CreateManagedConversationCommand)<br>`text/json`: [CreateManagedConversationCommand](#contract-CreateManagedConversationCommand)<br>`application/*+json`: [CreateManagedConversationCommand](#contract-CreateManagedConversationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |
| 201 | Created | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |

### POST /api/v1/conversations/direct

کد واقعی: [ConversationsController.StartDirect](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

بدنه درخواست: `application/json`: [StartDirectConversationCommand](#contract-StartDirectConversationCommand)<br>`text/json`: [StartDirectConversationCommand](#contract-StartDirectConversationCommand)<br>`application/*+json`: [StartDirectConversationCommand](#contract-StartDirectConversationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [DirectConversationResult](#contract-DirectConversationResult)<br>`application/json`: [DirectConversationResult](#contract-DirectConversationResult)<br>`text/json`: [DirectConversationResult](#contract-DirectConversationResult) |
| 201 | Created | `text/plain`: [DirectConversationResult](#contract-DirectConversationResult)<br>`application/json`: [DirectConversationResult](#contract-DirectConversationResult)<br>`text/json`: [DirectConversationResult](#contract-DirectConversationResult) |

### GET /api/v1/conversations/discover

کد واقعی: [ConversationsController.Discover](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `type` | query | خیر | string |
| `q` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [DiscoverableConversationPageResult](#contract-DiscoverableConversationPageResult)<br>`application/json`: [DiscoverableConversationPageResult](#contract-DiscoverableConversationPageResult)<br>`text/json`: [DiscoverableConversationPageResult](#contract-DiscoverableConversationPageResult) |

### POST /api/v1/conversations/groups

کد واقعی: [ConversationsController.CreateGroup](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

بدنه درخواست: `application/json`: [CreateManagedConversationCommand](#contract-CreateManagedConversationCommand)<br>`text/json`: [CreateManagedConversationCommand](#contract-CreateManagedConversationCommand)<br>`application/*+json`: [CreateManagedConversationCommand](#contract-CreateManagedConversationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |
| 201 | Created | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |

### GET /api/v1/conversations/saved

کد واقعی: [ConversationsController.GetSaved](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SavedConversationResult](#contract-SavedConversationResult)<br>`application/json`: [SavedConversationResult](#contract-SavedConversationResult)<br>`text/json`: [SavedConversationResult](#contract-SavedConversationResult) |
| 201 | Created | `text/plain`: [SavedConversationResult](#contract-SavedConversationResult)<br>`application/json`: [SavedConversationResult](#contract-SavedConversationResult)<br>`text/json`: [SavedConversationResult](#contract-SavedConversationResult) |

### GET /api/v1/conversations/{conversationId}

کد واقعی: [ConversationsController.GetDetails](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |

### DELETE /api/v1/conversations/{conversationId}/archive

کد واقعی: [ConversationsController.Unarchive](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`application/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`text/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult) |

### POST /api/v1/conversations/{conversationId}/archive

کد واقعی: [ConversationsController.Archive](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`application/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`text/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult) |

### GET /api/v1/conversations/{conversationId}/changes

کد واقعی: [ConversationsController.GetChanges](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `afterCursor` | query | خیر | string |
| `limit` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationChangePageResult](#contract-ConversationChangePageResult)<br>`application/json`: [ConversationChangePageResult](#contract-ConversationChangePageResult)<br>`text/json`: [ConversationChangePageResult](#contract-ConversationChangePageResult) |

### POST /api/v1/conversations/{conversationId}/clear-history

کد واقعی: [ConversationsController.ClearHistory](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`application/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`text/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult) |

### POST /api/v1/conversations/{conversationId}/delivered

کد واقعی: [ConversationsController.AcknowledgeDelivery](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [AdvanceConversationCursorCommand](#contract-AdvanceConversationCursorCommand)<br>`text/json`: [AdvanceConversationCursorCommand](#contract-AdvanceConversationCursorCommand)<br>`application/*+json`: [AdvanceConversationCursorCommand](#contract-AdvanceConversationCursorCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationCursorStateResult](#contract-ConversationCursorStateResult)<br>`application/json`: [ConversationCursorStateResult](#contract-ConversationCursorStateResult)<br>`text/json`: [ConversationCursorStateResult](#contract-ConversationCursorStateResult) |

### POST /api/v1/conversations/{conversationId}/join

کد واقعی: [ConversationsController.JoinPublic](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |

### POST /api/v1/conversations/{conversationId}/leave

کد واقعی: [ConversationsController.Leave](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |

### POST /api/v1/conversations/{conversationId}/mark-unread

کد واقعی: [ConversationsController.MarkUnread](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`application/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`text/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult) |

### GET /api/v1/conversations/{conversationId}/media

کد واقعی: [ConversationsController.GetMedia](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `kind` | query | خیر | string |
| `beforeSequence` | query | خیر | integer / int64 |
| `limit` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationMediaPageResult](#contract-ConversationMediaPageResult)<br>`application/json`: [ConversationMediaPageResult](#contract-ConversationMediaPageResult)<br>`text/json`: [ConversationMediaPageResult](#contract-ConversationMediaPageResult) |

### GET /api/v1/conversations/{conversationId}/members

کد واقعی: [ConversationsController.GetMembers](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationParticipantPageResult](#contract-ConversationParticipantPageResult)<br>`application/json`: [ConversationParticipantPageResult](#contract-ConversationParticipantPageResult)<br>`text/json`: [ConversationParticipantPageResult](#contract-ConversationParticipantPageResult) |

### POST /api/v1/conversations/{conversationId}/members

کد واقعی: [ConversationsController.AddMember](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [AddConversationMemberCommand](#contract-AddConversationMemberCommand)<br>`text/json`: [AddConversationMemberCommand](#contract-AddConversationMemberCommand)<br>`application/*+json`: [AddConversationMemberCommand](#contract-AddConversationMemberCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |

### DELETE /api/v1/conversations/{conversationId}/members/{targetUserId}

کد واقعی: [ConversationsController.RemoveMember](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `targetUserId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |

### PATCH /api/v1/conversations/{conversationId}/members/{targetUserId}/permissions

کد واقعی: [ConversationsController.UpdateMemberPermissions](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `targetUserId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateConversationPermissionsCommand](#contract-UpdateConversationPermissionsCommand)<br>`text/json`: [UpdateConversationPermissionsCommand](#contract-UpdateConversationPermissionsCommand)<br>`application/*+json`: [UpdateConversationPermissionsCommand](#contract-UpdateConversationPermissionsCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |

### PATCH /api/v1/conversations/{conversationId}/members/{targetUserId}/role

کد واقعی: [ConversationsController.ChangeMemberRole](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `targetUserId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [ChangeConversationMemberRoleCommand](#contract-ChangeConversationMemberRoleCommand)<br>`text/json`: [ChangeConversationMemberRoleCommand](#contract-ChangeConversationMemberRoleCommand)<br>`application/*+json`: [ChangeConversationMemberRoleCommand](#contract-ChangeConversationMemberRoleCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |

### GET /api/v1/conversations/{conversationId}/messages

کد واقعی: [ConversationsController.GetMessages](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `beforeSequence` | query | خیر | integer / int64 |
| `limit` | query | خیر | integer / int32 |
| `topicId` | query | خیر | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationMessagePageResult](#contract-ConversationMessagePageResult)<br>`application/json`: [ConversationMessagePageResult](#contract-ConversationMessagePageResult)<br>`text/json`: [ConversationMessagePageResult](#contract-ConversationMessagePageResult) |

### POST /api/v1/conversations/{conversationId}/messages

کد واقعی: [ConversationsController.SendMessage](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SendMessageCommand](#contract-SendMessageCommand)<br>`text/json`: [SendMessageCommand](#contract-SendMessageCommand)<br>`application/*+json`: [SendMessageCommand](#contract-SendMessageCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 429 | Too Many Requests | `text/plain`: [ProblemDetails](#contract-ProblemDetails)<br>`application/json`: [ProblemDetails](#contract-ProblemDetails)<br>`text/json`: [ProblemDetails](#contract-ProblemDetails) |
| 200 | OK | `text/plain`: [MessageReceiptResult](#contract-MessageReceiptResult)<br>`application/json`: [MessageReceiptResult](#contract-MessageReceiptResult)<br>`text/json`: [MessageReceiptResult](#contract-MessageReceiptResult) |

### DELETE /api/v1/conversations/{conversationId}/messages/{messageId}

کد واقعی: [ConversationsController.DeleteMessage](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |
| `expectedVersion` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MessageMutationResult](#contract-MessageMutationResult)<br>`application/json`: [MessageMutationResult](#contract-MessageMutationResult)<br>`text/json`: [MessageMutationResult](#contract-MessageMutationResult) |

### PATCH /api/v1/conversations/{conversationId}/messages/{messageId}

کد واقعی: [ConversationsController.EditMessage](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [EditMessageCommand](#contract-EditMessageCommand)<br>`text/json`: [EditMessageCommand](#contract-EditMessageCommand)<br>`application/*+json`: [EditMessageCommand](#contract-EditMessageCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 429 | Too Many Requests | `text/plain`: [ProblemDetails](#contract-ProblemDetails)<br>`application/json`: [ProblemDetails](#contract-ProblemDetails)<br>`text/json`: [ProblemDetails](#contract-ProblemDetails) |
| 200 | OK | `text/plain`: [MessageMutationResult](#contract-MessageMutationResult)<br>`application/json`: [MessageMutationResult](#contract-MessageMutationResult)<br>`text/json`: [MessageMutationResult](#contract-MessageMutationResult) |

### DELETE /api/v1/conversations/{conversationId}/messages/{messageId}/for-everyone

کد واقعی: [ConversationsController.DeleteMessageForEveryone](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |
| `expectedVersion` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MessageMutationResult](#contract-MessageMutationResult)<br>`application/json`: [MessageMutationResult](#contract-MessageMutationResult)<br>`text/json`: [MessageMutationResult](#contract-MessageMutationResult) |

### DELETE /api/v1/conversations/{conversationId}/messages/{messageId}/for-me

کد واقعی: [ConversationsController.DeleteMessageForMe](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [DeleteMessageForMeResult](#contract-DeleteMessageForMeResult)<br>`application/json`: [DeleteMessageForMeResult](#contract-DeleteMessageForMeResult)<br>`text/json`: [DeleteMessageForMeResult](#contract-DeleteMessageForMeResult) |

### POST /api/v1/conversations/{conversationId}/messages/{messageId}/forward

کد واقعی: [ConversationsController.ForwardMessage](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [ForwardMessageCommand](#contract-ForwardMessageCommand)<br>`text/json`: [ForwardMessageCommand](#contract-ForwardMessageCommand)<br>`application/*+json`: [ForwardMessageCommand](#contract-ForwardMessageCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 429 | Too Many Requests | `text/plain`: [ProblemDetails](#contract-ProblemDetails)<br>`application/json`: [ProblemDetails](#contract-ProblemDetails)<br>`text/json`: [ProblemDetails](#contract-ProblemDetails) |
| 200 | OK | `text/plain`: [MessageReceiptResult](#contract-MessageReceiptResult)<br>`application/json`: [MessageReceiptResult](#contract-MessageReceiptResult)<br>`text/json`: [MessageReceiptResult](#contract-MessageReceiptResult) |

### POST /api/v1/conversations/{conversationId}/messages/{messageId}/reactions

کد واقعی: [ConversationsController.AddReaction](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [AddReactionCommand](#contract-AddReactionCommand)<br>`text/json`: [AddReactionCommand](#contract-AddReactionCommand)<br>`application/*+json`: [AddReactionCommand](#contract-AddReactionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 429 | Too Many Requests | `text/plain`: [ProblemDetails](#contract-ProblemDetails)<br>`application/json`: [ProblemDetails](#contract-ProblemDetails)<br>`text/json`: [ProblemDetails](#contract-ProblemDetails) |
| 200 | OK | `text/plain`: [MessageReactionResult](#contract-MessageReactionResult)<br>`application/json`: [MessageReactionResult](#contract-MessageReactionResult)<br>`text/json`: [MessageReactionResult](#contract-MessageReactionResult) |

### DELETE /api/v1/conversations/{conversationId}/messages/{messageId}/reactions/{reaction}

کد واقعی: [ConversationsController.RemoveReaction](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |
| `reaction` | path | بله | [MessageReactionType](#contract-MessageReactionType) |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 429 | Too Many Requests | `text/plain`: [ProblemDetails](#contract-ProblemDetails)<br>`application/json`: [ProblemDetails](#contract-ProblemDetails)<br>`text/json`: [ProblemDetails](#contract-ProblemDetails) |
| 204 | No Content | بدون بدنهٔ منتشرشده |

### DELETE /api/v1/conversations/{conversationId}/mute

کد واقعی: [ConversationsController.Unmute](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`application/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`text/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult) |

### PUT /api/v1/conversations/{conversationId}/mute

کد واقعی: [ConversationsController.SetMute](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SetConversationMuteCommand](#contract-SetConversationMuteCommand)<br>`text/json`: [SetConversationMuteCommand](#contract-SetConversationMuteCommand)<br>`application/*+json`: [SetConversationMuteCommand](#contract-SetConversationMuteCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`application/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`text/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult) |

### POST /api/v1/conversations/{conversationId}/ownership-transfers

کد واقعی: [ConversationsController.StartOwnershipTransfer](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [StartOwnershipTransferCommand](#contract-StartOwnershipTransferCommand)<br>`text/json`: [StartOwnershipTransferCommand](#contract-StartOwnershipTransferCommand)<br>`application/*+json`: [StartOwnershipTransferCommand](#contract-StartOwnershipTransferCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [OwnershipTransferResult](#contract-OwnershipTransferResult)<br>`application/json`: [OwnershipTransferResult](#contract-OwnershipTransferResult)<br>`text/json`: [OwnershipTransferResult](#contract-OwnershipTransferResult) |

### POST /api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/accept

کد واقعی: [ConversationsController.AcceptOwnershipTransfer](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `transferId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [OwnershipTransferResult](#contract-OwnershipTransferResult)<br>`application/json`: [OwnershipTransferResult](#contract-OwnershipTransferResult)<br>`text/json`: [OwnershipTransferResult](#contract-OwnershipTransferResult) |

### POST /api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/decline

کد واقعی: [ConversationsController.DeclineOwnershipTransfer](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `transferId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [OwnershipTransferResult](#contract-OwnershipTransferResult)<br>`application/json`: [OwnershipTransferResult](#contract-OwnershipTransferResult)<br>`text/json`: [OwnershipTransferResult](#contract-OwnershipTransferResult) |

### DELETE /api/v1/conversations/{conversationId}/pins/{messageId}

کد واقعی: [ConversationsController.UnpinMessage](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationPinResult](#contract-ConversationPinResult)<br>`application/json`: [ConversationPinResult](#contract-ConversationPinResult)<br>`text/json`: [ConversationPinResult](#contract-ConversationPinResult) |

### POST /api/v1/conversations/{conversationId}/pins/{messageId}

کد واقعی: [ConversationsController.PinMessage](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationPinResult](#contract-ConversationPinResult)<br>`application/json`: [ConversationPinResult](#contract-ConversationPinResult)<br>`text/json`: [ConversationPinResult](#contract-ConversationPinResult) |

### POST /api/v1/conversations/{conversationId}/read

کد واقعی: [ConversationsController.MarkRead](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [AdvanceConversationCursorCommand](#contract-AdvanceConversationCursorCommand)<br>`text/json`: [AdvanceConversationCursorCommand](#contract-AdvanceConversationCursorCommand)<br>`application/*+json`: [AdvanceConversationCursorCommand](#contract-AdvanceConversationCursorCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationCursorStateResult](#contract-ConversationCursorStateResult)<br>`application/json`: [ConversationCursorStateResult](#contract-ConversationCursorStateResult)<br>`text/json`: [ConversationCursorStateResult](#contract-ConversationCursorStateResult) |

### GET /api/v1/conversations/{conversationId}/read-states

کد واقعی: [ConversationsController.GetReadStates](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationPeerReadStateResult](#contract-ConversationPeerReadStateResult)<br>`application/json`: [ConversationPeerReadStateResult](#contract-ConversationPeerReadStateResult)<br>`text/json`: [ConversationPeerReadStateResult](#contract-ConversationPeerReadStateResult) |
| 404 | Not Found | `text/plain`: [ProblemDetails](#contract-ProblemDetails)<br>`application/json`: [ProblemDetails](#contract-ProblemDetails)<br>`text/json`: [ProblemDetails](#contract-ProblemDetails) |

### PATCH /api/v1/conversations/{conversationId}/settings

کد واقعی: [ConversationsController.UpdateSettings](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateConversationSettingsCommand](#contract-UpdateConversationSettingsCommand)<br>`text/json`: [UpdateConversationSettingsCommand](#contract-UpdateConversationSettingsCommand)<br>`application/*+json`: [UpdateConversationSettingsCommand](#contract-UpdateConversationSettingsCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`application/json`: [ManagedConversationResult](#contract-ManagedConversationResult)<br>`text/json`: [ManagedConversationResult](#contract-ManagedConversationResult) |

### DELETE /api/v1/conversations/{conversationId}/state/pin

کد واقعی: [ConversationsController.UnpinConversation](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`application/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`text/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult) |

### POST /api/v1/conversations/{conversationId}/state/pin

کد واقعی: [ConversationsController.PinConversation](../MakanApp.Api/Controllers/ConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingService`، `MakanApp.Application.Messaging.IConversationManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`application/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult)<br>`text/json`: [ConversationUserStateResult](#contract-ConversationUserStateResult) |


## Courses

### GET /api/v1/academic/courses

کد واقعی: [CoursesController.Get](../MakanApp.Api/Controllers/CoursesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `Search` | query | خیر | string |
| `Page` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `status` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolPageOfCourseResult](#contract-SchoolPageOfCourseResult)<br>`application/json`: [SchoolPageOfCourseResult](#contract-SchoolPageOfCourseResult)<br>`text/json`: [SchoolPageOfCourseResult](#contract-SchoolPageOfCourseResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### POST /api/v1/academic/courses

کد واقعی: [CoursesController.Create](../MakanApp.Api/Controllers/CoursesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicService`، `MakanApp.Application.Academic.SchoolReadService`

بدنه درخواست: `application/json`: [CreateCourseCommand](#contract-CreateCourseCommand)<br>`text/json`: [CreateCourseCommand](#contract-CreateCourseCommand)<br>`application/*+json`: [CreateCourseCommand](#contract-CreateCourseCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [CourseResult](#contract-CourseResult)<br>`application/json`: [CourseResult](#contract-CourseResult)<br>`text/json`: [CourseResult](#contract-CourseResult) |


## Dashboards

### GET /api/v1/dashboards/manager

کد واقعی: [DashboardsController.GetManager](../MakanApp.Api/Controllers/DashboardsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Dashboards.IDashboardService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagerDashboardDto](#contract-ManagerDashboardDto)<br>`application/json`: [ManagerDashboardDto](#contract-ManagerDashboardDto)<br>`text/json`: [ManagerDashboardDto](#contract-ManagerDashboardDto) |

### GET /api/v1/dashboards/parent

کد واقعی: [DashboardsController.GetParent](../MakanApp.Api/Controllers/DashboardsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Dashboards.IDashboardService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ParentDashboardDto](#contract-ParentDashboardDto)<br>`application/json`: [ParentDashboardDto](#contract-ParentDashboardDto)<br>`text/json`: [ParentDashboardDto](#contract-ParentDashboardDto) |

### GET /api/v1/dashboards/student

کد واقعی: [DashboardsController.GetStudent](../MakanApp.Api/Controllers/DashboardsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Dashboards.IDashboardService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [StudentDashboardDto](#contract-StudentDashboardDto)<br>`application/json`: [StudentDashboardDto](#contract-StudentDashboardDto)<br>`text/json`: [StudentDashboardDto](#contract-StudentDashboardDto) |

### GET /api/v1/dashboards/teacher

کد واقعی: [DashboardsController.GetTeacher](../MakanApp.Api/Controllers/DashboardsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Dashboards.IDashboardService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [TeacherDashboardDto](#contract-TeacherDashboardDto)<br>`application/json`: [TeacherDashboardDto](#contract-TeacherDashboardDto)<br>`text/json`: [TeacherDashboardDto](#contract-TeacherDashboardDto) |


## Evaluations

### GET /api/v1/academic/evaluations/queue

کد واقعی: [EvaluationsController.GetQueue](../MakanApp.Api/Controllers/EvaluationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IEvaluationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `assignmentId` | query | خیر | string / uuid |
| `classId` | query | خیر | string / uuid |
| `reviewStatus` | query | خیر | string |
| `isLate` | query | خیر | boolean |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [EvaluationQueueItemResult](#contract-EvaluationQueueItemResult)<br>`application/json`: آرایه از [EvaluationQueueItemResult](#contract-EvaluationQueueItemResult)<br>`text/json`: آرایه از [EvaluationQueueItemResult](#contract-EvaluationQueueItemResult) |

### GET /api/v1/academic/submission-attempts/{attemptId}/evaluation

کد واقعی: [EvaluationsController.GetEvaluation](../MakanApp.Api/Controllers/EvaluationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IEvaluationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [EvaluatorEvaluationResult](#contract-EvaluatorEvaluationResult)<br>`application/json`: [EvaluatorEvaluationResult](#contract-EvaluatorEvaluationResult)<br>`text/json`: [EvaluatorEvaluationResult](#contract-EvaluatorEvaluationResult) |

### PUT /api/v1/academic/submission-attempts/{attemptId}/evaluation

کد واقعی: [EvaluationsController.SaveDraft](../MakanApp.Api/Controllers/EvaluationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IEvaluationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SaveEvaluationDraftCommand](#contract-SaveEvaluationDraftCommand)<br>`text/json`: [SaveEvaluationDraftCommand](#contract-SaveEvaluationDraftCommand)<br>`application/*+json`: [SaveEvaluationDraftCommand](#contract-SaveEvaluationDraftCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [EvaluatorEvaluationResult](#contract-EvaluatorEvaluationResult)<br>`application/json`: [EvaluatorEvaluationResult](#contract-EvaluatorEvaluationResult)<br>`text/json`: [EvaluatorEvaluationResult](#contract-EvaluatorEvaluationResult) |

### POST /api/v1/academic/submission-attempts/{attemptId}/evaluation/corrections

کد واقعی: [EvaluationsController.Correct](../MakanApp.Api/Controllers/EvaluationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IEvaluationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CorrectReleasedEvaluationCommand](#contract-CorrectReleasedEvaluationCommand)<br>`text/json`: [CorrectReleasedEvaluationCommand](#contract-CorrectReleasedEvaluationCommand)<br>`application/*+json`: [CorrectReleasedEvaluationCommand](#contract-CorrectReleasedEvaluationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [EvaluatorEvaluationResult](#contract-EvaluatorEvaluationResult)<br>`application/json`: [EvaluatorEvaluationResult](#contract-EvaluatorEvaluationResult)<br>`text/json`: [EvaluatorEvaluationResult](#contract-EvaluatorEvaluationResult) |

### POST /api/v1/academic/submission-attempts/{attemptId}/evaluation/release

کد واقعی: [EvaluationsController.Release](../MakanApp.Api/Controllers/EvaluationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IEvaluationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [ReleaseEvaluationCommand](#contract-ReleaseEvaluationCommand)<br>`text/json`: [ReleaseEvaluationCommand](#contract-ReleaseEvaluationCommand)<br>`application/*+json`: [ReleaseEvaluationCommand](#contract-ReleaseEvaluationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [GradeReleaseResult](#contract-GradeReleaseResult)<br>`application/json`: [GradeReleaseResult](#contract-GradeReleaseResult)<br>`text/json`: [GradeReleaseResult](#contract-GradeReleaseResult) |

### GET /api/v1/academic/submission-attempts/{attemptId}/evaluation/submission

کد واقعی: [EvaluationsController.GetSubmission](../MakanApp.Api/Controllers/EvaluationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IEvaluationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SubmissionForEvaluationResult](#contract-SubmissionForEvaluationResult)<br>`application/json`: [SubmissionForEvaluationResult](#contract-SubmissionForEvaluationResult)<br>`text/json`: [SubmissionForEvaluationResult](#contract-SubmissionForEvaluationResult) |

### GET /api/v1/academic/submission-attempts/{attemptId}/guardian-result

کد واقعی: [EvaluationsController.GetParentResult](../MakanApp.Api/Controllers/EvaluationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IEvaluationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ParentReleasedResult](#contract-ParentReleasedResult)<br>`application/json`: [ParentReleasedResult](#contract-ParentReleasedResult)<br>`text/json`: [ParentReleasedResult](#contract-ParentReleasedResult) |

### GET /api/v1/academic/submission-attempts/{attemptId}/result

کد واقعی: [EvaluationsController.GetStudentResult](../MakanApp.Api/Controllers/EvaluationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IEvaluationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [StudentReleasedResult](#contract-StudentReleasedResult)<br>`application/json`: [StudentReleasedResult](#contract-StudentReleasedResult)<br>`text/json`: [StudentReleasedResult](#contract-StudentReleasedResult) |


## ExamGradings

### POST /api/v1/academic/exam-attempts/{attemptId}/grade/corrections

کد واقعی: [ExamGradingsController.Correct](../MakanApp.Api/Controllers/ExamGradingsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamGradingService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CorrectReleasedExamGradeCommand](#contract-CorrectReleasedExamGradeCommand)<br>`text/json`: [CorrectReleasedExamGradeCommand](#contract-CorrectReleasedExamGradeCommand)<br>`application/*+json`: [CorrectReleasedExamGradeCommand](#contract-CorrectReleasedExamGradeCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [ExamGradeDto](#contract-ExamGradeDto)<br>`application/json`: [ExamGradeDto](#contract-ExamGradeDto)<br>`text/json`: [ExamGradeDto](#contract-ExamGradeDto) |

### POST /api/v1/academic/exam-attempts/{attemptId}/grade/release

کد واقعی: [ExamGradingsController.Release](../MakanApp.Api/Controllers/ExamGradingsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamGradingService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [ReleaseExamGradeCommand](#contract-ReleaseExamGradeCommand)<br>`text/json`: [ReleaseExamGradeCommand](#contract-ReleaseExamGradeCommand)<br>`application/*+json`: [ReleaseExamGradeCommand](#contract-ReleaseExamGradeCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamGradeReleaseDto](#contract-ExamGradeReleaseDto)<br>`application/json`: [ExamGradeReleaseDto](#contract-ExamGradeReleaseDto)<br>`text/json`: [ExamGradeReleaseDto](#contract-ExamGradeReleaseDto) |

### GET /api/v1/academic/exam-attempts/{attemptId}/grading

کد واقعی: [ExamGradingsController.GetForGrading](../MakanApp.Api/Controllers/ExamGradingsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamGradingService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamGradeDto](#contract-ExamGradeDto)<br>`application/json`: [ExamGradeDto](#contract-ExamGradeDto)<br>`text/json`: [ExamGradeDto](#contract-ExamGradeDto) |

### POST /api/v1/academic/exam-attempts/{attemptId}/grading

کد واقعی: [ExamGradingsController.Initialize](../MakanApp.Api/Controllers/ExamGradingsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamGradingService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamGradeDto](#contract-ExamGradeDto)<br>`application/json`: [ExamGradeDto](#contract-ExamGradeDto)<br>`text/json`: [ExamGradeDto](#contract-ExamGradeDto) |

### POST /api/v1/academic/exam-attempts/{attemptId}/grading/complete

کد واقعی: [ExamGradingsController.Complete](../MakanApp.Api/Controllers/ExamGradingsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamGradingService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CompleteExamGradeCommand](#contract-CompleteExamGradeCommand)<br>`text/json`: [CompleteExamGradeCommand](#contract-CompleteExamGradeCommand)<br>`application/*+json`: [CompleteExamGradeCommand](#contract-CompleteExamGradeCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamGradeDto](#contract-ExamGradeDto)<br>`application/json`: [ExamGradeDto](#contract-ExamGradeDto)<br>`text/json`: [ExamGradeDto](#contract-ExamGradeDto) |

### PATCH /api/v1/academic/exam-attempts/{attemptId}/grading/questions/{attemptQuestionId}

کد واقعی: [ExamGradingsController.GradeQuestion](../MakanApp.Api/Controllers/ExamGradingsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamGradingService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |
| `attemptQuestionId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [GradeExamQuestionCommand](#contract-GradeExamQuestionCommand)<br>`text/json`: [GradeExamQuestionCommand](#contract-GradeExamQuestionCommand)<br>`application/*+json`: [GradeExamQuestionCommand](#contract-GradeExamQuestionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamGradeDto](#contract-ExamGradeDto)<br>`application/json`: [ExamGradeDto](#contract-ExamGradeDto)<br>`text/json`: [ExamGradeDto](#contract-ExamGradeDto) |

### GET /api/v1/academic/exam-attempts/{attemptId}/guardian-result

کد واقعی: [ExamGradingsController.GetGuardianResult](../MakanApp.Api/Controllers/ExamGradingsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamGradingService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [GuardianExamResultDto](#contract-GuardianExamResultDto)<br>`application/json`: [GuardianExamResultDto](#contract-GuardianExamResultDto)<br>`text/json`: [GuardianExamResultDto](#contract-GuardianExamResultDto) |

### GET /api/v1/academic/exam-attempts/{attemptId}/result

کد واقعی: [ExamGradingsController.GetStudentResult](../MakanApp.Api/Controllers/ExamGradingsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamGradingService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [StudentExamResultDto](#contract-StudentExamResultDto)<br>`application/json`: [StudentExamResultDto](#contract-StudentExamResultDto)<br>`text/json`: [StudentExamResultDto](#contract-StudentExamResultDto) |

### GET /api/v1/academic/exams/{examId}/grading

کد واقعی: [ExamGradingsController.GetQueue](../MakanApp.Api/Controllers/ExamGradingsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamGradingService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |
| `classId` | query | خیر | string / uuid |
| `status` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [ExamGradingQueueItemDto](#contract-ExamGradingQueueItemDto)<br>`application/json`: آرایه از [ExamGradingQueueItemDto](#contract-ExamGradingQueueItemDto)<br>`text/json`: آرایه از [ExamGradingQueueItemDto](#contract-ExamGradingQueueItemDto) |


## Exams

### POST /api/v1/academic/classes/{classId}/exams

کد واقعی: [ExamsController.CreateDraft](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateExamDraftCommand](#contract-CreateExamDraftCommand)<br>`text/json`: [CreateExamDraftCommand](#contract-CreateExamDraftCommand)<br>`application/*+json`: [CreateExamDraftCommand](#contract-CreateExamDraftCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [ExamEditorDto](#contract-ExamEditorDto)<br>`application/json`: [ExamEditorDto](#contract-ExamEditorDto)<br>`text/json`: [ExamEditorDto](#contract-ExamEditorDto) |

### GET /api/v1/academic/exam-attempts/{attemptId}

کد واقعی: [ExamsController.GetAttempt](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [StudentExamAttemptDto](#contract-StudentExamAttemptDto)<br>`application/json`: [StudentExamAttemptDto](#contract-StudentExamAttemptDto)<br>`text/json`: [StudentExamAttemptDto](#contract-StudentExamAttemptDto) |

### GET /api/v1/academic/exam-attempts/{attemptId}/answers

کد واقعی: [ExamsController.GetMyAnswers](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [StudentExamAnswersDto](#contract-StudentExamAnswersDto)<br>`application/json`: [StudentExamAnswersDto](#contract-StudentExamAnswersDto)<br>`text/json`: [StudentExamAnswersDto](#contract-StudentExamAnswersDto) |

### PUT /api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}

کد واقعی: [ExamsController.SaveAnswer](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |
| `attemptQuestionId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SaveExamAnswerCommand](#contract-SaveExamAnswerCommand)<br>`text/json`: [SaveExamAnswerCommand](#contract-SaveExamAnswerCommand)<br>`application/*+json`: [SaveExamAnswerCommand](#contract-SaveExamAnswerCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamAnswerReceiptDto](#contract-ExamAnswerReceiptDto)<br>`application/json`: [ExamAnswerReceiptDto](#contract-ExamAnswerReceiptDto)<br>`text/json`: [ExamAnswerReceiptDto](#contract-ExamAnswerReceiptDto) |

### POST /api/v1/academic/exam-attempts/{attemptId}/finalize

کد واقعی: [ExamsController.FinalizeAttempt](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [FinalizeExamCommand](#contract-FinalizeExamCommand)<br>`text/json`: [FinalizeExamCommand](#contract-FinalizeExamCommand)<br>`application/*+json`: [FinalizeExamCommand](#contract-FinalizeExamCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamFinalReceiptDto](#contract-ExamFinalReceiptDto)<br>`application/json`: [ExamFinalReceiptDto](#contract-ExamFinalReceiptDto)<br>`text/json`: [ExamFinalReceiptDto](#contract-ExamFinalReceiptDto) |

### GET /api/v1/academic/exam-attempts/{attemptId}/receipt

کد واقعی: [ExamsController.GetFinalReceipt](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamFinalReceiptDto](#contract-ExamFinalReceiptDto)<br>`application/json`: [ExamFinalReceiptDto](#contract-ExamFinalReceiptDto)<br>`text/json`: [ExamFinalReceiptDto](#contract-ExamFinalReceiptDto) |

### POST /api/v1/academic/exam-attempts/{attemptId}/write-lease

کد واقعی: [ExamsController.AcquireWriteLease](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamWriteLeaseDto](#contract-ExamWriteLeaseDto)<br>`application/json`: [ExamWriteLeaseDto](#contract-ExamWriteLeaseDto)<br>`text/json`: [ExamWriteLeaseDto](#contract-ExamWriteLeaseDto) |

### POST /api/v1/academic/exam-attempts/{attemptId}/write-lease/transfer

کد واقعی: [ExamsController.TransferWriteLease](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamWriteLeaseDto](#contract-ExamWriteLeaseDto)<br>`application/json`: [ExamWriteLeaseDto](#contract-ExamWriteLeaseDto)<br>`text/json`: [ExamWriteLeaseDto](#contract-ExamWriteLeaseDto) |

### GET /api/v1/academic/exams

کد واقعی: [ExamsController.GetMyExams](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [StudentExamSummary](#contract-StudentExamSummary)<br>`application/json`: آرایه از [StudentExamSummary](#contract-StudentExamSummary)<br>`text/json`: آرایه از [StudentExamSummary](#contract-StudentExamSummary) |

### PATCH /api/v1/academic/exams/{examId}

کد واقعی: [ExamsController.UpdateDraft](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateExamDraftCommand](#contract-UpdateExamDraftCommand)<br>`text/json`: [UpdateExamDraftCommand](#contract-UpdateExamDraftCommand)<br>`application/*+json`: [UpdateExamDraftCommand](#contract-UpdateExamDraftCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamEditorDto](#contract-ExamEditorDto)<br>`application/json`: [ExamEditorDto](#contract-ExamEditorDto)<br>`text/json`: [ExamEditorDto](#contract-ExamEditorDto) |

### GET /api/v1/academic/exams/{examId}/attempts/me

کد واقعی: [ExamsController.GetMyAttempts](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [StudentExamAttemptSummaryDto](#contract-StudentExamAttemptSummaryDto)<br>`application/json`: آرایه از [StudentExamAttemptSummaryDto](#contract-StudentExamAttemptSummaryDto)<br>`text/json`: آرایه از [StudentExamAttemptSummaryDto](#contract-StudentExamAttemptSummaryDto) |

### GET /api/v1/academic/exams/{examId}/attempts/me/active

کد واقعی: [ExamsController.GetMyActiveAttempt](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [StudentExamAttemptDto](#contract-StudentExamAttemptDto)<br>`application/json`: [StudentExamAttemptDto](#contract-StudentExamAttemptDto)<br>`text/json`: [StudentExamAttemptDto](#contract-StudentExamAttemptDto) |

### POST /api/v1/academic/exams/{examId}/attempts/start

کد واقعی: [ExamsController.StartAttempt](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [StartExamCommand](#contract-StartExamCommand)<br>`text/json`: [StartExamCommand](#contract-StartExamCommand)<br>`application/*+json`: [StartExamCommand](#contract-StartExamCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [StudentExamAttemptDto](#contract-StudentExamAttemptDto)<br>`application/json`: [StudentExamAttemptDto](#contract-StudentExamAttemptDto)<br>`text/json`: [StudentExamAttemptDto](#contract-StudentExamAttemptDto) |

### GET /api/v1/academic/exams/{examId}/editor

کد واقعی: [ExamsController.GetEditor](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamEditorDto](#contract-ExamEditorDto)<br>`application/json`: [ExamEditorDto](#contract-ExamEditorDto)<br>`text/json`: [ExamEditorDto](#contract-ExamEditorDto) |

### POST /api/v1/academic/exams/{examId}/publish

کد واقعی: [ExamsController.Publish](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PublishExamCommand](#contract-PublishExamCommand)<br>`text/json`: [PublishExamCommand](#contract-PublishExamCommand)<br>`application/*+json`: [PublishExamCommand](#contract-PublishExamCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamEditorDto](#contract-ExamEditorDto)<br>`application/json`: [ExamEditorDto](#contract-ExamEditorDto)<br>`text/json`: [ExamEditorDto](#contract-ExamEditorDto) |

### POST /api/v1/academic/exams/{examId}/questions

کد واقعی: [ExamsController.AddQuestion](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [AddExamQuestionCommand](#contract-AddExamQuestionCommand)<br>`text/json`: [AddExamQuestionCommand](#contract-AddExamQuestionCommand)<br>`application/*+json`: [AddExamQuestionCommand](#contract-AddExamQuestionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [ExamEditorDto](#contract-ExamEditorDto)<br>`application/json`: [ExamEditorDto](#contract-ExamEditorDto)<br>`text/json`: [ExamEditorDto](#contract-ExamEditorDto) |

### DELETE /api/v1/academic/exams/{examId}/questions/{questionId}

کد واقعی: [ExamsController.DeleteQuestion](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |
| `questionId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [DeleteExamQuestionCommand](#contract-DeleteExamQuestionCommand)<br>`text/json`: [DeleteExamQuestionCommand](#contract-DeleteExamQuestionCommand)<br>`application/*+json`: [DeleteExamQuestionCommand](#contract-DeleteExamQuestionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 204 | No Content | بدون بدنهٔ منتشرشده |

### PATCH /api/v1/academic/exams/{examId}/questions/{questionId}

کد واقعی: [ExamsController.UpdateQuestion](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |
| `questionId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateExamQuestionCommand](#contract-UpdateExamQuestionCommand)<br>`text/json`: [UpdateExamQuestionCommand](#contract-UpdateExamQuestionCommand)<br>`application/*+json`: [UpdateExamQuestionCommand](#contract-UpdateExamQuestionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamEditorDto](#contract-ExamEditorDto)<br>`application/json`: [ExamEditorDto](#contract-ExamEditorDto)<br>`text/json`: [ExamEditorDto](#contract-ExamEditorDto) |

### GET /api/v1/academic/exams/{examId}/student-preview

کد واقعی: [ExamsController.GetStudentPreview](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [StudentSafeExamPreview](#contract-StudentSafeExamPreview)<br>`application/json`: [StudentSafeExamPreview](#contract-StudentSafeExamPreview)<br>`text/json`: [StudentSafeExamPreview](#contract-StudentSafeExamPreview) |

### GET /api/v1/academic/exams/{examId}/teacher-preview

کد واقعی: [ExamsController.GetTeacherPreview](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ExamTeacherPreview](#contract-ExamTeacherPreview)<br>`application/json`: [ExamTeacherPreview](#contract-ExamTeacherPreview)<br>`text/json`: [ExamTeacherPreview](#contract-ExamTeacherPreview) |

### POST /api/v1/academic/exams/{examId}/versions

کد واقعی: [ExamsController.CreateNextVersion](../MakanApp.Api/Controllers/ExamsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.IExamService`، `MakanApp.Application.Assessment.IExamAttemptService`، `MakanApp.Application.Assessment.IExamAnswerService`، `MakanApp.Application.Assessment.IExamFinalizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `examId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateNextExamVersionCommand](#contract-CreateNextExamVersionCommand)<br>`text/json`: [CreateNextExamVersionCommand](#contract-CreateNextExamVersionCommand)<br>`application/*+json`: [CreateNextExamVersionCommand](#contract-CreateNextExamVersionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [ExamEditorDto](#contract-ExamEditorDto)<br>`application/json`: [ExamEditorDto](#contract-ExamEditorDto)<br>`text/json`: [ExamEditorDto](#contract-ExamEditorDto) |


## Files

### POST /api/v1/files

کد واقعی: [FilesController.Upload](../MakanApp.Api/Controllers/FilesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Storage.IStorageService`

بدنه درخواست: `multipart/form-data`: object؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [FileAssetResult](#contract-FileAssetResult)<br>`application/json`: [FileAssetResult](#contract-FileAssetResult)<br>`text/json`: [FileAssetResult](#contract-FileAssetResult) |

### DELETE /api/v1/files/{fileId}

کد واقعی: [FilesController.Delete](../MakanApp.Api/Controllers/FilesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Storage.IStorageService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `fileId` | path | بله | string / uuid |
| `expectedRowVersion` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [FileAssetResult](#contract-FileAssetResult)<br>`application/json`: [FileAssetResult](#contract-FileAssetResult)<br>`text/json`: [FileAssetResult](#contract-FileAssetResult) |

### GET /api/v1/files/{fileId}

کد واقعی: [FilesController.GetMetadata](../MakanApp.Api/Controllers/FilesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Storage.IStorageService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `fileId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [FileAssetResult](#contract-FileAssetResult)<br>`application/json`: [FileAssetResult](#contract-FileAssetResult)<br>`text/json`: [FileAssetResult](#contract-FileAssetResult) |

### GET /api/v1/files/{fileId}/content

کد واقعی: [FilesController.Download](../MakanApp.Api/Controllers/FilesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Storage.IStorageService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `fileId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [FileStreamResult](#contract-FileStreamResult)<br>`application/json`: [FileStreamResult](#contract-FileStreamResult)<br>`text/json`: [FileStreamResult](#contract-FileStreamResult) |


## Guardian

### GET /api/v1/guardian/children

کد واقعی: [GuardianController.GetMyChildren](../MakanApp.Api/Controllers/GuardianController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.IGuardianService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [AuthorizedChildResult](#contract-AuthorizedChildResult)<br>`application/json`: آرایه از [AuthorizedChildResult](#contract-AuthorizedChildResult)<br>`text/json`: آرایه از [AuthorizedChildResult](#contract-AuthorizedChildResult) |

### POST /api/v1/guardian/children/{organizationPersonId}/select

کد واقعی: [GuardianController.SelectChildContext](../MakanApp.Api/Controllers/GuardianController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.IGuardianService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationPersonId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AccessContext](#contract-AccessContext)<br>`application/json`: [AccessContext](#contract-AccessContext)<br>`text/json`: [AccessContext](#contract-AccessContext) |

### GET /api/v1/guardian/relations/{organizationPersonId}

کد واقعی: [GuardianController.GetRelationSummary](../MakanApp.Api/Controllers/GuardianController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.IGuardianService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationPersonId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [GuardianRelationSummaryResult](#contract-GuardianRelationSummaryResult)<br>`application/json`: [GuardianRelationSummaryResult](#contract-GuardianRelationSummaryResult)<br>`text/json`: [GuardianRelationSummaryResult](#contract-GuardianRelationSummaryResult) |


## Invitations

### GET /api/v1/invitations

کد واقعی: [InvitationsController.GetMyInvitations](../MakanApp.Api/Controllers/InvitationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [InvitationResult](#contract-InvitationResult)<br>`application/json`: آرایه از [InvitationResult](#contract-InvitationResult)<br>`text/json`: آرایه از [InvitationResult](#contract-InvitationResult) |

### POST /api/v1/invitations/{invitationId}/accept

کد واقعی: [InvitationsController.AcceptInvitation](../MakanApp.Api/Controllers/InvitationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `invitationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [AcceptInvitationCommand](#contract-AcceptInvitationCommand)<br>`text/json`: [AcceptInvitationCommand](#contract-AcceptInvitationCommand)<br>`application/*+json`: [AcceptInvitationCommand](#contract-AcceptInvitationCommand)؛ optional در schema

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AcceptInvitationResult](#contract-AcceptInvitationResult)<br>`application/json`: [AcceptInvitationResult](#contract-AcceptInvitationResult)<br>`text/json`: [AcceptInvitationResult](#contract-AcceptInvitationResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### POST /api/v1/invitations/{invitationId}/decline

کد واقعی: [InvitationsController.DeclineInvitation](../MakanApp.Api/Controllers/InvitationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `invitationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [DeclineInvitationResult](#contract-DeclineInvitationResult)<br>`application/json`: [DeclineInvitationResult](#contract-DeclineInvitationResult)<br>`text/json`: [DeclineInvitationResult](#contract-DeclineInvitationResult) |


## MessageReports

### POST /api/v1/conversations/{conversationId}/messages/{messageId}/reports

کد واقعی: [MessageReportsController.Report](../MakanApp.Api/Controllers/MessageReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `messageId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [ReportMessageCommand](#contract-ReportMessageCommand)<br>`text/json`: [ReportMessageCommand](#contract-ReportMessageCommand)<br>`application/*+json`: [ReportMessageCommand](#contract-ReportMessageCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [AbuseReportReceiptResult](#contract-AbuseReportReceiptResult)<br>`application/json`: [AbuseReportReceiptResult](#contract-AbuseReportReceiptResult)<br>`text/json`: [AbuseReportReceiptResult](#contract-AbuseReportReceiptResult) |


## Messages

### GET /api/v1/messages/search

کد واقعی: [MessagesController.Search](../MakanApp.Api/Controllers/MessagesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `q` | query | خیر | string |
| `conversationId` | query | خیر | string / uuid |
| `senderUserId` | query | خیر | string / uuid |
| `kind` | query | خیر | string |
| `fromUtc` | query | خیر | string / date-time |
| `toUtc` | query | خیر | string / date-time |
| `cursor` | query | خیر | string |
| `limit` | query | خیر | integer / int32 |
| `savedLabel` | query | خیر | string |
| `hasAttachment` | query | خیر | boolean |
| `hasLinks` | query | خیر | boolean |
| `topicId` | query | خیر | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MessageSearchPageResult](#contract-MessageSearchPageResult)<br>`application/json`: [MessageSearchPageResult](#contract-MessageSearchPageResult)<br>`text/json`: [MessageSearchPageResult](#contract-MessageSearchPageResult) |


## MessagingFolders

### GET /api/v1/messaging/folders

کد واقعی: [MessagingFoldersController.Get](../MakanApp.Api/Controllers/MessagingFoldersController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.MessagingPresentationService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [ChatFolderResult](#contract-ChatFolderResult)<br>`application/json`: آرایه از [ChatFolderResult](#contract-ChatFolderResult)<br>`text/json`: آرایه از [ChatFolderResult](#contract-ChatFolderResult) |

### DELETE /api/v1/messaging/folders/{folderId}

کد واقعی: [MessagingFoldersController.Delete](../MakanApp.Api/Controllers/MessagingFoldersController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.MessagingPresentationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `folderId` | path | بله | string / uuid |
| `expectedVersion` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 204 | No Content | بدون بدنهٔ منتشرشده |

### PUT /api/v1/messaging/folders/{folderId}

کد واقعی: [MessagingFoldersController.Save](../MakanApp.Api/Controllers/MessagingFoldersController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.MessagingPresentationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `folderId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SaveChatFolderCommand](#contract-SaveChatFolderCommand)<br>`text/json`: [SaveChatFolderCommand](#contract-SaveChatFolderCommand)<br>`application/*+json`: [SaveChatFolderCommand](#contract-SaveChatFolderCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ChatFolderResult](#contract-ChatFolderResult)<br>`application/json`: [ChatFolderResult](#contract-ChatFolderResult)<br>`text/json`: [ChatFolderResult](#contract-ChatFolderResult) |


## MessagingPrivacy

### GET /api/v1/messaging/privacy

کد واقعی: [MessagingPrivacyController.Get](../MakanApp.Api/Controllers/MessagingPrivacyController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingDirectoryService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MessagingPrivacySettingsResult](#contract-MessagingPrivacySettingsResult)<br>`application/json`: [MessagingPrivacySettingsResult](#contract-MessagingPrivacySettingsResult)<br>`text/json`: [MessagingPrivacySettingsResult](#contract-MessagingPrivacySettingsResult) |

### PUT /api/v1/messaging/privacy

کد واقعی: [MessagingPrivacyController.Update](../MakanApp.Api/Controllers/MessagingPrivacyController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingDirectoryService`

بدنه درخواست: `application/json`: [UpdateMessagingPrivacyCommand](#contract-UpdateMessagingPrivacyCommand)<br>`text/json`: [UpdateMessagingPrivacyCommand](#contract-UpdateMessagingPrivacyCommand)<br>`application/*+json`: [UpdateMessagingPrivacyCommand](#contract-UpdateMessagingPrivacyCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MessagingPrivacySettingsResult](#contract-MessagingPrivacySettingsResult)<br>`application/json`: [MessagingPrivacySettingsResult](#contract-MessagingPrivacySettingsResult)<br>`text/json`: [MessagingPrivacySettingsResult](#contract-MessagingPrivacySettingsResult) |


## MessagingSafety

### GET /api/v1/messaging/blocks

کد واقعی: [MessagingSafetyController.GetBlocks](../MakanApp.Api/Controllers/MessagingSafetyController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingSafetyService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [BlockedUserResult](#contract-BlockedUserResult)<br>`application/json`: آرایه از [BlockedUserResult](#contract-BlockedUserResult)<br>`text/json`: آرایه از [BlockedUserResult](#contract-BlockedUserResult) |

### DELETE /api/v1/messaging/blocks/{userId}

کد واقعی: [MessagingSafetyController.Unblock](../MakanApp.Api/Controllers/MessagingSafetyController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 204 | No Content | بدون بدنهٔ منتشرشده |

### POST /api/v1/messaging/blocks/{userId}

کد واقعی: [MessagingSafetyController.Block](../MakanApp.Api/Controllers/MessagingSafetyController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [UserBlockResult](#contract-UserBlockResult)<br>`application/json`: [UserBlockResult](#contract-UserBlockResult)<br>`text/json`: [UserBlockResult](#contract-UserBlockResult) |
| 201 | Created | `text/plain`: [UserBlockResult](#contract-UserBlockResult)<br>`application/json`: [UserBlockResult](#contract-UserBlockResult)<br>`text/json`: [UserBlockResult](#contract-UserBlockResult) |

### GET /api/v1/messaging/reports/{reportId}

کد واقعی: [MessagingSafetyController.GetReport](../MakanApp.Api/Controllers/MessagingSafetyController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `reportId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AbuseReportReceiptResult](#contract-AbuseReportReceiptResult)<br>`application/json`: [AbuseReportReceiptResult](#contract-AbuseReportReceiptResult)<br>`text/json`: [AbuseReportReceiptResult](#contract-AbuseReportReceiptResult) |


## NotificationDevices

### GET /api/v1/notifications/devices

کد واقعی: [NotificationDevicesController.GetPage](../MakanApp.Api/Controllers/NotificationDevicesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Notification.IPushDeviceService`، `MakanApp.Application.Notification.IPushClientConfigurationProvider`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |
| `includeRevoked` | query | خیر | boolean |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PushDevicePageResult](#contract-PushDevicePageResult)<br>`application/json`: [PushDevicePageResult](#contract-PushDevicePageResult)<br>`text/json`: [PushDevicePageResult](#contract-PushDevicePageResult) |

### POST /api/v1/notifications/devices

کد واقعی: [NotificationDevicesController.Register](../MakanApp.Api/Controllers/NotificationDevicesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Notification.IPushDeviceService`، `MakanApp.Application.Notification.IPushClientConfigurationProvider`

بدنه درخواست: `application/json`: [RegisterPushDeviceCommand](#contract-RegisterPushDeviceCommand)<br>`text/json`: [RegisterPushDeviceCommand](#contract-RegisterPushDeviceCommand)<br>`application/*+json`: [RegisterPushDeviceCommand](#contract-RegisterPushDeviceCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PushDeviceRegistrationResult](#contract-PushDeviceRegistrationResult)<br>`application/json`: [PushDeviceRegistrationResult](#contract-PushDeviceRegistrationResult)<br>`text/json`: [PushDeviceRegistrationResult](#contract-PushDeviceRegistrationResult) |

### POST /api/v1/notifications/devices/web-push

کد واقعی: [NotificationDevicesController.RegisterWebPush](../MakanApp.Api/Controllers/NotificationDevicesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Notification.IPushDeviceService`، `MakanApp.Application.Notification.IPushClientConfigurationProvider`

بدنه درخواست: `application/json`: [RegisterWebPushCommand](#contract-RegisterWebPushCommand)<br>`text/json`: [RegisterWebPushCommand](#contract-RegisterWebPushCommand)<br>`application/*+json`: [RegisterWebPushCommand](#contract-RegisterWebPushCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PushDeviceRegistrationResult](#contract-PushDeviceRegistrationResult)<br>`application/json`: [PushDeviceRegistrationResult](#contract-PushDeviceRegistrationResult)<br>`text/json`: [PushDeviceRegistrationResult](#contract-PushDeviceRegistrationResult) |
| 400 | Bad Request | `text/plain`: [ProblemDetails](#contract-ProblemDetails)<br>`application/json`: [ProblemDetails](#contract-ProblemDetails)<br>`text/json`: [ProblemDetails](#contract-ProblemDetails) |
| 409 | Conflict | `text/plain`: [ProblemDetails](#contract-ProblemDetails)<br>`application/json`: [ProblemDetails](#contract-ProblemDetails)<br>`text/json`: [ProblemDetails](#contract-ProblemDetails) |
| 503 | Service Unavailable | `text/plain`: [ProblemDetails](#contract-ProblemDetails)<br>`application/json`: [ProblemDetails](#contract-ProblemDetails)<br>`text/json`: [ProblemDetails](#contract-ProblemDetails) |

### GET /api/v1/notifications/devices/web-push/configuration

کد واقعی: [NotificationDevicesController.GetWebPushConfiguration](../MakanApp.Api/Controllers/NotificationDevicesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Notification.IPushDeviceService`، `MakanApp.Application.Notification.IPushClientConfigurationProvider`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [WebPushClientConfiguration](#contract-WebPushClientConfiguration)<br>`application/json`: [WebPushClientConfiguration](#contract-WebPushClientConfiguration)<br>`text/json`: [WebPushClientConfiguration](#contract-WebPushClientConfiguration) |

### DELETE /api/v1/notifications/devices/{registrationId}

کد واقعی: [NotificationDevicesController.Revoke](../MakanApp.Api/Controllers/NotificationDevicesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Notification.IPushDeviceService`، `MakanApp.Application.Notification.IPushClientConfigurationProvider`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `registrationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 204 | No Content | بدون بدنهٔ منتشرشده |


## NotificationPreferences

### GET /api/v1/notification-preferences

کد واقعی: [NotificationPreferencesController.Get](../MakanApp.Api/Controllers/NotificationPreferencesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Notification.INotificationPreferenceService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | query | خیر | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [NotificationPreferenceItem](#contract-NotificationPreferenceItem)<br>`application/json`: آرایه از [NotificationPreferenceItem](#contract-NotificationPreferenceItem)<br>`text/json`: آرایه از [NotificationPreferenceItem](#contract-NotificationPreferenceItem) |

### PUT /api/v1/notification-preferences/{category}

کد واقعی: [NotificationPreferencesController.Update](../MakanApp.Api/Controllers/NotificationPreferencesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Notification.INotificationPreferenceService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `category` | path | بله | [NotificationPreferenceCategory](#contract-NotificationPreferenceCategory) |
| `organizationId` | query | خیر | string / uuid |

بدنه درخواست: `application/json`: [UpdateNotificationPreferenceCommand](#contract-UpdateNotificationPreferenceCommand)<br>`text/json`: [UpdateNotificationPreferenceCommand](#contract-UpdateNotificationPreferenceCommand)<br>`application/*+json`: [UpdateNotificationPreferenceCommand](#contract-UpdateNotificationPreferenceCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [NotificationPreferenceItem](#contract-NotificationPreferenceItem)<br>`application/json`: [NotificationPreferenceItem](#contract-NotificationPreferenceItem)<br>`text/json`: [NotificationPreferenceItem](#contract-NotificationPreferenceItem) |


## Notifications

### GET /api/v1/notifications

کد واقعی: [NotificationsController.GetPage](../MakanApp.Api/Controllers/NotificationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Notification.INotificationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `Page` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `UnreadOnly` | query | خیر | boolean |
| `Category` | query | خیر | string |
| `OrganizationId` | query | خیر | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [NotificationPage](#contract-NotificationPage)<br>`application/json`: [NotificationPage](#contract-NotificationPage)<br>`text/json`: [NotificationPage](#contract-NotificationPage) |

### GET /api/v1/notifications/unread-count

کد واقعی: [NotificationsController.GetUnreadCount](../MakanApp.Api/Controllers/NotificationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Notification.INotificationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | query | خیر | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [NotificationUnreadCount](#contract-NotificationUnreadCount)<br>`application/json`: [NotificationUnreadCount](#contract-NotificationUnreadCount)<br>`text/json`: [NotificationUnreadCount](#contract-NotificationUnreadCount) |

### POST /api/v1/notifications/{notificationId}/read

کد واقعی: [NotificationsController.MarkRead](../MakanApp.Api/Controllers/NotificationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Notification.INotificationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `notificationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [NotificationItem](#contract-NotificationItem)<br>`application/json`: [NotificationItem](#contract-NotificationItem)<br>`text/json`: [NotificationItem](#contract-NotificationItem) |


## OfficialClassConversations

### POST /api/v1/organization/classes/{classId}/conversation

کد واقعی: [OfficialClassConversationsController.Resolve](../MakanApp.Api/Controllers/OfficialClassConversationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.OfficialClassConversationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [OfficialClassConversationResult](#contract-OfficialClassConversationResult)<br>`application/json`: [OfficialClassConversationResult](#contract-OfficialClassConversationResult)<br>`text/json`: [OfficialClassConversationResult](#contract-OfficialClassConversationResult) |


## OrganizationMembers

### GET /api/v1/organization/members

کد واقعی: [OrganizationMembersController.Get](../MakanApp.Api/Controllers/OrganizationMembersController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.SchoolReadService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `Search` | query | خیر | string |
| `Role` | query | خیر | string |
| `Status` | query | خیر | string |
| `Page` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolPageOfSchoolMemberResult](#contract-SchoolPageOfSchoolMemberResult)<br>`application/json`: [SchoolPageOfSchoolMemberResult](#contract-SchoolPageOfSchoolMemberResult)<br>`text/json`: [SchoolPageOfSchoolMemberResult](#contract-SchoolPageOfSchoolMemberResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |


## OrganizationPeople

### GET /api/v1/organization/people

کد واقعی: [OrganizationPeopleController.List](../MakanApp.Api/Controllers/OrganizationPeopleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationPersonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `search` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [OrganizationPeoplePage](#contract-OrganizationPeoplePage)<br>`application/json`: [OrganizationPeoplePage](#contract-OrganizationPeoplePage)<br>`text/json`: [OrganizationPeoplePage](#contract-OrganizationPeoplePage) |

### POST /api/v1/organization/people

کد واقعی: [OrganizationPeopleController.Create](../MakanApp.Api/Controllers/OrganizationPeopleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationPersonService`

بدنه درخواست: `application/json`: [CreateOrganizationPersonCommand](#contract-CreateOrganizationPersonCommand)<br>`text/json`: [CreateOrganizationPersonCommand](#contract-CreateOrganizationPersonCommand)<br>`application/*+json`: [CreateOrganizationPersonCommand](#contract-CreateOrganizationPersonCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [OrganizationPersonDto](#contract-OrganizationPersonDto)<br>`application/json`: [OrganizationPersonDto](#contract-OrganizationPersonDto)<br>`text/json`: [OrganizationPersonDto](#contract-OrganizationPersonDto) |
| 200 | OK | `text/plain`: [OrganizationPersonDto](#contract-OrganizationPersonDto)<br>`application/json`: [OrganizationPersonDto](#contract-OrganizationPersonDto)<br>`text/json`: [OrganizationPersonDto](#contract-OrganizationPersonDto) |

### GET /api/v1/organization/people/{id}

کد واقعی: [OrganizationPeopleController.Get](../MakanApp.Api/Controllers/OrganizationPeopleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationPersonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `id` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [OrganizationPersonDto](#contract-OrganizationPersonDto)<br>`application/json`: [OrganizationPersonDto](#contract-OrganizationPersonDto)<br>`text/json`: [OrganizationPersonDto](#contract-OrganizationPersonDto) |

### PATCH /api/v1/organization/people/{id}

کد واقعی: [OrganizationPeopleController.Update](../MakanApp.Api/Controllers/OrganizationPeopleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationPersonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `id` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateOrganizationPersonNamesCommand](#contract-UpdateOrganizationPersonNamesCommand)<br>`text/json`: [UpdateOrganizationPersonNamesCommand](#contract-UpdateOrganizationPersonNamesCommand)<br>`application/*+json`: [UpdateOrganizationPersonNamesCommand](#contract-UpdateOrganizationPersonNamesCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [OrganizationPersonDto](#contract-OrganizationPersonDto)<br>`application/json`: [OrganizationPersonDto](#contract-OrganizationPersonDto)<br>`text/json`: [OrganizationPersonDto](#contract-OrganizationPersonDto) |

### POST /api/v1/organization/people/{id}/end

کد واقعی: [OrganizationPeopleController.End](../MakanApp.Api/Controllers/OrganizationPeopleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationPersonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `id` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`text/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`application/*+json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [OrganizationPersonDto](#contract-OrganizationPersonDto)<br>`application/json`: [OrganizationPersonDto](#contract-OrganizationPersonDto)<br>`text/json`: [OrganizationPersonDto](#contract-OrganizationPersonDto) |

### POST /api/v1/organization/people/{id}/invitations

کد واقعی: [OrganizationPeopleController.Invite](../MakanApp.Api/Controllers/OrganizationPeopleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationPersonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `id` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreatePersonLinkInvitationCommand](#contract-CreatePersonLinkInvitationCommand)<br>`text/json`: [CreatePersonLinkInvitationCommand](#contract-CreatePersonLinkInvitationCommand)<br>`application/*+json`: [CreatePersonLinkInvitationCommand](#contract-CreatePersonLinkInvitationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [PersonLinkInvitationDto](#contract-PersonLinkInvitationDto)<br>`application/json`: [PersonLinkInvitationDto](#contract-PersonLinkInvitationDto)<br>`text/json`: [PersonLinkInvitationDto](#contract-PersonLinkInvitationDto) |
| 200 | OK | `text/plain`: [PersonLinkInvitationDto](#contract-PersonLinkInvitationDto)<br>`application/json`: [PersonLinkInvitationDto](#contract-PersonLinkInvitationDto)<br>`text/json`: [PersonLinkInvitationDto](#contract-PersonLinkInvitationDto) |

### POST /api/v1/organization/people/{id}/invitations/{invitationId}/revoke

کد واقعی: [OrganizationPeopleController.RevokeInvitation](../MakanApp.Api/Controllers/OrganizationPeopleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationPersonService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `id` | path | بله | string / uuid |
| `invitationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`text/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`application/*+json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PersonLinkInvitationDto](#contract-PersonLinkInvitationDto)<br>`application/json`: [PersonLinkInvitationDto](#contract-PersonLinkInvitationDto)<br>`text/json`: [PersonLinkInvitationDto](#contract-PersonLinkInvitationDto) |


## People

### POST /api/v1/people/resolve

کد واقعی: [PeopleController.Resolve](../MakanApp.Api/Controllers/PeopleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingDirectoryService`

بدنه درخواست: `application/json`: [ResolvePersonCommand](#contract-ResolvePersonCommand)<br>`text/json`: [ResolvePersonCommand](#contract-ResolvePersonCommand)<br>`application/*+json`: [ResolvePersonCommand](#contract-ResolvePersonCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PeopleResolveResult](#contract-PeopleResolveResult)<br>`application/json`: [PeopleResolveResult](#contract-PeopleResolveResult)<br>`text/json`: [PeopleResolveResult](#contract-PeopleResolveResult) |

### GET /api/v1/people/search

کد واقعی: [PeopleController.Search](../MakanApp.Api/Controllers/PeopleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingDirectoryService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `q` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PeopleSearchPageResult](#contract-PeopleSearchPageResult)<br>`application/json`: [PeopleSearchPageResult](#contract-PeopleSearchPageResult)<br>`text/json`: [PeopleSearchPageResult](#contract-PeopleSearchPageResult) |

### GET /api/v1/people/{userId}

کد واقعی: [PeopleController.GetProfile](../MakanApp.Api/Controllers/PeopleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.IMessagingDirectoryService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [MessagingPublicProfileResult](#contract-MessagingPublicProfileResult)<br>`application/json`: [MessagingPublicProfileResult](#contract-MessagingPublicProfileResult)<br>`text/json`: [MessagingPublicProfileResult](#contract-MessagingPublicProfileResult) |


## PeoplePresence

### GET /api/v1/people/{userId}/presence

کد واقعی: [PeoplePresenceController.Get](../MakanApp.Api/Controllers/PeoplePresenceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.MessagingEphemeralService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PresenceResult](#contract-PresenceResult)<br>`application/json`: [PresenceResult](#contract-PresenceResult)<br>`text/json`: [PresenceResult](#contract-PresenceResult) |


## PlatformCommerce

### POST /api/admin/v1/commerce/organizations/{organizationId}/subscription-override

کد واقعی: [PlatformCommerceController.OverrideSubscription](../MakanApp.Api/Controllers/PlatformCommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.commerce.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [OverridePlatformSubscriptionCommand](#contract-OverridePlatformSubscriptionCommand)<br>`text/json`: [OverridePlatformSubscriptionCommand](#contract-OverridePlatformSubscriptionCommand)<br>`application/*+json`: [OverridePlatformSubscriptionCommand](#contract-OverridePlatformSubscriptionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformSubscriptionOverrideResult](#contract-PlatformSubscriptionOverrideResult)<br>`application/json`: [PlatformSubscriptionOverrideResult](#contract-PlatformSubscriptionOverrideResult)<br>`text/json`: [PlatformSubscriptionOverrideResult](#contract-PlatformSubscriptionOverrideResult) |

### POST /api/admin/v1/commerce/plan-versions/{planVersionId}/publish

کد واقعی: [PlatformCommerceController.PublishPlanVersion](../MakanApp.Api/Controllers/PlatformCommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.commerce.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `planVersionId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PublishPlatformPlanVersionCommand](#contract-PublishPlatformPlanVersionCommand)<br>`text/json`: [PublishPlatformPlanVersionCommand](#contract-PublishPlatformPlanVersionCommand)<br>`application/*+json`: [PublishPlatformPlanVersionCommand](#contract-PublishPlatformPlanVersionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformPlanVersionItem](#contract-PlatformPlanVersionItem)<br>`application/json`: [PlatformPlanVersionItem](#contract-PlatformPlanVersionItem)<br>`text/json`: [PlatformPlanVersionItem](#contract-PlatformPlanVersionItem) |

### GET /api/admin/v1/commerce/plans

کد واقعی: [PlatformCommerceController.GetPlans](../MakanApp.Api/Controllers/PlatformCommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.commerce.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `search` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformPlanPage](#contract-PlatformPlanPage)<br>`application/json`: [PlatformPlanPage](#contract-PlatformPlanPage)<br>`text/json`: [PlatformPlanPage](#contract-PlatformPlanPage) |

### POST /api/admin/v1/commerce/plans

کد واقعی: [PlatformCommerceController.CreatePlan](../MakanApp.Api/Controllers/PlatformCommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.commerce.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

بدنه درخواست: `application/json`: [CreatePlatformPlanCommand](#contract-CreatePlatformPlanCommand)<br>`text/json`: [CreatePlatformPlanCommand](#contract-CreatePlatformPlanCommand)<br>`application/*+json`: [CreatePlatformPlanCommand](#contract-CreatePlatformPlanCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformPlanDetail](#contract-PlatformPlanDetail)<br>`application/json`: [PlatformPlanDetail](#contract-PlatformPlanDetail)<br>`text/json`: [PlatformPlanDetail](#contract-PlatformPlanDetail) |

### GET /api/admin/v1/commerce/plans/{planId}

کد واقعی: [PlatformCommerceController.GetPlan](../MakanApp.Api/Controllers/PlatformCommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.commerce.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `planId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformPlanDetail](#contract-PlatformPlanDetail)<br>`application/json`: [PlatformPlanDetail](#contract-PlatformPlanDetail)<br>`text/json`: [PlatformPlanDetail](#contract-PlatformPlanDetail) |

### POST /api/admin/v1/commerce/plans/{planId}/versions

کد واقعی: [PlatformCommerceController.CreatePlanVersion](../MakanApp.Api/Controllers/PlatformCommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.commerce.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `planId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreatePlatformPlanVersionCommand](#contract-CreatePlatformPlanVersionCommand)<br>`text/json`: [CreatePlatformPlanVersionCommand](#contract-CreatePlatformPlanVersionCommand)<br>`application/*+json`: [CreatePlatformPlanVersionCommand](#contract-CreatePlatformPlanVersionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformPlanVersionItem](#contract-PlatformPlanVersionItem)<br>`application/json`: [PlatformPlanVersionItem](#contract-PlatformPlanVersionItem)<br>`text/json`: [PlatformPlanVersionItem](#contract-PlatformPlanVersionItem) |

### GET /api/admin/v1/commerce/subscriptions

کد واقعی: [PlatformCommerceController.GetSubscriptions](../MakanApp.Api/Controllers/PlatformCommerceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.commerce.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | query | خیر | string / uuid |
| `status` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformSubscriptionPage](#contract-PlatformSubscriptionPage)<br>`application/json`: [PlatformSubscriptionPage](#contract-PlatformSubscriptionPage)<br>`text/json`: [PlatformSubscriptionPage](#contract-PlatformSubscriptionPage) |


## PlatformEducation

### GET /api/admin/v1/education/assignments

کد واقعی: [PlatformEducationController.GetAssignments](../MakanApp.Api/Controllers/PlatformEducationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.education.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | query | خیر | string / uuid |
| `classId` | query | خیر | string / uuid |
| `search` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformAssignmentPage](#contract-PlatformAssignmentPage)<br>`application/json`: [PlatformAssignmentPage](#contract-PlatformAssignmentPage)<br>`text/json`: [PlatformAssignmentPage](#contract-PlatformAssignmentPage) |

### GET /api/admin/v1/education/classes

کد واقعی: [PlatformEducationController.GetClasses](../MakanApp.Api/Controllers/PlatformEducationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.education.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | query | خیر | string / uuid |
| `search` | query | خیر | string |
| `status` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformClassPage](#contract-PlatformClassPage)<br>`application/json`: [PlatformClassPage](#contract-PlatformClassPage)<br>`text/json`: [PlatformClassPage](#contract-PlatformClassPage) |

### GET /api/admin/v1/education/classes/{classId}

کد واقعی: [PlatformEducationController.GetClass](../MakanApp.Api/Controllers/PlatformEducationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.education.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformClassDetail](#contract-PlatformClassDetail)<br>`application/json`: [PlatformClassDetail](#contract-PlatformClassDetail)<br>`text/json`: [PlatformClassDetail](#contract-PlatformClassDetail) |

### GET /api/admin/v1/education/enrollments

کد واقعی: [PlatformEducationController.GetEnrollments](../MakanApp.Api/Controllers/PlatformEducationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.education.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | query | خیر | string / uuid |
| `classId` | query | خیر | string / uuid |
| `search` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformEnrollmentPage](#contract-PlatformEnrollmentPage)<br>`application/json`: [PlatformEnrollmentPage](#contract-PlatformEnrollmentPage)<br>`text/json`: [PlatformEnrollmentPage](#contract-PlatformEnrollmentPage) |

### GET /api/admin/v1/education/exams

کد واقعی: [PlatformEducationController.GetExams](../MakanApp.Api/Controllers/PlatformEducationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.education.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | query | خیر | string / uuid |
| `classId` | query | خیر | string / uuid |
| `search` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformExamPage](#contract-PlatformExamPage)<br>`application/json`: [PlatformExamPage](#contract-PlatformExamPage)<br>`text/json`: [PlatformExamPage](#contract-PlatformExamPage) |

### GET /api/admin/v1/education/teachers

کد واقعی: [PlatformEducationController.GetTeachers](../MakanApp.Api/Controllers/PlatformEducationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.education.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | query | خیر | string / uuid |
| `classId` | query | خیر | string / uuid |
| `search` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformTeacherAssignmentPage](#contract-PlatformTeacherAssignmentPage)<br>`application/json`: [PlatformTeacherAssignmentPage](#contract-PlatformTeacherAssignmentPage)<br>`text/json`: [PlatformTeacherAssignmentPage](#contract-PlatformTeacherAssignmentPage) |


## PlatformFinance

### GET /api/admin/v1/finance/orders

کد واقعی: [PlatformFinanceController.GetOrders](../MakanApp.Api/Controllers/PlatformFinanceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.finance.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | query | خیر | string / uuid |
| `status` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformOrderPage](#contract-PlatformOrderPage)<br>`application/json`: [PlatformOrderPage](#contract-PlatformOrderPage)<br>`text/json`: [PlatformOrderPage](#contract-PlatformOrderPage) |

### GET /api/admin/v1/finance/orders/{orderId}

کد واقعی: [PlatformFinanceController.GetOrder](../MakanApp.Api/Controllers/PlatformFinanceController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.finance.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `orderId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformOrderItem](#contract-PlatformOrderItem)<br>`application/json`: [PlatformOrderItem](#contract-PlatformOrderItem)<br>`text/json`: [PlatformOrderItem](#contract-PlatformOrderItem) |


## PlatformIam

### GET /api/admin/v1/me

کد واقعی: [PlatformIamController.GetMe](../MakanApp.Api/Controllers/PlatformIamController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.access`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformIamService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformMeResult](#contract-PlatformMeResult)<br>`application/json`: [PlatformMeResult](#contract-PlatformMeResult)<br>`text/json`: [PlatformMeResult](#contract-PlatformMeResult) |

### GET /api/admin/v1/role-assignments

کد واقعی: [PlatformIamController.GetAssignments](../MakanApp.Api/Controllers/PlatformIamController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.roles.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformIamService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | query | خیر | string / uuid |
| `roleKey` | query | خیر | string |
| `activeOnly` | query | خیر | boolean |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformRoleAssignmentPage](#contract-PlatformRoleAssignmentPage)<br>`application/json`: [PlatformRoleAssignmentPage](#contract-PlatformRoleAssignmentPage)<br>`text/json`: [PlatformRoleAssignmentPage](#contract-PlatformRoleAssignmentPage) |

### POST /api/admin/v1/role-assignments

کد واقعی: [PlatformIamController.GrantRole](../MakanApp.Api/Controllers/PlatformIamController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.roles.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformIamService`

بدنه درخواست: `application/json`: [GrantPlatformRoleCommand](#contract-GrantPlatformRoleCommand)<br>`text/json`: [GrantPlatformRoleCommand](#contract-GrantPlatformRoleCommand)<br>`application/*+json`: [GrantPlatformRoleCommand](#contract-GrantPlatformRoleCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformRoleMutationResult](#contract-PlatformRoleMutationResult)<br>`application/json`: [PlatformRoleMutationResult](#contract-PlatformRoleMutationResult)<br>`text/json`: [PlatformRoleMutationResult](#contract-PlatformRoleMutationResult) |
| 201 | Created | `text/plain`: [PlatformRoleMutationResult](#contract-PlatformRoleMutationResult)<br>`application/json`: [PlatformRoleMutationResult](#contract-PlatformRoleMutationResult)<br>`text/json`: [PlatformRoleMutationResult](#contract-PlatformRoleMutationResult) |

### DELETE /api/admin/v1/role-assignments/{assignmentId}

کد واقعی: [PlatformIamController.EndRole](../MakanApp.Api/Controllers/PlatformIamController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.roles.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformIamService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `assignmentId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [EndPlatformRoleAssignmentCommand](#contract-EndPlatformRoleAssignmentCommand)<br>`text/json`: [EndPlatformRoleAssignmentCommand](#contract-EndPlatformRoleAssignmentCommand)<br>`application/*+json`: [EndPlatformRoleAssignmentCommand](#contract-EndPlatformRoleAssignmentCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformRoleMutationResult](#contract-PlatformRoleMutationResult)<br>`application/json`: [PlatformRoleMutationResult](#contract-PlatformRoleMutationResult)<br>`text/json`: [PlatformRoleMutationResult](#contract-PlatformRoleMutationResult) |

### GET /api/admin/v1/roles

کد واقعی: [PlatformIamController.GetRoles](../MakanApp.Api/Controllers/PlatformIamController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.roles.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformIamService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [PlatformRoleCatalogItem](#contract-PlatformRoleCatalogItem)<br>`application/json`: آرایه از [PlatformRoleCatalogItem](#contract-PlatformRoleCatalogItem)<br>`text/json`: آرایه از [PlatformRoleCatalogItem](#contract-PlatformRoleCatalogItem) |


## PlatformMessagingMetadata

### GET /api/admin/v1/messaging/conversations

کد واقعی: [PlatformMessagingMetadataController.GetConversations](../MakanApp.Api/Controllers/PlatformMessagingMetadataController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.messaging.read_metadata`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | query | خیر | string / uuid |
| `type` | query | خیر | string |
| `scope` | query | خیر | string |
| `visibility` | query | خیر | string |
| `status` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformConversationMetadataPage](#contract-PlatformConversationMetadataPage)<br>`application/json`: [PlatformConversationMetadataPage](#contract-PlatformConversationMetadataPage)<br>`text/json`: [PlatformConversationMetadataPage](#contract-PlatformConversationMetadataPage) |


## PlatformModeration

### GET /api/admin/v1/moderation/cases

کد واقعی: [PlatformModerationController.GetCases](../MakanApp.Api/Controllers/PlatformModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.moderation.review`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `status` | query | خیر | string |
| `priority` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformModerationCasePage](#contract-PlatformModerationCasePage)<br>`application/json`: [PlatformModerationCasePage](#contract-PlatformModerationCasePage)<br>`text/json`: [PlatformModerationCasePage](#contract-PlatformModerationCasePage) |

### GET /api/admin/v1/moderation/cases/{caseId}

کد واقعی: [PlatformModerationController.GetCase](../MakanApp.Api/Controllers/PlatformModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.moderation.review`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `caseId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`application/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`text/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail) |

### POST /api/admin/v1/moderation/cases/{caseId}/actions

کد واقعی: [PlatformModerationController.ApplyAction](../MakanApp.Api/Controllers/PlatformModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.moderation.review`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `caseId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PlatformModerationActionCommand](#contract-PlatformModerationActionCommand)<br>`text/json`: [PlatformModerationActionCommand](#contract-PlatformModerationActionCommand)<br>`application/*+json`: [PlatformModerationActionCommand](#contract-PlatformModerationActionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`application/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`text/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail) |

### POST /api/admin/v1/moderation/cases/{caseId}/dismiss

کد واقعی: [PlatformModerationController.Dismiss](../MakanApp.Api/Controllers/PlatformModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.moderation.review`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `caseId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PlatformCompleteModerationCommand](#contract-PlatformCompleteModerationCommand)<br>`text/json`: [PlatformCompleteModerationCommand](#contract-PlatformCompleteModerationCommand)<br>`application/*+json`: [PlatformCompleteModerationCommand](#contract-PlatformCompleteModerationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`application/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`text/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail) |

### POST /api/admin/v1/moderation/cases/{caseId}/enforce-content-removal

کد واقعی: [PlatformModerationController.EnforceContentRemoval](../MakanApp.Api/Controllers/PlatformModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.moderation.review`، `platform.moderation.enforce`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `caseId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PlatformModerationVersionCommand](#contract-PlatformModerationVersionCommand)<br>`text/json`: [PlatformModerationVersionCommand](#contract-PlatformModerationVersionCommand)<br>`application/*+json`: [PlatformModerationVersionCommand](#contract-PlatformModerationVersionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`application/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`text/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail) |

### GET /api/admin/v1/moderation/cases/{caseId}/evidence/files/{fileAssetId}

کد واقعی: [PlatformModerationController.DownloadEvidence](../MakanApp.Api/Controllers/PlatformModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.moderation.review`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `caseId` | path | بله | string / uuid |
| `fileAssetId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | بدون بدنهٔ منتشرشده |

### POST /api/admin/v1/moderation/cases/{caseId}/expanded-context

کد واقعی: [PlatformModerationController.ExpandedContext](../MakanApp.Api/Controllers/PlatformModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.moderation.review`، `platform.moderation.enforce`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `caseId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PlatformModerationVersionCommand](#contract-PlatformModerationVersionCommand)<br>`text/json`: [PlatformModerationVersionCommand](#contract-PlatformModerationVersionCommand)<br>`application/*+json`: [PlatformModerationVersionCommand](#contract-PlatformModerationVersionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`application/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`text/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail) |

### POST /api/admin/v1/moderation/cases/{caseId}/resolve

کد واقعی: [PlatformModerationController.Resolve](../MakanApp.Api/Controllers/PlatformModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.moderation.review`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `caseId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PlatformCompleteModerationCommand](#contract-PlatformCompleteModerationCommand)<br>`text/json`: [PlatformCompleteModerationCommand](#contract-PlatformCompleteModerationCommand)<br>`application/*+json`: [PlatformCompleteModerationCommand](#contract-PlatformCompleteModerationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`application/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`text/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail) |

### POST /api/admin/v1/moderation/cases/{caseId}/start-review

کد واقعی: [PlatformModerationController.StartReview](../MakanApp.Api/Controllers/PlatformModerationController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.moderation.review`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformEducationSafetyService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `caseId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PlatformModerationVersionCommand](#contract-PlatformModerationVersionCommand)<br>`text/json`: [PlatformModerationVersionCommand](#contract-PlatformModerationVersionCommand)<br>`application/*+json`: [PlatformModerationVersionCommand](#contract-PlatformModerationVersionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`application/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail)<br>`text/json`: [PlatformModerationCaseDetail](#contract-PlatformModerationCaseDetail) |


## PlatformOrganizations

### GET /api/admin/v1/organizations

کد واقعی: [PlatformOrganizationsController.GetOrganizations](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.organizations.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `search` | query | خیر | string |
| `status` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |
| `sortBy` | query | خیر | string |
| `sortDirection` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformOrganizationPage](#contract-PlatformOrganizationPage)<br>`application/json`: [PlatformOrganizationPage](#contract-PlatformOrganizationPage)<br>`text/json`: [PlatformOrganizationPage](#contract-PlatformOrganizationPage) |

### POST /api/admin/v1/organizations

کد واقعی: [PlatformOrganizationsController.CreateOrganization](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.organizations.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

بدنه درخواست: `application/json`: [CreatePlatformOrganizationCommand](#contract-CreatePlatformOrganizationCommand)<br>`text/json`: [CreatePlatformOrganizationCommand](#contract-CreatePlatformOrganizationCommand)<br>`application/*+json`: [CreatePlatformOrganizationCommand](#contract-CreatePlatformOrganizationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformProvisioningResult](#contract-PlatformProvisioningResult)<br>`application/json`: [PlatformProvisioningResult](#contract-PlatformProvisioningResult)<br>`text/json`: [PlatformProvisioningResult](#contract-PlatformProvisioningResult) |

### GET /api/admin/v1/organizations/{organizationId}

کد واقعی: [PlatformOrganizationsController.GetOrganization](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.organizations.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformOrganizationDetail](#contract-PlatformOrganizationDetail)<br>`application/json`: [PlatformOrganizationDetail](#contract-PlatformOrganizationDetail)<br>`text/json`: [PlatformOrganizationDetail](#contract-PlatformOrganizationDetail) |

### POST /api/admin/v1/organizations/{organizationId}/invitations

کد واقعی: [PlatformOrganizationsController.CreateInvitation](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.organizations.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreatePlatformInvitationCommand](#contract-CreatePlatformInvitationCommand)<br>`text/json`: [CreatePlatformInvitationCommand](#contract-CreatePlatformInvitationCommand)<br>`application/*+json`: [CreatePlatformInvitationCommand](#contract-CreatePlatformInvitationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformInvitationResult](#contract-PlatformInvitationResult)<br>`application/json`: [PlatformInvitationResult](#contract-PlatformInvitationResult)<br>`text/json`: [PlatformInvitationResult](#contract-PlatformInvitationResult) |

### GET /api/admin/v1/organizations/{organizationId}/memberships

کد واقعی: [PlatformOrganizationsController.GetMemberships](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.organizations.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | path | بله | string / uuid |
| `activeOnly` | query | خیر | boolean |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformMembershipPage](#contract-PlatformMembershipPage)<br>`application/json`: [PlatformMembershipPage](#contract-PlatformMembershipPage)<br>`text/json`: [PlatformMembershipPage](#contract-PlatformMembershipPage) |

### DELETE /api/admin/v1/organizations/{organizationId}/memberships/{membershipId}

کد واقعی: [PlatformOrganizationsController.EndMembership](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.organizations.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | path | بله | string / uuid |
| `membershipId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [EndPlatformMembershipCommand](#contract-EndPlatformMembershipCommand)<br>`text/json`: [EndPlatformMembershipCommand](#contract-EndPlatformMembershipCommand)<br>`application/*+json`: [EndPlatformMembershipCommand](#contract-EndPlatformMembershipCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformMembershipMutationResult](#contract-PlatformMembershipMutationResult)<br>`application/json`: [PlatformMembershipMutationResult](#contract-PlatformMembershipMutationResult)<br>`text/json`: [PlatformMembershipMutationResult](#contract-PlatformMembershipMutationResult) |

### PUT /api/admin/v1/organizations/{organizationId}/memberships/{membershipId}/role

کد واقعی: [PlatformOrganizationsController.ChangeRole](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.organizations.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | path | بله | string / uuid |
| `membershipId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [ChangePlatformMembershipRoleCommand](#contract-ChangePlatformMembershipRoleCommand)<br>`text/json`: [ChangePlatformMembershipRoleCommand](#contract-ChangePlatformMembershipRoleCommand)<br>`application/*+json`: [ChangePlatformMembershipRoleCommand](#contract-ChangePlatformMembershipRoleCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformMembershipMutationResult](#contract-PlatformMembershipMutationResult)<br>`application/json`: [PlatformMembershipMutationResult](#contract-PlatformMembershipMutationResult)<br>`text/json`: [PlatformMembershipMutationResult](#contract-PlatformMembershipMutationResult) |

### POST /api/admin/v1/organizations/{organizationId}/reactivate

کد واقعی: [PlatformOrganizationsController.Reactivate](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.organizations.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PlatformOrganizationStateCommand](#contract-PlatformOrganizationStateCommand)<br>`text/json`: [PlatformOrganizationStateCommand](#contract-PlatformOrganizationStateCommand)<br>`application/*+json`: [PlatformOrganizationStateCommand](#contract-PlatformOrganizationStateCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformOrganizationMutationResult](#contract-PlatformOrganizationMutationResult)<br>`application/json`: [PlatformOrganizationMutationResult](#contract-PlatformOrganizationMutationResult)<br>`text/json`: [PlatformOrganizationMutationResult](#contract-PlatformOrganizationMutationResult) |

### POST /api/admin/v1/organizations/{organizationId}/suspend

کد واقعی: [PlatformOrganizationsController.Suspend](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.organizations.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PlatformOrganizationStateCommand](#contract-PlatformOrganizationStateCommand)<br>`text/json`: [PlatformOrganizationStateCommand](#contract-PlatformOrganizationStateCommand)<br>`application/*+json`: [PlatformOrganizationStateCommand](#contract-PlatformOrganizationStateCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformOrganizationMutationResult](#contract-PlatformOrganizationMutationResult)<br>`application/json`: [PlatformOrganizationMutationResult](#contract-PlatformOrganizationMutationResult)<br>`text/json`: [PlatformOrganizationMutationResult](#contract-PlatformOrganizationMutationResult) |

### GET /api/admin/v1/organizations/{organizationId}/suspension-impact

کد واقعی: [PlatformOrganizationsController.GetSuspensionImpact](../MakanApp.Api/Controllers/PlatformOrganizationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.organizations.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `organizationId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformOrganizationImpactPreview](#contract-PlatformOrganizationImpactPreview)<br>`application/json`: [PlatformOrganizationImpactPreview](#contract-PlatformOrganizationImpactPreview)<br>`text/json`: [PlatformOrganizationImpactPreview](#contract-PlatformOrganizationImpactPreview) |


## PlatformProvisioning

### GET /api/admin/v1/provisioning

کد واقعی: [PlatformProvisioningController.GetRequests](../MakanApp.Api/Controllers/PlatformProvisioningController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.provisioning.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `status` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformProvisioningPage](#contract-PlatformProvisioningPage)<br>`application/json`: [PlatformProvisioningPage](#contract-PlatformProvisioningPage)<br>`text/json`: [PlatformProvisioningPage](#contract-PlatformProvisioningPage) |

### POST /api/admin/v1/provisioning

کد واقعی: [PlatformProvisioningController.Provision](../MakanApp.Api/Controllers/PlatformProvisioningController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.provisioning.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

بدنه درخواست: `application/json`: [ProvisionOrganizationCommand](#contract-ProvisionOrganizationCommand)<br>`text/json`: [ProvisionOrganizationCommand](#contract-ProvisionOrganizationCommand)<br>`application/*+json`: [ProvisionOrganizationCommand](#contract-ProvisionOrganizationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformProvisioningResult](#contract-PlatformProvisioningResult)<br>`application/json`: [PlatformProvisioningResult](#contract-PlatformProvisioningResult)<br>`text/json`: [PlatformProvisioningResult](#contract-PlatformProvisioningResult) |


## PlatformSystemOperations

### GET /api/admin/v1/operations/health

کد واقعی: [PlatformSystemOperationsController.GetHealth](../MakanApp.Api/Controllers/PlatformSystemOperationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.operations.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformSystemHealth](#contract-PlatformSystemHealth)<br>`application/json`: [PlatformSystemHealth](#contract-PlatformSystemHealth)<br>`text/json`: [PlatformSystemHealth](#contract-PlatformSystemHealth) |

### GET /api/admin/v1/operations/jobs

کد واقعی: [PlatformSystemOperationsController.GetJobs](../MakanApp.Api/Controllers/PlatformSystemOperationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.operations.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformJobOperations](#contract-PlatformJobOperations)<br>`application/json`: [PlatformJobOperations](#contract-PlatformJobOperations)<br>`text/json`: [PlatformJobOperations](#contract-PlatformJobOperations) |

### GET /api/admin/v1/operations/notifications

کد واقعی: [PlatformSystemOperationsController.GetNotifications](../MakanApp.Api/Controllers/PlatformSystemOperationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.notifications.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformCommerceOperationsService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformNotificationOperations](#contract-PlatformNotificationOperations)<br>`application/json`: [PlatformNotificationOperations](#contract-PlatformNotificationOperations)<br>`text/json`: [PlatformNotificationOperations](#contract-PlatformNotificationOperations) |


## PlatformUsers

### GET /api/admin/v1/users

کد واقعی: [PlatformUsersController.GetUsers](../MakanApp.Api/Controllers/PlatformUsersController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.users.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `search` | query | خیر | string |
| `status` | query | خیر | string |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |
| `sortBy` | query | خیر | string |
| `sortDirection` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformUserPage](#contract-PlatformUserPage)<br>`application/json`: [PlatformUserPage](#contract-PlatformUserPage)<br>`text/json`: [PlatformUserPage](#contract-PlatformUserPage) |

### GET /api/admin/v1/users/{userId}

کد واقعی: [PlatformUsersController.GetUser](../MakanApp.Api/Controllers/PlatformUsersController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.users.read`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformUserDetail](#contract-PlatformUserDetail)<br>`application/json`: [PlatformUserDetail](#contract-PlatformUserDetail)<br>`text/json`: [PlatformUserDetail](#contract-PlatformUserDetail) |

### POST /api/admin/v1/users/{userId}/reactivate

کد واقعی: [PlatformUsersController.Reactivate](../MakanApp.Api/Controllers/PlatformUsersController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.users.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PlatformUserStateCommand](#contract-PlatformUserStateCommand)<br>`text/json`: [PlatformUserStateCommand](#contract-PlatformUserStateCommand)<br>`application/*+json`: [PlatformUserStateCommand](#contract-PlatformUserStateCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformUserMutationResult](#contract-PlatformUserMutationResult)<br>`application/json`: [PlatformUserMutationResult](#contract-PlatformUserMutationResult)<br>`text/json`: [PlatformUserMutationResult](#contract-PlatformUserMutationResult) |

### POST /api/admin/v1/users/{userId}/sessions/revoke

کد واقعی: [PlatformUsersController.RevokeAllSessions](../MakanApp.Api/Controllers/PlatformUsersController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.sessions.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [RevokePlatformSessionsCommand](#contract-RevokePlatformSessionsCommand)<br>`text/json`: [RevokePlatformSessionsCommand](#contract-RevokePlatformSessionsCommand)<br>`application/*+json`: [RevokePlatformSessionsCommand](#contract-RevokePlatformSessionsCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformSessionRevocationResult](#contract-PlatformSessionRevocationResult)<br>`application/json`: [PlatformSessionRevocationResult](#contract-PlatformSessionRevocationResult)<br>`text/json`: [PlatformSessionRevocationResult](#contract-PlatformSessionRevocationResult) |

### POST /api/admin/v1/users/{userId}/sessions/{sessionId}/revoke

کد واقعی: [PlatformUsersController.RevokeSession](../MakanApp.Api/Controllers/PlatformUsersController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.sessions.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |
| `sessionId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [RevokePlatformSessionsCommand](#contract-RevokePlatformSessionsCommand)<br>`text/json`: [RevokePlatformSessionsCommand](#contract-RevokePlatformSessionsCommand)<br>`application/*+json`: [RevokePlatformSessionsCommand](#contract-RevokePlatformSessionsCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformSessionRevocationResult](#contract-PlatformSessionRevocationResult)<br>`application/json`: [PlatformSessionRevocationResult](#contract-PlatformSessionRevocationResult)<br>`text/json`: [PlatformSessionRevocationResult](#contract-PlatformSessionRevocationResult) |

### POST /api/admin/v1/users/{userId}/suspend

کد واقعی: [PlatformUsersController.Suspend](../MakanApp.Api/Controllers/PlatformUsersController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

policy: `platform.users.manage`

وابستگی‌های واقعی Controller: `MakanApp.Application.Platform.IPlatformOperationsService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `userId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [PlatformUserStateCommand](#contract-PlatformUserStateCommand)<br>`text/json`: [PlatformUserStateCommand](#contract-PlatformUserStateCommand)<br>`application/*+json`: [PlatformUserStateCommand](#contract-PlatformUserStateCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [PlatformUserMutationResult](#contract-PlatformUserMutationResult)<br>`application/json`: [PlatformUserMutationResult](#contract-PlatformUserMutationResult)<br>`text/json`: [PlatformUserMutationResult](#contract-PlatformUserMutationResult) |


## Profile

### GET /api/v1/me

کد واقعی: [ProfileController.GetCurrentUser](../MakanApp.Api/Controllers/ProfileController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`، `MakanApp.Application.Identity.ProfilePhotoService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [CurrentUserResult](#contract-CurrentUserResult)<br>`application/json`: [CurrentUserResult](#contract-CurrentUserResult)<br>`text/json`: [CurrentUserResult](#contract-CurrentUserResult) |

### PATCH /api/v1/me/profile

کد واقعی: [ProfileController.CompleteProfile](../MakanApp.Api/Controllers/ProfileController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`، `MakanApp.Application.Identity.ProfilePhotoService`

بدنه درخواست: `application/json`: [CompleteProfileCommand](#contract-CompleteProfileCommand)<br>`text/json`: [CompleteProfileCommand](#contract-CompleteProfileCommand)<br>`application/*+json`: [CompleteProfileCommand](#contract-CompleteProfileCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [CurrentUserResult](#contract-CurrentUserResult)<br>`application/json`: [CurrentUserResult](#contract-CurrentUserResult)<br>`text/json`: [CurrentUserResult](#contract-CurrentUserResult) |

### DELETE /api/v1/me/profile-photo

کد واقعی: [ProfileController.RemoveProfilePhoto](../MakanApp.Api/Controllers/ProfileController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`، `MakanApp.Application.Identity.ProfilePhotoService`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 204 | No Content | بدون بدنهٔ منتشرشده |

### PUT /api/v1/me/profile-photo

کد واقعی: [ProfileController.SetProfilePhoto](../MakanApp.Api/Controllers/ProfileController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Identity.IIdentityService`، `MakanApp.Application.Identity.ProfilePhotoService`

بدنه درخواست: `application/json`: [SetProfilePhotoCommand](#contract-SetProfilePhotoCommand)<br>`text/json`: [SetProfilePhotoCommand](#contract-SetProfilePhotoCommand)<br>`application/*+json`: [SetProfilePhotoCommand](#contract-SetProfilePhotoCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ProfilePhotoResult3](#contract-ProfilePhotoResult3)<br>`application/json`: [ProfilePhotoResult3](#contract-ProfilePhotoResult3)<br>`text/json`: [ProfilePhotoResult3](#contract-ProfilePhotoResult3) |


## Reports

### POST /api/v1/reports/exports

کد واقعی: [ReportsController.CreateExport](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

بدنه درخواست: `application/json`: [CreateReportExportCommand](#contract-CreateReportExportCommand)<br>`text/json`: [CreateReportExportCommand](#contract-CreateReportExportCommand)<br>`application/*+json`: [CreateReportExportCommand](#contract-CreateReportExportCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 202 | Accepted | `text/plain`: [ReportExportJobResult](#contract-ReportExportJobResult)<br>`application/json`: [ReportExportJobResult](#contract-ReportExportJobResult)<br>`text/json`: [ReportExportJobResult](#contract-ReportExportJobResult) |

### GET /api/v1/reports/exports/{jobId}

کد واقعی: [ReportsController.GetExportStatus](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `jobId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportExportJobResult](#contract-ReportExportJobResult)<br>`application/json`: [ReportExportJobResult](#contract-ReportExportJobResult)<br>`text/json`: [ReportExportJobResult](#contract-ReportExportJobResult) |

### GET /api/v1/reports/exports/{jobId}/download

کد واقعی: [ReportsController.DownloadExport](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `jobId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [FileStreamResult](#contract-FileStreamResult)<br>`application/json`: [FileStreamResult](#contract-FileStreamResult)<br>`text/json`: [FileStreamResult](#contract-FileStreamResult) |

### GET /api/v1/reports/manager/assessments

کد واقعی: [ReportsController.GetManagerAssessments](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `CourseId` | query | خیر | string / uuid |
| `AcademicPeriodId` | query | خیر | string / uuid |
| `Kind` | query | خیر | string |
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfManagerAssessmentReportItem](#contract-ReportPageOfManagerAssessmentReportItem)<br>`application/json`: [ReportPageOfManagerAssessmentReportItem](#contract-ReportPageOfManagerAssessmentReportItem)<br>`text/json`: [ReportPageOfManagerAssessmentReportItem](#contract-ReportPageOfManagerAssessmentReportItem) |

### GET /api/v1/reports/manager/attendance

کد واقعی: [ReportsController.GetManagerAttendance](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfManagerAttendanceReportItem](#contract-ReportPageOfManagerAttendanceReportItem)<br>`application/json`: [ReportPageOfManagerAttendanceReportItem](#contract-ReportPageOfManagerAttendanceReportItem)<br>`text/json`: [ReportPageOfManagerAttendanceReportItem](#contract-ReportPageOfManagerAttendanceReportItem) |

### GET /api/v1/reports/manager/classes/{classId}

کد واقعی: [ReportsController.GetManagerClass](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ManagerClassPerformanceDto](#contract-ManagerClassPerformanceDto)<br>`application/json`: [ManagerClassPerformanceDto](#contract-ManagerClassPerformanceDto)<br>`text/json`: [ManagerClassPerformanceDto](#contract-ManagerClassPerformanceDto) |

### GET /api/v1/reports/manager/enrollments

کد واقعی: [ReportsController.GetManagerEnrollments](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `CourseId` | query | خیر | string / uuid |
| `AcademicPeriodId` | query | خیر | string / uuid |
| `Status` | query | خیر | string |
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfManagerEnrollmentReportItem](#contract-ReportPageOfManagerEnrollmentReportItem)<br>`application/json`: [ReportPageOfManagerEnrollmentReportItem](#contract-ReportPageOfManagerEnrollmentReportItem)<br>`text/json`: [ReportPageOfManagerEnrollmentReportItem](#contract-ReportPageOfManagerEnrollmentReportItem) |

### GET /api/v1/reports/manager/teacher-workload

کد واقعی: [ReportsController.GetManagerTeacherWorkload](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `CourseId` | query | خیر | string / uuid |
| `AcademicPeriodId` | query | خیر | string / uuid |
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfManagerTeacherWorkloadReportItem](#contract-ReportPageOfManagerTeacherWorkloadReportItem)<br>`application/json`: [ReportPageOfManagerTeacherWorkloadReportItem](#contract-ReportPageOfManagerTeacherWorkloadReportItem)<br>`text/json`: [ReportPageOfManagerTeacherWorkloadReportItem](#contract-ReportPageOfManagerTeacherWorkloadReportItem) |

### GET /api/v1/reports/parent/assignments

کد واقعی: [ReportsController.GetParentAssignments](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfParentAssignmentReportItem](#contract-ReportPageOfParentAssignmentReportItem)<br>`application/json`: [ReportPageOfParentAssignmentReportItem](#contract-ReportPageOfParentAssignmentReportItem)<br>`text/json`: [ReportPageOfParentAssignmentReportItem](#contract-ReportPageOfParentAssignmentReportItem) |

### GET /api/v1/reports/parent/attendance

کد واقعی: [ReportsController.GetParentAttendance](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AttendanceReportPageOfParentAttendanceReportItem](#contract-AttendanceReportPageOfParentAttendanceReportItem)<br>`application/json`: [AttendanceReportPageOfParentAttendanceReportItem](#contract-AttendanceReportPageOfParentAttendanceReportItem)<br>`text/json`: [AttendanceReportPageOfParentAttendanceReportItem](#contract-AttendanceReportPageOfParentAttendanceReportItem) |

### GET /api/v1/reports/parent/exams

کد واقعی: [ReportsController.GetParentExams](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfParentExamReportItem](#contract-ReportPageOfParentExamReportItem)<br>`application/json`: [ReportPageOfParentExamReportItem](#contract-ReportPageOfParentExamReportItem)<br>`text/json`: [ReportPageOfParentExamReportItem](#contract-ReportPageOfParentExamReportItem) |

### GET /api/v1/reports/student/assignments

کد واقعی: [ReportsController.GetStudentAssignments](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfStudentAssignmentReportItem](#contract-ReportPageOfStudentAssignmentReportItem)<br>`application/json`: [ReportPageOfStudentAssignmentReportItem](#contract-ReportPageOfStudentAssignmentReportItem)<br>`text/json`: [ReportPageOfStudentAssignmentReportItem](#contract-ReportPageOfStudentAssignmentReportItem) |

### GET /api/v1/reports/student/attendance

کد واقعی: [ReportsController.GetStudentAttendance](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AttendanceReportPageOfStudentAttendanceReportItem](#contract-AttendanceReportPageOfStudentAttendanceReportItem)<br>`application/json`: [AttendanceReportPageOfStudentAttendanceReportItem](#contract-AttendanceReportPageOfStudentAttendanceReportItem)<br>`text/json`: [AttendanceReportPageOfStudentAttendanceReportItem](#contract-AttendanceReportPageOfStudentAttendanceReportItem) |

### GET /api/v1/reports/student/exams

کد واقعی: [ReportsController.GetStudentExams](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfStudentExamReportItem](#contract-ReportPageOfStudentExamReportItem)<br>`application/json`: [ReportPageOfStudentExamReportItem](#contract-ReportPageOfStudentExamReportItem)<br>`text/json`: [ReportPageOfStudentExamReportItem](#contract-ReportPageOfStudentExamReportItem) |

### GET /api/v1/reports/student/grades

کد واقعی: [ReportsController.GetStudentGrades](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfStudentReleasedGradeReportItem](#contract-ReportPageOfStudentReleasedGradeReportItem)<br>`application/json`: [ReportPageOfStudentReleasedGradeReportItem](#contract-ReportPageOfStudentReleasedGradeReportItem)<br>`text/json`: [ReportPageOfStudentReleasedGradeReportItem](#contract-ReportPageOfStudentReleasedGradeReportItem) |

### GET /api/v1/reports/teacher/classes/{classId}/assignments

کد واقعی: [ReportsController.GetTeacherAssignments](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfTeacherAssignmentReportItem](#contract-ReportPageOfTeacherAssignmentReportItem)<br>`application/json`: [ReportPageOfTeacherAssignmentReportItem](#contract-ReportPageOfTeacherAssignmentReportItem)<br>`text/json`: [ReportPageOfTeacherAssignmentReportItem](#contract-ReportPageOfTeacherAssignmentReportItem) |

### GET /api/v1/reports/teacher/classes/{classId}/attendance

کد واقعی: [ReportsController.GetTeacherAttendance](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AttendanceReportPageOfTeacherAttendanceReportItem](#contract-AttendanceReportPageOfTeacherAttendanceReportItem)<br>`application/json`: [AttendanceReportPageOfTeacherAttendanceReportItem](#contract-AttendanceReportPageOfTeacherAttendanceReportItem)<br>`text/json`: [AttendanceReportPageOfTeacherAttendanceReportItem](#contract-AttendanceReportPageOfTeacherAttendanceReportItem) |

### GET /api/v1/reports/teacher/classes/{classId}/exams

کد واقعی: [ReportsController.GetTeacherExams](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `PageNumber` | query | خیر | integer / int32 |
| `PageSize` | query | خیر | integer / int32 |
| `FromUtc` | query | خیر | string / date-time |
| `ToUtc` | query | خیر | string / date-time |
| `ClassId` | query | خیر | string / uuid |
| `Sort` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ReportPageOfTeacherExamReportItem](#contract-ReportPageOfTeacherExamReportItem)<br>`application/json`: [ReportPageOfTeacherExamReportItem](#contract-ReportPageOfTeacherExamReportItem)<br>`text/json`: [ReportPageOfTeacherExamReportItem](#contract-ReportPageOfTeacherExamReportItem) |

### GET /api/v1/reports/teacher/classes/{classId}/learners/{learnerId}

کد واقعی: [ReportsController.GetTeacherLearner](../MakanApp.Api/Controllers/ReportsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Reports.Detailed.IDetailedReportService`، `MakanApp.Application.Reports.Exports.IReportExportService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |
| `learnerId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [TeacherLearnerOverviewDto](#contract-TeacherLearnerOverviewDto)<br>`application/json`: [TeacherLearnerOverviewDto](#contract-TeacherLearnerOverviewDto)<br>`text/json`: [TeacherLearnerOverviewDto](#contract-TeacherLearnerOverviewDto) |


## SavedMessageLabels

### GET /api/v1/conversations/saved/messages/{messageId}/labels

کد واقعی: [SavedMessageLabelsController.Get](../MakanApp.Api/Controllers/SavedMessageLabelsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.MessagingPresentationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `messageId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SavedMessageLabelsResult](#contract-SavedMessageLabelsResult)<br>`application/json`: [SavedMessageLabelsResult](#contract-SavedMessageLabelsResult)<br>`text/json`: [SavedMessageLabelsResult](#contract-SavedMessageLabelsResult) |

### PUT /api/v1/conversations/saved/messages/{messageId}/labels

کد واقعی: [SavedMessageLabelsController.Replace](../MakanApp.Api/Controllers/SavedMessageLabelsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.MessagingPresentationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `messageId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [ReplaceSavedLabelsCommand](#contract-ReplaceSavedLabelsCommand)<br>`text/json`: [ReplaceSavedLabelsCommand](#contract-ReplaceSavedLabelsCommand)<br>`application/*+json`: [ReplaceSavedLabelsCommand](#contract-ReplaceSavedLabelsCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SavedMessageLabelsResult](#contract-SavedMessageLabelsResult)<br>`application/json`: [SavedMessageLabelsResult](#contract-SavedMessageLabelsResult)<br>`text/json`: [SavedMessageLabelsResult](#contract-SavedMessageLabelsResult) |


## Schedule

### POST /api/v1/academic/classes/{classId}/schedule-rules

کد واقعی: [ScheduleController.CreateRule](../MakanApp.Api/Controllers/ScheduleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateScheduleRuleCommand](#contract-CreateScheduleRuleCommand)<br>`text/json`: [CreateScheduleRuleCommand](#contract-CreateScheduleRuleCommand)<br>`application/*+json`: [CreateScheduleRuleCommand](#contract-CreateScheduleRuleCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [ScheduleRuleResult](#contract-ScheduleRuleResult)<br>`application/json`: [ScheduleRuleResult](#contract-ScheduleRuleResult)<br>`text/json`: [ScheduleRuleResult](#contract-ScheduleRuleResult) |

### GET /api/v1/academic/schedule

کد واقعی: [ScheduleController.GetSchedule](../MakanApp.Api/Controllers/ScheduleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `fromUtc` | query | بله | string / date-time |
| `toUtc` | query | بله | string / date-time |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [SessionResult](#contract-SessionResult)<br>`application/json`: آرایه از [SessionResult](#contract-SessionResult)<br>`text/json`: آرایه از [SessionResult](#contract-SessionResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### PATCH /api/v1/academic/schedule-rules/{scheduleRuleId}

کد واقعی: [ScheduleController.UpdateRule](../MakanApp.Api/Controllers/ScheduleController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `scheduleRuleId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateScheduleRuleCommand](#contract-UpdateScheduleRuleCommand)<br>`text/json`: [UpdateScheduleRuleCommand](#contract-UpdateScheduleRuleCommand)<br>`application/*+json`: [UpdateScheduleRuleCommand](#contract-UpdateScheduleRuleCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ScheduleRuleResult](#contract-ScheduleRuleResult)<br>`application/json`: [ScheduleRuleResult](#contract-ScheduleRuleResult)<br>`text/json`: [ScheduleRuleResult](#contract-ScheduleRuleResult) |


## ScheduledMessages

### GET /api/v1/conversations/{conversationId}/scheduled-messages

کد واقعی: [ScheduledMessagesController.Get](../MakanApp.Api/Controllers/ScheduledMessagesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ScheduledMessageService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ScheduledMessagePageResult](#contract-ScheduledMessagePageResult)<br>`application/json`: [ScheduledMessagePageResult](#contract-ScheduledMessagePageResult)<br>`text/json`: [ScheduledMessagePageResult](#contract-ScheduledMessagePageResult) |

### POST /api/v1/conversations/{conversationId}/scheduled-messages

کد واقعی: [ScheduledMessagesController.Schedule](../MakanApp.Api/Controllers/ScheduledMessagesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ScheduledMessageService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [ScheduleMessageCommand](#contract-ScheduleMessageCommand)<br>`text/json`: [ScheduleMessageCommand](#contract-ScheduleMessageCommand)<br>`application/*+json`: [ScheduleMessageCommand](#contract-ScheduleMessageCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ScheduledMessageResult](#contract-ScheduledMessageResult)<br>`application/json`: [ScheduledMessageResult](#contract-ScheduledMessageResult)<br>`text/json`: [ScheduledMessageResult](#contract-ScheduledMessageResult) |

### POST /api/v1/conversations/{conversationId}/scheduled-messages/{id}/cancel

کد واقعی: [ScheduledMessagesController.Cancel](../MakanApp.Api/Controllers/ScheduledMessagesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Messaging.ScheduledMessageService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `conversationId` | path | بله | string / uuid |
| `id` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CancelScheduledMessageCommand](#contract-CancelScheduledMessageCommand)<br>`text/json`: [CancelScheduledMessageCommand](#contract-CancelScheduledMessageCommand)<br>`application/*+json`: [CancelScheduledMessageCommand](#contract-CancelScheduledMessageCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [ScheduledMessageResult](#contract-ScheduledMessageResult)<br>`application/json`: [ScheduledMessageResult](#contract-ScheduledMessageResult)<br>`text/json`: [ScheduledMessageResult](#contract-ScheduledMessageResult) |


## SchoolGuardianInvitations

### POST /api/v1/organization/guardian-invitations/{invitationId}/revoke

کد واقعی: [SchoolGuardianInvitationsController.Revoke](../MakanApp.Api/Controllers/SchoolGuardianInvitationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.SchoolGuardianInvitationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `invitationId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`text/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`application/*+json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolGuardianInvitationResult](#contract-SchoolGuardianInvitationResult)<br>`application/json`: [SchoolGuardianInvitationResult](#contract-SchoolGuardianInvitationResult)<br>`text/json`: [SchoolGuardianInvitationResult](#contract-SchoolGuardianInvitationResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 404 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 409 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 412 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 429 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |

### POST /api/v1/organization/people/{learnerId}/guardian-invitations

کد واقعی: [SchoolGuardianInvitationsController.Create](../MakanApp.Api/Controllers/SchoolGuardianInvitationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.SchoolGuardianInvitationService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `learnerId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateSchoolGuardianInvitationCommand](#contract-CreateSchoolGuardianInvitationCommand)<br>`text/json`: [CreateSchoolGuardianInvitationCommand](#contract-CreateSchoolGuardianInvitationCommand)<br>`application/*+json`: [CreateSchoolGuardianInvitationCommand](#contract-CreateSchoolGuardianInvitationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [SchoolGuardianInvitationResult](#contract-SchoolGuardianInvitationResult)<br>`application/json`: [SchoolGuardianInvitationResult](#contract-SchoolGuardianInvitationResult)<br>`text/json`: [SchoolGuardianInvitationResult](#contract-SchoolGuardianInvitationResult) |
| 200 | OK | `text/plain`: [SchoolGuardianInvitationResult](#contract-SchoolGuardianInvitationResult)<br>`application/json`: [SchoolGuardianInvitationResult](#contract-SchoolGuardianInvitationResult)<br>`text/json`: [SchoolGuardianInvitationResult](#contract-SchoolGuardianInvitationResult) |
| 400 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 401 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 403 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 404 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 409 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 412 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |
| 429 | ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است. | `application/problem+json`: object |


## SchoolGuardianRelations

### GET /api/v1/organization/guardian-relations

کد واقعی: [SchoolGuardianRelationsController.List](../MakanApp.Api/Controllers/SchoolGuardianRelationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.IGuardianManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `learnerId` | query | خیر | string / uuid |
| `page` | query | خیر | integer / int32 |
| `pageSize` | query | خیر | integer / int32 |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolGuardianRelationsPage](#contract-SchoolGuardianRelationsPage)<br>`application/json`: [SchoolGuardianRelationsPage](#contract-SchoolGuardianRelationsPage)<br>`text/json`: [SchoolGuardianRelationsPage](#contract-SchoolGuardianRelationsPage) |

### POST /api/v1/organization/guardian-relations

کد واقعی: [SchoolGuardianRelationsController.Create](../MakanApp.Api/Controllers/SchoolGuardianRelationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.IGuardianManagementService`

بدنه درخواست: `application/json`: [CreateSchoolGuardianRelationCommand](#contract-CreateSchoolGuardianRelationCommand)<br>`text/json`: [CreateSchoolGuardianRelationCommand](#contract-CreateSchoolGuardianRelationCommand)<br>`application/*+json`: [CreateSchoolGuardianRelationCommand](#contract-CreateSchoolGuardianRelationCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`application/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`text/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto) |
| 200 | OK | `text/plain`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`application/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`text/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto) |

### GET /api/v1/organization/guardian-relations/{id}

کد واقعی: [SchoolGuardianRelationsController.Get](../MakanApp.Api/Controllers/SchoolGuardianRelationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.IGuardianManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `id` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`application/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`text/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto) |

### POST /api/v1/organization/guardian-relations/{id}/activate

کد واقعی: [SchoolGuardianRelationsController.Activate](../MakanApp.Api/Controllers/SchoolGuardianRelationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.IGuardianManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `id` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`text/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`application/*+json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`application/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`text/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto) |

### POST /api/v1/organization/guardian-relations/{id}/end

کد واقعی: [SchoolGuardianRelationsController.End](../MakanApp.Api/Controllers/SchoolGuardianRelationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.IGuardianManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `id` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`text/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`application/*+json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`application/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`text/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto) |

### POST /api/v1/organization/guardian-relations/{id}/revoke

کد واقعی: [SchoolGuardianRelationsController.Revoke](../MakanApp.Api/Controllers/SchoolGuardianRelationsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Guardian.IGuardianManagementService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `id` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`text/json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)<br>`application/*+json`: [OnboardingStateCommand](#contract-OnboardingStateCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`application/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto)<br>`text/json`: [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto) |


## Sessions

### POST /api/v1/academic/classes/{classId}/sessions

کد واقعی: [SessionsController.Create](../MakanApp.Api/Controllers/SessionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `classId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CreateSessionCommand](#contract-CreateSessionCommand)<br>`text/json`: [CreateSessionCommand](#contract-CreateSessionCommand)<br>`application/*+json`: [CreateSessionCommand](#contract-CreateSessionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [SessionResult](#contract-SessionResult)<br>`application/json`: [SessionResult](#contract-SessionResult)<br>`text/json`: [SessionResult](#contract-SessionResult) |

### GET /api/v1/academic/sessions/{sessionId}

کد واقعی: [SessionsController.Get](../MakanApp.Api/Controllers/SessionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `sessionId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SessionResult](#contract-SessionResult)<br>`application/json`: [SessionResult](#contract-SessionResult)<br>`text/json`: [SessionResult](#contract-SessionResult) |

### PATCH /api/v1/academic/sessions/{sessionId}

کد واقعی: [SessionsController.Update](../MakanApp.Api/Controllers/SessionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `sessionId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [UpdateSessionCommand](#contract-UpdateSessionCommand)<br>`text/json`: [UpdateSessionCommand](#contract-UpdateSessionCommand)<br>`application/*+json`: [UpdateSessionCommand](#contract-UpdateSessionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SessionResult](#contract-SessionResult)<br>`application/json`: [SessionResult](#contract-SessionResult)<br>`text/json`: [SessionResult](#contract-SessionResult) |

### GET /api/v1/academic/sessions/{sessionId}/attendance

کد واقعی: [SessionsController.GetAttendance](../MakanApp.Api/Controllers/SessionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `sessionId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SessionAttendanceResult](#contract-SessionAttendanceResult)<br>`application/json`: [SessionAttendanceResult](#contract-SessionAttendanceResult)<br>`text/json`: [SessionAttendanceResult](#contract-SessionAttendanceResult) |

### PUT /api/v1/academic/sessions/{sessionId}/attendance

کد واقعی: [SessionsController.RecordAttendance](../MakanApp.Api/Controllers/SessionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `sessionId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [RecordAttendanceCommand](#contract-RecordAttendanceCommand)<br>`text/json`: [RecordAttendanceCommand](#contract-RecordAttendanceCommand)<br>`application/*+json`: [RecordAttendanceCommand](#contract-RecordAttendanceCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [AttendanceEntryResult](#contract-AttendanceEntryResult)<br>`application/json`: آرایه از [AttendanceEntryResult](#contract-AttendanceEntryResult)<br>`text/json`: آرایه از [AttendanceEntryResult](#contract-AttendanceEntryResult) |

### PATCH /api/v1/academic/sessions/{sessionId}/attendance/{attendanceId}

کد واقعی: [SessionsController.CorrectAttendance](../MakanApp.Api/Controllers/SessionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `sessionId` | path | بله | string / uuid |
| `attendanceId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [CorrectAttendanceCommand](#contract-CorrectAttendanceCommand)<br>`text/json`: [CorrectAttendanceCommand](#contract-CorrectAttendanceCommand)<br>`application/*+json`: [CorrectAttendanceCommand](#contract-CorrectAttendanceCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AttendanceEntryResult](#contract-AttendanceEntryResult)<br>`application/json`: [AttendanceEntryResult](#contract-AttendanceEntryResult)<br>`text/json`: [AttendanceEntryResult](#contract-AttendanceEntryResult) |

### POST /api/v1/academic/sessions/{sessionId}/cancel

کد واقعی: [SessionsController.Cancel](../MakanApp.Api/Controllers/SessionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `sessionId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SessionVersionCommand](#contract-SessionVersionCommand)<br>`text/json`: [SessionVersionCommand](#contract-SessionVersionCommand)<br>`application/*+json`: [SessionVersionCommand](#contract-SessionVersionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SessionResult](#contract-SessionResult)<br>`application/json`: [SessionResult](#contract-SessionResult)<br>`text/json`: [SessionResult](#contract-SessionResult) |

### POST /api/v1/academic/sessions/{sessionId}/complete

کد واقعی: [SessionsController.Complete](../MakanApp.Api/Controllers/SessionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Academic.IAcademicSessionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `sessionId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SessionVersionCommand](#contract-SessionVersionCommand)<br>`text/json`: [SessionVersionCommand](#contract-SessionVersionCommand)<br>`application/*+json`: [SessionVersionCommand](#contract-SessionVersionCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SessionResult](#contract-SessionResult)<br>`application/json`: [SessionResult](#contract-SessionResult)<br>`text/json`: [SessionResult](#contract-SessionResult) |


## Submissions

### POST /api/v1/academic/assignments/{assignmentId}/attempts

کد واقعی: [SubmissionsController.CreateOrResumeDraft](../MakanApp.Api/Controllers/SubmissionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.ISubmissionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `assignmentId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 201 | Created | `text/plain`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`application/json`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`text/json`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult) |

### GET /api/v1/academic/assignments/{assignmentId}/attempts/me

کد واقعی: [SubmissionsController.GetMyAttempts](../MakanApp.Api/Controllers/SubmissionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.ISubmissionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `assignmentId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`application/json`: آرایه از [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`text/json`: آرایه از [SubmissionAttemptResult](#contract-SubmissionAttemptResult) |

### GET /api/v1/academic/assignments/{assignmentId}/submitted-attempts

کد واقعی: [SubmissionsController.GetSubmittedAttempts](../MakanApp.Api/Controllers/SubmissionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.ISubmissionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `assignmentId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`application/json`: آرایه از [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`text/json`: آرایه از [SubmissionAttemptResult](#contract-SubmissionAttemptResult) |

### GET /api/v1/academic/submission-attempts/{attemptId}

کد واقعی: [SubmissionsController.GetAttempt](../MakanApp.Api/Controllers/SubmissionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.ISubmissionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`application/json`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`text/json`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult) |

### POST /api/v1/academic/submission-attempts/{attemptId}/attachments

کد واقعی: [SubmissionsController.AttachFile](../MakanApp.Api/Controllers/SubmissionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.ISubmissionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [AttachSubmissionFileCommand](#contract-AttachSubmissionFileCommand)<br>`text/json`: [AttachSubmissionFileCommand](#contract-AttachSubmissionFileCommand)<br>`application/*+json`: [AttachSubmissionFileCommand](#contract-AttachSubmissionFileCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`application/json`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`text/json`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult) |

### DELETE /api/v1/academic/submission-attempts/{attemptId}/attachments/{fileAssetId}

کد واقعی: [SubmissionsController.RemoveFile](../MakanApp.Api/Controllers/SubmissionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.ISubmissionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |
| `fileAssetId` | path | بله | string / uuid |
| `expectedRowVersion` | query | خیر | string |

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`application/json`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`text/json`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult) |

### PATCH /api/v1/academic/submission-attempts/{attemptId}/draft

کد واقعی: [SubmissionsController.SaveDraft](../MakanApp.Api/Controllers/SubmissionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.ISubmissionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [SaveSubmissionDraftCommand](#contract-SaveSubmissionDraftCommand)<br>`text/json`: [SaveSubmissionDraftCommand](#contract-SaveSubmissionDraftCommand)<br>`application/*+json`: [SaveSubmissionDraftCommand](#contract-SaveSubmissionDraftCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`application/json`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult)<br>`text/json`: [SubmissionAttemptResult](#contract-SubmissionAttemptResult) |

### POST /api/v1/academic/submission-attempts/{attemptId}/submit

کد واقعی: [SubmissionsController.FinalSubmit](../MakanApp.Api/Controllers/SubmissionsController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Assessment.ISubmissionService`

| پارامتر | محل | الزام در schema | نوع |
|---|---|---|---|
| `attemptId` | path | بله | string / uuid |

بدنه درخواست: `application/json`: [FinalSubmitAssignmentCommand](#contract-FinalSubmitAssignmentCommand)<br>`text/json`: [FinalSubmitAssignmentCommand](#contract-FinalSubmitAssignmentCommand)<br>`application/*+json`: [FinalSubmitAssignmentCommand](#contract-FinalSubmitAssignmentCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [SubmissionReceipt](#contract-SubmissionReceipt)<br>`application/json`: [SubmissionReceipt](#contract-SubmissionReceipt)<br>`text/json`: [SubmissionReceipt](#contract-SubmissionReceipt) |


## Workspaces

### GET /api/v1/workspaces/current

کد واقعی: [WorkspacesController.GetCurrentAccessContext](../MakanApp.Api/Controllers/WorkspacesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationService`، `MakanApp.Application.Organization.IAccessContextResolver`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AccessContext](#contract-AccessContext)<br>`application/json`: [AccessContext](#contract-AccessContext)<br>`text/json`: [AccessContext](#contract-AccessContext) |

### GET /api/v1/workspaces/me

کد واقعی: [WorkspacesController.GetMyWorkspaces](../MakanApp.Api/Controllers/WorkspacesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationService`، `MakanApp.Application.Organization.IAccessContextResolver`

بدنه درخواست: ندارد.

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: آرایه از [WorkspaceResult](#contract-WorkspaceResult)<br>`application/json`: آرایه از [WorkspaceResult](#contract-WorkspaceResult)<br>`text/json`: آرایه از [WorkspaceResult](#contract-WorkspaceResult) |

### POST /api/v1/workspaces/select

کد واقعی: [WorkspacesController.SelectWorkspace](../MakanApp.Api/Controllers/WorkspacesController.cs).

احراز هویت: نشست Bearer معتبر؛ مجوز نهایی منبع در Application بررسی می‌شود.

وابستگی‌های واقعی Controller: `MakanApp.Application.Organization.IOrganizationService`، `MakanApp.Application.Organization.IAccessContextResolver`

بدنه درخواست: `application/json`: [SelectWorkspaceCommand](#contract-SelectWorkspaceCommand)<br>`text/json`: [SelectWorkspaceCommand](#contract-SelectWorkspaceCommand)<br>`application/*+json`: [SelectWorkspaceCommand](#contract-SelectWorkspaceCommand)؛ required

| HTTP status | معنا | بدنه |
|---|---|---|
| 200 | OK | `text/plain`: [AccessContext](#contract-AccessContext)<br>`application/json`: [AccessContext](#contract-AccessContext)<br>`text/json`: [AccessContext](#contract-AccessContext) |

## قراردادهای JSON و Enum

Required زیر همان required در OpenAPI است؛ اختیاری بودن schema به معنی عبور از قواعد Application نیست. bounds فقط در صورت انتشار نشان داده می‌شوند.

DOC-CONTRACT-01 در نسخهٔ مبنا CLOSED_IN_CODE_AND_LOCAL_CONTRACT_TESTS است؛ اختلاف integer/String قبلی در snapshot جاری دیده نشد. OpenAPI با serializer رشته‌ای MVC هم‌تراز شده است؛ literalهای زیر از همان CLR/JsonOptions استخراج می‌شوند. این نتیجه شاهد انتشار OpenAPI روی سرور نیست؛ named Flags همهٔ ترکیب‌های مجاز یا ورودی‌های مجاز use case را تعریف نمی‌کند.

<a id="contract-AbuseReportReason"></a>
### AbuseReportReason

CLR واقعی: `MakanApp.Domain.Messaging.AbuseReportReason`؛ Flags: False

literalهای JSON واقعی MVC: `["Harassment", "Spam", "Threat", "InappropriateContent", "Impersonation", "Other", "ChildSafetyConcern"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-AbuseReportReceiptResult"></a>
### AbuseReportReceiptResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `reportId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `reason` | [AbuseReportReason](#contract-AbuseReportReason) | بله | خیر/تصریح نشده | — |
| `status` | [AbuseReportStatus](#contract-AbuseReportStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-AbuseReportStatus"></a>
### AbuseReportStatus

CLR واقعی: `MakanApp.Domain.Messaging.AbuseReportStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Submitted", "UnderReview", "Resolved", "Dismissed"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-AcademicPeriodResult"></a>
### AcademicPeriodResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `startDate` | string / date | بله | خیر/تصریح نشده | — |
| `endDate` | string / date | بله | خیر/تصریح نشده | — |
| `status` | [AcademicPeriodStatus](#contract-AcademicPeriodStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-AcademicPeriodStatus"></a>
### AcademicPeriodStatus

CLR واقعی: `MakanApp.Domain.Academic.AcademicPeriodStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Closed"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-AcceptInvitationCommand"></a>
### AcceptInvitationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `confirmPersonLink` | boolean | خیر | خیر/تصریح نشده | {"default": false} |
| `confirmGuardianRelation` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-AcceptInvitationResult"></a>
### AcceptInvitationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `invitationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationName` | string | بله | خیر/تصریح نشده | — |
| `membershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `roleAssignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `role` | [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `alreadyAccepted` | boolean | بله | خیر/تصریح نشده | — |
| `organizationPersonId` | string / uuid | خیر | بله | {"default": null} |
| `guardianRelationId` | string / uuid | خیر | بله | {"default": null} |

<a id="contract-AccessContext"></a>
### AccessContext

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `actorId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sessionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `workspaceType` | [WorkspaceType](#contract-WorkspaceType) | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `membershipId` | string / uuid | بله | بله | — |
| `activeRole` | [NullableOfOrganizationRole](#contract-NullableOfOrganizationRole) | بله | خیر/تصریح نشده | — |
| `subjectOrganizationPersonId` | string / uuid | بله | بله | — |

<a id="contract-AddConversationMemberCommand"></a>
### AddConversationMemberCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |

<a id="contract-AddExamQuestionCommand"></a>
### AddExamQuestionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `order` | integer / int32 | بله | خیر/تصریح نشده | — |
| `type` | [ExamQuestionType](#contract-ExamQuestionType) | بله | خیر/تصریح نشده | — |
| `prompt` | string | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `options` | آرایه از [ExamQuestionOptionCommand](#contract-ExamQuestionOptionCommand) | بله | بله | — |
| `expectedVersionRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-AddReactionCommand"></a>
### AddReactionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `reaction` | [MessageReactionType](#contract-MessageReactionType) | بله | خیر/تصریح نشده | — |

<a id="contract-AdminAuditLogItem"></a>
### AdminAuditLogItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `actorUserId` | string / uuid | بله | بله | — |
| `actorPersonId` | string / uuid | بله | بله | — |
| `actorSessionId` | string / uuid | بله | بله | — |
| `action` | string | بله | خیر/تصریح نشده | — |
| `targetType` | string | بله | خیر/تصریح نشده | — |
| `targetId` | string | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `reason` | string | بله | خیر/تصریح نشده | — |
| `occurredAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `correlationId` | string | بله | خیر/تصریح نشده | — |
| `result` | integer / int32 | بله | خیر/تصریح نشده | — |
| `beforeSummary` | string | بله | بله | — |
| `afterSummary` | string | بله | بله | — |

<a id="contract-AdminAuditLogPage"></a>
### AdminAuditLogPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [AdminAuditLogItem](#contract-AdminAuditLogItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-AdvanceConversationCursorCommand"></a>
### AdvanceConversationCursorCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `upToMessageSequence` | integer / int64 | بله | خیر/تصریح نشده | — |

<a id="contract-AiActivitySuggestionsResponse"></a>
### AiActivitySuggestionsResponse

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `requestId` | string | بله | خیر/تصریح نشده | — |
| `correlationId` | string | بله | بله | — |
| `summary` | string | بله | خیر/تصریح نشده | — |
| `activities` | آرایه از [AiTeachingActivity](#contract-AiTeachingActivity) | بله | خیر/تصریح نشده | — |
| `recommendations` | آرایه از [AiRecommendation](#contract-AiRecommendation) | بله | خیر/تصریح نشده | — |
| `sources` | آرایه از [AiSourceReference](#contract-AiSourceReference) | بله | خیر/تصریح نشده | — |
| `metadata` | [AiResponseMetadata](#contract-AiResponseMetadata) | بله | خیر/تصریح نشده | — |

<a id="contract-AiAttentionArea"></a>
### AiAttentionArea

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | خیر/تصریح نشده | — |
| `severity` | [AiSeverity](#contract-AiSeverity) | بله | خیر/تصریح نشده | — |
| `entityType` | string | بله | بله | — |
| `entityId` | string | بله | بله | — |

<a id="contract-AiChatResult"></a>
### AiChatResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `requestId` | string | بله | خیر/تصریح نشده | — |
| `correlationId` | string | بله | بله | — |
| `conversationId` | string | بله | بله | — |
| `answer` | [AiMessage](#contract-AiMessage) | بله | خیر/تصریح نشده | — |
| `insights` | آرایه از [AiInsight](#contract-AiInsight) | بله | خیر/تصریح نشده | — |
| `risks` | آرایه از بدون schema منتشرشده | بله | خیر/تصریح نشده | — |
| `recommendations` | آرایه از [AiRecommendation](#contract-AiRecommendation) | بله | خیر/تصریح نشده | — |
| `sources` | آرایه از [AiSourceReference](#contract-AiSourceReference) | بله | خیر/تصریح نشده | — |

<a id="contract-AiExamPreparationResponse"></a>
### AiExamPreparationResponse

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `requestId` | string | بله | خیر/تصریح نشده | — |
| `correlationId` | string | بله | بله | — |
| `summary` | string | بله | خیر/تصریح نشده | — |
| `priorityTopics` | آرایه از string | بله | خیر/تصریح نشده | — |
| `preparationSteps` | آرایه از string | بله | خیر/تصریح نشده | — |
| `risks` | آرایه از [AiInsight](#contract-AiInsight) | بله | خیر/تصریح نشده | — |
| `recommendations` | آرایه از [AiRecommendation](#contract-AiRecommendation) | بله | خیر/تصریح نشده | — |
| `sources` | آرایه از [AiSourceReference](#contract-AiSourceReference) | بله | خیر/تصریح نشده | — |
| `metadata` | [AiResponseMetadata](#contract-AiResponseMetadata) | بله | خیر/تصریح نشده | — |

<a id="contract-AiHomeworkExplanationResponse"></a>
### AiHomeworkExplanationResponse

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `requestId` | string | بله | خیر/تصریح نشده | — |
| `correlationId` | string | بله | بله | — |
| `explanation` | string | بله | خیر/تصریح نشده | — |
| `steps` | آرایه از string | بله | خیر/تصریح نشده | — |
| `hints` | آرایه از string | بله | خیر/تصریح نشده | — |
| `recommendations` | آرایه از [AiRecommendation](#contract-AiRecommendation) | بله | خیر/تصریح نشده | — |
| `sources` | آرایه از [AiSourceReference](#contract-AiSourceReference) | بله | خیر/تصریح نشده | — |
| `metadata` | [AiResponseMetadata](#contract-AiResponseMetadata) | بله | خیر/تصریح نشده | — |

<a id="contract-AiInsight"></a>
### AiInsight

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `type` | string | بله | خیر/تصریح نشده | — |
| `severity` | [AiSeverity](#contract-AiSeverity) | بله | خیر/تصریح نشده | — |
| `entityType` | string | بله | بله | — |
| `entityId` | string | بله | بله | — |
| `title` | string | بله | بله | — |
| `description` | string | بله | بله | — |
| `confidence` | number / double | بله | بله | — |

<a id="contract-AiMessage"></a>
### AiMessage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `text` | string | بله | خیر/تصریح نشده | — |
| `language` | string | خیر | خیر/تصریح نشده | {"default": "fa"} |

<a id="contract-AiParentChildSummaryResponse"></a>
### AiParentChildSummaryResponse

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `requestId` | string | بله | خیر/تصریح نشده | — |
| `correlationId` | string | بله | بله | — |
| `academicSummary` | string | بله | خیر/تصریح نشده | — |
| `progress` | [AiProgress](#contract-AiProgress) | بله | خیر/تصریح نشده | — |
| `attentionAreas` | آرایه از [AiAttentionArea](#contract-AiAttentionArea) | بله | خیر/تصریح نشده | — |
| `upcomingItems` | آرایه از [AiUpcomingItem](#contract-AiUpcomingItem) | بله | خیر/تصریح نشده | — |
| `risks` | آرایه از [AiInsight](#contract-AiInsight) | بله | خیر/تصریح نشده | — |
| `recommendations` | آرایه از [AiRecommendation](#contract-AiRecommendation) | بله | خیر/تصریح نشده | — |
| `sources` | آرایه از [AiSourceReference](#contract-AiSourceReference) | بله | خیر/تصریح نشده | — |
| `metadata` | [AiResponseMetadata](#contract-AiResponseMetadata) | بله | خیر/تصریح نشده | — |

<a id="contract-AiPlanItem"></a>
### AiPlanItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |
| `priority` | [AiSeverity](#contract-AiSeverity) | بله | خیر/تصریح نشده | — |
| `estimatedMinutes` | integer / int32 | بله | بله | — |

<a id="contract-AiProgress"></a>
### AiProgress

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `trend` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | خیر/تصریح نشده | — |

<a id="contract-AiRecommendation"></a>
### AiRecommendation

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |

<a id="contract-AiResponseMetadata"></a>
### AiResponseMetadata

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `modelAlias` | string | بله | خیر/تصریح نشده | — |
| `processingTimeMs` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-AiRoleInsightResponse"></a>
### AiRoleInsightResponse

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `requestId` | string | بله | خیر/تصریح نشده | — |
| `correlationId` | string | بله | بله | — |
| `summary` | string | بله | خیر/تصریح نشده | — |
| `strengths` | آرایه از [AiInsight](#contract-AiInsight) | بله | خیر/تصریح نشده | — |
| `attentionAreas` | آرایه از بدون schema منتشرشده | بله | خیر/تصریح نشده | — |
| `risks` | آرایه از بدون schema منتشرشده | بله | خیر/تصریح نشده | — |
| `recommendations` | آرایه از [AiRecommendation](#contract-AiRecommendation) | بله | خیر/تصریح نشده | — |
| `sources` | آرایه از [AiSourceReference](#contract-AiSourceReference) | بله | خیر/تصریح نشده | — |
| `metadata` | [AiResponseMetadata](#contract-AiResponseMetadata) | بله | خیر/تصریح نشده | — |

<a id="contract-AiSeverity"></a>
### AiSeverity

CLR واقعی: `MakanApp.Application.Ai.AiSeverity`؛ Flags: False

literalهای JSON واقعی MVC: `["Info", "Low", "Medium", "High", "Critical"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-AiSourceReference"></a>
### AiSourceReference

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `type` | string | بله | خیر/تصریح نشده | — |
| `id` | string | بله | خیر/تصریح نشده | — |

<a id="contract-AiStudentAnalysisResponse"></a>
### AiStudentAnalysisResponse

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `requestId` | string | بله | خیر/تصریح نشده | — |
| `correlationId` | string | بله | بله | — |
| `summary` | string | بله | خیر/تصریح نشده | — |
| `strengths` | آرایه از [AiInsight](#contract-AiInsight) | بله | خیر/تصریح نشده | — |
| `weaknesses` | آرایه از بدون schema منتشرشده | بله | خیر/تصریح نشده | — |
| `risks` | آرایه از بدون schema منتشرشده | بله | خیر/تصریح نشده | — |
| `recommendations` | آرایه از [AiRecommendation](#contract-AiRecommendation) | بله | خیر/تصریح نشده | — |
| `sources` | آرایه از [AiSourceReference](#contract-AiSourceReference) | بله | خیر/تصریح نشده | — |
| `metadata` | [AiResponseMetadata](#contract-AiResponseMetadata) | بله | خیر/تصریح نشده | — |

<a id="contract-AiStudyPlanResponse"></a>
### AiStudyPlanResponse

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `requestId` | string | بله | خیر/تصریح نشده | — |
| `correlationId` | string | بله | بله | — |
| `summary` | string | بله | خیر/تصریح نشده | — |
| `priorities` | آرایه از string | بله | خیر/تصریح نشده | — |
| `planItems` | آرایه از [AiPlanItem](#contract-AiPlanItem) | بله | خیر/تصریح نشده | — |
| `recommendations` | آرایه از [AiRecommendation](#contract-AiRecommendation) | بله | خیر/تصریح نشده | — |
| `sources` | آرایه از [AiSourceReference](#contract-AiSourceReference) | بله | خیر/تصریح نشده | — |
| `metadata` | [AiResponseMetadata](#contract-AiResponseMetadata) | بله | خیر/تصریح نشده | — |

<a id="contract-AiTeachingActivity"></a>
### AiTeachingActivity

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `objective` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |
| `steps` | آرایه از string | بله | خیر/تصریح نشده | — |
| `estimatedMinutes` | integer / int32 | بله | بله | — |

<a id="contract-AiUpcomingItem"></a>
### AiUpcomingItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `type` | string | بله | خیر/تصریح نشده | — |
| `id` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `date` | string | بله | بله | — |

<a id="contract-AssignTeacherCommand"></a>
### AssignTeacherCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `teacherMembershipId` | string / uuid | بله | خیر/تصریح نشده | — |

<a id="contract-AssignmentGradingQueueSummary"></a>
### AssignmentGradingQueueSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `awaitingEvaluationCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `draftEvaluationCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalPendingCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-AssignmentResult"></a>
### AssignmentResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `status` | [AssignmentStatus](#contract-AssignmentStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `publishedAtUtc` | string / date-time | بله | بله | — |
| `versionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `versionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | خیر/تصریح نشده | — |
| `dueAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `allowLateSubmission` | boolean | بله | خیر/تصریح نشده | — |
| `maxAttempts` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `assignmentRowVersion` | string | بله | خیر/تصریح نشده | — |
| `versionRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-AssignmentStatus"></a>
### AssignmentStatus

CLR واقعی: `MakanApp.Domain.Assessment.AssignmentStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Draft", "Published", "Closed", "Archived"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-AttachSubmissionFileCommand"></a>
### AttachSubmissionFileCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileAssetId` | string / uuid | بله | خیر/تصریح نشده | — |
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-AttendanceEntryResult"></a>
### AttendanceEntryResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `attendanceId` | string / uuid | بله | بله | — |
| `enrollmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [AttendanceStatus](#contract-AttendanceStatus) | بله | خیر/تصریح نشده | — |
| `recordedAtUtc` | string / date-time | بله | بله | — |
| `recordedByMembershipId` | string / uuid | بله | بله | — |
| `rowVersion` | string | بله | بله | — |

<a id="contract-AttendanceReportPageOfParentAttendanceReportItem"></a>
### AttendanceReportPageOfParentAttendanceReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `summary` | [AttendanceReportSummary](#contract-AttendanceReportSummary) | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ParentAttendanceReportItem](#contract-ParentAttendanceReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-AttendanceReportPageOfStudentAttendanceReportItem"></a>
### AttendanceReportPageOfStudentAttendanceReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `summary` | [AttendanceReportSummary](#contract-AttendanceReportSummary) | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [StudentAttendanceReportItem](#contract-StudentAttendanceReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-AttendanceReportPageOfTeacherAttendanceReportItem"></a>
### AttendanceReportPageOfTeacherAttendanceReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `summary` | [AttendanceReportSummary](#contract-AttendanceReportSummary) | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [TeacherAttendanceReportItem](#contract-TeacherAttendanceReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-AttendanceReportSummary"></a>
### AttendanceReportSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `eligibleCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `recordedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `presentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `absentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `lateCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `excusedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `notRecordedCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-AttendanceRevisionResult"></a>
### AttendanceRevisionResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `previousStatus` | [AttendanceStatus](#contract-AttendanceStatus) | بله | خیر/تصریح نشده | — |
| `newStatus` | [AttendanceStatus](#contract-AttendanceStatus) | بله | خیر/تصریح نشده | — |
| `correctedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `correctedByMembershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-AttendanceStatus"></a>
### AttendanceStatus

CLR واقعی: `MakanApp.Domain.Academic.AttendanceStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["NotRecorded", "Present", "Absent", "Late", "Excused"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-AuthorizedChildResult"></a>
### AuthorizedChildResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationName` | string | بله | خیر/تصریح نشده | — |
| `relationStatus` | [GuardianRelationStatus](#contract-GuardianRelationStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-BlockedUserResult"></a>
### BlockedUserResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `blockId` | string / uuid | بله | خیر/تصریح نشده | — |
| `user` | [SafeMessagingIdentityResult](#contract-SafeMessagingIdentityResult) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CancelScheduledMessageCommand"></a>
### CancelScheduledMessageCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ChangeConversationMemberRoleCommand"></a>
### ChangeConversationMemberRoleCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `role` | [ConversationParticipantRole](#contract-ConversationParticipantRole) | بله | خیر/تصریح نشده | — |
| `adminPermissions` | [ConversationAdminPermission2](#contract-ConversationAdminPermission2) | خیر | خیر/تصریح نشده | — |

<a id="contract-ChangePlatformMembershipRoleCommand"></a>
### ChangePlatformMembershipRoleCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fromRole` | [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `toRole` | [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ChatFolderResult"></a>
### ChatFolderResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `name` | string | بله | خیر/تصریح نشده | — |
| `sortOrder` | integer / int32 | بله | خیر/تصریح نشده | — |
| `includedTypes` | [FolderConversationTypes](#contract-FolderConversationTypes) | بله | خیر/تصریح نشده | — |
| `excludedTypes` | [FolderConversationTypes](#contract-FolderConversationTypes) | بله | خیر/تصریح نشده | — |
| `unreadOnly` | boolean | بله | خیر/تصریح نشده | — |
| `overrides` | آرایه از [FolderOverrideCommand](#contract-FolderOverrideCommand) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ChecklistItemInput"></a>
### ChecklistItemInput

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `itemId` | string / uuid | بله | خیر/تصریح نشده | — |
| `text` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ClassLearnerResult"></a>
### ClassLearnerResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `enrollmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `personId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | بله | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `username` | string | بله | بله | — |
| `profilePhoto` | [ProfilePhotoResult2](#contract-ProfilePhotoResult2) | بله | خیر/تصریح نشده | — |
| `status` | [EnrollmentStatus](#contract-EnrollmentStatus) | بله | خیر/تصریح نشده | — |
| `enrolledAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ClassLessonResult"></a>
### ClassLessonResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `courseId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `body` | string | بله | خیر/تصریح نشده | — |
| `displayOrder` | integer / int32 | بله | خیر/تصریح نشده | — |
| `status` | [LessonStatus](#contract-LessonStatus) | بله | خیر/تصریح نشده | — |
| `revisionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |
| `createdByUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | بله | — |
| `publishedAtUtc` | string / date-time | بله | بله | — |
| `attachments` | آرایه از [LessonAttachmentResult](#contract-LessonAttachmentResult) | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-ClassResult"></a>
### ClassResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `academicPeriodId` | string / uuid | بله | خیر/تصریح نشده | — |
| `academicPeriodTitle` | string | بله | خیر/تصریح نشده | — |
| `courseId` | string / uuid | بله | خیر/تصریح نشده | — |
| `courseTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `capacity` | integer / int32 | بله | خیر/تصریح نشده | — |
| `status` | [ClassStatus](#contract-ClassStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-ClassStatus"></a>
### ClassStatus

CLR واقعی: `MakanApp.Domain.Academic.ClassStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Draft", "Active", "Completed", "Archived"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ClassTeacherResult"></a>
### ClassTeacherResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `teacherAssignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `teacherMembershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `personId` | string / uuid | بله | بله | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `username` | string | بله | بله | — |
| `profilePhoto` | [ProfilePhotoResult2](#contract-ProfilePhotoResult2) | بله | خیر/تصریح نشده | — |
| `status` | [TeacherAssignmentStatus](#contract-TeacherAssignmentStatus) | بله | خیر/تصریح نشده | — |
| `assignedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CloseConversationPollCommand"></a>
### CloseConversationPollCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CommerceEntitlementDto"></a>
### CommerceEntitlementDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `key` | [EntitlementKey](#contract-EntitlementKey) | بله | خیر/تصریح نشده | — |
| `enabled` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-CommerceQuotaDto"></a>
### CommerceQuotaDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `key` | [QuotaKey](#contract-QuotaKey) | بله | خیر/تصریح نشده | — |
| `limit` | integer / int64 | بله | بله | — |
| `currentUsage` | integer / int64 | بله | خیر/تصریح نشده | — |
| `remaining` | integer / int64 | بله | بله | — |
| `isUnlimited` | boolean | بله | خیر/تصریح نشده | — |
| `isOverQuota` | boolean | بله | خیر/تصریح نشده | — |
| `usageSource` | [QuotaUsageSource](#contract-QuotaUsageSource) | بله | خیر/تصریح نشده | — |

<a id="contract-CommerceSubscriptionDto"></a>
### CommerceSubscriptionDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `subscriptionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `planId` | string / uuid | بله | خیر/تصریح نشده | — |
| `planVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `planCode` | string | بله | خیر/تصریح نشده | — |
| `planDisplayName` | string | بله | خیر/تصریح نشده | — |
| `planVersionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `status` | [SubscriptionStatus](#contract-SubscriptionStatus) | بله | خیر/تصریح نشده | — |
| `startsAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endsAtUtc` | string / date-time | بله | بله | — |

<a id="contract-CommerceSummaryState"></a>
### CommerceSummaryState

CLR واقعی: `MakanApp.Application.Commerce.CommerceSummaryState`؛ Flags: False

literalهای JSON واقعی MVC: `["NoSubscription", "Active", "Inactive"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-CommunicationAgeCategory"></a>
### CommunicationAgeCategory

CLR واقعی: `MakanApp.Domain.Identity.CommunicationAgeCategory`؛ Flags: False

literalهای JSON واقعی MVC: `["Unknown", "Minor", "Adult"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-CommunicationAgeResult"></a>
### CommunicationAgeResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classification` | [CommunicationAgeCategory](#contract-CommunicationAgeCategory) | بله | خیر/تصریح نشده | — |
| `verificationStatus` | [CommunicationAgeVerificationStatus](#contract-CommunicationAgeVerificationStatus) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CommunicationAgeVerificationStatus"></a>
### CommunicationAgeVerificationStatus

CLR واقعی: `MakanApp.Domain.Identity.CommunicationAgeVerificationStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Unverified", "Verified", "Conflict"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-CompleteExamGradeCommand"></a>
### CompleteExamGradeCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `learnerFeedback` | string | بله | بله | — |
| `guardianVisibleFeedback` | string | بله | بله | — |
| `evaluatorPrivateNote` | string | بله | بله | — |
| `expectedGradeRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CompleteJoinRequestCommand"></a>
### CompleteJoinRequestCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CompleteModerationCaseCommand"></a>
### CompleteModerationCaseCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |
| `internalNote` | string | بله | بله | — |

<a id="contract-CompleteProfileCommand"></a>
### CompleteProfileCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `firstName` | string | بله | خیر/تصریح نشده | — |
| `lastName` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | بله | — |
| `username` | string | بله | خیر/تصریح نشده | — |
| `dateOfBirth` | string / date | خیر | بله | {"default": null} |

<a id="contract-ContactCardResult"></a>
### ContactCardResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `username` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ContentSafetyDecision"></a>
### ContentSafetyDecision

CLR واقعی: `MakanApp.Domain.Messaging.ContentSafetyDecision`؛ Flags: False

literalهای JSON واقعی MVC: `["Allowed", "Blocked", "ReviewRequired", "Unavailable"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationAdminPermission"></a>
### ConversationAdminPermission

CLR واقعی: `MakanApp.Domain.Messaging.ConversationAdminPermission`؛ Flags: True

literalهای JSON واقعی MVC: `["None", "ManageMembers", "RemoveMembers", "ManageAdmins", "DeleteMessages", "PinMessages", "PublishMessages", "LegacyAdminDefault", "ManageHistory", "ManageVisibility", "ReviewReports", "ManageInvites", "BanMembers", "ChangeInfo", "ManageTopics", "All"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationAdminPermission2"></a>
### ConversationAdminPermission2

string

<a id="contract-ConversationChangePageResult"></a>
### ConversationChangePageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `changes` | آرایه از [ConversationChangeResult](#contract-ConversationChangeResult) | بله | خیر/تصریح نشده | — |
| `nextCursor` | string | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationChangeResult"></a>
### ConversationChangeResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `changeId` | string / uuid | بله | خیر/تصریح نشده | — |
| `type` | [MessagingChangeType](#contract-MessagingChangeType) | بله | خیر/تصریح نشده | — |
| `resourceId` | string / uuid | بله | خیر/تصریح نشده | — |
| `resourceVersion` | string | بله | بله | — |
| `cursor` | string | بله | خیر/تصریح نشده | — |
| `occurredAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `actorUserId` | string / uuid | بله | بله | — |
| `payloadVersion` | integer / int32 | بله | خیر/تصریح نشده | — |
| `peerReadThroughSequence` | integer / int64 | خیر | بله | {"default": null} |

<a id="contract-ConversationChecklistItemResult"></a>
### ConversationChecklistItemResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `itemId` | string / uuid | بله | خیر/تصریح نشده | — |
| `text` | string | بله | خیر/تصریح نشده | — |
| `isChecked` | boolean | بله | خیر/تصریح نشده | — |
| `stateVersion` | string | بله | بله | — |

<a id="contract-ConversationChecklistMode"></a>
### ConversationChecklistMode

CLR واقعی: `MakanApp.Domain.Messaging.ConversationChecklistMode`؛ Flags: False

literalهای JSON واقعی MVC: `["Personal", "Collaborative"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationChecklistResult"></a>
### ConversationChecklistResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `mode` | [ConversationChecklistMode](#contract-ConversationChecklistMode) | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ConversationChecklistItemResult](#contract-ConversationChecklistItemResult) | بله | خیر/تصریح نشده | — |
| `isCompleted` | boolean | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |
| `messageVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationCursorStateResult"></a>
### ConversationCursorStateResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `lastDeliveredMessageSequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `lastReadMessageSequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `unreadCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationInfoResult"></a>
### ConversationInfoResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationInvitationResult"></a>
### ConversationInvitationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `inviterUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `targetUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [ConversationInvitationStatus](#contract-ConversationInvitationStatus) | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationInvitationStatus"></a>
### ConversationInvitationStatus

CLR واقعی: `MakanApp.Domain.Messaging.ConversationInvitationStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Pending", "Accepted", "Declined", "Revoked", "Expired"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationJoinRequestPageResult"></a>
### ConversationJoinRequestPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [ConversationJoinRequestResult2](#contract-ConversationJoinRequestResult2) | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationJoinRequestResult"></a>
### ConversationJoinRequestResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [ConversationJoinRequestStatus](#contract-ConversationJoinRequestStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationJoinRequestResult2"></a>
### ConversationJoinRequestResult2

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [ConversationJoinRequestStatus](#contract-ConversationJoinRequestStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationJoinRequestStatus"></a>
### ConversationJoinRequestStatus

CLR واقعی: `MakanApp.Domain.Messaging.ConversationJoinRequestStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Pending", "Approved", "Rejected", "Cancelled", "Expired"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationManagementPolicy"></a>
### ConversationManagementPolicy

CLR واقعی: `MakanApp.Domain.Messaging.ConversationManagementPolicy`؛ Flags: False

literalهای JSON واقعی MVC: `["None", "UserManaged", "SystemManagedAcademic"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationMediaItemResult"></a>
### ConversationMediaItemResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `senderUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sentAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `attachment` | [MessageAttachmentResult](#contract-MessageAttachmentResult) | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationMediaPageResult"></a>
### ConversationMediaPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [ConversationMediaItemResult](#contract-ConversationMediaItemResult) | بله | خیر/تصریح نشده | — |
| `nextBeforeSequence` | integer / int64 | بله | بله | — |

<a id="contract-ConversationMessagePageResult"></a>
### ConversationMessagePageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messages` | آرایه از [ConversationMessageResult](#contract-ConversationMessageResult) | بله | خیر/تصریح نشده | — |
| `nextBeforeSequence` | integer / int64 | بله | بله | — |

<a id="contract-ConversationMessageResult"></a>
### ConversationMessageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `senderUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sender` | [SafeMessagingIdentityResult](#contract-SafeMessagingIdentityResult) | بله | خیر/تصریح نشده | — |
| `clientMessageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `kind` | [NullableOfMessageKind](#contract-NullableOfMessageKind) | بله | خیر/تصریح نشده | — |
| `text` | string | بله | بله | — |
| `sentAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `editedAtUtc` | string / date-time | بله | بله | — |
| `deletedAtUtc` | string / date-time | بله | بله | — |
| `isEdited` | boolean | بله | خیر/تصریح نشده | — |
| `isDeleted` | boolean | بله | خیر/تصریح نشده | — |
| `reply` | [MessageReplySummaryResult](#contract-MessageReplySummaryResult) | بله | خیر/تصریح نشده | — |
| `isForwarded` | boolean | بله | خیر/تصریح نشده | — |
| `reactions` | آرایه از [MessageReactionSummaryResult](#contract-MessageReactionSummaryResult) | بله | خیر/تصریح نشده | — |
| `mentions` | آرایه از [SafeMessagingIdentityResult](#contract-SafeMessagingIdentityResult) | بله | خیر/تصریح نشده | — |
| `attachments` | آرایه از [MessageAttachmentResult](#contract-MessageAttachmentResult) | بله | خیر/تصریح نشده | — |
| `contactCard` | [ContactCardResult](#contract-ContactCardResult) | بله | خیر/تصریح نشده | — |
| `isPinned` | boolean | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |
| `topicId` | string / uuid | خیر | بله | {"default": null} |
| `isSilent` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-ConversationMuteMode"></a>
### ConversationMuteMode

CLR واقعی: `MakanApp.Domain.Messaging.ConversationMuteMode`؛ Flags: False

literalهای JSON واقعی MVC: `["None", "Permanent", "Until"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationOrganizationBadgeResult"></a>
### ConversationOrganizationBadgeResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationName` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationParticipantPageResult"></a>
### ConversationParticipantPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [ConversationParticipantResult](#contract-ConversationParticipantResult) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationParticipantResult"></a>
### ConversationParticipantResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `participantId` | string / uuid | بله | خیر/تصریح نشده | — |
| `user` | [SafeMessagingIdentityResult](#contract-SafeMessagingIdentityResult) | بله | خیر/تصریح نشده | — |
| `role` | [ConversationParticipantRole](#contract-ConversationParticipantRole) | بله | خیر/تصریح نشده | — |
| `status` | [ConversationParticipantStatus](#contract-ConversationParticipantStatus) | بله | خیر/تصریح نشده | — |
| `joinedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |
| `historyVisibleFromSequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `adminPermissions` | [ConversationAdminPermission](#contract-ConversationAdminPermission) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationParticipantRole"></a>
### ConversationParticipantRole

CLR واقعی: `MakanApp.Domain.Messaging.ConversationParticipantRole`؛ Flags: False

literalهای JSON واقعی MVC: `["Owner", "Admin", "Member"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationParticipantStatus"></a>
### ConversationParticipantStatus

CLR واقعی: `MakanApp.Domain.Messaging.ConversationParticipantStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Removed", "Left"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationPeerReadStateResult"></a>
### ConversationPeerReadStateResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `peerReadThroughSequence` | integer / int64 | بله | بله | — |
| `visibility` | [PeerReadVisibility](#contract-PeerReadVisibility) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | بله | — |

<a id="contract-ConversationPinResult"></a>
### ConversationPinResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `pinnedByUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `pinnedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `isPinned` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationPollOptionResult"></a>
### ConversationPollOptionResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `number` | integer / int32 | بله | خیر/تصریح نشده | — |
| `text` | string | بله | خیر/تصریح نشده | — |
| `voteCount` | integer / int64 | بله | خیر/تصریح نشده | — |
| `isSelected` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationPollResult"></a>
### ConversationPollResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `question` | string | بله | خیر/تصریح نشده | — |
| `allowsMultipleAnswers` | boolean | بله | خیر/تصریح نشده | — |
| `isAnonymous` | boolean | بله | خیر/تصریح نشده | — |
| `isQuiz` | boolean | بله | خیر/تصریح نشده | — |
| `status` | [ConversationPollStatus](#contract-ConversationPollStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `closedAtUtc` | string / date-time | بله | بله | — |
| `options` | آرایه از [ConversationPollOptionResult](#contract-ConversationPollOptionResult) | بله | خیر/تصریح نشده | — |
| `totalVoters` | integer / int64 | بله | خیر/تصریح نشده | — |
| `ownVoteVersion` | string | بله | بله | — |
| `correctOptionNumber` | integer / int32 | بله | بله | — |
| `explanation` | string | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationPollStatus"></a>
### ConversationPollStatus

CLR واقعی: `MakanApp.Domain.Messaging.ConversationPollStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Open", "Closed"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationPollVoterPageResult"></a>
### ConversationPollVoterPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [ConversationPollVoterResult](#contract-ConversationPollVoterResult) | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationPollVoterResult"></a>
### ConversationPollVoterResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `user` | [SafeMessagingIdentityResult](#contract-SafeMessagingIdentityResult) | بله | خیر/تصریح نشده | — |
| `optionNumbers` | آرایه از integer / int32 | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationScope"></a>
### ConversationScope

CLR واقعی: `MakanApp.Domain.Messaging.ConversationScope`؛ Flags: False

literalهای JSON واقعی MVC: `["Personal", "Organization"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationStatus"></a>
### ConversationStatus

CLR واقعی: `MakanApp.Domain.Messaging.ConversationStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Archived"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationSummaryResult"></a>
### ConversationSummaryResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `type` | [ConversationType](#contract-ConversationType) | بله | خیر/تصریح نشده | — |
| `scope` | [ConversationScope](#contract-ConversationScope) | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `title` | string | بله | بله | — |
| `otherParticipant` | [SafeMessagingIdentityResult2](#contract-SafeMessagingIdentityResult2) | بله | خیر/تصریح نشده | — |
| `lastMessagePreview` | string | بله | بله | — |
| `lastMessageAtUtc` | string / date-time | بله | بله | — |
| `lastMessageSequence` | integer / int64 | بله | بله | — |
| `lastDeliveredMessageSequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `lastReadMessageSequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `unreadCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `unreadMentionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `isArchived` | boolean | بله | خیر/تصریح نشده | — |
| `isMuted` | boolean | بله | خیر/تصریح نشده | — |
| `muteMode` | [ConversationMuteMode](#contract-ConversationMuteMode) | بله | خیر/تصریح نشده | — |
| `mutedUntilUtc` | string / date-time | بله | بله | — |
| `isPinned` | boolean | بله | خیر/تصریح نشده | — |
| `isManuallyUnread` | boolean | بله | خیر/تصریح نشده | — |
| `organization` | [ConversationOrganizationBadgeResult](#contract-ConversationOrganizationBadgeResult) | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationTopicPageResult"></a>
### ConversationTopicPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [ConversationTopicResult](#contract-ConversationTopicResult) | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |
| `canManage` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationTopicResult"></a>
### ConversationTopicResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `topicId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `createdByUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [ConversationTopicStatus](#contract-ConversationTopicStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |
| `alreadyExisted` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-ConversationTopicStatus"></a>
### ConversationTopicStatus

CLR واقعی: `MakanApp.Domain.Messaging.ConversationTopicStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Open", "Closed"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationType"></a>
### ConversationType

CLR واقعی: `MakanApp.Domain.Messaging.ConversationType`؛ Flags: False

literalهای JSON واقعی MVC: `["Direct", "Group", "Channel", "Saved"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationUserStateResult"></a>
### ConversationUserStateResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `isArchived` | boolean | بله | خیر/تصریح نشده | — |
| `isMuted` | boolean | بله | خیر/تصریح نشده | — |
| `muteMode` | [ConversationMuteMode](#contract-ConversationMuteMode) | بله | خیر/تصریح نشده | — |
| `mutedUntilUtc` | string / date-time | بله | بله | — |
| `isPinned` | boolean | بله | خیر/تصریح نشده | — |
| `isManuallyUnread` | boolean | بله | خیر/تصریح نشده | — |
| `historyHiddenThroughSequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ConversationVisibility"></a>
### ConversationVisibility

CLR واقعی: `MakanApp.Domain.Messaging.ConversationVisibility`؛ Flags: False

literalهای JSON واقعی MVC: `["Private", "Public"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ConversationVisibility2"></a>
### ConversationVisibility2

string / Private, Public

<a id="contract-CorrectAttendanceCommand"></a>
### CorrectAttendanceCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `status` | [AttendanceStatus](#contract-AttendanceStatus) | بله | خیر/تصریح نشده | — |
| `correctionReason` | string | بله | خیر/تصریح نشده | — |
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CorrectReleasedEvaluationCommand"></a>
### CorrectReleasedEvaluationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `learnerFeedback` | string | بله | بله | — |
| `guardianVisibleFeedback` | string | بله | بله | — |
| `teacherPrivateNote` | string | بله | بله | — |
| `correctionReason` | string | بله | خیر/تصریح نشده | — |
| `expectedReleasedEvaluationRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CorrectReleasedExamGradeCommand"></a>
### CorrectReleasedExamGradeCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `correctionReason` | string | بله | خیر/تصریح نشده | — |
| `expectedReleasedGradeRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CourseResult"></a>
### CourseResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `status` | [CourseStatus](#contract-CourseStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-CourseStatus"></a>
### CourseStatus

CLR واقعی: `MakanApp.Domain.Academic.CourseStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Archived"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-CreateAcademicPeriodCommand"></a>
### CreateAcademicPeriodCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `startDate` | string / date | بله | خیر/تصریح نشده | — |
| `endDate` | string / date | بله | خیر/تصریح نشده | — |

<a id="contract-CreateAssignmentDraftCommand"></a>
### CreateAssignmentDraftCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | خیر/تصریح نشده | — |
| `dueAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `allowLateSubmission` | boolean | بله | خیر/تصریح نشده | — |
| `maxAttempts` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |

<a id="contract-CreateClassCommand"></a>
### CreateClassCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `academicPeriodId` | string / uuid | بله | خیر/تصریح نشده | — |
| `courseId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `capacity` | integer / int32 | بله | خیر/تصریح نشده | — |
| `activateImmediately` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-CreateClassLessonCommand"></a>
### CreateClassLessonCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `body` | string | بله | بله | — |
| `displayOrder` | integer / int32 | بله | خیر/تصریح نشده | — |
| `fileAssetIds` | آرایه از string / uuid | بله | خیر/تصریح نشده | — |

<a id="contract-CreateConversationChecklistCommand"></a>
### CreateConversationChecklistCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientMessageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `mode` | [NullableOfConversationChecklistMode](#contract-NullableOfConversationChecklistMode) | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ChecklistItemInput](#contract-ChecklistItemInput) | بله | خیر/تصریح نشده | — |
| `topicId` | string / uuid | خیر | بله | {"default": null} |
| `isSilent` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-CreateConversationInvitationCommand"></a>
### CreateConversationInvitationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `targetUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-CreateConversationPollCommand"></a>
### CreateConversationPollCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientMessageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `question` | string | بله | خیر/تصریح نشده | — |
| `options` | آرایه از string | بله | خیر/تصریح نشده | — |
| `allowsMultipleAnswers` | boolean | بله | خیر/تصریح نشده | — |
| `isAnonymous` | boolean | بله | بله | — |
| `isQuiz` | boolean | خیر | خیر/تصریح نشده | {"default": false} |
| `correctOptionNumber` | integer / int32 | خیر | بله | {"default": null} |
| `explanation` | string | خیر | بله | {"default": null} |
| `topicId` | string / uuid | خیر | بله | {"default": null} |
| `isSilent` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-CreateConversationTopicCommand"></a>
### CreateConversationTopicCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientTopicId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreateCourseCommand"></a>
### CreateCourseCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreateExamDraftCommand"></a>
### CreateExamDraftCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `availableFromUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `availableUntilUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `durationMinutes` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxAttempts` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `randomizationPolicy` | [ExamRandomizationPolicy](#contract-ExamRandomizationPolicy) | بله | خیر/تصریح نشده | — |

<a id="contract-CreateInviteLinkCommand"></a>
### CreateInviteLinkCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientLinkId` | string / uuid | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `maximumUses` | integer / int32 | بله | خیر/تصریح نشده | — |
| `requiresApproval` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-CreateManagedConversationCommand"></a>
### CreateManagedConversationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `scope` | [ConversationScope](#contract-ConversationScope) | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `initialParticipantUserIds` | آرایه از string / uuid | بله | بله | — |
| `visibility` | [ConversationVisibility2](#contract-ConversationVisibility2) | خیر | خیر/تصریح نشده | — |
| `groupHistoryPolicy` | [GroupHistoryPolicy2](#contract-GroupHistoryPolicy2) | خیر | خیر/تصریح نشده | — |
| `memberCapabilities` | [NullableOfGroupMemberCapability](#contract-NullableOfGroupMemberCapability) | خیر | خیر/تصریح نشده | — |

<a id="contract-CreateNextExamVersionCommand"></a>
### CreateNextExamVersionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedExamRowVersion` | string | بله | خیر/تصریح نشده | — |
| `expectedPublishedVersionRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreateOrderCommand"></a>
### CreateOrderCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `planVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |

<a id="contract-CreateOrderResult"></a>
### CreateOrderResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `order` | [OrderDto](#contract-OrderDto) | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-CreateOrganizationPersonCommand"></a>
### CreateOrganizationPersonCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `firstName` | string | بله | خیر/تصریح نشده | — |
| `lastName` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | بله | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreatePersonLinkInvitationCommand"></a>
### CreatePersonLinkInvitationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `destinationUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreatePlatformInvitationCommand"></a>
### CreatePlatformInvitationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `role` | [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreatePlatformOrganizationCommand"></a>
### CreatePlatformOrganizationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string | بله | خیر/تصریح نشده | — |
| `name` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreatePlatformPlanCommand"></a>
### CreatePlatformPlanCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `code` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreatePlatformPlanVersionCommand"></a>
### CreatePlatformPlanVersionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `priceAmount` | number / double | بله | خیر/تصریح نشده | — |
| `currency` | [CurrencyCode](#contract-CurrencyCode) | بله | خیر/تصریح نشده | — |
| `entitlements` | آرایه از [PlatformPlanEntitlementItem](#contract-PlatformPlanEntitlementItem) | بله | خیر/تصریح نشده | — |
| `quotas` | آرایه از [PlatformPlanQuotaItem](#contract-PlatformPlanQuotaItem) | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreateReportExportCommand"></a>
### CreateReportExportCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `reportType` | [ReportExportType](#contract-ReportExportType) | بله | خیر/تصریح نشده | — |
| `format` | [ReportExportFormat](#contract-ReportExportFormat) | بله | خیر/تصریح نشده | — |
| `filters` | [ReportExportFilters](#contract-ReportExportFilters) | بله | خیر/تصریح نشده | — |

<a id="contract-CreateScheduleRuleCommand"></a>
### CreateScheduleRuleCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `localDayOfWeek` | [DayOfWeek](#contract-DayOfWeek) | بله | خیر/تصریح نشده | — |
| `localStartTime` | string / time | بله | خیر/تصریح نشده | — |
| `durationMinutes` | integer / int32 | بله | خیر/تصریح نشده | — |
| `timeZoneId` | string | بله | خیر/تصریح نشده | — |
| `effectiveFrom` | string / date | بله | خیر/تصریح نشده | — |
| `effectiveUntil` | string / date | بله | خیر/تصریح نشده | — |
| `sessionTitle` | string | بله | خیر/تصریح نشده | — |
| `meetingUrl` | string | بله | بله | — |

<a id="contract-CreateSchoolGuardianInvitationCommand"></a>
### CreateSchoolGuardianInvitationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `username` | string | بله | بله | — |
| `phoneNumber` | string | بله | بله | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreateSchoolGuardianRelationCommand"></a>
### CreateSchoolGuardianRelationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `guardianUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-CreateSessionCommand"></a>
### CreateSessionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `startUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `timeZoneId` | string | بله | خیر/تصریح نشده | — |
| `meetingUrl` | string | بله | بله | — |

<a id="contract-CreateStudyPlanCommand"></a>
### CreateStudyPlanCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `goals` | آرایه از string | بله | خیر/تصریح نشده | — |
| `availableMinutes` | integer / int32 | بله | بله | — |
| `notes` | string | بله | بله | — |

<a id="contract-CreatedInviteLinkResult"></a>
### CreatedInviteLinkResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `link` | [InviteLinkResult](#contract-InviteLinkResult) | بله | خیر/تصریح نشده | — |
| `token` | string | بله | بله | — |
| `alreadyExisted` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-CurrencyCode"></a>
### CurrencyCode

CLR واقعی: `MakanApp.Domain.Commerce.CurrencyCode`؛ Flags: False

literalهای JSON واقعی MVC: `["Irr"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-CurrentUserResult"></a>
### CurrentUserResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `phoneNumber` | string | بله | خیر/تصریح نشده | — |
| `isProfileComplete` | boolean | بله | خیر/تصریح نشده | — |
| `firstName` | string | بله | بله | — |
| `lastName` | string | بله | بله | — |
| `displayName` | string | بله | بله | — |
| `username` | string | بله | بله | — |
| `dateOfBirth` | string / date | خیر | بله | {"default": null} |
| `profilePhoto` | [ProfilePhotoResult](#contract-ProfilePhotoResult) | خیر | خیر/تصریح نشده | — |

<a id="contract-DashboardAssignmentItem"></a>
### DashboardAssignmentItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `dueAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `allowLateSubmission` | boolean | بله | خیر/تصریح نشده | — |
| `status` | [StudentAssignmentActionStatus](#contract-StudentAssignmentActionStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-DashboardAssignmentItem2"></a>
### DashboardAssignmentItem2

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `dueAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `allowLateSubmission` | boolean | بله | خیر/تصریح نشده | — |
| `status` | [StudentAssignmentActionStatus](#contract-StudentAssignmentActionStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-DashboardAttendancePendingItem"></a>
### DashboardAttendancePendingItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `sessionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `sessionTitle` | string | بله | خیر/تصریح نشده | — |
| `startUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `expectedEnrollmentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `recordedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `missingCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-DashboardAttendanceSummary"></a>
### DashboardAttendanceSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `eligibleSessionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `recordedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `presentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `absentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `lateCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `excusedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `notRecordedCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-DashboardAudience"></a>
### DashboardAudience

CLR واقعی: `MakanApp.Application.Reports.Dashboards.DashboardAudience`؛ Flags: False

literalهای JSON واقعی MVC: `["Student", "Parent", "Teacher", "Manager"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-DashboardClassItem"></a>
### DashboardClassItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `courseTitle` | string | بله | خیر/تصریح نشده | — |
| `academicPeriodTitle` | string | بله | خیر/تصریح نشده | — |
| `activeLearnerCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-DashboardExamItem"></a>
### DashboardExamItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `availableFromUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `availableUntilUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `durationMinutes` | integer / int32 | بله | خیر/تصریح نشده | — |
| `latestAttemptStatus` | [NullableOfExamAttemptStatus](#contract-NullableOfExamAttemptStatus) | بله | خیر/تصریح نشده | — |
| `resultReleaseStatus` | [NullableOfExamResultReleaseStatus](#contract-NullableOfExamResultReleaseStatus) | بله | خیر/تصریح نشده | — |
| `releasedScore` | number / double | بله | بله | — |
| `maximumScore` | number / double | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-DashboardLearner"></a>
### DashboardLearner

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |

<a id="contract-DashboardSessionItem"></a>
### DashboardSessionItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `sessionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `startUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `timeZoneId` | string | بله | خیر/تصریح نشده | — |
| `meetingUrl` | string | بله | بله | — |
| `status` | [SessionStatus](#contract-SessionStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-DashboardSessionItem2"></a>
### DashboardSessionItem2

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `sessionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `startUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `timeZoneId` | string | بله | خیر/تصریح نشده | — |
| `meetingUrl` | string | بله | بله | — |
| `status` | [SessionStatus](#contract-SessionStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-DayOfWeek"></a>
### DayOfWeek

CLR واقعی: `System.DayOfWeek`؛ Flags: False

literalهای JSON واقعی MVC: `["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-DeclineInvitationResult"></a>
### DeclineInvitationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `invitationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [InvitationStatus](#contract-InvitationStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-DeleteExamQuestionCommand"></a>
### DeleteExamQuestionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersionRowVersion` | string | بله | خیر/تصریح نشده | — |
| `expectedQuestionRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-DeleteMessageForMeResult"></a>
### DeleteMessageForMeResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `hiddenAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `alreadyHidden` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-DetailedReportAssessmentKind"></a>
### DetailedReportAssessmentKind

CLR واقعی: `MakanApp.Application.Reports.Detailed.DetailedReportAssessmentKind`؛ Flags: False

literalهای JSON واقعی MVC: `["Assignment", "Exam"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-DetailedReportGradeKind"></a>
### DetailedReportGradeKind

CLR واقعی: `MakanApp.Application.Reports.Detailed.DetailedReportGradeKind`؛ Flags: False

literalهای JSON واقعی MVC: `["Assignment", "Exam"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-DirectConversationResult"></a>
### DirectConversationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `type` | [ConversationType](#contract-ConversationType) | بله | خیر/تصریح نشده | — |
| `scope` | [ConversationScope](#contract-ConversationScope) | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `otherParticipant` | [SafeMessagingIdentityResult](#contract-SafeMessagingIdentityResult) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `alreadyExisted` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-DiscoverableConversationPageResult"></a>
### DiscoverableConversationPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [DiscoverableConversationResult](#contract-DiscoverableConversationResult) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-DiscoverableConversationResult"></a>
### DiscoverableConversationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `type` | [ConversationType](#contract-ConversationType) | بله | خیر/تصریح نشده | — |
| `scope` | [ConversationScope](#contract-ConversationScope) | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `organization` | [ConversationOrganizationBadgeResult](#contract-ConversationOrganizationBadgeResult) | بله | خیر/تصریح نشده | — |
| `isJoined` | boolean | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-EditMessageCommand"></a>
### EditMessageCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `text` | string | بله | بله | — |
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-EndEnrollmentCommand"></a>
### EndEnrollmentCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `finalStatus` | [EnrollmentStatus](#contract-EnrollmentStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-EndPlatformMembershipCommand"></a>
### EndPlatformMembershipCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-EndPlatformRoleAssignmentCommand"></a>
### EndPlatformRoleAssignmentCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-EnrollLearnerCommand"></a>
### EnrollLearnerCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |

<a id="contract-EnrollmentResult"></a>
### EnrollmentResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [EnrollmentStatus](#contract-EnrollmentStatus) | بله | خیر/تصریح نشده | — |
| `enrolledAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-EnrollmentStatus"></a>
### EnrollmentStatus

CLR واقعی: `MakanApp.Domain.Academic.EnrollmentStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Completed", "Withdrawn"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-EntitlementKey"></a>
### EntitlementKey

CLR واقعی: `MakanApp.Domain.Commerce.EntitlementKey`؛ Flags: False

literalهای JSON واقعی MVC: `["Academic", "Assignments", "Exams", "Messaging", "Reports", "Storage"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-EntityTagHeaderValue"></a>
### EntityTagHeaderValue

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `tag` | [StringSegment](#contract-StringSegment) | خیر | خیر/تصریح نشده | — |
| `isWeak` | boolean | خیر | خیر/تصریح نشده | — |

<a id="contract-EvaluationQueueItemResult"></a>
### EvaluationQueueItemResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `submissionAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentTitle` | string | بله | خیر/تصریح نشده | — |
| `studentDisplayName` | string | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `submittedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `isLate` | boolean | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `reviewStatus` | [EvaluationReviewStatus](#contract-EvaluationReviewStatus) | بله | خیر/تصریح نشده | — |
| `evaluationRevisionNumber` | integer / int32 | بله | بله | — |
| `score` | number / double | بله | بله | — |

<a id="contract-EvaluationReviewStatus"></a>
### EvaluationReviewStatus

CLR واقعی: `MakanApp.Application.Assessment.EvaluationReviewStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["AwaitingEvaluation", "Draft", "Released", "CorrectionDraft"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-EvaluationRevisionStatus"></a>
### EvaluationRevisionStatus

CLR واقعی: `MakanApp.Domain.Assessment.EvaluationRevisionStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Draft", "Released", "Superseded"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-EvaluatorEvaluationResult"></a>
### EvaluatorEvaluationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `submissionAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `revisionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `learnerFeedback` | string | بله | بله | — |
| `guardianVisibleFeedback` | string | بله | بله | — |
| `teacherPrivateNote` | string | بله | بله | — |
| `status` | [EvaluationRevisionStatus](#contract-EvaluationRevisionStatus) | بله | خیر/تصریح نشده | — |
| `createdByMembershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `supersedesEvaluationRevisionId` | string / uuid | بله | بله | — |
| `correctionReason` | string | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | بله | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ExamAnswerReceiptDto"></a>
### ExamAnswerReceiptDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examAttemptQuestionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `answerRevisionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `revisionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `acceptedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `writeLeaseVersion` | integer / int64 | بله | خیر/تصریح نشده | — |
| `answerSetVersion` | integer / int64 | بله | خیر/تصریح نشده | — |
| `status` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ExamAttemptStatus"></a>
### ExamAttemptStatus

CLR واقعی: `MakanApp.Domain.Assessment.ExamAttemptStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["InProgress", "Expired", "Finalized"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ExamEditorDto"></a>
### ExamEditorDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `status` | [ExamStatus](#contract-ExamStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `examRowVersion` | string | بله | خیر/تصریح نشده | — |
| `versionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `versionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `versionStatus` | [ExamVersionStatus](#contract-ExamVersionStatus) | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `availableFromUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `availableUntilUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `durationMinutes` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxAttempts` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `randomizationPolicy` | [ExamRandomizationPolicy](#contract-ExamRandomizationPolicy) | بله | خیر/تصریح نشده | — |
| `publishedAtUtc` | string / date-time | بله | بله | — |
| `versionRowVersion` | string | بله | خیر/تصریح نشده | — |
| `questions` | آرایه از [ExamEditorQuestionDto](#contract-ExamEditorQuestionDto) | بله | خیر/تصریح نشده | — |

<a id="contract-ExamEditorOptionDto"></a>
### ExamEditorOptionDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `order` | integer / int32 | بله | خیر/تصریح نشده | — |
| `text` | string | بله | خیر/تصریح نشده | — |
| `isCorrect` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ExamEditorQuestionDto"></a>
### ExamEditorQuestionDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `order` | integer / int32 | بله | خیر/تصریح نشده | — |
| `type` | [ExamQuestionType](#contract-ExamQuestionType) | بله | خیر/تصریح نشده | — |
| `prompt` | string | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `options` | آرایه از [ExamEditorOptionDto](#contract-ExamEditorOptionDto) | بله | خیر/تصریح نشده | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ExamFinalReceiptDto"></a>
### ExamFinalReceiptDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `startedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `finalizedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `finalizedAnswerSetVersion` | integer / int64 | بله | خیر/تصریح نشده | — |
| `answeredQuestionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalQuestionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `status` | [ExamAttemptStatus](#contract-ExamAttemptStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-ExamGradeDto"></a>
### ExamGradeDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examGradeRevisionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examTitle` | string | بله | خیر/تصریح نشده | — |
| `studentDisplayName` | string | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `finalizedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `revisionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalScore` | number / double | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `status` | [ExamGradeRevisionStatus](#contract-ExamGradeRevisionStatus) | بله | خیر/تصریح نشده | — |
| `learnerFeedback` | string | بله | بله | — |
| `guardianVisibleFeedback` | string | بله | بله | — |
| `evaluatorPrivateNote` | string | بله | بله | — |
| `createdByMembershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `supersedesExamGradeRevisionId` | string / uuid | بله | بله | — |
| `correctionReason` | string | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | بله | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |
| `questions` | آرایه از [ExamQuestionGradeDto](#contract-ExamQuestionGradeDto) | بله | خیر/تصریح نشده | — |

<a id="contract-ExamGradeReleaseDto"></a>
### ExamGradeReleaseDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examGradeReleaseId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examGradeRevisionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `gradeRevisionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalScore` | number / double | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `releasedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `releasedByMembershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `gradeRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ExamGradeRevisionStatus"></a>
### ExamGradeRevisionStatus

CLR واقعی: `MakanApp.Domain.Assessment.ExamGradeRevisionStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Draft", "ReadyForRelease", "Released", "Superseded"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ExamGradingQueueItemDto"></a>
### ExamGradingQueueItemDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examTitle` | string | بله | خیر/تصریح نشده | — |
| `studentDisplayName` | string | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `finalizedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `status` | [ExamGradingQueueStatus](#contract-ExamGradingQueueStatus) | بله | خیر/تصریح نشده | — |
| `gradeRevisionNumber` | integer / int32 | بله | بله | — |
| `requiresManualReview` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ExamGradingQueueStatus"></a>
### ExamGradingQueueStatus

CLR واقعی: `MakanApp.Application.Assessment.ExamGradingQueueStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["AwaitingGrading", "Draft", "ReadyForRelease", "Released", "CorrectionDraft"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ExamGradingQueueSummary"></a>
### ExamGradingQueueSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `awaitingGradingCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `manualReviewCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `readyForReleaseCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalPendingCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-ExamQuestionGradeDto"></a>
### ExamQuestionGradeDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptQuestionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `questionVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `displayOrder` | integer / int32 | بله | خیر/تصریح نشده | — |
| `questionType` | [ExamQuestionType](#contract-ExamQuestionType) | بله | خیر/تصریح نشده | — |
| `prompt` | string | بله | خیر/تصریح نشده | — |
| `isAnswered` | boolean | بله | خیر/تصریح نشده | — |
| `selectedOptionText` | string | بله | بله | — |
| `textAnswer` | string | بله | بله | — |
| `awardedScore` | number / double | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `gradingMode` | [ExamQuestionGradingMode](#contract-ExamQuestionGradingMode) | بله | خیر/تصریح نشده | — |
| `isReviewed` | boolean | بله | خیر/تصریح نشده | — |
| `learnerFeedback` | string | بله | بله | — |
| `evaluatorPrivateNote` | string | بله | بله | — |
| `reviewedByMembershipId` | string / uuid | بله | بله | — |
| `reviewedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-ExamQuestionGradingMode"></a>
### ExamQuestionGradingMode

CLR واقعی: `MakanApp.Domain.Assessment.ExamQuestionGradingMode`؛ Flags: False

literalهای JSON واقعی MVC: `["Objective", "Manual"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ExamQuestionOptionCommand"></a>
### ExamQuestionOptionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `order` | integer / int32 | بله | خیر/تصریح نشده | — |
| `text` | string | بله | خیر/تصریح نشده | — |
| `isCorrect` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ExamQuestionType"></a>
### ExamQuestionType

CLR واقعی: `MakanApp.Domain.Assessment.ExamQuestionType`؛ Flags: False

literalهای JSON واقعی MVC: `["ObjectiveSingleChoice", "Descriptive"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ExamRandomizationPolicy"></a>
### ExamRandomizationPolicy

CLR واقعی: `MakanApp.Domain.Assessment.ExamRandomizationPolicy`؛ Flags: False

literalهای JSON واقعی MVC: `["None", "QuestionOrder"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ExamResultReleaseStatus"></a>
### ExamResultReleaseStatus

CLR واقعی: `MakanApp.Application.Assessment.ExamResultReleaseStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["AwaitingGrading", "AwaitingRelease", "Released"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ExamStatus"></a>
### ExamStatus

CLR واقعی: `MakanApp.Domain.Assessment.ExamStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Draft", "Published"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ExamTeacherPreview"></a>
### ExamTeacherPreview

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `exam` | [StudentExamSummary](#contract-StudentExamSummary) | بله | خیر/تصریح نشده | — |
| `questions` | آرایه از [ExamEditorQuestionDto](#contract-ExamEditorQuestionDto) | بله | خیر/تصریح نشده | — |

<a id="contract-ExamVersionStatus"></a>
### ExamVersionStatus

CLR واقعی: `MakanApp.Domain.Assessment.ExamVersionStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Draft", "Published"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ExamWriteLeaseDto"></a>
### ExamWriteLeaseDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `isCurrentSessionWriter` | boolean | بله | خیر/تصریح نشده | — |
| `writeLeaseVersion` | integer / int64 | بله | خیر/تصریح نشده | — |
| `canRequestTransfer` | boolean | بله | خیر/تصریح نشده | — |
| `acquiredAtUtc` | string / date-time | بله | بله | — |
| `answerSetVersion` | integer / int64 | بله | خیر/تصریح نشده | — |
| `serverNowUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-ExplainHomeworkCommand"></a>
### ExplainHomeworkCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `question` | string | بله | خیر/تصریح نشده | — |

<a id="contract-FileAssetResult"></a>
### FileAssetResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `uploadedByUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `originalFileName` | string | بله | خیر/تصریح نشده | — |
| `contentType` | string | بله | خیر/تصریح نشده | — |
| `sizeBytes` | integer / int64 | بله | خیر/تصریح نشده | — |
| `sha256Hash` | string | بله | بله | — |
| `status` | [FileAssetStatus](#contract-FileAssetStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `completedAtUtc` | string / date-time | بله | بله | — |
| `deletedAtUtc` | string / date-time | بله | بله | — |
| `retainedAtUtc` | string / date-time | بله | بله | — |
| `unattachedExpiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-FileAssetStatus"></a>
### FileAssetStatus

CLR واقعی: `MakanApp.Domain.Storage.FileAssetStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Pending", "Ready", "Rejected", "Deleted"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-FileStreamResult"></a>
### FileStreamResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileStream` | [Stream](#contract-Stream) | خیر | خیر/تصریح نشده | — |
| `contentType` | string | خیر | بله | — |
| `fileDownloadName` | string | خیر | بله | — |
| `lastModified` | string / date-time | خیر | بله | — |
| `entityTag` | [EntityTagHeaderValue](#contract-EntityTagHeaderValue) | خیر | خیر/تصریح نشده | — |
| `enableRangeProcessing` | boolean | خیر | خیر/تصریح نشده | — |

<a id="contract-FinalSubmitAssignmentCommand"></a>
### FinalSubmitAssignmentCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-FinalizeExamCommand"></a>
### FinalizeExamCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `expectedAnswerSetVersion` | integer / int64 | بله | خیر/تصریح نشده | — |
| `writeLeaseVersion` | integer / int64 | بله | خیر/تصریح نشده | — |

<a id="contract-FolderConversationTypes"></a>
### FolderConversationTypes

CLR واقعی: `MakanApp.Domain.Messaging.FolderConversationTypes`؛ Flags: True

literalهای JSON واقعی MVC: `["None", "Direct", "Group", "Channel", "Saved", "All"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-FolderOverrideCommand"></a>
### FolderOverrideCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `included` | boolean | بله | خیر/تصریح نشده | — |
| `pinOrder` | integer / int32 | خیر | بله | {"default": null} |

<a id="contract-ForwardMessageCommand"></a>
### ForwardMessageCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `destinationConversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `clientMessageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `destinationTopicId` | string / uuid | خیر | بله | {"default": null} |
| `isSilent` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-GradeExamQuestionCommand"></a>
### GradeExamQuestionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `awardedScore` | number / double | بله | خیر/تصریح نشده | — |
| `learnerFeedback` | string | بله | بله | — |
| `evaluatorPrivateNote` | string | بله | بله | — |
| `expectedGradeRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-GradeReleaseResult"></a>
### GradeReleaseResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `gradeReleaseId` | string / uuid | بله | خیر/تصریح نشده | — |
| `submissionAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `evaluationRevisionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `revisionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `releasedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `releasedByMembershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `evaluationRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-GrantPlatformRoleCommand"></a>
### GrantPlatformRoleCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `roleKey` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-GroupHistoryPolicy"></a>
### GroupHistoryPolicy

CLR واقعی: `MakanApp.Domain.Messaging.GroupHistoryPolicy`؛ Flags: False

literalهای JSON واقعی MVC: `["FullHistory", "FromJoin"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-GroupHistoryPolicy2"></a>
### GroupHistoryPolicy2

string / FullHistory, FromJoin

<a id="contract-GroupMemberCapability"></a>
### GroupMemberCapability

CLR واقعی: `MakanApp.Domain.Messaging.GroupMemberCapability`؛ Flags: True

literalهای JSON واقعی MVC: `["None", "SendMessages", "SendMedia", "LegacyMemberDefault", "InviteMembers", "All"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-GuardianExamResultDto"></a>
### GuardianExamResultDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `finalizedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `releaseStatus` | [ExamResultReleaseStatus](#contract-ExamResultReleaseStatus) | بله | خیر/تصریح نشده | — |
| `gradeRevisionNumber` | integer / int32 | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | بله | — |
| `score` | number / double | بله | بله | — |
| `maximumScore` | number / double | بله | بله | — |
| `guardianVisibleFeedback` | string | بله | بله | — |

<a id="contract-GuardianRelationStatus"></a>
### GuardianRelationStatus

CLR واقعی: `MakanApp.Domain.Guardian.GuardianRelationStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Pending", "Active", "Ended", "Revoked"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-GuardianRelationSummaryResult"></a>
### GuardianRelationSummaryResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `relationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationName` | string | بله | خیر/تصریح نشده | — |
| `status` | [GuardianRelationStatus](#contract-GuardianRelationStatus) | بله | خیر/تصریح نشده | — |
| `validFromUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-InitiatePaymentCommand"></a>
### InitiatePaymentCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |

<a id="contract-InitiatePaymentResult"></a>
### InitiatePaymentResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `order` | [OrderDto](#contract-OrderDto) | بله | خیر/تصریح نشده | — |
| `paymentAttempt` | [PaymentAttemptDto](#contract-PaymentAttemptDto) | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-InvitationResult"></a>
### InvitationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationName` | string | بله | خیر/تصریح نشده | — |
| `invitedByUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `role` | [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `status` | [InvitationStatus](#contract-InvitationStatus) | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `organizationPersonId` | string / uuid | خیر | بله | {"default": null} |
| `organizationPersonDisplayName` | string | خیر | بله | {"default": null} |
| `guardianLearnerOrganizationPersonId` | string / uuid | خیر | بله | {"default": null} |
| `guardianLearnerDisplayName` | string | خیر | بله | {"default": null} |
| `requiresPersonLinkConsent` | boolean | خیر | خیر/تصریح نشده | — |
| `requiresGuardianConsent` | boolean | خیر | خیر/تصریح نشده | — |
| `nextAction` | string | خیر | بله | — |

<a id="contract-InvitationStatus"></a>
### InvitationStatus

CLR واقعی: `MakanApp.Domain.Organization.InvitationStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Pending", "Accepted", "Declined", "Expired", "Revoked"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-InviteLinkPageResult"></a>
### InviteLinkPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [InviteLinkResult](#contract-InviteLinkResult) | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-InviteLinkRedemptionResult"></a>
### InviteLinkRedemptionResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `alreadyMember` | boolean | بله | خیر/تصریح نشده | — |
| `joinRequest` | [ConversationJoinRequestResult](#contract-ConversationJoinRequestResult) | بله | خیر/تصریح نشده | — |

<a id="contract-InviteLinkResult"></a>
### InviteLinkResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `createdByUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `maximumUses` | integer / int32 | بله | خیر/تصریح نشده | — |
| `usedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `requiresApproval` | boolean | بله | خیر/تصریح نشده | — |
| `revokedAtUtc` | string / date-time | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-LessonAttachmentResult"></a>
### LessonAttachmentResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileAssetId` | string / uuid | بله | خیر/تصریح نشده | — |
| `fileName` | string | بله | خیر/تصریح نشده | — |
| `contentType` | string | بله | خیر/تصریح نشده | — |
| `sizeBytes` | integer / int64 | بله | خیر/تصریح نشده | — |
| `status` | [FileAssetStatus](#contract-FileAssetStatus) | بله | خیر/تصریح نشده | — |
| `isAvailable` | boolean | بله | خیر/تصریح نشده | — |
| `downloadUrl` | string | بله | بله | — |

<a id="contract-LessonStatus"></a>
### LessonStatus

CLR واقعی: `MakanApp.Domain.Academic.LessonStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Draft", "Published"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ManagedConversationResult"></a>
### ManagedConversationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `type` | [ConversationType](#contract-ConversationType) | بله | خیر/تصریح نشده | — |
| `scope` | [ConversationScope](#contract-ConversationScope) | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `managementPolicy` | [ConversationManagementPolicy](#contract-ConversationManagementPolicy) | بله | خیر/تصریح نشده | — |
| `visibility` | [ConversationVisibility](#contract-ConversationVisibility) | بله | خیر/تصریح نشده | — |
| `groupHistoryPolicy` | [GroupHistoryPolicy](#contract-GroupHistoryPolicy) | بله | خیر/تصریح نشده | — |
| `memberCapabilities` | [GroupMemberCapability](#contract-GroupMemberCapability) | بله | خیر/تصریح نشده | — |
| `status` | [ConversationStatus](#contract-ConversationStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `archivedAtUtc` | string / date-time | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |
| `participants` | آرایه از [ConversationParticipantResult](#contract-ConversationParticipantResult) | بله | خیر/تصریح نشده | — |
| `alreadyExisted` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ManagerAssessmentReportItem"></a>
### ManagerAssessmentReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `kind` | [DetailedReportAssessmentKind](#contract-DetailedReportAssessmentKind) | بله | خیر/تصریح نشده | — |
| `assessmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `versionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `courseTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `deadlineUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `recipientCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `submittedOrFinalizedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `releasedResultCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pendingGradingCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-ManagerAttendanceReportItem"></a>
### ManagerAttendanceReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `courseTitle` | string | بله | خیر/تصریح نشده | — |
| `eligibleRecordCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `recordedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `presentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `absentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `lateCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `excusedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `notRecordedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `recordedAbsencePercentage` | number / double | بله | بله | — |

<a id="contract-ManagerClassPerformanceDto"></a>
### ManagerClassPerformanceDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `courseTitle` | string | بله | خیر/تصریح نشده | — |
| `activeLearnerCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `assignmentReleasedResultCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `assignmentAveragePercentage` | number / double | بله | بله | — |
| `examReleasedResultCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `examAveragePercentage` | number / double | بله | بله | — |

<a id="contract-ManagerCommerceSummaryDto"></a>
### ManagerCommerceSummaryDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `state` | [CommerceSummaryState](#contract-CommerceSummaryState) | بله | خیر/تصریح نشده | — |
| `subscription` | [CommerceSubscriptionDto](#contract-CommerceSubscriptionDto) | بله | خیر/تصریح نشده | — |
| `entitlements` | آرایه از [CommerceEntitlementDto](#contract-CommerceEntitlementDto) | بله | خیر/تصریح نشده | — |
| `quotas` | آرایه از [CommerceQuotaDto](#contract-CommerceQuotaDto) | بله | خیر/تصریح نشده | — |
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-ManagerDashboardDto"></a>
### ManagerDashboardDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `audience` | [DashboardAudience](#contract-DashboardAudience) | بله | خیر/تصریح نشده | — |
| `operationalCounts` | [ManagerOperationalCounts](#contract-ManagerOperationalCounts) | بله | خیر/تصریح نشده | — |
| `onboarding` | [ManagerOnboardingChecklist](#contract-ManagerOnboardingChecklist) | بله | خیر/تصریح نشده | — |
| `upcomingSessions` | آرایه از [DashboardSessionItem2](#contract-DashboardSessionItem2) | بله | خیر/تصریح نشده | — |
| `sessionsMissingMeetingLink` | آرایه از بدون schema منتشرشده | بله | خیر/تصریح نشده | — |
| `attendancePending` | آرایه از [DashboardAttendancePendingItem](#contract-DashboardAttendancePendingItem) | بله | خیر/تصریح نشده | — |
| `pendingInvitationCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `assignmentGradingQueue` | [AssignmentGradingQueueSummary](#contract-AssignmentGradingQueueSummary) | بله | خیر/تصریح نشده | — |
| `examGradingQueue` | [ExamGradingQueueSummary](#contract-ExamGradingQueueSummary) | بله | خیر/تصریح نشده | — |

<a id="contract-ManagerEnrollmentReportItem"></a>
### ManagerEnrollmentReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `enrollmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerDisplayName` | string | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `courseId` | string / uuid | بله | خیر/تصریح نشده | — |
| `courseTitle` | string | بله | خیر/تصریح نشده | — |
| `academicPeriodId` | string / uuid | بله | خیر/تصریح نشده | — |
| `academicPeriodTitle` | string | بله | خیر/تصریح نشده | — |
| `status` | [EnrollmentStatus](#contract-EnrollmentStatus) | بله | خیر/تصریح نشده | — |
| `enrolledAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-ManagerOnboardingChecklist"></a>
### ManagerOnboardingChecklist

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `organizationBasicDataAvailable` | boolean | بله | خیر/تصریح نشده | — |
| `hasAcademicPeriod` | boolean | بله | خیر/تصریح نشده | — |
| `hasCourse` | boolean | بله | خیر/تصریح نشده | — |
| `hasTeacher` | boolean | بله | خیر/تصریح نشده | — |
| `hasLearner` | boolean | بله | خیر/تصریح نشده | — |
| `hasClass` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ManagerOperationalCounts"></a>
### ManagerOperationalCounts

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `activeClassCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `enrolledLearnerCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `teacherCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-ManagerTeacherWorkloadReportItem"></a>
### ManagerTeacherWorkloadReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `teacherMembershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `teacherUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `teacherDisplayName` | string | بله | خیر/تصریح نشده | — |
| `activeClassCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `completedSessionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `attendanceMissingCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `assignmentPendingGradingCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `examPendingGradingCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-MemberRestrictionPageResult"></a>
### MemberRestrictionPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [MemberRestrictionResult](#contract-MemberRestrictionResult) | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-MemberRestrictionResult"></a>
### MemberRestrictionResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `isBanned` | boolean | بله | خیر/تصریح نشده | — |
| `deniedCapabilities` | [GroupMemberCapability](#contract-GroupMemberCapability) | بله | خیر/تصریح نشده | — |
| `untilUtc` | string / date-time | بله | بله | — |
| `isEffective` | boolean | بله | خیر/تصریح نشده | — |
| `version` | string | بله | بله | — |

<a id="contract-MembershipStatus"></a>
### MembershipStatus

CLR واقعی: `MakanApp.Domain.Organization.MembershipStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Suspended", "Ended"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-MessageAttachmentResult"></a>
### MessageAttachmentResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileAssetId` | string / uuid | بله | خیر/تصریح نشده | — |
| `kind` | [MessageKind](#contract-MessageKind) | بله | خیر/تصریح نشده | — |
| `fileName` | string | بله | خیر/تصریح نشده | — |
| `contentType` | string | بله | خیر/تصریح نشده | — |
| `sizeBytes` | integer / int64 | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-MessageKind"></a>
### MessageKind

CLR واقعی: `MakanApp.Domain.Messaging.MessageKind`؛ Flags: False

literalهای JSON واقعی MVC: `["Text", "Image", "Video", "Voice", "File", "ContactCard", "Poll", "Checklist"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-MessageMutationResult"></a>
### MessageMutationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `editedAtUtc` | string / date-time | بله | بله | — |
| `deletedAtUtc` | string / date-time | بله | بله | — |
| `isDeleted` | boolean | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-MessageReactionResult"></a>
### MessageReactionResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `reaction` | [MessageReactionType](#contract-MessageReactionType) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-MessageReactionSummaryResult"></a>
### MessageReactionSummaryResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `reaction` | [MessageReactionType](#contract-MessageReactionType) | بله | خیر/تصریح نشده | — |
| `count` | integer / int32 | بله | خیر/تصریح نشده | — |
| `reactedByCurrentUser` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-MessageReactionType"></a>
### MessageReactionType

CLR واقعی: `MakanApp.Domain.Messaging.MessageReactionType`؛ Flags: False

literalهای JSON واقعی MVC: `["Like", "Love", "Laugh", "Wow", "Sad"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-MessageReceiptResult"></a>
### MessageReceiptResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `clientMessageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `kind` | [MessageKind](#contract-MessageKind) | بله | خیر/تصریح نشده | — |
| `sentAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `status` | [MessageReceiptStatus](#contract-MessageReceiptStatus) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-MessageReceiptStatus"></a>
### MessageReceiptStatus

CLR واقعی: `MakanApp.Application.Messaging.MessageReceiptStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Sent"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-MessageReplySummaryResult"></a>
### MessageReplySummaryResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `senderUserId` | string / uuid | بله | بله | — |
| `kind` | [NullableOfMessageKind](#contract-NullableOfMessageKind) | بله | خیر/تصریح نشده | — |
| `text` | string | بله | بله | — |
| `isDeleted` | boolean | بله | خیر/تصریح نشده | — |
| `isUnavailable` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-MessageSearchItemResult"></a>
### MessageSearchItemResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationDisplayName` | string | بله | بله | — |
| `sender` | [SafeMessagingIdentityResult](#contract-SafeMessagingIdentityResult) | بله | خیر/تصریح نشده | — |
| `snippet` | string | بله | بله | — |
| `kind` | [MessageKind](#contract-MessageKind) | بله | خیر/تصریح نشده | — |
| `sentAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `sequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `attachments` | آرایه از [MessageAttachmentResult](#contract-MessageAttachmentResult) | بله | خیر/تصریح نشده | — |

<a id="contract-MessageSearchPageResult"></a>
### MessageSearchPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [MessageSearchItemResult](#contract-MessageSearchItemResult) | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `nextCursor` | string | بله | بله | — |

<a id="contract-MessagingChangeType"></a>
### MessagingChangeType

CLR واقعی: `MakanApp.Domain.Messaging.MessagingChangeType`؛ Flags: False

literalهای JSON واقعی MVC: `["MessageCreated", "MessageEdited", "MessageDeleted", "ReactionChanged", "PinChanged", "ParticipantChanged", "ConversationChanged", "ReadCursorAdvanced", "DeliveryCursorAdvanced", "MessageHiddenForUser", "ConversationUserStateChanged", "PeerReadStateChanged", "TopicChanged", "PollChanged", "ChecklistChanged"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-MessagingNextAction"></a>
### MessagingNextAction

CLR واقعی: `MakanApp.Application.Messaging.MessagingNextAction`؛ Flags: False

literalهای JSON واقعی MVC: `["None", "VerificationRequired", "ProfileCompletionRequired", "VerificationConflictResolutionRequired", "MessagingRestrictionActive", "SelectPersonalWorkspace"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-MessagingPresenceState"></a>
### MessagingPresenceState

CLR واقعی: `MakanApp.Application.Messaging.MessagingPresenceState`؛ Flags: False

literalهای JSON واقعی MVC: `["Online", "Recently", "Offline", "Hidden"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-MessagingPrivacySettingsResult"></a>
### MessagingPrivacySettingsResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `directMessagesAudience` | [PrivacyAudience](#contract-PrivacyAudience) | بله | خیر/تصریح نشده | — |
| `phoneNumberVisibilityAudience` | [PrivacyAudience](#contract-PrivacyAudience) | بله | خیر/تصریح نشده | — |
| `phoneNumberDiscoveryAudience` | [PrivacyAudience](#contract-PrivacyAudience) | بله | خیر/تصریح نشده | — |
| `groupInvitesAudience` | [PrivacyAudience](#contract-PrivacyAudience) | بله | خیر/تصریح نشده | — |
| `isExplicit` | boolean | بله | خیر/تصریح نشده | — |
| `version` | string | بله | بله | — |
| `onlineAudience` | [PrivacyAudience2](#contract-PrivacyAudience2) | خیر | خیر/تصریح نشده | — |
| `lastSeenAudience` | [PrivacyAudience2](#contract-PrivacyAudience2) | خیر | خیر/تصریح نشده | — |

<a id="contract-MessagingPublicProfileResult"></a>
### MessagingPublicProfileResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `username` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `phoneNumber` | string | بله | بله | — |
| `isSavedContact` | boolean | بله | خیر/تصریح نشده | — |
| `isFavorite` | boolean | بله | خیر/تصریح نشده | — |
| `canStartDirect` | boolean | بله | خیر/تصریح نشده | — |
| `profilePhoto` | [ProfilePhotoResult](#contract-ProfilePhotoResult) | خیر | خیر/تصریح نشده | — |

<a id="contract-MessagingPublicProfileResult2"></a>
### MessagingPublicProfileResult2

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `username` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `phoneNumber` | string | بله | بله | — |
| `isSavedContact` | boolean | بله | خیر/تصریح نشده | — |
| `isFavorite` | boolean | بله | خیر/تصریح نشده | — |
| `canStartDirect` | boolean | بله | خیر/تصریح نشده | — |
| `profilePhoto` | [ProfilePhotoResult](#contract-ProfilePhotoResult) | خیر | خیر/تصریح نشده | — |

<a id="contract-MessagingStatusResult"></a>
### MessagingStatusResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `verificationStatus` | [CommunicationAgeVerificationStatus](#contract-CommunicationAgeVerificationStatus) | بله | خیر/تصریح نشده | — |
| `effectiveAgeCategory` | [CommunicationAgeCategory](#contract-CommunicationAgeCategory) | بله | خیر/تصریح نشده | — |
| `allowedActions` | آرایه از [PersonalMessagingAction](#contract-PersonalMessagingAction) | بله | خیر/تصریح نشده | — |
| `nextAction` | [MessagingNextAction](#contract-MessagingNextAction) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ModerationActionResult"></a>
### ModerationActionResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `type` | [ModerationActionType](#contract-ModerationActionType) | بله | خیر/تصریح نشده | — |
| `actorUserId` | string / uuid | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-ModerationActionType"></a>
### ModerationActionType

CLR واقعی: `MakanApp.Domain.Messaging.ModerationActionType`؛ Flags: False

literalهای JSON واقعی MVC: `["Created", "ReviewStarted", "Resolved", "Dismissed", "Escalated", "ContentRemoved", "Warned", "MessagingRestricted"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ModerationCaseDetailResult"></a>
### ModerationCaseDetailResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `caseId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `source` | [ModerationCaseSource](#contract-ModerationCaseSource) | بله | خیر/تصریح نشده | — |
| `priority` | [ModerationPriority](#contract-ModerationPriority) | بله | خیر/تصریح نشده | — |
| `status` | [ModerationCaseStatus](#contract-ModerationCaseStatus) | بله | خیر/تصریح نشده | — |
| `assignedReviewerUserId` | string / uuid | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `reviewStartedAtUtc` | string / date-time | بله | بله | — |
| `resolvedAtUtc` | string / date-time | بله | بله | — |
| `dismissedAtUtc` | string / date-time | بله | بله | — |
| `reportId` | string / uuid | بله | خیر/تصریح نشده | — |
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `reason` | [AbuseReportReason](#contract-AbuseReportReason) | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `reporter` | [SafeMessagingIdentityResult](#contract-SafeMessagingIdentityResult) | بله | خیر/تصریح نشده | — |
| `reportedUser` | [SafeMessagingIdentityResult](#contract-SafeMessagingIdentityResult) | بله | خیر/تصریح نشده | — |
| `reportedMessageKind` | [MessageKind](#contract-MessageKind) | بله | خیر/تصریح نشده | — |
| `reportedContentSnapshot` | string | بله | بله | — |
| `attachments` | آرایه از [ModerationEvidenceAttachmentResult](#contract-ModerationEvidenceAttachmentResult) | بله | خیر/تصریح نشده | — |
| `actions` | آرایه از [ModerationActionResult](#contract-ModerationActionResult) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ModerationCasePageResult"></a>
### ModerationCasePageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [ModerationCaseSummaryResult](#contract-ModerationCaseSummaryResult) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-ModerationCaseSource"></a>
### ModerationCaseSource

CLR واقعی: `MakanApp.Domain.Messaging.ModerationCaseSource`؛ Flags: False

literalهای JSON واقعی MVC: `["UserReport", "AutomatedSafety"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ModerationCaseStatus"></a>
### ModerationCaseStatus

CLR واقعی: `MakanApp.Domain.Messaging.ModerationCaseStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Pending", "InReview", "Resolved", "Dismissed"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ModerationCaseSummaryResult"></a>
### ModerationCaseSummaryResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `caseId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `source` | [ModerationCaseSource](#contract-ModerationCaseSource) | بله | خیر/تصریح نشده | — |
| `priority` | [ModerationPriority](#contract-ModerationPriority) | بله | خیر/تصریح نشده | — |
| `status` | [ModerationCaseStatus](#contract-ModerationCaseStatus) | بله | خیر/تصریح نشده | — |
| `assignedReviewerUserId` | string / uuid | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ModerationEvidenceAttachmentResult"></a>
### ModerationEvidenceAttachmentResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileAssetId` | string / uuid | بله | خیر/تصریح نشده | — |
| `fileName` | string | بله | خیر/تصریح نشده | — |
| `contentType` | string | بله | خیر/تصریح نشده | — |
| `sizeBytes` | integer / int64 | بله | خیر/تصریح نشده | — |

<a id="contract-ModerationPriority"></a>
### ModerationPriority

CLR واقعی: `MakanApp.Domain.Messaging.ModerationPriority`؛ Flags: False

literalهای JSON واقعی MVC: `["Normal", "High", "Critical"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-NotificationCategory"></a>
### NotificationCategory

CLR واقعی: `MakanApp.Domain.Notification.NotificationCategory`؛ Flags: False

literalهای JSON واقعی MVC: `["Educational", "Communication", "AccountSystem"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-NotificationDeliveryChannel"></a>
### NotificationDeliveryChannel

CLR واقعی: `MakanApp.Domain.Notification.NotificationDeliveryChannel`؛ Flags: False

literalهای JSON واقعی MVC: `["Push", "Sms"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-NotificationDeliveryStatus"></a>
### NotificationDeliveryStatus

CLR واقعی: `MakanApp.Domain.Notification.NotificationDeliveryStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Pending", "Processing", "Deferred", "Delivered", "Failed", "Suppressed"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-NotificationItem"></a>
### NotificationItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `category` | [NotificationCategory](#contract-NotificationCategory) | بله | خیر/تصریح نشده | — |
| `type` | [NotificationType](#contract-NotificationType) | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `body` | string | بله | خیر/تصریح نشده | — |
| `resourceType` | [NotificationResourceType](#contract-NotificationResourceType) | بله | خیر/تصریح نشده | — |
| `resourceId` | string / uuid | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `readAtUtc` | string / date-time | بله | بله | — |

<a id="contract-NotificationPage"></a>
### NotificationPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [NotificationItem](#contract-NotificationItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-NotificationPreferenceCategory"></a>
### NotificationPreferenceCategory

CLR واقعی: `MakanApp.Domain.Notification.NotificationPreferenceCategory`؛ Flags: False

literalهای JSON واقعی MVC: `["Messages", "Assignments", "Results", "Sessions", "Reports", "Invitations", "AccountSecurity"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-NotificationPreferenceItem"></a>
### NotificationPreferenceItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `scope` | [NotificationPreferenceScope](#contract-NotificationPreferenceScope) | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `category` | [NotificationPreferenceCategory](#contract-NotificationPreferenceCategory) | بله | خیر/تصریح نشده | — |
| `inAppEnabled` | boolean | بله | خیر/تصریح نشده | — |
| `pushEnabled` | boolean | بله | خیر/تصریح نشده | — |
| `smsEnabled` | boolean | بله | خیر/تصریح نشده | — |
| `isMandatory` | boolean | بله | خیر/تصریح نشده | — |
| `canDisablePush` | boolean | بله | خیر/تصریح نشده | — |
| `canDisableSms` | boolean | بله | خیر/تصریح نشده | — |
| `quietHoursEnabled` | boolean | بله | خیر/تصریح نشده | — |
| `quietHoursStartLocal` | string / time | بله | بله | — |
| `quietHoursEndLocal` | string / time | بله | بله | — |
| `timeZoneId` | string | بله | بله | — |
| `isExplicit` | boolean | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | بله | — |
| `rowVersion` | string | بله | بله | — |

<a id="contract-NotificationPreferenceScope"></a>
### NotificationPreferenceScope

CLR واقعی: `MakanApp.Application.Notification.NotificationPreferenceScope`؛ Flags: False

literalهای JSON واقعی MVC: `["Personal", "Organization"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-NotificationResourceType"></a>
### NotificationResourceType

CLR واقعی: `MakanApp.Domain.Notification.NotificationResourceType`؛ Flags: False

literalهای JSON واقعی MVC: `["Assignment", "SubmissionAttempt", "Exam", "ExamAttempt", "Conversation"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-NotificationType"></a>
### NotificationType

CLR واقعی: `MakanApp.Domain.Notification.NotificationType`؛ Flags: False

literalهای JSON واقعی MVC: `["AssignmentPublished", "AssignmentGradeReleased", "ExamPublished", "ExamGradeReleased", "MessageReceived", "MessageReply", "MessageMention"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-NotificationUnreadCount"></a>
### NotificationUnreadCount

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `count` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-NullableOfConversationChecklistMode"></a>
### NullableOfConversationChecklistMode

string / Personal, Collaborative, None

<a id="contract-NullableOfCurrencyCode"></a>
### NullableOfCurrencyCode

string / Irr, None

<a id="contract-NullableOfEnrollmentStatus"></a>
### NullableOfEnrollmentStatus

string / Active, Completed, Withdrawn, None

<a id="contract-NullableOfEvaluationRevisionStatus"></a>
### NullableOfEvaluationRevisionStatus

string / Draft, Released, Superseded, None

<a id="contract-NullableOfExamAttemptStatus"></a>
### NullableOfExamAttemptStatus

string / InProgress, Expired, Finalized, None

<a id="contract-NullableOfExamGradeRevisionStatus"></a>
### NullableOfExamGradeRevisionStatus

string / Draft, ReadyForRelease, Released, Superseded, None

<a id="contract-NullableOfExamResultReleaseStatus"></a>
### NullableOfExamResultReleaseStatus

string / AwaitingGrading, AwaitingRelease, Released, None

<a id="contract-NullableOfGroupMemberCapability"></a>
### NullableOfGroupMemberCapability

string

<a id="contract-NullableOfMessageKind"></a>
### NullableOfMessageKind

string / Text, Image, Video, Voice, File, ContactCard, Poll, Checklist, None

<a id="contract-NullableOfNotificationDeliveryFailureReason"></a>
### NullableOfNotificationDeliveryFailureReason

string / SuppressedByPreference, NoDevice, NoSmsDestination, OrganizationAccessRevoked, DeviceRevoked, ProviderUnavailable, ProviderRejected, ProviderTimeout, InvalidRegistration, ProviderAuthenticationFailed, ProviderRateLimited, ProviderConfigurationInvalid, ResourceAccessRevoked, None

<a id="contract-NullableOfOrganizationRole"></a>
### NullableOfOrganizationRole

string / Student, Teacher, Parent, Manager, None

<a id="contract-NullableOfPaymentAttemptStatus"></a>
### NullableOfPaymentAttemptStatus

string / Created, Initiating, PendingVerification, Succeeded, Failed, Unknown, None

<a id="contract-NullableOfPrivacyAudience"></a>
### NullableOfPrivacyAudience

string / Everyone, Contacts, Nobody, None

<a id="contract-NullableOfReportExportAssessmentKind"></a>
### NullableOfReportExportAssessmentKind

string / Assignment, Exam, None

<a id="contract-NullableOfSubmissionAttemptStatus"></a>
### NullableOfSubmissionAttemptStatus

string / Draft, Submitted, None

<a id="contract-OfficialClassConversationResult"></a>
### OfficialClassConversationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `activeMemberCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-OnboardingStateCommand"></a>
### OnboardingStateCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-OrderDto"></a>
### OrderDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `orderId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `planVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [OrderStatus](#contract-OrderStatus) | بله | خیر/تصریح نشده | — |
| `amount` | number / double | بله | خیر/تصریح نشده | — |
| `currency` | string | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `paidAtUtc` | string / date-time | بله | بله | — |
| `fulfillmentState` | [OrderFulfillmentState](#contract-OrderFulfillmentState) | بله | خیر/تصریح نشده | — |
| `paymentAttempts` | آرایه از [PaymentAttemptDto](#contract-PaymentAttemptDto) | بله | خیر/تصریح نشده | — |

<a id="contract-OrderFulfillmentState"></a>
### OrderFulfillmentState

CLR واقعی: `MakanApp.Application.Commerce.OrderFulfillmentState`؛ Flags: False

literalهای JSON واقعی MVC: `["NotStarted", "SubscriptionActivationPending", "SubscriptionActivated", "ProvisioningPending"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-OrderListItemDto"></a>
### OrderListItemDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `orderId` | string / uuid | بله | خیر/تصریح نشده | — |
| `planVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [OrderStatus](#contract-OrderStatus) | بله | خیر/تصریح نشده | — |
| `amount` | number / double | بله | خیر/تصریح نشده | — |
| `currency` | string | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `paidAtUtc` | string / date-time | بله | بله | — |
| `fulfillmentState` | [OrderFulfillmentState](#contract-OrderFulfillmentState) | بله | خیر/تصریح نشده | — |
| `latestPaymentStatus` | [NullableOfPaymentAttemptStatus](#contract-NullableOfPaymentAttemptStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-OrderListResult"></a>
### OrderListResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [OrderListItemDto](#contract-OrderListItemDto) | بله | خیر/تصریح نشده | — |

<a id="contract-OrderStatus"></a>
### OrderStatus

CLR واقعی: `MakanApp.Domain.Commerce.OrderStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["PendingPayment", "Paid", "Cancelled"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-OrganizationPeoplePage"></a>
### OrganizationPeoplePage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [OrganizationPersonDto](#contract-OrganizationPersonDto) | بله | خیر/تصریح نشده | — |

<a id="contract-OrganizationPersonDto"></a>
### OrganizationPersonDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `personId` | string / uuid | بله | خیر/تصریح نشده | — |
| `firstName` | string | بله | خیر/تصریح نشده | — |
| `lastName` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | بله | — |
| `status` | [OrganizationPersonStatus](#contract-OrganizationPersonStatus) | بله | خیر/تصریح نشده | — |
| `isAccountLinked` | boolean | بله | خیر/تصریح نشده | — |
| `canEditNames` | boolean | بله | خیر/تصریح نشده | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |
| `personRowVersion` | string | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-OrganizationPersonStatus"></a>
### OrganizationPersonStatus

CLR واقعی: `MakanApp.Domain.Organization.OrganizationPersonStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Ended"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-OrganizationRole"></a>
### OrganizationRole

CLR واقعی: `MakanApp.Domain.Organization.OrganizationRole`؛ Flags: False

literalهای JSON واقعی MVC: `["Student", "Teacher", "Parent", "Manager"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-OrganizationStatus"></a>
### OrganizationStatus

CLR واقعی: `MakanApp.Domain.Organization.OrganizationStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Suspended"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-OverridePlatformSubscriptionCommand"></a>
### OverridePlatformSubscriptionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `planVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `endsAtUtc` | string / date-time | بله | بله | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-OwnershipTransferResult"></a>
### OwnershipTransferResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `transferId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `fromUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `toUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [OwnershipTransferStatus](#contract-OwnershipTransferStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `acceptedAtUtc` | string / date-time | بله | بله | — |
| `declinedAtUtc` | string / date-time | بله | بله | — |
| `alreadyCompleted` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-OwnershipTransferStatus"></a>
### OwnershipTransferStatus

CLR واقعی: `MakanApp.Domain.Messaging.OwnershipTransferStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Pending", "Accepted", "Declined", "Cancelled", "Expired"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ParentAssignmentReportItem"></a>
### ParentAssignmentReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `assignmentStatus` | [AssignmentStatus](#contract-AssignmentStatus) | بله | خیر/تصریح نشده | — |
| `dueAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `submissionStatus` | [NullableOfSubmissionAttemptStatus](#contract-NullableOfSubmissionAttemptStatus) | بله | خیر/تصریح نشده | — |
| `submittedAtUtc` | string / date-time | بله | بله | — |
| `isLate` | boolean | بله | بله | — |
| `releasedScore` | number / double | بله | بله | — |
| `guardianVisibleFeedback` | string | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-ParentAttendanceReportItem"></a>
### ParentAttendanceReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `sessionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `courseTitle` | string | بله | خیر/تصریح نشده | — |
| `sessionTitle` | string | بله | خیر/تصریح نشده | — |
| `startUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `status` | [AttendanceStatus](#contract-AttendanceStatus) | بله | خیر/تصریح نشده | — |
| `recordedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-ParentDashboardDto"></a>
### ParentDashboardDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `audience` | [DashboardAudience](#contract-DashboardAudience) | بله | خیر/تصریح نشده | — |
| `selectedLearner` | [DashboardLearner](#contract-DashboardLearner) | بله | خیر/تصریح نشده | — |
| `upcomingSessions` | آرایه از [DashboardSessionItem2](#contract-DashboardSessionItem2) | بله | خیر/تصریح نشده | — |
| `assignments` | آرایه از [DashboardAssignmentItem2](#contract-DashboardAssignmentItem2) | بله | خیر/تصریح نشده | — |
| `releasedAssignmentResults` | آرایه از [ParentReleasedAssignmentItem](#contract-ParentReleasedAssignmentItem) | بله | خیر/تصریح نشده | — |
| `exams` | آرایه از [DashboardExamItem](#contract-DashboardExamItem) | بله | خیر/تصریح نشده | — |
| `attendanceSummary` | [DashboardAttendanceSummary](#contract-DashboardAttendanceSummary) | بله | خیر/تصریح نشده | — |
| `attentionItemCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-ParentExamReportItem"></a>
### ParentExamReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `availableFromUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `availableUntilUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `durationMinutes` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `attemptCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `latestAttemptStatus` | [NullableOfExamAttemptStatus](#contract-NullableOfExamAttemptStatus) | بله | خیر/تصریح نشده | — |
| `latestFinalizedAtUtc` | string / date-time | بله | بله | — |
| `releasedScore` | number / double | بله | بله | — |
| `guardianVisibleFeedback` | string | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-ParentReleasedAssignmentItem"></a>
### ParentReleasedAssignmentItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `submissionAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentTitle` | string | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `guardianVisibleFeedback` | string | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-ParentReleasedResult"></a>
### ParentReleasedResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `submissionAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `evaluationRevisionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `guardianVisibleFeedback` | string | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PaymentAttemptDto"></a>
### PaymentAttemptDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `paymentAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `provider` | string | بله | خیر/تصریح نشده | — |
| `status` | [PaymentAttemptStatus](#contract-PaymentAttemptStatus) | بله | خیر/تصریح نشده | — |
| `amount` | number / double | بله | خیر/تصریح نشده | — |
| `currency` | string | بله | خیر/تصریح نشده | — |
| `providerReference` | string | بله | بله | — |
| `paymentUrl` | string | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `completedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-PaymentAttemptStatus"></a>
### PaymentAttemptStatus

CLR واقعی: `MakanApp.Domain.Commerce.PaymentAttemptStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Created", "Initiating", "PendingVerification", "Succeeded", "Failed", "Unknown"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-PeerReadVisibility"></a>
### PeerReadVisibility

CLR واقعی: `MakanApp.Application.Messaging.PeerReadVisibility`؛ Flags: False

literalهای JSON واقعی MVC: `["Visible", "Hidden", "NotSupported"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-PeopleResolveResult"></a>
### PeopleResolveResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `isDiscoverable` | boolean | بله | خیر/تصریح نشده | — |
| `profile` | [MessagingPublicProfileResult2](#contract-MessagingPublicProfileResult2) | بله | خیر/تصریح نشده | — |

<a id="contract-PeopleSearchPageResult"></a>
### PeopleSearchPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [MessagingPublicProfileResult](#contract-MessagingPublicProfileResult) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PersonLinkInvitationDto"></a>
### PersonLinkInvitationDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `destinationUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `role` | [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `status` | [InvitationStatus](#contract-InvitationStatus) | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-PersonalMessagingAction"></a>
### PersonalMessagingAction

CLR واقعی: `MakanApp.Application.Messaging.PersonalMessagingAction`؛ Flags: False

literalهای JSON واقعی MVC: `["ReadExistingConversations", "DiscoverUsers", "CreateDirect", "CreatePersonalGroup", "CreatePersonalChannel", "DiscoverRelatedUsers", "CreateDirectWithAuthorizedRelationship", "CreatePrivatePersonalGroup"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-PlanVersionStatus"></a>
### PlanVersionStatus

CLR واقعی: `MakanApp.Domain.Commerce.PlanVersionStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Draft", "Published"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-PlatformAbuseReportEvidence"></a>
### PlatformAbuseReportEvidence

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `reportId` | string / uuid | بله | خیر/تصریح نشده | — |
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `reporterUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `reportedUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `reason` | [AbuseReportReason](#contract-AbuseReportReason) | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `messageKind` | [MessageKind](#contract-MessageKind) | بله | خیر/تصریح نشده | — |
| `contentSnapshot` | string | بله | بله | — |

<a id="contract-PlatformAdminHistoryReference"></a>
### PlatformAdminHistoryReference

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `auditId` | string / uuid | بله | خیر/تصریح نشده | — |
| `action` | string | بله | خیر/تصریح نشده | — |
| `occurredAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `result` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformAssignmentItem"></a>
### PlatformAssignmentItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organization` | [PlatformOrganizationContext](#contract-PlatformOrganizationContext) | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `status` | [AssignmentStatus](#contract-AssignmentStatus) | بله | خیر/تصریح نشده | — |
| `currentVersionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `currentTitle` | string | بله | بله | — |
| `currentMaxScore` | number / double | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `publishedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-PlatformAssignmentPage"></a>
### PlatformAssignmentPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformAssignmentItem](#contract-PlatformAssignmentItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformClassAssessmentSummary"></a>
### PlatformClassAssessmentSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `totalAssignments` | integer / int32 | بله | خیر/تصریح نشده | — |
| `publishedAssignments` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalExams` | integer / int32 | بله | خیر/تصریح نشده | — |
| `publishedExams` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformClassAttendanceSummary"></a>
### PlatformClassAttendanceSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `totalRecords` | integer / int32 | بله | خیر/تصریح نشده | — |
| `present` | integer / int32 | بله | خیر/تصریح نشده | — |
| `absent` | integer / int32 | بله | خیر/تصریح نشده | — |
| `late` | integer / int32 | بله | خیر/تصریح نشده | — |
| `excused` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformClassDetail"></a>
### PlatformClassDetail

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `class` | [PlatformClassItem](#contract-PlatformClassItem) | بله | خیر/تصریح نشده | — |
| `activeTeachers` | آرایه از [PlatformClassPerson](#contract-PlatformClassPerson) | بله | خیر/تصریح نشده | — |
| `activeLearners` | آرایه از بدون schema منتشرشده | بله | خیر/تصریح نشده | — |
| `activeTeachersTruncated` | boolean | بله | خیر/تصریح نشده | — |
| `activeLearnersTruncated` | boolean | بله | خیر/تصریح نشده | — |
| `sessions` | [PlatformClassSessionSummary](#contract-PlatformClassSessionSummary) | بله | خیر/تصریح نشده | — |
| `assessments` | [PlatformClassAssessmentSummary](#contract-PlatformClassAssessmentSummary) | بله | خیر/تصریح نشده | — |
| `attendance` | [PlatformClassAttendanceSummary](#contract-PlatformClassAttendanceSummary) | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformClassItem"></a>
### PlatformClassItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organization` | [PlatformOrganizationContext](#contract-PlatformOrganizationContext) | بله | خیر/تصریح نشده | — |
| `academicPeriodId` | string / uuid | بله | خیر/تصریح نشده | — |
| `academicPeriodTitle` | string | بله | خیر/تصریح نشده | — |
| `courseId` | string / uuid | بله | خیر/تصریح نشده | — |
| `courseTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `capacity` | integer / int32 | بله | خیر/تصریح نشده | — |
| `status` | [ClassStatus](#contract-ClassStatus) | بله | خیر/تصریح نشده | — |
| `activeTeacherCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `activeLearnerCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformClassPage"></a>
### PlatformClassPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformClassItem](#contract-PlatformClassItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformClassPerson"></a>
### PlatformClassPerson

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `referenceId` | string / uuid | بله | خیر/تصریح نشده | — |
| `personId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | بله | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformClassSessionSummary"></a>
### PlatformClassSessionSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `total` | integer / int32 | بله | خیر/تصریح نشده | — |
| `scheduled` | integer / int32 | بله | خیر/تصریح نشده | — |
| `completed` | integer / int32 | بله | خیر/تصریح نشده | — |
| `cancelled` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformClassSummary"></a>
### PlatformClassSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `activeCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCapacity` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformCompleteModerationCommand"></a>
### PlatformCompleteModerationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |
| `internalNote` | string | بله | بله | — |

<a id="contract-PlatformConversationMetadataItem"></a>
### PlatformConversationMetadataItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `type` | [ConversationType](#contract-ConversationType) | بله | خیر/تصریح نشده | — |
| `scope` | [ConversationScope](#contract-ConversationScope) | بله | خیر/تصریح نشده | — |
| `organization` | [PlatformOrganizationContext2](#contract-PlatformOrganizationContext2) | بله | خیر/تصریح نشده | — |
| `visibility` | [ConversationVisibility](#contract-ConversationVisibility) | بله | خیر/تصریح نشده | — |
| `status` | [ConversationStatus](#contract-ConversationStatus) | بله | خیر/تصریح نشده | — |
| `activeMemberCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `moderationCaseCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformConversationMetadataPage"></a>
### PlatformConversationMetadataPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformConversationMetadataItem](#contract-PlatformConversationMetadataItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformDependencyHealth"></a>
### PlatformDependencyHealth

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `name` | string | بله | خیر/تصریح نشده | — |
| `status` | string | بله | خیر/تصریح نشده | — |
| `detail` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformEnrollmentItem"></a>
### PlatformEnrollmentItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `enrollmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organization` | [PlatformOrganizationContext](#contract-PlatformOrganizationContext) | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `organizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `personId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | بله | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `status` | [EnrollmentStatus](#contract-EnrollmentStatus) | بله | خیر/تصریح نشده | — |
| `enrolledAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-PlatformEnrollmentPage"></a>
### PlatformEnrollmentPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformEnrollmentItem](#contract-PlatformEnrollmentItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformExamItem"></a>
### PlatformExamItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organization` | [PlatformOrganizationContext](#contract-PlatformOrganizationContext) | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `status` | [ExamStatus](#contract-ExamStatus) | بله | خیر/تصریح نشده | — |
| `currentVersionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `currentTitle` | string | بله | بله | — |
| `currentMaxScore` | number / double | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformExamPage"></a>
### PlatformExamPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformExamItem](#contract-PlatformExamItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformGuardianSummary"></a>
### PlatformGuardianSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `activeAsGuardian` | integer / int32 | بله | خیر/تصریح نشده | — |
| `activeAsLearner` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformInvitationResult"></a>
### PlatformInvitationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `invitationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `destinationUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `role` | [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `status` | [InvitationStatus](#contract-InvitationStatus) | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformJobOperations"></a>
### PlatformJobOperations

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `counts` | آرایه از [PlatformJobStatusCount](#contract-PlatformJobStatusCount) | بله | خیر/تصریح نشده | — |
| `oldestPendingAtUtc` | string / date-time | بله | بله | — |
| `queues` | آرایه از [PlatformJobQueueDetail](#contract-PlatformJobQueueDetail) | خیر | خیر/تصریح نشده | — |

<a id="contract-PlatformJobQueueDetail"></a>
### PlatformJobQueueDetail

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `jobType` | string | بله | خیر/تصریح نشده | — |
| `pendingCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `processingCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `failedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `oldestPendingAtUtc` | string / date-time | بله | بله | — |
| `oldestPendingAgeSeconds` | number / double | بله | بله | — |

<a id="contract-PlatformJobStatusCount"></a>
### PlatformJobStatusCount

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `jobType` | string | بله | خیر/تصریح نشده | — |
| `status` | string | بله | خیر/تصریح نشده | — |
| `count` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformManagerSummary"></a>
### PlatformManagerSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformMeResult"></a>
### PlatformMeResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `personId` | string / uuid | بله | بله | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `roles` | آرایه از string | بله | خیر/تصریح نشده | — |
| `permissions` | آرایه از string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformMembershipItem"></a>
### PlatformMembershipItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `membershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `status` | [MembershipStatus](#contract-MembershipStatus) | بله | خیر/تصریح نشده | — |
| `activeRoles` | آرایه از [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformMembershipMutationResult"></a>
### PlatformMembershipMutationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `membership` | [PlatformMembershipItem](#contract-PlatformMembershipItem) | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformMembershipPage"></a>
### PlatformMembershipPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformMembershipItem](#contract-PlatformMembershipItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformModerationActionCommand"></a>
### PlatformModerationActionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |
| `action` | [ModerationActionType](#contract-ModerationActionType) | بله | خیر/تصریح نشده | — |
| `restrictedUntilUtc` | string / date-time | خیر | بله | {"default": null} |

<a id="contract-PlatformModerationActionItem"></a>
### PlatformModerationActionItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `type` | [ModerationActionType](#contract-ModerationActionType) | بله | خیر/تصریح نشده | — |
| `actorUserId` | string / uuid | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformModerationCaseDetail"></a>
### PlatformModerationCaseDetail

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `case` | [PlatformModerationCaseItem](#contract-PlatformModerationCaseItem) | بله | خیر/تصریح نشده | — |
| `reviewStartedAtUtc` | string / date-time | بله | بله | — |
| `resolvedAtUtc` | string / date-time | بله | بله | — |
| `dismissedAtUtc` | string / date-time | بله | بله | — |
| `abuseReport` | [PlatformAbuseReportEvidence](#contract-PlatformAbuseReportEvidence) | بله | خیر/تصریح نشده | — |
| `safetyIncident` | [PlatformSafetyIncidentEvidence](#contract-PlatformSafetyIncidentEvidence) | بله | خیر/تصریح نشده | — |
| `attachments` | آرایه از [PlatformModerationEvidenceItem](#contract-PlatformModerationEvidenceItem) | بله | خیر/تصریح نشده | — |
| `actions` | آرایه از [PlatformModerationActionItem](#contract-PlatformModerationActionItem) | بله | خیر/تصریح نشده | — |
| `contextMessages` | آرایه از [PlatformModerationContextMessage](#contract-PlatformModerationContextMessage) | خیر | بله | {"default": null} |

<a id="contract-PlatformModerationCaseItem"></a>
### PlatformModerationCaseItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `caseId` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organization` | [PlatformOrganizationContext2](#contract-PlatformOrganizationContext2) | بله | خیر/تصریح نشده | — |
| `source` | [ModerationCaseSource](#contract-ModerationCaseSource) | بله | خیر/تصریح نشده | — |
| `priority` | [ModerationPriority](#contract-ModerationPriority) | بله | خیر/تصریح نشده | — |
| `status` | [ModerationCaseStatus](#contract-ModerationCaseStatus) | بله | خیر/تصریح نشده | — |
| `assignedReviewerUserId` | string / uuid | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformModerationCasePage"></a>
### PlatformModerationCasePage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformModerationCaseItem](#contract-PlatformModerationCaseItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformModerationContextMessage"></a>
### PlatformModerationContextMessage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `senderUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sequence` | integer / int64 | بله | خیر/تصریح نشده | — |
| `kind` | [MessageKind](#contract-MessageKind) | بله | خیر/تصریح نشده | — |
| `text` | string | بله | بله | — |
| `sentAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformModerationEvidenceItem"></a>
### PlatformModerationEvidenceItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileAssetId` | string / uuid | بله | خیر/تصریح نشده | — |
| `fileName` | string | بله | خیر/تصریح نشده | — |
| `contentType` | string | بله | خیر/تصریح نشده | — |
| `sizeBytes` | integer / int64 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformModerationVersionCommand"></a>
### PlatformModerationVersionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformNotificationFailureItem"></a>
### PlatformNotificationFailureItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `deliveryAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `channel` | [NotificationDeliveryChannel](#contract-NotificationDeliveryChannel) | بله | خیر/تصریح نشده | — |
| `status` | [NotificationDeliveryStatus](#contract-NotificationDeliveryStatus) | بله | خیر/تصریح نشده | — |
| `attemptCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `failureReason` | [NullableOfNotificationDeliveryFailureReason](#contract-NullableOfNotificationDeliveryFailureReason) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `lastAttemptAtUtc` | string / date-time | بله | بله | — |
| `nextAttemptAtUtc` | string / date-time | بله | بله | — |

<a id="contract-PlatformNotificationOperations"></a>
### PlatformNotificationOperations

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `pendingCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `processingCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `deferredCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `failedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `oldestQueuedAgeSeconds` | number / double | بله | بله | — |
| `pushProviderAvailability` | string | بله | خیر/تصریح نشده | — |
| `smsProviderAvailability` | string | بله | خیر/تصریح نشده | — |
| `recentFailures` | آرایه از [PlatformNotificationFailureItem](#contract-PlatformNotificationFailureItem) | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrderItem"></a>
### PlatformOrderItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `orderId` | string / uuid | بله | خیر/تصریح نشده | — |
| `purchaserUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organization` | [PlatformOrganizationContext2](#contract-PlatformOrganizationContext2) | بله | خیر/تصریح نشده | — |
| `planVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [OrderStatus](#contract-OrderStatus) | بله | خیر/تصریح نشده | — |
| `amount` | number / double | بله | خیر/تصریح نشده | — |
| `currency` | [CurrencyCode](#contract-CurrencyCode) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `paidAtUtc` | string / date-time | بله | بله | — |
| `cancelledAtUtc` | string / date-time | بله | بله | — |
| `paymentAttempts` | آرایه از [PlatformPaymentAttemptItem](#contract-PlatformPaymentAttemptItem) | بله | خیر/تصریح نشده | — |
| `payment` | [PlatformPaymentItem](#contract-PlatformPaymentItem) | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrderPage"></a>
### PlatformOrderPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformOrderItem](#contract-PlatformOrderItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `reconciliationAvailability` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrganizationActivitySummary"></a>
### PlatformOrganizationActivitySummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `lastMembershipCreatedAtUtc` | string / date-time | بله | بله | — |
| `lastClassCreatedAtUtc` | string / date-time | بله | بله | — |
| `lastAdministrativeActionAtUtc` | string / date-time | بله | بله | — |

<a id="contract-PlatformOrganizationContext"></a>
### PlatformOrganizationContext

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `name` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrganizationContext2"></a>
### PlatformOrganizationContext2

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `name` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrganizationDetail"></a>
### PlatformOrganizationDetail

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `name` | string | بله | خیر/تصریح نشده | — |
| `status` | [OrganizationStatus](#contract-OrganizationStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `managers` | آرایه از [PlatformManagerSummary](#contract-PlatformManagerSummary) | بله | خیر/تصریح نشده | — |
| `activeMemberCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `activeRoles` | آرایه از [PlatformOrganizationRoleCount](#contract-PlatformOrganizationRoleCount) | بله | خیر/تصریح نشده | — |
| `classes` | [PlatformClassSummary](#contract-PlatformClassSummary) | بله | خیر/تصریح نشده | — |
| `subscription` | [PlatformSubscriptionSummary](#contract-PlatformSubscriptionSummary) | بله | خیر/تصریح نشده | — |
| `activity` | [PlatformOrganizationActivitySummary](#contract-PlatformOrganizationActivitySummary) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrganizationImpactPreview"></a>
### PlatformOrganizationImpactPreview

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `activeMembershipCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `activeClassCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `activeSubscriptionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pendingInvitationCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pendingProvisioningCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrganizationItem"></a>
### PlatformOrganizationItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `name` | string | بله | خیر/تصریح نشده | — |
| `status` | [OrganizationStatus](#contract-OrganizationStatus) | بله | خیر/تصریح نشده | — |
| `activeMemberCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `activeClassCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `activeManagerCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `managers` | آرایه از [PlatformManagerSummary](#contract-PlatformManagerSummary) | بله | خیر/تصریح نشده | — |
| `subscription` | [PlatformSubscriptionSummary](#contract-PlatformSubscriptionSummary) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrganizationMutationResult"></a>
### PlatformOrganizationMutationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `organization` | [PlatformOrganizationDetail](#contract-PlatformOrganizationDetail) | بله | خیر/تصریح نشده | — |
| `impact` | [PlatformOrganizationImpactPreview](#contract-PlatformOrganizationImpactPreview) | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrganizationPage"></a>
### PlatformOrganizationPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformOrganizationItem](#contract-PlatformOrganizationItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrganizationRoleCount"></a>
### PlatformOrganizationRoleCount

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `role` | [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `count` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformOrganizationStateCommand"></a>
### PlatformOrganizationStateCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformPaymentAttemptItem"></a>
### PlatformPaymentAttemptItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `paymentAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `provider` | string | بله | خیر/تصریح نشده | — |
| `status` | [PaymentAttemptStatus](#contract-PaymentAttemptStatus) | بله | خیر/تصریح نشده | — |
| `amount` | number / double | بله | خیر/تصریح نشده | — |
| `currency` | [CurrencyCode](#contract-CurrencyCode) | بله | خیر/تصریح نشده | — |
| `maskedProviderReference` | string | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `verifiedAtUtc` | string / date-time | بله | بله | — |
| `completedAtUtc` | string / date-time | بله | بله | — |
| `failureCode` | string | بله | بله | — |

<a id="contract-PlatformPaymentItem"></a>
### PlatformPaymentItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `paymentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `paymentAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `provider` | string | بله | خیر/تصریح نشده | — |
| `maskedProviderReference` | string | بله | خیر/تصریح نشده | — |
| `amount` | number / double | بله | خیر/تصریح نشده | — |
| `currency` | [CurrencyCode](#contract-CurrencyCode) | بله | خیر/تصریح نشده | — |
| `paidAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformPlanDetail"></a>
### PlatformPlanDetail

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `planId` | string / uuid | بله | خیر/تصریح نشده | — |
| `code` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `versions` | آرایه از [PlatformPlanVersionItem](#contract-PlatformPlanVersionItem) | بله | خیر/تصریح نشده | — |
| `versionsTruncated` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformPlanEntitlementItem"></a>
### PlatformPlanEntitlementItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `key` | [EntitlementKey](#contract-EntitlementKey) | بله | خیر/تصریح نشده | — |
| `enabled` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformPlanItem"></a>
### PlatformPlanItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `planId` | string / uuid | بله | خیر/تصریح نشده | — |
| `code` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `versionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `latestPublishedVersion` | integer / int32 | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformPlanPage"></a>
### PlatformPlanPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformPlanItem](#contract-PlatformPlanItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformPlanQuotaItem"></a>
### PlatformPlanQuotaItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `key` | [QuotaKey](#contract-QuotaKey) | بله | خیر/تصریح نشده | — |
| `limit` | integer / int64 | بله | بله | — |

<a id="contract-PlatformPlanVersionItem"></a>
### PlatformPlanVersionItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `planVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `versionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `status` | [PlanVersionStatus](#contract-PlanVersionStatus) | بله | خیر/تصریح نشده | — |
| `priceAmount` | number / double | بله | بله | — |
| `currency` | [NullableOfCurrencyCode](#contract-NullableOfCurrencyCode) | بله | خیر/تصریح نشده | — |
| `entitlements` | آرایه از [PlatformPlanEntitlementItem](#contract-PlatformPlanEntitlementItem) | بله | خیر/تصریح نشده | — |
| `quotas` | آرایه از [PlatformPlanQuotaItem](#contract-PlatformPlanQuotaItem) | بله | خیر/تصریح نشده | — |
| `subscriptionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `publishedAtUtc` | string / date-time | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformProvisioningItem"></a>
### PlatformProvisioningItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `clientOperationId` | string | بله | خیر/تصریح نشده | — |
| `requestedByUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationName` | string | بله | خیر/تصریح نشده | — |
| `planVersionId` | string / uuid | بله | بله | — |
| `initialManagerUserId` | string / uuid | بله | بله | — |
| `status` | [PlatformProvisioningStatus](#contract-PlatformProvisioningStatus) | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `subscriptionId` | string / uuid | بله | بله | — |
| `managerInvitationId` | string / uuid | بله | بله | — |
| `failureCode` | string | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformProvisioningPage"></a>
### PlatformProvisioningPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformProvisioningItem](#contract-PlatformProvisioningItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformProvisioningResult"></a>
### PlatformProvisioningResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `request` | [PlatformProvisioningItem](#contract-PlatformProvisioningItem) | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformProvisioningStatus"></a>
### PlatformProvisioningStatus

CLR واقعی: `MakanApp.Domain.Platform.PlatformProvisioningStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Requested", "OrganizationCreated", "SubscriptionAssigned", "ManagerInvitationCreated", "Completed", "NeedsAttention"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-PlatformRoleAssignmentItem"></a>
### PlatformRoleAssignmentItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `personId` | string / uuid | بله | بله | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `roleId` | string / uuid | بله | خیر/تصریح نشده | — |
| `roleKey` | string | بله | خیر/تصریح نشده | — |
| `roleDisplayName` | string | بله | خیر/تصریح نشده | — |
| `isActive` | boolean | بله | خیر/تصریح نشده | — |
| `grantedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |
| `grantedByUserId` | string / uuid | بله | بله | — |
| `endedByUserId` | string / uuid | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformRoleAssignmentPage"></a>
### PlatformRoleAssignmentPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformRoleAssignmentItem](#contract-PlatformRoleAssignmentItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformRoleCatalogItem"></a>
### PlatformRoleCatalogItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `roleId` | string / uuid | بله | خیر/تصریح نشده | — |
| `key` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `permissions` | آرایه از string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformRoleMutationResult"></a>
### PlatformRoleMutationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `assignment` | [PlatformRoleAssignmentItem](#contract-PlatformRoleAssignmentItem) | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformSafetyIncidentEvidence"></a>
### PlatformSafetyIncidentEvidence

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `incidentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `actorUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `clientMessageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `contentKind` | [MessageKind](#contract-MessageKind) | بله | خیر/تصریح نشده | — |
| `decision` | [ContentSafetyDecision](#contract-ContentSafetyDecision) | بله | خیر/تصریح نشده | — |
| `riskLevel` | string | بله | بله | — |
| `policyCodes` | string | بله | بله | — |
| `contentSnapshot` | string | بله | بله | — |

<a id="contract-PlatformSessionRevocationResult"></a>
### PlatformSessionRevocationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sessionId` | string / uuid | بله | بله | — |
| `revokedSessionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformSubscriptionItem"></a>
### PlatformSubscriptionItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `subscriptionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organization` | [PlatformOrganizationContext](#contract-PlatformOrganizationContext) | بله | خیر/تصریح نشده | — |
| `planId` | string / uuid | بله | خیر/تصریح نشده | — |
| `planCode` | string | بله | خیر/تصریح نشده | — |
| `planVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `planVersionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `status` | [SubscriptionStatus](#contract-SubscriptionStatus) | بله | خیر/تصریح نشده | — |
| `startsAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endsAtUtc` | string / date-time | بله | بله | — |
| `usage` | آرایه از [PlatformSubscriptionUsageItem](#contract-PlatformSubscriptionUsageItem) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformSubscriptionOverrideResult"></a>
### PlatformSubscriptionOverrideResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `subscription` | [PlatformSubscriptionItem](#contract-PlatformSubscriptionItem) | بله | خیر/تصریح نشده | — |
| `replacedSubscriptionId` | string / uuid | بله | بله | — |

<a id="contract-PlatformSubscriptionPage"></a>
### PlatformSubscriptionPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformSubscriptionItem](#contract-PlatformSubscriptionItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformSubscriptionSummary"></a>
### PlatformSubscriptionSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `subscriptionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `planVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `planName` | string | بله | خیر/تصریح نشده | — |
| `status` | [SubscriptionStatus](#contract-SubscriptionStatus) | بله | خیر/تصریح نشده | — |
| `startsAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endsAtUtc` | string / date-time | بله | بله | — |

<a id="contract-PlatformSubscriptionUsageItem"></a>
### PlatformSubscriptionUsageItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `key` | [QuotaKey](#contract-QuotaKey) | بله | خیر/تصریح نشده | — |
| `limit` | integer / int64 | بله | بله | — |
| `currentUsage` | integer / int64 | بله | خیر/تصریح نشده | — |
| `isOverQuota` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformSystemHealth"></a>
### PlatformSystemHealth

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `observedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `dependencies` | آرایه از [PlatformDependencyHealth](#contract-PlatformDependencyHealth) | بله | خیر/تصریح نشده | — |
| `notifications` | [PlatformNotificationOperations](#contract-PlatformNotificationOperations) | بله | خیر/تصریح نشده | — |
| `jobs` | [PlatformJobOperations](#contract-PlatformJobOperations) | بله | خیر/تصریح نشده | — |
| `aiIntegrationStatus` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformTeacherAssignmentItem"></a>
### PlatformTeacherAssignmentItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `teacherAssignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organization` | [PlatformOrganizationContext](#contract-PlatformOrganizationContext) | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `membershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `status` | [TeacherAssignmentStatus](#contract-TeacherAssignmentStatus) | بله | خیر/تصریح نشده | — |
| `assignedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-PlatformTeacherAssignmentPage"></a>
### PlatformTeacherAssignmentPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformTeacherAssignmentItem](#contract-PlatformTeacherAssignmentItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformUserDetail"></a>
### PlatformUserDetail

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `personId` | string / uuid | بله | بله | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `username` | string | بله | بله | — |
| `maskedPhone` | string | بله | بله | — |
| `mobileVerified` | boolean | بله | خیر/تصریح نشده | — |
| `accountStatus` | [UserAccountStatus](#contract-UserAccountStatus) | بله | خیر/تصریح نشده | — |
| `suspendedAtUtc` | string / date-time | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `memberships` | آرایه از [PlatformUserMembershipSummary](#contract-PlatformUserMembershipSummary) | بله | خیر/تصریح نشده | — |
| `guardianRelations` | [PlatformGuardianSummary](#contract-PlatformGuardianSummary) | بله | خیر/تصریح نشده | — |
| `sessions` | آرایه از [PlatformUserSessionSummary](#contract-PlatformUserSessionSummary) | بله | خیر/تصریح نشده | — |
| `platformRoles` | آرایه از string | بله | خیر/تصریح نشده | — |
| `administrativeHistory` | آرایه از [PlatformAdminHistoryReference](#contract-PlatformAdminHistoryReference) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformUserItem"></a>
### PlatformUserItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `personId` | string / uuid | بله | بله | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `maskedPhone` | string | بله | بله | — |
| `accountStatus` | [UserAccountStatus](#contract-UserAccountStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `lastSessionActivityAtUtc` | string / date-time | بله | بله | — |
| `platformRoles` | آرایه از string | بله | خیر/تصریح نشده | — |
| `activeOrganizationMembershipCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformUserMembershipSummary"></a>
### PlatformUserMembershipSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `membershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationName` | string | بله | خیر/تصریح نشده | — |
| `status` | [MembershipStatus](#contract-MembershipStatus) | بله | خیر/تصریح نشده | — |
| `activeRoles` | آرایه از [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformUserMutationResult"></a>
### PlatformUserMutationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `accountStatus` | [UserAccountStatus](#contract-UserAccountStatus) | بله | خیر/تصریح نشده | — |
| `suspendedAtUtc` | string / date-time | بله | بله | — |
| `revokedSessionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformUserPage"></a>
### PlatformUserPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PlatformUserItem](#contract-PlatformUserItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformUserSessionSummary"></a>
### PlatformUserSessionSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `sessionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `revokedAtUtc` | string / date-time | بله | بله | — |
| `isActive` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-PlatformUserStateCommand"></a>
### PlatformUserStateCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PresenceResult"></a>
### PresenceResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `state` | [MessagingPresenceState](#contract-MessagingPresenceState) | بله | خیر/تصریح نشده | — |
| `lastSeenAtUtc` | string / date-time | بله | بله | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-PrivacyAudience"></a>
### PrivacyAudience

CLR واقعی: `MakanApp.Domain.Messaging.PrivacyAudience`؛ Flags: False

literalهای JSON واقعی MVC: `["Everyone", "Contacts", "Nobody"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-PrivacyAudience2"></a>
### PrivacyAudience2

string / Everyone, Contacts, Nobody

<a id="contract-ProblemDetails"></a>
### ProblemDetails

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `type` | string | خیر | بله | — |
| `title` | string | خیر | بله | — |
| `status` | integer / int32 | خیر | بله | — |
| `detail` | string | خیر | بله | — |
| `instance` | string | خیر | بله | — |

<a id="contract-ProfilePhotoResult"></a>
### ProfilePhotoResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileAssetId` | string / uuid | بله | خیر/تصریح نشده | — |
| `contentType` | string | بله | خیر/تصریح نشده | — |
| `downloadUrl` | string | خیر | بله | — |

<a id="contract-ProfilePhotoResult2"></a>
### ProfilePhotoResult2

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileAssetId` | string / uuid | بله | خیر/تصریح نشده | — |
| `contentType` | string | بله | خیر/تصریح نشده | — |
| `downloadUrl` | string | خیر | بله | — |

<a id="contract-ProfilePhotoResult3"></a>
### ProfilePhotoResult3

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileAssetId` | string / uuid | بله | خیر/تصریح نشده | — |
| `contentType` | string | بله | خیر/تصریح نشده | — |
| `downloadUrl` | string | خیر | بله | — |

<a id="contract-ProvisionOrganizationCommand"></a>
### ProvisionOrganizationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string | بله | خیر/تصریح نشده | — |
| `organizationName` | string | بله | خیر/تصریح نشده | — |
| `planVersionId` | string / uuid | بله | بله | — |
| `initialManagerUserId` | string / uuid | بله | بله | — |
| `subscriptionEndsAtUtc` | string / date-time | بله | بله | — |
| `invitationExpiresAtUtc` | string / date-time | بله | بله | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PublishAssignmentCommand"></a>
### PublishAssignmentCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedAssignmentRowVersion` | string | بله | خیر/تصریح نشده | — |
| `expectedVersionRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PublishAssignmentResult"></a>
### PublishAssignmentResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `assignment` | [AssignmentResult](#contract-AssignmentResult) | بله | خیر/تصریح نشده | — |
| `recipientCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PublishClassLessonCommand"></a>
### PublishClassLessonCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PublishExamCommand"></a>
### PublishExamCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedExamRowVersion` | string | بله | خیر/تصریح نشده | — |
| `expectedVersionRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PublishPlatformPlanVersionCommand"></a>
### PublishPlatformPlanVersionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PushDeviceListItem"></a>
### PushDeviceListItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `platform` | [PushDevicePlatform](#contract-PushDevicePlatform) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `lastSeenAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `revokedAtUtc` | string / date-time | بله | بله | — |
| `isActive` | boolean | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-PushDevicePageResult"></a>
### PushDevicePageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [PushDeviceListItem](#contract-PushDeviceListItem) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-PushDevicePlatform"></a>
### PushDevicePlatform

CLR واقعی: `MakanApp.Domain.Notification.PushDevicePlatform`؛ Flags: False

literalهای JSON واقعی MVC: `["Android", "Ios", "Web"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-PushDeviceRegistrationResult"></a>
### PushDeviceRegistrationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `platform` | [PushDevicePlatform](#contract-PushDevicePlatform) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `lastSeenAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | خیر | بله | {"default": null} |

<a id="contract-QuotaKey"></a>
### QuotaKey

CLR واقعی: `MakanApp.Domain.Commerce.QuotaKey`؛ Flags: False

literalهای JSON واقعی MVC: `["ActiveLearners", "ActiveClasses", "StorageBytes", "ReportExports"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-QuotaUsageSource"></a>
### QuotaUsageSource

CLR واقعی: `MakanApp.Application.Commerce.QuotaUsageSource`؛ Flags: False

literalهای JSON واقعی MVC: `["AuthoritativeModule", "UsageLedger"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-RecentContactPageResult"></a>
### RecentContactPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [RecentContactResult](#contract-RecentContactResult) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-RecentContactResult"></a>
### RecentContactResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `profile` | [MessagingPublicProfileResult](#contract-MessagingPublicProfileResult) | بله | خیر/تصریح نشده | — |
| `lastActivityAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-RecordAttendanceCommand"></a>
### RecordAttendanceCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `entries` | آرایه از [RecordAttendanceEntryCommand](#contract-RecordAttendanceEntryCommand) | بله | خیر/تصریح نشده | — |

<a id="contract-RecordAttendanceEntryCommand"></a>
### RecordAttendanceEntryCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `enrollmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [AttendanceStatus](#contract-AttendanceStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-RedeemInviteLinkCommand"></a>
### RedeemInviteLinkCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `token` | string | بله | خیر/تصریح نشده | — |

<a id="contract-RefreshSessionCommand"></a>
### RefreshSessionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `refreshToken` | string | بله | خیر/تصریح نشده | — |

<a id="contract-RefreshSessionResult"></a>
### RefreshSessionResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `accessToken` | string | بله | خیر/تصریح نشده | — |
| `tokenType` | string | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `refreshToken` | string | بله | خیر/تصریح نشده | — |
| `refreshExpiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-RegisterPushDeviceCommand"></a>
### RegisterPushDeviceCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `platform` | [PushDevicePlatform](#contract-PushDevicePlatform) | بله | خیر/تصریح نشده | — |
| `pushToken` | string | بله | خیر/تصریح نشده | — |
| `p256dh` | string | خیر | بله | {"default": null} |
| `auth` | string | خیر | بله | {"default": null} |
| `expectedVersion` | string | خیر | بله | {"default": null} |

<a id="contract-RegisterWebPushCommand"></a>
### RegisterWebPushCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `endpoint` | string | بله | خیر/تصریح نشده | — |
| `p256dh` | string | بله | خیر/تصریح نشده | — |
| `auth` | string | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | خیر | بله | {"default": null} |

<a id="contract-ReleaseEvaluationCommand"></a>
### ReleaseEvaluationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ReleaseExamGradeCommand"></a>
### ReleaseExamGradeCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `expectedGradeRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ReplaceSavedLabelsCommand"></a>
### ReplaceSavedLabelsCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `labels` | آرایه از string | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | بله | — |

<a id="contract-ReportExportFilters"></a>
### ReportExportFilters

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fromUtc` | string / date-time | خیر | بله | {"default": null} |
| `toUtc` | string / date-time | خیر | بله | {"default": null} |
| `classId` | string / uuid | خیر | بله | {"default": null} |
| `sort` | string | خیر | بله | {"default": null} |
| `courseId` | string / uuid | خیر | بله | {"default": null} |
| `academicPeriodId` | string / uuid | خیر | بله | {"default": null} |
| `enrollmentStatus` | [NullableOfEnrollmentStatus](#contract-NullableOfEnrollmentStatus) | خیر | خیر/تصریح نشده | — |
| `assessmentKind` | [NullableOfReportExportAssessmentKind](#contract-NullableOfReportExportAssessmentKind) | خیر | خیر/تصریح نشده | — |

<a id="contract-ReportExportFormat"></a>
### ReportExportFormat

CLR واقعی: `MakanApp.Domain.Reports.ReportExportFormat`؛ Flags: False

literalهای JSON واقعی MVC: `["Csv"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ReportExportJobResult"></a>
### ReportExportJobResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `jobId` | string / uuid | بله | خیر/تصریح نشده | — |
| `reportType` | [ReportExportType](#contract-ReportExportType) | بله | خیر/تصریح نشده | — |
| `format` | [ReportExportFormat](#contract-ReportExportFormat) | بله | خیر/تصریح نشده | — |
| `status` | [ReportExportJobStatus](#contract-ReportExportJobStatus) | بله | خیر/تصریح نشده | — |
| `expectedRowCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `requestedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `startedAtUtc` | string / date-time | بله | بله | — |
| `completedAtUtc` | string / date-time | بله | بله | — |
| `failedAtUtc` | string / date-time | بله | بله | — |
| `downloadAvailable` | boolean | بله | خیر/تصریح نشده | — |
| `failureCode` | string | بله | بله | — |

<a id="contract-ReportExportJobStatus"></a>
### ReportExportJobStatus

CLR واقعی: `MakanApp.Domain.Reports.ReportExportJobStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Queued", "Processing", "Completed", "Failed"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ReportExportType"></a>
### ReportExportType

CLR واقعی: `MakanApp.Domain.Reports.ReportExportType`؛ Flags: False

literalهای JSON واقعی MVC: `["StudentAttendance", "StudentAssignments", "StudentExams", "StudentGrades", "ParentChildAttendance", "ParentChildAssignments", "ParentChildExams", "TeacherClassAttendance", "TeacherAssignmentGrading", "TeacherExamGrading", "ManagerEnrollments", "ManagerAttendance", "ManagerAssessments", "ManagerTeacherWorkload"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ReportMessageCommand"></a>
### ReportMessageCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientReportId` | string / uuid | بله | خیر/تصریح نشده | — |
| `reason` | [AbuseReportReason](#contract-AbuseReportReason) | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |

<a id="contract-ReportPageOfManagerAssessmentReportItem"></a>
### ReportPageOfManagerAssessmentReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ManagerAssessmentReportItem](#contract-ManagerAssessmentReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-ReportPageOfManagerAttendanceReportItem"></a>
### ReportPageOfManagerAttendanceReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ManagerAttendanceReportItem](#contract-ManagerAttendanceReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-ReportPageOfManagerEnrollmentReportItem"></a>
### ReportPageOfManagerEnrollmentReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ManagerEnrollmentReportItem](#contract-ManagerEnrollmentReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-ReportPageOfManagerTeacherWorkloadReportItem"></a>
### ReportPageOfManagerTeacherWorkloadReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ManagerTeacherWorkloadReportItem](#contract-ManagerTeacherWorkloadReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-ReportPageOfParentAssignmentReportItem"></a>
### ReportPageOfParentAssignmentReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ParentAssignmentReportItem](#contract-ParentAssignmentReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-ReportPageOfParentExamReportItem"></a>
### ReportPageOfParentExamReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ParentExamReportItem](#contract-ParentExamReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-ReportPageOfStudentAssignmentReportItem"></a>
### ReportPageOfStudentAssignmentReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [StudentAssignmentReportItem](#contract-StudentAssignmentReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-ReportPageOfStudentExamReportItem"></a>
### ReportPageOfStudentExamReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [StudentExamReportItem](#contract-StudentExamReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-ReportPageOfStudentReleasedGradeReportItem"></a>
### ReportPageOfStudentReleasedGradeReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [StudentReleasedGradeReportItem](#contract-StudentReleasedGradeReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-ReportPageOfTeacherAssignmentReportItem"></a>
### ReportPageOfTeacherAssignmentReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [TeacherAssignmentReportItem](#contract-TeacherAssignmentReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-ReportPageOfTeacherExamReportItem"></a>
### ReportPageOfTeacherExamReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `pageNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [TeacherExamReportItem](#contract-TeacherExamReportItem) | بله | خیر/تصریح نشده | — |

<a id="contract-RequestOtpCommand"></a>
### RequestOtpCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `phoneNumber` | string | بله | خیر/تصریح نشده | — |

<a id="contract-RequestOtpResult"></a>
### RequestOtpResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `challengeId` | string / uuid | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `resendAvailableAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-ResolvePersonCommand"></a>
### ResolvePersonCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `username` | string | بله | بله | — |
| `phoneNumber` | string | بله | بله | — |

<a id="contract-RevokeInviteLinkCommand"></a>
### RevokeInviteLinkCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-RevokePlatformSessionsCommand"></a>
### RevokePlatformSessionsCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-RoleAssignmentStatus"></a>
### RoleAssignmentStatus

CLR واقعی: `MakanApp.Domain.Organization.RoleAssignmentStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Ended"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-SafeMessagingIdentityResult"></a>
### SafeMessagingIdentityResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `username` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `profilePhoto` | [ProfilePhotoResult](#contract-ProfilePhotoResult) | خیر | خیر/تصریح نشده | — |

<a id="contract-SafeMessagingIdentityResult2"></a>
### SafeMessagingIdentityResult2

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `username` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `profilePhoto` | [ProfilePhotoResult](#contract-ProfilePhotoResult) | خیر | خیر/تصریح نشده | — |

<a id="contract-SaveChatFolderCommand"></a>
### SaveChatFolderCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `name` | string | بله | خیر/تصریح نشده | — |
| `sortOrder` | integer / int32 | بله | خیر/تصریح نشده | — |
| `includedTypes` | [FolderConversationTypes](#contract-FolderConversationTypes) | بله | خیر/تصریح نشده | — |
| `excludedTypes` | [FolderConversationTypes](#contract-FolderConversationTypes) | بله | خیر/تصریح نشده | — |
| `unreadOnly` | boolean | بله | خیر/تصریح نشده | — |
| `overrides` | آرایه از [FolderOverrideCommand](#contract-FolderOverrideCommand) | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | بله | — |

<a id="contract-SaveEvaluationDraftCommand"></a>
### SaveEvaluationDraftCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `learnerFeedback` | string | بله | بله | — |
| `guardianVisibleFeedback` | string | بله | بله | — |
| `teacherPrivateNote` | string | بله | بله | — |
| `expectedRowVersion` | string | بله | بله | — |

<a id="contract-SaveExamAnswerCommand"></a>
### SaveExamAnswerCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `writeLeaseVersion` | integer / int64 | بله | خیر/تصریح نشده | — |
| `expectedRevisionNumber` | integer / int32 | بله | بله | — |
| `selectedOptionId` | string / uuid | بله | بله | — |
| `textAnswer` | string | بله | بله | — |

<a id="contract-SaveSubmissionDraftCommand"></a>
### SaveSubmissionDraftCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `answerText` | string | بله | بله | — |
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-SaveUserContactCommand"></a>
### SaveUserContactCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `contactUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `savedName` | string | بله | بله | — |

<a id="contract-SavedConversationResult"></a>
### SavedConversationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `type` | [ConversationType](#contract-ConversationType) | بله | خیر/تصریح نشده | — |
| `scope` | [ConversationScope](#contract-ConversationScope) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `alreadyExisted` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-SavedMessageLabelsResult"></a>
### SavedMessageLabelsResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `messageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `labels` | آرایه از string | بله | خیر/تصریح نشده | — |
| `version` | string | بله | بله | — |

<a id="contract-ScheduleMessageCommand"></a>
### ScheduleMessageCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `scheduledAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `message` | [SendMessageCommand](#contract-SendMessageCommand) | بله | خیر/تصریح نشده | — |

<a id="contract-ScheduleRuleResult"></a>
### ScheduleRuleResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `localDayOfWeek` | [DayOfWeek](#contract-DayOfWeek) | بله | خیر/تصریح نشده | — |
| `localStartTime` | string / time | بله | خیر/تصریح نشده | — |
| `durationMinutes` | integer / int32 | بله | خیر/تصریح نشده | — |
| `timeZoneId` | string | بله | خیر/تصریح نشده | — |
| `effectiveFrom` | string / date | بله | خیر/تصریح نشده | — |
| `effectiveUntil` | string / date | بله | خیر/تصریح نشده | — |
| `sessionTitle` | string | بله | خیر/تصریح نشده | — |
| `meetingUrl` | string | بله | بله | — |
| `status` | [ScheduleRuleStatus](#contract-ScheduleRuleStatus) | بله | خیر/تصریح نشده | — |
| `generatedSessionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-ScheduleRuleStatus"></a>
### ScheduleRuleStatus

CLR واقعی: `MakanApp.Domain.Academic.ScheduleRuleStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Ended"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-ScheduledMessageContent"></a>
### ScheduledMessageContent

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `kind` | [MessageKind](#contract-MessageKind) | بله | خیر/تصریح نشده | — |
| `text` | string | بله | بله | — |
| `attachmentIds` | آرایه از string / uuid | بله | خیر/تصریح نشده | — |
| `replyToMessageId` | string / uuid | بله | بله | — |
| `mentionedUserIds` | آرایه از string / uuid | بله | خیر/تصریح نشده | — |
| `contactUserId` | string / uuid | بله | بله | — |
| `topicId` | string / uuid | بله | بله | — |
| `isSilent` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ScheduledMessagePageResult"></a>
### ScheduledMessagePageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [ScheduledMessageResult](#contract-ScheduledMessageResult) | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-ScheduledMessageResult"></a>
### ScheduledMessageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `clientMessageId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [ScheduledMessageStatus](#contract-ScheduledMessageStatus) | بله | خیر/تصریح نشده | — |
| `scheduledAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `completedAtUtc` | string / date-time | بله | بله | — |
| `dispatchedMessageId` | string / uuid | بله | بله | — |
| `lastFailureCode` | string | بله | بله | — |
| `content` | [ScheduledMessageContent](#contract-ScheduledMessageContent) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |
| `alreadyExisted` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-ScheduledMessageStatus"></a>
### ScheduledMessageStatus

CLR واقعی: `MakanApp.Domain.Messaging.ScheduledMessageStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Scheduled", "Dispatched", "Cancelled", "Failed"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-SchoolGuardianInvitationResult"></a>
### SchoolGuardianInvitationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [InvitationStatus](#contract-InvitationStatus) | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | بله | خیر/تصریح نشده | — |
| `nextAction` | string | بله | خیر/تصریح نشده | — |

<a id="contract-SchoolGuardianRelationDto"></a>
### SchoolGuardianRelationDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `guardianUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [GuardianRelationStatus](#contract-GuardianRelationStatus) | بله | خیر/تصریح نشده | — |
| `validFromUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |
| `replayed` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-SchoolGuardianRelationsPage"></a>
### SchoolGuardianRelationsPage

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [SchoolGuardianRelationDto](#contract-SchoolGuardianRelationDto) | بله | خیر/تصریح نشده | — |

<a id="contract-SchoolMemberResult"></a>
### SchoolMemberResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `userId` | string / uuid | بله | خیر/تصریح نشده | — |
| `personId` | string / uuid | بله | بله | — |
| `organizationPersonId` | string / uuid | بله | بله | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `username` | string | بله | بله | — |
| `profilePhoto` | [ProfilePhotoResult2](#contract-ProfilePhotoResult2) | بله | خیر/تصریح نشده | — |
| `membershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `membershipStatus` | [MembershipStatus](#contract-MembershipStatus) | بله | خیر/تصریح نشده | — |
| `activeRoles` | آرایه از [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `roleAssignments` | آرایه از [SchoolRoleAssignmentResult](#contract-SchoolRoleAssignmentResult) | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-SchoolPageOfAcademicPeriodResult"></a>
### SchoolPageOfAcademicPeriodResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [AcademicPeriodResult](#contract-AcademicPeriodResult) | بله | خیر/تصریح نشده | — |

<a id="contract-SchoolPageOfClassLearnerResult"></a>
### SchoolPageOfClassLearnerResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ClassLearnerResult](#contract-ClassLearnerResult) | بله | خیر/تصریح نشده | — |

<a id="contract-SchoolPageOfClassLessonResult"></a>
### SchoolPageOfClassLessonResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ClassLessonResult](#contract-ClassLessonResult) | بله | خیر/تصریح نشده | — |

<a id="contract-SchoolPageOfClassTeacherResult"></a>
### SchoolPageOfClassTeacherResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ClassTeacherResult](#contract-ClassTeacherResult) | بله | خیر/تصریح نشده | — |

<a id="contract-SchoolPageOfCourseResult"></a>
### SchoolPageOfCourseResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [CourseResult](#contract-CourseResult) | بله | خیر/تصریح نشده | — |

<a id="contract-SchoolPageOfSchoolMemberResult"></a>
### SchoolPageOfSchoolMemberResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `totalCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [SchoolMemberResult](#contract-SchoolMemberResult) | بله | خیر/تصریح نشده | — |

<a id="contract-SchoolRoleAssignmentResult"></a>
### SchoolRoleAssignmentResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `roleAssignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `role` | [OrganizationRole](#contract-OrganizationRole) | بله | خیر/تصریح نشده | — |
| `status` | [RoleAssignmentStatus](#contract-RoleAssignmentStatus) | بله | خیر/تصریح نشده | — |
| `assignedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-SelectWorkspaceCommand"></a>
### SelectWorkspaceCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `workspaceType` | [WorkspaceType](#contract-WorkspaceType) | بله | خیر/تصریح نشده | — |
| `membershipId` | string / uuid | بله | بله | — |
| `role` | [NullableOfOrganizationRole](#contract-NullableOfOrganizationRole) | بله | خیر/تصریح نشده | — |

<a id="contract-SendAiChatCommand"></a>
### SendAiChatCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `message` | string | بله | خیر/تصریح نشده | — |
| `conversationId` | string / uuid | بله | بله | — |

<a id="contract-SendMessageCommand"></a>
### SendMessageCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientMessageId` | string / uuid | خیر | خیر/تصریح نشده | — |
| `kind` | [MessageKind](#contract-MessageKind) | خیر | خیر/تصریح نشده | — |
| `text` | string | خیر | بله | — |
| `attachmentIds` | آرایه از string / uuid | خیر | بله | — |
| `replyToMessageId` | string / uuid | خیر | بله | — |
| `mentionedUserIds` | آرایه از string / uuid | خیر | بله | — |
| `contactUserId` | string / uuid | خیر | بله | — |
| `topicId` | string / uuid | خیر | بله | — |
| `isSilent` | boolean | خیر | خیر/تصریح نشده | — |

<a id="contract-SessionAttendanceResult"></a>
### SessionAttendanceResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `sessionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sessionStatus` | [SessionStatus](#contract-SessionStatus) | بله | خیر/تصریح نشده | — |
| `entries` | آرایه از [AttendanceEntryResult](#contract-AttendanceEntryResult) | بله | خیر/تصریح نشده | — |
| `revisions` | آرایه از [AttendanceRevisionResult](#contract-AttendanceRevisionResult) | بله | خیر/تصریح نشده | — |

<a id="contract-SessionResult"></a>
### SessionResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `scheduleRuleId` | string / uuid | بله | بله | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `startUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `timeZoneId` | string | بله | خیر/تصریح نشده | — |
| `meetingUrl` | string | بله | بله | — |
| `status` | [SessionStatus](#contract-SessionStatus) | بله | خیر/تصریح نشده | — |
| `cancelledAtUtc` | string / date-time | بله | بله | — |
| `completedAtUtc` | string / date-time | بله | بله | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-SessionStatus"></a>
### SessionStatus

CLR واقعی: `MakanApp.Domain.Academic.SessionStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Scheduled", "Cancelled", "Completed"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-SessionVersionCommand"></a>
### SessionVersionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-SetChecklistItemCommand"></a>
### SetChecklistItemCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `isChecked` | boolean | بله | بله | — |
| `expectedDefinitionVersion` | string | بله | خیر/تصریح نشده | — |
| `expectedStateVersion` | string | خیر | بله | {"default": null} |

<a id="contract-SetConversationMuteCommand"></a>
### SetConversationMuteCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `mode` | [ConversationMuteMode](#contract-ConversationMuteMode) | بله | خیر/تصریح نشده | — |
| `mutedUntilUtc` | string / date-time | بله | بله | — |

<a id="contract-SetMemberRestrictionCommand"></a>
### SetMemberRestrictionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `isBanned` | boolean | بله | خیر/تصریح نشده | — |
| `deniedCapabilities` | [GroupMemberCapability](#contract-GroupMemberCapability) | بله | خیر/تصریح نشده | — |
| `untilUtc` | string / date-time | بله | بله | — |
| `reason` | string | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | بله | — |

<a id="contract-SetProfilePhotoCommand"></a>
### SetProfilePhotoCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileAssetId` | string / uuid | بله | خیر/تصریح نشده | — |

<a id="contract-StartDirectConversationCommand"></a>
### StartDirectConversationCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `targetUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `scope` | [ConversationScope](#contract-ConversationScope) | بله | خیر/تصریح نشده | — |

<a id="contract-StartExamCommand"></a>
### StartExamCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `clientOperationId` | string / uuid | بله | خیر/تصریح نشده | — |

<a id="contract-StartModerationReviewCommand"></a>
### StartModerationReviewCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-StartOwnershipTransferCommand"></a>
### StartOwnershipTransferCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `targetUserId` | string / uuid | بله | خیر/تصریح نشده | — |

<a id="contract-Stream"></a>
### Stream

string / binary

<a id="contract-StringSegment"></a>
### StringSegment

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `buffer` | string | خیر | بله | — |
| `offset` | integer / int32 | خیر | خیر/تصریح نشده | — |
| `length` | integer / int32 | خیر | خیر/تصریح نشده | — |
| `value` | string | خیر | بله | — |
| `hasValue` | boolean | خیر | خیر/تصریح نشده | — |

<a id="contract-StudentAssignmentActionStatus"></a>
### StudentAssignmentActionStatus

CLR واقعی: `MakanApp.Application.Reports.Dashboards.StudentAssignmentActionStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["NotStarted", "Draft", "Submitted", "SubmittedLate", "Graded"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-StudentAssignmentReportItem"></a>
### StudentAssignmentReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `assignmentStatus` | [AssignmentStatus](#contract-AssignmentStatus) | بله | خیر/تصریح نشده | — |
| `dueAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `submissionStatus` | [NullableOfSubmissionAttemptStatus](#contract-NullableOfSubmissionAttemptStatus) | بله | خیر/تصریح نشده | — |
| `submittedAtUtc` | string / date-time | بله | بله | — |
| `isLate` | boolean | بله | بله | — |
| `releasedScore` | number / double | بله | بله | — |
| `learnerFeedback` | string | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-StudentAttendanceReportItem"></a>
### StudentAttendanceReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `sessionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `courseTitle` | string | بله | خیر/تصریح نشده | — |
| `sessionTitle` | string | بله | خیر/تصریح نشده | — |
| `startUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `status` | [AttendanceStatus](#contract-AttendanceStatus) | بله | خیر/تصریح نشده | — |
| `recordedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-StudentCurrentExamAnswerDto"></a>
### StudentCurrentExamAnswerDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptQuestionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `questionVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `answerRevisionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `revisionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `answerType` | [ExamQuestionType](#contract-ExamQuestionType) | بله | خیر/تصریح نشده | — |
| `selectedOptionId` | string / uuid | بله | بله | — |
| `textAnswer` | string | بله | بله | — |
| `acceptedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-StudentDashboardDto"></a>
### StudentDashboardDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `audience` | [DashboardAudience](#contract-DashboardAudience) | بله | خیر/تصریح نشده | — |
| `nextSession` | [DashboardSessionItem](#contract-DashboardSessionItem) | بله | خیر/تصریح نشده | — |
| `nearestAssignmentDeadline` | [DashboardAssignmentItem](#contract-DashboardAssignmentItem) | بله | خیر/تصریح نشده | — |
| `recentReleasedFeedback` | آرایه از [StudentReleasedAssignmentItem](#contract-StudentReleasedAssignmentItem) | بله | خیر/تصریح نشده | — |
| `upcomingExams` | آرایه از [DashboardExamItem](#contract-DashboardExamItem) | بله | خیر/تصریح نشده | — |
| `attendanceSummary` | [DashboardAttendanceSummary](#contract-DashboardAttendanceSummary) | بله | خیر/تصریح نشده | — |
| `pendingAssignmentCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-StudentExamAnswersDto"></a>
### StudentExamAnswersDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `answerSetVersion` | integer / int64 | بله | خیر/تصریح نشده | — |
| `writeLease` | [ExamWriteLeaseDto](#contract-ExamWriteLeaseDto) | بله | خیر/تصریح نشده | — |
| `answers` | آرایه از [StudentCurrentExamAnswerDto](#contract-StudentCurrentExamAnswerDto) | بله | خیر/تصریح نشده | — |

<a id="contract-StudentExamAttemptDto"></a>
### StudentExamAttemptDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `startedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `effectiveDeadlineUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `serverNowUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `status` | [ExamAttemptStatus](#contract-ExamAttemptStatus) | بله | خیر/تصریح نشده | — |
| `questions` | آرایه از [StudentExamAttemptQuestionDto](#contract-StudentExamAttemptQuestionDto) | بله | خیر/تصریح نشده | — |

<a id="contract-StudentExamAttemptOptionDto"></a>
### StudentExamAttemptOptionDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `order` | integer / int32 | بله | خیر/تصریح نشده | — |
| `text` | string | بله | خیر/تصریح نشده | — |

<a id="contract-StudentExamAttemptQuestionDto"></a>
### StudentExamAttemptQuestionDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `attemptQuestionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `questionVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `displayOrder` | integer / int32 | بله | خیر/تصریح نشده | — |
| `questionType` | [ExamQuestionType](#contract-ExamQuestionType) | بله | خیر/تصریح نشده | — |
| `prompt` | string | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `options` | آرایه از [StudentExamAttemptOptionDto](#contract-StudentExamAttemptOptionDto) | بله | خیر/تصریح نشده | — |

<a id="contract-StudentExamAttemptSummaryDto"></a>
### StudentExamAttemptSummaryDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `startedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `effectiveDeadlineUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `serverNowUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `status` | [ExamAttemptStatus](#contract-ExamAttemptStatus) | بله | خیر/تصریح نشده | — |

<a id="contract-StudentExamReportItem"></a>
### StudentExamReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `availableFromUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `availableUntilUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `durationMinutes` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maximumAttempts` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `attemptCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `latestAttemptStatus` | [NullableOfExamAttemptStatus](#contract-NullableOfExamAttemptStatus) | بله | خیر/تصریح نشده | — |
| `latestStartedAtUtc` | string / date-time | بله | بله | — |
| `latestFinalizedAtUtc` | string / date-time | بله | بله | — |
| `releasedScore` | number / double | بله | بله | — |
| `learnerFeedback` | string | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-StudentExamResultDto"></a>
### StudentExamResultDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `finalizedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `releaseStatus` | [ExamResultReleaseStatus](#contract-ExamResultReleaseStatus) | بله | خیر/تصریح نشده | — |
| `gradeRevisionNumber` | integer / int32 | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | بله | — |
| `score` | number / double | بله | بله | — |
| `maximumScore` | number / double | بله | بله | — |
| `learnerFeedback` | string | بله | بله | — |

<a id="contract-StudentExamRules"></a>
### StudentExamRules

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `availableFromUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `availableUntilUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `durationMinutes` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxAttempts` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `randomizationPolicy` | [ExamRandomizationPolicy](#contract-ExamRandomizationPolicy) | بله | خیر/تصریح نشده | — |

<a id="contract-StudentExamSummary"></a>
### StudentExamSummary

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `versionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `versionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `publicationState` | [ExamVersionStatus](#contract-ExamVersionStatus) | بله | خیر/تصریح نشده | — |
| `rules` | [StudentExamRules](#contract-StudentExamRules) | بله | خیر/تصریح نشده | — |

<a id="contract-StudentReleasedAssignmentItem"></a>
### StudentReleasedAssignmentItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `submissionAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentTitle` | string | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `learnerFeedback` | string | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-StudentReleasedGradeReportItem"></a>
### StudentReleasedGradeReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `kind` | [DetailedReportGradeKind](#contract-DetailedReportGradeKind) | بله | خیر/تصریح نشده | — |
| `assessmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `percentage` | number / double | بله | خیر/تصریح نشده | — |
| `releasedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-StudentReleasedResult"></a>
### StudentReleasedResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `submissionAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `evaluationRevisionNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `learnerFeedback` | string | بله | بله | — |
| `releasedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-StudentSafeExamOption"></a>
### StudentSafeExamOption

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `order` | integer / int32 | بله | خیر/تصریح نشده | — |
| `text` | string | بله | خیر/تصریح نشده | — |

<a id="contract-StudentSafeExamPreview"></a>
### StudentSafeExamPreview

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `exam` | [StudentExamSummary](#contract-StudentExamSummary) | بله | خیر/تصریح نشده | — |
| `questions` | آرایه از [StudentSafeExamQuestion](#contract-StudentSafeExamQuestion) | بله | خیر/تصریح نشده | — |

<a id="contract-StudentSafeExamQuestion"></a>
### StudentSafeExamQuestion

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `order` | integer / int32 | بله | خیر/تصریح نشده | — |
| `type` | [ExamQuestionType](#contract-ExamQuestionType) | بله | خیر/تصریح نشده | — |
| `prompt` | string | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `options` | آرایه از [StudentSafeExamOption](#contract-StudentSafeExamOption) | بله | خیر/تصریح نشده | — |

<a id="contract-SubmissionAttachmentResult"></a>
### SubmissionAttachmentResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `fileAssetId` | string / uuid | بله | خیر/تصریح نشده | — |
| `originalFileName` | string | بله | خیر/تصریح نشده | — |
| `contentType` | string | بله | خیر/تصریح نشده | — |
| `sizeBytes` | integer / int64 | بله | خیر/تصریح نشده | — |
| `fileStatus` | [FileAssetStatus](#contract-FileAssetStatus) | بله | خیر/تصریح نشده | — |
| `attachedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |

<a id="contract-SubmissionAttemptResult"></a>
### SubmissionAttemptResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentRecipientId` | string / uuid | بله | خیر/تصریح نشده | — |
| `enrollmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `status` | [SubmissionAttemptStatus](#contract-SubmissionAttemptStatus) | بله | خیر/تصریح نشده | — |
| `contentVisible` | boolean | بله | خیر/تصریح نشده | — |
| `answerText` | string | بله | بله | — |
| `attachments` | آرایه از [SubmissionAttachmentResult](#contract-SubmissionAttachmentResult) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `lastSavedAtUtc` | string / date-time | بله | بله | — |
| `submittedAtUtc` | string / date-time | بله | بله | — |
| `isLate` | boolean | بله | خیر/تصریح نشده | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-SubmissionAttemptStatus"></a>
### SubmissionAttemptStatus

CLR واقعی: `MakanApp.Domain.Assessment.SubmissionAttemptStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Draft", "Submitted"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-SubmissionForEvaluationResult"></a>
### SubmissionForEvaluationResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `submissionAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentTitle` | string | بله | خیر/تصریح نشده | — |
| `studentDisplayName` | string | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `submittedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `isLate` | boolean | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `answerText` | string | بله | بله | — |
| `attachments` | آرایه از [SubmissionAttachmentResult](#contract-SubmissionAttachmentResult) | بله | خیر/تصریح نشده | — |

<a id="contract-SubmissionReceipt"></a>
### SubmissionReceipt

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `submissionAttemptId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `attemptNumber` | integer / int32 | بله | خیر/تصریح نشده | — |
| `submittedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `isLate` | boolean | بله | خیر/تصریح نشده | — |
| `status` | [SubmissionAttemptStatus](#contract-SubmissionAttemptStatus) | بله | خیر/تصریح نشده | — |
| `rowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-SubscriptionStatus"></a>
### SubscriptionStatus

CLR واقعی: `MakanApp.Domain.Commerce.SubscriptionStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Pending", "Active", "Expired", "Cancelled"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-SuggestTeacherActivitiesCommand"></a>
### SuggestTeacherActivitiesCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `objective` | string | بله | خیر/تصریح نشده | — |
| `availableMinutes` | integer / int32 | بله | بله | — |
| `materials` | آرایه از string | بله | خیر/تصریح نشده | — |
| `notes` | string | بله | بله | — |

<a id="contract-TeacherAssignmentReportItem"></a>
### TeacherAssignmentReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `assignmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `assignmentVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `dueAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `enrollmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerDisplayName` | string | بله | خیر/تصریح نشده | — |
| `submissionAttemptId` | string / uuid | بله | بله | — |
| `submissionStatus` | [NullableOfSubmissionAttemptStatus](#contract-NullableOfSubmissionAttemptStatus) | بله | خیر/تصریح نشده | — |
| `submittedAtUtc` | string / date-time | بله | بله | — |
| `isLate` | boolean | بله | بله | — |
| `currentScore` | number / double | بله | بله | — |
| `evaluationStatus` | [NullableOfEvaluationRevisionStatus](#contract-NullableOfEvaluationRevisionStatus) | بله | خیر/تصریح نشده | — |
| `isReleased` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-TeacherAssignmentResult"></a>
### TeacherAssignmentResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `teacherMembershipId` | string / uuid | بله | خیر/تصریح نشده | — |
| `status` | [TeacherAssignmentStatus](#contract-TeacherAssignmentStatus) | بله | خیر/تصریح نشده | — |
| `assignedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-TeacherAssignmentStatus"></a>
### TeacherAssignmentStatus

CLR واقعی: `MakanApp.Domain.Academic.TeacherAssignmentStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Ended"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-TeacherAttendanceReportItem"></a>
### TeacherAttendanceReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `sessionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `sessionTitle` | string | بله | خیر/تصریح نشده | — |
| `startUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `enrollmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerDisplayName` | string | بله | خیر/تصریح نشده | — |
| `status` | [AttendanceStatus](#contract-AttendanceStatus) | بله | خیر/تصریح نشده | — |
| `recordedAtUtc` | string / date-time | بله | بله | — |

<a id="contract-TeacherDashboardDto"></a>
### TeacherDashboardDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | خیر/تصریح نشده | — |
| `audience` | [DashboardAudience](#contract-DashboardAudience) | بله | خیر/تصریح نشده | — |
| `assignedClasses` | آرایه از [DashboardClassItem](#contract-DashboardClassItem) | بله | خیر/تصریح نشده | — |
| `upcomingSessions` | آرایه از [DashboardSessionItem2](#contract-DashboardSessionItem2) | بله | خیر/تصریح نشده | — |
| `attendancePending` | آرایه از [DashboardAttendancePendingItem](#contract-DashboardAttendancePendingItem) | بله | خیر/تصریح نشده | — |
| `assignmentGradingQueue` | [AssignmentGradingQueueSummary](#contract-AssignmentGradingQueueSummary) | بله | خیر/تصریح نشده | — |
| `examGradingQueue` | [ExamGradingQueueSummary](#contract-ExamGradingQueueSummary) | بله | خیر/تصریح نشده | — |

<a id="contract-TeacherExamReportItem"></a>
### TeacherExamReportItem

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `examId` | string / uuid | بله | خیر/تصریح نشده | — |
| `examVersionId` | string / uuid | بله | خیر/تصریح نشده | — |
| `title` | string | بله | خیر/تصریح نشده | — |
| `availableUntilUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `maximumScore` | number / double | بله | خیر/تصریح نشده | — |
| `enrollmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerDisplayName` | string | بله | خیر/تصریح نشده | — |
| `examAttemptId` | string / uuid | بله | بله | — |
| `attemptStatus` | [NullableOfExamAttemptStatus](#contract-NullableOfExamAttemptStatus) | بله | خیر/تصریح نشده | — |
| `finalizedAtUtc` | string / date-time | بله | بله | — |
| `currentScore` | number / double | بله | بله | — |
| `gradeStatus` | [NullableOfExamGradeRevisionStatus](#contract-NullableOfExamGradeRevisionStatus) | بله | خیر/تصریح نشده | — |
| `isReleased` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-TeacherLearnerOverviewDto"></a>
### TeacherLearnerOverviewDto

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `dataAsOfUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `classId` | string / uuid | بله | خیر/تصریح نشده | — |
| `classTitle` | string | بله | خیر/تصریح نشده | — |
| `enrollmentId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerOrganizationPersonId` | string / uuid | بله | خیر/تصریح نشده | — |
| `learnerDisplayName` | string | بله | خیر/تصریح نشده | — |
| `eligibleSessionCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `presentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `absentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `lateCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `excusedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `notRecordedCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `assignmentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `submittedAssignmentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `releasedAssignmentCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `examCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `finalizedExamAttemptCount` | integer / int32 | بله | خیر/تصریح نشده | — |
| `releasedExamCount` | integer / int32 | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateAssignmentDraftCommand"></a>
### UpdateAssignmentDraftCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | خیر/تصریح نشده | — |
| `dueAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `allowLateSubmission` | boolean | بله | خیر/تصریح نشده | — |
| `maxAttempts` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `expectedAssignmentRowVersion` | string | بله | خیر/تصریح نشده | — |
| `expectedVersionRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateClassLessonCommand"></a>
### UpdateClassLessonCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `body` | string | بله | بله | — |
| `displayOrder` | integer / int32 | بله | خیر/تصریح نشده | — |
| `fileAssetIds` | آرایه از string / uuid | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateConversationChecklistCommand"></a>
### UpdateConversationChecklistCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `mode` | [NullableOfConversationChecklistMode](#contract-NullableOfConversationChecklistMode) | بله | خیر/تصریح نشده | — |
| `items` | آرایه از [ChecklistItemInput](#contract-ChecklistItemInput) | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateConversationInfoCommand"></a>
### UpdateConversationInfoCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateConversationPermissionsCommand"></a>
### UpdateConversationPermissionsCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `permissions` | [ConversationAdminPermission](#contract-ConversationAdminPermission) | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateConversationSettingsCommand"></a>
### UpdateConversationSettingsCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `visibility` | [ConversationVisibility](#contract-ConversationVisibility) | بله | خیر/تصریح نشده | — |
| `groupHistoryPolicy` | [GroupHistoryPolicy](#contract-GroupHistoryPolicy) | بله | خیر/تصریح نشده | — |
| `memberCapabilities` | [GroupMemberCapability](#contract-GroupMemberCapability) | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateConversationTopicCommand"></a>
### UpdateConversationTopicCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `status` | [ConversationTopicStatus](#contract-ConversationTopicStatus) | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateExamDraftCommand"></a>
### UpdateExamDraftCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `description` | string | بله | بله | — |
| `availableFromUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `availableUntilUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `durationMinutes` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxAttempts` | integer / int32 | بله | خیر/تصریح نشده | — |
| `maxScore` | number / double | بله | خیر/تصریح نشده | — |
| `randomizationPolicy` | [ExamRandomizationPolicy](#contract-ExamRandomizationPolicy) | بله | خیر/تصریح نشده | — |
| `expectedVersionRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateExamQuestionCommand"></a>
### UpdateExamQuestionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `order` | integer / int32 | بله | خیر/تصریح نشده | — |
| `type` | [ExamQuestionType](#contract-ExamQuestionType) | بله | خیر/تصریح نشده | — |
| `prompt` | string | بله | خیر/تصریح نشده | — |
| `score` | number / double | بله | خیر/تصریح نشده | — |
| `options` | آرایه از [ExamQuestionOptionCommand](#contract-ExamQuestionOptionCommand) | بله | بله | — |
| `expectedVersionRowVersion` | string | بله | خیر/تصریح نشده | — |
| `expectedQuestionRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateMessagingPrivacyCommand"></a>
### UpdateMessagingPrivacyCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `directMessagesAudience` | [PrivacyAudience](#contract-PrivacyAudience) | بله | خیر/تصریح نشده | — |
| `phoneNumberVisibilityAudience` | [PrivacyAudience](#contract-PrivacyAudience) | بله | خیر/تصریح نشده | — |
| `phoneNumberDiscoveryAudience` | [PrivacyAudience](#contract-PrivacyAudience) | بله | خیر/تصریح نشده | — |
| `groupInvitesAudience` | [PrivacyAudience](#contract-PrivacyAudience) | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | بله | — |
| `onlineAudience` | [NullableOfPrivacyAudience](#contract-NullableOfPrivacyAudience) | خیر | خیر/تصریح نشده | — |
| `lastSeenAudience` | [NullableOfPrivacyAudience](#contract-NullableOfPrivacyAudience) | خیر | خیر/تصریح نشده | — |

<a id="contract-UpdateNotificationPreferenceCommand"></a>
### UpdateNotificationPreferenceCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `pushEnabled` | boolean | بله | خیر/تصریح نشده | — |
| `smsEnabled` | boolean | بله | خیر/تصریح نشده | — |
| `quietHoursEnabled` | boolean | بله | خیر/تصریح نشده | — |
| `quietHoursStartLocal` | string / time | بله | بله | — |
| `quietHoursEndLocal` | string / time | بله | بله | — |
| `timeZoneId` | string | بله | بله | — |
| `expectedRowVersion` | string | بله | بله | — |

<a id="contract-UpdateOrganizationPersonNamesCommand"></a>
### UpdateOrganizationPersonNamesCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `firstName` | string | بله | خیر/تصریح نشده | — |
| `lastName` | string | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | بله | — |
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |
| `expectedPersonRowVersion` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateScheduleRuleCommand"></a>
### UpdateScheduleRuleCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `localDayOfWeek` | [DayOfWeek](#contract-DayOfWeek) | بله | خیر/تصریح نشده | — |
| `localStartTime` | string / time | بله | خیر/تصریح نشده | — |
| `durationMinutes` | integer / int32 | بله | خیر/تصریح نشده | — |
| `timeZoneId` | string | بله | خیر/تصریح نشده | — |
| `effectiveFrom` | string / date | بله | خیر/تصریح نشده | — |
| `effectiveUntil` | string / date | بله | خیر/تصریح نشده | — |
| `applyFromDate` | string / date | بله | خیر/تصریح نشده | — |
| `sessionTitle` | string | بله | خیر/تصریح نشده | — |
| `meetingUrl` | string | بله | بله | — |
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateSessionCommand"></a>
### UpdateSessionCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `title` | string | بله | خیر/تصریح نشده | — |
| `startUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `endUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `timeZoneId` | string | بله | خیر/تصریح نشده | — |
| `meetingUrl` | string | بله | بله | — |
| `expectedRowVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UpdateUserContactCommand"></a>
### UpdateUserContactCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `savedName` | string | بله | بله | — |
| `isFavorite` | boolean | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UserAccountStatus"></a>
### UserAccountStatus

CLR واقعی: `MakanApp.Domain.Identity.UserAccountStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Suspended"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-UserBlockResult"></a>
### UserBlockResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `blockId` | string / uuid | بله | خیر/تصریح نشده | — |
| `blockedUser` | [SafeMessagingIdentityResult](#contract-SafeMessagingIdentityResult) | بله | خیر/تصریح نشده | — |
| `status` | [UserBlockStatus](#contract-UserBlockStatus) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `alreadyExisted` | boolean | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |

<a id="contract-UserBlockStatus"></a>
### UserBlockStatus

CLR واقعی: `MakanApp.Domain.Messaging.UserBlockStatus`؛ Flags: False

literalهای JSON واقعی MVC: `["Active", "Ended"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

<a id="contract-UserContactPageResult"></a>
### UserContactPageResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `items` | آرایه از [UserContactResult](#contract-UserContactResult) | بله | خیر/تصریح نشده | — |
| `page` | integer / int32 | بله | خیر/تصریح نشده | — |
| `pageSize` | integer / int32 | بله | خیر/تصریح نشده | — |
| `hasMore` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-UserContactResult"></a>
### UserContactResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `contactUserId` | string / uuid | بله | خیر/تصریح نشده | — |
| `savedName` | string | بله | بله | — |
| `isFavorite` | boolean | بله | خیر/تصریح نشده | — |
| `profile` | [MessagingPublicProfileResult](#contract-MessagingPublicProfileResult) | بله | خیر/تصریح نشده | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `updatedAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `version` | string | بله | خیر/تصریح نشده | — |
| `alreadyExisted` | boolean | خیر | خیر/تصریح نشده | {"default": false} |

<a id="contract-UserSessionResult"></a>
### UserSessionResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `id` | string / uuid | بله | خیر/تصریح نشده | — |
| `deviceName` | string | بله | بله | — |
| `createdAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `absoluteExpiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `isCurrent` | boolean | بله | خیر/تصریح نشده | — |

<a id="contract-VerifyCommunicationAgeCommand"></a>
### VerifyCommunicationAgeCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `verifiedBirthDate` | string / date | بله | خیر/تصریح نشده | — |
| `expectedVersion` | string | بله | خیر/تصریح نشده | — |
| `reason` | string | بله | خیر/تصریح نشده | — |

<a id="contract-VerifyOtpCommand"></a>
### VerifyOtpCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `challengeId` | string / uuid | بله | خیر/تصریح نشده | — |
| `phoneNumber` | string | بله | خیر/تصریح نشده | — |
| `code` | string | بله | خیر/تصریح نشده | — |
| `deviceName` | string | خیر | بله | {"default": null} |

<a id="contract-VerifyOtpResult"></a>
### VerifyOtpResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `accessToken` | string | بله | خیر/تصریح نشده | — |
| `tokenType` | string | بله | خیر/تصریح نشده | — |
| `expiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `refreshToken` | string | بله | خیر/تصریح نشده | — |
| `refreshExpiresAtUtc` | string / date-time | بله | خیر/تصریح نشده | — |
| `user` | [CurrentUserResult](#contract-CurrentUserResult) | بله | خیر/تصریح نشده | — |

<a id="contract-VoteConversationPollCommand"></a>
### VoteConversationPollCommand

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `optionNumbers` | آرایه از integer / int32 | بله | خیر/تصریح نشده | — |
| `expectedVoteVersion` | string | خیر | بله | {"default": null} |

<a id="contract-WebPushClientConfiguration"></a>
### WebPushClientConfiguration

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `isConfigured` | boolean | بله | خیر/تصریح نشده | — |
| `vapidPublicKey` | string | بله | بله | — |

<a id="contract-WorkspaceResult"></a>
### WorkspaceResult

object

| فیلد | نوع | required | nullable | قید منتشرشده |
|---|---|---|---|---|
| `workspaceType` | [WorkspaceType](#contract-WorkspaceType) | بله | خیر/تصریح نشده | — |
| `organizationId` | string / uuid | بله | بله | — |
| `organizationName` | string | بله | بله | — |
| `membershipId` | string / uuid | بله | بله | — |
| `role` | [NullableOfOrganizationRole](#contract-NullableOfOrganizationRole) | بله | خیر/تصریح نشده | — |
| `displayName` | string | بله | خیر/تصریح نشده | — |
| `status` | string | بله | خیر/تصریح نشده | — |

<a id="contract-WorkspaceType"></a>
### WorkspaceType

CLR واقعی: `MakanApp.Domain.Organization.WorkspaceType`؛ Flags: False

literalهای JSON واقعی MVC: `["Personal", "Organization"]`

نوع خام OpenAPI: `string`؛ اختلاف integer/String: در این snapshot دیده نشد؛ قید use case و Flags جدا بررسی شود.

## کدهای خطای تعریف‌شده

این فهرست از فایل‌های ErrorCodes استخراج شده؛ تعریف یک literal به‌تنهایی اثبات امکان وقوع عمومی آن نیست. status فقط در صورت نگاشت صریح در GlobalExceptionHandler نشان داده شده است.

| code | HTTP صریح | منبع |
|---|---|---|
| `ACADEMIC_PERIOD_INVALID` | طبق handler/مسیر فراخوانی | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ACADEMIC_PERIOD_NOT_FOUND` | 404 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ACADEMIC_READ_NOT_ALLOWED` | 403 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ACCOUNT_SUSPENDED` | 403 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `AI_CONTEXT_NOT_ALLOWED` | 403 | [AiServiceErrorCodes](../MakanApp.Application/Ai/AiServiceErrorCodes.cs) |
| `AI_FEATURE_DISABLED` | 503 | [AiServiceErrorCodes](../MakanApp.Application/Ai/AiServiceErrorCodes.cs) |
| `AI_INVALID_REQUEST` | 400 | [AiServiceErrorCodes](../MakanApp.Application/Ai/AiServiceErrorCodes.cs) |
| `AI_RATE_LIMITED` | 429 | [AiServiceErrorCodes](../MakanApp.Application/Ai/AiServiceErrorCodes.cs) |
| `AI_RESPONSE_INVALID` | 502 | [AiServiceErrorCodes](../MakanApp.Application/Ai/AiServiceErrorCodes.cs) |
| `AI_SERVICE_FORBIDDEN` | 503 | [AiServiceErrorCodes](../MakanApp.Application/Ai/AiServiceErrorCodes.cs) |
| `AI_SERVICE_TIMEOUT` | 504 | [AiServiceErrorCodes](../MakanApp.Application/Ai/AiServiceErrorCodes.cs) |
| `AI_SERVICE_UNAUTHORIZED` | 503 | [AiServiceErrorCodes](../MakanApp.Application/Ai/AiServiceErrorCodes.cs) |
| `AI_SERVICE_UNAVAILABLE` | 503 | [AiServiceErrorCodes](../MakanApp.Application/Ai/AiServiceErrorCodes.cs) |
| `ASSIGNMENT_ALREADY_PUBLISHED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_ATTEMPTS_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_DESCRIPTION_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_DUE_DATE_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_MAX_SCORE_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_NOT_ALLOWED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_NOT_DRAFT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_NOT_FOUND` | 404 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_NOT_SUBMITTABLE` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_RECIPIENT_NOT_FOUND` | 404 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_TITLE_REQUIRED` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ATTENDANCE_ALREADY_CHANGED` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ATTENDANCE_ALREADY_RECORDED` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ATTENDANCE_CORRECTION_REASON_REQUIRED` | طبق handler/مسیر فراخوانی | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ATTENDANCE_ENROLLMENT_INVALID` | طبق handler/مسیر فراخوانی | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ATTENDANCE_NOT_ALLOWED` | 403 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `AUTH_REQUIRED` | 401 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `CHANGE_CURSOR_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CHILD_CONTEXT_NOT_ALLOWED` | 403 | [GuardianErrorCodes](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |
| `CHILD_CONTEXT_NOT_FOUND` | 404 | [GuardianErrorCodes](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |
| `CLASS_CAPACITY_EXCEEDED` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `CLASS_INVALID` | طبق handler/مسیر فراخوانی | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `CLASS_NOT_ACTIVE` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `CLASS_NOT_FOUND` | 404 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `CLASS_SESSION_CONFLICT` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `COMMERCE_SUMMARY_NOT_ALLOWED` | 403 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `COMMERCE_USAGE_CONFLICT` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `COMMERCE_USAGE_INVALID` | 400 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `CONCURRENCY_CONFLICT` | 412 | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `CONCURRENCY_CONFLICT` | طبق handler/مسیر فراخوانی | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `CONCURRENCY_CONFLICT` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `CONCURRENCY_CONFLICT` | طبق handler/مسیر فراخوانی | [GuardianErrorCodes](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |
| `CONCURRENCY_CONFLICT` | طبق handler/مسیر فراخوانی | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONCURRENCY_CONFLICT` | طبق handler/مسیر فراخوانی | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `CONTACT_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONTACT_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONTACT_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONTACT_SELF_NOT_ALLOWED` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONTACT_TARGET_NOT_AVAILABLE` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_ADVANCED_MANAGEMENT_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_ARCHIVED` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_CHECKLIST_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_CHECKLIST_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_CHECKLIST_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_CHECKLIST_STATE_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_CHECKLIST_STRUCTURE_IMMUTABLE` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_CREATION_CONFLICT` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_HISTORY_NOT_VISIBLE` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_INVITE_LINK_UNAVAILABLE` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_MANAGEMENT_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_MEMBER_RESTRICTED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_NOT_JOINABLE` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_PERMISSION_REQUIRED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_POLL_CLOSED` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_POLL_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_POLL_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_POLL_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_POLL_VOTE_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_POLL_VOTE_IMMUTABLE` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_ROLE_INVALID` | طبق handler/مسیر فراخوانی | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_STATE_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_TOPIC_CLOSED` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_TOPIC_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_TOPIC_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `COURSE_INVALID` | طبق handler/مسیر فراخوانی | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `COURSE_NOT_FOUND` | 404 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `DELIVERY_CURSOR_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `DIRECTORY_PAGE_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `DIRECT_CONVERSATION_CONFLICT` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `DIRECT_RECIPIENT_NOT_AVAILABLE` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `ENROLLMENT_ALREADY_ACTIVE` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ENROLLMENT_NOT_ACTIVE` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ENTITLEMENT_REQUIRED` | 403 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `EVALUATION_ALREADY_RELEASED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_CORRECTION_REASON_REQUIRED` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_NOT_ALLOWED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_NOT_FOUND` | 404 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_NOT_READY_FOR_RELEASE` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_SCORE_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_SUBMISSION_NOT_FINAL` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ALREADY_FINALIZED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ALREADY_PUBLISHED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_IDEMPOTENCY_CONFLICT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_KEY_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_NOT_ALLOWED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_OPTION_INVALID` | 400 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_SET_VERSION_CONFLICT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_TYPE_INVALID` | 400 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_VERSION_CONFLICT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPTS_EXHAUSTED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPTS_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPT_NOT_ALLOWED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPT_NOT_FINALIZABLE` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPT_NOT_FOUND` | 404 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPT_NOT_WRITABLE` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_DEADLINE_PASSED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_DURATION_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_FINALIZE_CONFLICT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_FINALIZE_IDEMPOTENCY_CONFLICT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_ALREADY_RELEASED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_ATTEMPT_NOT_FINALIZED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_CORRECTION_REASON_REQUIRED` | 400 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_INCOMPLETE` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_NOT_ALLOWED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_NOT_FOUND` | 404 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_NOT_RELEASED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_QUESTION_NOT_REVIEWED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_RELEASE_CONFLICT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_RELEASE_IDEMPOTENCY_CONFLICT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_RELEASE_NOT_ALLOWED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_SCORE_INVALID` | 400 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_MAX_SCORE_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_NOT_ALLOWED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_NOT_AVAILABLE_YET` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_NOT_DRAFT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_NOT_FOUND` | 404 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_NOT_PUBLISHED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_QUESTION_ORDER_INVALID` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_QUESTION_REQUIRED` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_QUESTION_SCORE_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_QUESTION_SET_INVALID` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_RANDOMIZATION_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_SCORE_TOTAL_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_START_IDEMPOTENCY_CONFLICT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_STUDENT_PREVIEW_NOT_ALLOWED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_TITLE_REQUIRED` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_VERSION_LOCKED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_WINDOW_CLOSED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_WINDOW_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_WRITE_LEASE_HELD_BY_OTHER_SESSION` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_WRITE_LEASE_REQUIRED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_WRITE_LEASE_STALE` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `FILE_ALREADY_DELETED` | 409 | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_CONTENT_INVALID` | 415 | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_EMPTY` | طبق handler/مسیر فراخوانی | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_IN_USE` | 409 | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_NOT_ALLOWED` | 403 | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_NOT_ALLOWED` | طبق handler/مسیر فراخوانی | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `FILE_NOT_FOUND` | 404 | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_NOT_READY` | 409 | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_STORAGE_FAILED` | 503 | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_TOO_LARGE` | 413 | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_TYPE_NOT_ALLOWED` | 415 | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_UPLOAD_FAILED` | 500 | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `GRADE_NOT_RELEASED` | 404 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `GRADE_RELEASE_CONFLICT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `GUARDIAN_RELATION_NOT_FOUND` | 404 | [GuardianErrorCodes](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |
| `IDENTITY_SESSION_NOT_FOUND` | 404 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `INVITATION_ALREADY_ACCEPTED` | 409 | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `INVITATION_EXPIRED` | 410 | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `INVITATION_NOT_FOUND` | 404 | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `INVITATION_REVOKED` | 410 | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `LAST_OWNER_REQUIRED` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `LEARNER_NOT_FOUND` | 404 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `MANAGED_CONVERSATION_INVALID` | طبق handler/مسیر فراخوانی | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MANAGER_ROLE_REQUIRED` | 403 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `MEMBERSHIP_NOT_ACTIVE` | 403 | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `MENTION_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_ATTACHMENT_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_ATTACHMENT_NOT_READY` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_ATTACHMENT_REQUIRED` | طبق handler/مسیر فراخوانی | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_CLIENT_ID_REQUIRED` | طبق handler/مسیر فراخوانی | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_DELETED` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_DELETE_WINDOW_EXPIRED` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_EDIT_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_EDIT_WINDOW_EXPIRED` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_EMPTY` | طبق handler/مسیر فراخوانی | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_FORWARD_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_FORWARD_SCOPE_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_IDEMPOTENCY_CONFLICT` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_KIND_INVALID` | طبق handler/مسیر فراخوانی | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_NOT_EDITABLE` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_PAGE_INVALID` | طبق handler/مسیر فراخوانی | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_PUBLISH_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_REPLY_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_SAFETY_BLOCKED` | 422 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_SAFETY_REVIEW_REQUIRED` | 422 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_SAFETY_UNAVAILABLE` | 503 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_TOO_LONG` | طبق handler/مسیر فراخوانی | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGING_ACTIVITY_RATE_LIMITED` | 429 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGING_DISCOVERY_RATE_LIMITED` | 429 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGING_PRESENTATION_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGING_PRESENTATION_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGING_PRESENTATION_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGING_PRIVACY_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGING_PRIVACY_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MODERATION_CASE_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MODERATION_PERMISSION_REQUIRED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MODERATION_STATE_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `ORDER_IDEMPOTENCY_CONFLICT` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `ORDER_INVALID` | 400 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `ORDER_MANAGEMENT_NOT_ALLOWED` | 403 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `ORDER_NOT_FOUND` | 404 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `ORDER_NOT_PAYABLE` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `ORGANIZATION_NOT_FOUND` | 404 | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `ORGANIZATION_SCOPE_MISMATCH` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `ORGANIZATION_SCOPE_MISMATCH` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `OTP_ALREADY_USED` | 409 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `OTP_EXPIRED` | 410 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `OTP_INVALID` | طبق handler/مسیر فراخوانی | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `OTP_RATE_LIMITED` | 429 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `OTP_TOO_MANY_ATTEMPTS` | 429 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `OWNERSHIP_TRANSFER_CONFLICT` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `OWNERSHIP_TRANSFER_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PARENT_ROLE_REQUIRED` | 403 | [GuardianErrorCodes](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |
| `PARTICIPANT_ALREADY_ACTIVE` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PARTICIPANT_NOT_ACTIVE` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PARTICIPANT_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PAYMENT_AMOUNT_MISMATCH` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PAYMENT_ATTEMPT_NOT_FOUND` | 404 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PAYMENT_CURRENCY_MISMATCH` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PAYMENT_IDEMPOTENCY_CONFLICT` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PAYMENT_INVALID` | 400 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PAYMENT_PENDING_VERIFICATION` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PAYMENT_PROVIDER_UNAVAILABLE` | 503 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PAYMENT_RATE_LIMITED` | 429 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PAYMENT_REFERENCE_CONFLICT` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PAYMENT_REFERENCE_INVALID` | 400 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PEOPLE_RESOLVE_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PEOPLE_SEARCH_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PERSON_NOT_DISCOVERABLE` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PHONE_INVALID` | طبق handler/مسیر فراخوانی | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `PIN_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PLAN_NOT_PURCHASABLE` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `PLATFORM_ACCESS_DENIED` | 403 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_ACCOUNT_SUSPENDED` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_ASSIGNMENT_CONFLICT` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_ASSIGNMENT_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_BOOTSTRAP_CLOSED` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_EDUCATION_RESOURCE_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_LAST_MANAGER_REQUIRED` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_LAST_OWNER_REQUIRED` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_MEMBERSHIP_CONFLICT` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_MEMBERSHIP_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_MODERATION_CASE_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_MODERATION_STATE_CONFLICT` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_ORDER_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_ORGANIZATION_CONFLICT` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_ORGANIZATION_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_PLAN_CONFLICT` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_PLAN_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_PLAN_VERSION_NOT_PUBLISHED` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_PROVISIONING_CONFLICT` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_PROVISIONING_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_QUERY_INVALID` | 400 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_REASON_REQUIRED` | 400 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_ROLE_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_SESSION_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_SUBSCRIPTION_CONFLICT` | 409 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PLATFORM_USER_NOT_FOUND` | 404 | [PlatformErrorCodes](../MakanApp.Application/Platform/PlatformErrorCodes.cs) |
| `PROFILE_VALIDATION_FAILED` | طبق handler/مسیر فراخوانی | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `QUOTA_EXCEEDED` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `QUOTA_REQUIRED` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `RATE_LIMITED` | طبق handler/مسیر فراخوانی | [ApiErrorCodes](../MakanApp.Api/Errors/ApiErrorCodes.cs) |
| `REACTION_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `READ_CURSOR_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `REALTIME_SUBSCRIPTION_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `REFRESH_TOKEN_INVALID` | 401 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `REFRESH_TOKEN_REUSED` | 401 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `REPORT_DATA_NO_LONGER_AUTHORIZED` | طبق handler/مسیر فراخوانی | [ReportExportErrorCodes](../MakanApp.Application/Reports/Exports/ReportExportErrorCodes.cs) |
| `REPORT_DATE_RANGE_INVALID` | طبق handler/مسیر فراخوانی | [DetailedReportErrorCodes](../MakanApp.Application/Reports/Detailed/DetailedReportErrorCodes.cs) |
| `REPORT_EXPORT_FORMAT_UNSUPPORTED` | 400 | [ReportExportErrorCodes](../MakanApp.Application/Reports/Exports/ReportExportErrorCodes.cs) |
| `REPORT_EXPORT_INVALID` | طبق handler/مسیر فراخوانی | [ReportExportErrorCodes](../MakanApp.Application/Reports/Exports/ReportExportErrorCodes.cs) |
| `REPORT_EXPORT_NOT_FOUND` | 404 | [ReportExportErrorCodes](../MakanApp.Application/Reports/Exports/ReportExportErrorCodes.cs) |
| `REPORT_EXPORT_NOT_READY` | 409 | [ReportExportErrorCodes](../MakanApp.Application/Reports/Exports/ReportExportErrorCodes.cs) |
| `REPORT_EXPORT_ROW_LIMIT_EXCEEDED` | 422 | [ReportExportErrorCodes](../MakanApp.Application/Reports/Exports/ReportExportErrorCodes.cs) |
| `REPORT_GENERATION_FAILED` | طبق handler/مسیر فراخوانی | [ReportExportErrorCodes](../MakanApp.Application/Reports/Exports/ReportExportErrorCodes.cs) |
| `REPORT_IDEMPOTENCY_CONFLICT` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `REPORT_MESSAGE_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `REPORT_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `REPORT_PAGINATION_INVALID` | طبق handler/مسیر فراخوانی | [DetailedReportErrorCodes](../MakanApp.Application/Reports/Detailed/DetailedReportErrorCodes.cs) |
| `REPORT_RESOURCE_NOT_FOUND` | 404 | [DetailedReportErrorCodes](../MakanApp.Application/Reports/Detailed/DetailedReportErrorCodes.cs) |
| `REPORT_SORT_INVALID` | طبق handler/مسیر فراخوانی | [DetailedReportErrorCodes](../MakanApp.Application/Reports/Detailed/DetailedReportErrorCodes.cs) |
| `ROLE_NOT_ACTIVE` | 403 | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `SCHEDULED_MESSAGE_CLAIM_LOST` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SCHEDULED_MESSAGE_CONFLICT` | 412 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SCHEDULED_MESSAGE_ID_RESERVED` | 409 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SCHEDULED_MESSAGE_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SCHEDULED_MESSAGE_NOT_FOUND` | 404 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SCHEDULE_RULE_NOT_FOUND` | 404 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SEARCH_CURSOR_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SEARCH_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SEARCH_QUERY_INVALID` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SEARCH_QUERY_REQUIRED` | 400 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SESSION_CANCELLED` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SESSION_DEVICE_INVALID` | طبق handler/مسیر فراخوانی | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `SESSION_NOT_ACTIVE` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SESSION_NOT_FOUND` | 404 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SESSION_TIME_CONFLICT` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SESSION_TIME_INVALID` | طبق handler/مسیر فراخوانی | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SMS_PROVIDER_AUTHENTICATION_FAILED` | 503 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `SMS_PROVIDER_CONFIGURATION_ERROR` | 503 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `SMS_PROVIDER_REJECTED` | 422 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `SMS_PROVIDER_UNAVAILABLE` | 503 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `SMS_RATE_LIMITED` | 429 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `SMS_RECIPIENT_BLOCKED` | 422 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `SMS_RECIPIENT_INVALID` | 400 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `SUBMISSION_ANSWER_INVALID` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_ATTEMPTS_EXHAUSTED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_DEADLINE_PASSED` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_EMPTY` | طبق handler/مسیر فراخوانی | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_FILE_NOT_ALLOWED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_FILE_NOT_READY` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_NOT_ALLOWED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_NOT_DRAFT` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_NOT_FOUND` | 404 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_VERSION_MISMATCH` | 409 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBSCRIPTION_INACTIVE` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `SUBSCRIPTION_REQUIRED` | 409 | [CommerceErrorCodes](../MakanApp.Application/Commerce/CommerceErrorCodes.cs) |
| `TEACHER_ASSIGNMENT_ALREADY_ACTIVE` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `TEACHER_ASSIGNMENT_NOT_ACTIVE` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `TEACHER_NOT_ALLOWED` | 403 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `TEACHER_NOT_ASSIGNED` | 403 | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `TEACHER_SESSION_CONFLICT` | 409 | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `USERNAME_ALREADY_EXISTS` | 409 | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `USER_BLOCK_NOT_ALLOWED` | 403 | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `WORKSPACE_NOT_ALLOWED` | 403 | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `WORKSPACE_NOT_FOUND` | 404 | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
