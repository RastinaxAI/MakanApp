using MakanApp.Domain.Organization;

namespace MakanApp.Domain.Intelligence;

public enum AssistantDataContextKind { Personal = 1, Organization = 2, OrganizationClass = 3 }
public sealed class AssistantContextSelection
{
    private AssistantContextSelection() { }
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public AssistantDataContextKind Kind { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? MembershipId { get; private set; }
    public OrganizationRole? Role { get; private set; }
    public Guid? SubjectOrganizationPersonId { get; private set; }
    public Guid? ClassId { get; private set; }
    public Guid LastClientOperationId { get; private set; }
    public DateTime SelectedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static AssistantContextSelection Create(Guid user, Guid session) => new() { UserId = user, SessionId = session };
    public void Select(AssistantDataContextKind kind, Guid? organization, Guid? membership, OrganizationRole? role, Guid? subject, Guid? classId, Guid operation, DateTime now)
    {
        if (!Enum.IsDefined(kind) || operation == Guid.Empty || now.Kind != DateTimeKind.Utc ||
            kind == AssistantDataContextKind.Personal && (organization.HasValue || membership.HasValue || role.HasValue || subject.HasValue || classId.HasValue) ||
            kind != AssistantDataContextKind.Personal && (organization is null || membership is null || role is null) ||
            (kind == AssistantDataContextKind.OrganizationClass) != classId.HasValue)
            throw new ArgumentException("انتخاب زمینهٔ Assistant معتبر نیست.");
        Kind = kind; OrganizationId = organization; MembershipId = membership; Role = role;
        SubjectOrganizationPersonId = subject; ClassId = classId; LastClientOperationId = operation; SelectedAtUtc = now;
    }
}
