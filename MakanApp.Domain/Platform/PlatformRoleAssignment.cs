namespace MakanApp.Domain.Platform;

public enum PlatformRoleAssignmentStatus
{
    Active = 1,
    Ended = 2
}

public sealed class PlatformRoleAssignment
{
    private PlatformRoleAssignment()
    {
    }

    private PlatformRoleAssignment(
        Guid id,
        Guid userId,
        Guid roleId,
        Guid? grantedByUserId,
        DateTime grantedAtUtc)
    {
        Id = id;
        UserId = userId;
        RoleId = roleId;
        GrantedByUserId = grantedByUserId;
        Status = PlatformRoleAssignmentStatus.Active;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid? GrantedByUserId { get; private set; }
    public PlatformRoleAssignmentStatus Status { get; private set; }
    public DateTime GrantedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public Guid? EndedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive =>
        Status == PlatformRoleAssignmentStatus.Active && !EndedAtUtc.HasValue;

    public static PlatformRoleAssignment CreateActive(
        Guid userId,
        Guid roleId,
        Guid? grantedByUserId,
        DateTime grantedAtUtc)
    {
        if (userId == Guid.Empty || roleId == Guid.Empty ||
            grantedByUserId.HasValue && grantedByUserId.Value == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های انتساب نقش پلتفرم معتبر نیستند.");
        }

        EnsureUtc(grantedAtUtc, nameof(grantedAtUtc));
        return new PlatformRoleAssignment(
            Guid.NewGuid(),
            userId,
            roleId,
            grantedByUserId,
            grantedAtUtc);
    }

    public void End(Guid endedByUserId, DateTime endedAtUtc)
    {
        if (endedByUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه عامل پایان نقش معتبر نیست.", nameof(endedByUserId));
        }

        EnsureUtc(endedAtUtc, nameof(endedAtUtc));
        if (!IsActive)
        {
            return;
        }

        Status = PlatformRoleAssignmentStatus.Ended;
        EndedAtUtc = endedAtUtc;
        EndedByUserId = endedByUserId;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان انتساب نقش پلتفرم باید UTC باشد.", parameterName);
        }
    }
}
