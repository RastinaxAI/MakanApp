using MakanApp.Application.Academic;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public enum SchoolInvitationType { Membership = 1, PersonLink = 2, GuardianLink = 3 }
public sealed record SchoolInvitationQuery(InvitationStatus? Status = null, SchoolInvitationType? InvitationType = null,
    int Page = 1, int PageSize = 50);
public sealed record SchoolSentInvitationDto(Guid InvitationId, SchoolInvitationType InvitationType, Guid OrganizationId,
    Guid DestinationUserId, string TargetDisplayName, string? TargetUsername, Guid? OrganizationPersonId,
    Guid? LearnerOrganizationPersonId, OrganizationRole Role, InvitationStatus Status, DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc, string Version, IReadOnlyList<string> AllowedActions);
public interface ISchoolInvitationReadStore
{
    Task<SchoolPage<SchoolSentInvitationDto>> ReadAsync(AccessContext context, SchoolInvitationQuery query, DateTime now, CancellationToken ct);
}
public sealed class SchoolInvitationReadService(SchoolManagementAccessResolver access, ISchoolInvitationReadStore store, TimeProvider clock)
{
    public async Task<SchoolPage<SchoolSentInvitationDto>> ReadAsync(Guid userId, Guid sessionId, SchoolInvitationQuery query, CancellationToken ct)
    {
        var context = await access.ResolveAsync(userId, sessionId, ct);
        SchoolOnboardingValidation.Page(query.Page, query.PageSize);
        SchoolQueryValidation.EnumValue(query.Status); SchoolQueryValidation.EnumValue(query.InvitationType);
        return await store.ReadAsync(context, query, clock.GetUtcNow().UtcDateTime, ct);
    }
}
