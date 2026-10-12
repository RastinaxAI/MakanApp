namespace MakanApp.Domain.Messaging;

public sealed class UserContact
{
    public const int MaximumSavedNameLength = 200;

    private UserContact()
    {
    }

    private UserContact(
        Guid id,
        Guid ownerUserId,
        Guid contactUserId,
        string? savedName,
        DateTime createdAtUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        ContactUserId = contactUserId;
        SavedName = savedName;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid ContactUserId { get; private set; }
    public string? SavedName { get; private set; }
    public bool IsFavorite { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static UserContact Create(
        Guid ownerUserId,
        Guid contactUserId,
        string? savedName,
        DateTime createdAtUtc)
    {
        ValidateUsers(ownerUserId, contactUserId);
        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        return new UserContact(
            Guid.NewGuid(),
            ownerUserId,
            contactUserId,
            NormalizeSavedName(savedName),
            createdAtUtc);
    }

    public void Update(string? savedName, bool isFavorite, DateTime updatedAtUtc)
    {
        ValidateUtc(updatedAtUtc, nameof(updatedAtUtc));
        if (updatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAtUtc));
        }

        SavedName = NormalizeSavedName(savedName);
        IsFavorite = isFavorite;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static string? NormalizeSavedName(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > MaximumSavedNameLength)
        {
            throw new ArgumentException("نام ذخیره‌شده مخاطب بیش از حد مجاز است.", nameof(value));
        }

        return normalized;
    }

    private static void ValidateUsers(Guid ownerUserId, Guid contactUserId)
    {
        if (ownerUserId == Guid.Empty || contactUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه مالک و مخاطب الزامی است.");
        }

        if (ownerUserId == contactUserId)
        {
            throw new ArgumentException("کاربر نمی‌تواند خودش را به‌عنوان مخاطب ذخیره کند.");
        }
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان مخاطب باید UTC باشد.", parameterName);
        }
    }
}
