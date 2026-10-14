using MakanApp.Application.Organization;
using MakanApp.Domain.Guardian;

namespace MakanApp.Application.Guardian;

public interface IGuardianManagementService
{
    Task<SchoolGuardianRelationDto> CreateAsync(Guid userId, Guid sessionId, CreateSchoolGuardianRelationCommand command, CancellationToken cancellationToken);
    Task<SchoolGuardianRelationsPage> ListAsync(Guid userId, Guid sessionId, Guid? learnerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<SchoolGuardianRelationDto> GetAsync(Guid userId, Guid sessionId, Guid id, CancellationToken cancellationToken);
    Task<SchoolGuardianRelationDto> TransitionAsync(Guid userId, Guid sessionId, Guid id, GuardianRelationStatus target, OnboardingStateCommand command, CancellationToken cancellationToken);
}

public interface IGuardianManagementStore
{
    Task<GuardianRelation?> GetAsync(Guid organizationId, Guid id, bool forUpdate, CancellationToken cancellationToken);
    Task<GuardianRelation?> FindCurrentAsync(Guid organizationId, Guid guardianId, Guid learnerId, CancellationToken cancellationToken);
    Task<bool> IsActiveParentAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<(int Total, IReadOnlyCollection<GuardianRelation> Items)> ListAsync(Guid organizationId, Guid? learnerId, int page, int pageSize, CancellationToken cancellationToken);
    void Add(GuardianRelation relation);
}
