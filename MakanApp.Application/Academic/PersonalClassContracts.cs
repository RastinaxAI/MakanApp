namespace MakanApp.Application.Academic;

public sealed record CreatePersonalClassCommand(string Title, Guid ClientOperationId);
public sealed record InvitePersonalClassStudentCommand(Guid DestinationUserId, Guid ClientOperationId);
public sealed record PersonalClassDto(Guid Id, Guid HostUserId, string Title, bool Archived, bool IsHost,
    bool CanCreateEducationalContent, string EducationalWorkflowStatus, DateTime CreatedAtUtc);
public sealed record PersonalClassInvitationDto(Guid Id, Guid PersonalClassId, Guid DestinationUserId, DateTime ExpiresAtUtc, bool Accepted);
public sealed record PersonalClassParticipantDto(Guid Id, Guid StudentUserId, DateTime JoinedAtUtc, bool Active);

public interface IPersonalClassStore
{
    Task<PersonalClassDto> CreateAsync(Guid hostUserId, CreatePersonalClassCommand command, DateTime utcNow, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersonalClassDto>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<PersonalClassDto> GetAsync(Guid userId, Guid classId, CancellationToken cancellationToken);
    Task<PersonalClassDto> ArchiveAsync(Guid userId, Guid classId, DateTime utcNow, CancellationToken cancellationToken);
    Task<PersonalClassInvitationDto> InviteAsync(Guid hostUserId, Guid classId, InvitePersonalClassStudentCommand command,
        DateTime utcNow, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersonalClassInvitationDto>> InvitationsAsync(Guid userId, CancellationToken cancellationToken);
    Task<PersonalClassDto> AcceptAsync(Guid userId, Guid invitationId, DateTime utcNow, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersonalClassParticipantDto>> ParticipantsAsync(Guid userId, Guid classId, CancellationToken cancellationToken);
    Task EndParticipantAsync(Guid userId, Guid classId, Guid participantId, DateTime utcNow, CancellationToken cancellationToken);
}
