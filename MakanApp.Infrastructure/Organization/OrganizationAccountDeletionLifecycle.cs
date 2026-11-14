using MakanApp.Application.Identity;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Organization;

public sealed class OrganizationAccountDeletionLifecycle(MakanDbContext db, TimeProvider clock) : IOrganizationAccountDeletionLifecycle
{
    public async Task EnsureCanFreezeAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var ids = await db.Memberships.Where(m => m.UserId == context.UserId && m.EndedAtUtc == null).Select(m => m.OrganizationId)
            .Union(db.PlatformProvisioningRequests.Where(p => p.InitialManagerUserId == context.UserId && p.OrganizationId != null).Select(p => p.OrganizationId!.Value))
            .Distinct().OrderBy(id => id).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            _ = await db.Organizations.FromSqlInterpolated($"SELECT * FROM [organization].[Organizations] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").AsNoTracking().SingleAsync(ct);
            var manages = await db.Memberships.AnyAsync(m => m.UserId == context.UserId && m.OrganizationId == id &&
                m.Status == MembershipStatus.Active && m.EndedAtUtc == null && db.RoleAssignments.Any(r => r.MembershipId == m.Id &&
                    r.Role == OrganizationRole.Manager && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null), ct);
            if (manages && !await ReplacementManagers(id, context.UserId).AnyAsync(ct))
                throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.LastOrganizationManager);
        }
        await EnsureSchoolManagerAsync(context.UserId, ct);
    }

    public async Task EndAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var members = await db.Memberships.Where(m => m.UserId == context.UserId && m.EndedAtUtc == null).ToArrayAsync(ct);
        var roles = await db.RoleAssignments.Where(r => r.EndedAtUtc == null && db.Memberships.Any(m => m.Id == r.MembershipId && m.UserId == context.UserId)).ToArrayAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var role in roles) role.End(now);
        foreach (var member in members) member.End(now);
        // فقط پروندهٔ قابل لغوِ بدون هیچ Order بسته می‌شود؛ وضعیت Paid/Unknown یا سوابق مالی mutate نمی‌شوند.
        var drafts = await db.SchoolApplications.Where(a => a.ApplicantUserId == context.UserId &&
            (a.Status == SchoolApplicationStatus.Draft || a.Status == SchoolApplicationStatus.Submitted ||
             a.Status == SchoolApplicationStatus.UnderReview || a.Status == SchoolApplicationStatus.NeedsChanges) &&
            !db.Orders.Any(o => o.SchoolApplicationId == a.Id)).ToArrayAsync(ct);
        foreach (var draft in drafts)
        {
            draft.Cancel(now);
            db.SchoolApplicationHistories.Add(SchoolApplicationHistory.Create(draft, context.UserId, "Cancelled", AccountDeletionRequestPolicy, now));
        }
    }

    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        if (await db.Memberships.AnyAsync(m => m.UserId == context.UserId && m.EndedAtUtc == null, ct) ||
            await db.RoleAssignments.AnyAsync(r => r.EndedAtUtc == null && db.Memberships.Any(m => m.Id == r.MembershipId && m.UserId == context.UserId), ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
        await EnsureSchoolManagerAsync(context.UserId, ct);
    }

    private IQueryable<Guid> ReplacementManagers(Guid org, Guid user) =>
        from m in db.Memberships join r in db.RoleAssignments on m.Id equals r.MembershipId join u in db.Users on m.UserId equals u.Id
        where m.OrganizationId == org && m.UserId != user && m.Status == MembershipStatus.Active && m.EndedAtUtc == null &&
            r.Role == OrganizationRole.Manager && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null && u.AccountStatus == UserAccountStatus.Active
        select m.Id;

    private async Task EnsureSchoolManagerAsync(Guid user, CancellationToken ct)
    {
        var paid = await db.SchoolApplications.Where(a => a.FinalInitialManagerUserId == user &&
            db.Orders.Any(o => o.SchoolApplicationId == a.Id && o.Status == OrderStatus.Paid)).Select(a => a.Id).ToArrayAsync(ct);
        foreach (var app in paid)
        {
            var organization = await db.PlatformProvisioningRequests.Where(p => p.SchoolApplicationId == app).Select(p => p.OrganizationId).SingleOrDefaultAsync(ct);
            if (!organization.HasValue || !await ReplacementManagers(organization.Value, user).AnyAsync(ct))
                throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.SchoolManagerDependency);
        }
    }
    private const string AccountDeletionRequestPolicy = MakanApp.Domain.Identity.AccountDeletionRequest.CurrentPolicyVersion;
}
