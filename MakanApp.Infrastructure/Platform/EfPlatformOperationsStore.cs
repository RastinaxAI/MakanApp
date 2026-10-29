using System.Data;
using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed class EfPlatformOperationsStore(
    MakanDbContext dbContext,
    TimeProvider timeProvider) : IPlatformOperationsStore
{
    public async Task<PlatformOrganizationPage> GetOrganizationsAsync(
        PlatformOrganizationQuery query,
        CancellationToken cancellationToken)
    {
        var organizations = dbContext.Organizations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            organizations = organizations.Where(item => item.Name.Contains(query.Search));
        }

        if (query.Status.HasValue)
        {
            organizations = organizations.Where(item => item.Status == query.Status.Value);
        }

        var totalCount = await organizations.CountAsync(cancellationToken);
        var descending = query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = query.SortBy.ToLowerInvariant() switch
        {
            "name" => descending
                ? organizations.OrderByDescending(item => item.Name).ThenBy(item => item.Id)
                : organizations.OrderBy(item => item.Name).ThenBy(item => item.Id),
            "status" => descending
                ? organizations.OrderByDescending(item => item.Status).ThenBy(item => item.Id)
                : organizations.OrderBy(item => item.Status).ThenBy(item => item.Id),
            _ => descending
                ? organizations.OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id)
                : organizations.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id)
        };
        var rows = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new OrganizationRow(item.Id, item.Name, item.Status, item.CreatedAtUtc, item.RowVersion))
            .ToArrayAsync(cancellationToken);

        var items = await BuildOrganizationItemsAsync(rows, cancellationToken);
        return new PlatformOrganizationPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformOrganizationDetail?> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations.AsNoTracking()
            .Where(item => item.Id == organizationId)
            .Select(item => new OrganizationRow(item.Id, item.Name, item.Status, item.CreatedAtUtc, item.RowVersion))
            .SingleOrDefaultAsync(cancellationToken);
        return organization is null
            ? null
            : await BuildOrganizationDetailAsync(organization, cancellationToken);
    }

    public async Task<PlatformOrganizationImpactPreview?> GetOrganizationImpactAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Organizations.AsNoTracking().AnyAsync(item => item.Id == organizationId, cancellationToken))
        {
            return null;
        }

        return await BuildImpactAsync(organizationId, cancellationToken);
    }

    public async Task<PlatformProvisioningPage> GetProvisioningAsync(
        PlatformProvisioningQuery query,
        CancellationToken cancellationToken)
    {
        var requests = dbContext.PlatformProvisioningRequests.AsNoTracking();
        if (query.Status.HasValue)
        {
            requests = requests.Where(item => item.Status == query.Status.Value);
        }

        var totalCount = await requests.CountAsync(cancellationToken);
        var rows = await requests
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        return new PlatformProvisioningPage(
            rows.Select(MapProvisioning).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<PlatformProvisioningResult> ProvisionAsync(
        PlatformActorContext actor,
        ProvisionOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        await ValidateProvisioningReferencesAsync(command, cancellationToken);
        var (requestId, replayed) = await GetOrCreateProvisioningRequestAsync(
            actor,
            command,
            cancellationToken);

        try
        {
            await EnsureOrganizationStageAsync(requestId, actor, cancellationToken);
            if (command.PlanVersionId.HasValue)
            {
                await EnsureSubscriptionStageAsync(requestId, actor, cancellationToken);
            }

            if (command.InitialManagerUserId.HasValue)
            {
                await EnsureManagerInvitationStageAsync(requestId, actor, cancellationToken);
            }

            await EnsureProvisioningCompletedAsync(requestId, actor, cancellationToken);
        }
        catch (PlatformException exception)
        {
            await MarkProvisioningNeedsAttentionAsync(requestId, exception.Code, actor, cancellationToken);
            throw;
        }

        var completed = await dbContext.PlatformProvisioningRequests.AsNoTracking()
            .SingleAsync(item => item.Id == requestId, cancellationToken);
        return new PlatformProvisioningResult(MapProvisioning(completed), replayed);
    }

    public async Task<PlatformOrganizationMutationResult> SetOrganizationActiveAsync(
        PlatformActorContext actor,
        Guid organizationId,
        bool active,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var organization = await LockOrganizationAsync(organizationId, cancellationToken)
            ?? throw Error(PlatformErrorCodes.OrganizationNotFound, "سازمان پیدا نشد.");
        var impact = await BuildImpactAsync(organizationId, cancellationToken);
        var replayed = organization.IsActive == active;
        if (!replayed)
        {
            var before = organization.Status.ToString();
            if (active)
            {
                organization.Reactivate();
            }
            else
            {
                organization.Suspend();
            }

            dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
                actor,
                active ? "platform.organization.reactivate" : "platform.organization.suspend",
                "Organization",
                organization.Id.ToString("D"),
                organization.Id,
                reason,
                before,
                organization.Status.ToString(),
                cancellationToken));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var detail = await GetOrganizationAsync(organizationId, cancellationToken)
            ?? throw Error(PlatformErrorCodes.OrganizationNotFound, "سازمان پیدا نشد.");
        return new PlatformOrganizationMutationResult(detail, impact, replayed);
    }

    public async Task<PlatformMembershipPage> GetMembershipsAsync(
        Guid organizationId,
        PlatformMembershipQuery query,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Organizations.AsNoTracking().AnyAsync(item => item.Id == organizationId, cancellationToken))
        {
            throw Error(PlatformErrorCodes.OrganizationNotFound, "سازمان پیدا نشد.");
        }

        var memberships = dbContext.Memberships.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId);
        if (query.ActiveOnly)
        {
            memberships = memberships.Where(item =>
                item.Status == MembershipStatus.Active && item.EndedAtUtc == null);
        }

        var totalCount = await memberships.CountAsync(cancellationToken);
        var rows = await (
            from membership in memberships
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            orderby membership.CreatedAtUtc descending, membership.Id
            select new MembershipRow(
                membership.Id,
                membership.UserId,
                person == null ? null : person.DisplayName,
                person == null ? null : person.FirstName,
                person == null ? null : person.LastName,
                user.Username,
                membership.Status,
                membership.CreatedAtUtc,
                membership.EndedAtUtc,
                membership.RowVersion))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        return new PlatformMembershipPage(
            await BuildMembershipItemsAsync(rows, cancellationToken),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<PlatformInvitationResult> CreateInvitationAsync(
        PlatformActorContext actor,
        Guid organizationId,
        CreatePlatformInvitationCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var organization = await LockOrganizationAsync(organizationId, cancellationToken)
            ?? throw Error(PlatformErrorCodes.OrganizationNotFound, "سازمان پیدا نشد.");
        if (!organization.IsActive)
        {
            throw Error(PlatformErrorCodes.OrganizationConflict, "سازمان معلق است.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == command.UserId, cancellationToken)
            ?? throw Error(PlatformErrorCodes.UserNotFound, "کاربر مقصد پیدا نشد.");
        if (user.AccountStatus != UserAccountStatus.Active)
        {
            throw Error(PlatformErrorCodes.AccountSuspended, "کاربر مقصد معلق است.");
        }

        var existing = await dbContext.Invitations.SingleOrDefaultAsync(item =>
            item.OrganizationId == organizationId && item.DestinationUserId == command.UserId &&
            item.Role == command.Role && item.Status == InvitationStatus.Pending && item.GuardianLearnerOrganizationPersonId == null,
            cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return MapInvitation(existing, true);
        }

        var invitation = Invitation.Create(
            organizationId,
            command.UserId,
            actor.UserId,
            command.Role,
            actor.OccurredAtUtc,
            command.ExpiresAtUtc);
        dbContext.Invitations.Add(invitation);
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.organization.invitation.create",
            "Invitation",
            invitation.Id.ToString("D"),
            organizationId,
            command.Reason,
            null,
            $"user={command.UserId:D};role={command.Role}",
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapInvitation(invitation, false);
    }

    public async Task<PlatformMembershipMutationResult> ChangeMembershipRoleAsync(
        PlatformActorContext actor,
        Guid organizationId,
        Guid membershipId,
        ChangePlatformMembershipRoleCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        _ = await LockOrganizationAsync(organizationId, cancellationToken)
            ?? throw Error(PlatformErrorCodes.OrganizationNotFound, "سازمان پیدا نشد.");
        var membership = await LockMembershipAsync(organizationId, membershipId, cancellationToken)
            ?? throw Error(PlatformErrorCodes.MembershipNotFound, "عضویت پیدا نشد.");
        if (!membership.IsActive)
        {
            throw Error(PlatformErrorCodes.MembershipConflict, "عضویت فعال نیست.");
        }

        if (!await dbContext.Users.AnyAsync(
                item => item.Id == membership.UserId && item.AccountStatus == UserAccountStatus.Active,
                cancellationToken))
        {
            throw Error(PlatformErrorCodes.AccountSuspended, "کاربر عضو معلق است.");
        }

        var from = await dbContext.RoleAssignments.SingleOrDefaultAsync(item =>
            item.MembershipId == membershipId && item.Role == command.FromRole &&
            item.Status == RoleAssignmentStatus.Active && item.EndedAtUtc == null,
            cancellationToken);
        var to = await dbContext.RoleAssignments.SingleOrDefaultAsync(item =>
            item.MembershipId == membershipId && item.Role == command.ToRole &&
            item.Status == RoleAssignmentStatus.Active && item.EndedAtUtc == null,
            cancellationToken);
        if (from is null)
        {
            throw Error(PlatformErrorCodes.MembershipConflict, "نقش مبدأ فعال نیست.");
        }

        if (command.FromRole == OrganizationRole.Manager)
        {
            await EnsureReplacementManagerAsync(organizationId, membershipId, cancellationToken);
        }

        from.End(actor.OccurredAtUtc);
        if (to is null)
        {
            dbContext.RoleAssignments.Add(RoleAssignment.CreateActive(
                membershipId,
                command.ToRole,
                actor.OccurredAtUtc));
        }

        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.organization.membership.role.change",
            "Membership",
            membershipId.ToString("D"),
            organizationId,
            command.Reason,
            command.FromRole.ToString(),
            command.ToRole.ToString(),
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlatformMembershipMutationResult(
            await GetMembershipItemAsync(membershipId, cancellationToken),
            false);
    }

    public async Task<PlatformMembershipMutationResult> EndMembershipAsync(
        PlatformActorContext actor,
        Guid organizationId,
        Guid membershipId,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        _ = await LockOrganizationAsync(organizationId, cancellationToken)
            ?? throw Error(PlatformErrorCodes.OrganizationNotFound, "سازمان پیدا نشد.");
        var membership = await LockMembershipAsync(organizationId, membershipId, cancellationToken)
            ?? throw Error(PlatformErrorCodes.MembershipNotFound, "عضویت پیدا نشد.");
        if (!membership.IsActive)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PlatformMembershipMutationResult(
                await GetMembershipItemAsync(membershipId, cancellationToken),
                true);
        }

        var roles = await dbContext.RoleAssignments.Where(item =>
            item.MembershipId == membershipId && item.Status == RoleAssignmentStatus.Active &&
            item.EndedAtUtc == null).ToArrayAsync(cancellationToken);
        if (roles.Any(item => item.Role == OrganizationRole.Manager))
        {
            await EnsureReplacementManagerAsync(organizationId, membershipId, cancellationToken);
        }

        foreach (var role in roles)
        {
            role.End(actor.OccurredAtUtc);
        }

        membership.End(actor.OccurredAtUtc);
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.organization.membership.end",
            "Membership",
            membershipId.ToString("D"),
            organizationId,
            reason,
            "Active",
            "Ended",
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlatformMembershipMutationResult(
            await GetMembershipItemAsync(membershipId, cancellationToken),
            false);
    }

    public async Task<PlatformUserPage> GetUsersAsync(
        PlatformUserQuery query,
        CancellationToken cancellationToken)
    {
        var users = dbContext.Users.AsNoTracking();
        if (query.Status.HasValue)
        {
            users = users.Where(item => item.AccountStatus == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search;
            var matchingPersonIds = dbContext.Persons.AsNoTracking()
                .Where(item => item.FirstName.Contains(search) || item.LastName.Contains(search) ||
                               item.DisplayName != null && item.DisplayName.Contains(search))
                .Select(item => item.Id);
            users = users.Where(item =>
                item.Username != null && item.Username.Contains(search) ||
                item.PersonId.HasValue && matchingPersonIds.Contains(item.PersonId.Value));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var descending = query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = query.SortBy.ToLowerInvariant() switch
        {
            "displayname" => descending
                ? users.OrderByDescending(item => item.Username).ThenBy(item => item.Id)
                : users.OrderBy(item => item.Username).ThenBy(item => item.Id),
            "status" => descending
                ? users.OrderByDescending(item => item.AccountStatus).ThenBy(item => item.Id)
                : users.OrderBy(item => item.AccountStatus).ThenBy(item => item.Id),
            _ => descending
                ? users.OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id)
                : users.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id)
        };
        var rows = await (
            from user in ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            select new UserRow(
                user.Id,
                user.PersonId,
                person == null ? null : person.DisplayName,
                person == null ? null : person.FirstName,
                person == null ? null : person.LastName,
                user.Username,
                user.AccountStatus,
                user.SuspendedAtUtc,
                user.CreatedAtUtc,
                user.RowVersion))
            .ToArrayAsync(cancellationToken);
        var items = await BuildUserItemsAsync(rows, cancellationToken);
        return new PlatformUserPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformUserDetail?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from user in dbContext.Users.AsNoTracking()
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            where user.Id == userId
            select new UserRow(
                user.Id,
                user.PersonId,
                person == null ? null : person.DisplayName,
                person == null ? null : person.FirstName,
                person == null ? null : person.LastName,
                user.Username,
                user.AccountStatus,
                user.SuspendedAtUtc,
                user.CreatedAtUtc,
                user.RowVersion))
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var credential = await dbContext.UserCredentials.AsNoTracking()
            .Where(item => item.UserId == userId && item.Kind == CredentialKind.MobilePhone)
            .Select(item => new { item.NormalizedIdentifier, item.VerifiedAtUtc })
            .SingleOrDefaultAsync(cancellationToken);
        var memberships = await (
            from membership in dbContext.Memberships.AsNoTracking()
            join organization in dbContext.Organizations.AsNoTracking()
                on membership.OrganizationId equals organization.Id
            where membership.UserId == userId
            orderby membership.CreatedAtUtc descending
            select new UserMembershipRow(
                membership.Id,
                organization.Id,
                organization.Name,
                membership.Status))
            .ToArrayAsync(cancellationToken);
        var membershipIds = memberships.Select(item => item.MembershipId).ToArray();
        var roleRows = await dbContext.RoleAssignments.AsNoTracking()
            .Where(item => membershipIds.Contains(item.MembershipId) &&
                           item.Status == RoleAssignmentStatus.Active && item.EndedAtUtc == null)
            .Select(item => new { item.MembershipId, item.Role })
            .ToArrayAsync(cancellationToken);
        var membershipDtos = memberships.Select(item => new PlatformUserMembershipSummary(
            item.MembershipId,
            item.OrganizationId,
            item.OrganizationName,
            item.Status,
            roleRows.Where(role => role.MembershipId == item.MembershipId)
                .Select(role => role.Role).OrderBy(role => role).ToArray())).ToArray();

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var sessions = await dbContext.UserSessions.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(100)
            .Select(item => new PlatformUserSessionSummary(
                item.Id,
                item.CreatedAtUtc,
                item.ExpiresAtUtc,
                item.RevokedAtUtc,
                item.RevokedAtUtc == null && nowUtc < item.ExpiresAtUtc))
            .ToArrayAsync(cancellationToken);
        var platformRoles = await GetPlatformRolesAsync([userId], cancellationToken);
        var activeAsGuardian = await dbContext.GuardianRelations.AsNoTracking().CountAsync(item =>
            item.GuardianUserId == userId && item.Status == GuardianRelationStatus.Active &&
            item.EndedAtUtc == null,
            cancellationToken);
        var personId = row.PersonId;
        var activeAsLearner = !personId.HasValue
            ? 0
            : await (
                from relation in dbContext.GuardianRelations.AsNoTracking()
                join organizationPerson in dbContext.OrganizationPersons.AsNoTracking()
                    on relation.LearnerOrganizationPersonId equals organizationPerson.Id
                where organizationPerson.PersonId == personId.Value &&
                      relation.Status == GuardianRelationStatus.Active && relation.EndedAtUtc == null
                select relation.Id).CountAsync(cancellationToken);
        var history = await dbContext.AdminAuditLogs.AsNoTracking()
            .Where(item => item.TargetId == userId.ToString())
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(20)
            .Select(item => new PlatformAdminHistoryReference(
                item.Id,
                item.Action,
                item.OccurredAtUtc,
                (int)item.Result))
            .ToArrayAsync(cancellationToken);

        return new PlatformUserDetail(
            row.UserId,
            row.PersonId,
            DisplayName(row.DisplayName, row.FirstName, row.LastName, row.Username, row.UserId),
            row.Username,
            MaskPhone(credential?.NormalizedIdentifier),
            credential is not null,
            row.AccountStatus,
            row.SuspendedAtUtc,
            row.CreatedAtUtc,
            membershipDtos,
            new PlatformGuardianSummary(activeAsGuardian, activeAsLearner),
            sessions,
            platformRoles.GetValueOrDefault(userId, []),
            history,
            Version(row.RowVersion));
    }

    public async Task<PlatformUserMutationResult> SetUserActiveAsync(
        PlatformActorContext actor,
        Guid userId,
        bool active,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);

        // ترتیب قفل‌ها با عملیات IAM یکسان نگه داشته می‌شود تا رقابت suspend/revoke به deadlock منجر نشود.
        await dbContext.PlatformRoles
            .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformRoles] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {PlatformCatalogIds.PlatformOwnerRole}")
            .SingleAsync(cancellationToken);
        Guid[] managedOrganizationIds = active
            ? []
            : await GetManagedOrganizationIdsAsync(userId, cancellationToken);
        foreach (var organizationId in managedOrganizationIds)
        {
            await LockOrganizationAsync(organizationId, cancellationToken);
        }

        var user = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {userId}")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw Error(PlatformErrorCodes.UserNotFound, "کاربر پیدا نشد.");
        var replayed = active
            ? user.AccountStatus == UserAccountStatus.Active
            : user.AccountStatus == UserAccountStatus.Suspended;
        if (replayed)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PlatformUserMutationResult(
                user.Id,
                user.AccountStatus,
                user.SuspendedAtUtc,
                0,
                true,
                Version(user.RowVersion));
        }

        var revokedSessions = 0;
        if (!active)
        {
            await EnsureUserIsNotLastOwnerAsync(userId, cancellationToken);
            await EnsureUserIsNotLastManagerAsync(userId, managedOrganizationIds, cancellationToken);
            user.Suspend(actor.OccurredAtUtc);
            var sessions = await dbContext.UserSessions.Where(item =>
                item.UserId == userId && item.RevokedAtUtc == null &&
                item.ExpiresAtUtc > actor.OccurredAtUtc).ToArrayAsync(cancellationToken);
            foreach (var session in sessions)
            {
                session.Revoke(actor.OccurredAtUtc);
            }

            revokedSessions = sessions.Length;
        }
        else
        {
            user.Reactivate(actor.OccurredAtUtc);
        }

        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            active ? "platform.user.reactivate" : "platform.user.suspend",
            "User",
            userId.ToString("D"),
            null,
            reason,
            active ? "Suspended" : "Active",
            active ? "Active" : $"Suspended;sessionsRevoked={revokedSessions}",
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlatformUserMutationResult(
            user.Id,
            user.AccountStatus,
            user.SuspendedAtUtc,
            revokedSessions,
            false,
            Version(user.RowVersion));
    }

    public async Task<PlatformSessionRevocationResult> RevokeSessionsAsync(
        PlatformActorContext actor,
        Guid userId,
        Guid? sessionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        if (!await dbContext.Users.AnyAsync(item => item.Id == userId, cancellationToken))
        {
            throw Error(PlatformErrorCodes.UserNotFound, "کاربر پیدا نشد.");
        }

        var sessions = sessionId.HasValue
            ? await dbContext.UserSessions.Where(item =>
                item.Id == sessionId.Value && item.UserId == userId).ToArrayAsync(cancellationToken)
            : await dbContext.UserSessions.Where(item =>
                item.UserId == userId && item.RevokedAtUtc == null &&
                item.ExpiresAtUtc > actor.OccurredAtUtc).ToArrayAsync(cancellationToken);
        if (sessionId.HasValue && sessions.Length == 0)
        {
            throw Error(PlatformErrorCodes.SessionNotFound, "نشست پیدا نشد.");
        }

        var revoked = 0;
        foreach (var session in sessions.Where(item => item.IsActive(actor.OccurredAtUtc)))
        {
            session.Revoke(actor.OccurredAtUtc);
            revoked++;
        }

        if (revoked > 0)
        {
            dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
                actor,
                sessionId.HasValue ? "platform.session.revoke" : "platform.sessions.revoke-all",
                sessionId.HasValue ? "UserSession" : "User",
                (sessionId ?? userId).ToString("D"),
                null,
                reason,
                null,
                $"user={userId:D};revoked={revoked}",
                cancellationToken));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new PlatformSessionRevocationResult(userId, sessionId, revoked, revoked == 0);
    }

    private async Task ValidateProvisioningReferencesAsync(
        ProvisionOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.PlanVersionId.HasValue && !await dbContext.PlanVersions.AsNoTracking().AnyAsync(
                item => item.Id == command.PlanVersionId.Value &&
                        item.Status == PlanVersionStatus.Published,
                cancellationToken))
        {
            throw Error(
                PlatformErrorCodes.PlanVersionNotPublished,
                "نسخه پلن منتشرشده پیدا نشد.");
        }

        if (command.InitialManagerUserId.HasValue && !await dbContext.Users.AsNoTracking().AnyAsync(
                item => item.Id == command.InitialManagerUserId.Value &&
                        item.AccountStatus == UserAccountStatus.Active,
                cancellationToken))
        {
            throw Error(PlatformErrorCodes.UserNotFound, "کاربر مدیر اولیه فعال نیست.");
        }
    }

    private async Task<(Guid RequestId, bool Replayed)> GetOrCreateProvisioningRequestAsync(
        PlatformActorContext actor,
        ProvisionOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var existing = await dbContext.PlatformProvisioningRequests
            .FromSqlInterpolated($"SELECT * FROM [platform].[ProvisioningRequests] WITH (UPDLOCK, HOLDLOCK) WHERE [ClientOperationId] = {command.ClientOperationId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (!existing.Matches(
                    command.OrganizationName,
                    command.PlanVersionId,
                    command.InitialManagerUserId,
                    command.SubscriptionEndsAtUtc,
                    command.InvitationExpiresAtUtc))
            {
                throw Error(
                    PlatformErrorCodes.ProvisioningConflict,
                    "ClientOperationId قبلاً با payload دیگری استفاده شده است.");
            }

            if (existing.Status == PlatformProvisioningStatus.NeedsAttention)
            {
                existing.Resume(actor.OccurredAtUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            var id = existing.Id;
            dbContext.ChangeTracker.Clear();
            return (id, true);
        }

        var request = PlatformProvisioningRequest.Create(
            command.ClientOperationId,
            actor.UserId,
            command.OrganizationName,
            command.PlanVersionId,
            command.InitialManagerUserId,
            command.SubscriptionEndsAtUtc,
            command.InvitationExpiresAtUtc,
            command.Reason,
            actor.OccurredAtUtc);
        dbContext.PlatformProvisioningRequests.Add(request);
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.provisioning.request",
            "PlatformProvisioningRequest",
            request.Id.ToString("D"),
            null,
            command.Reason,
            null,
            $"clientOperationId={command.ClientOperationId}",
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var requestId = request.Id;
        dbContext.ChangeTracker.Clear();
        return (requestId, false);
    }

    private async Task EnsureOrganizationStageAsync(
        Guid requestId,
        PlatformActorContext actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var request = await LockProvisioningAsync(requestId, cancellationToken);
        if (request.OrganizationId.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return;
        }

        var organization = MakanApp.Domain.Organization.Organization.Create(
            request.OrganizationName,
            actor.OccurredAtUtc);
        dbContext.Organizations.Add(organization);
        request.MarkOrganizationCreated(organization.Id, actor.OccurredAtUtc);
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.organization.create",
            "Organization",
            organization.Id.ToString("D"),
            organization.Id,
            request.Reason,
            null,
            $"name={organization.Name}",
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task EnsureSubscriptionStageAsync(
        Guid requestId,
        PlatformActorContext actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var request = await LockProvisioningAsync(requestId, cancellationToken);
        if (request.SubscriptionId.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return;
        }

        var planVersionId = request.PlanVersionId
            ?? throw Error(PlatformErrorCodes.ProvisioningConflict, "نسخه پلن در درخواست ثبت نشده است.");
        var organizationId = request.OrganizationId
            ?? throw Error(PlatformErrorCodes.ProvisioningConflict, "سازمان provisioning هنوز ساخته نشده است.");
        var planVersion = await dbContext.PlanVersions.SingleOrDefaultAsync(
            item => item.Id == planVersionId,
            cancellationToken);
        if (planVersion is null || !planVersion.IsPublished)
        {
            throw Error(
                PlatformErrorCodes.PlanVersionNotPublished,
                "نسخه پلن منتشرشده در دسترس نیست.");
        }

        var existing = await dbContext.Subscriptions.SingleOrDefaultAsync(item =>
            item.OrganizationId == organizationId && item.Status == SubscriptionStatus.Active,
            cancellationToken);
        if (existing is not null)
        {
            throw Error(
                PlatformErrorCodes.ProvisioningConflict,
                "سازمان از قبل اشتراک فعال دارد.");
        }

        var subscription = Subscription.CreatePending(
            organizationId,
            planVersion,
            actor.OccurredAtUtc,
            AsUtc(request.SubscriptionEndsAtUtc),
            actor.OccurredAtUtc);
        subscription.Activate(actor.OccurredAtUtc);
        dbContext.Subscriptions.Add(subscription);
        request.MarkSubscriptionAssigned(subscription.Id, actor.OccurredAtUtc);
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.provisioning.subscription.assign",
            "Subscription",
            subscription.Id.ToString("D"),
            organizationId,
            request.Reason,
            null,
            $"planVersionId={planVersionId:D};status=Active",
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task EnsureManagerInvitationStageAsync(
        Guid requestId,
        PlatformActorContext actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var request = await LockProvisioningAsync(requestId, cancellationToken);
        if (request.ManagerInvitationId.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return;
        }

        var organizationId = request.OrganizationId
            ?? throw Error(PlatformErrorCodes.ProvisioningConflict, "سازمان provisioning هنوز ساخته نشده است.");
        var managerUserId = request.InitialManagerUserId
            ?? throw Error(PlatformErrorCodes.ProvisioningConflict, "مدیر اولیه در درخواست ثبت نشده است.");
        if (!await dbContext.Users.AnyAsync(
                item => item.Id == managerUserId && item.AccountStatus == UserAccountStatus.Active,
                cancellationToken))
        {
            throw Error(PlatformErrorCodes.UserNotFound, "کاربر مدیر اولیه فعال نیست.");
        }

        var invitation = await dbContext.Invitations.SingleOrDefaultAsync(item =>
            item.OrganizationId == organizationId && item.DestinationUserId == managerUserId &&
            item.Role == OrganizationRole.Manager && item.Status == InvitationStatus.Pending,
            cancellationToken);
        if (invitation is null)
        {
            invitation = Invitation.Create(
                organizationId,
                managerUserId,
                actor.UserId,
                OrganizationRole.Manager,
                actor.OccurredAtUtc,
                AsUtc(request.InvitationExpiresAtUtc)!.Value);
            dbContext.Invitations.Add(invitation);
        }

        request.MarkManagerInvitationCreated(invitation.Id, actor.OccurredAtUtc);
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.provisioning.manager-invitation.create",
            "Invitation",
            invitation.Id.ToString("D"),
            organizationId,
            request.Reason,
            null,
            $"user={managerUserId:D};role=Manager",
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task EnsureProvisioningCompletedAsync(
        Guid requestId,
        PlatformActorContext actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var request = await LockProvisioningAsync(requestId, cancellationToken);
        if (request.Status == PlatformProvisioningStatus.Completed)
        {
            await transaction.CommitAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return;
        }

        request.Complete(actor.OccurredAtUtc);
        dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.provisioning.complete",
            "PlatformProvisioningRequest",
            request.Id.ToString("D"),
            request.OrganizationId,
            request.Reason,
            null,
            "status=Completed",
            cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task MarkProvisioningNeedsAttentionAsync(
        Guid requestId,
        string failureCode,
        PlatformActorContext actor,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var request = await LockProvisioningAsync(requestId, cancellationToken);
        if (request.Status != PlatformProvisioningStatus.Completed)
        {
            request.MarkNeedsAttention(failureCode, actor.OccurredAtUtc);
            dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
                actor,
                "platform.provisioning.needs-attention",
                "PlatformProvisioningRequest",
                request.Id.ToString("D"),
                request.OrganizationId,
                request.Reason,
                null,
                $"status=NeedsAttention;failureCode={failureCode}",
                cancellationToken));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task<PlatformProvisioningRequest> LockProvisioningAsync(
        Guid requestId,
        CancellationToken cancellationToken) =>
        await dbContext.PlatformProvisioningRequests
            .FromSqlInterpolated($"SELECT * FROM [platform].[ProvisioningRequests] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {requestId}")
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw Error(PlatformErrorCodes.ProvisioningNotFound, "درخواست provisioning پیدا نشد.");

    private async Task<IReadOnlyCollection<PlatformOrganizationItem>> BuildOrganizationItemsAsync(
        IReadOnlyCollection<OrganizationRow> rows,
        CancellationToken cancellationToken)
    {
        var ids = rows.Select(item => item.Id).ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var memberCounts = await dbContext.Memberships.AsNoTracking()
            .Where(item => ids.Contains(item.OrganizationId) &&
                           item.Status == MembershipStatus.Active && item.EndedAtUtc == null)
            .GroupBy(item => item.OrganizationId)
            .Select(group => new { OrganizationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.OrganizationId, item => item.Count, cancellationToken);
        var classCounts = await dbContext.Classes.AsNoTracking()
            .Where(item => ids.Contains(item.OrganizationId) &&
                           item.Status == ClassStatus.Active && item.EndedAtUtc == null)
            .GroupBy(item => item.OrganizationId)
            .Select(group => new { OrganizationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.OrganizationId, item => item.Count, cancellationToken);
        var managerRows = await GetManagersAsync(ids, cancellationToken);
        var subscriptions = await GetLatestSubscriptionsAsync(ids, cancellationToken);

        return rows.Select(row =>
        {
            var managers = managerRows.Where(item => item.OrganizationId == row.Id)
                .Select(item => new PlatformManagerSummary(
                    item.UserId,
                    DisplayName(item.DisplayName, item.FirstName, item.LastName, item.Username, item.UserId)))
                .Take(5)
                .ToArray();
            return new PlatformOrganizationItem(
                row.Id,
                row.Name,
                row.Status,
                memberCounts.GetValueOrDefault(row.Id),
                classCounts.GetValueOrDefault(row.Id),
                managerRows.Count(item => item.OrganizationId == row.Id),
                managers,
                subscriptions.GetValueOrDefault(row.Id),
                row.CreatedAtUtc);
        }).ToArray();
    }

    private async Task<PlatformOrganizationDetail> BuildOrganizationDetailAsync(
        OrganizationRow row,
        CancellationToken cancellationToken)
    {
        var ids = new[] { row.Id };
        var managers = (await GetManagersAsync(ids, cancellationToken))
            .Select(item => new PlatformManagerSummary(
                item.UserId,
                DisplayName(item.DisplayName, item.FirstName, item.LastName, item.Username, item.UserId)))
            .ToArray();
        var activeMembers = await dbContext.Memberships.AsNoTracking().CountAsync(item =>
            item.OrganizationId == row.Id && item.Status == MembershipStatus.Active &&
            item.EndedAtUtc == null,
            cancellationToken);
        var roleCounts = await (
            from membership in dbContext.Memberships.AsNoTracking()
            join role in dbContext.RoleAssignments.AsNoTracking() on membership.Id equals role.MembershipId
            where membership.OrganizationId == row.Id && membership.Status == MembershipStatus.Active &&
                  membership.EndedAtUtc == null && role.Status == RoleAssignmentStatus.Active &&
                  role.EndedAtUtc == null
            group role by role.Role into grouped
            select new PlatformOrganizationRoleCount(grouped.Key, grouped.Count()))
            .ToArrayAsync(cancellationToken);
        var classSummary = await dbContext.Classes.AsNoTracking()
            .Where(item => item.OrganizationId == row.Id)
            .GroupBy(_ => 1)
            .Select(group => new PlatformClassSummary(
                group.Count(),
                group.Count(item => item.Status == ClassStatus.Active && item.EndedAtUtc == null),
                group.Where(item => item.Status == ClassStatus.Active && item.EndedAtUtc == null)
                    .Sum(item => item.Capacity)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new PlatformClassSummary(0, 0, 0);
        var subscriptions = await GetLatestSubscriptionsAsync(ids, cancellationToken);
        var lastMembership = await dbContext.Memberships.AsNoTracking()
            .Where(item => item.OrganizationId == row.Id)
            .MaxAsync(item => (DateTime?)item.CreatedAtUtc, cancellationToken);
        var lastClass = await dbContext.Classes.AsNoTracking()
            .Where(item => item.OrganizationId == row.Id)
            .MaxAsync(item => (DateTime?)item.CreatedAtUtc, cancellationToken);
        var lastAdminAction = await dbContext.AdminAuditLogs.AsNoTracking()
            .Where(item => item.OrganizationId == row.Id)
            .MaxAsync(item => (DateTime?)item.OccurredAtUtc, cancellationToken);

        return new PlatformOrganizationDetail(
            row.Id,
            row.Name,
            row.Status,
            row.CreatedAtUtc,
            managers,
            activeMembers,
            roleCounts,
            classSummary,
            subscriptions.GetValueOrDefault(row.Id),
            new PlatformOrganizationActivitySummary(lastMembership, lastClass, lastAdminAction),
            Version(row.RowVersion));
    }

    private async Task<PlatformOrganizationImpactPreview> BuildImpactAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var activeMemberships = await dbContext.Memberships.AsNoTracking().CountAsync(item =>
            item.OrganizationId == organizationId && item.Status == MembershipStatus.Active &&
            item.EndedAtUtc == null,
            cancellationToken);
        var activeClasses = await dbContext.Classes.AsNoTracking().CountAsync(item =>
            item.OrganizationId == organizationId && item.Status == ClassStatus.Active &&
            item.EndedAtUtc == null,
            cancellationToken);
        var activeSubscriptions = await dbContext.Subscriptions.AsNoTracking().CountAsync(item =>
            item.OrganizationId == organizationId && item.Status == SubscriptionStatus.Active,
            cancellationToken);
        var pendingInvitations = await dbContext.Invitations.AsNoTracking().CountAsync(item =>
            item.OrganizationId == organizationId && item.Status == InvitationStatus.Pending,
            cancellationToken);
        var pendingProvisioning = await dbContext.PlatformProvisioningRequests.AsNoTracking().CountAsync(item =>
            item.OrganizationId == organizationId && item.Status != PlatformProvisioningStatus.Completed,
            cancellationToken);
        return new PlatformOrganizationImpactPreview(
            organizationId,
            activeMemberships,
            activeClasses,
            activeSubscriptions,
            pendingInvitations,
            pendingProvisioning);
    }

    private async Task<ManagerRow[]> GetManagersAsync(
        Guid[] organizationIds,
        CancellationToken cancellationToken) =>
        await (
            from membership in dbContext.Memberships.AsNoTracking()
            join role in dbContext.RoleAssignments.AsNoTracking() on membership.Id equals role.MembershipId
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            where organizationIds.Contains(membership.OrganizationId) &&
                  membership.Status == MembershipStatus.Active && membership.EndedAtUtc == null &&
                  role.Role == OrganizationRole.Manager && role.Status == RoleAssignmentStatus.Active &&
                  role.EndedAtUtc == null && user.AccountStatus == UserAccountStatus.Active
            orderby membership.OrganizationId, user.Id
            select new ManagerRow(
                membership.OrganizationId,
                user.Id,
                person == null ? null : person.DisplayName,
                person == null ? null : person.FirstName,
                person == null ? null : person.LastName,
                user.Username))
            .ToArrayAsync(cancellationToken);

    private async Task<Dictionary<Guid, PlatformSubscriptionSummary>> GetLatestSubscriptionsAsync(
        Guid[] organizationIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from subscription in dbContext.Subscriptions.AsNoTracking()
            join version in dbContext.PlanVersions.AsNoTracking() on subscription.PlanVersionId equals version.Id
            join plan in dbContext.Plans.AsNoTracking() on version.PlanId equals plan.Id
            where organizationIds.Contains(subscription.OrganizationId)
            orderby subscription.OrganizationId, subscription.CreatedAtUtc descending
            select new SubscriptionRow(
                subscription.OrganizationId,
                subscription.Id,
                subscription.PlanVersionId,
                plan.DisplayName,
                subscription.Status,
                subscription.StartsAtUtc,
                subscription.EndsAtUtc,
                subscription.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        return rows.GroupBy(item => item.OrganizationId).ToDictionary(
            group => group.Key,
            group =>
            {
                var item = group.OrderByDescending(value => value.CreatedAtUtc).First();
                return new PlatformSubscriptionSummary(
                    item.SubscriptionId,
                    item.PlanVersionId,
                    item.PlanName,
                    item.Status,
                    item.StartsAtUtc,
                    item.EndsAtUtc);
            });
    }

    private async Task<IReadOnlyCollection<PlatformMembershipItem>> BuildMembershipItemsAsync(
        IReadOnlyCollection<MembershipRow> rows,
        CancellationToken cancellationToken)
    {
        var ids = rows.Select(item => item.MembershipId).ToArray();
        MembershipRoleRow[] roles = ids.Length == 0
            ? []
            : await dbContext.RoleAssignments.AsNoTracking()
                .Where(item => ids.Contains(item.MembershipId) &&
                               item.Status == RoleAssignmentStatus.Active && item.EndedAtUtc == null)
                .Select(item => new MembershipRoleRow(item.MembershipId, item.Role))
                .ToArrayAsync(cancellationToken);
        return rows.Select(item => new PlatformMembershipItem(
            item.MembershipId,
            item.UserId,
            DisplayName(item.DisplayName, item.FirstName, item.LastName, item.Username, item.UserId),
            item.Status,
            roles.Where(role => role.MembershipId == item.MembershipId)
                .Select(role => role.Role).OrderBy(role => role).ToArray(),
            item.CreatedAtUtc,
            item.EndedAtUtc,
            Version(item.RowVersion))).ToArray();
    }

    private async Task<PlatformMembershipItem> GetMembershipItemAsync(
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from membership in dbContext.Memberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            where membership.Id == membershipId
            select new MembershipRow(
                membership.Id,
                membership.UserId,
                person == null ? null : person.DisplayName,
                person == null ? null : person.FirstName,
                person == null ? null : person.LastName,
                user.Username,
                membership.Status,
                membership.CreatedAtUtc,
                membership.EndedAtUtc,
                membership.RowVersion))
            .SingleAsync(cancellationToken);
        return (await BuildMembershipItemsAsync([row], cancellationToken)).Single();
    }

    private async Task<IReadOnlyCollection<PlatformUserItem>> BuildUserItemsAsync(
        IReadOnlyCollection<UserRow> rows,
        CancellationToken cancellationToken)
    {
        var ids = rows.Select(item => item.UserId).ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var phones = await dbContext.UserCredentials.AsNoTracking()
            .Where(item => ids.Contains(item.UserId) && item.Kind == CredentialKind.MobilePhone)
            .Select(item => new { item.UserId, item.NormalizedIdentifier })
            .ToDictionaryAsync(item => item.UserId, item => item.NormalizedIdentifier, cancellationToken);
        var memberships = await dbContext.Memberships.AsNoTracking()
            .Where(item => ids.Contains(item.UserId) && item.Status == MembershipStatus.Active &&
                           item.EndedAtUtc == null)
            .GroupBy(item => item.UserId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);
        var lastSessions = await dbContext.UserSessions.AsNoTracking()
            .Where(item => ids.Contains(item.UserId))
            .GroupBy(item => item.UserId)
            .Select(group => new { UserId = group.Key, Last = group.Max(item => item.CreatedAtUtc) })
            .ToDictionaryAsync(item => item.UserId, item => (DateTime?)item.Last, cancellationToken);
        var roles = await GetPlatformRolesAsync(ids, cancellationToken);
        return rows.Select(item => new PlatformUserItem(
            item.UserId,
            item.PersonId,
            DisplayName(item.DisplayName, item.FirstName, item.LastName, item.Username, item.UserId),
            MaskPhone(phones.GetValueOrDefault(item.UserId)),
            item.AccountStatus,
            item.CreatedAtUtc,
            lastSessions.GetValueOrDefault(item.UserId),
            roles.GetValueOrDefault(item.UserId, []),
            memberships.GetValueOrDefault(item.UserId))).ToArray();
    }

    private async Task<Dictionary<Guid, IReadOnlyCollection<string>>> GetPlatformRolesAsync(
        Guid[] userIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from assignment in dbContext.PlatformRoleAssignments.AsNoTracking()
            join role in dbContext.PlatformRoles.AsNoTracking() on assignment.RoleId equals role.Id
            where userIds.Contains(assignment.UserId) &&
                  assignment.Status == PlatformRoleAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null
            orderby role.Key
            select new { assignment.UserId, role.Key })
            .ToArrayAsync(cancellationToken);
        return rows.GroupBy(item => item.UserId).ToDictionary(
            group => group.Key,
            group => (IReadOnlyCollection<string>)group.Select(item => item.Key).ToArray());
    }

    private async Task EnsureReplacementManagerAsync(
        Guid organizationId,
        Guid excludedMembershipId,
        CancellationToken cancellationToken)
    {
        var replacementCount = await (
            from membership in dbContext.Memberships
            join role in dbContext.RoleAssignments on membership.Id equals role.MembershipId
            join user in dbContext.Users on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId && membership.Id != excludedMembershipId &&
                  membership.Status == MembershipStatus.Active && membership.EndedAtUtc == null &&
                  role.Role == OrganizationRole.Manager && role.Status == RoleAssignmentStatus.Active &&
                  role.EndedAtUtc == null && user.AccountStatus == UserAccountStatus.Active
            select membership.Id).Distinct().CountAsync(cancellationToken);
        if (replacementCount == 0)
        {
            throw Error(
                PlatformErrorCodes.LastManagerRequired,
                "آخرین مدیر فعال سازمان قابل حذف نیست.");
        }
    }

    private async Task EnsureUserIsNotLastOwnerAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isOwner = await dbContext.PlatformRoleAssignments.AnyAsync(item =>
            item.UserId == userId && item.RoleId == PlatformCatalogIds.PlatformOwnerRole &&
            item.Status == PlatformRoleAssignmentStatus.Active && item.EndedAtUtc == null,
            cancellationToken);
        if (!isOwner)
        {
            return;
        }

        var replacements = await (
            from assignment in dbContext.PlatformRoleAssignments
            join user in dbContext.Users on assignment.UserId equals user.Id
            where assignment.UserId != userId &&
                  assignment.RoleId == PlatformCatalogIds.PlatformOwnerRole &&
                  assignment.Status == PlatformRoleAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null && user.AccountStatus == UserAccountStatus.Active
            select assignment.Id).CountAsync(cancellationToken);
        if (replacements == 0)
        {
            throw Error(PlatformErrorCodes.LastOwnerRequired, "آخرین مالک فعال پلتفرم قابل تعلیق نیست.");
        }
    }

    private Task<Guid[]> GetManagedOrganizationIdsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from membership in dbContext.Memberships
            join role in dbContext.RoleAssignments on membership.Id equals role.MembershipId
            where membership.UserId == userId && membership.Status == MembershipStatus.Active &&
                  membership.EndedAtUtc == null && role.Role == OrganizationRole.Manager &&
                  role.Status == RoleAssignmentStatus.Active && role.EndedAtUtc == null
            select membership.OrganizationId)
        .Distinct()
        .OrderBy(item => item)
        .ToArrayAsync(cancellationToken);

    private async Task EnsureUserIsNotLastManagerAsync(
        Guid userId,
        IReadOnlyCollection<Guid> organizationIds,
        CancellationToken cancellationToken)
    {
        foreach (var organizationId in organizationIds)
        {
            var replacements = await (
                from membership in dbContext.Memberships
                join role in dbContext.RoleAssignments on membership.Id equals role.MembershipId
                join user in dbContext.Users on membership.UserId equals user.Id
                where membership.OrganizationId == organizationId && membership.UserId != userId &&
                      membership.Status == MembershipStatus.Active && membership.EndedAtUtc == null &&
                      role.Role == OrganizationRole.Manager && role.Status == RoleAssignmentStatus.Active &&
                      role.EndedAtUtc == null && user.AccountStatus == UserAccountStatus.Active
                select membership.Id).Distinct().CountAsync(cancellationToken);
            if (replacements == 0)
            {
                throw Error(
                    PlatformErrorCodes.LastManagerRequired,
                    "کاربر آخرین مدیر فعال یکی از سازمان‌ها است و قابل تعلیق نیست.");
            }
        }
    }

    private Task<MakanApp.Domain.Organization.Organization?> LockOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        dbContext.Organizations
            .FromSqlInterpolated($"SELECT * FROM [organization].[Organizations] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {organizationId}")
            .SingleOrDefaultAsync(cancellationToken);

    private Task<Membership?> LockMembershipAsync(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        dbContext.Memberships
            .FromSqlInterpolated($"SELECT * FROM [organization].[Memberships] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {membershipId} AND [OrganizationId] = {organizationId}")
            .SingleOrDefaultAsync(cancellationToken);

    private Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginSerializableAsync(
        CancellationToken cancellationToken) =>
        dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

    private async Task<AdminAuditLog> CreateAuditAsync(
        PlatformActorContext actor,
        string action,
        string targetType,
        string targetId,
        Guid? organizationId,
        string reason,
        string? before,
        string? after,
        CancellationToken cancellationToken)
    {
        var personId = await dbContext.Users.AsNoTracking()
            .Where(item => item.Id == actor.UserId)
            .Select(item => item.PersonId)
            .SingleAsync(cancellationToken);
        return AdminAuditLog.Create(
            actor.UserId,
            personId,
            actor.SessionId,
            action,
            targetType,
            targetId,
            organizationId,
            reason,
            actor.OccurredAtUtc,
            actor.CorrelationId,
            AdminAuditResult.Succeeded,
            before,
            after);
    }

    private static PlatformProvisioningItem MapProvisioning(PlatformProvisioningRequest item) => new(
        item.Id,
        item.ClientOperationId,
        item.RequestedByUserId,
        item.OrganizationName,
        item.PlanVersionId,
        item.InitialManagerUserId,
        item.Status,
        item.OrganizationId,
        item.SubscriptionId,
        item.ManagerInvitationId,
        item.FailureCode,
        item.CreatedAtUtc,
        item.UpdatedAtUtc,
        Version(item.RowVersion));

    private static PlatformInvitationResult MapInvitation(Invitation item, bool replayed) => new(
        item.Id,
        item.OrganizationId,
        item.DestinationUserId,
        item.Role,
        item.Status,
        item.ExpiresAtUtc,
        replayed);

    private static string DisplayName(
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
            : !string.IsNullOrWhiteSpace(username) ? username : userId.ToString("D");
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var value = phone.Trim();
        if (value.Length <= 6)
        {
            return new string('*', value.Length);
        }

        return string.Concat(value.AsSpan(0, 3), new string('*', value.Length - 6), value.AsSpan(value.Length - 3));
    }

    private static string Version(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static PlatformException Error(string code, string message) => new(code, message);

    private sealed record OrganizationRow(
        Guid Id,
        string Name,
        OrganizationStatus Status,
        DateTime CreatedAtUtc,
        byte[] RowVersion);

    private sealed record ManagerRow(
        Guid OrganizationId,
        Guid UserId,
        string? DisplayName,
        string? FirstName,
        string? LastName,
        string? Username);

    private sealed record SubscriptionRow(
        Guid OrganizationId,
        Guid SubscriptionId,
        Guid PlanVersionId,
        string PlanName,
        SubscriptionStatus Status,
        DateTime StartsAtUtc,
        DateTime? EndsAtUtc,
        DateTime CreatedAtUtc);

    private sealed record MembershipRow(
        Guid MembershipId,
        Guid UserId,
        string? DisplayName,
        string? FirstName,
        string? LastName,
        string? Username,
        MembershipStatus Status,
        DateTime CreatedAtUtc,
        DateTime? EndedAtUtc,
        byte[] RowVersion);

    private sealed record MembershipRoleRow(Guid MembershipId, OrganizationRole Role);

    private sealed record UserRow(
        Guid UserId,
        Guid? PersonId,
        string? DisplayName,
        string? FirstName,
        string? LastName,
        string? Username,
        UserAccountStatus AccountStatus,
        DateTime? SuspendedAtUtc,
        DateTime CreatedAtUtc,
        byte[] RowVersion);

    private sealed record UserMembershipRow(
        Guid MembershipId,
        Guid OrganizationId,
        string OrganizationName,
        MembershipStatus Status);
}
