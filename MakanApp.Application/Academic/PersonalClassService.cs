using MakanApp.Application.Commerce;
using MakanApp.Application.Organization;

namespace MakanApp.Application.Academic;

public sealed class PersonalClassService(CommercialActorAccess actors, IPersonalClassStore store, TimeProvider timeProvider)
{
    public async Task<PersonalClassDto> CreateAsync(Guid userId, Guid sessionId, CreatePersonalClassCommand command, CancellationToken ct)
    {
        await Actor(userId, sessionId, ct);
        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Trim().Length > 200 || command.ClientOperationId == Guid.Empty)
            throw CommercialSubscriptionService.Invalid();
        return await store.CreateAsync(userId, command with { Title = command.Title.Trim() }, Now(), ct);
    }
    public async Task<IReadOnlyList<PersonalClassDto>> ListAsync(Guid userId, Guid sessionId, CancellationToken ct)
    { await Actor(userId, sessionId, ct); return await store.ListAsync(userId, ct); }
    public async Task<PersonalClassDto> GetAsync(Guid userId, Guid sessionId, Guid classId, CancellationToken ct)
    { await Actor(userId, sessionId, ct); return await store.GetAsync(userId, classId, ct); }
    public async Task<PersonalClassDto> ArchiveAsync(Guid userId, Guid sessionId, Guid classId, CancellationToken ct)
    { await Actor(userId, sessionId, ct); return await store.ArchiveAsync(userId, classId, Now(), ct); }
    public async Task<PersonalClassInvitationDto> InviteAsync(Guid userId, Guid sessionId, Guid classId, InvitePersonalClassStudentCommand command, CancellationToken ct)
    {
        await Actor(userId, sessionId, ct);
        if (command.DestinationUserId == Guid.Empty || command.ClientOperationId == Guid.Empty || command.DestinationUserId == userId)
            throw CommercialSubscriptionService.Invalid();
        return await store.InviteAsync(userId, classId, command, Now(), ct);
    }
    public async Task<IReadOnlyList<PersonalClassInvitationDto>> InvitationsAsync(Guid userId, Guid sessionId, CancellationToken ct)
    { await Actor(userId, sessionId, ct); return await store.InvitationsAsync(userId, ct); }
    public async Task<PersonalClassDto> AcceptAsync(Guid userId, Guid sessionId, Guid invitationId, CancellationToken ct)
    { await Actor(userId, sessionId, ct); return await store.AcceptAsync(userId, invitationId, Now(), ct); }
    public async Task<IReadOnlyList<PersonalClassParticipantDto>> ParticipantsAsync(Guid userId, Guid sessionId, Guid classId, CancellationToken ct)
    { await Actor(userId, sessionId, ct); return await store.ParticipantsAsync(userId, classId, ct); }
    public async Task EndParticipantAsync(Guid userId, Guid sessionId, Guid classId, Guid participantId, CancellationToken ct)
    { await Actor(userId, sessionId, ct); await store.EndParticipantAsync(userId, classId, participantId, Now(), ct); }
    private Task Actor(Guid userId, Guid sessionId, CancellationToken ct) => actors.EnsureAsync(userId, sessionId, ct);
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
