using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Reports.Detailed;

public sealed partial class EfDetailedReportReadService(MakanDbContext dbContext) : IDetailedReportReadService
{
    private readonly MakanDbContext _dbContext = dbContext;

    private Task<Guid?> GetStudentLearnerIdAsync(AccessContext context, CancellationToken cancellationToken) =>
        (
            from organizationPerson in _dbContext.OrganizationPersons
            join user in _dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where organizationPerson.OrganizationId == context.OrganizationId!.Value &&
                  user.Id == context.UserId &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            select (Guid?)organizationPerson.Id)
        .SingleOrDefaultAsync(cancellationToken);

    private static int Skip(ReportQuerySpec query) => checked((query.PageNumber - 1) * query.PageSize);

    private static ReportPage<T> Page<T>(DateTime dataAsOfUtc, ReportQuerySpec query, int totalCount, IReadOnlyCollection<T> items) =>
        new(dataAsOfUtc, query.PageNumber, query.PageSize, totalCount, items);

    private static AttendanceReportPage<T> AttendancePage<T>(DateTime dataAsOfUtc, ReportQuerySpec query, AttendanceReportSummary summary, IReadOnlyCollection<T> items) =>
        new(dataAsOfUtc, query.PageNumber, query.PageSize, summary.EligibleCount, summary, items);

    private static AttendanceReportSummary EmptyAttendanceSummary { get; } = new(0, 0, 0, 0, 0, 0, 0);

}
