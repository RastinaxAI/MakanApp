using MakanApp.Application.Academic;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public sealed record GrantSchoolRoleCommand(OrganizationRole Role, Guid ClientOperationId, string ExpectedMembershipVersion, string Reason);
public sealed record ChangeSchoolRoleCommand(OrganizationRole ToRole, Guid ClientOperationId, string ExpectedMembershipVersion,
    string ExpectedRoleVersion, string Reason);
public sealed record EndSchoolRoleCommand(Guid ClientOperationId, string ExpectedMembershipVersion, string ExpectedRoleVersion, string Reason);
public sealed record EndSchoolMembershipCommand(Guid ClientOperationId, string ExpectedVersion, string Reason);
public sealed record SchoolMembershipMutationResult(Guid MembershipId, MembershipStatus Status, string MembershipVersion,
    IReadOnlyList<SchoolRoleAssignmentResult> ActiveRoleAssignments, Guid? ResultRoleAssignmentId,
    Guid ClientOperationId, DateTime OperationCompletedAtUtc, bool Replayed);

public sealed record SchoolMembershipMutation(OrganizationMembershipOperationKind Kind, Guid ClientOperationId,
    string ExpectedMembershipVersion, Guid? RoleAssignmentId, string? ExpectedRoleVersion, OrganizationRole? Role,
    string Reason, string CorrelationId);

public interface ISchoolMembershipManagementStore
{
    Task<SchoolMembershipMutationResult> ExecuteAsync(AccessContext actor, Guid membershipId,
        SchoolMembershipMutation command, CancellationToken cancellationToken);
}

public sealed class SchoolMembershipManagementService(SchoolManagementAccessResolver access, ISchoolMembershipManagementStore store)
{
    public Task<SchoolMembershipMutationResult> GrantAsync(Guid userId, Guid sessionId, Guid membershipId,
        GrantSchoolRoleCommand command, string correlationId, CancellationToken ct) => ExecuteAsync(userId, sessionId, membershipId,
        new(OrganizationMembershipOperationKind.GrantRole, command.ClientOperationId, command.ExpectedMembershipVersion,
            null, null, command.Role, command.Reason, correlationId), ct);

    public Task<SchoolMembershipMutationResult> ChangeAsync(Guid userId, Guid sessionId, Guid membershipId, Guid assignmentId,
        ChangeSchoolRoleCommand command, string correlationId, CancellationToken ct) => ExecuteAsync(userId, sessionId, membershipId,
        new(OrganizationMembershipOperationKind.ChangeRole, command.ClientOperationId, command.ExpectedMembershipVersion,
            assignmentId, command.ExpectedRoleVersion, command.ToRole, command.Reason, correlationId), ct);

    public Task<SchoolMembershipMutationResult> EndRoleAsync(Guid userId, Guid sessionId, Guid membershipId, Guid assignmentId,
        EndSchoolRoleCommand command, string correlationId, CancellationToken ct) => ExecuteAsync(userId, sessionId, membershipId,
        new(OrganizationMembershipOperationKind.EndRole, command.ClientOperationId, command.ExpectedMembershipVersion,
            assignmentId, command.ExpectedRoleVersion, null, command.Reason, correlationId), ct);

    public Task<SchoolMembershipMutationResult> EndMembershipAsync(Guid userId, Guid sessionId, Guid membershipId,
        EndSchoolMembershipCommand command, string correlationId, CancellationToken ct) => ExecuteAsync(userId, sessionId, membershipId,
        new(OrganizationMembershipOperationKind.EndMembership, command.ClientOperationId, command.ExpectedVersion,
            null, null, null, command.Reason, correlationId), ct);

    private async Task<SchoolMembershipMutationResult> ExecuteAsync(Guid userId, Guid sessionId, Guid membershipId,
        SchoolMembershipMutation command, CancellationToken ct)
    {
        var actor = await access.ResolveAsync(userId, sessionId, ct);
        if (membershipId == Guid.Empty || command.ClientOperationId == Guid.Empty || command.RoleAssignmentId == Guid.Empty ||
            command.Role.HasValue && !Enum.IsDefined(command.Role.Value))
            throw SchoolOnboardingValidation.Invalid("شناسه یا نقش معتبر لازم است.");
        command = command with
        {
            Reason = SchoolOnboardingValidation.Reason(command.Reason),
            ExpectedMembershipVersion = NormalizeVersion(command.ExpectedMembershipVersion),
            ExpectedRoleVersion = command.RoleAssignmentId.HasValue ? NormalizeVersion(command.ExpectedRoleVersion) : null
        };
        return await store.ExecuteAsync(actor, membershipId, command, ct);
    }

    private static string NormalizeVersion(string? version)
    {
        byte[] value;
        try { value = Convert.FromBase64String(version ?? string.Empty); }
        catch (FormatException) { throw SchoolOnboardingValidation.Invalid("نسخهٔ معتبر هشت‌بایتی لازم است."); }
        if (value.Length != 8) throw SchoolOnboardingValidation.Invalid("نسخهٔ معتبر هشت‌بایتی لازم است.");
        return Convert.ToBase64String(value);
    }
}

public static class SchoolMembershipErrorCodes
{
    public const string NotFound = "SCHOOL_MEMBERSHIP_NOT_FOUND";
    public const string RoleNotFound = "SCHOOL_ROLE_ASSIGNMENT_NOT_FOUND";
    public const string Conflict = "SCHOOL_MEMBERSHIP_STATE_CONFLICT";
    public const string OperationConflict = "SCHOOL_MEMBERSHIP_OPERATION_CONFLICT";
    public const string LastManagerRequired = "ORGANIZATION_LAST_MANAGER_REQUIRED";
}
