namespace MakanApp.Domain.Messaging;

public sealed class MessagingPrivacySettings
{
    public const PrivacyAudience DefaultDirectMessagesAudience = PrivacyAudience.Everyone;
    public const PrivacyAudience DefaultPhoneNumberVisibilityAudience = PrivacyAudience.Nobody;
    public const PrivacyAudience DefaultPhoneNumberDiscoveryAudience = PrivacyAudience.Nobody;
    public const PrivacyAudience DefaultGroupInvitesAudience = PrivacyAudience.Contacts;

    private MessagingPrivacySettings()
    {
    }

    private MessagingPrivacySettings(
        Guid userId,
        PrivacyAudience directMessagesAudience,
        PrivacyAudience phoneNumberVisibilityAudience,
        PrivacyAudience phoneNumberDiscoveryAudience,
        PrivacyAudience groupInvitesAudience,
        DateTime createdAtUtc)
    {
        UserId = userId;
        DirectMessagesAudience = directMessagesAudience;
        PhoneNumberVisibilityAudience = phoneNumberVisibilityAudience;
        PhoneNumberDiscoveryAudience = phoneNumberDiscoveryAudience;
        GroupInvitesAudience = groupInvitesAudience;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid UserId { get; private set; }
    public PrivacyAudience DirectMessagesAudience { get; private set; }
    public PrivacyAudience PhoneNumberVisibilityAudience { get; private set; }
    public PrivacyAudience PhoneNumberDiscoveryAudience { get; private set; }
    public PrivacyAudience GroupInvitesAudience { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static MessagingPrivacySettings Create(
        Guid userId,
        PrivacyAudience directMessagesAudience,
        PrivacyAudience phoneNumberVisibilityAudience,
        PrivacyAudience phoneNumberDiscoveryAudience,
        PrivacyAudience groupInvitesAudience,
        DateTime createdAtUtc)
    {
        Validate(
            userId,
            directMessagesAudience,
            phoneNumberVisibilityAudience,
            phoneNumberDiscoveryAudience,
            groupInvitesAudience,
            createdAtUtc);
        return new MessagingPrivacySettings(
            userId,
            directMessagesAudience,
            phoneNumberVisibilityAudience,
            phoneNumberDiscoveryAudience,
            groupInvitesAudience,
            createdAtUtc);
    }

    public void Update(
        PrivacyAudience directMessagesAudience,
        PrivacyAudience phoneNumberVisibilityAudience,
        PrivacyAudience phoneNumberDiscoveryAudience,
        PrivacyAudience groupInvitesAudience,
        DateTime updatedAtUtc)
    {
        Validate(
            UserId,
            directMessagesAudience,
            phoneNumberVisibilityAudience,
            phoneNumberDiscoveryAudience,
            groupInvitesAudience,
            updatedAtUtc);
        if (updatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAtUtc));
        }

        DirectMessagesAudience = directMessagesAudience;
        PhoneNumberVisibilityAudience = phoneNumberVisibilityAudience;
        PhoneNumberDiscoveryAudience = phoneNumberDiscoveryAudience;
        GroupInvitesAudience = groupInvitesAudience;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static void Validate(
        Guid userId,
        PrivacyAudience directMessagesAudience,
        PrivacyAudience phoneNumberVisibilityAudience,
        PrivacyAudience phoneNumberDiscoveryAudience,
        PrivacyAudience groupInvitesAudience,
        DateTime timestampUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("شناسه کاربر تنظیمات حریم خصوصی الزامی است.", nameof(userId));
        }

        if (!Enum.IsDefined(directMessagesAudience) ||
            !Enum.IsDefined(phoneNumberVisibilityAudience) ||
            !Enum.IsDefined(phoneNumberDiscoveryAudience) ||
            !Enum.IsDefined(groupInvitesAudience))
        {
            throw new ArgumentOutOfRangeException(nameof(directMessagesAudience), "مخاطب حریم خصوصی معتبر نیست.");
        }

        if (timestampUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان تنظیمات حریم خصوصی باید UTC باشد.", nameof(timestampUtc));
        }
    }
}
