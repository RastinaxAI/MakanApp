namespace MakanApp.Application.Academic;

public sealed record CreatePersonalClassCommand(string Title, Guid ClientOperationId);
public sealed record InvitePersonalClassStudentCommand(Guid DestinationUserId, Guid ClientOperationId,
    MakanApp.Domain.Academic.PersonalClassLocalRoles LocalRoles = MakanApp.Domain.Academic.PersonalClassLocalRoles.Learner,
    MakanApp.Domain.Academic.PersonalClassDelegatedCapabilities DelegatedCapabilities = MakanApp.Domain.Academic.PersonalClassDelegatedCapabilities.None);
public sealed record PersonalClassDto(Guid Id, Guid HostUserId, string Title, bool Archived, bool IsHost,
    bool CanCreateEducationalContent, string EducationalWorkflowStatus, DateTime CreatedAtUtc)
{
    public Guid? ManagerUserId { get; init; }
    public int? Capacity { get; init; }
    public bool AcceptingJoinRequests { get; init; }
    public string State { get; init; } = string.Empty;
    public string MigrationState { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public PersonalClassCapabilities Capabilities { get; init; } = new(false, false, false, false, false, false, false, false);
}
public sealed record PersonalClassCapabilities(bool CanManageMembers, bool CanManageRoles, bool CanManageSpaces,
    bool CanAuthorAssignments, bool CanAuthorExams, bool CanGrade, bool CanArchive, bool CanViewOwnResults);
public sealed record PersonalClassInviterIdentity(Guid UserId, string DisplayName, string? Username);
public sealed record PersonalClassInvitationDto(Guid Id, Guid PersonalClassId, Guid DestinationUserId, DateTime ExpiresAtUtc, bool Accepted)
{
    public string? ClassTitle { get; init; }
    public PersonalClassInviterIdentity? Inviter { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public MakanApp.Domain.Academic.PersonalClassLocalRoles LocalRoles { get; init; }
    public MakanApp.Domain.Academic.PersonalClassDelegatedCapabilities DelegatedCapabilities { get; init; }
}
public sealed record PersonalClassParticipantDto(Guid Id, Guid StudentUserId, DateTime JoinedAtUtc, bool Active);

public interface IPersonalClassStore
{
    Task<PersonalClassDto> CreateAsync(Guid hostUserId, CreatePersonalClassCommand command, DateTime utcNow, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersonalClassDto>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<PersonalClassDto> GetAsync(Guid userId, Guid classId, CancellationToken cancellationToken);
    Task<PersonalClassDto> ArchiveAsync(Guid userId, Guid classId, DateTime utcNow, CancellationToken cancellationToken);
    Task<PersonalClassInvitationDto> InviteAsync(Guid hostUserId, Guid classId, InvitePersonalClassStudentCommand command,
        DateTime utcNow, CancellationToken cancellationToken, Guid? actorSessionId = null);
    Task<IReadOnlyList<PersonalClassInvitationDto>> InvitationsAsync(Guid userId, CancellationToken cancellationToken);
    Task<PersonalClassDto> AcceptAsync(Guid userId, Guid invitationId, DateTime utcNow, CancellationToken cancellationToken, Guid? actorSessionId = null);
    Task<IReadOnlyList<PersonalClassParticipantDto>> ParticipantsAsync(Guid userId, Guid classId, CancellationToken cancellationToken);
    Task EndParticipantAsync(Guid userId, Guid classId, Guid participantId, DateTime utcNow, CancellationToken cancellationToken);
}
