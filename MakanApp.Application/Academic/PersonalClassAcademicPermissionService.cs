using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Academic;

public sealed record SetPersonalClassAcademicPermissionCommand(PersonalClassAcademicCapabilities Capabilities,
    Guid ClientOperationId, string ExpectedMembershipVersion, string? ExpectedGrantVersion, string Reason);
public sealed record PersonalClassAcademicPermissionDto(Guid Id, Guid PersonalClassId, Guid MembershipId,
    PersonalClassAcademicCapabilities Capabilities, Guid? ParentGrantId, bool IsEffective, DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc, string Version);
public interface IPersonalClassAcademicPermissionStore
{
    Task<PersonalClassAcademicPermissionDto> SetAsync(PersonalClassActor actor, Guid classId, Guid membershipId,
        SetPersonalClassAcademicPermissionCommand command, bool platform, DateTime now, CancellationToken ct);
    Task<PersonalClassPage<PersonalClassAcademicPermissionDto>> ListAsync(PersonalClassActor actor, Guid classId,
        bool platform, int page, int pageSize, CancellationToken ct);
}

public sealed class PersonalClassAcademicPermissionService(CommercialActorAccess actors, IPlatformPermissionResolver permissions,
    IPersonalClassAcademicPermissionStore store, TimeProvider clock)
{
    public async Task<PersonalClassAcademicPermissionDto> SetAsync(PersonalClassActor actor, Guid classId, Guid membershipId,
        SetPersonalClassAcademicPermissionCommand command, bool platform, CancellationToken ct)
    {
        await Authorize(actor, platform, ct);
        if (classId == Guid.Empty || membershipId == Guid.Empty || command.ClientOperationId == Guid.Empty || ((int)command.Capabilities & ~7) != 0)
            throw PersonalClassFormationService.Invalid();
        PersonalClassFormationService.ValidateReason(command.Reason);
        PersonalClassFormationService.ValidateVersion(command.ExpectedMembershipVersion);
        if (command.ExpectedGrantVersion is not null) PersonalClassFormationService.ValidateVersion(command.ExpectedGrantVersion);
        return await store.SetAsync(actor, classId, membershipId, command, platform, clock.GetUtcNow().UtcDateTime, ct);
    }

    public async Task<PersonalClassPage<PersonalClassAcademicPermissionDto>> ListAsync(PersonalClassActor actor, Guid classId,
        bool platform, int page, int pageSize, CancellationToken ct)
    {
        await Authorize(actor, platform, ct); PersonalClassFormationService.ValidatePage(page, pageSize);
        return await store.ListAsync(actor, classId, platform, page, pageSize, ct);
    }

    private async Task Authorize(PersonalClassActor actor, bool platform, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (platform && !await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.PersonalClassesAcademicPermissions, ct))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "مجوز مستقل مدیریت اختیار آموزشی لازم است.");
    }
}
