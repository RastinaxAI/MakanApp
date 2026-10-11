using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Dashboards;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Reports.Dashboards;

public sealed partial class EfDashboardReadService
{
    public async Task<TeacherDashboardDto> ReadTeacherAsync(
        AccessContext accessContext,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken)
    {
        var organizationId = accessContext.OrganizationId!.Value;
        var membershipId = accessContext.MembershipId!.Value;
        var classes = await (
            from assignment in _dbContext.TeacherAssignments
            join academicClass in _dbContext.Classes
                on new { assignment.OrganizationId, Id = assignment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join course in _dbContext.Courses
                on new { academicClass.OrganizationId, Id = academicClass.CourseId }
                equals new { course.OrganizationId, course.Id }
            join period in _dbContext.AcademicPeriods
                on new { academicClass.OrganizationId, Id = academicClass.AcademicPeriodId }
                equals new { period.OrganizationId, period.Id }
            let activeLearnerCount = _dbContext.Enrollments.Count(enrollment =>
                enrollment.OrganizationId == organizationId &&
                enrollment.ClassId == academicClass.Id &&
                enrollment.Status == EnrollmentStatus.Active &&
                enrollment.EndedAtUtc == null)
            where assignment.OrganizationId == organizationId &&
                  assignment.TeacherMembershipId == membershipId &&
                  assignment.Status == TeacherAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null &&
                  academicClass.Status == ClassStatus.Active &&
                  academicClass.EndedAtUtc == null
            orderby academicClass.Title, academicClass.Id
            select new DashboardClassItem(
                academicClass.Id,
                academicClass.Title,
                course.Title,
                period.Title,
                activeLearnerCount))
        .AsNoTracking()
        .Take(ClassLimit)
        .ToArrayAsync(cancellationToken);

        var sessions = await (
            from session in _dbContext.AcademicSessions
            join academicClass in _dbContext.Classes
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join assignment in _dbContext.TeacherAssignments
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { assignment.OrganizationId, Id = assignment.ClassId }
            where session.OrganizationId == organizationId &&
                  assignment.TeacherMembershipId == membershipId &&
                  assignment.Status == TeacherAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null &&
                  academicClass.Status == ClassStatus.Active &&
                  academicClass.EndedAtUtc == null &&
                  session.Status == SessionStatus.Scheduled &&
                  session.StartUtc >= dataAsOfUtc
            orderby session.StartUtc, session.Id
            select new DashboardSessionItem(
                session.Id,
                session.ClassId,
                academicClass.Title,
                session.Title,
                session.StartUtc,
                session.EndUtc,
                session.TimeZoneId,
                session.MeetingUrl,
                session.Status))
        .AsNoTracking()
        .Take(SessionLimit)
        .ToArrayAsync(cancellationToken);

        return new TeacherDashboardDto(
            dataAsOfUtc,
            organizationId,
            DashboardAudience.Teacher,
            classes,
            sessions,
            await ReadAttendancePendingAsync(
                organizationId,
                membershipId,
                dataAsOfUtc,
                cancellationToken),
            await ReadAssignmentQueueAsync(organizationId, membershipId, cancellationToken),
            await ReadExamQueueAsync(organizationId, membershipId, cancellationToken));
    }

    public async Task<ManagerDashboardDto> ReadManagerAsync(
        AccessContext accessContext,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken)
    {
        var organizationId = accessContext.OrganizationId!.Value;
        var activeClassCount = await _dbContext.Classes.CountAsync(
            academicClass =>
                academicClass.OrganizationId == organizationId &&
                academicClass.Status == ClassStatus.Active &&
                academicClass.EndedAtUtc == null,
            cancellationToken);
        var learnerCount = await (
            from enrollment in _dbContext.Enrollments
            join organizationPerson in _dbContext.OrganizationPersons
                on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            where enrollment.OrganizationId == organizationId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            select organizationPerson.Id)
        .Distinct()
        .CountAsync(cancellationToken);
        var teacherCount = await (
            from membership in _dbContext.Memberships
            join role in _dbContext.RoleAssignments on membership.Id equals role.MembershipId
            where membership.OrganizationId == organizationId &&
                  membership.Status == MembershipStatus.Active &&
                  membership.EndedAtUtc == null &&
                  role.Role == OrganizationRole.Teacher &&
                  role.Status == RoleAssignmentStatus.Active &&
                  role.EndedAtUtc == null
            select membership.Id)
        .Distinct()
        .CountAsync(cancellationToken);

        var organizationExists = await _dbContext.Organizations.AnyAsync(
            organization =>
                organization.Id == organizationId &&
                organization.Status == OrganizationStatus.Active,
            cancellationToken);
        var hasAcademicPeriod = await _dbContext.AcademicPeriods.AnyAsync(
            period => period.OrganizationId == organizationId,
            cancellationToken);
        var hasCourse = await _dbContext.Courses.AnyAsync(
            course => course.OrganizationId == organizationId,
            cancellationToken);
        var hasClass = await _dbContext.Classes.AnyAsync(
            academicClass => academicClass.OrganizationId == organizationId,
            cancellationToken);

        var sessionsQuery =
            from session in _dbContext.AcademicSessions
            join academicClass in _dbContext.Classes
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            where session.OrganizationId == organizationId &&
                  academicClass.Status == ClassStatus.Active &&
                  academicClass.EndedAtUtc == null &&
                  session.Status == SessionStatus.Scheduled &&
                  session.StartUtc >= dataAsOfUtc
            orderby session.StartUtc, session.Id
            select new { Session = session, ClassTitle = academicClass.Title };
        var upcomingSessions = await sessionsQuery
            .Select(item => new DashboardSessionItem(
                item.Session.Id,
                item.Session.ClassId,
                item.ClassTitle,
                item.Session.Title,
                item.Session.StartUtc,
                item.Session.EndUtc,
                item.Session.TimeZoneId,
                item.Session.MeetingUrl,
                item.Session.Status))
            .AsNoTracking()
            .Take(SessionLimit)
            .ToArrayAsync(cancellationToken);
        var missingMeetingLinks = await sessionsQuery
            .Where(item => item.Session.MeetingUrl == null)
            .Select(item => new DashboardSessionItem(
                item.Session.Id,
                item.Session.ClassId,
                item.ClassTitle,
                item.Session.Title,
                item.Session.StartUtc,
                item.Session.EndUtc,
                item.Session.TimeZoneId,
                item.Session.MeetingUrl,
                item.Session.Status))
            .AsNoTracking()
            .Take(SessionLimit)
            .ToArrayAsync(cancellationToken);
        var pendingInvitations = await _dbContext.Invitations.CountAsync(
            invitation =>
                invitation.OrganizationId == organizationId &&
                invitation.Status == InvitationStatus.Pending &&
                invitation.ExpiresAtUtc > dataAsOfUtc,
            cancellationToken);

        return new ManagerDashboardDto(
            dataAsOfUtc,
            organizationId,
            DashboardAudience.Manager,
            new ManagerOperationalCounts(activeClassCount, learnerCount, teacherCount),
            new ManagerOnboardingChecklist(
                organizationExists,
                hasAcademicPeriod,
                hasCourse,
                teacherCount > 0,
                learnerCount > 0,
                hasClass),
            upcomingSessions,
            missingMeetingLinks,
            await ReadAttendancePendingAsync(
                organizationId,
                null,
                dataAsOfUtc,
                cancellationToken),
            pendingInvitations,
            await ReadAssignmentQueueAsync(organizationId, null, cancellationToken),
            await ReadExamQueueAsync(organizationId, null, cancellationToken));
    }
}
