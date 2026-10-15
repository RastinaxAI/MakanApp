namespace MakanApp.Domain.Identity;

public sealed class User
{
    private User()
    {
    }

    private User(Guid id, DateTime createdAtUtc)
    {
        Id = id;
        AccountStatus = UserAccountStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? PersonId { get; private set; }
    public string? Username { get; private set; }
    public string? NormalizedUsername { get; private set; }
    public CommunicationAgeCategory CommunicationAgeCategory { get; private set; }
    public CommunicationAgeVerificationStatus CommunicationAgeVerificationStatus { get; private set; }
    public DateTime? MessagingRestrictedUntilUtc { get; private set; }
    public UserAccountStatus AccountStatus { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public DateTime? ProfileCompletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsProfileComplete =>
        PersonId.HasValue && !string.IsNullOrWhiteSpace(Username);

    public static User Create(DateTime createdAtUtc) =>
        new(Guid.NewGuid(), createdAtUtc);

    public void Suspend(DateTime suspendedAtUtc)
    {
        if (suspendedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان تعلیق حساب باید UTC باشد.", nameof(suspendedAtUtc));
        }

        if (AccountStatus == UserAccountStatus.Suspended)
        {
            return;
        }

        AccountStatus = UserAccountStatus.Suspended;
        SuspendedAtUtc = suspendedAtUtc;
        UpdatedAtUtc = suspendedAtUtc;
    }

    public void Reactivate(DateTime reactivatedAtUtc)
    {
        if (reactivatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان فعال‌سازی حساب باید UTC باشد.", nameof(reactivatedAtUtc));
        }

        if (AccountStatus == UserAccountStatus.Active)
        {
            return;
        }

        AccountStatus = UserAccountStatus.Active;
        SuspendedAtUtc = null;
        UpdatedAtUtc = reactivatedAtUtc;
    }

    public void SetCommunicationAgeCategory(
        CommunicationAgeCategory communicationAgeCategory,
        DateTime updatedAtUtc)
    {
        if (updatedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("زمان تأیید سن باید UTC باشد.", nameof(updatedAtUtc));
        if (!Enum.IsDefined(communicationAgeCategory))
        {
            throw new ArgumentOutOfRangeException(nameof(communicationAgeCategory));
        }

        CommunicationAgeCategory = communicationAgeCategory;
        CommunicationAgeVerificationStatus = communicationAgeCategory == CommunicationAgeCategory.Unknown
            ? CommunicationAgeVerificationStatus.Unverified : CommunicationAgeVerificationStatus.Verified;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void MarkCommunicationAgeConflict(DateTime nowUtc)
    {
        SetCommunicationAgeCategory(CommunicationAgeCategory.Unknown, nowUtc);
        CommunicationAgeVerificationStatus = CommunicationAgeVerificationStatus.Conflict;
    }

    public void RestrictMessaging(DateTime untilUtc, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc || untilUtc.Kind != DateTimeKind.Utc || untilUtc <= nowUtc)
            throw new ArgumentException("زمان پایان محدودیت باید UTC و در آینده باشد.");
        MessagingRestrictedUntilUtc = MessagingRestrictedUntilUtc > untilUtc ? MessagingRestrictedUntilUtc : untilUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void SetProfile(
        Guid personId,
        string username,
        string normalizedUsername,
        DateTime updatedAtUtc)
    {
        PersonId = personId;
        Username = username;
        NormalizedUsername = normalizedUsername;
        UpdatedAtUtc = updatedAtUtc;
        ProfileCompletedAtUtc ??= updatedAtUtc;
    }
}
