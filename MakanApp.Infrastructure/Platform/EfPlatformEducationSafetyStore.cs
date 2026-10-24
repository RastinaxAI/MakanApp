using System.Data;
using System.Security.Cryptography;
using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Platform;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed partial class EfPlatformEducationSafetyStore(MakanDbContext dbContext,
    IPlatformIamStore iamStore, TimeProvider clock)
    : IPlatformEducationSafetyStore
{
    private const int MaximumClassDetailPeople = 200;

    public async Task<PlatformClassPage> GetClassesAsync(
        PlatformClassQuery query,
        CancellationToken cancellationToken)
    {
        var classes =
            from academicClass in dbContext.Classes.AsNoTracking()
            join organization in dbContext.Organizations.AsNoTracking()
                on academicClass.OrganizationId equals organization.Id
            join period in dbContext.AcademicPeriods.AsNoTracking()
                on new { academicClass.OrganizationId, Id = academicClass.AcademicPeriodId }
                equals new { period.OrganizationId, period.Id }
            join course in dbContext.Courses.AsNoTracking()
                on new { academicClass.OrganizationId, Id = academicClass.CourseId }
                equals new { course.OrganizationId, course.Id }
            select new { academicClass, organization, period, course };
        if (query.OrganizationId.HasValue)
        {
            classes = classes.Where(item => item.academicClass.OrganizationId == query.OrganizationId.Value);
        }

        if (query.Status.HasValue)
        {
            classes = classes.Where(item => item.academicClass.Status == query.Status.Value);
        }

        if (query.Search is not null)
        {
            var search = query.Search;
            classes = classes.Where(item =>
                item.academicClass.Title.Contains(search) ||
                item.course.Title.Contains(search) ||
                item.organization.Name.Contains(search));
        }

        var totalCount = await classes.CountAsync(cancellationToken);
        var items = await classes
            .OrderByDescending(item => item.academicClass.CreatedAtUtc)
            .ThenBy(item => item.academicClass.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new PlatformClassItem(
                item.academicClass.Id,
                new PlatformOrganizationContext(item.organization.Id, item.organization.Name),
                item.period.Id,
                item.period.Title,
                item.course.Id,
                item.course.Title,
                item.academicClass.Title,
                item.academicClass.Capacity,
                item.academicClass.Status,
                dbContext.TeacherAssignments.Count(assignment =>
                    assignment.OrganizationId == item.academicClass.OrganizationId &&
                    assignment.ClassId == item.academicClass.Id &&
                    assignment.Status == TeacherAssignmentStatus.Active &&
                    assignment.EndedAtUtc == null),
                dbContext.Enrollments.Count(enrollment =>
                    enrollment.OrganizationId == item.academicClass.OrganizationId &&
                    enrollment.ClassId == item.academicClass.Id &&
                    enrollment.Status == EnrollmentStatus.Active &&
                    enrollment.EndedAtUtc == null),
                item.academicClass.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new PlatformClassPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformClassDetail?> GetClassAsync(
        Guid classId,
        CancellationToken cancellationToken)
    {
        var item = await (
            from academicClass in dbContext.Classes.AsNoTracking()
            join organization in dbContext.Organizations.AsNoTracking()
                on academicClass.OrganizationId equals organization.Id
            join period in dbContext.AcademicPeriods.AsNoTracking()
                on new { academicClass.OrganizationId, Id = academicClass.AcademicPeriodId }
                equals new { period.OrganizationId, period.Id }
            join course in dbContext.Courses.AsNoTracking()
                on new { academicClass.OrganizationId, Id = academicClass.CourseId }
                equals new { course.OrganizationId, course.Id }
            where academicClass.Id == classId
            select new PlatformClassItem(
                academicClass.Id,
                new PlatformOrganizationContext(organization.Id, organization.Name),
                period.Id,
                period.Title,
                course.Id,
                course.Title,
                academicClass.Title,
                academicClass.Capacity,
                academicClass.Status,
                dbContext.TeacherAssignments.Count(assignment =>
                    assignment.OrganizationId == academicClass.OrganizationId &&
                    assignment.ClassId == academicClass.Id &&
                    assignment.Status == TeacherAssignmentStatus.Active &&
                    assignment.EndedAtUtc == null),
                dbContext.Enrollments.Count(enrollment =>
                    enrollment.OrganizationId == academicClass.OrganizationId &&
                    enrollment.ClassId == academicClass.Id &&
                    enrollment.Status == EnrollmentStatus.Active &&
                    enrollment.EndedAtUtc == null),
                academicClass.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return null;
        }

        var teachers = await (
            from assignment in dbContext.TeacherAssignments.AsNoTracking()
            join membership in dbContext.Memberships.AsNoTracking()
                on new { assignment.OrganizationId, Id = assignment.TeacherMembershipId }
                equals new { membership.OrganizationId, membership.Id }
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            where assignment.OrganizationId == item.Organization.Id &&
                  assignment.ClassId == classId &&
                  assignment.Status == TeacherAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null
            orderby assignment.AssignedAtUtc, assignment.Id
            select new PlatformClassPerson(
                assignment.Id,
                person == null ? Guid.Empty : person.Id,
                user.Id,
                person == null
                    ? user.Username ?? "User"
                    : person.DisplayName ?? person.FirstName + " " + person.LastName))
            .Take(MaximumClassDetailPeople + 1)
            .ToArrayAsync(cancellationToken);
        var teachersTruncated = teachers.Length > MaximumClassDetailPeople;
        teachers = teachers.Take(MaximumClassDetailPeople).ToArray();
        var learners = await (
            from enrollment in dbContext.Enrollments.AsNoTracking()
            join organizationPerson in dbContext.OrganizationPersons.AsNoTracking()
                on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join person in dbContext.Persons.AsNoTracking() on organizationPerson.PersonId equals person.Id
            join candidateUser in dbContext.Users.AsNoTracking() on person.Id equals candidateUser.PersonId into users
            from user in users.DefaultIfEmpty()
            where enrollment.OrganizationId == item.Organization.Id &&
                  enrollment.ClassId == classId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null
            orderby enrollment.EnrolledAtUtc, enrollment.Id
            select new PlatformClassPerson(
                enrollment.Id,
                person.Id,
                user == null ? null : user.Id,
                person.DisplayName ?? person.FirstName + " " + person.LastName))
            .Take(MaximumClassDetailPeople + 1)
            .ToArrayAsync(cancellationToken);
        var learnersTruncated = learners.Length > MaximumClassDetailPeople;
        learners = learners.Take(MaximumClassDetailPeople).ToArray();
        var sessionRows = await dbContext.AcademicSessions.AsNoTracking()
            .Where(session => session.OrganizationId == item.Organization.Id && session.ClassId == classId)
            .GroupBy(_ => 1)
            .Select(group => new PlatformClassSessionSummary(
                group.Count(),
                group.Count(session => session.Status == SessionStatus.Scheduled),
                group.Count(session => session.Status == SessionStatus.Completed),
                group.Count(session => session.Status == SessionStatus.Cancelled)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new PlatformClassSessionSummary(0, 0, 0, 0);
        var assessment = new PlatformClassAssessmentSummary(
            await dbContext.Assignments.CountAsync(value =>
                value.OrganizationId == item.Organization.Id && value.ClassId == classId, cancellationToken),
            await dbContext.Assignments.CountAsync(value =>
                value.OrganizationId == item.Organization.Id && value.ClassId == classId &&
                value.Status == AssignmentStatus.Published, cancellationToken),
            await dbContext.Exams.CountAsync(value =>
                value.OrganizationId == item.Organization.Id && value.ClassId == classId, cancellationToken),
            await dbContext.Exams.CountAsync(value =>
                value.OrganizationId == item.Organization.Id && value.ClassId == classId &&
                value.Status == ExamStatus.Published, cancellationToken));
        var attendance = await dbContext.Attendance.AsNoTracking()
            .Where(value => value.OrganizationId == item.Organization.Id && value.ClassId == classId)
            .GroupBy(_ => 1)
            .Select(group => new PlatformClassAttendanceSummary(
                group.Count(),
                group.Count(value => value.Status == AttendanceStatus.Present),
                group.Count(value => value.Status == AttendanceStatus.Absent),
                group.Count(value => value.Status == AttendanceStatus.Late),
                group.Count(value => value.Status == AttendanceStatus.Excused)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new PlatformClassAttendanceSummary(0, 0, 0, 0, 0);
        return new PlatformClassDetail(
            item,
            teachers,
            learners,
            teachersTruncated,
            learnersTruncated,
            sessionRows,
            assessment,
            attendance);
    }

    public async Task<PlatformTeacherAssignmentPage> GetTeachersAsync(
        PlatformEducationQuery query,
        CancellationToken cancellationToken)
    {
        var rows =
            from assignment in dbContext.TeacherAssignments.AsNoTracking()
            join academicClass in dbContext.Classes.AsNoTracking()
                on new { assignment.OrganizationId, Id = assignment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join organization in dbContext.Organizations.AsNoTracking()
                on assignment.OrganizationId equals organization.Id
            join membership in dbContext.Memberships.AsNoTracking()
                on new { assignment.OrganizationId, Id = assignment.TeacherMembershipId }
                equals new { membership.OrganizationId, membership.Id }
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            select new { assignment, academicClass, organization, membership, user, person };
        if (query.OrganizationId.HasValue)
        {
            rows = rows.Where(item => item.assignment.OrganizationId == query.OrganizationId.Value);
        }

        if (query.ClassId.HasValue)
        {
            rows = rows.Where(item => item.assignment.ClassId == query.ClassId.Value);
        }

        if (query.Search is not null)
        {
            var search = query.Search;
            rows = rows.Where(item =>
                item.academicClass.Title.Contains(search) ||
                item.organization.Name.Contains(search) ||
                item.user.Username != null && item.user.Username.Contains(search) ||
                item.person != null &&
                (item.person.DisplayName != null && item.person.DisplayName.Contains(search) ||
                 item.person.FirstName.Contains(search) || item.person.LastName.Contains(search)));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(item => item.assignment.AssignedAtUtc)
            .ThenBy(item => item.assignment.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new PlatformTeacherAssignmentItem(
                item.assignment.Id,
                new PlatformOrganizationContext(item.organization.Id, item.organization.Name),
                item.academicClass.Id,
                item.academicClass.Title,
                item.membership.Id,
                item.user.Id,
                item.person == null
                    ? item.user.Username ?? "User"
                    : item.person.DisplayName ?? item.person.FirstName + " " + item.person.LastName,
                item.assignment.Status,
                item.assignment.AssignedAtUtc,
                item.assignment.EndedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new PlatformTeacherAssignmentPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformEnrollmentPage> GetEnrollmentsAsync(
        PlatformEducationQuery query,
        CancellationToken cancellationToken)
    {
        var rows =
            from enrollment in dbContext.Enrollments.AsNoTracking()
            join academicClass in dbContext.Classes.AsNoTracking()
                on new { enrollment.OrganizationId, Id = enrollment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join organization in dbContext.Organizations.AsNoTracking()
                on enrollment.OrganizationId equals organization.Id
            join organizationPerson in dbContext.OrganizationPersons.AsNoTracking()
                on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join person in dbContext.Persons.AsNoTracking() on organizationPerson.PersonId equals person.Id
            join candidateUser in dbContext.Users.AsNoTracking() on person.Id equals candidateUser.PersonId into users
            from user in users.DefaultIfEmpty()
            select new { enrollment, academicClass, organization, organizationPerson, person, user };
        if (query.OrganizationId.HasValue)
        {
            rows = rows.Where(item => item.enrollment.OrganizationId == query.OrganizationId.Value);
        }

        if (query.ClassId.HasValue)
        {
            rows = rows.Where(item => item.enrollment.ClassId == query.ClassId.Value);
        }

        if (query.Search is not null)
        {
            var search = query.Search;
            rows = rows.Where(item => item.academicClass.Title.Contains(search) ||
                item.organization.Name.Contains(search) ||
                item.person.DisplayName != null && item.person.DisplayName.Contains(search) ||
                item.person.FirstName.Contains(search) || item.person.LastName.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(item => item.enrollment.EnrolledAtUtc)
            .ThenBy(item => item.enrollment.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new PlatformEnrollmentItem(
                item.enrollment.Id,
                new PlatformOrganizationContext(item.organization.Id, item.organization.Name),
                item.academicClass.Id,
                item.academicClass.Title,
                item.organizationPerson.Id,
                item.person.Id,
                item.user == null ? null : item.user.Id,
                item.person.DisplayName ?? item.person.FirstName + " " + item.person.LastName,
                item.enrollment.Status,
                item.enrollment.EnrolledAtUtc,
                item.enrollment.EndedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new PlatformEnrollmentPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformAssignmentPage> GetAssignmentsAsync(
        PlatformEducationQuery query,
        CancellationToken cancellationToken)
    {
        var rows =
            from assignment in dbContext.Assignments.AsNoTracking()
            join academicClass in dbContext.Classes.AsNoTracking()
                on new { assignment.OrganizationId, Id = assignment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join organization in dbContext.Organizations.AsNoTracking()
                on assignment.OrganizationId equals organization.Id
            join currentVersion in dbContext.AssignmentVersions.AsNoTracking()
                on new { assignment.OrganizationId, AssignmentId = assignment.Id, assignment.CurrentVersionNumber }
                equals new { currentVersion.OrganizationId, currentVersion.AssignmentId, CurrentVersionNumber = currentVersion.VersionNumber }
                into versions
            from currentVersion in versions.DefaultIfEmpty()
            select new { assignment, academicClass, organization, currentVersion };
        if (query.OrganizationId.HasValue)
        {
            rows = rows.Where(item => item.assignment.OrganizationId == query.OrganizationId.Value);
        }

        if (query.ClassId.HasValue)
        {
            rows = rows.Where(item => item.assignment.ClassId == query.ClassId.Value);
        }

        if (query.Search is not null)
        {
            var search = query.Search;
            rows = rows.Where(item => item.academicClass.Title.Contains(search) ||
                item.organization.Name.Contains(search) ||
                item.currentVersion != null && item.currentVersion.Title.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(item => item.assignment.CreatedAtUtc)
            .ThenBy(item => item.assignment.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new PlatformAssignmentItem(
                item.assignment.Id,
                new PlatformOrganizationContext(item.organization.Id, item.organization.Name),
                item.academicClass.Id,
                item.academicClass.Title,
                item.assignment.Status,
                item.assignment.CurrentVersionNumber,
                item.currentVersion == null ? null : item.currentVersion.Title,
                item.currentVersion == null ? null : item.currentVersion.MaxScore,
                item.assignment.CreatedAtUtc,
                item.assignment.PublishedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new PlatformAssignmentPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformExamPage> GetExamsAsync(
        PlatformEducationQuery query,
        CancellationToken cancellationToken)
    {
        var rows =
            from exam in dbContext.Exams.AsNoTracking()
            join academicClass in dbContext.Classes.AsNoTracking()
                on new { exam.OrganizationId, Id = exam.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join organization in dbContext.Organizations.AsNoTracking()
                on exam.OrganizationId equals organization.Id
            join currentVersion in dbContext.ExamVersions.AsNoTracking()
                on new { exam.OrganizationId, ExamId = exam.Id, exam.CurrentVersionNumber }
                equals new { currentVersion.OrganizationId, currentVersion.ExamId, CurrentVersionNumber = currentVersion.VersionNumber }
                into versions
            from currentVersion in versions.DefaultIfEmpty()
            select new { exam, academicClass, organization, currentVersion };
        if (query.OrganizationId.HasValue)
        {
            rows = rows.Where(item => item.exam.OrganizationId == query.OrganizationId.Value);
        }

        if (query.ClassId.HasValue)
        {
            rows = rows.Where(item => item.exam.ClassId == query.ClassId.Value);
        }

        if (query.Search is not null)
        {
            var search = query.Search;
            rows = rows.Where(item => item.academicClass.Title.Contains(search) ||
                item.organization.Name.Contains(search) ||
                item.currentVersion != null && item.currentVersion.Title.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(item => item.exam.CreatedAtUtc)
            .ThenBy(item => item.exam.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new PlatformExamItem(
                item.exam.Id,
                new PlatformOrganizationContext(item.organization.Id, item.organization.Name),
                item.academicClass.Id,
                item.academicClass.Title,
                item.exam.Status,
                item.exam.CurrentVersionNumber,
                item.currentVersion == null ? null : item.currentVersion.Title,
                item.currentVersion == null ? null : item.currentVersion.MaxScore,
                item.exam.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new PlatformExamPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformConversationMetadataPage> GetConversationsAsync(
        PlatformConversationMetadataQuery query,
        CancellationToken cancellationToken)
    {
        var rows =
            from conversation in dbContext.Conversations.AsNoTracking()
            join candidateOrganization in dbContext.Organizations.AsNoTracking()
                on conversation.OrganizationId equals (Guid?)candidateOrganization.Id into organizations
            from organization in organizations.DefaultIfEmpty()
            select new { conversation, organization };
        if (query.OrganizationId.HasValue)
        {
            rows = rows.Where(item => item.conversation.OrganizationId == query.OrganizationId.Value);
        }

        if (query.Type.HasValue)
        {
            rows = rows.Where(item => item.conversation.Type == query.Type.Value);
        }

        if (query.Scope.HasValue)
        {
            rows = rows.Where(item => item.conversation.Scope == query.Scope.Value);
        }

        if (query.Visibility.HasValue)
        {
            rows = rows.Where(item => item.conversation.Visibility == query.Visibility.Value);
        }

        if (query.Status.HasValue)
        {
            rows = rows.Where(item => item.conversation.Status == query.Status.Value);
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(item => item.conversation.CreatedAtUtc)
            .ThenBy(item => item.conversation.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new PlatformConversationMetadataItem(
                item.conversation.Id,
                item.conversation.Type,
                item.conversation.Scope,
                item.organization == null
                    ? null
                    : new PlatformOrganizationContext(item.organization.Id, item.organization.Name),
                item.conversation.Visibility,
                item.conversation.Status,
                dbContext.ConversationParticipants.Count(participant =>
                    participant.ConversationId == item.conversation.Id &&
                    participant.Status == ConversationParticipantStatus.Active &&
                    participant.EndedAtUtc == null),
                dbContext.ModerationCases.Count(moderationCase =>
                    moderationCase.ConversationId == item.conversation.Id),
                item.conversation.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new PlatformConversationMetadataPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformModerationCasePage> GetModerationCasesAsync(
        PlatformModerationCaseQuery query,
        CancellationToken cancellationToken)
    {
        var rows =
            from moderationCase in dbContext.ModerationCases.AsNoTracking()
            join conversation in dbContext.Conversations.AsNoTracking()
                on moderationCase.ConversationId equals conversation.Id
            join candidateOrganization in dbContext.Organizations.AsNoTracking()
                on conversation.OrganizationId equals (Guid?)candidateOrganization.Id into organizations
            from organization in organizations.DefaultIfEmpty()
            where moderationCase.QueueScope == ModerationQueueScope.PlatformSafety
            select new { moderationCase, conversation, organization };
        if (query.Status.HasValue)
        {
            rows = rows.Where(item => item.moderationCase.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            rows = rows.Where(item => item.moderationCase.Priority == query.Priority.Value);
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var rawItems = await rows.OrderByDescending(item => item.moderationCase.Priority)
            .ThenBy(item => item.moderationCase.CreatedAtUtc)
            .ThenBy(item => item.moderationCase.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new
            {
                Case = item.moderationCase,
                OrganizationId = (Guid?)item.organization!.Id,
                OrganizationName = item.organization == null ? null : item.organization.Name
            })
            .ToArrayAsync(cancellationToken);
        var items = rawItems.Select(item => MapCase(
            item.Case,
            item.OrganizationId.HasValue
                ? new PlatformOrganizationContext(item.OrganizationId.Value, item.OrganizationName!)
                : null)).ToArray();
        return new PlatformModerationCasePage(items, query.Page, query.PageSize, totalCount);
    }

    public Task<PlatformModerationCaseDetail?> GetModerationCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken) =>
        LoadModerationCaseAsync(caseId, cancellationToken);

    public async Task<PlatformModerationCaseDetail> StartModerationReviewAsync(
        PlatformActorContext actor,
        Guid caseId,
        byte[] expectedVersion,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        await RequireReviewerAsync(actor, false, cancellationToken);
        var moderationCase = await LockPlatformSafetyCaseAsync(caseId, cancellationToken)
            ?? throw ModerationNotFound();
        if (await CaseSubjectAsync(moderationCase, cancellationToken) == actor.UserId) throw ModerationNotFound();
        EnsureVersion(moderationCase, expectedVersion);
        try
        {
            moderationCase.StartReview(actor.UserId, actor.OccurredAtUtc);
            if (moderationCase.AbuseReportId.HasValue)
            {
                var report = await dbContext.AbuseReports.SingleAsync(
                    item => item.Id == moderationCase.AbuseReportId.Value,
                    cancellationToken);
                if (report.Status != AbuseReportStatus.UnderReview) report.MarkUnderReview();
            }
        }
        catch (InvalidOperationException)
        {
            throw ModerationConflict();
        }

        dbContext.ModerationActions.Add(ModerationAction.Create(
            moderationCase.Id,
            ModerationActionType.ReviewStarted,
            actor.UserId,
            actor.OccurredAtUtc));
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.moderation.review.start",
            moderationCase,
            reason,
            "Pending",
            "InReview",
            cancellationToken));
        await SaveModerationAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadRequiredModerationCaseAsync(caseId, cancellationToken);
    }

    public async Task<PlatformModerationCaseDetail> CompleteModerationAsync(
        PlatformActorContext actor,
        Guid caseId,
        byte[] expectedVersion,
        string reason,
        string? internalNote,
        bool dismiss,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        await RequireReviewerAsync(actor, false, cancellationToken);
        var moderationCase = await LockPlatformSafetyCaseAsync(caseId, cancellationToken)
            ?? throw ModerationNotFound();
        if (await CaseSubjectAsync(moderationCase, cancellationToken) == actor.UserId) throw ModerationNotFound();
        EnsureVersion(moderationCase, expectedVersion);
        try
        {
            if (dismiss)
            {
                moderationCase.Dismiss(actor.UserId, internalNote, actor.OccurredAtUtc);
            }
            else
            {
                moderationCase.Resolve(actor.UserId, internalNote, actor.OccurredAtUtc);
            }

            if (moderationCase.AbuseReportId.HasValue)
            {
                var report = await dbContext.AbuseReports.SingleAsync(
                    item => item.Id == moderationCase.AbuseReportId.Value,
                    cancellationToken);
                if (dismiss)
                {
                    report.Dismiss();
                }
                else
                {
                    report.Resolve();
                }
            }
            else
            {
                var incident = await dbContext.SafetyIncidents.SingleAsync(
                    item => item.Id == moderationCase.SafetyIncidentId!.Value,
                    cancellationToken);
                if (dismiss)
                {
                    incident.Dismiss(actor.OccurredAtUtc);
                }
                else
                {
                    incident.Resolve(actor.OccurredAtUtc);
                }
            }
        }
        catch (InvalidOperationException)
        {
            throw ModerationConflict();
        }

        var actionType = dismiss ? ModerationActionType.Dismissed : ModerationActionType.Resolved;
        dbContext.ModerationActions.Add(ModerationAction.Create(
            moderationCase.Id,
            actionType,
            actor.UserId,
            actor.OccurredAtUtc));
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            dismiss ? "platform.moderation.dismiss" : "platform.moderation.resolve",
            moderationCase,
            reason,
            "InReview",
            dismiss ? "Dismissed" : "Resolved",
            cancellationToken));
        await SaveModerationAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadRequiredModerationCaseAsync(caseId, cancellationToken);
    }

    public async Task<PlatformModerationCaseDetail> EnforceModerationAsync(
        PlatformActorContext actor,
        Guid caseId,
        byte[] expectedVersion,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        await RequireReviewerAsync(actor, true, cancellationToken);
        var moderationCase = await LockPlatformSafetyCaseAsync(caseId, cancellationToken)
            ?? throw ModerationNotFound();
        if (await CaseSubjectAsync(moderationCase, cancellationToken) == actor.UserId) throw ModerationNotFound();
        EnsureVersion(moderationCase, expectedVersion);
        if (!moderationCase.AbuseReportId.HasValue)
        {
            throw ModerationConflict();
        }

        var report = await dbContext.AbuseReports.SingleAsync(
            item => item.Id == moderationCase.AbuseReportId.Value,
            cancellationToken);
        var message = await dbContext.Messages
            .FromSqlInterpolated($"SELECT * FROM [messaging].[Messages] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {report.MessageId} AND [ConversationId] = {moderationCase.ConversationId}")
            .SingleOrDefaultAsync(cancellationToken) ?? throw ModerationNotFound();
        try
        {
            message.Delete(actor.UserId, actor.OccurredAtUtc);
            moderationCase.Resolve(actor.UserId, null, actor.OccurredAtUtc);
            report.Resolve();
        }
        catch (InvalidOperationException)
        {
            throw ModerationConflict();
        }

        dbContext.ModerationActions.Add(ModerationAction.Create(
            moderationCase.Id,
            ModerationActionType.ContentRemoved,
            actor.UserId,
            actor.OccurredAtUtc));
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.moderation.content.remove",
            moderationCase,
            reason,
            "InReview;ContentPresent",
            "Resolved;ContentRemoved",
            cancellationToken));
        await SaveModerationAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadRequiredModerationCaseAsync(caseId, cancellationToken);
    }

    public Task<FileAsset?> GetModerationEvidenceFileAsync(
        Guid caseId,
        Guid fileAssetId,
        CancellationToken cancellationToken) =>
        (
            from moderationCase in dbContext.ModerationCases.AsNoTracking()
            join evidence in dbContext.ModerationEvidenceAttachments.AsNoTracking()
                on moderationCase.Id equals evidence.ModerationCaseId
            join file in dbContext.FileAssets.AsNoTracking()
                on evidence.FileAssetId equals file.Id
            where moderationCase.Id == caseId &&
                  moderationCase.QueueScope == ModerationQueueScope.PlatformSafety &&
                  file.Id == fileAssetId
            select file)
        .SingleOrDefaultAsync(cancellationToken);

    private async Task<PlatformModerationCaseDetail?> LoadModerationCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from moderationCase in dbContext.ModerationCases.AsNoTracking()
            join conversation in dbContext.Conversations.AsNoTracking()
                on moderationCase.ConversationId equals conversation.Id
            join candidateOrganization in dbContext.Organizations.AsNoTracking()
                on conversation.OrganizationId equals (Guid?)candidateOrganization.Id into organizations
            from organization in organizations.DefaultIfEmpty()
            where moderationCase.Id == caseId &&
                  moderationCase.QueueScope == ModerationQueueScope.PlatformSafety
            select new
            {
                Case = moderationCase,
                OrganizationId = (Guid?)organization!.Id,
                OrganizationName = organization == null ? null : organization.Name
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        PlatformAbuseReportEvidence? abuseEvidence = null;
        PlatformSafetyIncidentEvidence? incidentEvidence = null;
        if (row.Case.AbuseReportId.HasValue)
        {
            abuseEvidence = await dbContext.AbuseReports.AsNoTracking()
                .Where(report => report.Id == row.Case.AbuseReportId.Value)
                .Select(report => new PlatformAbuseReportEvidence(
                    report.Id,
                    report.MessageId,
                    report.ReporterUserId,
                    report.ReportedUserId,
                    report.Reason,
                    report.Description,
                    report.ReportedMessageKind,
                    report.ReportedContentSnapshot))
                .SingleAsync(cancellationToken);
        }
        else
        {
            incidentEvidence = await dbContext.SafetyIncidents.AsNoTracking()
                .Where(incident => incident.Id == row.Case.SafetyIncidentId!.Value)
                .Select(incident => new PlatformSafetyIncidentEvidence(
                    incident.Id,
                    incident.ActorUserId,
                    incident.ClientMessageId,
                    incident.ContentKind,
                    incident.Decision,
                    incident.RiskLevel,
                    incident.PolicyCodes,
                    incident.ContentSnapshot))
                .SingleAsync(cancellationToken);
        }

        var attachments = await (
            from evidence in dbContext.ModerationEvidenceAttachments.AsNoTracking()
            join file in dbContext.FileAssets.AsNoTracking() on evidence.FileAssetId equals file.Id
            where evidence.ModerationCaseId == caseId
            orderby evidence.CreatedAtUtc, evidence.Id
            select new PlatformModerationEvidenceItem(
                file.Id,
                file.OriginalFileName,
                file.ContentType,
                file.SizeBytes))
            .ToArrayAsync(cancellationToken);
        var actions = await dbContext.ModerationActions.AsNoTracking()
            .Where(action => action.ModerationCaseId == caseId)
            .OrderBy(action => action.CreatedAtUtc)
            .ThenBy(action => action.Id)
            .Select(action => new PlatformModerationActionItem(
                action.Type,
                action.ActorUserId,
                action.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        var organizationContext = row.OrganizationId.HasValue
            ? new PlatformOrganizationContext(row.OrganizationId.Value, row.OrganizationName!)
            : null;
        return new PlatformModerationCaseDetail(
            MapCase(row.Case, organizationContext),
            row.Case.ReviewStartedAtUtc,
            row.Case.ResolvedAtUtc,
            row.Case.DismissedAtUtc,
            abuseEvidence,
            incidentEvidence,
            attachments,
            actions);
    }

    private async Task<PlatformModerationCaseDetail> LoadRequiredModerationCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken) =>
        await LoadModerationCaseAsync(caseId, cancellationToken) ?? throw ModerationNotFound();

    private static PlatformModerationCaseItem MapCase(
        ModerationCase moderationCase,
        PlatformOrganizationContext? organization) => new(
        moderationCase.Id,
        moderationCase.ConversationId,
        organization,
        moderationCase.Source,
        moderationCase.Priority,
        moderationCase.Status,
        moderationCase.AssignedReviewerUserId,
        moderationCase.CreatedAtUtc,
        moderationCase.UpdatedAtUtc,
        Convert.ToBase64String(moderationCase.RowVersion));

    private Task<ModerationCase?> LockPlatformSafetyCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken) =>
        dbContext.ModerationCases
            .FromSqlInterpolated($"SELECT * FROM [messaging].[ModerationCases] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {caseId} AND [QueueScope] = 2")
            .SingleOrDefaultAsync(cancellationToken);

    private void EnsureVersion(ModerationCase moderationCase, byte[] expectedVersion)
    {
        if (!CryptographicOperations.FixedTimeEquals(moderationCase.RowVersion, expectedVersion))
        {
            throw ModerationConflict();
        }

        dbContext.Entry(moderationCase).Property(item => item.RowVersion).OriginalValue = expectedVersion;
    }

    private async Task<AdminAuditLog> CreateAuditAsync(
        PlatformActorContext actor,
        string action,
        ModerationCase moderationCase,
        string reason,
        string before,
        string after,
        CancellationToken cancellationToken)
    {
        var personId = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == actor.UserId)
            .Select(user => user.PersonId)
            .SingleAsync(cancellationToken);
        var organizationId = await dbContext.Conversations.AsNoTracking()
            .Where(conversation => conversation.Id == moderationCase.ConversationId)
            .Select(conversation => conversation.OrganizationId)
            .SingleAsync(cancellationToken);
        return AdminAuditLog.Create(
            actor.UserId,
            personId,
            actor.SessionId,
            action,
            "ModerationCase",
            moderationCase.Id.ToString("D"),
            organizationId,
            reason,
            actor.OccurredAtUtc,
            actor.CorrelationId,
            AdminAuditResult.Succeeded,
            before,
            after);
    }

    private async Task SaveModerationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ModerationConflict();
        }
    }

    private Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginSerializableAsync(
        CancellationToken cancellationToken) =>
        dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

    private static PlatformException ModerationNotFound() => new(
        PlatformErrorCodes.ModerationCaseNotFound,
        "پرونده تعدیل پیدا نشد.");

    private static PlatformException ModerationConflict() => new(
        PlatformErrorCodes.ModerationStateConflict,
        "وضعیت پرونده تعدیل قابل تغییر نیست یا هم‌زمان تغییر کرده است.");
}
