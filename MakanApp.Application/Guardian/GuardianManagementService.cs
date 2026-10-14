using MakanApp.Application.Organization;
using MakanApp.Domain.Guardian;

namespace MakanApp.Application.Guardian;

public sealed class GuardianManagementService(SchoolManagementAccessResolver accessResolver,
    IOrganizationPersonStore personStore, IGuardianManagementStore store, TimeProvider clock) : IGuardianManagementService
{
    public async Task<SchoolGuardianRelationDto> CreateAsync(Guid userId, Guid sessionId,
        CreateSchoolGuardianRelationCommand command, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var reason = SchoolOnboardingValidation.Reason(command.Reason);
        await using var transaction = await personStore.BeginAsync(cancellationToken);
        var learner = await personStore.GetAsync(actor.OrganizationId!.Value, command.LearnerOrganizationPersonId, true, cancellationToken);
        if (learner is null || !learner.Record.IsActive ||
            !await store.IsActiveParentAsync(actor.OrganizationId.Value, command.GuardianUserId, cancellationToken))
            throw SchoolOnboardingValidation.NotFound();
        var existing = await store.FindCurrentAsync(actor.OrganizationId.Value, command.GuardianUserId, command.LearnerOrganizationPersonId, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return Map(existing, true);
        }
        var relation = GuardianRelation.CreatePending(actor.OrganizationId.Value, command.GuardianUserId, command.LearnerOrganizationPersonId, UtcNow());
        store.Add(relation);
        await personStore.AuditAsync(actor, "organization.guardian.create", "GuardianRelation", relation.Id, reason, UtcNow(), null, "status=Pending", cancellationToken);
        await personStore.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(relation);
    }

    public async Task<SchoolGuardianRelationsPage> ListAsync(Guid userId, Guid sessionId, Guid? learnerId,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        SchoolOnboardingValidation.Page(page, pageSize);
        if (learnerId.HasValue && await personStore.GetAsync(actor.OrganizationId!.Value, learnerId.Value, false, cancellationToken) is null)
            throw SchoolOnboardingValidation.NotFound();
        var now = UtcNow();
        var rows = await store.ListAsync(actor.OrganizationId!.Value, learnerId, page, pageSize, cancellationToken);
        return new(now, page, pageSize, rows.Total, rows.Items.Select(item => Map(item)).ToArray());
    }

    public async Task<SchoolGuardianRelationDto> GetAsync(Guid userId, Guid sessionId, Guid id, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.GetAsync(actor.OrganizationId!.Value, id, false, cancellationToken) ?? throw NotFound());
    }

    public async Task<SchoolGuardianRelationDto> TransitionAsync(Guid userId, Guid sessionId, Guid id,
        GuardianRelationStatus target, OnboardingStateCommand command, CancellationToken cancellationToken)
    {
        var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var reason = SchoolOnboardingValidation.Reason(command.Reason);
        var initial = await store.GetAsync(actor.OrganizationId!.Value, id, false, cancellationToken) ?? throw NotFound();
        await using var transaction = await personStore.BeginAsync(cancellationToken);
        // قفل learner پیش از relation، مانند مسیر ایجاد، از race و ترتیب معکوس قفل جلوگیری می‌کند.
        var learner = await personStore.GetAsync(actor.OrganizationId.Value, initial.LearnerOrganizationPersonId, true, cancellationToken) ?? throw NotFound();
        var relation = await store.GetAsync(actor.OrganizationId.Value, id, true, cancellationToken) ?? throw NotFound();
        if (relation.Status == target || target is GuardianRelationStatus.Ended or GuardianRelationStatus.Revoked &&
            relation.Status is GuardianRelationStatus.Ended or GuardianRelationStatus.Revoked)
        {
            await transaction.CommitAsync(cancellationToken);
            return Map(relation, true);
        }
        SchoolOnboardingValidation.Version(relation.RowVersion, command.ExpectedRowVersion);
        var previous = relation.Status;
        if (target == GuardianRelationStatus.Active)
        {
            if (relation.Status != GuardianRelationStatus.Pending || !learner.Record.IsActive ||
                !await store.IsActiveParentAsync(actor.OrganizationId.Value, relation.GuardianUserId, cancellationToken))
                throw new OrganizationException(SchoolOnboardingErrorCodes.GuardianStateConflict, "رابطه برای فعال‌سازی معتبر نیست.");
            relation.Activate(UtcNow());
        }
        else if (target == GuardianRelationStatus.Ended) relation.End(UtcNow());
        else if (target == GuardianRelationStatus.Revoked) relation.Revoke(UtcNow());
        else throw SchoolOnboardingValidation.Invalid("انتقال وضعیت معتبر نیست.");
        await personStore.AuditAsync(actor, $"organization.guardian.{target.ToString().ToLowerInvariant()}", "GuardianRelation", id,
            reason, UtcNow(), $"status={previous}", $"status={relation.Status}", cancellationToken);
        await personStore.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(relation);
    }
    private DateTime UtcNow() => clock.GetUtcNow().UtcDateTime;
    private static OrganizationException NotFound() => new(SchoolOnboardingErrorCodes.GuardianNotFound, "رابطه مجاز پیدا نشد.");
    private static SchoolGuardianRelationDto Map(GuardianRelation relation, bool replayed = false) =>
        new(relation.Id, relation.OrganizationId, relation.GuardianUserId, relation.LearnerOrganizationPersonId,
            relation.Status, relation.ValidFromUtc, relation.EndedAtUtc, Convert.ToBase64String(relation.RowVersion), replayed);
}
