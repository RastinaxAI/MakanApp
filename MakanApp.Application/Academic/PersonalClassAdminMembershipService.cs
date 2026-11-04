using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Academic;

public sealed record PlatformPersonalClassMembershipQuery(string? Search = null,
    PersonalClassLocalRoles? Role = null, bool ActiveOnly = false, int Page = 1, int PageSize = 30);

public sealed record PlatformPersonalClassMembershipDto(Guid MembershipId, Guid PersonalClassId,
    Guid UserId, string DisplayName, string? Username, PersonalClassLocalRoles LocalRoles,
    string Status, DateTime JoinedAtUtc, DateTime? EndedAtUtc, string MembershipVersion,
    bool CanReceivePlatformAcademicGrant);

public interface IPersonalClassAdminMembershipStore
{
    Task<PersonalClassPage<PlatformPersonalClassMembershipDto>> ReadAsync(PersonalClassActor actor,
        Guid classId, PlatformPersonalClassMembershipQuery query, CancellationToken cancellationToken);
}

public sealed class PersonalClassAdminMembershipService(CommercialActorAccess actors,
    IPlatformPermissionResolver permissions, IPersonalClassAdminMembershipStore store)
{
    public async Task<PersonalClassPage<PlatformPersonalClassMembershipDto>> ReadAsync(PersonalClassActor actor,
        Guid classId, PlatformPersonalClassMembershipQuery query, CancellationToken cancellationToken)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, cancellationToken);
        if (!await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.PersonalClassesRead, cancellationToken))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "مجوز جاری خواندن metadata کلاس مستقل لازم است.");
        if (classId == Guid.Empty) throw PersonalClassFormationService.Invalid();
        return await store.ReadAsync(actor, classId, Normalize(query), cancellationToken);
    }

    public static PlatformPersonalClassMembershipQuery Normalize(PlatformPersonalClassMembershipQuery query)
    {
        PersonalClassFormationService.ValidatePage(query.Page, query.PageSize);
        var search = query.Search?.Trim();
        const PersonalClassLocalRoles allRoles = PersonalClassLocalRoles.Learner | PersonalClassLocalRoles.Teacher | PersonalClassLocalRoles.Manager;
        if (search?.Length > 100 || query.Role.HasValue &&
            (query.Role.Value == PersonalClassLocalRoles.None || (query.Role.Value & ~allRoles) != 0))
            throw PersonalClassFormationService.Invalid();
        return query with { Search = string.IsNullOrWhiteSpace(search) ? null : search };
    }
}
