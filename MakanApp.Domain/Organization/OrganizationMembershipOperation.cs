namespace MakanApp.Domain.Organization;

public enum OrganizationMembershipOperationKind
{
    GrantRole = 1,
    ChangeRole = 2,
    EndRole = 3,
    EndMembership = 4
}

public sealed class OrganizationMembershipOperation
{
    private OrganizationMembershipOperation() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid MembershipId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public OrganizationMembershipOperationKind Kind { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public Guid? ResultRoleAssignmentId { get; private set; }
    public DateTime CompletedAtUtc { get; private set; }

    public static OrganizationMembershipOperation Record(Guid organizationId, Guid actorUserId, Guid membershipId,
        Guid clientOperationId, OrganizationMembershipOperationKind kind, string requestHash,
        Guid? resultRoleAssignmentId, DateTime completedAtUtc)
    {
        if (organizationId == Guid.Empty || actorUserId == Guid.Empty || membershipId == Guid.Empty ||
            clientOperationId == Guid.Empty || resultRoleAssignmentId == Guid.Empty || !Enum.IsDefined(kind) ||
            requestHash is null || requestHash.Length != 64 || !requestHash.All(Uri.IsHexDigit) ||
            completedAtUtc.Kind != DateTimeKind.Utc ||
            (kind == OrganizationMembershipOperationKind.EndMembership) != (resultRoleAssignmentId is null))
            throw new ArgumentException("رسید عملیات مدیریت عضویت معتبر نیست.");
        return new()
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, ActorUserId = actorUserId, MembershipId = membershipId,
            ClientOperationId = clientOperationId, Kind = kind, RequestHash = requestHash.ToUpperInvariant(),
            ResultRoleAssignmentId = resultRoleAssignmentId, CompletedAtUtc = completedAtUtc
        };
    }
}
