using MakanApp.Domain.Academic;

namespace MakanApp.Application.Academic;

public sealed record PersonalClassRequestInput(string Title, string Purpose, string? Description, int SuggestedCapacity,
    Guid? SuggestedManagerUserId, Guid? SuggestedTeacherUserId);
public sealed record CreatePersonalClassRequestCommand(Guid ClientOperationId, PersonalClassRequestInput Details);
public sealed record EditPersonalClassRequestCommand(string ExpectedVersion, PersonalClassRequestInput Details);
public sealed record PersonalClassVersionCommand(string ExpectedVersion);
public sealed record PersonalClassReasonCommand(string ExpectedVersion, string Reason);
public sealed record ReviewPersonalClassRequestCommand(string ExpectedVersion, PersonalClassRequestStatus Decision,
    string Reason, Guid? FinalManagerUserId = null);
public sealed record PersonalClassPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record PersonalClassRequestCapabilities(bool CanEdit, bool CanSubmit, bool CanCancel, bool CanRespondToChanges);
public sealed record PersonalClassRequestDto(Guid Id, Guid RequesterUserId, PersonalClassRequestInput Details,
    PersonalClassRequestStatus Status, string Version, DateTime CreatedAtUtc, DateTime? SubmittedAtUtc,
    DateTime? DecidedAtUtc, string? DecisionReason, Guid? CreatedClassId, Guid? FinalManagerUserId,
    PersonalClassRequestCapabilities Capabilities)
{
    public PersonalClassFormationProgress? Progress { get; init; }
}
public sealed record PersonalClassFormationProgress(string ClassState, bool ManagerAccepted, string GroupState, string ChannelState);
public sealed record PersonalClassDecisionDto(Guid Id, string Action, string Reason, DateTime OccurredAtUtc, Guid? ActorUserId);
public sealed record PersonalClassActor(Guid UserId, Guid SessionId, string CorrelationId);

public interface IPersonalClassFormationStore
{
    Task<PersonalClassRequestDto> CreateAsync(PersonalClassActor actor, CreatePersonalClassRequestCommand command, DateTime now, CancellationToken ct);
    Task<PersonalClassPage<PersonalClassRequestDto>> ListAsync(Guid actor, bool platform, PersonalClassRequestStatus? status, int page, int pageSize, CancellationToken ct);
    Task<PersonalClassRequestDto> GetAsync(Guid actor, Guid id, bool platform, CancellationToken ct);
    Task<PersonalClassRequestDto> EditAsync(PersonalClassActor actor, Guid id, EditPersonalClassRequestCommand command, DateTime now, CancellationToken ct);
    Task<PersonalClassRequestDto> TransitionAsync(PersonalClassActor actor, Guid id, PersonalClassVersionCommand command, bool submit, DateTime now, CancellationToken ct);
    Task<PersonalClassRequestDto> ReviewAsync(PersonalClassActor actor, Guid id, ReviewPersonalClassRequestCommand command, DateTime now, CancellationToken ct);
    Task<PersonalClassPage<PersonalClassDecisionDto>> HistoryAsync(Guid actor, Guid id, bool platform, int page, int pageSize, CancellationToken ct);
}
