namespace MakanApp.Domain.Identity;

public sealed class UserSession
{
    private UserSession()
    {
    }

    private UserSession(
        Guid id,
        Guid userId,
        byte[] tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime absoluteExpiresAtUtc,
        string? deviceName)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        AbsoluteExpiresAtUtc = absoluteExpiresAtUtc;
        DeviceName = deviceName;
        AssuranceLevel = AuthenticationAssuranceLevel.Primary;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime AbsoluteExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? DeviceName { get; private set; }
    public AuthenticationAssuranceLevel AssuranceLevel { get; private set; }
    public DateTime? StepUpAtUtc { get; private set; }
    public Guid? SelectedMembershipId { get; private set; }
    public string? SelectedRole { get; private set; }
    public Guid? SelectedSubjectOrganizationPersonId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static UserSession Create(
        Guid userId,
        byte[] tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc) =>
        new(Guid.NewGuid(), userId, tokenHash, createdAtUtc, expiresAtUtc, expiresAtUtc, null);

    public static UserSession Create(
        Guid userId,
        byte[] tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime absoluteExpiresAtUtc,
        string? deviceName)
    {
        if (userId == Guid.Empty || tokenHash.Length != 32)
        {
            throw new ArgumentException("مشخصات نشست معتبر نیست.");
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));
        EnsureUtc(absoluteExpiresAtUtc, nameof(absoluteExpiresAtUtc));
        if (expiresAtUtc <= createdAtUtc || absoluteExpiresAtUtc < expiresAtUtc)
        {
            throw new ArgumentException("انقضای مطلق نشست نمی‌تواند پیش از access token باشد.");
        }

        return new UserSession(
            Guid.NewGuid(),
            userId,
            tokenHash,
            createdAtUtc,
            expiresAtUtc,
            absoluteExpiresAtUtc,
            deviceName);
    }

    public bool IsActive(DateTime nowUtc) =>
        !RevokedAtUtc.HasValue && nowUtc < ExpiresAtUtc && nowUtc < AbsoluteExpiresAtUtc;

    public void RotateAccessToken(byte[] tokenHash, DateTime expiresAtUtc)
    {
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));
        if (RevokedAtUtc.HasValue || tokenHash.Length != 32 || expiresAtUtc > AbsoluteExpiresAtUtc)
        {
            throw new InvalidOperationException("نشست قابل چرخش نیست.");
        }

        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void RecordStepUp(AuthenticationAssuranceLevel assuranceLevel, DateTime stepUpAtUtc)
    {
        EnsureUtc(stepUpAtUtc, nameof(stepUpAtUtc));
        if (assuranceLevel < AuthenticationAssuranceLevel.MultiFactor)
        {
            throw new ArgumentOutOfRangeException(nameof(assuranceLevel));
        }

        AssuranceLevel = assuranceLevel;
        StepUpAtUtc = stepUpAtUtc;
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        EnsureUtc(revokedAtUtc, nameof(revokedAtUtc));
        RevokedAtUtc ??= revokedAtUtc;
    }

    public void SelectPersonalWorkspace()
    {
        SelectedMembershipId = null;
        SelectedRole = null;
        SelectedSubjectOrganizationPersonId = null;
    }

    public void SelectOrganizationWorkspace(Guid membershipId, string role)
    {
        SelectedMembershipId = membershipId;
        SelectedRole = role;
        SelectedSubjectOrganizationPersonId = null;
    }

    public void SelectSubjectOrganizationPerson(Guid organizationPersonId)
    {
        SelectedSubjectOrganizationPersonId = organizationPersonId;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان نشست باید UTC باشد.", parameterName);
        }
    }
}
