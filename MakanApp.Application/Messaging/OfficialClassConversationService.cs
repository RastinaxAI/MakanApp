using MakanApp.Application.Organization;

namespace MakanApp.Application.Messaging;

public sealed record OfficialClassConversationResult(Guid ConversationId, Guid OrganizationId, Guid ClassId, int ActiveMemberCount);
public interface IOfficialClassConversationStore
{
    Task<OfficialClassConversationResult> ResolveClassAsync(Guid actorId, Guid classId, AccessContext access, DateTime nowUtc, CancellationToken ct);
}
public sealed class OfficialClassConversationService(IOfficialClassConversationStore store, IAccessContextResolver resolver, TimeProvider clock)
{
    public async Task<OfficialClassConversationResult> ResolveAsync(Guid actorId, Guid sessionId, Guid classId, CancellationToken ct) =>
        await store.ResolveClassAsync(actorId, classId, await resolver.ResolveAsync(actorId, sessionId, ct), clock.GetUtcNow().UtcDateTime, ct);
}
