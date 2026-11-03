using MakanApp.Application.Commerce;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Academic;

namespace MakanApp.Application.Academic;

public sealed class PersonalClassLifecycleService(CommercialActorAccess actors, IPersonalClassLifecycleStore store, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private Task Actor(PersonalClassActor actor, CancellationToken ct) => actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
    private static void Mutation(string version, string reason)
    { PersonalClassFormationService.ValidateVersion(version); PersonalClassFormationService.ValidateReason(reason); }
    public async Task<PersonalClassDto> ConfigureAsync(PersonalClassActor actor, Guid id, ConfigurePersonalClassCommand command, CancellationToken ct)
    {
        await Actor(actor, ct); Mutation(command.ExpectedVersion, command.Reason);
        if (command.Capacity <= 0) throw PersonalClassFormationService.Invalid();
        return await store.ConfigureAsync(actor, id, command, Now, ct);
    }
    public async Task<PersonalClassDto> StateAsync(PersonalClassActor actor, Guid id, PersonalClassReasonCommand command, PersonalClassState state, CancellationToken ct)
    { await Actor(actor, ct); Mutation(command.ExpectedVersion, command.Reason); return await store.StateAsync(actor, id, command, state, Now, ct); }
    public async Task<PersonalClassMembershipDto> RolesAsync(PersonalClassActor actor, Guid id, Guid memberId, ChangePersonalClassRolesCommand command, CancellationToken ct)
    { await Actor(actor, ct); Mutation(command.ExpectedVersion, command.Reason); return await store.RolesAsync(actor, id, memberId, command, Now, ct); }
    public async Task EndAsync(PersonalClassActor actor, Guid id, Guid memberId, PersonalClassReasonCommand command, CancellationToken ct)
    { await Actor(actor, ct); Mutation(command.ExpectedVersion, command.Reason); await store.EndAsync(actor, id, memberId, command, Now, ct); }
    public async Task<PersonalClassInvitationDto> CloseInvitationAsync(PersonalClassActor actor, Guid id, PersonalClassReasonCommand command, bool decline, CancellationToken ct)
    { await Actor(actor, ct); Mutation(command.ExpectedVersion, command.Reason); return await store.CloseInvitationAsync(actor, id, command, decline, Now, ct); }
    public async Task<PersonalClassJoinDto> RequestJoinAsync(PersonalClassActor actor, Guid id, CreatePersonalClassJoinCommand command, CancellationToken ct)
    { await Actor(actor, ct); if (command.ClientOperationId == Guid.Empty) throw PersonalClassFormationService.Invalid(); return await store.RequestJoinAsync(actor, id, command, Now, ct); }
    public async Task<PersonalClassJoinDto> DecideJoinAsync(PersonalClassActor actor, Guid id, DecidePersonalClassJoinCommand command, CancellationToken ct)
    {
        await Actor(actor, ct); Mutation(command.ExpectedVersion, command.Reason);
        if (command.Decision is not (PersonalClassJoinRequestStatus.Approved or PersonalClassJoinRequestStatus.Rejected or PersonalClassJoinRequestStatus.Cancelled)) throw PersonalClassFormationService.Invalid();
        return await store.DecideJoinAsync(actor, id, command, Now, ct);
    }
    public async Task<PersonalClassTransferDto> TransferAsync(PersonalClassActor actor, Guid id, CreatePersonalClassTransferCommand command, CancellationToken ct)
    {
        await Actor(actor, ct); Mutation(command.ExpectedVersion, command.Reason);
        if (command.TargetUserId == Guid.Empty || command.TargetUserId == actor.UserId || command.ClientOperationId == Guid.Empty) throw PersonalClassFormationService.Invalid();
        return await store.TransferAsync(actor, id, command, Now, ct);
    }
    public async Task<PersonalClassTransferDto> DecideTransferAsync(PersonalClassActor actor, Guid id, DecidePersonalClassTransferCommand command, CancellationToken ct)
    {
        await Actor(actor, ct); PersonalClassFormationService.ValidateVersion(command.ExpectedVersion);
        if (command.Decision is not (PersonalClassManagerTransferStatus.Accepted or PersonalClassManagerTransferStatus.Declined or PersonalClassManagerTransferStatus.Cancelled)) throw PersonalClassFormationService.Invalid();
        return await store.DecideTransferAsync(actor, id, command, Now, ct);
    }
    public async Task<PersonalClassPage<PersonalClassDto>> ClassesAsync(PersonalClassActor actor, int page, int size, CancellationToken ct)
    { await Actor(actor, ct); PersonalClassFormationService.ValidatePage(page, size); return await store.ClassesAsync(actor.UserId, page, size, ct); }
    public async Task<PersonalClassPage<PersonalClassMembershipDto>> MembersAsync(PersonalClassActor actor, Guid id, int page, int size, CancellationToken ct)
    { await Actor(actor, ct); PersonalClassFormationService.ValidatePage(page, size); return await store.MembersAsync(actor.UserId, id, page, size, ct); }
    public async Task<PersonalClassPage<PersonalClassInvitationDto>> InvitationsAsync(PersonalClassActor actor, int page, int size, CancellationToken ct)
    { await Actor(actor, ct); PersonalClassFormationService.ValidatePage(page, size); return await store.InvitationsPageAsync(actor.UserId, page, size, ct); }
    public async Task<PersonalClassPage<PersonalClassJoinDto>> JoinsAsync(PersonalClassActor actor, Guid? id, int page, int size, CancellationToken ct)
    { await Actor(actor, ct); PersonalClassFormationService.ValidatePage(page, size); return await store.JoinsAsync(actor.UserId, id, page, size, ct); }
    public async Task<PersonalClassPage<PersonalClassTransferDto>> TransfersAsync(PersonalClassActor actor, Guid? id, int page, int size, CancellationToken ct)
    { await Actor(actor, ct); PersonalClassFormationService.ValidatePage(page, size); return await store.TransfersAsync(actor.UserId, id, page, size, ct); }
    public async Task<OfficialSpacesPairResult> SpacesAsync(PersonalClassActor actor, Guid id, CancellationToken ct)
    { await Actor(actor, ct); return await store.SpacesAsync(actor.UserId, id, ct); }
    public async Task<PersonalClassBackfillPlanRow> BackfillPlanAsync(PersonalClassActor actor, Guid id, CancellationToken ct)
    { await Actor(actor, ct); return await store.BackfillPlanAsync(actor.UserId, id, ct); }
    public async Task RepairAsync(PersonalClassActor actor, Guid id, RepairPersonalClassSpacesCommand command, CancellationToken ct)
    { await Actor(actor, ct); Mutation(command.ExpectedVersion, command.Reason); await store.RepairAsync(actor, id, command, Now, ct); }
}
