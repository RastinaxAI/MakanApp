using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record CreateConversationInvitationCommand(Guid TargetUserId, DateTime ExpiresAtUtc);
public sealed record ConversationInvitationResult(Guid Id, Guid ConversationId, Guid InviterUserId, Guid TargetUserId,
    ConversationInvitationStatus Status, DateTime ExpiresAtUtc);
public interface IConversationInvitationStore
{
    Task<ConversationInvitationResult> InviteAsync(Guid actorId, Guid sessionId, Guid conversationId, CreateConversationInvitationCommand command,
        AccessContext access, string correlationId, DateTime nowUtc, CancellationToken ct);
    Task<IReadOnlyCollection<ConversationInvitationResult>> ListInvitationsAsync(Guid actorId, DateTime nowUtc, CancellationToken ct);
    Task<ConversationInvitationResult> CompleteInvitationAsync(Guid actorId, Guid sessionId, Guid invitationId,
        ConversationInvitationStatus status, AccessContext access, string correlationId, DateTime nowUtc, CancellationToken ct);
}
public sealed class ConversationInvitationService(IConversationInvitationStore store, IAccessContextResolver access, TimeProvider clock)
{
    public async Task<ConversationInvitationResult> InviteAsync(Guid actorId, Guid sessionId, Guid conversationId,
        CreateConversationInvitationCommand command, string correlationId, CancellationToken ct)
    {
        try { _ = ConversationInvitation.Create(conversationId, actorId, command.TargetUserId, command.ExpiresAtUtc, clock.GetUtcNow().UtcDateTime); }
        catch (ArgumentException) { throw new MessagingException(MessagingErrorCodes.ManagedConversationInvalid, "دعوت معتبر نیست."); }
        return await store.InviteAsync(actorId, sessionId, conversationId, command, await access.ResolveAsync(actorId, sessionId, ct),
            correlationId, clock.GetUtcNow().UtcDateTime, ct);
    }
    public Task<IReadOnlyCollection<ConversationInvitationResult>> ListAsync(Guid actorId, CancellationToken ct) =>
        store.ListInvitationsAsync(actorId, clock.GetUtcNow().UtcDateTime, ct);
    public async Task<ConversationInvitationResult> CompleteAsync(Guid actorId, Guid sessionId, Guid invitationId,
        ConversationInvitationStatus status, string correlationId, CancellationToken ct) =>
        await store.CompleteInvitationAsync(actorId, sessionId, invitationId, status, await access.ResolveAsync(actorId, sessionId, ct),
            correlationId, clock.GetUtcNow().UtcDateTime, ct);
}
