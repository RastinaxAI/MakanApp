using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Academic;

public sealed record PersonalClassHistoryItem(Guid Id, string Action, string ResourceType, Guid? ResourceId,
    DateTime OccurredAtUtc, Guid? ActorUserId, string? CorrelationId);

public interface IPersonalClassHistoryStore
{
    Task<PersonalClassPage<PersonalClassHistoryItem>> ReadAsync(PersonalClassActor actor, Guid classId,
        bool platform, int page, int pageSize, CancellationToken ct);
}

public sealed class PersonalClassHistoryService(CommercialActorAccess actors, IPlatformPermissionResolver permissions,
    IPersonalClassHistoryStore store)
{
    public async Task<PersonalClassPage<PersonalClassHistoryItem>> ReadAsync(PersonalClassActor actor, Guid classId,
        bool platform, int page, int pageSize, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        PersonalClassFormationService.ValidatePage(page, pageSize);
        if (platform && !await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.PersonalClassesRead, ct))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "مجوز جاری مشاهدهٔ metadata کلاس لازم است.");
        return await store.ReadAsync(actor, classId, platform, page, pageSize, ct);
    }
}
