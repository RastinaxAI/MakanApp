using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Domain.Reports;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Persistence;

public sealed class MakanDbContext(DbContextOptions<MakanDbContext> options)
    : DbContext(options)
{
    public DbSet<AcademicPeriod> AcademicPeriods => Set<AcademicPeriod>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<ClassLesson> ClassLessons => Set<ClassLesson>();
    public DbSet<ClassLessonAttachment> ClassLessonAttachments => Set<ClassLessonAttachment>();
    public DbSet<MakanApp.Domain.Academic.Class> Classes => Set<MakanApp.Domain.Academic.Class>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<Session> AcademicSessions => Set<Session>();
    public DbSet<ScheduleRule> ScheduleRules => Set<ScheduleRule>();
    public DbSet<Attendance> Attendance => Set<Attendance>();
    public DbSet<AttendanceRevision> AttendanceRevisions => Set<AttendanceRevision>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentVersion> AssignmentVersions => Set<AssignmentVersion>();
    public DbSet<AssignmentRecipient> AssignmentRecipients => Set<AssignmentRecipient>();
    public DbSet<SubmissionAttempt> SubmissionAttempts => Set<SubmissionAttempt>();
    public DbSet<SubmissionAttachment> SubmissionAttachments => Set<SubmissionAttachment>();
    public DbSet<EvaluationRevision> EvaluationRevisions => Set<EvaluationRevision>();
    public DbSet<GradeRelease> GradeReleases => Set<GradeRelease>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamVersion> ExamVersions => Set<ExamVersion>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<ExamAttemptQuestion> ExamAttemptQuestions => Set<ExamAttemptQuestion>();
    public DbSet<AnswerRevision> AnswerRevisions => Set<AnswerRevision>();
    public DbSet<ExamFinalAnswer> ExamFinalAnswers => Set<ExamFinalAnswer>();
    public DbSet<ExamGradeRevision> ExamGradeRevisions => Set<ExamGradeRevision>();
    public DbSet<ExamQuestionGrade> ExamQuestionGrades => Set<ExamQuestionGrade>();
    public DbSet<ExamGradeRelease> ExamGradeReleases => Set<ExamGradeRelease>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
    public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();
    public DbSet<PlanQuotaDefinition> PlanQuotaDefinitions => Set<PlanQuotaDefinition>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<UsageLedgerEntry> UsageLedgerEntries => Set<UsageLedgerEntry>();
    public DbSet<PlanVersionPrice> PlanVersionPrices => Set<PlanVersionPrice>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CommunicationAgeVerification> CommunicationAgeVerifications => Set<CommunicationAgeVerification>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationInvitation> ConversationInvitations => Set<ConversationInvitation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<ConversationUserState> ConversationUserStates => Set<ConversationUserState>();
    public DbSet<ChatFolder> ChatFolders => Set<ChatFolder>();
    public DbSet<FolderConversationOverride> FolderConversationOverrides => Set<FolderConversationOverride>();
    public DbSet<SavedMessageOrganization> SavedMessageOrganizations => Set<SavedMessageOrganization>();
    public DbSet<SavedMessageLabel> SavedMessageLabels => Set<SavedMessageLabel>();
    public DbSet<ConversationInviteLink> ConversationInviteLinks => Set<ConversationInviteLink>();
    public DbSet<ConversationJoinRequest> ConversationJoinRequests => Set<ConversationJoinRequest>();
    public DbSet<ConversationMemberRestriction> ConversationMemberRestrictions => Set<ConversationMemberRestriction>();
    public DbSet<ConversationTopic> ConversationTopics => Set<ConversationTopic>();
    public DbSet<ScheduledMessage> ScheduledMessages => Set<ScheduledMessage>();
    public DbSet<ConversationOwnershipTransfer> ConversationOwnershipTransfers =>
        Set<ConversationOwnershipTransfer>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageUserVisibility> MessageUserVisibilities => Set<MessageUserVisibility>();
    public DbSet<MessageRevision> MessageRevisions => Set<MessageRevision>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();
    public DbSet<MessageMention> MessageMentions => Set<MessageMention>();
    public DbSet<ConversationPin> ConversationPins => Set<ConversationPin>();
    public DbSet<MessagingChangeEvent> MessagingChangeEvents => Set<MessagingChangeEvent>();
    public DbSet<ConversationPoll> ConversationPolls => Set<ConversationPoll>();
    public DbSet<ConversationChecklist> ConversationChecklists => Set<ConversationChecklist>();
    public DbSet<ConversationChecklistItem> ConversationChecklistItems => Set<ConversationChecklistItem>();
    public DbSet<ConversationChecklistPersonalState> ConversationChecklistPersonalStates => Set<ConversationChecklistPersonalState>();
    public DbSet<ConversationPollOption> ConversationPollOptions => Set<ConversationPollOption>();
    public DbSet<ConversationPollVote> ConversationPollVotes => Set<ConversationPollVote>();
    public DbSet<ConversationPollSelection> ConversationPollSelections => Set<ConversationPollSelection>();
    public DbSet<ConversationQuizAnswer> ConversationQuizAnswers => Set<ConversationQuizAnswer>();
    public DbSet<MessagingRealtimeOutboxMessage> MessagingRealtimeOutboxMessages =>
        Set<MessagingRealtimeOutboxMessage>();
    public DbSet<PersonalCommunicationGrant> PersonalCommunicationGrants =>
        Set<PersonalCommunicationGrant>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<UserContact> UserContacts => Set<UserContact>();
    public DbSet<MessagingPrivacySettings> MessagingPrivacySettings => Set<MessagingPrivacySettings>();
    public DbSet<AbuseReport> AbuseReports => Set<AbuseReport>();
    public DbSet<ModerationCase> ModerationCases => Set<ModerationCase>();
    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();
    public DbSet<ModerationEvidenceAttachment> ModerationEvidenceAttachments =>
        Set<ModerationEvidenceAttachment>();
    public DbSet<SafetyIncident> SafetyIncidents => Set<SafetyIncident>();
    public DbSet<GuardianRelation> GuardianRelations => Set<GuardianRelation>();
    public DbSet<MakanApp.Domain.Organization.Organization> Organizations => Set<MakanApp.Domain.Organization.Organization>();
    public DbSet<OrganizationPerson> OrganizationPersons => Set<OrganizationPerson>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<PlatformRole> PlatformRoles => Set<PlatformRole>();
    public DbSet<PlatformPermission> PlatformPermissions => Set<PlatformPermission>();
    public DbSet<PlatformRolePermission> PlatformRolePermissions => Set<PlatformRolePermission>();
    public DbSet<PlatformRoleAssignment> PlatformRoleAssignments => Set<PlatformRoleAssignment>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<PlatformProvisioningRequest> PlatformProvisioningRequests => Set<PlatformProvisioningRequest>();
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();
    public DbSet<ReportExportJob> ReportExportJobs => Set<ReportExportJob>();
    public DbSet<MakanApp.Domain.Notification.Notification> Notifications => Set<MakanApp.Domain.Notification.Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<PushDeviceRegistration> PushDeviceRegistrations => Set<PushDeviceRegistration>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MakanDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAdminAuditIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminAuditIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureAdminAuditIsAppendOnly()
    {
        if (ChangeTracker.Entries<CommunicationAgeVerification>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("شاهد تأیید سن فقط قابل افزودن است.");
        if (ChangeTracker.Entries<AdminAuditLog>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("رویداد ممیزی مدیریتی فقط قابل افزودن است.");
        }
    }
}
