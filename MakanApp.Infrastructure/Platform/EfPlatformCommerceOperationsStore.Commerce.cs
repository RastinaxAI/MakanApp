using System.Data;
using System.Security.Cryptography;
using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed partial class EfPlatformCommerceOperationsStore
{
    public async Task<PlatformPlanPage> GetPlansAsync(
        PlatformPlanQuery query,
        CancellationToken cancellationToken)
    {
        var plans = _dbContext.Plans.AsNoTracking();
        if (query.Search is not null)
        {
            plans = plans.Where(plan =>
                plan.Code.Contains(query.Search) || plan.DisplayName.Contains(query.Search));
        }

        var totalCount = await plans.CountAsync(cancellationToken);
        var items = await plans
            .OrderBy(plan => plan.Code)
            .ThenBy(plan => plan.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(plan => new PlatformPlanItem(
                plan.Id,
                plan.Code,
                plan.DisplayName,
                _dbContext.PlanVersions.Count(version => version.PlanId == plan.Id),
                _dbContext.PlanVersions
                    .Where(version => version.PlanId == plan.Id && version.Status == PlanVersionStatus.Published)
                    .Max(version => (int?)version.VersionNumber),
                plan.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new PlatformPlanPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformPlanDetail?> GetPlanAsync(
        Guid planId,
        CancellationToken cancellationToken)
    {
        var plan = await _dbContext.Plans.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == planId, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var versions = await _dbContext.PlanVersions.AsNoTracking()
            .Where(version => version.PlanId == planId)
            .OrderByDescending(version => version.VersionNumber)
            .ToArrayAsync(cancellationToken);
        var mapped = new List<PlatformPlanVersionItem>(versions.Length);
        foreach (var version in versions)
        {
            mapped.Add(await MapPlanVersionAsync(version, cancellationToken));
        }

        return new PlatformPlanDetail(
            plan.Id,
            plan.Code,
            plan.DisplayName,
            plan.CreatedAtUtc,
            mapped);
    }

    public async Task<PlatformPlanDetail> CreatePlanAsync(
        PlatformActorContext actor,
        CreatePlatformPlanCommand command,
        CancellationToken cancellationToken)
    {
        Plan plan;
        try
        {
            plan = Plan.Create(command.Code, command.DisplayName, actor.OccurredAtUtc);
        }
        catch (ArgumentException)
        {
            throw InvalidQuery();
        }

        _dbContext.Plans.Add(plan);
        _dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.commerce.plan.create",
            "Plan",
            plan.Id.ToString("D"),
            null,
            command.Reason,
            null,
            $"Code={plan.Code}",
            cancellationToken));
        await SaveCommerceAsync(cancellationToken);
        return (await GetPlanAsync(plan.Id, cancellationToken))!;
    }

    public async Task<PlatformPlanVersionItem> CreatePlanVersionAsync(
        PlatformActorContext actor,
        Guid planId,
        CreatePlatformPlanVersionCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var plan = await _dbContext.Plans
            .FromSqlInterpolated($"SELECT * FROM [commerce].[Plans] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {planId}")
            .SingleOrDefaultAsync(cancellationToken) ?? throw PlanNotFound();
        var versionNumber = (await _dbContext.PlanVersions
            .Where(version => version.PlanId == planId)
            .MaxAsync(version => (int?)version.VersionNumber, cancellationToken) ?? 0) + 1;
        PlanVersion version;
        try
        {
            version = PlanVersion.CreateDraft(plan.Id, versionNumber, actor.OccurredAtUtc);
            version.SetPrice(command.PriceAmount, command.Currency);
            foreach (var entitlement in command.Entitlements)
            {
                version.SetEntitlement(entitlement.Key, entitlement.Enabled);
            }

            foreach (var quota in command.Quotas)
            {
                version.SetQuota(quota.Key, quota.Limit);
            }
        }
        catch (ArgumentException)
        {
            throw InvalidQuery();
        }

        _dbContext.PlanVersions.Add(version);
        _dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.commerce.plan-version.create",
            "PlanVersion",
            version.Id.ToString("D"),
            null,
            command.Reason,
            null,
            $"PlanId={plan.Id:D};Version={version.VersionNumber};Currency=IRR",
            cancellationToken));
        await SaveCommerceAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await MapPlanVersionAsync(version, cancellationToken);
    }

    public async Task<PlatformPlanVersionItem> PublishPlanVersionAsync(
        PlatformActorContext actor,
        Guid planVersionId,
        byte[] expectedVersion,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var version = await _dbContext.PlanVersions
            .FromSqlInterpolated($"SELECT * FROM [commerce].[PlanVersions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {planVersionId}")
            .SingleOrDefaultAsync(cancellationToken) ?? throw PlanNotFound();
        EnsureVersion(version, expectedVersion);
        try
        {
            version.Publish(actor.OccurredAtUtc);
        }
        catch (InvalidOperationException)
        {
            throw PlanConflict();
        }

        _dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.commerce.plan-version.publish",
            "PlanVersion",
            version.Id.ToString("D"),
            null,
            reason,
            "Draft",
            "Published",
            cancellationToken));
        await SaveCommerceAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await MapPlanVersionAsync(version, cancellationToken);
    }

    public async Task<PlatformSubscriptionPage> GetSubscriptionsAsync(
        PlatformSubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        var subscriptions = _dbContext.Subscriptions.AsNoTracking();
        if (query.OrganizationId.HasValue)
        {
            subscriptions = subscriptions.Where(item => item.OrganizationId == query.OrganizationId.Value);
        }

        if (query.Status.HasValue)
        {
            subscriptions = subscriptions.Where(item => item.Status == query.Status.Value);
        }

        var totalCount = await subscriptions.CountAsync(cancellationToken);
        var rows = await subscriptions
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var items = new List<PlatformSubscriptionItem>(rows.Length);
        foreach (var row in rows)
        {
            items.Add(await MapSubscriptionAsync(row, cancellationToken));
        }

        return new PlatformSubscriptionPage(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformSubscriptionOverrideResult> OverrideSubscriptionAsync(
        PlatformActorContext actor,
        Guid organizationId,
        OverridePlatformSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableAsync(cancellationToken);
        var organizationExists = await _dbContext.Organizations
            .FromSqlInterpolated($"SELECT * FROM [organization].[Organizations] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {organizationId}")
            .AnyAsync(cancellationToken);
        if (!organizationExists)
        {
            throw new PlatformException(PlatformErrorCodes.OrganizationNotFound, "سازمان پیدا نشد.");
        }

        var planVersion = await _dbContext.PlanVersions.AsNoTracking()
            .SingleOrDefaultAsync(version =>
                version.Id == command.PlanVersionId &&
                version.Status == PlanVersionStatus.Published,
                cancellationToken) ?? throw PlanNotFound();
        var current = await _dbContext.Subscriptions
            .FromSqlInterpolated($"SELECT * FROM [commerce].[Subscriptions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Status] = 2")
            .SingleOrDefaultAsync(cancellationToken);
        if (current?.PlanVersionId == planVersion.Id)
        {
            throw SubscriptionConflict();
        }

        Guid? replacedId = null;
        if (current is not null)
        {
            replacedId = current.Id;
            current.Cancel(actor.OccurredAtUtc);
            await SaveCommerceAsync(cancellationToken);
        }

        var subscription = Subscription.CreatePending(
            organizationId,
            planVersion,
            actor.OccurredAtUtc,
            command.EndsAtUtc,
            actor.OccurredAtUtc);
        subscription.Activate(actor.OccurredAtUtc);
        _dbContext.Subscriptions.Add(subscription);
        _dbContext.AdminAuditLogs.Add(await CreateAuditAsync(
            actor,
            "platform.commerce.subscription.override",
            "Subscription",
            subscription.Id.ToString("D"),
            organizationId,
            command.Reason,
            replacedId.HasValue ? $"SubscriptionId={replacedId:D}" : "None",
            $"PlanVersionId={planVersion.Id:D}",
            cancellationToken));
        await SaveCommerceAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlatformSubscriptionOverrideResult(
            await MapSubscriptionAsync(subscription, cancellationToken),
            replacedId);
    }

    private async Task<PlatformPlanVersionItem> MapPlanVersionAsync(
        PlanVersion version,
        CancellationToken cancellationToken)
    {
        var price = await _dbContext.PlanVersionPrices.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PlanVersionId == version.Id, cancellationToken);
        var entitlements = await _dbContext.PlanEntitlements.AsNoTracking()
            .Where(item => item.PlanVersionId == version.Id)
            .OrderBy(item => item.Key)
            .Select(item => new PlatformPlanEntitlementItem(item.Key, item.Enabled))
            .ToArrayAsync(cancellationToken);
        var quotas = await _dbContext.PlanQuotaDefinitions.AsNoTracking()
            .Where(item => item.PlanVersionId == version.Id)
            .OrderBy(item => item.Key)
            .Select(item => new PlatformPlanQuotaItem(item.Key, item.Limit))
            .ToArrayAsync(cancellationToken);
        var subscriptions = await _dbContext.Subscriptions.CountAsync(
            item => item.PlanVersionId == version.Id,
            cancellationToken);
        return new PlatformPlanVersionItem(
            version.Id,
            version.VersionNumber,
            version.Status,
            price?.Amount,
            price?.Currency,
            entitlements,
            quotas,
            subscriptions,
            version.CreatedAtUtc,
            version.PublishedAtUtc,
            Convert.ToBase64String(version.RowVersion));
    }

    private async Task<PlatformSubscriptionItem> MapSubscriptionAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        var source = await (
            from version in _dbContext.PlanVersions.AsNoTracking()
            join plan in _dbContext.Plans.AsNoTracking() on version.PlanId equals plan.Id
            join organization in _dbContext.Organizations.AsNoTracking()
                on subscription.OrganizationId equals organization.Id
            where version.Id == subscription.PlanVersionId
            select new { version, plan, organization })
            .SingleAsync(cancellationToken);
        var quotas = await _dbContext.PlanQuotaDefinitions.AsNoTracking()
            .Where(item => item.PlanVersionId == subscription.PlanVersionId)
            .OrderBy(item => item.Key)
            .ToArrayAsync(cancellationToken);
        var usage = await _commerceStore.GetCurrentUsageAsync(
            subscription.OrganizationId,
            subscription.Id,
            quotas.Select(item => item.Key).ToArray(),
            cancellationToken);
        return new PlatformSubscriptionItem(
            subscription.Id,
            new PlatformOrganizationContext(source.organization.Id, source.organization.Name),
            source.plan.Id,
            source.plan.Code,
            source.version.Id,
            source.version.VersionNumber,
            subscription.Status,
            subscription.StartsAtUtc,
            subscription.EndsAtUtc,
            quotas.Select(item => new PlatformSubscriptionUsageItem(
                item.Key,
                item.Limit,
                usage.GetValueOrDefault(item.Key),
                item.Limit.HasValue && usage.GetValueOrDefault(item.Key) > item.Limit.Value)).ToArray(),
            subscription.CreatedAtUtc);
    }

    private void EnsureVersion(PlanVersion version, byte[] expectedVersion)
    {
        if (!CryptographicOperations.FixedTimeEquals(version.RowVersion, expectedVersion))
        {
            throw PlanConflict();
        }

        _dbContext.Entry(version).Property(item => item.RowVersion).OriginalValue = expectedVersion;
    }

    private static PlatformException PlanNotFound() =>
        new(PlatformErrorCodes.PlanNotFound, "پلن یا نسخه پلن پیدا نشد.");

    private static PlatformException PlanConflict() =>
        new(PlatformErrorCodes.PlanConflict, "وضعیت پلن یا نسخه آن قابل تغییر نیست یا هم‌زمان تغییر کرده است.");

    private static PlatformException SubscriptionConflict() =>
        new(PlatformErrorCodes.SubscriptionConflict, "تغییر اشتراک با وضعیت جاری سازگار نیست.");

    private static PlatformException InvalidQuery() =>
        new(PlatformErrorCodes.QueryInvalid, "پارامترهای درخواست معتبر نیستند.");
}
