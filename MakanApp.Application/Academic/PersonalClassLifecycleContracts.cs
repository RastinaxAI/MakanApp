using MakanApp.Domain.Academic;
using MakanApp.Application.Messaging;

namespace MakanApp.Application.Academic;

public sealed record ConfigurePersonalClassCommand(string ExpectedVersion, int Capacity, bool AcceptingJoinRequests, string Reason);
public sealed record RepairPersonalClassSpacesCommand(string ExpectedVersion, string Reason, string? ApprovedPlanSha256 = null);
public sealed record ChangePersonalClassRolesCommand(string ExpectedVersion, PersonalClassLocalRoles Roles,
    PersonalClassDelegatedCapabilities DelegatedCapabilities, string Reason);
public sealed record CreatePersonalClassJoinCommand(Guid ClientOperationId);
public sealed record DecidePersonalClassJoinCommand(string ExpectedVersion, PersonalClassJoinRequestStatus Decision, string Reason);
public sealed record CreatePersonalClassTransferCommand(string ExpectedVersion, Guid TargetUserId, Guid ClientOperationId, string Reason);
public sealed record DecidePersonalClassTransferCommand(string ExpectedVersion, PersonalClassManagerTransferStatus Decision);
public sealed record PersonalClassMembershipDto(Guid Id, Guid UserId, PersonalClassLocalRoles LocalRoles,
    PersonalClassDelegatedCapabilities DelegatedCapabilities, string Status, DateTime JoinedAtUtc, DateTime? EndedAtUtc, string? EndReason, string Version);
public sealed record PersonalClassJoinDto(Guid Id, Guid PersonalClassId, Guid UserId, PersonalClassJoinRequestStatus Status,
    DateTime CreatedAtUtc, DateTime? ClosedAtUtc, string? ClosureReason, Guid? MembershipId, string Version);
public sealed record PersonalClassTransferDto(Guid Id, Guid PersonalClassId, Guid TargetUserId, Guid? PreviousManagerUserId,
    PersonalClassManagerTransferStatus Status, string Reason, DateTime CreatedAtUtc, string Version);

public interface IPersonalClassLifecycleStore
{
    Task<PersonalClassDto> ConfigureAsync(PersonalClassActor actor, Guid id, ConfigurePersonalClassCommand command, DateTime now, CancellationToken ct);
    Task<PersonalClassDto> StateAsync(PersonalClassActor actor, Guid id, PersonalClassReasonCommand command, PersonalClassState state, DateTime now, CancellationToken ct);
    Task<PersonalClassMembershipDto> RolesAsync(PersonalClassActor actor, Guid id, Guid memberId, ChangePersonalClassRolesCommand command, DateTime now, CancellationToken ct);
    Task EndAsync(PersonalClassActor actor, Guid id, Guid memberId, PersonalClassReasonCommand command, DateTime now, CancellationToken ct);
    Task<PersonalClassInvitationDto> CloseInvitationAsync(PersonalClassActor actor, Guid id, PersonalClassReasonCommand command, bool decline, DateTime now, CancellationToken ct);
    Task<PersonalClassJoinDto> RequestJoinAsync(PersonalClassActor actor, Guid id, CreatePersonalClassJoinCommand command, DateTime now, CancellationToken ct);
    Task<PersonalClassJoinDto> DecideJoinAsync(PersonalClassActor actor, Guid id, DecidePersonalClassJoinCommand command, DateTime now, CancellationToken ct);
    Task<PersonalClassTransferDto> TransferAsync(PersonalClassActor actor, Guid id, CreatePersonalClassTransferCommand command, DateTime now, CancellationToken ct);
    Task<PersonalClassTransferDto> DecideTransferAsync(PersonalClassActor actor, Guid id, DecidePersonalClassTransferCommand command, DateTime now, CancellationToken ct);
    Task<PersonalClassPage<PersonalClassDto>> ClassesAsync(Guid actor, int page, int size, CancellationToken ct);
    Task<PersonalClassPage<PersonalClassMembershipDto>> MembersAsync(Guid actor, Guid id, int page, int size, CancellationToken ct);
    Task<PersonalClassPage<PersonalClassInvitationDto>> InvitationsPageAsync(Guid actor, int page, int size, CancellationToken ct);
    Task<PersonalClassPage<PersonalClassJoinDto>> JoinsAsync(Guid actor, Guid? id, int page, int size, CancellationToken ct);
    Task<PersonalClassPage<PersonalClassTransferDto>> TransfersAsync(Guid actor, Guid? id, int page, int size, CancellationToken ct);
    Task<OfficialSpacesPairResult> SpacesAsync(Guid actor, Guid id, CancellationToken ct);
    Task<PersonalClassBackfillPlanRow> BackfillPlanAsync(Guid actor, Guid id, CancellationToken ct);
    Task RepairAsync(PersonalClassActor actor, Guid id, RepairPersonalClassSpacesCommand command, DateTime now, CancellationToken ct);
}
