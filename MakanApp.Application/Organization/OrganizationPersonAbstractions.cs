using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public sealed record OrganizationPersonRecord(OrganizationPerson Record, Person Person, bool IsAccountLinked, int ReferenceCount);

public interface IOrganizationPersonService
{
    Task<OrganizationPersonDto> CreateAsync(Guid userId, Guid sessionId, CreateOrganizationPersonCommand command, CancellationToken cancellationToken);
    Task<OrganizationPeoplePage> ListAsync(Guid userId, Guid sessionId, string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<OrganizationPersonDto> GetAsync(Guid userId, Guid sessionId, Guid id, CancellationToken cancellationToken);
    Task<OrganizationPersonDto> UpdateAsync(Guid userId, Guid sessionId, Guid id, UpdateOrganizationPersonNamesCommand command, CancellationToken cancellationToken);
    Task<OrganizationPersonDto> EndAsync(Guid userId, Guid sessionId, Guid id, OnboardingStateCommand command, CancellationToken cancellationToken);
    Task<PersonLinkInvitationDto> InviteAsync(Guid userId, Guid sessionId, Guid id, CreatePersonLinkInvitationCommand command, CancellationToken cancellationToken);
    Task<PersonLinkInvitationDto> RevokeInvitationAsync(Guid userId, Guid sessionId, Guid id, Guid invitationId, OnboardingStateCommand command, CancellationToken cancellationToken);
}

public interface IOrganizationPersonStore
{
    Task<IOrganizationTransaction> BeginAsync(CancellationToken cancellationToken);
    Task<OrganizationPersonRecord?> GetAsync(Guid organizationId, Guid id, bool forUpdate, CancellationToken cancellationToken);
    Task<OrganizationPersonRecord?> FindCreationAsync(Guid organizationId, Guid operationId, CancellationToken cancellationToken);
    Task<(int Total, IReadOnlyCollection<OrganizationPersonRecord> Items)> ListAsync(Guid organizationId, string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<bool> HasActiveEnrollmentsAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<User?> GetAvailableUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<Invitation?> FindLinkOperationAsync(Guid organizationId, Guid operationId, CancellationToken cancellationToken);
    Task<Invitation?> GetLinkInvitationAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<Invitation?> GetPendingLinkAsync(Guid organizationId, Guid personId, CancellationToken cancellationToken);
    void Add(Person person, OrganizationPerson organizationPerson);
    void Add(Invitation invitation);
    Task AuditAsync(AccessContext actor, string action, string targetType, Guid targetId, string reason, DateTime nowUtc, string? before, string? after, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
