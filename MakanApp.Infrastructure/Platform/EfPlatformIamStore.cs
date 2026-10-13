using System.Data;
using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed class EfPlatformIamStore(MakanDbContext dbContext) : IPlatformIamStore
{
    public async Task<PlatformAuthorizationSnapshot?> GetAuthorizationSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var identity = await (
            from user in dbContext.Users.AsNoTracking()
            join person in dbContext.Persons.AsNoTracking()
                on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            where user.Id == userId
            select new
            {
                user.Id,
                user.PersonId,
                user.Username,
                PersonDisplayName = person == null ? null : person.DisplayName,
                FirstName = person == null ? null : person.FirstName,
                LastName = person == null ? null : person.LastName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (identity is null)
        {
            return null;
        }

        var roleKeys = await (
            from assignment in dbContext.PlatformRoleAssignments.AsNoTracking()
            join role in dbContext.PlatformRoles.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.UserId == userId &&
                  assignment.Status == PlatformRoleAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null
            orderby role.Key
            select role.Key)
            .ToArrayAsync(cancellationToken);
        if (roleKeys.Length == 0)
        {
            return null;
        }

        var permissionKeys = await (
            from assignment in dbContext.PlatformRoleAssignments.AsNoTracking()
            join mapping in dbContext.PlatformRolePermissions.AsNoTracking()
                on assignment.RoleId equals mapping.RoleId
            join permission in dbContext.PlatformPermissions.AsNoTracking()
                on mapping.PermissionId equals permission.Id
            where assignment.UserId == userId &&
                  assignment.Status == PlatformRoleAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null
            orderby permission.Key
            select permission.Key)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return new PlatformAuthorizationSnapshot(
            identity.Id,
            identity.PersonId,
            BuildDisplayName(
                identity.PersonDisplayName,
                identity.FirstName,
                identity.LastName,
                identity.Username,
                identity.Id),
            roleKeys,
            permissionKeys);
    }

    public async Task<IReadOnlyCollection<PlatformRoleCatalogItem>> GetRoleCatalogAsync(
        CancellationToken cancellationToken)
    {
        var roles = await dbContext.PlatformRoles.AsNoTracking()
            .OrderBy(role => role.Key)
            .Select(role => new { role.Id, role.Key, role.DisplayName })
            .ToArrayAsync(cancellationToken);
        var permissions = await (
            from mapping in dbContext.PlatformRolePermissions.AsNoTracking()
            join permission in dbContext.PlatformPermissions.AsNoTracking()
                on mapping.PermissionId equals permission.Id
            orderby permission.Key
            select new { mapping.RoleId, permission.Key })
            .ToArrayAsync(cancellationToken);

        return roles.Select(role => new PlatformRoleCatalogItem(
            role.Id,
            role.Key,
            role.DisplayName,
            permissions
                .Where(permission => permission.RoleId == role.Id)
                .Select(permission => permission.Key)
                .ToArray()))
            .ToArray();
    }

    public async Task<PlatformRoleAssignmentPage> GetAssignmentsAsync(
        PlatformRoleAssignmentQuery query,
        CancellationToken cancellationToken)
    {
        var source = AssignmentQuery(
            null,
            query.UserId,
            query.RoleKey,
            query.ActiveOnly);

        var totalCount = await source.CountAsync(cancellationToken);
        var records = await source
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        return new PlatformRoleAssignmentPage(
            records.Select(MapAssignment).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<AdminAuditLogPage> GetAuditLogsAsync(
        AdminAuditQuerySpec query,
        CancellationToken cancellationToken)
    {
        var source = dbContext.AdminAuditLogs.AsNoTracking().AsQueryable();
        if (query.ActorUserId.HasValue)
        {
            source = source.Where(item => item.ActorUserId == query.ActorUserId.Value);
        }

        if (query.Action is not null)
        {
            source = source.Where(item => item.Action == query.Action);
        }

        if (query.TargetType is not null)
        {
            source = source.Where(item => item.TargetType == query.TargetType);
        }

        if (query.TargetId is not null)
        {
            source = source.Where(item => item.TargetId == query.TargetId);
        }

        if (query.Result.HasValue)
        {
            var result = (AdminAuditResult)query.Result.Value;
            source = source.Where(item => item.Result == result);
        }

        if (query.FromUtc.HasValue)
        {
            source = source.Where(item => item.OccurredAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            source = source.Where(item => item.OccurredAtUtc <= query.ToUtc.Value);
        }

        var totalCount = await source.CountAsync(cancellationToken);
        var ordered = query.SortField switch
        {
            AdminAuditSortField.Action when query.Descending => source
                .OrderByDescending(item => item.Action)
                .ThenByDescending(item => item.OccurredAtUtc),
            AdminAuditSortField.Action => source
                .OrderBy(item => item.Action)
                .ThenByDescending(item => item.OccurredAtUtc),
            AdminAuditSortField.Result when query.Descending => source
                .OrderByDescending(item => item.Result)
                .ThenByDescending(item => item.OccurredAtUtc),
            AdminAuditSortField.Result => source
                .OrderBy(item => item.Result)
                .ThenByDescending(item => item.OccurredAtUtc),
            _ when query.Descending => source
                .OrderByDescending(item => item.OccurredAtUtc),
            _ => source.OrderBy(item => item.OccurredAtUtc)
        };
        var items = await ordered
            .ThenBy(item => item.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new AdminAuditLogItem(
                item.Id,
                item.ActorUserId,
                item.ActorPersonId,
                item.ActorSessionId,
                item.Action,
                item.TargetType,
                item.TargetId,
                item.OrganizationId,
                item.Reason,
                item.OccurredAtUtc,
                item.CorrelationId,
                (int)item.Result,
                item.BeforeSummary,
                item.AfterSummary))
            .ToArrayAsync(cancellationToken);
        return new AdminAuditLogPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformBootstrapResult> BootstrapOwnerAsync(
        Guid userId,
        DateTime occurredAtUtc,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await LockRoleAsync(PlatformCatalogIds.PlatformOwnerRole, cancellationToken);
        await EnsureUserExistsAsync(userId, cancellationToken);
        var activeOwners = await LockActiveAssignmentsAsync(
            PlatformCatalogIds.PlatformOwnerRole,
            cancellationToken);
        if (activeOwners.Count > 0)
        {
            var replay = activeOwners.SingleOrDefault(item => item.UserId == userId);
            if (replay is null)
            {
                throw Error(
                    PlatformErrorCodes.BootstrapClosed,
                    "مسیر bootstrap پس از ایجاد اولین PlatformOwner بسته است.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new PlatformBootstrapResult(
                await GetAssignmentItemAsync(replay.Id, cancellationToken),
                true);
        }

        var assignment = PlatformRoleAssignment.CreateActive(
            userId,
            PlatformCatalogIds.PlatformOwnerRole,
            null,
            occurredAtUtc);
        dbContext.PlatformRoleAssignments.Add(assignment);
        dbContext.AdminAuditLogs.Add(AdminAuditLog.Create(
            null,
            null,
            null,
            "platform.owner.bootstrap",
            "PlatformRoleAssignment",
            assignment.Id.ToString("D"),
            null,
            "Initial PlatformOwner bootstrap",
            occurredAtUtc,
            correlationId,
            AdminAuditResult.Succeeded,
            null,
            $"userId={userId:D};role={PlatformRoleKeys.PlatformOwner};status=Active"));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlatformBootstrapResult(
            await GetAssignmentItemAsync(assignment.Id, cancellationToken),
            false);
    }

    public async Task<PlatformRoleMutationResult> GrantRoleAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid targetUserId,
        string roleKey,
        string reason,
        DateTime occurredAtUtc,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var role = await dbContext.PlatformRoles
            .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformRoles] WITH (UPDLOCK, HOLDLOCK) WHERE [Key] = {roleKey}")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error(PlatformErrorCodes.RoleNotFound, "نقش پلتفرم یافت نشد.");
        await EnsureUserExistsAsync(targetUserId, cancellationToken);
        var existing = await dbContext.PlatformRoleAssignments
            .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformRoleAssignments] WITH (UPDLOCK, HOLDLOCK) WHERE [UserId] = {targetUserId} AND [RoleId] = {role.Id} AND [Status] = 1 AND [EndedAtUtc] IS NULL")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PlatformRoleMutationResult(
                await GetAssignmentItemAsync(existing.Id, cancellationToken),
                true);
        }

        var assignment = PlatformRoleAssignment.CreateActive(
            targetUserId,
            role.Id,
            actorUserId,
            occurredAtUtc);
        dbContext.PlatformRoleAssignments.Add(assignment);
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actorUserId,
            actorSessionId,
            "platform.role.grant",
            assignment.Id,
            reason,
            occurredAtUtc,
            correlationId,
            null,
            $"userId={targetUserId:D};role={role.Key};status=Active",
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlatformRoleMutationResult(
            await GetAssignmentItemAsync(assignment.Id, cancellationToken),
            false);
    }

    public async Task<PlatformRoleMutationResult> EndRoleAssignmentAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid assignmentId,
        string reason,
        DateTime occurredAtUtc,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var roleId = await dbContext.PlatformRoleAssignments.AsNoTracking()
            .Where(item => item.Id == assignmentId)
            .Select(item => (Guid?)item.RoleId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error(PlatformErrorCodes.AssignmentNotFound, "انتساب نقش پلتفرم یافت نشد.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var role = await LockRoleAsync(roleId, cancellationToken);
        var assignment = await dbContext.PlatformRoleAssignments
            .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformRoleAssignments] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {assignmentId}")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error(PlatformErrorCodes.AssignmentNotFound, "انتساب نقش پلتفرم یافت نشد.");
        if (!assignment.IsActive)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PlatformRoleMutationResult(
                await GetAssignmentItemAsync(assignment.Id, cancellationToken),
                true);
        }

        if (role.Key == PlatformRoleKeys.PlatformOwner)
        {
            var activeOwners = await LockActiveAssignmentsAsync(role.Id, cancellationToken);
            if (activeOwners.Count <= 1)
            {
                throw Error(
                    PlatformErrorCodes.LastOwnerRequired,
                    "آخرین PlatformOwner فعال قابل حذف نیست.");
            }
        }

        var before = $"userId={assignment.UserId:D};role={role.Key};status=Active";
        assignment.End(actorUserId, occurredAtUtc);
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actorUserId,
            actorSessionId,
            "platform.role.revoke",
            assignment.Id,
            reason,
            occurredAtUtc,
            correlationId,
            before,
            $"userId={assignment.UserId:D};role={role.Key};status=Ended",
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlatformRoleMutationResult(
            await GetAssignmentItemAsync(assignment.Id, cancellationToken),
            false);
    }

    private IQueryable<PlatformAssignmentProjection> AssignmentQuery(
        Guid? assignmentId,
        Guid? userId,
        string? roleKey,
        bool activeOnly) =>
        from assignment in dbContext.PlatformRoleAssignments.AsNoTracking()
        join role in dbContext.PlatformRoles.AsNoTracking() on assignment.RoleId equals role.Id
        join user in dbContext.Users.AsNoTracking() on assignment.UserId equals user.Id
        join person in dbContext.Persons.AsNoTracking()
            on user.PersonId equals (Guid?)person.Id into people
        from person in people.DefaultIfEmpty()
        where (!assignmentId.HasValue || assignment.Id == assignmentId.Value) &&
              (!userId.HasValue || assignment.UserId == userId.Value) &&
              (roleKey == null || role.Key == roleKey) &&
              (!activeOnly ||
               assignment.Status == PlatformRoleAssignmentStatus.Active &&
               assignment.EndedAtUtc == null)
        orderby assignment.GrantedAtUtc descending, assignment.Id
        select new PlatformAssignmentProjection(
            assignment.Id,
            assignment.UserId,
            user.PersonId,
            person == null ? null : person.DisplayName,
            person == null ? null : person.FirstName,
            person == null ? null : person.LastName,
            user.Username,
            role.Id,
            role.Key,
            role.DisplayName,
            assignment.Status,
            assignment.GrantedAtUtc,
            assignment.EndedAtUtc,
            assignment.GrantedByUserId,
            assignment.EndedByUserId,
            assignment.RowVersion);

    private async Task<PlatformRoleAssignmentItem> GetAssignmentItemAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var record = await AssignmentQuery(assignmentId, null, null, false)
            .SingleAsync(cancellationToken);
        return MapAssignment(record);
    }

    private async Task<PlatformRole> LockRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken) =>
        await dbContext.PlatformRoles
            .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformRoles] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {roleId}")
            .SingleAsync(cancellationToken);

    private async Task<List<PlatformRoleAssignment>> LockActiveAssignmentsAsync(
        Guid roleId,
        CancellationToken cancellationToken) =>
        await dbContext.PlatformRoleAssignments
            .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformRoleAssignments] WITH (UPDLOCK, HOLDLOCK) WHERE [RoleId] = {roleId} AND [Status] = 1 AND [EndedAtUtc] IS NULL")
            .ToListAsync(cancellationToken);

    private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            throw Error(PlatformErrorCodes.UserNotFound, "کاربر هدف یافت نشد.");
        }
    }

    private async Task<AdminAuditLog> CreateAuditAsync(
        Guid actorUserId,
        Guid actorSessionId,
        string action,
        Guid targetId,
        string reason,
        DateTime occurredAtUtc,
        string correlationId,
        string? beforeSummary,
        string? afterSummary,
        CancellationToken cancellationToken)
    {
        var actorPersonId = await dbContext.Users
            .Where(user => user.Id == actorUserId)
            .Select(user => user.PersonId)
            .SingleAsync(cancellationToken);
        return AdminAuditLog.Create(
            actorUserId,
            actorPersonId,
            actorSessionId,
            action,
            "PlatformRoleAssignment",
            targetId.ToString("D"),
            null,
            reason,
            occurredAtUtc,
            correlationId,
            AdminAuditResult.Succeeded,
            beforeSummary,
            afterSummary);
    }

    private static PlatformRoleAssignmentItem MapAssignment(PlatformAssignmentProjection item) => new(
        item.AssignmentId,
        item.UserId,
        item.PersonId,
        BuildDisplayName(
            item.PersonDisplayName,
            item.FirstName,
            item.LastName,
            item.Username,
            item.UserId),
        item.RoleId,
        item.RoleKey,
        item.RoleDisplayName,
        item.Status == PlatformRoleAssignmentStatus.Active && !item.EndedAtUtc.HasValue,
        item.GrantedAtUtc,
        item.EndedAtUtc,
        item.GrantedByUserId,
        item.EndedByUserId,
        Convert.ToBase64String(item.RowVersion));

    private static string BuildDisplayName(
        string? displayName,
        string? firstName,
        string? lastName,
        string? username,
        Guid userId)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        var fullName = $"{firstName} {lastName}".Trim();
        return !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : !string.IsNullOrWhiteSpace(username)
                ? username
                : userId.ToString("D");
    }

    private static PlatformException Error(string code, string message) => new(code, message);

    private sealed record PlatformAssignmentProjection(
        Guid AssignmentId,
        Guid UserId,
        Guid? PersonId,
        string? PersonDisplayName,
        string? FirstName,
        string? LastName,
        string? Username,
        Guid RoleId,
        string RoleKey,
        string RoleDisplayName,
        PlatformRoleAssignmentStatus Status,
        DateTime GrantedAtUtc,
        DateTime? EndedAtUtc,
        Guid? GrantedByUserId,
        Guid? EndedByUserId,
        byte[] RowVersion);
}
