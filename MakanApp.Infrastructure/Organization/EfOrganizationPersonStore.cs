using System.Data;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MakanApp.Infrastructure.Organization;

public sealed class EfOrganizationPersonStore(MakanDbContext database, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : IOrganizationPersonStore
{
    public async Task<IOrganizationTransaction> BeginAsync(CancellationToken cancellationToken) =>
        new Transaction(await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken));

    public async Task<OrganizationPersonRecord?> GetAsync(Guid organizationId, Guid id, bool forUpdate, CancellationToken cancellationToken)
    {
        var record = forUpdate
            ? await database.OrganizationPersons.FromSqlInterpolated($"SELECT * FROM [organization].[OrganizationPersons] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {id}").SingleOrDefaultAsync(cancellationToken)
            : await database.OrganizationPersons.AsNoTracking().SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.Id == id, cancellationToken);
        if (record is null) return null;
        var person = forUpdate
            ? await database.Persons.FromSqlInterpolated($"SELECT * FROM [identity].[Persons] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {record.PersonId}").SingleAsync(cancellationToken)
            : await database.Persons.AsNoTracking().SingleAsync(item => item.Id == record.PersonId, cancellationToken);
        var linked = await database.Users.AnyAsync(item => item.PersonId == person.Id, cancellationToken);
        // همه ارجاع‌ها، از جمله پرونده پایان‌یافته، در guard مالکیت شمرده می‌شوند.
        var references = await database.OrganizationPersons.CountAsync(item => item.PersonId == person.Id, cancellationToken);
        return new(record, person, linked, references);
    }

    public async Task<OrganizationPersonRecord?> FindCreationAsync(Guid organizationId, Guid operationId, CancellationToken cancellationToken)
    {
        var record = await database.OrganizationPersons.FromSqlInterpolated($"SELECT * FROM [organization].[OrganizationPersons] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [CreationOperationId] = {operationId}")
            .SingleOrDefaultAsync(cancellationToken);
        return record is null ? null : await GetAsync(organizationId, record.Id, true, cancellationToken);
    }

    public async Task<(int Total, IReadOnlyCollection<OrganizationPersonRecord> Items)> ListAsync(
        Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = from record in database.OrganizationPersons.AsNoTracking()
                    join person in database.Persons.AsNoTracking() on record.PersonId equals person.Id
                    where record.OrganizationId == organizationId &&
                          (search == null || search == "" || person.FirstName.Contains(search) ||
                           person.LastName.Contains(search) || person.DisplayName != null && person.DisplayName.Contains(search))
                    select new
                    {
                        Record = record,
                        Person = person,
                        IsAccountLinked = database.Users.Any(user => user.PersonId == person.Id),
                        ReferenceCount = database.OrganizationPersons.Count(other => other.PersonId == person.Id)
                    };
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(item => item.Person.LastName).ThenBy(item => item.Person.FirstName)
            .ThenBy(item => item.Record.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new OrganizationPersonRecord(item.Record, item.Person, item.IsAccountLinked, item.ReferenceCount))
            .ToArrayAsync(cancellationToken);
        return (total, items);
    }

    public Task<bool> HasActiveEnrollmentsAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        database.Enrollments.AnyAsync(item => item.OrganizationId == organizationId &&
            item.LearnerOrganizationPersonId == id && item.Status == EnrollmentStatus.Active && item.EndedAtUtc == null, cancellationToken);
    public Task<User?> GetAvailableUserAsync(Guid userId, CancellationToken cancellationToken) =>
        database.Users.SingleOrDefaultAsync(item => item.Id == userId && item.AccountStatus == UserAccountStatus.Active, cancellationToken);
    public Task<Invitation?> FindLinkOperationAsync(Guid organizationId, Guid operationId, CancellationToken cancellationToken) =>
        database.Invitations.FromSqlInterpolated($"SELECT * FROM [organization].[Invitations] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ClientOperationId] = {operationId}")
            .SingleOrDefaultAsync(cancellationToken);
    public Task<Invitation?> GetLinkInvitationAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        database.Invitations.FromSqlInterpolated($"SELECT * FROM [organization].[Invitations] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {id} AND [OrganizationPersonId] IS NOT NULL")
            .SingleOrDefaultAsync(cancellationToken);
    public Task<Invitation?> GetPendingLinkAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken) =>
        database.Invitations.FromSqlInterpolated($"SELECT * FROM [organization].[Invitations] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [OrganizationPersonId] = {personId} AND [Status] = 1")
            .SingleOrDefaultAsync(cancellationToken);
    public void Add(Person person, OrganizationPerson organizationPerson)
    {
        database.Persons.Add(person);
        database.OrganizationPersons.Add(organizationPerson);
    }
    public void Add(Invitation invitation) => database.Invitations.Add(invitation);
    public async Task AuditAsync(AccessContext actor, string action, string targetType, Guid targetId, string reason,
        DateTime nowUtc, string? before, string? after, CancellationToken cancellationToken)
    {
        var actorPersonId = await database.Users.Where(item => item.Id == actor.UserId)
            .Select(item => item.PersonId).SingleAsync(cancellationToken);
        database.AdminAuditLogs.Add(AdminAuditLog.Create(actor.UserId, actorPersonId, actor.SessionId, action,
            targetType, targetId.ToString("D"), actor.OrganizationId, reason, nowUtc,
            $"school-{Guid.NewGuid():N}", AdminAuditResult.Succeeded, before, after));
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await realtime.CaptureSchoolAsync(cancellationToken); await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new OrganizationException(OrganizationErrorCodes.ConcurrencyConflict, "اطلاعات هم‌زمان تغییر کرده است.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new OrganizationException(SchoolOnboardingErrorCodes.Conflict, "رکورد یا عملیات یکتا قبلاً ثبت شده است.");
        }
    }

    private sealed class Transaction(IDbContextTransaction transaction) : IOrganizationTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
