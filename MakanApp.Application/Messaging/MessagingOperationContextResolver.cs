using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Messaging;

public interface IMessagingOperationContextStore
{
    Task<bool> HasActiveAccountSessionAsync(Guid userId, Guid sessionId, DateTime nowUtc, CancellationToken ct);
    Task<bool> IsPersonalParticipantAsync(Guid userId, Guid conversationId, CancellationToken ct);
    Task<bool> IsPersonalConversationAsync(Guid conversationId, CancellationToken ct) => Task.FromResult(false);
    Task<bool> IsPersonalInvitationAsync(Guid userId, Guid invitationId, CancellationToken ct) => Task.FromResult(false);
    Task<bool> IsPersonalJoinRequestAsync(Guid userId, Guid requestId, CancellationToken ct) => Task.FromResult(false);
    Task<bool> IsPersonalInviteLinkAsync(string token, CancellationToken ct) => Task.FromResult(false);
}

// این Resolver فقط بافت یک عملیات را انتخاب می‌کند؛ سیاست‌های فعلی Send/Read/Safety همچنان پس از آن اجرا می‌شوند.
public sealed class MessagingOperationContextResolver(IAccessContextResolver contexts,
    IMessagingOperationContextStore store, TimeProvider clock) : IAccountOperationContextResolver
{
    public async Task<AccessContext> PersonalAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        if (!await store.HasActiveAccountSessionAsync(userId, sessionId, clock.GetUtcNow().UtcDateTime, ct))
            throw new IdentityException(IdentityErrorCodes.AuthRequired, "برای ادامه باید وارد شوید.");
        return new(userId, userId, sessionId, WorkspaceType.Personal, null, null, null, null);
    }

    public async Task<AccessContext> ConversationAsync(Guid userId, Guid sessionId, Guid conversationId, CancellationToken ct)
    {
        if (await store.IsPersonalParticipantAsync(userId, conversationId, ct))
            return await PersonalAsync(userId, sessionId, ct);
        return await contexts.ResolveAsync(userId, sessionId, ct);
    }

    public Task<AccessContext> ScopeAsync(Guid userId, Guid sessionId, ConversationScope scope, CancellationToken ct) =>
        scope == ConversationScope.Personal ? PersonalAsync(userId, sessionId, ct) : contexts.ResolveAsync(userId, sessionId, ct);

    // تشخیص بافتِ دعوت/ورود مجوز عضویت نیست؛ Store همچنان رضایت، انقضا، رابطه، Block و Safety را بررسی می‌کند.
    public async Task<AccessContext> EntryAsync(Guid userId, Guid sessionId, Guid conversationId, CancellationToken ct) =>
        await store.IsPersonalConversationAsync(conversationId, ct)
            ? await PersonalAsync(userId, sessionId, ct) : await contexts.ResolveAsync(userId, sessionId, ct);
    public async Task<AccessContext> InvitationAsync(Guid userId, Guid sessionId, Guid invitationId, CancellationToken ct) =>
        await store.IsPersonalInvitationAsync(userId, invitationId, ct)
            ? await PersonalAsync(userId, sessionId, ct) : await contexts.ResolveAsync(userId, sessionId, ct);
    public async Task<AccessContext> JoinRequestAsync(Guid userId, Guid sessionId, Guid requestId, CancellationToken ct) =>
        await store.IsPersonalJoinRequestAsync(userId, requestId, ct)
            ? await PersonalAsync(userId, sessionId, ct) : await contexts.ResolveAsync(userId, sessionId, ct);
    public async Task<AccessContext> InviteLinkAsync(Guid userId, Guid sessionId, string token, CancellationToken ct) =>
        await store.IsPersonalInviteLinkAsync(token, ct)
            ? await PersonalAsync(userId, sessionId, ct) : await contexts.ResolveAsync(userId, sessionId, ct);

    public async Task<AccessContext> ForwardAsync(Guid userId, Guid sessionId, Guid sourceId, Guid destinationId, CancellationToken ct) =>
        await store.IsPersonalParticipantAsync(userId, sourceId, ct) && await store.IsPersonalParticipantAsync(userId, destinationId, ct)
            ? await PersonalAsync(userId, sessionId, ct) : await contexts.ResolveAsync(userId, sessionId, ct);

    public async Task<AccessContext> FolderAsync(Guid userId, Guid sessionId, IReadOnlyCollection<FolderOverrideCommand> overrides, CancellationToken ct)
    {
        foreach (var item in overrides)
            if (!await store.IsPersonalParticipantAsync(userId, item.ConversationId, ct))
                return await contexts.ResolveAsync(userId, sessionId, ct);
        return await PersonalAsync(userId, sessionId, ct);
    }
}
